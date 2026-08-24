' Phase 2 scenario C: "Edge-Case" - small on purpose (2 classes, 3
' subjects), so Solve stays fast and the focus is entirely on extraction.
' Hand-curated ground truth (like FullScenarioFixture.vb), not derived from
' an assignment table like GrundschuleFixture/OberstufeFixture, since each
' constraint here exists to probe one specific, deliberately risky
' phrasing pattern rather than to model a realistic school:
'
' 1. TWO independent period_exception rules in the same prompt ("6. Stunde
'    nur montags" AND "8. Stunde nur donnerstags") - tests whether the LLM
'    keeps them separate instead of conflating the two allowed-day sets.
' 2. teacher_availability phrased as a NEGATION ("kann an keinem Tag
'    ausser Montag und Mittwoch unterrichten") instead of the positive
'    "ist nur an X verfuegbar" phrasing used elsewhere. Stresses the same
'    day-complement reasoning the period_exception type was built to avoid
'    - but teacher_availability has NO deterministic expansion helper, so
'    a failure here is a candidate for a new one.
' 3. A forbidden_slot for TWO periods in one sentence ("die 3. und 4.
'    Stunde ... frei") - tests whether the LLM emits one entry per period
'    (x per class) instead of just one.
' 4. An IMPLICIT number instead of an explicit value ("nie mehr als eine
'    Doppelstunde pro Tag" must be inferred as max_per_day=2 - the digit
'    "2" never appears in the text).
Imports System.Text.Json.Nodes

