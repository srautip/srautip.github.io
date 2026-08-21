' Phase 2.9: builds a CP-SAT objective for Solver.SolveTop that bakes in
' ScheduleQuality.vb's full weighted scoring scheme (Kann-violations plus
' gaps, edge periods, and daily-load balance for classes/teachers)
' directly into the model, instead of letting CP-SAT optimize only
' Kann-violations while the secondary criteria are scored purely post-hoc.
'
' Deliberately separate from Solver.vb (same reasoning as ScheduleQuality.vb
' living apart from Verifier.vb) - keeps this file's CP-SAT-modeling
' concerns out of the already-large Solver.vb.
'
' ScheduleQuality.vb's true per-schedule metrics (real population variance,
' exact gap/edge counts) are NOT replaced - they remain the authoritative,
' independently-recomputed values used for display and for SolveTop's
' final Solutions sort (same "solver proposes, an independent
' re-derivation is the ground truth" relationship Verifier.vb has to
' Solver.vb). The terms built here are a CP-SAT-friendly APPROXIMATION
' (range instead of variance) whose only job is to steer the search
' toward good candidates from the first iteration onward.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat

''' <summary>Code-Review-Umsetzung (P2): die 7 Qualitaetskriterien als
''' EINZELNE, ungewichtete LinearExpr-Summen statt nur als fertige
''' gewichtete Gesamtzielfunktion - Solver.SolveTops lexikografischer
''' Modus optimiert damit Kann/ClassGaps/TeacherGaps als eigene Stufen
''' (Optimum je Stufe als Constraint fixiert), waehrend der klassische
''' gewichtete Modus dieselben Summen unveraendert zu ApplyQualityObjectives
''' bisheriger Zielfunktion kombiniert. Ein Property ist Nothing, wenn das
''' Kriterium strukturell ausgeschlossen wurde (Include*-Flag) oder keine
''' Variablen beitraegt.</summary>
Friend NotInheritable Class QualityTerms
    Public Property KannSum As LinearExpr
    Public Property ClassGapsSum As LinearExpr
    Public Property TeacherGapsSum As LinearExpr
    Public Property EdgeSum As LinearExpr
    Public Property AfternoonSum As LinearExpr
    Public Property ClassRangeSum As LinearExpr
    Public Property TeacherRangeSum As LinearExpr
    ''' <summary>P1: Summe der UNBELEGTEN Slots ueber alle should-
    ''' `occupied_window`-Constraints - eine reine Linearsumme ueber das
    ''' ohnehin gebaute occupied-Scaffolding (keine einzige neue Variable,
    ''' kein reifiziertes Constraint - der entscheidende Unterschied zur
    ''' abgeloesten occupied_slot-Batterie, die pro Slot eine eigene
    ''' Kann-BoolVar samt halbreifizierter Summe erzeugte). Gehoert zur
    ''' Rest-Zielfunktion, nie zu den lexikografischen Stufen.</summary>
    Public Property OccupiedDensitySum As LinearExpr
End Class

Friend Module SolveTopObjective

    Private Function IndexLessonByEntityDayPeriod(lesson As Dictionary(Of LessonKey, BoolVar),
                                                    entityOf As Func(Of LessonKey, String)) _
        As Dictionary(Of (Entity As String, Day As String, Period As Integer), List(Of BoolVar))
        Dim result As New Dictionary(Of (Entity As String, Day As String, Period As Integer), List(Of BoolVar))
        For Each kvp In lesson
            Dim k = (entityOf(kvp.Key), kvp.Key.Day, kvp.Key.Period)
            If Not result.ContainsKey(k) Then result(k) = New List(Of BoolVar)
            result(k).Add(kvp.Value)
        Next
        Return result
    End Function

    ''' <summary>Builds, per (entity, day, period): an `occupied` BoolVar
    ''' reified against the sum of matching Lesson vars; and per (entity,
    ''' day): a `dailyCount` LinearExpr (Sum of that day's occupied vars)
    ''' and - nur bei `buildHasAny` - a `hasAny` BoolVar reified against
    ''' it. Shared scaffolding reused by the gaps/balance/density
    ''' encodings below - built once per entity kind (classes or
    ''' teachers), not duplicated. Code-Review-Umsetzung (P4): `hasAny`
    ''' hat nur EINEN Abnehmer (BuildTeacherRangeVars' Sentinel-Guard) -
    ''' fuer Klassen wurde es frueher immer mitgebaut und nie gelesen
    ''' (eine tote reifizierte BoolVar pro Klasse x Tag), deshalb ist es
    ''' jetzt explizit zuschaltbar statt unbedingt.</summary>
    Private Sub BuildScaffolding(model As CpModel, entities As List(Of String), days As List(Of String), periods As List(Of Integer),
                                  lookup As Dictionary(Of (Entity As String, Day As String, Period As Integer), List(Of BoolVar)), tag As String,
                                  occupied As Dictionary(Of (Entity As String, Day As String, Period As Integer), BoolVar),
                                  hasAny As Dictionary(Of (Entity As String, Day As String), BoolVar),
                                  dailyCount As Dictionary(Of (Entity As String, Day As String), LinearExpr),
                                  buildHasAny As Boolean)
        For Each entity In entities
            For Each d In days
                Dim occList As New List(Of BoolVar)
                For Each p In periods
                    Dim periodSum As LinearExpr = LinearExpr.Sum(lookup((entity, d, p)))
                    Dim occ = model.NewBoolVar($"occ[{tag},{entity},{d},{p}]")
                    model.Add(periodSum >= 1).OnlyEnforceIf(occ)
                    model.Add(periodSum = 0).OnlyEnforceIf(occ.Not())
                    occupied((entity, d, p)) = occ
                    occList.Add(occ)
                Next
                Dim dc As LinearExpr = LinearExpr.Sum(occList)
                dailyCount((entity, d)) = dc
                If buildHasAny Then
                    Dim ha = model.NewBoolVar($"hasAny[{tag},{entity},{d}]")
                    model.Add(dc >= 1).OnlyEnforceIf(ha)
                    model.Add(dc = 0).OnlyEnforceIf(ha.Not())
                    hasAny((entity, d)) = ha
                End If
            Next
        Next
    End Sub

    ''' <summary>Per (entity, day): one Big-M-free BoolVar per INTERIOR
    ''' period that is a "Springstunde" (occupied periods exist both
    ''' strictly before AND strictly after it, but it itself is free) -
    ''' replaces the former sentinel-substitution Min/Max encoding
    ''' (`BuildGapVars`, removed in the Phase-2.25-Nachtrag-2 fix below).
    ''' Edge periods (first/last of the day) can never be a gap by
    ''' construction and get no variable at all.
    '''
    ''' Built from two Big-M-free prefix/suffix-OR chains: `anyBefore(p) =
    ''' OR(occupied(1..p))` for p=1..maxP-1, `anyAfter(p) =
    ''' OR(occupied(p..maxP))` for p=2..maxP (each step a plain
    ''' AddMaxEquality of two BoolVars, no sentinel value ever enters the
    ''' domain). For an interior period p, `before(p) = anyBefore(p-1)`
    ''' (strictly before p) and `after(p) = anyAfter(p+1)` (strictly after
    ''' p); `isGap(p)` is then a direct linear reification of
    ''' `Not(occupied(p)) And before(p) And after(p)` - no AddMinEquality/
    ''' AddMaxEquality/sentinel anywhere in the final gap variable itself.
    ''' `hasAny` is not needed either: on an empty day every `anyBefore`/
    ''' `anyAfter` is false, so every `isGap` is structurally false too.
    '''
    ''' Empirically motivated (Phase 2.25-Nachtrag-2, live against the real
    ''' `bw-grundschule-beispiel` fixture): the OLD `BuildGapVars` for
    ''' TEACHERS alone (isolated, `Kann+TeacherGaps` only, original modest
    ''' weight 10) left `BestObjectiveBound` stuck at 0 for the FULL 300s
    ''' budget - the encoding, not the weight, was the problem (the same
    ''' isolated test for CLASSES solved to a proven optimum in ~5s at
    ''' ANY tested weight, including the much larger 1000). This THIS
    ''' encoding fixed it: the same Kann+ClassGaps+TeacherGaps combination
    ''' solved to a proven optimum in ~12s. A first attempt at a "sentinel-
    ''' free" fix during Phase 2.25c that kept AddMinEquality/AddMaxEquality
    ''' (just without a literal Big-M value) did NOT help - it is
    ''' specifically the Min/Max operators themselves, not merely large
    ''' sentinel constants, that weaken the LP relaxation here.</summary>
    Private Sub BuildGapFlags(model As CpModel, entities As List(Of String), days As List(Of String), periods As List(Of Integer), tag As String,
                               occupied As Dictionary(Of (Entity As String, Day As String, Period As Integer), BoolVar),
                               gapFlags As List(Of BoolVar))
        Dim maxP = periods.Count
        If maxP < 3 Then Return ' no interior period possible with <=2 periods/day
        For Each entity In entities
            For Each d In days
                Dim occList = periods.Select(Function(p) occupied((entity, d, p))).ToList()

                Dim anyBefore(maxP - 1) As BoolVar ' index p-1, defined for p=1..maxP-1
                For p = 1 To maxP - 1
                    Dim v = model.NewBoolVar($"anyBefore[{tag},{entity},{d},{p}]")
                    If p = 1 Then
                        model.Add(v = occList(0))
                    Else
                        model.AddMaxEquality(v, {CType(anyBefore(p - 2), LinearExpr), CType(occList(p - 1), LinearExpr)})
                    End If
                    anyBefore(p - 1) = v
                Next

                Dim anyAfter(maxP - 1) As BoolVar ' index p-2, defined for p=2..maxP
                For p = maxP To 2 Step -1
                    Dim v = model.NewBoolVar($"anyAfter[{tag},{entity},{d},{p}]")
                    If p = maxP Then
                        model.Add(v = occList(maxP - 1))
                    Else
                        model.AddMaxEquality(v, {CType(anyAfter(p - 1), LinearExpr), CType(occList(p - 1), LinearExpr)})
                    End If
                    anyAfter(p - 2) = v
                Next

                For p = 2 To maxP - 1
                    Dim beforeP = anyBefore(p - 2) ' anyBefore(p-1)
                    Dim afterP = anyAfter(p - 1)   ' anyAfter(p+1)
                    Dim occP = occList(p - 1)
                    Dim isGap = model.NewBoolVar($"isGap[{tag},{entity},{d},{p}]")
                    model.Add(isGap <= 1L - occP)
                    model.Add(isGap <= beforeP)
                    model.Add(isGap <= afterP)
                    model.Add(isGap >= beforeP + afterP + (1L - occP) - 2L)
                    gapFlags.Add(isGap)
                Next
            Next
        Next
    End Sub

    ''' <summary>Per entity, per day: a `hasAfternoon` BoolVar reified
    ''' against the sum of `occupied` vars for periods &gt;=
    ''' AfternoonThresholdPeriod that day, being &gt;= 1. Reuses the SAME
    ''' `occupied` scaffolding BuildScaffolding already built (no new
    ''' per-period variables) - just a different sum over a subset of
    ''' periods, same reification pattern as `hasAny` in BuildScaffolding
    ''' itself. Called class-scoped only (see ScheduleQuality.
    ''' WeightAfternoonDayCount's comment on why this has no teacher
    ''' counterpart, unlike gaps/range above).</summary>
    Private Sub BuildAfternoonDayVars(model As CpModel, entities As List(Of String), days As List(Of String), periods As List(Of Integer),
                                       occupied As Dictionary(Of (Entity As String, Day As String, Period As Integer), BoolVar),
                                       afternoonDayVars As List(Of BoolVar))
        Dim afternoonPeriods = periods.Where(Function(p) p >= ScheduleQuality.AfternoonThresholdPeriod).ToList()
        If afternoonPeriods.Count = 0 Then Return
        For Each entity In entities
            For Each d In days
                Dim afternoonSum As LinearExpr = LinearExpr.Sum(afternoonPeriods.Select(Function(p) occupied((entity, d, p))))
                Dim hasAfternoon = model.NewBoolVar($"hasAfternoon[{entity},{d}]")
                model.Add(afternoonSum >= 1).OnlyEnforceIf(hasAfternoon)
                model.Add(afternoonSum = 0).OnlyEnforceIf(hasAfternoon.Not())
                afternoonDayVars.Add(hasAfternoon)
            Next
        Next
    End Sub

    ''' <summary>Per class: Max-Min of the daily lesson count over ALL
    ''' days (0 is a legitimate value on every day for a class - no
    ''' unavailability concept - so no sentinel is needed).</summary>
    Private Sub BuildClassRangeVars(model As CpModel, classNames As List(Of String), days As List(Of String), periods As List(Of Integer),
                                     dailyCount As Dictionary(Of (Entity As String, Day As String), LinearExpr),
                                     rangeVars As List(Of IntVar))
        For Each cls In classNames
            Dim counts = days.Select(Function(d) dailyCount((cls, d))).ToList()
            Dim mx = model.NewIntVar(0L, CLng(periods.Count), $"classMax[{cls}]")
            model.AddMaxEquality(mx, counts)
            Dim mn = model.NewIntVar(0L, CLng(periods.Count), $"classMin[{cls}]")
            model.AddMinEquality(mn, counts)
            Dim rng = model.NewIntVar(0L, CLng(periods.Count), $"classRange[{cls}]")
            model.Add(rng = mx - mn)
            rangeVars.Add(rng)
        Next
    End Sub

    ''' <summary>Per teacher: Max-Min of the daily lesson count, but only
    ''' over that teacher's actual WORKING days - preserves the same
    ''' semantic as ScheduleQuality.LoadVarianceOverWorkingDaysOnly (see
    ''' its dedicated regression test), so a part-time teacher's declared
    ''' unavailability is never miscounted as "imbalance". Only the MIN
    ''' side needs the sentinel substitution (idle days have count 0,
    ''' which would otherwise drag the minimum down) - the MAX side is
    ''' safe to compute directly over all days (a 0-count idle day never
    ''' exceeds a real working day's count).</summary>
    Private Sub BuildTeacherRangeVars(model As CpModel, teacherNames As List(Of String), days As List(Of String), periods As List(Of Integer),
                                       lesson As Dictionary(Of LessonKey, BoolVar),
                                       dailyCount As Dictionary(Of (Entity As String, Day As String), LinearExpr),
                                       hasAny As Dictionary(Of (Entity As String, Day As String), BoolVar),
                                       rangeVars As List(Of IntVar))
        Dim bigCount = CLng(periods.Count) + 1L
        For Each teacher In teacherNames
            Dim counts = days.Select(Function(d) dailyCount((teacher, d))).ToList()
            Dim mx = model.NewIntVar(0L, CLng(periods.Count), $"teacherMax[{teacher}]")
            model.AddMaxEquality(mx, counts)

            Dim minCandList As New List(Of LinearExpr)
            For Each d In days
                Dim ha = hasAny((teacher, d))
                Dim minCand = model.NewIntVar(0L, bigCount, $"teacherMinCand[{teacher},{d}]")
                model.Add(minCand = dailyCount((teacher, d))).OnlyEnforceIf(ha)
                model.Add(minCand = bigCount).OnlyEnforceIf(ha.Not())
                minCandList.Add(minCand)
            Next
            Dim mn = model.NewIntVar(0L, bigCount, $"teacherMin[{teacher}]")
            model.AddMinEquality(mn, minCandList)

            ' Guards the (in practice unreachable, since teacherNames is
            ' derived from built.Sessions) edge case of a teacher with zero
            ' scheduled sessions at all - cheap insurance against a
            ' meaningless Min-over-all-sentinels range.
            Dim weeklyTotal As LinearExpr = LinearExpr.Sum(
                lesson.Where(Function(kv) kv.Key.Teacher = teacher).Select(Function(kv) kv.Value))
            Dim worksAtAll = model.NewBoolVar($"teacherWorksAtAll[{teacher}]")
            model.Add(weeklyTotal >= 1).OnlyEnforceIf(worksAtAll)
            model.Add(weeklyTotal = 0).OnlyEnforceIf(worksAtAll.Not())

            Dim rng = model.NewIntVar(0L, CLng(periods.Count), $"teacherRange[{teacher}]")
            model.Add(rng = mx - mn).OnlyEnforceIf(worksAtAll)
            model.Add(rng = 0L).OnlyEnforceIf(worksAtAll.Not())
            rangeVars.Add(rng)
        Next
    End Sub

    ''' <summary>Adds a Minimize objective to `built.Model` combining all 7
    ''' ScheduleQuality criteria (Kann-violations, class/teacher gaps, edge
    ''' periods, afternoon-day count, and class/teacher load balance), using
    ''' the same weights ScheduleQuality.vb uses to score candidates
    ''' afterward - so the search itself is now steered toward what
    ''' SolveTop's final ranking already valued. `weights` defaults
    ''' (Nothing) to `New ScheduleQuality.QualityWeights()` (today's
    ''' hardcoded constants) - see that class's doc comment for the
    ''' backward-compatibility guarantee. Each of the 5 secondary criteria
    ''' (everything but Kann - seit der Review-Umsetzung inklusive
    ''' ClassGaps, siehe R3) can be structurally excluded from
    ''' the model via its own QualityWeights.Include*=False flag - the
    ''' corresponding auxiliary variables are never built at all (not just
    ''' weighted 0), for schools where the extra model size is not worth
    ''' the criterion at their scale. Called once by SolveTop, on a
    ''' BuiltModel from BuildCoreModel (NOT BuildModel - this replaces,
    ''' rather than adds to, BuildModel's own Kann-only Minimize).</summary>
    Friend Sub ApplyQualityObjective(built As BuiltModel, data As JsonObject, Optional weights As QualityWeights = Nothing)
        Dim w = If(weights, New QualityWeights())
        Dim terms = BuildQualityTerms(built, data, w)
        Dim total = WeightedTotal(terms, w)
        If total IsNot Nothing Then built.Model.Minimize(total)
    End Sub

    ''' <summary>Baut alle per Include*-Flag aktiven Hilfsvariablen/
    ''' -Constraints der Qualitaetskriterien ins Modell (identische
    ''' Variablen-Erzeugungsreihenfolge wie ApplyQualityObjective vor der
    ''' Review-Umsetzung - CP-SATs random_seed-Verhalten ist darauf
    ''' sensibel) und liefert die UNGEWICHTETEN Summen je Kriterium
    ''' zurueck. Setzt selbst KEINE Zielfunktion - das entscheidet der
    ''' Aufrufer (gewichtete Summe via WeightedTotal, oder Solver.SolveTops
    ''' lexikografische Stufen).</summary>
    Friend Function BuildQualityTerms(built As BuiltModel, data As JsonObject, weights As QualityWeights) As QualityTerms
        Dim w = If(weights, New QualityWeights())
        Dim model = built.Model
        Dim classNames = built.Sessions.Select(Function(s) s.ClassName).Distinct().ToList()
        Dim teacherNames = built.Sessions.Select(Function(s) s.Teacher).Distinct().ToList()

        ' P4: Scaffolding nur noch nachfragegesteuert bauen - vorher wurden
        ' occupied/dailyCount/hasAny fuer Klassen UND Lehrer immer
        ' vollstaendig erzeugt, auch wenn saemtliche Abnehmer per
        ' Include*-Flag abgeschaltet waren (tote reifizierte Variablen,
        ' die nur Presolve-Zeit kosteten). Abnehmer je Seite:
        ' Klassen: ClassGaps, AfternoonDayCount, ClassLoadVariance,
        '          occupied_window(scope=class);
        ' Lehrer:  TeacherGaps, TeacherLoadVariance,
        '          occupied_window(scope=teacher).
        Dim shouldWindows As New List(Of JsonObject)
        If w.IncludeOccupiedDensity Then
            shouldWindows = JsonHelpers.Constraints(data).
                Where(Function(c) JsonHelpers.GetString(c, "type") = "occupied_window" AndAlso
                                  JsonHelpers.GetPriority(c) = JsonHelpers.PriorityShould).ToList()
        End If
        Dim hasClassWindows = shouldWindows.Any(Function(c) JsonHelpers.GetString(c, "scope") <> "teacher")
        Dim hasTeacherWindows = shouldWindows.Any(Function(c) JsonHelpers.GetString(c, "scope") = "teacher")
        Dim needClassScaffolding = w.IncludeClassGaps OrElse w.IncludeAfternoonDayCount OrElse
                                   w.IncludeClassLoadVariance OrElse hasClassWindows
        Dim needTeacherScaffolding = w.IncludeTeacherGaps OrElse w.IncludeTeacherLoadVariance OrElse hasTeacherWindows

        Dim occupiedClass As New Dictionary(Of (Entity As String, Day As String, Period As Integer), BoolVar)
        Dim dailyCountClass As New Dictionary(Of (Entity As String, Day As String), LinearExpr)
        If needClassScaffolding Then
            ' buildHasAny:=False - hasAny hatte klassenseitig nie einen
            ' Abnehmer (P4-Befund), die Variablen entfallen ersatzlos.
            BuildScaffolding(model, classNames, built.Days, built.Periods,
                             IndexLessonByEntityDayPeriod(built.Lesson, Function(k) k.ClassName), "C",
                             occupiedClass, Nothing, dailyCountClass, buildHasAny:=False)
        End If

        Dim occupiedTeacher As New Dictionary(Of (Entity As String, Day As String, Period As Integer), BoolVar)
        Dim hasAnyTeacher As New Dictionary(Of (Entity As String, Day As String), BoolVar)
        Dim dailyCountTeacher As New Dictionary(Of (Entity As String, Day As String), LinearExpr)
        If needTeacherScaffolding Then
            ' hasAny nur, wenn sein einziger Abnehmer (BuildTeacherRangeVars)
            ' ueberhaupt gebaut wird.
            BuildScaffolding(model, teacherNames, built.Days, built.Periods,
                             IndexLessonByEntityDayPeriod(built.Lesson, Function(k) k.Teacher), "T",
                             occupiedTeacher, hasAnyTeacher, dailyCountTeacher, buildHasAny:=w.IncludeTeacherLoadVariance)
        End If

        Dim classGapFlags As New List(Of BoolVar)
        If w.IncludeClassGaps Then
            BuildGapFlags(model, classNames, built.Days, built.Periods, "C", occupiedClass, classGapFlags)
        End If
        Dim teacherGapFlags As New List(Of BoolVar)
        If w.IncludeTeacherGaps Then
            BuildGapFlags(model, teacherNames, built.Days, built.Periods, "T", occupiedTeacher, teacherGapFlags)
        End If

        Dim classAfternoonDayVars As New List(Of BoolVar)
        If w.IncludeAfternoonDayCount Then
            BuildAfternoonDayVars(model, classNames, built.Days, built.Periods, occupiedClass, classAfternoonDayVars)
        End If

        Dim classRangeVars As New List(Of IntVar)
        If w.IncludeClassLoadVariance Then
            BuildClassRangeVars(model, classNames, built.Days, built.Periods, dailyCountClass, classRangeVars)
        End If

        Dim teacherRangeVars As New List(Of IntVar)
        If w.IncludeTeacherLoadVariance Then
            BuildTeacherRangeVars(model, teacherNames, built.Days, built.Periods, built.Lesson, dailyCountTeacher, hasAnyTeacher, teacherRangeVars)
        End If

        ' P1: should-occupied_window -> Dichte-Term ueber die bereits
        ' reifizierten occupied-Variablen. must-Fenster sind harte
        ' Constraints (Solver.ApplyOccupiedWindow) und tauchen hier nicht
        ' auf. Ein unbekannter (Entity, Tag, Periode)-Schluessel wird
        ' still uebersprungen - Validation.ValidateEntities hat solche
        ' Fenster bereits als Fehler abgewiesen, bevor ein Solve startet.
        Dim occupiedDensityParts As New List(Of LinearExpr)
        If w.IncludeOccupiedDensity Then
            For Each c In shouldWindows
                Dim scope = JsonHelpers.GetString(c, "scope")
                Dim entity = JsonHelpers.GetString(c, "entity")
                Dim fromPeriod = JsonHelpers.GetInt(c, "from_period")
                Dim toPeriod = JsonHelpers.GetInt(c, "to_period")
                If Not fromPeriod.HasValue OrElse Not toPeriod.HasValue Then Continue For
                Dim windowDaysList = JsonHelpers.AsStringList(c, "days")
                Dim windowDays = If(windowDaysList.Any(), windowDaysList, built.Days)
                Dim occLookup = If(scope = "teacher", occupiedTeacher, occupiedClass)
                For Each d In windowDays
                    For p = fromPeriod.Value To toPeriod.Value
                        Dim occ As BoolVar = Nothing
                        If occLookup.TryGetValue((entity, d, p), occ) Then
                            occupiedDensityParts.Add(1L - occ)
                        End If
                    Next
                Next
            Next
        End If

        Dim result As New QualityTerms()
        If built.KannVars.Count > 0 Then
            result.KannSum = LinearExpr.Sum(built.KannVars.Values.Select(Function(kv) kv.Var))
        End If
        If classGapFlags.Count > 0 Then result.ClassGapsSum = LinearExpr.Sum(classGapFlags)
        If teacherGapFlags.Count > 0 Then result.TeacherGapsSum = LinearExpr.Sum(teacherGapFlags)
        If w.IncludeEdgePeriod Then
            result.EdgeSum = LinearExpr.Sum(
                built.Lesson.Where(Function(kv) kv.Key.Period = 1 OrElse kv.Key.Period >= ScheduleQuality.AfternoonThresholdPeriod).
                             Select(Function(kv) kv.Value))
        End If
        If classAfternoonDayVars.Count > 0 Then result.AfternoonSum = LinearExpr.Sum(classAfternoonDayVars)
        If classRangeVars.Count > 0 Then result.ClassRangeSum = LinearExpr.Sum(classRangeVars)
        If teacherRangeVars.Count > 0 Then result.TeacherRangeSum = LinearExpr.Sum(teacherRangeVars)
        If occupiedDensityParts.Count > 0 Then result.OccupiedDensitySum = occupiedDensityParts.Aggregate(Function(a, b) a + b)
        Return result
    End Function

    ''' <summary>Code-Review-Umsetzung (P6): kombiniert gewichtete Terme zu
    ''' EINER Zielfunktion, deren Integer-Koeffizienten vorher durch ihren
    ''' groessten gemeinsamen Teiler geteilt wurden. Eine positive
    ''' Gesamtskalierung aendert weder Optimum noch Ranking - sie
    ''' verkleinert aber die Koeffizienten (z.B. 100/500 -> 1/5), was
    ''' CP-SATs Bound-Beweis nachweislich leichter faellt (2.25c-Befund:
    ''' grosse Integer-Koeffizienten schwaechen die LP-Relaxierung;
    ''' GMS-P1-Befund: die Rest-Zielfunktion ist dort der verbliebene
    ''' Bound-Engpass). Zu beachten: ObjectiveValue/BestObjectiveBound
    ''' eines Laufs sind dadurch um den GCD-Faktor kleiner als die
    ''' nachgelagerte Quality.Total-Skala - die Optimalitaets-LUECKE in
    ''' Prozent ist skalierungsinvariant und bleibt vergleichbar.
    ''' Gewichte, deren CLng-Rundung 0 ergibt, bleiben wirkungslos (wie
    ''' bisher) und beeinflussen den GCD nicht.</summary>
    Private Function CombineGcdNormalized(parts As List(Of (Weight As Long, Expr As LinearExpr))) As LinearExpr
        If parts.Count = 0 Then Return Nothing
        Dim g As Long = 0
        For Each p In parts
            If p.Weight > 0 Then g = Gcd(g, p.Weight)
        Next
        If g = 0 Then g = 1
        Return parts.Select(Function(p) (p.Weight \ g) * p.Expr).Aggregate(Function(a, b) a + b)
    End Function

    Private Function Gcd(a As Long, b As Long) As Long
        While b <> 0
            Dim t = a Mod b
            a = b
            b = t
        End While
        Return a
    End Function

    ''' <summary>Die klassische gewichtete Gesamtzielfunktion ueber ALLE
    ''' vorhandenen Terme - identische Gewichtungsverhaeltnisse/
    ''' Termreihenfolge wie ApplyQualityObjective vor der Review-Umsetzung,
    ''' seit P6 GCD-normalisiert (siehe CombineGcdNormalized). Nothing,
    ''' wenn kein einziger Term existiert.</summary>
    Friend Function WeightedTotal(terms As QualityTerms, w As QualityWeights) As LinearExpr
        Dim parts As New List(Of (Weight As Long, Expr As LinearExpr))
        If terms.KannSum IsNot Nothing Then parts.Add((CLng(w.Kann), terms.KannSum))
        If terms.ClassGapsSum IsNot Nothing Then parts.Add((CLng(w.ClassGaps), terms.ClassGapsSum))
        If terms.TeacherGapsSum IsNot Nothing Then parts.Add((CLng(w.TeacherGaps), terms.TeacherGapsSum))
        If terms.EdgeSum IsNot Nothing Then parts.Add((CLng(w.EdgePeriod), terms.EdgeSum))
        If terms.AfternoonSum IsNot Nothing Then parts.Add((CLng(w.AfternoonDayCount), terms.AfternoonSum))
        If terms.ClassRangeSum IsNot Nothing Then parts.Add((CLng(w.ClassLoadVariance), terms.ClassRangeSum))
        If terms.TeacherRangeSum IsNot Nothing Then parts.Add((CLng(w.TeacherLoadVariance), terms.TeacherRangeSum))
        If terms.OccupiedDensitySum IsNot Nothing Then parts.Add((CLng(w.OccupiedDensity), terms.OccupiedDensitySum))
        Return CombineGcdNormalized(parts)
    End Function

    ''' <summary>P2: die gewichtete Summe der nicht lexikografisch
    ''' gestuften Kriterien (EdgePeriod/AfternoonDayCount/Class-/
    ''' TeacherLoadVariance) - Solver.SolveTops lexikografischer Modus
    ''' minimiert sie als letzte Rest-Zielfunktion, nachdem die Stufen
    ''' fixiert wurden. `teacherGapsInResidual` nimmt TeacherGaps mit
    ''' seinem konfigurierten Gewicht zusaetzlich auf - der Fall
    ''' "TeacherGaps ist KEINE eigene Stufe" (lexTeacherGapsStage=False,
    ''' der Default): Lehrer-Springstunden bleiben dann gegen die
    ''' uebrigen Restkriterien abwaegbar statt hart fixiert. Nothing,
    ''' wenn kein einziges Rest-Kriterium aktiv ist.</summary>
    Friend Function WeightedResidual(terms As QualityTerms, w As QualityWeights,
                                      Optional teacherGapsInResidual As Boolean = False) As LinearExpr
        Dim parts As New List(Of (Weight As Long, Expr As LinearExpr))
        If teacherGapsInResidual AndAlso terms.TeacherGapsSum IsNot Nothing Then
            parts.Add((CLng(w.TeacherGaps), terms.TeacherGapsSum))
        End If
        If terms.OccupiedDensitySum IsNot Nothing Then parts.Add((CLng(w.OccupiedDensity), terms.OccupiedDensitySum))
        If terms.EdgeSum IsNot Nothing Then parts.Add((CLng(w.EdgePeriod), terms.EdgeSum))
        If terms.AfternoonSum IsNot Nothing Then parts.Add((CLng(w.AfternoonDayCount), terms.AfternoonSum))
        If terms.ClassRangeSum IsNot Nothing Then parts.Add((CLng(w.ClassLoadVariance), terms.ClassRangeSum))
        If terms.TeacherRangeSum IsNot Nothing Then parts.Add((CLng(w.TeacherLoadVariance), terms.TeacherRangeSum))
        Return CombineGcdNormalized(parts)
    End Function

End Module
