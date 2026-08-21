' Ported 1:1 from timetable/validation.py. Deterministic, LLM-free
' validation of the constraint JSON, run *before* it reaches the CP-SAT
' model builder (Solver.vb calls ValidateEntities first, exactly like the
' Python original's build_model()).
'
' Two categories, kept deliberately separate:
'
' - ValidateEntities returns HARD errors: a constraint references a
'   class/teacher/subject/room that does not exist in `entities`. Such a
'   constraint is not just wrong - the model builder silently drops it
'   (there is no session/variable to attach it to), which can make an
'   incomplete schedule solve as OPTIMAL. This is not hypothetical: an
'   LLM-generated `consecutive_required` entry once had `"class": "Chemie"`
'   (a subject name, not a class) and the Python solver happily produced a
'   schedule with that subject missing entirely. These errors must block
'   solving.
'
' - CoverageWarnings returns SOFT warnings: a general rule (no_overlap)
'   does not cover every class/teacher. This may be intentional (not every
'   teacher needs a standalone no_overlap rule if they only teach one
'   class), so it does NOT block solving.
Imports System.Text.Json.Nodes


Public Module Validation

    Private ReadOnly FieldEntityKey As New Dictionary(Of String, String) From {
        {"class", "classes"},
        {"classes", "classes"},
        {"teacher", "teachers"},
        {"teachers", "teachers"},
        {"subject", "subjects"},
        {"subjects", "subjects"},
        {"room", "rooms"},
        {"allowed_rooms", "rooms"},
        {"kurse", "kurse"}
    }

    Private ReadOnly ResourceEntityKey As New Dictionary(Of String, String) From {
        {"teacher", "teachers"},
        {"class", "classes"},
        {"room", "rooms"}
    }

    ' Phase 2.5: constraint types that may be marked "priority": "should"
    ' (Kann). Everything else (no_overlap, shared_resource_conflict,
    ' teacher_subject_assignment, and weekly_hours' own hours_per_week) is
    ' physically/structurally necessary and must always stay "must".
    Private ReadOnly KannCapableTypes As New HashSet(Of String) From {
        "teacher_availability", "forbidden_slot", "room_requirement", "consecutive_required", "weekly_hours",
        "required_slot", "occupied_slot"
    }

    ' occupied_slot only supports "class"/"teacher" scope (unlike
    ' forbidden_slot/no_overlap's ResourceEntityKey, which also allows
    ' "room") - Solver.vb's Case "occupied_slot" has no room-scope
    ' handling, so accepting "room" here would validate cleanly but then
    ' silently build zero constraints (occVars.Count = 0) in Solver.vb -
    ' exactly the "incomplete schedule solves as OPTIMAL" trap this
    ' module's header warns about. A dedicated, narrower dictionary keeps
    ' that trap from being reachable via a valid-looking scope value.
    Private ReadOnly OccupiedSlotScopeEntityKey As New Dictionary(Of String, String) From {
        {"teacher", "teachers"},
        {"class", "classes"}
    }

    ''' <summary>Appends the constraint's "reason" (if any) to an error/
    ''' warning message, so it can be traced back to the rule that produced
    ''' it. Returns the message unchanged when no reason is set.</summary>
    Private Function WithReason(message As String, c As JsonObject) As String
        Dim reason = JsonHelpers.GetReason(c)
        If String.IsNullOrEmpty(reason) Then Return message
        Return $"{message} (Regel-Herkunft: '{reason}')"
    End Function

    ''' <summary>R2-Helfer: prueft das day/period-Feld eines Slot-Constraints
    ''' gegen das entities.timeslots-Raster - ein Slot ausserhalb des
    ''' Rasters existiert als Lesson-Variable nicht und die Regel wuerde im
    ''' Solver wirkungslos fallengelassen. Leere Liste, wenn kein Raster
    ''' bekannt ist (kein timeslots-Objekt) oder die Felder passen.</summary>
    Private Function SlotRangeErrors(c As JsonObject, i As Integer, constraintType As String,
                                      knownDays As HashSet(Of String), periodsPerDay As Integer?) As List(Of String)
        Dim errors As New List(Of String)
        Dim day = JsonHelpers.GetString(c, "day")
        Dim period = JsonHelpers.GetInt(c, "period")
        If knownDays IsNot Nothing AndAlso day IsNot Nothing AndAlso Not knownDays.Contains(day) Then
            errors.Add(WithReason($"constraints[{i}] (type={constraintType}): day='{day}' ist kein Tag aus entities.timeslots.days", c))
        End If
        If periodsPerDay.HasValue AndAlso period.HasValue AndAlso (period.Value < 1 OrElse period.Value > periodsPerDay.Value) Then
            errors.Add(WithReason($"constraints[{i}] (type={constraintType}): period={period.Value} liegt ausserhalb von 1..{periodsPerDay.Value}", c))
        End If
        Return errors
    End Function

    ''' <summary>Cross-references every class/teacher/subject/room value
    ''' used in `constraints` against `entities`. Returns a list of
    ''' error strings (empty = all references are valid).</summary>
    Public Function ValidateEntities(data As JsonObject) As List(Of String)
        Dim ent = JsonHelpers.Entities(data)
        Dim known As New Dictionary(Of String, HashSet(Of String))
        For Each key In {"classes", "teachers", "subjects", "rooms"}
            known(key) = New HashSet(Of String)(JsonHelpers.AsStringList(ent, key))
        Next
        ' Phase 2.11: entities.kurse holds objects (id/subject/teacher/...),
        ' not plain strings, so it can't go through AsStringList like the
        ' four lists above - built from each Kurs's "id" field instead.
        known("kurse") = New HashSet(Of String)(JsonHelpers.GetKurse(ent).Select(Function(k) JsonHelpers.GetString(k, "id")))

        Dim errors As New List(Of String)
        Dim constraints = JsonHelpers.Constraints(data)

        ' Code-Review-Umsetzung (R1/R2): Vorab-Pass ueber alle
        ' teacher_subject_assignment-Constraints - liefert (a) exakte
        ' Duplikate als harten Fehler (Solver.SessionsFromAssignments
        ' ueberschrieb frueher stillschweigend die Lesson-Variablen des
        ' ersten Duplikats) und (b) die Treffermengen, gegen die
        ' occupied_slot/required_slot unten auf "referenziert ueberhaupt
        ' eine existierende Session" geprueft werden (vorher stille No-Ops
        ' im Solver, selbst mit priority=must - exakt die "incomplete
        ' schedule solves as OPTIMAL"-Falle aus dem Modulkopf).
        Dim assignmentTriples As New HashSet(Of (ClassName As String, Subject As String, Teacher As String))
        Dim classSubjectPairs As New HashSet(Of (ClassName As String, Subject As String))
        Dim classesWithSessions As New HashSet(Of String)
        Dim teachersWithSessions As New HashSet(Of String)
        For i = 0 To constraints.Count - 1
            Dim c = constraints(i)
            If JsonHelpers.GetString(c, "type") <> "teacher_subject_assignment" Then Continue For
            Dim triple = (JsonHelpers.GetString(c, "class"), JsonHelpers.GetString(c, "subject"), JsonHelpers.GetString(c, "teacher"))
            If Not assignmentTriples.Add(triple) Then
                errors.Add(WithReason(
                    $"constraints[{i}] (type=teacher_subject_assignment): doppelte Zuweisung " &
                    $"(class='{triple.Item1}', subject='{triple.Item2}', teacher='{triple.Item3}') - " &
                    "dasselbe Tripel existiert bereits weiter oben", c))
            End If
            classSubjectPairs.Add((triple.Item1, triple.Item2))
            classesWithSessions.Add(triple.Item1)
            teachersWithSessions.Add(triple.Item3)
        Next

        ' Tag-/Perioden-Raster fuer die Slot-Existenzpruefungen unten -
        ' nur gelesen, wenn entities.timeslots existiert (defensive Guard:
        ' ValidateEntities selbst brauchte das Raster bisher nicht).
        Dim knownDays As HashSet(Of String) = Nothing
        Dim periodsPerDay As Integer? = Nothing
        If ent.ContainsKey("timeslots") AndAlso ent("timeslots") IsNot Nothing Then
            Dim timeslots = JsonHelpers.Timeslots(ent)
            knownDays = New HashSet(Of String)(JsonHelpers.AsStringList(timeslots, "days"))
            periodsPerDay = JsonHelpers.GetInt(timeslots, "periods_per_day")
        End If

        For i = 0 To constraints.Count - 1
            Dim c = constraints(i)
            Dim constraintType = JsonHelpers.GetString(c, "type")

            For Each kvp In FieldEntityKey
                Dim field = kvp.Key
                Dim entityKey = kvp.Value
                If Not c.ContainsKey(field) OrElse c(field) Is Nothing Then Continue For
                For Each v In JsonHelpers.AsStringList(c(field))
                    If Not known(entityKey).Contains(v) Then
                        errors.Add(WithReason(
                            $"constraints[{i}] (type={constraintType}): Feld '{field}'='{v}' " &
                            $"ist keine bekannte Entity (erlaubt: {JsonHelpers.PyListRepr(known(entityKey).OrderBy(Function(s) s))})", c))
                    End If
                Next
            Next

            If constraintType = "no_overlap" Then
                Dim resource = JsonHelpers.GetString(c, "resource")
                If Not ResourceEntityKey.ContainsKey(resource) Then
                    errors.Add(WithReason($"constraints[{i}]: no_overlap.resource={JsonHelpers.PyRepr(resource)} ungueltig (erlaubt: teacher/class/room)", c))
                Else
                    Dim entityKey = ResourceEntityKey(resource)
                    Dim entityVal = JsonHelpers.GetString(c, "entity")
                    If Not known(entityKey).Contains(entityVal) Then
                        errors.Add(WithReason($"constraints[{i}]: no_overlap.entity='{entityVal}' nicht in {entityKey}", c))
                    End If
                End If
            End If

            If constraintType = "forbidden_slot" Then
                Dim scope = JsonHelpers.GetString(c, "scope")
                If Not ResourceEntityKey.ContainsKey(scope) Then
                    errors.Add(WithReason($"constraints[{i}]: forbidden_slot.scope={JsonHelpers.PyRepr(scope)} ungueltig (erlaubt: teacher/class/room)", c))
                Else
                    Dim entityKey = ResourceEntityKey(scope)
                    Dim entityVal = JsonHelpers.GetString(c, "entity")
                    If Not known(entityKey).Contains(entityVal) Then
                        errors.Add(WithReason($"constraints[{i}]: forbidden_slot.entity='{entityVal}' nicht in {entityKey}", c))
                    End If
                End If
            End If

            If constraintType = "occupied_slot" Then
                Dim scope = JsonHelpers.GetString(c, "scope")
                If Not OccupiedSlotScopeEntityKey.ContainsKey(scope) Then
                    errors.Add(WithReason($"constraints[{i}]: occupied_slot.scope={JsonHelpers.PyRepr(scope)} ungueltig (erlaubt: teacher/class)", c))
                Else
                    Dim entityKey = OccupiedSlotScopeEntityKey(scope)
                    Dim entityVal = JsonHelpers.GetString(c, "entity")
                    If Not known(entityKey).Contains(entityVal) Then
                        errors.Add(WithReason($"constraints[{i}]: occupied_slot.entity='{entityVal}' nicht in {entityKey}", c))
                    Else
                        ' R2: eine occupied_slot-Regel ohne eine einzige
                        ' Session der Entity waere im Solver ein stiller
                        ' No-Op (occVars.Count = 0), selbst als Muss.
                        Dim hasSessions = If(scope = "class", classesWithSessions.Contains(entityVal), teachersWithSessions.Contains(entityVal))
                        If Not hasSessions Then
                            errors.Add(WithReason($"constraints[{i}]: occupied_slot.entity='{entityVal}' hat keine einzige teacher_subject_assignment-Session - die Regel wuerde im Solver wirkungslos fallengelassen", c))
                        End If
                    End If
                End If
                errors.AddRange(SlotRangeErrors(c, i, "occupied_slot", knownDays, periodsPerDay))
            End If

            ' R2: required_slot referenziert eine konkrete (Klasse,Fach)-
            ' Session auf einem konkreten Slot - existiert die Session oder
            ' der Slot nicht, wuerde der Solver die Regel wirkungslos
            ' fallenlassen (lesson.ContainsKey-Guard), selbst als Muss.
            If constraintType = "required_slot" Then
                Dim reqClass = JsonHelpers.GetString(c, "class")
                Dim reqSubject = JsonHelpers.GetString(c, "subject")
                If reqClass IsNot Nothing AndAlso reqSubject IsNot Nothing AndAlso
                   known("classes").Contains(reqClass) AndAlso known("subjects").Contains(reqSubject) AndAlso
                   Not classSubjectPairs.Contains((reqClass, reqSubject)) Then
                    errors.Add(WithReason($"constraints[{i}]: required_slot referenziert (class='{reqClass}', subject='{reqSubject}') ohne zugehoerige teacher_subject_assignment - die Regel wuerde im Solver wirkungslos fallengelassen", c))
                End If
                errors.AddRange(SlotRangeErrors(c, i, "required_slot", knownDays, periodsPerDay))
            End If

            ' Phase 2.5: Muss/Kann priority validation.
            If c.ContainsKey("priority") AndAlso c("priority") IsNot Nothing Then
                Dim priority = JsonHelpers.GetString(c, "priority")
                If priority <> JsonHelpers.PriorityMust AndAlso priority <> JsonHelpers.PriorityShould Then
                    errors.Add(WithReason($"constraints[{i}] (type={constraintType}): priority='{priority}' ungueltig (erlaubt: must/should)", c))
                ElseIf priority = JsonHelpers.PriorityShould Then
                    If Not KannCapableTypes.Contains(constraintType) Then
                        errors.Add(WithReason($"constraints[{i}] (type={constraintType}): priority='should' ist fuer diesen Constraint-Typ nicht erlaubt (immer Muss)", c))
                    ElseIf constraintType = "weekly_hours" Then
                        Dim maxPerDay = JsonHelpers.GetInt(c, "max_per_day")
                        If Not maxPerDay.HasValue OrElse maxPerDay.Value = 0 Then
                            errors.Add(WithReason($"constraints[{i}] (type=weekly_hours): priority='should' ohne gesetztes max_per_day ergibt nichts, das gelockert werden koennte", c))
                        End If
                    End If
                End If
            End If
        Next

        Return errors
    End Function

    ''' <summary>Phase 2.11 (Kursstufe/Kurssystem): hard-error checks beyond
    ''' the generic cross-reference checks in ValidateEntities (which already
    ''' catches unknown "kurse" references via the new FieldEntityKey entry
    ''' above). Checks entities.kurse/entities.schienen internal consistency
    ''' and each "kurswahl" constraint's structural validity. Additive - has
    ''' no effect on any fixture that doesn't use entities.kurse/kurswahl.</summary>
    Public Function ValidateKursstufeEntities(data As JsonObject) As List(Of String)
        Dim errors As New List(Of String)
        errors.AddRange(ValidateEntities(data))

        Dim ent = JsonHelpers.Entities(data)
        Dim teachers = New HashSet(Of String)(JsonHelpers.AsStringList(ent, "teachers"))
        Dim subjects = New HashSet(Of String)(JsonHelpers.AsStringList(ent, "subjects"))
        Dim kurse = JsonHelpers.GetKurse(ent)
        Dim schienen = JsonHelpers.GetSchienen(ent).Select(Function(s) New With {
            .Kursart = JsonHelpers.GetString(s, "kursart"),
            .HoursPerWeek = JsonHelpers.GetInt(s, "hours_per_week")
        }).ToList()

        ' KursId -> kursart, used below to check each kurswahl's LK count.
        Dim kursKursart As New Dictionary(Of String, String)

        For i = 0 To kurse.Count - 1
            Dim k = kurse(i)
            Dim id = JsonHelpers.GetString(k, "id")
            Dim kursart = JsonHelpers.GetString(k, "kursart")
            Dim hours = JsonHelpers.GetInt(k, "hours_per_week")
            Dim teacher = JsonHelpers.GetString(k, "teacher")
            Dim subject = JsonHelpers.GetString(k, "subject")
            kursKursart(id) = kursart

            If kursart <> JsonHelpers.KursartLK AndAlso kursart <> JsonHelpers.KursartGK Then
                errors.Add($"entities.kurse[{i}] (id={JsonHelpers.PyRepr(id)}): kursart={JsonHelpers.PyRepr(kursart)} ungueltig (erlaubt: LK/GK)")
            End If
            If Not hours.HasValue OrElse hours.Value <= 0 Then
                errors.Add($"entities.kurse[{i}] (id={JsonHelpers.PyRepr(id)}): hours_per_week muss > 0 sein")
            End If
            If Not teachers.Contains(teacher) Then
                errors.Add($"entities.kurse[{i}] (id={JsonHelpers.PyRepr(id)}): teacher={JsonHelpers.PyRepr(teacher)} ist keine bekannte Entity")
            End If
            If Not subjects.Contains(subject) Then
                errors.Add($"entities.kurse[{i}] (id={JsonHelpers.PyRepr(id)}): subject={JsonHelpers.PyRepr(subject)} ist keine bekannte Entity")
            End If
            If Not schienen.Any(Function(s) s.Kursart = kursart AndAlso s.HoursPerWeek = hours) Then
                errors.Add($"entities.kurse[{i}] (id={JsonHelpers.PyRepr(id)}): keine Schiene mit kursart={JsonHelpers.PyRepr(kursart)} und hours_per_week={hours} vorhanden")
            End If
        Next

        Dim rawSchienen = JsonHelpers.GetSchienen(ent)
        For i = 0 To rawSchienen.Count - 1
            Dim capacity = JsonHelpers.GetInt(rawSchienen(i), "capacity")
            If capacity.HasValue AndAlso capacity.Value <= 0 Then
                errors.Add($"entities.schienen[{i}] (id={JsonHelpers.PyRepr(JsonHelpers.GetString(rawSchienen(i), "id"))}): capacity muss > 0 sein, falls gesetzt")
            End If
        Next

        Dim seenWahlprofilIds As New HashSet(Of String)
        Dim constraints = JsonHelpers.Constraints(data)
        For i = 0 To constraints.Count - 1
            Dim c = constraints(i)
            If JsonHelpers.GetString(c, "type") <> "kurswahl" Then Continue For

            Dim wahlprofilId = JsonHelpers.GetString(c, "wahlprofil_id")
            Dim studentCount = JsonHelpers.GetInt(c, "student_count")
            Dim kurseIds = JsonHelpers.AsStringList(c, "kurse")

            If String.IsNullOrEmpty(wahlprofilId) Then
                errors.Add(WithReason($"constraints[{i}] (type=kurswahl): wahlprofil_id fehlt", c))
            ElseIf Not seenWahlprofilIds.Add(wahlprofilId) Then
                errors.Add(WithReason($"constraints[{i}] (type=kurswahl): wahlprofil_id='{wahlprofilId}' ist nicht eindeutig (Duplikat)", c))
            End If

            If Not studentCount.HasValue OrElse studentCount.Value <= 0 Then
                errors.Add(WithReason($"constraints[{i}] (type=kurswahl, wahlprofil_id={JsonHelpers.PyRepr(wahlprofilId)}): student_count muss > 0 sein", c))
            End If

            Dim lkCount = kurseIds.Where(Function(kid) kursKursart.ContainsKey(kid) AndAlso kursKursart(kid) = JsonHelpers.KursartLK).Count()
            If lkCount <> 3 Then
                errors.Add(WithReason($"constraints[{i}] (type=kurswahl, wahlprofil_id={JsonHelpers.PyRepr(wahlprofilId)}): genau 3 Leistungskurse (kursart=LK) erforderlich, gefunden: {lkCount}", c))
            End If
        Next

        Return errors
    End Function

    ''' <summary>Advisory (non-blocking) checks: does a general
    ''' no_overlap rule cover every class/teacher?</summary>
    Public Function CoverageWarnings(data As JsonObject) As List(Of String)
        Dim ent = JsonHelpers.Entities(data)
        Dim classes = New HashSet(Of String)(JsonHelpers.AsStringList(ent, "classes"))
        Dim teachers = New HashSet(Of String)(JsonHelpers.AsStringList(ent, "teachers"))

        Dim noOverlapClasses As New HashSet(Of String)
        Dim noOverlapTeachers As New HashSet(Of String)

        For Each c In JsonHelpers.Constraints(data)
            If JsonHelpers.GetString(c, "type") <> "no_overlap" Then Continue For
            Dim resource = JsonHelpers.GetString(c, "resource")
            Dim entityVal = JsonHelpers.GetString(c, "entity")
            If resource = "class" Then
                noOverlapClasses.Add(entityVal)
            ElseIf resource = "teacher" Then
                noOverlapTeachers.Add(entityVal)
            End If
        Next

        Dim warnings As New List(Of String)
        Dim missingClasses = classes.Except(noOverlapClasses).OrderBy(Function(s) s).ToList()
        Dim missingTeachers = teachers.Except(noOverlapTeachers).OrderBy(Function(s) s).ToList()
        If missingClasses.Any() Then
            warnings.Add($"no_overlap fehlt fuer Klassen {JsonHelpers.PyListRepr(missingClasses)} (evtl. gewollt, bitte pruefen)")
        End If
        If missingTeachers.Any() Then
            warnings.Add($"no_overlap fehlt fuer Lehrer {JsonHelpers.PyListRepr(missingTeachers)} (evtl. gewollt, bitte pruefen)")
        End If
        Return warnings
    End Function

End Module

