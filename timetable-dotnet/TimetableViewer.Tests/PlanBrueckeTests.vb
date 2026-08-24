' Die Bruecke im STUNDENTAFEL-Viewer (Stufe G2, gui-ui-konzept.md 5).
'
' Derselbe Trick wie in BridgeTests: ein Init-Skript stellt
' `window.chrome.webview.postMessage` und sammelt die Nachrichten. Damit
' ist das Protokoll ohne WebView2 pruefbar.
'
' Die WICHTIGERE Haelfte ist die andere: der Doppelklick-Betrieb ist eine
' dokumentierte Zusage (arc42 8.10). Ein Zusatz, der ihn veraendert, ist
' kein Zusatz, sondern ein Bruch - deshalb steht hier zuerst der Test,
' dass ohne Host nichts erscheint.
Imports System.Text.Json.Nodes
Imports System.Threading.Tasks
Imports Microsoft.Playwright
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
Public Class PlanBrueckeTests
    Inherits ViewerBasis

    <ClassInitialize>
    Public Shared Async Function Aufbauen(ctx As TestContext) As Task
        Await BrowserStartenAsync()
    End Function

    <ClassCleanup>
    Public Shared Async Function Abraeumen() As Task
        Await BrowserBeendenAsync()
    End Function

    <TestCleanup>
    Public Async Function NachTest() As Task
        Await SeiteSchliessenAsync()
    End Function

    Private Shared Function HostStub(Optional parameterJson As String = "null") As String
        Return "
            window.__gesendet = [];
            window.chrome = window.chrome || {};
            window.chrome.webview = { postMessage: function (m) { window.__gesendet.push(m); } };
            window.__planParameter = " & parameterJson & ";
        "
    End Function

    Private Async Function NachrichtenAsync() As Task(Of List(Of JsonObject))
        Dim roh = Await Seite.EvaluateAsync(Of String())("() => window.__gesendet")
        Return roh.Select(Function(s) JsonNode.Parse(s).AsObject()).ToList()
    End Function

    ' ---------------------------------------------------------------
    ' Der Doppelklick-Betrieb bleibt unberuehrt
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Async Function OhneHostBleibtDieAktionsleisteVerborgen() As Task
        Await SeiteOeffnenAsync(StundentafelSeite("bw-grundschule-beispiel"))

        Assert.IsTrue(Await Seite.Locator("#gui-aktionen").IsHiddenAsync(),
                      "ein toter Knopf ist schlimmer als keiner")
        ' Und die Seite funktioniert weiter: die Loesungsuebersicht steht.
        Assert.IsTrue(Await Seite.Locator("#solutions-overview table").IsVisibleAsync())
    End Function

    ' ---------------------------------------------------------------
    ' Mit Host
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Async Function MitHostErscheintDieLeisteUndUebernimmtDieGewaehlteLoesung() As Task
        Await SeiteOeffnenAsync(StundentafelSeite("bw-grundschule-beispiel"), HostStub())

        Assert.IsTrue(Await Seite.Locator("#gui-aktionen").IsVisibleAsync())

        ' Zweite Zeile der Uebersicht waehlen, dann uebernehmen - so ist
        ' belegt, dass die MARKIERTE Loesung gemeldet wird und nicht
        ' stumpf die erste.
        Await Seite.Locator("#solutions-overview tbody tr").Nth(1).ClickAsync()
        Await Seite.Locator("#gui-uebernehmen").ClickAsync()

        Dim nachrichten = Await NachrichtenAsync()
        Assert.AreEqual(1, nachrichten.Count)
        Assert.AreEqual("plan-uebernehmen", nachrichten(0)("typ").GetValue(Of String)())
        Dim nutzlast = nachrichten(0)("nutzlast").AsObject()
        Assert.AreEqual(2, nutzlast("loesung").GetValue(Of Integer)(), "1-basiert wie in der Uebersicht")
        Assert.IsTrue(nutzlast("zuteilung").GetValue(Of Integer)() >= 1)

        StringAssert.Contains(Await Seite.Locator("#gui-rueckmeldung").InnerTextAsync(), "Lösung 2")
    End Function

    <TestMethod>
    Public Async Function NeuRechnenSchicktDieKurzParameter() As Task
        Await SeiteOeffnenAsync(StundentafelSeite("bw-grundschule-beispiel"), HostStub())

        Await Seite.Locator("#gui-budget").FillAsync("45")
        Await Seite.Locator("#gui-loesungen").FillAsync("7")
        Await Seite.Locator("#gui-rechnen").ClickAsync()

        Dim nachrichten = Await NachrichtenAsync()
        Assert.AreEqual(1, nachrichten.Count)
        Assert.AreEqual("plan-neu-rechnen", nachrichten(0)("typ").GetValue(Of String)())
        Dim nutzlast = nachrichten(0)("nutzlast").AsObject()
        Assert.AreEqual(45.0, nutzlast("zeitbudget_s").GetValue(Of Double)())
        Assert.AreEqual(7, nutzlast("max_loesungen").GetValue(Of Integer)())
    End Function

    ''' <summary>Die Felder zeigen, womit zuletzt gerechnet WURDE. Leere
    ''' Felder zwaengen den Nutzer zu raten, was gerade gilt.</summary>
    <TestMethod>
    Public Async Function DieFelderSindMitDenLaufendenWertenVorbelegt() As Task
        Await SeiteOeffnenAsync(StundentafelSeite("bw-grundschule-beispiel"),
                                HostStub("{""zeitbudget_s"": 180, ""max_loesungen"": 30}"))

        Assert.AreEqual("180", Await Seite.Locator("#gui-budget").InputValueAsync())
        Assert.AreEqual("30", Await Seite.Locator("#gui-loesungen").InputValueAsync())
    End Function

End Class
