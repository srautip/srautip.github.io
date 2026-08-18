# Phase 2: Erweiterte Qwen-Robustheitstests — Ergebnisbericht

Dieser Bericht dokumentiert die Ergebnisse von Phase 2 (siehe Plan, Abschnitt
"Phase 2 (feingeplant)"): vier Testszenarien × drei Wiederholungsläufe ohne
festen LLM-Seed, mit dem Ziel, reale Streuung im Qwen-Verhalten zu messen und
systematische Fehlermodi zu finden und zu beheben. Alle Fixes wurden
ausschließlich in `TimetableCore/LlmExtraction.vb` vorgenommen (VB.NET); die
Python-Referenz (`timetable/`) bleibt wie vereinbart eingefroren.

Rohdaten aller Läufe (inkl. der Vorher/Nachher-Verifikationsläufe nach jedem
Fix): [`phase2-results-raw.log`](./phase2-results-raw.log).

## Die vier Szenarien

| Szenario | Größe | Prompt-Stil | Fokus |
|---|---|---|---|
| **Gymnasium-Klasse-5** (Baseline, aus Phase 1) | 4 Klassen, 9 Fächer, 15 Lehrer | ausführlich, strukturiert | Regressions-Baseline; bereits in Phase 1 auf 100% Vollständigkeit verifiziert |
| **A: Grundschule** | 2 Klassen (1a, 1b), 4 Fächer, 4 Lehrer, 1 Fachraum | knapper Telegrammstil, wenig Kontext | Testet knappe/elliptische Formulierungen |
| **B: Berufsschule Oberstufe** | 4 Fachklassen, mehrere geteilte Fachräume, Lehrer-Kreuzmatrix (2 Lehrer je 2 Fächer über dieselben Klassen) | ausführlich, viele Querverweise | Skalierung, Raumkonkurrenz, `period_exception` mit MEHREREN erlaubten Tagen |
| **C: Edge-Case** | klein (2 Klassen, 3 Fächer), aber 4 gezielt riskante Formulierungsmuster in einem Prompt | dicht gepackte Sonderfälle | gezielte Fehlermodus-Suche (siehe Plan, Abschnitt 2a) |

## Ergebnistabelle: ursprüngliche Wiederholungs-Studie (12 Läufe)

Dies ist die erste vollständige Studie, **vor** allen in diesem Bericht
dokumentierten Fixes (die Fixes entstanden aus der Analyse dieser Ergebnisse).

| Szenario | Lauf | Overall-Score | Solve-Status | Verifier-Verstöße | Extraktionsdauer |
|---|---|---|---|---|---|
| Gymnasium | 1/3 | 100 % | Optimal | 0 | 1248,4 s |
| Gymnasium | 2/3 | 100 % | Optimal | 0 | 978,3 s |
| Gymnasium | 3/3 | 100 % | Optimal | 0 | 930,5 s |
| Grundschule | 1/3 | 100 % | Optimal | 0 | 371,2 s |
| Grundschule | 2/3 | **100 %** | **Infeasible** | n/a | 364,9 s |
| Grundschule | 3/3 | 100 % | Optimal | 0 | 323,2 s |
| Oberstufe | 1/3 | **93 %** | Optimal | 0 | 1084,3 s |
| Oberstufe | 2/3 | **93 %** | Optimal | 0 | 847,5 s |
| Oberstufe | 3/3 | **93 %** | Optimal | 0 | 819,1 s |
| EdgeCase | 1/3 | **87 %** | Optimal | 0 | 478,0 s |
| EdgeCase | 2/3 | **87 %** | Optimal | 0 | 342,9 s |
| EdgeCase | 3/3 | **87 %** | Optimal | 0 | 338,2 s |

**Min/Mittel/Max Overall-Score pro Szenario (vor Fixes):**

