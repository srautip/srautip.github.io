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

    ''' <summary>Phase 2.9: proves the objective integration, not just
    ''' lucky enumeration order, drives quality. 1 class, 1 teacher,
    ''' weekly_hours=2, 2 days x 4 periods, zero Kann constraints - every
    ''' feasible placement has Kann=0, but secondary quality varies a lot
    ''' depending on which periods/days are chosen. The joint optimum (1
    ''' lesson each on Mo/Di, both at a non-edge period) has ClassGapCount=0,
    ''' TeacherGapCount=0, EdgePeriodCount=0, and both ranges 0 - every term
    ''' in QualityScore.Total is non-negative, so Total=0 is provably the
    ''' floor, not merely "the best one seen so far". maxSolutions:=1 is
    ''' the crucial part: only ONE CP-SAT solve happens, so if Total=0
    ''' comes back, the in-model objective itself found it - no
    ''' multi-iteration ranking could get "lucky" with a single solve.</summary>
    <TestMethod>
    Public Sub SolveTopSingleIterationFindsSecondaryOptimalSchedule()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di"}, 4), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim result = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=10)
        Assert.AreEqual(1, result.Solutions.Count)
        Assert.AreEqual(0.0, result.Solutions(0).Quality.Total, 0.0000001)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, result.Solutions(0).Schedule).Count)
    End Sub

    ''' <summary>Phase 2.9: proves the trickiest semantic (teacher
    ''' working-days-only range, mirroring ScheduleQuality's
    ''' TeacherLoadVarianceOnlyCountsWorkingDays) survives the move into
    ''' the CP-SAT objective. T1 is only available Mo/Di (a declared
    ''' 2-of-3-day part-timer); weekly_hours=2 on those 2 days -> the best
    ''' schedule has 1 lesson each on Mo and Di (TeacherLoadVariance=0,
    ''' since only 2 real working days, evenly split). A WRONG "range over
    ''' all 3 days" implementation would instead treat Mi's forced-0 as
    ''' part of the range and never reach a 0 variance schedule.</summary>
    <TestMethod>
    Public Sub SolveTopObjectiveIgnoresTeacherNonWorkingDaysForRange()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di", "Mi"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "teacher_availability"}, {"teacher", "T1"}, {"available_days", New JsonArray From {"Mo", "Di"}}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim result = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=10)
        Assert.AreEqual(1, result.Solutions.Count)
        Assert.AreEqual(0.0, result.Solutions(0).Quality.TeacherLoadVariance, 0.0000001)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, result.Solutions(0).Schedule).Count)
    End Sub

    ''' <summary>Phase 2.11 (Nachtrag): proves the new AfternoonDayCount
    ''' criterion (WeightAfternoonDayCount) actually enters the objective
    ''' and jointly co-optimizes with the other 5 criteria - it does NOT
    ''' independently prove "fewer afternoon days always wins", because
    ''' at these weights it usually doesn't once ClassLoadVariance/
    ''' TeacherLoadVariance are also in play (see below).
    '''
    ''' 3 days x 8 periods, 20h/week for a single class/teacher pair -
    ''' periods 2-6 give only 15 "safe" slots, so exactly 5 lessons are
    ''' unavoidably forced into edge periods (period 1 or &gt;=7); that
    ''' floor is identical (EdgePeriodCount=5) for every valid
    ''' arrangement, so it cannot discriminate between them, and neither
    ''' can ClassGapCount/TeacherGapCount (every valid arrangement fills a
    ''' contiguous 1..N or 2..N block each day - hand-checked, always
    ''' gap=0). The only two REMAINING discriminators are AfternoonDayCount
    ''' (weight 5) and Range (weight 3+3=6, NOT just 3 - since this
    ''' scenario's one teacher's own schedule exactly mirrors the class's,
    ''' ClassLoadVariance's and TeacherLoadVariance's range proxies are
    ''' numerically identical in every arrangement here, doubling the
    ''' effective range weight - an easy trap to fall into by hand, this
    ''' test's first version missed exactly this and asserted the wrong
    ''' expected value). Exhaustively enumerating the only 3 possible
    ''' per-day load distributions summing to 5 across 3 days (each day's
    ''' load capped at 3 = 1 period-1 slot + 2 afternoon slots) gives:
    ''' loads (1,1,3): 1*5 + 2*6=17; (1,2,2): 2*5 + 1*6=16; (0,2,3): 2*5 +
    ''' 3*6=28. (1,2,2) - i.e. 2 days WITH an afternoon lesson, not 1 - is
    ''' the unique minimum, confirming SPREADING wins over bunching once
    ''' the mirrored teacher range is correctly accounted for.</summary>
    <TestMethod>
    Public Sub SolveTopObjectiveWeighsAfternoonDaysAgainstLoadRange()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di", "Mi"}, 8), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 20}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim result = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=60, perSolveTimeLimitS:=60, numWorkers:=1)
        Assert.AreEqual(1, result.Solutions.Count)
        Dim quality = result.Solutions(0).Quality
        Assert.AreEqual(5, quality.EdgePeriodCount)
        Assert.AreEqual(2, quality.AfternoonDayCount)
        Assert.IsTrue(quality.ClassLoadVariance > 0.0)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, result.Solutions(0).Schedule).Count)
    End Sub

    ''' <summary>Isolates AfternoonDayCount from the Range confound the
    ''' test above documents: 2 INDEPENDENT classes/teachers (no shared
    ''' resource), each needing exactly 1 unavoidable afternoon lesson
    ''' (1 day, 8 periods; weekly_hours=7 leaves only 6 "safe" periods
    ''' 1-6, forcing exactly 1 lesson into an afternoon period). With only
    ''' 1 day total, "which day" has only one possible answer per class -
    ''' this does not test steering, but DOES prove the new term doesn't
    ''' break anything when every class is forced afternoon-day-count=1
    ''' by pigeonhole, independent of the class-count involved.</summary>
    <TestMethod>
    Public Sub AfternoonDayCountIsOnePerClassWhenForcedByPigeonhole()
        Dim data = Scenario(Mini({"5a", "5b"}, {"T1", "T2"}, {"Mathe"}, {}, {"Mo"}, 8), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 7}},
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5b"}, {"subject", "Mathe"}, {"hours_per_week", 7}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T2"}, {"class", "5b"}, {"subject", "Mathe"}}
        })
        Dim result = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=30, numWorkers:=1)
        Assert.AreEqual(1, result.Solutions.Count)
        Assert.AreEqual(2, result.Solutions(0).Quality.AfternoonDayCount)   ' 1 (5a) + 1 (5b)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, result.Solutions(0).Schedule).Count)
    End Sub

    ''' <summary>Phase 2.12: proves the staged-hint warm-start (default
    ''' useStagedHints:=True) still returns valid, verifier-clean solutions
    ''' on both branches of Stage 1's conditional - with a "should"
    ''' constraint present (Stage 1 sets Minimize(Kann) before handing a
    ''' hint to Stage 2) and without one (Stage 1 is a pure feasibility
    ''' solve, no Minimize call at all, per KannOnlyObjectiveExpr's guard).
    ''' Hints only bias search order, never the feasible-solution set - see
    ''' SolveTop's own doc comment - so this is a correctness/no-regression
    ''' check, not a speed claim (that's 2.12e/2.12f's job).</summary>
    <TestMethod>
    Public Sub StagedHintSolveTopStillReturnsValidSolutions()
        ' Kann-branch: a "should" forbidden_slot -> Stage 1 solves Kann-only first.
        Dim withKann = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}, {"priority", "should"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r1 = Solver.SolveTop(withKann, maxSolutions:=5, totalTimeLimitS:=30, perSolveTimeLimitS:=10, useStagedHints:=True)
        Assert.IsTrue(r1.Solutions.Count > 0)
        For Each s In r1.Solutions
            Assert.AreEqual(0, Verifier.VerifySchedule(withKann, s.Schedule).Count)
        Next

        ' No-Kann branch: no "should" constraints -> KannVars is empty, Stage
        ' 1 is a pure feasibility solve (no Minimize call, per the guard in
        ' KannOnlyObjectiveExpr's caller).
        Dim noKann = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di", "Mi"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r2 = Solver.SolveTop(noKann, maxSolutions:=5, totalTimeLimitS:=30, perSolveTimeLimitS:=10, useStagedHints:=True)
        Assert.IsTrue(r2.Solutions.Count > 0)
        For Each s In r2.Solutions
            Assert.AreEqual(0, Verifier.VerifySchedule(noKann, s.Schedule).Count)
        Next
    End Sub

End Class
