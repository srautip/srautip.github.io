' Shared scenario-building logic for Phase 2's new fixtures
' (GrundschuleFixture, OberstufeFixture), generalizing the pattern already
' used by GymnasiumKlasse5Fixture (Phase 1): a scenario described as a
' table of "who teaches what to which classes, how many hours, in which
' room" systematically expands into teacher_subject_assignment,
' weekly_hours, consecutive_required, room_requirement and no_overlap
' constraints, plus whatever scenario-specific extras (teacher_availability,
' forbidden_slot, shared_resource_conflict, ...) the caller supplies.
'
' Deliberately named `SubjectAssignment` (not `Assignment`) to avoid
' colliding with GymnasiumKlasse5Fixture's own nested `Assignment` class -
' VB.NET Modules expose their Public members unqualified throughout the
' project, and GymnasiumKlasse5Fixture.vb (Phase 1, already tested/shipped)
' relies on referring to its own `Assignment`/`Assignments` unqualified. A
' second same-named type in a sibling module would make every such
' reference ambiguous.
Imports System.Text.Json.Nodes

Public NotInheritable Class SubjectAssignment
    Public ReadOnly Subject As String
    Public ReadOnly Teacher As String
    Public ReadOnly TaughtClasses As List(Of String)
    Public ReadOnly Hours As Integer
    Public ReadOnly MaxPerDay As Integer
    Public ReadOnly BlockLength As Integer?
    Public ReadOnly AllowedRooms As List(Of String)

    Public Sub New(subject As String, teacher As String, taughtClasses As List(Of String),
                    hours As Integer, maxPerDay As Integer, blockLength As Integer?, allowedRooms As List(Of String))
        Me.Subject = subject
        Me.Teacher = teacher
        Me.TaughtClasses = taughtClasses
        Me.Hours = hours
        Me.MaxPerDay = maxPerDay
        Me.BlockLength = blockLength
        Me.AllowedRooms = allowedRooms
    End Sub
End Class

