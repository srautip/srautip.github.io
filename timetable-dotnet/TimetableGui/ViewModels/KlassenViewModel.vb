' Klassenstufen und Klassen (gui-ui-konzept.md 6.3).
'
' Die eine Operation, die mehr ist als ein Formular: "Zug ergaenzen -
' legt die naechste Parallelklasse (c, d, ...) ueber alle gewaehlten
' Stufen an."
Imports TimetableCore
Imports TimetableProjekt

Public NotInheritable Class KlassenViewModel
    Inherits ListenViewModel(Of Klasse)

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

    Protected Overrides Function Quelle() As IList(Of Klasse)
        Return Bestand.Klassen
    End Function

    Protected Overrides Function Erzeuge() As Klasse
        Return New Klasse With {.Klassenstufe = Bestand.Klassenstufen.Select(Function(s) s.Nummer).FirstOrDefault()}
    End Function

    Protected Overrides Function Kopiere(vorlage As Klasse) As Klasse
        Return New Klasse With {
            .Name = vorlage.Name,
            .Klassenstufe = vorlage.Klassenstufe,
            .Schuelerzahl = vorlage.Schuelerzahl,
            .ErlaubtKlassenlehrerTandem = vorlage.ErlaubtKlassenlehrerTandem}
    End Function

    Protected Overrides Function NameVon(eintrag As Klasse) As String
        Return If(eintrag.Name, "")
    End Function

    Protected Overrides Sub SetzeName(eintrag As Klasse, name As String)
        eintrag.Name = name
    End Sub

    Protected Overrides Function BasisName() As String
        Return "Neue Klasse"
    End Function

    Public ReadOnly Property Stufen As List(Of Klassenstufe)
        Get
            Return Bestand.Klassenstufen.OrderBy(Function(s) s.Nummer).ToList()
        End Get
    End Property

    ' ---------------------------------------------------------------
    ' Zug ergaenzen
    ' ---------------------------------------------------------------

    ''' <summary>Der naechste freie Zugbuchstabe ueber ALLE Stufen
    ''' hinweg. Bewusst nicht je Stufe einzeln: haetten die Stufen
    ''' unterschiedlich viele Zuege, entstuenden bei "Zug ergaenzen"
    ''' Namen wie 1c und 2d nebeneinander - und die Zuordnung, welche
    ''' Klassen zusammengehoeren, waere hin.</summary>
    Public Function NaechsterZug() As Char
        Dim hoechster = "a"c
        For Each k In Bestand.Klassen
            Dim n = If(k.Name, "")
            If n.Length = 0 Then Continue For
            Dim letzter = Char.ToLowerInvariant(n(n.Length - 1))
            If letzter >= "a"c AndAlso letzter <= "z"c AndAlso letzter >= hoechster Then
                hoechster = ChrW(AscW(letzter) + 1)
            End If
        Next
        Return hoechster
    End Function

    ''' <summary>Legt in jeder genannten Stufe die naechste
    ''' Parallelklasse an. Liefert die angelegten Namen - der Dialog
    ''' zeigt sie, damit die Massenoperation nicht wortlos passiert.</summary>
    Public Function ZugErgaenzen(stufen As IEnumerable(Of Integer)) As List(Of String)
        Dim buchstabe = NaechsterZug()
        Dim angelegt As New List(Of String)
        For Each nummer In If(stufen, Array.Empty(Of Integer)()).Distinct().OrderBy(Function(x) x)
            Dim name = nummer.ToString(Globalization.CultureInfo.InvariantCulture) & buchstabe
            If Bestand.Klassen.Any(Function(k) String.Equals(k.Name, name, StringComparison.CurrentCultureIgnoreCase)) Then Continue For
            Bestand.Klassen.Add(New Klasse With {.Name = name, .Klassenstufe = nummer})
            angelegt.Add(name)
        Next
        If angelegt.Count > 0 Then
            Aktualisiere()
            MeldeAenderung()
        End If
        Return angelegt
    End Function

    Public Overrides Function Pruefe() As List(Of String)
        Dim fehler As New List(Of String)
        Dim namen As New HashSet(Of String)(StringComparer.CurrentCultureIgnoreCase)
        Dim stufenNummern = Bestand.Klassenstufen.Select(Function(s) s.Nummer).ToHashSet()

        For Each k In Quelle()
            If String.IsNullOrWhiteSpace(k.Name) Then
                fehler.Add("Eine Klasse ohne Namen kann von keiner Regel referenziert werden.")
            ElseIf Not namen.Add(k.Name) Then
                fehler.Add($"Der Klassenname „{k.Name}" & Chr(34) & " kommt mehrfach vor.")
            End If
            If Not stufenNummern.Contains(k.Klassenstufe) Then
                fehler.Add($"„{k.Name}" & Chr(34) & $": Klassenstufe {k.Klassenstufe} ist nicht angelegt.")
            End If
            If k.Schuelerzahl.HasValue AndAlso k.Schuelerzahl.Value < 0 Then
                fehler.Add($"„{k.Name}" & Chr(34) & ": negative Schuelerzahl.")
            End If
        Next

        For Each s In Bestand.Klassenstufen
            If Kennzahlen.KlassenInStufe(Bestand, s.Nummer) = 0 Then
                fehler.Add($"Stufe {s.Nummer} hat keine Klasse - ihre Fachkontingente erzeugen keinen Unterricht.")
            End If
        Next

        fehler.AddRange(StammdatenValidation.ValidateStammdaten(Bestand).
            Where(Function(x) x.IndexOf("Klasse", StringComparison.OrdinalIgnoreCase) >= 0))
        Return fehler
    End Function

    Protected Overrides Function LoeschFolgen(eintrag As Klasse) As AenderungsFolgen
        Return Bestandspflege.Verweise(_projekt, Stammart.Klasse, eintrag.Name)
    End Function

    Protected Overrides Sub Entferne(eintrag As Klasse)
        Bestandspflege.Loesche(_projekt, Stammart.Klasse, eintrag.Name)
    End Sub

    Public Overrides Function BenenneUm(alt As String, neu As String) As Integer
        Dim folgen = Bestandspflege.BenenneUm(_projekt, Stammart.Klasse, alt, neu)
        Aktualisiere()
        MeldeAenderung()
        Return folgen.Verweise.Count
    End Function

End Class