Public Module EdgeCaseFixture

    Public Function BuildEdgeCaseScenario() As JsonObject
        Return New JsonObject From {
            {"entities", New JsonObject From {
                {"classes", New JsonArray From {"6a", "6b"}},
                {"teachers", New JsonArray From {"Herr Reiter", "Frau Lenz", "Herr Fuchs"}},
                {"subjects", New JsonArray From {"Sport", "Musik", "Geschichte"}},
                {"rooms", New JsonArray From {"Turnhalle"}},
                {"timeslots", New JsonObject From {
                    {"days", New JsonArray From {"Mo", "Di", "Mi", "Do", "Fr"}},
                    {"periods_per_day", 8}
                }}
            }},
            {"constraints", New JsonArray From {
                New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "Herr Reiter"}, {"class", "6a"}, {"subject", "Sport"}},
                New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "Herr Reiter"}, {"class", "6b"}, {"subject", "Sport"}},
                New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "Frau Lenz"}, {"class", "6a"}, {"subject", "Musik"}},
                New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "Frau Lenz"}, {"class", "6b"}, {"subject", "Musik"}},
                New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "Herr Fuchs"}, {"class", "6a"}, {"subject", "Geschichte"}},
                New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "Herr Fuchs"}, {"class", "6b"}, {"subject", "Geschichte"}},
                New JsonObject From {{"type", "weekly_hours"}, {"class", "6a"}, {"subject", "Sport"}, {"hours_per_week", 4}, {"max_per_day", 2}},
                New JsonObject From {{"type", "weekly_hours"}, {"class", "6b"}, {"subject", "Sport"}, {"hours_per_week", 4}, {"max_per_day", 2}},
                New JsonObject From {{"type", "weekly_hours"}, {"class", "6a"}, {"subject", "Musik"}, {"hours_per_week", 2}, {"max_per_day", 1}},
                New JsonObject From {{"type", "weekly_hours"}, {"class", "6b"}, {"subject", "Musik"}, {"hours_per_week", 2}, {"max_per_day", 1}},
                New JsonObject From {{"type", "weekly_hours"}, {"class", "6a"}, {"subject", "Geschichte"}, {"hours_per_week", 2}, {"max_per_day", 1}},
                New JsonObject From {{"type", "weekly_hours"}, {"class", "6b"}, {"subject", "Geschichte"}, {"hours_per_week", 2}, {"max_per_day", 1}},
                New JsonObject From {{"type", "room_requirement"}, {"subject", "Sport"}, {"allowed_rooms", New JsonArray From {"Turnhalle"}}},
                New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "6a"}},
                New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "6b"}},
                New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", "Herr Reiter"}},
                New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", "Frau Lenz"}},
                New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", "Herr Fuchs"}},
                New JsonObject From {{"type", "no_overlap"}, {"resource", "room"}, {"entity", "Turnhalle"}},
                New JsonObject From {{"type", "teacher_availability"}, {"teacher", "Herr Fuchs"}, {"available_days", New JsonArray From {"Mo", "Mi"}}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6a"}, {"day", "Di"}, {"period", 6}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6a"}, {"day", "Mi"}, {"period", 6}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6a"}, {"day", "Do"}, {"period", 6}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6a"}, {"day", "Fr"}, {"period", 6}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6b"}, {"day", "Di"}, {"period", 6}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6b"}, {"day", "Mi"}, {"period", 6}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6b"}, {"day", "Do"}, {"period", 6}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6b"}, {"day", "Fr"}, {"period", 6}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6a"}, {"day", "Mo"}, {"period", 8}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6a"}, {"day", "Di"}, {"period", 8}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6a"}, {"day", "Mi"}, {"period", 8}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6a"}, {"day", "Fr"}, {"period", 8}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6b"}, {"day", "Mo"}, {"period", 8}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6b"}, {"day", "Di"}, {"period", 8}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6b"}, {"day", "Mi"}, {"period", 8}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6b"}, {"day", "Fr"}, {"period", 8}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6a"}, {"day", "Fr"}, {"period", 3}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6a"}, {"day", "Fr"}, {"period", 4}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6b"}, {"day", "Fr"}, {"period", 3}},
                New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "6b"}, {"day", "Fr"}, {"period", 4}}
            }}
        }
    End Function

    Public Function ExpectedTeacherSubjectAssignments() As HashSet(Of (Cls As String, Subject As String, Teacher As String))
        Return New HashSet(Of (String, String, String)) From {
            ("6a", "Sport", "Herr Reiter"), ("6b", "Sport", "Herr Reiter"),
            ("6a", "Musik", "Frau Lenz"), ("6b", "Musik", "Frau Lenz"),
            ("6a", "Geschichte", "Herr Fuchs"), ("6b", "Geschichte", "Herr Fuchs")
        }
    End Function

    ''' <summary>Muster 4: "nie mehr als eine Doppelstunde pro Tag" must be
    ''' inferred as max_per_day=2 - the digit never appears in the
    ''' prompt.</summary>
    Public Function ExpectedWeeklyHours() As Dictionary(Of (Cls As String, Subject As String), (Hours As Integer, MaxPerDay As Integer))
        Return New Dictionary(Of (String, String), (Integer, Integer)) From {
            {("6a", "Sport"), (4, 2)}, {("6b", "Sport"), (4, 2)},
            {("6a", "Musik"), (2, 1)}, {("6b", "Musik"), (2, 1)},
            {("6a", "Geschichte"), (2, 1)}, {("6b", "Geschichte"), (2, 1)}
        }
    End Function

    Public Function ExpectedNoOverlap() As HashSet(Of (Resource As String, Entity As String))
        Return New HashSet(Of (String, String)) From {
            ("class", "6a"), ("class", "6b"),
            ("teacher", "Herr Reiter"), ("teacher", "Frau Lenz"), ("teacher", "Herr Fuchs"),
            ("room", "Turnhalle")
        }
    End Function

    Public Function ExpectedRoomRequirement() As Dictionary(Of String, List(Of String))
        Return New Dictionary(Of String, List(Of String)) From {
            {"Sport", New List(Of String) From {"Turnhalle"}}
        }
    End Function

    ''' <summary>Muster 2: negated availability ("kann an keinem Tag
    ''' ausser Montag und Mittwoch unterrichten") -> unavailable
    ''' Di/Do/Fr.</summary>
    Public ReadOnly ExpectedUnavailableDays As New Dictionary(Of String, HashSet(Of String)) From {
        {"Herr Fuchs", New HashSet(Of String) From {"Di", "Do", "Fr"}}
    }

    ''' <summary>Musters 1 (two period_exception rules) and 3 (one
    ''' forbidden_slot sentence covering two periods at once) combined.</summary>
    Public Function ExpectedForbiddenSlots() As HashSet(Of (Entity As String, Day As String, Period As Integer))
        Dim result As New HashSet(Of (String, String, Integer))
        For Each cls In {"6a", "6b"}
            For Each d In {"Di", "Mi", "Do", "Fr"} : result.Add((cls, d, 6)) : Next   ' Muster 1: 6. Stunde nur montags
            For Each d In {"Mo", "Di", "Mi", "Fr"} : result.Add((cls, d, 8)) : Next   ' Muster 1: 8. Stunde nur donnerstags
            result.Add((cls, "Fr", 3))   ' Muster 3: 3. und 4. Stunde freitags frei
            result.Add((cls, "Fr", 4))
        Next
        Return result
    End Function

    ''' <summary>Public (not tied to the MSTest class) so both
    ''' LlmExtractionE2ETests.vb and RobustnessRunner can call it. No
    ''' consecutive_required or shared_resource_conflict category here -
    ''' this scenario's ground truth has none, unlike Grundschule/
    ''' Oberstufe.</summary>
    Public Function CompletenessReport(extracted As List(Of JsonObject)) As Dictionary(Of String, Double)
        Dim ent = JsonHelpers.Entities(BuildEdgeCaseScenario())
        Dim allDays = JsonHelpers.AsStringList(JsonHelpers.Timeslots(ent), "days")
        Dim periodsPerDay = JsonHelpers.GetInt(JsonHelpers.Timeslots(ent), "periods_per_day").Value

        Dim expectedUnavailabilityPairs As New HashSet(Of (String, String))
        For Each kvp In ExpectedUnavailableDays
            For Each d In kvp.Value : expectedUnavailabilityPairs.Add((kvp.Key, d)) : Next
        Next

        Dim scores As New Dictionary(Of String, Double) From {
            {"teacher_subject_assignment", ScoreTeacherSubjectAssignment(ExpectedTeacherSubjectAssignments(), extracted)},
            {"weekly_hours", ScoreWeeklyHours(ExpectedWeeklyHours(), extracted)},
            {"no_overlap", ScoreNoOverlap(ExpectedNoOverlap(), extracted)},
            {"room_requirement", ScoreRoomRequirement(ExpectedRoomRequirement(), extracted)},
            {"teacher_availability", ScoreTeacherAvailability(expectedUnavailabilityPairs, extracted, allDays, periodsPerDay)},
            {"forbidden_slot", ScoreForbiddenSlot(ExpectedForbiddenSlots(), extracted)}
        }
        scores("overall") = OverallScore(scores)
        Return scores
    End Function

    Public ReadOnly Prompt As String =
        "An unserer Realschule gibt es zwei Klassen: 6a und 6b. Der Unterricht" & vbLf &
        "findet montags bis freitags mit je 8 Stunden pro Tag statt." & vbLf & vbLf &
        "Faecher:" & vbLf &
        "- Sport: Herr Reiter unterrichtet 6a und 6b, immer in der Turnhalle." & vbLf &
        "  Sport findet nie mehr als eine Doppelstunde pro Tag statt," & vbLf &
        "  insgesamt 4 Stunden pro Woche." & vbLf &
        "- Musik: Frau Lenz unterrichtet 6a und 6b, 2 Stunden pro Woche," & vbLf &
        "  hoechstens 1 Stunde pro Tag." & vbLf &
        "- Geschichte: Herr Fuchs unterrichtet 6a und 6b, 2 Stunden pro Woche," & vbLf &
        "  hoechstens 1 Stunde pro Tag." & vbLf & vbLf &
        "Verfuegbarkeit:" & vbLf &
        "- Herr Fuchs kann an keinem Tag ausser Montag und Mittwoch" & vbLf &
        "  unterrichten." & vbLf & vbLf &
        "Sperrzeiten:" & vbLf &
        "- Die 6. Stunde findet fuer alle Klassen nur montags statt." & vbLf &
        "- Die 8. Stunde findet fuer alle Klassen nur donnerstags statt." & vbLf &
        "- Freitags sind die 3. und 4. Stunde fuer alle Klassen frei." & vbLf & vbLf &
        "Zusaetzlich gilt fuer alle Klassen, Lehrkraefte und Fachraeume die" & vbLf &
        "uebliche Ueberschneidungsfreiheit." & vbLf & vbLf &
        "Erzeuge daraus die passenden Constraints im vereinbarten JSON-Format" & vbLf &
        "fuer den CP-SAT-Solver." & vbLf

End Module
