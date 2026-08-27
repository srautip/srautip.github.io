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

Zwei Fristen, wie im Client: Lief die Suche **ohne IMO**, ist sie
unvollständig (`foto_quelle = "teil"`, `FOTO_TEIL_MS` = 1 h) — die IMO kann
jede Minute per `ShipStaticData` eintreffen. Mit IMO war es eine vollständige
Suche, die hält `FOTO_FEHL_MS` = 7 Tage.

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

### Eine Obergrenze je Lauf, und sie wird gemeldet

`FOTO_MAX_PRO_LAUF` = 300. Ohne sie liefe der erste Lauf über 2 900 Schiffe
mal bis zu fünf Abrufen mal einer Sekunde — Stunden. Was nicht drankam,
bleibt fällig und steht als `fotoOffen` im Bericht. **Eine stille Kappung
liest sich wie „alles abgearbeitet".**

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
keine Abmessungen senden. Persistiert wird davon **nichts**: Die Tabelle
`schiff` fuellt nur das Register (Wikidata/Digitraffic). Nach einem Neustart
faengt diese Kurve also wieder bei null an.

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
