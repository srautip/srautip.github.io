# Auf einen frischen Ubuntu-VPS bringen

Getestet gegen Ubuntu 26.04. Rechnet mit rund zehn Minuten, davon acht
Warten.

## Was der Server braucht

Gemessen im Betrieb, nicht geschätzt — die kleinste Ausstattung reicht:

| | Bedarf | Anmerkung |
|---|---|---|
| Arbeitsspeicher | **10–13 MB** Heap, mit Node und Caddy zusammen unter 300 MB | 1 GB ist reichlich |
| Rechenleistung | dauerhaft im niedrigen einstelligen Prozentbereich | 1 vCPU genügt |
| Platte | ~500 MB Historie + ~400 MB Images | 10 GB sind bequem |
| **Verkehr** | **~0,8 GB/Tag ≈ 25 GB/Monat** eingehend | Das ist der einzige Wert, der bei manchen Tarifen eng wird — vorher im Tarif nachsehen |

## Vorher: der DNS-Eintrag

**Das Skript legt ihn nicht an und kann es auch nicht** — der Eintrag liegt
beim DNS-Anbieter der Domain, nicht auf dem Server. Er ist die einzige
Handarbeit vorweg.

1. Öffentliche IP des VPS notieren (im Panel oder auf dem Server mit
   `hostname -I`).
2. Beim DNS-Anbieter der Domain einen **A-Record** anlegen:

   | Typ | Name | Wert | TTL |
   |---|---|---|---|
   | `A` | `ais` | `<IP des VPS>` | 300 |

   Der Name ist meist nur die Subdomain (`ais`), nicht der volle
   `ais.deinedomain.de` — das hängt vom Anbieter ab.
3. Warten, bis er greift, und nachsehen:

   ```bash
   getent ahostsv4 ais.deinedomain.de
   ```

   `getent` statt `dig`: **`dig` ist auf einem frischen Ubuntu nicht
   installiert** (steckt in `dnsutils`), `getent` gehört zur glibc.

Das Skript prüft das beim Start und **bricht mit einer erklärenden Meldung
ab**, wenn der Name nicht auflöst — sonst liefe die ganze Einrichtung durch
und erst Caddy scheiterte danach still am Zertifikat. Überspringen mit
`AIS_DNS_EGAL=1` davor.

**Kein A-Record nötig,** wenn der Anbieter schon einen Namen mitliefert (bei
clouding.io etwa `<uuid>.clouding.host`) — den einfach benutzen. Und ganz
ohne Domain: `<ip-mit-bindestrichen>.sslip.io` löst bauartbedingt auf die IP
im Namen auf, ohne jede DNS-Arbeit, und Caddy bekommt dafür ein echtes
Zertifikat.

Bei clouding.io kommt der Server ohne Firewall-Regeln hoch; die Absicherung
macht `ufw` auf dem Server selbst, das erledigt das Skript. Falls im Panel
eine zusätzliche Firewall aktiv ist, müssen dort **80** und **443** offen
sein — sonst bekommt Caddy kein Zertifikat.

## Der schnelle Weg

Als `root` auf dem frischen Server:

```bash
curl -fsSL https://raw.githubusercontent.com/srautip/srautip.github.io/main/aisproxy/deploy.sh \
  | bash -s -- ais.deinedomain.de
```

Das Skript installiert Docker, holt **nur** das Verzeichnis `aisproxy/` aus
dem Monorepo (216 KB statt allem), würfelt Passwort und Zugangstoken aus,
richtet die Firewall ein, startet beides und wartet, bis der Dienst wirklich
Schiffe meldet. Am Ende stehen die Zugangsdaten auf dem Schirm und in
`/opt/aisproxy/zugangsdaten.txt`.

Probe:

```bash
curl -u skipper:<passwort> https://ais.deinedomain.de/v1/status
```

## Derselbe Weg von Hand

Falls man lieber sieht, was passiert — es sind fünf Schritte:

