' Die Stundenplan-Pipeline als wiederverwendbarer Dienst (Stufe B des
' GUI-Unterbaus, docs/gui-implementierungsplan.md).
'
' Bis hierher lebte diese Orchestrierung ausschliesslich in
' SchoolTestRunner/Run.vb - einer 280-Zeilen-Prozedur, die Dateipfade
' entgegennahm, acht Dateien schrieb und sieben Konsolenzeilen ausgab. Die
' Phase-3-GUI braucht dieselbe Abfolge, aber ohne Dateisystem und ohne
' Konsole. Sie hier zu duplizieren waere der sichere Weg in zwei
' auseinanderdriftende Pipelines gewesen; stattdessen ist DIES die eine
' Implementierung und die CLI ihr erster Konsument.
'
' Bewusst erhalten geblieben sind die vier Stellen, an denen echte
' Entscheidungen stecken - sie sind der Grund, warum es diesen Dienst
' ueberhaupt gibt statt einer Handvoll Kernaufrufe im UI-Code:
'   * Aufloesung der elf Nullable-Config-Felder auf SolveTops EIGENE
'     Defaults (Nothing heisst "nimm den Default", nicht "schalte ab"),
'   * Aufteilung von Zeitbudget und max_solutions auf mehrere Zuteilungen,
'   * Auswahl des besten Laufs ueber Quality.Total,
'   * DeepClone je Zuteilung (ein JsonNode darf nur EINEN Parent haben).
'
' Neu gegenueber Run.vb: Abbruch und Fortschritt werden durchgereicht
' (arc42 8.11). Run.vb hat die Parameter nie uebergeben - fuer die CLI
' egal, fuer eine bedienbare GUI der ganze Punkt.
Imports System.Text.Json.Nodes
Imports System.Threading
Imports Google.OrTools.Sat
Imports TimetableCore
Imports TimetableYaml

''' <summary>Wie weit die Pipeline gekommen ist. Bei Erfolg steht sie auf
''' Fertig, sonst auf der Stufe, die den Abbruch verursacht hat.</summary>
Public Enum LaufStufe
    Stammdatenpruefung
    Lehrereinsatz
    Lehrereinsatzpruefung
    Szenarienaufbau
    Stundenplan
    Fertig
End Enum

