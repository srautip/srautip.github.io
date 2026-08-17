' Small JSON-building helpers shared across the test suite, mirroring the
' `mini()`/`scenario()` helper functions at the top of
' tests/test_timetable_model.py.
Imports System.Text.Json.Nodes

Public Module TestBuilders

    Public Function Mini(classes As IEnumerable(Of String), teachers As IEnumerable(Of String),
                          subjects As IEnumerable(Of String), rooms As IEnumerable(Of String),
                          days As IEnumerable(Of String), periodsPerDay As Integer) As JsonObject
        Return New JsonObject From {
            {"classes", New JsonArray(classes.Select(Function(c) CType(c, JsonNode)).ToArray())},
            {"teachers", New JsonArray(teachers.Select(Function(t) CType(t, JsonNode)).ToArray())},
            {"subjects", New JsonArray(subjects.Select(Function(s) CType(s, JsonNode)).ToArray())},
            {"rooms", New JsonArray(rooms.Select(Function(r) CType(r, JsonNode)).ToArray())},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray(days.Select(Function(d) CType(d, JsonNode)).ToArray())},
                {"periods_per_day", periodsPerDay}
            }}
        }
    End Function

    Public Function Scenario(entities As JsonObject, constraints As IEnumerable(Of JsonObject)) As JsonObject
        Return New JsonObject From {
            {"entities", entities},
            {"constraints", New JsonArray(constraints.Select(Function(c) CType(c, JsonNode)).ToArray())}
        }
    End Function

End Module
