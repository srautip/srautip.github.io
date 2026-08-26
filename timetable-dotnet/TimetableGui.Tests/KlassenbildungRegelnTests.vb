' Gruppen, Balance, Wuensche und Fixierungen der Klassenbildung
' (gui-ui-konzept.md 6.11) - Stufe F6.
'
' Nachgezogen: F4 hatte diese vier nur ANGEZEIGT. Geprueft wird hier
' deshalb vor allem, dass das Geschriebene im Bestand ankommt und dass
' die Vorgaben stimmen - "dieselben Defaults wie das YAML". Eine Maske,
' die haerter voreinstellt als die Datei, erzeugt Regelwerke, die
' niemand so gemeint hat.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class KlassenbildungRegelnTests

    Private Function Projekt(Optional kinder As Integer = 6) As Projekt
        Dim p As New Projekt()
        p.Klassenbildung.Klassen.Anzahl = 2
        p.Klassenbildung.Klassen.MinGroesse = 1
        p.Klassenbildung.Klassen.MaxGroesse = 30
        For i = 1 To kinder
            Dim s As New KlassenbildungSchueler With {.Id = $"S{i:000}"}
            ' Ein kleines Vokabular, damit Balance etwas zu waehlen hat.
            s.Attribute("GESCHLECHT") = If(i Mod 2 = 0, "w", "m")
            p.Klassenbildung.Schueler.Add(s)
        Next
        Return p
    End Function

    Private Function Modelle(p As Projekt) As (Gruppen As KbGruppenViewModel,
                                               Balance As KbBalanceViewModel,
                                               Wuensche As KbWuenscheViewModel,
                                               Fix As KbFixierungenViewModel,
                                               Dialoge As TestDialoge)
        Dim d As New TestDialoge()
        Dim kinder As New KlassenbildungEingabeViewModel(p, d)
        Return (New KbGruppenViewModel(p, d, kinder),
                New KbBalanceViewModel(p, d, kinder),
                New KbWuenscheViewModel(p, d, kinder),
                New KbFixierungenViewModel(p, d, kinder), d)
    End Function

    ' ===============================================================
    ' Gruppen
    ' ===============================================================

    ''' <summary>"Dieselben Defaults wie das YAML (soft, Prio 2 bzw. 1)"
    ''' (6.11). Das ist keine Kleinigkeit: eine Maske, die auf `hard`
    ''' voreinstellt, macht aus einem Wunsch eine Bedingung.</summary>
    <TestMethod>
    Public Sub NeueEintraegeTragenDieYamlVorgaben()
        Dim p = Projekt()
        Dim m = Modelle(p)

        m.Gruppen.Neu()
        Dim g = p.Klassenbildung.Gruppen.Single()
        Assert.AreEqual("buendelung", g.Typ)
        Assert.AreEqual("soft", g.Modus)
        Assert.AreEqual(2, g.Prio)

        m.Balance.Neu()
        Dim b = p.Klassenbildung.Balance.Single()
        Assert.AreEqual("soft", b.Modus)
        Assert.AreEqual(2, b.Prio)
        Assert.AreEqual(0, b.Toleranz)
        Assert.IsNull(b.Attribut, "ein geratenes Attribut wirkte unbemerkt")

        m.Wuensche.Neu()
        Dim w = p.Klassenbildung.Wuensche.Single()
        Assert.AreEqual("zusammen", w.Typ)
        Assert.AreEqual("soft", w.Modus)
        Assert.AreEqual(1, w.Prio, "Wuensche wiegen leichter als Gruppen und Balance")
    End Sub

    <TestMethod>
    Public Sub GruppenMitgliederLandenImBestand()
        Dim p = Projekt()
        Dim m = Modelle(p)
        m.Gruppen.Neu()
        Dim g = p.Klassenbildung.Gruppen.Single()

        m.Gruppen.SetzeMitglied(g, "S001", True)
        m.Gruppen.SetzeMitglied(g, "S003", True)
        m.Gruppen.SetzeMitglied(g, "S001", True)

        CollectionAssert.AreEqual(New List(Of String) From {"S001", "S003"}, g.Mitglieder,
                                  "doppeltes Anhaken darf nicht doppelt eintragen")
        Assert.IsTrue(m.Gruppen.IstMitglied(g, "S003"))

        m.Gruppen.SetzeMitglied(g, "S001", False)
        CollectionAssert.AreEqual(New List(Of String) From {"S003"}, g.Mitglieder)
    End Sub

    <TestMethod>
    Public Sub DuplizierenKopiertDieMitgliederUndVergibtEinenFreienNamen()
        Dim p = Projekt()
        Dim m = Modelle(p)
        m.Gruppen.Neu()
        Dim g = p.Klassenbildung.Gruppen.Single()
        g.Id = "G_zwillinge"
        m.Gruppen.SetzeMitglied(g, "S001", True)
        m.Gruppen.Aktualisiere()
        m.Gruppen.Auswahl = g

        m.Gruppen.Duplizieren()

        Assert.AreEqual(2, p.Klassenbildung.Gruppen.Count)
        Dim kopie = p.Klassenbildung.Gruppen.Last()
        Assert.AreNotEqual(g.Id, kopie.Id, "Namen sind Schluessel - ein Duplikat braucht einen eigenen")
        CollectionAssert.AreEqual(g.Mitglieder, kopie.Mitglieder)
        ' Und die Kopie haengt nicht an der Vorlage.
        m.Gruppen.SetzeMitglied(kopie, "S002", True)
        Assert.AreEqual(1, g.Mitglieder.Count)
    End Sub

    ' ===============================================================
    ' Balance
    ' ===============================================================

    ''' <summary>Attribut und Wert stammen aus dem Vokabular der
    ''' Einschulungsliste. Freitext gaebe es hier nicht, weil eine
    ''' Balance auf ein Attribut, das kein Kind traegt, unbemerkt
    ''' wirkungslos bliebe.</summary>
    <TestMethod>
    Public Sub BalanceBietetNurVorhandeneAttributeUndWerte()
        Dim p = Projekt()
        Dim m = Modelle(p)

        CollectionAssert.AreEqual(New List(Of String) From {"GESCHLECHT"}, m.Balance.Attributnamen())
        CollectionAssert.AreEquivalent(New List(Of String) From {"m", "w"}, m.Balance.Werte("GESCHLECHT"))
        Assert.AreEqual(0, m.Balance.Werte("GIBTESNICHT").Count)
        Assert.AreEqual(0, m.Balance.Werte(Nothing).Count)
    End Sub

    ' ===============================================================
    ' Wuensche
    ' ===============================================================

    ''' <summary>Der Paar-Picker aus 6.11: genau zwei Kinder. Das Modell
    ''' erlaubt mehr, gewertet werden Paare - die Maske darf deshalb
    ''' nicht mehr versprechen.</summary>
    <TestMethod>
    Public Sub WunschHaeltGenauZweiPlaetze()
        Dim p = Projekt()
        Dim m = Modelle(p)
        m.Wuensche.Neu()
        Dim w = p.Klassenbildung.Wuensche.Single()

        m.Wuensche.SetzeKind(w, 1, "S002")

        Assert.AreEqual(2, w.Kinder.Count, "der erste Platz muss auch dann existieren")
        Assert.IsNull(w.Kinder(0))
        Assert.AreEqual("S002", w.Kinder(1))

        m.Wuensche.SetzeKind(w, 0, "S001")
        Assert.AreEqual("S001", m.Wuensche.Kind(w, 0))

        ' Ausserhalb des Paares passiert nichts.
        m.Wuensche.SetzeKind(w, 5, "S003")
        Assert.AreEqual(2, w.Kinder.Count)
    End Sub

    ' ===============================================================
    ' Fixierungen
    ' ===============================================================

    <TestMethod>
    Public Sub FixierungZeigtKindUndZielInKlartext()
        Dim p = Projekt()
        p.Klassenbildung.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S001", .Klasse = 1})
        p.Klassenbildung.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S002", .NichtKlasse = 2})
        Dim m = Modelle(p)

        Dim texte = p.Klassenbildung.Fixierungen.Select(AddressOf m.Fix.Zeilentext).ToList()
        StringAssert.Contains(texte(0), "Klasse 1")
        StringAssert.Contains(texte(1), "NICHT Klasse 2")
    End Sub

    ''' <summary>Klasse und NichtKlasse schliessen einander aus - beides
    ''' gleichzeitig waere eine Fixierung, die sich selbst
    ''' widerspricht.</summary>
    <TestMethod>
    Public Sub ZielSetzenLoeschtDasGegenteil()
        Dim p = Projekt()
        Dim f As New KlassenbildungFixierung With {.Kind = "S001", .NichtKlasse = 2}
        p.Klassenbildung.Fixierungen.Add(f)
        Dim m = Modelle(p)

        m.Fix.SetzeZiel(f, klasse:=1, nichtKlasse:=2)

        Assert.AreEqual(1, f.Klasse)
        Assert.IsFalse(f.NichtKlasse.HasValue)
    End Sub

    ''' <summary>"Herkunft aus dem Audit-Log" (6.11) - und zwar ehrlich:
    ''' das Protokoll fuehrt Board-Uebernahmen als SAMMELZEILE, nicht je
    ''' Kind. Eine Herkunft je Zeile vorzugaukeln waere eine
    ''' Erfindung.</summary>
    <TestMethod>
    Public Sub HerkunftNenntDieJuengsteProtokollzeileOderSagtDassEsKeineGibt()
        Dim p = Projekt()
        Dim m = Modelle(p)
        StringAssert.Contains(m.Fix.HerkunftHinweis(), "Keine Übernahme aus dem Board")

        p.Protokolliere("wer", "fixierung", "Aus dem Board uebernommen: 12 Fixierung(en)",
                        New DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero))
        Dim m2 = Modelle(p)
        StringAssert.Contains(m2.Fix.HerkunftHinweis(), "12 Fixierung(en)")
        StringAssert.Contains(m2.Fix.HerkunftHinweis(), "26.08.2026")
    End Sub

    ' ===============================================================
    ' Querschnitt
    ' ===============================================================

    ''' <summary>Alle vier pruefen mit derselben Kern-API. Eine eigene
    ''' Pruefung je Maske waere eine zweite Meinung darueber, was ein
    ''' gueltiges Regelwerk ist.</summary>
    <TestMethod>
    Public Sub AlleVierPruefenMitDerKernApi()
        Dim p = Projekt()
        p.Klassenbildung.Gruppen.Add(New KlassenbildungGruppe With {
            .Id = "G_x", .Typ = "buendelung", .Mitglieder = New List(Of String) From {"GIBTESNICHT"}})
        Dim m = Modelle(p)

        Dim erwartet = Klassenbildung.ValidateKlassenbildung(p.Klassenbildung)
        Assert.IsTrue(erwartet.Count > 0, "Testgrundlage: der Bestand muss fehlerhaft sein")
        CollectionAssert.AreEqual(erwartet, m.Gruppen.Pruefe())
        CollectionAssert.AreEqual(erwartet, m.Balance.Pruefe())
        CollectionAssert.AreEqual(erwartet, m.Wuensche.Pruefe())
        CollectionAssert.AreEqual(erwartet, m.Fix.Pruefe())
    End Sub

    ''' <summary>Kinder erscheinen als Klarname, gespeichert wird die Id -
    ''' der Weg laeuft ueber mapping.json, das eingebettete JSON bleibt
    ''' pseudonym (Datenhaltung 6.1/6.2).</summary>
    <TestMethod>
    Public Sub KinderErscheinenMitKlarnamenAberGespeichertWirdDieId()
        Dim p = Projekt()
        p.Mapping.Add(New MappingEintrag With {.Id = "S001", .Vorname = "Mia", .Nachname = "Meier"})
        Dim m = Modelle(p)
        m.Gruppen.Neu()
        Dim g = p.Klassenbildung.Gruppen.Single()
        m.Gruppen.SetzeMitglied(g, "S001", True)

        StringAssert.Contains(m.Gruppen.Anzeige("S001"), "Mia")
        CollectionAssert.AreEqual(New List(Of String) From {"S001"}, g.Mitglieder,
                                  "im Bestand steht die Id, nicht der Name")
    End Sub

    <TestMethod>
    Public Sub AenderungenMeldenSichAnDieHuelle()
        Dim p = Projekt()
        Dim m = Modelle(p)
        Dim gemeldet = 0
        AddHandler m.Gruppen.Geaendert, Sub() gemeldet += 1

        m.Gruppen.Neu()
        Assert.IsTrue(gemeldet > 0, "ohne Meldung bliebe der Ungespeichert-Indikator aus")
    End Sub

End Class
