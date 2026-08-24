# AISstream Live-Client

Browserbasierter Live-Client für AIS-Schiffsdaten (Standort, Kurs,
Geschwindigkeit, Typ, Größe) auf einer Leaflet-Karte. Reines
Vanilla-HTML/CSS/JS, keine Build-Pipeline, kein Backend — verbindet sich
per WebSocket direkt aus dem Browser zum AIS-Provider.

**Einzige Datei:** `aisstream/index.html` (alles inline: CSS, JS, HTML).
**Live-URL:** https://srautip.github.io/aisstream/
**Repo:** `srautip/srautip.github.io` (GitHub Pages, deployt automatisch bei
Push auf `main` — kein CI-Workflow nötig, GitHub baut/deployt selbst).

## Repo-Struktur (wichtig!)

Dieses Repo ist ein **Mono-Repo für mehrere unabhängige Projekte** des
Users, nicht nur für diesen Client:

- `aisstream/` — dieses Projekt
- `timetable-dotnet/`, `timetable/`, `stundentafel/` — komplett unabhängiges
  Stundenplan-/Scheduling-Projekt (.NET/VB, eigenes `timetable-dotnet/CLAUDE.md`)
- Root (`index.html`, `static/`, …) — die eigentliche React-App der
  Hauptseite (nur Build-Output, kein Quellcode im Repo)

**Git-Workflow, der sich bewährt hat:**
- Auf einem Feature-Branch entwickeln (zuletzt: `claude/aisstream-client-cb7pjv`)
- Zum Deployen: **niemals** einfach `main` überschreiben. `main` enthält
  parallel laufend Commits aus dem anderen Projekt. Immer:
  ```
  git fetch origin main
  git checkout main
  git merge origin/<feature-branch> -m "Merge branch '<feature-branch>' into main"
  git push origin main
  git checkout <feature-branch>
  ```
  Normalerweise ein sauberer Merge ohne Konflikte (andere Verzeichnisse).
  Bei Konflikt in `main` vorher `git fetch` + prüfen, was sich geändert hat,
  bevor irgendwas gemerged wird.

## Server-Endpoint — aktueller Stand

**Wichtig, zuerst prüfen:** Default ist aktuell
`wss://ais.openwaters.io/v0/stream` (Feld "Server-URL" im UI), **nicht**
der offizielle `wss://stream.aisstream.io/v0/stream`.

Grund: AISstream.io hatte ab ca. 5. Aug. 2026 einen länger andauernden
Ausfall (offiziell bestätigt als Circuit-Breaker-Bug, Team-seitig verzögerte
Reaktion). `openwaters.io` ist ein **inoffizieller Drittanbieter**
(community-betrieben), der unter `/v0/stream` das aisstream.io-Protokoll
1:1 spiegelt ("frozen, nichts weicht ab" laut deren Doku).

