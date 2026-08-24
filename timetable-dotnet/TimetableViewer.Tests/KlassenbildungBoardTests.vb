' Verhalten des Klassenbildungs-Boards (U1-U4) gegen die echte,
' committete Ergebnisdatei.
'
' Diese Tests sind die Absicherung, die Stufe E braucht: sobald die
' Vorlage fuer die Bridge angefasst wird, muss belegbar bleiben, dass der
' bestehende Doppelklick-Betrieb unveraendert funktioniert (arc42 8.10 -
' "der Viewer bleibt eine Doppelklick-Datei ohne Server").
Imports System.Threading.Tasks
Imports Microsoft.Playwright
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
Public Class KlassenbildungBoardTests
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

    <TestMethod>
    Public Async Function BoardRendertKartenUndSpalten() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())

        Dim karten = Await Seite.Locator(".karte").CountAsync()
        Dim spalten = Await Seite.Locator(".klasse-spalte").CountAsync()

        Assert.IsTrue(karten > 0, "keine Karten gerendert - das Inline-JS ist nicht gelaufen")
        Assert.IsTrue(spalten > 0, "keine Klassenspalten gerendert")
    End Function

    ''' <summary>Der Testhook der Vorlage (window.__kbTest) ist
    ''' ausdruecklich "kein UI-Vertrag" - fuer einen Test ueber die
    ''' Bewertungslogik ist er aber genau richtig, weil er die im Browser
    ''' laufende Formel direkt befragt.</summary>
    <TestMethod>
    Public Async Function TesthookLiefertBewertungUndSicht() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())

        Dim liveFaehig = Await Seite.EvaluateAsync(Of Boolean)("() => !!window.__kbTest && window.__kbTest.liveFaehig")
        Dim kinder = Await Seite.EvaluateAsync(Of Integer)("() => Object.keys(window.__kbTest.sicht().zuordnung).length")

        Assert.IsTrue(liveFaehig, "Live-Bewertung nicht verfuegbar - fehlt der balance-Block im Export?")
        Assert.IsTrue(kinder > 0, "die Arbeits-Sicht ist leer")
    End Function

    ''' <summary>Eine Verschiebung MUSS einen F1-Pin erzeugen und die
    ''' Bewertung neu rechnen - das ist der Kern von U4.</summary>
    <TestMethod>
    Public Async Function VerschiebenErzeugtPinUndBewertetNeu() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())

        Dim frei = Await FreiesKindAsync()
        Dim ergebnis = Await Seite.EvaluateAsync(Of String)("(id) => {
            const t = window.__kbTest;
            const alt = t.sicht().zuordnung[id];
            const ziel = alt === 1 ? 2 : 1;
            t.verschiebe(id, ziel);
            return JSON.stringify({id: id, alt: alt, neu: t.sicht().zuordnung[id], pin: t.pins()[id], herkunft: t.herkunft()[id]});
        }", frei)

        Dim d = System.Text.Json.Nodes.JsonNode.Parse(ergebnis)
        Assert.AreNotEqual(d("alt").GetValue(Of Integer)(), d("neu").GetValue(Of Integer)(),
                           "das Kind wurde nicht verschoben")
        Assert.AreEqual(d("neu").GetValue(Of Integer)(), d("pin").GetValue(Of Integer)(),
                        "die Verschiebung hat keinen F1-Pin erzeugt")
        Assert.AreEqual("verschoben", d("herkunft").GetValue(Of String)(),
                        "die Pin-Herkunft ist nicht als Verschiebung markiert")
    End Function

    ''' <summary>Die zentrale Zusage von arc42 8.10: die Live-Bewertung im
    ''' Browser ist ein bewusstes, kommentiertes Duplikat von
    ''' KlassenbildungQuality.Bewerte - und die Chip-Texte muessen
    ''' ZEICHENGLEICH sein. Bis dieser Test existierte, war das eine
    ''' Absichtserklaerung; genau so ist beim Umstellen auf Klassenlabels
    ''' auffliegen koennen, dass VB "alle in 1a" sagt und JS noch "alle in
    ''' Klasse 2".
    '''
    ''' Verglichen wird die JS-Bewertung der Basis-Variante gegen die vom
    ''' VB-Kern EXPORTIERTEN Chips derselben Variante.</summary>
    <TestMethod>
    Public Async Function LiveBewertungIstZeichengleichZumKern() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())

        Dim abweichungen = Await Seite.EvaluateAsync(Of String())("() => {
            const data = JSON.parse(document.getElementById('klassenbildung-data').textContent);
            const v = data.varianten[0];
            const js = window.__kbTest.bewerte(v.zuordnung).chips;
            const schluessel = c => c.kind + '|' + c.regel_id + '|' + c.regel_typ;
            const vonVb = new Map(v.chips.map(c => [schluessel(c), c]));
            const abw = [];
            js.forEach(c => {
                const k = schluessel(c);
                const b = vonVb.get(k);
                if (!b) { abw.push('nur im Browser: ' + k); return; }
                if (b.text !== c.text) abw.push(k + ' | Kern: ' + b.text + ' | Browser: ' + c.text);
                if (b.status !== c.status) abw.push(k + ' | Status Kern: ' + b.status + ' | Browser: ' + c.status);
                vonVb.delete(k);
            });
            vonVb.forEach((_, k) => abw.push('nur im Kern: ' + k));
            return abw;
        }")

        Assert.AreEqual(0, abweichungen.Length,
                        "Live-Bewertung weicht vom Kern ab:" & vbLf & String.Join(vbLf, abweichungen))
    End Function

    ''' <summary>Klassen werden dem Nutzer IMMER als Label gezeigt (1a,
    ''' 1b, ...), nie als Laufnummer - die Nummer ist ein internes Detail
    ''' des Solvers und bleibt auf die YAML-WERTE beschraenkt.</summary>
    <TestMethod>
    Public Async Function KlassenErscheinenAlsLabelNichtAlsNummer() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())

        Dim text = Await Seite.Locator("#haupt").InnerTextAsync()
        Dim titel = String.Join(" ", Await Seite.Locator("[title]").AllInnerTextsAsync())
        Dim alleTitel = Await Seite.EvaluateAsync(Of String)("() => Array.from(document.querySelectorAll('[title]')).map(e => e.title).join(' | ')")

        Dim roh = New Text.RegularExpressions.Regex("(?i)klasse\s+\d+(?![a-z])")
        Assert.IsFalse(roh.IsMatch(text), "rohe Klassennummer im sichtbaren Text: " & roh.Match(text).Value)
        Assert.IsFalse(roh.IsMatch(alleTitel), "rohe Klassennummer in einem Tooltip: " & roh.Match(alleTitel).Value)
        StringAssert.Matches(text, New Text.RegularExpressions.Regex("\b1[ab]\b"), "es werden gar keine Labels angezeigt")
    End Function

    ''' <summary>DER Regressionstest fuer Stufe E: ohne
    ''' window.chrome.webview muss der Viewer weiterhin der
    ''' YAML-Export-Weg sein. Dass er per Doppelklick funktioniert, ist
    ''' eine dokumentierte Zusage (arc42 8.10), keine Nebensache.</summary>
    <TestMethod>
    Public Async Function OhneBridgeBleibtDerYamlExportDerWeg() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())

        Dim frei = Await FreiesKindAsync()
        Await Seite.EvaluateAsync("(id) => window.__kbTest.verschiebe(id, window.__kbTest.sicht().zuordnung[id] === 1 ? 2 : 1)", frei)

        Dim export = Await Seite.Locator("#yaml-export").InputValueAsync()
        StringAssert.Contains(export, "fixierungen:", "der YAML-Block fehlt im Doppelklick-Betrieb")
        Assert.AreEqual(0, Await Seite.Locator("#neu-rechnen").CountAsync(),
                        "die GUI-Schaltflaeche erscheint, obwohl keine Bridge da ist")
    End Function

    ''' <summary>localStorage ist der Komfort-Pfad des Doppelklick-
    ''' Betriebs. Er muss weiter greifen - und darf, wenn er scheitert,
    ''' nichts kaputtmachen (try/catch in der Vorlage).</summary>
    <TestMethod>
    Public Async Function PinsUeberlebenEinNeuladenPerLocalStorage() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())

        Dim id = Await FreiesKindAsync()
        Await Seite.EvaluateAsync("(id) => window.__kbTest.verschiebe(id, window.__kbTest.sicht().zuordnung[id] === 1 ? 2 : 1)", id)
        Dim gespeichert = Await Seite.EvaluateAsync(Of Integer)("(id) => window.__kbTest.pins()[id]", id)

        Await SeiteNeuLadenAsync()

        Dim nachher = Await Seite.EvaluateAsync(Of Integer)("(id) => window.__kbTest.pins()[id] || -1", id)
        Assert.AreEqual(gespeichert, nachher, "der Pin hat das Neuladen nicht ueberlebt")
    End Function

End Class
