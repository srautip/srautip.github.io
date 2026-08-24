' Raeume (gui-ui-konzept.md 6.5). Der Abschnitt nennt die Maske
' "bewusst minimal - Raeume sind nur fuer room_requirement-Regeln
' noetig (6.10)". Genau deshalb steht sie hier zuerst: sie beweist das
' Grundmuster, ohne es hinter Fachlogik zu verstecken.
Imports TimetableCore
Imports TimetableProjekt

Public NotInheritable Class RaeumeViewModel
    Inherits ListenViewModel(Of Raum)

    Private ReadOnly _projekt As Projekt

    Public Sub New(projekt As Projekt, dialoge As IDialoge)
        MyBase.New(dialoge)
        _projekt = projekt
        Aktualisiere()
    End Sub

    Protected Overrides Function Quelle() As IList(Of Raum)
        Return _projekt.Bestand.Raeume
    End Function

    Protected Overrides Function Erzeuge() As Raum
        Return New Raum()
    End Function

    ''' <summary>Duplizieren kopiert alles ausser dem Namen - den setzt
    ''' die Basis auf "… (2)". Bei zwei Feldern ist das trivial; die
    ''' Ueberschreibung existiert, damit spaetere Felder nicht vergessen
    ''' werden koennen.</summary>
    Protected Overrides Function Kopiere(vorlage As Raum) As Raum
        Return New Raum With {.Name = vorlage.Name, .Typ = vorlage.Typ}
    End Function

    Protected Overrides Function NameVon(eintrag As Raum) As String
        Return If(eintrag.Name, "")
    End Function

    Protected Overrides Sub SetzeName(eintrag As Raum, name As String)
        eintrag.Name = name
    End Sub

    Protected Overrides Function BasisName() As String
        Return "Neuer Raum"
    End Function

    ''' <summary>Die Pruefung des Kerns, nicht eine eigene. Gefiltert auf
    ''' das, was diese Maske verantwortet - sonst meldete der Raum-Dialog
    ''' fehlende Lehrkraefte.</summary>
    Public Overrides Function Pruefe() As List(Of String)
        Dim fehler As New List(Of String)

        Dim namen As New HashSet(Of String)(StringComparer.CurrentCultureIgnoreCase)
        For Each r In Quelle()
            If String.IsNullOrWhiteSpace(r.Name) Then
                fehler.Add("Ein Raum ohne Namen kann von keiner Regel referenziert werden.")
            ElseIf Not namen.Add(r.Name) Then
                ' Namen sind Schluessel (arc42 8.15) - zwei gleichnamige
                ' Raeume waeren im Wire-Format nicht unterscheidbar.
                fehler.Add($"Der Raumname „{r.Name}" & Chr(34) & " kommt mehrfach vor.")
            End If
        Next

        For Each f In StammdatenValidation.ValidateStammdaten(_projekt.Bestand).
                          Where(Function(x) x.IndexOf("Raum", StringComparison.OrdinalIgnoreCase) >= 0)
            fehler.Add(f)
        Next
        Return fehler
    End Function

    ''' <summary>Ein Raum kann von room_requirement- und
    ''' forbidden_slot-Regeln referenziert werden. Die Folgen kommen aus
    ''' Bestandspflege - dieselbe Quelle, die auch loescht.</summary>
    Protected Overrides Function LoeschFolgen(eintrag As Raum) As AenderungsFolgen
        Return Bestandspflege.Verweise(_projekt, Stammart.Raum, eintrag.Name)
    End Function

    Protected Overrides Sub Entferne(eintrag As Raum)
        Bestandspflege.Loesche(_projekt, Stammart.Raum, eintrag.Name)
    End Sub

    Public Overrides Function BenenneUm(alt As String, neu As String) As Integer
        Dim folgen = Bestandspflege.BenenneUm(_projekt, Stammart.Raum, alt, neu)
        Aktualisiere()
        MeldeAenderung()
        Return folgen.Verweise.Count
    End Function

    ''' <summary>Vorschau fuer die Umbenennung: „12 Verweise werden
    ''' angepasst" (Konzept 7). Der Nutzer soll die Tragweite VOR dem
    ''' Bestaetigen sehen, nicht danach.</summary>
    Public Function UmbenennenVorschau(alt As String) As Integer
        If String.IsNullOrWhiteSpace(alt) Then Return 0
        Return Bestandspflege.Verweise(_projekt, Stammart.Raum, alt).Verweise.Count
    End Function

End Class
