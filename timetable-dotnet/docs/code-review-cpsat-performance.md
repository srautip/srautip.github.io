# Code-Review: TimetableCore CP-SAT-Kern (Stand Phase 2.27)

Auftrag: Review von `timetable-dotnet/` mit Schwerpunkt auf der
Performance der CP-SAT-Constraint-Implementierung, plus eine Bewertung
der Sekundaerkriterien-Frage: sind die 7 in `SolveTopObjective.vb`
einmodellierten Qualitaetskriterien sinnvoll, oder werden sie besser
durch nachtraegliche Qualitaetsbewertung (`ScheduleQuality.Score`)
abgedeckt? Zielbild des Nutzers: **moeglichst viele optimale UND
diverse Loesungen finden und den Anwender danach interaktiv selektieren
lassen.**

Gelesene Kernmodule: `Solver.vb`, `SolveTopObjective.vb`,
`ScheduleQuality.vb`, `Verifier.vb` (Kopf), `SchoolTestRunner/Run.vb`,
beide `config.yaml`-Beispiele, `Lehrereinsatzplanung.vb` (Querschnitt).
Referenzen auf fruehere Live-Befunde stammen aus
`docs/phase2-25-stagnation-heuristik.md` und den Config-Kommentaren.

## Gesamtbewertung

Der Kern ist in ungewoehnlich gutem Zustand fuer ein Projekt dieser
Entwicklungsgeschwindigkeit: strikte Solver/Verifier-Trennung ohne
geteilten Code, Fail-Fast-Validierung vor jedem Solve, ehrlich
dokumentierte Live-Experimente (inkl. verworfener Umbauten wie der
2.25c-Encoding-Studie), und die Big-M-freie Springstunden-Kodierung aus
Phase 2.25-Nachtrag-2 ist nachweislich die richtige Loesung gewesen.

Die verbleibenden Probleme liegen fast alle an EINER Stelle: der
**gewichteten Summen-Zielfunktion** und dem daran haengenden
Diversitaetsmechanismus. Die Einzelbefunde unten (P1-P6, R1-R4) sind
Symptome davon; Abschnitt "Sekundaerkriterien" beschreibt die
strukturelle Antwort.

## Befunde: CP-SAT-Performance

### P1 (hoch): `occupied_slot`-Batterien blaehen die Zielfunktion auf und duplizieren ClassGaps

`bw-gms-beispiel/input/constraints.yaml` enthaelt ~720
`occupied_slot`-Regeln, `bw-grundschule-beispiel` ~140 - jede als
eigenes Kann-Constraint. Jede Regel erzeugt in
`Solver.vb::ApplyConstraints` (Case `"occupied_slot"`) eine eigene
Kann-BoolVar plus eine halbreifizierte `Sum >= 1`-Bedingung, und jede
dieser BoolVars geht mit Gewicht `WeightKann` (100) in die
Zielfunktion. Ergebnis: allein dieser Constraint-Typ traegt im
GMS-Beispiel ~720 Binaerterme mit Koeffizient 100 bei - genau die Art
grosser gewichteter Summe, fuer die Phase 2.25c bereits gemessen hat,
dass CP-SATs Bound-Beweis daran strukturell scheitert (99,7 % Luecke
mit vollem Objective vs. Optimal in 0,2 s ohne).

Dazu kommt semantische Redundanz: die Regeln kodieren
"Vormittage durchgaengig belegen" - aber bei fixer Wochenstundenzahl
(`weekly_hours` ist immer harte Gleichheit) und minimierten
Springstunden (`ClassGaps`) folgt Vormittagsdichte groesstenteils
bereits aus den vorhandenen Kriterien. Die 720 Regeln bezahlen also
viel Objective-Komplexitaet fuer wenig zusaetzliche Steuerwirkung.

**Empfehlung:** die Batterie durch EIN kompaktes Kriterium ersetzen -
z. B. pro (Klasse, Tag) "letzte belegte Vormittagsperiode" bzw. eine
Summe `nicht belegter Vormittags-Slots` ueber die ohnehin gebauten
`occupied`-Variablen aus `SolveTopObjective.BuildScaffolding` (ein Term
pro Klasse/Tag statt ein reifiziertes Constraint pro Slot). Oder - im
Sinne der Zielarchitektur unten - Vormittagsdichte ganz aus der Suche
nehmen und nur nachgelagert bewerten.

