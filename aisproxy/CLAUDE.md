# aisproxy — warum er so gebaut ist

Caching-Proxy für den AIS-Client (`../aisstream/`). Node 22, zwei Dateien
Abhängigkeit (`ws`), SQLite aus dem Node-Kern. Bedienung steht im README —
hier steht, **warum** die Entscheidungen so gefallen sind, damit sie beim
nächsten Anfassen nicht versehentlich rückgängig gemacht werden.

Alle Zahlen sind am 27. Aug. 2026 an der echten Schnittstelle gemessen.

## Die vier Messungen, die alles bestimmt haben

| Befund | Wert | Folge |
|---|---|---|
| Stream über 148 sq° (Nordsee) | **50,2 msg/s — Limit gesättigt** | Über großen Flächen ist der Stream nur eine Stichprobe: 2 770 von 12 921 Schiffen je Minute |
| Stream über 21 sq° (unsere Region) | **37,6 msg/s**, 59 % der Schiffe/min | Eine Verbindung deckt alles ab, ungedrosselt |
| `permessage-deflate` | **1,80 → 0,62 GB/Tag** | Der Server handelt es aus. **Diese Messung hat den Entwurf gedreht** |
| Küstenentfernung aller Schiffe | 95,8 % liegen ≤ 100 km | Ein Küstenband-Zuschnitt spart 4,4 % — verworfen |

Die Regionsgröße war der einzige wirksame Hebel: von 412 sq° (19 511 Schiffe)
auf 21 sq° (rund 3 000) sind 88 % weniger Daten.

## Der Stream ist die Hauptquelle, nicht das Netz

Der erste Entwurf hatte es umgekehrt, weil der Stream mit 1,80 GB/Tag zu
teuer schien. Erst die Prüfung auf `permessage-deflate` hat das korrigiert.
Node's **eingebauter** WebSocket kann das nicht aushandeln — deshalb `ws` als
Abhängigkeit, und deshalb prüft `strom.js` beim Upgrade nach und warnt, wenn
der Server es nicht mitmacht.

Das 60-s-Netz bleibt trotzdem drin. Es fängt drei Dinge, die der Strom nicht
kann: Schiffe, die gerade gar nicht melden (drei Viertel liegen still), die
Lücke nach einem Verbindungsabriss, und den Kaltstart.

## `rev` — warum Deltas ohne Zustandshaltung je Client gehen

Ein globaler Zähler, der bei jeder echten Änderung um eins wächst und den
Datensatz stempelt. Der Client schickt seine letzte `rev` mit, der Server
liefert alles Höhere. Kein Zeitvergleich, kein Mengenabgleich, kein
Vergessen — und der Server muss sich pro Client nichts merken außer dem
Ausschnitt.

**Ein Zählerschritt je Meldung, nicht je geändertem Feld.** Der Zähler ist
die Währung der Deltas; er soll zählen, was passiert ist.

### Zwei Fallen, die im Bau aufgetreten sind

**`verlassen()` braucht die Liste der bekannten MMSIs.** Ohne diesen Filter
meldete jeder Takt *alle* geänderten Schiffe außerhalb der Box als „weg" —
bei 3 000 Schiffen und einer kleinen Box hunderte MMSIs je Takt, also genau
der Verkehr, den das Binärformat einspart.

**Ältere Positionsmeldungen dürfen neuere nicht überschreiben.** Strom und
Netz überholen sich gegenseitig. Ohne den Zeitstempelvergleich in `melde()`
springt die Position hinter einen frischeren Wert zurück. Stammdaten dürfen
aus einer alten Meldung trotzdem durch — ein Name veraltet nicht.

## Abweichung vom Entwurf: kein Ortsgitter im heißen Zustand

Der Entwurf sah ein 0,25°-Gitter auch für den heißen Zustand vor. Eingebaut
ist es nur für die **kalte** Ablage, dort als indizierte Spalte. Im heißen
Zustand wird linear durchgesehen. Gemessen (`test/zustand.test.js`):
**0,126 ms für einen vollen Durchlauf über 2 915 Schiffe.** Ein zweiter
Index, der bei jeder Positionsänderung gepflegt werden muss, wäre mehr
Fehlerquelle als Gewinn gewesen.

## Tagesweise Tabellen

`pos_YYYYMMDD`. Aufräumen heißt `DROP TABLE` — ein `DELETE` über Millionen
Zeilen lässt die Datei aufgebläht zurück und braucht `VACUUM`, das die
Datenbank minutenlang sperrt.

**Ein Schreibstapel über Mitternacht fällt auf zwei Tabellen.** Ohne
Gruppierung nach Tag landete die Hälfte falsch — dafür gibt es eine eigene
Probe.

**`sog IS NULL` gilt nicht als „liegt still".** Bei der Verdichtung werden
liegende Schiffe verworfen, Schiffe mit *unbekannter* Geschwindigkeit aber
nicht: Ein Schiff ohne Geschwindigkeitsangabe, das seine Position ändert,
fährt sehr wohl. Gemessen betrifft das 117 von 12 921 Schiffen (0,9 %),
kostet also fast nichts — und verhindert einen Datenverlust, den man
hinterher nicht mehr bemerkt.

## Register: was nur ein Proxy kann

**Digitraffic trägt in dieser Region nichts bei** — 0 von 20, im Livelauf
0 von 200. Deren AIS deckt die nördliche und östliche Ostsee ab (Flaggen im
Bestand: SE 106, FI 67, RU 40, EE 26 von 400), nicht die Deutsche Bucht. Der
Sammelabruf bleibt drin, weil er *einen* Abruf kostet (1 165 Schiffe, 58 KB)
und die Ostseeränder mitnimmt.

**Die IMO kommt deshalb aus `ShipStaticData`** (AIS-Typ 5), nicht mehr aus
Digitraffic. Der Strom liefert sie ohnehin.

**Wikidata gebündelt:** `VALUES { "211..." "244..." … }` — gemessen 200
MMSIs in einer Abfrage, 0,3 s, 28 Treffer. Im Livelauf: 200 Schiffe
vollständig abgearbeitet in 11,3 s mit **zwei** Wikidata-Abfragen statt 200.
Hochgerechnet auf 3 000 Schiffe: rund 16 Abfragen.

**`gefunden = 0` ist ein Ergebnis und muss gespeichert werden.** Ohne diesen
Vermerk liefe der Proxy endlos gegen dieselben aussichtslosen Schiffe. Der
zweite Lauf im Test stellt deshalb keine einzige Abfrage mehr.

**Fotos werden nicht selbst verkleinert.** Commons (`iiurlwidth`) und
Wikimedia (`?width=`) liefern die gewünschte Breite serverseitig — also
braucht der Proxy keine Bildbibliothek.

## Seezeichen: eingebaut, aber der Upstream liefert keine

Msg 21 (`AidsToNavigationReport`) wird jetzt uebersetzt, gespeichert und an
den Client gegeben. **Gemessen am 27. Aug. 2026 kommt davon trotzdem nichts
an, und zwar nicht wegen des Proxys:**

| Probe | Ergebnis |
|---|---|
| Abo nur auf `AidsToNavigationReport`, 3 min ueber der ganzen Region | **0 Nachrichten** |
| Abo ganz OHNE Typfilter, 75 s | 2 261 PositionReport, 104 ShipStaticData, 63 StandardClassB, 33 StaticDataReport - **kein einziges Seezeichen** |
| REST-Abzug von openwaters | 2 762 Datensaetze, **alle** `kind=vessel` |

Der Upstream `ais.openwaters.io` fuehrt schlicht keine Seezeichen. Wer sie
sehen will, stellt den Strom auf aisstream.io um - der Proxy kann das ohne
Codeaenderung:

```
AIS_STROM_URL=wss://stream.aisstream.io/v0/stream
AIS_TOKEN=<eigener aisstream-Schluessel>
```

Den zusaetzlichen Typ im Abo vertraegt der jetzige Upstream: gemessen laufen
die Schiffe unveraendert weiter (2 261 Positionsmeldungen in 75 s).

**Drei Dinge, die beim Bauen zaehlten:**

- **Der Typcode gehoert NICHT in `typ`.** Msg 21 zaehlt eine eigene Liste:
  20 ist dort "Leuchttonne", in Msg 5 waere es ein Segelboot. Er steht als
  `atonTyp` daneben, und der Client beschriftet ihn mit `atonTypeLabel()`.
- **Der Name kann laenger als 20 Zeichen sein.** Der Rest kommt in
  `NameExtension` und gehoert ohne Trennzeichen angehaengt
  ("ELBE APPROACH LIGHT" + "BUOY").
- **Keine Positionshistorie fuer Tonnen.** Sie bewegen sich nicht, melden
  aber alle paar Minuten - und weil ihre Geschwindigkeit unbekannt ist, wuerde
  die Verdichtung sie ausdruecklich behalten (siehe `sog IS NULL` oben). Das
  ergaebe eine Spur aus lauter identischen Punkten. `merke()` steigt fuer sie
  aus, der Stammsatz wird trotzdem geschrieben.

Die Flagge `FLAG_SEEZEICHEN` gab es im Drahtformat schon, der Client wertet
sie seit jeher aus - es kam nur nie eine an.

Geprueft ist die ganze Kette gegen einen **eigenen AIS-Strom**
(`scratchpad/seezeichen.js`): Msg 21 hinein, und am anderen Ende steht im
Client "ELBE APPROACH LIGHTBUOY / Leuchttonne / 8x6", im Detailfenster als
Seezeichen gefuehrt - 16 Pruefungen, dazu die Gegenprobe, dass die Tonne
keine Positionspunkte bekommt und das Schiff daneben schon.

