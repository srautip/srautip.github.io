' Tests fuer den Abbruch- und Fortschrittskanal (arc42 8.11).
'
' Leitregel dieser Datei: KEIN Thread.Sleep, kein "warten und hoffen". Ein
' Abbruchtest, der darauf baut, dass ein Solve laenger als X Millisekunden
' braucht, ist auf schneller Hardware oder unter Last flaky. Beide hier
' benutzten Ausloeser sind stattdessen deterministisch:
'   1. ein VORAB gesetztes Token - der Aufruf darf dann gar nichts tun;
'   2. Abbruch AUS DEM Fortschritts-Handler heraus - RunSolve meldet
'      garantiert einmal beim Phasenstart, der Handler feuert also
'      unabhaengig davon, wie schnell CP-SAT fertig wird.
'
' Bewusst NICHT Progress(Of T): das stellt ueber den
' SynchronizationContext zu, und im Testkontext gibt es keinen - die
' Zustellung liefe also asynchron ueber den ThreadPool. Damit waeren die
' Abbruchtests wieder ein Wettrennen, und im Exception-Test wuerde die
' Ausnahme auf einem fremden Thread landen statt in RunSolves Try/Catch,
' das der Test ja gerade pruefen soll. SofortProgress ruft synchron auf dem
' meldenden Thread auf - das ist zugleich der haertere Test.
Imports System.Diagnostics
Imports System.Text.Json.Nodes
Imports System.Threading
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

