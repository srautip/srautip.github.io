' Vorschau und freie Spalten-Zuordnung des Imports (9.1) - Stufe G5.
'
' Der Dialog entscheidet nichts: was eine Zuordnung bedeutet und was
' ihr fehlt, steht in Spaltenzuordnung.vb und ist dort ohne Fenster
' geprueft.
Imports System.Windows.Media
Imports TimetableCore

Partial Class ImportDialog

    Private ReadOnly _dialoge As IDialoge
    Private _vorschau As KlassenbildungEingabeViewModel.ImportVorschau
    Private ReadOnly _neuLesen As Func(Of String, KlassenbildungEingabeViewModel.ImportVorschau)
    Private ReadOnly _vokabular As List(Of String)
    Private _wahlen As New List(Of Spaltenwahl)
    Private _fuellt As Boolean

    ''' <summary>Die getroffene Zuordnung - das Ergebnis des Dialogs.</summary>
    Public ReadOnly Property Wahlen As List(Of Spaltenwahl)
        Get
            Return _wahlen
        End Get
    End Property

    Public ReadOnly Property Vorschau As KlassenbildungEingabeViewModel.ImportVorschau
        Get
            Return _vorschau
        End Get
    End Property

    ''' <summary>`neuLesen` zerlegt einen Text zu einer Vorschau - noetig
    ''' fuer den CSV-Weg, der eine zweite Quelle desselben Formats ist.
    ''' Der Dialog haelt dafuer keine eigene Zerlegung vor; zwei
    ''' Zerleger waeren zwei Meinungen ueber dasselbe Format.</summary>
    Public Sub New(vorschau As KlassenbildungEingabeViewModel.ImportVorschau,
                   Optional dialoge As IDialoge = Nothing,
                   Optional neuLesen As Func(Of String, KlassenbildungEingabeViewModel.ImportVorschau) = Nothing,
                   Optional vokabular As IEnumerable(Of String) = Nothing)
        InitializeComponent()
        _dialoge = dialoge
        _neuLesen = neuLesen
        _vokabular = If(vokabular, Enumerable.Empty(Of String)()).ToList()
        DateiKnopf.Visibility = If(dialoge IsNot Nothing AndAlso neuLesen IsNot Nothing,
                                   Visibility.Visible, Visibility.Collapsed)
        Uebernehmen(vorschau)
    End Sub

    Private Sub Uebernehmen(vorschau As KlassenbildungEingabeViewModel.ImportVorschau)
        _vorschau = vorschau
        _fuellt = True
        Try
            HatKopfzeile.IsChecked = vorschau.Kopfzeile
            Kopfzeile.Text = $"{vorschau.Zeilen.Count} Zeile(n), {vorschau.Spalten.Count} Spalte(n)" &
                             $", getrennt durch {TrennerName(vorschau.Trenner)}."
            _wahlen = Spaltenzuordnung.Vorschlag(vorschau.Spalten)
            ZuordnungBauen()
        Finally
            _fuellt = False
        End Try
        Zeichnen()
        Pruefen()
    End Sub

    Private Shared Function TrennerName(t As Char?) As String
        If Not t.HasValue Then Return "nichts (einspaltig)"
        Select Case t.Value
            Case vbTab(0) : Return "Tabulator"
            Case ";"c : Return "Semikolon"
            Case ","c : Return "Komma"
            Case Else : Return "'" & t.Value & "'"
        End Select
    End Function

    ' ===============================================================
    ' Zuordnung
    ' ===============================================================

    ''' <summary>Ein waehlbarer Eintrag der Rollenliste. Attribute stehen
    ''' NAMENTLICH darin, nicht als Sammelbegriff mit zweitem Feld
    ''' daneben: zwei Listen fuer eine Entscheidung sind eine zu viel
    ''' (Nutzerbefund 01.09.2026). Zur Spalte "Kann-Kind" waehlt man
    ''' direkt "Attribut: Kann-Kind".</summary>
    Private NotInheritable Class Rolleneintrag
        Public Property Rolle As Spaltenrolle
        Public Property Zielname As String = ""
        Public Property Text As String = ""
        Public Overrides Function ToString() As String
            Return Text
        End Function
    End Class

    ''' <summary>Die Liste je Spalte. Sie haengt von der Spalte ab - der
    ''' eigene Spaltenname steht als neu anzulegendes Attribut darin -
    ''' und vom Vokabular des Projekts.</summary>
    Private Function Rollenliste(spalte As String) As List(Of Rolleneintrag)
        Dim liste As New List(Of Rolleneintrag) From {
            New Rolleneintrag With {.Rolle = Spaltenrolle.Verwerfen, .Text = "verwerfen"},
            New Rolleneintrag With {.Rolle = Spaltenrolle.Nachname, .Text = "Nachname"},
            New Rolleneintrag With {.Rolle = Spaltenrolle.Vorname, .Text = "Vorname"}
        }

        ' Das Attribut mit dem Spaltennamen zuerst - es ist der Regelfall.
        ' Faellt es mit einem vorhandenen zusammen, steht es nur einmal da.
        Dim schonDa = _vokabular.Any(
            Function(v) String.Equals(v, spalte, StringComparison.CurrentCultureIgnoreCase))
        If Not schonDa Then
            liste.Add(New Rolleneintrag With {
                .Rolle = Spaltenrolle.Attribut, .Zielname = spalte,
                .Text = $"Attribut: {spalte}" & If(_vokabular.Count = 0, "", " (neu)")})
        End If
        For Each v In _vokabular
            liste.Add(New Rolleneintrag With {
                .Rolle = Spaltenrolle.Attribut, .Zielname = v, .Text = $"Attribut: {v}"})
        Next

        liste.Add(New Rolleneintrag With {.Rolle = Spaltenrolle.Gruppe, .Text = "Gruppe"})
        liste.Add(New Rolleneintrag With {.Rolle = Spaltenrolle.Klasse, .Text = "Klasse (als Fixierung)"})
        Return liste
    End Function

    Private Sub ZuordnungBauen()
        Zuordnung.Children.Clear()
        For i = 0 To _wahlen.Count - 1
            Dim wahl = _wahlen(i)
            Dim zeile As New StackPanel With {
                .Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 2, 0, 2)}
            zeile.Children.Add(New TextBlock With {
                .Text = wahl.Name, .Width = 220, .VerticalAlignment = VerticalAlignment.Center,
                .TextTrimming = TextTrimming.CharacterEllipsis})

            Dim eintraege = Rollenliste(wahl.Name)
            Dim rollenwahl As New ComboBox With {.Width = 260}
            For Each e In eintraege
                rollenwahl.Items.Add(e)
            Next
            rollenwahl.SelectedIndex = Math.Max(0, eintraege.FindIndex(
                Function(e) e.Rolle = wahl.Rolle AndAlso
                            (e.Rolle <> Spaltenrolle.Attribut OrElse e.Zielname = wahl.Schluessel)))

            ' Der Gruppentyp erscheint nur, wenn er gebraucht wird - ein
            ' dauerhaft sichtbares, meist wirkungsloses Feld erzieht dazu,
            ' es zu uebersehen.
            Dim typwahl As New ComboBox With {
                .Width = 150, .Margin = New Thickness(8, 0, 0, 0),
                .Visibility = If(wahl.Rolle = Spaltenrolle.Gruppe, Visibility.Visible, Visibility.Hidden)}
            typwahl.Items.Add("verteilung")
            typwahl.Items.Add("buendelung")
            typwahl.SelectedItem = wahl.Gruppentyp

            AddHandler rollenwahl.SelectionChanged,
                Sub()
                    If _fuellt Then Return
                    Dim e = TryCast(rollenwahl.SelectedItem, Rolleneintrag)
                    If e Is Nothing Then Return
                    wahl.Rolle = e.Rolle
                    wahl.Zielname = e.Zielname
                    typwahl.Visibility = If(wahl.Rolle = Spaltenrolle.Gruppe,
                                            Visibility.Visible, Visibility.Hidden)
                    Zeichnen()
                    Pruefen()
                End Sub
            AddHandler typwahl.SelectionChanged,
                Sub()
                    If _fuellt Then Return
                    wahl.Gruppentyp = CStr(If(typwahl.SelectedItem, "verteilung"))
                End Sub

            zeile.Children.Add(rollenwahl)
            zeile.Children.Add(typwahl)
            Zuordnung.Children.Add(zeile)
        Next
    End Sub

    Private Sub Pruefen()
        Dim einwaende = Spaltenzuordnung.Einwaende(_wahlen, _vokabular)
        Dim verworfen = _wahlen.Where(Function(w) w.Rolle = Spaltenrolle.Verwerfen).
            Select(Function(w) w.Name).ToList()

        If einwaende.Count > 0 Then
            Befund.Text = String.Join("  ·  ", einwaende)
            Befund.Foreground = CType(FindResource("farbe-warn-text"), Brush)
        ElseIf verworfen.Count > 0 Then
            ' Das Verwerfen ist gewollt, muss aber SICHTBAR sein - sonst
            ' merkt niemand, dass die halbe Datei nicht ankommt.
            Befund.Text = $"{_vorschau.Datensaetze} Datensatz/Datensaetze. " &
                          $"Nicht uebernommen: {String.Join(", ", verworfen)}."
            Befund.Foreground = CType(FindResource("farbe-text-2"), Brush)
        Else
            Befund.Text = $"{_vorschau.Datensaetze} Datensatz/Datensaetze, alle Spalten zugeordnet."
            Befund.Foreground = CType(FindResource("farbe-text-2"), Brush)
        End If
        UebernehmenKnopf.IsEnabled = einwaende.Count = 0
    End Sub

    ' ===============================================================
    ' Quellen
    ' ===============================================================

    ''' <summary>Zweite Quelle desselben Formats (9.1). Bewusst KEIN
    ''' xlsx-Parser - der braeuchte eine neue Abhaengigkeit gegen den
    ''' BCL-only-Grundsatz; CSV und Einfuegen aus Excel decken die
    ''' Faelle ab.</summary>
    Private Sub AufDatei(sender As Object, e As RoutedEventArgs)
        If _dialoge Is Nothing OrElse _neuLesen Is Nothing Then Return
        Dim pfad = _dialoge.DateiOeffnen("CSV-Datei wählen",
                                         "CSV-Datei (*.csv;*.txt)|*.csv;*.txt|Alle Dateien (*.*)|*.*")
        If pfad Is Nothing Then Return
        Try
            ' Encoding.Default ist auf .NET Core UTF-8; eine als ANSI
            ' gespeicherte Datei aus Excel kaeme mit kaputten Umlauten
            ' herein. Deshalb ausdruecklich mit BOM-Erkennung lesen und
            ' bei fehlendem BOM auf die Windows-Codepage zurueckfallen.
            Dim text = DateiLesen(pfad)
            Uebernehmen(_neuLesen(text))
        Catch ex As IO.IOException
            _dialoge.Hinweis("Datei nicht lesbar", ex.Message)
        Catch ex As UnauthorizedAccessException
            _dialoge.Hinweis("Datei nicht lesbar", ex.Message)
        End Try
    End Sub

    ''' <summary>Liest die Datei so, wie Excel sie ueblicherweise
    ''' hinterlaesst: mit BOM als UTF-8, ohne BOM als Windows-1252. Eine
    ''' Liste mit "Mueller" statt "Müller" ist kein Schoenheitsfehler -
    ''' der Name landet so in mapping.json.</summary>
    Friend Shared Function DateiLesen(pfad As String) As String
        Dim roh = IO.File.ReadAllBytes(pfad)
        If roh.Length >= 3 AndAlso roh(0) = &HEF AndAlso roh(1) = &HBB AndAlso roh(2) = &HBF Then
            Return Text.Encoding.UTF8.GetString(roh, 3, roh.Length - 3)
        End If
        ' Gueltiges UTF-8 ohne BOM erkennen: der strenge Decoder wirft,
        ' wenn die Bytefolge keine ist.
        Try
            Return New Text.UTF8Encoding(False, True).GetString(roh)
        Catch ex As Text.DecoderFallbackException
            Return Text.Encoding.Latin1.GetString(roh)
        End Try
    End Function

    Private Sub AufKopfzeile(sender As Object, e As RoutedEventArgs)
        If _fuellt Then Return
        _vorschau.Kopfzeile = HatKopfzeile.IsChecked = True
        _fuellt = True
        Try
            _vorschau.Spalten = If(_vorschau.Kopfzeile AndAlso _vorschau.Zeilen.Count > 0,
                                   _vorschau.Zeilen(0).ToList(),
                                   Enumerable.Range(1, If(_vorschau.Zeilen.Count = 0, 0, _vorschau.Zeilen(0).Length)).
                                       Select(Function(i) $"Spalte {i}").ToList())
            _wahlen = Spaltenzuordnung.Vorschlag(_vorschau.Spalten)
            ZuordnungBauen()
        Finally
            _fuellt = False
        End Try
        Zeichnen()
        Pruefen()
    End Sub

    ' ===============================================================
    ' Vorschautabelle
    ' ===============================================================

    ''' <summary>Hoechstens zehn Zeilen. Eine Vorschau, die 300 Kinder
    ''' zeigt, ist keine Vorschau mehr - man prueft die ersten paar und
    ''' scrollt sonst nur.</summary>
    Private Const Vorschauzeilen As Integer = 10

    Private Sub Zeichnen()
        Tabelle.Children.Clear()
        Tabelle.ColumnDefinitions.Clear()
        Tabelle.RowDefinitions.Clear()
        If _vorschau.Zeilen.Count = 0 Then Return

        Dim spalten = _wahlen.Count
        For i = 1 To spalten
            Tabelle.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Next

        Dim zeilen = _vorschau.Zeilen.Skip(If(_vorschau.Kopfzeile, 1, 0)).Take(Vorschauzeilen).ToList()
        Tabelle.RowDefinitions.Add(New RowDefinition())
        For i = 1 To zeilen.Count
            Tabelle.RowDefinitions.Add(New RowDefinition())
        Next

        For s = 0 To spalten - 1
            Tabelle.Children.Add(Zelle(_wahlen(s).Name, 0, s, kopf:=True,
                                       verworfen:=_wahlen(s).Rolle = Spaltenrolle.Verwerfen))
        Next
        For z = 0 To zeilen.Count - 1
            For s = 0 To Math.Min(spalten, zeilen(z).Length) - 1
                Tabelle.Children.Add(Zelle(zeilen(z)(s), z + 1, s, kopf:=False,
                                           verworfen:=_wahlen(s).Rolle = Spaltenrolle.Verwerfen))
            Next
        Next
    End Sub

    ''' <summary>Verworfene Spalten stehen durchgestrichen da - so sieht
    ''' man am Inhalt, was fehlen wird, statt es aus der Zuordnungsliste
    ''' erschliessen zu muessen.</summary>
    Private Function Zelle(text As String, zeile As Integer, spalte As Integer,
                           kopf As Boolean, verworfen As Boolean) As UIElement
        Dim t As New TextBlock With {
            .Text = text, .Margin = New Thickness(6, 3, 6, 3),
            .FontWeight = If(kopf, FontWeights.SemiBold, FontWeights.Normal),
            .FontSize = 12,
            .MaxWidth = 180, .TextTrimming = TextTrimming.CharacterEllipsis,
            .Foreground = CType(FindResource(If(verworfen, "farbe-text-3", "farbe-text")), Brush)}
        If verworfen Then t.TextDecorations = TextDecorations.Strikethrough
        Grid.SetRow(t, zeile)
        Grid.SetColumn(t, spalte)
        Return t
    End Function

    Private Sub AufUebernehmen(sender As Object, e As RoutedEventArgs)
        DialogResult = True
    End Sub

End Class
