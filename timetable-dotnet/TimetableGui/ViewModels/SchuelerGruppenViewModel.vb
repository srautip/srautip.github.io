' Schueler und Gruppen, Scheduling-Seite (gui-ui-konzept.md 6.8).
'
' Der Stundenplan braucht grundsaetzlich KEINE Einzelschueler - Klassen
' tragen nur eine informative `schuelerzahl`. Diese Maske ist nur noetig,
' wenn klassenuebergreifende Gruppen (Religion, Foerderung, Niveaukurse)
' mitgeplant werden sollen. "und dafuer genuegen anonyme Platzhalter."
'
' Der Generator erzeugt genau die Fixtures, "was die Beispiel-Fixtures
' bisher per Wegwerf-Skript erzeugt haben".
Imports TimetableCore
Imports TimetableProjekt

Public NotInheritable Class SchuelerGruppenViewModel
    Inherits ListenViewModel(Of Gruppe)

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

    ' Die Liste zeigt die GRUPPEN - sie sind das, was man pflegt.
    ' Die Schuelerliste haengt darunter als Mitglieder-Auswahl.
    Protected Overrides Function Quelle() As IList(Of Gruppe)
        Return Bestand.Gruppen
    End Function

    Protected Overrides Function Erzeuge() As Gruppe
        Return New Gruppe With {.Typ = "Fachgruppe"}
    End Function

    Protected Overrides Function Kopiere(vorlage As Gruppe) As Gruppe
        Return New Gruppe With {
            .Name = vorlage.Name, .Typ = vorlage.Typ,
            .FachName = vorlage.FachName, .Klassenstufe = vorlage.Klassenstufe,
            .Parallelverbund = vorlage.Parallelverbund,
            .MitgliederSchuelerIds = vorlage.MitgliederSchuelerIds.ToList()}
    End Function

    Protected Overrides Function NameVon(eintrag As Gruppe) As String
        Return If(eintrag.Name, "")
    End Function

    Protected Overrides Sub SetzeName(eintrag As Gruppe, name As String)
        eintrag.Name = name
    End Sub

    Protected Overrides Function BasisName() As String
        Return "Neue Gruppe"
    End Function

    Public ReadOnly Property Schueler As List(Of Schueler)
        Get
            Return Bestand.Schueler.OrderBy(Function(s) s.Id, StringComparer.Ordinal).ToList()
        End Get
    End Property

    Public Function IstMitglied(g As Gruppe, schuelerId As String) As Boolean
        Return g IsNot Nothing AndAlso g.MitgliederSchuelerIds.Contains(schuelerId)
    End Function

    Public Sub SetzeMitglied(g As Gruppe, schuelerId As String, drin As Boolean)
        If g Is Nothing Then Return
        If drin Then
            If Not g.MitgliederSchuelerIds.Contains(schuelerId) Then g.MitgliederSchuelerIds.Add(schuelerId)
        Else
            g.MitgliederSchuelerIds.Remove(schuelerId)
        End If
        MeldeAenderung()
    End Sub

    ' ---------------------------------------------------------------
    ' Anonyme Platzhalter (6.8, zugleich Assistent-Schritt 3)
    ' ---------------------------------------------------------------

    ''' <summary>Erzeugt deterministisch Platzhalter-Schueler je Klasse.
    '''
    ''' "Platzhalter erhalten KEINEN mapping.json-Eintrag (kein
    ''' Personenbezug), sind als solche markiert" - deshalb wird hier
    ''' NICHT ueber Projekt.Mapping gegangen. Die Id folgt dem Muster der
    ''' Beispiel-Fixtures (S-1a-01), damit erzeugte und mitgelieferte
    ''' Bestaende gleich aussehen.</summary>
    Public Function PlatzhalterErzeugen(klasseName As String, anzahl As Integer) As List(Of String)
        Dim erzeugt As New List(Of String)
        If String.IsNullOrWhiteSpace(klasseName) OrElse anzahl < 1 Then Return erzeugt
        If Not Bestand.Klassen.Any(Function(k) k.Name = klasseName) Then Return erzeugt

        Dim vorhanden = Bestand.Schueler.Where(Function(s) s.Klasse = klasseName).Count
        For i = 1 To anzahl
            Dim id = $"S-{klasseName}-{(vorhanden + i):00}"
            If Bestand.Schueler.Any(Function(s) s.Id = id) Then Continue For
            Bestand.Schueler.Add(New Schueler With {.Id = id, .Klasse = klasseName})
            erzeugt.Add(id)
        Next
        If erzeugt.Count > 0 Then MeldeAenderung()
        Return erzeugt
    End Function

    ''' <summary>Teilt die Kinder einer Klassenstufe auf mehrere Gruppen
    ''' auf - die "typischen Gruppen-Vorlagen" aus 6.8, z.B. Religion
    ''' ev/kath/Ethik. Reihum statt zufaellig: die Aufteilung ist damit
    ''' reproduzierbar, und genau das erwarten die Beispiel-Fixtures.</summary>
    Public Function AufteilenAufGruppen(stufe As Integer, gruppenNamen As IEnumerable(Of String),
                                        fachNamen As IEnumerable(Of String),
                                        Optional verbund As String = Nothing) As List(Of String)
        Dim namen = If(gruppenNamen, Array.Empty(Of String)()).ToList()
        Dim faecher = If(fachNamen, Array.Empty(Of String)()).ToList()
        Dim angelegt As New List(Of String)
        If namen.Count = 0 OrElse namen.Count <> faecher.Count Then Return angelegt

        Dim klassen = Bestand.Klassen.Where(Function(k) k.Klassenstufe = stufe).Select(Function(k) k.Name).ToHashSet()
        Dim kinder = Bestand.Schueler.Where(Function(s) klassen.Contains(s.Klasse)).
                         OrderBy(Function(s) s.Id, StringComparer.Ordinal).ToList()
        If kinder.Count = 0 Then Return angelegt

        For i = 0 To namen.Count - 1
            Dim g As New Gruppe With {
                .Name = namen(i), .Typ = "Fachgruppe",
                .FachName = faecher(i), .Klassenstufe = stufe,
                .Parallelverbund = verbund}
            For j = i To kinder.Count - 1 Step namen.Count
                g.MitgliederSchuelerIds.Add(kinder(j).Id)
            Next
            Bestand.Gruppen.Add(g)
            angelegt.Add(g.Name)
        Next

        Aktualisiere()
        MeldeAenderung()
        Return angelegt
    End Function

    Public Overrides Function Pruefe() As List(Of String)
        Dim fehler As New List(Of String)
        Dim ids = Bestand.Schueler.Select(Function(s) s.Id).ToHashSet(StringComparer.Ordinal)
        Dim faecher = Bestand.Faecher.Select(Function(f) f.Name).ToHashSet(StringComparer.Ordinal)
        Dim namen As New HashSet(Of String)(StringComparer.CurrentCultureIgnoreCase)

        For Each g In Quelle()
            If String.IsNullOrWhiteSpace(g.Name) Then
                fehler.Add("Eine Gruppe ohne Namen kann nicht referenziert werden.")
            ElseIf Not namen.Add(g.Name) Then
                fehler.Add($"Der Gruppenname „{g.Name}" & Chr(34) & " kommt mehrfach vor.")
            End If
            If g.FachName IsNot Nothing AndAlso Not faecher.Contains(g.FachName) Then
                fehler.Add($"„{g.Name}" & Chr(34) & $": Fach „{g.FachName}" & Chr(34) & " gibt es nicht.")
            End If
            For Each id In g.MitgliederSchuelerIds.Where(Function(x) Not ids.Contains(x))
                fehler.Add($"„{g.Name}" & Chr(34) & $": Schueler „{id}" & Chr(34) & " gibt es nicht.")
            Next
        Next

        ' Die Verbund-Regeln kommen aus dem KERN, nicht von hier.
        '
        ' Die erste Fassung hatte sie nach der Konzeptprosa
        ' nachgebaut ("Fach paarweise verschieden, gleiche Stufe") und
        ' meldete damit die loesende Beispielschule als fehlerhaft: ihr
        ' Verbund "Chor-Gesamt" fasst alle vier Chorgruppen
        ' STUFENUEBERGREIFEND mit demselben Fach zusammen - eine
        ' schulweite Gesamtprobe, die StammdatenValidation ausdruecklich
        ' unterstuetzt. Der Kern prueft die Eindeutigkeit auf dem TUPEL
        ' (Klassenstufe, Fach), nicht auf dem Fach allein.
        '
        ' Genau davor warnt das Konzept: "Validieren mit den
        ' bestehenden APIs, nicht mit UI-Sonderlogik" (gui-ui-konzept 1).
        ' Eine Maske, die strenger prueft als der Kern, meldet Fehler,
        ' die keine sind - und niemand traut ihr danach noch.
        fehler.AddRange(StammdatenValidation.ValidateStammdaten(Bestand).
            Where(Function(x) x.IndexOf("gruppen", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                              x.IndexOf("parallelverbund", StringComparison.OrdinalIgnoreCase) >= 0))

        Return fehler
    End Function

End Class
