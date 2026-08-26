' Freigabe eines Standes (gui-ui-konzept.md 6.13, klassenbildung-konzept 10).
'
' Der Kern der Sache ist NICHT das Setzen eines Häkchens. Art. 22 DSGVO
' verlangt den Nachweis, dass eine echte menschliche Pruefung
' stattgefunden hat - "ein blosser OK-Klick ohne angezeigte Abweichungen
' waere als menschliche Beteiligung angreifbar". Deshalb liegt hier die
' Aufbereitung der ABWEICHUNGEN: was der Bestaetigende gesehen haben
' muss, bevor seine Bestaetigung etwas wert ist.
'
' Gelesen wird aus dem gespeicherten Viewer-JSON des Standes und nicht
' aus einem neuen Lauf: freigegeben wird genau das, was angezeigt wurde.
Imports System.Text.Json.Nodes
Imports TimetableProjekt

''' <summary>Was der Freigabe-Dialog anzeigen muss. Reiner Datenhalter -
''' die Oberflaeche formatiert, entscheidet aber nichts.</summary>
Public NotInheritable Class Freigabevorlage
    Public Property StandId As String = ""
    Public Property Label As String = ""
    Public Property Art As String = ""
    ''' <summary>Die verbleibenden Regelabweichungen im Klartext. LEER
    ''' heisst wirklich "keine" - der Dialog sagt das dann auch, statt
    ''' eine leere Liste zu zeigen.</summary>
    Public Property Abweichungen As New List(Of String)
    ''' <summary>Harte Verstoesse. Sie verhindern die Freigabe nicht
    ''' (das waere Bevormundung), muessen aber getrennt und deutlich
    ''' erscheinen - sie sind etwas anderes als eine nicht erfuellte
    ''' Kann-Regel.</summary>
    Public Property HarteVerstoesse As Integer
    Public Property Kennzahlen As String = ""

    ''' <summary>Ob eine eigene Begruendung PFLICHT ist. Genau dann,
    ''' wenn es etwas abzuwaegen gibt - ohne Abweichungen waere der
    ''' Zwang zur Notiz selbst wieder Theater, und Theater entwertet den
    ''' Nachweis, statt ihn zu staerken.</summary>
    Public ReadOnly Property NotizPflicht As Boolean
        Get
            Return Abweichungen.Count > 0
        End Get
    End Property

    ''' <summary>Die Frage, die im Dialog ueber dem Notizfeld steht.
    ''' Bewusst KONKRET: "Bemerkung" erzeugt "ok", die Frage nach der
    ''' Vertretbarkeit erzeugt eine Begruendung.</summary>
    Public ReadOnly Property Notizfrage As String
        Get
            If Abweichungen.Count = 0 Then
                Return "Anmerkung zur Freigabe (freiwillig):"
            End If
            Return "Warum ist der Stand trotz dieser Abweichungen vertretbar?"
        End Get
    End Property
End Class

''' <summary>Die Antwort des Dialogs. Nothing steht fuer Abbruch.</summary>
Public NotInheritable Class Freigabebestaetigung
    Public Property Person As String = ""
    Public Property Bestaetigt As Boolean
    ''' <summary>Die eigene Begruendung der freigebenden Person.
    '''
    ''' Sie ist der eigentliche Beleg der Befassung: der feste
    ''' Bestaetigungssatz zeigt, DASS geklickt wurde, diese Notiz zeigt,
    ''' dass die Abweichungen gelesen und abgewogen wurden. Wer sie
    ''' schreibt, hat sie zur Kenntnis genommen - das laesst sich nicht
    ''' mechanisch erzeugen, ein Haken schon.</summary>
    Public Property Notiz As String = ""
End Class

