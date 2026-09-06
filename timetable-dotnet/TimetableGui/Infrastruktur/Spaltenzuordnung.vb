' Freie Spalten-Zuordnung beim Import (gui-ui-konzept.md 9.1) - Stufe G5.
'
' Bisher galt: Nachname, Vorname, und ALLES UEBRIGE wird Attribut. Das
' war fuer den Zwischenablage-Weg (F4) vertretbar, widerspricht aber dem
' Konzept an zwei Stellen:
'
'   * "nicht zugeordnete Spalten werden verworfen (mit Hinweis -
'     Datenminimierung als Default)". Alles blind zu uebernehmen ist das
'     Gegenteil davon: eine Klassenliste aus dem Sekretariat traegt
'     Telefonnummern und Geburtsdaten, die hier nichts zu suchen haben.
'   * Eine Spalte kann auch GRUPPEN erzeugen ("Religion" mit den Werten
'     ev/kath/ethik erzeugt die drei Gruppen und verteilt die Kinder")
'     oder eine bestehende Einteilung als Fixierung uebernehmen (9.3).
'
' Deshalb entscheidet der Nutzer je Spalte, und die Vorgabe ist
' VERWERFEN - nicht "uebernehmen, wird schon passen".
Imports TimetableCore
Imports TimetableProjekt

