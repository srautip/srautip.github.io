' Stundenplan rechnen und anzeigen, Stufe G1.
'
' Der Lauf selbst ist in TimetableWorkflow.Tests abgedeckt. Hier geht es
' um das, was die Oberflaeche daraus macht: landet die Seite im richtigen
' Dashboard, entsteht ein Stand, steht der Lauf im Protokoll - und bleibt
' das Klassenbildungs-Board davon unberuehrt.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableWorkflow
Imports TimetableYaml

<TestClass>
Public Class StundenplanLaufTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttsp-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(_ordner)
    End Sub

    <TestCleanup>
    Public Sub Abraeumen()
        If IO.Directory.Exists(_ordner) Then IO.Directory.Delete(_ordner, recursive:=True)
    End Sub

    ''' <summary>Eine MINI-Schule statt der Beispielschule: eine
    ''' Klassenstufe, ein Zug, vier Lehrkraefte. Genau das Vorgehen von
    ''' TimetableWorkflow.Tests - der Test soll die ABFOLGE der Oberflaeche
    ''' belegen, nicht die Planqualitaet, und die Beispielschule braucht
    ''' dafuer ein Vielfaches der Zeit (erst gemessen: 20 s reichten ihr
    ''' nicht einmal fuer eine einzige Loesung).
    '''
    ''' Der Weg fuehrt ueber den Assistenten-Entwurf, also durch dieselbe
    ''' Tuer wie in der Anwendung.</summary>
    Private Function MiniSchule() As (Modell As HauptViewModel, Dialoge As TestDialoge)
        Dim bestand = Scaffold.Baue("BW", "Grundschule",
                                    klassenstufenAnzahl:=1, lehrerAnzahl:=4, zuege:=1,
                                    schulName:="Minischule")
        Dim entwurf As New ProjektEntwurf With {
            .Bestand = bestand,
            .Pfad = IO.Path.Combine(_ordner, "mini.splanx"),
            .Passwort = "geheim12"}
        Dim d As New TestDialoge With {.AssistentEntwurf = entwurf}
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Neu()
        m.Projekt.Config = New RunConfig With {
            .LehrereinsatzTimeLimitS = 30.0, .SolveTimeLimitS = 30.0,
            .MaxSolutions = 1, .NumWorkers = 1, .Seed = 42}
        Return (m, d)
    End Function

    ' ===============================================================
    ' Die Schwelle
    ' ===============================================================

    ''' <summary>Anders als die Klassenbildung braucht der Stundenplan
    ''' KEINE Einzelschueler (6.1) - die Schwelle ist der Stammdatenkern.
    ''' Die Minischule hat keinen einzigen Schueler und ist trotzdem
    ''' rechenbar; genau das ist die Aussage.</summary>
    <TestMethod>
    Public Sub RechnenBrauchtStammdatenAberKeineSchueler()
        Dim d As New TestDialoge With {.SpeichernPfad = IO.Path.Combine(_ordner, "leer.splanx")}
        Dim m As New HauptViewModel(d, Function() Jetzt)
        Assert.IsFalse(m.KannStundenplanRechnen, "ohne Projekt gibt es nichts zu rechnen")

        m.Neu()
        Assert.IsFalse(m.KannStundenplanRechnen, "das leere Projekt hat keine Klassen")

        Dim mini = MiniSchule()
        Assert.AreEqual(0, mini.Modell.Projekt.Bestand.Schueler.Count)
        Assert.IsTrue(mini.Modell.KannStundenplanRechnen)
        Assert.IsFalse(mini.Modell.KannKlassenbildungRechnen,
                       "die Klassenbildung braucht sehr wohl eine Einschulungsliste")
    End Sub

    ''' <summary>"Rechnen nur bei gruener Pruefung" (Konzept 1) - und der
    ''' Grund wird GENANNT, nicht nur der Knopf ausgegraut.</summary>
    <TestMethod>
    Public Async Function UnvollstaendigeStammdatenNennenDenGrund() As Task
        Dim mini = MiniSchule()
        Dim m = mini.Modell
        ' Ohne Qualifikationen ist der Bestand widerspruchsfrei, aber
        ' unvollstaendig - genau der Fall, den StammdatenPruefen abfaengt.
        m.Projekt.Bestand.FachLehrerZuordnungen.Clear()
        Dim vorher = m.Projekt.Staende.Count

        Await m.StundenplanRechnenAsync()

        Assert.AreEqual(vorher, m.Projekt.Staende.Count, "ein blockierter Lauf darf keinen Stand anlegen")
        Assert.AreEqual(0, m.Auslieferung.SeitenGroesse)
        Assert.AreEqual(1, mini.Dialoge.Hinweise.Count,
                        "der Grund muss GENANNT werden, nicht nur der Knopf tot sein")
        StringAssert.Contains(mini.Dialoge.Hinweise(0), "Stammdaten")
    End Function

    ' ===============================================================
    ' Der Lauf
    ' ===============================================================

    ''' <summary>Der Durchstich: rechnen, Seite im Stundenplan-Dashboard,
    ''' Stand, Protokoll. Und die neue Invariante von G1 - die beiden
    ''' Dashboards teilen sich EINEN Auslieferungs-Slot, duerfen sich aber
    ''' nicht gegenseitig ueberschreiben.</summary>
    <TestMethod>
    Public Async Function LaufLiefertSeiteStandUndProtokollUndStoertDasBoardNicht() As Task
        Dim m = MiniSchule().Modell
        Dim vorher = m.Projekt.Staende.Count

        Await m.StundenplanRechnenAsync()

        Assert.AreEqual(Bereich.Stundenplan, m.Bereich, "das Ergebnis gehoert dorthin, wo man es sieht")
        Assert.IsTrue(m.Auslieferung.SeitenGroesse > 0, "keine Seite ausgeliefert: " & m.Meldung)

        ' Die Seite ist der echte Stundentafel-Viewer, nicht irgendein HTML.
        Dim seite = Text.Encoding.UTF8.GetString(m.Auslieferung.Antwort(m.Auslieferung.SeitenUrl).Inhalt)
        StringAssert.Contains(seite, "stundentafel-data")

        Assert.AreEqual(vorher + 1, m.Projekt.Staende.Count)
        Dim stand = m.Projekt.Staende.Last()
        Assert.IsNotNull(stand.Stundenplan, "der Stand traegt das Viewer-JSON")
        StringAssert.Contains(stand.Id, "stundenplan")
        Assert.IsTrue(m.Geaendert, "ein Lauf macht das Projekt ungespeichert")

        Dim protokoll = m.Projekt.AuditLog.Where(Function(e) e.Aktion = "lauf").ToList()
        Assert.AreEqual(1, protokoll.Count)
        StringAssert.Contains(protokoll(0).Beschreibung, "Stundenplan gerechnet")

        ' Wechsel auf das andere Dashboard: dort liegt nichts, und der
        ' Stundenplan darf dabei nicht verlorengehen.
        m.Bereich = Bereich.Klassenbildung
        Assert.AreEqual(0, m.Auslieferung.SeitenGroesse,
                        "das Klassenbildungs-Board zeigt sonst den Stundenplan - gleiche URL, falscher Inhalt")
        m.Bereich = Bereich.Stundenplan
        Assert.IsTrue(m.Auslieferung.SeitenGroesse > 0, "die Stundenplan-Seite wurde beim Wechsel verworfen")
    End Function

    ''' <summary>Ein gespeicherter Stand laesst sich ohne neuen Lauf wieder
    ''' anzeigen - der Weg, ueber den die Staende-Historie (6.13)
    ''' arbeitet.</summary>
    <TestMethod>
    Public Async Function DerStandLaesstSichOhneNeuenLaufWiederAufbauen() As Task
        Dim m = MiniSchule().Modell
        Await m.StundenplanRechnenAsync()

        Dim stand = m.Projekt.Staende.Last()
        Dim seite = ViewerInhalt.AusGespeichertemJson(stand.Stundenplan, istKlassenbildung:=False)

        Assert.IsNotNull(seite)
        StringAssert.Contains(seite, "stundentafel-data")
    End Function

    ''' <summary>Ohne Loesung darf die Statuszeile nicht mit einem
    ''' Doppelpunkt und dann nichts enden - live erlebt, als der erste
    ''' Testlauf im Zeitbudget nichts fand: "Keine Loesung (Solverlauf): ".
    ''' Das ist die unbrauchbarste aller Meldungen.</summary>
    <TestMethod>
    Public Sub OhneLoesungNenntDieMeldungEinenGrund()
        Dim m = MiniSchule().Modell
        m.VerwerteStundenplan(New LaufErgebnis With {.Stufe = LaufStufe.Stundenplan})

        Assert.AreEqual(0, m.Auslieferung.SeitenGroesse)
        StringAssert.Contains(m.Meldung, "Solverlauf")
        StringAssert.Contains(m.Meldung, "Zeitbudget")
        Assert.IsFalse(m.Meldung.TrimEnd().EndsWith(":"), "Meldung endet im Nichts: " & m.Meldung)
    End Sub

End Class
