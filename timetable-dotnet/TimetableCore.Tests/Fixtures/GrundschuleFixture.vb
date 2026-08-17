' Phase 2 scenario A: "Grundschule" - small, on purpose (2 classes, 4
' subjects, 4 teachers, 1 special room), with a deliberately terse,
' telegram-style German prompt (no explanatory sentences, minimal
' punctuation context) - the opposite extreme from the long, structured
' Gymnasium-Klasse-5 prompt. Tests whether the LLM extracts just as
' completely from clipped, elliptical input as from verbose input.
Imports System.Text.Json.Nodes

Public Module GrundschuleFixture

    ' Private (not Public) - GymnasiumKlasse5Fixture already exposes
    ' module-level Classes/Days/PeriodsPerDay, and VB Modules expose their
    ' Public members unqualified project-wide, so a second same-named
    ' Public member here would be ambiguous wherever referenced
    ' unqualified. External code can always derive days/periodsPerDay from
    ' the built scenario's entities via JsonHelpers instead (see
    ' LlmExtractionE2ETests.vb's existing Gymnasium test for the pattern).
    Private ReadOnly Classes As New List(Of String) From {"1a", "1b"}
    Private ReadOnly Days As New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}
    Private Const PeriodsPerDay As Integer = 4

    Public ReadOnly GrundschuleAssignments As New List(Of SubjectAssignment) From {
        New SubjectAssignment("Deutsch", "Frau Berger", New List(Of String) From {"1a", "1b"}, 6, 2, Nothing, Nothing),
        New SubjectAssignment("Mathe", "Herr Klein", New List(Of String) From {"1a", "1b"}, 5, 2, Nothing, Nothing),
        New SubjectAssignment("Sport", "Frau Wolf", New List(Of String) From {"1a", "1b"}, 2, 1, Nothing, New List(Of String) From {"Turnhalle"}),
        New SubjectAssignment("Kunst", "Herr Otto", New List(Of String) From {"1a", "1b"}, 2, 1, Nothing, Nothing)
    }

    ' Ground truth for teacher_availability (not derivable from
    ' GrundschuleAssignments - "Otto nur Mo-Mi da" in the prompt below).
    Public ReadOnly ExpectedUnavailableDays As New Dictionary(Of String, HashSet(Of String)) From {
        {"Herr Otto", New HashSet(Of String) From {"Do", "Fr"}}
    }

    ' Ground truth for forbidden_slot: "Freitags Schluss nach der 3.
    ' Stunde" -> period 4 blocked on Fr for every class.
    Public Function ExpectedForbiddenSlots() As HashSet(Of (Entity As String, Day As String, Period As Integer))
        Dim result As New HashSet(Of (String, String, Integer))
        For Each cls In Classes
            result.Add((cls, "Fr", PeriodsPerDay))
        Next
        Return result
    End Function

    Private Function ExtraConstraints() As List(Of JsonObject)
        Dim result As New List(Of JsonObject) From {
            New JsonObject From {
                {"type", "teacher_availability"}, {"teacher", "Herr Otto"},
                {"available_days", New JsonArray From {"Mo", "Di", "Mi"}}
            }
        }
        For Each cls In Classes
            result.Add(New JsonObject From {
                {"type", "forbidden_slot"}, {"scope", "class"}, {"entity", cls}, {"day", "Fr"}, {"period", PeriodsPerDay}
            })
        Next
        Return result
    End Function

    Public Function BuildGrundschuleScenario() As JsonObject
        Return AssignmentScenarioBuilder.BuildScenario(Classes, Days, PeriodsPerDay, GrundschuleAssignments, ExtraConstraints())
    End Function

    ''' <summary>Public (not tied to the MSTest class) so both
    ''' LlmExtractionE2ETests.vb and RobustnessRunner can call it.</summary>
    Public Function CompletenessReport(extracted As List(Of JsonObject)) As Dictionary(Of String, Double)
        Dim expectedUnavailabilityPairs As New HashSet(Of (String, String))
        For Each kvp In ExpectedUnavailableDays
            For Each d In kvp.Value : expectedUnavailabilityPairs.Add((kvp.Key, d)) : Next
        Next

        Dim scores As New Dictionary(Of String, Double) From {
            {"teacher_subject_assignment", ScoreTeacherSubjectAssignment(AssignmentScenarioBuilder.ExpectedTeacherSubjectAssignments(GrundschuleAssignments), extracted)},
            {"weekly_hours", ScoreWeeklyHours(AssignmentScenarioBuilder.ExpectedWeeklyHours(GrundschuleAssignments), extracted)},
            {"no_overlap", ScoreNoOverlap(AssignmentScenarioBuilder.ExpectedNoOverlap(Classes, GrundschuleAssignments), extracted)},
            {"room_requirement", ScoreRoomRequirement(AssignmentScenarioBuilder.ExpectedRoomRequirement(GrundschuleAssignments), extracted)},
            {"consecutive_required", ScoreConsecutiveRequired(AssignmentScenarioBuilder.ExpectedConsecutiveRequired(GrundschuleAssignments), extracted)},
            {"teacher_availability", ScoreTeacherAvailability(expectedUnavailabilityPairs, extracted, Days, PeriodsPerDay)},
            {"forbidden_slot", ScoreForbiddenSlot(ExpectedForbiddenSlots(), extracted)}
        }
        scores("overall") = OverallScore(scores)
        Return scores
    End Function

    Public ReadOnly Prompt As String =
        "Grundschule, 2 Klassen: 1a, 1b. Mo-Fr, 4 Stunden pro Tag." & vbLf &
        "Deutsch 6h/Woche, max 2/Tag, Frau Berger, beide Klassen." & vbLf &
        "Mathe 5h/Woche, max 2/Tag, Herr Klein, beide Klassen." & vbLf &
        "Sport 2h/Woche, max 1/Tag, Frau Wolf, beide Klassen, immer Turnhalle." & vbLf &
        "Kunst 2h/Woche, max 1/Tag, Herr Otto, beide Klassen." & vbLf &
        "Otto nur Mo-Mi da." & vbLf &
        "Freitags Schluss nach der 3. Stunde, alle Klassen." & vbLf &
        "Ueberschneidungsfreiheit fuer alle Klassen, Lehrer, Raeume." & vbLf &
        "Erzeuge Constraints im JSON-Format fuer den Solver." & vbLf

End Module