''' <summary>Was mit einer Spalte geschehen soll.</summary>
Public Enum Spaltenrolle
    ''' <summary>Die Vorgabe. Datenminimierung heisst: was niemand
    ''' ausdruecklich haben will, kommt nicht herein.</summary>
    Verwerfen
    Nachname
    Vorname
    ''' <summary>Freies Vokabular; der Spaltenname wird der Schluessel.
    ''' Balance-Regeln (6.11) greifen darauf zu.</summary>
    Attribut
    ''' <summary>Je vorkommendem Wert eine Gruppe, das Kind wird
    ''' Mitglied.</summary>
    Gruppe
    ''' <summary>Eine bestehende Einteilung: die Zahl in der Spalte ist
    ''' die Klassennummer, das Kind wird darauf fixiert (9.3).</summary>
    Klasse
End Enum

Public NotInheritable Class Spaltenwahl
    Public Property Name As String = ""
    Public Property Rolle As Spaltenrolle = Spaltenrolle.Verwerfen
    ''' <summary>Nur bei `Gruppe`: `buendelung` haelt die Kinder
    ''' zusammen, `verteilung` verteilt sie ueber die Klassen.</summary>
    Public Property Gruppentyp As String = "verteilung"

    ''' <summary>Wie das Attribut bzw. die Gruppenfamilie im Bestand
    ''' HEISSEN soll. Leer bedeutet: wie die Spalte.
    '''
    ''' Ohne dieses Feld war der Spaltenname zwingend der Schluessel -
    ''' und eine Liste mit der Spalte `Geschlecht` erzeugte neben einem
    ''' vorhandenen `geschlecht` ein ZWEITES Attribut. Balance-Regeln auf
    ''' dem alten griffen bei den importierten Kindern dann nicht mehr,
    ''' ohne dass irgendetwas darauf hingewiesen haette.</summary>
    Public Property Zielname As String = ""

    ''' <summary>Der tatsaechlich verwendete Name.</summary>
    Public ReadOnly Property Schluessel As String
        Get
            Return If(String.IsNullOrWhiteSpace(Zielname), Name, Zielname.Trim())
        End Get
    End Property

    ''' <summary>Nur bei `Attribut`: aus den Werten der Spalte zusaetzlich
    ''' eine Regel ableiten - eine Balance bei zwei Auspraegungen (ja/nein,
    ''' m/w), Buendelungsgruppen bei einer Werteliste (Kitas). Was genau,
    ''' entscheidet `Spaltenzuordnung.Ableitung` aus den Werten; der
    ''' Dialog zeigt es vorher an.</summary>
    Public Property RegelAbleiten As Boolean = False
End Class

''' <summary>Was aus den Werten einer Attributspalte an Regeln folgt.
''' `Art` ist "balance", "buendelung" oder "keine".</summary>
Public NotInheritable Class Regelableitung
    Public Property Art As String = "keine"
    ''' <summary>Die verschiedenen, nicht-leeren Werte, haeufigster zuerst.</summary>
    Public Property Werte As New List(Of String)
    ''' <summary>Bei "balance": der Wert, dessen Traeger gleichmaessig
    ''' verteilt werden.</summary>
    Public Property BalanceWert As String = ""

    ''' <summary>Ein Satz fuer den Dialog - was der Haken bewirkt.</summary>
    Public Function Beschreibung() As String
        Select Case Art
            Case "balance"
                Return $"Balance auf „{BalanceWert}"""
            Case "buendelung"
                Dim probe = String.Join(", ", Werte.Take(4)) & If(Werte.Count > 4, ", …", "")
                Return $"{Werte.Count} Bündelungen ({probe})"
            Case Else
                Return "keine Regel ableitbar"
        End Select
    End Function
End Class

''' <summary>Was der Import getan hat - im Klartext, weil ein Import,
''' der nur eine Zahl meldet, nicht pruefbar ist.</summary>
Public NotInheritable Class Importbericht
    Public Property Kinder As Integer
    Public Property Gruppen As New List(Of String)
    ''' <summary>Abgeleitete Balance-Regeln als "attribut = wert".</summary>
    Public Property Balance As New List(Of String)
    Public Property Fixierungen As Integer
    Public Property Verworfen As New List(Of String)
    Public Property Hinweise As New List(Of String)

    Public Function Klartext() As String
        Dim zeilen As New List(Of String) From {$"{Kinder} Kind(er) übernommen."}
        If Gruppen.Count > 0 Then zeilen.Add($"{Gruppen.Count} Gruppe(n) angelegt: {String.Join(", ", Gruppen)}.")
        If Balance.Count > 0 Then zeilen.Add($"{Balance.Count} Balance-Regel(n) angelegt: {String.Join(", ", Balance)}.")
        If Fixierungen > 0 Then zeilen.Add($"{Fixierungen} Fixierung(en) aus der bestehenden Einteilung.")
        If Verworfen.Count > 0 Then
            zeilen.Add($"Nicht übernommen: {String.Join(", ", Verworfen)}.")
        End If
        zeilen.AddRange(Hinweise)
        Return String.Join(vbLf, zeilen)
    End Function
End Class

Public Module Spaltenzuordnung

    ''' <summary>Vorschlag fuer die Zuordnung anhand der Spaltennamen.
    ''' Bewusst ZURUECKHALTEND: erkannt werden nur Name und Klasse, alles
    ''' andere bleibt auf Verwerfen. Ein Vorschlag, der freimuetig
    ''' uebernimmt, hebelt die Datenminimierung aus, ohne dass es jemand
    ''' merkt.</summary>
    Public Function Vorschlag(spalten As IEnumerable(Of String)) As List(Of Spaltenwahl)
        Dim ergebnis As New List(Of Spaltenwahl)
        Dim nachnameVergeben = False, vornameVergeben = False, klasseVergeben = False
        For Each name In spalten
            Dim w As New Spaltenwahl With {.Name = name}
            Dim k = If(name, "").Trim().ToLowerInvariant()
            If Not nachnameVergeben AndAlso (k = "nachname" OrElse k = "name" OrElse k = "familienname") Then
                w.Rolle = Spaltenrolle.Nachname
                nachnameVergeben = True
            ElseIf Not vornameVergeben AndAlso (k = "vorname" OrElse k = "rufname") Then
                w.Rolle = Spaltenrolle.Vorname
                vornameVergeben = True
            ElseIf Not klasseVergeben AndAlso (k = "klasse" OrElse k = "klassenzuordnung") Then
                w.Rolle = Spaltenrolle.Klasse
                klasseVergeben = True
            End If
            ergebnis.Add(w)
        Next
        Return ergebnis
    End Function

    ''' <summary>Was der Zuordnung noch fehlt. `vorhandene` ist das
    ''' Attribut-Vokabular des Projekts - damit faellt der gefaehrlichste
    ''' Fall auf: ein Ziel, das sich von einem vorhandenen NUR IN DER
    ''' SCHREIBWEISE unterscheidet.</summary>
    Public Function Einwaende(wahlen As IEnumerable(Of Spaltenwahl),
                              Optional vorhandene As IEnumerable(Of String) = Nothing) As List(Of String)
        Dim liste = wahlen.ToList()
        Dim fehler As New List(Of String)

        For Each rolle In {Spaltenrolle.Nachname, Spaltenrolle.Vorname, Spaltenrolle.Klasse}
            Dim r = rolle
            Dim n = liste.Where(Function(w) w.Rolle = r).Count
            If n > 1 Then fehler.Add($"{r} ist {n}-mal vergeben – diese Rolle gibt es nur einmal.")
        Next

        If Not liste.Any(Function(w) w.Rolle = Spaltenrolle.Nachname OrElse w.Rolle = Spaltenrolle.Vorname) Then
            ' Ohne Namen entstehen Kinder ohne Klarnamen-Eintrag. Das ist
            ' zulaessig (Platzhalter), aber sicher nicht gemeint, wenn
            ' jemand eine Klassenliste importiert.
            fehler.Add("Weder Nachname noch Vorname zugeordnet – die Kinder blieben namenlos.")
        End If

        Dim doppelt = liste.Where(Function(w) w.Rolle = Spaltenrolle.Attribut OrElse w.Rolle = Spaltenrolle.Gruppe).
            GroupBy(Function(w) w.Schluessel, StringComparer.CurrentCultureIgnoreCase).
            Where(Function(g) g.Count() > 1).Select(Function(g) g.Key).ToList()
        For Each name In doppelt
            fehler.Add($"Zwei Spalten heißen '{name}' – Attribut- und Gruppennamen müssen eindeutig sein.")
        Next
        ' Der Fall, der im manuellen Test zuschlug: die Spalte heisst
        ' Geschlecht, das Projekt fuehrt geschlecht. Ohne Hinweis
        ' entstuende ein zweites Attribut, und Balance-Regeln auf dem
        ' alten griffen bei den importierten Kindern nicht mehr.
        If vorhandene IsNot Nothing Then
            For Each w In liste.Where(Function(x) x.Rolle = Spaltenrolle.Attribut)
                Dim ziel = w.Schluessel
                Dim aehnlich = vorhandene.FirstOrDefault(
                    Function(v) Not String.Equals(v, ziel, StringComparison.Ordinal) AndAlso
                                String.Equals(v, ziel, StringComparison.CurrentCultureIgnoreCase))
                If aehnlich IsNot Nothing Then
                    fehler.Add($"Das Projekt führt bereits das Attribut {aehnlich}. " &
                               $"{ziel} unterscheidet sich nur in der Schreibweise und " &
                               "ergäbe ein zweites, an dem bestehende Regeln vorbeigreifen.")
                End If
            Next
        End If
        Return fehler
    End Function

    ''' <summary>Werte, die "ja" heissen - bei einer Balance auf ja/nein
    ''' sind die Ja-Kinder die, die man verteilen will, egal ob sie die
    ''' Mehrheit stellen.</summary>
    Private ReadOnly JaWerte As New HashSet(Of String)(
        {"ja", "j", "yes", "y", "x", "true", "wahr", "1"}, StringComparer.OrdinalIgnoreCase)

    ''' <summary>Leitet aus den Werten einer Attributspalte ab, welche
    ''' Regel dazu passt. Ein Wert je Kind; leere Werte zaehlen als
    ''' "Kind traegt das Attribut nicht" - so, wie der Import sie auch
    ''' nicht speichert.
    '''
    ''' Zwei Auspraegungen oder weniger (ja/nein, m/w, oder nur "ja" neben
    ''' Leerfeldern) sind typisch binaer: EINE Balance-Regel, auf dem
    ''' Ja-Wert, sonst auf dem selteneren - der ist der, dessen Verteilung
    ''' man steuert; die Gegenseite folgt aus der Klassengroesse. Drei und
    ''' mehr sind eine Werteliste (Kitas, Wohngebiete): je Wert eine
    ''' Buendelung. Tragen ALLE Kinder denselben einen Wert, ist nichts zu
    ''' balancieren.</summary>
    Public Function Ableitung(werte As IEnumerable(Of String)) As Regelableitung
        Dim alle = If(werte, Enumerable.Empty(Of String)()).Select(Function(w) If(w, "").Trim()).ToList()
        Dim haeufigkeit = alle.Where(Function(w) w <> "").
            GroupBy(Function(w) w, StringComparer.Ordinal).
            OrderByDescending(Function(g) g.Count()).
            ThenBy(Function(g) g.Key, StringComparer.CurrentCultureIgnoreCase).
            ToList()
        Dim a As New Regelableitung With {.Werte = haeufigkeit.Select(Function(g) g.Key).ToList()}

        If haeufigkeit.Count = 0 Then Return a
        If haeufigkeit.Count >= 3 Then
            a.Art = "buendelung"
            Return a
        End If
        If haeufigkeit.Count = 1 AndAlso haeufigkeit(0).Count() = alle.Count Then Return a

        Dim ja = haeufigkeit.FirstOrDefault(Function(g) JaWerte.Contains(g.Key))
        a.BalanceWert = If(ja IsNot Nothing, ja.Key, haeufigkeit.Last().Key)
        a.Art = "balance"
        Return a
    End Function

    ''' <summary>Fuehrt den Import aus. `zeilen` enthaelt die Kopfzeile
    ''' NICHT mehr.
    '''
    ''' Die Reihenfolge ist wichtig: erst alle Kinder anlegen (sie
    ''' bekommen dabei ihre Id), dann Gruppen und Fixierungen - beide
    ''' verweisen auf die Ids.</summary>
    Public Function Uebernehmen(projekt As Projekt, zeilen As List(Of String()),
                                wahlen As List(Of Spaltenwahl),
                                neuesKind As Func(Of String, String, Dictionary(Of String, String), String)) As Importbericht
        Dim bericht As New Importbericht
        If projekt Is Nothing OrElse zeilen Is Nothing OrElse wahlen Is Nothing Then Return bericht

        bericht.Verworfen = wahlen.Where(Function(w) w.Rolle = Spaltenrolle.Verwerfen).
            Select(Function(w) w.Name).ToList()

        Dim nachnameSpalte = Index(wahlen, Spaltenrolle.Nachname)
        Dim vornameSpalte = Index(wahlen, Spaltenrolle.Vorname)
        Dim klasseSpalte = Index(wahlen, Spaltenrolle.Klasse)

        ' Gruppenname -> Kinder-Ids. Erst am Ende in Gruppen umgesetzt,
        ' weil die Ids beim Anlegen der Kinder entstehen.
        Dim gruppen As New Dictionary(Of String, List(Of String))(StringComparer.CurrentCultureIgnoreCase)
        Dim gruppentypen As New Dictionary(Of String, String)(StringComparer.CurrentCultureIgnoreCase)
        Dim ohneKlassenzahl = 0

        ' Attributspalten mit Regelableitung: je Spalte die Werte aller
        ' Kinder mit deren Id - erst am Ende zur Regel gemacht, weil die
        ' Ableitung die ganze Spalte sehen muss, nicht eine Zeile.
        Dim abzuleiten = wahlen.Where(Function(w) w.Rolle = Spaltenrolle.Attribut AndAlso w.RegelAbleiten).ToList()
        Dim spaltenwerte = abzuleiten.ToDictionary(
            Function(w) w.Schluessel, Function(w) New List(Of (Id As String, Wert As String)), StringComparer.Ordinal)

        For Each zeile In zeilen
            Dim attribute As New Dictionary(Of String, String)(StringComparer.Ordinal)
            For i = 0 To Math.Min(zeile.Length, wahlen.Count) - 1
                If wahlen(i).Rolle = Spaltenrolle.Attribut AndAlso zeile(i) <> "" Then
                    attribute(wahlen(i).Schluessel) = zeile(i)
                End If
            Next

            Dim nach = Feld(zeile, nachnameSpalte)
            Dim vor = Feld(zeile, vornameSpalte)
            ' Eine vollstaendig leere Zeile ist kein Kind.
            If nach = "" AndAlso vor = "" AndAlso attribute.Count = 0 Then Continue For

            Dim id = neuesKind(nach, vor, attribute)
            bericht.Kinder += 1

            For Each w In abzuleiten
                Dim wert As String = Nothing
                ' Getrimmt wie beim Anlegen des Kindes - die Balance
                ' vergleicht spaeter mit dem gespeicherten Wert.
                spaltenwerte(w.Schluessel).Add((id, If(attribute.TryGetValue(w.Schluessel, wert), wert.Trim(), "")))
            Next

            For i = 0 To Math.Min(zeile.Length, wahlen.Count) - 1
                If wahlen(i).Rolle <> Spaltenrolle.Gruppe OrElse zeile(i) = "" Then Continue For
                ' "Religion" + "ev" ergibt "Religion-ev" - der Spaltenname
                ' allein waere mehrdeutig, der Wert allein verlöre den
                ' Zusammenhang.
                Dim name = $"{wahlen(i).Schluessel}-{zeile(i)}"
                If Not gruppen.ContainsKey(name) Then
                    gruppen(name) = New List(Of String)
                    gruppentypen(name) = wahlen(i).Gruppentyp
                End If
                gruppen(name).Add(id)
            Next

            If klasseSpalte >= 0 Then
                Dim nummer = Klassennummer(projekt, Feld(zeile, klasseSpalte))
                If nummer.HasValue Then
                    projekt.Klassenbildung.Fixierungen.Add(
                        New KlassenbildungFixierung With {.Kind = id, .Klasse = nummer.Value})
                    bericht.Fixierungen += 1
                ElseIf Feld(zeile, klasseSpalte).Trim() <> "" Then
                    ohneKlassenzahl += 1
                End If
            End If
        Next

        For Each paar In gruppen.OrderBy(Function(p) p.Key, StringComparer.CurrentCultureIgnoreCase)
            projekt.Klassenbildung.Gruppen.Add(New KlassenbildungGruppe With {
                .Id = paar.Key, .Typ = gruppentypen(paar.Key),
                .Modus = "soft", .Prio = 2,
                .Mitglieder = paar.Value})
            bericht.Gruppen.Add(paar.Key)
        Next

        For Each w In abzuleiten
            RegelUmsetzen(projekt,w.Schluessel, spaltenwerte(w.Schluessel), bericht)
        Next

        If ohneKlassenzahl > 0 Then
            ' Nicht still schlucken: eine Klasse, die weder Zahl noch
            ' Label ist, kommt vor - und wer es nicht erfaehrt, sucht
            ' spaeter nach fehlenden Fixierungen.
            bericht.Hinweise.Add($"{ohneKlassenzahl} Zeile(n) mit einer Klassenangabe, die weder eine " &
                                 "Zahl noch eines der Klassen-Labels ist – dort entstand keine Fixierung.")
        End If
        Return bericht
    End Function

    ''' <summary>Setzt die Ableitung fuer eine Spalte um. Eine Regel, die
    ''' es schon gibt, wird nicht ein zweites Mal angelegt - der Bericht
    ''' nennt sie stattdessen; sonst laege nach zwei Importen derselben
    ''' Liste alles doppelt. Buendelungen heissen `attribut_wert`.</summary>
    Private Sub RegelUmsetzen(projekt As Projekt, attribut As String,
                              kinder As List(Of (Id As String, Wert As String)),
                              bericht As Importbericht)
        Dim a = Ableitung(kinder.Select(Function(k) k.Wert))
        Dim kb = projekt.Klassenbildung
        Select Case a.Art
            Case "balance"
                Dim schonDa = kb.Balance.Any(
                    Function(b) String.Equals(b.Attribut, attribut, StringComparison.Ordinal) AndAlso
                                String.Equals(b.Wert, a.BalanceWert, StringComparison.Ordinal))
                If schonDa Then
                    bericht.Hinweise.Add($"Balance {attribut} = {a.BalanceWert} gab es schon – nicht erneut angelegt.")
                Else
                    kb.Balance.Add(New KlassenbildungBalance With {
                        .Attribut = attribut, .Wert = a.BalanceWert, .Toleranz = 1,
                        .Modus = "soft", .Prio = 2})
                    bericht.Balance.Add($"{attribut} = {a.BalanceWert}")
                End If
            Case "buendelung"
                For Each wert In a.Werte
                    Dim name = $"{attribut}_{wert}"
                    If kb.Gruppen.Any(Function(g) String.Equals(g.Id, name, StringComparison.CurrentCultureIgnoreCase)) Then
                        bericht.Hinweise.Add($"Gruppe {name} gab es schon – nicht erneut angelegt.")
                        Continue For
                    End If
                    Dim w = wert
                    kb.Gruppen.Add(New KlassenbildungGruppe With {
                        .Id = name, .Typ = "buendelung", .Modus = "soft", .Prio = 2,
                        .Mitglieder = kinder.Where(Function(k) k.Wert = w).Select(Function(k) k.Id).ToList()})
                    bericht.Gruppen.Add(name)
                Next
            Case Else
                bericht.Hinweise.Add($"Aus {attribut} ließ sich keine Regel ableiten – " &
                                     If(a.Werte.Count = 0, "die Spalte ist leer.", "alle Kinder tragen denselben Wert."))
        End Select
    End Sub

    ''' <summary>Die Klassennummer aus einer Zellenangabe. Echte Listen
    ''' schreiben nicht 1, sondern 5a - deshalb wird auch gegen die
    ''' LABELS der Klassen geprueft. Ohne das verlangte die Oberflaeche
    ''' vom Nutzer, seine Datei vorher umzuschreiben, und genau das soll
    ''' sie ihm ersparen.
    '''
    ''' Nothing heisst: nicht zuzuordnen - der Aufrufer meldet es.</summary>
    Private Function Klassennummer(projekt As Projekt, wert As String) As Integer?
        Dim w = If(wert, "").Trim()
        If w = "" Then Return Nothing

        Dim n As Integer
        If Integer.TryParse(w, n) AndAlso n >= 1 Then Return n

        Dim labels = projekt.Klassenbildung.Klassen.Labels
        If labels IsNot Nothing Then
            Dim i = labels.FindIndex(Function(l) String.Equals(l, w, StringComparison.CurrentCultureIgnoreCase))
            If i >= 0 Then Return i + 1
        End If
        Return Nothing
    End Function

    Private Function Index(wahlen As List(Of Spaltenwahl), rolle As Spaltenrolle) As Integer
        Return wahlen.FindIndex(Function(w) w.Rolle = rolle)
    End Function

    Private Function Feld(zeile As String(), index As Integer) As String
        If index < 0 OrElse index >= zeile.Length Then Return ""
        Return zeile(index)
    End Function

End Module
