' Ported 1:1 from timetable/timetable_model.py. Builds a CP-SAT model
' (Google.OrTools.Sat) from the same entities/constraints JSON shape as the
' Python original and solves it. ValidateEntities (Validation.vb) is called
' first, exactly like the Python build_model() - an out-of-range reference
' would otherwise be silently dropped by the model builder (no session/
' variable to attach it to), which can make an incomplete schedule solve as
' OPTIMAL. See Validation.vb's header comment for the concrete incident that
' motivated this ordering.
Imports System.Diagnostics
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat


Public NotInheritable Class Session
    Public ReadOnly Property ClassName As String
    Public ReadOnly Property Subject As String
    Public ReadOnly Property Teacher As String

    Public Sub New(className As String, subject As String, teacher As String)
        Me.ClassName = className
        Me.Subject = subject
        Me.Teacher = teacher
    End Sub
End Class

''' <summary>Composite key for the (class, subject, teacher, day, period)
''' -keyed `lesson` dict and the (..., room)-keyed `room` dict in the
''' Python original. VB.NET has no native tuple-hashing dict key like
''' Python, so this struct stands in for it.</summary>
Public Structure LessonKey
    Implements IEquatable(Of LessonKey)

    Public ReadOnly ClassName As String
    Public ReadOnly Subject As String
    Public ReadOnly Teacher As String
    Public ReadOnly Day As String
    Public ReadOnly Period As Integer

    Public Sub New(className As String, subject As String, teacher As String, day As String, period As Integer)
        Me.ClassName = className
        Me.Subject = subject
        Me.Teacher = teacher
        Me.Day = day
        Me.Period = period
    End Sub

    Public Overloads Function Equals(other As LessonKey) As Boolean Implements IEquatable(Of LessonKey).Equals
        Return ClassName = other.ClassName AndAlso Subject = other.Subject AndAlso
               Teacher = other.Teacher AndAlso Day = other.Day AndAlso Period = other.Period
    End Function

    Public Overrides Function Equals(obj As Object) As Boolean
        Return TypeOf obj Is LessonKey AndAlso Equals(DirectCast(obj, LessonKey))
    End Function

    Public Overrides Function GetHashCode() As Integer
        Return HashCode.Combine(ClassName, Subject, Teacher, Day, Period)
    End Function
End Structure

Public Structure RoomKey
    Implements IEquatable(Of RoomKey)

    Public ReadOnly ClassName As String
    Public ReadOnly Subject As String
    Public ReadOnly Teacher As String
    Public ReadOnly Day As String
    Public ReadOnly Period As Integer
    Public ReadOnly Room As String

    Public Sub New(className As String, subject As String, teacher As String, day As String, period As Integer, room As String)
        Me.ClassName = className
        Me.Subject = subject
        Me.Teacher = teacher
        Me.Day = day
        Me.Period = period
        Me.Room = room
    End Sub

    Public Overloads Function Equals(other As RoomKey) As Boolean Implements IEquatable(Of RoomKey).Equals
        Return ClassName = other.ClassName AndAlso Subject = other.Subject AndAlso
               Teacher = other.Teacher AndAlso Day = other.Day AndAlso Period = other.Period AndAlso Room = other.Room
    End Function

    Public Overrides Function Equals(obj As Object) As Boolean
        Return TypeOf obj Is RoomKey AndAlso Equals(DirectCast(obj, RoomKey))
    End Function

    Public Overrides Function GetHashCode() As Integer
        Return HashCode.Combine(ClassName, Subject, Teacher, Day, Period, Room)
    End Function
End Structure

''' <summary>Phase 2.5: one shared CP-SAT violation BoolVar per Kann
''' ("should"-priority) constraint JSON object - keyed by the constraint's
''' index in JsonHelpers.Constraints(data), so a single flag covers every
''' slot/session that constraint touches (counting violated CONSTRAINTS,
''' not violated slot-occurrences, per the "simple binary" weighting
''' decision).</summary>
Public NotInheritable Class BuiltModel
    Public Property Model As CpModel
    Public Property Lesson As Dictionary(Of LessonKey, BoolVar)
    Public Property Room As Dictionary(Of RoomKey, BoolVar)
    Public Property Sessions As List(Of Session)
    Public Property Days As List(Of String)
    Public Property Periods As List(Of Integer)
    Public Property KannVars As Dictionary(Of Integer, (Type As String, Var As BoolVar, Reason As String))
