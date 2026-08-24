' Projekt-Assistent, Stufe F5 (gui-ui-konzept.md 6.1).
'
' Der Assistent ist die einzige Maske, die einen KOMPLETTEN Bestand aus
' dem Nichts erzeugt. Geprueft wird deshalb nicht, ob Felder ankommen,
' sondern ob das Erzeugte traegt: `ValidateStammdaten` muss gruen sein,
' sonst hat der Nutzer ein Projekt, das er nicht rechnen kann - und
' genau das verspricht der Assistent ("ein sofort rechenbares Projekt").
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class ProjektAssistentTests

    Private Function Modell() As ProjektAssistentViewModel
        Return New ProjektAssistentViewModel(New TestDialoge())
    End Function

    Private Function Grundschule(Optional kinderJeKlasse As Integer = 0,
                                 Optional mitVorlage As Boolean = False) As ProjektAssistentViewModel
        Dim m = Modell()
        m.SchulName = "Testschule"
        m.Schulart = "Grundschule"
        m.KlassenstufenAnzahl = 4
        m.Zuege = 2
        m.LehrerAnzahl = 8
        If kinderJeKlasse > 0 Then
            For Each stufe In m.Klassenstufen()
                m.SchuelerJeKlasse(stufe) = kinderJeKlasse
            Next
        End If
        If mitVorlage Then m.GewaehlteVorlagen.Add(GruppenVorlagen.Alle(0).Name)
        Return m
    End Function

    ' ===============================================================
    ' Schritt 1 und 2
    ' ===============================================================

    ''' <summary>Die Schulart bestimmt die Obergrenze - "1..4 GS / 1..6
    ''' GMS" (6.1). Ein stehengebliebenes "6" nach dem Wechsel auf
    ''' Grundschule waere eine Eingabe, die erst im letzten Schritt
    ''' auffliegt.</summary>
    <TestMethod>
    Public Sub SchulartWechselZiehtVorgabenNach()
        Dim m = Modell()
        m.Schulart = "Gemeinschaftsschule"
        Assert.AreEqual(6, m.MaxKlassenstufen())
        Assert.AreEqual(6, m.KlassenstufenAnzahl)
        Assert.AreEqual(3, m.MindestLehrer(), "die GMS hat drei Klassenlehrer-Pool-Typen")

        m.Schulart = "Grundschule"
        Assert.AreEqual(4, m.MaxKlassenstufen())
        Assert.AreEqual(4, m.KlassenstufenAnzahl)
        Assert.AreEqual(1, m.MindestLehrer())
    End Sub

    <TestMethod>
    Public Sub SchrittEinsVerlangtNamenUndKenntNurBadenWuerttemberg()
        Dim m = Modell()
        Assert.IsTrue(m.Pruefe(1).Any(Function(f) f.Contains("Schulname")))

        m.SchulName = "Testschule"
        Assert.AreEqual(0, m.Pruefe(1).Count)

        m.Bundesland = "BY"
        Assert.IsTrue(m.Pruefe(1).Any(Function(f) f.Contains("Baden-Württemberg")),
                      "ein stiller Rückfall auf BW würde erfundene Lehrplanzahlen erzeugen")
    End Sub

    <TestMethod>
    Public Sub SchrittZweiHaeltDieGrenzenDesTemplatesEin()
        Dim m = Grundschule()
        Assert.AreEqual(0, m.Pruefe(2).Count)

        m.KlassenstufenAnzahl = 5
        Assert.IsTrue(m.Pruefe(2).Any(Function(f) f.Contains("Klassenstufen")))

        m.KlassenstufenAnzahl = 4
        m.LehrerAnzahl = 0
        Assert.IsTrue(m.Pruefe(2).Any(Function(f) f.Contains("Klassenlehrkräfte")))

        m.LehrerAnzahl = 8
        m.Zuege = 0
        Assert.IsTrue(m.Pruefe(2).Any(Function(f) f.Contains("Zug")))
    End Sub

    ''' <summary>Eine Vorlage ohne Kinder waere eine leere Fachgruppe -
    ''' der Solver muesste sie einplanen, obwohl niemand darin sitzt.</summary>
    <TestMethod>
    Public Sub GruppenVorlageOhneSchuelerWirdBlockiert()
        Dim m = Grundschule(mitVorlage:=True)
        Assert.IsTrue(m.Pruefe(3).Any(Function(f) f.Contains("brauchen Schüler")))

        m.SchuelerJeKlasse(1) = 20
        Assert.AreEqual(0, m.Pruefe(3).Count)
    End Sub

    <TestMethod>
    Public Sub SchrittVierVerlangtPasswortUndSpeicherort()
        Dim m = Grundschule()
        m.Passwort = "geheim12"
        m.PasswortWiederholung = "geheim13"
        Assert.IsTrue(m.Pruefe(4).Any(Function(f) f.Contains("überein")))

        m.PasswortWiederholung = "geheim12"
        Assert.IsTrue(m.Pruefe(4).Any(Function(f) f.Contains("Speicherort")))

        m.Pfad = "C:\tmp\x.splanx"
        Assert.AreEqual(0, m.Pruefe(4).Count)

        m.Passwort = "kurz"
        m.PasswortWiederholung = "kurz"
        Assert.IsTrue(m.Pruefe(4).Any(Function(f) f.Contains("zu kurz")))
    End Sub

    <TestMethod>
    Public Sub PasswortStaerkeUnterscheidetVierStufen()
        Dim m = Modell()
        m.Passwort = "abc"
        StringAssert.Contains(m.PasswortStaerke(), "zu kurz")
        m.Passwort = "abcdefgh"
        Assert.AreEqual("schwach", m.PasswortStaerke())
        m.Passwort = "abcdefgh1234"
        Assert.AreEqual("brauchbar", m.PasswortStaerke())
        m.Passwort = "Abcdefgh1234!xyz"
        Assert.AreEqual("stark", m.PasswortStaerke())
    End Sub

    ' ===============================================================
    ' Der erzeugte Bestand
    ' ===============================================================

    ''' <summary>Ohne Schritt 3 rechnet der Plan "rein klassenbasiert"
    ''' (6.1) - also KEINE Schueler, und trotzdem ein gueltiger
    ''' Bestand.</summary>
    <TestMethod>
    Public Sub OhneSchrittDreiEntstehtEinGueltigerKlassenbasierterBestand()
        Dim b = Grundschule().Vorschau()

        Assert.AreEqual(0, b.Schueler.Count)
        Assert.AreEqual(0, b.Gruppen.Count)
        Assert.AreEqual(8, b.Klassen.Count, "4 Stufen x 2 Zuege")
        Assert.IsTrue(b.Lehrkraefte.Count > 8, "die Fachlehrer kommen bedarfsgerecht dazu")
        CollectionAssert.AreEqual(New List(Of String) From {"1a", "1b", "2a", "2b", "3a", "3b", "4a", "4b"},
                                  b.Klassen.Select(Function(k) k.Name).ToList())

        Dim fehler = StammdatenValidation.ValidateStammdaten(b)
        Assert.AreEqual(0, fehler.Count, String.Join(vbLf, fehler))
    End Sub

    <TestMethod>
    Public Sub SchrittDreiErzeugtPlatzhalterOhneKlarnamen()
        Dim b = Grundschule(kinderJeKlasse:=20).Vorschau()

        Assert.AreEqual(160, b.Schueler.Count, "8 Klassen x 20")
        Assert.IsTrue(b.Schueler.All(Function(s) s.Id.StartsWith("S-")),
                      "Platzhalter folgen dem Muster der Beispiel-Fixtures")
        Assert.IsTrue(b.Klassen.All(Function(k) k.Schuelerzahl = 20))
        Dim fehler = StammdatenValidation.ValidateStammdaten(b)
        Assert.AreEqual(0, fehler.Count, String.Join(vbLf, fehler))
    End Sub

    ''' <summary>Der eigentliche Pruefstein von F5. Die Vorlage spaltet ein
    ''' FACH auf, nicht nur die Kinder - sonst haetten die drei Gruppen
    ''' dasselbe Fach, und die Verbund-Regel verlangt paarweise
    ''' verschiedene.</summary>
    <TestMethod>
    Public Sub ReligionsVorlageSpaltetDasFachUndBleibtGueltig()
        Dim b = Grundschule(kinderJeKlasse:=20, mitVorlage:=True).Vorschau()

        Assert.IsFalse(b.Faecher.Any(Function(f) f.Name = "Religion"),
                       "auf allen vier Stufen aufgeteilt - das Quellfach wird nicht mehr gefuehrt")
        For Each name In {"Religion-ev", "Religion-kath", "Ethik"}
            Dim f = b.Faecher.FirstOrDefault(Function(x) x.Name = name)
            Assert.IsNotNull(f, $"{name} fehlt")
            Assert.AreEqual(4, f.Klassenstufen.Count, $"{name} auf allen vier Stufen")
            Assert.IsTrue(b.FachLehrerZuordnungen.Any(Function(z) z.FachName = name),
                          $"{name} haette keine qualifizierte Lehrkraft")
        Next

        Dim verbuende = b.Gruppen.Select(Function(g) g.Parallelverbund).Distinct().ToList()
        Assert.AreEqual(4, verbuende.Count, "ein Verbund je Klassenstufe")
        Assert.AreEqual(12, b.Gruppen.Count, "4 Stufen x 3 Gruppen")

        For Each stufe In {1, 2, 3, 4}
            Dim gruppen = b.Gruppen.Where(Function(g) g.Klassenstufe = stufe).ToList()
            Dim mitglieder = gruppen.SelectMany(Function(g) g.MitgliederSchuelerIds).ToList()
            Assert.AreEqual(40, mitglieder.Count, $"Stufe {stufe}: jedes Kind genau einmal")
            Assert.AreEqual(40, mitglieder.Distinct().Count(), $"Stufe {stufe}: kein Kind doppelt")
            Assert.IsTrue(gruppen.All(Function(g) g.MitgliederSchuelerIds.Count > 0),
                          $"Stufe {stufe}: keine leere Gruppe")
            Assert.AreEqual(3, gruppen.Select(Function(g) g.FachName).Distinct().Count(),
                            $"Stufe {stufe}: die Faecher des Verbunds muessen paarweise verschieden sein")
        Next

        Dim fehler = StammdatenValidation.ValidateStammdaten(b)
        Assert.AreEqual(0, fehler.Count, String.Join(vbLf, fehler))
    End Sub

    ''' <summary>Nach der Aufspaltung tragen GRUPPEN den Bedarf: drei
    ''' parallele Gruppen je Stufe statt zwei Klassen. Wer das nicht
    ''' nachbemisst, hinterlaesst eine Lehrkraft mit mehr Stunden als
    ''' Deputat - und der Solver wird infeasible.</summary>
    <TestMethod>
    Public Sub NachDerAufteilungDecktDasDeputatDenBedarf()
        Dim b = Grundschule(kinderJeKlasse:=20, mitVorlage:=True).Vorschau()

        For Each name In {"Religion-ev", "Religion-kath", "Ethik"}
            Assert.AreEqual(8, Kennzahlen.BedarfJeFach(b, name),
                            $"{name}: 4 Stufen x 1 Gruppe x 2 Wochenstunden")
        Next
        Dim bedarf = {"Religion-ev", "Religion-kath", "Ethik"}.Sum(Function(f) Kennzahlen.BedarfJeFach(b, f))
        Dim deputat = Kennzahlen.DeputatFuerFach(b, "Religion-ev")
        Assert.IsTrue(deputat >= bedarf,
                      $"Bedarf {bedarf} Stunden, Deputat nur {deputat} - es fehlt eine Lehrkraft")
    End Sub

    ''' <summary>Eine Stufe ohne das Quellfach wird uebersprungen, nicht
    ''' erfunden - und der Bericht sagt es.</summary>
    <TestMethod>
    Public Sub VorlageUeberspringtStufenOhneDasQuellfach()
        Dim b = New Stammdatenbestand() With {.PeriodsPerDay = 6}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        b.Faecher.Add(New Fach With {.Name = "Deutsch"})

        Dim bericht = GruppenVorlagen.Anwenden(b, GruppenVorlagen.Alle(0), {1})

        Assert.AreEqual(0, b.Gruppen.Count)
        Assert.IsTrue(bericht.Any(Function(z) z.Contains("gibt es nicht")), String.Join(vbLf, bericht))
    End Sub

    <TestMethod>
    Public Sub AufteilenVerteiltVollstaendigUndOhneLeereGruppe()
        For Each gesamt In {3, 4, 7, 20, 21, 100}
            Dim teile = GruppenVorlagen.Aufteilen(gesamt, New List(Of Integer) From {40, 25, 35})
            Assert.AreEqual(gesamt, teile.Sum(), $"{gesamt}: es muessen alle Kinder untergebracht sein")
            Assert.IsTrue(teile.All(Function(n) n >= 1), $"{gesamt}: keine leere Gruppe")
        Next
        ' Weniger Kinder als Teile: ehrlich leer statt erfunden.
        Dim knapp = GruppenVorlagen.Aufteilen(2, New List(Of Integer) From {40, 25, 35})
        Assert.AreEqual(2, knapp.Sum())
    End Sub

    <TestMethod>
    Public Sub ZusammenfassungNenntZahlenUndDasPruefergebnis()
        Dim m = Grundschule(kinderJeKlasse:=20, mitVorlage:=True)
        m.Vorschau()
        Dim zeilen = m.Zusammenfassung()

        Assert.IsTrue(zeilen.Any(Function(z) z.Contains("8 Klassen")), String.Join(vbLf, zeilen))
        Assert.IsTrue(zeilen.Any(Function(z) z.Contains("Prüfung: keine Beanstandungen")), String.Join(vbLf, zeilen))
        Assert.IsTrue(zeilen.Any(Function(z) z.Contains("Räume und Regeln bleiben leer")),
                      "der Assistent soll sagen, was er NICHT erzeugt")
    End Sub

    ''' <summary>Zweimal Vorschau darf nicht zweimal aufspalten - sonst
    ''' entstuenden beim Blaettern zwischen Schritt 4 und 5 immer mehr
    ''' Faecher.</summary>
    <TestMethod>
    Public Sub ZweiteVorschauLiefertDenselbenBestand()
        Dim m = Grundschule(kinderJeKlasse:=20, mitVorlage:=True)
        Dim erste = m.Vorschau()
        Dim zweite = m.Vorschau()

        Assert.AreEqual(erste.Faecher.Count, zweite.Faecher.Count)
        Assert.AreEqual(erste.Gruppen.Count, zweite.Gruppen.Count)
        Assert.AreEqual(erste.Lehrkraefte.Count, zweite.Lehrkraefte.Count)
        Assert.AreEqual(erste.Schueler.Count, zweite.Schueler.Count)
    End Sub

    ' ===============================================================
    ' Der Weg durchs HauptViewModel
    ' ===============================================================

    ''' <summary>"Datei -> Neues Projekt" ist seit F5 der Assistent. Der
    ''' Test haelt fest, dass das HauptViewModel den Entwurf UEBERNIMMT
    ''' und nicht bloss ein leeres Projekt anlegt - und dass die Herkunft
    ''' im Protokoll steht: einer generierten Schule sieht man spaeter
    ''' nicht mehr an, dass ihre Zahlen aus einem Template stammen.</summary>
    <TestMethod>
    Public Sub NeuUebernimmtDenEntwurfDesAssistentenUndProtokolliertIhn()
        Dim ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttas-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(ordner)
        Try
            Dim m = Grundschule(kinderJeKlasse:=20, mitVorlage:=True)
            m.Vorschau()
            m.Pfad = IO.Path.Combine(ordner, "assistent.splanx")
            m.Passwort = "geheim12"

            Dim d As New TestDialoge With {.AssistentEntwurf = m.Entwurf()}
            Dim haupt As New HauptViewModel(d, Function() New DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.Zero))
            haupt.Neu()

            Assert.AreEqual(1, d.AssistentGefragt)
            Assert.IsTrue(haupt.ProjektOffen)
            Assert.IsFalse(haupt.Geaendert, "nach dem Speichern darf nichts mehr offen sein")
            Assert.AreEqual(8, haupt.Projekt.Bestand.Klassen.Count)
            Assert.AreEqual(160, haupt.Projekt.Bestand.Schueler.Count)
            Assert.AreEqual(12, haupt.Projekt.Bestand.Gruppen.Count)
            Assert.IsTrue(IO.File.Exists(m.Pfad))

            Dim protokoll = haupt.Projekt.AuditLog.Where(Function(e) e.Aktion = "assistent").ToList()
            Assert.AreEqual(1, protokoll.Count)
            StringAssert.Contains(protokoll(0).Beschreibung, "Klassen")
        Finally
            IO.Directory.Delete(ordner, recursive:=True)
        End Try
    End Sub

    <TestMethod>
    Public Sub AbbruchImAssistentenLegtNichtsAn()
        Dim d As New TestDialoge With {.AssistentBricht = True}
        Dim haupt As New HauptViewModel(d)
        haupt.Neu()

        Assert.AreEqual(1, d.AssistentGefragt)
        Assert.IsFalse(haupt.ProjektOffen, "ein Abbruch darf kein halbes Projekt hinterlassen")
    End Sub

End Class