| Szenario | Min | Mittel | Max |
|---|---|---|---|
| Gymnasium | 100 % | 100 % | 100 % |
| Grundschule | 100 % | 100 % | 100 % *(aber 1/3 Infeasible trotz 100% Recall — siehe unten)* |
| Oberstufe | 93 % | 93 % | 93 % *(konstant, alle 3 Läufe identisch)* |
| EdgeCase | 87 % | 87 % | 87 % *(konstant, alle 3 Läufe identisch)* |

Auffällig: bei Oberstufe und EdgeCase war der Score über alle 3 Wiederholungen
**exakt identisch** — die zugrundeliegenden Fehlermodi waren also nicht
zufällige Ausreißer, sondern reproduzierbar in jedem einzelnen Lauf (siehe
Fehlermodi unten). Bei Grundschule zeigte sich dagegen echte Lauf-zu-Lauf-
Streuung: 2 von 3 Läufen lieferten ein lösbares Ergebnis, einer nicht — trotz
identisch hoher (100%) Recall-Bewertung in allen dreien.

## Gefundene Fehlermodi & Fixes

Insgesamt wurden in Phase 2 vier neue, reale Fehlermodi gefunden (zusätzlich
zum bereits in Phase 1 behobenen `teacher_availability`-Problem, das während
der gesamten Studie stabil bei 100% blieb). Für jeden gilt: erst wurde
versucht, die Instruktion in `LlmExtraction.vb` zu schärfen; nur wo das nicht
ausreichte, kam eine deterministische Nachbearbeitung dazu (nach dem
`ExpandPeriodException`-Muster aus Phase 1).

### 1. `consecutive_required`-Halluzination → Infeasible trotz 100% Recall (Grundschule)

**Symptom:** Lauf 2/3 des Grundschule-Szenarios erreichte 100% Recall in
*jeder* Kategorie, aber `Solver.Solve()` lieferte `Infeasible`. Das deckte
eine echte methodische Lücke auf: **reine Recall-Bewertung prüft nur, ob
erwartete Fakten abgedeckt sind — nicht, ob zusätzliche, falsche oder
widersprüchliche Constraints erzeugt wurden.** Ein Ergebnis kann also 100%
Recall haben und trotzdem unlösbar sein.

