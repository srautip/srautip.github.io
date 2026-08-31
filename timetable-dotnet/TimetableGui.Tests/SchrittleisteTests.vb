' Die Startseite als Schrittleiste (gui-ui-konzept.md 8) - Stufe G6.
'
' Die Skizze im Konzept ist woertlich gemeint: "[3] Rechnen ▶ zuletzt
' 22.08. 14:32 - 10 Loesungen". Eine Leiste, die nur Ueberschriften
' zeigt, waere ein Inhaltsverzeichnis - sie soll sagen, WO MAN STEHT.
' Geprueft wird deshalb der Zustand jedes Schritts und die Substanz
' seiner Zeile, nicht dass fuenf Zeilen da sind.
Imports System.Text.Json.Nodes
Imports System.Windows
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt
Imports TimetableWorkflow
Imports TimetableYaml

<TestClass>
Public Class SchrittleisteTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 30, 14, 32, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttsl-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(_ordner)
    End Sub

    <TestCleanup>
    Public Sub Abraeumen()
        If IO.Directory.Exists(_ordner) Then IO.Directory.Delete(_ordner, recursive:=True)
    End Sub

    Private Function Leer() As HauptViewModel
        Return New HauptViewModel(New TestDialoge(), Function() Jetzt)
    End Function

    ''' <summary>Ein offenes Projekt mit tragfaehigen Stammdaten - ueber
    ''' den echten Weg, weil der Setzer von Projekt privat ist.</summary>
    Private Function MitSchule() As HauptViewModel
        Dim entwurf As New ProjektEntwurf With {
            .Bestand = Scaffold.Baue("BW", "Grundschule", 1, 4, 1, "Minischule"),
            .Pfad = IO.Path.Combine(_ordner, "p.splanx"), .Passwort = "geheim12"}
        Dim d As New TestDialoge With {.AssistentEntwurf = entwurf}
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Neu()
        Return m
    End Function

    Private Shared Function Schritt(m As HauptViewModel, nummer As Integer) As HauptViewModel.Startschritt
        Return m.Schritte().Single(Function(s) s.Nummer = nummer)
    End Function

    ' ===============================================================
    ' Aufbau
    ' ===============================================================

    ''' <summary>Fuenf Schritte, in der Reihenfolge des Konzepts. Das
    ''' ANLEGEN eines Projekts ist keiner davon - "neue Projekte starten
    ''' bei [1] mit dem Assistenten-Ergebnis".</summary>
    <TestMethod>
    Public Sub DieLeisteHatDieFuenfSchritteDesKonzepts()
        Dim s = Leer().Schritte()
        CollectionAssert.AreEqual(
            New List(Of String) From {"Stammdaten", "Regeln", "Rechnen", "Entscheiden", "Freigabe & Export"},
            s.Select(Function(x) x.Titel).ToList())
        CollectionAssert.AreEqual(New List(Of Integer) From {1, 2, 3, 4, 5},
                                  s.Select(Function(x) x.Nummer).ToList())
    End Sub

    ''' <summary>"Jeder Schritt ist klickbar (fuehrt in den Bereich)".
    ''' Ein Schritt ohne Ziel waere eine Zeile, die nur so aussieht, als
    ''' koennte man ihr folgen.</summary>
    <TestMethod>
    Public Sub JederSchrittFuehrtInEinenBereich()
        Dim s = Leer().Schritte()
        Assert.AreEqual(Bereich.Stammdaten, s(0).Ziel)
        Assert.AreEqual(Bereich.Regeln, s(1).Ziel)
        Assert.AreEqual(Bereich.Laeufe, s(2).Ziel)
        Assert.AreEqual(Bereich.Laeufe, s(4).Ziel)
    End Sub

    <TestMethod>
    Public Sub OhneProjektIstAllesOffenUndSagtWarum()
        Dim s = Leer().Schritte()
        Assert.IsTrue(s.All(Function(x) x.Stand = SchrittStand.Offen))
        Assert.IsTrue(s.All(Function(x) x.Text.Contains("Projekt")),
                      "jeder Schritt soll sagen, was ihm fehlt")
    End Sub

    ' ===============================================================
    ' [1] Stammdaten
    ' ===============================================================

    <TestMethod>
    Public Sub StammdatenNennenZahlenUndDasPruefergebnis()
        Dim m = MitSchule()
        Dim s = Schritt(m, 1)

        Assert.AreEqual(SchrittStand.Erledigt, s.Stand)
        StringAssert.Contains(s.Text, "Klassen")
        StringAssert.Contains(s.Text, "gruen")
    End Sub

    ''' <summary>Bei Befunden nennt die Zeile den ERSTEN im Klartext -
    ''' eine blosse Zahl zwaenge zum Weiterklicken, um ueberhaupt zu
    ''' erfahren, worum es geht.</summary>
    <TestMethod>
    Public Sub UnvollstaendigeStammdatenWerdenZurWarnungMitBefund()
        Dim m = MitSchule()
        m.Projekt.Bestand.FachLehrerZuordnungen.Clear()

        Dim s = Schritt(m, 1)
        Assert.AreEqual(SchrittStand.Warnung, s.Stand)
        StringAssert.Contains(s.Text, "offen:")
    End Sub

    ' ===============================================================
    ' [2] Regeln
    ' ===============================================================

    ''' <summary>Regeln sind OPTIONAL - viele Schulen kommen ohne aus.
    ''' "Keine Handregel" ist deshalb Bereit und nicht Warnung: eine
    ''' Warnung, die den Normalfall trifft, erzieht dazu, Warnungen zu
    ''' uebersehen.</summary>
    <TestMethod>
    Public Sub KeineRegelnIstKeineWarnung()
        Dim s = Schritt(MitSchule(), 2)
        Assert.AreEqual(SchrittStand.Bereit, s.Stand)
        StringAssert.Contains(s.Text, "Keine Handregeln")
    End Sub

    <TestMethod>
    Public Sub VorhandeneRegelnGeltenAlsErledigt()
        Dim m = MitSchule()
        m.Projekt.Constraints.Add(JsonNode.Parse(
            "{""type"":""forbidden_slot"",""scope"":""class"",""entity"":""1a"",""day"":""Mo"",""period"":1}").AsObject())

        Dim s = Schritt(m, 2)
        Assert.AreEqual(SchrittStand.Erledigt, s.Stand)
        StringAssert.Contains(s.Text, "1 Handregel")
    End Sub

    ''' <summary>Eine Regel, die auf einen Slot ausserhalb des Rasters
    ''' zeigt, wirkt nie - und faellt sonst niemandem auf.</summary>
    <TestMethod>
    Public Sub EineRegelAusserhalbDesRastersWirdZumHinweis()
        Dim m = MitSchule()
        m.Projekt.Constraints.Add(JsonNode.Parse(
            "{""type"":""forbidden_slot"",""scope"":""class"",""entity"":""1a"",""day"":""Mo"",""period"":99}").AsObject())

        Dim s = Schritt(m, 2)
        Assert.AreEqual(SchrittStand.Warnung, s.Stand)
        StringAssert.Contains(s.Text, "Hinweis")
    End Sub

    ' ===============================================================
    ' [3] Rechnen
    ' ===============================================================

    <TestMethod>
    Public Sub OhneLaufNenntRechnenDieTastenkuerzel()
        Dim s = Schritt(MitSchule(), 3)
        StringAssert.Contains(s.Text, "F5")
        StringAssert.Contains(s.Text, "F6")
    End Sub

    ''' <summary>Die Skizze aus Konzept 8: "zuletzt 22.08. 14:32". Der
    ''' Stand wird aus den STAENDEN gelesen, nicht aus dem
    ''' Auslieferungs-Slot - ein Lauf bleibt ein Lauf, auch wenn gerade
    ''' ein anderes Dashboard angezeigt wird.</summary>
    <TestMethod>
    Public Async Function NachEinemLaufNenntRechnenZeitpunktUndErgebnis() As Task
        Dim m = MitSchule()
        m.Projekt.Config = New RunConfig With {
            .LehrereinsatzTimeLimitS = 30.0, .SolveTimeLimitS = 30.0,
            .MaxSolutions = 1, .NumWorkers = 1, .Seed = 42}

        Await m.StundenplanRechnenAsync()

        Dim s = Schritt(m, 3)
        Assert.AreEqual(SchrittStand.Erledigt, s.Stand)
        StringAssert.Contains(s.Text, "30.08. 14:32")
        StringAssert.Contains(s.Text, "Loesung")

        ' Und der Wechsel auf ein leeres Dashboard aendert daran nichts.
        m.Bereich = Bereich.Klassenbildung
        Assert.AreEqual(SchrittStand.Erledigt, Schritt(m, 3).Stand,
                        "der Lauf haengt nicht daran, was gerade angezeigt wird")
    End Function

    ' ===============================================================
    ' [4] Entscheiden und [5] Freigabe
    ' ===============================================================

    Private Function MitStand() As HauptViewModel
        Dim m = MitSchule()
        m.Projekt.StandHinzufuegen(New ProjektStand With {
            .Id = "s1", .Label = "Stundenplan, 1 Loesung(en)", .Erstellt = Jetzt,
            .Stundenplan = JsonNode.Parse("{""solutions"": [{""muss_violation_count"": 0}]}").AsObject()})
        Return m
    End Function

    <TestMethod>
    Public Sub EntscheidenIstOffenOhneLaufUndBereitDanach()
        Assert.AreEqual(SchrittStand.Offen, Schritt(MitSchule(), 4).Stand)

        Dim m = MitStand()
        Assert.AreEqual(SchrittStand.Bereit, Schritt(m, 4).Stand)
        StringAssert.Contains(Schritt(m, 4).Text, "Arbeitsstand")
    End Sub

    ''' <summary>Entschieden ist, wenn eine Loesung als Arbeitsstand
    ''' markiert wurde (Konzept 5).</summary>
    <TestMethod>
    Public Sub EinArbeitsstandMachtEntscheidenErledigt()
        Dim m = MitStand()
        m.VerarbeiteBrueckenNachricht(
            "{""v"":1,""typ"":""plan-uebernehmen"",""nutzlast"":{""zuteilung"":1,""loesung"":1}}")

        Dim s = Schritt(m, 4)
        Assert.AreEqual(SchrittStand.Erledigt, s.Stand)
        StringAssert.Contains(s.Text, "Stundenplan, 1 Loesung")
    End Sub

    <TestMethod>
    Public Sub FreigabeIstErstNachDerMarkierungBereit()
        Assert.AreEqual(SchrittStand.Offen, Schritt(MitStand(), 5).Stand)

        Dim m = MitStand()
        m.VerarbeiteBrueckenNachricht(
            "{""v"":1,""typ"":""plan-uebernehmen"",""nutzlast"":{""zuteilung"":1,""loesung"":1}}")
        Assert.AreEqual(SchrittStand.Bereit, Schritt(m, 5).Stand)
        StringAssert.Contains(Schritt(m, 5).Text, "Noch nicht freigegeben")
    End Sub

    ''' <summary>Klassenbildung und Stundenplan sind zwei Entscheidungen
    ''' mit je eigenem Nachweis - die Zeile nennt beide, nicht nur die
    ''' letzte.</summary>
    <TestMethod>
    Public Sub DieFreigabezeileNenntJedeFreigegebeneArt()
        Dim m = MitStand()
        m.Projekt.StandHinzufuegen(New ProjektStand With {
            .Id = "kb", .Label = "Klassenbildung", .Erstellt = Jetzt,
            .Klassenbildung = JsonNode.Parse("{""varianten"": [{""verletzungen"": []}]}").AsObject()})

        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {.Person = "Frau Meier", .Bestaetigt = True}}
        Dim historie As New LaeufeViewModel(m.Projekt, d, Function() Jetzt)
        Assert.IsTrue(historie.Freigeben("s1"))
        Assert.IsTrue(historie.Freigeben("kb"))

        Dim s = Schritt(m, 5)
        Assert.AreEqual(SchrittStand.Erledigt, s.Stand)
        StringAssert.Contains(s.Text, "Stundenplan freigegeben")
        StringAssert.Contains(s.Text, "Klassenbildung freigegeben")
    End Sub

    ''' <summary>Der Klick auf "Entscheiden" fuehrt in das Dashboard, das
    ''' tatsaechlich etwas zeigt. "Immer Stundenplan" waere bei einer
    ''' Schule, die nur die Klassenbildung nutzt, ein Klick ins
    ''' Leere.</summary>
    <TestMethod>
    Public Sub EntscheidenFuehrtInDasDashboardMitInhalt()
        Assert.AreEqual(Bereich.Stundenplan, Schritt(MitStand(), 4).Ziel)

        Dim m = MitSchule()
        m.Projekt.StandHinzufuegen(New ProjektStand With {
            .Id = "kb", .Label = "Klassenbildung", .Erstellt = Jetzt,
            .Klassenbildung = JsonNode.Parse("{""varianten"": []}").AsObject()})
        Assert.AreEqual(Bereich.Klassenbildung, Schritt(m, 4).Ziel)
    End Sub


    ' ===============================================================
    ' Die Leiste im Fenster
    ' ===============================================================

    ''' <summary>Die Schrittleiste entsteht zur Laufzeit aus einer
    ''' Vorlage - StaticResource-Schluessel darin sieht kein Compiler.
    ''' Dieser Test baut das Hauptfenster wirklich auf; er ist zugleich
    ''' die einzige Aufbaupruefung, die MainWindow ueberhaupt hat.</summary>
    <TestMethod>
    Public Sub DasHauptfensterBautDieSchrittleisteAuf()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim f As New MainWindow()

                   Dim zeilen = CType(f.Schrittleiste.ItemsSource, IEnumerable(Of HauptViewModel.Startschritt)).ToList()
                   Assert.AreEqual(5, zeilen.Count)
                   Assert.AreEqual("Stammdaten", zeilen(0).Titel)

                   ' Die Vorlage muss sich auch wirklich erzeugen lassen -
                   ' ein Fehler darin faellt sonst erst beim Anzeigen auf.
                   f.Schrittleiste.Measure(New Size(800, 600))
                   f.Schrittleiste.UpdateLayout()
                   Assert.AreEqual(5, f.Schrittleiste.Items.Count)
               End Sub)
    End Sub

End Class

