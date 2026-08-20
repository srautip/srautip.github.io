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

    ''' <summary>Phase 2.18-Nachtrag: ScoredSolution.Status must carry the
    ''' per-solve CpSolverStatus through so a caller can tell a proven-
    ''' optimal result apart from a merely time-limited Feasible one. This
    ''' scenario has a single possible Lesson assignment (1 day/period),
    ''' so CP-SAT proves Optimal almost instantly given a generous time
    ''' budget - the most direct possible check that the new field is
    ''' actually wired to the real per-solve status, not left at its
    ''' Nothing/zero-value default.</summary>
    <TestMethod>
    Public Sub SolveTopScoredSolutionCarriesSolverStatus()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim result = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=30)
        Assert.AreEqual(1, result.Solutions.Count)
        Assert.AreEqual(CpSolverStatus.Optimal, result.Solutions(0).Status)
    End Sub

    ''' <summary>Phase 2.22: ScoredSolution.ObjectiveValue/BestObjectiveBound/
    ''' Convergence must carry real, non-default data - the direct answer to
    ''' "how far from optimal, and when did it stop improving". Same
    ''' single-assignment scenario as above (proves Optimal almost
    ''' instantly): at Optimal, CP-SAT's own bound must equal its own
    ''' objective exactly (zero gap, not just "close"), and the callback
    ''' must have recorded at least the one accepted solution.</summary>
    <TestMethod>
    Public Sub SolveTopScoredSolutionCarriesObjectiveGapAndConvergence()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim result = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=30)
        Dim sol = result.Solutions(0)
        Assert.AreEqual(CpSolverStatus.Optimal, sol.Status)
        Assert.AreEqual(sol.ObjectiveValue, sol.BestObjectiveBound, 0.0001,
            "Proven-Optimal must mean zero optimality gap.")
        Assert.IsTrue(sol.Convergence.Count >= 1, "Must have recorded at least the accepted incumbent.")
        Assert.AreEqual(sol.ObjectiveValue, sol.Convergence.Last().ObjectiveValue, 0.0001,
            "The final recorded incumbent must match the solve's own final ObjectiveValue.")
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

    ''' <summary>Phase 2.24: proves SolveTop's optional `qualityWeights`
    ''' parameter actually reaches the CP-SAT objective (not just the
    ''' post-hoc QualityScore display) - constructs a scenario with a
    ''' genuine trade-off between two secondary criteria (Springstunden
    ''' vs. Randstunden): period 3 is hard-blocked, leaving periods
    ''' {1,2,4} for 2 weekly Mathe hours. {1,2} has 0 gaps but uses the
    ''' edge period (1); {2,4} has 1 gap but avoids it. Default weights
    ''' (class+teacher gaps = 20 combined > edge = 5) must pick {1,2};
    ''' weights that invert the priority (edge=100 >> gaps=1+1) must flip
    ''' the SOLVER's actual choice to {2,4} - hand-verified live against
    ''' the installed OrTools build before writing this test (both Optimal,
    ''' Totals 5 and 2 respectively, matching the formula exactly).</summary>
    <TestMethod>
    Public Sub SolveTopQualityWeightsInfluenceChosenSchedule()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 4), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 3}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })

        Dim defaultResult = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=10)
        Assert.AreEqual(CpSolverStatus.Optimal, defaultResult.Solutions(0).Status)
        Dim defaultPeriods = defaultResult.Solutions(0).Schedule.Select(Function(l) l.Period).OrderBy(Function(p) p).ToList()
        CollectionAssert.AreEqual(New List(Of Integer) From {1, 2}, defaultPeriods)
        Assert.AreEqual(5.0, defaultResult.Solutions(0).Quality.Total, 0.0000001)

        Dim customWeights As New QualityWeights With {.EdgePeriod = 100.0, .ClassGaps = 1.0, .TeacherGaps = 1.0}
        Dim customResult = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=10, qualityWeights:=customWeights)
        Assert.AreEqual(CpSolverStatus.Optimal, customResult.Solutions(0).Status)
        Dim customPeriods = customResult.Solutions(0).Schedule.Select(Function(l) l.Period).OrderBy(Function(p) p).ToList()
        CollectionAssert.AreEqual(New List(Of Integer) From {2, 4}, customPeriods)
        Assert.AreEqual(2.0, customResult.Solutions(0).Quality.Total, 0.0000001)

        Assert.AreEqual(0, Verifier.VerifySchedule(data, defaultResult.Solutions(0).Schedule).Count)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, customResult.Solutions(0).Schedule).Count)
    End Sub

    ''' <summary>Phase 2.25 (Nachtrag 2 timing fix): a 9-class/3-teacher,
    ''' 5-day/8-period scenario (weekly_hours=15 each, 3 classes sharing
    ''' each teacher - real no_overlap(teacher) contention) with a
    ''' stagnationTimeoutS well above the 500ms poll granularity but well
    ''' below the scenario's live-observed ~1.4s mid-search stall (3/3
    ''' manual repeats during test development: StagnationTriggeredCount=1,
    ''' Status=Feasible, ElapsedS~1.0-1.3s - noticeably short of the ~3.0-
    ''' 3.2s this same scenario needs to reach proven-Optimal without a
    ''' cutoff, see the companion test below). The previous 3-class/3-
    ''' teacher scenario here (independent teachers, no real contention)
    ''' became too fast after the Phase-2.25-Nachtrag-2 BuildGapFlags
    ''' encoding change (~0.6s total) for its ~0.05-0.18s stall to reliably
    ''' survive the 500ms poll-granularity race - see the "current work"
    ''' investigation notes in docs/phase2-25-stagnation-heuristik.md.
    ''' Proves: (a) the cutoff actually fires (StagnationTriggeredCount
    ''' &gt;= 1), (b) it returns the best-so-far incumbent rather than
    ''' nothing (Solutions.Count = 1, Status=Feasible since optimality was
    ''' never proven), (c) that incumbent is still a fully valid schedule
    ''' (0 Verifier violations) despite being cut short mid-search.</summary>
    <TestMethod>
    Public Sub StagnationCutoffFiresAndReturnsEarly()
        Dim data = CoupledTeacherContentionScenario()
        Dim result = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=20, perSolveTimeLimitS:=20, stagnationTimeoutS:=0.5, numWorkers:=1)
        Assert.AreEqual(1, result.Solutions.Count)
        Assert.IsTrue(result.StagnationTriggeredCount >= 1, "Stagnation cutoff should have fired at least once.")
        Assert.AreEqual(CpSolverStatus.Feasible, result.Solutions(0).Status,
            "A stagnation-cut-off iteration must not claim Optimal - it never finished proving that.")
        Assert.IsTrue(result.ElapsedS < 10.0, $"ElapsedS={result.ElapsedS} - cutoff should keep this well under the 20s budget.")
        Assert.AreEqual(0, Verifier.VerifySchedule(data, result.Solutions(0).Schedule).Count)
    End Sub

    ''' <summary>Companion to the test above, same scenario: `stagnationTimeoutS:=
    ''' Nothing` must reproduce the pre-Phase-2.25 behavior byte-for-byte -
    ''' no cutoff ever fires, and the scenario reaches genuine Optimal with
    ''' 0 gap.</summary>
    <TestMethod>
    Public Sub StagnationTimeoutNothingNeverTriggersAndReachesOptimal()
        Dim data = CoupledTeacherContentionScenario()
        Dim result = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=20, perSolveTimeLimitS:=20, stagnationTimeoutS:=Nothing, numWorkers:=1)
        Assert.AreEqual(1, result.Solutions.Count)
        Assert.AreEqual(0, result.StagnationTriggeredCount)
        Assert.AreEqual(CpSolverStatus.Optimal, result.Solutions(0).Status)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, result.Solutions(0).Schedule).Count)
    End Sub

    ''' <summary>Guard-logic regression: a stagnationTimeoutS LARGER than
    ''' the iteration's own perSolveTimeLimitS must behave exactly like
    ''' Nothing (SolveTop's `thisStagnationTimeout` clamp) - it can never
    ''' fire before the iteration would have finished on its own anyway.</summary>
    <TestMethod>
    Public Sub StagnationTimeoutLargerThanBudgetNeverTriggers()
        Dim data = CoupledTeacherContentionScenario()
        Dim result = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=20, perSolveTimeLimitS:=20, stagnationTimeoutS:=100.0, numWorkers:=1)
        Assert.AreEqual(1, result.Solutions.Count)
        Assert.AreEqual(0, result.StagnationTriggeredCount)
        Assert.AreEqual(CpSolverStatus.Optimal, result.Solutions(0).Status)
    End Sub

    ''' <summary>Phase 2.25-Nachtrag-2: proves `QualityWeights.IncludeTeacherGaps`
    ''' actually changes the SOLVER's search, not just whether a display
    ''' field is populated (`ScheduleQuality.Score`'s own TeacherGapCount is
    ''' always independently recomputed from whatever schedule comes back,
    ''' regardless of this flag - see its own doc comment - so a test must
    ''' show a genuine STEERING difference, not merely a number appearing).
    '''
    ''' Shared teacher T1 teaches 5a (Mathe, free choice) and 5b (Deutsch,
    ''' pinned to period 2 via a hard forbidden_slot). 5a is restricted to
    ''' EXACTLY periods {1, 4} (forbidding 2/3 - 2 is already taken by 5b
    ''' via no_overlap(teacher) anyway). Single day, non-afternoon periods
    ''' only, so ClassLoadVariance/TeacherLoadVariance/AfternoonDayCount are
    ''' all 0 no matter what - EdgePeriod and TeacherGaps are the only two
    ''' criteria left standing, and they pull in OPPOSITE directions:
    ''' - 5a@1: T1 occupied={1,2} -&gt; TeacherGap=0, but period 1 is an edge
    '''   period -&gt; EdgePeriod cost = 5.
    ''' - 5a@4: T1 occupied={2,4} -&gt; span=3,count=2-&gt;TeacherGap=1 (cost 100
    '''   at the unified weight), but period 4 is not an edge -&gt; EdgePeriod
    '''   cost = 0.
    ''' With IncludeTeacherGaps:=True (default): 5(edge) &lt; 100(teachergap)
    ''' -&gt; solver picks 5a@1. With IncludeTeacherGaps:=False: the
    ''' TeacherGaps term is not even built into the objective, so 5a@4's
    ''' true cost of 0 beats 5a@1's cost of 5 -&gt; solver switches to 5a@4,
    ''' even though that schedule's POST-HOC Quality.Total (still computed
    ''' with the full, unmodified weights) is worse (100 vs 5) - proof the
    ''' solver was genuinely blind to it during search, not just that the
    ''' display happens to differ.</summary>
    <TestMethod>
    Public Sub IncludeTeacherGapsControlsWhetherSolverSteersAroundTeacherGaps()
        Dim data = Scenario(Mini({"5a", "5b"}, {"T1"}, {"Mathe", "Deutsch"}, {}, {"Mo"}, 4), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5b"}, {"subject", "Deutsch"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5b"}, {"subject", "Deutsch"}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5b"}, {"day", "Mo"}, {"period", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5b"}, {"day", "Mo"}, {"period", 3}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5b"}, {"day", "Mo"}, {"period", 4}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 2}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 3}}
        })

        Dim withTeacherGaps = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=10, numWorkers:=1)
        Assert.AreEqual(CpSolverStatus.Optimal, withTeacherGaps.Solutions(0).Status)
        Dim periodA = withTeacherGaps.Solutions(0).Schedule.Single(Function(l) l.ClassName = "5a").Period
        Assert.AreEqual(1, periodA, "With IncludeTeacherGaps (default True), the solver should avoid the teacher gap even at the cost of an edge period.")
        Assert.AreEqual(0, withTeacherGaps.Solutions(0).Quality.TeacherGapCount)
        Assert.AreEqual(5.0, withTeacherGaps.Solutions(0).Quality.Total, 0.0000001)

        Dim noTeacherGapsWeights As New QualityWeights With {.IncludeTeacherGaps = False}
        Dim withoutTeacherGaps = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=10, numWorkers:=1, qualityWeights:=noTeacherGapsWeights)
        Assert.AreEqual(CpSolverStatus.Optimal, withoutTeacherGaps.Solutions(0).Status)
        Dim periodB = withoutTeacherGaps.Solutions(0).Schedule.Single(Function(l) l.ClassName = "5a").Period
        Assert.AreEqual(4, periodB, "With IncludeTeacherGaps:=False, the solver must be blind to the teacher gap and prefer avoiding the edge period instead.")
        Assert.AreEqual(1, withoutTeacherGaps.Solutions(0).Quality.TeacherGapCount,
            "The post-hoc score still sees the real teacher gap - only the SOLVER's search was blind to it.")
        Assert.AreEqual(100.0, withoutTeacherGaps.Solutions(0).Quality.Total, 0.0000001)

        Assert.AreEqual(0, Verifier.VerifySchedule(data, withTeacherGaps.Solutions(0).Schedule).Count)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, withoutTeacherGaps.Solutions(0).Schedule).Count)
    End Sub

    ''' <summary>Proves `QualityWeights.IncludeClassLoadVariance` genuinely
    ''' steers the SOLVER (not just a display difference) - same "opposite
    ''' pull" pattern as IncludeTeacherGapsControlsWhetherSolverSteersAroundTeacherGaps
    ''' above, this time between ClassLoadVariance and EdgePeriod.
    '''
    ''' 1 class "5a", 1 teacher "T1" teaching both Deutsch and Mathe (1h
    ''' each - 2 total lessons), 2 days x 4 periods; forbidden_slot narrows
    ''' class "5a" to exactly 3 open (day,period) slots: Mo/2, Mo/3, Di/1 -
    ''' leaving exactly 3 ways to place the 2 lessons (no_overlap forbids
    ''' two lessons in the same slot; WHICH subject lands where is
    ''' irrelevant to either criterion below, only the resulting OCCUPIED
    ''' set matters):
    ''' - {Mo/2,Di/1} or {Mo/3,Di/1}: Monday=1, Tuesday=1 -&gt; balanced
    '''   (ClassLoadVariance=0.0), but Di/period1 is an edge period -&gt;
    '''   EdgePeriod cost = 1 occurrence (5.0). TOTAL = 5.0.
    ''' - {Mo/2,Mo/3}: Monday=2, Tuesday=0 -&gt; imbalanced (real population
    '''   variance = ((2-1)^2+(0-1)^2)/2 = 1.0, cost 3.0), but NEITHER
    '''   period is an edge -&gt; EdgePeriod cost = 0. TOTAL = 3.0.
    ''' With IncludeClassLoadVariance:=True (default): 5.0 &lt; 6.0 (the
    ''' {Mo2,Mo3} set's cost WITH ClassLoadVariance counted, i.e. 0+3.0*2=6.0
    ''' using the in-model RANGE approximation, not the 3.0 real-variance
    ''' figure above - SolveTopObjective's search uses range, ScheduleQuality.
    ''' Score's post-hoc display uses population variance, see both
    ''' modules' doc comments) -&gt; solver picks a Di/1-containing set
    ''' (Tuesday count = 1). With IncludeClassLoadVariance:=False: 0(edge
    ''' excluded)+0(loadvar excluded)=0 for {Mo2,Mo3} beats 5(edge, still
    ''' counted)+0(loadvar excluded) for the Di/1 sets -&gt; solver switches
    ''' to {Mo/2,Mo/3} (Tuesday count = 0), even though that's worse on the
    ''' still-displayed Quality.ClassLoadVariance (1.0 vs 0.0).
    ''' IncludeTeacherLoadVariance is disabled in BOTH configs here purely
    ''' to isolate the test to ClassLoadVariance alone - it turns out NOT
    ''' to matter numerically either way: T1 teaches only this one class,
    ''' but ScheduleQuality.LoadVarianceOverWorkingDaysOnly (the real,
    ''' always-computed teacher-side metric) counts variance only across a
    ''' teacher's OWN busy days, and in every candidate set here T1 is busy
    ''' on either 1 day (trivially 0 variance - a single data point has no
    ''' spread) or 2 EQUAL-count days (also 0) - unlike the class-side
    ''' LoadVarianceOverAllDays, which always spans every declared day
    ''' (including an empty one), so 5a's own imbalance IS visible while
    ''' T1's mirrored schedule never trips this particular metric.</summary>
    <TestMethod>
    Public Sub IncludeClassLoadVarianceControlsWhetherSolverBalancesDailyLoad()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe", "Deutsch"}, {}, {"Mo", "Di"}, 4), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Deutsch"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Deutsch"}},
            New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "5a"}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 4}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Di"}, {"period", 2}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Di"}, {"period", 3}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Di"}, {"period", 4}}
        })

        Dim withLoadVar = New QualityWeights With {.IncludeTeacherLoadVariance = False}
        Dim r1 = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=10, numWorkers:=1, qualityWeights:=withLoadVar)
        Assert.AreEqual(CpSolverStatus.Optimal, r1.Solutions(0).Status)
        Dim tuesdayCountOn = r1.Solutions(0).Schedule.Where(Function(l) l.ClassName = "5a" AndAlso l.Day = "Di").Count()
        Assert.AreEqual(1, tuesdayCountOn, "With IncludeClassLoadVariance (default True), the solver should use the Tuesday slot to keep the daily load balanced, even at the cost of an edge period.")
        Assert.AreEqual(0.0, r1.Solutions(0).Quality.ClassLoadVariance, 0.0000001)
        Assert.AreEqual(5.0, r1.Solutions(0).Quality.Total, 0.0000001)

        Dim withoutLoadVar = New QualityWeights With {.IncludeTeacherLoadVariance = False, .IncludeClassLoadVariance = False}
        Dim r2 = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=10, numWorkers:=1, qualityWeights:=withoutLoadVar)
        Assert.AreEqual(CpSolverStatus.Optimal, r2.Solutions(0).Status)
        Dim tuesdayCountOff = r2.Solutions(0).Schedule.Where(Function(l) l.ClassName = "5a" AndAlso l.Day = "Di").Count()
        Assert.AreEqual(0, tuesdayCountOff, "With IncludeClassLoadVariance:=False, the solver must be blind to the imbalance and prefer avoiding the edge period instead.")
        Assert.AreEqual(1.0, r2.Solutions(0).Quality.ClassLoadVariance, 0.0000001,
            "The post-hoc score still sees the real imbalance - only the SOLVER's search was blind to it.")
        Assert.AreEqual(3.0, r2.Solutions(0).Quality.Total, 0.0000001)

        Assert.AreEqual(0, Verifier.VerifySchedule(data, r1.Solutions(0).Schedule).Count)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, r2.Solutions(0).Schedule).Count)
    End Sub

    ''' <summary>Combined smoke test mirroring the real bw-grundschule-
    ''' beispiel config.yaml deployment (all four newly-configurable
    ''' secondary criteria - EdgePeriod, AfternoonDayCount, ClassLoadVariance,
    ''' TeacherLoadVariance - disabled at once, only Kann/ClassGaps/
    ''' TeacherGaps still contribute): proves SolveTop still returns a
    ''' valid, fully verified schedule with all four IncludeX flags off
    ''' simultaneously, not just one at a time as in the dedicated steering
    ''' tests above (which cover the identical code path for TeacherGaps
    ''' and ClassLoadVariance individually - EdgePeriod/AfternoonDayCount/
    ''' TeacherLoadVariance share that same "If w.IncludeX Then Build...()"
    ''' structure, see SolveTopObjective.ApplyQualityObjective).</summary>
    <TestMethod>
    Public Sub AllFourNewIncludeFlagsCanBeDisabledSimultaneously()
        Dim data = CoupledTeacherContentionScenario()
        Dim weights As New QualityWeights With {
            .IncludeEdgePeriod = False,
            .IncludeAfternoonDayCount = False,
            .IncludeClassLoadVariance = False,
            .IncludeTeacherLoadVariance = False
        }
        Dim result = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30, perSolveTimeLimitS:=15, numWorkers:=1, qualityWeights:=weights)
        Assert.AreEqual(1, result.Solutions.Count)
        Assert.IsTrue(result.Solutions(0).Status = CpSolverStatus.Optimal OrElse result.Solutions(0).Status = CpSolverStatus.Feasible)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, result.Solutions(0).Schedule).Count)
    End Sub

    ''' <summary>9 classes sharing only 3 teachers (3 classes per teacher -
    ''' real no_overlap(teacher) contention, not the trivially-independent
    ''' per-class-own-teacher shape the old ThreeClassLooseScenario used),
    ''' 15h/week each - live-observed (scratch experiments,
    ''' /tmp/.../scratchpad/stagtiming/) to reliably produce a genuine
    ''' ~1.4s mid-search stagnation window well clear of the 500ms poll
    ''' granularity used by SolveWithStagnationCutoff.</summary>
    Private Function CoupledTeacherContentionScenario() As JsonObject
        Dim classes = Enumerable.Range(0, 9).Select(Function(i) $"C{i}").ToArray()
        Dim teachers = {"T1", "T2", "T3"}
        Dim cons As New List(Of JsonObject)
        For i = 0 To classes.Length - 1
            Dim teacher = teachers(i Mod teachers.Length)
            cons.Add(New JsonObject From {{"type", "weekly_hours"}, {"class", classes(i)}, {"subject", "Mathe"}, {"hours_per_week", 15}})
            cons.Add(New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", teacher}, {"class", classes(i)}, {"subject", "Mathe"}})
        Next
        Return Scenario(Mini(classes, teachers, {"Mathe"}, {}, {"Mo", "Di", "Mi", "Do", "Fr"}, 8), cons)
    End Function

End Class
