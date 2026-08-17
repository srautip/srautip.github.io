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
        {"subject", "subjects"},
        {"room", "rooms"},
        {"allowed_rooms", "rooms"}
    }

    Private ReadOnly ResourceEntityKey As New Dictionary(Of String, String) From {
        {"teacher", "teachers"},
        {"class", "classes"},
        {"room", "rooms"}
    }

    ''' <summary>Cross-references every class/teacher/subject/room value
    ''' used in `constraints` against `entities`. Returns a list of
    ''' error strings (empty = all references are valid).</summary>
    Public Function ValidateEntities(data As JsonObject) As List(Of String)
        Dim ent = JsonHelpers.Entities(data)
        Dim known As New Dictionary(Of String, HashSet(Of String))
        For Each key In {"classes", "teachers", "subjects", "rooms"}
            known(key) = New HashSet(Of String)(JsonHelpers.AsStringList(ent, key))
        Next

        Dim errors As New List(Of String)
        Dim constraints = JsonHelpers.Constraints(data)

        For i = 0 To constraints.Count - 1
            Dim c = constraints(i)
            Dim constraintType = JsonHelpers.GetString(c, "type")

            For Each kvp In FieldEntityKey
                Dim field = kvp.Key
                Dim entityKey = kvp.Value
                If Not c.ContainsKey(field) OrElse c(field) Is Nothing Then Continue For
                For Each v In JsonHelpers.AsStringList(c(field))
                    If Not known(entityKey).Contains(v) Then
                        errors.Add(
                            $"constraints[{i}] (type={constraintType}): Feld '{field}'='{v}' " &
                            $"ist keine bekannte Entity (erlaubt: {JsonHelpers.PyListRepr(known(entityKey).OrderBy(Function(s) s))})")
                    End If
                Next
            Next

            If constraintType = "no_overlap" Then
                Dim resource = JsonHelpers.GetString(c, "resource")
                If Not ResourceEntityKey.ContainsKey(resource) Then
                    errors.Add($"constraints[{i}]: no_overlap.resource={JsonHelpers.PyRepr(resource)} ungueltig (erlaubt: teacher/class/room)")
                Else
                    Dim entityKey = ResourceEntityKey(resource)
                    Dim entityVal = JsonHelpers.GetString(c, "entity")
                    If Not known(entityKey).Contains(entityVal) Then
                        errors.Add($"constraints[{i}]: no_overlap.entity='{entityVal}' nicht in {entityKey}")
                    End If
                End If
            End If

            If constraintType = "forbidden_slot" Then
                Dim scope = JsonHelpers.GetString(c, "scope")
                If Not ResourceEntityKey.ContainsKey(scope) Then
                    errors.Add($"constraints[{i}]: forbidden_slot.scope={JsonHelpers.PyRepr(scope)} ungueltig (erlaubt: teacher/class/room)")
                Else
                    Dim entityKey = ResourceEntityKey(scope)
                    Dim entityVal = JsonHelpers.GetString(c, "entity")
                    If Not known(entityKey).Contains(entityVal) Then
                        errors.Add($"constraints[{i}]: forbidden_slot.entity='{entityVal}' nicht in {entityKey}")
                    End If
                End If
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

