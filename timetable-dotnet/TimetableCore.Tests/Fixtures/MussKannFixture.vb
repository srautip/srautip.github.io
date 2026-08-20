' Phase 2.6: "MussKann" - a dedicated fixture (not woven into the existing,
' already-100%-verified Gymnasium/Grundschule/Oberstufe/EdgeCase scenarios,
' to avoid regressing their baselines) that probes whether the LLM correctly
' infers the new "priority" field (see LlmExtraction.vb's Phase 2.6
' instruction additions) from natural-language Muss/Kann phrasing.
'
' Per Kann-capable type (teacher_availability, weekly_hours.max_per_day,
' room_requirement, forbidden_slot, consecutive_required), exactly 4 test
' patterns:
'   - 2x "should", using different wish-phrasings ("wenn moeglich",
'     "idealerweise"/"bevorzugt"/"nach Moeglichkeit")
'   - 2x "must", one with an explicit reinforcing word ("muss", "unbedingt",
'     "zwingend"), one with NO signal word at all - this second must-case is
'     important: it tests that the model doesn't just default to "should"
'     whenever nothing decisive is said, but genuinely requires wish-language
'     to be present before setting "should".
'
' Systematic (1 teacher, 1 subject, 2 classes) like Grundschule/Oberstufe, so
' built via AssignmentScenarioBuilder rather than hand-rolled like
' EdgeCaseFixture - every block_length/max_per_day/hours_per_week combination
' below is deliberately internally consistent (block_length divides
' hours_per_week, max_per_day >= block_length) so none of the
' consecutive_required test subjects can ever be silently dropped by
' LlmExtraction.vb's DropContradictoryConsecutiveRequired safety net, which
' would otherwise make their priority unscoreable.
Imports System.Text.Json.Nodes

