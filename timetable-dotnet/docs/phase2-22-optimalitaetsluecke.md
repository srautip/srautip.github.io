# Phase 2.22: Optimalitäts-Lücke + Konvergenz-Verlauf sichtbar machen

**Nachtrag: negativer `Quality.Total`-Bug behoben** - siehe Abschnitt
"Nebenbefund" unten. Beim Live-Testen deckte die neue, unabhängige
`objective_value`/`best_objective_bound`-Anzeige einen bereits
bestehenden Fehler auf: `bw-grundschule-beispiel` zeigte
`Quality.Total = -513.0`, obwohl diese Metrik laut ihrer eigenen
Konstruktion (Summe nicht-negativer, positiv gewichteter Terme) niemals
negativ sein kann. Auf Nutzerentscheidung wurde das direkt mitbehoben.

## Kontext

Nutzerfrage nach dem Hochskalieren von `bw-gms-beispiel`s Zeitbudgets
(30 Min gesamt / 10 Min pro Solve-Versuch, weiterhin nur `Feasible` statt
`Optimal`): "kann man bestimmen, wie weit man von optimal entfernt ist,
oder in welcher Zeit die Qualität nochmal deutlich steigen würde?"

Beide Fragen lassen sich mit bereits vorhandener CP-SAT-API beantworten,
live gegen die installierte `Google.OrTools` 9.15.6755-DLL per Reflection
UND per Wegwerf-Smoke-Modell bestätigt (nicht nur aus Dokumentation
angenommen):

- `CpSolver.ObjectiveValue`/`CpSolver.BestObjectiveBound` (beide `Double`)
  existieren bereits nach jedem `Solve()`-Aufruf - die Differenz ist die
  bewiesene **Optimalitäts-Lücke** (bei `Optimal` per Definition 0, live an
  einem Toy-Knapsack-Modell bestätigt: beide Werte identisch bei Status
  `Optimal`).
- `CpSolver.Solve(model, callback)` (Überladung mit einem
  `CpSolverSolutionCallback`) existiert; `CpSolverSolutionCallback`s
  Basisklasse (`SolutionCallback`) stellt `WallTime()`/`ObjectiveValue()`
  bereits innerhalb von `OnSolutionCallback()` bereit - live bestätigt:
  der Callback feuert korrekt bei jeder gefundenen Verbesserung
  (Incumbent), nicht in festen Zeitabständen.

## Nutzerentscheidung

"Ja, sowohl im Markdown als auch im HTML sichtbar machen" - beide
Ausgabeformate des `SchoolTestRunner` bekommen die neuen Informationen.

## Mechanik

- **`TimetableCore/Solver.vb`**: neue `ConvergencePoint`-Klasse
  (`ElapsedS`, `ObjectiveValue`) + private `ConvergenceCallback`
  (erbt von `CpSolverSolutionCallback`, sammelt einen `ConvergencePoint`
  pro `OnSolutionCallback()`-Aufruf). `ScoredSolution` bekommt drei neue
  additive Felder: `ObjectiveValue`, `BestObjectiveBound` (beide aus dem
  `CpSolver` nach dem Solve übernommen) und `Convergence` (die vom
  Callback gesammelte Liste, defaultet auf eine leere statt einer
  `Nothing`-Liste, damit handgebaute `ScoredSolution`-Objekte in
  bestehenden Tests - die dieses Feld nicht setzen - nicht mit einer
  `NullReferenceException` scheitern). `SolveTop`s Iterationsschleife
  übergibt den Callback an `solver.Solve(built.Model, convergenceCb)`
  statt `solver.Solve(built.Model)` - sonst unveränderte Mechanik.
  `BuildModel`/`Solve()` (der Einzel-Lösungspfad) bleiben unangetastet -
  die neue Mechanik lebt ausschließlich im `SolveTop`-Pfad, wo sie
  angefragt wurde.
