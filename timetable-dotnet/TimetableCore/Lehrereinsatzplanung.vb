' Phase 2.15c: Lehrereinsatzplanung - der von der Nutzeranfrage gewuenschte
' Solver, der Lehrkraefte IDEAL auf Klassen/Faecher verteilt (wer
' unterrichtet was), VOR der bestehenden Tag/Periode-Planung
' (Solver.Solve/SolveTop bleiben komplett unveraendert - siehe
' Lehrereinsatzplanung.BuildAssignmentConstraints fuer die Uebergabe).
' Ein eigenstaendiges CP-SAT-Teilmodell (gleiches Muster wie
' Kursblockung.vb, siehe dessen Kopfkommentar): kein Tag/Periode-Bezug,
' reine Zuordnungs-Entscheidung.
'
' Precondition (wie bei Kursblockung.vb): Aufrufer muessen
' StammdatenValidation.ValidateStammdaten(bestand) vorher aufrufen und 0
' Fehler bestaetigen - dieses Modul re-validiert nicht, dass jedes Fach
' mindestens einen Kandidaten hat; eine unvalidierte Luecke wuerde die
' "genau eine Lehrkraft pro Klasse/Fach"-Summe unten ueber eine leere
' Variablenliste bilden (0 = 1, garantiert Infeasible fuer das gesamte
' Modell statt einer gezielten Fehlermeldung) - exakt der Fall, den
' StammdatenValidation's "Fach ohne qualifizierte Lehrkraft"-Pruefung
' vorher abfaengt.
'
' Nutzerentscheidungen aus der Feinplanungsrunde (Phase 2.15): schlanker
' Kern zuerst - Vollstaendigkeit (hart) + Deputat als Zielkorridor mit
' Toleranz (weich) + Klassenlehrer-Zuweisung (weich) + einfache
' Klassenstufen-Praeferenzen (weich). Weitere moegliche Constraints
' (Kontinuitaet ueber Jahre, fachfremder Einsatz, max. Klassen/Lehrer,
' Teilzeit-Kohaerenz, Tandem-Balance, Springerreserve, Fairness) sind
' dokumentiert, aber bewusst nicht Teil dieses MVP - siehe
' docs/phase2-15-lehrereinsatzplanung.md.
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat

Public NotInheritable Class LehrereinsatzZuweisung
    Public Property Lehrer As String
    Public Property Klasse As String
    Public Property Fach As String
End Class

