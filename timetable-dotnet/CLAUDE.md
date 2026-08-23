# timetable-dotnet - Arbeitsregeln

## Testumfang nach Aenderungsbereich (feste Regel)

Den Prueful auf den tatsaechlichen Aenderungsbereich zuschneiden, statt
pauschal die volle Suite zu fahren:

- **Aenderungen an `TimetableCore/`** (Solver, SolveTopObjective,
  Validation, Verifier, Formatting, ScheduleQuality, Stammdaten,
  Lehrereinsatzplanung, ...): volle Suite ist Pflicht -
  `dotnet test TimetableCore.Tests` muss vollstaendig gruen bleiben
  (Definition-of-Done-Konvention aller Phasen-Berichte in `docs/`).
- **Aenderungen NUR an der Klassenbildung** (`TimetableCore/
  Klassenbildung.vb`, `TimetableCore/KlassenbildungQuality.vb`,
  `TimetableWorkflow/KlassenbildungLauf.vb`, `TimetableWorkflow/
  KlassenbildungBericht.vb`, `TimetableYaml/YamlKlassenbildung.vb`,
  `SchoolTestRunner/KlassenRun.vb`, `tests/*/input/klassenbildung.yaml`): die
  EIGENE Suite genuegt - `dotnet test Klassenbildung.Tests` (laeuft in
  unter einer Sekunde; die Klassenbildung ist ein eigenstaendiges
  Modul ohne jede Kopplung an Solver/SolveTop, die teuren
  Stundenplan-Tests exerzieren sie nicht und umgekehrt). Bei
  querschneidenden Aenderungen (z.B. gemeinsame Helfer) beide Suiten
  fahren.
- **Aenderungen an `TimetableYaml/`, `TimetableWorkflow/`, `TimetableProjekt/` oder `TimetableGui/`** (seit dem
  GUI-Unterbau, siehe `docs/gui-implementierungsplan.md`): die eigenen
  Suiten `dotnet test TimetableYaml.Tests` (<1s) und
  `dotnet test TimetableWorkflow.Tests` (~3s), `dotnet test TimetableProjekt.Tests`
  (~2s) bzw. `dotnet test TimetableGui.Tests` (~45s, faehrt echte
  Klassenbildungs-Laeufe). Beruehrt die Aenderung die
  Pipeline-Orchestrierung oder die Berichts-Zeichenketten, zusaetzlich
  `dotnet run --project SchoolTestRunner -- run --all` - nur der echte
  Lauf belegt, dass die committeten Beispiel-Outputs unveraendert bleiben.

- **Aenderungen nur an `SchoolTestRunner/`-CLI oder
  `TimetableWorkflow/Templates/*.html`** (Viewer): die Suite
  deckt beides NICHT ab - stattdessen gezielt pruefen:
  1. `dotnet build TimetableCore.sln` (Compile inkl. Embedded-Resource),
  2. `dotnet run --project SchoolTestRunner -- render <schule>` gegen
     beide Beispiel-Schulen (baut stundentafel.html aus vorhandener
     stundenplan.json neu - KEIN teurer Solver-Lauf noetig),
  3. Headless-Browser-Smoke gegen die generierten Seiten. Unter Windows:
     `powershell -File tools\viewer-smoke.ps1` (nutzt Edge, prueft alle
     Viewer-Seiten unter `tests/`). Unter Linux liegt Chromium unter
     `/opt/pw-browsers/`; in beiden Faellen fuehrt `--headless --dump-dom
     --virtual-time-budget=...` das Inline-JS aus. Fuer Interaktionstests
     ein kleines Skript vor `</body>` injizieren, das Events dispatcht und
     Ergebnisse in `document.title` schreibt.
  Aendert sich das JSON-Schema des Exports (`Formatting.
  ToStundentafelJson` liegt in TimetableCore!), gilt wieder die volle
  Suite plus `StundentafelJsonTests`.
- **Aenderungen nur an `tests/<schule>/input/*.yaml` oder Doku**: kein
  Testlauf noetig; Config-Aenderungen werden per `run <schule>`-Lauf
  live belegt und die Outputs mitcommittet (bestehende Konvention).

"Volle Suite" heisst seit dem GUI-Unterbau ALLE SECHS Testprojekte, am
einfachsten ueber `dotnet test TimetableCore.sln`.

Hintergrund: Die Regel entstand, nachdem fuer einen reinen
Viewer-Commit die volle 5-Minuten-Suite lief, obwohl sie den
geaenderten Code gar nicht exerziert - Build + render + Browser-Smoke
waren dort die tatsaechlich tragende Validierung.

## Weitere Konventionen (Kurzreferenz)

- Beispiel-Outputs (`tests/*/output/`) sind generiert und werden nach
  jedem Lauf mitcommittet; die GitHub-Pages-Kopien unter
  `../stundentafel/*.html` nachziehen (`cp` aus `output/`). GitHub
  Pages liefert NUR den `main`-Branch aus - Merge nach `main` nur auf
  explizite Nutzeranweisung.
- Messlaeufe ehrlich dokumentieren (inkl. negativer Befunde) als
  Kommentar in der jeweiligen `config.yaml` bzw. in `docs/`; bei
  `num_workers > 1` sind Laeufe trotz fixem Seed nicht deterministisch -
  Einzellaeufe entsprechend vorsichtig interpretieren.
- Lange Laeufe (>10 Min) nicht direkt im Tool-Aufruf starten, sondern
  als `setsid nohup`-Skript entkoppeln und per Log ueberwachen.

## Viewer-Artifacts (schneller Vorschau-Kanal ohne main-Merge)

Neben den GitHub-Pages-Kopien (main-Stand) werden die beiden
Stundentafel-Viewer als Claude-Artifacts bereitgestellt - private,
stabil verlinkte Seiten fuer Zwischenstaende direkt nach einem Lauf:

- Grundschule: https://claude.ai/code/artifact/d644d791-48e1-4bbd-89fa-02d2cf13fe09
- GMS: https://claude.ai/code/artifact/ae942861-a1ae-4664-a1f5-51ac9ce702d1
- Klassenbildung Grundschule: https://claude.ai/code/artifact/d00f8a57-75ca-4809-9690-19613dd071a1
  (Quelle: tests/bw-grundschule-beispiel/output/klassenbildung.html,
  Titel "Klassenbildung Grundschule", Favicon 🧩)

Aktualisierung nach jedem `run`-Lauf: den aeusseren Dokumentrahmen der
generierten `output/stundentafel.html` strippen (doctype/html/head/
body-Tags entfernen - das Artifact ergaenzt den Rahmen selbst; den
`<title>` dabei auf "Stundentafel Grundschule" bzw. "Stundentafel GMS"
setzen) und per Artifact-Tool mit `url` = obiger Link publizieren -
NIE ohne `url` (das erzeugte ein neues, separates Artifact). Favicons
stabil halten: Grundschule 🎒, GMS 🏫.
