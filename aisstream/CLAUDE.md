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

**Stand 24. Aug. 2026 — AISstream.io antwortet wieder.** Gemessen:
`https://aisstream.io/` liefert HTTP 200, und
`https://stream.aisstream.io/v0/stream` antwortet mit HTTP 400 — genau das,
was ein lebender WebSocket-Endpunkt auf ein Plain-GET ohne Upgrade-Header
zurückgibt. Ein „tot" sähe anders aus (Timeout, Connection Reset, 502).

Das ist **noch kein Beweis, dass der Dienst wieder Daten liefert** — dafür
bräuchte es einen gültigen aisstream.io-Key, den diese Sandbox nicht hat.
Wer einen hat: im UI die Server-URL auf
`wss://stream.aisstream.io/v0/stream` umstellen und den Key eintragen — kein
Code-Change nötig, das Feld ist frei editierbar und persistiert in
`localStorage`. Wenn das stabil läuft, kann der Default zurückgestellt
werden (dann `SUPERSEDED_DEFAULT_URLS` um die openwaters-URL ergänzen, damit
alte gespeicherte Werte verworfen statt wiederhergestellt werden).

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

## openwaters-Token: Limits, die stillschweigend greifen

`POST https://ais.openwaters.io/v1/keys` (der „Token holen"-Button) liefert
einen `personal`-Token mit diesen Claims — hier die real gemessenen Werte:

```
conns: 2      gleichzeitige Verbindungen
rate:  50     Nachrichten pro Sekunde
area:  400    Quadratgrad Bounding-Box-Fläche
mmsis: 50     MMSIs im FiltersShipMMSI
```

**Fallstrick, der eine halbe Debug-Runde kostet:** Wird die Box größer als
`area`, verwirft der Server die Subscription **kommentarlos** — kein
`SubscriptionConfirmation`, keine Fehlermeldung, einfach nie Daten. Passiert
schnell: `[[35,-10],[62,30]]` (Europa) sind 40° × 40° = 1600 sq° und damit
das Vierfache des Erlaubten. `[[51.0,3.8],[52.3,7.6]]` (Rhein-/Maasdelta)
sind 4,9 sq° und funktionieren.

Wer also „verbunden, aber keine Nachrichten" sieht: zuerst die Boxfläche
rechnen, nicht den Client debuggen.

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

**Konkreter nächster Schritt, jetzt machbar:** Die Sandbox kommt per Node
nach draußen (siehe Testabschnitt), und AISstream.io antwortet wieder. Mit
einem gültigen aisstream.io-Key ließe sich in ein paar Minuten prüfen, ob
*deren* Feed ASM durchreicht — dann wären sowohl der Befund „Feed führt
keine" als auch die Kodierungsfrage endgültig geklärt. Rezept: kleines
Node-Skript, `FilterMessageTypes: ["BinaryBroadcastMessage",
"AddressedBinaryMessage"]`, Box über der Rhein-/Maasdelta
(`[[51.0,3.8],[52.3,7.6]]`), ein paar Minuten mitzählen.

## Warum so viele Schiffe „unbekannt" sind (gemessen)

Häufige Nutzerfrage: Schiffe haben einen **Namen**, aber keinen Typ, kein
Rufzeichen, keine Größe. Das ist kein Fehler, sondern die Bauart von AIS.

**Der Name kommt aus `MetaData.ShipName` und liegt an *jeder* Nachricht an** —
der Server (aisstream/openwaters) füllt ihn aus seinem eigenen Statik-Cache.
**Typ, Größe und Rufzeichen kommen ausschließlich aus einer echten
`ShipStaticData` (Msg 5, alle 6 Minuten) bzw. `StaticDataReport` (Msg 24,
seltener).** Der Server kennt das Schiff also, der Client hat die Nachricht
aber noch nicht selbst gesehen.

Messung, 5 Minuten über der Inneren Deutschen Bucht
(`[[53.65,7.40],[53.90,8.25]]`), 24. Aug. 2026:

| | |
|---|---|
| Verschiedene MMSIs | 62 |
| davon mit Namen | 54 (87 %) |
| davon mit Statiknachricht | **12 (19 %)** |
| Wartezeit bis zur Statik | Median 14 s, Maximum 273 s |

Die Schiffe ohne Typ hatten ausnahmslos nur `PositionReport` empfangen.

**Drei Wege, das abzukürzen** (alle im Client vorhanden):
1. **Snapshot laden** — `GET /v1/vessels?bbox=…` hat für denselben Ausschnitt
   bei **75 %** der Schiffe einen Typ, gegen 19 % aus 5 Minuten Livestream.
   Mit Abstand der schnellste Hebel.
2. **Statik-Cache** — beim nächsten Besuch sind bekannte Schiffe sofort benannt.
3. **Registeranreicherung** — Digitraffic/Wikidata je MMSI, allerdings mit
   niedriger Trefferquote (siehe unten).

## Anreicherung per IMO/MMSI: Digitraffic + Wikidata

Zwei Quellen, beide **frei, ohne Schlüssel und mit `access-control-allow-origin: *`**
— passt damit zur Architektur ohne Backend. Am 24. Aug. 2026 verifiziert.

### Die Kette (die Reihenfolge ist der Punkt)

```
MMSI --> Digitraffic /vessels/{mmsi} --> IMO-Nummer
                                          |
MMSI --> Wikidata P587 ---(kein Treffer)--+--> Wikidata P458
```

Der Umweg über die IMO lohnt sich messbar: In einer Stichprobe von 75 realen
Schiffen (Wattenmeer, Rhein, Ostsee) fand Wikidata **8 Schiffe über die MMSI
und 9 weitere ausschließlich über die IMO-Nummer aus Digitraffic**. Ohne die
Kette wäre also gut die Hälfte der Treffer verloren gegangen.

### Trefferquoten — Erwartungen niedrig halten

| Quelle | Treffer von 75 | Anmerkung |
|---|---|---|
| Digitraffic kennt die MMSI | 16 (21 %) | stark Ostsee-lastig, es ist finnisches AIS |
| davon mit IMO-Nummer | 15 | |
| Wikidata gesamt | 17 (23 %) | nur bekanntere Schiffe |

**Am Rhein: null Treffer.** Binnenschiffe und kleine Boote sind in beiden
Quellen praktisch nicht erfasst. Das ist kein Fehler, das ist die Realität
offener Schiffsdaten — im UI steht die Zahl deshalb direkt dran.

### `GET https://meri.digitraffic.fi/api/ais/v1/vessels/{mmsi}`

Liefert die AIS-Statik: `imo`, `callSign`, `destination`, `eta`, `draught`,
`shipType`, `posType`, `referencePointA..D`. Ohne MMSI im Pfad kommt die
komplette Liste (~790 Schiffe, 39 KB) — `?mmsi=` als Query wird mit HTTP 400
abgelehnt, es muss der Pfad sein.

Kodierung, gegen echte Daten geprüft:
- `draught` in **Dezimetern** (68 = 6,8 m)
- `eta` als ein Integer: `month<<16 | day<<11 | hour<<6 | minute`.
  Der Wert `1596` ist der Sentinel „keine Angabe" (Monat 0, Tag 0, Stunde 24,
  Minute 60) — `etaLabel()` fängt das bereits ab
