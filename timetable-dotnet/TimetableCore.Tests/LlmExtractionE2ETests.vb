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

    ''' <summary>Builds the expected sets/dicts for the Gymnasium-Klasse-5
    ''' scenario from GymnasiumKlasse5Fixture.Assignments and hands them to
    ''' the shared CompletenessScoring functions (Phase 2 refactor - this
    ''' used to be inline, scenario-specific logic; now every scenario just
    ''' builds its own expected data and calls the same scorers).</summary>
    Private Shared Function CompletenessReport(extracted As List(Of JsonObject)) As Dictionary(Of String, Double)
        Dim ent = JsonHelpers.Entities(BuildGymnasiumKlasse5Scenario())
        Dim allDays = JsonHelpers.AsStringList(JsonHelpers.Timeslots(ent), "days")
        Dim periodsPerDay = JsonHelpers.GetInt(JsonHelpers.Timeslots(ent), "periods_per_day").Value

        Dim expectedNoOverlap As New HashSet(Of (String, String))
        For Each cls In JsonHelpers.AsStringList(ent, "classes") : expectedNoOverlap.Add(("class", cls)) : Next
        For Each t In JsonHelpers.AsStringList(ent, "teachers") : expectedNoOverlap.Add(("teacher", t)) : Next
        For Each r In JsonHelpers.AsStringList(ent, "rooms") : expectedNoOverlap.Add(("room", r)) : Next

        Dim expectedRooms As New Dictionary(Of String, List(Of String))
        For Each a In Assignments
            If a.AllowedRooms IsNot Nothing AndAlso Not expectedRooms.ContainsKey(a.Subject) Then
                expectedRooms(a.Subject) = a.AllowedRooms.OrderBy(Function(s) s).ToList()
            End If
        Next

        Dim expectedConsecutive As New HashSet(Of (String, String, Integer))
        For Each a In Assignments
            If a.BlockLength.HasValue Then
                For Each cls In a.TaughtClasses
                    expectedConsecutive.Add((cls, a.Subject, a.BlockLength.Value))
                Next
            End If
        Next

        ' Sperrzeiten-Regel aus dem Prompt: 7. Stunde nur dienstags, an
        ' allen anderen Tagen (Mo, Mi, Do, Fr) muss sie fuer jede Klasse
        ' gesperrt sein.
        Dim expectedForbidden As New HashSet(Of (String, String, Integer))
        For Each cls In JsonHelpers.AsStringList(ent, "classes")
            For Each d In {"Mo", "Mi", "Do", "Fr"}
                expectedForbidden.Add((cls, d, periodsPerDay))
            Next
        Next

        Dim expectedUnavailabilityPairs As New HashSet(Of (String, String))
        For Each kvp In ExpectedUnavailableDays
            For Each d In kvp.Value
                expectedUnavailabilityPairs.Add((kvp.Key, d))
            Next
        Next

        Dim scores As New Dictionary(Of String, Double) From {
            {"teacher_subject_assignment", ScoreTeacherSubjectAssignment(ExpectedTeacherSubjectAssignments(), extracted)},
            {"weekly_hours", ScoreWeeklyHours(ExpectedWeeklyHours(), extracted)},
            {"no_overlap", ScoreNoOverlap(expectedNoOverlap, extracted)},
            {"room_requirement", ScoreRoomRequirement(expectedRooms, extracted)},
            {"consecutive_required", ScoreConsecutiveRequired(expectedConsecutive, extracted)},
            {"teacher_availability", ScoreTeacherAvailability(expectedUnavailabilityPairs, extracted, allDays, periodsPerDay)},
            {"forbidden_slot", ScoreForbiddenSlot(expectedForbidden, extracted)}
        }
        scores("overall") = OverallScore(scores)
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

    ''' <summary>Shared body for the 3 new Phase-2 scenario tests below
    ''' (extract -> validate -> score -> solve -> verify). The existing
    ''' Gymnasium test above is deliberately left as its own, separately
    ''' already-twice-live-verified implementation rather than refactored
    ''' onto this helper too - that would need a third expensive live
    ''' re-verification for no behavioral benefit.</summary>
    Private Shared Async Function RunScenarioE2E(entities As JsonObject, prompt As String,
                                                   scoreFn As Func(Of List(Of JsonObject), Dictionary(Of String, Double)),
                                                   Optional timeLimitS As Double = 60) As Task
        If Environment.GetEnvironmentVariable("RUN_LLM_TESTS") <> "1" Then
            Assert.Inconclusive(
                "LLM e2e test skipped by default (needs a running Ollama server with " &
                "qwen3.5:4b, takes several minutes, and is not deterministic). " &
                "Set RUN_LLM_TESTS=1 to run it.")
        End If

        Dim avail = Await LlmExtraction.IsOllamaAvailable()
        If Not avail.Available Then Assert.Inconclusive(avail.Reason)

        Dim result = Await LlmExtraction.ExtractAllConstraints(entities, prompt)

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

        Dim scores = scoreFn(result.Constraints)
        Console.WriteLine(vbLf & "=== Vollstaendigkeit vs. Ground Truth ===")
        For Each kvp In scores
            Console.WriteLine($"  {kvp.Key,-28} {kvp.Value:P0}")
        Next

        Assert.IsTrue(scores("overall") >= 0.5, $"Vollstaendigkeit nur {scores("overall"):P0}")

        Dim solveResult = Solver.Solve(scenarioData, timeLimitS:=timeLimitS)
        Console.WriteLine(vbLf & $"solve() status: {Solver.StatusName(solveResult.Status)}")
        Assert.IsTrue(solveResult.Status = CpSolverStatus.Optimal OrElse solveResult.Status = CpSolverStatus.Feasible, Solver.StatusName(solveResult.Status))

        Dim violations = Verifier.VerifySchedule(scenarioData, solveResult.Schedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))
    End Function

    <TestMethod>
    Public Async Function LlmExtractionE2EGrundschule() As Task
        Await RunScenarioE2E(JsonHelpers.Entities(GrundschuleFixture.BuildGrundschuleScenario()), GrundschuleFixture.Prompt,
                              AddressOf GrundschuleFixture.CompletenessReport, timeLimitS:=20)
    End Function

    <TestMethod>
    Public Async Function LlmExtractionE2EOberstufe() As Task
        Await RunScenarioE2E(JsonHelpers.Entities(OberstufeFixture.BuildOberstufeScenario()), OberstufeFixture.Prompt,
                              AddressOf OberstufeFixture.CompletenessReport, timeLimitS:=60)
    End Function

    <TestMethod>
    Public Async Function LlmExtractionE2EEdgeCase() As Task
        Await RunScenarioE2E(JsonHelpers.Entities(EdgeCaseFixture.BuildEdgeCaseScenario()), EdgeCaseFixture.Prompt,
                              AddressOf EdgeCaseFixture.CompletenessReport, timeLimitS:=20)
    End Function

End Class
