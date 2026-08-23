' Aufbereitung eines LaufErgebnis zu den beiden Markdown-Berichten und zum
' Stundentafel-JSON (Stufe B des GUI-Unterbaus).
'
' Mechanisch aus SchoolTestRunner/Run.vb herausgeloest - die Zeichenketten
' sind absichtlich UNVERAENDERT uebernommen, damit die committeten
' Beispiel-Outputs byte-identisch bleiben und der Umbau nachweisbar
' verhaltensneutral ist. Die CLI schreibt das Ergebnis in Dateien, die
' Phase-3-GUI zeigt es im Bericht-Export.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports TimetableCore

Public Module StundenplanBericht

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

    ''' <summary>lehrerzuteilung.md - existiert in drei Auspraegungen, je
    ''' nachdem wie weit die Pipeline kam. Liefert Nothing, wenn die Stufe
    ''' noch gar nicht erreicht war (dann schreibt der Aufrufer nichts).</summary>
    Public Function BaueLehrerzuteilungMarkdown(e As LaufErgebnis) As String
        Dim schulName = e.Bestand.SchulName

        If e.Stufe = LaufStufe.Stammdatenpruefung Then
            If e.Meldungen.Count = 0 Then Return Nothing
            Dim zeilen As New List(Of String) From {
                $"# Lehrerzuteilung: {schulName}", "", "**Status:** StammdatenValidation FEHLGESCHLAGEN", ""}
            For Each m In e.Meldungen : zeilen.Add($"- {m}") : Next
            Return String.Join(vbLf, zeilen)
        End If

        If e.LehrereinsatzBloecke.Count = 0 Then
            Dim status = If(e.Einsaetze.Count > 0, e.Einsaetze(0).Status.ToString(), CpSolverStatus.Unknown.ToString())
            Return $"# Lehrerzuteilung: {schulName}{vbLf}{vbLf}**Status:** {status} (Lehrereinsatzplanung nicht loesbar){vbLf}"
        End If

        Return String.Join(vbLf & vbLf & "---" & vbLf & vbLf, e.LehrereinsatzBloecke)
    End Function

    ''' <summary>stundenplan.md. Liefert Nothing, solange die Pipeline die
    ''' Stundenplan-Stufe nicht erreicht hat - dann gibt es nichts zu
    ''' berichten, und der bisherige Stand bleibt stehen ("kein
    ''' Alles-oder-Nichts", Run.vb-Kopfkommentar).</summary>
    Public Function BaueStundenplanMarkdown(e As LaufErgebnis) As String
        Dim schulName = e.Bestand.SchulName

        If e.Stufe = LaufStufe.Szenarienaufbau AndAlso e.FehlerZuteilung.HasValue Then
            Return $"# Stundenplan: {schulName}{vbLf}{vbLf}**Status:** Validation.ValidateEntities FEHLGESCHLAGEN (Zuteilung {e.FehlerZuteilung.Value}){vbLf}{vbLf}" &
                String.Join(vbLf, e.Meldungen.Select(Function(m) $"- {m}"))
        End If

        If e.Stufe < LaufStufe.Stundenplan Then Return Nothing

        If e.BesterLauf Is Nothing Then
            Dim reasons = String.Join("|", e.Laeufe.Select(Function(r) r.Result.StopReason.ToString()).Distinct())
            Return $"# Stundenplan: {schulName}{vbLf}{vbLf}**Status:** {reasons} (Solver.SolveTop fand keine Loesung){vbLf}"
        End If

        Dim cfg = e.Config
        Dim data = e.BesterLauf.Data
        Dim topResult = e.BesterLauf.Result
        Dim best = topResult.Solutions(0)
        Dim schedule = best.Schedule
        Dim zeilen As New List(Of String)

        zeilen.Add($"# Stundenplan: {schulName}")
        zeilen.Add("")
        zeilen.Add($"**Status:** SolveTop ({topResult.StopReason})  |  **CP-SAT-Status:** {best.Status}  |  **Kann-Verstoesse:** {best.Quality.KannViolationCount}  |  **Qualitaet (Total):** {best.Quality.Total:F1}  |  **Verstoesse:** {e.PlanVerstoesse.Count}")
        zeilen.Add("")
        If e.Einsaetze.Count > 1 Then
            zeilen.Add($"*Mehr-Zuteilungs-Modus: {e.Einsaetze.Count} Lehrer-Zuteilungen mit je eigenem Stufe-2-Lauf ({e.PerZuteilungBudgetS:F0}s / {e.PerZuteilungMaxLoesungen} Loesungen pro Zuteilung); die hier dokumentierte beste Loesung stammt aus Zuteilung {e.BesterLauf.AssignmentIndex}. Alle Loesungen aller Zuteilungen: output/stundentafel.html (Spalte 'Zuteilung').*")
            zeilen.Add("")
        End If
        If topResult.StagnationTriggeredCount > 0 Then
            zeilen.Add($"*Phase 2.25: die Stagnationserkennung hat {topResult.StagnationTriggeredCount} von {topResult.IterationsRun} Solve-Iteration(en) vorzeitig abgebrochen, weil ueber `stagnation_timeout_s` hinweg keine Verbesserung mehr gefunden wurde - spart Zeit fuer weitere Iterationen statt eine stehende Suche bis zum Zeitlimit weiterlaufen zu lassen.*")
            zeilen.Add("")
        End If
        If best.Status = CpSolverStatus.Feasible Then
            zeilen.Add($"*Hinweis: `Feasible` statt `Optimal` bedeutet, CP-SAT konnte innerhalb von `solve_time_limit_s` (aktuell {cfg.SolveTimeLimitS}s) keinen Optimalitaetsbeweis erbringen - ein hoeherer Wert in `config.yaml` kann helfen, ein noch besseres Ergebnis zu finden oder das aktuelle als optimal zu beweisen.*")
            If cfg.PerSolveTimeLimitS.HasValue AndAlso e.PerSolveLimitS <> cfg.SolveTimeLimitS Then
                zeilen.Add($"*Zusaetzlich begrenzt `per_solve_time_limit_s` (aktuell {e.PerSolveLimitS}s) jede EINZELNE Solve-Iteration - ein hoeherer Wert kann derselben Iteration mehr Zeit fuer einen Optimalitaetsbeweis geben, auf Kosten weniger Iterationen fuer zusaetzliche `max_solutions`-Alternativen innerhalb desselben Gesamtbudgets.*")
            End If
            zeilen.Add("")
        End If

        ' Phase 2.22: Antwort auf "wie weit von Optimal entfernt?"/"wuerde
        ' mehr Zeit die Qualitaet noch deutlich verbessern?" - CP-SAT's
        ' bewiesene untere Schranke (BestObjectiveBound) macht die
        ' Optimalitaets-Luecke sichtbar, der Konvergenz-Verlauf zeigt, WANN
        ' innerhalb dieses einen Solve-Versuchs die letzte Verbesserung
        ' gefunden wurde.
        Dim gapAbs = Math.Max(best.ObjectiveValue - best.BestObjectiveBound, 0.0)
        Dim gapPercent = If(best.ObjectiveValue > 0.0, 100.0 * gapAbs / best.ObjectiveValue, 0.0)
        zeilen.Add("## Optimalitaets-Luecke")
        zeilen.Add("")
        If best.Status = CpSolverStatus.Optimal Then
            zeilen.Add("CP-SAT hat bewiesen, dass diese Loesung fuer das aktuelle Modell optimal ist (Luecke = 0%).")
        Else
            zeilen.Add($"Gefundene Loesung (Objective): **{best.ObjectiveValue:F1}**  |  Bewiesene untere Schranke: **{best.BestObjectiveBound:F1}**  |  Maximal noch moegliche Verbesserung: **{gapPercent:F1}%**")
            zeilen.Add("")
            zeilen.Add("*Diese Luecke ist eine bewiesene OBERGRENZE, keine Vorhersage - die tatsaechlich erreichbare Verbesserung kann kleiner sein (bis hin zu 0, falls die gefundene Loesung bereits optimal ist, CP-SAT das aber innerhalb der Zeit nicht beweisen konnte).*")
        End If
        zeilen.Add("")
        If best.Convergence.Count > 1 Then
            zeilen.Add("**Verlauf dieses Solve-Versuchs** (jede gefundene Verbesserung, nicht in festen Zeitabstaenden):")
            zeilen.Add("")
            zeilen.Add("| Zeit (s) | Objective |")
            zeilen.Add("|---|---|")
            For Each p In best.Convergence
                zeilen.Add($"| {p.ElapsedS:F1} | {p.ObjectiveValue:F1} |")
            Next
            Dim lastImprovementS = best.Convergence.Last().ElapsedS
            zeilen.Add("")
            zeilen.Add($"*Letzte Verbesserung bei {lastImprovementS:F1}s - fand danach bis zum Abbruch keine weitere statt. Ein deutlich frueherer letzter Eintrag als das Zeitbudget legt nahe, dass zusaetzliche Zeit fuer DIESEN Versuch wenig bringen wuerde.*")
        Else
            zeilen.Add("*Nur eine einzige (die erste gefundene) Loesung in diesem Versuch - kein Verbesserungsverlauf.*")
        End If
        zeilen.Add("")

        If e.PlanVerstoesse.Count > 0 Then
            zeilen.Add("## Verstoesse (Verifier.VerifySchedule)")
            zeilen.Add("")
            For Each v In e.PlanVerstoesse : zeilen.Add($"- {v}") : Next
            zeilen.Add("")
        End If

        Dim days = e.Bestand.Tage
        Dim periods = Enumerable.Range(1, e.Bestand.PeriodsPerDay).ToList()

        zeilen.Add("## Klassen")
        zeilen.Add("")
        Dim classGrids = Formatting.ToClassGrids(data, schedule)
        For Each klasse In e.Bestand.Klassen
            zeilen.Add(Formatting.FormatGridMarkdown(klasse.Name, classGrids(klasse.Name), days, periods, AddressOf ClassCellText))
            zeilen.Add("")
        Next

        zeilen.Add("## Lehrkraefte")
        zeilen.Add("")
        Dim teacherGrids = Formatting.ToTeacherGrids(data, schedule)
        For Each lehrer In e.Bestand.Lehrkraefte
            zeilen.Add(Formatting.FormatGridMarkdown(lehrer.Name, teacherGrids(lehrer.Name), days, periods, AddressOf TeacherCellText))
            zeilen.Add("")
        Next

        Return String.Join(vbLf, zeilen)
    End Function

    ''' <summary>Phase 2.21: das JSON mit ALLEN gefundenen Loesungen (nicht
    ''' nur der besten) - Grundlage von stundenplan.json und der in
    ''' stundentafel.html eingebetteten Daten.</summary>
    Public Function BaueStundentafelJson(e As LaufErgebnis) As JsonObject
        Return Formatting.ToStundentafelJsonMulti(e.Bestand, e.Laeufe, e.Gewichte, e.AequivalenzKlassen)
    End Function

End Module
