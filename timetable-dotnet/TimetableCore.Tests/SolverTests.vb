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

    ''' <summary>Phase 2.23: required_slot is the positive counterpart of
    ''' forbidden_slot - forces (instead of forbidding) a (class,subject)
    ''' session onto an exact (day,period).</summary>
    <TestMethod>
    Public Sub RequiredSlotForcesSessionOntoExactSlot()
        Dim ent = Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di"}, 2)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "required_slot"}, {"class", "5a"}, {"subject", "Mathe"}, {"day", "Di"}, {"period", 2}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(1, r.Schedule.Count)
        Assert.AreEqual("Di", r.Schedule(0).Day)
        Assert.AreEqual(2, r.Schedule(0).Period)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, r.Schedule).Count)
    End Sub

    ''' <summary>Phase 2.23: a required_slot that directly contradicts a
    ''' Muss forbidden_slot on the same slot is Infeasible - marking the
    ''' required_slot "should" relaxes it to Optimal, with the session
    ''' placed on the only remaining open slot and exactly 1 Kann violation
    ''' carrying the constraint's reason.</summary>
    <TestMethod>
    Public Sub KannRequiredSlotRelaxesConflictWithMussForbiddenSlot()
        Dim mustData = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}},
            New JsonObject From {{"type", "required_slot"}, {"class", "5a"}, {"subject", "Mathe"}, {"day", "Mo"}, {"period", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim rMust = Solver.Solve(mustData)
        Assert.AreEqual(CpSolverStatus.Infeasible, rMust.Status, Solver.StatusName(rMust.Status))

        Dim reasonText = "Chor-Gesamtprobe donnerstags 6. Stunde"
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}},
            New JsonObject From {{"type", "required_slot"}, {"class", "5a"}, {"subject", "Mathe"}, {"day", "Mo"}, {"period", 1},
                {"priority", "should"}, {"reason", reasonText}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(1, r.Schedule.Count)
        Assert.AreEqual(2, r.Schedule(0).Period)

        Dim detail = Verifier.VerifyScheduleDetailed(data, r.Schedule)
        Assert.AreEqual(0, detail.MussViolations.Count, String.Join(vbLf, detail.MussViolations))
        Assert.AreEqual(1, detail.KannViolations.Count)
        Assert.AreEqual("required_slot", detail.KannViolations(0).ConstraintType)
        Assert.AreEqual(reasonText, detail.KannViolations(0).Reason)
        StringAssert.Contains(detail.KannViolations(0).Message, "Regel-Herkunft")
        StringAssert.Contains(detail.KannViolations(0).Message, reasonText)

        Assert.IsNotNull(r.KannConstraintFlags)
        Assert.AreEqual(1, r.KannConstraintFlags.Count)
        Assert.IsTrue(r.KannConstraintFlags(0).Relaxed)
        Assert.AreEqual(reasonText, r.KannConstraintFlags(0).Reason)
    End Sub

    ''' <summary>occupied_slot is the subject-agnostic sibling of
    ''' required_slot: it forces SOME session of a class/teacher (any
    ''' subject) onto an exact (day,period), instead of pinning one named
    ''' (class,subject) session like required_slot does. 2 subjects share
    ''' class "5a" here - occupied_slot only cares that SOME lesson lands
    ''' on Mo/period1, not which one.</summary>
    <TestMethod>
    Public Sub OccupiedSlotForcesAnySessionOntoExactSlot()
        Dim ent = Mini({"5a"}, {"T1"}, {"Mathe", "Deutsch"}, {}, {"Mo", "Di"}, 2)
        Dim data = Scenario(ent, {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Deutsch"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "5a"}},
            New JsonObject From {{"type", "occupied_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Deutsch"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(2, r.Schedule.Count)
        Assert.IsTrue(r.Schedule.Any(Function(l) l.Day = "Mo" AndAlso l.Period = 1),
            "Some lesson of 5a should occupy Mo/period1 - occupied_slot doesn't care which subject.")
        Assert.AreEqual(0, Verifier.VerifySchedule(data, r.Schedule).Count)
    End Sub

    ''' <summary>An occupied_slot that directly contradicts a Muss
    ''' forbidden_slot on the same slot is Infeasible - marking the
    ''' occupied_slot "should" relaxes it to Optimal, with the session
    ''' placed on the only remaining open slot and exactly 1 Kann violation
    ''' carrying the constraint's reason.</summary>
    <TestMethod>
    Public Sub KannOccupiedSlotRelaxesConflictWithMussForbiddenSlot()
        Dim mustData = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}},
            New JsonObject From {{"type", "occupied_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim rMust = Solver.Solve(mustData)
        Assert.AreEqual(CpSolverStatus.Infeasible, rMust.Status, Solver.StatusName(rMust.Status))

        Dim reasonText = "1./2. Klasse soll Stunde 2-4 taeglich belegt sein"
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}},
            New JsonObject From {{"type", "occupied_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1},
                {"priority", "should"}, {"reason", reasonText}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(1, r.Schedule.Count)
        Assert.AreEqual(2, r.Schedule(0).Period)

        Dim detail = Verifier.VerifyScheduleDetailed(data, r.Schedule)
        Assert.AreEqual(0, detail.MussViolations.Count, String.Join(vbLf, detail.MussViolations))
        Assert.AreEqual(1, detail.KannViolations.Count)
        Assert.AreEqual("occupied_slot", detail.KannViolations(0).ConstraintType)
        Assert.AreEqual(reasonText, detail.KannViolations(0).Reason)
        StringAssert.Contains(detail.KannViolations(0).Message, "Regel-Herkunft")
        StringAssert.Contains(detail.KannViolations(0).Message, reasonText)

        Assert.IsNotNull(r.KannConstraintFlags)
        Assert.AreEqual(1, r.KannConstraintFlags.Count)
        Assert.IsTrue(r.KannConstraintFlags(0).Relaxed)
        Assert.AreEqual(reasonText, r.KannConstraintFlags(0).Reason)
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

    ' --- 4. Phase 2.5: Muss/Kann constraint priority ---
    ' Test 1 (backward compatibility) is implicit: every test above this
    ' section sets no "priority" field and must keep passing unmodified -
    ' that IS the backward-compatibility proof, no separate test needed.

    ''' <summary>Test 2: GetPriority defaults to "must", and an explicit
    ''' "priority": "must" behaves identically to omitting the field
    ''' entirely (same solve status, same schedule shape, same
    ''' VerifySchedule output).</summary>
    <TestMethod>
    Public Sub PriorityDefaultsToMustAndIsBackwardCompatible()
        Dim withoutPriority As New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}}
        Assert.AreEqual(JsonHelpers.PriorityMust, JsonHelpers.GetPriority(withoutPriority))
        Dim withExplicitMust As New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}, {"priority", "must"}}
        Assert.AreEqual(JsonHelpers.PriorityMust, JsonHelpers.GetPriority(withExplicitMust))

        Dim dataWithout = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim dataWithMust = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}, {"priority", "must"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })

        Dim r1 = Solver.Solve(dataWithout)
        Dim r2 = Solver.Solve(dataWithMust)
        Assert.AreEqual(r1.Status, r2.Status)
        Assert.AreEqual(2, r1.Schedule(0).Period) ' period 1 forbidden -> must land on period 2
        Assert.AreEqual(2, r2.Schedule(0).Period)
        Assert.AreEqual(0, Verifier.VerifySchedule(dataWithout, r1.Schedule).Count)
        Assert.AreEqual(0, Verifier.VerifySchedule(dataWithMust, r2.Schedule).Count)
    End Sub

    ''' <summary>Test 3 (+9, traceability): an otherwise-Infeasible scenario
    ''' (every slot blocked for the only teacher who still needs hours/week)
    ''' becomes Optimal once that teacher_availability constraint is marked
    ''' "should" - 0 Muss violations, exactly 1 Kann violation carrying the
    ''' constraint's "reason" through to both VerifyScheduleDetailed and
    ''' SolveResult.KannConstraintFlags.</summary>
    <TestMethod>
    Public Sub KannTeacherAvailabilityRelaxesInfeasibleScenario()
        Dim mustData = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_availability"}, {"teacher", "T1"}, {"unavailable_periods", AllSlots({"Mo", "Di"}, {1, 2})}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim rMust = Solver.Solve(mustData)
        Assert.AreEqual(CpSolverStatus.Infeasible, rMust.Status, Solver.StatusName(rMust.Status))

        Dim reasonText = "T1 hat laut Prompt keine Zeit"
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_availability"}, {"teacher", "T1"}, {"unavailable_periods", AllSlots({"Mo", "Di"}, {1, 2})},
                {"priority", "should"}, {"reason", reasonText}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))

        Dim detail = Verifier.VerifyScheduleDetailed(data, r.Schedule)
        Assert.AreEqual(0, detail.MussViolations.Count, String.Join(vbLf, detail.MussViolations))
        Assert.AreEqual(1, detail.KannViolations.Count)
        Assert.AreEqual("teacher_availability", detail.KannViolations(0).ConstraintType)
        Assert.AreEqual(reasonText, detail.KannViolations(0).Reason)
        StringAssert.Contains(detail.KannViolations(0).Message, "Regel-Herkunft")
        StringAssert.Contains(detail.KannViolations(0).Message, reasonText)

        Assert.IsNotNull(r.KannConstraintFlags)
        Assert.AreEqual(1, r.KannConstraintFlags.Count)
        Assert.IsTrue(r.KannConstraintFlags(0).Relaxed)
        Assert.AreEqual(reasonText, r.KannConstraintFlags(0).Reason)
    End Sub

    ''' <summary>Test 4 (+9, traceability): two Kann forbidden_slot
    ''' constraints that cannot both be satisfied at once (only 2 periods
    ''' exist, each forbidden by one of them, exactly 1 lesson must land
    ''' somewhere) -> Optimal with exactly 1 Kann violation, proving the
    ''' objective actually minimizes rather than giving up on everything.
    ''' Each constraint carries its own distinct "reason" - the violated
    ''' one's must show up in the Kann message/Reason, and both must show up
    ''' in KannConstraintFlags (proving reason threading isn't limited to
    ''' the constraint that happens to lose).</summary>
    <TestMethod>
    Public Sub KannConflictingForbiddenSlotsMinimizesViolationCount()
        Dim reason1 = "Periode 1 laut Prompt bevorzugt frei"
        Dim reason2 = "Periode 2 laut Prompt bevorzugt frei"
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}, {"priority", "should"}, {"reason", reason1}},
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 2}, {"priority", "should"}, {"reason", reason2}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Dim detail = Verifier.VerifyScheduleDetailed(data, r.Schedule)
        Assert.AreEqual(0, detail.MussViolations.Count, String.Join(vbLf, detail.MussViolations))
        Assert.AreEqual(1, detail.KannViolations.Count)
        Dim violatedReason = detail.KannViolations(0).Reason
        Assert.IsTrue(violatedReason = reason1 OrElse violatedReason = reason2)
        StringAssert.Contains(detail.KannViolations(0).Message, "Regel-Herkunft")
        StringAssert.Contains(detail.KannViolations(0).Message, violatedReason)

        Assert.AreEqual(2, r.KannConstraintFlags.Count)
        Assert.AreEqual(1, r.KannConstraintFlags.Where(Function(f) f.Relaxed).Count())
        Assert.AreEqual(violatedReason, r.KannConstraintFlags.Single(Function(f) f.Relaxed).Reason)
        Assert.IsTrue(r.KannConstraintFlags.Any(Function(f) f.Reason = reason1))
        Assert.IsTrue(r.KannConstraintFlags.Any(Function(f) f.Reason = reason2))
    End Sub

    ''' <summary>Test 5 (+9, traceability): room_requirement as Kann (analog
    ''' to RoomRequirementPigeonhole) - the shared "Lab" can only fit one
    ''' subject's 2h, so relaxing the second subject's room_requirement
    ''' turns Infeasible into Optimal, with that subject's lessons landing
    ''' without an allowed room (correctly caught by the existing,
    ''' unmodified room_requirement detection in Verifier.vb). Its "reason"
    ''' must thread into the Kann violation message/Reason and into
    ''' KannConstraintFlags.</summary>
    <TestMethod>
    Public Sub KannRoomRequirementRelaxesPigeonhole()
        Dim reasonText = "Physik bevorzugt im Lab laut Prompt"
        Dim data = Scenario(Mini({"5a", "5b"}, {"T1", "T2"}, {"Chemie", "Physik"}, {"Lab"}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Chemie"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5b"}, {"subject", "Physik"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "room_requirement"}, {"subject", "Chemie"}, {"allowed_rooms", New JsonArray From {"Lab"}}},
            New JsonObject From {{"type", "room_requirement"}, {"subject", "Physik"}, {"allowed_rooms", New JsonArray From {"Lab"}}, {"priority", "should"}, {"reason", reasonText}},
            New JsonObject From {{"type", "no_overlap"}, {"resource", "room"}, {"entity", "Lab"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Chemie"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T2"}, {"class", "5b"}, {"subject", "Physik"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Dim detail = Verifier.VerifyScheduleDetailed(data, r.Schedule)
        Assert.AreEqual(0, detail.MussViolations.Count, String.Join(vbLf, detail.MussViolations))
        Dim roomViolations = detail.KannViolations.Where(Function(v) v.ConstraintType = "room_requirement").ToList()
        Assert.IsTrue(roomViolations.Count > 0, String.Join(vbLf, detail.KannViolations.Select(Function(v) v.Message)))
        For Each violation In roomViolations
            Assert.AreEqual(reasonText, violation.Reason)
            StringAssert.Contains(violation.Message, "Regel-Herkunft")
            StringAssert.Contains(violation.Message, reasonText)
        Next

        Assert.AreEqual(1, r.KannConstraintFlags.Count)
        Assert.AreEqual("room_requirement", r.KannConstraintFlags(0).ConstraintType)
        Assert.AreEqual(reasonText, r.KannConstraintFlags(0).Reason)
        Assert.IsTrue(r.KannConstraintFlags(0).Relaxed)
    End Sub

    ''' <summary>Test 6 (+9, traceability): consecutive_required as Kann
    ''' (analog to ConsecutiveRequiredRejectsNonMultipleOfBlockLength - 3h
    ''' demanded with block_length=2, 3 is not a multiple of 2) -> Optimal
    ''' instead of Infeasible, hours_per_week still fully scheduled. Its
    ''' "reason" must thread into the Kann violation message/Reason and
    ''' into KannConstraintFlags.</summary>
    <TestMethod>
    Public Sub KannConsecutiveRequiredRelaxesNonMultipleOfBlockLength()
        Dim reasonText = "Chemie wenn moeglich als Doppelstunde laut Prompt"
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Chemie"}, {}, {"Mo", "Di"}, 3), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Chemie"}, {"hours_per_week", 3}},
            New JsonObject From {{"type", "consecutive_required"}, {"class", "5a"}, {"subject", "Chemie"}, {"block_length", 2}, {"priority", "should"}, {"reason", reasonText}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Chemie"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(3, r.Schedule.Count)
        Dim detail = Verifier.VerifyScheduleDetailed(data, r.Schedule)
        Assert.AreEqual(0, detail.MussViolations.Count, String.Join(vbLf, detail.MussViolations))
        Assert.AreEqual(1, detail.KannViolations.Count)
        Assert.AreEqual("consecutive_required", detail.KannViolations(0).ConstraintType)
        Assert.AreEqual(reasonText, detail.KannViolations(0).Reason)
        StringAssert.Contains(detail.KannViolations(0).Message, "Regel-Herkunft")
        StringAssert.Contains(detail.KannViolations(0).Message, reasonText)

        Assert.AreEqual(1, r.KannConstraintFlags.Count)
        Assert.AreEqual("consecutive_required", r.KannConstraintFlags(0).ConstraintType)
        Assert.AreEqual(reasonText, r.KannConstraintFlags(0).Reason)
        Assert.IsTrue(r.KannConstraintFlags(0).Relaxed)
    End Sub

    ''' <summary>Test 7 (+9, traceability): weekly_hours' max_per_day as
    ''' Kann - only 1 day exists, so the 3 demanded hours can only be
    ''' scheduled by exceeding max_per_day=1 on that day. hours_per_week
    ''' stays exact (always must, untouched by priority); only the
    ''' max_per_day violation is relaxed. Its "reason" must thread into the
    ''' Kann violation message/Reason and into KannConstraintFlags.</summary>
    <TestMethod>
    Public Sub KannWeeklyHoursMaxPerDayRelaxesWhileHoursPerWeekStaysExact()
        Dim reasonText = "Mathe wenn moeglich hoechstens 1x pro Tag laut Prompt"
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 3), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 3}, {"max_per_day", 1}, {"priority", "should"}, {"reason", reasonText}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(3, r.Schedule.Count)
        Dim detail = Verifier.VerifyScheduleDetailed(data, r.Schedule)
        Assert.AreEqual(0, detail.MussViolations.Count, String.Join(vbLf, detail.MussViolations))
        Assert.AreEqual(1, detail.KannViolations.Count)
        Assert.AreEqual("weekly_hours", detail.KannViolations(0).ConstraintType)
        Assert.AreEqual(reasonText, detail.KannViolations(0).Reason)
        StringAssert.Contains(detail.KannViolations(0).Message, "Regel-Herkunft")
        StringAssert.Contains(detail.KannViolations(0).Message, reasonText)

        Assert.AreEqual(1, r.KannConstraintFlags.Count)
        Assert.AreEqual("weekly_hours", r.KannConstraintFlags(0).ConstraintType)
        Assert.AreEqual(reasonText, r.KannConstraintFlags(0).Reason)
        Assert.IsTrue(r.KannConstraintFlags(0).Relaxed)
    End Sub

    ''' <summary>Test 8 (+9, traceability): Validation rejects every
    ''' malformed use of "priority" - an unknown value, "should" on an
    ''' always-must type, and "should" on weekly_hours without max_per_day -
    ''' and Solver.BuildModel throws in every case. Also: a message with no
    ''' "reason" set must NOT grow an empty "(Regel-Herkunft: '')"
    ''' suffix.</summary>
    <TestMethod>
    Public Sub ValidationRejectsInvalidPriorityUsage()
        Dim badValue = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}, {"max_per_day", 1}, {"priority", "vielleicht"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim errorsA = Validation.ValidateEntities(badValue)
        Assert.IsTrue(errorsA.Any(Function(e) e.Contains("priority")))
        Assert.IsFalse(errorsA.Any(Function(e) e.Contains("Regel-Herkunft")))
        Assert.ThrowsException(Of ArgumentException)(Sub() Solver.BuildModel(badValue))

        Dim badType = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "5a"}, {"priority", "should"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim errorsB = Validation.ValidateEntities(badType)
        Assert.IsTrue(errorsB.Any(Function(e) e.Contains("priority")))
        Assert.ThrowsException(Of ArgumentException)(Sub() Solver.BuildModel(badType))

        Dim badWeeklyHours = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}, {"priority", "should"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim errorsC = Validation.ValidateEntities(badWeeklyHours)
        Assert.IsTrue(errorsC.Any(Function(e) e.Contains("priority") OrElse e.Contains("max_per_day")))
        Assert.ThrowsException(Of ArgumentException)(Sub() Solver.BuildModel(badWeeklyHours))
    End Sub

    ''' <summary>Test 9a (traceability, Muss/Validation-side): a malformed
    ''' "priority" value with an explicit "reason" set produces a
    ''' Validation error that carries that reason - complementing Test 8,
    ''' which only proves the absence case (no reason -> no empty
    ''' "(Regel-Herkunft: '')" suffix).</summary>
    <TestMethod>
    Public Sub ValidationErrorIncludesReasonWhenSet()
        Dim reasonText = "Prompt sagt 'vielleicht ginge das'"
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}, {"max_per_day", 1}, {"priority", "vielleicht"}, {"reason", reasonText}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim errors = Validation.ValidateEntities(data)
        Dim priorityErrors = errors.Where(Function(e) e.Contains("priority")).ToList()
        Assert.IsTrue(priorityErrors.Count > 0, String.Join(vbLf, errors))
        Assert.IsTrue(priorityErrors.Any(Function(e) e.Contains("Regel-Herkunft") AndAlso e.Contains(reasonText)))
    End Sub

    ''' <summary>Test 9b (traceability, Muss/Verifier-side): Verifier.vb is
    ''' independent of Solver.vb (see module header) - a schedule solved
    ''' WITHOUT a forbidden_slot constraint is checked afterward against a
    ''' data copy that adds a Muss forbidden_slot for exactly the slot the
    ''' schedule landed on. The resulting violation message must carry the
    ''' constraint's "reason" through to VerifySchedule's plain-string
    ''' output (the same output shape callers have always used with the
    ''' "assert 0" pattern).</summary>
    <TestMethod>
    Public Sub VerifierMussViolationIncludesReasonWhenSet()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(1, r.Schedule.Count)
        Assert.AreEqual("Mo", r.Schedule(0).Day)
        Assert.AreEqual(1, r.Schedule(0).Period)

        Dim reasonText = "Prompt sagt Montag 1. Stunde muss frei bleiben"
        Dim dataChecked = DirectCast(data.DeepClone(), JsonObject)
        dataChecked("constraints").AsArray().Add(
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 1}, {"reason", reasonText}})

        Dim violations = Verifier.VerifySchedule(dataChecked, r.Schedule)
        Assert.AreEqual(1, violations.Count, String.Join(vbLf, violations))
        StringAssert.Contains(violations(0), "Regel-Herkunft")
        StringAssert.Contains(violations(0), reasonText)
    End Sub

    ''' <summary>Phase 2.23: same independence proof as
    ''' VerifierMussViolationIncludesReasonWhenSet, for the new
    ''' "required_slot" Case - solves a schedule that the Solver was never
    ''' told needs to land on a specific slot, then checks (via a
    ''' DeepClone'd data copy with the required_slot appended afterward,
    ''' never seen by Solver.Solve) that Verifier.vb independently detects
    ''' the session is NOT on the required slot.</summary>
    <TestMethod>
    Public Sub VerifierDetectsRequiredSlotViolationIndependently()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(1, r.Schedule.Count)
        Assert.AreEqual("Mo", r.Schedule(0).Day)
        Assert.AreEqual(1, r.Schedule(0).Period)

        Dim reasonText = "Chor-Gesamtprobe donnerstags 6. Stunde"
        Dim dataChecked = DirectCast(data.DeepClone(), JsonObject)
        dataChecked("constraints").AsArray().Add(
            New JsonObject From {{"type", "required_slot"}, {"class", "5a"}, {"subject", "Mathe"}, {"day", "Di"}, {"period", 1}, {"reason", reasonText}})

        Dim violations = Verifier.VerifySchedule(dataChecked, r.Schedule)
        Assert.AreEqual(1, violations.Count, String.Join(vbLf, violations))
        StringAssert.Contains(violations(0), "Regel-Herkunft")
        StringAssert.Contains(violations(0), reasonText)
    End Sub

    ''' <summary>Same independence proof as
    ''' VerifierDetectsRequiredSlotViolationIndependently, for the new
    ''' "occupied_slot" Case - a solved schedule the Solver was never told
    ''' needs occupancy on a specific slot, then an occupied_slot appended
    ''' afterward to a DeepClone'd data copy (never seen by Solver.Solve)
    ''' is independently detected as unmet.</summary>
    <TestMethod>
    Public Sub VerifierDetectsOccupiedSlotViolationIndependently()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(1, r.Schedule.Count)
        Assert.AreEqual("Mo", r.Schedule(0).Day)
        Assert.AreEqual(1, r.Schedule(0).Period)

        Dim reasonText = "1./2. Klasse soll Stunde 2-4 taeglich belegt sein"
        Dim dataChecked = DirectCast(data.DeepClone(), JsonObject)
        dataChecked("constraints").AsArray().Add(
            New JsonObject From {{"type", "occupied_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Di"}, {"period", 1}, {"reason", reasonText}})

        Dim violations = Verifier.VerifySchedule(dataChecked, r.Schedule)
        Assert.AreEqual(1, violations.Count, String.Join(vbLf, violations))
        StringAssert.Contains(violations(0), "Regel-Herkunft")
        StringAssert.Contains(violations(0), reasonText)
    End Sub

    ''' <summary>Phase 2.20b hand-smoke test for the new "parallel_group"
    ''' primitive - proven standalone here BEFORE any Lehrereinsatzplanung.vb
    ''' change, per the approved plan's explicit gate. Mirrors the intended
    ''' Religion-ev/Religion-kath/Ethik use case: 2 classes x 3 subjects x 3
    ''' teachers, all 6 (class,subject,teacher) triples in ONE parallel_group.
    ''' Each subject demands exactly 1h/week - since the pre-pass forces every
    ''' member's Lesson var equal to the SAME shared parallelVar per slot, the
    ''' only way to satisfy all 6 weekly_hours=1 constraints at once is for
    ''' every member to fire at the identical (day,period). Also exercises
    ''' the deduped no_overlap(class)/no_overlap(teacher) term collection
    ''' (each entity's 3 co-group sessions must contribute only ONE term).
    ''' Deliberately does NOT assert Verifier.VerifySchedule(...).Count = 0:
    ''' Verifier.vb has no parallel_group awareness yet (planned for 2.20c),
    ''' so it would currently flag every legitimately-simultaneous session as
    ''' a false-positive "doppelt belegt" violation.</summary>
    <TestMethod>
    Public Sub ParallelGroupSynchronizesSessionsAcrossClasses()
        Dim ent = Mini({"1a", "1b"}, {"T-ev", "T-kath", "T-eth"},
                        {"Religion-ev", "Religion-kath", "Ethik"}, {}, {"Mo", "Di"}, 2)
        Dim classes = {"1a", "1a", "1a", "1b", "1b", "1b"}
        Dim subjects = {"Religion-ev", "Religion-kath", "Ethik", "Religion-ev", "Religion-kath", "Ethik"}
        Dim teachers = {"T-ev", "T-kath", "T-eth", "T-ev", "T-kath", "T-eth"}

        Dim constraints As New List(Of JsonObject)
        For i = 0 To 5
            constraints.Add(New JsonObject From {{"type", "weekly_hours"}, {"class", classes(i)}, {"subject", subjects(i)}, {"hours_per_week", 1}})
            constraints.Add(New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", teachers(i)}, {"class", classes(i)}, {"subject", subjects(i)}})
        Next
        constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "1a"}})
        constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "1b"}})
        constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", "T-ev"}})
        constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", "T-kath"}})
        constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", "T-eth"}})
        constraints.Add(New JsonObject From {
            {"type", "parallel_group"},
            {"classes", New JsonArray(classes.Select(Function(c) CType(c, JsonNode)).ToArray())},
            {"subjects", New JsonArray(subjects.Select(Function(s) CType(s, JsonNode)).ToArray())},
            {"teachers", New JsonArray(teachers.Select(Function(t) CType(t, JsonNode)).ToArray())}
        })

        Dim data = Scenario(ent, constraints)
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count)

        Dim r = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(r.Status), Solver.StatusName(r.Status))
        Assert.AreEqual(6, r.Schedule.Count)

        Dim slots = r.Schedule.Select(Function(l) (l.Day, l.Period)).Distinct().ToList()
        Assert.AreEqual(1, slots.Count, "All 6 parallel_group members must land on the identical (day,period) slot")

        For i = 0 To 5
            Dim entry = r.Schedule.SingleOrDefault(Function(l) l.ClassName = classes(i) AndAlso l.Subject = subjects(i) AndAlso l.Teacher = teachers(i))
            Assert.IsNotNull(entry, $"Missing session for ({classes(i)},{subjects(i)},{teachers(i)})")
            Assert.AreEqual(slots(0).Day, entry.Day)
            Assert.AreEqual(slots(0).Period, entry.Period)
        Next
    End Sub

    ''' <summary>Phase 2.20c gate: the independent Verifier.vb "parallel_group"
    ''' check must catch a deliberately desynchronized schedule - built by
    ''' hand here (not via Solver.Solve) so this proves Verifier.vb's own
    ''' re-derived logic, not merely that the Solver.vb pre-pass happens to
    ''' behave. One of the 6 members (1b/Ethik/T-eth) is simply missing from
    ''' the Mo/1 slot where the other 5 sit.</summary>
    <TestMethod>
    Public Sub ParallelGroupDetectsDesynchronizedScheduleAsViolation()
        Dim ent = Mini({"1a", "1b"}, {"T-ev", "T-kath", "T-eth"},
                        {"Religion-ev", "Religion-kath", "Ethik"}, {}, {"Mo"}, 1)
        Dim classes = {"1a", "1a", "1a", "1b", "1b", "1b"}
        Dim subjects = {"Religion-ev", "Religion-kath", "Ethik", "Religion-ev", "Religion-kath", "Ethik"}
        Dim teachers = {"T-ev", "T-kath", "T-eth", "T-ev", "T-kath", "T-eth"}
        Dim data = Scenario(ent, {
            New JsonObject From {
                {"type", "parallel_group"},
                {"classes", New JsonArray(classes.Select(Function(c) CType(c, JsonNode)).ToArray())},
                {"subjects", New JsonArray(subjects.Select(Function(s) CType(s, JsonNode)).ToArray())},
                {"teachers", New JsonArray(teachers.Select(Function(t) CType(t, JsonNode)).ToArray())}
            }
        })

        Dim brokenSchedule As New List(Of ScheduleEntry) From {
            New ScheduleEntry With {.ClassName = "1a", .Subject = "Religion-ev", .Teacher = "T-ev", .Day = "Mo", .Period = 1, .Room = Nothing},
            New ScheduleEntry With {.ClassName = "1a", .Subject = "Religion-kath", .Teacher = "T-kath", .Day = "Mo", .Period = 1, .Room = Nothing},
            New ScheduleEntry With {.ClassName = "1a", .Subject = "Ethik", .Teacher = "T-eth", .Day = "Mo", .Period = 1, .Room = Nothing},
            New ScheduleEntry With {.ClassName = "1b", .Subject = "Religion-ev", .Teacher = "T-ev", .Day = "Mo", .Period = 1, .Room = Nothing},
            New ScheduleEntry With {.ClassName = "1b", .Subject = "Religion-kath", .Teacher = "T-kath", .Day = "Mo", .Period = 1, .Room = Nothing}
        }

        Dim violations = Verifier.VerifySchedule(data, brokenSchedule)
        Assert.AreEqual(1, violations.Count, String.Join(vbLf, violations))
        StringAssert.Contains(violations(0), "nicht synchron")
    End Sub

    Private Shared Function AllSlots(days As IEnumerable(Of String), periods As IEnumerable(Of Integer)) As JsonArray
        Dim arr As New JsonArray()
        For Each d In days
            For Each p In periods
                arr.Add(New JsonObject From {{"day", d}, {"period", p}})
            Next
        Next
        Return arr
    End Function

    ''' <summary>Code-Review-Umsetzung (R1): ein exakt doppeltes
    ''' teacher_subject_assignment-Tripel muss die Validierung hart
    ''' ablehnen - vorher ueberschrieb das zweite Duplikat die
    ''' Lesson-Variablen des ersten stillschweigend im Modellbau.</summary>
    <TestMethod>
    Public Sub ValidationRejectsDuplicateTeacherSubjectAssignment()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim errors = Validation.ValidateEntities(data)
        Assert.AreEqual(1, errors.Count, String.Join(vbLf, errors))
        StringAssert.Contains(errors(0), "doppelte Zuweisung")
    End Sub

    ''' <summary>Code-Review-Umsetzung (R2): eine occupied_slot-Regel, deren
    ''' Entity zwar existiert, aber keine einzige Session hat, waere im
    ''' Solver ein stiller No-Op (selbst mit priority=must) - jetzt ein
    ''' harter Validierungsfehler. "5b" ist eine bekannte Klasse, hat aber
    ''' keine teacher_subject_assignment.</summary>
    <TestMethod>
    Public Sub ValidationRejectsOccupiedSlotWithoutAnySession()
        Dim data = Scenario(Mini({"5a", "5b"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "occupied_slot"}, {"scope", "class"}, {"entity", "5b"}, {"day", "Mo"}, {"period", 1}}
        })
        Dim errors = Validation.ValidateEntities(data)
        Assert.AreEqual(1, errors.Count, String.Join(vbLf, errors))
        StringAssert.Contains(errors(0), "keine einzige teacher_subject_assignment-Session")
    End Sub

    ''' <summary>R2: required_slot auf ein (Klasse,Fach)-Paar ohne
    ''' zugehoerige teacher_subject_assignment - vorher ein stiller No-Op
    ''' im Solver (lesson.ContainsKey-Guard), jetzt ein Fehler. Deutsch
    ''' existiert als Fach, wird fuer 5a aber nicht unterrichtet.</summary>
    <TestMethod>
    Public Sub ValidationRejectsRequiredSlotWithoutMatchingAssignment()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe", "Deutsch"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "required_slot"}, {"class", "5a"}, {"subject", "Deutsch"}, {"day", "Mo"}, {"period", 1}}
        })
        Dim errors = Validation.ValidateEntities(data)
        Assert.AreEqual(1, errors.Count, String.Join(vbLf, errors))
        StringAssert.Contains(errors(0), "ohne zugehoerige teacher_subject_assignment")
    End Sub

    ''' <summary>R2: ein Slot ausserhalb des timeslots-Rasters (unbekannter
    ''' Tag bzw. Periode ausserhalb 1..periods_per_day) existiert als
    ''' Lesson-Variable nicht - required_slot/occupied_slot darauf waren
    ''' stille No-Ops, jetzt Fehler.</summary>
    <TestMethod>
    Public Sub ValidationRejectsSlotOutsideTimeslotGrid()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "required_slot"}, {"class", "5a"}, {"subject", "Mathe"}, {"day", "Di"}, {"period", 1}},
            New JsonObject From {{"type", "occupied_slot"}, {"scope", "class"}, {"entity", "5a"}, {"day", "Mo"}, {"period", 9}}
        })
        Dim errors = Validation.ValidateEntities(data)
        Assert.AreEqual(2, errors.Count, String.Join(vbLf, errors))
        StringAssert.Contains(errors(0), "kein Tag aus entities.timeslots.days")
        StringAssert.Contains(errors(1), "ausserhalb von 1..2")
    End Sub

    ''' <summary>Code-Review-Umsetzung (P1): occupied_window/must erzwingt
    ''' JEDEN Slot des Fensters hart - ein Objekt statt einer
    ''' occupied_slot-Batterie. 4 Perioden, 2 Wochenstunden, Fenster 3..4
    ''' -&gt; die beiden Stunden MUESSEN auf 3 und 4 liegen.</summary>
    <TestMethod>
    Public Sub OccupiedWindowMustForcesWindowOccupancy()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 4), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "occupied_window"}, {"scope", "class"}, {"entity", "5a"}, {"from_period", 3}, {"to_period", 4}}
        })
        Dim result = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(result.Status))
        Dim periods = result.Schedule.Select(Function(l) l.Period).OrderBy(Function(p) p).ToList()
        CollectionAssert.AreEqual(New List(Of Integer) From {3, 4}, periods)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, result.Schedule).Count)
    End Sub

    ''' <summary>P1: der Verifier prueft occupied_window/must unabhaengig
    ''' nach - ein Plan, der ohne die Regel geloest wurde, muss beim
    ''' Nachpruefen gegen ein nachtraeglich ergaenztes 1..4-Fenster genau
    ''' die 2 unbelegten Fenster-Slots als Verstoesse melden (2 Stunden
    ''' belegt, 4 gefordert - unabhaengig davon, WO die 2 liegen).</summary>
    <TestMethod>
    Public Sub VerifierDetectsOccupiedWindowViolationIndependently()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 4), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 2}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
        Dim result = Solver.Solve(data)
        Assert.IsTrue(IsFeasibleOrOptimal(result.Status))

        Dim withWindow = data.DeepClone().AsObject()
        withWindow("constraints").AsArray().Add(New JsonObject From {
            {"type", "occupied_window"}, {"scope", "class"}, {"entity", "5a"}, {"from_period", 1}, {"to_period", 4}})
        Dim violations = Verifier.VerifySchedule(withWindow, result.Schedule)
        Assert.AreEqual(2, violations.Count, String.Join(vbLf, violations))
        For Each v In violations
            StringAssert.Contains(v, "Fenster-Slot")
        Next
    End Sub

    ''' <summary>P1: Validierung eines inkonsistenten Fensters -
    ''' from &gt; to und Fenster ausserhalb des Periodenrasters sind harte
    ''' Fehler (sonst stiller Teil-No-Op im Solver/Objective).</summary>
    <TestMethod>
    Public Sub ValidationRejectsInconsistentOccupiedWindow()
        Dim data = Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 4), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}},
            New JsonObject From {{"type", "occupied_window"}, {"scope", "class"}, {"entity", "5a"}, {"from_period", 3}, {"to_period", 2}},
            New JsonObject From {{"type", "occupied_window"}, {"scope", "class"}, {"entity", "5a"}, {"from_period", 2}, {"to_period", 9}}
        })
        Dim errors = Validation.ValidateEntities(data)
        Assert.AreEqual(2, errors.Count, String.Join(vbLf, errors))
        StringAssert.Contains(errors(0), "from_period=3 > to_period=2")
        StringAssert.Contains(errors(1), "liegt nicht vollstaendig in 1..4")
    End Sub

    ' --- shared helpers ---

    Private Shared Function IsFeasibleOrOptimal(status As CpSolverStatus) As Boolean
        Return status = CpSolverStatus.Optimal OrElse status = CpSolverStatus.Feasible
    End Function

    Private Shared Function SequenceEqual(a As List(Of Integer), b As Integer()) As Boolean
        Return a.SequenceEqual(b)
    End Function

End Class
