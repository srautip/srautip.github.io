' Klassenbildung K4 (docs/klassenbildung-plan.md): das eigenstaendige
' `klassen <schule>`-Subkommando - laedt input/klassenbildung.yaml,
' validiert fail-fast, rechnet die Varianten-Schleife und schreibt
' output/klassenbildung.md (menschenlesbarer Report inkl. Scorecards,
' Konsens-Kern und Verletzungsreport - die Dokumentationsgrundlage der
' menschlichen Letztentscheidung) + output/klassenbildung.json
' (maschinenlesbar fuer den spaeteren Viewer, K5). Bewusst NICHT in
' Run.RunOne integriert: Stufe 0 ist eigenstaendig, Schulen ohne
' klassenbildung.yaml bleiben unberuehrt.
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
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
        Dim kb = If(cfg.Klassenbildung, New KlassenbildungConfig())

        Dim errors = Klassenbildung.ValidateKlassenbildung(input)
        If errors.Count > 0 Then
            IO.File.WriteAllText(IO.Path.Combine(outputDir, "klassenbildung.md"),
                $"# Klassenbildung: {schule}{vbLf}{vbLf}**Status:** Validierung FEHLGESCHLAGEN{vbLf}{vbLf}" &
                String.Join(vbLf, errors.Select(Function(e) $"- {e}")))
            Console.WriteLine($"[{schule}] FAIL - ValidateKlassenbildung: {errors.Count} Fehler")
            Return False
        End If

        Dim zeitlimit = If(kb.ZeitlimitS, 30.0)
        Dim nVarianten = If(kb.NVarianten, 3)
        Dim epsilon = If(kb.Epsilon, 0.05)
        Dim minDistanz = If(kb.MinDistanz, 8)
        Dim symmetriebrechung = If(kb.Symmetriebrechung, True)
        Dim top = Klassenbildung.SolveKlassenbildungTop(input,
            zeitlimitS:=zeitlimit, seed:=cfg.Seed, numWorkers:=cfg.NumWorkers,
            prioGewichte:=kb.PrioGewichte, symmetriebrechung:=symmetriebrechung,
            nVarianten:=nVarianten, epsilon:=epsilon, minDistanz:=minDistanz)

        Dim geloeste = top.Varianten.Where(Function(v) v.Zuordnung IsNot Nothing).ToList()
        If geloeste.Count = 0 Then
            IO.File.WriteAllText(IO.Path.Combine(outputDir, "klassenbildung.md"),
                $"# Klassenbildung: {schule}{vbLf}{vbLf}**Status:** {top.Varianten(0).Status} (keine Loesung - harte Regeln/Fixierungen kollidieren; Konfliktkern-Analyse siehe Plan K6){vbLf}")
            Console.WriteLine($"[{schule}] FAIL - SolveKlassenbildungTop: {top.Varianten(0).Status}")
            Return False
        End If

        ' Unabhaengige Nachpruefung (Verifier-Prinzip): der Bewertungslauf
        ' muss die Solver-Verletzungen exakt reproduzieren.
        Dim bewertungen As New List(Of KlassenbildungBewertung)
        For Each v In geloeste
            Dim bewertung = KlassenbildungQuality.Bewerte(input, v.Zuordnung)
            bewertungen.Add(bewertung)
            For Each sv In v.Verletzungen
                Dim unabhaengig = bewertung.Verletzungen.Single(Function(b) b.RegelId = sv.RegelId)
                If unabhaengig.Mass <> sv.Mass Then
                    Console.WriteLine($"[{schule}] FAIL - Bewertung widerspricht Solver: Regel {sv.RegelId} Solver={sv.Mass} Bewertung={unabhaengig.Mass}")
                    Return False
                End If
            Next
        Next

        Dim md = BaueMarkdown(schule, input, top, geloeste, bewertungen, zeitlimit, nVarianten, epsilon, minDistanz)
        IO.File.WriteAllText(IO.Path.Combine(outputDir, "klassenbildung.md"), md)

        Dim json = BaueJson(schule, input, top, geloeste, bewertungen, zeitlimit, epsilon, minDistanz)
        IO.File.WriteAllText(IO.Path.Combine(outputDir, "klassenbildung.json"),
            json.ToJsonString(New JsonSerializerOptions With {.WriteIndented = True}))
        ' K5: self-contained Viewer (Varianten-Uebersicht, Board mit
        ' Ampel-Chips, Konsens-Markierung, Varianten-Diff).
        IO.File.WriteAllText(IO.Path.Combine(outputDir, "klassenbildung.html"),
            KlassenbildungHtml.BuildKlassenbildungHtml(json.ToJsonString()))

        Console.WriteLine($"[{schule}] PASS - Klassenbildung: {geloeste.Count} Variante(n) " &
            $"(beste Objective={geloeste(0).Objective}, Status={geloeste(0).Status}), " &
            $"Konsens-Kern {top.KonsensKern.Count}/{input.Schueler.Count} Kinder")
        Return True
    End Function

End Module
