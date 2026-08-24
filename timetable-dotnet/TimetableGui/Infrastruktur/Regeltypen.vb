' Die acht Regel-Masken (gui-ui-konzept.md 6.10) als DATEN statt als
' acht handgeschriebene Formulare.
'
' Warum: die Typen unterscheiden sich in Feldern und Geltungsbereichen,
' nicht in ihrer Mechanik. Acht Masken von Hand waeren acht Stellen, an
' denen Pflichtfeldpruefung, Prio-Vorgabe und Grund-Feld auseinander
' laufen koennen - und genau solche Unterschiede faellt niemandem auf,
' bis sie stoeren.
'
' Die Feldnamen sind das WIRE-FORMAT (arc42 8.7), nicht .NET-Namen: die
' Maske schreibt rohe JsonObject, die unveraendert durch
' Validation/Solver laufen. Jede Uebersetzungstabelle dazwischen waere
' eine zweite Wahrheit.
Imports System.Text.Json.Nodes
Imports TimetableCore

''' <summary>Was ein Feld einer Regel-Maske ist. `Art` bestimmt das
''' Steuerelement, `Quelle` bei Auswahlfeldern den Inhalt - "Referenzfelder
''' sind IMMER Auswahllisten aus dem Bestand, nie Freitext" (Abschnitt 6).</summary>
Public Enum FeldArt
    Text
    Zahl
    AuswahlKlasse
    AuswahlLehrkraft
    AuswahlFach
    AuswahlRaum
    AuswahlKlasseOderLehrkraftOderRaum
    AuswahlKlasseOderLehrkraft
    MehrfachRaum
    MehrfachTag
    Raster
    Prio
End Enum

Public NotInheritable Class Regelfeld
    Public Property Name As String = ""          ' Wire-Format-Schluessel
    Public Property Beschriftung As String = ""
    Public Property Art As FeldArt
    Public Property Pflicht As Boolean = True
    Public Property Hilfe As String = ""
End Class

Public NotInheritable Class Regeltyp
    Public Property Typ As String = ""           ' Wire-Format `type`
    Public Property Titel As String = ""
    Public Property Bemerkung As String = ""
    Public Property Felder As New List(Of Regelfeld)
    ''' <summary>Erzeugt aus EINER Maskeneingabe unter Umstaenden MEHRERE
    ''' Regeln - forbidden_slot eine je Slot, subject_period_window eine
    ''' je Klasse (6.10). Nothing heisst: eine Eingabe, eine Regel.</summary>
    Public Property Vervielfacht As String = Nothing
End Class

