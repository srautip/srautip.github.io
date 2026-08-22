' Abbruch- und Fortschrittskanal fuer alle langlaufenden Solver-Einstiegs-
' punkte des Kerns (arc42 8.11). Vor dieser Datei war jeder Solve-Aufruf
' blockierend und stumm: ein GMS-Lauf mit solve_time_limit_s: 1200 lief 20
' Minuten ohne Lebenszeichen und ohne Abbruchmoeglichkeit - fuer eine GUI
' (Phase 3) nicht bedienbar.
'
' Der Mechanismus ist KEINE Neuerfindung: SolveWithStagnationCutoff loeste
' seit Phase 2.25 bereits jede SolveTop-Iteration auf einem Task, pollte alle
' 500ms und rief solver.StopSearch() (dort live-verifiziert, dass ein
' laufender cross-thread Solve() daraufhin zuegig mit seinem besten Zwischen-
' ergebnis zurueckkehrt). Diese Datei verallgemeinert genau diese Schleife zu
' SolveRunner.RunSolve und haengt Abbruch und Fortschritt daran.
Imports System.Diagnostics
Imports System.Threading
Imports System.Threading.Tasks
Imports Google.OrTools.Sat

''' <summary>Welcher Abschnitt eines mehrstufigen Laufs gerade rechnet.
''' Bewusst nur die Phasen, die auch tatsaechlich gemeldet werden - der
''' Modellbau selbst ist weder unterbrechbar noch nennenswert lang.</summary>
Public Enum SolvePhase
    ''' <summary>Eine lexikographische Stufe in SolveTop (Kann, Dichte,
    ''' Fach-Fenster, ClassGaps, TeacherGaps).</summary>
    LexStufe
    ''' <summary>Der vorgelagerte Warmstart-Solve (useStagedHints).</summary>
    WarmStart
    ''' <summary>Eine Iteration der SolveTop-Hauptschleife bzw. ein einzelner
    ''' Solve-Aufruf (Solve, SolveKursblockung, SolveLehrereinsatz).</summary>
    Iteration
    ''' <summary>Eine Variante der Klassenbildung (SolveKlassenbildungTop).</summary>
    Variante
    ''' <summary>Eine Zuteilung in SolveLehrereinsatzTop.</summary>
    Zuteilung
    ''' <summary>Eine Stufe der Kursstufen-Kette (Kursblockung ->
    ''' Schienenraster -> Raumzuordnung) bzw. der kombinierten Schule.</summary>
    Stufe
End Enum

