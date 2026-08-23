' Phase 2.18d: laedt die code-freie constraints.yaml (Liste handverfasster
' Ergaenzungsregeln der zweiten Solver-Stufe, siehe tests/README.md) und
' wandelt sie in System.Text.Json.Nodes.JsonObject/JsonArray um - ab dort
' fliessen die Eintraege UNVERAENDERT in die bestehende Validation.vb/
' Solver.vb-Pipeline (siehe Run.vb), kein Code dort aendert sich fuer
' diese neue Eingabequelle.
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports YamlDotNet.Serialization

Public Module YamlConstraints

    Private ReadOnly Deserializer As IDeserializer = New DeserializerBuilder().Build()

    ''' <summary>Bewusst OHNE NamingConvention - anders als bei den
    ''' POCO-Modulen sind die Schluessel hier keine .NET-Propertynamen,
    ''' sondern stehen bereits in ihrer Zielschreibweise im JsonObject
    ''' (`hours_per_week`, `available_days`). Eine Convention wuerde sie
    ''' ein zweites Mal umschreiben.</summary>
    Private ReadOnly Serializer As ISerializer = New SerializerBuilder().Build()

    ''' <summary>Liest eine YAML-Datei mit einer Liste von Constraint-
    ''' Objekten (oberste Ebene MUSS eine Sequenz sein - je ein Mapping pro
    ''' Constraint, gleiche Feldnamen wie in
    ''' docs/json-constraints-reference.md). Eine leere/nicht vorhandene
    ''' Datei liefert eine leere Liste (eine Schule ohne zusaetzliche
    ''' Regeln braucht keine leeren Platzhalter-Eintraege).</summary>
    Public Function LoadConstraintsYaml(path As String) As List(Of JsonObject)
        If Not IO.File.Exists(path) Then Return New List(Of JsonObject)
        Dim yaml = IO.File.ReadAllText(path)
        If String.IsNullOrWhiteSpace(yaml) Then Return New List(Of JsonObject)

        Dim raw = Deserializer.Deserialize(Of Object)(yaml)
        If raw Is Nothing Then Return New List(Of JsonObject)

        ' Bewusst NICHT ueber ToJsonNode(raw) + Zerlegen des Ergebnis-
        ' JsonArray: ein JsonNode kann laut System.Text.Json.Nodes nur
        ' GENAU EINEN Parent haben - waere raw als Ganzes erst in ein
        ' JsonArray konvertiert worden, haetten dessen Kindelemente
        ' bereits DIESES (gleich wieder verworfene) Array als Parent,
        ' und ein erneutes Einfuegen in ein neues JsonArray beim
        ' Aufrufer (Run.vb) wirft "The node already has a parent." (live
        ' entdeckt). Deshalb wird jedes oberste Listenelement einzeln,
        ' ohne je einen Zwischen-Parent zu durchlaufen, konvertiert.
        Dim rawList = TryCast(raw, IList(Of Object))
        If rawList Is Nothing Then
            Throw New InvalidOperationException($"{path}: oberste Ebene muss eine YAML-Liste von Constraint-Objekten sein (- type: ...).")
        End If
        Return rawList.Select(Function(item) CType(ToJsonNode(item), JsonObject)).ToList()
    End Function

    ''' <summary>Gegenstueck zu LoadConstraintsYaml: schreibt die Regeln als
    ''' YAML-Sequenz zurueck. Die Phase-3-GUI pflegt Handregeln in Masken
    ''' (gui-ui-konzept.md 6.10) und muss sie in den `tests/&lt;schule&gt;/`-
    ''' Austauschordner exportieren koennen.
    '''
    ''' EINSCHRAENKUNG, geerbt von der Leseseite und dort dokumentiert: ein
    ''' String, der wie eine Zahl aussieht (`reason: "42"`), kommt beim
    ''' naechsten Laden als Zahl zurueck - ScalarStringToJsonValue kann
    ''' quotiert und unquotiert nicht unterscheiden. Der Schreiber macht
    ''' das nicht schlimmer, kann es aber auch nicht heilen; betroffen sind
    ''' real nur Freitextfelder, die niemand rein numerisch befuellt.</summary>
    Public Function SerializeConstraintsYaml(constraints As IEnumerable(Of JsonObject)) As String
        Dim liste = constraints.Select(Function(c) FromJsonNode(c)).ToList()
        Return Serializer.Serialize(liste)
    End Function

    Public Sub SaveConstraintsYaml(constraints As IEnumerable(Of JsonObject), path As String)
        IO.File.WriteAllText(path, SerializeConstraintsYaml(constraints))
    End Sub

    ''' <summary>Umkehrung von ToJsonNode: JsonNode-Graph zurueck in den
    ''' generischen Objektgraphen, den YamlDotNet serialisieren kann.
    ''' Zahlen werden wie auf der Leseseite Int32 zuerst probiert, damit
    ''' ein gelesenes `period: 6` auch als `6` und nicht als `6.0`
    ''' zurueckgeschrieben wird.</summary>
    Private Function FromJsonNode(node As JsonNode) As Object
        If node Is Nothing Then Return Nothing

        Dim obj = TryCast(node, JsonObject)
        If obj IsNot Nothing Then
            Dim d As New Dictionary(Of String, Object)
            For Each kvp In obj
                d(kvp.Key) = FromJsonNode(kvp.Value)
            Next
            Return d
        End If

        Dim arr = TryCast(node, JsonArray)
        If arr IsNot Nothing Then
            Dim l As New List(Of Object)
            For Each item In arr
                l.Add(FromJsonNode(item))
            Next
            Return l
        End If

        Select Case node.GetValueKind()
            Case JsonValueKind.True
                Return True
            Case JsonValueKind.False
                Return False
            Case JsonValueKind.Number
                Dim asInt As Integer
                If node.AsValue().TryGetValue(Of Integer)(asInt) Then Return asInt
                Dim asLong As Long
                If node.AsValue().TryGetValue(Of Long)(asLong) Then Return asLong
                Return node.GetValue(Of Double)()
            Case Else
                Return node.GetValue(Of String)()
        End Select
    End Function

    ''' <summary>Rekursive Umwandlung des generischen YamlDotNet-Objekt-
    ''' graphen (Dictionary(Of Object, Object)/List(Of Object)/Skalare) in
    ''' den aequivalenten JsonNode-Graphen.</summary>
    Private Function ToJsonNode(value As Object) As JsonNode
        If value Is Nothing Then Return Nothing

        Dim dict = TryCast(value, IDictionary(Of Object, Object))
        If dict IsNot Nothing Then
            Dim obj As New JsonObject()
            For Each kvp In dict
                obj(kvp.Key.ToString()) = ToJsonNode(kvp.Value)
            Next
            Return obj
        End If

        Dim list = TryCast(value, IList(Of Object))
        If list IsNot Nothing Then
            Dim arr As New JsonArray()
            For Each item In list
                arr.Add(ToJsonNode(item))
            Next
            Return arr
        End If

        Select Case True
            Case TypeOf value Is String : Return ScalarStringToJsonValue(CStr(value))
            Case TypeOf value Is Boolean : Return JsonValue.Create(CBool(value))
            Case TypeOf value Is Integer : Return JsonValue.Create(CInt(value))
            Case TypeOf value Is Long : Return JsonValue.Create(CLng(value))
            Case TypeOf value Is Double : Return JsonValue.Create(CDbl(value))
            Case Else : Return JsonValue.Create(value.ToString())
        End Select
    End Function

    ''' <summary>Live entdeckt (Phase 2.18d): YamlDotNets generischer
    ''' `Deserialize(Of Object)`-Pfad liefert JEDEN Skalar als String
    ''' zurueck (auch unquotiertes `period: 6`) - ohne diese Nachbearbeitung
    ''' wuerden Zahlenfelder wie `period`/`hours_per_week`/`max_per_day`/
    ''' `block_length` als JSON-Strings statt Zahlen ankommen und
    ''' `JsonHelpers.GetInt` wuerfe eine Ausnahme (live bestaetigt). Bildet
    ''' deshalb die uebliche YAML-1.1-Skalar-Typinferenz von Hand nach:
    ''' Ganzzahl -&gt; Kommazahl -&gt; Bool ("true"/"false") -&gt; sonst String.
    ''' Einzige bekannte Einschraenkung (dokumentiert in tests/README.md):
    ''' quotierte vs. unquotierte Schreibweise ist auf dieser Ebene nicht
    ''' mehr unterscheidbar, ein rein numerisch aussehender String wird
    ''' deshalb IMMER als Zahl interpretiert.</summary>
    Private Function ScalarStringToJsonValue(s As String) As JsonNode
        ' Int32 zuerst (nicht Int64): System.Text.Json.Nodes.JsonValue(Of T).
        ' GetValue(Of T) rundet/weitet NICHT zwischen numerischen CLR-Typen
        ' (live entdeckt: ein per Int64 erzeugter JsonValue liess
        ' JsonHelpers.GetInt's `GetValue(Of Integer)` mit einer
        ' InvalidOperationException scheitern) - alle Ganzzahlfelder in
        ' diesem Projekt (period, hours_per_week, max_per_day,
        ' block_length, student_count) sind ohnehin klein genug fuer
        ' Int32; Int64 bleibt nur als Fallback fuer den unrealistischen
        ' Ueberlauf-Fall.
        Dim asInt As Integer
        If Integer.TryParse(s, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, asInt) Then
            Return JsonValue.Create(asInt)
        End If
        Dim asLong As Long
        If Long.TryParse(s, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, asLong) Then
            Return JsonValue.Create(asLong)
        End If
        Dim asDouble As Double
        If Double.TryParse(s, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, asDouble) Then
            Return JsonValue.Create(asDouble)
        End If
        If String.Equals(s, "true", StringComparison.OrdinalIgnoreCase) Then Return JsonValue.Create(True)
        If String.Equals(s, "false", StringComparison.OrdinalIgnoreCase) Then Return JsonValue.Create(False)
        Return JsonValue.Create(s)
    End Function

End Module
