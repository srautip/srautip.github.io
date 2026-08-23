' Eingabemasken, Stufe F1 (gui-ui-konzept.md 6).
'
' Geprueft wird ueber die ViewModels, nicht ueber XAML - so verlangt es
' der Plan fuer Stufe F, und so bleibt es ohne Fenster pruefbar
' (arc42 8.13). Was die Masken zusichern, ist damit hier zusagbar:
' das Grundmuster Neu/Duplizieren/Loeschen/Pruefen, die Kaskade beim
' Umbenennen, und die Auswahllogik des Rasterpickers.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class MaskenTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 23, 17, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttmk-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(_ordner)
    End Sub

    <TestCleanup>
    Public Sub Abraeumen()
        If IO.Directory.Exists(_ordner) Then IO.Directory.Delete(_ordner, recursive:=True)
    End Sub

    Private Shared ReadOnly Property TestsRoot As String
        Get
            Dim dir = New IO.DirectoryInfo(AppContext.BaseDirectory)
            While dir IsNot Nothing
                Dim kandidat = IO.Path.Combine(dir.FullName, "tests", "bw-grundschule-beispiel")
                If IO.Directory.Exists(kandidat) Then Return IO.Path.Combine(dir.FullName, "tests")
                dir = dir.Parent
            End While
            Throw New InvalidOperationException("tests/-Verzeichnis nicht gefunden")
        End Get
    End Property

    Private Function Beispielprojekt() As Projekt
        Dim d As New TestDialoge With {
            .OrdnerPfad = IO.Path.Combine(TestsRoot, "bw-grundschule-beispiel"),
            .SpeichernPfad = IO.Path.Combine(_ordner, "gs.splanx")}
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Importieren()
        Return m.Projekt
    End Function

    ' ===============================================================
    ' Grundmuster: Neu . Duplizieren . Loeschen . Pruefen
    ' ===============================================================

    <TestMethod>
    Public Sub NeuLegtAnUndWaehltAus()
        Dim p = Beispielprojekt()
        Dim vm As New RaeumeViewModel(p, New TestDialoge())
        Dim vorher = p.Bestand.Raeume.Count

        vm.Neu()

        Assert.AreEqual(vorher + 1, p.Bestand.Raeume.Count)
        Assert.IsNotNull(vm.Auswahl, "Nach Neu muss der neue Eintrag ausgewaehlt sein.")
        Assert.AreEqual("Neuer Raum", vm.Auswahl.Name)
    End Sub

    ''' <summary>Namen sind Schluessel (arc42 8.15) - zwei gleichnamige
    ''' Raeume waeren im Wire-Format nicht unterscheidbar. Deshalb zaehlt
    ''' die Maske hoch, statt es dem Nutzer zu ueberlassen.</summary>
    <TestMethod>
    Public Sub ZweimalNeuGibtNichtZweimalDenselbenNamen()
        Dim p = Beispielprojekt()
        Dim vm As New RaeumeViewModel(p, New TestDialoge())

        vm.Neu()
        vm.Neu()

        Dim namen = p.Bestand.Raeume.Select(Function(r) r.Name).ToList()
        Assert.AreEqual(namen.Count, namen.Distinct(StringComparer.CurrentCultureIgnoreCase).Count(),
                        "Doppelte Raumnamen: " & String.Join(", ", namen))
        Assert.IsTrue(namen.Contains("Neuer Raum (2)"), "Erwartet wurde eine hochgezaehlte Zweitfassung.")
    End Sub

    <TestMethod>
    Public Sub DuplizierenUebernimmtDieFelderAberNichtDenNamen()
        Dim p = Beispielprojekt()
        p.Bestand.Raeume.Add(New Raum With {.Name = "Turnhalle", .Typ = "Sport"})
        Dim vm As New RaeumeViewModel(p, New TestDialoge())
        vm.Auswahl = p.Bestand.Raeume.First(Function(r) r.Name = "Turnhalle")

        vm.Duplizieren()

        Assert.AreEqual("Turnhalle (2)", vm.Auswahl.Name)
        Assert.AreEqual("Sport", vm.Auswahl.Typ, "Das Duplikat hat die uebrigen Felder verloren.")
    End Sub

    ''' <summary>"niemals stilles Verwaisen von Referenzen" (Konzept 7).
    ''' Der Test belegt beides: dass gefragt wird, und dass ein Nein
    ''' wirklich nichts aendert.</summary>
    <TestMethod>
    Public Sub LoeschenFragtErstUndEinNeinAendertNichts()
        Dim p = Beispielprojekt()
        p.Bestand.Raeume.Add(New Raum With {.Name = "Turnhalle", .Typ = "Sport"})
        Dim d As New TestDialoge With {.FrageAntwort = False}
        Dim vm As New RaeumeViewModel(p, d)
        vm.Auswahl = p.Bestand.Raeume.First(Function(r) r.Name = "Turnhalle")

        vm.Loeschen()

        Assert.AreEqual(1, d.Fragen.Count, "Es wurde nicht gefragt.")
        Assert.IsTrue(p.Bestand.Raeume.Any(Function(r) r.Name = "Turnhalle"),
                      "Trotz Nein wurde geloescht.")
    End Sub

    <TestMethod>
    Public Sub LoeschenNenntDieVerweiseImKlartext()
        Dim p = Beispielprojekt()
        p.Bestand.Raeume.Add(New Raum With {.Name = "Turnhalle", .Typ = "Sport"})
        p.Constraints.Add(Text.Json.Nodes.JsonNode.Parse(
            "{""type"":""room_requirement"",""subject"":""Sport"",""allowed_rooms"":[""Turnhalle""]}").AsObject())

        Dim d As New TestDialoge With {.FrageAntwort = True}
        Dim vm As New RaeumeViewModel(p, d)
        vm.Auswahl = p.Bestand.Raeume.First(Function(r) r.Name = "Turnhalle")

        vm.Loeschen()

        Assert.AreEqual(1, d.Fragen.Count)
        StringAssert.Contains(d.Fragen(0), "Turnhalle",
                              "Der Loeschdialog nennt den Namen nicht: " & d.Fragen(0))
        StringAssert.Contains(d.Fragen(0), "verwendet",
                              "Der Loeschdialog nennt die Verweise nicht: " & d.Fragen(0))
        Assert.IsFalse(p.Bestand.Raeume.Any(Function(r) r.Name = "Turnhalle"))
    End Sub

    ''' <summary>Die Zusage des Plans fuer Stufe F: was die Maske
    ''' schreibt, muss die Validate*-API des Kerns gruen sehen. Nicht
    ''' eine maskeneigene Zweitpruefung.</summary>
    <TestMethod>
    Public Sub WasDieMaskeAnlegtBleibtValide()
        Dim p = Beispielprojekt()
        Dim vm As New RaeumeViewModel(p, New TestDialoge())

        vm.Neu()
        vm.Auswahl.Name = "Musiksaal"
        vm.Auswahl.Typ = "Musik"
        vm.Aktualisiere()

        Assert.AreEqual(0, vm.Pruefe().Count, String.Join(" | ", vm.Pruefe()))
        Assert.AreEqual(0, StammdatenValidation.ValidateStammdaten(p.Bestand).Count,
                        String.Join(" | ", StammdatenValidation.ValidateStammdaten(p.Bestand)))
    End Sub

    <TestMethod>
    Public Sub DieMaskeFindetDoppelteNamen()
        Dim p = Beispielprojekt()
        p.Bestand.Raeume.Add(New Raum With {.Name = "Halle"})
        p.Bestand.Raeume.Add(New Raum With {.Name = "halle"})
        Dim vm As New RaeumeViewModel(p, New TestDialoge())

        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains("mehrfach")),
                      "Zwei Raeume mit gleichem Namen sind im Wire-Format nicht unterscheidbar.")
    End Sub

    <TestMethod>
    Public Sub DerFilterZeigtNurPassendeUndHaeltDieAuswahl()
        Dim p = Beispielprojekt()
        p.Bestand.Raeume.Clear()
        For Each n In {"Turnhalle", "Musiksaal", "Werkraum"}
            p.Bestand.Raeume.Add(New Raum With {.Name = n})
        Next
        Dim vm As New RaeumeViewModel(p, New TestDialoge())
        Assert.AreEqual(3, vm.Eintraege.Count)

        vm.Auswahl = vm.Eintraege.First(Function(r) r.Name = "Musiksaal")
        vm.Filter = "saal"

        Assert.AreEqual(1, vm.Eintraege.Count)
        Assert.AreEqual("Musiksaal", vm.Auswahl.Name, "Die sichtbare Auswahl darf nicht wegspringen.")
    End Sub

    ''' <summary>Umbenennen kaskadiert (arc42 8.15). Die Maske ruft
    ''' Bestandspflege - eine eigene Suche waere eine zweite Antwort auf
    ''' dieselbe Frage.</summary>
    <TestMethod>
    Public Sub UmbenennenZiehtDieVerweiseMit()
        Dim p = Beispielprojekt()
        p.Bestand.Raeume.Add(New Raum With {.Name = "Turnhalle"})
        p.Constraints.Add(Text.Json.Nodes.JsonNode.Parse(
            "{""type"":""room_requirement"",""subject"":""Sport"",""allowed_rooms"":[""Turnhalle""]}").AsObject())
        Dim vm As New RaeumeViewModel(p, New TestDialoge())

        Assert.AreEqual(1, vm.UmbenennenVorschau("Turnhalle"),
                        "Die Vorschau muss die Tragweite VOR dem Bestaetigen nennen.")
        Dim angepasst = vm.BenenneUm("Turnhalle", "Sporthalle")

        Assert.AreEqual(1, angepasst)
        ' Die Regel per Suche holen, nicht per Index: das importierte
        ' Beispielprojekt bringt eigene Regeln mit, meine steht nicht
        ' an Position 0.
        Dim regel = p.Constraints.First(Function(c) c.ContainsKey("allowed_rooms"))
        Assert.AreEqual("Sporthalle", regel("allowed_rooms")(0).GetValue(Of String)(),
                        "Der Verweis in der Regel wurde nicht mitgezogen.")
    End Sub

    ' ===============================================================
    ' Rasterpicker (6.10, "zentrales Shared Control")
    ' ===============================================================

    Private Shared Function Raster() As RasterAuswahl
        Return New RasterAuswahl({"Mo", "Di", "Mi", "Do", "Fr"}, 6)
    End Function

    <TestMethod>
    Public Sub ZiehenWaehltDasRechteckUnabhaengigVonDerRichtung()
        Dim a = Raster()
        a.Bereich("Di", 2, "Do", 4, True)

        Dim b = Raster()
        b.Bereich("Do", 4, "Di", 2, True)   ' rueckwaerts gezogen

        Assert.AreEqual(9, a.Anzahl, "3 Tage x 3 Stunden erwartet.")
        Assert.AreEqual(a.Anzahl, b.Anzahl, "Die Ziehrichtung darf nichts aendern.")
        Assert.IsTrue(a.IstGewaehlt("Mi", 3))
        Assert.IsFalse(a.IstGewaehlt("Mo", 3), "Mo liegt ausserhalb des Rechtecks.")
        Assert.IsFalse(a.IstGewaehlt("Di", 5), "Stunde 5 liegt ausserhalb des Rechtecks.")
    End Sub

    ''' <summary>Der wichtigste Test des Pickers. `subject_period_window`
    ''' und `occupied_window` koennen nur ein von/bis ausdruecken - eine
    ''' zerklueftete Auswahl still auf ihre Huelle zu runden waere eine
    ''' Regel, die niemand gemeint hat.</summary>
    <TestMethod>
    Public Sub EineLueckenhafteAuswahlIstKeinFenster()
        Dim a = Raster()
        a.Bereich("Mo", 2, "Mi", 4, True)
        Assert.IsNotNull(a.AlsFenster(), "Ein volles Rechteck IST ein Fenster.")

        a.Setze("Di", 3, False)   ' ein Loch hineinschlagen

        Assert.IsNull(a.AlsFenster(),
                      "Eine Auswahl mit Loch darf nicht als Fenster durchgehen.")
        Assert.AreEqual(8, a.AlsSlots().Count, "Als Einzel-Slots ist sie weiterhin ausdrueckbar.")
    End Sub

    <TestMethod>
    Public Sub DasFensterKommtInRasterreihenfolgeZurueck()
        Dim a = Raster()
        a.Bereich("Do", 3, "Do", 5, True)
        a.Bereich("Di", 3, "Di", 5, True)   ' spaeter gewaehlt, aber frueher im Raster

        Dim f = a.AlsFenster()
        Assert.IsNotNull(f)
        CollectionAssert.AreEqual({"Di", "Do"}, f.Value.Tage,
                                  "Die Tage muessen in Rasterreihenfolge stehen, nicht in Auswahlreihenfolge.")
        Assert.AreEqual(3, f.Value.Von)
        Assert.AreEqual(5, f.Value.Bis)
    End Sub

    ''' <summary>Beim Ziehen laeuft der Zeiger regelmaessig ueber den
    ''' Rand. Eine Ausnahme mitten in einer Mausbewegung waere kein
    ''' sinnvolles Verhalten - also still ignorieren.</summary>
    <TestMethod>
    Public Sub AusserhalbDesRastersPassiertNichts()
        Dim a = Raster()
        a.Setze("Sa", 3, True)
        a.Setze("Mo", 99, True)
        a.Setze("Mo", 0, True)
        a.Bereich("Mo", 1, "Sa", 3, True)

        Assert.AreEqual(0, a.Anzahl, "Nichts davon liegt im Raster.")
    End Sub

    <TestMethod>
    Public Sub DieBeschreibungUnterscheidetFensterVonSlots()
        Dim a = Raster()
        Assert.AreEqual("keine Auswahl", a.Beschreibung())

        a.Bereich("Mo", 3, "Di", 5, True)
        StringAssert.Contains(a.Beschreibung(), "Mo, Di")
        StringAssert.Contains(a.Beschreibung(), "3.-5.")

        a.Setze("Mo", 4, False)
        StringAssert.Contains(a.Beschreibung(), "Slots",
                              "Ohne Rechteck darf keine Fensterbeschreibung entstehen.")
    End Sub

    <TestMethod>
    Public Sub SlotsKommenInRasterreihenfolgeZurueck()
        Dim a = Raster()
        a.Setze("Mi", 2, True)
        a.Setze("Mo", 5, True)
        a.Setze("Mo", 1, True)

        Dim s = a.AlsSlots()
        CollectionAssert.AreEqual({"Mo", "Mo", "Mi"}, s.Select(Function(x) x.Tag).ToArray())
        CollectionAssert.AreEqual({1, 5, 2}, s.Select(Function(x) x.Stunde).ToArray())
    End Sub

End Class
