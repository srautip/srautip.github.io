' Phase 2.13: orchestrates a combined solve of the class-based Sek-I half
' and the Kurs-based Kursstufe half so that a teacher/room name SHARED
' between both halves' entities.teachers/entities.rooms lists cannot be
' double-booked across them - closing the gap explicitly documented since
' Phase 2.11 (GsgCompleteScenarioTests.vb's header comment: "this project
' has no notion of 'this Sek-I teacher ALSO teaches a Kursstufe-Kurs'").
'
' Does NOT change Solver.Solve/SolveTop/SolveKursstufe or Verifier.vb at
' all - "sharedness" is derived purely structurally (name intersection
' between the two JsonObjects' own entity lists), and the two supported
' solve orders differ genuinely in mechanism, not just in which call comes
' first:
'
' - KursstufeFirst: after Solver.SolveKursstufe returns a real per-Kurs
'   schedule (real teachers/rooms, day/period already solved), a
'   DeepClone()'d copy of the Sek-I JSON gets extra teacher_availability/
'   forbidden_slot(scope:="room") constraints appended for whichever
'   shared names are busy - then Solver.Solve runs on that clone
'   completely unchanged. Straightforward: Sek I's own solver already
'   supports both constraint types natively.
' - SekIFirst (the default): Solver.Solve() runs first, then the 3-stage
'   Kursstufe pipeline is rebuilt manually here (mirroring the precedent
'   GsgCompleteScenarioSolveTopTests.vb already set for swapping Solve for
'   SolveTop mid-pipeline) so that Schienenraster.BuildSchienenrasterScenario's
'   new `externalTeacherBusySlots` parameter and Raumzuordnung.
'   BuildRaumzuordnungScenario's new `externalRoomBusySlots` parameter can
'   both be wired in - Solver.SolveKursstufe itself is not touched, since
'   it has no way to accept either of those.
'
' Known, accepted asymmetry (see docs/phase2-13-combined-school.md):
' Kursstufe's own room assignment (stage C) only runs AFTER day/period is
' already pinned (stage B), so in the SekIFirst direction a shared room
' that is externally busy at EXACTLY a Kurs's one pinned slot can make
' stage C genuinely Infeasible with no recourse - day/period cannot
' renegotiate at that point. This is a structural limitation of the
' existing 3-stage design, not a bug in this module; a dedicated test
' documents it rather than letting it surface as a surprise later.
Imports System.Text.Json.Nodes
Imports System.Diagnostics
Imports System.Threading
Imports Google.OrTools.Sat

Public Enum SolveOrder
    SekIFirst
    KursstufeFirst
End Enum

