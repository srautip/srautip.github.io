' Der Freigabe-Dialog als FENSTER (Stufe G3).
'
' Diese Probe hat sich beim Bauen sofort bezahlt gemacht: der Dialog
' griff auf `farbe-krit-rand` zu, das es in Tokens.xaml nicht gab. Der
' Compiler sieht das nicht - FindResource ist ein Laufzeitaufruf, und
' ohne Fensteraufbau faellt es erst dem Nutzer auf.
'
' Inhaltlich geprueft wird die eine Eigenschaft, an der die Rechtsform
' der Freigabe haengt: der Knopf bleibt aus, solange nicht bestaetigt
' UND benannt ist (klassenbildung-konzept 10.1).
Imports System.Text.Json.Nodes
Imports System.Windows
Imports System.Windows.Controls.Primitives
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class FreigabeFensterTests

    Private Shared Function Vorlage(muss As Integer, kann As Integer) As Freigabevorlage
        Dim stand As New ProjektStand With {
            .Id = "a", .Label = "Stundenplan a", .Erstellt = DateTimeOffset.UnixEpoch,
            .Stundenplan = JsonNode.Parse($"
                {{""solutions"": [
                    {{""muss_violation_count"": {muss}, ""kann_violation_count"": {kann},
                      ""quality_total"": 193}}]}}").AsObject()}
        Return Freigabe.Vorlage(stand)
    End Function

    <TestMethod>
    Public Sub DerKnopfBleibtAusOhneHakenUndOhneNamen()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   ' Ohne Abweichungen, damit dieser Test WIRKLICH nur
                   ' Haken und Namen prueft - die Begruendungspflicht hat
                   ' ihren eigenen Test.
                   Dim f As New FreigabeFenster(Vorlage(muss:=0, kann:=0))

                   ' Der Name ist vorbelegt, der Haken nicht - also aus.
                   Assert.IsFalse(f.FreigebenKnopf.IsEnabled)

                   f.Haken.IsChecked = True
                   f.Haken.RaiseEvent(New RoutedEventArgs(ButtonBase.ClickEvent))
                   Assert.IsTrue(f.FreigebenKnopf.IsEnabled)

                   f.PersonFeld.Text = "   "
                   Assert.IsFalse(f.FreigebenKnopf.IsEnabled, "ein Haken ohne Namen ist kein Nachweis")
               End Sub)
    End Sub

    ''' <summary>Die Abweichungen stehen im Fenster - nicht als Zahl,
    ''' sondern im Wortlaut. Genau darauf beruht, dass die Bestaetigung
    ''' kein Durchwinken ist.</summary>
    <TestMethod>
    Public Sub DasFensterZeigtDieAbweichungenUndDenPassendenSatz()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim f As New FreigabeFenster(Vorlage(muss:=1, kann:=4))

                   Dim gezeigt = CType(f.Abweichungen.ItemsSource, IEnumerable(Of String)).ToList()
                   Assert.AreEqual(2, gezeigt.Count)
                   Assert.IsTrue(gezeigt.Any(Function(z) z.Contains("Muss-Regel")))
                   StringAssert.Contains(f.Satz.Text, "2 Regelabweichungen")
                   StringAssert.Contains(f.Abweichungskopf.Text, "2 verbleibende")
               End Sub)
    End Sub

    <TestMethod>
    Public Sub OhneAbweichungenSagtDasFensterDasAuch()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim f As New FreigabeFenster(Vorlage(muss:=0, kann:=0))

                   StringAssert.Contains(f.Abweichungskopf.Text, "Keine verbleibenden")
                   StringAssert.Contains(f.Satz.Text, "keine Regelabweichungen")
               End Sub)
    End Sub

    ''' <summary>Der Knopf bleibt aus, solange die Begruendung fehlt -
    ''' und zwar NUR, wenn es Abweichungen gibt. Ohne sie ist das
    ''' Notizfeld freiwillig.</summary>
    <TestMethod>
    Public Sub OhneBegruendungBleibtDerKnopfAusAberNurBeiAbweichungen()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim f As New FreigabeFenster(Vorlage(muss:=0, kann:=4))
                   f.Haken.IsChecked = True
                   f.Haken.RaiseEvent(New RoutedEventArgs(ButtonBase.ClickEvent))

                   Assert.IsFalse(f.FreigebenKnopf.IsEnabled,
                                  "ein Häkchen ohne eigene Worte belegt keine Befassung")
                   StringAssert.Contains(f.Notizfrage.Text, "vertretbar")

                   f.NotizFeld.Text = "Nur Randstunden betroffen, Fachräume wiegen schwerer."
                   Assert.IsTrue(f.FreigebenKnopf.IsEnabled)
               End Sub)
    End Sub

    <TestMethod>
    Public Sub OhneAbweichungenIstDasNotizfeldFreiwillig()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim f As New FreigabeFenster(Vorlage(muss:=0, kann:=0))
                   f.Haken.IsChecked = True
                   f.Haken.RaiseEvent(New RoutedEventArgs(ButtonBase.ClickEvent))

                   Assert.IsTrue(f.FreigebenKnopf.IsEnabled)
                   StringAssert.Contains(f.Notizfrage.Text, "freiwillig")
               End Sub)
    End Sub

End Class
