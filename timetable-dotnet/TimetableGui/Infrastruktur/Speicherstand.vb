' Der Speicherzustand, sichtbar auch in den Masken.
'
' Hintergrund (Nutzerhinweis 26.08.2026): "im UI ist unklar, wann die
' Eingabe jeweils gespeichert wird". Dahinter stecken ZWEI Fragen, die
' die Oberflaeche bisher vermischt hat:
'
'   1. Wann wandert das Getippte in den Bestand?
'      Bei Textfeldern, sobald man das Feld verlaesst; bei Auswahllisten
'      und Ankreuzfeldern sofort. Das ist richtig so - ein Feld, das bei
'      jedem Tastendruck uebernimmt, sortiert die Liste nach jedem
'      Buchstaben neu -, war aber nirgends sichtbar.
'
'   2. Wann steht es in der Projektdatei?
'      Erst bei "Speichern". Und genau das war unmoeglich: die Masken
'      sind MODALE Fenster. Solange eine offen ist, verdeckt sie den
'      Ungespeichert-Indikator im Titel des Hauptfensters, und Strg+S
'      hing dort ebenfalls. Man musste die Maske schliessen, um
'      speichern zu koennen - ohne dass irgendetwas das gesagt haette.
'
' Diese Schnittstelle reicht beides in die Masken hinein: den Zustand
' zum Anzeigen und die Aktion zum Ausloesen.

''' <summary>Was eine Maske ueber das Speichern wissen und tun muss.
''' Bewusst schmal - die Maske soll den Zustand ZEIGEN und ihn ausloesen
''' koennen, nicht ihn verwalten.</summary>
Public Interface ISpeicherung
    ReadOnly Property Ungespeichert As Boolean
    ''' <summary>True, wenn ueberhaupt gespeichert werden kann - ohne
    ''' offenes Projekt gibt es nichts zu sichern.</summary>
    ReadOnly Property Moeglich As Boolean
    Sub Speichern()
    ''' <summary>Faellt, wenn sich `Ungespeichert` geaendert hat.</summary>
    Event ZustandGeaendert As EventHandler
End Interface

''' <summary>Die Umsetzung ueber das HauptViewModel. Sie liegt hier und
''' nicht im ViewModel selbst, damit dessen Schnittstelle nicht um eine
''' zweite Sicht auf dieselbe Eigenschaft waechst.</summary>
Public NotInheritable Class Speicherstand
    Implements ISpeicherung

    Private ReadOnly _modell As HauptViewModel

    Public Sub New(modell As HauptViewModel)
        _modell = modell
        AddHandler _modell.PropertyChanged,
            Sub(s, e)
                If e.PropertyName = NameOf(HauptViewModel.Geaendert) OrElse
                   e.PropertyName = NameOf(HauptViewModel.Projekt) Then
                    RaiseEvent ZustandGeaendert(Me, EventArgs.Empty)
                End If
            End Sub
    End Sub

    Public ReadOnly Property Ungespeichert As Boolean Implements ISpeicherung.Ungespeichert
        Get
            Return _modell.Geaendert
        End Get
    End Property

    Public ReadOnly Property Moeglich As Boolean Implements ISpeicherung.Moeglich
        Get
            Return _modell.ProjektOffen AndAlso Not _modell.Monitor.Laeuft
        End Get
    End Property

    Public Sub Speichern() Implements ISpeicherung.Speichern
        _modell.Speichern()
    End Sub

    Public Event ZustandGeaendert As EventHandler Implements ISpeicherung.ZustandGeaendert
End Class

''' <summary>Die Fusszeile einer Maske: Zustandstext, Knopf und
''' Strg+S - an einer Stelle, damit die drei Masken nicht auseinander
''' laufen.</summary>
Public Module Speicheranzeige

    ''' <summary>Der Hinweis, wann eine Eingabe in den Bestand wandert.
    ''' Er steht hier und nicht dreimal im XAML, damit er ueberall
    ''' gleich lautet.</summary>
    Public Const Uebernahmehinweis As String =
        "Eingaben werden übernommen, sobald Sie das Feld verlassen; " &
        "Auswahllisten und Häkchen wirken sofort. Erst mit Speichern stehen sie in der Projektdatei."

    Public Function Zustandstext(speicherung As ISpeicherung) As String
        If speicherung Is Nothing Then Return ""
        If Not speicherung.Moeglich Then Return "Kein Projekt geöffnet."
        Return If(speicherung.Ungespeichert,
                  "● Nicht gespeicherte Änderungen",
                  "Alle Änderungen gespeichert")
    End Function

    ''' <summary>Farbrolle des Zustandstextes. Der ungespeicherte Zustand
    ''' ist KEINE Warnung - er ist der Normalfall beim Arbeiten. Deshalb
    ''' die gedeckte Textfarbe und nicht Gelb; Gelb waere ein Alarm, den
    ''' niemand ernst naehme, weil er dauernd anstuende.</summary>
    Public Function Zustandsfarbe(speicherung As ISpeicherung) As String
        If speicherung IsNot Nothing AndAlso speicherung.Ungespeichert Then Return "farbe-text"
        Return "farbe-text-3"
    End Function

End Module
