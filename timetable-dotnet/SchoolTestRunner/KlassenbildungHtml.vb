' Klassenbildung K5 (docs/klassenbildung-plan.md): baut die
' Klassenbildungs-Viewer-Seite (Templates/klassenbildung.html) aus der
' JSON-Darstellung von KlassenRun.BaueJson - identische Mechanik wie
' StundentafelHtml (Embedded Resource, JSON-Platzhalter, </script-Escape).
Public Module KlassenbildungHtml

    Private Const ResourceName As String = "SchoolTestRunner.klassenbildung.html"
    Private Const JsonPlaceholder As String = "__KLASSENBILDUNG_JSON__"

    Public Function BuildKlassenbildungHtml(jsonText As String) As String
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
