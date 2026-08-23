' Lehrkraefte (gui-ui-konzept.md 6.6). Alle neun Felder plus die
' Qualifikationen als Fach-Checkliste - die pflegen
' `fach_lehrer_zuordnungen` und sind bewusst kein eigener Dialog.
'
' Die Plausibilitaet im Kopf ist der Grund, warum diese Maske mehr ist
' als ein Formular: "die 'Kanarienvogel'-Erfahrung des GMS-Beispiels
' (ueberdimensionierte Pools erzeugen verteilten Deputat-Leerlauf) wird
' so VOR dem Lauf sichtbar, nicht erst im Ergebnis."
Imports TimetableCore
Imports TimetableProjekt

Public NotInheritable Class LehrkraefteViewModel
    Inherits ListenViewModel(Of Lehrer)

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

    Protected Overrides Function Quelle() As IList(Of Lehrer)
        Return Bestand.Lehrkraefte
    End Function

    Protected Overrides Function Erzeuge() As Lehrer
        ' Ein neuer Eintrag mit Deputat 0 waere sofort ein Pruefbefund.
        ' Der Default folgt dem, was der Scaffold anlegt.
        Return New Lehrer With {.DeputatSollstunden = 25}
    End Function

    Protected Overrides Function Kopiere(vorlage As Lehrer) As Lehrer
        Return New Lehrer With {
            .Name = vorlage.Name,
            .DeputatSollstunden = vorlage.DeputatSollstunden,
            .Anrechnungsstunden = vorlage.Anrechnungsstunden,
            .SpringerReserveStunden = vorlage.SpringerReserveStunden,
            .VerfuegbareTage = If(vorlage.VerfuegbareTage Is Nothing, Nothing, vorlage.VerfuegbareTage.ToList()),
            .BevorzugteKlassenstufen = vorlage.BevorzugteKlassenstufen.ToList(),
            .KlassenlehrerFaehig = vorlage.KlassenlehrerFaehig,
            .MaxKlassen = vorlage.MaxKlassen,
            .MaxFaecher = vorlage.MaxFaecher}
    End Function

    Protected Overrides Function NameVon(eintrag As Lehrer) As String
        Return If(eintrag.Name, "")
    End Function

    Protected Overrides Sub SetzeName(eintrag As Lehrer, name As String)
        eintrag.Name = name
    End Sub

    Protected Overrides Function BasisName() As String
        Return "Neue Lehrkraft"
    End Function

    ' ---------------------------------------------------------------
    ' Qualifikationen (Teil DIESER Maske, nicht ein eigener Dialog)
    ' ---------------------------------------------------------------

    Public Function IstQualifiziert(lehrer As Lehrer, fachName As String) As Boolean
        If lehrer Is Nothing Then Return False
        Return Bestand.FachLehrerZuordnungen.
            Any(Function(z) z.LehrerName = lehrer.Name AndAlso z.FachName = fachName)
    End Function

    Public Function IstFachfremd(lehrer As Lehrer, fachName As String) As Boolean
        If lehrer Is Nothing Then Return False
        Dim z = Bestand.FachLehrerZuordnungen.
            FirstOrDefault(Function(x) x.LehrerName = lehrer.Name AndAlso x.FachName = fachName)
        Return z IsNot Nothing AndAlso z.Fachfremd
    End Function

    ''' <summary>Setzt die Qualifikation. `fachfremd` ist eine
    ''' Eigenschaft der ZUORDNUNG, nicht der Lehrkraft - dieselbe Person
    ''' kann ihr Fach unterrichten und ein zweites fachfremd.</summary>
    Public Sub SetzeQualifikation(lehrer As Lehrer, fachName As String,
                                  qualifiziert As Boolean, fachfremd As Boolean)
        If lehrer Is Nothing OrElse String.IsNullOrWhiteSpace(fachName) Then Return
        Dim vorhanden = Bestand.FachLehrerZuordnungen.
            FirstOrDefault(Function(x) x.LehrerName = lehrer.Name AndAlso x.FachName = fachName)

        If Not qualifiziert Then
            If vorhanden IsNot Nothing Then Bestand.FachLehrerZuordnungen.Remove(vorhanden)
        ElseIf vorhanden Is Nothing Then
            Bestand.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {
                .LehrerName = lehrer.Name, .FachName = fachName, .Fachfremd = fachfremd})
        Else
            vorhanden.Fachfremd = fachfremd
        End If
        MeldeAenderung()
        Melde(NameOf(PlausibilitaetsZeile))
    End Sub

    ' ---------------------------------------------------------------
    ' Die Plausibilitaet im Kopf des Dialogs
    ' ---------------------------------------------------------------

    ''' <summary>Verfuegbares Deputat gegen Gesamtstundenbedarf.
    '''
    ''' Die Formulierung unterscheidet bewusst zwei Richtungen: zu WENIG
    ''' Deputat ist ein Beweis, dass es nicht aufgeht. Zu VIEL ist kein
    ''' Fehler, sondern der Hinweis auf den verteilten Leerlauf, den das
    ''' GMS-Beispiel gelehrt hat - deshalb steht dort "Reserve" und nicht
    ''' "Fehler".</summary>
    Public ReadOnly Property PlausibilitaetsZeile As String
        Get
            Dim bedarf = Kennzahlen.Gesamtbedarf(Bestand)
            Dim deputat = Kennzahlen.VerfuegbaresDeputat(Bestand)
            If bedarf = 0 Then Return $"{deputat:0.#} Stunden Deputat - noch kein Stundenbedarf hinterlegt."

            Dim diff = deputat - bedarf
            If diff < 0 Then
                Return $"{deputat:0.#} Stunden Deputat gegen {bedarf} Stunden Bedarf - " &
                       $"es fehlen {Math.Abs(diff):0.#}. So kann kein Plan aufgehen."
            End If
            Dim anteil = If(bedarf > 0, diff / bedarf, 0)
            If anteil > 0.25 Then
                Return $"{deputat:0.#} Stunden Deputat gegen {bedarf} Stunden Bedarf - " &
                       $"{diff:0.#} Stunden Reserve ({anteil:P0}). Grosse Pools erzeugen verteilten Leerlauf."
            End If
            Return $"{deputat:0.#} Stunden Deputat gegen {bedarf} Stunden Bedarf - {diff:0.#} Stunden Reserve."
        End Get
    End Property

    ''' <summary>Faecher ohne genug qualifiziertes Deputat (6.7). Steht
    ''' auch hier, weil die Qualifikationen in DIESER Maske gepflegt
    ''' werden und die Folge sofort sichtbar sein soll.</summary>
    Public ReadOnly Property Engpaesse As List(Of (Fach As String, Bedarf As Integer, Deputat As Double))
        Get
            Return Kennzahlen.EngpassFaecher(Bestand)
        End Get
    End Property

    Public Overrides Function Pruefe() As List(Of String)
        Dim fehler As New List(Of String)

        Dim namen As New HashSet(Of String)(StringComparer.CurrentCultureIgnoreCase)
        For Each l In Quelle()
            If String.IsNullOrWhiteSpace(l.Name) Then
                fehler.Add("Eine Lehrkraft ohne Namen kann keiner Klasse zugeordnet werden.")
            ElseIf Not namen.Add(l.Name) Then
                fehler.Add($"Der Lehrkraftname „{l.Name}" & Chr(34) & " kommt mehrfach vor.")
            End If
            If Kennzahlen.DeputatVon(l) < 0 Then
                fehler.Add($"„{l.Name}" & Chr(34) & ": Anrechnungen und Reserve uebersteigen das Deputat.")
            End If
        Next

        For Each e In Engpaesse
            fehler.Add($"Fach „{e.Fach}" & Chr(34) & $": {e.Bedarf} Stunden Bedarf, aber nur " &
                       $"{e.Deputat:0.#} Stunden qualifiziertes Deputat.")
        Next

        fehler.AddRange(StammdatenValidation.ValidateStammdaten(Bestand).
            Where(Function(x) x.IndexOf("Lehr", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                              x.IndexOf("qualifiz", StringComparison.OrdinalIgnoreCase) >= 0))
        Return fehler
    End Function

    Protected Overrides Function LoeschFolgen(eintrag As Lehrer) As AenderungsFolgen
        Return Bestandspflege.Verweise(_projekt, Stammart.Lehrkraft, eintrag.Name)
    End Function

    Protected Overrides Sub Entferne(eintrag As Lehrer)
        Bestandspflege.Loesche(_projekt, Stammart.Lehrkraft, eintrag.Name)
    End Sub

    Public Overrides Function BenenneUm(alt As String, neu As String) As Integer
        Dim folgen = Bestandspflege.BenenneUm(_projekt, Stammart.Lehrkraft, alt, neu)
        Aktualisiere()
        MeldeAenderung()
        Return folgen.Verweise.Count
    End Function

End Class
