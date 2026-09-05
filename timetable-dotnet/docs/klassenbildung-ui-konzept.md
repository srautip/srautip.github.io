# UI-Konzept: Klassenbildungs-Viewer als interaktiver Entscheidungsraum

Aufsetzend auf `docs/klassenbildung-konzept.md` (v.a. Abschnitte 9
"Fixierungen und UI-Interaktion" und 11 "Ampel-Visualisierung") und dem
K5-Viewer (`Templates/klassenbildung.html`). Ausloeser sind drei
beobachtete Schwaechen des heutigen Standes:

1. **Gruppen sind unsichtbar:** Karten tragen anonyme Ampel-Chips -
   WELCHE Gruppe ein Chip meint, steht nur im Tooltip; die Zuordnung
   Kaertchen <-> Gruppe erschliesst sich nur ueber Einzel-Klicks in der
   Gruppen-Tabelle (ein Highlight zur Zeit).
2. **Prioritaeten sind Zahlen in einer Tabellenspalte**, nicht
   Struktur: der Blick "was ist kritisch, was nur wenn moeglich?"
   erfordert Lesen statt Sehen.
3. **Es gibt keinen Weg VORWAERTS:** der Viewer zeigt Varianten, aber
   der eigentliche Prozess - schrittweise fixieren, neu rechnen, Rest
   diskutieren - findet komplett ausserhalb (YAML editieren) statt.

Leitbild: Der Viewer wird vom Ergebnis-BETRACHTER zum
**Arbeitsbrett einer Klassenbildungs-Konferenz**: Gruppen sind
erstklassige, sichtbare Objekte; die Prioritaet strukturiert den
Bildschirm; und jeder Schritt des Trichters "Konsens -> Gruppen ->
Einzelfaelle -> Re-Solve" hat eine direkte Aktion im UI.

---

## 1. Gruppen sichtbar machen (Kernproblem)

### 1.1 Gruppen-Identitaet: Farbe + Kuerzel, ueberall dieselbe

Jede Gruppe erhaelt beim Laden deterministisch:

- eine **Gruppenfarbe** aus einer kategorialen Palette (12-16 gut
  unterscheidbare, entsaettigte Toene; bei mehr Gruppen wiederholt sich
  die Palette mit anderem Muster/Rahmen). Die Farbe ist die IDENTITAET
  der Gruppe - sie ist strikt getrennt von der Ampel-Semantik
  (gruen/gelb/rot bleibt exklusiv fuer Erfuellungsstatus).
- ein **Kuerzel** (2-3 Zeichen), aus der Id abgeleitet oder im YAML
  optional vorgebbar (`gruppen[].kuerzel: SOZ`): ZWI, K1..K12, NOR,
  SOZ, SBG. Das Kuerzel macht Badges lesbar, wo Farbe allein nicht
  reicht (Farbfehlsichtigkeit, Druck).

### 1.2 Karten tragen Gruppen-Badges statt anonymer Chips

Der heutige Chip (nur Ampelfarbe + Symbol) wird zum **Gruppen-Badge**:

```
+------------------------------------------+
| ● S017   [ZWI ✓] [K3 ✓] [SOZ !]   [W+ ✗] |
+------------------------------------------+
   ^Rand = Worst-of (unveraendert)
```

- Badge-Grundfarbe = GRUPPENfarbe, kleines Statuszeichen (✓/!/✗) in
  Ampelfarbe daran - beide Informationen auf einen Blick, ohne
  Tooltip. Tooltip weiterhin mit Klartext.
- Wuensche als eigene Badge-Art `W+` (zusammen) / `W-` (getrennt) in
  neutraler Farbe; Tooltip nennt den Partner. Balance-Beitraege
  NICHT als Badge (das waere Rauschen - fast jedes Kind traegt
  Geschlecht), sondern nur als Ampel-Einfluss + im Detail-Popover.
- Klick auf eine Karte oeffnet ein **Detail-Popover**: alle Kriterien
  des Kindes in Klartext (auch Balance), Partner-Wuensche, aktuelle
  Klasse in allen Varianten, Fixier-Aktionen (Abschnitt 3).

### 1.3 Buendelungsgruppen als sichtbare Stapel im Board