Public Module Regeltypen

    Private Function Feld(name As String, beschriftung As String, art As FeldArt,
                          Optional pflicht As Boolean = True, Optional hilfe As String = "") As Regelfeld
        Return New Regelfeld With {.Name = name, .Beschriftung = beschriftung,
                                   .Art = art, .Pflicht = pflicht, .Hilfe = hilfe}
    End Function

    Private ReadOnly Prio As Regelfeld = Feld("priority", "Priorität", FeldArt.Prio, False,
        "must = harte Regel, should = Kann-Regel. Leer laesst dem Kern seinen Default.")
    Private ReadOnly Grund As Regelfeld = Feld("reason", "Grund", FeldArt.Text, False,
        "Erscheint in Pruef- und Verletzungsmeldungen; beim Export auf neutralen Wortlaut achten.")

    ''' <summary>Die acht Typen, "genau die, die die Beispiele real
    ''' nutzen" (6.10). Die Haeufigkeiten in den Bemerkungen stammen aus
    ''' den beiden Beispielschulen und begruenden, warum es GENAU diese
    ''' acht sind.</summary>
    Public ReadOnly Property Alle As List(Of Regeltyp) = New List(Of Regeltyp) From {
        New Regeltyp With {
            .Typ = "forbidden_slot", .Titel = "Gesperrter Slot",
            .Bemerkung = "Haeufigster Typ der Grundschule (82/48). Eine Mehrfachauswahl im " &
                         "Raster erzeugt EINE REGEL JE SLOT - die Liste gruppiert sie nur.",
            .Vervielfacht = "slots",
            .Felder = New List(Of Regelfeld) From {
                Feld("scope", "Geltung", FeldArt.Text, True, "class, teacher oder room"),
                Feld("entity", "Betroffene(r)", FeldArt.AuswahlKlasseOderLehrkraftOderRaum),
                Feld("__raster", "Slots", FeldArt.Raster, True, "Klicken oder ziehen."),
                Prio, Grund}},
        New Regeltyp With {
            .Typ = "subject_period_window", .Titel = "Fach-Zeitfenster / Rhythmisierung",
            .Bemerkung = "Mit 224 Vorkommen der haeufigste Typ der GMS. Mehrfachauswahl von " &
                         "Klassen erzeugt je Klasse eine Regel. ACHTUNG: ein nicht gewaehlter " &
                         "Tag ist nicht ""egal"", sondern ganz ausgeschlossen.",
            .Vervielfacht = "classes",
            .Felder = New List(Of Regelfeld) From {
                Feld("class", "Klasse(n)", FeldArt.AuswahlKlasse),
                Feld("subject", "Fach", FeldArt.AuswahlFach),
                Feld("__raster", "Tage und Stunden", FeldArt.Raster, True,
                     "Die Auswahl muss ein Rechteck sein - das Fenster kennt nur ein von/bis."),
                Prio, Grund}},
        New Regeltyp With {
            .Typ = "occupied_window", .Titel = "Belegungsfenster",
            .Bemerkung = "Ersetzt die fruehere occupied_slot-Batterie (8/24).",
            .Felder = New List(Of Regelfeld) From {
                Feld("scope", "Geltung", FeldArt.Text, True, "class oder teacher"),
                Feld("entity", "Betroffene(r)", FeldArt.AuswahlKlasseOderLehrkraft),
                Feld("__raster", "Tage und Stunden", FeldArt.Raster, True,
                     "Die Auswahl muss ein Rechteck sein."),
                Prio, Grund}},
        New Regeltyp With {
            .Typ = "teacher_availability", .Titel = "Lehrkraft-Verfügbarkeit",
            .Bemerkung = "Verfuegbare Tage UND/ODER gesperrte Einzelstunden (3/0).",
            .Felder = New List(Of Regelfeld) From {
                Feld("teacher", "Lehrkraft", FeldArt.AuswahlLehrkraft),
                Feld("available_days", "Verfügbare Tage", FeldArt.MehrfachTag, False),
                Prio, Grund}},
        New Regeltyp With {
            .Typ = "required_slot", .Titel = "Pflicht-Slot",
            .Bemerkung = "Selten (1/0), z.B. die Chor-Gesamtprobe.",
            .Vervielfacht = "classes",
            .Felder = New List(Of Regelfeld) From {
                Feld("class", "Klasse(n)", FeldArt.AuswahlKlasse),
                Feld("subject", "Fach", FeldArt.AuswahlFach),
                Feld("day", "Tag", FeldArt.Text),
                Feld("period", "Stunde", FeldArt.Zahl),
                Prio, Grund}},
        New Regeltyp With {
            .Typ = "room_requirement", .Titel = "Raumbedarf",
            .Bemerkung = "KEIN Klassen-Feld - die Regel ist rein fachbezogen. Zwei " &
                         "Doku-Beispiele zeigen faelschlich ein class-Feld; die Maske bietet " &
                         "es bewusst nicht an (0/15).",
            .Felder = New List(Of Regelfeld) From {
                Feld("subject", "Fach", FeldArt.AuswahlFach),
                Feld("allowed_rooms", "Erlaubte Räume", FeldArt.MehrfachRaum),
                Prio, Grund}},
        New Regeltyp With {
            .Typ = "occupied_slot", .Titel = "Einzelbelegung",
            .Bemerkung = "Fuer punktuelle Faelle - normalerweise das Belegungsfenster bevorzugen.",
            .Felder = New List(Of Regelfeld) From {
                Feld("scope", "Geltung", FeldArt.Text, True, "class oder teacher"),
                Feld("entity", "Betroffene(r)", FeldArt.AuswahlKlasseOderLehrkraft),
                Feld("day", "Tag", FeldArt.Text),
                Feld("period", "Stunde", FeldArt.Zahl),
                Prio, Grund}},
        New Regeltyp With {
            .Typ = "consecutive_required", .Titel = "Ad-hoc-Block",
            .Bemerkung = "Normalerweise ueber Fach.block_length (6.4) - diese Maske ist fuer Ausnahmen.",
            .Felder = New List(Of Regelfeld) From {
                Feld("class", "Klasse", FeldArt.AuswahlKlasse),
                Feld("subject", "Fach", FeldArt.AuswahlFach),
                Feld("block_length", "Blocklänge", FeldArt.Zahl),
                Prio, Grund}}
    }

    Public Function Finde(typ As String) As Regeltyp
        Return Alle.FirstOrDefault(Function(t) t.Typ = typ)
    End Function

    ''' <summary>Regeln, die `BuildAssignmentConstraints` je Lauf erzeugt.
    ''' Sie erscheinen read-only mit Herkunfts-Badge; "Handpflege ist dort
    ''' ausdruecklich verboten und die GUI erzwingt das strukturell"
    ''' (6.10). Strukturell heisst: keine Bearbeitungsmoeglichkeit, nicht
    ''' ein Warnhinweis.</summary>
    Public ReadOnly Property Generierte As HashSet(Of String) =
        New HashSet(Of String)(StringComparer.Ordinal) From {
            "teacher_subject_assignment", "weekly_hours", "no_overlap", "parallel_group"}

    Public Function IstGeneriert(c As JsonObject) As Boolean
        Dim typ = JsonHelpers.GetString(c, "type")
        If typ Is Nothing Then Return False
        If Generierte.Contains(typ) Then Return True
        ' consecutive_required gibt es in BEIDEN Rollen: aus block_length
        ' erzeugt (read-only) und von Hand als Ausnahme. Unterscheidbar
        ' sind sie nur ueber die Herkunft, die der Bestand nicht mitfuehrt -
        ' hier gilt deshalb: was in den Handregeln steht, ist Handregel.
        Return False
    End Function

    ''' <summary>Einzeiler fuer die Listenansicht. Bewusst hier und nicht
    ''' in der Maske: die Liste zeigt Hand- und generierte Regeln
    ''' nebeneinander, und beide sollen gleich aussehen.</summary>
    Public Function Beschreibe(c As JsonObject) As String
        If c Is Nothing Then Return ""
        Dim typ = JsonHelpers.GetString(c, "type")
        Dim t = Finde(typ)
        Dim teile As New List(Of String)

        For Each schluessel In {"entity", "teacher", "class", "subject"}
            Dim w = JsonHelpers.GetString(c, schluessel)
            If w IsNot Nothing Then teile.Add(w)
        Next
        If c.ContainsKey("allowed_rooms") AndAlso c("allowed_rooms") IsNot Nothing Then
            teile.Add(String.Join("/", JsonHelpers.AsStringList(c("allowed_rooms"))))
        End If
        If c.ContainsKey("available_days") AndAlso c("available_days") IsNot Nothing Then
            teile.Add(String.Join(",", JsonHelpers.AsStringList(c("available_days"))))
        End If

        Dim tag = JsonHelpers.GetString(c, "day")
        If tag IsNot Nothing Then
            teile.Add(tag & If(c.ContainsKey("period") AndAlso c("period") IsNot Nothing,
                               " " & JsonHelpers.GetInt(c, "period").ToString() & ".", ""))
        ElseIf c.ContainsKey("from_period") AndAlso c("from_period") IsNot Nothing Then
            teile.Add($"{JsonHelpers.GetInt(c, "from_period")}.-{JsonHelpers.GetInt(c, "to_period")}. Std.")
        End If

        Dim prio = JsonHelpers.GetString(c, "priority")
        Dim kopf = If(t IsNot Nothing, t.Titel, typ)
        Return $"{kopf}: {String.Join(" · ", teile)}" & If(prio Is Nothing, "", $"  [{prio}]")
    End Function

End Module