Public NotInheritable Class CombinedSolveResult
    Public Property Order As SolveOrder
    Public Property SekIStatus As CpSolverStatus
    Public Property SekISchedule As List(Of ScheduleEntry)
    Public Property KursstufeResult As KursstufeSolveResult
    ''' <summary>Nothing unless BOTH halves solved Optimal/Feasible - the
    ''' concatenation of SekISchedule and KursstufeResult.Schedule, ready
    ''' to pass to BuildMergedVerificationScenario's caller.</summary>
    Public Property CombinedSchedule As List(Of ScheduleEntry)
    ''' <summary>Abbruch durch den Aufrufer (arc42 8.11). Wie weit die Kette
    ''' kam, zeigen SekIStatus/KursstufeResult wie im Fehlerfall auch.</summary>
    Public Property Cancelled As Boolean
End Class

Public Module CombinedSchool

    Private Function SharedNames(dataA As JsonObject, dataB As JsonObject, entityKey As String) As HashSet(Of String)
        Dim a = JsonHelpers.AsStringList(JsonHelpers.Entities(dataA), entityKey)
        Dim b = JsonHelpers.AsStringList(JsonHelpers.Entities(dataB), entityKey)
        Return New HashSet(Of String)(a.Intersect(b))
    End Function

    Private Function BusySlotsFor(schedule As List(Of ScheduleEntry), sharedNames As HashSet(Of String),
                                   selector As Func(Of ScheduleEntry, String)) As Dictionary(Of String, HashSet(Of (Day As String, Period As Integer)))
        Dim result As New Dictionary(Of String, HashSet(Of (Day As String, Period As Integer)))
        For Each e In schedule
            Dim key = selector(e)
            If key Is Nothing OrElse Not sharedNames.Contains(key) Then Continue For
            If Not result.ContainsKey(key) Then result(key) = New HashSet(Of (Day As String, Period As Integer))
            result(key).Add((e.Day, e.Period))
        Next
        Return result
    End Function

    Public Function SolveCombinedSchool(sekIData As JsonObject, kursstufeData As JsonObject,
                                         Optional order As SolveOrder = SolveOrder.SekIFirst,
                                         Optional timeLimitS As Double = 30.0,
                                         Optional seed As Integer = 42,
                                         Optional numWorkers As Integer = 1,
                                         Optional cancellationToken As CancellationToken = Nothing,
                                         Optional progress As IProgress(Of SolveProgress) = Nothing) As CombinedSolveResult
        If cancellationToken.IsCancellationRequested Then
            Return New CombinedSolveResult With {.Order = order, .SekIStatus = CpSolverStatus.Unknown, .Cancelled = True}
        End If
        If order = SolveOrder.KursstufeFirst Then
            Return SolveKursstufeFirst(sekIData, kursstufeData, timeLimitS, seed, numWorkers, cancellationToken, progress)
        End If
        Return SolveSekIFirst(sekIData, kursstufeData, timeLimitS, seed, numWorkers, cancellationToken, progress)
    End Function

    Private Function SolveKursstufeFirst(sekIData As JsonObject, kursstufeData As JsonObject,
                                          timeLimitS As Double, seed As Integer, numWorkers As Integer,
                                          ct As CancellationToken, progress As IProgress(Of SolveProgress)) As CombinedSolveResult
        ' Zwei Aussenstufen (die Kursstufe buendelt ihre eigenen drei intern).
        Dim sw = Stopwatch.StartNew()
        Dim gesamt = timeLimitS * 4.0

        Dim kursResult = Solver.SolveKursstufe(kursstufeData, timeLimitS, seed, numWorkers, ct,
            StageProgressAdapter.Wrap(progress, SolvePhase.Stufe, 1, 2, "Kursstufe", sw, gesamt))
        If kursResult.Cancelled Then
            Return New CombinedSolveResult With {.Order = SolveOrder.KursstufeFirst, .KursstufeResult = kursResult,
                                                 .Cancelled = True}
        End If
        If kursResult.Schedule Is Nothing Then
            Return New CombinedSolveResult With {.Order = SolveOrder.KursstufeFirst, .KursstufeResult = kursResult}
        End If

        Dim sharedTeachers = SharedNames(sekIData, kursstufeData, "teachers")
        Dim sharedRooms = SharedNames(sekIData, kursstufeData, "rooms")
        Dim teacherBusy = BusySlotsFor(kursResult.Schedule, sharedTeachers, Function(e) e.Teacher)
        Dim roomBusy = BusySlotsFor(kursResult.Schedule, sharedRooms, Function(e) e.Room)

        Dim clonedSekI = DirectCast(sekIData.DeepClone(), JsonObject)
        Dim constraintsArray = clonedSekI("constraints").AsArray()
        For Each kvp In teacherBusy
            constraintsArray.Add(New JsonObject From {
                {"type", "teacher_availability"}, {"teacher", kvp.Key},
                {"unavailable_periods", New JsonArray(kvp.Value.Select(
                    Function(slot) CType(New JsonObject From {{"day", slot.Day}, {"period", slot.Period}}, JsonNode)).ToArray())}
            })
        Next
        For Each kvp In roomBusy
            For Each slot In kvp.Value
                constraintsArray.Add(New JsonObject From {
                    {"type", "forbidden_slot"}, {"scope", "room"}, {"entity", kvp.Key}, {"day", slot.Day}, {"period", slot.Period}
                })
            Next
        Next

        Dim sekIResult = Solver.Solve(clonedSekI, timeLimitS, seed, numWorkers, ct,
            StageProgressAdapter.Wrap(progress, SolvePhase.Stufe, 2, 2, "Sek I", sw, gesamt))
        If sekIResult.Cancelled Then
            Return New CombinedSolveResult With {.Order = SolveOrder.KursstufeFirst, .KursstufeResult = kursResult,
                                                 .SekIStatus = sekIResult.Status, .Cancelled = True}
        End If
        Dim combined As List(Of ScheduleEntry) = Nothing
        If sekIResult.Status = CpSolverStatus.Optimal OrElse sekIResult.Status = CpSolverStatus.Feasible Then
            combined = sekIResult.Schedule.Concat(kursResult.Schedule).ToList()
        End If
        Return New CombinedSolveResult With {
            .Order = SolveOrder.KursstufeFirst, .SekIStatus = sekIResult.Status, .SekISchedule = sekIResult.Schedule,
            .KursstufeResult = kursResult, .CombinedSchedule = combined
        }
    End Function

    Private Function SolveSekIFirst(sekIData As JsonObject, kursstufeData As JsonObject,
                                     timeLimitS As Double, seed As Integer, numWorkers As Integer,
                                     ct As CancellationToken, progress As IProgress(Of SolveProgress)) As CombinedSolveResult
        Dim sw = Stopwatch.StartNew()
        Dim gesamt = timeLimitS * 4.0

        Dim sekIResult = Solver.Solve(sekIData, timeLimitS, seed, numWorkers, ct,
            StageProgressAdapter.Wrap(progress, SolvePhase.Stufe, 1, 4, "Sek I", sw, gesamt))
        If sekIResult.Cancelled Then
            Return New CombinedSolveResult With {.Order = SolveOrder.SekIFirst, .SekIStatus = sekIResult.Status,
                                                 .Cancelled = True}
        End If
        If sekIResult.Status <> CpSolverStatus.Optimal AndAlso sekIResult.Status <> CpSolverStatus.Feasible Then
            Return New CombinedSolveResult With {.Order = SolveOrder.SekIFirst, .SekIStatus = sekIResult.Status}
        End If

        Dim sharedTeachers = SharedNames(sekIData, kursstufeData, "teachers")
        Dim sharedRooms = SharedNames(sekIData, kursstufeData, "rooms")
        Dim teacherBusy = BusySlotsFor(sekIResult.Schedule, sharedTeachers, Function(e) e.Teacher)
        Dim roomBusy = BusySlotsFor(sekIResult.Schedule, sharedRooms, Function(e) e.Room)

        Dim kb = Kursblockung.SolveKursblockung(kursstufeData, timeLimitS, seed, numWorkers, ct,
            StageProgressAdapter.Wrap(progress, SolvePhase.Stufe, 2, 4, "Kursblockung", sw, gesamt))
        If kb.Cancelled Then
            Return New CombinedSolveResult With {
                .Order = SolveOrder.SekIFirst, .SekIStatus = sekIResult.Status, .SekISchedule = sekIResult.Schedule,
                .KursstufeResult = New KursstufeSolveResult With {.KursblockungStatus = kb.Status, .Cancelled = True},
                .Cancelled = True}
        End If
        If kb.Status <> CpSolverStatus.Optimal AndAlso kb.Status <> CpSolverStatus.Feasible Then
            Return New CombinedSolveResult With {
                .Order = SolveOrder.SekIFirst, .SekIStatus = sekIResult.Status, .SekISchedule = sekIResult.Schedule,
                .KursstufeResult = New KursstufeSolveResult With {.KursblockungStatus = kb.Status}
            }
        End If

        Dim schienenScenario = Schienenraster.BuildSchienenrasterScenario(kursstufeData, kb.Assignment, teacherBusy)
        Dim schienenResult = Solver.Solve(schienenScenario, timeLimitS, seed, numWorkers, ct,
            StageProgressAdapter.Wrap(progress, SolvePhase.Stufe, 3, 4, "Schienenraster", sw, gesamt))
        If schienenResult.Cancelled Then
            Return New CombinedSolveResult With {
                .Order = SolveOrder.SekIFirst, .SekIStatus = sekIResult.Status, .SekISchedule = sekIResult.Schedule,
                .KursstufeResult = New KursstufeSolveResult With {.KursblockungStatus = kb.Status,
                                                                 .SchienenrasterStatus = schienenResult.Status,
                                                                 .Cancelled = True},
                .Cancelled = True}
        End If
        If schienenResult.Status <> CpSolverStatus.Optimal AndAlso schienenResult.Status <> CpSolverStatus.Feasible Then
            Return New CombinedSolveResult With {
                .Order = SolveOrder.SekIFirst, .SekIStatus = sekIResult.Status, .SekISchedule = sekIResult.Schedule,
                .KursstufeResult = New KursstufeSolveResult With {.KursblockungStatus = kb.Status, .SchienenrasterStatus = schienenResult.Status}
            }
        End If

        Dim raumScenario = Raumzuordnung.BuildRaumzuordnungScenario(kursstufeData, kb.Assignment, schienenResult.Schedule,
                                                                      externalRoomBusySlots:=roomBusy)
        Dim raumResult = Solver.Solve(raumScenario, timeLimitS, seed, numWorkers, ct,
            StageProgressAdapter.Wrap(progress, SolvePhase.Stufe, 4, 4, "Raumzuordnung", sw, gesamt))
        Dim kursResult As New KursstufeSolveResult With {
            .KursblockungStatus = kb.Status, .SchienenrasterStatus = schienenResult.Status, .RaumzuordnungStatus = raumResult.Status,
            .Cancelled = raumResult.Cancelled
        }
        If raumResult.Cancelled Then
            Return New CombinedSolveResult With {
                .Order = SolveOrder.SekIFirst, .SekIStatus = sekIResult.Status, .SekISchedule = sekIResult.Schedule,
                .KursstufeResult = kursResult, .Cancelled = True}
        End If
        If raumResult.Status <> CpSolverStatus.Optimal AndAlso raumResult.Status <> CpSolverStatus.Feasible Then
            Return New CombinedSolveResult With {
                .Order = SolveOrder.SekIFirst, .SekIStatus = sekIResult.Status, .SekISchedule = sekIResult.Schedule,
                .KursstufeResult = kursResult
            }
        End If
        kursResult.Schedule = raumResult.Schedule

        Return New CombinedSolveResult With {
            .Order = SolveOrder.SekIFirst, .SekIStatus = sekIResult.Status, .SekISchedule = sekIResult.Schedule,
            .KursstufeResult = kursResult, .CombinedSchedule = sekIResult.Schedule.Concat(raumResult.Schedule).ToList()
        }
    End Function

    ''' <summary>Builds a `data` JSON suitable for
    ''' Verifier.VerifySchedule(merged, sekISchedule.Concat(kursstufeSchedule))
    ''' - concatenates both halves' own constraints (Verifier.vb's
    ''' CollectViolations only reads JsonHelpers.Entities(data) for its
    ''' `timeslots` fallback, never for classes/teachers/rooms/subjects
    ''' directly, so no per-half entity reconciliation beyond that is
    ''' needed) plus a fresh `no_overlap(teacher)`/`no_overlap(room)` for
    ''' the FULL UNION of both halves' teacher/room names (not just the
    ''' shared subset - costs nothing and states the stronger, easier-to-
    ''' verify invariant "no name is ever double-booked across the
    ''' combined schedule"). No Verifier.vb change needed - its existing
    ''' `no_overlap` detection is already schedule-list-agnostic.
    '''
    ''' `kurswahl`-type constraints (Kursstufe-only, consumed by
    ''' Kursblockung, never by Verifier.CollectViolations) are excluded -
    ''' CollectViolations treats any constraint type it doesn't recognize
    ''' as a violation in its own right ("Unbekannter Constraint-Typ"), so
    ''' passing them through here would produce false-positive
    ''' violations unrelated to the actual no_overlap check this merge
    ''' exists for.
    '''
    ''' Deliberately does NOT re-include either half's own original
    ''' constraints (weekly_hours, room_requirement, teacher_availability,
    ''' etc.) - a real, live-discovered reason why not: this project's
    ''' `room_requirement`/`weekly_hours`/etc. constraints match purely by
    ''' SUBJECT STRING (Verifier.vb's CollectViolations, no class/origin
    ''' filter), and Sek I/Kursstufe intentionally reuse the same subject
    ''' names for equivalent subjects (Phase 2.13's own premise) - so
    ''' e.g. Sek I's `room_requirement(subject:="Physik", NaWi-only)`
    ''' would incorrectly also apply to Kursstufe's OWN "Physik" Kurse
    ''' even when their actual solve was never asked to honor that
    ''' restriction (discovered via the full-scale benchmark test, which
    ''' failed with exactly this false-positive before this fix). Each
    ''' half's own constraints are already independently guaranteed
    ''' satisfied by its own successful Solve()/SolveKursstufe() -
    ''' re-checking them here would be redundant even where it wouldn't
    ''' be outright wrong. The only thing this merge needs to prove is the
    ''' NEW, cross-half concern Phase 2.13 exists for: no shared teacher/
    ''' room name is ever double-booked across the two halves.</summary>
    Public Function BuildMergedVerificationScenario(sekIData As JsonObject, kursstufeData As JsonObject) As JsonObject
        Dim sekIEnt = JsonHelpers.Entities(sekIData)
        Dim kursEnt = JsonHelpers.Entities(kursstufeData)
        Dim allTeachers = JsonHelpers.AsStringList(sekIEnt, "teachers").Union(JsonHelpers.AsStringList(kursEnt, "teachers")).Distinct().ToList()
        Dim allRooms = JsonHelpers.AsStringList(sekIEnt, "rooms").Union(JsonHelpers.AsStringList(kursEnt, "rooms")).Distinct().ToList()

        Dim mergedConstraints As New List(Of JsonNode)
        For Each teacher In allTeachers
            mergedConstraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", teacher}})
        Next
        For Each room In allRooms
            mergedConstraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "room"}, {"entity", room}})
        Next

        Dim mergedEnt As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray(allTeachers.Select(Function(t) CType(t, JsonNode)).ToArray())},
            {"subjects", New JsonArray()},
            {"rooms", New JsonArray(allRooms.Select(Function(r) CType(r, JsonNode)).ToArray())},
            {"timeslots", DirectCast(JsonHelpers.Timeslots(kursEnt).DeepClone(), JsonObject)}
        }
        Return New JsonObject From {
            {"entities", mergedEnt},
            {"constraints", New JsonArray(mergedConstraints.ToArray())}
        }
    End Function

End Module
