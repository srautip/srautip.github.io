' Code-Behind des Stammdaten-Fensters. Nur Verdrahtung und Aufbau der
' Listen-Bereiche; alle Entscheidungen liegen in den ViewModels und sind
' dort ohne Fenster geprueft (arc42 8.13).
'
' Die Listen-Bereiche werden im Code gebaut statt als acht
' XAML-Bloecke: sie unterscheiden sich nur im Detailformular, und acht
' fast gleiche Bloecke waeren acht Stellen, an denen das Grundmuster
' auseinanderlaufen kann.
Imports System.Windows.Media
Imports TimetableCore
Imports TimetableProjekt

Partial Class StammdatenFenster

    Private ReadOnly _projekt As Projekt
    Private ReadOnly _dialoge As IDialoge

    Private _schuldaten As SchuldatenViewModel
    Private _klassen As KlassenViewModel
    Private _faecher As FaecherViewModel
    Private _raeume As RaeumeViewModel
    Private _lehrkraefte As LehrkraefteViewModel
    Private _gruppen As SchuelerGruppenViewModel
    Private _zuordnungen As FesteZuordnungenViewModel
    Private _qualifikationen As QualifikationsmatrixViewModel

    ''' <summary>Welche Liste der aktive Reiter zeigt - daran haengen die
    ''' vier Aktionen am Fensterfuss.</summary>
    Private _aktiveListe As IListenMaske
    Private _aktivePruefung As Func(Of List(Of String))

    Public Event Geaendert As EventHandler

    Public Sub New(projekt As Projekt, dialoge As IDialoge)
        InitializeComponent()
        _projekt = projekt
        _dialoge = dialoge

        _schuldaten = New SchuldatenViewModel(projekt, dialoge)
        _klassen = New KlassenViewModel(projekt, dialoge)
        _faecher = New FaecherViewModel(projekt, dialoge)
        _raeume = New RaeumeViewModel(projekt, dialoge)
        _lehrkraefte = New LehrkraefteViewModel(projekt, dialoge)
        _gruppen = New SchuelerGruppenViewModel(projekt, dialoge)
        _zuordnungen = New FesteZuordnungenViewModel(projekt, dialoge)
        _qualifikationen = New QualifikationsmatrixViewModel(projekt)

        AddHandler _klassen.Geaendert, AddressOf AufAenderung
        AddHandler _faecher.Geaendert, AddressOf AufAenderung
        AddHandler _raeume.Geaendert, AddressOf AufAenderung
        AddHandler _lehrkraefte.Geaendert, AddressOf AufAenderung
        AddHandler _gruppen.Geaendert, AddressOf AufAenderung
        AddHandler _zuordnungen.Geaendert, AddressOf AufAenderung
        AddHandler _schuldaten.Geaendert, AddressOf AufAenderung
        AddHandler _qualifikationen.Geaendert, AddressOf AufAenderung

        SchuldatenFuellen()
        KlassenBereich.Content = ListenBereich(_klassen, AddressOf KlasseDetail)
        FaecherBereich.Content = ListenBereich(_faecher, AddressOf FachDetail)
        RaeumeBereich.Content = ListenBereich(_raeume, AddressOf RaumDetail)
        LehrkraefteBereich.Content = ListenBereich(_lehrkraefte, AddressOf LehrerDetail)
        GruppenBereich.Content = ListenBereich(_gruppen, AddressOf GruppeDetail)
        ZuordnungenBereich.Content = ListenBereich(_zuordnungen, AddressOf ZuordnungDetail)
        MatrixFuellen()

        _aktiveListe = Nothing
        AktionenAktualisieren()
    End Sub

    Private Sub AufAenderung(sender As Object, e As EventArgs)
        RaiseEvent Geaendert(Me, EventArgs.Empty)
        StatusAktualisieren()
    End Sub

    ' ===============================================================
    ' Der gemeinsame Listen-Bereich (Grundmuster aus Abschnitt 6)
    ' ===============================================================

    Private Function ListenBereich(Of T As Class)(vm As ListenViewModel(Of T),
                                                  detail As Func(Of T, UIElement)) As UIElement
        Dim wurzel As New Grid With {.Margin = New Thickness(0, 12, 0, 0)}
        wurzel.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(270)})
        wurzel.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})

        ' --- links: Filter + Liste ---
        Dim links As New DockPanel()
        Dim filter As New TextBox With {.Margin = New Thickness(0, 0, 0, 6)}
        DockPanel.SetDock(filter, Dock.Top)
        Dim liste As New ListBox With {
            .DisplayMemberPath = Nothing,
            .BorderBrush = CType(FindResource("farbe-linie"), Brush),
            .BorderThickness = New Thickness(1)}
        liste.ItemsSource = vm.Eintraege

        Dim rechts As New ContentControl With {.Margin = New Thickness(16, 0, 0, 0)}

        AddHandler filter.TextChanged, Sub() vm.Filter = filter.Text
        AddHandler liste.SelectionChanged,
            Sub()
                vm.Auswahl = CType(liste.SelectedItem, T)
                rechts.Content = If(vm.Auswahl Is Nothing, Nothing, detail(vm.Auswahl))
                AktionenAktualisieren()
            End Sub

        ' Wenn das ViewModel die Liste neu aufbaut (Neu, Loeschen,
        ' Filter), muss die Auswahl im Steuerelement nachziehen.
        AddHandler vm.PropertyChanged,
            Sub(s, e2)
                If e2.PropertyName <> NameOf(vm.Auswahl) Then Return
                If Not ReferenceEquals(liste.SelectedItem, vm.Auswahl) Then liste.SelectedItem = vm.Auswahl
                rechts.Content = If(vm.Auswahl Is Nothing, Nothing, detail(vm.Auswahl))
            End Sub

        links.Children.Add(filter)
        links.Children.Add(liste)
        Grid.SetColumn(links, 0)
        Grid.SetColumn(rechts, 1)
        wurzel.Children.Add(links)
        wurzel.Children.Add(rechts)

        ' Anzeigename in der Liste
        Dim vorlage As New DataTemplate()
        Dim block As New FrameworkElementFactory(GetType(TextBlock))
        block.SetBinding(TextBlock.TextProperty, New Binding(".") With {.Converter = New NamensKonverter(vm)})
        vorlage.VisualTree = block
        liste.ItemTemplate = vorlage

        If vm.Eintraege.Count > 0 Then liste.SelectedIndex = 0
        wurzel.Tag = vm
        AddHandler wurzel.IsVisibleChanged, Sub() If wurzel.IsVisible Then SetzeAktiv(vm)
        Return wurzel
    End Function

    ''' <summary>Zeigt in der Liste den Namen, den das ViewModel vergibt -
    ''' bei festen Zuordnungen ist das ein abgeleitetes Tripel und kein
    ''' Feld, deshalb nicht ueber DisplayMemberPath.</summary>
    Private NotInheritable Class NamensKonverter
        Implements IValueConverter
        Private ReadOnly _namen As Func(Of Object, String)
        Public Sub New(vm As IListenMaske)
            _namen = AddressOf vm.AnzeigeName
        End Sub
        Public Function Convert(value As Object, t As Type, p As Object, c As Globalization.CultureInfo) As Object _
            Implements IValueConverter.Convert
            Return If(value Is Nothing, "", _namen(value))
        End Function
        Public Function ConvertBack(value As Object, t As Type, p As Object, c As Globalization.CultureInfo) As Object _
            Implements IValueConverter.ConvertBack
            Throw New NotSupportedException()
        End Function
    End Class

    ' ===============================================================
    ' Detailformulare
    ' ===============================================================

    Private Function Beschriftung(text As String) As TextBlock
        Return New TextBlock With {
            .Text = text, .Margin = New Thickness(0, 10, 0, 2),
            .FontSize = 12, .Foreground = CType(FindResource("farbe-text-2"), Brush)}
    End Function

    Private Function TextFeld(lesen As Func(Of String), schreiben As Action(Of String)) As TextBox
        Dim t As New TextBox With {.Text = If(lesen(), "")}
        AddHandler t.LostFocus, Sub() schreiben(t.Text)
        Return t
    End Function

    Private Function ZahlFeld(lesen As Func(Of String), schreiben As Action(Of String)) As TextBox
        ' "Zahlenfelder strikt numerisch" (Abschnitt 6). Die Pruefung
        ' laeuft beim Verlassen, nicht beim Tippen - sonst kann man
        ' keine mehrstellige Zahl eingeben.
        Dim t As New TextBox With {.Text = If(lesen(), ""), .Width = 90, .HorizontalAlignment = HorizontalAlignment.Left}
        AddHandler t.LostFocus,
            Sub()
                Dim roh = t.Text.Trim()
                If roh = "" OrElse Double.TryParse(roh, Globalization.NumberStyles.Any,
                                                   Globalization.CultureInfo.CurrentCulture, Nothing) Then
                    schreiben(roh)
                Else
                    t.Text = If(lesen(), "")
                End If
            End Sub
        Return t
    End Function

    Private Function Stapel(ParamArray teile As UIElement()) As UIElement
        Dim s As New StackPanel With {.MaxWidth = 520, .HorizontalAlignment = HorizontalAlignment.Left}
        For Each t In teile
            s.Children.Add(t)
        Next
        Return New ScrollViewer With {.Content = s, .VerticalScrollBarVisibility = ScrollBarVisibility.Auto}
    End Function

    Private Function RaumDetail(r As Raum) As UIElement
        Return Stapel(
            Beschriftung("Name"), TextFeld(Function() r.Name, Sub(v) UmbenennenOderSetzen(_raeume, r.Name, v, Sub(x) r.Name = x)),
            Beschriftung("Typ (Freitext-Kategorie)"), TextFeld(Function() r.Typ, Sub(v) r.Typ = v))
    End Function

    Private Function KlasseDetail(k As Klasse) As UIElement
        Dim stufe As New ComboBox()
        For Each s In _klassen.Stufen
            stufe.Items.Add(s.Nummer)
        Next
        stufe.SelectedItem = k.Klassenstufe
        AddHandler stufe.SelectionChanged, Sub() If stufe.SelectedItem IsNot Nothing Then k.Klassenstufe = CInt(stufe.SelectedItem)

        Dim tandem As New CheckBox With {.Content = "Klassenlehrer-Tandem erlaubt", .IsChecked = k.ErlaubtKlassenlehrerTandem, .Margin = New Thickness(0, 10, 0, 0)}
        AddHandler tandem.Click, Sub() k.ErlaubtKlassenlehrerTandem = tandem.IsChecked = True

        Dim zug As New Button With {.Content = "Zug über alle Stufen ergänzen", .Margin = New Thickness(0, 16, 0, 0), .HorizontalAlignment = HorizontalAlignment.Left}
        AddHandler zug.Click,
            Sub()
                Dim angelegt = _klassen.ZugErgaenzen(_klassen.Stufen.Select(Function(s) s.Nummer))
                _dialoge.Hinweis("Zug ergänzen",
                    If(angelegt.Count = 0, "Es wurde nichts angelegt.", "Angelegt: " & String.Join(", ", angelegt)))
            End Sub

        Return Stapel(
            Beschriftung("Name (eindeutig)"), TextFeld(Function() k.Name, Sub(v) UmbenennenOderSetzen(_klassen, k.Name, v, Sub(x) k.Name = x)),
            Beschriftung("Klassenstufe"), stufe,
            Beschriftung("Schülerzahl (informativ)"), ZahlFeld(Function() If(k.Schuelerzahl?.ToString(), ""),
                Sub(v) k.Schuelerzahl = If(v = "", CType(Nothing, Integer?), CInt(v))),
            tandem, zug)
    End Function

    Private Function FachDetail(f As Fach) As UIElement
        Dim unbeliebt As New CheckBox With {.Content = "unbeliebt (Verteilungs-Fairness)", .IsChecked = f.Unbeliebt, .Margin = New Thickness(0, 10, 0, 0)}
        AddHandler unbeliebt.Click, Sub() f.Unbeliebt = unbeliebt.IsChecked = True

        Dim tabelle As New StackPanel With {.Margin = New Thickness(0, 8, 0, 0)}
        For Each zeile In _faecher.StufenZeilen(f)
            Dim z = zeile
            Dim reihe As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 2, 0, 2)}
            reihe.Children.Add(New TextBlock With {.Text = $"Stufe {z.Stufe}", .Width = 70, .VerticalAlignment = VerticalAlignment.Center})
            Dim soll = ZahlFeld(Function() If(z.Soll?.ToString(), ""),
                                Sub(v) _faecher.SetzeStufe(f, z.Stufe, If(v = "", CType(Nothing, Integer?), CInt(v)), z.MaxProTag))
            soll.Width = 70
            reihe.Children.Add(soll)
            If z.Hinweis <> "" Then
                reihe.Children.Add(New TextBlock With {
                    .Text = "  " & z.Hinweis, .VerticalAlignment = VerticalAlignment.Center,
                    .FontSize = 11, .Foreground = CType(FindResource("farbe-text-3"), Brush)})
            End If
            tabelle.Children.Add(reihe)
        Next

        Return Stapel(
            Beschriftung("Name (eindeutig)"), TextFeld(Function() f.Name, Sub(v) UmbenennenOderSetzen(_faecher, f.Name, v, Sub(x) f.Name = x)),
            Beschriftung("Blocklänge (Doppelstunde, optional)"), ZahlFeld(Function() If(f.BlockLength?.ToString(), ""),
                Sub(v) f.BlockLength = If(v = "", CType(Nothing, Integer?), CInt(v))),
            unbeliebt,
            Beschriftung("Wochenstunden je Stufe (leer = wird dort nicht unterrichtet)"), tabelle,
            New TextBlock With {.Text = _faecher.SummenZeile, .Margin = New Thickness(0, 12, 0, 0),
                                .FontSize = 11, .Foreground = CType(FindResource("farbe-text-2"), Brush)})
    End Function

    Private Function LehrerDetail(l As Lehrer) As UIElement
        Dim kl As New CheckBox With {.Content = "klassenlehrerfähig", .IsChecked = l.KlassenlehrerFaehig, .Margin = New Thickness(0, 10, 0, 0)}
        AddHandler kl.Click, Sub() l.KlassenlehrerFaehig = kl.IsChecked = True

        Dim faecher As New StackPanel With {.Margin = New Thickness(0, 8, 0, 0)}
        For Each f In _projekt.Bestand.Faecher.OrderBy(Function(x) x.Name)
            Dim fach = f
            Dim reihe As New StackPanel With {.Orientation = Orientation.Horizontal}
            Dim an As New CheckBox With {.Content = fach.Name, .Width = 200, .IsChecked = _lehrkraefte.IstQualifiziert(l, fach.Name)}
            Dim fremd As New CheckBox With {.Content = "fachfremd", .IsChecked = _lehrkraefte.IstFachfremd(l, fach.Name)}
            AddHandler an.Click, Sub() _lehrkraefte.SetzeQualifikation(l, fach.Name, an.IsChecked = True, fremd.IsChecked = True)
            AddHandler fremd.Click, Sub() _lehrkraefte.SetzeQualifikation(l, fach.Name, an.IsChecked = True, fremd.IsChecked = True)
            reihe.Children.Add(an)
            reihe.Children.Add(fremd)
            faecher.Children.Add(reihe)
        Next

        Return Stapel(
            New TextBlock With {.Text = _lehrkraefte.PlausibilitaetsZeile, .TextWrapping = TextWrapping.Wrap,
                                .Margin = New Thickness(0, 0, 0, 12), .FontSize = 12,
                                .Foreground = CType(FindResource("farbe-text-2"), Brush)},
            Beschriftung("Name (eindeutig - ist Schlüssel)"), TextFeld(Function() l.Name, Sub(v) UmbenennenOderSetzen(_lehrkraefte, l.Name, v, Sub(x) l.Name = x)),
            Beschriftung("Deputat-Sollstunden"), ZahlFeld(Function() l.DeputatSollstunden.ToString(), Sub(v) l.DeputatSollstunden = If(v = "", 0, CDbl(v))),
            Beschriftung("Anrechnungsstunden"), ZahlFeld(Function() l.Anrechnungsstunden.ToString(), Sub(v) l.Anrechnungsstunden = If(v = "", 0, CDbl(v))),
            Beschriftung("Springer-Reserve"), ZahlFeld(Function() l.SpringerReserveStunden.ToString(), Sub(v) l.SpringerReserveStunden = If(v = "", 0, CDbl(v))),
            Beschriftung("max. Klassen (leer = unbegrenzt)"), ZahlFeld(Function() If(l.MaxKlassen?.ToString(), ""), Sub(v) l.MaxKlassen = If(v = "", CType(Nothing, Integer?), CInt(v))),
            Beschriftung("max. Fächer (leer = unbegrenzt)"), ZahlFeld(Function() If(l.MaxFaecher?.ToString(), ""), Sub(v) l.MaxFaecher = If(v = "", CType(Nothing, Integer?), CInt(v))),
            kl,
            Beschriftung("Qualifikationen"), faecher)
    End Function

    Private Function GruppeDetail(g As Gruppe) As UIElement
        Dim mitglieder As New StackPanel()
        For Each s In _gruppen.Schueler.Take(400)
            Dim kind = s
            Dim cb As New CheckBox With {.Content = $"{kind.Id} ({kind.Klasse})", .IsChecked = _gruppen.IstMitglied(g, kind.Id)}
            AddHandler cb.Click, Sub() _gruppen.SetzeMitglied(g, kind.Id, cb.IsChecked = True)
            mitglieder.Children.Add(cb)
        Next

        Return Stapel(
            Beschriftung("Name"), TextFeld(Function() g.Name, Sub(v) g.Name = v),
            Beschriftung("Typ"), TextFeld(Function() g.Typ, Sub(v) g.Typ = v),
            Beschriftung("Fach"), TextFeld(Function() g.FachName, Sub(v) g.FachName = v),
            Beschriftung("Klassenstufe"), ZahlFeld(Function() If(g.Klassenstufe?.ToString(), ""), Sub(v) g.Klassenstufe = If(v = "", CType(Nothing, Integer?), CInt(v))),
            Beschriftung("Parallelverbund"), TextFeld(Function() g.Parallelverbund, Sub(v) g.Parallelverbund = v),
            Beschriftung($"Mitglieder ({g.MitgliederSchuelerIds.Count})"),
            New ScrollViewer With {.Content = mitglieder, .MaxHeight = 260, .VerticalScrollBarVisibility = ScrollBarVisibility.Auto})
    End Function

    Private Function ZuordnungDetail(z As FesteZuordnung) As UIElement
        Dim lehrer As New ComboBox()
        For Each l In _projekt.Bestand.Lehrkraefte.OrderBy(Function(x) x.Name)
            lehrer.Items.Add(l.Name)
        Next
        lehrer.SelectedItem = z.LehrerName

        Dim fach As New ComboBox()
        Dim fachFuellen = Sub()
                              fach.Items.Clear()
                              For Each f In _zuordnungen.MoeglicheFaecher(z.LehrerName)
                                  fach.Items.Add(f)
                              Next
                              fach.SelectedItem = z.FachName
                          End Sub
        fachFuellen()

        Dim klasse As New ComboBox()
        For Each k In _zuordnungen.MoeglicheKlassen()
            klasse.Items.Add(k)
        Next
        klasse.SelectedItem = z.KlasseName

        AddHandler lehrer.SelectionChanged,
            Sub()
                z.LehrerName = CStr(lehrer.SelectedItem)
                ' Die Fachliste filtert auf qualifizierte Kombinationen
                ' (6.9) - nach einem Lehrerwechsel neu aufbauen.
                fachFuellen()
            End Sub
        AddHandler klasse.SelectionChanged, Sub() z.KlasseName = CStr(klasse.SelectedItem)
        AddHandler fach.SelectionChanged, Sub() z.FachName = CStr(fach.SelectedItem)

        Return Stapel(
            Beschriftung("Lehrkraft"), lehrer,
            Beschriftung("Klasse oder Gruppe"), klasse,
            Beschriftung("Fach (nur qualifizierte)"), fach)
    End Function

    ' ===============================================================
    ' Schuldaten und Matrix
    ' ===============================================================

    Private Sub SchuldatenFuellen()
        SchulName.Text = If(_schuldaten.SchulName, "")
        Bundesland.Text = If(_schuldaten.Bundesland, "")
        StundenProTag.Text = _schuldaten.StundenProTag.ToString()
        For Each item As ComboBoxItem In Schulart.Items
            If String.Equals(CStr(item.Content), _schuldaten.Schulart, StringComparison.OrdinalIgnoreCase) Then
                Schulart.SelectedItem = item
            End If
        Next
        TageLeiste.Children.Clear()
        For Each t In _schuldaten.MoeglicheTage
            TageLeiste.Children.Add(New CheckBox With {
                .Content = t, .Margin = New Thickness(0, 0, 12, 0), .IsChecked = _schuldaten.TagAktiv(t)})
        Next
        Kapazitaetszeile.Text = _schuldaten.KapazitaetsZeile
    End Sub

    Private Sub AufSchuldatenAenderung(sender As Object, e As RoutedEventArgs)
        If _schuldaten Is Nothing Then Return
        _schuldaten.SchulName = SchulName.Text
        _schuldaten.Bundesland = Bundesland.Text
        Dim gewaehlt = TryCast(Schulart.SelectedItem, ComboBoxItem)
        If gewaehlt IsNot Nothing Then _schuldaten.Schulart = CStr(gewaehlt.Content)
    End Sub

    Private Sub AufRasterUebernehmen(sender As Object, e As RoutedEventArgs)
        Dim tage = TageLeiste.Children.OfType(Of CheckBox)().
            Where(Function(c) c.IsChecked = True).Select(Function(c) CStr(c.Content)).ToList()
        Dim stunden As Integer
        If Not Integer.TryParse(StundenProTag.Text.Trim(), stunden) Then
            _dialoge.Hinweis("Schuldaten", "Stunden je Tag muss eine Zahl sein.")
            StundenProTag.Text = _schuldaten.StundenProTag.ToString()
            Return
        End If
        If _schuldaten.SetzeRaster(tage, stunden) Then
            RaiseEvent Geaendert(Me, EventArgs.Empty)
        End If
        SchuldatenFuellen()
    End Sub

    Private Sub MatrixFuellen()
        Matrix.Children.Clear()
        Matrix.ColumnDefinitions.Clear()
        Matrix.RowDefinitions.Clear()

        Dim faecher = _qualifikationen.Faecher
        Dim lehrer = _qualifikationen.Lehrkraefte
        Matrix.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        For i = 1 To faecher.Count
            Matrix.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Next
        For i = 0 To lehrer.Count + 1
            Matrix.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Next

        For s = 0 To faecher.Count - 1
            Dim kopf As New TextBlock With {
                .Text = faecher(s).Name, .Margin = New Thickness(6, 2, 6, 6), .FontSize = 11,
                .LayoutTransform = New RotateTransform(-60)}
            Grid.SetRow(kopf, 0) : Grid.SetColumn(kopf, s + 1)
            Matrix.Children.Add(kopf)
        Next

        For z = 0 To lehrer.Count - 1
            Dim name = lehrer(z).Name
            Dim zelleName As New TextBlock With {.Text = name, .Margin = New Thickness(0, 2, 10, 2), .FontSize = 12}
            Grid.SetRow(zelleName, z + 1) : Grid.SetColumn(zelleName, 0)
            Matrix.Children.Add(zelleName)

            For s = 0 To faecher.Count - 1
                Dim fachName = faecher(s).Name
                Dim knopf As New Button With {.MinWidth = 30, .MinHeight = 22, .Margin = New Thickness(1), .FontSize = 11}
                Dim male = Sub()
                               Select Case _qualifikationen.Zustand(name, fachName)
                                   Case Qualifikation.Qualifiziert : knopf.Content = "✓"
                                   Case Qualifikation.Fachfremd : knopf.Content = "(✓)"
                                   Case Else : knopf.Content = ""
                               End Select
                           End Sub
                male()
                AddHandler knopf.Click,
                    Sub()
                        _qualifikationen.Weiterschalten(name, fachName)
                        male()
                        RaiseEvent Geaendert(Me, EventArgs.Empty)
                        MatrixFussAktualisieren()
                    End Sub
                Grid.SetRow(knopf, z + 1) : Grid.SetColumn(knopf, s + 1)
                Matrix.Children.Add(knopf)
            Next
        Next

        MatrixFussAktualisieren()
    End Sub

    Private Sub MatrixFussAktualisieren()
        ' Spaltenfuss: Bedarf vs. Deputat, Engpaesse rot (6.7).
        Dim zeile = _qualifikationen.Lehrkraefte.Count + 1
        For Each alt In Matrix.Children.OfType(Of TextBlock)().Where(Function(t) Grid.GetRow(t) = zeile).ToList()
            Matrix.Children.Remove(alt)
        Next
        Dim fuss = _qualifikationen.Spaltenfuss
        For s = 0 To fuss.Count - 1
            Dim e = fuss(s)
            Dim t As New TextBlock With {
                .Text = $"{e.Bedarf}/{e.Deputat:0.#}", .FontSize = 10, .Margin = New Thickness(2, 6, 2, 0),
                .HorizontalAlignment = HorizontalAlignment.Center,
                .Foreground = CType(FindResource(If(e.Engpass, "farbe-krit-text", "farbe-text-3")), Brush)}
            Grid.SetRow(t, zeile) : Grid.SetColumn(t, s + 1)
            Matrix.Children.Add(t)
        Next
    End Sub

    ' ===============================================================
    ' Aktionsleiste
    ' ===============================================================

    Private Sub SetzeAktiv(vm As IListenMaske)
        _aktiveListe = vm
        _aktivePruefung = AddressOf vm.Pruefe
        AktionenAktualisieren()
    End Sub

    Private Sub AufReiterWechsel(sender As Object, e As SelectionChangedEventArgs)
        If Not ReferenceEquals(e.OriginalSource, Reiter) Then Return
        Dim kopf = TryCast(Reiter.SelectedItem, TabItem)
        If kopf IsNot Nothing AndAlso CStr(kopf.Header) = "Schuldaten" Then
            _aktiveListe = Nothing
            _aktivePruefung = AddressOf _schuldaten.Pruefe
            SchuldatenFuellen()
        ElseIf kopf IsNot Nothing AndAlso CStr(kopf.Header) = "Qualifikationen" Then
            _aktiveListe = Nothing
            _aktivePruefung = Nothing
            MatrixFuellen()
        End If
        AktionenAktualisieren()
    End Sub

    Private Sub AktionenAktualisieren()
        Dim istListe = _aktiveListe IsNot Nothing
        NeuKnopf.IsEnabled = istListe
        DuplizierenKnopf.IsEnabled = istListe
        LoeschenKnopf.IsEnabled = istListe
        StatusAktualisieren()
    End Sub

    Private Sub StatusAktualisieren()
        If _aktivePruefung Is Nothing Then
            Statuszeile.Text = ""
            Return
        End If
        Dim n = _aktivePruefung().Count
        Statuszeile.Text = If(n = 0, "Prüfung ohne Beanstandung.", $"{n} Hinweis(e) - Pruefen zeigt sie.")
    End Sub

    Private Sub AufNeu(sender As Object, e As RoutedEventArgs)
        If _aktiveListe IsNot Nothing Then _aktiveListe.Neu()
        StatusAktualisieren()
    End Sub

    Private Sub AufDuplizieren(sender As Object, e As RoutedEventArgs)
        If _aktiveListe IsNot Nothing Then _aktiveListe.Duplizieren()
        StatusAktualisieren()
    End Sub

    Private Sub AufLoeschen(sender As Object, e As RoutedEventArgs)
        If _aktiveListe IsNot Nothing Then _aktiveListe.Loeschen()
        StatusAktualisieren()
    End Sub

    Private Sub AufPruefen(sender As Object, e As RoutedEventArgs)
        If _aktivePruefung Is Nothing Then
            _dialoge.Hinweis("Prüfung", "Für diese Ansicht gibt es keine eigene Prüfung.")
            Return
        End If
        Dim fehler = _aktivePruefung()
        If fehler.Count = 0 Then
            _dialoge.Hinweis("Prüfung", "Keine Beanstandungen.")
        Else
            _dialoge.Hinweis("Prüfung", String.Join(vbLf, fehler.Take(25)) &
                             If(fehler.Count > 25, vbLf & $"… und {fehler.Count - 25} weitere", ""))
        End If
    End Sub

    ''' <summary>Namensaenderung: laeuft ueber Bestandspflege, damit sie
    ''' kaskadiert (arc42 8.15), und zeigt vorher die Tragweite.</summary>
    Private Sub UmbenennenOderSetzen(Of T As Class)(vm As ListenViewModel(Of T),
                                                    alt As String, neu As String, direkt As Action(Of String))
        If String.Equals(alt, neu, StringComparison.Ordinal) Then Return
        If String.IsNullOrWhiteSpace(alt) OrElse String.IsNullOrWhiteSpace(neu) Then
            direkt(neu)
            Return
        End If
        Dim betroffen = vm.BenenneUm(alt, neu)
        If betroffen > 0 Then
            _dialoge.Hinweis("Umbenannt", $"{betroffen} Verweis(e) wurden angepasst.")
        Else
            direkt(neu)
        End If
        RaiseEvent Geaendert(Me, EventArgs.Empty)
    End Sub

End Class
