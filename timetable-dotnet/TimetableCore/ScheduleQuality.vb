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

        Dim total = w.Kann * kannViolationCount +
                    w.ClassGaps * classGaps + w.TeacherGaps * teacherGaps +
                    w.EdgePeriod * edgeCount + w.AfternoonDayCount * afternoonDayCount +
                    w.ClassLoadVariance * classVariance + w.TeacherLoadVariance * teacherVariance

        Return New QualityScore With {
            .KannViolationCount = kannViolationCount, .ClassGapCount = classGaps, .TeacherGapCount = teacherGaps,
            .EdgePeriodCount = edgeCount, .AfternoonDayCount = afternoonDayCount,
            .ClassLoadVariance = classVariance, .TeacherLoadVariance = teacherVariance,
            .Total = total
        }
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
