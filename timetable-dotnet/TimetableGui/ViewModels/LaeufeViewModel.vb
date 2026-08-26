' Die Historie der gesicherten Staende (gui-ui-konzept.md 6.13) samt
' Freigabe. Kein Fenster noetig: alle Entscheidungen - was geschuetzt
' ist, was geloescht werden darf, was ins Protokoll geht - liegen hier.
'
' Der aktive Lauf-Monitor bleibt, wo er ist (LaufMonitorViewModel). Hier
' geht es ausschliesslich um das, was NACH einem Lauf uebrig bleibt.
Imports System.Text.Json.Nodes
Imports TimetableProjekt

''' <summary>Eine Zeile der Historie - fertig aufbereitet, damit die
''' Ansicht nichts entscheiden muss.</summary>
Public NotInheritable Class Standzeile
    Public Property Id As String = ""
    Public Property Label As String = ""
    Public Property Art As String = ""
    Public Property Erstellt As DateTimeOffset
    Public Property Geschuetzt As Boolean
    Public Property IstFreigabe As Boolean
    Public Property Kennzahlen As String = ""

    Public ReadOnly Property Anzeige As String
        Get
            Dim marke = If(IstFreigabe, "  [freigegeben]", If(Geschuetzt, "  [geschützt]", ""))
            Return $"{Erstellt:dd.MM.yyyy HH:mm}   {Label}{marke}"
        End Get
    End Property
End Class