End Class

Public NotInheritable Class ScheduleEntry
    Public Property ClassName As String
    Public Property Subject As String
    Public Property Teacher As String
    Public Property Day As String
    Public Property Period As Integer
    Public Property Room As String
End Class

''' <summary>Whether one specific Kann-constraint ended up violated in the
''' returned (optimal) solution, plus its type/reason for reporting -
''' see Verifier.VerifyScheduleDetailed for the schedule-derived
''' equivalent (independently re-checked, not read from these flags).</summary>
Public NotInheritable Class KannConstraintFlag
    Public Property ConstraintIndex As Integer
    Public Property ConstraintType As String
    Public Property Reason As String
    Public Property Relaxed As Boolean
End Class

Public NotInheritable Class SolveResult
    Public Property Status As CpSolverStatus
    Public Property Solver As CpSolver
    Public Property Schedule As List(Of ScheduleEntry)
    Public Property KannConstraintFlags As List(Of KannConstraintFlag)
End Class

''' <summary>Phase 2.8: why Solver.SolveTop's search loop stopped.</summary>
Public Enum MultiSolveStopReason
    MaxSolutionsReached
    TimeLimitReached
    ''' <summary>The solver returned Infeasible/ModelInvalid on a later
    ''' iteration - no schedule distinct from the ones already found
    ''' exists. If Solutions is empty, this means the scenario had no
    ''' feasible solution at all.</summary>
    SearchSpaceExhausted
End Enum

''' <summary>One candidate schedule from Solver.SolveTop, bundled with
''' everything a caller needs to render/rank it - same bundling philosophy
''' as KannViolationDetail (index/type/message/reason together).</summary>
Public NotInheritable Class ScoredSolution
    Public Property Schedule As List(Of ScheduleEntry)
    Public Property KannConstraintFlags As List(Of KannConstraintFlag)
    Public Property Quality As QualityScore
End Class

