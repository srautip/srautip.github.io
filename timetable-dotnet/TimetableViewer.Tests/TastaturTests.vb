' Tastaturbedienung der Viewer (Designsystem, Stufe 5).
'
' klassenbildung-ui-konzept.md:203 sagt sie ausdruecklich zu:
' "alle Zustaende doppelt kodiert (Farbe + Symbol/Text), Fokus-Reihenfolge
' Panel -> Board, Aktionen per Tastatur (Pin = Enter auf fokussierter
' Karte)". Eingeloest war davon bis zu diesem Schritt nichts - es gab
' weder eine :focus-Regel noch ein einziges tabindex.
'
' Diese Tests sind der Grund, warum die Zusage jetzt eine ist und nicht
' wieder nur eine Absichtserklaerung.
Imports System.Threading.Tasks
Imports Microsoft.Playwright
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
Public Class TastaturTests
    Inherits ViewerBasis

    <ClassInitialize>
    Public Shared Function VorAllen(kontext As TestContext) As Task
        Return BrowserStartenAsync()
    End Function

    <ClassCleanup>
    Public Shared Function NachAllen() As Task
        Return BrowserBeendenAsync()
    End Function

    <TestCleanup>
    Public Function NachTest() As Task
        Return SeiteSchliessenAsync()
    End Function

    ''' <summary>Die Kernzusage des Konzepts. Fokus auf eine Karte,
    ''' Enter - das Kind ist fixiert. Geprueft wird am Pin-Zustand des
    ''' Viewers, nicht am DOM: der Pin ist die Wahrheit, die Darstellung
    ''' nur ihre Anzeige.</summary>
    <TestMethod>
    Public Async Function EnterAufDerKarteFixiertDasKind() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())
        Dim id = Await FreiesKindAsync()

        Dim vorher = Await Seite.EvaluateAsync(Of Boolean)("(id) => window.__kbTest.pins().hasOwnProperty(id)", id)
        Assert.IsFalse(vorher, "Testgrundlage: das Kind darf noch nicht fixiert sein.")

        Dim karte = Seite.Locator($".karte[data-kind='{id}']")
        Await karte.FocusAsync()
        Await Seite.Keyboard.PressAsync("Enter")

        Dim nachher = Await Seite.EvaluateAsync(Of Boolean)("(id) => window.__kbTest.pins().hasOwnProperty(id)", id)
        Assert.IsTrue(nachher, "Enter auf der fokussierten Karte hat nicht fixiert.")

        ' Und wieder loesen - eine Aktion, die nur in eine Richtung
        ' funktioniert, waere eine Falle.
        Await Seite.Locator($".karte[data-kind='{id}']").FocusAsync()
        Await Seite.Keyboard.PressAsync("Enter")
        Dim wiederFrei = Await Seite.EvaluateAsync(Of Boolean)("(id) => !window.__kbTest.pins().hasOwnProperty(id)", id)
        Assert.IsTrue(wiederFrei, "Enter hat die Fixierung nicht wieder geloest.")
    End Function

    ''' <summary>Jedes Element, das auf einen Klick reagiert, muss auch
    ''' fokussierbar sein - sonst gibt es Funktionen, die nur die Maus
    ''' erreicht. Badges sind bewusst ausgenommen: sie sitzen zu Dutzenden
    ''' je Karte, und ihre Funktion ist ueber die Panel-Zeile
    ''' erreichbar.</summary>
    <TestMethod>
    Public Async Function AlleHandlungsElementeSindErreichbar() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())
        For Each auswahl In {".karte", ".gruppe-zeile", ".variante-kachel"}
            Dim gesamt = Await Seite.Locator(auswahl).CountAsync()
            Dim erreichbar = Await Seite.Locator(auswahl & "[tabindex='0']").CountAsync()
            Assert.IsTrue(gesamt > 0, $"{auswahl}: nichts gefunden - Testgrundlage fehlt.")
            Assert.AreEqual(gesamt, erreichbar,
                $"{auswahl}: {gesamt - erreichbar} von {gesamt} sind per Tastatur nicht erreichbar.")
        Next
    End Function

    ''' <summary>Fokus-Reihenfolge Panel -> Board. Sie ergibt sich aus dem
    ''' DOM; der Test haelt fest, dass das so bleibt - eine spaetere
    ''' Umstellung des Layouts wuerde es sonst still kippen.</summary>
    <TestMethod>
    Public Async Function DieFokusReihenfolgeLaeuftVomPanelZumBoard() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())
        Dim reihenfolge = Await Seite.EvaluateAsync(Of Integer)("() => {
            const alle = Array.from(document.querySelectorAll('[tabindex=""0""]'));
            const ersteZeile = alle.findIndex(e => e.classList.contains('gruppe-zeile'));
            const ersteKarte = alle.findIndex(e => e.classList.contains('karte'));
            if (ersteZeile < 0 || ersteKarte < 0) return -1;
            return ersteZeile < ersteKarte ? 1 : 0;
        }")
        Assert.AreEqual(1, reihenfolge,
            "Die erste Panel-Zeile muss in der Tab-Kette vor der ersten Karte liegen (Konzept 8).")
    End Function

    ''' <summary>Der Fokusring darf NUR bei Tastaturbedienung erscheinen.
    ''' Ein Ring nach jedem Mausklick waere optisches Rauschen - deshalb
    ''' `:focus-visible` und nicht `:focus`. Geprueft ueber die
    ''' tatsaechliche Aussenlinie, nicht ueber den Regeltext.</summary>
    <TestMethod>
    Public Async Function DerFokusringErscheintNurBeiTastaturbedienung() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())
        Dim id = Await FreiesKindAsync()
        Dim auswahl = $".karte[data-kind='{id}']"

        Await Seite.Locator(auswahl).ClickAsync()
        Dim nachKlick = Await Seite.EvaluateAsync(Of String)(
            "(s) => getComputedStyle(document.querySelector(s)).outlineStyle", auswahl)

        Await Seite.Locator(auswahl).FocusAsync()
        Await Seite.Keyboard.PressAsync("Tab")
        Await Seite.Keyboard.PressAsync("Shift+Tab")
        Dim nachTaste = Await Seite.EvaluateAsync(Of String)(
            "(s) => getComputedStyle(document.querySelector(s)).outlineStyle", auswahl)

        Assert.AreEqual("none", nachKlick, "Nach einem Mausklick darf kein Fokusring stehen.")
        Assert.AreNotEqual("none", nachTaste, "Nach Tastaturbedienung MUSS ein Fokusring stehen.")
    End Function

    ''' <summary>Die Loesungsuebersicht der Stundentafel war bis hierher
    ''' ausschliesslich per Maus bedienbar - Sortieren und Auswaehlen
    ''' haengen an Klick-Handlern auf `th` und `tr`, die von Haus aus
    ''' keinen Fokus annehmen.</summary>
    <TestMethod>
    Public Async Function LoesungsuebersichtIstPerTastaturBedienbar() As Task
        Await SeiteOeffnenAsync(StundentafelSeite("bw-grundschule-beispiel"))

        Dim zeilen = Await Seite.Locator("#solutions-overview tbody tr[tabindex='0']").CountAsync()
        Dim koepfe = Await Seite.Locator("#solutions-overview thead th[tabindex='0']").CountAsync()
        Assert.IsTrue(zeilen > 1, $"Nur {zeilen} fokussierbare Loesungszeilen.")
        Assert.IsTrue(koepfe > 1, $"Nur {koepfe} fokussierbare Spaltenkoepfe.")

        ' Enter auf einer anderen Zeile waehlt jene Loesung aus.
        Dim zweite = Seite.Locator("#solutions-overview tbody tr").Nth(1)
        Await zweite.FocusAsync()
        Await Seite.Keyboard.PressAsync("Enter")
        Dim aktiv = Await Seite.Locator("#solutions-overview tbody tr.active").CountAsync()
        Assert.AreEqual(1, aktiv, "Nach Enter muss genau eine Zeile als aktiv markiert sein.")
        Dim istZweite = Await zweite.EvaluateAsync(Of Boolean)("e => e.classList.contains('active')")
        Assert.IsTrue(istZweite, "Enter hat nicht die fokussierte Zeile ausgewaehlt.")
    End Function

