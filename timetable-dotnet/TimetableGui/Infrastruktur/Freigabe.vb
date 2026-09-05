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
    ''' <summary>Klassenbildung: die freigegebene Zuordnung Kind -> Klasse
    ''' der ARBEITSSICHT (Variante plus Pins) - sie gehoert in den
    ''' Nachweis, denn die Pins im Projekt sind veraenderlich, der Stand
    ''' allein kennt sie nicht. Nothing beim Stundenplan.</summary>
    Public Property Zuordnung As JsonObject
    ''' <summary>Wie viele Kinder die Pins gegenueber der Variante
    ''' verschieben.</summary>
    Public Property Verschoben As Integer

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

    ''' <summary>Welche ENTSCHEIDUNG dieser Stand traegt. Klassenbildung
    ''' und Stundenplan sind zwei verschiedene Entscheidungen mit je
    ''' eigenem Nachweis - welches Kind in welche Klasse kommt, ist etwas
    ''' anderes als wann welche Stunde liegt. Sie werden deshalb getrennt
    ''' freigegeben.</summary>
    Public Function ArtVon(stand As ProjektStand) As String
        If stand Is Nothing Then Return "unbekannt"
        If stand.Stundenplan IsNot Nothing Then Return "Stundenplan"
        If stand.Klassenbildung IsNot Nothing Then Return "Klassenbildung"
        Return "unbekannt"
    End Function

    ''' <summary>Baut die Vorlage aus einem Stand. Massgeblich ist die
    ''' MARKIERTE Loesung (siehe `lauf.arbeitsstand`); ohne Markierung
    ''' gilt die erste - dieselbe, die das Dashboard beim Oeffnen
    ''' zeigt.
    '''
    ''' Mit `projekt` wird bei der Klassenbildung die ARBEITSSICHT
    ''' bewertet: die gewaehlte Basis-Variante plus die Pins des Boards
    ''' (gui-state). Ohne das sagte der Dialog "keine Abweichungen",
    ''' obwohl eine Verschiebung eine Buendelung zerrissen hatte - der
    ''' Stand kennt nur das Solver-Ergebnis (live gemeldet 06.09.2026).</summary>
    Public Function Vorlage(stand As ProjektStand, Optional projekt As Projekt = Nothing) As Freigabevorlage
        If stand Is Nothing Then Return Nothing
        Dim v As New Freigabevorlage With {.StandId = stand.Id, .Label = stand.Label}

        v.Art = ArtVon(stand)
        If v.Art = "Stundenplan" Then
            FuelleStundenplan(v, stand)
        ElseIf v.Art = "Klassenbildung" Then
            FuelleKlassenbildung(v, stand, projekt)
        Else
            ' Ein Stand ohne Ergebnis ist nichts, was man freigeben
            ' koennte - und das ehrlich zu sagen ist besser, als eine
            ' leere Liste als "keine Abweichungen" auszugeben.
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

    Private Sub FuelleKlassenbildung(v As Freigabevorlage, stand As ProjektStand, projekt As Projekt)
        Dim varianten = TryCast(stand.Klassenbildung("varianten"), JsonArray)
        If varianten Is Nothing OrElse varianten.Count = 0 Then
            v.Kennzahlen = "Der Stand enthält keine Variante."
            Return
        End If

        ' Die Basis ist die im Board gewaehlte Variante (gui-state
        ' `basis`, 0-basiert); ohne Board-Zustand die markierte Loesung.
        Dim index = Math.Min(GewaehlteLoesung(stand), varianten.Count) - 1
        Dim gewaehlteBasis = Basis(projekt)
        If gewaehlteBasis.HasValue AndAlso gewaehlteBasis.Value >= 0 AndAlso gewaehlteBasis.Value < varianten.Count Then
            index = gewaehlteBasis.Value
        End If
        Dim variante = TryCast(varianten(index), JsonObject)
        If variante Is Nothing Then Return

        ' Arbeitssicht: Zuordnung der Variante, ueberlagert mit den Pins -
        ' dieselbe Rechnung wie im Board (aktuelleSicht), und dieselbe
        ' Bewertung (KlassenbildungQuality.Bewerte, das VB-Original des
        ' JS-Duplikats). Nur wenn Eingabe und Stand zusammenpassen; ein
        ' aelterer Stand mit anderen Kindern faellt auf das Solver-Ergebnis
        ' zurueck.
        Dim eingabe = projekt?.Klassenbildung
        Dim zuordnung = ZuordnungVon(variante)
        If eingabe IsNot Nothing AndAlso zuordnung.Count > 0 AndAlso Passt(eingabe, zuordnung) Then
            v.Verschoben = PinsAnwenden(zuordnung, projekt.GuiState, eingabe.Klassen.Anzahl)
            Dim bewertung = KlassenbildungQuality.Bewerte(eingabe, zuordnung)
            For Each x In bewertung.Verletzungen
                If x.Mass <= 0 Then Continue For
                v.Abweichungen.Add($"{x.RegelTyp} {x.RegelId} ({PrioWort(x.Prio)}) nicht erfuellt, Mass {x.Mass}.")
            Next
            Dim zo As New JsonObject()
            For Each kvp In zuordnung.OrderBy(Function(k) k.Key, StringComparer.Ordinal)
                zo(kvp.Key) = kvp.Value
            Next
            v.Zuordnung = zo
            v.Kennzahlen = $"Variante {index + 1} von {varianten.Count}" &
                           If(v.Verschoben > 0, $", {v.Verschoben} Kind(er) per Pin verschoben - Arbeitssicht bewertet.", ", wie gerechnet.")
            Return
        End If

        ' Der Export fuehrt JEDE Regel mit ihrem Mass - auch die
        ' erfuellten mit mass 0 (Verifier-Prinzip: der Bewertungslauf
        ' reproduziert die Solver-Verletzungen vollstaendig). Abweichung
        ' ist nur, was ein Mass > 0 hat; sonst standen 50 erfuellte
        ' Regeln als "nicht erfuellt" im Nachweis (live gemeldet 05.09.2026).
        Dim verletzungen = TryCast(variante("verletzungen"), JsonArray)
        If verletzungen IsNot Nothing Then
            For Each eintrag In verletzungen
                Dim o = TryCast(eintrag, JsonObject)
                If o Is Nothing Then Continue For
                Dim mass = Ganzzahl(o, "mass")
                If mass <= 0 Then Continue For
                Dim id = Zeichenkette(o, "regel_id")
                Dim typ = Zeichenkette(o, "regel_typ")
                ' Die Regel-ID ist ein Pseudonym (G_kita_sonnenblume_1),
                ' kein Personenbezug - sie darf hier stehen.
                v.Abweichungen.Add($"{typ} {id} ({PrioWort(Ganzzahl(o, "prio"))}) nicht erfuellt, Mass {mass}.")
            Next
        End If
        v.Kennzahlen = $"Variante {index + 1} von {varianten.Count}."
    End Sub

    Private Function Basis(projekt As Projekt) As Integer?
        Dim st = projekt?.GuiState
        If st Is Nothing OrElse Not st.ContainsKey("basis") OrElse st("basis") Is Nothing Then Return Nothing
        Try
            Return st("basis").GetValue(Of Integer)()
        Catch ex As InvalidOperationException
            Return Nothing
        Catch ex As FormatException
            Return Nothing
        End Try
    End Function

    Private Function ZuordnungVon(variante As JsonObject) As Dictionary(Of String, Integer)
        Dim z As New Dictionary(Of String, Integer)
        Dim o = TryCast(variante("zuordnung"), JsonObject)
        If o Is Nothing Then Return z
        For Each kvp In o
            Try
                z(kvp.Key) = kvp.Value.GetValue(Of Integer)()
            Catch ex As InvalidOperationException
            Catch ex As FormatException
            End Try
        Next
        Return z
    End Function

    ''' <summary>Bewerten kann man nur, wenn jedes Kind der Eingabe in der
    ''' Zuordnung steht - sonst stammt der Stand aus einer anderen Liste.</summary>
    Private Function Passt(eingabe As KlassenbildungInput, zuordnung As Dictionary(Of String, Integer)) As Boolean
        If eingabe.Schueler.Count = 0 OrElse eingabe.Klassen Is Nothing OrElse eingabe.Klassen.Anzahl < 1 Then Return False
        Return eingabe.Schueler.All(Function(s) zuordnung.ContainsKey(s.Id))
    End Function

    ''' <summary>Pins des Boards (gui-state `pins`: Kind -> Klasse) auf die
    ''' Zuordnung legen. Liefert die Zahl der Kinder, die dadurch in einer
    ''' anderen Klasse stehen als in der Variante.</summary>
    Private Function PinsAnwenden(zuordnung As Dictionary(Of String, Integer), guiState As JsonObject, anzahlKlassen As Integer) As Integer
        Dim pins = TryCast(guiState?("pins"), JsonObject)
        If pins Is Nothing Then Return 0
        Dim verschoben = 0
        For Each kvp In pins
            If Not zuordnung.ContainsKey(kvp.Key) Then Continue For
            Dim klasse As Integer
            Try
                klasse = kvp.Value.GetValue(Of Integer)()
            Catch ex As InvalidOperationException
                Continue For
            Catch ex As FormatException
                Continue For
            End Try
            If klasse < 1 OrElse klasse > anzahlKlassen Then Continue For
            If zuordnung(kvp.Key) <> klasse Then
                zuordnung(kvp.Key) = klasse
                verschoben += 1
            End If
        Next
        Return verschoben
    End Function

    ''' <summary>Die Stufen heissen, sie werden nicht nummeriert - im
    ''' YAML ist 3 die hoechste Prio, gelesen wird "kritisch" als Nummer 1
    ''' (dieselbe Entscheidung wie im Viewer, U6).</summary>
    Private Function PrioWort(prio As Integer) As String
        Select Case prio
            Case 3 : Return "kritisch"
            Case 2 : Return "wichtig"
            Case 1 : Return "wenn moeglich"
            Case Else : Return $"Prio {prio}"
        End Select
    End Function

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
