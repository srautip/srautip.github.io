' Qualifikationsmatrix (gui-ui-konzept.md 6.7) und feste Zuordnungen
' (6.9). Beide sind Zweitsichten auf Daten, die anderswo gepflegt
' werden - deshalb stehen sie zusammen und nicht bei ihren Quellen.
'
' 6.7 woertlich: "dieselben Daten wie 6.6, aber fuer den Ueberblick 'hat
' jedes Fach genug Lehrkraefte?'; Spaltenfuss zeigt Bedarf vs.
' verfuegbare Deputate je Fach und faerbt Engpaesse rot (die
' StammdatenValidation-Luecke 'Fach ohne qualifizierte Lehrkraft' wird
' hier praeventiv sichtbar)."
Imports TimetableCore
Imports TimetableProjekt

''' <summary>Die drei Zustaende einer Matrixzelle (6.7). Bewusst KEIN
''' Boolean: "fachfremd" ist ein eigener Zustand, kein Sonderfall von
''' "qualifiziert" - er wird im Lehrereinsatz anders gewichtet.</summary>
Public Enum Qualifikation
    Nein
    Fachfremd
    Qualifiziert
End Enum

Public NotInheritable Class QualifikationsmatrixViewModel
    Inherits Beobachtbar

    Private ReadOnly _projekt As Projekt

    Public Sub New(projekt As Projekt)
        _projekt = projekt
    End Sub

    Public Event Geaendert As EventHandler

    Private ReadOnly Property Bestand As Stammdatenbestand
        Get
            Return _projekt.Bestand
        End Get
    End Property

    Public ReadOnly Property Faecher As List(Of Fach)
        Get
            Return Bestand.Faecher.OrderBy(Function(f) f.Name, StringComparer.CurrentCultureIgnoreCase).ToList()
        End Get
    End Property

    Public ReadOnly Property Lehrkraefte As List(Of Lehrer)
        Get
            Return Bestand.Lehrkraefte.OrderBy(Function(l) l.Name, StringComparer.CurrentCultureIgnoreCase).ToList()
        End Get
    End Property

    Public Function Zustand(lehrerName As String, fachName As String) As Qualifikation
        Dim z = Bestand.FachLehrerZuordnungen.
            FirstOrDefault(Function(x) x.LehrerName = lehrerName AndAlso x.FachName = fachName)
        If z Is Nothing Then Return Qualifikation.Nein
        Return If(z.Fachfremd, Qualifikation.Fachfremd, Qualifikation.Qualifiziert)
    End Function

    ''' <summary>Weiterschalten durch die drei Zustaende - ein Klick in
    ''' die Zelle. Reihenfolge nein -> qualifiziert -> fachfremd -> nein:
    ''' der haeufige Fall liegt einen Klick entfernt, der seltene zwei.</summary>
    Public Sub Weiterschalten(lehrerName As String, fachName As String)
        Select Case Zustand(lehrerName, fachName)
            Case Qualifikation.Nein : Setze(lehrerName, fachName, Qualifikation.Qualifiziert)
            Case Qualifikation.Qualifiziert : Setze(lehrerName, fachName, Qualifikation.Fachfremd)
            Case Else : Setze(lehrerName, fachName, Qualifikation.Nein)
        End Select
    End Sub

    Public Sub Setze(lehrerName As String, fachName As String, wert As Qualifikation)
        Dim vorhanden = Bestand.FachLehrerZuordnungen.
            FirstOrDefault(Function(x) x.LehrerName = lehrerName AndAlso x.FachName = fachName)

        If wert = Qualifikation.Nein Then
            If vorhanden IsNot Nothing Then Bestand.FachLehrerZuordnungen.Remove(vorhanden)
        ElseIf vorhanden Is Nothing Then
            Bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {
                .LehrerName = lehrerName, .FachName = fachName,
                .Fachfremd = (wert = Qualifikation.Fachfremd)})
        Else
            vorhanden.Fachfremd = (wert = Qualifikation.Fachfremd)
        End If

        RaiseEvent Geaendert(Me, EventArgs.Empty)
        Melde(NameOf(Spaltenfuss))
    End Sub

    ''' <summary>Der Spaltenfuss aus 6.7: Bedarf gegen verfuegbares
    ''' Deputat je Fach. `Engpass` ist die Stelle, die 6.7 rot faerbt -
    ''' und zugleich die Luecke, die StammdatenValidation offenlaesst.
    '''
    ''' Zum Deputat: es ist eine OBERGRENZE. Dieselbe Lehrkraft zaehlt
    ''' bei jedem ihrer Faecher voll mit, kann ihre Stunden aber nur
    ''' einmal geben. Zu wenig beweist einen Engpass; genug beweist
    ''' nichts.</summary>
    Public ReadOnly Property Spaltenfuss As List(Of (Fach As String, Bedarf As Integer, Deputat As Double, Engpass As Boolean))
        Get
            Return Faecher.Select(Function(f)
                                      Dim bedarf = Kennzahlen.BedarfJeFach(Bestand, f.Name)
                                      Dim deputat = Kennzahlen.DeputatFuerFach(Bestand, f.Name)
                                      Return (f.Name, bedarf, deputat, bedarf > 0 AndAlso deputat < bedarf)
                                  End Function).ToList()
        End Get
    End Property

