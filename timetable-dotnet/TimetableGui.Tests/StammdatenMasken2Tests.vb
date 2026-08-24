' Stammdaten-Masken, zweiter Teil (gui-ui-konzept.md 6.3, 6.4, 6.7, 6.8, 6.9).
' Wieder nur das, was ueber ein Formular hinausgeht.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class StammdatenMasken2Tests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 23, 19, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttsd2-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(_ordner)
    End Sub

    <TestCleanup>
    Public Sub Abraeumen()
        If IO.Directory.Exists(_ordner) Then IO.Directory.Delete(_ordner, recursive:=True)
    End Sub

    Private Shared ReadOnly Property TestsRoot As String
        Get
            Dim dir = New IO.DirectoryInfo(AppContext.BaseDirectory)
            While dir IsNot Nothing
                If IO.Directory.Exists(IO.Path.Combine(dir.FullName, "tests", "bw-grundschule-beispiel")) Then
                    Return IO.Path.Combine(dir.FullName, "tests")
                End If
                dir = dir.Parent
            End While
            Throw New InvalidOperationException("tests/-Verzeichnis nicht gefunden")
        End Get
    End Property

    Private Function Beispielprojekt(d As TestDialoge) As Projekt
        d.OrdnerPfad = IO.Path.Combine(TestsRoot, "bw-grundschule-beispiel")
        d.SpeichernPfad = IO.Path.Combine(_ordner, "gs.splanx")
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Importieren()
        Return m.Projekt
    End Function

    ' ============ 6.3 Klassen: Zug ergaenzen ============

    ''' <summary>"legt die naechste Parallelklasse (c, d, ...) ueber alle
    ''' gewaehlten Stufen an" - der Buchstabe wird ueber ALLE Stufen
    ''' hinweg bestimmt. Haetten die Stufen eigene Zaehler, entstuenden
    ''' 1c und 2d nebeneinander, und die Zusammengehoerigkeit waere
    ''' hin.</summary>
    <TestMethod>
    Public Sub ZugErgaenzenLegtDenselbenBuchstabenInAllenStufenAn()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New KlassenViewModel(p, d)
        Dim stufen = vm.Stufen.Select(Function(s) s.Nummer).ToList()

        Dim angelegt = vm.ZugErgaenzen(stufen)

        Assert.AreEqual(stufen.Count, angelegt.Count, "In jeder Stufe muss genau eine Klasse entstehen.")
        Dim buchstaben = angelegt.Select(Function(n) n(n.Length - 1)).Distinct().ToList()
        Assert.AreEqual(1, buchstaben.Count, "Alle neuen Klassen brauchen denselben Zugbuchstaben: " & String.Join(", ", angelegt))
        Assert.AreEqual("c"c, buchstaben(0), "Die Beispielschule hat a und b - erwartet wird c.")
    End Sub

    <TestMethod>
    Public Sub ZugErgaenzenLegtNichtsDoppeltAn()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New KlassenViewModel(p, d)
        vm.ZugErgaenzen({1})
        Dim vorher = p.Bestand.Klassen.Count

        ' Noch einmal fuer dieselbe Stufe: der naechste Buchstabe ist
        ' jetzt d, also entsteht 1d - aber kein zweites 1c.
        Dim zweiter = vm.ZugErgaenzen({1})

        Assert.AreEqual(vorher + 1, p.Bestand.Klassen.Count)
        Assert.AreEqual(p.Bestand.Klassen.Select(Function(k) k.Name).Distinct().Count(),
                        p.Bestand.Klassen.Count, "Doppelte Klassennamen entstanden.")
    End Sub

    <TestMethod>
    Public Sub EineStufeOhneKlasseFaelltAuf()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim stufe = p.Bestand.Klassenstufen.First().Nummer
        p.Bestand.Klassen.RemoveAll(Function(k) k.Klassenstufe = stufe)
        Dim vm As New KlassenViewModel(p, d)

        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains($"Stufe {stufe}") AndAlso f.Contains("keine Klasse")),
                      "Fachkontingente ohne Klasse erzeugen keinen Unterricht - das muss auffallen.")
    End Sub

    ' ============ 6.4 Faecher ============

    ''' <summary>"Eine Stufe ohne Zeile heisst 'wird dort nicht
    ''' unterrichtet' - der Dialog zeigt das explizit als Badge statt es
    ''' stumm zu lassen."</summary>
    <TestMethod>
    Public Sub FehlendeStufenzeileWirdBenanntStattVerschwiegen()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New FaecherViewModel(p, d)
        Dim englisch = p.Bestand.Faecher.First(Function(f) f.Name = "Englisch")

        Dim zeilen = vm.StufenZeilen(englisch)

        Assert.AreEqual(p.Bestand.Klassenstufen.Count, zeilen.Count,
                        "Es muss eine Zeile je Klassenstufe geben, auch ohne Kontingent.")
        Assert.IsTrue(zeilen.Any(Function(z) z.Hinweis <> ""),
                      "Englisch wird in der Grundschule nicht ueberall unterrichtet - das muss dastehen.")
        For Each z In zeilen.Where(Function(x) x.Hinweis <> "")
            Assert.IsFalse(z.Soll.HasValue, "Eine Stufe mit Hinweis darf kein Soll tragen.")
        Next
    End Sub

    ''' <summary>Ein Kontingent auf Nothing entfernt die Zeile - "wird
    ''' dort nicht unterrichtet". Eine Zeile mit 0 waere etwas anderes
    ''' ("vorgesehen, aber null Stunden"), und das kennt das Modell
    ''' nicht.</summary>
    <TestMethod>
    Public Sub SollAufNothingEntferntDieZeile()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New FaecherViewModel(p, d)
        Dim sport = p.Bestand.Faecher.First(Function(f) f.Name = "Sport")
        Dim stufe = sport.Klassenstufen.First().Klassenstufe

        vm.SetzeStufe(sport, stufe, Nothing, Nothing)

        Assert.IsFalse(sport.Klassenstufen.Any(Function(fk) fk.Klassenstufe = stufe))
        Assert.IsTrue(vm.StufenZeilen(sport).First(Function(z) z.Stufe = stufe).Hinweis <> "")
    End Sub

    <TestMethod>
    Public Sub EineZuVolleKlassenwocheFaelltAuf()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New FaecherViewModel(p, d)
        Dim stufe = p.Bestand.Klassenstufen.First().Nummer
        Dim deutsch = p.Bestand.Faecher.First(Function(f) f.Name = "Deutsch")

        vm.SetzeStufe(deutsch, stufe, 200, Nothing)

        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains("passen nicht")),
                      "200 Wochenstunden passen in keine Klassenwoche.")
    End Sub

    ' ============ 6.7 Qualifikationsmatrix ============

    ''' <summary>Drei Zustaende, nicht zwei: "fachfremd" ist ein eigener
    ''' Zustand und kein Sonderfall von "qualifiziert" - im
    ''' Lehrereinsatz wird er anders gewichtet.</summary>
    <TestMethod>
    Public Sub DieMatrixSchaltetDurchDreiZustaende()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New QualifikationsmatrixViewModel(p)
        Dim l = p.Bestand.Lehrkraefte.First().Name
        Dim f = "Musik"
        vm.Setze(l, f, Qualifikation.Nein)

        vm.Weiterschalten(l, f)
        Assert.AreEqual(Qualifikation.Qualifiziert, vm.Zustand(l, f), "Der haeufige Fall liegt einen Klick entfernt.")
        vm.Weiterschalten(l, f)
        Assert.AreEqual(Qualifikation.Fachfremd, vm.Zustand(l, f))
        vm.Weiterschalten(l, f)
        Assert.AreEqual(Qualifikation.Nein, vm.Zustand(l, f))
    End Sub

    <TestMethod>
    Public Sub DerSpaltenfussMarkiertNurEchteEngpaesse()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New QualifikationsmatrixViewModel(p)

        Assert.IsFalse(vm.Spaltenfuss.Any(Function(e) e.Engpass),
                       "Die Beispielschule loest - es darf kein Engpass gemeldet werden: " &
                       String.Join(" | ", vm.Spaltenfuss.Where(Function(e) e.Engpass).Select(Function(e) e.Fach)))

        For Each z In p.Bestand.FachLehrerZuordnungen.Where(Function(x) x.FachName = "Sport").ToList()
            p.Bestand.FachLehrerZuordnungen.Remove(z)
        Next
        Assert.IsTrue(vm.Spaltenfuss.First(Function(e) e.Fach = "Sport").Engpass,
                      "Ohne Sportlehrkraft muss Sport rot werden.")
    End Sub

    ' ============ 6.9 Feste Zuordnungen ============

    ''' <summary>"die Auswahllisten filtern auf qualifizierte
    ''' Kombinationen" - eine feste Zuordnung auf ein Fach, das die
    ''' Lehrkraft nicht geben darf, faellt sonst erst dem Solver
    ''' auf.</summary>
    <TestMethod>
    Public Sub DieFachlisteZeigtNurQualifizierteKombinationen()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New FesteZuordnungenViewModel(p, d)
        Dim chor = p.Bestand.Lehrkraefte.First(Function(l) l.Name = "Chorleiterin-1").Name

        Dim moeglich = vm.MoeglicheFaecher(chor)

        CollectionAssert.Contains(moeglich, "Chor")
        CollectionAssert.DoesNotContain(moeglich, "Mathematik",
                                        "Die Chorleiterin ist fuer Mathematik nicht qualifiziert.")
    End Sub

    <TestMethod>
    Public Sub EineUnqualifizierteZuordnungFaelltAuf()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        p.Bestand.FesteZuordnungen.Add(New FesteZuordnung With {
            .LehrerName = "Chorleiterin-1", .KlasseName = "1a", .FachName = "Mathematik"})
        Dim vm As New FesteZuordnungenViewModel(p, d)

        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains("nicht qualifiziert")),
                      String.Join(" | ", vm.Pruefe()))
    End Sub

    ''' <summary>"Klasse ODER aktive Gruppe - gemeinsamer Namensraum"
    ''' (6.9): eine feste Zuordnung darf auf eine Fachgruppe zeigen.</summary>
    <TestMethod>
    Public Sub DieZielListeEnthaeltKlassenUndGruppen()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New FesteZuordnungenViewModel(p, d)

        Dim ziele = vm.MoeglicheKlassen()
        CollectionAssert.Contains(ziele, "1a")
        Assert.IsTrue(p.Bestand.Gruppen.Any(Function(g) ziele.Contains(g.Name)),
                      "Gruppen fehlen im gemeinsamen Namensraum.")
    End Sub

    ' ============ 6.8 Schueler und Gruppen ============

    <TestMethod>
    Public Sub PlatzhalterWerdenDeterministischErzeugt()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New SchuelerGruppenViewModel(p, d)
        Dim vorher = p.Bestand.Schueler.Where(Function(s) s.Klasse = "1a").Count

        Dim erzeugt = vm.PlatzhalterErzeugen("1a", 3)

        Assert.AreEqual(3, erzeugt.Count)
        Assert.AreEqual(vorher + 3, p.Bestand.Schueler.Where(Function(s) s.Klasse = "1a").Count)
        ' Kein Personenbezug: Platzhalter bekommen keinen Mapping-Eintrag.
        For Each id In erzeugt
            Assert.IsFalse(p.Mapping.Any(Function(m) m.Id = id),
                           $"Platzhalter {id} darf keinen mapping.json-Eintrag haben.")
        Next
    End Sub

    ''' <summary>Die Verbund-Regel, wie der KERN sie kennt: eindeutig ist
    ''' das Tupel (Klassenstufe, Fach), nicht das Fach allein. Dasselbe
    ''' Fach ueber mehrere Stufen hinweg ist ausdruecklich erlaubt - die
    ''' schulweite Chor-Gesamtprobe der Beispielschule lebt davon.
    '''
    ''' Der Test prueft bewusst auf den Wortlaut des Kerns: haette die
    ''' Maske eine eigene Meldung, gaebe es zwei Wahrheiten ueber
    ''' denselben Sachverhalt.</summary>
    <TestMethod>
    Public Sub ZweiGruppenMitGleicherStufeUndFachFallenAuf()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        p.Bestand.Gruppen.Add(New Gruppe With {
            .Name = "Doppel-A", .Typ = "Fachgruppe", .FachName = "Sport",
            .Klassenstufe = 1, .Parallelverbund = "Testverbund"})
        p.Bestand.Gruppen.Add(New Gruppe With {
            .Name = "Doppel-B", .Typ = "Fachgruppe", .FachName = "Sport",
            .Klassenstufe = 1, .Parallelverbund = "Testverbund"})
        Dim vm As New SchuelerGruppenViewModel(p, d)

        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains("dieselbe Kombination aus klassenstufe und fach_name")),
                      String.Join(" | ", vm.Pruefe()))
    End Sub

    <TestMethod>
    Public Sub DieBeispielschuleHatEinenSauberenVerbund()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New SchuelerGruppenViewModel(p, d)

        Assert.AreEqual(0, vm.Pruefe().Count, String.Join(" | ", vm.Pruefe()))
    End Sub

End Class
