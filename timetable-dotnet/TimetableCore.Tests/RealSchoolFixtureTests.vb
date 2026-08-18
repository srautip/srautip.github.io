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

    ''' <summary>Phase 2.11h: same LLM-free sanity pattern as the two
    ''' Sek-I fixtures above, but through the full 3-stage
    ''' Solver.SolveKursstufe pipeline (Kursblockung -> Schienenraster ->
    ''' Raumzuordnung) instead of the class-based Solve().</summary>
    <TestMethod>
    Public Sub KursstufeEntitiesAreValid()
        Dim data = KursstufeFixture.BuildKursstufeScenario()
        Assert.AreEqual(0, Validation.ValidateKursstufeEntities(data).Count)
    End Sub

    <TestMethod>
    Public Sub KursstufeKursblockungFeasible()
        Dim data = KursstufeFixture.BuildKursstufeScenario()
        Dim r = Kursblockung.SolveKursblockung(data, timeLimitS:=60)
        Assert.IsTrue(r.Status = CpSolverStatus.Optimal OrElse r.Status = CpSolverStatus.Feasible, Solver.StatusName(r.Status))
        Assert.AreEqual(0, Verifier.VerifyKursblockung(data, r.Assignment).Count)
    End Sub

    ''' <summary>End-to-end through all 3 stages. Note: `data` itself
    ''' only carries "kurswahl" constraints (no class-based types), so
    ''' Verifier.VerifySchedule(data, ...) would misinterpret them as
    ''' unknown constraint types - the meaningful independent re-check
    ''' here is VerifyKursblockung (stage A) plus re-deriving stage C's
    ''' own synthetic scenario (which DOES carry class-based constraints)
    ''' to verify the final room/teacher assignment, mirroring
    ''' VerifyKursblockungTests.ExistingVerifyScheduleDetectsRealViolationOnKursLevelData's pattern.</summary>
    <TestMethod>
    Public Sub KursstufeSolvesAndVerifiesClean()
        Dim data = KursstufeFixture.BuildKursstufeScenario()
        Dim r = Solver.SolveKursstufe(data, timeLimitS:=60)
        Assert.IsTrue(r.RaumzuordnungStatus = CpSolverStatus.Optimal OrElse r.RaumzuordnungStatus = CpSolverStatus.Feasible,
            $"Kursblockung={r.KursblockungStatus}, Schienenraster={r.SchienenrasterStatus}, Raumzuordnung={r.RaumzuordnungStatus}")
        Assert.IsNotNull(r.Schedule)

        Dim kb = Kursblockung.SolveKursblockung(data, timeLimitS:=60)
        Assert.AreEqual(0, Verifier.VerifyKursblockung(data, kb.Assignment).Count)

        Dim schienenResult = Solver.Solve(Schienenraster.BuildSchienenrasterScenario(data, kb.Assignment), timeLimitS:=60)
        Dim raumScenario = Raumzuordnung.BuildRaumzuordnungScenario(data, kb.Assignment, schienenResult.Schedule)
        Dim violations = Verifier.VerifySchedule(raumScenario, r.Schedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))
    End Sub

End Class
