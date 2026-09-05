' Undo/Redo fuer das Klassenbildungs-Board im GUI-Betrieb (U6).
'
' Im Doppelklick-Betrieb fuehrt die Seite ihren Verlauf selbst. Im Host
' geht das nicht: "Neu rechnen" erzeugt einen neuen Stand und laedt die
' Seite neu - ihr Gedaechtnis ist dann weg. Deshalb liegt der Verlauf
' HIER: ein Schritt ist der Stand, der gezeigt wird, plus der
' Board-Zustand (Pins, Haertungen, Basis) plus die Eingaben der
' Klassenbildung, die "Neu rechnen" veraendert (Fixierungen, Modus der
' Regeln). Zurueck heisst: alle drei wiederherstellen und die Seite mit
' dem alten Zustand neu laden.
'
' Der Verlauf lebt nur in der Sitzung - er ist Bedienkomfort, kein
' Nachweis. Der Nachweis sind die Staende und das Protokoll.
Imports System.Text.Json.Nodes
Imports TimetableCore

Public NotInheritable Class VerlaufSchritt
    Public Property StandId As String
    Public Property Zustand As JsonObject
    Public Property Eingabe As JsonObject

    ''' <summary>Zwei Schritte mit gleichem Schluessel sind derselbe -
    ''' der Verlauf legt sie nicht doppelt ab.</summary>
    Friend Function Schluessel() As String
        Return If(StandId, "") & "|" & If(Zustand?.ToJsonString(), "") & "|" & If(Eingabe?.ToJsonString(), "")
    End Function
End Class

Public NotInheritable Class Verlauf

    Private ReadOnly _schritte As New List(Of VerlaufSchritt)
    Private _index As Integer = -1

    Public ReadOnly Property Aktuell As VerlaufSchritt
        Get
            Return If(_index >= 0 AndAlso _index < _schritte.Count, _schritte(_index), Nothing)
        End Get
    End Property

    Public ReadOnly Property Anzahl As Integer
        Get
            Return _schritte.Count
        End Get
    End Property

    Public ReadOnly Property Zurueckzaehler As Integer
        Get
            Return Math.Max(0, _index)
        End Get
    End Property

    Public ReadOnly Property Vorzaehler As Integer
        Get
            Return Math.Max(0, _schritte.Count - 1 - _index)
        End Get
    End Property

    ''' <summary>Einen neuen Schritt anhaengen. Alles, was nach dem
    ''' aktuellen Schritt lag (die Redo-Kette), verfaellt - so verhaelt
    ''' sich jeder Editor. Ein Schritt, der dem aktuellen gleicht, wird
    ''' nicht abgelegt: ein erneutes Laden derselben Seite ist kein
    ''' Schritt.</summary>
    Public Sub Merke(neu As VerlaufSchritt)
        If neu Is Nothing Then Return
        If Aktuell IsNot Nothing AndAlso Aktuell.Schluessel() = neu.Schluessel() Then Return
        If _index < _schritte.Count - 1 Then
            _schritte.RemoveRange(_index + 1, _schritte.Count - 1 - _index)
        End If
        _schritte.Add(neu)
        _index = _schritte.Count - 1
    End Sub

    Public Function Zurueck() As VerlaufSchritt
        If _index <= 0 Then Return Nothing
        _index -= 1
        Return Aktuell
    End Function

    Public Function Vor() As VerlaufSchritt
        If _index >= _schritte.Count - 1 Then Return Nothing
        _index += 1
        Return Aktuell
    End Function

    Public Sub Leeren()
        _schritte.Clear()
        _index = -1
    End Sub

End Class

''' <summary>Das, was "Neu rechnen" an den Eingaben der Klassenbildung
''' veraendert - als JSON, damit ein Schritt es tragen und vergleichen
''' kann: Fixierungen (aus den Pins), Modus der Gruppen und Wuensche
''' (aus den Haertungen). Nicht die Kinder, nicht die Regeln selbst -
''' die aendert das Board nicht.</summary>
Public Module Eingabeschnappschuss

    Public Function Aufnehmen(kb As KlassenbildungInput) As JsonObject
        Dim bild As New JsonObject()
        If kb Is Nothing Then Return bild
        Dim fix As New JsonArray()
        For Each f In kb.Fixierungen
            Dim o As New JsonObject From {{"kind", f.Kind}}
            If f.Klasse.HasValue Then o("klasse") = f.Klasse.Value
            If f.NichtKlasse.HasValue Then o("nicht_klasse") = f.NichtKlasse.Value
            fix.Add(o)
        Next
        bild("fixierungen") = fix
        Dim gruppen As New JsonObject()
        For Each g In kb.Gruppen
            gruppen(g.Id) = g.Modus
        Next
        bild("gruppen") = gruppen
        Dim wuensche As New JsonArray()
        For Each w In kb.Wuensche
            wuensche.Add(w.Modus)
        Next
        bild("wuensche") = wuensche
        Return bild
    End Function

    Public Sub Anwenden(kb As KlassenbildungInput, bild As JsonObject)
        If kb Is Nothing OrElse bild Is Nothing Then Return
        Dim fix = TryCast(bild("fixierungen"), JsonArray)
        If fix IsNot Nothing Then
            kb.Fixierungen.Clear()
            For Each eintrag In fix
                Dim o = TryCast(eintrag, JsonObject)
                If o Is Nothing OrElse Not o.ContainsKey("kind") Then Continue For
                Dim f As New KlassenbildungFixierung With {.Kind = o("kind").GetValue(Of String)()}
                If o.ContainsKey("klasse") Then f.Klasse = o("klasse").GetValue(Of Integer)()
                If o.ContainsKey("nicht_klasse") Then f.NichtKlasse = o("nicht_klasse").GetValue(Of Integer)()
                kb.Fixierungen.Add(f)
            Next
        End If
        Dim gruppen = TryCast(bild("gruppen"), JsonObject)
        If gruppen IsNot Nothing Then
            For Each g In kb.Gruppen
                If gruppen.ContainsKey(g.Id) Then g.Modus = gruppen(g.Id).GetValue(Of String)()
            Next
        End If
        Dim wuensche = TryCast(bild("wuensche"), JsonArray)
        If wuensche IsNot Nothing Then
            For i = 0 To Math.Min(wuensche.Count, kb.Wuensche.Count) - 1
                kb.Wuensche(i).Modus = wuensche(i).GetValue(Of String)()
            Next
        End If
    End Sub

End Module
