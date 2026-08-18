' Phase 2.11f: tests for Formatting.ToWahlprofilGrids/FormatKursstufeSchedule.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class WahlprofilGridsTests

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

    Private Shared Function HappyPathScenario() As JsonObject
        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray({"T-Deutsch", "T-Mathe", "T-Bio", "T-Englisch"})},
            {"subjects", New JsonArray({"Deutsch", "Mathematik", "Biologie", "Englisch"})},
            {"rooms", New JsonArray({"R1", "R2"})},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray({"Mo", "Di", "Mi", "Do", "Fr"})}, {"periods_per_day", 8}
            }},
            {"kurse", New JsonArray({
                CType(Kurs("D-LK1", "Deutsch", "T-Deutsch", "LK", 5), JsonNode),
                Kurs("MA-LK1", "Mathematik", "T-Mathe", "LK", 5),
                Kurs("BIO-LK1", "Biologie", "T-Bio", "LK", 5),
                Kurs("EN-GK1", "Englisch", "T-Englisch", "GK", 3)
            })},
            {"schienen", New JsonArray({
                CType(Schiene("S1", "LK", 5), JsonNode), Schiene("S2", "LK", 5), Schiene("S3", "LK", 5),
                Schiene("S4", "GK", 3)
            })}
        }
        Return New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray({
                CType(Kurswahl("WP1", {"D-LK1", "MA-LK1", "BIO-LK1", "EN-GK1"}), JsonNode)
            })}
        }
    End Function

    <TestMethod>
    Public Sub ToWahlprofilGridsPlacesEveryOwnKursWithoutCollision()
        Dim data = HappyPathScenario()
        Dim r = Solver.SolveKursstufe(data, numWorkers:=1)
        Assert.IsNotNull(r.Schedule)

        Dim grids = Formatting.ToWahlprofilGrids(data, r.Schedule)
        Assert.IsTrue(grids.ContainsKey("WP1"))

        Dim filledCells = grids("WP1").Values.SelectMany(Function(byPeriod) byPeriod.Values).Where(Function(c) c IsNot Nothing).ToList()
        ' 5+5+5+3 = 18 Slots insgesamt fuer WP1s 4 Kurse.
        Assert.AreEqual(18, filledCells.Count)

        Dim kursIdsSeen = New HashSet(Of String)(filledCells.Select(Function(c) c.ClassName))
        CollectionAssert.AreEquivalent(New List(Of String) From {"D-LK1", "MA-LK1", "BIO-LK1", "EN-GK1"}, kursIdsSeen.ToList())

        For Each cell In filledCells
            Assert.IsFalse(String.IsNullOrEmpty(cell.Subject))
            Assert.IsFalse(String.IsNullOrEmpty(cell.Teacher))
            Assert.IsTrue(cell.Room = "R1" OrElse cell.Room = "R2")
        Next
    End Sub

    <TestMethod>
    Public Sub FormatKursstufeScheduleContainsWahlprofilHeaderAndId()
        Dim data = HappyPathScenario()
        Dim r = Solver.SolveKursstufe(data, numWorkers:=1)
        Dim text = Formatting.FormatKursstufeSchedule(data, r.Schedule)

        StringAssert.Contains(text, "WAHLPROFILE")
        StringAssert.Contains(text, "WP1")
        ' Mindestens ein tatsaechlich belegtes Kurs-Feld muss auftauchen.
        StringAssert.Contains(text, "D-LK1")
    End Sub

    ''' <summary>Defense-in-depth: two entries under the same Wahlprofil's
    ''' own Kurse claiming the identical (Day,Period) slot must Throw
    ''' rather than silently overwrite one of them - this can only happen
    ''' via a manually tampered/synthetic kursSchedule (a real
    ''' Kursblockung-derived one can't produce this), which is exactly
    ''' what this test constructs.</summary>
    <TestMethod>
    Public Sub ThrowsOnManufacturedCollisionWithinOneWahlprofil()
        Dim ent As New JsonObject From {
            {"classes", New JsonArray()}, {"teachers", New JsonArray({"T-A", "T-B"})},
            {"subjects", New JsonArray({"Fach A", "Fach B"})}, {"rooms", New JsonArray()},
            {"timeslots", New JsonObject From {{"days", New JsonArray({"Mo"})}, {"periods_per_day", 1}}},
            {"kurse", New JsonArray({
                CType(Kurs("A-GK1", "Fach A", "T-A", "GK", 1), JsonNode),
                Kurs("B-GK1", "Fach B", "T-B", "GK", 1)
            })},
            {"schienen", New JsonArray({CType(Schiene("S1", "GK", 1), JsonNode)})}
        }
        Dim data As New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray({CType(Kurswahl("WP1", {"A-GK1", "B-GK1"}), JsonNode)})}
        }
        Dim tamperedSchedule As New List(Of ScheduleEntry) From {
            New ScheduleEntry With {.ClassName = "A-GK1", .Subject = "Fach A", .Teacher = "T-A", .Day = "Mo", .Period = 1},
            New ScheduleEntry With {.ClassName = "B-GK1", .Subject = "Fach B", .Teacher = "T-B", .Day = "Mo", .Period = 1}
        }

        Assert.ThrowsException(Of InvalidOperationException)(Sub() Formatting.ToWahlprofilGrids(data, tamperedSchedule))
    End Sub

End Class
