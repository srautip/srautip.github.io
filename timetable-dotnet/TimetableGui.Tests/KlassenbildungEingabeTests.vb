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

        Dim n = vm.ImportUebernehmen(v, nachnameSpalte:=0, vornameSpalte:=1)
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

        vm.ImportUebernehmen(vm.ImportPruefen(text), 0, 1)

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

        vm.ImportUebernehmen(vm.ImportPruefen(text), nachnameSpalte:=-1, vornameSpalte:=-1)

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
    ' Pruefung
    ' ===============================================================

    ''' <summary>Eine Balance auf ein Attribut, das kein Kind traegt -
    ''' genau der Fall, den die Auswahlliste aus dem Vokabular
    ''' verhindern soll.</summary>
    <TestMethod>
    Public Sub EineBalanceAufUnbekanntesAttributFaelltAuf()
        Dim p = LeeresProjekt()
        Dim vm As New KlassenbildungEingabeViewModel(p, New TestDialoge())
        vm.ImportUebernehmen(vm.ImportPruefen("Betreuung" & vbLf & "SOZ" & vbLf & "FOE"), -1, -1)
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
