' Code-Behind des Hauptfensters: nur Verdrahtung. Zustand und Aktionen
' liegen im HauptViewModel, damit sie ohne Fenster pruefbar sind.
Imports System.ComponentModel
Imports Microsoft.Win32

Class MainWindow

    Private ReadOnly _modell As HauptViewModel
    Private ReadOnly _host As ViewerHost

    Public Sub New()
        InitializeComponent()

        ' Das ViewModel entsteht HIER, auf dem UI-Thread. Sein
        ' Progress(Of SolveProgress) faengt damit den richtigen
        ' SynchronizationContext ein und marshallt die Solver-Meldungen
        ' selbsttaetig zurueck (arc42 8.11).
        _modell = New HauptViewModel(New WpfDialoge(Me))
        DataContext = _modell
        _host = New ViewerHost(Dashboard, _modell.Auslieferung,
                               AddressOf _modell.VerarbeiteBrueckenNachricht,
                               AddressOf _modell.BrueckenStartSkript)

        AddHandler _modell.PropertyChanged, AddressOf AufModellAenderung

        InputBindings.Add(New KeyBinding(_modell.SpeichernBefehl, Key.S, ModifierKeys.Control))
        InputBindings.Add(New KeyBinding(_modell.KlassenbildungBefehl, Key.F5, ModifierKeys.None))
    End Sub

    Private Async Sub AufModellAenderung(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName <> NameOf(HauptViewModel.Bereich) Then Return

        Dim zeigtDashboard = _modell.Bereich = Bereich.Klassenbildung OrElse _modell.Bereich = Bereich.Stundenplan
        Startseite.Visibility = If(zeigtDashboard, Visibility.Collapsed, Visibility.Visible)
        Dashboard.Visibility = If(zeigtDashboard, Visibility.Visible, Visibility.Collapsed)

        If zeigtDashboard AndAlso _modell.Auslieferung.SeitenGroesse > 0 Then
            Await _host.AnzeigenAsync()
        End If
    End Sub

    Private Sub AufBeenden(sender As Object, e As RoutedEventArgs)
        Close()
    End Sub

    Private Sub AufBrowserdatenBereinigen(sender As Object, e As RoutedEventArgs)
        ' Ein laufendes WebView2 haelt seine Profildateien - deshalb der
        ' ehrliche Hinweis statt einer Erfolgsmeldung, die keine ist.
        If ViewerHost.ProfilLoeschen() Then
            MessageBox.Show("Browserdaten wurden geloescht.", "Schulplanung")
        Else
            MessageBox.Show($"Die Browserdaten konnten nicht vollstaendig geloescht werden, weil die Anzeige sie gerade benutzt." & vbLf & vbLf &
                            $"Sie liegen unter:{vbLf}{ViewerHost.BenutzerDatenOrdner}{vbLf}{vbLf}Nach dem Beenden der Anwendung erneut versuchen.",
                            "Schulplanung", MessageBoxButton.OK, MessageBoxImage.Information)
        End If
    End Sub

    Private Sub AufUeber(sender As Object, e As RoutedEventArgs)
        MessageBox.Show("Schulplanung - Phase-3-Oberflaeche (Durchstich)." & vbLf &
                        "Rechenkern: TimetableCore mit Google OR-Tools CP-SAT.",
                        "Über", MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub

    Private Sub AufSchliessen(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If Not _modell.Geaendert Then Return
        Dim antwort = MessageBox.Show("Es gibt ungespeicherte Änderungen. Trotzdem beenden?",
                                      "Schulplanung", MessageBoxButton.YesNo, MessageBoxImage.Warning)
        If antwort <> MessageBoxResult.Yes Then e.Cancel = True
    End Sub

End Class

''' <summary>Die WPF-Umsetzung der Dialoge. Liegt bewusst im View - das
''' HauptViewModel kennt nur die Schnittstelle und bleibt damit ohne
''' Fenster testbar.</summary>
Friend NotInheritable Class WpfDialoge
    Implements IDialoge

    Private ReadOnly _besitzer As Window

    Public Sub New(besitzer As Window)
        _besitzer = besitzer
    End Sub

    Private Const Filter As String = "Schulplanungs-Projekt (*.splanx)|*.splanx|Alle Dateien (*.*)|*.*"

    Public Function ProjektdateiOeffnen() As String Implements IDialoge.ProjektdateiOeffnen
        Dim d As New OpenFileDialog With {.Filter = Filter, .Title = "Projekt öffnen"}
        If d.ShowDialog(_besitzer) = True Then Return d.FileName
        Return Nothing
    End Function

    Public Function ProjektdateiSpeichernUnter(vorschlag As String) As String Implements IDialoge.ProjektdateiSpeichernUnter
        Dim d As New SaveFileDialog With {
            .Filter = Filter, .Title = "Projekt speichern", .FileName = vorschlag, .DefaultExt = ".splanx",
            .InitialDirectory = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Schulplanung")
        }
        If d.ShowDialog(_besitzer) = True Then Return d.FileName
        Return Nothing
    End Function

    Public Function SchulordnerWaehlen() As String Implements IDialoge.SchulordnerWaehlen
        Dim d As New OpenFolderDialog With {.Title = "Schulordner wählen (enthält input/stammdaten.yaml)"}
        If d.ShowDialog(_besitzer) = True Then Return d.FolderName
        Return Nothing
    End Function

    Public Function PasswortAbfragen(titel As String, bestaetigen As Boolean) As String Implements IDialoge.PasswortAbfragen
        Dim d As New PasswortFenster(titel, bestaetigen) With {.Owner = _besitzer}
        If d.ShowDialog() = True Then Return d.Passwort
        Return Nothing
    End Function

    Public Sub Hinweis(titel As String, text As String) Implements IDialoge.Hinweis
        MessageBox.Show(_besitzer, text, titel, MessageBoxButton.OK, MessageBoxImage.Warning)
    End Sub

    Public Function Frage(titel As String, text As String) As Boolean Implements IDialoge.Frage
        Return MessageBox.Show(_besitzer, text, titel, MessageBoxButton.YesNo, MessageBoxImage.Question) = MessageBoxResult.Yes
    End Function

End Class
