# Architekturdokumentation TimetableCore (arc42)

Diese Dokumentation folgt der [arc42](https://arc42.de)-Gliederung und beschreibt
die .NET/VB.NET-Lösung unter `timetable-dotnet/` - den Nachfolger des
eingefrorenen Python-Prototyps unter `timetable/`. Stand: Phase 2.25 plus
Code-Review-Umsetzung (lexikografische Stufen als `SolveTop`-Default,
`occupied_window`/Dichte-Stufe), Rhythmisierung (`subject_period_window`),
Mehr-Zuteilungs-Pipeline mit Lehrer-Äquivalenzklassen, ausgebaute
Self-contained-HTML-Viewer und die vorgelagerte Klassenbildungs-Stufe
(Stufe 0, K1-K5 + Viewer U1-U4). Entstehungsgeschichte der einzelnen
Fähigkeiten: `docs/phase2-*.md`, `docs/code-review-cpsat-performance.md`,
`docs/viewer-ausbau-plan.md`, `docs/klassenbildung-plan.md` und
`docs/klassenbildung-ui-konzept.md`.

Ergänzend zu diesem Dokument beschreibt
[`docs/json-constraints-reference.md`](json-constraints-reference.md) das
JSON-Wire-Format (Entities/Constraints) im Detail, inkl. Beispielen für jeden
Constraint-Typ.

## 1. Einführung und Ziele

### 1.1 Aufgabenstellung

TimetableCore ist der Lösungskern eines Stundenplan-Planungswerkzeugs für
Schulen. Aus einer strukturierten Beschreibung einer Schule (Klassen,
Lehrkräfte, Fächer, Räume, Zeitraster) und einer Liste von
Rahmenbedingungen ("Constraints", z.B. "Lehrer X ist nur montags bis
mittwochs verfügbar") berechnet es einen widerspruchsfreien Stundenplan.
Die Rahmenbedingungen können wahlweise strukturiert (Formular/JSON) oder
per Freitext-Chat (Deutsch) eingegeben werden - Letzteres wird von einem
lokal laufenden LLM (Qwen, via Ollama) in dieselbe JSON-Constraint-Form
übersetzt, die auch der strukturierte Weg erzeugt.

Zwei grundsätzlich verschiedene Schulformen werden abgedeckt:

- **Klassenbasierter Unterricht** (Sekundarstufe I, Grundschule): jede
  Klasse hat ein festes Fächer-/Stundendeputat, Lehrkräfte unterrichten
  ganze Klassen.
- **Kursbasierter Unterricht** (Kursstufe/Oberstufe, BW-Qualifikationsphase):
  Schüler wählen individuell Kurse (Leistungs-/Grundkurse); Überschneidungsfreiheit
  gilt pro Schüler(-Wahlprofil), nicht pro Klasse. Modelliert über das in
  echten Schulen verbreitete **Schienenmodell**.

Der eigentlichen Stundenplanung vorgelagert existieren zwei
eigenständige Planungsstufen: die **Lehrereinsatzplanung** (Stufe 1:
Lehrkraft → Klasse/Fach, siehe 6.6) und - noch davor - die
**Klassenbildung** (Stufe 0: Einteilung eines Einschulungs-/Neubildungs-
Jahrgangs in Parallelklassen nach pädagogischen Regeln, siehe 6.9 und
`docs/klassenbildung-konzept.md`). Beide sind CP-SAT-Teilmodelle ohne
Tag/Periode-Bezug; die Klassenbildung ist bewusst ein vollständig
entkoppeltes Modul mit eigener Testsuite (siehe 5.1), da ihr Ergebnis
Menschen-moderiert ist (der Solver liefert Vorschläge, die Schulleitung
entscheidet) und sie keinerlei Code mit der Stundenplan-Pipeline teilt.

### 1.2 Qualitätsziele

| Rang | Qualitätsziel | Motivation / Szenario |
|---|---|---|
| 1 | **Korrektheit** | Ein als "lösbar" gemeldeter Plan darf keine Muss-Verletzung enthalten. Erzwungen durch einen von der Solver-Logik komplett unabhängigen `Verifier.vb` (siehe 8.2) - jeder Solve-Test prüft `Verifier.VerifySchedule(...).Count = 0`, nie nur den CP-SAT-Status. |
| 2 | **Nachvollziehbarkeit** | Warum wurde eine Regel gelockert/abgelehnt? Das optionale `reason`-Feld wird bis in Validierungs-/Verifier-Meldungen durchgereicht (siehe 8.4). |
| 3 | **Robustheit der LLM-Extraktion** | Freitext ist mehrdeutig und das Modell nicht deterministisch. Wiederholungsstudien (`RobustnessRunner`) messen Vollständigkeits-Scores über mehrere Läufe; gefundene systematische Fehlermodi werden entweder durch schärfere Instruktionen oder durch deterministische Nachbearbeitung behoben (`docs/phase2-robustness-report.md`). |
| 4 | **Determinismus des Solvers** | Gleiche Eingabe + fixer `seed`/`numWorkers:=1` → reproduzierbar gleiche Lösung (Testbarkeit, Nachvollziehbarkeit für Nutzer). |
| 5 | **Portabilität / Plattformunabhängigkeit des Kerns** | `TimetableCore` hat keine UI-Abhängigkeit und läuft identisch unter Linux (Sandbox-Entwicklung) und Windows (Ziel-GUI) - reine .NET-8-Klassenbibliothek + `Google.OrTools.Sat` + `HttpClient`. |
| 6 | **Erweiterbarkeit ohne Kernänderung** | Neue Planungsstufen (Kursstufe, kombinierte Schule) wurden überwiegend durch neue Module erreicht, die die bestehende `Solver.Solve()`-Pipeline über synthetisch konstruierte JSON-Szenarien wiederverwenden, statt sie zu verändern (siehe 8.6). |

### 1.3 Stakeholder

| Rolle | Erwartungshaltung |
|---|---|
| Schulleitung / Stundenplaner (Endnutzer, GUI-Zielgruppe, nachgelagert) | Bedienbare Desktop-Anwendung unter Windows, Chat- und Formulareingabe, lesbare Stundenpläne pro Klasse/Lehrer/Wahlprofil. |
| Entwickler (VB.NET-Kenntnis, aktueller alleiniger Umsetzer) | Wartbarer, gut getesteter Kern; Portierung ohne Python-Laufzeitabhängigkeit. |
| Dieses Projekt selbst (Sandbox-Entwicklung) | Vollständige Entwicklung/Test in einer Linux-Sandbox, GUI-Bau nachgelagert unter Windows. |

## 2. Randbedingungen

| Randbedingung | Konsequenz |
|---|---|
| Zielplattform GUI: **Windows** (Mac/Linux optional/später) | GUI-Technologie **WPF + WebView2** geplant (Phase 3, noch nicht begonnen; ersetzt den früheren WinForms-Vermerk): WPF für Formulare/Editoren mit Databinding auf das typisierte `Stammdaten.vb`-Modell, WebView2 hostet die bestehenden Self-contained-HTML-Viewer für Klassenzuordnung und Stundenplan-Interaktion unverändert (siehe 8.10 und `docs/gui-datenhaltung-konzept.md`). Der Kern selbst bleibt plattformneutral. |
| Entwickler kennt sich mit **VB.NET** aus | Sprachwahl VB.NET statt C#/F# für den gesamten .NET-Code. |
| **Kein Python** im finalen Produkt | Kompletter Python-Prototyp (`timetable/`) 1:1 nach VB.NET portiert; Python bleibt nur als eingefrorenes Referenz-Orakel im Repo, wird nicht mitausgeliefert. |
| Entwicklung/Test so lange wie möglich **in der Sandbox** (Linux, kein Windows verfügbar) | `Google.OrTools` läuft unter Linux via `Google.OrTools.runtime.linux-x64` (transitive NuGet-Abhängigkeit); GUI-Bau explizit nachgelagert. |
| LLM-Anbindung **lokal, kein Cloud-Anbieter** | Ollama (`http://127.0.0.1:11434`), Modell `qwen3.5:4b`, direkt per `HttpClient` aus VB.NET - kein Wrapper, kein separater Python-Prozess. |
| CP-SAT-Solver: **Google.OrTools.Sat 9.15.6755** (NuGet) | Bindet die komplette Solver-Modellierung an die tatsächlich installierte API-Version; mehrfach dokumentierte Doku/DLL-Diskrepanzen wurden per Live-Reflection statt Blindvertrauen in die XML-Doku verifiziert (siehe 9). |
| .NET 8, MSTest | Testausführung via `dotnet test`, keine weiteren Testframework-Abhängigkeiten. |

## 3. Kontextabgrenzung

### 3.1 Fachlicher Kontext

```
                         ┌──────────────────────────┐
   Freitext (Deutsch) →  │                          │  →  Stundenplan
   "Herr Müller ist nur  │      TimetableCore        │     (pro Klasse /
    Mo-Mi verfügbar..."  │                          │      Lehrer /
                         │  Validation → Solver →    │      Wahlprofil)
   Strukturierte Daten → │  Verifier → Formatting    │  →  Validierungs-
   (Entities/Constraints)│                          │      fehler/Warnungen
                         └──────────────────────────┘
                                    ↑
                         Ollama/Qwen (lokal, optional -
                         nur für den Freitext-Weg nötig)
```

- **Eingabe A (strukturiert):** JSON nach dem in
  `json-constraints-reference.md` beschriebenen Schema - `entities`
  (Klassen/Lehrer/Fächer/Räume/Zeitraster, optional Kurse/Schienen) plus
  `constraints` (typisierte Regelobjekte).
- **Eingabe B (Freitext):** deutscher Fließtext, von `LlmExtraction.vb`
  in genau dasselbe `constraints`-JSON übersetzt (siehe 6.3).
- **Ausgabe:** ein Stundenplan (`List(Of ScheduleEntry)`), aufbereitet
  als Klassen-/Lehrer-/Wahlprofil-Raster (`Formatting.vb`), plus
  Diagnosen (Validierungsfehler, Kann-Verstöße, Solver-Status).

### 3.2 Technischer Kontext

| Nachbarsystem | Richtung | Protokoll/Form |
|---|---|---|
| Ollama-Server (lokal) | TimetableCore → Ollama | HTTP `POST /api/chat`, `GET /api/tags` - JSON, `format`=striktes JSON-Schema pro Constraint-Typ, `think:false` |
| Google OR-Tools CP-SAT (eingebettete native Bibliothek) | TimetableCore → OrTools | In-Process-Aufruf über `Google.OrTools.Sat`-NuGet-Paket (kein Netzwerk) |
| (Nachgelagert) WPF-GUI | GUI → TimetableCore | Direkter .NET-Projektverweis (`ProjectReference`), keine Netzwerkgrenze |
| (Nachgelagert) WebView2 (eingebetteter Browser) | GUI → WebView2 | In-Process-Hosting der generierten Viewer-HTML (`NavigateToString`/virtuelles Host-Mapping aus dem entschlüsselten Projektbestand - keine Klartext-Datei, kein Netzzugriff; User-Data-Folder app-lokal, siehe `docs/gui-datenhaltung-konzept.md` 7.6) |
| Dateisystem | RobustnessRunner → Dateisystem | Ergebnis-Log (`docs/phase2-results-raw.log`) wird nach jedem Einzellauf sofort angehängt |

## 4. Lösungsstrategie

- **1:1-Portierung statt Neuentwurf** (Phase 0/1): Der bereits gegen echte
  Szenarien getestete Python-Prototyp wurde Modul für Modul nach VB.NET
  übersetzt (`timetable_model.py`→`Solver.vb`, `validation.py`→
  `Validation.vb`, `verifier.py`→`Verifier.vb`, `formatting.py`→
  `Formatting.vb`, `llm_extraction.py`→`LlmExtraction.vb`), inkl.
  wortgleicher Übernahme der deutschen LLM-Instruktionstexte und
  JSON-Schemas - diese wurden gegen echtes Modellverhalten kalibriert,
  nicht nur zur Lesbarkeit geschrieben, und dürfen beim Portieren nicht
  "aufgeräumt" werden.
- **Unabhängiger Verifier statt Selbstprüfung**: `Verifier.vb` teilt
  bewusst keinen Code mit `Solver.vb` (siehe 8.2) - ein Übersetzungsfehler
  im Modellbauer würde sonst unsichtbar bleiben.
- **Muss/Kann statt starrer Unlösbarkeit** (Phase 2.5): CP-SAT-Zielfunktion
  minimiert die Zahl verletzter weicher ("Kann"/`priority:"should"`)
  Regeln, statt bei einem einzigen zu engen weichen Constraint komplett
  `Infeasible` zu melden.
- **Wiederverwendung durch synthetische Szenario-Konstruktion statt
  Code-Verzweigung**: Neue Planungsstufen (Kursstufe: Kursblockung →
  Schienenraster → Raumzuordnung; kombinierte Schule) bauen jeweils ein
  eigenes, in sich konsistentes `entities`/`constraints`-JSON und
  schicken es durch die UNVERÄNDERTE `Solver.Solve()`-Pipeline, statt
  diese um Spezialfälle zu erweitern (siehe 8.6). `Solver.BuildModel`/
  `BuildCoreModel`/`ApplyConstraints`/`AddBlockConstraint` sind seit
  Phase 2.5 nur noch für neue *Constraint-Typen* verändert worden, nie für
  neue *Schulformen*.
- **Dekomponierte, schema-beschränkte LLM-Extraktion** statt eines
  großen Prompts: ein separater Ollama-Call pro Constraint-Typ mit
  striktem JSON-Schema, weil ein einzelner großer Call nachweislich
  unvollständige Ergebnisse lieferte und ein zweistufiges
  "Extrahieren-dann-Reparieren"-Verfahren sogar neue Halluzinationen
  einführte (siehe `LlmExtraction.vb`-Kopfkommentar).
- **Jede neue Fähigkeit erst deterministisch, dann optional
  LLM-fähig machen**: Muss/Kann (2.5) vor "LLM leitet Muss/Kann ab" (2.6);
  Kursstufen-Solver-Kern (2.11a-f) vor `kurswahl`-LLM-Typ (2.11g). Live
  gegen die tatsächlich installierte OrTools-DLL bzw. gegen laufendes
  Ollama verifizieren, nicht nur aus Dokumentation annehmen (siehe 9).

## 5. Bausteinsicht

### 5.1 Ebene 1 - Gesamtsystem

```
timetable-dotnet/
├── TimetableCore/            Klassenbibliothek (.NET 8) - der eigentliche Kern
├── TimetableCore.Tests/      MSTest-Suite + Fixtures (Testszenarien)
├── Klassenbildung.Tests/     eigene, schnelle MSTest-Suite NUR für die
│                             Klassenbildung (<1s statt ~6min - siehe 8.9)
├── TimetableYaml/            Klassenbibliothek: YAML-Persistenz (YamlDotNet)
│                             für Stammdaten, Constraints, Klassenbildung
│                             und config.yaml (RunConfig + LoadConfig)
├── TimetableWorkflow/        Klassenbibliothek: die Stundenplan-Pipeline
│                             als I/O-freier Dienst (StundenplanLauf),
│                             Berichts- und Viewer-Aufbereitung
│                             (StundenplanBericht, KlassenbildungBericht,
│                             Build*Html + Templates/*.html als Embedded
│                             Resources, siehe 8.10), Curriculum-Vorlagen
│                             und Scaffold für `new`
├── TimetableProjekt/         Klassenbibliothek: die verschlüsselte
│                             Ein-Datei-Projektablage (.splanx), Klarnamen-
│                             Mapping, Audit-Log, Ergebnis-Stände sowie
│                             Im-/Export in das tests/<schule>/-Layout
│                             (Konzept: docs/gui-datenhaltung-konzept.md)
├── TimetableGui/             WPF-Anwendung (net8.0-windows): Hauptfenster,
│                             Lauf-Monitor und die WebView2-gehosteten
│                             Dashboards (siehe 8.13)
├── RobustnessRunner/         Konsolenprogramm: LLM-Wiederholungsstudien
├── SchoolTestRunner/         Konsolenprogramm: code-freie YAML-Testfälle -
│                             seit dem GUI-Unterbau nur noch CLI-Hülle
│                             (Argument-Parsing + Datei-/Konsolenschicht;
│                             6.7 und docs/schooltestrunner-benutzerhandbuch.md)
├── TimetableViewer.Tests/    Playwright-Tests der Viewer-Vorlagen und der
│                             Bruecke - headless, ohne Desktop (8.14)
└── TimetableGui.Tests/       ViewModel- und Auslieferungstests, headless
                              (kein Fenster, kein Browser - siehe 8.13)
```

`TimetableCore` hat keine Abhängigkeit auf eines der übrigen Projekte.
`Klassenbildung.Tests` referenziert nur `TimetableCore` (RootNamespace
bewusst `KlassenbildungTests`, damit der Namespace das Modul
`Klassenbildung` nicht verschattet). `RobustnessRunner` bindet
einzelne Testfixtures per `<Compile Include>` ein, um MSTest als
Abhängigkeit zu vermeiden.

`TimetableYaml` und `TimetableWorkflow` sind **Bibliotheken**, und zwar
aus einem einzigen Grund: `SchoolTestRunner` ist ein `Exe`-Projekt mit
eigener `Main`, das eine GUI nicht sauber referenzieren kann. Alles, was
die Phase-3-GUI mit der CLI teilen muss - YAML lesen und schreiben,
Viewer-HTML bauen, Projekte aus einer Vorlage anlegen - liegt deshalb
seit dem GUI-Unterbau (Stufe A, `docs/gui-implementierungsplan.md`)
diesseits dieser Grenze. Die Abhängigkeitsrichtung ist
`SchoolTestRunner`/`TimetableGui` → `TimetableWorkflow` → `TimetableYaml`
→ `TimetableCore`, zyklenfrei.

**Die Pipeline gibt es genau einmal.** `StundenplanLauf.Ausfuehren`
(`TimetableWorkflow`) führt die Abfolge `StammdatenValidation` →
`Lehrereinsatzplanung` → `VerifyLehrereinsatz` →
`BuildEntitiesFragment`/`BuildAssignmentConstraints` → `ValidateEntities`
→ `SolveTop` je Zuteilung → `VerifySchedule` aus - ohne Dateizugriff und
ohne Konsolenausgabe, mit durchgereichtem Abbruch- und
Fortschrittskanal (8.11). `SchoolTestRunner/Run.vb` ist seither nur noch
die Datei- und Konsolenschicht darum herum und damit der erste Konsument
dieses Dienstes; die GUI wird der zweite. Der Grund für diesen Zuschnitt
ist nicht Ästhetik: die vier Stellen mit echten Entscheidungen
(Auflösung der Nullable-Config-Felder auf `SolveTop`s eigene Defaults,
Aufteilung von Zeitbudget und `max_solutions` auf mehrere Zuteilungen,
Auswahl des besten Laufs über `Quality.Total`, `DeepClone` je Zuteilung)
dürfen nicht in zwei Oberflächen doppelt existieren.

`YamlDotNet` liegt bewusst **nur** in `TimetableYaml`, nicht in
`TimetableCore` - damit dessen Abhängigkeitsoberfläche minimal bleibt
(siehe 8.7). Dort liegt seit Stufe B auch der Rückschreibweg: bis dahin
existierten Schreib-APIs nur für Stammdaten, `constraints.yaml`,
`klassenbildung.yaml` und `config.yaml` konnten ausschließlich gelesen
werden. Eine Einschränkung bleibt und ist von der Leseseite geerbt: ein
String, der wie eine Zahl aussieht, kommt beim nächsten Laden als Zahl
zurück (`ScalarStringToJsonValue` kann quotiert und unquotiert nicht
unterscheiden). Ebenfalls dokumentiert: die ausführlichen
Messprotokoll-Kommentare der Beispiel-`config.yaml` überleben einen
programmatischen Schreibvorgang nicht - YamlDotNet serialisiert Werte,
keine Kommentare. Dasselbe gilt für die Embedded-Resource-Vorlagen: ihr
logischer Ressourcenname trägt den Assemblynamen als Präfix
(`TimetableWorkflow.stundentafel.html`) und wird über
`GetExecutingAssembly()` aufgelöst - beim Verschieben zwischen Projekten
müssen `EmbeddedResource`-Item und `ResourceName`-Konstante deshalb immer
gemeinsam wandern, sonst schlägt der Zugriff erst zur Laufzeit fehl.

### 5.2 Ebene 2 - Whitebox `TimetableCore`

| Modul | Verantwortung | Abhängig von |
|---|---|---|
| **Models.vb** | `JsonHelpers` (Zugriff auf das rohe `System.Text.Json.Nodes`-JSON, Python-`dict.get`-artig statt starrem Typmodell - minimiert Übersetzungsrisiko beim Portieren), Muss/Kann-Konstanten, Kursstufe-Zugriffshelfer (`GetKurse`/`GetSchienen`). | - |
| **Validation.vb** | Deterministische Vorab-Prüfung: harte Cross-Reference-Fehler (unbekannte Entity-Referenz) vs. weiche Coverage-Warnungen (fehlende `no_overlap`-Regel). `ValidateKursstufeEntities` erweitert das um Kurs-/Schienen-/Wahlprofil-Konsistenz. | Models.vb |
| **Solver.vb** | CP-SAT-Modellbau (`BuildCoreModel`/`BuildModel`/`ApplyConstraints`/`AddBlockConstraint`) und -Lösung (`Solve`, `SolveTop`, `SolveKursstufe`). Größtes Modul - bewusst der einzige Ort, der `model.Add(...)` für die inzwischen 13 klassenbasierten Constraint-Typen aufruft (u.a. `parallel_group` seit Phase 2.20, `required_slot` seit 2.23, `occupied_window` seit Code-Review P1, `subject_period_window` für die Rhythmisierung - vollständige Liste in `json-constraints-reference.md`). Seit der Code-Review-Umsetzung P2 ist `lexicographic:=True` der `SolveTop`-Default: statt einer gewichteten Gesamtsumme werden Stufen nacheinander optimiert und jeweils als hartes Band (`<= opt + lexTolerance`) fixiert - Kann → (Dichte, opt-in `lexOccupiedDensityStage`) → (Fach-Fenster, opt-in `lexSubjectWindowStage`) → ClassGaps → (TeacherGaps, opt-in) - der gewichtete Rest bildet die letzte Stufe; `lexicographic:=False` liefert weiterhin den früheren Ein-Summen-Modus. P3 ergänzt `minDiversity` (Distanz-Cuts: jede weitere Lösung muss sich in mindestens n Belegungen unterscheiden, statt nur per No-Good "nicht identisch" zu sein), `laterIterationsGapLimit` erlaubt späteren Iterationen ein früheres Abbrechen bei akzeptierter Optimalitätslücke. Phase 2.22: `SolveTop`s Iterationsschleife übergibt einen `CpSolverSolutionCallback` an `Solve()`, der jede gefundene Verbesserung (Zeit + Objective) aufzeichnet; `ScoredSolution` trägt zusätzlich `ObjectiveValue`/`BestObjectiveBound` (die Optimalitäts-Lücke) mit. Phase 2.25: `SolveRunner.RunSolve` (seit dem Abbruchkanal in `SolveControl.vb`, siehe 8.11 - vorher `SolveWithStagnationCutoff`) löst jede Iteration auf einem separaten `Task`, pollt periodisch gegen die `ConvergenceCallback`-Historie und ruft `solver.StopSearch()` (dokumentiert cross-thread-sicher), sobald `stagnationTimeoutS` (Default 45s, standardmäßig aktiv) ohne neue Verbesserung verstrichen ist - die gesparte Zeit steht der nächsten `SolveTop`-Iteration zur Verfügung, statt eine stehende Suche bis zum Zeitlimit weiterlaufen zu lassen (`StagnationTriggeredCount` macht sichtbar, ob/wie oft das griff). `diversifySeed`/`randomizeSearch` streuen aufeinanderfolgende Iterationen zusätzlich, bleiben aber für denselben Basis-`seed` deterministisch. | Models.vb, Validation.vb, Verifier.vb (für `SolveTop`s Kann-Neuberechnung), ScheduleQuality.vb, SolveTopObjective.vb, Kursblockung.vb, Schienenraster.vb, Raumzuordnung.vb |
| **Verifier.vb** | Unabhängiger Solution-Checker - teilt bewusst KEINEN Code mit Solver.vb (siehe 8.2). `VerifySchedule`/`VerifyScheduleDetailed` (Muss/Kann getrennt), `VerifyKursblockung` (Stufe-A-Ergebnis unabhängig re-prüfen), `VerifyLehrereinsatz`. Phase 2.20 ergänzt einen unabhängig re-derivierten `parallel_group`-Check plus eine Gruppen-bewusste `VerifyLehrereinsatz`-Erweiterung (alle real umspannten Klassen einer Gruppe müssen vom selben Lehrer unterrichtet werden). | Models.vb |
| **Formatting.vb** | Rohes `ScheduleEntry`-Ergebnis → Klassen-/Lehrer-/Wahlprofil-Raster (`GridCell`), ASCII-Tabellen, JSON-Export. Reine Präsentationsschicht, keine GUI-Abhängigkeit. Seit Phase 2.20 kollisionsbewusst: mehrere gleichzeitige Sessions derselben Klasse (Parallelgruppe) werden in `ToClassGrids` zu einer kombinierten Zelle zusammengeführt statt einander zu überschreiben. Phase 2.21 ergänzt `ToStundentafelJson` (Klassenstufen-/Parallelklassen-gruppierter Multi-Lösungs-Export für den SchoolTestRunner) - erste Stelle, an der `Formatting.vb` `Verifier.vb` aufruft (pro Lösung ein unabhängiger Muss-Verstoß-Recheck). Phase 2.22: `ToStundentafelJson` exportiert pro Lösung zusätzlich `objective_value`/`best_objective_bound`/`gap_percent` (Optimalitäts-Lücke) und `convergence` (Zeit-vs-Objective-Verlauf). Viewer-Ausbau: der Export trägt den vollen Qualitätsvektor + `quality_weights` je Lösung (Grundlage für Sortierung, Gewichte-Regler und Pareto-Filter im Viewer, siehe 8.10); `ToStundentafelJsonMulti` (Mehr-Zuteilungs-Modus, siehe 6.8) fasst mehrere `AssignmentRun`s (je: Stammdaten-abgeleitetes Szenario + `MultiSolveResult` + Zuteilungs-Index) zu EINEM Export mit global nach Qualität sortierten Lösungen zusammen und exportiert `teacher_equivalence_classes` (nur Klassen mit ≥2 Mitgliedern) für die Tausch-Anzeige. | Models.vb, Verifier.vb |
| **LlmExtraction.vb** | Freitext → strukturierte Constraints via Ollama/Qwen. Ein Call pro Constraint-Typ mit eigenem JSON-Schema; `period_exception` wird deterministisch zu `forbidden_slot`-Einträgen expandiert (`ExpandPeriodException`); `DropContradictoryConsecutiveRequired` als deterministisches Sicherheitsnetz gegen unmögliche Block-Kombinationen. | Models.vb |
| **ScheduleQuality.vb** | Post-hoc-Bewertungsschema für Kandidaten-Stundenpläne, 9 Kriterien (Kann-Verstöße, Klassen-/Lehrer-Lücken, Randstunden, Nachmittags-Tage, Klassen-/Lehrer-Tagesausgewogenheit, Dichte-Defizit `OccupiedDensity` für should-`occupied_window`, Fenster-Verstöße `SubjectWindow` für should-`subject_period_window`) - unabhängig von der CP-SAT-Modellierung, dient als "Wahrheit" für `SolveTop`s Endsortierung. `QualityWeights` (Phase 2.24 über `config.yaml` konfigurierbar - Modell und Lader in `TimetableYaml/YamlConfig.vb`, siehe `tests/README.md`) gewichtet jedes Kriterium; seit Phase 2.25-Nachtrag-2 sind `ClassGaps`/`TeacherGaps`-Gewicht mit `Kann` vereinheitlicht (früher war `ClassGaps` bewusst 10x höher gewichtet - Live-Experimente zeigten, dass nicht das Gewicht, sondern `TeacherGaps`' CP-SAT-Kodierung die eigentliche Ursache schlecht beweisbarer Lösungsschranken war, siehe `docs/phase2-25-stagnation-heuristik.md` Nachtrag 2). Neues `IncludeTeacherGaps`-Flag (Default `true`) - ein Sicherheitsventil, das den Aufbau der `TeacherGaps`-Hilfskonstrukte im CP-SAT-Modell komplett unterdrücken kann. | Models.vb |
| **SolveControl.vb** | Querschnittlicher Abbruch- und Fortschrittskanal (siehe 8.11): die öffentlichen Typen `SolvePhase`/`SolveProgress` und der gemeinsame Ausführungspfad `SolveRunner.RunSolve`, über den seither JEDER Solve-Aufruf des Kerns läuft - Fast-Path ohne Token/Progress unverändert direkt und blockierend, sonst `Task` + 500ms-Polling mit `solver.StopSearch()`. `StageProgressAdapter` etikettiert die Meldungen verketteter Aufrufe (`SolveKursstufe`, `SolveCombinedSchool`) auf die Sicht des Gesamtlaufs um. Keine UI-Abhängigkeit: `CancellationToken` und `IProgress(Of T)` sind BCL-Typen. | Solver.vb (für `ConvergenceCallback`) |
| **SolveTopObjective.vb** | Baut dieselben Bewertungskriterien zusätzlich direkt ins CP-SAT-Modell (`Friend`, nur von `Solver.SolveTop` genutzt), damit die Suche selbst dorthin gelenkt wird, statt nur die gefundenen Kandidaten hinterher zu sortieren: Randstunden/Nachmittags-Tage/Tagesausgewogenheit über eine CP-SAT-freundliche Näherung (Spannweite statt echter Varianz, teils weiterhin über den Sentinel-Min/Max-Trick, siehe 9). `ClassGaps`/`TeacherGaps` nutzen seit Phase 2.25-Nachtrag-2 `BuildGapFlags` - eine Big-M-freie Kodierung (Präfix/Suffix-OR-Ketten `anyBefore`/`anyAfter` + lineare Reifikation jeder einzelnen Lücken-PERIODE als eigene `BoolVar`), die die vorherige `AddMinEquality`/`AddMaxEquality`-Sentinel-Konstruktion ersetzt, siehe 9. `BuildGapFlags` wird für Lehrkräfte nur aufgerufen, wenn `QualityWeights.IncludeTeacherGaps = True` ist (strukturelles Abschalten, nicht nur Gewicht 0). `BuildQualityTerms` liefert die Stufen-Terme (`KannSum`/`OccupiedDensitySum`/`SubjectWindowSum`/`ClassGapsSum`/`TeacherGapsSum`) einzeln an `SolveTop`s lexikografischen Modus; `OccupiedDensitySum`/`SubjectWindowSum` sind reine Linearsummen über ohnehin existierende Variablen (keine zusätzlichen Verletzungs-BoolVars - der Kern von P1). | Models.vb |
| **Kursblockung.vb** | Kursstufe Stufe A: Kurs→Schiene-Zuordnung (CP-SAT-Teilmodell, eigenständig, kein Tag/Periode-Bezug). | Models.vb |
| **Schienenraster.vb** | Kursstufe Stufe B: Schiene→Tag/Periode. Konstruiert ein synthetisches Szenario und ruft `Solver.Solve()` unverändert auf. | Models.vb |
| **Raumzuordnung.vb** | Kursstufe Stufe C: Kurs→Raum, mit aus Stufe B gepinntem Tag/Periode. Ebenfalls über ein synthetisches Szenario + `Solver.Solve()`. | Models.vb, Schienenraster.vb (für `SlotsForKurs`) |
| **CombinedSchool.vb** | Orchestriert einen gemeinsamen Solve von Sek-I- und Kursstufen-Hälfte, sodass ein geteilter Lehrer-/Raum-Name nicht doppelt belegt wird - ohne `Solver.vb` selbst zu ändern. | Models.vb, Solver.vb, Kursblockung.vb, Schienenraster.vb, Raumzuordnung.vb |
| **Stammdaten.vb** | Typisiertes Domänenmodell (Klassenstufe/Fach/Klasse/Raum/Lehrer/FachLehrerZuordnung) für dauerhaft verwaltete Schul-Stammdaten (Phase 2.15) - bewusst NICHT das rohe `JsonObject`-Muster der Constraints (siehe 8.7). Laden/Speichern als JSON, `BuildEntitiesFragment` projiziert in das bestehende `entities`-Format. Phase 2.19 ergänzt ein Mitgliedschaftsdatenmodell (`Schueler`: pseudonyme ID + Heimatklasse; `Gruppe`: benannte, klassenunabhängige Schülergruppe für Fachgruppen/Förderung/Aufsicht). Phase 2.20 macht eine Gruppe für den Fall "klassenübergreifende Fachgruppe" solver-wirksam (`Gruppe.FachName`/`Klassenstufe`/`Parallelverbund`, neuer Helper `KlassenOfGruppe`) - siehe `docs/phase2-20-parallelgruppen.md`. | - |
| **StammdatenValidation.vb** | Cross-Reference-Prüfung für Stammdaten (unbekannte Klassenstufen-/Lehrer-/Fach-Referenzen, unplausible Deputate, unbekannte Schüler-/Gruppen-Referenzen, doppelte Schüler-IDs), gleiche "Fail-Fast VOR jedem Solve"-Philosophie wie `Validation.vb`. Phase 2.20 ergänzt eine harte Parallelverbund-Konsistenzprüfung (gleiche Klassenstufe/Wochenstunden/Blocklänge über alle Gruppen eines Verbunds - sonst wäre das CP-SAT-Modell strukturell unlösbar). | Stammdaten.vb |
| **Lehrereinsatzplanung.vb** | Neue, vorgeschaltete Planungsstufe (Phase 2.15): verteilt Lehrkräfte IDEAL auf Klassen/Fächer (Qualifikation hart, Deputat-Korridor/Klassenlehrer/Präferenzen weich) - ein eigenständiges CP-SAT-Teilmodell ohne Tag/Periode-Bezug. `BuildAssignmentConstraints` übersetzt das Ergebnis in `teacher_subject_assignment`/`weekly_hours`/`no_overlap`-Constraints, die unverändert an `Solver.Solve` gehen (`no_overlap` ist zwingend - siehe `docs/phase2-15-lehrereinsatzplanung.md`s Phase-2.16-Nachtrag zu einem live gefundenen Bug ohne diese Regel). Phase 2.17 ergänzt sieben weitere weiche/harte Ziele (Kontinuität über Jahre, Fachfremd-Vermeidung, Max. Klassen/Fächer pro Lehrer, Teilzeit-Tage-Kohärenz als harter Vorfilter, Klassenlehrer-Tandem-Balance, Springerreserve, faire Verteilung unbeliebter Fächer) - siehe `docs/phase2-15-lehrereinsatzplanung.md`s Nachtrag 5. Phase 2.20 ergänzt einen Gruppen-Zweig (`AssignKey.IstGruppe`): ein klassenübergreifendes Fach bekommt EINE Zuweisung pro Gruppe statt einer pro echter Klasse (Deputat wird dadurch korrekt einmal gezählt), bei der Lösungsextraktion sofort auf alle real umspannten Klassen expandiert; `BuildAssignmentConstraints` emittiert zusätzlich eine `parallel_group`-Regel pro Parallelverbund. Mehr-Zuteilungs-Ausbau (siehe 6.8): `TeacherEquivalenceClasses` berechnet Äquivalenzklassen austauschbarer Lehrkräfte über eine Voll-Pipeline-Signatur (Profil + Qualifikationen + feste Zuordnungen + Hand-Constraints, Eigenreferenzen auf `<SELF>` normalisiert), `AddSymmetryBreaking` bricht Orbits per lexikografischer Kette über benachbarte Klassenmitglieder, `SolveLehrereinsatzTop` liefert mehrere echt NICHT-symmetrische Zuteilungen (Qualitätsband + namensbasierte Distanz-Cuts). | Stammdaten.vb |
| **Klassenbildung.vb** | Stufe 0 (siehe 6.9, `docs/klassenbildung-plan.md` K1-K3): Datenmodell (`KlassenbildungInput`: Schüler mit Attributen, Klassen-Korridor, Gruppen [Bündelung/Verteilung], Balance-Regeln, Wünsche, Fixierungen), `ValidateKlassenbildung` (Fail-Fast), Kern-Solver `SolveKlassenbildung` (x[s,c]-BoolVars, ExactlyOne via Sum=1, Größenkorridor hart, 4 Regeltypen hart/weich mit Prio-Gewichten 1000/50/1) und Varianten-Schleife `SolveKlassenbildungTop` (Qualitätsband ε, Mindest-Diff-Distanz-Cuts, Konsens-Kern über alle Varianten; Klassen-Symmetriebrechung per Präzedenzkette, Fixierung-referenzierte Klassen ausgenommen). Bewusst NULL Kopplung an Solver/SolveTop - eigenständiges CP-SAT-Teilmodell, nur repo-verifizierte Primitive. | - |
| **KlassenbildungQuality.vb** | Reiner Bewertungslauf `Bewerte` (Verifier-Prinzip): zählt alle Regeln gegen eine gegebene Zuordnung unabhängig nach - es teilt mit `Klassenbildung.vb` **keine Zeile Bewertungslogik**. Die einzige Berührung ist `KlassenLabels`, das die Anzeigenamen (1a, 1b, …) liefert: reine Benennung, die kein Kriterium auszählt und die Unabhängigkeit der Nachprüfung deshalb nicht berührt - sie dort zu duplizieren hätte genau die Textabweichung riskiert, die der Zeichengleichheits-Test (8.10) verhindern soll (Verletzungsmaß je weicher Regel) und erzeugt je (Kind, betroffene Regel) einen Ampel-Chip grün/gelb/rot (gelb = erfüllt, aber knapp: Kappe exakt voll bzw. Balance am Toleranzrand). `KlassenRun` prüft nach jedem Lauf, dass Bewertung und Solver-Verletzungen exakt übereinstimmen (FAIL bei Abweichung). | - |

**Abhängigkeitsrichtung** (keine Zyklen): `Models.vb` ist die einzige von
praktisch allem genutzte Basis; `Solver.vb` ist der einzige "große"
Konsument, der `ScheduleQuality`/`SolveTopObjective`/`Kursblockung`/
`Schienenraster`/`Raumzuordnung`/`Verifier` zusammenführt.
`CombinedSchool.vb` liegt eine Ebene darüber und ruft nur öffentliche
Funktionen von `Solver.vb` und den drei Kursstufe-Modulen auf.

### 5.3 Datenmodell (Kernklassen)

- `Session` (ClassName, Subject, Teacher) - eine planbare Unterrichtseinheit,
  abgeleitet aus `teacher_subject_assignment`-Constraints.
- `LessonKey`/`RoomKey` - zusammengesetzte Schlüssel (Klasse, Fach,
  Lehrer, Tag, Periode[, Raum]) für die CP-SAT-`BoolVar`-Dictionaries
  (VB.NET hat keine native Tupel-Hashing-Dict-Schlüssel wie Python).
- `BuiltModel` - Ergebnis von `BuildCoreModel`: `CpModel` + alle
  `Lesson`/`Room`-Variablen + `KannVars` (eine `BoolVar` pro
  Kann-Constraint).
- `ScheduleEntry` - eine Zeile des Endergebnisses (Klasse, Fach, Lehrer,
  Tag, Periode, Raum).
- `SolveResult`/`MultiSolveResult`/`ScoredSolution` - Solve()- bzw.
  SolveTop()-Rückgabetypen.
- `KursstufeSolveResult`/`CombinedSolveResult` - Pro-Stufe-Diagnostik für
  die mehrstufigen Pipelines.
- `LehrereinsatzResult` + `AssignmentRun` - Ergebnis der Stufe 1 bzw.
  ein kompletter Stufe-2-Lauf auf Basis EINER Zuteilung (für
  `ToStundentafelJsonMulti`, siehe 6.8).
- `KlassenbildungInput`/`KlassenbildungResult`/`KlassenbildungTopResult`
  (Varianten + Konsens-Kern) und `KlassenbildungBewertung`
  (Verletzungen + Ampel-Chips) - das eigenständige Datenmodell der
  Stufe 0 (siehe 6.9), bewusst typisiert statt `JsonObject`-basiert
  (gleiche Begründung wie `Stammdaten.vb`, siehe 8.7).

## 6. Laufzeitsicht

### 6.1 Szenario: einfacher klassenbasierter Solve

1. Aufrufer übergibt `data As JsonObject` (`entities`+`constraints`) an
   `Solver.Solve(data, timeLimitS, seed, numWorkers)`.
2. `BuildModel` → `BuildCoreModel`: `Validation.ValidateEntities(data)`
   läuft zuerst (Pflicht - eine nicht validierte Referenz würde sonst
   vom Modellbauer stillschweigend ignoriert, siehe 8.1). Danach werden
   `Session`s aus `teacher_subject_assignment` abgeleitet, `Lesson`-
   BoolVars für jede (Klasse,Fach,Lehrer,Tag,Periode)-Kombination erzeugt,
   `Room`-BoolVars für `room_requirement`-Fälle, und `ApplyConstraints`
   übersetzt jeden der übrigen Constraint-Typen in `model.Add(...)`-Aufrufe
   (vollständige Typliste in `json-constraints-reference.md`).
3. Existieren Kann-Constraints, setzt `BuildModel` `model.Minimize(Sum(KannVars))`.
4. Ein `CpSolver` mit `max_time_in_seconds`/`random_seed`/`num_search_workers`
   löst das Modell.
5. `ExtractSchedule`/`ExtractKannFlags` lesen das Ergebnis aus den
   `BoolVar`-Werten in `ScheduleEntry`/`KannConstraintFlag`-Listen um.
6. Aufrufer prüft typischerweise zusätzlich unabhängig via
   `Verifier.VerifySchedule(data, result.Schedule)` (0 Einträge = sauber).

### 6.2 Szenario: `SolveTop` (mehrere bewertete Kandidaten, gestufter Warmstart)

1. `BuildCoreModel` einmalig (kein `Minimize` gesetzt).
2. **Stufe 1** (falls `useStagedHints:=True`, Default): kurzzeitig die
   günstige Kann-only-Zielfunktion setzen, lösen, das Ergebnis per
   `AddHint` auf die `Lesson`-Variablen als Startpunkt für Stufe 2
   übergeben. Behebt eine reale Kaltstart-Schwäche der vollen
   6-Kriterien-Zielfunktion bei großen Szenarien (siehe
   `docs/phase2-12-staged-hints.md`).
3. **Stufe 2**: Im Default-Modus `lexicographic:=True` (Code-Review P2)
   werden die Qualitäts-Stufen nacheinander optimiert und jeweils als
   hartes Band `<= opt + lexTolerance` ins Modell fixiert (Kann →
   optionale Dichte-/Fach-Fenster-Stufen → ClassGaps → optional
   TeacherGaps, siehe 5.2); die gewichtete Rest-Zielfunktion bildet die
   letzte Stufe. Mit `lexicographic:=False` setzt
   `SolveTopObjective.ApplyQualityObjective` stattdessen die frühere
   volle gewichtete Ein-Summen-Zielfunktion. Danach löst eine Schleife
   wiederholt dasselbe `CpModel` (jeweils neuer `CpSolver`, über
   `SolveWithStagnationCutoff` - siehe Phase 2.25 in 5.2 - falls
   `stagnationTimeoutS` nicht `Nothing` ist), sperrt jede gefundene
   Lösung per `BlockSolution` (No-Good-`AddBoolOr`) gegen Wiederholung -
   mit `minDiversity > 0` als Distanz-Cut (mindestens n abweichende
   Belegungen, Code-Review P3) -, reicht die gefundene Belegung optional
   als Hint an die nächste Iteration weiter, und bricht ab bei
   `maxSolutions`, `totalTimeLimitS` oder `Infeasible` (Suchraum erschöpft).
4. Jede Kandidatenlösung wird unabhängig via `Verifier.VerifyScheduleDetailed`
   gezählt und `ScheduleQuality.Score` bewertet; `Solutions` wird am Ende
   nach `Quality.Total` aufsteigend sortiert - die Reihenfolge, in der
   CP-SAT Lösungen fand, bestimmt NICHT die Endreihenfolge.

### 6.3 Szenario: Freitext → Constraints (LLM-Extraktion)

1. `LlmExtraction.IsOllamaAvailable(...)` prüft Erreichbarkeit und ob das
   Modell gepullt ist (schnelles Fail-Fast).
2. `ExtractAllConstraints(entities, promptText, types)` ruft für jeden
   Constraint-Typ separat `ExtractConstraintType` auf: ein
   `POST /api/chat` mit typspezifischem System-Prompt, dem vollständigen
   `entities`-JSON plus dem Freitext als User-Content, `format:`=striktes
   JSON-Schema, `think:false`.
3. `period_exception`-Treffer werden deterministisch (kein LLM) zu
   `forbidden_slot`-Einträgen expandiert (`ExpandPeriodException` -
   reine Tage-Mengendifferenz statt den Modell eine
   "alle Tage AUSSER Y"-Aufzählung abzuverlangen, die in Tests zweimal
   auf unterschiedliche Art falsch war).
4. `DropContradictoryConsecutiveRequired` entfernt Block-Vorgaben, die
   rechnerisch unmöglich mit den zugehörigen `weekly_hours` sind.
5. Ergebnis: ein flaches `List(Of JsonObject)`, das direkt in ein
   `constraints`-Array eingesetzt und wie jedes andere Szenario an
   `Solver.Solve`/`SolveTop` übergeben werden kann - die LLM-Herkunft ist
   dem Solver danach nicht mehr anzusehen.

### 6.4 Szenario: Kursstufe end-to-end (`Solver.SolveKursstufe`)

```
entities.kurse/schienen + "kurswahl"-Constraints
        │
        ▼
Kursblockung.SolveKursblockung   (Stufe A: Kurs → Schiene, eigenes CP-SAT-Modell)
        │  Dictionary(KursId → SchieneId)
        ▼
Schienenraster.BuildSchienenrasterScenario → Solver.Solve   (Stufe B: Schiene → Tag/Periode,
        │                                                    synthetisches Szenario, UNVERÄNDERTE Solve()-Pipeline)
        │  gelöstes Schienenraster
        ▼
Raumzuordnung.BuildRaumzuordnungScenario → Solver.Solve     (Stufe C: Kurs → Raum, Tag/Periode
        │                                                    bereits aus Stufe B gepinnt)
        ▼
KursstufeSolveResult (Pro-Stufe-Status + finaler Schedule)
```

Jede Stufe stoppt die Pipeline bei einem nicht-lösbaren Zwischenergebnis
und meldet, WELCHE Stufe fehlschlug - statt nur "hat nicht funktioniert".

### 6.5 Szenario: kombinierte Schule (`CombinedSchool.SolveCombinedSchool`)

Zwei Reihenfolgen, strukturell unterschiedlich (nicht nur "wer zuerst
dran ist"):

- **SekIFirst (Default):** Sek I wird zuerst gelöst; die 3-Kursstufe-Stufen
  werden hier manuell nachgebaut (nicht `SolveKursstufe` aufgerufen), weil
  `Schienenraster`/`Raumzuordnung` neue optionale Parameter
  (`externalTeacherBusySlots`/`externalRoomBusySlots`) brauchen, um
  Sek-I-Belegzeiten geteilter Lehrer/Räume als zusätzliche
  `teacher_availability`/`forbidden_slot`-Regeln einzuspeisen.
- **KursstufeFirst:** `Solver.SolveKursstufe` läuft unverändert zuerst;
  danach werden Sek-Is Belegzeiten geteilter Namen in eine
  `DeepClone()`-Kopie des Sek-I-JSON injiziert, bevor `Solver.Solve`
  darauf läuft.

Bekanntes, dokumentiertes Restrisiko der SekIFirst-Richtung: Stufe C
(Raumzuordnung) wählt Räume erst NACH bereits aus Stufe B gepinntem
Tag/Periode - ist der einzige erlaubte Raum zu genau diesem Slot durch
Sek I belegt, wird die Kursstufe an dieser Stelle ohne Ausweichmöglichkeit
`Infeasible` (siehe `docs/phase2-13-combined-school.md`).

### 6.6 Szenario: Lehrereinsatzplanung (Stammdaten → Lehrer-Klasse-Zuordnung)

1. `StammdatenValidation.ValidateStammdaten(bestand)` läuft zuerst (Pflicht,
   gleiche Fail-Fast-Disziplin wie überall sonst im Projekt) - prüft
   Cross-Referenzen und zwei strukturelle Lücken (Klassenstufe ohne Fach,
   Fach ohne qualifizierte Lehrkraft), die `SolveLehrereinsatz` sonst erst
   als schwer diagnostizierbares Infeasible entdecken würde.
2. `Lehrereinsatzplanung.SolveLehrereinsatz(bestand)` baut pro
   kompatiblem (Lehrer,Klasse,Fach)-Tripel eine `BoolVar`, erzwingt hart
   genau eine Lehrkraft pro (Klasse,Pflichtfach) und minimiert eine
   gewichtete Summe aus drei weichen Zielen (Deputat-Korridor >
   Klassenlehrer-Fehlen > Klassenstufen-Präferenz - siehe 8.3-Analogon in
   `Lehrereinsatzplanung.vb`).
3. `Verifier.VerifyLehrereinsatz(bestand, result)` prüft das Ergebnis
   unabhängig aus den rohen Stammdaten nach (kein Aufruf in die
   CP-SAT-Modellierung hinein).
4. `Lehrereinsatzplanung.BuildAssignmentConstraints(result, bestand)` +
   `Stammdaten.BuildEntitiesFragment(bestand)` ergeben ein vollständiges
   `entities`/`constraints`-JSON im bestehenden Format - `Solver.Solve`/
   `SolveTop` laufen darauf UNVERÄNDERT weiter (Tag/Periode/Raum wie
   bisher). Kein einziges bestehendes Solver-Modul wurde für diese Stufe
   verändert - siehe 8.6.

### 6.7 Szenario: code-freier Testfall über `SchoolTestRunner` (Phase 2.18)

`SchoolTestRunner` ist ein eigenständiges Konsolenprogramm (kein Teil von
`TimetableCore`), das die gesamte Pipeline aus 6.6 END-TO-END über reine
YAML-Dateien statt VB.NET-Code steuert - eine Schule wird als
`tests/<schule>/input/{stammdaten,constraints,config}.yaml` beschrieben,
direkt im GitHub-Web-Editor bearbeitbar. Für die ausführliche
Endnutzer-Anleitung siehe `docs/schooltestrunner-benutzerhandbuch.md`; für
die vollständige YAML-Feldreferenz `tests/README.md`.

```
tests/<schule>/input/stammdaten.yaml + constraints.yaml + config.yaml
        │
        ▼
YamlStammdaten.LoadStammdatenYaml / YamlConstraints.LoadConstraintsYaml
        │  (YamlDotNet, UnderscoredNamingConvention, direkt auf die
        │   bestehenden Stammdaten.vb-POCOs - keine eigenen Modellklassen)
        ▼
StammdatenValidation.ValidateStammdaten → Lehrereinsatzplanung.SolveLehrereinsatz
        │  (identisch zu 6.6, Schritte 1-2)
        ▼
BuildAssignmentConstraints + geladene constraints.yaml-Einträge kombiniert
        │  (identisch zu 6.6, Schritt 4 - constraints.yaml ergänzt NUR
        │   handverfasste 2.-Stufe-Regeln, die Stufe 1 nicht ableiten kann,
        │   siehe tests/README.md "Architektur-Hintergrund")
        ▼
Validation.ValidateEntities → Solver.SolveTop (config.yaml steuert
        │  solve_time_limit_s/num_workers/max_solutions/quality_weights/...)
        ▼
Formatting: FormatLehrereinsatzMarkdown + FormatSchedule + ToStundentafelJson
        │  + StundentafelHtml.BuildStundentafelHtml
        ▼
output/{lehrerzuteilung,stundenplan}.md + stundenplan.json + stundentafel.html
```

Nach JEDER Stufe wird geschrieben, was bereits feststeht (kein
Alles-oder-Nichts bei einem Abbruch in einer späteren Stufe). Der Runner
gibt pro Schule eine `PASS`/`FAIL`-Zeile aus und liefert Exitcode 0 nur,
wenn ALLE Stufen sauber durchlaufen - macht eine spätere CI-Anbindung
trivial nachrüstbar, ohne dass diese Phase selbst einen Workflow anlegt.
Zwei tatsächlich ausgeführte Referenzbeispiele sind committet:
`tests/bw-grundschule-beispiel/` (BW-Grundschule, per CLI-Scaffold
erzeugt) und `tests/bw-gms-beispiel/` (BW-Gemeinschaftsschule, realitätsnah
von Hand nach der Kontingentstundentafel nachgebildet) - siehe
`docs/schooltestrunner-benutzerhandbuch.md` für eine kurze Beschreibung
beider.

Der Runner kennt vier Subkommandos: `new` (Scaffold einer neuen
Schule), `run` (volle Pipeline wie oben), `render` (baut NUR die
HTML-Viewer aus bereits vorhandenen JSON-Outputs neu - kein
Solver-Lauf; die tragende Validierung für reine Viewer-Änderungen) und
`klassen` (die Klassenbildungs-Stufe 0, siehe 6.9).

### 6.8 Szenario: Mehr-Zuteilungs-Modus (Äquivalenzklassen + Symmetriebrechung)

Antwort auf die Frage "könnte man mit mehreren NICHT-symmetrischen
Lehrerzuteilungen in die zweite Stufe gehen?": ja - aber naiv
enumerierte Alternativen wären überwiegend Symmetrie-Duplikate
(zwei qualifikatorisch identische Lehrkräfte tauschen die Klassen).

1. `Lehrereinsatzplanung.TeacherEquivalenceClasses(bestand,
   handConstraints)` gruppiert Lehrkräfte mit identischer
   Voll-Pipeline-Signatur (Profil, Qualifikationen, feste Zuordnungen,
   normalisierte Hand-Constraints - Eigenreferenzen werden zu `<SELF>`,
   damit inhaltsgleiche Regeln die Äquivalenz erhalten).
2. `SolveLehrereinsatzTop` löst Stufe 1 mehrfach:
   `AddSymmetryBreaking` erzwingt pro Äquivalenzklasse eine
   lexikografische Ordnung der Zuteilungsvektoren benachbarter
   Mitglieder (genau EIN Repräsentant pro Orbit), ein Qualitätsband
   (`assignmentTolerance`) und namensbasierte Distanz-Cuts
   (`assignmentMinDiversity`) liefern echte Alternativen.
3. `SchoolTestRunner/Run.vb` fährt Stufe 2 (`SolveTop`) pro Zuteilung
   mit aufgeteiltem Budget (Hand-Constraints je Lauf per `DeepClone`,
   da `JsonNode` nur einen Parent erlaubt) und übergibt alle Läufe als
   `AssignmentRun`-Liste an `Formatting.ToStundentafelJsonMulti` -
   die Lösungen ALLER Zuteilungen werden global nach Qualität sortiert.
4. Der Viewer zeigt pro Lösung die Zuteilung ("Zuteilung"-Spalte) und
   markiert über die exportierten `teacher_equivalence_classes`,
   welche Lehrkräfte per Äquivalenz direkt tauschbar wären (⇄).

Bewusste Entscheidung dabei (live erkannt): der Diversitäts-Cut
arbeitet auf NAMENS-Ebene, nicht auf der Äquivalenzklassen-Projektion -
Letztere wäre zu grob und hätte echte Alternativen (z.B. "A übernimmt
BEIDE Klassen" vs. Aufteilung) fälschlich als Duplikat verboten;
Orbit-Eindeutigkeit leistet allein die Lex-Symmetriebrechung. Der
Nutzen ist belegt: im Grundschul-Beispiel lieferte erst eine
nicht-symmetrische Alternativ-Zuteilung den global besten Plan.

### 6.9 Szenario: Klassenbildung (`klassen <schule>`, Stufe 0)

```
tests/<schule>/input/klassenbildung.yaml (+ config.yaml klassenbildung:-Block)
        │
        ▼
YamlKlassenbildung.LoadKlassenbildungYaml → Klassenbildung.ValidateKlassenbildung
        │  (Fail-Fast wie überall: unbekannte Referenzen, unmögliche
        │   Korridore, Fixierungs-Widersprüche blockieren den Solve)
        ▼
Klassenbildung.SolveKlassenbildungTop   (n Varianten im Qualitätsband ε,
        │                                Mindest-Distanz, Konsens-Kern)
        ▼
KlassenbildungQuality.Bewerte je Variante   (unabhängige Nachzählung -
        │                                    Abweichung vom Solver = FAIL)
        ▼
output/klassenbildung.{md,json,html}   (Report, Maschinenformat,
                                        interaktives Arbeitsbrett)
```

Die Stufe ist als **menschen-moderierter Entscheidungsprozess**
entworfen (Konzept-Abschnitt 10: der Solver liefert Vorschläge, die
Schulleitung entscheidet): der Viewer (`Templates/klassenbildung.html`,
siehe 8.10) unterstützt den Trichter Basis-Variante wählen →
Konsens-Kern/Unkritische bulk-fixieren → Gruppen/Einzelfälle pinnen →
Karten per Drag & Drop verschieben (Live-Bewertung im Browser,
JS-Duplikat von `Bewerte`) → `fixierungen:`-YAML-Block exportieren →
`klassen`-Lauf erneut rechnen. Ein Re-Solve direkt aus dem Viewer
(U5) ist bewusst noch nicht umgesetzt - der Viewer bewertet nur, er
optimiert nicht. Hinweis zur Reproduzierbarkeit: durch das
Wandzeit-Limit je Lauf sind Varianten und Konsens-Kern-Größe trotz
festem Seed nicht run-zu-run-stabil (die Zielwerte der akzeptierten
Varianten liegen aber stets im ε-Band).

## 7. Verteilungssicht

| Umgebung | Inhalt | Status |
|---|---|---|
| Linux-Sandbox (aktuelle Entwicklungsumgebung) | `TimetableCore`, `TimetableYaml`, `TimetableWorkflow`, `TimetableProjekt`, die fünf Testprojekte (`TimetableCore.Tests`, `Klassenbildung.Tests`, `TimetableYaml.Tests`, `TimetableWorkflow.Tests`, `TimetableProjekt.Tests`), `RobustnessRunner`, `SchoolTestRunner`, lokaler Ollama-Server | Aktiv, vollständig lauffähig (`dotnet test`, `dotnet run --project RobustnessRunner`, `dotnet run --project SchoolTestRunner`) - `SchoolTestRunner` braucht KEINEN Ollama-Server (rein YAML-basiert, keine LLM-Extraktion) |
| Windows-Zielumgebung (nachgelagert) | `TimetableGui` (WPF + WebView2) + `TimetableCore` (per `ProjectReference`) + lokaler Ollama-Server; Nutzdaten als verschlüsselte Ein-Datei-Projektablage (`.splanx`-Arbeitstitel, `docs/gui-datenhaltung-konzept.md`); WebView2 nutzt die auf aktuellen Windows-10/11-Systemen vorhandene Evergreen-Runtime (sonst Bootstrapper) | Oberfläche noch nicht begonnen (Phase 3); der Unterbau steht: Projektablage in `TimetableProjekt` umgesetzt (8.12), Pipeline als Dienst in `TimetableWorkflow` |

`Google.OrTools` läuft nativ unter beiden Plattformen (`Google.OrTools.runtime.linux-x64`
bzw. `...win-x64` als transitive NuGet-Abhängigkeit) - derselbe
`TimetableCore.vbproj`-Build funktioniert unverändert auf beiden. Ollama
läuft in beiden Umgebungen als separater, lokal zu installierender
Prozess (`http://127.0.0.1:11434`), keine Cloud-Abhängigkeit.

Die generierten Self-contained-HTML-Viewer (siehe 8.10) brauchen
keinerlei Laufzeitumgebung außer einem Browser. Bereitgestellt werden
sie als **Claude-Artifacts** - stabil verlinkte private Vorschau-Seiten
für Zwischenstände, ohne Merge nach `main`; URLs und
Aktualisierungs-Prozedur in `timetable-dotnet/CLAUDE.md`.

Die früheren GitHub-Pages-Kopien unter `stundentafel/*.html` werden
**nicht mehr gepflegt** (Nutzerentscheidung 23.08.2026). Sie liegen als
eingefrorener Altstand im Repository; sie zu entfernen oder die Seite
abzuschalten wäre ein eigener, nach außen wirkender Schritt.

## 8. Querschnittliche Konzepte

### 8.1 Validierung vor dem Solve (Fail-Fast bei unbekannten Referenzen)

`Validation.ValidateEntities` läuft in `BuildCoreModel` VOR jedem
Modellbau. Grund: eine Constraint-Referenz auf eine nicht existierende
Klasse/Lehrkraft/Fach/Raum wird vom Modellbauer sonst stillschweigend
ignoriert (keine Session/Variable, an die sie andocken könnte) - ein
konkreter, im Python-Original tatsächlich aufgetretener Vorfall: ein
LLM-generierter `consecutive_required`-Eintrag hatte `"class": "Chemie"`
(ein Fachname, keine Klasse), und der Solver produzierte klaglos einen
Plan, in dem das Fach komplett fehlte. Solche Fehler müssen das Lösen
blockieren, nicht erst hinterher als "0 Stunden Chemie" auffallen.

### 8.2 Unabhängiger Verifier ("Solver schlägt vor, Verifier prüft nach")

`Verifier.vb` teilt bewusst KEINEN Code mit `Solver.vb` - jede
Prüf-Logik wird direkt aus dem rohen JSON und der Ergebnis-Zeilenliste
neu abgeleitet, nie aus den internen `BoolVar`-Werten des Solvers
gelesen. Würde der Checker dieselbe Kodierlogik wie der Modellbauer
wiederverwenden, bliebe ein Übersetzungsfehler in dieser gemeinsamen
Logik unsichtbar - der Checker würde einfach zustimmen, was das
fehlerhafte Modell produziert hat. Dasselbe Prinzip wiederholt sich bei
`ScheduleQuality.vb` (unabhängig von `SolveTopObjective.vb`s
In-Modell-Näherung), bei `Verifier.VerifyKursblockung` (unabhängig von
`Kursblockung.vb`s CP-SAT-Constraints) und bei
`KlassenbildungQuality.Bewerte` (unabhängig vom CP-SAT-Modell in
`Klassenbildung.vb`; `KlassenRun` bricht mit FAIL ab, wenn Nachzählung
und Solver-Verletzungen auseinanderlaufen). Die Viewer führen das
Prinzip in umgekehrter Rolle fort: ihr Inline-JS enthält BEWUSSTE,
kommentierte Formel-Duplikate (Gewichte-Regler der Stundentafel,
U4-Live-Bewertung der Klassenbildung) - dort ist der VB-Kern die
unabhängige Ground Truth, gegen die die JS-Kopie per
Chromium-Interaktionstest zeichengleich verifiziert wird (siehe 8.10).

### 8.3 Muss/Kann-Priorität (`priority: "must"|"should"`)

Sieben der 13 klassenbasierten Constraint-Typen (`teacher_availability`,
`forbidden_slot`, `required_slot`, `occupied_slot`, `room_requirement`,
`consecutive_required`, sowie der `max_per_day`-Teil von `weekly_hours`)
können als weich (`"should"`) markiert werden. Ein weicher Constraint
bekommt eine gemeinsame Verletzungs-`BoolVar`; die eigentliche
Anforderung wird über `.OnlyEnforceIf(violated.Not())` daran gekoppelt,
die Zielfunktion minimiert die Summe aller Verletzungs-Variablen (binär
gewichtet - eine verletzte Regel zählt 1, unabhängig davon wie viele
Slots sie betrifft). Zwei Fenster-Typen weichen bewusst davon ab:
should-`occupied_window` und should-`subject_period_window` erzeugen
KEINE Verletzungs-BoolVars, sondern zählen jeden unbelegten bzw. außen
platzierten Slot in einem eigenen Qualitätskriterium
(`OccupiedDensity`/`SubjectWindow`) - reine Linearsummen über ohnehin
existierende Variablen, der Kern der Code-Review-Umsetzung P1.
Strukturell zwingende Typen (`no_overlap`, `shared_resource_conflict`,
`teacher_subject_assignment`, `parallel_group`, `hours_per_week`)
bleiben immer hart - sie sind physisch/strukturell notwendig. Details
und Beispiele: `json-constraints-reference.md`.

### 8.4 Rückverfolgbarkeit (`reason`-Feld)

Ein optionales `reason`-Feld auf Constraint-Objekten trägt eine kurze,
menschenlesbare Herkunftsangabe (z.B. eine vom LLM paraphrasierte
Textstelle). `Validation.vb` und `Verifier.vb` hängen diesen Text (falls
gesetzt) an jede zugehörige Fehler-/Verstoß-Meldung an
(`"... (Regel-Herkunft: '...')"`), `Solver.vb`s `KannConstraintFlag`
trägt ihn ebenfalls. Bewusste Grenze: `reason` ist eine Paraphrase, kein
wörtliches Zitat mit Zeichen-Offset - eine exakte Textstellen-Markierung
im GUI bräuchte ein zusätzliches Verbatim-Feld, das (noch) nicht existiert.

### 8.5 Determinismus

Jeder Solve-Aufruf nimmt `seed`/`numWorkers` entgegen und übergibt sie
direkt als CP-SAT-`StringParameters` (`random_seed`, `num_search_workers`).
Bei `numWorkers:=1` und festem `seed` liefert derselbe Aufruf
reproduzierbar dieselbe Lösung - Grundlage sowohl für
Determinismus-Regressionstests als auch für nachvollziehbares
Nutzerverhalten ("derselbe Input liefert denselben Plan").

Zwei Einschränkungen, beide zeitgesteuert und daher naturgemäß nicht
reproduzierbar: der Stagnations-Cutoff (8.5-Nachtrag zu Phase 2.25) und
ein Abbruch per `CancellationToken` (8.11) beenden eine Suche nach
Wanduhrzeit statt nach Suchzustand. Der Aufrufpfad **ohne** Token und
ohne `IProgress` ist davon nicht berührt - er läuft unverändert direkt
und blockierend, und genau darauf zielt der Test
`DefaultCallPathUnchangedAndDeterministic`.

### 8.6 Wiederverwendung durch synthetische Szenario-Konstruktion

Ein durchgängiges Architekturmuster seit Phase 2.11: statt `Solver.vb`
um eine neue Planungsstufe zu erweitern, baut ein neues Modul ein
eigenständiges, in sich konsistentes `entities`/`constraints`-JSON
("synthetisches Szenario") und schickt es unverändert durch
`Solver.Solve()`. Beispiele: `Schienenraster.vb` bildet jede Schiene als
"Fach" einer Pseudo-Klasse ab (`no_overlap(class:=<Konfliktgruppe>)`
synchronisiert die Schienen einer Gruppe gegeneinander);
`Raumzuordnung.vb` pinnt Tag/Periode aus Stufe B über `forbidden_slot`
auf allen anderen Slots. Der Vorteil: `BuildCoreModel`/`ApplyConstraints`
bleiben stabil und weiterhin nur für die 13 klassenbasierten
Constraint-Typen zuständig - jede neue Schulform ist ein neues Modul,
keine neue `Case`-Verzweigung im Kern. Die Klassenbildung (Stufe 0)
treibt das Muster auf die Spitze: sie nutzt die Pipeline gar nicht,
sondern ist ein komplett eigenständiges CP-SAT-Teilmodell ohne jede
Kopplung an `Solver.vb` - ihr Ergebnis fließt fachlich (welche Kinder
bilden Klasse 1a) in die Stammdaten der Folgestufen ein, nicht
technisch.

### 8.7 Wire-Format-Parität

`System.Text.Json.Nodes` (`JsonObject`/`JsonArray`) statt eines starren
Klassenmodells - bewusst dieselbe "dict-artige" Flexibilität wie Pythons
`dict`, um das Übersetzungsrisiko beim Portieren zu minimieren.
`JsonHelpers` in `Models.vb` kapselt den Zugriff (`GetString`, `GetInt`,
`AsStringList` - Letzteres normalisiert Felder, die sowohl ein einzelner
String als auch eine Liste sein dürfen). Ein typisiertes Modell (für
GUI-Databinding) kann später darüber gelegt werden, ohne diesen Kern
anzufassen.

### 8.8 Symmetriebrechung und Lösungsvielfalt

Überall dort, wo mehrere Alternativen enumeriert werden, trennt die
Architektur zwei Anliegen sauber:

- **Symmetrie-Duplikate verhindern** übernimmt Symmetriebrechung IM
  Modell: lexikografische Ketten über die Zuteilungsvektoren
  benachbarter Mitglieder einer Lehrer-Äquivalenzklasse
  (`Lehrereinsatzplanung.AddSymmetryBreaking`) bzw. eine
  Präzedenzkette über die Klassen der Klassenbildung (Kind i darf
  Klasse c nur eröffnen, wenn ein früheres Kind Klasse c-1 eröffnet
  hat; per Fixierung referenzierte Klassen sind ausgenommen, weil ihre
  Nummern extern Bedeutung tragen).
- **Echte Vielfalt erzwingen** übernehmen Cuts ZWISCHEN den Läufen:
  No-Good-Klauseln ("nicht identisch") bzw. Distanz-Cuts ("mindestens
  n Entscheidungen anders" - `minDiversity` in `SolveTop`,
  `assignmentMinDiversity` in `SolveLehrereinsatzTop`, `min_distanz`
  in `SolveKlassenbildungTop`), jeweils kombiniert mit einem
  Qualitätsband (ε bzw. absolute Toleranz), damit Vielfalt nicht auf
  Kosten deutlich schlechterer Lösungen geht.

Die Grenze zwischen beiden ist eine dokumentierte Lehre (siehe 6.8):
Diversitäts-Cuts auf der Äquivalenzklassen-PROJEKTION wären zu grob
und würden echte Alternativen verbieten - Cuts bleiben deshalb auf
Namens-/Variablen-Ebene, Orbit-Eindeutigkeit leistet allein die
Symmetriebrechung.

### 8.9 Getrennte Testsuiten nach Änderungsbereich

`Klassenbildung.Tests` ist ein eigenes Testprojekt neben
`TimetableCore.Tests` - nicht aus Ordnungsliebe, sondern als
Kosten-Entscheidung: die Stundenplan-Suite exerziert die Klassenbildung
nicht (und umgekehrt), läuft aber Minuten statt Sekunden. Die feste
Regel dazu (welcher Änderungsbereich welchen Prüfumfang verlangt, inkl.
des Sonderfalls "reine Viewer-Änderung → Build + `render` +
Chromium-Smoke statt Suite") steht in `timetable-dotnet/CLAUDE.md`.

Mit dem GUI-Unterbau sind drei weitere Suiten dazugekommen, alle nach
demselben Kosten-Prinzip schnell gehalten: `TimetableYaml.Tests`
(Round-Trips gegen die echten Beispieldateien, <1s) und
`TimetableWorkflow.Tests` (die Pipeline als Dienst, ~3s gegen eine per
`Scaffold` erzeugte einzügige Testschule - die echte
Grundschul-Fixture braucht 180s Solve-Budget und gehört deshalb in die
`run`-Beispielläufe, nicht in eine Unit-Suite) und `TimetableProjekt.Tests`
(Container, Krypto und Modellzusagen, ~2s - die PBKDF2-Iterationszahl ist
dort bewusst klein, der Produktionswert wird separat festgenagelt). "Volle Suite" heißt
seither ALLE SIEBEN Testprojekte, am einfachsten über
`dotnet test TimetableCore.sln`.

### 8.10 Self-contained-HTML-Viewer und ihre Verifikation

Beide Viewer (`stundentafel.html`, `klassenbildung.html`) folgen
demselben Muster: ein statisches HTML-Template mit Inline-CSS und
ES5-Inline-JS liegt als Embedded Resource im `TimetableWorkflow`; beim
Schreiben wird das komplette Ergebnis-JSON in einen
`<script type="application/json">`-Block eingebettet (`__..._JSON__`-
Platzhalter, `</script`-Escape). Die erzeugte Datei ist vollständig
offline per Doppelklick nutzbar - keine Bibliothek, kein Server, keine
Build-Kette; ältere JSON-Stände werden defensiv behandelt (fehlende
Felder → Feature degradiert statt Fehler, z.B. läuft der
Klassenbildungs-Viewer ohne `balance`-Block im reinen Anzeige-Modus
ohne Drag & Drop).

Interaktive Was-wäre-wenn-Funktionen (Gewichte-Regler, Pareto-Filter,
U4-Live-Bewertung mit Drag & Drop) erfordern Zähllogik im Browser -
diese ist als bewusste, kommentierte Formel-Duplikation des VB-Kerns
umgesetzt (siehe 8.2) und wird pro Änderung durch
Headless-Chromium-Interaktionstests abgesichert: ein vor `</body>`
injiziertes Testskript dispatcht echte DOM-/Drag-Events, schreibt
Kennzahlen in `document.title`, und eine unabhängige
Python-Nachrechnung derselben Rohdaten prüft die Browser-Ergebnisse
(z.B. "Live-Bewertung == Bewerte" zeichengleich für alle Varianten,
Ampel-Zähler nach einer simulierten Verschiebung exakt wie
vorhergesagt). Verteilungs-Detail siehe 7: die generierten Seiten
werden als Claude-Artifacts bereitgestellt; die WPF-GUI hostet dieselben
Viewer als dritten Kanal in WebView2 (siehe 2 und
`docs/gui-datenhaltung-konzept.md`), statt sie in XAML nachzubauen.

### 8.11 Abbruch und Fortschritt

Jeder langlaufende Einstiegspunkt des Kerns nimmt zwei zusätzliche
optionale Parameter entgegen:

```vb
Optional cancellationToken As CancellationToken = Nothing,
Optional progress As IProgress(Of SolveProgress) = Nothing
```

Betroffen sind alle neun: `Solve`, `SolveTop`, `SolveKursstufe`,
`SolveKlassenbildung`, `SolveKlassenbildungTop`, `SolveKursblockung`,
`SolveLehrereinsatz`, `SolveLehrereinsatzTop`, `SolveCombinedSchool`.
Motiv ist die GUI (Phase 3): ein GMS-Lauf mit
`solve_time_limit_s: 1200` lief vorher 20 Minuten ohne Lebenszeichen
und ohne Abbruchmöglichkeit.

**Ein gemeinsamer Ausführungspfad.** `SolveRunner.RunSolve`
(`SolveControl.vb`) ist die verallgemeinerte Fassung von Phase 2.25s
`SolveWithStagnationCutoff`: dieselbe Task-plus-500ms-Polling-Schleife,
jetzt zusätzlich um Abbruch und Fortschritt erweitert - und von *allen*
Solve-Stellen benutzt statt nur von der `SolveTop`-Iterationsschleife.
`solver.StopSearch()` bleibt der Abbruchmechanismus (in 2.25a
cross-thread live verifiziert).

**Der Pfad ohne Token und ohne Progress ist unverändert.** `RunSolve`
erkennt diesen Fall und ruft `solver.Solve(model)` direkt und
blockierend auf dem aufrufenden Thread - kein `Task`, kein Polling.
Das hält die Zusage aus 8.5 (`numWorkers:=1` + fester `seed` =
reproduzierbar dieselbe Lösung) und verhindert, dass sich die
Benchmark-Laufzeiten verschieben. Ein bereits gesetztes Token wird noch
vor dem Modellbau erkannt; der Aufruf rechnet dann gar nichts.

**Abbruch wirft nicht, sondern liefert das Teilergebnis.** Das
entspricht dem etablierten Stagnations-Cutoff (früh stoppen, Bestes
behalten) und der Konvention des Kerns, Fehlerzustände als
Ergebnisobjekte zu liefern statt als Exception (vgl. 8.1). Konkret:
`MultiSolveStopReason` hat den Wert `Cancelled`, alle übrigen
Ergebnistypen tragen ein `Cancelled As Boolean`. Ein abgebrochenes
`SolveKlassenbildungTop` gibt die bereits fertigen Varianten zurück,
ein abgebrochenes `SolveTop` die bereits gefundenen Lösungen -
weiterhin nach `Quality.Total` sortiert. Erfolgt der Abbruch in der
Vorphase (lexikographische Stufen, Warmstart), ist `Solutions` **leer**,
ohne dass das Szenario unlösbar wäre - Aufrufer dürfen aus einer leeren
Liste bei `Cancelled` also nicht auf Unlösbarkeit schließen (anders als
bei `SearchSpaceExhausted`). Fällt ein Abbruch mit dem Erreichen von
`maxSolutions` zusammen, gewinnt `MaxSolutionsReached`: die Suche wäre
ohnehin zu Ende gewesen.

**Gemeldet wird ausschließlich vom aufrufenden Thread.**
`OnSolutionCallback` läuft auf einem CP-SAT-Workerthread *innerhalb* des
nativen SWIG-Aufrufs; fremden Handler-Code von dort zu rufen wäre
doppelt riskant (eine Exception propagierte über die native Grenze, und
ein langsamer Handler bremste die Suche). `ConvergenceCallback` sammelt
deshalb weiterhin nur Datenpunkte - threadsicher unter einer Sperre,
inklusive `BestObjectiveBound()` für die Live-Optimalitätslücke - und
die Polling-Schleife liest den Stand und meldet ihn. Jeder
`Report`-Aufruf ist zusätzlich in `Try/Catch` gekapselt: ein fehlerhafter
GUI-Handler darf keinen laufenden Solve abbrechen. `IProgress(Of T)` und
nicht ein Event, weil `Progress(Of T)` den `SynchronizationContext` beim
Konstruieren einfängt und selbsttätig auf den UI-Thread marshallt -
ein Event feuerte auf dem Pollingthread, und jeder Zugriff auf ein
`DispatcherObject` von dort löst in WPF sofort eine
`InvalidOperationException` aus (Thread-Affinität; für die
WebView2-gehosteten Viewer gilt dasselbe, siehe 2). `ConvergenceCallback` bleibt bewusst
`Friend`: sie öffentlich zu machen hieße, den OrTools-Basistyp
`CpSolverSolutionCallback` in den GUI-Vertrag zu heben und Aufrufern zu
erlauben, beliebigen Code auf den CP-SAT-Thread zu legen.

Verkettete Einstiegspunkte (`SolveKursstufe`, `SolveCombinedSchool`)
rechnen nicht selbst, sondern reichen Token und Progress durch. Damit
die Anzeige dabei nicht je Stufe auf 0 zurückspringt, etikettiert
`StageProgressAdapter` die Meldungen der inneren Aufrufe auf die Sicht
des Gesamtlaufs um ("Stufe 2 von 4", durchlaufende Uhr).

Getestet wird ohne jede Timing-Annahme (`CancellationProgressTests.vb`,
`KlassenbildungCancellationTests.vb`): Abbruch wird entweder vorab
gesetzt oder deterministisch aus einem **synchron** aufgerufenen
`IProgress` ausgelöst - möglich, weil `RunSolve` beim Phasenstart
garantiert einmal meldet, unabhängig von der Solve-Dauer. Zu jedem
Abbruchtest gehört eine Gegenprobe, die belegt, dass dasselbe Szenario
ohne Token tatsächlich lösbar ist.

### 8.12 Projektablage: ein verschlüsselter Container

`TimetableProjekt` setzt `docs/gui-datenhaltung-konzept.md` um. Eine
Schule plus ein Schuljahr sind **eine Datei** (`.splanx`): ein
ZIP-Container mit JSON-Einträgen, als Ganzes mit AES-256-GCM
verschlüsselt.

Aufbau der Datei — der Kopf liegt bewusst **unverschlüsselt**:

```
Bytes 0..7    Magic "SPLANX01"
Bytes 8..11   Länge des Klartext-Kopfes (Int32 LE)
danach        Kopf-JSON: KDF, Iterationszahl, Salt, Nonce
danach        16 Byte GCM-Tag
Rest          Chiffrat = ZIP mit manifest/stammdaten/constraints/
              klassenbildung/config/mapping/audit-log/gui-state/ergebnisse
```

Die Iterationszahl muss lesbar sein, *bevor* der Schlüssel abgeleitet
werden kann, und soll später erhöhbar sein, ohne alte Dateien unlesbar zu
machen. Salt und Nonce sind per Definition öffentlich; ein Geheimnis
steht nicht im Kopf. Schlüsselableitung ist PBKDF2-SHA256 mit ≥ 600.000
Runden, die Nonce ist pro Speichervorgang frisch.

**Alles BCL** — `System.IO.Compression`, `AesGcm`,
`Rfc2898DeriveBytes.Pbkdf2`, `System.Text.Json`. Keine neue Abhängigkeit,
kein zweiter Native-Stack neben `Google.OrTools`. Bewusst *nicht* hier:
das optionale „Passwort merken" per Windows-DPAPI — es bräuchte ein
Windows-only-Paket und gehört deshalb in `TimetableGui`
(`net8.0-windows`), nicht in eine plattformneutrale Bibliothek.

**Atomar speichern** (Anforderung A9): serialisieren → ZIP im Speicher →
verschlüsseln → Temp-Datei **im selben Ordner** → `File.Replace`. Der
Umweg über `%TEMP%` scheidet doppelt aus: `File.Replace` wäre über
Datenträgergrenzen hinweg kein Rename mehr, und Klartext hätte in einem
fremden Verzeichnis nichts verloren. Ein Absturz mitten im Speichern
hinterlässt damit die alte, intakte Datei.

**Kein Recovery-Pfad.** Passwort vergessen heißt Daten verloren; das ist
die Kehrseite echter Verschlüsselung und die getroffene Entscheidung
(schließt offenen Punkt 1 des Datenhaltungskonzepts). Abgemildert wird
sie organisatorisch, nicht technisch.

Drei Zusagen liegen im Modell, nicht im Container, und sind einzeln
getestet: eine **einmal vergebene Schüler-ID bleibt verbrannt** (der
Zähler im Manifest zählt nur aufwärts, damit alte Audit-Einträge nie auf
ein anderes Kind zeigen); **Freigabe- und Bestands-Stände** sind gegen
Verdrängen *und* Löschen geschützt, und wenn nur noch geschützte Stände
übrig sind, tritt die Obergrenze zurück statt eine Freigabe wegzuwerfen;
das **Audit-Log überlebt das Löschen eines Standes** — die Ergebnisdaten
verschwinden, die Zeile bleibt.

`AesGcm` ist authentifiziert: ein falsches Passwort und eine manipulierte
Datei scheitern beide an derselben Prüfung. Der Kern kann sie nicht
unterscheiden, und die Fehlermeldung behauptet es deshalb auch nicht.

### 8.13 Die Oberfläche: Viewer hosten statt nachbauen

`TimetableGui` ist eine WPF-Anwendung (`net8.0-windows`, `UseWPF`), die
`TimetableCore`, `-Yaml`, `-Workflow` und `-Projekt` per `ProjectReference`
nutzt. Ihre einzige zusätzliche Abhängigkeit ist `Microsoft.Web.WebView2`;
alle vier Bibliotheken bleiben plattformneutrales `net8.0` (5.1).

**Die beiden Dashboards sind die bestehenden HTML-Viewer, 1:1 gehostet.**
Sie sind fertig, per Interaktionstest verifiziert (8.10) und in XAML
nachzubauen hieße, ihre Logik ein zweites Mal zu schreiben. WPF liefert
Rahmen, Navigation und Formulare.

**Wie die Seiten in den Browser kommen.** Das Datenhaltungskonzept ließ
offen, ob `NavigateToString` oder ein virtuelles Host-Mapping benutzt wird
(7.6). Beides scheidet aus:

- `NavigateToString` hat eine dokumentierte Grenze von rund 2 MB. Die
  Stundentafel-Seite überschreitet sie in realistischen Konfigurationen -
  am GMS-Beispiel wurden 2,49 MB gemessen (28 exportierte Lösungen). Die
  Größe ist dabei **lauf- und konfigurationsabhängig**: derselbe Datensatz
  ergab bei einem späteren Lauf mit 20 Lösungen 1,77 MB, weil der Export
  *alle* gefundenen Lösungen enthält und damit mit `max_solutions` und
  Schulgröße skaliert. Eine Anzeige, die ab einer bestimmten Lösungszahl
  kaputtgeht, ist keine Option.
- Virtuelles Host-Mapping bildet einen *Ordner* ab. Dafür müsste die
  entschlüsselte HTML als Datei auf der Platte liegen - genau das, was 7.6
  verbietet.

Stattdessen `AddWebResourceRequestedFilter` plus `WebResourceRequested`:
eine synthetische Herkunft (`https://viewer.local`, `.local` ist per
RFC 6762 nie öffentlich auflösbar), deren Antworten vollständig aus dem
Speicher kommen. Kein Klartext auf Platte, keine Größengrenze; Navigation
außerhalb dieser Herkunft wird abgebrochen, und `NewWindowRequested` ist
unterbunden - es ist ein Anzeigerahmen, kein Browser. Der WebView2-
User-Data-Folder liegt app-eigen unter
`%LocalAppData%\Schulplanung\WebView2\` (7.6).

**Was ohne Fenster prüfbar ist, liegt außerhalb des Fensters.** Auf dem
Entwicklungsrechner existiert nur eine getrennte Desktop-Sitzung, das
laufende Fenster ist also nicht beobachtbar. Deshalb liegen Zustand und
Ablauf in ViewModels, Datei- und Passwortabfragen hinter `IDialoge`, und
die Auslieferung in einer Klasse, die WebView2 gar nicht kennt.
`TimetableGui.Tests` fährt gegen genau diese Teile - ohne Fenster, ohne
Browser. Was dadurch **nicht** abgedeckt ist und ehrlich offen bleibt: das
tatsächliche Rendern in WebView2 und das Verhalten der XAML-Bindungen zur
Laufzeit. Beides ist nur am laufenden Fenster zu sehen.

### 8.14 Die Brücke: ein Kanal, zwei Betriebsarten

Der Klassenbildungs-Viewer läuft in **zwei** Betriebsarten, und beide
müssen funktionieren:

- **Doppelklick** — keine Brücke, Pins im `localStorage`, Rückweg ist der
  `fixierungen:`-YAML-Block zum Kopieren. Das ist eine dokumentierte
  Zusage (8.10), kein Nebenweg.
- **WebView2** — der Host injiziert den Zustand vor dem Laden, bekommt
  jede Änderung zurück und ersetzt den Export-Block durch „Neu rechnen".

Deshalb ist die Erweiterung **additiv und feature-erkannt**: alles hängt
an `window.chrome.webview.postMessage`, das WebView2 selbst stellt. Der
alte Pfad wurde nicht ersetzt, sondern nur umgangen, wenn ein Host da ist.

**Nachrichtenformat.** Weder das UI- noch das Datenhaltungskonzept legt
eines fest — sie nennen nur Transportmittel und fachliche Nutzlasten. Es
entsteht daher hier, mit einem **versionierten Umschlag**:

```json
{"v": 1, "typ": "zustand" | "neu-rechnen", "nutzlast": { … }}
```

Die Versionierung ist nicht vorsorglich: die Vorlagen sind Embedded
Resources, ein Host läuft also immer mit *seiner* Version — aber eine als
Claude-Artifact veröffentlichte Seite (CLAUDE.md) kann älter oder neuer
sein. Der Host ignoriert deshalb unbekannte Typen und neuere Versionen
**still**, statt zu werfen.

**Richtung Host → Seite** gibt es genau zwei Variablen, per
`AddScriptToExecuteOnDocumentCreated` vor dem Laden gesetzt:
`window.__gastZustand` (Pins, Härtungen, Herkunft — ersetzt die
`localStorage`-Rolle durch `gui-state.json` der Projektdatei) und
`window.__anzeigeNamen`. Letztere ist der **einzige** Weg, auf dem
Klarnamen in die Seite gelangen; sie werden dort ausschließlich in den
DOM gerendert, während das eingebettete JSON und jeder Export pseudonym
bleiben (Datenhaltung 6.1/6.2). Ein Test prüft genau diese Grenze.

**U5 ist damit geschlossen.** „Neu rechnen" schickt die Fixierungen
strukturiert an den Host — mit denselben Feldnamen wie das YAML
(`kind`/`klasse`/`nicht_klasse`), sodass die Übernahme eine reine
Umformung ohne Namenszuordnung ist. Der Host ersetzt die Fixierungsliste
(nicht anhängen: die YAML-Bestandsfixierungen sind in der Board-Liste
bereits als `herkunft: bestehend` enthalten, Anhängen würde sie
verdoppeln), setzt die gehärteten Regeln auf `modus: hard`, schreibt eine
Audit-Zeile und startet den Lauf. Der Weg „YAML-Block kopieren → CLI
aufrufen" entfällt — und **nur** der; Board, Bewertung und Trichter
bleiben unverändert (gui-ui-konzept.md 4).

**Verifikation ohne Browser-Einbettung.** Die Feature-Erkennung hängt an
einer JS-Eigenschaft, also lässt sich der Host durch ein Init-Skript
nachstellen, das `postMessage` in ein Array schreibt. `TimetableViewer.Tests`
(Playwright, headless, ohne Desktop-Sitzung) prüft damit beide
Betriebsarten und das Protokoll an der echten Vorlage;
`TimetableGui.Tests` prüft die Gegenseite an derselben Zeichenkette. Was
weiterhin **nicht** abgedeckt ist: dass WebView2 die Nachricht auch
tatsächlich zustellt — das hängt am laufenden Fenster (8.13).

### 8.15 Namen sind Schlüssel — Umbenennen und Löschen

Lehrkraft-, Fach-, Klassen- und Raumnamen referenzieren einander über den
**Namen** (Wire-Format, 8.7). Für eine Oberfläche, in der man Objekte
umbenennt und löscht, ist das die riskanteste Eigenschaft des Modells: ein
vergessener Verweis fällt nicht sofort auf, sondern wird zu einer
verwaisten Referenz, die erst der nächste Solve-Lauf als „unbekannte
Entity" meldet — bis dahin sieht alles gut aus.

`Bestandspflege` (`TimetableProjekt`) setzt deshalb die beiden Regeln aus
gui-ui-konzept.md 7 um: **Umbenennen kaskadiert** über Qualifikationen,
feste Zuordnungen, Gruppen, Schüler und Regeln — mit Vorschau; **Löschen**
zeigt vorher alle betroffenen Objekte und räumt sie mit, statt Referenzen
verwaisen zu lassen.

**Die Regel-Seite fragt, statt nachzubauen.** Welches Constraint-Feld auf
welche Entity-Art verweist, weiß `Validation` — dort steht die Zuordnung,
gegen die auch geprüft wird. `Validation.ReferenzenVon` macht sie
abfragbar. Der knifflige Fall ist `entity`: worauf es zeigt, hängt vom
Geschwisterfeld ab (`resource` bei `no_overlap`, `scope` bei
`forbidden_slot` bzw. `occupied_slot`/`-window`), und `occupied_*` erlaubt
bewusst **kein** `room`. Diese Kenntnis in der Oberfläche zu duplizieren
hieße, dass beide Seiten auseinanderlaufen können — genau der stille
Fehler, den die Kaskade verhindern soll.

Zwei Entscheidungen, die im Code begründet stehen:

- **Ein Name, den es schon gibt, wird abgelehnt.** Zwei Lehrkräfte
  gleichen Namens wären im Wire-Format nicht unterscheidbar; der Solver
  legte ihre Stunden stillschweigend zusammen.
- **Bei Listenfeldern** (`allowed_rooms`, `classes`) wird nur der Eintrag
  entfernt — eine Sport-Regel mit zwei Turnhallen darf nicht
  verschwinden, weil eine davon gelöscht wird. Erst die leere Liste macht
  die Regel sinnlos.
- **Kinder überleben ihre Klasse.** Beim Löschen einer Klasse wird die
  Heimatklasse geleert, das Kind bleibt — es ist kein Anhängsel.

Der Maßstab der Tests ist nicht „die Kaskade hat etwas geändert", sondern
`ValidateEntities` findet danach **null** verwaiste Referenzen — gefahren
gegen beide echten Beispielschulen, weil die GMS-Fixture mit ihren 2204
Constraint-Zeilen alle Referenzarten enthält.

**Prüfen und Rechnen-Sperre.** „Speichern ist immer möglich, Rechnen nur
bei grüner Prüfung" (Konzept 1). Die Prüfung führt die bestehenden
Validate-APIs aus — sie sind die eine Wahrheit. Ergänzt wird nur eine
**Vollständigkeits**-Vorprüfung, und zwar in der Oberfläche, nicht im
Kern: ein leeres Stammdatenmodell ist widerspruchsfrei und fällt durch
jede Konsistenzprüfung durch; der Kern scheitert erst spät und technisch
(„Keine `teacher_subject_assignment`-Constraints gefunden"), die
Oberfläche soll früh und in Klartext sagen, was fehlt.

### 8.16 Designsystem: eine Quelle, drei Kopien

Die Oberfläche besteht aus **zwei Technologien** — der WPF-Hülle und den
beiden HTML-Viewern, die in WebView2 den größten Teil der Fläche
einnehmen (8.13). Sie sahen bis August 2026 auch so aus: der Bestand
hatte **kein einziges Design-Token**, ~20 Grautöne für vier Rollen, 14
Schriftgrößen zwischen 0,66 und 1,3 rem, vier Eckenradien für dieselbe
Elementklasse, drei unabhängige Knopf-Definitionen, zwei Schattenrezepte
für dieselbe Rolle, **null** `:focus`-Regeln und nirgends eine deklarierte
Zeilenhöhe. Die zwischen beiden Vorlagen wörtlich duplizierten Regeln
(`body`, `h1`, `h2`, `#meta`, `#controls`, `#compare-info`) waren bereits
auseinandergelaufen.

Zwei Befunde waren keine Schönheitsfehler, sondern Verstöße gegen die
eigenen Konzepte: es gab **keine Tastaturbedienung**, obwohl
`klassenbildung-ui-konzept.md:203` sie zusagt ("Pin = Enter auf
fokussierter Karte"), und die kategoriale Palette **kollidierte mit der
Ampel-Semantik** — `#15803d` war zeichengleich mit dem Ampel-Grün,
`#c2410c` mit dem Diff-Marker, obwohl dieselbe Konzeptzeile die Trennung
"strikt" nennt und entsättigte Töne fordert.

#### Kopie statt Injektion

Quelle sind `TimetableWorkflow/Templates/design-tokens.css` (Werte) und
`design-basis.css` (geteilte Regeln). Beide Viewer-Vorlagen tragen eine
**zeichengleiche Kopie** zwischen Markern, `TimetableGui/Design/Tokens.xaml`
spiegelt den Kanon.

Eine Injektion zur Generierungszeit wäre naheliegender gewesen und wurde
verworfen: sie hätte die Rohvorlage unstyled gelassen (ein realer
Debug-Weg bei 1900 Zeilen), über `String.Replace` einen still
scheiternden Platzhalter eingeführt — und der dann nötige Fallback-`:root`
wäre selbst wieder eine Drift-Quelle gewesen, womit die Injektion
überflüssig wird. Es ist dasselbe Muster wie bei den JS-Formelkopien des
VB-Kerns (8.2): bewusste, maschinell geprüft zeichengleiche Duplikation.

**Kanon** ist, was in beiden Welten sichtbar ist: die Präfixe `--farbe-`,
`--kat-`, `--radius-`. Gefiltert wird über eine Regel, nicht über eine
Liste — eine Liste wäre selbst ein Objekt, das driften kann. Schriftstapel
gehören ausdrücklich NICHT dazu: der Web-Stapel enthält `-apple-system`
und `sans-serif`, die es in WPF nicht gibt; eine Gleichheitsprüfung darauf
wäre ein Wächter, der Unsinn assertiert. Geprüft wird XAML → CSS, nicht
umgekehrt — WPF muss nicht jede Farbe der Viewer kennen.

#### Was die Wächter fangen

| Test | Fehlerklasse |
|---|---|
| `DesignTokenTests.JedeRegionIstZeichengleichZurQuelle` | Kopie driftet von der Quelle |
| `.JedeVariableHatEineDeklaration` | Tippfehler in `var(--…)` — in CSS **kein** Fehler, die Deklaration fällt still auf `inherit` zurück |
| `.KeineFarbliteraleAusserhalbDerRegion` | Rückfall auf Literale |
| `.DieBasisStehtVorDenEigenenRegeln` | Reihenfolge — `#meta`/`#controls` sind ID-Selektoren, eine nachgestellte Basis clobbert jede Überschreibung still |
| `DesignKanonTests.JedeVerwendeteRessourceIstDefiniert` | fehlender `StaticResource`-Schlüssel — wirft erst beim Fensteraufbau |
| `.SymbolzeichenGibtEsInBeidenSchriften` | ein nur in *Segoe Fluent Icons* vorhandenes Zeichen erscheint auf Windows 10 still als Kästchen |
| `TokenVerbrauchTests.KeinTokenBleibtUngenutzt` | die Quelle wird zum Friedhof |
| `TastaturTests` (5) | die Zusage aus dem Konzept bleibt wieder unerfüllt |

Jeder dieser Wächter wurde beim Einbau mit echter Drift **rot gemacht und
wieder zurückgenommen** — ein Test, der nie rot war, ist keine Zusage.

#### Belege des Umbaus

Der Byte-Vergleich der ganzen Datei scheidet aus, weil sich der
`<style>`-Block absichtlich ändert. Getragen hat stattdessen:

- **`tools/css-umbau-pruefen.ps1`** — alles AUSSERHALB von `<style>` muss
  byteweise unverändert bleiben. Bei der GMS-Seite sind das 1,85 MB
  Markup und JS gegen 15 KB CSS, also 99,7 % der Datei. Der Wächter fing
  bei seinem ersten Einsatz einen echten Fehler: `sed -i` schreibt die
  CRLF-Vorlagen still auf LF um — 1.269 stille Byte-Änderungen.
- **Berechnete Stile vorher/nachher**, je Selektor und Eigenschaft (49
  bzw. 69 Selektoren × 26 Eigenschaften). Jede der 437 bzw. 648
  Abweichungen ließ sich einem Token zuordnen; keine fiel auf
  `transparent`, `none` oder leer — genau so sähe eine nicht auflösende
  `var()` aus.

#### Zwei Korrekturen am eigenen Vorgehen

**Eine streng wertbewahrende Tokenisierung ist unmöglich.** Der Bestand
hatte drei Grautöne für "Sekundärtext", drei für "Rahmen" und zwei
Gelbtöne für denselben Zustand — genau diese Zusammenlegung *ist* der
Zweck. Wertbewahrend zu tokenisieren hieße, zwanzig Grautöne als zwanzig
Token zu behalten. Deshalb wurde der Nachweis feinkörniger gemacht statt
die Sache: pro Eigenschaft statt pro Stufengrenze.

**`--dichte-zeile` meint die Gesamthöhe, nicht die Inhaltshöhe.** Ohne
`box-sizing: border-box` kamen Polsterung und Rahmen obendrauf, und die
kompakte Karte wuchs von 24 auf 28 px — der Umbau hätte jedem, der heute
kompakt arbeitet, Fläche weggenommen. Aufgefallen ist das nur, weil ein
Test die Dichte in Pixeln misst statt sie zu behaupten.

#### Bewusst nicht

**Dunkelmodus.** Er verlangt eine zweite Kalibrierung von Ampel *und*
kategorialer Palette und kollidiert mit dem Druckbild; `color-scheme:
light` bleibt gesetzt. Die Tokens machen ihn später zu einer Änderung an
einem Block — dann mit ordentlicher Kalibrierung statt nebenbei.

**Badges sind nicht fokussierbar.** Sie sitzen zu Dutzenden je Karte und
würden die Tab-Kette unbrauchbar machen; ihre Funktion — eine Gruppe
hervorheben — ist über die Panel-Zeile erreichbar, und die ist es.

**Die Dichte liegt im `localStorage`, nicht im Projekt.** Sie ist eine
Eigenschaft des Bildschirms, an dem jemand sitzt, nicht des Plans — und
funktioniert dadurch in beiden Betriebsarten gleich, denn auch der
WebView2-Host liefert unter einer echten Herkunft aus.

## 9. Architekturentscheidungen

Ausgewählte, besonders folgenreiche Entscheidungen (ausführliche
Begründungen jeweils in den referenzierten `docs/phase2-*.md`-Berichten):

| Entscheidung | Alternative(n) verworfen | Begründung |
|---|---|---|
| Live-Verifikation neuer OrTools-APIs per Reflection/Wegwerf-Smoke-Test VOR jeder Code-Änderung | Blindes Vertrauen in die NuGet-Paket-XML-Doku | Mehrfach real bestätigt notwendig: `CpModel.ClearObjective()` ist dokumentiert, existiert aber NICHT auf der installierten 9.15.6755-DLL (Phase 2.12a); `AddMinEquality`/`AddMaxEquality`-Signaturen wurden vor Phase 2.9 verifiziert statt angenommen. |
| Gestufter Warmstart (Kann-only-Vorlösung + `AddHint`) für `SolveTop` bei großen Szenarien | Nur CP-SATs eigenes Portfolio-Threading (`numWorkers>1`) | Bei `numWorkers:=1` fand die volle 6-Kriterien-Zielfunktion beim 30-Klassen-GSG-Szenario 30 Minuten lang KEINE Lösung; mit Staging < 20 Minuten. `numWorkers:=4`+Staging kombiniert bleibt Zeile für Zeile in `docs/phase2-12-staged-hints.md` dokumentiert (kein klarer zusätzlicher Gewinn gegenüber `numWorkers:=4` allein - ehrlich berichtet, nicht schöngeredet). |
| Schienenmodell mit Pseudo-Klasse pro Konfliktgruppe (nicht: eine globale Pseudo-Klasse für alle Schienen) | Alle Schienen in einer einzigen `no_overlap(class:="Kursstufe")`-Gruppe | Eine einzelne Gruppe verlangt so viele Wochen-Slots wie die SUMME aller Schienen-Wochenstunden (48h > 40 verfügbare Slots im realistischen Fixture) - Konfliktgruppen (nur tatsächlich durch ein gemeinsames Wahlprofil verbundene Schienen) lassen unabhängige Schienen echt parallel laufen, wie reale Schulplanung es auch tut. |
| Pro-Schiene-Platzhalterlehrer (`_schiene_{Id}`) statt eines geteilten `_schiene` | Ein einzelner gemeinsamer Platzhalter für alle Schienen | Eine gezielte `teacher_availability`-Regel für eine Schiene hätte sonst versehentlich JEDE Schiene blockiert (Solver.vb ist nach `.Teacher`, nicht nur `.ClassName`, indiziert) - notwendig für Phase 2.13s Sek-I/Kursstufe-Überschneidungsvermeidung. |
| Kombinierte Verifikation NICHT durch Zusammenführen beider Original-Constraint-Listen | Beide Halbszenarien-Constraints unverändert in ein gemeinsames JSON kopieren | Live entdeckter Fehler: `room_requirement` matched rein über den Fachnamen-String (kein Herkunfts-Filter in `Verifier.vb`) - Sek Is Raumbindung griff dadurch fälschlich auch auf Kursstufes gleichnamige, aber unrestriktierte Kurse durch. Behoben durch ausschließlich frische `no_overlap`-Regeln für die Namens-Vereinigung (siehe `docs/phase2-13-combined-school.md`). |
| Lehrerkontinuität (Kl.5→Kl.6) rein auf Fixture-/Anwendungsebene, kein neues Solver-Feature | Ein neues "weiches Kontinuitäts-Constraint" im CP-SAT-Modell | `teacher_subject_assignment` definiert bereits vor `ApplyConstraints` die Entscheidungsvariablen-Identität selbst (`SessionsFromAssignments`) - es ist strukturell nicht Kann-fähig. Kontinuität kann nur durch Wiederverwendung desselben Lehrernamens beim Bau des Folgejahr-Szenarios erreicht werden (siehe `docs/phase2-14-lehrerkontinuitaet.md`). |
| `System.Text.Json.Nodes` statt eines generierten/handgeschriebenen typisierten Modells | Records/Klassen pro Constraint-Typ mit `JsonSerializer`-Attributen | Minimiert das Übersetzungsrisiko beim 1:1-Portieren aus Pythons dict-basiertem Original; ein typisiertes Modell kann bei Bedarf später ergänzt werden (GUI-Databinding), ohne den Kern zu ändern. |
| Klassenlehrer-Tandem-Balance (Phase 2.17) über den bereits in `SolveTopObjective.vb` verifizierten Sentinel-Min/Max-Trick, mit `>=`-Hinge statt `=`-Gleichheit | Direkte `AddMaxEquality`/`AddMinEquality`-Gleichheit für den Bereich (`tandemRange = tandemMax - tandemMin`) | Live beim Testschreiben entdeckt: bei genau einem (oder keinem) aktiven Tandem-Kandidaten wird der Sentinel-substituierte `tandemMin` (`bigStunden`) größer als der rohe `tandemMax` (0) - eine erzwungene Gleichheit wäre in diesem Randfall unlösbar gewesen. Die Ungleichung `tandemRange >= tandemMax - tandemMin, >= 0` (gleicher Hinge-Trick wie im Deputat-Korridor) lässt der Zielfunktion die Freiheit, `tandemRange` in diesem Fall auf 0 zu setzen, statt Infeasible zu werden. |
| `ClassGaps`/`TeacherGaps` (Phase 2.25-Nachtrag-2) über eine komplett Big-M-freie Präfix/Suffix-OR-Kodierung (`BuildGapFlags`: `anyBefore`/`anyAfter` je Periode, jede Lücken-PERIODE als eigene reifizierte `BoolVar`) statt des ursprünglichen Sentinel-Min/Max-Tricks | (1) `AddMinEquality`/`AddMaxEquality` mit Big-M-Sentinel-Substitution (Original); (2) eine erste "sentinel-freie" Zwischenstufe, die `AddMinEquality`/`AddMaxEquality` weiterhin nutzte, nur ohne Big-M-Konstante | Systematische Scratch-Experimente (Exp 1-13, `docs/phase2-25-stagnation-heuristik.md` Nachtrag 2) identifizierten `TeacherGaps`' ALTE Kodierung (nicht `ClassGaps`' ursprünglich 10x höheres Gewicht, wie zunächst vermutet) als eigentlichen Treiber einer über 300s hinweg komplett unbewegten `BestObjectiveBound` - selbst bei `TeacherGaps`' eigenem, vergleichsweise niedrigen Original-Gewicht. Zwischenstufe (2) reichte NICHT (97.9% statt 99.7% Lücke im Vollmaßstab-Test, kaum Verbesserung) - erst der komplette Verzicht auf `AddMinEquality`/`AddMaxEquality` behob es. `TeacherLoadVariance` (`BuildTeacherRangeVars`) und die Tandem-Balance oben nutzen den Sentinel-Trick bewusst unverändert weiter - beide waren nicht Teil dieser Diagnose, bleiben als möglicher späterer Kandidat dokumentiert. |
| Lexikografische Stufen als `SolveTop`-Default (Code-Review P2) statt der gewichteten Gesamtsumme | Nur die gewichtete Ein-Summen-Zielfunktion (vorheriger Stand) | Gewichte vermengen inkommensurable Kriterien (ein eingesparter Kann-Verstoß darf nie gegen viele Springstunden "verrechnet" werden); Stufen mit hartem Band machen die Prioritätsordnung explizit und nachvollziehbar. Der gewichtete Modus bleibt per `lexicographic:=False` erreichbar; `lexTolerance` erlaubt kontrolliertes Aufweichen des Bands. Belegläufe in `docs/code-review-cpsat-performance.md` und den `config.yaml`-Kommentaren der Beispiel-Schulen. |
| `occupied_window`/`subject_period_window` als Kriteriums-Semantik (Linearsumme) statt Kann-BoolVar-Batterien (Code-Review P1) | Pro Slot ein eigenes should-`occupied_slot`/`forbidden_slot` (Batterie; im GMS-Beispiel 720 Einzelregeln) | Batterien blähen das Modell mit Verletzungs-BoolVars auf und zählen "eine Regel = 1" unabhängig von der Slot-Zahl; die Fenster-Typen beschreiben dasselbe als EIN Objekt und zählen jeden Slot einzeln über ohnehin existierende Variablen. Opt-in-Lex-Stufen (`lexOccupiedDensityStage`/`lexSubjectWindowStage`) geben dem Kriterium bei Bedarf ein dediziertes Budget - die Antwort auf den P1-Langvergleich, in dem die Batterie die Fensterabdeckung zunächst dominierte (ehrlich dokumentiert). |
| Diversitäts-Cuts der Mehr-Zuteilungs-Enumeration auf Namens-Ebene, Orbit-Eindeutigkeit allein per Lex-Symmetriebrechung | Diversitäts-Cut auf der Äquivalenzklassen-Projektion ("dieselbe Klasse übernimmt dieselben Einheiten") | Beim Umsetzen selbst erkannt: die Projektion ist zu grob und hätte strukturell verschiedene Zuteilungen (z.B. "A übernimmt beide Klassen" vs. Aufteilung innerhalb derselben Äquivalenzklasse) als Duplikat verboten. Symmetrie und Vielfalt sind getrennte Anliegen mit getrennten Mechanismen (siehe 8.8). |
| Klassenbildung als vollständig entkoppeltes Modul mit eigener Testsuite und eigenen Prio-Gewichten 1000/50/1 (statt der Konzept-Vorgabe 10000/100/1) | (1) Integration in die `Solver.Solve()`-Pipeline via synthetisches Szenario; (2) Konzept-Gewichte unverändert übernehmen | (1) Die Klassenbildung teilt KEINE Fachlichkeit mit der Stundenplanung (keine Tage/Perioden/Sessions) - ein synthetisches Szenario hätte nur Ballast importiert; als eigenes Modell bleibt sie in Sekunden lösbar und in <1s testbar (siehe 8.9). (2) 1000/50/1 hält die Stufen-Trennung (keine realistische Zahl von Prio-2-Verletzungen wiegt eine Prio-3-Verletzung auf), bleibt aber im `Int64`-sicheren Bereich auch für große Jahrgänge; per `config.yaml` überschreibbar. |
| Viewer-Interaktivität durch bewusste, kommentierte JS-Formel-Duplikate des VB-Kerns | (1) Kein Was-wäre-wenn im Viewer (nur statische Anzeige); (2) ein Backend-/Server-Prozess für Neuberechnung | (1) verfehlt den Zweck des Arbeitsbretts (Konzept 9.2 Phase 3: reine Bewertung in Millisekunden, ohne Solver-Lauf); (2) bräche das Self-contained-Prinzip (Doppelklick, offline). Das Risiko der Duplikation (Divergenz) wird nicht geleugnet, sondern verifiziert: seit Stufe E prüft `TimetableViewer.Tests` (Playwright, headless) die JS-Kopie bei jedem Testlauf zeichengleich gegen den VB-Export, die Ground Truth bleibt der nächste CLI-Lauf (siehe 8.10). |

## 10. Qualitätsanforderungen

### 10.1 Qualitätsbaum (Auszug)

```
Qualität
├── Korrektheit
│   ├── Kein Muss-Verstoß in einer als lösbar gemeldeten Lösung
│   └── Unabhängige Re-Prüfung (Verifier) statt Selbstauskunft des Solvers
├── Robustheit
│   ├── LLM-Extraktion: Vollständigkeits-Score über Wiederholungen stabil
│   └── Deterministische Sicherheitsnetze für bekannte LLM-Fehlermodi (period_exception, DropContradictoryConsecutiveRequired)
├── Nachvollziehbarkeit
│   ├── reason-Feld bis in Fehler-/Verstoßmeldungen durchgereicht
│   └── Pro-Stufe-Diagnostik bei mehrstufigen Pipelines (KursstufeSolveResult, CombinedSolveResult)
└── Wartbarkeit / Erweiterbarkeit
    ├── Neue Schulformen als neue Module, nicht als neue Solver.vb-Verzweigungen
    └── Jede neue Fähigkeit zuerst gegen die volle Regressionssuite verifiziert
```

### 10.2 Qualitätsszenarien (Auszug)

| Szenario | Erwartetes Verhalten |
|---|---|
| Ein Constraint referenziert eine nicht existierende Klasse. | `Validation.ValidateEntities` meldet einen Fehler VOR jedem Solve-Versuch; `Solver.BuildModel` wirft eine `ArgumentException`, statt einen unvollständigen Plan als `Optimal` zu melden. |
| Ein weiches (`should`) `teacher_availability`-Constraint ist mit den übrigen Regeln unvereinbar. | Der Solver meldet `Optimal` statt `Infeasible`; `Verifier.VerifyScheduleDetailed(...).KannViolations` weist genau diesen einen Constraint aus, `MussViolations` bleibt leer. |
| Zwei Solve-Aufrufe mit identischem `data`, `seed` und `numWorkers:=1`. | Identisches `Schedule`-Ergebnis (byte-/wertgleich). |
| Ein 30-Klassen/75-Lehrer-Realmaßstab-Szenario wird mit der vollen 6-Kriterien-`SolveTop`-Zielfunktion bei `numWorkers:=1` gelöst. | Mit `useStagedHints:=True` (Default) wird innerhalb eines begrenzten Zeitbudgets (~20 Min) mindestens eine Lösung gefunden - ohne Staging fand dieselbe Konfiguration in Tests 30 Minuten lang keine. |
| Die LLM-Extraktion läuft dreimal ohne festen Seed auf demselben Freitext-Szenario. | Ergebnis wird in `RobustnessRunner`-Ergebnisdateien dokumentiert; ein über mehrere Läufe wiederkehrender (nicht nur einmaliger) Fehlermodus wird entweder durch Instruktions-Schärfung oder deterministische Nachbearbeitung behoben. |
| Ein Lehrer/Raum-Name ist sowohl in der Sek-I- als auch in der Kursstufen-Entity-Liste vorhanden. | `CombinedSchool.SolveCombinedSchool` verhindert in BEIDEN unterstützten Lösungsreihenfolgen nachweislich eine Doppelbelegung dieses Namens (siehe `CombinedSchoolTests.vb`). |
| Ein `klassen`-Lauf findet Varianten mit weichen Regelverletzungen. | `KlassenbildungQuality.Bewerte` zählt jede Verletzung unabhängig nach; weicht ein Maß vom Solver-Ergebnis ab, meldet der Lauf FAIL statt stillschweigend den Solver-Wert zu übernehmen. Im Beispiel A entspricht der Zielwert 1001 exakt der Hand-Vorhersage (1 unvermeidbarer Prio-3-Überlauf + 1 Prio-1-Split). |
| Der Klassenbildungs-Viewer bewertet eine per Drag & Drop veränderte Zuordnung. | Die JS-Live-Bewertung reproduziert die exportierten VB-Chips (Status UND Texte) zeichengleich - seit Stufe E als stehender Test in `TimetableViewer.Tests`, nicht mehr nur als einmalige Interaktionsprüfung (siehe 8.10). |

## 11. Risiken und technische Schulden

| Risiko / Schuld | Status |
|---|---|
| **`kurswahl`-LLM-Extraktion nie live gegen Qwen verifiziert.** Der deterministische Kursstufen-Kern (Kursblockung/Schienenraster/Raumzuordnung) ist vollständig getestet, der LLM-Typ selbst hatte in der Umsetzungssession keinen Ollama-Zugriff. | Offen, explizit dokumentiert in `docs/phase2-11-kursstufe.md`. |
| **Raum-Restrisiko der SekIFirst-Kombinationsrichtung.** Ist ein geteilter Spezialraum zu genau dem aus Stufe B gepinnten Slot durch Sek I belegt, wird Stufe C ohne Ausweichmöglichkeit `Infeasible`. Strukturelle Grenze der 3-Stufen-Pipeline, kein Bug. | Akzeptiert, durch einen dedizierten Test demonstriert statt verschwiegen (`docs/phase2-13-combined-school.md`). |
| **`SolveTop` bei kleinen/mittleren Szenarien mit Staging reproduzierbar langsamer** als ohne (Overhead der Stufe-1-Vorlösung übersteigt den Nutzen, wenn eine erste Lösung ohnehin leicht zu finden ist). | Bekannt, `useStagedHints:=False` bleibt für solche Fälle die schnellere Wahl trotz `:=True`-Default; keine größenabhängige Auto-Wahl implementiert (`docs/phase2-12-staged-hints.md`). |
| **Muss/Kann-Priorität ("should") ist eine subjektive Sprecher-Einschätzung**, kein prüfbarer Fakt - es gibt keine deterministische Verifikation von `priority_accuracy` wie z.B. bei `block_length`. | Dokumentierte, akzeptierte Grenze (`docs/phase2-robustness-report.md`, Phase-2.6-Abschnitt). |
| **`reason` ist eine Paraphrase, kein Verbatim-Zitat mit Zeichen-Offset** - eine exakte GUI-Textstellen-Markierung ist damit nicht möglich. | Als möglicher Umfang einer künftigen Phase vermerkt, nicht umgesetzt. |
| **Solver.vb ist mit ~1400 Zeilen das mit Abstand größte Modul.** | Bewusst in Kauf genommen (alle 13 klassenbasierten Constraint-Typen an einem Ort, direkt neben der Modellkonstruktion, die sie konsumiert) statt künstlich aufgeteilt - abgemildert durch die konsequente Auslagerung neuer, orthogonaler Konzepte (`ScheduleQuality.vb`, `SolveTopObjective.vb`, `Kursblockung.vb` etc.) in eigene Module. |
| **`BestObjectiveBound` bleibt bei größeren Realmaßstab-Szenarien (`bw-grundschule-beispiel`) auch nach der Big-M-freien `ClassGaps`/`TeacherGaps`-Kodierung (Phase 2.25-Nachtrag-2) deutlich hinter `ObjectiveValue` zurück** (~74.6% Lücke bei 120s/`numWorkers:=4`, gegenüber 99.7% vorher) - CP-SAT erreicht `Feasible`, nicht bewiesen `Optimal`. Die Kodierungs-Schwäche ist behoben, eine zugrundeliegende LP-Relaxations-Schwäche des Gesamtmodells bleibt bestehen. | Teilweise behoben, ehrlich als nicht vollständig geschlossen dokumentiert (`docs/phase2-25-stagnation-heuristik.md` Nachtrag 2). `stagnation_timeout_s`/`per_solve_time_limit_s`/`max_solutions` mildern die praktische Auswirkung (mehrere schnell gefundene, gut bewertete `Feasible`-Alternativen statt einer einzelnen langen Suche). `TeacherLoadVariance` (weiterhin Sentinel-basiert) bleibt als möglicher nächster Untersuchungskandidat offen. |
| **Viewer-JS-Formel-Duplikate können vom VB-Kern divergieren**, wenn eine Formel nur auf einer Seite geändert wird. | Akzeptiertes, aktiv verifiziertes Risiko: Chromium-Interaktionstests prüfen die Duplikate zeichengleich gegen den VB-Export (siehe 8.10); der nächste CLI-Lauf bleibt in jedem Fall die Ground Truth, ein divergenter Viewer kann also fehlleiten, aber kein falsches Endergebnis erzeugen. |
| **Klassenbildung: Konfliktkern-Analyse (Plan K6) und Re-Solve aus dem Viewer (UI-Konzept U5) offen.** Bei kollidierenden harten Regeln/Fixierungen meldet der Lauf nur Infeasible ohne Benennung des minimalen Konfliktkerns; der Viewer-Loop läuft über YAML-Export + erneuten CLI-Lauf. | Offen, in `docs/klassenbildung-plan.md` bzw. `docs/klassenbildung-ui-konzept.md` als nächste Ausbaustufen beschrieben; das UI ist so geschnitten, dass U5 nur den Export-Teil ersetzt. |
| **`klassen`-Läufe sind trotz festem Seed nicht run-zu-run-stabil** (Wandzeit-Limits beeinflussen, welche Varianten im ε-Band gefunden werden - beobachtet: Konsens-Kern 27 vs. 54 von 100 bei identischem Input). | Bekannt und in 6.9 dokumentiert; die Zielwerte akzeptierter Varianten liegen stets im ε-Band, der Konsens-Kern ist als Arbeitshilfe (Bulk-Fixierung), nicht als stabile Kennzahl zu lesen. |
| **Ein abgebrochenes `SolveTop` kann ein LEERES `Solutions` liefern** (Abbruch während der lexikographischen Vorphase, bevor die erste Lösung existiert) - ununterscheidbar von "nichts gefunden", wenn ein Aufrufer nur die Listenlänge prüft. | Bewusste Semantik, in 8.11 und am Enum-Wert `MultiSolveStopReason.Cancelled` dokumentiert: `StopReason` unterscheidet den Fall eindeutig von `SearchSpaceExhausted`. Die GUI muss den Leerfall behandeln. |
| **Passwort einer Projektdatei vergessen = Daten unwiederbringlich verloren.** Es gibt bewusst keinen Recovery-Schlüssel und keine Hintertür (8.12). | Entschieden und akzeptiert - die Kehrseite echter Verschlüsselung; ein zweiter Schlüsselweg wäre selbst wieder eine Angriffsfläche. Abmilderung ist organisatorisch (Handbuch: Passwort in den Schultresor, "Sicherungskopie erstellen" prominent). Schließt offenen Punkt 1 des Datenhaltungskonzepts. |
| **GUI (Phase 3) noch nicht begonnen.** | Geplant, nachgelagert unter Windows als WPF + WebView2 (siehe 2, 7); das Datenhaltungskonzept inkl. DSGVO-Betrachtung liegt vor (`docs/gui-datenhaltung-konzept.md`). Der Kern ist GUI-unabhängig entworfen (siehe 4, 7) und seit 8.11 zusätzlich abbrechbar und beobachtbar - abbrechbar und beobachtbar. Der Unterbau der Stufen A-C ist umgesetzt (`docs/gui-implementierungsplan.md`): YAML in beide Richtungen, Pipeline als I/O-freier Dienst, verschlüsselte Projektablage. Offen ist die Oberfläche selbst. |

## 12. Glossar

| Begriff | Bedeutung |
|---|---|
| **Muss / Kann (`must`/`should`)** | Priorität eines Constraints: `must` (Default) ist eine harte, nie verletzbare Regel; `should` ist eine weiche Präferenz, die der Solver verletzen darf, wenn nötig, um überhaupt einen Plan zu finden. |
| **Session** | Eine planbare Unterrichtseinheit (Klasse, Fach, Lehrer) - abgeleitet aus `teacher_subject_assignment`; DIE Grundeinheit, für die pro Tag/Periode eine `Lesson`-Entscheidungsvariable existiert. |
| **Lesson-Variable** | Eine boolesche CP-SAT-Variable "findet Session S am Tag D in Periode P statt?". |
| **Kann-Verstoß (Kann-Violation)** | Ein tatsächlich verletztes `should`-Constraint im gefundenen Plan. |
| **Springstunde / Gap** | Eine freie Periode zwischen zwei besetzten Perioden desselben Tages (unerwünscht, Teil der Bewertungskriterien). |
| **Randstunde / Edge Period** | Erste Stunde des Tages oder Nachmittagsstunde (Periode ≥ 7) - beides gilt als mild störend. |
| **Wahlprofil** | Eine Gruppe von Schülern mit identischer Kurswahl (Kursstufe) - trägt eine Schüleranzahl statt einzelner Schülernamen. |
| **Kurs** | Ein einzelnes Kursangebot der Kursstufe (Fach, Lehrer, Kursart LK/GK, Wochenstunden). |
| **Schiene** | Ein paralleler Zeitblock, dem mehrere zeitgleiche Kurse zugeordnet werden (Schienenmodell) - Kernidee: Kurse EINER Schiene laufen synchron, Kurse VERSCHIEDENER Schienen (ohne gemeinsames Wahlprofil) können parallel laufen. |
| **Kursblockung** | Stufe A der Kursstufen-Pipeline: Zuordnung Kurs → Schiene. |
| **Schienenraster** | Stufe B: Zuordnung Schiene → Tag/Periode. |
| **Raumzuordnung** (Kursstufe) | Stufe C: Zuordnung Kurs → Raum, bei bereits aus Stufe B feststehendem Tag/Periode. |
| **Synthetisches Szenario** | Ein intern konstruiertes `entities`/`constraints`-JSON, das eine neue Planungsstufe auf die bestehende `Solver.Solve()`-Pipeline abbildet, ohne diese zu verändern. |
| **CP-SAT** | Googles Constraint-Programming-/SAT-Solver (Teil von OR-Tools), das eigentliche Optimierungs-Backend. |
| **Verifier** | Der von der Solver-Logik unabhängige Nachprüfer eines gefundenen Plans. |
| **Lexikografische Stufe** | Ein Qualitätskriterium, das in `SolveTop` (Default seit Code-Review P2) einzeln optimiert und dann als hartes Band (`<= opt + lexTolerance`) fixiert wird, bevor die nächste Stufe optimiert - macht Prioritätsordnung explizit statt gewichtet verrechnet. |
| **Dichte-Kriterium (`OccupiedDensity`)** | Zählt unbelegte Slots innerhalb von should-`occupied_window`-Fenstern - Kriteriums-Semantik statt Kann-BoolVar-Batterie (Code-Review P1). |
| **Rhythmisierung / `subject_period_window`** | Fach-bezogene Zeitfenster-Regel ("Kernfächer vormittags, AGs Mo-Do nachmittags"): must = hartes Verbot außerhalb des Fensters, should = Zählung im `SubjectWindow`-Kriterium. |
| **Äquivalenzklasse (Lehrkräfte)** | Menge von Lehrkräften mit identischer Voll-Pipeline-Signatur (Profil, Qualifikationen, feste Zuordnungen, normalisierte Hand-Constraints) - untereinander tauschbar, Quelle von Symmetrie-Duplikaten; im Viewer als ⇄ sichtbar. |
| **Zuteilung (Mehr-Zuteilungs-Modus)** | Ein Stufe-1-Ergebnis (Lehrer→Klasse/Fach), auf dessen Basis Stufe 2 eigene Stundenplan-Lösungen rechnet; `ToStundentafelJsonMulti` sortiert die Lösungen aller Zuteilungen global. |
| **Klassenbildung (Stufe 0)** | Einteilung eines Jahrgangs in Parallelklassen nach pädagogischen Regeln (Gruppen, Balance, Wünsche, Fixierungen) - eigenständiges CP-SAT-Modul, menschen-moderiert. |
| **Ampel-Chip** | Status eines Kriteriums für EIN betroffenes Kind der Klassenbildung: grün (erfüllt), gelb (erfüllt, aber knapp), rot (weiche Regel verletzt); Kinder ohne Chips sind "frei". |
| **Konsens-Kern** | Kinder, die in ALLEN gefundenen Klassenbildungs-Varianten identisch zugeordnet sind - Kandidaten für eine Bulk-Fixierung, keine stabile Kennzahl (siehe 11). |
| **Fixierung / Pin** | Harte Vorab-Zuordnung eines Kindes zu einer Klasse (bzw. Ausschluss, `nicht_klasse`) - per YAML als Solver-Input, im Viewer als F1/F2-Pin mit YAML-Export für den nächsten Lauf. |
| **Härtung (F3/F5)** | Viewer-Export-Direktive, einen weichen Wunsch bzw. eine weiche Gruppe im nächsten Lauf `modus: hard` zu stellen - ändert bewusst nicht die Live-Bewertung. |