End Class

''' <summary>Dichteschalter (Designsystem, Stufe 5). Der Nutzer hat
''' "umschaltbar kompakt/komfortabel" gewaehlt - diese Tests halten
''' fest, dass beide Stufen wirken UND die Wahl einen Neustart
''' ueberlebt.</summary>
<TestClass>
Public Class DichteTests
    Inherits ViewerBasis

    <ClassInitialize>
    Public Shared Function VorAllen(kontext As TestContext) As Task
        Return BrowserStartenAsync()
    End Function

    <ClassCleanup>
    Public Shared Function NachAllen() As Task
        Return BrowserBeendenAsync()
    End Function

    <TestCleanup>
    Public Function NachTest() As Task
        Return SeiteSchliessenAsync()
    End Function

    Private Async Function KartenhoeheAsync() As Task(Of Double)
        Dim kasten = Await Seite.Locator(".karte").First.BoundingBoxAsync()
        Return kasten.Height
    End Function

    ''' <summary>"Kompakt" muss der bisherigen Dichte entsprechen - der
    ''' Umbau darf niemandem Flaeche wegnehmen, der sie heute nutzt.
    ''' Komfortabel ist der ZUGEWINN, nicht der Normalfall.</summary>
    <TestMethod>
    Public Async Function KomfortabelMachtDieZeilenHoeherUndKompaktIstDerStandard() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())

        Dim kompakt = Await KartenhoeheAsync()
        Assert.IsTrue(kompakt <= 26, $"Standard ist nicht kompakt: {kompakt} px statt hoechstens 26.")

        Dim schalter = Seite.Locator("#controls label:has-text('komfortable Dichte') input")
        Assert.AreEqual(1, Await schalter.CountAsync(), "Der Dichteschalter fehlt in der Bedienleiste.")
        Await schalter.CheckAsync()

        Dim komfortabel = Await KartenhoeheAsync()
        Assert.IsTrue(komfortabel > kompakt + 4,
            $"Komfortabel wirkt nicht: {kompakt} px -> {komfortabel} px.")
    End Function

    ''' <summary>Die Wahl gilt dem Bildschirm, nicht dem Projekt -
    ''' deshalb localStorage und deshalb muss sie ein Neuladen
    ''' ueberleben. Geprueft unter derselben Herkunft, unter der auch der
    ''' WebView2-Host ausliefert.</summary>
    <TestMethod>
    Public Async Function DieGewaehlteDichteUeberlebtEinNeuladen() As Task
        Await SeiteOeffnenAsync(KlassenbildungSeite())
        Await Seite.Locator("#controls label:has-text('komfortable Dichte') input").CheckAsync()
        Dim vorher = Await KartenhoeheAsync()

        Await SeiteNeuLadenAsync()

        Dim gesetzt = Await Seite.EvaluateAsync(Of String)(
            "() => document.documentElement.getAttribute('data-dichte')")
        Assert.AreEqual("komfortabel", gesetzt, "Die Dichte hat das Neuladen nicht ueberlebt.")
        Assert.AreEqual(vorher, Await KartenhoeheAsync(), 0.6, "Die Zeilenhoehe stimmt nach dem Neuladen nicht.")
    End Function

    ''' <summary>Die Stundentafel schrumpfte ihr Raster bisher unbegrenzt
    ''' herunter. Unterhalb der Lesbarkeitsgrenze muss sie stattdessen
    ''' scrollen - und dann greifen auch die sticky-Kopfzeilen, die im
    ''' frueheren overflow:hidden wirkungslos waren.</summary>
    <TestMethod>
    Public Async Function DasRasterSchrumpftNichtUnterDieLesbarkeitsgrenze() As Task
        Await SeiteOeffnenAsync(StundentafelSeite("bw-gms-beispiel"))
        Await Seite.SetViewportSizeAsync(560, 800)
        Await Seite.EvaluateAsync("() => window.dispatchEvent(new Event('resize'))")
        Await Seite.WaitForTimeoutAsync(400)

        Dim skala = Await Seite.EvaluateAsync(Of Double)("() => {
            const t = document.querySelector('#table-container table');
            const m = /scale\(([0-9.]+)\)/.exec(t.style.transform || '');
            return m ? parseFloat(m[1]) : 1;
        }")
        Assert.IsTrue(skala >= 0.719, $"Das Raster wurde auf {skala} verkleinert - unter die Lesbarkeitsgrenze.")

        Dim scrollt = Await Seite.EvaluateAsync(Of String)(
            "() => getComputedStyle(document.getElementById('table-container')).overflowX")
        Assert.AreEqual("auto", scrollt, "Bei Mindestgroesse muss der Behaelter scrollen koennen.")
    End Function

End Class
