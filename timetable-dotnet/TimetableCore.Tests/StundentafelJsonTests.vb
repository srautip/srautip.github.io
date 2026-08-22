' Phase 2.21: hand-built Stammdatenbestand/MultiSolveResult objects (both
' plain public-property classes with parameterless constructors - no real
' CP-SAT solve needed) proving Formatting.ToStundentafelJson's data-shaping
' logic: uneven-parallel-class padding (including the "gap, not just
' trailing" case), a non-conforming-name fallback, per-solution quality/
' violation carrying, and that the independent Verifier re-check has real
' teeth (same "pigeonhole" discipline as the rest of this project).
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class StundentafelJsonTests

    Private Function EmptyData() As JsonObject
        Return Mini({}, {}, {}, {}, {"Mo"}, 1)
    End Function

    Private Function LeeresMultiResult() As MultiSolveResult
        Return New MultiSolveResult With {
            .Solutions = New List(Of ScoredSolution),
            .StopReason = MultiSolveStopReason.SearchSpaceExhausted,
            .IterationsRun = 0,
            .ElapsedS = 0
        }
    End Function

    Private Function KlassenstufeJson(json As JsonObject, nummer As Integer) As JsonObject
        For Each node In json("klassenstufen").AsArray()
            Dim obj = node.AsObject()
            If JsonHelpers.GetInt(obj, "nummer").Value = nummer Then Return obj
        Next
        Assert.Fail($"Klassenstufe {nummer} nicht gefunden")
        Return Nothing
    End Function

    <TestMethod>
    Public Sub PadsUnevenParallelKlassenAtEnd()
        Dim b As New Stammdatenbestand With {.SchulName = "Test"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 2, .Bezeichnung = "Klasse 2"})
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        b.Klassen.Add(New Klasse With {.Name = "1b", .Klassenstufe = 1})
        b.Klassen.Add(New Klasse With {.Name = "2a", .Klassenstufe = 2})
        b.Klassen.Add(New Klasse With {.Name = "2b", .Klassenstufe = 2})
        b.Klassen.Add(New Klasse With {.Name = "2c", .Klassenstufe = 2})

        Dim json = Formatting.ToStundentafelJson(b, EmptyData(), LeeresMultiResult())

        Assert.AreEqual(3, JsonHelpers.GetInt(json, "max_parallel_klassen").Value)
        Dim klassen1 = KlassenstufeJson(json, 1)("klassen").AsArray()
        Assert.AreEqual(3, klassen1.Count)
        Assert.AreEqual("1a", klassen1(0).GetValue(Of String)())
        Assert.AreEqual("1b", klassen1(1).GetValue(Of String)())
        Assert.IsNull(klassen1(2))
    End Sub

    ''' <summary>Nur "2b" existiert (kein "2a") - der leere Slot MUSS an
    ''' Index 0 (Buchstabe "a") liegen, nicht einfach ans Ende
    ''' angehaengt werden, sonst waere die Buchstaben-Spalten-Ausrichtung
    ''' ueber die Klassenstufen hinweg falsch.</summary>
    <TestMethod>
    Public Sub HandlesGapInParallelLetters()
        Dim b As New Stammdatenbestand With {.SchulName = "Test"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 2, .Bezeichnung = "Klasse 2"})
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        b.Klassen.Add(New Klasse With {.Name = "1b", .Klassenstufe = 1})
        b.Klassen.Add(New Klasse With {.Name = "2b", .Klassenstufe = 2})

        Dim json = Formatting.ToStundentafelJson(b, EmptyData(), LeeresMultiResult())

        Assert.AreEqual(2, JsonHelpers.GetInt(json, "max_parallel_klassen").Value)
        Dim klassen2 = KlassenstufeJson(json, 2)("klassen").AsArray()
        Assert.IsNull(klassen2(0))
        Assert.AreEqual("2b", klassen2(1).GetValue(Of String)())
    End Sub

    ''' <summary>Eine nicht-konforme Klassenbenennung (kein einzelner
    ''' Kleinbuchstabe als Suffix) darf nicht werfen - faellt stattdessen
    ''' auf eine stabile alphabetische Reihenfolge zurueck.</summary>
    <TestMethod>
    Public Sub FallsBackDeterministicallyForNonConformingNames()
        Dim b As New Stammdatenbestand With {.SchulName = "Test"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 5, .Bezeichnung = "Klasse 5"})
        b.Klassen.Add(New Klasse With {.Name = "5-Sport", .Klassenstufe = 5})
        b.Klassen.Add(New Klasse With {.Name = "5-Musik", .Klassenstufe = 5})

        Dim json = Formatting.ToStundentafelJson(b, EmptyData(), LeeresMultiResult())

        Dim klassen5 = KlassenstufeJson(json, 5)("klassen").AsArray().
            Select(Function(n) If(n Is Nothing, Nothing, n.GetValue(Of String)())).ToList()
        CollectionAssert.AreEquivalent(New List(Of String) From {"5-Musik", "5-Sport"}, klassen5)
    End Sub

    <TestMethod>
    Public Sub SolutionsCarryQualityAndViolationCounts()
        Dim b As New Stammdatenbestand With {.SchulName = "Test"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})

        Dim data = Scenario(Mini({"1a"}, {"T1"}, {"Mathe"}, {}, {"Mo"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "1a"}, {"subject", "Mathe"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T1"}, {"class", "1a"}, {"subject", "Mathe"}}
        })

        Dim schedule1 As New List(Of ScheduleEntry) From {
            New ScheduleEntry With {.ClassName = "1a", .Subject = "Mathe", .Teacher = "T1", .Day = "Mo", .Period = 1, .Room = Nothing}
        }
        Dim quality1 As New QualityScore With {.KannViolationCount = 0, .Total = 1.5}
        Dim convergence1 As New List(Of ConvergencePoint) From {
            New ConvergencePoint With {.ElapsedS = 0.2, .ObjectiveValue = 10.0},
            New ConvergencePoint With {.ElapsedS = 1.5, .ObjectiveValue = 8.0}
        }
        Dim sol1 As New ScoredSolution With {
            .Schedule = schedule1, .KannConstraintFlags = New List(Of KannConstraintFlag), .Quality = quality1, .Status = CpSolverStatus.Optimal,
            .ObjectiveValue = 8.0, .BestObjectiveBound = 6.0, .Convergence = convergence1}

        Dim multiResult As New MultiSolveResult With {
            .Solutions = New List(Of ScoredSolution) From {sol1},
            .StopReason = MultiSolveStopReason.MaxSolutionsReached,
            .IterationsRun = 1,
            .ElapsedS = 0.1
        }

        Dim json = Formatting.ToStundentafelJson(b, data, multiResult)

        Dim solutions = json("solutions").AsArray()
        Assert.AreEqual(1, solutions.Count)
        Dim sol0 = solutions(0).AsObject()
        Assert.AreEqual("Optimal", JsonHelpers.GetString(sol0, "status"))
        Assert.AreEqual(0, JsonHelpers.GetInt(sol0, "kann_violation_count").Value)
        Assert.AreEqual(1.5, sol0("quality_total").GetValue(Of Double)(), 0.001)

        ' Phase 2.22: Optimalitaets-Luecke + Konvergenz-Verlauf.
        Assert.AreEqual(8.0, sol0("objective_value").GetValue(Of Double)(), 0.001)
        Assert.AreEqual(6.0, sol0("best_objective_bound").GetValue(Of Double)(), 0.001)
        Assert.AreEqual(25.0, sol0("gap_percent").GetValue(Of Double)(), 0.001, "(8-6)/8 = 25%")
        Dim convergenceJson = sol0("convergence").AsArray()
        Assert.AreEqual(2, convergenceJson.Count)
        Assert.AreEqual(1.5, convergenceJson(1).AsObject()("elapsed_s").GetValue(Of Double)(), 0.001)
        Assert.AreEqual(8.0, convergenceJson(1).AsObject()("objective_value").GetValue(Of Double)(), 0.001)

        Dim cell = sol0("classes").AsObject()("1a").AsObject()("Mo").AsObject()("1").AsObject()
        Assert.AreEqual("Mathe", JsonHelpers.GetString(cell, "subject"))
        Assert.AreEqual("T1", JsonHelpers.GetString(cell, "teacher"))
    End Sub

    ''' <summary>Beweist, dass muss_violation_count tatsaechlich unabhaengig
    ''' (per Verifier.VerifySchedule) nachgerechnet wird, statt blind 0
    ''' zurueckzugeben - ein handgebauter, echt kollidierender Schedule
    ''' (kein parallel_group beteiligt) MUSS als Verstoss erkannt werden.</summary>
    <TestMethod>
    Public Sub MussViolationCountHasRealTeeth()
        Dim b As New Stammdatenbestand With {.SchulName = "Test"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})

        Dim data = Scenario(Mini({"1a"}, {"T1", "T2"}, {"Mathe", "Deutsch"}, {}, {"Mo"}, 1), {
            New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "1a"}}
        })

        Dim sauber As New List(Of ScheduleEntry) From {
            New ScheduleEntry With {.ClassName = "1a", .Subject = "Mathe", .Teacher = "T1", .Day = "Mo", .Period = 1, .Room = Nothing}
        }
        Dim kaputt As New List(Of ScheduleEntry) From {
            New ScheduleEntry With {.ClassName = "1a", .Subject = "Mathe", .Teacher = "T1", .Day = "Mo", .Period = 1, .Room = Nothing},
            New ScheduleEntry With {.ClassName = "1a", .Subject = "Deutsch", .Teacher = "T2", .Day = "Mo", .Period = 1, .Room = Nothing}
        }
        Dim quality As New QualityScore With {.KannViolationCount = 0, .Total = 0}
        Dim solSauber As New ScoredSolution With {.Schedule = sauber, .KannConstraintFlags = New List(Of KannConstraintFlag), .Quality = quality, .Status = CpSolverStatus.Optimal}
        Dim solKaputt As New ScoredSolution With {.Schedule = kaputt, .KannConstraintFlags = New List(Of KannConstraintFlag), .Quality = quality, .Status = CpSolverStatus.Optimal}

        Dim multiResult As New MultiSolveResult With {
            .Solutions = New List(Of ScoredSolution) From {solSauber, solKaputt},
            .StopReason = MultiSolveStopReason.MaxSolutionsReached,
            .IterationsRun = 2,
            .ElapsedS = 0.1
        }

        Dim json = Formatting.ToStundentafelJson(b, data, multiResult)
        Dim solutions = json("solutions").AsArray()
        Assert.AreEqual(0, JsonHelpers.GetInt(solutions(0).AsObject(), "muss_violation_count").Value)
        Assert.IsTrue(JsonHelpers.GetInt(solutions(1).AsObject(), "muss_violation_count").Value > 0)
    End Sub

    ''' <summary>Viewer-Ausbau Schritt 1: pro Loesung wird der VOLLE
    ''' Qualitaetsvektor exportiert (nicht nur quality_total) - jedes
    ''' Feld mit einem eindeutigen, hand-gesetzten Wert, damit eine
    ''' Feld-Verwechslung im Export auffliegt.</summary>
    <TestMethod>
    Public Sub ExportsFullQualityVectorPerSolution()
        Dim b As New Stammdatenbestand With {.SchulName = "Test"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        Dim quality As New QualityScore With {
            .KannViolationCount = 1, .ClassGapCount = 2, .TeacherGapCount = 3,
            .EdgePeriodCount = 4, .AfternoonDayCount = 5,
            .ClassLoadVariance = 6.25, .TeacherLoadVariance = 7.5,
            .OccupiedDensityCount = 8, .SubjectWindowCount = 11, .Total = 9.75}
        Dim sol As New ScoredSolution With {
            .Schedule = New List(Of ScheduleEntry), .KannConstraintFlags = New List(Of KannConstraintFlag),
            .Quality = quality, .Status = CpSolverStatus.Optimal}
        Dim multiResult As New MultiSolveResult With {
            .Solutions = New List(Of ScoredSolution) From {sol},
            .StopReason = MultiSolveStopReason.MaxSolutionsReached, .IterationsRun = 1, .ElapsedS = 1}

        Dim json = Formatting.ToStundentafelJson(b, Scenario(Mini({"1a"}, {}, {}, {}, {"Mo"}, 1), {}), multiResult)
        Dim s0 = json("solutions").AsArray()(0).AsObject()
        Assert.AreEqual(1, JsonHelpers.GetInt(s0, "kann_violation_count").Value)
        Assert.AreEqual(2, JsonHelpers.GetInt(s0, "class_gap_count").Value)
        Assert.AreEqual(3, JsonHelpers.GetInt(s0, "teacher_gap_count").Value)
        Assert.AreEqual(4, JsonHelpers.GetInt(s0, "edge_period_count").Value)
        Assert.AreEqual(5, JsonHelpers.GetInt(s0, "afternoon_day_count").Value)
        Assert.AreEqual(6.25, s0("class_load_variance").GetValue(Of Double)(), 0.0001)
        Assert.AreEqual(7.5, s0("teacher_load_variance").GetValue(Of Double)(), 0.0001)
        Assert.AreEqual(8, JsonHelpers.GetInt(s0, "occupied_density_count").Value)
        Assert.AreEqual(11, JsonHelpers.GetInt(s0, "subject_window_count").Value)
        Assert.AreEqual(9.75, s0("quality_total").GetValue(Of Double)(), 0.0001)
    End Sub

    ''' <summary>Viewer-Ausbau Schritt 1: top-level werden die effektiven
    ''' quality_weights inkl. Include*-Flags exportiert - Grundlage fuer
    ''' Gewichts-Regler und die gedaempften Spalten der
    ''' Vergleichstabelle. Ohne uebergebene Gewichte gelten die
    ''' Default-Konstanten.</summary>
    <TestMethod>
    Public Sub ExportsEffectiveQualityWeights()
        Dim b As New Stammdatenbestand With {.SchulName = "Test"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        Dim weights As New QualityWeights With {
            .ClassGaps = 1000.0, .TeacherGaps = 50.0, .OccupiedDensity = 500.0,
            .SubjectWindow = 42.0, .IncludeSubjectWindow = False,
            .IncludeEdgePeriod = False, .IncludeTeacherLoadVariance = False}

        Dim json = Formatting.ToStundentafelJson(b, EmptyData(), LeeresMultiResult(), weights)
        Dim qw = json("quality_weights").AsObject()
        Assert.AreEqual(1000.0, qw("class_gaps").GetValue(Of Double)(), 0.0001)
        Assert.AreEqual(50.0, qw("teacher_gaps").GetValue(Of Double)(), 0.0001)
        Assert.AreEqual(500.0, qw("occupied_density").GetValue(Of Double)(), 0.0001)
        Assert.AreEqual(42.0, qw("subject_window").GetValue(Of Double)(), 0.0001)
        Assert.IsFalse(qw("include_subject_window").GetValue(Of Boolean)())
        Assert.IsFalse(qw("include_edge_period").GetValue(Of Boolean)())
        Assert.IsFalse(qw("include_teacher_load_variance").GetValue(Of Boolean)())
        Assert.IsTrue(qw("include_class_gaps").GetValue(Of Boolean)())

        ' Ohne uebergebene Gewichte: Default-Konstanten.
        Dim jsonDefaults = Formatting.ToStundentafelJson(b, EmptyData(), LeeresMultiResult())
        Dim qwDefaults = jsonDefaults("quality_weights").AsObject()
        Assert.AreEqual(ScheduleQuality.WeightKann, qwDefaults("kann").GetValue(Of Double)(), 0.0001)
        Assert.AreEqual(ScheduleQuality.WeightOccupiedDensity, qwDefaults("occupied_density").GetValue(Of Double)(), 0.0001)
        Assert.AreEqual(ScheduleQuality.WeightSubjectWindow, qwDefaults("subject_window").GetValue(Of Double)(), 0.0001)
        Assert.IsTrue(qwDefaults("include_occupied_density").GetValue(Of Boolean)())
        Assert.IsTrue(qwDefaults("include_subject_window").GetValue(Of Boolean)())
    End Sub

    ''' <summary>Mehr-Zuteilungs-Export: Loesungen ZWEIER Zuteilungs-Laeufe
    ''' werden global nach Quality.Total sortiert zusammengefuehrt, jede
    ''' Loesung traegt ihren assignment_index, top-level stehen die
    ''' Zuteilungs-Metadaten und die Aequivalenzklassen (nur Gruppen mit
    ''' &gt;= 2 Mitgliedern; Singletons bleiben draussen).</summary>
    <TestMethod>
    Public Sub ExportsAssignmentsAndEquivalenceClasses()
        Dim b As New Stammdatenbestand With {.SchulName = "Test"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})

        Dim solOf = Function(total As Double) New ScoredSolution With {
            .Schedule = New List(Of ScheduleEntry), .KannConstraintFlags = New List(Of KannConstraintFlag),
            .Quality = New QualityScore With {.Total = total}, .Status = CpSolverStatus.Optimal}
        Dim runOf = Function(idx As Integer, objective As Double, totals As Double()) New Formatting.AssignmentRun With {
            .Data = Scenario(Mini({"1a"}, {}, {}, {}, {"Mo"}, 1), {}),
            .AssignmentIndex = idx, .LehrereinsatzObjective = objective,
            .Result = New MultiSolveResult With {
                .Solutions = totals.Select(Function(t) solOf(t)).ToList(),
                .StopReason = MultiSolveStopReason.MaxSolutionsReached, .IterationsRun = totals.Length, .ElapsedS = 1}}

        Dim runs As New List(Of Formatting.AssignmentRun) From {
            runOf(1, 20.0, {50.0, 90.0}),
            runOf(2, 40.0, {30.0, 70.0})
        }
        Dim eq As New List(Of List(Of String)) From {
            New List(Of String) From {"Lehrer A", "Lehrer B"},
            New List(Of String) From {"Solo"}
        }
        Dim json = Formatting.ToStundentafelJsonMulti(b, runs, equivalenceClasses:=eq)

        Dim sols = json("solutions").AsArray()
        Assert.AreEqual(4, sols.Count)
        ' global sortiert: 30 (Z2), 50 (Z1), 70 (Z2), 90 (Z1)
        Assert.AreEqual(30.0, sols(0)("quality_total").GetValue(Of Double)(), 0.001)
        Assert.AreEqual(2, JsonHelpers.GetInt(sols(0).AsObject(), "assignment_index").Value)
        Assert.AreEqual(50.0, sols(1)("quality_total").GetValue(Of Double)(), 0.001)
        Assert.AreEqual(1, JsonHelpers.GetInt(sols(1).AsObject(), "assignment_index").Value)
        Assert.AreEqual(2, JsonHelpers.GetInt(sols(2).AsObject(), "assignment_index").Value)
        Assert.AreEqual(1, JsonHelpers.GetInt(sols(3).AsObject(), "assignment_index").Value)

        Dim assignments = json("assignments").AsArray()
        Assert.AreEqual(2, assignments.Count)
        Assert.AreEqual(20.0, assignments(0)("lehrereinsatz_objective").GetValue(Of Double)(), 0.001)
        Assert.AreEqual(2, JsonHelpers.GetInt(assignments(1).AsObject(), "solution_count").Value)

        Dim eqJson = json("teacher_equivalence_classes").AsArray()
        Assert.AreEqual(1, eqJson.Count, "Singleton-Klassen duerfen nicht exportiert werden.")
        Assert.AreEqual("Lehrer A", eqJson(0).AsArray()(0).GetValue(Of String)())
        Assert.AreEqual("Lehrer B", eqJson(0).AsArray()(1).GetValue(Of String)())
    End Sub

End Class
