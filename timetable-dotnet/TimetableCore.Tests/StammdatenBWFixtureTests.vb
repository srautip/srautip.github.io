' Phase 2.15f: End-to-End-Sanity-Tests fuer die neue Gesamtpipeline
' (Stammdaten -> Lehrereinsatzplanung -> BuildAssignmentConstraints ->
' UNVERAENDERTER Solver.Solve) auf den beiden neuen BW-Referenz-
' Stammdatensaetzen - LLM-frei, analog RealSchoolFixtureTests.vb's Muster.
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class StammdatenBWFixtureTests

    Private Sub RunEndToEnd(bestand As Stammdatenbestand, lehrereinsatzTimeLimitS As Double, solveTimeLimitS As Double)
        Assert.AreEqual(0, StammdatenValidation.ValidateStammdaten(bestand).Count)

        Dim lehrereinsatz = Lehrereinsatzplanung.SolveLehrereinsatz(bestand, timeLimitS:=lehrereinsatzTimeLimitS)
        Assert.IsTrue(lehrereinsatz.Status = CpSolverStatus.Optimal OrElse lehrereinsatz.Status = CpSolverStatus.Feasible, lehrereinsatz.Status.ToString())

        Dim lehrereinsatzViolations = Verifier.VerifyLehrereinsatz(bestand, lehrereinsatz)
        Assert.AreEqual(0, lehrereinsatzViolations.Count, String.Join(vbLf, lehrereinsatzViolations))

        Dim assignmentConstraints = Lehrereinsatzplanung.BuildAssignmentConstraints(lehrereinsatz, bestand)
        Dim ent = Stammdaten.BuildEntitiesFragment(bestand)
        Dim data As New Text.Json.Nodes.JsonObject From {
            {"entities", ent},
            {"constraints", New Text.Json.Nodes.JsonArray(assignmentConstraints.Select(Function(c) CType(c, Text.Json.Nodes.JsonNode)).ToArray())}
        }
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count)

        Dim result = Solver.Solve(data, timeLimitS:=solveTimeLimitS)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())

        Dim scheduleViolations = Verifier.VerifySchedule(data, result.Schedule)
        Assert.AreEqual(0, scheduleViolations.Count, String.Join(vbLf, scheduleViolations))
    End Sub

    ''' <summary>Live gemessen: Lehrereinsatzplanung + Solver.Solve
    ''' zusammen unter 5s fuer die 8-Klassen-Grundschule.</summary>
    <TestMethod>
    Public Sub GrundschuleEndToEndSolvesAndVerifiesClean()
        RunEndToEnd(StammdatenBWFixture.BuildBWGrundschule(), lehrereinsatzTimeLimitS:=30, solveTimeLimitS:=30)
    End Sub

    ''' <summary>Live gemessen: Lehrereinsatzplanung + Solver.Solve
    ''' zusammen deutlich unter 60s fuer die 12-Klassen-
    ''' Gemeinschaftsschule.</summary>
    <TestMethod>
    Public Sub GemeinschaftsschuleEndToEndSolvesAndVerifiesClean()
        RunEndToEnd(StammdatenBWFixture.BuildBWGemeinschaftsschule(), lehrereinsatzTimeLimitS:=60, solveTimeLimitS:=60)
    End Sub

End Class
