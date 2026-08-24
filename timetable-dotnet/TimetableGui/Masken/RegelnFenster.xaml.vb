' Code-Behind der Regelverwaltung. Baut die acht Masken aus
' Regeltypen.vb; alle Entscheidungen liegen in RegelnViewModel und sind
' dort ohne Fenster geprueft.
Imports System.Text.Json.Nodes
Imports System.Windows.Media
Imports TimetableCore
Imports TimetableProjekt

Partial Class RegelnFenster

    Private ReadOnly _projekt As Projekt
    Private ReadOnly _dialoge As IDialoge
    Private ReadOnly _modell As RegelnViewModel
    Private _fuellt As Boolean

    ' Der aktuelle Maskenzustand. `_bearbeitet` ist Nothing, solange eine
    ' NEUE Regel entsteht - dann legt "Anlegen" an; sonst schreiben die
    ' Felder direkt in die gewaehlte Regel.
    Private _typ As Regeltyp
    Private _bearbeitet As JsonObject
    Private _werte As New Dictionary(Of String, String)(StringComparer.Ordinal)
    Private _mehrfach As New Dictionary(Of String, List(Of String))(StringComparer.Ordinal)
    Private _auswahl As RasterAuswahl

    Public Event Geaendert As EventHandler

    Public Sub New(projekt As Projekt, dialoge As IDialoge)
        InitializeComponent()
        _projekt = projekt
        _dialoge = dialoge
        _modell = New RegelnViewModel(projekt, dialoge)
        AddHandler _modell.Geaendert, Sub() RaiseEvent Geaendert(Me, EventArgs.Empty)

        _fuellt = True
        Try
            TypFilter.Items.Add("(alle Typen)")
            For Each t In _modell.VorkommendeTypen()
                TypFilter.Items.Add(t)
            Next
            TypFilter.SelectedIndex = 0
            For Each t In Regeltypen.Alle
                NeuTyp.Items.Add(t.Titel)
            Next
            NeuTyp.SelectedIndex = 0
        Finally
            _fuellt = False
        End Try

        ListeFuellen()
        GenerierteFuellen()
        YamlEditor.Text = _modell.AlsYaml()
        MaskeLeeren()
        Aktualisieren()
    End Sub

    ' ===============================================================
    ' Liste
    ' ===============================================================

    Private _sichtbar As New List(Of JsonObject)

    Private Sub ListeFuellen()
        _sichtbar = _modell.Handregeln()
        Liste.Items.Clear()
        For Each c In _sichtbar
            Liste.Items.Add(Regeltypen.Beschreibe(c))
        Next
    End Sub

    Private Sub GenerierteFuellen()
        Generierte.Items.Clear()
        Dim erzeugt = _projekt.Constraints.Where(AddressOf Regeltypen.IstGeneriert).ToList()
        If erzeugt.Count = 0 Then
            ' Ehrlich statt leer: sie entstehen erst im Lauf und stehen
            ' deshalb normalerweise gar nicht im Bestand.
            Generierte.Items.Add("(keine – sie entstehen bei jedem Lauf neu und werden nicht gespeichert)")
            Return
        End If
        For Each c In erzeugt
            Generierte.Items.Add(Regeltypen.Beschreibe(c))
        Next
    End Sub

    Private Sub AufFilter(sender As Object, e As RoutedEventArgs)
        If _fuellt Then Return
        _modell.FilterTyp = If(TypFilter.SelectedIndex <= 0, "", CStr(TypFilter.SelectedItem))
        _modell.FilterText = TextFilter.Text
        ListeFuellen()
        Aktualisieren()
    End Sub

    Private Sub AufAuswahl(sender As Object, e As SelectionChangedEventArgs)
        If _fuellt Then Return
        Dim i = Liste.SelectedIndex
        If i < 0 OrElse i >= _sichtbar.Count Then Return
        MaskeFuerRegel(_sichtbar(i))
    End Sub

    Private Sub Aktualisieren()
        Statuszeile.Text = _modell.Zusammenfassung
    End Sub

    ' ===============================================================
    ' Maske bauen
    ' ===============================================================

    Private Sub MaskeLeeren()
        Maske.Children.Clear()
        Maskenkopf.Text = "Neue Regel"
        Maskenhinweis.Text = "Typ oben wählen und „Neu"" drücken."
        Maskenfuss.Visibility = Visibility.Collapsed
        _typ = Nothing
        _bearbeitet = Nothing
    End Sub

    Private Sub AufNeu(sender As Object, e As RoutedEventArgs)
        Dim t = Regeltypen.Alle(Math.Max(0, NeuTyp.SelectedIndex))
        _bearbeitet = Nothing
        MaskeBauen(t)
        Liste.SelectedIndex = -1
    End Sub

    Private Sub MaskeFuerRegel(c As JsonObject)
        Dim t = Regeltypen.Finde(JsonHelpers.GetString(c, "type"))
        If t Is Nothing Then
            Maske.Children.Clear()
            Maskenkopf.Text = JsonHelpers.GetString(c, "type")
            Maskenhinweis.Text = "Für diesen Typ gibt es keine Maske – im YAML-Expertenmodus bearbeitbar."
            Maskenfuss.Visibility = Visibility.Collapsed
            Return
        End If
        _bearbeitet = c
        MaskeBauen(t)
    End Sub

    Private Sub MaskeBauen(t As Regeltyp)
        _typ = t
        _werte.Clear()
        _mehrfach.Clear()
        _auswahl = New RasterAuswahl(_projekt.Bestand.Tage, _projekt.Bestand.PeriodsPerDay)

        Maskenkopf.Text = If(_bearbeitet Is Nothing, "Neu: " & t.Titel, t.Titel)
        Maskenhinweis.Text = t.Bemerkung
        Maskenfuss.Visibility = If(_bearbeitet Is Nothing, Visibility.Visible, Visibility.Collapsed)
        Anlegenhinweis.Text = ""

        Maske.Children.Clear()
        For Each feld In t.Felder
            Dim f = feld
            Maske.Children.Add(Beschriftung(f))
            Select Case f.Art
                Case FeldArt.Raster
                    Maske.Children.Add(RasterBauen(f))
                Case FeldArt.MehrfachRaum
                    Maske.Children.Add(MehrfachBauen(f, _projekt.Bestand.Raeume.Select(Function(r) r.Name)))
                Case FeldArt.MehrfachTag
                    Maske.Children.Add(MehrfachBauen(f, _projekt.Bestand.Tage))
                Case FeldArt.AuswahlKlasse
                    ' Bei "Neu" mehrfach (eine Regel je Klasse), beim
                    ' Bearbeiten einer bestehenden Regel einzeln.
                    If _bearbeitet Is Nothing AndAlso t.Vervielfacht = "classes" Then
                        Maske.Children.Add(MehrfachBauen(f, _projekt.Bestand.Klassen.Select(Function(k) k.Name)))
                    Else
                        Maske.Children.Add(AuswahlBauen(f, _projekt.Bestand.Klassen.Select(Function(k) k.Name)))
                    End If
                Case FeldArt.AuswahlLehrkraft
                    Maske.Children.Add(AuswahlBauen(f, _projekt.Bestand.Lehrkraefte.Select(Function(l) l.Name)))
                Case FeldArt.AuswahlFach
                    Maske.Children.Add(AuswahlBauen(f, _projekt.Bestand.Faecher.Select(Function(x) x.Name)))
                Case FeldArt.AuswahlRaum
                    Maske.Children.Add(AuswahlBauen(f, _projekt.Bestand.Raeume.Select(Function(r) r.Name)))
                Case FeldArt.AuswahlKlasseOderLehrkraft
                    Maske.Children.Add(AuswahlBauen(f, Betroffene(False)))
                Case FeldArt.AuswahlKlasseOderLehrkraftOderRaum
                    Maske.Children.Add(AuswahlBauen(f, Betroffene(True)))
                Case FeldArt.Prio
                    Maske.Children.Add(AuswahlBauen(f, {"must", "should"}, erlaubtLeer:=True))
                Case FeldArt.Zahl, FeldArt.Text
                    ' `scope` und `day` sind zwar Text, haben aber feste
                    ' Werte - eine Auswahlliste verhindert Tippfehler, die
                    ' sonst erst der Solver bemerkt.
                    If f.Name = "scope" Then
                        Dim werte = If(_typ.Typ = "forbidden_slot", {"class", "teacher", "room"}, {"class", "teacher"})
                        Maske.Children.Add(AuswahlBauen(f, werte))
                    ElseIf f.Name = "day" Then
                        Maske.Children.Add(AuswahlBauen(f, _projekt.Bestand.Tage))
                    Else
                        Maske.Children.Add(TextBauen(f))
                    End If
            End Select
        Next
    End Sub

    Private Function Betroffene(mitRaum As Boolean) As IEnumerable(Of String)
        Dim liste = _projekt.Bestand.Klassen.Select(Function(k) k.Name).
            Concat(_projekt.Bestand.Lehrkraefte.Select(Function(l) l.Name))
        If mitRaum Then liste = liste.Concat(_projekt.Bestand.Raeume.Select(Function(r) r.Name))
        Return liste.Distinct()
    End Function

    Private Function Beschriftung(f As Regelfeld) As UIElement
        Dim s As New StackPanel With {.Margin = New Thickness(0, 10, 0, 2)}
        s.Children.Add(New TextBlock With {
            .Text = f.Beschriftung & If(f.Pflicht, " *", ""),
            .FontSize = 12, .Foreground = CType(FindResource("farbe-text-2"), Brush)})
        If f.Hilfe <> "" Then
            s.Children.Add(New TextBlock With {
                .Text = f.Hilfe, .TextWrapping = TextWrapping.Wrap, .MaxWidth = 560,
                .FontSize = 11, .Foreground = CType(FindResource("farbe-text-3"), Brush)})
        End If
        Return s
    End Function

    Private Function Bestandswert(f As Regelfeld) As String
        If _bearbeitet Is Nothing OrElse Not _bearbeitet.ContainsKey(f.Name) Then Return ""
        Dim knoten = _bearbeitet(f.Name)
        If knoten Is Nothing Then Return ""
        Return knoten.ToString()
    End Function

    Private Sub Uebernehmen(f As Regelfeld, wert As String)
        _werte(f.Name) = wert
        If _bearbeitet Is Nothing Then Return
        ' Bearbeiten schreibt SOFORT in die Regel - es gibt keinen
        ' Zwischenstand, den man verlieren koennte.
        If String.IsNullOrWhiteSpace(wert) Then
            _bearbeitet.Remove(f.Name)
        ElseIf f.Art = FeldArt.Zahl Then
            Dim n As Integer
            If Integer.TryParse(wert.Trim(), n) Then _bearbeitet(f.Name) = JsonValue.Create(n)
        Else
            _bearbeitet(f.Name) = JsonValue.Create(wert.Trim())
        End If
        RaiseEvent Geaendert(Me, EventArgs.Empty)
        ListeAktualisierenBehutsam()
        Aktualisieren()
    End Sub

    ''' <summary>Liste neu beschriften, ohne die Auswahl zu verlieren -
    ''' sonst springt die Maske beim Tippen weg.</summary>
    Private Sub ListeAktualisierenBehutsam()
        Dim i = Liste.SelectedIndex
        _fuellt = True
        Try
            For j = 0 To Math.Min(_sichtbar.Count, Liste.Items.Count) - 1
                Liste.Items(j) = Regeltypen.Beschreibe(_sichtbar(j))
            Next
            If i >= 0 AndAlso i < Liste.Items.Count Then Liste.SelectedIndex = i
        Finally
            _fuellt = False
        End Try
    End Sub

    Private Function TextBauen(f As Regelfeld) As UIElement
        Dim t As New TextBox With {.Text = Bestandswert(f), .MaxWidth = 420, .HorizontalAlignment = HorizontalAlignment.Left}
        If f.Art = FeldArt.Zahl Then t.Width = 100
        If f.Name = "reason" Then t.MinWidth = 420
        AddHandler t.LostFocus, Sub() Uebernehmen(f, t.Text)
        Return t
    End Function

    Private Function AuswahlBauen(f As Regelfeld, werte As IEnumerable(Of String),
                                  Optional erlaubtLeer As Boolean = False) As UIElement
        Dim c As New ComboBox With {.MaxWidth = 420, .HorizontalAlignment = HorizontalAlignment.Left, .MinWidth = 220}
        If Not f.Pflicht OrElse erlaubtLeer Then c.Items.Add("")
        For Each w In werte
            c.Items.Add(w)
        Next
        Dim bestand = Bestandswert(f)
        If bestand <> "" AndAlso c.Items.Contains(bestand) Then c.SelectedItem = bestand
        AddHandler c.SelectionChanged, Sub() Uebernehmen(f, CStr(If(c.SelectedItem, "")))
        Return c
    End Function

    Private Function MehrfachBauen(f As Regelfeld, werte As IEnumerable(Of String)) As UIElement
        Dim gewaehlt As New List(Of String)
        If _bearbeitet IsNot Nothing AndAlso _bearbeitet.ContainsKey(f.Name) AndAlso _bearbeitet(f.Name) IsNot Nothing Then
            gewaehlt = JsonHelpers.AsStringList(_bearbeitet(f.Name))
        End If
        _mehrfach(f.Name) = gewaehlt

        Dim s As New WrapPanel With {.MaxWidth = 560}
        For Each wert In werte
            Dim w = wert
            Dim cb As New CheckBox With {
                .Content = w, .Margin = New Thickness(0, 2, 16, 2), .IsChecked = gewaehlt.Contains(w)}
            AddHandler cb.Click,
                Sub()
                    If cb.IsChecked = True Then
                        If Not _mehrfach(f.Name).Contains(w) Then _mehrfach(f.Name).Add(w)
                    Else
                        _mehrfach(f.Name).Remove(w)
                    End If
                    If _bearbeitet IsNot Nothing Then
                        Dim arr As New JsonArray()
                        For Each x In _mehrfach(f.Name)
                            arr.Add(JsonValue.Create(x))
                        Next
                        If _mehrfach(f.Name).Count = 0 Then _bearbeitet.Remove(f.Name) Else _bearbeitet(f.Name) = arr
                        RaiseEvent Geaendert(Me, EventArgs.Empty)
                        ListeAktualisierenBehutsam()
                        Aktualisieren()
                    End If
                End Sub
            s.Children.Add(cb)
        Next
        Return s
    End Function

    Private Function RasterBauen(f As Regelfeld) As UIElement
        ' Bei einer bestehenden Regel die vorhandene Lage zeigen.
        If _bearbeitet IsNot Nothing Then
            Dim tag = JsonHelpers.GetString(_bearbeitet, "day")
            If tag IsNot Nothing AndAlso _bearbeitet.ContainsKey("period") Then
                _auswahl.Setze(tag, JsonHelpers.GetInt(_bearbeitet, "period"), True)
            ElseIf _bearbeitet.ContainsKey("from_period") Then
                Dim von = JsonHelpers.GetInt(_bearbeitet, "from_period")
                Dim bis = JsonHelpers.GetInt(_bearbeitet, "to_period")
                Dim tage = If(_bearbeitet.ContainsKey("days") AndAlso _bearbeitet("days") IsNot Nothing,
                              JsonHelpers.AsStringList(_bearbeitet("days")), _projekt.Bestand.Tage)
                For Each d In tage
                    For p = von To bis
                        _auswahl.Setze(d, p, True)
                    Next
                Next
            End If
        End If

        Dim picker As New Rasterpicker With {.Auswahl = _auswahl, .HorizontalAlignment = HorizontalAlignment.Left}
        AddHandler picker.AuswahlGeaendert,
            Sub()
                If _bearbeitet Is Nothing Then Return
                ' Eine bestehende Regel per Raster zu veraendern hiesse,
                ' aus einer Regel mehrere zu machen - dafuer gibt es
                ' "Neu". Hier bleibt die Anzeige stehen.
                Anlegenhinweis.Text = "Zum Ändern der Slots eine neue Regel anlegen und die alte löschen."
            End Sub
        Return picker
    End Function

    ' ===============================================================
    ' Anlegen und Loeschen
    ' ===============================================================

    Private Sub AufAnlegen(sender As Object, e As RoutedEventArgs)
        If _typ Is Nothing Then Return

        Dim fehlend = _modell.PflichtfelderFehlen(_typ.Typ, _werte, _mehrfach, _auswahl)
        If fehlend.Count > 0 Then
            _dialoge.Hinweis("Unvollständig", "Es fehlen: " & String.Join(", ", fehlend))
            Return
        End If

        ' Fenster-Typen brauchen ein Rechteck. Das still auf die Huelle
        ' zu runden waere eine Regel, die niemand gemeint hat - also
        ' nachfragen.
        If _typ.Felder.Any(Function(f) f.Art = FeldArt.Raster) AndAlso _typ.Vervielfacht <> "slots" Then
            If Not _auswahl.AlsFenster().HasValue Then
                _dialoge.Hinweis("Kein Fenster",
                    "Die Auswahl ist kein zusammenhängendes Rechteck. Ein Zeitfenster kennt nur ein " &
                    "von/bis – bitte die Auswahl begradigen.")
                Return
            End If
        End If

        Dim regeln = _modell.Baue(_typ.Typ, _werte, _mehrfach, _auswahl)
        If regeln.Count = 0 Then
            _dialoge.Hinweis("Nichts angelegt", "Aus der Eingabe ließ sich keine Regel bilden.")
            Return
        End If

        _modell.Hinzufuegen(regeln)
        ListeFuellen()
        Aktualisieren()
        YamlEditor.Text = _modell.AlsYaml()
        Anlegenhinweis.Text = If(regeln.Count = 1, "1 Regel angelegt.", $"{regeln.Count} Regeln angelegt.")
    End Sub

    Private Sub AufLoeschen(sender As Object, e As RoutedEventArgs)
        Dim i = Liste.SelectedIndex
        If i < 0 OrElse i >= _sichtbar.Count Then Return
        Dim c = _sichtbar(i)
        If Not _dialoge.Frage("Löschen", "Diese Regel löschen?" & vbLf & vbLf & Regeltypen.Beschreibe(c)) Then Return
        _modell.Entfernen(c)
        ListeFuellen()
        MaskeLeeren()
        Aktualisieren()
        YamlEditor.Text = _modell.AlsYaml()
    End Sub

    ' ===============================================================
    ' YAML-Expertenmodus
    ' ===============================================================

    Private Sub AufReiterWechsel(sender As Object, e As SelectionChangedEventArgs)
        If Not ReferenceEquals(e.OriginalSource, Reiter) Then Return
        Dim kopf = TryCast(Reiter.SelectedItem, TabItem)
        If kopf Is Nothing Then Return
        If CStr(kopf.Header) = "YAML-Expertenmodus" Then
            ' Beim Betreten frisch aus dem Bestand - sonst zeigt der
            ' Editor einen Stand, den die Masken laengst ueberholt haben.
            YamlEditor.Text = _modell.AlsYaml()
            YamlBefunde.Text = ""
        ElseIf CStr(kopf.Header) = "Generierte Regeln" Then
            GenerierteFuellen()
        End If
    End Sub

    Private Sub BefundeZeigen(befunde As List(Of String))
        If befunde.Count = 0 Then
            YamlBefunde.Text = "Keine Beanstandungen."
            YamlBefunde.Foreground = CType(FindResource("farbe-ok-text"), Brush)
            Return
        End If
        YamlBefunde.Text = String.Join(vbLf, befunde)
        YamlBefunde.Foreground = CType(FindResource(
            If(befunde.Any(Function(f) f.StartsWith("YAML-Syntax")), "farbe-krit-text", "farbe-warn-text")), Brush)
    End Sub

    Private Sub AufYamlPruefen(sender As Object, e As RoutedEventArgs)
        BefundeZeigen(_modell.YamlPruefen(YamlEditor.Text))
    End Sub

    Private Sub AufYamlUebernehmen(sender As Object, e As RoutedEventArgs)
        Dim befunde = _modell.YamlPruefen(YamlEditor.Text)
        BefundeZeigen(befunde)
        If befunde.Any(Function(f) f.StartsWith("YAML-Syntax")) Then
            _dialoge.Hinweis("Nicht übernommen",
                "Der Text ist nicht lesbar. Der Bestand bleibt unverändert.")
            Return
        End If
        ' Referenzfehler hindern NICHT: "Speichern ist immer möglich,
        ' Rechnen nur bei grüner Prüfung" (Konzept 1).
        If Not _modell.YamlUebernehmen(YamlEditor.Text) Then
            _dialoge.Hinweis("Nicht übernommen", "Der Text ließ sich nicht lesen.")
            Return
        End If
        ListeFuellen()
        GenerierteFuellen()
        MaskeLeeren()
        Aktualisieren()
    End Sub

    Private Sub AufYamlVerwerfen(sender As Object, e As RoutedEventArgs)
        YamlEditor.Text = _modell.AlsYaml()
        YamlBefunde.Text = ""
    End Sub

    Private Sub AufPruefen(sender As Object, e As RoutedEventArgs)
        Dim fehler = _modell.Pruefe()
        If fehler.Count = 0 Then
            _dialoge.Hinweis("Prüfung", "Keine Beanstandungen.")
        Else
            _dialoge.Hinweis("Prüfung", String.Join(vbLf, fehler.Take(25)) &
                             If(fehler.Count > 25, vbLf & $"… und {fehler.Count - 25} weitere", ""))
        End If
    End Sub

End Class