Public NotInheritable Class MultiSolveResult
    ''' <summary>Sorted ascending by Quality.Total - best candidate first.</summary>
    Public Property Solutions As List(Of ScoredSolution)
    Public Property StopReason As MultiSolveStopReason
    Public Property IterationsRun As Integer
    Public Property ElapsedS As Double
End Class

''' <summary>Replaces the plain Dictionary(Of String, List(Of String)) that
''' room_requirement used to collect into - now also carries the
''' constraint's priority/index/reason so BuildModel's room-variable
''' construction loop can decide must vs should per subject.</summary>
Friend NotInheritable Class RoomRequirementInfo
    Public ReadOnly AllowedRooms As List(Of String)
    Public ReadOnly Priority As String
    Public ReadOnly ConstraintIndex As Integer
    Public ReadOnly Reason As String

    Public Sub New(allowedRooms As List(Of String), priority As String, constraintIndex As Integer, reason As String)
        Me.AllowedRooms = allowedRooms
        Me.Priority = priority
        Me.ConstraintIndex = constraintIndex
        Me.Reason = reason
    End Sub
End Class

Public Module Solver

    ''' <summary>Phase 2.5: lazily creates (or returns the already-created)
    ''' shared violation BoolVar for the Kann-constraint at `index` - one
    ''' flag per JSON constraint object, reused across every slot/session it
    ''' touches, so the objective counts violated CONSTRAINTS not violated
    ''' occurrences.</summary>
    Private Function GetOrCreateKannVar(model As CpModel, kannVars As Dictionary(Of Integer, (Type As String, Var As BoolVar, Reason As String)),
                                         index As Integer, constraintType As String, reason As String) As BoolVar
        If Not kannVars.ContainsKey(index) Then
            Dim v = model.NewBoolVar($"kann_violated[{index}]")
            kannVars(index) = (constraintType, v, reason)
        End If
        Return kannVars(index).Var
    End Function

    Private Function SessionsFromAssignments(data As JsonObject) As List(Of Session)
        Dim sessions = JsonHelpers.Constraints(data).
            Where(Function(c) JsonHelpers.GetString(c, "type") = "teacher_subject_assignment").
            Select(Function(c) New Session(
                JsonHelpers.GetString(c, "class"),
                JsonHelpers.GetString(c, "subject"),
                JsonHelpers.GetString(c, "teacher"))).
            ToList()
        If sessions.Count = 0 Then
            Throw New ArgumentException("Keine teacher_subject_assignment-Constraints gefunden - es gibt nichts zu planen.")
        End If
        Return sessions
    End Function

    Public Function BuildModel(data As JsonObject) As BuiltModel
        Dim errors = Validation.ValidateEntities(data)
        If errors.Any() Then
            Throw New ArgumentException("Ungueltige Constraint-Referenzen:" & vbLf & String.Join(vbLf, errors))
        End If

        Dim model As New CpModel()
        Dim ent = JsonHelpers.Entities(data)
        Dim timeslots = JsonHelpers.Timeslots(ent)
        Dim days = JsonHelpers.AsStringList(timeslots, "days")
        Dim periodsPerDay = JsonHelpers.GetInt(timeslots, "periods_per_day").Value
        Dim periods = Enumerable.Range(1, periodsPerDay).ToList()

        Dim sessions = SessionsFromAssignments(data)

        Dim lesson As New Dictionary(Of LessonKey, BoolVar)
        For Each s In sessions
            For Each d In days
                For Each p In periods
                    Dim key As New LessonKey(s.ClassName, s.Subject, s.Teacher, d, p)
                    lesson(key) = model.NewBoolVar($"lesson[{s.ClassName},{s.Subject},{s.Teacher},{d},{p}]")
                Next
            Next
        Next

        Dim kannVars As New Dictionary(Of Integer, (Type As String, Var As BoolVar, Reason As String))

        Dim allConstraints = JsonHelpers.Constraints(data)
        Dim roomReq As New Dictionary(Of String, RoomRequirementInfo)
        For ri = 0 To allConstraints.Count - 1
            Dim c = allConstraints(ri)
            If JsonHelpers.GetString(c, "type") = "room_requirement" Then
                roomReq(JsonHelpers.GetString(c, "subject")) = New RoomRequirementInfo(
                    JsonHelpers.AsStringList(c, "allowed_rooms"), JsonHelpers.GetPriority(c), ri, JsonHelpers.GetReason(c))
            End If
        Next

        Dim room As New Dictionary(Of RoomKey, BoolVar)
        For Each s In sessions
            If Not roomReq.ContainsKey(s.Subject) Then Continue For
            Dim info = roomReq(s.Subject)
            Dim allowedRooms = info.AllowedRooms
            If allowedRooms.Count = 0 Then Continue For

            ' Phase 2.5: "should" splits the original equality
            ' (Sum(choices) = lesson) into an always-true upper bound (never
            ' assign an allowed room without the lesson, never more than
            ' one) plus a reified lower bound (the lesson MUST get one of
            ' the allowed rooms) - relaxing only the lower bound is what
            ' lets the lesson happen with no allowed room assigned instead
            ' of forcing Infeasible, without ever allowing more than one
            ' room to be assigned at once.
            Dim rrViolated As BoolVar = Nothing
            If info.Priority = JsonHelpers.PriorityShould Then
                rrViolated = GetOrCreateKannVar(model, kannVars, info.ConstraintIndex, "room_requirement", info.Reason)
            End If

            For Each d In days
                For Each p In periods
                    Dim choices As New List(Of BoolVar)
                    For Each r In allowedRooms
                        Dim v = model.NewBoolVar($"room[{s.ClassName},{s.Subject},{s.Teacher},{d},{p},{r}]")
                        room(New RoomKey(s.ClassName, s.Subject, s.Teacher, d, p, r)) = v
                        choices.Add(v)
                    Next
                    Dim lessonKey As New LessonKey(s.ClassName, s.Subject, s.Teacher, d, p)
                    If rrViolated Is Nothing Then
                        model.Add(LinearExpr.Sum(choices) = lesson(lessonKey))
                    Else
                        model.Add(LinearExpr.Sum(choices) <= lesson(lessonKey))
                        model.Add(LinearExpr.Sum(choices) >= lesson(lessonKey)).OnlyEnforceIf(rrViolated.Not())
                    End If
                Next
            Next
        Next

        ApplyConstraints(model, data, sessions, lesson, room, days, periods, kannVars)

        If kannVars.Count > 0 Then
            model.Minimize(LinearExpr.Sum(kannVars.Values.Select(Function(kv) kv.Var)))
        End If

        Return New BuiltModel With {
            .Model = model, .Lesson = lesson, .Room = room,
            .Sessions = sessions, .Days = days, .Periods = periods, .KannVars = kannVars
        }
    End Function

    Private Sub AddBlockConstraint(model As CpModel, lesson As Dictionary(Of LessonKey, BoolVar),
                                    session As Session, days As List(Of String), periods As List(Of Integer),
                                    blockLen As Integer, Optional violated As BoolVar = Nothing)
        Dim lastPeriod = periods(periods.Count - 1)
        For Each d In days
            Dim validStarts = periods.Where(Function(p) p + blockLen - 1 <= lastPeriod).ToList()
            Dim blockStart As New Dictionary(Of Integer, BoolVar)
            For Each p0 In validStarts
                blockStart(p0) = model.NewBoolVar($"blockstart[{session.ClassName},{session.Subject},{session.Teacher},{d},{p0}]")
            Next
            For Each p In periods
                Dim covering = validStarts.
                    Where(Function(p0) p0 <= p AndAlso p <= p0 + blockLen - 1).
                    Select(Function(p0) blockStart(p0)).
                    ToList()
                Dim key As New LessonKey(session.ClassName, session.Subject, session.Teacher, d, p)
                If violated Is Nothing Then
                    model.Add(lesson(key) = LinearExpr.Sum(covering))
                Else
                    ' Phase 2.5 "should": a chosen block-start still forces
                    ' its covered periods to be scheduled (structural, not a
                    ' preference - keep unconditional). Only the OTHER
                    ' direction - every scheduled period must belong to a
                    ' chosen block - is the actual "prefer blocks" ask, so
                    ' only that half is gated.
                    model.Add(lesson(key) >= LinearExpr.Sum(covering))
                    model.Add(lesson(key) <= LinearExpr.Sum(covering)).OnlyEnforceIf(violated.Not())
                End If
            Next
        Next
    End Sub

    Private Sub ApplyConstraints(model As CpModel, data As JsonObject, sessions As List(Of Session),
                                  lesson As Dictionary(Of LessonKey, BoolVar), room As Dictionary(Of RoomKey, BoolVar),
                                  days As List(Of String), periods As List(Of Integer),
                                  kannVars As Dictionary(Of Integer, (Type As String, Var As BoolVar, Reason As String)))

        Dim sessionsOfClass = Function(className As String) sessions.Where(Function(s) s.ClassName = className).ToList()
        Dim sessionsOfTeacher = Function(teacher As String) sessions.Where(Function(s) s.Teacher = teacher).ToList()
        Dim sessionsOfSubjectClass = Function(subject As String, className As String) _
            sessions.Where(Function(s) s.Subject = subject AndAlso s.ClassName = className).ToList()

        Dim constraintsList = JsonHelpers.Constraints(data)
        For ci = 0 To constraintsList.Count - 1
            Dim c = constraintsList(ci)
            Dim constraintType = JsonHelpers.GetString(c, "type")
            Dim priority = JsonHelpers.GetPriority(c)

            Select Case constraintType

                Case "teacher_availability"
                    Dim teacher = JsonHelpers.GetString(c, "teacher")
                    Dim availDaysList = JsonHelpers.AsStringList(c, "available_days")
                    Dim availDays As New HashSet(Of String)(If(availDaysList.Any(), availDaysList, days))
                    Dim blocked As New HashSet(Of (Day As String, Period As Integer))
                    If c.ContainsKey("unavailable_periods") AndAlso c("unavailable_periods") IsNot Nothing Then
                        For Each node In c("unavailable_periods").AsArray()
                            Dim entryObj = node.AsObject()
                            blocked.Add((JsonHelpers.GetString(entryObj, "day"), JsonHelpers.GetInt(entryObj, "period").Value))
                        Next
                    End If
                    Dim taViolated As BoolVar = Nothing
                    If priority = JsonHelpers.PriorityShould Then
                        taViolated = GetOrCreateKannVar(model, kannVars, ci, constraintType, JsonHelpers.GetReason(c))
                    End If
                    For Each s In sessionsOfTeacher(teacher)
                        For Each d In days
                            For Each p In periods
                                If Not availDays.Contains(d) OrElse blocked.Contains((d, p)) Then
                                    Dim con = model.Add(lesson(New LessonKey(s.ClassName, s.Subject, s.Teacher, d, p)) = 0)
                                    If taViolated IsNot Nothing Then con.OnlyEnforceIf(taViolated.Not())
                                End If
                            Next
                        Next
                    Next

                Case "weekly_hours"
                    Dim className = JsonHelpers.GetString(c, "class")
                    Dim subject = JsonHelpers.GetString(c, "subject")
                    Dim hoursPerWeek = JsonHelpers.GetInt(c, "hours_per_week").Value
                    Dim maxPerDay = JsonHelpers.GetInt(c, "max_per_day")
                    ' priority only ever governs the max_per_day cap below -
                    ' hours_per_week's exact-count stays always-must
                    ' (Validation.vb rejects "should" without max_per_day).
                    Dim whViolated As BoolVar = Nothing
                    If priority = JsonHelpers.PriorityShould Then
                        whViolated = GetOrCreateKannVar(model, kannVars, ci, constraintType, JsonHelpers.GetReason(c))
                    End If
                    For Each s In sessionsOfSubjectClass(subject, className)
                        Dim terms As New List(Of BoolVar)
                        For Each d In days
                            For Each p In periods
                                terms.Add(lesson(New LessonKey(s.ClassName, s.Subject, s.Teacher, d, p)))
                            Next
                        Next
                        model.Add(LinearExpr.Sum(terms) = hoursPerWeek)

                        If maxPerDay.HasValue AndAlso maxPerDay.Value <> 0 Then
                            For Each d In days
                                Dim dayTerms = periods.
                                    Select(Function(p) lesson(New LessonKey(s.ClassName, s.Subject, s.Teacher, d, p))).
                                    ToList()
                                Dim con = model.Add(LinearExpr.Sum(dayTerms) <= maxPerDay.Value)
                                If whViolated IsNot Nothing Then con.OnlyEnforceIf(whViolated.Not())
                            Next
                        End If
                    Next

                Case "no_overlap"
                    Dim resource = JsonHelpers.GetString(c, "resource")
                    Dim entityVal = JsonHelpers.GetString(c, "entity")
                    Dim relevantSessions As List(Of Session)
                    Select Case resource
                        Case "class"
                            relevantSessions = sessionsOfClass(entityVal)
                        Case "teacher"
                            relevantSessions = sessionsOfTeacher(entityVal)
                        Case "room"
                            relevantSessions = sessions
                        Case Else
                            relevantSessions = New List(Of Session)
                    End Select

                    For Each d In days
                        For Each p In periods
                            Dim terms As New List(Of BoolVar)
                            If resource = "room" Then
                                For Each s In relevantSessions
                                    Dim key As New RoomKey(s.ClassName, s.Subject, s.Teacher, d, p, entityVal)
                                    If room.ContainsKey(key) Then terms.Add(room(key))
                                Next
                            Else
                                For Each s In relevantSessions
                                    terms.Add(lesson(New LessonKey(s.ClassName, s.Subject, s.Teacher, d, p)))
                                Next
                            End If
                            If terms.Count > 0 Then
                                model.Add(LinearExpr.Sum(terms) <= 1)
                            End If
                        Next
                    Next

                Case "shared_resource_conflict"
                    Dim classesInvolved = JsonHelpers.AsStringList(c, "classes")
                    Dim sharedSubject = JsonHelpers.GetString(c, "subject")
                    Dim sharedTeacher = JsonHelpers.GetString(c, "teacher")
                    Dim relevantSessions = classesInvolved.
                        SelectMany(Function(cls) sessionsOfSubjectClass(sharedSubject, cls)).
                        Where(Function(s) s.Teacher = sharedTeacher).
                        ToList()
                    For Each d In days
                        For Each p In periods
                            Dim terms = relevantSessions.
                                Select(Function(s) lesson(New LessonKey(s.ClassName, s.Subject, s.Teacher, d, p))).
                                ToList()
                            If terms.Count > 0 Then
                                model.Add(LinearExpr.Sum(terms) <= 1)
                            End If
                        Next
                    Next

                Case "forbidden_slot"
                    Dim scope = JsonHelpers.GetString(c, "scope")
                    Dim entityVal = JsonHelpers.GetString(c, "entity")
                    Dim day = JsonHelpers.GetString(c, "day")
                    Dim period = JsonHelpers.GetInt(c, "period").Value

                    Dim relevantSessions As List(Of Session)
                    Select Case scope
                        Case "class"
                            relevantSessions = sessionsOfClass(entityVal)
                        Case "teacher"
                            relevantSessions = sessionsOfTeacher(entityVal)
                        Case "room"
                            relevantSessions = sessions
                        Case Else
                            relevantSessions = New List(Of Session)
                    End Select

                    Dim fsViolated As BoolVar = Nothing
                    If priority = JsonHelpers.PriorityShould Then
                        fsViolated = GetOrCreateKannVar(model, kannVars, ci, constraintType, JsonHelpers.GetReason(c))
                    End If

                    If scope = "room" Then
                        For Each s In relevantSessions
                            Dim key As New RoomKey(s.ClassName, s.Subject, s.Teacher, day, period, entityVal)
                            If room.ContainsKey(key) Then
                                Dim con = model.Add(room(key) = 0)
                                If fsViolated IsNot Nothing Then con.OnlyEnforceIf(fsViolated.Not())
                            End If
                        Next
                    Else
                        For Each s In relevantSessions
                            Dim key As New LessonKey(s.ClassName, s.Subject, s.Teacher, day, period)
                            If lesson.ContainsKey(key) Then
                                Dim con = model.Add(lesson(key) = 0)
                                If fsViolated IsNot Nothing Then con.OnlyEnforceIf(fsViolated.Not())
                            End If
                        Next
                    End If

                Case "consecutive_required"
                    Dim className = JsonHelpers.GetString(c, "class")
                    Dim subject = JsonHelpers.GetString(c, "subject")
                    Dim blockLen = JsonHelpers.GetInt(c, "block_length").Value
                    Dim crViolated As BoolVar = Nothing
                    If priority = JsonHelpers.PriorityShould Then
                        crViolated = GetOrCreateKannVar(model, kannVars, ci, constraintType, JsonHelpers.GetReason(c))
                    End If
                    For Each s In sessionsOfSubjectClass(subject, className)
                        AddBlockConstraint(model, lesson, s, days, periods, blockLen, crViolated)
                    Next

                Case "teacher_subject_assignment", "room_requirement"
                    ' Already consumed above (session/room-choice construction); no direct constraint here.

                Case Else
                    Throw New ArgumentException($"Unbekannter Constraint-Typ: '{constraintType}'")

            End Select
        Next
    End Sub

    ''' <summary>Phase 2.8: factored out of Solve() (verbatim body, no logic
    ''' change) so SolveTop can reuse the same extraction on every iteration
    ''' of its multi-solution loop without duplicating it.</summary>
    Private Function ExtractSchedule(built As BuiltModel, solver As CpSolver, status As CpSolverStatus) As List(Of ScheduleEntry)
        If status <> CpSolverStatus.Optimal AndAlso status <> CpSolverStatus.Feasible Then Return Nothing
        Dim schedule As New List(Of ScheduleEntry)
        For Each kvp In built.Lesson
            If solver.BooleanValue(kvp.Value) Then
                Dim key = kvp.Key
                Dim assignedRoom As String = Nothing
                For Each rkvp In built.Room
                    Dim rk = rkvp.Key
                    If rk.ClassName = key.ClassName AndAlso rk.Subject = key.Subject AndAlso
                       rk.Teacher = key.Teacher AndAlso rk.Day = key.Day AndAlso rk.Period = key.Period AndAlso
                       solver.BooleanValue(rkvp.Value) Then
                        assignedRoom = rk.Room
                        Exit For
                    End If
                Next
                schedule.Add(New ScheduleEntry With {
                    .ClassName = key.ClassName, .Subject = key.Subject, .Teacher = key.Teacher,
                    .Day = key.Day, .Period = key.Period, .Room = assignedRoom
                })
            End If
        Next
        Return schedule
    End Function

    ''' <summary>Phase 2.8: factored out of Solve() (verbatim body, no logic
    ''' change) - see ExtractSchedule.</summary>
    Private Function ExtractKannFlags(built As BuiltModel, solver As CpSolver, status As CpSolverStatus) As List(Of KannConstraintFlag)
        If status <> CpSolverStatus.Optimal AndAlso status <> CpSolverStatus.Feasible Then Return Nothing
        Dim kannFlags As New List(Of KannConstraintFlag)
        For Each kvp In built.KannVars
            kannFlags.Add(New KannConstraintFlag With {
                .ConstraintIndex = kvp.Key, .ConstraintType = kvp.Value.Type,
                .Reason = kvp.Value.Reason, .Relaxed = solver.BooleanValue(kvp.Value.Var)
            })
        Next
        Return kannFlags
    End Function

    Public Function Solve(data As JsonObject,
                           Optional timeLimitS As Double = 30.0,
                           Optional seed As Integer = 42,
                           Optional numWorkers As Integer = 1) As SolveResult
        Dim built = BuildModel(data)
        Dim solver As New CpSolver()
        solver.StringParameters = $"max_time_in_seconds:{timeLimitS.ToString(Globalization.CultureInfo.InvariantCulture)},random_seed:{seed},num_search_workers:{numWorkers}"
        Dim status = solver.Solve(built.Model)

        Dim schedule = ExtractSchedule(built, solver, status)
        Dim kannFlags = ExtractKannFlags(built, solver, status)

        Return New SolveResult With {.Status = status, .Solver = solver, .Schedule = schedule, .KannConstraintFlags = kannFlags}
    End Function

    ''' <summary>Phase 2.8: adds a hard "no-good" cut to `model` forbidding
    ''' the exact `Lesson`-variable assignment `solver` just found from ever
    ''' recurring. Deliberately scoped to `Lesson` only (not `Room`) - that
    ''' is what most naturally defines "a different schedule"; alternate
    ''' Room-only variants of an already-returned Lesson assignment are not
    ''' separately enumerated (documented, deliberate scope, not a bug).
    ''' Verified against the actual installed OrTools build: BoolVar
    ''' implements ILiteral directly (no cast needed) and this exact
    ''' true-Not()/false-as-is split enumerates a small model's full
    ''' distinct-assignment space with zero duplicates/omissions.</summary>
    Private Sub BlockSolution(model As CpModel, lesson As Dictionary(Of LessonKey, BoolVar), solver As CpSolver)
        Dim literals As New List(Of ILiteral)
        For Each kvp In lesson
            If solver.BooleanValue(kvp.Value) Then
                literals.Add(kvp.Value.Not())
            Else
                literals.Add(kvp.Value)
            End If
        Next
        model.AddBoolOr(literals)
    End Sub

    ''' <summary>Phase 2.8: returns up to `maxSolutions` distinct candidate
    ''' schedules, ranked by ScheduleQuality.Score (ascending Total, best
    ''' first), instead of Solve()'s single result. Builds the model once,
    ''' then repeatedly solves the SAME CpModel with a fresh CpSolver each
    ''' iteration - BlockSolution accumulates a no-good constraint after
    ''' each find, so later iterations can never reproduce an earlier
    ''' schedule. Stops on whichever of maxSolutions/totalTimeLimitS is hit
    ''' first, or when the solver reports Infeasible (the distinct-solution
    ''' search space is exhausted).
    '''
    ''' Because BuildModel still sets model.Minimize(...) whenever Kann
    ''' constraints exist, every iteration keeps optimizing against that
    ''' same objective - early iterations tend to surface the lowest-Kann-
    ''' violation schedules first. Later iterations may return Feasible
    ''' rather than Optimal if perSolveTimeLimitS is too tight to prove
    ''' optimality within the shrinking remaining search space; this is
    ''' still a usable candidate, and correctness of the final ranking
    ''' never depends on iteration order since Solutions is always
    ''' re-sorted by Quality.Total before returning.</summary>
    Public Function SolveTop(data As JsonObject,
                              Optional maxSolutions As Integer = 10,
                              Optional totalTimeLimitS As Double = 120.0,
                              Optional perSolveTimeLimitS As Double = 30.0,
                              Optional seed As Integer = 42,
                              Optional numWorkers As Integer = 1) As MultiSolveResult
        Dim built = BuildModel(data)
        Dim solutions As New List(Of ScoredSolution)
        Dim sw = Stopwatch.StartNew()
        Dim iterations = 0
        Dim stopReason As MultiSolveStopReason

        Do
            Dim remaining = totalTimeLimitS - sw.Elapsed.TotalSeconds
            If solutions.Count >= maxSolutions Then
                stopReason = MultiSolveStopReason.MaxSolutionsReached
                Exit Do
            End If
            If remaining <= 0 Then
                stopReason = MultiSolveStopReason.TimeLimitReached
                Exit Do
            End If
            Dim thisLimit = Math.Min(perSolveTimeLimitS, remaining)

            Dim solver As New CpSolver()
            solver.StringParameters = $"max_time_in_seconds:{thisLimit.ToString(Globalization.CultureInfo.InvariantCulture)},random_seed:{seed},num_search_workers:{numWorkers}"
            Dim status = solver.Solve(built.Model)
            iterations += 1

            If status = CpSolverStatus.Infeasible OrElse status = CpSolverStatus.ModelInvalid Then
                ' A genuine proof that no further distinct solution exists.
                stopReason = MultiSolveStopReason.SearchSpaceExhausted
                Exit Do
            ElseIf status <> CpSolverStatus.Optimal AndAlso status <> CpSolverStatus.Feasible Then
                ' CpSolverStatus.Unknown: this solve's own time budget ran
                ' out without a conclusive answer either way - that is a
                ' time-budget exhaustion, not a proof the search space is
                ' exhausted, so it must not be mislabeled as such.
                stopReason = MultiSolveStopReason.TimeLimitReached
                Exit Do
            End If

            Dim schedule = ExtractSchedule(built, solver, status)
            Dim kannFlags = ExtractKannFlags(built, solver, status)
            Dim kannCount = Verifier.VerifyScheduleDetailed(data, schedule).KannViolations.Count
            Dim quality = ScheduleQuality.Score(data, schedule, kannCount)
            solutions.Add(New ScoredSolution With {.Schedule = schedule, .KannConstraintFlags = kannFlags, .Quality = quality})

            BlockSolution(built.Model, built.Lesson, solver)
        Loop

        Return New MultiSolveResult With {
            .Solutions = solutions.OrderBy(Function(s) s.Quality.Total).ToList(),
            .StopReason = stopReason, .IterationsRun = iterations, .ElapsedS = sw.Elapsed.TotalSeconds
        }
    End Function

    Public Function StatusName(status As CpSolverStatus) As String
        Return status.ToString()
    End Function

End Module