- **`TimetableCore/Formatting.vb`, `ToStundentafelJson`**: pro Lösung
  drei neue JSON-Felder - `objective_value`, `best_objective_bound`,
  `gap_percent` (`100 * (objective_value - best_objective_bound) /
  objective_value`, 0 falls `objective_value <= 0`) - sowie `convergence`
  als JSON-Array von `{elapsed_s, objective_value}`-Paaren.
- **`SchoolTestRunner/Run.vb`**: neuer `## Optimalitaets-Luecke`-Abschnitt
  in `stundenplan.md` - bei `Optimal` ein Bestätigungssatz (Lücke = 0%),
  sonst Objective/Schranke/Lücke in Prozent plus eine Markdown-Tabelle des
  Konvergenz-Verlaufs (nur falls mehr als 1 Eintrag vorhanden) mit einem
  Hinweis, wie lange vor Ablauf des Zeitbudgets die letzte Verbesserung
  gefunden wurde.
- **`SchoolTestRunner/Templates/stundentafel.html`**: neues
  `#convergence-panel` unterhalb der Lösungsauswahl - Text mit der
  Optimalitäts-Lücke plus (falls `convergence.length > 1`) eine kleine,
  abhängigkeitsfrei per Inline-SVG gezeichnete Sparkline (Zeit auf der
  X-Achse, Objective auf der Y-Achse, so gespiegelt dass "besser" visuell
  nach oben zeigt statt einem sinkenden Zahlenwert zu folgen).

## Live-Verifikation

- Wegwerf-Reflection-Check gegen die installierte OrTools-DLL bestätigte
  vor jeder Code-Änderung: `CpSolver.BestObjectiveBound`-Property
  existiert, `CpSolverSolutionCallback`/`SolutionCallback` stellen
  `WallTime()`/`ObjectiveValue()`/`BestObjectiveBound()` bereit,
  `CpSolver.Solve(CpModel, SolutionCallback)`-Überladung existiert.
- Wegwerf-Smoke-Modell (Toy-Knapsack, 40 Bool-Variablen) bestätigte: bei
  Status `Optimal` sind `ObjectiveValue`/`BestObjectiveBound` exakt
  identisch (Lücke = 0), der Callback liefert sinnvolle
  `(WallTime, ObjectiveValue)`-Paare.
- Neuer Test `SolveTopScoredSolutionCarriesObjectiveGapAndConvergence`
  (`SolveTopTests.vb`) bestätigt dasselbe End-to-End über den echten
  `Solver.SolveTop`-Pfad: bei bewiesenem `Optimal` ist die Lücke exakt 0,
  mindestens ein Konvergenz-Punkt wurde aufgezeichnet, und dessen letzter
  Eintrag stimmt mit dem finalen `ObjectiveValue` überein.
- `StundentafelJsonTests.vb`s bestehender Test
  `SolutionsCarryQualityAndViolationCounts` um Assertions für
  `objective_value`/`best_objective_bound`/`gap_percent`/`convergence`
  erweitert (handgerechnetes Beispiel: 8.0/6.0 → 25% Lücke).
- Volle Regressionssuite (`dotnet test TimetableCore.Tests`) bleibt grün,
  0 Regressionen.
- Beide Referenzbeispiele (`bw-grundschule-beispiel`, `bw-gms-beispiel`)
  live neu durchlaufen; `stundentafel.html` per Playwright/Chromium
  gescreenshotet - Lücken-Text und Sparkline sichtbar korrekt dargestellt.

## Nebenbefund: negativer `Quality.Total`-Bug (behoben)

Der Live-Lauf von `bw-grundschule-beispiel` zeigte
`Quality.Total = -513.0` - unmöglich laut `ScheduleQuality.Score`s eigener
Konstruktion (Summe ausschließlich nicht-negativer, positiv gewichteter
Terme). Die neue, unabhängige `objective_value`/`best_objective_bound`-
Anzeige (68/24, sinnvoll) machte den Widerspruch sofort sichtbar.

