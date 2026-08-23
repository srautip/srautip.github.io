' `klassen <schule>`-Subkommando. Seit dem GUI-Unterbau nur noch die
' Datei- und Konsolenschicht um KlassenbildungLauf.Ausfuehren; die
' Orchestrierung liegt in TimetableWorkflow, damit CLI und Phase-3-GUI
' dieselbe benutzen.
'
' Bewusst NICHT in Run.RunOne integriert: Stufe 0 ist eigenstaendig,
' Schulen ohne klassenbildung.yaml bleiben unberuehrt.
Imports System.Text.Json
Imports TimetableCore

Public Module KlassenRun

    Public Function KlassenOne(testsRoot As String, schule As String) As Boolean
        Dim inputDir = IO.Path.Combine(testsRoot, schule, "input")
        Dim outputDir = IO.Path.Combine(testsRoot, schule, "output")
        Dim inputPath = IO.Path.Combine(inputDir, "klassenbildung.yaml")
        If Not IO.File.Exists(inputPath) Then
            Console.Error.WriteLine($"[{schule}] FAIL - {inputPath} nicht gefunden.")
            Return False
        End If
        IO.Directory.CreateDirectory(outputDir)

        Dim input = YamlKlassenbildung.LoadKlassenbildungYaml(inputPath)
        Dim cfg = LoadConfig(IO.Path.Combine(inputDir, "config.yaml"))

        Dim e = KlassenbildungLauf.Ausfuehren(input, cfg.Klassenbildung, cfg.Seed, cfg.NumWorkers)

        Dim md = KlassenbildungLauf.BaueBerichtMarkdown(schule, e)
        If md IsNot Nothing Then IO.File.WriteAllText(IO.Path.Combine(outputDir, "klassenbildung.md"), md)

        Select Case e.Stufe
            Case KlassenbildungStufe.Regelpruefung
                Console.WriteLine($"[{schule}] FAIL - ValidateKlassenbildung: {e.Meldungen.Count} Fehler")
                Return False
            Case KlassenbildungStufe.Varianten
                Console.WriteLine($"[{schule}] FAIL - SolveKlassenbildungTop: {e.Top.Varianten(0).Status}")
                Return False
            Case KlassenbildungStufe.Nachpruefung
                Console.WriteLine($"[{schule}] FAIL - {e.Meldungen.FirstOrDefault()}")
                Return False
        End Select

        Dim json = KlassenbildungLauf.BaueViewerJson(schule, e)
        IO.File.WriteAllText(IO.Path.Combine(outputDir, "klassenbildung.json"),
            json.ToJsonString(New JsonSerializerOptions With {.WriteIndented = True}))
        ' K5: self-contained Viewer (Varianten-Uebersicht, Board mit
        ' Ampel-Chips, Konsens-Markierung, Varianten-Diff).
        IO.File.WriteAllText(IO.Path.Combine(outputDir, "klassenbildung.html"),
            KlassenbildungHtml.BuildKlassenbildungHtml(json.ToJsonString()))

        Console.WriteLine($"[{schule}] PASS - Klassenbildung: {e.Geloeste.Count} Variante(n) " &
            $"(beste Objective={e.Geloeste(0).Objective}, Status={e.Geloeste(0).Status}), " &
            $"Konsens-Kern {e.Top.KonsensKern.Count}/{input.Schueler.Count} Kinder")
        Return True
    End Function

End Module
