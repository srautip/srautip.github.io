' Phase 2.18: CLI-Einstiegspunkt fuer die code-freie Schul-Testfaelle
' (siehe tests/README.md). Erwartet, aus timetable-dotnet/ heraus
' aufgerufen zu werden (Standard-Arbeitsverzeichnis von `dotnet run`),
' damit "tests/" relativ zum Repo-Wurzelverzeichnis der neuen Faehigkeit
' aufgeloest wird.
'
' Aufrufe:
'   dotnet run --project SchoolTestRunner -- new <schule> --schulart <Grundschule|Gemeinschaftsschule> --bundesland BW --klassenstufen <N> --lehrer <N> [--zuege <N>]
'   dotnet run --project SchoolTestRunner -- run <schule>
'   dotnet run --project SchoolTestRunner -- run --all
Module Program

    Private Const TestsRoot As String = "tests"

    Private Function GetOption(args As String(), name As String) As String
        Dim idx = Array.IndexOf(args, name)
        If idx < 0 OrElse idx + 1 >= args.Length Then Return Nothing
        Return args(idx + 1)
    End Function

    Private Function RequireOption(args As String(), name As String) As String
        Dim v = GetOption(args, name)
        If v Is Nothing Then Throw New InvalidOperationException($"Fehlender Parameter {name}.")
        Return v
    End Function

    Private Sub PrintUsage()
        Console.WriteLine("Usage:")
        Console.WriteLine("  dotnet run --project SchoolTestRunner -- new <schule> --schulart <Grundschule|Gemeinschaftsschule> --bundesland BW --klassenstufen <N> --lehrer <N> [--zuege <N>]")
        Console.WriteLine("  dotnet run --project SchoolTestRunner -- run <schule>")
        Console.WriteLine("  dotnet run --project SchoolTestRunner -- run --all")
        Console.WriteLine("  dotnet run --project SchoolTestRunner -- render <schule>")
    End Sub

    ''' <summary>Viewer-Ausbau (docs/viewer-ausbau-plan.md, Leitplanke
    ''' "Regenerierung"): baut output/stundentafel.html aus der BEREITS
    ''' vorhandenen output/stundenplan.json neu - fuer reine
    ''' Template-Aenderungen am Viewer, ohne den (bei grossen Schulen
    ''' teuren) Solver-Lauf zu wiederholen. Fehlt die JSON, ist das ein
    ''' Fehler (erst `run` ausfuehren).</summary>
    Private Function RenderOne(schule As String) As Integer
        Dim jsonPath = IO.Path.Combine(TestsRoot, schule, "output", "stundenplan.json")
        If Not IO.File.Exists(jsonPath) Then
            Console.Error.WriteLine($"[{schule}] FAIL - {jsonPath} nicht gefunden (erst 'run' ausfuehren).")
            Return 1
        End If
        Dim node = System.Text.Json.Nodes.JsonNode.Parse(IO.File.ReadAllText(jsonPath))
        Dim htmlPath = IO.Path.Combine(TestsRoot, schule, "output", "stundentafel.html")
        IO.File.WriteAllText(htmlPath, StundentafelHtml.BuildStundentafelHtml(node.ToJsonString()))
        Console.WriteLine($"[{schule}] RENDER OK - {htmlPath} aus vorhandener stundenplan.json neu gebaut")
        Return 0
    End Function

    Function Main(args As String()) As Integer
        If args.Length = 0 Then
            PrintUsage()
            Return 1
        End If

        Try
            Select Case args(0)
                Case "new"
                    If args.Length < 2 Then Throw New InvalidOperationException("Fehlender Schulname.")
                    Dim schule = args(1)
                    Dim rest = args.Skip(2).ToArray()
                    Dim schulart = RequireOption(rest, "--schulart")
                    Dim bundesland = RequireOption(rest, "--bundesland")
                    Dim klassenstufen = Integer.Parse(RequireOption(rest, "--klassenstufen"))
                    Dim lehrer = Integer.Parse(RequireOption(rest, "--lehrer"))
                    Dim zuegeOpt = GetOption(rest, "--zuege")
                    Dim zuege = If(zuegeOpt Is Nothing, 2, Integer.Parse(zuegeOpt))
                    Scaffold.Run(TestsRoot, schule, bundesland, schulart, klassenstufen, lehrer, zuege)
                    Return 0

                Case "run"
                    If args.Length < 2 Then Throw New InvalidOperationException("Fehlender Schulname (oder --all).")
                    If args(1) = "--all" Then
                        Return If(Run.RunAll(TestsRoot), 0, 1)
                    Else
                        Return If(Run.RunOne(TestsRoot, args(1)), 0, 1)
                    End If

                Case "render"
                    If args.Length < 2 Then Throw New InvalidOperationException("Fehlender Schulname.")
                    Return RenderOne(args(1))

                Case Else
                    PrintUsage()
                    Return 1
            End Select
        Catch ex As InvalidOperationException
            Console.Error.WriteLine($"Fehler: {ex.Message}")
            Return 1
        End Try
    End Function

End Module
