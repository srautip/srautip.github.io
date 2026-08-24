' Klassenbildung K3 (docs/klassenbildung-plan.md): reiner Bewertungslauf
' OHNE Optimierung - Constraints werden gegen eine gegebene Zuordnung
' nur AUSGEZAEHLT (Konzept 9.2 Phase 3 / 11.2, Millisekunden).
' Verifier-Philosophie: teilt keine Zeile mit dem CP-SAT-Modell in
' Klassenbildung.vb - "solver proposes, an independent re-derivation is
' the ground truth". Liefert je weicher Regel das Verletzungsmass
' (Scorecard/Report) UND je (Kind, betroffene Regel) einen
' Ampel-Chip-Status nach Konzept 11.1.
Imports System.Text

''' <summary>Ein Ampel-Chip: Status eines Kriteriums fuer EIN davon
''' betroffenes Kind (Konzept 11.1). `Status`: gruen (erfuellt,
''' unkritisch), gelb (erfuellt, aber knapp - Kappe exakt voll bzw.
''' Balance am Toleranzrand), rot (weiche Regel verletzt). Kinder ohne
''' einen einzigen Chip sind "freie Kinder" (grau - Sache des
''' Konsumenten). `Text` ist der Klartext fuers Tippen auf den Chip.</summary>
Public NotInheritable Class KlassenbildungChip
    Public Property KindId As String
    Public Property RegelId As String
    Public Property RegelTyp As String
    Public Property Status As String
    Public Property Text As String
End Class

''' <summary>Ergebnis des Bewertungslaufs: Verletzungsmass je weicher
''' Regel (unabhaengig nachgezaehlt) + alle Ampel-Chips.</summary>
Public NotInheritable Class KlassenbildungBewertung
    Public Property Verletzungen As List(Of KlassenbildungVerletzung)
    Public Property Chips As List(Of KlassenbildungChip)
End Class

