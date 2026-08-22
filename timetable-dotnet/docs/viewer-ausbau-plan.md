# Plan: Stundentafel-Viewer-Ausbau (Code-Review-Empfehlung 6)

Ziel aus der Zielarchitektur des Code-Reviews (Phase 4, "Interaktiv
selektieren", siehe `docs/code-review-cpsat-performance.md`): der
Anwender waehlt aus den vielen stufen-optimalen, diversen Loesungen
interaktiv aus - Gewichte werden dabei eine ANZEIGE-Einstellung zum
Auswahlzeitpunkt, keine Solver-Eingabe mehr. Der Viewer
(`SchoolTestRunner/Templates/stundentafel.html`, per
`StundentafelHtml.BuildStundentafelHtml` mit eingebettetem JSON
generiert) kann heute: Loesungs-Dropdown, Klassenstufen-/Lehrer-Filter,
Klassen- + Lehrerraster, Konvergenz-Sparkline. Es fehlen: der
Kriterienvektor pro Loesung, Sortier-/Vergleichsmoeglichkeiten,
Gewichts-Regler und Pareto-Filter.

## Schritt 1: Datenexport erweitern (Voraussetzung fuer alles Weitere)

`Formatting.ToStundentafelJson` exportiert pro Loesung bisher nur
`quality_total`, `kann_violation_count`, `muss_violation_count` und
`occupied_density_count`. Ergaenzen (alles liegt in `sol.Quality`
bereits berechnet vor - reine Serialisierung, keine neuen Kosten):

- pro Loesung der volle Vektor: `class_gap_count`, `teacher_gap_count`,
  `edge_period_count`, `afternoon_day_count`, `class_load_variance`,
  `teacher_load_variance`.
- Top-Level `quality_weights`: die effektiv konfigurierten Gewichte der
  Schule (inkl. der `include_*`-Flags) - Startwerte fuer den Regler in
  Schritt 3 und Grundlage, im UI strukturell abgeschaltete Kriterien
  als "nicht gesteuert" zu kennzeichnen. Dafuer muss `RunOne` die
  aufgeloesten `QualityWeights` an `ToStundentafelJson` durchreichen
  (kleine Signaturerweiterung).

Tests: `StundentafelJsonTests` um die neuen Felder erweitern
(vorhandenes Muster, hand-gebaute ScoredSolutions).

## Schritt 2: Loesungsuebersicht als sortierbare Vergleichstabelle

Das Dropdown "Loesung N" durch eine Uebersichtstabelle ergaenzen (das
Dropdown kann bleiben, die Tabelle wird der primaere Auswahlweg):

- eine Zeile pro Loesung; Spalten: Rang, Status-Chip (Optimal/
  Feasible), Kann, Fenster-Defizit, ClassGaps, TeacherGaps, Randstunden,
  Nachmittags-Tage, beide Varianzen, Total (nach aktuellen
  Regler-Gewichten, siehe Schritt 3).
- Klick auf eine Zeile waehlt die Loesung (aktive Zeile hervorgehoben),
  Klick auf einen Spaltenkopf sortiert; `font-variant-numeric:
  tabular-nums` fuer die Zahlenspalten; Tabelle im eigenen
  `overflow-x: auto`-Container.
- strukturell abgeschaltete Kriterien (`include_* = false`) bekommen
  eine gedaempfte Spalte + Tooltip "Suche war fuer dieses Kriterium
  blind - Wert nur nachgelagert gezaehlt".

## Schritt 3: Gewichts-Regler (client-seitig)

- Ein einklappbares Panel "Gewichte" mit einem Zahlenfeld/Slider je
  Kriterium, initialisiert aus dem exportierten `quality_weights`;
  Reset-Knopf "Schulgewichte".
- Aenderungen berechnen `total` je Loesung live neu (identische Formel
  wie `ScheduleQuality.Score`, als kleine pure JS-Funktion - die
  bewusste, kommentierte Duplikation der Gewichtsformel ist der Preis
  dafuer, dass der Viewer eine per Doppelklick oeffenbare, statische
  Datei ohne Server bleibt) und sortieren die Uebersichtstabelle neu.
- Wichtig fuers Verstaendnis im UI: ein Hinweis, dass die Regler nur
  das RANKING der bereits gefundenen Loesungen aendern - nicht die
  Suche (die lief mit den Schulgewichten bzw. Stufen).
- Kein localStorage-Persist eingeplant: der Viewer laeuft regulaer als
  `file://`-Doppelklick, dort ist localStorage unzuverlaessig; die
  Reglerwerte sind bewusst fluechtige Was-waere-wenn-Exploration.

## Schritt 4: Pareto-Filter

- Toggle "nur Pareto-optimale Loesungen": eine Loesung ist dominiert,
  wenn eine andere in ALLEN Vektorkriterien <= und in mindestens einem
  < ist (gewichtsunabhaengig - genau deshalb ergaenzt der Filter die
  Regler, statt sie zu doppeln). Muss-Verstoesse und strukturell
  abgeschaltete Kriterien zaehlen mit - Dominanz ueber den ECHTEN
  Vektor.
- Dominierte Zeilen werden abgedunkelt statt entfernt (Transparenz:
  der Anwender sieht, WAS aussortiert wurde und warum - Tooltip nennt
  die dominierende Loesung); Zaehler "n von m Loesungen
  Pareto-optimal".
- Bei den aktuellen Beispielen wirkt der Filter sichtbar: die
  GMS-Dichte-Stufen-Loesungen unterscheiden sich fast nur in
  TeacherGaps (44-53) - dort bleibt wenig dominiert; die
  Grundschul-Loesungen streuen ueber mehrere Kriterien.

## Schritt 5 (Ausbaustufe, optional): Loesungsvergleich

- Zwei Loesungen nebeneinander bzw. als Diff: Zellen, die sich
  zwischen Loesung A und B unterscheiden, farblich markieren;
  Kopfzeile zeigt die Slot-Distanz (aus den `classes`-Grids client-
  seitig berechenbar - dieselbe Metrik, die `min_diversity` erzwingt).
- Nutzen: macht sichtbar, WORIN sich zwei aehnlich gute Kandidaten
  tatsaechlich unterscheiden - die eigentliche Entscheidungshilfe bei
  der interaktiven Auswahl.

## Leitplanken

- **Self-contained bleibt Pflicht:** kein fetch(), keine CDN-Libs,
  inline JS/CSS im Template - Doppelklick ohne Webserver ist der
  dokumentierte Nutzungsweg (Phase 2.21).
- **Stilkontinuitaet:** das Template nutzt ES5 (`var`,
  `function`-Syntax) - dabei bleiben, kein Build-Schritt.
- **Regenerierung:** Template-Aenderungen werden erst durch einen
  `SchoolTestRunner`-Lauf in den Beispiel-Outputs sichtbar; Schritt 1
  aendert zusaetzlich das JSON-Schema. Beide Beispiele neu laufen
  lassen (Grundschule ~3 Min; GMS mit reduziertem Kurzbudget fuer die
  Regeneration, die 20-Min-Referenzlaeufe muessen nicht wiederholt
  werden) und die GitHub-Pages-Kopien (`stundentafel/*.html`)
  nachziehen.
- **Abwaertskompatibilitaet:** der Viewer liest fehlende neue
  JSON-Felder defensiv (aeltere stundenplan.json bleiben oeffenbar,
  Spalten zeigen dann "-").
- **Verifikation:** JS-Kernlogik (Total-Formel, Dominanz, Distanz) als
  pure Funktionen halten; Smoke-Test der generierten Seite ueber das
  vorinstallierte Chromium (Playwright) gegen die realen
  Beispiel-JSONs - mindestens: Seite laedt fehlerfrei, Tabelle zeigt n
  Zeilen, Reglertausch aendert die Sortierung deterministisch.

## Reihenfolge und Aufwand

| Schritt | Aufwand | Nutzen |
|---|---|---|
| 1 Datenexport | klein | Voraussetzung fuer 2-4 |
| 2 Vergleichstabelle | mittel | groesster Einzelnutzen (Vektor sichtbar + sortierbar) |
| 3 Gewichts-Regler | klein-mittel | Gewichte werden Anzeige-Einstellung (Kernziel) |
| 4 Pareto-Filter | klein | natuerliche Auswahlmenge, gewichtsunabhaengig |
| 5 Loesungs-Diff | mittel | Entscheidungshilfe, optional nachziehbar |

Schritte 1+2 als erster Wurf (ein Commit inkl. Regeneration), 3+4 als
zweiter, 5 nach Bedarf.

## Umsetzungsstand

- **Schritt 1 umgesetzt:** voller Qualitaetsvektor + `quality_weights`
  (inkl. `include_*`) im JSON-Export; 2 neue `StundentafelJsonTests`.
- **Schritt 2 umgesetzt:** sortierbare Loesungsuebersicht im Viewer
  (Zeilenklick waehlt, Spaltenkopf sortiert, gedaempfte Spalten fuer
  strukturell abgeschaltete Kriterien, defensiv gegen alte JSONs) -
  per Headless-Chromium-Smoke gegen altes UND neues Schema sowie gegen
  beide regenerierten Beispiele verifiziert.
- **Schritt 3 umgesetzt:** Gewichte-Panel mit Zahlenfeld je Kriterium
  (Start = exportierte Schulgewichte, Reset-Knopf, `Total*`-Markierung
  bei Abweichung, Hinweis "wirkt nur aufs Ranking"); Total wird live per
  JS-Duplikat der Score-Formel neu berechnet - beim Laden identisch mit
  `quality_total` (gegen beide Beispiele auf 0.0 Abweichung geprueft).
- **Schritt 4 umgesetzt:** Pareto-Filter (gewichtsunabhaengige Dominanz
  ueber den echten Vektor, dominierte Zeilen abgedunkelt + Tooltip mit
  dominierender Loesung, Zaehler "n von m Pareto-optimal") - die
  JS-Dominanz wurde per Headless-Chromium-Interaktionstest gegen eine
  unabhaengige Python-Nachrechnung verifiziert (Grundschule: 10/30,
  GMS: 25/28 Pareto-optimal). Neues CLI-Subkommando `render <schule>`
  baut stundentafel.html ohne Solver-Lauf aus vorhandener JSON neu.
- **Schritt 5 umgesetzt:** "Vergleichen mit"-Auswahl - Diff-Markierung
  abweichender Zellen im Klassen- UND Lehrerraster (Tooltip zeigt die
  Belegung der Vergleichsloesung), Slot-Distanz-Anzeige,
  Vergleichszeilen-Markierung in der Uebersicht. Per Chromium-
  Interaktionstest verifiziert: die angezeigte Distanz (8 Slots
  zwischen Loesung 1 und 2 der Grundschule) trifft die unabhaengige
  Python-Nachrechnung exakt - und entspricht genau dem
  min_diversity=8-Cut der Enumeration. Damit ist der Plan vollstaendig
  umgesetzt.
- **Nachtrag (Nutzerentscheidung):** das "Loesung N"-Dropdown ist
  ENTFERNT - die Uebersichtstabelle ist der einzige Auswahlweg
  (Zeilenklick aktualisiert Klassen- UND Lehrerraster; die
  Zusammenfassung im Kopf nennt jetzt selbst die gewaehlte Loesung).
  Die "Vergleichen mit"-Auswahl (Schritt 5) bleibt als Dropdown.
