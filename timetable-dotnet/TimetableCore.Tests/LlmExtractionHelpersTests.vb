' Ported 1:1 from tests/test_llm_extraction_helpers.py. Fast, deterministic
' tests for the pure-code parts of LlmExtraction.vb (no Ollama needed -
' these don't call the LLM at all).
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class LlmExtractionHelpersTests

    <TestMethod>
    Public Sub ExpandPeriodExceptionBlocksEveryDayExceptAllowed()
        Dim entities As New JsonObject From {
            {"classes", New JsonArray From {"5a", "5b"}},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray From {"Mo", "Di", "Mi", "Do", "Fr"}},
                {"periods_per_day", 7}
            }}
        }
        Dim item As New JsonObject From {
            {"type", "period_exception"}, {"period", 7}, {"allowed_days", New JsonArray From {"Di"}}
        }

        Dim result = LlmExtraction.ExpandPeriodException(entities, item)

        Assert.AreEqual(4 * 2, result.Count) ' 4 blocked days x 2 classes
        Dim daysSeen = New HashSet(Of String)(result.Select(Function(c) JsonHelpers.GetString(c, "day")))
        CollectionAssert.AreEquivalent({"Mo", "Mi", "Do", "Fr"}, daysSeen.ToList())
        Assert.IsTrue(result.All(Function(c) JsonHelpers.GetInt(c, "period").Value = 7))
        Assert.IsTrue(result.All(Function(c) JsonHelpers.GetString(c, "type") = "forbidden_slot" AndAlso JsonHelpers.GetString(c, "scope") = "class"))
        Dim classesSeen = New HashSet(Of String)(result.Select(Function(c) JsonHelpers.GetString(c, "entity")))
        CollectionAssert.AreEquivalent({"5a", "5b"}, classesSeen.ToList())
    End Sub

    <TestMethod>
    Public Sub ExpandPeriodExceptionMultipleAllowedDays()
        Dim entities As New JsonObject From {
            {"classes", New JsonArray From {"5a"}},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray From {"Mo", "Di", "Mi", "Do", "Fr"}},
                {"periods_per_day", 7}
            }}
        }
        Dim item As New JsonObject From {
            {"type", "period_exception"}, {"period", 7}, {"allowed_days", New JsonArray From {"Mo", "Di"}}
        }

        Dim result = LlmExtraction.ExpandPeriodException(entities, item)

        Dim daysSeen = New HashSet(Of String)(result.Select(Function(c) JsonHelpers.GetString(c, "day")))
        CollectionAssert.AreEquivalent({"Mi", "Do", "Fr"}, daysSeen.ToList())
    End Sub

    <TestMethod>
    Public Sub ExpandPeriodExceptionNoAllowedDaysBlocksEverything()
        Dim entities As New JsonObject From {
            {"classes", New JsonArray From {"5a"}},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray From {"Mo", "Di"}},
                {"periods_per_day", 7}
            }}
        }
        Dim item As New JsonObject From {
            {"type", "period_exception"}, {"period", 7}, {"allowed_days", New JsonArray()}
        }

        Dim result = LlmExtraction.ExpandPeriodException(entities, item)

        Dim daysSeen = New HashSet(Of String)(result.Select(Function(c) JsonHelpers.GetString(c, "day")))
        CollectionAssert.AreEquivalent({"Mo", "Di"}, daysSeen.ToList())
    End Sub

End Class