- `referencePointA..D` in **Metern**, A+B = Länge, C+D = Breite
- **Achtung `curl`:** Der Dienst verlangt `Accept-Encoding: gzip` und
  antwortet sonst mit HTTP 406. Browser senden den Header ohnehin immer,
  aber `curl` braucht `--compressed`

### Wikidata SPARQL

`https://query.wikidata.org/sparql?format=json&query=…` — **`format=json` in
der Query-String statt eines `Accept`-Headers**, das spart den
CORS-Preflight bei jeder Abfrage.

Genutzte Properties: `P458` IMO, `P587` MMSI, `P2317` Rufzeichen, `P1093`
Bruttoraumzahl, `P2790` Nettoraumzahl, `P2043` Länge, `P2261` Breite,
`P2262` Tiefgang, `P2052` Geschwindigkeit, `P729` Indienststellung, `P31`
Typ, `P289` Schiffsklasse, `P127` Eigner, `P137` Betreiber, `P176` Werft,
`P8047` Flaggenstaat, `P532` Registerhafen, `P18` Foto.

Drei Dinge, die Zeit gekostet haben:

- **`P2067` („Masse") ist als Tragfähigkeit unbrauchbar.** Für die Emma
  Mærsk (170 793 BRZ) liefert sie 16 810 t — die echte Tragfähigkeit liegt
  bei rund 156 000 t. Die Property ist bewusst **nicht** eingebunden; die
  eigene Schätzung aus den Maßen ist deutlich näher dran.
- **Eine TEU-Property gibt es nicht.** Auch nicht bei der Emma Mærsk. Dafür
  liefert `P31` den *echten* Schiffstyp („Containerschiff", „Massengutfrachter",
  „RoRo-Schiff", „Fähre") — und genau das kann der ITU-Typcode 70–79 nicht.
  Der Client nutzt das: Bestätigt Wikidata ein Containerschiff, entfällt beim
  TEU-Wert der Zusatz „falls Containerschiff"; sagt Wikidata Bulker oder
  Tanker, wird die TEU-Schätzung ganz unterdrückt.
- **`P18` liefert `http://`-URLs.** Auf der per HTTPS ausgelieferten Seite ist
  das Mixed Content und wird blockiert — das Bild bleibt einfach leer. Die
  URL muss auf `https://` gehoben werden (macht `fetchWikidata()`).

### Umsetzung im Client

- **Nur für das geöffnete Schiff**, nie für die ganze Tabelle. Beide APIs sind
  Gemeingut; Abfragen laufen serialisiert mit 350 ms Abstand (`throttled()`)
- **Cache in `localStorage`** unter `aisstream_enrich_<mmsi>`, 30 Tage für
  Treffer. **Auch Fehlschläge werden gecacht** (3 Tage) — bei 77 % Miss-Rate
  ist das der eigentliche Gewinn, sonst fragt jedes Öffnen erneut vergeblich an
- **Getrennte Töpfe:** Digitraffic landet unter `enrich.dt`, Wikidata auf der
  obersten Ebene. Grund: Beide haben ein Feld `beam`, und beim flachen Mergen
  erschien Digitraffics Live-AIS-Breite stillschweigend in der Wikidata-Zeile
- **Anreicherung überschreibt nie den Livestream**, sie füllt nur Lücken —
  der Stream ist aktuell, das Register kann Jahre alt sein
- **Plausibilitätsfilter:** Fehlkonfigurierte Transponder senden `"0"` als
  Rufzeichen. `plausibleCallSign()` verwirft das; mit Müll anreichern ist
  schlechter als das Feld leer zu lassen

## Kartenmarker: Farbe = Typ, Durchmesser = Länge

Die Palette ist **errechnet, nicht ausgesucht** — mit dem Validator der
dataviz-Skill über den OKLCH-Farbtonkreis gesucht.

**Warum nicht die Referenzpalette:** Marker auf einer Karte sind eine
*All-Pairs*-Form — alle Farben liegen gleichzeitig nebeneinander, „benachbart"
gibt es nicht. Unter `--pairs all` fällt die 8er-Referenzordnung durch
(schlechtestes Paar CVD ΔE 3,2) und ist dort auf drei Slots gedeckelt. Eine
eigene Suche über den Farbtonkreis liefert **sechs** Farben, die das *strenge*
Gate halten.

| Kategorie | Hex | ITU-Typcode |
|---|---|---|
| Frachtschiff | `#2a78d6` | 70–79 |
| Tanker | `#ab2000` | 80–89 |
| Passagierschiff | `#77a400` | 60–69 |
| Fischerei | `#007a61` | 30 |
| Schlepper & Arbeitsschiffe | `#8c2c95` | 31–35, 50–59 |
| Sport & Segel | `#ca7197` | 36, 37 |
| Sonstige / unbekannt | `#6b6b68` | Rest — neutral, kein Slot |

Gemessen gegen **beide** dominanten Kachelfarben (Land `#f2efe9`, Wasser
`#aad3df`): schlechtestes Paar CVD ΔE 8,8 · normal ΔE 19,0 — alle Prüfungen
bestanden. **Sechs ist das gemessene Maximum**, eine siebte Farbe hält die
Schwellen nicht mehr. Wer Kategorien ergänzen will, muss also eine andere
zusammenlegen — nicht einfach eine Farbe dazuerfinden.

Der Kontrast-WARN (zwei bzw. drei Farben unter 3:1 gegen die Kacheln) ist
durch „relief" gedeckt: Der Typ steht im Klartext in der Tabelle und im Popup,
und jeder Marker trägt einen weißen Ring, der ihn von der Kachel und von
überlappenden Markern trennt.

**Größe** über die Länge über alles, Quelle in dieser Reihenfolge: Inland-ERI,
`Dimension` A+B, Digitraffic, Wikidata.

| Stufe | Länge | Durchmesser |
|---|---|---|
| S | < 50 m | 8 px |
| M | 50–100 m | 11 px |
| L | 100–200 m | 15 px |
| XL | ≥ 200 m | 20 px |
| ? | unbekannt | 11 px, **Ring** (3 px Kontur in der Typfarbe, weißer Kern) |

Ein Schiff ohne Maßangabe darf nicht wie ein kleines Boot aussehen, deshalb
die eigene Darstellung. **Der Ring ist aber genau so gebaut, dass die
Typfarbe erhalten bleibt** — siehe die Falle direkt darunter.

Im Livebetrieb ist „Länge unbekannt" der **Normalfall**, nicht die Ausnahme:
Der Snapshot liefert Typen, aber keine Maße, und der Stream liefert Maße nur
mit einer `ShipStaticData`. In einem Testlauf über der Deutschen Bucht hatten
**26 von 26** Markern keine Länge, aber 18 davon einen bekannten Typ.

### Die Falle: Größenkodierung darf die Farbkodierung nicht überschreiben

Erste Fassung setzte für unbekannte Länge `background: #ffffff !important`
und schob die Typfarbe in einen 1,5-px-Rand. Ergebnis auf der echten Karte:
**alle Marker weiß**, die Farbkodierung praktisch unsichtbar — obwohl bei
zwei Dritteln der Typ bekannt war. Der Fehler fiel im synthetischen Test
nicht auf, weil dort jedes Schiff Maße mitbekam; erst echte Snapshot-Daten
haben ihn gezeigt.

**Regel:** Die Füllung bzw. die 3-px-Kontur trägt immer die Typfarbe. Wer die
Größendarstellung anfasst, darf den Farbkanal nicht dafür verbrauchen — und
sollte gegen echte Daten testen, nicht gegen Fixtures mit vollständigen
Feldern.

**Form** trennt die Entitätsklassen, ohne eine Farbe zu verbrauchen: Kreis =
Schiff, Raute = Seezeichen, Quadrat = Landstation.

Drei Punkte, die aus dem Anti-Pattern-Katalog der Skill kamen:
- **Klickziel:** Ein 8-px-Punkt ist ein mieses Ziel. Der Marker sitzt deshalb
  zentriert in einer transparenten **24-px-Box** (`MARKER_HIT_PX`) — sichtbare
  Größe und Trefferfläche sind entkoppelt.
- **Legende ist Pflicht** bei ≥ 2 Kategorien. Sie ist einklappbar, weil sie
  aufgeklappt rund zwei Drittel der 420 px hohen Karte einnimmt und genau die
  Marker verdeckt, die sie erklärt. Zustand in `aisstream_legend_open`.
- **Farbe folgt der Entität, nie dem Rang** — die Kategorie hängt am
  Schiffstyp, nicht an der Sortierreihenfolge.

Bestätigt Wikidata einen Typ, schlägt der den ITU-Code (`typeCategory()`) —
ein „Frachtschiff" laut Code, das laut Register ein Tanker ist, wird rot.

### Ladereihenfolge — schon zweimal reingefallen

`addMapLegend()` stand zuerst im Karten-Init-Block, also **vor** der
`var`-Initialisierung von `TYPE_COLORS`. Ergebnis: `TYPE_COLORS.forEach` wirft,
die IIFE bricht ab, **keine** Event-Listener werden mehr registriert — die
ganze Seite reagiert auf nichts, ohne sichtbare Fehlermeldung. Derselbe
Fehlertyp wie bei `loadStaticCache()`.

**Merke:** In dieser Datei sind Funktionsdeklarationen gehoistet, `var`-Werte
nicht. Alles, was auf die Konstanten weiter unten zugreift, gehört in den
Verdrahtungsblock am Ende der IIFE, nicht in den Init oben.

## Abfragebox ist größer als die Sicht

Alle Serveranfragen — WebSocket-Subscription **und** `GET /v1/vessels` —
gehen mit **50 km Rand** in jede Richtung raus (`API_MARGIN_KM`,
`expandBox()`). Tabelle und Marker filtern weiterhin auf die *exakte*
Kartensicht, der Rand ist für den Leser also unsichtbar.

**Warum:** Eine frische Subscription startet blind. Ein Class-A-Schiff
wiederholt seine Statik nur alle sechs Minuten, und beim Verschieben der
Karte wären die neu hereinkommenden Schiffe erst nach Minuten benannt. Mit
Rand liegen sie schon im Speicher. Nachgemessen: Karte nach Norden gezogen →
die vorher außerhalb liegenden Schiffe stehen **sofort** in der Tabelle,
**ohne** neue Snapshot-Anfrage.

Details:
- Längengrade schrumpfen polwärts, deshalb wird der Ost-West-Rand über die
  **äußerste** Breite des Kastens berechnet — sonst wären es im Norden
  weniger als 50 km. Gemessen ergibt das 49,9–50,2 km auf allen vier Seiten.
- **Flächendeckel:** Der openwaters-Token erlaubt 400 Quadratgrad und
  verwirft größere Subscriptions kommentarlos (siehe oben). `expandBox()`
  lässt den Rand deshalb weg, wenn er den Kasten über diese Grenze schieben
  würde, und der Client warnt im Log, wenn schon die reine Sicht darüber
  liegt.
- Der Log schreibt die tatsächlich gesendete Box samt „(+50 km Rand)", damit
  nicht verwirrt, dass die Bbox-Felder etwas anderes zeigen.

**Marker folgen der Sicht, nicht der Abfragebox.** Ohne das lagen bei 20
sichtbaren Schiffen 610 Marker im DOM — unnötige Last, gerade auf dem
Telefon. `markerInView()` nimmt sie mit `getBounds().pad(0.25)` von der
Karte; der kleine Rand verhindert, dass beim Verschieben Marker sichtbar
aufpoppen. Danach: ~69 statt 610.

## Filter und Autostart

### Filter wirken auf Karte **und** Tabelle

Eine gemeinsame Filterleiste über beidem (nicht je Ansicht eine eigene —
Karte und Tabelle zeigen immer denselben Ausschnitt der Daten). Zwei
Gruppen, beide als Mehrfachauswahl:

- **Typ** — die sechs Kategoriefarben plus „Sonstige / unbekannt", jeder Chip
  mit seinem Farbpunkt, damit der Bezug zur Karte sofort da ist
- **Status** — Navigationsstatus zu fünf Gruppen zusammengefasst:
  `In Fahrt` (0, 8) · `Vor Anker / fest` (1, 5) · `Eingeschränkt`
  (2, 3, 4, 6, 11, 12) · `Fischerei` (7) · `Sonstige / unbekannt` (Rest und
  fehlender Status)

Zentral ist `passesFilters(entry)` — **eine** Funktion, die `refreshVisibleShips()`
für die Tabelle und `applyMapFilter()` für die Marker benutzen. Wer einen
Filter ergänzt, fasst nur diese Stelle an; beide Ansichten ziehen automatisch
nach.

**Leere Auswahl heißt „keine Einschränkung", nicht „nichts"** (`anyActive()`).
Damit sind Startzustand und Zurücksetzen identisch, und ein versehentlich
leergeklickter Filter blendet nicht die ganze Karte aus.

`applyMapFilter()` nimmt Marker wirklich per `removeLayer` von der Karte,
statt sie nur transparent zu schalten — sonst blieben ihre 24-px-Klickflächen
liegen und würden Treffer abfangen. Der Zustand hängt an
`entry.markerShown`, damit nicht bei jedem Durchlauf alle Layer angefasst
werden.

Auswahl steckt in `aisstream_filters`. Gegengeprüft mit echten
Snapshot-Daten: Tabelle und Karte zeigen bei jeder Kombination dieselbe
Anzahl.

### Autostart beim Laden

Zwei Schalter, beide vorbelegt und in `aisstream_auto_snapshot` /
`aisstream_auto_connect` gemerkt:

1. **Snapshot zuerst** — braucht keinen Key, antwortet in einer Anfrage und
   füllt die Karte sofort, während der Stream nur langsam eintröpfelt.
2. **Dann verbinden** — aber **nur, wenn schon ein Key gespeichert ist**.
   Sonst würde `connect()` bei *jedem* Seitenaufruf ein `alert()` werfen;
   stattdessen gibt es eine Zeile im Log und sonst nichts.

Beides steht am Ende der IIFE, nach allen `var`-Initialisierungen (siehe die
Ladereihenfolge-Falle weiter oben).

**Für Tests wichtig:** Ist ein Key gespeichert, ist `#connectBtn` nach dem
Laden bereits **deaktiviert**, weil die Seite von selbst verbindet. Skripte
müssen den Klick überspringen:
`if (!await page.evaluate(()=>document.getElementById('connectBtn').disabled)) await page.click('#connectBtn');`

## Layout auf iPhone und iPad

Der Client wird viel auf dem Telefon benutzt. Leitgedanke: **Schiffsdaten
zuerst, Technik weg.**

### Drei Layoutstufen

| Breite | Verhalten |
|---|---|
| > 1024 px | Zweispaltig wie bisher, Technik als feste Seitenspalte |
| ≤ 1024 px (iPad hoch, iPhone quer) | Technik wird zur Schublade von rechts, Inhalt einspaltig, Karte 42 vh |
| ≤ 700 px (iPhone) | Zusätzlich: Tabelle als Kartenliste, Filterleiste als Scrollzeilen, Schublade voll breit, Karte 38 vh |

Vorher rutschte bei ≤ 900 px das komplette Technikpanel **über** Karte und
Tabelle — man scrollte an API-Key, Bounding Box und Log vorbei, bevor ein
einziges Schiff sichtbar wurde. Jetzt hängt es hinter dem Knopf
„Verbindung & Technik" in der Kopfzeile (`#settings`, Schließen per Kreuz,
`Esc` oder Tipp daneben; auf dem Telefon voll breit, weil bei 92 vw nur
32 px Hintergrund übrig blieben — als Tippziel unbrauchbar).