Public Module MussKannFixture

    Private ReadOnly Classes As New List(Of String) From {"7a", "7b"}
    Private ReadOnly Days As New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}
    Private Const PeriodsPerDay As Integer = 7

    ' hours_per_week/max_per_day/block_length triples chosen so every
    ' consecutive_required subject satisfies block_length | hours_per_week
    ' and max_per_day >= block_length.
    Public ReadOnly MussKannAssignments As New List(Of SubjectAssignment) From {
        New SubjectAssignment("Sport", "Frau Berg", New List(Of String) From {"7a", "7b"}, 4, 2, Nothing, New List(Of String) From {"Turnhalle"}),
        New SubjectAssignment("Musik", "Herr Voss", New List(Of String) From {"7a", "7b"}, 2, 1, Nothing, New List(Of String) From {"Musiksaal"}),
        New SubjectAssignment("Kunst", "Frau Klein", New List(Of String) From {"7a", "7b"}, 2, 2, 2, New List(Of String) From {"Kunstraum"}),
        New SubjectAssignment("Chemie", "Herr Adler", New List(Of String) From {"7a", "7b"}, 4, 2, 2, New List(Of String) From {"NaWi-Raum"}),
        New SubjectAssignment("Erdkunde", "Frau Schmidt", New List(Of String) From {"7a", "7b"}, 2, 1, Nothing, Nothing),
        New SubjectAssignment("Biologie", "Herr Fink", New List(Of String) From {"7a", "7b"}, 2, 1, Nothing, Nothing),
        New SubjectAssignment("Sozialkunde", "Frau Wagner", New List(Of String) From {"7a", "7b"}, 2, 2, 2, Nothing),
        New SubjectAssignment("Geschichte", "Herr Baumann", New List(Of String) From {"7a", "7b"}, 2, 2, 2, Nothing)
    }

    ' teacher_availability ground truth (not derivable from
    ' MussKannAssignments) - 2 "should" (wish-phrased), 2 "must" (one
    ' explicit, one bare).
    Public ReadOnly ExpectedUnavailableDays As New Dictionary(Of String, HashSet(Of String)) From {
        {"Frau Schmidt", New HashSet(Of String) From {"Mo", "Mi", "Fr"}},
        {"Herr Voss", New HashSet(Of String) From {"Di", "Do"}},
        {"Frau Berg", New HashSet(Of String) From {"Fr"}},
        {"Herr Adler", New HashSet(Of String) From {"Do", "Fr"}}
    }

    ' forbidden_slot ground truth: 4 school-wide rules (2 "should", 2
    ' "must"), one entry per class each.
    Public Function ExpectedForbiddenSlots() As HashSet(Of (Entity As String, Day As String, Period As Integer))
        Dim result As New HashSet(Of (String, String, Integer))
        For Each cls In Classes
            result.Add((cls, "Mo", 7))   ' should: "wenn moeglich frei bleiben"
            result.Add((cls, "Fr", 6))   ' should: "nach Moeglichkeit frei bleiben"
            result.Add((cls, "Mi", 7))   ' must (explicit): "zwingend frei bleiben"
            result.Add((cls, "Di", 7))   ' must (bare): "findet ... nicht statt"
        Next
        Return result
    End Function

    ''' <summary>The core of this fixture: what priority we expect the LLM
    ''' to infer for each explicitly-tested rule instance. Key shape per
    ''' type mirrors CompletenessScoring.ScorePriorityAccuracy's internal
    ''' PriorityKey exactly: teacher name for teacher_availability, subject
    ''' for room_requirement, (class, subject) for weekly_hours/
    ''' consecutive_required, (scope, entity, day, period) for
    ''' forbidden_slot. Class-scoped types get one row per class, so this
    ''' has more rows than the 20 distinct prompt sentences.</summary>
    Public Function ExpectedPriorities() As List(Of (ConstraintType As String, Key As Object, ExpectedPriority As String))
        Dim result As New List(Of (String, Object, String))

        result.Add(("teacher_availability", CObj("Frau Schmidt"), JsonHelpers.PriorityShould))
        result.Add(("teacher_availability", CObj("Herr Voss"), JsonHelpers.PriorityShould))
        result.Add(("teacher_availability", CObj("Frau Berg"), JsonHelpers.PriorityMust))
        result.Add(("teacher_availability", CObj("Herr Adler"), JsonHelpers.PriorityMust))

        For Each cls In Classes
            result.Add(("weekly_hours", CObj((cls, "Sport")), JsonHelpers.PriorityShould))
            result.Add(("weekly_hours", CObj((cls, "Erdkunde")), JsonHelpers.PriorityShould))
            result.Add(("weekly_hours", CObj((cls, "Kunst")), JsonHelpers.PriorityMust))
            result.Add(("weekly_hours", CObj((cls, "Biologie")), JsonHelpers.PriorityMust))
        Next

        result.Add(("room_requirement", CObj("Sport"), JsonHelpers.PriorityShould))
        result.Add(("room_requirement", CObj("Musik"), JsonHelpers.PriorityShould))
        result.Add(("room_requirement", CObj("Kunst"), JsonHelpers.PriorityMust))
        result.Add(("room_requirement", CObj("Chemie"), JsonHelpers.PriorityMust))

        For Each cls In Classes
            result.Add(("forbidden_slot", CObj(("class", cls, "Mo", 7)), JsonHelpers.PriorityShould))
            result.Add(("forbidden_slot", CObj(("class", cls, "Fr", 6)), JsonHelpers.PriorityShould))
            result.Add(("forbidden_slot", CObj(("class", cls, "Mi", 7)), JsonHelpers.PriorityMust))
            result.Add(("forbidden_slot", CObj(("class", cls, "Di", 7)), JsonHelpers.PriorityMust))
        Next

        For Each cls In Classes
            result.Add(("consecutive_required", CObj((cls, "Kunst")), JsonHelpers.PriorityShould))
            result.Add(("consecutive_required", CObj((cls, "Sozialkunde")), JsonHelpers.PriorityShould))
            result.Add(("consecutive_required", CObj((cls, "Chemie")), JsonHelpers.PriorityMust))
            result.Add(("consecutive_required", CObj((cls, "Geschichte")), JsonHelpers.PriorityMust))
        Next

        Return result
    End Function

    Private Function ExtraConstraints() As List(Of JsonObject)
        Dim result As New List(Of JsonObject)
        For Each kvp In ExpectedUnavailableDays
            Dim available = Days.Where(Function(d) Not kvp.Value.Contains(d)).ToList()
            result.Add(New JsonObject From {
                {"type", "teacher_availability"}, {"teacher", kvp.Key},
                {"available_days", New JsonArray(available.Select(Function(d) CType(d, JsonNode)).ToArray())}
            })
        Next
        For Each cls In Classes
            For Each dp In New List(Of (Day As String, Period As Integer)) From {("Mo", 7), ("Fr", 6), ("Mi", 7), ("Di", 7)}
                result.Add(New JsonObject From {
                    {"type", "forbidden_slot"}, {"scope", "class"}, {"entity", cls}, {"day", dp.Day}, {"period", dp.Period}
                })
            Next
        Next
        Return result
    End Function

    Public Function BuildMussKannScenario() As JsonObject
        Return AssignmentScenarioBuilder.BuildScenario(Classes, Days, PeriodsPerDay, MussKannAssignments, ExtraConstraints())
    End Function

    ''' <summary>Public (not tied to the MSTest class) so both
    ''' LlmExtractionE2ETests.vb and RobustnessRunner can call it.</summary>
    Public Function CompletenessReport(extracted As List(Of JsonObject)) As Dictionary(Of String, Double)
        Dim expectedUnavailabilityPairs As New HashSet(Of (String, String))
        For Each kvp In ExpectedUnavailableDays
            For Each d In kvp.Value : expectedUnavailabilityPairs.Add((kvp.Key, d)) : Next
        Next

        Dim scores As New Dictionary(Of String, Double) From {
            {"teacher_subject_assignment", ScoreTeacherSubjectAssignment(AssignmentScenarioBuilder.ExpectedTeacherSubjectAssignments(MussKannAssignments), extracted)},
            {"weekly_hours", ScoreWeeklyHours(AssignmentScenarioBuilder.ExpectedWeeklyHours(MussKannAssignments), extracted)},
            {"no_overlap", ScoreNoOverlap(AssignmentScenarioBuilder.ExpectedNoOverlap(Classes, MussKannAssignments), extracted)},
            {"room_requirement", ScoreRoomRequirement(AssignmentScenarioBuilder.ExpectedRoomRequirement(MussKannAssignments), extracted)},
            {"consecutive_required", ScoreConsecutiveRequired(AssignmentScenarioBuilder.ExpectedConsecutiveRequired(MussKannAssignments), extracted)},
            {"teacher_availability", ScoreTeacherAvailability(expectedUnavailabilityPairs, extracted, Days, PeriodsPerDay)},
            {"forbidden_slot", ScoreForbiddenSlot(ExpectedForbiddenSlots(), extracted)},
            {"priority_accuracy", CompletenessScoring.ScorePriorityAccuracy(ExpectedPriorities(), extracted)}
        }
        scores("overall") = OverallScore(scores)
        Return scores
    End Function

    Public ReadOnly Prompt As String =
        "An unserer Realschule gibt es zwei Klassen: 7a und 7b. Der Unterricht" & vbLf &
        "findet montags bis freitags mit je 7 Stunden pro Tag statt." & vbLf & vbLf &
        "Faecher:" & vbLf &
        "- Sport (4 Stunden/Woche): Frau Berg unterrichtet 7a und 7b. Sport soll" & vbLf &
        "  wenn moeglich hoechstens 2 Stunden pro Tag stattfinden. Sport findet" & vbLf &
        "  wenn moeglich in der Turnhalle statt." & vbLf &
        "- Musik (2 Stunden/Woche, hoechstens 1 Stunde pro Tag): Herr Voss" & vbLf &
        "  unterrichtet 7a und 7b. Musik findet idealerweise im Musiksaal statt." & vbLf &
        "- Kunst (2 Stunden/Woche): Frau Klein unterrichtet 7a und 7b. Kunst muss" & vbLf &
        "  auf jeden Fall hoechstens 2 Stunden pro Tag stattfinden. Kunst muss im" & vbLf &
        "  Kunstraum stattfinden. Kunst findet wenn moeglich als Doppelstunde statt." & vbLf &
        "- Chemie (4 Stunden/Woche, hoechstens 2 pro Tag): Herr Adler unterrichtet" & vbLf &
        "  7a und 7b. Chemie findet im NaWi-Raum statt. Chemie muss immer" & vbLf &
        "  als Doppelstunde unterrichtet werden." & vbLf &
        "- Erdkunde (2 Stunden/Woche): Frau Schmidt unterrichtet 7a und 7b." & vbLf &
        "  Erdkunde soll nach Moeglichkeit hoechstens 1 Stunde pro Tag stattfinden." & vbLf &
        "- Biologie (2 Stunden/Woche): Herr Fink unterrichtet 7a und 7b. Biologie" & vbLf &
        "  hat hoechstens 1 Stunde pro Tag." & vbLf &
        "- Sozialkunde (2 Stunden/Woche, hoechstens 2 pro Tag): Frau Wagner" & vbLf &
        "  unterrichtet 7a und 7b. Sozialkunde findet idealerweise als" & vbLf &
        "  Doppelstunde statt." & vbLf &
        "- Geschichte (2 Stunden/Woche, hoechstens 2 pro Tag): Herr Baumann" & vbLf &
        "  unterrichtet 7a und 7b. Geschichte findet als Doppelstunde statt." & vbLf & vbLf &
        "Verfuegbarkeit:" & vbLf &
        "- Frau Schmidt ist bevorzugt dienstags und donnerstags an der Schule." & vbLf &
        "- Herr Voss ist idealerweise montags, mittwochs und freitags im Haus." & vbLf &
        "- Frau Berg ist unbedingt nur montags bis donnerstags an der Schule" & vbLf &
        "  verfuegbar." & vbLf &
        "- Herr Adler ist nur montags, dienstags und mittwochs an der Schule" & vbLf &
        "  taetig." & vbLf & vbLf &
        "Sperrzeiten:" & vbLf &
        "- Am Montag soll die 7. Stunde fuer alle Klassen wenn moeglich frei" & vbLf &
        "  bleiben." & vbLf &
        "- Am Freitag soll die 6. Stunde fuer alle Klassen nach Moeglichkeit frei" & vbLf &
        "  bleiben." & vbLf &
        "- Am Mittwoch muss die 7. Stunde fuer alle Klassen zwingend frei bleiben." & vbLf &
        "- Am Dienstag findet die 7. Stunde fuer alle Klassen nicht statt." & vbLf & vbLf &
        "Zusaetzlich gilt fuer alle Klassen, Lehrkraefte und Fachraeume die" & vbLf &
        "uebliche Ueberschneidungsfreiheit." & vbLf & vbLf &
        "Erzeuge daraus die passenden Constraints im vereinbarten JSON-Format" & vbLf &
        "fuer den CP-SAT-Solver." & vbLf

End Module
