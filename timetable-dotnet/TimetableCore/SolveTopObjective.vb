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
    ''' and a `hasAny` BoolVar reified against it. Shared scaffolding reused
    ''' by both the gaps and the balance/range encodings below - built once
    ''' per entity kind (classes or teachers), not duplicated.</summary>
    Private Sub BuildScaffolding(model As CpModel, entities As List(Of String), days As List(Of String), periods As List(Of Integer),
                                  lookup As Dictionary(Of (Entity As String, Day As String, Period As Integer), List(Of BoolVar)), tag As String,
                                  occupied As Dictionary(Of (Entity As String, Day As String, Period As Integer), BoolVar),
                                  hasAny As Dictionary(Of (Entity As String, Day As String), BoolVar),
                                  dailyCount As Dictionary(Of (Entity As String, Day As String), LinearExpr))
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
                Dim ha = model.NewBoolVar($"hasAny[{tag},{entity},{d}]")
                model.Add(dc >= 1).OnlyEnforceIf(ha)
                model.Add(dc = 0).OnlyEnforceIf(ha.Not())
                hasAny((entity, d)) = ha
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
    ''' (everything but Kann/ClassGaps) can be structurally excluded from
    ''' the model via its own QualityWeights.Include*=False flag - the
    ''' corresponding auxiliary variables are never built at all (not just
    ''' weighted 0), for schools where the extra model size is not worth
    ''' the criterion at their scale. Called once by SolveTop, on a
    ''' BuiltModel from BuildCoreModel (NOT BuildModel - this replaces,
    ''' rather than adds to, BuildModel's own Kann-only Minimize).</summary>
    Friend Sub ApplyQualityObjective(built As BuiltModel, data As JsonObject, Optional weights As QualityWeights = Nothing)
        Dim w = If(weights, New QualityWeights())
        Dim model = built.Model
        Dim classNames = built.Sessions.Select(Function(s) s.ClassName).Distinct().ToList()
        Dim teacherNames = built.Sessions.Select(Function(s) s.Teacher).Distinct().ToList()

        Dim byClass = IndexLessonByEntityDayPeriod(built.Lesson, Function(k) k.ClassName)
        Dim byTeacher = IndexLessonByEntityDayPeriod(built.Lesson, Function(k) k.Teacher)

        Dim occupiedClass As New Dictionary(Of (Entity As String, Day As String, Period As Integer), BoolVar)
        Dim hasAnyClass As New Dictionary(Of (Entity As String, Day As String), BoolVar)
        Dim dailyCountClass As New Dictionary(Of (Entity As String, Day As String), LinearExpr)
        BuildScaffolding(model, classNames, built.Days, built.Periods, byClass, "C", occupiedClass, hasAnyClass, dailyCountClass)

        Dim occupiedTeacher As New Dictionary(Of (Entity As String, Day As String, Period As Integer), BoolVar)
        Dim hasAnyTeacher As New Dictionary(Of (Entity As String, Day As String), BoolVar)
        Dim dailyCountTeacher As New Dictionary(Of (Entity As String, Day As String), LinearExpr)
        BuildScaffolding(model, teacherNames, built.Days, built.Periods, byTeacher, "T", occupiedTeacher, hasAnyTeacher, dailyCountTeacher)

        Dim classGapFlags As New List(Of BoolVar)
        BuildGapFlags(model, classNames, built.Days, built.Periods, "C", occupiedClass, classGapFlags)
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

        Dim terms As New List(Of LinearExpr)
        If built.KannVars.Count > 0 Then
            terms.Add(CLng(w.Kann) * LinearExpr.Sum(built.KannVars.Values.Select(Function(kv) kv.Var)))
        End If
        If classGapFlags.Count > 0 Then terms.Add(CLng(w.ClassGaps) * LinearExpr.Sum(classGapFlags))
        If teacherGapFlags.Count > 0 Then terms.Add(CLng(w.TeacherGaps) * LinearExpr.Sum(teacherGapFlags))
        If w.IncludeEdgePeriod Then
            Dim edgeTerm As LinearExpr = LinearExpr.Sum(
                built.Lesson.Where(Function(kv) kv.Key.Period = 1 OrElse kv.Key.Period >= ScheduleQuality.AfternoonThresholdPeriod).
                             Select(Function(kv) kv.Value))
            terms.Add(CLng(w.EdgePeriod) * edgeTerm)
        End If
        If classAfternoonDayVars.Count > 0 Then terms.Add(CLng(w.AfternoonDayCount) * LinearExpr.Sum(classAfternoonDayVars))
        If classRangeVars.Count > 0 Then terms.Add(CLng(w.ClassLoadVariance) * LinearExpr.Sum(classRangeVars))
        If teacherRangeVars.Count > 0 Then terms.Add(CLng(w.TeacherLoadVariance) * LinearExpr.Sum(teacherRangeVars))

        If terms.Count > 0 Then model.Minimize(terms.Aggregate(Function(a, b) a + b))
    End Sub

End Module
