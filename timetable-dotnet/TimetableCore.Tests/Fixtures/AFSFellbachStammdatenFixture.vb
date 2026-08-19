' Phase 2.16: eine realistische, an der Anne-Frank-Schule (AFS) Fellbach
' orientierte Grundschule - ueber das Phase-2.15-Stammdaten-Modell gebaut
' (im Unterschied zu GrundschuleGrossFixture.vb, das denselben realen
' Schul-Vorbild bereits vor Phase 2.15 rein ueber das JSON/entities-Muster
' abbildete). Wie jede andere named-school-Fixture in diesem Projekt
' AUSDRUECKLICH synthetische, plausible Testdaten - kein Anspruch auf
' reale, aktuelle Schuldaten der AFS Fellbach.
'
' Die reale AFS Fellbach (Fellbach-Schmiden) ist eine Ganztags-Grundschule
' mit Sport-/Bewegungsprofil, die selbst DREIZUEGIGKEIT als Ziel nennt
' (Phase-2.10-Recherche, siehe GrundschuleGrossFixture.vb) - diese Fixture
' bildet deshalb bewusst 3 Zuege ab (nicht die 4-zuegige Stresstest-
' Variante aus Phase 2.10), sodass Klassenstufe 4 automatisch 4a/4b/4c
' hat.
'
' Faecher/Wochenstunden sind 1:1 aus GrundschuleGrossFixture.vb's bereits
' recherchierten UND bereits als loesbar bestaetigten Werten uebernommen
' (reduziert das Risiko einer neu erfundenen, unloesbaren Kombination -
' gleiches Wiederverwendungsprinzip wie bei StammdatenBWFixture.vb,
' Phase 2.15).
Public Module AFSFellbachStammdatenFixture

    Private Sub AddKlassen(bestand As Stammdatenbestand, klassenstufe As Integer, buchstaben As IEnumerable(Of String))
        For Each buchstabe In buchstaben
            bestand.Klassen.Add(New Klasse With {.Name = $"{klassenstufe}{buchstabe}", .Klassenstufe = klassenstufe, .Schuelerzahl = 24})
        Next
    End Sub

    Private Function GetOrAddFach(bestand As Stammdatenbestand, name As String) As Fach
        Dim existing = bestand.Faecher.FirstOrDefault(Function(f) f.Name = name)
        If existing IsNot Nothing Then Return existing
        Dim fach As New Fach With {.Name = name}
        bestand.Faecher.Add(fach)
        Return fach
    End Function

    Private Sub AddFachKlassenstufen(bestand As Stammdatenbestand, name As String, klassenstufen As IEnumerable(Of Integer),
                                      wochenstunden As Integer, maxProTag As Integer?)
        Dim fach = GetOrAddFach(bestand, name)
        For Each ks In klassenstufen
            fach.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = ks, .WochenstundenSoll = wochenstunden, .MaxProTag = maxProTag})
        Next
    End Sub

    Private Sub AddLehrerPool(bestand As Stammdatenbestand, namePrefix As String, anzahl As Integer, deputat As Double,
                               faecher As IEnumerable(Of String), klassenlehrerFaehig As Boolean)
        For i = 1 To anzahl
            Dim name = $"{namePrefix}-{i}"
            bestand.Lehrkraefte.Add(New Lehrer With {.Name = name, .DeputatSollstunden = deputat, .KlassenlehrerFaehig = klassenlehrerFaehig})
            For Each f In faecher
                bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = name, .FachName = f})
            Next
        Next
    End Sub

    ''' <summary>Kl.1-4, 3-zuegig (12 Klassen). Klassenlehrer-Prinzip: 6
    ''' Kernfach-Lehrkraefte (Deputat 28h) decken die Deutsch/Mathematik/
    ''' Sachunterricht-Gesamtnachfrage (162h) fast passgenau ab
    ''' (168h Kapazitaet, exakt 2 Klassen pro Lehrkraft im Schnitt).
    ''' Sport ist bewusst mit zwei Lehrkraeften bemannt (passend zum
    ''' realen Sport-/Bewegungsprofil der AFS), deren Deputate zusammen
    ''' die Sport-Gesamtnachfrage exakt treffen.</summary>
    Public Function BuildAFSFellbachGrundschule() As Stammdatenbestand
        Dim b As New Stammdatenbestand With {
            .SchulName = "Anne-Frank-Schule Fellbach (synthetisch, an der realen AFS orientiert)",
            .Bundesland = "BW", .Schulart = "Grundschule",
            .Tage = New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}, .PeriodsPerDay = 6
        }
        For ks = 1 To 4
            b.Klassenstufen.Add(New Klassenstufe With {.Nummer = ks, .Bezeichnung = $"Klasse {ks}"})
            AddKlassen(b, ks, {"a", "b", "c"})
        Next

        AddFachKlassenstufen(b, "Deutsch", {1, 2}, 6, maxProTag:=2)
        AddFachKlassenstufen(b, "Deutsch", {3, 4}, 5, maxProTag:=2)
        AddFachKlassenstufen(b, "Mathematik", {1, 2, 3, 4}, 5, maxProTag:=2)
        AddFachKlassenstufen(b, "Sachunterricht", {1, 2, 3, 4}, 3, maxProTag:=2)
        AddFachKlassenstufen(b, "Sport", {1, 2, 3, 4}, 3, maxProTag:=1)
        AddFachKlassenstufen(b, "Musik", {1, 2, 3, 4}, 2, maxProTag:=1)
        AddFachKlassenstufen(b, "Kunst", {1, 2, 3, 4}, 2, maxProTag:=1)
        AddFachKlassenstufen(b, "Religion", {1, 2, 3, 4}, 2, maxProTag:=1)
        AddFachKlassenstufen(b, "Englisch", {3, 4}, 2, maxProTag:=1)

        b.Raeume.Add(New Raum With {.Name = "Turnhalle1", .Typ = "Turnhalle"})
        b.Raeume.Add(New Raum With {.Name = "Turnhalle2", .Typ = "Turnhalle"})

        AddLehrerPool(b, "Klassenlehrer", 6, 28, {"Deutsch", "Mathematik", "Sachunterricht"}, klassenlehrerFaehig:=True)
        AddLehrerPool(b, "Sportlehrer", 2, 18, {"Sport"}, klassenlehrerFaehig:=False)
        AddLehrerPool(b, "Musiklehrer", 1, 24, {"Musik"}, klassenlehrerFaehig:=False)
        AddLehrerPool(b, "Kunstlehrer", 1, 24, {"Kunst"}, klassenlehrerFaehig:=False)
        AddLehrerPool(b, "Religionslehrer", 1, 24, {"Religion"}, klassenlehrerFaehig:=False)
        AddLehrerPool(b, "Englischlehrer", 1, 12, {"Englisch"}, klassenlehrerFaehig:=False)

        Return b
    End Function

End Module
