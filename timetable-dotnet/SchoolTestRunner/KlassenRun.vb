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
        Dim cfg = Run.LoadConfig(IO.Path.Combine(inputDir, "config.yaml"))
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

    Private Function DiffZuErster(erste As KlassenbildungResult, v As KlassenbildungResult) As Integer
        Return erste.Zuordnung.Keys.Where(Function(id) erste.Zuordnung(id) <> v.Zuordnung(id)).Count()
    End Function

    ''' <summary>Worst-of ueber die Chips eines Kindes (Konzept 11.1);
    ''' Kinder ohne Chips sind "frei" (grau).</summary>
    Private Function AmpelZaehler(input As KlassenbildungInput, bewertung As KlassenbildungBewertung) As Dictionary(Of String, Integer)
        Dim rang As New Dictionary(Of String, Integer) From {{"gruen", 0}, {"gelb", 1}, {"rot", 2}}
        Dim jeKind = bewertung.Chips.GroupBy(Function(c) c.KindId).
            ToDictionary(Function(g) g.Key,
                         Function(g) g.OrderByDescending(Function(c) rang(c.Status)).First().Status)
        Dim zaehler As New Dictionary(Of String, Integer) From {{"rot", 0}, {"gelb", 0}, {"gruen", 0}, {"frei", 0}}
        For Each s In input.Schueler
            Dim status As String = Nothing
            zaehler(If(jeKind.TryGetValue(s.Id, status), status, "frei")) += 1
        Next
        Return zaehler
    End Function

    Private Function BaueMarkdown(schule As String, input As KlassenbildungInput, top As KlassenbildungTopResult,
                                   geloeste As List(Of KlassenbildungResult), bewertungen As List(Of KlassenbildungBewertung),
                                   zeitlimit As Double, nVarianten As Integer, epsilon As Double, minDistanz As Integer) As String
        Dim z As New List(Of String)
        z.Add($"# Klassenbildung: {schule}")
        z.Add("")
        z.Add($"{input.Schueler.Count} Kinder -> {input.Klassen.Anzahl} Klassen ({input.Klassen.MinGroesse}-{input.Klassen.MaxGroesse}) | " &
              $"Regeln: {input.Gruppen.Count} Gruppen, {input.Balance.Count} Balance, {input.Wuensche.Count} Wuensche, {input.Fixierungen.Count} Fixierungen | " &
              $"Parameter: {nVarianten} Varianten, epsilon {epsilon}, min_distanz {minDistanz}, Zeitlimit {zeitlimit}s je Lauf")
        z.Add("")
        z.Add("*Der Solver liefert VORSCHLAEGE - die Klassenzuordnung entscheidet die Schulleitung " &
              "(menschliche Letztentscheidung, Konzept Abschnitt 10). Dieser Report dokumentiert Regeln, " &
              "Parameter und Abweichungen jedes Vorschlags.*")
        z.Add("")

        z.Add($"## Konsens-Kern")
        z.Add("")
        z.Add($"{top.KonsensKern.Count} von {input.Schueler.Count} Kindern sind in allen {geloeste.Count} Varianten identisch zugeordnet" &
              If(geloeste.Count < 2, " (nur eine Variante - Konsens ohne Aussagekraft)", " - der stabile Kern fuer eine Bulk-Fixierung") & ".")
        z.Add("")

        For i = 0 To geloeste.Count - 1
            Dim v = geloeste(i)
            Dim bewertung = bewertungen(i)
            z.Add($"## Variante {i + 1} ({v.Status}, Zielwert {v.Objective})")
            z.Add("")
            If i > 0 Then
                z.Add($"Diff zu Variante 1: **{DiffZuErster(geloeste(0), v)} Kinder anders zugeordnet**.")
                z.Add("")
            End If

            Dim ampel = AmpelZaehler(input, bewertung)
            z.Add($"Ampel: {ampel("rot")} rot, {ampel("gelb")} gelb, {ampel("gruen")} gruen, {ampel("frei")} frei (von keinem Kriterium betroffen).")
            z.Add("")

            z.Add("| Klasse | Groesse | Kinder |")
            z.Add("|---|---|---|")
            For klasse = 1 To input.Klassen.Anzahl
                Dim kk = klasse
                Dim mitglieder = input.Schueler.Where(Function(s) v.Zuordnung(s.Id) = kk).Select(Function(s) s.Id).ToList()
                z.Add($"| {klasse} | {mitglieder.Count} | {String.Join(", ", mitglieder)} |")
            Next
            z.Add("")

            If input.Balance.Count > 0 Then
                z.Add("Balance-Kennzahlen (Anzahl je Klasse):")
                z.Add("")
                For Each b In input.Balance
                    Dim treffer = input.Schueler.Where(Function(s) s.Attribute.ContainsKey(b.Attribut) AndAlso s.Attribute(b.Attribut) = b.Wert).ToList()
                    Dim counts = Enumerable.Range(1, input.Klassen.Anzahl).
                        Select(Function(c) treffer.Where(Function(s) v.Zuordnung(s.Id) = c).Count())
                    z.Add($"- {b.Attribut}={b.Wert}: {String.Join(" / ", counts)} (Ziel ~{Math.Round(treffer.Count / CDbl(input.Klassen.Anzahl))} +/- {b.Toleranz})")
                Next
                z.Add("")
            End If

            z.Add("Verletzungsreport (weiche Regeln):")
            z.Add("")
            For Each verl In bewertung.Verletzungen.OrderByDescending(Function(x) x.Prio).ThenByDescending(Function(x) x.Mass)
                z.Add($"- {If(verl.Mass = 0, "[ok]", "[VERLETZT]")} {verl.RegelId} ({verl.RegelTyp}, Prio {verl.Prio}): Mass {verl.Mass}")
            Next
            z.Add("")
        Next
        Return String.Join(vbLf, z)
    End Function

    Private Function BaueJson(schule As String, input As KlassenbildungInput, top As KlassenbildungTopResult,
                               geloeste As List(Of KlassenbildungResult), bewertungen As List(Of KlassenbildungBewertung),
                               zeitlimit As Double, epsilon As Double, minDistanz As Integer) As JsonObject
        Dim variantenJson As New JsonArray()
        For i = 0 To geloeste.Count - 1
            Dim v = geloeste(i)
            Dim bewertung = bewertungen(i)
            Dim zuordnungJson As New JsonObject()
            For Each kvp In v.Zuordnung : zuordnungJson(kvp.Key) = kvp.Value : Next
            ' K5: verdichtete Balance-Kennzahlen je Variante (Zaehler je
            ' Klasse + Ziel/Toleranz) - der Viewer zeigt sie im
            ' Spaltenkopf, ohne dass Roh-Attribute exportiert werden
            ' muessten (Datenminimierung).
            Dim balanceJson As New JsonArray()
            For Each b In input.Balance
                Dim treffer = input.Schueler.Where(Function(s) s.Attribute.ContainsKey(b.Attribut) AndAlso
                                                       s.Attribute(b.Attribut) = b.Wert).Select(Function(s) s.Id).ToList()
                Dim counts As New JsonArray(Enumerable.Range(1, input.Klassen.Anzahl).
                    Select(Function(c) CType(treffer.Where(Function(id) v.Zuordnung(id) = c).Count(), JsonNode)).ToArray())
                balanceJson.Add(New JsonObject From {
                    {"regel_id", $"{b.Attribut}={b.Wert}"},
                    {"ziel", Math.Round(treffer.Count / CDbl(input.Klassen.Anzahl))},
                    {"toleranz", b.Toleranz},
                    {"counts", counts}
                })
            Next
            Dim verletzungenJson As New JsonArray(bewertung.Verletzungen.Select(Function(x) CType(New JsonObject From {
                {"regel_id", x.RegelId}, {"regel_typ", x.RegelTyp}, {"prio", x.Prio}, {"mass", x.Mass}
            }, JsonNode)).ToArray())
            Dim chipsJson As New JsonArray(bewertung.Chips.Select(Function(c) CType(New JsonObject From {
                {"kind", c.KindId}, {"regel_id", c.RegelId}, {"regel_typ", c.RegelTyp},
                {"status", c.Status}, {"text", c.Text}
            }, JsonNode)).ToArray())
            variantenJson.Add(New JsonObject From {
                {"index", i + 1},
                {"status", v.Status.ToString()},
                {"objective", v.Objective},
                {"diff_zu_v1", DiffZuErster(geloeste(0), v)},
                {"zuordnung", zuordnungJson},
                {"balance_kennzahlen", balanceJson},
                {"verletzungen", verletzungenJson},
                {"chips", chipsJson}
            })
        Next
        Return New JsonObject From {
            {"schule", schule},
            {"schueler_anzahl", input.Schueler.Count},
            {"klassen_anzahl", input.Klassen.Anzahl},
            {"min_groesse", input.Klassen.MinGroesse},
            {"max_groesse", input.Klassen.MaxGroesse},
            {"parameter", New JsonObject From {
                {"zeitlimit_s", zeitlimit}, {"epsilon", epsilon}, {"min_distanz", minDistanz}}},
            {"konsens_kern", New JsonArray(top.KonsensKern.Select(Function(id) CType(id, JsonNode)).ToArray())},
            {"varianten", variantenJson}
        }
    End Function

End Module