Public Module AssignmentScenarioBuilder

    Private Function DerivedTeachers(assignments As List(Of SubjectAssignment)) As List(Of String)
        Return assignments.Select(Function(a) a.Teacher).Distinct().OrderBy(Function(s) s).ToList()
    End Function

    Private Function DerivedRooms(assignments As List(Of SubjectAssignment)) As List(Of String)
        Return assignments.Where(Function(a) a.AllowedRooms IsNot Nothing).
            SelectMany(Function(a) a.AllowedRooms).Distinct().OrderBy(Function(s) s).ToList()
    End Function

    ''' <summary>Builds entities+constraints purely from a subject/teacher
    ''' assignment table (see GymnasiumKlasse5Fixture for the original,
    ''' non-generalized version this was extracted from). Appends
    ''' `extraConstraints` (e.g. teacher_availability, forbidden_slot,
    ''' shared_resource_conflict) verbatim after the systematically
    ''' generated ones.</summary>
    Public Function BuildScenario(classes As List(Of String), days As List(Of String), periodsPerDay As Integer,
                                   assignments As List(Of SubjectAssignment),
                                   Optional extraConstraints As IEnumerable(Of JsonObject) = Nothing) As JsonObject
        Dim teachers = DerivedTeachers(assignments)
        Dim subjects = assignments.Select(Function(a) a.Subject).Distinct().OrderBy(Function(s) s).ToList()
        Dim rooms = DerivedRooms(assignments)

        Dim constraints As New JsonArray()

        For Each a In assignments
            For Each cls In a.TaughtClasses
                constraints.Add(New JsonObject From {
                    {"type", "teacher_subject_assignment"}, {"teacher", a.Teacher}, {"class", cls}, {"subject", a.Subject}
                })
                constraints.Add(New JsonObject From {
                    {"type", "weekly_hours"}, {"class", cls}, {"subject", a.Subject},
                    {"hours_per_week", a.Hours}, {"max_per_day", a.MaxPerDay}
                })
                If a.BlockLength.HasValue Then
                    constraints.Add(New JsonObject From {
                        {"type", "consecutive_required"}, {"class", cls}, {"subject", a.Subject}, {"block_length", a.BlockLength.Value}
                    })
                End If
            Next
        Next

        Dim roomsPerSubject As New Dictionary(Of String, List(Of String))
        For Each a In assignments
            If a.AllowedRooms IsNot Nothing AndAlso Not roomsPerSubject.ContainsKey(a.Subject) Then
                roomsPerSubject(a.Subject) = a.AllowedRooms
            End If
        Next
        For Each kvp In roomsPerSubject
            constraints.Add(New JsonObject From {
                {"type", "room_requirement"}, {"subject", kvp.Key},
                {"allowed_rooms", New JsonArray(kvp.Value.Select(Function(r) CType(r, JsonNode)).ToArray())}
            })
        Next

        For Each cls In classes
            constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", cls}})
        Next
        For Each teacher In teachers
            constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", teacher}})
        Next
        For Each room In rooms
            constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "room"}, {"entity", room}})
        Next

        If extraConstraints IsNot Nothing Then
            For Each c In extraConstraints
                constraints.Add(c)
            Next
        End If

        Return New JsonObject From {
            {"entities", New JsonObject From {
                {"classes", New JsonArray(classes.Select(Function(c) CType(c, JsonNode)).ToArray())},
                {"teachers", New JsonArray(teachers.Select(Function(t) CType(t, JsonNode)).ToArray())},
                {"subjects", New JsonArray(subjects.Select(Function(s) CType(s, JsonNode)).ToArray())},
                {"rooms", New JsonArray(rooms.Select(Function(r) CType(r, JsonNode)).ToArray())},
                {"timeslots", New JsonObject From {
                    {"days", New JsonArray(days.Select(Function(d) CType(d, JsonNode)).ToArray())},
                    {"periods_per_day", periodsPerDay}
                }}
            }},
            {"constraints", constraints}
        }
    End Function

    ' --- Ground-truth "expected" builders, mirroring the systematic part
    '     of BuildScenario above. teacher_availability and forbidden_slot
    '     are NOT derivable from the assignment table (they're not part of
    '     it) - each fixture builds those expected sets itself. ---

    Public Function ExpectedTeacherSubjectAssignments(assignments As List(Of SubjectAssignment)) As HashSet(Of (Cls As String, Subject As String, Teacher As String))
        Dim result As New HashSet(Of (String, String, String))
        For Each a In assignments
            For Each cls In a.TaughtClasses
                result.Add((cls, a.Subject, a.Teacher))
            Next
        Next
        Return result
    End Function

    Public Function ExpectedWeeklyHours(assignments As List(Of SubjectAssignment)) As Dictionary(Of (Cls As String, Subject As String), (Hours As Integer, MaxPerDay As Integer))
        Dim result As New Dictionary(Of (String, String), (Integer, Integer))
        For Each a In assignments
            For Each cls In a.TaughtClasses
                result((cls, a.Subject)) = (a.Hours, a.MaxPerDay)
            Next
        Next
        Return result
    End Function

    Public Function ExpectedNoOverlap(classes As List(Of String), assignments As List(Of SubjectAssignment)) As HashSet(Of (Resource As String, Entity As String))
        Dim result As New HashSet(Of (String, String))
        For Each cls In classes : result.Add(("class", cls)) : Next
        For Each t In DerivedTeachers(assignments) : result.Add(("teacher", t)) : Next
        For Each r In DerivedRooms(assignments) : result.Add(("room", r)) : Next
        Return result
    End Function

    Public Function ExpectedRoomRequirement(assignments As List(Of SubjectAssignment)) As Dictionary(Of String, List(Of String))
        Dim result As New Dictionary(Of String, List(Of String))
        For Each a In assignments
            If a.AllowedRooms IsNot Nothing AndAlso Not result.ContainsKey(a.Subject) Then
                result(a.Subject) = a.AllowedRooms.OrderBy(Function(s) s).ToList()
            End If
        Next
        Return result
    End Function

    Public Function ExpectedConsecutiveRequired(assignments As List(Of SubjectAssignment)) As HashSet(Of (Cls As String, Subject As String, BlockLength As Integer))
        Dim result As New HashSet(Of (String, String, Integer))
        For Each a In assignments
            If a.BlockLength.HasValue Then
                For Each cls In a.TaughtClasses
                    result.Add((cls, a.Subject, a.BlockLength.Value))
                Next
            End If
        Next
        Return result
    End Function

End Module
