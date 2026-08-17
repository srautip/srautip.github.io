' Ported 1:1 from tests/test_timetable_model.py.
'
' Testing philosophy (see the Python original's module docstring and
' README.md in timetable/ for the full write-up):
'
' 1. Unit-test each constraint TYPE in isolation with a minimal scenario.
' 2. Verify actual SOLUTIONS with the independent Verifier.vb - never
'    assert only status = Feasible, always check the returned schedule
'    really satisfies every constraint.
' 3. "Pigeonhole" tests: make demand exceed capacity by exactly one unit
'    and assert Infeasible. Proves a constraint has real teeth.
' 4. One integration test against the full multi-constraint scenario that
'    came out of the LLM extraction pipeline (FullScenarioFixture).
' 5. A determinism test: CP-SAT is only reproducible run-to-run if you pin
'    RandomSeed AND NumSearchWorkers=1.
' 6. A "mutation" test on the full scenario expecting Infeasible.
' 7. A validation test proving BuildModel rejects unknown entity
'    references before ever calling the solver.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class SolverTests

    ' --- 1. Isolated unit tests, one per constraint type ---

    <TestMethod>
    Public Sub WeeklyHoursExactCount()
        Dim ent = Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di", "Mi"}, 4)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 3}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(3, r.Schedule.Count)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, r.Schedule).Count)
    End Sub

    <TestMethod>
    Public Sub TeacherAvailabilityRestrictsDays()
        Dim ent = Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di", "Mi"}, 2)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "teacher_availability"}, {"teacher", "T1"}, {"available_days", New JsonArray From {"Di"}}},
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.IsTrue(r.Schedule.All(Function(l) l.Day = "Di"))
        Assert.AreEqual(0, Verifier.VerifySchedule(data, r.Schedule).Count)
    End Sub

    ''' <summary>4 lesson-hours demanded, only 2 slots exist, no_overlap
    ''' present -> must be Infeasible. If the translation of no_overlap
    ''' were broken (e.g. wrong entity key), this scenario would wrongly
    ''' come back Feasible.</summary>
    <TestMethod>
    Public Sub NoOverlapHasRealTeethPigeonhole()
        Dim ent = Mini({"5a"}, {"T1", "T2"}, {"Mathe", "Deutsch"}, {}, {"Mo"}, 2)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Deutsch"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "5a"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T2"}, {"class", "5a"}, {"subject", "Deutsch"}}
        })
        Dim r = Solver.Solve(data)
        Assert.AreEqual(CpSolverStatus.Infeasible, r.Status, Solver.StatusName(r.Status))
    End Sub

    ''' <summary>Same shape as above but demand == capacity exactly ->
    ''' Feasible, and the independent verifier confirms no double-booking
    ''' occurred.</summary>
    <TestMethod>
    Public Sub NoOverlapExactFitIsFeasibleAndClean()
        Dim ent = Mini({"5a"}, {"T1", "T2"}, {"Mathe", "Deutsch"}, {}, {"Mo"}, 2)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Deutsch"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "5a"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T2"}, {"class", "5a"}, {"subject", "Deutsch"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(2, r.Schedule.Count)
        Dim slots = r.Schedule.Select(Function(l) (l.Day, l.Period)).Distinct().ToList()
        Assert.AreEqual(2, slots.Count)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, r.Schedule).Count)
    End Sub

    ''' <summary>Two different subjects both restricted to the one lab
    ''' room, both needing 2h, only 2 slots total -> Infeasible proves
    ''' room-level no_overlap is actually wired to the room-choice
    ''' variables.</summary>
    <TestMethod>
    Public Sub RoomRequirementPigeonhole()
        Dim ent = Mini({"5a", "5b"}, {"T1", "T2"}, {"Chemie", "Physik"}, {"Lab"}, {"Mo"}, 2)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Chemie"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5b"}, {"subject", "Physik"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "room_requirement"}, {"subject", "Chemie"}, {"allowed_rooms", New JsonArray From {"Lab"}}},
            New JsonObject From {{"type", "room_requirement"}, {"subject", "Physik"}, {"allowed_rooms", New JsonArray From {"Lab"}}},
            New JsonObject From {{"type", "no_overlap"}, {"resource", "room"}, {"entity", "Lab"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Chemie"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T2"}, {"class", "5b"}, {"subject", "Physik"}}
        })
        Dim r = Solver.Solve(data)
        Assert.AreEqual(CpSolverStatus.Infeasible, r.Status, Solver.StatusName(r.Status))
    End Sub

    <TestMethod>
    Public Sub ForbiddenSlotIsAvoided()
        Dim ent = Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(2, r.Schedule(0).Period)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, r.Schedule).Count)
    End Sub

    <TestMethod>
    Public Sub ConsecutiveRequiredFormsABlock()
        Dim ent = Mini({"5a"}, {"T1"}, {"Chemie"}, {}, {"Mo"}, 3)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Chemie"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "consecutive_required"}, {"class", "5a"}, {"subject", "Chemie"}, {"block_length", 2}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Chemie"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Dim periods = r.Schedule.Select(Function(l) l.Period).OrderBy(Function(p) p).ToList()
        Dim isContiguousPair = (periods.Count = 2) AndAlso (periods(1) = periods(0) + 1) AndAlso
            (SequenceEqual(periods, {1, 2}) OrElse SequenceEqual(periods, {2, 3}))
        Assert.IsTrue(isContiguousPair, $"periods was [{String.Join(",", periods)}]")
        Assert.AreEqual(0, Verifier.VerifySchedule(data, r.Schedule).Count)
    End Sub

    ''' <summary>3 hours demanded with block_length=2: 3 is not a multiple
    ''' of 2, so no arrangement of whole blocks can sum to exactly 3 ->
    ''' Infeasible.</summary>
    <TestMethod>
    Public Sub ConsecutiveRequiredRejectsNonMultipleOfBlockLength()
        Dim ent = Mini({"5a"}, {"T1"}, {"Chemie"}, {}, {"Mo", "Di"}, 3)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Chemie"}, {"hours_per_week", 3}},
            New JsonObject From {{"type", "consecutive_required"}, {"class", "5a"}, {"subject", "Chemie"}, {"block_length", 2}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Chemie"}}
        })
        Dim r = Solver.Solve(data)
        Assert.AreEqual(CpSolverStatus.Infeasible, r.Status, Solver.StatusName(r.Status))
    End Sub

    <TestMethod>
    Public Sub SharedResourceConflictPigeonhole()
        Dim ent = Mini({"5a", "5b"}, {"T1"}, {"Sport"}, {}, {"Mo"}, 1)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Sport"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5b"}, {"subject", "Sport"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "shared_resource_conflict"}, {"classes", New JsonArray From {"5a", "5b"}}, {"subject", "Sport"}, {"teacher", "T1"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Sport"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5b"}, {"subject", "Sport"}}
        })
        Dim r = Solver.Solve(data)
        Assert.AreEqual(CpSolverStatus.Infeasible, r.Status, Solver.StatusName(r.Status))
    End Sub

    ' --- 2. Integration test against the (curated) real LLM extraction output ---

    <TestMethod>
    Public Sub FullScenarioFromLlmExtractionIsSolvableAndClean()
        Dim data = BuildFullScenario()
        Dim r = Solver.Solve(data, timeLimitS:=20)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Dim violations = Verifier.VerifySchedule(data, r.Schedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))
    End Sub

    ''' <summary>Mutation test: block every single slot for Herr Meier
    ''' while he still has 4h/week of Mathematik required -> must flip to
    ''' Infeasible.</summary>
    <TestMethod>
    Public Sub FullScenarioBecomesInfeasibleWhenOverconstrained()
        Dim broken = DirectCast(BuildFullScenario().DeepClone(), JsonObject)
        Dim days = JsonHelpers.AsStringList(JsonHelpers.Timeslots(JsonHelpers.Entities(broken)), "days")
        Dim periods = Enumerable.Range(1, JsonHelpers.GetInt(JsonHelpers.Timeslots(JsonHelpers.Entities(broken)), "periods_per_day").Value).ToList()

        For Each c In JsonHelpers.Constraints(broken)
            If JsonHelpers.GetString(c, "type") = "teacher_availability" AndAlso JsonHelpers.GetString(c, "teacher") = "Herr Meier" Then
                c("available_days") = New JsonArray()
                Dim unavail As New JsonArray()
                For Each d In days
                    For Each p In periods
                        unavail.Add(New JsonObject From {{"day", d}, {"period", p}})
                    Next
                Next
                c("unavailable_periods") = unavail
            End If
        Next

        Dim r = Solver.Solve(broken, timeLimitS:=20)
        Assert.AreEqual(CpSolverStatus.Infeasible, r.Status, Solver.StatusName(r.Status))
    End Sub

    ''' <summary>CP-SAT is only guaranteed reproducible with
    ''' NumSearchWorkers=1 and a fixed RandomSeed. This asserts two
    ''' independent solves return the exact same schedule under those
    ''' settings.</summary>
    <TestMethod>
    Public Sub DeterminismWithFixedSeedAndSingleWorker()
        Dim data = BuildFullScenario()
        Dim r1 = Solver.Solve(data, timeLimitS:=20, seed:=7, numWorkers:=1)
        Dim r2 = Solver.Solve(data, timeLimitS:=20, seed:=7, numWorkers:=1)
        Assert.AreEqual(r1.Status, r2.Status)

        Dim sig1 = Signature(r1.Schedule)
        Dim sig2 = Signature(r2.Schedule)
        CollectionAssert.AreEqual(sig1, sig2)
    End Sub

    Private Shared Function Signature(schedule As List(Of ScheduleEntry)) As List(Of String)
        Return schedule.
            Select(Function(l) $"{l.ClassName}|{l.Subject}|{l.Teacher}|{l.Day}|{l.Period}").
            OrderBy(Function(s) s).
            ToList()
    End Function

    ' --- 3. Validation: BuildModel must reject invalid entity references ---

    ''' <summary>Reproduces the exact bug found earlier: a
    ''' consecutive_required entry with a subject name ('Chemie') put into
    ''' the 'class' field instead of a real class. Before Validation.vb
    ''' was wired in, BuildModel silently dropped this constraint and the
    ''' solver returned Optimal with Chemistry missing entirely from the
    ''' schedule - no error anywhere.</summary>
    <TestMethod>
    Public Sub BuildModelRejectsUnknownClassReference()
        Dim ent = Mini({"5a"}, {"T1"}, {"Chemie"}, {}, {"Mo"}, 3)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Chemie"}},
            New JsonObject From {{"type", "consecutive_required"}, {"class", "Chemie"}, {"subject", "Chemie"}, {"block_length", 2}}
        })
        Dim ex = Assert.ThrowsException(Of ArgumentException)(Sub() Solver.BuildModel(data))
        Assert.IsTrue(ex.Message.Contains("Chemie"))
    End Sub

    <TestMethod>
    Public Sub BuildModelRejectsUnknownTeacherAndRoom()
        Dim ent = Mini({"5a"}, {"T1"}, {"Mathe"}, {"R1"}, {"Mo"}, 2)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", "Herr Unbekannt"}},
            New JsonObject From {{"type", "room_requirement"}, {"subject", "Mathe"}, {"allowed_rooms", New JsonArray From {"R99"}}}
        })
        Dim errors = Validation.ValidateEntities(data)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("Herr Unbekannt")))
        Assert.IsTrue(errors.Any(Function(e) e.Contains("R99")))
        Assert.ThrowsException(Of ArgumentException)(Sub() Solver.BuildModel(data))
    End Sub

    <TestMethod>
    Public Sub BuildModelAcceptsValidScenarioWithoutRaising()
        Dim data = BuildFullScenario()
        Solver.BuildModel(data) ' must not raise
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count)
    End Sub

    ''' <summary>A missing no_overlap entry for one teacher is a soft
    ''' warning, not a hard error: solving must still succeed.</summary>
    <TestMethod>
    Public Sub CoverageWarningsAreAdvisoryNotBlocking()
        Dim ent = Mini({"5a"}, {"T1", "T2"}, {"Mathe", "Deutsch"}, {}, {"Mo"}, 2)
        ' T2 intentionally has no no_overlap entry below.
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T2"}, {"class", "5a"}, {"subject", "Deutsch"}},
            New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", "T1"}}
        })
        Dim warnings = Validation.CoverageWarnings(data)
        Assert.IsTrue(warnings.Any(Function(w) w.Contains("T2")))
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count) ' not a hard error
        Dim r = Solver.Solve(data) ' must still solve fine
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
    End Sub

    ' --- shared helpers ---

    Private Shared Function IsFeasibleOrOptimal(status As CpSolverStatus) As Boolean
        Return status = CpSolverStatus.Optimal OrElse status = CpSolverStatus.Feasible
    End Function

    Private Shared Function SequenceEqual(a As List(Of Integer), b As Integer()) As Boolean
        Return a.SequenceEqual(b)
    End Function

End Class
