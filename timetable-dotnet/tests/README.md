# Code-freie Schul-Testfälle

Dieses Verzeichnis enthält Testfälle für den `TimetableCore`-Kern, die
**ohne VB.NET-Code** angelegt und gepflegt werden können - direkt in der
GitHub-Weboberfläche editierbar (YAML-Dateien). Jede Schule bekommt ein
eigenes Unterverzeichnis:

```
tests/
  README.md                        (diese Datei)
  <schule>/
    input/
      stammdaten.yaml               (Pflicht) - Klassenstufen/Klassen/Räume/
                                     Lehrkräfte/Fächer/Fach-Lehrer-Zuordnung
      constraints.yaml               (optional) - zusätzliche, handverfasste
                                     Stundenplan-Regeln der 2. Solver-Stufe
      config.yaml                    (optional) - Solve-Parameter
    output/
      lehrerzuteilung.md             (generiert) - wer unterrichtet was,
                                     Klassenlehrer je Klasse
      stundenplan.md                  (generiert) - fertiger Stundenplan
      stundenplan.json                (generiert) - ALLE von Solver.SolveTop
                                     gefundenen Loesungen als JSON (Phase 2.21)
      stundentafel.html               (generiert) - interaktive "Stundentafel"-
                                     Gesamtuebersicht (Phase 2.21), siehe unten
```

