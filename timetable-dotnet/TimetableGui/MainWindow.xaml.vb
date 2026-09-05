' Code-Behind des Hauptfensters: nur Verdrahtung. Zustand und Aktionen
' liegen im HauptViewModel, damit sie ohne Fenster pruefbar sind.
Imports System.ComponentModel
Imports Microsoft.Win32

Class MainWindow

    Private ReadOnly _modell As HauptViewModel
    Private ReadOnly _host As ViewerHost
    Private ReadOnly _speicherung As Speicherstand

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

        _speicherung = New Speicherstand(_modell)
        AddHandler _modell.PropertyChanged, AddressOf AufModellAenderung
        AddHandler _modell.PropertyChanged, Sub() SchrittleisteFuellen()
        SchrittleisteFuellen()

        InputBindings.Add(New KeyBinding(_modell.SpeichernBefehl, Key.S, ModifierKeys.Control))
        InputBindings.Add(New KeyBinding(_modell.KlassenbildungBefehl, Key.F5, ModifierKeys.None))
        InputBindings.Add(New KeyBinding(_modell.StundenplanBefehl, Key.F6, ModifierKeys.None))
    End Sub

    Private Async Sub AufModellAenderung(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = HauptViewModel.AnzeigeAktualisiert Then
            ' Bereich selbst hat sich nicht geaendert (erneutes Rechnen aus
            ' dem schon offenen Dashboard) - dort bliebe WebView2 sonst auf
            ' dem vorigen Stand stehen, siehe HauptViewModel.AnzeigeAktualisiert.
            If _modell.Auslieferung.SeitenGroesse > 0 Then Await _host.AnzeigenAsync()
            Return
        End If
        If e.PropertyName <> NameOf(HauptViewModel.Bereich) Then Return

        ' Stammdaten oeffnen als MODALER Dialog ueber der Flaeche - "das
        ' Dashboard bleibt der Anker" (gui-ui-konzept.md 2). Der Bereich
        ' springt danach auf Start zurueck, damit die Seitenleiste nicht
        ' auf einem Eintrag stehenbleibt, hinter dem nichts liegt.
        If _modell.Bereich = Bereich.Stammdaten Then
            StammdatenOeffnen()
            _modell.Bereich = Bereich.Start
            Return
        End If
        If _modell.Bereich = Bereich.Regeln Then
            RegelnOeffnen()
            _modell.Bereich = Bereich.Start
            Return
        End If

        Dim zeigtDashboard = _modell.Bereich = Bereich.Klassenbildung OrElse _modell.Bereich = Bereich.Stundenplan
        Dim zeigtLaeufe = _modell.Bereich = Bereich.Laeufe
        Startseite.Visibility = If(zeigtDashboard OrElse zeigtLaeufe, Visibility.Collapsed, Visibility.Visible)
        Dashboard.Visibility = If(zeigtDashboard, Visibility.Visible, Visibility.Collapsed)
        Laeufe.Visibility = If(zeigtLaeufe, Visibility.Visible, Visibility.Collapsed)
        If zeigtLaeufe Then LaeufeAufbauen()

        If zeigtDashboard AndAlso _modell.Auslieferung.SeitenGroesse > 0 Then
            Await _host.AnzeigenAsync()
        End If
    End Sub

    ''' <summary>Oeffnet die Stammdaten-Pflege. Ohne Projekt gibt es
    ''' nichts zu pflegen - dann der Hinweis statt eines leeren Fensters,
    ''' in dem jede Aktion ins Nichts liefe.</summary>
    Private Sub StammdatenOeffnen()
        If Not _modell.ProjektOffen Then
            MessageBox.Show(Me, "Erst ein Projekt anlegen, öffnen oder eine Schule übernehmen.",
                            "Stammdaten", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim f As New StammdatenFenster(_modell.Projekt, New WpfDialoge(Me), _speicherung) With {.Owner = Me}
        ' Jede Aenderung in den Masken macht das Projekt ungespeichert -
        ' Autosave ist ausdruecklich abgelehnt (Konzept 7), also muss der
        ' Indikator stimmen.
        AddHandler f.Geaendert, Sub() _modell.Geaendert = True
        f.ShowDialog()
    End Sub

    ''' <summary>Die Eingaben der Klassenbildung (6.11) samt
    ''' Solver-Einstellungen (6.12). Eigenes Fenster, weil die
    ''' Einschulungsliste ausdruecklich NICHT die Schuelerliste der
    ''' Stammdaten ist - "die Klassenbildung laeuft VOR der
    ''' Klassenzuteilung".</summary>
    Private Sub KlassenbildungEingabenOeffnen()
        If Not _modell.ProjektOffen Then
            MessageBox.Show(Me, "Erst ein Projekt anlegen, öffnen oder eine Schule übernehmen.",
                            "Klassenbildung", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If
        Dim f As New KlassenbildungFenster(_modell.Projekt, New WpfDialoge(Me), _speicherung) With {.Owner = Me}
        AddHandler f.Geaendert, Sub() _modell.Geaendert = True
        f.ShowDialog()
    End Sub

    ''' <summary>Die Regelverwaltung (6.10). Sie sitzt auf dem
    ''' Seitenleisten-Eintrag "Regeln"; die Eingaben der Klassenbildung,
    ''' die dort zwischenzeitlich lagen, bleiben ueber Extras erreichbar -
    ''' sie sind Stammdaten eines anderen Laufs, keine Regeln.</summary>
    Private Sub RegelnOeffnen()
        If Not _modell.ProjektOffen Then
            MessageBox.Show(Me, "Erst ein Projekt anlegen, öffnen oder eine Schule übernehmen.",
                            "Regeln", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If
        Dim f As New RegelnFenster(_modell.Projekt, New WpfDialoge(Me), _speicherung) With {.Owner = Me}
        AddHandler f.Geaendert, Sub() _modell.Geaendert = True
        f.ShowDialog()
    End Sub

    ''' <summary>Ein Klick auf einen Schritt fuehrt in seinen Bereich
    ''' (Konzept 8). Das Ziel steht am Knopf, nicht in einer
    ''' Fallunterscheidung hier - sonst gaebe es zwei Orte, an denen
    ''' die Zuordnung Schritt-zu-Bereich festgelegt ist.</summary>
    Private Sub AufSchritt(sender As Object, e As RoutedEventArgs)
        Dim knopf = TryCast(sender, Button)
        If knopf Is Nothing OrElse Not TypeOf knopf.Tag Is Bereich Then Return
        _modell.Bereich = CType(knopf.Tag, Bereich)
    End Sub

    Private Sub AufKlarnamenExport(sender As Object, e As RoutedEventArgs)
        _modell.KlarnamenExportieren()
    End Sub

    Private Sub AufRegeln(sender As Object, e As RoutedEventArgs)
        RegelnOeffnen()
    End Sub

    Private Sub AufStammdaten(sender As Object, e As RoutedEventArgs)
        StammdatenOeffnen()
    End Sub

    Private Sub AufKlassenbildungEingaben(sender As Object, e As RoutedEventArgs)
        KlassenbildungEingabenOeffnen()
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


    ' ===============================================================
    ' Bereich "Laeufe" (Stufe G3)
    ' ===============================================================

    Private _historie As LaeufeViewModel

    ''' <summary>Das Laeufe-ViewModel haengt am PROJEKT, nicht am Fenster -
    ''' nach "Datei oeffnen" ist es ein anderes. Es deshalb bei jedem
    ''' Betreten neu zu bauen ist billiger und ehrlicher, als eines ueber
    ''' Projektwechsel hinweg gueltig zu halten.</summary>
    Private Sub LaeufeAufbauen()
        If Not _modell.ProjektOffen Then
            Standliste.ItemsSource = Nothing
            StandInfo.Text = "Kein Projekt geöffnet."
            Return
        End If

        _historie = New LaeufeViewModel(_modell.Projekt, New WpfDialoge(Me))
        AddHandler _historie.Geaendert, Sub()
                                          _modell.Geaendert = True
                                          LaeufeFuellen()
                                      End Sub
        AddHandler _historie.Anzeigen, Sub(s, stand) _modell.StandAnzeigen(stand)
        LaeufeFuellen()
    End Sub

    Private Sub LaeufeFuellen()
        Dim vorher = TryCast(Standliste.SelectedItem, Standzeile)
        Dim zeilen = _historie.Zeilen()
        Standliste.ItemsSource = zeilen
        If vorher IsNot Nothing Then
            Standliste.SelectedItem = zeilen.FirstOrDefault(Function(z) z.Id = vorher.Id)
        End If
        If Standliste.SelectedItem Is Nothing Then Standliste.SelectedIndex = 0
        StandInfoZeigen()
    End Sub

    Private Function GewaehlterStand() As Standzeile
        Return TryCast(Standliste.SelectedItem, Standzeile)
    End Function

    Private Sub StandInfoZeigen()
        Dim z = GewaehlterStand()
        If z Is Nothing Then
            StandInfo.Text = "Noch kein Lauf gerechnet."
            StandFreigeben.IsEnabled = False
            Return
        End If
        StandInfo.Text = $"{z.Art} · {z.Kennzahlen}" &
                         If(z.IstFreigabe, "  ·  freigegeben – geschützt gegen Löschen und Verdrängen", "")
        StandFreigeben.IsEnabled = Not z.IstFreigabe
    End Sub

    Private Sub AufStandAuswahl(sender As Object, e As SelectionChangedEventArgs)
        StandInfoZeigen()
    End Sub

    Private Sub AufStandAnsehen(sender As Object, e As RoutedEventArgs)
        Dim z = GewaehlterStand()
        If z IsNot Nothing Then _historie.Ansehen(z.Id)
    End Sub

    Private Sub AufStandUmbenennen(sender As Object, e As RoutedEventArgs)
        Dim z = GewaehlterStand()
        If z Is Nothing Then Return
        Dim neu = Microsoft.VisualBasic.Interaction.InputBox(
            "Neues Label für diesen Stand:", "Umbenennen", z.Label)
        If neu = "" Then Return
        _historie.Umbenennen(z.Id, neu)
    End Sub

    Private Sub AufStandLoeschen(sender As Object, e As RoutedEventArgs)
        Dim z = GewaehlterStand()
        If z IsNot Nothing Then _historie.Loeschen(z.Id)
    End Sub

    Private Sub AufStandFreigeben(sender As Object, e As RoutedEventArgs)
        Dim z = GewaehlterStand()
        If z IsNot Nothing Then _historie.Freigeben(z.Id)
    End Sub


    ''' <summary>Die Leiste wird NEU GEBAUT statt gebunden: ihre Zeilen
    ''' sind abgeleitete Werte, keine Eigenschaften mit
    ''' Aenderungsmeldung. Ein ItemsSource-Binding zeigte sonst den
    ''' Stand von vorhin.</summary>
    Private Sub SchrittleisteFuellen()
        Schrittleiste.ItemsSource = _modell.Schritte()
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

    Public Function ProjektAssistent() As ProjektEntwurf Implements IDialoge.ProjektAssistent
        Dim d As New ProjektAssistent(Me) With {.Owner = _besitzer}
        If d.ShowDialog() = True Then Return d.Entwurf
        Return Nothing
    End Function

    Public Function FreigabeBestaetigen(vorlage As Freigabevorlage) As Freigabebestaetigung _
        Implements IDialoge.FreigabeBestaetigen
        Dim d As New FreigabeFenster(vorlage) With {.Owner = _besitzer}
        If d.ShowDialog() = True Then Return d.Bestaetigung
        Return Nothing
    End Function

    Public Function DateiOeffnen(titel As String, filter As String) As String _
        Implements IDialoge.DateiOeffnen
        Dim d As New OpenFileDialog With {.Filter = filter, .Title = titel}
        If d.ShowDialog(_besitzer) = True Then Return d.FileName
        Return Nothing
    End Function

    Public Function DateiSpeichernUnter(titel As String, filter As String, vorschlag As String) As String _
        Implements IDialoge.DateiSpeichernUnter
        Dim d As New SaveFileDialog With {.Filter = filter, .Title = titel, .FileName = vorschlag}
        If d.ShowDialog(_besitzer) = True Then Return d.FileName
        Return Nothing
    End Function

    Public Sub Hinweis(titel As String, text As String) Implements IDialoge.Hinweis
        MessageBox.Show(_besitzer, text, titel, MessageBoxButton.OK, MessageBoxImage.Warning)
    End Sub

    Public Function Frage(titel As String, text As String) As Boolean Implements IDialoge.Frage
        Return MessageBox.Show(_besitzer, text, titel, MessageBoxButton.YesNo, MessageBoxImage.Question) = MessageBoxResult.Yes
    End Function

End Class