Public NotInheritable Class LaeufeViewModel

    Private ReadOnly _projekt As Projekt
    Private ReadOnly _dialoge As IDialoge
    Private ReadOnly _jetzt As Func(Of DateTimeOffset)

    Public Sub New(projekt As Projekt, dialoge As IDialoge, Optional jetzt As Func(Of DateTimeOffset) = Nothing)
        _projekt = projekt
        _dialoge = dialoge
        _jetzt = If(jetzt, Function() DateTimeOffset.Now)
    End Sub

    ''' <summary>Meldet, dass sich am Projekt etwas geaendert hat - der
    ''' Aufrufer setzt daraufhin den Ungespeichert-Indikator.</summary>
    Public Event Geaendert As EventHandler

    ''' <summary>Ein Stand soll angezeigt werden. Das Laden uebernimmt das
    ''' HauptViewModel; hier faellt nur die Entscheidung, WELCHER.</summary>
    Public Event Anzeigen As EventHandler(Of ProjektStand)

    ' ===============================================================
    ' Historie
    ' ===============================================================

    ''' <summary>Neueste zuerst - die Historie wird von oben gelesen, und
    ''' der interessante Stand ist fast immer der letzte.</summary>
    Public Function Zeilen() As List(Of Standzeile)
        If _projekt Is Nothing Then Return New List(Of Standzeile)
        Return _projekt.Staende.
            OrderByDescending(Function(s) s.Erstellt).
            Select(Function(s) Zeile(s)).ToList()
    End Function

    Private Function Zeile(stand As ProjektStand) As Standzeile
        Dim v = Freigabe.Vorlage(stand)
        Return New Standzeile With {
            .Id = stand.Id, .Label = stand.Label, .Art = v.Art,
            .Erstellt = stand.Erstellt, .Geschuetzt = stand.Geschuetzt,
            .IstFreigabe = IstFreigabe(stand),
            .Kennzahlen = v.Kennzahlen}
    End Function

    ''' <summary>Geschuetzt heisst nicht automatisch freigegeben: der
    ''' Bestandsplan (9.3) ist ebenfalls geschuetzt, aber niemandes
    ''' Entscheidung. Unterschieden wird an der Freigabe-Marke.</summary>
    Public Shared Function IstFreigabe(stand As ProjektStand) As Boolean
        Return stand IsNot Nothing AndAlso stand.Lauf IsNot Nothing AndAlso
               stand.Lauf.ContainsKey("freigabe") AndAlso stand.Lauf("freigabe") IsNot Nothing
    End Function

    Public Function Finde(id As String) As ProjektStand
        If _projekt Is Nothing Then Return Nothing
        Return _projekt.Staende.FirstOrDefault(Function(s) s.Id = id)
    End Function

    ''' <summary>Der freigegebene Stand einer ART. Die Einschraenkung ist
    ''' wesentlich: Klassenbildung und Stundenplan sind zwei getrennte
    ''' Entscheidungen mit je eigenem Nachweis. Ohne den Filter zog die
    ''' Freigabe eines Stundenplans die Freigabe der Klassenbildung
    ''' zurueck - ein zerstoerter Nachweis, gemeldet als sinnlose Frage
    ''' (live gefunden im Test, 26.08.2026).
    '''
    ''' Ohne `art` gilt: irgendein freigegebener Stand.</summary>
    Public Function Freigegebener(Optional art As String = Nothing) As ProjektStand
        If _projekt Is Nothing Then Return Nothing
        Return _projekt.Staende.FirstOrDefault(
            Function(s) IstFreigabe(s) AndAlso
                        (art Is Nothing OrElse Freigabe.ArtVon(s) = art))
    End Function

    ' ===============================================================
    ' Aktionen
    ' ===============================================================

    Public Sub Ansehen(id As String)
        Dim stand = Finde(id)
        If stand Is Nothing Then Return
        RaiseEvent Anzeigen(Me, stand)
    End Sub

    ''' <summary>Label aendern. Der Freigabe-Stand behaelt seines: sein
    ''' Label ist Teil des Nachweises, kein Notizzettel.</summary>
    Public Function Umbenennen(id As String, label As String) As Boolean
        Dim stand = Finde(id)
        If stand Is Nothing OrElse String.IsNullOrWhiteSpace(label) Then Return False
        If IstFreigabe(stand) Then
            _dialoge.Hinweis("Freigegeben",
                             "Das Label eines freigegebenen Standes bleibt, wie es ist - es gehört zum Nachweis.")
            Return False
        End If
        stand.Label = label.Trim()
        RaiseEvent Geaendert(Me, EventArgs.Empty)
        Return True
    End Function

    ''' <summary>Loeschen mit Konsequenzen-Dialog (Konzept 7). Geschuetzte
    ''' Staende bleiben stehen; die Audit-Zeile des Laufs bleibt in JEDEM
    ''' Fall erhalten - sie ist der Nachweis, nicht der Stand.</summary>
    Public Function Loeschen(id As String) As Boolean
        Dim stand = Finde(id)
        If stand Is Nothing Then Return False
        If stand.Geschuetzt Then
            _dialoge.Hinweis("Geschützt",
                             If(IstFreigabe(stand),
                                "Ein freigegebener Stand wird nicht gelöscht.",
                                "Dieser Stand ist gegen Löschen geschützt."))
            Return False
        End If

        Dim frage = $"{stand.Label} vom {stand.Erstellt:dd.MM.yyyy HH:mm} löschen?" & vbLf & vbLf &
                    "Das Ergebnis ist danach nicht mehr anzeigbar. " &
                    "Die Protokollzeile des Laufs bleibt erhalten."
        If Not _dialoge.Frage("Stand löschen", frage) Then Return False

        If Not _projekt.StandLoeschen(id) Then Return False
        _projekt.Protokolliere(Umgebung.Benutzer, "stand", $"Stand {id} gelöscht", _jetzt())
        RaiseEvent Geaendert(Me, EventArgs.Empty)
        Return True
    End Function

    ''' <summary>Freigabe (klassenbildung-konzept 10.1). Der Dialog zeigt
    ''' die verbleibenden Abweichungen und verlangt eine aktive
    ''' Bestaetigung mit Namen - "ein blosser OK-Klick ohne angezeigte
    ''' Abweichungen waere als menschliche Beteiligung angreifbar".
    '''
    ''' Was danach im Projekt steht, ist der NACHWEIS: Person, Zeitpunkt
    ''' und der Abweichungsbericht, WIE ER ANGEZEIGT WURDE. Ihn spaeter
    ''' neu zu berechnen waere wertlos - freigegeben wurde, was auf dem
    ''' Bildschirm stand.</summary>
    Public Function Freigeben(id As String) As Boolean
        Dim stand = Finde(id)
        If stand Is Nothing Then Return False

        If IstFreigabe(stand) Then
            _dialoge.Hinweis("Bereits freigegeben", "Dieser Stand ist schon freigegeben.")
            Return False
        End If

        Dim vorlage = Freigabe.Vorlage(stand)
        If vorlage.Art = "unbekannt" Then
            _dialoge.Hinweis("Nichts freizugeben", "Dieser Stand enthält kein Ergebnis.")
            Return False
        End If

        ' Eine bestehende Freigabe wird NICHT still ersetzt: sie ist der
        ' Nachweis einer Entscheidung, und zwei davon nebeneinander waeren
        ' genau die Unklarheit, die der Nachweis ausschliessen soll.
        Dim bisher = Freigegebener(vorlage.Art)
        If bisher IsNot Nothing Then
            Dim frage = $"Freigegeben ist derzeit {bisher.Label} vom {bisher.Erstellt:dd.MM.yyyy}." &
                        vbLf & vbLf &
                        $"Die bisherige Freigabe der {vorlage.Art}-Entscheidung wird zurückgezogen; " &
                        "ihre Protokollzeile bleibt erhalten." & vbLf & vbLf &
                        "Freigaben anderer Art sind davon nicht betroffen."
            If Not _dialoge.Frage($"Freigabe ersetzen ({vorlage.Art})", frage) Then Return False
        End If

        Dim antwort = _dialoge.FreigabeBestaetigen(vorlage)
        If antwort Is Nothing OrElse Not antwort.Bestaetigt Then Return False
        If String.IsNullOrWhiteSpace(antwort.Person) Then
            _dialoge.Hinweis("Name fehlt", "Eine Freigabe ohne benannte Person ist kein Nachweis.")
            Return False
        End If
        ' Die eigene Begruendung ist der Beleg der Befassung. Sie hier
        ' und nicht nur im Dialog zu erzwingen ist kein Misstrauen gegen
        ' das Fenster, sondern die Stelle, an der die Regel steht: das
        ' Fenster kann man umgehen, diese Funktion nicht.
        If vorlage.NotizPflicht AndAlso String.IsNullOrWhiteSpace(antwort.Notiz) Then
            _dialoge.Hinweis("Begründung fehlt",
                "Bei verbleibenden Abweichungen gehört eine eigene Begründung zum Nachweis - " &
                "ein Häkchen allein belegt keine Befassung.")
            Return False
        End If

        Dim zeitpunkt = _jetzt()
        If bisher IsNot Nothing Then
            bisher.Lauf.Remove("freigabe")
            bisher.Geschuetzt = False
            _projekt.Protokolliere(Umgebung.Benutzer, "freigabe",
                                   $"Freigabe von Stand {bisher.Id} zurückgezogen", zeitpunkt)
        End If

        If stand.Lauf Is Nothing Then stand.Lauf = New JsonObject()
        Dim abweichungen As New JsonArray()
        For Each a In vorlage.Abweichungen
            abweichungen.Add(a)
        Next
        stand.Lauf("freigabe") = New JsonObject From {
            {"person", antwort.Person.Trim()},
            {"zeitpunkt", zeitpunkt.ToString("o")},
            {"satz", Freigabe.Bestaetigungssatz(vorlage)},
            {"abweichungen", abweichungen},
            {"kennzahlen", vorlage.Kennzahlen},
            {"notiz", If(antwort.Notiz, "").Trim()}
        }
        ' Geschuetzt heisst: weder Verdraengung noch Loeschen. Beides
        ' erledigt der Kern (Projekt.StandHinzufuegen/StandLoeschen).
        stand.Geschuetzt = True

        _projekt.Protokolliere(antwort.Person.Trim(), "freigabe",
            $"Stand {stand.Id} freigegeben ({vorlage.Art}). " &
            Freigabe.Bestaetigungssatz(vorlage) &
            If(vorlage.Abweichungen.Count = 0, "",
               " Abweichungen: " & String.Join(" | ", vorlage.Abweichungen)) &
            If(String.IsNullOrWhiteSpace(antwort.Notiz), "",
               " Begründung: " & antwort.Notiz.Trim()), zeitpunkt)

        RaiseEvent Geaendert(Me, EventArgs.Empty)
        Return True
    End Function

End Class
