' Ported 1:1 from timetable/llm_extraction.py. LLM-based extraction of
' CP-SAT constraints from natural-language scheduling requirements, using
' a locally hosted Ollama model, via direct HttpClient calls (no Python
' involved at runtime, per the project's "kein Python" decision).
'
' Deliberately decomposed into one narrow, schema-constrained call PER
' CONSTRAINT TYPE - this is a design decision backed by direct experiment
' on the Python original, not a guess:
'
' - A single big extraction call with only a loose JSON format produced a
'   syntactically valid but incomplete result (some general rules only
'   applied to the first-mentioned entity instead of all of them).
' - A two-stage "extract, then ask the model to check completeness and
'   repair" pipeline made things WORSE: the repair pass hallucinated new,
'   unsupported constraints instead of fixing the real gaps.
' - Decomposing into one call per constraint type, each with its own tight
'   JSON Schema (Ollama's `format: <schema>`, not just `"json"`) and
'   `think: false`, produced the most complete and least hallucinated
'   results, verified with the deterministic Validation.vb afterwards.
'
' The German instruction texts and the JSON Schemas below are copied
' wortgleich from llm_extraction.py - they were tuned against real model
' behavior, not just written for readability, so they must not be
' "cleaned up" during porting.
'
' Requires a running Ollama server (default http://127.0.0.1:11434) with
' the model already pulled (default qwen3.5:4b). Call IsOllamaAvailable
' before ExtractAllConstraints to fail fast with a clear reason if not.
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json.Nodes
Imports System.Threading


