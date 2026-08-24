' Bericht- und Viewer-Aufbereitung der Klassenbildung: Markdown-Report und
' das JSON, das Templates/klassenbildung.html einbettet (K4/K5, siehe
' docs/klassenbildung-plan.md).
'
' Bis Stufe A des GUI-Unterbaus lagen diese vier Funktionen Private in
' SchoolTestRunner/KlassenRun.vb - die Phase-3-GUI haette ihr erstes
' Dashboard damit gar nicht fuellen koennen. Sie sind hier Public und
' I/O-frei: rein Modell rein, Zeichenkette bzw. JsonObject raus. Das
' Gegenstueck fuer den Stundenplan liegt schon oeffentlich im Kern
' (Formatting.ToStundentafelJsonMulti).
Imports System.Text.Json.Nodes
Imports Google.OrTools.Sat
Imports TimetableCore

Public Module KlassenbildungBericht

    ''' <summary>UI-Konzept U1: Gruppen-Regelwerk-Echo inkl.
    ''' deterministischem Anzeige-Kuerzel (Klassenbildung.GruppenKuerzel).</summary>
    Public Function BaueGruppenJson(input As KlassenbildungInput) As JsonArray
        Dim kuerzel = Klassenbildung.GruppenKuerzel(input)
        Return New JsonArray(input.Gruppen.Select(Function(g) CType(New JsonObject From {
            {"id", g.Id}, {"kuerzel", kuerzel(g.Id)}, {"typ", g.Typ}, {"modus", g.Modus}, {"prio", g.Prio},
            {"max_pro_klasse", If(g.MaxProKlasse.HasValue, CType(g.MaxProKlasse.Value, JsonNode), Nothing)},
            {"mitglieder", New JsonArray(g.Mitglieder.Select(Function(m) CType(m, JsonNode)).ToArray())}
        }, JsonNode)).ToArray())
    End Function

    Public Function DiffZuErster(erste As KlassenbildungResult, v As KlassenbildungResult) As Integer
        Return erste.Zuordnung.Keys.Where(Function(id) erste.Zuordnung(id) <> v.Zuordnung(id)).Count()
    End Function

    ''' <summary>Worst-of ueber die Chips eines Kindes (Konzept 11.1);
    ''' Kinder ohne Chips sind "frei" (grau).</summary>
    Public Function AmpelZaehler(input As KlassenbildungInput, bewertung As KlassenbildungBewertung) As Dictionary(Of String, Integer)
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

    Public Function BaueMarkdown(schule As String, input As KlassenbildungInput, top As KlassenbildungTopResult,
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
            Dim labels = Klassenbildung.KlassenLabels(input)
            For klasse = 1 To input.Klassen.Anzahl
                Dim kk = klasse
                Dim mitglieder = input.Schueler.Where(Function(s) v.Zuordnung(s.Id) = kk).Select(Function(s) s.Id).ToList()
                z.Add($"| {labels(klasse - 1)} | {mitglieder.Count} | {String.Join(", ", mitglieder)} |")
            Next
            z.Add("")

            If input.Balance.Count > 0 Then
                z.Add("Balance-Kennzahlen (Anzahl je Klasse):")
                z.Add("")
                For Each b In input.Balance
                    Dim treffer = input.Schueler.Where(Function(s) s.Attribute.ContainsKey(b.Attribut) AndAlso s.Attribute(b.Attribut) = b.Wert).ToList()
                    ' Mit Label je Zahl statt "12 / 11 / 13 / 12" - sonst
                    ' muss der Leser mitzaehlen, welche Klasse gemeint ist.
                    Dim counts = Enumerable.Range(1, input.Klassen.Anzahl).
                        Select(Function(c) $"{labels(c - 1)}: {treffer.Where(Function(s) v.Zuordnung(s.Id) = c).Count()}")
                    z.Add($"- {b.Attribut}={b.Wert}: {String.Join(", ", counts)} (Ziel ~{Math.Round(treffer.Count / CDbl(input.Klassen.Anzahl))} +/- {b.Toleranz})")
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

    Public Function BaueJson(schule As String, input As KlassenbildungInput, top As KlassenbildungTopResult,
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
        ' U4: Balance-Regelwerk-Echo INKLUSIVE Treffer-IDs - die
        ' Live-Bewertung im Viewer (JS-Duplikat von Bewerte) muss die
        ' Zaehler nach jeder Verschiebung neu rechnen koennen, ohne
        ' Roh-Attribute zu exportieren (nur Mitgliedschaft je Regel).
        ' `ziel` wird hier in VB gerundet (Math.Round = Banker's
        ' Rounding) und im JS NICHT neu berechnet - die Treffermenge
        ' ist statisch, das Ziel damit auch.
        Dim balanceRegelnJson As New JsonArray()
        For Each b In input.Balance
            Dim treffer = input.Schueler.Where(Function(s) s.Attribute.ContainsKey(b.Attribut) AndAlso
                                                   s.Attribute(b.Attribut) = b.Wert).Select(Function(s) s.Id).ToList()
            balanceRegelnJson.Add(New JsonObject From {
                {"regel_id", $"{b.Attribut}={b.Wert}"},
                {"ziel", Math.Round(treffer.Count / CDbl(input.Klassen.Anzahl))},
                {"toleranz", b.Toleranz},
                {"modus", b.Modus},
                {"prio", b.Prio},
                {"treffer", New JsonArray(treffer.Select(Function(id) CType(id, JsonNode)).ToArray())}
            })
        Next
        Return New JsonObject From {
            {"schule", schule},
            {"schueler_anzahl", input.Schueler.Count},
            {"klassen_anzahl", input.Klassen.Anzahl},
            {"min_groesse", input.Klassen.MinGroesse},
            {"max_groesse", input.Klassen.MaxGroesse},
            {"klassen_labels", New JsonArray(Klassenbildung.KlassenLabels(input).
                Select(Function(l) CType(l, JsonNode)).ToArray())},
            {"gruppen", BaueGruppenJson(input)},
            {"balance", balanceRegelnJson},
            {"wuensche", New JsonArray(input.Wuensche.Select(Function(w, wi) CType(New JsonObject From {
                {"index", wi}, {"typ", w.Typ}, {"modus", w.Modus}, {"prio", w.Prio},
                {"regel_id", $"wunsch[{wi}]:{w.Kinder(0)}+{w.Kinder(1)}"},
                {"kinder", New JsonArray(w.Kinder.Select(Function(m) CType(m, JsonNode)).ToArray())}
            }, JsonNode)).ToArray())},
            {"fixierungen", New JsonArray(input.Fixierungen.Select(Function(fx) CType(New JsonObject From {
                {"kind", fx.Kind},
                {"klasse", If(fx.Klasse.HasValue, CType(fx.Klasse.Value, JsonNode), Nothing)},
                {"nicht_klasse", If(fx.NichtKlasse.HasValue, CType(fx.NichtKlasse.Value, JsonNode), Nothing)}
            }, JsonNode)).ToArray())},
            {"parameter", New JsonObject From {
                {"zeitlimit_s", zeitlimit}, {"epsilon", epsilon}, {"min_distanz", minDistanz}}},
            {"konsens_kern", New JsonArray(top.KonsensKern.Select(Function(id) CType(id, JsonNode)).ToArray())},
            {"varianten", variantenJson}
        }
    End Function

End Module
