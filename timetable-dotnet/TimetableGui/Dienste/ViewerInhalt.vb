' Erzeugt die Viewer-Seiten aus einem Lauf-Ergebnis - in-memory, ohne
' Datei. Duennste moegliche Schicht ueber TimetableWorkflow: die
' HTML-Vorlagen und ihre JSON-Einbettung existieren dort seit Phase 2.21
' und werden bewusst 1:1 gehostet, nicht in XAML nachgebaut
' (gui-ui-konzept.md, Leitprinzip "Viewer wiederverwenden").
Imports System.Text.Json.Nodes
Imports TimetableCore
Imports TimetableWorkflow

Public Module ViewerInhalt

    ''' <summary>Die Seite fuer das Klassenbildungs-Board. Nothing, wenn
    ''' der Lauf keine Variante hervorgebracht hat - dann zeigt die
    ''' Oberflaeche ihre eigene Fehlermeldung statt einer leeren Seite.</summary>
    Public Function KlassenbildungSeite(schule As String, e As KlassenbildungLaufErgebnis) As String
        Dim json = KlassenbildungLauf.BaueViewerJson(schule, e)
        If json Is Nothing Then Return Nothing
        Return KlassenbildungHtml.BuildKlassenbildungHtml(json.ToJsonString())
    End Function

    ''' <summary>Die Seite fuer die Stundentafel. Nothing ohne Loesung.</summary>
    Public Function StundenplanSeite(e As LaufErgebnis) As String
        If e Is Nothing OrElse e.BesterLauf Is Nothing Then Return Nothing
        Return StundentafelHtml.BuildStundentafelHtml(StundenplanBericht.BaueStundentafelJson(e).ToJsonString())
    End Function

    ''' <summary>Seite aus einem gesicherten Stand der Projektdatei - der
    ''' Weg, ueber den die Staende-Historie (gui-ui-konzept 6.13) frueheres
    ''' Ergebnis wieder anzeigt, ohne neu zu rechnen.</summary>
    Public Function AusGespeichertemJson(json As JsonObject, istKlassenbildung As Boolean) As String
        If json Is Nothing Then Return Nothing
        If istKlassenbildung Then Return KlassenbildungHtml.BuildKlassenbildungHtml(json.ToJsonString())
        Return StundentafelHtml.BuildStundentafelHtml(json.ToJsonString())
    End Function

End Module
