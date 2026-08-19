# Architekturdokumentation TimetableCore (arc42)

Diese Dokumentation folgt der [arc42](https://arc42.de)-Gliederung und beschreibt
die .NET/VB.NET-Lösung unter `timetable-dotnet/` - den Nachfolger des
eingefrorenen Python-Prototyps unter `timetable/`. Stand: Phase 2.14
abgeschlossen (siehe `docs/phase2-*.md` für die Entstehungsgeschichte der
einzelnen Fähigkeiten).

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
| Zielplattform GUI: **Windows** (Mac/Linux optional/später) | GUI-Technologie WinForms geplant (Phase 3, noch nicht begonnen); der Kern selbst bleibt plattformneutral. |
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
| (Nachgelagert) WinForms-GUI | GUI → TimetableCore | Direkter .NET-Projektverweis (`ProjectReference`), keine Netzwerkgrenze |
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
├── RobustnessRunner/         Konsolenprogramm: LLM-Wiederholungsstudien
└── (nachgelagert) TimetableGui/   WinForms-GUI, referenziert TimetableCore direkt
```

`TimetableCore` hat keine Abhängigkeit auf `TimetableCore.Tests` oder
`RobustnessRunner`; `RobustnessRunner` bindet einzelne Testfixtures per
`<Compile Include>` ein, um MSTest als Abhängigkeit zu vermeiden.

### 5.2 Ebene 2 - Whitebox `TimetableCore`

| Modul | Verantwortung | Abhängig von |
|---|---|---|
| **Models.vb** | `JsonHelpers` (Zugriff auf das rohe `System.Text.Json.Nodes`-JSON, Python-`dict.get`-artig statt starrem Typmodell - minimiert Übersetzungsrisiko beim Portieren), Muss/Kann-Konstanten, Kursstufe-Zugriffshelfer (`GetKurse`/`GetSchienen`). | - |
| **Validation.vb** | Deterministische Vorab-Prüfung: harte Cross-Reference-Fehler (unbekannte Entity-Referenz) vs. weiche Coverage-Warnungen (fehlende `no_overlap`-Regel). `ValidateKursstufeEntities` erweitert das um Kurs-/Schienen-/Wahlprofil-Konsistenz. | Models.vb |
| **Solver.vb** | CP-SAT-Modellbau (`BuildCoreModel`/`BuildModel`/`ApplyConstraints`/`AddBlockConstraint`) und -Lösung (`Solve`, `SolveTop`, `SolveKursstufe`). Größtes Modul (825 Zeilen) - bewusst der einzige Ort, der `model.Add(...)` für die 9 klassenbasierten Constraint-Typen aufruft. | Models.vb, Validation.vb, Verifier.vb (für `SolveTop`s Kann-Neuberechnung), ScheduleQuality.vb, SolveTopObjective.vb, Kursblockung.vb, Schienenraster.vb, Raumzuordnung.vb |
| **Verifier.vb** | Unabhängiger Solution-Checker - teilt bewusst KEINEN Code mit Solver.vb (siehe 8.2). `VerifySchedule`/`VerifyScheduleDetailed` (Muss/Kann getrennt), `VerifyKursblockung` (Stufe-A-Ergebnis unabhängig re-prüfen). | Models.vb |
| **Formatting.vb** | Rohes `ScheduleEntry`-Ergebnis → Klassen-/Lehrer-/Wahlprofil-Raster (`GridCell`), ASCII-Tabellen, JSON-Export. Reine Präsentationsschicht, keine GUI-Abhängigkeit. | Models.vb |
| **LlmExtraction.vb** | Freitext → strukturierte Constraints via Ollama/Qwen. Ein Call pro Constraint-Typ mit eigenem JSON-Schema; `period_exception` wird deterministisch zu `forbidden_slot`-Einträgen expandiert (`ExpandPeriodException`); `DropContradictoryConsecutiveRequired` als deterministisches Sicherheitsnetz gegen unmögliche Block-Kombinationen. | Models.vb |
| **ScheduleQuality.vb** | Post-hoc-Bewertungsschema für Kandidaten-Stundenpläne (Kann-Verstöße, Lücken, Randstunden, Nachmittags-Tage, Tagesausgewogenheit) - unabhängig von der CP-SAT-Modellierung, dient als "Wahrheit" für `SolveTop`s Endsortierung. | Models.vb |
| **SolveTopObjective.vb** | Baut dieselben Bewertungskriterien zusätzlich direkt ins CP-SAT-Modell (`Friend`, nur von `Solver.SolveTop` genutzt) - eine CP-SAT-freundliche Näherung (Spannweite statt echter Varianz), damit die Suche selbst dorthin gelenkt wird, statt nur die gefundenen Kandidaten hinterher zu sortieren. | Models.vb |
| **Kursblockung.vb** | Kursstufe Stufe A: Kurs→Schiene-Zuordnung (CP-SAT-Teilmodell, eigenständig, kein Tag/Periode-Bezug). | Models.vb |
| **Schienenraster.vb** | Kursstufe Stufe B: Schiene→Tag/Periode. Konstruiert ein synthetisches Szenario und ruft `Solver.Solve()` unverändert auf. | Models.vb |
| **Raumzuordnung.vb** | Kursstufe Stufe C: Kurs→Raum, mit aus Stufe B gepinntem Tag/Periode. Ebenfalls über ein synthetisches Szenario + `Solver.Solve()`. | Models.vb, Schienenraster.vb (für `SlotsForKurs`) |
| **CombinedSchool.vb** | Orchestriert einen gemeinsamen Solve von Sek-I- und Kursstufen-Hälfte, sodass ein geteilter Lehrer-/Raum-Name nicht doppelt belegt wird - ohne `Solver.vb` selbst zu ändern. | Models.vb, Solver.vb, Kursblockung.vb, Schienenraster.vb, Raumzuordnung.vb |
| **Stammdaten.vb** | Typisiertes Domänenmodell (Klassenstufe/Fach/Klasse/Raum/Lehrer/FachLehrerZuordnung) für dauerhaft verwaltete Schul-Stammdaten (Phase 2.15) - bewusst NICHT das rohe `JsonObject`-Muster der Constraints (siehe 8.7). Laden/Speichern als JSON, `BuildEntitiesFragment` projiziert in das bestehende `entities`-Format. | - |
| **StammdatenValidation.vb** | Cross-Reference-Prüfung für Stammdaten (unbekannte Klassenstufen-/Lehrer-/Fach-Referenzen, unplausible Deputate), gleiche "Fail-Fast VOR jedem Solve"-Philosophie wie `Validation.vb`. | Stammdaten.vb |
| **Lehrereinsatzplanung.vb** | Neue, vorgeschaltete Planungsstufe (Phase 2.15): verteilt Lehrkräfte IDEAL auf Klassen/Fächer (Qualifikation hart, Deputat-Korridor/Klassenlehrer/Präferenzen weich) - ein eigenständiges CP-SAT-Teilmodell ohne Tag/Periode-Bezug. `BuildAssignmentConstraints` übersetzt das Ergebnis in `teacher_subject_assignment`/`weekly_hours`/`no_overlap`-Constraints, die unverändert an `Solver.Solve` gehen (`no_overlap` ist zwingend - siehe `docs/phase2-15-lehrereinsatzplanung.md`s Phase-2.16-Nachtrag zu einem live gefundenen Bug ohne diese Regel). Phase 2.17 ergänzt sieben weitere weiche/harte Ziele (Kontinuität über Jahre, Fachfremd-Vermeidung, Max. Klassen/Fächer pro Lehrer, Teilzeit-Tage-Kohärenz als harter Vorfilter, Klassenlehrer-Tandem-Balance, Springerreserve, faire Verteilung unbeliebter Fächer) - siehe `docs/phase2-15-lehrereinsatzplanung.md`s Nachtrag 5. | Stammdaten.vb |

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
   übersetzt jeden der übrigen 8 Constraint-Typen in `model.Add(...)`-Aufrufe.
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
3. **Stufe 2**: `SolveTopObjective.ApplyQualityObjective` setzt die volle
   gewichtete Zielfunktion; eine Schleife löst wiederholt dasselbe
   `CpModel` (jeweils neuer `CpSolver`), sperrt jede gefundene Lösung
   per `BlockSolution` (No-Good-`AddBoolOr`) gegen Wiederholung, reicht
   die gefundene Belegung optional als Hint an die nächste Iteration
   weiter, und bricht ab bei `maxSolutions`, `totalTimeLimitS` oder
   `Infeasible` (Suchraum erschöpft).
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

## 7. Verteilungssicht

| Umgebung | Inhalt | Status |
|---|---|---|
| Linux-Sandbox (aktuelle Entwicklungsumgebung) | `TimetableCore`, `TimetableCore.Tests`, `RobustnessRunner`, lokaler Ollama-Server | Aktiv, vollständig lauffähig (`dotnet test`, `dotnet run --project RobustnessRunner`) |
| Windows-Zielumgebung (nachgelagert) | `TimetableGui` (WinForms) + `TimetableCore` (per `ProjectReference`) + lokaler Ollama-Server | Noch nicht begonnen (Phase 3) |

`Google.OrTools` läuft nativ unter beiden Plattformen (`Google.OrTools.runtime.linux-x64`
bzw. `...win-x64` als transitive NuGet-Abhängigkeit) - derselbe
`TimetableCore.vbproj`-Build funktioniert unverändert auf beiden. Ollama
läuft in beiden Umgebungen als separater, lokal zu installierender
Prozess (`http://127.0.0.1:11434`), keine Cloud-Abhängigkeit.

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
In-Modell-Näherung) und bei `Verifier.VerifyKursblockung` (unabhängig von
`Kursblockung.vb`s CP-SAT-Constraints).