Public Module KlassenbildungQuality

    ''' <summary>Zaehlt alle Regeln gegen `zuordnung` (Kind-Id -> Klasse,
    ''' 1-basiert) aus. Weiche Regeln liefern eine Verletzungs-Zeile
    ''' (Mass 0 = erfuellt); Chips entstehen fuer JEDE Regel (auch
    ''' harte - deren Chips sind in einer gueltigen Loesung immer
    ''' gruen, das Kind ist aber sichtbar "betroffen").</summary>
    Public Function Bewerte(input As KlassenbildungInput, zuordnung As Dictionary(Of String, Integer)) As KlassenbildungBewertung
        Dim verletzungen As New List(Of KlassenbildungVerletzung)
        Dim chips As New List(Of KlassenbildungChip)
        Dim anzahl = input.Klassen.Anzahl
        ' Klassen werden dem Nutzer IMMER als Label gezeigt (1a, 1b, ...),
        ' nie als Laufnummer - die Nummer ist ein internes Detail des
        ' Solvers. Ohne `stufe` faellt KlassenLabels auf "Klasse n"
        ' zurueck, dann steht dort weiterhin etwas Lesbares.
        Dim labels = Klassenbildung.KlassenLabels(input)

        ' --- Gruppen ---
        For Each g In input.Gruppen
            Dim proKlasse = g.Mitglieder.GroupBy(Function(m) zuordnung(m)).
                ToDictionary(Function(grp) grp.Key, Function(grp) grp.Count())
            If g.Typ = "buendelung" Then
                Dim spread = proKlasse.Count
                Dim mass = CLng(spread - 1)
                If g.Modus = "soft" Then
                    verletzungen.Add(New KlassenbildungVerletzung With {
                        .RegelId = g.Id, .RegelTyp = "buendelung", .Prio = g.Prio, .Mass = mass})
                End If
                For Each m In g.Mitglieder
                    chips.Add(New KlassenbildungChip With {
                        .KindId = m, .RegelId = g.Id, .RegelTyp = "buendelung",
                        .Status = If(mass > 0, "rot", "gruen"),
                        .Text = If(mass > 0,
                                   $"Buendelung {g.Id}: auf {spread} Klassen verteilt",
                                   $"Buendelung {g.Id}: alle in {labels(zuordnung(g.Mitglieder(0)) - 1)}")})
                Next
            Else ' verteilung
                Dim kappe = g.MaxProKlasse.Value
                Dim mass = CLng(proKlasse.Values.Sum(Function(cnt) Math.Max(cnt - kappe, 0)))
                If g.Modus = "soft" Then
                    verletzungen.Add(New KlassenbildungVerletzung With {
                        .RegelId = g.Id, .RegelTyp = "verteilung", .Prio = g.Prio, .Mass = mass})
                End If
                For Each m In g.Mitglieder
                    Dim cnt = proKlasse(zuordnung(m))
                    Dim status = If(cnt > kappe, "rot", If(cnt = kappe, "gelb", "gruen"))
                    chips.Add(New KlassenbildungChip With {
                        .KindId = m, .RegelId = g.Id, .RegelTyp = "verteilung",
                        .Status = status,
                        .Text = $"Verteilung {g.Id}: {cnt}/{kappe} in dieser Klasse" &
                                If(cnt = kappe AndAlso cnt <= kappe, " - Kappe voll", If(cnt > kappe, " - Kappe ueberschritten", ""))})
                Next
            End If
        Next

        ' --- Balance ---
        For Each b In input.Balance
            Dim treffer = input.Schueler.Where(Function(s) s.Attribute IsNot Nothing AndAlso
                                                   s.Attribute.ContainsKey(b.Attribut) AndAlso
                                                   s.Attribute(b.Attribut) = b.Wert).
                                          Select(Function(s) s.Id).ToList()
            Dim target = CLng(Math.Round(treffer.Count / CDbl(anzahl)))
            Dim cntJeKlasse = Enumerable.Range(1, anzahl).ToDictionary(
                Function(c) c, Function(c) treffer.Where(Function(id) zuordnung(id) = c).Count())
            Dim regelId = $"{b.Attribut}={b.Wert}"
            Dim mass = CLng(cntJeKlasse.Values.Sum(Function(cnt) Math.Max(Math.Abs(cnt - target) - b.Toleranz, 0L)))
            If b.Modus = "soft" Then
                verletzungen.Add(New KlassenbildungVerletzung With {
                    .RegelId = regelId, .RegelTyp = "balance", .Prio = b.Prio, .Mass = mass})
            End If
            For Each id In treffer
                Dim dev = Math.Abs(cntJeKlasse(zuordnung(id)) - target)
                Dim status As String
                If dev > b.Toleranz Then
                    status = "rot"
                ElseIf dev = b.Toleranz AndAlso b.Toleranz > 0 Then
                    status = "gelb"
                Else
                    status = "gruen"
                End If
                chips.Add(New KlassenbildungChip With {
                    .KindId = id, .RegelId = regelId, .RegelTyp = "balance",
                    .Status = status,
                    .Text = $"Balance {regelId}: {cntJeKlasse(zuordnung(id))} in dieser Klasse (Ziel {target} +/- {b.Toleranz})"})
            Next
        Next

        ' --- Wuensche ---
        For i = 0 To input.Wuensche.Count - 1
            Dim w = input.Wuensche(i)
            Dim s1 = w.Kinder(0)
            Dim s2 = w.Kinder(1)
            Dim zusammen = zuordnung(s1) = zuordnung(s2)
            Dim erfuellt = If(w.Typ = "zusammen", zusammen, Not zusammen)
            Dim regelId = $"wunsch[{i}]:{s1}+{s2}"
            Dim regelTyp = If(w.Typ = "zusammen", "wunsch_zusammen", "wunsch_getrennt")
            If w.Modus = "soft" Then
                verletzungen.Add(New KlassenbildungVerletzung With {
                    .RegelId = regelId, .RegelTyp = regelTyp, .Prio = w.Prio, .Mass = If(erfuellt, 0L, 1L)})
            End If
            For Each kind In w.Kinder
                chips.Add(New KlassenbildungChip With {
                    .KindId = kind, .RegelId = regelId, .RegelTyp = regelTyp,
                    .Status = If(erfuellt, "gruen", "rot"),
                    .Text = $"Wunsch {If(w.Typ = "zusammen", "zusammen mit", "getrennt von")} " &
                            $"{If(kind = s1, s2, s1)}: {If(erfuellt, "erfuellt", "nicht erfuellt")}"})
            Next
        Next

        Return New KlassenbildungBewertung With {.Verletzungen = verletzungen, .Chips = chips}
    End Function

End Module
