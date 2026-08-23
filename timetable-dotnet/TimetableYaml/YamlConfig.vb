' Solve-Parameter aus tests/<schule>/input/config.yaml. Liegt seit dem
' GUI-Unterbau (Stufe A) in TimetableYaml statt im SchoolTestRunner:
' die Phase-3-GUI spiegelt diese Felder in ihrem Einstellungsdialog
' (gui-ui-konzept.md 6.12) und braucht sie deshalb hinter einer
' Assemblygrenze, die kein Konsolenprojekt ist. Reine Verschiebung -
' Feldnamen, Defaults und Aufloesungsregeln sind unveraendert.
Imports TimetableCore
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
    ''' <summary>Stufe 0 (docs/klassenbildung-plan.md, K4): Parameter des
    ''' `klassen`-Subkommandos. Nothing/fehlender Block = Defaults.</summary>
    Public Property Klassenbildung As KlassenbildungConfig = Nothing
End Class

''' <summary>Parameter der Klassenbildung (Konzept Abschnitte 4/8) als
''' optionaler `klassenbildung:`-Block in config.yaml - Nothing je Feld
''' = Default von Klassenbildung.SolveKlassenbildungTop.</summary>
Public NotInheritable Class KlassenbildungConfig
    Public Property ZeitlimitS As Double? = Nothing
    Public Property NVarianten As Integer? = Nothing
    ''' <summary>Qualitaetsschranke der Varianten: Zielfunktion &lt;=
    ''' Optimum * (1 + epsilon). Default 0.05.</summary>
    Public Property Epsilon As Double? = Nothing
    ''' <summary>Mindestanzahl Kinder, die jede weitere Variante anders
    ''' zuordnen muss. Default 8.</summary>
    Public Property MinDistanz As Integer? = Nothing
    Public Property Symmetriebrechung As Boolean? = Nothing
    ''' <summary>Prio-Stufe (1..3) -> Zielfunktions-Gewicht. Fehlend =
    ''' Klassenbildung.DefaultPrioGewichte (1000/50/1).</summary>
    Public Property PrioGewichte As Dictionary(Of Integer, Long) = Nothing
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

Public Module YamlConfig


    Private ReadOnly ConfigDeserializer As IDeserializer = New DeserializerBuilder().
        WithNamingConvention(UnderscoredNamingConvention.Instance).
        Build()

    Public Function LoadConfig(path As String) As RunConfig
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
    Public Function BuildQualityWeights(cfg As QualityWeightsConfig) As QualityWeights
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

End Module
