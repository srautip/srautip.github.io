' Shared JSON access helpers used by Validation.vb, Solver.vb, Verifier.vb and
' Formatting.vb. Deliberately keeps `entities`/`constraints` as raw
' System.Text.Json.Nodes objects (JsonObject/JsonArray) rather than a rigid
' class hierarchy - this mirrors the Python original's dict-based data model
' field-for-field, which minimizes translation risk during the port (the
' Python code operates on plain dicts throughout, e.g. `c.get("field")`).
' A typed model (for GUI data-binding) can be layered on top later without
' touching this core - see the project plan's Phase 3 note.
Imports System.Text.Json.Nodes


Public Module JsonHelpers

    ''' <summary>Mimics Python's dict.get(key) -> returns Nothing if the
    ''' key is absent or its value is JSON null, instead of throwing.</summary>
    Public Function GetString(obj As JsonObject, key As String) As String
        If obj Is Nothing OrElse Not obj.ContainsKey(key) OrElse obj(key) Is Nothing Then
            Return Nothing
        End If
        Return obj(key).GetValue(Of String)()
    End Function

    Public Function GetInt(obj As JsonObject, key As String) As Integer?
        If obj Is Nothing OrElse Not obj.ContainsKey(key) OrElse obj(key) Is Nothing Then
            Return Nothing
        End If
        Return obj(key).GetValue(Of Integer)()
    End Function

    ''' <summary>A field can be either a single string or a list of
    ''' strings in the JSON schema (e.g. "class" vs "classes"). Mirrors
    ''' Python's `val if isinstance(val, list) else [val]`. Returns an
    ''' empty list if the field is absent/null.</summary>
    Public Function AsStringList(node As JsonNode) As List(Of String)
        Dim result As New List(Of String)
        If node Is Nothing Then Return result
        If node.GetValueKind() = System.Text.Json.JsonValueKind.Array Then
            For Each item In node.AsArray()
                If item IsNot Nothing Then result.Add(item.GetValue(Of String)())
            Next
        Else
            result.Add(node.GetValue(Of String)())
        End If
        Return result
    End Function

    Public Function AsStringList(obj As JsonObject, key As String) As List(Of String)
        If obj Is Nothing OrElse Not obj.ContainsKey(key) Then Return New List(Of String)
        Return AsStringList(obj(key))
    End Function

    ''' <summary>Python-style list repr, e.g. ['5a', '5b'] - used to keep
    ''' error/warning message wording close to the Python original.</summary>
    Public Function PyListRepr(items As IEnumerable(Of String)) As String
        Return "[" & String.Join(", ", items.Select(Function(s) $"'{s}'")) & "]"
    End Function

    Public Function PyRepr(s As String) As String
        If s Is Nothing Then Return "None"
        Return $"'{s}'"
    End Function

    Public Function Constraints(data As JsonObject) As List(Of JsonObject)
        Return data("constraints").AsArray().Select(Function(n) n.AsObject()).ToList()
    End Function

    Public Function Entities(data As JsonObject) As JsonObject
        Return data("entities").AsObject()
    End Function

    Public Function Timeslots(entities As JsonObject) As JsonObject
        Return entities("timeslots").AsObject()
    End Function

End Module

