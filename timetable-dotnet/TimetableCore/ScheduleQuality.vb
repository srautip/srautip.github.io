' Phase 2.8: post-hoc quality scoring for candidate schedules returned by
' Solver.SolveTop. Deliberately separate from Verifier.vb - Verifier detects
' violations of explicit constraints (a pass/fail concern), while this
' module ranks otherwise-valid schedules against soft aesthetic criteria
' that are never fed into the CP-SAT model itself. Lower Total = better.
Imports System.Text.Json.Nodes

''' <summary>One schedule's quality breakdown - every sub-score is exposed
''' individually (not just the weighted Total) so a caller can show why one
''' candidate outranks another, matching this codebase's general
''' traceability preference (see KannViolationDetail/KannConstraintFlag).</summary>
Public NotInheritable Class QualityScore
    Public Property KannViolationCount As Integer
    Public Property ClassGapCount As Integer
    Public Property TeacherGapCount As Integer
    Public Property EdgePeriodCount As Integer
    Public Property AfternoonDayCount As Integer
    Public Property ClassLoadVariance As Double
    Public Property TeacherLoadVariance As Double
    ''' <summary>Code-Review-Umsetzung (P1): Anzahl UNBELEGTER Slots ueber
    ''' alle `occupied_window`-Constraints (jeder Prioritaet - bei must
    ''' garantiert der Solver ohnehin 0, bei should ist dies das echte,
    ''' nachgelagert gezaehlte Dichte-Defizit). 0, wenn das Szenario keine
    ''' occupied_window-Constraints enthaelt.</summary>
    Public Property OccupiedDensityCount As Integer
    ''' <summary>Rhythmisierung: Anzahl Unterrichtsstunden, die AUSSERHALB
    ''' des erlaubten Bereichs (days x from_period..to_period) eines
    ''' `subject_period_window`-Constraints ihres (Klasse,Fach)-Paars
    ''' liegen - ueber alle solchen Constraints summiert, unabhaengig von
    ''' der Prioritaet (bei must garantiert der Solver 0, bei should ist
    ''' dies der nachgelagert exakt gezaehlte Fenster-Verstoss). 0, wenn
    ''' das Szenario keine subject_period_window-Constraints enthaelt.</summary>
    Public Property SubjectWindowCount As Integer
    Public Property Total As Double
End Class

''' <summary>Phase 2.24: the seven weights ScheduleQuality.Score and
''' SolveTopObjective.ApplyQualityObjective combine into a single ranking/
''' CP-SAT objective, pulled out of hardcoded module constants into an
''' overridable object - lets SchoolTestRunner's config.yaml tune them per
''' school without any code change. Every property defaults to the
''' module's own Weight*-constants below, so `New QualityWeights()`
''' reproduces today's behavior byte-identically (every existing school
''' without a `quality_weights` section in its config.yaml keeps exactly
''' the same ranking/objective as before this class existed).
''' Note: SolveTopObjective.ApplyQualityObjective feeds these into a
''' CP-SAT objective via CLng(...) (integer coefficients only) - a
''' fractional weight there gets rounded to the nearest whole number for
''' the actual SEARCH, while ScheduleQuality.Score's post-hoc Quality.Total
''' display uses the exact (unrounded) Double value.</summary>
Public NotInheritable Class QualityWeights
    Public Property Kann As Double = ScheduleQuality.WeightKann
    Public Property ClassGaps As Double = ScheduleQuality.WeightClassGaps
    Public Property TeacherGaps As Double = ScheduleQuality.WeightTeacherGaps
    Public Property EdgePeriod As Double = ScheduleQuality.WeightEdgePeriod
    Public Property AfternoonDayCount As Double = ScheduleQuality.WeightAfternoonDayCount
    Public Property ClassLoadVariance As Double = ScheduleQuality.WeightClassLoadVariance
    Public Property TeacherLoadVariance As Double = ScheduleQuality.WeightTeacherLoadVariance
    ''' <summary>P1: Gewicht pro unbelegtem `occupied_window`-Slot (siehe
    ''' QualityScore.OccupiedDensityCount) - Default bewusst im selben
    ''' "mildly disruptive"-Tier wie EdgePeriod/AfternoonDayCount; eine
    ''' Schule, die Vormittagsdichte hart priorisieren will, hebt es per
    ''' config.yaml an (das bw-grundschule-beispiel nutzt 100, den
    ''' frueheren Kann-Gewichtswert seiner abgeloesten
    ''' occupied_slot-Batterie).</summary>
    Public Property OccupiedDensity As Double = ScheduleQuality.WeightOccupiedDensity
    ''' <summary>Rhythmisierung: Gewicht pro Unterrichtsstunde ausserhalb
    ''' ihres `subject_period_window`-Bereichs (siehe QualityScore.
    ''' SubjectWindowCount) - Default im selben "mildly disruptive"-Tier
    ''' wie EdgePeriod/OccupiedDensity; eine Schule, die z.B. "Kernfaecher
    ''' vormittags" hart priorisieren will, hebt es per config.yaml an
    ''' oder nutzt die dedizierte Lex-Stufe (lex_subject_window_stage).</summary>
    Public Property SubjectWindow As Double = ScheduleQuality.WeightSubjectWindow
    ''' <summary>Phase 2.25-Nachtrag-2: whether SolveTopObjective.
    ''' ApplyQualityObjective builds TeacherGaps' auxiliary variables/
    ''' constraints into the CP-SAT model at all - a structural on/off
    ''' switch, not just a weight of 0 (setting TeacherGaps=0 alone would
    ''' still build the now-cheap-but-nonzero auxiliary construction; this
    ''' flag skips building it entirely, for schools where even the fixed
    ''' Phase-2.25-Nachtrag-2 encoding remains too costly at their scale).
    ''' Default True - unchanged behavior for every existing school without
    ''' an explicit override. Does NOT affect ScheduleQuality.Score's own
    ''' independent, always-computed TeacherGapCount (display/ranking
    ''' still sees the true count regardless of whether the solver
    ''' searched for it).</summary>
    Public Property IncludeTeacherGaps As Boolean = True
    ''' <summary>Code-Review-Umsetzung (R3): dasselbe strukturelle An/Aus-
    ''' Muster wie IncludeTeacherGaps, jetzt auch fuer ClassGaps - vorher
    ''' das einzige Sekundaerkriterium OHNE Flag (es wurde immer gebaut).
    ''' Default True - unveraendertes Verhalten fuer jede bestehende
    ''' Schule ohne explizites Override. Beeinflusst wie alle Include*-
    ''' Flags NICHT ScheduleQuality.Scores immer berechneten
    ''' ClassGapCount.</summary>
    Public Property IncludeClassGaps As Boolean = True
    ''' <summary>Same structural on/off pattern as IncludeTeacherGaps above,
    ''' extended to the remaining four secondary criteria (requested to let
    ''' a school opt out of ALL of them, e.g. when only Kann/ClassGaps/
    ''' TeacherGaps matter and the extra CP-SAT variables for these are not
    ''' worth their cost at that school's scale). Each Default True -
    ''' unchanged behavior for every existing school without an explicit
    ''' override. None of these affect ScheduleQuality.Score's own
    ''' independent, always-computed counts (display/ranking still sees
    ''' the true values regardless of whether the solver searched for
    ''' them).</summary>
    Public Property IncludeEdgePeriod As Boolean = True
    Public Property IncludeAfternoonDayCount As Boolean = True
    Public Property IncludeClassLoadVariance As Boolean = True
    Public Property IncludeTeacherLoadVariance As Boolean = True
    ''' <summary>P1: strukturelles An/Aus fuer den OccupiedDensity-Term in
    ''' SolveTops Zielfunktion (der Term selbst erzeugt KEINE neuen
    ''' Variablen - er ist eine reine Linearsumme ueber das ohnehin
    ''' gebaute occupied-Scaffolding - das Flag existiert fuer Symmetrie
    ''' zu den uebrigen Kriterien und um die Suche gezielt blind zu
    ''' schalten). Beeinflusst nie den immer berechneten
    ''' OccupiedDensityCount der Anzeige.</summary>
    Public Property IncludeOccupiedDensity As Boolean = True
    ''' <summary>Strukturelles An/Aus fuer den SubjectWindow-Term in
    ''' SolveTops Zielfunktion (wie IncludeOccupiedDensity erzeugt der
    ''' Term selbst KEINE neuen Variablen - er ist eine reine Linearsumme
    ''' ueber die ohnehin existierenden Lesson-Variablen; das Flag
    ''' existiert fuer Symmetrie und um die Suche gezielt blind zu
    ''' schalten). Beeinflusst nie den immer berechneten
    ''' SubjectWindowCount der Anzeige.</summary>
    Public Property IncludeSubjectWindow As Boolean = True
End Class

Public Module ScheduleQuality

    ' Kann-Verstoesse dominieren die uebrigen Sekundaerkriterien
    ' (EdgePeriod/AfternoonDayCount/LoadVariance), aber NICHT mehr per
    ' Brechstange (verworfene urspruengliche Heuristik "100000, damit
    ' nichts es je aufwiegen kann").
    Public Const WeightKann As Double = 100.0

    ' Phase 2.25-Nachtrag-2: einheitliches Gewicht mit WeightKann (frueher
    ' bewusst hoeher gewichtet, WeightClassGaps=1000 - siehe
    ' docs/phase2-25-stagnation-heuristik.md, Nachtrag 2, fuer die volle
    ' Historie). Live-Experimente gegen die reale bw-grundschule-beispiel-
    ' Fixture zeigten: das Gewicht selbst war nie das eigentliche Problem
    ' (Springstunden bei Klassen loesten bei JEDEM getesteten Gewicht,
    ' 1 bis 10000, in Sekunden bis zum bewiesenen Optimum) - die
    ' eigentliche Bound-Proving-Schwaeche kam von TeacherGaps' fruehere
    ' Sentinel/Min-Max-Kodierung (siehe SolveTopObjective.BuildGapFlags'
    ' Dokumentation), nicht von einem der beiden Gewichte. Die
    ' Vereinheitlichung auf WeightKann ist eine bewusste, bei dieser
    ' Gelegenheit getroffene Nutzerentscheidung, keine technische
    ' Notwendigkeit - sie hebt die vorherige explizite Prioritaet
    ' "ClassGaps > Kann" auf "ClassGaps == Kann" auf.
    Public Const WeightClassGaps As Double = 100.0
    Public Const WeightTeacherGaps As Double = 100.0

    ' Randstunden-Vermeidung: mildly disruptive, weighted below gaps.
    Public Const WeightEdgePeriod As Double = 5.0

    ' Nachmittags-TAGE (nicht -Stunden) minimieren: a distinct criterion
    ' from WeightEdgePeriod above - EdgePeriodCount counts individual
    ' afternoon LESSON OCCURRENCES (indifferent to whether they land on 1
    ' day or spread across many), this counts DAYS that contain at least
    ' one afternoon lesson (indifferent to how many). A schedule with 4
    ' afternoon lessons bunched on 1 day scores the same EdgePeriodCount
    ' as one with 1 afternoon lesson on each of 4 days, but very different
    ' AfternoonDayCount (1 vs 4) - concentrating afternoon teaching onto
    ' as few days as possible is a real, distinct scheduling preference
    ' (fewer "long days" for a class) this project didn't previously have
    ' a criterion for. Same tier as WeightEdgePeriod (a related, similarly
    ' "mildly disruptive" concern) - class-scoped only, per the same
    ' rationale ScheduleQuality's other class-only choices already follow
    ' (this is a students'/parents' concern per class, not something a
    ' teacher's OWN schedule needs a symmetric criterion for).
    Public Const WeightAfternoonDayCount As Double = 5.0

    ' Even daily load: a nice-to-have smoothing preference, weakest weight.
    Public Const WeightClassLoadVariance As Double = 3.0
    Public Const WeightTeacherLoadVariance As Double = 3.0

    ' P1: Vormittags-/Fensterdichte - Kosten pro unbelegtem Slot innerhalb
    ' eines occupied_window-Constraints. Gleicher Tier wie EdgePeriod/
    ' AfternoonDayCount (eine Komfort-Praeferenz); Schulen mit harter
    ' Dichte-Anforderung heben das Gewicht per config.yaml an.
    Public Const WeightOccupiedDensity As Double = 5.0

    ' Rhythmisierung: Kosten pro Unterrichtsstunde ausserhalb des
    ' erlaubten Bereichs eines should-subject_period_window-Constraints
    ' ("Kernfach bevorzugt vormittags", "AG bevorzugt Mo-Do nachmittags").
    ' Gleicher Tier wie EdgePeriod/OccupiedDensity - eine Praeferenz, kein
    ' hartes Verbot (das waere priority=must).
    Public Const WeightSubjectWindow As Double = 5.0

    ''' <summary>Periods &gt;= this count as "Nachmittag" for the
    ''' Randstunden metric - matches the convention already used in this
    ''' project's own fixture prompts (Gymnasium/MussKann explicitly call
    ''' period 7 "Nachmittagsunterricht"). Period 1 always counts too.</summary>
    Public Const AfternoonThresholdPeriod As Integer = 7

    ''' <summary>Scores one candidate schedule. `kannViolationCount` is
    ''' supplied by the caller rather than computed here (SolveTop sources
    ''' it from Verifier.VerifyScheduleDetailed, independently re-derived
    ''' from the schedule rather than trusted off the solver's own
    ''' KannConstraintFlags - same "don't trust the solver" philosophy as
    ''' Verifier.vb's header comment). `weights` defaults (Nothing) to the
    ''' module's own Weight*-constants via `New QualityWeights()` - see
    ''' that class's doc comment for the backward-compatibility
    ''' guarantee.</summary>
    Public Function Score(data As JsonObject, schedule As List(Of ScheduleEntry), kannViolationCount As Integer,
                           Optional weights As QualityWeights = Nothing) As QualityScore
        Dim w = If(weights, New QualityWeights())
        Dim ent = JsonHelpers.Entities(data)
        Dim timeslots = JsonHelpers.Timeslots(ent)
        Dim allDays = JsonHelpers.AsStringList(timeslots, "days")

        Dim classGaps = GapsOverEntities(schedule.GroupBy(Function(l) l.ClassName))
        Dim teacherGaps = GapsOverEntities(schedule.GroupBy(Function(l) l.Teacher))

        Dim edgeCount = schedule.Where(Function(l) l.Period = 1 OrElse l.Period >= AfternoonThresholdPeriod).Count()
        Dim afternoonDayCount = AfternoonDaysOverEntities(schedule.GroupBy(Function(l) l.ClassName))

        Dim classVariance = LoadVarianceOverAllDays(schedule.GroupBy(Function(l) l.ClassName), allDays)
        Dim teacherVariance = LoadVarianceOverWorkingDaysOnly(schedule.GroupBy(Function(l) l.Teacher))

        Dim occupiedDensity = OccupiedWindowDeficit(data, schedule, allDays)
        Dim subjectWindow = SubjectWindowOutsideCount(data, schedule, allDays)

        Dim total = w.Kann * kannViolationCount +
                    w.ClassGaps * classGaps + w.TeacherGaps * teacherGaps +
                    w.EdgePeriod * edgeCount + w.AfternoonDayCount * afternoonDayCount +
                    w.ClassLoadVariance * classVariance + w.TeacherLoadVariance * teacherVariance +
                    w.OccupiedDensity * occupiedDensity +
                    w.SubjectWindow * subjectWindow

        Return New QualityScore With {
            .KannViolationCount = kannViolationCount, .ClassGapCount = classGaps, .TeacherGapCount = teacherGaps,
            .EdgePeriodCount = edgeCount, .AfternoonDayCount = afternoonDayCount,
            .ClassLoadVariance = classVariance, .TeacherLoadVariance = teacherVariance,
            .OccupiedDensityCount = occupiedDensity, .SubjectWindowCount = subjectWindow,
            .Total = total
        }
    End Function

    ''' <summary>Rhythmisierung: zaehlt ueber alle `subject_period_window`-
    ''' Constraints in `data` die Unterrichtsstunden des jeweiligen
    ''' (Klasse,Fach)-Paars, die AUSSERHALB des erlaubten Bereichs (days x
    ''' from_period..to_period) liegen - unabhaengig von der Prioritaet
    ''' (bei must garantiert der Solver 0, bei should ist dies der
    ''' nachgelagert exakt gezaehlte Fenster-Verstoss). Ein Tag, der nicht
    ''' in `days` steht, liegt VOLLSTAENDIG ausserhalb (der erlaubte
    ''' Bereich ist das Kreuzprodukt, nicht nur die Periodengrenze).
    ''' Unabhaengig re-deriviert aus Schedule + JSON, teilt keinen Code
    ''' mit SolveTopObjectives In-Modell-Term (gleiche Philosophie wie
    ''' Verifier.vb).</summary>
    Private Function SubjectWindowOutsideCount(data As JsonObject, schedule As List(Of ScheduleEntry), allDays As List(Of String)) As Integer
        Dim outside = 0
        For Each c In JsonHelpers.Constraints(data)
            If JsonHelpers.GetString(c, "type") <> "subject_period_window" Then Continue For
            Dim className = JsonHelpers.GetString(c, "class")
            Dim subject = JsonHelpers.GetString(c, "subject")
            Dim fromPeriod = JsonHelpers.GetInt(c, "from_period")
            Dim toPeriod = JsonHelpers.GetInt(c, "to_period")
            If Not fromPeriod.HasValue OrElse Not toPeriod.HasValue Then Continue For
            Dim windowDaysList = JsonHelpers.AsStringList(c, "days")
            Dim windowDays As New HashSet(Of String)(If(windowDaysList.Any(), windowDaysList, allDays))

            ' DISTINCT ueber (Tag, Periode) - eine parallel_group kann
            ' mehrere ScheduleEntry-Zeilen auf denselben Slot legen
            ' (gleiche Falle wie in GapsOverEntities, Phase 2.22).
            outside += schedule.
                Where(Function(l) l.ClassName = className AndAlso l.Subject = subject).
                Select(Function(l) (l.Day, l.Period)).Distinct().
                Count(Function(slot) Not windowDays.Contains(slot.Day) OrElse
                                     slot.Period < fromPeriod.Value OrElse slot.Period > toPeriod.Value)
        Next
        Return outside
    End Function

    ''' <summary>P1: zaehlt ueber alle `occupied_window`-Constraints in
    ''' `data` die (Tag, Periode)-Slots innerhalb des jeweiligen Fensters,
    ''' an denen die Entity KEINEN Unterricht hat - unabhaengig von der
    ''' Prioritaet (bei must garantiert der Solver 0, bei should ist dies
    ''' das nachgelagert exakt gezaehlte Dichte-Defizit). Unabhaengig
    ''' re-deriviert aus Schedule + JSON, teilt keinen Code mit
    ''' SolveTopObjectives In-Modell-Term (gleiche Philosophie wie
    ''' Verifier.vb).</summary>
    Private Function OccupiedWindowDeficit(data As JsonObject, schedule As List(Of ScheduleEntry), allDays As List(Of String)) As Integer
        Dim deficit = 0
        For Each c In JsonHelpers.Constraints(data)
            If JsonHelpers.GetString(c, "type") <> "occupied_window" Then Continue For
            Dim scope = JsonHelpers.GetString(c, "scope")
            Dim entity = JsonHelpers.GetString(c, "entity")
            Dim fromPeriod = JsonHelpers.GetInt(c, "from_period")
            Dim toPeriod = JsonHelpers.GetInt(c, "to_period")
            If Not fromPeriod.HasValue OrElse Not toPeriod.HasValue Then Continue For
            Dim windowDaysList = JsonHelpers.AsStringList(c, "days")
            Dim windowDays = If(windowDaysList.Any(), windowDaysList, allDays)

            Dim occupiedSlots As HashSet(Of (Day As String, Period As Integer))
            Select Case scope
                Case "class"
                    occupiedSlots = New HashSet(Of (Day As String, Period As Integer))(
                        schedule.Where(Function(l) l.ClassName = entity).Select(Function(l) (l.Day, l.Period)))
                Case "teacher"
                    occupiedSlots = New HashSet(Of (Day As String, Period As Integer))(
                        schedule.Where(Function(l) l.Teacher = entity).Select(Function(l) (l.Day, l.Period)))
                Case Else
                    Continue For
            End Select

            For Each d In windowDays
                For p = fromPeriod.Value To toPeriod.Value
                    If Not occupiedSlots.Contains((d, p)) Then deficit += 1
                Next
            Next
        Next
        Return deficit
    End Function

    ''' <summary>Sum, over every entity (class), of the number of DISTINCT
    ''' days containing at least one lesson in an afternoon period
    ''' (Period &gt;= AfternoonThresholdPeriod) - see WeightAfternoonDayCount's
    ''' comment for why this differs from EdgePeriodCount (occurrences,
    ''' not days). Period=1 does NOT count here, unlike EdgePeriodCount -
    ''' this metric is specifically about "Nachmittag" (afternoon) days,
    ''' not "Randstunden" (edge periods) in general.</summary>
    Private Function AfternoonDaysOverEntities(byEntity As IEnumerable(Of IGrouping(Of String, ScheduleEntry))) As Integer
        Dim total = 0
        For Each entityGroup In byEntity
            total += entityGroup.Where(Function(l) l.Period >= AfternoonThresholdPeriod).
                                  Select(Function(l) l.Day).Distinct().Count()
        Next
        Return total
    End Function

    ''' <summary>Sum, over every (entity, day) group with >=1 lesson, of
    ''' the free periods trapped between the first and last occupied period
    ''' that day ("Springstunden"). A day with no lessons contributes
    ''' nothing - there's no first/last occupied period to speak of.
    ''' Phase 2.22 bugfix: periods are DISTINCT-ed before counting - a
    ''' Phase 2.20 "parallel_group" slot (e.g. Religion-ev/Religion-kath/
    ''' Ethik running simultaneously for one class) puts multiple
    ''' ScheduleEntry rows on the SAME period for that class; counting raw
    ''' rows there over-counted "occupied periods" past the day's actual
    ''' span, making span - periods.Count go negative (discovered live: a
    ''' real bw-grundschule-beispiel run showed Quality.Total = -513,
    ''' impossible under this function's all-non-negative-terms design -
    ''' confirmed the cause by cross-checking against Solver.SolveTop's
    ''' own, unaffected in-model objective, which correctly treats a
    ''' parallel_group slot as a single occupied period via a reified
    ''' BoolVar rather than a raw row count).</summary>
    Private Function GapsOverEntities(byEntity As IEnumerable(Of IGrouping(Of String, ScheduleEntry))) As Integer
        Dim total = 0
        For Each entityGroup In byEntity
            For Each dayGroup In entityGroup.GroupBy(Function(l) l.Day)
                Dim periods = dayGroup.Select(Function(l) l.Period).Distinct().ToList()
                Dim span = periods.Max() - periods.Min() + 1
                total += span - periods.Count
            Next
        Next
        Return total
    End Function

    ''' <summary>Population variance of periods-per-day, summed over every
    ''' entity, counting EVERY day in `allDays` (including zero-lesson
    ''' days) - a class's full week is fixed, there's no "unavailability"
    ''' concept for classes the way there is for teachers.</summary>
    Private Function LoadVarianceOverAllDays(byEntity As IEnumerable(Of IGrouping(Of String, ScheduleEntry)), allDays As List(Of String)) As Double
        Dim total = 0.0
        For Each entityGroup In byEntity
            Dim counts = allDays.Select(Function(d) entityGroup.Count(Function(l) l.Day = d)).ToList()
            total += PopulationVariance(counts)
        Next
        Return total
    End Function

    ''' <summary>Population variance of periods-per-day, summed over every
    ''' entity, counting ONLY the days that entity actually appears on in
    ''' the schedule ("working days") - so a teacher's declared part-time
    ''' unavailability is never miscounted as "imbalance". A teacher with
    ''' exactly 1 working day yields variance 0 by construction.</summary>
    Private Function LoadVarianceOverWorkingDaysOnly(byEntity As IEnumerable(Of IGrouping(Of String, ScheduleEntry))) As Double
        Dim total = 0.0
        For Each entityGroup In byEntity
            Dim counts = entityGroup.GroupBy(Function(l) l.Day).Select(Function(g) g.Count()).ToList()
            total += PopulationVariance(counts)
        Next
        Return total
    End Function

    Private Function PopulationVariance(counts As List(Of Integer)) As Double
        If counts.Count = 0 Then Return 0.0
        Dim mean = counts.Average()
        Return counts.Select(Function(c) (c - mean) * (c - mean)).Sum() / counts.Count
    End Function

End Module
