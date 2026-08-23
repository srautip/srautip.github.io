' Schuldaten (gui-ui-konzept.md 6.2). Keine Liste, sondern EIN Formular -
' deshalb ohne ListenViewModel-Basis.
'
' Die eine Stelle mit echtem Verhalten ist das Verkleinern des Rasters:
' "Warnung mit Konsequenzliste, wenn Tage/Stunden verkleinert werden,
' waehrend Slot-Regeln oder Fenster existieren, die dann ins Leere
' zeigen." Das Raster ist zugleich die Grundlage des Rasterpickers
' (6.10) - wer es schrumpft, entwertet bestehende Auswahlen.
Imports TimetableCore
Imports TimetableProjekt

Public NotInheritable Class SchuldatenViewModel
    Inherits Beobachtbar

    Private ReadOnly _projekt As Projekt
    Private ReadOnly _dialoge As IDialoge

    Public Sub New(projekt As Projekt, dialoge As IDialoge)
        _projekt = projekt
        _dialoge = dialoge
    End Sub

    Public Event Geaendert As EventHandler

    Private Sub MeldeAenderung()
        RaiseEvent Geaendert(Me, EventArgs.Empty)
        Melde(NameOf(KapazitaetsZeile))
    End Sub

    Private ReadOnly Property Bestand As Stammdatenbestand
        Get
            Return _projekt.Bestand
        End Get
    End Property

    ' ---------------------------------------------------------------
    ' Einfache Felder
    ' ---------------------------------------------------------------

    Public Property SchulName As String
        Get
            Return Bestand.SchulName
        End Get
        Set
            If Bestand.SchulName <> value Then
                Bestand.SchulName = value
                Melde()
                MeldeAenderung()
            End If
        End Set
    End Property

    Public Property Schulart As String
        Get
            Return Bestand.Schulart
        End Get
        Set
            If Bestand.Schulart <> value Then
                Bestand.Schulart = value
                Melde()
                MeldeAenderung()
            End If
        End Set
    End Property

    ''' <summary>Informativ - der Kern rechnet damit nicht (6.2).</summary>
    Public Property Bundesland As String
        Get
            Return Bestand.Bundesland
        End Get
        Set
            If Bestand.Bundesland <> value Then
                Bestand.Bundesland = value
                Melde()
                MeldeAenderung()
            End If
        End Set
    End Property

    Public ReadOnly Property MoeglicheTage As String() = {"Mo", "Di", "Mi", "Do", "Fr", "Sa"}

    Public Function TagAktiv(tag As String) As Boolean
        Return Bestand.Tage.Contains(tag)
    End Function

    ' ---------------------------------------------------------------
    ' Das Raster - die Stelle mit Verhalten
    ' ---------------------------------------------------------------

    ''' <summary>Setzt Tage und Stunden. Wird das Raster KLEINER, werden
    ''' vorher die Regeln gesucht, die dann ins Leere zeigen, und der
    ''' Nutzer gefragt. Liefert False, wenn er abgelehnt hat - dann
    ''' bleibt alles, wie es war.
    '''
    ''' Bewusst EINE Operation fuer Tage und Stunden: wer von Fr auf Do
    ''' UND von 8 auf 6 Stunden geht, soll einmal die Gesamtfolge sehen
    ''' und nicht zwei Teilwarnungen nacheinander.</summary>
    Public Function SetzeRaster(tage As IEnumerable(Of String), stundenProTag As Integer) As Boolean
        Dim neueTage = If(tage, Array.Empty(Of String)()).ToList()
        If neueTage.Count = 0 Then
            _dialoge.Hinweis("Schuldaten", "Mindestens ein Unterrichtstag muss bleiben.")
            Return False
        End If
        If stundenProTag < 1 Then
            _dialoge.Hinweis("Schuldaten", "Mindestens eine Stunde je Tag muss bleiben.")
            Return False
        End If

        Dim wirdKleiner = neueTage.Count < Bestand.Tage.Count OrElse
                          stundenProTag < Bestand.PeriodsPerDay OrElse
                          Bestand.Tage.Any(Function(t) Not neueTage.Contains(t))

        If wirdKleiner Then
            Dim betroffen = Kennzahlen.RegelnAusserhalb(_projekt.Constraints, neueTage, stundenProTag)
            If betroffen.Count > 0 Then
                Dim zeilen = betroffen.Take(12).Select(Function(x) "  - " & x)
                Dim rest = betroffen.Count - 12
                Dim text = $"{betroffen.Count} Regel(n) zeigen danach ins Leere:" & vbLf &
                           String.Join(vbLf, zeilen) &
                           If(rest > 0, vbLf & $"  … und {rest} weitere", "") & vbLf & vbLf &
                           "Raster trotzdem verkleinern? Die Regeln bleiben erhalten, greifen aber nicht mehr."
                If Not _dialoge.Frage("Raster verkleinern", text) Then Return False
            End If
        End If

        Bestand.Tage = neueTage
        Bestand.PeriodsPerDay = stundenProTag
        Melde(NameOf(StundenProTag))
        MeldeAenderung()
        Return True
    End Function

    Public ReadOnly Property StundenProTag As Integer
        Get
            Return Bestand.PeriodsPerDay
        End Get
    End Property

    ''' <summary>Was in eine Klassenwoche passt, gegen das, was die
    ''' Kontingente je Stufe verlangen. Die Fusszeile aus 6.4 - hier
    ''' schon, weil sie am Raster haengt und der Nutzer die Folge seiner
    ''' Aenderung sofort sehen soll.</summary>
    Public ReadOnly Property KapazitaetsZeile As String
        Get
            Dim kap = Kennzahlen.KapazitaetJeKlasse(Bestand)
            Dim eng = Bestand.Klassenstufen.
                Select(Function(s) (s.Nummer, Soll:=Kennzahlen.SollJeKlasse(Bestand, s.Nummer))).
                Where(Function(x) x.Soll > kap).ToList()

            If eng.Count = 0 Then
                Return $"{kap} Stunden je Klassenwoche ({Bestand.Tage.Count} Tage x {Bestand.PeriodsPerDay})."
            End If
            Return $"{kap} Stunden je Klassenwoche - zu wenig fuer " &
                   String.Join(", ", eng.Select(Function(x) $"Stufe {x.Nummer} ({x.Soll})")) & "."
        End Get
    End Property

    Public Function Pruefe() As List(Of String)
        Return StammdatenValidation.ValidateStammdaten(Bestand)
    End Function

End Class
