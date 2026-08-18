' Phase 2.11d: live tests for stage C (Raumzuordnung.vb). Proves that the
' pinned-schedule + room_requirement/no_overlap(room) reuse of the
' UNCHANGED Solver.Solve() actually does real room-conflict resolution
' work (not just a no-op pass-through), via a direct pigeonhole: two GK-
' Kurse sharing the one GK-Schiene run at IDENTICAL day/period slots (a
' Schiene's defining property), so they need two DIFFERENT rooms at once.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class RaumzuordnungTests

    Private Shared Function Kurs(id As String, subject As String, teacher As String,
                                  kursart As String, hoursPerWeek As Integer) As JsonObject
        Return New JsonObject From {
            {"id", id}, {"subject", subject}, {"teacher", teacher},
            {"kursart", kursart}, {"hours_per_week", hoursPerWeek}
        }
    End Function

    Private Shared Function Schiene(id As String, kursart As String, hoursPerWeek As Integer) As JsonObject
        Return New JsonObject From {{"id", id}, {"kursart", kursart}, {"hours_per_week", hoursPerWeek}}
    End Function

    Private Shared Function Kurswahl(wahlprofilId As String, kurse As IEnumerable(Of String)) As JsonObject
        Return New JsonObject From {
            {"type", "kurswahl"}, {"wahlprofil_id", wahlprofilId}, {"student_count", 20},
            {"kurse", New JsonArray(kurse.Select(Function(k) CType(k, JsonNode)).ToArray())}
        }
    End Function

    ''' <summary>Two GK-Kurse (different subjects/teachers, chosen by two
    ''' DIFFERENT Wahlprofile so Kursblockung is free to put both on the
    ''' one GK-Schiene), `roomCount` rooms available.</summary>
    Private Shared Function TwoCoursesOneSchieneScenario(roomCount As Integer) As JsonObject
        Dim rooms = Enumerable.Range(1, roomCount).Select(Function(i) $"R{i}").ToList()
        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray({"T-A", "T-B"})},
            {"subjects", New JsonArray({"Fach A", "Fach B"})},
            {"rooms", New JsonArray(rooms.Select(Function(r) CType(r, JsonNode)).ToArray())},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray({"Mo", "Di", "Mi", "Do", "Fr"})}, {"periods_per_day", 4}
            }},
            {"kurse", New JsonArray({
                CType(Kurs("A-GK1", "Fach A", "T-A", "GK", 3), JsonNode),
                Kurs("B-GK1", "Fach B", "T-B", "GK", 3)
            })},
            {"schienen", New JsonArray({CType(Schiene("S1", "GK", 3), JsonNode)})}
        }
        Return New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray({
                CType(Kurswahl("WP1", {"A-GK1"}), JsonNode),
                Kurswahl("WP2", {"B-GK1"})
            })}
        }
    End Function

    Private Shared Function SolveThroughStageC(data As JsonObject) As SolveResult
        Dim kb = Kursblockung.SolveKursblockung(data, numWorkers:=1)
        Assert.IsTrue(kb.Status = CpSolverStatus.Optimal OrElse kb.Status = CpSolverStatus.Feasible)
        Dim schienenResult = Solver.Solve(Schienenraster.BuildSchienenrasterScenario(data), numWorkers:=1)
        Assert.IsTrue(schienenResult.Status = CpSolverStatus.Optimal OrElse schienenResult.Status = CpSolverStatus.Feasible)
        Dim raumScenario = Raumzuordnung.BuildRaumzuordnungScenario(data, kb.Assignment, schienenResult.Schedule)
        Return Solver.Solve(raumScenario, numWorkers:=1)
    End Function

    <TestMethod>
    Public Sub InfeasibleWhenFewerRoomsThanSimultaneousKurse()
        Dim data = TwoCoursesOneSchieneScenario(roomCount:=1)
        Dim r = SolveThroughStageC(data)
        Assert.AreEqual(CpSolverStatus.Infeasible, r.Status)
    End Sub

    <TestMethod>
    Public Sub SolvesCleanlyAndAssignsDistinctRoomsWithEnoughRooms()
        Dim data = TwoCoursesOneSchieneScenario(roomCount:=2)
        Dim r = SolveThroughStageC(data)
        Assert.IsTrue(r.Status = CpSolverStatus.Optimal OrElse r.Status = CpSolverStatus.Feasible, Solver.StatusName(r.Status))

        Dim raumScenario = Raumzuordnung.BuildRaumzuordnungScenario(data,
            Kursblockung.SolveKursblockung(data, numWorkers:=1).Assignment,
            Solver.Solve(Schienenraster.BuildSchienenrasterScenario(data), numWorkers:=1).Schedule)
        Dim violations = Verifier.VerifySchedule(raumScenario, r.Schedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))

        ' A-GK1 und B-GK1 laufen an identischen Slots (dieselbe Schiene) -
        ' muessen also unterschiedliche Raeume bekommen.
        Dim aEntries = r.Schedule.Where(Function(e) e.ClassName = "A-GK1").ToList()
        Dim bEntries = r.Schedule.Where(Function(e) e.ClassName = "B-GK1").ToList()
        Assert.AreEqual(3, aEntries.Count)
        Assert.AreEqual(3, bEntries.Count)
        For Each a In aEntries
            Dim b = bEntries.Single(Function(e) e.Day = a.Day AndAlso e.Period = a.Period)
            Assert.AreNotEqual(a.Room, b.Room)
        Next
    End Sub

End Class
