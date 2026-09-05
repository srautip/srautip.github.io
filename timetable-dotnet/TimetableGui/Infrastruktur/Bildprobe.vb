' Die Bildprobe: die Anwendung startet, laeuft durch ihre Bereiche,
' schreibt je Sicht ein PNG und beendet sich.
'
' Grund: ein laufendes WPF-Fenster laesst sich von AUSSEN nicht
' verlaesslich fotografieren - WPF zeichnet per DirectX, GDI-Captures
' (CopyFromScreen, PrintWindow) liefern eine weisse Flaeche. Von innen
' sieht RenderTargetBitmap alles, was WPF zeichnet; das WebView2-
' Dashboard liefert sein Bild selbst (CapturePreview) und wird an
' seiner Stelle eingeblendet. So laesst sich jede Aenderung an der
' Oberflaeche ohne Bildschirm belegen - auch von einem Werkzeug aus,
' das nur Dateien lesen kann.
'
' Aufruf:
'   TimetableGui --bildprobe <ordner> [--schule <tests/schule>]
'                [--projekt <datei.splanx>] [--rechnen stundenplan|klassenbildung]
'                [--masken]
' Das Passwort einer Projektdatei kommt aus SCHULPLANUNG_PASSWORT - nie
' aus der Kommandozeile, die steht in jeder Prozessliste.
Imports System.IO
Imports System.Text.Json.Nodes
Imports System.Windows.Media.Imaging
Imports TimetableProjekt

Public NotInheritable Class BildprobeAuftrag
    Public Property Ordner As String = ""
    ''' <summary>Ein `tests/&lt;schule&gt;`-Ordner. Seine `output/*.json`
    ''' werden als Staende eingehaengt - beide Dashboards ohne Lauf.</summary>
    Public Property Schule As String
    Public Property Projekt As String
    Public Property Rechnen As String
    Public Property Masken As Boolean
End Class