`output/` wird bei jedem Lauf **komplett neu geschrieben** - nicht von Hand
bearbeiten. Der Sinn eines committeten `output/`-Stands ist, Änderungen an
`input/` per Pull-Request-Diff sichtbar zu machen (z.B. "diese
Stammdaten-Änderung ändert den Stundenplan von Klasse 4b so").

## Architektur-Hintergrund (kurz)

Die Pipeline hat zwei Stufen (siehe `docs/arc42-architecture.md` Abschnitt
8.6/8.7 für Details):

1. **Stammdaten → Lehrereinsatzplanung** (automatisch): aus
   `stammdaten.yaml` leitet `Lehrereinsatzplanung.SolveLehrereinsatz`
   selbst ab, WER WAS unterrichtet (Deputat-Korridor, Klassenlehrer-
   Bündelung, Kontinuität, Fachfremd-Vermeidung, ... - siehe
   `docs/phase2-15-lehrereinsatzplanung.md`).
2. **Constraints → Solver.SolveTop** (automatisch + optional handverfasst):
   das Ergebnis von Stufe 1 wird deterministisch in
   `teacher_subject_assignment`/`weekly_hours`/`consecutive_required`/
   `no_overlap`-Regeln übersetzt. `constraints.yaml` ergänzt NUR
   handverfasste Regeln, die Stufe 1 nicht abdeckt - `teacher_availability`,
   `forbidden_slot`, `room_requirement` (Raumbindung ist bislang NICHT aus
   den Stammdaten ableitbar, siehe `Raum`-Kopfkommentar in
   `TimetableCore/Stammdaten.vb`), und ad-hoc `consecutive_required`.
   **Nicht** hier hineinschreiben: `teacher_subject_assignment`/
   `weekly_hours.hours_per_week` - die kommen ausschließlich aus Stufe 1.

## `stammdaten.yaml`

Beschreibt die feste Grundausstattung der Schule: Klassenstufen, Klassen,
Räume, Lehrkräfte, Fächer je Klassenstufe und wer welches Fach
unterrichten darf. Beispiel:

```yaml
schul_name: Beispiel-Grundschule
bundesland: BW
schulart: Grundschule
tage: [Mo, Di, Mi, Do, Fr]
periods_per_day: 6
klassenstufen:
  - {nummer: 1, bezeichnung: "Klasse 1"}
klassen:
  - {name: 1a, klassenstufe: 1, schuelerzahl: 22, erlaubt_klassenlehrer_tandem: false}
faecher:
  - name: Deutsch
    block_length: 2
    unbeliebt: false
    klassenstufen:
      - {klassenstufe: 1, wochenstunden_soll: 6, max_pro_tag: 2}
raeume:
  - {name: Turnhalle1, typ: Turnhalle}
lehrkraefte:
  - name: Klassenlehrer-1
    deputat_sollstunden: 28
    anrechnungsstunden: 2
    springer_reserve_stunden: 0
    verfuegbare_tage: [Mo, Di, Mi]
    bevorzugte_klassenstufen: [1, 2]
    klassenlehrer_faehig: true
    max_klassen: 1
    max_faecher: 3
fach_lehrer_zuordnungen:
  - {lehrer_name: Klassenlehrer-1, fach_name: Deutsch, fachfremd: false}
```

Ein leer gelassener Wert (`block_length:` ohne Text) bedeutet in YAML
"nicht gesetzt" - identisch zu komplettem Weglassen des Feldes. Alle unten
als "optional" markierten Felder dürfen weggelassen werden und wirken sich
dann nicht auf die Planung aus.

### Attribute auf oberster Ebene

- `schul_name` - Name der Schule, rein informativ, taucht so in den
  Reports auf.
- `bundesland` - Bundesland-Kürzel (z.B. `BW`), rein informativ für die
  Stammdaten selbst; für die `new`-CLI entscheidet es, welches Curriculum-
  Template greift.
- `schulart` - Schulart als Freitext (z.B. `Grundschule`), rein
  informativ.
- `tage` - Liste der Wochentage, an denen überhaupt Unterricht stattfindet
  (z.B. `[Mo, Di, Mi, Do, Fr]`).
- `periods_per_day` - Anzahl der Unterrichtsperioden (Stunden) pro Tag.
- `klassenstufen` - Liste aller Klassenstufen der Schule (siehe unten).
- `klassen` - Liste aller Klassen der Schule (siehe unten).
- `faecher` - Liste aller Fächer der Schule (siehe unten).
- `raeume` - Liste aller Räume der Schule (siehe unten), optional (eine
  Schule ohne besondere Raumbindung braucht keine Räume).
- `lehrkraefte` - Liste aller Lehrkräfte der Schule (siehe unten).
- `fach_lehrer_zuordnungen` - Liste, welche Lehrkraft welches Fach
  unterrichten darf (siehe unten).
- `schueler` - optional, Liste aller Schüler der Schule (siehe unten) -
  nur nötig, wenn `gruppen` genutzt wird.
- `gruppen` - optional, Liste klassenunabhängiger Schülergruppen (siehe
  unten), z.B. für Religion ev./kath./Ethik.

### `klassenstufen[]`

- `nummer` - Nummer der Klassenstufe (z.B. `1` für "Klasse 1").
- `bezeichnung` - Anzeigename der Klassenstufe, rein informativ.

### `klassen[]`

- `name` - Name der Klasse (z.B. `1a`), muss eindeutig sein.
- `klassenstufe` - zu welcher Klassenstufen-Nummer die Klasse gehört.
- `schuelerzahl` - optional, Schülerzahl der Klasse, rein informativ.
- `erlaubt_klassenlehrer_tandem` - optional, Default `false`. Wenn `true`,
  dürfen für diese Klasse zwei Lehrkräfte gleichzeitig als Klassenlehrer
  aktiv sein (Tandem) statt wie sonst üblich nur eine einzige.

### `faecher[]`

- `name` - Fachname (z.B. `Deutsch`), muss eindeutig sein und wird von
  `fach_lehrer_zuordnungen` referenziert.
- `block_length` - optional. Wenn gesetzt, müssen die Wochenstunden dieses
  Fachs als zusammenhängender Block dieser Länge unterrichtet werden (z.B.
  `2` für eine Doppelstunde).
- `unbeliebt` - optional, Default `false`. Markiert ein im Kollegium
  unbeliebtes Fach, dessen Zuweisungen möglichst gleichmäßig auf alle
  dafür qualifizierten Lehrkräfte verteilt werden sollen.
- `klassenstufen` - Liste, in welchen Klassenstufen das Fach mit welchem
  Wochenstundenumfang geführt wird (siehe unten). Ein Fach ohne Eintrag
  für eine Klassenstufe wird dort nicht unterrichtet.

#### `faecher[].klassenstufen[]`

- `klassenstufe` - für welche Klassenstufen-Nummer dieser Eintrag gilt.
- `wochenstunden_soll` - wie viele Wochenstunden dieses Fach in dieser
  Klassenstufe hat.
- `max_pro_tag` - optional, Obergrenze, wie viele Stunden dieses Fachs an
  einem einzigen Tag für dieselbe Klasse stattfinden dürfen.

### `raeume[]`

- `name` - Raumname (z.B. `Turnhalle1`), referenzierbar in
  `room_requirement`-Regeln der `constraints.yaml`.
- `typ` - optional, Freitext-Kategorie des Raums (z.B. `Turnhalle`,
  `NaWi`), rein informativ/zur eigenen Orientierung.

### `lehrkraefte[]`

- `name` - Name der Lehrkraft, muss eindeutig sein.
- `deputat_sollstunden` - vertragliches Wochendeputat in Stunden.
- `anrechnungsstunden` - optional, Default `0`. Stunden, die für andere
  Aufgaben (z.B. eine Funktionsstelle) vom Deputat abgezogen werden, bevor
  der tatsächliche Unterrichts-Sollwert berechnet wird.
- `springer_reserve_stunden` - optional, Default `0`. Wie
  `anrechnungsstunden`, aber für bewusst freigehaltene Vertretungsreserve
  - senkt ebenfalls den Unterrichts-Sollwert, ohne dass die
  Nicht-Ausschöpfung als Problem gewertet wird.
- `verfuegbare_tage` - optional, Default alle Schultage (Vollzeit). Liste
  der Wochentage, an denen die Lehrkraft überhaupt im Haus ist (Teilzeit).
- `bevorzugte_klassenstufen` - optional, Default leer (keine Präferenz).
  Liste von Klassenstufen-Nummern, die die Lehrkraft bevorzugt
  unterrichten möchte.
- `klassenlehrer_faehig` - optional, Default `true`. Ob die Lehrkraft
  grundsätzlich als Klassenlehrer:in einer Klasse infrage kommt.
- `max_klassen` - optional, keine Grenze wenn weggelassen. Obergrenze, in
  wie vielen unterschiedlichen Klassen diese Lehrkraft eingesetzt werden
  soll.
- `max_faecher` - optional, keine Grenze wenn weggelassen. Wie
  `max_klassen`, aber für die Anzahl unterschiedlicher Fächer.

### `fach_lehrer_zuordnungen[]`

- `lehrer_name` - Name der Lehrkraft (muss in `lehrkraefte[]` existieren).
- `fach_name` - Name des Fachs (muss in `faecher[]` existieren). Nur hier
  gelistete (Lehrkraft, Fach)-Paare kommen für eine Zuweisung überhaupt in
  Frage.
- `fachfremd` - optional, Default `false`. Markiert diese Zuordnung als
  fachfremden Einsatz - die Lehrkraft bleibt einsetzbar, eine tatsächliche
  Zuweisung wird aber gegenüber einer regulär qualifizierten Lehrkraft
  benachteiligt.

### `schueler[]`

- `id` - pseudonyme ID der/des Schülers (z.B. `S-1a-01`), muss eindeutig
  sein. Bewusst kein Name-Feld (Datenschutz - fürs Scheduling wird kein
  Klarname gebraucht).
- `klasse` - Heimatklasse (muss in `klassen[]` existieren).

### `gruppen[]`

Eine klassenunabhängige Gruppe von Schülern - deckt z.B. Religion
ev./kath./Ethik, Fördergruppen oder Aufsichtsgruppen ab (alle strukturell
gleich: eine benannte Gruppe, eine Liste von Schüler-IDs).

- `name` - Name der Gruppe (z.B. `Religion-ev-Kl1`), muss eindeutig sein.
- `typ` - optional, Freitext-Kategorie zur eigenen Einordnung (z.B.
  `Fachgruppe`, `Foerderung`, `Aufsicht`).
- `mitglieder_schueler_ids` - Liste von Schüler-IDs (müssen in
  `schueler[]` existieren).
- `fach_name` - optional (Phase 2.20): welches Fach diese Gruppe
  unterrichtet (muss in `faecher[]` existieren). Erst mit diesem Feld
  gesetzt bekommt eine Gruppe eine Solver-Wirkung.
- `klassenstufe` - optional (Phase 2.20): die Klassenstufe der Gruppe
  (nötig, da eine Gruppe mehrere echte Klassen umspannen kann und daher
  keine einzelne `Klasse.klassenstufe` hat).
- `parallelverbund` - optional (Phase 2.20): Gruppen mit demselben Wert
  bilden gemeinsam eine synchron zu planende Partition (z.B.
  `Religion-Ethik-Kl1` für die drei Religion-ev-/Religion-kath-/
  Ethik-Kl1-Gruppen) - sie werden über eine neue `parallel_group`-
  Constraint (siehe `docs/json-constraints-reference.md` Abschnitt 5)
  gezwungen, immer zur exakt selben Zeit stattzufinden.

**Solver-Wirkung (Phase 2.20):** ist `fach_name`/`klassenstufe`/
`parallelverbund` gesetzt, plant `Lehrereinsatzplanung.SolveLehrereinsatz`
für diese Gruppe automatisch EINE Lehrkraft (statt einer pro echter
Klasse), und `BuildAssignmentConstraints` emittiert eine `parallel_group`-
Regel, die alle Gruppen desselben Parallelverbunds im Stundenplan
synchronisiert - der gerenderte `output/stundenplan.md` zeigt sie dann als
kombinierte Zelle (z.B. `Ethik / Religion-ev / Religion-kath`). Ohne diese
drei Felder bleibt eine Gruppe weiterhin reine, wirkungslose Stammdatum
(Phase-2.19-Verhalten). `StammdatenValidation` prüft dabei hart: alle
Gruppen eines Parallelverbunds brauchen `fach_name` (paarweise
verschieden), dieselbe `klassenstufe`, UND dasselbe `wochenstunden_soll`/
`block_length` für diese Klassenstufe - sonst wäre das CP-SAT-Modell
strukturell unlösbar. Details siehe
`docs/phase2-20-parallelgruppen.md`.

Vor jedem Lauf prüft `StammdatenValidation.ValidateStammdaten` die Datei
auf Konsistenz (unbekannte Klassenstufen-Referenzen, Fach ohne
qualifizierte Lehrkraft, Deputat-Unsinn, Teilzeit-Tage-Kohärenz, unbekannte
Schüler-/Gruppen-Referenzen, doppelte Schüler-IDs, ...) - Fehler werden mit
Datei-/Objektbezug in `output/lehrerzuteilung.md` gemeldet.

## `constraints.yaml`

YAML-Liste von Constraint-Objekten, ein Mapping pro Regel - identisches
Feldschema wie in `docs/json-constraints-reference.md`, nur in YAML statt
JSON geschrieben:

```yaml
- type: teacher_availability
  teacher: Religionslehrer-1
  available_days: [Mo, Di, Mi, Do]
  reason: Teilzeit, freitags nicht im Haus
- type: room_requirement
  class: 1a
  subject: Sport
  allowed_rooms: [Turnhalle1, Turnhalle2]
- type: forbidden_slot
  scope: class
  entity: 1a
  day: Fr
  period: 6
  priority: should
```

**Achtung bei Zahlen:** ein unquotierter Wert wie `period: 6` wird als
Zahl interpretiert, ein in Anführungszeichen gesetzter Wert wie
`period: "6"` ebenfalls (auf dieser Verarbeitungsstufe ist das nicht mehr
unterscheidbar) - für dieses Constraint-Schema unproblematisch, da alle
Zahlenfelder (`period`, `hours_per_week`, `max_per_day`, `block_length`)
tatsächlich immer Zahlen sein sollen.

Eine leere oder fehlende `constraints.yaml` ist gültig (keine
zusätzlichen Regeln).

## `config.yaml` (optional)

```yaml
deputat_toleranz_stunden: 2.0   # Default 2.0
lehrereinsatz_time_limit_s: 30.0
solve_time_limit_s: 30.0
seed: 42
num_workers: 1   # Default: Anzahl CPU-Kerne - 1 (mindestens 1)
max_solutions: 1   # Default 1 (Phase 2.21) - siehe unten
```

Fehlt die Datei komplett, gelten diese Defaults unverändert. `solve_time_limit_s`
begrenzt sowohl `Solver.SolveTop`s Gesamt- als auch dessen Einzel-Solve-
Zeitbudget (siehe unten).

**Für eine möglichst optimale EINZELNE Lösung** ist NICHT `max_solutions`
der relevante Hebel - jede weitere Solve-Iteration optimiert ohnehin gegen
dieselbe Zielfunktion und kann daher nur eine gleich gute, nie eine
bessere Alternative finden. Entscheidend ist stattdessen ein ausreichend
hohes `solve_time_limit_s`: nur mit genug Zeit kann CP-SAT den gefundenen
Plan tatsächlich als `Optimal` BEWEISEN statt ihn nach Zeitablauf nur als
`Feasible` zurückzugeben. Das tatsächlich erreichte Ergebnis steht im
`CP-SAT-Status`-Feld der `**Status:**`-Zeile in `output/stundenplan.md` -
bei `Feasible` erscheint dort zusätzlich ein Hinweis, `solve_time_limit_s`
zu erhöhen.

**`max_solutions`** (Default 1) steuert stattdessen, wie viele
VERGLEICHBARE Alternativ-Lösungen berechnet und in `output/stundenplan.json`
+ `output/stundentafel.html` exportiert werden (siehe Abschnitt
"Stundentafel-Visualisierung" unten) - der beste Kandidat bleibt weiterhin
allein maßgeblich für `lehrerzuteilung.md`/`stundenplan.md`. Ein höherer
Wert verlängert die Gesamtlaufzeit NICHT über `solve_time_limit_s` hinaus
(`Solver.SolveTop` prüft das verbleibende Zeitbudget vor jeder weiteren
Iteration) - er kann aber dazu führen, dass die ERSTE Lösung bereits das
gesamte Budget aufbraucht (falls sie nicht schnell als `Optimal` bewiesen
werden kann) und dadurch trotz höherem `max_solutions` nur eine einzige
Lösung gefunden wird, bevor `solve_time_limit_s` abläuft - das ist kein
Fehler, sondern zeigt lediglich, dass für weitere Alternativen zusätzlich
`solve_time_limit_s` erhöht werden müsste.

## CLI: Grundgerüst per Template erzeugen

```bash
cd timetable-dotnet   # Arbeitsverzeichnis wichtig - "tests/" wird relativ dazu aufgelöst
dotnet run --project SchoolTestRunner -- new <schule> \
  --schulart Grundschule --bundesland BW --klassenstufen 4 --lehrer 8 [--zuege 2]
```

- `--schulart`: `Grundschule` oder `Gemeinschaftsschule`.
- `--bundesland`: aktuell nur `BW` recherchiert (siehe
  `docs/phase2-15-lehrereinsatzplanung.md`) - jedes andere Bundesland
  liefert eine klare Fehlermeldung statt erfundener Lehrplan-Zahlen.
- `--klassenstufen`: wie viele Klassenstufen (von der niedrigsten
  aufsteigend) erzeugt werden sollen - Grundschule max. 4, Gemeinschafts-
  schule max. 6.
- `--lehrer`: EXAKTE Anzahl der zu erzeugenden Klassenlehrer-Pool-
  Einträge (Nutzerentscheidung Phase 2.18: keine automatische Korrektur).
  Wird proportional zum tatsächlichen Fächer-Bedarf auf die Klassenlehrer-
  Pool-Typen der Schulart verteilt (Grundschule: ein Pool-Typ;
  Gemeinschaftsschule: drei, je Zwei-Fächer-Kombination) - muss mindestens
  so groß sein wie die Anzahl Pool-Typen. Die stets benötigten
  Fachlehrer-Spezialisten (Religion/Englisch bzw. NaWi/Sport/Musik-Kunst/
  Religion) werden dafür automatisch bedarfsgerecht ergänzt, ohne eigenen
  Parameter.
- `--zuege` (optional, Default 2): Anzahl paralleler Klassen je
  Klassenstufe (a, b, c, ...).

Schreibt `tests/<schule>/input/stammdaten.yaml` + eine leere,
kommentierte `constraints.yaml`. Bricht ab, falls `tests/<schule>/input/`
bereits existiert (kein versehentliches Überschreiben).

## CLI: Testfall ausführen

```bash
dotnet run --project SchoolTestRunner -- run <schule>
dotnet run --project SchoolTestRunner -- run --all   # alle Schulen unter tests/
```

Durchläuft die komplette Pipeline und schreibt `output/lehrerzuteilung.md`
+ `output/stundenplan.md` + `output/stundenplan.json` +
`output/stundentafel.html` - auch bei einem Abbruch in einer späten Stufe
bleibt der bis dahin erreichte Fortschritt sichtbar (kein
Alles-oder-Nichts; die beiden neuen Stundentafel-Dateien werden nur
geschrieben, wenn `Solver.SolveTop` mindestens eine Lösung fand). Gibt pro
Schule eine `PASS`/`FAIL`-Zeile aus und liefert Exitcode 0 nur, wenn ALLE
Stufen (StammdatenValidation, Lehrereinsatzplanung, VerifyLehrereinsatz,
Validation.ValidateEntities, Solver.SolveTop, VerifySchedule) sauber
durchlaufen - Exitcode 1 sonst (nutzbar für eine spätere CI-Anbindung,
ohne dass diese schon Teil dieses Tools ist).

Der Stundenplan wird über `Solver.SolveTop` (nicht das einfachere
`Solver.Solve`) erzeugt: dieselbe Qualitäts-Zielfunktion (Lücken,
Randstunden, Tagesausgewogenheit - siehe `ScheduleQuality.vb`), die sonst
erst nachträglich zum Sortieren mehrerer Kandidaten benutzt wird, fließt
hier direkt ins CP-SAT-Modell ein - der Solver sucht von vornherein einen
bzgl. dieser Kriterien möglichst guten statt nur irgendeinen zulässigen
Plan. Standardmäßig wird nur EIN finaler Plan erzeugt (`max_solutions: 1`);
ein höherer Wert in `config.yaml` exportiert zusätzliche, vergleichbare
Alternativen (siehe Abschnitt "Stundentafel-Visualisierung" unten).

## Stundentafel-Visualisierung (Phase 2.21)

`output/stundenplan.json` enthält ALLE von `Solver.SolveTop` gefundenen
Lösungen (nicht nur die beste) mit Status/Kann-Verstößen/Quality-Wert je
Lösung, dazu die Klassenstufen-/Parallelklassen-Struktur der Schule
(siehe Feldschema unten). `output/stundentafel.html` ist ein
eigenständiger, wiederverwendbarer JavaScript-Viewer: die JSON-Daten sind
inline eingebettet (kein `fetch()`, funktioniert deshalb auch bei
direktem Öffnen per Doppelklick ohne lokalen Webserver), ein
Dropdown-Menü schaltet zwischen den gefundenen Lösungen um. Die Tabelle
zeigt Wochentage in Spalten (unterteilt durch Klassenstufen) und
Schulstunden in Zeilen (unterteilt durch die Parallelklassen a/b/c/...
jeder Klassenstufe) - eine Gesamtübersicht über alle Klassen zugleich,
statt der separaten Pro-Klasse-Raster aus `stundenplan.md`.

**JSON-Feldschema** (Ausschnitt, snake_case):
```jsonc
{
  "schul_name": "...", "tage": ["Mo", ...], "periods_per_day": 6,
  "max_parallel_klassen": 2,   // groesste Parallelklassen-Anzahl je Klassenstufe
  "klassenstufen": [
    { "nummer": 1, "bezeichnung": "Klasse 1", "klassen": ["1a", "1b"] }
    // "klassen" ist auf max_parallel_klassen Laenge gepolstert (null bei
    // fehlender Parallelklasse an dieser Buchstaben-Position)
  ],
  "stop_reason": "MaxSolutionsReached",
  "solutions": [
    { "index": 0, "status": "Optimal", "kann_violation_count": 0,
      "muss_violation_count": 0, "quality_total": -515.4,
      "classes": { "1a": { "Mo": { "1": null, "2": {"subject":"...","teacher":"...","room": null}, ... }, ... } } }
  ]
}
```

Details, Nutzerentscheidungen und Live-Verifikationsergebnisse siehe
`docs/phase2-21-stundentafel-visualisierung.md`.

## Referenzbeispiele

Zwei tatsächlich per CLI erzeugte UND ausgeführte Testfälle als
lauffähige Vorbilder zum Kopieren. Beide haben zusätzlich eine eigene
`input/config.yaml` mit explizit gesetztem `solve_time_limit_s` (120.0 für
`bw-grundschule-beispiel`, 60.0 für `bw-gms-beispiel`) UND
`max_solutions: 5` (siehe Abschnitte oben) - ein direktes Vorbild dafür,
wie man diese Felder für die eigene Schule anpasst:

- **`tests/bw-grundschule-beispiel/`** (4 Klassenstufen, 8 Klassenlehrer,
  BW-Grundschule):
  ```bash
  dotnet run --project SchoolTestRunner -- new bw-grundschule-beispiel \
    --schulart Grundschule --bundesland BW --klassenstufen 4 --lehrer 8
  dotnet run --project SchoolTestRunner -- run bw-grundschule-beispiel
  ```
  Seine `constraints.yaml` enthält zusätzlich ein Beispiel für eine
  handverfasste `teacher_availability`-Regel.

- **`tests/bw-gms-beispiel/`** (6 Klassenstufen [5-10], 8 Klassenlehrer
  über 3 Zwei-Fächer-Pools verteilt, BW-Gemeinschaftsschule):
  ```bash
  dotnet run --project SchoolTestRunner -- new bw-gms-beispiel \
    --schulart Gemeinschaftsschule --bundesland BW --klassenstufen 6 --lehrer 8
  dotnet run --project SchoolTestRunner -- run bw-gms-beispiel
  ```
  Zeigt die proportionale `--lehrer`-Verteilung auf mehrere
  Klassenlehrer-Pool-Typen (siehe `--lehrer`-Beschreibung oben).
