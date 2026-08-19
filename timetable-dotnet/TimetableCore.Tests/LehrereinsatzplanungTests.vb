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

    Private Function BestandMitDreiKernfaechern() As Stammdatenbestand
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})

        Dim deutsch As New Fach With {.Name = "Deutsch"}
        deutsch.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 4})
        b.Faecher.Add(deutsch)
        Dim mathematik As New Fach With {.Name = "Mathematik"}
        mathematik.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 4})
        b.Faecher.Add(mathematik)
        Dim sachunterricht As New Fach With {.Name = "Sachunterricht"}
        sachunterricht.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        b.Faecher.Add(sachunterricht)

        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        b.Klassen.Add(New Klasse With {.Name = "1b", .Klassenstufe = 1})
        Return b
    End Function

    ''' <summary>Zwei Klassen mit je drei Kernfaechern (Deutsch+Mathematik+
    ''' Sachunterricht = 10h), zwei fuer alle drei qualifizierte,
    ''' klassenlehrerfaehige Lehrkraefte mit je Deputat=10 (exakt eine
    ''' Klasse). Von Hand nachgerechnet: die Deputat-Kapazitaet reicht
    ''' JEWEILS nur fuer eine Klasse (2 Klassen x 10h &gt; 10h+2h Toleranz
    ''' eines einzelnen Lehrers), Objective muss also 0 sein UND jede
    ''' Klasse muss alle drei Faecher bei EINER einzigen Lehrkraft
    ''' gebuendelt bekommen (Beweis der Phase-2.16-Nachtrag-Erweiterung -
    ''' vorher haette das Modell die drei Faecher beliebig auf beide
    ''' Lehrkraefte verteilen koennen, ohne dass das die Zielfunktion
    ''' beeinflusst haette).</summary>
    <TestMethod>
    Public Sub CoreSubjectsAreBundledOntoASingleTeacherPerClass()
        Dim b = BestandMitDreiKernfaechern()
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 10, .KlassenlehrerFaehig = True})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 10, .KlassenlehrerFaehig = True})
        For Each fach In New List(Of String) From {"Deutsch", "Mathematik", "Sachunterricht"}
            b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = fach})
            b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = fach})
        Next

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=2.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(0.0, result.Solver.ObjectiveValue, 0.001)

        For Each klasse In New List(Of String) From {"1a", "1b"}
            Dim lehrerDieserKlasse = result.Zuweisungen.Where(Function(z) z.Klasse = klasse).Select(Function(z) z.Lehrer).Distinct().ToList()
            Assert.AreEqual(1, lehrerDieserKlasse.Count, $"Klasse {klasse} sollte alle Kernfaecher bei EINER Lehrkraft gebuendelt bekommen, tatsaechlich: {String.Join(", ", lehrerDieserKlasse)}")
        Next
        Dim lehrer1a = result.Zuweisungen.First(Function(z) z.Klasse = "1a").Lehrer
        Dim lehrer1b = result.Zuweisungen.First(Function(z) z.Klasse = "1b").Lehrer
        Assert.AreNotEqual(lehrer1a, lehrer1b, "Bei nur 10h Deputat pro Lehrkraft muessen 1a und 1b unterschiedliche Klassenlehrer bekommen.")
    End Sub

    ''' <summary>Eine Klasse, deren zwei Faecher NUR von je einer
    ''' unterschiedlichen, disjunkt qualifizierten klassenlehrerfaehigen
    ''' Lehrkraft abgedeckt werden koennen - Vollstaendigkeit (hart)
    ''' erzwingt dadurch zwangslaeufig BEIDE Lehrkraefte gleichzeitig aktiv
    ''' in dieser einen Klasse. Beweist die im Modell-Kommentar
    ''' dokumentierte Design-Entscheidung: die Buendelung bleibt ein
    ''' WEICHES Ziel (bestraft mit genau WeightBuendelungVerletzt), damit
    ''' genau dieser Fall Optimal/Feasible bleibt statt faelschlich
    ''' Infeasible zu werden.</summary>
    <TestMethod>
    Public Sub BundlingViolationIsPenalizedNotInfeasibleWhenQualificationsAreDisjoint()
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        Dim deutsch As New Fach With {.Name = "Deutsch"}
        deutsch.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        b.Faecher.Add(deutsch)
        Dim mathematik As New Fach With {.Name = "Mathematik"}
        mathematik.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        b.Faecher.Add(mathematik)
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})

        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 2, .KlassenlehrerFaehig = True})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 2, .KlassenlehrerFaehig = True})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "Deutsch"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "Mathematik"})

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=2.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(CDbl(Lehrereinsatzplanung.WeightBuendelungVerletzt), result.Solver.ObjectiveValue, 0.001)
        Assert.AreEqual(2, result.Zuweisungen.Count)
        Assert.IsTrue(result.Zuweisungen.Any(Function(z) z.Lehrer = "Lehrer A"))
        Assert.IsTrue(result.Zuweisungen.Any(Function(z) z.Lehrer = "Lehrer B"))
    End Sub

    ''' <summary>Phase 2.16-Nachtrag-3 (Live-Rueckmeldung: "ein
    ''' Klassenlehrer hat ueblicherweise nur eine Klasse"): zwei Klassen,
    ''' zwei fuer beide qualifizierte Kandidaten, Deputat-Toleranz bewusst
    ''' riesig gesetzt, damit die Deputat-Abweichung fuer JEDE denkbare
    ''' Aufteilung 0 bleibt - so kann NUR die neue "hoechstens 1 Klasse
    ''' pro Lehrkraft"-Regel selbst den Unterschied machen, nicht das
    ''' Deputat. Von Hand nachgerechnet: OHNE die Regel waere "1 Lehrkraft
    ''' fuer beide Klassen" genauso guenstig wie eine Aufteilung (beides
    ''' Objective=0, da nur EIN Kandidat je Klasse aktiv ist - die
    ''' Klassen-Richtung aus Nachtrag 2 allein wuerde das nicht
    ''' verhindern). MIT der Regel kostet "1 Lehrkraft fuer beide Klassen"
    ''' exakt WeightBuendelungVerletzt, eine Aufteilung auf zwei
    ''' verschiedene Lehrkraefte bleibt bei 0 - der Solver MUSS also
    ''' aufteilen, um das (bewiesen erreichbare) Optimum 0 zu treffen.</summary>
    <TestMethod>
    Public Sub SameTeacherIsNotBundledAsKlassenlehrerOfTwoClassesWhenDeputatDoesNotForceIt()
        Dim b = BestandMitDreiKernfaechern()
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 10, .KlassenlehrerFaehig = True})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 10, .KlassenlehrerFaehig = True})
        For Each fach In New List(Of String) From {"Deutsch", "Mathematik", "Sachunterricht"}
            b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = fach})
            b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = fach})
        Next

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=1000.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(0.0, result.Solver.ObjectiveValue, 0.001, "Bei riesiger Deputat-Toleranz ist eine Aufteilung auf zwei Lehrkraefte immer erreichbar und muss das beweisbare Optimum sein.")

        Dim lehrer1a = result.Zuweisungen.First(Function(z) z.Klasse = "1a").Lehrer
        Dim lehrer1b = result.Zuweisungen.First(Function(z) z.Klasse = "1b").Lehrer
        Assert.AreNotEqual(lehrer1a, lehrer1b, "Dieselbe Lehrkraft darf nicht Klassenlehrer beider Klassen zugleich sein.")
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