### Tabelle → Kartenliste, ohne zweiten Render-Pfad

Jede `<td>` trägt ein `data-label` mit ihrer Spaltenüberschrift und eine
Klasse `c-*`. Unter 700 px stellt CSS dieselben Zeilen auf `display: block`
um, blendet den `thead` aus und setzt das Label per `::before` davor. Es gibt
**keine** zweite Renderfunktion — wer eine Spalte ergänzt, hat sie
automatisch auch in der Kartenansicht.

Der Name steht per `order: -1` als Überschrift oben (unabhängig von der
Spaltenreihenfolge), davor ein Punkt in der Typfarbe als Bezug zur Karte.
Flagge absolut oben rechts. `Lat`, `Lon`, `Zeit` und `Rufzeichen` sind
ausgeblendet — die stehen in der Detailansicht.

### iOS-spezifisch

- `viewport-fit=cover` plus `env(safe-area-inset-*)` an Kopfzeile, `main`,
  Schublade und Detailansicht — sonst liegt Inhalt unter Notch und
  Home-Indicator
- **Eingabefelder auf 16 px** unter 700 px: Alles darunter lässt iOS beim
  Fokus in das Feld hineinzoomen, und die Seite bleibt danach verschoben
- `-webkit-text-size-adjust: 100%`, sonst vergrößert iOS Text im Querformat
- `map.invalidateSize()` nach `resize` und `orientationchange` (debounced) —
  Leaflet kennt die Höhe einer `vh`-Karte sonst nicht nach dem Drehen
