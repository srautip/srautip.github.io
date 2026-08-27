' Ein Waechter gegen einen Fehler, den ich an einem Tag DREIMAL gemacht
' habe (26.08.2026).
'
' U+201C - das schliessende deutsche Anfuehrungszeichen - ist in VB ein
' GUELTIGER STRINGBEGRENZER. `$"... Regel {id} nicht erfuellt."` mit
' typografischen Anfuehrungszeichen um `{id}` endet deshalb mitten im
' Ausdruck, und der Compiler meldet den Fehler irgendwo dahinter:
' "Character is not valid", "'For' must end with a matching 'Next'",
' "'Module' statement must end with a matching 'End Module'". Die
' Meldung zeigt nie auf die Ursache.
'
' EHRLICHE GRENZE: meistens bricht so ein Zeichen schon die
' UEBERSETZUNG - dann laeuft dieser Test gar nicht erst, und es
' bleibt bei der irrefuehrenden Compilermeldung. Der Waechter faengt
' die verbleibenden Faelle (das Zeichen steht in einem String, der
' trotzdem noch aufgeht) und dient sonst als Fundstelle: wer eine
' dieser Meldungen sieht, sucht zuerst danach. Die Diagnose steht
' in CLAUDE.md.
'
' In der OBERFLAECHE sind typografische Anfuehrungszeichen richtig und
' erwuenscht - deshalb steht das Verbot nur fuer VB-Quelltext, nicht
' fuer XAML. Dort sind Attributwerte in echte Anfuehrungszeichen
' gefasst, und U+201C ist harmlos.
Imports Microsoft.VisualStudio.TestTools.UnitTesting

<TestClass>
Public Class QuelltextWaechterTests

    Private Shared ReadOnly Property GuiQuellen As List(Of String)
        Get
            Dim wurzel = IO.Path.Combine(IO.Path.GetDirectoryName(TestsWurzel()), "TimetableGui")
            Return IO.Directory.GetFiles(wurzel, "*.vb", IO.SearchOption.AllDirectories).
                Where(Function(p) Not p.Contains(IO.Path.DirectorySeparatorChar & "obj" & IO.Path.DirectorySeparatorChar)).
                Where(Function(p) Not p.Contains(IO.Path.DirectorySeparatorChar & "bin" & IO.Path.DirectorySeparatorChar)).
                ToList()
        End Get
    End Property

    <TestMethod>
    Public Sub KeinTypografischesAnfuehrungszeichenImVbQuelltext()
        Const Schliessend As Char = ChrW(&H201C)
        Dim treffer As New List(Of String)

        For Each pfad In GuiQuellen
            Dim zeilen = IO.File.ReadAllLines(pfad)
            For i = 0 To zeilen.Length - 1
                If zeilen(i).IndexOf(Schliessend) < 0 Then Continue For
                ' In Kommentaren ist es harmlos - dort endet ohnehin
                ' nichts, und der Fliesstext soll lesbar bleiben.
                If zeilen(i).TrimStart().StartsWith("'") Then Continue For
                treffer.Add($"{IO.Path.GetFileName(pfad)}:{i + 1}: {zeilen(i).Trim()}")
            Next
        Next

        Assert.AreEqual(0, treffer.Count,
            "U+201C ist in VB ein gueltiger Stringbegrenzer - der String endet dort, " &
            "und der Compilerfehler erscheint irgendwo dahinter:" & vbLf &
            String.Join(vbLf, treffer))
    End Sub

    ''' <summary>Dass der Waechter ueberhaupt etwas sieht - sonst waere
    ''' er nach einer Umbenennung der Ordner stillschweigend
    ''' wirkungslos.</summary>
    <TestMethod>
    Public Sub DerWaechterFindetDieQuellenUeberhaupt()
        Assert.IsTrue(GuiQuellen.Count > 20, $"nur {GuiQuellen.Count} Quelldateien gefunden")
    End Sub

End Class
