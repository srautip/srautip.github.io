' Phase 2 repeated-run variance study (plan section 2d). Runs each of the
' 4 robustness-matrix scenarios (Gymnasium-Klasse-5 baseline + the 3 new
' Phase 2 scenarios) against live Ollama/Qwen multiple times, WITHOUT a
' fixed LLM seed - the whole point is to measure the real production
' variance at temperature=0.1, not eliminate it. Each run's result is
' appended to the results file immediately (not batched at the end), so a
' sandbox interruption mid-study doesn't lose prior runs.
'
' This does not fit MSTest's pass/fail model (the goal is measuring
' spread, not asserting a threshold) - hence a standalone console app
' rather than another gated test.
'
' Usage: dotnet run [-- <ScenarioName> [<repeats>]]
'   no args            -> all 4 scenarios, 3 repeats each
'   ScenarioName        -> just that scenario ("Gymnasium", "Grundschule",
'                          "Oberstufe", "EdgeCase"), 3 repeats
'   ScenarioName N      -> just that scenario, N repeats
' Lets a long study be run incrementally across several invocations
' instead of one multi-hour blocking call.
Imports System.IO
Imports System.Text.Json.Nodes
Imports TimetableCore

Module Program

    Private Const DefaultRepeats As Integer = 3

    Private Structure ScenarioDef
        Public Name As String
        Public GetEntities As Func(Of JsonObject)
        Public Prompt As String
        Public ScoreFn As Func(Of List(Of JsonObject), Dictionary(Of String, Double))
        Public TimeLimitS As Double
    End Structure

    ''' <summary>Deliberately a standalone duplicate of
    ''' LlmExtractionE2ETests.CompletenessReport (which stays private to
    ''' that already-twice-live-verified test class - see that file's
    ''' RunScenarioE2E doc comment) rather than a shared reference: pulling
    ''' in the Tests project here would also pull in MSTest, which this
    ''' console app doesn't need, and GymnasiumKlasse5Fixture's own
    ''' `Assignment` type is intentionally distinct from
    ''' AssignmentScenarioBuilder's `SubjectAssignment` (see
    ''' AssignmentScenarioBuilder.vb's header comment), so the generic
    ''' Expected* helpers don't apply here anyway.</summary>
    Private Function GymnasiumCompletenessReport(extracted As List(Of JsonObject)) As Dictionary(Of String, Double)
        Dim ent = JsonHelpers.Entities(GymnasiumKlasse5Fixture.BuildGymnasiumKlasse5Scenario())
        Dim allDays = JsonHelpers.AsStringList(JsonHelpers.Timeslots(ent), "days")
        Dim periodsPerDay = JsonHelpers.GetInt(JsonHelpers.Timeslots(ent), "periods_per_day").Value

        Dim expectedNoOverlap As New HashSet(Of (String, String))
        For Each cls In JsonHelpers.AsStringList(ent, "classes") : expectedNoOverlap.Add(("class", cls)) : Next
        For Each t In JsonHelpers.AsStringList(ent, "teachers") : expectedNoOverlap.Add(("teacher", t)) : Next
        For Each r In JsonHelpers.AsStringList(ent, "rooms") : expectedNoOverlap.Add(("room", r)) : Next

        Dim expectedRooms As New Dictionary(Of String, List(Of String))
        For Each a In GymnasiumKlasse5Fixture.Assignments
            If a.AllowedRooms IsNot Nothing AndAlso Not expectedRooms.ContainsKey(a.Subject) Then
                expectedRooms(a.Subject) = a.AllowedRooms.OrderBy(Function(s) s).ToList()
            End If
        Next

        Dim expectedConsecutive As New HashSet(Of (String, String, Integer))
        For Each a In GymnasiumKlasse5Fixture.Assignments
            If a.BlockLength.HasValue Then
                For Each cls In a.TaughtClasses
                    expectedConsecutive.Add((cls, a.Subject, a.BlockLength.Value))
                Next
            End If
        Next

        Dim expectedForbidden As New HashSet(Of (String, String, Integer))
        For Each cls In JsonHelpers.AsStringList(ent, "classes")
            For Each d In {"Mo", "Mi", "Do", "Fr"}
                expectedForbidden.Add((cls, d, periodsPerDay))
            Next
        Next

        Dim expectedUnavailabilityPairs As New HashSet(Of (String, String)) From {
            ("Frau Nguyen", "Do"), ("Frau Nguyen", "Fr"),
            ("Herr Werner", "Fr")
        }

        Dim expectedTsa As New HashSet(Of (String, String, String))
        For Each a In GymnasiumKlasse5Fixture.Assignments
            For Each cls In a.TaughtClasses : expectedTsa.Add((cls, a.Subject, a.Teacher)) : Next
        Next

        Dim expectedWh As New Dictionary(Of (String, String), (Integer, Integer))
        For Each a In GymnasiumKlasse5Fixture.Assignments
            For Each cls In a.TaughtClasses : expectedWh((cls, a.Subject)) = (a.Hours, a.MaxPerDay) : Next
        Next

        Dim scores As New Dictionary(Of String, Double) From {
            {"teacher_subject_assignment", ScoreTeacherSubjectAssignment(expectedTsa, extracted)},
            {"weekly_hours", ScoreWeeklyHours(expectedWh, extracted)},
            {"no_overlap", ScoreNoOverlap(expectedNoOverlap, extracted)},
            {"room_requirement", ScoreRoomRequirement(expectedRooms, extracted)},
            {"consecutive_required", ScoreConsecutiveRequired(expectedConsecutive, extracted)},
            {"teacher_availability", ScoreTeacherAvailability(expectedUnavailabilityPairs, extracted, allDays, periodsPerDay)},
            {"forbidden_slot", ScoreForbiddenSlot(expectedForbidden, extracted)}
        }
        scores("overall") = OverallScore(scores)
        Return scores
    End Function

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

    Private Function Scenarios() As List(Of ScenarioDef)
        Return New List(Of ScenarioDef) From {
            New ScenarioDef With {
                .Name = "Gymnasium", .GetEntities = Function() JsonHelpers.Entities(GymnasiumKlasse5Fixture.BuildGymnasiumKlasse5Scenario()),
                .Prompt = GymnasiumPrompt, .ScoreFn = AddressOf GymnasiumCompletenessReport, .TimeLimitS = 60
            },
            New ScenarioDef With {
                .Name = "Grundschule", .GetEntities = Function() JsonHelpers.Entities(GrundschuleFixture.BuildGrundschuleScenario()),
                .Prompt = GrundschuleFixture.Prompt, .ScoreFn = AddressOf GrundschuleFixture.CompletenessReport, .TimeLimitS = 20
            },
            New ScenarioDef With {
                .Name = "Oberstufe", .GetEntities = Function() JsonHelpers.Entities(OberstufeFixture.BuildOberstufeScenario()),
                .Prompt = OberstufeFixture.Prompt, .ScoreFn = AddressOf OberstufeFixture.CompletenessReport, .TimeLimitS = 60
            },
            New ScenarioDef With {
                .Name = "EdgeCase", .GetEntities = Function() JsonHelpers.Entities(EdgeCaseFixture.BuildEdgeCaseScenario()),
                .Prompt = EdgeCaseFixture.Prompt, .ScoreFn = AddressOf EdgeCaseFixture.CompletenessReport, .TimeLimitS = 20
            }
        }
    End Function

    Private Function ResultsFilePath() As String
        Dim docsDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "docs")
        Directory.CreateDirectory(docsDir)
        Return Path.Combine(docsDir, "phase2-results-raw.log")
    End Function

    Private Sub AppendResult(text As String)
        File.AppendAllText(ResultsFilePath(), text & vbLf)
        Console.Write(text & vbLf)
    End Sub

    Private Async Function RunOnce(scenario As ScenarioDef, runNumber As Integer, totalRuns As Integer) As Task
        Dim sb As New Text.StringBuilder()
        sb.AppendLine($"=== [{scenario.Name}] Run {runNumber}/{totalRuns} === {DateTime.UtcNow:O}")

        Dim avail = Await LlmExtraction.IsOllamaAvailable()
        If Not avail.Available Then
            sb.AppendLine($"  SKIPPED - Ollama nicht verfuegbar: {avail.Reason}")
            AppendResult(sb.ToString())
            Return
        End If

        Dim entities = scenario.GetEntities()
        Dim t0 = DateTime.UtcNow
        Dim result = Await LlmExtraction.ExtractAllConstraints(entities, scenario.Prompt)
        Dim extractDuration = (DateTime.UtcNow - t0).TotalSeconds

        For Each m In result.MetaList
            sb.AppendLine($"  {JsonHelpers.GetString(m, "type"),-28} duration={CDbl(m("duration_s")),6:F1}s valid_json={m("valid_json")} n_items={m("n_items")}")
        Next

        Dim allValidJson = result.MetaList.All(Function(m) CBool(m("valid_json")))
        If Not allValidJson Then
            sb.AppendLine("  FEHLER: mindestens ein Constraint-Typ lieferte kein valides JSON")
            AppendResult(sb.ToString())
            Return
        End If

        Dim scenarioData As New JsonObject From {
            {"entities", entities.DeepClone()},
            {"constraints", New JsonArray(result.Constraints.Select(Function(c) CType(c, JsonNode)).ToArray())}
        }

        Dim errors = Validation.ValidateEntities(scenarioData)
        If errors.Count > 0 Then
            sb.AppendLine("  Validation errors:")
            For Each e In errors : sb.AppendLine("    - " & e) : Next
        End If

        Dim scores = scenario.ScoreFn(result.Constraints)
        sb.AppendLine("  Scores:")
        For Each kvp In scores
            sb.AppendLine($"    {kvp.Key,-28} {kvp.Value:P0}")
        Next

        Dim solveStatus = "n/a (Validation fehlgeschlagen)"
        Dim violationCount = -1
        If errors.Count = 0 Then
            Dim solveResult = Solver.Solve(scenarioData, timeLimitS:=scenario.TimeLimitS)
            solveStatus = Solver.StatusName(solveResult.Status)
            If solveResult.Schedule IsNot Nothing Then
                violationCount = Verifier.VerifySchedule(scenarioData, solveResult.Schedule).Count
            End If
        End If

        Dim totalDuration = (DateTime.UtcNow - t0).TotalSeconds
        sb.AppendLine($"  Solve status: {solveStatus}   Verifier violations: {violationCount}   Extraction: {extractDuration:F1}s   Total: {totalDuration:F1}s")
        AppendResult(sb.ToString())
    End Function

    Private Async Function RunAsync(args As String()) As Task
        Dim all = Scenarios()
        Dim toRun = all
        Dim repeats = DefaultRepeats

        If args.Length >= 1 Then
            toRun = all.Where(Function(s) String.Equals(s.Name, args(0), StringComparison.OrdinalIgnoreCase)).ToList()
            If toRun.Count = 0 Then
                Console.WriteLine($"Unbekanntes Szenario '{args(0)}'. Bekannt: {String.Join(", ", all.Select(Function(s) s.Name))}")
                Return
            End If
        End If
        If args.Length >= 2 Then repeats = Integer.Parse(args(1))

        Console.WriteLine($"Results file: {ResultsFilePath()}")
        Console.WriteLine($"Running {toRun.Count} scenario(s) x {repeats} repeat(s), no fixed LLM seed.")

        For Each scenario In toRun
            For run = 1 To repeats
                Await RunOnce(scenario, run, repeats)
            Next
        Next

        Console.WriteLine("Done.")
    End Function

    Sub Main(args As String())
        RunAsync(args).Wait()
    End Sub

End Module
