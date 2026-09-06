' Das Klassenbildungs-Fenster als FENSTER (Stufe F6).
'
' Die vier Regelmasken sind neu; ihre Auswahllisten und die
' Mitglieder-Ankreuzfelder entstehen zur Laufzeit. Genau dort sitzt die
' Fehlerklasse, die headless unsichtbar bleibt - siehe Fensterprobe.vb.
'
' Geprueft wird ausserdem der Weg, den KlassenbildungRegelnTests nicht
' sieht: von der Bedienoberflaeche in den Bestand.
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class KlassenbildungFensterTests

    Private Shared Function Projekt(Optional kinder As Integer = 6) As Projekt
        Dim p As New Projekt()
        p.Klassenbildung.Klassen.Anzahl = 2
        p.Klassenbildung.Klassen.MinGroesse = 1
        p.Klassenbildung.Klassen.MaxGroesse = 30
        For i = 1 To kinder
            Dim s As New KlassenbildungSchueler With {.Id = $"S{i:000}"}
            s.Attribute("GESCHLECHT") = If(i Mod 2 = 0, "w", "m")
            p.Klassenbildung.Schueler.Add(s)
        Next
        Return p
    End Function

    Private Shared Sub Klick(k As Button)
        k.RaiseEvent(New RoutedEventArgs(ButtonBase.ClickEvent))
    End Sub

    ''' <summary>Text setzen UND das Feld verlassen. Die Masken
    ''' uebernehmen Textfelder bei LostFocus, nicht bei jedem
    ''' Tastendruck - sonst sortierte sich die Liste nach jedem
    ''' Buchstaben neu und die Auswahl spraenge weg. Ein Test, der nur
    ''' `.Text` setzt, stellt deshalb keine Eingabe nach.</summary>
    Private Shared Sub Tippe(feld As TextBox, text As String)
        feld.Text = text
        feld.RaiseEvent(New RoutedEventArgs(UIElement.LostFocusEvent))
    End Sub

    <TestMethod>
    Public Sub DasFensterBautDieVierRegelmaskenAuf()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim f As New KlassenbildungFenster(Projekt(), New TestDialoge())

                   ' Gefuellte Vorgabelisten - ein leeres Auswahlfeld
                   ' waere ein Feld ohne moegliche Eingabe.
                   Assert.AreEqual(2, f.GruppeTyp.Items.Count)
                   Assert.AreEqual(2, f.WunschTyp.Items.Count)
                   Assert.AreEqual(3, f.GruppePrio.Items.Count)
                   Assert.AreEqual(1, f.BalanceAttribut.Items.Count, "das Vokabular der Kinder")
                   Assert.AreEqual(7, f.WunschKindA.Items.Count, "6 Kinder plus (keins)")

                   ' Ohne Auswahl bleibt das Detailformular aus - sonst
                   ' tippt jemand in Felder, die nirgends ankommen.
                   Assert.IsFalse(f.GruppeDetail.IsEnabled)
                   Assert.IsFalse(f.BalanceDetail.IsEnabled)
                   Assert.IsFalse(f.WunschDetail.IsEnabled)
               End Sub)
    End Sub

    ''' <summary>Der Weg, den das ViewModel allein nicht belegt: Knopf
    ''' druecken, Felder fuellen, Mitglied ankreuzen - und im Bestand
    ''' steht das Richtige.</summary>
    <TestMethod>
    Public Sub EineGruppeEntstehtVollstaendigUeberDieOberflaeche()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   Dim f As New KlassenbildungFenster(p, New TestDialoge())

                   Klick(f.GruppeNeuKnopf)

                   Assert.AreEqual(1, p.Klassenbildung.Gruppen.Count)
                   Assert.IsTrue(f.GruppeDetail.IsEnabled, "nach Neu muss das Formular bedienbar sein")
                   Assert.AreEqual(1, f.GruppenListe.Items.Count)

                   Tippe(f.GruppeId, "G_zwillinge")
                   Tippe(f.GruppeKuerzel, "ZWI")
                   f.GruppeTyp.SelectedItem = "verteilung"
                   Tippe(f.GruppeMax, "1")
                   f.GruppePrio.SelectedItem = "3"

                   Dim g = p.Klassenbildung.Gruppen.Single()
                   Assert.AreEqual("G_zwillinge", g.Id)
                   Assert.AreEqual("ZWI", g.Kuerzel)
                   Assert.AreEqual("verteilung", g.Typ)
                   Assert.AreEqual(1, g.MaxProKlasse)
                   Assert.AreEqual(3, g.Prio)

                   ' Die Ankreuzfelder entstehen zur Laufzeit, eines je Kind.
                   Dim haken = f.GruppeMitglieder.Children.OfType(Of CheckBox)().ToList()
                   Assert.AreEqual(6, haken.Count)
                   haken(0).IsChecked = True
                   haken(0).RaiseEvent(New RoutedEventArgs(ButtonBase.ClickEvent))
                   Assert.AreEqual(1, g.Mitglieder.Count)
               End Sub)
    End Sub

    ''' <summary>Der Fehler aus dem manuellen Test (06.09.2026): zweite
    ''' Gruppe angelegt, erstes Mitglied angehakt - und die Liste sprang
    ''' auf die erste Gruppe zurueck. Ursache: die Auswahl wurde vor dem
    ''' Neufuellen per TryCast auf den Listeneintrag gelesen, der aber ein
    ''' Zeilenpaar ist; der Cast ergab immer Nothing.</summary>
    <TestMethod>
    Public Sub EinHakenWechseltNichtDieGewaehlteGruppe()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   Dim f As New KlassenbildungFenster(p, New TestDialoge())

                   Klick(f.GruppeNeuKnopf)
                   Tippe(f.GruppeId, "G_erste")
                   Klick(f.GruppeNeuKnopf)
                   Tippe(f.GruppeId, "G_zweite")
                   Assert.AreEqual(2, f.GruppenListe.Items.Count)
                   Dim zweite = p.Klassenbildung.Gruppen.Single(Function(g) g.Id = "G_zweite")
                   Assert.AreSame(zweite, CType(f.GruppenListe.SelectedItem, KlassenbildungFenster.Zeilenpaar).Eintrag,
                                  "Testgrundlage: die neue Gruppe ist gewaehlt")

                   Dim haken = f.GruppeMitglieder.Children.OfType(Of CheckBox)().First()
                   haken.IsChecked = True
                   haken.RaiseEvent(New RoutedEventArgs(ButtonBase.ClickEvent))

                   Assert.AreEqual(1, zweite.Mitglieder.Count, "der Haken gehoert zur zweiten Gruppe")
                   Assert.AreSame(zweite, CType(f.GruppenListe.SelectedItem, KlassenbildungFenster.Zeilenpaar).Eintrag,
                                  "nach dem Haken muss die zweite Gruppe gewaehlt bleiben")
                   Assert.AreEqual("G_zweite", f.GruppeId.Text, "das Formular zeigt weiter die zweite Gruppe")
               End Sub)
    End Sub

    ''' <summary>Der zweite Fehler aus dem manuellen Test (06.09.2026):
    ''' eine Buendelung mit "Hoechstens je Klasse" - die Maske sagte
    ''' "ohne Beanstandung", das Rechnen lehnte ab. Jetzt prueft die Maske
    ''' mit dem Kern, und die Kappe ist bei einer Buendelung gar nicht
    ''' bedienbar.</summary>
    <TestMethod>
    Public Sub DieMaskePrueftWieDerKernUndKappeGiltNurFuerVerteilung()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   ' Der kaputte Zustand, wie er aus einer aelteren Eingabe kommen kann.
                   p.Klassenbildung.Gruppen.Add(New KlassenbildungGruppe With {
                       .Id = "G_kaputt", .Typ = "buendelung", .Modus = "soft", .Prio = 2, .MaxProKlasse = 3,
                       .Mitglieder = New List(Of String) From {"S001", "S002"}})
                   Dim d As New TestDialoge()
                   Dim f As New KlassenbildungFenster(p, d)

                   StringAssert.Contains(f.Statuszeile.Text, "Hinweis", "die Statuszeile muss den Kernbefund zeigen")
                   Assert.IsFalse(f.GruppeMax.IsEnabled, "bei einer Buendelung ist die Kappe nicht bedienbar")

                   ' Wechsel auf Verteilung: Kappe bedienbar, Befund bleibt bis sie stimmt.
                   f.GruppeTyp.SelectedItem = "verteilung"
                   Assert.IsTrue(f.GruppeMax.IsEnabled)
                   Assert.AreEqual(3, p.Klassenbildung.Gruppen(0).MaxProKlasse)
                   StringAssert.Contains(f.Statuszeile.Text, "ohne Beanstandung")

                   ' Zurueck auf Buendelung: die Kappe wird weggeraeumt, nicht versteckt.
                   f.GruppeTyp.SelectedItem = "buendelung"
                   Assert.IsFalse(p.Klassenbildung.Gruppen(0).MaxProKlasse.HasValue)
                   Assert.AreEqual("", f.GruppeMax.Text)
                   StringAssert.Contains(f.Statuszeile.Text, "ohne Beanstandung")
               End Sub)
    End Sub

    ''' <summary>Die Mindestzahl (min_pro_klasse) ist das Pendant der Kappe:
    ''' nur bei einer Buendelung bedienbar, ein Wechsel auf Verteilung
    ''' raeumt sie weg, und Duplizieren nimmt sie mit.</summary>
    <TestMethod>
    Public Sub MindestzahlGiltNurFuerBuendelungUndWirdKopiert()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   Dim f As New KlassenbildungFenster(p, New TestDialoge())
                   Klick(f.GruppeNeuKnopf)
                   Dim g = p.Klassenbildung.Gruppen.Single()
                   Assert.IsTrue(f.GruppeMin.IsEnabled, "eine neue Gruppe ist eine Buendelung")
                   Assert.IsFalse(f.GruppeMax.IsEnabled)

                   Tippe(f.GruppeMin, "2")
                   Assert.AreEqual(2, g.MinProKlasse.Value)
                   StringAssert.Contains(CType(f.GruppenListe.SelectedItem, KlassenbildungFenster.Zeilenpaar).Text, "min 2")

                   Klick(f.GruppeDuplKnopf)
                   Assert.AreEqual(2, p.Klassenbildung.Gruppen.Count)
                   Assert.AreEqual(2, p.Klassenbildung.Gruppen(1).MinProKlasse.Value, "Duplizieren muss die Mindestzahl mitnehmen")

                   f.GruppeTyp.SelectedItem = "verteilung"
                   Dim kopie = p.Klassenbildung.Gruppen(1)
                   Assert.IsFalse(kopie.MinProKlasse.HasValue, "eine Verteilung kennt keine Mindestzahl")
                   Assert.IsFalse(f.GruppeMin.IsEnabled)
                   Assert.AreEqual("", f.GruppeMin.Text)
               End Sub)
    End Sub

    ''' <summary>Ein leeres Feld ist keine Ausnahme: `max_pro_klasse`
    ''' heisst dann "ohne Grenze", nicht 0.</summary>
    <TestMethod>
    Public Sub LeeresHoechstensJeKlasseHeisstOhneGrenze()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   Dim f As New KlassenbildungFenster(p, New TestDialoge())
                   Klick(f.GruppeNeuKnopf)
                   Dim g = p.Klassenbildung.Gruppen.Single()
                   ' Die Kappe gibt es nur bei einer Verteilung.
                   f.GruppeTyp.SelectedItem = "verteilung"

                   Tippe(f.GruppeMax, "2")
                   Assert.AreEqual(2, g.MaxProKlasse)

                   Tippe(f.GruppeMax, "")
                   Assert.IsFalse(g.MaxProKlasse.HasValue)
               End Sub)
    End Sub

    ''' <summary>Der Wert haengt am Attribut. Nach einem Attributwechsel
    ''' muss die Werteliste neu gefuellt werden, sonst stuende dort ein
    ''' Wert aus einem anderen Vokabular.</summary>
    <TestMethod>
    Public Sub DerBalanceWertFolgtDemAttribut()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   p.Klassenbildung.Schueler(0).Attribute("FOERDER") = "ja"
                   Dim f As New KlassenbildungFenster(p, New TestDialoge())

                   Klick(f.BalanceNeuKnopf)
                   Assert.AreEqual(0, f.BalanceWert.Items.Count, "ohne Attribut gibt es nichts zu waehlen")

                   f.BalanceAttribut.SelectedItem = "GESCHLECHT"
                   Assert.AreEqual(2, f.BalanceWert.Items.Count)

                   f.BalanceAttribut.SelectedItem = "FOERDER"
                   Assert.AreEqual(1, f.BalanceWert.Items.Count)
                   Assert.IsNull(p.Klassenbildung.Balance.Single().Wert,
                                 "ein Wert aus dem alten Vokabular darf nicht stehenbleiben")

                   f.BalanceWert.SelectedItem = "ja"
                   Assert.AreEqual("ja", p.Klassenbildung.Balance.Single().Wert)
                   StringAssert.Contains(f.BalanceBetroffen.Text, "1 Kind")
               End Sub)
    End Sub

    ''' <summary>Kinder erscheinen als Klarname, gespeichert wird die Id -
    ''' auch im Paar-Picker der Wuensche.</summary>
    <TestMethod>
    Public Sub DerPaarPickerSpeichertIdsUndZeigtNamen()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   p.Mapping.Add(New MappingEintrag With {.Id = "S001", .Vorname = "Mia", .Nachname = "Meier"})
                   Dim f As New KlassenbildungFenster(p, New TestDialoge())

                   Klick(f.WunschNeuKnopf)
                   f.WunschKindA.SelectedIndex = 1
                   f.WunschKindB.SelectedIndex = 2

                   Dim w = p.Klassenbildung.Wuensche.Single()
                   Assert.AreEqual(2, w.Kinder.Count)
                   Assert.IsTrue(w.Kinder.All(Function(k) k.StartsWith("S")),
                                 "im Bestand stehen Ids, keine Namen")
                   StringAssert.Contains(CStr(CType(f.WunschKindA.SelectedItem, ComboBoxItem).Content), "Mia")
               End Sub)
    End Sub

    <TestMethod>
    Public Sub FixierungenLassenSichEinzelnLoesen()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   p.Klassenbildung.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S001", .Klasse = 1})
                   p.Klassenbildung.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S002", .Klasse = 2})
                   Dim d As New TestDialoge()
                   Dim f As New KlassenbildungFenster(p, d)

                   Assert.AreEqual(2, f.FixListe.Items.Count)
                   f.FixListe.SelectedIndex = 0
                   Klick(f.FixWegKnopf)

                   Assert.AreEqual(1, p.Klassenbildung.Fixierungen.Count)
                   Assert.AreEqual(1, f.FixListe.Items.Count)
               End Sub)
    End Sub


    ''' <summary>Der CSV-Weg braucht einen EIGENEN Knopf. Bis er den
    ''' bekam, sass er im Import-Dialog - und den oeffnete nur, wer schon
    ''' brauchbaren Text in der Zwischenablage hatte. Wer eine Datei
    ''' importieren wollte, musste also erst etwas ganz anderes kopieren
    ''' (im manuellen Test aufgefallen, 01.09.2026).</summary>
    <TestMethod>
    Public Sub DerCsvWegIstOhneZwischenablageErreichbar()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   Dim datei = IO.Path.Combine(TestsWurzel(), "bw-grundschule-beispiel",
                                               "import-beispiel", "einschulungsliste.csv")
                   Dim d As New TestDialoge With {.DateiOeffnenPfad = datei}
                   Dim f As New KlassenbildungFenster(p, d)

                   Assert.IsTrue(f.CsvKnopf.IsEnabled, "der Knopf muss ohne Zwischenablage bedienbar sein")
               End Sub)
    End Sub


    ''' <summary>Attribute stehen NAMENTLICH in der Rollenliste - zwei
    ''' Listen fuer eine Entscheidung waren eine zu viel (Nutzerbefund
    ''' 01.09.2026). Zur Spalte "Kann-Kind" waehlt man direkt
    ''' "Attribut: Kann-Kind".</summary>
    <TestMethod>
    Public Sub DieRollenlisteNenntDieAttributeBeimNamen()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   Dim m As New KlassenbildungEingabeViewModel(p, New TestDialoge())
                   Dim v = m.ImportPruefen("Nachname;Kann-Kind" & vbLf & "Meier;ja")
                   Dim d As New ImportDialog(v, New TestDialoge(), AddressOf m.ImportPruefen)

                   Dim texte = Rollentexte(d, spalte:=1)
                   CollectionAssert.Contains(texte, "Attribut: Kann-Kind")
                   Assert.IsFalse(texte.Contains("Attribut"), "der Sammelbegriff allein hilft niemandem")
               End Sub)
    End Sub

    ''' <summary>Fuehrt das Projekt schon ein Attribut, steht es in der
    ''' Liste - und der Spaltenname erscheint NICHT zusaetzlich, wenn er
    ''' sich nur in der Schreibweise unterscheidet. Genau daran waere
    ''' sonst ein zweites Attribut entstanden.</summary>
    <TestMethod>
    Public Sub VorhandeneAttributeStehenInDerListeUndVerdraengenDieDublette()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   Dim m As New KlassenbildungEingabeViewModel(p, New TestDialoge())
                   Dim v = m.ImportPruefen("Nachname;Geschlecht" & vbLf & "Meier;w")
                   ' Das Projekt fuehrt bereits "geschlecht" (klein).
                   Dim d As New ImportDialog(v, New TestDialoge(), AddressOf m.ImportPruefen,
                                             New List(Of String) From {"geschlecht", "kann_kind"})

                   Dim texte = Rollentexte(d, spalte:=1)
                   CollectionAssert.Contains(texte, "Attribut: geschlecht")
                   CollectionAssert.Contains(texte, "Attribut: kann_kind")
                   Assert.IsFalse(texte.Any(Function(t) t.Contains("Attribut: Geschlecht")),
                                  "die Beinahe-Dublette darf gar nicht erst waehlbar sein")
               End Sub)
    End Sub

    ''' <summary>Und die Auswahl kommt am Bestand an: der gewaehlte
    ''' Eintrag setzt Rolle UND Schluessel.</summary>
    <TestMethod>
    Public Sub DieAuswahlSetztRolleUndSchluessel()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   Dim m As New KlassenbildungEingabeViewModel(p, New TestDialoge())
                   Dim v = m.ImportPruefen("Nachname;Geschlecht" & vbLf & "Meier;w")
                   Dim d As New ImportDialog(v, New TestDialoge(), AddressOf m.ImportPruefen,
                                             New List(Of String) From {"geschlecht"})

                   Dim feld = Rollenfeld(d, spalte:=1)
                   feld.SelectedIndex = Rollentexte(d, 1).IndexOf("Attribut: geschlecht")

                   Dim wahl = d.Wahlen(1)
                   Assert.AreEqual(Spaltenrolle.Attribut, wahl.Rolle)
                   Assert.AreEqual("geschlecht", wahl.Schluessel)
               End Sub)
    End Sub

    ''' <summary>Das zweite Auswahlfeld ist weg - eine Zeile traegt jetzt
    ''' die Rollenliste und (nur bei Gruppen) den Typ.</summary>
    <TestMethod>
    Public Sub EineZeileHatHoechstensZweiAuswahlfelder()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Projekt()
                   Dim m As New KlassenbildungEingabeViewModel(p, New TestDialoge())
                   Dim v = m.ImportPruefen("Nachname;Geschlecht" & vbLf & "Meier;w")
                   Dim d As New ImportDialog(v, New TestDialoge(), AddressOf m.ImportPruefen)

                   Dim zeile = CType(d.Zuordnung.Children(1), StackPanel)
                   Assert.AreEqual(2, zeile.Children.OfType(Of ComboBox)().Count())
               End Sub)
    End Sub

    ' ---------------------------------------------------------------

    Private Shared Function Rollenfeld(d As ImportDialog, spalte As Integer) As ComboBox
        Dim zeile = CType(d.Zuordnung.Children(spalte), StackPanel)
        Return zeile.Children.OfType(Of ComboBox)().First()
    End Function

    Private Shared Function Rollentexte(d As ImportDialog, spalte As Integer) As List(Of String)
        Return Rollenfeld(d, spalte).Items.Cast(Of Object)().
            Select(Function(o) o.ToString()).ToList()
    End Function

End Class


