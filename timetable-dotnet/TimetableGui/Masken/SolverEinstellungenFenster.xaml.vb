' Code-Behind der Solver-Einstellungen (6.12). Nur Verdrahtung; die
' Entscheidungen liegen in SolverEinstellungenViewModel und sind dort
' ohne Fenster geprueft.
Imports System.Windows.Media
Imports TimetableProjekt

Partial Class SolverEinstellungenFenster

    Private ReadOnly _dialoge As IDialoge
    Private ReadOnly _solver As SolverEinstellungenViewModel
    Private _fuellt As Boolean

    Public Event Geaendert As EventHandler

    Public Sub New(projekt As Projekt, dialoge As IDialoge,
                   Optional speicherung As ISpeicherung = Nothing)
        InitializeComponent()
        _dialoge = dialoge
        _solver = New SolverEinstellungenViewModel(projekt)
        AddHandler _solver.Geaendert, Sub() RaiseEvent Geaendert(Me, EventArgs.Empty)

        Fuellen()
        ExpertenBauen()
        SpeicherungVerdrahten(speicherung)
    End Sub

    Private Sub Fuellen()
        _fuellt = True
        Try
            Zeitbudget.Text = _solver.ZeitbudgetS.ToString()
            Loesungen.Text = _solver.MaxSolutions.ToString()
            Varianten.Text = If(_solver.Varianten?.ToString(), "")
            Workers.Text = _solver.NumWorkers.ToString()
            Seed.Text = _solver.Seed.ToString()
            Aktualisieren()
        Finally
            _fuellt = False
        End Try
    End Sub

    Private Sub Aktualisieren()
        Determinismus.Text = _solver.DeterminismusHinweis
        Dim n = _solver.Pruefe().Count
        Statuszeile.Text = If(n = 0, "Prüfung ohne Beanstandung.", $"{n} Hinweis(e) - Prüfen zeigt sie.")
    End Sub

    Private Function Ganz(t As TextBox, rueckfall As Integer) As Integer
        Dim n As Integer
        If Integer.TryParse(t.Text.Trim(), n) Then Return n
        t.Text = rueckfall.ToString()
        Return rueckfall
    End Function

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

    Private Function Ueberschrift(text As String) As UIElement
        Return New TextBlock With {
            .Text = text, .FontWeight = FontWeights.SemiBold,
            .Margin = New Thickness(0, 16, 0, 6), .FontSize = 15}
    End Function

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
        Dim fehler = _solver.Pruefe()
        If fehler.Count = 0 Then
            _dialoge.Hinweis("Prüfung", "Keine Beanstandungen.")
        Else
            _dialoge.Hinweis("Prüfung", String.Join(vbLf, fehler))
        End If
    End Sub

    ' ===============================================================
    ' Speicherzustand - wie in den uebrigen Masken (Speicherstand.vb)
    ' ===============================================================

    Private _speicherung As ISpeicherung

    Private Sub SpeicherungVerdrahten(speicherung As ISpeicherung)
        _speicherung = speicherung
        If _speicherung Is Nothing Then
            SpeichernKnopf.Visibility = Visibility.Collapsed
            Speicherzustand.Visibility = Visibility.Collapsed
            Return
        End If
        AddHandler _speicherung.ZustandGeaendert, Sub() SpeicherzustandZeigen()
        InputBindings.Add(New KeyBinding(New Befehl(Sub() Speichern()), Key.S, ModifierKeys.Control))
        SpeicherzustandZeigen()
    End Sub

    Private Sub SpeicherzustandZeigen()
        Speicherzustand.Text = Speicheranzeige.Zustandstext(_speicherung)
        Speicherzustand.Foreground = CType(FindResource(Speicheranzeige.Zustandsfarbe(_speicherung)), Brush)
        Speicherzustand.ToolTip = Speicheranzeige.Uebernahmehinweis
        SpeichernKnopf.IsEnabled = _speicherung IsNot Nothing AndAlso
                                   _speicherung.Moeglich AndAlso _speicherung.Ungespeichert
    End Sub

    Private Sub Speichern()
        If _speicherung Is Nothing OrElse Not _speicherung.Moeglich Then Return
        _speicherung.Speichern()
        SpeicherzustandZeigen()
    End Sub

    Private Sub AufSpeichern(sender As Object, e As RoutedEventArgs)
        Speichern()
    End Sub
End Class