```bash
# 1  Docker
apt-get update && apt-get install -y ca-certificates curl git ufw
curl -fsSL https://get.docker.com | sh

# 2  Nur das Proxy-Verzeichnis holen. Der Klon BLEIBT stehen, /opt/aisproxy
#    ist nur ein Verweis darauf - sonst gäbe es später kein `git pull`.
git clone --depth 1 --sparse https://github.com/srautip/srautip.github.io.git /opt/aisproxy-src
git -C /opt/aisproxy-src sparse-checkout set aisproxy
ln -sfn /opt/aisproxy-src/aisproxy /opt/aisproxy

# 3  Zugangsdaten
cd /opt/aisproxy
cp .env.beispiel .env
docker run --rm caddy:2-alpine caddy hash-password --plaintext 'DEIN-PASSWORT'
$EDITOR .env          # AIS_DOMAIN, AIS_BENUTZER, AIS_PASSWORT_HASH eintragen

# 4  Firewall
ufw allow OpenSSH && ufw allow 80/tcp && ufw allow 443/tcp && ufw --force enable

# 5  Los
docker compose up -d --build
docker compose logs -f proxy
```

Im Protokoll soll innerhalb einer Minute stehen:

```
Token geholt: conns=2 rate=50 area=400 mmsis=50
Stream verbunden, Region 21.0 sq
```

## Ohne eigene Domain

Caddy bekommt für eine nackte IP kein Zertifikat. Zwei Möglichkeiten:

**a) Nur über SSH erreichbar** — am sichersten, und für einen Proxy, den nur
man selbst benutzt, oft genug:

```bash
# In der docker-compose.yml bei proxy einkommentieren:
#   ports: ["127.0.0.1:8080:8080"]
# und den caddy-Dienst weglassen. Dann vom eigenen Rechner:
ssh -L 8080:127.0.0.1:8080 root@<ip>
# -> http://localhost:8080/v1/status
```

**b) Selbst signiert**: `AIS_DOMAIN` auf die IP setzen und in der `Caddyfile`
`tls internal` ergänzen. Der Browser warnt dann bei jedem Aufruf — für einen
Dienst, den der AIS-Client per `fetch` anspricht, ist das unpraktisch.

## Läuft es?

```bash
cd /opt/aisproxy
docker compose ps                       # beide Dienste "running"
docker compose logs --tail=30 proxy
curl -s localhost:8080/v1/status | head -c 400    # nur von der Maschine selbst
```

Die zwei Zahlen, auf die es ankommt, stehen in `/v1/status`:

- **`strom.rateSpitze` gegen `strom.rateLimit`** — gemessen 37,5 von 50 msg/s.
  Nur 25 % Luft, und über der Grenze drosselt der Upstream **kommentarlos**.
  Der Proxy warnt ab 42 msg/s im Protokoll.
- **`netz.letzteNeu`** — wie viele Schiffe der Snapshot beisteuert, die der
  Stream nicht gezeigt hat.

## Der Browser-Client braucht das Token, nicht das Passwort

Ein Browser kann bei einem WebSocket keine Header setzen — Basic-Auth ist für
`/v1/live` also gar nicht erfüllbar. Deshalb deckt Basic-Auth in der
`Caddyfile` **nur** noch ab, was ein Mensch aufruft (`/v1/status`, die
Startseite). Die Schnittstellenpfade schützt das Token des Proxys selbst
(`AIS_ZUGANG`), das der Client als `?token=` mitschickt.

Auf der Technikseite des Clients gehört also **nur das Token** aus
`zugangsdaten.txt` hinein. Benutzername und Passwort sind für den
Browseraufruf und für `curl` da:

```bash
curl -u skipper:<passwort> https://<domain>/v1/status
```

## Aktualisieren

```bash
cd /opt/aisproxy && git pull && docker compose up -d --build
```

Die Historie liegt im Volume `daten` und übersteht das.

### Wenn dort „not a git repository" steht

Dann stammt die Installation aus einer frühen Fassung des Skripts, die das
Verzeichnis aus dem Klon herausschob und den Klon samt `.git` löschte.
Einmalig umstellen — die Zugangsdaten bleiben erhalten:

```bash
git clone --depth 1 --sparse https://github.com/srautip/srautip.github.io.git /opt/aisproxy-src
git -C /opt/aisproxy-src sparse-checkout set aisproxy
cp /opt/aisproxy/.env /opt/aisproxy/zugangsdaten.txt /opt/aisproxy-src/aisproxy/
cd /opt/aisproxy && docker compose down
cd /opt && mv aisproxy aisproxy.alt && ln -sfn /opt/aisproxy-src/aisproxy /opt/aisproxy
cd /opt/aisproxy && docker compose up -d --build
```

Dasselbe macht ein erneuter Lauf von `deploy.sh` inzwischen von selbst. Die
Volumes überleben, weil der Projektname in der `docker-compose.yml` fest
steht (`name: aisproxy`) und nicht aus dem Verzeichnisnamen abgeleitet wird.
Läuft alles, kann `/opt/aisproxy.alt*` weg.

## Sichern

Es gibt genau eine Datei, die zählt:

```bash
docker compose exec proxy sh -c 'cp /daten/ais.db /daten/sicherung.db'
docker compose cp proxy:/daten/sicherung.db ./ais-$(date +%F).db
```

Die Fotos darunter sind Wikimedia-Bilder und jederzeit neu holbar — sie zu
sichern lohnt nicht.

## Wenn es klemmt

| Symptom | Ursache in aller Regel |
|---|---|
| Caddy bekommt kein Zertifikat | DNS zeigt noch nicht auf den Server, oder Port 80 ist zu. Mit `getent ahostsv4 <domain>` prüfen (`dig` ist auf einem frischen Ubuntu nicht installiert) |
| `Stream verbunden`, aber `schiffe: 0` | Region größer als 400 sq° — der Upstream verwirft die Subscription dann **ohne Fehlermeldung**. Der Proxy warnt beim Start, wenn er es bemerkt |
| `Cannot find module 'node:sqlite'` | Node älter als 22.5. Im Image kann das nicht passieren; bei einer Installation ohne Docker Node auf 22.22+ heben |
| Basic-Auth wird abgewiesen, auch mit richtigem Passwort | Der bcrypt-Hash steckt voller `$`, und Docker Compose deutet `$` in der `.env` als Variable — der Hash kommt zerstückelt am Container an. Prüfen mit `docker compose config \| grep PASSWORT`; im `.env` jedes `$` verdoppeln (`$$`) |
| `git pull` sagt „not a git repository" | Installation aus einer frühen Skriptfassung ohne `.git`. Siehe *Aktualisieren* — einmalig umstellen, Zugangsdaten bleiben |
| Der Client verbindet sich nicht, obwohl das Token stimmt | Alte `Caddyfile` mit Basic-Auth über allen Pfaden. Ein Browser kann bei einem WebSocket keine Header setzen, also kommt er nie durch. `git pull && docker compose up -d --build` |
| Alles läuft, aber der Browser bekommt nichts | Ohne `Access-Control-Allow-Origin` kommt keine Antwort an — der Proxy setzt ihn selbst, aber ein vorgeschalteter Reverse-Proxy darf ihn nicht abschneiden |

## Was hier nicht getestet ist

**Docker selbst.** In der Entwicklungsumgebung lief kein Daemon, `docker
build` konnte also nie laufen. Geprüft sind:

- die Syntax von `deploy.sh` (`bash -n`),
- der Sparse-Checkout gegen das echte Repo (216 KB, alle Dateien da),
- die Produktionsinstallation (`npm install --omit=dev` zieht nur `ws`),
- **ein vollständiger Start aus diesem frischen Checkout heraus**: 3 070
  Schiffe nach 20 Sekunden, Token-Sperre greift, 135 KB gzip auf der Leitung,
  10,4 MB Heap,
- der Healthcheck-Befehl aus dem `Dockerfile` (Exitcode 0 bei laufendem
  Dienst).

Damit ist alles außer der Container-Schicht selbst erprobt. Beim ersten
`docker compose up -d --build` also einmal ins Protokoll sehen.