''' <summary>Synchroner IProgress fuer Tests - siehe Dateikopf.</summary>
Friend NotInheritable Class SofortProgress
    Implements IProgress(Of SolveProgress)

    Private ReadOnly _aktion As Action(Of SolveProgress)
    Private ReadOnly _gate As New Object()
    Private ReadOnly _gesehen As New List(Of SolveProgress)

    Public Sub New(aktion As Action(Of SolveProgress))
        _aktion = aktion
    End Sub

    Public Sub Report(value As SolveProgress) Implements IProgress(Of SolveProgress).Report
        SyncLock _gate
            _gesehen.Add(value)
        End SyncLock
        If _aktion IsNot Nothing Then _aktion(value)
    End Sub

    Public ReadOnly Property Meldungen As List(Of SolveProgress)
        Get
            SyncLock _gate
                Return New List(Of SolveProgress)(_gesehen)
            End SyncLock
        End Get
    End Property
End Class

<TestClass>
Public Class CancellationProgressTests

    ''' <summary>Eine Wochenstunde auf 5 Tagen x 6 Stunden: 30 gleich gute,
    ''' verschiedene Loesungen. Damit laeuft SolveTops Iterationsschleife
    ''' garantiert mehrfach, ohne dass ein einzelner Solve lange braucht -
    ''' genau das, was ein Abbruchtest zwischen Iterationen braucht.</summary>
    Private Shared Function VieleLoesungen() As JsonObject
        Return Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo", "Di", "Mi", "Do", "Fr"}, 6), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
    End Function

    Private Shared Function Winzig() As JsonObject
        Return Scenario(Mini({"5a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "5a"}, {"subject", "Mathe"}}
        })
    End Function

    ''' <summary>Minimales, gueltiges Kursstufen-Szenario (ein Kurs, eine
    ''' Schiene) - bewusst LOESBAR, damit der Vorab-Abbruch-Test wirklich
    ''' belegt, dass nicht gerechnet wurde, statt nur an ungueltigen Daten
    ''' zu scheitern.</summary>
    Private Shared Function KursstufeSzenario() As JsonObject
        Dim ent = Mini({"J1"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 2)
        ent("kurse") = New JsonArray(New JsonNode() {
            New JsonObject From {{"id", "K1"}, {"kursart", "GK"}, {"hours_per_week", 2}, {"teacher", "T1"}}})
        ent("schienen") = New JsonArray(New JsonNode() {
            New JsonObject From {{"id", "S1"}, {"kursart", "GK"}, {"hours_per_week", 2}}})
        Return Scenario(ent, New JsonObject() {})
    End Function

    ' ---------------------------------------------------------------
    ' 1. Vorab gesetztes Token: gar kein Solve
    ' ---------------------------------------------------------------

    ''' <summary>Grosszuegiges Zeitlimit, aber bereits abgebrochen: jeder
    ''' Einstiegspunkt muss sofort zurueckkehren. Die Laufzeitschranke ist
    ''' absichtlich weit (2s gegen 600s Budget) - sie belegt "es wurde nicht
    ''' gerechnet", ohne von der Maschinenleistung abzuhaengen.</summary>
    <TestMethod>
    Public Sub PreCancelledTokenReturnsImmediatelyWithoutSolving()
        Dim cts As New CancellationTokenSource()
        cts.Cancel()
        Dim data = VieleLoesungen()
        Dim sw = Stopwatch.StartNew()

        Dim einzeln = Solver.Solve(data, timeLimitS:=600.0, cancellationToken:=cts.Token)
        Assert.IsTrue(einzeln.Cancelled, "Solve: Cancelled nicht gesetzt")
        Assert.AreEqual(CpSolverStatus.Unknown, einzeln.Status)
        Assert.IsNull(einzeln.Solver, "Solve: es haette nicht einmal ein Modell gebaut werden duerfen")

        Dim top = Solver.SolveTop(data, maxSolutions:=10, totalTimeLimitS:=600.0, cancellationToken:=cts.Token)
        Assert.AreEqual(MultiSolveStopReason.Cancelled, top.StopReason)
        Assert.AreEqual(0, top.Solutions.Count)

        Dim kurs = Kursblockung.SolveKursblockung(KursstufeSzenario(), timeLimitS:=600.0, cancellationToken:=cts.Token)
        Assert.IsTrue(kurs.Cancelled, "SolveKursblockung: Cancelled nicht gesetzt")
        Assert.IsNull(kurs.Assignment)

        Dim kursstufe = Solver.SolveKursstufe(KursstufeSzenario(), timeLimitS:=600.0, cancellationToken:=cts.Token)
        Assert.IsTrue(kursstufe.Cancelled, "SolveKursstufe: Cancelled nicht gesetzt")

        sw.Stop()
        Assert.IsTrue(sw.Elapsed.TotalSeconds < 2.0,
                      $"Vorab abgebrochene Aufrufe brauchten {sw.Elapsed.TotalSeconds:F1}s - da wurde gerechnet")
    End Sub

    ''' <summary>Gegenprobe zum vorigen Test: derselbe Aufruf OHNE Token
    ''' loest das Szenario tatsaechlich. Ohne diese Absicherung koennte
    ''' PreCancelled... auch dann gruen sein, wenn das Szenario schlicht
    ''' unloesbar waere.</summary>
    <TestMethod>
    Public Sub PreCancelledCounterProofScenarioIsActuallySolvable()
        Dim kurs = Kursblockung.SolveKursblockung(KursstufeSzenario(), timeLimitS:=30.0)
        Assert.IsFalse(kurs.Cancelled)
        Assert.IsTrue(kurs.Status = CpSolverStatus.Optimal OrElse kurs.Status = CpSolverStatus.Feasible,
                      $"Kursblockungs-Szenario ist nicht loesbar (Status={kurs.Status})")
        Assert.IsNotNull(kurs.Assignment)
    End Sub

    ' ---------------------------------------------------------------
    ' 2. Abbruch aus dem Fortschritts-Handler
    ' ---------------------------------------------------------------

    ''' <summary>Der Handler bricht bei der ersten Meldung ab. Weil RunSolve
    ''' beim Phasenstart garantiert meldet, ist der Ausloeser unabhaengig von
    ''' der Solve-Dauer - kein Timing-Glueck noetig.</summary>
    <TestMethod>
    Public Sub CancellationFromProgressHandlerStopsSolveTop()
        Dim cts As New CancellationTokenSource()
        Dim progress As New SofortProgress(Sub(p) cts.Cancel())

        Dim sw = Stopwatch.StartNew()
        Dim result = Solver.SolveTop(VieleLoesungen(), maxSolutions:=25, totalTimeLimitS:=600.0,
                                     perSolveTimeLimitS:=600.0,
                                     cancellationToken:=cts.Token, progress:=progress)
        sw.Stop()

        Assert.IsTrue(progress.Meldungen.Count > 0, "Es kam keine Fortschrittsmeldung an")
        Assert.AreEqual(MultiSolveStopReason.Cancelled, result.StopReason)
        Assert.IsTrue(result.Solutions.Count < 25,
                      "Trotz Abbruch wurden alle angeforderten Loesungen gerechnet")
        Assert.IsTrue(sw.Elapsed.TotalSeconds < 60.0,
                      $"Abbruch dauerte {sw.Elapsed.TotalSeconds:F1}s - Budget waren 600s")
    End Sub

    ''' <summary>Ein Abbruch darf das bereits Gefundene nicht wegwerfen - das
    ''' ist der Grund, warum der Kern hier nicht wirft. Der Handler laesst
    ''' erst eine Loesung entstehen und bricht dann ab.</summary>
    <TestMethod>
    Public Sub SolveTopCancelledKeepsSolutionsFoundSoFar()
        Dim data = VieleLoesungen()
        Dim cts As New CancellationTokenSource()
        Dim progress As New SofortProgress(Sub(p)
                                               If p.SolutionsFound >= 1 Then cts.Cancel()
                                           End Sub)

        Dim result = Solver.SolveTop(data, maxSolutions:=25, totalTimeLimitS:=600.0,
                                     perSolveTimeLimitS:=600.0,
                                     cancellationToken:=cts.Token, progress:=progress)

        Assert.AreEqual(MultiSolveStopReason.Cancelled, result.StopReason)
        Assert.IsTrue(result.Solutions.Count > 0,
                      "Der Abbruch hat die bereits gefundenen Loesungen verworfen")
        Assert.IsTrue(result.Solutions.Count < 25)
        ' Das Teilergebnis muss uneingeschraenkt verwertbar sein.
        Assert.IsNotNull(result.Solutions(0).Schedule)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, result.Solutions(0).Schedule).Count)
    End Sub

    ' ---------------------------------------------------------------
    ' 3. Form der Fortschrittsmeldungen
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub ProgressReportsAreMonotonicAndWellFormed()
        Dim progress As New SofortProgress(Nothing)

        Solver.SolveTop(VieleLoesungen(), maxSolutions:=3, totalTimeLimitS:=60.0, progress:=progress)

        Dim meldungen = progress.Meldungen
        Assert.IsTrue(meldungen.Count > 0, "Es kam keine einzige Fortschrittsmeldung an")

        Dim letzteZeit = -1.0
        For Each p In meldungen
            Assert.IsFalse(String.IsNullOrWhiteSpace(p.Label), "Meldung ohne Label")
            Assert.IsTrue(p.ElapsedS >= 0.0)
            Assert.IsTrue(p.ElapsedS >= letzteZeit,
                          $"ElapsedS faellt: {p.ElapsedS:F3} nach {letzteZeit:F3}")
            letzteZeit = p.ElapsedS
            Assert.IsTrue(p.PhaseIndex >= 1, $"PhaseIndex {p.PhaseIndex} ist nicht 1-basiert")
            If p.PhaseCount > 0 Then
                Assert.IsTrue(p.PhaseIndex <= p.PhaseCount,
                              $"PhaseIndex {p.PhaseIndex} liegt ausserhalb von 1..{p.PhaseCount}")
            End If
            Assert.IsTrue(p.SolutionsFound >= 0)
            Assert.IsTrue(p.BudgetS >= 0.0)
        Next
    End Sub

    ''' <summary>Ein fehlerhafter GUI-Handler darf den Solverlauf nicht
    ''' mitreissen - deshalb kapselt RunSolve jeden Report in Try/Catch.
    ''' Weil SofortProgress synchron aufruft, landet die Ausnahme genau
    ''' dort, wo dieser Schutz sitzt.</summary>
    <TestMethod>
    Public Sub ProgressHandlerExceptionDoesNotFailSolve()
        Dim data = Winzig()
        Dim progress As New SofortProgress(Sub(p) Throw New InvalidOperationException("kaputter Handler"))

        Dim result = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=30.0, progress:=progress)

        Assert.AreEqual(1, result.Solutions.Count)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, result.Solutions(0).Schedule).Count)
    End Sub

    ' ---------------------------------------------------------------
    ' 4. Der Pfad ohne Token/Progress bleibt unveraendert (arc42 8.5)
    ' ---------------------------------------------------------------

    ''' <summary>Absicherung der Determinismus-Zusage: ohne Token und ohne
    ''' Progress nimmt RunSolve den Fast-Path (direkter, blockierender
    ''' solver.Solve auf dem aufrufenden Thread). Bei numWorkers:=1 und
    ''' festem seed muessen zwei Laeufe denselben Plan liefern.</summary>
    <TestMethod>
    Public Sub DefaultCallPathUnchangedAndDeterministic()
        Dim data = VieleLoesungen()
        Dim a = Solver.SolveTop(data, maxSolutions:=5, totalTimeLimitS:=60.0, seed:=42, numWorkers:=1)
        Dim b = Solver.SolveTop(data, maxSolutions:=5, totalTimeLimitS:=60.0, seed:=42, numWorkers:=1)

        Assert.AreEqual(a.StopReason, b.StopReason)
        Assert.AreEqual(a.Solutions.Count, b.Solutions.Count)
        Assert.IsTrue(a.Solutions.Count > 0)
        For i = 0 To a.Solutions.Count - 1
            Assert.AreEqual(Signatur(a.Solutions(i).Schedule), Signatur(b.Solutions(i).Schedule),
                            $"Loesung {i} unterscheidet sich zwischen zwei identischen Laeufen")
        Next
    End Sub

    Private Shared Function Signatur(schedule As List(Of ScheduleEntry)) As String
        Return String.Join("|", schedule.
            Select(Function(e) $"{e.ClassName}/{e.Subject}/{e.Teacher}/{e.Day}/{e.Period}").
            OrderBy(Function(s) s))
    End Function

End Class
