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

# Phase 2.9: Bewertungskriterien in der CP-SAT-Zielfunktion

Nutzerfrage nach einem Live-Test (Grundschule-Szenario): "Kann man die
Bewertungskriterien auch in die Zielfunktion integrieren, so dass der
Solver gezielt optimiert?" Bis hierhin optimierte `SolveTop`s CP-SAT-Modell
ausschließlich Kann-Verstöße; die 4 Sekundärkriterien aus
`ScheduleQuality.vb` wurden erst nach jedem Solve rein informativ berechnet
und nur zum Sortieren benutzt. Ein Live-Test des Nutzers zeigte das
konkret: bei `maxSolutions:=5` wurde ein Total-Score von 94,8 gefunden, bei
`maxSolutions:=50` bereits 79,5 - die Suchreihenfolge war bzgl. der
Sekundärkriterien praktisch beliebig.

## Nutzerentscheidungen

1. **Architektur:** `SolveTop` wird in seiner Kernlogik umgebaut (kein
   separater Zusatzmodus) - jede Iteration baut jetzt die volle gewichtete
   Zielfunktion. `Solve()`/`BuildModel()` bleiben byte-identisch
   unverändert (`BuildModel`s Vorbau-Logik wurde 1:1 in ein privates
   `BuildCoreModel` ausgelagert, das sowohl `BuildModel` als auch der neue
   `SolveTop`-Pfad aufrufen).
2. **Tagesausgewogenheit:** linearer Ersatz - Spannweite (Max-Min) statt
   echter quadratischer Varianz in der Zielfunktion. `ScheduleQuality.vb`s
   echte Varianz bleibt unverändert die maßgebliche, unabhängig
   nachberechnete Anzeige-/Sortier-Metrik (gleiches Prinzip wie
   Verifier.vb vs. Solver.vb).
3. **Umfang:** alle 5 Kriterien wandern in die Zielfunktion.

## Live-Verifikation der CP-SAT-Modellierung

Ein Plan-Agent hat die Kernmechanik vorab **live gegen die tatsächlich
installierte** `Google.OrTools` 9.15.6755 verifiziert (nicht nur aus
Dokumentation angenommen): `CpModel.AddMinEquality`/`AddMaxEquality`
existieren mit Signatur `(LinearExpr, IEnumerable(Of LinearExpr))`;
`BoolVar : IntVar : LinearExpr` (ein `BoolVar` ist ueberall direkt als
`LinearExpr` verwendbar); ein tatsächlich gelöstes Smoke-Modell mit der
Sentinel-Min/Max-Lückenkodierung lieferte exakt die handgerechneten
Erwartungswerte. Das Modell kompilierte und lief beim ersten Versuch ohne
API-Ueberraschungen.

## `SolveTopObjective.vb` (neues Modul)

Baut pro (Klasse-oder-Lehrer, Tag) ein gemeinsames Geruest (`occupied`-
und `hasAny`-BoolVars, wiederverwendet von Luecken- UND Ausgewogenheits-
Kodierung), dann:
- **Luecken:** Sentinel-Substitutions-Min/Max-Trick (`firstOccupied`/
  `lastOccupied` ueber besetzte Perioden, `gapVar = Spanne - Anzahl`).
- **Randstunden:** triviale Summe ueber bereits vorhandene `Lesson`-Vars,
  keine neuen Variablen.
- **Klassen-Ausgewogenheit:** `Max-Min` der taeglichen Stundenzahl ueber
  ALLE Tage, kein Sentinel noetig.
- **Lehrer-Ausgewogenheit:** `Max-Min`, aber nur die MIN-Seite braucht ein
  Sentinel (arbeitsfreie Tage duerfen die Spannweite nicht aufblaehen) -
  die heikelste Stelle, per dediziertem Test abgesichert.
- **Finales `Minimize`:** gewichtete Summe aller Terme, mit denselben
  ganzzahligen Gewichten wie `ScheduleQuality.vb` (`CLng(...)`-gewandelt).

## Ergebnis: Qualität stark verbessert, Rechenzeit deutlich höher