## Zielangaben: Kuerzel in der Tabelle, Klartext im Detailfenster

Das Zielfeld im AIS ist Freitext, und die Schiffe schreiben, was sie wollen.
Gemessen an **1 538 Zielangaben** aus dem laufenden Betrieb (765 verschiedene):

| Form | Anteil |
|---|---|
| Klartext ("HAMBURG", "FISHING GROUNDS", "FOR ORDERS") | 1 079 |
| reiner UN/LOCODE ("DEHAM") | 320 |
| Route mit Trenner ("SEMMA<>DETRV", "BE ANR > DE HAM") | 84 |
| LOCODE mit Leerzeichen ("DE HAM") | 55 |

Aufgeloest wird **nur im Detailfenster** - in der Tabelle bliebe von der
Spalte nichts uebrig, wenn dort "Antwerpen ⇄ Hamburg (BEANR<>DEHAM)" stuende.

**Die Falle: Ein fuenfstelliger Ortsname sieht aus wie ein LOCODE.** "EMDEN",
"BRAKE", "STADE", "RODBY", "VAREL" und "TAARS" kommen alle im echten Verkehr
vor. "BRAKE" als BR+AKE zu lesen und zu Brasilien zu machen waere schlimmer
als der rohe Text - deshalb wird **ausschliesslich** aufgeloest, was in der
Tabelle steht, und alles andere bleibt unangetastet.

### Die Liste kommt von der UNECE, nicht aus Wikidata

Erst stand hier eine aus Wikidata (P1937) gebaute Tabelle. Gemessen deckte
sie **76 %** der Codes im echten Verkehr ab und fuehrte Namen wie "Hamburger
Hafen" statt "Hamburg"; Eemshaven (NLEEM) und Aabenraa (DKAAB) fehlten ganz.

Die **offizielle Liste** der UNECE hat 116 213 Zeilen und eine Spalte, die
alles einfacher macht: `Function`. Beginnt sie mit `1`, ist es ein Seehafen -
**17 596 weltweit**. Gemessen deckt diese Auswahl **84 %** ab (alle Codes
zusammen 86 %; die Luecke sind Klartextnamen wie EMDEN, BRAKE, STADE, die nur
wie ein Code aussehen). Import: 1,0 s fuer die 7-MB-Datei, danach 17 520
Zeilen in der Tabelle `ort`.

**Sie liegt im Proxy, nicht im Client**: 289 KB waeren fast so viel wie der
ganze ausgelieferte Client. Der Client fragt ueber `/v1/ort?codes=…` nur die
Codes ab, die er gerade sieht, merkt sich die Antwort lokal - **auch die
Fehlanzeige**, sonst fragt er denselben Fantasiecode bei jedem Oeffnen erneut -
und traegt fuer den Betrieb ohne Proxy einen Rueckfall von 163 Haefen bei sich
(2,5 KB): die der Region plus die grossen Welthaefen.

### Angezeigt wird der deutsche Name, daneben steht der amtliche

Erst standen hier die amtlichen Namen - "København", "Gdansk", "Szczecin".
Das ist die Schreibweise auf der Seekarte, in einer deutschen Oberflaeche
aber fremd. Gewuenscht war die deutsche Form, und die holt `ortDeutsch()`
aus Wikidata (P1937 zum Code, `rdfs:label` mit `FILTER(LANG(?l) = "de")`).

**Gemessen beim ersten Lauf: 726 der 17 596 Seehaefen bekommen einen
deutschen Namen, in 41 s, kein Buendel fehlgeschlagen.** Danach steht dort
Kopenhagen statt København, Danzig statt Gdansk, Stettin statt Szczecin,
Sankt Petersburg statt "Saint Petersburg (ex Leningrad)" und Genua statt
Genova.

Vier Dinge daran sind Absicht:

- **Beide Namen bleiben stehen.** `name` traegt die angezeigte Form,
  `amtlich` die aus der UNECE-Liste. Ein ueberschriebener Quellwert waere
  weg, und `ortStand().deutsch` kann nur zaehlen, solange es beide gibt.
- **Der naechste Listenabzug ueberschreibt den deutschen Namen nicht.** Die
  Liste wird alle 90 Tage neu geholt; ohne die `CASE`-Bedingung im Upsert
  waere aus Kopenhagen nach 90 Tagen wieder København. Der Test dazu faellt
  mit zurueckgebautem Upsert durch - er misst also wirklich etwas.
- **Anlagen fallen raus.** Zu einem Code stehen mehrere Labels ("Hamburg",
  "Hamburger Hafen", "Hamburg Hauptbahnhof"). Gemeint ist der Ort: Labels mit
  Hafen/Bahnhof/Terminal/Station werden verworfen, vom Rest gewinnt das
  kuerzeste.
- **Gefragt wird in Buendeln von zwoelf Laendern.** Eine Abfrage ueber alle
  Codes kam gekappt zurueck ("Unterminated string"); ein gescheitertes
  Buendel laesst die anderen unberuehrt, und wiederholt wird einmal.

Wikidatas deutsche Labels sind dabei genau das, was heute ueblich ist:
Danzig und Stettin ja, Memel und Koenigsberg nein - dort stehen Klaipeda und
Kaliningrad. Es wird also nichts entschieden, was nicht schon entschieden
ist. Wo es keine deutsche Form gibt, bleibt der amtliche Name stehen; das ist
die richtige Auskunft, keine Luecke.

### Der Weg dorthin muss auch offenstehen

Gemeldet: „Warum wird bei MMSI 247091500 CIABJ im Ziel nicht aufgeloest?" —
CIABJ ist Abidjan, steht mit `Function = 1--45---` in der UNECE-Liste und
haette aufgeloest werden muessen. Nachgemessen an der laufenden Anlage:

| Pfad | ohne Basic-Auth, nur mit Token |
|---|---|
| `/v1/ship/247091500` | **200** |
| `/v1/snapshot` | **200** |
| `/v1/ort?codes=CIABJ` | **401** |

`/v1/ort` kam nach der `Caddyfile` dazu und stand nicht im
`@schnittstelle`-Matcher. Caddy verlangte dafuer Basic-Auth, die ein
Browser-`fetch` nicht mitschickt, und antwortete mit 401 — **der Proxy wurde
nie gefragt**. Auffallen konnte das nur bei Haefen ausserhalb der 163 Eintraege
des Client-Rueckfalls: Hamburg, Rotterdam und Danzig loest der Client selbst
auf, Abidjan nicht.

Drei Dinge sind daraufhin geaendert worden, und alle drei braucht es:

- **Der Pfad steht jetzt im Matcher.** `test/caddy.test.js` vergleicht die
  Liste mit den Routen in `server.js` und faellt durch, wenn einer fehlt —
  mit zurueckgebautem Eintrag meldet er genau `/v1/ort`.
- **`update.sh` laedt Caddy neu.** Die `Caddyfile` ist eingehaengt, nicht ins
  Abbild gebaut: `up -d --build` tauscht den Container nicht aus, wenn sich
  nur diese Datei aendert. Der Fix waere sonst nach dem Update in der Datei
  gestanden und trotzdem wirkungslos geblieben.
- **Der Client meldet den 401.** Vorher verschluckte er jede fehlgeschlagene
  Antwort (`r.ok ? r.json() : null`), und der einzige Hinweis war ein
  Ortsname, der roh stehenblieb. Jetzt steht die Ursache im Protokoll —
  einmal, nicht bei jeder Zielangabe.

### Und dann ein drittes Mal — deshalb prüft jetzt ein Skript die ANLAGE

`/v1/ort` (27. Aug.), `/v1/einstellungen` (30. Aug. vormittags), `/v1/anomalien`
(30. Aug. nachmittags): dreimal derselbe Fehler. Beim dritten Mal war es
besonders deutlich, weil alles andere nachweislich lief —
`/v1/status` meldete **1 505 Schleifen von 471 Schiffen**, mit Basic-Auth
lieferte `/v1/anomalien` 30 Gebiete, und die Karte im Browser blieb trotzdem
leer. Von allen acht Schnittstellenpfaden war genau der neue betroffen:

| Pfad, ohne Token | wer antwortet |
|---|---|
| `/v1/live`, `/v1/snapshot`, `/v1/replay`, `/v1/track`, `/v1/ship/…`, `/v1/ort`, `/v1/einstellungen` | Proxy (401 als JSON) |
| `/v1/anomalien` | **Caddy** (401 mit `www-authenticate: Basic`) |

**Diese Unterscheidung ist das ganze Werkzeug:** Caddys 401 trägt den Kopf
`www-authenticate: Basic`, der 401 des Proxys nicht. Damit lässt sich von
außen feststellen, wer abweist — **ohne Token auf einer Befehlszeile**.

`caddy-pruefen.sh` vergleicht die `@schnittstelle`-Zeile mit der **laufenden**
Konfiguration (Caddys Admin-Schnittstelle im Container auf `localhost:2019`,
sonst der Abruf von außen). `update.sh` ruft es nach dem Reload mit `--heilen`
auf: Bei Abweichung wird der Container neu gestartet **und danach nachgesehen**
— ein Neustart, dessen Wirkung niemand prüft, ist genau der Schritt, der hier
schon zweimal Erfolg gemeldet hat, ohne einen zu haben. Von Hand geht es auch:

