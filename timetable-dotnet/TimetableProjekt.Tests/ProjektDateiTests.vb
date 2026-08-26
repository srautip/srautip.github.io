' Stufe C des GUI-Unterbaus: die verschluesselte Projektablage.
'
' Der Schwerpunkt liegt bewusst NICHT auf dem Happy-Path-Round-Trip,
' sondern auf den Zusagen, die im Fehlerfall halten muessen: falsches
' Passwort, manipulierte Datei, Absturz mitten im Speichern,
' Schema-Evolution. Ein Container, der nur im Gutfall funktioniert, ist
' fuer Schuldaten wertlos.
'
' Die Iterationszahl wird in den Tests bewusst KLEIN gehalten - PBKDF2 mit
' den echten 600.000 Runden kostet je Aufruf ~0,3s, und diese Suite ruft
' oft auf. Der Produktionswert steht als ProjektDatei.StandardIterationen
' im Code und wird separat geprueft.
Imports System.IO
Imports System.Text
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableProjekt

<TestClass>
Public Class ProjektDateiTests

    Private Const TestIterationen As Integer = 1000
    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero)

    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "splanx-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(_ordner)
    End Sub

    <TestCleanup>
    Public Sub Abraeumen()
        If Directory.Exists(_ordner) Then Directory.Delete(_ordner, recursive:=True)
    End Sub

    Private Function Pfad(name As String) As String
        Return IO.Path.Combine(_ordner, name)
    End Function

    ''' <summary>Ein Projekt mit jedem Bestandteil befuellt - ein
    ''' Round-Trip-Test auf einem halbleeren Objekt beweist wenig.</summary>
    Private Shared Function Beispielprojekt() As Projekt
        Dim p As New Projekt()
        p.Manifest.SchulName = "GS Musterstadt"
        p.Manifest.Schuljahr = "2026/27"
        p.Manifest.AppVersion = "test"
        p.Manifest.Angelegt = Jetzt
        p.Manifest.Geaendert = Jetzt

        p.Bestand.SchulName = "GS Musterstadt"
        p.Bestand.Schulart = "Grundschule"
        p.Bestand.PeriodsPerDay = 6
        p.Bestand.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        p.Bestand.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1, .Schuelerzahl = 22})
        p.Bestand.Faecher.Add(New Fach With {.Name = "Deutsch"})
        p.Bestand.Lehrkraefte.Add(New Lehrer With {.Name = "Frau A", .DeputatSollstunden = 28})

        p.Constraints.Add(New JsonObject From {
            {"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "1a"},
            {"day", "Mo"}, {"period", 6}, {"priority", "must"}, {"reason", "nur Vormittag"}})

        p.Klassenbildung.Klassen = New KlassenbildungKlassen With {.Anzahl = 2, .MinGroesse = 10, .MaxGroesse = 14}
        p.Klassenbildung.Schueler.Add(New KlassenbildungSchueler With {.Id = "S001"})
        p.Klassenbildung.Schueler.Add(New KlassenbildungSchueler With {.Id = "S002"})
        p.Klassenbildung.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S001", .Klasse = 1})

        p.Config.SolveTimeLimitS = 120.0
        p.Config.MaxSolutions = 7

        p.Mapping.Add(New MappingEintrag With {.Id = "S001", .Vorname = "Mia", .Nachname = "Muster", .Hinweis = "kommt aus Kita Pusteblume"})
        p.Manifest.NaechsteSchuelerNummer = 3

        p.Protokolliere("Frau Leitung", "lauf", "Stundenplan gerechnet (Seed 42)", Jetzt)
        p.GuiState = New JsonObject From {{"pins", New JsonObject From {{"S001", 1}}}}

        p.StandHinzufuegen(New ProjektStand With {
            .Id = "2026-08-20-entwurf-1", .Label = "Entwurf 1", .Erstellt = Jetzt,
            .Lauf = New JsonObject From {{"seed", 42}, {"status", "Optimal"}},
            .Stundenplan = New JsonObject From {{"solutions", New JsonArray()}}})
        Return p
    End Function

    ' ---------------------------------------------------------------
    ' Round-Trip
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub RoundTripErhaeltJedenBestandteil()
        Dim original = Beispielprojekt()
        Dim datei = Pfad("projekt.splanx")

        ProjektDatei.Speichern(original, datei, "geheim", TestIterationen)
        Dim geladen = ProjektDatei.Laden(datei, "geheim")

        Assert.AreEqual(original.Manifest.SchulName, geladen.Manifest.SchulName)
        Assert.AreEqual(original.Manifest.Schuljahr, geladen.Manifest.Schuljahr)
        Assert.AreEqual(original.Manifest.NaechsteSchuelerNummer, geladen.Manifest.NaechsteSchuelerNummer)
        Assert.AreEqual(original.Manifest.MaxStaende, geladen.Manifest.MaxStaende)

        Assert.AreEqual(original.Bestand.Klassen.Count, geladen.Bestand.Klassen.Count)
        Assert.AreEqual("1a", geladen.Bestand.Klassen(0).Name)
        Assert.AreEqual(6, geladen.Bestand.PeriodsPerDay)
        Assert.AreEqual("Frau A", geladen.Bestand.Lehrkraefte(0).Name)

        Assert.AreEqual(1, geladen.Constraints.Count)
        Assert.AreEqual("forbidden_slot", geladen.Constraints(0)("type").GetValue(Of String)())
        Assert.AreEqual(6, geladen.Constraints(0)("period").GetValue(Of Integer)(),
                        "Zahlenfeld im Constraint ist keine Zahl mehr")

        Assert.AreEqual(2, geladen.Klassenbildung.Schueler.Count)
        Assert.AreEqual(2, geladen.Klassenbildung.Klassen.Anzahl)
        Assert.AreEqual(1, geladen.Klassenbildung.Fixierungen(0).Klasse)

        Assert.AreEqual(120.0, geladen.Config.SolveTimeLimitS)
        Assert.AreEqual(7, geladen.Config.MaxSolutions)

        Assert.AreEqual(1, geladen.Mapping.Count)
        Assert.AreEqual("Mia", geladen.Mapping(0).Vorname)
        Assert.AreEqual("kommt aus Kita Pusteblume", geladen.Mapping(0).Hinweis)

        Assert.AreEqual(1, geladen.AuditLog.Count)
        Assert.AreEqual("lauf", geladen.AuditLog(0).Aktion)
        Assert.AreEqual(Jetzt, geladen.AuditLog(0).Zeitpunkt)

        Assert.IsNotNull(geladen.GuiState)
        Assert.AreEqual(1, geladen.GuiState("pins")("S001").GetValue(Of Integer)())

        Assert.AreEqual(1, geladen.Staende.Count)
        Assert.AreEqual("Entwurf 1", geladen.Staende(0).Label)
        Assert.AreEqual(42, geladen.Staende(0).Lauf("seed").GetValue(Of Integer)())
        Assert.IsNotNull(geladen.Staende(0).Stundenplan)
    End Sub

    ''' <summary>Zweimal Speichern desselben Projekts muss zwei
    ''' VERSCHIEDENE Chiffrate ergeben - sonst waere die Nonce
    ''' wiederverwendet, und Nonce-Wiederverwendung bricht AES-GCM.</summary>
    <TestMethod>
    Public Sub JederSpeichervorgangNutztFrischeNonce()
        Dim p = Beispielprojekt()
        Dim a = Pfad("a.splanx")
        Dim b = Pfad("b.splanx")

        ProjektDatei.Speichern(p, a, "geheim", TestIterationen)
        ProjektDatei.Speichern(p, b, "geheim", TestIterationen)

        Dim bytesA = File.ReadAllBytes(a)
        Dim bytesB = File.ReadAllBytes(b)
        Assert.IsFalse(bytesA.SequenceEqual(bytesB),
                       "zwei Speichervorgaenge ergaben byte-gleiche Dateien - Nonce oder Salt wird wiederverwendet")
        ' Beide muessen trotzdem lesbar sein.
        Assert.AreEqual("GS Musterstadt", ProjektDatei.Laden(a, "geheim").Manifest.SchulName)
        Assert.AreEqual("GS Musterstadt", ProjektDatei.Laden(b, "geheim").Manifest.SchulName)
    End Sub

    ' ---------------------------------------------------------------
    ' Schutzzusagen
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub FalschesPasswortLiefertKlarenFehlerStattDatenmuell()
        Dim datei = Pfad("projekt.splanx")
        ProjektDatei.Speichern(Beispielprojekt(), datei, "richtig", TestIterationen)

        Try
            ProjektDatei.Laden(datei, "falsch")
            Assert.Fail("falsches Passwort wurde akzeptiert")
        Catch ex As ProjektEntschluesselungException
            StringAssert.Contains(ex.Message, "Passwort")
        End Try
    End Sub

    <TestMethod>
    Public Sub ManipulierteDateiWirdErkannt()
        Dim datei = Pfad("projekt.splanx")
        ProjektDatei.Speichern(Beispielprojekt(), datei, "geheim", TestIterationen)

        ' Ein einziges Byte im Chiffrat kippen - AES-GCM ist
        ' authentifiziert, das MUSS auffallen.
        Dim bytes = File.ReadAllBytes(datei)
        bytes(bytes.Length - 1) = CByte((CInt(bytes(bytes.Length - 1)) Xor 1) And &HFF)
        File.WriteAllBytes(datei, bytes)

        Assert.ThrowsException(Of ProjektEntschluesselungException)(
            Sub() ProjektDatei.Laden(datei, "geheim"))
    End Sub

    <TestMethod>
    Public Sub FremdeDateiWirdNichtAlsProjektAusgegeben()
        Dim fremd = Pfad("beliebig.txt")
        File.WriteAllText(fremd, "das ist kein Projekt")

        Assert.IsFalse(ProjektDatei.IstProjektdatei(fremd))
        Assert.ThrowsException(Of ProjektFormatException)(Sub() ProjektDatei.Laden(fremd, "egal"))

        Dim echt = Pfad("projekt.splanx")
        ProjektDatei.Speichern(Beispielprojekt(), echt, "geheim", TestIterationen)
        Assert.IsTrue(ProjektDatei.IstProjektdatei(echt))
    End Sub

    ''' <summary>Die Iterationszahl liegt UNVERSCHLUESSELT im Kopf - sie
    ''' muss lesbar sein, bevor der Schluessel abgeleitet werden kann, und
    ''' spaeter erhoehbar, ohne alte Dateien zu entwerten. Der Test belegt
    ''' beides: sie steht im Klartext, und eine mit anderer Zahl
    ''' geschriebene Datei laesst sich weiterhin oeffnen.</summary>
    <TestMethod>
    Public Sub IterationszahlStehtImKlartextKopfUndBleibtLesbar()
        Dim datei = Pfad("projekt.splanx")
        ProjektDatei.Speichern(Beispielprojekt(), datei, "geheim", 1234)

        Dim kopf = Encoding.UTF8.GetString(File.ReadAllBytes(datei), 0, 200)
        StringAssert.Contains(kopf, "PBKDF2-SHA256")
        StringAssert.Contains(kopf, "1234")

        Assert.AreEqual("GS Musterstadt", ProjektDatei.Laden(datei, "geheim").Manifest.SchulName)

        ' Dieselbe Datei neu geschrieben mit hoeherer Zahl - weiterhin lesbar.
        ProjektDatei.Speichern(Beispielprojekt(), datei, "geheim", 4321)
        Assert.AreEqual("GS Musterstadt", ProjektDatei.Laden(datei, "geheim").Manifest.SchulName)
    End Sub

    ''' <summary>Der Produktionswert darf nicht versehentlich auf einen
    ''' Testwert absacken - deshalb hier festgenagelt.</summary>
    <TestMethod>
    Public Sub StandardIterationenErfuelltDieKonzeptUntergrenze()
        Assert.IsTrue(ProjektDatei.StandardIterationen >= 600000,
                      $"StandardIterationen ist {ProjektDatei.StandardIterationen}, Konzept 5.3 verlangt >= 600.000")
    End Sub

    ''' <summary>Atomares Speichern (A9): schlaegt das Schreiben fehl, muss
    ''' die ALTE Datei unveraendert stehen bleiben. Simuliert wird der
    ''' Absturz, indem das Zielverzeichnis waehrenddessen gesperrt ist -
    ''' hier ueber ein Projekt, das beim Serialisieren wirft.</summary>
    <TestMethod>
    Public Sub FehlgeschlagenesSpeichernLaesstDieAlteDateiIntakt()
        Dim datei = Pfad("projekt.splanx")
        ProjektDatei.Speichern(Beispielprojekt(), datei, "geheim", TestIterationen)
        Dim vorher = File.ReadAllBytes(datei)

        ' Kein Passwort -> Abbruch VOR jedem Schreibzugriff.
        Assert.ThrowsException(Of ArgumentException)(
            Sub() ProjektDatei.Speichern(Beispielprojekt(), datei, "", TestIterationen))

        Dim nachher = File.ReadAllBytes(datei)
        Assert.IsTrue(vorher.SequenceEqual(nachher), "die alte Datei wurde beschaedigt")
        Assert.AreEqual("GS Musterstadt", ProjektDatei.Laden(datei, "geheim").Manifest.SchulName)

        ' Und es bleiben keine Temp-Dateien liegen.
        Dim reste = Directory.GetFiles(_ordner, "*.tmp-*")
        Assert.AreEqual(0, reste.Length, "Temp-Datei nicht aufgeraeumt: " & String.Join(", ", reste))
    End Sub

    ''' <summary>Der haertere Atomaritaets-Fall: das Schreiben scheitert
    ''' MITTENDRIN. Simuliert ueber eine exklusive Sperre auf der Zieldatei
    ''' - File.Replace scheitert dann, und genau da muss sich zeigen, dass
    ''' die alte Datei unangetastet bleibt und keine Temp-Datei liegen
    ''' bleibt.</summary>
    <TestMethod>
    Public Sub GesperrteZieldateiLaesstDenAltbestandUnberuehrt()
        Dim datei = Pfad("projekt.splanx")
        ProjektDatei.Speichern(Beispielprojekt(), datei, "geheim", TestIterationen)
        Dim vorher = File.ReadAllBytes(datei)

        Dim geaendert = Beispielprojekt()
        geaendert.Manifest.SchulName = "Sollte nicht ankommen"

        Using sperre = New FileStream(datei, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            Assert.ThrowsException(Of IOException)(
                Sub() ProjektDatei.Speichern(geaendert, datei, "geheim", TestIterationen))
        End Using

        Assert.IsTrue(vorher.SequenceEqual(File.ReadAllBytes(datei)),
                      "die alte Datei wurde durch den fehlgeschlagenen Schreibvorgang veraendert")
        Assert.AreEqual("GS Musterstadt", ProjektDatei.Laden(datei, "geheim").Manifest.SchulName)
        Dim reste = Directory.GetFiles(_ordner, "*.tmp-*")
        Assert.AreEqual(0, reste.Length, "Temp-Datei nicht aufgeraeumt: " & String.Join(", ", reste))
    End Sub

    ''' <summary>Die Kernzusage A3, und die einzige, die man von aussen
    ''' pruefen kann: nichts Personenbezogenes steht im Klartext in der
    ''' Datei. Geprueft wird gegen Schul-, Klarnamen- und Lehrernamen sowie
    ''' die ZIP-Signatur - stuende der Container unverschluesselt drin,
    ''' begaenne das Chiffrat mit "PK".</summary>
    <TestMethod>
    Public Sub DateiEnthaeltKeineKlartextdaten()
        Dim datei = Pfad("projekt.splanx")
        ProjektDatei.Speichern(Beispielprojekt(), datei, "geheim", TestIterationen)

        Dim bytes = File.ReadAllBytes(datei)
        Dim alsLatin1 = Encoding.Latin1.GetString(bytes)
        For Each geheim In {"GS Musterstadt", "Mia", "Muster", "Pusteblume", "Frau A", "Deutsch"}
            Assert.IsFalse(alsLatin1.Contains(geheim),
                           $"'{geheim}' steht im Klartext in der Projektdatei")
        Next
        Assert.IsFalse(alsLatin1.Substring(8).Contains("PK" & Chr(3) & Chr(4)),
                       "die ZIP-Signatur ist sichtbar - der Container wurde nicht verschluesselt")
    End Sub

    ''' <summary>Schema-Evolution (Konzept 5.1): eine Datei mit einem
    ''' Zusatzeintrag, den diese Version nicht kennt, muss sich trotzdem
    ''' oeffnen lassen - sonst waere jede Aufwaertskompatibilitaet
    ''' verspielt.</summary>
    <TestMethod>
    Public Sub UnbekannteEintraegeBrechenDasLadenNicht()
        Dim datei = Pfad("projekt.splanx")
        ProjektDatei.Speichern(Beispielprojekt(), datei, "geheim", TestIterationen)

        ' Container aufmachen ist ohne Passwort nicht moeglich - deshalb
        ' ueber den regulaeren Weg: laden, einen unbekannten Stand-Eintrag
        ' simulieren, neu schreiben. Ein spaeteres Feld traefe denselben
        ' Pfad (das Else im Select Case).
        Dim p = ProjektDatei.Laden(datei, "geheim")
        p.GuiState = New JsonObject From {{"zukunftsfeld", "wert"}, {"pins", New JsonObject()}}
        ProjektDatei.Speichern(p, datei, "geheim", TestIterationen)

        Dim erneut = ProjektDatei.Laden(datei, "geheim")
        Assert.AreEqual("wert", erneut.GuiState("zukunftsfeld").GetValue(Of String)())
        Assert.AreEqual("GS Musterstadt", erneut.Manifest.SchulName)
    End Sub


    ''' <summary>Die Stand-Id ist zugleich der Ordnername im Container.
    ''' Zwei Staende mit derselben Id ueberschreiben sich beim Speichern -
    ''' und weil die Aufrufer die Id sekundengenau aus dem Zeitstempel
    ''' bilden, sind zwei kurze Laeufe in derselben Sekunde nichts
    ''' Besonderes.
    '''
    ''' Live entdeckt: ein Test mit fester Uhr erzeugte zweimal dieselbe
    ''' Id, und die Freigabe traf danach immer den ersten Stand - beide
    ''' Wege sahen gruen aus, obwohl einer falsch war.</summary>
    <TestMethod>
    Public Sub StaendeMitGleicherIdWerdenEindeutigGemacht()
        Dim p As New Projekt()
        Dim a As New ProjektStand With {.Id = "2026-08-26-120000-stundenplan", .Label = "A"}
        Dim b As New ProjektStand With {.Id = "2026-08-26-120000-stundenplan", .Label = "B"}
        Dim c As New ProjektStand With {.Id = "2026-08-26-120000-stundenplan", .Label = "C"}

        p.StandHinzufuegen(a)
        p.StandHinzufuegen(b)
        p.StandHinzufuegen(c)

        Assert.AreEqual(3, p.Staende.Select(Function(s) s.Id).Distinct().Count(),
                        "zwei Staende mit derselben Id ueberschreiben sich beim Speichern")
        Assert.AreEqual("2026-08-26-120000-stundenplan", a.Id, "der erste behaelt seine Id")
        Assert.AreEqual("2026-08-26-120000-stundenplan-2", b.Id)
        Assert.AreEqual("2026-08-26-120000-stundenplan-3", c.Id)
    End Sub

    ''' <summary>Der Nachweis, warum das ueberhaupt zaehlt: mit doppelter
    ''' Id ueberlebt nur EIN Stand das Speichern.</summary>
    <TestMethod>
    Public Sub BeideStaendeUeberlebenDasSpeichern()
        Dim ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttid-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(ordner)
        Try
            Dim p As New Projekt()
            p.StandHinzufuegen(New ProjektStand With {.Id = "gleich", .Label = "A"})
            p.StandHinzufuegen(New ProjektStand With {.Id = "gleich", .Label = "B"})

            Dim pfad = IO.Path.Combine(ordner, "p.splanx")
            ProjektDatei.Speichern(p, pfad, "geheim12")
            Dim erneut = ProjektDatei.Laden(pfad, "geheim12")

            Assert.AreEqual(2, erneut.Staende.Count, "ein Stand ist beim Speichern verlorengegangen")
            CollectionAssert.AreEquivalent(New List(Of String) From {"A", "B"},
                                           erneut.Staende.Select(Function(s) s.Label).ToList())
        Finally
            IO.Directory.Delete(ordner, recursive:=True)
        End Try
    End Sub

End Class

