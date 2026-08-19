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
'
' Phase 2.16-Nachtrag: die urspruengliche Klassenlehrer-Zuweisung verlangte
' nur "mindestens ein klassenlehrerfaehiger Kandidat aktiv" - live am
' AFS-Fellbach-Benchmark sichtbar geworden, dass das haeufig zu mehreren
' TEILWEISE aktiven Klassenlehrer-Kandidaten pro Klasse fuehrte (jeder nur
' 1 von 3 Kernfaechern), statt einem "richtigen" Klassenlehrer, der alle
' seine Faecher fuer eine Klasse buendelt. Neues weiches Ziel
' `WeightBuendelungVerletzt` bestraft mehr als einen gleichzeitig aktiven
' klassenlehrerfaehigen Kandidaten pro Klasse - siehe den Kommentar am
' entsprechenden Modellblock unten fuer die genaue Begruendung, warum das
' weiterhin ein weiches statt hartes Ziel bleibt.
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
    ''' kann fuer einzelne Klassen fehlen). Seit der Buendelungs-Erweiterung
    ''' (Phase 2.16-Nachtrag) ist "mehr als eine klassenlehrerfaehige,
    ''' aktive Lehrkraft pro Klasse" selbst ein weiches, bestraftes Ziel
    ''' (`WeightBuendelungVerletzt`) - im Regelfall gibt es deshalb genau
    ''' einen Kandidaten. Bleiben trotzdem mehrere uebrig (das Modell
    ''' erzwingt Eindeutigkeit weiterhin nicht hart, um bei ungewoehnlichen
    ''' Stammdaten nicht unnoetig Infeasible zu werden), waehlt die
    ''' Extraktion post-hoc denjenigen mit den meisten eigenen
    ''' Fach-Zuweisungen in dieser Klasse als Fallback.</summary>
    Public Property Klassenlehrer As Dictionary(Of String, String)
End Class

Public Module Lehrereinsatzplanung

    ' Gewichts-Reihenfolge aus der Feinplanungsrunde: Deputat-Abweichung
    ' (Vertragsverletzung) > Klassenlehrer-Fehlen/-Buendelung > Praeferenz-
    ' Verletzung - dokumentierte Konstanten, gleiches Muster wie
    ' ScheduleQuality.vb's Gewichtstabelle. Klassenlehrer-Fehlen und
    ' Buendelungs-Verletzung sind zwei Seiten derselben Frage ("hat diese
    ' Klasse EINE klare Klassenlehrkraft?") und liegen deshalb bewusst auf
    ' derselben Gewichtsstufe (Phase 2.16-Nachtrag).
    Public Const WeightDeputatAbweichung As Integer = 100
    Public Const WeightKlassenlehrerFehlt As Integer = 20
    Public Const WeightBuendelungVerletzt As Integer = 20
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

        ' --- Weich: Klassenlehrer-Zuweisung + Faecher-Buendelung ---
        ' "unterrichtet[l,k]" ist wahr, sobald Lehrkraft l IRGENDEIN Fach
        ' von Klasse k unterrichtet. hatKlassenlehrer[k] verlangt weiterhin
        ' nur "mindestens ein klassenlehrerfaehiger Kandidat aktiv"
        ' (Nutzerentscheidung Phase 2.15: weiches Ziel). Neu (Phase
        ' 2.16-Nachtrag, Nutzerwunsch "richtiger Klassenlehrer"):
        ' zusaetzlich wird bestraft, wenn MEHR als eine klassenlehrerfaehige
        ' Lehrkraft in derselben Klasse aktiv ist - erzwingt bei
        ' hinreichender Deputat-Kapazitaet, dass eine Klasse ihre
        ' klassenlehrerfaehig unterrichteten Faecher (in der Praxis: die
        ' Kernfaecher) BUENDELT statt sie ueber mehrere Klassenlehrer-
        ' Kandidaten zu verstreuen. Bewusst weiterhin ein weiches statt
        ' hartes Ziel: ein hartes "hoechstens 1 aktiv" koennte bei
        ' Stammdaten, in denen die klassenlehrerfaehigen Kandidaten nicht
        ' alle fuer dieselben Faecher qualifiziert sind, unnoetig
        ' Infeasible werden lassen (Vollstaendigkeit koennte dann pro Fach
        ' unterschiedliche Kandidaten erzwingen).
        Dim fehltKlassenlehrer As New List(Of BoolVar)
        Dim buendelungVerletzt As New List(Of BoolVar)
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

            If kandidaten.Count > 1 Then
                Dim mehrereAktiv = model.NewBoolVar($"buendelungVerletzt[{klasse.Name}]")
                model.Add(LinearExpr.Sum(kandidaten) <= 1).OnlyEnforceIf(mehrereAktiv.Not())
                buendelungVerletzt.Add(mehrereAktiv)
            End If
        Next

        ' Symmetrische Ergaenzung (Phase 2.16-Nachtrag 3, Live-Rueckmeldung:
        ' "ueblicherweise hat ein Klassenlehrer nur eine Klasse als
        ' Klassenlehrer"): die obige Schleife verhindert nur, dass eine
        ' Klasse MEHRERE klassenlehrerfaehige Kandidaten gleichzeitig hat -
        ' sie verhindert NICHT, dass EINE Lehrkraft bei ausreichender
        ' Deputat-Kapazitaet als buendelnder Klassenlehrer mehrerer
        ' verschiedener Klassen auftritt (in der Realitaet unueblich - eine
        ' Lehrkraft mag mehrere Klassen unterrichten, ist aber ueblicherweise
        ' nur fuer EINE davon die Klassenlehrkraft). Gleiches Muster wie
        ' oben, nur nach Lehrkraft statt nach Klasse gruppiert, und in
        ' denselben Buendelungs-Gewichtstopf einzahlend (beide Richtungen
        ' beantworten dieselbe Frage: "hat diese Klasse GENAU EINE klare
        ' Klassenlehrkraft, und ist diese Lehrkraft NUR fuer diese eine
        ' Klasse zustaendig?").
        For Each lehrerGroup In istKlassenlehrer.GroupBy(Function(kvp) kvp.Key.Lehrer)
            Dim vars = lehrerGroup.Select(Function(kvp) kvp.Value).ToList()
            If vars.Count > 1 Then
                Dim mehrereKlassen = model.NewBoolVar($"mehrfacheKlassenlehrerrolle[{lehrerGroup.Key}]")
                model.Add(LinearExpr.Sum(vars) <= 1).OnlyEnforceIf(mehrereKlassen.Not())
                buendelungVerletzt.Add(mehrereKlassen)
            End If
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
            LinearExpr.Sum(buendelungVerletzt) * CLng(WeightBuendelungVerletzt) +
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
    ''' weekly_hours[/consecutive_required]/no_overlap) - siehe
    ''' docs/json-constraints-reference.md Abschnitt 5. Zusammen mit
    ''' Stammdaten.BuildEntitiesFragment(bestand) ergibt das ein
    ''' vollstaendiges entities/constraints-JSON, das unveraendert an
    ''' Solver.Solve/SolveTop uebergeben werden kann - keine einzige Zeile
    ''' in Solver.vb/ApplyConstraints aendert sich fuer diese neue
    ''' Faehigkeit.
    '''
    ''' Emittiert zwingend `no_overlap(resource:="class", ...)` fuer jede
    ''' betroffene Klasse UND `no_overlap(resource:="teacher", ...)` fuer
    ''' jede betroffene Lehrkraft (live in Phase 2.16 als fehlend entdeckt:
    ''' ohne diese beiden Regeln haeuft `Solver.Solve` mangels jeder
    ''' Kollisions-Vermeidung alle Wochenstunden einer Klasse/Lehrkraft in
    ''' denselben Tag/Periode-Slot, da `weekly_hours` nur die
    ''' Gesamtanzahl zaehlt, nie die Verteilung auf verschiedene Slots
    ''' erzwingt - `Verifier.VerifySchedule` kann eine so fehlende Regel
    ''' strukturell nicht selbst auffangen, da es nur bereits vorhandene
    ''' Constraint-Typen prueft, siehe Verifier.vb's Kopfkommentar).</summary>
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

        For Each klasseName In result.Zuweisungen.Select(Function(z) z.Klasse).Distinct()
            constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", klasseName}})
        Next
        For Each lehrerName In result.Zuweisungen.Select(Function(z) z.Lehrer).Distinct()
            constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", lehrerName}})
        Next

        Return constraints
    End Function

End Module