### 8.3 Muss/Kann-Priorität (`priority: "must"|"should"`)

Fünf von neun klassenbasierten Constraint-Typen (`teacher_availability`,
`forbidden_slot`, `room_requirement`, `consecutive_required`, sowie der
`max_per_day`-Teil von `weekly_hours`) können als weich (`"should"`)
markiert werden. Ein weicher Constraint bekommt eine gemeinsame
Verletzungs-`BoolVar`; die eigentliche Anforderung wird über
`.OnlyEnforceIf(violated.Not())` daran gekoppelt, die Zielfunktion
minimiert die Summe aller Verletzungs-Variablen (binär gewichtet - eine
verletzte Regel zählt 1, unabhängig davon wie viele Slots sie betrifft).
Strukturell zwingende Typen (`no_overlap`, `shared_resource_conflict`,
`teacher_subject_assignment`, `hours_per_week`) bleiben immer hart - sie
sind physisch/strukturell notwendig (siehe
`json-constraints-reference.md` für die vollständige Liste). Details und
Beispiele: `json-constraints-reference.md`.

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
bleiben stabil und weiterhin nur für die 9 klassenbasierten
Constraint-Typen zuständig - jede neue Schulform ist ein neues Modul,
keine neue `Case`-Verzweigung im Kern.

