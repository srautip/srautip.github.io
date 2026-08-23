' Die Host-Seite der Bruecke (U5, Stufe E).
'
' Gegenstueck zu TimetableViewer.Tests/BridgeTests.vb: dort wird geprueft,
' was die SEITE sendet, hier was der HOST damit macht. Beides ohne
' WebView2 - das Protokoll ist bewusst so geschnitten, dass es an einer
' Zeichenkette haengt und nicht am Browser.
Imports System.Text.Json.Nodes
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt
Imports TimetableYaml

<TestClass>
Public Class BrueckeTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 23, 14, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttbr-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(_ordner)
    End Sub

    <TestCleanup>
    Public Sub Abraeumen()
        If IO.Directory.Exists(_ordner) Then IO.Directory.Delete(_ordner, recursive:=True)
    End Sub

    Private Shared ReadOnly Property TestsRoot As String
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

    Private Function ModellMitProjekt() As HauptViewModel
        Dim d As New TestDialoge With {
            .OrdnerPfad = IO.Path.Combine(TestsRoot, "bw-grundschule-beispiel"),
            .SpeichernPfad = IO.Path.Combine(_ordner, "gs.splanx")}
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Importieren()
        Return m
    End Function

    Private Shared Function Umschlag(typ As String, nutzlast As String, Optional version As Integer = 1) As String
        Return $"{{""v"": {version}, ""typ"": ""{typ}"", ""nutzlast"": {nutzlast}}}"
    End Function

    ' ---------------------------------------------------------------
    ' Umschlag lesen
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub GueltigerUmschlagWirdGelesen()
        Dim n = BrueckenNachricht.Lesen(Umschlag("zustand", "{""pins"": {""S001"": 2}}"))

        Assert.IsNotNull(n)
        Assert.AreEqual(1, n.Version)
        Assert.AreEqual("zustand", n.Typ)
        Assert.AreEqual(2, n.Nutzlast("pins")("S001").GetValue(Of Integer)())
    End Sub

    ''' <summary>Eine eingebettete Seite ist zwar vertrauenswuerdig, aber
    ''' ein Host, der auf beliebige Zeichenketten mit einer Ausnahme
    ''' reagiert, ist trotzdem falsch gebaut.</summary>
    <DataTestMethod>
    <DataRow("")>
    <DataRow("kein json")>
    <DataRow("[1,2,3]")>
    <DataRow("{""typ"": ""zustand""}")>
    <DataRow("{""v"": 1}")>
    Public Sub UnsinnLiefertNothingStattAusnahme(json As String)
        Assert.IsNull(BrueckenNachricht.Lesen(json))
    End Sub

    ''' <summary>Eine neuere Version wird still ignoriert - eine als
    ''' Artifact veroeffentlichte Seite (CLAUDE.md) kann aelter oder
    ''' neuer sein als der Host, und keiner von beiden darf daran
    ''' sterben.</summary>
    <TestMethod>
    Public Sub NeuereVersionWirdStillIgnoriert()
        Dim m = ModellMitProjekt()
        Dim vorher = m.Projekt.GuiState

        m.VerarbeiteBrueckenNachricht(Umschlag("zustand", "{""pins"": {""S001"": 2}}", version:=99))

        Assert.AreSame(vorher, m.Projekt.GuiState, "eine Nachricht aus der Zukunft wurde verarbeitet")
    End Sub

    <TestMethod>
    Public Sub UnbekannterTypWirdStillIgnoriert()
        Dim m = ModellMitProjekt()
        m.VerarbeiteBrueckenNachricht(Umschlag("gibt-es-nicht", "{}"))
        Assert.IsNull(m.Projekt.GuiState)
    End Sub

    ' ---------------------------------------------------------------
    ' Zustand
    ' ---------------------------------------------------------------

    ''' <summary>Der Board-Zustand landet in gui-state.json der
    ''' Projektdatei und ersetzt damit die localStorage-Rolle der Vorlage
    ''' (Datenhaltung 7.6).</summary>
    <TestMethod>
    Public Sub ZustandLandetInDerProjektdatei()
        Dim m = ModellMitProjekt()

        m.VerarbeiteBrueckenNachricht(Umschlag("zustand", "{""pins"": {""S001"": 2}, ""haertungen"": {""gruppen"": {}, ""wuensche"": {}}}"))

        Assert.IsNotNull(m.Projekt.GuiState)
        Assert.AreEqual(2, m.Projekt.GuiState("pins")("S001").GetValue(Of Integer)())
        Assert.IsTrue(m.Geaendert, "der geaenderte Zustand wurde nicht als Aenderung markiert")

        ' Und er ueberlebt einen Speicher-/Ladezyklus.
        m.Speichern()
        Dim erneut = ProjektDatei.Laden(IO.Path.Combine(_ordner, "gs.splanx"), "geheim")
        Assert.AreEqual(2, erneut.GuiState("pins")("S001").GetValue(Of Integer)())
    End Sub

    ' ---------------------------------------------------------------
    ' Fixierungen und Haertungen
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub FixierungenWerdenInKernObjekteUebersetzt()
        Dim nutzlast = JsonNode.Parse("{""fixierungen"": [
            {""kind"": ""S001"", ""klasse"": 2, ""herkunft"": ""verschoben""},
            {""kind"": ""S002"", ""nicht_klasse"": 3, ""herkunft"": ""manuell""},
            {""kind"": ""S003""}
        ]}").AsObject()

        Dim liste = Bruecke.LiesFixierungen(nutzlast)

        Assert.AreEqual(2, liste.Count, "der Eintrag ohne klasse/nicht_klasse haette verworfen werden muessen")
        Assert.AreEqual("S001", liste(0).Kind)
        Assert.AreEqual(2, liste(0).Klasse)
        Assert.IsFalse(liste(0).NichtKlasse.HasValue)
        Assert.AreEqual("S002", liste(1).Kind)
        Assert.AreEqual(3, liste(1).NichtKlasse)
        Assert.IsFalse(liste(1).Klasse.HasValue)
    End Sub

    <TestMethod>
    Public Sub HaertungenSetzenModusAufHard()
        Dim eingabe As New KlassenbildungInput()
        eingabe.Gruppen.Add(New KlassenbildungGruppe With {.Id = "G_soz", .Typ = "verteilung", .Modus = "soft"})
        eingabe.Gruppen.Add(New KlassenbildungGruppe With {.Id = "G_kita", .Typ = "buendelung", .Modus = "soft"})
        eingabe.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "zusammen", .Modus = "soft"})
        eingabe.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "getrennt", .Modus = "soft"})

        Dim nutzlast = JsonNode.Parse("{""haertungen"": {""gruppen"": {""G_soz"": true}, ""wuensche"": {""1"": true}}}").AsObject()
        Dim geaendert = Bruecke.WendeHaertungenAn(nutzlast, eingabe)

        Assert.AreEqual(2, geaendert)
        Assert.AreEqual("hard", eingabe.Gruppen(0).Modus)
        Assert.AreEqual("soft", eingabe.Gruppen(1).Modus, "eine nicht gehaertete Gruppe wurde mitgeaendert")
        Assert.AreEqual("soft", eingabe.Wuensche(0).Modus)
        Assert.AreEqual("hard", eingabe.Wuensche(1).Modus)
    End Sub

    ''' <summary>Ein Index ausserhalb der Wunschliste oder eine unbekannte
    ''' Gruppen-Id darf nicht werfen - die Seite koennte aus einem
    ''' aelteren Stand stammen.</summary>
    <TestMethod>
    Public Sub UnbekannteHaertungenWerdenUebergangen()
        Dim eingabe As New KlassenbildungInput()
        eingabe.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "zusammen", .Modus = "soft"})

        Dim nutzlast = JsonNode.Parse("{""haertungen"": {""gruppen"": {""gibt-es-nicht"": true}, ""wuensche"": {""99"": true, ""abc"": true}}}").AsObject()

        Assert.AreEqual(0, Bruecke.WendeHaertungenAn(nutzlast, eingabe))
        Assert.AreEqual("soft", eingabe.Wuensche(0).Modus)
    End Sub

    ' ---------------------------------------------------------------
    ' Startskript (Host -> Seite)
    ' ---------------------------------------------------------------

    ''' <summary>Die Anzeige-Map ist der einzige Weg, auf dem Klarnamen in
    ''' die Seite gelangen (Datenhaltung 6.1).</summary>
    <TestMethod>
    Public Sub StartSkriptTraegtZustandUndAnzeigeNamen()
        Dim m = ModellMitProjekt()
        m.Projekt.Mapping.Add(New MappingEintrag With {.Id = "S001", .Vorname = "Mia", .Nachname = "Muster"})
        m.VerarbeiteBrueckenNachricht(Umschlag("zustand", "{""pins"": {""S001"": 2}}"))

        Dim skript = m.BrueckenStartSkript()

        StringAssert.Contains(skript, "window.__gastZustand")
        StringAssert.Contains(skript, "window.__anzeigeNamen")
        StringAssert.Contains(skript, "Mia Muster")
        StringAssert.Contains(skript, """S001"":2")
    End Sub

    ''' <summary>Platzhalter-Schueler haben bewusst keinen
    ''' Mapping-Eintrag - dann darf auch kein leerer Name in die Map.</summary>
    <TestMethod>
    Public Sub OhneMappingBleibtDieAnzeigeMapLeer()
        Dim m = ModellMitProjekt()

        Dim skript = m.BrueckenStartSkript()

        StringAssert.Contains(skript, "window.__anzeigeNamen = {}")
    End Sub

    ' ---------------------------------------------------------------
    ' U5: der geschlossene Loop
    ' ---------------------------------------------------------------

    ''' <summary>DER Kern von Stufe E: Pins aus dem Board landen im
    ''' Projektbestand, werden protokolliert, und der Lauf startet neu -
    ''' ohne YAML-Kopieren, ohne CLI.</summary>
    <TestMethod>
    Public Async Function NeuRechnenUebernimmtFixierungenUndRechnet() As Task
        Dim m = ModellMitProjekt()
        m.Projekt.Config.Klassenbildung = New KlassenbildungConfig With {.ZeitlimitS = 10.0, .NVarianten = 1, .MinDistanz = 4}
        m.Projekt.Config.NumWorkers = 1
        Dim erstesKind = m.Projekt.Klassenbildung.Schueler(0).Id
        Dim logVorher = m.Projekt.AuditLog.Count

        Dim nutzlast = JsonNode.Parse($"{{""fixierungen"": [{{""kind"": ""{erstesKind}"", ""klasse"": 1, ""herkunft"": ""verschoben""}}], ""haertungen"": {{""gruppen"": {{}}, ""wuensche"": {{}}}}}}").AsObject()

        Await m.NeuRechnenAsync(nutzlast)

        Assert.AreEqual(1, m.Projekt.Klassenbildung.Fixierungen.Count,
                        "die Fixierungsliste wurde nicht ersetzt")
        Assert.AreEqual(erstesKind, m.Projekt.Klassenbildung.Fixierungen(0).Kind)
        Assert.AreEqual(1, m.Projekt.Klassenbildung.Fixierungen(0).Klasse)

        ' Audit: eine Zeile fuer die Uebernahme, eine fuer den Lauf.
        Assert.AreEqual(logVorher + 2, m.Projekt.AuditLog.Count,
                        "Uebernahme und Lauf muessen je eine Audit-Zeile schreiben")
        Assert.AreEqual("fixierung", m.Projekt.AuditLog(logVorher).Aktion)
        Assert.AreEqual("lauf", m.Projekt.AuditLog(logVorher + 1).Aktion)

        Assert.IsTrue(m.Auslieferung.SeitenGroesse > 0, "das Board hat keine neue Seite bekommen")
        Assert.IsFalse(m.Monitor.Laeuft)
    End Function

    ''' <summary>Die Bestandsfixierungen sind in der Liste des Boards
    ''' bereits enthalten (`herkunft: bestehend`) - Anhaengen statt
    ''' Ersetzen wuerde sie verdoppeln, und doppelte Fixierungen desselben
    ''' Kindes sind fuer die Validierung ein Widerspruch.</summary>
    <TestMethod>
    Public Sub FixierungenWerdenErsetztNichtAngehaengt()
        Dim m = ModellMitProjekt()
        Dim vorher = m.Projekt.Klassenbildung.Fixierungen.Count
        Assert.IsTrue(vorher > 0, "die Fixture hat keine YAML-Fixierungen - Testgrundlage fehlt")

        Dim kind = m.Projekt.Klassenbildung.Schueler(0).Id
        Dim nutzlast = JsonNode.Parse($"{{""fixierungen"": [{{""kind"": ""{kind}"", ""klasse"": 1}}]}}").AsObject()
        Dim liste = Bruecke.LiesFixierungen(nutzlast)
        m.Projekt.Klassenbildung.Fixierungen.Clear()
        m.Projekt.Klassenbildung.Fixierungen.AddRange(liste)

        Assert.AreEqual(1, m.Projekt.Klassenbildung.Fixierungen.Count)
    End Sub

End Class