''' <summary>Alles, was CLI und GUI nach einem Lauf brauchen - Ergebnisse
''' UND Diagnose. Bewusst ein Datenbehaelter ohne Verhalten: die
''' Aufbereitung zu Markdown/JSON/HTML liegt in StundenplanBericht, das
''' Schreiben beim Aufrufer.</summary>
Public NotInheritable Class LaufErgebnis
    Public Property Stufe As LaufStufe
    ''' <summary>True nur bei vollstaendigem Erfolg: alle Stufen
    ''' Optimal/Feasible UND null Verstoesse. Entspricht exakt dem
    ''' bisherigen Rueckgabewert von Run.RunOne.</summary>
    Public Property Erfolgreich As Boolean
    ''' <summary>Abbruch durch den Aufrufer (arc42 8.11). Die bis dahin
    ''' erreichten Teilergebnisse bleiben erhalten.</summary>
    Public Property Abgebrochen As Boolean
    ''' <summary>Klartextmeldungen der gescheiterten Stufe (Validierungs-
    ''' fehler, Verstoesse). Leer, wenn nichts zu melden war.</summary>
    Public Property Meldungen As New List(Of String)
    ''' <summary>Nur gesetzt, wenn Szenarienaufbau an einer bestimmten
    ''' Zuteilung scheiterte - 1-basiert.</summary>
    Public Property FehlerZuteilung As Integer?

    Public Property Bestand As Stammdatenbestand
    Public Property Config As RunConfig
    Public Property AequivalenzKlassen As New List(Of List(Of String))
    Public Property Einsaetze As New List(Of LehrereinsatzResult)
    ''' <summary>Je Zuteilung ein Markdown-Block inkl. etwaiger
    ''' Verifier-Verstoesse - vorbereitet, weil nur hier bekannt ist,
    ''' welche Zuteilung welchen Block ergab.</summary>
    Public Property LehrereinsatzBloecke As New List(Of String)
    Public Property LehrereinsatzVerstoesse As Integer
    Public Property Laeufe As New List(Of Formatting.AssignmentRun)
    Public Property BesterLauf As Formatting.AssignmentRun
    Public Property PlanVerstoesse As New List(Of String)

    ''' <summary>Aufgeloeste Parameter - die Berichte zitieren sie, und die
    ''' GUI zeigt sie im Lauf-Monitor.</summary>
    Public Property Gewichte As QualityWeights
    Public Property PerZuteilungBudgetS As Double
    Public Property PerZuteilungMaxLoesungen As Integer
    Public Property PerSolveLimitS As Double

    ''' <summary>Bequemer Zugriff auf die global beste Loesung; Nothing,
    ''' solange keine existiert.</summary>
    Public ReadOnly Property BesteLoesung As ScoredSolution
        Get
            If BesterLauf Is Nothing OrElse BesterLauf.Result.Solutions.Count = 0 Then Return Nothing
            Return BesterLauf.Result.Solutions(0)
        End Get
    End Property
End Class

Public Module StundenplanLauf

    ''' <summary>Fuehrt die komplette Pipeline aus: StammdatenValidation ->
    ''' Lehrereinsatzplanung -> VerifyLehrereinsatz ->
    ''' BuildEntitiesFragment/BuildAssignmentConstraints ->
    ''' ValidateEntities -> SolveTop je Zuteilung -> VerifySchedule.
    '''
    ''' Kein Dateizugriff, keine Konsolenausgabe. `handConstraints` sind die
    ''' handverfassten Regeln (aus constraints.yaml oder aus den
    ''' GUI-Regelmasken) - die generierten Regeln entstehen hier drinnen und
    ''' duerfen von aussen nie mitgegeben werden.</summary>
    Public Function Ausfuehren(bestand As Stammdatenbestand,
                                handConstraints As List(Of JsonObject),
                                cfg As RunConfig,
                                Optional cancellationToken As CancellationToken = Nothing,
                                Optional progress As IProgress(Of SolveProgress) = Nothing) As LaufErgebnis
        Dim e As New LaufErgebnis With {.Bestand = bestand, .Config = cfg, .Stufe = LaufStufe.Stammdatenpruefung}
        If handConstraints Is Nothing Then handConstraints = New List(Of JsonObject)

        ' --- Stufe 1: Stammdaten -------------------------------------
        Melde(progress, 1, "Stammdaten werden geprueft", cfg)
        If Abgebrochen(cancellationToken, e) Then Return e

        Dim stammdatenErrors = StammdatenValidation.ValidateStammdaten(bestand)
        If stammdatenErrors.Count > 0 Then
            e.Meldungen = stammdatenErrors
            Return e
        End If

        ' --- Stufe 2: Lehrereinsatz ----------------------------------
        e.Stufe = LaufStufe.Lehrereinsatz
        Melde(progress, 2, "Lehrereinsatz wird geplant", cfg)
        If Abgebrochen(cancellationToken, e) Then Return e

        ' Mehr-Zuteilungs-Modus: Aequivalenzklassen austauschbarer
        ' Lehrkraefte (fuer Symmetriebrechung, invariante Diversitaet und
        ' die "direkt tauschbar"-Anzeige im Viewer) + bis zu
        ' max_assignments Zuteilungen als je eigener Stufe-2-Input.
        e.AequivalenzKlassen = Lehrereinsatzplanung.TeacherEquivalenceClasses(bestand, handConstraints)
        Dim maxAssignments = Math.Max(If(cfg.MaxAssignments, 1), 1)
        Dim symBreak = If(cfg.AssignmentSymmetryBreaking, maxAssignments > 1)
        e.Einsaetze = Lehrereinsatzplanung.SolveLehrereinsatzTop(
            bestand, deputatToleranzStunden:=cfg.DeputatToleranzStunden, timeLimitS:=cfg.LehrereinsatzTimeLimitS,
            seed:=cfg.Seed, numWorkers:=cfg.NumWorkers,
            maxAssignments:=maxAssignments,
            assignmentTolerance:=If(cfg.AssignmentTolerance, 0),
            assignmentMinDiversity:=If(cfg.AssignmentMinDiversity, 1),
            aequivalenzKlassen:=If(symBreak, e.AequivalenzKlassen, Nothing),
            cancellationToken:=cancellationToken,
            progress:=StufenFortschritt(progress, 2, "Lehrereinsatz wird geplant", cfg))

        ' Seit dem Abbruchkanal kann diese Liste LEER sein - vor Stufe A
        ' griff Run.vb hier unbesehen auf einsaetze(0) zu.
        If e.Einsaetze.Count = 0 Then
            e.Abgebrochen = True
            Return e
        End If
        If e.Einsaetze(0).Cancelled Then e.Abgebrochen = True

        Dim lehrereinsatz = e.Einsaetze(0)
        If lehrereinsatz.Status <> CpSolverStatus.Optimal AndAlso lehrereinsatz.Status <> CpSolverStatus.Feasible Then
            Return e
        End If

        ' --- Stufe 3: unabhaengige Nachpruefung ----------------------
        e.Stufe = LaufStufe.Lehrereinsatzpruefung
        Melde(progress, 3, "Lehrereinsatz wird nachgeprueft", cfg)

        For i = 0 To e.Einsaetze.Count - 1
            Dim part = Formatting.FormatLehrereinsatzMarkdown(bestand, e.Einsaetze(i))
            If e.Einsaetze.Count > 1 Then
                part = $"# Zuteilung {i + 1} von {e.Einsaetze.Count} (Lehrereinsatz-Objective {e.Einsaetze(i).Solver.ObjectiveValue})" & vbLf & vbLf & part
            End If
            Dim violations = Verifier.VerifyLehrereinsatz(bestand, e.Einsaetze(i))
            If violations.Count > 0 Then
                part &= vbLf & vbLf & "## Verstoesse (Verifier.VerifyLehrereinsatz)" & vbLf & vbLf &
                    String.Join(vbLf, violations.Select(Function(v) $"- {v}"))
                e.LehrereinsatzVerstoesse += violations.Count
            End If
            e.LehrereinsatzBloecke.Add(part)
        Next
        Dim tauschbare = e.AequivalenzKlassen.Where(Function(k) k.Count >= 2).ToList()
        If tauschbare.Count > 0 Then
            e.LehrereinsatzBloecke.Add("## Aequivalente (direkt tauschbare) Lehrkraefte" & vbLf & vbLf &
                "Diese Lehrkraefte sind fuer die GESAMTE Pipeline ununterscheidbar (identische Qualifikationen, Deputate, Verfuegbarkeiten und Constraint-Erwaehnungen) - innerhalb einer Gruppe koennen sie ohne jede Auswirkung auf die Plan-Qualitaet direkt getauscht werden:" & vbLf & vbLf &
                String.Join(vbLf, tauschbare.Select(Function(k) $"- {String.Join(" <-> ", k)}")))
        End If

        If e.LehrereinsatzVerstoesse > 0 Then Return e
        If e.Abgebrochen Then Return e

        ' --- Stufe 4: Szenarien je Zuteilung -------------------------
        e.Stufe = LaufStufe.Szenarienaufbau
        If Abgebrochen(cancellationToken, e) Then Return e

        Dim ent = Stammdaten.BuildEntitiesFragment(bestand)
        Dim dataOfEinsatz As New List(Of JsonObject)
        For i = 0 To e.Einsaetze.Count - 1
            Dim derivedConstraints = Lehrereinsatzplanung.BuildAssignmentConstraints(e.Einsaetze(i), bestand)
            Dim alleConstraints = derivedConstraints.Concat(handConstraints).ToList()
            ' DeepClone: die handConstraints-Knoten wuerden sonst beim
            ' zweiten Datenobjekt erneut angehaengt ("node already has a
            ' parent") - jede Zuteilung bekommt ihre eigene Kopie.
            Dim dataI As New JsonObject From {
                {"entities", ent.DeepClone().AsObject()},
                {"constraints", New JsonArray(alleConstraints.Select(Function(c) CType(c.DeepClone(), JsonNode)).ToArray())}
            }
            Dim validationErrors = Validation.ValidateEntities(dataI)
            If validationErrors.Count > 0 Then
                e.Meldungen = validationErrors
                e.FehlerZuteilung = i + 1
                Return e
            End If
            dataOfEinsatz.Add(dataI)
        Next

        ' --- Stufe 5: Stundenplan ------------------------------------
        e.Stufe = LaufStufe.Stundenplan
        If Abgebrochen(cancellationToken, e) Then Return e

        ' Nullable-Aufloesung VOR dem Aufruf: Nothing muss SolveTops eigene
        ' Defaults reproduzieren (45s/True/True, lexicographic=True per
        ' Nutzerentscheidung), nicht das jeweilige Verhalten stillschweigend
        ' abschalten. RelativeGapLimit wird durchgereicht - dort bedeutet
        ' Nothing auf beiden Seiten dasselbe.
        e.PerSolveLimitS = If(cfg.PerSolveTimeLimitS, cfg.SolveTimeLimitS)
        e.Gewichte = YamlConfig.BuildQualityWeights(cfg.QualityWeights)
        Dim stagnationTimeoutS = If(cfg.StagnationTimeoutS.HasValue, cfg.StagnationTimeoutS, New Double?(45.0))
        Dim diversifySeed = If(cfg.DiversifySeed, True)
        Dim randomizeSearch = If(cfg.RandomizeSearch, True)
        Dim lexicographic = If(cfg.Lexicographic, True)
        Dim lexTolerance = If(cfg.LexTolerance, 0)
        Dim lexTeacherGapsStage = If(cfg.LexTeacherGapsStage, False)
        Dim lexOccupiedDensityStage = If(cfg.LexOccupiedDensityStage, False)
        Dim lexSubjectWindowStage = If(cfg.LexSubjectWindowStage, False)
        Dim minDiversity = If(cfg.MinDiversity, 0)
        Dim rehintFoundSolutions = If(cfg.RehintFoundSolutions, True)
        Dim stage1TimeLimitS = If(cfg.Stage1TimeLimitS, 60.0)

        ' Mehr-Zuteilungs-Modus: ein Stufe-2-Lauf PRO Zuteilung;
        ' Gesamtbudget und max_solutions werden gleichmaessig aufgeteilt
        ' (Ein-Zuteilungs-Modus: identisch zum bisherigen Verhalten).
        e.PerZuteilungBudgetS = cfg.SolveTimeLimitS / e.Einsaetze.Count
        e.PerZuteilungMaxLoesungen = Math.Max(1, (cfg.MaxSolutions + e.Einsaetze.Count - 1) \ e.Einsaetze.Count)

        For i = 0 To e.Einsaetze.Count - 1
            If cancellationToken.IsCancellationRequested Then
                e.Abgebrochen = True
                Exit For
            End If
            Dim label = If(e.Einsaetze.Count > 1,
                           $"Stundenplan wird gerechnet (Zuteilung {i + 1} von {e.Einsaetze.Count})",
                           "Stundenplan wird gerechnet")
            Dim topResultI = Solver.SolveTop(dataOfEinsatz(i),
                maxSolutions:=e.PerZuteilungMaxLoesungen, totalTimeLimitS:=e.PerZuteilungBudgetS,
                perSolveTimeLimitS:=e.PerSolveLimitS, seed:=cfg.Seed, numWorkers:=cfg.NumWorkers,
                qualityWeights:=e.Gewichte,
                stage1TimeLimitS:=stage1TimeLimitS,
                stagnationTimeoutS:=stagnationTimeoutS, diversifySeed:=diversifySeed, randomizeSearch:=randomizeSearch,
                relativeGapLimit:=cfg.RelativeGapLimit,
                lexicographic:=lexicographic, lexTolerance:=lexTolerance, lexTeacherGapsStage:=lexTeacherGapsStage,
                lexOccupiedDensityStage:=lexOccupiedDensityStage,
                lexSubjectWindowStage:=lexSubjectWindowStage,
                minDiversity:=minDiversity, rehintFoundSolutions:=rehintFoundSolutions,
                laterIterationsGapLimit:=cfg.LaterIterationsGapLimit,
                cancellationToken:=cancellationToken,
                progress:=StufenFortschritt(progress, 5, label, cfg))
            If topResultI.StopReason = MultiSolveStopReason.Cancelled Then e.Abgebrochen = True
            e.Laeufe.Add(New Formatting.AssignmentRun With {
                .Data = dataOfEinsatz(i), .Result = topResultI, .AssignmentIndex = i + 1,
                .LehrereinsatzObjective = e.Einsaetze(i).Solver.ObjectiveValue})
        Next

        Dim successfulRuns = e.Laeufe.Where(Function(r) r.Result.Solutions.Count > 0).ToList()
        If successfulRuns.Count = 0 Then Return e

        ' Der Lauf mit der global besten Loesung liefert auch data/topResult
        ' fuer den Bericht.
        e.BesterLauf = successfulRuns.OrderBy(Function(r) r.Result.Solutions(0).Quality.Total).First()
        e.PlanVerstoesse = Verifier.VerifySchedule(e.BesterLauf.Data, e.BesterLauf.Result.Solutions(0).Schedule)

        e.Stufe = LaufStufe.Fertig
        e.Erfolgreich = e.PlanVerstoesse.Count = 0 AndAlso Not e.Abgebrochen
        Return e
    End Function

    Private Function Abgebrochen(ct As CancellationToken, e As LaufErgebnis) As Boolean
        If Not ct.IsCancellationRequested Then Return False
        e.Abgebrochen = True
        Return True
    End Function

    ''' <summary>Die Stufen ohne Solverlauf (Validierung, Nachpruefung)
    ''' melden sich einmalig, damit die Schrittanzeige des Lauf-Monitors
    ''' (gui-ui-konzept.md 6.13) auch dort weiterlaeuft.</summary>
    Private Sub Melde(progress As IProgress(Of SolveProgress), stufe As Integer, label As String, cfg As RunConfig)
        If progress Is Nothing Then Return
        Try
            progress.Report(New SolveProgress With {
                .Phase = SolvePhase.Stufe, .PhaseIndex = stufe, .PhaseCount = StufenGesamt,
                .Label = label, .BudgetS = GesamtBudgetS(cfg)})
        Catch
            ' Ein fehlerhafter Handler darf keinen Lauf abbrechen - gleiche
            ' Disziplin wie in SolveRunner (arc42 8.11).
        End Try
    End Sub

    Private Const StufenGesamt As Integer = 5

    Private Function GesamtBudgetS(cfg As RunConfig) As Double
        Return cfg.LehrereinsatzTimeLimitS + cfg.SolveTimeLimitS
    End Function

    Private Function StufenFortschritt(progress As IProgress(Of SolveProgress), stufe As Integer,
                                        label As String, cfg As RunConfig) As IProgress(Of SolveProgress)
        If progress Is Nothing Then Return Nothing
        Return New PipelineFortschritt(progress, stufe, StufenGesamt, label, GesamtBudgetS(cfg))
    End Function

