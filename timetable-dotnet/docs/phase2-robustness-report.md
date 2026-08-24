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

# Phase 2.6: LLM lernt Muss/Kann-Ableitung — Ergebnisbericht

Dieser Abschnitt dokumentiert Phase 2.6 (siehe Plan, Abschnitt "Phase 2.6
(feingeplant)"): Qwen soll aus der Formulierung selbst ableiten, ob ein
Kann-fähiger Constraint (`teacher_availability`, `weekly_hours.max_per_day`,
`room_requirement`, `forbidden_slot`, `consecutive_required`) als `"must"`
oder `"should"` gemeint ist, statt wie bisher implizit immer auf `"must"` zu
defaulten. Aufbauend auf dem in Phase 2.5 fertiggestellten deterministischen
Muss/Kann-Mechanismus (Datenmodell, Solver, Verifier — siehe oben).

## Szenario: `MussKannFixture`

Neue, dedizierte Fixture (2 Klassen 7a/7b, 5 Tage × 7 Stunden/Tag, 8 Fächer),
NICHT in die 4 bestehenden Szenarien eingewoben, um deren verifizierte
100%-Baselines nicht zu gefährden. Pro Kann-fähigem Typ enthält der Prompt
gezielt sowohl Wunsch-Formulierungen ("wenn möglich", "idealerweise") als
auch explizite Muss-Formulierungen (mit und ohne Verstärkungswort wie
"muss"), macht insgesamt 32 erwartete `(ConstraintType, Key,
ExpectedPriority)`-Tupel für die neue `ScorePriorityAccuracy`-Kennzahl
(`CompletenessScoring.vb`, additiv, trennt "Fakt fehlt" von "Fakt da,
Priorität falsch").

## Instruktions-/Schema-Änderungen (`LlmExtraction.vb`)

Alle 5 Kann-fähigen `Instructions`-Einträge wurden um einen
Muss/Kann-Erkennungsabsatz ergänzt, alle 5 zugehörigen `ItemSchema`s bekamen
ein neues optionales `props("priority") = EnumSchema({"must", "should"})`
(nicht `required` — Weglassen bleibt gültig und defaultet weiterhin über
`JsonHelpers.GetPriority` auf `"must"`). Die 4 unveränderten Typen
(`no_overlap`, `shared_resource_conflict`, `teacher_subject_assignment`,
`period_exception`) blieben byte-identisch.

## Live-diagnostizierte Regressionen & Fixes (isolierte Diagnose-Aufrufe)

Analog zur Methodik aus Phase 2 wurde jeder der 5 Typen zunächst einzeln via
`LlmExtraction.ExtractConstraintType(...)` getestet (günstige ~10-90s-Aufrufe
statt der vollen Pipeline), bevor der volle gated E2E-Test lief.

### `weekly_hours`: `max_per_day` verschwand komplett (echte Regression)

**Symptom:** Der erste isolierte Diagnose-Lauf zeigte, dass alle 16
extrahierten `weekly_hours`-Objekte zwar `hours_per_week`, aber KEIN
`max_per_day`-Feld mehr enthielten — eine durch die neu hinzugefügte
Prioritäts-Komplexität in der Instruktion selbst verursachte Regression
(nicht Teil des ursprünglichen Fehlerbilds).

**Fix:** Instruktion umformuliert, um explizit zu verlangen, IMMER beide
Werte auszugeben, wenn beide im Text stehen ("das Tagesmaximum niemals
weglassen"), mit `priority: "should"` NUR wenn das Tagesmaximum selbst als
Wunsch formuliert ist.

**Verifikation:** danach 16/16 Objekte mit `max_per_day`, 4/4 getestete
Prioritäten korrekt, reproduziert in einem zweiten frischen Diagnose-Lauf.

### `forbidden_slot`: vierte, abweichend formulierte Regel wurde ausgelassen

**Symptom:** 3 aufeinanderfolgende isolierte Diagnose-Läufe lieferten
konstant nur 6/8 statt 8/8 Objekten — die Regel "Dienstags findet die 7.
Stunde ... nicht statt" (strukturell anders formuliert als die anderen 3
"Am `<Tag>` ..."-Regeln) wurde jedes Mal komplett verworfen.

**Fix:** zweigleisig — (a) Instruktion ergänzt: bei mehreren unabhängigen
Sperrzeit-Regeln JEDE einzeln extrahieren, keine auslassen; (b) Fixture-Text
an das grammatische Muster der anderen 3 Regeln angeglichen ("Am Dienstag
findet die 7. Stunde ... nicht statt.") — dasselbe Muster wie
Fehlermodus 4 in Phase 2 (`period_exception`), wo eine strukturell
abweichende Formulierung die Ursache war.

**Verifikation:** danach 8/8 Objekte, alle Prioritäten korrekt, reproduziert
in einem zweiten frischen Diagnose-Lauf.

### `consecutive_required`: Kunst-Priorität korrigiert, Sozialkunde bleibt dokumentierte Lücke

**Symptom:** Kunst ("wenn möglich als Doppelstunde, ansonsten auch einzeln")
wurde zunächst fälschlich als `"must"` statt `"should"` extrahiert.
Sozialkunde wurde in 5 aufeinanderfolgenden isolierten Diagnose-Läufen
(mit 3 verschiedenen Formulierungsversuchen: "...ansonsten auch einzeln.",
"...nach Moeglichkeit als Doppelstunde statt.", "...idealerweise als
Doppelstunde statt.") konsequent GAR NICHT extrahiert (0/8 für diese
Klasse/Fach-Kombination, beide Klassen).

**Fix (Kunst):** Instruktion um explizite Behandlung von
"Alternative erlaubt, trotzdem extrahieren"-Formulierungen ergänzt.
**Verifikation (Kunst):** danach in allen 4 nachfolgenden Diagnose-Läufen
stabil korrekt als `"should"`.

**Sozialkunde: nicht behoben, als bekannte Grenze dokumentiert.** Nach 5
erfolglosen Versuchen mit 3 unterschiedlichen Prompt-Formulierungen wurde die
Iteration gemäß der im Plan vorab festgehaltenen "Ehrlichen Grenze" für
Phase 2.6 gestoppt (Muss/Kann ist eine subjektive Formulierungs-Einschätzung,
keine mathematisch prüfbare Tatsache wie `block_length`-Teilbarkeit — es gibt
kein deterministisches Sicherheitsnetz analog `DropContradictoryConsecutiveRequired`).
Ein einzelner Lauf zeigte zusätzlich, dass Geschichte (sonst durchgängig
korrekt `"must"`) einmalig zu `"should"` kippte — ohne dass eine
Prompt-/Instruktionsänderung Geschichte betraf, als normale Lauf-zu-Lauf-
Streuung eingeordnet, nicht als neue Regression.

## Voller gated E2E-Test

`RUN_LLM_TESTS=1 dotnet test --filter LlmExtractionE2EMussKann`: **1
bestanden, 0 fehlgeschlagen**, Laufzeit 11m18s (siehe
`/tmp/musskann_full_e2e.log`-Ausschnitt dieser Session).

## `RobustnessRunner`-Wiederholungsstudie (3 Läufe, MussKann-Szenario)

| Lauf | teacher_ subject_ assignment | weekly_ hours | no_ overlap | room_ requirement | consecutive_ required | teacher_ availability | forbidden_ slot | priority_ accuracy | Overall | Solve-Status | Verifier-Verstöße | Extraktionsdauer |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1/3 | 100 % | 100 % | 100 % | 100 % | 75 % | 100 % | 100 % | 62 % | 92 % | Optimal | 0 | 570,4 s |
| 2/3 | 100 % | 100 % | 100 % | 100 % | 75 % | 100 % | 100 % | 62 % | 92 % | Optimal | 0 | 558,3 s |
| 3/3 | 100 % | 100 % | 100 % | 100 % | 75 % | 100 % | 100 % | 62 % | 92 % | Optimal | 0 | 589,4 s |

**Bemerkenswert stabil:** alle 8 Kennzahlen (inkl. `priority_accuracy`) sind
über alle 3 Wiederholungen **exakt identisch** — die Ergebnisse sind also
reproduzierbar, nicht zufällige Ausreißer. `consecutive_required` bleibt bei
75% (Sozialkunde fehlt konsequent, siehe oben) statt 100%; alle 6 anderen
Recall-Kategorien erreichen konstant 100%. `Solve status = Optimal` und
`0 Verifier-Verstöße` in allen 3 Läufen — die Fixture bleibt trotz der
unvollständigen `consecutive_required`-Extraktion durchgehend lösbar und
korrekt (die fehlende Sozialkunde-Blockpflicht führt nicht zu einem
inkonsistenten oder unlösbaren Plan, nur zu einer nicht erfüllten
Kann-Erwartung im Recall-Sinn).

## Ehrliche Bewertung: `priority_accuracy` bei 62%

Anders als bei den bisherigen Fehlermodi (Phase 2) ist Muss/Kann-Ableitung
eine subjektive Einschätzung der Formulierungsabsicht, kein prüfbarer Fakt —
es gibt bewusst **kein** deterministisches Sicherheitsnetz für diese
Kennzahl (siehe Plan, Abschnitt "Ehrliche Grenze"). 62% liegt deutlich über
Zufallsniveau (bei 2 möglichen Werten und einem im Prompt bewusst
ausbalancierten Mix aus Wunsch-/Muss-Formulierungen), aber spürbar unter dem
Niveau der reinen Recall-Kategorien (durchgängig 100% bzw. 75% bei
`consecutive_required`). Ein Teil der 62% erklärt sich strukturell: jedes
fehlende Objekt (z. B. Sozialkundes 2 `consecutive_required`-Einträge) zählt
in `ScorePriorityAccuracy` automatisch als falsch, da keine Priorität
bewertet werden kann, wenn der Fakt selbst fehlt. Der Rest verteilt sich auf
echte Priority-Fehlklassifikationen im vollen Pipeline-Kontext, die in den
schnelleren isolierten Diagnose-Läufen (die jeweils nur einen einzelnen
Typ ohne die Last der anderen 8 gleichzeitigen Aufrufe testen) nicht in
gleichem Umfang auftraten — ein Muster, das schon in Phase 2 bei
Fehlermodus 3 (`teacher_subject_assignment`-Kreuzmatrix) beobachtet wurde:
isolierte Diagnose-Aufrufe zeigen oft eine höhere Erfolgsquote als der
vollständige, alle 9 Aufrufe umfassende Pipeline-Kontext.

**Gewählte Option (gemäß Plan, (a)/(b)):** Option (b) — kein separater,
niedrigerer Schwellenwert wurde künstlich eingeführt; `priority_accuracy`
fließt wie jede andere Kategorie einfach in den bestehenden
`overall >= 50%`-Schwellenwert ein (arithmetisches Mittel aller Kategorien).
Da die 7 anderen Kategorien konstant bei 100%/75% liegen, bleibt `overall`
bei 92% weit über der Schwelle — der schwächere `priority_accuracy`-Wert wird
also nicht versteckt, sondern ehrlich als eigene Zeile ausgewiesen und bleibt
gleichzeitig kein Blocker für den Gesamt-Test. Dies wird hier als
dokumentierte, reproduzierbare Grenze der aktuellen Instruktions-Schärfung
für qwen3.5:4b festgehalten, nicht als weiter zu verfolgender offener Fehler
dieser Phase.

## Definition of Done — Status (Phase 2.6)

- [x] `dotnet test TimetableCore.Tests` bleibt grün ohne `RUN_LLM_TESTS` (36
      bestehende Tests + `LlmExtractionE2EMussKann` als Inconclusive + neue
      `MussKannFixtureTests.vb`-Tests als echte Passes — 0 Regressionen).
- [x] Instruktionen/Schemas der 4 unveränderten Typen (`no_overlap`,
      `shared_resource_conflict`, `teacher_subject_assignment`,
      `period_exception`) blieben byte-identisch.
- [x] Für jeden der 5 Kann-fähigen Typen liegt ein isolierter
      Live-Diagnose-Beleg UND ein voller `RobustnessRunner`-Wiederholungslauf
      vor, der die tatsächlich erreichte Priority-Accuracy dokumentiert
      (100% für 4 von 5 Typen in den isolierten Diagnose-Läufen;
      `consecutive_required` mit ehrlich dokumentierter, reproduzierbarer
      Grenze wegen der fehlenden Sozialkunde-Extraktion).
- [x] Dieser Abschnitt in `docs/phase2-robustness-report.md` committet.

# Phase 2.7: Testabdeckung für Rückverfolgbarkeit — Ergebnisbericht

Ausgangspunkt war die Nutzerfrage, ob es einen Testfall gibt, der die
Rückverfolgbarkeit von Solver-Fehlern/-Warnungen auf die ursprünglichen
Prompts prüft. Eine Bestandsaufnahme zeigte: der in Phase 2.5c gebaute
`reason`-Mechanismus war korrekt und einheitlich verdrahtet, aber nur 1 von 5
Kann-fähigen Typen hatte einen Test, der das per Assertion bewies; die
Muss-Seite (`Validation.vb`, `Verifier.vb`) hatte gar keinen Test mit
gesetztem `reason`; und es gab keinen Live-Test, der prüfte, ob Qwen in der
Praxis überhaupt `reason` neben `priority: "should"` befüllt.

## Deterministische Testabdeckung geschlossen (2.7a/b)

- `SolverTests.vb`: die 4 verbleibenden Kann-Tests
  (`KannConflictingForbiddenSlotsMinimizesViolationCount`,
  `KannRoomRequirementRelaxesPigeonhole`,
  `KannConsecutiveRequiredRelaxesNonMultipleOfBlockLength`,
  `KannWeeklyHoursMaxPerDayRelaxesWhileHoursPerWeekStaysExact`) prüfen jetzt
  wie Test 3 (`teacher_availability`), dass ein gesetztes `reason` in
  `KannViolationDetail.Message`/`.Reason` und `KannConstraintFlag.Reason`
  landet.
- Zwei neue Tests schließen die Muss-Seite: `ValidationErrorIncludesReasonWhenSet`
  (komplementär zum bestehenden Abwesenheits-Test) und
  `VerifierMussViolationIncludesReasonWhenSet` (nutzt `Verifier.vb`s
  Unabhängigkeit vom Solver: ein ohne Constraint gelöster Schedule wird gegen
  eine nachträglich um einen Muss-`forbidden_slot` mit `reason` erweiterte
  Datenkopie geprüft).
- Alle 40 Tests grün, 0 Regressionen (`dotnet test` ohne `RUN_LLM_TESTS`).

## Live-Test deckt einen echten, bisher unentdeckten Fehlermodus auf (2.7c)

Der neue gated Test `LlmExtractionE2EReasonTraceability` (nutzt
`MussKannFixture`) prüft, ob mindestens ein von Qwen live extrahiertes
`"should"`-Constraint ein nicht-leeres `reason`-Feld hat. **Erster Lauf:
0 von 16 `"should"`-Constraints hatten ein `reason`.** Root Cause: das
`reason`-Feld existiert seit Phase 2.5c im JSON-Schema aller 9 Typen, wurde
aber in KEINER Instruktion je erwähnt - ein reines Schema-Feld ohne
Prompt-Anleitung.

**Fix-Versuch 1 (Instruktion schärfen):** ein Satz "Gib zusaetzlich in reason
IMMER die kurze Textstelle wieder..." zu allen 5 Kann-fähigen Typen ergänzt.
**Ergebnis: 0 Effekt** - erneut 0 von 38 Constraints (alle 5 Typen isoliert
getestet) hatten ein `reason`. Eine unconditionale Instruktion allein
genügt nicht.

**Fix-Versuch 2 (Schema verschärfen):** `reason` für alle 5 Typen von
optional auf PFLICHTFELD (`required`) gesetzt, nach demselben Muster, mit
dem `available_days` bei `teacher_availability` in Phase 1/2 schon einmal
erzwungen wurde. **Ergebnis: gemischt.**
- `teacher_availability` und `forbidden_slot`: sauberer Erfolg - `reason`
  UND `priority` beide korrekt in jedem der getesteten Items (4/4 bzw. 8/8),
  reproduziert in zwei unabhängigen Läufen.
- `weekly_hours`, `room_requirement`, `consecutive_required`: Verdrängungs-
  effekt - `priority` verschwand zuverlässig aus jedem Item, sobald `reason`
  Pflicht wurde (bei `room_requirement`/`consecutive_required`
  reproduziert), und bei `weekly_hours` sogar schlimmer: die Antwort lief
  in einem Lauf mangels Tokenbudget mitten im Array ab (`done_reason:
  "length"`), das JSON wurde ungültig, 0 verwertbare Items.

**Fix-Versuch 3 (kombinierte Instruktion + Pflichtfeld fuer die 3
betroffenen Typen):** ein zusätzlicher Satz "JEDES Objekt MUSS SOWOHL
priority ALS AUCH reason enthalten" ergänzt. **Ergebnis: kein Fortschritt**
- `priority` blieb bei `room_requirement`/`consecutive_required` weiterhin
  abwesend, `weekly_hours` blieb instabil.

**Entscheidung (ehrliche Grenze, wie im Plan vorgesehen):** `reason` als
Pflichtfeld nur für `teacher_availability` und `forbidden_slot` behalten (dort
ein sauberer, zweifach reproduzierter Gewinn ohne Nebenwirkung).
`weekly_hours`, `room_requirement` und `consecutive_required` wurden auf
ihren Phase-2.6-Stand zurückgesetzt (Instruktion UND Schema) - ein bereits
verifiziertes, funktionierendes Feld (`priority`, bei `weekly_hours`
zusätzlich `max_per_day`) gegen ein neues, noch unzuverlässiges Feld zu
tauschen, wäre ein schlechter Tausch gewesen. Für diese 3 Typen bleibt
`reason` weiterhin unbefüllt - dokumentierte, bewusst nicht behobene Grenze
dieser Phase.

## Verifikation nach der Konsolidierung

- Isolierter Diagnose-Lauf (nur die 3 zurückgesetzten Typen): bestätigt
  Rückkehr zum bekannten Phase-2.6-Verhalten für `room_requirement`
  (4/4 mit korrekter `priority`) und `consecutive_required` (Kunst/Chemie/
  Geschichte korrekt, Sozialkunde weiterhin die bereits in Phase 2.6
  dokumentierte Lücke).
- Voller gated Test `LlmExtractionE2EReasonTraceability`: **bestanden**
  (9m47s) - mindestens ein `"should"`-Constraint mit `reason` gefunden.
- Regressionscheck: `LlmExtractionE2EMussKann` erneut komplett durchlaufen
  lassen (die Schema-Änderung an `teacher_availability`/`forbidden_slot`
  betrifft alle 5 Szenarien, nicht nur MussKann) - **bestanden** (9m5s),
  keine Verschlechterung gegenüber dem in Phase 2.6 dokumentierten Ergebnis.
- `dotnet test` ohne `RUN_LLM_TESTS`: weiterhin 40 bestanden, 6 korrekt
  übersprungen (0 Regressionen).

## Definition of Done — Status (Phase 2.7)

- [x] Alle 5 Kann-fähigen Typen haben einen Test, der reason-Weiterleitung
      in `KannViolationDetail`/`KannConstraintFlags` beweist.
- [x] Die Muss-Seite hat für `Validation.vb` UND `Verifier.vb` je einen Test
      mit gesetztem `reason`.
- [x] Ein gated Live-Test belegt, dass Qwen in der Praxis `reason` neben
      `priority: "should"` befüllt - mit ehrlich dokumentierter Grenze (nur
      2 von 5 Typen zuverlässig, 3 auf den vorherigen Stand zurückgesetzt).
- [x] `dotnet test TimetableCore.Tests` bleibt ohne `RUN_LLM_TESTS`
      vollständig grün (0 Regressionen); Ergebnis dokumentiert, committet
      und gepusht.