- **Legende auf dem Telefon:** startet zugeklappt und merkt sich das unter
  einem *eigenen* Schlüssel (`aisstream_legend_open_sm`), damit eine
  Desktop-Einstellung nicht aufs Telefon durchschlägt.
  Wichtiger noch: Die Legende ist ein `bottomleft`-Control und **wächst nach
  oben**. Aufgeklappt war sie 339 px hoch in einer 250-px-Karte — ihr Kopf,
  der einzige Weg zum Zuklappen, lag damit *über* der Karte und war nicht
  mehr antippbar. `fitToMap()` setzt beim Öffnen und bei jedem
  `resize`/`orientationchange` eine `maxHeight` aus der echten Kartenhöhe,
  der Rumpf scrollt darin. Wer an der Kartenhöhe dreht: diesen Deckel
  mitprüfen.

### Zwei CSS-Fallen, beide dieselbe Ursache

Beide haben das Dokument überbreit gemacht und quer scrollen lassen — **auch
auf dem Desktop**, wo es jahrelang unbemerkt blieb:

- **`main > * { min-width: 0 }`** — ohne das wächst ein Grid-Kind auf die
  *Mindestbreite seines Inhalts*. Die 13-spaltige Tabelle blies das Dokument
  auf 1678 px auf, obwohl sie in einem `overflow: auto`-Container steckt.
