' `run`-Subcommand. Seit Stufe B des GUI-Unterbaus nur noch die Datei- und
' Konsolenschicht um StundenplanLauf.Ausfuehren: Eingaben laden, Dienst
' aufrufen, Berichte schreiben, Statuszeile ausgeben. Die Orchestrierung
' selbst (und damit alles, was zwischen CLI und Phase-3-GUI driften
' koennte) liegt in TimetableWorkflow.
'
' Unveraendert bleibt das Prinzip "kein Alles-oder-Nichts": nach jeder
' erreichten Stufe wird der bisherige Fortschritt geschrieben, ein Abbruch
' in einer spaeten Stufe loescht nicht die frueheren Ergebnisse.
Imports System.Text.Json
Imports Google.OrTools.Sat
Imports TimetableCore

Public Module Run

    ''' <summary>Fuehrt die Pipeline fuer genau eine Schule aus. Liefert
    ''' True bei vollstaendigem Erfolg (alle Stufen Optimal/Feasible, 0
    ''' Verstoesse), False sonst - der Aufrufer setzt daraus den
    ''' Exitcode.</summary>
    Public Function RunOne(testsRoot As String, schule As String) As Boolean
        Dim inputDir = IO.Path.Combine(testsRoot, schule, "input")
        Dim outputDir = IO.Path.Combine(testsRoot, schule, "output")
        Dim stammdatenPath = IO.Path.Combine(inputDir, "stammdaten.yaml")

        If Not IO.File.Exists(stammdatenPath) Then
            Console.WriteLine($"[{schule}] FAIL - {stammdatenPath} nicht gefunden")
            Return False
        End If
        IO.Directory.CreateDirectory(outputDir)

        Dim bestand = YamlStammdaten.LoadStammdatenYaml(stammdatenPath)
        Dim handConstraints = YamlConstraints.LoadConstraintsYaml(IO.Path.Combine(inputDir, "constraints.yaml"))
        Dim cfg = LoadConfig(IO.Path.Combine(inputDir, "config.yaml"))

        Dim e = StundenplanLauf.Ausfuehren(bestand, handConstraints, cfg)

        ' Berichte schreiben, soweit die jeweilige Stufe erreicht wurde.
        Dim lehrerMd = StundenplanBericht.BaueLehrerzuteilungMarkdown(e)
        If lehrerMd IsNot Nothing Then
            IO.File.WriteAllText(IO.Path.Combine(outputDir, "lehrerzuteilung.md"), lehrerMd)
        End If
        Dim planMd = StundenplanBericht.BaueStundenplanMarkdown(e)
        If planMd IsNot Nothing Then
            IO.File.WriteAllText(IO.Path.Combine(outputDir, "stundenplan.md"), planMd)
        End If

        If Not MeldeFehler(schule, e) Then Return False

        ' Phase 2.21: "Stundentafel"-Visualisierung - eine JSON-Datei mit
        ' ALLEN von SolveTop gefundenen Loesungen plus die dazugehoerige
        ' Viewer-HTML, die dieselben Daten inline eingebettet enthaelt (kein
        ' fetch() noetig, funktioniert daher auch beim Oeffnen per
        ' Doppelklick ohne lokalen Webserver).
        Dim stundentafelJson = StundenplanBericht.BaueStundentafelJson(e)
        IO.File.WriteAllText(IO.Path.Combine(outputDir, "stundenplan.json"),
            stundentafelJson.ToJsonString(New JsonSerializerOptions With {.WriteIndented = True}))
        IO.File.WriteAllText(IO.Path.Combine(outputDir, "stundentafel.html"),
            StundentafelHtml.BuildStundentafelHtml(stundentafelJson.ToJsonString()))

        Dim lehrereinsatz = e.Einsaetze(0)
        Dim best = e.BesteLoesung
        Dim zuteilungsInfo = If(e.Einsaetze.Count > 1, $", Zuteilungen={e.Einsaetze.Count} (beste={e.BesterLauf.AssignmentIndex})", "")
        Console.WriteLine($"[{schule}] {If(e.Erfolgreich, "PASS", "FAIL")} - Lehrereinsatzplanung={lehrereinsatz.Status} (Objective={lehrereinsatz.Solver.ObjectiveValue}){zuteilungsInfo}, Solver.SolveTop={e.BesterLauf.Result.StopReason}, CP-SAT-Status={best.Status} (Kann-Verstoesse={best.Quality.KannViolationCount}, Quality.Total={best.Quality.Total:F1}), Verstoesse={e.PlanVerstoesse.Count}")
        Return e.Erfolgreich
    End Function

    ''' <summary>Gibt die Fehlermeldung der gescheiterten Stufe aus.
    ''' Liefert False, wenn der Lauf abgebrochen wurde (dann hat RunOne
    ''' nichts mehr zu schreiben), True wenn er bis zur Stundenplan-Stufe
    ''' durchgelaufen ist.</summary>
    Private Function MeldeFehler(schule As String, e As LaufErgebnis) As Boolean
        Select Case e.Stufe
            Case LaufStufe.Stammdatenpruefung
                Console.WriteLine($"[{schule}] FAIL - StammdatenValidation: {e.Meldungen.Count} Fehler")
                Return False
            Case LaufStufe.Lehrereinsatz
                Dim status = If(e.Einsaetze.Count > 0, e.Einsaetze(0).Status.ToString(), "Abgebrochen")
                Console.WriteLine($"[{schule}] FAIL - Lehrereinsatzplanung: {status}")
                Return False
            Case LaufStufe.Lehrereinsatzpruefung
                Console.WriteLine($"[{schule}] FAIL - VerifyLehrereinsatz: {e.LehrereinsatzVerstoesse} Verstoesse")
                Return False
            Case LaufStufe.Szenarienaufbau
                Console.WriteLine($"[{schule}] FAIL - Validation.ValidateEntities (Zuteilung {e.FehlerZuteilung}): {e.Meldungen.Count} Fehler")
                Return False
            Case LaufStufe.Stundenplan
                Dim reasons = String.Join("|", e.Laeufe.Select(Function(r) r.Result.StopReason.ToString()).Distinct())
                Console.WriteLine($"[{schule}] FAIL - Solver.SolveTop: {reasons}")
                Return False
            Case Else
                Return True
        End Select
    End Function

    ''' <summary>`--all`: iteriert ueber jedes Unterverzeichnis von
    ''' testsRoot mit einer input/stammdaten.yaml. Liefert True nur, wenn
    ''' ALLE Schulen erfolgreich waren.</summary>
    Public Function RunAll(testsRoot As String) As Boolean
        If Not IO.Directory.Exists(testsRoot) Then
            Console.WriteLine($"'{testsRoot}' existiert nicht.")
            Return False
        End If
        Dim schulen = IO.Directory.GetDirectories(testsRoot).
            Where(Function(d) IO.File.Exists(IO.Path.Combine(d, "input", "stammdaten.yaml"))).
            Select(Function(d) IO.Path.GetFileName(d)).OrderBy(Function(s) s).ToList()
        If schulen.Count = 0 Then
            Console.WriteLine($"Keine Schulen mit '{IO.Path.Combine("input", "stammdaten.yaml")}' unter '{testsRoot}' gefunden.")
            Return False
        End If
        Dim alleOk = True
        For Each schule In schulen
            alleOk = RunOne(testsRoot, schule) AndAlso alleOk
        Next
        Return alleOk
    End Function

End Module
