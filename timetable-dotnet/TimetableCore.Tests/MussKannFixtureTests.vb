' Phase 2.6: LLM-free pre-check for MussKannFixture.vb's own numbers, in the
' same spirit as GymnasiumKlasse5Tests.vb - catches an internally
' contradictory fixture (e.g. a block_length/max_per_day/hours_per_week
' mismatch) cheaply, before any live Ollama time is spent on it.
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class MussKannFixtureTests

    <TestMethod>
    Public Sub MussKannEntitiesAreValid()
        Dim data = MussKannFixture.BuildMussKannScenario()
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count)
        Assert.AreEqual(0, Validation.CoverageWarnings(data).Count)
    End Sub

    <TestMethod>
    Public Sub MussKannSolvesAndVerifiesClean()
        Dim data = MussKannFixture.BuildMussKannScenario()
        Dim r = Solver.Solve(data, timeLimitS:=30)
        Assert.IsTrue(r.Status = CpSolverStatus.Optimal OrElse r.Status = CpSolverStatus.Feasible, Solver.StatusName(r.Status))

        Dim violations = Verifier.VerifySchedule(data, r.Schedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))
    End Sub

End Class