**Ursache:** `GapsOverEntities` (verwendet für `ClassGapCount`/
`TeacherGapCount`) zählte pro (Entity, Tag) die ROHEN `ScheduleEntry`-
Zeilen statt der tatsächlich unterschiedlichen belegten Perioden. Seit
Phase 2.20 (`parallel_group`, z.B. Religion-ev/Religion-kath/Ethik
gleichzeitig für dieselbe Klasse) erzeugt EIN belegter Slot für eine
Klasse mehrere `ScheduleEntry`-Zeilen auf derselben Periode - die rohe
Zeilenzahl uebertraf dadurch die tatsächliche Tages-Spanne (`span`),
wodurch `span - periods.Count` negativ wurde. Phase 2.9s eigener
Kommentar zu genau dieser Funktion hatte diesen Fall bereits als
Randfall benannt, aber (vor Phase 2.20) faelschlich als "durch
`no_overlap` in der Praxis ausgeschlossen" eingestuft - `parallel_group`
erzeugt diese Situation aber ABSICHTLICH (das ist sein ganzer Zweck).

**Fix:** `periods.Distinct()` vor der Span-/Gap-Berechnung
(`TimetableCore/ScheduleQuality.vb`, `GapsOverEntities`) - eine Klasse
gilt fuer Luecken-Zwecke an einer Periode als belegt, unabhängig davon,
wie viele parallele Fächer dort gleichzeitig laufen. Neuer
Regressionstest `ClassGapCountIgnoresParallelGroupDuplicatesAtSamePeriod`
(`ScheduleQualityTests.vb`) baut genau dieses 3-fach-parallele Szenario
handgebaut nach und beweist sowohl den korrekten `ClassGapCount` als auch
`Total >= 0`.

**Bewusst NICHT mitgeändert:** `edgeCount`/`LoadVarianceOverAllDays`
zählen ebenfalls rohe Zeilen und wären durch `parallel_group` ähnlich
(nach oben, nicht negativ) verzerrt - das ist kein Vorzeichenfehler,
sondern eine subtilere Frage, WIE ein paralleler Slot fuer diese beiden
Metriken zu werten ist; auf Nutzerentscheidung bewusst als eigener,
zurückgestellter Punkt behandelt statt in dieser Phase mitgelöst.

## Definition of Done

- `dotnet test TimetableCore.Tests` bleibt vollständig grün, 0
  Regressionen, inkl. der neuen/erweiterten Tests.
- `Solve()`/`BuildModel()` bleiben unverändert - die neue Mechanik lebt
  ausschließlich im `SolveTop`-Pfad.
- Beide Referenzbeispiele laufen live durch, PASS, 0 Verstöße;
  `stundenplan.md` zeigt den neuen `## Optimalitaets-Luecke`-Abschnitt,
  `stundentafel.html` zeigt Lücken-Text + Sparkline (soweit
  `convergence.length > 1`).
- Committet und gepusht auf `claude/qwen-3.5-sandbox-test-ubfhmo`.

**Kritische Dateien:**
- `timetable-dotnet/TimetableCore/Solver.vb` - `ConvergencePoint`,
  `ConvergenceCallback`, `ScoredSolution`-Erweiterung, `SolveTop`-Wiring.
- `timetable-dotnet/TimetableCore/Formatting.vb` - `ToStundentafelJson`-
  Erweiterung.
- `timetable-dotnet/TimetableCore.Tests/SolveTopTests.vb` - neuer Test.
- `timetable-dotnet/TimetableCore.Tests/StundentafelJsonTests.vb` -
  erweiterter Test.
- `timetable-dotnet/SchoolTestRunner/Run.vb` - neuer
  `## Optimalitaets-Luecke`-Abschnitt in `stundenplan.md`.
- `timetable-dotnet/SchoolTestRunner/Templates/stundentafel.html` - neues
  `#convergence-panel` + Inline-SVG-Sparkline.
- `timetable-dotnet/tests/README.md` - JSON-Feldschema-Ergänzung.
- `timetable-dotnet/tests/{bw-grundschule-beispiel,bw-gms-beispiel}/output/*`
  - live regeneriert.
