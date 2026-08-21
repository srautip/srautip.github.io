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

    ' Phase 2.20: Parallelgruppe-Referenz-/Konsistenzpruefung.

    ''' <summary>2 Klassen (1a/1b) derselben Klassenstufe, 2 Faecher
    ''' (Religion-ev/Religion-kath, je 2h), je 1 qualifizierte Lehrkraft,
    ''' 2 Schueler pro Klasse (je 1 pro Variante) - ein sauberer
    ''' Parallelverbund "Religion-Kl1" ueber beide Klassen hinweg.</summary>
    Private Function BestandMitParallelverbund() As Stammdatenbestand
        Dim bestand = CleanBestand()
        bestand.Klassen.Add(New Klasse With {.Name = "1b", .Klassenstufe = 1})

        Dim religionEv As New Fach With {.Name = "Religion-ev"}
        religionEv.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        bestand.Faecher.Add(religionEv)

        Dim religionKath As New Fach With {.Name = "Religion-kath"}
        religionKath.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 2})
        bestand.Faecher.Add(religionKath)

        bestand.Lehrkraefte.Add(New Lehrer With {.Name = "Herr Ev", .DeputatSollstunden = 8})
        bestand.Lehrkraefte.Add(New Lehrer With {.Name = "Frau Kath", .DeputatSollstunden = 8})
        bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Herr Ev", .FachName = "Religion-ev"})
        bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Frau Kath", .FachName = "Religion-kath"})

        bestand.Schueler.Add(New Schueler With {.Id = "S-1a-01", .Klasse = "1a"})
        bestand.Schueler.Add(New Schueler With {.Id = "S-1a-02", .Klasse = "1a"})
        bestand.Schueler.Add(New Schueler With {.Id = "S-1b-01", .Klasse = "1b"})
        bestand.Schueler.Add(New Schueler With {.Id = "S-1b-02", .Klasse = "1b"})

        bestand.Gruppen.Add(New Gruppe With {
            .Name = "Religion-ev-Kl1", .Typ = "Fachgruppe", .FachName = "Religion-ev",
            .Klassenstufe = 1, .Parallelverbund = "Religion-Kl1",
            .MitgliederSchuelerIds = New List(Of String) From {"S-1a-01", "S-1b-01"}
        })
        bestand.Gruppen.Add(New Gruppe With {
            .Name = "Religion-kath-Kl1", .Typ = "Fachgruppe", .FachName = "Religion-kath",
            .Klassenstufe = 1, .Parallelverbund = "Religion-Kl1",
            .MitgliederSchuelerIds = New List(Of String) From {"S-1a-02", "S-1b-02"}
        })

        Return bestand
    End Function

    <TestMethod>
    Public Sub CleanParallelverbundHasNoErrors()
        Dim errors = StammdatenValidation.ValidateStammdaten(BestandMitParallelverbund())
        Assert.AreEqual(0, errors.Count, String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub GruppeReferencingUnknownFachNameIsRejected()
        Dim bestand = BestandMitParallelverbund()
        bestand.Gruppen(0).FachName = "Nicht-Existent"
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Nicht-Existent'") AndAlso e.Contains("kein bekanntes Fach")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub GruppeKlassenstufeMismatchingMembersIsRejected()
        Dim bestand = BestandMitParallelverbund()
        bestand.Gruppen(0).Klassenstufe = 2
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Religion-ev-Kl1'") AndAlso e.Contains("stimmt nicht mit der tatsaechlichen Klassenstufe")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub GruppeFachNotOfferedInKlassenstufeIsRejected()
        Dim bestand = BestandMitParallelverbund()
        bestand.Faecher.Single(Function(f) f.Name = "Religion-ev").Klassenstufen.Clear()
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Religion-ev-Kl1'") AndAlso e.Contains("wird in klassenstufe 1 nicht gefuehrt")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub ParallelverbundMemberWithoutFachNameIsRejected()
        Dim bestand = BestandMitParallelverbund()
        bestand.Gruppen(0).FachName = Nothing
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Religion-Kl1'") AndAlso e.Contains("ohne fach_name")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub ParallelverbundWithDuplicateFachNameIsRejected()
        Dim bestand = BestandMitParallelverbund()
        bestand.Gruppen(1).FachName = "Religion-ev"
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Religion-Kl1'") AndAlso e.Contains("beanspruchen dieselbe Kombination aus klassenstufe und fach_name")), String.Join(vbLf, errors))
    End Sub

    ''' <summary>Phase 2.23: die frueher harte "alle Mitglieder muessen
    ''' dieselbe Klassenstufe haben"-Pruefung ist einer klassenstufen-
    ''' uebergreifenden Kombination gewichen (Anwendungsfall: eine
    ''' schulweite Chor-Gesamtprobe, bei der ein Fach ueber mehrere
    ''' Klassenstufen hinweg synchron laufen soll) - ein drittes Mitglied
    ''' mit ABWEICHENDER Klassenstufe UND demselben Fach wie ein
    ''' bestehendes Mitglied (Religion-ev, bereits in Klassenstufe 1
    ''' vertreten) validiert jetzt sauber, solange die (Klassenstufe,
    ''' FachName)-Kombination im Verbund eindeutig bleibt und
    ''' WochenstundenSoll/BlockLength identisch sind.</summary>
    <TestMethod>
    Public Sub ParallelverbundAcrossKlassenstufenIsAllowed()
        Dim bestand = BestandMitParallelverbund()
        bestand.Klassenstufen.Add(New Klassenstufe With {.Nummer = 2, .Bezeichnung = "Klasse 2"})
        bestand.Klassen.Add(New Klasse With {.Name = "2a", .Klassenstufe = 2})
        bestand.Faecher.Single(Function(f) f.Name = "Religion-ev").Klassenstufen.Add(
            New FachKlassenstufe With {.Klassenstufe = 2, .WochenstundenSoll = 2})
        bestand.Schueler.Add(New Schueler With {.Id = "S-2a-01", .Klasse = "2a"})
        bestand.Gruppen.Add(New Gruppe With {
            .Name = "Religion-ev-Kl2", .Typ = "Fachgruppe", .FachName = "Religion-ev",
            .Klassenstufe = 2, .Parallelverbund = "Religion-Kl1",
            .MitgliederSchuelerIds = New List(Of String) From {"S-2a-01"}
        })
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.AreEqual(0, errors.Count, String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub ParallelverbundWithDifferingWochenstundenSollIsRejected()
        Dim bestand = BestandMitParallelverbund()
        bestand.Faecher.Single(Function(f) f.Name = "Religion-kath").Klassenstufen(0).WochenstundenSoll = 3
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Religion-Kl1'") AndAlso e.Contains("unterschiedliche wochenstunden_soll")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub ParallelverbundWithDifferingBlockLengthIsRejected()
        Dim bestand = BestandMitParallelverbund()
        bestand.Faecher.Single(Function(f) f.Name = "Religion-ev").BlockLength = 2
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Religion-Kl1'") AndAlso e.Contains("unterschiedliche block_length")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub ParallelverbundWithOverlappingMemberIsRejected()
        Dim bestand = BestandMitParallelverbund()
        bestand.Gruppen(1).MitgliederSchuelerIds.Add("S-1a-01")
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Religion-Kl1'") AndAlso e.Contains("'S-1a-01'") AndAlso e.Contains("mehr als einer Gruppe")), String.Join(vbLf, errors))
    End Sub

    ' Phase 2.26: FesteZuordnung - harte Lehrer-Klasse-Fach-Pinnung.

    <TestMethod>
    Public Sub FesteZuordnungWithUnknownKlasseIsRejected()
        Dim bestand = CleanBestand()
        bestand.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Frau Müller", .KlasseName = "9z", .FachName = "Deutsch"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'9z'") AndAlso e.Contains("keine bekannte Klasse")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FesteZuordnungWithUnknownFachIsRejected()
        Dim bestand = CleanBestand()
        bestand.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Frau Müller", .KlasseName = "1a", .FachName = "Chemie"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Chemie'") AndAlso e.Contains("kein bekanntes Fach")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FesteZuordnungWithUnknownLehrerIsRejected()
        Dim bestand = CleanBestand()
        bestand.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Herr Unbekannt", .KlasseName = "1a", .FachName = "Deutsch"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("'Herr Unbekannt'") AndAlso e.Contains("keine bekannte Lehrkraft")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FesteZuordnungReferencingGruppeInsteadOfKlasseIsRejected()
        Dim bestand = CleanBestand()
        bestand.Gruppen.Add(New Gruppe With {.Name = "Religion-Kl1", .Typ = "Fachgruppe"})
        bestand.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Frau Müller", .KlasseName = "Religion-Kl1", .FachName = "Deutsch"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("referenziert eine Gruppe")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FesteZuordnungForUnqualifiedTeacherIsRejected()
        Dim bestand = CleanBestand()
        bestand.Lehrkraefte.Add(New Lehrer With {.Name = "Herr Ohne-Deutsch", .DeputatSollstunden = 28})
        bestand.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Herr Ohne-Deutsch", .KlasseName = "1a", .FachName = "Deutsch"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("nicht qualifiziert")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FesteZuordnungForFachNotOfferedInKlassenstufeIsRejected()
        Dim bestand = CleanBestand()
        Dim englisch As New Fach With {.Name = "Englisch"}
        englisch.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 2, .WochenstundenSoll = 2})
        bestand.Faecher.Add(englisch)
        bestand.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Frau Müller", .KlasseName = "1a", .FachName = "Englisch"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("wird in klassenstufe 1") AndAlso e.Contains("nicht gefuehrt")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FesteZuordnungWithTeilzeitInkohaerenterLehrerIsRejected()
        Dim bestand = CleanBestand()
        bestand.Faecher(0).Klassenstufen(0).MaxProTag = 2 ' effectiveMaxProTag=2, WochenstundenSoll=6
        bestand.Lehrkraefte.Add(New Lehrer With {.Name = "Teilzeit-Lehrer", .DeputatSollstunden = 10, .VerfuegbareTage = New List(Of String) From {"Mo", "Di"}}) ' 2 Tage * 2 = 4 < 6
        bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Teilzeit-Lehrer", .FachName = "Deutsch"})
        bestand.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Teilzeit-Lehrer", .KlasseName = "1a", .FachName = "Deutsch"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("teilzeit-tage-inkohaerent")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FesteZuordnungenWithConflictingTeachersForSameKlasseFachAreRejected()
        Dim bestand = CleanBestand()
        bestand.Lehrkraefte.Add(New Lehrer With {.Name = "Zweite Lehrkraft", .DeputatSollstunden = 28})
        bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Zweite Lehrkraft", .FachName = "Deutsch"})
        bestand.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Frau Müller", .KlasseName = "1a", .FachName = "Deutsch"})
        bestand.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Zweite Lehrkraft", .KlasseName = "1a", .FachName = "Deutsch"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("widerspruechliche feste Zuordnungen")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub FesteZuordnungForValidQualifiedTeacherIsAccepted()
        Dim bestand = CleanBestand()
        bestand.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Frau Müller", .KlasseName = "1a", .FachName = "Deutsch"})
        Dim errors = StammdatenValidation.ValidateStammdaten(bestand)
        Assert.AreEqual(0, errors.Count, String.Join(vbLf, errors))
    End Sub

End Class
