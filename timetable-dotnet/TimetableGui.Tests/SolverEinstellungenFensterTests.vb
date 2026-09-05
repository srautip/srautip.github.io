' Die Solver-Einstellungen als FENSTER (Stufe H4). Die Expertenliste
' entsteht zur Laufzeit, ein FindResource sitzt im Code-Behind - genau
' die Fehlerklasse, die headless unsichtbar bleibt (Fensterprobe.vb).
Imports System.Windows
Imports System.Windows.Controls
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt
Imports TimetableYaml

<TestClass>
Public Class SolverEinstellungenFensterTests

    Private Shared Sub Tippe(feld As TextBox, text As String)
        feld.Text = text
        feld.RaiseEvent(New RoutedEventArgs(UIElement.LostFocusEvent))
    End Sub

    <TestMethod>
    Public Sub DasFensterBautSichAufUndSchreibtInDieConfig()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim p As New Projekt()
                   p.Config = New RunConfig With {.SolveTimeLimitS = 60, .MaxSolutions = 3, .NumWorkers = 1, .Seed = 7}
                   Dim geaendert = 0
                   Dim f As New SolverEinstellungenFenster(p, New TestDialoge())
                   AddHandler f.Geaendert, Sub() geaendert += 1

                   Assert.AreEqual("60", f.Zeitbudget.Text)
                   Assert.AreEqual("3", f.Loesungen.Text)
                   Assert.IsTrue(f.Experten.Children.Count > 0, "die Expertenliste ist gebaut")
                   ' Ohne Speicherhuelle (Tests) gibt es nichts zu speichern -
                   ' dann bleibt der Knopf weg statt tot.
                   Assert.AreEqual(Visibility.Collapsed, f.SpeichernKnopf.Visibility)

                   Tippe(f.Zeitbudget, "120")
                   Tippe(f.Loesungen, "5")
                   Assert.AreEqual(120.0, p.Config.SolveTimeLimitS)
                   Assert.AreEqual(5, p.Config.MaxSolutions)
                   Assert.IsTrue(geaendert >= 2, "jede Aenderung meldet sich")
               End Sub)
    End Sub

End Class
