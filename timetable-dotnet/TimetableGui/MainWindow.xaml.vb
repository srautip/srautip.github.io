' Code-Behind des Hauptfensters: nur Verdrahtung. Zustand und Aktionen
' liegen im HauptViewModel, damit sie ohne Fenster pruefbar sind.
Imports System.ComponentModel
Imports Microsoft.Win32

Class MainWindow

    Private ReadOnly _modell As HauptViewModel
    Private ReadOnly _host As ViewerHost
    Private ReadOnly _speicherung As Speicherstand
    Private _fuelltStandwahl As Boolean

    Public Sub New()
        InitializeComponent()

        ' Das ViewModel entsteht HIER, auf dem UI-Thread. Sein
        ' Progress(Of SolveProgress) faengt damit den richtigen
        ' SynchronizationContext ein und marshallt die Solver-Meldungen
        ' selbsttaetig zurueck (arc42 8.11).
        Dim dialoge As New WpfDialoge(Me)
        _modell = New HauptViewModel(dialoge)
        DataContext = _modell
        ' Die Pflegemasken brauchen Modell und Speicherstand; beide gibt
        ' es erst jetzt - deshalb nachtraeglich verdrahtet.
        _speicherung = New Speicherstand(_modell)
        dialoge.Verdrahte(_modell, _speicherung)

        _host = New ViewerHost(Dashboard, _modell.Auslieferung,
                               AddressOf _modell.VerarbeiteBrueckenNachricht,
                               AddressOf _modell.BrueckenStartSkript)

        AddHandler _modell.PropertyChanged, AddressOf AufModellAenderung
        AnsichtenFuellen()
        SichtbarkeitSetzen()

        InputBindings.Add(New KeyBinding(_modell.SpeichernBefehl, Key.S, ModifierKeys.Control))
        InputBindings.Add(New KeyBinding(_modell.KlassenbildungBefehl, Key.F5, ModifierKeys.None))
        InputBindings.Add(New KeyBinding(_modell.StundenplanBefehl, Key.F6, ModifierKeys.None))
    End Sub

    Private Async Sub AufModellAenderung(sender As Object, e As PropertyChangedEventArgs)
        Select Case e.PropertyName
            Case HauptViewModel.AnzeigeAktualisiert
                ' Bereich selbst hat sich nicht geaendert (erneutes Rechnen aus
                ' dem schon offenen Dashboard) - dort bliebe WebView2 sonst auf
                ' dem vorigen Stand stehen, siehe HauptViewModel.AnzeigeAktualisiert.
                KopfFuellen()
                If _modell.HatAnzeige Then Await _host.AnzeigenAsync()

            Case NameOf(HauptViewModel.Bereich), NameOf(HauptViewModel.HatAnzeige)
                SichtbarkeitSetzen()
                KopfFuellen()
                If _modell.Bereich = Bereich.Laeufe Then LaeufeAufbauen()
                If e.PropertyName = NameOf(HauptViewModel.Bereich) AndAlso
                   ZeigtRechnung() AndAlso _modell.HatAnzeige Then
                    Await _host.AnzeigenAsync()
                End If

            Case HauptViewModel.KartenAktualisiert, NameOf(HauptViewModel.Projekt)
                AnsichtenFuellen()
        End Select
    End Sub

    Private Function ZeigtRechnung() As Boolean
        Return HauptViewModel.ArtDesBereichs(_modell.Bereich).HasValue
    End Function

    Private Shared Function Sichtbar(ja As Boolean) As Visibility
        Return If(ja, Visibility.Visible, Visibility.Collapsed)
    End Function

    ''' <summary>Welche Flaeche zu sehen ist. Ein Rechnungs-Bereich zeigt
    ''' Kopf plus Dashboard - oder Kopf plus Leerseite, solange kein
    ''' Ergebnis da ist. Vorher wurde WebView2 in beiden Faellen
    ''' eingeblendet und blieb ohne Ergebnis weiss oder auf dem zuletzt
    ''' geladenen Inhalt stehen.</summary>
    Private Sub SichtbarkeitSetzen()
        Dim rechnung = ZeigtRechnung()
        Startseite.Visibility = Sichtbar(_modell.Bereich = Bereich.Start)
        Laeufe.Visibility = Sichtbar(_modell.Bereich = Bereich.Laeufe)
        Bereichskopf.Visibility = Sichtbar(rechnung)
        Dashboard.Visibility = Sichtbar(rechnung AndAlso _modell.HatAnzeige)
        Leerseite.Visibility = Sichtbar(rechnung AndAlso Not _modell.HatAnzeige)
    End Sub

    ''' <summary>Karten, Kopf und Leerseite werden NEU GEBAUT statt
    ''' gebunden: ihre Zeilen sind abgeleitete Werte, keine Eigenschaften
    ''' mit Aenderungsmeldung. Ein ItemsSource-Binding zeigte sonst den
    ''' Stand von vorhin.</summary>
    Private Sub AnsichtenFuellen()
        Startkarten.ItemsSource = _modell.Karten()
        KopfFuellen()
    End Sub

    Private Sub KopfFuellen()
        Dim art = HauptViewModel.ArtDesBereichs(_modell.Bereich)
        If Not art.HasValue Then Return

        Dim klassenbildung = art.Value = Rechnungsart.Klassenbildung
        Kopftitel.Text = _modell.KopfTitel
        Kopfzeilen.ItemsSource = _modell.KopfZeilen()
        KopfRechnen.Command = If(klassenbildung, _modell.KlassenbildungBefehl, _modell.StundenplanBefehl)
        KopfRechnen.Content = If(klassenbildung, "Klassenbildung rechnen  (F5)", "Stundenplan rechnen  (F6)")

        LeerTitel.Text = _modell.KopfTitel
        Leerzeilen.ItemsSource = _modell.Karte(art.Value).Zeilen

        StandwahlFuellen()
    End Sub

    ''' <summary>Der Stand-Wechsler zeigt die Staende der aktiven Rechnung
    ''' und markiert den, den das Dashboard gerade zeigt. Das Fuellen
    ''' loest SelectionChanged aus - die Sperre haelt das vom Modell fern.</summary>
    Private Sub StandwahlFuellen()
        _fuelltStandwahl = True
        Try
            Dim zeilen = _modell.StaendeDesBereichs()
            Standwahl.ItemsSource = zeilen
            Standwahl.IsEnabled = zeilen.Count > 0
            Dim angezeigt = _modell.AngezeigterStand()
            Standwahl.SelectedItem = If(angezeigt Is Nothing, Nothing,
                                        zeilen.FirstOrDefault(Function(z) z.Id = angezeigt.Id))
        Finally
            _fuelltStandwahl = False
        End Try
    End Sub

    Private Sub AufStandwahl(sender As Object, e As SelectionChangedEventArgs)
        If _fuelltStandwahl Then Return
        Dim z = TryCast(Standwahl.SelectedItem, Standzeile)
        If z Is Nothing Then Return
        Dim angezeigt = _modell.AngezeigterStand()
        If angezeigt IsNot Nothing AndAlso angezeigt.Id = z.Id Then Return
        _modell.StandAnzeigen(z.Id)
    End Sub

    ''' <summary>Ein Klick auf eine Zeile fuehrt ihre Aktion aus. Die
    ''' steht am Objekt, nicht in einer Fallunterscheidung hier - sonst
    ''' gaebe es zwei Orte, an denen festgelegt ist, was eine Zeile tut.</summary>
    Private Sub AufSchrittAktion(sender As Object, e As RoutedEventArgs)
        Dim knopf = TryCast(sender, Button)
        Dim zeile = TryCast(knopf?.Tag, HauptViewModel.Startschritt)
        zeile?.Aktion?.Invoke()
    End Sub

    Private Sub AufKlarnamenExport(sender As Object, e As RoutedEventArgs)
        _modell.KlarnamenExportieren()
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
        If Bildprobe.Auftrag IsNot Nothing Then Return
        If Not _modell.Geaendert Then Return
        Dim antwort = MessageBox.Show("Es gibt ungespeicherte Änderungen. Trotzdem beenden?",
                                      "Schulplanung", MessageBoxButton.YesNo, MessageBoxImage.Warning)
        If antwort <> MessageBoxResult.Yes Then e.Cancel = True
    End Sub


    ' ===============================================================
    ' Bildprobe (siehe Infrastruktur/Bildprobe.vb)
    ' ===============================================================

    Private Async Sub AufGeladen(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Dim auftrag = Bildprobe.Auftrag
        If auftrag Is Nothing Then Return
        Dim code = 0
        Try
            Await BildprobenAufnehmenAsync(auftrag)
        Catch ex As Exception
            IO.Directory.CreateDirectory(auftrag.Ordner)
            IO.File.WriteAllText(IO.Path.Combine(auftrag.Ordner, "fehler.txt"), ex.ToString())
            code = 1
        End Try
        Application.Current.Shutdown(code)
    End Sub

    ''' <summary>Der Ablauf: leere Startseite, Projekt laden, optional
    ''' rechnen, dann jeder Bereich der Seitenleiste, optional jede
    ''' Maske. Die Bilder sind durchnummeriert, damit die Reihenfolge
    ''' auch im Dateisystem erkennbar bleibt.</summary>
    ''' <summary>Laufende Nummer der Bilder. Ein Feld, kein ByRef-Parameter:
    ''' Async-Methoden duerfen keinen haben.</summary>
    Private _bildNummer As Integer

    Private Async Function BildprobenAufnehmenAsync(auftrag As BildprobeAuftrag) As Task
        IO.Directory.CreateDirectory(auftrag.Ordner)
        _bildNummer = 0

        Await BildAufnehmenAsync(auftrag, "start-ohne-projekt")

        If auftrag.Schule IsNot Nothing Then
            _modell.UebernimmProjekt(Bildprobe.ProjektAusSchulordner(auftrag.Schule, DateTimeOffset.Now))
        ElseIf auftrag.Projekt IsNot Nothing Then
            _modell.OeffneDatei(auftrag.Projekt, If(Environment.GetEnvironmentVariable("SCHULPLANUNG_PASSWORT"), ""))
        End If
        If Not _modell.ProjektOffen Then Return

        If auftrag.Rechnen = "stundenplan" Then
            Await _modell.StundenplanRechnenAsync()
        ElseIf auftrag.Rechnen = "klassenbildung" Then
            Await _modell.KlassenbildungRechnenAsync()
        End If

        For Each b In {Bereich.Start, Bereich.Klassenbildung, Bereich.Stundenplan, Bereich.Laeufe}
            _modell.Bereich = b
            Await BildAufnehmenAsync(auftrag, b.ToString().ToLowerInvariant())
        Next

        If auftrag.Masken Then
            Dim dialoge As New WpfDialoge(Me)
            Await MaskeAufnehmenAsync(auftrag, "stammdaten",
                New StammdatenFenster(_modell.Projekt, dialoge, _speicherung))
            Await MaskeAufnehmenAsync(auftrag, "regeln",
                New RegelnFenster(_modell.Projekt, dialoge, _speicherung))
            Await MaskeAufnehmenAsync(auftrag, "klassenbildung-eingaben",
                New KlassenbildungFenster(_modell.Projekt, dialoge, _speicherung))
            Await MaskeAufnehmenAsync(auftrag, "solver-einstellungen",
                New SolverEinstellungenFenster(_modell.Projekt, dialoge, _speicherung))
        End If
    End Function

    ''' <summary>Das Hauptfenster fotografieren. Zeigt der Bereich ein
    ''' Dashboard, wird auf die Seite gewartet und ihr Bild an der Stelle
    ''' des WebView2 eingeblendet - WPF selbst kann es nicht zeichnen.</summary>
    Private Async Function BildAufnehmenAsync(auftrag As BildprobeAuftrag, name As String) As Task
        Dim web As System.Windows.Media.Imaging.BitmapSource = Nothing
        If Dashboard.Visibility = Visibility.Visible AndAlso _modell.HatAnzeige Then
            Await _host.SeiteGeladen
            ' Die Seite baut sich per Inline-JS auf; NavigationCompleted
            ' kommt vor dem letzten Layout.
            Await Task.Delay(800)
            Using strom As New IO.MemoryStream()
                Await Dashboard.CoreWebView2.CapturePreviewAsync(
                    Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, strom)
                web = Bildprobe.BildAus(strom)
            End Using
        End If
        Await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Background)
        _bildNummer += 1
        Bildprobe.Speichern(Me, IO.Path.Combine(auftrag.Ordner, $"{_bildNummer:00}-{name}.png"), Dashboard, web)
    End Function

    Private Async Function MaskeAufnehmenAsync(auftrag As BildprobeAuftrag, name As String, maske As Window) As Task
        maske.Owner = Me
        maske.Show()
        Await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Background)
        _bildNummer += 1
        Bildprobe.Speichern(maske, IO.Path.Combine(auftrag.Ordner, $"{_bildNummer:00}-maske-{name}.png"))
        maske.Close()
    End Function


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
End Class

