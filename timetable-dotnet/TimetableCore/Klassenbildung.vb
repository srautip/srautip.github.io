' Klassenbildung (Stufe 0, siehe docs/klassenbildung-plan.md, Schritte
' K1/K2): regelbasierte Zuordnung Schueler -> Klasse als CP-SAT-Modell
' nach docs/klassenbildung-konzept.md. Eigenstaendiges Modul neben
' Lehrereinsatzplanung.vb/Kursblockung.vb - beruehrt Solver.vb mit
' keiner Zeile und arbeitet ausschliesslich auf Pseudonym-IDs
' (Datenminimierung, Konzept Abschnitt 2).
'
' Modellierungs-Disziplin: bewusst NUR die in diesem Repo bereits live
' verifizierten CP-SAT-Primitiven (Sum-=-Gleichungen, lineare
' Ungleichungen, OnlyEnforceIf, AddBoolOr) - insbesondere:
' - "genau eine Klasse" als Sum(x) = 1 (statt AddExactlyOne),
' - Implikation x -> used als used >= x (linear aequivalent),
' - |diff| ueber ZWEI einseitige Schranken (dev >= diff, dev >= -diff
'   bzw. excess >= diff - tol UND excess >= -diff - tol) statt
'   AddAbsEquality - unter Minimierung exakt, und im harten Modus
'   direkt als Korridor -tol <= diff <= tol formuliert.
Imports Google.OrTools.Sat

''' <summary>Ein Kind - nur Pseudonym-ID plus frei definierbare
''' Attribut-Tags fuer Balance-Kriterien (Konzept Abschnitt 2: die
''' Diagnose selbst muss nie ins System, Gruppenmitgliedschaft genuegt).</summary>
Public NotInheritable Class KlassenbildungSchueler
    Public Property Id As String
    Public Property Attribute As New Dictionary(Of String, String)
End Class

''' <summary>Klassenrahmen: Anzahl zu bildender Klassen und
''' Groessen-Korridor je Klasse (harte Grundconstraints).</summary>
Public NotInheritable Class KlassenbildungKlassen
    Public Property Anzahl As Integer
    Public Property MinGroesse As Integer
    Public Property MaxGroesse As Integer
End Class

''' <summary>Buendelungs- oder Verteilungsgruppe (Konzept 3.2/3.3).
''' `Typ` = "buendelung" (Mitglieder in moeglichst wenige/genau eine
''' Klasse) oder "verteilung" (max. MaxProKlasse Mitglieder je Klasse).
''' Ein Kind darf in mehreren Gruppen sein - Konflikte loest die
''' Priorisierung.</summary>
Public NotInheritable Class KlassenbildungGruppe
    Public Property Id As String
    Public Property Typ As String
    Public Property Mitglieder As New List(Of String)
    Public Property MaxProKlasse As Integer?
    Public Property Modus As String = "soft"
    Public Property Prio As Integer = 2
End Class

