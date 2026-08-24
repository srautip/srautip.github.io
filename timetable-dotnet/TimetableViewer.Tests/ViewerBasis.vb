' Gemeinsame Basis der Viewer-Tests (Stufe E des GUI-Unterbaus).
'
' Warum Playwright und nicht der bisherige `--dump-dom`-Rauchtest
' (tools/viewer-smoke.ps1): der belegt nur, DASS das Inline-JS laeuft.
' Ab Stufe E aendern wir die Vorlagen selbst - dann muss VERHALTEN
' geprueft werden: echte Drag-Events, Pins, und vor allem die Bridge in
' beiden Betriebsarten. Playwright laeuft dabei headless und braucht
' KEINE Desktop-Sitzung; die Viewer-Tests bleiben damit auch auf einem
' Rechner ohne angemeldeten Benutzer lauffaehig.
'
' Benutzt den installierten Edge ueber Channel "msedge" - so entfaellt
' der Browser-Download von Playwright komplett.
Imports System.Text.Json.Nodes
Imports System.Threading.Tasks
Imports Microsoft.Playwright
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableWorkflow

Public MustInherit Class ViewerBasis

    Protected Shared _playwright As IPlaywright
    Protected Shared _browser As IBrowser

    Protected Property Seite As IPage
    Private _kontext As IBrowserContext

    Protected Shared Async Function BrowserStartenAsync() As Task
        _playwright = Await Playwright.CreateAsync()
        _browser = Await _playwright.Chromium.LaunchAsync(New BrowserTypeLaunchOptions With {
            .Channel = "msedge",
            .Headless = True,
            .Args = {"--disable-gpu", "--no-sandbox"}
        })
    End Function

    Protected Shared Async Function BrowserBeendenAsync() As Task
        If _browser IsNot Nothing Then Await _browser.CloseAsync()
        _playwright?.Dispose()
    End Function

    ''' <summary>Dieselbe synthetische Herkunft, unter der auch der
    ''' WebView2-Host ausliefert (ViewerAuslieferung.Ursprung). Nicht
    ''' Kosmetik: unter `about:blank` - dem Ergebnis von SetContentAsync -
    ''' ist localStorage nicht nutzbar, und genau den braucht der
    ''' Doppelklick-Betrieb. Ueber eine echte Herkunft zu routen bildet
    ''' zugleich die Produktionslage ab.</summary>
    Protected Const Ursprung As String = "https://viewer.local"
    Protected Const SeitenUrl As String = Ursprung & "/viewer.html"

    Private _html As String

    ''' <summary>Frischer Kontext je Test - sonst schleppte der naechste
    ''' Test den localStorage des vorigen mit, und genau der ist hier
    ''' pruefungsrelevant.</summary>
    Protected Async Function SeiteOeffnenAsync(html As String, Optional vorabSkript As String = Nothing) As Task
        _html = html
        _kontext = Await _browser.NewContextAsync()
        Seite = Await _kontext.NewPageAsync()
        ' Fehler der Seite sollen den Test rot machen, nicht stumm
        ' verschwinden - ein Viewer mit JS-Ausnahme sieht sonst nur "leer"
        ' aus.
        AddHandler Seite.PageError, Sub(s, e) Assert.Fail("JS-Fehler in der Seite: " & e)
        Await Seite.RouteAsync(Ursprung & "/**",
            Function(route) route.FulfillAsync(New RouteFulfillOptions With {
                .Status = 200, .ContentType = "text/html; charset=utf-8", .Body = _html}))
        If vorabSkript IsNot Nothing Then
            Await Seite.AddInitScriptAsync(vorabSkript)
        End If
        Await Seite.GotoAsync(SeitenUrl, New PageGotoOptions With {.WaitUntil = WaitUntilState.Load})
    End Function

    ''' <summary>Neu laden unter derselben Herkunft - so bleibt der
    ''' localStorage erhalten, genau wie beim erneuten Doppelklick auf
    ''' dieselbe Datei.</summary>
    Protected Async Function SeiteNeuLadenAsync() As Task
        Await Seite.ReloadAsync(New PageReloadOptions With {.WaitUntil = WaitUntilState.Load})
    End Function

    Protected Async Function SeiteSchliessenAsync() As Task
        If _kontext IsNot Nothing Then Await _kontext.CloseAsync()
    End Function

    ' ---------------------------------------------------------------
    ' Testdaten
    ' ---------------------------------------------------------------

    Protected Shared ReadOnly Property TestsRoot As String
        Get
            Dim dir = New IO.DirectoryInfo(AppContext.BaseDirectory)
            While dir IsNot Nothing
                Dim kandidat = IO.Path.Combine(dir.FullName, "tests", "bw-grundschule-beispiel")
                If IO.Directory.Exists(kandidat) Then Return IO.Path.Combine(dir.FullName, "tests")
                dir = dir.Parent
            End While
            Throw New InvalidOperationException("tests/-Verzeichnis nicht gefunden")
        End Get
    End Property

    ''' <summary>Baut die Klassenbildungs-Seite aus dem COMMITTETEN
    ''' Ergebnis-JSON. Bewusst ohne Solverlauf: die Tests pruefen die
    ''' Vorlage, nicht den Kern - und bleiben so in Sekunden.</summary>
    Protected Shared Function KlassenbildungSeite() As String
        Dim json = IO.File.ReadAllText(
            IO.Path.Combine(TestsRoot, "bw-grundschule-beispiel", "output", "klassenbildung.json"))
        Return KlassenbildungHtml.BuildKlassenbildungHtml(json)
    End Function

    ''' <summary>Liefert die Id eines Kindes OHNE YAML-Fixierung.
    ''' `verschiebe` steigt bei fixierten Kindern wortlos aus ("YAML-
    ''' Fixierung gewinnt") - ein Test, der zufaellig ein solches Kind
    ''' erwischt, sieht dann aus wie ein Fehler in der Verschiebung.
    ''' Gelesen wird aus dem eingebetteten JSON im DOM, nicht aus dem
    ''' Closure-Zustand der Seite.</summary>
    Protected Async Function FreiesKindAsync() As Task(Of String)
        Dim id = Await Seite.EvaluateAsync(Of String)("() => {
            const data = JSON.parse(document.getElementById('klassenbildung-data').textContent);
            const fixiert = new Set((data.fixierungen || [])
                .filter(f => f.klasse !== null && f.klasse !== undefined)
                .map(f => f.kind));
            const zuordnung = window.__kbTest.sicht().zuordnung;
            return Object.keys(zuordnung).find(k => !fixiert.has(k)) || null;
        }")
        Assert.IsNotNull(id, "kein unfixiertes Kind gefunden - Testgrundlage fehlt")
        Return id
    End Function

    Protected Shared Function StundentafelSeite(schule As String) As String
        Dim json = IO.File.ReadAllText(
            IO.Path.Combine(TestsRoot, schule, "output", "stundenplan.json"))
        Return StundentafelHtml.BuildStundentafelHtml(json)
    End Function

End Class