**Root Cause (via lokaler Bisektion ohne LLM-Aufruf gefunden):** Der
`consecutive_required`-Extraktions-Call halluzinierte `block_length: 2` für
Sport/Kunst rein aus der Zahlenkombination "2h/Woche, max 1/Tag" — obwohl im
Prompt nirgends "Doppelstunde" o.ä. steht. `Solver.vb`s `AddBlockConstraint`
erzwingt aber, dass *jedes* Vorkommen dieses Fachs an *jedem* Tag Teil eines
vollständigen Blocks von genau `block_length` Perioden sein muss — das ist
mathematisch unmöglich, wenn entweder (a) `block_length` größer ist als
`max_per_day`, oder (b) `hours_per_week` kein Vielfaches von `block_length`
ist. Ein bei einer Reproduktion gefundener zweiter Fall bestätigte (b) sehr
deutlich: Mathe mit 5h/Woche bekam `block_length: 2` zugewiesen (5 ist keine
Vielfache von 2) — das Modell begründete in seinem eigenen `reason`-Feld
sogar ausdrücklich, *warum* das falsch ist ("Mathe 5h/Woche impliziert keine
feste Doppelstunde... nicht explizit gesagt"), erzeugte das Objekt aber
trotzdem.

**Fix:**
1. Instruktion geschärft: `consecutive_required` nur bei EXPLIZITEM Wort
   ("Doppelstunde", "Block", "zusammenhängend" o.ä.) extrahieren, niemals aus
   Zahlen ableiten.
2. Da die Instruktion allein nicht zuverlässig griff (das Modell handelte
   teils gegen seine eigene, korrekte Begründung), zusätzlich eine
   deterministische Nachbearbeitung `DropContradictoryConsecutiveRequired`
   in `ExtractAllConstraints` ergänzt: verwirft jedes `consecutive_required`-
   Objekt, dessen `block_length` entweder größer als das zugehörige
   `max_per_day` ist ODER nicht `hours_per_week` teilt. Das kann nur
   unmögliche Kombinationen entfernen, nie eine echte (ein echter Block hat
   immer `max_per_day >= block_length` UND `hours_per_week` als Vielfaches
   von `block_length`).

**Verifikation (vorher/nachher):**
- Vorher: 1 von 3 Studienläufen Infeasible; isolierte Reproduktion bestätigte
  es erneut beim allerersten Folgeversuch (Iteration 2/2).
- Nachher: 4 von 4 isolierten Reproduktionsversuchen Optimal, ein sauberer
  Lauf des gated `LlmExtractionE2EGrundschule`-Tests, und 3 von 3 frischen
  `RobustnessRunner`-Läufen Optimal/0 Verstöße — insgesamt 7 konsekutive
  erfolgreiche Live-Läufe nach dem Fix, gegenüber der ursprünglichen ~33%
  Fehlerquote.

### 2. `consecutive_required`: unvollständige Klassen-Abdeckung (Oberstufe)

**Symptom:** `consecutive_required` erreichte in allen 3 Studienläufen exakt
50% Recall (unabhängig von der genauen Item-Anzahl).

**Root Cause:** Für Fächer mit Block-Pflicht, die von ZWEI Klassen belegt
werden (Elektropraktikum: E11+E12; Metallpraktikum: M11+M12), extrahierte das
Modell konsistent nur EINE der beiden Klassen pro Fach (z.B. nur
E12/Elektropraktikum, nicht E11/Elektropraktikum) — eine reine
Vollständigkeits-/Abdeckungslücke, kein Widerspruch.

**Fix:** Instruktion geschärft: explizite Aufforderung, bei einem
Block-Fach mit mehreren genannten Klassen ALLE zu berücksichtigen, mit
einer expliziten Selbstprüfungs-Aufforderung am Ende der Instruktion.

**Verifikation:** 2 von 2 frischen isolierten Diagnose-Aufrufen lieferten
danach alle 4 erwarteten Objekte korrekt; ein formaler
`RobustnessRunner`-Nachlauf bestätigte 100% (vorher 50%) bei gleichzeitig
weiterhin Optimal/0 Verstöße.

### 3. `teacher_subject_assignment`: Verwechslung der Kreuzmatrix-Zuordnung (Oberstufe)

**Symptom:** `teacher_subject_assignment` erreichte in allen 3 Studienläufen
exakt 92% Recall (22 von 24 korrekten Tupeln).

**Root Cause:** Der Prompt beschreibt für "Mathematik" eine "diagonale"
Lehrer-Klassen-Zuordnung (Krause→E11+M11, Nguyen→E12+M12) und referenziert sie
für "Wirtschaftskunde" mit "Herr Krause unterrichtet ZUSAETZLICH E11 und M11,
Frau Nguyen unterrichtet zusaetzlich E12 und M12" — also bewusst dieselbe
Zuordnung. Das Modell extrahierte Mathematik korrekt, verwechselte aber bei
Wirtschaftskunde die Zuordnung mit dem in anderen Fächern (Fachtheorie
Elektro/Metall) üblichen "Track"-Muster (Krause→E11+E12, Nguyen→M11+M12) —
eine inhaltliche Querverweis-Verwechslung, kein Struktur- oder Zahlenfehler.

**Fix:** Instruktion geschärft: expliziter Hinweis auf das
"zusätzlich/ebenfalls"-Muster mit der Anweisung, die exakte Klassen-Zuordnung
des referenzierten Fachs zu übernehmen statt neu zu raten oder von einem
anderen, ähnlich klingenden Fach zu übertragen.

**Verifikation (ehrliches Teilresultat):** 2 von 2 frischen isolierten
Diagnose-Aufrufen lieferten danach alle 24 Tupel korrekt (100%). Ein
zusätzlicher formaler `RobustnessRunner`-Nachlauf zeigte jedoch weiterhin
92% — derselbe Fehler trat also noch einmal auf. **Diese Instruktions-
Schärfung reduziert die Fehlerquote (von 3/3 auf 1/3 in den bisherigen
Stichproben nach dem Fix), behebt sie aber nicht vollständig.** Anders als
bei Fehlermodus 1 lässt sich dieser Fehler nicht deterministisch abfangen,
da er kein struktureller/mathematischer Widerspruch ist, sondern ein
inhaltlicher Wert-Fehler (welche Klasse zu welchem Lehrer gehört) — eine
Nachbearbeitung müsste den Prompt-Text selbst re-parsen, was außerhalb des
bisherigen deterministischen Nachbearbeitungs-Musters liegt. Als bekannte
Einschränkung dokumentiert; ein möglicher nächster Schritt wäre ein
Konsistenz-Check gegen eine bereits korrekt extrahierte, thematisch verwandte
Kategorie (hier: `weekly_hours`, das dieselbe Kreuzmatrix ebenfalls enthält
und in allen Läufen 100% korrekt war) - aber das ginge über den in Phase 2
vorgesehenen Rahmen (Prompt-Schärfung + rein-mathematische Nachbearbeitung)
hinaus.

### 4. `period_exception` liefert leeres Ergebnis bei MEHREREN unabhängigen Regeln (EdgeCase)

**Symptom:** `period_exception` lieferte in allen 3 EdgeCase-Studienläufen
`n_items=0` (valides, aber leeres JSON). Dadurch blieb `forbidden_slot` in
allen 3 Läufen konstant bei 20% Recall hängen (nur das unabhängige
"Freitags 3./4. Stunde frei"-Muster wurde erfasst, die beiden Regeln "6.
Stunde nur montags" / "8. Stunde nur donnerstags" komplett verloren).

**Root Cause:** Die Instruktion sagte bisher "Erzeuge EIN Objekt" (Singular)
— geschrieben, als in jedem bisherigen Szenario (Gymnasium, Oberstufe) genau
eine `period_exception`-Regel vorkam. EdgeCase enthält bewusst ZWEI
unabhängige Regeln für unterschiedliche Stundennummern im selben Prompt
(Testmuster 1 aus der Feinplanung) — und statt zwei Objekte zu erzeugen,
lieferte das Modell lieber gar nichts. Bei Gymnasium/Oberstufe blieb dieses
Problem bisher unbemerkt, weil deren Prompts jeweils zusätzlich einen
redundanten Satz enthalten, der die gesperrten Tage direkt nennt (Fallback
über `forbidden_slot`) — EdgeCase hat diesen Fallback bewusst NICHT, um genau
diesen Fall zu erzwingen.

**Fix:** Instruktion geschärft: explizite Klarstellung, dass bei mehreren
unabhängigen Regeln für unterschiedliche Stundennummern für JEDE ein eigenes
Objekt zu erzeugen ist ("das Ergebnis ist dann eine Liste mit MEHREREN
Objekten, nicht nur einem").

**Verifikation:** 2 von 2 frischen isolierten Diagnose-Aufrufen lieferten
danach exakt die 2 erwarteten Objekte (`period:6→[Mo]`, `period:8→[Do]`); ein
sauberer Lauf des gated `LlmExtractionE2EEdgeCase`-Tests; ein formaler
`RobustnessRunner`-Nachlauf zeigte `forbidden_slot` bei 100% (vorher 20%) und
Overall bei 100% (vorher 87%), weiterhin Optimal/0 Verstöße.

## Bereits in Phase 1 behobener Fehlermodus (zur Einordnung)

Vor Beginn der Wiederholungs-Studie wurde noch ein Rest-Fehlermodus aus der
Phase-1/2-Übergangsphase behoben: `teacher_availability` lieferte teils
`{"type":"teacher_availability","teacher":"..."}` ganz ohne Inhalt
(`available_days`/`unavailable_periods` fehlten komplett). Fix: Instruktion
um explizite Beispielformulierungen erweitert (inkl. Verneinungen wie "kann
an keinem Tag außer X, Y unterrichten") und `available_days` im JSON-Schema
als PFLICHTFELD markiert, um leere Objekte strukturell auszuschließen. Dieser
Fix war bereits vor der Wiederholungs-Studie aktiv und ist der Grund, warum
`teacher_availability` in **allen 12 Studienläufen** konstant 100% erreichte
— keine Regression in Phase 2.

## Bekannte, nicht (vollständig) behobene Restfunde

- **`period_exception`-Überinterpretation (Grundschule, nicht blockierend):**
  Der `period_exception`-Call interpretiert gelegentlich einen reinen
  `forbidden_slot`-Satz ("Freitags Schluss nach der 3. Stunde") fälschlich
  als Tage-Komplement-Regel und erzeugt zusätzliche, so nicht angeforderte
  `forbidden_slot`-Einträge (z.B. zusätzlich für Donnerstag). In den bisher
  beobachteten Fällen blieb das Ergebnis dennoch lösbar und die Recall-Werte
  blieben bei 100% (extra, nicht angeforderte Constraints werden vom
  Recall-Scoring nicht bestraft) — es trat in dieser Studie nicht als
  eigenständiger, wiederkehrender Blocker auf (Kriterium "≥2 von 3
  Wiederholungen" wurde nicht erreicht) und wurde daher nicht behoben, aber
  hier dokumentiert, falls es in künftigen Läufen erneut sichtbar wird.
- **`teacher_subject_assignment`-Kreuzmatrix-Verwechslung (Oberstufe):** siehe
  Fehlermodus 3 oben — Instruktions-Schärfung reduziert, aber eliminiert die
  Fehlerquote nicht vollständig.

## Methodische Erkenntnis: Recall-Scoring erkennt keine Überextraktion

Die zentrale, über den einzelnen Fehlermodus hinausgehende Erkenntnis aus
Phase 2: das bestehende Vollständigkeits-Scoring (`CompletenessScoring.vb`)
misst ausschließlich **Recall** — ob erwartete Fakten abgedeckt sind. Es
bestraft weder zusätzliche noch widersprüchliche Constraints. Fehlermodus 1
oben zeigt konkret, dass das zu einem 100%-Recall-Ergebnis führen kann, das
trotzdem unlösbar ist. Für den produktiven Einsatz bedeutet das: **das
`Solver.Solve()`/`Verifier.VerifySchedule()`-Duo bleibt die tatsächlich
verlässliche Korrektheitsprüfung** - Recall-Scores sind nützlich für
gezielte Diagnose einzelner Kategorien, aber kein Ersatz für den echten
Solve-Versuch.

## Definition of Done — Status

- [x] 4 Szenarien (Baseline + 3 neu) je 3-mal durch `RobustnessRunner`
      gelaufen, Ergebnisse vollständig dokumentiert.
- [x] Für jedes neue Szenario existiert ein gated Einzellauf-Test in
      `TimetableCore.Tests` (`dotnet test` bleibt grün ohne
      `RUN_LLM_TESTS`: 29 bestanden, 4 übersprungen).
- [x] Jeder gefundene, wiederkehrende Fehlermodus wurde entweder vollständig
      behoben und durch einen erneuten Lauf verifiziert (Fehlermodi 1, 2, 4),
      oder - wo eine vollständige Behebung über den vorgesehenen Rahmen
      hinausging - mit ehrlicher Vorher/Nachher-Evidenz und als bekannte
      Einschränkung dokumentiert (Fehlermodus 3).
- [x] `CompletenessScoring.vb` wird von allen 4 Szenarien genutzt; der
      Gymnasium-Klasse-5-Test zeigte nach dem Refactoring identisches
      Ergebnis wie vorher (100%, Optimal, 0 Verstöße - keine Regression über
      die gesamte Studie hinweg).
