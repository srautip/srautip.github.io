' Code-Behind des Projekt-Assistenten. Nur Verdrahtung: welcher Schritt
' sichtbar ist, was der Weiter-Knopf tut und wie die Schrittleiste
' aussieht. Jede Entscheidung darueber, ob eine Eingabe taugt, liegt in
' ProjektAssistentViewModel und ist dort ohne Fenster geprueft.
Imports System.Windows.Media
Imports TimetableCore

Partial Class ProjektAssistent

    Private ReadOnly _modell As ProjektAssistentViewModel
    Private _schritt As Integer = 1
    Private _fuellt As Boolean

    ''' <summary>Das Ergebnis, oder Nothing bei Abbruch. Das Fenster
    ''' schliesst sich mit DialogResult=True nur dann, wenn hier etwas
    ''' steht.</summary>
    Public ReadOnly Property Entwurf As ProjektEntwurf

    Private ReadOnly _felder As New Dictionary(Of Integer, TextBox)
    Private ReadOnly _haken As New Dictionary(Of String, CheckBox)

    Public Sub New(dialoge As IDialoge)
        InitializeComponent()
        _modell = New ProjektAssistentViewModel(dialoge)

        _fuellt = True
        Try
            For Each a In _modell.Schularten
                SchulartFeld.Items.Add(a)
            Next
            SchulartFeld.SelectedItem = _modell.Schulart
            BundeslandFeld.Items.Add("BW")
            BundeslandFeld.SelectedIndex = 0
            SchuljahrFeld.Text = _modell.Schuljahr
            StufenFeld.Text = _modell.KlassenstufenAnzahl.ToString()
            ZuegeFeld.Text = _modell.Zuege.ToString()
            LehrerFeld.Text = _modell.LehrerAnzahl.ToString()

            For Each v In GruppenVorlagen.Alle
                Dim cb As New CheckBox With {.Content = v.Name, .Margin = New Thickness(0, 2, 0, 0)}
                Dim hinweis As New TextBlock With {
                    .Text = v.Bemerkung, .TextWrapping = TextWrapping.Wrap, .MaxWidth = 520,
                    .Margin = New Thickness(22, 0, 0, 8),
                    .FontSize = CDbl(FindResource("grad-klein")),
                    .Foreground = CType(FindResource("farbe-text-3"), Brush)}
                AddHandler cb.Click, AddressOf AufFeld
                _haken(v.Name) = cb
                VorlagenFelder.Children.Add(cb)
                VorlagenFelder.Children.Add(hinweis)
            Next
        Finally
            _fuellt = False
        End Try

        Zeige()
    End Sub

    ' ===============================================================
    ' Anzeige
    ' ===============================================================

    Private Shared ReadOnly Titel As String() = {
        "Schule", "Struktur", "Schüler & Gruppen", "Schutz", "Zusammenfassung"}

    Private Shared ReadOnly Unterkopfe As String() = {
        "Wer plant hier? Diese Angaben bestimmen, welche Kontingentstundentafel zugrunde liegt.",
        "Wie viele Klassenstufen, wie viele Züge – daraus entstehen Klassen, Fächer und Lehrkräfte.",
        "Optional: anonyme Platzhalter-Kinder und die typischen Gruppen.",
        "Die Projektdatei wird verschlüsselt. Ohne Passwort kein Projekt.",
        "Das entsteht gleich. Nichts davon ist endgültig – alles ist danach in den Masken änderbar."}

    ''' <summary>Ein Eintrag der Schrittleiste. Zustand doppelt kodiert:
    ''' Zeichen UND Farbe - die Zeichen stammen aus dem MDL2-Kernbereich,
    ''' damit sie auch auf Windows 10 rendern.</summary>
    Public NotInheritable Class Leistenschritt
        Public Property Zeichen As String
        Public Property Titel As String
        Public Property Farbe As Brush
        Public Property Gewicht As FontWeight
    End Class

    Private Sub Zeige()
        Dim seiten = {Schritt1, Schritt2, Schritt3, Schritt4, Schritt5}
        For i = 0 To seiten.Length - 1
            seiten(i).Visibility = If(i = _schritt - 1, Visibility.Visible, Visibility.Collapsed)
        Next

        Kopf.Text = $"Schritt {_schritt} von {ProjektAssistentViewModel.LetzterSchritt}: {Titel(_schritt - 1)}"
        Unterkopf.Text = Unterkopfe(_schritt - 1)
        ZurueckKnopf.IsEnabled = _schritt > 1
        WeiterKnopf.Content = If(_schritt = ProjektAssistentViewModel.LetzterSchritt, "Projekt anlegen", "Weiter")

        LeisteFuellen()
        If _schritt = 2 Then SchrittZweiBeschriften()
        If _schritt = 3 Then SchuelerfelderBauen()
        If _schritt = 4 Then
            StaerkeLabel.Text = "Stärke: " & _modell.PasswortStaerke()
            PfadLabel.Text = If(_modell.Pfad = "", "(noch nicht gewählt)", _modell.Pfad)
        End If
        If _schritt = 5 Then
            _modell.Vorschau()
            Bilanz.ItemsSource = _modell.Zusammenfassung()
        End If
        BefundZeigen()
    End Sub

    Private Sub LeisteFuellen()
        Dim eintraege As New List(Of Leistenschritt)
        For i = 1 To ProjektAssistentViewModel.LetzterSchritt
            Dim erledigt = i < _schritt
            Dim aktuell = i = _schritt
            eintraege.Add(New Leistenschritt With {
                .Zeichen = If(erledigt, ChrW(&HE73E), If(aktuell, ChrW(&HE76C), ChrW(&HE915))),
                .Titel = $"{i}. {Titel(i - 1)}",
                .Farbe = CType(FindResource(If(aktuell, "farbe-text", If(erledigt, "farbe-ok-text", "farbe-text-3"))), Brush),
                .Gewicht = If(aktuell, FontWeights.SemiBold, FontWeights.Normal)})
        Next
        Leiste.ItemsSource = eintraege
    End Sub

    Private Sub SchrittZweiBeschriften()
        StufenLabel.Text = $"Klassenstufen (1 bis {_modell.MaxKlassenstufen()})"
        LehrerLabel.Text = $"Klassenlehrkräfte (mindestens {_modell.MindestLehrer()})"
        Dim stufen = _modell.Klassenstufen()
        StrukturVorschau.Text = If(stufen.Count = 0, "",
            $"Ergibt {stufen.Count * Math.Max(0, _modell.Zuege)} Klassen: " &
            String.Join(", ", stufen.Select(Function(s) $"{s}a…")) & ".")
    End Sub

    ''' <summary>Ein Feld je Klassenstufe. Wird bei jedem Betreten neu
    ''' gebaut - die Zahl der Stufen kann sich in Schritt 2 geaendert
    ''' haben, und ein stehengebliebenes Feld fuer eine Stufe, die es nicht
    ''' mehr gibt, waere eine Eingabe ohne Wirkung.</summary>
    Private Sub SchuelerfelderBauen()
        Dim stufen = _modell.Klassenstufen()
        If _felder.Keys.OrderBy(Function(k) k).SequenceEqual(stufen) Then Return

        _fuellt = True
        Try
            _felder.Clear()
            SchuelerFelder.Children.Clear()
            For Each stufe In stufen
                Dim s = stufe
                Dim zeile As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 0, 0, 4)}
                zeile.Children.Add(New TextBlock With {
                    .Text = $"Klassenstufe {s}", .Width = 140, .VerticalAlignment = VerticalAlignment.Center,
                    .Foreground = CType(FindResource("farbe-text-2"), Brush)})
                Dim feld As New TextBox With {.Width = 80, .Text = Bestandszahl(s).ToString()}
                AddHandler feld.TextChanged, AddressOf AufFeld
                zeile.Children.Add(feld)
                _felder(s) = feld
                SchuelerFelder.Children.Add(zeile)
            Next
        Finally
            _fuellt = False
        End Try
    End Sub

    Private Function Bestandszahl(stufe As Integer) As Integer
        Dim n = 0
        If _modell.SchuelerJeKlasse.TryGetValue(stufe, n) Then Return n
        Return 0
    End Function

    Private Sub BefundZeigen()
        Dim fehler = _modell.Pruefe(_schritt)
        Befund.Text = String.Join("  ·  ", fehler)
        Befund.Foreground = CType(FindResource(If(fehler.Count = 0, "farbe-text-3", "farbe-warn-text")), Brush)
        ' Nicht ausgrauen, sondern beim Druecken melden: ein toter Knopf
        ' sagt nicht, WAS fehlt - und genau das will man wissen.
        WeiterKnopf.IsEnabled = True
    End Sub

    ' ===============================================================
    ' Eingaben einsammeln
    ' ===============================================================

    Private Sub AufFeld(sender As Object, e As RoutedEventArgs)
        If _fuellt Then Return
        Einsammeln()
        If _schritt = 2 Then SchrittZweiBeschriften()
        If _schritt = 4 Then StaerkeLabel.Text = "Stärke: " & _modell.PasswortStaerke()
        BefundZeigen()
    End Sub

    Private Sub AufSchulart(sender As Object, e As SelectionChangedEventArgs)
        If _fuellt Then Return
        ' Die Schulart setzt Vorgaben (Klassenstufen, Lehrerzahl) neu -
        ' die Felder muessen dem folgen, sonst zeigt die Maske Zahlen, mit
        ' denen das Modell nicht mehr rechnet.
        _modell.Schulart = CStr(SchulartFeld.SelectedItem)
        _fuellt = True
        Try
            StufenFeld.Text = _modell.KlassenstufenAnzahl.ToString()
            LehrerFeld.Text = _modell.LehrerAnzahl.ToString()
        Finally
            _fuellt = False
        End Try
        BefundZeigen()
    End Sub

    Private Sub Einsammeln()
        _modell.SchulName = NameFeld.Text.Trim()
        _modell.Schuljahr = SchuljahrFeld.Text.Trim()
        If SchulartFeld.SelectedItem IsNot Nothing Then _modell.Schulart = CStr(SchulartFeld.SelectedItem)
        If BundeslandFeld.SelectedItem IsNot Nothing Then _modell.Bundesland = CStr(BundeslandFeld.SelectedItem)

        _modell.KlassenstufenAnzahl = Zahl(StufenFeld.Text, 0)
        _modell.Zuege = Zahl(ZuegeFeld.Text, 0)
        _modell.LehrerAnzahl = Zahl(LehrerFeld.Text, 0)

        For Each paar In _felder
            _modell.SchuelerJeKlasse(paar.Key) = Zahl(paar.Value.Text, 0)
        Next
        _modell.GewaehlteVorlagen = _haken.Where(Function(h) h.Value.IsChecked = True).
            Select(Function(h) h.Key).ToList()

        _modell.Passwort = PasswortFeld.Password
        _modell.PasswortWiederholung = PasswortFeld2.Password
    End Sub

    ''' <summary>Unlesbares gilt als `ersatz`, nicht als Ausnahme: waehrend
    ''' des Tippens ist ein Feld zwangslaeufig kurz leer, und ein Dialog
    ''' dafuer waere unbenutzbar. Die Pruefung meldet die 0 dann selbst.</summary>
    Private Shared Function Zahl(text As String, ersatz As Integer) As Integer
        Dim n As Integer
        If Integer.TryParse(If(text, "").Trim(), n) Then Return n
        Return ersatz
    End Function

    ' ===============================================================
    ' Navigation
    ' ===============================================================

    Private Sub AufZurueck(sender As Object, e As RoutedEventArgs)
        If _schritt <= 1 Then Return
        _schritt -= 1
        Zeige()
    End Sub

    Private Sub AufWeiter(sender As Object, e As RoutedEventArgs)
        Einsammeln()
        Dim fehler = _modell.Pruefe(_schritt)
        If fehler.Count > 0 Then
            BefundZeigen()
            Return
        End If

        If _schritt < ProjektAssistentViewModel.LetzterSchritt Then
            _schritt += 1
            Zeige()
            Return
        End If

        Try
            _Entwurf = _modell.Entwurf()
        Catch ex As InvalidOperationException
            ' Scaffold.Baue wirft bei Kombinationen, die keine Schule
            ' ergeben. Hier statt eines Absturzes der Klartext - und der
            ' Assistent bleibt offen, damit man zurueckgehen kann.
            Befund.Text = ex.Message
            Befund.Foreground = CType(FindResource("farbe-krit-text"), Brush)
            Return
        End Try
        DialogResult = True
    End Sub

    Private Sub AufPfad(sender As Object, e As RoutedEventArgs)
        Einsammeln()
        _modell.SpeicherortWaehlen()
        PfadLabel.Text = If(_modell.Pfad = "", "(noch nicht gewählt)", _modell.Pfad)
        BefundZeigen()
    End Sub

End Class
