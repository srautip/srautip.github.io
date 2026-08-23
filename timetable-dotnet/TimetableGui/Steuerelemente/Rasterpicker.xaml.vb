' Anzeige und Eingabe des Rasterpickers. Die Auswahllogik liegt in
' RasterAuswahl und ist dort ohne Fenster geprueft; hier steht nur, was
' sich ohne laufendes Fenster ohnehin nicht pruefen liesse (arc42 8.13).
Imports System.Windows.Media

Partial Class Rasterpicker

    Private _auswahl As RasterAuswahl
    Private _zellen As New Dictionary(Of String, Border)(StringComparer.Ordinal)

    ' Ziehen: gemerkt wird die Startzelle und ob das Ziehen setzt oder
    ' loescht. Die Richtung ergibt sich aus der ERSTEN Zelle - wer auf
    ' einer gewaehlten Zelle beginnt, loescht; wer auf einer freien
    ' beginnt, waehlt. Das ist die Erwartung, die jede Tabellenauswahl
    ' bedient.
    Private _zieht As Boolean
    Private _setzt As Boolean
    Private _startTag As String
    Private _startStunde As Integer

    Public Sub New()
        InitializeComponent()
    End Sub

    ''' <summary>Ereignis fuer die Maske - sie zeigt die Auswahl in ihrer
    ''' eigenen Zusammenfassung oder aktiviert das Speichern.</summary>
    Public Event AuswahlGeaendert As EventHandler

    Public Property Auswahl As RasterAuswahl
        Get
            Return _auswahl
        End Get
        Set
            _auswahl = value
            BaueGitter()
        End Set
    End Property

    ' ---------------------------------------------------------------

    Private Shared Function Schluessel(tag As String, stunde As Integer) As String
        Return tag & "|" & stunde.ToString(Globalization.CultureInfo.InvariantCulture)
    End Function

    Private Sub BaueGitter()
        Gitter.Children.Clear()
        Gitter.ColumnDefinitions.Clear()
        Gitter.RowDefinitions.Clear()
        _zellen.Clear()
        If _auswahl Is Nothing Then Return

        ' Spalte 0 traegt die Stundennummern, Zeile 0 die Tagesnamen -
        ' genau wie die Stundentafel.
        Gitter.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        For i = 1 To _auswahl.Tage.Count
            Gitter.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Next
        Gitter.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        For i = 1 To _auswahl.Stunden
            Gitter.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Next

        For s = 0 To _auswahl.Tage.Count - 1
            Gitter.Children.Add(Kopf(_auswahl.Tage(s), 0, s + 1))
        Next
        For p = 1 To _auswahl.Stunden
            Gitter.Children.Add(Kopf(p.ToString(Globalization.CultureInfo.InvariantCulture), p, 0))
            For s = 0 To _auswahl.Tage.Count - 1
                Dim z = BaueZelle(_auswahl.Tage(s), p)
                Grid.SetRow(z, p)
                Grid.SetColumn(z, s + 1)
                Gitter.Children.Add(z)
                _zellen(Schluessel(_auswahl.Tage(s), p)) = z
            Next
        Next

        Male()
    End Sub

    Private Function Kopf(text As String, zeile As Integer, spalte As Integer) As UIElement
        Dim t As New TextBlock With {
            .Text = text,
            .Margin = New Thickness(6, 3, 6, 3),
            .HorizontalAlignment = HorizontalAlignment.Center,
            .FontSize = 11,
            .Foreground = CType(FindResource("farbe-text-2"), Brush)}
        Grid.SetRow(t, zeile)
        Grid.SetColumn(t, spalte)
        Return t
    End Function

    Private Function BaueZelle(tag As String, stunde As Integer) As Border
        Dim b As New Border With {
            .BorderBrush = CType(FindResource("farbe-linie"), Brush),
            .BorderThickness = New Thickness(1),
            .MinWidth = 34,
            .MinHeight = 22,
            .Margin = New Thickness(1),
            .CornerRadius = CType(FindResource("radius-s"), CornerRadius),
            .Cursor = Input.Cursors.Hand,
            .Focusable = True}
        b.Tag = (tag, stunde)

        AddHandler b.MouseLeftButtonDown, AddressOf AufZellenDruck
        AddHandler b.MouseEnter, AddressOf AufZellenUeberfahrt
        ' Tastatur: dieselbe Zusage wie im Board - was die Maus kann,
        ' kann auch Enter (klassenbildung-ui-konzept.md 203).
        AddHandler b.KeyDown, AddressOf AufZellenTaste
        Return b
    End Function

    Private Sub Male()
        If _auswahl Is Nothing Then Return
        Dim an = CType(FindResource("farbe-akzent"), Brush)
        Dim anFlaeche = CType(FindResource("farbe-akzent-flaeche"), Brush)
        Dim aus = CType(FindResource("farbe-linie"), Brush)
        Dim leer = CType(FindResource("farbe-flaeche"), Brush)

        For Each paar In _zellen
            Dim teile = paar.Key.Split("|"c)
            Dim gewaehlt = _auswahl.IstGewaehlt(teile(0), Integer.Parse(teile(1), Globalization.CultureInfo.InvariantCulture))
            paar.Value.Background = If(gewaehlt, anFlaeche, leer)
            paar.Value.BorderBrush = If(gewaehlt, an, aus)
        Next
        Zusammenfassung.Text = _auswahl.Beschreibung()
    End Sub

    Private Shared Function Ort(sender As Object) As (Tag As String, Stunde As Integer)
        Return CType(CType(sender, Border).Tag, (Tag As String, Stunde As Integer))
    End Function

    Private Sub AufZellenDruck(sender As Object, e As Input.MouseButtonEventArgs)
        If _auswahl Is Nothing Then Return
        Dim o = Ort(sender)
        _zieht = True
        _setzt = Not _auswahl.IstGewaehlt(o.Tag, o.Stunde)
        _startTag = o.Tag
        _startStunde = o.Stunde
        _auswahl.Setze(o.Tag, o.Stunde, _setzt)
        CType(sender, Border).Focus()
        CType(sender, Border).CaptureMouse()
        Male()
        RaiseEvent AuswahlGeaendert(Me, EventArgs.Empty)
    End Sub

    Private Sub AufZellenUeberfahrt(sender As Object, e As Input.MouseEventArgs)
        If Not _zieht OrElse e.LeftButton <> Input.MouseButtonState.Pressed Then Return
        Dim o = Ort(sender)
        ' Das ganze Rechteck ab der Startzelle neu setzen, nicht nur die
        ' ueberfahrene Zelle: sonst haengt das Ergebnis davon ab, WIE
        ' schnell jemand zieht - bei schneller Bewegung feuert MouseEnter
        ' nicht auf jeder Zelle.
        _auswahl.Bereich(_startTag, _startStunde, o.Tag, o.Stunde, _setzt)
        Male()
        RaiseEvent AuswahlGeaendert(Me, EventArgs.Empty)
    End Sub

    Private Sub AufLosgelassen(sender As Object, e As Input.MouseButtonEventArgs) Handles Me.PreviewMouseLeftButtonUp
        _zieht = False
        Mouse.Capture(Nothing)
    End Sub

    Private Sub AufZellenTaste(sender As Object, e As Input.KeyEventArgs)
        If _auswahl Is Nothing Then Return
        If e.Key <> Input.Key.Enter AndAlso e.Key <> Input.Key.Space Then Return
        Dim o = Ort(sender)
        _auswahl.Umschalten(o.Tag, o.Stunde)
        Male()
        RaiseEvent AuswahlGeaendert(Me, EventArgs.Empty)
        e.Handled = True
    End Sub

End Class
