# Phase 2.8: Mehrere bewertete Lösungen (`Solver.SolveTop`)

Dieser Bericht dokumentiert Phase 2.8 (siehe Plan, Abschnitt "Phase 2.8
(feingeplant)"): `Solver.Solve()` liefert weiterhin genau eine Lösung
(unverändert, byte-identisches Verhalten für alle bestehenden Aufrufer).
Neu ist `Solver.SolveTop(...)`, das mehrere - begrenzt viele - bewertete
Kandidaten-Stundenpläne liefert, damit ein späterer Aufrufer zwischen
Alternativen wählen kann.

## Nutzerentscheidungen

1. **Anzahl-Steuerung:** feste Obergrenze (`maxSolutions`, Default 10) UND
   ein Gesamt-Zeitbudget (`totalTimeLimitS`, Default 120s) - was zuerst
   greift, stoppt die Suche.
2. **Bewertungsschema:** umfassend, mehrere gewichtete Kriterien (siehe
   unten).
3. **Diversität:** einfache Verschiedenheit genügt - jede neue Lösung muss
   sich nur in mindestens einer `Lesson`-Variable von allen bisherigen
   unterscheiden (Standard-No-Good-Constraint über `AddBoolOr`), bewusst nur
   über `Lesson`, nicht `Room`, gesperrt.
4. **Kann-Zählweise:** `Verifier.VerifyScheduleDetailed(...).KannViolations.Count`
   (Vorkommen-genau, unabhängig vom Solver neu abgeleitet), nicht die
   günstigere `KannConstraintFlags.Count(Relaxed=True)`-Zählung.
5. **Randstunden-Scope:** erste Stunde (`Period=1`) UND Nachmittagsstunden
   (`Period >= AfternoonThresholdPeriod`, Konstante = 7 - passend zur schon
   in den Fixture-Prompts etablierten Konvention "Periode 7 =
   Nachmittagsunterricht").
6. **Gewichtung:** feste, dokumentierte Konstanten (kein szenario-relatives
   dynamisches Gewicht).

## `ScheduleQuality.vb`: Bewertungsschema

`QualityScore` mit 6 Feldern (`KannViolationCount`, `ClassGapCount`,
`TeacherGapCount`, `EdgePeriodCount`, `ClassLoadVariance`,
`TeacherLoadVariance`, `Total`). Gewichte:

| Kriterium | Gewicht | Begründung |
|---|---|---|
| Kann-Verstöße | 100000 | muss dominieren - selbst ein extrem schlechtes Sekundär-Ergebnis bleibt weit darunter (siehe Dominanz-Test) |
| Lücken Klassen/Lehrer | je 10 | "Springstunden" sind am störendsten in der Praxis |
| Randstunden | 5 | störend, aber weniger als Lücken |
| Tagesausgewogenheit Klassen/Lehrer | je 3 | "nice to have"-Glättung |

Lehrerbelastung-Ausgewogenheit wird bewusst nur über die tatsächlichen
Arbeitstage eines Lehrers berechnet (Tage, an denen er im Schedule
vorkommt), nicht über alle Kalendertage - sonst würde erklärte
Teilzeit-Unverfügbarkeit fälschlich als "Unausgewogenheit" bestraft (durch
einen dedizierten Regressionstest `TeacherLoadVarianceOnlyCountsWorkingDays`
abgesichert).

## `Solver.SolveTop`: Mechanik

Baut das Modell einmalig, löst wiederholt dasselbe `CpModel` mit einem
frischen `CpSolver` pro Iteration; nach jedem Fund wird eine No-Good-Sperre
(`AddBoolOr` über die `Lesson`-BoolVars) hinzugefügt, sodass dieselbe
Zuordnung nie wieder auftauchen kann. Ein Plan-Agent hat die Kernmechanik
vorab per Live-Reflection gegen die tatsächlich installierte
`Google.OrTools` 9.15.6755 verifiziert (`BoolVar` implementiert `ILiteral`
direkt, kein Cast nötig; ein Smoke-Test mit 3 freien BoolVars enumerierte
korrekt alle 8 Zuweisungen ohne Duplikate).

## Live-diagnostizierter Fehlermodus (während der Testentwicklung gefunden)

**Symptom:** der ursprüngliche `TotalTimeLimitCapTest` sollte beweisen,
dass `totalTimeLimitS` als eigenständiges Abbruchkriterium funktioniert -
mit einem absichtlich riesigen Lösungsraum (3 Klassen × 20 Slots,
`C(20,3)³ ≈ 1,48 Mrd.` rein kombinatorisch mögliche Zuordnungen) und einem
kleinen Zeitbudget. Der Test schlug wiederholt fehl: `StopReason` war
`SearchSpaceExhausted`, nicht `TimeLimitReached`.

**Root Cause (zweistufig):**
1. CP-SATs Suche bewies "keine weitere Lösung" nach überraschend wenigen
   No-Good-Sperren - 495 von 1140 möglichen Kombinationen bei einem
   einzelnen 20-Slot-Szenario, 176 bei 3 unabhängigen Klassen, 122 beim
   großen `FullScenarioFixture` - jeweils weit unter der naiven
   kombinatorischen Schätzung, aber jedes Mal mit tatsächlich bewiesenem
   `Infeasible`-Status (kein Bug in `BlockSolution` selbst).
2. Ein echter Korrektheitsfehler in `SolveTop` kam dazu: jeder Status
   außer `Optimal`/`Feasible` wurde als `SearchSpaceExhausted` gemeldet -
   auch `CpSolverStatus.Unknown` (das eigene Zeitbudget des Solve-Aufrufs
   ist abgelaufen, ohne dass irgendetwas bewiesen wurde). Das ist keine
   Erschöpfung des Suchraums, sondern selbst ein Zeit-Ereignis.

**Fix:**
1. `SolveTop` unterscheidet jetzt explizit: `Infeasible`/`ModelInvalid` ->
   `SearchSpaceExhausted` (echter Beweis), `Unknown` -> `TimeLimitReached`
   (kein Beweis, nur Zeit abgelaufen).
2. `TotalTimeLimitCapTest` verwendet jetzt `BuildFullScenario()` (das
   aufwendigste verfügbare Fixture, höchste Kosten pro Iteration) mit einem
   absichtlich winzigen Zeitbudget (50ms) - weit unter dem beobachteten
   ~16ms/Iteration-Tempo, das selbst dieses große Szenario in ~2s
   "erschöpfen" konnte. Über 3 Wiederholungsläufe stabil grün.

## Verifikation

- Alle 6 neuen `ScheduleQualityTests.vb`-Tests grün (inkl. Dominanz- und
  Arbeitstage-Regressionstest).
- Alle 6 neuen `SolveTopTests.vb`-Tests grün (Einzellösung, sortierte
  Mehrfachlösung mit nicht-fallender Kann-Zahl, `maxSolutions`-Cap,
  `totalTimeLimitS`-Cap, Determinismus, Muss-Sauberkeit aller Kandidaten).
- Vollständige Regressionssuite: 52 bestanden, 6 korrekt übersprungen
  (gated Live-Tests), 0 Regressionen gegenüber dem Vor-Phase-2.8-Stand.
- Kein Live-LLM-Test nötig - diese Phase ist rein solver-seitig und jede
  Behauptung deterministisch prüfbar.

## Definition of Done — Status

- [x] `dotnet test TimetableCore.Tests` bleibt vollständig grün, 0
      Regressionen.
- [x] `SolveTop` liefert nachweislich >1 Lösungen für ein Szenario mit
      echtem Freiheitsgrad, aufsteigend sortiert, nicht-fallende
      Kann-Verstoss-Zahl.
- [x] `maxSolutions` und `totalTimeLimitS` greifen beide nachweislich als
      unabhängige Abbruchkriterien.
- [x] Alle 5 Bewertungskriterien einzeln durch handgerechnete Tests belegt.
- [x] Dieser Bericht committet.