```
/opt/aisproxy/caddy-pruefen.sh            # nur nachsehen
/opt/aisproxy/caddy-pruefen.sh --heilen   # und bei Abweichung neu starten
```

Rückgabe **0** alles da · **1** es fehlt etwas · **2** nicht feststellbar. Die
2 ist Absicht und schlägt keinen Alarm: Ein falscher Alarm, der jemanden eine
laufende Anlage zurückbauen lässt, wäre schlimmer als die Lücke.

**Geprüft ist, dass es „nein" sagen kann** (`test/caddy-pruefen.probe.sh`, 13
Prüfungen mit gestelltem `docker` auf dem PATH): vollständige Konfiguration →
0; fehlendes `/v1/anomalien*` → 1, **und der fehlende Pfad wird benannt**;
`--heilen` startet wirklich neu und sieht danach nach; ein vergeblicher
Neustart bleibt bei 1 und sagt es; ohne Admin-Schnittstelle und ohne erreichbare
Domain → 2 **ohne ACHTUNG**. Eine Prüfung, die nie „nein" gesagt hat, ist nur
eine Zeile, die immer grün leuchtet.

**Was ich dabei falsch gemacht habe, gehört dazu:** Nach dem zweiten Vorfall
war genau dieses Skript geplant und freigegeben. Ich habe es dann **nicht
gebaut**, weil die 401 beim Nachmessen von selbst verschwunden waren und ich
daraus schloss, der Reload-Schritt erledige das und ein Wächter schlüge nur
Fehlalarm. Zwei Stunden später stand derselbe Fehler wieder da. Aus „ich habe
es nicht mehr reproduzieren können" folgt nicht „es passiert nicht".

Dazu eine vierte Aenderung, dieselbe Familie: Der Client merkte sich
**Fehlanzeigen dauerhaft**. Ein „kenne ich nicht" aus einem Moment, in dem das
Ortsregister noch leer war, waere damit fuer immer stehengeblieben. Gemerkt
werden jetzt nur die Treffer; die Fehlanzeige gilt fuer die Sitzung, und das
reicht, um den Client von wiederholten Fragen abzuhalten.

**Der Rueckfall im Client folgt derselben Regel** - und **nur Seehaefen**.
Beim Nachziehen der deutschen Namen waren vier Binnenorte hineingeraten:
DEBHV (Bruchhausen-Vilsen), DEBKM (Bruckmuehl), DENOK (Nordkirchen), DEODE
(Odenthal). Ihre `Function` beginnt nicht mit `1`, der Proxy kennt sie also
gar nicht - der Client haette Codes ausgeschrieben, zu denen der Proxy `null`
sagt. Besonders daneben: "DENOK" meint auf der Leitung den Nord-Ostsee-Kanal.
**Wer die Tabelle neu baut, filtert wieder auf `Function[0] === "1"`.**

Die Trennerliste kommt ebenfalls aus den Daten, nicht aus der Vorstellung:
`<>`, `<=>`, `<-->`, `<->`, `><`, `>>`, `>`, `-`. Der Bindestrich trennt nur,
wenn **alle** Teile wie ein Code aussehen, sonst zerfiele "SPODSBJERG-TAARS".
Und die Reihenfolge im regulaeren Ausdruck ist laengster Trenner zuerst -
sonst frisst `->` den Anfang von `<-->` und uebrig bleibt "DEHAM<-".

### Wohin es frueher unterwegs war

Gewuenscht: unter „Reise" nicht nur das aktuelle Ziel, sondern auch die
bisherigen. Das AIS-Feld traegt immer nur den aktuellen Stand, und
`stammSetze()` schreibt ihn in dieselbe Spalte - die vorherige Reise war damit
weg, obwohl der Proxy sie mitgehoert hat. Genau das kann nur der Proxy: Er
hoert seit Wochen zu, der Browser erst seit dem Oeffnen der Seite.

Eigene Tabelle `ziel_verlauf` (`mmsi`, `ziel`, `zuerst`, `zuletzt`, `folge`),
gedeckelt auf `ZIEL_VERLAUF_MAX` = 12 je Schiff. Geschrieben wird **nur beim
Wechsel**, nicht bei jeder Wiederholung: Msg 5 kommt je Schiff alle sechs
Minuten, ein Upsert je Nachricht waere bei 3 000 Schiffen Arbeit fuer nichts.

Drei Dinge, die beim Bauen zaehlten:

- **Der Puffer schluckte Wechsel.** `merkeStamm()` haelt je Schiff nur die
  juengste Meldung; drei Ziele innerhalb eines Schreibtakts wurden zu einem,
  und `stammSetze()` sah die mittleren nie. Im Betrieb liegen Wechsel Stunden
  auseinander - „faellt selten weg" ist fuer einen Verlauf aber kein Zustand.
  `merkeStamm()` schreibt das verdraengte Ziel deshalb selbst weg. Gefunden
  hat das die Browserprobe, nicht das Lesen: Sie schickte drei Ziele in
  600 ms, und am Ende stand genau eines in der Datenbank.
- **Sortiert wird nach einem Zaehler, nicht nach der Zeit.** `zuletzt` hat
  Sekundenaufloesung, und im Test lagen vier Wechsel in derselben Sekunde -
  die Reihenfolge war dem Zufall ueberlassen. `folge` ist auch gegen eine
  zurueckgestellte Uhr immun.
- **Ein Schiff ohne Verlauf bekommt das Feld gar nicht**, keine leere Liste.
  Eine leere Liste ist eine Angabe ueber etwas, das es nicht gibt.

`zuerst` ist die erste Sichtung dieses Ziels, `zuletzt` der letzte **Wechsel
darauf** - nicht die letzte Wiederholung. Fuer die Reihenfolge der
vergangenen Ziele ist genau das die richtige Angabe.

## Die Zugaenge fuer die 3D-Ansicht liegen hier, nicht in jedem Browser

`GET /v1/einstellungen` gibt `{ ion, google }` aus `AIS_ION_TOKEN` und
`AIS_GOOGLE_KEY`. Gebeten wurde darum, damit der ion-Token nicht auf jedem
Geraet neu eingetragen werden muss.

**Es ist kein Geheimnis, und das gehoert dazugesagt.** Der Pfad liegt hinter
derselben Tokenpruefung wie alles andere - aber dieses Token steht auf
Entscheidung des Betreibers oeffentlich im Client. Wer den Client lesen kann,
kann auch den ion-Token lesen. Der Gewinn ist **eine Stelle zum Wechseln**
statt jedes Geraets; die Absicherung leistet ein ion-Token, der nur lesen darf
und nur die benoetigten Assets kennt.

Zwei Kleinigkeiten mit Absicht:

- **`null` statt `""`.** „Hier ist keiner hinterlegt" ist eine Aussage; ein
  leerer String saehe im Client wie ein leeres Feld aus und liesse offen, ob
  gefragt wurde.
- **Der Pfad musste in den Caddyfile-Matcher.** `test/caddy.test.js` hat das
  beim ersten Lauf gemeldet, bevor irgendjemand es haette merken koennen -
  genau der Fall, fuer den die Probe nach dem `/v1/ort`-Vorfall gebaut wurde.

## Der Typ kommt aus drei Quellen - und keine ist sicher

Gemeldet: „Warum fehlt bei MMSI 309436000 der Typ?" (Liberty of the Seas,
Kreuzfahrtschiff, 339 m). Nachgemessen am 27. Aug. 2026:

| Quelle | Befund |
|---|---|
| AIS-Strom | Gefiltertes Abo auf genau diese MMSI, sechs Minuten: **9 PositionReports, keine einzige Statiknachricht** |
| Snapshot von openwaters | Der Datensatz hat weder `type` noch `name` - nur Position, Kurs, Fahrt |
| Wikidata | kennt das Schiff und sagt **„Kreuzfahrtschiff"** (P31) |

Der Typ steht in AIS **nur in Msg 5 bzw. Msg 24**, nicht in einer
Positionsmeldung. Msg 5 wiederholt sich je Schiff etwa alle sechs Minuten,
und der Feed reicht unter Last nur eine Stichprobe durch - gemessen kannten
nach vier Minuten 455 von 2 775 Schiffen ihre AIS-Klasse, nach 13 Minuten
668 ihre Abmessungen. Ein Schiff ohne Typ ist also der Normalfall, kein
Fehler.

**Die dritte Quelle wurde weggeworfen:** Die SPARQL-Abfrage in `register.js`
holt `?typeLabel` (P31) seit jeher mit, die Zuordnung darunter las das Feld
nur nie. Jetzt landet es als `wd_typ` in der Stammtabelle und geht an den
Client, der daraus die Zeile „Typ laut Register" fuellt - und in
`typeCategory()` Farbe und Kategorie bestimmt, wenn der AIS-Code fehlt.

Beim Anlegen der Spalte setzt `speicher.js` **einmalig** `geprueft = 0` fuer
alle Saetze mit `wd_entity`: Wer schon als „gefunden" vermerkt ist, wuerde
sonst 30 Tage lang nicht mehr gefragt und bliebe so lange ohne Typ. Nur
diese Saetze - ein Fehltreffer ohne Wikidata-Eintrag bleibt in Ruhe, sonst
liefe die Abfrage wieder gegen aussichtslose Schiffe.

## Bilder: der Abzug ist der Hebel, nicht die nächste Quelle

Gemeldeter Ausgangspunkt: „Welche Möglichkeit gibt es, an mehr Bilder zu
kommen?" Die naheliegende Antwort — neue Bilddienste anbinden — wäre die
falsche gewesen. Gemessen am 27. Aug. 2026 am laufenden Server:

