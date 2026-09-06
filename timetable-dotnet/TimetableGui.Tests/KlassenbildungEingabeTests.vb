' Klassenbildungs-Eingaben, Stufe F4 (gui-ui-konzept.md 6.11).
'
' Schwerpunkt: der Zwischenablage-Import. Dort treffen Klarnamen und
' Pseudonymitaetsgrenze aufeinander - "Klarnamen nur in der
' Anzeigeschicht" (Konzept 1), gespeichert wird pseudonym. Ein Import,
' der das verwechselt, schreibt Namen in die Rechendaten, und das faellt
' erst beim Export auf.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class KlassenbildungEingabeTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 23, 20, 0, 0, TimeSpan.Zero)

    Private Shared Function LeeresProjekt() As Projekt
        Dim p As New Projekt()
        p.Klassenbildung.Klassen.Anzahl = 4
        p.Klassenbildung.Klassen.MinGroesse = 22
        p.Klassenbildung.Klassen.MaxGroesse = 26
        p.Klassenbildung.Klassen.Stufe = 1
        Return p
    End Function

    ' ===============================================================
    ' Zerlegen des eingefuegten Textes
    ' ===============================================================

    ''' <summary>Tabulator zuerst: wer aus einer Tabellenkalkulation
    ''' kopiert, bekommt Tabulatoren - und "Meier, Anna" wuerde bei
    ''' Komma-Vorrang mitten im Namen zerrissen.</summary>
    ''' <summary>Bis Stufe G5 galt: alles ausser Nachname und Vorname
    ''' wird Attribut. Seither entscheidet der Nutzer je Spalte, und die
    ''' Vorgabe ist VERWERFEN (9.1, Datenminimierung). Diese Tests meinen
    ''' weiterhin das alte Verhalten - sie sagen es jetzt ausdruecklich,
    ''' statt sich auf eine Vorgabe zu verlassen, die es nicht mehr gibt.</summary>
    Private Shared Function AlleAlsAttribut(v As KlassenbildungEingabeViewModel.ImportVorschau,
                                            nachname As Integer, vorname As Integer) As List(Of Spaltenwahl)
        Dim wahlen As New List(Of Spaltenwahl)
        For i = 0 To v.Spalten.Count - 1
            Dim rolle = Spaltenrolle.Attribut
            If i = nachname Then rolle = Spaltenrolle.Nachname
            If i = vorname Then rolle = Spaltenrolle.Vorname
            wahlen.Add(New Spaltenwahl With {.Name = v.Spalten(i), .Rolle = rolle})
        Next
        Return wahlen
    End Function

    <TestMethod>
    Public Sub TabulatorSchlaegtKomma()
        Dim text = "Meier, Anna" & vbTab & "SOZ" & vbLf & "Schulz, Ben" & vbTab & "FOE"
        Assert.AreEqual(vbTab(0), ZeilenImport.Trennzeichen(text))

        Dim zeilen = ZeilenImport.Zerlege(text)
        Assert.AreEqual(2, zeilen.Count)
        Assert.AreEqual("Meier, Anna", zeilen(0)(0), "Der Name wurde am Komma zerrissen.")
        Assert.AreEqual("SOZ", zeilen(0)(1))
    End Sub

    ''' <summary>Ein Semikolon in einer von vielen Zeilen ist eher Teil
    ''' eines Namens als eine Spaltengrenze - deshalb entscheidet die
    ''' MEHRHEIT der Zeilen.</summary>
    <TestMethod>
    Public Sub EinAusreisserBestimmtNichtDasTrennzeichen()
        Dim zeilen = New List(Of String) From {"Anna", "Ben", "Clara; die Zweite", "Dora", "Emil"}
        Dim text = String.Join(vbLf, zeilen)

        Dim v = ZeilenImport.Zerlege(text)
        Assert.AreEqual(5, v.Count)
        Assert.AreEqual("Clara; die Zweite", v(2)(0),
                        "Ein einzelnes Semikolon darf keine Spaltengrenze werden.")
    End Sub

    <TestMethod>
    Public Sub AlleZeilenBekommenDieselbeSpaltenzahl()
        Dim text = "a;b;c" & vbLf & "d;e" & vbLf & "f"
        Dim zeilen = ZeilenImport.Zerlege(text)

        Assert.IsTrue(zeilen.All(Function(z) z.Length = 3),
                      "Sonst muesste jeder Aufrufer bei jedem Zugriff die Laenge pruefen.")
        Assert.AreEqual("", zeilen(2)(1))
    End Sub

    <TestMethod>
    Public Sub AnfuehrungszeichenUmFelderFallenWeg()
        Dim text = """Meier; Anna"";SOZ"
        Dim zeilen = ZeilenImport.Zerlege(text, ";"c)
        Assert.AreEqual("Meier; Anna", zeilen(0)(0))
        Assert.AreEqual("SOZ", zeilen(0)(1))
    End Sub

    <TestMethod>
    Public Sub EineKopfzeileWirdErkanntAberNurVorgeschlagen()
        Dim mitKopf = "Nachname;Vorname;Betreuung" & vbLf & "Meier;Anna;SOZ" & vbLf & "Schulz;Ben;"
        Assert.IsTrue(ZeilenImport.SiehtNachKopfzeileAus(ZeilenImport.Zerlege(mitKopf)))

        ' Steht in der ersten Zeile eine Zahl, ist es eher ein Datensatz.
        Dim ohneKopf = "Meier;Anna;3" & vbLf & "Schulz;Ben;4"
        Assert.IsFalse(ZeilenImport.SiehtNachKopfzeileAus(ZeilenImport.Zerlege(ohneKopf)))
    End Sub

    ' ===============================================================
    ' Import: die Pseudonymitaetsgrenze
    ' ===============================================================

    ''' <summary>DER Test dieser Stufe. Nach dem Import stehen in der
    ''' Rechenliste NUR IDs und Attribute; die Namen liegen
    ''' ausschliesslich im Mapping (Datenhaltung 6.1).</summary>
    <TestMethod>
    Public Sub NachDemImportStehenKeineNamenInDenRechendaten()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        Dim text = "Nachname;Vorname;Betreuung;Wohngebiet" & vbLf &
                   "Meier;Anna;SOZ;Nord" & vbLf &
                   "Schulz;Ben;;Sued"

        Dim v = vm.ImportPruefen(text)
        Assert.IsTrue(v.Kopfzeile)
        Assert.AreEqual(2, v.Datensaetze)

        Dim n = vm.ImportUebernehmen(v, AlleAlsAttribut(v, 0, 1)).Kinder
        Assert.AreEqual(2, n)

        ' Kein Name in der Einschulungsliste - weder als Id noch als Attribut.
        For Each kind In p.Klassenbildung.Schueler
            StringAssert.Matches(kind.Id, New Text.RegularExpressions.Regex("^S\d{3}$"))
            For Each kv In kind.Attribute
                Assert.IsFalse(kv.Value.Contains("Meier") OrElse kv.Value.Contains("Anna"),
                               $"Klarname im Attribut {kv.Key}: {kv.Value}")
            Next
        Next

        ' Die Namen stehen im Mapping - und nur dort.
        Assert.AreEqual(2, p.Mapping.Count)
        Assert.IsTrue(p.Mapping.Any(Function(m) m.Nachname = "Meier" AndAlso m.Vorname = "Anna"))
    End Sub

    ''' <summary>Die uebrigen Spalten werden ATTRIBUTE mit dem
    ''' Spaltennamen als Schluessel - genau das Vokabular, auf das
    ''' Balance und Gruppen danach zugreifen, ohne dass jemand es vorher
    ''' anlegen muss.</summary>
    <TestMethod>
    Public Sub DieUebrigenSpaltenWerdenZumAttributVokabular()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        Dim text = "Nachname;Vorname;Betreuung;Wohngebiet" & vbLf &
                   "Meier;Anna;SOZ;Nord" & vbLf &
                   "Schulz;Ben;;Sued"

        Dim vAttr = vm.ImportPruefen(text)
        vm.ImportUebernehmen(vAttr, AlleAlsAttribut(vAttr, 0, 1))

        CollectionAssert.AreEquivalent({"Betreuung", "Wohngebiet"}, vm.Attributnamen,
                                       "Erwartet wurden die Spaltennamen als Vokabular.")
        CollectionAssert.AreEquivalent({"Nord", "Sued"}, vm.Werte("Wohngebiet"))
        ' Ben hat kein Betreuungs-Attribut - ein leeres Feld ist kein Wert.
        CollectionAssert.AreEquivalent({"SOZ"}, vm.Werte("Betreuung"))
    End Sub

    ''' <summary>Ohne Klarnamen entsteht KEIN Mapping-Eintrag: ein
    ''' anonymer Platzhalter hat keinen Personenbezug (6.8).</summary>
    <TestMethod>
    Public Sub AnonymeZeilenErzeugenKeinenMappingEintrag()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        Dim text = "Betreuung;Wohngebiet" & vbLf & "SOZ;Nord" & vbLf & "FOE;Sued"

        Dim vOhne = vm.ImportPruefen(text)
        vm.ImportUebernehmen(vOhne, AlleAlsAttribut(vOhne, -1, -1))

        Assert.AreEqual(2, p.Klassenbildung.Schueler.Count)
        Assert.AreEqual(0, p.Mapping.Count, "Ohne Namen darf kein Personenbezug entstehen.")
    End Sub

    ''' <summary>IDs zaehlen nur aufwaerts und werden nie wiederverwendet
    ''' (Datenhaltung 6.1) - auch nicht nach dem Loeschen.</summary>
    <TestMethod>
    Public Sub GeloeschteIdsBleibenVerbrannt()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        Dim erste = vm.Hinzufuegen("Meier", "Anna", Nothing)
        vm.Entfernen(erste)
        Dim zweite = vm.Hinzufuegen("Schulz", "Ben", Nothing)

        Assert.AreNotEqual(erste, zweite, "Eine geloeschte Id darf nie wieder vergeben werden.")
    End Sub

    ''' <summary>Ein geloeschtes Kind darf nirgends als Verweis
    ''' zuruecklassen - sonst zeigt eine Gruppe auf niemanden.</summary>
    <TestMethod>
    Public Sub LoeschenNimmtGruppenWuenscheUndFixierungenMit()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        Dim a = vm.Hinzufuegen("Meier", "Anna", Nothing)
        Dim b = vm.Hinzufuegen("Schulz", "Ben", Nothing)
        p.Klassenbildung.Gruppen.Add(New KlassenbildungGruppe With {
            .Id = "K1", .Typ = "buendelung", .Mitglieder = New List(Of String) From {a, b}})
        p.Klassenbildung.Wuensche.Add(New KlassenbildungWunsch With {
            .Typ = "zusammen", .Kinder = New List(Of String) From {a, b}})
        p.Klassenbildung.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = a, .Klasse = 1})

        vm.Entfernen(a)

        Assert.IsFalse(p.Klassenbildung.Gruppen(0).Mitglieder.Contains(a))
        Assert.AreEqual(0, p.Klassenbildung.Wuensche.Count, "Ein Wunsch mit einem Kind ist keiner.")
        Assert.AreEqual(0, p.Klassenbildung.Fixierungen.Count)
        Assert.IsFalse(p.Mapping.Any(Function(m) m.Id = a))
    End Sub

    ' ===============================================================
    ' Klassenrahmen
    ' ===============================================================

    <TestMethod>
    Public Sub DerRahmenMeldetZuWenigUndZuVielPlatz()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        For i = 1 To 200
            vm.Hinzufuegen("", "", Nothing)
        Next

        StringAssert.Contains(vm.RahmenZeile, "keinen Platz",
                              "200 Kinder passen nicht in 4 x 26: " & vm.RahmenZeile)

        p.Klassenbildung.Schueler.RemoveRange(10, 190)
        StringAssert.Contains(vm.RahmenZeile, "fehlen",
                              "10 Kinder fuellen keine 4 Klassen zu je 22: " & vm.RahmenZeile)
    End Sub

    ''' <summary>Stufe ODER Labels (6.11) - wer Labels setzt, hat die
    ''' Namen entschieden; eine Stufe daneben waere eine zweite,
    ''' womoeglich widersprechende Quelle derselben Namen.</summary>
    <TestMethod>
    Public Sub LabelsUndStufeSchliessenEinanderAus()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        Assert.IsTrue(vm.Stufe.HasValue)

        vm.Labels = New List(Of String) From {"1a", "1b", "1c", "1d"}
        Assert.IsFalse(vm.Stufe.HasValue, "Labels muessen die Stufe abraeumen.")

        vm.Stufe = 2
        Assert.IsNull(vm.Labels, "Eine Stufe muss die Labels abraeumen.")
    End Sub

    ''' <summary>Die Vorschau nutzt dieselbe Ableitung wie der Kern -
    ''' die Labels im Board muessen genau so heissen.</summary>
    <TestMethod>
    Public Sub DieLabelVorschauStimmtMitDemKernUeberein()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())

        Assert.AreEqual(String.Join(", ", Klassenbildung.KlassenLabels(p.Klassenbildung)),
                        vm.LabelVorschau)
        StringAssert.Contains(vm.LabelVorschau, "1a")
    End Sub

    ' ===============================================================
    ' Mehrere Kinder entfernen, leere Regeln
    ' ===============================================================

    ''' <summary>Drei Kinder auf einmal weg - mit EINER Meldung. Eine je
    ''' Kind liesse das Fenster dreimal neu zeichnen und speichern.</summary>
    <TestMethod>
    Public Sub MehrereKinderVerschwindenMitEinerMeldung()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        Dim a = vm.Hinzufuegen("Meier", "Anna", Nothing)
        Dim b = vm.Hinzufuegen("Schulz", "Ben", Nothing)
        Dim c = vm.Hinzufuegen("Braun", "Cem", Nothing)
        Dim meldungen = 0
        AddHandler vm.Geaendert, Sub() meldungen += 1

        vm.Entfernen({a, b})

        Assert.AreEqual(1, meldungen)
        CollectionAssert.AreEqual(New List(Of String) From {c}, p.Klassenbildung.Schueler.Select(Function(s) s.Id).ToList())
        Assert.AreEqual(1, p.Mapping.Count, "die Klarnamen der beiden gehen mit")
        Assert.AreEqual(c, p.Mapping(0).Id)

        vm.AlleEntfernen()
        Assert.AreEqual(0, p.Klassenbildung.Schueler.Count)
        Assert.AreEqual(0, p.Mapping.Count)
        Assert.AreEqual(2, meldungen)

        vm.Entfernen({"S999"})
        Assert.AreEqual(2, meldungen, "ein unbekanntes Kind aendert nichts und meldet nichts")
    End Sub

    ''' <summary>Regeln ohne Kind werden ERKANNT, aber nur auf Wunsch
    ''' entfernt - eine Gruppe mit verbliebenem Mitglied bleibt.</summary>
    <TestMethod>
    Public Sub LeereRegelnWerdenErkanntUndNurAufWunschEntfernt()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        Dim a = vm.Hinzufuegen("Meier", "Anna", New Dictionary(Of String, String) From {{"foe", "ja"}, {"geschlecht", "w"}})
        Dim b = vm.Hinzufuegen("Schulz", "Ben", New Dictionary(Of String, String) From {{"geschlecht", "m"}})
        Dim c = vm.Hinzufuegen("Braun", "Cem", New Dictionary(Of String, String) From {{"geschlecht", "m"}})
        p.Klassenbildung.Gruppen.Add(New KlassenbildungGruppe With {
            .Id = "K1", .Typ = "buendelung", .Mitglieder = New List(Of String) From {a, b}})
        p.Klassenbildung.Gruppen.Add(New KlassenbildungGruppe With {
            .Id = "K2", .Typ = "buendelung", .Mitglieder = New List(Of String) From {c}})
        p.Klassenbildung.Balance.Add(New KlassenbildungBalance With {.Attribut = "foe", .Wert = "ja"})
        p.Klassenbildung.Balance.Add(New KlassenbildungBalance With {.Attribut = "geschlecht", .Wert = "w"})
        p.Klassenbildung.Balance.Add(New KlassenbildungBalance With {.Attribut = "geschlecht", .Wert = "m"})
        Assert.AreEqual(0, vm.LeereRegeln().Count)

        vm.Entfernen({a, b})

        Dim leere = vm.LeereRegeln()
        Assert.AreEqual(3, leere.Count, String.Join(" | ", leere))
        Assert.IsTrue(leere.Any(Function(l) l.Contains("K1") AndAlso l.Contains("kein Mitglied")))
        Assert.IsTrue(leere.Any(Function(l) l.Contains("foe = ja")))
        Assert.IsTrue(leere.Any(Function(l) l.Contains("geschlecht = w")))
        Assert.IsFalse(leere.Any(Function(l) l.Contains("K2")), "K2 hat noch ein Mitglied")
        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains("geschlecht = w") AndAlso f.Contains("wirkungslos")),
                      "die Pruefung nennt die verwaiste Balance")
        ' Nichts ist ohne Entscheidung verschwunden.
        Assert.AreEqual(2, p.Klassenbildung.Gruppen.Count)
        Assert.AreEqual(3, p.Klassenbildung.Balance.Count)

        Assert.AreEqual(3, vm.LeereRegelnEntfernen())
        Assert.AreEqual("K2", p.Klassenbildung.Gruppen.Single().Id)
        Assert.AreEqual("m", p.Klassenbildung.Balance.Single().Wert)
        Assert.AreEqual(0, vm.LeereRegelnEntfernen(), "ein zweiter Aufruf findet nichts mehr")
    End Sub

    ' ===============================================================
    ' Rahmenvorschlag nach dem Import
    ' ===============================================================

    ''' <summary>Die vier Beispiele aus dem Plan: Hoechstgroesse ist der
    ''' Klassenteiler, die Anzahl die kleinste mit Platz, die Mindestgroesse
    ''' sechs darunter, aber nie ueber dem Durchschnitt.</summary>
    <TestMethod>
    Public Sub DerRahmenvorschlagKenntDieVierBeispiele()
        For Each fall In {(100, 4, 22), (116, 5, 22), (57, 3, 19), (30, 2, 15)}
            Dim v = KlassenbildungEingabeViewModel.RahmenBerechnen(fall.Item1, 28, 0, 0, 0)
            Assert.AreEqual(fall.Item2, v.Anzahl.Value, $"Anzahl bei {fall.Item1}")
            Assert.AreEqual(fall.Item3, v.MinGroesse.Value, $"Min bei {fall.Item1}")
            Assert.AreEqual(28, v.MaxGroesse.Value)
            ' Der Rahmen ist auf Anhieb rechenbar.
            Assert.IsTrue(v.Anzahl.Value * v.MinGroesse.Value <= fall.Item1)
            Assert.IsTrue(v.Anzahl.Value * v.MaxGroesse.Value >= fall.Item1)
        Next
        Assert.IsTrue(KlassenbildungEingabeViewModel.RahmenBerechnen(0, 28, 0, 0, 0).Leer)
    End Sub

    ''' <summary>Nur leere Felder werden gefuellt; ein gesetztes geht in
    ''' die uebrigen ein - eine Anzahl von 5 bestimmt den Durchschnitt.</summary>
    <TestMethod>
    Public Sub DerRahmenvorschlagFuelltNurLeereFelder()
        Dim p As New Projekt()
        p.Klassenbildung.Klassen.Anzahl = 5
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        For i = 1 To 100
            vm.Hinzufuegen("", "", Nothing)
        Next

        Dim v = vm.RahmenVorschlagen()

        Assert.IsFalse(v.Anzahl.HasValue, "die gesetzte Anzahl bleibt")
        Assert.AreEqual(5, p.Klassenbildung.Klassen.Anzahl)
        Assert.AreEqual(20, p.Klassenbildung.Klassen.MinGroesse, "100/5 = 20 liegt unter 28-6")
        Assert.AreEqual(28, p.Klassenbildung.Klassen.MaxGroesse)
        StringAssert.Contains(v.Hinweistext("Grundschule", "BW"), "Klassenteiler Grundschule/BW: 28")
        StringAssert.Contains(v.Hinweistext("Grundschule", "BW"), "Mindestgröße 20")
        Assert.IsFalse(v.Hinweistext("Grundschule", "BW").Contains("Klassenanzahl"), "nur Gesetztes wird genannt")

        Assert.IsNull(vm.RahmenVorschlagen(), "alles gesetzt: nichts mehr vorzuschlagen")

        Dim leer As New Projekt()
        Assert.IsNull(New KlassenbildungEingabeViewModel(leer, New TestDialoge()).RahmenVorschlagen(),
                      "ohne Kinder kein Vorschlag")
        Assert.AreEqual(0, leer.Klassenbildung.Klassen.Anzahl)
    End Sub

    ''' <summary>Der Import eines frischen Projekts nennt den Vorschlag im
    ''' Bericht; ein zweiter Import findet nichts Leeres mehr.</summary>
    <TestMethod>
    Public Sub DerImportberichtNenntDenRahmenvorschlag()
        Dim p As New Projekt()
        p.Bestand.Schulart = "Gemeinschaftsschule"
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        Dim text = "Nachname;Vorname" & vbLf & "Meier;Anna" & vbLf & "Schulz;Ben" & vbLf & "Braun;Cem"

        Dim v1 = vm.ImportPruefen(text)
        Dim bericht = vm.ImportUebernehmen(v1, Spaltenzuordnung.Vorschlag(v1.Spalten))

        StringAssert.Contains(bericht.Klartext(), "Klassenteiler Gemeinschaftsschule/BW: 28")
        StringAssert.Contains(bericht.Klartext(), "Klassenanzahl 1")
        StringAssert.Contains(bericht.Klartext(), "Höchstgröße 28")
        Assert.AreEqual(1, p.Klassenbildung.Klassen.Anzahl)
        Assert.AreEqual(3, p.Klassenbildung.Klassen.MinGroesse, "3/1 = 3 liegt unter 22")

        Dim v2 = vm.ImportPruefen(text)
        Dim zweiter = vm.ImportUebernehmen(v2, Spaltenzuordnung.Vorschlag(v2.Spalten))
        Assert.IsFalse(zweiter.Klartext().Contains("Klassenrahmen"), "nichts mehr leer, nichts vorgeschlagen")
        Assert.AreEqual(1, p.Klassenbildung.Klassen.Anzahl, "der gesetzte Rahmen bleibt, auch wenn er nun zu klein ist")
    End Sub

    ' ===============================================================
    ' Pruefung
    ' ===============================================================

    ''' <summary>Eine Balance auf ein Attribut, das kein Kind traegt -
    ''' genau der Fall, den die Auswahlliste aus dem Vokabular
    ''' verhindern soll.</summary>
    <TestMethod>
    Public Sub EineBalanceAufUnbekanntesAttributFaelltAuf()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        Dim vB = vm.ImportPruefen("Betreuung" & vbLf & "SOZ" & vbLf & "FOE")
        vm.ImportUebernehmen(vB, AlleAlsAttribut(vB, -1, -1))
        p.Klassenbildung.Balance.Add(New KlassenbildungBalance With {.Attribut = "Geschlecht", .Wert = "w"})

        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains("Geschlecht")),
                      String.Join(" | ", vm.Pruefe()))
    End Sub

    <TestMethod>
    Public Sub EinWunschBrauchtGenauZweiKinder()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        Dim a = vm.Hinzufuegen("", "", Nothing)
        p.Klassenbildung.Wuensche.Add(New KlassenbildungWunsch With {
            .Typ = "zusammen", .Kinder = New List(Of String) From {a}})

        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains("genau zwei")),
                      String.Join(" | ", vm.Pruefe()))
    End Sub

End Class
