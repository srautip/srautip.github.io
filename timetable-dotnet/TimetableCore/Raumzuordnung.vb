' Phase 2.11d: Stage C - room assignment on top of the already-solved
' Schienenraster (stage B). Reuses Solver.Solve()/BuildModel() UNCHANGED,
' same as stage B: builds one synthetic "class" per real Kurs (so the
' returned schedule carries the real Kurs identity as .ClassName, unlike
' stage B's internal "Kursstufe" pseudo-class), with the Kurs's day/period
' slots from stage B PINNED via forbidden_slot on every OTHER slot -
' combined with weekly_hours set to exactly that slot count, this forces
' the model to use precisely those slots (no JSON constraint type can
' directly "pin" a lesson to a specific slot, so pinning-via-exclusion is
' the deterministic, Solver.vb-unchanged way to do it).
'
' room_requirement (one per distinct subject, allowed_rooms = the full
' room pool) plus one no_overlap(resource="room") per room is what
' actually does the interesting work here: several Kurse sharing one
' Schiene run at the identical slots, so they need DIFFERENT rooms at
' that same time - exactly the kind of conflict room_requirement/
' no_overlap(room) already solve for the class-based model.
'
' no_overlap(resource="teacher") is included as a defense-in-depth check,
' even though Kursblockung's own teacher-collision constraint (stage A)
' should already make a same-teacher collision impossible here - if this
' DOES fire, that is a canary revealing a bug in stage A, not a
' constraint this stage expects to be doing real work.
Imports System.Text.Json.Nodes

Public Module Raumzuordnung

    Public Function BuildRaumzuordnungScenario(data As JsonObject, kursblockungAssignment As Dictionary(Of String, String),
                                                schienenSchedule As List(Of ScheduleEntry)) As JsonObject
        Dim ent = JsonHelpers.Entities(data)
        Dim timeslots = JsonHelpers.Timeslots(ent)
        Dim days = JsonHelpers.AsStringList(timeslots, "days")
        Dim periodsPerDay = JsonHelpers.GetInt(timeslots, "periods_per_day").Value
        Dim allPeriods = Enumerable.Range(1, periodsPerDay).ToList()
        Dim rooms = JsonHelpers.AsStringList(ent, "rooms")
        Dim kurse = JsonHelpers.GetKurse(ent)

        Dim classNames As New List(Of String)
        Dim teacherNames As New HashSet(Of String)
        Dim subjectNames As New HashSet(Of String)
        Dim roomReqSubjectsAdded As New HashSet(Of String)
        Dim constraints As New List(Of JsonObject)

        For Each k In kurse
            Dim id = JsonHelpers.GetString(k, "id")
            Dim subject = JsonHelpers.GetString(k, "subject")
            Dim teacher = JsonHelpers.GetString(k, "teacher")
            classNames.Add(id)
            teacherNames.Add(teacher)
            subjectNames.Add(subject)

            Dim slots = Schienenraster.SlotsForKurs(id, kursblockungAssignment, schienenSchedule)
            Dim slotSet As New HashSet(Of (Day As String, Period As Integer))(
                slots.Select(Function(s) (Day:=s.Day, Period:=s.Period)))

            constraints.Add(New JsonObject From {
                {"type", "teacher_subject_assignment"}, {"class", id}, {"subject", subject}, {"teacher", teacher}
            })
            constraints.Add(New JsonObject From {
                {"type", "weekly_hours"}, {"class", id}, {"subject", subject}, {"hours_per_week", slots.Count}
            })

            For Each d In days
                For Each p In allPeriods
                    If Not slotSet.Contains((Day:=d, Period:=p)) Then
                        constraints.Add(New JsonObject From {
                            {"type", "forbidden_slot"}, {"scope", "class"}, {"entity", id}, {"day", d}, {"period", p}
                        })
                    End If
                Next
            Next

            If rooms.Count > 0 AndAlso roomReqSubjectsAdded.Add(subject) Then
                constraints.Add(New JsonObject From {
                    {"type", "room_requirement"}, {"subject", subject},
                    {"allowed_rooms", New JsonArray(rooms.Select(Function(r) CType(r, JsonNode)).ToArray())}
                })
            End If
        Next

        For Each room In rooms
            constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "room"}, {"entity", room}})
        Next
        For Each teacher In teacherNames
            constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", teacher}})
        Next

        Dim raumEnt As New JsonObject From {
            {"classes", New JsonArray(classNames.Select(Function(c) CType(c, JsonNode)).ToArray())},
            {"teachers", New JsonArray(teacherNames.Select(Function(t) CType(t, JsonNode)).ToArray())},
            {"subjects", New JsonArray(subjectNames.Select(Function(s) CType(s, JsonNode)).ToArray())},
            {"rooms", New JsonArray(rooms.Select(Function(r) CType(r, JsonNode)).ToArray())},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray(days.Select(Function(d) CType(d, JsonNode)).ToArray())},
                {"periods_per_day", periodsPerDay}
            }}
        }

        Return New JsonObject From {
            {"entities", raumEnt},
            {"constraints", New JsonArray(constraints.Select(Function(c) CType(c, JsonNode)).ToArray())}
        }
    End Function

End Module
