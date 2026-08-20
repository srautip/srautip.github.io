' Ported 1:1 from tests/test_gymnasium_klasse5.py. Integration test for the
' larger, more realistic Gymnasium-Klasse-5 scenario (4 Klassen, 9 Faecher,
' 15 Lehrkraefte, 5 Fachraeume - see GymnasiumKlasse5Fixture.vb for details
' and caveats).
'
' This exercises the full pipeline (validate -> solve -> verify -> format)
' under a load an order of magnitude larger than FullScenarioFixture.vb,
' including room contention shared across two different teachers (Kunst,
' BNT), a shared single-room pool across four classes (Musik), and two
' independent teacher-availability restrictions.
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class GymnasiumKlasse5Tests

    <TestMethod>
    Public Sub GymnasiumKlasse5EntitiesAreValid()
        Dim data = BuildGymnasiumKlasse5Scenario()
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count)
        Assert.AreEqual(0, Validation.CoverageWarnings(data).Count)
    End Sub

    <TestMethod>
    Public Sub GymnasiumKlasse5SolvesAndVerifiesClean()
        Dim data = BuildGymnasiumKlasse5Scenario()
        Dim r = Solver.Solve(data, timeLimitS:=60)
        Assert.IsTrue(r.Status = CpSolverStatus.Optimal OrElse r.Status = CpSolverStatus.Feasible, Solver.StatusName(r.Status))

        Dim expectedHours = JsonHelpers.Constraints(data).
            Where(Function(c) JsonHelpers.GetString(c, "type") = "weekly_hours").
            Sum(Function(c) JsonHelpers.GetInt(c, "hours_per_week").Value)
        Assert.AreEqual(expectedHours, r.Schedule.Count)

        Dim violations = Verifier.VerifySchedule(data, r.Schedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))
    End Sub

    <TestMethod>
    Public Sub GymnasiumKlasse5RendersWithoutError()
        Dim data = BuildGymnasiumKlasse5Scenario()
        Dim r = Solver.Solve(data, timeLimitS:=60)
        Dim text = Formatting.FormatSchedule(data, r.Schedule)
        For Each cls In JsonHelpers.AsStringList(JsonHelpers.Entities(data), "classes")
            Assert.IsTrue(text.Contains($"=== {cls} ==="))
        Next
    End Sub

End Class
