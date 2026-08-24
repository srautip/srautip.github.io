' Der Projekt-Assistent (gui-ui-konzept.md 6.1) - fuenf Schritte,
' vollstaendig ohne Fenster pruefbar.
'
' Motor ist `Scaffold.Baue`, nicht eine eigene Erzeugung: der CLI-Befehl
' `new` und dieser Assistent sollen dieselbe Vorstellung davon haben, wie
' eine frische Schule aussieht. Was hier oben drauf kommt, sind genau die
' zwei Dinge, die die CLI nicht kennt - Platzhalter-Schueler (6.8) und
' die Gruppen-Vorlagen.
Imports TimetableCore
Imports TimetableProjekt
Imports TimetableWorkflow

''' <summary>Das Ergebnis des Assistenten. Bewusst ein reiner Datenhalter:
''' das HauptViewModel baut daraus das Projekt und speichert - es soll
''' nicht vom Fenster abhaengen, das ihn gefuellt hat.</summary>
Public NotInheritable Class ProjektEntwurf
    Public Property Bestand As Stammdatenbestand
    Public Property Schuljahr As String = ""
    Public Property Pfad As String = ""
    Public Property Passwort As String = ""
    ''' <summary>Die Klartext-Zeilen aus Schritt 5 - sie wandern ins
    ''' Audit-Log, damit spaeter nachvollziehbar ist, woher der
    ''' Startbestand stammt.</summary>
    Public Property Bericht As New List(Of String)
End Class

