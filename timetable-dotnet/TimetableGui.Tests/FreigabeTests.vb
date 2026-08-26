' Staende-Historie und Freigabe, Stufe G3.
'
' Die Freigabe ist die einzige Stelle der Anwendung, deren Zweck ein
' RECHTLICHER Nachweis ist (Art. 22 DSGVO, klassenbildung-konzept 10):
' dass eine echte menschliche Pruefung stattgefunden hat. Geprueft wird
' hier deshalb nicht "wird ein Feld gesetzt", sondern ob der Nachweis
' traegt - werden die Abweichungen GEZEIGT, nennt der Bestaetigungssatz
' ihre Zahl, ueberlebt der Nachweis das Speichern, und ist der Stand
' danach wirklich gegen Loeschen und Verdraengen geschuetzt.
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class FreigabeTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 24, 15, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttfg-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(_ordner)
    End Sub

    <TestCleanup>
    Public Sub Abraeumen()
        If IO.Directory.Exists(_ordner) Then IO.Directory.Delete(_ordner, recursive:=True)
    End Sub

    ''' <summary>Ein Stand mit erfundenem, aber formgleichem Viewer-JSON.
    ''' Bewusst OHNE Solverlauf: geprueft wird die Aufbereitung, nicht der
    ''' Kern - und so bleiben die Tests in Millisekunden.</summary>
    Private Shared Function Plan(id As String, minuten As Integer,
                                 muss As Integer, kann As Integer) As ProjektStand
        Return New ProjektStand With {
            .Id = id, .Label = "Stundenplan " & id,
            .Erstellt = Jetzt.AddMinutes(minuten),
            .Stundenplan = JsonNode.Parse($"
                {{""solutions"": [
                    {{""muss_violation_count"": {muss}, ""kann_violation_count"": {kann},
                      ""quality_total"": 193, ""class_gap_count"": 4,
                      ""teacher_gap_count"": 7, ""edge_period_count"": 26}}]}}").AsObject()}
    End Function

    Private Function Projekt(ParamArray staende As ProjektStand()) As Projekt
        Dim p As New Projekt()
        For Each s In staende
            p.Staende.Add(s)
        Next
        Return p
    End Function

    Private Function Modell(p As Projekt, d As TestDialoge) As LaeufeViewModel
        Return New LaeufeViewModel(p, d, Function() Jetzt)
    End Function

    ' ===============================================================
    ' Die Vorlage
    ' ===============================================================

    ''' <summary>Ohne angezeigte Abweichungen ist die Bestaetigung ein
    ''' Durchwinken. Der Satz muss ihre ZAHL nennen - daran haengt, dass
    ''' er Substanz hat (10.1).</summary>
    <TestMethod>
    Public Sub BestaetigungssatzNenntDieZahlDerAbweichungen()
        Dim sauber = Freigabe.Vorlage(Plan("a", 0, muss:=0, kann:=0))
        Assert.AreEqual(0, sauber.Abweichungen.Count)
        StringAssert.Contains(Freigabe.Bestaetigungssatz(sauber), "keine Regelabweichungen")

        Dim eine = Freigabe.Vorlage(Plan("b", 0, muss:=0, kann:=3))
        Assert.AreEqual(1, eine.Abweichungen.Count)
        StringAssert.Contains(Freigabe.Bestaetigungssatz(eine), "1 Regelabweichung")

        Dim zwei = Freigabe.Vorlage(Plan("c", 0, muss:=2, kann:=3))
        Assert.AreEqual(2, zwei.Abweichungen.Count)
        StringAssert.Contains(Freigabe.Bestaetigungssatz(zwei), "2 Regelabweichungen")
        Assert.AreEqual(2, zwei.HarteVerstoesse, "Muss-Verstoesse sind etwas anderes als Kann-Regeln")
    End Sub

    ''' <summary>Luecken und Randstunden sind Qualitaetsmerkmale, keine
    ''' Regelverletzungen. Sie mitzuzaehlen wuerde den
    ''' Bestaetigungssatz aufblaehen mit Dingen, die niemand versprochen
    ''' hat - und den Nachweis damit entwerten.</summary>
    <TestMethod>
    Public Sub LueckenZaehlenNichtAlsAbweichungSondernAlsKennzahl()
        Dim v = Freigabe.Vorlage(Plan("a", 0, muss:=0, kann:=0))
        Assert.AreEqual(0, v.Abweichungen.Count)
        StringAssert.Contains(v.Kennzahlen, "4 Klassen- und 7 Lehrerlücken")
        StringAssert.Contains(v.Kennzahlen, "26 Randstunden")
    End Sub

    ''' <summary>Freigegeben wird die MARKIERTE Loesung, nicht die erste -
    ''' sonst pruefte der Mensch etwas anderes, als hinterher im Nachweis
    ''' steht.</summary>
    <TestMethod>
    Public Sub DieMarkierteLoesungBestimmtDieAbweichungen()
        Dim stand As New ProjektStand With {
            .Id = "x", .Label = "Plan", .Erstellt = Jetzt,
            .Stundenplan = JsonNode.Parse("
                {""solutions"": [
                    {""muss_violation_count"": 0, ""kann_violation_count"": 0},
                    {""muss_violation_count"": 0, ""kann_violation_count"": 5}]}").AsObject(),
            .Lauf = JsonNode.Parse("{""arbeitsstand"": {""zuteilung"": 1, ""loesung"": 2}}").AsObject()}

        Dim v = Freigabe.Vorlage(stand)
        Assert.AreEqual(1, v.Abweichungen.Count)
        StringAssert.Contains(v.Abweichungen(0), "5 nicht erfüllte Kann-Regel")
        StringAssert.Contains(v.Kennzahlen, "Lösung 2")
    End Sub

    <TestMethod>
    Public Sub EinStandOhneErgebnisIstNichtFreizugeben()
        Dim leer As New ProjektStand With {.Id = "leer", .Label = "leer", .Erstellt = Jetzt}
        Dim d As New TestDialoge()
        Dim m = Modell(Projekt(leer), d)

        Assert.IsFalse(m.Freigeben("leer"))
        Assert.AreEqual(1, d.Hinweise.Count)
        StringAssert.Contains(d.Hinweise(0), "kein Ergebnis")
    End Sub

    ' ===============================================================
    ' Der Nachweis
    ' ===============================================================

    ''' <summary>Der eigentliche Pruefstein: der Dialog bekommt die
    ''' Abweichungen zu SEHEN, und was danach im Projekt steht, ist genau
    ''' das - Person, Zeitpunkt, Satz, Abweichungsliste.</summary>
    <TestMethod>
    Public Sub FreigabeHinterlegtDenNachweisWieAngezeigt()
        Dim p = Projekt(Plan("a", 0, muss:=1, kann:=4))
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {
                .Person = "Frau Meier", .Bestaetigt = True, .Notiz = "Geprüft und vertretbar."}}
        Dim m = Modell(p, d)

        Assert.IsTrue(m.Freigeben("a"))

        Assert.AreEqual(1, d.FreigabeVorlagen.Count, "der Dialog muss die Abweichungen zu sehen bekommen")
        Assert.AreEqual(2, d.FreigabeVorlagen(0).Abweichungen.Count)

        Dim nachweis = p.Staende(0).Lauf("freigabe").AsObject()
        Assert.AreEqual("Frau Meier", nachweis("person").GetValue(Of String)())
        StringAssert.Contains(nachweis("satz").GetValue(Of String)(), "2 Regelabweichungen")
        Assert.AreEqual(2, nachweis("abweichungen").AsArray().Count,
                        "der Nachweis muss die ANGEZEIGTEN Abweichungen enthalten")

        Dim protokoll = p.AuditLog.Where(Function(e) e.Aktion = "freigabe").ToList()
        Assert.AreEqual(1, protokoll.Count)
        Assert.AreEqual("Frau Meier", protokoll(0).Benutzer, "das Protokoll nennt die Person, nicht das Konto")
    End Sub

    ''' <summary>Ein Abbruch im Dialog darf NICHTS hinterlassen - sonst
    ''' gaebe es einen Nachweis ohne Entscheidung.</summary>
    <TestMethod>
    Public Sub AbbruchImDialogGibtNichtsFrei()
        Dim p = Projekt(Plan("a", 0, muss:=0, kann:=0))
        Dim d As New TestDialoge()   ' FreigabeAntwort = Nothing => Abbruch
        Dim m = Modell(p, d)

        Assert.IsFalse(m.Freigeben("a"))
        Assert.IsFalse(LaeufeViewModel.IstFreigabe(p.Staende(0)))
        Assert.IsFalse(p.Staende(0).Geschuetzt)
        Assert.AreEqual(0, p.AuditLog.Where(Function(e) e.Aktion = "freigabe").Count)
    End Sub

    ''' <summary>Ein Haken ohne Namen ist kein Nachweis.</summary>
    <TestMethod>
    Public Sub FreigabeOhneNamenWirdAbgelehnt()
        Dim p = Projekt(Plan("a", 0, muss:=0, kann:=0))
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {.Person = "  ", .Bestaetigt = True}}
        Dim m = Modell(p, d)

        Assert.IsFalse(m.Freigeben("a"))
        Assert.IsTrue(d.Hinweise.Any(Function(h) h.Contains("benannte Person")))
    End Sub

    ' ===============================================================
    ' Schutz
    ' ===============================================================

    ''' <summary>"Freigabe-Stand gegen Loeschen/Verdraengen geschuetzt".
    ''' Beides erledigt der Kern - der Test belegt, dass die Freigabe die
    ''' Marke wirklich setzt und der Schutz dadurch greift.</summary>
    <TestMethod>
    Public Sub FreigegebenerStandUeberlebtLoeschenUndVerdraengung()
        Dim p = Projekt(Plan("alt", 0, muss:=0, kann:=0))
        p.Manifest.MaxStaende = 2
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {.Person = "Frau Meier", .Bestaetigt = True}}
        Dim m = Modell(p, d)
        Assert.IsTrue(m.Freigeben("alt"))

        ' Loeschen prallt ab - mit Begruendung, nicht wortlos.
        Assert.IsFalse(m.Loeschen("alt"))
        Assert.IsTrue(d.Hinweise.Any(Function(h) h.Contains("freigegebener Stand")))

        ' Verdraengung ebenfalls: drei Staende bei Obergrenze zwei, und
        ' der aelteste ist der freigegebene.
        p.StandHinzufuegen(Plan("b", 10, 0, 0))
        p.StandHinzufuegen(Plan("c", 20, 0, 0))
        Assert.IsTrue(p.Staende.Any(Function(s) s.Id = "alt"),
                      "die Freigabe wurde von der Obergrenze weggeworfen")
    End Sub

    ''' <summary>Zwei Freigaben nebeneinander waeren genau die Unklarheit,
    ''' die der Nachweis ausschliessen soll. Die alte wird
    ''' zurueckgezogen - sichtbar, mit Frage und Protokollzeile.</summary>
    <TestMethod>
    Public Sub ZweiteFreigabeZiehtDieErsteSichtbarZurueck()
        Dim p = Projekt(Plan("a", 0, 0, 0), Plan("b", 10, 0, 0))
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {.Person = "Frau Meier", .Bestaetigt = True}}
        Dim m = Modell(p, d)
        Assert.IsTrue(m.Freigeben("a"))

        Assert.IsTrue(m.Freigeben("b"))

        Assert.IsTrue(d.Fragen.Any(Function(f) f.Contains("zurückgezogen")),
                      "das Ersetzen muss GEFRAGT werden, nicht still geschehen")
        Assert.IsFalse(LaeufeViewModel.IstFreigabe(m.Finde("a")))
        Assert.IsFalse(m.Finde("a").Geschuetzt, "der Schutz gehoert zur Freigabe, nicht zum Stand")
        Assert.IsTrue(LaeufeViewModel.IstFreigabe(m.Finde("b")))
        Assert.AreEqual(1, p.AuditLog.Where(Function(e) e.Beschreibung.Contains("zurückgezogen")).Count)
    End Sub

    <TestMethod>
    Public Sub NeinBeimErsetzenLaesstAllesWieEsIst()
        Dim p = Projekt(Plan("a", 0, 0, 0), Plan("b", 10, 0, 0))
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {.Person = "Frau Meier", .Bestaetigt = True}}
        Dim m = Modell(p, d)
        Assert.IsTrue(m.Freigeben("a"))

        d.FrageAntwort = False
        Assert.IsFalse(m.Freigeben("b"))

        Assert.IsTrue(LaeufeViewModel.IstFreigabe(m.Finde("a")))
        Assert.IsFalse(LaeufeViewModel.IstFreigabe(m.Finde("b")))
    End Sub

    ''' <summary>Das Label eines freigegebenen Standes gehoert zum
    ''' Nachweis - es nachtraeglich zu aendern hiesse, den Nachweis zu
    ''' aendern.</summary>
    <TestMethod>
    Public Sub FreigegebenerStandLaesstSichNichtUmbenennen()
        Dim p = Projekt(Plan("a", 0, 0, 0))
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {.Person = "Frau Meier", .Bestaetigt = True}}
        Dim m = Modell(p, d)
        m.Freigeben("a")

        Assert.IsFalse(m.Umbenennen("a", "Anders"))
        Assert.AreEqual("Stundenplan a", m.Finde("a").Label)
    End Sub

    ' ===============================================================
    ' Historie
    ' ===============================================================

    <TestMethod>
    Public Sub HistorieZeigtNeuesteZuerstUndMarkiertDieFreigabe()
        Dim p = Projekt(Plan("alt", 0, 0, 0), Plan("neu", 60, 0, 0))
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {.Person = "Frau Meier", .Bestaetigt = True}}
        Dim m = Modell(p, d)
        m.Freigeben("alt")

        Dim zeilen = m.Zeilen()
        Assert.AreEqual("neu", zeilen(0).Id, "die Historie wird von oben gelesen")
        Assert.IsTrue(zeilen(1).IstFreigabe)
        StringAssert.Contains(zeilen(1).Anzeige, "freigegeben")
        Assert.AreEqual("Stundenplan", zeilen(0).Art)
    End Sub

    ''' <summary>Loeschen nennt die Folgen (Konzept 7) - und "nein" laesst
    ''' alles stehen.</summary>
    <TestMethod>
    Public Sub LoeschenFragtMitFolgenUndNeinLoeschtNicht()
        Dim p = Projekt(Plan("a", 0, 0, 0))
        Dim d As New TestDialoge With {.FrageAntwort = False}
        Dim m = Modell(p, d)

        Assert.IsFalse(m.Loeschen("a"))
        Assert.AreEqual(1, p.Staende.Count)
        StringAssert.Contains(d.Fragen(0), "Protokollzeile des Laufs bleibt erhalten")

        d.FrageAntwort = True
        Assert.IsTrue(m.Loeschen("a"))
        Assert.AreEqual(0, p.Staende.Count)
        Assert.IsTrue(p.AuditLog.Any(Function(e) e.Aktion = "stand"),
                      "die Protokollzeile ist der Nachweis, nicht der Stand")
    End Sub

    ''' <summary>Die Freigabe wird aus dem GESPEICHERTEN Stand gelesen -
    ''' ein Nachweis, der das Speichern nicht ueberlebt, ist keiner.</summary>
    <TestMethod>
    Public Sub DerNachweisUeberlebtSpeichernUndLaden()
        Dim p = Projekt(Plan("a", 0, muss:=0, kann:=2))
        p.Manifest.SchulName = "Testschule"
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {
                .Person = "Frau Meier", .Bestaetigt = True, .Notiz = "Geprüft und vertretbar."}}
        Modell(p, d).Freigeben("a")

        Dim pfad = IO.Path.Combine(_ordner, "f.splanx")
        ProjektDatei.Speichern(p, pfad, "geheim12")
        Dim erneut = ProjektDatei.Laden(pfad, "geheim12")

        Dim stand = erneut.Staende.First()
        Assert.IsTrue(stand.Geschuetzt, "der Schutz hat das Speichern nicht ueberlebt")
        Assert.IsTrue(LaeufeViewModel.IstFreigabe(stand))
        Assert.AreEqual("Frau Meier", stand.Lauf("freigabe").AsObject()("person").GetValue(Of String)())
        Assert.AreEqual(1, stand.Lauf("freigabe").AsObject()("abweichungen").AsArray().Count)
    End Sub

    ' ===============================================================
    ' Die eigene Begruendung (Art. 22)
    ' ===============================================================

    ''' <summary>Pflicht genau dann, wenn es etwas abzuwaegen gibt. Ohne
    ''' Abweichungen waere der Zwang zur Notiz selbst wieder Theater -
    ''' und Theater entwertet den Nachweis, statt ihn zu staerken.</summary>
    <TestMethod>
    Public Sub DieNotizIstPflichtNurBeiAbweichungen()
        Assert.IsFalse(Freigabe.Vorlage(Plan("a", 0, muss:=0, kann:=0)).NotizPflicht)
        Assert.IsTrue(Freigabe.Vorlage(Plan("b", 0, muss:=0, kann:=1)).NotizPflicht)
    End Sub

    ''' <summary>"Bemerkung" erzeugt "ok". Die Frage nach der
    ''' Vertretbarkeit erzeugt eine Begruendung - deshalb steht sie so im
    ''' Dialog und nicht anders.</summary>
    <TestMethod>
    Public Sub DieNotizfrageFragtNachDerVertretbarkeit()
        StringAssert.Contains(Freigabe.Vorlage(Plan("b", 0, 0, 1)).Notizfrage, "vertretbar")
        StringAssert.Contains(Freigabe.Vorlage(Plan("a", 0, 0, 0)).Notizfrage, "freiwillig")
    End Sub

    ''' <summary>Der Kern der Sache: ein Haken ohne eigene Worte belegt
    ''' keine Befassung. Geprueft wird das HIER und nicht nur im Fenster -
    ''' das Fenster kann man umgehen, diese Funktion nicht.</summary>
    <TestMethod>
    Public Sub OhneBegruendungKeineFreigabeWennAbweichungenBleiben()
        Dim p = Projekt(Plan("a", 0, muss:=0, kann:=3))
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {
                .Person = "Frau Meier", .Bestaetigt = True, .Notiz = "   "}}
        Dim m = Modell(p, d)

        Assert.IsFalse(m.Freigeben("a"))
        Assert.IsFalse(LaeufeViewModel.IstFreigabe(p.Staende(0)))
        Assert.IsTrue(d.Hinweise.Any(Function(h) h.Contains("Begründung")))
    End Sub

    <TestMethod>
    Public Sub OhneAbweichungenGehtEsAuchOhneBegruendung()
        Dim p = Projekt(Plan("a", 0, muss:=0, kann:=0))
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {
                .Person = "Frau Meier", .Bestaetigt = True}}

        Assert.IsTrue(Modell(p, d).Freigeben("a"))
    End Sub

    ''' <summary>Die Begruendung steht woertlich im Nachweis UND in der
    ''' Protokollzeile - sie ist der Teil, der eine echte Pruefung von
    ''' einem Durchwinken unterscheidet, und darf deshalb nicht nur
    ''' irgendwo mitlaufen.</summary>
    <TestMethod>
    Public Sub DieBegruendungStehtWoertlichImNachweisUndImProtokoll()
        Dim p = Projekt(Plan("a", 0, muss:=0, kann:=3))
        Dim begruendung = "Die drei Wunschregeln betreffen nur Randstunden; " &
                          "die Fachraeume sind wichtiger."
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {
                .Person = "Frau Meier", .Bestaetigt = True, .Notiz = begruendung}}

        Assert.IsTrue(Modell(p, d).Freigeben("a"))

        Dim nachweis = p.Staende(0).Lauf("freigabe").AsObject()
        Assert.AreEqual(begruendung, nachweis("notiz").GetValue(Of String)())
        Dim zeile = p.AuditLog.First(Function(e) e.Aktion = "freigabe")
        StringAssert.Contains(zeile.Beschreibung, begruendung)
        StringAssert.Contains(zeile.Beschreibung, "in eigener Verantwortung",
                              "der feste Satz traegt die Pflichtbestandteile und bleibt daneben stehen")
    End Sub

    ''' <summary>Auch die Begruendung muss das Speichern ueberleben -
    ''' sonst bliebe vom Nachweis genau der Teil uebrig, der sich
    ''' mechanisch erzeugen laesst.</summary>
    <TestMethod>
    Public Sub DieBegruendungUeberlebtSpeichernUndLaden()
        Dim p = Projekt(Plan("a", 0, muss:=0, kann:=2))
        Dim d As New TestDialoge With {
            .FreigabeAntwort = New Freigabebestaetigung With {
                .Person = "Frau Meier", .Bestaetigt = True, .Notiz = "Abgewogen und vertretbar."}}
        Modell(p, d).Freigeben("a")

        Dim pfad = IO.Path.Combine(_ordner, "n.splanx")
        ProjektDatei.Speichern(p, pfad, "geheim12")
        Dim erneut = ProjektDatei.Laden(pfad, "geheim12")

        Assert.AreEqual("Abgewogen und vertretbar.",
                        erneut.Staende.First().Lauf("freigabe").AsObject()("notiz").GetValue(Of String)())
    End Sub

End Class
