' Phase 2.11g: deterministic (no LLM) tests for
' CompletenessScoring.ScoreKurswahl/ScoreKurswahlStudentCount, mirroring
' the existing Score* function tests' style (hand-built extracted lists,
' no fixture/solver involved).
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class KurswahlScoringTests

    Private Shared Function Kurswahl(wahlprofilId As String, studentCount As Integer, kurse As IEnumerable(Of String)) As JsonObject
        Return New JsonObject From {
            {"type", "kurswahl"}, {"wahlprofil_id", wahlprofilId}, {"student_count", studentCount},
            {"kurse", New JsonArray(kurse.Select(Function(k) CType(k, JsonNode)).ToArray())}
        }
    End Function

    <TestMethod>
    Public Sub ScoreKurswahlIsOneWhenExactSetMatches()
        Dim expected = New Dictionary(Of String, List(Of String)) From {
            {"WP1", New List(Of String) From {"BIO-LK1", "D-LK1", "MA-LK1"}}
        }
        Dim extracted = New List(Of JsonObject) From {Kurswahl("WP1", 24, {"D-LK1", "MA-LK1", "BIO-LK1"})}
        Assert.AreEqual(1.0, CompletenessScoring.ScoreKurswahl(expected, extracted))
    End Sub

    <TestMethod>
    Public Sub ScoreKurswahlIsZeroWhenOneKursIsWrong()
        Dim expected = New Dictionary(Of String, List(Of String)) From {
            {"WP1", New List(Of String) From {"BIO-LK1", "D-LK1", "MA-LK1"}}
        }
        Dim extracted = New List(Of JsonObject) From {Kurswahl("WP1", 24, {"D-LK1", "MA-LK1", "EN-LK1"})}
        Assert.AreEqual(0.0, CompletenessScoring.ScoreKurswahl(expected, extracted))
    End Sub

    <TestMethod>
    Public Sub ScoreKurswahlIsZeroWhenWahlprofilMissingEntirely()
        Dim expected = New Dictionary(Of String, List(Of String)) From {
            {"WP1", New List(Of String) From {"D-LK1"}}, {"WP2", New List(Of String) From {"MA-LK1"}}
        }
        Dim extracted = New List(Of JsonObject) From {Kurswahl("WP1", 24, {"D-LK1"})}
        Assert.AreEqual(0.5, CompletenessScoring.ScoreKurswahl(expected, extracted))
    End Sub

    <TestMethod>
    Public Sub ScoreKurswahlStudentCountIsIndependentOfKursSet()
        Dim expected = New Dictionary(Of String, Integer) From {{"WP1", 24}}
        ' Kurs-Set ist hier absichtlich falsch - ScoreKurswahlStudentCount
        ' soll das ignorieren und nur student_count pruefen.
        Dim extracted = New List(Of JsonObject) From {Kurswahl("WP1", 24, {"NICHT-DIE-ERWARTETEN-KURSE"})}
        Assert.AreEqual(1.0, CompletenessScoring.ScoreKurswahlStudentCount(expected, extracted))
    End Sub

    <TestMethod>
    Public Sub ScoreKurswahlStudentCountIsZeroWhenWrong()
        Dim expected = New Dictionary(Of String, Integer) From {{"WP1", 24}}
        Dim extracted = New List(Of JsonObject) From {Kurswahl("WP1", 20, {"D-LK1"})}
        Assert.AreEqual(0.0, CompletenessScoring.ScoreKurswahlStudentCount(expected, extracted))
    End Sub

End Class