End Module

''' <summary>Etikettiert die Meldungen der Kernaufrufe auf die Stufen der
''' PIPELINE um. Ohne das meldete jeder Kernaufruf seine eigene Sicht
''' ("Iteration 3", "Zuteilung 1 von 2") und der Lauf-Monitor koennte die
''' im Konzept verlangte Schrittfolge "Validierung -> Lehrereinsatz ->
''' Verifikation -> Stundenplan" gar nicht zeigen.
'''
''' Bewusst hier statt in TimetableCore: der dortige StageProgressAdapter
''' ist Friend, und die Stufen einer Schul-Pipeline sind kein Begriff, den
''' der Solver-Kern kennen muss.</summary>
Friend NotInheritable Class PipelineFortschritt
    Implements IProgress(Of SolveProgress)

    Private ReadOnly _inner As IProgress(Of SolveProgress)
    Private ReadOnly _stufe As Integer
    Private ReadOnly _stufenGesamt As Integer
    Private ReadOnly _label As String
    Private ReadOnly _budgetS As Double

    Public Sub New(inner As IProgress(Of SolveProgress), stufe As Integer, stufenGesamt As Integer,
                    label As String, budgetS As Double)
        _inner = inner
        _stufe = stufe
        _stufenGesamt = stufenGesamt
        _label = label
        _budgetS = budgetS
    End Sub

    Public Sub Report(value As SolveProgress) Implements IProgress(Of SolveProgress).Report
        If value Is Nothing Then Return
        _inner.Report(New SolveProgress With {
            .Phase = SolvePhase.Stufe,
            .PhaseIndex = _stufe,
            .PhaseCount = _stufenGesamt,
            .Label = _label,
            .ElapsedS = value.ElapsedS,
            .BudgetS = _budgetS,
            .SolutionsFound = value.SolutionsFound,
            .IncumbentObjective = value.IncumbentObjective,
            .BestObjectiveBound = value.BestObjectiveBound
        })
    End Sub
End Class
