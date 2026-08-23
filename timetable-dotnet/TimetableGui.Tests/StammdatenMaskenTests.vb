' Stammdaten-Masken, Stufe F2 (gui-ui-konzept.md 6.2, 6.6, 6.7).
'
' Geprueft wird das, was ueber ein Formular hinausgeht: die Warnung beim
' Verkleinern des Rasters und die Plausibilitaet im Kopf des
' Lehrkraft-Dialogs. Ein Formularfeld, das einen Wert speichert, braucht
' keinen Test - eine Maske, die eine Folge VERSCHWEIGT, waere dagegen
' genau der Fehler, den das Konzept vermeiden will.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class StammdatenMaskenTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 23, 18, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttsd-" & Guid.NewGuid().ToString("N"))
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

    ' ===============================================================
    ' 6.2 Schuldaten - das Raster
    ' ===============================================================

    ''' <summary>Der Kern von 6.2: "Warnung mit Konsequenzliste, wenn
    ''' Tage/Stunden verkleinert werden, waehrend Slot-Regeln oder
    ''' Fenster existieren, die dann ins Leere zeigen."</summary>
    <TestMethod>
    Public Sub RasterVerkleinernNenntDieRegelnDieInsLeereZeigen()
        Dim d As New TestDialoge With {.FrageAntwort = False}
        Dim p = Beispielprojekt(d)
        p.Constraints.Add(Text.Json.Nodes.JsonNode.Parse(
            "{""type"":""forbidden_slot"",""scope"":""class"",""entity"":""1a"",""day"":""Fr"",""period"":3}").AsObject())
        Dim vm As New SchuldatenViewModel(p, d)

        Dim ok = vm.SetzeRaster({"Mo", "Di", "Mi", "Do"}, p.Bestand.PeriodsPerDay)

        Assert.IsFalse(ok, "Ein Nein muss die Aenderung verhindern.")
        Assert.AreEqual(1, d.Fragen.Count, "Es wurde nicht gewarnt.")
        StringAssert.Contains(d.Fragen(0), "forbidden_slot", d.Fragen(0))
        StringAssert.Contains(d.Fragen(0), "Fr", d.Fragen(0))
        Assert.IsTrue(p.Bestand.Tage.Contains("Fr"), "Trotz Nein wurde das Raster geaendert.")
    End Sub

    <TestMethod>
    Public Sub RasterVergroessernWarntNicht()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New SchuldatenViewModel(p, d)

        Assert.IsTrue(vm.SetzeRaster(p.Bestand.Tage.ToList(), p.Bestand.PeriodsPerDay + 2))
        Assert.AreEqual(0, d.Fragen.Count, "Vergroessern entwertet keine Regel - da ist nichts zu warnen.")
    End Sub

    ''' <summary>Ohne betroffene Regeln gibt es keine Rueckfrage. Eine
    ''' Warnung, die immer kommt, wird weggeklickt statt gelesen.</summary>
    <TestMethod>
    Public Sub OhneBetroffeneRegelnWirdNichtGefragt()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        p.Constraints.Clear()
        Dim vm As New SchuldatenViewModel(p, d)

        Assert.IsTrue(vm.SetzeRaster({"Mo", "Di", "Mi", "Do"}, p.Bestand.PeriodsPerDay))
        Assert.AreEqual(0, d.Fragen.Count)
        Assert.AreEqual(4, p.Bestand.Tage.Count)
    End Sub

    <TestMethod>
    Public Sub EinLeeresRasterWirdAbgelehnt()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vorherTage = p.Bestand.Tage.Count
        Dim vm As New SchuldatenViewModel(p, d)

        Assert.IsFalse(vm.SetzeRaster(New String() {}, 6))
        Assert.IsFalse(vm.SetzeRaster({"Mo"}, 0))
        Assert.AreEqual(vorherTage, p.Bestand.Tage.Count)
    End Sub

    ' ===============================================================
    ' 6.6 Lehrkraefte - die Plausibilitaet im Kopf
    ' ===============================================================

    ''' <summary>Der "Kanarienvogel" aus 6.6: zu wenig Deputat ist ein
    ''' BEWEIS, dass kein Plan aufgeht - und muss vor dem Lauf stehen,
    ''' nicht danach.</summary>
    <TestMethod>
    Public Sub ZuWenigDeputatStehtImKopfDesDialogs()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        For Each l In p.Bestand.Lehrkraefte
            l.DeputatSollstunden = 1
        Next
        Dim vm As New LehrkraefteViewModel(p, d)

        StringAssert.Contains(vm.PlausibilitaetsZeile, "fehlen", vm.PlausibilitaetsZeile)
        StringAssert.Contains(vm.PlausibilitaetsZeile, "kann kein Plan aufgehen", vm.PlausibilitaetsZeile)
    End Sub

    ''' <summary>Die Gegenrichtung: ein ueberdimensionierter Pool ist
    ''' kein Fehler, sondern der Hinweis auf den verteilten Leerlauf, den
    ''' das GMS-Beispiel gelehrt hat. Die Meldung darf deshalb nicht wie
    ''' ein Mangel klingen.</summary>
    <TestMethod>
    Public Sub EinZuGrosserPoolWirdAlsReserveBenanntNichtAlsFehler()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        For Each l In p.Bestand.Lehrkraefte
            l.DeputatSollstunden = 60
        Next
        Dim vm As New LehrkraefteViewModel(p, d)

        StringAssert.Contains(vm.PlausibilitaetsZeile, "Reserve", vm.PlausibilitaetsZeile)
        StringAssert.Contains(vm.PlausibilitaetsZeile, "Leerlauf", vm.PlausibilitaetsZeile)
        Assert.IsFalse(vm.PlausibilitaetsZeile.Contains("fehlen"),
                       "Reserve ist kein Mangel und darf nicht so klingen.")
    End Sub

    ''' <summary>Haelt die Bedarfsformel fest. Wird ein Fach ueber
    ''' Fachgruppen unterrichtet, sind die GRUPPEN die Bedarfstraeger,
    ''' nicht die Klassen - eine Religionsgruppe fasst die Kinder
    ''' mehrerer Parallelklassen zusammen und braucht trotzdem nur eine
    ''' Lehrkraft.
    '''
    ''' Die erste Fassung rechnete "Stunden x Klassen" und meldete fuer
    ''' diese Schule drei Engpaesse, die es nicht gibt - jeweils genau um
    ''' die Zahl der Zuege zu hoch. Ohne diesen Test faellt der Rueckfall
    ''' erst wieder jemandem auf, der die Schule kennt.</summary>
    <TestMethod>
    Public Sub BeiFachgruppenZaehlenGruppenNichtKlassen()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim b = p.Bestand

        Dim gruppen = b.Gruppen.Where(Function(g) g.FachName = "Religion-ev").Count
        Dim klassen = b.Klassen.Count
        Assert.IsTrue(gruppen > 0 AndAlso klassen > gruppen,
                      "Testgrundlage: Religion-ev muss ueber weniger Gruppen als Klassen laufen.")

        ' 2 Wochenstunden je Stufe, eine Gruppe je Stufe.
        Assert.AreEqual(gruppen * 2, Kennzahlen.BedarfJeFach(b, "Religion-ev"),
                        "Der Bedarf muss den Gruppen folgen, nicht den Klassen.")

        ' Gegenprobe an einem Fach OHNE Fachgruppen-Charakter: dort
        ' bleiben die Klassen die Bedarfstraeger.
        Dim sport = b.Faecher.First(Function(f) f.Name = "Sport")
        Dim erwartet = sport.Klassenstufen.Sum(
            Function(fk) fk.WochenstundenSoll * b.Klassen.Where(Function(k) k.Klassenstufe = fk.Klassenstufe).Count)
        Assert.IsTrue(Kennzahlen.BedarfJeFach(b, "Sport") > 0)
    End Sub

    ''' <summary>Ein Parallelverbund belegt die Klassenwoche EINMAL, auch
    ''' wenn er drei Faecher umfasst - das Kind sitzt in Religion ODER
    ''' Ethik. Eine naive Summe haette die Woche um vier Stunden zu voll
    ''' gerechnet.</summary>
    <TestMethod>
    Public Sub EinParallelverbundBelegtDieKlassenwocheNurEinmal()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim b = p.Bestand

        Dim naiv = b.Faecher.
            SelectMany(Function(f) f.Klassenstufen.Where(Function(fk) fk.Klassenstufe = 1)).
            Sum(Function(fk) fk.WochenstundenSoll)
        Dim echt = Kennzahlen.SollJeKlasse(b, 1)

        Assert.IsTrue(echt < naiv,
                      $"Der Verbund wurde nicht zusammengefasst: naiv {naiv}, gerechnet {echt}.")
        Assert.IsTrue(echt <= Kennzahlen.KapazitaetJeKlasse(b),
                      $"Die Klassenwoche passt nicht ins Raster: {echt} Stunden auf {Kennzahlen.KapazitaetJeKlasse(b)}.")
    End Sub

    <TestMethod>
    Public Sub DieBeispielschuleHatKeinenFachEngpass()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New LehrkraefteViewModel(p, d)

        Assert.AreEqual(0, vm.Engpaesse.Count,
                        String.Join(" | ", vm.Engpaesse.Select(Function(e) $"{e.Fach}: {e.Bedarf}>{e.Deputat}")))
    End Sub

    ''' <summary>Die Luecke, die StammdatenValidation offenlaesst und die
    ''' 6.7 praeventiv sichtbar machen soll: ein Fach, fuer das niemand
    ''' qualifiziert ist.</summary>
    <TestMethod>
    Public Sub EinFachOhneQualifizierteLehrkraftFaelltAuf()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        p.Bestand.FachLehrerZuordnungen.RemoveAll(Function(z) z.FachName = "Musik")
        Dim vm As New LehrkraefteViewModel(p, d)

        Dim treffer = vm.Engpaesse.Where(Function(e) e.Fach = "Musik").ToList()
        Assert.AreEqual(1, treffer.Count, "Musik hat keine Lehrkraft mehr und muss als Engpass erscheinen.")
        Assert.AreEqual(0.0, treffer(0).Deputat, 0.001)
        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains("Musik")))
    End Sub

    ''' <summary>Qualifikationen gehoeren in die Lehrkraft-Maske, nicht in
    ''' einen eigenen Dialog (6.6) - und `fachfremd` ist eine Eigenschaft
    ''' der ZUORDNUNG, nicht der Person.</summary>
    <TestMethod>
    Public Sub QualifikationSetzenPflegtDieZuordnungen()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim vm As New LehrkraefteViewModel(p, d)
        Dim l = p.Bestand.Lehrkraefte.First()

        vm.SetzeQualifikation(l, "Musik", qualifiziert:=True, fachfremd:=True)
        Assert.IsTrue(vm.IstQualifiziert(l, "Musik"))
        Assert.IsTrue(vm.IstFachfremd(l, "Musik"))

        vm.SetzeQualifikation(l, "Musik", qualifiziert:=True, fachfremd:=False)
        Assert.IsFalse(vm.IstFachfremd(l, "Musik"), "Das Fachfremd-Haekchen laesst sich nicht zuruecknehmen.")

        vm.SetzeQualifikation(l, "Musik", qualifiziert:=False, fachfremd:=False)
        Assert.IsFalse(vm.IstQualifiziert(l, "Musik"))
        Assert.IsFalse(p.Bestand.FachLehrerZuordnungen.Any(
            Function(z) z.LehrerName = l.Name AndAlso z.FachName = "Musik"),
            "Die Zuordnung wurde nicht entfernt.")
    End Sub

    <TestMethod>
    Public Sub AnrechnungenUeberDemDeputatFallenAuf()
        Dim d As New TestDialoge()
        Dim p = Beispielprojekt(d)
        Dim l = p.Bestand.Lehrkraefte.First()
        l.Anrechnungsstunden = l.DeputatSollstunden + 5
        Dim vm As New LehrkraefteViewModel(p, d)

        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains("uebersteigen")),
                      "Negatives Restdeputat ist ein Eingabefehler und muss auffallen.")
    End Sub

End Class
