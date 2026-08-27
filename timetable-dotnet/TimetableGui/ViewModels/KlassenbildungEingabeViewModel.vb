' Klassenbildungs-Eingaben (gui-ui-konzept.md 6.11).
'
' Die Einschulungsliste ist AUSDRUECKLICH nicht die Schuelerliste aus
' 6.8: "die Klassenbildung laeuft VOR der Klassenzuteilung". Vorher gibt
' es noch keine Klassen, denen man jemanden zuordnen koennte - deshalb
' ein eigenes Modell (KlassenbildungInput) und eine eigene Liste.
'
' Die Klarnamen-Grenze gilt auch hier: die Liste traegt IDs, der Name
' steht ausschliesslich in mapping.json (Datenhaltung 6.1). Der Import
' unten trennt beides beim Anlegen.
Imports TimetableCore
Imports TimetableProjekt

Public NotInheritable Class KlassenbildungEingabeViewModel
    Inherits Beobachtbar

    Private ReadOnly _projekt As Projekt
    Private ReadOnly _dialoge As IDialoge

    Public Sub New(projekt As Projekt, dialoge As IDialoge)
        _projekt = projekt
        _dialoge = dialoge
    End Sub

    Public Event Geaendert As EventHandler

    Private Sub MeldeAenderung()
        RaiseEvent Geaendert(Me, EventArgs.Empty)
        Melde(NameOf(RahmenZeile))
        Melde(NameOf(LabelVorschau))
    End Sub

    Private ReadOnly Property Eingabe As KlassenbildungInput
        Get
            Return _projekt.Klassenbildung
        End Get
    End Property

    ' ===============================================================
    ' Klassenrahmen
    ' ===============================================================

    Public Property Anzahl As Integer
        Get
            Return Eingabe.Klassen.Anzahl
        End Get
        Set
            If Eingabe.Klassen.Anzahl <> value Then
                Eingabe.Klassen.Anzahl = value
                Melde()
                MeldeAenderung()
            End If
        End Set
    End Property

    Public Property MinGroesse As Integer
        Get
            Return Eingabe.Klassen.MinGroesse
        End Get
        Set
            Eingabe.Klassen.MinGroesse = value
            Melde()
            MeldeAenderung()
        End Set
    End Property

    Public Property MaxGroesse As Integer
        Get
            Return Eingabe.Klassen.MaxGroesse
        End Get
        Set
            Eingabe.Klassen.MaxGroesse = value
            Melde()
            MeldeAenderung()
        End Set
    End Property

    ''' <summary>Stufe ODER explizite Labels (6.11) - nicht beides. Wer
    ''' Labels setzt, hat die Namen bereits entschieden; eine Stufe
    ''' daneben waere eine zweite, womoeglich widersprechende Quelle
    ''' derselben Namen.</summary>
    Public Property Stufe As Integer?
        Get
            Return Eingabe.Klassen.Stufe
        End Get
        Set
            Eingabe.Klassen.Stufe = value
            If value.HasValue Then Eingabe.Klassen.Labels = Nothing
            Melde()
            MeldeAenderung()
        End Set
    End Property

    Public Property Labels As List(Of String)
        Get
            Return Eingabe.Klassen.Labels
        End Get
        Set
            Eingabe.Klassen.Labels = If(value IsNot Nothing AndAlso value.Count > 0, value, Nothing)
            If Eingabe.Klassen.Labels IsNot Nothing Then Eingabe.Klassen.Stufe = Nothing
            Melde()
            MeldeAenderung()
        End Set
    End Property

    ''' <summary>Die Vorschau "1a, 1b, ..." aus 6.11 - dieselbe Ableitung,
    ''' die der Kern beim Rechnen verwendet. Keine zweite Namenslogik
    ''' hier: die Labels im Board muessen genau so heissen.</summary>
    Public ReadOnly Property LabelVorschau As String
        Get
            Try
                Return String.Join(", ", Klassenbildung.KlassenLabels(Eingabe))
            Catch
                Return "(noch nicht bestimmbar)"
            End Try
        End Get
    End Property

    ''' <summary>Live-Check "Anzahl x Groesse gegen Schuelerzahl" (6.11).
    ''' Beide Richtungen zaehlen: zu wenig Platz ist unloesbar, zu viel
    ''' erzeugt Klassen unter der Mindestgroesse.</summary>
    Public ReadOnly Property RahmenZeile As String
        Get
            Dim n = Eingabe.Schueler.Count
            Dim k = Eingabe.Klassen.Anzahl
            If k < 1 Then Return $"{n} Kinder - noch keine Klassenanzahl gesetzt."

            Dim minPlatz = k * Eingabe.Klassen.MinGroesse
            Dim maxPlatz = k * Eingabe.Klassen.MaxGroesse
            If maxPlatz < n Then
                Return $"{n} Kinder auf {k} Klassen zu je hoechstens {Eingabe.Klassen.MaxGroesse}: " &
                       $"{n - maxPlatz} Kinder haben keinen Platz."
            End If
            If minPlatz > n Then
                Return $"{n} Kinder auf {k} Klassen zu je mindestens {Eingabe.Klassen.MinGroesse}: " &
                       $"es fehlen {minPlatz - n} Kinder, um alle Klassen zu fuellen."
            End If
            Return $"{n} Kinder auf {k} Klassen ({Eingabe.Klassen.MinGroesse}-{Eingabe.Klassen.MaxGroesse} je Klasse)."
        End Get
    End Property

    ' ===============================================================
    ' Einschulungsliste
    ' ===============================================================

    Public ReadOnly Property Schueler As List(Of KlassenbildungSchueler)
        Get
            Return Eingabe.Schueler
        End Get
    End Property

    ''' <summary>Alle bisher vergebenen Attributnamen - das "frei
    ''' definierbare Vokabular" aus 6.11. Balance-Regeln waehlen daraus
    ''' aus, statt Freitext zu erlauben; sonst zeigt eine Balance auf ein
    ''' Attribut, das kein Kind traegt.</summary>
    Public ReadOnly Property Attributnamen As List(Of String)
        Get
            Return Eingabe.Schueler.SelectMany(Function(s) s.Attribute.Keys).
                Distinct().OrderBy(Function(x) x, StringComparer.CurrentCultureIgnoreCase).ToList()
        End Get
    End Property

    Public Function Werte(attribut As String) As List(Of String)
        Return Eingabe.Schueler.
            Where(Function(s) s.Attribute.ContainsKey(attribut)).
            Select(Function(s) s.Attribute(attribut)).
            Distinct().OrderBy(Function(x) x, StringComparer.CurrentCultureIgnoreCase).ToList()
    End Function

    ''' <summary>Der Hinweis, den 6.11 fuer die Spaltenverwaltung
    ''' verlangt: "empfiehlt wertneutrale Tags und verlinkt den Grundsatz
    ''' 'die Diagnose selbst muss nie ins System'". Er steht hier und
    ''' nicht als Beschriftung im XAML, damit er in jeder Ansicht
    ''' gleich lautet.</summary>
    Public ReadOnly Property SpaltenHinweis As String
        Get
            Return "Wertneutrale Kuerzel verwenden (z.B. „SOZ"", „FOE"") - die Diagnose " &
                   "selbst muss nie ins System. Die Spaltennamen erscheinen in " &
                   "Pruefmeldungen und im Export."
        End Get
    End Property

    ''' <summary>Fuegt ein Kind hinzu. Der Klarname wandert in
    ''' mapping.json und NICHT in die Liste - dort steht nur die ID
    ''' (Datenhaltung 6.1). Ohne Klarnamen entsteht kein
    ''' Mapping-Eintrag: ein anonymer Platzhalter hat keinen
    ''' Personenbezug.</summary>
    Public Function Hinzufuegen(nachname As String, vorname As String,
                                attribute As IDictionary(Of String, String)) As String
        Dim id = _projekt.NeueSchuelerId()
        Dim kind As New KlassenbildungSchueler With {.Id = id}
        If attribute IsNot Nothing Then
            For Each kv In attribute.Where(Function(x) Not String.IsNullOrWhiteSpace(x.Value))
                kind.Attribute(kv.Key) = kv.Value.Trim()
            Next
        End If
        Eingabe.Schueler.Add(kind)

        If Not String.IsNullOrWhiteSpace(nachname) OrElse Not String.IsNullOrWhiteSpace(vorname) Then
            _projekt.Mapping.Add(New MappingEintrag With {
                .Id = id, .Nachname = If(nachname, "").Trim(), .Vorname = If(vorname, "").Trim()})
        End If

        MeldeAenderung()
        Return id
    End Function

    Public Sub Entfernen(id As String)
        Dim kind = Eingabe.Schueler.FirstOrDefault(Function(s) s.Id = id)
        If kind Is Nothing Then Return
        Eingabe.Schueler.Remove(kind)
        ' Mitgliedschaften und Wuensche mitnehmen - sonst zeigen sie auf
        ' ein Kind, das es nicht mehr gibt.
        For Each g In Eingabe.Gruppen
            g.Mitglieder.Remove(id)
        Next
        Eingabe.Wuensche.RemoveAll(Function(w) w.Kinder.Contains(id))
        Eingabe.Fixierungen.RemoveAll(Function(f) f.Kind = id)
        _projekt.Mapping.RemoveAll(Function(m) m.Id = id)
        MeldeAenderung()
    End Sub

    ''' <summary>Anzeigename fuer die Liste: "Mia Muster (S001)" bzw. nur
    ''' die ID. "Klarnamen nur in der Anzeigeschicht" (Konzept 1) - die
    ''' Liste selbst haelt sie nicht.</summary>
    Public Function Anzeigename(id As String) As String
        Return _projekt.Anzeigename(id)
    End Function

    ' ===============================================================
    ' Zwischenablage-Import
    ' ===============================================================

    ''' <summary>Was der Import aus dem eingefuegten Text macht, BEVOR er
    ''' etwas anlegt. Die Maske zeigt das als Vorschau - ein Import, der
    ''' erst nach dem Ausfuehren zeigt, was er verstanden hat, ist bei
    ''' hundert Kindern nicht mehr zurueckzunehmen.</summary>
    Public NotInheritable Class ImportVorschau
        Public Property Zeilen As New List(Of String())
        Public Property Kopfzeile As Boolean
        Public Property Spalten As New List(Of String)
        Public Property Trenner As Char?
        Public ReadOnly Property Datensaetze As Integer
            Get
                Return Math.Max(0, Zeilen.Count - If(Kopfzeile, 1, 0))
            End Get
        End Property
    End Class

    Public Function ImportPruefen(text As String) As ImportVorschau
        Dim trenner = ZeilenImport.Trennzeichen(text)
        Dim zeilen = ZeilenImport.Zerlege(text, trenner)
        Dim kopf = ZeilenImport.SiehtNachKopfzeileAus(zeilen)
        Dim v As New ImportVorschau With {.Zeilen = zeilen, .Kopfzeile = kopf, .Trenner = trenner}
        If zeilen.Count > 0 Then
            v.Spalten = If(kopf, zeilen(0).ToList(),
                           Enumerable.Range(1, zeilen(0).Length).
                               Select(Function(i) $"Spalte {i}").ToList())
        End If
        Return v
    End Function

    ''' <summary>Uebernimmt die Vorschau mit der Zuordnung aus 9.1.
    '''
    ''' Die Zuordnung selbst liegt in Spaltenzuordnung.vb - hier steht
    ''' nur, wie ein Kind entsteht: die Id vergibt die GUI, der Klarname
    ''' geht nach mapping.json und nirgendwo sonst hin.</summary>
    Public Function ImportUebernehmen(v As ImportVorschau,
                                      wahlen As List(Of Spaltenwahl)) As Importbericht
        If v Is Nothing OrElse v.Zeilen.Count = 0 Then Return New Importbericht
        Dim zeilen = v.Zeilen.Skip(If(v.Kopfzeile, 1, 0)).ToList()
        Dim bericht = Spaltenzuordnung.Uebernehmen(
            _projekt, zeilen, wahlen,
            Function(nach, vor, attribute) Hinzufuegen(nach, vor, attribute))
        MeldeAenderung()
        Return bericht
    End Function

    ' ===============================================================
    ' Pruefung
    ' ===============================================================

    Public Function Pruefe() As List(Of String)
        Dim fehler As New List(Of String)

        If Eingabe.Klassen.Anzahl < 1 Then fehler.Add("Es ist keine Klassenanzahl gesetzt.")
        If Eingabe.Klassen.MinGroesse > Eingabe.Klassen.MaxGroesse Then
            fehler.Add($"Mindestgroesse {Eingabe.Klassen.MinGroesse} liegt ueber der Hoechstgroesse {Eingabe.Klassen.MaxGroesse}.")
        End If
        If Eingabe.Klassen.Labels IsNot Nothing AndAlso Eingabe.Klassen.Labels.Count <> Eingabe.Klassen.Anzahl Then
            fehler.Add($"{Eingabe.Klassen.Labels.Count} Label(s) fuer {Eingabe.Klassen.Anzahl} Klassen.")
        End If

        Dim n = Eingabe.Schueler.Count
        If Eingabe.Klassen.Anzahl > 0 Then
            If Eingabe.Klassen.Anzahl * Eingabe.Klassen.MaxGroesse < n Then
                fehler.Add(RahmenZeile)
            End If
        End If

        Dim ids = Eingabe.Schueler.Select(Function(s) s.Id).ToList()
        If ids.Distinct().Count() <> ids.Count Then fehler.Add("Doppelte Schueler-IDs in der Einschulungsliste.")
        Dim idMenge = ids.ToHashSet(StringComparer.Ordinal)

        For Each g In Eingabe.Gruppen
            If String.IsNullOrWhiteSpace(g.Id) Then fehler.Add("Eine Gruppe ohne Id.")
            For Each m In g.Mitglieder.Where(Function(x) Not idMenge.Contains(x))
                fehler.Add($"Gruppe „{g.Id}"": Kind „{m}"" steht nicht in der Liste.")
            Next
            If g.Prio < 1 OrElse g.Prio > 3 Then fehler.Add($"Gruppe „{g.Id}"": Prio {g.Prio} liegt ausserhalb 1-3.")
        Next

        Dim vokabular = Attributnamen.ToHashSet(StringComparer.Ordinal)
        For Each b In Eingabe.Balance
            If Not vokabular.Contains(If(b.Attribut, "")) Then
                ' Genau der Fall, den die Auswahlliste verhindern soll.
                fehler.Add($"Balance auf „{b.Attribut}"": dieses Attribut traegt kein Kind.")
            End If
        Next

        For Each w In Eingabe.Wuensche
            If w.Kinder.Count <> 2 Then
                fehler.Add($"Ein Wunsch nennt {w.Kinder.Count} Kinder - es muessen genau zwei sein.")
            End If
            For Each k In w.Kinder.Where(Function(x) Not idMenge.Contains(x))
                fehler.Add($"Wunsch: Kind „{k}"" steht nicht in der Liste.")
            Next
        Next

        For Each f In Eingabe.Fixierungen.Where(Function(x) Not idMenge.Contains(x.Kind))
            fehler.Add($"Fixierung: Kind „{f.Kind}"" steht nicht in der Liste.")
        Next

        Return fehler
    End Function

End Class
