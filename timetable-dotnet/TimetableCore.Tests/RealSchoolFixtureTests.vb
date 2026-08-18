' Phase 2.10: LLM-free sanity checks for GrundschuleGrossFixture.vb and
' GymnasiumSekIFixture.vb - both are pure Solve()/SolveTop benchmark
' fixtures (no prompt text, no LLM involved), so this just proves each
' scenario's hand-chosen numbers are internally consistent: valid entity
' references and an actually solvable, Muss-clean timetable. Mirrors
' MussKannFixtureTests.vb's pattern.
'
' Only the fast, Kann-only Solve() is checked here, not the richer
' SolveTop quality objective (Phase 2.9) - at this scenario's real school
' scale (30 classes/75 teachers for the Gymnasium), SolveTop's heavier
' per-iteration objective would make the regular fast test suite slow/
' flaky. SolveTop benchmarking at this size is deliberately left as
' manual/interactive exploration (see docs/phase2-8-multi-solution.md's
' Phase 2.9 benchmarking approach) rather than an automated test.
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class RealSchoolFixtureTests

    <TestMethod>
    Public Sub GrundschuleGrossEntitiesAreValid()
        Dim data = GrundschuleGrossFixture.BuildGrundschuleGrossScenario()
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count)
    End Sub

    <TestMethod>
    Public Sub GrundschuleGrossSolvesAndVerifiesClean()
        Dim data = GrundschuleGrossFixture.BuildGrundschuleGrossScenario()
        Dim r = Solver.Solve(data, timeLimitS:=60)
        Assert.IsTrue(r.Status = CpSolverStatus.Optimal OrElse r.Status = CpSolverStatus.Feasible, Solver.StatusName(r.Status))

        Dim violations = Verifier.VerifySchedule(data, r.Schedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))
    End Sub

    <TestMethod>
    Public Sub GymnasiumSekIEntitiesAreValid()
        Dim data = GymnasiumSekIFixture.BuildGymnasiumSekIScenario()
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count)
    End Sub

    ''' <summary>Live-measured at ~93s to Optimal on the reference
    ''' machine (30 classes, 75 teachers, 885 scheduled lessons) - a
    ''' generous 150s budget leaves real headroom rather than cutting it
    ''' close to the observed time.</summary>
    <TestMethod>
    Public Sub GymnasiumSekISolvesAndVerifiesClean()
        Dim data = GymnasiumSekIFixture.BuildGymnasiumSekIScenario()
        Dim r = Solver.Solve(data, timeLimitS:=150)
        Assert.IsTrue(r.Status = CpSolverStatus.Optimal OrElse r.Status = CpSolverStatus.Feasible, Solver.StatusName(r.Status))

        Dim violations = Verifier.VerifySchedule(data, r.Schedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))
    End Sub

End Class
