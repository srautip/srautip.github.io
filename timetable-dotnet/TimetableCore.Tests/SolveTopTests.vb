' Phase 2.8: deterministic tests for Solver.SolveTop - no LLM/Ollama
' involved, everything here is a checkable fact about CP-SAT's behavior.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class SolveTopTests

    ''' <summary>Only one day/period exists for the single required lesson
    ''' -> exactly one possible Lesson-variable assignment.</summary>
    <TestMethod>
    Public Sub SingleFeasibleSolutionReturnsExactlyOne()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim result = Solver.SolveTop(data, maxSolutions:=10, totalTimeLimitS:=30, perSolveTimeLimitS:=10)
        Assert.AreEqual(1, result.Solutions.Count)
        Assert.AreEqual(MultiSolveStopReason.SearchSpaceExhausted, result.StopReason)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, result.Solutions(0).Schedule).Count)
    End Sub

    ''' <summary>2 days x 1 period, 1 hour/week -> exactly 2 distinct
    ''' Lesson assignments (Mo or Di). A "should" forbidden_slot on Mo
    ''' means the Di placement has 0 Kann violations, the Mo placement 1 -
    ''' proving both the sort order and the non-decreasing KannViolation
    ''' count across the ranked list end-to-end.</summary>
    <TestMethod>
    Public Sub MultipleDistinctSolutionsSortedByQuality()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}, {"priority", "should"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim result = Solver.SolveTop(data, maxSolutions:=10, totalTimeLimitS:=30, perSolveTimeLimitS:=10)
        Assert.AreEqual(2, result.Solutions.Count)
        Assert.AreEqual(MultiSolveStopReason.SearchSpaceExhausted, result.StopReason)

        For i = 1 To result.Solutions.Count - 1
            Assert.IsTrue(result.Solutions(i - 1).Quality.Total <= result.Solutions(i).Quality.Total)
            Assert.IsTrue(result.Solutions(i - 1).Quality.KannViolationCount <= result.Solutions(i).Quality.KannViolationCount)
        Next

        Assert.AreEqual(0, result.Solutions(0).Quality.KannViolationCount)
        Assert.AreEqual("Di", result.Solutions(0).Schedule(0).Day)
        Assert.AreEqual(1, result.Solutions(1).Quality.KannViolationCount)
        Assert.AreEqual("Mo", result.Solutions(1).Schedule(0).Day)

        For Each s In result.Solutions
            Assert.AreEqual(0, Verifier.VerifySchedule(data, s.Schedule).Count)
        Next
    End Sub

    ''' <summary>5 independent single-period days, no Kann constraints -> 5
    ''' distinct solutions exist, but maxSolutions caps the search at 3.</summary>
    <TestMethod>
    Public Sub MaxSolutionsCapTest()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di", "Mi", "Do", "Fr"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim result = Solver.SolveTop(data, maxSolutions:=3, totalTimeLimitS:=60, perSolveTimeLimitS:=10)
        Assert.AreEqual(3, result.Solutions.Count)
        Assert.AreEqual(MultiSolveStopReason.MaxSolutionsReached, result.StopReason)
    End Sub

    ''' <summary>Non-flaky by design (see plan), refined after live
    ''' diagnostics: betting on a solution space "too large to exhaust in
    ''' a few seconds" turned out unreliable - CP-SAT's search proved even
    ''' fairly large scenarios (BuildFullScenario's ~120-solution
    ''' exhaustion point, observed via an isolated diagnostic) "exhausted"
    ''' within ~2s, faster than naive combinatorics suggested. That
    ''' diagnostic also exposed a real SolveTop bug (now fixed): a
    ''' per-solve CpSolverStatus.Unknown (its own time budget ran out
    ''' inconclusively) was mislabeled as SearchSpaceExhausted instead of
    ''' TimeLimitReached. This test now uses BuildFullScenario (the
    ''' heaviest fixture available, for the largest achievable per-
    ''' iteration cost) with a deliberately tiny time budget (50ms) far
    ''' below its observed ~16ms/iteration x ~120-iteration exhaustion
    ''' cost - the budget runs out (via either path above) within the
    ''' first few iterations, regardless of the true exhaustion point.
    ''' Asserts a generous upper bound on ElapsedS rather than tight
    ''' timing.</summary>
    <TestMethod>
    Public Sub TotalTimeLimitCapTest()
        Dim data = BuildFullScenario()
        Dim totalTimeLimitS = 0.05
        Dim perSolveTimeLimitS = 0.05
        Dim result = Solver.SolveTop(data, maxSolutions:=100000, totalTimeLimitS:=totalTimeLimitS, perSolveTimeLimitS:=perSolveTimeLimitS)
        Assert.AreEqual(MultiSolveStopReason.TimeLimitReached, result.StopReason)
        Assert.IsTrue(result.Solutions.Count < 100000)
        Assert.IsTrue(result.ElapsedS <= totalTimeLimitS + perSolveTimeLimitS + 2.0,
            $"ElapsedS={result.ElapsedS} exceeded the generous upper bound")
    End Sub

    ''' <summary>Same (data, seed, numWorkers:=1) called twice must produce
    ''' identical Solutions sequences - mirrors this project's existing
    ''' single-solve determinism-test practice (SolverTests.vb, point 5).</summary>
    <TestMethod>
    Public Sub DeterminismAcrossRepeatedCalls()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di", "Mi", "Do", "Fr"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r1 = Solver.SolveTop(data, maxSolutions:=5, totalTimeLimitS:=30, perSolveTimeLimitS:=10, seed:=42, numWorkers:=1)
        Dim r2 = Solver.SolveTop(data, maxSolutions:=5, totalTimeLimitS:=30, perSolveTimeLimitS:=10, seed:=42, numWorkers:=1)

        Assert.AreEqual(r1.Solutions.Count, r2.Solutions.Count)
        For i = 0 To r1.Solutions.Count - 1
            Assert.AreEqual(r1.Solutions(i).Quality.Total, r2.Solutions(i).Quality.Total)
            Dim s1 = r1.Solutions(i).Schedule.Select(Function(l) (l.ClassName, l.Subject, l.Teacher, l.Day, l.Period, l.Room)).ToList()
            Dim s2 = r2.Solutions(i).Schedule.Select(Function(l) (l.ClassName, l.Subject, l.Teacher, l.Day, l.Period, l.Room)).ToList()
            CollectionAssert.AreEqual(s1, s2)
        Next
    End Sub

    ''' <summary>Every returned candidate must independently verify clean
    ''' on Muss constraints, regardless of how many candidates SolveTop
    ''' returns - mirrors MussKannFixtureTests.vb's "always re-verify
    ''' independently" pattern.</summary>
    <TestMethod>
    Public Sub AllCandidatesVerifyCleanOnMuss()
        Dim data = BuildFullScenario()
        Dim result = Solver.SolveTop(data, maxSolutions:=3, totalTimeLimitS:=60, perSolveTimeLimitS:=20)
        Assert.IsTrue(result.Solutions.Count > 0)
        For Each s In result.Solutions
            Dim violations = Verifier.VerifySchedule(data, s.Schedule)
            Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))
        Next
    End Sub

End Class
