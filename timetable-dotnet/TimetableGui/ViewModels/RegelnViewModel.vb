' Regelverwaltung (gui-ui-konzept.md 6.10): Handregeln editierbar,
' generierte Regeln read-only, dazu der validierte YAML-Expertenmodus.
'
' Die Masken selbst sind Daten (Regeltypen.vb) - hier steht, was mit
' ihnen geschieht: anlegen, vervielfachen, pruefen, exportieren.
Imports System.Text.Json.Nodes
Imports TimetableCore
Imports TimetableProjekt
Imports TimetableYaml

Public NotInheritable Class RegelnViewModel
    Inherits Beobachtbar

    Private ReadOnly _projekt As Projekt
    Private ReadOnly _dialoge As IDialoge
    Private _filterTyp As String = ""
    Private _filterText As String = ""

    Public Sub New(projekt As Projekt, dialoge As IDialoge)
        _projekt = projekt
        _dialoge = dialoge
    End Sub

    Public Event Geaendert As EventHandler

    Private Sub MeldeAenderung()
        RaiseEvent Geaendert(Me, EventArgs.Empty)
        Melde(NameOf(Zusammenfassung))
    End Sub

    ' ===============================================================
    ' Liste
    ' ===============================================================

    Public Property FilterTyp As String
        Get
            Return _filterTyp
        End Get
        Set
            Setze(_filterTyp, If(value, ""))
        End Set
    End Property

    Public Property FilterText As String
        Get
            Return _filterText
        End Get
        Set
            Setze(_filterText, If(value, ""))
        End Set
    End Property

    ''' <summary>Die sichtbaren Handregeln. Generierte stehen NICHT hier -
    ''' sie kommen aus einer eigenen Quelle und duerfen gar nicht erst in
    ''' derselben bearbeitbaren Liste landen.</summary>
    Public Function Handregeln() As List(Of JsonObject)
        Return _projekt.Constraints.
            Where(Function(c) Not Regeltypen.IstGeneriert(c)).
            Where(Function(c) _filterTyp = "" OrElse JsonHelpers.GetString(c, "type") = _filterTyp).
            Where(Function(c) _filterText = "" OrElse
                              Regeltypen.Beschreibe(c).IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0).
            ToList()
    End Function

    ''' <summary>Die Typen, die im Bestand wirklich vorkommen - fuer den
    ''' Typfilter. Eine Liste aller acht waere bei einer Schule mit drei
    ''' Regelarten mehr Auswahl als Hilfe.</summary>
    Public Function VorkommendeTypen() As List(Of String)
        Return _projekt.Constraints.
            Where(Function(c) Not Regeltypen.IstGeneriert(c)).
            Select(Function(c) JsonHelpers.GetString(c, "type")).
            Where(Function(t) t IsNot Nothing).
            Distinct().OrderBy(Function(t) t, StringComparer.Ordinal).ToList()
    End Function

    Public ReadOnly Property Zusammenfassung As String
        Get
            Dim hand = _projekt.Constraints.Where(Function(c) Not Regeltypen.IstGeneriert(c)).Count
            Dim fehler = Pruefe().Count
            Return $"{hand} Handregel(n)" & If(fehler = 0, ", Prüfung grün.", $", {fehler} Hinweis(e).")
        End Get
    End Property

    ' ===============================================================
    ' Anlegen und Vervielfachen
    ' ===============================================================

    ''' <summary>Baut aus einer Maskeneingabe die Regel(n).
    '''
    ''' `werte` traegt die einfachen Felder, `slots` die Rasterauswahl,
    ''' `mehrfach` die Mehrfachauswahlen (Klassen, Raeume, Tage).
    '''
    ''' EINE Eingabe kann MEHRERE Regeln ergeben: forbidden_slot eine je
    ''' Slot, subject_period_window und required_slot eine je Klasse
    ''' (6.10). Das ist kein Komfort, sondern das Format - der Kern kennt
    ''' keine Regel ueber mehrere Klassen.</summary>
    Public Function Baue(typ As String,
                         werte As IDictionary(Of String, String),
                         mehrfach As IDictionary(Of String, List(Of String)),
                         auswahl As RasterAuswahl) As List(Of JsonObject)
        Dim t = Regeltypen.Finde(typ)
        If t Is Nothing Then Return New List(Of JsonObject)

        Dim grund As New JsonObject()
        grund("type") = typ
        For Each f In t.Felder.Where(Function(x) x.Art <> FeldArt.Raster)
            If f.Art = FeldArt.MehrfachRaum OrElse f.Art = FeldArt.MehrfachTag Then
                Dim liste = If(mehrfach IsNot Nothing AndAlso mehrfach.ContainsKey(f.Name), mehrfach(f.Name), Nothing)
                If liste IsNot Nothing AndAlso liste.Count > 0 Then
                    Dim arr As New JsonArray()
                    For Each w In liste
                        arr.Add(JsonValue.Create(w))
                    Next
                    grund(f.Name) = arr
                End If
                Continue For
            End If

            Dim wert = If(werte IsNot Nothing AndAlso werte.ContainsKey(f.Name), werte(f.Name), Nothing)
            If String.IsNullOrWhiteSpace(wert) Then Continue For
            If f.Art = FeldArt.Zahl Then
                Dim n As Integer
                If Integer.TryParse(wert.Trim(), n) Then grund(f.Name) = JsonValue.Create(n)
            Else
                grund(f.Name) = JsonValue.Create(wert.Trim())
            End If
        Next

        Dim ergebnis As New List(Of JsonObject)

        Select Case t.Vervielfacht
            Case "slots"
                ' Eine Regel je Slot - "wie im Beispiel" (6.10).
                If auswahl Is Nothing OrElse auswahl.Anzahl = 0 Then Return ergebnis
                For Each s In auswahl.AlsSlots()
                    Dim c = grund.DeepClone().AsObject()
                    c("day") = JsonValue.Create(s.Tag)
                    c("period") = JsonValue.Create(s.Stunde)
                    ergebnis.Add(c)
                Next

            Case "classes"
                Dim klassen = If(mehrfach IsNot Nothing AndAlso mehrfach.ContainsKey("class"),
                                 mehrfach("class"), New List(Of String))
                If klassen.Count = 0 AndAlso werte IsNot Nothing AndAlso werte.ContainsKey("class") Then
                    klassen = New List(Of String) From {werte("class")}
                End If
                For Each k In klassen.Where(Function(x) Not String.IsNullOrWhiteSpace(x))
                    Dim c = grund.DeepClone().AsObject()
                    c("class") = JsonValue.Create(k)
                    FensterEintragen(c, t, auswahl)
                    ergebnis.Add(c)
                Next

            Case Else
                FensterEintragen(grund, t, auswahl)
                ergebnis.Add(grund)
        End Select

        Return ergebnis
    End Function

    ''' <summary>Traegt ein Rechteck als from_period/to_period ein. Ist
    ''' die Auswahl KEIN Rechteck, geschieht nichts - der Aufrufer hat
    ''' das vorher zu pruefen und den Nutzer zu fragen.</summary>
    Private Shared Sub FensterEintragen(c As JsonObject, t As Regeltyp, auswahl As RasterAuswahl)
        If auswahl Is Nothing Then Return
        If Not t.Felder.Any(Function(f) f.Art = FeldArt.Raster) Then Return
        Dim fenster = auswahl.AlsFenster()
        If Not fenster.HasValue Then Return
        c("from_period") = JsonValue.Create(fenster.Value.Von)
        c("to_period") = JsonValue.Create(fenster.Value.Bis)
        ' Die Tage nur eintragen, wenn nicht ohnehin alle gewaehlt sind -
        ' "Tag nicht gewaehlt = ganz ausserhalb" (6.10), und eine
        ' vollstaendige Liste ist dieselbe Aussage wie keine.
        If fenster.Value.Tage.Count < auswahl.Tage.Count Then
            Dim arr As New JsonArray()
            For Each tag In fenster.Value.Tage
                arr.Add(JsonValue.Create(tag))
            Next
            c("days") = arr
        End If
    End Sub

    Public Sub Hinzufuegen(regeln As IEnumerable(Of JsonObject))
        If regeln Is Nothing Then Return
        For Each c In regeln
            _projekt.Constraints.Add(c)
        Next
        MeldeAenderung()
    End Sub

    Public Sub Entfernen(c As JsonObject)
        If c Is Nothing Then Return
        If Regeltypen.IstGeneriert(c) Then Return   ' strukturell unmoeglich
        _projekt.Constraints.Remove(c)
        MeldeAenderung()
    End Sub

    ''' <summary>Fehlende Pflichtfelder einer Maskeneingabe - in
    ''' Klartext, bevor die Regel entsteht. Die spaetere
    ''' ValidateEntities-Meldung nennt nur Feldnamen.</summary>
    Public Function PflichtfelderFehlen(typ As String,
                                        werte As IDictionary(Of String, String),
                                        mehrfach As IDictionary(Of String, List(Of String)),
                                        auswahl As RasterAuswahl) As List(Of String)
        Dim t = Regeltypen.Finde(typ)
        Dim fehlend As New List(Of String)
        If t Is Nothing Then Return fehlend

        For Each f In t.Felder.Where(Function(x) x.Pflicht)
            Select Case f.Art
                Case FeldArt.Raster
                    If auswahl Is Nothing OrElse auswahl.Anzahl = 0 Then fehlend.Add(f.Beschriftung)
                Case FeldArt.MehrfachRaum, FeldArt.MehrfachTag
                    If mehrfach Is Nothing OrElse Not mehrfach.ContainsKey(f.Name) OrElse
                       mehrfach(f.Name).Count = 0 Then fehlend.Add(f.Beschriftung)
                Case FeldArt.AuswahlKlasse
                    Dim hatMehrfach = mehrfach IsNot Nothing AndAlso mehrfach.ContainsKey("class") AndAlso mehrfach("class").Count > 0
                    Dim hatEinzel = werte IsNot Nothing AndAlso werte.ContainsKey(f.Name) AndAlso
                                    Not String.IsNullOrWhiteSpace(werte(f.Name))
                    If Not hatMehrfach AndAlso Not hatEinzel Then fehlend.Add(f.Beschriftung)
                Case Else
                    If werte Is Nothing OrElse Not werte.ContainsKey(f.Name) OrElse
                       String.IsNullOrWhiteSpace(werte(f.Name)) Then fehlend.Add(f.Beschriftung)
            End Select
        Next
        Return fehlend
    End Function

    ''' <summary>Baut das Modelldokument, das ValidateEntities erwartet -
    ''' genau wie die Pipeline es tut (StundenplanLauf).
    '''
    ''' MIT ABGELEITETER ZUTEILUNGSSCHICHT, und das ist der Punkt:
    ''' ValidateEntities prueft nicht nur, ob Entitaeten existieren,
    ''' sondern auch, ob zu einer Regel ueberhaupt eine
    ''' `teacher_subject_assignment` gehoert - sonst "wuerde die Regel im
    ''' Solver wirkungslos fallengelassen". Diese Schicht entsteht aber
    ''' erst IM LAUF (BuildAssignmentConstraints).
    '''
    ''' Ohne sie meldete die Maske die intakte Grundschul-Beispielschule
    ''' mit 25 Befunden - jede Rhythmisierungsregel angeblich wirkungslos
    ''' (live gemessen). Eine Pruefung, die eine funktionierende Schule
    ''' rot faerbt, wird nach dem zweiten Mal ignoriert.
    '''
    ''' Abgeleitet wird sie aus den Stammdaten: jede (Klasse, Fach)-Paarung
    ''' mit Wochenstunden bekommt eine Zuteilung. WELCHE Lehrkraft, ist
    ''' hier gleichgueltig - gefragt ist nur, ob es die Session ueberhaupt
    ''' geben wird. Das ist keine Vorwegnahme des Lehrereinsatzes, sondern
    ''' die Feststellung, dass die Regel einen Gegenstand hat.
    ''' </summary>
    Private Function Pruefdokument(regeln As IEnumerable(Of JsonObject)) As JsonObject
        Dim b = _projekt.Bestand
        Dim ent = Stammdaten.BuildEntitiesFragment(b)
        Dim alle As New List(Of JsonNode)

        For Each k In b.Klassen
            For Each f In b.Faecher
                Dim fk = f.Klassenstufen.FirstOrDefault(Function(x) x.Klassenstufe = k.Klassenstufe)
                If fk Is Nothing OrElse fk.WochenstundenSoll <= 0 Then Continue For
                Dim lehrer = b.FachLehrerZuordnungen.
                    Where(Function(z) z.FachName = f.Name).
                    Select(Function(z) z.LehrerName).FirstOrDefault()
                If lehrer Is Nothing Then Continue For
                alle.Add(New JsonObject From {
                    {"type", "teacher_subject_assignment"},
                    {"class", k.Name}, {"subject", f.Name}, {"teacher", lehrer}})
                alle.Add(New JsonObject From {
                    {"type", "weekly_hours"},
                    {"class", k.Name}, {"subject", f.Name},
                    {"hours_per_week", fk.WochenstundenSoll}})
            Next
        Next

        ' DeepClone ist Pflicht, nicht Vorsicht: ein JsonNode kann laut
        ' System.Text.Json.Nodes nur GENAU EINEN Parent haben. Ohne die
        ' Kopie haengten die Regeln des Projekts danach im Pruefdokument
        ' statt im Bestand.
        For Each c In regeln
            alle.Add(c.DeepClone())
        Next

        Return New JsonObject From {
            {"entities", ent.DeepClone().AsObject()},
            {"constraints", New JsonArray(alle.ToArray())}
        }
    End Function

    ' ===============================================================
    ' Pruefung
    ' ===============================================================

    ''' <summary>Die Pruefung des KERNS ueber alle Regeln - keine eigene
    ''' Logik (Konzept 1). ValidateEntities ist genau die Stelle, die
    ''' unbekannte Referenzen meldet.</summary>
    Public Function Pruefe() As List(Of String)
        Dim fehler As New List(Of String)
        Try
            fehler.AddRange(Validation.ValidateEntities(Pruefdokument(_projekt.Constraints)))
        Catch ex As Exception
            fehler.Add("Die Regeln liessen sich nicht pruefen: " & ex.Message)
        End Try
        Return fehler
    End Function

    ' ===============================================================
    ' YAML-Expertenmodus
    ' ===============================================================

    ''' <summary>Der volle constraints.yaml-Inhalt - "Masken und Editor
    ''' arbeiten auf demselben Bestand" (6.10). Generierte Regeln stehen
    ''' nicht darin: sie entstehen je Lauf und gehoeren nicht in eine
    ''' Datei, die jemand von Hand pflegt.</summary>
    Public Function AlsYaml() As String
        Return YamlConstraints.SerializeConstraintsYaml(
            _projekt.Constraints.Where(Function(c) Not Regeltypen.IstGeneriert(c)))
    End Function

    ''' <summary>Was beim Uebernehmen des Editorinhalts herauskaeme -
    ''' Syntaxfehler und ValidateEntities-Fehler, BEVOR etwas ersetzt
    ''' wird. Leere Liste heisst: uebernehmbar.</summary>
    Public Function YamlPruefen(yaml As String) As List(Of String)
        Dim fehler As New List(Of String)
        Dim geparst As List(Of JsonObject)
        Try
            geparst = YamlLesen(yaml)
        Catch ex As Exception
            fehler.Add("YAML-Syntax: " & ex.Message)
            Return fehler
        End Try

        Try
            fehler.AddRange(Validation.ValidateEntities(Pruefdokument(geparst)))
        Catch ex As Exception
            fehler.Add("Regelpruefung: " & ex.Message)
        End Try
        Return fehler
    End Function

    Private Shared Function YamlLesen(yaml As String) As List(Of JsonObject)
        ' Ueber eine temporaere Datei, weil YamlConstraints nur den
        ' Dateiweg oeffentlich anbietet. Der Umweg ist unschoen, aber
        ' ehrlicher als eine zweite Parserimplementierung hier.
        Dim pfad = IO.Path.Combine(IO.Path.GetTempPath(), "ttrules-" & Guid.NewGuid().ToString("N") & ".yaml")
        Try
            IO.File.WriteAllText(pfad, If(yaml, ""))
            Return YamlConstraints.LoadConstraintsYaml(pfad)
        Finally
            Try
                IO.File.Delete(pfad)
            Catch
            End Try
        End Try
    End Function

    ''' <summary>Uebernimmt den Editorinhalt. Liefert False, wenn er
    ''' nicht lesbar ist - dann bleibt der Bestand unveraendert.
    '''
    ''' ValidateEntities-Fehler HINDERN NICHT: "Speichern ist immer
    ''' moeglich, Rechnen nur bei gruener Pruefung" (Konzept 1). Ein
    ''' Zwischenstand mit noch unbekannter Referenz muss sich ablegen
    ''' lassen.</summary>
    Public Function YamlUebernehmen(yaml As String) As Boolean
        Dim geparst As List(Of JsonObject)
        Try
            geparst = YamlLesen(yaml)
        Catch
            Return False
        End Try

        _projekt.Constraints.Clear()
        For Each c In geparst
            _projekt.Constraints.Add(c)
        Next
        MeldeAenderung()
        Return True
    End Function

End Class