| | |
|---|---|
| 120 Schiffe aus einem 90-km-Ausschnitt | **10** hatten ein Foto |
| dieselben Schiffe gegen Wikidata | **46 von 77** mit IMO haben dort ein Bild, dazu 21 über die MMSI |
| Stichprobe 31 mit IMO | Wikidata hat für 22 ein Bild, der Proxy führte **19 davon ohne Foto** |

Und an den Fehlstellen nachgesehen — das war die Diagnose:

```
211209320 HARLINGERLAND  gefunden=1  foto=None  wd_entity=Q1585523
211217990 Spiekeroog II  gefunden=1  foto=None  wd_entity=Q52324773
211224140 NORDSEE        gefunden=1  foto=None  wd_entity=Q1998609
```

`wd_entity` steht drin: **Der Wikidata-Treffer kam samt Bild-URL an, nur der
Download schlug fehl.** Die Gegenprobe mit derselben Abfrage findet heute für
5 von 6 dieser MMSIs ein Bild, und der Download liefert HTTP 200 / 42 KB.

### Drei Ursachen, alle behoben

**1. Die Bilddownloads liefen ohne Pause.** `uebernimm()` lud das Foto
*innerhalb* der Trefferschleife; die 1 s aus `REGISTER_PAUSE_MS` liegt nur
zwischen den Bündeln. 26 Downloads am Stück. Gemessen: **25 Bilder ohne Pause
→ 2× HTTP 429.** Jetzt holt `fotoLauf()` die Bilder getrennt, mit
`FOTO_PAUSE_MS` und einem Wiederholversuch bei 429.

**2. Ein verlorener Download war von „hat kein Bild" nicht zu
unterscheiden.** `uebernimm()` setzte `gefunden: 1, geprueft: jetzt`
unabhängig davon — 30 Tage gesperrt. Jetzt hat das Foto **einen eigenen
Stand**: `foto_geprueft`, `foto_quelle`, `foto_seite` und `fotoFaellig()`.
`holeFoto()` **wirft** bei einem gescheiterten Abruf, statt `null` zu
liefern; nur „gibt es nicht" wird vermerkt.

Zwei Fristen: Lief die Suche **ohne IMO**, ist sie unvollständig
(`foto_quelle = "teil"`), mit IMO war sie vollständig (`FOTO_FEHL_MS` = 7 Tage).

**Korrektur vom 28. Aug. 2026:** Der „teil"-Fall war zunächst an eine kurze
Uhr gehängt (`FOTO_TEIL_MS` = 1 h) mit der Begründung, die IMO könne jede
Minute eintreffen. Am laufenden Server gemessen kostete das mehr, als es
brachte:

| Weg | Abrufe | Bilder |
|---|---|---|
| `commonsMmsi` | 243 | **1** |
| `flickrMmsi` | 281 | **6** |

524 Abrufe für 7 Bilder, und weil dieselben Schiffe stündlich wieder fällig
wurden, ging das Kontingent von 300 Versuchen je Lauf dafür drauf — der
Rückstand stand unverändert bei `fotoOffen: 1587`.

Jetzt entscheidet der **Anlass**, nicht die Uhr: Ein „teil"-Schiff wird wieder
fällig, sobald es **wirklich eine IMO hat** (dann lohnen Kategorie- und
Volltextweg) oder der **Bildabzug es über die MMSI kennt** (dann ist es ein
Download ohne jede Suche). Ohne beides bleibt es bis zur langen Frist liegen —
nicht für immer, denn Wikidata wächst. `FOTO_TEIL_MS` ist damit die Frist für
den Anlassfall, nicht mehr der Takt einer Wiederholung.

**3. Der Proxy hatte nur den schwächsten Commons-Weg.** Gemessen im Client:
Kategorie **24 von 25**, Volltext **6 von 25**. Der Proxy kannte nur den
Volltext. `src/bilder.js` spiegelt jetzt alle Regeln des Clients
(`aisstream/index.html` ab Z. 5291) — dieselbe Verabredung wie bei
`src/ais.js`: zweimal vorhanden, mit Verweis aufeinander.

### Der Wikidata-Abzug: alles auf einmal statt je Schiff

Der eigentliche Vorteil des Servers. Gemessen:

| Abzug | Zeilen | Größe | Dauer |
|---|---|---|---|
| IMO → Bild (`P458` + `P18`) | **17 144** | 0,89 MB schlank | 9,8 s |
| MMSI → Bild (`P587` + `P18`) | **8 638** | | 0,9 s |
| Wikidata gesamt mit IMO / MMSI | 96 120 / 38 167 | | |

Beide liegen in `bild_index` (`art`, `kennung`, `url`). Danach ist „hat
dieses Schiff ein Bild?" ein Datenbankzugriff **ohne Netzabruf** — und die
Antwort steht schon bereit, wenn die IMO Stunden später per `ShipStaticData`
eintrifft. Genau das kann der Client prinzipiell nicht: Er fragt je Schiff
und trifft dabei den Moment, in dem er fragt.

Nach dem Schreiben stehen 16 880 statt 17 144 Zeilen — derselbe Rumpf kommt
in Wikidata mehrfach vor, der Primärschlüssel legt sie zusammen. Das ist
richtig so, nicht ein Verlust.

### Die Reihenfolge der Wege

```
Abzug (kein Netzabruf) → Commons-Kategorie → Commons-Volltext
  → Commons MMSI+Name → Flickr imo<nr> → Flickr mmsi<nr>
```

Kuratiertes zuerst, Geratenes zuletzt. Die **Titelregel gehört nur auf die
Volltextwege**: Auf dem Kategorieweg würfe sie die Bilder aller umbenannten
Schiffe weg (IMO 9052692 fährt heute als BON VIVANT, das Foto heißt
*Vestfjord*). Auf dem MMSI-Weg bestätigt stattdessen der **Name** die
Kennung — eine neunstellige Zahl kommt auch zufällig in Beschreibungstexten
vor (Fall HAV MARLIN gegen ein Vietnamkriegsfoto).

**Flickr ohne Schlüssel**, auf Entscheidung des Betreibers auch ohne
Lizenzfilter: Der öffentliche Feed nennt Titel, Autor und Link, **aber keine
Lizenz**. Ausgewiesen wird deshalb, was da ist — Urheber und Link auf die
Fotoseite (`foto_seite`, den der Client als `page` anzeigt). Nur die
geprägten Tags `imo<nr>` und `mmsi<nr>`; eine nackte Zahl wäre kein
Schiffsbeleg.

**Korrektur einer zu frühen Aussage:** Nach einer Stichprobe von 60 Schiffen
stand hier, Flickr trage im Lauf nichts bei — es hatte dort nur 6 Schiffe
erreicht und keines getroffen. Über **134 Schiffe** gemessen sind es
**7 von 65 Bildern** (`imo<nr>` 6, `mmsi<nr>` 1), also gut ein Zehntel des
Ertrags. Eine Stichprobe, in der ein Weg nur sechsmal drankommt, taugt nicht
für die Aussage „trägt nichts bei".

### Der gemessene Ertrag

Der ganze Lauf gegen **134 echte Schiffe** der Region, echter Speicher auf
frischer Datenbank (`scratchpad/fotolauf.js`):

| | |
|---|---|
| Vorher (laufender Server) | **14** von 134 mit Foto |
| Nachher | **65** von 134 — Faktor 4,6 |
| Wikidata-Abzug | 47 |
| Commons-Kategorie | 10 |
| Flickr `imo<nr>` | 6 |
| Commons MMSI+Name / Flickr `mmsi<nr>` | 1 / 1 |
| Fehlgeschlagene Downloads | **0** (vorher die Hauptverlustquelle) |
| Dauer / Platz | 373 s für 134 Schiffe, 3,2 MB, im Mittel 49 KB je Bild |

Der **Volltextweg findet nichts mehr**, was die Kategorie nicht schon hat
(22 Abrufe, 0 Treffer) — er bleibt als Rückfall drin, kostet aber sichtbar.

### Die Reihenfolge im Fotolauf ist nach Preis sortiert

Drei Stufen: **im Abzug** (ein Download, kein Suchabruf) → **mit IMO, nicht im
Abzug** (bis zu vier Suchabrufe, gute Aussicht) → **ohne IMO** (zwei Abrufe,
magere Aussicht).

Der Grund ist der Rückstand: Beim ersten Lauf auf einem gefüllten Server sind
alle 2 900 Schiffe fällig, die Obergrenze lässt aber nur einen Teil zu.
Gemessen kostet ein Schiff im Schnitt 2,8 s, eines aus dem Abzug knapp eine.
Mit dieser Reihenfolge und einer Obergrenze von 30 kamen **30 von 30
Versuchen zu einem Bild — in 50 Sekunden, mit null Suchabrufen.**

### Der Bericht muss "uebersprungen" von "nie passiert" trennen

Gemeldet am 27. Aug. 2026: Auf dem Server stand bei `abzug` null - der Abzug
schien nicht zu laufen. Er lief. **Die Zaehler leben im Prozess, der Index
lebt in der Datenbank.** Nach einem Neustart ist der Abzug keine 24 Stunden
alt, wird also uebersprungen - und die fruehere Fassung liess `abzug` dann auf
null stehen. Aus dem Status war "frisch und deshalb nicht geholt" nicht von
"nie geholt" zu unterscheiden.

