' Vorschau und Spaltenzuordnung des Zwischenablage-Imports.
Imports System.Windows.Media

Partial Class ImportDialog

    Private ReadOnly _vorschau As KlassenbildungEingabeViewModel.ImportVorschau
    Private _fuellt As Boolean

    Public Sub New(vorschau As KlassenbildungEingabeViewModel.ImportVorschau)
        InitializeComponent()
        _vorschau = vorschau
        _fuellt = True
        Try
            HatKopfzeile.IsChecked = vorschau.Kopfzeile
            Kopfzeile.Text = $"{vorschau.Zeilen.Count} Zeile(n), {vorschau.Spalten.Count} Spalte(n)" &
                             $", getrennt durch {TrennerName(vorschau.Trenner)}."
            SpaltenwahlFuellen()
        Finally
            _fuellt = False
        End Try
        Zeichnen()
    End Sub

    Private Shared Function TrennerName(t As Char?) As String
        If Not t.HasValue Then Return "nichts (einspaltig)"
        Select Case t.Value
            Case vbTab(0) : Return "Tabulator"
            Case ";"c : Return "Semikolon"
            Case ","c : Return "Komma"
            Case Else : Return "„" & t.Value & """"
        End Select
    End Function

    Public ReadOnly Property NachnameSpalte As Integer
        Get
            Return CInt(NachnameWahl.SelectedIndex) - 1
        End Get
    End Property

    Public ReadOnly Property VornameSpalte As Integer
        Get
            Return CInt(VornameWahl.SelectedIndex) - 1
        End Get
    End Property

    Private Sub SpaltenwahlFuellen()
        For Each wahl In {NachnameWahl, VornameWahl}
            wahl.Items.Clear()
            wahl.Items.Add("(keine)")
            For Each s In _vorschau.Spalten
                wahl.Items.Add(s)
            Next
            wahl.SelectedIndex = 0
        Next
        ' Ein VORSCHLAG, kein Automatismus: erkennt der Text eine
        ' Kopfzeile mit passenden Namen, werden sie vorbelegt - der
        ' Nutzer sieht es und kann es aendern.
        VorschlagenAus("nachname", "name", NachnameWahl)
        VorschlagenAus("vorname", "rufname", VornameWahl)
    End Sub

    Private Sub VorschlagenAus(ParamArray teile As Object())
        Dim ziel = CType(teile(teile.Length - 1), ComboBox)
        For i = 0 To _vorschau.Spalten.Count - 1
            Dim s = _vorschau.Spalten(i).ToLowerInvariant()
            For j = 0 To teile.Length - 2
                If s = CStr(teile(j)) Then
                    ziel.SelectedIndex = i + 1
                    Return
                End If
            Next
        Next
    End Sub

    Private Sub AufKopfzeile(sender As Object, e As RoutedEventArgs)
        If _fuellt Then Return
        _vorschau.Kopfzeile = HatKopfzeile.IsChecked = True
        ' Ohne Kopfzeile gibt es keine Spaltennamen mehr - dann heissen
        ' die Attribute "Spalte 1", "Spalte 2", ...
        _vorschau.Spalten = If(_vorschau.Kopfzeile AndAlso _vorschau.Zeilen.Count > 0,
                               _vorschau.Zeilen(0).ToList(),
                               Enumerable.Range(1, If(_vorschau.Zeilen.Count > 0, _vorschau.Zeilen(0).Length, 0)).
                                   Select(Function(i) $"Spalte {i}").ToList())
        _fuellt = True
        Try
            SpaltenwahlFuellen()
        Finally
            _fuellt = False
        End Try
        Zeichnen()
    End Sub

    Private Sub AufSpaltenwahl(sender As Object, e As SelectionChangedEventArgs)
        If _fuellt Then Return
        Zeichnen()
    End Sub

    Private Sub Zeichnen()
        Kopfzeile.Text = $"{_vorschau.Datensaetze} Kind(er) aus {_vorschau.Zeilen.Count} Zeile(n)" &
                         $", getrennt durch {TrennerName(_vorschau.Trenner)}."

        Dim attribute = _vorschau.Spalten.
            Where(Function(s, i) i <> NachnameSpalte AndAlso i <> VornameSpalte).ToList()
        Attributzeile.Text = If(attribute.Count = 0,
            "Alle Spalten sind als Name zugeordnet - es entstehen keine Attribute.",
            "Wird zu Attributen: " & String.Join(", ", attribute) &
            "   (Namen wandern in die Klarnamen-Tabelle, nicht in die Rechendaten.)")

        Tabelle.Children.Clear()
        Tabelle.ColumnDefinitions.Clear()
        Tabelle.RowDefinitions.Clear()
        If _vorschau.Zeilen.Count = 0 Then Return

        Dim breite = _vorschau.Zeilen(0).Length
        For i = 1 To breite
            Tabelle.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Next

        ' Hoechstens zwoelf Zeilen: die Vorschau soll zeigen, ob die
        ' Zuordnung stimmt, nicht die ganze Liste ersetzen.
        Dim zeigen = _vorschau.Zeilen.Take(12).ToList()
        For z = 0 To zeigen.Count - 1
            Tabelle.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
            Dim istKopf = (z = 0 AndAlso _vorschau.Kopfzeile)
            For s = 0 To breite - 1
                Dim rolle = If(s = NachnameSpalte, "Nachname", If(s = VornameSpalte, "Vorname", ""))
                Dim t As New TextBlock With {
                    .Text = zeigen(z)(s),
                    .Margin = New Thickness(6, 2, 12, 2),
                    .FontSize = 12,
                    .FontWeight = If(istKopf, FontWeights.SemiBold, FontWeights.Normal),
                    .Foreground = CType(FindResource(
                        If(istKopf, "farbe-text-3",
                           If(rolle <> "", "farbe-akzent", "farbe-text"))), Brush)}
                If istKopf Then t.Opacity = 0.8
                Grid.SetRow(t, z)
                Grid.SetColumn(t, s)
                Tabelle.Children.Add(t)
            Next
        Next

        If _vorschau.Zeilen.Count > zeigen.Count Then
            Tabelle.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
            Dim rest As New TextBlock With {
                .Text = $"… und {_vorschau.Zeilen.Count - zeigen.Count} weitere Zeile(n)",
                .Margin = New Thickness(6, 6, 6, 2), .FontSize = 11,
                .Foreground = CType(FindResource("farbe-text-3"), Brush)}
            Grid.SetRow(rest, zeigen.Count)
            Grid.SetColumnSpan(rest, breite)
            Tabelle.Children.Add(rest)
        End If
    End Sub

    Private Sub AufOk(sender As Object, e As RoutedEventArgs)
        DialogResult = True
    End Sub

End Class