''' <summary>Eine Fortschrittsmeldung. Flach gehalten, weil alle neun
''' Einstiegspunkte dieselben Fragen beantworten muessen ("wo stehe ich, wie
''' lange noch, wie gut ist es gerade?"); die Aufbereitung fuer den Nutzer
''' ist Sache der GUI, nicht des Kerns.
'''
''' Wird ausschliesslich vom aufrufenden Thread aus gemeldet, nie aus
''' CpSolverSolutionCallback heraus - siehe SolveRunner.</summary>
Public NotInheritable Class SolveProgress
    Public Property Phase As SolvePhase
    ''' <summary>1-basiert.</summary>
    Public Property PhaseIndex As Integer
    ''' <summary>0 = unbekannt. SolveTops Hauptschleife kennt ihre
    ''' Iterationszahl vorab nicht (sie endet an maxSolutions ODER am
    ''' Zeitbudget ODER an der Erschoepfung des Suchraums).</summary>
    Public Property PhaseCount As Integer
    ''' <summary>Sekunden seit Beginn des GESAMTEN Aufrufs, nicht seit Beginn
    ''' dieser Phase.</summary>
    Public Property ElapsedS As Double
    ''' <summary>Gesamtes Zeitbudget des Aufrufs in Sekunden; 0 = unbekannt.
    ''' Zusammen mit ElapsedS die Grundlage fuer einen Fortschrittsbalken.</summary>
    Public Property BudgetS As Double
    ''' <summary>Bereits fertig vorliegende Loesungen/Varianten.</summary>
    Public Property SolutionsFound As Integer
    ''' <summary>Zielwert der aktuell besten Zwischenloesung, oder Nothing,
    ''' solange CP-SAT in dieser Phase noch keine gefunden hat.</summary>
    Public Property IncumbentObjective As Double?
    ''' <summary>CP-SATs bewiesene untere Schranke zum selben Zeitpunkt.
    ''' (IncumbentObjective - BestObjectiveBound) ist die noch offene
    ''' Optimalitaetsluecke - live, nicht erst nach dem Lauf.</summary>
    Public Property BestObjectiveBound As Double?
    ''' <summary>Kurzer deutscher Text fuer die Statuszeile.</summary>
    Public Property Label As String
End Class

''' <summary>Momentaufnahme der Konvergenz-Aufzeichnung, in EINEM Zug unter
''' der Sperre gelesen. Vorher las die Polling-Schleife Points.Count und
''' Points.Last() als zwei getrennte Zugriffe auf eine List(Of T), waehrend
''' der CP-SAT-Thread nebenlaeufig anhaengte - eine Datenrace, die bei einer
''' internen Neuallokation werfen oder einen inkonsistenten Wert liefern
''' konnte. Mit dem Fortschrittskanal steigt die Lesefrequenz, deshalb hier
''' sauber gekapselt.</summary>
Friend NotInheritable Class ConvergenceSnapshot
    Public Property Count As Integer
    Public Property LastElapsedS As Double
    Public Property LastObjective As Double
    Public Property LastBound As Double
End Class

''' <summary>Alles, was RunSolve ueber einen einzelnen Solve-Aufruf wissen
''' muss. Nothing ist ueberall zulaessig und bedeutet "wie bisher".</summary>
Friend NotInheritable Class SolveRunOptions
    Public Property StagnationTimeoutS As Double?
    Public Property Cancellation As CancellationToken
    Public Property Progress As IProgress(Of SolveProgress)
    ''' <summary>Phase/PhaseIndex/PhaseCount/SolutionsFound/BudgetS/Label sind
    ''' vom Aufrufer vorbelegt; RunSolve ergaenzt je Meldung nur ElapsedS und
    ''' die beiden Zielwerte.</summary>
    Public Property Template As SolveProgress
    ''' <summary>Stoppuhr des GESAMTEN Aufrufs (fuer SolveProgress.ElapsedS).</summary>
    Public Property Elapsed As Stopwatch
End Class

''' <summary>Ergebnis eines RunSolve-Aufrufs. Cancelled und
''' StagnationTriggered schliessen einander aus - beides sind vorzeitige
''' Abbrueche, aber nur der erste ist vom Nutzer gewollt.</summary>
Friend NotInheritable Class SolveRunOutcome
    Public Property Status As CpSolverStatus
    Public Property StagnationTriggered As Boolean
    Public Property Cancelled As Boolean
End Class

''' <summary>Der eine Ausfuehrungspfad, ueber den JEDER Solve-Aufruf des Kerns
''' laeuft.</summary>
Friend Module SolveRunner

    ''' <summary>Wie in Phase 2.25 gewaehlt und dort belegt: fein genug fuer
    ''' eine fluessige Anzeige, grob genug, um die Suche nicht zu stoeren.</summary>
    Private Const PollIntervalMs As Integer = 500

    ''' <summary>Fuehrt einen Solve aus und macht ihn abbrechbar und
    ''' beobachtbar. `callback` darf Nothing sein (dann laeuft
    ''' solver.Solve(model) ohne Callback, wie an den Stellen, die vor dieser
    ''' Aenderung gar keinen hatten).
    '''
    ''' WICHTIG - Reihenfolge ist Absicht:
    ''' 1. Bereits gesetztes Token = gar kein Solve. Macht den Abbruch
    '''    kostenlos und Tests deterministisch.
    ''' 2. Ohne Token, ohne Progress, ohne Stagnation: der EXAKT bisherige
    '''    Pfad - direkter, blockierender solver.Solve auf dem aufrufenden
    '''    Thread. Das ist die Absicherung von arc42 8.5 (numWorkers:=1 +
    '''    fester seed = reproduzierbar dieselbe Loesung) und verhindert,
    '''    dass die Benchmark-Laufzeiten sich verschieben.
    ''' 3. Sonst: Task + Polling.</summary>
    Friend Function RunSolve(model As CpModel, solver As CpSolver,
                              callback As ConvergenceCallback,
                              opts As SolveRunOptions) As SolveRunOutcome
        Dim outcome As New SolveRunOutcome()

        If opts IsNot Nothing AndAlso opts.Cancellation.IsCancellationRequested Then
            outcome.Status = CpSolverStatus.Unknown
            outcome.Cancelled = True
            Return outcome
        End If

        Dim needsPolling = opts IsNot Nothing AndAlso
                           (opts.Cancellation.CanBeCanceled OrElse
                            opts.Progress IsNot Nothing OrElse
                            opts.StagnationTimeoutS.HasValue)

        If Not needsPolling Then
            outcome.Status = SolveDirect(model, solver, callback)
            Return outcome
        End If

        Dim solveTask = Task.Run(Function() SolveDirect(model, solver, callback))
        Dim sw = Stopwatch.StartNew()

        ' Sofortmeldung beim Start der Phase. Ohne sie bekaeme ein Aufrufer
        ' fuer jeden Solve, der unter PollIntervalMs fertig ist, ueberhaupt
        ' keine Meldung - die Anzeige bliebe bei kurzen Phasen stumm, und ein
        ' Abbruch aus dem Handler heraus haette keinen Ausloeser.
        Report(opts, SnapshotOf(callback))

        Do
            If solveTask.Wait(PollIntervalMs) Then Exit Do

            If opts.Cancellation.IsCancellationRequested Then
                outcome.Cancelled = True
                solver.StopSearch()
                solveTask.Wait()
                Exit Do
            End If

            Dim snap = SnapshotOf(callback)

            ' Stagnations-Cutoff, unveraendert aus Phase 2.25 uebernommen:
            ' greift nur, wenn ueberhaupt schon eine Loesung existiert - sonst
            ' wuerde ein Szenario, das lange bis zur ERSTEN Loesung braucht,
            ' faelschlich abgeschnitten.
            If opts.StagnationTimeoutS.HasValue AndAlso snap.Count > 0 Then
                Dim idleS = sw.Elapsed.TotalSeconds - snap.LastElapsedS
                If idleS >= opts.StagnationTimeoutS.Value Then
                    outcome.StagnationTriggered = True
                    solver.StopSearch()
                    solveTask.Wait()
                    Exit Do
                End If
            End If

            Report(opts, snap)
        Loop

        ' Abschlussmeldung, damit die Anzeige nicht auf dem vorletzten Tick
        ' stehen bleibt.
        Report(opts, SnapshotOf(callback))

        outcome.Status = solveTask.Result
        Return outcome
    End Function

    Private Function SolveDirect(model As CpModel, solver As CpSolver, callback As ConvergenceCallback) As CpSolverStatus
        If callback Is Nothing Then Return solver.Solve(model)
        Return solver.Solve(model, callback)
    End Function

    Private Function SnapshotOf(callback As ConvergenceCallback) As ConvergenceSnapshot
        If callback Is Nothing Then Return New ConvergenceSnapshot()
        Return callback.Snapshot()
    End Function

    ''' <summary>Meldet den Fortschritt - ausschliesslich von hier, also vom
    ''' aufrufenden Thread aus. Aus OnSolutionCallback heraus zu melden waere
    ''' falsch: der laeuft auf einem CP-SAT-Workerthread INNERHALB des nativen
    ''' SWIG-Aufrufs, und eine Exception aus fremdem Handler-Code wuerde ueber
    ''' die native Grenze propagieren.
    '''
    ''' Auch hier faengt Try/Catch alles ab: ein fehlerhafter GUI-Handler darf
    ''' niemals einen laufenden Solve zum Absturz bringen.</summary>
    Private Sub Report(opts As SolveRunOptions, snap As ConvergenceSnapshot)
        If opts Is Nothing OrElse opts.Progress Is Nothing Then Return
        Dim t = If(opts.Template, New SolveProgress())
        Dim p As New SolveProgress With {
            .Phase = t.Phase,
            .PhaseIndex = t.PhaseIndex,
            .PhaseCount = t.PhaseCount,
            .SolutionsFound = t.SolutionsFound,
            .BudgetS = t.BudgetS,
            .Label = t.Label,
            .ElapsedS = If(opts.Elapsed IsNot Nothing, opts.Elapsed.Elapsed.TotalSeconds, 0.0)
        }
        If snap IsNot Nothing AndAlso snap.Count > 0 Then
            p.IncumbentObjective = snap.LastObjective
            p.BestObjectiveBound = snap.LastBound
        End If
        Try
            opts.Progress.Report(p)
        Catch
            ' Bewusst geschluckt (siehe Doc-Kommentar).
        End Try
    End Sub

    ''' <summary>Baut die Optionen fuer einen Solve-Aufruf. Liefert Nothing,
    ''' wenn nichts davon gebraucht wird - dann nimmt RunSolve den
    ''' unveraenderten Fast-Path.</summary>
    Friend Function Options(stagnationTimeoutS As Double?,
                             ct As CancellationToken,
                             progress As IProgress(Of SolveProgress),
                             template As SolveProgress,
                             elapsed As Stopwatch) As SolveRunOptions
        If Not stagnationTimeoutS.HasValue AndAlso Not ct.CanBeCanceled AndAlso progress Is Nothing Then Return Nothing
        Return New SolveRunOptions With {
            .StagnationTimeoutS = stagnationTimeoutS,
            .Cancellation = ct,
            .Progress = progress,
            .Template = template,
            .Elapsed = elapsed
        }
    End Function

    ''' <summary>Kurzform fuer die einstufigen Einstiegspunkte (ein Solve,
    ''' keine Schleife): baut Optionen und Template in einem Zug.</summary>
    Friend Function SingleStage(phase As SolvePhase, label As String, budgetS As Double,
                                 ct As CancellationToken,
                                 progress As IProgress(Of SolveProgress),
                                 elapsed As Stopwatch) As SolveRunOptions
        Return Options(Nothing, ct, progress,
                       New SolveProgress With {.Phase = phase, .PhaseIndex = 1, .PhaseCount = 1,
                                               .Label = label, .BudgetS = budgetS},
                       elapsed)
    End Function

End Module

''' <summary>Etikettiert die Meldungen eines GESCHACHTELTEN Aufrufs um.
'''
''' SolveKursstufe und SolveCombinedSchool rechnen nicht selbst, sondern
''' verketten mehrere Einstiegspunkte. Ohne Adapter meldete jeder innere
''' Aufruf seine eigene Phase (Iteration 1 von 1) und - schlimmer - seine
''' eigene, bei jeder Stufe wieder bei 0 startende Laufzeit. Der Adapter
''' ersetzt beides durch die Sicht des Gesamtlaufs ("Stufe 2 von 3", Uhr
''' laeuft durch) und reicht nur die Zielwerte unveraendert durch.</summary>
Friend NotInheritable Class StageProgressAdapter
    Implements IProgress(Of SolveProgress)

    Private ReadOnly _inner As IProgress(Of SolveProgress)
    Private ReadOnly _phase As SolvePhase
    Private ReadOnly _index As Integer
    Private ReadOnly _count As Integer
    Private ReadOnly _label As String
    Private ReadOnly _elapsed As Stopwatch
    Private ReadOnly _budgetS As Double

    Public Sub New(inner As IProgress(Of SolveProgress), phase As SolvePhase,
                    index As Integer, count As Integer, label As String,
                    elapsed As Stopwatch, budgetS As Double)
        _inner = inner
        _phase = phase
        _index = index
        _count = count
        _label = label
        _elapsed = elapsed
        _budgetS = budgetS
    End Sub

    ''' <summary>Liefert Nothing, wenn es nichts weiterzureichen gibt - so
    ''' bleibt der Fast-Path in den inneren Aufrufen erhalten.</summary>
    Friend Shared Function Wrap(inner As IProgress(Of SolveProgress), phase As SolvePhase,
                                 index As Integer, count As Integer, label As String,
                                 elapsed As Stopwatch, budgetS As Double) As IProgress(Of SolveProgress)
        If inner Is Nothing Then Return Nothing
        Return New StageProgressAdapter(inner, phase, index, count, label, elapsed, budgetS)
    End Function

    Public Sub Report(value As SolveProgress) Implements IProgress(Of SolveProgress).Report
        If value Is Nothing Then Return
        _inner.Report(New SolveProgress With {
            .Phase = _phase,
            .PhaseIndex = _index,
            .PhaseCount = _count,
            .Label = _label,
            .ElapsedS = If(_elapsed IsNot Nothing, _elapsed.Elapsed.TotalSeconds, value.ElapsedS),
            .BudgetS = _budgetS,
            .SolutionsFound = value.SolutionsFound,
            .IncumbentObjective = value.IncumbentObjective,
            .BestObjectiveBound = value.BestObjectiveBound
        })
    End Sub
End Class