Public Module Freigabe

    ''' <summary>Der Satz, den die bestaetigende Person unterschreibt. Er
    ''' NENNT die Zahl der geprueften Abweichungen - genau daran haengt,
    ''' dass die Bestaetigung Substanz hat und kein Durchwinken ist
    ''' (klassenbildung-konzept 10.1).</summary>
    Public Function Bestaetigungssatz(vorlage As Freigabevorlage) As String
        If vorlage Is Nothing Then Return ""
        Dim gegenstand = If(vorlage.Art = "Klassenbildung", "die Klassenzuordnung", "den Stundenplan")
        If vorlage.Abweichungen.Count = 0 Then
            Return $"Ich habe den Stand geprüft – es verbleiben keine Regelabweichungen – " &
                   $"und entscheide {gegenstand} in eigener Verantwortung."
        End If
        Dim zahl = vorlage.Abweichungen.Count
        Return $"Ich habe die verbleibende{If(zahl = 1, "", "n")} {zahl} " &
               $"Regelabweichung{If(zahl = 1, "", "en")} (siehe Liste) geprüft und " &
               $"entscheide {gegenstand} in eigener Verantwortung."
    End Function

    ''' <summary>Baut die Vorlage aus einem Stand. `loesung` ist der
    ''' 1-basierte Index der markierten Loesung (siehe
    ''' `lauf.arbeitsstand`); ohne Markierung gilt die erste - dieselbe,
    ''' die das Dashboard beim Oeffnen zeigt.</summary>
    Public Function Vorlage(stand As ProjektStand) As Freigabevorlage
        If stand Is Nothing Then Return Nothing
        Dim v As New Freigabevorlage With {.StandId = stand.Id, .Label = stand.Label}

        If stand.Stundenplan IsNot Nothing Then
            v.Art = "Stundenplan"
            FuelleStundenplan(v, stand)
        ElseIf stand.Klassenbildung IsNot Nothing Then
            v.Art = "Klassenbildung"
            FuelleKlassenbildung(v, stand)
        Else
            ' Ein Stand ohne Ergebnis ist nichts, was man freigeben
            ' koennte - und das ehrlich zu sagen ist besser, als eine
            ' leere Liste als "keine Abweichungen" auszugeben.
            v.Art = "unbekannt"
            v.Kennzahlen = "Dieser Stand enthält kein Ergebnis."
        End If
        Return v
    End Function

    Private Function GewaehlteLoesung(stand As ProjektStand) As Integer
        If stand.Lauf Is Nothing Then Return 1
        Dim a = TryCast(stand.Lauf("arbeitsstand"), JsonObject)
        If a Is Nothing OrElse Not a.ContainsKey("loesung") Then Return 1
        Try
            Return Math.Max(1, a("loesung").GetValue(Of Integer)())
        Catch ex As InvalidOperationException
            Return 1
        End Try
    End Function

    Private Sub FuelleStundenplan(v As Freigabevorlage, stand As ProjektStand)
        Dim loesungen = TryCast(stand.Stundenplan("solutions"), JsonArray)
        If loesungen Is Nothing OrElse loesungen.Count = 0 Then
            v.Kennzahlen = "Der Stand enthält keine Lösung."
            Return
        End If

        Dim index = Math.Min(GewaehlteLoesung(stand), loesungen.Count) - 1
        Dim sol = TryCast(loesungen(index), JsonObject)
        If sol Is Nothing Then Return

        Dim muss = Ganzzahl(sol, "muss_violation_count")
        Dim kann = Ganzzahl(sol, "kann_violation_count")
        v.HarteVerstoesse = muss

        If muss > 0 Then
            v.Abweichungen.Add($"{muss} verletzte Muss-Regel(n) – der Plan hält eine harte Vorgabe nicht ein.")
        End If
        If kann > 0 Then
            v.Abweichungen.Add($"{kann} nicht erfüllte Kann-Regel(n).")
        End If
        ' Luecken und Randstunden sind KEINE Regelverletzungen, sondern
        ' Qualitaetsmerkmale. Sie stehen deshalb bei den Kennzahlen und
        ' nicht bei den Abweichungen - sonst zaehlt der
        ' Bestaetigungssatz Dinge mit, die niemand versprochen hat.
        v.Kennzahlen = $"Lösung {index + 1}, Zielwert {Ganzzahl(sol, "quality_total")}, " &
                       $"{Ganzzahl(sol, "class_gap_count")} Klassen- und " &
                       $"{Ganzzahl(sol, "teacher_gap_count")} Lehrerlücken, " &
                       $"{Ganzzahl(sol, "edge_period_count")} Randstunden."
    End Sub

    Private Sub FuelleKlassenbildung(v As Freigabevorlage, stand As ProjektStand)
        Dim varianten = TryCast(stand.Klassenbildung("varianten"), JsonArray)
        If varianten Is Nothing OrElse varianten.Count = 0 Then
            v.Kennzahlen = "Der Stand enthält keine Variante."
            Return
        End If

        Dim index = Math.Min(GewaehlteLoesung(stand), varianten.Count) - 1
        Dim variante = TryCast(varianten(index), JsonObject)
        If variante Is Nothing Then Return

        Dim verletzungen = TryCast(variante("verletzungen"), JsonArray)
        If verletzungen IsNot Nothing Then
            For Each eintrag In verletzungen
                Dim o = TryCast(eintrag, JsonObject)
                If o Is Nothing Then Continue For
                Dim id = Zeichenkette(o, "regel_id")
                Dim typ = Zeichenkette(o, "regel_typ")
                Dim prio = Ganzzahl(o, "prio")
                ' Die Regel-ID ist ein Pseudonym (G_kita_sonnenblume_1),
                ' kein Personenbezug - sie darf hier stehen.
                v.Abweichungen.Add($"{typ} {id} (Prioritaet {prio}) nicht erfuellt.")
            Next
        End If
        v.Kennzahlen = $"Variante {index + 1} von {varianten.Count}."
    End Sub

    Private Function Ganzzahl(o As JsonObject, schluessel As String) As Integer
        If o Is Nothing OrElse Not o.ContainsKey(schluessel) OrElse o(schluessel) Is Nothing Then Return 0
        Try
            Return CInt(Math.Round(o(schluessel).GetValue(Of Double)()))
        Catch ex As InvalidOperationException
            Return 0
        Catch ex As FormatException
            Return 0
        End Try
    End Function

    Private Function Zeichenkette(o As JsonObject, schluessel As String) As String
        If o Is Nothing OrElse Not o.ContainsKey(schluessel) OrElse o(schluessel) Is Nothing Then Return "?"
        Try
            Return o(schluessel).GetValue(Of String)()
        Catch ex As InvalidOperationException
            Return o(schluessel).ToString()
        End Try
    End Function

End Module