Public Module Bildprobe

    ''' <summary>Der Auftrag dieses Prozesses; Nothing im Normalbetrieb.
    ''' Gesetzt beim Start (Application.OnStartup), gelesen vom
    ''' Hauptfenster nach dem Laden.</summary>
    Public Property Auftrag As BildprobeAuftrag

    ''' <summary>Liest die Kommandozeile. Nothing ohne `--bildprobe`.
    ''' Ein unbekannter Schalter oder ein fehlender Wert ist ein Fehler -
    ''' eine Bildprobe, die still etwas anderes tut als verlangt, waere
    ''' ein falscher Beleg.</summary>
    Public Function Lesen(args As IList(Of String)) As BildprobeAuftrag
        If args Is Nothing OrElse Not args.Contains("--bildprobe") Then Return Nothing
        Dim a As New BildprobeAuftrag()
        Dim i = 0
        While i < args.Count
            Dim schalter = args(i)
            Select Case schalter
                Case "--bildprobe" : a.Ordner = Wert(args, i)
                Case "--schule" : a.Schule = Wert(args, i)
                Case "--projekt" : a.Projekt = Wert(args, i)
                Case "--rechnen"
                    a.Rechnen = Wert(args, i).ToLowerInvariant()
                    If a.Rechnen <> "stundenplan" AndAlso a.Rechnen <> "klassenbildung" Then
                        Throw New ArgumentException($"--rechnen erwartet stundenplan oder klassenbildung, nicht '{a.Rechnen}'.")
                    End If
                Case "--masken" : a.Masken = True
                Case Else
                    Throw New ArgumentException($"Unbekannter Schalter: {schalter}")
            End Select
            i += 1
        End While
        If a.Ordner = "" Then Throw New ArgumentException("--bildprobe braucht einen Zielordner.")
        Return a
    End Function

    Private Function Wert(args As IList(Of String), ByRef i As Integer) As String
        If i + 1 >= args.Count OrElse args(i + 1).StartsWith("--", StringComparison.Ordinal) Then
            Throw New ArgumentException($"{args(i)} braucht einen Wert.")
        End If
        i += 1
        Return args(i)
    End Function

    ''' <summary>Ein Projekt aus einem `tests/&lt;schule&gt;`-Ordner, samt
    ''' Staenden aus `output/stundenplan.json` und `output/klassenbildung.json`,
    ''' soweit vorhanden. Die JSONs sind dieselben, die ein Lauf als Stand
    ''' sichert - der Viewer sieht keinen Unterschied.</summary>
    Public Function ProjektAusSchulordner(ordner As String, jetzt As DateTimeOffset) As Projekt
        Dim p = ProjektOrdner.Importieren(ordner, jetzt)
        Dim ausgabe = Path.Combine(ordner, "output")

        Dim kb = Path.Combine(ausgabe, "klassenbildung.json")
        If File.Exists(kb) Then
            p.StandHinzufuegen(New ProjektStand With {
                .Id = "bestand-klassenbildung", .Label = "Klassenbildung aus output/",
                .Erstellt = jetzt.AddMinutes(-2),
                .Klassenbildung = JsonNode.Parse(File.ReadAllText(kb)).AsObject()})
        End If
        Dim sp = Path.Combine(ausgabe, "stundenplan.json")
        If File.Exists(sp) Then
            p.StandHinzufuegen(New ProjektStand With {
                .Id = "bestand-stundenplan", .Label = "Stundenplan aus output/",
                .Erstellt = jetzt.AddMinutes(-1),
                .Stundenplan = JsonNode.Parse(File.ReadAllText(sp)).AsObject()})
        End If
        Return p
    End Function

    ''' <summary>Rendert den Inhalt eines angezeigten Fensters als PNG.
    ''' `einblendung` (ein von WPF nicht zeichenbares Steuerelement wie
    ''' WebView2) wird durch `einblendungBild` an seiner Stelle ersetzt.
    ''' Der Fensterhintergrund wird zuerst gemalt - der Inhalt allein
    ''' waere dort transparent, wo nichts zeichnet.</summary>
    Public Sub Speichern(fenster As Window, pfad As String,
                         Optional einblendung As FrameworkElement = Nothing,
                         Optional einblendungBild As BitmapSource = Nothing)
        ' Die WURZEL der Fenstervorlage, nicht Window.Content: der Inhalt
        ' traegt oft einen Rand (pad-platte), und ein Visual rendert an
        ' seiner eigenen Position - das Bild waere um den Rand versetzt
        ' und rechts/unten beschnitten.
        Dim wurzel = CType(VisualTreeHelper.GetChild(fenster, 0), FrameworkElement)
        Dim breite = Math.Max(1, CInt(Math.Ceiling(wurzel.ActualWidth)))
        Dim hoehe = Math.Max(1, CInt(Math.Ceiling(wurzel.ActualHeight)))

        Dim bild As New RenderTargetBitmap(breite, hoehe, 96, 96, PixelFormats.Pbgra32)
        Dim grund As New DrawingVisual()
        Using zeichner = grund.RenderOpen()
            zeichner.DrawRectangle(If(fenster.Background, Brushes.White), Nothing, New Rect(0, 0, breite, hoehe))
        End Using
        bild.Render(grund)
        bild.Render(wurzel)

        If einblendung IsNot Nothing AndAlso einblendungBild IsNot Nothing AndAlso
           einblendung.IsVisible AndAlso einblendung.ActualWidth > 0 Then
            Dim ecke = einblendung.TranslatePoint(New Point(0, 0), wurzel)
            Dim lage As New DrawingVisual()
            Using zeichner = lage.RenderOpen()
                zeichner.DrawImage(einblendungBild,
                                   New Rect(ecke.X, ecke.Y, einblendung.ActualWidth, einblendung.ActualHeight))
            End Using
            bild.Render(lage)
        End If

        Directory.CreateDirectory(Path.GetDirectoryName(pfad))
        Dim kodierer As New PngBitmapEncoder()
        kodierer.Frames.Add(BitmapFrame.Create(bild))
        Using strom = File.Create(pfad)
            kodierer.Save(strom)
        End Using
    End Sub

    ''' <summary>PNG-Strom (CapturePreview) in ein Bild fuer DrawImage.</summary>
    Public Function BildAus(strom As Stream) As BitmapSource
        strom.Position = 0
        Dim dekodierer As New PngBitmapDecoder(strom, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad)
        Return dekodierer.Frames(0)
    End Function

End Module
