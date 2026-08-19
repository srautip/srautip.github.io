' Phase 2.15b: ein Test pro Fehlerklasse aus StammdatenValidation.vb -
' jeweils ein sonst sauberer Mini-Bestand, gezielt um genau einen Fehler
' ergaenzt, analog zu SolverTests.vb's Pigeonhole-Test-Muster.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class StammdatenValidationTests

    Private Function CleanBestand() As Stammdatenbestand
        Dim bestand As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        bestand.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})

        Dim deutsch As New Fach With {.Name = "Deutsch"}
        deutsch.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 6})
        bestand.Faecher.Add(deutsch)

        bestand.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        bestand.Lehrkraefte.Add(New Lehrer With {.Name = "Frau Müller", .DeputatSollstunden = 28})
        bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Frau Müller", .FachName = "Deutsch"})
        Return bestand
    End Function

    <TestMethod>
    Public Sub CleanBestandHasNoErrors()
        Assert.AreEqual(0, StammdatenValidation.ValidateStammdaten(CleanBestand()).Count)
    End Sub

    <TestMethod>
    Public Sub FachReferencingUnknownKlassenstufeIsRejected()
        Dim bestand = CleanBestand()
        bestand.Faecher(0).Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 9, .WochenstundenSoll = 2})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("Klassenstufe=9") AndAlso e.Contains("keine bekannte Klassenstufe")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FachWithNonPositiveWochenstundenSollIsRejected()
        Dim bestand = CleanBestand()
        bestand.Faecher(0).Klassenstufen(0).WochenstundenSoll = 0
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("WochenstundenSoll muss > 0 sein")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub KlasseReferencingUnknownKlassenstufeIsRejected()
        Dim bestand = CleanBestand()
        bestand.Klassen.Add(New Klasse With {.Name = "9a", .Klassenstufe = 9})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'9a'") AndAlso e.Contains("keine bekannte Klassenstufe")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FachLehrerZuordnungWithUnknownLehrerIsRejected()
        Dim bestand = CleanBestand()
        bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Herr Unbekannt", .FachName = "Deutsch"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Herr Unbekannt'") AndAlso e.Contains("keine bekannte Lehrkraft")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FachLehrerZuordnungWithUnknownFachIsRejected()
        Dim bestand = CleanBestand()
        bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Frau Müller", .FachName = "Chemie"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Chemie'") AndAlso e.Contains("kein bekanntes Fach")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub NonPositiveDeputatIsRejected()
        Dim bestand = CleanBestand()
        bestand.Lehrkraefte(0).DeputatSollstunden = 0
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("DeputatSollstunden muss > 0 sein")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub NegativeAnrechnungsstundenIsRejected()
        Dim bestand = CleanBestand()
        bestand.Lehrkraefte(0).Anrechnungsstunden = -1
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("Anrechnungsstunden darf nicht negativ sein")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub AnrechnungsstundenConsumingWholeDeputatIsRejected()
        Dim bestand = CleanBestand()
        bestand.Lehrkraefte(0).Anrechnungsstunden = 28
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("darf nicht das gesamte Deputat")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub UsedKlassenstufeWithoutAnyFachIsRejected()
        Dim bestand = CleanBestand()
        bestand.Klassenstufen.Add(New Klassenstufe With {.Nummer = 2, .Bezeichnung = "Klasse 2"})
        bestand.Klassen.Add(New Klasse With {.Name = "2a", .Klassenstufe = 2})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("klassenstufe 2") AndAlso e.Contains("kein einziges Fach")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FachInUsedKlassenstufeWithoutQualifiedTeacherIsRejected()
        Dim bestand = CleanBestand()
        Dim sport As New Fach With {.Name = "Sport"}
        sport.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 3})
        bestand.Faecher.Add(sport)
        ' Bewusst KEINE FachLehrerZuordnung fuer Sport ergaenzt.
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Sport'") AndAlso e.Contains("keine qualifizierte Lehrkraft")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub UnusedKlassenstufeWithoutFachIsNotAnError()
        Dim bestand = CleanBestand()
        ' Klassenstufe 2 existiert im Katalog, wird aber von KEINER Klasse
        ' genutzt - das ist kein Fehler (z.B. eine noch nicht eroeffnete Stufe).
        bestand.Klassenstufen.Add(New Klassenstufe With {.Nummer = 2, .Bezeichnung = "Klasse 2"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.AreEqual(0, errors.Count, String.Join(vbLf, errors))
    End Sub

    ' Phase 2.19: Mitgliedschaftsdatenmodell, Schritt 1 - reine
    ' Referenz-/Eindeutigkeitspruefung fuer Schueler/Gruppen.

    <TestMethod>
    Public Sub SchuelerReferencingUnknownKlasseIsRejected()
        Dim bestand = CleanBestand()
        bestand.Schueler.Add(New Schueler With {.Id = "S-1a-01", .Klasse = "9z"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'9z'") AndAlso e.Contains("keine bekannte Klasse")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub DuplicateSchuelerIdIsRejected()
        Dim bestand = CleanBestand()
        bestand.Schueler.Add(New Schueler With {.Id = "S-1a-01", .Klasse = "1a"})
        bestand.Schueler.Add(New Schueler With {.Id = "S-1a-01", .Klasse = "1a"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'S-1a-01'") AndAlso e.Contains("bereits vergeben")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub GruppeReferencingUnknownSchuelerIdIsRejected()
        Dim bestand = CleanBestand()
        bestand.Schueler.Add(New Schueler With {.Id = "S-1a-01", .Klasse = "1a"})
        bestand.Gruppen.Add(New Gruppe With {
            .Name = "Religion-ev-Kl1", .Typ = "Fachgruppe",
            .MitgliederSchuelerIds = New List(Of String) From {"S-1a-01", "S-unbekannt"}
        })
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'S-unbekannt'") AndAlso e.Contains("keine bekannte Schueler-ID")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub GruppeWithKnownMembersIsNotAnError()
        Dim bestand = CleanBestand()
        bestand.Schueler.Add(New Schueler With {.Id = "S-1a-01", .Klasse = "1a"})
        bestand.Gruppen.Add(New Gruppe With {
            .Name = "Religion-ev-Kl1", .Typ = "Fachgruppe",
            .MitgliederSchuelerIds = New List(Of String) From {"S-1a-01"}
        })
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.AreEqual(0, errors.Count, String.Join(vbLf, errors))
    End Sub

End Class
