' Die Startseite als zwei Karten (gui-ui-konzept.md 8) - Stufe H2.
'
' Je Rechnung eine Karte mit ihrem Ablauf. Jede Zeile soll sagen, WO MAN
' STEHT - "zuletzt 30.08. 14:32, 3 Loesungen" -, nicht nur, dass es sie
' gibt. Geprueft wird deshalb der Zustand jeder Zeile und die Substanz
' ihres Textes, und dass die zwei Rechnungen einander NICHT beeinflussen:
' vorher galt "Rechnen" als erledigt, sobald irgendein Lauf da war.
Imports System.Text.Json.Nodes
Imports System.Windows
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt
Imports TimetableWorkflow
Imports TimetableYaml

<TestClass>
Public Class StartkartenTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 30, 14, 32, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttsk-" & Guid.NewGuid().ToString("N"))
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
    Private Function MitSchule(Optional dialoge As TestDialoge = Nothing) As HauptViewModel
        Dim entwurf As New ProjektEntwurf With {
            .Bestand = Scaffold.Baue("BW", "Grundschule", 1, 4, 1, "Minischule"),
            .Pfad = IO.Path.Combine(_ordner, "p.splanx"), .Passwort = "geheim12"}
        Dim d = If(dialoge, New TestDialoge())
        d.AssistentEntwurf = entwurf
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Neu()
        Return m
    End Function

    Private Shared Function Zeile(m As HauptViewModel, art As Rechnungsart, titel As String) As HauptViewModel.Startschritt
        Return m.Karte(art).Zeilen.Single(Function(z) z.Titel = titel)
    End Function

    Private Shared Function Stundenplan(m As HauptViewModel, titel As String) As HauptViewModel.Startschritt
        Return Zeile(m, Rechnungsart.Stundenplan, titel)
    End Function

    Private Shared Function Klassenbildung(m As HauptViewModel, titel As String) As HauptViewModel.Startschritt
        Return Zeile(m, Rechnungsart.Klassenbildung, titel)
    End Function

    ' ===============================================================
    ' Aufbau
    ' ===============================================================

    ''' <summary>Zwei Karten, je eine Rechnung, jede mit ihrem Ablauf. Das
    ''' ANLEGEN eines Projekts ist keine Zeile - die Karten setzen ein
    ''' offenes Projekt voraus.</summary>
    <TestMethod>
    Public Sub ZweiKartenMitJeEigenemAblauf()
        Dim k = Leer().Karten()
        CollectionAssert.AreEqual(New List(Of String) From {"Klassenbildung", "Stundenplan"},
                                  k.Select(Function(x) x.Titel).ToList())
        CollectionAssert.AreEqual(New List(Of String) From {"Kinder & Regeln", "Rechnen", "Entscheiden", "Freigabe"},
                                  k(0).Zeilen.Select(Function(z) z.Titel).ToList())
        CollectionAssert.AreEqual(New List(Of String) From {"Stammdaten", "Regeln", "Rechnen", "Entscheiden", "Freigabe"},
                                  k(1).Zeilen.Select(Function(z) z.Titel).ToList())
        Assert.AreEqual(Bereich.Klassenbildung, k(0).Bereich)
        Assert.AreEqual(Bereich.Stundenplan, k(1).Bereich)
        For Each karte In k
            CollectionAssert.AreEqual(Enumerable.Range(1, karte.Zeilen.Count).ToList(),
                                      karte.Zeilen.Select(Function(z) z.Nummer).ToList())
            Assert.IsTrue(karte.Zeilen.All(Function(z) z.Aktion IsNot Nothing), "jede Zeile tut etwas")
        Next
    End Sub

    ''' <summary>Nur die Eingabe-Zeilen stehen im Bereichskopf - Rechnen
    ''' ist dort der Knopf, Entscheiden das Dashboard selbst.</summary>
    <TestMethod>
    Public Sub DerKopfZeigtNurDieEingaben()
        Dim m = Leer()
        m.Bereich = Bereich.Stundenplan
        CollectionAssert.AreEqual(New List(Of String) From {"Stammdaten", "Regeln"},
                                  m.KopfZeilen().Select(Function(z) z.Titel).ToList())
        Assert.AreEqual("Stundenplan", m.KopfTitel)

        m.Bereich = Bereich.Klassenbildung
        CollectionAssert.AreEqual(New List(Of String) From {"Kinder & Regeln"},
                                  m.KopfZeilen().Select(Function(z) z.Titel).ToList())

        m.Bereich = Bereich.Laeufe
        Assert.AreEqual(0, m.KopfZeilen().Count)
        Assert.AreEqual("", m.KopfTitel)
    End Sub

    <TestMethod>
    Public Sub OhneProjektIstAllesOffenUndSagtWarum()
        Dim zeilen = Leer().Karten().SelectMany(Function(k) k.Zeilen).ToList()
        Assert.IsTrue(zeilen.All(Function(z) z.Stand = SchrittStand.Offen))
        Assert.IsTrue(zeilen.All(Function(z) z.Text.Contains("Projekt")),
                      "jede Zeile soll sagen, was ihr fehlt")
    End Sub

    ' ===============================================================
    ' Eingabe-Zeilen fuehren in die Masken
    ' ===============================================================

    ''' <summary>Der Klick auf eine Eingabe-Zeile oeffnet die Maske - ueber
    ''' IDialoge, damit das Modell kein Fenster kennt. Vorher fuehrte er
    ''' in einen Seitenleisten-Bereich, hinter dem nichts lag.</summary>
    <TestMethod>
    Public Sub EingabeZeilenOeffnenIhreMaske()
        Dim d As New TestDialoge()
        Dim m = MitSchule(d)

        Stundenplan(m, "Stammdaten").Aktion.Invoke()
        Stundenplan(m, "Regeln").Aktion.Invoke()
        Klassenbildung(m, "Kinder & Regeln").Aktion.Invoke()
        m.SolverEinstellungenPflegen()

        CollectionAssert.AreEqual(New List(Of String) From {"Stammdaten", "Regeln", "Klassenbildung", "Solver"}, d.Masken)
    End Sub

    ''' <summary>Ohne Projekt gibt es nichts zu pflegen - dann der Hinweis
    ''' statt eines leeren Fensters.</summary>
    <TestMethod>
    Public Sub OhneProjektOeffnetKeineMaske()
        Dim d As New TestDialoge()
        Dim m As New HauptViewModel(d, Function() Jetzt)

        m.StammdatenPflegen()

        Assert.AreEqual(0, d.Masken.Count)
        Assert.AreEqual(1, d.Hinweise.Count)
        StringAssert.Contains(d.Hinweise(0), "Projekt")
    End Sub

    ' ===============================================================
    ' Stundenplan: Stammdaten und Regeln
    ' ===============================================================

    <TestMethod>
    Public Sub StammdatenNennenZahlenUndDasPruefergebnis()
        Dim s = Stundenplan(MitSchule(), "Stammdaten")
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

        Dim s = Stundenplan(m, "Stammdaten")
        Assert.AreEqual(SchrittStand.Warnung, s.Stand)
        StringAssert.Contains(s.Text, "offen:")
    End Sub

    ''' <summary>Regeln sind OPTIONAL - viele Schulen kommen ohne aus.
    ''' "Keine Handregel" ist deshalb Bereit und nicht Warnung: eine
    ''' Warnung, die den Normalfall trifft, erzieht dazu, Warnungen zu
    ''' uebersehen.</summary>
    <TestMethod>
    Public Sub KeineRegelnIstKeineWarnung()
        Dim s = Stundenplan(MitSchule(), "Regeln")
        Assert.AreEqual(SchrittStand.Bereit, s.Stand)
        StringAssert.Contains(s.Text, "Keine Handregeln")
    End Sub

    <TestMethod>
    Public Sub VorhandeneRegelnGeltenAlsErledigt()
        Dim m = MitSchule()
        m.Projekt.Constraints.Add(JsonNode.Parse(
            "{""type"":""forbidden_slot"",""scope"":""class"",""entity"":""1a"",""day"":""Mo"",""period"":1}").AsObject())

        Dim s = Stundenplan(m, "Regeln")
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

        Dim s = Stundenplan(m, "Regeln")
        Assert.AreEqual(SchrittStand.Warnung, s.Stand)
        StringAssert.Contains(s.Text, "Hinweis")
    End Sub

    ' ===============================================================
    ' Klassenbildung: Kinder & Regeln
    ' ===============================================================

    ''' <summary>Ohne Kinder ist die Zeile Bereit, nicht Warnung: ein
    ''' leeres Projekt ist der Normalzustand vor der Eingabe.</summary>
    <TestMethod>
    Public Sub OhneKinderIstDieEingabeBereit()
        Dim s = Klassenbildung(MitSchule(), "Kinder & Regeln")
        Assert.AreEqual(SchrittStand.Bereit, s.Stand)
        StringAssert.Contains(s.Text, "Noch keine Kinder")
    End Sub

    <TestMethod>
    Public Sub KinderUndRegelnWerdenGezaehlt()
        Dim m = MitSchule()
        Dim kb = m.Projekt.Klassenbildung
        kb.Klassen.Anzahl = 2
        kb.Klassen.MinGroesse = 1
        kb.Klassen.MaxGroesse = 30
        For i = 1 To 3
            kb.Schueler.Add(New KlassenbildungSchueler With {.Id = $"S{i:000}"})
        Next
        kb.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "zusammen", .Kinder = New List(Of String) From {"S001", "S002"}})

        Dim s = Klassenbildung(m, "Kinder & Regeln")
        Assert.AreEqual(SchrittStand.Erledigt, s.Stand)
        StringAssert.Contains(s.Text, "3 Kinder")
        StringAssert.Contains(s.Text, "2 Klassen")
        StringAssert.Contains(s.Text, "1 Regel")
        StringAssert.Contains(s.Text, "gruen")
    End Sub

    <TestMethod>
    Public Sub EinBefundMachtDieEingabeZurWarnung()
        Dim m = MitSchule()
        Dim kb = m.Projekt.Klassenbildung
        kb.Klassen.Anzahl = 0
        kb.Schueler.Add(New KlassenbildungSchueler With {.Id = "S001"})

        Dim s = Klassenbildung(m, "Kinder & Regeln")
        Assert.AreEqual(SchrittStand.Warnung, s.Stand)
        StringAssert.Contains(s.Text, "offen:")
    End Sub

    ' ===============================================================
    ' Rechnen
    ' ===============================================================

    <TestMethod>
    Public Sub OhneLaufNenntRechnenSeineTaste()
        Dim m = MitSchule()
        Dim sp = Stundenplan(m, "Rechnen")
        Assert.AreEqual(SchrittStand.Bereit, sp.Stand)
        StringAssert.Contains(sp.Text, "F6")

        ' Die Klassenbildung kann ohne Kinder nicht rechnen - Offen, und
        ' die Zeile sagt, was fehlt.
        Dim kb = Klassenbildung(m, "Rechnen")
        Assert.AreEqual(SchrittStand.Offen, kb.Stand)
        StringAssert.Contains(kb.Text, "Kinder")
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

        Dim s = Stundenplan(m, "Rechnen")
        Assert.AreEqual(SchrittStand.Erledigt, s.Stand)
        StringAssert.Contains(s.Text, "30.08. 14:32")
        StringAssert.Contains(s.Text, "Loesung")

        ' Und der Wechsel auf das leere Board aendert daran nichts.
        m.Bereich = Bereich.Klassenbildung
        Assert.AreEqual(SchrittStand.Erledigt, Stundenplan(m, "Rechnen").Stand,
                        "der Lauf haengt nicht daran, was gerade angezeigt wird")
    End Function

    ''' <summary>Die Rechnungen sind getrennt: ein Stundenplan-Lauf macht
    ''' die Klassenbildung nicht "gerechnet" - genau das tat die alte
    ''' Leiste.</summary>
    <TestMethod>
    Public Sub EinStandDerEinenRechnungZaehltNichtFuerDieAndere()
        Dim m = MitStand()
        Assert.AreEqual(SchrittStand.Erledigt, Stundenplan(m, "Rechnen").Stand)
        Assert.AreEqual(SchrittStand.Offen, Klassenbildung(m, "Rechnen").Stand)
        Assert.AreEqual(SchrittStand.Offen, Klassenbildung(m, "Entscheiden").Stand)

        m.Projekt.StandHinzufuegen(New ProjektStand With {
            .Id = "kb", .Label = "Klassenbildung, 2 Variante(n)", .Erstellt = Jetzt.AddMinutes(5),
            .Klassenbildung = JsonNode.Parse("{""varianten"": []}").AsObject()})
        Assert.AreEqual(SchrittStand.Erledigt, Klassenbildung(m, "Rechnen").Stand)
        StringAssert.Contains(Klassenbildung(m, "Rechnen").Text, "2 Variante")
        StringAssert.Contains(Stundenplan(m, "Rechnen").Text, "1 Loesung",
                              "die Stundenplan-Zeile nennt IHREN letzten Stand, nicht den juengsten ueberhaupt")
    End Sub

    ''' <summary>Der Klick auf "Rechnen" rechnet. Ohne Kinder laeuft die
    ''' Klassenbildung nicht an - und der Befehl sagt es nicht zweimal.</summary>
    <TestMethod>
    Public Sub DieRechnenZeileRechnet()
        Dim m = MitSchule()
        Assert.IsFalse(m.KannKlassenbildungRechnen)
        Klassenbildung(m, "Rechnen").Aktion.Invoke()
        Assert.IsFalse(m.Monitor.Laeuft, "ohne Kinder startet nichts")
        Assert.AreEqual(0, m.Projekt.Staende.Count)
    End Sub

    ' ===============================================================
    ' Entscheiden und Freigabe
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
        Assert.AreEqual(SchrittStand.Offen, Stundenplan(MitSchule(), "Entscheiden").Stand)

        Dim m = MitStand()
        Assert.AreEqual(SchrittStand.Bereit, Stundenplan(m, "Entscheiden").Stand)
        StringAssert.Contains(Stundenplan(m, "Entscheiden").Text, "Arbeitsstand")
    End Sub

    ''' <summary>Entschieden ist, wenn eine Loesung als Arbeitsstand
    ''' markiert wurde (Konzept 5).</summary>
    <TestMethod>
    Public Sub EinArbeitsstandMachtEntscheidenErledigt()
        Dim m = MitStand()
        m.VerarbeiteBrueckenNachricht(
            "{""v"":1,""typ"":""plan-uebernehmen"",""nutzlast"":{""zuteilung"":1,""loesung"":1}}")

        Dim s = Stundenplan(m, "Entscheiden")
        Assert.AreEqual(SchrittStand.Erledigt, s.Stand)
        StringAssert.Contains(s.Text, "Stundenplan, 1 Loesung")
    End Sub

    ''' <summary>Die Entscheiden-Zeile fuehrt in das Dashboard IHRER
    ''' Rechnung - nicht in das, das zufaellig zuletzt gerechnet wurde.</summary>
    <TestMethod>
    Public Sub EntscheidenFuehrtInDasEigeneDashboard()
        Dim m = MitStand()
        Stundenplan(m, "Entscheiden").Aktion.Invoke()
        Assert.AreEqual(Bereich.Stundenplan, m.Bereich)
        Klassenbildung(m, "Entscheiden").Aktion.Invoke()
        Assert.AreEqual(Bereich.Klassenbildung, m.Bereich)
    End Sub

    <TestMethod>
    Public Sub FreigabeIstErstNachDerMarkierungBereit()
        Assert.AreEqual(SchrittStand.Offen, Stundenplan(MitStand(), "Freigabe").Stand)

        Dim m = MitStand()
        m.VerarbeiteBrueckenNachricht(
            "{""v"":1,""typ"":""plan-uebernehmen"",""nutzlast"":{""zuteilung"":1,""loesung"":1}}")
        Assert.AreEqual(SchrittStand.Bereit, Stundenplan(m, "Freigabe").Stand)
        StringAssert.Contains(Stundenplan(m, "Freigabe").Text, "Noch nicht freigegeben")
    End Sub

    ''' <summary>Klassenbildung und Stundenplan sind zwei Entscheidungen
    ''' mit je eigenem Nachweis - jede Karte nennt nur ihre eigene.</summary>
    <TestMethod>
    Public Sub JedeKarteNenntNurIhreEigeneFreigabe()
        Dim m = MitStand()
        m.Projekt.StandHinzufuegen(New ProjektStand With {
            .Id = "kb", .Label = "Klassenbildung", .Erstellt = Jetzt,
            .Klassenbildung = JsonNode.Parse("{""varianten"": [{""verletzungen"": []}]}").AsObject()})

        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {.Person = "Frau Meier", .Bestaetigt = True}}
        Dim historie As New LaeufeViewModel(m.Projekt, d, Function() Jetzt)
        Assert.IsTrue(historie.Freigeben("s1"))

        Assert.AreEqual(SchrittStand.Erledigt, Stundenplan(m, "Freigabe").Stand)
        StringAssert.Contains(Stundenplan(m, "Freigabe").Text, "Stundenplan freigegeben")
        Assert.AreEqual(SchrittStand.Bereit, Klassenbildung(m, "Freigabe").Stand,
                        "die Freigabe des Stundenplans ist keine Freigabe der Klassenbildung")

        Assert.IsTrue(historie.Freigeben("kb"))
        Assert.AreEqual(SchrittStand.Erledigt, Klassenbildung(m, "Freigabe").Stand)
        StringAssert.Contains(Klassenbildung(m, "Freigabe").Text, "Klassenbildung freigegeben")
        Assert.AreEqual(SchrittStand.Erledigt, Klassenbildung(m, "Entscheiden").Stand,
                        "bei der Klassenbildung IST die Freigabe die Entscheidung")
    End Sub

    ''' <summary>Ohne angezeigten Stand fuehrt die Freigabe-Zeile in die
    ''' Historie, wo man einen waehlt - nicht in einen Dialog ohne
    ''' Gegenstand.</summary>
    <TestMethod>
    Public Sub FreigabeOhneAngezeigtenStandFuehrtInDieLaeufe()
        Dim m = MitStand()
        Stundenplan(m, "Freigabe").Aktion.Invoke()
        Assert.AreEqual(Bereich.Laeufe, m.Bereich)
    End Sub

    ' ===============================================================
    ' Bereichskopf: Stand-Wechsler und Leerseite
    ' ===============================================================

    <TestMethod>
    Public Sub DerStandWechslerZeigtNurDieStaendeSeinerRechnung()
        Dim m = MitStand()
        m.Projekt.StandHinzufuegen(New ProjektStand With {
            .Id = "kb", .Label = "Klassenbildung", .Erstellt = Jetzt.AddMinutes(1),
            .Klassenbildung = JsonNode.Parse("{""varianten"": []}").AsObject()})

        m.Bereich = Bereich.Stundenplan
        CollectionAssert.AreEqual(New List(Of String) From {"s1"}, m.StaendeDesBereichs().Select(Function(z) z.Id).ToList())
        m.Bereich = Bereich.Klassenbildung
        CollectionAssert.AreEqual(New List(Of String) From {"kb"}, m.StaendeDesBereichs().Select(Function(z) z.Id).ToList())
        m.Bereich = Bereich.Start
        Assert.AreEqual(0, m.StaendeDesBereichs().Count)
    End Sub

    ''' <summary>Ohne Ergebnis hat der Bereich keine Anzeige - das Fenster
    ''' zeigt dann die Leerseite. Gibt es Staende, wird beim Betreten der
    ''' juengste vorgemerkt: "Noch kein Ergebnis" neben einem Stand-Wechsler
    ''' voller Staende waere eine Luege (Bildprobe, 05.09.2026).</summary>
    <TestMethod>
    Public Sub BeimBetretenWirdDerLetzteStandAngezeigt()
        Dim m = MitSchule()
        m.Bereich = Bereich.Stundenplan
        Assert.IsFalse(m.HatAnzeige, "ohne Stand bleibt es leer")
        Assert.IsNull(m.AngezeigterStand())

        m.Bereich = Bereich.Start
        m.Projekt.StandHinzufuegen(New ProjektStand With {
            .Id = "s1", .Label = "Stundenplan, 1 Loesung(en)", .Erstellt = Jetzt,
            .Stundenplan = JsonNode.Parse("{""solutions"": [{""muss_violation_count"": 0}]}").AsObject()})
        m.Bereich = Bereich.Stundenplan
        Assert.IsTrue(m.HatAnzeige)
        Assert.AreEqual("s1", m.AngezeigterStand().Id)

        m.Bereich = Bereich.Klassenbildung
        Assert.IsFalse(m.HatAnzeige, "das andere Dashboard hat weiterhin nichts")
    End Sub

    ' ===============================================================
    ' Projekt schliessen
    ' ===============================================================

    <TestMethod>
    Public Sub SchliessenFragtBeiAenderungenUndLeertDieFlaeche()
        ' Ohne Projekt passiert nichts, auch keine Frage.
        Dim dLeer As New TestDialoge()
        Dim mLeer As New HauptViewModel(dLeer, Function() Jetzt)
        mLeer.Schliessen()
        Assert.AreEqual(0, dLeer.Fragen.Count)

        ' Nein heisst Nein.
        Dim dNein As New TestDialoge With {.FrageAntwort = False}
        Dim mNein = MitSchule(dNein)
        mNein.Geaendert = True
        mNein.Schliessen()
        Assert.AreEqual(1, dNein.Fragen.Count)
        Assert.IsTrue(mNein.ProjektOffen)

        ' Ja leert Projekt, Flaeche und Anzeige.
        Dim dJa As New TestDialoge With {.FrageAntwort = True}
        Dim mJa = MitSchule(dJa)
        mJa.Projekt.StandHinzufuegen(New ProjektStand With {
            .Id = "s1", .Label = "Stundenplan, 1 Loesung(en)", .Erstellt = Jetzt,
            .Stundenplan = JsonNode.Parse("{""solutions"": [{""muss_violation_count"": 0}]}").AsObject()})
        mJa.StandAnzeigen("s1")
        Assert.IsTrue(mJa.HatAnzeige)
        mJa.Geaendert = True

        mJa.Schliessen()
        Assert.AreEqual(1, dJa.Fragen.Count)
        Assert.IsFalse(mJa.ProjektOffen)
        Assert.AreEqual(Bereich.Start, mJa.Bereich)
        Assert.IsFalse(mJa.Geaendert)
        mJa.Bereich = Bereich.Stundenplan
        Assert.IsFalse(mJa.HatAnzeige, "die alte Seite darf das naechste Projekt nicht ueberleben")
    End Sub


    ' ===============================================================
    ' Die Karten im Fenster
    ' ===============================================================

    ''' <summary>Karten, Kopf und Leerseite entstehen zur Laufzeit aus
    ''' Vorlagen - StaticResource-Schluessel darin sieht kein Compiler.
    ''' Dieser Test baut das Hauptfenster wirklich auf; er ist zugleich
    ''' die einzige Aufbaupruefung, die MainWindow ueberhaupt hat - und
    ''' der Beleg fuer die Seitenleiste, die dem Bereich FOLGT.</summary>
    <TestMethod>
    Public Sub DasHauptfensterBautKartenKopfUndLeerseiteAuf()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim f As New MainWindow()
                   Dim m = CType(f.DataContext, HauptViewModel)

                   Dim karten = CType(f.Startkarten.ItemsSource, IEnumerable(Of HauptViewModel.Startkarte)).ToList()
                   Assert.AreEqual(2, karten.Count)
                   Assert.AreEqual("Klassenbildung", karten(0).Titel)

                   ' Die Vorlagen muessen sich auch wirklich erzeugen lassen -
                   ' ein Fehler darin faellt sonst erst beim Anzeigen auf.
                   f.Startkarten.Measure(New Size(1200, 800))
                   f.Startkarten.UpdateLayout()
                   Assert.AreEqual(2, f.Startkarten.Items.Count)
                   Assert.AreEqual(Visibility.Visible, f.Startseite.Visibility)
                   Assert.AreEqual(Visibility.Collapsed, f.Bereichskopf.Visibility)
                   Assert.IsTrue(f.SchalterStart.IsChecked)

                   ' Wechsel aus dem MODELL - die Seitenleiste folgt, und ohne
                   ' Ergebnis steht die Leerseite statt des Dashboards.
                   m.Bereich = Bereich.Stundenplan
                   Assert.IsTrue(f.SchalterStundenplan.IsChecked)
                   Assert.IsFalse(f.SchalterStart.IsChecked)
                   Assert.AreEqual(Visibility.Collapsed, f.Startseite.Visibility)
                   Assert.AreEqual(Visibility.Visible, f.Bereichskopf.Visibility)
                   Assert.AreEqual(Visibility.Visible, f.Leerseite.Visibility)
                   Assert.AreEqual(Visibility.Collapsed, f.Dashboard.Visibility)
                   Assert.AreEqual("Stundenplan", f.Kopftitel.Text)
                   Assert.AreEqual(2, f.Kopfzeilen.Items.Count)
                   f.Kopfzeilen.Measure(New Size(1200, 60))
                   f.Kopfzeilen.UpdateLayout()
                   f.Leerzeilen.Measure(New Size(600, 800))
                   f.Leerzeilen.UpdateLayout()
                   Assert.AreEqual(5, f.Leerzeilen.Items.Count)
                   Assert.IsFalse(f.Standwahl.IsEnabled, "ohne Staende gibt es nichts zu wechseln")

                   ' Und andersherum: der Schalter setzt den Bereich.
                   f.SchalterLaeufe.IsChecked = True
                   Assert.AreEqual(Bereich.Laeufe, m.Bereich)
                   Assert.AreEqual(Visibility.Visible, f.Laeufe.Visibility)
                   Assert.AreEqual(Visibility.Collapsed, f.Bereichskopf.Visibility)
               End Sub)
    End Sub

End Class
