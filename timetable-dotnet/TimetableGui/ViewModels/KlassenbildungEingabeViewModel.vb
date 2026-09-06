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
Imports TimetableWorkflow

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

    ''' <summary>Was der Vorschlag fuer den Klassenrahmen gesetzt hat.
    ''' Nothing je Feld heisst: es war schon gesetzt und blieb.</summary>
    Public NotInheritable Class Rahmenvorschlag
        Public Property Klassenteiler As Integer
        Public Property Anzahl As Integer?
        Public Property MinGroesse As Integer?
        Public Property MaxGroesse As Integer?

        Public ReadOnly Property Leer As Boolean
            Get
                Return Not (Anzahl.HasValue OrElse MinGroesse.HasValue OrElse MaxGroesse.HasValue)
            End Get
        End Property

        Public Function Hinweistext(schulart As String, bundesland As String) As String
            Dim teile As New List(Of String)
            If Anzahl.HasValue Then teile.Add($"Klassenanzahl {Anzahl.Value}")
            If MinGroesse.HasValue Then teile.Add($"Mindestgröße {MinGroesse.Value}")
            If MaxGroesse.HasValue Then teile.Add($"Höchstgröße {MaxGroesse.Value}")
            Dim quelle = If(String.IsNullOrWhiteSpace(schulart), "ohne Schulart", schulart) & "/" &
                         If(String.IsNullOrWhiteSpace(bundesland), "?", bundesland)
            Return $"Klassenrahmen vorgeschlagen (Klassenteiler {quelle}: {Klassenteiler}): " &
                   String.Join(", ", teile) &
                   " – nur leere Felder wurden gefüllt; änderbar unter „Kinder & Rahmen""."
        End Function
    End Class

    ''' <summary>Rechnet den Vorschlag, rein und ohne Bestand. Die
    ''' Hoechstgroesse ist der Klassenteiler, die Anzahl die kleinste,
    ''' bei der alle Platz haben. Die Mindestgroesse liegt sechs unter
    ''' dem wirksamen Maximum, aber nie ueber dem Durchschnitt - so gilt
    ''' immer anzahl*min &lt;= kinder und min &lt;= max, und der Rahmen
    ''' ist auf Anhieb rechenbar. Nur Felder mit 0 werden vorgeschlagen;
    ''' ein gesetztes Feld geht in die uebrigen ein (eine gesetzte Anzahl
    ''' bestimmt den Durchschnitt).</summary>
    Public Shared Function RahmenBerechnen(kinderzahl As Integer, teiler As Integer,
                                           anzahlVorhanden As Integer, minVorhanden As Integer,
                                           maxVorhanden As Integer) As Rahmenvorschlag
        Dim v As New Rahmenvorschlag With {.Klassenteiler = teiler}
        If kinderzahl < 1 OrElse teiler < 1 Then Return v
        Dim klassenzahl = If(anzahlVorhanden > 0, anzahlVorhanden, Math.Max(1, (kinderzahl + teiler - 1) \ teiler))
        Dim obere = If(maxVorhanden > 0, maxVorhanden, teiler)
        If anzahlVorhanden < 1 Then v.Anzahl = klassenzahl
        If maxVorhanden < 1 Then v.MaxGroesse = obere
        If minVorhanden < 1 Then v.MinGroesse = Math.Max(1, Math.Min(kinderzahl \ klassenzahl, obere - 6))
        Return v
    End Function

    ''' <summary>Schreibt den Vorschlag in die noch leeren Rahmenfelder.
    ''' Nothing, wenn es keine Kinder gibt oder nichts leer war - dann
    ''' hat sich auch nichts geaendert.</summary>
    Public Function RahmenVorschlagen() As Rahmenvorschlag
        Dim teiler = Templates.Klassenteiler(_projekt.Bestand.Bundesland, _projekt.Bestand.Schulart)
        Dim v = RahmenBerechnen(Eingabe.Schueler.Count, teiler, Anzahl, MinGroesse, MaxGroesse)
        If v.Leer Then Return Nothing
        If v.Anzahl.HasValue Then Eingabe.Klassen.Anzahl = v.Anzahl.Value
        If v.MinGroesse.HasValue Then Eingabe.Klassen.MinGroesse = v.MinGroesse.Value
        If v.MaxGroesse.HasValue Then Eingabe.Klassen.MaxGroesse = v.MaxGroesse.Value
        Melde(NameOf(Anzahl))
        Melde(NameOf(MinGroesse))
        Melde(NameOf(MaxGroesse))
        MeldeAenderung()
        Return v
    End Function

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
        Entfernen({id})
    End Sub

    ''' <summary>Mehrere Kinder auf einmal - mit EINER Meldung, nicht
    ''' einer je Kind. Mitgliedschaften, Wuensche, Fixierungen und
    ''' Klarnamen gehen mit - sonst zeigen sie auf ein Kind, das es
    ''' nicht mehr gibt.</summary>
    Public Sub Entfernen(ids As IEnumerable(Of String))
        Dim menge = ids.Where(Function(x) x IsNot Nothing).ToHashSet(StringComparer.Ordinal)
        If Eingabe.Schueler.RemoveAll(Function(s) menge.Contains(s.Id)) = 0 Then Return
        For Each g In Eingabe.Gruppen
            g.Mitglieder.RemoveAll(Function(m) menge.Contains(m))
        Next
        Eingabe.Wuensche.RemoveAll(Function(w) w.Kinder.Any(Function(k) menge.Contains(k)))
        Eingabe.Fixierungen.RemoveAll(Function(f) menge.Contains(f.Kind))
        _projekt.Mapping.RemoveAll(Function(m) menge.Contains(m.Id))
        MeldeAenderung()
    End Sub

    Public Sub AlleEntfernen()
        Entfernen(Eingabe.Schueler.Select(Function(s) s.Id).ToList())
    End Sub

    ''' <summary>Regeln, die kein Kind mehr betreffen: Gruppen ohne
    ''' Mitglied, Balancen auf einen Wert, den niemand traegt. Sie
    ''' bleiben nach dem Entfernen stehen, bis der Nutzer entscheidet -
    ''' der Kern lehnt sie beim Rechnen ab, still verschwinden sollen sie
    ''' aber nicht.</summary>
    Public Function LeereRegeln() As List(Of String)
        Dim liste As New List(Of String)
        For Each g In Eingabe.Gruppen.Where(Function(x) x.Mitglieder.Count = 0)
            liste.Add($"Gruppe „{g.Id}"": kein Mitglied mehr")
        Next
        For Each b In Eingabe.Balance.Where(Function(x) Not HatTraeger(x))
            liste.Add($"Balance „{b.Attribut} = {b.Wert}"": kein Kind trägt diesen Wert mehr")
        Next
        Return liste
    End Function

    Public Function LeereRegelnEntfernen() As Integer
        Dim n = Eingabe.Gruppen.RemoveAll(Function(g) g.Mitglieder.Count = 0) +
                Eingabe.Balance.RemoveAll(Function(b) Not HatTraeger(b))
        If n > 0 Then MeldeAenderung()
        Return n
    End Function

    Private Function HatTraeger(b As KlassenbildungBalance) As Boolean
        If b.Attribut Is Nothing OrElse b.Wert Is Nothing Then Return False
        Return Eingabe.Schueler.Any(
            Function(s) s.Attribute.ContainsKey(b.Attribut) AndAlso s.Attribute(b.Attribut) = b.Wert)
    End Function

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
        ' Ein frisches Projekt steht auf 0/0/0 - nach dem Import ist die
        ' Kinderzahl bekannt, also gibt es keinen Grund, den Rahmen raten
        ' zu lassen. Gesetzte Felder bleiben unangetastet.
        Dim vorschlag = RahmenVorschlagen()
        If vorschlag IsNot Nothing Then
            bericht.Hinweise.Add(vorschlag.Hinweistext(_projekt.Bestand.Schulart, _projekt.Bestand.Bundesland))
        End If
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
            ElseIf Not HatTraeger(b) Then
                fehler.Add($"Balance auf „{b.Attribut} = {b.Wert}"": kein Kind traegt diesen Wert - die Regel bliebe wirkungslos.")
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
