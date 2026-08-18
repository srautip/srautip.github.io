' Phase 2.11c: Stage B - Schiene-to-day/period assignment. Reuses
' Solver.Solve()/BuildModel() COMPLETELY UNCHANGED: constructs a synthetic
' JSON scenario with a single pseudo-class "Kursstufe" whose "subjects"
' are the Schiene IDs from stage A (Kursblockung.SolveKursblockung) -
' `no_overlap(class="Kursstufe")` then keeps every Schiene at a mutually
' exclusive day/period slot, which is exactly what's needed (stage A
' already guarantees no Wahlprofil has two of its own courses in the same
' Schiene, so it suffices to keep the Schienen themselves from
' overlapping).
'
' See the phase-2.11 plan section for why the ORIGINAL "Schiene = virtual
' class carrying its courses" hypothesis was refuted (no_overlap(class=X)
' in Solver.vb enforces AT MOST ONE lesson of X per slot - the opposite of
' what a Schiene, which synchronizes several courses onto the SAME slot,
' needs) and this "Schienen = subjects of one pseudo-class" reformulation
' is the corrected, verified form. Not one line of Solver.vb changes for
' this stage - only this module's synthetic-scenario construction is new.
'
' The pseudo-class "Kursstufe" and pseudo-teacher "_schiene" are purely
' internal to this stage and must never leak into a caller-visible
' schedule/constraint list - DeriveKursSchedule below replaces them with
' the real Kurs/Lehrkraft identities before returning anything.
Imports System.Text.Json.Nodes

Public Module Schienenraster

    ''' <summary>Builds the synthetic Solver.Solve() input for stage B.
    ''' Every Schiene becomes one "subject" taught by the pseudo-teacher
    ''' "_schiene" to the pseudo-class "Kursstufe", with its weekly_hours
    ''' set from `hours_per_week`. If the Schiene JSON carries an optional
    ''' "block_length" (e.g. for LK-Doppelstunden - only meaningful when
    ''' hours_per_week is an exact multiple of it, same precondition as
    ''' every other block_length use in this project, e.g.
    ''' GymnasiumSekIFixture's Physik/Chemie double lessons), a matching
    ''' consecutive_required + max_per_day pair is added too.</summary>
    Public Function BuildSchienenrasterScenario(data As JsonObject) As JsonObject
        Dim ent = JsonHelpers.Entities(data)
        Dim timeslots = JsonHelpers.Timeslots(ent)
        Dim days = JsonHelpers.AsStringList(timeslots, "days")
        Dim periodsPerDay = JsonHelpers.GetInt(timeslots, "periods_per_day").Value
        Dim schienen = JsonHelpers.GetSchienen(ent)

        Dim constraints As New List(Of JsonObject)
        For Each s In schienen
            Dim id = JsonHelpers.GetString(s, "id")
            Dim hours = JsonHelpers.GetInt(s, "hours_per_week").Value
            Dim blockLength = JsonHelpers.GetInt(s, "block_length")

            constraints.Add(New JsonObject From {
                {"type", "teacher_subject_assignment"}, {"class", "Kursstufe"}, {"subject", id}, {"teacher", "_schiene"}
            })

            Dim wh As New JsonObject From {
                {"type", "weekly_hours"}, {"class", "Kursstufe"}, {"subject", id}, {"hours_per_week", hours}
            }
            If blockLength.HasValue Then wh("max_per_day") = blockLength.Value
            constraints.Add(wh)

            If blockLength.HasValue Then
                constraints.Add(New JsonObject From {
                    {"type", "consecutive_required"}, {"class", "Kursstufe"}, {"subject", id}, {"block_length", blockLength.Value}
                })
            End If
        Next
        constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "Kursstufe"}})

        Dim schienenEnt As New JsonObject From {
            {"classes", New JsonArray({CType("Kursstufe", JsonNode)})},
            {"teachers", New JsonArray({CType("_schiene", JsonNode)})},
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
    ''' Reused by DeriveKursSchedule below and by Raumzuordnung.vb's stage
    ''' C scenario builder (which needs the same per-Kurs slots to pin
    ''' them via forbidden_slot).</summary>
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
