' Phase 2.11c: Stage B - Schiene-to-day/period assignment. Reuses
' Solver.Solve()/BuildModel() COMPLETELY UNCHANGED: constructs a synthetic
' JSON scenario with one pseudo-class PER CONFLICT GROUP of Schienen
' (GroupSchienenByConflict below), each pseudo-class's "subjects" being
' the Schiene IDs in that group - `no_overlap(class=<group>)` then keeps
' every Schiene WITHIN a group at a mutually exclusive day/period slot.
' Schienen in DIFFERENT groups have no constraint relating them at all,
' so the solver is free to schedule them at literally the same day/period -
' i.e. run them in parallel.
'
' Phase 2.11h correction: an earlier version of this module put EVERY
' Schiene into a single pseudo-class ("Kursstufe"), forcing ALL Schienen
' to be mutually exclusive across the whole week. That is safe (no
' Wahlprofil can ever collide) but does not scale: it demands as many
' weekly slots as the SUM of every Schiene's hours_per_week, whereas a
' real school's whole point of running MULTIPLE Schienen is that most of
' them run in PARALLEL (in different rooms, at the same clock time) -
' that only breaks for a Wahlprofil that has courses in BOTH of two
' specific Schienen. GroupSchienenByConflict computes exactly that:
' Schienen are connected (need mutual exclusion) only if some Wahlprofil
' actually has courses in both; connected components can be scheduled
' fully independently. Real schools' Kursblockung achieves the same
' effect for the same reason - this is not a fixture-specific workaround.
'
' See the phase-2.11 plan section for why the EVEN EARLIER "Schiene =
' virtual class carrying its courses" hypothesis was refuted
' (no_overlap(class=X) in Solver.vb enforces AT MOST ONE lesson of X per
' slot - the opposite of what a Schiene, which synchronizes several
' courses onto the SAME slot, needs) and "Schienen = subjects of a
' pseudo-class" is the corrected form; grouping by conflict is this
' module's own refinement on top of that, not a change to the
' pseudo-class idea itself. Not one line of Solver.vb changes for this
' stage - only this module's synthetic-scenario construction.
'
' The pseudo-classes ("Kursstufe-1", "Kursstufe-2", ...) and the pseudo-
' teachers ("_schiene_{SchieneId}", one PER Schiene since Phase 2.13 - see
' BuildSchienenrasterScenario's own doc comment for why) are purely
' internal to this stage and must never leak into a caller-visible
' schedule/constraint list - DeriveKursSchedule below replaces them with
' the real Kurs/Lehrkraft identities before returning anything.
Imports System.Text.Json.Nodes