End Class


''' <summary>Feste Zuordnungen (6.9): Lehrkraft x Klasse x Fach.
''' "die Auswahllisten filtern auf qualifizierte Kombinationen" - eine
''' feste Zuordnung auf ein Fach, das die Lehrkraft nicht unterrichten
''' darf, waere sofort ein Widerspruch.</summary>
Public NotInheritable Class FesteZuordnungenViewModel
    Inherits ListenViewModel(Of FesteZuordnung)

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

    Protected Overrides Function Quelle() As IList(Of FesteZuordnung)
        Return Bestand.FesteZuordnungen
    End Function

    Protected Overrides Function Erzeuge() As FesteZuordnung
        Return New FesteZuordnung()
    End Function

    Protected Overrides Function Kopiere(vorlage As FesteZuordnung) As FesteZuordnung
        Return New FesteZuordnung With {
            .LehrerName = vorlage.LehrerName, .KlasseName = vorlage.KlasseName, .FachName = vorlage.FachName}
    End Function

    ''' <summary>Eine feste Zuordnung hat keinen eigenen Namen - sie IST
    ''' ihr Tripel. Die Basis braucht trotzdem einen Anzeigenamen fuer
    ''' Liste, Filter und Loeschdialog.</summary>
    Protected Overrides Function NameVon(eintrag As FesteZuordnung) As String
        Return $"{eintrag.LehrerName} - {eintrag.KlasseName} - {eintrag.FachName}"
    End Function

    Protected Overrides Sub SetzeName(eintrag As FesteZuordnung, name As String)
        ' Der Name ist abgeleitet; Neu/Duplizieren setzen die Felder.
    End Sub

    ''' <summary>Die Faecher, die diese Lehrkraft ueberhaupt unterrichten
    ''' darf - die gefilterte Auswahlliste aus 6.9.</summary>
    Public Function MoeglicheFaecher(lehrerName As String) As List(Of String)
        If String.IsNullOrWhiteSpace(lehrerName) Then Return Bestand.Faecher.Select(Function(f) f.Name).ToList()
        Return Bestand.FachLehrerZuordnungen.
            Where(Function(z) z.LehrerName = lehrerName).
            Select(Function(z) z.FachName).
            Distinct().OrderBy(Function(x) x, StringComparer.CurrentCultureIgnoreCase).ToList()
    End Function

    ''' <summary>Klassen UND aktive Gruppen - "gemeinsamer Namensraum"
    ''' (6.9). Eine feste Zuordnung kann auf eine Fachgruppe zeigen.</summary>
    Public Function MoeglicheKlassen() As List(Of String)
        Return Bestand.Klassen.Select(Function(k) k.Name).
            Concat(Bestand.Gruppen.Select(Function(g) g.Name)).
            Distinct().OrderBy(Function(x) x, StringComparer.CurrentCultureIgnoreCase).ToList()
    End Function

    Public Overrides Function Pruefe() As List(Of String)
        Dim fehler As New List(Of String)
        Dim lehrer = Bestand.Lehrkraefte.Select(Function(l) l.Name).ToHashSet(StringComparer.Ordinal)
        Dim ziele = MoeglicheKlassen().ToHashSet(StringComparer.Ordinal)
        Dim faecher = Bestand.Faecher.Select(Function(f) f.Name).ToHashSet(StringComparer.Ordinal)

        For Each z In Quelle()
            Dim wer = NameVon(z)
            If Not lehrer.Contains(If(z.LehrerName, "")) Then
                fehler.Add($"{wer}: Lehrkraft „{z.LehrerName}" & Chr(34) & " gibt es nicht.")
            End If
            If Not ziele.Contains(If(z.KlasseName, "")) Then
                fehler.Add($"{wer}: „{z.KlasseName}" & Chr(34) & " ist weder Klasse noch Gruppe.")
            End If
            If Not faecher.Contains(If(z.FachName, "")) Then
                fehler.Add($"{wer}: Fach „{z.FachName}" & Chr(34) & " gibt es nicht.")
            ElseIf lehrer.Contains(If(z.LehrerName, "")) AndAlso
                   Not MoeglicheFaecher(z.LehrerName).Contains(z.FachName) Then
                ' Der eigentliche Zweck der Maske: dieser Widerspruch
                ' faellt sonst erst dem Solver auf.
                fehler.Add($"{wer}: „{z.LehrerName}" & Chr(34) & $" ist fuer „{z.FachName}" & Chr(34) & " nicht qualifiziert.")
            End If
        Next
        Return fehler
    End Function

End Class
