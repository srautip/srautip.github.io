' Undo/Redo des Boards im GUI-Betrieb (U6, Infrastruktur/Verlauf.vb).
'
' Der Verlauf lebt im Host, weil "Neu rechnen" die Seite neu laedt.
' Geprueft wird ohne Fenster: Schritte entstehen aus den
' Bruecken-Nachrichten und aus gezeigten Staenden, ein Sprung stellt
' Zustand, Eingaben und Stand wieder her.
Imports System.Text.Json.Nodes
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt
Imports TimetableYaml

<TestClass>
Public Class VerlaufTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 9, 5, 23, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttvl-" & Guid.NewGuid().ToString("N"))
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

    Private Shared Function Zustand(pinsJson As String) As String
        Return $"{{""v"": 1, ""typ"": ""zustand"", ""nutzlast"": {{""pins"": {pinsJson}, ""notPins"": {{}}, ""herkunft"": {{}}, ""haertungen"": {{""gruppen"": {{}}, ""wuensche"": {{}}}}, ""basis"": 0}}}}"
    End Function

    Private Shared Function Schritt(stand As String, zustand As String) As VerlaufSchritt
        Return New VerlaufSchritt With {.StandId = stand, .Zustand = JsonNode.Parse(zustand).AsObject()}
    End Function

    ' ===============================================================
    ' Der Verlauf selbst
    ' ===============================================================

    <TestMethod>
    Public Sub ZurueckUndVorWieInJedemEditor()
        Dim v As New Verlauf()
        Assert.IsNull(v.Zurueck())
        v.Merke(Schritt("s1", "{""a"":1}"))
        v.Merke(Schritt("s1", "{""a"":2}"))
        v.Merke(Schritt("s2", "{""a"":2}"))
        Assert.AreEqual(2, v.Zurueckzaehler)
        Assert.AreEqual(0, v.Vorzaehler)

        Assert.AreEqual("s1", v.Zurueck().StandId, "ein Schritt zurueck: derselbe Stand, der zweite Zustand")
        Assert.AreEqual(1, v.Zurueck().Zustand("a").GetValue(Of Integer)(), "zwei zurueck: der erste Zustand")
        Assert.IsNull(v.Zurueck(), "am Anfang gibt es nichts mehr")
        Assert.AreEqual(2, v.Vorzaehler)

        Assert.AreEqual(2, v.Vor().Zustand("a").GetValue(Of Integer)())
        ' Ein neuer Schritt mitten im Verlauf kappt die Redo-Kette.
        v.Merke(Schritt("s3", "{}"))
        Assert.AreEqual(0, v.Vorzaehler)
        Assert.IsNull(v.Vor())
        Assert.AreEqual(3, v.Anzahl)
    End Sub

    ''' <summary>Dasselbe noch einmal ist kein Schritt - das erneute Laden
    ''' einer Seite darf den Verlauf nicht verlaengern.</summary>
    <TestMethod>
    Public Sub GleicheSchritteWerdenNichtDoppeltAbgelegt()
        Dim v As New Verlauf()
        v.Merke(Schritt("s1", "{""a"":1}"))
        v.Merke(Schritt("s1", "{""a"":1}"))
        Assert.AreEqual(1, v.Anzahl)
    End Sub

    ''' <summary>Der Schnappschuss der Eingaben deckt genau das ab, was
    ''' "Neu rechnen" veraendert: Fixierungen und den Modus der Regeln.</summary>
    <TestMethod>
    Public Sub EingabeschnappschussStelltFixierungenUndModusWiederHer()
        Dim kb As New KlassenbildungInput()
        kb.Gruppen.Add(New KlassenbildungGruppe With {.Id = "G_a", .Modus = "soft"})
        kb.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "zusammen", .Modus = "soft"})
        kb.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S001", .Klasse = 1})
        Dim bild = Eingabeschnappschuss.Aufnehmen(kb)

        kb.Gruppen(0).Modus = "hard"
        kb.Wuensche(0).Modus = "hard"
        kb.Fixierungen.Clear()
        kb.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S002", .NichtKlasse = 2})

        Eingabeschnappschuss.Anwenden(kb, bild)
        Assert.AreEqual("soft", kb.Gruppen(0).Modus)
        Assert.AreEqual("soft", kb.Wuensche(0).Modus)
        Assert.AreEqual(1, kb.Fixierungen.Count)
        Assert.AreEqual("S001", kb.Fixierungen(0).Kind)
        Assert.AreEqual(1, kb.Fixierungen(0).Klasse)
        Assert.IsFalse(kb.Fixierungen(0).NichtKlasse.HasValue)
    End Sub

    ' ===============================================================
    ' Im HauptViewModel
    ' ===============================================================

    ''' <summary>Jede zustand-Nachricht ist ein Schritt; undo/redo stellen
    ''' den Board-Zustand wieder her und das Startskript traegt die
    ''' Tiefe fuer die Knoepfe.</summary>
    <TestMethod>
    Public Sub BedienschritteLassenSichZurueckUndVorNehmen()
        Dim m = ModellMitProjekt()
        m.VerarbeiteBrueckenNachricht(Zustand("{""S001"": 1}"))
        m.VerarbeiteBrueckenNachricht(Zustand("{""S001"": 1, ""S002"": 2}"))
        Assert.AreEqual(2, m.Verlauf.Anzahl)
        StringAssert.Contains(m.BrueckenStartSkript(), "window.__verlauf = {""zurueck"":1,""vor"":0}")

        m.VerarbeiteBrueckenNachricht("{""v"": 1, ""typ"": ""undo"", ""nutzlast"": {}}")
        Assert.IsFalse(m.Projekt.GuiState("pins").AsObject().ContainsKey("S002"), "undo hat den zweiten Pin nicht zurueckgenommen")
        StringAssert.Contains(m.BrueckenStartSkript(), "window.__verlauf = {""zurueck"":0,""vor"":1}")

        m.VerarbeiteBrueckenNachricht("{""v"": 1, ""typ"": ""redo"", ""nutzlast"": {}}")
        Assert.IsTrue(m.Projekt.GuiState("pins").AsObject().ContainsKey("S002"), "redo hat den Pin nicht wiederhergestellt")

        ' Ein neuer Schritt nach dem Undo kappt die Redo-Kette.
        m.VerarbeiteBrueckenNachricht("{""v"": 1, ""typ"": ""undo"", ""nutzlast"": {}}")
        m.VerarbeiteBrueckenNachricht(Zustand("{""S003"": 3}"))
        Assert.AreEqual(0, m.Verlauf.Vorzaehler)
        Assert.AreEqual(2, m.Verlauf.Anzahl)
    End Sub

    ''' <summary>Das Wesentliche: "Neu rechnen" ist ein Schritt, und
    ''' zurueck zeigt den VORIGEN Stand mit dem Zustand von damals -
    ''' nicht nur die Pins.</summary>
    <TestMethod>
    Public Async Function ZurueckZeigtDenVorigenStandMitSeinemZustand() As Task
        Dim m = ModellMitProjekt()
        m.Projekt.Config.Klassenbildung = New KlassenbildungConfig With {.ZeitlimitS = 10.0, .NVarianten = 1, .MinDistanz = 4}
        m.Projekt.Config.NumWorkers = 1

        Await m.KlassenbildungRechnenAsync()
        Dim erster = m.AngezeigterStand().Id
        m.VerarbeiteBrueckenNachricht(Zustand("{""S001"": 1}"))
        Dim fixVorher = m.Projekt.Klassenbildung.Fixierungen.Count

        ' Neu rechnen mit einem Pin: veraendert die Fixierungen der Eingabe
        ' und erzeugt einen zweiten Stand.
        Dim nutzlast = JsonNode.Parse("{""fixierungen"": [{""kind"": ""S001"", ""klasse"": 1, ""herkunft"": ""manuell""}], ""haertungen"": {""gruppen"": {""G_nordstadt"": ""hard""}, ""wuensche"": {}}}").AsObject()
        Await m.NeuRechnenAsync(nutzlast)
        Dim zweiter = m.AngezeigterStand().Id
        Assert.AreNotEqual(erster, zweiter, "Testgrundlage: ein zweiter Stand")
        Assert.AreEqual("hard", m.Projekt.Klassenbildung.Gruppen.First(Function(g) g.Id = "G_nordstadt").Modus)

        m.VerlaufSpringen(-1)
        Assert.AreEqual(erster, m.AngezeigterStand().Id, "zurueck zeigt nicht den vorigen Stand")
        Assert.AreEqual(fixVorher, m.Projekt.Klassenbildung.Fixierungen.Count, "die Eingabe-Fixierungen von damals fehlen")
        Assert.AreEqual("soft", m.Projekt.Klassenbildung.Gruppen.First(Function(g) g.Id = "G_nordstadt").Modus, "die Haertung wurde nicht zurueckgenommen")
        Assert.IsTrue(m.Projekt.GuiState("pins").AsObject().ContainsKey("S001"), "der Pin von damals fehlt")

        m.VerlaufSpringen(1)
        Assert.AreEqual(zweiter, m.AngezeigterStand().Id, "vor zeigt nicht den neuen Stand wieder")
        Assert.AreEqual("hard", m.Projekt.Klassenbildung.Gruppen.First(Function(g) g.Id = "G_nordstadt").Modus)
    End Function

End Class
