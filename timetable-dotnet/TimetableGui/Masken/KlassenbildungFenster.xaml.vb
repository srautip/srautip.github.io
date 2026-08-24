' Code-Behind der Klassenbildungs-Eingaben. Nur Verdrahtung; alle
' Entscheidungen liegen in KlassenbildungEingabeViewModel und
' SolverEinstellungenViewModel und sind dort ohne Fenster geprueft.
Imports System.Windows.Media
Imports TimetableCore
Imports TimetableProjekt

Partial Class KlassenbildungFenster

    Private ReadOnly _projekt As Projekt
    Private ReadOnly _dialoge As IDialoge
    Private ReadOnly _eingabe As KlassenbildungEingabeViewModel
    Private ReadOnly _solver As SolverEinstellungenViewModel
    Private _fuellt As Boolean

    Public Event Geaendert As EventHandler

    Public Sub New(projekt As Projekt, dialoge As IDialoge)
        InitializeComponent()
        _projekt = projekt
        _dialoge = dialoge
        _eingabe = New KlassenbildungEingabeViewModel(projekt, dialoge)
        _solver = New SolverEinstellungenViewModel(projekt)

        AddHandler _eingabe.Geaendert, Sub() RaiseEvent Geaendert(Me, EventArgs.Empty)
        AddHandler _solver.Geaendert, Sub() RaiseEvent Geaendert(Me, EventArgs.Empty)

        Fuellen()
        ExpertenBauen()
        RegelnBauen()
    End Sub

    ' ===============================================================
    ' Anzeige
    ' ===============================================================

    Private Sub Fuellen()
        _fuellt = True
        Try
            Anzahl.Text = _eingabe.Anzahl.ToString()
            MinGroesse.Text = _eingabe.MinGroesse.ToString()
            MaxGroesse.Text = _eingabe.MaxGroesse.ToString()
            Stufe.Text = If(_eingabe.Stufe?.ToString(), "")
            Labels.Text = If(_eingabe.Labels Is Nothing, "", String.Join(", ", _eingabe.Labels))

            Zeitbudget.Text = _solver.ZeitbudgetS.ToString()
            Loesungen.Text = _solver.MaxSolutions.ToString()
            Varianten.Text = If(_solver.Varianten?.ToString(), "")
            Workers.Text = _solver.NumWorkers.ToString()
            Seed.Text = _solver.Seed.ToString()

            KinderFuellen()
            Aktualisieren()
        Finally
            _fuellt = False
        End Try
    End Sub

    Private Sub KinderFuellen()
        Dim gewaehlt = TryCast(Kinder.SelectedItem, String)
        Kinder.Items.Clear()
        For Each k In _eingabe.Schueler
            ' Klarname NUR in der Anzeige (Konzept 1) - die Liste selbst
            ' haelt ihn nicht.
            Dim beschreibung = _eingabe.Anzeigename(k.Id)
            If k.Attribute.Count > 0 Then
                beschreibung &= "   " & String.Join(", ", k.Attribute.Select(Function(a) $"{a.Key}={a.Value}"))
            End If
            Kinder.Items.Add(beschreibung)
        Next
        If gewaehlt IsNot Nothing AndAlso Kinder.Items.Contains(gewaehlt) Then Kinder.SelectedItem = gewaehlt
    End Sub

    Private Sub Aktualisieren()
        Vorschau.Text = "Klassen: " & _eingabe.LabelVorschau
        Rahmenzeile.Text = _eingabe.RahmenZeile
        Anzahlzeile.Text = $"{_eingabe.Schueler.Count} Kinder"
        Determinismus.Text = _solver.DeterminismusHinweis
        Dim n = _eingabe.Pruefe().Count + _solver.Pruefe().Count
        Statuszeile.Text = If(n = 0, "Prüfung ohne Beanstandung.", $"{n} Hinweis(e) - Prüfen zeigt sie.")
    End Sub

    ' ===============================================================
    ' Klassenrahmen
    ' ===============================================================

    Private Function Ganz(t As TextBox, rueckfall As Integer) As Integer
        Dim n As Integer
        If Integer.TryParse(t.Text.Trim(), n) Then Return n
        t.Text = rueckfall.ToString()
        Return rueckfall
    End Function

    Private Sub AufRahmen(sender As Object, e As RoutedEventArgs)
        If _fuellt Then Return
        _eingabe.Anzahl = Ganz(Anzahl, _eingabe.Anzahl)
        _eingabe.MinGroesse = Ganz(MinGroesse, _eingabe.MinGroesse)
        _eingabe.MaxGroesse = Ganz(MaxGroesse, _eingabe.MaxGroesse)

        ' Stufe ODER Labels - wer beides fuellt, bekommt die Labels; sie
        ' sind die konkretere Angabe.
        Dim rohLabels = Labels.Text.Split(","c).Select(Function(x) x.Trim()).Where(Function(x) x <> "").ToList()
        If rohLabels.Count > 0 Then
            _eingabe.Labels = rohLabels
            Stufe.Text = ""
        Else
            _eingabe.Labels = Nothing
            Dim s As Integer
            _eingabe.Stufe = If(Integer.TryParse(Stufe.Text.Trim(), s), CType(s, Integer?), Nothing)
        End If
        Aktualisieren()
    End Sub

    ' ===============================================================
    ' Zwischenablage-Import
    ' ===============================================================

    Private Sub AufEinfuegen(sender As Object, e As RoutedEventArgs)
        Dim text As String = Nothing
        Try
            If Clipboard.ContainsText() Then text = Clipboard.GetText()
        Catch
            ' Die Zwischenablage kann von einem anderen Programm belegt
            ' sein - das ist kein Absturzgrund.
        End Try
        If String.IsNullOrWhiteSpace(text) Then
            _dialoge.Hinweis("Einfügen", "In der Zwischenablage steht kein Text.")
            Return
        End If

        Dim v = _eingabe.ImportPruefen(text)
        If v.Datensaetze = 0 Then
            _dialoge.Hinweis("Einfügen", "Aus dem Text ließ sich keine Zeile lesen.")
            Return
        End If

        Dim d As New ImportDialog(v) With {.Owner = Me}
        If d.ShowDialog() <> True Then Return

        Dim n = _eingabe.ImportUebernehmen(v, d.NachnameSpalte, d.VornameSpalte)
        KinderFuellen()
        Aktualisieren()
        _dialoge.Hinweis("Einfügen", $"{n} Kind(er) übernommen.")
    End Sub

    Private Sub AufEntfernen(sender As Object, e As RoutedEventArgs)
        Dim i = Kinder.SelectedIndex
        If i < 0 OrElse i >= _eingabe.Schueler.Count Then Return
        Dim kind = _eingabe.Schueler(i)
        If Not _dialoge.Frage("Entfernen",
            $"„{_eingabe.Anzeigename(kind.Id)}"" entfernen? Gruppenmitgliedschaften, Wünsche und Fixierungen dieses Kindes verschwinden mit.") Then Return
        _eingabe.Entfernen(kind.Id)
        KinderFuellen()
        Aktualisieren()
    End Sub

    ' ===============================================================
    ' Regeln (Gruppen, Balance, Wuensche, Fixierungen)
    ' ===============================================================

    Private Sub RegelnBauen()
        Regeln.Children.Clear()
        Dim e = _projekt.Klassenbildung

        Regeln.Children.Add(Ueberschrift($"Gruppen ({e.Gruppen.Count})"))
        For Each g In e.Gruppen
            Regeln.Children.Add(Zeile($"[{If(g.Kuerzel, g.Id)}] {g.Typ}" &
                                      If(g.MaxProKlasse.HasValue, $", max {g.MaxProKlasse}/Klasse", "") &
                                      $" - {g.Mitglieder.Count} Kinder, {g.Modus}, Prio {g.Prio}"))
        Next

        Regeln.Children.Add(Ueberschrift($"Balance ({e.Balance.Count})"))
        For Each b In e.Balance
            Regeln.Children.Add(Zeile($"{b.Attribut}={b.Wert}, Toleranz {b.Toleranz}, {b.Modus}, Prio {b.Prio}"))
        Next

        Regeln.Children.Add(Ueberschrift($"Wünsche ({e.Wuensche.Count})"))
        For Each w In e.Wuensche
            Regeln.Children.Add(Zeile($"{w.Typ}: " &
                                      String.Join(" + ", w.Kinder.Select(AddressOf _eingabe.Anzeigename)) &
                                      $", {w.Modus}, Prio {w.Prio}"))
        Next

        ' Fixierungen: "primaer entstehen sie am Board (F1/F2 per Pin/Drag
        ' & Drop) - der Dialog dient der Durchsicht und dem gezielten
        ' Loesen" (6.11). Deshalb hier nur Anzeige und Loesen.
        Regeln.Children.Add(Ueberschrift($"Fixierungen ({e.Fixierungen.Count})"))
        Regeln.Children.Add(Zeile("Entstehen normalerweise am Board per Pin. Hier nur zur Durchsicht und zum Lösen.", True))
        For Each f In e.Fixierungen.ToList()
            Dim fix = f
            Dim reihe As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 2, 0, 2)}
            reihe.Children.Add(New TextBlock With {
                .Text = $"{_eingabe.Anzeigename(fix.Kind)} → " &
                        If(fix.Klasse.HasValue, $"Klasse {fix.Klasse}", $"NICHT Klasse {fix.NichtKlasse}"),
                .Width = 420, .VerticalAlignment = VerticalAlignment.Center})
            Dim loesen As New Button With {.Content = "lösen", .MinWidth = 60}
            AddHandler loesen.Click,
                Sub()
                    _projekt.Klassenbildung.Fixierungen.Remove(fix)
                    RaiseEvent Geaendert(Me, EventArgs.Empty)
                    RegelnBauen()
                    Aktualisieren()
                End Sub
            reihe.Children.Add(loesen)
            Regeln.Children.Add(reihe)
        Next
    End Sub

    Private Function Ueberschrift(text As String) As UIElement
        Return New TextBlock With {
            .Text = text, .FontWeight = FontWeights.SemiBold,
            .Margin = New Thickness(0, 16, 0, 6), .FontSize = 15}
    End Function

    Private Function Zeile(text As String, Optional gedaempft As Boolean = False) As UIElement
        Return New TextBlock With {
            .Text = text, .Margin = New Thickness(0, 2, 0, 2), .TextWrapping = TextWrapping.Wrap,
            .FontSize = If(gedaempft, 12, 13),
            .Foreground = CType(FindResource(If(gedaempft, "farbe-text-3", "farbe-text")), Brush)}
    End Function

    ' ===============================================================
    ' Solver
    ' ===============================================================

    Private Sub AufSolver(sender As Object, e As RoutedEventArgs)
        If _fuellt Then Return
        Dim d As Double
        If Double.TryParse(Zeitbudget.Text.Trim(), d) Then _solver.ZeitbudgetS = d Else Zeitbudget.Text = _solver.ZeitbudgetS.ToString()
        _solver.MaxSolutions = Ganz(Loesungen, _solver.MaxSolutions)
        _solver.NumWorkers = Ganz(Workers, _solver.NumWorkers)
        _solver.Seed = Ganz(Seed, _solver.Seed)
        Dim v As Integer
        _solver.Varianten = If(Integer.TryParse(Varianten.Text.Trim(), v), CType(v, Integer?), Nothing)
        Aktualisieren()
    End Sub

    Private Sub ExpertenBauen()
        Experten.Children.Clear()
        Dim letzteGruppe = ""
        For Each feld In _solver.Expertenfelder()
            Dim f = feld
            If f.Gruppe <> letzteGruppe Then
                Experten.Children.Add(Ueberschrift(f.Gruppe))
                letzteGruppe = f.Gruppe
            End If
            Dim reihe As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 2, 0, 2)}
            reihe.Children.Add(New TextBlock With {
                .Text = f.Name, .Width = 260, .VerticalAlignment = VerticalAlignment.Center, .FontSize = 12})
            Dim kasten As New TextBox With {.Text = f.Lesen.Invoke(), .Width = 110}
            ' Leer heisst "Default des Kerns" - deshalb wird ein leeres
            ' Feld uebernommen und nicht zurueckgesetzt.
            AddHandler kasten.LostFocus,
                Sub()
                    f.Schreiben.Invoke(kasten.Text)
                    kasten.Text = f.Lesen.Invoke()
                    Aktualisieren()
                End Sub
            reihe.Children.Add(kasten)
            If f.Hilfe <> "" Then
                reihe.Children.Add(New TextBlock With {
                    .Text = "  " & f.Hilfe, .MaxWidth = 430, .TextWrapping = TextWrapping.Wrap,
                    .VerticalAlignment = VerticalAlignment.Center, .FontSize = 11,
                    .Foreground = CType(FindResource("farbe-text-3"), Brush)})
            End If
            Experten.Children.Add(reihe)
        Next
    End Sub

    Private Sub AufPruefen(sender As Object, e As RoutedEventArgs)
        Dim fehler = _eingabe.Pruefe().Concat(_solver.Pruefe()).ToList()
        If fehler.Count = 0 Then
            _dialoge.Hinweis("Prüfung", "Keine Beanstandungen.")
        Else
            _dialoge.Hinweis("Prüfung", String.Join(vbLf, fehler.Take(25)) &
                             If(fehler.Count > 25, vbLf & $"… und {fehler.Count - 25} weitere", ""))
        End If
    End Sub

End Class
