' Code-Behind der Klassenbildungs-Eingaben. Nur Verdrahtung; alle
' Entscheidungen liegen in KlassenbildungEingabeViewModel und den
' Regel-ViewModels und sind dort ohne Fenster geprueft.
Imports System.Windows.Media
Imports TimetableCore
Imports TimetableProjekt

Partial Class KlassenbildungFenster

    Private ReadOnly _projekt As Projekt
    Private ReadOnly _dialoge As IDialoge
    Private ReadOnly _eingabe As KlassenbildungEingabeViewModel
    Private ReadOnly _gruppen As KbGruppenViewModel
    Private ReadOnly _balance As KbBalanceViewModel
    Private ReadOnly _wuensche As KbWuenscheViewModel
    Private ReadOnly _fixierungen As KbFixierungenViewModel
    Private _fuellt As Boolean

    Public Event Geaendert As EventHandler

    Public Sub New(projekt As Projekt, dialoge As IDialoge,
                   Optional speicherung As ISpeicherung = Nothing)
        InitializeComponent()
        _projekt = projekt
        _dialoge = dialoge
        _eingabe = New KlassenbildungEingabeViewModel(projekt, dialoge)
        _gruppen = New KbGruppenViewModel(projekt, dialoge, _eingabe)
        _balance = New KbBalanceViewModel(projekt, dialoge, _eingabe)
        _wuensche = New KbWuenscheViewModel(projekt, dialoge, _eingabe)
        _fixierungen = New KbFixierungenViewModel(projekt, dialoge, _eingabe)

        AddHandler _eingabe.Geaendert, Sub() RaiseEvent Geaendert(Me, EventArgs.Empty)
        AddHandler _gruppen.Geaendert, AddressOf AufRegelAenderung
        AddHandler _balance.Geaendert, AddressOf AufRegelAenderung
        AddHandler _wuensche.Geaendert, AddressOf AufRegelAenderung
        AddHandler _fixierungen.Geaendert, AddressOf AufRegelAenderung

        Fuellen()
        RegelmaskenFuellen()
        SpeicherungVerdrahten(speicherung)
    End Sub

    ' ===============================================================
    ' Anzeige
    ' ===============================================================

    Private Sub Fuellen()
        _fuellt = True
        Try
            Anzahl.Text = _eingabe.Anzahl.ToString()
            MinGroesse.Text = _eingabe.MinGroesse.ToString()
            MaxGroesse.Text = _eingabe.MaxGroesse.ToString()
            Stufe.Text = If(_eingabe.Stufe?.ToString(), "")
            Labels.Text = If(_eingabe.Labels Is Nothing, "", String.Join(", ", _eingabe.Labels))

            KinderFuellen()
            Aktualisieren()
        Finally
            _fuellt = False
        End Try
    End Sub

    Private Sub KinderFuellen()
        Dim gewaehlt = TryCast(Kinder.SelectedItem, String)
        Kinder.Items.Clear()
        For Each k In _eingabe.Schueler
            ' Klarname NUR in der Anzeige (Konzept 1) - die Liste selbst
            ' haelt ihn nicht.
            Dim beschreibung = _eingabe.Anzeigename(k.Id)
            If k.Attribute.Count > 0 Then
                beschreibung &= "   " & String.Join(", ", k.Attribute.Select(Function(a) $"{a.Key}={a.Value}"))
            End If
            Kinder.Items.Add(beschreibung)
        Next
        If gewaehlt IsNot Nothing AndAlso Kinder.Items.Contains(gewaehlt) Then Kinder.SelectedItem = gewaehlt
    End Sub

    Private Sub Aktualisieren()
        Vorschau.Text = "Klassen: " & _eingabe.LabelVorschau
        Rahmenzeile.Text = _eingabe.RahmenZeile
        Anzahlzeile.Text = $"{_eingabe.Schueler.Count} Kinder"
        Dim n = Pruefbefunde().Count
        Statuszeile.Text = If(n = 0, "Prüfung ohne Beanstandung.", $"{n} Hinweis(e) - Prüfen zeigt sie.")
    End Sub

    ' ===============================================================
    ' Klassenrahmen
    ' ===============================================================

    Private Function Ganz(t As TextBox, rueckfall As Integer) As Integer
        Dim n As Integer
        If Integer.TryParse(t.Text.Trim(), n) Then Return n
        t.Text = rueckfall.ToString()
        Return rueckfall
    End Function

    Private Sub AufRahmen(sender As Object, e As RoutedEventArgs)
        If _fuellt Then Return
        _eingabe.Anzahl = Ganz(Anzahl, _eingabe.Anzahl)
        _eingabe.MinGroesse = Ganz(MinGroesse, _eingabe.MinGroesse)
        _eingabe.MaxGroesse = Ganz(MaxGroesse, _eingabe.MaxGroesse)

        ' Stufe ODER Labels - wer beides fuellt, bekommt die Labels; sie
        ' sind die konkretere Angabe.
        Dim rohLabels = Labels.Text.Split(","c).Select(Function(x) x.Trim()).Where(Function(x) x <> "").ToList()
        If rohLabels.Count > 0 Then
            _eingabe.Labels = rohLabels
            Stufe.Text = ""
        Else
            _eingabe.Labels = Nothing
            Dim s As Integer
            _eingabe.Stufe = If(Integer.TryParse(Stufe.Text.Trim(), s), CType(s, Integer?), Nothing)
        End If
        Aktualisieren()
    End Sub

    ' ===============================================================
    ' Zwischenablage-Import
    ' ===============================================================

    Private Sub AufEinfuegen(sender As Object, e As RoutedEventArgs)
        Dim text As String = Nothing
        Try
            If Clipboard.ContainsText() Then text = Clipboard.GetText()
        Catch
            ' Die Zwischenablage kann von einem anderen Programm belegt
            ' sein - das ist kein Absturzgrund.
        End Try
        If String.IsNullOrWhiteSpace(text) Then
            _dialoge.Hinweis("Einfügen", "In der Zwischenablage steht kein Text.")
            Return
        End If

        Dim v = _eingabe.ImportPruefen(text)
        If v.Datensaetze = 0 Then
            _dialoge.Hinweis("Einfügen", "Aus dem Text ließ sich keine Zeile lesen.")
            Return
        End If

        ImportZeigen(v)
    End Sub

    ''' <summary>Der zweite Weg aus 9.1: eine CSV-Datei. Er braucht
    ''' einen EIGENEN Knopf - bisher sass er im Dialog, und den oeffnete
    ''' nur, wer schon brauchbaren Text in der Zwischenablage hatte. Wer
    ''' eine Datei importieren wollte, musste also erst etwas ganz
    ''' anderes kopieren.</summary>
    Private Sub AufCsvDatei(sender As Object, e As RoutedEventArgs)
        Dim pfad = _dialoge.DateiOeffnen("CSV-Datei wählen",
                                         "CSV-Datei (*.csv;*.txt)|*.csv;*.txt|Alle Dateien (*.*)|*.*")
        If pfad Is Nothing Then Return
        Dim text As String
        Try
            text = ImportDialog.DateiLesen(pfad)
        Catch ex As IO.IOException
            _dialoge.Hinweis("Datei nicht lesbar", ex.Message)
            Return
        Catch ex As UnauthorizedAccessException
            _dialoge.Hinweis("Datei nicht lesbar", ex.Message)
            Return
        End Try

        Dim v = _eingabe.ImportPruefen(text)
        If v.Datensaetze = 0 Then
            _dialoge.Hinweis("Import", "Aus der Datei ließ sich keine Zeile lesen.")
            Return
        End If
        ImportZeigen(v)
    End Sub

    ''' <summary>Beide Wege muenden hier - Zwischenablage und Datei
    ''' unterscheiden sich nur in der Herkunft des Textes.</summary>
    Private Sub ImportZeigen(v As KlassenbildungEingabeViewModel.ImportVorschau)
        Dim d As New ImportDialog(v, _dialoge, AddressOf _eingabe.ImportPruefen,
                                  _eingabe.Attributnamen) With {.Owner = Me}
        If d.ShowDialog() <> True Then Return

        Dim bericht = _eingabe.ImportUebernehmen(d.Vorschau, d.Wahlen)
        KinderFuellen()
        RegelmaskenFuellen()
        Aktualisieren()
        ' Der Bericht im Klartext, nicht nur eine Zahl: ein Import, der
        ' still Spalten verwirft und Gruppen anlegt, ist sonst nicht
        ' nachvollziehbar.
        _dialoge.Hinweis("Import", bericht.Klartext())
    End Sub

    Private Sub AufEntfernen(sender As Object, e As RoutedEventArgs)
        Dim i = Kinder.SelectedIndex
        If i < 0 OrElse i >= _eingabe.Schueler.Count Then Return
        Dim kind = _eingabe.Schueler(i)
        If Not _dialoge.Frage("Entfernen",
            $"„{_eingabe.Anzeigename(kind.Id)}"" entfernen? Gruppenmitgliedschaften, Wünsche und Fixierungen dieses Kindes verschwinden mit.") Then Return
        _eingabe.Entfernen(kind.Id)
        KinderFuellen()
        Aktualisieren()
    End Sub

    ' ===============================================================
    ' Regeln: Gruppen, Balance, Wuensche, Fixierungen (Stufe F6)
    ' ===============================================================
    '
    ' Vier Listenmasken nach dem Grundmuster. Bewusst KEIN Erbauer wie
    ' bei den Regeln des Stundenplans (F3): dort waren acht fast gleiche
    ' Masken zu bauen, hier sind es vier deutlich verschiedene - ein
    ' Erbauer haette mehr Fallunterscheidungen als gespartes XAML.

    Private Sub AufRegelAenderung(sender As Object, e As EventArgs)
        RaiseEvent Geaendert(Me, EventArgs.Empty)
    End Sub

    Private Sub RegelmaskenFuellen()
        _fuellt = True
        Try
            Fuelle(GruppeTyp, KbGruppenViewModel.Typen)
            Fuelle(WunschTyp, KbWuenscheViewModel.Typen)
            For Each feld In {GruppeModus, BalanceModus, WunschModus}
                Fuelle(feld, {"soft", "hard"})
            Next
            For Each feld In {GruppePrio, BalancePrio, WunschPrio}
                Fuelle(feld, {"1", "2", "3"})
            Next
            Fuelle(BalanceAttribut, _balance.Attributnamen())
            KinderFuellen(WunschKindA)
            KinderFuellen(WunschKindB)
        Finally
            _fuellt = False
        End Try
        GruppenListeFuellen()
        BalanceListeFuellen()
        WunschListeFuellen()
        FixListeFuellen()
    End Sub

    Private Shared Sub Fuelle(feld As ComboBox, werte As IEnumerable(Of String))
        feld.Items.Clear()
        For Each w In werte
            feld.Items.Add(w)
        Next
    End Sub

    ''' <summary>Kinder erscheinen als Klarname, gespeichert wird die Id.
    ''' Der Eintrag traegt die Id deshalb als Tag - so bleibt die Anzeige
    ''' frei waehlbar, ohne dass jemand den Namen zurueckuebersetzen
    ''' muesste.</summary>
    Private Sub KinderFuellen(feld As ComboBox)
        feld.Items.Clear()
        feld.Items.Add(New ComboBoxItem With {.Content = "(keins)", .Tag = Nothing})
        For Each k In _gruppen.AlleKinder()
            feld.Items.Add(New ComboBoxItem With {.Content = _gruppen.Anzeige(k.Id), .Tag = k.Id})
        Next
    End Sub

    Private Shared Sub WaehleKind(feld As ComboBox, id As String)
        For Each eintrag As ComboBoxItem In feld.Items
            If CStr(If(eintrag.Tag, "")) = If(id, "") Then
                feld.SelectedItem = eintrag
                Return
            End If
        Next
        feld.SelectedIndex = 0
    End Sub

    Private Shared Function GewaehltesKind(feld As ComboBox) As String
        Dim eintrag = TryCast(feld.SelectedItem, ComboBoxItem)
        If eintrag Is Nothing Then Return Nothing
        Return TryCast(eintrag.Tag, String)
    End Function

    ' ---------------------------------------------------------------
    ' Gruppen
    ' ---------------------------------------------------------------

    Private Sub GruppenListeFuellen()
        _gruppen.Aktualisiere()
        ' Die Auswahl VOR dem Neufuellen merken - ueber Gewaehlt, nicht
        ' per TryCast des Listeneintrags: der ist ein Zeilenpaar, der Cast
        ' ergab immer Nothing, und die Liste sprang nach jedem Haken auf
        ' die erste Gruppe zurueck (live gemeldet 06.09.2026).
        Dim vorher = Gewaehlt(Of KlassenbildungGruppe)(GruppenListe)
        GruppenListe.ItemsSource = _gruppen.Eintraege.
            Select(Function(g) New Zeilenpaar(g, _gruppen.Zeilentext(g))).ToList()
        Auswahl(GruppenListe, vorher)
        GruppeZeigen()
    End Sub

    Private Sub GruppeZeigen()
        Dim g = Gewaehlt(Of KlassenbildungGruppe)(GruppenListe)
        GruppeDetail.IsEnabled = g IsNot Nothing
        _fuellt = True
        Try
            GruppeId.Text = If(g?.Id, "")
            GruppeKuerzel.Text = If(g?.Kuerzel, "")
            GruppeTyp.SelectedItem = If(g?.Typ, "buendelung")
            GruppeMax.Text = If(g?.MaxProKlasse?.ToString(), "")
            GruppeMin.Text = If(g?.MinProKlasse?.ToString(), "")
            ' Die Kappe gehoert nur zur Verteilung, die Mindestzahl nur
            ' zur Buendelung - der Kern lehnt das jeweils andere ab. Das
            ' Feld ist dann gar nicht erst bedienbar.
            GruppeMax.IsEnabled = (g?.Typ = "verteilung")
            GruppeMin.IsEnabled = (g?.Typ = "buendelung")
            GruppeModus.SelectedItem = If(g?.Modus, "soft")
            GruppePrio.SelectedItem = If(g Is Nothing, "2", g.Prio.ToString())
        Finally
            _fuellt = False
        End Try
        MitgliederBauen()
    End Sub

    Private Sub MitgliederBauen()
        GruppeMitglieder.Children.Clear()
        Dim g = Gewaehlt(Of KlassenbildungGruppe)(GruppenListe)
        GruppeMitgliederKopf.Text = If(g Is Nothing, "Mitglieder",
                                       $"Mitglieder ({g.Mitglieder.Count} von {_gruppen.AlleKinder().Count})")
        If g Is Nothing Then Return

        Dim suche = GruppeSuche.Text.Trim()
        For Each kind In _gruppen.AlleKinder()
            Dim id = kind.Id
            Dim anzeige = _gruppen.Anzeige(id)
            ' Gefiltert wird die Anzeige, nicht die Id - gesucht wird nach
            ' dem Namen, den man kennt.
            If suche <> "" AndAlso anzeige.IndexOf(suche, StringComparison.CurrentCultureIgnoreCase) < 0 Then
                ' Bereits gewaehlte Kinder bleiben sichtbar, sonst
                ' verschwaende der Filter eine Mitgliedschaft aus dem Blick.
                If Not _gruppen.IstMitglied(g, id) Then Continue For
            End If
            Dim cb As New CheckBox With {
                .Content = anzeige, .Margin = New Thickness(0, 2, 0, 2),
                .IsChecked = _gruppen.IstMitglied(g, id)}
            AddHandler cb.Click, Sub()
                                     _gruppen.SetzeMitglied(g, id, cb.IsChecked = True)
                                     GruppenListeFuellen()
                                     Aktualisieren()
                                 End Sub
            GruppeMitglieder.Children.Add(cb)
        Next
    End Sub

    Private Sub AufGruppeNeu(sender As Object, e As RoutedEventArgs)
        _gruppen.Neu()
        GruppenListeFuellen()
        Auswahl(GruppenListe, _gruppen.Auswahl)
        GruppeZeigen()
        Aktualisieren()
    End Sub

    Private Sub AufGruppeDupl(sender As Object, e As RoutedEventArgs)
        _gruppen.Auswahl = Gewaehlt(Of KlassenbildungGruppe)(GruppenListe)
        _gruppen.Duplizieren()
        GruppenListeFuellen()
        Auswahl(GruppenListe, _gruppen.Auswahl)
        Aktualisieren()
    End Sub

    Private Sub AufGruppeWeg(sender As Object, e As RoutedEventArgs)
        _gruppen.Auswahl = Gewaehlt(Of KlassenbildungGruppe)(GruppenListe)
        _gruppen.Loeschen()
        GruppenListeFuellen()
        Aktualisieren()
    End Sub

    Private Sub AufGruppeAuswahl(sender As Object, e As SelectionChangedEventArgs)
        If _fuellt Then Return
        GruppeZeigen()
    End Sub

    Private Sub AufGruppeSuche(sender As Object, e As RoutedEventArgs)
        If _fuellt Then Return
        MitgliederBauen()
    End Sub

    Private Sub AufGruppeFeld(sender As Object, e As RoutedEventArgs)
        If _fuellt Then Return
        Dim g = Gewaehlt(Of KlassenbildungGruppe)(GruppenListe)
        If g Is Nothing Then Return
        g.Id = GruppeId.Text.Trim()
        g.Kuerzel = If(GruppeKuerzel.Text.Trim() = "", Nothing, GruppeKuerzel.Text.Trim())
        g.Typ = CStr(If(GruppeTyp.SelectedItem, "buendelung"))
        Dim n As Integer
        If g.Typ = "buendelung" Then
            ' Wechsel auf Buendelung raeumt eine frueher gesetzte Kappe weg -
            ' sonst bliebe sie unsichtbar stehen und bricht das Rechnen.
            g.MaxProKlasse = Nothing
            GruppeMax.Text = ""
            Dim mindest As Integer
            g.MinProKlasse = If(Integer.TryParse(GruppeMin.Text.Trim(), mindest), CType(mindest, Integer?), Nothing)
        Else
            g.MaxProKlasse = If(Integer.TryParse(GruppeMax.Text.Trim(), n), CType(n, Integer?), Nothing)
            ' Dasselbe umgekehrt: eine Verteilung kennt keine Mindestzahl.
            g.MinProKlasse = Nothing
            GruppeMin.Text = ""
        End If
        GruppeMax.IsEnabled = (g.Typ = "verteilung")
        GruppeMin.IsEnabled = (g.Typ = "buendelung")
        g.Modus = CStr(If(GruppeModus.SelectedItem, "soft"))
        g.Prio = Integer.Parse(CStr(If(GruppePrio.SelectedItem, "2")))
        GruppenListeFuellen()
        Aktualisieren()
    End Sub

    ' ---------------------------------------------------------------
    ' Balance
    ' ---------------------------------------------------------------

    Private Sub BalanceListeFuellen()
        _balance.Aktualisiere()
        Dim vorher = Gewaehlt(Of KlassenbildungBalance)(BalanceListe)
        BalanceListe.ItemsSource = _balance.Eintraege.
            Select(Function(b) New Zeilenpaar(b, _balance.Zeilentext(b))).ToList()
        Auswahl(BalanceListe, vorher)
        BalanceZeigen()
    End Sub

    Private Sub BalanceZeigen()
        Dim b = Gewaehlt(Of KlassenbildungBalance)(BalanceListe)
        BalanceDetail.IsEnabled = b IsNot Nothing
        _fuellt = True
        Try
            Fuelle(BalanceAttribut, _balance.Attributnamen())
            BalanceAttribut.SelectedItem = If(b?.Attribut, Nothing)
            Fuelle(BalanceWert, _balance.Werte(If(b?.Attribut, "")))
            BalanceWert.SelectedItem = If(b?.Wert, Nothing)
            BalanceToleranz.Text = If(b Is Nothing, "0", b.Toleranz.ToString())
            BalanceModus.SelectedItem = If(b?.Modus, "soft")
            BalancePrio.SelectedItem = If(b Is Nothing, "2", b.Prio.ToString())
        Finally
            _fuellt = False
        End Try
        BetroffeneZeigen()
    End Sub

    ''' <summary>Wieviele Kinder diese Balance ueberhaupt betrifft. Ohne
    ''' diese Zahl merkt niemand, dass eine Regel auf einen Wert zeigt,
    ''' den kein Kind mehr traegt - sie waere dann wirkungslos, aber
    ''' formal in Ordnung.</summary>
    Private Sub BetroffeneZeigen()
        Dim b = Gewaehlt(Of KlassenbildungBalance)(BalanceListe)
        If b Is Nothing OrElse b.Attribut Is Nothing OrElse b.Wert Is Nothing Then
            BalanceBetroffen.Text = ""
            Return
        End If
        Dim n = _projekt.Klassenbildung.Schueler.
            Where(Function(s) s.Attribute.ContainsKey(b.Attribut) AndAlso s.Attribute(b.Attribut) = b.Wert).Count
        BalanceBetroffen.Text = If(n = 0,
            "Kein Kind trägt diesen Wert – die Regel bliebe wirkungslos.",
            $"{n} Kind(er) tragen diesen Wert.")
    End Sub

    Private Sub AufBalanceNeu(sender As Object, e As RoutedEventArgs)
        _balance.Neu()
        BalanceListeFuellen()
        Auswahl(BalanceListe, _balance.Auswahl)
        Aktualisieren()
    End Sub

    Private Sub AufBalanceDupl(sender As Object, e As RoutedEventArgs)
        _balance.Auswahl = Gewaehlt(Of KlassenbildungBalance)(BalanceListe)
        _balance.Duplizieren()
        BalanceListeFuellen()
        Auswahl(BalanceListe, _balance.Auswahl)
        Aktualisieren()
    End Sub

    Private Sub AufBalanceWeg(sender As Object, e As RoutedEventArgs)
        _balance.Auswahl = Gewaehlt(Of KlassenbildungBalance)(BalanceListe)
        _balance.Loeschen()
        BalanceListeFuellen()
        Aktualisieren()
    End Sub

    Private Sub AufBalanceAuswahl(sender As Object, e As SelectionChangedEventArgs)
        If _fuellt Then Return
        BalanceZeigen()
    End Sub

    ''' <summary>Der Wert haengt am Attribut - nach einem Attributwechsel
    ''' muss die Werteliste neu gefuellt werden, sonst stuende dort ein
    ''' Wert aus einem anderen Vokabular.</summary>
    Private Sub AufBalanceAttribut(sender As Object, e As SelectionChangedEventArgs)
        If _fuellt Then Return
        Dim b = Gewaehlt(Of KlassenbildungBalance)(BalanceListe)
        If b Is Nothing Then Return
        b.Attribut = CStr(If(BalanceAttribut.SelectedItem, Nothing))
        b.Wert = Nothing
        ' Die Werteliste fuellt BalanceZeigen - hier stand dieselbe
        ' Zeile noch einmal. Der Negativtest hat sie entlarvt: sie
        ' abzuschalten aenderte nichts.
        BalanceListeFuellen()
        Aktualisieren()
    End Sub

    Private Sub AufBalanceFeld(sender As Object, e As RoutedEventArgs)
        If _fuellt Then Return
        Dim b = Gewaehlt(Of KlassenbildungBalance)(BalanceListe)
        If b Is Nothing Then Return
        b.Wert = CStr(If(BalanceWert.SelectedItem, Nothing))
        Dim n As Integer
        If Integer.TryParse(BalanceToleranz.Text.Trim(), n) Then b.Toleranz = Math.Max(0, n)
        b.Modus = CStr(If(BalanceModus.SelectedItem, "soft"))
        b.Prio = Integer.Parse(CStr(If(BalancePrio.SelectedItem, "2")))
        BalanceListeFuellen()
        Aktualisieren()
    End Sub

    ' ---------------------------------------------------------------
    ' Wuensche
    ' ---------------------------------------------------------------

    Private Sub WunschListeFuellen()
        _wuensche.Aktualisiere()
        Dim vorher = Gewaehlt(Of KlassenbildungWunsch)(WunschListe)
        WunschListe.ItemsSource = _wuensche.Eintraege.
            Select(Function(w) New Zeilenpaar(w, _wuensche.Zeilentext(w))).ToList()
        Auswahl(WunschListe, vorher)
        WunschZeigen()
    End Sub

    Private Sub WunschZeigen()
        Dim w = Gewaehlt(Of KlassenbildungWunsch)(WunschListe)
        WunschDetail.IsEnabled = w IsNot Nothing
        _fuellt = True
        Try
            WunschTyp.SelectedItem = If(w?.Typ, "zusammen")
            WaehleKind(WunschKindA, _wuensche.Kind(w, 0))
            WaehleKind(WunschKindB, _wuensche.Kind(w, 1))
            WunschModus.SelectedItem = If(w?.Modus, "soft")
            WunschPrio.SelectedItem = If(w Is Nothing, "1", w.Prio.ToString())
        Finally
            _fuellt = False
        End Try
    End Sub

    Private Sub AufWunschNeu(sender As Object, e As RoutedEventArgs)
        _wuensche.Neu()
        WunschListeFuellen()
        Auswahl(WunschListe, _wuensche.Auswahl)
        Aktualisieren()
    End Sub

    Private Sub AufWunschDupl(sender As Object, e As RoutedEventArgs)
        _wuensche.Auswahl = Gewaehlt(Of KlassenbildungWunsch)(WunschListe)
        _wuensche.Duplizieren()
        WunschListeFuellen()
        Auswahl(WunschListe, _wuensche.Auswahl)
        Aktualisieren()
    End Sub

    Private Sub AufWunschWeg(sender As Object, e As RoutedEventArgs)
        _wuensche.Auswahl = Gewaehlt(Of KlassenbildungWunsch)(WunschListe)
        _wuensche.Loeschen()
        WunschListeFuellen()
        Aktualisieren()
    End Sub

    Private Sub AufWunschAuswahl(sender As Object, e As SelectionChangedEventArgs)
        If _fuellt Then Return
        WunschZeigen()
    End Sub

    Private Sub AufWunschFeld(sender As Object, e As RoutedEventArgs)
        If _fuellt Then Return
        Dim w = Gewaehlt(Of KlassenbildungWunsch)(WunschListe)
        If w Is Nothing Then Return
        w.Typ = CStr(If(WunschTyp.SelectedItem, "zusammen"))
        _wuensche.SetzeKind(w, 0, GewaehltesKind(WunschKindA))
        _wuensche.SetzeKind(w, 1, GewaehltesKind(WunschKindB))
        w.Modus = CStr(If(WunschModus.SelectedItem, "soft"))
        w.Prio = Integer.Parse(CStr(If(WunschPrio.SelectedItem, "1")))
        WunschListeFuellen()
        Aktualisieren()
    End Sub

    ' ---------------------------------------------------------------
    ' Fixierungen
    ' ---------------------------------------------------------------

    Private Sub FixListeFuellen()
        _fixierungen.Aktualisiere()
        FixListe.ItemsSource = _fixierungen.Eintraege.
            Select(Function(f) New Zeilenpaar(f, _fixierungen.Zeilentext(f))).ToList()
        FixHerkunft.Text = _fixierungen.HerkunftHinweis()
    End Sub

    Private Sub AufFixAuswahl(sender As Object, e As SelectionChangedEventArgs)
    End Sub

    Private Sub AufFixWeg(sender As Object, e As RoutedEventArgs)
        _fixierungen.Auswahl = Gewaehlt(Of KlassenbildungFixierung)(FixListe)
        _fixierungen.Loeschen()
        FixListeFuellen()
        Aktualisieren()
    End Sub

    ' ---------------------------------------------------------------
    ' Gemeinsame Helfer
    ' ---------------------------------------------------------------

    ''' <summary>Ein Listeneintrag: der Text fuer die Anzeige, das Objekt
    ''' fuer alles Weitere. Ohne dieses Paar muesste die Liste entweder
    ''' den Eintrag ueber ToString darstellen (unlesbar) oder ihn aus dem
    ''' Text zurueckgewinnen (fehleranfaellig).</summary>
    Friend NotInheritable Class Zeilenpaar
        Public Sub New(eintrag As Object, text As String)
            Me.Eintrag = eintrag
            Me.Text = text
        End Sub
        Public ReadOnly Property Eintrag As Object
        Public ReadOnly Property Text As String
        Public Overrides Function ToString() As String
            Return Text
        End Function
    End Class

    Private Shared Function Gewaehlt(Of T As Class)(liste As ListBox) As T
        Dim paar = TryCast(liste.SelectedItem, Zeilenpaar)
        If paar Is Nothing Then Return Nothing
        Return TryCast(paar.Eintrag, T)
    End Function

    Private Sub Auswahl(liste As ListBox, eintrag As Object)
        If eintrag Is Nothing Then
            If liste.Items.Count > 0 Then liste.SelectedIndex = 0
            Return
        End If
        For Each paar As Zeilenpaar In liste.Items
            If ReferenceEquals(paar.Eintrag, eintrag) Then
                liste.SelectedItem = paar
                Return
            End If
        Next
        If liste.Items.Count > 0 Then liste.SelectedIndex = 0
    End Sub

    ''' <summary>Rahmen/Kinder UND die Regelpruefung des Kerns
    ''' (ValidateKlassenbildung ueber das Gruppen-ViewModel) - dieselbe
    ''' Pruefung, an der spaeter das Rechnen scheitert. Vorher fragte die
    ''' Maske nur den Rahmen und sagte "ohne Beanstandung", waehrend
    ''' "Rechnen" die Gruppe ablehnte (live gemeldet 06.09.2026).</summary>
    Private Function Pruefbefunde() As List(Of String)
        Return _eingabe.Pruefe().Concat(_gruppen.Pruefe()).Distinct().ToList()
    End Function

    Private Sub AufPruefen(sender As Object, e As RoutedEventArgs)
        Dim fehler = Pruefbefunde()
        If fehler.Count = 0 Then
            _dialoge.Hinweis("Prüfung", "Keine Beanstandungen.")
        Else
            _dialoge.Hinweis("Prüfung", String.Join(vbLf, fehler.Take(25)) &
                             If(fehler.Count > 25, vbLf & $"… und {fehler.Count - 25} weitere", ""))
        End If
    End Sub


    ' ===============================================================
    ' Speicherzustand (Nutzerhinweis 26.08.2026)
    ' ===============================================================
    '
    ' Die Maske ist ein MODALES Fenster - solange sie offen ist, verdeckt
    ' sie den Ungespeichert-Indikator im Titel des Hauptfensters, und
    ' Strg+S hing bisher ausschliesslich dort. Man musste die Maske
    ' schliessen, um ueberhaupt speichern zu koennen, ohne dass etwas das
    ' gesagt haette. Deshalb beides hier: Zustand und Aktion.

    Private _speicherung As ISpeicherung

    Private Sub SpeicherungVerdrahten(speicherung As ISpeicherung)
        _speicherung = speicherung
        If _speicherung Is Nothing Then
            ' Ohne Huelle (Tests) gibt es nichts zu speichern - dann den
            ' Knopf ausblenden statt einen toten anzubieten.
            SpeichernKnopf.Visibility = Visibility.Collapsed
            Speicherzustand.Visibility = Visibility.Collapsed
            Return
        End If
        AddHandler _speicherung.ZustandGeaendert, Sub() SpeicherzustandZeigen()
        InputBindings.Add(New KeyBinding(New Befehl(Sub() Speichern()), Key.S, ModifierKeys.Control))
        SpeicherzustandZeigen()
    End Sub

    Private Sub SpeicherzustandZeigen()
        Speicherzustand.Text = Speicheranzeige.Zustandstext(_speicherung)
        Speicherzustand.Foreground = CType(FindResource(Speicheranzeige.Zustandsfarbe(_speicherung)), Brush)
        Speicherzustand.ToolTip = Speicheranzeige.Uebernahmehinweis
        SpeichernKnopf.IsEnabled = _speicherung IsNot Nothing AndAlso
                                   _speicherung.Moeglich AndAlso _speicherung.Ungespeichert
    End Sub

    Private Sub Speichern()
        If _speicherung Is Nothing OrElse Not _speicherung.Moeglich Then Return
        _speicherung.Speichern()
        SpeicherzustandZeigen()
    End Sub

    Private Sub AufSpeichern(sender As Object, e As RoutedEventArgs)
        Speichern()
    End Sub
End Class
