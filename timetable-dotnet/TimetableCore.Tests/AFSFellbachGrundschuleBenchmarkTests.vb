' Phase 2.16: End-to-End-Test mit vollstaendigem Konsolen-Report fuer die
' neue AFS-Fellbach-Grundschule (AFSFellbachStammdatenFixture.vb) -
' Stammdaten -> Lehrereinsatzplanung -> Solver.Solve, mit fuer den Nutzer
' nachvollziehbarer Ausgabe: Stammdatenliste, Lehrerzuordnung (inkl.
' Klassenlehrer) und der fertige Stundenplan der Klasse 4b. Gleiches
' Konsolen-Report-Muster wie LehrerKontinuitaetBenchmarkTests.vb (Phase
' 2.14).
'
' Bewusst UNGEGATET (kein RUN_SLOW_BENCHMARKS noetig) - live gemessen
' loest die komplette Pipeline (12 Klassen, 12 Lehrkraefte) in deutlich
' unter 1s, gleiche Groessenordnung wie StammdatenBWFixtureTests.vb
' (Phase 2.15).
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class AFSFellbachGrundschuleBenchmarkTests

    Private Shared Function ClassText(cell As GridCell) As String
        If cell Is Nothing Then Return "-"
        Return $"{cell.Subject} ({cell.Teacher})"
    End Function

    <TestMethod>
    Public Sub AFSFellbachGrundschuleEndToEndMitKonsolenReport()
        Dim bestand = AFSFellbachStammdatenFixture.BuildAFSFellbachGrundschule()
        Assert.AreEqual(0, StammdatenValidation.ValidateStammdaten(bestand).Count)

        ' === 1. Stammdatenliste ===
        Console.WriteLine("=== Stammdaten: " & bestand.SchulName & " ===")
        Console.WriteLine($"Bundesland: {bestand.Bundesland}, Schulart: {bestand.Schulart}")
        Console.WriteLine()
        Console.WriteLine("--- Klassenstufen ---")
        For Each ks In bestand.Klassenstufen
            Dim klassenNamen = bestand.Klassen.Where(Function(k) k.Klassenstufe = ks.Nummer).Select(Function(k) k.Name)
            Console.WriteLine($"{ks.Bezeichnung} (Stufe {ks.Nummer}): Klassen {String.Join(", ", klassenNamen)}")
        Next
        Console.WriteLine()
        Console.WriteLine("--- Faecher (Wochenstunden je Klassenstufe, Max/Tag) ---")
        For Each fach In bestand.Faecher
            Dim details = fach.Klassenstufen.OrderBy(Function(fk) fk.Klassenstufe).
                Select(Function(fk) $"Kl.{fk.Klassenstufe}: {fk.WochenstundenSoll}h (max {If(fk.MaxProTag.HasValue, fk.MaxProTag.Value.ToString(), "-")}/Tag)")
            Console.WriteLine($"{fach.Name,-14}: {String.Join("; ", details)}")
        Next
        Console.WriteLine()
        Console.WriteLine("--- Lehrkraefte ---")
        For Each l In bestand.Lehrkraefte
            Dim faecher = bestand.FachLehrerZuordnungen.Where(Function(z) z.LehrerName = l.Name).Select(Function(z) z.FachName)
            Console.WriteLine($"{l.Name,-16} Deputat={l.DeputatSollstunden,4}h  Klassenlehrer-faehig={l.KlassenlehrerFaehig,-5}  Faecher: {String.Join(", ", faecher)}")
        Next
        Console.WriteLine($"Gesamt: {bestand.Klassen.Count} Klassen, {bestand.Lehrkraefte.Count} Lehrkraefte, {bestand.Faecher.Count} Faecher, {bestand.Raeume.Count} Raeume")

        ' === 2. Lehrereinsatzplanung ===
        Dim lehrereinsatz = Lehrereinsatzplanung.SolveLehrereinsatz(bestand, timeLimitS:=30)
        Assert.IsTrue(lehrereinsatz.Status = CpSolverStatus.Optimal OrElse lehrereinsatz.Status = CpSolverStatus.Feasible, lehrereinsatz.Status.ToString())
        Dim lehrereinsatzViolations = Verifier.VerifyLehrereinsatz(bestand, lehrereinsatz)
        Assert.AreEqual(0, lehrereinsatzViolations.Count, String.Join(vbLf, lehrereinsatzViolations))

        Console.WriteLine()
        Console.WriteLine($"=== Lehrereinsatzplanung: {lehrereinsatz.Status}, Objective={lehrereinsatz.Solver.ObjectiveValue} ===")
        Console.WriteLine("--- Lehrerzuordnung (pro Lehrkraft: Soll-/Ist-Deputat, Klasse/Fach) ---")
        For Each l In bestand.Lehrkraefte
            Dim eigene = lehrereinsatz.Zuweisungen.Where(Function(z) z.Lehrer = l.Name).ToList()
            Dim summe = eigene.Sum(Function(z) Stammdaten.WochenstundenFuer(
                bestand.Faecher.Single(Function(f) f.Name = z.Fach),
                bestand.Klassen.Single(Function(k) k.Name = z.Klasse).Klassenstufe).WochenstundenSoll)
            Dim zeilen = eigene.Select(Function(z) $"{z.Klasse}/{z.Fach}")
            Console.WriteLine($"{l.Name,-16} Soll={l.DeputatSollstunden,4}h Ist={summe,4}h  [{String.Join(", ", zeilen)}]")
        Next
        Console.WriteLine()
        Console.WriteLine("--- Klassenlehrer je Klasse ---")
        For Each k In bestand.Klassen
            Dim kl = If(lehrereinsatz.Klassenlehrer.ContainsKey(k.Name), lehrereinsatz.Klassenlehrer(k.Name), "(keiner gefunden)")
            Console.WriteLine($"{k.Name,-4}: {kl}")
        Next
        Console.WriteLine(
            "Hinweis: seit dem Phase-2.16-Nachtrag bestraft die " &
            "Zielfunktion zusaetzlich, wenn mehr als eine klassenlehrer- " &
            "faehige Lehrkraft gleichzeitig in derselben Klasse aktiv ist " &
            "(WeightBuendelungVerletzt) - bei ausreichender Deputat- " &
            "Kapazitaet buendelt eine Klasse dadurch ihre Kernfaecher bei " &
            "genau EINER Lehrkraft (echtes Klassenlehrerprinzip). Weiterhin " &
            "ein WEICHES Ziel, kein hartes: bei disjunkt qualifizierten " &
            "Kandidaten bleibt das Szenario loesbar statt faelschlich " &
            "Infeasible zu werden.")

        ' === 3. Uebersetzung in das bestehende Constraint-Format + Stundenplan ===
        Dim assignmentConstraints = Lehrereinsatzplanung.BuildAssignmentConstraints(lehrereinsatz, bestand)
        Dim ent = Stammdaten.BuildEntitiesFragment(bestand)
        Dim data As New Text.Json.Nodes.JsonObject From {
            {"entities", ent},
            {"constraints", New Text.Json.Nodes.JsonArray(assignmentConstraints.Select(Function(c) CType(c, Text.Json.Nodes.JsonNode)).ToArray())}
        }
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count)

        Dim result = Solver.Solve(data, timeLimitS:=30)
        Assert.IsTrue(result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible, result.Status.ToString())
        Dim scheduleViolations = Verifier.VerifySchedule(data, result.Schedule)
        Assert.AreEqual(0, scheduleViolations.Count, String.Join(vbLf, scheduleViolations))

        Console.WriteLine()
        Console.WriteLine($"=== Solver.Solve: {result.Status}, 0 Verstoesse ===")

        Dim days = bestand.Tage
        Dim periods = Enumerable.Range(1, bestand.PeriodsPerDay).ToList()
        Dim grids = Formatting.ToClassGrids(data, result.Schedule)
        Console.WriteLine(Formatting.FormatGrid("4b", grids("4b"), days, periods, AddressOf ClassText))
    End Sub

End Class
