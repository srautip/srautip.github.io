' Phase 2.15f: zwei konkrete, synthetische Referenz-Stammdatensaetze fuer
' Baden-Wuerttemberg (Stufe 1 der Nutzeranfrage) - eine kleine Grundschule
' (Kl.1-4) und eine kleine Gemeinschaftsschule ("kleine Gesamtschule",
' Kl.5-10), je 2-zuegig. Wie bei GymnasiumSekIFixture.vb/
' GrundschuleGrossFixture.vb (Phase 2.10) ausdruecklich SYNTHETISCHE,
' PLAUSIBLE Testdaten - kein Anspruch auf reale, aktuelle Schuldaten.
'
' Faecher/Wochenstunden fuer die Grundschule sind bewusst identisch zu den
' bereits in GrundschuleGrossFixture.vb (Phase 2.10) verwendeten, bereits
' als loesbar bestaetigten Werten uebernommen (Deutsch/Mathematik/
' Sachunterricht/Sport/Musik/Kunst/Religion, Englisch ab Kl.3) - reduziert
' das Risiko einer neu erfundenen, unloesbaren Kombination.
'
' Recherche-Grundlage (Ministerium fuer Kultus, Jugend und Sport
' Baden-Wuerttemberg, siehe docs/phase2-15-lehrereinsatzplanung.md fuer
' Quellenangaben):
' - Grundschule: Englisch seit Schuljahr 2018/19 ab Klassenstufe 3 (davor
'   ab Kl.1); Musik/Kunst-Werken laut Bildungsplan 2016 gemeinsam mit 13
'   Kontingentstunden ueber die 4 Jahre ausgewiesen, hier vereinfacht als
'   zwei separate Faecher mit je eigener Wochenstundenzahl gefuehrt (die
'   Kontingentstundentafel selbst legt nur die Gesamtstundenzahl fest, die
'   Schule verteilt selbst - siehe Stammdaten.vb's FachKlassenstufe-
'   Kopfkommentar). Deputat Grundschullehrkraft: 28 Wochenstunden.
' - Gemeinschaftsschule: verpflichtende Ganztagsschule Kl.5-10; Englisch ab
'   Kl.5 fuer alle; BNT (Biologie, Naturphaenomene und Technik) als
'   Faecherverbund Kl.5-6, danach eigenstaendige Biologie ab Kl.7; Physik
'   ab Kl.7, Chemie/Gemeinschaftskunde ab Kl.8 (vereinfacht - die reale
'   Kontingentstundentafel erlaubt Kl.7 oder Kl.8, hier einheitlich Kl.8
'   gewaehlt); der Wahlpflichtbereich (Technik/AES vs. 2. Fremdsprache ab
'   Kl.6) sowie die Niveaustufen-Differenzierung (G/M/E) sind fuer das
'   Lehrer-Klasse-Fach-Zuordnungsmodell nicht strukturrelevant (die
'   Differenzierung passiert BINNEN der Klasse) und deshalb hier bewusst
'   nicht nachgebildet. Deputat Gemeinschaftsschullehrkraft: 27
'   Wochenstunden.
Public Module StammdatenBWFixture

    Private Sub AddKlassen(bestand As Stammdatenbestand, klassenstufe As Integer, buchstaben As IEnumerable(Of String))
        For Each buchstabe In buchstaben
            bestand.Klassen.Add(New Klasse With {.Name = $"{klassenstufe}{buchstabe}", .Klassenstufe = klassenstufe, .Schuelerzahl = 22})
        Next
    End Sub

    Private Function GetOrAddFach(bestand As Stammdatenbestand, name As String, blockLength As Integer?) As Fach
        Dim existing = bestand.Faecher.FirstOrDefault(Function(f) f.Name = name)
        If existing IsNot Nothing Then Return existing
        Dim fach As New Fach With {.Name = name, .BlockLength = blockLength}
        bestand.Faecher.Add(fach)
        Return fach
    End Function

    Private Sub AddFachKlassenstufen(bestand As Stammdatenbestand, name As String, klassenstufen As IEnumerable(Of Integer),
                                      wochenstunden As Integer, Optional maxProTag As Integer? = Nothing, Optional blockLength As Integer? = Nothing)
        Dim fach = GetOrAddFach(bestand, name, blockLength)
        For Each ks In klassenstufen
            fach.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = ks, .WochenstundenSoll = wochenstunden, .MaxProTag = maxProTag})
        Next
    End Sub

    Private Sub AddLehrerPool(bestand As Stammdatenbestand, namePrefix As String, anzahl As Integer, deputat As Double,
                               faecher As IEnumerable(Of String), klassenlehrerFaehig As Boolean,
                               Optional anrechnungsstunden As Double = 0)
        For i = 1 To anzahl
            Dim name = $"{namePrefix}-{i}"
            bestand.Lehrkraefte.Add(New Lehrer With {.Name = name, .DeputatSollstunden = deputat, .KlassenlehrerFaehig = klassenlehrerFaehig, .Anrechnungsstunden = anrechnungsstunden})
            For Each f In faecher
                bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = name, .FachName = f})
            Next
        Next
    End Sub

    ''' <summary>Kl.1-4, 2-zuegig (8 Klassen). Klassenlehrer-Prinzip: EINE
    ''' VOLLZEIT-Klassenlehrkraft PRO Klasse (8 statt einer kleineren, sich
    ''' Klassen teilenden Gruppe - ein Klassenlehrer hat ueblicherweise nur
    ''' eine Klasse, siehe Phase-2.16-Nachtrag-3), Deputat 28h Vollzeit
    ''' (Phase-2.16-Nachtrag-4: realistischer als die zuvor verwendete
    ''' haelftige Teilzeit) minus 2h Klassenleitungs-Anrechnungsstunde
    ''' (reale BW-Praxis). Faecherumfang erweitert auf Deutsch/Mathematik/
    ''' Sachunterricht PLUS Sport/Musik/Kunst der eigenen Klasse (reales
    ''' Klassenlehrerprinzip: eine Lehrkraft deckt fast alle Faecher der
    ''' eigenen Klasse ab). Religion (konfessionsgebunden, klassen-
    ''' uebergreifend organisiert) und Englisch (eigene Qualifikation)
    ''' bleiben bei dedizierten Fachlehrkraeften. Selbst bei voller
    ''' Uebernahme aller sechs Faecher erreicht eine Klasse strukturell nur
    ''' ~20-21h/Woche (weniger als das volle 28h-Deputat, da eine
    ''' Grundschulklasse nicht mehr Wochenstunden hat) - die verbleibende
    ''' Deputat-Luecke bleibt bewusst als ehrliche Rest-Abweichung im
    ''' Lehrereinsatzplanung-Objective sichtbar (analog zur bereits
    ''' dokumentierten BW-Gemeinschaftsschule-Grenze), statt sie ueber ein
    ''' erweitertes Toleranzband wegzuoptimieren - siehe
    ''' docs/phase2-15-lehrereinsatzplanung.md, Nachtrag 4.</summary>
    Public Function BuildBWGrundschule() As Stammdatenbestand
        Dim b As New Stammdatenbestand With {
            .SchulName = "Beispiel-Grundschule (synthetisch)", .Bundesland = "BW", .Schulart = "Grundschule",
            .Tage = New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}, .PeriodsPerDay = 6
        }
        For ks = 1 To 4
            b.Klassenstufen.Add(New Klassenstufe With {.Nummer = ks, .Bezeichnung = $"Klasse {ks}"})
            AddKlassen(b, ks, {"a", "b"})
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

        AddLehrerPool(b, "Klassenlehrer", 8, 28, {"Deutsch", "Mathematik", "Sachunterricht", "Sport", "Musik", "Kunst"}, klassenlehrerFaehig:=True, anrechnungsstunden:=2)
        AddLehrerPool(b, "Religionslehrer", 1, 16, {"Religion"}, klassenlehrerFaehig:=False)
        AddLehrerPool(b, "Englischlehrer", 1, 8, {"Englisch"}, klassenlehrerFaehig:=False)

        Return b
    End Function

    ''' <summary>Kl.5-10, 2-zuegig (12 Klassen). Lehrkraefte in
    ''' realistischen Zwei-Fach-Kombinationen gepoolt (deutsches
    ''' Lehramtsstudium: Zwei-Faecher-Prinzip), Deputat 27h passend zur
    ''' jeweiligen Fachkombinations-Gesamtnachfrage bemessen.</summary>
    Public Function BuildBWGemeinschaftsschule() As Stammdatenbestand
        Dim b As New Stammdatenbestand With {
            .SchulName = "Beispiel-Gemeinschaftsschule (synthetisch)", .Bundesland = "BW", .Schulart = "Gemeinschaftsschule",
            .Tage = New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}, .PeriodsPerDay = 8
        }
        For ks = 5 To 10
            b.Klassenstufen.Add(New Klassenstufe With {.Nummer = ks, .Bezeichnung = $"Klasse {ks}"})
            AddKlassen(b, ks, {"a", "b"})
        Next

        Dim alle = {5, 6, 7, 8, 9, 10}
        AddFachKlassenstufen(b, "Deutsch", alle, 4, maxProTag:=2)
        AddFachKlassenstufen(b, "Mathematik", alle, 4, maxProTag:=2)
        AddFachKlassenstufen(b, "Englisch", alle, 4, maxProTag:=2)
        AddFachKlassenstufen(b, "BNT", {5, 6}, 2, maxProTag:=1)
        AddFachKlassenstufen(b, "Biologie", {7, 8, 9, 10}, 2, maxProTag:=1)
        AddFachKlassenstufen(b, "Physik", {7, 8, 9, 10}, 2, maxProTag:=2, blockLength:=2)
        AddFachKlassenstufen(b, "Chemie", {8, 9, 10}, 2, maxProTag:=2, blockLength:=2)
        AddFachKlassenstufen(b, "Gemeinschaftskunde", {8, 9, 10}, 2, maxProTag:=1)
        AddFachKlassenstufen(b, "Erdkunde", alle, 2, maxProTag:=1)
        AddFachKlassenstufen(b, "Geschichte", {6, 7, 8, 9, 10}, 2, maxProTag:=1)
        AddFachKlassenstufen(b, "Sport", alle, 3, maxProTag:=1)
        AddFachKlassenstufen(b, "Musik", {5, 6, 7, 8}, 2, maxProTag:=1)
        AddFachKlassenstufen(b, "Kunst", {5, 6, 7, 8}, 2, maxProTag:=1)
        AddFachKlassenstufen(b, "Religion", alle, 2, maxProTag:=1)

        ' Poolgroessen so bemessen, dass Kapazitaet (Anzahl x 27h) je Fach-
        ' kombination moeglichst nah an der tatsaechlichen Wochenstunden-
        ' nachfrage liegt (siehe docs/phase2-15-lehrereinsatzplanung.md) -
        ' zu grosszuegige Pools bestrafen sich im Deputat-Korridor selbst
        ' (Unterdeckung ist genauso eine Abweichung wie Ueberdeckung).
        AddLehrerPool(b, "Deutsch-Geschichte-Lehrer", 3, 27, {"Deutsch", "Geschichte", "Gemeinschaftskunde"}, klassenlehrerFaehig:=True)
        AddLehrerPool(b, "Mathematik-Physik-Lehrer", 2, 27, {"Mathematik", "Physik"}, klassenlehrerFaehig:=True)
        AddLehrerPool(b, "Englisch-Erdkunde-Lehrer", 3, 27, {"Englisch", "Erdkunde"}, klassenlehrerFaehig:=True)
        AddLehrerPool(b, "NaWi-Lehrer", 1, 27, {"Biologie", "Chemie", "BNT"}, klassenlehrerFaehig:=False)
        AddLehrerPool(b, "Sportlehrer", 1, 27, {"Sport"}, klassenlehrerFaehig:=False)
        AddLehrerPool(b, "Musik-Kunst-Lehrer", 1, 27, {"Musik", "Kunst"}, klassenlehrerFaehig:=False)
        AddLehrerPool(b, "Religionslehrer", 1, 24, {"Religion"}, klassenlehrerFaehig:=False)

        Return b
    End Function

End Module