Public NotInheritable Class LehrereinsatzResult
    Public Property Status As CpSolverStatus
    ''' <summary>Gleiches Muster wie Solver.SolveResult.Solver - erlaubt
    ''' Aufrufern/Tests, z.B. `Solver.ObjectiveValue` zur Diagnose
    ''' auszulesen, ohne dass dieses Modul selbst einen eigenen
    ''' Objective-Wrapper pflegen muesste.</summary>
    Public Property Solver As CpSolver
    ''' <summary>Nothing, sofern Status nicht Optimal/Feasible ist.</summary>
    Public Property Zuweisungen As List(Of LehrereinsatzZuweisung)
    ''' <summary>Klasse -&gt; Klassenlehrer-Name, nur fuer Klassen befuellt,
    ''' fuer die tatsaechlich eine klassenlehrerfaehige, zugewiesene
    ''' Lehrkraft gefunden wurde (weiches Ziel, siehe Modul-Kopfkommentar -
    ''' kann fuer einzelne Klassen fehlen). Das CP-SAT-Modell selbst
    ''' verlangt nur "mindestens ein Kandidat", nie Eindeutigkeit -
    ''' existiert mehr als eine klassenlehrerfaehige, zugewiesene
    ''' Lehrkraft fuer dieselbe Klasse (haeufig der Fall, da nichts eine
    ''' Buendelung aller Kernfaecher einer Klasse bei EINER Lehrkraft
    ''' erzwingt - siehe docs/phase2-15-lehrereinsatzplanung.md's
    ''' zurueckgestellte "Kontinuitaet/Faecher-Buendelung"-Erweiterung),
    ''' waehlt die Extraktion post-hoc diejenige mit den meisten eigenen
    ''' Fach-Zuweisungen in dieser Klasse - die plausibelste Naeherung an
    ''' "die" Klassenlehrkraft, ohne dass das Optimierungsmodell selbst
    ''' dafuer erweitert werden muesste.</summary>
    Public Property Klassenlehrer As Dictionary(Of String, String)
End Class

Public Module Lehrereinsatzplanung

    ' Gewichts-Reihenfolge aus der Feinplanungsrunde: Deputat-Abweichung
    ' (Vertragsverletzung) > Klassenlehrer-Fehlen > Praeferenz-Verletzung -
    ' dokumentierte Konstanten, gleiches Muster wie ScheduleQuality.vb's
    ' Gewichtstabelle.
    Public Const WeightDeputatAbweichung As Integer = 100
    Public Const WeightKlassenlehrerFehlt As Integer = 20
    Public Const WeightPraeferenzVerletzt As Integer = 1

    Private Structure AssignKey
        Public Lehrer As String
        Public Klasse As String
        Public Fach As String
    End Structure

    ''' <summary>Loest die Lehrereinsatzplanung fuer einen kompletten
    ''' Stammdatenbestand (eine Schule). `deputatToleranzStunden`: Breite
    ''' des Zielkorridors um jedes Lehrer-Deputat, innerhalb derer keine
    ''' Abweichung bestraft wird (Nutzerentscheidung "Zielkorridor mit
    ''' Toleranz" statt exakter Gleichung).</summary>
    Public Function SolveLehrereinsatz(bestand As Stammdatenbestand,
                                        Optional deputatToleranzStunden As Double = 2.0,
                                        Optional timeLimitS As Double = 30.0,
                                        Optional seed As Integer = 42,
                                        Optional numWorkers As Integer = 1) As LehrereinsatzResult
        Dim model As New CpModel()
        Dim lehrerByName = bestand.Lehrkraefte.ToDictionary(Function(l) l.Name)
        Dim klasseByName = bestand.Klassen.ToDictionary(Function(k) k.Name)
        Dim fachByName = bestand.Faecher.ToDictionary(Function(f) f.Name)

        ' --- Entscheidungsvariablen: nur fuer kompatible (Lehrer,Klasse,Fach)-Tripel ---
        Dim assign As New Dictionary(Of AssignKey, BoolVar)
        For Each klasse In bestand.Klassen
            For Each fach In Stammdaten.FaecherOfKlassenstufe(bestand, klasse.Klassenstufe)
                For Each lehrer In Stammdaten.LehrerFuerFach(bestand, fach.Name)
                    Dim key As New AssignKey With {.Lehrer = lehrer.Name, .Klasse = klasse.Name, .Fach = fach.Name}
                    assign(key) = model.NewBoolVar($"assign[{lehrer.Name},{klasse.Name},{fach.Name}]")
                Next
            Next
        Next

        ' --- Hart: jede (Klasse,Fach)-Kombination bekommt genau eine Lehrkraft ---
        For Each klasse In bestand.Klassen
            For Each fach In Stammdaten.FaecherOfKlassenstufe(bestand, klasse.Klassenstufe)
                Dim vars = assign.Where(Function(kvp) kvp.Key.Klasse = klasse.Name AndAlso kvp.Key.Fach = fach.Name).
                    Select(Function(kvp) kvp.Value).ToList()
                model.Add(LinearExpr.Sum(vars) = 1)
            Next
        Next

        ' --- Weich: Deputat-Korridor (hinge-loss ueber die Toleranzgrenze
        ' hinaus, sowohl bei Ueber- als auch bei Unterdeckung) ---
        Dim deputatToleranz = CInt(Math.Round(deputatToleranzStunden))
        Dim deputatUeberschuss As New List(Of IntVar)
        For Each lehrer In bestand.Lehrkraefte
            Dim eigeneVars = assign.Where(Function(kvp) kvp.Key.Lehrer = lehrer.Name).ToList()
            If eigeneVars.Count = 0 Then Continue For

            Dim wochenstundenTerms As New List(Of LinearExpr)
            Dim maxMoeglich = 0
            For Each kvp In eigeneVars
                Dim fach = fachByName(kvp.Key.Fach)
                Dim klasse = klasseByName(kvp.Key.Klasse)
                Dim fk = Stammdaten.WochenstundenFuer(fach, klasse.Klassenstufe)
                wochenstundenTerms.Add(kvp.Value * CLng(fk.WochenstundenSoll))
                maxMoeglich += fk.WochenstundenSoll
            Next
            Dim tatsaechlich As LinearExpr = LinearExpr.Sum(wochenstundenTerms)
            Dim sollNetto = CInt(Math.Round(lehrer.DeputatSollstunden - lehrer.Anrechnungsstunden))

            Dim abwPos = model.NewIntVar(0, maxMoeglich, $"deputatUeber[{lehrer.Name}]")
            Dim abwNeg = model.NewIntVar(0, Math.Max(sollNetto, 0) + maxMoeglich, $"deputatUnter[{lehrer.Name}]")
            model.Add(tatsaechlich - sollNetto = abwPos - abwNeg)

            Dim ueberschussPos = model.NewIntVar(0, maxMoeglich, $"deputatUeberschussPos[{lehrer.Name}]")
            model.Add(ueberschussPos >= abwPos - deputatToleranz)
            Dim ueberschussNeg = model.NewIntVar(0, Math.Max(sollNetto, 0) + maxMoeglich, $"deputatUeberschussNeg[{lehrer.Name}]")
            model.Add(ueberschussNeg >= abwNeg - deputatToleranz)

            deputatUeberschuss.Add(ueberschussPos)
            deputatUeberschuss.Add(ueberschussNeg)
        Next

        ' --- Weich: Klassenlehrer-Zuweisung ---
        Dim fehltKlassenlehrer As New List(Of BoolVar)
        Dim istKlassenlehrer As New Dictionary(Of (Lehrer As String, Klasse As String), BoolVar)
        For Each klasse In bestand.Klassen
            Dim kandidaten As New List(Of BoolVar)
            For Each lehrer In bestand.Lehrkraefte.Where(Function(l) l.KlassenlehrerFaehig)
                Dim eigeneVarsInKlasse = assign.Where(Function(kvp) kvp.Key.Lehrer = lehrer.Name AndAlso kvp.Key.Klasse = klasse.Name).
                    Select(Function(kvp) kvp.Value).ToList()
                If eigeneVarsInKlasse.Count = 0 Then Continue For
                Dim unterrichtet = model.NewBoolVar($"unterrichtet[{lehrer.Name},{klasse.Name}]")
                model.Add(LinearExpr.Sum(eigeneVarsInKlasse) >= 1).OnlyEnforceIf(unterrichtet)
                model.Add(LinearExpr.Sum(eigeneVarsInKlasse) = 0).OnlyEnforceIf(unterrichtet.Not())
                istKlassenlehrer((lehrer.Name, klasse.Name)) = unterrichtet
                kandidaten.Add(unterrichtet)
            Next

            Dim hatKlassenlehrer = model.NewBoolVar($"hatKlassenlehrer[{klasse.Name}]")
            If kandidaten.Count > 0 Then
                model.AddMaxEquality(hatKlassenlehrer, kandidaten)
            Else
                model.Add(hatKlassenlehrer = 0)
            End If
            Dim fehlt = model.NewBoolVar($"fehltKlassenlehrer[{klasse.Name}]")
            model.Add(hatKlassenlehrer + fehlt = 1)
            fehltKlassenlehrer.Add(fehlt)
        Next

        ' --- Weich: Klassenstufen-Praeferenzen (eine leere
        ' BevorzugteKlassenstufen-Liste bedeutet "keine Praeferenz", nie
        ' eine Verletzung) ---
        Dim praeferenzVerletzt As New List(Of BoolVar)
        For Each kvp In assign
            Dim lehrer = lehrerByName(kvp.Key.Lehrer)
            If lehrer.BevorzugteKlassenstufen.Count = 0 Then Continue For
            Dim klasse = klasseByName(kvp.Key.Klasse)
            If Not lehrer.BevorzugteKlassenstufen.Contains(klasse.Klassenstufe) Then
                praeferenzVerletzt.Add(kvp.Value)
            End If
        Next

        model.Minimize(
            LinearExpr.Sum(deputatUeberschuss) * CLng(WeightDeputatAbweichung) +
            LinearExpr.Sum(fehltKlassenlehrer) * CLng(WeightKlassenlehrerFehlt) +
            LinearExpr.Sum(praeferenzVerletzt) * CLng(WeightPraeferenzVerletzt))

        Dim solver As New CpSolver()
        solver.StringParameters = $"max_time_in_seconds:{timeLimitS.ToString(Globalization.CultureInfo.InvariantCulture)},random_seed:{seed},num_search_workers:{numWorkers}"
        Dim status = solver.Solve(model)

        Dim result As New LehrereinsatzResult With {.Status = status, .Solver = solver}
        If status = CpSolverStatus.Optimal OrElse status = CpSolverStatus.Feasible Then
            Dim zuweisungen As New List(Of LehrereinsatzZuweisung)
            For Each kvp In assign
                If solver.BooleanValue(kvp.Value) Then
                    zuweisungen.Add(New LehrereinsatzZuweisung With {.Lehrer = kvp.Key.Lehrer, .Klasse = kvp.Key.Klasse, .Fach = kvp.Key.Fach})
                End If
            Next
            result.Zuweisungen = zuweisungen

            Dim klassenlehrer As New Dictionary(Of String, String)
            For Each klasseGroup In istKlassenlehrer.Where(Function(kvp) solver.BooleanValue(kvp.Value)).GroupBy(Function(kvp) kvp.Key.Klasse)
                Dim bester = klasseGroup.
                    OrderByDescending(Function(kvp) zuweisungen.Where(Function(z) z.Lehrer = kvp.Key.Lehrer AndAlso z.Klasse = kvp.Key.Klasse).Count()).
                    First()
                klassenlehrer(klasseGroup.Key) = bester.Key.Lehrer
            Next
            result.Klassenlehrer = klassenlehrer
        End If
        Return result
    End Function

    ''' <summary>Phase 2.15d: uebersetzt ein geloestes LehrereinsatzResult
    ''' rein deterministisch (kein CP-SAT) in das bestehende,
    ''' UNVERAENDERTE Constraint-Format (teacher_subject_assignment/
    ''' weekly_hours[/consecutive_required]) - siehe
    ''' docs/json-constraints-reference.md Abschnitt 5. Zusammen mit
    ''' Stammdaten.BuildEntitiesFragment(bestand) ergibt das ein
    ''' vollstaendiges entities/constraints-JSON, das unveraendert an
    ''' Solver.Solve/SolveTop uebergeben werden kann - keine einzige Zeile
    ''' in Solver.vb/ApplyConstraints aendert sich fuer diese neue
    ''' Faehigkeit.</summary>
    Public Function BuildAssignmentConstraints(result As LehrereinsatzResult, bestand As Stammdatenbestand) As List(Of JsonObject)
        Dim klasseByName = bestand.Klassen.ToDictionary(Function(k) k.Name)
        Dim fachByName = bestand.Faecher.ToDictionary(Function(f) f.Name)

        Dim constraints As New List(Of JsonObject)
        For Each z In result.Zuweisungen
            constraints.Add(New JsonObject From {
                {"type", "teacher_subject_assignment"}, {"class", z.Klasse}, {"subject", z.Fach}, {"teacher", z.Lehrer}
            })

            Dim klasse = klasseByName(z.Klasse)
            Dim fach = fachByName(z.Fach)
            Dim fk = Stammdaten.WochenstundenFuer(fach, klasse.Klassenstufe)

            Dim wh As New JsonObject From {
                {"type", "weekly_hours"}, {"class", z.Klasse}, {"subject", z.Fach}, {"hours_per_week", fk.WochenstundenSoll}
            }
            If fk.MaxProTag.HasValue Then wh("max_per_day") = fk.MaxProTag.Value
            constraints.Add(wh)

            If fach.BlockLength.HasValue Then
                constraints.Add(New JsonObject From {
                    {"type", "consecutive_required"}, {"class", z.Klasse}, {"subject", z.Fach}, {"block_length", fach.BlockLength.Value}
                })
            End If
        Next
        Return constraints
    End Function

End Module