''' <summary>Balance-Kriterium (Konzept 3.4): Kinder mit
''' Attribute(Attribut) = Wert sollen je Klasse nahe am Idealwert
''' |A|/|C| liegen; Abweichung innerhalb `Toleranz` kostet nichts.</summary>
Public NotInheritable Class KlassenbildungBalance
    Public Property Attribut As String
    Public Property Wert As String
    Public Property Toleranz As Integer = 0
    Public Property Modus As String = "soft"
    Public Property Prio As Integer = 2
End Class

''' <summary>Individualwunsch (Konzept 3.5): Paar-Beziehung
''' "zusammen" oder "getrennt" ueber genau zwei Kinder.</summary>
Public NotInheritable Class KlassenbildungWunsch
    Public Property Typ As String
    Public Property Kinder As New List(Of String)
    Public Property Modus As String = "soft"
    Public Property Prio As Integer = 1
End Class

''' <summary>Manuelle Vorgabe der Schulleitung: F1 (`Klasse` gesetzt =
''' Kind MUSS in diese Klasse) oder F2 (`NichtKlasse` gesetzt = Kind
''' darf NICHT in diese Klasse). Klassen sind 1-basiert wie in der
''' YAML-Sicht des Anwenders.</summary>
Public NotInheritable Class KlassenbildungFixierung
    Public Property Kind As String
    Public Property Klasse As Integer?
    Public Property NichtKlasse As Integer?
End Class

''' <summary>Vollstaendiger Input eines Klassenbildungs-Laufs -
''' deserialisierbar 1:1 aus input/klassenbildung.yaml
''' (UnderscoredNamingConvention, siehe YamlKlassenbildung im
''' SchoolTestRunner).</summary>
Public NotInheritable Class KlassenbildungInput
    Public Property Schueler As New List(Of KlassenbildungSchueler)
    Public Property Klassen As New KlassenbildungKlassen
    Public Property Gruppen As New List(Of KlassenbildungGruppe)
    Public Property Balance As New List(Of KlassenbildungBalance)
    Public Property Wuensche As New List(Of KlassenbildungWunsch)
    Public Property Fixierungen As New List(Of KlassenbildungFixierung)
End Class

''' <summary>Verletzungsreport-Zeile: eine weiche Regel mit ihrem
''' gemessenen Verletzungsmass (0 = erfuellt) - Grundlage fuer
''' Scorecard/Ampel (Konzept 5 "Erklaerbarkeit"). Harte Regeln koennen
''' in einer gueltigen Loesung nie verletzt sein und erscheinen nicht.</summary>
Public NotInheritable Class KlassenbildungVerletzung
    Public Property RegelId As String
    Public Property RegelTyp As String
    Public Property Prio As Integer
    Public Property Mass As Long
End Class

''' <summary>Ergebnis eines Solve-Laufs: Zuordnung Kind-Id -> Klasse
''' (1-basiert), Status/Objective und der Verletzungsreport aller
''' weichen Regeln.</summary>
Public NotInheritable Class KlassenbildungResult
    Public Property Status As CpSolverStatus
    Public Property Objective As Double
    Public Property Zuordnung As Dictionary(Of String, Integer)
    Public Property Verletzungen As List(Of KlassenbildungVerletzung)
End Class

Public Module Klassenbildung

    ''' <summary>Default-Gewichte der drei Prioritaetsstufen
    ''' (Konzept Variante A). Bewusst moderater gespreizt als der
    ''' 10000/100/1-Vorschlag des Konzepts: grosse Integer-Koeffizienten
    ''' schwaechen CP-SATs Bound-Beweis (P6-Befund dieses Repos), und
    ''' die Faustregel "Faktor > maximale Verletzungszahl der
    ''' darunterliegenden Stufe" ist bei realen Groessen (wenige hundert
    ''' Regeln) mit 1000/50/1 sicher erfuellt.</summary>
    Public ReadOnly DefaultPrioGewichte As New Dictionary(Of Integer, Long) From {
        {3, 1000L}, {2, 50L}, {1, 1L}
    }

    Private ReadOnly GueltigeModi As New HashSet(Of String) From {"hard", "soft"}

    ''' <summary>Fail-Fast-Validierung VOR dem Solve (gleiche Philosophie
    ''' wie Validation.ValidateEntities: eine Regel, die auf unbekannte
    ''' IDs zeigt oder strukturell wirkungslos waere, ist ein harter
    ''' Fehler - nie ein stiller No-Op). Leere Liste = Input konsistent.</summary>
    Public Function ValidateKlassenbildung(input As KlassenbildungInput) As List(Of String)
        Dim errors As New List(Of String)

        Dim k = input.Klassen
        If k Is Nothing OrElse k.Anzahl < 1 Then
            errors.Add("klassen.anzahl muss >= 1 sein")
            Return errors ' ohne Klassenrahmen ist nichts weiter pruefbar
        End If
        If k.MinGroesse < 0 Then errors.Add($"klassen.min_groesse={k.MinGroesse} darf nicht negativ sein")
        If k.MaxGroesse < k.MinGroesse Then errors.Add($"klassen.max_groesse={k.MaxGroesse} < min_groesse={k.MinGroesse}")

        Dim ids As New HashSet(Of String)
        For i = 0 To input.Schueler.Count - 1
            Dim s = input.Schueler(i)
            If String.IsNullOrWhiteSpace(s.Id) Then
                errors.Add($"schueler[{i}]: id fehlt")
            ElseIf Not ids.Add(s.Id) Then
                errors.Add($"schueler[{i}]: id='{s.Id}' ist doppelt")
            End If
        Next

        Dim n = input.Schueler.Count
        If CLng(k.Anzahl) * k.MaxGroesse < n Then
            errors.Add($"Kapazitaet zu klein: {k.Anzahl} Klassen x max_groesse {k.MaxGroesse} < {n} Schueler")
        End If
        If CLng(k.Anzahl) * k.MinGroesse > n Then
            errors.Add($"Mindestbelegung unerfuellbar: {k.Anzahl} Klassen x min_groesse {k.MinGroesse} > {n} Schueler")
        End If

        Dim pruefeModusPrio = Sub(kontext As String, modus As String, prio As Integer)
                                  If Not GueltigeModi.Contains(modus) Then errors.Add($"{kontext}: modus='{modus}' ungueltig (erlaubt: hard/soft)")
                                  If prio < 1 OrElse prio > 3 Then errors.Add($"{kontext}: prio={prio} ungueltig (erlaubt: 1..3)")
                              End Sub

        Dim gruppenIds As New HashSet(Of String)
        For i = 0 To input.Gruppen.Count - 1
            Dim g = input.Gruppen(i)
            Dim kontext = $"gruppen[{i}] (id='{g.Id}')"
            If String.IsNullOrWhiteSpace(g.Id) OrElse Not gruppenIds.Add(g.Id) Then
                errors.Add($"gruppen[{i}]: id fehlt oder ist doppelt")
            End If
            If g.Typ <> "buendelung" AndAlso g.Typ <> "verteilung" Then
                errors.Add($"{kontext}: typ='{g.Typ}' ungueltig (erlaubt: buendelung/verteilung)")
            End If
            If g.Mitglieder.Count = 0 Then errors.Add($"{kontext}: keine mitglieder - die Regel waere wirkungslos")
            Dim seen As New HashSet(Of String)
            For Each m In g.Mitglieder
                If Not ids.Contains(m) Then errors.Add($"{kontext}: mitglied '{m}' ist keine bekannte schueler-id")
                If Not seen.Add(m) Then errors.Add($"{kontext}: mitglied '{m}' ist doppelt")
            Next
            If g.Typ = "verteilung" Then
                If Not g.MaxProKlasse.HasValue Then
                    errors.Add($"{kontext}: verteilung braucht max_pro_klasse")
                ElseIf g.MaxProKlasse.Value < 1 Then
                    errors.Add($"{kontext}: max_pro_klasse={g.MaxProKlasse.Value} muss >= 1 sein")
                End If
            ElseIf g.Typ = "buendelung" AndAlso g.MaxProKlasse.HasValue Then
                errors.Add($"{kontext}: max_pro_klasse gehoert nicht zu einer buendelung")
            End If
            pruefeModusPrio(kontext, g.Modus, g.Prio)
        Next

        For i = 0 To input.Balance.Count - 1
            Dim b = input.Balance(i)
            Dim kontext = $"balance[{i}] (attribut='{b.Attribut}', wert='{b.Wert}')"
            If String.IsNullOrWhiteSpace(b.Attribut) OrElse b.Wert Is Nothing Then
                errors.Add($"balance[{i}]: attribut und wert muessen gesetzt sein")
            ElseIf Not input.Schueler.Any(Function(s) AttributMatcht(s, b)) Then
                ' R2-Falle: eine Balance-Regel ohne einen einzigen Treffer
                ' waere ein stiller No-Op.
                errors.Add($"{kontext}: kein einziger Schueler traegt dieses Attribut")
            End If
            If b.Toleranz < 0 Then errors.Add($"{kontext}: toleranz={b.Toleranz} darf nicht negativ sein")
            pruefeModusPrio(kontext, b.Modus, b.Prio)
        Next

        For i = 0 To input.Wuensche.Count - 1
            Dim w = input.Wuensche(i)
            Dim kontext = $"wuensche[{i}]"
            If w.Typ <> "zusammen" AndAlso w.Typ <> "getrennt" Then
                errors.Add($"{kontext}: typ='{w.Typ}' ungueltig (erlaubt: zusammen/getrennt)")
            End If
            If w.Kinder.Count <> 2 Then
                errors.Add($"{kontext}: genau 2 kinder erwartet, {w.Kinder.Count} angegeben")
            Else
                For Each kind In w.Kinder
                    If Not ids.Contains(kind) Then errors.Add($"{kontext}: kind '{kind}' ist keine bekannte schueler-id")
                Next
                If w.Kinder(0) = w.Kinder(1) Then errors.Add($"{kontext}: beide kinder sind identisch ('{w.Kinder(0)}')")
            End If
            pruefeModusPrio(kontext, w.Modus, w.Prio)
        Next

        Dim fixiertAuf As New Dictionary(Of String, Integer)
        For i = 0 To input.Fixierungen.Count - 1
            Dim f = input.Fixierungen(i)
            Dim kontext = $"fixierungen[{i}] (kind='{f.Kind}')"
            If Not ids.Contains(If(f.Kind, "")) Then errors.Add($"{kontext}: kind ist keine bekannte schueler-id")
            If f.Klasse.HasValue = f.NichtKlasse.HasValue Then
                errors.Add($"{kontext}: genau EINES von klasse/nicht_klasse muss gesetzt sein")
                Continue For
            End If
            Dim ziel = If(f.Klasse.HasValue, f.Klasse.Value, f.NichtKlasse.Value)
            If ziel < 1 OrElse ziel > k.Anzahl Then
                errors.Add($"{kontext}: klasse {ziel} liegt ausserhalb von 1..{k.Anzahl}")
            End If
            If f.Klasse.HasValue AndAlso f.Kind IsNot Nothing Then
                Dim vorhanden As Integer
                If fixiertAuf.TryGetValue(f.Kind, vorhanden) AndAlso vorhanden <> f.Klasse.Value Then
                    errors.Add($"{kontext}: bereits auf klasse {vorhanden} fixiert - widerspruechliche Doppel-Fixierung")
                Else
                    fixiertAuf(f.Kind) = f.Klasse.Value
                End If
            End If
            If f.NichtKlasse.HasValue AndAlso f.Kind IsNot Nothing Then
                Dim vorhanden As Integer
                If fixiertAuf.TryGetValue(f.Kind, vorhanden) AndAlso vorhanden = f.NichtKlasse.Value Then
                    errors.Add($"{kontext}: nicht_klasse {f.NichtKlasse.Value} widerspricht der Fixierung auf dieselbe Klasse")
                End If
            End If
        Next

        Return errors
    End Function

    Private Function AttributMatcht(s As KlassenbildungSchueler, b As KlassenbildungBalance) As Boolean
        Dim v As String = Nothing
        Return s.Attribute IsNot Nothing AndAlso s.Attribute.TryGetValue(b.Attribut, v) AndAlso v = b.Wert
    End Function

    ''' <summary>Loest die Klassenbildung (Konzept Abschnitt 3, Variante A
    ''' "Gewichtsstufen"). Precondition: ValidateKlassenbildung hat 0
    ''' Fehler bestaetigt. `prioGewichte` (Nothing -> DefaultPrioGewichte)
    ''' bildet die Prio-Stufen 1..3 auf Zielfunktions-Gewichte ab.
    ''' `symmetriebrechung` (Default True): Klassen ohne jede Fixierung
    ''' sind voll austauschbar - die kanonische Ordnung "erste Belegung
    ''' der freien Klassen in aufsteigender Schueler-Reihenfolge"
    ''' (Praezedenz-Kette: ein Kind darf erst in die naechste freie
    ''' Klasse, wenn ein frueheres Kind in der vorigen sitzt) laesst pro
    ''' Symmetrie-Orbit genau einen Repraesentanten zu; per Fixierung
    ''' referenzierte Klassen sind unterscheidbar und bleiben aussen vor
    ''' (dieselbe konservative Logik wie TeacherEquivalenceClasses).</summary>
    Public Function SolveKlassenbildung(input As KlassenbildungInput,
                                         Optional zeitlimitS As Double = 30.0,
                                         Optional seed As Integer = 42,
                                         Optional numWorkers As Integer = 1,
                                         Optional prioGewichte As Dictionary(Of Integer, Long) = Nothing,
                                         Optional symmetriebrechung As Boolean = True) As KlassenbildungResult
        Dim gewichte = If(prioGewichte, DefaultPrioGewichte)
        Dim model As New CpModel()
        Dim schueler = input.Schueler
        Dim anzahl = input.Klassen.Anzahl

        ' --- Entscheidungsvariablen + Grundconstraints (immer hart) ---
        Dim x As New Dictionary(Of (Id As String, Klasse As Integer), BoolVar)
        For Each s In schueler
            For c = 0 To anzahl - 1
                x((s.Id, c)) = model.NewBoolVar($"x[{s.Id},{c}]")
            Next
            model.Add(LinearExpr.Sum(Enumerable.Range(0, anzahl).Select(Function(c) x((s.Id, c)))) = 1L)
        Next
        For c = 0 To anzahl - 1
            Dim cc = c
            Dim groesse As LinearExpr = LinearExpr.Sum(schueler.Select(Function(s) x((s.Id, cc))))
            model.Add(groesse >= CLng(input.Klassen.MinGroesse))
            model.Add(groesse <= CLng(input.Klassen.MaxGroesse))
        Next
        For Each f In input.Fixierungen
            If f.Klasse.HasValue Then model.Add(x((f.Kind, f.Klasse.Value - 1)) = 1L)
            If f.NichtKlasse.HasValue Then model.Add(x((f.Kind, f.NichtKlasse.Value - 1)) = 0L)
        Next

        ' Strafterme: (Regel-Metadaten, Verletzungsmass-Ausdruck, Gewicht)
        Dim strafen As New List(Of (RegelId As String, RegelTyp As String, Prio As Integer, Mass As LinearExpr, Gewicht As Long))

        ' --- Buendelungs-/Verteilungsgruppen (Konzept 3.2/3.3) ---
        For Each g In input.Gruppen
            If g.Typ = "buendelung" Then
                If g.Modus = "hard" Then
                    Dim y = Enumerable.Range(0, anzahl).Select(Function(c) model.NewBoolVar($"y[{g.Id},{c}]")).ToList()
                    model.Add(LinearExpr.Sum(y) = 1L)
                    For Each m In g.Mitglieder
                        For c = 0 To anzahl - 1
                            model.Add(x((m, c)) = y(c))
                        Next
                    Next
                Else
                    ' spread - 1 bestrafen; used >= x je Mitglied genuegt,
                    ' die Minimierung drueckt used selbst auf 0, wo kein
                    ' Mitglied sitzt (Konzept 3.2).
                    Dim usedList As New List(Of BoolVar)
                    For c = 0 To anzahl - 1
                        Dim used = model.NewBoolVar($"used[{g.Id},{c}]")
                        For Each m In g.Mitglieder
                            model.Add(used >= x((m, c)))
                        Next
                        usedList.Add(used)
                    Next
                    strafen.Add((g.Id, "buendelung", g.Prio, LinearExpr.Sum(usedList) - 1L, gewichte(g.Prio)))
                End If
            Else ' verteilung
                Dim kappe = CLng(g.MaxProKlasse.Value)
                Dim overList As New List(Of IntVar)
                For c = 0 To anzahl - 1
                    Dim cc = c
                    Dim belegung As LinearExpr = LinearExpr.Sum(g.Mitglieder.Select(Function(m) x((m, cc))))
                    If g.Modus = "hard" Then
                        model.Add(belegung <= kappe)
                    Else
                        Dim over = model.NewIntVar(0L, CLng(g.Mitglieder.Count), $"over[{g.Id},{c}]")
                        model.Add(belegung - kappe <= over)
                        overList.Add(over)
                    End If
                Next
                If overList.Count > 0 Then
                    strafen.Add((g.Id, "verteilung", g.Prio, LinearExpr.Sum(overList), gewichte(g.Prio)))
                End If
            End If
        Next

        ' --- Balance-Kriterien (Konzept 3.4) ---
        For Each b In input.Balance
            Dim treffer = schueler.Where(Function(s) AttributMatcht(s, b)).Select(Function(s) s.Id).ToList()
            Dim target = CLng(Math.Round(treffer.Count / CDbl(anzahl)))
            Dim tol = CLng(b.Toleranz)
            Dim excessList As New List(Of IntVar)
            For c = 0 To anzahl - 1
                Dim cc = c
                Dim cnt As LinearExpr = LinearExpr.Sum(treffer.Select(Function(id) x((id, cc))))
                If b.Modus = "hard" Then
                    ' |cnt - target| <= tol direkt als Korridor.
                    model.Add(cnt >= target - tol)
                    model.Add(cnt <= target + tol)
                Else
                    ' excess >= |cnt - target| - tol ueber zwei einseitige
                    ' Schranken; die Minimierung haelt excess exakt.
                    Dim excess = model.NewIntVar(0L, CLng(Math.Max(treffer.Count, 1)), $"excess[{b.Attribut}={b.Wert},{c}]")
                    model.Add(excess >= cnt - target - tol)
                    model.Add(excess >= target - cnt - tol)
                    excessList.Add(excess)
                End If
            Next
            If excessList.Count > 0 Then
                strafen.Add(($"{b.Attribut}={b.Wert}", "balance", b.Prio, LinearExpr.Sum(excessList), gewichte(b.Prio)))
            End If
        Next

        ' --- Individualwuensche (Konzept 3.5) ---
        For i = 0 To input.Wuensche.Count - 1
            Dim w = input.Wuensche(i)
            Dim s1 = w.Kinder(0)
            Dim s2 = w.Kinder(1)
            Dim beide As New List(Of BoolVar)
            For c = 0 To anzahl - 1
                Dim bc = model.NewBoolVar($"b[{s1},{s2},{c}]")
                model.Add(bc <= x((s1, c)))
                model.Add(bc <= x((s2, c)))
                model.Add(bc >= x((s1, c)) + x((s2, c)) - 1L)
                beide.Add(bc)
            Next
            Dim together As LinearExpr = LinearExpr.Sum(beide)
            Dim regelId = $"wunsch[{i}]:{s1}+{s2}"
            If w.Typ = "zusammen" Then
                If w.Modus = "hard" Then
                    model.Add(together = 1L)
                Else
                    strafen.Add((regelId, "wunsch_zusammen", w.Prio, 1L - together, gewichte(w.Prio)))
                End If
            Else ' getrennt
                If w.Modus = "hard" Then
                    model.Add(together = 0L)
                Else
                    strafen.Add((regelId, "wunsch_getrennt", w.Prio, together, gewichte(w.Prio)))
                End If
            End If
        Next

        ' --- Symmetriebrechung (Konzept 5): Praezedenz-Kette ueber die
        ' nicht per Fixierung unterscheidbaren Klassen ---
        If symmetriebrechung Then
            Dim fixierteKlassen As New HashSet(Of Integer)(
                input.Fixierungen.Select(Function(f) If(f.Klasse.HasValue, f.Klasse.Value, f.NichtKlasse.Value) - 1))
            Dim freie = Enumerable.Range(0, anzahl).Where(Function(c) Not fixierteKlassen.Contains(c)).ToList()
            For j = 1 To freie.Count - 1
                Dim cPrev = freie(j - 1)
                Dim cCur = freie(j)
                ' Kind i darf erst in cCur, wenn ein Kind mit kleinerem
                ' Index bereits in cPrev sitzt - erzwingt aufsteigende
                ' Erstbelegung der freien Klassen. Kind 0 mit leerem
                ' Praefix ist damit automatisch von cCur ausgeschlossen.
                Dim prefix As LinearExpr = Nothing
                For i = 0 To schueler.Count - 1
                    If prefix Is Nothing Then
                        model.Add(x((schueler(i).Id, cCur)) = 0L)
                    Else
                        model.Add(x((schueler(i).Id, cCur)) <= prefix)
                    End If
                    Dim beitrag As LinearExpr = x((schueler(i).Id, cPrev))
                    prefix = If(prefix Is Nothing, beitrag, prefix + beitrag)
                Next
            Next
        End If

        ' --- Zielfunktion (Konzept 3.6, Variante A) ---
        If strafen.Count > 0 Then
            model.Minimize(strafen.Select(Function(t) t.Mass * t.Gewicht).Aggregate(Function(a, b) a + b))
        End If

        Dim solver As New CpSolver()
        solver.StringParameters = $"max_time_in_seconds:{zeitlimitS.ToString(Globalization.CultureInfo.InvariantCulture)},random_seed:{seed},num_search_workers:{numWorkers}"
        Dim status = solver.Solve(model)

        Dim result As New KlassenbildungResult With {.Status = status}
        If status = CpSolverStatus.Optimal OrElse status = CpSolverStatus.Feasible Then
            result.Objective = solver.ObjectiveValue
            Dim zuordnung As New Dictionary(Of String, Integer)
            For Each s In schueler
                For c = 0 To anzahl - 1
                    If solver.BooleanValue(x((s.Id, c))) Then
                        zuordnung(s.Id) = c + 1
                        Exit For
                    End If
                Next
            Next
            result.Zuordnung = zuordnung
            ' Verletzungsreport: jede weiche Regel mit ihrem gemessenen
            ' Mass (auch 0 = erfuellt - der Report zeigt beides, Konzept
            ' 5 "Erklaerbarkeit").
            result.Verletzungen = strafen.Select(Function(t) New KlassenbildungVerletzung With {
                .RegelId = t.RegelId, .RegelTyp = t.RegelTyp, .Prio = t.Prio,
                .Mass = solver.Value(t.Mass)
            }).ToList()
        End If
        Return result
    End Function

End Module
