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

        ' Phase 2.20c (fix): pre-derived ONCE, used below by the "no_overlap"
        ' case to avoid flagging legitimately-simultaneous parallel_group
        ' sessions as a false "doppelt belegt" collision - independently
        ' duplicated (not shared code) from Solver.vb's ApplyConstraints
        ' term-deduplication for the exact same reason (see module header).
        ' Without this, EVERY parallel_group-based schedule would trip the
        ' pre-existing "class"/"teacher" no_overlap check, since that check
        ' has no notion of intentional simultaneity.
        Dim parallelGroupOf As New Dictionary(Of (ClassName As String, Subject As String, Teacher As String), Integer)
        For gi = 0 To constraints.Count - 1
            If JsonHelpers.GetString(constraints(gi), "type") <> "parallel_group" Then Continue For
            Dim gClasses = JsonHelpers.AsStringList(constraints(gi), "classes")
            Dim gSubjects = JsonHelpers.AsStringList(constraints(gi), "subjects")
            Dim gTeachers = JsonHelpers.AsStringList(constraints(gi), "teachers")
            For mi = 0 To gClasses.Count - 1
                parallelGroupOf((gClasses(mi), gSubjects(mi), gTeachers(mi))) = gi
            Next
        Next

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
                    ' Phase 2.20c (fix): per (Day,Period) slot, sessions
                    ' belonging to the SAME parallel_group must contribute
                    ' only ONE counted entry (resource "room" is exempt -
                    ' each member keeps its own room). Mirrors Solver.vb's
                    ' ApplyConstraints term-deduplication.
                    Dim countedGroupsPerSlot As New Dictionary(Of (Day As String, Period As Integer), HashSet(Of Integer))
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

                        If resource <> "room" Then
                            Dim gi As Integer
                            If parallelGroupOf.TryGetValue((l.ClassName, l.Subject, l.Teacher), gi) Then
                                If Not countedGroupsPerSlot.ContainsKey(slot) Then countedGroupsPerSlot(slot) = New HashSet(Of Integer)
                                If Not countedGroupsPerSlot(slot).Add(gi) Then Continue For
                            End If
                        End If

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

                Case "required_slot"
                    ' Phase 2.23: unabhaengig re-derivierte Gegenpruefung
                    ' zum Solver.vb-Case "required_slot" - teilt keinen Code
                    ' mit dessen Pre-Pass, nutzt nur den bereits bestehenden
                    ' Find-Helper (gleiches Prinzip wie ueberall sonst in
                    ' diesem Modul).
                    Dim requiredClassName = JsonHelpers.GetString(c, "class")
                    Dim requiredSubject = JsonHelpers.GetString(c, "subject")
                    Dim requiredDay = JsonHelpers.GetString(c, "day")
                    Dim requiredPeriod = JsonHelpers.GetInt(c, "period").Value
                    If Not Find(schedule, cls:=requiredClassName, subject:=requiredSubject, day:=requiredDay, period:=requiredPeriod).Any() Then
                        violations.Add((i, t, WithReason(
                            $"{requiredClassName}/{requiredSubject} findet nicht im geforderten Slot {requiredDay}/{requiredPeriod} statt", c)))
                    End If

                Case "occupied_slot"
                    ' Unabhaengig re-derivierte Gegenpruefung zum Solver.vb-
                    ' Case "occupied_slot" (teilt keinen Code) - genau die
                    ' Negation von forbidden_slot's Pruefung oben: statt
                    ' "hat der/die Entity dort Unterricht?" (Verstoss wenn
                    ' JA) hier "hat der/die Entity dort KEINEN Unterricht?"
                    ' (Verstoss wenn KEINE passende Zeile existiert).
                    Dim occScope = JsonHelpers.GetString(c, "scope")
                    Dim occEntity = JsonHelpers.GetString(c, "entity")
                    Dim occDay = JsonHelpers.GetString(c, "day")
                    Dim occPeriod = JsonHelpers.GetInt(c, "period").Value
                    Dim occFound As Boolean
                    Select Case occScope
                        Case "class" : occFound = Find(schedule, cls:=occEntity, day:=occDay, period:=occPeriod).Any()
                        Case "teacher" : occFound = Find(schedule, teacher:=occEntity, day:=occDay, period:=occPeriod).Any()
                        Case Else : occFound = True ' unbekannter scope -> nichts zu pruefen, kein falscher Verstoss
                    End Select
                    If Not occFound Then
                        violations.Add((i, t, WithReason($"{occEntity} ({occScope}) hat KEINEN Unterricht im geforderten Slot {occDay}/{occPeriod}", c)))
                    End If

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

                Case "parallel_group"
                    ' Phase 2.20c: independently re-derived synchronization
                    ' check for the Solver.vb "parallel_group" primitive -
                    ' shares NO code with the Solver.vb pre-pass (same
                    ' module-wide principle, see header). For every member
                    ' triple and every (day,period), all members must be
                    ' EITHER all present OR all absent - anything else means
                    ' the group's sessions drifted out of sync.
                    Dim classesInGroup = JsonHelpers.AsStringList(c, "classes")
                    Dim subjectsInGroup = JsonHelpers.AsStringList(c, "subjects")
                    Dim teachersInGroup = JsonHelpers.AsStringList(c, "teachers")
                    For Each d In allDays
                        For Each p In allPeriods
                            Dim presentCount = 0
                            For mi = 0 To classesInGroup.Count - 1
                                If Find(schedule, cls:=classesInGroup(mi), teacher:=teachersInGroup(mi),
                                        day:=d, period:=p, subject:=subjectsInGroup(mi)).Any() Then
                                    presentCount += 1
                                End If
                            Next
                            If presentCount > 0 AndAlso presentCount < classesInGroup.Count Then
                                violations.Add((i, t, WithReason(
                                    $"parallel_group #{i} am {d}/{p}: nur {presentCount} von {classesInGroup.Count} Mitgliedern aktiv - Gruppe ist nicht synchron", c)))
                            End If
                        Next
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

    ''' <summary>Phase 2.11: independent re-check of a Kursblockung result
    ''' (Kursblockung.SolveKursblockung's Kurs->Schiene assignment) against
    ''' the raw entities/kurswahl JSON - deliberately does NOT call into
    ''' Kursblockung.vb's CP-SAT code, same "no shared code with the
    ''' solver" principle as the rest of this module (see header). Checks:
    ''' every Kurs is assigned to a Schiene compatible with it (kursart AND
    ''' hours_per_week), no Wahlprofil has two of its own Kurse in the same
    ''' Schiene, and no teacher has two of their Kurse in the same Schiene -
    ''' the latter two should already be structurally impossible by
    ''' Kursblockung's own constraints, so a hit here is a canary revealing
    ''' a bug in that CP-SAT model, not an expected real-world finding.</summary>
    Public Function VerifyKursblockung(data As JsonObject, assignment As Dictionary(Of String, String)) As List(Of String)
        Dim violations As New List(Of String)
        Dim ent = JsonHelpers.Entities(data)
        Dim schienenById = JsonHelpers.GetSchienen(ent).ToDictionary(Function(s) JsonHelpers.GetString(s, "id"))
        Dim kursTeacher As New Dictionary(Of String, String)

        For Each k In JsonHelpers.GetKurse(ent)
            Dim id = JsonHelpers.GetString(k, "id")
            Dim kursart = JsonHelpers.GetString(k, "kursart")
            Dim hours = JsonHelpers.GetInt(k, "hours_per_week").GetValueOrDefault()
            kursTeacher(id) = JsonHelpers.GetString(k, "teacher")

            If Not assignment.ContainsKey(id) Then
                violations.Add($"Kurs '{id}' hat keine Schienen-Zuordnung")
                Continue For
            End If

            Dim schieneId = assignment(id)
            If Not schienenById.ContainsKey(schieneId) Then
                violations.Add($"Kurs '{id}' ist Schiene '{schieneId}' zugeordnet, die in entities.schienen nicht existiert")
                Continue For
            End If

            Dim schiene = schienenById(schieneId)
            Dim schieneKursart = JsonHelpers.GetString(schiene, "kursart")
            Dim schieneHours = JsonHelpers.GetInt(schiene, "hours_per_week").GetValueOrDefault()
            If schieneKursart <> kursart OrElse schieneHours <> hours Then
                violations.Add(
                    $"Kurs '{id}' (kursart={kursart}, hours_per_week={hours}) ist der inkompatiblen " &
                    $"Schiene '{schieneId}' (kursart={schieneKursart}, hours_per_week={schieneHours}) zugeordnet")
            End If
        Next

        For Each wahlprofil In JsonHelpers.Constraints(data).Where(Function(con) JsonHelpers.GetString(con, "type") = "kurswahl")
            Dim wahlprofilId = JsonHelpers.GetString(wahlprofil, "wahlprofil_id")
            Dim ownSchienen = JsonHelpers.AsStringList(wahlprofil, "kurse").
                Where(Function(kid) assignment.ContainsKey(kid)).
                GroupBy(Function(kid) assignment(kid))
            For Each g In ownSchienen
                If g.Count() > 1 Then
                    violations.Add($"Wahlprofil '{wahlprofilId}': Kurse {JsonHelpers.PyListRepr(g)} liegen gemeinsam in Schiene '{g.Key}'")
                End If
            Next
        Next

        For Each teacherGroup In kursTeacher.GroupBy(Function(kvp) kvp.Value, Function(kvp) kvp.Key)
            Dim bySchiene = teacherGroup.Where(Function(kid) assignment.ContainsKey(kid)).GroupBy(Function(kid) assignment(kid))
            For Each g In bySchiene
                If g.Count() > 1 Then
                    violations.Add($"Lehrkraft '{teacherGroup.Key}': Kurse {JsonHelpers.PyListRepr(g)} liegen gemeinsam in Schiene '{g.Key}'")
                End If
            Next
        Next

        Return violations
    End Function

    ''' <summary>Phase 2.15e: unabhaengige Re-Pruefung eines geloesten
    ''' Lehrereinsatzplanung.LehrereinsatzResult direkt aus den rohen
    ''' Stammdaten - ruft bewusst NICHT in Lehrereinsatzplanung.vb's
    ''' CP-SAT-Code hinein, gleiches "kein geteilter Code mit dem
    ''' Solver"-Prinzip wie der Rest dieses Moduls (siehe Kopfkommentar)
    ''' und wie VerifyKursblockung oben. Prueft: jede (Klasse,Pflichtfach)-
    ''' Kombination hat genau eine Zuweisung, jede zugewiesene Lehrkraft
    ''' ist laut fach_lehrer_zuordnungen qualifiziert, und jede gemeldete
    ''' Klassenlehrer-Zuweisung ist sowohl klassenlehrerfaehig als auch
    ''' tatsaechlich einer der Zuweisungen dieser Klasse.</summary>
    Public Function VerifyLehrereinsatz(bestand As Stammdatenbestand, result As LehrereinsatzResult) As List(Of String)
        Dim violations As New List(Of String)
        If result.Zuweisungen Is Nothing Then Return violations

        Dim lehrerByName = bestand.Lehrkraefte.ToDictionary(Function(l) l.Name)
        Dim klasseByName = bestand.Klassen.ToDictionary(Function(k) k.Name)
        Dim fachByName = bestand.Faecher.ToDictionary(Function(f) f.Name)

        For Each klasse In bestand.Klassen
            For Each fach In Stammdaten.FaecherOfKlassenstufe(bestand, klasse.Klassenstufe)
                Dim treffer = result.Zuweisungen.Where(Function(z) z.Klasse = klasse.Name AndAlso z.Fach = fach.Name).ToList()
                If treffer.Count <> 1 Then
                    violations.Add($"{klasse.Name}/{fach.Name}: {treffer.Count} Zuweisungen gefunden, erwartet genau 1")
                End If
            Next
        Next

        For Each z In result.Zuweisungen
            Dim qualifiziert = bestand.FachLehrerZuordnungen.Any(Function(fz) fz.LehrerName = z.Lehrer AndAlso fz.FachName = z.Fach)
            If Not qualifiziert Then
                violations.Add($"{z.Lehrer} unterrichtet {z.Klasse}/{z.Fach}, ist dafuer aber laut fach_lehrer_zuordnungen nicht qualifiziert")
            End If
        Next

        ' Phase 2.26/2.27: Kanarienvogel-Pruefung fuer die harte
        ' FesteZuordnung-Pinnung - unabhaengig aus bestand.FesteZuordnungen +
        ' result.Zuweisungen re-derivert (kein geteilter Code mit dem
        ' CP-SAT-Constraint). Sollte NIE feuern, wenn der Constraint-Block in
        ' Lehrereinsatzplanung.vb korrekt verdrahtet ist - ein Treffer hier
        ' ist ein Beweis fuer einen SOLVER-Bug, nicht fuer ein Stammdaten-
        ' Problem (das faengt StammdatenValidation bereits VOR dem Solve ab).
        ' Phase 2.27: fz.KlasseName kann seit der Gruppen-Erweiterung auch
        ' einen Gruppennamen tragen - result.Zuweisungen ist dabei laut
        ' bestehender Dokumentation IMMER Gruppen-EXPANDIERT (eine Zeile pro
        ' real gespannter Klasse), daher wird hier unabhaengig ueber
        ' Stammdaten.KlassenOfGruppe re-derivert statt sich auf den Solver-
        ' internen AssignKey zu verlassen.
        Dim gruppeByNameCanary = bestand.Gruppen.ToDictionary(Function(g) g.Name)
        For Each fz In bestand.FesteZuordnungen
            If gruppeByNameCanary.ContainsKey(fz.KlasseName) Then
                Dim gruppe = gruppeByNameCanary(fz.KlasseName)
                For Each realeKlasse In Stammdaten.KlassenOfGruppe(bestand, gruppe)
                    Dim tatsaechlicherLehrer = result.Zuweisungen.
                        Where(Function(z) z.Klasse = realeKlasse AndAlso z.Fach = fz.FachName).
                        Select(Function(z) z.Lehrer).FirstOrDefault()
                    If tatsaechlicherLehrer Is Nothing Then
                        violations.Add($"feste_zuordnung {fz.LehrerName}/{fz.KlasseName}/{fz.FachName}: keine Zuweisung fuer {realeKlasse}/{fz.FachName} im Ergebnis gefunden")
                    ElseIf tatsaechlicherLehrer <> fz.LehrerName Then
                        violations.Add($"feste_zuordnung {fz.LehrerName}/{fz.KlasseName}/{fz.FachName}: tatsaechlich zugewiesen ist '{tatsaechlicherLehrer}' statt der fest zugeordneten Lehrkraft (klasse {realeKlasse})")
                    End If
                Next
            Else
                Dim tatsaechlicherLehrer = result.Zuweisungen.
                    Where(Function(z) z.Klasse = fz.KlasseName AndAlso z.Fach = fz.FachName).
                    Select(Function(z) z.Lehrer).FirstOrDefault()
                If tatsaechlicherLehrer Is Nothing Then
                    violations.Add($"feste_zuordnung {fz.LehrerName}/{fz.KlasseName}/{fz.FachName}: keine Zuweisung fuer {fz.KlasseName}/{fz.FachName} im Ergebnis gefunden")
                ElseIf tatsaechlicherLehrer <> fz.LehrerName Then
                    violations.Add($"feste_zuordnung {fz.LehrerName}/{fz.KlasseName}/{fz.FachName}: tatsaechlich zugewiesen ist '{tatsaechlicherLehrer}' statt der fest zugeordneten Lehrkraft")
                End If
            End If
        Next

        ' Phase 2.17: Kanarienvogel-Pruefung fuer den harten Teilzeit-Tage-
        ' Kohaerenz-Vorfilter in Lehrereinsatzplanung.SolveLehrereinsatz -
        ' unabhaengig aus den rohen Stammdaten re-derivert (kein geteilter
        ' Code mit dem CP-SAT-Vorfilter, siehe Modul-Kopfkommentar). Sollte
        ' bei korrektem Vorfilter NIE feuern; ein Treffer hier waere ein
        ' Beweis fuer einen Bug im Vorfilter selbst, nicht in den
        ' Stammdaten.
        For Each z In result.Zuweisungen
            If Not lehrerByName.ContainsKey(z.Lehrer) OrElse Not klasseByName.ContainsKey(z.Klasse) OrElse Not fachByName.ContainsKey(z.Fach) Then Continue For
            Dim fk = Stammdaten.WochenstundenFuer(fachByName(z.Fach), klasseByName(z.Klasse).Klassenstufe)
            If fk IsNot Nothing AndAlso Not Stammdaten.IstTeilzeitKohaerent(lehrerByName(z.Lehrer), bestand, fk) Then
                violations.Add($"{z.Lehrer} unterrichtet {z.Klasse}/{z.Fach} ({fk.WochenstundenSoll}h/Woche), ist aber laut VerfuegbareTage teilzeit-tage-inkohaerent")
            End If
        Next

        ' Phase 2.20c: Gruppen-bewusste Konsistenzpruefung - eine
        ' Parallelgruppen-gefuehrte (Gruppe,Fach)-Kombination MUSS ueber
        ' alle von der Gruppe umspannten echten Klassen (Stammdaten.
        ' KlassenOfGruppe) hinweg vom SELBEN Lehrer unterrichtet werden,
        ' sonst waere die vom "parallel_group"-Solver.vb-Constraint
        ' erzwungene Slot-Synchronisation real unmoeglich (ein Lehrer
        ' koennte nicht gleichzeitig in zwei Klassen mit unterschiedlichen
        ' Kollegen synchron sein). Unabhaengig aus den rohen Stammdaten +
        ' dem Zuweisungsergebnis re-derivert, kein geteilter Code mit
        ' Lehrereinsatzplanung.vb.
        For Each gruppe In bestand.Gruppen.Where(Function(g) g.FachName IsNot Nothing)
            Dim klassenDerGruppe = Stammdaten.KlassenOfGruppe(bestand, gruppe)
            If klassenDerGruppe.Count = 0 Then Continue For
            Dim lehrerJeKlasse = klassenDerGruppe.
                Select(Function(kn) (Klasse:=kn, Lehrer:=result.Zuweisungen.
                    Where(Function(z) z.Klasse = kn AndAlso z.Fach = gruppe.FachName).
                    Select(Function(z) z.Lehrer).FirstOrDefault())).
                ToList()
            If lehrerJeKlasse.Any(Function(x) x.Lehrer Is Nothing) Then
                violations.Add($"Gruppe '{gruppe.Name}' ({gruppe.FachName}): mindestens eine Klasse aus {String.Join(",", klassenDerGruppe)} hat keine Zuweisung fuer {gruppe.FachName}")
            End If
            Dim distinctLehrer = lehrerJeKlasse.Select(Function(x) x.Lehrer).Where(Function(l) l IsNot Nothing).Distinct().ToList()
            If distinctLehrer.Count > 1 Then
                violations.Add($"Gruppe '{gruppe.Name}' ({gruppe.FachName}): unterschiedliche Lehrkraefte je Klasse ({String.Join(", ", lehrerJeKlasse.Select(Function(x) $"{x.Klasse}={x.Lehrer}"))}) - Parallelgruppen-Synchronisation erfordert denselben Lehrer in allen umspannten Klassen")
            End If
        Next

        If result.Klassenlehrer IsNot Nothing Then
            For Each kvp In result.Klassenlehrer
                Dim klasseName = kvp.Key
                Dim lehrerName = kvp.Value
                If Not lehrerByName.ContainsKey(lehrerName) Then
                    violations.Add($"Klasse {klasseName}: Klassenlehrer '{lehrerName}' ist keine bekannte Lehrkraft")
                    Continue For
                End If
                If Not lehrerByName(lehrerName).KlassenlehrerFaehig Then
                    violations.Add($"Klasse {klasseName}: Klassenlehrer '{lehrerName}' ist laut Stammdaten nicht klassenlehrerfaehig")
                End If
                If Not result.Zuweisungen.Any(Function(z) z.Klasse = klasseName AndAlso z.Lehrer = lehrerName) Then
                    violations.Add($"Klasse {klasseName}: Klassenlehrer '{lehrerName}' unterrichtet dort laut Zuweisungen kein Fach")
                End If
            Next
        End If

        Return violations
    End Function

End Module
