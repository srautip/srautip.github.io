' Ported 1:1 from tests/test_llm_extraction_e2e.py: a natural-language
' prompt describing the Gymnasium-Klasse-5 scenario -> LLM-based extraction
' (LlmExtraction.vb, decomposed per constraint type, running against a
' local Ollama server) -> ValidateEntities -> Solve -> VerifySchedule, plus
' a completeness score against the hand-built ground truth
' (GymnasiumKlasse5Fixture.Assignments).
'
' Unlike the rest of the suite, this test:
' - needs a running Ollama server with qwen3.5:4b pulled (Inconclusive
'   otherwise, MSTest's closest equivalent to pytest's skip)
' - is NOT deterministic - LLM output varies run to run
' - can take several minutes on CPU (9 sequential model calls covering a
'   4-class, 9-subject, 15-teacher scenario)
'
' Skipped by default. Run explicitly with the RUN_LLM_TESTS=1 environment
' variable set, analogous to the Python original's pytest.mark.skipif
' gate.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class LlmExtractionE2ETests

    Private Const GymnasiumPrompt As String =
        "Wir sind ein vierzuegiges Gymnasium (Klassen 5a, 5b, 5c, 5d) in" & vbLf &
        "Baden-Wuerttemberg. Der Stundenplan laeuft Montag bis Freitag mit je 7" & vbLf &
        "Stunden pro Tag." & vbLf & vbLf &
        "Faecher und Zuordnung:" & vbLf &
        "- Deutsch (5 Stunden/Woche, hoechstens 2 pro Tag): Frau Vogel unterrichtet" & vbLf &
        "  5a und 5b, Herr Baumann unterrichtet 5c und 5d." & vbLf &
        "- Mathematik (5 Stunden/Woche, hoechstens 2 pro Tag): Herr Krause" & vbLf &
        "  unterrichtet 5a und 5c, Frau Nguyen unterrichtet 5b und 5d." & vbLf &
        "- Englisch (5 Stunden/Woche, hoechstens 2 pro Tag): Frau Fischer" & vbLf &
        "  unterrichtet 5a und 5d, Herr Roth unterrichtet 5b und 5c." & vbLf &
        "- BNT, Biologie/Naturphaenomene und Technik (4 Stunden/Woche, hoechstens 2" & vbLf &
        "  pro Tag, muss als zwei Doppelstunden stattfinden, immer im NaWi-Raum):" & vbLf &
        "  Frau Kraemer unterrichtet 5a und 5b, Herr Werner unterrichtet 5c und 5d." & vbLf &
        "- Sport (3 Stunden/Woche, hoechstens 2 pro Tag, in Sporthalle1 oder" & vbLf &
        "  Sporthalle2): Herr Braun unterrichtet 5a und 5b, Frau Lang unterrichtet" & vbLf &
        "  5c und 5d." & vbLf &
        "- Musik (2 Stunden/Woche, hoechstens 2 pro Tag, im Musiksaal): Frau Adler" & vbLf &
        "  unterrichtet alle vier Klassen 5a, 5b, 5c und 5d." & vbLf &
        "- Kunst (2 Stunden/Woche, hoechstens 2 pro Tag, im Kunstraum): Herr" & vbLf &
        "  Schuster unterrichtet 5a und 5c, Frau Weiss unterrichtet 5b und 5d." & vbLf &
        "- Religion (2 Stunden/Woche, hoechstens 2 pro Tag): Pfarrer Huber" & vbLf &
        "  unterrichtet alle vier Klassen." & vbLf &
        "- Erdkunde (2 Stunden/Woche, hoechstens 2 pro Tag): Herr Fink unterrichtet" & vbLf &
        "  alle vier Klassen." & vbLf & vbLf &
        "Verfuegbarkeit:" & vbLf &
        "- Frau Nguyen arbeitet Teilzeit und ist nur montags, dienstags und" & vbLf &
        "  mittwochs verfuegbar." & vbLf &
        "- Herr Werner hat freitags einen festen Fortbildungstag und ist dann" & vbLf &
        "  nicht verfuegbar." & vbLf & vbLf &
        "Sperrzeiten:" & vbLf &
        "- Eine 7. Stunde (Nachmittagsunterricht) soll fuer alle vier Klassen" & vbLf &
        "  hoechstens an einem Tag pro Woche stattfinden, idealerweise dienstags." & vbLf &
        "  An allen anderen Tagen (Montag, Mittwoch, Donnerstag, Freitag) endet der" & vbLf &
        "  Unterricht spaetestens nach der 6. Stunde - keine Klasse hat dann eine" & vbLf &
        "  7. Stunde." & vbLf & vbLf &
        "Zusaetzlich gilt fuer alle Klassen, Lehrkraefte und Fachraeume die" & vbLf &
        "uebliche Ueberschneidungsfreiheit: niemand kann zwei Dinge gleichzeitig" & vbLf &
        "haben, und kein Fachraum kann von zwei Gruppen gleichzeitig genutzt" & vbLf &
        "werden." & vbLf & vbLf &
        "Erzeuge daraus die passenden Constraints im vereinbarten JSON-Format fuer" & vbLf &
        "den CP-SAT-Solver." & vbLf

    ' Ground truth for teacher_availability: which days is each teacher
    ' fully unavailable (not just "one period blocked"). Sourced from the
    ' prompt text above, not from GymnasiumKlasse5Fixture (that fixture's
    ' own teacher_availability entries are for the separate, hand-built
    ' scenario).
    Private Shared ReadOnly ExpectedUnavailableDays As New Dictionary(Of String, HashSet(Of String)) From {
        {"Frau Nguyen", New HashSet(Of String) From {"Do", "Fr"}},
        {"Herr Werner", New HashSet(Of String) From {"Fr"}}
    }

    Private Shared Function ExpectedTeacherSubjectAssignments() As HashSet(Of (Cls As String, Subject As String, Teacher As String))
        Dim result As New HashSet(Of (String, String, String))
        For Each a In Assignments
            For Each cls In a.TaughtClasses
                result.Add((cls, a.Subject, a.Teacher))
            Next
        Next
        Return result
    End Function

    Private Shared Function ExpectedWeeklyHours() As Dictionary(Of (Cls As String, Subject As String), (Hours As Integer, MaxPerDay As Integer))
        Dim result As New Dictionary(Of (String, String), (Integer, Integer))
        For Each a In Assignments
            For Each cls In a.TaughtClasses
                result((cls, a.Subject)) = (a.Hours, a.MaxPerDay)
            Next
        Next
        Return result
    End Function

    ''' <summary>Days on which `entry` (a teacher_availability constraint)
    ''' blocks EVERY period - via a day missing from available_days, or
    ''' via unavailable_periods listing all periods of that day. A day
    ''' with only some periods blocked does NOT count.</summary>
    Private Shared Function FullyUnavailableDays(entry As JsonObject, allDays As List(Of String), periodsPerDay As Integer) As HashSet(Of String)
        Dim availDaysList = JsonHelpers.AsStringList(entry, "available_days")
        Dim availableDays As New HashSet(Of String)(If(availDaysList.Any(), availDaysList, allDays))
        Dim viaAvailableDays As New HashSet(Of String)(allDays.Where(Function(d) Not availableDays.Contains(d)))

        Dim blockedPeriodsByDay As New Dictionary(Of String, HashSet(Of Integer))
        If entry.ContainsKey("unavailable_periods") AndAlso entry("unavailable_periods") IsNot Nothing Then
            For Each node In entry("unavailable_periods").AsArray()
                Dim p = node.AsObject()
                Dim d = JsonHelpers.GetString(p, "day")
                If Not blockedPeriodsByDay.ContainsKey(d) Then blockedPeriodsByDay(d) = New HashSet(Of Integer)
                blockedPeriodsByDay(d).Add(JsonHelpers.GetInt(p, "period").Value)
            Next
        End If
        Dim viaUnavailablePeriods = blockedPeriodsByDay.Where(Function(kvp) kvp.Value.Count >= periodsPerDay).Select(Function(kvp) kvp.Key)

        viaAvailableDays.UnionWith(viaUnavailablePeriods)
        Return viaAvailableDays
    End Function

    Private Shared Function CompletenessReport(extracted As List(Of JsonObject)) As Dictionary(Of String, Double)
        Dim ent = JsonHelpers.Entities(BuildGymnasiumKlasse5Scenario())

        Dim expectedTsa = ExpectedTeacherSubjectAssignments()
        Dim actualTsa As New HashSet(Of (String, String, String))(
            extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "teacher_subject_assignment").
                Select(Function(c) (JsonHelpers.GetString(c, "class"), JsonHelpers.GetString(c, "subject"), JsonHelpers.GetString(c, "teacher"))))
        Dim tsaRecall = expectedTsa.Where(Function(x) actualTsa.Contains(x)).Count() / CDbl(expectedTsa.Count)

        Dim expectedWh = ExpectedWeeklyHours()
        Dim actualWh As New Dictionary(Of (String, String), (Integer?, Integer?))
        For Each whItem In extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "weekly_hours")
            actualWh((JsonHelpers.GetString(whItem, "class"), JsonHelpers.GetString(whItem, "subject"))) =
                (JsonHelpers.GetInt(whItem, "hours_per_week"), JsonHelpers.GetInt(whItem, "max_per_day"))
        Next
        Dim whRecall = expectedWh.Where(Function(kvp)
                                             If Not actualWh.ContainsKey(kvp.Key) Then Return False
                                             Dim actualPair = actualWh(kvp.Key)
                                             Return actualPair.Item1 = kvp.Value.Hours AndAlso actualPair.Item2 = kvp.Value.MaxPerDay
                                         End Function).Count() / CDbl(expectedWh.Count)

        Dim expectedNoOverlap As New HashSet(Of (String, String))
        For Each cls In JsonHelpers.AsStringList(ent, "classes") : expectedNoOverlap.Add(("class", cls)) : Next
        For Each t In JsonHelpers.AsStringList(ent, "teachers") : expectedNoOverlap.Add(("teacher", t)) : Next
        For Each r In JsonHelpers.AsStringList(ent, "rooms") : expectedNoOverlap.Add(("room", r)) : Next
        Dim actualNoOverlap As New HashSet(Of (String, String))(
            extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "no_overlap").
                Select(Function(c) (JsonHelpers.GetString(c, "resource"), JsonHelpers.GetString(c, "entity"))))
        Dim noOverlapRecall = expectedNoOverlap.Where(Function(x) actualNoOverlap.Contains(x)).Count() / CDbl(expectedNoOverlap.Count)

        Dim expectedRooms As New Dictionary(Of String, List(Of String))
        For Each a In Assignments
            If a.AllowedRooms IsNot Nothing AndAlso Not expectedRooms.ContainsKey(a.Subject) Then
                expectedRooms(a.Subject) = a.AllowedRooms.OrderBy(Function(s) s).ToList()
            End If
        Next
        Dim actualRooms As New Dictionary(Of String, List(Of String))
        For Each roomItem In extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "room_requirement")
            actualRooms(JsonHelpers.GetString(roomItem, "subject")) = JsonHelpers.AsStringList(roomItem, "allowed_rooms").OrderBy(Function(s) s).ToList()
        Next
        Dim roomRecall = expectedRooms.Where(Function(kvp) actualRooms.ContainsKey(kvp.Key) AndAlso actualRooms(kvp.Key).SequenceEqual(kvp.Value)).Count() / CDbl(expectedRooms.Count)

        Dim expectedConsecutive As New HashSet(Of (String, String, Integer))
        For Each a In Assignments
            If a.BlockLength.HasValue Then
                For Each cls In a.TaughtClasses
                    expectedConsecutive.Add((cls, a.Subject, a.BlockLength.Value))
                Next
            End If
        Next
        Dim actualConsecutive As New HashSet(Of (String, String, Integer))(
            extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "consecutive_required").
                Select(Function(c) (JsonHelpers.GetString(c, "class"), JsonHelpers.GetString(c, "subject"), JsonHelpers.GetInt(c, "block_length").Value)))
        Dim consecutiveRecall = expectedConsecutive.Where(Function(x) actualConsecutive.Contains(x)).Count() / CDbl(expectedConsecutive.Count)

        ' Sharpened check: not just "is there an entry for this teacher",
        ' but "does the entry actually block every period on the expected
        ' days". This is the check that would have caught the real bug
        ' found earlier: an entry for Herr Werner that only blocked
        ' Fr/period 7 instead of all of Friday still counted as "covered"
        ' under a presence-only check.
        Dim allDays = JsonHelpers.AsStringList(JsonHelpers.Timeslots(ent), "days")
        Dim periodsPerDay = JsonHelpers.GetInt(JsonHelpers.Timeslots(ent), "periods_per_day").Value
        Dim expectedUnavailabilityPairs As New HashSet(Of (String, String))
        For Each kvp In ExpectedUnavailableDays
            For Each d In kvp.Value
                expectedUnavailabilityPairs.Add((kvp.Key, d))
            Next
        Next
        Dim actualUnavailabilityPairs As New HashSet(Of (String, String))
        For Each availItem In extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "teacher_availability")
            For Each d In FullyUnavailableDays(availItem, allDays, periodsPerDay)
                actualUnavailabilityPairs.Add((JsonHelpers.GetString(availItem, "teacher"), d))
            Next
        Next
        Dim availabilityRecall = expectedUnavailabilityPairs.Where(Function(x) actualUnavailabilityPairs.Contains(x)).Count() / CDbl(expectedUnavailabilityPairs.Count)

        ' Sperrzeiten-Regel aus dem Prompt: 7. Stunde nur dienstags, an
        ' allen anderen Tagen (Mo, Mi, Do, Fr) muss sie fuer jede Klasse
        ' gesperrt sein.
        Dim expectedForbidden As New HashSet(Of (String, String, Integer))
        For Each cls In JsonHelpers.AsStringList(ent, "classes")
            For Each d In {"Mo", "Mi", "Do", "Fr"}
                expectedForbidden.Add((cls, d, periodsPerDay))
            Next
        Next
        Dim actualForbidden As New HashSet(Of (String, String, Integer))(
            extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "forbidden_slot" AndAlso JsonHelpers.GetString(c, "scope") = "class").
                Select(Function(c) (JsonHelpers.GetString(c, "entity"), JsonHelpers.GetString(c, "day"), JsonHelpers.GetInt(c, "period").Value)))
        Dim forbiddenRecall = expectedForbidden.Where(Function(x) actualForbidden.Contains(x)).Count() / CDbl(expectedForbidden.Count)

        Dim scores As New Dictionary(Of String, Double) From {
            {"teacher_subject_assignment", tsaRecall},
            {"weekly_hours", whRecall},
            {"no_overlap", noOverlapRecall},
            {"room_requirement", roomRecall},
            {"consecutive_required", consecutiveRecall},
            {"teacher_availability", availabilityRecall},
            {"forbidden_slot", forbiddenRecall}
        }
        scores("overall") = scores.Values.Sum() / scores.Count
        Return scores
    End Function

    <TestMethod>
    Public Async Function LlmExtractionE2EGymnasiumKlasse5() As Task
        If Environment.GetEnvironmentVariable("RUN_LLM_TESTS") <> "1" Then
            Assert.Inconclusive(
                "LLM e2e test skipped by default (needs a running Ollama server with " &
                "qwen3.5:4b, takes several minutes, and is not deterministic). " &
                "Set RUN_LLM_TESTS=1 to run it.")
        End If

        Dim avail = Await LlmExtraction.IsOllamaAvailable()
        If Not avail.Available Then Assert.Inconclusive(avail.Reason)

        Dim entities = JsonHelpers.Entities(BuildGymnasiumKlasse5Scenario())
        Dim result = Await LlmExtraction.ExtractAllConstraints(entities, GymnasiumPrompt)

        Console.WriteLine(vbLf & "=== Extraction meta ===")
        For Each m In result.MetaList
            Console.WriteLine($"  {JsonHelpers.GetString(m, "type"),-28} duration={CDbl(m("duration_s")),6:F1}s valid_json={m("valid_json")} n_items={m("n_items")}")
        Next

        For Each m In result.MetaList
            Assert.IsTrue(CBool(m("valid_json")), $"{JsonHelpers.GetString(m, "type")}: kein valides JSON - {m("parse_error")}")
        Next

        Dim scenarioData As New JsonObject From {
            {"entities", entities.DeepClone()},
            {"constraints", New JsonArray(result.Constraints.Select(Function(c) CType(c, JsonNode)).ToArray())}
        }

        Dim errors = Validation.ValidateEntities(scenarioData)
        Assert.AreEqual(0, errors.Count, "Ungueltige Entity-Referenzen im LLM-Output:" & vbLf & String.Join(vbLf, errors))

        Dim scores = CompletenessReport(result.Constraints)
        Console.WriteLine(vbLf & "=== Vollstaendigkeit vs. Ground Truth (Assignments) ===")
        For Each kvp In scores
            Console.WriteLine($"  {kvp.Key,-28} {kvp.Value:P0}")
        Next

        Assert.IsTrue(scores("overall") >= 0.5, $"Vollstaendigkeit nur {scores("overall"):P0}")

        Dim solveResult = Solver.Solve(scenarioData, timeLimitS:=60)
        Console.WriteLine(vbLf & $"solve() status: {Solver.StatusName(solveResult.Status)}")
        Assert.IsTrue(solveResult.Status = CpSolverStatus.Optimal OrElse solveResult.Status = CpSolverStatus.Feasible, Solver.StatusName(solveResult.Status))

        Dim violations = Verifier.VerifySchedule(scenarioData, solveResult.Schedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))
    End Function

End Class
