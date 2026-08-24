' Stufe B des GUI-Unterbaus: die Pipeline lag bis hierher als
' 280-Zeilen-Prozedur mit Dateipfad-Ein- und -Ausgabe in
' SchoolTestRunner/Run.vb und war damit ueberhaupt nicht testbar. Jetzt ist
' sie ein I/O-freier Dienst - und diese Datei ist der erste Test, den es
' fuer sie je gab.
'
' Gefahren wird gegen eine per Scaffold erzeugte EINZUEGIGE Grundschule mit
' einer Klassenstufe. Der erste Entwurf nahm die echte
' bw-grundschule-beispiel-Fixture - die braucht laut ihrer eigenen
' config.yaml aber 180s Solve-Budget, und mit den 30s einer Testsuite
' verbraucht schon die lexikografische Vorphase das gesamte Budget, sodass
' SolveTop mit NULL Loesungen zurueckkam. Realistische Groessen gehoeren in
' die `run`-Beispiellaeufe, nicht in eine Unit-Suite.
'
' BEWUSST NICHT geprueft werden konkrete Qualitaetszahlen. Der
' Stagnations-Cutoff ist wanduhrgesteuert (arc42 8.5), die Menge der
' gefundenen Alternativloesungen daher zwischen zwei Laeufen desselben
' Codes nicht stabil - live nachgewiesen beim A/B-Vergleich dieser Stufe.
' Geprueft wird deshalb Struktur und Invarianten, nicht Zahlenwerte.
Imports System.Threading
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableWorkflow
Imports TimetableYaml

