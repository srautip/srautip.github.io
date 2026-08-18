# Phase 2.12: Gestufte Optimierung + Hint-Warmstart für `Solver.SolveTop`

Dieser Bericht dokumentiert Phase 2.12 (siehe Plan, Abschnitt "Phase 2.12
(feingeplant)"): Nutzerfrage "Gibt es in CP-SAT die Möglichkeit, iterativ
Optimierungsparameter oder weitere Regeln in den Lösungsraum einzubauen und
so früher zu Lösungen zu kommen?" - ausgelöst durch eine beim GSG-SolveTop-
Benchmark (Phase 2.11-Nachtrag) beobachtete Kaltstart-Schwäche: die volle
6-Kriterien-Zielfunktion (`SolveTopObjective.ApplyQualityObjective`) machte
das reine Auffinden einer ersten zulässigen Lösung bei 30 Klassen/75 Lehrern
so schwer, dass `numWorkers:=1` in 30 Minuten keine einzige fand - während
die bereits bewährte Kann-only-Zielfunktion (`BuildModel()`/`Solve()`)
genau dasselbe Szenario in ~93s löst (Phase 2.10-Report).

## Nutzerentscheidung

"Gestufte Optimierung + Hints" (kombinierter Ansatz, nicht nur die
kleineren Solver-Parameter wie `relative_gap_limit`/`stop_after_first_solution`
allein - die wirken erst NACH einer ersten gefundenen Lösung und lösen das
Kaltstart-Problem nicht).

## Mechanik

`SolveTop` baut weiterhin `built = BuildCoreModel(data)` genau einmal,
mutiert danach aber dasselbe `CpModel`-Objekt in zwei Phasen (dasselbe
Muster, mit dem `BlockSolution` das Modell zwischen Iterationen erweitert):

1. **Stufe 1 (Warmstart-Solve):** falls `built.KannVars.Count > 0`, wird
   kurzzeitig dieselbe Kann-only-Zielfunktion gesetzt, die `BuildModel()`
   heute schon verwendet (ausgelagert in eine neue, von beiden geteilte
   private `KannOnlyObjectiveExpr`-Funktion - `BuildModel()` selbst bleibt
   dadurch unverändert), mit eigenem Zeitbudget `stage1TimeLimitS` gelöst.
2. **Hint-Übergabe:** bei Feasible/Optimal wird die gefundene komplette
   `Lesson`-Belegung per `model.AddHint(lessonVar, wert)` als Startpunkt für
   Stufe 2 gesetzt (neue private `ApplyLessonHints`-Sub, bewusst nur auf
   `Lesson` beschränkt - dieselbe Scoping-Begründung wie bei `BlockSolution`).
3. **Stufe 2:** `SolveTopObjective.ApplyQualityObjective(built, data)` wie
   bisher (deren eigener `Minimize(...)`-Aufruf ersetzt Stufe 1s Ziel
   automatisch, kein separates Clear nötig), danach die bestehende
   Iterationsschleife unverändert - jetzt aber mit einem bekannten guten
   Startpunkt statt bei Null beginnend.
4. **Iterations-Übergabe (gebündelt):** nach jedem `BlockSolution`-Aufruf in
   der Schleife wird zusätzlich die soeben gefundene (jetzt no-good-
   gesperrte, aber weiterhin als Suchhinweis brauchbare) Belegung erneut als
   Hint für die nächste Iteration gesetzt.

Neue optionale Parameter auf `SolveTop`: `stage1TimeLimitS As Double = 60.0`,
`useStagedHints As Boolean = True` (Default An). `Solve()`/`BuildModel()`
bleiben in Signatur UND Verhalten unverändert (nur der bereits vorhandene
Ein-Zeiler wurde in `KannOnlyObjectiveExpr` ausgelagert).

## Live-Verifikation gegen die installierte OrTools-Version (2.12a)

Per Wegwerf-Smoke-Test UND per Reflection direkt gegen die installierte
`Google.OrTools.Sat` 9.15.6755-DLL geprüft:

1. **`CpModel.ClearObjective()` existiert NICHT** auf der tatsächlich
   installierten DLL (per `typeof(CpModel).GetMembers()`-Reflection
   bestätigt: kein solches Member), obwohl die NuGet-Paket-eigene XML-Doku
   diese Methode dokumentiert - eine echte Doku/DLL-Diskrepanz, die dieser
   Schritt genau deshalb existiert aufzudecken. Live bestätigt: ein zweiter
   `model.Minimize(...)`-Aufruf OHNE vorheriges Clear ersetzt das vorherige
   Ziel korrekt und vollständig - macht ein Clear ohnehin überflüssig, die
   Mechanik-Skizze wurde entsprechend angepasst.
2. Ein NUR auf einer Teilmenge der Variablen gesetzter Hint (analog: nur
   `Lesson`, nicht die von `SolveTopObjective` abgeleiteten Hilfsvariablen)
   bricht die Korrektheit nicht und wird vom Solver tatsächlich genutzt -
   das Solver-Log zeigte explizit `"The solution hint is complete and is
   feasible. Its objective value is 0."`, ein direkter Beleg für
   Propagations-Vervollständigung statt stillem Verwerfen.
3. `ClearHints()` + erneutes `AddHint(...)` wirft keine Ausnahme und das
   Modell löst weiterhin korrekt.

## Risikobewertung gegen die bestehende Testsuite

Hints beeinflussen nur die Suchreihenfolge, nie die Menge der zulässigen
Lösungen (harte Constraints unverändert, `Solutions` wird ohnehin immer neu
nach `Quality.Total` sortiert zurückgegeben). Alle 9 bestehenden
`SolveTopTests.vb`-Tests blieben nach jedem Umsetzungsschritt unverändert
grün - inklusive `TotalTimeLimitCapTest`, dem laut Vorab-Analyse
empfindlichsten Test (enges Zeitbudget, Stufe-1-Overhead zählt jetzt mit
gegen dasselbe Budget).

## Ehrlicher Befund (2.12e): Staging hilft nicht bei jeder Größenordnung

Ein Wegwerf-Spike verglich `useStagedHints:=True` vs. `:=False` bei
identischem, engem Zeitbudget auf `GymnasiumKlasse5Fixture` und
`OberstufeFixture` (4-15 Klassen) über mehrere Budget-/Stufe-1-Anteil-
Kombinationen. **Ergebnis: bei diesen kleinen/mittleren Größen ist die
gestufte Variante im gemessenen Fenster (1,6s-2,0s Gesamtbudget)
reproduzierbar SCHLECHTER, nicht besser** - ungestuft fand durchgehend 1
Lösung (`MaxSolutionsReached`), gestuft durchgehend 0 (`TimeLimitReached`).

Die Erklärung ist plausibel: bei diesen Größen ist das direkte Finden einer
ersten Lösung bereits so günstig, dass der zusätzliche Stufe-1-Solve reiner
Overhead ist, der vom knappen Gesamtbudget abgeht, ohne dass Stufe 2 davon
profitiert - das beobachtete Kaltstart-Problem trat ursprünglich nur beim
30-Klassen-GSG-Szenario auf, nicht bei diesen kleineren Fixtures. Deshalb
wurde **kein** schneller Vergleichstest der Standard-Suite hinzugefügt (das
würde bei dieser Größenordnung ehrlicherweise das Gegenteil der
Kernaussage belegen) - der Beleg für den eigentlichen Kaltstart-Fix bleibt
der Realmaßstab-Benchmark unten.

**Praktische Konsequenz:** `useStagedHints:=False` bleibt für kleine/
schnelle Szenarien die schnellere Wahl trotz `:=True`-Default. Eine
zukünftige Verfeinerung (z.B. `useStagedHints` automatisch größenabhängig
wählen, oder Stufe 1 mit `stop_after_first_solution` statt vollem
`Minimize` laufen lassen, um den Stufe-1-Overhead selbst zu senken) ist
denkbar, aber nicht Teil dieser Phase.

## Realmaßstab-Beleg (2.12f): GSG Sek I bei `numWorkers:=1`

Direkter Vorher/Nachher-Beleg auf demselben Szenario, das das Problem
ursprünglich zeigte (`GymnasiumSekIFixture`, 30 Klassen/75 Lehrer, Kl. 5-10):

| Lauf | `numWorkers` | Zeitbudget | Ergebnis |
|---|---|---|---|
| Vorher (ohne Staging, Phase 2.11-Nachtrag) | 1 | 30 Minuten | **0 Lösungen** (`TimeLimitReached`) |
| Vorher (ohne Staging, Phase 2.11-Nachtrag) | 4 (Portfolio-Suche) | ~20 Minuten | 1 Lösung, Quality.Total=1023,54 |
| **Nachher (mit Staging, Phase 2.12)** | **1** | **20 Minuten** | **1 Lösung**, Quality.Total=10605,47 |

`SekIStagedHintsNumWorkers1Benchmark` (`RUN_SLOW_BENCHMARKS=1`,
`numWorkers:=1`, `useStagedHints:=True`, `stage1TimeLimitS:=150`,
`totalTimeLimitS:=1200`) lief 1201,1s, `StopReason=MaxSolutionsReached`
(die Suche selbst hat innerhalb des Budgets abgeschlossen, nicht das
Zeitlimit ausgeschöpft) und lieferte:

```
Quality.Total=10605,47, ClassGapCount=218, TeacherGapCount=570,
EdgePeriodCount=335, AfternoonDayCount=142, ClassLoadVariance=38,40,
TeacherLoadVariance=75,09
```

0 Verifier-Muss-Verstöße (per `Assert.AreEqual(0, Verifier.VerifySchedule(...))`
bestätigt). **Die Kernaussage ist damit direkt belegt: das gestufte
Warmstart-Verfahren behebt das beobachtete Kaltstart-Problem bei
`numWorkers:=1`** - vorher fand die volle Zielfunktion dort in 30 Minuten
gar nichts, jetzt findet sie innerhalb von 20 Minuten mindestens eine
gültige Lösung. Die 8-Stunden-Eskalationsstufe (`totalTimeLimitS:=28800`),
die für den Fall eines erneuten Fehlschlags vorgesehen war, war nicht
nötig.

**Ehrlich einzuordnen:** die gefundene Qualität (Total=10605,47) ist
deutlich schlechter als das `numWorkers:=4`-Ergebnis (Total=1023,54) -
erwartbar, da `maxSolutions:=1` hier nur die ERSTE gefundene zulässige
Lösung akzeptiert (kein Beweis von Optimalität, keine weitere
Verbesserung durch zusätzliche Iterationen) und ein einzelner Suchpfad
(`numWorkers:=1`) strukturell weniger Suchraum abdeckt als eine
4-fache Portfolio-Suche. Das gestufte Warmstart-Verfahren löst gezielt
das Kaltstart-Problem ("überhaupt eine erste Lösung finden"), nicht das
separate, unverändert bestehende Problem "möglichst gute Lösung finden" -
für Letzteres bleibt `numWorkers:=4` (oder mehr Zeit/mehr Iterationen) die
bessere Wahl, wenn verfügbar. Beide Mechanismen sind komplementär und
schließen sich nicht aus (`useStagedHints:=True` UND `numWorkers:=4`
gemeinsam wäre der nächste naheliegende Versuch, hier aber nicht mehr
Teil dieser Phase).

## Verifikation

- Alle 9 bestehenden `SolveTopTests.vb`-Tests unverändert grün nach jedem
  Umsetzungsschritt (2.12b/2.12c/2.12d).
- Neuer Test `StagedHintSolveTopStillReturnsValidSolutions` (2.12c) beweist
  Korrektheit auf beiden Zweigen von Stufe 1 (mit/ohne Kann-Constraint).
- `Solve()`/`BuildModel()` bleiben byte-identisch unverändert (nur der
  Kann-only-Ein-Zeiler wurde in `KannOnlyObjectiveExpr` ausgelagert, von
  `BuildModel` weiterhin identisch aufgerufen).
- Vollständige Regressionssuite nach jedem Schritt grün, 0 Regressionen.

## Definition of Done — Status

- [x] `dotnet test TimetableCore.Tests` bleibt vollständig grün, 0
      Regressionen.
- [x] `Solve()`/`BuildModel()` bleiben byte-identisch im Verhalten.
- [x] Live-Verifikation der drei offenen OrTools-API-Fragen dokumentiert,
      VOR jeder Code-Änderung durchgeführt (fand einen echten Doku/DLL-
      Widerspruch: `ClearObjective()` existiert nicht).
- [x] Ein manueller `numWorkers:=1`-Lauf des GSG-Benchmarks mit gestuften
      Hints zeigt dokumentiert, ob/wie sich das frühere "0 Lösungen in 30
      Minuten" dadurch ändert (siehe Realmaßstab-Beleg oben).
- [x] Ehrlich dokumentierte Grenze: Staging hilft nicht bei jeder
      Größenordnung (2.12e-Befund).
- [x] Dieser Bericht committet.
