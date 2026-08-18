' Phase 2.11c: proves that stage B (Schienenraster.BuildSchienenrasterScenario
' + the UNCHANGED Solver.Solve()/Verifier.VerifySchedule) correctly turns a
' Kursblockung result into a real day/period schedule, and that
' Schienenraster.DeriveKursSchedule then correctly derives per-Kurs entries
' with no Wahlprofil ever holding two of its own courses at the same
' (Day,Period) - the property the whole three-stage design exists to
' guarantee "for free" from stage A's Kursblockung constraints alone.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class SchienenrasterTests

    Private Shared Function Kurs(id As String, subject As String, teacher As String,
                                  kursart As String, hoursPerWeek As Integer) As JsonObject
        Return New JsonObject From {
            {"id", id}, {"subject", subject}, {"teacher", teacher},
            {"kursart", kursart}, {"hours_per_week", hoursPerWeek}
        }
    End Function

    Private Shared Function Schiene(id As String, kursart As String, hoursPerWeek As Integer) As JsonObject
        Return New JsonObject From {
            {"id", id}, {"kursart", kursart}, {"hours_per_week", hoursPerWeek}
        }
    End Function

    Private Shared Function Kurswahl(wahlprofilId As String, kurse As IEnumerable(Of String)) As JsonObject
        Return New JsonObject From {
            {"type", "kurswahl"}, {"wahlprofil_id", wahlprofilId}, {"student_count", 20},
            {"kurse", New JsonArray(kurse.Select(Function(k) CType(k, JsonNode)).ToArray())}
        }
    End Function

    ''' <summary>2 Wahlprofile sharing 2 of their 3 LK-Kurse (so the LK-
    ''' Schienen carry courses used by both profiles at once - a stronger
    ''' test of stage B's no_overlap(class="Kursstufe") than a single
    ''' profile would be), 1 GK-Kurs per profile on the single GK-Schiene.
    ''' 5 Tage x 8 Perioden gives ample room for 18h/Woche across 4
    ''' Schienen.</summary>
    Private Shared Function ToyScenario() As JsonObject
        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray({"T-Deutsch", "T-Mathe", "T-Bio", "T-Englisch", "T-Erdkunde"})},
            {"subjects", New JsonArray({"Deutsch", "Mathematik", "Biologie", "Englisch", "Erdkunde"})},
            {"rooms", New JsonArray()},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray({"Mo", "Di", "Mi", "Do", "Fr"})}, {"periods_per_day", 8}
            }},
            {"kurse", New JsonArray({
                CType(Kurs("D-LK1", "Deutsch", "T-Deutsch", "LK", 5), JsonNode),
                Kurs("MA-LK1", "Mathematik", "T-Mathe", "LK", 5),
                Kurs("BIO-LK1", "Biologie", "T-Bio", "LK", 5),
                Kurs("EN-GK1", "Englisch", "T-Englisch", "GK", 3),
                Kurs("EK-GK1", "Erdkunde", "T-Erdkunde", "GK", 3)
            })},
            {"schienen", New JsonArray({
                CType(Schiene("S1", "LK", 5), JsonNode), Schiene("S2", "LK", 5), Schiene("S3", "LK", 5),
                Schiene("S4", "GK", 3)
            })}
        }
        Return New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray({
                CType(Kurswahl("WP1", {"D-LK1", "MA-LK1", "BIO-LK1", "EN-GK1"}), JsonNode),
                Kurswahl("WP2", {"D-LK1", "MA-LK1", "BIO-LK1", "EK-GK1"})
            })}
        }
    End Function

    <TestMethod>
    Public Sub SchienenrasterScenarioSolvesCleanlyViaUnchangedSolver()
        Dim data = ToyScenario()
        Dim kb = Kursblockung.SolveKursblockung(data, numWorkers:=1)
        Assert.IsTrue(kb.Status = CpSolverStatus.Optimal OrElse kb.Status = CpSolverStatus.Feasible)

        Dim synthetic = Schienenraster.BuildSchienenrasterScenario(data)
        Dim r = Solver.Solve(synthetic, timeLimitS:=30, numWorkers:=1)

        Assert.IsTrue(r.Status = CpSolverStatus.Optimal OrElse r.Status = CpSolverStatus.Feasible, Solver.StatusName(r.Status))
        Dim violations = Verifier.VerifySchedule(synthetic, r.Schedule)
        Assert.AreEqual(0, violations.Count, String.Join(vbLf, violations))

        ' Jede Schiene bekommt genau ihre hours_per_week viele Slots.
        Dim byS = r.Schedule.GroupBy(Function(e) e.Subject).ToDictionary(Function(g) g.Key, Function(g) g.Count())
        Assert.AreEqual(5, byS("S1"))
        Assert.AreEqual(5, byS("S2"))
        Assert.AreEqual(5, byS("S3"))
        Assert.AreEqual(3, byS("S4"))
    End Sub

    ''' <summary>End-to-end stage A+B: derives the real per-Kurs schedule
    ''' and proves no Wahlprofil ever has two of its own Kurse scheduled at
    ''' the same (Day,Period) - the core guarantee the whole Schienenmodell
    ''' design exists to provide, checked here empirically rather than just
    ''' argued from the constraint shapes.</summary>
    <TestMethod>
    Public Sub DerivedKursScheduleNeverCollidesWithinAWahlprofil()
        Dim data = ToyScenario()
        Dim ent = JsonHelpers.Entities(data)

        Dim kb = Kursblockung.SolveKursblockung(data, numWorkers:=1)
        Assert.IsTrue(kb.Status = CpSolverStatus.Optimal OrElse kb.Status = CpSolverStatus.Feasible)

        Dim synthetic = Schienenraster.BuildSchienenrasterScenario(data)
        Dim r = Solver.Solve(synthetic, timeLimitS:=30, numWorkers:=1)
        Assert.IsTrue(r.Status = CpSolverStatus.Optimal OrElse r.Status = CpSolverStatus.Feasible)

        Dim kursSchedule = Schienenraster.DeriveKursSchedule(ent, kb.Assignment, r.Schedule)
        ' Jeder echte Kurs behaelt seine reale Fach-/Lehrkraft-Identitaet -
        ' die interne Pseudo-Klasse "Kursstufe" darf nicht durchsickern.
        Assert.IsFalse(kursSchedule.Any(Function(e) e.ClassName = "Kursstufe" OrElse e.Teacher = "_schiene"))

        Dim kurseById = JsonHelpers.GetKurse(ent).ToDictionary(
            Function(k) JsonHelpers.GetString(k, "id"),
            Function(k) (Subject:=JsonHelpers.GetString(k, "subject"), Teacher:=JsonHelpers.GetString(k, "teacher")))

        For Each wpConstraint In JsonHelpers.Constraints(data).Where(Function(c) JsonHelpers.GetString(c, "type") = "kurswahl")
            Dim ownKursIds = New HashSet(Of String)(JsonHelpers.AsStringList(wpConstraint, "kurse"))
            Dim ownIdentities = ownKursIds.Select(Function(kid) kurseById(kid)).ToHashSet()

            Dim ownEntries = kursSchedule.Where(Function(e) ownIdentities.Contains((Subject:=e.Subject, Teacher:=e.Teacher))).ToList()
            Dim collisions = ownEntries.GroupBy(Function(e) (e.Day, e.Period)).Where(Function(g) g.Count() > 1).ToList()
            Assert.AreEqual(0, collisions.Count,
                $"Wahlprofil {JsonHelpers.GetString(wpConstraint, "wahlprofil_id")} hat eine Kollision: " &
                String.Join(", ", collisions.Select(Function(g) $"{g.Key.Day}/{g.Key.Period}")))
        Next
    End Sub

End Class