Public Module Schienenraster

    ''' <summary>Groups Schienen into connected components of the "shares
    ''' a Wahlprofil" graph: two Schienen are connected if some Wahlprofil
    ''' has courses in both (via `kursblockungAssignment`). Returns
    ''' SchieneId -> pseudo-class name ("Kursstufe-1", "Kursstufe-2", ...).
    ''' A Schiene nobody's Wahlprofil ever reaches (e.g. an as-yet-unused
    ''' Schiene) still gets its own singleton group.</summary>
    Private Function GroupSchienenByConflict(ent As JsonObject, constraints As List(Of JsonObject),
                                              kursblockungAssignment As Dictionary(Of String, String)) As Dictionary(Of String, String)
        Dim schieneIds = JsonHelpers.GetSchienen(ent).Select(Function(s) JsonHelpers.GetString(s, "id")).ToList()
        Dim adjacency As New Dictionary(Of String, HashSet(Of String))
        For Each id In schieneIds
            adjacency(id) = New HashSet(Of String)
        Next

        For Each wahlprofil In constraints.Where(Function(c) JsonHelpers.GetString(c, "type") = "kurswahl")
            Dim ownSchienen = JsonHelpers.AsStringList(wahlprofil, "kurse").
                Where(Function(kid) kursblockungAssignment.ContainsKey(kid)).
                Select(Function(kid) kursblockungAssignment(kid)).
                Distinct().ToList()
            For Each a In ownSchienen
                For Each b In ownSchienen
                    If a <> b Then adjacency(a).Add(b)
                Next
            Next
        Next

        Dim groupOf As New Dictionary(Of String, String)
        Dim groupIndex = 0
        For Each start In schieneIds
            If groupOf.ContainsKey(start) Then Continue For
            groupIndex += 1
            Dim groupName = $"Kursstufe-{groupIndex}"
            Dim queue As New Queue(Of String)
            queue.Enqueue(start)
            groupOf(start) = groupName
            While queue.Count > 0
                Dim current = queue.Dequeue()
                For Each neighbor In adjacency(current)
                    If Not groupOf.ContainsKey(neighbor) Then
                        groupOf(neighbor) = groupName
                        queue.Enqueue(neighbor)
                    End If
                Next
            End While
        Next
        Return groupOf
    End Function

    ''' <summary>Phase 2.13: for a given Schiene id, the real teachers of
    ''' every Kurs assigned to it (inverts `kursblockungAssignment`,
    ''' KursId-&gt;SchieneId, then looks up each matching Kurs's own
    ''' `teacher` field in `entities.kurse`). Needed to derive which
    ''' external [Sek-I] busy-slots are actually relevant to THIS Schiene,
    ''' not the whole Kursstufe.</summary>
    Private Function TeachersOfSchiene(ent As JsonObject, schieneId As String, kursblockungAssignment As Dictionary(Of String, String)) As List(Of String)
        Dim kursIds = kursblockungAssignment.Where(Function(kvp) kvp.Value = schieneId).Select(Function(kvp) kvp.Key).ToHashSet()
        Return JsonHelpers.GetKurse(ent).
            Where(Function(k) kursIds.Contains(JsonHelpers.GetString(k, "id"))).
            Select(Function(k) JsonHelpers.GetString(k, "teacher")).Distinct().ToList()
    End Function

    ''' <summary>Builds the synthetic Solver.Solve() input for stage B.
    ''' Every Schiene becomes one "subject" taught by its OWN distinct
    ''' pseudo-teacher "_schiene_{SchieneId}" (Phase 2.13: was a single
    ''' shared "_schiene" for every Schiene before - the rename itself
    ''' changes nothing about no_overlap(class:=groupName) semantics,
    ''' which is indexed by .ClassName not .Teacher, but a DISTINCT
    ''' identity per Schiene is required for `externalTeacherBusySlots`
    ''' below to target one specific Schiene instead of blocking every
    ''' Schiene in every conflict group at once) to its conflict group's
    ''' pseudo-class (see GroupSchienenByConflict and the module header),
    ''' with its weekly_hours set from `hours_per_week`. If the Schiene
    ''' JSON carries an optional "block_length" (e.g. for LK-
    ''' Doppelstunden - only meaningful when hours_per_week is an exact
    ''' multiple of it, same precondition as every other block_length use
    ''' in this project, e.g. GymnasiumSekIFixture's Physik/Chemie double
    ''' lessons), a matching consecutive_required + max_per_day pair is
    ''' added too. `kursblockungAssignment` is stage A's result - used
    ''' here to compute the conflict grouping AND (if
    ''' `externalTeacherBusySlots` is given) to look up which real
    ''' teachers a Schiene's Kurse have; placing any Kurs is still
    ''' DeriveKursSchedule's job, after this stage solves.
    '''
    ''' `externalTeacherBusySlots` (Nothing by default, reproducing
    ''' today's exact behavior for every existing caller): a map of real
    ''' teacher name -&gt; the (day,period) slots that teacher is already
    ''' busy elsewhere (e.g. in an already-solved Sek-I schedule). For
    ''' each Schiene, the union of busy slots across whichever of its
    ''' Kurse's teachers appear in this map becomes a `teacher_availability`
    ''' constraint on that Schiene's own placeholder identity - so stage B
    ''' avoids scheduling a Schiene at a time one of its real teachers is
    ''' already committed elsewhere, instead of only discovering the
    ''' conflict too late in stage C.</summary>
    Public Function BuildSchienenrasterScenario(data As JsonObject, kursblockungAssignment As Dictionary(Of String, String),
                                                 Optional externalTeacherBusySlots As Dictionary(Of String, HashSet(Of (Day As String, Period As Integer))) = Nothing) As JsonObject
        Dim ent = JsonHelpers.Entities(data)
        Dim timeslots = JsonHelpers.Timeslots(ent)
        Dim days = JsonHelpers.AsStringList(timeslots, "days")
        Dim periodsPerDay = JsonHelpers.GetInt(timeslots, "periods_per_day").Value
        Dim schienen = JsonHelpers.GetSchienen(ent)

        Dim groupOf = GroupSchienenByConflict(ent, JsonHelpers.Constraints(data), kursblockungAssignment)
        Dim groupNames = groupOf.Values.Distinct().OrderBy(Function(g) g).ToList()

        Dim constraints As New List(Of JsonObject)
        Dim placeholderTeachers As New List(Of String)
        For Each s In schienen
            Dim id = JsonHelpers.GetString(s, "id")
            Dim groupName = groupOf(id)
            Dim hours = JsonHelpers.GetInt(s, "hours_per_week").Value
            Dim blockLength = JsonHelpers.GetInt(s, "block_length")
            Dim placeholderTeacher = $"_schiene_{id}"
            placeholderTeachers.Add(placeholderTeacher)

            constraints.Add(New JsonObject From {
                {"type", "teacher_subject_assignment"}, {"class", groupName}, {"subject", id}, {"teacher", placeholderTeacher}
            })

            Dim wh As New JsonObject From {
                {"type", "weekly_hours"}, {"class", groupName}, {"subject", id}, {"hours_per_week", hours}
            }
            If blockLength.HasValue Then wh("max_per_day") = blockLength.Value
            constraints.Add(wh)

            If blockLength.HasValue Then
                constraints.Add(New JsonObject From {
                    {"type", "consecutive_required"}, {"class", groupName}, {"subject", id}, {"block_length", blockLength.Value}
                })
            End If

            If externalTeacherBusySlots IsNot Nothing Then
                Dim busy As New HashSet(Of (Day As String, Period As Integer))
                For Each realTeacher In TeachersOfSchiene(ent, id, kursblockungAssignment)
                    If externalTeacherBusySlots.ContainsKey(realTeacher) Then
                        busy.UnionWith(externalTeacherBusySlots(realTeacher))
                    End If
                Next
                If busy.Count > 0 Then
                    constraints.Add(New JsonObject From {
                        {"type", "teacher_availability"}, {"teacher", placeholderTeacher},
                        {"unavailable_periods", New JsonArray(busy.Select(Function(slot) CType(
                            New JsonObject From {{"day", slot.Day}, {"period", slot.Period}}, JsonNode)).ToArray())}
                    })
                End If
            End If
        Next
        For Each g In groupNames
            constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", g}})
        Next

        Dim schienenEnt As New JsonObject From {
            {"classes", New JsonArray(groupNames.Select(Function(g) CType(g, JsonNode)).ToArray())},
            {"teachers", New JsonArray(placeholderTeachers.Select(Function(t) CType(t, JsonNode)).ToArray())},
            {"subjects", New JsonArray(schienen.Select(Function(s) CType(JsonHelpers.GetString(s, "id"), JsonNode)).ToArray())},
            {"rooms", New JsonArray()},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray(days.Select(Function(d) CType(d, JsonNode)).ToArray())},
                {"periods_per_day", periodsPerDay}
            }}
        }

        Return New JsonObject From {
            {"entities", schienenEnt},
            {"constraints", New JsonArray(constraints.Select(Function(c) CType(c, JsonNode)).ToArray())}
        }
    End Function

    ''' <summary>The (day,period) slots the Kurs identified by `kursId`
    ''' ends up at: looked up via its assigned Schiene (stage A's result)
    ''' and that Schiene's own solved slots (stage B's result). Every Kurs
    ''' assigned to the same Schiene shares exactly this same slot set -
    ''' that IS what a Schiene is, a shared, synchronized time block.
    ''' Unaffected by which pseudo-class/group a Schiene ended up in above
    ''' - this only ever looks at `.Subject` (the Schiene id), never
    ''' `.ClassName` (the group name). Reused by DeriveKursSchedule below
    ''' and by Raumzuordnung.vb's stage C scenario builder (which needs
    ''' the same per-Kurs slots to pin them via forbidden_slot).</summary>
    Public Function SlotsForKurs(kursId As String, kursblockungAssignment As Dictionary(Of String, String),
                                  schienenSchedule As List(Of ScheduleEntry)) As List(Of ScheduleEntry)
        Dim schieneId = kursblockungAssignment(kursId)
        Return schienenSchedule.Where(Function(e) e.Subject = schieneId).ToList()
    End Function

    ''' <summary>Pure derivation, no CP-SAT: replaces each Schiene's generic
    ''' day/period slots (from solving BuildSchienenrasterScenario via the
    ''' UNCHANGED Solver.Solve()) with one ScheduleEntry per real Kurs
    ''' assigned to that Schiene (stage A's result) - carrying the Kurs's
    ''' real Subject/Teacher, and the Schiene's id as .ClassName (see
    ''' module doc: internal-only stand-in, never exposed further). Room is
    ''' intentionally left Nothing - room assignment is stage C
    ''' (Raumzuordnung.vb), whose own output uses the Kurs's real id as
    ''' .ClassName instead and is the more useful caller-facing shape;
    ''' this function mainly exists to let stage-B-only tests verify
    ''' Wahlprofil non-collision before stage C runs.</summary>
    Public Function DeriveKursSchedule(ent As JsonObject, kursblockungAssignment As Dictionary(Of String, String),
                                        schienenSchedule As List(Of ScheduleEntry)) As List(Of ScheduleEntry)
        Dim result As New List(Of ScheduleEntry)
        For Each k In JsonHelpers.GetKurse(ent)
            Dim id = JsonHelpers.GetString(k, "id")
            Dim subject = JsonHelpers.GetString(k, "subject")
            Dim teacher = JsonHelpers.GetString(k, "teacher")
            For Each slot In SlotsForKurs(id, kursblockungAssignment, schienenSchedule)
                result.Add(New ScheduleEntry With {
                    .ClassName = kursblockungAssignment(id), .Subject = subject, .Teacher = teacher,
                    .Day = slot.Day, .Period = slot.Period, .Room = Nothing
                })
            Next
        Next
        Return result
    End Function

End Module