### P2 (hoch): Gewichtete Summe verhindert Optimalitaetsbeweise - lexikografische Stufen sind mit der bestehenden API bereits machbar

Der zentrale, in Phase 2.25 selbst diagnostizierte Befund: das
Kann-only-Modell beweist Optimal in 0,2 s, das 7-Kriterien-Modell
kommt in 150 s auf 97-99 % Restluecke; isolierte Kriterien
(ClassGaps allein) beweisen Optimal in Sekunden. Die Konsequenz wurde
damals aber nur teilweise gezogen (Stagnation-Cutoff), nicht
strukturell.

Die strukturelle Loesung ist **lexikografische (gestufte)
Optimierung** statt einer Summe: Kriterium 1 optimieren, Optimum als
Constraint fixieren (`model.Add(expr <= opt)`), dann Kriterium 2
optimieren usw. Jede Stufe hat eine kleine, beweisbare Zielfunktion -
genau die Konstellation, die in den eigenen Experimenten in Sekunden
zum bewiesenen Optimum kam.

Wichtig: die Codebasis kann das heute schon, ohne neue API-Risiken.
`SolveTop` ruft bereits zweimal `Minimize` auf demselben `CpModel` auf
(Stage 1 Kann-only in `SolveTop`, danach ersetzt
`ApplyQualityObjective` die Zielfunktion) - das dokumentierte Fehlen
von `ClearObjective()` auf der installierten DLL ist also kein
Hindernis; Re-`Minimize` ersetzt nachweislich. Der Umbau ist damit im
Wesentlichen: nach Stage 1 `Sum(kannVars) <= kannOpt` als Constraint
setzen, dann `Minimize(classGapSum)`, fixieren, optional
`Minimize(teacherGapSum)`. Ein optionales Toleranzbudget pro Stufe
(`<= opt + epsilon`) erhaelt Spielraum fuer die nachfolgenden Stufen.

### P3 (hoch): Der Diversitaetsmechanismus erzeugt Nachbarloesungen, keine Alternativen

`BlockSolution` (Solver.vb:822) verbietet nur die EXAKTE
Lesson-Belegung; direkt danach hintet `ApplyLessonHints`
(Solver.vb:1024) die naechste Iteration auf genau diese soeben
verbotene Loesung. Der Solver wird also aktiv auf den naechstgelegenen
Nachbarn der Vorloesung gelenkt - zwei Loesungen, die sich in einem
einzigen getauschten Slot-Paar unterscheiden, zaehlen als "verschieden".
`diversify_seed`/`randomize_search` (Phase 2.25) wirken dem entgegen,
aber der Hint zieht in die Gegenrichtung. Fuer das erklaerte Ziel
"diverse Loesungen zur interaktiven Auswahl" ist das der wirksamste
einzelne Hebel:

1. **Echte Distanz-Cuts statt No-Good:** da `weekly_hours` die Anzahl
   wahrer Lesson-Vars fixiert, erzwingt
   `Sum(vorher wahre Lesson-Vars) <= T - d` eine
   Mindest-Hamming-Distanz von `2d` zur Vorloesung. `d` als
   Config-Feld (`min_diversity`), Default z. B. 5-10 % der
   Wochenstunden. Implementierbar als 3-Zeilen-Aenderung in
   `BlockSolution`.
2. **Re-Hinting fuer Folge-Iterationen abschaltbar machen** (oder nur
   den Stage-1-Hint behalten): der Hint beschleunigt Feasibility, aber
   er ist eine Aehnlichkeits-, keine Diversitaetsheuristik.
3. Optional: Diversitaet maximieren statt nur erzwingen - in einer
   Enumerationsphase `Maximize(Hamming-Distanz zur naechstgelegenen
   bisherigen Loesung)` ist teuer; der Distanz-Cut aus (1) ist der
   pragmatische 90 %-Ersatz.

### P4 (mittel): Objective-Scaffolding wird auch dann gebaut, wenn niemand es nutzt

