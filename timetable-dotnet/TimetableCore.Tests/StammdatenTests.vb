' Phase 2.15a: reine Serialisierungs-/Projektionstests fuer Stammdaten.vb -
' kein Solver/CP-SAT involviert, prueft nur, dass das typisierte
' Domaenenmodell verlustfrei rundtrippt und dass BuildEntitiesFragment
' korrekt in das bestehende entities-JSON-Format projiziert.
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class StammdatenTests

    Private Function SampleBestand() As Stammdatenbestand
        Dim bestand As New Stammdatenbestand With {
            .SchulName = "Testgrundschule",
            .Bundesland = "BW",
            .Schulart = "Grundschule",
            .Tage = New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"},
            .PeriodsPerDay = 6
        }
        bestand.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        bestand.Klassenstufen.Add(New Klassenstufe With {.Nummer = 2, .Bezeichnung = "Klasse 2"})

        Dim deutsch As New Fach With {.Name = "Deutsch"}
        deutsch.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 6, .MaxProTag = 2})
        deutsch.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 2, .WochenstundenSoll = 6, .MaxProTag = 2})
        bestand.Faecher.Add(deutsch)

        Dim sport As New Fach With {.Name = "Sport", .BlockLength = Nothing}
        sport.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 3, .MaxProTag = 1})
        bestand.Faecher.Add(sport)

        bestand.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1, .Schuelerzahl = 22})
        bestand.Klassen.Add(New Klasse With {.Name = "2a", .Klassenstufe = 2, .Schuelerzahl = 20})

        bestand.Raeume.Add(New Raum With {.Name = "Turnhalle1", .Typ = "Turnhalle"})

        bestand.Lehrkraefte.Add(New Lehrer With {
            .Name = "Frau Müller", .DeputatSollstunden = 28, .Anrechnungsstunden = 0,
            .VerfuegbareTage = Nothing, .KlassenlehrerFaehig = True
        })
        bestand.Lehrkraefte.Add(New Lehrer With {
            .Name = "Herr Schmidt", .DeputatSollstunden = 14, .Anrechnungsstunden = 0,
            .VerfuegbareTage = New List(Of String) From {"Mo", "Di", "Mi"}, .KlassenlehrerFaehig = False
        })
        bestand.Lehrkraefte(0).BevorzugteKlassenstufen.Add(1)

        bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Frau Müller", .FachName = "Deutsch"})
        bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Herr Schmidt", .FachName = "Sport"})

        bestand.Schueler.Add(New Schueler With {.Id = "S-1a-01", .Klasse = "1a"})
        bestand.Schueler.Add(New Schueler With {.Id = "S-1a-02", .Klasse = "1a"})
        bestand.Schueler.Add(New Schueler With {.Id = "S-2a-01", .Klasse = "2a"})

        bestand.Gruppen.Add(New Gruppe With {
            .Name = "Religion-ev-Kl1", .Typ = "Fachgruppe",
            .MitgliederSchuelerIds = New List(Of String) From {"S-1a-01"}
        })

        Return bestand
    End Function

    <TestMethod>
    Public Sub SerializeThenDeserializeRoundtripsExactly()
        Dim original = SampleBestand()
        Dim json = Stammdaten.SerializeStammdaten(original)
        Dim restored = Stammdaten.DeserializeStammdaten(json)

        Assert.AreEqual(original.SchulName, restored.SchulName)
        Assert.AreEqual(original.Bundesland, restored.Bundesland)
        Assert.AreEqual(original.Schulart, restored.Schulart)
        Assert.AreEqual(original.PeriodsPerDay, restored.PeriodsPerDay)
        CollectionAssert.AreEqual(original.Tage, restored.Tage)

        Assert.AreEqual(2, restored.Klassenstufen.Count)
        Assert.AreEqual(2, restored.Faecher.Count)
        Assert.AreEqual(2, restored.Faecher(0).Klassenstufen.Count)
        Assert.AreEqual(6, restored.Faecher(0).Klassenstufen(0).WochenstundenSoll)
        Assert.AreEqual(2, restored.Faecher(0).Klassenstufen(0).MaxProTag)

        Assert.AreEqual(2, restored.Klassen.Count)
        Assert.AreEqual(1, restored.Raeume.Count)

        Assert.AreEqual(2, restored.Lehrkraefte.Count)
        Dim mueller = restored.Lehrkraefte.Single(Function(l) l.Name = "Frau Müller")
        Assert.AreEqual(28, mueller.DeputatSollstunden)
        Assert.IsNull(mueller.VerfuegbareTage)
        CollectionAssert.AreEqual(New List(Of Integer) From {1}, mueller.BevorzugteKlassenstufen)
        Assert.IsTrue(mueller.KlassenlehrerFaehig)

        Dim schmidt = restored.Lehrkraefte.Single(Function(l) l.Name = "Herr Schmidt")
        CollectionAssert.AreEqual(New List(Of String) From {"Mo", "Di", "Mi"}, schmidt.VerfuegbareTage)
        Assert.IsFalse(schmidt.KlassenlehrerFaehig)

        Assert.AreEqual(2, restored.FachLehrerZuordnungen.Count)

        Assert.AreEqual(3, restored.Schueler.Count)
        Dim s1 = restored.Schueler.Single(Function(s) s.Id = "S-1a-01")
        Assert.AreEqual("1a", s1.Klasse)

        Assert.AreEqual(1, restored.Gruppen.Count)
        Dim gruppe = restored.Gruppen(0)
        Assert.AreEqual("Religion-ev-Kl1", gruppe.Name)
        Assert.AreEqual("Fachgruppe", gruppe.Typ)
        CollectionAssert.AreEqual(New List(Of String) From {"S-1a-01"}, gruppe.MitgliederSchuelerIds)
    End Sub

    <TestMethod>
    Public Sub SaveThenLoadRoundtripsViaDisk()
        Dim original = SampleBestand()
        Dim filePath = Path.Combine(Path.GetTempPath(), $"stammdaten-test-{Guid.NewGuid()}.json")
        Try
            Stammdaten.SaveStammdaten(original, filePath)
            Dim restored = Stammdaten.LoadStammdaten(filePath)
            Assert.AreEqual(original.SchulName, restored.SchulName)
            Assert.AreEqual(original.Klassen.Count, restored.Klassen.Count)
            Assert.AreEqual(original.Lehrkraefte.Count, restored.Lehrkraefte.Count)
        Finally
            If File.Exists(filePath) Then File.Delete(filePath)
        End Try
    End Sub

    <TestMethod>
    Public Sub BuildEntitiesFragmentProjectsNamesIntoExistingFormat()
        Dim bestand = SampleBestand()
        Dim ent = Stammdaten.BuildEntitiesFragment(bestand)

        CollectionAssert.AreEquivalent(New List(Of String) From {"1a", "2a"}, JsonHelpers.AsStringList(ent, "classes"))
        CollectionAssert.AreEquivalent(New List(Of String) From {"Frau Müller", "Herr Schmidt"}, JsonHelpers.AsStringList(ent, "teachers"))
        CollectionAssert.AreEquivalent(New List(Of String) From {"Deutsch", "Sport"}, JsonHelpers.AsStringList(ent, "subjects"))
        CollectionAssert.AreEquivalent(New List(Of String) From {"Turnhalle1"}, JsonHelpers.AsStringList(ent, "rooms"))

        Dim timeslots = JsonHelpers.Timeslots(ent)
        CollectionAssert.AreEqual(New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}, JsonHelpers.AsStringList(timeslots, "days"))
        Assert.AreEqual(6, JsonHelpers.GetInt(timeslots, "periods_per_day"))

        ' entities-Fragment allein reicht bereits, um an Validation.ValidateEntities
        ' teilzunehmen (leere constraints-Liste ist ein triviales gueltiges Szenario).
        Dim data As New Text.Json.Nodes.JsonObject From {{"entities", ent}, {"constraints", New Text.Json.Nodes.JsonArray()}}
        Assert.AreEqual(0, Validation.ValidateEntities(data).Count)
    End Sub

    <TestMethod>
    Public Sub FaecherOfKlassenstufeAndWochenstundenFuerAndLehrerFuerFachWork()
        Dim bestand = SampleBestand()

        Dim faecherKl1 = Stammdaten.FaecherOfKlassenstufe(bestand, 1)
        CollectionAssert.AreEquivalent(New List(Of String) From {"Deutsch", "Sport"}, faecherKl1.Select(Function(f) f.Name).ToList())

        Dim faecherKl2 = Stammdaten.FaecherOfKlassenstufe(bestand, 2)
        CollectionAssert.AreEquivalent(New List(Of String) From {"Deutsch"}, faecherKl2.Select(Function(f) f.Name).ToList())

        Dim deutsch = bestand.Faecher.Single(Function(f) f.Name = "Deutsch")
        Dim wsKl1 = Stammdaten.WochenstundenFuer(deutsch, 1)
        Assert.AreEqual(6, wsKl1.WochenstundenSoll)
        Assert.IsNull(Stammdaten.WochenstundenFuer(deutsch, 3))

        Dim deutschLehrer = Stammdaten.LehrerFuerFach(bestand, "Deutsch")
        CollectionAssert.AreEquivalent(New List(Of String) From {"Frau Müller"}, deutschLehrer.Select(Function(l) l.Name).ToList())
    End Sub

End Class
