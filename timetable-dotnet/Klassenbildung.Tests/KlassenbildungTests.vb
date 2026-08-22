' Klassenbildung K1/K2 (docs/klassenbildung-plan.md): handnachgerechnete
' Mini-Szenarien je Regeltyp gegen die installierte OrTools-DLL -
' dieselbe Disziplin wie bei jeder neuen CP-SAT-Modellierung in diesem
' Projekt (arc42 Abschnitt 9, Muster LehrereinsatzplanungTests).
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class KlassenbildungTests

    ''' <summary>n Schueler S1..Sn, `anzahl` Klassen mit Korridor
    ''' [minG, maxG] - der Grundstock aller Szenarien.</summary>
    Private Function Basis(n As Integer, anzahl As Integer, minG As Integer, maxG As Integer) As KlassenbildungInput
        Dim input As New KlassenbildungInput With {
            .Klassen = New KlassenbildungKlassen With {.Anzahl = anzahl, .MinGroesse = minG, .MaxGroesse = maxG}
        }
        For i = 1 To n
            input.Schueler.Add(New KlassenbildungSchueler With {.Id = $"S{i}"})
        Next
        Return input
    End Function

    Private Function IstGeloest(result As KlassenbildungResult) As Boolean
        Return result.Status = CpSolverStatus.Optimal OrElse result.Status = CpSolverStatus.Feasible
    End Function

    ''' <summary>Fail-Fast-Validierung: jede der Konsistenzfallen wird als
    ''' harter Fehler gemeldet (nie stiller No-Op) - unbekannte IDs,
    ''' Verteilung ohne Kappe, treffer-lose Balance, Wunsch mit falscher
    ''' Kinderzahl, Fixierung ausserhalb des Klassenbereichs,
    ''' widerspruechliche Doppel-Fixierung und zu kleine Kapazitaet.</summary>
    <TestMethod>
    Public Sub ValidationRejectsInconsistentInput()
        Dim input = Basis(4, 2, 0, 1) ' Kapazitaet 2x1 < 4 Schueler
        input.Gruppen.Add(New KlassenbildungGruppe With {.Id = "G1", .Typ = "verteilung", .Mitglieder = New List(Of String) From {"S1", "S99"}})
        input.Balance.Add(New KlassenbildungBalance With {.Attribut = "geschlecht", .Wert = "w"})
        input.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "zusammen", .Kinder = New List(Of String) From {"S1"}})
        input.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S2", .Klasse = 5})
        input.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S3", .Klasse = 1})
        input.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S3", .Klasse = 2})

        Dim errors = Klassenbildung.ValidateKlassenbildung(input)
        Dim alle = String.Join(vbLf, errors)
        StringAssert.Contains(alle, "Kapazitaet zu klein")
        StringAssert.Contains(alle, "verteilung braucht max_pro_klasse")
        StringAssert.Contains(alle, "'S99' ist keine bekannte schueler-id")
        StringAssert.Contains(alle, "kein einziger Schueler traegt dieses Attribut")
        StringAssert.Contains(alle, "genau 2 kinder erwartet")
        StringAssert.Contains(alle, "klasse 5 liegt ausserhalb von 1..2")
        StringAssert.Contains(alle, "widerspruechliche Doppel-Fixierung")
        Assert.AreEqual(7, errors.Count, alle)
    End Sub

    ''' <summary>Konsistenter Input passiert die Validierung fehlerfrei -
    ''' inklusive Mehrfach-Mitgliedschaft desselben Kindes in Buendelungs-
    ''' UND Verteilungsgruppe (Konzept Abschnitt 2).</summary>
    <TestMethod>
    Public Sub ValidationAcceptsConsistentInput()
        Dim input = Basis(6, 2, 2, 4)
        input.Schueler(0).Attribute("geschlecht") = "w"
        input.Gruppen.Add(New KlassenbildungGruppe With {.Id = "G_b", .Typ = "buendelung", .Mitglieder = New List(Of String) From {"S1", "S2"}})
        input.Gruppen.Add(New KlassenbildungGruppe With {.Id = "G_v", .Typ = "verteilung", .MaxProKlasse = 1, .Mitglieder = New List(Of String) From {"S1", "S3"}})
        input.Balance.Add(New KlassenbildungBalance With {.Attribut = "geschlecht", .Wert = "w", .Toleranz = 1})
        input.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "getrennt", .Kinder = New List(Of String) From {"S4", "S5"}})
        input.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S6", .NichtKlasse = 1})
        Assert.AreEqual(0, Klassenbildung.ValidateKlassenbildung(input).Count,
            String.Join(vbLf, Klassenbildung.ValidateKlassenbildung(input)))
    End Sub

    ''' <summary>Grundconstraints + F1/F2: 4 Kinder auf 2 Klassen a exakt
    ''' 2; S1 auf Klasse 1 fixiert, S2 aus Klasse 1 ausgeschlossen -&gt;
    ''' S2 muss in Klasse 2 landen, beide Klassen exakt 2 Kinder.</summary>
    <TestMethod>
    Public Sub GrundconstraintsAndFixierungenHold()
        Dim input = Basis(4, 2, 2, 2)
        input.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S1", .Klasse = 1})
        input.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S2", .NichtKlasse = 1})
        Dim result = Klassenbildung.SolveKlassenbildung(input, zeitlimitS:=10)
        Assert.IsTrue(IstGeloest(result), result.Status.ToString())
        Assert.AreEqual(1, result.Zuordnung("S1"))
        Assert.AreEqual(2, result.Zuordnung("S2"))
        For klasse = 1 To 2
            Assert.AreEqual(2, result.Zuordnung.Values.Where(Function(c) c = klasse).Count())
        Next
    End Sub

    ''' <summary>Buendelung hart: alle 3 Mitglieder zwingend in derselben
    ''' Klasse. Buendelung weich mit unvermeidbarem Split: 3 Mitglieder,
    ''' 2 Klassen a max 2 -&gt; bester spread = 2, Strafe = Gewicht(Prio 2)
    ''' * (2-1) = 50 - von Hand nachgerechnet, inklusive
    ''' Verletzungsreport.</summary>
    <TestMethod>
    Public Sub BuendelungHardForcesAndSoftCountsSpread()
        Dim hart = Basis(4, 2, 2, 2)
        hart.Gruppen.Add(New KlassenbildungGruppe With {.Id = "G", .Typ = "buendelung", .Modus = "hard", .Mitglieder = New List(Of String) From {"S1", "S2"}})
        Dim hartResult = Klassenbildung.SolveKlassenbildung(hart, zeitlimitS:=10)
        Assert.IsTrue(IstGeloest(hartResult))
        Assert.AreEqual(hartResult.Zuordnung("S1"), hartResult.Zuordnung("S2"))

        Dim weich = Basis(4, 2, 2, 2)
        weich.Gruppen.Add(New KlassenbildungGruppe With {.Id = "G", .Typ = "buendelung", .Modus = "soft", .Prio = 2, .Mitglieder = New List(Of String) From {"S1", "S2", "S3"}})
        Dim weichResult = Klassenbildung.SolveKlassenbildung(weich, zeitlimitS:=10)
        Assert.AreEqual(CpSolverStatus.Optimal, weichResult.Status)
        Assert.AreEqual(50.0, weichResult.Objective, 0.001,
            "3 Mitglieder passen nicht in eine 2er-Klasse - spread 2, Strafe (2-1)*50.")
        Dim v = weichResult.Verletzungen.Single(Function(x) x.RegelId = "G")
        Assert.AreEqual(1L, v.Mass)
        Assert.AreEqual("buendelung", v.RegelTyp)
    End Sub

    ''' <summary>Verteilung hart kappt exakt (4 Mitglieder, 2 Klassen,
    ''' Kappe 2 -&gt; je genau 2); Verteilung weich mit unvermeidbarem
    ''' Ueberlauf: 3 Mitglieder, 2 Klassen, Kappe 1 -&gt; bester
    ''' Gesamt-Ueberlauf 1, Strafe = 50.</summary>
    <TestMethod>
    Public Sub VerteilungHardCapsAndSoftCountsOverflow()
        Dim hart = Basis(4, 2, 2, 2)
        hart.Gruppen.Add(New KlassenbildungGruppe With {.Id = "D", .Typ = "verteilung", .Modus = "hard", .MaxProKlasse = 2, .Mitglieder = New List(Of String) From {"S1", "S2", "S3", "S4"}})
        Dim hartResult = Klassenbildung.SolveKlassenbildung(hart, zeitlimitS:=10)
        Assert.IsTrue(IstGeloest(hartResult))
        For klasse = 1 To 2
            Assert.AreEqual(2, {"S1", "S2", "S3", "S4"}.Count(Function(s) hartResult.Zuordnung(s) = klasse))
        Next

        Dim weich = Basis(4, 2, 2, 2)
        weich.Gruppen.Add(New KlassenbildungGruppe With {.Id = "D", .Typ = "verteilung", .Modus = "soft", .Prio = 2, .MaxProKlasse = 1, .Mitglieder = New List(Of String) From {"S1", "S2", "S3"}})
        Dim weichResult = Klassenbildung.SolveKlassenbildung(weich, zeitlimitS:=10)
        Assert.AreEqual(CpSolverStatus.Optimal, weichResult.Status)
        Assert.AreEqual(50.0, weichResult.Objective, 0.001,
            "3 Mitglieder auf 2 Klassen mit Kappe 1 - genau 1 Ueberlauf unvermeidbar.")
        Assert.AreEqual(1L, weichResult.Verletzungen.Single(Function(x) x.RegelId = "D").Mass)
    End Sub

    ''' <summary>Balance: 4 w + 4 m auf 2 Klassen a 4, Toleranz 0 -&gt;
    ''' exakt 2 w je Klasse (Optimal mit Objective 0); die harte Variante
    ''' erzwingt dasselbe als Korridor.</summary>
    <TestMethod>
    Public Sub BalanceSteersAttributeDistribution()
        For Each modus In {"soft", "hard"}
            Dim input = Basis(8, 2, 4, 4)
            For i = 0 To 7
                input.Schueler(i).Attribute("geschlecht") = If(i < 4, "w", "m")
            Next
            input.Balance.Add(New KlassenbildungBalance With {.Attribut = "geschlecht", .Wert = "w", .Toleranz = 0, .Modus = modus, .Prio = 2})
            Dim result = Klassenbildung.SolveKlassenbildung(input, zeitlimitS:=10)
            Assert.AreEqual(CpSolverStatus.Optimal, result.Status, modus)
            Assert.AreEqual(0.0, result.Objective, 0.001, modus)
            For klasse = 1 To 2
                Dim wInKlasse = Enumerable.Range(1, 4).Count(Function(i) result.Zuordnung($"S{i}") = klasse)
                Assert.AreEqual(2, wInKlasse, $"{modus}: Klasse {klasse} muss genau 2 von 4 w-Kindern haben.")
            Next
        Next
    End Sub

    ''' <summary>Wuensche: "zusammen" wird erfuellt, wenn nichts
    ''' dagegensteht (Objective 0); ein harter "getrennt"-Wunsch trennt
    ''' zwingend.</summary>
    <TestMethod>
    Public Sub WuenscheZusammenUndGetrennt()
        Dim zusammen = Basis(4, 2, 2, 2)
        zusammen.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "zusammen", .Kinder = New List(Of String) From {"S1", "S2"}, .Prio = 1})
        Dim zusammenResult = Klassenbildung.SolveKlassenbildung(zusammen, zeitlimitS:=10)
        Assert.AreEqual(CpSolverStatus.Optimal, zusammenResult.Status)
        Assert.AreEqual(0.0, zusammenResult.Objective, 0.001)
        Assert.AreEqual(zusammenResult.Zuordnung("S1"), zusammenResult.Zuordnung("S2"))

        Dim getrennt = Basis(4, 2, 2, 2)
        getrennt.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "getrennt", .Modus = "hard", .Kinder = New List(Of String) From {"S1", "S2"}})
        Dim getrenntResult = Klassenbildung.SolveKlassenbildung(getrennt, zeitlimitS:=10)
        Assert.IsTrue(IstGeloest(getrenntResult))
        Assert.AreNotEqual(getrenntResult.Zuordnung("S1"), getrenntResult.Zuordnung("S2"))
    End Sub

    ''' <summary>Prio-Dominanz (Konzept Abschnitt 4, Faustregel): EINE
    ''' kritische Verteilungsregel (Prio 3, Kappe 1) schlaegt den
    ''' niedrig priorisierten Zusammen-Wunsch ueber dieselben Kinder -
    ''' der Solver trennt S1/S2 (Verteilungs-Strafe 1000 vermieden) und
    ''' bezahlt dafuer exakt die Wunsch-Strafe 1.</summary>
    <TestMethod>
    Public Sub PrioDominanceSeparatesDespiteWish()
        Dim input = Basis(4, 2, 2, 2)
        input.Gruppen.Add(New KlassenbildungGruppe With {.Id = "D", .Typ = "verteilung", .Modus = "soft", .Prio = 3, .MaxProKlasse = 1, .Mitglieder = New List(Of String) From {"S1", "S2"}})
        input.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "zusammen", .Kinder = New List(Of String) From {"S1", "S2"}, .Prio = 1})
        Dim result = Klassenbildung.SolveKlassenbildung(input, zeitlimitS:=10)
        Assert.AreEqual(CpSolverStatus.Optimal, result.Status)
        Assert.AreNotEqual(result.Zuordnung("S1"), result.Zuordnung("S2"),
            "Die kritische Verteilung (1000) muss den Wunsch (1) dominieren.")
        Assert.AreEqual(1.0, result.Objective, 0.001)
        Assert.AreEqual(0L, result.Verletzungen.Single(Function(x) x.RegelTyp = "verteilung").Mass)
        Assert.AreEqual(1L, result.Verletzungen.Single(Function(x) x.RegelTyp = "wunsch_zusammen").Mass)
    End Sub

    ''' <summary>K3, Verifier-Prinzip: der Bewertungslauf
    ''' (KlassenbildungQuality.Bewerte, reine Auszaehlung ohne Solver)
    ''' reproduziert die Verletzungsmasse des Solvers unabhaengig - fuer
    ''' ein Szenario mit bekannter Buendelungs- und Wunsch-Verletzung.</summary>
    <TestMethod>
    Public Sub BewerteMatchesSolverViolationsIndependently()
        Dim input = Basis(4, 2, 2, 2)
        input.Gruppen.Add(New KlassenbildungGruppe With {.Id = "G", .Typ = "buendelung", .Modus = "soft", .Prio = 2, .Mitglieder = New List(Of String) From {"S1", "S2", "S3"}})
        input.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "zusammen", .Kinder = New List(Of String) From {"S1", "S4"}, .Prio = 1})
        Dim result = Klassenbildung.SolveKlassenbildung(input, zeitlimitS:=10)
        Assert.AreEqual(CpSolverStatus.Optimal, result.Status)

        Dim bewertung = KlassenbildungQuality.Bewerte(input, result.Zuordnung)
        Assert.AreEqual(result.Verletzungen.Count, bewertung.Verletzungen.Count)
        For Each sv In result.Verletzungen
            Assert.AreEqual(sv.Mass, bewertung.Verletzungen.Single(Function(b) b.RegelId = sv.RegelId).Mass,
                $"Unabhaengige Nachzaehlung weicht ab fuer {sv.RegelId}")
        Next
    End Sub

    ''' <summary>K3, Ampel-Chips (Konzept 11.1): exakte Kappen-Auslastung
    ''' ist GELB, Ueberschreitung ROT, Balance am Toleranzrand GELB,
    ''' verletzter Wunsch ROT - gegen eine handgebaute Zuordnung
    ''' klassifiziert (kein Solve noetig).</summary>
    <TestMethod>
    Public Sub ChipsClassifyKnappUndVerletzt()
        Dim input = Basis(6, 2, 3, 3)
        ' Verteilung Kappe 1: S1/S2 in Klasse 1 -> Ueberlauf (rot), S3 allein in 2 -> Kappe voll (gelb).
        input.Gruppen.Add(New KlassenbildungGruppe With {.Id = "D", .Typ = "verteilung", .MaxProKlasse = 1, .Mitglieder = New List(Of String) From {"S1", "S2", "S3"}})
        ' Balance mit Toleranz 1: 2 w-Kinder beide in Klasse 1 -> dev |2-1|=1 = Toleranzrand (gelb).
        input.Schueler(0).Attribute("geschlecht") = "w" ' S1
        input.Schueler(3).Attribute("geschlecht") = "w" ' S4
        input.Balance.Add(New KlassenbildungBalance With {.Attribut = "geschlecht", .Wert = "w", .Toleranz = 1})
        ' Wunsch zusammen S5+S6, aber getrennt zugeordnet -> rot.
        input.Wuensche.Add(New KlassenbildungWunsch With {.Typ = "zusammen", .Kinder = New List(Of String) From {"S5", "S6"}})

        Dim zuordnung As New Dictionary(Of String, Integer) From {
            {"S1", 1}, {"S2", 1}, {"S4", 1}, {"S3", 2}, {"S5", 1}, {"S6", 2}}
        Dim bewertung = KlassenbildungQuality.Bewerte(input, zuordnung)

        Assert.AreEqual("rot", bewertung.Chips.Single(Function(c) c.KindId = "S1" AndAlso c.RegelId = "D").Status)
        Assert.AreEqual("gelb", bewertung.Chips.Single(Function(c) c.KindId = "S3" AndAlso c.RegelId = "D").Status)
        Assert.AreEqual("gelb", bewertung.Chips.Single(Function(c) c.KindId = "S1" AndAlso c.RegelTyp = "balance").Status)
        Assert.AreEqual("rot", bewertung.Chips.Single(Function(c) c.KindId = "S5").Status)
        Assert.AreEqual(1L, bewertung.Verletzungen.Single(Function(v) v.RegelId = "D").Mass)
        ' S2 sitzt mit S1 in der ueberlaufenen Klasse -> ebenfalls rot;
        ' freie Kinder (ohne jede Regel) haben keine Chips.
        Assert.AreEqual("rot", bewertung.Chips.Single(Function(c) c.KindId = "S2" AndAlso c.RegelId = "D").Status)
        Assert.IsFalse(bewertung.Chips.Any(Function(c) c.KindId = "S4" AndAlso c.RegelTyp <> "balance"))
    End Sub

    ''' <summary>K3, Varianten-Schleife (Konzept 8): liefert n paarweise
    ''' um mindestens min_distanz verschiedene Varianten; mit aktiver
    ''' Symmetriebrechung ist keine davon eine blosse
    ''' Klassennummern-Vertauschung (Fallstrick 8.2). Der Konsens-Kern
    ''' enthaelt genau die in allen Varianten identisch zugeordneten
    ''' Kinder - das per Fixierung gepinnte Kind immer.</summary>
    <TestMethod>
    Public Sub SolveKlassenbildungTopProducesDiverseVariantsAndKonsens()
        Dim input = Basis(8, 2, 4, 4)
        input.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S8", .Klasse = 2})
        Dim top = Klassenbildung.SolveKlassenbildungTop(input, zeitlimitS:=10, nVarianten:=3, minDistanz:=2)
        Assert.AreEqual(3, top.Varianten.Count)
        For i = 0 To 2
            For j = i + 1 To 2
                Dim dist = top.Varianten(i).Zuordnung.Keys.
                    Where(Function(id) top.Varianten(i).Zuordnung(id) <> top.Varianten(j).Zuordnung(id)).Count()
                Assert.IsTrue(dist >= 2, $"Variante {i + 1} vs {j + 1}: Distanz {dist} < 2")
            Next
        Next
        CollectionAssert.Contains(top.KonsensKern, "S8", "Das fixierte Kind ist zwangslaeufig in allen Varianten identisch.")
        For Each id In top.KonsensKern
            Dim erste = top.Varianten(0).Zuordnung(id)
            Assert.IsTrue(top.Varianten.All(Function(v) v.Zuordnung(id) = erste))
        Next
    End Sub

    ''' <summary>K3, Qualitaetsschranke: bei epsilon 0 muss jede weitere
    ''' Variante exakt den Optimalwert halten - im Szenario mit
    ''' unvermeidbarem Buendelungs-Split (Optimum 50) sind alle
    ''' gefundenen Varianten gleich gut.</summary>
    <TestMethod>
    Public Sub SolveKlassenbildungTopHoldsQualityBound()
        Dim input = Basis(4, 2, 2, 2)
        input.Gruppen.Add(New KlassenbildungGruppe With {.Id = "G", .Typ = "buendelung", .Modus = "soft", .Prio = 2, .Mitglieder = New List(Of String) From {"S1", "S2", "S3"}})
        Dim top = Klassenbildung.SolveKlassenbildungTop(input, zeitlimitS:=10, nVarianten:=3, epsilon:=0.0, minDistanz:=1)
        Assert.IsTrue(top.Varianten.Count >= 2, "Mindestens eine gleich gute Alternative erwartet.")
        For Each v In top.Varianten
            Assert.AreEqual(50.0, v.Objective, 0.001, "epsilon 0: jede Variante muss das Optimum halten.")
        Next
    End Sub

    ''' <summary>Anzeige-Labels: explizite `labels` gewinnen, `stufe`
    ''' generiert 1a/1b/..., ohne beides bleibt "Klasse N"; Labels mit
    ''' falscher Anzahl oder Duplikaten sind Validierungsfehler.</summary>
    <TestMethod>
    Public Sub KlassenLabelsFolgenStufeUndExplizitenLabels()
        Dim mitStufe = Basis(4, 2, 2, 2)
        mitStufe.Klassen.Stufe = 1
        CollectionAssert.AreEqual(New List(Of String) From {"1a", "1b"}, Klassenbildung.KlassenLabels(mitStufe))

        Dim explizit = Basis(4, 2, 2, 2)
        explizit.Klassen.Stufe = 1
        explizit.Klassen.Labels = New List(Of String) From {"Igel", "Fuchs"}
        CollectionAssert.AreEqual(New List(Of String) From {"Igel", "Fuchs"}, Klassenbildung.KlassenLabels(explizit))

        Dim ohne = Basis(4, 2, 2, 2)
        CollectionAssert.AreEqual(New List(Of String) From {"Klasse 1", "Klasse 2"}, Klassenbildung.KlassenLabels(ohne))

        Dim kaputt = Basis(4, 2, 2, 2)
        kaputt.Klassen.Labels = New List(Of String) From {"1a", "1a", "1b"}
        Dim errors = Klassenbildung.ValidateKlassenbildung(kaputt)
        Dim alle = String.Join(vbLf, errors)
        StringAssert.Contains(alle, "3 Eintraege, aber anzahl=2")
        StringAssert.Contains(alle, "eindeutig")
    End Sub

    ''' <summary>U1: Gruppen-Kuerzel - explizit gesetzt gewinnt
    ''' (uppercased), Ids mit Zahl-Suffix werden zu Initial+Zahl,
    ''' andere zu den ersten drei Buchstaben; Kollisionen bekommen eine
    ''' Laufnummer. Deterministisch ueber die Gruppen-Reihenfolge.</summary>
    <TestMethod>
    Public Sub GruppenKuerzelSindDeterministischUndEindeutig()
        Dim input = Basis(4, 2, 2, 2)
        Dim addGruppe = Sub(id As String, kuerzel As String)
                            input.Gruppen.Add(New KlassenbildungGruppe With {
                                .Id = id, .Typ = "buendelung", .Kuerzel = kuerzel,
                                .Mitglieder = New List(Of String) From {"S1", "S2"}})
                        End Sub
        addGruppe("G_zwillinge", Nothing)
        addGruppe("G_kita_sonnenblume_3", Nothing)
        addGruppe("G_sozialverhalten", Nothing)
        addGruppe("G_schulbegleitung", "sbg")
        addGruppe("G_sozialkompetenz", Nothing) ' Kollision mit SOZ -> SOZ2

        Dim k = Klassenbildung.GruppenKuerzel(input)
        Assert.AreEqual("ZWI", k("G_zwillinge"))
        Assert.AreEqual("K3", k("G_kita_sonnenblume_3"))
        Assert.AreEqual("SOZ", k("G_sozialverhalten"))
        Assert.AreEqual("SBG", k("G_schulbegleitung"))
        Assert.AreEqual("SOZ2", k("G_sozialkompetenz"))
        Assert.AreEqual(k.Values.Count, k.Values.Distinct().Count())
    End Sub

    ''' <summary>Symmetriebrechung: ohne Fixierungen sind die Klassen
    ''' austauschbar - die Praezedenz-Kette erzwingt den kanonischen
    ''' Repraesentanten (S1 sitzt in Klasse 1, und Klasse 2 wird
    ''' fruehestens ab S2 erstbelegt). Mit einer Fixierung auf Klasse 2
    ''' bleibt das Modell loesbar (die fixierte Klasse ist von der Kette
    ''' ausgenommen).</summary>
    <TestMethod>
    Public Sub SymmetryBreakingPicksCanonicalRepresentativeAndRespectsFixierungen()
        Dim input = Basis(4, 2, 2, 2)
        Dim result = Klassenbildung.SolveKlassenbildung(input, zeitlimitS:=10)
        Assert.IsTrue(IstGeloest(result))
        Assert.AreEqual(1, result.Zuordnung("S1"), "Kanonisch: das erste Kind gehoert in die erste freie Klasse.")

        Dim mitFixierung = Basis(4, 2, 2, 2)
        mitFixierung.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S1", .Klasse = 2})
        Dim fixResult = Klassenbildung.SolveKlassenbildung(mitFixierung, zeitlimitS:=10)
        Assert.IsTrue(IstGeloest(fixResult), "Fixierte Klassen duerfen die Symmetriekette nicht unloesbar machen.")
        Assert.AreEqual(2, fixResult.Zuordnung("S1"))
    End Sub

End Class