`ApplyQualityObjective` baut `BuildScaffolding` fuer Klassen UND
Lehrer immer vollstaendig (occupied/hasAny/dailyCount), unabhaengig
von den `Include*`-Flags:

- `hasAnyClass` wird von keinem Abnehmer verwendet (nur
  `BuildTeacherRangeVars` liest `hasAny`, und nur fuer Lehrer) - das
  sind pro Klasse x Tag eine reifizierte BoolVar umsonst.
- Sind `IncludeTeacherGaps` und `IncludeTeacherLoadVariance` beide
  False, ist das komplette Lehrer-Scaffolding tot (im
  GMS-Beispiel real: nur TeacherGaps haelt es dort am Leben).

Presolve raeumt tote Variablen weg, aber Presolve-Zeit skaliert mit
der Modellgroesse, und beide Beispiel-Configs schalten Kriterien
gezielt ab, um Modellgroesse zu sparen - das Sparziel wird aktuell nur
teilweise erreicht. **Empfehlung:** Scaffolding nachfragegesteuert
bauen (Klassen-`hasAny` gar nicht; Lehrer-Scaffolding nur wenn ein
Lehrer-Kriterium aktiv ist).

### P5 (mittel): `ExtractSchedule` ist O(Lessons x Rooms)

`Solver.vb:758`: fuer jede wahre Lesson-Var wird das GESAMTE
`built.Room`-Dictionary linear durchsucht, um den zugewiesenen Raum zu
finden. Bei Raumgroessenordnung `Sessions x Tage x Perioden x
erlaubte Raeume` und `max_solutions: 30` (Grundschul-Config) laeuft
diese Schleife 30-mal pro Lauf. Fix: das Room-Dictionary einmalig nach
`(ClassName, Subject, Teacher, Day, Period)` gruppieren und pro Lesson
nur die eigene Kandidatenliste pruefen - macht die Extraktion linear.

### P6 (niedrig): Kleinere Performance-Hebel

- **Gewichts-Skalierung:** `class_gaps: 1000.0` (Grundschul-Config)
  gegen `teacher_gaps: 50` - dieselbe Prioritaet druecken `20 : 1`
  aus. CP-SAT-Bounds leiden unter grossen Integer-Koeffizienten
  (eigener 2.25c-Befund); Gewichte sollten als kleinstmoegliche
  ganzzahlige Verhaeltnisse konfiguriert werden. Ein
  GCD-Normalisierungsschritt in `ApplyQualityObjective` waere ein
  billiger Automatismus. (Mit der Stufen-Architektur aus P2 entfaellt
  das Problem ganz.)
- **`relative_gap_limit` als Diversitaets-Hebel:** fuer
  Folge-Iterationen, deren Zweck ALTERNATIVEN sind (nicht ein besseres
  Optimum), ist z. B. `relative_gap_limit: 0.05-0.2` genau richtig:
  CP-SAT akzeptiert frueher, mehr Iterationen passen ins Budget. Das
  Feld existiert bereits (Phase 2.25, opt-in) - es fehlt nur die
  Empfehlung/der Default, es ab Iteration 2 zu setzen.
- **EdgePeriod ist im Modell fast ein konstanter Term:** bei fixer
  Wochenstundenzahl und vollen Vormittagen ist Periode-1-Belegung
  weitgehend unvermeidlich; der Term verschiebt das Objective, ohne
  viel zu diskriminieren, und verwaessert Bounds. Beide
  Beispiel-Configs schalten ihn bereits ab - das sollte der
  Code-Default werden (siehe Sekundaerkriterien unten).

## Befunde: Robustheit / Wartbarkeit

### R1 (mittel): Doppelte `teacher_subject_assignment`-Eintraege ueberschreiben Lesson-Vars stillschweigend

`SessionsFromAssignments` dedupliziert nicht. Zwei identische
Assignment-Constraints erzeugen zwei `Session`-Objekte mit denselben
`LessonKey`s: die zweite Variablenerzeugung ueberschreibt den
Dictionary-Eintrag der ersten, die erste Variable bleibt als
unreferenzierte Waise im Modell, und `weekly_hours` addiert seine
Gleichung doppelt (harmlos, aber unnoetig). Empfehlung: in
`SessionsFromAssignments` deduplizieren und Duplikate in
`Validation.ValidateEntities` als Fehler melden.

