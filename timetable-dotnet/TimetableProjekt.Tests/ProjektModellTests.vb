' Die Zusagen des Datenhaltungskonzepts, die NICHT im Container stecken,
' sondern im Verhalten des Modells: verbrannte Ids, geschuetzte Staende,
' append-only-Audit-Log, Loeschung nach Art. 17, und der Round-Trip in das
' vorhandene tests/<schule>/-Ordnerlayout.
Imports System.IO
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableProjekt

<TestClass>
Public Class ProjektModellTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.Zero)

    Private Shared Function Stand(id As String, minuten As Integer, Optional geschuetzt As Boolean = False) As ProjektStand
        Return New ProjektStand With {.Id = id, .Label = id, .Erstellt = Jetzt.AddMinutes(minuten), .Geschuetzt = geschuetzt}
    End Function

    ' ---------------------------------------------------------------
    ' Pseudonym-Ids
    ' ---------------------------------------------------------------

    ''' <summary>Konzept 6.1: "eine geloeschte ID bleibt verbrannt, damit
    ''' alte Audit-Log-Eintraege nicht auf ein anderes Kind zeigen
    ''' koennen." Das ist der Test, der genau das festhaelt.</summary>
    <TestMethod>
    Public Sub GeloeschteSchuelerIdWirdNieWiederVergeben()
        Dim p As New Projekt()
        Dim a = p.NeueSchuelerId()
        Dim b = p.NeueSchuelerId()
        Assert.AreEqual("S001", a)
        Assert.AreEqual("S002", b)

        p.Mapping.Add(New MappingEintrag With {.Id = b, .Vorname = "Max", .Nachname = "M"})
        p.SchuelerLoeschen(b)

        Dim c = p.NeueSchuelerId()
        Assert.AreEqual("S003", c, "eine verbrannte Id wurde erneut vergeben")
        Assert.AreNotEqual(b, c)
    End Sub

    ''' <summary>Art. 17: Loeschen entfernt das Kind aus JEDER Struktur,
    ''' nicht nur aus der Namensliste - sonst bliebe es ueber die
    ''' Gruppenmitgliedschaft weiter erkennbar.</summary>
    <TestMethod>
    Public Sub SchuelerLoeschenRaeumtAlleMitgliedschaftenAus()
        Dim p As New Projekt()
        p.Mapping.Add(New MappingEintrag With {.Id = "S001", .Vorname = "Mia", .Nachname = "Muster"})
        p.Klassenbildung.Schueler.Add(New KlassenbildungSchueler With {.Id = "S001"})
        p.Klassenbildung.Schueler.Add(New KlassenbildungSchueler With {.Id = "S002"})
        p.Klassenbildung.Gruppen.Add(New KlassenbildungGruppe With {
            .Id = "G1", .Typ = "buendelung", .Mitglieder = New List(Of String) From {"S001", "S002"}})
        p.Klassenbildung.Wuensche.Add(New KlassenbildungWunsch With {
            .Typ = "zusammen", .Kinder = New List(Of String) From {"S001", "S002"}})
        p.Klassenbildung.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S001", .Klasse = 1})
        p.Bestand.Schueler.Add(New Schueler With {.Id = "S001", .Klasse = "1a"})
        p.Bestand.Gruppen.Add(New Gruppe With {
            .Name = "Reli-ev", .Typ = "fach", .MitgliederSchuelerIds = New List(Of String) From {"S001", "S002"}})

        p.SchuelerLoeschen("S001")

        Assert.AreEqual(0, p.Mapping.Count)
        Assert.IsFalse(p.Klassenbildung.Schueler.Any(Function(s) s.Id = "S001"))
        Assert.IsFalse(p.Klassenbildung.Gruppen(0).Mitglieder.Contains("S001"))
        Assert.IsFalse(p.Klassenbildung.Wuensche(0).Kinder.Contains("S001"))
        Assert.AreEqual(0, p.Klassenbildung.Fixierungen.Count)
        Assert.IsFalse(p.Bestand.Schueler.Any(Function(s) s.Id = "S001"))
        Assert.IsFalse(p.Bestand.Gruppen(0).MitgliederSchuelerIds.Contains("S001"))
        ' Das andere Kind bleibt unangetastet.
        Assert.IsTrue(p.Klassenbildung.Gruppen(0).Mitglieder.Contains("S002"))
    End Sub

    ''' <summary>Konzept 6.1: "'Mapping loeschen' entfernt nur mapping.json
    ''' und macht das Projekt dauerhaft pseudonym ... die Planung bleibt
    ''' vollstaendig funktionsfaehig."</summary>
    <TestMethod>
    Public Sub MappingLoeschenLaesstDiePlanungIntakt()
        Dim p As New Projekt()
        p.Klassenbildung.Schueler.Add(New KlassenbildungSchueler With {.Id = "S001"})
        p.Mapping.Add(New MappingEintrag With {.Id = "S001", .Vorname = "Mia", .Nachname = "Muster"})
        Assert.AreEqual("Mia Muster (S001)", p.Anzeigename("S001"))

        p.MappingLoeschen()

        Assert.AreEqual(0, p.Mapping.Count)
        Assert.AreEqual("S001", p.Anzeigename("S001"), "ohne Mapping muss die reine Id angezeigt werden")
        Assert.AreEqual(1, p.Klassenbildung.Schueler.Count, "die Planungsdaten wurden mitgeloescht")
    End Sub

    ''' <summary>Anonyme Platzhalter bekommen bewusst KEINEN
    ''' Mapping-Eintrag (Konzept 8.2) - die Anzeige muss damit umgehen.</summary>
    <TestMethod>
    Public Sub PlatzhalterOhneMappingZeigenIhreId()
        Dim p As New Projekt()
        Assert.AreEqual("S042", p.Anzeigename("S042"))
        p.Mapping.Add(New MappingEintrag With {.Id = "S042", .Vorname = "", .Nachname = ""})
        Assert.AreEqual("S042", p.Anzeigename("S042"), "leerer Mapping-Eintrag darf keinen leeren Namen anzeigen")
    End Sub

    ' ---------------------------------------------------------------
    ' Staende
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub AelteseStaendeWerdenVerdraengt()
        Dim p As New Projekt()
        p.Manifest.MaxStaende = 3

        For i = 1 To 5
            p.StandHinzufuegen(Stand($"stand-{i}", i))
        Next

        Assert.AreEqual(3, p.Staende.Count)
        CollectionAssert.AreEqual(New List(Of String) From {"stand-3", "stand-4", "stand-5"},
                                  p.Staende.Select(Function(s) s.Id).ToList())
    End Sub

    ''' <summary>Konzept 5.2/8.2: Freigabe- und Bestands-Staende sind gegen
    ''' automatisches Verdraengen geschuetzt. Der aelteste Stand ist hier
    ''' bewusst der geschuetzte - eine naive "aeltesten raus"-Regel wuerde
    ''' genau ihn treffen.</summary>
    <TestMethod>
    Public Sub GeschuetzteStaendeWerdenNichtVerdraengt()
        Dim p As New Projekt()
        p.Manifest.MaxStaende = 2
        p.StandHinzufuegen(Stand("bestand", 0, geschuetzt:=True))

        For i = 1 To 4
            p.StandHinzufuegen(Stand($"stand-{i}", i))
        Next

        Assert.IsTrue(p.Staende.Any(Function(s) s.Id = "bestand"), "der geschuetzte Stand wurde verdraengt")
        Assert.AreEqual(2, p.Staende.Count)
        Assert.AreEqual("stand-4", p.Staende.Last().Id)
    End Sub

    ''' <summary>Wenn nur noch geschuetzte Staende da sind, tritt die
    ''' Obergrenze zurueck - lieber ueber der Grenze liegen als eine
    ''' Freigabe wegwerfen.</summary>
    <TestMethod>
    Public Sub ObergrenzeTrittHinterFreigabenZurueck()
        Dim p As New Projekt()
        p.Manifest.MaxStaende = 1
        p.StandHinzufuegen(Stand("freigabe-a", 0, geschuetzt:=True))
        p.StandHinzufuegen(Stand("freigabe-b", 1, geschuetzt:=True))

        Assert.AreEqual(2, p.Staende.Count, "eine Freigabe wurde trotz Schutz entfernt")
    End Sub

    <TestMethod>
    Public Sub GeschuetzteStaendeLassenSichNichtLoeschen()
        Dim p As New Projekt()
        p.StandHinzufuegen(Stand("entwurf", 0))
        p.StandHinzufuegen(Stand("freigabe", 1, geschuetzt:=True))

        Assert.IsTrue(p.StandLoeschen("entwurf"))
        Assert.IsFalse(p.StandLoeschen("freigabe"), "ein Freigabe-Stand liess sich loeschen")
        Assert.IsFalse(p.StandLoeschen("gibt-es-nicht"))
        Assert.AreEqual(1, p.Staende.Count)
    End Sub

    ''' <summary>Konzept 7.3: "Laeufe-Loeschung entfernt Ergebnisdaten,
    ''' laesst aber die Log-Zeile stehen." Ohne das waere der
    ''' Art.-22-Nachweis luecken haft, sobald jemand aufraeumt.</summary>
    <TestMethod>
    Public Sub StandLoeschenLaesstDieAuditZeileStehen()
        Dim p As New Projekt()
        p.StandHinzufuegen(Stand("entwurf", 0))
        p.Protokolliere("Frau Leitung", "lauf", "Stand entwurf gerechnet", Jetzt)

        p.StandLoeschen("entwurf")

        Assert.AreEqual(0, p.Staende.Count)
        Assert.AreEqual(1, p.AuditLog.Count, "die Audit-Zeile wurde mitgeloescht")
        StringAssert.Contains(p.AuditLog(0).Beschreibung, "entwurf")
    End Sub

    ' ---------------------------------------------------------------
    ' Ordner-Round-Trip
    ' ---------------------------------------------------------------

    Private Shared ReadOnly Property TestsRoot As String
        Get
            Dim dir = New DirectoryInfo(AppContext.BaseDirectory)
            While dir IsNot Nothing
                Dim kandidat = IO.Path.Combine(dir.FullName, "tests", "bw-grundschule-beispiel")
                If Directory.Exists(kandidat) Then Return IO.Path.Combine(dir.FullName, "tests")
                dir = dir.Parent
            End While
            Throw New InvalidOperationException("tests/-Verzeichnis nicht gefunden")
        End Get
    End Property

    ''' <summary>Der Weg, den die unterjaehrige Einfuehrung nimmt: einen
    ''' vorhandenen Schulordner uebernehmen und spaeter wieder ausgeben
    ''' koennen, damit der CLI-Kanal vollwertig bleibt (Konzept 8.2, A6).</summary>
    <TestMethod>
    Public Sub SchulordnerImportUndExportSindVerlustfrei()
        Dim quelle = IO.Path.Combine(TestsRoot, "bw-grundschule-beispiel")
        Dim projekt = ProjektOrdner.Importieren(quelle, Jetzt)

        Assert.IsTrue(projekt.Bestand.Klassen.Count > 0, "keine Klassen importiert")
        Assert.IsTrue(projekt.Constraints.Count > 0, "keine Constraints importiert")
        Assert.IsTrue(projekt.Klassenbildung.Schueler.Count > 0, "keine Einschulungsdaten importiert")
        Assert.AreEqual(projekt.Bestand.SchulName, projekt.Manifest.SchulName)
        Assert.AreEqual(0, projekt.Mapping.Count, "der Import hat Klarnamen erfunden")

        ' Der Id-Zaehler muss ueber allen vorhandenen Ids liegen.
        Dim hoechste = projekt.Klassenbildung.Schueler.
            Select(Function(s) Integer.Parse(s.Id.Substring(1))).Max()
        Assert.IsTrue(projekt.Manifest.NaechsteSchuelerNummer > hoechste,
                      $"NeueSchuelerId wuerde eine vergebene Id wiederverwenden ({projekt.Manifest.NaechsteSchuelerNummer} <= {hoechste})")

        Dim ziel = IO.Path.Combine(IO.Path.GetTempPath(), "splanx-export-" & Guid.NewGuid().ToString("N"))
        Try
            ProjektOrdner.Exportieren(projekt, ziel)
            Dim erneut = ProjektOrdner.Importieren(ziel, Jetzt)

            Assert.AreEqual(projekt.Bestand.Klassen.Count, erneut.Bestand.Klassen.Count)
            Assert.AreEqual(projekt.Bestand.Lehrkraefte.Count, erneut.Bestand.Lehrkraefte.Count)
            Assert.AreEqual(projekt.Bestand.Faecher.Count, erneut.Bestand.Faecher.Count)
            Assert.AreEqual(projekt.Constraints.Count, erneut.Constraints.Count)
            Assert.AreEqual(projekt.Klassenbildung.Schueler.Count, erneut.Klassenbildung.Schueler.Count)
            Assert.AreEqual(projekt.Klassenbildung.Gruppen.Count, erneut.Klassenbildung.Gruppen.Count)
            Assert.AreEqual(projekt.Config.SolveTimeLimitS, erneut.Config.SolveTimeLimitS)
            Assert.AreEqual(projekt.Config.MaxSolutions, erneut.Config.MaxSolutions)
        Finally
            If Directory.Exists(ziel) Then Directory.Delete(ziel, recursive:=True)
        End Try
    End Sub

    ''' <summary>Ein Schulordner ohne klassenbildung.yaml ist der Normalfall
    ''' (die Stufe ist eigenstaendig) - der Import darf daran nicht
    ''' scheitern, und der Export darf keine leere Datei erzeugen, die
    ''' einen sinnlosen `klassen`-Lauf nahelegt.</summary>
    <TestMethod>
    Public Sub OrdnerOhneKlassenbildungIstZulaessig()
        Dim quelle = IO.Path.Combine(TestsRoot, "bw-gms-beispiel")
        Dim projekt = ProjektOrdner.Importieren(quelle, Jetzt)
        Assert.AreEqual(0, projekt.Klassenbildung.Schueler.Count)

        Dim ziel = IO.Path.Combine(IO.Path.GetTempPath(), "splanx-export-" & Guid.NewGuid().ToString("N"))
        Try
            ProjektOrdner.Exportieren(projekt, ziel)
            Assert.IsFalse(File.Exists(IO.Path.Combine(ziel, "input", "klassenbildung.yaml")),
                           "leere klassenbildung.yaml wurde erzeugt")
            Assert.IsTrue(File.Exists(IO.Path.Combine(ziel, "input", "stammdaten.yaml")))
            Assert.IsTrue(File.Exists(IO.Path.Combine(ziel, "input", "constraints.yaml")))
        Finally
            If Directory.Exists(ziel) Then Directory.Delete(ziel, recursive:=True)
        End Try
    End Sub

End Class
