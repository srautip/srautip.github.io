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
