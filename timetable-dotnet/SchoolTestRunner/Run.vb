' Phase 2.18f: `run`-Subcommand - laedt tests/<schule>/input/{stammdaten,
' constraints[,config]}.yaml, durchlaeuft die komplette Pipeline
' (StammdatenValidation -> Lehrereinsatzplanung -> BuildAssignmentConstraints
' + Handconstraints -> Validation.ValidateEntities -> Solver.Solve ->
' Verifier.VerifySchedule) und schreibt nach jeder erreichten Stufe den
' bisherigen Fortschritt in tests/<schule>/output/{lehrerzuteilung,
' stundenplan}.md - kein Alles-oder-Nichts, ein Abbruch in einer spaeten
' Stufe loescht nicht die vorherigen Ergebnisse.
Imports TimetableCore
Imports Google.OrTools.Sat
Imports System.Text.Json
Imports System.Text.Json.Nodes

Public Module Run

    Private Function ClassCellText(cell As GridCell) As String
        If cell Is Nothing Then Return "-"
        Dim text = $"{cell.Subject} ({cell.Teacher})"
        If Not String.IsNullOrEmpty(cell.Room) Then text &= $" [{cell.Room}]"
        Return text
    End Function

    Private Function TeacherCellText(cell As GridCell) As String
        If cell Is Nothing Then Return "-"
        Dim text = $"{cell.ClassName} {cell.Subject}"
        If Not String.IsNullOrEmpty(cell.Room) Then text &= $" [{cell.Room}]"
        Return text
    End Function

    ''' <summary>Fuehrt die Pipeline fuer genau eine Schule aus. Liefert
    ''' True bei vollstaendigem Erfolg (alle Stufen Optimal/Feasible, 0
    ''' Verstoesse), False sonst - der Aufrufer setzt daraus den
    ''' Exitcode.</summary>
    Public Function RunOne(testsRoot As String, schule As String) As Boolean
        Dim inputDir = IO.Path.Combine(testsRoot, schule, "input")
        Dim outputDir = IO.Path.Combine(testsRoot, schule, "output")
        Dim stammdatenPath = IO.Path.Combine(inputDir, "stammdaten.yaml")

        If Not IO.File.Exists(stammdatenPath) Then
            Console.WriteLine($"[{schule}] FAIL - {stammdatenPath} nicht gefunden")
            Return False
        End If
        IO.Directory.CreateDirectory(outputDir)

        Dim bestand = YamlStammdaten.LoadStammdatenYaml(stammdatenPath)
        Dim handConstraints = YamlConstraints.LoadConstraintsYaml(IO.Path.Combine(inputDir, "constraints.yaml"))
        Dim cfg = LoadConfig(IO.Path.Combine(inputDir, "config.yaml"))

        Dim lehrerzuteilungLines As New List(Of String)
        Dim stundenplanLines As New List(Of String)
        Dim erfolg = True

        Dim stammdatenErrors = StammdatenValidation.ValidateStammdaten(bestand)
        If stammdatenErrors.Count > 0 Then
            lehrerzuteilungLines.Add($"# Lehrerzuteilung: {bestand.SchulName}")
            lehrerzuteilungLines.Add("")
            lehrerzuteilungLines.Add("**Status:** StammdatenValidation FEHLGESCHLAGEN")
            lehrerzuteilungLines.Add("")
            For Each e In stammdatenErrors : lehrerzuteilungLines.Add($"- {e}") : Next
            IO.File.WriteAllText(IO.Path.Combine(outputDir, "lehrerzuteilung.md"), String.Join(vbLf, lehrerzuteilungLines))
            Console.WriteLine($"[{schule}] FAIL - StammdatenValidation: {stammdatenErrors.Count} Fehler")
            Return False
        End If

        ' Mehr-Zuteilungs-Modus: Aequivalenzklassen austauschbarer
        ' Lehrkraefte (fuer Symmetriebrechung, invariante Diversitaet und
        ' die "direkt tauschbar"-Anzeige im Viewer) + bis zu
        ' max_assignments Zuteilungen als je eigener Stufe-2-Input.
        Dim eqKlassen = Lehrereinsatzplanung.TeacherEquivalenceClasses(bestand, handConstraints)
        Dim maxAssignments = Math.Max(If(cfg.MaxAssignments, 1), 1)
        Dim symBreak = If(cfg.AssignmentSymmetryBreaking, maxAssignments > 1)
        Dim einsaetze = Lehrereinsatzplanung.SolveLehrereinsatzTop(
            bestand, deputatToleranzStunden:=cfg.DeputatToleranzStunden, timeLimitS:=cfg.LehrereinsatzTimeLimitS,
            seed:=cfg.Seed, numWorkers:=cfg.NumWorkers,
            maxAssignments:=maxAssignments,
            assignmentTolerance:=If(cfg.AssignmentTolerance, 0),
            assignmentMinDiversity:=If(cfg.AssignmentMinDiversity, 1),
            aequivalenzKlassen:=If(symBreak, eqKlassen, Nothing))
        Dim lehrereinsatz = einsaetze(0)
        Dim lehrereinsatzOk = lehrereinsatz.Status = CpSolverStatus.Optimal OrElse lehrereinsatz.Status = CpSolverStatus.Feasible

        If Not lehrereinsatzOk Then
            IO.File.WriteAllText(IO.Path.Combine(outputDir, "lehrerzuteilung.md"),
                $"# Lehrerzuteilung: {bestand.SchulName}{vbLf}{vbLf}**Status:** {lehrereinsatz.Status} (Lehrereinsatzplanung nicht loesbar){vbLf}")
            Console.WriteLine($"[{schule}] FAIL - Lehrereinsatzplanung: {lehrereinsatz.Status}")
            Return False
        End If

        Dim lehrereinsatzViolationsTotal = 0
        Dim mdParts As New List(Of String)
        For i = 0 To einsaetze.Count - 1
            Dim part = Formatting.FormatLehrereinsatzMarkdown(bestand, einsaetze(i))
            If einsaetze.Count > 1 Then
                part = $"# Zuteilung {i + 1} von {einsaetze.Count} (Lehrereinsatz-Objective {einsaetze(i).Solver.ObjectiveValue})" & vbLf & vbLf & part
            End If
            Dim violations = Verifier.VerifyLehrereinsatz(bestand, einsaetze(i))
            If violations.Count > 0 Then
                part &= vbLf & vbLf & "## Verstoesse (Verifier.VerifyLehrereinsatz)" & vbLf & vbLf &
                    String.Join(vbLf, violations.Select(Function(v) $"- {v}"))
                lehrereinsatzViolationsTotal += violations.Count
                erfolg = False
            End If
            mdParts.Add(part)
        Next
        Dim tauschbare = eqKlassen.Where(Function(k) k.Count >= 2).ToList()
        If tauschbare.Count > 0 Then
            mdParts.Add("## Aequivalente (direkt tauschbare) Lehrkraefte" & vbLf & vbLf &
                "Diese Lehrkraefte sind fuer die GESAMTE Pipeline ununterscheidbar (identische Qualifikationen, Deputate, Verfuegbarkeiten und Constraint-Erwaehnungen) - innerhalb einer Gruppe koennen sie ohne jede Auswirkung auf die Plan-Qualitaet direkt getauscht werden:" & vbLf & vbLf &
                String.Join(vbLf, tauschbare.Select(Function(k) $"- {String.Join(" <-> ", k)}")))
        End If
        IO.File.WriteAllText(IO.Path.Combine(outputDir, "lehrerzuteilung.md"), String.Join(vbLf & vbLf & "---" & vbLf & vbLf, mdParts))

        If lehrereinsatzViolationsTotal > 0 Then
            Console.WriteLine($"[{schule}] FAIL - VerifyLehrereinsatz: {lehrereinsatzViolationsTotal} Verstoesse")
            Return False
        End If

        Dim ent = Stammdaten.BuildEntitiesFragment(bestand)
        Dim dataOfEinsatz As New List(Of JsonObject)
        For i = 0 To einsaetze.Count - 1
            Dim derivedConstraints = Lehrereinsatzplanung.BuildAssignmentConstraints(einsaetze(i), bestand)
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
                IO.File.WriteAllText(IO.Path.Combine(outputDir, "stundenplan.md"),
                    $"# Stundenplan: {bestand.SchulName}{vbLf}{vbLf}**Status:** Validation.ValidateEntities FEHLGESCHLAGEN (Zuteilung {i + 1}){vbLf}{vbLf}" &
                    String.Join(vbLf, validationErrors.Select(Function(e) $"- {e}")))
                Console.WriteLine($"[{schule}] FAIL - Validation.ValidateEntities (Zuteilung {i + 1}): {validationErrors.Count} Fehler")
                Return False
            End If
            dataOfEinsatz.Add(dataI)
        Next

        ' Nutzt SolveTop statt Solve: dieselbe volle Qualitaets-Zielfunktion
        ' (Luecken/Randstunden/Tagesausgewogenheit, siehe ScheduleQuality.vb)
        ' fliesst direkt ins CP-SAT-Modell ein, statt erst nachtraeglich nur
        ' zum Sortieren einer Kann-only-Loesung benutzt zu werden - der Solver
        ' sucht dadurch von vornherein nach einem bzgl. dieser Kriterien
        ' moeglichst guten statt nur irgendeinem zulaessigen Plan.
        ' maxSolutions:=cfg.MaxSolutions (Phase 2.21, Default weiterhin 1):
        ' der beste Kandidat (Solutions(0)) bleibt fuer lehrerzuteilung.md/
        ' stundenplan.md massgeblich, ALLE gefundenen Kandidaten werden
        ' zusaetzlich in output/stundenplan.json + output/stundentafel.html
        ' als vergleichbare Alternativen exportiert (siehe unten).
        Dim perSolveLimit = If(cfg.PerSolveTimeLimitS, cfg.SolveTimeLimitS)
        Dim qualityWeights = BuildQualityWeights(cfg.QualityWeights)
        ' Phase 2.25: resolves each nullable config field to a concrete
        ' value BEFORE the call (same pattern as perSolveLimit above) -
        ' cfg.StagnationTimeoutS/DiversifySeed/RandomizeSearch = Nothing
        ' (no override in config.yaml) must reproduce SolveTop's OWN
        ' defaults (45.0s/True/True), not silently disable them.
        ' RelativeGapLimit passes through as-is - Nothing means the same
        ' thing (no gap-limit override) on both sides.
        Dim stagnationTimeoutS = If(cfg.StagnationTimeoutS.HasValue, cfg.StagnationTimeoutS, New Double?(45.0))
        Dim diversifySeed = If(cfg.DiversifySeed, True)
        Dim randomizeSearch = If(cfg.RandomizeSearch, True)
        ' Code-Review-Umsetzung (P2/P3): Nothing loest jeweils zu SolveTops
        ' eigenem Default auf (lexicographic=True per Nutzerentscheidung,
        ' lex_tolerance=0, lex_teacher_gaps_stage=False, min_diversity=0,
        ' rehint_found_solutions=True).
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
        Dim perAssignmentBudget = cfg.SolveTimeLimitS / einsaetze.Count
        Dim perAssignmentMaxSolutions = Math.Max(1, (cfg.MaxSolutions + einsaetze.Count - 1) \ einsaetze.Count)
        Dim runs As New List(Of Formatting.AssignmentRun)
        For i = 0 To einsaetze.Count - 1
            Dim topResultI = Solver.SolveTop(dataOfEinsatz(i), maxSolutions:=perAssignmentMaxSolutions, totalTimeLimitS:=perAssignmentBudget,
                perSolveTimeLimitS:=perSolveLimit, seed:=cfg.Seed, numWorkers:=cfg.NumWorkers, qualityWeights:=qualityWeights,
                stage1TimeLimitS:=stage1TimeLimitS,
                stagnationTimeoutS:=stagnationTimeoutS, diversifySeed:=diversifySeed, randomizeSearch:=randomizeSearch,
                relativeGapLimit:=cfg.RelativeGapLimit,
                lexicographic:=lexicographic, lexTolerance:=lexTolerance, lexTeacherGapsStage:=lexTeacherGapsStage,
                lexOccupiedDensityStage:=lexOccupiedDensityStage,
                lexSubjectWindowStage:=lexSubjectWindowStage,
                minDiversity:=minDiversity, rehintFoundSolutions:=rehintFoundSolutions,
                laterIterationsGapLimit:=cfg.LaterIterationsGapLimit)
            runs.Add(New Formatting.AssignmentRun With {
                .Data = dataOfEinsatz(i), .Result = topResultI, .AssignmentIndex = i + 1,
                .LehrereinsatzObjective = einsaetze(i).Solver.ObjectiveValue})
        Next
        Dim successfulRuns = runs.Where(Function(r) r.Result.Solutions.Count > 0).ToList()
        If successfulRuns.Count = 0 Then
            Dim reasons = String.Join("|", runs.Select(Function(r) r.Result.StopReason.ToString()).Distinct())
            IO.File.WriteAllText(IO.Path.Combine(outputDir, "stundenplan.md"),
                $"# Stundenplan: {bestand.SchulName}{vbLf}{vbLf}**Status:** {reasons} (Solver.SolveTop fand keine Loesung){vbLf}")
            Console.WriteLine($"[{schule}] FAIL - Solver.SolveTop: {reasons}")
            Return False
        End If

        ' stundenplan.md dokumentiert die global beste Loesung - der Lauf,
        ' der sie enthaelt, liefert auch data/topResult fuer die
        ' nachfolgenden (unveraenderten) Markdown-Abschnitte.
        Dim bestRun = successfulRuns.OrderBy(Function(r) r.Result.Solutions(0).Quality.Total).First()
        Dim data = bestRun.Data
        Dim topResult = bestRun.Result
        Dim best = topResult.Solutions(0)
        Dim schedule = best.Schedule
        Dim scheduleViolations = Verifier.VerifySchedule(data, schedule)

        stundenplanLines.Add($"# Stundenplan: {bestand.SchulName}")
        stundenplanLines.Add("")
        stundenplanLines.Add($"**Status:** SolveTop ({topResult.StopReason})  |  **CP-SAT-Status:** {best.Status}  |  **Kann-Verstoesse:** {best.Quality.KannViolationCount}  |  **Qualitaet (Total):** {best.Quality.Total:F1}  |  **Verstoesse:** {scheduleViolations.Count}")
        stundenplanLines.Add("")
        If einsaetze.Count > 1 Then
            stundenplanLines.Add($"*Mehr-Zuteilungs-Modus: {einsaetze.Count} Lehrer-Zuteilungen mit je eigenem Stufe-2-Lauf ({perAssignmentBudget:F0}s / {perAssignmentMaxSolutions} Loesungen pro Zuteilung); die hier dokumentierte beste Loesung stammt aus Zuteilung {bestRun.AssignmentIndex}. Alle Loesungen aller Zuteilungen: output/stundentafel.html (Spalte 'Zuteilung').*")
            stundenplanLines.Add("")
        End If
        If topResult.StagnationTriggeredCount > 0 Then
            stundenplanLines.Add($"*Phase 2.25: die Stagnationserkennung hat {topResult.StagnationTriggeredCount} von {topResult.IterationsRun} Solve-Iteration(en) vorzeitig abgebrochen, weil ueber `stagnation_timeout_s` hinweg keine Verbesserung mehr gefunden wurde - spart Zeit fuer weitere Iterationen statt eine stehende Suche bis zum Zeitlimit weiterlaufen zu lassen.*")
            stundenplanLines.Add("")
        End If
        If best.Status = CpSolverStatus.Feasible Then
            stundenplanLines.Add($"*Hinweis: `Feasible` statt `Optimal` bedeutet, CP-SAT konnte innerhalb von `solve_time_limit_s` (aktuell {cfg.SolveTimeLimitS}s) keinen Optimalitaetsbeweis erbringen - ein hoeherer Wert in `config.yaml` kann helfen, ein noch besseres Ergebnis zu finden oder das aktuelle als optimal zu beweisen.*")
            If cfg.PerSolveTimeLimitS.HasValue AndAlso perSolveLimit <> cfg.SolveTimeLimitS Then
                stundenplanLines.Add($"*Zusaetzlich begrenzt `per_solve_time_limit_s` (aktuell {perSolveLimit}s) jede EINZELNE Solve-Iteration - ein hoeherer Wert kann derselben Iteration mehr Zeit fuer einen Optimalitaetsbeweis geben, auf Kosten weniger Iterationen fuer zusaetzliche `max_solutions`-Alternativen innerhalb desselben Gesamtbudgets.*")
            End If
            stundenplanLines.Add("")
        End If

        ' Phase 2.22: Antwort auf "wie weit von Optimal entfernt?"/"wuerde
        ' mehr Zeit die Qualitaet noch deutlich verbessern?" - CP-SAT's
        ' bewiesene untere Schranke (BestObjectiveBound) macht die
        ' Optimalitaets-Luecke sichtbar, der Konvergenz-Verlauf zeigt, WANN
        ' innerhalb dieses einen Solve-Versuchs die letzte Verbesserung
        ' gefunden wurde.
        Dim gapAbs = Math.Max(best.ObjectiveValue - best.BestObjectiveBound, 0.0)
        Dim gapPercent = If(best.ObjectiveValue > 0.0, 100.0 * gapAbs / best.ObjectiveValue, 0.0)
        stundenplanLines.Add("## Optimalitaets-Luecke")
        stundenplanLines.Add("")
        If best.Status = CpSolverStatus.Optimal Then
            stundenplanLines.Add("CP-SAT hat bewiesen, dass diese Loesung fuer das aktuelle Modell optimal ist (Luecke = 0%).")
        Else
            stundenplanLines.Add($"Gefundene Loesung (Objective): **{best.ObjectiveValue:F1}**  |  Bewiesene untere Schranke: **{best.BestObjectiveBound:F1}**  |  Maximal noch moegliche Verbesserung: **{gapPercent:F1}%**")
            stundenplanLines.Add("")
            stundenplanLines.Add("*Diese Luecke ist eine bewiesene OBERGRENZE, keine Vorhersage - die tatsaechlich erreichbare Verbesserung kann kleiner sein (bis hin zu 0, falls die gefundene Loesung bereits optimal ist, CP-SAT das aber innerhalb der Zeit nicht beweisen konnte).*")
        End If
        stundenplanLines.Add("")
        If best.Convergence.Count > 1 Then
            stundenplanLines.Add("**Verlauf dieses Solve-Versuchs** (jede gefundene Verbesserung, nicht in festen Zeitabstaenden):")
            stundenplanLines.Add("")
            stundenplanLines.Add("| Zeit (s) | Objective |")
            stundenplanLines.Add("|---|---|")
            For Each p In best.Convergence
                stundenplanLines.Add($"| {p.ElapsedS:F1} | {p.ObjectiveValue:F1} |")
            Next
            Dim lastImprovementS = best.Convergence.Last().ElapsedS
            stundenplanLines.Add("")
            stundenplanLines.Add($"*Letzte Verbesserung bei {lastImprovementS:F1}s - fand danach bis zum Abbruch keine weitere statt. Ein deutlich frueherer letzter Eintrag als das Zeitbudget legt nahe, dass zusaetzliche Zeit fuer DIESEN Versuch wenig bringen wuerde.*")
        Else
            stundenplanLines.Add("*Nur eine einzige (die erste gefundene) Loesung in diesem Versuch - kein Verbesserungsverlauf.*")
        End If
        stundenplanLines.Add("")

        If scheduleViolations.Count > 0 Then
            stundenplanLines.Add("## Verstoesse (Verifier.VerifySchedule)")
            stundenplanLines.Add("")
            For Each v In scheduleViolations : stundenplanLines.Add($"- {v}") : Next
            stundenplanLines.Add("")
            erfolg = False
        End If

        Dim days = bestand.Tage
        Dim periods = Enumerable.Range(1, bestand.PeriodsPerDay).ToList()

        stundenplanLines.Add("## Klassen")
        stundenplanLines.Add("")
        Dim classGrids = Formatting.ToClassGrids(data, schedule)
        For Each klasse In bestand.Klassen
            stundenplanLines.Add(Formatting.FormatGridMarkdown(klasse.Name, classGrids(klasse.Name), days, periods, AddressOf ClassCellText))
            stundenplanLines.Add("")
        Next

        stundenplanLines.Add("## Lehrkraefte")
        stundenplanLines.Add("")
        Dim teacherGrids = Formatting.ToTeacherGrids(data, schedule)
        For Each lehrer In bestand.Lehrkraefte
            stundenplanLines.Add(Formatting.FormatGridMarkdown(lehrer.Name, teacherGrids(lehrer.Name), days, periods, AddressOf TeacherCellText))
            stundenplanLines.Add("")
        Next

        IO.File.WriteAllText(IO.Path.Combine(outputDir, "stundenplan.md"), String.Join(vbLf, stundenplanLines))

        ' Phase 2.21: "Stundentafel"-Visualisierung - eine JSON-Datei mit
        ' ALLEN von SolveTop gefundenen Loesungen (nicht nur der besten,
        ' siehe Zeile 150 oben) plus eine dazugehoerige, generische
        ' JS-Viewer-HTML, die dieselben Daten inline eingebettet enthaelt
        ' (kein fetch() noetig, funktioniert daher auch bei direktem
        ' Doeffnen per Doppelklick ohne lokalen Webserver).
        Dim stundentafelJson = Formatting.ToStundentafelJsonMulti(bestand, runs, qualityWeights, eqKlassen)
        Dim stundentafelJsonText = stundentafelJson.ToJsonString(New JsonSerializerOptions With {.WriteIndented = True})
        IO.File.WriteAllText(IO.Path.Combine(outputDir, "stundenplan.json"), stundentafelJsonText)
        IO.File.WriteAllText(IO.Path.Combine(outputDir, "stundentafel.html"),
            StundentafelHtml.BuildStundentafelHtml(stundentafelJson.ToJsonString()))

        Dim zuteilungsInfo = If(einsaetze.Count > 1, $", Zuteilungen={einsaetze.Count} (beste={bestRun.AssignmentIndex})", "")
        Console.WriteLine($"[{schule}] {If(erfolg, "PASS", "FAIL")} - Lehrereinsatzplanung={lehrereinsatz.Status} (Objective={lehrereinsatz.Solver.ObjectiveValue}){zuteilungsInfo}, Solver.SolveTop={topResult.StopReason}, CP-SAT-Status={best.Status} (Kann-Verstoesse={best.Quality.KannViolationCount}, Quality.Total={best.Quality.Total:F1}), Verstoesse={scheduleViolations.Count}")
        Return erfolg
    End Function

    ''' <summary>`--all`: iteriert ueber jedes Unterverzeichnis von
    ''' testsRoot mit einer input/stammdaten.yaml. Liefert True nur, wenn
    ''' ALLE Schulen erfolgreich waren.</summary>
    Public Function RunAll(testsRoot As String) As Boolean
        If Not IO.Directory.Exists(testsRoot) Then
            Console.WriteLine($"'{testsRoot}' existiert nicht.")
            Return False
        End If
        Dim schulen = IO.Directory.GetDirectories(testsRoot).
            Where(Function(d) IO.File.Exists(IO.Path.Combine(d, "input", "stammdaten.yaml"))).
            Select(Function(d) IO.Path.GetFileName(d)).OrderBy(Function(s) s).ToList()
        If schulen.Count = 0 Then
            Console.WriteLine($"Keine Schulen mit '{IO.Path.Combine("input", "stammdaten.yaml")}' unter '{testsRoot}' gefunden.")
            Return False
        End If
        Dim alleOk = True
        For Each schule In schulen
            alleOk = RunOne(testsRoot, schule) AndAlso alleOk
        Next
        Return alleOk
    End Function

End Module
