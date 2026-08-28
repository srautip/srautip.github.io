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

**Für den Browser-Client reicht das aber nicht.** Gemessen in Chromium: Ein
`fetch` von der ausgelieferten Seite (`https://srautip.github.io`) nach
`http://127.0.0.1:<port>` **hängt** — keine Antwort, kein Fehler; mit
`http://localhost` genauso. Der WebSocket dorthin kommt zustande, der Abruf
nicht. Der Tunnel taugt also zum Nachsehen von Hand, nicht als Datenweg für
den Client. Dafür braucht es `https`, und das ist mit `sslip.io` in einer
Minute erledigt.

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
/opt/aisproxy/update.sh
```

Das ist alles. Beim allerersten Mal muss das Skript erst ins Verzeichnis
kommen: `cd /opt/aisproxy && git pull && ./update.sh` — danach holt es sich
selbst.

Was es tut, und warum es mehr ist als `git pull && docker compose up`:

1. **Stand notieren** — Git-Commit und `/v1/status` (Positionen, Tagestabellen,
   Stammdaten).
2. **Aufräumen, Platz prüfen, sichern.** In dieser Reihenfolge:
   - Alte Stände weg, bis einer weniger übrig ist als `AIS_BEHALTEN` erlaubt —
     **der jüngste bleibt immer stehen**, auch bei `AIS_BEHALTEN=1`. Ohne
     Sicherung dazustehen ist das eine, was während eines Updates nicht
     passieren darf.
   - Dann nachrechnen: Ist auf `/opt/aisproxy-sicherungen` **und** unter dem
     Docker-Verzeichnis je das 1,3-fache der Datenbankgröße frei? Sonst
     Abbruch, bevor irgendetwas geschrieben wird. Beide Seiten brauchen den
     Platz, weil `VACUUM INTO` die Sicherung erst im Volume anlegt und sie
     danach herauskopiert wird.
   - `VACUUM INTO`, danach wird die Sicherung sofort wieder geöffnet und
     gezählt. Dazu `.env` und `zugangsdaten.txt`. Alles nach
     `/opt/aisproxy-sicherungen` (Modus 700, die Dateien 600). Scheitert hier
     etwas, wird **nichts** aktualisiert.
3. `git pull --ff-only`, dann prüfen, ob die Zugangsdaten noch dieselben sind —
   sonst werden sie aus der Sicherung von eben zurückgeschrieben, **vor** dem
   Neubau.
4. `docker compose up -d --build`.
5. Warten, bis `/v1/status` wieder Schiffe meldet (bis 180 s).
6. **Gegenlesen:** Sind es mindestens so viele Positionen wie vorher? Sonst
   Abbruch mit Exitcode 1 und dem Weg zurück. Einzige erlaubte Ausnahme: Die
   Zahl der Tagestabellen ist gesunken — dann ist eine Tagestabelle nach
   `AIS_HISTORIE_TAGE` ausgelaufen, das ist Pflege und kein Verlust.

Zum Schluss stehen der Registerstand (Fotos, offene, Bildwege) und die Pfade
der Sicherungen da. Aufgeräumt wird **vor** der Sicherung, nicht danach:
Erst 146 MB dazuzulegen und dann zu räumen hebt den Spitzenbedarf um einen
ganzen Stand — auf einer knappen Platte genau der Unterschied. Datenbank und
zugehörige Zugangsdaten gehen dabei zusammen.

Stellschrauben als Umgebungsvariablen: `AIS_ZIEL`, `AIS_SICHERUNGEN`,
`AIS_BEHALTEN`, `AIS_WARTE_RUNDEN` (× 5 s).

Die Historie liegt im Volume `aisproxy_daten` und übersteht den Neubau ohnehin
— der Projektname steht in der `docker-compose.yml` fest. Der einzige Befehl,
der sie wirklich löscht, ist `docker compose down -v`; er kommt in keinem
Skript dieses Projekts vor.

Von Hand geht es weiterhin:

```bash
cd /opt/aisproxy && git pull && docker compose up -d --build
```

Dann gibt es nur eben keine Sicherung davor und keine Gegenprobe danach.

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

## Eigene Schiffsbilder nachtragen

Die automatischen Quellen decken gemessen 65 von 134 Schiffen ab. Der Rest
geht von Hand — und zwar so, dass der Browser das Bild holt und der Proxy es
nur ablegt:

- **Einzeln:** Schiff im Client anklicken, beim Platzhalter „Bild hinzufügen"
  wählen — oder ein Bild kopieren und mit **Strg+V** einfügen.
- **Stapelweise:** Dateien nach der MMSI benennen (`211224140.jpg`) und in den
  Client ziehen.
- **Ganzer Ordner:**

  ```bash
  ./bilder-hochladen.sh ~/schiffsbilder https://<domain>/v1 <token>
  ```

Der Proxy prüft jede Datei: Token, Größengrenze (`AIS_FOTO_UPLOAD_MAX`, 6 MB,
geprüft **während** des Lesens) und die ersten Bytes — nur echtes JPEG oder
PNG. Ein selbst beigesteuertes Bild trägt `foto_quelle: "eigen"` und wird vom
automatischen Fotolauf nicht mehr angefasst.

## Die Ortsliste

Der Proxy löst UN/LOCODEs für den Client auf (`/v1/ort?codes=DEHAM,BEANR`).
Die Liste holt er sich beim ersten Registerlauf selbst — 7 MB CSV von der
UNECE, daraus 17 596 Seehäfen, Dauer rund eine Sekunde. Danach bleibt sie
90 Tage still (`AIS_ORT_ABZUG_MS`). Im Status steht der Stand unter
`register.orte`.

## Sichern

`update.sh` macht das bei jedem Lauf mit. Von Hand:

```bash
cd /opt/aisproxy
docker compose exec -T proxy node -e '
  const { DatabaseSync } = require("node:sqlite");
  const d = new DatabaseSync("/daten/ais.db");
  d.exec("VACUUM INTO \x27/daten/sicherung.db\x27");' < /dev/null