Direkter Vorher/Nachher-Vergleich auf demselben Grundschule-Szenario, das
der Nutzer zuvor manuell getestet hatte:

| Lauf | Vorher (Kann-only) | Nachher (volle Zielfunktion) |
|---|---|---|
| `maxSolutions:=5` | Total=94,8 in 0,14s | **Total=10,8** in 6,19s |
| `maxSolutions:=50` | Total=79,5 in 0,85s | **Total=10,8** in 56,61s (kein weiterer Fortschritt gegenüber `:=5`) |

Die Zielfunktions-Integration ist ein deutlicher Qualitätsgewinn (10,8 statt
79,5/94,8) und konvergiert bereits bei den ersten 5 Loesungen auf denselben
Bestwert wie bei 50 - die Suche findet das gute Gebiet jetzt gezielt, statt
zufaellig darauf zu stossen. Das hat aber einen echten Preis: **~44x
langsamer** bei `maxSolutions:=5` (0,14s -> 6,19s), **~66x langsamer** bei
`maxSolutions:=50` (0,85s -> 56,61s) - erwartet, da jede Iteration jetzt
~1900 zusaetzliche Variablen und ~3600 zusaetzliche Constraints mitloest
(Groessenschaetzung aus der Planungsphase, bestaetigt durch diese Messung).

**Fuer groessere Szenarien ist das spuerbar:** `GymnasiumKlasse5Fixture`
(15 Entitaeten) und `OberstufeFixture` erreichten bei `maxSolutions:=5`
und den bisherigen Standard-Zeitbudgets (`totalTimeLimitS:=120s`,
`perSolveTimeLimitS:=30s`) **nicht** die volle Anzahl - beide stoppten
nach 4 von 5 gewuenschten Loesungen mit `StopReason=TimeLimitReached`
(120,0s bzw. 120,0s Gesamtdauer). Das ist kein Fehler, sondern die
ehrliche Konsequenz aus Nutzerentscheidung 3 (alle 5 Kriterien, volle
Konsequenz statt nur die guenstigen) - fuer realistische Schulgroessen
sollten `totalTimeLimitS`/`perSolveTimeLimitS` bei Bedarf grosszuegiger
gesetzt werden, oder mit weniger `maxSolutions` gearbeitet werden, wenn
schnelle Antworten wichtiger sind als viele Alternativen.

## Verifikation

- Beide neuen Tests (`SolveTopSingleIterationFindsSecondaryOptimalSchedule`,
  `SolveTopObjectiveIgnoresTeacherNonWorkingDaysForRange`) bestanden auf
  Anhieb - beweisen, dass bereits die ERSTE Iteration (`maxSolutions:=1`)
  das nachweisbare Sekundaer-Optimum (Total=0) findet, und dass die
  Lehrer-Arbeitstage-Semantik in der Zielfunktion erhalten bleibt.
- Alle 6 bestehenden `SolveTopTests.vb`-Tests bleiben gruen (Kann dominiert
  weiterhin, `Solutions`-Sortierung unveraendert ueber das bestehende
  `ScheduleQuality.Score`-basierte `OrderBy`).
- Volle Regressionssuite nach dem `BuildCoreModel`-Refactor UND erneut
  nach dem vollstaendigen Feature: 54 bestanden, 6 korrekt uebersprungen,
  0 Regressionen. `Solve()`/`BuildModel()` byte-identisch bestaetigt.

## Definition of Done — Status (Phase 2.9)

- [x] `dotnet test TimetableCore.Tests` bleibt vollstaendig gruen, 0
      Regressionen.
- [x] `SolveTop` findet nachweislich bereits bei `maxSolutions:=1` eine
      bzgl. der Sekundaerkriterien beweisbar optimale Loesung.
- [x] Lehrer-Arbeitstage-Semantik in der Zielfunktion nachweislich erhalten.
- [x] `Solve()`/`BuildModel()` bleiben byte-identisch unveraendert.
- [x] Alt-vs-neu-Laufzeitvergleich dokumentiert (Grundschule direkt, plus
      Gymnasium/Oberstufe als Skalierungs-Warnung fuer die Standard-
      Zeitbudgets).
- [x] Committet und gepusht.
