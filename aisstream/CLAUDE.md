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

### Auf `main` heißt nicht ausgeliefert

**Ein Push auf `main` ist kein Deploy.** GitHub Pages baut daraus einen
eigenen Lauf („pages build and deployment", GitHubs eingebauter Workflow —
es gibt keine `.github/workflows`-Datei), und der kann scheitern. Einmal
passiert und minutenlang unbemerkt geblieben:

```
Error: Failed to get ID Token.
Error Message: Request timeout: /167//idtoken/…?api-version=2.0
##[error]Ensure GITHUB_TOKEN has permission "id-token: write".
```

Eine Zeitüberschreitung beim OIDC-Token in `actions/deploy-pages@v5` — kein
Rechteproblem (acht Läufe davor liefen mit derselben Konfiguration durch)
und kein Codefehler; Bau und Upload waren grün, nur der Deploy-Schritt starb.
Der Nutzer meldete daraufhin völlig zu Recht „nach dem Reload immer noch
dasselbe Problem". Behoben mit `rerun_failed_jobs` auf den Lauf.

**Deshalb nach jedem Deploy gegen die ausgelieferte Seite prüfen, nicht gegen
die Arbeitskopie:**

```
curl -s https://srautip.github.io/aisstream/ | grep -c <neues-Merkmal>
curl -sI https://srautip.github.io/aisstream/ | grep -i last-modified
```

`scratchpad/groessecache.js live` fährt denselben Test gegen die echte Seite
statt gegen `localhost:8000`. Jede Prüfung lief bis dahin nur lokal — genau
deshalb ist der geplatzte Deploy nicht aufgefallen.

**Und beim Nachprüfen im Browser: harter Reload.** Pages liefert die Seite
mit `cache-control: max-age=600` aus (dazu ein CDN-`age`), ein normales
Neuladen kann also bis zu zehn Minuten die alte Datei zeigen.

#### Wie lange ein Deploy wirklich dauert

Gemessen über zwölf Läufe (`created_at` bis `updated_at` des Pages-Workflows):

| | |
|---|---|
| Median | **51 s** |
| schnellster | 41 s |
| langsamster (erfolgreich) | 158 s |
| der eine Ausreißer | 2,5 h — der Lauf, dessen Deploy-Schritt am OIDC-Token starb und von Hand neu gestartet werden musste |

**Ein Deploy dauert also rund eine Minute, nicht mehrere.** Wenn es sich
länger anfühlte, lag es nicht an GitHub:

- **Zu grobes Nachfragen.** Mit 40-Sekunden-Pausen wird ein 51-Sekunden-Bau
  erst beim zweiten oder dritten Versuch bemerkt — aus einer Minute werden
  gemessen zwei bis drei.
- **Der Live-Test danach.** `entfernung.js live` kostet noch einmal 30 bis
  60 s, ein `lauf.js --live` drei Minuten. Das ist Prüfen, nicht Warten, und
  gehört getrennt gezählt.

**Regel: höchstens 3 Minuten auf einen Deploy warten**, dabei alle 10 s
nachfragen. Ist die Seite dann nicht draußen, ist Warten der falsche Zug —
dann den Workflow-Lauf ansehen (`actions_list` → `conclusion`), denn genau
das war der einzige Fall, in dem es wirklich lange dauerte. Ein
fehlgeschlagener Lauf wird nicht von selbst besser.

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

## Datenquellen im Überblick

Sechs externe Quellen, keine davon braucht mehr als einen Schlüssel-losen
Aufruf. Details und Trefferquoten je Quelle stehen in den Abschnitten
darunter — hier nur die Zusammenfassung, damit nichts doppelt gepflegt wird.

