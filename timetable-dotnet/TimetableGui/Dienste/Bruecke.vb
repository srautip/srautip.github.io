' Das Nachrichtenprotokoll zwischen Viewer und Host (U5, Stufe E).
'
' Weder gui-ui-konzept.md noch gui-datenhaltung-konzept.md legen ein
' Schema fest - sie nennen nur das Transportmittel
' (window.chrome.webview.postMessage) und die fachlichen Nutzlasten. Das
' Format entsteht deshalb hier und ist bewusst VERSIONIERT: die Vorlagen
' sind Embedded Resources, ein Host laeuft also immer mit "seiner"
' Version - aber eine als Artifact veroeffentlichte Seite (CLAUDE.md)
' kann aelter sein.
'
' Diese Datei kennt WebView2 nicht. Sie uebersetzt nur Zeichenketten in
' Absichten, damit das Protokoll ohne Browser pruefbar bleibt.
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports TimetableCore

Public NotInheritable Class BrueckenNachricht
    Public Const AktuelleVersion As Integer = 1

    Public Property Version As Integer
    Public Property Typ As String = ""
    Public Property Nutzlast As JsonObject

    ''' <summary>Liest eine Nachricht. Liefert Nothing bei allem, was
    ''' nicht dem Umschlag entspricht - eine eingebettete Seite ist zwar
    ''' vertrauenswuerdig, aber ein Host, der auf beliebige Zeichenketten
    ''' mit einer Ausnahme reagiert, ist trotzdem falsch gebaut.</summary>
    Public Shared Function Lesen(json As String) As BrueckenNachricht
        If String.IsNullOrWhiteSpace(json) Then Return Nothing
        Try
            Dim wurzel = TryCast(JsonNode.Parse(json), JsonObject)
            If wurzel Is Nothing Then Return Nothing
            If Not wurzel.ContainsKey("typ") OrElse Not wurzel.ContainsKey("v") Then Return Nothing
            Return New BrueckenNachricht With {
                .Version = wurzel("v").GetValue(Of Integer)(),
                .Typ = wurzel("typ").GetValue(Of String)(),
                .Nutzlast = TryCast(wurzel("nutzlast"), JsonObject)
            }
        Catch ex As JsonException
            Return Nothing
        Catch ex As InvalidOperationException
            Return Nothing
        End Try
    End Function
End Class