Public Module LlmExtraction

    Public Const OllamaUrl As String = "http://127.0.0.1:11434"
    Public Const Model As String = "qwen3.5:4b"

    Public ReadOnly AllTypes As New List(Of String) From {
        "teacher_availability", "weekly_hours", "room_requirement", "no_overlap",
        "shared_resource_conflict", "forbidden_slot", "consecutive_required",
        "teacher_subject_assignment", "period_exception"
    }

    Private ReadOnly SharedHttpClient As New HttpClient() With {.Timeout = Timeout.InfiniteTimeSpan}

    Private ReadOnly Instructions As New Dictionary(Of String, String) From {
        {"teacher_availability",
            "Extrahiere Verfuegbarkeits-Einschraenkungen einzelner Lehrkraefte - " &
            "auch wenn das Wort 'verfuegbar' nicht woertlich vorkommt, z.B. bei " &
            "'ist nur montags bis mittwochs an der Schule', 'arbeitet Teilzeit " &
            "und ist montags, dienstags da', 'kann an keinem Tag ausser Montag " &
            "und Mittwoch unterrichten' oder aehnlichen Formulierungen. Bei einer " &
            "Verneinung ('kann an keinem Tag ausser X, Y unterrichten') sind X " &
            "und Y die available_days (die Tage NACH 'ausser'/'außer', nicht die " &
            "davor genannten). JEDES Objekt MUSS available_days (Liste der Tage, " &
            "an denen die Lehrkraft unterrichten kann) oder unavailable_periods " &
            "gesetzt haben - niemals nur Typ und Lehrername ohne diese Angabe. " &
            "Ein Objekt pro betroffener Lehrkraft, keine Duplikate. Keine " &
            "Lehrkraft ohne im Text genannte Einschraenkung erwaehnen."},
        {"weekly_hours",
            "Extrahiere fuer JEDE im Text genannte Klasse+Fach-Kombination die " &
            "Wochenstunden (und, falls genannt, das Tagesmaximum). Erfinde keine " &
            "Werte fuer nicht genannte Kombinationen."},
        {"room_requirement",
            "Extrahiere Faecher, die laut Text nur in bestimmten Raeumen " &
            "stattfinden duerfen."},
        {"no_overlap",
            "Extrahiere die generelle Ueberschneidungsfreiheit-Regel. Falls der " &
            "Text eine solche allgemeine Regel nennt, erzeuge JE EIN Objekt fuer " &
            "JEDE Klasse aus entities.classes (resource=class), JEDEN Lehrer aus " &
            "entities.teachers (resource=teacher) und JEDEN Fachraum aus " &
            "entities.rooms, der als gemeinsam genutzter Raum vorkommt " &
            "(resource=room). Liste alle vollstaendig auf, keine auslassen."},
        {"shared_resource_conflict",
            "Extrahiere Faelle, in denen mehrere Klassen wegen derselben " &
            "Lehrkraft nicht gleichzeitig denselben Unterricht haben duerfen."},
        {"forbidden_slot",
            "Extrahiere feste Sperrzeiten (Tag+Stunde), die der Text DIREKT als " &
            "gesperrt benennt (z.B. 'freitags 6. Stunde frei'). Wenn die " &
            "Sperrzeit schulweit fuer alle Klassen gilt, erzeuge JE EIN Objekt " &
            "PRO Klasse aus entities.classes. Ignoriere Ausnahme-Formulierungen " &
            "der Form 'nur an Tag X erlaubt' / 'hoechstens an einem Tag' - " &
            "dafuer gibt es einen anderen, spezialisierten Constraint-Typ."},
        {"consecutive_required",
            "Extrahiere Faecher, die als zusammenhaengender Block (Doppelstunde " &
            "o.ae.) unterrichtet werden muessen. Erzeuge JE EIN Objekt PRO " &
            "betroffener Klasse."},
        {"teacher_subject_assignment",
            "Extrahiere, welche Lehrkraft welches Fach in welcher Klasse " &
            "unterrichtet. Ein Objekt PRO genannter Klasse, auch wenn eine " &
            "Lehrkraft mehrere Klassen unterrichtet."},
        {"period_exception",
            "Extrahiere Regeln der Form 'Stunde X findet hoechstens an einem " &
            "Tag pro Woche statt, idealerweise Tag Y' bzw. 'Stunde X nur an " &
            "bestimmten Tagen'. Erzeuge EIN Objekt mit der Stundennummer und " &
            "der Liste der ERLAUBTEN Tage (NICHT der gesperrten!). Beispiel: " &
            "'7. Stunde nur dienstags, idealerweise' -> " &
            "{""period"": 7, ""allowed_days"": [""Di""]}. Ignoriere normale " &
            "Sperrzeiten, die direkt einen gesperrten Tag nennen (z.B. " &
            "'freitags 6. Stunde frei') - dafuer gibt es einen anderen " &
            "Constraint-Typ."}
    }

    ' --- JSON-Schema-Bausteine (mirrors _obj/_ITEM_SCHEMAS) ---

    Private Function ObjSchema(props As JsonObject, required As IEnumerable(Of String)) As JsonObject
        Dim o As New JsonObject()
        o("type") = "object"
        o("properties") = props
        Dim arr As New JsonArray()
        For Each r In required
            arr.Add(r)
        Next
        o("required") = arr
        Return o
    End Function

    Private Function StringSchema() As JsonObject
        Dim o As New JsonObject()
        o("type") = "string"
        Return o
    End Function

    Private Function IntegerSchema() As JsonObject
        Dim o As New JsonObject()
        o("type") = "integer"
        Return o
    End Function

    Private Function ConstSchema(value As String) As JsonObject
        Dim o As New JsonObject()
        o("const") = value
        Return o
    End Function

    Private Function EnumSchema(values As IEnumerable(Of String)) As JsonObject
        Dim o As New JsonObject()
        o("type") = "string"
        Dim arr As New JsonArray()
        For Each v In values
            arr.Add(v)
        Next
        o("enum") = arr
        Return o
    End Function

    Private Function ArraySchema(items As JsonObject) As JsonObject
        Dim o As New JsonObject()
        o("type") = "array"
        o("items") = items
        Return o
    End Function

    ''' <summary>Builds a fresh copy of the item schema for one
    ''' constraint type each call - JsonNode instances can only have one
    ''' parent at a time, so a shared/cached instance can't be reused
    ''' across requests.</summary>
    Private Function ItemSchema(constraintType As String) As JsonObject
        Select Case constraintType

            Case "teacher_availability"
                Dim props As New JsonObject()
                props("type") = ConstSchema("teacher_availability")
                props("teacher") = StringSchema()
                props("available_days") = ArraySchema(StringSchema())
                Dim periodItemProps As New JsonObject()
                periodItemProps("day") = StringSchema()
                periodItemProps("period") = IntegerSchema()
                props("unavailable_periods") = ArraySchema(ObjSchema(periodItemProps, {"day", "period"}))
                props("reason") = StringSchema()
                ' available_days is required (not just type/teacher) to force
                ' the model to actually populate the constraint content -
                ' Phase 2 found it would otherwise sometimes emit a bare
                ' {type, teacher} object with no available_days/
                ' unavailable_periods at all whenever the input text didn't
                ' use the literal word "verfuegbar".
                Return ObjSchema(props, {"type", "teacher", "available_days"})

            Case "weekly_hours"
                Dim props As New JsonObject()
                props("type") = ConstSchema("weekly_hours")
                props("class") = StringSchema()
                props("subject") = StringSchema()
                props("hours_per_week") = IntegerSchema()
                props("max_per_day") = IntegerSchema()
                Return ObjSchema(props, {"type", "class", "subject", "hours_per_week"})

            Case "room_requirement"
                Dim props As New JsonObject()
                props("type") = ConstSchema("room_requirement")
                props("subject") = StringSchema()
                props("allowed_rooms") = ArraySchema(StringSchema())
                props("reason") = StringSchema()
                Return ObjSchema(props, {"type", "subject", "allowed_rooms"})

            Case "no_overlap"
                Dim props As New JsonObject()
                props("type") = ConstSchema("no_overlap")
                props("resource") = EnumSchema({"teacher", "class", "room"})
                props("entity") = StringSchema()
                props("reason") = StringSchema()
                Return ObjSchema(props, {"type", "resource", "entity"})

            Case "shared_resource_conflict"
                Dim props As New JsonObject()
                props("type") = ConstSchema("shared_resource_conflict")
                props("classes") = ArraySchema(StringSchema())
                props("subject") = StringSchema()
                props("teacher") = StringSchema()
                props("reason") = StringSchema()
                Return ObjSchema(props, {"type", "classes", "subject", "teacher"})

            Case "forbidden_slot"
                Dim props As New JsonObject()
                props("type") = ConstSchema("forbidden_slot")
                props("scope") = EnumSchema({"class", "teacher", "room"})
                props("entity") = StringSchema()
                props("day") = StringSchema()
                props("period") = IntegerSchema()
                props("reason") = StringSchema()
                Return ObjSchema(props, {"type", "scope", "entity", "day", "period"})

            Case "consecutive_required"
                Dim props As New JsonObject()
                props("type") = ConstSchema("consecutive_required")
                props("class") = StringSchema()
                props("subject") = StringSchema()
                props("block_length") = IntegerSchema()
                props("reason") = StringSchema()
                Return ObjSchema(props, {"type", "class", "subject", "block_length"})

            Case "teacher_subject_assignment"
                Dim props As New JsonObject()
                props("type") = ConstSchema("teacher_subject_assignment")
                props("teacher") = StringSchema()
                props("class") = StringSchema()
                props("subject") = StringSchema()
                Return ObjSchema(props, {"type", "teacher", "class", "subject"})

            Case "period_exception"
                ' NOT a CP-SAT-facing constraint type. Captures "period X only
                ' happens on days Y" as a single fact (an easy extraction) instead
                ' of asking the model to enumerate "every day EXCEPT Y" (a
                ' set-difference computation the model got wrong two different
                ' ways in testing - once incomplete, once with the polarity
                ' inverted). ExtractAllConstraints deterministically expands this
                ' into forbidden_slot entries in pure code - see
                ' ExpandPeriodException below.
                Dim props As New JsonObject()
                props("type") = ConstSchema("period_exception")
                props("period") = IntegerSchema()
                props("allowed_days") = ArraySchema(StringSchema())
                props("reason") = StringSchema()
                Return ObjSchema(props, {"type", "period", "allowed_days"})

            Case Else
                Throw New ArgumentException($"Unbekannter Constraint-Typ: '{constraintType}'")
        End Select
    End Function

    ''' <summary>Deterministically turns {"period": X, "allowed_days":
    ''' [...]} into one forbidden_slot entry per (blocked day, class) -
    ''' pure set difference over entities.timeslots.days, no LLM
    ''' involved.</summary>
    Public Function ExpandPeriodException(entities As JsonObject, item As JsonObject) As List(Of JsonObject)
        Dim allDays = JsonHelpers.AsStringList(JsonHelpers.Timeslots(entities), "days")
        Dim allowed As New HashSet(Of String)(JsonHelpers.AsStringList(item, "allowed_days"))
        Dim blockedDays = allDays.Where(Function(d) Not allowed.Contains(d)).ToList()
        Dim period = JsonHelpers.GetInt(item, "period").Value
        Dim classes = JsonHelpers.AsStringList(entities, "classes")

        Dim result As New List(Of JsonObject)
        For Each blockedDay In blockedDays
            For Each cls In classes
                Dim o As New JsonObject()
                o("type") = "forbidden_slot"
                o("scope") = "class"
                o("entity") = cls
                o("day") = blockedDay
                o("period") = period
                o("reason") = $"nur erlaubt an {JsonHelpers.PyListRepr(allowed.OrderBy(Function(s) s))}"
                result.Add(o)
            Next
        Next
        Return result
    End Function

    Private ReadOnly Expanders As New Dictionary(Of String, Func(Of JsonObject, JsonObject, List(Of JsonObject))) From {
        {"period_exception", AddressOf ExpandPeriodException}
    }

    ''' <summary>Returns (available, reason) - reason explains why not,
    ''' if not available.</summary>
    Public Async Function IsOllamaAvailable(Optional model As String = Model, Optional baseUrl As String = OllamaUrl) As Task(Of (Available As Boolean, Reason As String))
        Try
            Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(3))
                Dim resp = Await SharedHttpClient.GetAsync($"{baseUrl}/", cts.Token)
                resp.EnsureSuccessStatusCode()
            End Using
        Catch ex As Exception
            Return (False, $"Ollama nicht erreichbar unter {baseUrl}: {ex.Message}")
        End Try

        Dim tagsJson As String
        Try
            Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(5))
                Dim resp = Await SharedHttpClient.GetAsync($"{baseUrl}/api/tags", cts.Token)
                resp.EnsureSuccessStatusCode()
                tagsJson = Await resp.Content.ReadAsStringAsync(cts.Token)
            End Using
        Catch ex As Exception
            Return (False, $"Ollama /api/tags fehlgeschlagen: {ex.Message}")
        End Try

        Dim tags = JsonNode.Parse(tagsJson).AsObject()
        Dim names As New HashSet(Of String)
        If tags.ContainsKey("models") AndAlso tags("models") IsNot Nothing Then
            For Each m In tags("models").AsArray()
                Dim nm = JsonHelpers.GetString(m.AsObject(), "name")
                If nm IsNot Nothing Then names.Add(nm)
            Next
        End If
        If Not names.Contains(model) Then
            Return (False, $"Modell '{model}' nicht gepullt (vorhanden: {JsonHelpers.PyListRepr(names.OrderBy(Function(s) s))})")
        End If
        Return (True, "")
    End Function

    ''' <summary>Calls the LLM for exactly one constraint type. Returns
    ''' (constraints, meta). Returned JsonObject items are always
    ''' unparented (via DeepClone), so callers can freely reinsert them
    ''' into other JsonObject/JsonArray structures.</summary>
    Public Async Function ExtractConstraintType(
        entities As JsonObject, promptText As String, constraintType As String,
        Optional model As String = Model, Optional baseUrl As String = OllamaUrl,
        Optional temperature As Double = 0.1, Optional numCtx As Integer = 8192,
        Optional numPredict As Integer = 1800, Optional timeoutS As Integer = 600) As Task(Of (Constraints As List(Of JsonObject), Meta As JsonObject))

        Dim constraintsSchema As New JsonObject()
        constraintsSchema("type") = "array"
        constraintsSchema("items") = ItemSchema(constraintType)
        Dim propsObj As New JsonObject()
        propsObj("constraints") = constraintsSchema
        Dim schema = ObjSchema(propsObj, {"constraints"})

        Dim systemPrompt =
            "Du bist ein spezialisierter Extraktions-Assistent fuer GENAU EINEN " &
            $"Constraint-Typ ('{constraintType}') fuer einen Schul-Stundenplan-Solver " &
            "(CP-SAT). Ignoriere alle anderen Einschraenkungsarten im Text " &
            "vollstaendig." & vbLf & vbLf & Instructions(constraintType)

        Dim userContent =
            "ENTITIES (vollstaendige Liste, nur diese Namen verwenden):" & vbLf &
            entities.ToJsonString() & vbLf & vbLf & "ANFORDERUNGEN:" & vbLf & promptText

        Dim sysMsg As New JsonObject()
        sysMsg("role") = "system"
        sysMsg("content") = systemPrompt
        Dim userMsg As New JsonObject()
        userMsg("role") = "user"
        userMsg("content") = userContent
        Dim messages As New JsonArray From {sysMsg, userMsg}

        Dim options As New JsonObject()
        options("temperature") = temperature
        options("num_ctx") = numCtx
        options("num_predict") = numPredict

        Dim payload As New JsonObject()
        payload("model") = model
        payload("messages") = messages
        payload("stream") = False
        payload("format") = schema
        payload("think") = False
        payload("options") = options

        Dim requestJson = payload.ToJsonString()
        Dim t0 = DateTime.UtcNow
        Dim outerJson As String
        Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(timeoutS))
            Using httpContent As New StringContent(requestJson, Encoding.UTF8, "application/json")
                Dim resp = Await SharedHttpClient.PostAsync($"{baseUrl}/api/chat", httpContent, cts.Token)
                resp.EnsureSuccessStatusCode()
                outerJson = Await resp.Content.ReadAsStringAsync(cts.Token)
            End Using
        End Using
        Dim dt = (DateTime.UtcNow - t0).TotalSeconds

        Dim outer = JsonNode.Parse(outerJson).AsObject()
        Dim messageObj = outer("message").AsObject()
        Dim content = If(JsonHelpers.GetString(messageObj, "content"), "")

        Dim meta As New JsonObject()
        meta("type") = constraintType
        meta("duration_s") = dt
        meta("done_reason") = JsonHelpers.GetString(outer, "done_reason")

        Dim constraints As New List(Of JsonObject)
        Try
            Dim parsed = JsonNode.Parse(content).AsObject()
            If parsed.ContainsKey("constraints") AndAlso parsed("constraints") IsNot Nothing Then
                For Each item In parsed("constraints").AsArray()
                    constraints.Add(DirectCast(item.DeepClone(), JsonObject))
                Next
            End If
            meta("valid_json") = True
        Catch ex As Exception
            constraints = New List(Of JsonObject)
            meta("valid_json") = False
            meta("parse_error") = ex.Message
            meta("raw") = content.Substring(0, Math.Min(1000, content.Length))
        End Try
        meta("n_items") = constraints.Count

        Return (constraints, meta)
    End Function

    ''' <summary>Runs ExtractConstraintType for each type (default: all,
    ''' including period_exception), sequentially. Types with an entry
    ''' in Expanders are deterministically expanded into their real
    ''' CP-SAT constraint(s) before being merged in - the caller always
    ''' gets back a flat list of valid constraint types, never
    ''' period_exception itself. Returns (mergedConstraints,
    ''' metaPerType).</summary>
    Public Async Function ExtractAllConstraints(
        entities As JsonObject, promptText As String,
        Optional types As List(Of String) = Nothing,
        Optional model As String = Model, Optional baseUrl As String = OllamaUrl,
        Optional temperature As Double = 0.1, Optional numCtx As Integer = 8192,
        Optional numPredict As Integer = 1800, Optional timeoutS As Integer = 600) As Task(Of (Constraints As List(Of JsonObject), MetaList As List(Of JsonObject)))

        Dim effectiveTypes = If(types IsNot Nothing AndAlso types.Any(), types, AllTypes)
        Dim allConstraints As New List(Of JsonObject)
        Dim metaList As New List(Of JsonObject)

        For Each constraintType In effectiveTypes
            Dim result = Await ExtractConstraintType(entities, promptText, constraintType, model, baseUrl, temperature, numCtx, numPredict, timeoutS)
            Dim rawConstraints = result.Constraints
            Dim meta = result.Meta

            If Expanders.ContainsKey(constraintType) Then
                Dim expanded As New List(Of JsonObject)
                For Each item In rawConstraints
                    expanded.AddRange(Expanders(constraintType)(entities, item))
                Next
                meta("expanded_to_n_items") = expanded.Count
                allConstraints.AddRange(expanded)
            Else
                allConstraints.AddRange(rawConstraints)
            End If
            metaList.Add(meta)
        Next

        Return (allConstraints, metaList)
    End Function

End Module

