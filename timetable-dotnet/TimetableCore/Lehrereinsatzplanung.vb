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
    ''' <summary>Phase 2.17: gleiche Gewichtsstufe wie Klassenlehrer-Fehlen/
    ''' Buendelung - beide beantworten dieselbe Grundfrage "bleibt die
    ''' Lehrer-Klasse-Zuordnung stabil?", nur ueber Jahre statt innerhalb
    ''' eines Schuljahres.</summary>
    Public Const WeightKontinuitaetVerletzt As Integer = 20
    Public Const WeightFachfremdEinsatz As Integer = 10
    Public Const WeightMaxKlassenVerletzt As Integer = 5
    Public Const WeightMaxFaecherVerletzt As Integer = 5
    Public Const WeightTandemBalance As Integer = 5
    Public Const WeightPraeferenzVerletzt As Integer = 1
    ''' <summary>Phase 2.17: bewusst dieselbe, niedrigste Gewichtsstufe wie
    ''' WeightPraeferenzVerletzt - "faire Verteilung unbeliebter Faecher"
    ''' war in der Nutzeranfrage explizit als niedrigste Prioritaet
    ''' benannt.</summary>
    Public Const WeightUnbeliebteFaecherUngleichheit As Integer = 1

    ''' <summary>Phase 2.20d: `IstGruppe`=True marks a Gruppen-gefuehrtes
    ''' Fach (z.B. Religion-ev/-kath/Ethik ueber eine klassenuebergreifende
    ''' Stammdaten.Gruppe) - `Klasse` then holds the Gruppe's Name, not a
    ''' real Klasse.Name (Gruppen and Klassen are assumed to use disjoint
    ''' name spaces, same convention as everywhere else Klasse.Name is used
    ''' as a dictionary/lookup key in this project).</summary>
    Private Structure AssignKey
        Public Lehrer As String
        Public Klasse As String
        Public Fach As String
        Public IstGruppe As Boolean
    End Structure

    ''' <summary>Loest die Lehrereinsatzplanung fuer einen kompletten
    ''' Stammdatenbestand (eine Schule). `deputatToleranzStunden`: Breite
    ''' des Zielkorridors um jedes Lehrer-Deputat, innerhalb derer keine
    ''' Abweichung bestraft wird (Nutzerentscheidung "Zielkorridor mit
    ''' Toleranz" statt exakter Gleichung). `vorjahresZuordnung` (Phase
    ''' 2.17): optionale Vorjahres-Zuordnung (Klasse-diesjaehrigen-Namens,
    ''' Fach) -&gt; Lehrername - jedes fortbestehende Fach wird bevorzugt
    ''' erneut demselben Lehrer zugewiesen (weiches Ziel, siehe
    ''' WeightKontinuitaetVerletzt). Ein Eintrag, dessen Lehrer diese Runde
    ''' nicht (mehr) qualifiziert/kandidatenfaehig ist, wird stillschweigend
    ''' uebersprungen - ehrlich keine Kontinuitaet moeglich, exakt wie beim
    ''' rein fixture-seitigen Kontinuitaetsmuster aus Phase 2.14.</summary>
    Public Function SolveLehrereinsatz(bestand As Stammdatenbestand,
                                        Optional deputatToleranzStunden As Double = 2.0,
                                        Optional vorjahresZuordnung As Dictionary(Of (Klasse As String, Fach As String), String) = Nothing,
                                        Optional timeLimitS As Double = 30.0,
                                        Optional seed As Integer = 42,
                                        Optional numWorkers As Integer = 1) As LehrereinsatzResult
        Dim model As New CpModel()
        Dim lehrerByName = bestand.Lehrkraefte.ToDictionary(Function(l) l.Name)
        Dim klasseByName = bestand.Klassen.ToDictionary(Function(k) k.Name)
        Dim fachByName = bestand.Faecher.ToDictionary(Function(f) f.Name)
        Dim gruppeByName = bestand.Gruppen.ToDictionary(Function(g) g.Name)
        Dim fachfremdSet As New HashSet(Of (Lehrer As String, Fach As String))(
            bestand.FachLehrerZuordnungen.Where(Function(z) z.Fachfremd).Select(Function(z) (z.LehrerName, z.FachName)))

        ' Phase 2.20d: welche (Klassenstufe,Fach)-Kombinationen sind ueber
        ' eine klassenuebergreifende Gruppe statt normal pro Klasse
        ' gefuehrt? StammdatenValidation garantiert bereits Eindeutigkeit
        ' innerhalb eines Parallelverbunds (paarweise verschiedene
        ' fach_name); zwei komplett unabhaengige Gruppen fuer dieselbe
        ' (Klassenstufe,Fach)-Kombination sind Stammdaten-seitig nicht
        ' ausgeschlossen, werden hier aber deterministisch auf die zuletzt
        ' gesehene reduziert (kein Anspruch auf Mehrdeutigkeits-Detektion
        ' in diesem Modul - StammdatenValidation ist dafuer zustaendig).
        Dim gruppeFuerKlassenstufeFach As New Dictionary(Of (Klassenstufe As Integer, Fach As String), Gruppe)
        For Each gruppe In bestand.Gruppen.Where(Function(g) g.FachName IsNot Nothing AndAlso g.Klassenstufe.HasValue)
            gruppeFuerKlassenstufeFach((gruppe.Klassenstufe.Value, gruppe.FachName)) = gruppe
        Next

        ' Helper: liefert die fuer eine AssignKey-Zuweisung massgebliche
        ' Klassenstufe - bei Gruppen-Eintraegen (Klasse=Gruppe.Name) ueber
        ' die Gruppe selbst, sonst wie bisher ueber die echte Klasse.
        Dim KlassenstufeFuer = Function(key As AssignKey) As Integer
                                    If key.IstGruppe Then Return gruppeByName(key.Klasse).Klassenstufe.Value
                                    Return klasseByName(key.Klasse).Klassenstufe
                                End Function
        ' Phase 2.17 (Tandem-Balance): Sentinel-Obergrenze fuer den
        ' Min-Trick unten - garantiert groesser als die Wochenstunden
        ' irgendeiner einzelnen Klasse (Summe ALLER Fach-Wochenstunden der
        ' ganzen Schule ist eine sichere, grobe obere Schranke).
        Dim bigStunden As Long = bestand.Faecher.SelectMany(Function(f) f.Klassenstufen).Sum(Function(fk) CLng(fk.WochenstundenSoll)) + 1

        ' --- Entscheidungsvariablen: nur fuer kompatible (Lehrer,Klasse,Fach)-Tripel ---
        ' Phase 2.17: Teilzeit-Tage-Kohaerenz ist ein HARTER Vorfilter -
        ' ein Kandidat, dessen Praesenztage das Wochenpensum strukturell
        ' nicht tragen koennen, wird gar nicht erst als Variable erzeugt
        ' (Nutzerentscheidung, siehe Stammdaten.IstTeilzeitKohaerent).
        ' Fachfremd markierte Kandidaten bleiben dagegen zulaessig - nur
        ' ihre AKTIVE Zuweisung wird spaeter weich bestraft (siehe
        ' fachfremdEinsatz unten), sie werden hier bereits gesammelt.
        Dim assign As New Dictionary(Of AssignKey, BoolVar)
        Dim fachfremdEinsatz As New List(Of BoolVar)
        For Each klasse In bestand.Klassen
            For Each fach In Stammdaten.FaecherOfKlassenstufe(bestand, klasse.Klassenstufe)
                ' Phase 2.20d: ein Gruppen-gefuehrtes Fach bekommt in
                ' diesem Zweig KEINE Pro-Klasse-Variable - es wird
                ' stattdessen unten einmal pro Gruppe erzeugt.
                If gruppeFuerKlassenstufeFach.ContainsKey((klasse.Klassenstufe, fach.Name)) Then Continue For
                Dim fk = Stammdaten.WochenstundenFuer(fach, klasse.Klassenstufe)
                For Each lehrer In Stammdaten.LehrerFuerFach(bestand, fach.Name)
                    If Not Stammdaten.IstTeilzeitKohaerent(lehrer, bestand, fk) Then Continue For
                    Dim key As New AssignKey With {.Lehrer = lehrer.Name, .Klasse = klasse.Name, .Fach = fach.Name}
                    Dim v = model.NewBoolVar($"assign[{lehrer.Name},{klasse.Name},{fach.Name}]")
                    assign(key) = v
                    If fachfremdSet.Contains((lehrer.Name, fach.Name)) Then fachfremdEinsatz.Add(v)
                Next
            Next
        Next

        ' Phase 2.20d: einmal pro Gruppe statt pro Klasse - eine
        ' klassenuebergreifende Gruppe (z.B. Religion-ev-Kl1, umspannt 1a
        ' UND 1b) bekommt genau EINEN Kandidatensatz, nicht je einen pro
        ' real umspannter Klasse (sonst wuerde derselbe Lehrer mehrfach mit
        ' unabhaengigen Variablen fuer dieselbe tatsaechliche Unterrichts-
        ' einheit auftreten, was das Deputat faelschlich vervielfachen
        ' wuerde - Plan-Agent-Befund, siehe Phase-2.20-Plan).
        For Each gruppe In bestand.Gruppen.Where(Function(g) g.FachName IsNot Nothing AndAlso g.Klassenstufe.HasValue)
            Dim fach = fachByName(gruppe.FachName)
            Dim fk = Stammdaten.WochenstundenFuer(fach, gruppe.Klassenstufe.Value)
            For Each lehrer In Stammdaten.LehrerFuerFach(bestand, fach.Name)
                If Not Stammdaten.IstTeilzeitKohaerent(lehrer, bestand, fk) Then Continue For
                Dim key As New AssignKey With {.Lehrer = lehrer.Name, .Klasse = gruppe.Name, .Fach = fach.Name, .IstGruppe = True}
                Dim v = model.NewBoolVar($"assign[{lehrer.Name},{gruppe.Name},{fach.Name},gruppe]")
                assign(key) = v
                If fachfremdSet.Contains((lehrer.Name, fach.Name)) Then fachfremdEinsatz.Add(v)
            Next
        Next

        ' --- Hart: jede (Klasse,Fach)-Kombination bekommt genau eine Lehrkraft ---
        For Each klasse In bestand.Klassen
            For Each fach In Stammdaten.FaecherOfKlassenstufe(bestand, klasse.Klassenstufe)
                If gruppeFuerKlassenstufeFach.ContainsKey((klasse.Klassenstufe, fach.Name)) Then Continue For
                Dim vars = assign.Where(Function(kvp) kvp.Key.Klasse = klasse.Name AndAlso kvp.Key.Fach = fach.Name).
                    Select(Function(kvp) kvp.Value).ToList()
                model.Add(LinearExpr.Sum(vars) = 1)
            Next
        Next

        ' --- Hart: jede (Gruppe,Fach)-Kombination bekommt ebenfalls genau
        ' eine Lehrkraft (dieselbe Vollstaendigkeitsregel, nur ueber
        ' IstGruppe statt ueber echte Klassen partitioniert) ---
        For Each gruppe In bestand.Gruppen.Where(Function(g) g.FachName IsNot Nothing AndAlso g.Klassenstufe.HasValue)
            Dim vars = assign.Where(Function(kvp) kvp.Key.IstGruppe AndAlso kvp.Key.Klasse = gruppe.Name AndAlso kvp.Key.Fach = gruppe.FachName).
                Select(Function(kvp) kvp.Value).ToList()
            model.Add(LinearExpr.Sum(vars) = 1)
        Next

        ' --- Hart: FesteZuordnungen (Phase 2.26) - explizite Lehrer-Klasse-
        ' Fach-Pinnung aus den Stammdaten. Precondition: StammdatenValidation.
        ' ValidateStammdaten muss VOR diesem Aufruf 0 Fehler bestaetigt haben -
        ' das garantiert bereits, dass klasse_name/fach_name/lehrer_name
        ' bekannt sind, die Lehrkraft fuer das Fach qualifiziert UND
        ' teilzeit-tage-kohaerent ist, der zugehoerige assign-Key also
        ' existieren MUSS. Trotzdem defensiv per Throw statt stillem
        ' Ueberspringen abgesichert (anders als vorjahresZuordnung unten, wo
        ' ein nicht aufloesbarer Eintrag ein legitimer, erwartbarer Fall
        ' ist): ein harter Pin, der intern nicht greift, waere ein stiller
        ' Korrektheitsverlust, den niemand bemerken wuerde. Gleiches
        ' Verteidigungs-in-die-Tiefe-Prinzip wie der Teilzeit-Kohaerenz-
        ' Vorfilter (Phase 2.17). Da die "genau 1 Lehrkraft pro (Klasse,
        ' Fach)"-Summe oben bereits alle Kandidaten dieser Kombination
        ' enthaelt, genuegt model.Add(assign(key) = 1) - CP-SAT leitet
        ' automatisch her, dass jede andere Variable derselben Summe 0 sein
        ' muss.
        For Each fz In bestand.FesteZuordnungen
            Dim key As New AssignKey With {.Lehrer = fz.LehrerName, .Klasse = fz.KlasseName, .Fach = fz.FachName}
            If Not assign.ContainsKey(key) Then
                Throw New InvalidOperationException(
                    $"feste_zuordnungen: {fz.LehrerName}/{fz.KlasseName}/{fz.FachName} hat keinen Kandidaten im Modell - " &
                    "StammdatenValidation.ValidateStammdaten haette das VOR diesem Aufruf abfangen muessen.")
            End If
            model.Add(assign(key) = 1)
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
                Dim fk = Stammdaten.WochenstundenFuer(fach, KlassenstufeFuer(kvp.Key))
                wochenstundenTerms.Add(kvp.Value * CLng(fk.WochenstundenSoll))
                maxMoeglich += fk.WochenstundenSoll
            Next
            Dim tatsaechlich As LinearExpr = LinearExpr.Sum(wochenstundenTerms)
            ' Phase 2.17: Springerreserve senkt den Zielkorridor genau wie
            ' Anrechnungsstunden (0 = heutiges Verhalten) - eine bewusst
            ' freigehaltene Lehrkraft wird dadurch fuer ihre Nicht-
            ' Ausschoepfung nicht bestraft.
            Dim sollNetto = CInt(Math.Round(lehrer.DeputatSollstunden - lehrer.Anrechnungsstunden - lehrer.SpringerReserveStunden))

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
        Dim tandemRanges As New List(Of IntVar)
        For Each klasse In bestand.Klassen
            Dim kandidaten As New List(Of BoolVar)
            Dim stundenExprs As New List(Of LinearExpr)
            Dim stundenSentinelExprs As New List(Of LinearExpr)
            For Each lehrer In bestand.Lehrkraefte.Where(Function(l) l.KlassenlehrerFaehig)
                Dim eigeneVarsInKlasse = assign.Where(Function(kvp) kvp.Key.Lehrer = lehrer.Name AndAlso kvp.Key.Klasse = klasse.Name).ToList()
                If eigeneVarsInKlasse.Count = 0 Then Continue For
                Dim vars = eigeneVarsInKlasse.Select(Function(kvp) kvp.Value).ToList()
                Dim unterrichtet = model.NewBoolVar($"unterrichtet[{lehrer.Name},{klasse.Name}]")
                model.Add(LinearExpr.Sum(vars) >= 1).OnlyEnforceIf(unterrichtet)
                model.Add(LinearExpr.Sum(vars) = 0).OnlyEnforceIf(unterrichtet.Not())
                istKlassenlehrer((lehrer.Name, klasse.Name)) = unterrichtet
                kandidaten.Add(unterrichtet)

                If klasse.ErlaubtKlassenlehrerTandem Then
                    Dim stundenTerms = eigeneVarsInKlasse.Select(Function(kvp)
                        Dim fk = Stammdaten.WochenstundenFuer(fachByName(kvp.Key.Fach), klasse.Klassenstufe)
                        Return CType(kvp.Value * CLng(fk.WochenstundenSoll), LinearExpr)
                    End Function).ToList()
                    Dim stundenExpr As LinearExpr = LinearExpr.Sum(stundenTerms)
                    stundenExprs.Add(stundenExpr)
                    ' Sentinel-Min-Trick (identisch zur bereits live
                    ' verifizierten Lehrer-Arbeitstage-Ausgewogenheit in
                    ' SolveTopObjective.vb): nur AKTIVE Kandidaten sollen
                    ' fuer das Minimum zaehlen, sonst wuerde ein inaktiver
                    ' Kandidat (0 Stunden) das Minimum immer auf 0 ziehen.
                    Dim sentinelVar = model.NewIntVar(0, bigStunden, $"tandemSentinel[{lehrer.Name},{klasse.Name}]")
                    model.Add(sentinelVar = stundenExpr).OnlyEnforceIf(unterrichtet)
                    model.Add(sentinelVar = bigStunden).OnlyEnforceIf(unterrichtet.Not())
                    stundenSentinelExprs.Add(sentinelVar)
                End If
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

            ' Phase 2.17: Tandem-Klassen duerfen bis zu 2 gleichzeitig
            ' aktive Klassenlehrer-Kandidaten haben, alle anderen wie
            ' bisher hoechstens 1 (Nachtrag 2/3).
            Dim buendelungsSchwelle = If(klasse.ErlaubtKlassenlehrerTandem, 2, 1)
            If kandidaten.Count > buendelungsSchwelle Then
                Dim mehrereAktiv = model.NewBoolVar($"buendelungVerletzt[{klasse.Name}]")
                model.Add(LinearExpr.Sum(kandidaten) <= buendelungsSchwelle).OnlyEnforceIf(mehrereAktiv.Not())
                buendelungVerletzt.Add(mehrereAktiv)
            End If

            If klasse.ErlaubtKlassenlehrerTandem AndAlso stundenExprs.Count > 0 Then
                Dim tandemMax = model.NewIntVar(0, bigStunden, $"tandemMax[{klasse.Name}]")
                model.AddMaxEquality(tandemMax, stundenExprs)
                Dim tandemMin = model.NewIntVar(0, bigStunden, $"tandemMin[{klasse.Name}]")
                model.AddMinEquality(tandemMin, stundenSentinelExprs)
                ' Ungleichung statt Gleichheit (Hinge-Trick, wie im
                ' Deputat-Korridor oben): faengt den Entartungsfall "kein
                ' einziger Kandidat aktiv" ab, bei dem tandemMax (0, da
                ' rohe Werte) kleiner als tandemMin (bigStunden, da
                ' Sentinel-substituiert) waere - eine erzwungene Gleichheit
                ' waere dort unloesbar, waehrend die Zielfunktion
                ' tandemRange dank positivem Gewicht ohnehin auf 0 druecken
                ' will, sobald das zulaessig ist.
                Dim tandemRange = model.NewIntVar(0, bigStunden, $"tandemRange[{klasse.Name}]")
                model.Add(tandemRange >= tandemMax - tandemMin)
                tandemRanges.Add(tandemRange)
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

        ' --- Weich: Kontinuitaet ueber Jahre (Phase 2.17, gilt fuer ALLE
        ' Faecher der Klasse, Nutzerentscheidung). Mathematisch aequivalent
        ' zu "Sum((1-assign)-Terme) minimieren" (unterscheidet sich davon
        ' nur um eine von den Entscheidungsvariablen unabhaengige additive
        ' Konstante - dieselbe Loesung minimiert beides), aber ohne eine
        ' Integer-minus-LinearExpr-Subtraktion zu brauchen, deren
        ' Operator-Richtung in diesem Projekt bislang nirgends verifiziert
        ' ist (nur LinearExpr-minus-Integer wird bereits genutzt, siehe
        ' Deputat-Korridor oben) - stattdessen fliesst die tatsaechliche
        ' Wiederverwendung direkt als negativ gewichteter "Bonus"-Term in
        ' die Zielfunktion ein.
        Dim kontinuitaetErhalten As New List(Of BoolVar)
        If vorjahresZuordnung IsNot Nothing Then
            For Each eintrag In vorjahresZuordnung
                Dim key As New AssignKey With {.Lehrer = eintrag.Value, .Klasse = eintrag.Key.Klasse, .Fach = eintrag.Key.Fach}
                If assign.ContainsKey(key) Then
                    kontinuitaetErhalten.Add(assign(key))
                End If
            Next
        End If

        ' --- Weich: maximale Anzahl Klassen/Faecher pro Lehrer
        ' (Zersplitterung vermeiden, Hinge-Loss wie der Deputat-Korridor -
        ' nur fuer Lehrkraefte mit gesetztem MaxKlassen/MaxFaecher werden
        ' ueberhaupt Variablen gebaut) ---
        Dim maxKlassenUeberschuss As New List(Of IntVar)
        Dim maxFaecherUeberschuss As New List(Of IntVar)
        For Each lehrer In bestand.Lehrkraefte
            If Not lehrer.MaxKlassen.HasValue AndAlso Not lehrer.MaxFaecher.HasValue Then Continue For
            Dim eigeneVars = assign.Where(Function(kvp) kvp.Key.Lehrer = lehrer.Name).ToList()
            If eigeneVars.Count = 0 Then Continue For

            If lehrer.MaxKlassen.HasValue Then
                Dim aktivProKlasse As New List(Of BoolVar)
                For Each klasseName In eigeneVars.Select(Function(kvp) kvp.Key.Klasse).Distinct()
                    Dim varsInKlasse = eigeneVars.Where(Function(kvp) kvp.Key.Klasse = klasseName).Select(Function(kvp) kvp.Value).ToList()
                    Dim aktiv = model.NewBoolVar($"unterrichtetKlasseAlle[{lehrer.Name},{klasseName}]")
                    model.Add(LinearExpr.Sum(varsInKlasse) >= 1).OnlyEnforceIf(aktiv)
                    model.Add(LinearExpr.Sum(varsInKlasse) = 0).OnlyEnforceIf(aktiv.Not())
                    aktivProKlasse.Add(aktiv)
                Next
                Dim ueberschreitung = model.NewIntVar(0, aktivProKlasse.Count, $"maxKlassenUeberschuss[{lehrer.Name}]")
                model.Add(ueberschreitung >= LinearExpr.Sum(aktivProKlasse) - lehrer.MaxKlassen.Value)
                maxKlassenUeberschuss.Add(ueberschreitung)
            End If

            If lehrer.MaxFaecher.HasValue Then
                Dim aktivProFach As New List(Of BoolVar)
                For Each fachName In eigeneVars.Select(Function(kvp) kvp.Key.Fach).Distinct()
                    Dim varsInFach = eigeneVars.Where(Function(kvp) kvp.Key.Fach = fachName).Select(Function(kvp) kvp.Value).ToList()
                    Dim aktiv = model.NewBoolVar($"unterrichtetFach[{lehrer.Name},{fachName}]")
                    model.Add(LinearExpr.Sum(varsInFach) >= 1).OnlyEnforceIf(aktiv)
                    model.Add(LinearExpr.Sum(varsInFach) = 0).OnlyEnforceIf(aktiv.Not())
                    aktivProFach.Add(aktiv)
                Next
                Dim ueberschreitung = model.NewIntVar(0, aktivProFach.Count, $"maxFaecherUeberschuss[{lehrer.Name}]")
                model.Add(ueberschreitung >= LinearExpr.Sum(aktivProFach) - lehrer.MaxFaecher.Value)
                maxFaecherUeberschuss.Add(ueberschreitung)
            End If
        Next

        ' --- Weich: Klassenstufen-Praeferenzen (eine leere
        ' BevorzugteKlassenstufen-Liste bedeutet "keine Praeferenz", nie
        ' eine Verletzung) ---
        Dim praeferenzVerletzt As New List(Of BoolVar)
        For Each kvp In assign
            Dim lehrer = lehrerByName(kvp.Key.Lehrer)
            If lehrer.BevorzugteKlassenstufen.Count = 0 Then Continue For
            If Not lehrer.BevorzugteKlassenstufen.Contains(KlassenstufeFuer(kvp.Key)) Then
                praeferenzVerletzt.Add(kvp.Value)
            End If
        Next

        ' --- Weich: faire Verteilung unbeliebter Faecher (niedrigste
        ' Prioritaet) - Bereich (Max-Min) der Anzahl unbeliebter-Fach-
        ' Zuweisungen ueber alle dafuer qualifizierten Lehrkraefte, kein
        ' Sentinel noetig (0 ist hier ein legitimer, nicht zu
        ' korrigierender Wert - anders als bei der Tandem-Balance oben, wo
        ' ein inaktiver Kandidat das Minimum verfaelschen wuerde) ---
        Dim unbeliebtRanges As New List(Of IntVar)
        Dim unbeliebteFaecherNamen As New HashSet(Of String)(bestand.Faecher.Where(Function(f) f.Unbeliebt).Select(Function(f) f.Name))
        If unbeliebteFaecherNamen.Count > 0 Then
            Dim lehrerMitUnbeliebtemFach = bestand.Lehrkraefte.Where(
                Function(l) bestand.FachLehrerZuordnungen.Any(Function(z) z.LehrerName = l.Name AndAlso unbeliebteFaecherNamen.Contains(z.FachName))).ToList()
            If lehrerMitUnbeliebtemFach.Count > 1 Then
                Dim anzahlExprs As New List(Of LinearExpr)
                For Each lehrer In lehrerMitUnbeliebtemFach
                    Dim vars = assign.Where(Function(kvp) kvp.Key.Lehrer = lehrer.Name AndAlso unbeliebteFaecherNamen.Contains(kvp.Key.Fach)).
                        Select(Function(kvp) CType(kvp.Value, LinearExpr)).ToList()
                    anzahlExprs.Add(LinearExpr.Sum(vars))
                Next
                Dim unbeliebtMax = model.NewIntVar(0, bigStunden, "unbeliebtMax")
                model.AddMaxEquality(unbeliebtMax, anzahlExprs)
                Dim unbeliebtMin = model.NewIntVar(0, bigStunden, "unbeliebtMin")
                model.AddMinEquality(unbeliebtMin, anzahlExprs)
                Dim unbeliebtRange = model.NewIntVar(0, bigStunden, "unbeliebtRange")
                model.Add(unbeliebtRange >= unbeliebtMax - unbeliebtMin)
                unbeliebtRanges.Add(unbeliebtRange)
            End If
        End If

        model.Minimize(
            LinearExpr.Sum(deputatUeberschuss) * CLng(WeightDeputatAbweichung) +
            LinearExpr.Sum(fehltKlassenlehrer) * CLng(WeightKlassenlehrerFehlt) +
            LinearExpr.Sum(buendelungVerletzt) * CLng(WeightBuendelungVerletzt) +
            LinearExpr.Sum(kontinuitaetErhalten) * (-CLng(WeightKontinuitaetVerletzt)) +
            LinearExpr.Sum(fachfremdEinsatz) * CLng(WeightFachfremdEinsatz) +
            LinearExpr.Sum(maxKlassenUeberschuss) * CLng(WeightMaxKlassenVerletzt) +
            LinearExpr.Sum(maxFaecherUeberschuss) * CLng(WeightMaxFaecherVerletzt) +
            LinearExpr.Sum(tandemRanges) * CLng(WeightTandemBalance) +
            LinearExpr.Sum(praeferenzVerletzt) * CLng(WeightPraeferenzVerletzt) +
            LinearExpr.Sum(unbeliebtRanges) * CLng(WeightUnbeliebteFaecherUngleichheit))

        Dim solver As New CpSolver()
        solver.StringParameters = $"max_time_in_seconds:{timeLimitS.ToString(Globalization.CultureInfo.InvariantCulture)},random_seed:{seed},num_search_workers:{numWorkers}"
        Dim status = solver.Solve(model)

        Dim result As New LehrereinsatzResult With {.Status = status, .Solver = solver}
        If status = CpSolverStatus.Optimal OrElse status = CpSolverStatus.Feasible Then
            Dim zuweisungen As New List(Of LehrereinsatzZuweisung)
            For Each kvp In assign
                If Not solver.BooleanValue(kvp.Value) Then Continue For
                If kvp.Key.IstGruppe Then
                    ' Phase 2.20d: eine Gruppen-Zuweisung wird SOFORT hier
                    ' auf alle echten, von der Gruppe umspannten Klassen
                    ' expandiert (Stammdaten.KlassenOfGruppe) - derselbe
                    ' Lehrer, dasselbe Fach, einmal pro Klasse. Das haelt
                    ' LehrereinsatzResult.Zuweisungen fuer jeden
                    ' nachgelagerten Konsumenten (Verifier.
                    ' VerifyLehrereinsatz's bestehende "genau 1 Zuweisung
                    ' pro (Klasse,Fach)"-Pruefung, spaeter
                    ' BuildAssignmentConstraints) uniform: nur echte
                    ' Klassennamen, keine Sonderbehandlung fuer Gruppen
                    ' noetig.
                    Dim gruppe = gruppeByName(kvp.Key.Klasse)
                    For Each klasseName In Stammdaten.KlassenOfGruppe(bestand, gruppe)
                        zuweisungen.Add(New LehrereinsatzZuweisung With {.Lehrer = kvp.Key.Lehrer, .Klasse = klasseName, .Fach = kvp.Key.Fach})
                    Next
                Else
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

        ' Phase 2.20e: pro Parallelverbund genau eine "parallel_group"-
        ' Constraint - ein Mitglied pro (echter Klasse, Fach, Lehrer)-
        ' Kombination, ueber alle Gruppen des Verbunds und alle von ihnen
        ' umspannten echten Klassen hinweg (Stammdaten.KlassenOfGruppe).
        ' Der Lehrer je Mitglied wird aus den bereits (in SolveLehrereinsatz)
        ' klassen-expandierten result.Zuweisungen nachgeschlagen - dort
        ' steht fuer jede von der Gruppe umspannte Klasse bereits derselbe
        ' Lehrer (siehe VerifyLehrereinsatz's Gruppen-Konsistenzpruefung,
        ' Phase 2.20c), es reicht daher der erste Treffer je (Klasse,Fach).
        For Each verbund In bestand.Gruppen.Where(Function(g) g.FachName IsNot Nothing AndAlso g.Parallelverbund IsNot Nothing).
            GroupBy(Function(g) g.Parallelverbund)
            Dim classes As New List(Of String)
            Dim subjects As New List(Of String)
            Dim teachers As New List(Of String)
            For Each gruppe In verbund
                For Each klasseName In Stammdaten.KlassenOfGruppe(bestand, gruppe)
                    Dim lehrerName = result.Zuweisungen.
                        Where(Function(z) z.Klasse = klasseName AndAlso z.Fach = gruppe.FachName).
                        Select(Function(z) z.Lehrer).FirstOrDefault()
                    If lehrerName Is Nothing Then Continue For
                    classes.Add(klasseName)
                    subjects.Add(gruppe.FachName)
                    teachers.Add(lehrerName)
                Next
            Next
            If classes.Count > 0 Then
                constraints.Add(New JsonObject From {
                    {"type", "parallel_group"},
                    {"classes", New JsonArray(classes.Select(Function(c) CType(c, JsonNode)).ToArray())},
                    {"subjects", New JsonArray(subjects.Select(Function(s) CType(s, JsonNode)).ToArray())},
                    {"teachers", New JsonArray(teachers.Select(Function(t) CType(t, JsonNode)).ToArray())}
                })
            End If
        Next

        Return constraints
    End Function

End Module