- **Filterleiste auf dem Telefon als `display: block`, nicht Column-Flex.**
  Als Flex-Item hat `.filtergroup` seine Cross-Achse nicht auf die
  Containerbreite begrenzt und ist trotz `overflow-x: auto` und
  `min-width: 0` auf 846 px aufgegangen. Als Blockelement füllt sie schlicht
  die Breite und scrollt darin.

**Merke:** Bei „die Seite scrollt quer" nicht raten, sondern messen —
Elemente nacheinander auf `display: none` setzen und `scrollWidth`
beobachten. Ein Detektor über Bounding-Rects liefert Fehltreffer, weil er
geclippte Kinder (Leaflet-Kacheln, Off-Canvas-Schubladen) mitzählt.

### Testen

`playwright.devices` liefert die echten Geräteprofile — **eigene
`viewport`-Objekte reichen nicht**, ohne `screen` löst `width=device-width`
auf eine falsche Breite auf und die Media Queries greifen anders als auf dem
Gerät. Benutzt wurden `iPhone 15`, `iPhone 15 landscape`, `iPad Pro 11`,
`iPad Pro 11 landscape` plus ein Desktop-Viewport. Geprüft wird je Größe:
Position und Höhe der Karte, ob die Technik ausgelagert ist, ob die Tabelle
als Karten läuft, `scrollWidth` gegen `clientWidth` und die Schriftgröße im
Eingabefeld. Bedienung per `page.tap()`, nicht `click()`.

## Was in `localStorage` liegt

Vier Dinge, mit sehr unterschiedlicher Lebensdauer:

