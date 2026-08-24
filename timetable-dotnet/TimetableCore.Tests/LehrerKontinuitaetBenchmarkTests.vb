' Phase 2.14: manueller Benchmark fuer die Lehrerkontinuitaet Kl.5->Kl.6
' (LehrerKontinuitaetFixture.vb) - loest ein "Vorjahresstundenplan" (Kl.5,
' tatsaechlich geloest und verifiziert sauber), leitet daraus die
' Lehrerzuordnung fuer das aktuelle Jahr (Kl.6) ab, loest dieses ebenfalls,
' und gibt eine Kontinuitaets-Uebersicht plus Stundenplaene fuer einen
' Beispiel-Zug beider Jahre aus.
'
' Bewusst UNGEGATET (kein RUN_SLOW_BENCHMARKS noetig, anders als die
' SolveTop-Benchmarks in GsgCompleteScenarioSolveTopTests.vb): live
' gemessen (Phase 2.14a) loest Kl.5 [5 Klassen, 45 Lehrer] in ~1,3s ueber
' die schnelle Kann-only Solve() - keine SolveTop-Zielfunktion involviert,
' also keine Gefahr fuer die Standard-Suite-Laufzeit.
Imports System.Diagnostics
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class LehrerKontinuitaetBenchmarkTests

    Private Shared Function ClassText(cell As GridCell) As String
        If cell Is Nothing Then Return "-"
        Return $"{cell.Subject} ({cell.Teacher})"
    End Function

    <TestMethod>
    Public Sub KlasseSechsUebernimmtLehrerAusVorjahresstundenplan()
        ' --- Vorjahresstundenplan: Kl.5 loesen ---
        Dim klasse5Data = LehrerKontinuitaetFixture.BuildKlasse5Scenario()
        Dim sw5 = Stopwatch.StartNew()
        Dim klasse5Result = Solver.Solve(klasse5Data, timeLimitS:=30)
        sw5.Stop()
        Assert.IsTrue(klasse5Result.Status = CpSolverStatus.Optimal OrElse klasse5Result.Status = CpSolverStatus.Feasible, Solver.StatusName(klasse5Result.Status))
        Dim klasse5Violations = Verifier.VerifySchedule(klasse5Data, klasse5Result.Schedule)
        Assert.AreEqual(0, klasse5Violations.Count, String.Join(vbLf, klasse5Violations))
        Console.WriteLine($"Kl.5 (Vorjahresstundenplan): {klasse5Result.Status}, {sw5.Elapsed.TotalSeconds:F2}s, 0 Verstoesse.")

        ' --- Lehrerzuordnung fuer Kl.6 aus dem Vorjahresstundenplan ableiten ---
        Dim vorjahrMap = LehrerKontinuitaetFixture.DeriveVorjahresTeacherMap(klasse5Result.Schedule)

        ' --- Kl.6 (aktuelles Jahr) loesen ---
        Dim klasse6Data = LehrerKontinuitaetFixture.BuildKlasse6Scenario(vorjahrMap)
        Dim sw6 = Stopwatch.StartNew()
        Dim klasse6Result = Solver.Solve(klasse6Data, timeLimitS:=30)
        sw6.Stop()
        Assert.IsTrue(klasse6Result.Status = CpSolverStatus.Optimal OrElse klasse6Result.Status = CpSolverStatus.Feasible, Solver.StatusName(klasse6Result.Status))
        Dim klasse6Violations = Verifier.VerifySchedule(klasse6Data, klasse6Result.Schedule)
        Assert.AreEqual(0, klasse6Violations.Count, String.Join(vbLf, klasse6Violations))
        Console.WriteLine($"Kl.6 (aktuelles Jahr): {klasse6Result.Status}, {sw6.Elapsed.TotalSeconds:F2}s, 0 Verstoesse.")

        ' --- Kontinuitaets-Uebersicht ---
        Dim klasse6Assignments = LehrerKontinuitaetFixture.BuildKlasse6Assignments(vorjahrMap)
        Dim report = LehrerKontinuitaetFixture.BuildContinuityReport(vorjahrMap, klasse6Assignments)

        Console.WriteLine(vbLf & "=== Lehrerkontinuitaet Kl.5 -> Kl.6 ===")
        Console.WriteLine($"{"Zug",-4} | {"Fach",-14} | {"Kl.5-Lehrer",-18} | {"Kl.6-Lehrer",-20} | Status")
        Console.WriteLine(New String("-"c, 75))
        For Each row In report
            Dim status = If(row.IsNewSubject, "NEU (kein Vorjahr)", "gleich")
            Console.WriteLine($"{row.Zug,-4} | {row.Subject,-14} | {If(row.Kl5Teacher, "-"),-18} | {row.Kl6Teacher,-20} | {status}")
        Next

        Dim persisted = report.Where(Function(r) Not r.IsNewSubject).ToList()
        Dim neu = report.Where(Function(r) r.IsNewSubject).ToList()
        Dim kontinuierlich = persisted.Where(Function(r) r.Kl5Teacher = r.Kl6Teacher).Count()
        Console.WriteLine(vbLf & $"Fortbestehende Faecher mit gleichem Lehrer: {kontinuierlich}/{persisted.Count} " &
            $"({100.0 * kontinuierlich / persisted.Count:F0}%). Neue Faecher (kein Vorjahreslehrer moeglich): {neu.Count}.")

        ' Harte Assertion: JEDES fortbestehende Fach behaelt exakt denselben
        ' Lehrer, JEDES neue Fach hat garantiert KEINEN Vorjahreslehrer.
        For Each row In persisted
            Assert.AreEqual(row.Kl5Teacher, row.Kl6Teacher, $"Zug {row.Zug}, Fach {row.Subject}: Lehrer haette gleich bleiben muessen.")
        Next
        For Each row In neu
            Assert.IsNull(row.Kl5Teacher, $"Zug {row.Zug}, Fach {row.Subject}: sollte keinen Vorjahreslehrer haben.")
        Next

        ' --- Stundenplaene fuer Zug "a" beider Jahre nebeneinander ---
        Dim days = LehrerKontinuitaetFixture.Days
        Dim periods = Enumerable.Range(1, LehrerKontinuitaetFixture.PeriodsPerDay).ToList()

        Dim klasse5Grids = Formatting.ToClassGrids(klasse5Data, klasse5Result.Schedule)
        Console.WriteLine(vbLf & Formatting.FormatGrid("5a (Vorjahr)", klasse5Grids("5a"), days, periods, AddressOf ClassText))

        Dim klasse6Grids = Formatting.ToClassGrids(klasse6Data, klasse6Result.Schedule)
        Console.WriteLine(vbLf & Formatting.FormatGrid("6a (aktuell)", klasse6Grids("6a"), days, periods, AddressOf ClassText))
    End Sub

End Class
