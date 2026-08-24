' Faecher (gui-ui-konzept.md 6.4). Kopfsatz plus Untertabelle
' "Wochenstunden je Stufe".
'
' Zwei Dinge, die der Abschnitt ausdruecklich verlangt:
'   - "Eine Stufe ohne Zeile heisst 'wird dort nicht unterrichtet' - der
'      Dialog zeigt das explizit als Badge statt es stumm zu lassen."
'   - "Fusszeile: Summen-Kontrolle Wochenstunden je Stufe (Soll gegen
'      periods_per_day x Tage)."
Imports TimetableCore
Imports TimetableProjekt

Public NotInheritable Class FaecherViewModel
    Inherits ListenViewModel(Of Fach)

    Private ReadOnly _projekt As Projekt

    Public Sub New(projekt As Projekt, dialoge As IDialoge)
        MyBase.New(dialoge)
        _projekt = projekt
        Aktualisiere()
    End Sub

    Private ReadOnly Property Bestand As Stammdatenbestand
        Get
            Return _projekt.Bestand
        End Get
    End Property

    Protected Overrides Function Quelle() As IList(Of Fach)
        Return Bestand.Faecher
    End Function

    Protected Overrides Function Erzeuge() As Fach
        Return New Fach()
    End Function

    Protected Overrides Function Kopiere(vorlage As Fach) As Fach
        Return New Fach With {
            .Name = vorlage.Name,
            .BlockLength = vorlage.BlockLength,
            .Unbeliebt = vorlage.Unbeliebt,
            .Klassenstufen = vorlage.Klassenstufen.
                Select(Function(fk) New FachKlassenstufe With {
                    .Klassenstufe = fk.Klassenstufe,
                    .WochenstundenSoll = fk.WochenstundenSoll,
                    .MaxProTag = fk.MaxProTag}).ToList()}
    End Function

    Protected Overrides Function NameVon(eintrag As Fach) As String
        Return If(eintrag.Name, "")
    End Function

    Protected Overrides Sub SetzeName(eintrag As Fach, name As String)
        eintrag.Name = name
    End Sub

    Protected Overrides Function BasisName() As String
        Return "Neues Fach"
    End Function

    ' ---------------------------------------------------------------
    ' Untertabelle: Wochenstunden je Stufe
    ' ---------------------------------------------------------------

    ''' <summary>Eine Zeile je Klassenstufe der Schule - AUCH fuer
    ''' Stufen ohne Kontingent. Genau das verlangt 6.4: die fehlende
    ''' Zeile hat eine Bedeutung ("wird dort nicht unterrichtet") und
    ''' soll sichtbar sein, nicht stumm.</summary>
    Public Function StufenZeilen(f As Fach) As List(Of (Stufe As Integer, Soll As Integer?, MaxProTag As Integer?, Hinweis As String))
        Dim zeilen As New List(Of (Stufe As Integer, Soll As Integer?, MaxProTag As Integer?, Hinweis As String))
        If f Is Nothing Then Return zeilen
        For Each s In Bestand.Klassenstufen.OrderBy(Function(x) x.Nummer)
            Dim fk = f.Klassenstufen.FirstOrDefault(Function(x) x.Klassenstufe = s.Nummer)
            zeilen.Add((s.Nummer,
                        If(fk Is Nothing, CType(Nothing, Integer?), fk.WochenstundenSoll),
                        If(fk Is Nothing, CType(Nothing, Integer?), fk.MaxProTag),
                        If(fk Is Nothing, "wird dort nicht unterrichtet", "")))
        Next
        Return zeilen
    End Function

    ''' <summary>Setzt das Kontingent einer Stufe. `Nothing` entfernt die
    ''' Zeile - und damit ist das Fach dort ausdruecklich nicht
    ''' vorgesehen. Eine Zeile mit 0 waere etwas anderes: "vorgesehen,
    ''' aber null Stunden", und das kennt das Modell nicht.</summary>
    Public Sub SetzeStufe(f As Fach, stufe As Integer, soll As Integer?, maxProTag As Integer?)
        If f Is Nothing Then Return
        Dim fk = f.Klassenstufen.FirstOrDefault(Function(x) x.Klassenstufe = stufe)
        If Not soll.HasValue Then
            If fk IsNot Nothing Then f.Klassenstufen.Remove(fk)
        ElseIf fk Is Nothing Then
            f.Klassenstufen.Add(New FachKlassenstufe With {
                .Klassenstufe = stufe, .WochenstundenSoll = soll.Value, .MaxProTag = maxProTag})
        Else
            fk.WochenstundenSoll = soll.Value
            fk.MaxProTag = maxProTag
        End If
        f.Klassenstufen.Sort(Function(a, b) a.Klassenstufe.CompareTo(b.Klassenstufe))
        MeldeAenderung()
        Melde(NameOf(SummenZeile))
    End Sub

    ''' <summary>Die Fusszeile aus 6.4: Soll je Stufe gegen das, was in
    ''' eine Klassenwoche passt.</summary>
    Public ReadOnly Property SummenZeile As String
        Get
            Dim kap = Kennzahlen.KapazitaetJeKlasse(Bestand)
            Dim teile = Bestand.Klassenstufen.OrderBy(Function(s) s.Nummer).
                Select(Function(s)
                           Dim soll = Kennzahlen.SollJeKlasse(Bestand, s.Nummer)
                           Return $"St.{s.Nummer}: {soll}/{kap}" & If(soll > kap, " !", "")
                       End Function)
            Return String.Join("   ", teile)
        End Get
    End Property

    Public Overrides Function Pruefe() As List(Of String)
        Dim fehler As New List(Of String)
        Dim kap = Kennzahlen.KapazitaetJeKlasse(Bestand)

        Dim namen As New HashSet(Of String)(StringComparer.CurrentCultureIgnoreCase)
        For Each f In Quelle()
            If String.IsNullOrWhiteSpace(f.Name) Then
                fehler.Add("Ein Fach ohne Namen kann keiner Lehrkraft zugeordnet werden.")
            ElseIf Not namen.Add(f.Name) Then
                fehler.Add($"Der Fachname „{f.Name}" & Chr(34) & " kommt mehrfach vor.")
            End If
            For Each fk In f.Klassenstufen
                If fk.WochenstundenSoll < 0 Then
                    fehler.Add($"„{f.Name}" & Chr(34) & $", Stufe {fk.Klassenstufe}: negative Wochenstunden.")
                End If
                If fk.MaxProTag.HasValue AndAlso fk.MaxProTag.Value < 1 Then
                    fehler.Add($"„{f.Name}" & Chr(34) & $", Stufe {fk.Klassenstufe}: max/Tag muss mindestens 1 sein.")
                End If
            Next
        Next

        For Each s In Bestand.Klassenstufen
            Dim soll = Kennzahlen.SollJeKlasse(Bestand, s.Nummer)
            If soll > kap Then
                fehler.Add($"Stufe {s.Nummer}: {soll} Wochenstunden passen nicht in {kap} verfuegbare " &
                           $"({Bestand.Tage.Count} Tage x {Bestand.PeriodsPerDay}).")
            End If
        Next

        fehler.AddRange(StammdatenValidation.ValidateStammdaten(Bestand).
            Where(Function(x) x.IndexOf("Fach", StringComparison.OrdinalIgnoreCase) >= 0))
        Return fehler
    End Function

    Protected Overrides Function LoeschFolgen(eintrag As Fach) As AenderungsFolgen
        Return Bestandspflege.Verweise(_projekt, Stammart.Fach, eintrag.Name)
    End Function

    Protected Overrides Sub Entferne(eintrag As Fach)
        Bestandspflege.Loesche(_projekt, Stammart.Fach, eintrag.Name)
    End Sub

    Public Overrides Function BenenneUm(alt As String, neu As String) As Integer
        Dim folgen = Bestandspflege.BenenneUm(_projekt, Stammart.Fach, alt, neu)
        Aktualisiere()
        MeldeAenderung()
        Return folgen.Verweise.Count
    End Function

End Class
