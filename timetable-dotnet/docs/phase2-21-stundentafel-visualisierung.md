# Phase 2.21: Stundentafel-Visualisierung im SchoolTestRunner (JSON + HTML)

Dieser Bericht dokumentiert Phase 2.21 (siehe Plan, Abschnitt "Phase 2.21
(feingeplant)"): der `SchoolTestRunner` bekommt eine visuelle
Gesamtübersicht der gelösten Stundenpläne - eine **"Stundentafel"**.

## Kontext

Bisher schrieb `run` nur Markdown-Raster: ein separates Grid pro Klasse/
Lehrkraft in `output/stundenplan.md`, und immer nur die EINE beste
gefundene Lösung (`Solver.SolveTop(..., maxSolutions:=1)`, fest
verdrahtet). Nutzerwunsch: eine einzige tabellarische
Gesamt-Übersicht über ALLE Klassen zugleich (Wochentage in Spalten,
Schulstunden in Zeilen; Wochentage zusätzlich durch Klassenstufen
unterteilt, Schulstunden zusätzlich durch die Parallelklassen einer
Klassenstufe), als HTML dargestellt - und dafür soll eine JSON-Datei mit
ALLEN vom Solver gefundenen Lösungen (nicht nur der besten) geschrieben
werden.

## Nutzerentscheidung

Die HTML-Seite ist ein **wiederverwendbarer, generischer
JavaScript-Viewer**, der die JSON-Daten lädt und die verschachtelte
Tabelle zur Laufzeit im Browser aufbaut - keine bei jedem Lauf in VB.NET
fertig vorgerenderte statische Tabelle. Da ein direktes Öffnen einer
lokalen HTML-Datei per Doppelklick (`file://`) `fetch()`-Aufrufe auf eine
benachbarte Datei am Browser-CORS-Schutz scheitern lässt, werden die
JSON-Daten **inline in die HTML eingebettet** (in einem
`<script type="application/json">`-Block) statt zur Laufzeit
nachzuladen - funktioniert dadurch garantiert per Doppelklick ohne
lokalen Webserver. Die eigenständige `.json`-Datei wird zusätzlich
geschrieben (wie explizit gewünscht, z.B. für spätere GUI-/Tooling-
Weiterverwendung), enthält aber exakt dieselben Daten wie der
eingebettete Block - eine einzige `JsonObject`-Konstruktion in VB.NET
(`Formatting.ToStundentafelJson`) ist die alleinige Quelle für beides.

## Technische Mechanik

- `TimetableCore/Formatting.vb`: neue `ToStundentafelJson(bestand, data,
  multiResult) As JsonObject`. Da `Klasse` (Stammdaten.vb) kein eigenes
  "Parallel-Buchstabe"-Feld hat - die einzige im Projekt etablierte
  Konvention ist `Klasse.Name = "<Klassenstufe.Nummer><Kleinbuchstabe>"`
  (z.B. "1a", "2b") -, parst eine neue private `ParallelIndexOf`-Funktion
  den Buchstaben aus dem Namen (`Asc(buchstabe)-Asc("a"c)`); bei
  Nicht-Konformität fällt sie auf eine alphabetische Reihenfolge
  innerhalb der Klassenstufe zurück statt zu werfen. Jede Klassenstufe
  wird auf die schulweit größte Parallelklassen-Anzahl gepolstert (`null`
  an fehlenden Buchstaben-Positionen - wichtig: eine LÜCKE wie "nur 2b,
  kein 2a" landet korrekt bei Index 0=null/Index 1="2b", nicht einfach ans
  Ende angehängt, sonst wäre die Spalten-Ausrichtung über Klassenstufen
  hinweg falsch). Pro `ScoredSolution` wird `Verifier.VerifySchedule`
  erneut aufgerufen (Defense-in-Depth, sollte strukturell immer 0 sein,
  macht das aber sichtbar statt blind vorauszusetzen) und
  `ToClassGrids`/eine neu extrahierte `GridToJsonObject`-Hilfsfunktion
  (aus dem bereits bestehenden `ToJsonPerClass` herausgezogen, ohne
  dessen Verhalten zu ändern) liefert den Zelleninhalt - inkl. der bereits
  bestehenden Phase-2.20-Parallelgruppen-Zusammenführung (mehrere
  gleichzeitige Sessions einer Klasse werden zu einer Zelle kombiniert).
- `SchoolTestRunner/Templates/stundentafel.html`: als
  `<EmbeddedResource>` in `SchoolTestRunner.vbproj` eingebettet
  (kein `CopyToOutputDirectory`-Pfadrisiko). **Live überraschender
  Befund:** trotz `Include="Templates\stundentafel.html"` im `.vbproj`
  ergab sich KEIN `"Templates."`-Präfix im tatsächlichen
  Ressourcennamen - der tatsächliche Name ist `"SchoolTestRunner.
  stundentafel.html"`, nicht das angenommene `"SchoolTestRunner.
  Templates.stundentafel.html"`. Per `Assembly.GetManifestResourceNames()`
  live bestätigt (die absichtlich klare Fehlermeldung in
  `StundentafelHtml.BuildStundentafelHtml` listete beim ersten Testlauf
  sofort den tatsächlichen Namen auf - genau der Zweck dieser
  Fehlerbehandlung).
- `SchoolTestRunner/StundentafelHtml.vb`: `BuildStundentafelHtml(jsonText)`
  ersetzt den Platzhalter `__STUNDENTAFEL_JSON__` in der Vorlage durch den
  (defensiv `</script`-escapten) JSON-Text.
- `SchoolTestRunner/Run.vb`: `RunConfig.MaxSolutions` (Default 1,
  unverändertes Verhalten für jede Schule ohne `max_solutions` in ihrer
  `config.yaml`) steuert jetzt `SolveTop`s `maxSolutions`-Parameter (vorher
  fest auf 1 verdrahtet). Nach dem bestehenden `stundenplan.md`-Schreiben
  werden `output/stundenplan.json` + `output/stundentafel.html` ergänzt.

## Live-Verifikation

- **`dotnet test TimetableCore.Tests`**: 5 neue Tests in
  `StundentafelJsonTests.vb` - Polsterung am Ende, Lücke an einer
  Buchstaben-Position (nicht nur zählbasiert), nicht-konformer
  Klassenname (alphabetischer Fallback statt Absturz), Quality-/
  Verstoß-Werte pro Lösung, UND ein Beweis, dass `muss_violation_count`
  echte Zähne hat (ein handgebauter, tatsächlich kollidierender Schedule
  wird als Verstoß erkannt, nicht blind auf 0 gesetzt). Alle grün, 0
  Regressionen gegenüber dem Phase-2.20-Stand.
- **Beide Referenzschulen** (`bw-grundschule-beispiel`,
  `bw-gms-beispiel`) bekamen `max_solutions: 5` in ihrer `config.yaml`
  ergänzt und liefen live durch `run --all`: beide PASS, 0 Verstöße,
  `output/stundenplan.json` + `output/stundentafel.html` wurden erzeugt.
  Gesamtlaufzeit für beide Schulen zusammen: ~3 Minuten (bestätigt die
  vorab getroffene Annahme, dass das Gesamt-Zeitbudget unabhängig von
  `max_solutions` durch `solve_time_limit_s` gedeckelt bleibt).
- **Ehrlicher Befund:** beide Referenzschulen fanden trotz
  `max_solutions: 5` live tatsächlich nur EINE Lösung
  (`stop_reason: "TimeLimitReached"`) - weil `Run.vb` `perSolveTimeLimitS`
  identisch zu `totalTimeLimitS` setzt (`cfg.SolveTimeLimitS` für beide),
  und die erste Solve-Iteration bei diesen beiden Schulen bereits das
  komplette Zeitbudget aufbraucht, ohne `Optimal` zu beweisen (`Feasible`
  zurückgegeben - bereits vor dieser Phase bekannt, siehe
  `stundenplan.md`s bestehender Feasible-Hinweistext). Das ist kein Bug
  der neuen Mechanik selbst (der Export-Pfad ist für N=1 korrekt und
  vollständig getestet), sondern eine bereits bestehende, orthogonale
  Eigenschaft dieser beiden konkreten Testfälle bei ihrem aktuellen
  `solve_time_limit_s` - `tests/README.md` erklärt das jetzt explizit,
  statt es zu verschweigen. Um den Mehr-Lösungen-Fall (`max_solutions >
  1`, Dropdown-Umschalter) trotzdem tatsächlich zu verifizieren statt nur
  anzunehmen: eine synthetische 2-Lösungen-JSON (abgeleitet von der
  echten `bw-grundschule-beispiel`-Ausgabe, eine Zelle + Kennzahlen
  gezielt verändert) wurde durch dieselbe Vorlage gerendert und per
  Playwright/Chromium screenshotet - Dropdown-Auswahl UND die
  aktualisierte Tabelle/Zusammenfassungszeile bestätigt sichtbar korrekt.
- **Manuelle Browser-Prüfung** (Playwright/Chromium, `file://`, wie vom
  Design vorausgesetzt): `stundentafel.html` von
  `bw-grundschule-beispiel` geöffnet und gescreenshotet - Wochentag-/
  Klassenstufen-Kopfzeilen-Verschachtelung, Perioden-/Parallelklassen-
  Zeilen-Verschachtelung (inkl. der Phase-2.20-Parallelgruppen-Zelle
  "Ethik / Religion-ev / Religion-kath"), Zusammenfassungszeile - alles
  korrekt dargestellt.
- Volle Regressionssuite (`dotnet test TimetableCore.Tests`) als
  Abschlussgate: 0 Regressionen.

## Definition of Done

- `dotnet test TimetableCore.Tests` bleibt vollständig grün, 0
  Regressionen, inkl. der 5 neuen `StundentafelJsonTests.vb`-Tests.
- `ToStundentafelJson` liefert nachweislich korrekt gepolsterte
  Klassenstufen-Arrays (inkl. Lücken-Fall) und einen echten Zähne
  zeigenden `muss_violation_count`.
- Beide Referenzschulen laufen live mit `max_solutions: 5` durch, PASS, 0
  Verstöße, `output/stundenplan.json` + `output/stundentafel.html` sind
  erzeugt und committet.
- `stundentafel.html` wurde manuell/per Screenshot geprüft (Kopfzeilen-
  Verschachtelung, Zeilen-Verschachtelung, Lösungs-Umschalter über eine
  ergänzende synthetische Mehr-Lösungen-Prüfung).
- Bestehende Schulen ohne `max_solutions` in ihrer `config.yaml` bleiben
  beim bisherigen `maxSolutions=1`-Verhalten.
- `tests/README.md` dokumentiert das neue Feature vollständig, inkl. des
  ehrlichen Befunds zu `max_solutions` vs. `solve_time_limit_s`.
- Committet und gepusht auf `claude/qwen-3.5-sandbox-test-ubfhmo`.

**Kritische Dateien:**
- `timetable-dotnet/TimetableCore/Formatting.vb` - `GridToJsonObject`,
  `ToStundentafelJson`.
- `timetable-dotnet/TimetableCore.Tests/StundentafelJsonTests.vb` (neu).
- `timetable-dotnet/SchoolTestRunner/Templates/stundentafel.html` (neu).
- `timetable-dotnet/SchoolTestRunner/StundentafelHtml.vb` (neu).
- `timetable-dotnet/SchoolTestRunner/SchoolTestRunner.vbproj` -
  `<EmbeddedResource>`.
- `timetable-dotnet/SchoolTestRunner/Run.vb` - `RunConfig.MaxSolutions`,
  neue Output-Schritte.
- `timetable-dotnet/tests/{bw-grundschule-beispiel,bw-gms-beispiel}/input/config.yaml` -
  `max_solutions: 5`.
- `timetable-dotnet/tests/{bw-grundschule-beispiel,bw-gms-beispiel}/output/*`.
- `timetable-dotnet/tests/README.md`.
