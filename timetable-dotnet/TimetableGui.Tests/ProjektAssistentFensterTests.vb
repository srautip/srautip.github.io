' Der Projekt-Assistent als FENSTER (F5). Was hier geprueft wird, sieht
' ProjektAssistentTests nicht: die Schrittleiste, die zur Laufzeit
' gebauten Schuelerfelder und die Frage, ob "Weiter" bei unvollstaendiger
' Eingabe wirklich stehenbleibt.
'
' Vgl. Fensterprobe.vb fuer die Begruendung, warum das ueberhaupt ein
' eigener Testtyp ist.
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableGui

<TestClass>
Public Class ProjektAssistentFensterTests

    Private Shared Sub Klick(k As Button)
        k.RaiseEvent(New RoutedEventArgs(ButtonBase.ClickEvent))
    End Sub

    <TestMethod>
    Public Sub AssistentBautAlleFuenfSchritteAuf()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim d As New TestDialoge With {.SpeichernPfad = "C:\tmp\neu.splanx"}
                   Dim f As New ProjektAssistent(d)

                   Assert.IsFalse(f.ZurueckKnopf.IsEnabled, "im ersten Schritt gibt es kein Zurueck")
                   StringAssert.Contains(f.Kopf.Text, "Schritt 1 von 5")

                   ' Schritt 1 unvollstaendig: der Assistent bleibt stehen
                   ' und sagt, was fehlt - ein toter Knopf taete das nicht.
                   Klick(f.WeiterKnopf)
                   StringAssert.Contains(f.Kopf.Text, "Schritt 1 von 5")
                   StringAssert.Contains(f.Befund.Text, "Schulname")

                   f.NameFeld.Text = "Testschule"
                   Klick(f.WeiterKnopf)
                   StringAssert.Contains(f.Kopf.Text, "Schritt 2 von 5")
                   StringAssert.Contains(f.StufenLabel.Text, "1 bis 4")

                   Klick(f.WeiterKnopf)
                   StringAssert.Contains(f.Kopf.Text, "Schritt 3 von 5")
                   ' Ein Feld je Klassenstufe, zur Laufzeit gebaut.
                   Assert.AreEqual(4, f.SchuelerFelder.Children.Count)
                   Assert.AreEqual(GruppenVorlagen.Alle.Count,
                                   f.VorlagenFelder.Children.OfType(Of CheckBox)().Count())

                   Klick(f.WeiterKnopf)
                   StringAssert.Contains(f.Kopf.Text, "Schritt 4 von 5")
                   Klick(f.WeiterKnopf)
                   StringAssert.Contains(f.Kopf.Text, "Schritt 4 von 5")
                   StringAssert.Contains(f.Befund.Text, "Passwort")

                   f.PasswortFeld.Password = "geheim12"
                   f.PasswortFeld2.Password = "geheim12"
                   Klick(f.PfadKnopf)
                   Klick(f.WeiterKnopf)
                   StringAssert.Contains(f.Kopf.Text, "Schritt 5 von 5")
                   Assert.AreEqual("Projekt anlegen", CStr(f.WeiterKnopf.Content))

                   Dim zeilen = CType(f.Bilanz.ItemsSource, IEnumerable(Of String)).ToList()
                   Assert.IsTrue(zeilen.Any(Function(z) z.Contains("8 Klassen")), String.Join(vbLf, zeilen))
               End Sub)
    End Sub

    ''' <summary>Die Schulart aendert die Vorgaben - die FELDER muessen
    ''' folgen, sonst zeigt die Maske "4", waehrend das Modell mit 6
    ''' rechnet.</summary>
    <TestMethod>
    Public Sub SchulartWechselAktualisiertDieFelder()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim f As New ProjektAssistent(New TestDialoge())

                   Assert.AreEqual("4", f.StufenFeld.Text)
                   f.SchulartFeld.SelectedItem = "Gemeinschaftsschule"
                   Assert.AreEqual("6", f.StufenFeld.Text)
               End Sub)
    End Sub

    ''' <summary>Die Schuelerfelder werden bei jedem Betreten von Schritt 3
    ''' neu gebaut. Wer in Schritt 2 zurueckgeht und die Stufenzahl
    ''' verkleinert, darf kein Feld fuer eine Stufe behalten, die es nicht
    ''' mehr gibt - die Eingabe waere ohne Wirkung.</summary>
    <TestMethod>
    Public Sub WenigerKlassenstufenLassenKeinVerwaistesFeldZurueck()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim f As New ProjektAssistent(New TestDialoge())
                   f.NameFeld.Text = "Testschule"
                   Klick(f.WeiterKnopf)
                   Klick(f.WeiterKnopf)
                   Assert.AreEqual(4, f.SchuelerFelder.Children.Count)

                   Klick(f.ZurueckKnopf)
                   f.StufenFeld.Text = "2"
                   Klick(f.WeiterKnopf)
                   Assert.AreEqual(2, f.SchuelerFelder.Children.Count)
               End Sub)
    End Sub

End Class
