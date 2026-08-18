' Phase 2.11e: unit tests for Verifier.VerifyKursblockung (independent
' re-check of a Kursblockung.SolveKursblockung result, no CP-SAT
' involved), plus a regression test proving the EXISTING
' Verifier.VerifySchedule/VerifyScheduleDetailed routes - completely
' unchanged by Phase 2.11 - still correctly detect a real violation when
' run on Kurs-level (Raumzuordnung-stage) schedule data.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class VerifyKursblockungTests

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

    Private Shared Function BaselineData() As JsonObject
        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray({"T-Deutsch", "T-Mathe", "T-Bio"})},
            {"subjects", New JsonArray({"Deutsch", "Mathematik", "Biologie"})},
            {"rooms", New JsonArray()},
            {"timeslots", New JsonObject From {{"days", New JsonArray({"Mo"})}, {"periods_per_day", 8}}},
            {"kurse", New JsonArray({
                CType(Kurs("D-LK1", "Deutsch", "T-Deutsch", "LK", 5), JsonNode),
                Kurs("MA-LK1", "Mathematik", "T-Mathe", "LK", 5),
                Kurs("BIO-LK1", "Biologie", "T-Bio", "LK", 5)
            })},
            {"schienen", New JsonArray({
                CType(Schiene("S1", "LK", 5), JsonNode), Schiene("S2", "LK", 5), Schiene("S3", "LK", 5)
            })}
        }
        Return New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray({CType(Kurswahl("WP1", {"D-LK1", "MA-LK1", "BIO-LK1"}), JsonNode)})}
        }
    End Function

    <TestMethod>
    Public Sub CleanAssignmentHasNoViolations()
        Dim data = BaselineData()
        Dim assignment = New Dictionary(Of String, String) From {
            {"D-LK1", "S1"}, {"MA-LK1", "S2"}, {"BIO-LK1", "S3"}
        }
        Assert.AreEqual(0, Verifier.VerifyKursblockung(data, assignment).Count)
    End Sub

    <TestMethod>
    Public Sub MissingAssignmentIsFlagged()
        Dim data = BaselineData()
        Dim assignment = New Dictionary(Of String, String) From {{"D-LK1", "S1"}, {"MA-LK1", "S2"}}
        Dim violations = Verifier.VerifyKursblockung(data, assignment)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("BIO-LK1") AndAlso v.Contains("keine Schienen-Zuordnung")))
    End Sub

    <TestMethod>
    Public Sub IncompatibleSchieneAssignmentIsFlagged()
        Dim data = BaselineData()
        ' D-LK1 (LK, 5h) faelschlich einer GK-Schiene zugeordnet - eine
        ' echte Kursblockung.vb wuerde das nie tun (assign-Variablen
        ' existieren nur fuer kompatible Paare), aber der Verifier prueft
        ' unabhaengig, ob genau DAS gilt, statt es zu unterstellen.
        CType(JsonHelpers.Entities(data)("schienen"), JsonArray).Add(Schiene("S4", "GK", 3))
        Dim assignment = New Dictionary(Of String, String) From {
            {"D-LK1", "S4"}, {"MA-LK1", "S2"}, {"BIO-LK1", "S3"}
        }
        Dim violations = Verifier.VerifyKursblockung(data, assignment)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("D-LK1") AndAlso v.Contains("inkompatiblen")))
    End Sub

    <TestMethod>
    Public Sub WahlprofilCollisionIsFlagged()
        Dim data = BaselineData()
        ' D-LK1 und MA-LK1 landen beide in S1 - WP1 waehlt aber beide.
        Dim assignment = New Dictionary(Of String, String) From {
            {"D-LK1", "S1"}, {"MA-LK1", "S1"}, {"BIO-LK1", "S3"}
        }
        Dim violations = Verifier.VerifyKursblockung(data, assignment)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("WP1") AndAlso v.Contains("S1")))
    End Sub

    <TestMethod>
    Public Sub TeacherCollisionIsFlagged()
        Dim data = BaselineData()
        ' Ein zweiter Deutsch-LK-Kurs, ebenfalls von T-Deutsch unterrichtet.
        CType(JsonHelpers.Entities(data)("kurse"), JsonArray).Add(Kurs("D-LK2", "Deutsch", "T-Deutsch", "LK", 5))
        Dim assignment = New Dictionary(Of String, String) From {
            {"D-LK1", "S1"}, {"D-LK2", "S1"}, {"MA-LK1", "S2"}, {"BIO-LK1", "S3"}
        }
        Dim violations = Verifier.VerifyKursblockung(data, assignment)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("T-Deutsch") AndAlso v.Contains("S1")))
    End Sub

    ''' <summary>Regression proof (2.11e gate): the EXISTING, completely
    ''' unchanged Verifier.VerifySchedule still correctly detects a real
    ''' violation when run on Kurs-level (Raumzuordnung-stage) schedule
    ''' data - not just "0 violations because nothing is really being
    ''' checked".</summary>
    <TestMethod>
    Public Sub ExistingVerifyScheduleDetectsRealViolationOnKursLevelData()
        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray({"T-A"})},
            {"subjects", New JsonArray({"Fach A"})},
            {"rooms", New JsonArray({"R1"})},
            {"timeslots", New JsonObject From {{"days", New JsonArray({"Mo"})}, {"periods_per_day", 4}}},
            {"kurse", New JsonArray({CType(Kurs("A-GK1", "Fach A", "T-A", "GK", 2), JsonNode)})},
            {"schienen", New JsonArray({CType(Schiene("S1", "GK", 2), JsonNode)})}
        }
        Dim data As New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray({CType(Kurswahl("WP1", {"A-GK1"}), JsonNode)})}
        }

        Dim kb = Kursblockung.SolveKursblockung(data, numWorkers:=1)
        Dim schienenResult = Solver.Solve(Schienenraster.BuildSchienenrasterScenario(data, kb.Assignment), numWorkers:=1)
        Dim raumScenario = Raumzuordnung.BuildRaumzuordnungScenario(data, kb.Assignment, schienenResult.Schedule)
        Dim raumResult = Solver.Solve(raumScenario, numWorkers:=1)
        Assert.IsTrue(raumResult.Status = CpSolverStatus.Optimal OrElse raumResult.Status = CpSolverStatus.Feasible)
        Assert.AreEqual(0, Verifier.VerifySchedule(raumScenario, raumResult.Schedule).Count)

        ' Manipuliere einen Eintrag auf einen Raum, der nicht existiert /
        ' nicht erlaubt ist - der Solver hat das nie so geloest, das ist
        ' ein rein synthetischer Verifier-Test wie
        ' VerifierMussViolationIncludesReasonWhenSet in SolverTests.vb.
        Dim tampered = raumResult.Schedule.Select(Function(e) New ScheduleEntry With {
            .ClassName = e.ClassName, .Subject = e.Subject, .Teacher = e.Teacher,
            .Day = e.Day, .Period = e.Period, .Room = "Nicht-Existierender-Raum"
        }).ToList()
        Dim violations = Verifier.VerifySchedule(raumScenario, tampered)
        Assert.IsTrue(violations.Count > 0)
        Assert.IsTrue(violations.All(Function(v) v.Contains("Nicht-Existierender-Raum")))
    End Sub

End Class