Innerhalb einer Klassen-Spalte werden Karten derselben
Buendelungsgruppe **zusammengefasst dargestellt**: ein dezenter
Gruppenrahmen in der Gruppenfarbe mit Kopfzeile
`[K3] Kita Pusteblume - 3/4 hier` (x von y Mitgliedern in dieser
Klasse). Ein zerrissenes Buendel ist damit SOFORT sichtbar: derselbe
farbige Rahmen taucht in zwei Spalten auf, beide Koepfe zeigen "2/4"
und "2/4" mit rotem Status. Kinder in mehreren Buendelungen werden dem
Stapel ihrer hoechstprioren Gruppe zugeordnet, die uebrigen bleiben
Badges. Verteilungsgruppen bilden KEINE Stapel (sie sind ja gestreut) -
sie leben als Badges + im Panel.

### 1.4 Kreuz-Hervorhebung statt Ein-Gruppen-Klick

- **Hover** ueber ein Badge, einen Stapel-Kopf oder eine Panel-Zeile:
  alle Mitglieder der Gruppe leuchten in allen Spalten auf (heutiger
  blauer Ring, aber in der Gruppenfarbe), die Panel-Zeile ebenso -
  fluechtige Erkundung ohne Klick-Zustand.
- **Klick** pinnt die Hervorhebung (heutiges Verhalten), zusaetzlich
  sind MEHRERE Gruppen gleichzeitig pinnbar (z.B. zwei kollidierende
  Gruppen nebeneinander betrachten); jede in ihrer eigenen Farbe.
- Hover ueber eine KARTE hebt umgekehrt ihre Gruppen im Panel hervor.

## 2. Prioritaet als Bildschirm-Struktur

Die Gruppen-Tabelle wird zum **Gruppen-Panel als linke Seitenleiste**
(einklappbar), gegliedert nach den drei benannten Stufen aus Konzept
9.2 - nicht nach Zahlen:

```
KRITISCH (Prio 3)
  [SOZ] Verteilung max 1     ✗ 1 Ueberlauf   [Verteilung: Mini-Balken]
  [ZWI] Buendelung (hart)    fixiert 1a      [->]
WICHTIG (Prio 2)
  [K1] Kita Sonnenblume      ✓ alle in 1c
  ...
WENN MOEGLICH (Prio 1)
  [NOR] Wohngebiet Nordstadt ! auf 2 Klassen
Wuensche: 31 zusammen (31 ✓), 3 getrennt (3 ✓)   [Liste]
```

- Je Zeile: Farbfeld+Kuerzel, Typ-Symbol (⧉ Buendelung / ⇌
  Verteilung), Modus (Schloss-Icon fuer hart), Status in Ampel,
  **Ist-Verteilung als Mini-Balken** ueber die Klassen (ein Segment je
  Klasse, Hoehe = Mitgliederzahl, Kappen-/Ziellinie bei Verteilung
  und Balance) - die heutige Textspalte "1a×2, 1c×1" als Bild.
- Sortierung innerhalb der Stufe: verletzte zuerst, dann knappe, dann
  erfuellte - das Panel IST damit die Tagesordnung der Konferenz.
- Balance-Kriterien erscheinen als eigener Panel-Block mit denselben
  Mini-Balken (Ziel +/- Toleranz als Band) statt nur im Spaltenkopf.
- Filter oben im Panel: nach Stufe, nach Status, "nur offene".

## 3. Der Weg vorwaerts: Fixieren als UI-Aktion

### 3.1 Der Trichter als sichtbarer Prozess

Eine schlanke **Fortschrittsleiste** ueber dem Board macht den
empfohlenen Ablauf (Konzept 11.3/11.4) explizit und zaehlbar:

```
[1] Basis waehlen -> [2] Konsens fixieren (33) -> [3] Gruppen fixieren -> [4] Einzelfaelle (9 rot / 35 gelb) -> [5] Export & Re-Solve
     Variante 2          78 von 100 Kindern fixiert                             Rest: 22
```

Die Schritte sind Angebote, kein Zwang - jede Aktion ist jederzeit
moeglich. Aber die Leiste beantwortet permanent die Konferenz-Frage
"wie weit sind wir, was fehlt noch?".

### 3.2 Fixier-Aktionen (Mapping auf die F-Taxonomie des Konzepts)

