' Manueller/gegateter Benchmark: dasselbe komplette GSG-Szenario wie
' GsgCompleteScenarioTests (Sek I + Kursstufe, Kl. 5-12), aber ueber
' Solver.SolveTop statt Solver.Solve fuer jeden Tag/Periode-Solve-Schritt -
' zeigt, wie sich Luecken/Kompaktheit veraendern, wenn die Kann-only-Suche
' durch die volle Bewertungskriterien-Zielfunktion (Phase 2.9's
' ScheduleQuality/SolveTopObjective, u.a. ClassGapCount) ersetzt wird.
'
' Bewusst NICHT Teil der automatisierten Standard-Suite - mirrort die
' bereits in RealSchoolFixtureTests.vb dokumentierte Entscheidung
' ("SolveTop bei dieser Groesse bleibt manuelles/interaktives
' Ausprobieren, kein fester Bestandteil der automatisierten Suite", siehe
' docs/phase2-8-multi-solution.md). Gegatet ueber RUN_SLOW_BENCHMARKS=1,
' gleiche Assert.Inconclusive-Konvention wie die RUN_LLM_TESTS-gegateten
' Live-Tests.
'
' Solver.SolveKursstufe() selbst nutzt fuer Stufe B/C (Schienenraster/
' Raumzuordnung) intern Solve(), nicht SolveTop() - dieser Test baut die
' 3-Stufen-Pipeline daher HIER manuell nach (Kursblockung unveraendert -
' das ist kein Tag/Periode-Modell und hat kein Luecken-Konzept;
' Schienenraster/Raumzuordnung ueber SolveTop statt Solve), statt
' Solver.SolveKursstufe selbst zu aendern.
Imports System.Diagnostics
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class GsgCompleteScenarioSolveTopTests

    Private Shared Function ClassText(cell As GridCell) As String
        If cell Is Nothing Then Return "-"
        Return $"{cell.Subject} ({cell.Teacher})"
    End Function

    Private Shared Function TeacherText(cell As GridCell) As String
        If cell Is Nothing Then Return "-"
        Return $"{cell.ClassName} {cell.Subject}"
    End Function

    Private Shared Function WahlprofilText(cell As GridCell) As String
        If cell Is Nothing Then Return "-"
        Return $"{cell.ClassName}: {cell.Subject} ({cell.Teacher})"
    End Function

    ''' <summary>Time budgets are a best-effort cap, not a tuned value -
    ''' SolveTop's quality objective is documented (Phase 2.9) as 44-66x
    ''' slower per iteration than plain Solve on much smaller fixtures.
    ''' maxSolutions:=1 means exactly one CP-SAT solve per stage, bounded
    ''' by these limits - whatever it finds within budget (Feasible or
    ''' Optimal) is reported, same "no promise of proven optimality"
    ''' honesty as every other SolveTop use in this project.
    '''
    ''' numWorkers:=4 (this sandbox has 4 CPU cores) - a first attempt
    ''' with the project's usual numWorkers:=1 (deterministic, used
    ''' everywhere else for reproducibility) found ZERO feasible solutions
    ''' for the 30-class Sek-I part even after 30 minutes; CP-SAT's
    ''' parallel portfolio search (multiple differing strategies racing on
    ''' separate cores) trades that determinism away in exchange for
    ''' finding a first feasible solution faster on hard models - the
    ''' right tradeoff for a one-off manual benchmark, wrong for an
    ''' automated regression test (hence still numWorkers:=1 everywhere
    ''' else in this project).</summary>
    <TestMethod>
    Public Sub CompleteGsgScenarioSolveTopBenchmark()
        If Environment.GetEnvironmentVariable("RUN_SLOW_BENCHMARKS") <> "1" Then
            Assert.Inconclusive(
                "Manueller SolveTop-Benchmark uebersprungen (kann mehrere Minuten dauern, " &
                "kein fester Bestandteil der Standard-Suite). Set RUN_SLOW_BENCHMARKS=1 to run it.")
        End If

        ' --- Sek I ueber SolveTop ---
        Dim sekI = GymnasiumSekIFixture.BuildGymnasiumSekIScenario()
        Dim sw = Stopwatch.StartNew()
        Dim sekITop = Solver.SolveTop(sekI, maxSolutions:=1, totalTimeLimitS:=1200, perSolveTimeLimitS:=1200, numWorkers:=4)
        sw.Stop()
        Assert.IsTrue(sekITop.Solutions.Count > 0, $"Sek I: kein Solve gefunden - StopReason={sekITop.StopReason}")
        Dim sekIBest = sekITop.Solutions(0)
        Console.WriteLine(
            $"Sek I SolveTop: {sw.Elapsed.TotalSeconds:F1}s, StopReason={sekITop.StopReason}, " &
            $"Quality.Total={sekIBest.Quality.Total}, ClassGapCount={sekIBest.Quality.ClassGapCount}, " &
            $"TeacherGapCount={sekIBest.Quality.TeacherGapCount}, EdgePeriodCount={sekIBest.Quality.EdgePeriodCount}, " &
            $"ClassLoadVariance={sekIBest.Quality.ClassLoadVariance:F2}, TeacherLoadVariance={sekIBest.Quality.TeacherLoadVariance:F2}")

        Dim sekIViolations = Verifier.VerifySchedule(sekI, sekIBest.Schedule)
        Assert.AreEqual(0, sekIViolations.Count, String.Join(vbLf, sekIViolations))

        Dim sekIEnt = JsonHelpers.Entities(sekI)
        Dim sekIDays = JsonHelpers.AsStringList(JsonHelpers.Timeslots(sekIEnt), "days")
        Dim sekIPeriods = Enumerable.Range(1, JsonHelpers.GetInt(JsonHelpers.Timeslots(sekIEnt), "periods_per_day").Value).ToList()
        Dim classGrids = Formatting.ToClassGrids(sekI, sekIBest.Schedule)
        Console.WriteLine(vbLf & Formatting.FormatGrid("5d (SolveTop)", classGrids("5d"), sekIDays, sekIPeriods, AddressOf ClassText))

        Dim teachers = JsonHelpers.AsStringList(sekIEnt, "teachers")
        Dim teacher73 = teachers(72)
        Dim teacherGrids = Formatting.ToTeacherGrids(sekI, sekIBest.Schedule)
        Console.WriteLine(vbLf & $"Lehrer 73 = {teacher73}")
        Console.WriteLine(Formatting.FormatGrid(teacher73 & " (SolveTop)", teacherGrids(teacher73), sekIDays, sekIPeriods, AddressOf TeacherText))

        ' --- Kursstufe: Kursblockung unveraendert (kein Tag/Periode-
        ' Modell), Schienenraster/Raumzuordnung manuell ueber SolveTop
        ' statt Solve nachgebaut ---
        Dim kursstufe = KursstufeFixture.BuildKursstufeScenario()
        Dim kb = Kursblockung.SolveKursblockung(kursstufe, timeLimitS:=60)
        Assert.IsTrue(kb.Status = CpSolverStatus.Optimal OrElse kb.Status = CpSolverStatus.Feasible)

        Dim schienenScenario = Schienenraster.BuildSchienenrasterScenario(kursstufe, kb.Assignment)
        Dim schienenTop = Solver.SolveTop(schienenScenario, maxSolutions:=1, totalTimeLimitS:=300, perSolveTimeLimitS:=300, numWorkers:=4)
        Assert.IsTrue(schienenTop.Solutions.Count > 0, $"Schienenraster: kein Solve gefunden - StopReason={schienenTop.StopReason}")
        Dim schienenBest = schienenTop.Solutions(0)

        Dim raumScenario = Raumzuordnung.BuildRaumzuordnungScenario(kursstufe, kb.Assignment, schienenBest.Schedule)
        Dim raumTop = Solver.SolveTop(raumScenario, maxSolutions:=1, totalTimeLimitS:=300, perSolveTimeLimitS:=300, numWorkers:=4)
        Assert.IsTrue(raumTop.Solutions.Count > 0, $"Raumzuordnung: kein Solve gefunden - StopReason={raumTop.StopReason}")
        Dim raumBest = raumTop.Solutions(0)
        Assert.AreEqual(0, Verifier.VerifySchedule(raumScenario, raumBest.Schedule).Count)

        Dim kEnt = JsonHelpers.Entities(kursstufe)
        Dim kDays = JsonHelpers.AsStringList(JsonHelpers.Timeslots(kEnt), "days")
        Dim kPeriods = Enumerable.Range(1, JsonHelpers.GetInt(JsonHelpers.Timeslots(kEnt), "periods_per_day").Value).ToList()
        Dim wpGrids = Formatting.ToWahlprofilGrids(kursstufe, raumBest.Schedule)
        Console.WriteLine(vbLf & Formatting.FormatGrid("WP3 (SolveTop)", wpGrids("WP3"), kDays, kPeriods, AddressOf WahlprofilText))
    End Sub

End Class
