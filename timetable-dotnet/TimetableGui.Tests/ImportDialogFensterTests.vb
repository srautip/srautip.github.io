' Der Import-Dialog als FENSTER. Die Zuordnungszeilen entstehen zur
' Laufzeit, und seit der Regelableitung haengt an jeder Attributspalte
' ein Ankreuzfeld, dessen Text aus den Werten der Spalte kommt - genau
' die Fehlerklasse, die headless unsichtbar bleibt (Fensterprobe.vb).
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class ImportDialogFensterTests

    Private Shared Function Zeile(d As ImportDialog, spalte As Integer) As StackPanel
        Return CType(d.Zuordnung.Children(spalte), StackPanel)
    End Function

    Private Shared Function Rollenwahl(d As ImportDialog, spalte As Integer) As ComboBox
        Return CType(Zeile(d, spalte).Children(1), ComboBox)
    End Function

    Private Shared Function Ableiten(d As ImportDialog, spalte As Integer) As CheckBox
        Return CType(Zeile(d, spalte).Children(3), CheckBox)
    End Function

    ''' <summary>Waehlt in der Rollenliste den Eintrag, dessen Text so
    ''' beginnt - die Eintraege selbst sind ein privater Typ des Dialogs.</summary>
    Private Shared Sub Waehle(liste As ComboBox, textanfang As String)
        For i = 0 To liste.Items.Count - 1
            If liste.Items(i).ToString().StartsWith(textanfang, StringComparison.Ordinal) Then
                liste.SelectedIndex = i
                Return
            End If
        Next
        Assert.Fail($"Kein Eintrag '{textanfang}…' in der Rollenliste")
    End Sub

    <TestMethod>
    Public Sub DasAnkreuzfeldZeigtDieAbleitungUndSchreibtInDieWahl()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim m As New KlassenbildungEingabeViewModel(New Projekt(), New TestDialoge())
                   Dim v = m.ImportPruefen(
                       "Nachname;Betreuung;Kita" & vbLf &
                       "Meier;ja;Sonne" & vbLf & "Schulz;nein;Mond" & vbLf & "Braun;ja;Stern")
                   Dim d As New ImportDialog(v)
                   Assert.AreEqual(3, d.Zuordnung.Children.Count, "eine Zeile je Spalte")

                   ' Vorgabe verwerfen: kein Ankreuzfeld zu sehen.
                   Assert.AreEqual(Visibility.Hidden, Ableiten(d, 1).Visibility)
                   Assert.IsFalse(d.Wahlen(1).RegelAbleiten)

                   ' Attribut gewaehlt: Haken an, Text sagt, was entstuende.
                   Waehle(Rollenwahl(d, 1), "Attribut:")
                   Assert.AreEqual(Visibility.Visible, Ableiten(d, 1).Visibility)
                   Assert.IsTrue(Ableiten(d, 1).IsChecked)
                   Assert.IsTrue(d.Wahlen(1).RegelAbleiten, "die Vorgabe bei einem Attribut ist ableiten")
                   StringAssert.Contains(CStr(Ableiten(d, 1).Content), "Balance auf „ja""")

                   Waehle(Rollenwahl(d, 2), "Attribut:")
                   StringAssert.Contains(CStr(Ableiten(d, 2).Content), "3 Bündelungen")

                   ' Haken weg: die Wahl folgt.
                   Ableiten(d, 1).IsChecked = False
                   Ableiten(d, 1).RaiseEvent(New RoutedEventArgs(ButtonBase.ClickEvent))
                   Assert.IsFalse(d.Wahlen(1).RegelAbleiten)

                   ' Zurueck auf verwerfen: Feld verschwindet, Wahl ist aus.
                   Waehle(Rollenwahl(d, 2), "verwerfen")
                   Assert.AreEqual(Visibility.Hidden, Ableiten(d, 2).Visibility)
                   Assert.IsFalse(d.Wahlen(2).RegelAbleiten)
               End Sub)
    End Sub

    ''' <summary>Eine Spalte, aus der nichts folgt, bekommt ein
    ''' abgeschaltetes Feld statt eines Hakens, der nichts bewirkt.</summary>
    <TestMethod>
    Public Sub OhneAbleitbareRegelIstDasFeldAbgeschaltet()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim m As New KlassenbildungEingabeViewModel(New Projekt(), New TestDialoge())
                   Dim v = m.ImportPruefen("Nachname;Stufe" & vbLf & "Meier;1" & vbLf & "Schulz;1")
                   Dim d As New ImportDialog(v)

                   Waehle(Rollenwahl(d, 1), "Attribut:")
                   Assert.IsFalse(Ableiten(d, 1).IsEnabled)
                   Assert.IsFalse(Ableiten(d, 1).IsChecked)
                   Assert.IsFalse(d.Wahlen(1).RegelAbleiten)
                   StringAssert.Contains(CStr(Ableiten(d, 1).Content), "keine Regel")
               End Sub)
    End Sub

End Class