Dasselbe Muster an zwei weiteren Stellen: `wege` und die Fotozahlen wurden
erst **am Ende** eines Laufs uebernommen. Der erste Lauf auf einem gefuellten
Server dauert Minuten, und solange stand dort ueberall null oder 0. Jetzt
kommt `wege` direkt aus den Zaehlern, `fotoVersucht`/`fotoOffen` wachsen
waehrend des Laufs, und `laeuftSeit` sagt, seit wann.

Die Regel dahinter ist dieselbe wie bei der stillen Kappung: **Ein Bericht,
der Untaetigkeit und laufende Arbeit gleich aussehen laesst, ist schlimmer als
gar keiner** - er laedt dazu ein, an einer funktionierenden Anlage zu suchen.

**Und derselbe Fehler ein drittes Mal, am 28. Aug. 2026:** Nach einem Update
meldete `update.sh` "Register: 0 Laeufe, 0 Fotos, 0 offen". Der Container war
zwanzig Sekunden alt, der erste Registerlauf startet nach neunzig. Die Zahl
war richtig und die Frage falsch. Das Skript liest jetzt die **dauerhafte**
Zahl aus der Datenbank - `speicher.bericht()` nennt `fotos` und `fotosEigen` -
und schreibt beim frischen Prozess ausdruecklich hin, dass noch kein Lauf
stattgefunden hat. Merksatz fuer den naechsten Bericht: **Zaehler im Prozess
taugen nicht als Antwort auf "hat es funktioniert?"** - dafuer zaehlt nur,
was auf der Platte steht.

### Eigene Bilder: der einzige Weg mit Trefferquote 1

`POST /v1/foto/<mmsi>`, der reine Bildinhalt im Rumpf. Der Client bietet ihn
im Detailfenster an - Datei waehlen oder ein kopiertes Bild mit Strg+V
einfuegen. **Der Browser holt das Bild, der Proxy legt es nur ab**; auf fremde
Seiten greift kein Automat zu. Fuer Seiten, die automatisiertes Auslesen in
ihrer robots.txt untersagen (VesselFinder nennt `*/ship-photos/*` und
`*/uploads/ship-photo/*` und antwortet Nicht-Browsern mit 403), ist das der
Weg, der bleibt.

Drei Riegel, weil ein Schreibpfad auf einem oeffentlich erreichbaren Dienst
sonst eine offene Tuer ist: Token wie ueberall, eine Groessengrenze, die
**waehrend** des Lesens greift (`FOTO_UPLOAD_MAX`, 6 MB), und ein Blick auf die
ersten Bytes - die Angabe des Absenders zaehlt nicht. Eine als `image/jpeg`
etikettierte HTML-Datei bekommt 415 und wird nicht abgelegt; die Probe dazu
steht in `test/server.test.js`.

**Stapelweise geht es auch**, denn einzeln anklicken ist der eigentliche
Aufwand: Mehrere Dateien lassen sich in den Client ziehen, die Zuordnung
kommt dann aus dem Dateinamen (neun Ziffern am Stueck: `211224140.jpg`,
`211224140 nordsee.png`, `foto_211224140_2.jpg`). Fuer einen ganzen Ordner
gibt es `bilder-hochladen.sh`. Beide melden am Ende, was **nicht** zugeordnet
werden konnte - eine stille Uebergehung liest sich wie "alles geladen".

Ein eigenes Bild traegt `foto_quelle: "eigen"`. Der Fotolauf fasst es nicht
mehr an, weil `fotoFaellig()` jedes Schiff mit `foto_datei` ueberspringt.

**Dabei aufgefallen:** Der Client hat jede Proxyantwort weggeworfen, deren
`register` nicht `"gefunden"` war - also jedes Bild, das ueber die
Commons-Kategorie oder Flickr zu einem Schiff **ohne** Wikidata-Eintrag kam.
Die Datei lag auf dem Server und war trotzdem unsichtbar. Jetzt reicht ein
Foto allein. Und `photoUrl()` hebt fremde Adressen weiter auf https, laesst
die des eigenen Proxys aber in Ruhe: Ein Proxy im eigenen Netz laeuft
womoeglich auf http, und ein umgeschriebener Bildlink scheitert stumm.

### Eine Obergrenze je Lauf, und sie wird gemeldet

`FOTO_MAX_PRO_LAUF` = 300. Ohne sie liefe der erste Lauf über 2 900 Schiffe
mal bis zu fünf Abrufen mal einer Sekunde — Stunden. Was nicht drankam,
bleibt fällig und steht als `fotoOffen` im Bericht. **Eine stille Kappung
liest sich wie „alles abgearbeitet".**

## Lotsenbereiche: die Schleife war der Weg, nicht das Ziel

Gemeldet war ursprünglich: „MMSI 311003300 ist die letzten 10 h Schleifen
gefahren. Oder Lotsenschiffe fahren ständig Schleifen — wie kann ich solche
Schleifen erkennen?" Am Ende stand der zweite Halbsatz: Gezeigt werden
**Lotsenbereiche**, also die Reviere, in denen Lotsenboote ihre Runden drehen.
Der Weg dahin ging über drei Zwischenstufen, und die Zahlen daraus sind die
eigentliche Auskunft (jeweils Gebiete in der ganzen Region):

| Stand | Regel | Gebiete |
|---|---|---|
| 30. Aug., früh | alle Schleifen, Einstufung „gewohnt/auffällig" | **277** |
| 30. Aug., mittags | zusätzlich Landabstand ≥ 2 km | **100** |
| 30. Aug., abends | statt dessen Typfilter (36, 60–69) | **225** |
| **heute** | **nur Lotsenboote, ab 3 Runden je Gebiet** | **31** |

Die Schleifenerkennung selbst ist **unverändert** — sie ist weiterhin das
Werkzeug, nur wird sie jetzt auf 54 Boote statt auf 3 600 Spuren angewandt.
Warum sie so und nicht anders arbeitet, steht darum weiter hier.

### Die drei naheliegenden Kennzahlen versagen, jede auf ihre Art

Nachgemessen am 30. Aug. 2026 an **3 437 echten Spuren** (553 457 Punkte, 8 h):

| Kennzahl | Woran sie scheitert |
|---|---|
| **kumulierte Kursänderung** | Die Elbfähren sammeln in 8 h mehr davon als jedes Lotsenboot — sie wenden ja an jedem Ende. BRESLAU stand mit 7 932° vor PILOTT.WANGEROOG. |
| **Umweg = Weg / Luftlinie** | Wer dort endet, wo er anfing, hat Luftlinie 0 und Umweg unendlich. Gemessen kamen Werte bis **101 606** heraus. |
| **Länglichkeit** (Hauptachsen) | Trennt Fähre und Lotse nicht: BRESLAU **5,92** gegen PILOTVESSEL HANSE **5,44**. Zwei Prozent Abstand ist keine Grenze, das ist ein Zufall. |

**Was trägt, ist die Schleife selbst, nicht eine Note über die ganze Spur.**
`src/anomalie.js` sucht die geschlossene Runde: Die Spur kommt bis auf 400 m
an einen früheren Punkt zurück, hat dabei mindestens 800 m Weg gemacht,
bleibt unter 6 km Durchmesser — und **umschließt Fläche**. Eine Pendelfahrt
kommt auch zurück, umschließt aber fast nichts; sie ist ein Strich. Der
Nebengewinn ist der eigentliche: Eine Schleife hat damit einen **Ort** und
eine **Zeit**, keine Note. Genau das braucht die Karte.

`A / (L²/4π)` — 1,0 wäre ein exakter Kreis, 0 ein Strich. Gemessen: GENTLE
LEADER 0,58 · PILOTT.WANGEROOG 0,46 · WESER PILOT 0,33 gegen die Fähren
KLEINENSIEL 0,22 und OPPELN 0,001. Die Schranke steht bei **0,06** und damit
bewusst tief: Sie soll den entarteten Strich ausschließen, nicht die Fähre.
**Fähre und Lotse kann Geometrie nicht trennen** — WESER PILOT 0,33 gegen
BRESLAU 0,31 wäre wieder eine Zufallsgrenze. Deshalb trennt heute die
Bootsliste, nicht die Form.

### Der Sprungfilter ist keine Vorsicht, sondern eine Messung

GRIETJE (Autotransporter) kam mit **drei** Positionssprüngen auf **443 km Weg
in 8 h** — 30 kn Dauerfahrt für ein Schiff, das vor Anker lag. Nach dem Filter
(alles über 40 kn zwischen zwei Punkten fliegt raus) sind es 2,7 km. Ohne ihn
stand die Spur auf Platz eins jeder Rangliste.

### Wer als Lotsenboot gilt: Typ 50 ODER der Name

Gemessen am Regionsbestand (2 730 Schiffe): **49** Boote führen AIS-Typ 50,
**45** tragen PILOT oder LOTSE im Namen — und **sechs Lotsenboote melden einen
falschen Typ**: HAMBURG PILOT 3 und LOTSE 4 als 99, HAMBURG PILOT 4 und
DANPILOT ALDEBARAN als 90, MEES (PILOTS) als 53.

**Ohne die Namensregel fehlt die zweitstärkste Station der Region** — Elbe bei
Hamburg, 94 Runden in 24 h, ausschließlich von Booten mit falschem Typ. Ein
Fehltreffer bliebe dafür sichtbar: Der Tooltip auf der Karte nennt die
Bootsnamen des Gebiets. Die Regel steht zweimal — als SQL in
`speicher.lotsenMmsis()` und als `istLotse()` in `anomalie.js`, für den heißen
Zustand. Wer eine ändert, muss die andere mitziehen.

### Der Durchgang liest 54 Spuren, nicht 3 600

