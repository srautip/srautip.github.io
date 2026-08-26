' Der Speicherzustand in den Masken (Nutzerhinweis 26.08.2026:
' "im UI ist unklar, wann die Eingabe jeweils gespeichert wird").
'
' Der Befund dahinter war handfest: die Masken sind MODALE Fenster.
' Solange eine offen war, verdeckte sie den Ungespeichert-Indikator im
' Titel des Hauptfensters, und Strg+S hing ausschliesslich dort - man
' konnte aus einer Maske heraus gar nicht speichern.
Imports System.Windows
Imports System.Linq
Imports System.Windows.Input
Imports System.Windows.Controls.Primitives
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableGui
Imports TimetableProjekt

''' <summary>Attrappe der Huelle: zaehlt, wie oft gespeichert wurde.</summary>
Friend NotInheritable Class TestSpeicherung
    Implements ISpeicherung

    Public Property Offen As Boolean = True
    Private _ungespeichert As Boolean

    Public Property Ungespeichert As Boolean Implements ISpeicherung.Ungespeichert
        Get
            Return _ungespeichert
        End Get
        Set
            _ungespeichert = Value
            RaiseEvent ZustandGeaendert(Me, EventArgs.Empty)
        End Set
    End Property

    Public ReadOnly Property Moeglich As Boolean Implements ISpeicherung.Moeglich
        Get
            Return Offen
        End Get
    End Property

    Public ReadOnly Property Speicherungen As Integer

    Public Sub Speichern() Implements ISpeicherung.Speichern
        _Speicherungen += 1
        Ungespeichert = False
    End Sub

    Public Event ZustandGeaendert As EventHandler Implements ISpeicherung.ZustandGeaendert
End Class

<TestClass>
Public Class SpeicherzustandTests

    ' ===============================================================
    ' Der Text
    ' ===============================================================

    <TestMethod>
    Public Sub DerZustandstextUnterscheidetDreiLagen()
        Dim s As New TestSpeicherung()
        Assert.AreEqual("Alle Änderungen gespeichert", Speicheranzeige.Zustandstext(s))

        s.Ungespeichert = True
        StringAssert.Contains(Speicheranzeige.Zustandstext(s), "Nicht gespeicherte")

        s.Offen = False
        StringAssert.Contains(Speicheranzeige.Zustandstext(s), "Kein Projekt")
    End Sub

    ''' <summary>Ungespeichert ist KEINE Warnung, sondern der Normalfall
    ''' beim Arbeiten. Gelb waere ein Alarm, den niemand ernst naehme,
    ''' weil er dauernd anstuende.</summary>
    <TestMethod>
    Public Sub UngespeichertIstKeineWarnfarbe()
        Dim s As New TestSpeicherung()
        Assert.AreEqual("farbe-text-3", Speicheranzeige.Zustandsfarbe(s))
        s.Ungespeichert = True
        Assert.AreEqual("farbe-text", Speicheranzeige.Zustandsfarbe(s))
    End Sub

    ''' <summary>Die andere Haelfte der Frage: wann wandert das Getippte
    ''' ueberhaupt in den Bestand? Der Hinweis sagt beides - und steht an
    ''' EINER Stelle, damit er in allen drei Masken gleich lautet.</summary>
    <TestMethod>
    Public Sub DerHinweisErklaertBeideZeitpunkte()
        StringAssert.Contains(Speicheranzeige.Uebernahmehinweis, "Feld verlassen")
        StringAssert.Contains(Speicheranzeige.Uebernahmehinweis, "Projektdatei")
    End Sub

    ' ===============================================================
    ' In den Masken
    ' ===============================================================

    Private Shared Function Projekt() As Projekt
        Dim p As New Projekt()
        p.Bestand.Klassenstufen.Add(New TimetableCore.Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})
        Return p
    End Function

    ''' <summary>Der Kern der Sache: aus der Maske heraus speichern.
    ''' Vorher ging das gar nicht.</summary>
    <TestMethod>
    Public Sub AusDerMaskeHerausLaesstSichSpeichern()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim s As New TestSpeicherung With {.Ungespeichert = True}
                   Dim f As New StammdatenFenster(Projekt(), New TestDialoge(), s)

                   StringAssert.Contains(f.Speicherzustand.Text, "Nicht gespeicherte")
                   Assert.IsTrue(f.SpeichernKnopf.IsEnabled)

                   f.SpeichernKnopf.RaiseEvent(New RoutedEventArgs(ButtonBase.ClickEvent))

                   Assert.AreEqual(1, s.Speicherungen)
                   Assert.AreEqual("Alle Änderungen gespeichert", f.Speicherzustand.Text)
                   Assert.IsFalse(f.SpeichernKnopf.IsEnabled, "ohne Änderung gibt es nichts zu speichern")
               End Sub)
    End Sub

    ''' <summary>Und der Zustand folgt der Huelle: sobald eine Eingabe
    ''' uebernommen wurde, sagt die Maske es - genau das ist die
    ''' Rueckmeldung, die gefehlt hat.</summary>
    <TestMethod>
    Public Sub DerZustandInDerMaskeFolgtDerHuelle()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim s As New TestSpeicherung()
                   Dim f As New KlassenbildungFenster(Projekt(), New TestDialoge(), s)

                   Assert.AreEqual("Alle Änderungen gespeichert", f.Speicherzustand.Text)
                   Assert.IsFalse(f.SpeichernKnopf.IsEnabled)

                   s.Ungespeichert = True

                   StringAssert.Contains(f.Speicherzustand.Text, "Nicht gespeicherte")
                   Assert.IsTrue(f.SpeichernKnopf.IsEnabled)
               End Sub)
    End Sub

    ''' <summary>Strg+S wirkt jetzt AUCH in der Maske. Vorher hing es
    ''' allein am Hauptfenster, das die modale Maske verdeckt.</summary>
    <TestMethod>
    Public Sub StrgSWirktInDerMaske()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim s As New TestSpeicherung With {.Ungespeichert = True}
                   Dim f As New RegelnFenster(Projekt(), New TestDialoge(), s)

                   Dim treffer = f.InputBindings.OfType(Of KeyBinding)().
                       Where(Function(b) b.Key = Key.S AndAlso b.Modifiers = ModifierKeys.Control).ToList()
                   Assert.AreEqual(1, treffer.Count, "in der Maske fehlt Strg+S")

                   treffer(0).Command.Execute(Nothing)
                   Assert.AreEqual(1, s.Speicherungen)
               End Sub)
    End Sub

    ''' <summary>Ohne Huelle - im Test und in jeder Verwendung ohne
    ''' Projektdatei - wird kein toter Knopf angeboten.</summary>
    <TestMethod>
    Public Sub OhneHuelleGibtEsKeinenToterKnopf()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim f As New StammdatenFenster(Projekt(), New TestDialoge())

                   Assert.AreEqual(Visibility.Collapsed, f.SpeichernKnopf.Visibility)
                   Assert.AreEqual(Visibility.Collapsed, f.Speicherzustand.Visibility)
               End Sub)
    End Sub

    ''' <summary>Der Hinweis haengt an der Anzeige, damit die Erklaerung
    ''' dort steht, wo die Frage entsteht.</summary>
    <TestMethod>
    Public Sub DerUebernahmehinweisHaengtAmZustandstext()
        AufSta(Sub()
                   RessourcenSicherstellen()
                   Dim s As New TestSpeicherung()
                   Dim f As New StammdatenFenster(Projekt(), New TestDialoge(), s)

                   StringAssert.Contains(CStr(f.Speicherzustand.ToolTip), "Feld verlassen")
               End Sub)
    End Sub

End Class