| Schlüssel | Inhalt | TTL |
|---|---|---|
| `aisstream_api_key`, `aisstream_server_url`, `aisstream_mmsi_filter`, `aisstream_auto_enrich`, `aisstream_legend_open`, `aisstream_legend_open_sm`, `aisstream_auto_snapshot`, `aisstream_auto_connect`, `aisstream_filters` | Einstellungen & Filterauswahl | unbegrenzt |
| `aisstream_enrich_<mmsi>` | Registerdaten je Schiff, **auch Fehlschläge** | 30 d Treffer / 3 d Miss |
| `aisstream_static` | **eine** JSON-Map MMSI → Schiffsstatik | 60 d |

### Schiffsstatik-Cache (`aisstream_static`)

Zweck: Nach einem Neuladen sind Schiffe sofort benannt, statt bis zur
nächsten `ShipStaticData`-Nachricht namenlos in der Tabelle zu stehen — die
kommt bei Class A nur alle sechs Minuten, bei Class B noch seltener.

**Gespeichert wird nur, was wirklich statisch ist:** `name`, `callSign`,
`imo`, `typeCode`, `typeLabel`, `typeDetail`, `size`, `dim`, `aisClass`,
`vendor`, `serial`, `station` und die statischen Inland-Felder (`eni`,
`eriCode`, `eriLabel`, `eriLength`, `eriBeam`).

**Bewusst nicht gespeichert:** Positionen, Track, `destination`, `eta`,
`draught`, `loaded`, `blueCones`. Das sind Reisedaten — ein drei Wochen
altes Ziel als „aktuell" anzuzeigen wäre irreführend, und der Tiefgang
ändert sich mit jeder Beladung. Wer hier Felder ergänzt: Diese Grenze
bitte halten.

Mechanik:
- Eine **einzelne** Map statt eines Schlüssels pro MMSI — ein Lesevorgang
  beim Start, und das Kontingent lässt sich in einem Rutsch trimmen
- Schreiben ist um 3 s **debounced**, zusätzlich hängt ein Sichern an
  `pagehide` und `visibilitychange` — sonst geht der letzte Stand beim
  Schließen des Tabs verloren
- Cap `STATIC_MAX` = 2000 MMSIs, ältester Eintrag fliegt zuerst. Gemessen:
  2000 Einträge sind rund **406 KB**, passt bequem in das ~5-MB-Budget
- Bei `QuotaExceededError` wird die ältere Hälfte verworfen und **einmal**
  neu versucht, danach gibt der Cache auf, statt die App zu brechen
- Ein aus dem Cache befülltes Schiff bekommt `staticFromCache`; die
  Detailansicht weist das als „Statik aus Zwischenspeicher" aus, und die
  erste echte Statiknachricht setzt das Flag wieder zurück

### Zwei Fallen, die beim Bauen zugeschnappt sind

- **Ladereihenfolge:** `loadStaticCache()` stand zunächst weit oben bei den
  anderen `localStorage`-Zugriffen — also **vor** `var staticCache = {}`.
  Der Zugriff auf das noch undefinierte Objekt landete im `catch`, und der
  Initializer hat kurz darauf sowieso alles überschrieben. Ergebnis: Cache
  wurde brav geschrieben und nie angewandt, völlig lautlos. Der Aufruf steht
  jetzt direkt vor `getShip()`, nach allen `var`-Initialisierungen.
- **Race beim Leeren:** Eine noch laufende Registerabfrage schrieb nach dem
  Klick auf „Zwischenspeicher leeren" ihr Ergebnis zurück — der Zähler
  sprang auf 1. Deshalb gibt es `cacheGeneration`: `enrichShip()` merkt sich
  den Stand beim Start und schreibt nur, wenn er unverändert ist.

„Zwischenspeicher leeren" räumt **nur den Speicher**, nicht die laufende
Sitzung. Die Namen der Schiffe, die man gerade ansieht, verschwinden zu
lassen wäre ein überraschender Nebeneffekt; alles im Speicher wurde
legitim empfangen. Der Log sagt das auch so.

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
- **Sentinel-Werte:** `Sog` 102.3 (Rohwert 1023) und `Cog` 360.0 (Rohwert
  3600) heißen **„nicht verfügbar"**, ebenso `TrueHeading` 511. Dazu
  `RateOfTurn` -128 = keine Angabe / ±127 = „dreht schneller als 5°/30 s",
  `Timestamp` 60–63 = Statuscodes statt Sekunden, `Eta.Month`/`Day` 0 = keine
  Angabe.
  SOG und COG werden **beim Einlesen** auf `null` normalisiert
  (`normalizeSog`/`normalizeCog`/`normalizeHeading`), nicht erst beim
  Anzeigen — sonst rutscht der Rohwert an jedem Helfer vorbei, der ihn direkt
  ausgibt. Genau das ist passiert: Die Tabelle zeigte ein Schiff mit
  „102,3 kn" und „Kurs 360". Gemessen über der Deutschen Bucht steckt in
  **11 % aller Positionsmeldungen** ein COG-Sentinel und in 1 % ein
  SOG-Sentinel — das ist kein Randfall.
  **Achtung bei der Zuweisung:** Die Felder werden gesetzt, sobald die
  Nachricht sie *mitbringt*, nicht wenn der Wert nicht-null ist. Eine Meldung
  „gerade kein Fix" muss einen alten Wert löschen statt ihn stehen zu lassen;
  gültige Nullen (0 kn, Kurs 0°) müssen aber erhalten bleiben.
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
- **Kartenmarker kodieren Typ und Größe** (siehe eigenen Abschnitt oben):
  sechs validierte Kategoriefarben, vier Größenstufen S/M/L/XL am Durchmesser,
  Form trennt Schiff / Seezeichen / Landstation, einklappbare Legende
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
- **Angepasst für iPhone und iPad** (siehe eigenen Abschnitt oben): Technik
  hinter einer Schublade, Tabelle als Kartenliste, Safe-Area-Ränder,
  kein Zoom beim Fokus in Eingabefelder