| UI-Aktion | Wo | Konzept |
|---|---|---|
| Pin-Toggle auf der Karte ("S017 in 1b fixieren") | Karte / Popover | F1 |
| "Nicht in diese Klasse" | Karten-Popover | F2 |
| Wunsch hart stellen (zusammen/getrennt) | Karten-Popover / Wunschliste | F3 |
| **"Gruppe hier fixieren"** (alle Mitglieder auf diese Klasse) | Stapel-Kopf / Panel-Zeile | F4 |
| "Gruppe schliessen, Klasse offen lassen" (Buendelung auf hart) | Panel-Zeile | F5 |
| "Klasse einfrieren" (alle aktuellen Karten pinnen) | Klassen-Spaltenkopf | F6 |
| **"Konsens-Kern fixieren (33)"** Bulk-Button | Fortschrittsleiste | 11.3 Konsens-Fixierung |
| "Alle unkritischen fixieren" (gruen+grau, gefiltert) | Fortschrittsleiste | 11.3 Bulk |

Gepinnte Karten zeigen ein Pin-Symbol, werden leicht gedimmt und sind
per Filter "nur offene Kinder" ausblendbar - die Diskussion
konzentriert sich sichtbar auf den Rest. Jeder Pin ist einzeln und
gesammelt (Bulk-Herkunft) wieder loesbar.

### 3.3 Ehrliche Grenze des self-contained Viewers - und der Loop

Der Viewer bleibt eine Doppelklick-Datei ohne Server. Der
Re-Solve-Loop wird deshalb als **Export-Schritt** gestaltet:

- Panel "Fixierungen (n)" sammelt alle Pins und rendert sie live als
  fertigen **`fixierungen:`-YAML-Block** (mit Kommentar je Pin:
  Herkunft "Konsens-Bulk" / "manuell" / "Gruppe SOZ") zum Kopieren in
  `input/klassenbildung.yaml`. Daneben die Kommandozeile zum
  erneuten Rechnen: `dotnet run --project SchoolTestRunner -- klassen
  <schule>`. Gruppen-/Wunsch-Haertungen (F3/F5) erscheinen analog als
  `modus: hard`-Diff-Hinweis.
