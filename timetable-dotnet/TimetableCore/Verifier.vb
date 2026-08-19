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
