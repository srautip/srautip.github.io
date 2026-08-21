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

    ''' <summary>Phase 2.20d/e Gate: 2 Klassen (1a,1b), 1 Klassenstufe, 3
    ''' Gruppen-gefuehrte Faecher (Religion-ev/Religion-kath/Ethik, je 2h,
    ''' ein gemeinsamer Parallelverbund) - jede Gruppe umspannt BEIDE
    ''' Klassen. 3 dedizierte, jeweils exakt fuer eine Gruppe qualifizierte
    ''' Lehrkraefte mit Deputat=2h (exakt EINE Gruppe, nicht zwei echte
    ''' Klassen).</summary>
    Private Function BestandMitParallelgruppen() As Stammdatenbestand
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        b.Klassen.Add(New Klasse With {.Name = "1b", .Klassenstufe = 1})

        For Each fachName In {"Religion-ev", "Religion-kath", "Ethik"}
            Dim fach As New Fach With {.Name = fachName}
            fach.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
            b.Faecher.Add(fach)
        Next

        b.Lehrkraefte.Add(New Lehrer With {.Name = "T-ev", .DeputatSollstunden = 2})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "T-kath", .DeputatSollstunden = 2})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "T-eth", .DeputatSollstunden = 2})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "T-ev", .FachName = "Religion-ev"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "T-kath", .FachName = "Religion-kath"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "T-eth", .FachName = "Ethik"})

        b.Schueler.Add(New Schueler With {.Id = "S1", .Klasse = "1a"})
        b.Schueler.Add(New Schueler With {.Id = "S2", .Klasse = "1b"})
        b.Schueler.Add(New Schueler With {.Id = "S3", .Klasse = "1a"})
        b.Schueler.Add(New Schueler With {.Id = "S4", .Klasse = "1b"})
        b.Schueler.Add(New Schueler With {.Id = "S5", .Klasse = "1a"})
        b.Schueler.Add(New Schueler With {.Id = "S6", .Klasse = "1b"})

        b.Gruppen.Add(New Gruppe With {
            .Name = "Religion-ev-Kl1", .FachName = "Religion-ev", .Klassenstufe = 1, .Parallelverbund = "Religion-Kl1",
            .MitgliederSchuelerIds = New List(Of String) From {"S1", "S2"}
        })
        b.Gruppen.Add(New Gruppe With {
            .Name = "Religion-kath-Kl1", .FachName = "Religion-kath", .Klassenstufe = 1, .Parallelverbund = "Religion-Kl1",
            .MitgliederSchuelerIds = New List(Of String) From {"S3", "S4"}
        })
        b.Gruppen.Add(New Gruppe With {
            .Name = "Ethik-Kl1", .FachName = "Ethik", .Klassenstufe = 1, .Parallelverbund = "Religion-Kl1",
            .MitgliederSchuelerIds = New List(Of String) From {"S5", "S6"}
        })

        Return b
    End Function

    <TestMethod>
    Public Sub GruppenGefuehrtesFachZaehltDeputatEinmalProGruppe()
        Dim b = BestandMitParallelgruppen()
        Assert.AreEqual(0, StammdatenValidation.ValidateStammdaten(b).Count)

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=0.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        ' Alle 3 Lehrkraefte sind reine Fachspezialisten (keine
        ' klassenlehrerfaehige Kandidatin fuer 1a/1b vorhanden, da Gruppen-
        ' gefuehrte Faecher strukturell nie in die Klassenlehrer-
        ' Kandidatenliste einer echten Klasse einfliessen) - "fehlt
        ' Klassenlehrer" ist deshalb fuer BEIDE Klassen unvermeidbar und
        ' unabhaengig vom hier eigentlich getesteten Deputat-Mechanismus
        ' (2 x WeightKlassenlehrerFehlt = 40). Waere das Deputat je Gruppe
        ' faelschlich verdoppelt worden (Ist=4h gegen Soll=2h, ausserhalb
        ' der Toleranz 0), kaeme zusaetzlich WeightDeputatAbweichung*... on
        ' top - das waere hier sofort sichtbar als Objective > 40.
        Dim erwarteteKlassenlehrerFehltStrafe = 2.0 * Lehrereinsatzplanung.WeightKlassenlehrerFehlt
        Assert.AreEqual(erwarteteKlassenlehrerFehltStrafe, result.Solver.ObjectiveValue, 0.001,
            "Ausserhalb der (strukturell unvermeidbaren) Klassenlehrer-Fehlt-Strafe darf keine Deputat-Abweichung entstehen (waere das Deputat faelschlich verdoppelt worden, laege Ist=4h gegen Soll=2h vor).")

        Assert.AreEqual(6, result.Zuweisungen.Count, "3 Gruppen x 2 real umspannte Klassen = 6 expandierte Zuweisungen erwartet")
        For Each fachTeacher In New Dictionary(Of String, String) From {{"Religion-ev", "T-ev"}, {"Religion-kath", "T-kath"}, {"Ethik", "T-eth"}}
            Dim lehrerVon1a = result.Zuweisungen.Single(Function(z) z.Klasse = "1a" AndAlso z.Fach = fachTeacher.Key).Lehrer
            Dim lehrerVon1b = result.Zuweisungen.Single(Function(z) z.Klasse = "1b" AndAlso z.Fach = fachTeacher.Key).Lehrer
            Assert.AreEqual(fachTeacher.Value, lehrerVon1a)
            Assert.AreEqual(fachTeacher.Value, lehrerVon1b)
        Next

        Assert.AreEqual(0, Verifier.VerifyLehrereinsatz(b, result).Count)
    End Sub

    ''' <summary>Phase 2.20e Gate: das komplette Ergebnis von
    ''' SolveLehrereinsatz wird ueber BuildAssignmentConstraints +
    ''' Stammdaten.BuildEntitiesFragment in ein echtes entities/constraints-
    ''' JSON uebersetzt und durch den UNVERAENDERTEN Solver.Solve geloest -
    ''' beweist end-to-end, dass die neue "parallel_group"-Constraint
    ''' tatsaechlich dazu fuehrt, dass alle 6 Sessions (3 Faecher x 2
    ''' Klassen) auf einem identischen Slot landen, UND dass sowohl
    ''' Verifier.VerifyLehrereinsatz als auch Verifier.VerifySchedule (inkl.
    ''' des neuen parallel_group-Checks) 0 Verstoesse melden.</summary>
    <TestMethod>
    Public Sub GruppenBasierteZuweisungLoestEndToEndMitSynchronenSessions()
        Dim b = BestandMitParallelgruppen()
        Assert.AreEqual(0, StammdatenValidation.ValidateStammdaten(b).Count)

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=0.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(0, Verifier.VerifyLehrereinsatz(b, result).Count)

        Dim constraints = Lehrereinsatzplanung.BuildAssignmentConstraints(result, b)
        Assert.IsTrue(constraints.Any(Function(c) JsonHelpers.GetString(c, "type") = "parallel_group"),
            "Erwartete genau 1 parallel_group-Constraint fuer den gemeinsamen Parallelverbund")

        Dim entities = Stammdaten.BuildEntitiesFragment(b)
        Dim data As New JsonObject From {
            {"entities", entities},
            {"constraints", New JsonArray(constraints.Select(Function(c) CType(c, JsonNode)).ToArray())}
        }
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count)

        Dim r = Solver.Solve(data, timeLimitS:=10)
        Assert.IsTrue(r.Status = CpSolverStatus.Optimal OrElse r.Status = CpSolverStatus.Feasible, Solver.StatusName(r.Status))
        Dim scheduleViolations = Verifier.VerifySchedule(data, r.Schedule)
        Assert.AreEqual(0, scheduleViolations.Count, String.Join(vbLf, scheduleViolations))

        ' Jedes der 3 Faecher fordert 2 Wochenstunden (siehe
        ' BestandMitParallelgruppen) - da alle Mitglieder zwingend
        ' zusammen feuern, sind das GENAU 2 gemeinsame Slots fuer die
        ' gesamte Gruppe (nicht 1 pro Fach - waeren die Faecher nicht
        ' synchronisiert, kaemen bis zu 6 verschiedene Slots vor).
        Dim slots = r.Schedule.Select(Function(l) (l.Day, l.Period)).Distinct().ToList()
        Assert.AreEqual(2, slots.Count, "Alle Parallelgruppen-Mitglieder muessen an GENAU 2 gemeinsamen Slots (= wochenstunden_soll) synchron auftreten")
        For Each slot In slots
            Assert.AreEqual(6, r.Schedule.Where(Function(l) l.Day = slot.Day AndAlso l.Period = slot.Period).Count(),
                $"Slot {slot}: erwartet alle 6 Mitglieder (3 Faecher x 2 Klassen) gemeinsam aktiv")
        Next
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

    ''' <summary>Phase 2.27: 1 Klassenstufe, 2 Klassen (1a,1b), 1 Gruppen-
    ''' gefuehrtes Fach (Religion-ev, WochenstundenSoll=2, spannt beide
    ''' Klassen), 2 qualifizierte Kandidaten mit ABSICHTLICH unterschiedlichem
    ''' Deputat (A=2h passend/bevorzugt, B=5h/nie-erfuellbare Praeferenz) -
    ''' die Asymmetrie macht eine korrekt EINMAL pro Gruppe gezaehlte
    ''' Deputat-Abweichung (bei Pin auf B) rechnerisch von einer faelschlich
    ''' VERDOPPELTEN (einmal pro real umspannter Klasse) unterscheidbar (bei
    ''' identischem Soll waeren beide Fehlerarten zufaellig betragsgleich -
    ''' siehe Testkommentar unten fuer die Herleitung).</summary>
    Private Function BestandMitGruppeUndZweiKandidaten() As Stammdatenbestand
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        b.Klassen.Add(New Klasse With {.Name = "1b", .Klassenstufe = 1})

        Dim religion As New Fach With {.Name = "Religion-ev"}
        religion.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        b.Faecher.Add(religion)

        b.Schueler.Add(New Schueler With {.Id = "S1", .Klasse = "1a"})
        b.Schueler.Add(New Schueler With {.Id = "S2", .Klasse = "1b"})
        b.Gruppen.Add(New Gruppe With {
            .Name = "Religion-ev-Kl1", .FachName = "Religion-ev", .Klassenstufe = 1,
            .MitgliederSchuelerIds = New List(Of String) From {"S1", "S2"}
        })

        Dim lehrerA As New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 2}
        lehrerA.BevorzugteKlassenstufen.Add(1)
        Dim lehrerB As New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 5}
        lehrerB.BevorzugteKlassenstufen.Add(9) ' nie erfuellbar
        b.Lehrkraefte.Add(lehrerA)
        b.Lehrkraefte.Add(lehrerB)
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "Religion-ev"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "Religion-ev"})

        Return b
    End Function

    ''' <summary>Phase 2.27: beweist, dass eine FesteZuordnung auch fuer ein
    ''' Gruppen-gefuehrtes Fach greift (klasse_name traegt hier den
    ''' Gruppennamen), UND dass das Deputat dabei weiterhin korrekt EINMAL
    ''' pro Gruppe gezaehlt wird - nicht einmal pro real umspannter Klasse
    ''' (Phase-2.20-Korrektheitspunkt, hier fuer den neuen Gruppen-Pin-Pfad
    ''' erneut bestaetigt). Von Hand nachgerechnet (deputatToleranzStunden:=0,
    ''' bei Pin auf Lehrer B):
    ''' - "fehlt Klassenlehrer" ist fuer BEIDE Klassen unvermeidbar (kein
    '''   klassenlehrerfaehiger Kandidat existiert ueberhaupt) = 2*20 = 40.
    ''' - Lehrer A (unbenutzt): Ist=0h vs Soll=2h -> Abweichung 2h*100=200.
    ''' - Lehrer B (gepinnt, KORREKT einmal gezaehlt): Ist=2h vs Soll=5h ->
    '''   Abweichung 3h*100=300.
    ''' - Praeferenz: B's Praeferenz (Klassenstufe 9) bleibt unerfuellt = 1.
    ''' Summe = 40+200+300+1 = 541. Waere das Deputat faelschlich VERDOPPELT
    ''' worden (Ist=4h statt 2h fuer B), ergaebe sich stattdessen Abweichung
    ''' 1h*100=100 statt 300h -> Summe 341 (Unterschied nur SICHTBAR, weil A
    ''' und B bewusst verschiedenes Soll-Deputat haben - bei identischem Soll
    ''' waeren beide Fehlerarten zufaellig betragsgleich).</summary>
    <TestMethod>
    Public Sub FesteZuordnungOnGruppeForcesLessPreferredTeacherAndCountsDeputatOnce()
        Dim b = BestandMitGruppeUndZweiKandidaten()

        ' Gegenprobe ohne Pin: Solver waehlt A (guenstiger: Praeferenz erfuellt
        ' UND kleinere Deputat-Abweichung, siehe Kommentar oben).
        Dim ohnePin = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=0.0, timeLimitS:=10)
        Assert.IsTrue(ohnePin.Status = CpSolverStatus.Optimal OrElse ohnePin.Status = CpSolverStatus.Feasible, ohnePin.Status.ToString())
        Assert.IsTrue(ohnePin.Zuweisungen.All(Function(z) z.Lehrer = "Lehrer A"))

        b.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Lehrer B", .KlasseName = "Religion-ev-Kl1", .FachName = "Religion-ev"})
        Assert.AreEqual(0, StammdatenValidation.ValidateStammdaten(b).Count)

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=0.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(541.0, result.Solver.ObjectiveValue, 0.001,
            "Abweichung von 541 deutet auf eine falsche (z.B. verdoppelte) Deputat-Zaehlung fuer den Gruppen-Pin hin.")

        Assert.AreEqual(2, result.Zuweisungen.Where(Function(z) z.Lehrer = "Lehrer B").Count(), "1a UND 1b muessen expandiert auf Lehrer B zeigen")
        Assert.IsFalse(result.Zuweisungen.Any(Function(z) z.Lehrer = "Lehrer A"), "Lehrer A haette durch die Vollstaendigkeits-Constraint ausgeschlossen sein muessen.")
        Assert.AreEqual(0, Verifier.VerifyLehrereinsatz(b, result).Count)
    End Sub

    ''' <summary>Phase 2.26: beweist, dass eine FesteZuordnung den Solver
    ''' auch GEGEN die guenstigere, "natuerliche" Alternative zwingt.
    ''' Lehrer A hat eine erfuellte Klassenstufen-Praeferenz (Kosten 0),
    ''' Lehrer B eine nie erfuellbare (Kosten WeightPraeferenzVerletzt) -
    ''' ohne Pin waehlt der Solver deterministisch A. Mit einer
    ''' FesteZuordnung auf Lehrer B MUSS der Solver B waehlen, trotz des
    ''' Praeferenz-Mehrpreises - von Hand nachgerechnet: Objective = genau
    ''' 1 * WeightPraeferenzVerletzt (0 fehlender Klassenlehrer, da B
    ''' klassenlehrerfaehig ist). deputatToleranzStunden bewusst grosszuegig
    ''' gesetzt, damit der jeweils NICHT gewaehlte, aber weiterhin
    ''' kandidatenfaehige Lehrer (0h tatsaechlich vs. 4h Deputat-Soll) keine
    ''' Deputat-Abweichungs-Strafe beitraegt - der Test soll ausschliesslich
    ''' den Praeferenz-Effekt isolieren. Beweist gleichzeitig, dass
    ''' Vollstaendigkeit weiterhin exakt 1 Zuweisung liefert (Lehrer A
    ''' taucht NICHT mehr in den Zuweisungen fuer 1a/Deutsch auf).</summary>
    <TestMethod>
    Public Sub FesteZuordnungForcesLessPreferredTeacherOverCheaperAlternative()
        Dim b = Bestand(zweiteKlasse:=False)
        Dim lehrerA As New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 4, .KlassenlehrerFaehig = True}
        lehrerA.BevorzugteKlassenstufen.Add(1)
        Dim lehrerB As New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 4, .KlassenlehrerFaehig = True}
        lehrerB.BevorzugteKlassenstufen.Add(9) ' nie erfuellbar
        b.Lehrkraefte.Add(lehrerA)
        b.Lehrkraefte.Add(lehrerB)
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "Deutsch"})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "Deutsch"})

        ' Gegenprobe ohne Pin: Solver waehlt A (Objective 0).
        Dim ohnePin = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=10.0, timeLimitS:=10)
        Assert.AreEqual(0.0, ohnePin.Solver.ObjectiveValue, 0.001)
        Assert.AreEqual("Lehrer A", ohnePin.Zuweisungen.Single().Lehrer)

        b.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Lehrer B", .KlasseName = "1a", .FachName = "Deutsch"})
        Assert.AreEqual(0, StammdatenValidation.ValidateStammdaten(b).Count)

        Dim result = Lehrereinsatzplanung.SolveLehrereinsatz(b, deputatToleranzStunden:=10.0, timeLimitS:=10)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Assert.AreEqual(CDbl(Lehrereinsatzplanung.WeightPraeferenzVerletzt), result.Solver.ObjectiveValue, 0.001)

        Assert.AreEqual(1, result.Zuweisungen.Where(Function(z) z.Klasse = "1a" AndAlso z.Fach = "Deutsch").Count())
        Assert.AreEqual("Lehrer B", result.Zuweisungen.Single().Lehrer)
        Assert.IsFalse(result.Zuweisungen.Any(Function(z) z.Lehrer = "Lehrer A"), "Lehrer A haette durch die Vollstaendigkeits-Constraint ausgeschlossen sein muessen.")
    End Sub

    ''' <summary>Phase 2.26: Kanarienvogel - bestaetigt, dass der defensive
    ''' Throw in SolveLehrereinsatz tatsaechlich feuert, wenn eine
    ''' FesteZuordnung (bewusst unter Umgehung von StammdatenValidation
    ''' konstruiert) keinen Kandidaten im Modell hat.</summary>
    <TestMethod>
    Public Sub FesteZuordnungWithoutResolvableCandidateThrows()
        Dim b = Bestand(zweiteKlasse:=False)
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 4})
        ' Bewusst KEINE FachLehrerZuordnung fuer Lehrer A/Deutsch -
        ' StammdatenValidation wuerde das eigentlich schon vorher stoppen;
        ' dieser Test simuliert einen umgangenen/fehlerhaften Aufrufer
        ' direkt gegen SolveLehrereinsatz.
        b.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Lehrer A", .KlasseName = "1a", .FachName = "Deutsch"})
        Assert.ThrowsException(Of InvalidOperationException)(Sub() Lehrereinsatzplanung.SolveLehrereinsatz(b, timeLimitS:=10))
    End Sub

End Class