- Pins ueberleben Reloads per localStorage (reiner Komfort, mit
  try/catch-Fallback - file:// ist unzuverlaessig); massgeblich ist
  immer die YAML.
- **Ausbaustufe** (wenn ein interaktiver Kanal gewuenscht ist): dieselbe
  Seite als Artifact mit Speicher-Faehigkeit oder gegen einen lokalen
  Mini-Serverprozess, der `klassen` direkt anstoesst - dann wird aus
  "Export + CLI" ein "Neu optimieren"-Button mit Diff-Vorschau
  (Konzept 9.2 Phase 4). Das UI-Konzept ist so geschnitten, dass
  dieser Schritt NUR den Export-Teil ersetzt.

### 3.4 Was-waere-wenn: Live-Bewertung bei Drag & Drop

Karten sind zwischen Spalten **verschiebbar** (Drag & Drop; jede
Verschiebung erzeugt automatisch einen F1-Pin, sichtbar und loesbar).
Nach jeder Verschiebung laeuft die **reine Bewertung im Browser**
(JS-Duplikat von `KlassenbildungQuality.Bewerte` - dieselbe bewusste,
kommentierte Formel-Duplikation wie der Gewichts-Regler der
Stundentafel; der Verletzungsreport des naechsten CLI-Laufs bleibt die
unabhaengige Ground Truth): Chips, Panel-Status, Mini-Balken,
Ampel-Zaehler und Klassen-Kapazitaet aktualisieren sich sofort -
inklusive Warnung im Stil des Konzepts ("damit sind 2 SOZ-Kinder in
1c"). Kapazitaetsverletzungen (Klasse voll) blockieren den Drop nicht,
faerben aber den Spaltenkopf rot - harte Grundconstraints prueft
endgueltig der Solver.

## 4. Weitere gezielte Verbesserungen

- **Klassen-Spaltenkopf**: Kapazitaetsanzeige `24 / 22-26` als
  schmaler Fuellbalken (rot ausserhalb des Korridors), darunter die
  Balance-Mini-Balken - der heutige Textblock wird scanbar.
- **Varianten als Tabs/Karten** statt Tabelle: drei kompakte
  Scorecard-Kacheln (Zielwert, Ampelsumme, Diff) oberhalb; der
  Vergleichsmodus bleibt. "Variante als Arbeitsstand uebernehmen"
  (Schritt 1 der Leiste) macht die Basis-Entscheidung explizit -
  danach zaehlt der Fortschritt relativ zu dieser Basis.
- **Wunsch-Sicht**: verletzte Wuensche als kurze Liste im Panel
  ("S051 + S087 getrennt - Grund: Kappe SOZ"); Hover zeigt beide
  Karten. Wunsch-Linien im Board bewusst NICHT (bei 30+ Wuenschen
  wird jedes Liniengeflecht unlesbar).
- **Zugaenglichkeit**: alle Zustaende doppelt kodiert (Farbe +
  Symbol/Text), Fokus-Reihenfolge Panel -> Board, Aktionen per
  Tastatur (Pin = Enter auf fokussierter Karte), Tooltips auch als
  Popover erreichbar.

## 5. Umsetzungsschritte

| Schritt | Inhalt | Aufwand | Wirkung |
|---|---|---|---|
| U1 | Gruppenfarben + Kuerzel, Badges statt anonymer Chips, Hover-/Mehrfach-Kreuz-Highlight, Panel-Seitenleiste nach Prio-Stufen mit Status-Sortierung | mittel | loest Kernproblem "welche Karte gehoert wozu" |
| U2 | Buendelungs-Stapel im Board, Klassen-Kopf mit Kapazitaets-/Balance-Mini-Balken, Varianten-Kacheln + "als Arbeitsstand uebernehmen" | mittel | Struktur sichtbar, zerrissene Gruppen springen ins Auge |
| U3 | Pins (F1/F2/F4/F6 + Bulk/Konsens), Fortschrittsleiste, Fixierungen-Panel mit YAML-Export, localStorage-Komfort | mittel | der Trichter wird bedienbar; Loop via Export + CLI |
| U4 | JS-Bewertungslauf (Duplikat von Bewerte) + Drag & Drop mit Live-Ampel und Warnungen; F3/F5-Haertungen im Export | mittel-gross | Was-waere-wenn ohne Solver-Lauf (Konzept 9.2 Phase 3) |
| U5 | Ausbaustufe: "Neu optimieren"-Button (Artifact-Capability oder lokaler Serverprozess), Diff-Vorschau, Konfliktkern-Dialog (setzt K6-Assumptions voraus) | gross | schliesst den Loop ohne YAML-Umweg (Konzept 9.2 Phasen 4-5) |

U1-U3 sind reine Viewer-/Exporter-Arbeit im bestehenden
self-contained-Rahmen (plus Mini-Erweiterungen des JSON-Exports:
`gruppen[].kuerzel`, Wunsch-Echo mit Status). U4 dupliziert bewusst
Zaehllogik im JS (etabliertes Muster). U5 ist die einzige Stufe, die
den Rahmen erweitert - alles davor funktioniert per Doppelklick.

Verifikation je Schritt wie gehabt: Headless-Chromium-Interaktionstests
gegen unabhaengige Python-Nachrechnungen (Badge-Zaehlung,
Highlight-Mengen, Pin-Export-Inhalt, Live-Bewertung == Bewerte).

## Umsetzungsstand

- **U1-U3 umgesetzt** (Template-Redesign von klassenbildung.html):
  Gruppenfarben + deterministische Kuerzel (Klassenbildung.
  GruppenKuerzel, YAML-Feld `gruppen[].kuerzel` optional, Test),
  Gruppen-/Wunsch-Badges mit Statuszeichen, Buendelungs-Stapel mit
  x/y-Kopf, Hover-/Mehrfach-Kreuz-Highlight, Panel-Seitenleiste nach
  Prio-Stufen mit Status-Sortierung und Mini-Balken (Gruppen, Balance,
  Wuensche inkl. Verletzten-Liste), Varianten-Kacheln mit
  "Arbeitsstand"-Uebernahme, Kapazitaetsbalken je Spalte,
  Fortschrittsleiste mit Konsens-/Unkritisch-Bulk, Pins F1/F2/F4/F6
  (Karte, Popover, Panel, Spaltenkopf), Fixierungen-Panel mit
  YAML-Export (Bestand + Herkunfts-Kommentare) und
  localStorage-Komfort. JSON dafuer um gruppen[].kuerzel, wuensche-
  und fixierungen-Echo erweitert. Chromium-Interaktionstest: 116
  Badges, 15 Stapel, Sektionen kritisch/wichtig/wenn moeglich/
  Balance/Wuensche, Konsens-Bulk 27 -> Einzelpin 28 -> Gruppen-Pin 31,
  YAML-Export 31 Eintraege inkl. Bestand, Filter "nur offene"
  100 -> 69 Karten (exakt 100 - 31).
- **U4 umgesetzt** (Live-Bewertung + Drag & Drop): `bewerte()` als
  bewusstes, kommentiertes JS-Duplikat von `KlassenbildungQuality.
  Bewerte` (Status UND Chip-Texte zeichengleich; Ground Truth bleibt
  der CLI-Lauf). Die "Arbeits-Sicht" ueberlagert die Basis-Variante
  mit den Pins - ein Pin auf eine andere Klasse IST die Verschiebung,
  Pin loesen legt die Karte zurueck. Karten sind per Drag & Drop
  zwischen Spalten verschiebbar (Drop erzeugt F1-Pin mit Herkunft
  "verschoben", Karte gestrichelt markiert); Chips, Panel, Mini-Balken,
  Ampel-Zaehler und Kapazitaetsbalken rechnen sofort nach, ein
  Warnbanner nennt verschlechterte Regeln im Klartext ("damit:
  Verteilung G_sozialverhalten: 2/1 in dieser Klasse - Kappe
  ueberschritten") plus Korridor-Hinweis; Kapazitaetsverletzung
  blockiert den Drop nicht, faerbt aber den Spaltenkopf rot (Konzept
  3.4). F3/F5-Haertungen (Wunsch/Gruppe auf hart, Popover bzw.
  Panel/Wunschliste) sind reine Export-Direktiven und erscheinen als
  `modus: hard`-Diff-Hinweis im Fixierungen-Block. JSON dafuer um den
  Top-Level-Block `balance` (regel_id, ziel, toleranz, modus, prio,
  treffer-IDs) erweitert - aeltere JSONs ohne den Block laufen im
  reinen Anzeige-Modus weiter (kein Drag & Drop). Verifikation:
  Chromium-Interaktionstest mit echten DragEvents gegen unabhaengige
  Python-Nachrechnung - Live-Bewertung == Bewerte fuer alle 3
  Varianten (Chips + Verletzungen zeichengleich), Ampel nach
  Verschiebung exakt wie vorhergesagt, Warnbanner/Kapazitaets-Rot/
  Haertungs-Export/Pin-Ruecknahme geprueft. Offen: U5 (Re-Solve-Loop) -
  der Kanal dafuer ist inzwischen konkretisiert: die geplante WPF-GUI
  hostet das Board in WebView2 und ersetzt den Export+CLI-Umweg durch
  eine "Neu rechnen"-Bridge (docs/gui-ui-konzept.md Abschnitt 4).
- **U6 umgesetzt - Board-Verdichtung** (Nutzerauftrag 05.09.2026:
  "kompakte Uebersicht, moeglichst viele Informationen; kompaktere
  Varianten; bessere Zuordnung rot/gelb/gruen/frei zu Kindern und
  Regeln"). Befund am Vollbild: drei fast gleiche Varianten-Kacheln
  mit den Unterschieden als Fliesstext und einem getrennten
  Vergleichs-Dropdown; die Ampel dreimal verschieden kodiert (Rand,
  blasses Zeichen AUF der Gruppenfarbe, Text im Panel); Hoehe verloren
  an `W+ ✓`-Badges fuer erfuellte Wuensche, zweizeilige Stapelkoepfe
  mit vollem Namen und drei Balance-Textzeilen je Spaltenkopf.
  Entscheidungen:
  - **Scorecard-Leiste**: eine Zeile je Variante mit Basis (Radio),
    Vergleich (Haken - ersetzt das Dropdown), Zielwert, Diff,
    Ampelbalken (vier Segmente) und Verstoessen je Prio (aus
    `verletzungen[].prio`, vorher nirgends sichtbar); darunter eine
    Zeile "Arbeitsstand", sobald Pins die Basis veraendern.
  - **Ein Statuszeichen ueberall** (`.status-punkt`, Vollton mit
    ✓/!/✗): vor der Kind-Id (Worst-of), im Badge als kleiner Punkt
    statt blassem Zeichen, in der Panelzeile, im Stapelkopf und in
    einer Legende. "frei" heisst nach aussen **"ohne Regel"** und ist
    ein hohler Kreis - kein Status, sondern dessen Abwesenheit; der
    JS-Schluessel `frei` bleibt. Mini-Balken im Panel in Ampelfarbe
    (zerrissene Buendelung: alle belegten Segmente rot).
  - **Kreuzbezug in beide Richtungen**: Zeigen auf eine Gruppe dimmt
    die Nicht-Mitglieder im Board; Zeigen auf eine Karte hebt ihre
    Panelzeilen hervor.
  - **Erfuellte Wuensche eines Kindes sind EIN Badge** `W n`;
    verletzte bleiben einzeln und rot. Schalter "alle Wuensche
    einzeln" stellt die Einzelansicht her.
  - **Stapel einklappen**: vollstaendig in der Spalte UND ohne
    Diskussionsbedarf (alle Mitglieder gruen/ohne Regel) - dann Karten
    nebeneinander mit Punkt und Id, Kopf `[Kuerzel] ● x/y`, Klick
    klappt auf. Die Karten bleiben echte `.karte` (fokussierbar,
    pinnbar, ziehbar); Stapel mit gelb/rot sind nie zu.
  - **Spaltenkopf**: Balance je Regel als Balken mit Zielband
    (Ist-Balken endet im Band = erfuellt, am Rand = knapp, ausserhalb
    = verletzt; Zahlen im Tooltip). **Panel** 21 -> 17 rem, Namen ohne
    `G_`-Praefix, Pin/Schloss erst bei Hover/Fokus.
  - **Gelb ist Gelb** (Nutzerrueckmeldung nach der ersten Sicht): das
    bisherige Braun `#8a5a00` fuer "knapp" las sich wie ein Alarm. Die
    Warn-Familie ist jetzt gelb-gruen - Text olive `#6f7400`, Rand
    `#d6d45c`, Flaeche `#f9f9e0` - und fuer FLAECHEN (Punkt, Balken,
    Kartenkante) gibt es die neue Rolle `--farbe-warn-marke` `#d9cf00`
    mit dunklem Zeichen darauf: ein echtes Gelb als Text waere
    unlesbar. Kanon in `design-tokens.css`, beide Vorlagen-Kopien und
    `Tokens.xaml` (per `tools/design-einsetzen.pl`).
  - **Zeigen hebt bei JEDER Regel hervor**: auch Balance-Zeilen
    (Treffer der Regel) und verletzte Wuensche (das Paar) dimmen die
    uebrigen Karten - dieselbe Mechanik wie bei Gruppen
    (`hoverKinder`), in Akzentfarbe statt Gruppenfarbe.
  - **Prio-Stufen ohne Nummer**: die Sektionen heissen KRITISCH /
    WICHTIG / WENN MOEGLICH. Die Zahl im YAML (3 = hoechste, das
    Gewicht im Solver) und die Lesart "kritisch = Nummer 1"
    widersprechen sich; beide nebeneinander zu zeigen verwirrt. Eine
    Umkehr der YAML-Semantik waere ein Eingriff in Kern, YAML-Schema,
    GUI-Masken und Beispiele - bewusst nicht Teil von U6.
  - Kein Eingriff in `bewerte()` und die Chip-Texte (Zeichengleichheit
    zum Kern bleibt getestet).
  - **Kopfbereich als ein Kasten** (Schritt D): Fortschritt,
    Scorecard und Filterzeile sind drei Zeilen desselben Rahmens, das
    Warnbanner steht darunter. Die Fortschrittsleiste ist ein kurzer
    Balken (fixiert gegen offen - die Ampel steht in der Scorecard)
    mit Zaehler; rechts ALLE Aktionen des Boards: Konsens-Kern
    fixieren, alle unkritischen fixieren, alle Pins loesen und im
    GUI-Betrieb "Neu rechnen" und "Freigeben" - die sassen bisher
    unten im Fixierungen-Block, wo man sie nach dem Scrollen erst
    suchen musste. Die Schrittfolge "[1] ... [5]" als Text entfaellt -
    sie war Erklaerung, keine Standortbestimmung. Die Ueberschreibung der Basis-Regel `#controls`
    laeuft ueber `#controls.kopf-fuss`, weil der Waechter eine zweite
    `#controls`-Deklaration verbietet. Kopf von ~200 auf ~150 px.
  - **Fehler behoben: Haertung ohne Rueckweg.** Das Schloss (F3/F5)
    gab es nur an weichen Regeln; die Bruecke setzte beim Neurechnen
    `modus: hard` ins Projekt - danach war die Regel im Viewer nicht
    mehr zu loesen. Die Direktive traegt jetzt eine Richtung
    (`'hard'`/`'soft'`, `true` aus aelteren Zustaenden heisst hard),
    das Schloss steht an jeder Regel (Panelzeile, Wunschliste,
    Karten-Popover), der Export schreibt `modus: hard` oder `soft`, und
    `Bruecke.WendeHaertungenAn` wendet beide Richtungen an. Tests:
    `BrueckeTests.AufweichungSetztModusAufSoft`,
    `KlassenbildungBoardTests.HarteRegelnLassenSichAufweichen`.
  - **Fehler behoben: Freigabe zaehlte erfuellte Regeln als
    Abweichung.** Der Export fuehrt in `verletzungen` JEDE Regel mit
    ihrem Mass (Verifier-Prinzip), erfuellte mit `mass: 0`;
    `Freigabe.FuelleKlassenbildung` listete alle 52 als "nicht
    erfuellt" - und erzwang damit eine Begruendung fuer 50 Regeln, die
    gehalten haben. Jetzt zaehlt nur `mass > 0`, die Zeile nennt Prio
    in Worten und das Mass (`FreigabeTests.ErfuellteRegelnSindKeineAbweichung`).
  - **Undo/Redo** (Nutzerwunsch 05.09.2026) im Kopfkasten links, auch
    Strg+Z / Strg+Y. Ein Schritt ist der Zustand nach einer
    Bedienaktion - Pin, Verschiebung, hart/weich, Bulk-Fixierung (58
    Pins = EIN Schritt, weil der Schritt in `render()` erkannt wird,
    nicht je Pin). Im Doppelklick-Betrieb fuehrt die Seite den Verlauf
    selbst. Im GUI-Betrieb fuehrt ihn der Host (`Infrastruktur/
    Verlauf.vb`): "Neu rechnen" erzeugt einen neuen Stand und laedt die
    Seite neu, also traegt jeder Schritt Stand-Id, Board-Zustand und
    einen Schnappschuss der Eingaben, die das Neurechnen veraendert
    (Fixierungen, Modus der Regeln). Undo/Redo senden `undo`/`redo`
    an den Host, der stellt alle drei wieder her und zeigt den Stand
    des Schritts sofort an - denselben mit altem Zustand neu geladen,
    einen anderen ueber die Staende-Historie. Die Tiefe kommt per
    `window.__verlauf` zurueck und schaltet die Knoepfe. Die Basis-
    Variante gehoert seither zum Zustand (`basis`), damit sie ein
    Neuladen ueberlebt. Der Verlauf lebt nur in der Sitzung - er ist
    Komfort, kein Nachweis; Staende und Protokoll bleiben unberuehrt.
    Tests: `VerlaufTests` (Host, inkl. echtem Neurechnen mit zwei
    Staenden) und `KlassenbildungBoardTests.UndoUndRedoImDoppelklickBetrieb`.
  - **"1 von 100 fixiert" direkt nach dem ersten Lauf** ist kein
    Fehler: die Beispielschule fixiert `S030` in `input/
    klassenbildung.yaml` (Rollstuhl, barrierefreier Raum). Der Zaehler
    nennt solche Bestandsfixierungen jetzt ausdruecklich
    ("(1 aus der Eingabe)") und erklaert sie im Tooltip.
  Verifikation: vier neue Playwright-Faelle in
  `KlassenbildungBoardTests` (Scorecard-Segmente summieren sich zu
  100 %, Vergleichshaken, Wunsch-Badges <= verletzte + 1, eingeklappte
  Stapel ohne gelb/rot und mit bedienbaren Karten, Legende und genau
  ein Punkt je Karte/Panelzeile), bestehende 33 unveraendert gruen,
  DesignTokenTests, Smoke, Vollbild per headless Edge (Rezept in
  CLAUDE.md): Spaltenhoehe im Grundschul-Beispiel von ~1300 auf ~980 px.