Das ist der eigentliche Rückbau. Vorher las er **alle** Spuren der Region in
21 Kacheln und siebte hinterher; jetzt fragt er die Lotsenboote einzeln über
`speicher.spur(mmsi, …)` — ein Indexzugriff auf `(mmsi, t)` je Boot. Gemessen
an echten Daten:

| | |
|---|---|
| Lotsendurchgang (54 Spuren, 24 h, volle Auflösung) | **267 ms** |
| Ruhedurchgang (21 Kacheln, alle Schiffe, 24 h) | **1 969 ms** |
| Antwort von `/v1/lotsen` für die ganze Region | **12 ms, 4 KB** |

Kacheln, `setImmediate` je Kachel und der Dopplungsschlüssel
`mmsi:startzeit` entfallen dort ersatzlos — eine Spur kommt genau einmal. Der
**Ruhedurchgang behält sie**, denn seine Grundlinie braucht alle liegenden
Schiffe. Beide haben ihren eigenen Takt (5 min gegen 15 min) und ihren
**eigenen Zeitstempel im Bericht**: Eine gemeinsame Zahl ließe den älteren
aktueller aussehen, als er ist.

Nebenbei fällt die 60-s-Rasterung weg: Bei 54 Spuren ist die volle Auflösung
geschenkt, und sie findet gemessen **mehr** Schleifen (194 statt 147 in
denselben acht Stunden).

### 24 Stunden, und ab drei Runden

**Das Fenster ist 24 h**, nicht 8: Gemessen ergeben 8 h 38 Gebiete aus 194
Runden, 24 h dagegen 57 aus 574 — und erst dabei sind alle bekannten Reviere
vertreten. Eine Station, an der gerade Ruhe war, fehlte im kurzen Fenster ganz.
Gedeckt ist das durch `verdichte()`: Sie löscht nach `ROH_STUNDEN` nur die
**liegenden** Punkte und dünnt die fahrenden auf 60 s aus. Eine Schleife ist
Fahrt, sie überlebt also bis `HISTORIE_TAGE`.

**Ein Gebiet braucht drei Runden.** Gemessen beruhen **19 von 57** Gebieten auf
einer einzigen Runde — ein Boot auf dem Weg, keine Station. Ab drei bleiben
**31**, und die bekannten Reviere liegen mit 15 bis 85 Runden weit darüber:

| Ort | Runden in 24 h | Boote |
|---|---|---|
| 54,245 / 9,614 — Nord-Ostsee-Kanal | 85 | RUESTERBERGEN, BREIHOLZ |
| 53,885 / 9,138 — Elbe bei Glückstadt | 56 | PILOT STEINBURG, PILOT DITHMARSCHEN |
| 53,540 / 9,875 — Elbe bei Hamburg | 54 | HAMBURG PILOT 3/4, LOTSE 4 |
| 53,868 / 7,869 — Jade/Weser-Ansteuerung | 38 | PILOTT.WANGEROOG, WESER PILOT |
| 54,001 / 8,242 — Elbe-Ansteuerung Cuxhaven | 34 | PILOTTENDER DOESE, PILOTVESSEL HANSE |
| 53,335 / 7,175 — Ems | 32 | IZURDIA |
| 54,491 / 10,289 · 54,368 / 10,159 — Kieler Förde | 22 · 18 | PILOT TRAVEMUENDE, PILOT BUELK, PILOT LABOE |
| 54,188 / 12,089 — Warnemünde | 15 | PILOT MUTTLAND |

Erkannt wird im Takt, **verdichtet bei jeder Anfrage** — das erlaubt ein freies
Zeitfenster (`?stunden=`), ohne neu erkennen zu müssen. Mehr als vorgehalten
wird, gibt es nicht; das benutzte Fenster steht deshalb **in der Antwort**,
ebenso die Zahl der bekannten Boote: Ohne sie wäre „0 Gebiete" nicht von
„keine Lotsenliste" zu unterscheiden.

### Zwei Endpunkte, weil es zwei Fragen sind

`/v1/lotsen` sagt, wo gelotst wird; `/v1/anomalien` sagt, wer außerhalb der
gewohnten Liegeplätze stillliegt. Bis dahin lieferte **ein** Endpunkt beides,
und das war richtig, solange beide Ebenen dieselbe Frage beantworteten. Ein
Endpunkt namens „Lotsen", der Stillstand mitliefert, wäre der schlechtere
Handel gewesen. Der Client holt jetzt zwei statt einer Antwort — parallel, je
gemessen im Millisekundenbereich.

**`/v1/lotsen*` musste in den Caddy-Matcher.** `test/caddy.test.js` erzwingt
das, und `update.sh` prüft nach dem Reload mit `caddy-pruefen.sh --heilen` die
**laufende** Konfiguration. Genau dieser Schritt ist hier dreimal schiefgegangen.

### Was der Landfilter gekostet hat, und was davon bleibt

