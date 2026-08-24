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
- "Token holen"-Button für openwaters.io (self-serve, kein Login)
- Fällt Leaflet/CDN aus, degradiert die App auf reine Tabellen-Ansicht statt
  komplett zu brechen

## Testen — wichtig, sonst verschwendest du Zeit

**In dieser Sandbox/Remote-Umgebung sind blockiert:** `aisstream.io`,
`ais.openwaters.io`, `unpkg.com` (Leaflet-CDN), diverse
Vessel-Tracking-Seiten. Ein echter Verbindungstest gegen die echten Server
ist von hier aus **nicht möglich** (Proxy-Policy, nicht umgehbar/nicht
umgehen).

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
- `FiltersShipMMSI` (Filter nach bestimmten MMSIs) ist von der API
  unterstützt, aber im UI noch nicht angeboten
- `GET /v1/vessels` (openwaters, kein Auth nötig) wäre eine Alternative/
  Ergänzung zum WebSocket-Stream für einen reinen Snapshot ohne Live-Verbindung
- Kein automatisiertes Test-Setup — bei größeren Änderungen die
  Playwright-Smoke-Tests wie oben beschrieben neu aufsetzen