### R2 (mittel): `occupied_slot`/`required_slot` sind bei leerer Treffermenge stille No-Ops - auch als Muss

Case `"occupied_slot"`: `If occVars.Count > 0` - matcht keine Session
(Tippfehler im Klassennamen faengt die Validierung; aber z. B. eine
Klasse ohne Sessions am fraglichen Slot-Raster nicht), wird die Regel
kommentarlos fallengelassen, selbst mit `priority: must`.
`required_slot` verhaelt sich analog (`If lesson.ContainsKey(key)`).
Der unabhaengige Verifier faengt das Ergebnis hinterher ab, aber ein
Fail-Fast in der Validierung ("Constraint referenziert keinen einzigen
existierenden Slot") passt besser zur Projektphilosophie.

### R3 (niedrig): `IncludeClassGaps` fehlt

Alle Sekundaerkriterien ausser ClassGaps haben ein strukturelles
`Include*`-Flag. ClassGaps wird immer gebaut. Fuer die
Zielarchitektur unten (und schlicht fuer Symmetrie) sollte auch
ClassGaps abschaltbar sein.

### R4 (niedrig): `ApplyConstraints` waechst monolithisch

10 Constraint-Typen in einem Select Case in einer 280-Zeilen-Methode
in der groessten Datei (1092 Zeilen). Noch beherrschbar, aber der
naechste Constraint-Typ waere ein guter Anlass, pro Typ eine private
Methode zu extrahieren (`ApplyTeacherAvailability(...)` usw.) - reine
mechanische Extraktion, kein Verhaltensrisiko, deutlich bessere
Testbarkeit pro Typ.

## Sekundaerkriterien: ins Modell oder nachgelagert?

Antwort auf die Kernfrage - pro Kriterium, auf Basis der eigenen
Messdaten des Projekts:

| Kriterium | Modell-Kodierung | Beweisbarkeit (gemessen) | Empfehlung |
|---|---|---|---|
| Kann-Verstoesse | 1 BoolVar/Constraint | Optimal in 0,2 s | **Im Modell, Stufe 1** |
| ClassGaps | Big-M-frei (2.25-N2) | Optimal in ~5 s isoliert | **Im Modell, Stufe 2** |
| TeacherGaps | Big-M-frei (2.25-N2) | Optimal in ~12 s kombiniert | **Im Modell, Stufe 3 (optional)** |
| EdgePeriod | Direktsumme ueber Lesson-Vars | verwaessert Bounds, kaum Steuerwirkung (P6) | **Nur nachgelagert** |
| AfternoonDayCount | Reifizierte Tages-BoolVars | in beiden Beispielen abgeschaltet | **Nur nachgelagert** |
| ClassLoadVariance | Min/Max-Range | Range ist ohnehin nur Approximation der echten Varianz | **Nur nachgelagert** |
| TeacherLoadVariance | Min/Max-Range + Sentinel | dito, plus teuerste Kodierung | **Nur nachgelagert** |

Begruendung der Trennlinie: ein Kriterium verdient einen Platz in der
SUCHE nur, wenn (a) der Solver ohne es systematisch in schlechte
Regionen laeuft UND (b) seine Kodierung beweisbar gut ist. Das trifft
auf Kann und die beiden Gap-Kriterien zu. Die vier uebrigen sind
Feinrankings zwischen ohnehin guten Loesungen - exakt der Job, den
`ScheduleQuality.Score` heute schon unabhaengig und exakt (echte
Varianz statt Range-Approximation!) erledigt. Beide Beispiel-Configs
haben diese Entscheidung empirisch schon getroffen (`include_* :
false`); der Code sollte sie zum Default machen, statt sie jeder
Schule einzeln abzuverlangen.

### Zielarchitektur fuer "viele optimale, diverse Loesungen + interaktive Auswahl"

Vier Phasen, alle mit vorhandenen Bausteinen erreichbar:

1. **Optimieren (lexikografisch):** Kann minimieren -> fixieren ->
   ClassGaps minimieren -> fixieren -> (optional TeacherGaps). Jede
   Stufe klein und beweisbar (P2). Ergebnis: ein bewiesenes
   Qualitaetsniveau `(K*, G*, T*)` statt eines diffusen
   Summen-Optimums mit 70 % Restluecke.
2. **Toleranzband definieren:** die fixierten Optima als
   `<= Opt + epsilon`-Constraints (epsilon pro Kriterium
   konfigurierbar, z. B. "1 Springstunde mehr ist ok"). Das ist der
   formale Ersatz fuer die heutige unklare "Feasible mit grosser
   Luecke"-Semantik.
3. **Divers enumerieren:** im Toleranzband OHNE Zielfunktion (oder mit
   `relative_gap_limit`) iterieren; pro gefundener Loesung ein
   Distanz-Cut (P3.1) statt des exakten No-Goods; Re-Hinting aus
   (P3.2); `diversify_seed`/`randomize_search` wie gehabt. Jede
   Iteration ist jetzt ein billiges Feasibility-Problem - viele
   Loesungen pro Zeitbudget.
4. **Interaktiv selektieren (nachgelagert):** `ScheduleQuality.Score`
   liefert pro Loesung den vollen Kriterienvektor - der existierende
   Stundentafel-Viewer braucht dafuer nur (a) die Vektor-Spalten
   sichtbar/sortierbar und (b) einen client-seitigen Gewichts-Regler:
   Gewichte werden damit eine ANZEIGE-Einstellung des Anwenders zum
   Auswahlzeitpunkt, keine Solver-Eingabe mehr. Optional: dominierte
   Loesungen (in allen Kriterien schlechter als eine andere)
   ausblenden - die Pareto-Front ist die natuerliche Auswahlmenge.

Nettoeffekt: die Sekundaerkriterien verschwinden nicht - sie wandern
an die Stelle, an der sie exakt berechenbar, erklaerbar und vom
Anwender gewichtbar sind. Der Solver behaelt nur, was er beweisen
kann.

## Umsetzungsstand

In derselben Session umgesetzt (Details in den Commit-Messages und den
Doc-Kommentaren der betroffenen Module):

- **P2** - `Solver.SolveTop` hat einen lexikografischen Modus
  (`lexicographic:=True`, opt-in): Kann -> ClassGaps -> TeacherGaps als
  einzeln beweisbare Stufen, Stufenoptimum als Constraint fixiert
  (`lexTolerance` weitet das Band), Iterationen danach ueber die
  gewichtete Rest-Zielfunktion. `SolveTopObjective` liefert dafuer die
  Kriterien als einzelne ungewichtete Summen (`BuildQualityTerms`/
  `WeightedTotal`/`WeightedResidual`); der gewichtete Modus bleibt
  Default, weil die Stufenreihenfolge fix ist und sonst den per
  `quality_weights` frei waehlbaren Prioritaetentausch aushebeln wuerde
  (garantiert durch `SolveTopQualityWeightsInfluenceChosenSchedule`).
- **P3** - `BlockSolution` setzt bei `minDiversity >= 1` zusaetzlich zum
  exakten No-Good einen echten Distanz-Cut (`Sum(bisher wahre Vars) <=
  Anzahl - d`); `rehintFoundSolutions:=False` schaltet das Re-Hinting
  auf die jeweils letzte Loesung ab. Beide als `config.yaml`-Felder
  (`min_diversity`/`rehint_found_solutions`) verfuegbar.
- **R1** - doppelte `teacher_subject_assignment`-Tripel sind jetzt ein
  harter `Validation.ValidateEntities`-Fehler; `SessionsFromAssignments`
  dedupliziert zusaetzlich defensiv.
- **R2** - `occupied_slot`/`required_slot` mit leerer Treffermenge
  (Entity ohne Sessions, fehlende Zuweisung, Tag/Periode ausserhalb des
  Rasters) sind jetzt Validierungsfehler statt stiller No-Ops.
- **R3** - `QualityWeights.IncludeClassGaps` ergaenzt (strukturelles
  Flag wie bei den uebrigen Kriterien), inkl. `config.yaml`-Feld.
- **R4** - `ApplyConstraints` ist ein reiner Dispatcher; jeder
  Constraint-Typ lebt in einer eigenen `ApplyXxx`-Methode.

Offen aus der Empfehlungsliste bleiben P1 (occupied_slot-Batterien),
P4/P5 (Scaffolding/Extraktion) und der Viewer-Ausbau (Empfehlung 6).

**Live-Beleg** (`bw-grundschule-beispiel`, identisches 2-Min-Budget,
`lexicographic: true`, `min_diversity: 8`, `rehint_found_solutions:
false`):

| | Vorher (Summenmodell, committeter Stand) | Nachher (lexikografisch + Distanz-Cuts) |
|---|---|---|
| CP-SAT-Status | Feasible, 71-74 % Luecke | Kann/ClassGaps/TeacherGaps **bewiesen optimal (0/0/0)** |
| Gefundene Loesungen | 1-2 | **15** (Restkriterien aktiv) bzw. **30** (Restkriterien aus) |
| Diversitaet | exakter No-Good (Ein-Slot-Nachbarn moeglich) | jede Loesung >= 8 Slots von jeder anderen entfernt |
| Beste Quality.Total | 167-187 | 183.6 |
| Muss-/Kann-Verstoesse | 0 / 0 | 0 / 0 |

Der frueher zentrale Befund "Bound bewegt sich nicht" (Objective 178-199
gegen Bound 51) ist damit fuer die drei gestuften Kriterien vollstaendig
aufgeloest; eine (deutlich kleinere) unbewiesene Luecke verbleibt nur
noch in der Rest-Zielfunktion der vier schwachen Kriterien. Testsuite:
242 bestanden / 0 Regressionen (9 neue Tests fuer P2/P3/R1-R3).

## Priorisierte Empfehlungen

| # | Massnahme | Aufwand | Wirkung |
|---|---|---|---|
| 1 | Distanz-Cut statt exaktem No-Good + Re-Hinting abschaltbar (P3) | klein | Diversitaet, direkt |
| 2 | Lexikografische Stufen Kann -> ClassGaps (-> TeacherGaps) (P2) | mittel | Beweisbarkeit, Laufzeit |
| 3 | `occupied_slot`-Batterien durch kompaktes Dichte-Kriterium ersetzen oder streichen (P1) | klein-mittel | Bound-Qualitaet im GMS-Beispiel |
| 4 | Default der 4 schwachen Kriterien auf `Include* = False` + `IncludeClassGaps` ergaenzen (R3) | klein | Modellgroesse, Konsistenz |
| 5 | Scaffolding nachfragegesteuert bauen (P4), Room-Index fuer `ExtractSchedule` (P5) | klein | Presolve-/Extraktionszeit |
| 6 | Viewer: Kriterienvektor-Spalten + Gewichts-Regler + Pareto-Filter (Zielarchitektur 4) | mittel | interaktive Auswahl |
| 7 | Validierung: Assignment-Duplikate, leere Slot-Treffermengen (R1, R2) | klein | Robustheit |
| 8 | `ApplyConstraints` pro Typ extrahieren (R4) | klein | Wartbarkeit |

## Modulueberblick (Ist-Stand, zur Einordnung)

| Modul | Zeilen | Rolle | Review-Befunde |
|---|---|---|---|
| Solver.vb | 1092 | Modellbau + Solve/SolveTop/SolveKursstufe | P3, P5, R1, R2, R4 |
| SolveTopObjective.vb | 314 | 7-Kriterien-Zielfunktion | P2, P4, P6 |
| ScheduleQuality.vb | 244 | Nachgelagerte, exakte Bewertung | Zielarchitektur Phase 4 |
| Verifier.vb | 574 | Unabhaengiger Checker | keine Befunde (Design vorbildlich) |
| Lehrereinsatzplanung.vb | 661 | Vorgelagerte Zuweisungsstufe | eigenes, kleines CP-SAT-Modell; unauffaellig |
| SchoolTestRunner/Run.vb | 381 | YAML-Pipeline + Reports | Konsument der Empfehlungen 1-4 |
