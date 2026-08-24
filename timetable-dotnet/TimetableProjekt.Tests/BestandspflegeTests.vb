' Umbenennen und Loeschen (Stufe F, Querschnitt aus gui-ui-konzept.md 7).
'
' "Namen sind Schluessel" - deshalb ist Umbenennen hier keine Kosmetik,
' sondern eine Kaskade ueber Qualifikationen, feste Zuordnungen, Gruppen
' und Regeln. Ein vergessener Verweis faellt nicht sofort auf: er ergibt
' eine verwaiste Referenz, die erst der naechste Solve-Lauf als
' "unbekannte Entity" meldet - und bis dahin sieht alles gut aus.
'
' Gefahren wird deshalb gegen die ECHTEN Beispielschulen. Die GMS-Fixture
' hat 2204 Zeilen Constraints mit allen Referenzarten (class, teacher,
' subject, allowed_rooms, entity mit scope/resource) - ein synthetisches
' Miniszenario wuerde genau die Faelle nicht treffen, die hier zaehlen.
Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableProjekt

<TestClass>
Public Class BestandspflegeTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 23, 16, 0, 0, TimeSpan.Zero)

    Private Shared ReadOnly Property TestsRoot As String
        Get
            Dim dir = New DirectoryInfo(AppContext.BaseDirectory)
            While dir IsNot Nothing
                Dim kandidat = IO.Path.Combine(dir.FullName, "tests", "bw-gms-beispiel")
                If Directory.Exists(kandidat) Then Return IO.Path.Combine(dir.FullName, "tests")
                dir = dir.Parent
            End While
            Throw New InvalidOperationException("tests/-Verzeichnis nicht gefunden")
        End Get
    End Property

    Private Shared Function LadeSchule(name As String) As Projekt
        Return ProjektOrdner.Importieren(IO.Path.Combine(TestsRoot, name), Jetzt)
    End Function

    ''' <summary>Der eigentliche Massstab: nach der Aenderung darf
    ''' ValidateEntities keine unbekannte Referenz mehr finden. Baut
    ''' dasselbe Szenario wie der CLI-Lauf - Stammdaten plus
    ''' Handregeln.</summary>
    Private Shared Function ReferenzFehler(p As Projekt) As List(Of String)
        Dim ent = Stammdaten.BuildEntitiesFragment(p.Bestand)
        Dim data As New System.Text.Json.Nodes.JsonObject From {
            {"entities", ent},
            {"constraints", New System.Text.Json.Nodes.JsonArray(
                p.Constraints.Select(Function(c) CType(c.DeepClone(), System.Text.Json.Nodes.JsonNode)).ToArray())}
        }
        ' Nur die Referenz-Fehler interessieren; die Vollstaendigkeits-
        ' meldungen (fehlende teacher_subject_assignment o.ae.) entstehen
        ' erst durch die generierten Regeln des Lehrereinsatzes.
        Return Validation.ValidateEntities(data).
            Where(Function(e) e.Contains("keine bekannte Entity") OrElse e.Contains("nicht in ")).ToList()
    End Function

    ' ---------------------------------------------------------------
    ' Vorschau
    ' ---------------------------------------------------------------

    ''' <summary>Die Vorschau des Konzepts ("12 Verweise werden
    ''' angepasst") - sie darf nichts veraendern.</summary>
    <TestMethod>
    Public Sub VerweiseZeigenOhneZuAendern()
        Dim p = LadeSchule("bw-gms-beispiel")
        Dim lehrer = p.Bestand.Lehrkraefte.First(Function(l) p.Bestand.FachLehrerZuordnungen.Any(Function(z) z.LehrerName = l.Name)).Name
        Dim vorherQuali = p.Bestand.FachLehrerZuordnungen.Count
        Dim vorherRegeln = p.Constraints.Count

        Dim f = Bestandspflege.Verweise(p, Stammart.Lehrkraft, lehrer)

        Assert.IsTrue(f.Anzahl > 0, $"'{lehrer}' hat laut Fixture Qualifikationen, die Vorschau findet aber nichts")
        StringAssert.Contains(f.Zusammenfassung, "Qualifikation")
        Assert.AreEqual(vorherQuali, p.Bestand.FachLehrerZuordnungen.Count, "die Vorschau hat etwas geaendert")
        Assert.AreEqual(vorherRegeln, p.Constraints.Count, "die Vorschau hat etwas geaendert")
    End Sub

    <TestMethod>
    Public Sub UnbekannterNameHatKeineVerweise()
        Dim p = LadeSchule("bw-grundschule-beispiel")
        Assert.AreEqual(0, Bestandspflege.Verweise(p, Stammart.Lehrkraft, "gibt-es-nicht").Anzahl)
        Assert.AreEqual(0, Bestandspflege.Verweise(p, Stammart.Fach, Nothing).Anzahl)
    End Sub

    ' ---------------------------------------------------------------
    ' Umbenennen
    ' ---------------------------------------------------------------

    <DataTestMethod>
    <DataRow("bw-grundschule-beispiel")>
    <DataRow("bw-gms-beispiel")>
    Public Sub LehrkraftUmbenennenZiehtAlleVerweiseMit(schule As String)
        Dim p = LadeSchule(schule)
        Assert.AreEqual(0, ReferenzFehler(p).Count, "die Fixture ist schon vorher inkonsistent")
        Dim alt = p.Bestand.Lehrkraefte.First().Name
        Dim neu = alt & "-umbenannt"

        Bestandspflege.BenenneUm(p, Stammart.Lehrkraft, alt, neu)

        Assert.IsFalse(p.Bestand.Lehrkraefte.Any(Function(l) l.Name = alt), "der alte Name steht noch im Bestand")
        Assert.IsTrue(p.Bestand.Lehrkraefte.Any(Function(l) l.Name = neu))
        Assert.IsFalse(p.Bestand.FachLehrerZuordnungen.Any(Function(z) z.LehrerName = alt))
        Assert.IsFalse(p.Bestand.FesteZuordnungen.Any(Function(z) z.LehrerName = alt))
        Assert.AreEqual(0, ReferenzFehler(p).Count,
                        "nach dem Umbenennen sind Referenzen verwaist:" & vbLf & String.Join(vbLf, ReferenzFehler(p)))
    End Sub

    <DataTestMethod>
    <DataRow("bw-grundschule-beispiel")>
    <DataRow("bw-gms-beispiel")>
    Public Sub FachUmbenennenZiehtAlleVerweiseMit(schule As String)
        Dim p = LadeSchule(schule)
        Dim alt = p.Bestand.Faecher.First().Name
        Dim neu = alt & "-neu"

        Bestandspflege.BenenneUm(p, Stammart.Fach, alt, neu)

        Assert.IsFalse(p.Bestand.Faecher.Any(Function(f) f.Name = alt))
        Assert.IsFalse(p.Bestand.FachLehrerZuordnungen.Any(Function(z) z.FachName = alt))
        Assert.IsFalse(p.Bestand.Gruppen.Any(Function(g) g.FachName = alt))
        Assert.AreEqual(0, ReferenzFehler(p).Count,
                        "verwaiste Referenzen:" & vbLf & String.Join(vbLf, ReferenzFehler(p)))
    End Sub

    <DataTestMethod>
    <DataRow("bw-grundschule-beispiel")>
    <DataRow("bw-gms-beispiel")>
    Public Sub KlasseUmbenennenZiehtAlleVerweiseMit(schule As String)
        Dim p = LadeSchule(schule)
        Dim alt = p.Bestand.Klassen.First().Name
        Dim neu = alt & "-x"

        Bestandspflege.BenenneUm(p, Stammart.Klasse, alt, neu)

        Assert.IsFalse(p.Bestand.Klassen.Any(Function(k) k.Name = alt))
        Assert.IsFalse(p.Bestand.FesteZuordnungen.Any(Function(z) z.KlasseName = alt))
        Assert.IsFalse(p.Bestand.Schueler.Any(Function(s) s.Klasse = alt))
        Assert.AreEqual(0, ReferenzFehler(p).Count,
                        "verwaiste Referenzen:" & vbLf & String.Join(vbLf, ReferenzFehler(p)))
    End Sub

    ''' <summary>Raeume werden AUSSCHLIESSLICH ueber Regeln referenziert -
    ''' und dort teils in Listenfeldern (allowed_rooms). Genau der Fall,
    ''' den eine handgeschriebene Kaskade uebersieht.</summary>
    <TestMethod>
    Public Sub RaumUmbenennenTrifftAuchListenfelder()
        Dim p = LadeSchule("bw-gms-beispiel")
        Dim alt = p.Constraints.
            Where(Function(c) JsonHelpers.GetString(c, "type") = "room_requirement").
            SelectMany(Function(c) JsonHelpers.AsStringList(c, "allowed_rooms")).First()
        Dim neu = alt & "-neu"

        Bestandspflege.BenenneUm(p, Stammart.Raum, alt, neu)

        Assert.IsFalse(p.Bestand.Raeume.Any(Function(r) r.Name = alt))
        Dim nochAlt = p.Constraints.SelectMany(Function(c) Validation.ReferenzenVon(c)).
            Where(Function(r) r.EntityArt = "rooms" AndAlso r.Wert = alt).Count
        Assert.AreEqual(0, nochAlt, "der alte Raumname steht noch in einer Regel")
        Assert.AreEqual(0, ReferenzFehler(p).Count,
                        "verwaiste Referenzen:" & vbLf & String.Join(vbLf, ReferenzFehler(p)))
    End Sub

    ''' <summary>Zwei Lehrkraefte gleichen Namens waeren im Wire-Format
    ''' nicht unterscheidbar - der Solver legte ihre Stunden stillschweigend
    ''' zusammen. Deshalb Ablehnung statt Zusammenfuehrung.</summary>
    <TestMethod>
    Public Sub UmbenennenAufVergebenenNamenWirdAbgelehnt()
        Dim p = LadeSchule("bw-grundschule-beispiel")
        Dim a = p.Bestand.Lehrkraefte(0).Name
        Dim b = p.Bestand.Lehrkraefte(1).Name

        Assert.ThrowsException(Of InvalidOperationException)(
            Sub() Bestandspflege.BenenneUm(p, Stammart.Lehrkraft, a, b))
        Assert.ThrowsException(Of ArgumentException)(
            Sub() Bestandspflege.BenenneUm(p, Stammart.Lehrkraft, a, "   "))

        Assert.IsTrue(p.Bestand.Lehrkraefte.Any(Function(l) l.Name = a), "der Bestand wurde trotz Ablehnung veraendert")
    End Sub

    <TestMethod>
    Public Sub UmbenennenAufDenselbenNamenIstEinNoOp()
        Dim p = LadeSchule("bw-grundschule-beispiel")
        Dim name = p.Bestand.Lehrkraefte(0).Name
        Dim vorher = p.Constraints.Count

        Dim f = Bestandspflege.BenenneUm(p, Stammart.Lehrkraft, name, name)

        Assert.AreEqual(0, f.Anzahl)
        Assert.AreEqual(vorher, p.Constraints.Count)
    End Sub

    ' ---------------------------------------------------------------
    ' Loeschen
    ' ---------------------------------------------------------------

    ''' <summary>"Niemals stilles Verwaisen von Referenzen" (Konzept 7):
    ''' nach dem Loeschen darf ValidateEntities nichts mehr finden.</summary>
    <DataTestMethod>
    <DataRow("bw-grundschule-beispiel")>
    <DataRow("bw-gms-beispiel")>
    Public Sub LehrkraftLoeschenHinterlaesstKeineVerwaistenVerweise(schule As String)
        Dim p = LadeSchule(schule)
        Dim name = p.Bestand.Lehrkraefte.First(Function(l) p.Bestand.FachLehrerZuordnungen.Any(Function(z) z.LehrerName = l.Name)).Name

        Dim folgen = Bestandspflege.Loesche(p, Stammart.Lehrkraft, name)

        Assert.IsTrue(folgen.Anzahl > 0, "es wurden keine Folgen gemeldet, obwohl es welche gab")
        Assert.IsFalse(p.Bestand.Lehrkraefte.Any(Function(l) l.Name = name))
        Assert.IsFalse(p.Bestand.FachLehrerZuordnungen.Any(Function(z) z.LehrerName = name))
        Assert.AreEqual(0, ReferenzFehler(p).Count,
                        "verwaiste Referenzen nach dem Loeschen:" & vbLf & String.Join(vbLf, ReferenzFehler(p)))
    End Sub

    ''' <summary>Ein Kind ist kein Anhaengsel seiner Klasse: beim Loeschen
    ''' der Klasse wird die Heimatklasse geleert, das Kind bleibt.</summary>
    <TestMethod>
    Public Sub KlasseLoeschenBehaeltDieSchueler()
        Dim p = LadeSchule("bw-gms-beispiel")
        Dim klasse = p.Bestand.Klassen.First(Function(k) p.Bestand.Schueler.Any(Function(s) s.Klasse = k.Name)).Name
        Dim kinderVorher = p.Bestand.Schueler.Count

        Bestandspflege.Loesche(p, Stammart.Klasse, klasse)

        Assert.AreEqual(kinderVorher, p.Bestand.Schueler.Count, "Kinder wurden mit der Klasse geloescht")
        Assert.IsFalse(p.Bestand.Schueler.Any(Function(s) s.Klasse = klasse), "die Heimatklasse zeigt ins Leere")
        Assert.AreEqual(0, ReferenzFehler(p).Count)
    End Sub

    ''' <summary>Bei Listenfeldern bleibt die Regel bestehen, solange noch
    ''' ein Eintrag drinsteht - erst die leere Liste macht sie sinnlos.
    ''' Eine Sport-Regel mit zwei Turnhallen darf nicht verschwinden, nur
    ''' weil eine davon geloescht wird.</summary>
    <TestMethod>
    Public Sub RaumLoeschenLeertNurDenListeneintrag()
        Dim p = LadeSchule("bw-gms-beispiel")
        Dim mehrfach = p.Constraints.First(Function(c) JsonHelpers.GetString(c, "type") = "room_requirement" AndAlso
                                                       JsonHelpers.AsStringList(c, "allowed_rooms").Count >= 2)
        Dim fach = JsonHelpers.GetString(mehrfach, "subject")
        Dim raum = JsonHelpers.AsStringList(mehrfach, "allowed_rooms").First()
        Dim regelnVorher = p.Constraints.Count

        Bestandspflege.Loesche(p, Stammart.Raum, raum)

        Dim danach = p.Constraints.FirstOrDefault(Function(c) JsonHelpers.GetString(c, "type") = "room_requirement" AndAlso
                                                              JsonHelpers.GetString(c, "subject") = fach)
        Assert.IsNotNull(danach, "die Raumregel wurde geloescht, obwohl noch andere Raeume erlaubt waren")
        Assert.IsFalse(JsonHelpers.AsStringList(danach, "allowed_rooms").Contains(raum))
        Assert.IsTrue(JsonHelpers.AsStringList(danach, "allowed_rooms").Count > 0)
        Assert.AreEqual(0, ReferenzFehler(p).Count)
    End Sub

    <TestMethod>
    Public Sub NameVergebenErkenntDoppelte()
        Dim p = LadeSchule("bw-grundschule-beispiel")
        Assert.IsTrue(Bestandspflege.NameVergeben(p, Stammart.Lehrkraft, p.Bestand.Lehrkraefte(0).Name))
        Assert.IsFalse(Bestandspflege.NameVergeben(p, Stammart.Lehrkraft, "Neue Lehrkraft"))
        Assert.IsTrue(Bestandspflege.NameVergeben(p, Stammart.Klasse, p.Bestand.Klassen(0).Name))
        Assert.IsFalse(Bestandspflege.NameVergeben(p, Stammart.Fach, "Astrophysik"))
    End Sub

End Class