**Beide Live-Quellen laufen aktuell über denselben Anbieter, openwaters.io.**
`aisstream.io` selbst ist derzeit **nicht im Einsatz** — der Default-Server
ist der `/v0/stream`-Kompatibilitätsendpunkt von openwaters.io, ein
inoffizieller Drittanbieter, der das aisstream.io-Protokoll 1:1 spiegelt
(Ausweichlösung während der AISstream.io-Störung, siehe „Server-Endpoint —
aktueller Stand" oben). Wer einen offiziellen aisstream.io-Key hat, kann die
Server-URL im UI manuell umstellen — das ist keine Quelle, die der Client
von sich aus anspricht.

| Quelle | Endpunkt | Liefert | Ausgelöst |
|---|---|---|---|
| openwaters-Livestream | `wss://ais.openwaters.io/v0/stream` (Default; `stream.aisstream.io` nur bei manueller Umstellung) | Position, SOG, COG, Heading, Navstatus (`PositionReport` u. Class-B-Varianten); Typ, Maße, IMO, Ziel (`ShipStaticData`); Name/Rufzeichen (`StaticDataReport`); ENI, Personen an Bord (Binär, **standardmäßig aus**) | laufend, solange verbunden |
| openwaters-Snapshot | `GET ais.openwaters.io/v1/vessels?bbox=…` | Position, SOG, COG, Heading, Navstatus, Name, Typcode, Kind (Seezeichen/Landstation) — **keine** Maße, IMO oder Rufzeichen | beim Laden, alle 60 s, nach Kartenschwenk |
| Digitraffic | `GET meri.digitraffic.fi/api/ais/v1/vessels/{mmsi}` | IMO, Name, Rufzeichen, Ziel, ETA, Tiefgang, Typcode, Länge/Breite | beim Öffnen eines Schiffs (Register-Kette) |
| Wikidata | SPARQL, `query.wikidata.org` | Name, Typ, IMO, MMSI, Rufzeichen, BRZ/NRZ, Länge, Breite, Tiefgang, Geschwindigkeit, Baujahr, Klasse, Eigner, Betreiber, Werft, Flagge, Heimathafen, Foto-URL | beim Öffnen eines Schiffs (Register-Kette) |
| Wikimedia Commons | `commons.wikimedia.org/w/api.php` | Foto samt Urheber/Lizenz, gefunden über die IMO im Dateinamen | Fallback, wenn Wikidata kein Foto hat |
| Browser-Geolocation | `navigator.geolocation.watchPosition` | eigener Standort (✕-Symbol, Entfernungsspalte, Sichtlinien) | nach Erlaubnis, laufend |

Digitraffic und Wikidata laufen **hintereinander**, nicht parallel — die aus
Digitraffic gewonnene IMO verdoppelt näherungsweise die Wikidata-Trefferquote
gegenüber der Suche allein über die MMSI. Details, Feldlisten und Zahlen zur
Trefferquote: nächster Abschnitt.

## Betrieb über den eigenen Proxy (abschaltbar, Vorgabe: aus)

Auf der Technikseite lässt sich der Client von openwaters weg auf einen
eigenen Proxy umstellen (`../aisproxy`). **Der bisherige Weg bleibt die
Vorgabe** — der Schalter steht auf aus, bis sich der Server im Betrieb
bewährt hat. Gespeichert in `aisstream_proxy_an` und `aisstream_proxy_url`.

Im Proxybetrieb ändert sich der Datenweg vollständig:

| | direkt | über den Proxy |
|---|---|---|
| Live-Daten | WebSocket zu openwaters + Snapshot alle 60 s | ein WebSocket zum Proxy, nur Änderungen |
| API-Key | nötig | **keiner** — den hält der Proxy |
| Register | Digitraffic → Wikidata → Commons je Schiff | `GET /v1/ship/{mmsi}`, vorgewärmt |
| Verkehr | ein Vollbild je Abruf | gemessen **6,4 KB/min** |

**Umgeschaltet wird zur Laufzeit**, ohne Neuladen: `proxyUmschalten()` räumt
den einen Weg ab und baut den anderen auf. `sendSubscription()`,
`snapshotTick()` und `maybeLoadSnapshot()` steigen im Proxybetrieb vorn aus,
statt dass der Aufrufer das wissen müsste.

**Das Register fällt zurück.** Der Proxy deckt nur seine Region ab, die Karte
darf aber überallhin. Antwortet er mit `register: "offen"` oder gar nicht,
läuft `enrichDirekt()` — die unveränderte alte Kette.

### Das Drahtformat muss auf beiden Seiten dasselbe sein

22 Byte je Schiff, gespiegelt in `proxyEntpacke()` hier und
`aisproxy/src/draht.js` dort. Ändert sich das eine, muss das andere mit —
sonst liest der Client Unsinn, ohne es zu merken. Die Längenprüfung im
Entpacker ist genau dafür da.

**Das Altersfeld war zuerst nicht drin** und musste nachgerüstet werden. Ohne
es müsste der Client „jetzt" annehmen und zeigte Daten als taufrisch, die
gemessen im Median 31 s alt sind — genau die Vortäuschung, die der Entwurf
vermeiden wollte. Übertragen werden **Sekunden seit der Meldung**, nicht ein
Zeitstempel: Ein relativer Wert ist gegen eine abweichende Browseruhr immun.

### Und noch einmal die Hoisting-Falle

`PROXY_URL_VORGABE` stand zunächst per `var` unten beim Proxy-Block, wurde
aber oben beim Wiederherstellen aus dem Speicher schon gebraucht. Ergebnis:
Im Adressfeld stand wörtlich **„undefined"**. Funktionsdeklarationen werden
hochgezogen, `var`-**Werte** nicht. Die Konstante steht deshalb jetzt oben
bei den Elementen, mit Vermerk. Das ist das fünfte Mal, dass dieselbe Falle
in diesem Projekt zugeschlagen hat — der Test hat sie gefunden, nicht das
Lesen.

Geprüft in `scratchpad/proxyclient.js`: der Client gegen einen **echten**
aisproxy, nicht gegen einen Nachbau — Einschalten, Daten, Ausschnittwechsel,
Ausschalten, Neustart. Ein Nachbau hätte genau die Formatabweichung
durchgehen lassen, auf die es hier ankommt.

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

**In der Deutschen Bucht auch: null.** Nachgemessen am 27. Aug. 2026 mit 20
Schiffen aus 53–56 N / 6–13 E: **0 von 20** bei Digitraffic. Gegenprobe mit
MMSIs aus deren eigener Liste: HTTP 200, die Abfrage stimmt also. Die
Flaggenverteilung im Digitraffic-Bestand erklärt es — Schweden 106, Finnland
67, Russland 40, Estland 26 von 400: Es ist finnisches AIS und deckt die
**nördliche und östliche** Ostsee ab. Die 21 % oben stammen aus einer
Stichprobe, die diese Gewässer einschloss.

Praktische Folge für den Client: Wer im heimischen Revier zwischen Ems und
Elbe unterwegs ist, bekommt die IMO **nicht** von Digitraffic, sondern nur
aus `ShipStaticData` (AIS-Typ 5, alle 6 Minuten). Der Digitraffic-Aufruf
kostet dort eine Anfrage und liefert nichts — abschalten wäre falsch (in der
Ostsee trägt er), aber die Erwartung gehört korrigiert.

**Sammelabruf statt Einzelabfrage:** `GET /api/ais/v1/vessels` ohne MMSI im
Pfad liefert den kompletten Bestand — am 27. Aug. 2026 gemessen **1 165
Schiffe in 58 KB in 0,8 s**. Für einen Server, der viele MMSIs braucht, ist
das ein Abruf statt tausender. Im Browser lohnt es nicht, weil dort immer nur
ein Schiff zur Zeit interessiert.

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
  URL muss auf `https://` gehoben werden (macht `wikidataQuery()`).

### Umsetzung im Client

- **Nur für das geöffnete Schiff**, nie für die ganze Tabelle. Alle APIs sind
  Gemeingut; pro Host läuft **eine** Abfrage zur Zeit mit 350 ms Abstand
  (`throttled(host, fn)`) — verschiedene Hosts laufen parallel, siehe
  „Registerabfragen parallelisieren" weiter unten
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
eigene Suche über den Farbtonkreis hält das *strenge* Gate mit **sieben**
Farben.

| Kategorie | Hex | ITU-Typcode |
|---|---|---|
| Frachtschiff | `#2a78d6` | 70–79 |
| Tanker | `#ab2000` | 80–89 |
| Passagierschiff | `#77a400` | 60–69 |
| Fischerei | `#007a61` | 30 |
| Schlepper & Arbeitsschiffe | `#8c2c95` | 31–34, 50–54, 56–58 |
| Sport & Segel | `#ca7197` | 36, 37 |
| Militär & Behörden | `#460fdb` | 35, 55, 59 |
| Sonstige / unbekannt | `#6b6b68` | Rest — neutral, kein Slot |

Gemessen gegen **beide** dominanten Kachelfarben (Land `#f2efe9`, Wasser
`#aad3df`): schlechtestes Paar CVD ΔE 8,8 · normal ΔE 19,0 — alle Prüfungen
bestanden.

**Korrektur:** Hier stand „sechs ist das gemessene Maximum". Das war falsch und
kam von einer zu groben Suche — gleichmäßig verteilte Farbtöne bei *einem*
gemeinsamen L und C. So scheitert schon die Sechserpalette (bestes Ergebnis
CVD ΔE 3,0), während die tatsächlich eingesetzte 8,8 hält: Die Trennung kommt
nicht aus dem Farbton allein, sondern aus L und C **je Slot** — genau dann,
wenn Protanopie den Farbton einebnet, trägt der Helligkeitsunterschied.

Mit per-Slot-Suche (`scratchpad/best7.mjs`, 6813 Kandidaten über den ganzen
Farbtonkreis) kostet der siebte Platz **nichts**: Das schlechteste Paar bleibt
`#ca7197`/`#007a61` bei CVD ΔE 8,8, der nächste Nachbar der neuen Farbe liegt
bei CVD ΔE 11,4 (Arbeit) und Normalsicht ΔE 18,7 (Fracht). Nur drei der 6813
Kandidaten erfüllen zusätzlich Kontrast ≥ 2,9 — alle im tiefen Blauviolett um
`#460fdb`. Cyan- und Stahlblautöne halten die Trennung nur hell (L ≈ 0,74) und
bleiben dann unter 2,6 Kontrast; dunkles Cyan kollidiert mit dem Fischerei-Teal.

**Regel bleibt:** Eine Farbe nie dazuerfinden. Erst suchen lassen, dann den
Validator unter `--pairs all` laufen lassen, und die neue Farbe darf das
schlechteste Paar der Palette nicht verschlechtern.

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

### `Dimension` aus lauter Nullen ist ein Sentinel, kein Maß

Gemeldet als „Verzögerungen, bis der Marker die neuen Abmessungen zeigt". Der
Weg selbst ist schnell — gemessen **7 ms** vom Eintreffen der Statiknachricht
bis zum größeren Marker, und **46 ms** unter Dauerlast (400 Schiffe,
100 Nachrichten/s). Das Problem lag in den Daten, nicht im Rendern.

AIS meldet fehlende Abmessungen als `Dimension {A:0, B:0, C:0, D:0}` — 0 heißt
für jede der vier Strecken „nicht verfügbar". Als **Objekt** ist das aber
truthy, und das schlug an zwei Stellen durch (`scratchpad/nulldim.js`):

| Fall | vorher | jetzt |
|---|---|---|
| Class A: echte Maße, dann eine Nullmeldung | Marker fällt auf den 11-px-Ring zurück, Tabelle zeigt weiter „291×40" | Marker bleibt bei 20 px |
| Class B: erst Nullen, dann echte Maße (Msg 19) | Marker wächst **nie** | Marker wächst |

Der erste Fall ist der gemeldete: Class A wiederholt seine Statik alle sechs
Minuten, der Marker sprang also im Takt zwischen richtiger Größe und Ring hin
und her. Der zweite ist ein Dauerzustand — `if (msg.Dimension && !entry.dim)`
ließ sich von einem Nullobjekt **dauerhaft** blockieren, und Msg 5 kommt bei
Class B nie.

`usableDim(dim)` gibt die Maße nur zurück, wenn `A+B` eine Länge ergibt, und
steht jetzt an **jeder** Zuweisung: Msg 5, Msg 19, Msg 24, Seezeichen,
Registeranreicherung, Statik-Cache (alte Bestände können noch Nullobjekte
enthalten) und `rememberStatic()`. Maßgeblich ist die **Länge** — sie trägt
Markergröße und Größenklasse; eine fehlende Breite ist kein Grund, die Länge
wegzuwerfen (`shipSize()` verlangt für den Text „L×B" weiterhin beides).

Msg 19 füllt dabei nicht mehr nur Lücken, sondern übernimmt jede brauchbare
Angabe — die Nachricht wiederholt sich, also darf eine spätere die frühere
ersetzen. Nur die **Registeranreicherung** bleibt füllend: Der Stream ist
aktuell, das Register kann Jahre alt sein.

**Merke:** Dieselbe Familie wie SOG 102,3 und COG 360 — ein Sentinel, der als
echter Wert durchrutscht. Bei `Dimension` ist er besonders tückisch, weil er
nicht als Zahl auffällt, sondern als vorhandenes Objekt.

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

**Fahrtrichtung** kommt als kleiner Pfeil **im** Punkt dazu (siehe unten) —
im Inneren, damit Durchmesser und farbiger Rand unangetastet bleiben.

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

#### „sport" steckt in „Autotran**sport**schiff"

Gemeldet: *„Das Autotransportschiff IMO 9561277 wird unter Sport/Segel
gefiltert?"* — Glorious Ace, MMSI 319409000. Wikidatas `P31` sagt
`Autotransportschiff`, und die Regel `/yacht|segel|sport/i` traf **„sport" an
Position 8, mitten im Wort**. Dieselbe Fehlerfamilie wie „Marines"/„Marlin"
beim MMSI-Weg.

**Der Reflex „dann eben Wortgrenzen" wäre hier grundfalsch.** Deutsche
Komposita tragen das Grundwort **hinten**, und genau davon lebt die Regel.
Gemessen über die 300 häufigsten `P31`-Werte von Schiffen mit IMO:

| P31 | Kategorie | Treffer sitzt | Schiffe |
|---|---|---|---|
| Öl**tanker** | tanker ✅ | mitten im Wort | 3070 |
| Zement**frachter** | cargo ✅ | mitten im Wort | 197 |
| Auto**fähre** | passenger ✅ | mitten im Wort | 109 |
| Motor**yacht** | leisure ✅ | mitten im Wort | 145 |
| Hafen**schlepper** | working ✅ | mitten im Wort | 33 |

Eine `\b`-Regel hätte allein 3070 Öltanker in die Neutralfarbe geworfen. **Die
Teilzeichenkette ist für Grundwörter richtig** — falsch ist sie nur, wenn das
Stichwort zufällig in einem *fremden Stamm* steckt.

Betroffen war genau ein Stichwort, „sport" in „Transport":

| P31 | vorher | jetzt | Schiffe |
|---|---|---|---|
| Autotransportschiff | leisure | **cargo** | 484 |
| Tiertransporter | leisure | **cargo** | 45 |
| Truppentransporter | leisure | **military** | 22 |
| Transportunternehmen · Amphibisches Transportdock · attack transport · Holzspänetransporter | leisure | ITU-Code | 33 |

**Der Fix ist zweiteilig**, und beide Hälften braucht es:

1. **`transport` wird vorher abgefangen** — `truppentransport` → `military`,
   `autotransport|fahrzeugtransport|tiertransport|car carrier` → `cargo`.
2. **Die Sportregel verlangt, dass kein Buchstabe davorsteht**:
   `/yacht|segel|(^|[^a-zäöüß])sport/i`. „Sportboot" trifft weiter, „Transport"
   nicht mehr. Nur dieses eine Stichwort bekommt die Schranke — `yacht`,
   `tanker`, `fähre` und die anderen Grundwörter behalten die freie
   Teilzeichenkette, weil sie sie brauchen.

Vier Werte fallen jetzt auf den ITU-Code durch statt eine Registerkategorie zu
bekommen. Das ist **besser als eine geratene**: „Transportunternehmen" ist eine
Reederei, gar kein Schiff.

**Kontrolliert, nicht gehofft:** Über alle 300 Typen ändern sich **7** (584
Schiffe), **293 bleiben gleich**. `scratchpad/typkategorie.js` hält beide Seiten
fest — die drei reparierten Fälle **und** zwölf Komposita, die weiter treffen
müssen. Gegenprobe mit zurückgebautem Fix meldet exakt die gemeldete
Fehlzuordnung (`leisure`) und lässt die Komposita grün.

**Testfalle dabei:** Der Client liest aus der SPARQL-Antwort `?typeLabel`, nicht
`?type`. Wer den Mock falsch stellt, bekommt überall den ITU-Code zurück und
misst gar nichts — im ersten Lauf standen deshalb 12 falsche „FEHL".

### Ladereihenfolge — schon zweimal reingefallen

`addMapLegend()` stand zuerst im Karten-Init-Block, also **vor** der
`var`-Initialisierung von `TYPE_COLORS`. Ergebnis: `TYPE_COLORS.forEach` wirft,
die IIFE bricht ab, **keine** Event-Listener werden mehr registriert — die
ganze Seite reagiert auf nichts, ohne sichtbare Fehlermeldung. Derselbe
Fehlertyp wie bei `loadStaticCache()`.

**Merke:** In dieser Datei sind Funktionsdeklarationen gehoistet, `var`-Werte
nicht. Alles, was auf die Konstanten weiter unten zugreift, gehört in den
Verdrahtungsblock am Ende der IIFE, nicht in den Init oben.

### Fahrtrichtung: kleiner Pfeil im Punkt

Die vierte Kodierung am Marker, nach Farbe (Typ), Durchmesser (Länge) und Form
(Entitätsklasse). Ein gefüllter **Pfeil innerhalb** des Punktes, Spitze in
Fahrtrichtung. Der Punkt behält damit seinen Umriss: Durchmesser (Größe) und
farbiger Rand (Typ) bleiben vollständig lesbar, die Richtung sitzt im Inneren.

**Pfeil, nicht Dreieck.** Ein gleichschenkliges Dreieck hat an *beiden* Enden
eine markante Kante — die breite Grundseite drängt sich optisch vor die Spitze,
und man liest die Richtung falsch herum (mir selbst passiert, siehe unten).
Der Pfeil ist nur an einem Ende bewehrt, seine Leserichtung ist eindeutig.
Nebenbei verdeckt der schmale Schaft **weniger** Innenfläche als das Dreieck,
die Typfarbe bleibt also besser sichtbar, nicht schlechter.

Geometrie relativ zum Innenradius `r = px/2 − Randbreite` (1,5 px beim gefüllten
Punkt, 3 px beim Ring-Marker): halbe Länge `0,92·r`, Kopflänge `0,78·r`, halbe
Kopfbreite `0,60·r`, halbe Schaftbreite `0,20·r` — **mit Untergrenze 0,55 px**,
weil der Schaft beim 8-px-Punkt sonst unter einem halben Pixel bliebe und im
Antialiasing verschwände. So bleibt zu jeder Größenstufe ein farbiger Rand
stehen.

**Der erste Punkt des Pfads ist die Spitze.** Darauf baut die Winkelprobe in
`scratchpad/spitze.js` auf (`getPointAtLength(0)`) — wer den Pfad umschreibt,
muss das erhalten oder die Probe mitziehen.

**Zwei Füllfarben, aus einem Grund:** Auf der gefüllten Scheibe trägt **Weiß**
den Kontrast, im hohlen Ring-Marker („Länge unbekannt") ist der Kern weiß —
dort die **Typfarbe**. Beide Varianten bleiben sichtbar, ohne die Typfarbe zu
ersetzen. Ein Pfeil in einer eigenen Farbe würde eine zweite, konkurrierende
Kodierung aufmachen.

**Wann er erscheint** (`travelCourse()`):

1. `entry.course` (COG) — die Richtung, in die das Schiff **fährt**.
2. sonst `entry.heading` (Bugrichtung) als Rückfall — nachrangig, denn ein quer
   versetztes Schiff zeigt woanders hin, als es fährt.
3. sonst `null` → **kein Pfeil**. Die Abwesenheit einer Angabe darf nicht wie
   „fährt nach Norden" aussehen.

Dazu eine Fahrtschwelle: **unter 0,5 kn kein Pfeil** (`DIR_MIN_SOG`). Ein
festgemachtes Schiff sendet weiter einen Kurs, der sich zufällig dreht — ohne
die Schwelle stünde eine Reede voll zappelnder Pfeile. Fehlt die
Geschwindigkeit ganz, entscheidet allein der Kurs.

Beide Quellen sind schon am Eingang von ihren Sentinels befreit
(`normalizeCog` verwirft 360, `normalizeHeading` die 511) — im Marker-Code
deshalb **nur auf `null` prüfen**, nicht noch einmal auf 360/511.

#### Gedreht wird ohne Icon-Neubau

Der Kurs ändert sich mit **jeder** Positionsmeldung. Stünde der Winkel im
`iconKey()`, riefe `refreshMarkerIcon()` bei jeder Nachricht `setIcon()` und
baute das Marker-DOM neu — bei 400 Schiffen und 100 Nachrichten/s genau die Art
Dauerlast, die man nicht einbaut. Deshalb dreigeteilt:

- **`iconKey()` trägt nur, OB ein Pfeil da ist** (`"dir"` / `"-"`).
  Erscheinen und Verschwinden sind selten und rechtfertigen einen Neubau.
- **Der Winkel läuft über die CSS-Variable `--dir`** am vorhandenen Element
  (`refreshMarkerDirection()`, aufgerufen aus `syncMarker()`): ein
  Style-Schreibvorgang statt eines DOM-Neubaus.
- **Der Winkel steht zusätzlich inline im Icon-HTML.** Leaflet erzeugt das
  Element bei `setIcon()` und bei **jedem** `addLayer` neu, und
  `applyMapFilter()` hängt Marker beim Verschieben der Karte laufend ab und
  wieder an. Ohne den Wert im HTML stünde der Pfeil danach auf 0° (Norden).

Gemessen (`scratchpad/richtung.js`): Kurswechsel 90° → 180° zieht nach, dabei
**null** `setIcon`-Aufrufe; die Latenz unter Dauerlast bleibt bei rund 50 ms
(vorher 46 ms, `markerlast.js`).

**Richtung nachmessen, nicht auf dem Screenshot beurteilen.** Beim Umbau des
Dreiecks nach innen sah es auf dem Bild verdreht aus — tatsächlich dominierte
die breite Grundseite optisch, und ich hatte sie für die Spitze gehalten. Genau
diese Verwechselbarkeit hat dann den Wechsel zum Pfeil ausgelöst; sie war also
kein reiner Lesefehler von mir, sondern eine echte Schwäche der Form.
`scratchpad/spitze.js` rechnet den Bildschirmwinkel der **Spitze** relativ zur
Punktmitte zurück (`getPointAtLength(0)` + `getScreenCTM()`) und zeigt für
0/45/90/180/270° exakt dieselben Werte. Ein Test, der nur die CSS-Matrix des
`.dir`-Elements prüft, würde eine verdrehte Pfadgeometrie nicht bemerken.

### Zuletzt gewähltes Schiff: gelbes Fadenkreuz mit freier Mitte

Das zuletzt gewählte Schiff bekommt auf der Karte ein gelbes Fadenkreuz
(`#ffd21e`) mit dunklem Umriss.

Drei Entscheidungen, die zusammengehören:

- **Kreuz, kein Ring.** Die Kreisfläche ist in dieser Karte vollständig für
  Schiffstypen vergeben; ein zusätzlicher Ring wäre als Typ lesbar.
- **„+", nicht „✕".** Das schwarze ✕ gehört schon dem eigenen Standort. Zwei
  gleich geformte Kreuze nebeneinander wären verwechselbar, die Farbe allein
  trägt das nicht.
- **Freie Mitte.** Erste Fassung zog zwei durchgehende Balken durch den Punkt.
  Mit Umriss sind das 7 px — bei einem 8-px-S-Punkt bleibt davon nichts übrig,
  und die Typfarbe wäre weg. Genau der Fehler, der die Karte schon einmal
  einfarbig gemacht hat (siehe direkt darunter). Jetzt sind es vier Arme mit
  einer Lücke von `px/2 + 3` — sie wächst mit dem Punkt, der bleibt bei jeder
  Größe vollständig sichtbar.

**Die Auswahl gehört in `iconKey()`.** Sonst merkt `refreshMarkerIcon()` den
Wechsel nicht und das Fadenkreuz bleibt beim alten Schiff stehen.
`openDetail()` zeichnet über `markiereAuswahl(vorher)` genau **zwei** Marker
neu — einer verliert das Kreuz, einer bekommt es. Ein voller Durchlauf über
alle Marker wäre dafür Verschwendung.

#### Die Markierung überlebt das Schließen — zwei Zustände, nicht einer

Gewünscht: *„Behalte die Markierung des zuletzt gewählten Schiffes in der
Karte auch nach schließen der detailansicht."* Wer die Details gelesen und
die Spalte geschlossen hatte, musste den Punkt sonst in der Karte wieder
suchen.

Dafür gibt es **zwei** Variablen, und das ist der Kern der Sache:

| Variable | Bedeutung | endet mit |
|---|---|---|
| `selectedMmsi` | die Detailspalte zeigt dieses Schiff | `closeDetail()` |
| `markierteMmsi` | zuletzt gewählt (Fadenkreuz, `tr.selected`) | Wahl eines anderen Schiffs, Escape, `pruneStaleShips()` |

**Warum nicht einfach `selectedMmsi` stehen lassen:** An einem halben Dutzend
Stellen steht `if (selectedMmsi && ships.has(selectedMmsi)) renderDetail(…)`
ohne Prüfung, ob die Spalte überhaupt offen ist. Die zeichneten ab dann
dauerhaft in eine unsichtbare Spalte — samt Bild- und Registerarbeit. Der
Fehler wäre unsichtbar und teuer. Die zweite Variable kostet dagegen fünf
berührte Zeilen, und alles, was heute an `selectedMmsi` hängt, bleibt
unverändert.

Drei Folgeentscheidungen:

- **`closeDetail()` zeichnet gar nichts mehr neu.** Es ändert sich kein
  Marker, also gibt es auch nichts nachzuführen.
- **Zwei Wege zurück: Escape und der Klick ins Leere.** Erstes Escape
  schließt die Spalte, zweites räumt die Markierung ab — aber ein Telefon hat
  keine Escape-Taste. Deshalb hebt auch ein Klick in den freien
  Kartenbereich die Auswahl auf (`map.on("click", …)`, direkt neben
  `moveend`); beide Wege laufen über `auswahlAufheben()`. Deselektieren heißt
  dabei **Markierung weg und Spalte zu**: Eine offene Detailspalte zu einem
  nicht mehr ausgewählten Schiff wäre ein Widerspruch.
- **`pruneStaleShips()` muss `markierteMmsi` freigeben.** Sonst trüge
  dasselbe Schiff nach 30 Minuten Funkstille bei seiner Rückkehr wieder ein
  Fadenkreuz, das niemand gesetzt hat.

`zeichneAutoTracks()` prüft weiter auf `selectedMmsi`: Übersprungen wird dort
das Schiff mit der **dicken** Spur, und die gibt es nur bei offener Spalte
(`trackLayer` wird beim Schließen entfernt). Nach dem Schließen bekommt das
markierte Schiff seine dünne Spur wie jedes andere.

Geprüft: Kreuz und Punkt sind bei 11, 15 und 20 px exakt konzentrisch
(dx = dy = 0, `scratchpad/kreuzmitte.js`), Typfarbe und Größe bleiben
unverändert, und nach `#detailClose` steht **genau ein** Kreuz an derselben
Bildschirmhöhe wie vorher (`scratchpad/auswahlkreuz.js`). Die Höhe ist der
Beweis, dass es dasselbe Schiff ist — die beiden Testschiffe liegen 0,01°
auseinander. Gegenprobe mit zurückgebautem `closeDetail()`: null Kreuze,
sechs rote Proben.

#### Die Markerprüfung im Kartenklick ist Vorsorge, nicht Notwendigkeit

Der Handler steigt aus, wenn das Klickziel in einer `.leaflet-marker-icon`
liegt. **Gemessen braucht Chromium das nicht:** Mit ausgebauter Prüfung
bleibt die Probe „Klick auf einen Marker wählt weiterhin aus" grün — Leaflet
reicht den Markerklick entweder nicht bis zur Karte durch, oder der
Kartenzuhörer läuft vor `openDetail()`. Genau das ist der Grund, die Zeile
zu behalten: Das richtige Ergebnis hinge sonst an der Reihenfolge, in der
Leaflet seine Zuhörer bedient, und die ist nichts, was hier zugesichert ist.

**Testfalle bei der Leerstelle:** Der erste Anlauf zielte 40 px über den
unteren Kartenrand — dort liegt die Legende, der Klick kam nie bei Leaflet
an. `auswahlkreuz.js` sucht die Stelle deshalb unter mehreren Kandidaten und
verlangt ausdrücklich, dass dort eine Leaflet-Kachel, -Ebene oder der
Container selbst liegt.

### Bildersuche-Quicklink in der Detailansicht

`bildersucheUrl(mmsi)` baut
`https://www.google.com/search?tbm=isch&q=MMSI%20<mmsi>`. Das Wort **MMSI**
gehört in den Suchbegriff: Zu einer nackten neunstelligen Zahl liefert die
Bildersuche beliebiges.

Zwei Plätze, wie gewünscht:

- **Unten in `#detailLinks`, immer** — direkt neben VesselFinder.
- **Oben in `#detailSuche`, nur ohne Foto** — an genau der Stelle, an der
  sonst das Bild steht. Gestrichelter Rahmen, kein gefüllter Kasten: Es ist
  ein leerer Platz, keine Fehlermeldung. Der Text sagt „Kein Foto
  vorhanden", nicht „kein Foto im Register" — der Platzhalter steht auch,
  während die Registerabfrage noch läuft oder wenn sie abgeschaltet ist.

**Beide Stellen entstehen einmal je Schiff, nicht je Nachricht.**
`renderDetail()` läuft bei jeder eingehenden AIS-Nachricht; ein ersetzter
Knoten verschluckt einen Klick, der zwischen Berühren und Loslassen läuft —
dieselbe Falle wie bei den Tabellenzeilen. `#detailSuche` wird nur bei
wechselnder `dataset.mmsi` neu geschrieben und über eine Klasse ein- und
ausgeblendet; `#detailLinks` ebenso, und nur der OpenStreetMap-Link bekommt
bei jeder Position ein neues `href`. Vorher wurde der ganze Linkblock bei
jeder Meldung neu gesetzt — ein Tipp auf VesselFinder konnte ins Leere
gehen.

Geprüft in `scratchpad/bildersuche.js`: Schiff mit und ohne Foto (das Foto
kommt aus einem gefälschten Wikidata-Treffer, Rezept aus `tagebuchbild.js`),
Wechsel in beide Richtungen, und fünf Positionsmeldungen bei offener Spalte
ohne einen einzigen Kindtausch — mit der Gegenprobe, dass der
OpenStreetMap-Link in derselben Zeit sein `href` wirklich geändert hat,
sonst hätte die Probe nichts gemessen.

### Spuren aller sichtbaren Schiffe, solange es wenige sind

Sind **weniger als 100 Schiffe** (Voreinstellung, einstellbar) im
Kartenausschnitt zu sehen, zeichnet
`zeichneAutoTracks()` deren Tracks als sehr dünne Linie (`weight: 1`,
`opacity: 0.55`) **in der Typfarbe des Schiffs** — dieselbe Farbe wie sein
Marker, damit man bei sich kreuzenden Spuren erkennt, wozu welche gehört.
Bei voller Nordsee wäre das ein Wollknäuel und teuer zu zeichnen; darunter
erzählen die Linien etwas.

**Was „sichtbar" heißt:** die Liste aus `applyMapFilter()` — also die
Schiffe, deren Marker wirklich auf der Karte stehen. **Nicht** die
Tabellenauswahl: Die filtert zusätzlich nach dem Suchfeld, die Karte nicht.

**Beschränkung auf den Ausschnitt.** Gezeichnet wird nur, was im Bild liegt
(`map.getBounds().pad(0.15)`). Ein Abschnitt zählt, sobald **ein** Ende
drin liegt — sonst fehlte genau das Stück, das den Rand kreuzt, und die Spur
endete sichtbar zu früh. Verlässt eine Spur das Bild und kommt zurück,
entstehen zwei getrennte Stücke.

**Eine Linie je Typfarbe, nicht je Schiff:** acht Ebenen statt hundert, und
`setLatLngs()` auf einer bestehenden Ebene statt Abbau und Neuanlage.
Gemessen bei **98 Schiffen mit je sechs Spurpunkten** — dem teuersten Fall
knapp unter der Grenze — kostet das Nachzeichnen beim Zoomen **höchstens
21 ms** (Spitzen 21,1 / 20,5 / 16,7). Die Anhebung von 50 auf 100 kostet
also nichts Spürbares; die Schranke der Probe bleibt bei 250 ms.

`interactive: false`, wie beim Sichtkreis: Linien dürfen keine Klicks auf
die Marker abfangen. Das ausgewählte Schiff wird ausgelassen, wenn seine
eigene, dickere Spur eingeschaltet ist — sonst lägen zwei übereinander.

#### Hysterese, sonst flackert es

An der Grenze (Voreinstellung **100**) gehen die Spuren an, aus erst wieder
**fünf Schiffe darüber** (`TRACK_HYSTERESE`). Ohne diesen
Abstand flackerten sie an der Grenze im Sekundentakt, weil ständig ein
Schiff den Ausschnitt betritt oder verlässt. `autotracks.js` fährt die Zahl
deshalb **monoton** hoch und wieder herunter: 20 → 102 (bleibt an) → 108
(aus) → 102 (bleibt aus) → 20 (wieder an).

#### Die Grenze steht auf der Technikseite

Feld `#trackLimit` unter „Karte", Voreinstellung 100, gespeichert unter
`aisstream_track_limit`. **0 schaltet die Spuren ganz ab**, 500 ist die
Obergrenze.

- **Eine Klammer für beide Wege hinein.** `trackGrenzeAus()` prüft Feld
  **und** Speicher: leer → Voreinstellung (nicht 0! `Number("")` wäre 0
  gewesen, also aus), unlesbar → Voreinstellung, sonst gerundet auf 0…500.
  Der geklammerte Wert wird ins Feld zurückgeschrieben — es soll dort stehen,
  was wirklich gilt.
- **Die Änderung muss sofort wirken.** Der Handler setzt `autoTracksAn =
  false` und ruft `applyMapFilter()`. Ohne das nimmt die Hysterese die neue
  Grenze erst zur Kenntnis, wenn die Schiffszahl von selbst darüber
  hinwegläuft — gegengeprüft: mit ausgebautem Aufruf bleibt die Anzeige aus,
  obwohl die Grenze von 50 auf 200 gestellt wurde (Probe 7d rot).

**Testfalle: `starte()` löscht bei _jeder_ Navigation `localStorage`.** Der
`addInitScript`-Schuss läuft auch bei jedem `location.reload()` im Test. Die
Probe „übersteht den Neustart" meldete deshalb einen Fehler, den es nicht
gab — der Wert stand vor dem Neuladen im Speicher und war danach weg.
Deshalb lädt sie eine **zweite Seite im selben Kontext** (`verdrahte(page)`
ohne den Schuss); dort kommen auch die 98 Schiffe aus dem AIS-Cache zurück
und bekommen bei Grenze 30 korrekt keine Spuren.

**Das Raster in `flotte()` hängt an der Grenze mit.** Mit dem alten Raster
(zehn Zeilen à 0,022°, Spalten à 0,055°) wären achtzig Schiffe seitlich aus
dem Bild gelaufen, und die Probe „sichtbare Zahl wie geplant" hätte Unsinn
gemessen. Der Startausschnitt reicht von 53,705 bis 53,885 und von 7,663 bis
8,119 — an den bbox-Feldern abgelesen, weil die Leaflet-Karte in einem
Closure steckt und im Test nicht greifbar ist. Zwölf Zeilen à 0,0127° und
Spalten à 0,04° halten auch 108 Schiffe vollständig im Bild.

**Und die Spuren müssen einen Knick haben.** Leaflet vereinfacht Linienzüge
beim Zeichnen (`smoothFactor`): Eine exakt gerade Spur aus sechs Punkten kam
als **zwei** Punkte im Pfad an — 98 Schiffe ergaben 196 statt 588 Punkte, und
die Kostenmessung sah billiger aus, als sie mit echten Kursen ist. `flotte()`
fährt deshalb Zickzack.

#### Drei Testfallen, alle drei auf einmal

Der erste Lauf war grün, wo er es nicht sein durfte, und rot, wo alles
stimmte:

- **`#map path` fängt auch die Richtungspfeile der Marker ein.** Die haben
  `stroke: none` und keine Breite; bei 60 Schiffen meldete der Test „66
  Spuren", obwohl keine gezeichnet wurden. Richtig ist
  `.leaflet-overlay-pane path` — dort und nur dort liegen Leaflets Vektoren.
- **Leaflet setzt bei einer geleerten Linie `d="M0 0"`.** Ein
  `d.split('M')` zählt das als ein Stück. Es zählen nur Stücke mit
  mindestens zwei Punkten.
- **Der Weg zum Messpunkt zählt mit.** Die Hysterese über den Typfilter
  einzustellen ging schief: Der Filter setzt erst zurück und klickt dann
  Chips einzeln, läuft also zwischendurch über acht sichtbare Schiffe und
  schaltet die Spuren genau dort ein. Gemessen wurde damit nicht die
  Hysterese, sondern der Umweg.

### Eigener Standort: schwarzes ✕, eigener Layer

Ein kleines schwarzes Kreuz mit hellem Umriss, `OWN_POS_PX` = 18 px.

- **Form statt Farbe:** Ein Kreis in irgendeiner Farbe wäre als „noch ein
  Schiff" lesbar — die Kreisfläche ist in dieser Karte bereits vollständig für
  Schiffstypen vergeben. Das Kreuz ist die einzige Marke, die keiner
  Schiffskategorie gehört. Der weiße Umriss (`.halo`, zuerst gezeichnet, 3,5 px)
  hält es über dunklem Wasser und Waldflächen sichtbar.
- **Nicht in `ships`:** eigener Layer, also weder in der Tabelle noch von den
  Filtern noch von `pruneStaleShips()` erfasst. Wer den Standort in `ships`
  legt, bekommt ihn als Geisterschiff in Tabelle, Zähler und AIS-Cache.
- **`interactive: false`** — sonst fängt das Kreuz Klicks auf darunterliegende
  Schiffsmarker ab. `zIndexOffset: 1000` hält es trotzdem obenauf. Geprüft mit
  `elementFromPoint` auf der Kreuzmitte: dort liegt der `leaflet-container`,
  nicht der Marker.
- **`watchPosition`**, nicht `getCurrentPosition`: der Standort soll mitlaufen.
  `enableHighAccuracy: false` — Metergenauigkeit bringt auf einer Seekarte
  nichts und kostet auf dem Telefon Akku.
- Schalter `aisstream_own_pos`, standardmäßig **an**. Der Browser fragt beim
  ersten Mal um Erlaubnis; die Position wird nirgendwohin gesendet.

**Der Fehlerpfad ist der eigentliche Knackpunkt.** Erste Fassung rief bei
*jedem* Fehler `stopOwnPos()` — Watch beenden, Kreuz entfernen. Im Test
verschwand das Kreuz prompt bei der ersten Positionsänderung, weil Chromium
dabei kurz `POSITION_UNAVAILABLE` liefert. Auf dem Telefon wäre es beim ersten
Tunnel oder Gebäude für immer weg gewesen.

Jetzt beendet **nur `code === 1`** (Freigabe verweigert) die Ortung — dann
Schalter aus und merken, sonst fragt jeder Reload erneut und wird erneut
abgelehnt. Alles andere ist vorübergehend: Die Überwachung läuft weiter, das
Kreuz bleibt auf der letzten bekannten Position, und die Meldung erscheint
**einmal** (`ownPosErrLogged`), nicht bei jedem Aussetzer.

**Test-Hinweis:** Ein Playwright-Kontext ohne erteilte Freigabe ruft *gar
keinen* Callback auf — weder Erfolg noch Fehler, auch nach 12 s nicht. Die
Fehlercodes muss man per `addInitScript` selbst stellen
(`scratchpad/ownpos.js`), sonst prüft man Chromium statt der eigenen Logik.

### Entfernung und Peilung: vom eigenen Standort, in Kilometern

Der Bezugspunkt der Sektion „Relativ zu…" ist **der eigene Standort**, sobald
einer vorliegt; die Kartenmitte ist nur noch der Rückfall. Die Überschrift sagt
jeweils, welcher der beiden gilt — ohne das rät man beim Ablesen.

Warum: Die Kartenmitte verschiebt sich bei jedem Scrollen. Eine Entfernung, die
sich ändert, weil man die Karte bewegt hat, beantwortet keine Frage, die
irgendwer stellt. Nachgewiesen in `scratchpad/disttest.js`: Karte um 160 px nach
Osten geschoben → mit Standort bleibt es bei 3,3 km / 90°, ohne Standort springt
derselbe Wert auf 7,5 km / 270°.

**Alle Entfernungen in km**, nicht in Seemeilen: `haversineKm()`,
`trackDistanceKm()`, Ausgabe über `formatKm()`. Unter 1 km ganze Meter — zwei
Nachkommastellen suggerieren dort eine Genauigkeit, die weder die AIS-Position
noch die Handy-Ortung hergibt.

**Geschwindigkeiten bleiben in Knoten.** SOG kommt so aus dem AIS-Feed und steht
in derselben Ansicht direkt darüber; die Ø-Geschwindigkeit aus dem Track wird
deshalb aus km/h zurückgerechnet (`/ 1.852`) statt mitgewandelt.

`showOwnPos()` und `hideOwnPos()` zeichnen die offene Detailansicht neu — sonst
stünden dort bis zur nächsten Nachricht noch die Werte des alten Bezugspunkts.

### „Militärschiff" gibt es im Standard nicht

ITU-R M.1371 kennt **kein** Kriegsschiff. Was es gibt:

| Code | Bedeutung | im Client |
|---|---|---|
| 35 | Military operations | „Militär" |
| 55 | Law enforcement (Polizei, Zoll, Küstenwache) | „Polizei/Behörde" |
| 59 | Nichtkämpfendes Schiff nach RR Res. 18 (Mob-83) | „Nichtkämpfendes Schiff" |

Marineeinheiten senden im Regelfall entweder gar kein AIS oder melden sich
unter 35 — mehr gibt die Datenquelle nicht her. Die drei Codes teilen sich
deshalb **eine** Kategorie „Militär & Behörden"; sie einzeln einzufärben würde
drei Farben für etwas verbrauchen, das sich in der Praxis kaum unterscheiden
lässt.

Vorher lagen 35 und 55 zusammen mit Schleppern und Lotsenbooten im Topf
„Schlepper & Arbeitsschiffe" — das Label in Tabelle und Detailansicht war
korrekt („Militär"), nur Kartenfarbe und Filter kannten die Unterscheidung
nicht. 56, 57 und 59 hatten überhaupt kein Label und standen als „Typ 56" da.

## Namen kommen verzögert — `syncMarker()` ist die einzige Nachziehstelle

Ein Schiffsname trudelt aus bis zu fünf Quellen ein, zeitlich versetzt:

1. `MetaData.ShipName` — liegt an **jeder** Nachricht an, auch an Typen ohne
   eigene Behandlung
2. `ShipStaticData` (Msg 5) / `StaticDataReport` (Msg 24) — alle paar Minuten
3. `ExtendedClassBPositionReport` (Msg 19) — Position **und** Name in einem
4. openwaters-Snapshot bzw. Einzelnachschlag
5. Registeranreicherung (Digitraffic/Wikidata) — auch aus deren
   `localStorage`-Cache, siehe unten

Früher hing an jedem Zweig ein eigenes `bindPopup(popupHtml(entry))` — sechs
Stellen. **Drei Pfade hatten keines**, dort behielt der Marker den alten
Namen, obwohl die Tabelle ihn längst zeigte:

- der `else`-Zweig für unbehandelte Nachrichtentypen (und dort wird
  `MetaData.ShipName` trotzdem übernommen)
- `AidsToNavigationReport` / `BaseStationReport` ohne Koordinaten
- Binärnachrichten außerhalb von DAC 200

**Regel:** Es gibt genau eine Funktion, die einen Marker nachzieht —
`syncMarker(entry)` (Symbol, Popup, Beschriftung). Sie wird **einmal am Ende
von `processMessageText()`** aufgerufen, also hinter allen Zweigen, plus in
`applyEnrichment()` und `applySnapshotFeature()`. Wer einen Nachrichtentyp
ergänzt, muss nichts weiter tun. Kein neues `bindPopup` in einen Zweig
schreiben.

`syncMarker()` vergleicht das erzeugte HTML mit `entry.popupHtml` und bindet
nur bei echter Änderung neu. Leaflets `bindPopup` ruft intern `setContent`,
ein **bereits geöffnetes** Popup aktualisiert sich damit sofort, ohne Klick —
getestet.

### „Manchmal muss man das Schiff neu anwählen, damit sich etwas tut"

Gemeldet für die Kartenansicht. Nachgemessen (`scratchpad/detailupdate.js`,
`popupupdate.js`): **Solange eine eigene Nachricht des Schiffs hereinkommt, ist
alles in Ordnung** — Detailansicht und offenes Kartenpopup ziehen ohne Klick
nach, auch wenn das Schiff aus dem Kartenausschnitt fährt oder ausgefiltert
wird. Der Fehler steckte in den beiden Fällen, in denen **keine** eigene
Nachricht kommt:

**1. Der Snapshot füllte nur Lücken, statt zu aktualisieren.** In
`applySnapshotFeature()` stand für die Fahrtdaten `&& entry.speed == null`:

```js
if (props.sog != null && entry.speed == null) entry.speed = normalizeSog(props.sog);
```

Hatte ein Schiff einmal eine Geschwindigkeit, konnte kein späterer Snapshot sie
mehr ändern — Kurs, Heading und Status genauso. Die **Position** wurde
gleichzeitig bedingungslos gesetzt. Die Karte bewegte sich also, während
Fahrtdaten und Status auf dem allerersten Wert festhingen. Gemessen: neuer
Snapshot mit 27,7 kn, Anzeige blieb bei 9,9 kn.

Die Füll-Regel ist für das **Register** richtig (der Stream ist aktuell, das
Register kann Jahre alt sein) — für den Snapshot ist sie falsch. Ein Snapshot
ist eine Positionsmeldung über einen anderen Weg, also dieselbe Datenklasse wie
der Stream. Richtig ist der **Zeitstempelvergleich**: Neueres übernehmen,
Älteres ignorieren — auch für die Position, die sonst hinter einen frischeren
Livewert zurückspringt. Beide Richtungen sind in `scratchpad/snapshotalter.js`
festgehalten.

**2. Die Zeitfelder der Detailansicht hingen bis zu 15 s hinterher.** Die
Tabelle führt ihr Alter im Sekundentakt (`tickAges`), die Detailansicht baute
sich nur alle 15 s komplett neu auf. Gemessen: Die Tabelle zeigte „11 s",
während daneben in der Detailansicht noch „gerade eben" stand — und ein
erneutes Anklicken brachte den Sprung. Genau der gemeldete Eindruck.

`tickDetailTimes()` hängt jetzt am selben Sekundentakt und fasst **nur die
Zeitfelder** an, kein Neuaufbau: Ein `<dd class="d-rel" data-at="…">` bekommt
neuen Text, dazu das aktuell/veraltet-Abzeichen. Ein voller Neuaufbau im
Sekundentakt wäre der falsche Weg — er nimmt markierten Text weg und würde das
Foto anfassen (siehe oben). Wer ein Zeitfeld ergänzt: dritter Eintrag in der
`section()`-Zeile ist der Zeitstempel, mehr braucht es nicht.

### Tabellenordnung: groß nach klein, Unbekanntes ans Ende

`refreshVisibleShips()` sortiert **zweimal, mit Absicht** — die beiden
Sortierungen beantworten verschiedene Fragen:

1. `entries.sort()` nach `updatedAt` absteigend entscheidet, **welche** Schiffe
   die Tabelle zeigt, bevor `slice(0, MAX_TABLE_ROWS)` bei 200 Zeilen kappt.
2. `shown.sort(byLengthDesc)` entscheidet, **in welcher Reihenfolge** sie
   erscheinen: große Schiffe zuerst, Unbekanntes ganz ans Ende.

**Warum nicht einfach nach Größe sortieren und dann kappen?** Dann würde die
200er-Grenze bei vollem Ausschnitt *jedes* kleine Boot restlos aus der Liste
werfen — und das ausgewählte Schiff könnte mitsamt seiner markierten Zeile
verschwinden. Gekappt wird deshalb weiter nach Alter (die ältesten Meldungen
fallen raus), wie vor der Änderung.

`byLengthDesc()` zieht die Länge aus `shipLengthMeters()` — **derselben**
Quelle wie der Markerdurchmesser, damit Liste und Karte dieselbe Rangfolge
zeigen. Zwei Feinheiten:

- **Gleich lange Schiffe stehen nach MMSI**, nicht nach Aktualität. Hier
  stand einmal das Gegenteil, mit der Begründung, ohne den
  Aktualitäts-Stichentscheid spränge die Liste durcheinander. **Das war
  genau verkehrt herum** — `updatedAt` ändert sich mit jeder
  Positionsmeldung, also war er die Ursache des Springens, nicht das
  Gegenmittel. Siehe „Die Zeile, auf die man zielt" weiter unten.
- **Das Sentinel `Dimension {A:0,B:0,C:0,D:0}` landet bei den Unbekannten**,
  nicht als „Länge 0" ganz vorn — `shipLengthMeters()` gibt dafür `null`
  zurück (`if (l) return l`), nicht 0. Der Sentinel ist in dieser Datei schon
  mehrfach zur Falle geworden, deshalb prüft `scratchpad/sortierung.js` ihn
  ausdrücklich mit.

Der Spaltenkopf trägt ein `▼`, damit die Ordnung ablesbar ist. Sie ist fest,
nicht klickbar sortierbar.

### Entfernungsspalte statt Rufzeichen, Sichtlinie auf der Karte

Die Spalte **„Rufzeichen" ist aus der Tabelle verschwunden** — sie steht in
der Detailansicht, und im Betrieb sucht man sie dort. An ihrer Stelle steht
**„Entfernung"**: Luftlinie vom eigenen Standort, ersatzweise von der
Kartenmitte, wenn keine Standortfreigabe vorliegt. Denselben Bezugspunkt
benutzt die Detailansicht schon lange; der Spaltenkopf sagt ihn per `title`,
denn eine Entfernung ohne Bezugspunkt wäre eine Zahl ohne Bedeutung.

Der Bezugspunkt wird **einmal je Neuzeichnen** bestimmt, nicht je Zeile —
`map.getCenter()` 200-mal zu rufen wäre reine Arbeit ohne Ertrag.

Auf dem Telefon bleibt die Spalte **sichtbar** (anders als `Lat`, `Lon`,
`Zeit`): „Wie weit ist das weg?" ist dort eher wichtiger als am Schreibtisch.

Dazu auf der Karte **zwei gestrichelte Ringe** um den eigenen Standort
(`SICHTLINIEN`): 45 km in `#9d9d9d` und 20 km in `#8f8f8f` — der äußere rund
zehn Prozent heller als der innere (157 gegen 143). 45 km ist grob die VHF-Funkreichweite eines
Landempfängers — der Rand dessen, was überhaupt selbst zu empfangen ist;
20 km gibt der Entfernung einen zweiten Anhaltspunkt, damit man nicht
zwischen „dicht dran" und „am Rand" schätzen muss.

Drei Dinge daran sind nicht verhandelbar:

- **`fill: false`**, nicht `fillOpacity: 0`. Leaflet zeichnet dann gar kein
  Füllelement statt eines unsichtbaren — und zwei übereinanderliegende
  Scheiben würden die Kacheln doppelt einfärben.
- **`interactive: false`.** Eine 45-km-Scheibe, die Klicks abfängt, wäre die
  größte Klickfalle der ganzen Karte.
- Vektoren liegen in Leaflets `overlayPane`, Marker im `markerPane` darüber
  — die Schiffe bleiben also obenauf. Der Test prüft alle drei ausdrücklich.

Der **größere Ring wird zuerst angelegt**, damit der kleinere im DOM darüber
liegt und an einer Kreuzung nicht vom helleren übermalt wird.

`SICHTLINIEN` steht **oberhalb** von `addMapLegend()`, obwohl es fachlich
zum eigenen Standort gehört: Die Legende liest die Werte, und
Funktionsdeklarationen werden hochgezogen, `var`-**Werte** nicht. Der
Symbolradius in der Legende folgt dem echten Verhältnis der Kilometer, statt
fest verdrahtet zu sein.

#### Wie gut sieht man die Ringe wirklich?

Gemessen an den Bildpunkten eines Kartenausschnitts (Helligkeitsabstand
Ring zu Umfeld, 180 Messpunkte rundherum, 0–255):

| Ring | oberes Viertel | Spitze |
|---|---|---|
| 20 km `#8f8f8f` | 15–19 | ~51 |
| 45 km **vorher** `#c9c9c9` | **9** | 27 |
| 45 km **jetzt** `#9d9d9d` | **27** | 48 |

**Der Median täuscht** und steht deshalb nicht in der Tabelle — die Ringe
sind gestrichelt, also fällt rund die Hälfte der Messpunkte in eine Lücke
mit Abstand ~0. Aussagekräftig ist das obere Viertel, also dort, wo der
Strich wirklich liegt.

Mit `#c9c9c9` blieben **neun Stufen von 255** — der Nutzer meldete den Ring
als „kaum sichtbar", und die Messung hatte genau das vorher schon gesagt.
Mit `#9d9d9d` sind es **27**, also das Dreifache, und der äußere bleibt
erkennbar der hellere von beiden.

Der Wert für den 20-km-Ring schwankt zwischen Messungen um ein paar Stufen
(15 bis 19 bei **unveränderter** Farbe), weil er über anderen Kacheln
liegt. Unterschiede unterhalb von etwa fünf Stufen sind damit kein Befund.

Bliebe der Ring einmal zu blass, gibt es neben der Farbe noch `weight`:
Bei `weight: 1` frisst das Kantenglätten auf 2×-Displays einen guten Teil
des Tons.

`scratchpad/entfernung.js` rechnet die drei Entfernungen **unabhängig nach**
(eigene Haversine-Formel im Testskript, nicht die des Clients) — sonst
prüfte der Test nur, dass zwei Kopien derselben Formel übereinstimmen. Der
Kreisradius wird in Bildschirmpixeln gegen die Markerabstände gehalten: zwei
Schiffe innerhalb, eines außerhalb.

**Eine Testfalle dabei:** Der erste Lauf meldete für zwei Schiffe „-". Die
Ursache lag im Test, nicht im Code — bei der Voreinstellung ist die Karte
nur rund 63 km breit und 23 km hoch, die beiden entfernten Schiffe lagen
außerhalb der Bounds und damit weder auf der Karte noch in der Tabelle. Der
Test zoomt jetzt erst heraus.

### Die Zeile, auf die man zielt, muss beim Klick noch dieselbe sein

Gemeldet: *„Wenn die Detailseite zu einem Schiff geöffnet ist, funktioniert
die Selektion eines anderen Schiffes über die Tabelle nicht richtig, sondern
führt zu einem Flackern zwischen Schiff in Detailansicht und neuer
Selektion."*

**Es war nicht der Klick.** Der delegierte Handler auf `#shipTableBody` traf
in sechs von sechs Versuchen genau das Schiff, das im Moment des Klicks in
der Zeile stand. Nur stand dort jedes Mal ein anderes: Zeile 5 hielt
nacheinander D, G, J, A, D, G. Die Gegenprobe (alte Ordnung
wiederhergestellt) zielt auf J und bekommt L, zielt auf E und bekommt G —
**sechs von sechs daneben**.

Zwei Ursachen, beide in `refreshVisibleShips()`:

1. **Der Stichentscheid war `updatedAt`.** Schiffe ohne bekannte Maße liegen
   alle in derselben Größenstufe — und das sind die meisten, bis Meldung 5
   durch ist. Jede Positionsmeldung schob eines davon nach vorn, also
   tauschten sie sekündlich die Plätze. Jetzt bricht die **MMSI** den
   Gleichstand: eindeutig, unveränderlich, und die Reihenfolge steht still.
2. **Der `<tbody>` wurde bei jeder Nachricht komplett neu gebaut** —
   gemessen **8,9 ms je Meldung bei 84 Zeilen**, und in einem belebten
   Ausschnitt kommen dutzende Meldungen je Sekunde. Jetzt wird gedrosselt:
   `refreshVisibleShips()` zeichnet an der **Vorderkante** sofort (ein Klick
   markiert also ohne Verzögerung) und fasst alles, was in den nächsten
   200 ms nachkommt, zu einem Nachlauf zusammen. Gemessen 12 Neuaufbauten in
   3 s statt einem je Nachricht.

Dazu eine dritte Regel, weil auch eine stabile Ordnung sich ändert, sobald
ein Schiff seine Maße nachreicht und aus der „unbekannt"-Gruppe nach oben
springt:

3. **Solange der Zeiger über der Tabelle steht, bleibt die Reihenfolge
   stehen.** `pointerenter` auf `.table-wrap` friert sie ein (`festeOrdnung`
   merkt sich die Ränge), `pointerleave` gibt sie frei. Die **Inhalte laufen
   weiter** — Alter, Position, Fahrtdaten aktualisieren sich, nur der Platz
   bleibt. Neu hinzukommende Schiffe hängen sich hinten an. Nach einem
   `pointerup` wird noch 400 ms festgehalten: Am Finger kommt `pointerup`
   vor dem `click`, und ein Neusortieren dazwischen zöge die Zeile wieder
   weg.

#### Das Einfrieren darf nicht hängen bleiben

Gemeldet: *„In der iPhone-Ansicht funktioniert die Sortierung nach Änderung
Zoom in der Karte nicht zuverlässig."* Genau der Satz „neu hinzukommende
Schiffe hängen sich hinten an" ist beim Zoomen der Schaden: Beim
Herauszoomen sind **alle** neu ins Bild kommenden Schiffe „unbekannt" und
landen unten, unabhängig von der Größe. Nachgemessen mit erzwungenem
Einfrieren:

```
vorher:   nah150(150) > nah120(120) > nah90(90)
nachher:  nah150 > nah120 > nah90 > FERN400 > FERN300 > FERN200
```

Ein 400-m-Schiff unter einem 90-m-Boot. Drei Auflösungswege, damit das nicht
mehr vorkommen kann:

- **Der Kartenzoom gibt immer frei.** Im `moveend`-Handler steht
  `ordnungEinfrieren(false)` vor dem Neuzeichnen. Begründung ist inhaltlich,
  nicht technisch: Wer die Karte bewegt, arbeitet an der Karte, und der
  Inhalt der Tabelle ändert sich dabei zwangsläufig — eine Ordnung
  festzuhalten, die für einen anderen Ausschnitt aufgenommen wurde, ist in
  keinem Fall richtig.
- **`pointercancel`** wird behandelt. Eine Wischgeste, die der Browser zum
  Scrollen übernimmt, endet damit und **nicht** mit `pointerup`. Bisher fing
  das nur ein anschließendes `pointerleave` auf, das Chromium schickt, aber
  nicht jeder Browser garantiert.
- **Der Zustand verfällt** nach 4 s ohne Zeigerereignis
  (`ORDNUNG_FRIST_MS`). Jedes `pointermove` stellt die Frist zurück, wer die
  Tabelle benutzt, merkt also nichts.

**Warum drei Wege und nicht einer:** Welche Geste den Zustand auf echtem iOS
Safari hängen lässt, ist hier **nicht prüfbar** — getestet wird Chromium mit
Touch-Nachbildung, und dort löst er sich sauber (Tippen →
`pointerenter, pointerdown, pointerup`; Wischen → `…, pointercancel,
pointerout, pointerleave`). Statt einen Pfad zu flicken, den man nicht
messen kann, wird die Fehlerklasse beseitigt.

**Testfalle dabei:** Die erste Gegenprobe fror alle 120 ms neu ein, um „ohne
Freigabe" nachzustellen — der Schnappschuss nimmt dann die bereits richtig
sortierte Tabelle auf, und die Probe war grün, ohne etwas zu prüfen.
`ordnungzoom.js` isoliert den `moveend`-Weg stattdessen sauber: Während des
Zoomens läuft alle 500 ms ein `pointermove`, das die Sicherheitsfrist
zurückstellt. Gibt die Ordnung trotzdem frei, kann das nur `moveend` gewesen
sein.

`scratchpad/zeilentreffer.js` misst genau das: sechs Mal zielen (mit einer
Denkpause von 600 ms, wie ein Mensch) und klicken; sechs Treffer. Die
Gegenprobe stellt den `updatedAt`-Stichentscheid wieder her und meldet
zuverlässig sechs Fehlgriffe — ohne sie prüfte der Test nichts.

#### Nachtrag: „manchmal passiert gar nichts"

Nach dem obigen Fix meldete der Nutzer, es sei „meist gut, aber manchmal
immer noch keine Übernahme". Ein anderer Mechanismus, und ein härterer:

**Wird die Zeile zwischen `mousedown` und `mouseup` zerstört, feuert gar
kein `click`.** Der Browser hat kein Ziel mehr, dem er das Ereignis zustellen
könnte — er verwirft es kommentarlos. Gemessen: **zehn von zehn Versuchen
ohne jede Übernahme und ohne ein einziges angekommenes `click`-Ereignis.**
Und `tableBody.innerHTML = ""` zerstörte bei jedem Neuzeichnen alle Zeilen.

Deshalb werden Zeilen jetzt **wiederverwendet statt neu gebaut**
(`zeilenCache`, `holeZeile()`, `zeilenWerte()`):

- Je MMSI ein `<tr>` mit 14 festen `<td>`, die über die Lebensdauer stehen.
- Geschrieben wird nur, was sich geändert hat — ein unverändertes
  `innerHTML` neu zu setzen würde das `<td>` unter dem Finger ersetzen.
- Umsortiert wird nur, wo die Zeile nicht ohnehin an der Reihe ist: Ein
  `insertBefore` nimmt den Knoten heraus und wieder hinein und zerreißt
  damit einen laufenden Klick genauso.

Danach: null von zehn ohne Übernahme, und die Zeile ist nach dem
Neuzeichnen nachweislich **derselbe DOM-Knoten**, während ihre Alterspalte
weiterläuft.

**Folge für `scratchpad/tabellenlast.js`:** Der Test hing danach, weil er auf
`childList`-Mutationen an `#shipTableBody` wartete, die es bei
wiederverwendeten Zeilen nicht mehr gibt. Umgebaut auf einen Beacon an
`#shipCount` und einen Taktgeber — siehe „Lasttests".

Gemessen (`scratchpad/tabellenlast.js`, 400 Schiffe / 200 Zeilen): Der
Tabellenaufbau kostet mit Sortierung 163,5 ms, ohne 163,0 ms — davon sind
150 ms die Entprellung des Sucheingangs. Die Sortierung selbst liegt bei rund
**0,5 ms** und damit im Rauschen. Der Kontrolllauf ohne `shown.sort()` meldet
`sortiert: false`; das ist der Beleg, dass die Probe die Ordnung wirklich prüft
und nicht bloß immer „grün" sagt.

### Und die Tabelle

Die Tabelle wird bei jedem `refreshVisibleShips()` komplett neu gebaut, sie
kann also nur veralten, wenn der Neubau **nicht angestoßen** wird. Genau das
passierte im **Cache-Pfad von `enrichShip()`**: Bei einem Treffer aus
`localStorage` schrieb `applyEnrichment()` Name, IMO, Rufzeichen und Ziel in
den Datensatz und die Funktion kehrte zurück — ohne Neubau. Die Zeile blieb
auf altem Stand, bis zufällig die nächste Nachricht für dieses Schiff kam.
Der Cache-Zweig ruft jetzt selbst `refreshVisibleShips()`.

**Der eigentliche Grund, warum Namen aus Registern nie ankamen:**
`fetchDigitraffic()` hat das Feld `name` schlicht **nicht ausgelesen** —
`imo`, `callSign`, `destination`, `eta`, `draught`, `shipType` und die
Dimensionen wurden übernommen, der Name fiel durch. Deshalb sah es so aus,
als „aktualisiere sich der Name nicht", während Rufzeichen und Typ sehr wohl
erschienen. `applyEnrichment()` nimmt jetzt `data.name || dt.name`
(Wikidata zuerst, weil gepflegter Registername, sonst Digitraffic).

Live gegengeprüft mit drei echten Ostsee-MMSIs ohne Namen im Stream: alle
drei bekommen ihn aus dem Register (Bore Wave, Viggen, Meri) — vorher blieben
alle drei `-`.

### Das Foto flackerte, weil renderDetail zu oft lief

`renderDetail()` läuft bei **jeder** eingehenden Nachricht (über
`refreshVisibleShips()`) und setzte dabei `detailBody.innerHTML` neu. Das
Foto hing mit drin, also entstand jedes Mal ein **frisches `<img>`** — der
Browser begann den Ladevorgang von vorn. Bei einem Schiff, das im
Sekundentakt meldet, wurde das Bild nie fertig oder verschwand wieder.

Das Foto steht deshalb in einem **eigenen Container `#detailMedia`
außerhalb** des neu gebauten Bereichs und wird nur angefasst, wenn sich die
URL ändert (`detailMedia.dataset.src`). Beim Schiffswechsel wird er geleert,
sonst hinge das alte Bild am neuen Schiff, bis dessen Registerabfrage durch
ist.

Gemessen: **eine** Bildanfrage vor und nach 15 Nachrichten in Folge.

**Regel:** Nichts, was Netzwerkressourcen lädt, gehört in einen Bereich, der
per `innerHTML` neu aufgebaut wird.

**Platz im Panel:** `#detailMedia` steht **oben**, zwischen `.detail-sub` und
`#detailBody` — der Name, seine Flaggen-/Klassenzeile, dann das Foto, dann die
Daten. Unter der Sub-Zeile und nicht darüber, weil die Zeile sonst als
Bildunterschrift des Fotos gelesen wird statt als Untertitel des Namens. Der
Container bleibt an dieser Stelle auch dann stehen, wenn er leer ist; er darf
nicht in `#detailBody` wandern (siehe oben).

### Namen auf der Karte

Bis dahin war der Name nur im Popup, also erst nach einem Klick sichtbar.
Jetzt zeigt die Karte ihn als Beschriftung (Leaflet-Tooltip, `permanent`),
ab **Zoomstufe 12** (`LABEL_MIN_ZOOM`) — darunter überlagern sich die Namen
zu Brei. Abschaltbar über „Schiffsnamen auf der Karte", gemerkt in
`aisstream_show_labels`. `refreshAllLabels()` hängt an `zoomend` und am
Schalter.

### `ExtendedClassBPositionReport` (Msg 19) fiel komplett durch

Der Typ landete im `else`-Zweig, seine **Position wurde also gar nicht
ausgewertet** — obwohl er Position, Name, Schiffstyp und Maße in einer
einzigen Nachricht trägt. Im weltweiten openwaters-Bestand kamen 17 von
55 402 Einträgen darüber. Jetzt wie die anderen Positionsberichte behandelt
und im UI standardmäßig abonniert.

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

### Snapshot beim Verschieben, und warum nicht parallel

`loadSnapshot()` lief lange **nur beim Seitenstart** und beim Knopfdruck. Der
`moveend`-Handler zog Bbox-Felder und Subscription nach, holte aber keinen
Snapshot. Wer in ein neues Gebiet schwenkte, sah:

- **mit** Verbindung: Positionen nach Sekunden, Name, Typ und Maße aber erst mit
  der nächsten `ShipStaticData` — bei Class A alle sechs Minuten.
- **ohne** API-Key: **gar nichts, dauerhaft.** `sendSubscription()` steigt ohne
  Socket sofort aus, `refreshVisibleShips()` filtert nur den Bestand.

`maybeLoadSnapshot()` hängt jetzt im `moveend`-Handler. Vier Ausstiege, jeder
mit eigenem Grund:

| Ausstieg | warum |
|---|---|
| `autoSnapshot` aus | der Ausschalter für den Nutzer, Beschriftung passt schon |
| kein openwaters-Server | `/v1/vessels` gibt es nur dort |
| Sicht liegt in `snapshotBox` **und** jünger als `SNAPSHOT_MAX_AGE_MS` (60 s) | der 50-km-Rand deckt kleine Schwenke ab |
| Sichtfläche > `SNAPSHOT_MAX_AUTO_SQDEG` (15) | siehe unten |

**Der Rand ist größer, als man denkt.** Bei Zoomstufe „Deutsche Bucht" sind die
50 km rund **0,76° Länge je Seite** — über eine Bildschirmbreite. Gemessen
(`scratchpad/snapshotpan.js`): 120 px Schwenk kosten keine Anfrage, eine Reise
von 2600 px kostet zwei. Ein Test, der nach einem großen Schwenk *genau eine*
Anfrage erwartet, hat also die falsche Erwartung.

**Flächengrenze nur für den automatischen Weg.** `applyMapFilter()` hat keinen
Deckel — jedes Schiff in der Sicht bekommt eine eigene Leaflet-Ebene. Gemessen
(`scratchpad/markerlimit.js`, echte Snapshot-Antworten wachsender Größe):

| Schiffe | Marker in Sicht | Snapshot einarbeiten | **Schwenk** |
|---|---|---|---|
| 600 | 90 | 106 ms | 224 ms |
| 1500 | 230 | 203 ms | 244 ms |
| 3000 | 429 | 482 ms | **366 ms** |
| 6000 | 893 | 523 ms | 582 ms |
| 12 900 | 1863 | 1144 ms | 1865 ms |

Bei rund 3000 Schiffen kippt es. Dichte europäischer Küstengewässer, ebenfalls
gemessen: Deutsche Bucht 600 Schiffe / 2,5 sq°, Nordsee 12 900 / 91 sq° — also
140–240 je Quadratgrad. 3000 / 200 ≈ **15 Quadratgrad**. Von Hand bleibt jede
Größe möglich, und die Grenze **meldet sich im Log** statt still zu greifen;
stilles Weglassen sieht wie ein Fehler aus.

> **Messfalle:** Ein früherer Versuch schickte N Einzelnachrichten durch den
> WebSocket. Das ist über `refreshVisibleShips()` O(n²) und ergab 200 s für
> 12 900 Schiffe — gegenüber 1,1 s auf dem echten Snapshot-Weg. Wer die Grenze
> nachprüft: über eine Snapshot-**Antwort** messen, nicht über Einzelmeldungen.

**Abbruch statt Auflaufen.** `loadSnapshot(auto)` hat einen eigenen
`AbortController`; ein neuer Schwenk bricht die vorige Abfrage ab, und ein
`AbortError` landet **nicht** als „Snapshot fehlgeschlagen" im Log. Dazu eine
Frist von 15 s — vorher lief die Abfrage mit nacktem `fetch`, und eine hängende
Antwort ließ `snapshotBtn` **dauerhaft** deaktiviert, weil `finally` nie lief.
Ein bestehender kleiner Fehler, den das Auslösen bei jedem Schwenk viel
wahrscheinlicher gemacht hätte.

**`snapshotBox` wird erst nach dem Erfolg gesetzt** — sonst gälte eine
abgebrochene oder fehlgeschlagene Abfrage als abgedeckt und blockierte den
Nachschlag.

**Falle beim Verdrahten:** `snapshotBtn.addEventListener("click", loadSnapshot)`
reicht das `MouseEvent` als erstes Argument durch. Ein Event ist truthy, der
Knopf liefe damit im `auto`-Modus samt Abdeckungslogik. Deshalb steht dort ein
Wrapper mit `loadSnapshot(false)`.

#### Laufendes Auffrischen alle 60 s

Schalter „Snapshot alle 60 s neu holen" (`aisstream_auto_refresh`, standardmäßig
**an**). Hält die Karte auch **ohne API-Key** lebendig — der Snapshot bündelt
mehrere Quellen (`aishub`, `aisstream`, `kystverket`, `digitraffic`) und kennt
damit Schiffe, die ein einzelner Stream nicht liefert.

**Warum 60 s.** Stromaufwärts gibt es keinen festen Takt, die Daten laufen
kontinuierlich nach. Gemessen an derselben Box über wachsende Pausen:

| Pause | Schiffe mit neuer Position |
|---|---|
| 10 s | 60 von 617 (10 %) |
| 30 s | 183 (30 %) |
| 60 s | 281 (46 %) |
| 120 s | 397 (64 %) |

Also ein Kompromiss, keine technische Grenze: 60 s frischt knapp die Hälfte auf.
Schneller zu fragen holt wenig und kostet viel.

`snapshotTick()` hat vier Ausstiege — **ohne** den Abdeckungstest des Schwenks,
denn genau die schon abgedeckte Box soll ja aufgefrischt werden:

| Ausstieg | warum |
|---|---|
| Schalter aus | der Ausschalter |
| `document.hidden` | ein vergessener Tab fragt sonst tagelang weiter |
| `snapshotCtrl` gesetzt | eine Abfrage läuft schon — siehe unten |
| Fläche > `SNAPSHOT_MAX_AUTO_SQDEG` / kein openwaters | wie beim Schwenk |

**Die `snapshotCtrl`-Sperre ist nicht bloß Sparsamkeit.** `loadSnapshot()`
bricht eine laufende Abfrage ab, bevor es eine neue startet. Ohne die Sperre
würde ein Takt, der in eine laufende **Nutzer**-Abfrage fällt, genau diese
abschießen.

**Beim Zurückkommen sofort, nicht erst zum nächsten Takt**
(`snapshotSichtbarWieder()` an `visibilitychange`): Wer den Tab wechselt, sähe
sonst bis zu 60 s lang eine eingefrorene Karte.

**Drei Lautstärken statt eines `auto`-Flags.** `loadSnapshot(modus)`:
`"knopf"` (oder nichts) volle Auskunft · `"schwenk"` eine Zeile · `"takt"`
schweigt. 1440 Logzeilen am Tag wären kein Log mehr. Ein **dauerhaft gestörter**
Takt meldet sich genau **einmal** und dann erst wieder nach einem Erfolg
(`snapshotTickFehler`) — dasselbe Muster wie `ownPosErrLogged` beim eigenen
Standort.

**Der Log hatte keine Obergrenze** und wuchs unbegrenzt — in einer langen
Sitzung ein stiller Speicherfresser, mit dem Takt eine Gewissheit. Jetzt
`LOG_MAX` = 400 Zeilen, älteste zuerst raus.

**Das Intervall wird nicht an- und abgemeldet.** `setInterval(snapshotTick, …)`
läuft immer; ob er etwas tut, entscheidet `snapshotTick()`. Ein Timer, der je
nach Schalter neu registriert wird, geht schneller verloren, als er spart — und
der Timer selbst kostet nichts.

##### Zwei Testfallen, beide haben den Test erst grün gelogen

1. **Vorgestellte Uhr macht die Antwort veraltet.** `page.clock.install()` +
   `fastForward` verschieben `Date.now()` in der Seite. Die `seen`-Zeitstempel
   einer in Node gebauten Fake-Antwort liegen dann in der **Vergangenheit** der
   Seite, und `applySnapshotFeature()` verwirft sie völlig zu Recht
   (Zeitstempelvergleich). Die Positionen bewegen sich nie, der Test prüft
   nichts. Der Mock muss die Zeit **aus der Seite** holen
   (`page.evaluate(() => Date.now())` im Route-Handler).
2. **`runFor(60000)` springt über die 15-s-Frist.** Um die
   `snapshotCtrl`-Sperre zu prüfen, muss eine Abfrage laufen, *wenn* der Takt
   fällt. Ein 60-s-Sprung feuert aber erst die Abbruchfrist und dann den Takt —
   die Anfrage ist längst weg, der Takt darf feuern, und der Test meldet grün,
   ohne die Sperre je berührt zu haben. `scratchpad/snapshottakt.js` sucht
   deshalb erst die **Taktphase** (in 1-s-Schritten vorstellen, bis eine
   Anfrage rausgeht), stellt dann bis kurz davor und startet dort eine bewusst
   langsame Antwort.

Gegenprobe gemacht: Mit auskommentierter `snapshotCtrl`-Sperre meldet die Probe
`FEHL … (1 zusaetzlich)`. Sie prüft also wirklich etwas.

#### Parallele kleine Teilboxen bringen nichts (gemessen, nicht vermutet)

Naheliegende Idee: die Box in vier Teile schneiden und parallel abfragen.
Gemessen gegen die echte API, warme Verbindung, Deutsche Bucht — Median über
fünf Läufe **302 ms für eine Vollbox gegen 318 ms für vier parallele Viertel**,
bei identischer Schiffszahl (599). Nie schneller, mit Ausreißern bis 681 ms.

Drei Messungen erklären das:

1. **Fester Boden von ~172 ms.** Eine *leere* Antwort — 43 Bytes, mitten im
   Atlantik, null Schiffe — braucht warm 172 ms, die volle Bucht mit 600
   Schiffen 197 ms. Der Umlauf dominiert, die Boxgröße kaum. Jede Teilanfrage
   zahlt diesen Boden erneut.
2. **Auch bei großen Boxen nicht.** Nordsee, 12 900 Schiffe, 583 KB gzip: eine
   Anfrage 512/616/602 ms, vier parallel 826/622/799 ms, neun parallel
   818/842/551 ms. Die Übertragung ist bandbreitenbegrenzt, Aufteilen ändert
   die Gesamtbytes nicht.
3. **Kein Trefferdeckel, den man umgehen könnte.** Das war der eigentliche
   Verdacht — schneidet der Server bei einer Anfrage still ab, brächte
   Aufteilen *mehr Daten*, was wichtiger wäre als Tempo. Gegenprobe: dieselbe
   Nordseefläche einmal am Stück und einmal als vier Teile → **12 884 gegen
   12 885 MMSIs**, der eine Unterschied ist ein Schiff, das in den Sekunden
   dazwischen auftauchte. Eine Box von ~800 Quadratgrad lieferte 28 913
   Schiffe in *einer* Antwort.

Nebenbefund: Der `area: 400`-Deckel des Tokens gilt für die
**WebSocket-Subscription**, nicht für die schlüssellose REST-Abfrage — die nahm
800 Quadratgrad anstandslos an. `expandBox()` ist für den Snapshot also
überkorrekt. **Trotzdem nicht ändern:** Dieselbe Funktion bedient die
Subscription, wo der Deckel real ist, und über 400 Quadratgrad ist ein
50-km-Rand ohnehin bedeutungslos.

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

- **Typ** — die sieben Kategoriefarben plus „Sonstige / unbekannt", jeder Chip
  mit seinem Farbpunkt, damit der Bezug zur Karte sofort da ist. Kommt eine
  Kategorie dazu, muss `FILTER_VERSION` hoch: Die Chips sind opt-in, eine
  gespeicherte Auswahl kennt die neue Kategorie nicht und würde deren Schiffe
  stumm ausblenden. Beim Versionswechsel fällt die Typauswahl einmal auf
  „kein Filter" zurück.
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
   füllt die Karte sofort, während der Stream nur langsam eintröpfelt. Derselbe
   Schalter steuert auch das Nachladen beim Verschieben der Karte (siehe
   „Snapshot beim Verschieben").
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
| jede | Einspaltig, Technik als Schublade von rechts; Filter in der Kopfzeile |
| ≥ 1100 px | Die Gruppenbeschriftungen „TYP" / „STATUS" sind eingeblendet |
| ≥ 900 px | Die Detailsicht ist eine **Spalte**, kein Overlay (s. u.) |
| ≤ 1024 px (iPad hoch, iPhone quer) | Zusätzlich: Tabelle ohne Höhendeckel |
| ≤ 700 px (iPhone) | Zusätzlich: Tabelle als Kartenliste, Schublade voll breit, Karte 38 vh statt der 64-vh-Rechnung |

**Die Schublade gilt seit dem Nutzerhinweis auf jeder Breite**, nicht mehr nur
bis 1024 px. Am Desktop und auf dem iPad quer lagen Verbindungs- und Debugdaten
sonst dauerhaft in einer 320-px-Spalte neben der Karte, obwohl man sie im
Betrieb praktisch nie braucht. Die Karte gewinnt dadurch die volle Breite
(gemessen 1392 von 1440 px). Damit entfällt auch die Sonderbehandlung im
Skript: `istSchublade()` und das `immer`-Flag der `DRAWERS` sind weg, beide
Schubladen verhalten sich gleich.

**Folge für Testskripte:** Bedienelemente in `#settings` (API-Key,
`connectBtn`, `snapshotBtn`, die Checkboxen, `clearCacheBtn`) liegen jetzt auch
am Desktop hinter der geschlossenen Schublade — ein `page.click()` /
`page.fill()` läuft dort in einen Timeout, weil Playwright ein fest
positioniertes, weggeschobenes Element nicht ins Bild scrollen kann. In den
Skripten deshalb direkt über das DOM auslösen:
`page.evaluate(() => document.getElementById('connectBtn').click())`.

Vorher rutschte bei ≤ 900 px das komplette Technikpanel **über** Karte und
Tabelle — man scrollte an API-Key, Bounding Box und Log vorbei, bevor ein
einziges Schiff sichtbar wurde. Jetzt hängt es hinter dem Knopf
„Verbindung & Technik" in der Kopfzeile (`#settings`, Schließen per Kreuz,
`Esc` oder Tipp daneben; auf dem Telefon voll breit, weil bei 92 vw nur
32 px Hintergrund übrig blieben — als Tippziel unbrauchbar).

### Filter in der Kopfzeile, Karte ganz nach oben

Gewünscht war **in Summe mehr Platz für Karte und Tabelle**, mit drei
vorgegebenen Ansatzpunkten: keine Überschrift mehr, Filterknöpfe kompakt in
die Kopfzeile, Typfilter einzeilig (auf dem iPad quer brauchten sie zwei
Zeilen), und für die Liste unter der Karte mindestens 30 % übrig.

**Vorher — was der Karte im Weg stand:**

| Gerät | Kopfzeile | Filterleiste | Karte begann bei | Karte | darunter |
|---|---|---|---|---|---|
| iPad Pro 11 quer (1194×834) | 71 | 74 | **177** (21 %) | 420 | 237 (**28 %**) |
| iPad Mini quer (1024×768) | 58 | 101 (Typ **2 Zeilen**) | **187** (24 %) | 323 | 258 (34 %) |
| iPad hoch (834×1194) | 58 | 101 (Typ **2 Zeilen**) | 187 | 501 | 505 (42 %) |
| Desktop 1440×900 | 71 | 74 | 177 | 420 | 303 (34 %) |
| iPhone 15 hoch | 58 | 101 | **281** | 250 | — |

**Nachher — gemessen:**

| Gerät | Kopfzeile | Karte beginnt bei | Karte | darunter |
|---|---|---|---|---|
| iPad Pro 11 quer | 74 | **90** | 460 (+40) | 284 (**34 %**) |
| iPad Mini quer | 74 | **82** | 418 (+95) | 268 (35 %) |
| iPad hoch | 74 | 82 | 690 (+189) | 422 (35 %) |
| Desktop 1440 | 58 | 74 | 518 (+98) | 308 (34 %) |
| iPhone 15 hoch | 107 | **116** | 252 | — |

Auf dem iPhone hoch ist die Kopfzeile höher als vorher (zwei Chipzeilen mit
34-px-Zielen), die Karte beginnt trotzdem 165 px weiter oben — die eigene
Filterleiste war schlicht teurer.

#### Der waagerechte Platz war die eigentliche Hürde

| | iPad Pro quer | iPad 10 quer | iPad Mini quer | iPad hoch | Desktop |
|---|---|---|---|---|---|
| Kopfzeile innen | 1146 | 1032 | 992 | 802 | 1392 |
| rechte Seite **vorher** | 419 | 419 | 419 | 419 | 419 |
| rechte Seite **nur Symbole** | 128 | 128 | 128 | 128 | 128 |
| Typchips **lang** | 916 | 916 | 916 | 916 | 916 |
| Typchips **kurz** (heute) | 531 | 531 | 531 | 531 | 531 |

Die rechte Seite allein kostete 419 px — Verbindungstext 114, „Tagebuch" 118,
„Verbindung & Technik" 163. Erst als sie auf 📖 und ⚙ schrumpfte, war
überhaupt Platz. Der Verbindungstext liegt weiterhin als `.sr-only` im DOM
(`setStatus()` schreibt hinein) und zusätzlich im `title` des farbigen
Punktes — sonst wäre „gelb" nicht mehr auflösbar.

Heute bleiben auf dem iPad Mini quer 855 px Platz gegen 531 px Bedarf, also
über 300 px Reserve gegen abweichende Schriftmetriken auf echtem iOS.

#### Einzeilig ist Struktur, nicht Hoffnung

Beide Gruppen sind `flex-wrap: nowrap` + `overflow-x: auto` — auf **jeder**
Breite, nicht mehr erst unter 700 px. Damit kann aus zwei Zeilen keine dritte
werden; wird es eng (iPhone hoch: 292 px Platz gegen 531 px Bedarf), wischt
man. **`min-width: 0` ist dabei Pflicht** — ohne das wächst die Gruppe auf die
Summe aller Chips statt zu scrollen und zieht die ganze Seite quer. Genau
dieser Fehler steht in dieser Datei schon zweimal.

Die Kurzform steht nur auf den Chips (`short` in `TYPE_COLORS` und
`STATUS_GROUPS`), der volle Name im `title` und `aria-label`. `label` bleibt
unangetastet, weil die **Kartenlegende** daraus die vollen Namen zieht — dort
steht weiterhin „Schlepper & Arbeitsschiffe", auf dem Chip „Arbeit".

#### Die Kartenhöhe ist eine Rechnung, kein Wert

`#map { height: calc(64vh - var(--head-h, 64px)) }`. Kopfzeile plus Karte
belegen zusammen 64 % der Fensterhöhe, für alles darunter bleiben rund 34 % —
gefordert waren 30 %. Die Rechnung ist damit unabhängig davon, wie hoch die
Kopfzeile mit ihren zwei Filterzeilen gerade wirklich ist.

**`--head-h` hat dadurch zwei Abnehmer:** die Detailspalte (`top`) *und* die
Karte. `measureHeader()` stößt deshalb `kartengroesseNachziehen()` an —
ändert sich die Kopfzeile, ändert sich die Karte, und Leaflet erfährt davon
nur, wenn man es sagt. Nur bei echter Änderung (`letzteKopfhoehe`), sonst
dreht sich der `ResizeObserver` im Kreis.

Der `42vh`-Wert im 1024er Block **musste weg**, nicht nur der 420-px-Wert:
Eine spätere Regel hätte die Formel stillschweigend ausgehebelt, und zwar
ausgerechnet auf dem iPad Mini quer.

#### Drei Dinge, die erst die Messung gezeigt hat

- **`all: unset` räumt auch spätere Wirkung ab.** Die Regel
  `@media (pointer: coarse) { .chip { min-height: 30px } }` stand zunächst
  *vor* `.chip { all: unset; … }`. `all: unset` setzt jede Eigenschaft
  zurück, `min-height` eingeschlossen — der Test meldete unverändert 20 px.
  Die Regel muss **nach** der `.chip`-Regel stehen.
- **`all: unset` setzt auch `box-sizing` auf `content-box`.** Aus
  `min-height: 30px` wurden dadurch gemessene 38 px (Polster und Rahmen oben
  drauf) und die Kopfzeile wuchs auf 94 px. Mit `box-sizing: border-box`
  sind es die gewollten 28 px.
- **Ein langer Knopftext lässt die Kopfzeile nicht mehr wachsen.** Die zwei
  Chipzeilen sind höher als jeder Knopf. Die `--head-h`-Probe in
  `detailtouch.js` hat damit nichts mehr geprüft und war trotzdem grün —
  sie braucht jetzt einen 160-px-Klotz und fordert echtes Wachstum.

#### Was Testskripte davon merken

- `.filterbar` gibt es nicht mehr; der Container heißt `.headfilter`.
  **Auch im Zurücksetzen-Handler im Client** — mit dem alten Selektor bliebe
  der Filter weg, die Chips aber sichtbar gedrückt.
- Chiptexte sind kurz. `:has-text("Frachtschiff")` trifft nichts mehr;
  `#typeFilter .chip[title="Frachtschiff"]` ist der stabile Weg.
- Die Tabellenüberschrift steht in `.search-row`, nicht mehr in einer eigenen
  Zeile.

### Detailsicht ab 900 px: Spalte statt Overlay

Gemeldet: *„Auf dem iPad in Queransicht funktioniert die Selektion eines
anderen Schiffes in der Karte nicht, wenn schon die Detailsicht zu einem
Schiff geöffnet ist."*

**Was es *nicht* war** — beides nachgemessen, bevor irgendetwas geändert
wurde:

- *Verdeckte Marker.* Im geprüften Ausschnitt lag `ship-mark` ganz oben,
  `elementFromPoint` bestätigt es.
- *Tipptoleranz.* Mit echten Touch-Ereignissen über CDP liegt die
  Driftschwelle bei **~13 px — mit und ohne offene Leiste identisch**. Ein
  erster Lauf hatte 16 px gegen 20 px gemeldet und sah nach einem Befund aus.
  Erst drei Wiederholungen je Schritt zeigten, dass das Rauschen war.
  **Merke: eine einzelne Driftmessung ist keine Messung.**

**Es war schlicht Fläche.** `#detail` ist 380 px breit und lag als Overlay
über der Karte, von der Kopfzeile bis zum Seitenende:

| Gerät | Karte | Leiste verdeckt | Legende | zusammen |
|---|---|---|---|---|
| iPad Mini quer (1024) | 992 × 323 | 37 % | 14 % | **51 %** |
| iPad Pro 11 quer (1194) | 1146 × 420 | 31 % | 14 % | **45 %** |
| Desktop 1440 | 1392 × 420 | 26 % | 11 % | 37 % |

Auf dem iPad quer war also rund die halbe sichtbare Karte nicht antippbar,
sobald ein Schiff offen war. Nebenbei lagen auch die rechten Tabellenspalten
darunter.

**Jetzt:** Ab 900 px hält `main` den Platz frei, statt ihn herzugeben.

- `--detail-breite: 380px` steht einmal in `:root`; `#detail` und die
  Media Query benutzen dieselbe Zahl. Liefen die beiden auseinander, bliebe
  genau der Streifen verdeckt, um den es geht.
- `main { padding-right: calc(1.5rem + var(--detail-w, 0px)) }`,
  `@media (min-width: 900px) { body.detail-spalte { --detail-w: … } }`.
- `updateScrollLock()` heißt jetzt `updateBodyState()` und **leitet** die
  Klasse `detail-spalte` aus dem tatsächlichen Zustand ab, wie schon die
  Scroll-Sperre. `openDetail()`/`closeDetail()` setzen sie nicht selbst —
  sonst wiederholt sich der alte Fehler, dass das Schließen einer Schublade
  eine Sperre wegnimmt, die noch jemand anders braucht. Der alte Name bleibt
  als einzeiliger Weiterleiter stehen.

**Warum 900 und nicht 1025:** Der schlimmste Fall ist ausgerechnet das iPad
**Mini** quer mit 1024 px. Eine Grenze darüber hätte genau ihn ausgelassen.
Bei 900 px bleiben nach Abzug der Leiste noch ≥ 520 px Karte (gemessen:
612 px auf dem Mini). iPad hochkant (768/834) bleibt bewusst Overlay, dort
blieben sonst unter 460 px übrig.

#### Zwei Stellen, die der Test gefunden hat und Nachdenken nicht

- **`@media (max-width: 1024px) { main { padding: 0.75rem 1rem 2rem } }`
  überschrieb die Kurzform komplett** und nahm den freigehaltenen Platz
  wieder weg — bei *genau* 1024 px, also beim iPad Mini quer, dem Fall, um
  den es ging. Die Regel trägt den `--detail-w`-Anteil jetzt mit. Wer eine
  weitere `padding`-Kurzform für `main` schreibt, muss ihn ebenfalls
  mitnehmen.
- **`map.invalidateSize()` braucht das Panning (Leaflet-Standard),
  `{pan: false}` reicht nicht.** Gemessen am gemeldeten Fall: Schiff B lag
  bei x = 871, die geschrumpfte Karte endet bei x = 790. Mit Panning bleibt
  der geografische Mittelpunkt, der Inhalt rückt 190 px nach links, B landet
  bei x = 681 und ist antippbar. Mit `{pan: false}` bleibt B bei 871 —
  außerhalb der Karte, und der Test meldet wieder den ursprünglichen Fehler.
  Der Aufruf kommt **nach** der 0,18-s-Einblendung (`setTimeout` 220 ms),
  sonst misst er die Zwischengröße; und nur beim Öffnen und Schließen, nicht
  in `renderDetail()`, das bei jeder Nachricht läuft.

`invalidateSize()` feuert `moveend`, und daran hängen `sendSubscription()`,
`maybeLoadSnapshot()` und `refreshVisibleShips()`. Das ist richtig, der
Ausschnitt ändert sich ja wirklich — und die Abdeckungsprüfung in
`maybeLoadSnapshot()` erkennt den *kleineren* Ausschnitt als bereits gedeckt:
zehnmal öffnen und schließen ergibt **0 zusätzliche Snapshot-Anfragen**
(gemessen, auf allen vier Profilen).

#### Testfalle: `td.c-time` ist der falsche Maßstab

Die Prüfung „liegt die rechte Tabellenspalte links von der Leiste?" schlug
zunächst überall fehl, auch am Desktop mit reichlich Platz. `.table-wrap` hat
`overflow: auto` — die Zelle ragt als *Layoutkasten* weit über den sichtbaren
Rand hinaus und wird dort abgeschnitten. Gemessen gehört der sichtbare
Kasten (`.table-wrap`), plus eine `elementFromPoint`-Kontrolle direkt hinter
der Leistenkante.

`scratchpad/detailspalte.js` sucht Schiff B nicht auf einer festen Koordinate,
sondern **kalibriert sich selbst**: zwei Marker messen, daraus Pixel je Grad
ableiten, dann B in den Streifen setzen, den die Leiste belegt. So läuft der
Test bei jedem Zoom und auf jedem Gerät. Gegenprobe per
`addStyleTag('body.detail-spalte { --detail-w: 0px !important }')` — das
stellt exakt das alte Overlay wieder her, und Probe 1 meldet dann den
gemeldeten Fehler.

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
  `TOP_RESERVE_PX` = 90 hält zusätzlich die **Zoomknöpfe** frei: Bei einer
  420 px hohen Karte wuchs die aufgeklappte Legende bis in sie hinein und
  fing deren Klicks ab — das Herauszoomen war blockiert.

### Zwei CSS-Fallen, beide dieselbe Ursache

Beide haben das Dokument überbreit gemacht und quer scrollen lassen — **auch
auf dem Desktop**, wo es jahrelang unbemerkt blieb:

- **`main > * { min-width: 0 }`** — ohne das wächst ein Grid-Kind auf die
  *Mindestbreite seines Inhalts*. Die 13-spaltige Tabelle blies das Dokument
  auf 1678 px auf, obwohl sie in einem `overflow: auto`-Container steckt.
- **Die Filtergruppe braucht einen Container, der ihre Breite begrenzt.**
  In der alten `.filterbar` hat `.filtergroup` als Flex-*Item* seine
  Cross-Achse nicht auf die Containerbreite begrenzt und ist trotz
  `overflow-x: auto` und `min-width: 0` auf 846 px aufgegangen; die Leiste
  musste dafür `display: block` werden. Heute liegen die Gruppen in
  `.headfilter`, einer **Spalten**-Flexbox: Dort füllt jede Zeile schlicht
  die Breite und scrollt darin — dieselbe Lösung, anderer Weg. `min-width: 0`
  bleibt an beiden Stellen Pflicht.

**Merke:** Bei „die Seite scrollt quer" nicht raten, sondern messen —
Elemente nacheinander auf `display: none` setzen und `scrollWidth`
beobachten. Ein Detektor über Bounding-Rects liefert Fehltreffer, weil er
geclippte Kinder (Leaflet-Kacheln, Off-Canvas-Schubladen) mitzählt.

### Die Regression laufen lassen

```
node lauf.js                 gepflegter Satz, 4 Arbeiter, 2 Geräteprofile   ~2:51 min
node lauf.js --voll          alle Geräteprofile (für Layoutarbeit)
node lauf.js --live          gegen die ausgelieferte Seite statt localhost
node lauf.js sortierung ...  nur diese Tests
```

25 gepflegte Tests, seriell 586 s. **Parallel ist der große Hebel** — und
zwar aus einem messbaren Grund: Ein Seitenaufbau kostet 624 ms (ohne Kacheln
255 ms), die Laufzeit steckt fast vollständig in festen Wartepausen, und die
kosten keine CPU.

| Arbeiter | Gesamt | Faktor |
|---|---|---|
| 1 (seriell) | 586 s | — |
| 3 | 213 s | 2,7 |
| **4** | **171 s** | **3,4** |

Vier Arbeiter auf vier Kernen drängeln sich **nicht**: Die Einzellaufzeiten
sind unverändert (12,8 gegen 12,6 s usw.). Deshalb ist 4 die Voreinstellung.
Der Läufer startet die längsten Tests zuerst, sonst wartet am Ende ein
Arbeiter allein auf den längsten und die anderen stehen still.

**Zwei Geräteprofile statt sieben.** `AIS_GERAETE=schmal` (Voreinstellung)
lässt je ein Telefon hochkant und ein Tablet quer durch — die beiden
Layoutwelten (Kartenliste und breite Tabelle). Für Arbeit **an den
Breitengrenzen selbst** gilt `--voll`: Erst das iPad Mini quer mit genau
1024 px hat den Fehler mit der überschriebenen `padding`-Kurzform gezeigt.

Die fünf längsten: `zeilentreffer` 66 s, `groessecache` 64 s,
`typkategorie` 47 s, `snapshotpan` 46 s, `mvtest` 38 s. Wer weiter drücken
will, holt dort die festen Wartepausen heraus, nicht anderswo.

**Der eine zeitempfindliche Test: `snapshotpan`.** Er ist unter vier
parallelen Browsern einmal durchgefallen und lief allein wie im nächsten
Gesamtlauf sauber durch. Kein Zufallsbefund zum Abhaken: Er prüft die
15-Sekunden-Frist von `SNAPSHOT_TIMEOUT_MS` und mehrere 400-ms-Entprellungen
mit festen Wartepausen — unter Last rutschen die aneinander. Fällt er im
Parallellauf durch, **erst allein wiederholen**; nur wenn er dann auch rot
ist, liegt es am Code.

#### Zwei Fallen beim Einbau, beide vom Läufer aufgedeckt

- **Feste Indizes auf die Geräteliste.** `GERAETE[2][1]` gab es nach dem
  Filtern nicht mehr, `kopfzeile` stürzte ab. Proben, die ein *bestimmtes*
  Profil brauchen (die 1024-px-Grenze!), greifen jetzt über `profil('iPad
  Mini quer')` auf die **volle** Liste zu, nicht auf die gefilterte.
- **Ein zu gieriges Fehlermuster.** Der Läufer wertete jedes
  „fehlgeschlagen" im Text als Fehler — und traf damit die Beschriftung
  einer Probe (`kein "Snapshot fehlgeschlagen" durch den Abbruch`). Nur die
  Schlusszeile zählt.

Dazu ein echter Befund: `richtung` nahm an, „das frisch gemeldete Schiff
steht in der Tabelle oben (nach `updatedAt` sortiert)". Genau diese Annahme
ist mit dem MMSI-Stichentscheid weggefallen. Der Test sucht jetzt nach
`data-mmsi` statt nach Position.

### Lasttests

| Test | Misst | Dauer |
|---|---|---|
| `tabellenlast` | Blockade des Hauptstrangs beim Neuzeichnen, 400 Schiffe | 15 s |
| `markerlast` | Wie schnell zeigt ein Marker neue Maße unter Last? | ~12 s |
| `markerlimit` | Ab welcher Schiffszahl hängt die Karte? Setzt `SNAPSHOT_MAX_AUTO_SQDEG` | ~12 s |
| `diaryquota` | Wann läuft `localStorage` über? | ~9 s |

**`tabellenlast` misst jetzt zwei Fälle getrennt** (200 Zeilen):

| | Median |
|---|---|
| Nachführen (Zeilenmenge bleibt gleich — der Alltag) | **21 ms** |
| Umbauen (halbe Tabelle raus und wieder rein) | **63 ms** |

Der Messpunkt hat **zweimal** nicht zur Umsetzung gepasst, und beide Male
war das Ergebnis wertlos:

1. Erst ein synchroner Zeitnehmer um das `input`-Ereignis — der Sucheingang
   ist 150 ms entprellt, gemessen wurden **0 ms**.
2. Dann das Warten auf `childList`-Mutationen an `#shipTableBody`. Seit die
   Zeilen wiederverwendet werden, gibt es die nicht mehr: Der Test **hing**
   bis zum Abbruch und hat ein Zehn-Minuten-Budget gesprengt.

Jetzt getrennt: **Beacon** — `refreshVisibleShips()` schreibt bei jedem
Durchlauf `shipCountEl.textContent`, und ein `textContent`-Setzen ersetzt
den Textknoten *immer*, auch bei gleichem Wert. Eine `childList`-Mutation an
`#shipCount` heißt also „ein Neuzeichnen ist gelaufen", unabhängig davon, ob
sich in der Tabelle etwas geändert hat. **Dauer** — ein 4-ms-Taktgeber misst
die Lücken im Hauptstrang. Und die Schleife ist **begrenzt**: feste Runden,
feste Pausen, kein Warten auf ein Ereignis, das ausbleiben kann.

Die Zahl ist **nicht** mit dem früheren „163 ms" vergleichbar — der enthielt
die 150 ms Entprellung. Und sie ist nicht die reine Laufzeit von
`renderVisibleShips()`: In derselben Blockade steckt auch `applyMapFilter()`
samt Leaflet.

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
| `aisstream_api_key`, `aisstream_server_url`, `aisstream_mmsi_filter`, `aisstream_auto_enrich`, `aisstream_legend_open`, `aisstream_legend_open_sm`, `aisstream_show_labels`, `aisstream_own_pos`, `aisstream_auto_snapshot`, `aisstream_auto_connect`, `aisstream_auto_refresh`, `aisstream_filters` | Einstellungen & Filterauswahl | unbegrenzt |
| `aisstream_enrich_<mmsi>` | Registerdaten je Schiff, **auch Fehlschläge** | 30 d Treffer / 3 d Miss / **10 min unvollständig** (ohne IMO gelaufen), max. `ENRICH_MAX` = 400 |
| `aisstream_static` | **eine** JSON-Map MMSI → Schiffsstatik | 60 d, max. 2000 |
| `aisstream_ais` | **eine** JSON-Map MMSI → Position, Fahrtdaten, Personen an Bord, Binnenangaben | **30 min**, max. 1500 |
| `aisstream_diary` | Schiff-Spotter-Tagebuch: Sichtungen mit eingefrorenem Schiffszustand | **kein Cache** — läuft nie ab, wird nie gekürzt |

**Alle drei brauchen eine Obergrenze.** Das Kontingent liegt bei ~5 MB und
teilen sich alle Schlüssel; wer einen vierten Speicher ergänzt, gibt ihm ein
Limit *und* ein sinnvolles Verhalten bei `QuotaExceededError` — siehe unten,
das war ein echter Datenverlust.

Dazu ein Speicher **außerhalb** von `localStorage`:

| Speicher | Inhalt | Grenze |
|---|---|---|
| Cache Storage `aisstream-photos-v1` | Schiffsfotos als Blob (Miniatur 480 px) | 120 Bilder, älteste zuerst |
| Cache Storage `aisstream-diary-photos-v1` | Fotos zu Tagebucheinträgen | **ohne Obergrenze**, wird nie beschnitten |

### VesselFinder & Co.: geprüft, geht nicht

Naheliegende Idee: die Detailseite (`vesselfinder.com/vessels/details/<IMO>`)
scrapen und das Foto übernehmen. **Am 24. Aug. 2026 geprüft — aus dem Browser
technisch ausgeschlossen:**

- Die Seite antwortet mit HTTP 200, sendet aber **keinen
  `access-control-allow-origin`-Header**, auch nicht mit gesetztem `Origin`.
  Ein `fetch()` von unserer Seite kann den HTML-Text also nicht lesen. Ohne
  Backend gibt es keinen Weg daran vorbei.
- `robots.txt` liefert selbst **403 Forbidden** — der Betreiber blockt
  automatisierte Zugriffe grundsätzlich.

Dazu kommt: Fremde Bilder direkt einzubinden ist Hotlinking auf deren Kosten,
und die Nutzungsbedingungen solcher Anbieter untersagen automatisiertes
Auslesen üblicherweise. Auch mit Proxy wäre es also keine gute Idee. Das gilt
sinngemäß für MarineTraffic, ShipSpotting und FleetMon.

**Der gangbare Weg für mehr Bilder ist Wikimedia Commons** (siehe direkt
darunter): frei lizenziert, CORS offen, kein Schlüssel.

### Zweite Bildquelle: Commons über die IMO

Wikidatas `P18` gibt es nur zu bekannteren Schiffen. Auf Commons liegen
dagegen tausende Schiffsfotos, deren **Dateiname die IMO enthält**.

`fetchCommonsPhoto(imo)` holt in **einem** Aufruf Treffer, Thumbnail-URL,
Urheber und Lizenz:

```
action=query&generator=search&gsrsearch=IMO <nr>&gsrnamespace=6
&prop=imageinfo&iiprop=url|extmetadata&iiurlwidth=480&origin=*
```

**Die IMO muss im Dateinamen stehen — das ist die entscheidende Regel.** Die
Volltextsuche ist unscharf: Für IMO 9892896 („Bore Wave") lieferte sie
*„Aftermath of Severn Bore wave"*, also einen **Fluss**. In einer Stichprobe
von 15 Schiffen fing die Regel **8 solche Fehltreffer** ab und lieferte
**6 richtige Fotos** — darunter die MAERSK VIRGINIA (IMO 9235531), zu der
Wikidata nichts hat. Lieber kein Bild als das falsche Schiff.

Läuft **nur, wenn Wikidata kein Foto hat** — dessen `P18` ist dem Schiff
eindeutig zugeordnet statt über eine Textsuche gefunden.

**Lizenz:** Commons-Fotos stehen meist unter CC-BY. Urheber und Lizenz kommen
aus `extmetadata` und werden unter dem Bild ausgewiesen, dazu ein Link auf die
Dateiseite. Ohne das wäre die Einbindung nicht lizenzkonform — beim Ergänzen
weiterer Bildquellen bitte genauso halten.

### MAERSK VIRGINIA hatte kein Bild — zwei Löcher in der Kette

Gemeldeter Fall: Zu einem Schiff, von dem Commons **drei** Fotos führt, zeigte
der Client keines. Nachgestellt in `scratchpad/mvtest.js`, Wikidata-Antwort
gestellt, Commons echt.

Wikidata kennt das Schiff (Q52351254, MMSI 477195100, IMO 9235531), hat aber
**kein P18**. Zwei unabhängige Fehler verhinderten trotzdem den Commons-Griff:

1. **Die IMO kam nie aus Wikidata.** Der Bezugswert stand als
   `(dt && dt.imo) || entry.imo` fest, *bevor* das Wikidata-Ergebnis vorlag —
   und ohne IMO wird Commons übersprungen. Digitraffic deckt vor allem die
   Ostsee ab, und die AIS-Statiknachricht mit der IMO kommt nur alle paar
   Minuten: Ob ein Foto erschien, hing damit daran, ob zufällig schon eine
   Msg 5 eingetroffen war. Im Test: ohne Msg 5 kein Bild, mit Msg 5 Bild.
2. **`if (!dt && !wd) return null;`** brach ab, sobald beide Register leer
   waren — Commons braucht aber nur die IMO. Ein Schiff, das in keinem der
   beiden Register steht, blieb ohne Bild, obwohl Commons Fotos davon hat.

Beides behoben: Die IMO wird auch aus `wd.imo` genommen, und der Abbruch fiel
weg (`merged.sources.length ? merged : null` am Ende). Commons ist damit eine
**eigenständige dritte Quelle**, kein Anhängsel der beiden Register.

### Commons: Kategorie zur IMO statt Volltextsuche

Commons pflegt für Schiffe `Category:IMO <nummer>`. Sie enthält meist keine
Dateien direkt, sondern die **Namenskategorie** des Schiffs — daher zwei
Schritte. Das ist kuratiert statt geraten, und der Unterschied ist groß.
Gemessen an 25 Schiffen mit IMO aus Wikidata (`scratchpad/commonsroutes.js`):

| Weg | findet ein Foto |
|---|---|
| Volltextsuche `IMO <nr>` + strenge Titelregel | 6 von 25 |
| Kategorie `IMO <nr>` → Unterkategorie | **24 von 25** |
| davon nur über die Kategorie erreichbar | 18 |

Die Volltextsuche bleibt als Rückfall, **mit** der strengen Titelregel — ohne
sie lieferte sie „Aftermath of Severn Bore wave" als Schiffsfoto, weil im
Beschreibungstext „IMO" und die Zahl vorkamen. Innerhalb der Kategorie ist die
Regel unnötig: Wer dort einsortiert ist, gehört zum Schiff. Genau deshalb
kommen jetzt auch die sechs „Maersk Virginia, Fremantle, 2015"-Aufnahmen in
Frage, die die Titelregel verworfen hätte.

#### Die IMO bleibt am Rumpf, der Name wechselt

Gemeldeter Fall: MMSI 275482000 (BON VIVANT) zeigte ein **Stadtbild**. Die
Kette nachgefahren:

```
MMSI 275482000  --Digitraffic-->  IMO 9052692
IMO 9052692     --Commons-------> Category:IMO 9052692
                                    -> Category:Vestfjord (ship, 1993)
                                       -> File:Vestfjord anchored in Tallinn Bay
                                          Tallinn 6 September 2016.jpg
```

**Die Zuordnung ist richtig.** Die Commons-Kategorie zu IMO 9052692 sagt
`Name of ship: VESTFJORD (since 01/01/2011)`, MMSI 246162000, Baujahr 1993;
Wikidata führt **dieselbe IMO** als *Bon Vivant*. Es ist derselbe Rumpf: **Die
IMO-Nummer bleibt lebenslang am Schiff, Name, Flagge und MMSI wechseln mit dem
Eigner.** Aus VESTFJORD (Niederlande) wurde BON VIVANT (Lettland).

**Daraus folgt eine Regel für künftige Umbauten:** Ein Commons-Foto darf zu
Recht einen *anderen* Schiffsnamen tragen als der AIS-Feed. Wer eine
Namensprüfung auf den **Kategorieweg** legen will — so wie sie der MMSI-Weg
braucht —, wirft damit die richtigen Bilder aller umbenannten Schiffe weg. Der
Kategorieweg bleibt deshalb bewusst ohne Titelregel: Wer dort einsortiert ist,
gehört zum Rumpf.

**Das Bild taugt trotzdem nicht.** Es zeigt zu etwa neun Zehnteln die Altstadt
von Tallinn — rote Dächer, Kirchtürme, ein Baukran; das Schiff liegt als
wenige Pixel auf dem Horizontstreifen. Jemand hat auf Commons ein
Stadtpanorama in eine Schiffskategorie einsortiert. Die Meldung war also in
der Sache berechtigt, nur lag der Fehler nicht im Client.

**Und dagegen hilft kein Filter** — geprüft, was an Metadaten zur Verfügung
steht:

- Der **Titel enthält den Schiffsnamen** („Vestfjord anchored in Tallinn Bay").
  Eine „Name muss im Titel stehen"-Regel hätte das Bild **durchgelassen**.
- Die **Kategorie enthält genau eine Datei**. Eine Sortierregel läuft ins Leere.
- **Strukturierte Daten** führen weder Motiv noch Bildinhalt (`haswbstatement`
  liefert für diese Kennungen ohnehin null Treffer, siehe oben).

Ob ein Schiff auf einem Bild formatfüllend oder als Pixelfleck zu sehen ist,
steht in **keinem abfragbaren Feld**. Ohne die Pixel anzusehen ist dieser Fall
nicht entscheidbar, und Bildanalyse im Browser wäre für diese App
unverhältnismäßig. Das ist die **Grenze des Verfahrens** — kein Fehler, den man
wegprogrammiert.

Grenzt sich ab von der Regel „lieber kein Bild als das falsche Schiff": Hier
ist es **das richtige Schiff auf einem untauglichen Bild**, ein anderer Fall.

Der naheliegende Ausweg wäre eine **Bildunterschrift** — Dateititel unter das
Foto, plus bei abweichendem Kategorienamen eine Zeile „Aufnahme unter dem Namen
*Vestfjord*". `commonsCredit()` hat den Titel bereits in der Hand
(`page.title`) und benutzt ihn heute nur für den Link. Damit kann der Leser
selbst einordnen, was er sieht. Noch nicht umgesetzt.

**Zwei Merksätze aus diesem Fall:**

- **Erst die Kette nachfahren, dann den zuletzt geänderten Weg beschuldigen.**
  Der Verdacht fiel auf den neuen MMSI-Weg — Commons hat zu `"275482000"` aber
  **null** Treffer, `commonsByMmsi()` war gar nicht beteiligt. Schuld war der
  alte IMO-Kategorieweg.
- **Ein Dateititel ist keine Bildbeschreibung.** Aus „anchored in Tallinn Bay"
  hatte ich auf eine Hafenszene mit Schiff im Vordergrund geschlossen und das
  dem Nutzer so gesagt; das Bild ist ein Stadtpanorama. Dieselbe Lehre wie beim
  [Richtungspfeil](#fahrtrichtung-kleiner-pfeil-im-punkt) — **ansehen bzw.
  messen, nicht ableiten.**

**Auswahl stabil halten:** Aus der Kategorie kommen bis zu 20 Dateien. Sortiert
wird IMO-im-Namen zuerst, sonst alphabetisch — sonst zeigt dasselbe Schiff bei
jedem Abruf ein anderes Bild.

**Falle:** `fetchCommonsPhoto()` setzt bis zu drei Abrufe hintereinander ab,
läuft aber selbst schon in einem `throttled("commons")`-Platz. Ein `throttled()`
darin würde auf den eigenen Platz warten und die Warteschlange **dauerhaft**
blockieren — kein Timeout greift, weil die Anfrage nie startet. Der Abstand
zwischen den Teilabrufen wird deshalb von Hand eingelegt (`afterGap()`).

### Registerdaten gelten ab dem Anlegen des Schiffs, nicht ab dem Anklicken

Gemeldet: *„Nach dem Reload wird die ‚AS Claudia' mit unbekannter Größe in
der Karte visualisiert. Sobald man sie selektiert, wird es sofort auf die
richtige Größe aktualisiert."*

**„Sofort" ist die Diagnose.** Kein Nachladen dauert null Millisekunden —
das war ein Cache-Treffer. Wenn ein Klick etwas augenblicklich repariert,
liegt die Antwort schon lokal und wird nur nicht gelesen.

`shipLengthMeters()` kennt drei Quellen. Zwei überleben den Reload, eine
nicht:

| Länge kommt aus | Landet in | Marker nach Reload |
|---|---|---|
| AIS Msg 5 `Dimension` | `entry.dim` → Statik-Cache | 15 px, richtig |
| Digitraffic `referencePointA..D` | `entry.dim` → Statik-Cache | 15 px, richtig |
| **nur Wikidata `loa` (P2043)** | **`entry.enrich`, sonst nirgends** | **11 px, hohler Ring** |

`entry.enrich` steht ausschließlich in `aisstream_enrich_<mmsi>`, und diesen
Cache las bis dahin nur `enrichShip()` — also erst beim Öffnen.

**Jetzt** wendet `getShip()` den Registereintrag direkt nach
`applyStaticCache()` an (`applyEnrichCache()`). Das muss **vor**
`setPosition()` geschehen, denn dort entsteht der Marker mit `shipIcon()`:
So ist das Symbol beim ersten Bau richtig, statt neu gebaut zu werden.
Erwünschter Nebeneffekt: Auch ein Schiff, das live oder per Snapshot neu
auftaucht und früher schon nachgeschlagen wurde, hat seine Registerdaten
sofort.

#### Ein Verzeichnis statt eines Speicherzugriffs je Schiff

`localStorage` je Schiff zu befragen wäre zu teuer — ein Snapshot legt bis zu
12 900 Schiffe an. `enrichIndex` ist eine `Set` der MMSIs, für die überhaupt
ein Eintrag existiert; sie fällt in `pruneEnrichCache()` als Nebenprodukt ab,
das beim Start ohnehin über genau diese Schlüssel läuft.

Gemessen (400 Einträge à 510 Byte, ~200 kB — `ENRICH_MAX` deckelt bei 400):

| | |
|---|---|
| Verzeichnis aufbauen | 1,0 ms |
| alle 400 Einträge lesen und parsen | 1,9 ms |
| 12 900 × `Set.has()` | 4,6 ms |
| **Aufschlag auf die Startdauer, ganze Seite** | **23 ms** (2408 gegen 2385) |

`enrichIndex` ist absichtlich weit oben deklariert, obwohl es fachlich zu den
Enrich-Konstanten gehört: `getShip()` steht in der Datei darüber, und die
Ladereihenfolge-Falle (Deklarationen werden hochgezogen, `var`-**Werte**
nicht) hat hier schon zweimal zugeschlagen. `null` heißt „noch nicht
aufgebaut" — dann wird nichts angewandt und nichts geht kaputt.
`enrichCacheSet()` trägt ein, `clearAllCaches()` leert, und ein von
`enrichCacheGet()` als abgelaufen verworfener Eintrag fliegt auch aus dem
Verzeichnis.

Die Regeln bleiben, wo sie waren: `enrichCacheGet()` prüft `ENRICH_VERSION`
und TTL und räumt selbst auf. Es gibt keinen zweiten Regelsatz für den
Ladeweg.

#### Die Logzeile beim Laden ist die Diagnose

```
679 Schiff(e) aus dem 30-Minuten-Zwischenspeicher wiederhergestellt ·
12 Registereinträge angewandt (Verzeichnis: 143).
```

Bewusst **„angewandt"** und nicht „mit Registerdaten": Gezählt wird jeder
angewandte Eintrag, auch ein Leertreffer — „nichts gefunden" wird ebenfalls
zwischengespeichert. Genau das ist die Diagnosefrage („hat der Weg
gegriffen?"), und die Verzeichnisgröße daneben sagt, wie viel es überhaupt
anzuwenden gab. Meldet ein Nutzer wieder eine fehlende Größe, steht die
Antwort damit im Log statt in einer Vermutung.

Beim Schreiben des Tests dazu war meine Erwartung falsch, nicht der Zähler:
Ich hatte einen Treffer erwartet (nur das Wikidata-Schiff braucht den Weg),
gemeldet wurden drei. Richtig so — auch die beiden anderen haben nach dem
Anklicken einen Eintrag, bei einem davon einen Leertreffer.

#### Die Tabelle sagte „unbekannt", während die Karte „L" zeigte

Zweiter Befund derselben Ursache: `applyEnrichment()` leitete `entry.size`
nur aus einem brauchbaren `dim` ab. Wikidata liefert Länge über alles und
Breite (P2043/P2261), aber keine Antennenabstände — daraus lässt sich kein
`dim` bilden, also blieb die Spalte leer, während der Marker längst die
richtige Größe hatte. `entry.size` wird jetzt auch aus `loa`/`beam` gebildet.
Ein Präzedenzfall war da: Digitraffic-Maße füllen die Spalte schon lange.

Interessant an der Gegenprobe: Mit stillgelegtem Verzeichnis ist die
**Tabelle** nach dem Reload trotzdem richtig — `size` steht in
`STATIC_FIELDS` und überlebt. Nur der **Marker** fällt zurück. Wer also nur
die Spalte repariert hätte, hätte den gemeldeten Fehler nicht angefasst.

#### Was bewusst nicht passiert

Schiffe, die **nie** angeklickt wurden, haben keinen Registereintrag und
bleiben ohne Größe. Sie beim Laden alle nachzuschlagen hieße, Wikidata und
Digitraffic mit hunderten Abfragen zu belegen — Wikimedia hat auf zu dichte
Abfragen schon mit 429 geantwortet. Der Weg holt heraus, was lokal liegt; er
fragt nichts Neues. `groessecache.js` prüft das: **null** Netzanfragen beim
Laden.

#### Der Registername darf den Livenamen nicht überholen

`applyEnrichment()` füllt `name` nur, wenn das Feld leer ist, und alle
Live-Pfade schreiben `entry.name` bedingungslos. Das muss so bleiben — sonst
zeigt ein umbenanntes Schiff wieder seinen alten Registernamen (Fall
BON VIVANT, siehe unten). Der Test hält das fest.

### Der Cache sperrte Commons aus, wenn die IMO zu spät kam

Der größte Hebel für mehr Bilder war **kein neuer Dienst**, sondern ein Fehler
im Zusammenspiel von Kette und Cache. Nachgestellt und bestätigt (Playwright,
Digitraffic antwortet ohne IMO, Wikidata leer, Commons-Anfragen mitgezählt):

```
1) Erste Anreicherung OHNE IMO  -> Commons-Anfragen: 0
   Cache-Eintrag: {"found":true,"quellen":["Digitraffic"]}   -> 30 Tage
2) IMO trifft per Msg 5 ein     -> im Datensatz sichtbar: true
3) Wiederöffnen MIT IMO         -> zusätzliche Commons-Anfragen: 0
```

Drei Stellen wirkten zusammen:

- `if (merged.image || !imo) return …` — ohne IMO wurde Commons übersprungen.
- `enrichCacheSet(mmsi, data, !!data)` — das Ergebnis wurde trotzdem gecacht.
  Hatte Digitraffic geantwortet (Name, Rufzeichen, nur eben keine IMO), galt es
  als **Treffer** und lag **30 Tage** fest; sonst 3 Tage.
- `enrichCacheGet()` lieferte beim nächsten Öffnen den Eintrag, und
  `enrichShip()` stieg aus, bevor Commons je gefragt wurde.

**Das trifft die Mehrheit der Schiffe.** Die IMO kommt aus `ShipStaticData`
(Msg 5), und Class A sendet die nur alle sechs Minuten — nach 5 Minuten
Livestream hatten [gemessen 19 %](#warum-so-viele-schiffe-unbekannt-sind-gemessen)
der Schiffe eine Statiknachricht. Wer ein frisch aufgetauchtes Schiff anklickt,
der Normalfall, sperrte es damit für 3 bis 30 Tage vom Bilderdienst aus.

**Die Lösung hat drei Teile:**

1. **Unvollständig statt fertig.** Lief der Durchlauf ohne IMO und fand kein
   Bild, trägt der Cache-Eintrag `teil: true` und hält nur
   `ENRICH_TTL_TEIL` = **10 Minuten** — er wartet ohnehin bloß darauf, dass die
   IMO nachkommt.
2. **Nicht auf das nächste Öffnen warten.** `imoNachzug(entry)` hängt im
   Msg-5-Zweig: Liegt die IMO jetzt vor und ist ein `teil`-Eintrag da, läuft die
   Anreicherung erneut. Das Foto erscheint in der offenen Detailansicht **ohne**
   erneutes Anklicken.
3. **`ENRICH_VERSION` auf 3.** Ohne die Erhöhung wirkte der Fix bei bestehenden
   Nutzern bis zu 30 Tage lang nicht — genau bei den Einträgen, die ihn brauchen.

**Msg 5 wiederholt sich alle sechs Minuten**, deshalb merkt `imoNachgezogen`
je MMSI, dass der Nachzug gelaufen ist. Ohne diese Liste fragte der Client
Commons im Sechsminutentakt für jedes Schiff auf der Karte. Die Liste wird bei
„Zwischenspeicher leeren" zurückgesetzt, sonst bliebe der Nachzug für den Rest
der Sitzung gesperrt.

Gegenprobe gemacht: Mit auskommentiertem `imoNachzug()` meldet die Probe
`0 neue Commons-Anfragen` und fünf `FEHL`. Sie prüft also wirklich etwas
(`scratchpad/imonachzug.js`, 13 Prüfungen).

### Zweiter Identifikator: die MMSI, bestätigt durch den Namen

**Die IMO ist der Flaschenhals, nicht Commons.** An 24 zufälligen Schiffen aus
dem Feed war für **7 (29 %)** überhaupt eine IMO beschaffbar; wo eine vorlag,
fand die Kategorie bei 4 von 7 ein Foto.

*(Korrektur zu den „24 von 25" weiter oben: Diese Stichprobe bestand aus
Schiffen, deren IMO **aus Wikidata** kam — also bekannten Schiffen. An
zufälligen Schiffen aus dem Feed sind es 57 %.)*

Ein Lotsenboot, ein Arbeitsschiff oder eine Yacht hat **gar keine IMO** — zu
klein für SOLAS. Für diese ganze Klasse war der Client grundsätzlich blind.

`commonsByMmsi(mmsi, name)` schließt die Lücke, aber **nur mit
Bestätigungsregel**: Die Datei muss die MMSI im Text führen **und** ein
unterscheidungskräftiges Wort des Schiffsnamens im **Titel** tragen. Die Kennung
liefert die Identität, der Name bestätigt sie.

**Warum die MMSI allein nicht reicht** — die drei Rohtreffer einer Stichprobe:

| MMSI | Schiff | Rohtreffer | mit Regel |
|---|---|---|---|
| 211324470 | PILOTVESSEL HANSE | `Pilot vessel Hanse (1).jpg` | ✅ bleibt |
| 304734000 | HAV MARLIN | `US Marines … Operation Jackstay` | ❌ raus |
| 355948000 | VEGA LEADER | `Bremerhaven, Überseehafen…` | ❌ raus |

Eine neunstellige Zahl kommt auch zufällig in Beschreibungstexten vor — das
Vietnamkriegsfoto ist der Beleg.

Drei Feinheiten, jede aus einem Fehlschlag:

- **Wortgrenzen, keine Teilzeichenkette.** „Marines" darf „Marlin" nicht
  erfüllen. Genau daran hängt die Abweisung.
- **Umlaute falten, beide Seiten.** AIS sendet ASCII, Commons schreibt richtig:
  STORSKAR fiel gegen `Storskär` durch, obwohl es dasselbe Schiff ist. `faltUm()`
  normalisiert ä/æ/å → a, ö/ø → o, ü → u, ß → ss plus NFD-Zerlegung.
- **Ein reiner Titeltest reicht nicht.** Er allein hätte auch den *richtigen*
  Treffer verworfen — im Titel `Pilot vessel Hanse (1).jpg` steht keine MMSI.
  Erst MMSI-im-Text **plus** Name-im-Titel trägt.

`NAME_STOPP` wirft Namensbestandteile ohne Trennkraft weg (`pilot`, `vessel`,
`ship`, `tug`, …), dazu fallen Wörter unter vier Zeichen, reine Zahlen und
römische Ziffern raus — sonst passt jedes Lotsenboot auf jedes andere.

**Trefferquote ehrlich:** An 44 Schiffen aus vier Revieren fand der MMSI-Weg
**2**, beide richtig, bei **0** Fehlzuordnungen — und die Rohtreffer-Obergrenze
lag ebenfalls bei 2, die Regel wirft also nichts Richtiges weg. Das sind ~5 %,
gegenüber ~11 % für den IMO-Weg. Klein, aber es sind Schiffe, die sonst
**grundsätzlich** kein Bild bekommen. An den fünf Fällen mit bekannter Wahrheit
(oben plus STORSKAR und HAGLAND PROGRESS) verhält sich die Regel exakt wie
entworfen.

**Reihenfolge in `fetchCommonsPhoto(imo, mmsi, name)`:** IMO-Kategorie →
IMO-Volltext → MMSI + Name. Ohne IMO fängt die Kette direkt bei der MMSI an.
Die kuratierte Kategorie bleibt zuerst, weil sie ohne Ratequote arbeitet.

### Was für Bilder NICHT funktioniert (gemessen, nicht vermutet)

Damit die Frage nicht ein zweites Mal untersucht wird:

- **Google Custom Search.** CORS ist offen, die API wäre aus dem Browser
  aufrufbar. Aber: Der API-Schlüssel stünde im Quelltext (kein Backend), die
  Nutzungsbedingungen beschränken das Zwischenspeichern von Ergebnissen, und
  über die IMO findet Google nur, was Commons auch führt.
- **Die MMSI als Suchbegriff** ist der falsche Schlüssel: `IMO 9321483` liefert
  auf Commons 70 Treffer, `MMSI 220417000` vier. ~12 137 Dateien tragen eine
  IMO im Text, ~790 eine MMSI. Als **Kennung** trägt die MMSI trotzdem (siehe
  oben) — nur nicht als Suchwort ins Blaue.
- **Suche über den Schiffsnamen.** Sieht mit 42 % doppelt so gut aus wie der
  IMO-Weg, ist aber Raten: NORDSEE → ein Strand, RUBECULA → ein Vogel, TINTIN →
  ein Fluss, LADY → „Lady Elizabeth (ship, 1879)", SOFIA → „St Sofia (funnel)".
  Schiffsnamen sind Alltagswörter. Der Name taugt nur als **Bestätigung** einer
  Kennung, nie als Sucheinstieg.
- **Openverse** (CC-Suche über Flickr, Museen, Commons): Bei IMO-Suchen liefert
  es ausschließlich `wikimedia` — dieselbe Quelle, die der Client schon abfragt.
- **`Category:MMSI <nr>`** gibt es (zwei Schreibweisen, `MMSI 201100098` und
  `MMSI: 226008110`), ist aber zu dünn besetzt: 0 Treffer in 18 Schiffen.
- **Strukturierte Daten** (`haswbstatement:P587=<mmsi>` / `P458=<imo>`) wären
  von Bauart her präzise, liefern aber **0 Treffer** — auch für die Emma Mærsk.
  Commons pflegt diese Kennungen nicht als Aussagen.
- **Schwesterschiff über die Wikidata-Schiffsklasse (P289):** Von fünf Schiffen
  mit IMO hatte eines überhaupt eine Klasse hinterlegt — und das hatte bereits
  ein eigenes Foto.

**Die naheliegende Erweiterung, die noch offen ist:** eigene Fotos aufnehmen
(`<input type="file" accept="image/*" capture="environment">`) und im
vorhandenen Tagebuch-Bildspeicher ablegen. Einzige Quelle mit 100 % Treffer und
null Fehlzuordnungen. Der Export müsste sie dann als Data-URL mittragen — bisher
enthält er nur URLs, weil Registerfotos jederzeit nachladbar sind.

### Cache-Version: alte Fehltreffer festhalten wäre schlimmer als kein Cache

Der Anreicherungs-Cache hält Fehlschläge 3 Tage, Treffer 30. Ändert sich die
Abfragekette, sind die alten Einträge mit weniger Quellen entstanden und würden
das schlechtere Ergebnis genau so lange konservieren — der Nutzer sieht den Fix
nicht. Jeder Eintrag trägt deshalb `v: ENRICH_VERSION`; passt sie nicht, wird
er beim Lesen verworfen und neu abgefragt. **Beim Ändern der Kette hochzählen.**

### Hängende Abfrage blockierte die ganze Warteschlange

Beim Testen der Commons-Anbindung fiel auf: Alle Registerabfragen laufen
serialisiert durch `throttled()`. Eine Anfrage, die **nie** antwortet,
blockiert damit jede weitere — im Test kam das zweite Schiff gar nicht mehr
an die Reihe, der Zustand blieb auf „wird abgefragt".

Jeder Abruf hat deshalb jetzt eine harte Frist (`fetchWithTimeout`,
12 s für Register, 15 s für Bilder, per `AbortController`). Wer eine weitere
Quelle einhängt: dieselbe Frist benutzen, nicht das nackte `fetch`.

### Registerabfragen parallelisieren: eine Schlange **je Host**

Die eine globale Warteschlange kostete mehr, als sie schützte: Sie hielt auch
Anfragen an *verschiedene* Hosts auseinander. Gemessen mit künstlich 250 ms
Serverlatenz je Anfrage (`scratchpad/timing.js`, Playwright, jeder Host-Treffer
mit Zeitstempel):

| | vorher (eine Schlange) | nachher (je Host) |
|---|---|---|
| ein Schiff, ganze Kette | 1716 ms | 1113 ms |
| drei Schiffe, 12 Anfragen | 5806 ms | 3470 ms |

**Was parallel darf und was nicht.** Pro Schiff sind `fetchDigitraffic(mmsi)`
und die Wikidata-Abfrage **über die MMSI** voneinander unabhängig — sie starten
zusammen über `Promise.all`. Wirklich abhängig sind nur die beiden Nachschläge
über die **IMO**: der Wikidata-Fallback (`wikidataByImo`) und das Commons-Foto,
denn die IMO liefert erst Digitraffic. Deshalb ist `fetchWikidata(mmsi, imo)` in
`wikidataByMmsi()` und `wikidataByImo()` aufgeteilt.

**Die Höflichkeit bleibt gleich.** Wikidata und Wikimedia haben dieser Sandbox
schon 429/403 zurückgegeben — pro Host ändert sich nichts: weiterhin eine
Anfrage zur Zeit, weiterhin 350 ms Abstand. In der Messung liegen die
Wikidata-Treffer 605–611 ms auseinander (250 ms Latenz + 350 ms Pause), also
nie überlappend. Wer eine Quelle ergänzt: **eigenen Host-Schlüssel** vergeben
(`throttled("commons", …)`), nicht einen fremden mitbenutzen — sonst bremsen
sich zwei Dienste wieder gegenseitig aus.

Die Frist von oben bleibt trotzdem nötig: Eine hängende Anfrage blockiert jetzt
nur noch ihren eigenen Host, aber den vollständig.

### Bildzwischenspeicher (Cache Storage, nicht localStorage)

Die Bild-URL lag längst im Anreicherungs-Cache, die **Bilddaten** aber nicht —
die holte der Browser bei jedem Laden neu.

**Warum Cache Storage und nicht `localStorage`:** Dort müssten Bilder als
Data-URL liegen, also Base64 mit ~33 % Aufschlag, in einem Kontingent von
insgesamt ~5 MB, das sich Statik- und AIS-Cache schon teilen. Cache Storage
legt Blobs nativ ab, hat ein eigenes, weit größeres Kontingent und braucht
**keinen Service Worker** — `caches.open()` funktioniert direkt im
Fensterkontext.

**`?width=480` ist Pflicht, nicht Kosmetik.** Ohne den Parameter liefert
Commons das Original (bei der Emma Mærsk mehrere MB), und die Umleitung
landete im Test sogar auf einer HTML-Seite statt auf dem Bild — ein
plausibler Miterklärer für „Bilder erscheinen nicht zuverlässig". Mit
`width=480` kommen rund 50 KB als `image/jpeg` mit
`access-control-allow-origin: *`, direkt und ohne Umleitungskette.

Ablauf in `cachedPhoto(url)`: Treffer → Blob → Object-URL; sonst einmal
`fetch`, ablegen, anzeigen. Schlägt beides fehl oder fehlt Cache Storage,
wird die nackte URL zurückgegeben — lieber ein Bild aus dem Netz als keins.

- Cap `PHOTO_MAX` = 120; `cache.keys()` liefert Einfügereihenfolge, die
  ältesten fliegen zuerst
- **Object-URLs müssen widerrufen werden**, sonst bleibt der Blob im
  Speicher. `releaseDetailPhoto()` macht das beim Schiffswechsel, beim
  Leeren und vor jedem Neuladen
- Das asynchrone Ergebnis prüft vor dem Einsetzen, ob noch dasselbe Schiff
  offen ist — sonst klebte das Bild am nächsten
- „Zwischenspeicher leeren" löscht auch diesen Speicher

Gemessen: erster Besuch ein Netzabruf, nach einem Reload **null**.

### AIS-Cache (`aisstream_ais`, 30 Minuten)

Alles, was schnell veraltet: `lat`, `lon`, `speed`, `course`, `heading`,
`navStatus`, `rot`, `accuracy`, `raim`, `maneuver`, `fixType`,
`msgTimestamp`, `destination`, `eta`, `draught`, `time` — je MMSI mit dem
Empfangszeitpunkt. Dazu `persons` (Besatzung/Passagiere aus DAC 200 FI 55),
`personsAt` und `inland` (ENI, ERI-Typ, blaue Kegel, beladen/unbeladen):
Die lagen vorher **nirgends** — der Statik-Cache hält nur die dauerhaften
Binnenfelder, die Zahl der Personen an Bord und die Ladungsangaben gingen
bei jedem Reload verloren.

Der frühere Einwand („ein drei Wochen altes Ziel als aktuell anzuzeigen wäre
irreführend") gilt hier nicht mehr, weil **die Tabelle das Alter in Sekunden
direkt hinter dem Namen führt**. Damit ist jederzeit sichtbar, wie frisch ein
Wert ist, und 30 Minuten sind eine brauchbare Kontextspanne.

- Beim Laden wiederhergestellt, aber **nur, wenn der Livestream nichts
  Frischeres hat** (`entry.updatedAt >= rec.at` → überspringen)
- `entry.updatedAt` bleibt der **Empfangs**zeitpunkt, nicht der Ladezeitpunkt —
  sonst zeigte die Altersspalte nach jedem Reload 0 s. `setPosition()` stempelt
  die Gegenwart in den Track, das wird danach zurückgesetzt
- `pruneStaleShips()` räumt alle 60 s auch zur Laufzeit auf: Was länger als
  30 Minuten keine Meldung hatte, verschwindet aus Tabelle und Karte. Das
  schließt zugleich den alten offenen Punkt „Ship-Map wächst unbegrenzt"
- Cap 1500 Einträge, älteste zuerst; Schreiben um 5 s debounced plus
  `pagehide`/`visibilitychange`

**`loadAisCache()` steht bewusst ganz unten im Verdrahtungsblock**, nicht bei
den anderen Cache-Aufrufen: Es legt über `setPosition()` Marker an und braucht
damit alles, was weiter oben per `var` initialisiert wird — `TYPE_COLORS`, die
Filter und so weiter. Dieselbe Ladereihenfolge-Falle wie zweimal zuvor.

### Altersspalte

Direkt hinter dem Namen, rechtsbündig, in Sekunden. Farbschwellen: ab 120 s
normal statt gedämpft, ab 600 s orange, ab 1800 s rot.

`tickAges()` läuft im Sekundentakt und schreibt **nur die Zellen** neu, kein
Tabellenneuaufbau. Ohne Ticker fror die Zahl genau bei den Schiffen ein, bei
denen sie interessant ist — denen, von denen nichts mehr hereinkommt.

Auf dem Telefon sitzt das Alter oben rechts in der Karte, links daneben die
Flagge (`.c-age` und `.c-flag` sind dort absolut positioniert).

**Achtung bei Testskripten:** Die Spalte sitzt an Position 4 und hat alle
folgenden verschoben. Zellen deshalb über ihre Klasse ansprechen
(`td.c-sog`, `td.c-cog`, …), nicht über `nth-child`.

### Schiffsstatik-Cache (`aisstream_static`)

Zweck: Nach einem Neuladen sind Schiffe sofort benannt, statt bis zur
nächsten `ShipStaticData`-Nachricht namenlos in der Tabelle zu stehen — die
kommt bei Class A nur alle sechs Minuten, bei Class B noch seltener.

**Gespeichert wird nur, was wirklich statisch ist:** `name`, `callSign`,
`imo`, `typeCode`, `typeLabel`, `typeDetail`, `size`, `dim`, `aisClass`,
`vendor`, `serial`, `station` und die statischen Inland-Felder (`eni`,
`eriCode`, `eriLabel`, `eriLength`, `eriBeam`).

**Hier bewusst nicht gespeichert:** Positionen, Track, `destination`, `eta`,
`draught`, `loaded`, `blueCones` — die liegen im **AIS-Cache mit 30 Minuten
TTL** (siehe oben). Die Trennung ist der Punkt: Identität und Rumpf ändern
sich über Jahre nicht, Reisedaten in Minuten. Wer hier Felder ergänzt: Diese
Grenze bitte halten.

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

### Voller Speicher löschte den kompletten AIS-Cache (behoben)

Symptom aus dem Betrieb: **nach einem Reload waren alle AIS-Daten weg** —
keine Schiffe, keine Marker, keine Tabellenzeilen, und **keine einzige
Meldung im Log**.

Ursache waren zwei Fehler, die zusammenspielten:

1. **Der Anreicherungs-Cache hatte keine Obergrenze.** Er legt einen
   Schlüssel je nachgeschlagener MMSI an, und abgelaufene Einträge
   verschwanden nur, wenn *genau diese* MMSI noch einmal geöffnet wurde. Wer
   viele verschiedene Schiffe ansieht, füllt damit über Wochen das ganze
   ~5-MB-Kontingent mit Einträgen, die nie wieder jemand liest.
2. **Der AIS-Cache löschte sich bei Platzmangel selbst.** Im `catch` des
   Speichervorgangs stand ein blankes `localStorage.removeItem(AIS_CACHE_KEY)`.
   Der AIS-Cache wird am häufigsten geschrieben (alle 5 s, debounced), also
   traf ihn die volle Quota zuerst — und er warf jedes Mal *alles* weg.

Nachgestellt mit Playwright (`scratchpad/aiscache.js`): localStorage bis zur
Quota mit gültigen Anreicherungseinträgen gefüllt, 400 Schiffe gestreamt,
neu geladen.

| | vorher | nachher |
|---|---|---|
| Einträge im AIS-Cache | 0 | 400 |
| Tabellenzeilen nach Reload | 0 | 68 (= im Kartenausschnitt) |
| Marker nach Reload | 0 | 104 |
| Hinweis im Log | keiner | Aufräummeldung |

**Was jetzt passiert**, in dieser Reihenfolge:

- `pruneEnrichCache(false)` läuft **beim Start**: erst alles Abgelaufene raus,
  dann auf `ENRICH_MAX` = 400 deckeln, ältester Eintrag zuerst. Wichtig beim
  Nachbauen: erst alle Schlüssel einsammeln, *dann* löschen — ein
  `removeItem` mitten in der `localStorage.key(i)`-Schleife verschiebt die
  Indizes und überspringt Einträge.
- `saveAisCache()` schrumpft statt zu löschen: bei `QuotaExceededError` erst
  `pruneEnrichCache(true)` (deckelt auf die Hälfte), dann notfalls die
  Zeilenzahl halbieren, bis es passt — die **neuesten** Schiffe bleiben.
- Reicht selbst das nicht (Quota von fremden Schlüsseln belegt), bleibt der
  **alte Stand stehen**, es gibt aber eine Logmeldung. Ein veralteter Cache
  ist besser als keiner: Beim Laden fällt ohnehin alles über 30 Minuten
  heraus, und die Alterspalte weist den Rest aus.
- `enrichCacheSet()` räumt bei Quota auf und versucht **einmal** erneut.
- `updateCacheCounts()` läuft jetzt nach jedem AIS-Speichervorgang. Vorher
  zeigte der Zähler im Einstellungsbereich `0`, während 400 Einträge im
  Speicher lagen — er wurde nur vom Statik-Cache aktualisiert.

**Regel für neue Speicher:** Ein Cache, der bei Platzmangel den eigenen
Bestand wegwirft, ist schlimmer als gar kein Cache — er verliert Daten
lautlos und genau dann, wenn am meisten drinsteht.

**Zwei Nachzügler aus genau diesem Umbau**, beide im Test aufgefallen:

- **Null Zeilen ist kein Platzmangel.** Bei `rows.length === 0` lief die
  Schrumpfschleife gar nicht erst an, und der Code fiel direkt auf die
  Meldung „Kein Platz im lokalen Speicher" durch. Ausgelöst hat das ein
  völlig normaler Fall: ein Snapshot, dessen Positionen alle älter als
  30 Minuten sind — dann bleibt nach dem TTL-Filter keine Zeile übrig.
  Jetzt wird in dem Fall eine leere Map geschrieben und zurückgekehrt.
- **Nur `setItem` gehört in den `try`.** Standen `log()` und
  `updateCacheCounts()` mit darin, hätte ein beliebiger Fehler daraus — ein
  fehlendes Zählerelement genügt — den Speichervorgang in ein vermeintliches
  Platzproblem verwandelt, samt Halbieren der Zeilen und falscher Meldung.

„Zwischenspeicher leeren" räumt **nur den Speicher**, nicht die laufende
Sitzung. Die Namen der Schiffe, die man gerade ansieht, verschwinden zu
lassen wäre ein überraschender Nebeneffekt; alles im Speicher wurde
legitim empfangen. Der Log sagt das auch so.

## Schiff-Spotter-Tagebuch

Die erste Datenmenge in dieser App, die **der Nutzer erzeugt**. Daraus folgt die
einzige Regel, die hier wirklich zählt:

> **Das Tagebuch ist kein Cache.** Es läuft nicht ab, „Zwischenspeicher leeren"
> fasst es nicht an, und bei Platzmangel wird es nicht gekürzt, sondern gemeldet.

Bei einem Cache ist „im Zweifel wegwerfen" der richtige Reflex — genau der hat
[weiter oben](#voller-speicher-löschte-den-kompletten-ais-cache-behoben) einmal
alle AIS-Daten gelöscht. Hier wäre derselbe Reflex Datenverlust ohne Rückweg.
Konkret: `saveDiary()` versucht `setItem`, räumt bei `QuotaExceededError` den
Anreicherungs-Cache auf, versucht **einmal** erneut — und lässt danach den
gespeicherten Bestand unverändert stehen, mit Meldung im Log. Auch eine
unbekannte `v` im Datensatz führt **nicht** zum Verwerfen.

### Eingefrorener Zustand, aber nicht eingefrorene Texte

Ein Eintrag muss für sich allein stehen: `pruneStaleShips()` entfernt das Schiff
nach 30 Minuten aus `ships`, der AIS-Cache verfällt genauso. Gespeichert wird
deshalb ein Abzug des Schiffszustands — aber **entry-kompatibel geformt**, mit
denselben Feldnamen wie im Live-Datensatz. Dadurch laufen die vorhandenen
Ableitungen unverändert auf dem gespeicherten Objekt:

| Anzeige | wiederverwendet |
|---|---|
| Flagge, MMSI-Kategorie | `decodeMmsi()` |
| Farbpunkt | `typeCategory()` + `TYPE_COLOR_MAP` |
| Größenklasse, Verdrängung, **TEU** | `capacityEstimate()` |
| Entfernung, Peilung | `formatKm()`, `compass()` |

Fertig formatierte Strings einzufrieren wäre der naheliegende, aber schlechtere
Weg: Eine spätere Verbesserung der TEU-Schätzung wirkt so auch auf alte
Einträge. Wer Felder ergänzt, erweitert `DIARY_SHIP_FIELDS` — nicht die
Anzeigefunktionen.

### Fotos: eigener Topf

`prunePhotoCache()` wirft im normalen Bildspeicher alles über `PHOTO_MAX = 120`
weg. Ein Tagebuchfoto gehört zu einem Eintrag, den der Nutzer behalten will, und
liegt deshalb in `aisstream-diary-photos-v1` **ohne** Obergrenze. Beim Löschen
einer Sichtung wird das Bild nur entfernt, wenn **kein anderer Eintrag** darauf
zeigt — dasselbe Schiff darf mehrfach im Tagebuch stehen.

Der Export enthält nur die Bild-**URLs**, nicht die Bilddaten; sonst wäre die
Datei um Größenordnungen größer. Nach einem Import wird ein Foto bei Bedarf neu
geladen.

#### Die rohe URL war der Fehler: „im Tagebuch fehlt das Bild zur WANGEROOGE"

Gemeldeter Fall. Deren Foto kommt aus **Wikidata `P18`**, und das ist eine
`Special:FilePath`-URL **ohne** `width`-Parameter. Überall sonst läuft jede
Bild-URL durch `photoUrl()` (http→https, plus `?width=480`) — **im Tagebuch lief
die rohe URL**. Das schlug gleich dreifach durch:

| Stelle | Folge |
|---|---|
| `keepDiaryPhoto()` | `PHOTO_CACHE.match(roh)` verfehlt den längst vorhandenen Blob — **anderer Schlüssel** |
| dessen Ersatzabruf | geht auf die URL ohne `width`, die kein Bild liefert (siehe „`?width=480` ist Pflicht") |
| `renderDiary()` → `diaryPhoto()` | setzt die rohe URL als `<img src>` — `naturalWidth` bleibt 0 |

Gemessen (`scratchpad/tagebuchbild.js`): Die Detailansicht fragte
`…Harle%20Gatt.JPG?width=480` und zeigte das Bild; das Tagebuch fragte
dieselbe Datei **dreimal ohne** `width` und blieb leer.

**Warum es bei Commons-Fotos nie auffiel:** Deren URLs kommen fertig aus der
API (`info.thumburl`, schon `https` auf `upload.wikimedia.org`). Für die ist
`photoUrl()` die Identität, roh und gefaltet sind gleich. Nur bei
**Wikidata-P18-URLs** gehen sie auseinander — und genau diese Quelle hat die
WANGEROOGE.

**Behoben an einer Stelle statt an vier:** `diaryBild(ship)` liefert die URL,
und `keepDiaryPhoto()`, `dropDiaryPhoto()` und `diaryPhoto()` falten zusätzlich
**selbst** (`photoUrl()` ist idempotent). So können die Aufrufer es nicht wieder
auseinanderlaufen lassen. Bei `dropDiaryPhoto()` müssen **beide** Seiten des
`stillUsed`-Vergleichs gefaltet sein — sonst vergleicht man rohe mit gefalteten
URLs und löscht ein Bild, das noch ein anderer Eintrag braucht.

**Bestehende Einträge heilen sich selbst:** `renderDiary()` faltet beim Lesen,
findet den Blob im normalen Bildspeicher und legt ihn im Tagebuchtopf ab. Der
Nutzer muss nichts neu eintragen — im Test ausdrücklich mit einem alten Eintrag
samt roher `http://`-URL geprüft.

Netter Nebeneffekt: Nach dem Fix macht `keepDiaryPhoto()` **null**
Netzanfragen — der Blob liegt schon unter demselben Schlüssel, den die
Detailansicht benutzt hat.

**Merke:** Jede Bild-URL in dieser App gehört durch `photoUrl()`. Wer einen
neuen Anzeigeort ergänzt, faltet dort ebenfalls — die Funktion ist idempotent,
doppeltes Falten schadet nicht, fehlendes schon.

**Zwei Testfallen dabei**, beide haben die Probe erst nichts messen lassen:

- **Ein selbstgebasteltes JPEG-Bytemuster lädt der Browser nicht**
  (`naturalWidth` bleibt 0) — die Probe wäre auch nach dem Fix rot geblieben.
  Es braucht ein wirklich dekodierbares Bild.
- **`addInitScript(() => localStorage.clear())` läuft bei JEDEM Laden**, also
  auch beim `reload` — und löscht den gerade gesetzten Testeintrag wieder.
  Deshalb ein Merker (`__behalten`), der das Leeren einmalig überspringt.

### Zwei Schubladen, ein Abdunkler

`#settings` ist erst unter 1024 px eine Schublade und darüber eine Spalte;
`#diary` ist auf **jeder** Breite eine. `setDrawerOpen()` schließt deshalb immer
die jeweils andere und zeigt den Abdunkler nur, wenn die offene Schublade an
dieser Breite auch wirklich eine ist (`istSchublade()`). Beides ist im Bauen
schiefgegangen: Ohne das Erste nimmt das Schließen der einen den Hintergrund
unter der anderen weg, ohne das Zweite legt sich der Abdunkler am Desktop über
genau die Technik-Spalte, die er freigeben soll.

### Stapelreihenfolge: die Kopfzeile war unter der Detailleiste begraben

`#detail` ist eine feste Leiste ganz rechts über die volle Höhe (z-index 1200) —
also genau über den Knöpfen der Kopfzeile. Bei geöffnetem Schiff war
„Verbindung & Technik" nicht anklickbar; mit dem Tagebuch-Knopf fiel es auf, weil
man ihn gerade dann braucht.

Die Ordnung ist jetzt: `#detail` 1200 · **Kopfzeile 1220** · Abdunkler 1250 ·
Schubladen 1300. Die Kopfzeile liegt über der Detailleiste (Knöpfe erreichbar),
aber unter den Schubladen (die bringen ihr eigenes Kreuz mit).

### Und prompt war das Schließkreuz der Leiste selbst unerreichbar

Aus dem Betrieb gemeldet: „Schließen mit Klick auf das Kreuz funktioniert oft
nicht", dazu „auf dem iPhone teilweise zwei senkrechte Bildlaufleisten".

Erste Fassung ließ die Detailleiste über die volle Höhe laufen und gab ihr
`padding-top: calc(var(--head-h) + 0.6rem)`, damit der Inhalt unter der
Kopfzeile beginnt. **Diese Zeile wird auf dem Telefon überschrieben** — in
`@media (max-width: 700px)` stand seit langem
`#detail { padding-top: max(1rem, env(safe-area-inset-top)); … }`. Auf jedem
iPhone im Hochformat gewann die Media Query, der Abstand fiel weg, und das
Kreuz lag hinter der jetzt darüberliegenden Kopfzeile. Daher „oft" und nicht
„immer": Am Desktop und auf dem iPad quer griff der Abstand.

**Die Lehre:** Ein Abstand ist die falsche Zurückhaltung. Die Leiste beginnt
jetzt per **`top: var(--head-h)`** unter der Kopfzeile und endet bei
`bottom: 0` — sie überlappt gar nicht mehr, und keine Abstandsregel kann das
noch kippen. `top`/`bottom` statt `height: 100%` bindet die Höhe nebenbei an
den wirklich sichtbaren Bereich; auf iOS passt `height: 100%` nicht, sobald
Safari seine Leisten ein- oder ausblendet.

Dazu drei Ergänzungen:

- **`.detail-head` klebt** (`position: sticky; top: 0`), sonst ist das Kreuz in
  einer langen Detailansicht nach ein paar Wischern aus dem Bild. Der obere
  Innenabstand gehört dabei auf **den Kopf**, nicht auf den Container — sonst
  bleibt über dem klebenden Kopf ein Streifen offen, durch den der gescrollte
  Inhalt sichtbar durchläuft.
- **`--head-h` hängt an einem `ResizeObserver`** auf `<header>`. `resize` allein
  genügt nicht: `.headright` bricht um, wenn die Beschriftung wächst
  („Tagebuch (0)" → „Tagebuch (128)"), und dabei feuert kein `resize`.
- **`#detailClose` ist unter `@media (pointer: coarse)` 44 × 44 px.** 28 px in
  der Ecke sind am Finger zu wenig.

### Zwei Bildlaufleisten: `overscroll-behavior` und ein Besitzer für die Sperre

`#detail` ist ein fester Scrollbereich **über** einer Seite, die selbst scrollt.
Ohne `overscroll-behavior: contain` reicht iOS den Wisch nach hinten durch —
genau das sieht wie zwei Bildlaufleisten aus. Die Regel haben jetzt `#detail`,
`#settings` und `#diary`.

Zusätzlich sperrt die Detailleiste unter 640 px die Seite (dieselbe Grenze, ab
der sie voll verdeckt; am Desktop soll der Hintergrund weiter scrollen). Die
Sperre hat dafür **einen Besitzer**: `updateScrollLock()` **leitet** sie aus dem
tatsächlichen Zustand ab, statt sie zu setzen. Vorher schrieb `setDrawerOpen()`
direkt in `document.body.style.overflow` — damit hätte das Schließen einer
Schublade die Sperre aufgehoben, obwohl darunter noch die Leiste offen stand.

**Test-Hinweis:** `window.scrollBy()` taugt als Probe **nicht**.
`overflow: hidden` verhindert das Scrollen durch den Nutzer, nicht das
programmatische — der Test lief damit grün ins Leere. Gemessen wird stattdessen
das, was der Finger tut: über der Leiste weiterwischen, wenn sie am Ende ist
(`page.mouse.wheel` über dem Element), und prüfen, dass die Seite dahinter
stehen bleibt.

### „Ins Tagebuch" färbte sich kurz und trug nichts ein

Gemeldet kurz nach dem Kreuz-Fix, und es ist **dieselbe Familie**: ein
Bedienelement in einem senkrecht scrollbaren Bereich.

Playwrights `tap()` trifft punktgenau und reproduziert das nie. Mit echten
Touch-Ereignissen per CDP (`scratchpad/touchslop.js`, Wischweg in Schritten
gestellt) fällt es sofort auf:

| Wischweg | Klick | Leiste gescrollt | Ereignisse am Knopf |
|---|---|---|---|
| 0–12 px | ✅ | 0 px | `pointerdown … click` |
| **20 px** | ❌ | 9 px | `pointerdown, touchstart, touchmove, **pointercancel**, touchend` |
| 40 px | ❌ | 39 px | dito |

`pointerdown` kommt an — **daher der kurze Farbwechsel** — dann entscheidet der
Browser, dass die Geste ein Scrollen ist, und verwirft den Klick per
`pointercancel`. Auf dem Telefon passiert das ständig, weil man zur Knopfleiste
am Ende der Leiste erst scrollen muss und unmittelbar danach tippt.

**Was nicht hilft:** `touch-action: manipulation` nimmt nur die
Doppeltipp-Wartezeit. Und eine „großzügige" eigene Tipperkennung wäre gefährlich
gewesen, solange die Leiste dabei wirklich scrollt (9 px bei 20 px Zugweg) —
dann hätte sie beim Scrollen Sichtungen eingetragen.

**Was hilft, in dieser Reihenfolge:**

1. **`touch-action: none` auf den Knöpfen** in `.detail-head` und
   `.detail-actions`. Damit ist die Fläche kein Scrollbereich mehr: `pointercancel`
   bleibt aus, die Leiste scrollt von einem Knopf aus nicht mehr (gemessen 0 px).
   Von einem Bedienelement aus zu scrollen entfällt — bei Knöpfen richtig so.
2. **Erst dadurch wird eine eigene Tipperkennung sicher** (`tippfest()`): Es gibt
   keine konkurrierende Geste mehr, die der Zug bedeuten könnte. Die Regel ist
   die eines jeden Knopfes — gedrückt auf dem Knopf, losgelassen auf dem Knopf =
   ausgelöst. `preventDefault()` im `touchend` plus ein 700-ms-Fenster
   verhindern, dass ein nachgereichter Klick die Aktion ein zweites Mal auslöst.
   Chromium erzeugt oberhalb seiner eigenen Schwelle **auch ohne** Scrollen
   keinen Klick mehr — ohne diesen Schritt blieben 20 px weiterhin wirkungslos.
3. **44 px Mindesthöhe** für die Knöpfe unter `pointer: coarse`. Nicht Kosmetik:
   Ausgelöst wird beim Loslassen *auf* dem Knopf, und ein 34-px-Knopf ist bei
   20 px Fingerdrift schon verlassen.
4. **Ein zweiter Knopf im klebenden Kopf** (`#detailDiaryTop`, „📖+"), damit die
   Hauptaktion **ohne vorheriges Scrollen** erreichbar ist — die Situation, in
   der es schiefging, entsteht damit gar nicht erst.

Ergebnis derselben Messung nach dem Umbau: 20 px tragen ein, 40 px nicht — und
das ist korrekt, dort hat der Finger den Knopf verlassen.

**Und die halbe Miete war Rückmeldung.** Vorher sahen „Tipp verworfen" und
„Tipp angekommen" identisch aus: gleiche Beschriftung vorher wie nachher, und
der Zähler in der Kopfzeile liegt bei offener Leiste dahinter. Jetzt quittiert
der Knopf mit grünem ✓ (`quittung()`), die Unterzeile zeigt „📖 1× im Tagebuch,
zuletzt gerade eben", und im Log steht eine Zeile. Wer eine Aktion ergänzt, die
in einen unsichtbaren Bereich schreibt: genauso quittieren.

### Warum die Tests das nicht gefangen haben

`viztest.js` klickt das Kreuz — aber nur bei **Desktop-Breite**, wo der Abstand
griff. `mobiletest.js` prüft die Telefonbreiten, öffnete dort aber nie eine
Detailansicht. Die Lücke lag genau dazwischen.

`mobiletest.js` hat deshalb jetzt eine **Dauerprobe**: Detailansicht öffnen und
per `elementFromPoint` auf der Kreuzmitte nachsehen, was dort wirklich liegt.
Gegen den alten Stand meldet sie `VERDECKT von <header>` — und zwar nur im
iPhone-Profil, genau wie im Betrieb. Dazu `scratchpad/detailtouch.js` mit
32 Prüfungen über vier Geräteprofile.

**Beim Messen die Einblendung abwarten:** Die Leiste fährt mit
`transform`/`transition` in 0,18 s herein. Wer im selben synchronen
`evaluate()` klickt und misst, bekommt `elementFromPoint === null`, weil sie
noch außerhalb des Viewports liegt.

### Test

`scratchpad/diarytest.js` (24 Prüfungen) und `scratchpad/diaryquota.js`.
Die beiden Proben, die den Charakter der Funktion festhalten:
**„Zwischenspeicher leeren" lässt das Tagebuch unberührt**, und bei vollem
`localStorage` bleibt der gespeicherte Bestand vollständig stehen.

Zwei Fallen beim Testen: Ein `localStorage.removeItem("aisstream_ais")` vor
einem Reload wird vom `pagehide`-Sichern sofort rückgängig gemacht — es gehört
in ein `addInitScript`. Und am Desktop ist `#settingsClose` per CSS ausgeblendet,
weil die Technik dort eine Spalte ist.

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

- **Schiff-Spotter-Tagebuch**: Sichtungen dauerhaft lokal festhalten, mit
  eingefrorenem Schiffszustand, Notiz, Foto und Entfernung zum eigenen
  Standort; Export/Import als JSON. Eigene Schublade, kein Cache
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
- **Schiffsnamen auf der Karte** ab Zoomstufe 12, abschaltbar; Namen werden
  über `syncMarker()` aus allen Quellen zuverlässig nachgezogen (siehe
  eigenen Abschnitt oben)
- **Kartenmarker kodieren Typ und Größe** (siehe eigenen Abschnitt oben):
  sieben validierte Kategoriefarben, vier Größenstufen S/M/L/XL am Durchmesser,
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
- **Laufendes Auffrischen** alle 60 s für den aktuellen Ausschnitt, abschaltbar
  (siehe eigenen Abschnitt oben): hält die Karte auch ganz ohne API-Key
  lebendig, pausiert im Hintergrundtab und bei weit herausgezoomter Karte
- **Alter der Daten** in Sekunden, rechtsbündig direkt hinter dem Namen,
  sekündlich mitlaufend und ab 600 s farblich markiert
- **Lokale Zwischenspeicher**: Schiffsstatik (60 d), AIS-Daten inklusive
  Personen an Bord (30 min), Registerdaten je MMSI (30 d) und Schiffsfotos
  als Blob im Cache Storage — alles über einen Knopf leerbar, mit
  Zählerständen im UI
- **Freitextsuche** über Tabelle (MMSI, Name, Rufzeichen, IMO, Ziel, Typ,
  Land, Stationsart), 150 ms debounced
- Rohdaten-Ansicht je MMSI (letzte Nachricht je Typ als JSON) + „JSON
  kopieren" in die Zwischenablage + Deeplinks zu MarineTraffic, VesselFinder
  und OpenStreetMap
- **Snapshot-Button**: holt `GET /v1/vessels?bbox=…` für den aktuellen
  Ausschnitt und füllt Karte und Tabelle — ohne WebSocket-Verbindung und
  ohne API-Key. **Beim Verschieben der Karte passiert das automatisch**, sofern
  der Ausschnitt den 50-km-Rand der letzten Abfrage verlässt und nicht größer
  als 15 Quadratgrad ist (siehe eigenen Abschnitt oben). Warnt, wenn kein geladenes Schiff im Kartenausschnitt liegt
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
- **Schiffsfotos** aus Wikidata `P18`, ersatzweise über Wikimedia Commons —
  IMO-Kategorie, IMO-Volltext und zuletzt **MMSI mit Namensbestätigung**, was
  auch Schiffe ohne IMO-Nummer erreicht (Lotsen- und Arbeitsboote). Mit
  Urheber- und Lizenzangabe. Reicht Msg 5 die IMO nach, wird das Bild
  automatisch nachgeholt (siehe eigenen Abschnitt)
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
- `MAX_TRACK_POINTS` (300) begrenzt die Tracklänge; die Ship-Map räumt
  `pruneStaleShips()` inzwischen alle 60 s auf (30 Minuten ohne Meldung →
  raus)
