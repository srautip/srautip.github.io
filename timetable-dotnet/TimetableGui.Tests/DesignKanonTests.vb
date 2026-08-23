' Waechter des Designsystems auf der WPF-Seite.
'
' Quelle der Wahrheit ist TimetableWorkflow/Templates/design-tokens.css.
' TimetableGui/Design/Tokens.xaml spiegelt davon den KANON - alles, was
' in beiden Welten gleichzeitig sichtbar ist und wo Drift deshalb ein
' echter Fehler waere. Ohne diese Tests waere "gespiegelt" nur eine
' Absichtserklaerung.
'
' Geprueft wird XAML -> CSS, nicht umgekehrt: jeder Eintrag drueben muss
' hier mit gleichem Wert existieren. Die Gegenrichtung waere KEINE
' Korrektheitseigenschaft - WPF muss nicht jede Farbe der Viewer kennen -
' sondern nur eine Liste, die selbst driften kann.
'
' Gelesen wird per XDocument, NICHT ueber WPF-ResourceDictionary: das
' Testprojekt laeuft headless (siehe Kommentar in seiner vbproj), und ein
' Pack-URI-Dictionary wuerde diese Zusage ohne Not antasten.
Imports System.Globalization
Imports System.Text.RegularExpressions
Imports System.Xml.Linq
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
Public Class DesignKanonTests

    ''' <summary>Praefixe, die den Kanon ausmachen. Bewusst eine REGEL und
    ''' keine Positivliste einzelner Namen - eine Liste waere selbst ein
    ''' Objekt, das driften kann. Nicht dabei: Schriftstapel (der
    ''' Web-Stapel enthaelt -apple-system und sans-serif, die es in WPF
    ''' nicht gibt), Masse in rem, Schriftgrade, Schatten, Dichte.</summary>
    Private Shared ReadOnly KanonPraefixe As String() = {"farbe-", "kat-", "radius-"}

    ' ---------------------------------------------------------------
    ' Quellen
    ' ---------------------------------------------------------------

    Private Shared ReadOnly Property RepoWurzel As String
        Get
            Dim dir = New IO.DirectoryInfo(AppContext.BaseDirectory)
            While dir IsNot Nothing
                If IO.Directory.Exists(IO.Path.Combine(dir.FullName, "tests", "bw-gms-beispiel")) Then Return dir.FullName
                dir = dir.Parent
            End While
            Throw New InvalidOperationException("Repo-Wurzel nicht gefunden")
        End Get
    End Property

    Private Shared ReadOnly Property GuiOrdner As String
        Get
            Return IO.Path.Combine(RepoWurzel, "TimetableGui")
        End Get
    End Property

    ''' <summary>Die Token-Quelle, gelesen aus der eingebetteten Ressource -
    ''' so haengt der Test nicht am Dateipfad, sondern am selben Weg, den
    ''' auch die Waechtertests der Vorlagen nehmen.</summary>
    Private Shared Function TokenQuelle() As String
        Dim asm = GetType(TimetableWorkflow.LaufErgebnis).Assembly
        Const name As String = "TimetableWorkflow.design-tokens.css"
        Using strom = asm.GetManifestResourceStream(name)
            If strom Is Nothing Then
                Throw New InvalidOperationException(
                    $"Ressource '{name}' fehlt. Vorhanden: {String.Join(", ", asm.GetManifestResourceNames())}")
            End If
            Using leser As New IO.StreamReader(strom)
                Return leser.ReadToEnd()
            End Using
        End Using
    End Function

    ''' <summary>Alle `--name: wert;` aus dem :root-Block. Bewusst ein
    ''' einfacher Ausdruck statt eines CSS-Parsers: die Quelle ist eine
    ''' eingecheckte Datei in bekannter Form, kein Fremdformat.</summary>
    Private Shared Function CssTokens() As Dictionary(Of String, String)
        Dim d As New Dictionary(Of String, String)(StringComparer.Ordinal)
        For Each m As Match In Regex.Matches(TokenQuelle(), "--([a-z0-9-]+)\s*:\s*([^;]+);")
            d(m.Groups(1).Value) = m.Groups(2).Value.Trim()
        Next
        Return d
    End Function

    Private Shared Function XamlTokens() As List(Of (Schluessel As String, Wert As String))
        Dim x = XDocument.Load(IO.Path.Combine(GuiOrdner, "Design", "Tokens.xaml"))
        Dim xNs As XNamespace = "http://schemas.microsoft.com/winfx/2006/xaml"
        Dim ergebnis As New List(Of (String, String))
        For Each el In x.Root.Elements()
            Dim schluessel = CStr(el.Attribute(xNs + "Key"))
            If schluessel Is Nothing Then Continue For
            ' SolidColorBrush traegt den Wert im Attribut, CornerRadius
            ' und die uebrigen Primitiven im Elementtext.
            Dim farbe = el.Attribute("Color")
            ergebnis.Add((schluessel, If(farbe IsNot Nothing, farbe.Value, el.Value.Trim())))
        Next
        Return ergebnis
    End Function

    ' ---------------------------------------------------------------
    ' Normalisierung - zwei Regeln, keine Zuordnungstabelle
    ' ---------------------------------------------------------------

    ''' <summary>#rgb / #rrggbb / #AARRGGBB auf achtstelliges ARGB in
    ''' Grossbuchstaben. Ein fehlender Alphakanal heisst deckend.</summary>
    Private Shared Function NormFarbe(w As String) As String
        Dim s = w.Trim().TrimStart("#"c).ToUpperInvariant()
        If s.Length = 3 Then s = String.Concat(s.Select(Function(c) New String(c, 2)))
        If s.Length = 6 Then s = "FF" & s
        Return s
    End Function

    ''' <summary>`4px` und `4` sind derselbe Radius. Mehr Laengenlogik
    ''' braucht der Kanon nicht - was rem oder Prozent verlangt, gehoert
    ''' nicht hinein.</summary>
    Private Shared Function NormLaenge(w As String) As String
        Return w.Trim().Replace("px", "").Trim()
    End Function

    Private Shared Function IstKanon(schluessel As String) As Boolean
        Return KanonPraefixe.Any(Function(p) schluessel.StartsWith(p, StringComparison.Ordinal))
    End Function

    ' ---------------------------------------------------------------
    ' Tests
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub JederKanonEintragInXamlStehtGleichlautendImCss()
        Dim css = CssTokens()
        Dim abweichungen As New List(Of String)
        Dim geprueft = 0

        For Each eintrag In XamlTokens()
            If Not IstKanon(eintrag.Schluessel) Then Continue For
            geprueft += 1

            If Not css.ContainsKey(eintrag.Schluessel) Then
                abweichungen.Add($"{eintrag.Schluessel}: in Tokens.xaml, aber NICHT in design-tokens.css")
                Continue For
            End If

            Dim istFarbe = eintrag.Wert.StartsWith("#", StringComparison.Ordinal)
            Dim links = If(istFarbe, NormFarbe(eintrag.Wert), NormLaenge(eintrag.Wert))
            Dim rechts = If(istFarbe, NormFarbe(css(eintrag.Schluessel)), NormLaenge(css(eintrag.Schluessel)))
            If links <> rechts Then
                abweichungen.Add($"{eintrag.Schluessel}: XAML '{eintrag.Wert}' <> CSS '{css(eintrag.Schluessel)}'")
            End If
        Next

        Assert.IsTrue(geprueft >= 15, $"Nur {geprueft} Kanon-Eintraege gefunden - der Test prueft offenbar nichts mehr.")
        Assert.AreEqual(0, abweichungen.Count,
            "Designsystem-Drift zwischen Tokens.xaml und design-tokens.css:" & vbLf &
            String.Join(vbLf, abweichungen) & vbLf &
            "Quelle ist die CSS-Datei; Tokens.xaml nachziehen.")
    End Sub

    ''' <summary>Fehlende StaticResource-Schluessel sind die einzige
    ''' WPF-Fehlerklasse, die weder der Compiler noch ein headless
    ''' laufender Test bemerkt - sie wirft erst beim Aufbau des Fensters,
    ''' also genau dort, wo hier niemand hinsehen kann (arc42 8.13).
    ''' Rein textuell geprueft, ohne WPF zu laden.</summary>
    <TestMethod>
    Public Sub JedeVerwendeteRessourceIstDefiniert()
        Dim definiert As New HashSet(Of String)(StringComparer.Ordinal)
        Dim xNs As XNamespace = "http://schemas.microsoft.com/winfx/2006/xaml"

        Dim dateien = IO.Directory.GetFiles(GuiOrdner, "*.xaml", IO.SearchOption.AllDirectories).
                          Where(Function(p) Not p.Contains(IO.Path.DirectorySeparatorChar & "obj" & IO.Path.DirectorySeparatorChar)).
                          ToList()
        Assert.IsTrue(dateien.Count >= 3, "Zu wenige XAML-Dateien gefunden - Pfadsuche stimmt nicht.")

        For Each pfad In dateien
            For Each el In XDocument.Load(pfad).Descendants()
                Dim k = el.Attribute(xNs + "Key")
                If k IsNot Nothing Then definiert.Add(k.Value)
            Next
        Next

        ' Von WPF selbst mitgebrachte Schluesselformen ({x:Type ...},
        ' SystemColors etc.) sind keine eigenen Ressourcen.
        Dim fehlend As New List(Of String)
        For Each pfad In dateien
            Dim text = IO.File.ReadAllText(pfad)
            For Each m As Match In Regex.Matches(text, "\{(?:Static|Dynamic)Resource\s+([^\}\s,]+)\s*\}")
                Dim name = m.Groups(1).Value
                If name.StartsWith("{", StringComparison.Ordinal) Then Continue For
                If Not definiert.Contains(name) Then
                    fehlend.Add($"{IO.Path.GetFileName(pfad)}: {{StaticResource {name}}}")
                End If
            Next
        Next

        Assert.AreEqual(0, fehlend.Count,
            "Nicht definierte Ressourcen - das wirft erst beim Fensteraufbau:" & vbLf & String.Join(vbLf, fehlend.Distinct()))
    End Sub

    ''' <summary>Jedes in XAML verwendete Symbolzeichen muss in BEIDEN
    ''' Schriften existieren. Fehlt eins in "Segoe MDL2 Assets", erscheint
    ''' es auf Windows 10 still als Kaestchen - ein Ausfall, den kein
    ''' anderer Test und kein Blick auf einem Windows-11-Rechner
    ''' bemerkt.</summary>
    <TestMethod>
    Public Sub SymbolzeichenGibtEsInBeidenSchriften()
        Dim zeichen As New HashSet(Of Integer)
        For Each pfad In IO.Directory.GetFiles(GuiOrdner, "*.xaml", IO.SearchOption.AllDirectories).
                             Where(Function(p) Not p.Contains(IO.Path.DirectorySeparatorChar & "obj" & IO.Path.DirectorySeparatorChar))
            For Each m As Match In Regex.Matches(IO.File.ReadAllText(pfad), "&#x(E[0-9A-Fa-f]{3}|F[0-8][0-9A-Fa-f]{2});")
                zeichen.Add(Integer.Parse(m.Groups(1).Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            Next
        Next
        Assert.IsTrue(zeichen.Count >= 6, $"Nur {zeichen.Count} Symbolzeichen gefunden - die Suche greift nicht mehr.")

        Dim schriften = {("Segoe MDL2 Assets", "segmdl2.ttf"), ("Segoe Fluent Icons", "SegoeIcons.ttf")}
        Dim fehlend As New List(Of String)
        Dim gepruefteSchriften = 0

        For Each s In schriften
            Dim pfad = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", s.Item2)
            If Not IO.File.Exists(pfad) Then Continue For
            gepruefteSchriften += 1
            Dim gt As New Windows.Media.GlyphTypeface(New Uri(pfad))
            For Each cp In zeichen
                If Not gt.CharacterToGlyphMap.ContainsKey(cp) Then
                    fehlend.Add($"U+{cp:X4} fehlt in {s.Item1}")
                End If
            Next
        Next

        If gepruefteSchriften = 0 Then
            Assert.Inconclusive("Keine der beiden Symbolschriften ist auf diesem Rechner installiert.")
        End If
        Assert.AreEqual(0, fehlend.Count,
            "Symbolzeichen ohne Glyphe - sie erscheinen still als Kaestchen:" & vbLf & String.Join(vbLf, fehlend))
    End Sub

    ''' <summary>Die Huelle darf keine Farbliterale mehr tragen. Dieser
    ''' Test SCHLIESST den Umbau ab: solange er rot ist, ist er nicht
    ''' fertig - und er verhindert den Rueckfall.</summary>
    <TestMethod>
    Public Sub KeineFarbliteraleAusserhalbDerTokendatei()
        Dim treffer As New List(Of String)
        For Each pfad In IO.Directory.GetFiles(GuiOrdner, "*.xaml", IO.SearchOption.AllDirectories).
                             Where(Function(p) Not p.Contains(IO.Path.DirectorySeparatorChar & "obj" & IO.Path.DirectorySeparatorChar))
            If IO.Path.GetFileName(pfad) = "Tokens.xaml" Then Continue For
            For Each zeile In IO.File.ReadAllLines(pfad)
                If Regex.IsMatch(zeile, "#[0-9A-Fa-f]{3,8}\b") AndAlso Not zeile.TrimStart().StartsWith("<!--", StringComparison.Ordinal) Then
                    treffer.Add($"{IO.Path.GetFileName(pfad)}: {zeile.Trim()}")
                End If
            Next
        Next
        Assert.AreEqual(0, treffer.Count,
            "Farbliterale ausserhalb von Tokens.xaml:" & vbLf & String.Join(vbLf, treffer))
    End Sub

End Class

''' <summary>Verhindert, dass die Token-Quelle zum Friedhof wird.
'''
''' Dieser Test steht HIER und nicht bei den Vorlagen-Waechtern, weil er
''' als einziger beide Welten sieht: TimetableGui.Tests referenziert
''' TimetableWorkflow (also die eingebetteten Vorlagen und die Quelle)
''' und liest zugleich die XAML-Dateien der Huelle. Ein Token darf von
''' irgendeinem der drei Verbraucher gebraucht werden.</summary>
<TestClass>
Public Class TokenVerbrauchTests

    ''' <summary>Die kategoriale Palette wird als FAMILIE ueber einen
    ''' berechneten Namen gelesen (`getPropertyValue('--kat-' + i)`), nicht
    ''' als sechzehn Einzelnamen. Eine Textsuche kann sie deshalb
    ''' grundsaetzlich nicht finden - das ist keine Ausnahme "weil es
    ''' gerade passt", sondern eine Eigenschaft der Zugriffsart. Geprueft
    ''' wird stattdessen, dass der Familienzugriff ueberhaupt existiert.</summary>
    Private Const FamilienPraefix As String = "kat-"

    <TestMethod>
    Public Sub KeinTokenBleibtUngenutzt()
        Dim wurzel = New IO.DirectoryInfo(AppContext.BaseDirectory)
        While wurzel IsNot Nothing AndAlso
              Not IO.Directory.Exists(IO.Path.Combine(wurzel.FullName, "TimetableWorkflow", "Templates"))
            wurzel = wurzel.Parent
        End While
        Assert.IsNotNull(wurzel, "Repo-Wurzel nicht gefunden")

        Dim vorlagen = IO.Path.Combine(wurzel.FullName, "TimetableWorkflow", "Templates")
        Dim quelle = IO.File.ReadAllText(IO.Path.Combine(vorlagen, "design-tokens.css"))

        ' Alle Verbraucher in einen Topf: beide Vorlagen, der Basis-Block
        ' und die XAML-Dateien der Huelle.
        Dim verbraucher As New Text.StringBuilder()
        For Each p In IO.Directory.GetFiles(vorlagen, "*.html").Concat(
                      IO.Directory.GetFiles(vorlagen, "design-basis.css")).Concat(
                      IO.Directory.GetFiles(IO.Path.Combine(wurzel.FullName, "TimetableGui"), "*.xaml",
                                            IO.SearchOption.AllDirectories).
                          Where(Function(x) Not x.Contains(IO.Path.DirectorySeparatorChar & "obj" & IO.Path.DirectorySeparatorChar)))
            verbraucher.Append(IO.File.ReadAllText(p))
        Next
        Dim alles = verbraucher.ToString()

        Dim familieBenutzt = alles.Contains("'--" & FamilienPraefix & "' +")

        Dim tot As New List(Of String)
        Dim gezaehlt = 0
        For Each m As Text.RegularExpressions.Match In
            Text.RegularExpressions.Regex.Matches(quelle, "--([a-z0-9-]+)\s*:")
            Dim name = m.Groups(1).Value
            gezaehlt += 1
            If name.StartsWith(FamilienPraefix, StringComparison.Ordinal) Then
                If Not familieBenutzt Then tot.Add("--" & name & " (Familienzugriff fehlt)")
                Continue For
            End If
            ' In XAML heisst der Schluessel wie der Token, nur ohne die
            ' zwei fuehrenden Minuszeichen.
            If alles.Contains("var(--" & name) OrElse alles.Contains("""" & name & """") Then Continue For
            tot.Add("--" & name)
        Next

        Assert.IsTrue(gezaehlt >= 40, $"Nur {gezaehlt} Token gefunden - die Quelle wurde offenbar nicht gelesen.")
        Assert.AreEqual(0, tot.Count,
            "Deklarierte, aber von niemandem verwendete Token:" & vbLf & String.Join(vbLf, tot.Distinct()) & vbLf &
            "Entweder verwenden oder aus design-tokens.css entfernen.")
    End Sub

End Class
