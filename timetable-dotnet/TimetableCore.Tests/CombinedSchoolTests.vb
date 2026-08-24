' Phase 2.13f: deterministic, LLM-free tests for CombinedSchool.vb - proves
' that a teacher/room name SHARED between a Sek-I-like and a Kursstufe-like
' scenario cannot be double-booked across them, in BOTH solve orders, via
' the one ground-truth check that matters: 0 Verifier violations on the
' merged, concatenated schedule.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class CombinedSchoolTests

    Private Shared Function Kurs(id As String, subject As String, teacher As String,
                                  kursart As String, hoursPerWeek As Integer) As JsonObject
        Return New JsonObject From {
            {"id", id}, {"subject", subject}, {"teacher", teacher},
            {"kursart", kursart}, {"hours_per_week", hoursPerWeek}
        }
    End Function

    Private Shared Function Schiene(id As String, kursart As String, hoursPerWeek As Integer) As JsonObject
        Return New JsonObject From {{"id", id}, {"kursart", kursart}, {"hours_per_week", hoursPerWeek}}
    End Function

    Private Shared Function Kurswahl(wahlprofilId As String, kurse As IEnumerable(Of String)) As JsonObject
        Return New JsonObject From {
            {"type", "kurswahl"}, {"wahlprofil_id", wahlprofilId}, {"student_count", 20},
            {"kurse", New JsonArray(kurse.Select(Function(k) CType(k, JsonNode)).ToArray())}
        }
    End Function

    ''' <summary>1 Tag x 2 Perioden - der gesamte Slot-Raum des Systems.
    ''' Sek I: 1 Klasse, 1 Fach "Deutsch" bei "T-Shared", 1h/Woche (frei
    ''' unter den 2 Slots waehlbar). Kursstufe: 2 GK-Schienen, per EINEM
    ''' Wahlprofil in dieselbe Konfliktgruppe gezwungen (muessen also die
    ''' beiden verfuegbaren Slots UNTEREINANDER aufteilen) - eine davon
    ''' trägt einen Kurs von "T-Shared" (demselben Lehrer wie Sek I), die
    ''' andere von "T-Other". Enge Slot-Lage macht eine Kollision
    ''' plausibel, wenn der Mechanismus fehlt oder falsch verdrahtet
    ''' waere.</summary>
    Private Shared Function SekIScenario() As JsonObject
        Return Scenario(Mini({"5a"}, {"T-Shared"}, {"Deutsch"}, {}, {"Mo"}, 2), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Deutsch"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T-Shared"}, {"class", "5a"}, {"subject", "Deutsch"}}
        })
    End Function

    Private Shared Function KursstufeScenario() As JsonObject
        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray({"T-Shared", "T-Other"})},
            {"subjects", New JsonArray({"Fach X", "Fach Y"})},
            {"rooms", New JsonArray()},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray({"Mo"})}, {"periods_per_day", 2}
            }},
            {"kurse", New JsonArray({
                CType(Kurs("X-GK1", "Fach X", "T-Shared", "GK", 1), JsonNode),
                Kurs("Y-GK1", "Fach Y", "T-Other", "GK", 1)
            })},
            {"schienen", New JsonArray({CType(Schiene("S1", "GK", 1), JsonNode), Schiene("S2", "GK", 1)})}
        }
        Return New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray({CType(Kurswahl("WP1", {"X-GK1", "Y-GK1"}), JsonNode)})}
        }
    End Function

    ''' <summary>The primary regression proof: BOTH solve orders correctly
    ''' avoid double-booking the one shared teacher "T-Shared" - checked
    ''' via the actual ground truth (Verifier.VerifySchedule against the
    ''' merged scenario+concatenated schedule), not by predicting exact
    ''' slot numbers a legitimate solver could pick either way.</summary>
    <TestMethod>
    Public Sub SharedTeacherNeverDoubleBookedInEitherSolveOrder()
        Dim sekI = SekIScenario()
        Dim kursstufe = KursstufeScenario()
        Dim merged = CombinedSchool.BuildMergedVerificationScenario(sekI, kursstufe)

        For Each order In New SolveOrder() {SolveOrder.SekIFirst, SolveOrder.KursstufeFirst}
            Dim result = CombinedSchool.SolveCombinedSchool(sekI, kursstufe, order:=order, timeLimitS:=30, numWorkers:=1)
            Assert.IsNotNull(result.CombinedSchedule, $"Order={order}: kein kombinierter Zeitplan gefunden - SekIStatus={result.SekIStatus}, KursblockungStatus={result.KursstufeResult.KursblockungStatus}, SchienenrasterStatus={result.KursstufeResult.SchienenrasterStatus}, RaumzuordnungStatus={result.KursstufeResult.RaumzuordnungStatus}")

            Dim violations = Verifier.VerifySchedule(merged, result.CombinedSchedule)
            Assert.AreEqual(0, violations.Count, $"Order={order}: " & String.Join(vbLf, violations))

            ' Direkter, expliziter Doppelbelegungs-Check auf T-Shared -
            ' redundant zum Verifier-Check, aber macht die eigentliche
            ' Behauptung (kein Slot kommt fuer T-Shared doppelt vor)
            ' unmittelbar lesbar statt nur ueber die generische
            ' no_overlap-Meldung.
            Dim sharedSlots = result.CombinedSchedule.Where(Function(e) e.Teacher = "T-Shared").
                Select(Function(e) (e.Day, e.Period)).ToList()
            Assert.AreEqual(sharedSlots.Count, sharedSlots.Distinct().Count(),
                $"Order={order}: T-Shared doppelt belegt: {String.Join(", ", sharedSlots)}")
        Next
    End Sub

    ''' <summary>Phase 2.13: dokumentiertes Restrisiko der SekIFirst-
    ''' Richtung - ein geteilter Spezialraum ist genau zum von Stufe B
    ''' gepinnten Slot durch Sek I belegt, UND es ist der einzige erlaubte
    ''' Raum fuer den betroffenen Kurs, sodass Stufe C nicht mehr
    ''' ausweichen kann. Beweist, dass der Orchestrator das sauber als
    ''' Infeasible (RaumzuordnungStatus) meldet statt unklar
    ''' abzustuerzen.</summary>
    <TestMethod>
    Public Sub SekIFirstReportsCleanInfeasibleWhenSharedRoomBlocksTheOnlyPinnedSlot()
        Dim sekI = Scenario(Mini({"5a"}, {"T-Sport"}, {"Sport"}, {"Turnhalle1"}, {"Mo"}, 1), {
            New JsonObject From {{"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Sport"}, {"hours_per_week", 1}},
            New JsonObject From {{"type", "teacher_subject_assignment"}, {"teacher", "T-Sport"}, {"class", "5a"}, {"subject", "Sport"}},
            New JsonObject From {{"type", "room_requirement"}, {"subject", "Sport"}, {"allowed_rooms", New JsonArray From {"Turnhalle1"}}}
        })

        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray({"T-KursSport"})},
            {"subjects", New JsonArray({"Sport"})},
            {"rooms", New JsonArray({"Turnhalle1"})},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray({"Mo"})}, {"periods_per_day", 1}
            }},
            {"kurse", New JsonArray({CType(Kurs("Sport-GK1", "Sport", "T-KursSport", "GK", 1), JsonNode)})},
            {"schienen", New JsonArray({CType(Schiene("S1", "GK", 1), JsonNode)})}
        }
        Dim kursstufe = New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray({CType(Kurswahl("WP1", {"Sport-GK1"}), JsonNode)})}
        }

        ' Nur 1 Tag x 1 Periode auf beiden Seiten -> Sek I belegt
        ' zwangslaeufig Mo/1 in Turnhalle1, und die Kursstufen-Schiene hat
        ' ebenfalls nur Mo/1 zur Verfuegung - "Turnhalle1" ist der EINZIGE
        ' in entities.rooms gelistete Raum auf Kursstufen-Seite, also
        ' automatisch auch der einzige erlaubte (kein specialRoomsBySubject
        ' noetig, um das zu erzwingen), sodass Stufe C nicht ausweichen kann.
        Dim result = CombinedSchool.SolveCombinedSchool(sekI, kursstufe, order:=SolveOrder.SekIFirst, timeLimitS:=15, numWorkers:=1)

        Assert.AreEqual(CpSolverStatus.Optimal, result.SekIStatus)
        Assert.IsNull(result.CombinedSchedule, "Erwartete KEINEN kombinierten Zeitplan - der Raumkonflikt haette Stufe C scheitern lassen muessen.")
        Assert.IsNotNull(result.KursstufeResult.RaumzuordnungStatus, "Erwartete, dass die Pipeline bis Stufe C (Raumzuordnung) kommt.")
        Assert.AreEqual(CpSolverStatus.Infeasible, result.KursstufeResult.RaumzuordnungStatus.Value)
    End Sub

    ''' <summary>Realmasstab-Beleg auf den echten GSG-Fixtures, Sek-I-
    ''' zuerst (Default-Reihenfolge) - ungegated, analog
    ''' GsgCompleteScenarioTests.CompleteGsgScenarioSolvesEndToEnd (die
    ''' bereits ein ~93s+-Solve unkgated in der Standard-Suite akzeptiert).
    ''' Meldet die tatsaechlich erreichte kombinierte, geteilte
    ''' Lehrerzahl (empirisch, nicht nur per Handrechnung wie in Phase
    ''' 2.13a).</summary>
    <TestMethod>
    Public Sub CombinedSchoolSekIFirstFullScaleBenchmark()
        Dim sekI = GymnasiumSekIFixture.BuildGymnasiumSekIScenario()
        Dim kursstufe = KursstufeFixture.BuildKursstufeScenario()
        Dim result = CombinedSchool.SolveCombinedSchool(sekI, kursstufe, order:=SolveOrder.SekIFirst, timeLimitS:=150, numWorkers:=1)

        Assert.IsNotNull(result.CombinedSchedule,
            $"Kein kombinierter Zeitplan - SekIStatus={result.SekIStatus}, KursblockungStatus={result.KursstufeResult.KursblockungStatus}, " &
            $"SchienenrasterStatus={result.KursstufeResult.SchienenrasterStatus}, RaumzuordnungStatus={result.KursstufeResult.RaumzuordnungStatus}")

        Dim merged = CombinedSchool.BuildMergedVerificationScenario(sekI, kursstufe)
        Dim violations = Verifier.VerifySchedule(merged, result.CombinedSchedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))

        Dim sekITeachers = JsonHelpers.AsStringList(JsonHelpers.Entities(sekI), "teachers")
        Dim kursTeachers = JsonHelpers.AsStringList(JsonHelpers.Entities(kursstufe), "teachers")
        Dim combinedTeacherCount = sekITeachers.Union(kursTeachers).Distinct().Count()
        Console.WriteLine($"CombinedSchool (SekIFirst): {combinedTeacherCount} kombinierte Lehrer, 0 Verstoesse.")
    End Sub

    ''' <summary>Realmasstab-Beleg, Kursstufe-zuerst - RUN_SLOW_BENCHMARKS-
    ''' gegated (vermeidet Verdopplung der Standard-Laufzeit, da beide
    ''' Reihenfolgen denselben teuren ~93s+-Sek-I-Solve einmal durchlaufen
    ''' muessen, nur an unterschiedlicher Stelle der Pipeline).</summary>
    <TestMethod>
    Public Sub CombinedSchoolKursstufeFirstFullScaleBenchmark()
        If Environment.GetEnvironmentVariable("RUN_SLOW_BENCHMARKS") <> "1" Then
            Assert.Inconclusive(
                "Manueller CombinedSchool-Benchmark (KursstufeFirst) uebersprungen (kann mehrere Minuten dauern, " &
                "kein fester Bestandteil der Standard-Suite). Set RUN_SLOW_BENCHMARKS=1 to run it.")
        End If

        Dim sekI = GymnasiumSekIFixture.BuildGymnasiumSekIScenario()
        Dim kursstufe = KursstufeFixture.BuildKursstufeScenario()
        Dim result = CombinedSchool.SolveCombinedSchool(sekI, kursstufe, order:=SolveOrder.KursstufeFirst, timeLimitS:=150, numWorkers:=1)

        Assert.IsNotNull(result.CombinedSchedule,
            $"Kein kombinierter Zeitplan - SekIStatus={result.SekIStatus}, KursblockungStatus={result.KursstufeResult.KursblockungStatus}, " &
            $"SchienenrasterStatus={result.KursstufeResult.SchienenrasterStatus}, RaumzuordnungStatus={result.KursstufeResult.RaumzuordnungStatus}")

        Dim merged = CombinedSchool.BuildMergedVerificationScenario(sekI, kursstufe)
        Dim violations = Verifier.VerifySchedule(merged, result.CombinedSchedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))
        Console.WriteLine("CombinedSchool (KursstufeFirst): 0 Verstoesse.")
    End Sub

End Class
