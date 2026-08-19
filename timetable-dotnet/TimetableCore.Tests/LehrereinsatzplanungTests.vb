' Phase 2.15c: Hand-Smoke-Tests fuer Lehrereinsatzplanung.SolveLehrereinsatz
' - live gegen die installierte Google.OrTools-DLL geloest, Erwartungswerte
' von Hand nachgerechnet (gleiche Disziplin wie bei jeder neuen CP-SAT-
' Modellierung in diesem Projekt, siehe docs/arc42-architecture.md
' Abschnitt 9).
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class LehrereinsatzplanungTests

    Private Function Bestand(Optional zweiteKlasse As Boolean = True) As Stammdatenbestand
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})

        Dim deutsch As New Fach With {.Name = "Deutsch"}
        deutsch.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 4})
        b.Faecher.Add(deutsch)

        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        If zweiteKlasse Then b.Klassen.Add(New Klasse With {.Name = "1b", .Klassenstufe = 1})
        Return b
    End Function

    ''' <summary>Zwei Klassen, zwei gleichermassen qualifizierte und
    ''' klassenlehrerfaehige Lehrkraefte mit je Deputat=4 (exakt eine
    ''' Klasse Deutsch @4h). Von Hand nachgerechnet: die einzige
    ''' Deputat-/Klassenlehrer-optimale Loesung ist eine 1:1-Aufteilung
    ''' (je ein Lehrer pro Klasse) - Total-Objective MUSS 0 sein (0
    ''' Deputat-Abweichung, 0 fehlende Klassenlehrer, 0 Praeferenzen
    ''' gesetzt). Jede andere Aufteilung (z.B. ein Lehrer uebernimmt
    ''' beide Klassen) haette eine Deputat-Abweichung von 4h auf der einen
    ''' und -4h auf der anderen Seite - beide ueber der Toleranz von 2h,
    ''' also &gt; 0 Objective.</summary>
    <TestMethod>
    Public Sub SplitAcrossTwoEquallyQualifiedTeachersAchievesZeroObjective()
        Dim b = Bestand()
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 4, .KlassenlehrerFaehig = True})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 4, .KlassenlehrerFaehig = True})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "Deutsch"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "Deutsch"})

        Assert.AreEqual(0, StammdatenValidation.ValidateStammdaten(b).Count)

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=2.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(0.0, result.Solver.ObjectiveValue, 0.001)

        Assert.AreEqual(2, result.Zuweisungen.Count)
        Dim lehrerVon1a = result.Zuweisungen.Single(Function(z) z.Klasse = "1a").Lehrer
        Dim lehrerVon1b = result.Zuweisungen.Single(Function(z) z.Klasse = "1b").Lehrer
        Assert.AreNotEqual(lehrerVon1a, lehrerVon1b, "Erwartete 1:1-Aufteilung, nicht ein Lehrer fuer beide Klassen.")

        Assert.AreEqual(2, result.Klassenlehrer.Count)
        Assert.AreEqual(lehrerVon1a, result.Klassenlehrer("1a"))
        Assert.AreEqual(lehrerVon1b, result.Klassenlehrer("1b"))
    End Sub

    ''' <summary>Nur EIN Kandidat fuer die einzige Klasse - er MUSS
    ''' zugewiesen werden, auch wenn seine Praeferenz (Klassenstufe 5,
    ''' nicht existent) nie erfuellbar ist. Von Hand nachgerechnet: exakt
    ''' 1 Praeferenz-Verletzung (die einzige Zuweisung ueberhaupt), 0
    ''' Deputat-Abweichung (Deputat=4 passt exakt zu den 4h), 0 fehlende
    ''' Klassenlehrer -&gt; Total = WeightPraeferenzVerletzt * 1.</summary>
    <TestMethod>
    Public Sub PreferenceViolationIsPenalizedButAssignmentStillHappens()
        Dim b = Bestand(zweiteKlasse:=False)
        Dim lehrer As New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 4, .KlassenlehrerFaehig = True}
        lehrer.BevorzugteKlassenstufen.Add(5)
        b.Lehrkraefte.Add(lehrer)
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "Deutsch"})

        Assert.AreEqual(0, StammdatenValidation.ValidateStammdaten(b).Count)

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(CDbl(Lehrereinsatzplanung.WeightPraeferenzVerletzt), result.Solver.ObjectiveValue, 0.001)
        Assert.AreEqual(1, result.Zuweisungen.Count)
        Assert.AreEqual("Lehrer A", result.Zuweisungen(0).Lehrer)
    End Sub

    ''' <summary>Der einzige qualifizierte Kandidat ist NICHT
    ''' klassenlehrerfaehig - die Klasse bleibt zwangslaeufig ohne
    ''' Klassenlehrer (weiches Ziel, kein Infeasible). Von Hand
    ''' nachgerechnet: 0 Deputat-Abweichung, 1 fehlender Klassenlehrer, 0
    ''' Praeferenzen -&gt; Total = WeightKlassenlehrerFehlt * 1.</summary>
    <TestMethod>
    Public Sub MissingKlassenlehrerCandidateIsPenalizedNotInfeasible()
        Dim b = Bestand(zweiteKlasse:=False)
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 4, .KlassenlehrerFaehig = False})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "Deutsch"})

        Assert.AreEqual(0, StammdatenValidation.ValidateStammdaten(b).Count)

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(CDbl(Lehrereinsatzplanung.WeightKlassenlehrerFehlt), result.Solver.ObjectiveValue, 0.001)
        Assert.AreEqual(1, result.Zuweisungen.Count)
        Assert.AreEqual(0, result.Klassenlehrer.Count)
    End Sub

    ''' <summary>Deputat-Abweichung innerhalb der Toleranz (hier: 1h ueber
    ''' Soll bei Toleranz 2h) darf gar nicht in die Zielfunktion
    ''' einfliessen - Total muss trotz der Abweichung 0 bleiben.</summary>
    <TestMethod>
    Public Sub DeviationWithinToleranceIsNotPenalized()
        Dim b = Bestand(zweiteKlasse:=False)
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 3, .KlassenlehrerFaehig = True})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "Deutsch"})

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=2.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(0.0, result.Solver.ObjectiveValue, 0.001)
    End Sub

    ''' <summary>Phase 2.15d: das uebersetzte Ergebnis (teacher_subject_
    ''' assignment/weekly_hours/consecutive_required) muss zusammen mit
    ''' Stammdaten.BuildEntitiesFragment ein Validation.ValidateEntities-
    ''' sauberes entities/constraints-JSON ergeben - der Beweis, dass die
    ''' Uebergabe an den bestehenden Solver funktioniert, ohne dessen Code
    ''' anzufassen.</summary>
    <TestMethod>
    Public Sub BuildAssignmentConstraintsProducesValidEntitiesJson()
        Dim b = Bestand()
        b.Faecher(0).BlockLength = 2
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 4, .KlassenlehrerFaehig = True})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 4, .KlassenlehrerFaehig = True})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "Deutsch"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "Deutsch"})

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible)

        Dim constraints = Lehrereinsatzplanung.BuildAssignmentConstraints(result, b)
        Dim distinctKlassen = result.Zuweisungen.Select(Function(z) z.Klasse).Distinct().Count()
        Dim distinctLehrer = result.Zuweisungen.Select(Function(z) z.Lehrer).Distinct().Count()
        Dim erwarteteAnzahl = result.Zuweisungen.Count * 3 + distinctKlassen + distinctLehrer ' je Zuweisung: assignment+weekly_hours+consecutive_required, plus no_overlap(class)/no_overlap(teacher)
        Assert.AreEqual(erwarteteAnzahl, constraints.Count)

        Dim ent = Stammdaten.BuildEntitiesFragment(b)
        Dim data As New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray(constraints.Select(Function(c) CType(c, JsonNode)).ToArray())}
        }
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count)

        Dim assignmentFor1a = constraints.Single(Function(c) JsonHelpers.GetString(c, "type") = "teacher_subject_assignment" AndAlso JsonHelpers.GetString(c, "class") = "1a")
        Assert.AreEqual("Deutsch", JsonHelpers.GetString(assignmentFor1a, "subject"))

        Dim weeklyFor1a = constraints.Single(Function(c) JsonHelpers.GetString(c, "type") = "weekly_hours" AndAlso JsonHelpers.GetString(c, "class") = "1a")
        Assert.AreEqual(4, JsonHelpers.GetInt(weeklyFor1a, "hours_per_week"))

        Dim consecutiveFor1a = constraints.Single(Function(c) JsonHelpers.GetString(c, "type") = "consecutive_required" AndAlso JsonHelpers.GetString(c, "class") = "1a")
        Assert.AreEqual(2, JsonHelpers.GetInt(consecutiveFor1a, "block_length"))

        ' Phase 2.16-Bugfix: no_overlap fuer jede Klasse UND jede Lehrkraft
        ' muss vorhanden sein - ohne diese Regeln kann Solver.Solve alle
        ' Wochenstunden einer Klasse/Lehrkraft in denselben Slot haeufen
        ' (live in Phase 2.16 am AFS-Fellbach-Benchmark entdeckt).
        Assert.AreEqual(1, constraints.Where(Function(c) JsonHelpers.GetString(c, "type") = "no_overlap" AndAlso JsonHelpers.GetString(c, "resource") = "class" AndAlso JsonHelpers.GetString(c, "entity") = "1a").Count())
        Assert.AreEqual(1, constraints.Where(Function(c) JsonHelpers.GetString(c, "type") = "no_overlap" AndAlso JsonHelpers.GetString(c, "resource") = "class" AndAlso JsonHelpers.GetString(c, "entity") = "1b").Count())
    End Sub

    ''' <summary>Phase 2.16-Bugfix, dediziert: baut ein Szenario, in dem
    ''' zwei Klassen dieselbe Lehrkraft fuer unterschiedliche Faecher
    ''' teilen, und beweist end-to-end (nicht nur an der Constraint-Liste),
    ''' dass Solver.Solve daraus einen tatsaechlich kollisionsfreien
    ''' Stundenplan macht - vor dem Fix haeufte der Solver mangels
    ''' no_overlap alle Wochenstunden einer Klasse in denselben Slot.</summary>
    <TestMethod>
    Public Sub BuildAssignmentConstraintsResultingScheduleHasNoOverlaps()
        Dim b = Bestand()
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 4, .KlassenlehrerFaehig = True})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 4, .KlassenlehrerFaehig = True})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "Deutsch"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "Deutsch"})

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible)

        Dim constraints = Lehrereinsatzplanung.BuildAssignmentConstraints(result, b)
        Dim ent = Stammdaten.BuildEntitiesFragment(b)
        Dim data As New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray(constraints.Select(Function(c) CType(c, JsonNode)).ToArray())}
        }

        Dim solveResult = Solver.Solve(data, timeLimitS:=10)
        Assert.IsTrue(solveResult.Status = CpSolverStatus.Optimal OrElse solveResult.Status = CpSolverStatus.Feasible)
        Assert.AreEqual(0, Verifier.VerifySchedule(data, solveResult.Schedule).Count)

        ' Harte, direkte Kollisions-Pruefung unabhaengig vom Verifier:
        ' keine Klasse und keine Lehrkraft darf zweimal im selben (Tag,
        ' Periode)-Slot auftauchen.
        For Each gruppe In solveResult.Schedule.GroupBy(Function(e) (e.ClassName, e.Day, e.Period))
            Assert.AreEqual(1, gruppe.Count(), $"Klasse {gruppe.Key.ClassName} doppelt belegt am {gruppe.Key.Day}/{gruppe.Key.Period}")
        Next
        For Each gruppe In solveResult.Schedule.GroupBy(Function(e) (e.Teacher, e.Day, e.Period))
            Assert.AreEqual(1, gruppe.Count(), $"Lehrkraft {gruppe.Key.Teacher} doppelt belegt am {gruppe.Key.Day}/{gruppe.Key.Period}")
        Next
    End Sub

End Class
