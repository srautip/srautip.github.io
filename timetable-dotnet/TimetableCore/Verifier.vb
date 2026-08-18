' Ported 1:1 from timetable/verifier.py.
'
' This module deliberately shares NO code with Solver.vb. It re-derives
' every check directly from the JSON constraint list and the solver's
' output schedule, with no CP-SAT involved.
'
' Rationale: if the checker reused the same encoding logic as the model
' builder (e.g. a shared "sessions of class" helper), a translation bug in
' that shared logic would be invisible to tests - the checker would simply
' agree with whatever the buggy model produced. A truly independent
' verifier is the only way to catch bugs in the CP-SAT builder itself.
Imports System.Text.Json.Nodes

''' <summary>Phase 2.5: one Kann ("should"-priority) violation found while
''' re-checking a schedule - independently re-derived from the JSON here,
''' not read from Solver.vb's internal BoolVars (see the module header).</summary>
Public NotInheritable Class KannViolationDetail
    Public Property ConstraintIndex As Integer
    Public Property ConstraintType As String
    Public Property Message As String
    Public Property Reason As String
End Class

''' <summary>Muss violations as plain strings (same shape VerifySchedule
''' has always returned - callers keep using the "assert 0" pattern), plus
''' Kann violations as richer records for informational display.</summary>
Public NotInheritable Class VerificationResult
    Public Property MussViolations As List(Of String)
    Public Property KannViolations As List(Of KannViolationDetail)
End Class

Public Module Verifier

    Private Function Find(schedule As List(Of ScheduleEntry),
                           Optional cls As String = Nothing, Optional teacher As String = Nothing,
                           Optional day As String = Nothing, Optional period As Integer? = Nothing,
                           Optional room As String = Nothing, Optional subject As String = Nothing) As List(Of ScheduleEntry)
        Return schedule.Where(Function(l)
                                   Return (cls Is Nothing OrElse l.ClassName = cls) AndAlso
                                          (teacher Is Nothing OrElse l.Teacher = teacher) AndAlso
                                          (day Is Nothing OrElse l.Day = day) AndAlso
                                          (Not period.HasValue OrElse l.Period = period.Value) AndAlso
                                          (room Is Nothing OrElse l.Room = room) AndAlso
                                          (subject Is Nothing OrElse l.Subject = subject)
                               End Function).ToList()
    End Function

    ''' <summary>Appends the constraint's "reason" (if any) to a violation
    ''' message, so it can be traced back to the rule that produced it.
    ''' Duplicated (not shared) from Validation.vb's WithReason, matching
    ''' this module's deliberate no-shared-code design (see header).</summary>
    Private Function WithReason(message As String, c As JsonObject) As String
        Dim reason = JsonHelpers.GetReason(c)
        If String.IsNullOrEmpty(reason) Then Return message
        Return $"{message} (Regel-Herkunft: '{reason}')"
    End Function

    ''' <summary>Runs every detection check exactly once and returns each
    ''' finding tagged with its originating constraint's index/type - the
    ''' detection logic itself (every branch below) is unchanged from
    ''' before Phase 2.5; only the sink (tagged list instead of a flat
    ''' List(Of String)) is new.</summary>
    Private Function CollectViolations(data As JsonObject, schedule As List(Of ScheduleEntry)) As List(Of (Index As Integer, ConstraintType As String, Message As String))
        Dim violations As New List(Of (Index As Integer, ConstraintType As String, Message As String))
        Dim ent = JsonHelpers.Entities(data)
        Dim timeslots = JsonHelpers.Timeslots(ent)
        Dim allDays = JsonHelpers.AsStringList(timeslots, "days")
        Dim allPeriods = Enumerable.Range(1, JsonHelpers.GetInt(timeslots, "periods_per_day").Value).ToList()

        Dim constraints = JsonHelpers.Constraints(data)
        For i = 0 To constraints.Count - 1
            Dim c = constraints(i)
            Dim t = JsonHelpers.GetString(c, "type")

            Select Case t

                Case "teacher_availability"
                    Dim teacher = JsonHelpers.GetString(c, "teacher")
                    Dim availDaysList = JsonHelpers.AsStringList(c, "available_days")
                    Dim avail As New HashSet(Of String)(If(availDaysList.Any(), availDaysList, allDays))
                    Dim blocked As New HashSet(Of (Day As String, Period As Integer))
                    If c.ContainsKey("unavailable_periods") AndAlso c("unavailable_periods") IsNot Nothing Then
                        For Each node In c("unavailable_periods").AsArray()
                            Dim entryObj = node.AsObject()
                            blocked.Add((JsonHelpers.GetString(entryObj, "day"), JsonHelpers.GetInt(entryObj, "period").Value))
                        Next
                    End If
                    For Each l In Find(schedule, teacher:=teacher)
                        If Not avail.Contains(l.Day) Then
                            violations.Add((i, t, WithReason($"{teacher} unterrichtet an {l.Day}, ist dort aber nicht verfuegbar", c)))
                        End If
                        If blocked.Contains((l.Day, l.Period)) Then
                            violations.Add((i, t, WithReason($"{teacher} unterrichtet {l.Day}/{l.Period}, obwohl explizit gesperrt", c)))
                        End If
                    Next

                Case "weekly_hours"
                    Dim className = JsonHelpers.GetString(c, "class")
                    Dim subject = JsonHelpers.GetString(c, "subject")
                    Dim hoursPerWeek = JsonHelpers.GetInt(c, "hours_per_week").Value
                    Dim cnt = Find(schedule, cls:=className, subject:=subject).Count
                    If cnt <> hoursPerWeek Then
                        violations.Add((i, t, WithReason($"{className}/{subject}: {cnt}h geplant, {hoursPerWeek}h gefordert", c)))
                    End If
                    Dim maxPerDay = JsonHelpers.GetInt(c, "max_per_day")
                    If maxPerDay.HasValue AndAlso maxPerDay.Value <> 0 Then
                        Dim byDay As New Dictionary(Of String, Integer)
                        For Each l In Find(schedule, cls:=className, subject:=subject)
                            byDay(l.Day) = If(byDay.ContainsKey(l.Day), byDay(l.Day), 0) + 1
                        Next
                        For Each kvp In byDay
                            If kvp.Value > maxPerDay.Value Then
                                violations.Add((i, t, WithReason($"{className}/{subject} am {kvp.Key}: {kvp.Value}h > erlaubtes Maximum {maxPerDay.Value}h/Tag", c)))
                            End If
                        Next
                    End If

                Case "room_requirement"
                    Dim subject = JsonHelpers.GetString(c, "subject")
                    Dim allowedRooms = JsonHelpers.AsStringList(c, "allowed_rooms")
                    For Each l In Find(schedule, subject:=subject)
                        If Not allowedRooms.Contains(l.Room) Then
                            violations.Add((i, t, WithReason(
                                $"{subject} ({l.ClassName}, {l.Day}/{l.Period}) in Raum {l.Room}, " &
                                $"erlaubt sind nur {JsonHelpers.PyListRepr(allowedRooms)}", c)))
                        End If
                    Next

                Case "no_overlap"
                    Dim resource = JsonHelpers.GetString(c, "resource")
                    Dim entityVal = JsonHelpers.GetString(c, "entity")
                    Dim seen As New Dictionary(Of (Day As String, Period As Integer), List(Of ScheduleEntry))
                    For Each l In schedule
                        Dim matches As Boolean
                        Select Case resource
                            Case "class" : matches = l.ClassName = entityVal
                            Case "teacher" : matches = l.Teacher = entityVal
                            Case "room" : matches = l.Room = entityVal
                            Case Else : matches = False
                        End Select
                        If Not matches Then Continue For
                        Dim slot = (l.Day, l.Period)
                        If Not seen.ContainsKey(slot) Then seen(slot) = New List(Of ScheduleEntry)
                        seen(slot).Add(l)
                    Next
                    For Each kvp In seen
                        If kvp.Value.Count > 1 Then
                            violations.Add((i, t, WithReason($"{resource} {entityVal} doppelt belegt am {kvp.Key}: {kvp.Value.Count} Eintraege", c)))
                        End If
                    Next

                Case "shared_resource_conflict"
                    Dim classesInvolved = JsonHelpers.AsStringList(c, "classes")
                    Dim subject = JsonHelpers.GetString(c, "subject")
                    Dim teacher = JsonHelpers.GetString(c, "teacher")
                    For Each d In allDays
                        For Each p In allPeriods
                            Dim hits = schedule.Where(Function(l) l.Teacher = teacher AndAlso l.Subject = subject AndAlso
                                                           classesInvolved.Contains(l.ClassName) AndAlso
                                                           l.Day = d AndAlso l.Period = p).ToList()
                            If hits.Count > 1 Then
                                violations.Add((i, t, WithReason(
                                    $"{teacher} gleichzeitig in {JsonHelpers.PyListRepr(hits.Select(Function(h) h.ClassName))} am {d}/{p} ({subject})", c)))
                            End If
                        Next
                    Next

                Case "forbidden_slot"
                    Dim scope = JsonHelpers.GetString(c, "scope")
                    Dim entityVal = JsonHelpers.GetString(c, "entity")
                    Dim day = JsonHelpers.GetString(c, "day")
                    Dim period = JsonHelpers.GetInt(c, "period").Value
                    For Each l In Find(schedule, day:=day, period:=period)
                        Dim matches As Boolean
                        Select Case scope
                            Case "class" : matches = l.ClassName = entityVal
                            Case "teacher" : matches = l.Teacher = entityVal
                            Case "room" : matches = l.Room = entityVal
                            Case Else : matches = False
                        End Select
                        If matches Then
                            violations.Add((i, t, WithReason($"{entityVal} ({scope}) hat Unterricht im gesperrten Slot {day}/{period}", c)))
                        End If
                    Next

                Case "consecutive_required"
                    Dim className = JsonHelpers.GetString(c, "class")
                    Dim subject = JsonHelpers.GetString(c, "subject")
                    Dim blockLength = JsonHelpers.GetInt(c, "block_length").Value
                    Dim byDay As New Dictionary(Of String, List(Of Integer))
                    For Each l In Find(schedule, cls:=className, subject:=subject)
                        If Not byDay.ContainsKey(l.Day) Then byDay(l.Day) = New List(Of Integer)
                        byDay(l.Day).Add(l.Period)
                    Next
                    For Each kvp In byDay
                        Dim d = kvp.Key
                        Dim ps = kvp.Value.OrderBy(Function(x) x).ToList()
                        Dim idx = 0
                        While idx < ps.Count
                            Dim run As New List(Of Integer) From {ps(idx)}
                            While idx + 1 < ps.Count AndAlso ps(idx + 1) = ps(idx) + 1
                                idx += 1
                                run.Add(ps(idx))
                            End While
                            If run.Count <> blockLength Then
                                violations.Add((i, t, WithReason(
                                    $"{className}/{subject} am {d}: Block der Laenge {run.Count} statt geforderter " &
                                    $"{blockLength} ({String.Join(", ", run)})", c)))
                            End If
                            idx += 1
                        End While
                    Next

                Case "teacher_subject_assignment"
                    Dim className = JsonHelpers.GetString(c, "class")
                    Dim subject = JsonHelpers.GetString(c, "subject")
                    Dim teacher = JsonHelpers.GetString(c, "teacher")
                    For Each l In Find(schedule, cls:=className, subject:=subject)
                        If l.Teacher <> teacher Then
                            violations.Add((i, t, WithReason(
                                $"{className}/{subject} wird von {l.Teacher} statt vorgeschriebener Lehrkraft {teacher} unterrichtet", c)))
                        End If
                    Next

                Case Else
                    violations.Add((i, t, $"Unbekannter Constraint-Typ im Verifier: '{t}'"))

            End Select
        Next

        Return violations
    End Function

    ''' <summary>Returns a list of human-readable Muss (hard) violation
    ''' strings (empty = OK) - signature and output unchanged from before
    ''' Phase 2.5: every existing fixture has no "priority" field, so
    ''' JsonHelpers.GetPriority defaults every constraint to "must" and
    ''' this is byte-identical to the pre-Phase-2.5 behavior. Kann
    ''' ("should") violations are silently excluded here - see
    ''' VerifyScheduleDetailed for those.</summary>
    Public Function VerifySchedule(data As JsonObject, schedule As List(Of ScheduleEntry)) As List(Of String)
        Dim constraints = JsonHelpers.Constraints(data)
        Return CollectViolations(data, schedule).
            Where(Function(v) JsonHelpers.GetPriority(constraints(v.Index)) = JsonHelpers.PriorityMust).
            Select(Function(v) v.Message).
            ToList()
    End Function

    ''' <summary>Phase 2.5: same checks as VerifySchedule, but partitioned
    ''' into Muss (for the same "assert 0" pattern) and Kann (informational
    ''' - which preferences ended up violated, with type/reason for
    ''' reporting).</summary>
    Public Function VerifyScheduleDetailed(data As JsonObject, schedule As List(Of ScheduleEntry)) As VerificationResult
        Dim constraints = JsonHelpers.Constraints(data)
        Dim all = CollectViolations(data, schedule)

        Dim mussViolations = all.
            Where(Function(v) JsonHelpers.GetPriority(constraints(v.Index)) = JsonHelpers.PriorityMust).
            Select(Function(v) v.Message).
            ToList()

        Dim kannViolations = all.
            Where(Function(v) JsonHelpers.GetPriority(constraints(v.Index)) = JsonHelpers.PriorityShould).
            Select(Function(v) New KannViolationDetail With {
                .ConstraintIndex = v.Index, .ConstraintType = v.ConstraintType,
                .Message = v.Message, .Reason = JsonHelpers.GetReason(constraints(v.Index))
            }).
            ToList()

        Return New VerificationResult With {.MussViolations = mussViolations, .KannViolations = kannViolations}
    End Function

End Module