''' <summary>Die WPF-Umsetzung der Dialoge. Liegt bewusst im View - das
''' HauptViewModel kennt nur die Schnittstelle und bleibt damit ohne
''' Fenster testbar.</summary>
Friend NotInheritable Class WpfDialoge
    Implements IDialoge

    Private ReadOnly _besitzer As Window
    Private _modell As HauptViewModel
    Private _speicherung As Speicherstand

    Public Sub New(besitzer As Window)
        _besitzer = besitzer
    End Sub

    ''' <summary>Die Pflegemasken brauchen das Modell (Projekt,
    ''' Geaendert) und den Speicherstand; beides entsteht nach dem
    ''' Dialogobjekt. Ohne diese Verdrahtung (Untermasken, die nur
    ''' Datei- und Frage-Dialoge brauchen) oeffnen die Masken nichts.</summary>
    Friend Sub Verdrahte(modell As HauptViewModel, speicherung As Speicherstand)
        _modell = modell
        _speicherung = speicherung
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

    ' ---------------------------------------------------------------
    ' Pflegemasken - MODALE Dialoge ueber der Flaeche, "das Dashboard
    ' bleibt der Anker" (gui-ui-konzept.md 2). Jede Aenderung in einer
    ' Maske macht das Projekt ungespeichert - Autosave ist ausdruecklich
    ' abgelehnt (Konzept 7), also muss der Indikator stimmen.
    ' ---------------------------------------------------------------

    Public Sub StammdatenPflegen() Implements IDialoge.StammdatenPflegen
        If _modell Is Nothing Then Return
        Dim f As New StammdatenFenster(_modell.Projekt, New WpfDialoge(_besitzer), _speicherung) With {.Owner = _besitzer}
        AddHandler f.Geaendert, Sub() _modell.Geaendert = True
        f.ShowDialog()
    End Sub

    Public Sub RegelnPflegen() Implements IDialoge.RegelnPflegen
        If _modell Is Nothing Then Return
        Dim f As New RegelnFenster(_modell.Projekt, New WpfDialoge(_besitzer), _speicherung) With {.Owner = _besitzer}
        AddHandler f.Geaendert, Sub() _modell.Geaendert = True
        f.ShowDialog()
    End Sub

    ''' <summary>Die Eingaben der Klassenbildung (6.11). Eigenes Fenster,
    ''' weil die Einschulungsliste ausdruecklich NICHT die Schuelerliste
    ''' der Stammdaten ist - "die Klassenbildung laeuft VOR der
    ''' Klassenzuteilung".</summary>
    Public Sub KlassenbildungPflegen() Implements IDialoge.KlassenbildungPflegen
        If _modell Is Nothing Then Return
        Dim f As New KlassenbildungFenster(_modell.Projekt, New WpfDialoge(_besitzer), _speicherung) With {.Owner = _besitzer}
        AddHandler f.Geaendert, Sub() _modell.Geaendert = True
        f.ShowDialog()
    End Sub

    Public Sub SolverEinstellungenPflegen() Implements IDialoge.SolverEinstellungenPflegen
        If _modell Is Nothing Then Return
        Dim f As New SolverEinstellungenFenster(_modell.Projekt, New WpfDialoge(_besitzer), _speicherung) With {.Owner = _besitzer}
        AddHandler f.Geaendert, Sub() _modell.Geaendert = True
        f.ShowDialog()
    End Sub

End Class