### 8.7 Wire-Format-Parität

`System.Text.Json.Nodes` (`JsonObject`/`JsonArray`) statt eines starren
Klassenmodells - bewusst dieselbe "dict-artige" Flexibilität wie Pythons
`dict`, um das Übersetzungsrisiko beim Portieren zu minimieren.
`JsonHelpers` in `Models.vb` kapselt den Zugriff (`GetString`, `GetInt`,
`AsStringList` - Letzteres normalisiert Felder, die sowohl ein einzelner
String als auch eine Liste sein dürfen). Ein typisiertes Modell (für
GUI-Databinding) kann später darüber gelegt werden, ohne diesen Kern
anzufassen.

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

## 11. Risiken und technische Schulden

| Risiko / Schuld | Status |
|---|---|
| **`kurswahl`-LLM-Extraktion nie live gegen Qwen verifiziert.** Der deterministische Kursstufen-Kern (Kursblockung/Schienenraster/Raumzuordnung) ist vollständig getestet, der LLM-Typ selbst hatte in der Umsetzungssession keinen Ollama-Zugriff. | Offen, explizit dokumentiert in `docs/phase2-11-kursstufe.md`. |
| **Raum-Restrisiko der SekIFirst-Kombinationsrichtung.** Ist ein geteilter Spezialraum zu genau dem aus Stufe B gepinnten Slot durch Sek I belegt, wird Stufe C ohne Ausweichmöglichkeit `Infeasible`. Strukturelle Grenze der 3-Stufen-Pipeline, kein Bug. | Akzeptiert, durch einen dedizierten Test demonstriert statt verschwiegen (`docs/phase2-13-combined-school.md`). |
| **`SolveTop` bei kleinen/mittleren Szenarien mit Staging reproduzierbar langsamer** als ohne (Overhead der Stufe-1-Vorlösung übersteigt den Nutzen, wenn eine erste Lösung ohnehin leicht zu finden ist). | Bekannt, `useStagedHints:=False` bleibt für solche Fälle die schnellere Wahl trotz `:=True`-Default; keine größenabhängige Auto-Wahl implementiert (`docs/phase2-12-staged-hints.md`). |
| **Muss/Kann-Priorität ("should") ist eine subjektive Sprecher-Einschätzung**, kein prüfbarer Fakt - es gibt keine deterministische Verifikation von `priority_accuracy` wie z.B. bei `block_length`. | Dokumentierte, akzeptierte Grenze (`docs/phase2-robustness-report.md`, Phase-2.6-Abschnitt). |
| **`reason` ist eine Paraphrase, kein Verbatim-Zitat mit Zeichen-Offset** - eine exakte GUI-Textstellen-Markierung ist damit nicht möglich. | Als möglicher Umfang einer künftigen Phase vermerkt, nicht umgesetzt. |
| **Solver.vb ist mit 825 Zeilen das mit Abstand größte Modul.** | Bewusst in Kauf genommen (alle 9 klassenbasierten Constraint-Typen an einem Ort, direkt neben der Modellkonstruktion, die sie konsumiert) statt künstlich aufgeteilt - abgemildert durch die konsequente Auslagerung neuer, orthogonaler Konzepte (`ScheduleQuality.vb`, `SolveTopObjective.vb`, `Kursblockung.vb` etc.) in eigene Module. |
| **GUI (Phase 3) noch nicht begonnen.** | Geplant, nachgelagert unter Windows; der Kern ist bereits GUI-unabhängig entworfen (siehe 4, 7). |

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
