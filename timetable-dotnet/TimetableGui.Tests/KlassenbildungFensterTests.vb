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

End Class
