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
Imports System.Threading.Tasks
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

''' <summary>One incumbent improvement CP-SAT found while solving a single
''' SolveTop iteration - ElapsedS/ObjectiveValue at the moment a new, better
''' solution was accepted (NOT a fixed sampling interval - CP-SAT calls the
''' underlying callback exactly once per incumbent, so a flat tail simply
''' means no further improvement happened before the time limit).</summary>
Public NotInheritable Class ConvergencePoint
    Public Property ElapsedS As Double
    Public Property ObjectiveValue As Double
End Class

''' <summary>Records every incumbent CP-SAT finds during one Solve() call,
''' live-verified against the installed Google.OrTools DLL (9.15.6755):
''' CpSolverSolutionCallback.OnSolutionCallback() fires per improving
''' solution, and WallTime()/ObjectiveValue() are both readable from
''' inside it (inherited from the SolutionCallback base class).</summary>
Friend NotInheritable Class ConvergenceCallback
    Inherits CpSolverSolutionCallback
    Public ReadOnly Points As New List(Of ConvergencePoint)
    Public Overrides Sub OnSolutionCallback()
        Points.Add(New ConvergencePoint With {.ElapsedS = WallTime(), .ObjectiveValue = ObjectiveValue()})
    End Sub
End Class

''' <summary>One candidate schedule from Solver.SolveTop, bundled with
''' everything a caller needs to render/rank it - same bundling philosophy
''' as KannViolationDetail (index/type/message/reason together).</summary>
Public NotInheritable Class ScoredSolution
    Public Property Schedule As List(Of ScheduleEntry)
    Public Property KannConstraintFlags As List(Of KannConstraintFlag)
    Public Property Quality As QualityScore
    ''' <summary>Phase 2.18-Nachtrag: Optimal means CP-SAT proved no better
    ''' solution exists within this solve's model; Feasible means the
    ''' per-solve time limit ran out before optimality could be proven - a
    ''' caller that wants a stronger optimality guarantee should raise
    ''' perSolveTimeLimitS/totalTimeLimitS rather than maxSolutions (later
    ''' iterations can only find equally-good alternatives, never a better
    ''' Quality.Total, since they all optimize the same objective and only
    ''' exclude already-found exact Lesson assignments).</summary>
    Public Property Status As CpSolverStatus
    ''' <summary>The raw CP-SAT objective (the same weighted Kann/Luecken/
    ''' Randstunden/Ausgewogenheits-sum SolveTopObjective.ApplyQualityObjective
    ''' builds into the model) that THIS solve iteration found - normally
    ''' tracks Quality.Total closely, but is the model's own value, not a
    ''' post-hoc recomputation (see Quality.Total's own doc comment for the
    ''' narrow case where they can diverge).</summary>
    Public Property ObjectiveValue As Double
    ''' <summary>CP-SAT's proven lower bound on the objective for this
    ''' iteration - at Status=Optimal this equals ObjectiveValue exactly
    ''' (zero gap, proven optimal); at Status=Feasible it is strictly lower,
    ''' and (ObjectiveValue - BestObjectiveBound) is how much better a
    ''' solution COULD exist, not how much better one DOES exist.</summary>
    Public Property BestObjectiveBound As Double
    ''' <summary>Every incumbent this iteration's Solve() call found, in
    ''' order - lets a caller show "how much did quality still improve over
    ''' time" instead of only the final number. Defaults to an empty list
    ''' (not Nothing) so a caller/test that constructs a ScoredSolution by
    ''' hand without setting this - e.g. StundentafelJsonTests.vb's
    ''' hand-built ScoredSolutions - never hits a NullReferenceException.</summary>
    Public Property Convergence As New List(Of ConvergencePoint)
End Class

Public NotInheritable Class MultiSolveResult
    ''' <summary>Sorted ascending by Quality.Total - best candidate first.</summary>
    Public Property Solutions As List(Of ScoredSolution)
    Public Property StopReason As MultiSolveStopReason
    Public Property IterationsRun As Integer
    Public Property ElapsedS As Double
    ''' <summary>Phase 2.25: how many of the IterationsRun solves were cut
    ''' short by the stagnation-cutoff (see SolveWithStagnationCutoff) rather
    ''' than running to their natural time limit or proving Optimal - lets a
    ''' caller see whether/how often the mechanism actually fired instead of
    ''' leaving it invisible. 0 whenever stagnationTimeoutS is Nothing.</summary>
    Public Property StagnationTriggeredCount As Integer
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

    ''' <summary>Phase 2.9: everything BuildModel does EXCEPT the final
    ''' Minimize call, factored out so SolveTop can build its own, richer
    ''' quality-aware objective on top of the same core model instead of
    ''' the Kann-only one BuildModel sets. Verbatim body (no reordering) -
    ''' CP-SAT's random_seed behavior is sensitive to variable-creation
    ''' order, so this must stay a pure cut-paste of BuildModel's former
    ''' pre-Minimize logic.</summary>
    Private Function BuildCoreModel(data As JsonObject) As BuiltModel
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

        ' Phase 2.20: Parallelgruppe-Pre-Pass - jede "parallel_group"-
        ' Constraint bekommt pro (Tag,Periode) EINE geteilte BoolVar; jedes
        ' Mitglied-Tripel (Klasse,Fach,Lehrer) wird per Gleichheit daran
        ' gekoppelt, was Kreuz-Klassen-Synchronisation automatisch erzwingt
        ' (identisches Slot-Muster ueber alle Mitglieder hinweg, auch wenn
        ' sie zu verschiedenen echten Klassen gehoeren). Muss VOR
        ' ApplyConstraints laufen, damit dessen "no_overlap"-Fall die
        ' Gruppenzugehoerigkeit kennt: ohne Deduplizierung dort wuerde
        ' no_overlap dieselbe (jetzt gleiche) Variable mehrfach in die Summe
        ' aufnehmen und sie so permanent auf 0 zwingen (live durch einen
        ' Plan-Agenten gegen den Code verifizierter Befund).
        Dim parallelGroupOf As New Dictionary(Of (ClassName As String, Subject As String, Teacher As String), Integer)
        Dim parallelVars As New Dictionary(Of (GroupIndex As Integer, Day As String, Period As Integer), BoolVar)
        For ci = 0 To allConstraints.Count - 1
            Dim c = allConstraints(ci)
            If JsonHelpers.GetString(c, "type") <> "parallel_group" Then Continue For
            Dim classesInGroup = JsonHelpers.AsStringList(c, "classes")
            Dim subjectsInGroup = JsonHelpers.AsStringList(c, "subjects")
            Dim teachersInGroup = JsonHelpers.AsStringList(c, "teachers")
            For Each d In days
                For Each p In periods
                    parallelVars((ci, d, p)) = model.NewBoolVar($"parallel[{ci},{d},{p}]")
                Next
            Next
            For mi = 0 To classesInGroup.Count - 1
                Dim memberKey As (ClassName As String, Subject As String, Teacher As String) =
                    (classesInGroup(mi), subjectsInGroup(mi), teachersInGroup(mi))
                parallelGroupOf(memberKey) = ci
                For Each d In days
                    For Each p In periods
                        Dim lessonKey As New LessonKey(memberKey.ClassName, memberKey.Subject, memberKey.Teacher, d, p)
                        If lesson.ContainsKey(lessonKey) Then
                            model.Add(lesson(lessonKey) = parallelVars((ci, d, p)))
                        End If
                    Next
                Next
            Next
        Next

        ApplyConstraints(model, data, sessions, lesson, room, days, periods, kannVars, parallelGroupOf, parallelVars)

        Return New BuiltModel With {
            .Model = model, .Lesson = lesson, .Room = room,
            .Sessions = sessions, .Days = days, .Periods = periods, .KannVars = kannVars
        }
    End Function

    ''' <summary>Phase 2.12: the exact one-line Kann-only objective BuildModel
    ''' already sets - factored out so SolveTop's Stage 1 warm-start solve
    ''' can reuse the identical, already-proven-fast objective without
    ''' duplicating BuildModel's logic. BuildModel's own behavior is
    ''' unchanged (same computation, same object identity of the resulting
    ''' LinearExpr construction).</summary>
    Private Function KannOnlyObjectiveExpr(kannVars As Dictionary(Of Integer, (Type As String, Var As BoolVar, Reason As String))) As LinearExpr
        Return LinearExpr.Sum(kannVars.Values.Select(Function(kv) kv.Var))
    End Function

    Public Function BuildModel(data As JsonObject) As BuiltModel
        Dim built = BuildCoreModel(data)
        If built.KannVars.Count > 0 Then
            built.Model.Minimize(KannOnlyObjectiveExpr(built.KannVars))
        End If
        Return built
    End Function

    ''' <summary>Phase 2.12: replaces any previously-set hints on `model`
    ''' with `solver`'s just-found Boolean values for every `lesson` var -
    ''' shared by the Stage 1 -&gt; Stage 2 warm-start handoff and by
    ''' SolveTop's own iteration-to-iteration carryover. Deliberately
    ''' Lesson-only (not the auxiliary occupied/hasAny/gapVar/rangeVar vars
    ''' SolveTopObjective adds) - same "Lesson defines the schedule" scoping
    ''' BlockSolution already uses; those auxiliary vars are pure reified
    ''' functions of Lesson, and a live smoke test against the installed
    ''' OrTools build (Phase 2.12a) confirmed CP-SAT's search log explicitly
    ''' reports such a partial hint as "complete and feasible" - i.e. it
    ''' derives the rest via propagation rather than requiring full
    ''' coverage. ClearHints() first: the live smoke test confirmed this
    ''' sequence (ClearHints then AddHint again) raises no exception and the
    ''' model keeps solving correctly, the expected way to replace a
    ''' previously-set hint between successive Solve() calls on the same
    ''' mutated CpModel (same "one CpModel, many Solve() calls" pattern
    ''' BlockSolution already established).</summary>
    Private Sub ApplyLessonHints(model As CpModel, lesson As Dictionary(Of LessonKey, BoolVar), solver As CpSolver)
        model.ClearHints()
        For Each kvp In lesson
            model.AddHint(kvp.Value, solver.BooleanValue(kvp.Value))
        Next
    End Sub

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
                                  kannVars As Dictionary(Of Integer, (Type As String, Var As BoolVar, Reason As String)),
                                  parallelGroupOf As Dictionary(Of (ClassName As String, Subject As String, Teacher As String), Integer),
                                  parallelVars As Dictionary(Of (GroupIndex As Integer, Day As String, Period As Integer), BoolVar))

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
                                ' Phase 2.20: Sessions belonging to the same
                                ' Parallelgruppe must contribute only ONE
                                ' shared term (the group's own parallelVar
                                ' for this slot) instead of one term per
                                ' member - their Lesson vars were already
                                ' forced equal in the pre-pass, so summing
                                ' each individually would count the same
                                ' value multiple times and force the group
                                ' permanently to 0 (the exact degeneracy the
                                ' Plan-Agent review flagged).
                                Dim countedGroups As New HashSet(Of Integer)
                                For Each s In relevantSessions
                                    Dim gi As Integer
                                    If parallelGroupOf.TryGetValue((s.ClassName, s.Subject, s.Teacher), gi) Then
                                        If countedGroups.Add(gi) Then terms.Add(parallelVars((gi, d, p)))
                                    Else
                                        terms.Add(lesson(New LessonKey(s.ClassName, s.Subject, s.Teacher, d, p)))
                                    End If
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

                Case "required_slot"
                    ' Phase 2.23: positives Gegenstueck zu forbidden_slot -
                    ' erzwingt (statt verbietet) eine (Klasse,Fach)-Session
                    ' auf einem exakten (Tag,Periode)-Slot. Kombiniert mit
                    ' dem bestehenden parallel_group-Pre-Pass reicht es, NUR
                    ' EIN Mitglied einer synchronisierten Gruppe zu pinnen -
                    ' die per Gleichheit gekoppelten uebrigen Mitglieder
                    ' wandern automatisch mit.
                    Dim reqClassName = JsonHelpers.GetString(c, "class")
                    Dim reqSubject = JsonHelpers.GetString(c, "subject")
                    Dim reqDay = JsonHelpers.GetString(c, "day")
                    Dim reqPeriod = JsonHelpers.GetInt(c, "period").Value

                    Dim rsViolated As BoolVar = Nothing
                    If priority = JsonHelpers.PriorityShould Then
                        rsViolated = GetOrCreateKannVar(model, kannVars, ci, constraintType, JsonHelpers.GetReason(c))
                    End If

                    For Each s In sessionsOfSubjectClass(reqSubject, reqClassName)
                        Dim key As New LessonKey(s.ClassName, s.Subject, s.Teacher, reqDay, reqPeriod)
                        If lesson.ContainsKey(key) Then
                            Dim con = model.Add(lesson(key) = 1)
                            If rsViolated IsNot Nothing Then con.OnlyEnforceIf(rsViolated.Not())
                        End If
                    Next

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

                Case "teacher_subject_assignment", "room_requirement", "parallel_group"
                    ' Already consumed above (session/room-choice construction / Phase-2.20 Parallelgruppe-Pre-Pass); no direct constraint here.

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

    ''' <summary>Phase 2.25: runs `solver.Solve(model, callback)` on a
    ''' background Task and, if `stagnationTimeoutS` is set, polls the
    ''' calling thread every 500ms for how long it has been since
    ''' `callback`'s last recorded improvement (CpSolverSolutionCallback.
    ''' OnSolutionCallback only fires on a NEW incumbent, never periodically -
    ''' there is no way to observe "stuck for N seconds" from inside the
    ''' callback itself, hence the separate polling thread). Once idle time
    ''' exceeds the timeout AND at least one solution already exists, calls
    ''' `solver.StopSearch()` - live-verified (Phase 2.25a) to make an
    ''' in-progress cross-thread Solve() call return promptly with its
    ''' best-found result rather than running out the full time budget on a
    ''' plateau. `stagnationTimeoutS = Nothing` (or an iteration whose time
    ''' budget is already shorter) reduces this to a plain, un-cutoff
    ''' `solver.Solve(model, callback)` call - no behavior change from before
    ''' this phase for that case.</summary>
    Private Function SolveWithStagnationCutoff(model As CpModel, solver As CpSolver, callback As ConvergenceCallback,
                                                 stagnationTimeoutS As Double?, ByRef triggered As Boolean) As CpSolverStatus
        If Not stagnationTimeoutS.HasValue Then
            Return solver.Solve(model, callback)
        End If
        Dim solveTask = Task.Run(Function() solver.Solve(model, callback))
        Dim sw = Stopwatch.StartNew()
        Do
            If solveTask.Wait(500) Then Exit Do
            Dim lastImprovementS = If(callback.Points.Count > 0, callback.Points.Last().ElapsedS, 0.0)
            Dim idleS = sw.Elapsed.TotalSeconds - lastImprovementS
            If callback.Points.Count > 0 AndAlso idleS >= stagnationTimeoutS.Value Then
                triggered = True
                solver.StopSearch()
                solveTask.Wait()
                Exit Do
            End If
        Loop
        Return solveTask.Result
    End Function

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
    ''' re-sorted by Quality.Total before returning.
    '''
    ''' Phase 2.12: `useStagedHints` (default True) fixes a real, observed
    ''' cold-start weakness - the full quality objective's many auxiliary
    ''' reified variables (see SolveTopObjective) make the search itself,
    ''' not just optimality-proving, dramatically harder than the plain
    ''' Kann-only model at large scale (a 30-class/75-teacher scenario found
    ''' ZERO feasible solutions in 30 minutes at numWorkers:=1 without this,
    ''' despite the Kann-only model solving the same scenario's constraints
    ''' in ~93s). When enabled, a warm-start Stage 1 solve first finds a
    ''' Kann-only-optimal complete schedule (reusing KannOnlyObjectiveExpr,
    ''' the exact objective BuildModel already uses) within `stage1TimeLimitS`,
    ''' then hints Stage 2's full-objective solve with that complete Lesson
    ''' assignment via ApplyLessonHints - CP-SAT starts from a known-valid
    ''' point instead of searching for one from nothing (a live smoke test,
    ''' Phase 2.12a, confirmed CP-SAT completes such a partial hint - only
    ''' Lesson vars, not the auxiliary ones - via propagation rather than
    ''' requiring full coverage). The same helper then re-hints each
    ''' subsequent iteration with its own just-found (now no-good-blocked,
    ''' but still a useful "near miss" search bias) solution, instead of
    ''' every iteration after the first starting cold. Hints only bias
    ''' search order; they never change the feasible-solution set or any
    ''' correctness guarantee.
    '''
    ''' Phase 2.25: `stagnationTimeoutS` (default 45.0s, ACTIVE BY DEFAULT -
    ''' a deliberate exception to this module's usual "Nothing = today's
    ''' behavior" convention, per explicit user decision) cuts an iteration
    ''' short via SolveWithStagnationCutoff once it has gone that long
    ''' without a new incumbent AND already has at least one solution -
    ''' motivated by a live-measured real scenario (bw-grundschule-beispiel)
    ''' where BestObjectiveBound sat completely flat for a full 30-minute
    ''' single run AND identically across 4 different-seed runs, while
    ''' `perSolveTimeLimitS`/`totalTimeLimitS` had no way to react to that
    ''' plateau except waiting it out. At the small default budgets most
    ''' callers use (`perSolveTimeLimitS` default 30.0s), the cutoff is
    ''' shorter than the iteration's own time limit and never fires - no
    ''' behavior change there. `diversifySeed` (default True) makes
    ''' iterations after the first use `seed + iterations` instead of the
    ''' same fixed seed, so a stagnation-triggered or natural next iteration
    ''' explores a different part of the search space instead of retracing
    ''' the same one (still fully deterministic for repeated SolveTop calls
    ''' with the same base seed, since it's a pure function of the iteration
    ''' index). `randomizeSearch` (default True) sets CP-SAT's
    ''' `randomize_search` parameter, adding search-order diversity
    ''' independent of `numWorkers` (helpful at `numWorkers:=1`, where
    ''' portfolio threading isn't already providing that). `relativeGapLimit`
    ''' stays opt-in (default Nothing) - unlike the above, it changes WHEN
    ''' CP-SAT accepts a solution as proven-final, a stronger behavioral
    ''' change this phase deliberately does not force on every caller.</summary>
    Public Function SolveTop(data As JsonObject,
                              Optional maxSolutions As Integer = 10,
                              Optional totalTimeLimitS As Double = 120.0,
                              Optional perSolveTimeLimitS As Double = 30.0,
                              Optional seed As Integer = 42,
                              Optional numWorkers As Integer = 1,
                              Optional stage1TimeLimitS As Double = 60.0,
                              Optional useStagedHints As Boolean = True,
                              Optional qualityWeights As QualityWeights = Nothing,
                              Optional stagnationTimeoutS As Double? = 45.0,
                              Optional diversifySeed As Boolean = True,
                              Optional randomizeSearch As Boolean = True,
                              Optional relativeGapLimit As Double? = Nothing) As MultiSolveResult
        Dim weights = If(qualityWeights, New QualityWeights())
        Dim built = BuildCoreModel(data)
        Dim sw = Stopwatch.StartNew()

        If useStagedHints Then
            Dim stage1Limit = Math.Min(stage1TimeLimitS, Math.Max(totalTimeLimitS - sw.Elapsed.TotalSeconds, 0.0))
            If stage1Limit > 0 Then
                If built.KannVars.Count > 0 Then
                    built.Model.Minimize(KannOnlyObjectiveExpr(built.KannVars))
                End If
                Dim stage1Solver As New CpSolver()
                stage1Solver.StringParameters = $"max_time_in_seconds:{stage1Limit.ToString(Globalization.CultureInfo.InvariantCulture)},random_seed:{seed},num_search_workers:{numWorkers}"
                Dim stage1Status = stage1Solver.Solve(built.Model)
                If stage1Status = CpSolverStatus.Optimal OrElse stage1Status = CpSolverStatus.Feasible Then
                    ApplyLessonHints(built.Model, built.Lesson, stage1Solver)
                End If
            End If
        End If

        SolveTopObjective.ApplyQualityObjective(built, data, weights)
        Dim solutions As New List(Of ScoredSolution)
        Dim iterations = 0
        Dim stagnationTriggeredCount = 0
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

            Dim effectiveSeed = If(diversifySeed, seed + iterations, seed)
            Dim paramsStr = $"max_time_in_seconds:{thisLimit.ToString(Globalization.CultureInfo.InvariantCulture)},random_seed:{effectiveSeed},num_search_workers:{numWorkers}"
            If randomizeSearch Then paramsStr &= ",randomize_search:true"
            If relativeGapLimit.HasValue Then paramsStr &= $",relative_gap_limit:{relativeGapLimit.Value.ToString(Globalization.CultureInfo.InvariantCulture)}"

            Dim solver As New CpSolver()
            solver.StringParameters = paramsStr
            Dim convergenceCb As New ConvergenceCallback()
            Dim triggered = False
            Dim thisStagnationTimeout = If(stagnationTimeoutS.HasValue AndAlso stagnationTimeoutS.Value < thisLimit, stagnationTimeoutS, Nothing)
            Dim status = SolveWithStagnationCutoff(built.Model, solver, convergenceCb, thisStagnationTimeout, triggered)
            If triggered Then stagnationTriggeredCount += 1
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
            Dim quality = ScheduleQuality.Score(data, schedule, kannCount, weights)
            solutions.Add(New ScoredSolution With {
                .Schedule = schedule, .KannConstraintFlags = kannFlags, .Quality = quality, .Status = status,
                .ObjectiveValue = solver.ObjectiveValue, .BestObjectiveBound = solver.BestObjectiveBound,
                .Convergence = convergenceCb.Points})

            BlockSolution(built.Model, built.Lesson, solver)
            If useStagedHints Then ApplyLessonHints(built.Model, built.Lesson, solver)
        Loop

        Return New MultiSolveResult With {
            .Solutions = solutions.OrderBy(Function(s) s.Quality.Total).ToList(),
            .StopReason = stopReason, .IterationsRun = iterations, .ElapsedS = sw.Elapsed.TotalSeconds,
            .StagnationTriggeredCount = stagnationTriggeredCount
        }
    End Function

    Public Function StatusName(status As CpSolverStatus) As String
        Return status.ToString()
    End Function

    ''' <summary>Phase 2.11: orchestrates the three Kursstufe CP-SAT stages
    ''' (Kursblockung.vb -> Schienenraster.vb -> Raumzuordnung.vb)
    ''' end-to-end. Each of stages B/C is solved via THIS SAME Solve()
    ''' function operating on a synthetic scenario - not one line of
    ''' BuildModel/BuildCoreModel/ApplyConstraints/AddBlockConstraint above
    ''' changes for this feature; only this new function and the three new
    ''' modules exist. Stops at the first stage that doesn't solve
    ''' Optimal/Feasible, so a caller can tell WHICH stage failed (see
    ''' KursstufeSolveResult) instead of only "it didn't work".
    ''' Precondition: Validation.ValidateKursstufeEntities(data) returned
    ''' no errors (same "validate before solving" discipline as
    ''' BuildCoreModel/Validation.ValidateEntities).</summary>
    Public Function SolveKursstufe(data As JsonObject,
                                    Optional timeLimitS As Double = 30.0,
                                    Optional seed As Integer = 42,
                                    Optional numWorkers As Integer = 1) As KursstufeSolveResult
        Dim kb = Kursblockung.SolveKursblockung(data, timeLimitS, seed, numWorkers)
        If kb.Status <> CpSolverStatus.Optimal AndAlso kb.Status <> CpSolverStatus.Feasible Then
            Return New KursstufeSolveResult With {.KursblockungStatus = kb.Status}
        End If

        Dim schienenScenario = Schienenraster.BuildSchienenrasterScenario(data, kb.Assignment)
        Dim schienenResult = Solve(schienenScenario, timeLimitS, seed, numWorkers)
        If schienenResult.Status <> CpSolverStatus.Optimal AndAlso schienenResult.Status <> CpSolverStatus.Feasible Then
            Return New KursstufeSolveResult With {.KursblockungStatus = kb.Status, .SchienenrasterStatus = schienenResult.Status}
        End If

        Dim raumScenario = Raumzuordnung.BuildRaumzuordnungScenario(data, kb.Assignment, schienenResult.Schedule)
        Dim raumResult = Solve(raumScenario, timeLimitS, seed, numWorkers)
        If raumResult.Status <> CpSolverStatus.Optimal AndAlso raumResult.Status <> CpSolverStatus.Feasible Then
            Return New KursstufeSolveResult With {
                .KursblockungStatus = kb.Status, .SchienenrasterStatus = schienenResult.Status, .RaumzuordnungStatus = raumResult.Status}
        End If

        Return New KursstufeSolveResult With {
            .KursblockungStatus = kb.Status, .SchienenrasterStatus = schienenResult.Status,
            .RaumzuordnungStatus = raumResult.Status, .Schedule = raumResult.Schedule
        }
    End Function

End Module

''' <summary>Phase 2.11: per-stage diagnostics for Solver.SolveKursstufe -
''' which of the three CP-SAT stages succeeded, and the final per-Kurs
''' schedule (each entry's .ClassName is the real Kurs id, not a Schiene
''' or class name - see Raumzuordnung.vb) once all three have. A stage's
''' Status property stays Nothing if an earlier stage already failed, so
''' a caller can tell exactly where the pipeline stopped.</summary>
Public NotInheritable Class KursstufeSolveResult
    Public Property KursblockungStatus As CpSolverStatus
    Public Property SchienenrasterStatus As CpSolverStatus?
    Public Property RaumzuordnungStatus As CpSolverStatus?
    Public Property Schedule As List(Of ScheduleEntry)
End Class

