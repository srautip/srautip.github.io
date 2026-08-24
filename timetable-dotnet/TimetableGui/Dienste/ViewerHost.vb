' Bindet eine ViewerAuslieferung an ein WebView2-Steuerelement.
'
' Bewusst duenn: alles, was ohne Browser pruefbar ist, liegt in
' ViewerAuslieferung. Hier bleibt nur die Verdrahtung - und die drei
' Einstellungen, die aus einem eingebetteten Browser einen
' vertrauenswuerdigen Anzeigerahmen machen.
Imports System.IO
Imports System.Threading.Tasks
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.Wpf

Public NotInheritable Class ViewerHost

    ''' <summary>App-eigener Ablageort statt des Defaults neben der EXE -
    ''' ausdrueckliche Vorgabe des Datenhaltungskonzepts (7.6), damit die
    ''' Browserdaten auffindbar und geziehlt loeschbar sind.</summary>
    Public Shared ReadOnly Property BenutzerDatenOrdner As String
        Get
            Return IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Schulplanung", "WebView2")
        End Get
    End Property

    Private ReadOnly _sicht As WebView2
    Private ReadOnly _auslieferung As ViewerAuslieferung
    Private ReadOnly _aufNachricht As Action(Of String)
    Private ReadOnly _startSkript As Func(Of String)
    Private _bereit As Boolean
    Private _skriptId As String

    Public Sub New(sicht As WebView2, auslieferung As ViewerAuslieferung,
                    Optional aufNachricht As Action(Of String) = Nothing,
                    Optional startSkript As Func(Of String) = Nothing)
        _sicht = sicht
        _auslieferung = auslieferung
        _aufNachricht = aufNachricht
        _startSkript = startSkript
    End Sub

    Public Async Function VorbereitenAsync() As Task
        If _bereit Then Return
        Directory.CreateDirectory(BenutzerDatenOrdner)
        Dim umgebung = Await CoreWebView2Environment.CreateAsync(Nothing, BenutzerDatenOrdner, Nothing)
        Await _sicht.EnsureCoreWebView2Async(umgebung)

        Dim kern = _sicht.CoreWebView2
        ' Ein Anzeigerahmen, kein Browser: kein Kontextmenue, keine
        ' Entwicklerwerkzeuge, keine neuen Fenster.
        kern.Settings.AreDefaultContextMenusEnabled = False
        kern.Settings.AreDevToolsEnabled = False
        kern.Settings.IsStatusBarEnabled = False
        AddHandler kern.NewWindowRequested,
            Sub(s, e) e.Handled = True

        ' Nur unsere synthetische Herkunft wird beantwortet; alles andere
        ' laeuft ins Leere (siehe ViewerAuslieferung).
        kern.AddWebResourceRequestedFilter(_auslieferung.Filtermuster, CoreWebView2WebResourceContext.All)
        AddHandler kern.WebResourceRequested, AddressOf AufAnfrage

        ' Navigation nach draussen unterbinden - die Seiten sind
        ' self-contained und haben nichts nachzuladen (Konzept 7.6:
        ' "kein Netzzugriff").
        AddHandler kern.NavigationStarting,
            Sub(s, e)
                If Not e.Uri.StartsWith(ViewerAuslieferung.Ursprung, StringComparison.OrdinalIgnoreCase) Then
                    e.Cancel = True
                End If
            End Sub

        ' Die Bruecke (U5): Nachrichten der Seite entgegennehmen. Der
        ' Handler laeuft auf dem UI-Thread, weil WebView2 seine Ereignisse
        ' dorthin zustellt.
        If _aufNachricht IsNot Nothing Then
            AddHandler kern.WebMessageReceived,
                Sub(s, e)
                    ' TryGetWebMessageAsString wirft, wenn die Seite ein
                    ' Objekt statt einer Zeichenkette gepostet hat -
                    ' unsere Vorlage tut das nicht, aber der Host darf an
                    ' einer fremden Nachricht nicht sterben.
                    Try
                        _aufNachricht(e.TryGetWebMessageAsString())
                    Catch ex As ArgumentException
                        ' Nachricht in unerwartetem Format - ignorieren.
                    End Try
                End Sub
        End If

        _bereit = True
    End Function

    ''' <summary>Hinterlegt das Startskript (Board-Zustand und
    ''' Anzeige-Map) fuer die NAECHSTE Navigation. Muss vor dem Laden
    ''' geschehen, weil die Vorlage den Zustand beim Initialisieren liest;
    ''' ein spaeteres ExecuteScript kaeme zu spaet.</summary>
    Public Async Function ZustandVorbereitenAsync() As Task
        If _startSkript Is Nothing Then Return
        Await VorbereitenAsync()
        ' Das vorige Skript entfernen - sonst stapeln sich mit jedem Lauf
        ' veraltete Zustaende. (Entfernen ist synchron, anders als das
        ' Hinzufuegen - an der installierten DLL 1.0.2903.40 nachgesehen,
        ' nicht angenommen.)
        If _skriptId IsNot Nothing Then
            _sicht.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(_skriptId)
        End If
        _skriptId = Await _sicht.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(_startSkript())
    End Function

    Private Sub AufAnfrage(sender As Object, e As CoreWebView2WebResourceRequestedEventArgs)
        Dim antwort = _auslieferung.Antwort(e.Request.Uri)
        Dim strom As Stream = If(antwort.Gefunden, New MemoryStream(antwort.Inhalt), New MemoryStream())
        e.Response = _sicht.CoreWebView2.Environment.CreateWebResourceResponse(
            strom, antwort.Status, If(antwort.Gefunden, "OK", "Not Found"), antwort.Kopfzeilen)
    End Sub

    ''' <summary>Zeigt die aktuell hinterlegte Seite. Reload statt
    ''' Navigate, wenn die URL schon anliegt - sonst haelt WebView2 die
    ''' Navigation auf dieselbe Adresse fuer ueberfluessig und der neue
    ''' Lauf waere unsichtbar.</summary>
    Public Async Function AnzeigenAsync() As Task
        Await VorbereitenAsync()
        Await ZustandVorbereitenAsync()
        Dim ziel = _auslieferung.SeitenUrl
        If String.Equals(_sicht.Source?.ToString(), ziel, StringComparison.OrdinalIgnoreCase) Then
            _sicht.CoreWebView2.Reload()
        Else
            _sicht.CoreWebView2.Navigate(ziel)
        End If
    End Function

    ''' <summary>"Browserdaten bereinigen" (Konzept 7.6, Menue Extras).
    ''' Loescht das Profil auf der Platte; wirkt erst nach dem
    ''' Neustart, weil ein laufendes WebView2 seine Dateien haelt.</summary>
    Public Shared Function ProfilLoeschen() As Boolean
        If Not Directory.Exists(BenutzerDatenOrdner) Then Return True
        Try
            Directory.Delete(BenutzerDatenOrdner, recursive:=True)
            Return True
        Catch ex As IOException
            Return False
        Catch ex As UnauthorizedAccessException
            Return False
        End Try
    End Function

End Class
