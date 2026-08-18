' Phase 2.11b: live tests against the real Google.OrTools.Sat 9.15.6755
' installation for the new Kursblockung CP-SAT model shape (course-to-
' Schiene assignment). No new OrTools primitives are used here (only
' NewBoolVar/LinearExpr.Sum/model.Add, all already verified elsewhere in
' this project) - these tests instead serve as the project's established
' "solve a small hand-computed model and check the result" discipline for
' a NEW model *shape*, mirroring Phase 2.8/2.9's smoke tests.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class KursblockungTests

    Private Shared Function Kurs(id As String, subject As String, teacher As String,
                                  kursart As String, hoursPerWeek As Integer) As JsonObject
        Return New JsonObject From {
            {"id", id}, {"subject", subject}, {"teacher", teacher},
            {"kursart", kursart}, {"hours_per_week", hoursPerWeek}
        }
    End Function

    Private Shared Function Schiene(id As String, kursart As String, hoursPerWeek As Integer, Optional capacity As Integer? = Nothing) As JsonObject
        Dim o As New JsonObject From {
            {"id", id}, {"kursart", kursart}, {"hours_per_week", hoursPerWeek}
        }
        If capacity.HasValue Then o("capacity") = capacity.Value
        Return o
    End Function

    Private Shared Function Kurswahl(wahlprofilId As String, kurse As IEnumerable(Of String)) As JsonObject
        Return New JsonObject From {
            {"type", "kurswahl"}, {"wahlprofil_id", wahlprofilId}, {"student_count", 20},
            {"kurse", New JsonArray(kurse.Select(Function(k) CType(k, JsonNode)).ToArray())}
        }
    End Function

    Private Shared Function BuildData(kurse As IEnumerable(Of JsonObject), schienen As IEnumerable(Of JsonObject),
                                  teachers As IEnumerable(Of String), subjects As IEnumerable(Of String),
                                  kurswahlen As IEnumerable(Of JsonObject)) As JsonObject
        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray(teachers.Select(Function(t) CType(t, JsonNode)).ToArray())},
            {"subjects", New JsonArray(subjects.Select(Function(s) CType(s, JsonNode)).ToArray())},
            {"rooms", New JsonArray()},
            {"timeslots", New JsonObject From {{"days", New JsonArray({"Mo"})}, {"periods_per_day", 8}}},
            {"kurse", New JsonArray(kurse.Select(Function(k) CType(k, JsonNode)).ToArray())},
            {"schienen", New JsonArray(schienen.Select(Function(s) CType(s, JsonNode)).ToArray())}
        }
        Return New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray(kurswahlen.Select(Function(c) CType(c, JsonNode)).ToArray())}
        }
    End Function

    ''' <summary>3 LK-Schienen (exactly matching the 3 required
    ''' Leistungskurse), 1 GK-Schiene, one Wahlprofil holding all 4 Kurse.
    ''' Hand-computed expectation: the 3 LK-Kurse must land on 3 DISTINCT
    ''' LK-Schienen (there's no other way to satisfy "1 Kurs pro Schiene" +
    ''' "hoechstens 1 eigener Kurs pro Schiene je Wahlprofil" with exactly 3
    ''' LK-Kurse and 3 LK-Schienen), and the GK-Kurs must land on the one
    ''' GK-Schiene (S4, its only compatible option) - asserted structurally
    ''' since which of S1/S2/S3 each LK-Kurs gets is symmetric/unconstrained.</summary>
    <TestMethod>
    Public Sub FeasibleToyScenarioAssignsDistinctSchienenPerWahlprofil()
        Dim data = BuildData(
            {Kurs("D-LK1", "Deutsch", "T-Deutsch", "LK", 5),
             Kurs("MA-LK1", "Mathematik", "T-Mathe", "LK", 5),
             Kurs("BIO-LK1", "Biologie", "T-Bio", "LK", 5),
             Kurs("MA-GK1", "Mathematik", "T-Mathe", "GK", 3)},
            {Schiene("S1", "LK", 5), Schiene("S2", "LK", 5), Schiene("S3", "LK", 5), Schiene("S4", "GK", 3)},
            {"T-Deutsch", "T-Mathe", "T-Bio"}, {"Deutsch", "Mathematik", "Biologie"},
            {Kurswahl("WP1", {"D-LK1", "MA-LK1", "BIO-LK1", "MA-GK1"})})

        Dim result = Kursblockung.SolveKursblockung(data, numWorkers:=1)

        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(4, result.Assignment.Count)

        Dim lkSchienen = {result.Assignment("D-LK1"), result.Assignment("MA-LK1"), result.Assignment("BIO-LK1")}
        Assert.AreEqual(3, lkSchienen.Distinct().Count(), "die 3 LK-Kurse muessen auf 3 verschiedene Schienen fallen")
        For Each s In lkSchienen
            Assert.IsTrue({"S1", "S2", "S3"}.Contains(s))
        Next
        Assert.AreEqual("S4", result.Assignment("MA-GK1"))
    End Sub

    <TestMethod>
    Public Sub DeterministicAcrossRepeatedCalls()
        Dim data = BuildData(
            {Kurs("D-LK1", "Deutsch", "T-Deutsch", "LK", 5),
             Kurs("MA-LK1", "Mathematik", "T-Mathe", "LK", 5),
             Kurs("BIO-LK1", "Biologie", "T-Bio", "LK", 5)},
            {Schiene("S1", "LK", 5), Schiene("S2", "LK", 5), Schiene("S3", "LK", 5)},
            {"T-Deutsch", "T-Mathe", "T-Bio"}, {"Deutsch", "Mathematik", "Biologie"},
            {Kurswahl("WP1", {"D-LK1", "MA-LK1", "BIO-LK1"})})

        Dim r1 = Kursblockung.SolveKursblockung(data, numWorkers:=1)
        Dim r2 = Kursblockung.SolveKursblockung(data, numWorkers:=1)

        CollectionAssert.AreEqual(
            r1.Assignment.OrderBy(Function(kv) kv.Key).ToList(),
            r2.Assignment.OrderBy(Function(kv) kv.Key).ToList())
    End Sub

    ''' <summary>Pigeonhole: one Wahlprofil chooses 4 Leistungskurse but
    ''' only 3 LK-Schienen exist - four courses can never fit into three
    ''' Schienen with at most one of this profile's own courses per
    ''' Schiene, regardless of which teachers teach them. Must be
    ''' Infeasible, not silently drop a course.</summary>
    <TestMethod>
    Public Sub InfeasibleWhenWahlprofilHasMoreLKCoursesThanLKSchienen()
        Dim data = BuildData(
            {Kurs("D-LK1", "Deutsch", "T-Deutsch", "LK", 5),
             Kurs("MA-LK1", "Mathematik", "T-Mathe", "LK", 5),
             Kurs("BIO-LK1", "Biologie", "T-Bio", "LK", 5),
             Kurs("EN-LK1", "Englisch", "T-Englisch", "LK", 5)},
            {Schiene("S1", "LK", 5), Schiene("S2", "LK", 5), Schiene("S3", "LK", 5)},
            {"T-Deutsch", "T-Mathe", "T-Bio", "T-Englisch"},
            {"Deutsch", "Mathematik", "Biologie", "Englisch"},
            {Kurswahl("WP1", {"D-LK1", "MA-LK1", "BIO-LK1", "EN-LK1"})})

        Dim result = Kursblockung.SolveKursblockung(data, numWorkers:=1)

        Assert.AreEqual(CpSolverStatus.Infeasible, result.Status)
        Assert.IsNull(result.Assignment)
    End Sub

    ''' <summary>Pigeonhole for the teacher-collision constraint in
    ''' isolation (no Wahlprofil involved): two GK-Kurse taught by the SAME
    ''' teacher, but only one GK-Schiene exists - the "1 Kurs pro Schiene"
    ''' constraint forces both into that single Schiene, which the
    ''' teacher-collision constraint then forbids. Must be Infeasible.</summary>
    <TestMethod>
    Public Sub InfeasibleWhenSameTeacherHasTwoCoursesButOnlyOneCompatibleSchiene()
        Dim data = BuildData(
            {Kurs("A-GK1", "Fach A", "T-X", "GK", 3),
             Kurs("B-GK1", "Fach B", "T-X", "GK", 3)},
            {Schiene("S1", "GK", 3)},
            {"T-X"}, {"Fach A", "Fach B"},
            {})

        Dim result = Kursblockung.SolveKursblockung(data, numWorkers:=1)

        Assert.AreEqual(CpSolverStatus.Infeasible, result.Status)
        Assert.IsNull(result.Assignment)
    End Sub

    ''' <summary>Phase 2.11h: pigeonhole for the new "capacity" field
    ''' (max simultaneous Kurse per Schiene, e.g. a room-count limit).
    ''' Two GK-Kurse, different subjects/teachers/Wahlprofile (so nothing
    ''' ELSE would block them sharing a Schiene), but the only compatible
    ''' Schiene has capacity=1 - must be Infeasible on capacity alone.</summary>
    <TestMethod>
    Public Sub InfeasibleWhenSchieneCapacityIsExceededWithNoAlternative()
        Dim data = BuildData(
            {Kurs("A-GK1", "Fach A", "T-A", "GK", 3),
             Kurs("B-GK1", "Fach B", "T-B", "GK", 3)},
            {Schiene("S1", "GK", 3, capacity:=1)},
            {"T-A", "T-B"}, {"Fach A", "Fach B"},
            {Kurswahl("WP1", {"A-GK1"}), Kurswahl("WP2", {"B-GK1"})})

        Dim result = Kursblockung.SolveKursblockung(data, numWorkers:=1)

        Assert.AreEqual(CpSolverStatus.Infeasible, result.Status)
        Assert.IsNull(result.Assignment)
    End Sub

    ''' <summary>Same two Kurse as above, but now 2 Schienen each with
    ''' capacity=1 - nothing else forces them apart (different
    ''' Wahlprofile, different teachers), so capacity alone must force
    ''' each Kurs onto a DIFFERENT Schiene.</summary>
    <TestMethod>
    Public Sub CapacityForcesDistinctSchienenWithNoOtherConstraintDoingSo()
        Dim data = BuildData(
            {Kurs("A-GK1", "Fach A", "T-A", "GK", 3),
             Kurs("B-GK1", "Fach B", "T-B", "GK", 3)},
            {Schiene("S1", "GK", 3, capacity:=1), Schiene("S2", "GK", 3, capacity:=1)},
            {"T-A", "T-B"}, {"Fach A", "Fach B"},
            {Kurswahl("WP1", {"A-GK1"}), Kurswahl("WP2", {"B-GK1"})})

        Dim result = Kursblockung.SolveKursblockung(data, numWorkers:=1)

        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible)
        Assert.AreNotEqual(result.Assignment("A-GK1"), result.Assignment("B-GK1"))
    End Sub

    ''' <summary>A Schiene without "capacity" set stays uncapped - byte-
    ''' identical to every capacity-unaware test above/before this
    ''' feature existed. Piles 5 mutually-compatible Kurse (no Wahlprofil/
    ''' teacher conflicts) onto the one available Schiene and expects that
    ''' to succeed without any capacity field present.</summary>
    <TestMethod>
    Public Sub SchieneWithoutCapacityFieldStaysUncapped()
        Dim kurse = Enumerable.Range(1, 5).Select(Function(i) Kurs($"K{i}", $"Fach{i}", $"T{i}", "GK", 2)).ToList()
        Dim data = BuildData(
            kurse, {Schiene("S1", "GK", 2)},
            kurse.Select(Function(k) JsonHelpers.GetString(k, "teacher")),
            kurse.Select(Function(k) JsonHelpers.GetString(k, "subject")),
            {})

        Dim result = Kursblockung.SolveKursblockung(data, numWorkers:=1)

        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible)
        Assert.IsTrue(kurse.All(Function(k) result.Assignment(JsonHelpers.GetString(k, "id")) = "S1"))
    End Sub

End Class
