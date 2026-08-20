' Phase 2.21: baut die "Stundentafel"-HTML-Seite (siehe Templates/
' stundentafel.html) aus einer bereits fertig gebauten JSON-Text-
' Darstellung von Formatting.ToStundentafelJson. Die Vorlage ist als
' Embedded Resource in die Assembly eingebettet (siehe
' SchoolTestRunner.vbproj), damit sie unabhaengig vom aktuellen
' Arbeitsverzeichnis lesbar ist - kein CopyToOutputDirectory-Pfadrisiko.
Public Module StundentafelHtml

    ' Live bestaetigt (Phase 2.21): trotz "Templates\stundentafel.html" als
    ' Include-Pfad im .vbproj ergibt sich KEIN "Templates."-Praefix im
    ' Ressourcennamen - SDK-style VB-Projekte scheinen bei
    ' EmbeddedResource-Items den Ordnerpfad nicht in den logischen Namen
    ' aufzunehmen (anders als das C#-Verhalten). Per
    ' Assembly.GetManifestResourceNames() live bestaetigt statt angenommen.
    Private Const ResourceName As String = "SchoolTestRunner.stundentafel.html"
    Private Const JsonPlaceholder As String = "__STUNDENTAFEL_JSON__"

    ''' <summary>Ersetzt den Platzhalter in der eingebetteten Vorlage durch
    ''' den uebergebenen JSON-Text. `jsonText` wird defensiv escaped
    ''' (`&lt;/script` -&gt; `&lt;\/script`) - Standard-Mitigation fuers
    ''' Einbetten von JSON in einen &lt;script&gt;-Block: `\/` ist ein
    ''' gueltiges JSON-Escape fuer `/`, `JSON.parse` wandelt es korrekt
    ''' zurueck, daher bleibt der Text funktional identisch.</summary>
    Public Function BuildStundentafelHtml(jsonText As String) As String
        Dim asm = Reflection.Assembly.GetExecutingAssembly()
        Using stream = asm.GetManifestResourceStream(ResourceName)
            If stream Is Nothing Then
                Throw New InvalidOperationException(
                    $"Embedded resource '{ResourceName}' nicht gefunden. Verfuegbar: {String.Join(", ", asm.GetManifestResourceNames())}")
            End If
            Using reader As New IO.StreamReader(stream)
                Dim template = reader.ReadToEnd()
                Dim escaped = jsonText.Replace("</script", "<\/script")
                Return template.Replace(JsonPlaceholder, escaped)
            End Using
        End Using
    End Function

End Module