Public NotInheritable Class ProjektAssistentViewModel

    Public Const LetzterSchritt As Integer = 5

    Private ReadOnly _dialoge As IDialoge

    Public Sub New(dialoge As IDialoge)
        _dialoge = dialoge
        Schulart = "Grundschule"
    End Sub

    ' ---------------------------------------------------------------
    ' Schritt 1 - Schule
    ' ---------------------------------------------------------------

    Public Property SchulName As String = ""
    Public Property Bundesland As String = "BW"
    Public Property Schuljahr As String = ""

    Private _schulart As String = ""

    ''' <summary>Setzt zugleich die Vorgaben, die von der Schulart
    ''' abhaengen. Ohne das stuende nach einem Wechsel von Gemeinschafts-
    ''' auf Grundschule "6 Klassenstufen" da, was es dort nicht gibt.</summary>
    Public Property Schulart As String
        Get
            Return _schulart
        End Get
        Set(wert As String)
            If _schulart = wert Then Return
            _schulart = wert
            Dim t = Vorlage()
            KlassenstufenAnzahl = t.Klassenstufen.Count
            LehrerAnzahl = Math.Max(MindestLehrer(), t.Klassenstufen.Count * 2)
            _bestand = Nothing
        End Set
    End Property

    Public ReadOnly Property Schularten As String() = {"Grundschule", "Gemeinschaftsschule"}

    Private Function Vorlage() As SchoolTemplate
        Return Templates.TemplateFuer(Bundesland, If(_schulart = "", "Grundschule", _schulart))
    End Function

    ''' <summary>Obergrenze fuer Schritt 2, aus dem Template - nicht
    ''' hartkodiert: die Grundschule hat 4, die Gemeinschaftsschule 6
    ''' Klassenstufen, und das steht drueben.</summary>
    Public Function MaxKlassenstufen() As Integer
        Return Vorlage().Klassenstufen.Count
    End Function

    ''' <summary>Untergrenze fuer die Zahl der Klassenlehrkraefte: je ein
    ''' gesteuerter Pool-Typ. Darunter bliebe ein Kernfach ohne
    ''' qualifizierte Lehrkraft - `Scaffold.Baue` wuerfe dann.</summary>
    Public Function MindestLehrer() As Integer
        Return Vorlage().LehrerPools.Where(
            Function(p) p.GesteuertDurchAnzahlLehrerParameter).Count
    End Function

    ' ---------------------------------------------------------------
    ' Schritt 2 - Struktur
    ' ---------------------------------------------------------------

    Public Property KlassenstufenAnzahl As Integer = 4
    Public Property Zuege As Integer = 2
    Public Property LehrerAnzahl As Integer = 8

    ''' <summary>Die Klassenstufen, die mit den aktuellen Eingaben
    ''' entstehen - Schritt 3 fragt die Schuelerzahlen je Stufe ab und
    ''' braucht dafuer die Nummern.</summary>
    Public Function Klassenstufen() As List(Of Integer)
        Return Vorlage().Klassenstufen.Take(Math.Max(0, KlassenstufenAnzahl)).
            Select(Function(k) k.Nummer).ToList()
    End Function

    ' ---------------------------------------------------------------
    ' Schritt 3 - Schueler und Gruppen (optional)
    ' ---------------------------------------------------------------

    ''' <summary>Anzahl Kinder je KLASSE, nicht je Stufe - so steht es in
    ''' der Klassenliste, und der Nutzer denkt in Klassenstaerken.</summary>
    Public Property SchuelerJeKlasse As New Dictionary(Of Integer, Integer)

    Public Property GewaehlteVorlagen As New List(Of String)

    ''' <summary>Schritt 3 ist ueberspringbar: "ohne diesen Schritt rechnet
    ''' der Plan rein klassenbasiert" (6.1). Kein Sonderfall im Code -
    ''' ohne Zahlen entstehen keine Kinder.</summary>
    Public ReadOnly Property ErzeugtSchueler As Boolean
        Get
            Return SchuelerJeKlasse.Values.Any(Function(n) n > 0)
        End Get
    End Property

    ' ---------------------------------------------------------------
    ' Schritt 4 - Schutz
    ' ---------------------------------------------------------------

    Public Property Passwort As String = ""
    Public Property PasswortWiederholung As String = ""
    Public Property Pfad As String = ""

    ''' <summary>Grobe Staerke in vier Stufen. Bewusst KEINE Prozentzahl:
    ''' eine Zahl mit zwei Nachkommastellen taeuscht eine Genauigkeit vor,
    ''' die keine Heuristik hat.</summary>
    Public Function PasswortStaerke() As String
        Dim p = If(Passwort, "")
        If p.Length < 8 Then Return "zu kurz (mindestens 8 Zeichen)"
        Dim klassen = 0
        If p.Any(AddressOf Char.IsLower) Then klassen += 1
        If p.Any(AddressOf Char.IsUpper) Then klassen += 1
        If p.Any(AddressOf Char.IsDigit) Then klassen += 1
        If p.Any(Function(c) Not Char.IsLetterOrDigit(c)) Then klassen += 1
        If p.Length >= 16 AndAlso klassen >= 3 Then Return "stark"
        If p.Length >= 12 AndAlso klassen >= 2 Then Return "brauchbar"
        Return "schwach"
    End Function

    Public Sub SpeicherortWaehlen()
        Dim vorschlag = If(String.IsNullOrWhiteSpace(SchulName), "Neues Projekt", SchulName) & ".splanx"
        Dim gewaehlt = _dialoge.ProjektdateiSpeichernUnter(vorschlag)
        If gewaehlt IsNot Nothing Then Pfad = gewaehlt
    End Sub

    ' ---------------------------------------------------------------
    ' Pruefung je Schritt
    ' ---------------------------------------------------------------

    ''' <summary>Was diesen Schritt am Weitergehen hindert. Leere Liste =
    ''' weiter. Die Pruefung sitzt HIER und nicht im Fenster, weil sie die
    ''' einzige Stelle ist, die Schaden verhindern kann - `Scaffold.Baue`
    ''' wirft sonst mitten im letzten Schritt.</summary>
    Public Function Pruefe(schritt As Integer) As List(Of String)
        Dim fehler As New List(Of String)
        Select Case schritt
            Case 1
                If String.IsNullOrWhiteSpace(SchulName) Then fehler.Add("Der Schulname fehlt.")
                If Not Schularten.Contains(_schulart) Then fehler.Add("Bitte eine Schulart wählen.")
                If Not String.Equals(Bundesland, "BW", StringComparison.OrdinalIgnoreCase) Then
                    ' Kein stiller Rückfall auf BW: erfundene Lehrplanzahlen
                    ' wären schlimmer als ein blockierter Schritt.
                    fehler.Add("Bisher ist nur Baden-Württemberg (BW) hinterlegt.")
                End If
            Case 2
                Dim max = MaxKlassenstufen()
                If KlassenstufenAnzahl < 1 OrElse KlassenstufenAnzahl > max Then
                    fehler.Add($"Klassenstufen: 1 bis {max} für {_schulart}.")
                End If
                If Zuege < 1 Then fehler.Add("Mindestens ein Zug je Klassenstufe.")
                If LehrerAnzahl < MindestLehrer() Then
                    fehler.Add($"Mindestens {MindestLehrer()} Klassenlehrkräfte – sonst bliebe ein Kernfach ohne qualifizierte Lehrkraft.")
                End If
            Case 3
                For Each paar In SchuelerJeKlasse
                    If paar.Value < 0 Then fehler.Add($"Klassenstufe {paar.Key}: negative Schülerzahl.")
                Next
                If GewaehlteVorlagen.Count > 0 AndAlso Not ErzeugtSchueler Then
                    fehler.Add("Gruppen-Vorlagen brauchen Schüler – bitte Anzahlen eintragen oder die Vorlagen abwählen.")
                End If
            Case 4
                If Passwort <> PasswortWiederholung Then fehler.Add("Die Passwörter stimmen nicht überein.")
                If String.IsNullOrEmpty(Passwort) Then fehler.Add("Ohne Passwort keine verschlüsselte Projektdatei.")
                If PasswortStaerke().StartsWith("zu kurz") Then fehler.Add("Das Passwort ist zu kurz (mindestens 8 Zeichen).")
                If String.IsNullOrWhiteSpace(Pfad) Then fehler.Add("Der Speicherort fehlt.")
        End Select
        Return fehler
    End Function

    ' ---------------------------------------------------------------
    ' Schritt 5 - Zusammenfassung
    ' ---------------------------------------------------------------

    Private _bestand As Stammdatenbestand
    Private _bericht As New List(Of String)

    ''' <summary>Baut den Startbestand. Wird bei jedem Betreten von Schritt
    ''' 5 neu gerufen: die Zusammenfassung soll den Stand zeigen, der
    ''' gleich entsteht, nicht einen von vorhin.</summary>
    Public Function Vorschau() As Stammdatenbestand
        _bericht = New List(Of String)
        Dim b = Scaffold.Baue(Bundesland, _schulart, KlassenstufenAnzahl, LehrerAnzahl, Zuege, SchulName)

        For Each klasse In b.Klassen
            Dim anzahl = 0
            If Not SchuelerJeKlasse.TryGetValue(klasse.Klassenstufe, anzahl) Then Continue For
            If anzahl <= 0 Then Continue For
            klasse.Schuelerzahl = anzahl
            For i = 1 To anzahl
                b.Schueler.Add(New Schueler With {.Id = $"S-{klasse.Name}-{i:00}", .Klasse = klasse.Name})
            Next
        Next
        If b.Schueler.Count > 0 Then
            _bericht.Add($"{b.Schueler.Count} anonyme Platzhalter-Schüler (S-Klasse-NN) – ohne Klarnamen-Eintrag.")
        End If

        For Each name In GewaehlteVorlagen
            Dim v = GruppenVorlagen.Alle.FirstOrDefault(Function(x) x.Name = name)
            If v Is Nothing Then Continue For
            _bericht.AddRange(GruppenVorlagen.Anwenden(b, v, Klassenstufen()))
        Next

        _bestand = b
        Return b
    End Function

    ''' <summary>Der erzeugte Startbestand in Zahlen (6.1, Schritt 5) plus
    ''' die Klartext-Zeilen der Vorlagen. Ruft `Vorschau` NICHT selbst -
    ''' der Aufrufer entscheidet, wann neu gebaut wird.</summary>
    Public Function Zusammenfassung() As List(Of String)
        If _bestand Is Nothing Then Return New List(Of String) From {"(noch nichts erzeugt)"}
        Dim b = _bestand
        Dim zeilen As New List(Of String) From {
            $"{b.Klassenstufen.Count} Klassenstufen mit {b.Klassen.Count} Klassen ({Zuege} Züge je Stufe)",
            $"{b.Faecher.Count} Fächer aus dem Kontingent-Template {Bundesland}/{_schulart}",
            $"{b.Lehrkraefte.Count} Lehrkräfte ({LehrerAnzahl} davon als Klassenlehrkräfte angefordert, " &
                $"die übrigen bedarfsgerecht ergänzt)",
            $"{b.PeriodsPerDay} Stunden an {b.Tage.Count} Tagen"
        }
        If b.Gruppen.Count > 0 Then zeilen.Add($"{b.Gruppen.Count} Gruppen")
        zeilen.AddRange(_bericht)

        ' Der ehrliche Teil: der Assistent verspricht einen rechenbaren
        ' Start, keinen fertigen Plan. Was die Prüfung sieht, steht hier.
        Dim fehler = StammdatenValidation.ValidateStammdaten(b)
        If fehler.Count = 0 Then
            zeilen.Add("Prüfung: keine Beanstandungen – das Projekt ist sofort rechenbar.")
        Else
            zeilen.Add($"Prüfung: {fehler.Count} Beanstandung(en) – anlegen geht trotzdem, rechnen erst nach Korrektur:")
            zeilen.AddRange(fehler.Take(10))
        End If
        zeilen.Add("Räume und Regeln bleiben leer – beides wird selten generisch gebraucht.")
        Return zeilen
    End Function

    ''' <summary>Das Ergebnis fuer das HauptViewModel. Setzt voraus, dass
    ''' `Vorschau` gelaufen ist.</summary>
    Public Function Entwurf() As ProjektEntwurf
        If _bestand Is Nothing Then Vorschau()
        Return New ProjektEntwurf With {
            .Bestand = _bestand, .Schuljahr = Schuljahr,
            .Pfad = Pfad, .Passwort = Passwort,
            .Bericht = Zusammenfassung()}
    End Function

End Class
