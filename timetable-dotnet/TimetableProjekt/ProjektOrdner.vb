' Import und Export zwischen Projektdatei und dem vorhandenen
' `tests/<schule>/`-Ordnerlayout (docs/gui-datenhaltung-konzept.md 8.2,
' Anforderung A6).
'
' Die YAML-Dateien bleiben AUSTAUSCHFORMAT, nicht Speicherformat. Der Sinn
' dieser beiden Richtungen ist, dass der komplette CLI-Weg
' (`run`/`klassen`/`render`) als zweiter, skriptbarer Kanal erhalten bleibt
' und die committeten Beispielschulen unveraendert nutzbar sind.
'
' Der Import laedt bewusst ueber DIESELBEN Lader wie die CLI - ein eigener
' Konverter waere eine zweite Interpretation derselben Dateien und damit
' eine Quelle stiller Abweichungen.
Imports System.IO
Imports TimetableCore
Imports TimetableYaml

Public Module ProjektOrdner

    ''' <summary>Liest einen `tests/&lt;schule&gt;/input/`-Ordner in ein
    ''' frisches Projekt. Fehlende optionale Dateien (constraints,
    ''' klassenbildung, config) sind zulaessig und ergeben leere bzw.
    ''' Default-Bestandteile - genau wie beim CLI-Lauf.
    '''
    ''' Vergibt KEINE Mapping-Eintraege: was aus den YAML-Fixtures kommt,
    ''' ist pseudonym und soll es bleiben (Konzept 8.3 - die committeten
    ''' Fixtures sind synthetisch und ohne Personenbezug).</summary>
    Public Function Importieren(schulOrdner As String, jetzt As DateTimeOffset) As Projekt
        Dim inputDir = IO.Path.Combine(schulOrdner, "input")
        If Not Directory.Exists(inputDir) Then
            Throw New DirectoryNotFoundException($"{inputDir} nicht gefunden - erwartet wird das tests/<schule>/-Layout.")
        End If
        Dim stammdatenPfad = IO.Path.Combine(inputDir, "stammdaten.yaml")
        If Not File.Exists(stammdatenPfad) Then
            Throw New FileNotFoundException($"{stammdatenPfad} nicht gefunden - ohne Stammdaten gibt es kein Projekt.", stammdatenPfad)
        End If

        Dim projekt As New Projekt()
        projekt.Bestand = YamlStammdaten.LoadStammdatenYaml(stammdatenPfad)
        projekt.Constraints = YamlConstraints.LoadConstraintsYaml(IO.Path.Combine(inputDir, "constraints.yaml"))
        projekt.Config = YamlConfig.LoadConfig(IO.Path.Combine(inputDir, "config.yaml"))

        Dim kbPfad = IO.Path.Combine(inputDir, "klassenbildung.yaml")
        If File.Exists(kbPfad) Then projekt.Klassenbildung = YamlKlassenbildung.LoadKlassenbildungYaml(kbPfad)

        projekt.Manifest.SchulName = projekt.Bestand.SchulName
        projekt.Manifest.Angelegt = jetzt
        projekt.Manifest.Geaendert = jetzt
        ' Der ID-Zaehler muss ueber ALLEN vorhandenen Ids liegen, sonst
        ' vergaebe NeueSchuelerId eine bereits benutzte - und genau das
        ' soll nie passieren (Konzept 6.1, "verbrannte" Ids).
        projekt.Manifest.NaechsteSchuelerNummer = HoechsteNummer(projekt) + 1

        Return projekt
    End Function

    ''' <summary>Schreibt die Eingabeseite des Projekts als
    ''' `&lt;ziel&gt;/input/*.yaml`. `klassenbildung.yaml` entsteht nur, wenn
    ''' es ueberhaupt Einschulungsdaten gibt - eine leere Datei wuerde den
    ''' CLI-Lauf sonst zu einem sinnlosen `klassen`-Aufruf einladen.</summary>
    Public Sub Exportieren(projekt As Projekt, zielOrdner As String)
        Dim inputDir = IO.Path.Combine(zielOrdner, "input")
        Directory.CreateDirectory(inputDir)

        YamlStammdaten.SaveStammdatenYaml(projekt.Bestand, IO.Path.Combine(inputDir, "stammdaten.yaml"))
        YamlConstraints.SaveConstraintsYaml(projekt.Constraints, IO.Path.Combine(inputDir, "constraints.yaml"))
        YamlConfig.SaveConfig(projekt.Config, IO.Path.Combine(inputDir, "config.yaml"))

        If projekt.Klassenbildung IsNot Nothing AndAlso projekt.Klassenbildung.Schueler.Count > 0 Then
            YamlKlassenbildung.SaveKlassenbildungYaml(projekt.Klassenbildung, IO.Path.Combine(inputDir, "klassenbildung.yaml"))
        End If
    End Sub

    ''' <summary>Hoechste bereits vergebene Schueler-Nummer ueber alle
    ''' Quellen. Ids ohne das S000-Muster werden ignoriert - sie stammen
    ''' dann aus einer Fremdquelle und stoeren die Zaehlung nicht.</summary>
    Private Function HoechsteNummer(projekt As Projekt) As Integer
        Dim alle = projekt.Klassenbildung.Schueler.Select(Function(s) s.Id).
            Concat(projekt.Bestand.Schueler.Select(Function(s) s.Id)).
            Concat(projekt.Mapping.Select(Function(m) m.Id))
        Dim hoechste = 0
        For Each id In alle
            If String.IsNullOrEmpty(id) OrElse Not id.StartsWith("S", StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim nummer As Integer
            If Integer.TryParse(id.Substring(1), Globalization.NumberStyles.Integer,
                                Globalization.CultureInfo.InvariantCulture, nummer) Then
                hoechste = Math.Max(hoechste, nummer)
            End If
        Next
        Return hoechste
    End Function

End Module
