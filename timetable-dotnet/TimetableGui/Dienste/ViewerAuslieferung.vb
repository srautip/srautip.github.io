' Liefert die Viewer-Seiten an WebView2 aus - IN-MEMORY, ohne je eine
' Klartext-HTML-Datei anzulegen.
'
' Diese Klasse kennt WebView2 nicht. Sie beantwortet nur die Frage "welche
' Bytes gehoeren zu dieser URL"; das Anhaengen an den Browser macht
' ViewerHost. Der Schnitt ist Absicht: die interessante Zusage - kein
' Klartext auf Platte, keine Groessengrenze - laesst sich so ohne Fenster
' und ohne Browser pruefen.
'
' WARUM NICHT NavigateToString, und warum kein virtuelles Host-Mapping:
' Das Datenhaltungskonzept (7.6) liess beides offen ("NavigateToString
' bzw. virtuelles Host-Mapping"). Beides scheidet aus.
'   * NavigateToString hat eine dokumentierte Grenze von rund 2 MB. Die
'     Stundentafel-Seite ueberschreitet sie in realistischen
'     Konfigurationen: am GMS-Beispiel wurden 2,49 MB gemessen (28
'     exportierte Loesungen). Die Groesse ist dabei LAUF- UND
'     KONFIGURATIONSABHAENGIG - derselbe Datensatz ergab bei einem
'     spaeteren Lauf mit 20 Loesungen nur 1,77 MB -, weil der Export ALLE
'     gefundenen Loesungen enthaelt und damit mit max_solutions und
'     Schulgroesse skaliert. Eine Anzeige, die ab einer bestimmten
'     Loesungszahl kaputtgeht, ist keine Option.
'   * Virtuelles Host-Mapping (SetVirtualHostNameToFolderMapping) bildet
'     einen ORDNER ab. Dafuer muesste die entschluesselte HTML als Datei
'     auf der Platte liegen - genau das, was 7.6 verbietet.
' Bleibt WebResourceRequested: eine synthetische Herkunft, deren Antworten
' vollstaendig aus dem Speicher kommen.
Imports System.Text

Public NotInheritable Class ViewerAuslieferung

    ''' <summary>Synthetische Herkunft. `.local` ist per RFC 6762 fuer
    ''' lokale Namensaufloesung reserviert und wird nie oeffentlich
    ''' aufgeloest - selbst wenn eine Navigation durchrutschte, ginge sie
    ''' nirgendwohin.</summary>
    Public Const Ursprung As String = "https://viewer.local"

    Public Const SeitenPfad As String = "/viewer.html"

    Private ReadOnly _gate As New Object()
    Private _seite As Byte() = Array.Empty(Of Byte)()

    Public ReadOnly Property SeitenUrl As String
        Get
            Return Ursprung & SeitenPfad
        End Get
    End Property

    ''' <summary>Alle URLs unterhalb der synthetischen Herkunft - das
    ''' Filtermuster, das WebView2 abfangen soll.</summary>
    Public ReadOnly Property Filtermuster As String
        Get
            Return Ursprung & "/*"
        End Get
    End Property

    ''' <summary>Groesse der aktuell hinterlegten Seite in Bytes.</summary>
    Public ReadOnly Property SeitenGroesse As Integer
        Get
            SyncLock _gate
                Return _seite.Length
            End SyncLock
        End Get
    End Property

    Public Sub Setze(html As String)
        Dim bytes = If(html Is Nothing, Array.Empty(Of Byte)(), New UTF8Encoding(False).GetBytes(html))
        SyncLock _gate
            _seite = bytes
        End SyncLock
    End Sub

    ''' <summary>Antwort auf eine abgefangene Anfrage.
    ''' `Gefunden = False` heisst: diese URL gehoert nicht zu uns und darf
    ''' NICHT ausgeliefert werden (die Seite soll nichts nachladen
    ''' koennen).</summary>
    Public Function Antwort(url As String) As (Gefunden As Boolean, Inhalt As Byte(), Status As Integer, Kopfzeilen As String)
        Dim pfad = PfadVon(url)
        If pfad <> SeitenPfad Then
            Return (False, Array.Empty(Of Byte)(), 404, Kopf("text/plain; charset=utf-8"))
        End If
        SyncLock _gate
            Return (True, _seite, 200, Kopf("text/html; charset=utf-8"))
        End SyncLock
    End Function

    ''' <summary>Cache-Unterdrueckung ist hier kein Feintuning: die Seite
    ''' aendert sich bei jedem Lauf unter DERSELBEN URL. Ohne no-store
    ''' zeigte der Viewer nach dem zweiten Rechnen den alten Stand.</summary>
    Private Shared Function Kopf(inhaltstyp As String) As String
        Return $"Content-Type: {inhaltstyp}" & vbCrLf &
               "Cache-Control: no-store, no-cache, must-revalidate" & vbCrLf &
               "X-Content-Type-Options: nosniff"
    End Function

    ''' <summary>Pfad ohne Query/Fragment. Eine kaputte URL ist kein
    ''' Ausnahmefall, sondern schlicht "gehoert nicht zu uns".</summary>
    Private Shared Function PfadVon(url As String) As String
        If String.IsNullOrEmpty(url) Then Return String.Empty
        Dim u As Uri = Nothing
        If Not Uri.TryCreate(url, UriKind.Absolute, u) Then Return String.Empty
        If Not String.Equals(u.GetLeftPart(UriPartial.Authority), Ursprung, StringComparison.OrdinalIgnoreCase) Then
            Return String.Empty
        End If
        Return u.AbsolutePath
    End Function

End Class
