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
Imports System.Text.Json.Nodes
Imports YamlDotNet.Serialization
Imports YamlDotNet.Serialization.NamingConventions

Public NotInheritable Class RunConfig
    Public Property DeputatToleranzStunden As Double = 2.0
    Public Property LehrereinsatzTimeLimitS As Double = 30.0
    Public Property SolveTimeLimitS As Double = 30.0
    Public Property Seed As Integer = 42
    ''' <summary>Default: alle CPU-Kerne minus 1 (mindestens 1) - laesst dem
    ''' Betriebssystem/anderen Prozessen einen Kern frei, statt die Maschine
    ''' beim CP-SAT-Portfolio-Search komplett auszulasten.</summary>
    Public Property NumWorkers As Integer = Math.Max(1, Environment.ProcessorCount - 1)
End Class

Public Module Run

    Private ReadOnly ConfigDeserializer As IDeserializer = New DeserializerBuilder().
        WithNamingConvention(UnderscoredNamingConvention.Instance).
        Build()

    Private Function LoadConfig(path As String) As RunConfig
        If Not IO.File.Exists(path) Then Return New RunConfig()
        Dim yaml = IO.File.ReadAllText(path)
        If String.IsNullOrWhiteSpace(yaml) Then Return New RunConfig()
        Return ConfigDeserializer.Deserialize(Of RunConfig)(yaml)
    End Function

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

        Dim lehrereinsatz = Lehrereinsatzplanung.SolveLehrereinsatz(
            bestand, deputatToleranzStunden:=cfg.DeputatToleranzStunden, timeLimitS:=cfg.LehrereinsatzTimeLimitS,
            seed:=cfg.Seed, numWorkers:=cfg.NumWorkers)
        Dim lehrereinsatzOk = lehrereinsatz.Status = CpSolverStatus.Optimal OrElse lehrereinsatz.Status = CpSolverStatus.Feasible

        If Not lehrereinsatzOk Then
            IO.File.WriteAllText(IO.Path.Combine(outputDir, "lehrerzuteilung.md"),
                $"# Lehrerzuteilung: {bestand.SchulName}{vbLf}{vbLf}**Status:** {lehrereinsatz.Status} (Lehrereinsatzplanung nicht loesbar){vbLf}")
            Console.WriteLine($"[{schule}] FAIL - Lehrereinsatzplanung: {lehrereinsatz.Status}")
            Return False
        End If

        Dim lehrereinsatzViolations = Verifier.VerifyLehrereinsatz(bestand, lehrereinsatz)
        Dim lehrerzuteilungMd = Formatting.FormatLehrereinsatzMarkdown(bestand, lehrereinsatz)
        If lehrereinsatzViolations.Count > 0 Then
            lehrerzuteilungMd &= vbLf & vbLf & "## Verstoesse (Verifier.VerifyLehrereinsatz)" & vbLf & vbLf &
                String.Join(vbLf, lehrereinsatzViolations.Select(Function(v) $"- {v}"))
            erfolg = False
        End If
        IO.File.WriteAllText(IO.Path.Combine(outputDir, "lehrerzuteilung.md"), lehrerzuteilungMd)

        If lehrereinsatzViolations.Count > 0 Then
            Console.WriteLine($"[{schule}] FAIL - VerifyLehrereinsatz: {lehrereinsatzViolations.Count} Verstoesse")
            Return False
        End If

        Dim derivedConstraints = Lehrereinsatzplanung.BuildAssignmentConstraints(lehrereinsatz, bestand)
        Dim ent = Stammdaten.BuildEntitiesFragment(bestand)
        Dim alleConstraints = derivedConstraints.Concat(handConstraints).ToList()
        Dim data As New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray(alleConstraints.Select(Function(c) CType(c, JsonNode)).ToArray())}
        }

        Dim validationErrors = Validation.ValidateEntities(data)
        If validationErrors.Count > 0 Then
            IO.File.WriteAllText(IO.Path.Combine(outputDir, "stundenplan.md"),
                $"# Stundenplan: {bestand.SchulName}{vbLf}{vbLf}**Status:** Validation.ValidateEntities FEHLGESCHLAGEN{vbLf}{vbLf}" &
                String.Join(vbLf, validationErrors.Select(Function(e) $"- {e}")))
            Console.WriteLine($"[{schule}] FAIL - Validation.ValidateEntities: {validationErrors.Count} Fehler")
            Return False
        End If

        ' Nutzt SolveTop statt Solve: dieselbe volle Qualitaets-Zielfunktion
        ' (Luecken/Randstunden/Tagesausgewogenheit, siehe ScheduleQuality.vb)
        ' fliesst direkt ins CP-SAT-Modell ein, statt erst nachtraeglich nur
        ' zum Sortieren einer Kann-only-Loesung benutzt zu werden - der Solver
        ' sucht dadurch von vornherein nach einem bzgl. dieser Kriterien
        ' moeglichst guten statt nur irgendeinem zulaessigen Plan.
        ' maxSolutions:=1, da hier ein einzelner finaler Plan pro Schule
        ' geschrieben wird (kein Alternativen-Vergleich noetig) - die
        ' Zielfunktion selbst steuert bereits die Guete dieser einen Loesung.
        Dim topResult = Solver.SolveTop(data, maxSolutions:=1, totalTimeLimitS:=cfg.SolveTimeLimitS,
            perSolveTimeLimitS:=cfg.SolveTimeLimitS, seed:=cfg.Seed, numWorkers:=cfg.NumWorkers)
        Dim solveOk = topResult.Solutions.Count > 0
        If Not solveOk Then
            IO.File.WriteAllText(IO.Path.Combine(outputDir, "stundenplan.md"),
                $"# Stundenplan: {bestand.SchulName}{vbLf}{vbLf}**Status:** {topResult.StopReason} (Solver.SolveTop fand keine Loesung){vbLf}")
            Console.WriteLine($"[{schule}] FAIL - Solver.SolveTop: {topResult.StopReason}")
            Return False
        End If

        Dim best = topResult.Solutions(0)
        Dim schedule = best.Schedule
        Dim scheduleViolations = Verifier.VerifySchedule(data, schedule)

        stundenplanLines.Add($"# Stundenplan: {bestand.SchulName}")
        stundenplanLines.Add("")
        stundenplanLines.Add($"**Status:** SolveTop ({topResult.StopReason})  |  **Kann-Verstoesse:** {best.Quality.KannViolationCount}  |  **Qualitaet (Total):** {best.Quality.Total:F1}  |  **Verstoesse:** {scheduleViolations.Count}")
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

        Console.WriteLine($"[{schule}] {If(erfolg, "PASS", "FAIL")} - Lehrereinsatzplanung={lehrereinsatz.Status} (Objective={lehrereinsatz.Solver.ObjectiveValue}), Solver.SolveTop={topResult.StopReason} (Kann-Verstoesse={best.Quality.KannViolationCount}, Quality.Total={best.Quality.Total:F1}), Verstoesse={scheduleViolations.Count}")
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
