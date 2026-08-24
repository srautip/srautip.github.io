' Auswahllogik des Rasterpickers (gui-ui-konzept.md 6.10, "zentrales
' Shared Control"): Tag x Stunde, Klick oder Ziehen waehlt Slots oder ein
' Fenster.
'
' BEWUSST OHNE WPF. Die Logik liegt hier, das Steuerelement daneben ist
' nur Anzeige und Eingabe. Sonst waere die einzige Stelle, an der sich
' pruefen liesse, ob "von Mo/3 bis Do/5" das Richtige auswaehlt, das
' laufende Fenster - und genau das ist auf diesem Rechner nicht
' beobachtbar (arc42 8.13).
'
' Das Raster kennt seine Groesse aus `tage`/`periods_per_day` der
' Schuldaten (6.2). Deshalb verlangt 6.2 auch die Warnung mit
' Konsequenzliste, wenn jemand das Raster verkleinert: bestehende
' Auswahlen zeigen dann ins Leere.
Imports System.Text

Public NotInheritable Class RasterAuswahl

    Private ReadOnly _tage As List(Of String)
    Private ReadOnly _gewaehlt As New HashSet(Of String)(StringComparer.Ordinal)

    ' ACHTUNG: der Parameter heisst NICHT "stunden". VB ist
    ' gross-/kleinschreibungsunempfindlich - Parameter und Eigenschaft
    ' waeren derselbe Bezeichner, und "Stunden = stunden" wiese den
    ' Parameter sich selbst zu. Die Eigenschaft bliebe 0, und damit
    ' waere KEINE Zelle gueltig (live erlebt).
    Public Sub New(wochentage As IEnumerable(Of String), anzahlStunden As Integer)
        _tage = If(wochentage, Array.Empty(Of String)()).ToList()
        If _tage.Count = 0 Then Throw New ArgumentException("Ein Raster ohne Tage gibt es nicht.", NameOf(wochentage))
        If anzahlStunden < 1 Then Throw New ArgumentOutOfRangeException(NameOf(anzahlStunden), "Mindestens eine Stunde.")
        Stunden = anzahlStunden
    End Sub

    Public ReadOnly Property Tage As IReadOnlyList(Of String)
        Get
            Return _tage
        End Get
    End Property

    ''' <summary>Stunden je Tag, 1-basiert - wie `period` im Wire-Format
    ''' (arc42 8.7). Ein 0-basiertes Raster hier waere eine zweite
    ''' Zaehlweise und damit eine Fehlerquelle an jeder Grenze.</summary>
    Public ReadOnly Property Stunden As Integer

    Private Shared Function Schluessel(tag As String, stunde As Integer) As String
        Return tag & "|" & stunde.ToString(Globalization.CultureInfo.InvariantCulture)
    End Function

    Private Function Gueltig(tag As String, stunde As Integer) As Boolean
        Return _tage.Contains(tag) AndAlso stunde >= 1 AndAlso stunde <= Stunden
    End Function

    Public Function IstGewaehlt(tag As String, stunde As Integer) As Boolean
        Return _gewaehlt.Contains(Schluessel(tag, stunde))
    End Function

    Public ReadOnly Property Anzahl As Integer
        Get
            Return _gewaehlt.Count
        End Get
    End Property

    ''' <summary>Setzt eine Zelle. Ausserhalb des Rasters liegende Zellen
    ''' werden STILL ignoriert statt zu werfen: beim Ziehen laeuft der
    ''' Zeiger regelmaessig ueber den Rand, und eine Ausnahme mitten in
    ''' einer Mausbewegung waere kein sinnvolles Verhalten.</summary>
    Public Sub Setze(tag As String, stunde As Integer, gewaehlt As Boolean)
        If Not Gueltig(tag, stunde) Then Return
        If gewaehlt Then
            _gewaehlt.Add(Schluessel(tag, stunde))
        Else
            _gewaehlt.Remove(Schluessel(tag, stunde))
        End If
    End Sub

    Public Sub Umschalten(tag As String, stunde As Integer)
        If Not Gueltig(tag, stunde) Then Return
        Setze(tag, stunde, Not IstGewaehlt(tag, stunde))
    End Sub

    ''' <summary>Rechteck zwischen zwei Zellen - das Ergebnis eines
    ''' Ziehens. Die Reihenfolge der Ecken ist egal; wer von unten rechts
    ''' nach oben links zieht, meint dasselbe Rechteck.</summary>
    Public Sub Bereich(vonTag As String, vonStunde As Integer,
                       bisTag As String, bisStunde As Integer, gewaehlt As Boolean)
        Dim a = _tage.IndexOf(vonTag), b = _tage.IndexOf(bisTag)
        If a < 0 OrElse b < 0 Then Return
        For t = Math.Min(a, b) To Math.Max(a, b)
            For s = Math.Max(1, Math.Min(vonStunde, bisStunde)) To Math.Min(Stunden, Math.Max(vonStunde, bisStunde))
                Setze(_tage(t), s, gewaehlt)
            Next
        Next
    End Sub

    Public Sub Leeren()
        _gewaehlt.Clear()
    End Sub

    Public Sub UebernehmeSlots(slots As IEnumerable(Of (Tag As String, Stunde As Integer)))
        Leeren()
        If slots Is Nothing Then Return
        For Each s In slots
            Setze(s.Tag, s.Stunde, True)
        Next
    End Sub

    ''' <summary>Die Auswahl als einzelne Slots, in Rasterreihenfolge.
    ''' `forbidden_slot` erzeugt daraus EINE REGEL JE SLOT (6.10) - die
    ''' Gruppierung in der Liste ist reine Anzeige.</summary>
    Public Function AlsSlots() As List(Of (Tag As String, Stunde As Integer))
        Dim liste As New List(Of (Tag As String, Stunde As Integer))
        For Each tag In _tage
            For s = 1 To Stunden
                If IstGewaehlt(tag, s) Then liste.Add((tag, s))
            Next
        Next
        Return liste
    End Function

    ''' <summary>Die Auswahl als Fenster (Tage + von/bis), oder Nothing,
    ''' wenn sie kein Rechteck ist.
    '''
    ''' Das Nothing ist der Punkt: `subject_period_window` und
    ''' `occupied_window` koennen nur ein von/bis ausdruecken. Eine
    ''' zerklueftete Auswahl stillschweigend auf ihre Huelle zu runden
    ''' waere eine Regel, die der Nutzer nicht gemeint hat - die Maske
    ''' muss stattdessen nachfragen.</summary>
    Public Function AlsFenster() As (Tage As List(Of String), Von As Integer, Bis As Integer)?
        Dim slots = AlsSlots()
        If slots.Count = 0 Then Return Nothing

        Dim tage = slots.Select(Function(x) x.Tag).Distinct().ToList()
        Dim von = slots.Min(Function(x) x.Stunde)
        Dim bis = slots.Max(Function(x) x.Stunde)

        ' Rechteck heisst: JEDER gewaehlte Tag traegt genau die Stunden
        ' von..bis, luecken- und ueberhangfrei.
        Dim proTag = (bis - von + 1)
        If slots.Count <> tage.Count * proTag Then Return Nothing
        For Each t In tage
            For s = von To bis
                If Not IstGewaehlt(t, s) Then Return Nothing
            Next
        Next

        ' Tage in Rasterreihenfolge, nicht in Auswahlreihenfolge.
        Return (_tage.Where(Function(t) tage.Contains(t)).ToList(), von, bis)
    End Function

    ''' <summary>Kurzfassung fuer Listenanzeige und Tooltip, z.B.
    ''' "Mo, Di 3.-5." bzw. "4 Slots". Bewusst hier und nicht in der
    ''' Maske: sonst schriebe sie jede Maske ein wenig anders.</summary>
    Public Function Beschreibung() As String
        If _gewaehlt.Count = 0 Then Return "keine Auswahl"
        Dim f = AlsFenster()
        If f.HasValue Then
            Dim v = f.Value
            Dim spanne = If(v.Von = v.Bis, $"{v.Von}.", $"{v.Von}.-{v.Bis}.")
            Return String.Join(", ", v.Tage) & " " & spanne & " Stunde"
        End If
        Return $"{_gewaehlt.Count} Slots"
    End Function

End Class