Zwischendurch fielen Fähren über den **Landabstand** weg statt über den Typ:
alles unter 2 km. Das traf sie genauer (454 von 466 Fährenschleifen, ohne dass
irgendwo „Fähre" steht), nahm aber alles andere mit, was dicht unter Land
arbeitet — Schlepper, Lotsenboote, Fischer, Bagger. Gelöscht ist er wieder
(`src/kueste.js`, `karten/kueste.json`, `werkzeug/kueste-bauen.js`); die
Git-Historie hält ihn fest. Die Quellenprüfung war zu teuer, um sie zu
verlieren — wer je wieder eine Küstenlinie braucht, fängt hier an:

| Quelle | Befund |
|---|---|
| **Natural Earth 10m** (10 MB) | Löst die Elbe nicht auf: Hamburger Hafen läge „7,9 km von Land". |
| **OSM simplified land polygons** (24 MB) | Noch schlechter — die Elbe ganz geschluckt (Hamburg „20 km"). |
| **Overpass API** | Nicht erreichbar (Tunnelabbruch bzw. HTTP 500); als *laufende* Abhängigkeit ohnehin fragil. |
| **Aus den AIS-Daten lernen** | Nur **4 %** der Regionszellen waren je befahren; die Regel hätte alles weggeworfen. |
| **OSM `land-polygons-split-4326`** (925 MB, volle Auflösung) | Trifft jede Probe. Daraus wurden einmalig 1 042 Ringe / 395 KB. **Das war die brauchbare.** |

Und drei Fallstricke, jeder einmal zugeschlagen: **OSM führt Flüsse oberhalb
der Küstenlinie als Land** (der bloße Kantenabstand meldete für den Hamburger
Hafen „3,16 km", weil der Punkt *innen* liegt — es braucht einen
Punkt-in-Polygon-Test). **Die Polygone sind kachelweise zerschnitten**, ein
Strahlentest über den beschnittenen Segmentsatz erklärte die offene Nordsee zu
Land; geprüft werden muss je vollständigem Ring. **Douglas-Peucker auf einem
geschlossenen Ring** hat Anfang und Ende am selben Punkt und ließ 535 Ringe
entarten, davon 37 größer als 200 m und der größte 1 373 m — echte Inseln.

**Die Lehre aus vier Anläufen an einer Ebene:** Die Frage war nie „welche
Schiffe filtere ich weg", sondern „welche Auskunft soll die Karte geben". Drei
Filterrunden lang stand die erste Frage vorn, und jede Antwort darauf war
nachweisbar schlechter als die nächste. Erst „zeig mir die Lotsenreviere" hat
die Ebene brauchbar gemacht — und dabei nebenbei den Durchgang von 3 600 auf
54 Spuren gebracht.

## Stillstand: 77 % liegen still, die Auskunft steckt im „außerhalb"

**2 346 von 3 021 Schiffen liegen still.** „Stillstand" allein ist deshalb
keine Auskunft — alles hängt am *außerhalb*.

Eine Ankerplatzliste braucht es dafür nicht: **„Ankerplatz" heißt „hier liegen
viele Schiffe still"**, und das ist in denselben Daten messbar. Die Grundlinie
deckt so Reeden *und* Kais ab, was eine OSM-Ankerplatzliste nicht täte.

Drei Messbefunde haben den Entwurf bestimmt:

1. **Der Umkreis muss 3 km sein.** Das Reede-Nest bei 54,04 °N / 8,13 °O
   besteht aus **sieben** Schiffen — bei 1 km Umkreis hat dort jedes *null*
   Nachbarn und würde einzeln gemeldet, bei 3 km sind es im Median vier.
   Offshore ankert man kilometerweit auseinander, am Kai liegt man
   nebeneinander; ein enger Umkreis kann beides nicht.
2. **Die Grundlinie zählt ALLE Liegenden**, auch Fähren, Festgemachte und
   Dauerlieger. Mein erster Anlauf filterte Grundlinie und Meldung mit
   demselben Sieb — dadurch fielen die Nachbarn weg und es wirkten **mehr**
   Schiffe einsam (136 statt 67 Meldungen). Gerade die Festgemachten
   definieren, wo Liegen normal ist.
3. **Wer die ganze Beobachtung über lag, ist Möbel** — HOEGH ESPERANZA (294 m
   am LNG-Terminal), FINO 1, SEAFOX 4, REIWA am Kai. Das halbiert die
   Kandidaten (501 → 309).

Ergebnis mit ≥ 6 h, 3 km, 2+ anderen: **39 Meldungen in 24 h** für die Deutsche
Bucht — darunter **HMM ALGECIRAS** (400 m, 17 h vor Anker), LADY ALIDA,
IEVOLI COBALT, ORION, SEA INSTALLER, JETTE THERESA.

**Kein Filter auf den Navigationsstatus.** „Festgemacht" auszunehmen spart nur
6 der 39 Meldungen — und der Status, den der Proxy kennt, ist der *heutige*,
nicht der von damals. Ein Filter auf der falschen Zeitscheibe ist es für sechs
Zeilen nicht wert.

### Der Möbel-Vermerk: drei Anläufe, jeder von einer Messung korrigiert

Die Frage dahinter ist immer dieselbe — **haben wir das Schiff ankommen
sehen?** Wer schon dalag, als die Beobachtung begann, hat nirgends angehalten.
Der Weg zur richtigen Formulierung ging über zwei falsche:

**Erster Anlauf: gegen das angefragte Fenster.** An einer Probe mit zwei
Stunden alten Daten wurden daraus **107 statt 39 Meldungen**, und die längsten
hießen alle „22,0 h": Kein Schiff reichte bis an den Fensterrand, also galt
keines als Möbel. Bezug ist seitdem der Rand der **wirklich vorhandenen**
Daten — gemessen über *alle* Spuren des Durchgangs, nicht nur die ruhenden,
sonst wäre bei einem einzigen Lieger dessen eigene Phase die ganze
„Beobachtung".

**Zweiter Anlauf: Anfang UND Ende.** An der laufenden Anlage scheiterte die
Endbedingung in **24 von 92 Fällen** an Meldelücken — ein festgemachtes
Kleinfahrzeug sendet unregelmäßig, sein letzter Punkt lag 24 Minuten vor dem
jüngsten Punkt der Region, und schon galt es nicht mehr als Möbel. Ob es
inzwischen weg ist, ändert an der Frage aber nichts. Also nur der Anfang, mit
**1 h Toleranz** für Meldelücken.

**Dritter Anlauf, und erst der taugt: die Historie fragen.** Die reine
Randregel nahm **HMM ALGECIRAS** mit heraus — 400 m, 16,4 h vor Anker, Beginn
**18 Sekunden** nach dem Datenrand. Wer länger ankert als das Fenster, wird
sonst ununterscheidbar von einem Kai, und das trifft ausgerechnet die
interessantesten Fälle.

Die Antwort liegt in den eigenen Daten: **Die Verdichtung löscht nach
`ROH_STUNDEN` die liegenden Punkte, behält aber die fahrenden.** Wer
angekommen ist, hat davor eine Anfahrt; ein Kai, eine Plattform, eine Hubinsel
haben keine. Für jede Phase am Datenrand wird deshalb einmal je Schiff gefragt,
ob es im Vorlauf (24 h) in Fahrt war — `pos_*` trägt einen Index auf
`(mmsi, t)`, das sind ein paar Dutzend Indexzugriffe je Lauf.

Gemessen an echten Daten: **90 Meldungen** (nur Rand, mit Endbedingung) →
**69** (nur Rand, ohne Endbedingung, HMM ALGECIRAS weg) → **76** (mit der
Historienfrage, HMM ALGECIRAS wieder da).

Und der Vermerk urteilt erst ab dem **Doppelten der Meldeschwelle**: „lag die
ganze Zeit da" heißt bei drei Stunden Beobachtung nichts. Darunter ist niemand
Möbel — im Zweifel lieber melden als still verschweigen.

### Zwei Takte, weil `schritt` die falsche Schraube ist

Der Ruhedurchgang trieb einen Lauf an der laufenden Anlage von **9,3 s auf
56,3 s** — sechsmal so viel, nicht doppelt, wie ich vorher geschätzt hatte.
Der Grund ist nicht der Rasterabstand: **`speicher.spuren()` liest alle Zeilen
des Zeitraums und dünnt erst danach in JS aus**, `ANOMALIE_STILL_SCHRITT_S`
spart also nichts am Lesen. Der Durchgang liest schlicht ein dreimal so langes
Fenster.

Kaputt war nichts — 21 Kacheln mit `setImmediate` dazwischen, rund 1,3 s je
Abschnitt, der AIS-Strom staut sich darin um etwa 43 Nachrichten. Aber 56 s
alle fünf Minuten sind 19 % Dauerlast. Eine Ruhephase dauert mindestens sechs
Stunden, also hat der Durchgang jetzt einen **eigenen Takt**
(`ANOMALIE_STILL_TAKT_MS`, 15 min): `lauf(false)` nur für die Lotsenbereiche,
`lauf(true)` für beides. Der letzte Ruhestand bleibt dabei stehen — ihn zu
leeren hieße, die Ebene zwischen zwei Ruheläufen stumm abzuschalten — und
`ruheGerechnet` sagt im Status, wie alt er ist.

Seit der Lotsendurchgang nur noch 54 Spuren je MMSI liest (**267 ms** gegen
1 969 ms für die Kacheln), ist er ohnehin der billigere von beiden — die
Trennung der Takte bleibt trotzdem richtig, denn die Last kam nie von ihm.

### In dünn abgedeckten Gewässern meldet das Verfahren mehr

Von 92 Meldungen lagen **46 in der Ostsee**, überwiegend Kleinfahrzeuge
(9–12 m). Das ist kein Fehler, sondern eine Eigenschaft: Wo wenige Schiffe
liegen, hat jeder wenige Nachbarn, und die gelernte Grundlinie findet keinen
Liegeplatz. In der Deutschen Bucht stehen dagegen Arbeitsschiffe und
Ankerlieger oben. Wer die Zahl drücken will, dreht an `ANOMALIE_STILL_MIN`
oder am Umkreis — **nicht** an einer Mindestlänge: „Unbekannte mitnehmen" war
die ausdrückliche Entscheidung.

### Die harte Grenze sind 24 Stunden

`speicher.verdichte()` löscht nach `ROH_STUNDEN` **alle Punkte mit
`sog < FAHRT_KN`** — also genau die Liegenden. Eine Grundlinie über mehrere
Tage ist damit nicht rekonstruierbar; nachgemessen reichten zwei von vier
stillliegenden Frachtern genau 24,1 h zurück, die anderen (schwojend, also
zeitweise über 0,5 kn) weiter. `ANOMALIE_STILL_STUNDEN` ist deshalb auf
`ROH_STUNDEN` gedeckelt, und das benutzte Fenster steht **in der Antwort**.

### Was daran noch offen ist

- **AIS-Lücken und Geschwindigkeitssprünge** liegen im selben Rahmen, sind
  aber nicht gebaut.
- **Es gibt keine gelernte Lotsenstation über Tage.** Das Fenster sind 24 h;
  wo seit Jahren gelotst wird, weiß der Proxy nicht. Eine dauerhaft
  mitgeschriebene Stationstabelle wäre der nächste Schritt — beim Stillstand
  ist er getan (die Grundlinie), bei den Lotsenbereichen nicht.
- **Eine Grundlinie über mehrere Tage** bräuchte eine eigene, dauerhaft
  mitgeschriebene Tabelle, weil die Verdichtung die Liegenden wegwirft. Für
  die Lotsenbereiche gilt das nicht — Schleifen sind Fahrt und überleben bis
  `HISTORIE_TAGE`; dort wäre ein längeres Fenster nur eine Zahl.

## Sichern heißt `VACUUM INTO`, nicht `cp`

Die Datenbank läuft im WAL-Modus. Ein `cp ais.db` nimmt genau das nicht mit,
was seit dem letzten Checkpoint geschrieben wurde — und das kann alles sein.
Gemessen an 5 000 frisch geschriebenen Zeilen mit offenem Schreiber: `cp`
liefert **0** Positionen (Hauptdatei 4 KB, WAL 190 KB), `VACUUM INTO` liefert
**5 000**. Die `cp`-Sicherung sieht dabei aus wie eine Sicherung. Genau so
stand es bis zum 27. Aug. 2026 in `DEPLOY.md`.

Deshalb macht `update.sh` vor jedem Update ein `VACUUM INTO`, **öffnet die
Sicherung sofort wieder und zählt sie**, und liest nach dem Neustart den
Bestand gegen den vorherigen. Mitgesichert werden `.env` und
`zugangsdaten.txt`: Beide sind ungetrackt, kein git-Befehl holt sie zurück,
und ohne sie käme niemand mehr an den Dienst — ein neues Token müsste in
jeden Client nachgetragen werden.

## Stammdaten: die vier Bezugspunkte gehoeren mit auf die Leitung

`ais.masse()` rechnet aus `Dimension` die Laenge (A+B) und die Breite (C+D).
Lange hat der Zustand **nur diese Summen** behalten. Der Client braucht aber
A/B/C/D einzeln: Daraus zeichnet er den Rumpf und rechnet Groessenklasse und
Verdraengung (`capacityEstimate`, `shipLengthMeters` in
`aisstream/index.html`).

Gemessen am 27. Aug. 2026 gegen einen echten Proxy, Client mit **leerem**
localStorage: **80 von 80 Markern ohne Rumpf**, obwohl der Proxy die Masse von
10 der sichtbaren Schiffe kannte und die Tabelle daneben „400×61" anzeigte.
Aufgefallen ist es erst beim Leeren des Clientspeichers - vorher kamen die
Bezugspunkte aus dem direkten AIS-Empfang und ueberlebten dort jeden Neuladen.

Deshalb gibt `ais.masseFelder()` jetzt `dimA..dimD` mit, der Zustand fuehrt sie
als Stammdaten, und `alsStamm()` schickt sie als `dim: {A,B,C,D}` - `null`,
wenn nichts bekannt ist, denn `{A:0,B:0,C:0,D:0}` ist truthy und genau daran
ist der Client schon einmal haengengeblieben.

**Der Strom ist die einzige Quelle dafuer**, und `ShipStaticData` kommt je
Schiff nur etwa alle sechs Minuten. Gemessen an einem frisch gestarteten
Proxy: nach 3 min 155 von 2 791 Schiffen mit Masse, nach 13 min 668, danach
nur noch rund 10 je Minute - der Rest sind Fahrzeuge, die selten oder gar
keine Abmessungen senden.

### Was alles aus Msg 5 und Msg 24 kommt

Seit dem 27. Aug. 2026 uebernimmt der Proxy die **ganze** Statik, nicht mehr
nur die Felder, die im Client eine Tabellenspalte fuellen:

| aus dem Strom | Spalte | im Snapshot (2 775 Schiffe, 4 min) |
|---|---|---|
| Msg 5 / 24 | `klasse` (A/B) | 455 |
| `FixType` (Msg 5 und ReportB) | `geraet` | 347 |
| `AisVersion` | `aisVersion` | 337 |
| `Dte` | `dte` (0/1) | 337 |
| `VendorIDName` | `hersteller` | 17 |
| `VenderIDModel` | `modell` | 14 |
| `VenderIDSerial` | `seriennr` | 16 |

**Die Feldnamen stehen so im Feed, samt Tippfehler:** `VenderIDModel` und
`VenderIDSerial`. Zuerst hatte ich `UnitModelCode` und `SerialNumber` aus dem
Schema abgeschrieben - die Spalten blieben leer, 0 von 2 775, ohne dass
irgendetwas fehlgeschlagen waere. Erst ein Blick auf rohe Nachrichten hat es
gezeigt. Der Client trug denselben Fehler seit jeher: Seine Zeile
"Seriennummer" las `partB.SerialNumber` und war deshalb immer leer.

`MothershipMMSI` gibt es im Feed nicht - dafuer wurde die schon gebaute
Spalte wieder entfernt. Eine Spalte, die nie etwas enthaelt, ist Ballast.

**Der Riegel gegen Teil A:** Msg 24 kommt als zwei Nachrichten, und die
jeweils andere Haelfte ist mit Nullen gefuellt und traegt `Valid: false`.
Ohne Pruefung darauf setzte **jede Teil-A-Meldung den Schiffstyp auf 0**
("keine Angabe") und loeschte damit einen bekannten Typ - im Proxy wie im
Client. Beide pruefen jetzt `ReportB.Valid !== false`.

### Deshalb werden sie mitgeschrieben

Bis zum 27. Aug. 2026 standen diese Stammdaten **nur im Arbeitsspeicher** -
die Tabelle `schiff` fuellte allein das Register. Nach jedem Neustart begann
die Lernkurve oben wieder bei null, und ein Client mit leerem
Zwischenspeicher sah in der Zeit lauter Schiffe ohne Masse.

Jetzt haelt `speicher.merkeStamm()` sie fest, gebuendelt im selben Takt wie
die Positionen (`SCHREIB_MS`, gemessen rund 10 Saetze je Sekunde). Drei
Regeln, die dabei zaehlen:

- **Nur was bekannt ist.** `stammSetze()` schreibt jedes uebergebene Feld,
  also wuerde ein `null` aus dem Strom eine Registerangabe *loeschen*. Die
  Probe dazu steht in `test/speicher.test.js`: Wikidata-Laenge 95, danach eine
  AIS-Meldung ohne Masse - die 95 bleibt stehen.
- **Register- und Fotospalten fasst dieser Weg nicht an** (`STAMM_SPALTEN`).
  Ein Schreiber, der beide Seiten bedient, ueberschreibt frueher oder spaeter
  die eine mit dem Nichtwissen der anderen.
- **Beim ersten Sehen wird nachgeschlagen**, nicht nur beim Kaltstart: Der
  Kaltstart erwischt die Schiffe mit frischer Position in der Historie, wer
  danach auftaucht, bekaeme sonst wieder minutenlang nichts. Gefuellt werden
  dabei nur *fehlende* Felder - die eben eingetroffene Meldung ist juenger als
  die Datenbank.

Gemessen an einem echten Proxy mit echtem Strom: 250 Schiffe mit Abmessungen
in drei Minuten gelernt, sauber beendet, mit derselben Datenbank neu
gestartet - **25 Sekunden spaeter kannte er 286 davon wieder** (die 250 aus
der Datenbank plus die inzwischen frisch gemeldeten) und 2 475 Namen. Ohne
Persistenz waeren es die zwei, drei aus 25 Sekunden Strom gewesen.

## `/v1/status` liegt hinter der Tokenpruefung — auch fuer eigene Abfragen

`erlaubt()` in `src/server.js` steht **vor** dem Pfadverteiler; `/v1/status`
ist nicht ausgenommen. Ist `AIS_ZUGANG` gesetzt, antwortet der Status ohne
Token mit 401 — auch von `127.0.0.1` aus, auch aus dem eigenen Container.

Das hat zwei eigene Prüfungen still ausgehebelt, bis es am 27. Aug. 2026
auffiel: Der **Healthcheck** im `Dockerfile` war seit dem Setzen des Tokens
dauerhaft rot (Container `unhealthy`, obwohl alles lief), und die Warteschleife
in `update.sh` meldete nach 180 s „der Dienst meldet keine Schiffe", während
der Client einwandfrei bediente. Nachgemessen gegen den echten `Server`:
ohne Token HTTP 401, mit `?token=` HTTP 200.

Beide holen das Token jetzt aus `process.env.AIS_ZUGANG` **im Container** —
also aus derselben Variablen, aus der der Server sein `konfig.ZUGANG` bildet.
Sie können damit nicht auseinanderlaufen, und das Token steht auf keiner
Befehlszeile. Wer eine neue Prüfung gegen `/v1/status` baut, muss dasselbe tun.

## Der Fehler, der zweimal passiert ist: entpackt statt Leitung messen

Beim Stream und beim Snapshot habe ich zuerst die **entpackte** Größe
gezählt und daraus geschlossen, der Weg sei teuer:

- Stream: 20,4 KB/s entpackt gegen 7,1 KB/s auf der Leitung. Hätte fast die
  Architektur gedreht.
- Snapshot: 938 KB entpackt gegen **136 KB auf der Leitung**. Die Probe im
  Livetest war rot, obwohl der Code stimmte.

Deshalb holt `netz.js` seine Daten **nicht** mit `fetch()`, sondern mit
`https.get` plus eigenem `zlib` — dort lässt sich am Socket zählen. `fetch()`
liefert über `arrayBuffer()` nur die entpackten Bytes, und `content-length`
fehlt bei gestückelter Übertragung. Der Status meldet jetzt **beide** Zahlen
(`letzteBytes`, `letzteBytesEntpackt`), damit niemand sie wieder verwechselt.

## Geteilte Fachlogik — ehrlicher als im Entwurf behauptet

Die Node-Entscheidung im Entwurf stand auf dem Argument „die AIS-Fachlogik
des Clients ist direkt übernehmbar". Beim Bauen zeigte sich: **Der Anteil ist
kleiner als behauptet.** Typtabellen, MMSI/MID-Dekodierung und
ETA-Beschriftung braucht der Proxy gar nicht — er speichert Rohwerte und
lässt den Client beschriften.

Geteilt werden muss nur, was den **gespeicherten** Wert verändert: die
Sentinels (SOG 1023, COG 3600, Heading 511, `Dimension` aus lauter Nullen)
und das Säubern der `@`-aufgefüllten Texte. Genau die stehen in `src/ais.js`,
mit Verweis auf die Fundstelle im Client (`aisstream/index.html` ab Z. 2556).
Wer dort etwas ändert, muss es hier mitziehen.

Node bleibt trotzdem richtig — wegen des WebSocket-Fan-outs und weil diese
Sentinels sonst doch zweimal existierten. Aber das Argument trägt weniger
weit, als es im Entwurf klang.

## Was noch offen ist

- **Hält die Rate über 24 Stunden?** Gemessen wurden Minuten an einem
  Donnerstagvormittag: 37,5 msg/s, Spitze 38,4. Ob Verkehrsspitzen an die 50
  stoßen, zeigt erst ein Tageslauf mit `strom.rateSpitze`. Das ist die
  einzige Zahl, die den Entwurf noch kippen könnte.
- ~~**Docker ist ungeprüft.**~~ Erledigt: läuft seit dem 27. Aug. 2026 auf
  einem Ubuntu-VPS bei clouding.io, `/v1/status` antwortet mit 200. Die
  beiden Fehler, die dabei auftraten, stecken in `deploy.sh` und `DEPLOY.md`
  (bcrypt-`$` in der `.env`, `curl | bash` verschluckt den Skriptrest).
- **Die Animation fehlt noch.** Seit dem 27. Aug. 2026 holt der Client aus
  `/v1/replay` und `/v1/track` die letzten sechs Stunden und zeichnet sie als
  verblassende Spur (gemessen 144 KB auf der Leitung für einen 90-km-
  Ausschnitt, 196 Spuren, 13 202 Punkte). Das *Abspielen* über die Zeit —
  wofür `von`/`bis`/`schritt` eigentlich gedacht sind — gibt es noch nicht.
- **Kein Löschen von Fotos.** Fällt ein Schiff dauerhaft aus der Region,
  bleibt sein Bild liegen. Bei ein paar tausend verkleinerten Bildern ist das
  vertretbar, aber irgendwann will es eine Pflege.