<TestClass>
Public Class StundenplanLaufTests

    Private Shared _tempRoot As String

    ''' <summary>Erzeugt die Testschule einmal per Scaffold - das validiert
    ''' seinen Bestand selbst (StammdatenValidation) und wirft sonst, der
    ''' Test steht also auf gepruefter Grundlage.</summary>
    <ClassInitialize>
    Public Shared Sub Aufbauen(ctx As TestContext)
        _tempRoot = IO.Path.Combine(IO.Path.GetTempPath(), "ttwf-" & Guid.NewGuid().ToString("N"))
        Scaffold.Run(_tempRoot, "mini", "BW", "Grundschule",
                     klassenstufenAnzahl:=1, lehrerAnzahl:=4, zuege:=1)
    End Sub

    <ClassCleanup>
    Public Shared Sub Abraeumen()
        If _tempRoot IsNot Nothing AndAlso IO.Directory.Exists(_tempRoot) Then
            IO.Directory.Delete(_tempRoot, recursive:=True)
        End If
    End Sub

    Private Shared Function Mini() As (Bestand As Stammdatenbestand, Regeln As List(Of System.Text.Json.Nodes.JsonObject))
        Dim inputDir = IO.Path.Combine(_tempRoot, "mini", "input")
        Return (YamlStammdaten.LoadStammdatenYaml(IO.Path.Combine(inputDir, "stammdaten.yaml")),
                YamlConstraints.LoadConstraintsYaml(IO.Path.Combine(inputDir, "constraints.yaml")))
    End Function

    ''' <summary>Knapp gehaltene Testkonfiguration: eine Zuteilung, eine
    ''' Loesung, numWorkers 1. Der Test soll die ABFOLGE belegen, nicht die
    ''' Planqualitaet - dafuer gibt es die Beispiel-Laeufe.</summary>
    Private Shared Function SchnelleConfig() As RunConfig
        Return New RunConfig With {
            .LehrereinsatzTimeLimitS = 30.0,
            .SolveTimeLimitS = 30.0,
            .MaxSolutions = 1,
            .NumWorkers = 1,
            .Seed = 42
        }
    End Function

    <TestMethod>
    Public Sub PipelineLaeuftBisFertigDurch()
        Dim gs = Mini()

        Dim e = StundenplanLauf.Ausfuehren(gs.Bestand, gs.Regeln, SchnelleConfig())

        Assert.AreEqual(LaufStufe.Fertig, e.Stufe, "Pipeline nicht bis Fertig gekommen: " & String.Join(" | ", e.Meldungen))
        Assert.IsFalse(e.Abgebrochen)
        Assert.IsTrue(e.Erfolgreich, "Lauf nicht erfolgreich, Verstoesse: " & String.Join(" | ", e.PlanVerstoesse))
        Assert.AreEqual(0, e.LehrereinsatzVerstoesse)
        Assert.AreEqual(0, e.PlanVerstoesse.Count)
        Assert.IsNotNull(e.BesterLauf)
        Assert.IsNotNull(e.BesteLoesung)
        Assert.IsTrue(e.Einsaetze.Count >= 1)
        Assert.IsTrue(e.Laeufe.Count >= 1)
        Assert.IsNotNull(e.Gewichte, "aufgeloeste QualityWeights fehlen")
    End Sub

    ''' <summary>Die vier Werte, an denen sich CLI und GUI auseinander
    ''' entwickeln koennten, wenn jemand die Aufloesung dupliziert statt den
    ''' Dienst zu benutzen.</summary>
    <TestMethod>
    Public Sub PipelineLoestConfigDefaultsWieSolveTopAuf()
        Dim gs = Mini()
        Dim cfg = SchnelleConfig()
        cfg.MaxSolutions = 4
        cfg.MaxAssignments = 2

        Dim e = StundenplanLauf.Ausfuehren(gs.Bestand, gs.Regeln, cfg)

        Assert.AreEqual(LaufStufe.Fertig, e.Stufe)
        ' Budget und max_solutions werden gleichmaessig auf die Zuteilungen
        ' verteilt; max_solutions rundet dabei AUF.
        Assert.AreEqual(cfg.SolveTimeLimitS / e.Einsaetze.Count, e.PerZuteilungBudgetS, 1.0E-9)
        Dim erwartet = Math.Max(1, (cfg.MaxSolutions + e.Einsaetze.Count - 1) \ e.Einsaetze.Count)
        Assert.AreEqual(erwartet, e.PerZuteilungMaxLoesungen)
        ' per_solve_time_limit_s nicht gesetzt -> faellt auf das Gesamtbudget
        ' zurueck (NICHT auf 0 oder einen eigenen Default).
        Assert.AreEqual(cfg.SolveTimeLimitS, e.PerSolveLimitS, 1.0E-9)
    End Sub

    ''' <summary>Der beste Lauf ist der mit der niedrigsten Quality.Total -
    ''' die eine Zeile, die im Mehr-Zuteilungs-Modus entscheidet, welcher
    ''' Plan berichtet wird.</summary>
    <TestMethod>
    Public Sub BesterLaufIstDerMitNiedrigsterQualityTotal()
        Dim gs = Mini()
        Dim cfg = SchnelleConfig()
        cfg.MaxSolutions = 4
        cfg.MaxAssignments = 2

        Dim e = StundenplanLauf.Ausfuehren(gs.Bestand, gs.Regeln, cfg)

        Assert.IsNotNull(e.BesterLauf)
        Dim erfolgreiche = e.Laeufe.Where(Function(r) r.Result.Solutions.Count > 0).ToList()
        For Each r In erfolgreiche
            Assert.IsTrue(e.BesterLauf.Result.Solutions(0).Quality.Total <= r.Result.Solutions(0).Quality.Total,
                          $"Zuteilung {r.AssignmentIndex} hat eine bessere Loesung als der gewaehlte beste Lauf")
        Next
    End Sub

    ''' <summary>Vorab gesetztes Token: die Pipeline darf nicht rechnen.
    ''' Grosszuegige Zeitlimits, harte Laufzeitschranke - so belegt der Test
    ''' "es wurde nichts gerechnet" ohne Timing-Annahme.</summary>
    <TestMethod>
    Public Sub PipelineVorabAbgebrochenRechnetNicht()
        Dim gs = Mini()
        Dim cfg = SchnelleConfig()
        cfg.LehrereinsatzTimeLimitS = 600.0
        cfg.SolveTimeLimitS = 600.0
        Dim cts As New CancellationTokenSource()
        cts.Cancel()

        Dim sw = Diagnostics.Stopwatch.StartNew()
        Dim e = StundenplanLauf.Ausfuehren(gs.Bestand, gs.Regeln, cfg, cts.Token)
        sw.Stop()

        Assert.IsTrue(e.Abgebrochen, "Abgebrochen nicht gesetzt")
        Assert.IsFalse(e.Erfolgreich)
        Assert.AreEqual(0, e.Laeufe.Count, "es wurde trotz Abbruch ein Stundenplan gerechnet")
        Assert.IsNull(e.BesterLauf)
        Assert.IsTrue(sw.Elapsed.TotalSeconds < 5.0,
                      $"Abbruch brauchte {sw.Elapsed.TotalSeconds:F1}s - da wurde gerechnet")
    End Sub

    ''' <summary>Der Lauf-Monitor (gui-ui-konzept 6.13) zeigt die Schrittfolge
    ''' "Validierung -> Lehrereinsatz -> Verifikation -> Stundenplan". Dafuer
    ''' muessen die Meldungen der KERN-Aufrufe auf Pipeline-Stufen
    ''' umetikettiert ankommen, nicht als deren eigene Iterationszaehler.</summary>
    <TestMethod>
    Public Sub PipelineMeldetFortschrittInPipelineStufen()
        Dim gs = Mini()
        Dim gesehen As New List(Of SolveProgress)
        Dim gate As New Object()
        Dim progress As New SofortProgress(Sub(p)
                                               SyncLock gate
                                                   gesehen.Add(p)
                                               End SyncLock
                                           End Sub)

        StundenplanLauf.Ausfuehren(gs.Bestand, gs.Regeln, SchnelleConfig(), Nothing, progress)

        Dim kopie As List(Of SolveProgress)
        SyncLock gate
            kopie = New List(Of SolveProgress)(gesehen)
        End SyncLock

        Assert.IsTrue(kopie.Count > 0, "keine Fortschrittsmeldung angekommen")
        For Each p In kopie
            Assert.AreEqual(SolvePhase.Stufe, p.Phase, $"Meldung '{p.Label}' traegt die Phase des Kernaufrufs statt der Pipeline")
            Assert.IsTrue(p.PhaseIndex >= 1 AndAlso p.PhaseIndex <= p.PhaseCount,
                          $"PhaseIndex {p.PhaseIndex} liegt ausserhalb von 1..{p.PhaseCount}")
            Assert.IsFalse(String.IsNullOrWhiteSpace(p.Label), "Meldung ohne Label")
            Assert.IsTrue(p.BudgetS > 0.0, "kein Gesamtbudget gemeldet")
        Next
        ' Die Stufen muessen aufsteigen - der Monitor zeigt sonst Ruecksprünge.
        Dim indizes = kopie.Select(Function(p) p.PhaseIndex).ToList()
        For i = 1 To indizes.Count - 1
            Assert.IsTrue(indizes(i) >= indizes(i - 1),
                          $"Stufe faellt zurueck: {indizes(i - 1)} -> {indizes(i)}")
        Next
        Assert.IsTrue(kopie.Any(Function(p) p.Label.Contains("Stammdaten")), "Validierungsstufe wurde nicht gemeldet")
        Assert.IsTrue(kopie.Any(Function(p) p.Label.Contains("Stundenplan")), "Stundenplanstufe wurde nicht gemeldet")
    End Sub

    ''' <summary>Fail-Fast: ungueltige Stammdaten muessen VOR dem ersten
    ''' Solve auffallen und als Klartext zurueckkommen - die GUI deaktiviert
    ''' daraufhin ihre Rechnen-Aktionen (gui-ui-konzept 7).</summary>
    <TestMethod>
    Public Sub PipelineMeldetStammdatenfehlerStattZuRechnen()
        Dim gs = Mini()
        ' Ein Fach ohne jede qualifizierte Lehrkraft - der Klassiker.
        gs.Bestand.FachLehrerZuordnungen.Clear()

        Dim sw = Diagnostics.Stopwatch.StartNew()
        Dim e = StundenplanLauf.Ausfuehren(gs.Bestand, gs.Regeln, SchnelleConfig())
        sw.Stop()

        Assert.AreEqual(LaufStufe.Stammdatenpruefung, e.Stufe)
        Assert.IsFalse(e.Erfolgreich)
        Assert.IsTrue(e.Meldungen.Count > 0, "keine Klartextmeldung geliefert")
        Assert.AreEqual(0, e.Einsaetze.Count, "es wurde trotz Stammdatenfehler gerechnet")
        Assert.IsTrue(sw.Elapsed.TotalSeconds < 5.0, "Fail-Fast hat nicht fail-fast reagiert")
    End Sub

    ''' <summary>Die Berichte muessen aus jedem Ergebniszustand
    ''' erzeugbar sein - auch aus einem gescheiterten. Ein NullReference im
    ''' Fehlerfall waere genau der Bug, der erst beim ersten echten Problem
    ''' auffiele.</summary>
    <TestMethod>
    Public Sub BerichteSindAuchAusFehlerzustaendenErzeugbar()
        Dim gs = Mini()
        gs.Bestand.FachLehrerZuordnungen.Clear()
        Dim gescheitert = StundenplanLauf.Ausfuehren(gs.Bestand, gs.Regeln, SchnelleConfig())

        Dim lehrerMd = StundenplanBericht.BaueLehrerzuteilungMarkdown(gescheitert)
        Assert.IsNotNull(lehrerMd)
        StringAssert.Contains(lehrerMd, "StammdatenValidation FEHLGESCHLAGEN")
        Assert.IsNull(StundenplanBericht.BaueStundenplanMarkdown(gescheitert),
                      "stundenplan.md wurde erzeugt, obwohl die Stufe nie erreicht wurde")

        Dim erfolgreich = StundenplanLauf.Ausfuehren(Mini().Bestand, gs.Regeln, SchnelleConfig())
        Dim planMd = StundenplanBericht.BaueStundenplanMarkdown(erfolgreich)
        Assert.IsNotNull(planMd)
        StringAssert.Contains(planMd, "## Optimalitaets-Luecke")
        StringAssert.Contains(planMd, "## Klassen")
        StringAssert.Contains(planMd, "## Lehrkraefte")
        Assert.IsNotNull(StundenplanBericht.BaueStundentafelJson(erfolgreich))
    End Sub

End Class

''' <summary>Synchroner IProgress - Progress(Of T) waere im Testkontext
''' asynchron ueber den ThreadPool zugestellt und damit ein Wettrennen
''' (gleiche Begruendung wie in TimetableCore.Tests).</summary>
Friend NotInheritable Class SofortProgress
    Implements IProgress(Of SolveProgress)

    Private ReadOnly _aktion As Action(Of SolveProgress)

    Public Sub New(aktion As Action(Of SolveProgress))
        _aktion = aktion
    End Sub

    Public Sub Report(value As SolveProgress) Implements IProgress(Of SolveProgress).Report
        If _aktion IsNot Nothing Then _aktion(value)
    End Sub
End Class