docker compose cp proxy:/daten/sicherung.db ./ais-$(date +%F).db
docker compose exec -T proxy rm /daten/sicherung.db < /dev/null
cp .env ./env-$(date +%F)          # ohne die kommt niemand mehr an den Dienst
```

**Nicht `cp /daten/ais.db`.** Hier stand das vorher, und es ist falsch: Die
Datenbank läuft im WAL-Modus, alles seit dem letzten Checkpoint steht in
`ais.db-wal`. Nachgemessen an einer Datenbank mit 5 000 frisch geschriebenen
Zeilen und offenem Schreiber:

| | Positionen in der Sicherung |
|---|---|
| `cp ais.db` | **0** von 5 000 (die Hauptdatei war 4 KB, das WAL 190 KB) |
| `VACUUM INTO` | **5 000** von 5 000 |

Die `cp`-Sicherung sah dabei wie eine richtige Sicherung aus. Steht der Dienst
gerade still, geht ein Kopieren doch — dann aber `ais.db` **samt** `-wal` und
`-shm`, und beim Zurückspielen alle drei zusammen (gegengeprüft: liest 5 000
Positionen zurück).

Die Fotos darunter sind Wikimedia- und Flickr-Bilder und jederzeit neu holbar
— sie zu sichern lohnt nicht.

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
| `update.sh` bricht mit „das sieht nach Verlust aus" ab | Der Bestand ist kleiner als vor dem Update, ohne dass eine Tagestabelle ausgelaufen wäre. Das Skript hat den Weg zurück ausgegeben; die Sicherung von wenigen Minuten vorher liegt unter `/opt/aisproxy-sicherungen` |
| `update.sh` meldet „Nach 180 s meldet der Dienst noch keine Schiffe", der Client läuft aber | Fassung vom ersten Tag: `/v1/status` liegt hinter der Tokenprüfung, die Statusabfrage schickte keins und bekam 401. Behoben — `cd /opt/aisproxy && git pull && ./update.sh`. Dieselbe Ursache hatte der dauerhaft rote Healthcheck (`docker compose ps` zeigte `unhealthy`) |
| Der Proxy startet nicht mehr, im Protokoll `disk I/O error` in `speicher.js` | Das ist `PRAGMA journal_mode = WAL`, der erste **Schreib**zugriff: SQLite kann `/daten/ais.db-wal` nicht anlegen. Zwei Ursachen — **Platte voll** (`df -h /`, `df -i /`) oder **Rechte** in `/daten` (der Container läuft als UID 1000): `docker run --rm -v aisproxy_daten:/daten alpine:3 chown -R 1000:1000 /daten` |
| `update.sh` bricht mit „Zu wenig Platz" ab | Genau dafür ist die Prüfung da: Es wurde **nichts** gesichert und nichts aktualisiert, der Dienst läuft weiter. Platz schaffen (`docker system prune -f`, alte Sicherungen bis auf die jüngste), dann erneut starten |
| `update.sh` sagt „In /opt/aisproxy fehlt die .env" | Die Zugangsdaten fehlen schon vor dem Update — es wird bewusst nichts angefasst. Aus `/opt/aisproxy-sicherungen/env-*` zurückkopieren; gibt es keine, legt ein erneuter Lauf von `deploy.sh` neue an, und der Client braucht dann das neue Token |

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

**`update.sh`** ist aus demselben Grund gegen eine Docker-Attrappe geprüft,
nicht gegen echtes Docker — dafür aber jeder Zweig einzeln, denn ein Zweig,
der nie feuert, beweist nichts:

| Zweig | Ergebnis |
|---|---|
| guter Lauf | sichert, zieht `git pull`, startet, „die Historie ist vollständig da" |
| `VACUUM INTO` aus einem offenen WAL | 5 000 von 5 000 Positionen (der Codeschnipsel wurde dafür aus `update.sh` herausgeschnitten, nicht nachgebaut) |
| Positionen weg, Tage gleich | Exit 1 mit Rückweg |
| Tagestabelle ausgelaufen | Exit 0, „normale Pflege" |
| Dienst meldet keine Schiffe / Bau scheitert | Exit 1 mit Rückweg |
| Sicherung leer | Abbruch **vor** `git pull`, Commit unverändert |
| `.env` fehlt schon vorher | Abbruch, kein Byte angefasst |
| `.env` verschwindet oder ändert sich beim Pull | aus der Sicherung wiederhergestellt (gleiche Prüfsumme, Modus 600), die vorgefundene Fassung bleibt als `*.abweichend` liegen |
| Dienst steht still | Kopie aus dem Volume samt `-wal`/`-shm`; zurückgespielt liest sie 5 000 Positionen |
| Aufräumen | alte Stände weg, Datenbank und Zugangsdaten desselben Standes zusammen; bei `AIS_BEHALTEN=1` bleibt der jüngste vorhandene Stand stehen und der neue kommt dazu |
| Zu wenig Platz | gegen ein 20-MB-Dateisystem gemessen: Abbruch mit Exitcode 1 **vor** der Sicherung, Git-Stand unverändert, die vorhandene Sicherung unangetastet |
| Dienst steht | Kopie über `docker compose cp` aus dem gestoppten Container — ohne fremdes Image, denn auf einer vollen Platte scheiterte vorher zuerst der `alpine`-Abruf und die Meldung handelte dann vom Image statt vom Platz |
| Status hinter der Tokenprüfung | mit Token aus der Containerumgebung: Zahlen und Code 0; ohne: Code 2, eigene Meldung statt „keine Schiffe", Abbruch nach 7 s statt 180 s — gemessen gegen den echten `Server` aus `src/server.js`, nicht gegen eine Attrappe |
