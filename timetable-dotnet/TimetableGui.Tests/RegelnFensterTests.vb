' Der Fensteraufbau selbst - die eine Fehlerklasse, die alle uebrigen
' Tests dieses Projekts NICHT sehen.
'
' Sie laufen ausdruecklich headless (siehe Kopf von DesignKanonTests.vb)
' und erzeugen keine WPF-Steuerelemente. Ein StaticResource-Schluessel,
' den es nicht gibt, ein FindResource im Code-Behind, eine Bindung ins
' Leere: nichts davon ist ein Compilerfehler, und nichts davon faellt
' ohne Aufbau auf. Live erlebt: `schrift-mono` stand in der Vorlage,
' aber nicht in Tokens.xaml.
'
' Deshalb EIN Test, der genau das tut - Fenster bauen, jede der acht
' Masken einmal aufziehen - und zwar auf einem eigenen STA-Thread. Das
' Testprojekt bleibt damit fuer alles andere headless; nur dieser eine
' Thread kennt WPF.
Imports System.Threading
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class RegelnFensterTests
    Private Shared Function Beispielprojekt(schule As String) As Projekt
        Return ProjektOrdner.Importieren(IO.Path.Combine(TestsWurzel(), schule),
                                         New DateTimeOffset(2026, 8, 23, 21, 0, 0, TimeSpan.Zero))
    End Function

    <TestMethod>
    Public Sub RegelnfensterBautJedeDerAchtMaskenAuf()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Beispielprojekt("bw-gms-beispiel")
                   Dim f As New RegelnFenster(p, New StummeDialoge())

                   Assert.IsTrue(f.Liste.Items.Count > 0, "Keine Handregel in der Liste - die GMS hat welche.")
                   Assert.IsTrue(f.YamlEditor.Text.Length > 20, "Der YAML-Editor ist leer.")

                   For i = 0 To Regeltypen.Alle.Count - 1
                       Dim typ = Regeltypen.Alle(i)
                       f.NeuTyp.SelectedIndex = i
                       f.NeuKnopf.RaiseEvent(New RoutedEventArgs(Primitives.ButtonBase.ClickEvent))
                       ' Je Feld ein Beschriftungsblock UND ein
                       ' Steuerelement - eine still ausgelassene Feldart
                       ' faellt damit auf.
                       Assert.AreEqual(2 * typ.Felder.Count, f.Maske.Children.Count,
                                       $"Maske '{typ.Typ}' hat nicht fuer jedes Feld ein Steuerelement.")
                   Next
               End Sub)
    End Sub

    ''' <summary>Das Fenster fragt nie von selbst - der Test soll bei
    ''' einem unerwarteten Dialog scheitern, nicht haengen.</summary>
    Private NotInheritable Class StummeDialoge
        Implements IDialoge

        Public Function ProjektdateiOeffnen() As String Implements IDialoge.ProjektdateiOeffnen
            Throw New InvalidOperationException("unerwartet")
        End Function

        Public Function ProjektdateiSpeichernUnter(vorschlag As String) As String _
            Implements IDialoge.ProjektdateiSpeichernUnter
            Throw New InvalidOperationException("unerwartet")
        End Function

        Public Function SchulordnerWaehlen() As String Implements IDialoge.SchulordnerWaehlen
            Throw New InvalidOperationException("unerwartet")
        End Function

        Public Function PasswortAbfragen(titel As String, bestaetigen As Boolean) As String _
            Implements IDialoge.PasswortAbfragen
            Throw New InvalidOperationException("unerwartet")
        End Function

        Public Function ProjektAssistent() As ProjektEntwurf Implements IDialoge.ProjektAssistent
            Throw New InvalidOperationException("unerwartet")
        End Function

        Public Function FreigabeBestaetigen(vorlage As Freigabevorlage) As Freigabebestaetigung _
            Implements IDialoge.FreigabeBestaetigen
            Throw New InvalidOperationException("unerwartet")
        End Function

        Public Sub Hinweis(titel As String, text As String) Implements IDialoge.Hinweis
            Throw New InvalidOperationException($"Unerwarteter Hinweis: {titel} - {text}")
        End Sub

        Public Function Frage(titel As String, text As String) As Boolean Implements IDialoge.Frage
            Throw New InvalidOperationException($"Unerwartete Frage: {titel}")
        End Function
    End Class

    ''' <summary>Eine Regel ueber die MASKE anlegen, nicht ueber das
    ''' ViewModel. Dazwischen liegt die Verdrahtung Steuerelement -> Wert,
    ''' und die ist eine eigene Fehlerquelle: ein Feld, dessen Ereignis
    ''' nicht haengt, bleibt beim Anlegen einfach leer - ohne Meldung.</summary>
    <TestMethod>
    Public Sub MaskeSchreibtIhreEingabenInDieNeueRegel()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p = Beispielprojekt("bw-gms-beispiel")
                   Dim vorher = p.Constraints.Count
                   Dim f As New RegelnFenster(p, New StummeDialoge())

                   Dim i = Regeltypen.Alle.FindIndex(Function(t) t.Typ = "room_requirement")
                   f.NeuTyp.SelectedIndex = i
                   f.NeuKnopf.RaiseEvent(New RoutedEventArgs(Primitives.ButtonBase.ClickEvent))

                   ' Fach ueber die Auswahlliste, Raeume ueber die
                   ' Ankreuzfelder - genau die Wege, die ein Mensch nimmt.
                   Dim fach = p.Bestand.Faecher.First().Name
                   Kombi(f, 0).SelectedItem = fach
                   Dim raeume = Ankreuzfelder(f).Take(2).ToList()
                   Assert.AreEqual(2, raeume.Count, "Zu wenige Raeume in der Maske.")
                   For Each cb In raeume
                       cb.IsChecked = True
                       cb.RaiseEvent(New RoutedEventArgs(Primitives.ButtonBase.ClickEvent))
                   Next

                   f.AnlegenKnopf.RaiseEvent(New RoutedEventArgs(Primitives.ButtonBase.ClickEvent))

                   Assert.AreEqual(vorher + 1, p.Constraints.Count, "Es wurde keine Regel angelegt.")
                   Dim neu = p.Constraints.Last()
                   Assert.AreEqual("room_requirement", JsonHelpers.GetString(neu, "type"))
                   Assert.AreEqual(fach, JsonHelpers.GetString(neu, "subject"))
                   CollectionAssert.AreEqual(raeume.Select(Function(c) CStr(c.Content)).ToList(),
                                             JsonHelpers.AsStringList(neu("allowed_rooms")),
                                             "Die angekreuzten Raeume stehen nicht in der Regel.")
               End Sub)
    End Sub

    ''' <summary>Die n-te Auswahlliste der Maske. Die Maske ist gebaut,
    ''' nicht ausgeschrieben - es gibt keinen Namen, ueber den man greifen
    ''' koennte.</summary>
    Private Shared Function Kombi(f As RegelnFenster, n As Integer) As ComboBox
        Return f.Maske.Children.OfType(Of ComboBox)().ElementAt(n)
    End Function

    Private Shared Function Ankreuzfelder(f As RegelnFenster) As List(Of CheckBox)
        Return f.Maske.Children.OfType(Of WrapPanel)().
            SelectMany(Function(w) w.Children.OfType(Of CheckBox)()).ToList()
    End Function

End Class
