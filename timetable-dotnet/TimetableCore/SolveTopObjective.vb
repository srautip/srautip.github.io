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

    ''' <summary>Per (entity, day): "span of occupied periods minus count
    ''' of occupied periods" ("Springstunden"), via the sentinel-
    ''' substitution Min/Max trick - live-verified against the actual
    ''' installed OrTools build during planning (a solved smoke model with
    ''' occupied={0,2} on a 3-period day produced firstOccupied=0,
    ''' lastOccupied=2, gapVar=1, matching hand computation exactly).</summary>
    Private Sub BuildGapVars(model As CpModel, entities As List(Of String), days As List(Of String), periods As List(Of Integer), tag As String,
                              occupied As Dictionary(Of (Entity As String, Day As String, Period As Integer), BoolVar),
                              hasAny As Dictionary(Of (Entity As String, Day As String), BoolVar),
                              gapVars As List(Of IntVar))
        Dim maxPeriod = periods.Max()
        Dim bigPeriod = CLng(maxPeriod) + 1L
        For Each entity In entities
            For Each d In days
                Dim fcList As New List(Of LinearExpr)
                Dim lcList As New List(Of LinearExpr)
                Dim occList As New List(Of BoolVar)
                For Each p In periods
                    Dim occ = occupied((entity, d, p))
                    occList.Add(occ)
                    Dim fc = model.NewIntVar(0L, bigPeriod, $"fc[{tag},{entity},{d},{p}]")
                    model.Add(fc = CLng(p)).OnlyEnforceIf(occ)
                    model.Add(fc = bigPeriod).OnlyEnforceIf(occ.Not())
                    fcList.Add(fc)
                    Dim lc = model.NewIntVar(0L, CLng(maxPeriod), $"lc[{tag},{entity},{d},{p}]")
                    model.Add(lc = CLng(p)).OnlyEnforceIf(occ)
                    model.Add(lc = 0L).OnlyEnforceIf(occ.Not())
                    lcList.Add(lc)
                Next
                Dim firstOccupied = model.NewIntVar(0L, bigPeriod, $"first[{tag},{entity},{d}]")
                model.AddMinEquality(firstOccupied, fcList)
                Dim lastOccupied = model.NewIntVar(0L, CLng(maxPeriod), $"last[{tag},{entity},{d}]")
                model.AddMaxEquality(lastOccupied, lcList)

                Dim occSum As LinearExpr = LinearExpr.Sum(occList)
                Dim ha = hasAny((entity, d))
                Dim gapVar = model.NewIntVar(0L, CLng(periods.Count), $"gap[{tag},{entity},{d}]")
                model.Add(gapVar = (lastOccupied - firstOccupied + 1L) - occSum).OnlyEnforceIf(ha)
                model.Add(gapVar = 0L).OnlyEnforceIf(ha.Not())
                gapVars.Add(gapVar)
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

    ''' <summary>Adds a Minimize objective to `built.Model` combining all 5
    ''' ScheduleQuality criteria (Kann-violations dominant, then gaps, edge
    ''' periods, and class/teacher load balance), using the same weight
    ''' constants ScheduleQuality.vb uses to score candidates afterward -
    ''' so the search itself is now steered toward what SolveTop's final
    ''' ranking already valued. Called once by SolveTop, on a BuiltModel
    ''' from BuildCoreModel (NOT BuildModel - this replaces, rather than
    ''' adds to, BuildModel's own Kann-only Minimize).</summary>
    Friend Sub ApplyQualityObjective(built As BuiltModel, data As JsonObject)
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

        Dim classGapVars As New List(Of IntVar)
        BuildGapVars(model, classNames, built.Days, built.Periods, "C", occupiedClass, hasAnyClass, classGapVars)
        Dim teacherGapVars As New List(Of IntVar)
        BuildGapVars(model, teacherNames, built.Days, built.Periods, "T", occupiedTeacher, hasAnyTeacher, teacherGapVars)

        Dim classRangeVars As New List(Of IntVar)
        BuildClassRangeVars(model, classNames, built.Days, built.Periods, dailyCountClass, classRangeVars)

        Dim teacherRangeVars As New List(Of IntVar)
        BuildTeacherRangeVars(model, teacherNames, built.Days, built.Periods, built.Lesson, dailyCountTeacher, hasAnyTeacher, teacherRangeVars)

        Dim edgeTerm As LinearExpr = LinearExpr.Sum(
            built.Lesson.Where(Function(kv) kv.Key.Period = 1 OrElse kv.Key.Period >= ScheduleQuality.AfternoonThresholdPeriod).
                         Select(Function(kv) kv.Value))

        Dim terms As New List(Of LinearExpr)
        If built.KannVars.Count > 0 Then
            terms.Add(CLng(ScheduleQuality.WeightKann) * LinearExpr.Sum(built.KannVars.Values.Select(Function(kv) kv.Var)))
        End If
        If classGapVars.Count > 0 Then terms.Add(CLng(ScheduleQuality.WeightClassGaps) * LinearExpr.Sum(classGapVars))
        If teacherGapVars.Count > 0 Then terms.Add(CLng(ScheduleQuality.WeightTeacherGaps) * LinearExpr.Sum(teacherGapVars))
        terms.Add(CLng(ScheduleQuality.WeightEdgePeriod) * edgeTerm)
        If classRangeVars.Count > 0 Then terms.Add(CLng(ScheduleQuality.WeightClassLoadVariance) * LinearExpr.Sum(classRangeVars))
        If teacherRangeVars.Count > 0 Then terms.Add(CLng(ScheduleQuality.WeightTeacherLoadVariance) * LinearExpr.Sum(teacherRangeVars))

        model.Minimize(terms.Aggregate(Function(a, b) a + b))
    End Sub

End Module