**Wenn eine neue Session hier weitermacht:** Prüfen, ob AISstream.io wieder
läuft (z. B. https://github.com/aisstream/issues Issues checken). Falls ja,
kann der User einfach im UI die Server-URL auf
`wss://stream.aisstream.io/v0/stream` umstellen und seinen echten
aisstream.io-Key eintragen — kein Code-Change nötig, das Feld ist frei
editierbar und persistiert in `localStorage`.

Der "Token holen"-Button ist **openwaters.io-spezifisch** (mintet dort per
`POST https://ais.openwaters.io/v1/keys` einen `ak1....`-Token via
Ed25519-Keypair aus der WebCrypto-API). Der Button ergibt bei
`stream.aisstream.io` keinen Sinn und funktioniert dort nicht — falls der
User dauerhaft zurückwechselt, ggf. den Button ausblenden/umbenennen.

## openwaters REST: `GET /v1/vessels` (am 24. Aug. 2026 verifiziert)

Wichtig, weil in einer früheren Session noch als „ungeprüfter offener Punkt"
notiert: Der Endpunkt ist **live erreichbar und aus dem Browser nutzbar** —
`access-control-allow-origin: *`, kein Key, kein Login.

**Zwei Query-Parameter funktionieren, alles andere wird stillschweigend
ignoriert** (empirisch durchprobiert):

| Parameter | Wirkung |
|---|---|
| `bbox=latMin,lonMin,latMax,lonMax` | Filtert auf die Box. **Lat zuerst**, wie bei `BoundingBoxes` im WebSocket. |
| `mmsi=a,b,c` | Ein oder mehrere MMSIs, kommagetrennt. Wiederholter Parameter (`?mmsi=a&mmsi=b`) wirkt **nicht** — nur der erste zählt. |

`minLat`/`maxLat`, `kind`, `since`, `limit` und `/v1/vessels/<mmsi>` gibt es
nicht — die ersten vier liefern kommentarlos den kompletten Weltbestand
(≈ 55 000 Features, ~17 MB), der Pfad-Zugriff ein 404. **Ohne `bbox` also
niemals abrufen**, sonst lädt der Browser 17 MB.

**Rückgabe ist GeoJSON** (`FeatureCollection`), Properties je Feature:

```
kind (vessel|aton|base), mmsi, msg_type, seen, source, station,
sog, name, type, cog, nav_status, heading
```

**Es gibt hier kein IMO, kein Rufzeichen, kein Ziel, keine Dimensionen und
keinen Tiefgang.** Das ist eine reine Positions-/Namens-Momentaufnahme, kein
Schiffsregister — für TEU, Bruttoraumzahl oder Passagierkapazität also
unbrauchbar. Nützlich ist er als *Snapshot ohne WebSocket und ohne Key* und
zum Nachschlagen einzelner MMSIs, weil openwaters mehrere Quellen bündelt
(`aishub`, `aisstream`, `kystverket`, `digitraffic` sowie privat
eingespeiste Empfänger) und dadurch Schiffe kennt, die der eigene Stream
gerade nicht liefert.

## Application Specific Messages — implementiert, aber der Feed führt sie nicht

Der Client dekodiert `BinaryBroadcastMessage` (Msg 8) und
`AddressedBinaryMessage` (Msg 6) für zwei Binnen-AIS-Funktionen:

- **DAC 200 / FI 10** — ENI-Nummer, Länge/Breite auf 1/10 m, ERI-Schiffstyp,
  blaue Kegel, Tiefgang auf 1/100 m, beladen/unbeladen
- **DAC 200 / FI 55** — Besatzung (8 Bit), **Passagiere (13 Bit)**,
  Bordpersonal (8 Bit)

**Erwarte davon auf dem openwaters-Feed nichts.** Messung vom 24. Aug. 2026:
5 Minuten Subscription über der Rhein-/Maasdelta (dichteste
Binnenschifffahrt Europas, Box `[[51.0,3.8],[52.3,7.6]]`) ergaben 4783
Nachrichten — davon **0 binäre**. Der weltweite `/v1/vessels`-Bestand
enthielt zeitgleich 5 `BinaryBroadcastMessage` unter 55 402 Einträgen. Die
Upstream-Aggregatoren reichen ASM praktisch nicht durch.

Der Dekoder ist trotzdem drin und getestet, weil er sofort trägt, sobald ein
Feed sie liefert — etwa ein eigener AIS-Empfänger (openwaters nimmt eigene
Quellen per `udp:`/`http:ed25519:` entgegen) oder das offizielle
aisstream.io. Die Checkboxen sind deshalb **standardmäßig aus**, mit
erklärendem Hinweistext im UI.

**Kodierung von `BinaryData`:** Im aisstream-Modell nur als `string`
deklariert, ohne Format — und es gab kein Live-Sample zum Gegenprüfen (siehe
oben: der Feed liefert keine). `binaryDataToBits()` erkennt deshalb zur
Laufzeit Base64 (auch URL-safe), Hex und reine Bitstrings. Alle drei sind
per Round-Trip getestet. Wer je ein echtes Sample in die Finger bekommt:
bitte hier notieren, dann kann die Erkennung entfallen.

## Protokoll-Fallstricke (bereits gelöst, aber gut zu wissen)

Alle über mehrere Debugging-Runden mit dem User empirisch/per Doku
verifiziert:

- **`BoundingBoxes`-Reihenfolge:** `[[Lat, Lon], [Lat, Lon]]` — **nicht**
  Lon/Lat. Wurde zwischenzeitlich fälschlich umgedreht (basierend auf einer
  mehrdeutigen Beispielinterpretation aus dem offiziellen aisstream-Repo),
  dann anhand der openwaters.io-Doku wieder korrigiert und verifiziert
  (Beispiel dort: `[58.5, 9.5]` = Oslofjord, Lat zuerst).
- **Binärframes:** Server schickt teils Binär- statt Text-Frames
  (`evt.data` kommt dann als `Blob`/`ArrayBuffer`, nicht als String).
  `JSON.parse(blob)` würde `"[object Blob]"` parsen → Fehler. Client liest
  jetzt Blob/ArrayBuffer korrekt als Text ein.
- **Gzip-Kompression:** `SubscriptionConfirmation` enthält
  `Message.CompressionEnabled: true` — nachfolgende Binärframes sind
  teils Gzip-komprimiert (Magic Bytes `1f 8b` geprüft), Dekomprimierung via
  nativer `DecompressionStream`-API (kein externes Lib nötig).
- **`SubscriptionConfirmation`** ist eine normale Bestätigungsnachricht
  (kein Schiff, keine MMSI) — wird separat behandelt, zählt nicht in den
  Nachrichten-Zähler.
- **Ship-Typ/Größe** kommen nur über `ShipStaticData`-Nachrichten (Felder
  `msg.Type` [ITU-R M.1371 Code] und `msg.Dimension.{A,B,C,D}` [Meter ab
  GPS-Antenne bis Bug/Heck/Backbord/Steuerbord]), nicht über
  `PositionReport`. Deshalb ist `ShipStaticData` im UI standardmäßig mit
  aktiviert.
- **Name-Feld:** `MetaData.ShipName` ist die verlässliche Quelle für den
  Schiffsnamen bei **jedem** Nachrichtentyp — nicht `Message.<Typ>.ShipName`
  (das Feld existiert im Message-Body nicht unter diesem Namen).
- **`@`-Padding:** AIS füllt Textfelder fester Breite (`Name`, `CallSign`,
  `Destination`) mit `@` auf. Ohne Bereinigung steht in der UI
  `NORDLICHT@@@@@@`. Dafür gibt es `cleanText()`.
- **Optionale Felder nie blind überschreiben:** Nicht jeder `PositionReport`
  trägt `PositionAccuracy`/`Raim`/`SpecialManoeuvreIndicator`. Ein
  `entry.x = msg.X` ohne `!= null`-Guard löscht bereits bekannte Werte
  wieder — beim Anreichern pro MMSI immer nur bei vorhandenem Feld setzen.
- **Class B splittet die Statikdaten:** `StaticDataReport` (Msg 24) liefert
  den Namen in `Message.StaticDataReport.ReportA.Name` und Rufzeichen/Typ/
  Dimensionen/Hersteller in `.ReportB`. Ohne diesen Typ bleiben Class-B-Boote
  namenlos — `ShipStaticData` (Msg 5) senden sie nicht.
- **`Fixtype` vs. `FixType`:** `ShipStaticData` nutzt `FixType`,
  `AidsToNavigationReport`/`BaseStationReport` schreiben `Fixtype`. Der Code
  prüft beide Schreibweisen.
- **Sentinel-Werte:** `TrueHeading` 511 = keine Angabe, `RateOfTurn` -128 =
  keine Angabe / ±127 = „dreht schneller als 5°/30 s", `Timestamp` 60–63 =
  Statuscodes statt Sekunden, `Eta.Month`/`Day` 0 = keine Angabe. Alles in
  den jeweiligen `*Label()`-Helfern behandelt.
- **Namen kommen aus dem Netz:** Tabellen-/Popup-Rendering läuft über
  `innerHTML`, deshalb geht jeder Streamwert durch `esc()`. Nicht entfernen.

## Implementierte Features

- Verbindungsaufbau/-abbau, API-Key + Server-URL editierbar & in
  `localStorage` persistiert (mit Selbstheilung: bekannte, überholte
  Default-Werte werden beim Laden verworfen statt wiederhergestellt —
  wichtig bei künftigen Default-URL-Änderungen, siehe Commit
  `5c872ae`)
- Auto-Reconnect mit Backoff (2s/5s/10s/20s/30s) bei unerwartetem
  Verbindungsabbruch; manuelles "Trennen" bricht das sauber ab
- Bounding Box folgt der Kartenansicht (Leaflet `moveend`, 400ms
  debounced): Felder + laufende Subscription werden automatisch
  nachgeführt
- Ergebnistabelle ist auf den sichtbaren Kartenausschnitt gefiltert
  (`map.getBounds().contains(...)`), nicht nur auf die Subscription-Box
- Schiffstyp (lesbare Kategorie) + Größe (L×B in Metern) aus
  `ShipStaticData`, in Tabelle und Marker-Popup
- **Detailansicht je MMSI** (Drawer rechts, öffnet per Klick auf Tabellenzeile,
  Marker-Popup-Link oder `openDetail()`; schließt per `Esc`). Zeigt gruppiert:
  Identität (MMSI-Kategorie, Flaggenstaat, MID, Name, Rufzeichen, IMO,
  AIS-Klasse, Transponder), Schiff (Typ inkl. Gefahrgutkategorie, Typcode,
  L×B, Tiefgang, Antennen-Offsets), Navigation (Status, SOG, COG, Heading,
  Drehrate, Positionsgenauigkeit, RAIM, EPFD, Ortungssekunde), Reise (Ziel,
  ETA), Entfernung/Peilung zur Kartenmitte sowie Empfangsstatistik
  (Nachrichten gesamt und je Typ, erste/letzte Sichtung, Trackpunkte,
  zurückgelegte Strecke, Ø Geschwindigkeit)
- **MMSI-Dekodierung offline** (`decodeMmsi()`): Stationsart nach ITU-R M.585
  (Schiff / Küstenfunkstelle / Gruppenruf / SAR-Luftfahrzeug / Seezeichen /
  Beiboot / AIS-SART / MOB / EPIRB / Handfunkgerät) plus MID → Flaggenstaat
  mit Flaggen-Emoji (`MID_DATA`, ~250 Einträge, kein Netzzugriff nötig)
- **Positionshistorie je MMSI** (bis `MAX_TRACK_POINTS` = 300 Punkte), auf
  Wunsch als Polyline auf der Karte („Track anzeigen", zoomt auf den Track)
- **Mehr Nachrichtentypen ausgewertet:** `StaticDataReport` (Class-B-Name,
  Rufzeichen, Typ, Hersteller), `AidsToNavigationReport` (Seezeichen inkl.
  virtuell/außer Position), `BaseStationReport` (Landstationen)
- **MMSI-Filter** (`FiltersShipMMSI`) im UI, kommagetrennt, in `localStorage`
  persistiert; wird beim `change`-Event neu subscribed (nicht bei jedem
  Tastendruck)
- **Freitextsuche** über Tabelle (MMSI, Name, Rufzeichen, IMO, Ziel, Typ,
  Land, Stationsart), 150 ms debounced
- Rohdaten-Ansicht je MMSI (letzte Nachricht je Typ als JSON) + „JSON
  kopieren" in die Zwischenablage + Deeplinks zu MarineTraffic, VesselFinder
  und OpenStreetMap
- **Snapshot-Button**: holt `GET /v1/vessels?bbox=…` für den aktuellen
  Ausschnitt und füllt Karte und Tabelle — ohne WebSocket-Verbindung und
  ohne API-Key. Warnt, wenn kein geladenes Schiff im Kartenausschnitt liegt
  (passiert, wenn die Box von Hand abweichend gesetzt wurde)
- **„Bei openwaters nachschlagen"** in der Detailansicht: Einzelabfrage per
  `?mmsi=`, ergänzt fehlende Felder und weist die Quelle aus. Beides nur
  aktiv, wenn die Server-URL auf `openwaters.io` zeigt — die REST-Basis wird
  aus der Server-URL abgeleitet, damit ein Wechsel zu aisstream.io nicht
  stillschweigend weiter openwaters abfragt
- **Binnen-AIS-Dekoder** für DAC 200 FI 10 / FI 55 (siehe eigenen Abschnitt
  oben) — ENI, ERI-Typ, blaue Kegel, beladen/unbeladen, Personen an Bord
- **Größen- und Kapazitätsabschätzung** aus den AIS-Maßen:
  - Größenklasse: bei Seeschiffen aus Schleusenmaßen (Panamax, Neopanamax,
    Kiel-Kanal, Seaway, Küstenmotorschiff-Größe, „zu groß für Panama"), bei
    Binnenschiffen CEMT I–VIb. Das ist keine Schätzung, sondern Geometrie
  - Verdrängung `Δ = 0,96·LOA · B · T · Cb · 1,025` mit typabhängigem
    Blockkoeffizienten, plus Tragfähigkeit über einen typabhängigen Anteil.
    Gegengeprüft an VLCC, Panamax-Bulker und Feeder
  - TEU über `0,011375 · (L·B)^1,4225`, gefittet an elf Referenzschiffen von
    500 TEU bis MSC Gulsun (23 756 TEU): mittlere Abweichung 7,8 %, max
    15,6 %. **Immer als „falls Containerschiff" ausgewiesen**, weil der
    ITU-Typcode 70–79 Container-, Bulk- und Stückgutfrachter nicht trennt
  - Passagierzahlen werden **bewusst nicht geschätzt** — Fähre und
    Kreuzfahrtschiff gleicher Länge liegen um eine Größenordnung
    auseinander. Echte Zahlen gibt es nur über FI 55
- "Token holen"-Button für openwaters.io (self-serve, kein Login)
- Fällt Leaflet/CDN aus, degradiert die App auf reine Tabellen-Ansicht statt
  komplett zu brechen

## Datenmodell (`ships`)

`ships` ist eine `Map` von **MMSI als String** (nicht Number — `MetaData.MMSI`
kommt als Zahl, wird in `processMessageText()` bewusst normalisiert) auf einen
langlebigen Datensatz, der über alle Nachrichtentypen hinweg angereichert wird:

```
{ mmsi, mmsiInfo:{category,mid,country,iso,flag},
  name, callSign, imo, aisClass, vendor, serial, station,
  typeCode, typeLabel, typeDetail, dim, size, draught, destination, eta,
  lat, lon, speed, course, heading, navStatus, rot, accuracy, raim,
  maneuver, fixType, msgTimestamp, time,
  counts:{<MessageType>:n}, total, lastMessageType,
  track:[{lat,lon,t,sog}], raw:{<MessageType>:<letzte Message>},
  firstSeen, updatedAt, marker }
```

Zentrale Funktionen: `getShip(mmsi)` legt den Datensatz an, `recordMessage()`
zählt/merkt die Rohnachricht, `setPosition()` schreibt Position + Track +
Marker. Wer einen neuen Nachrichtentyp ergänzt, hängt einfach einen weiteren
`else if`-Zweig in `processMessageText()` an und füllt die passenden Felder —
Tabelle, Popup und Detailansicht ziehen automatisch nach.

## Testen — wichtig, sonst verschwendest du Zeit

**Stand 24. Aug. 2026 — das hat sich geändert:** `ais.openwaters.io` ist
aus der Sandbox **per `curl` und Node erreichbar** (REST *und* WebSocket,
inklusive Token-Minting über `POST /v1/keys`). Damit sind echte
Endpunkt-Tests möglich — genau so wurden die `/v1/vessels`-Parameter und der
ASM-Befund oben verifiziert.

**Aber: Chromium kommt weiterhin nicht raus.** Der Agent-Proxy lässt
curl/Node durch und resettet Browser-Verbindungen (`ERR_CONNECTION_RESET`)
— auch für `unpkg.com`. Konsequenz für Tests:

- Netzabhängige Pfade **nicht** live aus dem Test-Browser prüfen. Stattdessen
  Antworten vorab per `curl` holen, als Datei ablegen und im Test per
  `page.route(...)` ausliefern. So wird gegen *echte* Daten getestet, ohne am
  Proxy zu scheitern.
- `page.route()` mit Glob (`'**host/pfad**'`) matcht diese URLs **nicht**
  zuverlässig — Regex nehmen: `page.route(/ais\.openwaters\.io\/v1\/vessels/, …)`.
- `--proxy-server=…` an Chromium zu hängen hilft nicht, der Reset kommt
  trotzdem.

**Wie bisher getestet wurde (funktioniert zuverlässig):**
1. `python3 -m http.server 8000` im Repo-Root, Seite unter
   `http://localhost:8000/aisstream/` aufrufen
2. Playwright + das lokal vorinstallierte Chromium
   (`/opt/pw-browsers/chromium-1194/chrome-linux/chrome`, `--no-sandbox`)
3. `window.WebSocket` im Browser-Kontext durch eine Fake-Klasse ersetzen
   (`page.addInitScript`), die realistische Nachrichten simuliert — **wichtig:**
   `this.readyState` muss von 0 auf 1 wechseln, sobald `onopen` feuert (der
   Code prüft das jetzt explizit vor jedem `send()`, siehe `sendSubscription()`)
4. Für Tests, die eine echte Kartenansicht brauchen (Bbox-Sync, Viewport-Filter):
   Leaflet lässt sich nicht vom CDN laden, aber `npm pack leaflet@1.9.4` +
   `page.route(...)` auf die unpkg-URLs, die stattdessen die lokal
   entpackten `dist/leaflet.js`/`dist/leaflet.css` ausliefern, funktioniert
   gut. Tile-Requests ebenfalls faken (z. B. 1×1-GIF), sonst hängt Leaflet.
5. Reales Pannen der Karte lässt sich über echte `page.mouse.move/down/up`-
   Drag-Gesten auf `#map` simulieren (nicht nötig, interne Funktionen
   künstlich freizulegen)

Es gibt kein Test-Framework/keine Test-Dateien im Repo — alle bisherigen
Tests waren Wegwerf-Skripte im Scratchpad-Verzeichnis der jeweiligen
Session. Bei Bedarf neu aufsetzen nach obigem Muster.

## Offene Punkte / mögliche nächste Schritte

- Sobald AISstream.io stabil zurück ist: Default-Server-URL ggf. wieder auf
  `wss://stream.aisstream.io/v0/stream` umstellen (siehe oben)
- Für echte TEU-/Bruttoraumzahl-/Passagierkapazitätsdaten führt kein Weg an
  einem Schiffsregister vorbei (Join über die IMO-Nummer aus
  `ShipStaticData`). Wikidata (`P458` = IMO) ist frei und CORS-fähig, deckt
  aber fast nur prominente Schiffe ab und hat keine gepflegte TEU-Property;
  kommerzielle APIs (Datalastic, Data Docked) hätten die Felder, brauchen
  aber einen Key — und ein Key im Browser-JS ist öffentlich, das bräuchte
  einen kleinen Proxy und damit erstmals ein Backend
- Kein automatisiertes Test-Setup — bei größeren Änderungen die
  Playwright-Smoke-Tests wie oben beschrieben neu aufsetzen
- Schiffsdaten sind rein flüchtig: Reload verwirft alle gesammelten MMSIs
  samt Track. Eine Persistenz in `localStorage`/IndexedDB wäre der nächste
  logische Schritt, ebenso ein Deeplink `?mmsi=…`, der die Detailansicht
  direkt öffnet
- `MAX_TRACK_POINTS` (300) und die Ship-Map wachsen unbegrenzt über die
  Sitzung; bei sehr langen Läufen wäre ein Aufräumen alter MMSIs
  (`STALE_AFTER_MS` wird bisher nur für das Badge genutzt) sinnvoll
