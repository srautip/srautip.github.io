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

    ' Phase 2.5: Muss/Kann priority. "must" (default when absent) keeps every
    ' existing constraint's behavior byte-identical; "should" marks a
    ' constraint as a soft preference the solver may violate (see Solver.vb's
    ' KannVars/model.Minimize and Verifier.vb's VerifyScheduleDetailed).
    Public Const PriorityMust As String = "must"
    Public Const PriorityShould As String = "should"

    ''' <summary>Defaults to PriorityMust when the "priority" field is
    ''' absent - this default is what makes every pre-Phase-2.5 fixture
    ''' behave unchanged. Does NOT validate the value; Validation.vb is
    ''' responsible for rejecting anything other than must/should.
    ''' NOTE: LlmExtraction.vb does NOT yet ask Qwen to set this field from
    ''' wording like "wenn moeglich"/"idealerweise" - that is an explicit,
    ''' separately live-tested follow-up phase, not part of Phase 2.5's
    ''' deterministic core (Models/Solver/Verifier + hand-written
    ''' fixtures).</summary>
    Public Function GetPriority(c As JsonObject) As String
        Dim p = GetString(c, "priority")
        Return If(String.IsNullOrEmpty(p), PriorityMust, p)
    End Function

    ''' <summary>Optional human-readable provenance for a constraint (e.g.
    ''' an LLM-authored paraphrase of the prompt text that produced it, see
    ''' LlmExtraction.vb's per-type "reason" schema field). Nothing when
    ''' absent - used to enrich Validation/Verifier messages, not required
    ''' by any constraint type.</summary>
    Public Function GetReason(c As JsonObject) As String
        Return GetString(c, "reason")
    End Function

End Module

