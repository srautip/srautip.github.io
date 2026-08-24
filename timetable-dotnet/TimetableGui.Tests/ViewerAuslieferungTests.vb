' Die Auslieferung der Viewer-Seiten an WebView2.
'
' Das Datenhaltungskonzept (7.6) liess offen, ob NavigateToString oder
' virtuelles Host-Mapping benutzt wird. Beides scheidet aus - der
' Groessentest unten ist der Grund fuer die eine Haelfte dieser
' Entscheidung, und er faehrt bewusst gegen die ECHTE GMS-Stundentafel
' statt gegen einen synthetischen Puffer: die 2,49 MB sind kein
' theoretischer Grenzfall, sondern der groesste committete Datensatz.
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableGui

<TestClass>
Public Class ViewerAuslieferungTests

    Private Shared ReadOnly Property TestsRoot As String
        Get
            Dim dir = New IO.DirectoryInfo(AppContext.BaseDirectory)
            While dir IsNot Nothing
                Dim kandidat = IO.Path.Combine(dir.FullName, "tests", "bw-gms-beispiel")
                If IO.Directory.Exists(kandidat) Then Return IO.Path.Combine(dir.FullName, "tests")
                dir = dir.Parent
            End While
            Throw New InvalidOperationException("tests/-Verzeichnis nicht gefunden")
        End Get
    End Property

    <TestMethod>
    Public Sub SeiteWirdUnterDerEigenenUrlAusgeliefert()
        Dim a As New ViewerAuslieferung()
        a.Setze("<html><body>Hallo</body></html>")

        Dim antwort = a.Antwort(a.SeitenUrl)

        Assert.IsTrue(antwort.Gefunden)
        Assert.AreEqual(200, antwort.Status)
        Assert.AreEqual("<html><body>Hallo</body></html>", Encoding.UTF8.GetString(antwort.Inhalt))
        StringAssert.Contains(antwort.Kopfzeilen, "text/html")
    End Sub

    ''' <summary>Die Seiten sind self-contained und haben nichts
    ''' nachzuladen. Alles ausserhalb der eigenen URL muss deshalb ins
    ''' Leere laufen - sonst waere die Auslieferung ein offener Proxy fuer
    ''' die eingebettete Seite.</summary>
    <DataTestMethod>
    <DataRow("https://viewer.local/etwas-anderes.html")>
    <DataRow("https://example.com/viewer.html")>
    <DataRow("file:///C:/Windows/win.ini")>
    <DataRow("kaputt")>
    <DataRow("")>
    Public Sub FremdeUrlsWerdenNichtBeantwortet(url As String)
        Dim a As New ViewerAuslieferung()
        a.Setze("<html></html>")

        Dim antwort = a.Antwort(url)

        Assert.IsFalse(antwort.Gefunden, $"'{url}' wurde ausgeliefert")
        Assert.AreEqual(404, antwort.Status)
        Assert.AreEqual(0, antwort.Inhalt.Length)
    End Sub

    ''' <summary>Der eigentliche Grund fuer WebResourceRequested:
    ''' NavigateToString hat eine dokumentierte Grenze von rund 2 MB, und
    ''' die Stundentafel-Seite ueberschreitet sie in realistischen
    ''' Konfigurationen.
    '''
    ''' WICHTIG - die Groesse ist LAUF- UND KONFIGURATIONSABHAENGIG, kein
    ''' fester Wert: der Export enthaelt ALLE gefundenen Loesungen, also
    ''' skaliert er mit max_solutions und Schulgroesse. Gemessen wurden am
    ''' GMS-Beispiel schon 2,49 MB (28 Loesungen); ein spaeterer Lauf
    ''' derselben Schule lieferte 20 Loesungen und 1,77 MB. Deshalb prueft
    ''' dieser Test gegen eine SYNTHETISCH auf ueber 2 MB gebrachte Seite
    ''' statt gegen die jeweils committete Datei - sonst haenge die
    ''' Aussagekraft am Zufall des letzten Beispiel-Laufs.</summary>
    <TestMethod>
    Public Sub SeiteUeberZweiMegabyteWirdUnveraendertAusgeliefert()
        Dim grundlage = IO.File.ReadAllText(IO.Path.Combine(TestsRoot, "bw-gms-beispiel", "output", "stundentafel.html"))
        Dim bau As New StringBuilder(grundlage)
        While New UTF8Encoding(False).GetByteCount(bau.ToString()) <= 2 * 1024 * 1024
            bau.Append(grundlage)
        End While
        Dim html = bau.ToString()
        Dim erwarteteBytes = New UTF8Encoding(False).GetByteCount(html)

        Dim a As New ViewerAuslieferung()
        a.Setze(html)

        Assert.IsTrue(erwarteteBytes > 2 * 1024 * 1024)
        Assert.AreEqual(erwarteteBytes, a.SeitenGroesse)
        Dim antwort = a.Antwort(a.SeitenUrl)
        Assert.IsTrue(antwort.Gefunden)
        Assert.AreEqual(erwarteteBytes, antwort.Inhalt.Length)
        Assert.AreEqual(html, Encoding.UTF8.GetString(antwort.Inhalt), "Inhalt hat sich beim Ausliefern veraendert")
    End Sub

    ''' <summary>Und derselbe Weg mit der ECHTEN, aktuell committeten
    ''' GMS-Stundentafel - ohne Groessen-Behauptung, weil die sich mit
    ''' jedem Beispiel-Lauf aendert.</summary>
    <TestMethod>
    Public Sub EchteGmsSeiteWirdUnveraendertAusgeliefert()
        Dim html = IO.File.ReadAllText(IO.Path.Combine(TestsRoot, "bw-gms-beispiel", "output", "stundentafel.html"))

        Dim a As New ViewerAuslieferung()
        a.Setze(html)

        Dim antwort = a.Antwort(a.SeitenUrl)
        Assert.IsTrue(antwort.Gefunden)
        Assert.AreEqual(html, Encoding.UTF8.GetString(antwort.Inhalt))
    End Sub

    ''' <summary>Die Seite aendert sich bei jedem Lauf unter DERSELBEN URL.
    ''' Ohne no-store zeigte der Viewer nach dem zweiten Rechnen den alten
    ''' Stand - ein Fehler, der aussaehe wie "der Solver rechnet nicht".</summary>
    <TestMethod>
    Public Sub AntwortVerbietetZwischenspeichern()
        Dim a As New ViewerAuslieferung()
        a.Setze("<html></html>")

        Dim kopf = a.Antwort(a.SeitenUrl).Kopfzeilen

        StringAssert.Contains(kopf, "no-store")
        StringAssert.Contains(kopf, "nosniff")
    End Sub

    <TestMethod>
    Public Sub NeuerInhaltErsetztDenAlten()
        Dim a As New ViewerAuslieferung()
        a.Setze("<html>alt</html>")
        a.Setze("<html>neu</html>")

        Assert.AreEqual("<html>neu</html>", Encoding.UTF8.GetString(a.Antwort(a.SeitenUrl).Inhalt))

        a.Setze(Nothing)
        Assert.AreEqual(0, a.SeitenGroesse, "Zuruecksetzen auf Nothing muss die Seite leeren")
    End Sub

    ''' <summary>Die synthetische Herkunft darf nie oeffentlich aufloesbar
    ''' sein. `.local` ist per RFC 6762 dafuer reserviert.</summary>
    <TestMethod>
    Public Sub HerkunftIstNichtOeffentlichAufloesbar()
        StringAssert.EndsWith(ViewerAuslieferung.Ursprung, ".local")
        StringAssert.StartsWith(ViewerAuslieferung.Ursprung, "https://")
    End Sub

End Class