- **Abfragebox mit 50 km Rand** (siehe eigenen Abschnitt oben): Server
  liefern mehr als sichtbar ist, Tabelle und Marker filtern exakt auf die
  Kartensicht — beim Verschieben sind Schiffe sofort da
- **Filterleiste über Karte und Tabelle** (siehe eigenen Abschnitt oben):
  Schiffstyp und Navigationsstatus als Mehrfachauswahl, beide Ansichten
  zeigen immer dieselbe Auswahl, Zustand wird gemerkt
- **Autostart**: Snapshot laden und verbinden beim Seitenaufruf, beides
  abschaltbar; Verbinden nur mit bereits gespeichertem Key
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
- **Registeranreicherung je MMSI** (siehe eigenen Abschnitt oben): Digitraffic
  liefert IMO, Rufzeichen, Ziel, ETA, Tiefgang und Maße, Wikidata
  Bruttoraumzahl, Schiffsklasse, Eigner, Betreiber, Werft, Baujahr,
  Registerhafen, Foto und den echten Schiffstyp. Läuft automatisch beim
  Öffnen eines Schiffs (abschaltbar) oder per Button, mit lokalem Cache
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
  inland:{eni,eriCode,eriLabel,eriLength,eriBeam,eriDraught,blueCones,loaded},
  persons:{crew,passengers,personnel}, personsAt,
  enrich:{ sources:[...], dt:{<Digitraffic>}, <Wikidata-Felder> },
  enrichState:'loading'|'done'|'empty'|'error', enrichError,
  snapshotSource, snapshotSeen, staticFromCache,
  firstSeen, updatedAt, marker }
```

`enrich.dt` und die Wikidata-Felder liegen bewusst getrennt — beide Quellen
haben ein `beam`, siehe Anreicherungsabschnitt.

Zentrale Funktionen: `getShip(mmsi)` legt den Datensatz an, `recordMessage()`
zählt/merkt die Rohnachricht, `setPosition()` schreibt Position + Track +
Marker. Wer einen neuen Nachrichtentyp ergänzt, hängt einfach einen weiteren
`else if`-Zweig in `processMessageText()` an und füllt die passenden Felder —
Tabelle, Popup und Detailansicht ziehen automatisch nach.

## Testen — die Sandbox ist offen, aber nur zur Hälfte

**Alles unten am 24. Aug. 2026 gemessen, nicht vermutet.** Die frühere Notiz
„aisstream.io, openwaters, unpkg sind blockiert, echte Tests unmöglich" ist
**überholt** — sie stimmt für `curl`/Node nicht mehr.

### Was geht und was nicht

| Von wo | HTTP | HTTPS / WSS |
|---|---|---|
| `curl`, Node | ✅ | ✅ **auch direkt ohne Proxy** |
| Chromium (Playwright) | ✅ | ❌ `ERR_CONNECTION_RESET`, **bei jedem Host** |

Per `curl`/Node erreichbar und verifiziert: `ais.openwaters.io` (REST,
WebSocket **und** Token-Minting über `POST /v1/keys`), `unpkg.com`,
`tile.openstreetmap.org`, `query.wikidata.org` (SPARQL), `www.wikidata.org`
(API), `aisstream.io`.

**Chromium kommt per HTTPS nirgends hin** — auch nicht zu `example.com`.
Plain HTTP funktioniert. Erfolglos durchprobiert, spar dir die Zeit:
`--proxy-server`, Playwrights `proxy:`-Launchoption,
`--ignore-certificate-errors`, `--disable-features=EncryptedClientHello`,
`--disable-quic`. Der Proxy protokolliert die Requests nicht einmal, es ist
kein CA-Problem.

### Trotzdem live testen: zwei Brücken

Beides ist erprobt, Skripte lagen im Scratchpad der Session (`liveproxy.js`,
`wsbridge.js`, `livefull.js`) — bei Bedarf nach diesem Muster neu bauen.

**1. HTTPS: im Test abfangen und aus Node beantworten.** Node kann TLS, der
Browser nicht — also alle `https://`-Requests umleiten:

```js
await page.route(/^https:\/\//, async route => {
  const req = route.request();
  const res = await fetch(req.url(), { method: req.method(), headers: req.headers(),
    body: ['GET','HEAD'].includes(req.method()) ? undefined : req.postData() });
  const body = Buffer.from(await res.arrayBuffer());
  const headers = Object.fromEntries(res.headers);
  delete headers['content-encoding']; delete headers['content-length'];
  headers['access-control-allow-origin'] = '*';
  await route.fulfill({ status: res.status, headers, body });
});
```

Damit läuft im Test **echtes** Leaflet von unpkg, es kommen **echte**
OSM-Kacheln, `GET /v1/vessels` liefert **echte** Schiffe, und sogar der
„Token holen"-Button funktioniert im Browser. Kein `npm pack leaflet` und
keine Fake-Kacheln mehr nötig — der alte Weg über lokal entpackte
Leaflet-Dateien ist damit hinfällig.

**2. WebSocket: kleine Plain-`ws://`-Brücke in Node.** Der Browser kann
Plain-WS zu localhost, Node kann WSS nach draußen:

```js
const { WebSocketServer } = require('ws');           // npm i ws
new WebSocketServer({ port: 9001 }).on('connection', page => {
  const up = new WebSocket('wss://ais.openwaters.io/v0/stream');
  const queue = [];
  up.binaryType = 'arraybuffer';
  up.onopen    = () => queue.splice(0).forEach(m => up.send(m));
  up.onmessage = e => page.send(typeof e.data === 'string' ? e.data : Buffer.from(e.data));
  page.on('message', m => up.readyState === 1 ? up.send(m.toString()) : queue.push(m.toString()));
  page.on('close', () => up.close());
});
```

Im Test dann einfach `#serverUrl` auf `ws://localhost:9001` setzen — das
Feld ist frei editierbar, es braucht keinen Eingriff in den Client. So
getestet: echter Live-Stream im Wattenmeer, „verbunden", Schiffe mit echten
Namen, Flaggen und Navigationsstatus in der Tabelle.

**Was nicht funktioniert:** Playwrights `page.routeWebSocket()` mit
`connectToServer()`. Der Handler feuert, `connectToServer()` wirft nicht,
aber die Serververbindung schließt sofort mit Code 1006 — Playwright baut
sie über den **Browser**-Netzwerkstack auf, und der kann kein WSS. Deshalb
die Brücke als eigener Prozess.

### Wann weiterhin gefaked wird

Live ist nicht immer besser. Mit dem Fake-WebSocket (`page.addInitScript`)
testen, wenn es um **Determinismus oder um Nachrichten geht, die real nie
kommen**:

- Binärnachrichten DAC 200 FI 10 / FI 55 — der Feed führt sie schlicht nicht
  (siehe eigener Abschnitt oben), synthetisch ist der einzige Weg
- Kapazitätsschätzung an Referenzschiffen mit exakt gesetzten Maßen
- Sentinel- und Fehlerfälle (`TrueHeading` 511, `RateOfTurn` ±127,
  `@`-Padding, XSS-Versuche in Namen, Gzip-Binärframes)
- Der Fallback ohne Leaflet (CDN-Requests einfach `route.abort()`)

**Wichtig beim Fake-WS:** `this.readyState` muss von 0 auf 1 wechseln, bevor
`onopen` feuert — `sendSubscription()` prüft das explizit.

### Weitere Stolpersteine, die Zeit gekostet haben

- **`page.route()` mit Glob matcht diese URLs nicht.** `'**ais.openwaters.io/v1/vessels**'`
  greift nicht, `/ais\.openwaters\.io\/v1\/vessels/` greift. Im Zweifel Regex.
- **Viewport-Filter schlägt bei manuell gesetzter Box zu.** Die Tabelle zeigt
  nur, was in `map.getBounds()` liegt. Wer die Bbox-Felder von Hand auf ein
  anderes Revier setzt, ohne die Karte zu bewegen, bekommt Daten und eine
  leere Tabelle. Im Test entweder die Default-Box lassen oder die Karte
  wirklich per `page.mouse`-Drag bewegen. (Der Client loggt inzwischen einen
  Hinweis, wenn ein Snapshot komplett außerhalb der Sicht landet.)
- **Testserver:** `python3 -m http.server 8000` im Repo-Root, Seite unter
  `http://localhost:8000/aisstream/`. Läuft er nicht, gibt es nur ein
  nacktes `ERR_CONNECTION_REFUSED`.
- Chromium liegt unter `/opt/pw-browsers/chromium-1194/chrome-linux/chrome`,
  immer mit `--no-sandbox` starten.

Es gibt weiterhin **kein Test-Framework und keine Testdateien im Repo** —
alle Tests waren Wegwerf-Skripte im Scratchpad der jeweiligen Session.

## Offene Punkte / mögliche nächste Schritte

- Sobald AISstream.io stabil zurück ist: Default-Server-URL ggf. wieder auf
  `wss://stream.aisstream.io/v0/stream` umstellen (siehe oben)
- Die Bruttoraumzahl kommt jetzt aus Wikidata, **eine echte TEU-Zahl gibt es
  weiterhin nirgends frei**. Dafür bräuchte es eine kommerzielle API
  (Datalastic, Data Docked) — mit Key, und ein Key im Browser-JS ist
  öffentlich, das hieße einen kleinen Proxy und damit erstmals ein Backend.
  Die geschätzte TEU-Zahl ist der bewusste Ersatz
- Weitere freie Quellen, die noch niemand geprüft hat: Kystverket (Norwegen)
  und BarentsWatch haben offene AIS-Daten, letzteres allerdings mit
  Registrierung. Für die Nordsee wäre das die naheliegende Ergänzung zu
  Digitraffic, das ja vor allem die Ostsee abdeckt
- Kein automatisiertes Test-Setup — bei größeren Änderungen die
  Playwright-Smoke-Tests wie oben beschrieben neu aufsetzen. Da jetzt echte
  Live-Tests möglich sind (HTTPS-Relay + ws-Brücke), wäre ein kleines
  eingechecktes `test/`-Verzeichnis statt Wegwerf-Skripten allmählich
  lohnend — bisher wird jede Session neu aufgesetzt
- Die **Statik** überlebt einen Reload inzwischen (siehe eigenen Abschnitt),
  Positionen und Tracks weiterhin nicht — das ist Absicht. Ein Deeplink
  `?mmsi=…`, der die Detailansicht direkt öffnet, fehlt noch
- `MAX_TRACK_POINTS` (300) und die Ship-Map wachsen unbegrenzt über die
  Sitzung; bei sehr langen Läufen wäre ein Aufräumen alter MMSIs
  (`STALE_AFTER_MS` wird bisher nur für das Badge genutzt) sinnvoll
