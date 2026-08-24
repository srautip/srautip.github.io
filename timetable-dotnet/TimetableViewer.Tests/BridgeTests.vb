' Die Bruecke zwischen Board und GUI-Host (U5, Stufe E).
'
' Der Trick, der diese Tests ohne WebView2 moeglich macht: die Vorlage
' erkennt den Host an `window.chrome.webview.postMessage`. Ein
' Init-Skript, das genau das stellt und die Nachrichten in ein Array
' schreibt, macht das Nachrichtenprotokoll vollstaendig pruefbar - ohne
' Browser-Einbettung, ohne Fenster, ohne Desktop-Sitzung.
'
' Geprueft wird beides: dass der GUI-Betrieb tut, was er soll, UND dass
' der Doppelklick-Betrieb davon unberuehrt bleibt.
Imports System.Text.Json.Nodes
Imports System.Threading.Tasks
Imports Microsoft.Playwright
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
Public Class BridgeTests
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

    ''' <summary>Stellt den Host nach: postMessage sammelt in
    ''' window.__gesendet. Optional werden Zustand und Anzeige-Map
    ''' injiziert - genau der Weg, den der echte Host ueber
    ''' AddScriptToExecuteOnDocumentCreated nimmt.</summary>
    Private Shared Function HostStub(Optional zustandJson As String = "null",
                                      Optional namenJson As String = "null") As String
        Return "
            window.__gesendet = [];
            window.chrome = window.chrome || {};
            window.chrome.webview = { postMessage: function (m) { window.__gesendet.push(m); } };
            window.__gastZustand = " & zustandJson & ";
            window.__anzeigeNamen = " & namenJson & ";
        "
    End Function

    Private Async Function NachrichtenAsync() As Task(Of List(Of JsonObject))
        Dim roh = Await Seite.EvaluateAsync(Of String())("() => window.__gesendet")
        Return roh.Select(Function(s) JsonNode.Parse(s).AsObject()).ToList()
    End Function

    ' ---------------------------------------------------------------
    ' Betriebsart-Erkennung
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Async Function OhneHostIstDieBrueckeAus() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())

        Assert.IsFalse(Await Seite.EvaluateAsync(Of Boolean)("() => window.__kbTest.brueckeAktiv"))
        Assert.AreEqual(1, Await Seite.Locator("#yaml-export").CountAsync(),
                        "der YAML-Export fehlt im Doppelklick-Betrieb")
        Assert.AreEqual(0, Await Seite.Locator("#neu-rechnen").CountAsync())
    End Function

    <TestMethod>
    Public Async Function MitHostErsetztDieSchaltflaecheDenYamlBlock() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite(), HostStub())

        Assert.IsTrue(Await Seite.EvaluateAsync(Of Boolean)("() => window.__kbTest.brueckeAktiv"))
        Assert.AreEqual(1, Await Seite.Locator("#neu-rechnen").CountAsync(),
                        "die Neu-rechnen-Schaltflaeche fehlt im GUI-Betrieb")
        Assert.AreEqual(0, Await Seite.Locator("#yaml-export").CountAsync(),
                        "der YAML-Block steht noch da, obwohl der Host ihn ersetzt")
    End Function

    ' ---------------------------------------------------------------
    ' JS -> Host
    ' ---------------------------------------------------------------

    ''' <summary>Jede Zustandsaenderung geht als versionierter Umschlag
    ''' an den Host - er schreibt sie nach gui-state.json und protokolliert
    ''' die Fixierung (Datenhaltung 7.3).</summary>
    <TestMethod>
    Public Async Function VerschiebenMeldetDenZustandAnDenHost() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite(), HostStub())
        Dim frei = Await FreiesKindAsync()

        Await Seite.EvaluateAsync("(id) => window.__kbTest.verschiebe(id, window.__kbTest.sicht().zuordnung[id] === 1 ? 2 : 1)", frei)

        Dim nachrichten = Await NachrichtenAsync()
        Assert.IsTrue(nachrichten.Count > 0, "es wurde nichts an den Host gemeldet")
        Dim letzte = nachrichten.Last()
        Assert.AreEqual(1, letzte("v").GetValue(Of Integer)(), "Umschlag ohne Version")
        Assert.AreEqual("zustand", letzte("typ").GetValue(Of String)())
        Dim pins = letzte("nutzlast")("pins").AsObject()
        Assert.IsTrue(pins.ContainsKey(frei), "der neue Pin fehlt in der Meldung")
    End Function

    ''' <summary>Der eigentliche U5-Schritt: aus dem Board heraus neu
    ''' rechnen. Der Host bekommt die Fixierungen strukturiert - mit
    ''' denselben Feldnamen wie das YAML, damit er sie ohne
    ''' Uebersetzungstabelle uebernehmen kann.</summary>
    <TestMethod>
    Public Async Function NeuRechnenSendetFixierungenUndHaertungen() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite(), HostStub())
        Dim frei = Await FreiesKindAsync()
        Await Seite.EvaluateAsync("(id) => window.__kbTest.verschiebe(id, window.__kbTest.sicht().zuordnung[id] === 1 ? 2 : 1)", frei)

        Await Seite.Locator("#neu-rechnen").ClickAsync()

        Dim nachrichten = Await NachrichtenAsync()
        Dim rechnen = nachrichten.LastOrDefault(Function(n) n("typ").GetValue(Of String)() = "neu-rechnen")
        Assert.IsNotNull(rechnen, "Klick auf 'Neu rechnen' hat nichts gesendet")
        Assert.AreEqual(1, rechnen("v").GetValue(Of Integer)())

        Dim fixierungen = rechnen("nutzlast")("fixierungen").AsArray()
        Assert.IsTrue(fixierungen.Count > 0, "keine Fixierungen mitgesendet")
        Dim meine = fixierungen.FirstOrDefault(Function(f) f("kind").GetValue(Of String)() = frei)
        Assert.IsNotNull(meine, "die eigene Verschiebung fehlt in der Fixierungsliste")
        Assert.AreEqual("verschoben", meine("herkunft").GetValue(Of String)())
        Assert.IsTrue(meine.AsObject().ContainsKey("klasse"), "Feldname weicht vom YAML ab")
        Assert.IsNotNull(rechnen("nutzlast")("haertungen"))
    End Function

    ''' <summary>Ein abwesender oder kaputter Host darf das Board nicht
    ''' lahmlegen - der Nutzer arbeitet mitten in einer Konferenz.</summary>
    <TestMethod>
    Public Async Function KaputterHostLegtDasBoardNichtLahm() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite(), "
            window.chrome = { webview: { postMessage: function () { throw new Error('Host weg'); } } };
        ")
        Dim frei = Await FreiesKindAsync()

        Dim vorher = Await Seite.EvaluateAsync(Of Integer)("(id) => window.__kbTest.sicht().zuordnung[id]", frei)
        Await Seite.EvaluateAsync("(id) => window.__kbTest.verschiebe(id, window.__kbTest.sicht().zuordnung[id] === 1 ? 2 : 1)", frei)
        Dim nachher = Await Seite.EvaluateAsync(Of Integer)("(id) => window.__kbTest.sicht().zuordnung[id]", frei)

        Assert.AreNotEqual(vorher, nachher, "die Verschiebung ist am Host-Fehler gescheitert")
        Assert.IsTrue(Await Seite.Locator(".karte").CountAsync() > 0, "das Board ist zusammengebrochen")
    End Function

    ' ---------------------------------------------------------------
    ' Host -> JS
    ' ---------------------------------------------------------------

    ''' <summary>Im GUI-Betrieb kommt der Zustand vom Host, nicht aus dem
    ''' localStorage - die Projektdatei ist die Wahrheit (Datenhaltung
    ''' 7.6).</summary>
    <TestMethod>
    Public Async Function ZustandVomHostWirdUebernommen() As Task
        Dim vorbereitet = "{""pins"": {""S001"": 3}, ""herkunft"": {""S001"": ""konsens""}, ""notPins"": {}, ""haertungen"": {""gruppen"": {}, ""wuensche"": {}}}"
        Await SeiteOeffnenAsync(KlassenbildungSeite(), HostStub(zustandJson:=vorbereitet))

        Assert.AreEqual(3, Await Seite.EvaluateAsync(Of Integer)("() => window.__kbTest.pins()['S001']"),
                        "der injizierte Pin wurde nicht uebernommen")
        Assert.AreEqual("konsens", Await Seite.EvaluateAsync(Of String)("() => window.__kbTest.herkunft()['S001']"))
    End Function

    ''' <summary>Klarnamen erscheinen im DOM - und NUR dort. Das
    ''' eingebettete JSON bleibt pseudonym, sonst waere die harte
    ''' Export-Grenze des Datenhaltungskonzepts (6.2) durchbrochen.</summary>
    <TestMethod>
    Public Async Function KlarnamenNurImDomNichtImEingebettetenJson() As Task
        Dim namen = "{""S001"": ""Mia M.""}"
        Await SeiteOeffnenAsync(KlassenbildungSeite(), HostStub(namenJson:=namen))

        Dim dom = Await Seite.Locator("#board").InnerTextAsync()
        StringAssert.Contains(dom, "Mia M.", "der Klarname wird nicht angezeigt")
        StringAssert.Contains(dom, "S001", "die Pseudonym-Id muss sichtbar bleiben")

        Dim eingebettet = Await Seite.Locator("#klassenbildung-data").InnerTextAsync()
        Assert.IsFalse(eingebettet.Contains("Mia"),
                       "der Klarname ist ins eingebettete JSON gelangt - Export-Grenze verletzt")
    End Function

    ''' <summary>Ohne Anzeige-Map bleibt es bei der reinen Id - der
    ''' Doppelklick-Betrieb kennt keine Klarnamen.</summary>
    <TestMethod>
    Public Async Function OhneAnzeigeMapBleibtDieIdStehen() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())

        Dim ersteKarte = Await Seite.Locator(".karte .kind-id").First.InnerTextAsync()
        StringAssert.Matches(ersteKarte, New Text.RegularExpressions.Regex("^S\d+$"),
                             $"erwartet wurde eine reine Id, angezeigt wird '{ersteKarte}'")
    End Function

End Class
