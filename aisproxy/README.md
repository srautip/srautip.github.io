# aisproxy

Caching-Proxy für AIS-Schiffsdaten. Bündelt eine Upstream-Verbindung für alle
Clients, hält sieben Tage Historie vor und beliefert den Browser mit Deltas
statt Vollbildern.

**Region:** Deutsche Bucht + westliche Ostsee (53–56 N, 6–13 E, 21 sq°),
gemessen rund 3 000 Schiffe.

## Was es kostet (gemessen, nicht geschätzt)

| | Wert |
|---|---|
| Upstream-Last gesamt | **0,81 GB/Tag** |
| davon Livestream (`permessage-deflate`) | 0,61 GB/Tag bei 37,5 msg/s |
| davon Snapshot-Netz (60 s) | 0,20 GB/Tag, 136 KB je Abruf auf der Leitung |
| Client: Erstbild eines Ausschnitts | ~100 KB (davon 90 % Namen, einmalig) |
| Client: laufender Betrieb | **6,4 KB/min** |
| Historie, 7 Tage | ~200 MB, mit Index unter 500 MB |
| Arbeitsspeicher | 13 MB Heap |

Zum Vergleich: Ein Client, der denselben Ausschnitt direkt beim Upstream
abholt, lädt bei jedem Abruf ein Vollbild.

## Schnellstart

```bash
npm install
npm start                 # hört auf Port 8080
curl localhost:8080/v1/status
```

Ohne `AIS_TOKEN` holt sich der Proxy beim Start selbst einen kostenlosen
openwaters-Token — genau wie der „Token holen"-Knopf im Client.

### Auf einem Server

Ein Befehl auf einem frischen Ubuntu-VPS:

```bash
curl -fsSL https://raw.githubusercontent.com/srautip/srautip.github.io/main/aisproxy/deploy.sh \
  | bash -s -- ais.deinedomain.de
```

Ausführlich, samt Bedarf, Sicherung und dem, was nicht getestet ist:
**[DEPLOY.md](DEPLOY.md)**.

## Schnittstelle

```
GET  /v1/snapshot?bbox=53,6,56,13          Vollbild + aktuelle rev
WS   /v1/live?bbox=…&takt=2000             nur Änderungen
GET  /v1/replay?bbox=…&von=&bis=&schritt=  Stützpunktfolgen für die Animation
GET  /v1/track?mmsi=&von=&bis=             eine Spur in voller Auflösung
GET  /v1/ship/{mmsi}                       Stammdaten, Register, Foto
GET  /v1/foto/{datei}                      das Bild selbst
GET  /v1/status                            Ratenwächter und Zähler
```

`bbox` ist immer `latMin,lonMin,latMax,lonMax` — Breite zuerst, wie beim
Upstream.

### Der Live-Kanal

Nach dem Verbinden kommen drei Arten von Nachrichten:

- **JSON `{typ:"stamm", schiffe:[…]}`** — Name, Rufzeichen, IMO, Maße. Geht
  nur **einmal je MMSI** hinüber.
- **Binär, 20 Byte je Schiff** — Position, Fahrt, Kurs. Aufbau siehe
  `src/draht.js`. Der Rahmen trägt die `rev` im Kopf.
- **JSON `{typ:"weg", mmsi:[…]}`** — was den Ausschnitt verlassen hat oder
  aus dem Bestand gefallen ist.

Der Client darf jederzeit `{"bbox":"…"}` oder `{"takt":1000}` senden, um
Ausschnitt oder Takt nachzuziehen — beim Schwenken der Karte ist genau das
der Normalfall.

**Wichtig für die Oberfläche:** Takt und Alter sind zwei verschiedene Dinge.
Der Proxy liefert alle zwei Sekunden, aber die Daten darunter sind im Median
**31 Sekunden alt** (bestenfalls 1,3 s). Das ist eine Eigenschaft der
Upstream-Aggregation, die kein Proxy repariert. Das Alter je Schiff (`seen`)
gehört angezeigt, statt Frische vorzutäuschen.

## Einstellungen

Alles über Umgebungsvariablen, Vorgaben in `src/konfig.js`.

| Variable | Vorgabe | Bedeutung |
|---|---|---|
| `AIS_LAT_MIN` … `AIS_LON_MAX` | 53/6/56/13 | Region. **Fläche unter 400 sq° halten** — darüber verwirft der Upstream die Subscription kommentarlos |
| `AIS_TOKEN` | leer | leer = selbst holen |
| `AIS_NETZ_MS` | 60000 | Takt des Snapshot-Netzes |
| `AIS_HISTORIE_TAGE` | 7 | Aufbewahrung |
| `AIS_ROH_STUNDEN` | 24 | danach wird verdichtet |
| `AIS_VERDICHTUNG_S` | 60 | ein Punkt je Minute für ältere Tage |
| `AIS_REGISTER` | 1 | `0` schaltet die Registerabfragen ab |
| `AIS_ZUGANG` | leer | Token für den Zugang; leer = offen |
| `AIS_TAKT_MS` | 2000 | Vorgabetakt der Delta-Auslieferung |

## Tests

```bash
npm test                       # 45 Prüfungen, offline, ~1,6 s
node test/live.js 120     # gegen den echten Upstream, ~2,5 min
node test/register.live.js     # gegen Wikidata/Digitraffic/Commons
```

Der Offline-Satz braucht kein Netz. Die beiden Live-Läufe fassen fremde,
unbezahlte Dienste an — sparsam einsetzen.

## Betrieb

`GET /v1/status` ist der Ort, an dem man nachsieht. Zwei Zahlen sind
wichtiger als alle anderen:

- **`strom.rateSpitze` gegen `strom.rateLimit`.** Gemessen läuft die Region
  mit 37,5 msg/s gegen ein Limit von 50 — nur 25 % Luft. Über der Grenze
  drosselt der Server **kommentarlos**; man merkt es nur daran, dass Schiffe
  fehlen. Der Proxy warnt ab 42 msg/s im Log. Dann die Region auf beide
  erlaubten Verbindungen aufteilen (die zweite ist absichtlich frei).
- **`netz.letzteNeu`.** Wie viele Schiffe der Snapshot beisteuert, die der
  Strom nicht gezeigt hat. Bleibt die Zahl klein, trägt der Strom.
