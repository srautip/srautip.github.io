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

    ' ===== Phase 2.17: die 7 zurueckgestellten Erweiterungen aus 2.15g =====

    ''' <summary>Kontinuitaet ueber Jahre, gilt fuer ALLE Faecher einer
    ''' Klasse (Nutzerentscheidung). Zwei Klassen (2a/2b), je zwei Faecher
    ''' (Deutsch+Mathematik, 4h je Fach), zwei gleichermassen qualifizierte
    ''' Kandidaten mit riesiger Deputat-Toleranz (isoliert den Effekt,
    ''' gleiche Technik wie SameTeacherIsNotBundledAsKlassenlehrerOfTwoClasses...).
    ''' Von Hand nachgerechnet: Vorjahres-Zuordnung zeigt fuer 2a auf
    ''' Lehrer A (beide Faecher). Waehlt der Solver A fuer BEIDE Faecher von
    ''' 2a, zaehlen beide assign-Variablen als "kontinuitaetErhalten" ->
    ''' Bonus -2*WeightKontinuitaetVerletzt = -40, sonst 0 fuer 2a. Eine
    ''' Aufspaltung von 2a auf A+B waere zusaetzlich noch eine
    ''' Buendelungsverletzung (+20) und braechte nur einen der beiden Boni
    ''' (-20) - per Konstruktion also strikt schlechter als -40. Das
    ''' beweisbare Gesamtoptimum ist deshalb exakt -40 (2b bleibt
    ''' undifferenziert, egal welcher Kandidat sie uebernimmt).</summary>
    <TestMethod>
    Public Sub ContinuityAcrossYearsRewardsReassigningAllSubjectsToSameTeacher()
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 2, .Bezeichnung = "Klasse 2"})
        Dim deutsch As New Fach With {.Name = "Deutsch"}
        deutsch.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 2, .WochenstundenSoll = 4})
        b.Faecher.Add(deutsch)
        Dim mathematik As New Fach With {.Name = "Mathematik"}
        mathematik.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 2, .WochenstundenSoll = 4})
        b.Faecher.Add(mathematik)
        b.Klassen.Add(New Klasse With {.Name = "2a", .Klassenstufe = 2})
        b.Klassen.Add(New Klasse With {.Name = "2b", .Klassenstufe = 2})

        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 20, .KlassenlehrerFaehig = True})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 20, .KlassenlehrerFaehig = True})
        For Each fach In New List(Of String) From {"Deutsch", "Mathematik"}
            b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = fach})
            b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = fach})
        Next

        Dim vorjahr As New Dictionary(Of (Klasse As String, Fach As String), String) From {
            {("2a", "Deutsch"), "Lehrer A"},
            {("2a", "Mathematik"), "Lehrer A"}
        }
        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=1000.0, vorjahresZuordnung:=vorjahr, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(-2.0 * Lehrereinsatzplanung.WeightKontinuitaetVerletzt, result.Solver.ObjectiveValue, 0.001)

        Dim lehrerVon2a = result.Zuweisungen.Where(Function(z) z.Klasse = "2a").Select(Function(z) z.Lehrer).Distinct().ToList()
        Assert.AreEqual(1, lehrerVon2a.Count, "2a sollte bei EINER Lehrkraft gebuendelt sein.")
        Assert.AreEqual("Lehrer A", lehrerVon2a(0), "Kontinuitaet haette 2a wieder Lehrer A zuweisen muessen.")
    End Sub

    ''' <summary>Kontinuitaet: ein Eintrag ohne existierende assign-Variable
    ''' (Lehrer diese Runde nicht qualifiziert) wird stillschweigend
    ''' uebersprungen, kein Fehler/keine Ausnahme - ehrlich keine
    ''' Kontinuitaet moeglich, exakt wie in Phase 2.14 fuer neue Faecher
    ''' etabliert.</summary>
    <TestMethod>
    Public Sub ContinuityEntryForUnqualifiedTeacherIsSkippedWithoutError()
        Dim b = Bestand(zweiteKlasse:=False)
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 4, .KlassenlehrerFaehig = True})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "Deutsch"})

        Dim vorjahr As New Dictionary(Of (Klasse As String, Fach As String), String) From {
            {("1a", "Deutsch"), "Ehemaliger Lehrer (existiert nicht mehr)"}
        }
        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, vorjahresZuordnung:=vorjahr, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(1, result.Zuweisungen.Count)
        Assert.AreEqual("Lehrer A", result.Zuweisungen(0).Lehrer)
    End Sub

    ''' <summary>Fachfremder Einsatz: eine Klasse mit zwei Faechern, je nur
    ''' EIN Kandidat qualifiziert - fuer "F1" nur Lehrer B (fachfremd
    ''' markiert), fuer "F2" nur Lehrer A (regulaer). Beide nicht
    ''' klassenlehrerfaehig - das macht "1a" zu einer Klasse ganz OHNE
    ''' klassenlehrerfaehigen Kandidaten, was (unabhaengig von dieser
    ''' Erweiterung, bereits seit Phase 2.15 bestehendes Verhalten)
    ''' unconditional WeightKlassenlehrerFehlt beitraegt - im erwarteten
    ''' Wert mit eingerechnet. Von Hand nachgerechnet: Vollstaendigkeit
    ''' erzwingt beide Zuweisungen (je einziger Kandidat), F1/B ist die
    ''' einzige fachfremde aktive Zuweisung -&gt; Objective =
    ''' WeightFachfremdEinsatz + WeightKlassenlehrerFehlt.</summary>
    <TestMethod>
    Public Sub FachfremdAssignmentIsPenalizedWhenForcedBySoleCandidate()
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        Dim f1 As New Fach With {.Name = "F1"}
        f1.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        b.Faecher.Add(f1)
        Dim f2 As New Fach With {.Name = "F2"}
        f2.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        b.Faecher.Add(f2)
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})

        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 20, .KlassenlehrerFaehig = False})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 20, .KlassenlehrerFaehig = False})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "F2"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "F1", .Fachfremd = True})

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=1000.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(CDbl(Lehrereinsatzplanung.WeightFachfremdEinsatz + Lehrereinsatzplanung.WeightKlassenlehrerFehlt), result.Solver.ObjectiveValue, 0.001)
        Assert.AreEqual(2, result.Zuweisungen.Count)
        Assert.IsTrue(result.Zuweisungen.Any(Function(z) z.Lehrer = "Lehrer A" AndAlso z.Fach = "F2"))
        Assert.IsTrue(result.Zuweisungen.Any(Function(z) z.Lehrer = "Lehrer B" AndAlso z.Fach = "F1"))
    End Sub

    ''' <summary>Max. Klassen/Faecher pro Lehrer: zwei Klassenstufen, damit
    ''' Fach "F" ausschliesslich Klassenstufe 1 (Klassen 1a/1b) und Fach
    ''' "G" ausschliesslich Klassenstufe 2 (Klasse 2a) betrifft (bewusst
    ''' getrennte Klassenstufen - sonst wuerde "G" automatisch auch in 1b
    ''' gefuehrt, da FaecherOfKlassenstufe alle Faecher EINER
    ''' Klassenstufe liefert). Lehrer A ist ueberall der einzige Kandidat
    ''' UND klassenlehrerfaehig, dadurch aber (unabhaengig von dieser
    ''' Erweiterung, bereits seit Nachtrag 3 bestehendes Verhalten) selbst
    ''' Klassenlehrer aller 3 Klassen zugleich - das loest zusaetzlich zu
    ''' den beiden neuen Hinge-Loss-Termen die bereits bestehende
    ''' Pro-Lehrkraft-Buendelungsregel aus ("hoechstens 1 Klasse als
    ''' Klassenlehrer"), im erwarteten Wert mit eingerechnet.
    ''' Vollstaendigkeit erzwingt alle 3 Zuweisungen (1a/F, 1b/F, 2a/G) -
    ''' 3 distinkte Klassen, 2 distinkte Faecher. Von Hand nachgerechnet:
    ''' MaxKlassen:=1 -&gt; Ueberschreitung 3-1=2 -&gt; 2*WeightMaxKlassenVerletzt;
    ''' MaxFaecher:=1 -&gt; Ueberschreitung 2-1=1 -&gt; 1*WeightMaxFaecherVerletzt;
    ''' plus 1*WeightBuendelungVerletzt fuer die erzwungene
    ''' Mehrfach-Klassenlehrerrolle.</summary>
    <TestMethod>
    Public Sub MaxKlassenAndMaxFaecherOverschreitungArePenalizedAsHingeLoss()
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 2, .Bezeichnung = "Klasse 2"})
        Dim f As New Fach With {.Name = "F"}
        f.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        b.Faecher.Add(f)
        Dim g As New Fach With {.Name = "G"}
        g.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 2, .WochenstundenSoll = 2})
        b.Faecher.Add(g)
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        b.Klassen.Add(New Klasse With {.Name = "1b", .Klassenstufe = 1})
        b.Klassen.Add(New Klasse With {.Name = "2a", .Klassenstufe = 2})

        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 20, .KlassenlehrerFaehig = True, .MaxKlassen = 1, .MaxFaecher = 1})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "F"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "G"})

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=1000.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(3, result.Zuweisungen.Count)
        Assert.AreEqual(
            CDbl(2 * Lehrereinsatzplanung.WeightMaxKlassenVerletzt + Lehrereinsatzplanung.WeightMaxFaecherVerletzt + Lehrereinsatzplanung.WeightBuendelungVerletzt),
            result.Solver.ObjectiveValue, 0.001)
    End Sub

    ''' <summary>Teilzeit-Tage-Kohaerenz: Lehrer A hat nur 2 Praesenztage
    ''' (Mo/Di), Fach "F" verlangt 5h/Woche bei MaxProTag=2 -&gt; maximal
    ''' 2*2=4h moeglich, 5h &gt; 4h also inkohaerent - A wird beim Bau der
    ''' Kandidaten HART ausgeschlossen. Lehrer B (Vollzeit) bleibt der
    ''' einzige Kandidat und MUSS gewaehlt werden.</summary>
    <TestMethod>
    Public Sub TeilzeitInkohaerenterKandidatWirdHartAusgeschlossen()
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        Dim f As New Fach With {.Name = "F"}
        f.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 5, .MaxProTag = 2})
        b.Faecher.Add(f)
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})

        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 10, .KlassenlehrerFaehig = False, .VerfuegbareTage = New List(Of String) From {"Mo", "Di"}})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 20, .KlassenlehrerFaehig = False})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "F"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "F"})

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=1000.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(1, result.Zuweisungen.Count)
        Assert.AreEqual("Lehrer B", result.Zuweisungen(0).Lehrer)
    End Sub

    ''' <summary>Gegenprobe zu obigem Test auf StammdatenValidation-Ebene:
    ''' ist der teilzeit-inkohaerente Kandidat der EINZIGE Kandidat, wuerde
    ''' Lehrereinsatzplanung diesen beim harten Vorfilter komplett
    ''' ausschliessen und "genau 1 Lehrkraft"-Vollstaendigkeit ueber eine
    ''' leere Variablenliste bilden (0=1, Infeasible) - StammdatenValidation
    ''' muss das VORHER als klaren Fehler melden.</summary>
    <TestMethod>
    Public Sub StammdatenValidationDetectsSoleTeilzeitInkohaerenterKandidat()
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        Dim f As New Fach With {.Name = "F"}
        f.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 5, .MaxProTag = 2})
        b.Faecher.Add(f)
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 10, .VerfuegbareTage = New List(Of String) From {"Mo", "Di"}})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "F"})

        Dim errors = StammdatenValidation.ValidateStammdaten(b)
        Assert.AreEqual(1, errors.Count)
        StringAssert.Contains(errors(0), "teilzeit-tage-kohaerenter Kandidat")
    End Sub

    ''' <summary>Klassenlehrer-Tandem-Balance: Klasse "1a" mit
    ''' ErlaubtKlassenlehrerTandem, vier Faecher F1(4h, nur A qualifiziert),
    ''' F2(4h, nur B qualifiziert), F3/F4 (je 2h, beide qualifiziert).
    ''' Vollstaendigkeit erzwingt A UND B gleichzeitig aktiv (je ihr
    ''' Alleinstellungs-Fach) - ein echter, erzwungener Tandem-Fall, kein
    ''' Zufallsprodukt der Deputat-Zielfunktion (riesige Toleranz schaltet
    ''' die aus). Von Hand nachgerechnet: F3/F4 je zur Haelfte auf A/B
    ''' verteilt ergibt A=B=6h -&gt; Bereich 0 -&gt; Objective 0 (Buendelung
    ''' erlaubt bei Tandem bis zu 2 aktive Kandidaten, hier keine
    ''' Verletzung). Jede andere Verteilung von F3/F4 waere unausgewogener
    ''' und teurer (z.B. beide an A: 8 vs. 4 -&gt; Bereich 4 -&gt;
    ''' Objective 20) - das beweisbare Optimum 0 zwingt den Solver zur
    ''' ausgewogenen Aufteilung.</summary>
    <TestMethod>
    Public Sub TandemBalanceAchievesEvenSplitBetweenTwoForcedActiveKlassenlehrer()
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        Dim f1 As New Fach With {.Name = "F1"}
        f1.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 4})
        b.Faecher.Add(f1)
        Dim f2 As New Fach With {.Name = "F2"}
        f2.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 4})
        b.Faecher.Add(f2)
        Dim f3 As New Fach With {.Name = "F3"}
        f3.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        b.Faecher.Add(f3)
        Dim f4 As New Fach With {.Name = "F4"}
        f4.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        b.Faecher.Add(f4)
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1, .ErlaubtKlassenlehrerTandem = True})

        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 20, .KlassenlehrerFaehig = True})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 20, .KlassenlehrerFaehig = True})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "F1"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "F2"})
        For Each fach In New List(Of String) From {"F3", "F4"}
            b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = fach})
            b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = fach})
        Next

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=1000.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(0.0, result.Solver.ObjectiveValue, 0.001)

        Dim stundenA = result.Zuweisungen.Where(Function(z) z.Lehrer = "Lehrer A").Sum(Function(z) Stammdaten.WochenstundenFuer(b.Faecher.Single(Function(fa) fa.Name = z.Fach), 1).WochenstundenSoll)
        Dim stundenB = result.Zuweisungen.Where(Function(z) z.Lehrer = "Lehrer B").Sum(Function(z) Stammdaten.WochenstundenFuer(b.Faecher.Single(Function(fa) fa.Name = z.Fach), 1).WochenstundenSoll)
        Assert.AreEqual(6, stundenA, "Ausgewogener Tandem-Split erwartet 6h fuer Lehrer A.")
        Assert.AreEqual(6, stundenB, "Ausgewogener Tandem-Split erwartet 6h fuer Lehrer B.")
    End Sub

    ''' <summary>Springerreserve: Lehrer A ist der einzige Kandidat fuer
    ''' Fach "F" (6h), Deputat=10h, klassenlehrerfaehig (haelt
    ''' fehltKlassenlehrer bei 0, da A ohnehin der einzige aktive
    ''' Kandidat ist - keine Verzerrung durch die Klassenlehrer-Logik).
    ''' MIT SpringerReserveStunden=4 ist der Zielkorridor 10-4=6h = exakt
    ''' die zugewiesenen 6h -&gt; Objective 0. OHNE die Reserve
    ''' (identisches Szenario sonst) ist der Korridor 10h, Abweichung 4h
    ''' &gt; Toleranz 2h -&gt; Ueberschuss 2h -&gt; Objective
    ''' 2*WeightDeputatAbweichung=200. Direkter Vorher/Nachher-Beweis, dass
    ''' die Reserve die Nicht-Ausschoepfung NICHT bestraft.</summary>
    <TestMethod>
    Public Sub SpringerReserveLowersDeputatCorridorWithoutPenalty()
        Dim BuildBestand = Function(springerReserve As Double) As Stammdatenbestand
                                Dim bb As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
                                bb.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
                                Dim f As New Fach With {.Name = "F"}
                                f.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 6})
                                bb.Faecher.Add(f)
                                bb.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
                                bb.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 10, .KlassenlehrerFaehig = True, .SpringerReserveStunden = springerReserve})
                                bb.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "F"})
                                Return bb
                            End Function

        Dim mitReserve = Lehrereinsatzplanung.SolveLehrereinsatz(BuildBestand(4.0), deputatToleranzStunden:=2.0, timeLimitS:=10)
        Assert.IsTrue(mitReserve.Status = CpSolverStatus.Optimal OrElse mitReserve.Status = CpSolverStatus.Feasible, mitReserve.Status.ToString())
        Assert.AreEqual(0.0, mitReserve.Solver.ObjectiveValue, 0.001)

        Dim ohneReserve = Lehrereinsatzplanung.SolveLehrereinsatz(BuildBestand(0.0), deputatToleranzStunden:=2.0, timeLimitS:=10)
        Assert.IsTrue(ohneReserve.Status = CpSolverStatus.Optimal OrElse ohneReserve.Status = CpSolverStatus.Feasible, ohneReserve.Status.ToString())
        Assert.AreEqual(200.0, ohneReserve.Solver.ObjectiveValue, 0.001)
    End Sub

    ''' <summary>Faire Verteilung unbeliebter Faecher (niedrigste
    ''' Prioritaet): zwei Klassen (1a/1b) brauchen je 1x das als Unbeliebt
    ''' markierte Fach "F" (2h), zwei gleichermassen qualifizierte,
    ''' klassenlehrerfaehige Kandidaten (haelt die Klassenlehrer-Logik bei
    ''' 0, solange - wie hier erwartet - jede Klasse nur einen einzigen
    ''' aktiven Kandidaten bekommt: eine 1:1-Aufteilung waere zusaetzlich
    ''' durch die bereits bestehende Pro-Lehrkraft-Buendelungsregel
    ''' geschuetzt, "eine Lehrkraft uebernimmt beide Klassen" wuerde also
    ''' zusaetzlich noch WeightBuendelungVerletzt kosten). Von Hand
    ''' nachgerechnet: eine 1:1-Aufteilung (je 1 unbeliebte Zuweisung pro
    ''' Lehrkraft) ergibt Fairness-Bereich 0 -&gt; Objective 0; das
    ''' beweisbare Optimum erzwingt deshalb die Aufteilung.</summary>
    <TestMethod>
    Public Sub UnbeliebtesFachWirdGleichmaessigVerteilt()
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        Dim f As New Fach With {.Name = "F", .Unbeliebt = True}
        f.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        b.Faecher.Add(f)
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        b.Klassen.Add(New Klasse With {.Name = "1b", .Klassenstufe = 1})

        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 20, .KlassenlehrerFaehig = True})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 20, .KlassenlehrerFaehig = True})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "F"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "F"})

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=1000.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(0.0, result.Solver.ObjectiveValue, 0.001)

        Dim lehrer1a = result.Zuweisungen.Single(Function(z) z.Klasse = "1a").Lehrer
        Dim lehrer1b = result.Zuweisungen.Single(Function(z) z.Klasse = "1b").Lehrer
        Assert.AreNotEqual(lehrer1a, lehrer1b, "Faire Verteilung sollte das unbeliebte Fach auf beide Lehrkraefte aufteilen.")
    End Sub

End Class
