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
        Dim sol1 As New ScoredSolution With {.Schedule = schedule1, .KannConstraintFlags = New List(Of KannConstraintFlag), .Quality = quality1, .Status = CpSolverStatus.Optimal}

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

End Class
