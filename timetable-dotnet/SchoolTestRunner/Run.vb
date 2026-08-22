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
    ''' <summary>Phase 2.21: Default UNVERAENDERT bei 1 - bestehende Schulen
    ''' ohne explizites `max_solutions` in ihrer config.yaml berechnen
    ''' weiterhin genau eine Loesung, keine Laufzeit-/Output-Diff-
    ''' Regression. Ein hoeherer Wert exportiert zusaetzliche, vergleichbare
    ''' Alternativen in output/stundenplan.json + output/stundentafel.html -
    ''' das Gesamt-Zeitbudget bleibt dabei unveraendert durch
    ''' solve_time_limit_s gedeckelt (Solver.SolveTop prueft das
    ''' verbleibende Budget vor jeder weiteren Iteration).</summary>
    Public Property MaxSolutions As Integer = 1
    ''' <summary>Default Nothing - faellt dann auf SolveTimeLimitS zurueck,
    ''' identisch zum bisherigen Verhalten (jede bestehende Schule ohne
    ''' dieses Feld bleibt byte-identisch). Begrenzt nur die EINZELNE
    ''' Solve-Iteration in Solver.SolveTop, waehrend SolveTimeLimitS
    ''' weiterhin das Gesamtbudget ueber alle Iterationen deckelt -
    ''' relevant, wenn eine einzelne Iteration sonst das komplette Budget
    ''' aufbrauchen wuerde, bevor SolveTop bei max_solutions > 1 ueberhaupt
    ''' zu einer zweiten Iteration kommt.</summary>
    Public Property PerSolveTimeLimitS As Double? = Nothing
    ''' <summary>Phase 2.24: Default Nothing - faellt dann komplett auf
    ''' ScheduleQuality.vb's eigene Weight*-Konstanten zurueck (identisch
    ''' zum Verhalten vor dieser Config-Erweiterung, byte-identisch fuer
    ''' jede bestehende Schule ohne "quality_weights"-Block in ihrer
    ''' config.yaml). Jedes einzelne Unterfeld ist selbst wieder optional -
    ''' nur gesetzte Felder ueberschreiben den jeweiligen Default, siehe
    ''' BuildQualityWeights.</summary>
    Public Property QualityWeights As QualityWeightsConfig = Nothing
    ''' <summary>Phase 2.25: Nothing falls back to SolveTop's own 45.0s
    ''' default (active there regardless of this field - see SolveTop's
    ''' doc comment). Set explicitly here only to override that default
    ''' per school, e.g. a larger value for a school whose genuine
    ''' improvements are known to arrive slowly.</summary>
    Public Property StagnationTimeoutS As Double? = Nothing
    Public Property DiversifySeed As Boolean? = Nothing
    Public Property RandomizeSearch As Boolean? = Nothing
    ''' <summary>Phase 2.25: stays Nothing (SolveTop's own default, i.e.
    ''' CP-SAT's proof-of-optimality behavior is unchanged) unless a school
    ''' explicitly opts in - unlike the three fields above, this one
    ''' changes WHEN a solution is accepted as proven-final, not just how
    ''' long a stagnant search runs.</summary>
    Public Property RelativeGapLimit As Double? = Nothing
    ''' <summary>Code-Review-Umsetzung (P2): Solver.SolveTops
    ''' lexikografischer Modus (Kann -> ClassGaps als einzeln beweisbare
    ''' Stufen, Optimum je Stufe als Constraint fixiert, danach Iterationen
    ''' ueber die gewichtete Rest-Zielfunktion). Nothing -> True: seit der
    ''' expliziten Nutzerentscheidung dieser Review-Runde DEFAULT (bewusste
    ''' Ausnahme vom "fehlt das Feld, bleibt das Verhalten unveraendert"-
    ''' Prinzip, wie zuvor stagnation_timeout_s). `false` liefert den
    ''' frueheren gewichteten Summenmodus, in dem quality_weights auch die
    ''' relative Prioritaet von Kann/ClassGaps/TeacherGaps frei steuern.</summary>
    Public Property Lexicographic As Boolean? = Nothing
    ''' <summary>P2: Toleranzband je fixierter Stufe (`<= Stufenoptimum +
    ''' lex_tolerance`) - 0 (Default) haelt jede Stufe exakt auf ihrem
    ''' gefundenen Optimum, ein kleiner Wert (z.B. 1) erlaubt den
    ''' Folgestufen/der Diversitaets-Enumeration mehr Spielraum.</summary>
    Public Property LexTolerance As Integer? = Nothing
    ''' <summary>P2 (Nutzerentscheidung): TeacherGaps als DRITTE
    ''' lexikografische Stufe ist opt-in (Nothing -> False). Ohne die
    ''' Stufe wird TeacherGaps nicht hart auf sein Optimum fixiert,
    ''' sondern flieSSt mit seinem quality_weights-Gewicht in die
    ''' Rest-Zielfunktion ein - Lehrer-Springstunden bleiben so gegen
    ''' Randstunden/Nachmittage/Ausgewogenheit abwaegbar.</summary>
    Public Property LexTeacherGapsStage As Boolean? = Nothing
    ''' <summary>Dichte-STUFE (opt-in, Nothing -> False): occupied_window-
    ''' Dichte als eigene lexikografische Stufe zwischen Kann und
    ''' ClassGaps - dediziertes Budget + hartes Band, exakt der
    ''' strukturelle Vorteil, mit dem die fruehere occupied_slot-Batterie
    ''' den P1-Langvergleich auf der Fensterabdeckung gewann. Ohne die
    ''' Stufe bleibt die Dichte gewichtet in der Rest-Zielfunktion
    ''' (abwaegbar gegen die uebrigen Restkriterien).</summary>
    Public Property LexOccupiedDensityStage As Boolean? = Nothing
    ''' <summary>Rhythmisierung (opt-in, Nothing -> False): Fach-Fenster-
    ''' Verstoesse (subject_period_window, should) als eigene
    ''' lexikografische Stufe nach der Dichte-Stufe und vor ClassGaps -
    ''' dediziertes Budget + hartes Band fuer "Kernfaecher vormittags"/
    ''' "AGs nachmittags". Ohne die Stufe bleiben die Verstoesse mit
    ''' quality_weights.subject_window gewichtet in der Rest-Zielfunktion
    ''' (abwaegbar gegen die uebrigen Restkriterien).</summary>
    Public Property LexSubjectWindowStage As Boolean? = Nothing
    ''' <summary>Code-Review-Umsetzung (P3): Mindestanzahl bisher belegter
    ''' Slots, die jede WEITERE Loesung anders belegen muss (echter
    ''' Distanz-Cut statt nur des exakten No-Goods). Nothing/0 = heutiges
    ''' Verhalten; sinnvolle Werte liegen bei ~5-10% der Wochenstunden.</summary>
    Public Property MinDiversity As Integer? = Nothing
    ''' <summary>P3: `false` schaltet das Re-Hinting jeder Iteration auf die
    ''' soeben gefundene Loesung ab (das ist eine Aehnlichkeits-Heuristik -
    ''' fuer Laeufe, deren Ziel moeglichst VERSCHIEDENE Alternativen sind,
    ''' abschalten). Nothing/true = heutiges Verhalten.</summary>
    Public Property RehintFoundSolutions As Boolean? = Nothing
    ''' <summary>Code-Review-Umsetzung (P6): relatives Gap-Limit NUR fuer
    ''' Iterationen ab der zweiten - die erste darf sorgfaeltig beweisen,
    ''' Folge-Iterationen (Zweck: Alternativen) akzeptieren frueher.
    ''' Sinnvolle Werte ~0.05-0.2; Nothing = kein Limit (unveraendertes
    ''' Verhalten). Ueberstimmt ab Iteration 2 ein gesetztes
    ''' relative_gap_limit.</summary>
    Public Property LaterIterationsGapLimit As Double? = Nothing
    ''' <summary>Mehr-Zuteilungs-Modus (Nothing -> 1 = bisheriges
    ''' Verhalten): Anzahl unterschiedlicher Lehrer-Zuteilungen aus
    ''' Stufe 1, die jeweils einen EIGENEN Stufe-2-Lauf bekommen
    ''' (Gesamtbudget solve_time_limit_s und max_solutions werden
    ''' gleichmaessig aufgeteilt). Bei &gt; 1 ist die
    ''' Lex-Symmetriebrechung automatisch aktiv (siehe
    ''' assignment_symmetry_breaking).</summary>
    Public Property MaxAssignments As Integer? = Nothing
    ''' <summary>Band um das beste Lehrereinsatz-Objective, innerhalb
    ''' dessen alternative Zuteilungen akzeptiert werden (Nothing -> 0 =
    ''' nur gleich gute Alternativen).</summary>
    Public Property AssignmentTolerance As Integer? = Nothing
    ''' <summary>Mindestanzahl (Klasse,Fach)-Einheiten, in denen jede
    ''' weitere Zuteilung eine ANDERE Lehrkraft waehlen muss (das
    ''' minDiversity-Muster der Stufe 2). Reine Permutationen
    ''' austauschbarer Lehrkraefte blockt nicht dieser Cut, sondern die
    ''' automatisch aktive Lex-Symmetriebrechung. Nothing -> 1.</summary>
    Public Property AssignmentMinDiversity As Integer? = Nothing
    ''' <summary>Lex-Symmetriebrechung ueber die Aequivalenzklassen
    ''' austauschbarer Lehrkraefte (TeacherEquivalenceClasses). Nothing ->
    ''' automatisch: aktiv genau dann, wenn max_assignments &gt; 1 (im
    ''' Ein-Zuteilungs-Modus bleibt der bisherige Repraesentant
    ''' unveraendert). Explizit true/false erzwingt an/aus.</summary>
    Public Property AssignmentSymmetryBreaking As Boolean? = Nothing
    ''' <summary>Budget je lexikografischer Stufe bzw. (im gewichteten
    ''' Modus) fuer den Kann-Warm-Start - Nothing faellt auf SolveTops
    ''' eigenen Default (60s) zurueck. Relevant fuer grosse Szenarien,
    ''' bei denen eine Stufe in 60s nicht einmal Feasibility erreicht
    ''' und die Folge-Iterationen dann ohne Warm-Start-Hint kalt
    ''' starten (live beobachteter GMS-Fehlermodus: TimeLimitReached
    ''' ohne eine einzige Loesung).</summary>
    Public Property Stage1TimeLimitS As Double? = Nothing
End Class

''' <summary>Phase 2.24: die sieben Gewichte aus ScheduleQuality.
''' QualityWeights, als optionale (Nothing = ungesetzt) YAML-Felder unter
''' `quality_weights:` in config.yaml. Feldnamen bewusst identisch zu den
''' Property-Namen von ScheduleQuality.QualityWeights (nur snake_case ueber
''' YamlDotNets UnderscoredNamingConvention), damit BuildQualityWeights
''' unten eine simple 1:1-Uebertragung bleibt.</summary>
Public NotInheritable Class QualityWeightsConfig
    Public Property Kann As Double? = Nothing
    Public Property ClassGaps As Double? = Nothing
    Public Property TeacherGaps As Double? = Nothing
    Public Property EdgePeriod As Double? = Nothing
    Public Property AfternoonDayCount As Double? = Nothing
    Public Property ClassLoadVariance As Double? = Nothing
    Public Property TeacherLoadVariance As Double? = Nothing
    ''' <summary>P1: Gewicht pro unbelegtem occupied_window-Slot (Default
    ''' 5.0 - siehe ScheduleQuality.WeightOccupiedDensity).</summary>
    Public Property OccupiedDensity As Double? = Nothing
    ''' <summary>Rhythmisierung: Gewicht pro Unterrichtsstunde ausserhalb
    ''' ihres subject_period_window-Bereichs (Default 5.0 - siehe
    ''' ScheduleQuality.WeightSubjectWindow).</summary>
    Public Property SubjectWindow As Double? = Nothing
    ''' <summary>Phase 2.25-Nachtrag-2: Nothing -&gt; Default True (unveraendertes
    ''' Verhalten). `false` schaltet TeacherGaps strukturell aus SolveTops
    ''' Zielfunktion aus (keine Hilfsvariablen/-Constraints, nicht nur
    ''' Gewicht 0) - Sicherheitsventil fuer Schulen, bei denen selbst die
    ''' gefixte Kodierung noch zu teuer ist.</summary>
    Public Property IncludeTeacherGaps As Boolean? = Nothing
    ''' <summary>Code-Review-Umsetzung (R3): gleiches Muster jetzt auch fuer
    ''' ClassGaps - vorher das einzige Kriterium ohne strukturelles Flag.</summary>
    Public Property IncludeClassGaps As Boolean? = Nothing
    ''' <summary>Gleiches strukturelles An/Aus-Muster wie IncludeTeacherGaps
    ''' oben, auf die verbleibenden vier Sekundaerkriterien erweitert -
    ''' Nothing -&gt; Default True (unveraendertes Verhalten) je Feld.</summary>
    Public Property IncludeEdgePeriod As Boolean? = Nothing
    Public Property IncludeAfternoonDayCount As Boolean? = Nothing
    Public Property IncludeClassLoadVariance As Boolean? = Nothing
    Public Property IncludeTeacherLoadVariance As Boolean? = Nothing
    Public Property IncludeOccupiedDensity As Boolean? = Nothing
    Public Property IncludeSubjectWindow As Boolean? = Nothing
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

    ''' <summary>Phase 2.24: startet bei ScheduleQuality.QualityWeights'
    ''' eigenen Defaults (`New QualityWeights()` - identisch zu den
    ''' hartcodierten Weight*-Konstanten) und ueberschreibt nur die in
    ''' config.yaml tatsaechlich gesetzten Unterfelder. `cfg = Nothing`
    ''' (kein "quality_weights"-Block in config.yaml) liefert unveraendert
    ''' die reinen Defaults.</summary>
    Private Function BuildQualityWeights(cfg As QualityWeightsConfig) As QualityWeights
        Dim w As New QualityWeights()
        If cfg Is Nothing Then Return w
        If cfg.Kann.HasValue Then w.Kann = cfg.Kann.Value
        If cfg.ClassGaps.HasValue Then w.ClassGaps = cfg.ClassGaps.Value
        If cfg.TeacherGaps.HasValue Then w.TeacherGaps = cfg.TeacherGaps.Value
        If cfg.EdgePeriod.HasValue Then w.EdgePeriod = cfg.EdgePeriod.Value
        If cfg.AfternoonDayCount.HasValue Then w.AfternoonDayCount = cfg.AfternoonDayCount.Value
        If cfg.ClassLoadVariance.HasValue Then w.ClassLoadVariance = cfg.ClassLoadVariance.Value
        If cfg.TeacherLoadVariance.HasValue Then w.TeacherLoadVariance = cfg.TeacherLoadVariance.Value
        If cfg.OccupiedDensity.HasValue Then w.OccupiedDensity = cfg.OccupiedDensity.Value
        If cfg.SubjectWindow.HasValue Then w.SubjectWindow = cfg.SubjectWindow.Value
        If cfg.IncludeTeacherGaps.HasValue Then w.IncludeTeacherGaps = cfg.IncludeTeacherGaps.Value
        If cfg.IncludeClassGaps.HasValue Then w.IncludeClassGaps = cfg.IncludeClassGaps.Value
        If cfg.IncludeEdgePeriod.HasValue Then w.IncludeEdgePeriod = cfg.IncludeEdgePeriod.Value
        If cfg.IncludeAfternoonDayCount.HasValue Then w.IncludeAfternoonDayCount = cfg.IncludeAfternoonDayCount.Value
        If cfg.IncludeClassLoadVariance.HasValue Then w.IncludeClassLoadVariance = cfg.IncludeClassLoadVariance.Value
        If cfg.IncludeTeacherLoadVariance.HasValue Then w.IncludeTeacherLoadVariance = cfg.IncludeTeacherLoadVariance.Value
        If cfg.IncludeOccupiedDensity.HasValue Then w.IncludeOccupiedDensity = cfg.IncludeOccupiedDensity.Value
        If cfg.IncludeSubjectWindow.HasValue Then w.IncludeSubjectWindow = cfg.IncludeSubjectWindow.Value
        Return w
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
