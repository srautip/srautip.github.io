' Waechter des Designsystems auf der Viewer-Seite. Braucht weder Browser
' noch Generator noch JSON - liest die eingebetteten Ressourcen direkt und
' laeuft in Millisekunden. Das ist das Arbeitspferd des Umbaus.
'
' Quelle ist Templates/design-tokens.css. Jede Vorlage traegt eine
' ZEICHENGLEICHE Kopie des Blocks zwischen den Markern. Warum Kopie und
' nicht Injektion zur Generierungszeit, steht im Kopf der CSS-Datei.
'
' Welche Vorlagen geprueft werden, ergibt sich aus dem MARKER, nicht aus
' einer Liste: eine Vorlage ohne Token-Region wurde noch nicht umgestellt
' und wird uebersprungen. So waechst die Pruefung mit dem Umbau mit,
' ohne dass jemand eine Liste nachziehen muss.
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
Public Class DesignTokenTests

    Private Const QuellName As String = "TimetableWorkflow.design-tokens.css"
    Private Shared ReadOnly VorlagenNamen As String() =
        {"TimetableWorkflow.stundentafel.html", "TimetableWorkflow.klassenbildung.html"}

    ''' <summary>`--hl` wird ausschliesslich aus JS am Element gesetzt
    ''' (klassenbildung.html: style.setProperty) und ist deshalb nirgends
    ''' deklariert. Die einzige Ausnahme, und sie ist begruendet.</summary>
    Private Shared ReadOnly OhneDeklaration As String() = {"hl"}

    ' ---------------------------------------------------------------

    Private Shared Function Ressource(name As String) As String
        Dim asm = GetType(TimetableWorkflow.LaufErgebnis).Assembly
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

    ''' <summary>Zeilenenden vereinheitlichen. Die Quelle liegt mit LF im
    ''' Arbeitsbaum, die Vorlagen mit CRLF (core.autocrlf=true) - ohne
    ''' diese Normalisierung waere der Test auf dem einen Rechner gruen
    ''' und auf dem anderen rot.</summary>
    Private Shared Function Norm(s As String) As String
        Return s.Replace(vbCrLf, vbLf)
    End Function

    ''' <summary>Die beiden geteilten Bloecke. Ihre Namen sind zugleich
    ''' ihre Marker - eine Vorlage ohne den Marker wurde noch nicht
    ''' umgestellt und wird uebersprungen. So waechst die Pruefung mit
    ''' dem Umbau mit, ohne dass jemand eine Liste nachziehen muss.</summary>
    Private Shared ReadOnly Bloecke As (Marke As String, Quelle As String)() = {
        ("DESIGN-TOKENS", "TimetableWorkflow.design-tokens.css"),
        ("DESIGN-BASIS", "TimetableWorkflow.design-basis.css")
    }

    Private Shared Function Region(text As String, Optional marke As String = "DESIGN-TOKENS") As String
        Dim m = Regex.Match(Norm(text),
            $"/\* == {marke}:.*?/\* == ENDE {marke} == \*/", RegexOptions.Singleline)
        If Not m.Success Then Return Nothing
        Return m.Value
    End Function

    ''' <summary>CSS-Kommentare entfernen. Sie nennen bewusst Altwerte
    ''' und Gegenbeispiele zur Begruendung - Lints duerfen darauf nicht
    ''' anschlagen.</summary>
    Private Shared Function OhneKommentare(s As String) As String
        Return Regex.Replace(s, "/\*.*?\*/", "", RegexOptions.Singleline)
    End Function

    Private Shared Function StilBlock(html As String) As String
        Dim m = Regex.Match(Norm(html), "<style>(.*?)</style>", RegexOptions.Singleline)
        Assert.IsTrue(m.Success, "kein <style>-Block gefunden")
        Return m.Groups(1).Value
    End Function

    ''' <summary>Nur die Vorlagen, die bereits eine Token-Region tragen.</summary>
    Private Shared Iterator Function Umgestellte() As IEnumerable(Of (Name As String, Html As String))
        For Each n In VorlagenNamen
            Dim h = Ressource(n)
            If Region(h) IsNot Nothing Then Yield (n, h)
        Next
    End Function

    ' ---------------------------------------------------------------
    ' Tests
    ' ---------------------------------------------------------------

    <TestMethod>
    Public Sub JedeRegionIstZeichengleichZurQuelle()
        Dim geprueft = 0
        For Each b In Bloecke
            Dim soll = Region(Ressource(b.Quelle), b.Marke)
            Assert.IsNotNull(soll, $"In {b.Quelle} fehlen die {b.Marke}-Marker.")

            For Each v In Umgestellte()
                geprueft += 1
                Dim ist = Region(v.Html, b.Marke)
                Assert.AreEqual(soll, ist,
                    $"Die {b.Marke}-Region in {v.Name} weicht von {b.Quelle} ab." & vbLf &
                    "Quelle ist die CSS-Datei. Ersetze den Block zwischen den Markern durch:" & vbLf &
                    vbLf & soll)
            Next
        Next
        Assert.IsTrue(geprueft >= 4, $"Nur {geprueft} Regionen geprueft - erwartet: 2 Bloecke x 2 Vorlagen.")
    End Sub

    ''' <summary>Die Basis-Regeln MUESSEN vor den vorlageneigenen stehen.
    ''' `#meta` und `#controls` sind ID-Selektoren; kaeme die Basis
    ''' danach, wuerde sie jede spezifischere Ueberschreibung still
    ''' clobbern - ein Fehler, den man im Bild sieht, aber nicht im
    ''' Diff.</summary>
    <TestMethod>
    Public Sub DieBasisStehtVorDenEigenenRegeln()
        For Each v In Umgestellte()
            Dim stil = Norm(StilBlock(v.Html))
            Dim tok = stil.IndexOf("== DESIGN-TOKENS:", StringComparison.Ordinal)
            Dim bas = stil.IndexOf("== DESIGN-BASIS:", StringComparison.Ordinal)
            Dim endeBas = stil.IndexOf("== ENDE DESIGN-BASIS ==", StringComparison.Ordinal)
            Assert.IsTrue(tok >= 0 AndAlso bas > tok, $"{v.Name}: Reihenfolge Tokens -> Basis verletzt.")

            ' Nach dem Basis-Block darf nichts mehr kommen, was ihn
            ' wiederholt - sonst waere die Deduplizierung nur halb.
            Dim danach = stil.Substring(endeBas)
            For Each sel In {"  body {", "  h1 {", "  h2 {", "  #meta {", "  #controls {", "  #compare-info {"}
                Assert.IsFalse(danach.Contains(sel),
                    $"{v.Name}: '{sel.Trim()}' steht erneut NACH dem Basis-Block - dann gewinnt die Kopie.")
            Next
        Next
    End Sub

    ''' <summary>Der wertvollste Test dieser Klasse. Ein Tippfehler in
    ''' `var(--farbe-rahmn)` ist in CSS KEIN Fehler: die Deklaration wird
    ''' "invalid at computed-value time" und faellt still auf inherit
    ''' zurueck. Ein optischer Defekt, den weder der Browser meldet noch
    ''' ein Blick zuverlaessig faengt.</summary>
    <TestMethod>
    Public Sub JedeVariableHatEineDeklaration()
        For Each v In Umgestellte()
            Dim stil = StilBlock(v.Html)
            Dim deklariert As New HashSet(Of String)(StringComparer.Ordinal)
            For Each m As Match In Regex.Matches(stil, "--([a-z0-9-]+)\s*:")
                deklariert.Add(m.Groups(1).Value)
            Next
            For Each n In OhneDeklaration
                deklariert.Add(n)
            Next

            Dim fehlend As New List(Of String)
            For Each m As Match In Regex.Matches(stil, "var\(\s*--([a-z0-9-]+)")
                If Not deklariert.Contains(m.Groups(1).Value) Then fehlend.Add("--" & m.Groups(1).Value)
            Next
            Assert.AreEqual(0, fehlend.Count,
                $"{v.Name}: verwendete Variablen ohne Deklaration - sie fallen still auf inherit zurueck: " &
                String.Join(", ", fehlend.Distinct()))
        Next
    End Sub

    ''' <summary>Schliesst den Umbau ab: solange hier noch etwas steht,
    ''' ist eine Vorlage nicht fertig umgestellt - und danach verhindert
    ''' der Test den Rueckfall.</summary>
    <TestMethod>
    Public Sub KeineFarbliteraleAusserhalbDerRegion()
        For Each v In Umgestellte()
            Dim stil = StilBlock(v.Html)
            Dim rest = stil
            For Each b In Bloecke
                rest = rest.Replace(Norm(Region(v.Html, b.Marke)), "")
            Next
            ' Kommentare zaehlen nicht - sie nennen die Altwerte zur
            ' Begruendung, und genau das soll erhalten bleiben.
            rest = Regex.Replace(rest, "/\*.*?\*/", "", RegexOptions.Singleline)

            Dim treffer As New List(Of String)
            For Each m As Match In Regex.Matches(rest, "#[0-9a-fA-F]{3,8}\b|rgba?\([^)]*\)")
                treffer.Add(m.Value)
            Next
            Assert.AreEqual(0, treffer.Count,
                $"{v.Name}: Farbliterale ausserhalb der Token-Region: " & String.Join(", ", treffer.Distinct()))
        Next
    End Sub

    ''' <summary>Sobald die Reihenfolge stimmt, wird !important nicht
    ''' gebraucht; sobald es einmal drinsteht, ist die Reihenfolge nicht
    ''' mehr diagnostizierbar. Heute ist es in keiner Vorlage vorhanden -
    ''' das ist eine Konservierung, kein Umbau.
    '''
    ''' Kommentare werden vorher entfernt: die erste Fassung schlug auf
    ''' die eigene BEGRUENDUNG an ("...statt ueber transition: none
    ''' !important..."). Ein Waechter, der seine Dokumentation anzeigt,
    ''' erzieht dazu, ihn wegzulassen.</summary>
    <TestMethod>
    Public Sub KeinImportant()
        For Each n In VorlagenNamen
            Dim ohneKommentar = OhneKommentare(StilBlock(Ressource(n)))
            Dim treffer = Regex.Matches(ohneKommentar, "!\s*important").Count
            Assert.AreEqual(0, treffer, $"{n}: !important gefunden.")
        Next
    End Sub

End Class
