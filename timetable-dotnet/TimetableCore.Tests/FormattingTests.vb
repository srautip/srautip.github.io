' Ported 1:1 from tests/test_formatting.py.
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class FormattingTests

    <TestMethod>
    Public Sub ToClassGridsMatchesRawScheduleExactly()
        Dim data = BuildFullScenario()
        Dim r = Solver.Solve(data, timeLimitS:=20)
        Dim grids = Formatting.ToClassGrids(data, r.Schedule)

        For Each l In r.Schedule
            Dim cell = grids(l.ClassName)(l.Day)(l.Period)
            Assert.IsNotNull(cell)
            Assert.AreEqual(l.Subject, cell.Subject)
            Assert.AreEqual(l.Teacher, cell.Teacher)
            Assert.AreEqual(l.Room, cell.Room)
        Next

        Dim scheduledSlots = New HashSet(Of (String, String, Integer))(
            r.Schedule.Select(Function(l) (l.ClassName, l.Day, l.Period)))
        For Each clsEntry In grids
            For Each dayEntry In clsEntry.Value
                For Each periodEntry In dayEntry.Value
                    If periodEntry.Value IsNot Nothing Then
                        Assert.IsTrue(scheduledSlots.Contains((clsEntry.Key, dayEntry.Key, periodEntry.Key)))
                    End If
                Next
            Next
        Next
    End Sub

    <TestMethod>
    Public Sub ToTeacherGridsMatchesRawScheduleExactly()
        Dim data = BuildFullScenario()
        Dim r = Solver.Solve(data, timeLimitS:=20)
        Dim grids = Formatting.ToTeacherGrids(data, r.Schedule)

        For Each l In r.Schedule
            Dim cell = grids(l.Teacher)(l.Day)(l.Period)
            Assert.IsNotNull(cell)
            Assert.AreEqual(l.ClassName, cell.ClassName)
            Assert.AreEqual(l.Subject, cell.Subject)
        Next
    End Sub

    ''' <summary>Every (day, period) slot must be a key in the grid, even
    ''' if no lesson happens there (value Nothing) - callers shouldn't
    ''' need extra Nothing-checks to render a full week.</summary>
    <TestMethod>
    Public Sub GridsAreFullyPopulatedIncludingFreePeriods()
        Dim data = BuildFullScenario()
        Dim r = Solver.Solve(data, timeLimitS:=20)
        Dim grids = Formatting.ToClassGrids(data, r.Schedule)
        Dim ent = JsonHelpers.Entities(data)
        Dim days = JsonHelpers.AsStringList(JsonHelpers.Timeslots(ent), "days")
        Dim nPeriods = JsonHelpers.GetInt(JsonHelpers.Timeslots(ent), "periods_per_day").Value

        For Each cls In JsonHelpers.AsStringList(ent, "classes")
            CollectionAssert.AreEquivalent(days, grids(cls).Keys.ToList())
            For Each d In days
                CollectionAssert.AreEquivalent(Enumerable.Range(1, nPeriods).ToList(), grids(cls)(d).Keys.ToList())
            Next
        Next
    End Sub

    <TestMethod>
    Public Sub FormatGridRendersReadableAsciiTable()
        Dim gridForEntity As New Dictionary(Of String, Dictionary(Of Integer, GridCell)) From {
            {"Mo", New Dictionary(Of Integer, GridCell) From {
                {1, New GridCell With {.Subject = "Mathe", .Teacher = "T1", .Room = Nothing}},
                {2, Nothing}
            }},
            {"Di", New Dictionary(Of Integer, GridCell) From {
                {1, Nothing},
                {2, New GridCell With {.Subject = "Chemie", .Teacher = "T2", .Room = "R1"}}
            }}
        }
        Dim text = Formatting.FormatGrid("5a", gridForEntity, {"Mo", "Di"}.ToList(), {1, 2}.ToList(),
                                          Function(c) If(c Is Nothing, "-", c.Subject))
        Dim lines = text.Split(vbLf)
        Assert.AreEqual("=== 5a ===", lines(0))
        Assert.IsTrue(lines(1).Contains("Mo") AndAlso lines(1).Contains("Di"))
        Assert.IsTrue(text.Contains("Mathe"))
        Assert.IsTrue(text.Contains("Chemie"))
        Assert.AreEqual(5, lines.Length) ' title, header, separator, 2 period rows
    End Sub

    <TestMethod>
    Public Sub FormatScheduleContainsEveryScheduledLessonOnce()
        Dim data = BuildFullScenario()
        Dim r = Solver.Solve(data, timeLimitS:=20)
        Dim text = Formatting.FormatSchedule(data, r.Schedule)

        Assert.IsTrue(text.Contains("KLASSEN"))
        Assert.IsTrue(text.Contains("LEHRKRAEFTE"))
        Dim ent = JsonHelpers.Entities(data)
        For Each cls In JsonHelpers.AsStringList(ent, "classes")
            Assert.IsTrue(text.Contains($"=== {cls} ==="))
        Next
        For Each teacher In JsonHelpers.AsStringList(ent, "teachers")
            Assert.IsTrue(text.Contains($"=== {teacher} ==="))
        Next

        For Each l In r.Schedule
            Assert.IsTrue(text.Contains(l.Subject))
            Assert.IsTrue(text.Contains(l.Teacher))
        Next
    End Sub

    <TestMethod>
    Public Sub FormatScheduleCanOmitTeacherTables()
        Dim data = BuildFullScenario()
        Dim r = Solver.Solve(data, timeLimitS:=20)
        Dim text = Formatting.FormatSchedule(data, r.Schedule, includeTeachers:=False)
        Assert.IsTrue(text.Contains("KLASSEN"))
        Assert.IsFalse(text.Contains("LEHRKRAEFTE"))
    End Sub

    <TestMethod>
    Public Sub ToJsonPerClassRoundTripsThroughJsonDumps()
        Dim data = BuildFullScenario()
        Dim r = Solver.Solve(data, timeLimitS:=20)
        Dim exported = Formatting.ToJsonPerClass(data, r.Schedule)

        Dim dumped = exported.ToJsonString() ' must not raise
        Dim reloaded = JsonNode.Parse(dumped).AsObject()

        For Each l In r.Schedule
            Dim cell = reloaded(l.ClassName).AsObject()(l.Day).AsObject()(l.Period.ToString()).AsObject()
            Assert.AreEqual(l.Subject, JsonHelpers.GetString(cell, "subject"))
            Assert.AreEqual(l.Teacher, JsonHelpers.GetString(cell, "teacher"))
        Next
    End Sub

    ''' <summary>Phase 2.18: FormatGridMarkdown liefert eine gueltige
    ''' GFM-Tabelle (Kopfzeile + Trennzeile + eine Datenzeile pro Periode)
    ''' und enthaelt denselben Zelleninhalt wie das bestehende ASCII-
    ''' FormatGrid, nur im Markdown-Pipe-Format.</summary>
    <TestMethod>
    Public Sub FormatGridMarkdownProducesValidTableWithExpectedCellContent()
        Dim days = New List(Of String) From {"Mo", "Di"}
        Dim periods = New List(Of Integer) From {1, 2}
        Dim grid As New Dictionary(Of String, Dictionary(Of Integer, GridCell)) From {
            {"Mo", New Dictionary(Of Integer, GridCell) From {{1, New GridCell With {.Subject = "Deutsch", .Teacher = "Lehrer A"}}, {2, Nothing}}},
            {"Di", New Dictionary(Of Integer, GridCell) From {{1, Nothing}, {2, New GridCell With {.Subject = "Mathematik", .Teacher = "Lehrer B"}}}}
        }
        Dim cellText = Function(cell As GridCell) As String
                           If cell Is Nothing Then Return "-"
                           Return $"{cell.Subject} ({cell.Teacher})"
                       End Function

        Dim md = Formatting.FormatGridMarkdown("1a", grid, days, periods, cellText)
        Dim lines = md.Split(vbLf)
        Assert.AreEqual("### 1a", lines(0))
        Assert.AreEqual("| Std. | Mo | Di |", lines(2))
        Assert.AreEqual("|---|---|---|", lines(3))
        Assert.AreEqual("| 1 | Deutsch (Lehrer A) | - |", lines(4))
        Assert.AreEqual("| 2 | - | Mathematik (Lehrer B) |", lines(5))
    End Sub

    ''' <summary>Phase 2.18: FormatLehrereinsatzMarkdown rendert Status,
    ''' die Lehrkraefte-Tabelle (Soll/Ist/Klassenlehrer-von/Zuweisungen)
    ''' und die Klassenlehrer-je-Klasse-Tabelle aus einem gebauten
    ''' LehrereinsatzResult - reine Formatierungspruefung, kein Solve
    ''' noetig.</summary>
    <TestMethod>
    Public Sub FormatLehrereinsatzMarkdownRendersLehrkraefteAndKlassenlehrerTables()
        Dim b As New Stammdatenbestand With {.SchulName = "Test-Schule"}
        Dim deutsch As New Fach With {.Name = "Deutsch"}
        deutsch.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 4})
        b.Faecher.Add(deutsch)
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 4})

        Dim result As New LehrereinsatzResult With {
            .Status = Google.OrTools.Sat.CpSolverStatus.Optimal,
            .Zuweisungen = New List(Of LehrereinsatzZuweisung) From {
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer A", .Klasse = "1a", .Fach = "Deutsch"}
            },
            .Klassenlehrer = New Dictionary(Of String, String) From {{"1a", "Lehrer A"}}
        }

        Dim md = Formatting.FormatLehrereinsatzMarkdown(b, result)
        Assert.IsTrue(md.Contains("# Lehrerzuteilung: Test-Schule"))
        Assert.IsTrue(md.Contains("| Lehrer A | 4 | 4 | 1a | 1a/Deutsch |"))
        Assert.IsTrue(md.Contains("| 1a | Lehrer A |"))
    End Sub

End Class
