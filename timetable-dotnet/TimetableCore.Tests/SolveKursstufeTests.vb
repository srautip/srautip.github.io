' Phase 2.11d: end-to-end tests for the Solver.SolveKursstufe orchestrator
' (Kursblockung -> Schienenraster -> Raumzuordnung). Individual stages
' already have dedicated live tests (KursblockungTests.vb,
' SchienenrasterTests.vb, RaumzuordnungTests.vb) - these tests instead
' prove the WIRING: correct KursId propagation into the final Schedule,
' and correct "stop at the first failing stage" behavior.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class SolveKursstufeTests

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
    Public Sub HappyPathSolvesAllThreeStagesWithRealKursIdentities()
        Dim data = HappyPathScenario()
        Assert.AreEqual(0, Validation.ValidateKursstufeEntities(data).Count)

        Dim r = Solver.SolveKursstufe(data, numWorkers:=1)

        Assert.IsTrue(r.KursblockungStatus = CpSolverStatus.Optimal OrElse r.KursblockungStatus = CpSolverStatus.Feasible)
        Assert.IsTrue(r.SchienenrasterStatus = CpSolverStatus.Optimal OrElse r.SchienenrasterStatus = CpSolverStatus.Feasible)
        Assert.IsTrue(r.RaumzuordnungStatus = CpSolverStatus.Optimal OrElse r.RaumzuordnungStatus = CpSolverStatus.Feasible)
        Assert.IsNotNull(r.Schedule)

        Dim realKursIds = New HashSet(Of String) From {"D-LK1", "MA-LK1", "BIO-LK1", "EN-GK1"}
        Assert.IsTrue(r.Schedule.All(Function(e) realKursIds.Contains(e.ClassName)))
        Assert.IsFalse(r.Schedule.Any(Function(e) e.ClassName = "Kursstufe" OrElse e.Teacher = "_schiene"))

        Dim byKurs = r.Schedule.GroupBy(Function(e) e.ClassName).ToDictionary(Function(g) g.Key, Function(g) g.Count())
        Assert.AreEqual(5, byKurs("D-LK1"))
        Assert.AreEqual(5, byKurs("MA-LK1"))
        Assert.AreEqual(5, byKurs("BIO-LK1"))
        Assert.AreEqual(3, byKurs("EN-GK1"))

        ' Jeder Eintrag bekam auch tatsaechlich einen der 2 Raeume.
        Assert.IsTrue(r.Schedule.All(Function(e) e.Room = "R1" OrElse e.Room = "R2"))
    End Sub

    ''' <summary>Kursblockung selbst ist schon Infeasible (4 LK-Kurse in
    ''' einem Wahlprofil, nur 3 LK-Schienen) - SolveKursstufe darf gar
    ''' nicht erst versuchen, Stufe B/C zu loesen.</summary>
    <TestMethod>
    Public Sub StopsAtKursblockungWhenThatStageIsInfeasible()
        Dim data = HappyPathScenario()
        CType(JsonHelpers.Entities(data)("kurse"), JsonArray).Add(Kurs("EN-LK1", "Englisch", "T-Englisch", "LK", 5))
        CType(data("constraints")(0), JsonObject)("kurse") = New JsonArray({"D-LK1", "MA-LK1", "BIO-LK1", "EN-LK1"})

        Dim r = Solver.SolveKursstufe(data, numWorkers:=1)

        Assert.AreEqual(CpSolverStatus.Infeasible, r.KursblockungStatus)
        Assert.IsNull(r.SchienenrasterStatus)
        Assert.IsNull(r.RaumzuordnungStatus)
        Assert.IsNull(r.Schedule)
    End Sub

    ''' <summary>Kursblockung ist trivial loesbar (1 Kurs, 1 Schiene), aber
    ''' die Schienen brauchen insgesamt mehr Wochenstunden als das Raster
    ''' Slots hat (1 Tag x 1 Periode = 1 Slot, aber hours_per_week=5) -
    ''' Stufe B (Schienenraster) muss Infeasible werden, Stufe C darf gar
    ''' nicht erst versucht werden.</summary>
    <TestMethod>
    Public Sub StopsAtSchienenrasterWhenThatStageIsInfeasible()
        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray({"T-A"})},
            {"subjects", New JsonArray({"Fach A"})},
            {"rooms", New JsonArray()},
            {"timeslots", New JsonObject From {{"days", New JsonArray({"Mo"})}, {"periods_per_day", 1}}},
            {"kurse", New JsonArray({CType(Kurs("A-LK1", "Fach A", "T-A", "LK", 5), JsonNode)})},
            {"schienen", New JsonArray({CType(Schiene("S1", "LK", 5), JsonNode)})}
        }
        Dim data As New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray({CType(Kurswahl("WP1", {"A-LK1"}), JsonNode)})}
        }

        Dim r = Solver.SolveKursstufe(data, numWorkers:=1)

        Assert.IsTrue(r.KursblockungStatus = CpSolverStatus.Optimal OrElse r.KursblockungStatus = CpSolverStatus.Feasible)
        Assert.AreEqual(CpSolverStatus.Infeasible, r.SchienenrasterStatus)
        Assert.IsNull(r.RaumzuordnungStatus)
        Assert.IsNull(r.Schedule)
    End Sub

End Class