Public Module Bruecke

    ''' <summary>Das Skript, das der Host VOR dem Laden injiziert
    ''' (CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync). Es setzt
    ''' nur zwei Variablen; die Feature-Erkennung der Vorlage haengt an
    ''' window.chrome.webview, das WebView2 selbst stellt.
    '''
    ''' Die Anzeige-Map ist der EINZIGE Weg, auf dem Klarnamen in die
    ''' Seite gelangen - sie werden dort ausschliesslich in den DOM
    ''' gerendert (Datenhaltung 6.1/6.2).</summary>
    Public Function StartSkript(zustand As JsonObject, anzeigeNamen As Dictionary(Of String, String),
                                Optional planParameter As JsonObject = Nothing,
                                Optional freigabe As JsonObject = Nothing) As String
        Dim namen As New JsonObject()
        If anzeigeNamen IsNot Nothing Then
            For Each kvp In anzeigeNamen
                namen(kvp.Key) = kvp.Value
            Next
        End If
        ' Die Plan-Kurzparameter sind fuer das Stundentafel-Dashboard;
        ' das Board ignoriert die Variable. Ein zweites Startskript je
        ' Seite waere eine Fallunterscheidung ohne Gegenwert.
        Return $"window.__gastZustand = {If(zustand Is Nothing, "null", zustand.ToJsonString())};" &
               $"window.__anzeigeNamen = {namen.ToJsonString()};" &
               $"window.__planParameter = {If(planParameter Is Nothing, "null", planParameter.ToJsonString())};" &
               $"window.__freigabe = {If(freigabe Is Nothing, "null", freigabe.ToJsonString())};"
    End Function

    ''' <summary>Uebersetzt die Fixierungsliste der Bruecke in
    ''' Kern-Objekte. Die Feldnamen sind absichtlich identisch zum YAML
    ''' (kind/klasse/nicht_klasse), deshalb ist das hier eine reine
    ''' Umformung ohne Namenszuordnung.</summary>
    Public Function LiesFixierungen(nutzlast As JsonObject) As List(Of KlassenbildungFixierung)
        Dim liste As New List(Of KlassenbildungFixierung)
        If nutzlast Is Nothing Then Return liste
        Dim arr = TryCast(nutzlast("fixierungen"), JsonArray)
        If arr Is Nothing Then Return liste

        For Each eintrag In arr
            Dim o = TryCast(eintrag, JsonObject)
            If o Is Nothing OrElse Not o.ContainsKey("kind") Then Continue For
            Dim f As New KlassenbildungFixierung With {.Kind = o("kind").GetValue(Of String)()}
            If o.ContainsKey("klasse") AndAlso o("klasse") IsNot Nothing Then
                f.Klasse = o("klasse").GetValue(Of Integer)()
            ElseIf o.ContainsKey("nicht_klasse") AndAlso o("nicht_klasse") IsNot Nothing Then
                f.NichtKlasse = o("nicht_klasse").GetValue(Of Integer)()
            Else
                ' Weder klasse noch nicht_klasse - das waere eine
                ' bedeutungslose Fixierung, die der Validierung spaeter um
                ' die Ohren flaege.
                Continue For
            End If
            liste.Add(f)
        Next
        Return liste
    End Function

    ''' <summary>Setzt die im Board gehaerteten Regeln (F5 fuer Gruppen,
    ''' F3 fuer Wuensche) auf `modus: hard`. Liefert die Zahl der
    ''' tatsaechlich geaenderten Regeln - die Oberflaeche meldet sie, statt
    ''' still zu wirken.</summary>
    Public Function WendeHaertungenAn(nutzlast As JsonObject, eingabe As KlassenbildungInput) As Integer
        If nutzlast Is Nothing OrElse eingabe Is Nothing Then Return 0
        Dim h = TryCast(nutzlast("haertungen"), JsonObject)
        If h Is Nothing Then Return 0
        Dim geaendert = 0

        Dim gruppen = TryCast(h("gruppen"), JsonObject)
        If gruppen IsNot Nothing Then
            For Each kvp In gruppen
                Dim g = eingabe.Gruppen.FirstOrDefault(Function(x) x.Id = kvp.Key)
                If g IsNot Nothing AndAlso g.Modus <> "hard" Then
                    g.Modus = "hard"
                    geaendert += 1
                End If
            Next
        End If

        Dim wuensche = TryCast(h("wuensche"), JsonObject)
        If wuensche IsNot Nothing Then
            For Each kvp In wuensche
                Dim index As Integer
                If Not Integer.TryParse(kvp.Key, index) Then Continue For
                If index < 0 OrElse index >= eingabe.Wuensche.Count Then Continue For
                If eingabe.Wuensche(index).Modus <> "hard" Then
                    eingabe.Wuensche(index).Modus = "hard"
                    geaendert += 1
                End If
            Next
        End If

        Return geaendert
    End Function


    ''' <summary>Die Kurz-Parameter des Stundentafel-Dashboards (5): mehr
    ''' als Zeitbudget und Loesungszahl gibt es dort bewusst NICHT - die
    ''' Feinsteuerung bleibt in den Solver-Einstellungen (6.12), sonst
    ''' entstuenden zwei Orte fuer dieselbe Einstellung.
    '''
    ''' Unplausible Werte werden GEKAPPT statt abgewiesen: die Seite ist
    ''' HTML, ein manipuliertes Feld darf hoechstens eine dumme
    ''' Einstellung ergeben, nie einen Absturz oder einen Lauf ohne Ende.
    ''' Nothing heisst "nichts mitgegeben" - dann gilt die Projekt-Config
    ''' unveraendert.</summary>
    Public Function LiesKurzparameter(nutzlast As JsonObject) As (Zeitbudget As Double?, MaxLoesungen As Integer?)
        If nutzlast Is Nothing Then Return (Nothing, Nothing)

        Dim budget As Double? = Nothing
        Dim z = Zahl(nutzlast, "zeitbudget_s")
        If z.HasValue Then budget = Math.Min(3600.0, Math.Max(1.0, z.Value))

        Dim anzahl As Integer? = Nothing
        Dim n = Zahl(nutzlast, "max_loesungen")
        If n.HasValue Then anzahl = CInt(Math.Min(200.0, Math.Max(1.0, n.Value)))

        Return (budget, anzahl)
    End Function

    ''' <summary>Welche Loesung das Dashboard als Arbeitsstand markiert
    ''' hat. Beides 1-basiert, wie in der Loesungsuebersicht angezeigt -
    ''' eine 0-basierte Bruecke waere die Sorte Detail, die man beim
    ''' Debuggen dreimal falsch herum liest.</summary>
    Public Function LiesPlanAuswahl(nutzlast As JsonObject) As (Zuteilung As Integer, Loesung As Integer)?
        If nutzlast Is Nothing Then Return Nothing
        Dim zut = Zahl(nutzlast, "zuteilung")
        Dim los = Zahl(nutzlast, "loesung")
        If Not zut.HasValue OrElse Not los.HasValue Then Return Nothing
        If zut.Value < 1 OrElse los.Value < 1 Then Return Nothing
        Return (CInt(zut.Value), CInt(los.Value))
    End Function

    ''' <summary>Zahl aus dem JSON, egal ob sie als Zahl oder als
    ''' Zeichenkette ankommt - ein `value="12"` aus einem Eingabefeld ist
    ''' der Normalfall, kein Sonderfall.</summary>
    Private Function Zahl(o As JsonObject, schluessel As String) As Double?
        If Not o.ContainsKey(schluessel) OrElse o(schluessel) Is Nothing Then Return Nothing
        Try
            Return o(schluessel).GetValue(Of Double)()
        Catch ex As InvalidOperationException
            Dim text As String = Nothing
            Try
                text = o(schluessel).GetValue(Of String)()
            Catch ex2 As InvalidOperationException
                Return Nothing
            End Try
            Dim d As Double
            If Double.TryParse(text, Globalization.NumberStyles.Any,
                               Globalization.CultureInfo.InvariantCulture, d) Then Return d
            Return Nothing
        Catch ex As FormatException
            Return Nothing
        End Try
    End Function

End Module
