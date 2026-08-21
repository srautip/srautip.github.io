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
   `forbidden_slot`, `required_slot` (Fach-Slot erzwingen/bevorzugen),
   `occupied_slot` (fach-unabhängig: irgendeine Stunde der Klasse/Lehrkraft
   soll diesen Slot belegen - z.B. für eine durchgängige zeitliche Belegung
   ohne Fachbezug), `room_requirement` (Raumbindung ist bislang NICHT aus
   den Stammdaten ableitbar, siehe `Raum`-Kopfkommentar in
   `TimetableCore/Stammdaten.vb`), und ad-hoc `consecutive_required`. Volle
   Feldreferenz für alle Constraint-Typen: `docs/json-constraints-reference.md`.
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
- `feste_zuordnungen` - optional, Liste harter Lehrer-Klasse-Fach-
  Pinnungen (siehe unten).

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

### `feste_zuordnungen[]`

Optional - eine explizite, harte Vorgabe "diese Lehrkraft unterrichtet
dieses Fach in dieser Klasse", zusätzlich zu den weichen Präferenzen
(`bevorzugte_klassenstufen`). Additiv: ohne `feste_zuordnungen` verhält
sich die Planung exakt wie bisher.

- `lehrer_name` - Name der Lehrkraft (muss in `lehrkraefte[]` existieren
  UND laut `fach_lehrer_zuordnungen` für `fach_name` qualifiziert sein).
- `klasse_name` - Name einer Klasse (muss in `klassen[]` existieren) ODER
  seit Phase 2.27 der Name einer aktiven Gruppe (muss in `gruppen[]`
  existieren, dort `fach_name`/`klassenstufe` gesetzt haben - siehe
  `gruppen[]` oben). Klassen und Gruppen teilen sich einen Namensraum,
  welche Variante gemeint ist wird automatisch erkannt.
- `fach_name` - Name des Fachs (muss für die Klassenstufe dieser Klasse
  geführt werden; bei einer Gruppe muss `fach_name` exakt dem `fach_name`
  der Gruppe selbst entsprechen - eine Gruppe führt strukturell immer
  genau ein Fach).

**Solver-Wirkung:** `Lehrereinsatzplanung.SolveLehrereinsatz` erzwingt für
jeden Eintrag hart `assign(lehrer,klasse,fach)=1` - die bestehende "genau 1
Lehrkraft pro Klasse/Fach"-Summe sorgt dabei automatisch dafür, dass kein
anderer Kandidat für dieselbe (Klasse,Fach)-Kombination aktiv wird. Bei
einer Gruppen-Pinnung gilt das für die eine Gruppen-Variable, die anschließend
auf ALLE real von der Gruppe umspannten Klassen expandiert wird (dieselbe
Lehrkraft erscheint dann in `lehrerzuteilung.md` für jede dieser Klassen).
Anders als eine Präferenz kann eine feste Zuordnung NICHT durch die
Zielfunktion "wegoptimiert" werden - ist die Lehrkraft dafür nicht
qualifiziert oder teilzeit-tage-inkohärent, meldet
`StammdatenValidation.ValidateStammdaten` das schon VOR dem Solve als
Fehler statt eines schwer diagnostizierbaren Infeasible.

Vor jedem Lauf prüft `StammdatenValidation.ValidateStammdaten` die Datei
auf Konsistenz (unbekannte Klassenstufen-Referenzen, Fach ohne
qualifizierte Lehrkraft, Deputat-Unsinn, Teilzeit-Tage-Kohärenz, unbekannte
Schüler-/Gruppen-Referenzen, doppelte Schüler-IDs, feste Zuordnungen mit
unbekannten Referenzen/fehlender Qualifikation/Teilzeit-Inkohärenz/
widersprüchlicher Mehrfachzuordnung, ...) - Fehler werden mit Datei-/
Objektbezug in `output/lehrerzuteilung.md` gemeldet.

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
per_solve_time_limit_s: 30.0   # Optional, Default: faellt auf solve_time_limit_s zurueck - siehe unten
seed: 42
num_workers: 1   # Default: Anzahl CPU-Kerne - 1 (mindestens 1)
max_solutions: 1   # Default 1 (Phase 2.21) - siehe unten
stagnation_timeout_s: 45.0   # Optional, Default 45.0 - Phase 2.25, siehe unten
diversify_seed: true   # Optional, Default true - Phase 2.25
randomize_search: true   # Optional, Default true - Phase 2.25
relative_gap_limit: null   # Optional, Default nicht gesetzt - Phase 2.25, siehe unten
lexicographic: false   # Optional, Default false - Code-Review-Umsetzung P2, siehe unten
lex_tolerance: 0   # Optional, Default 0 - nur wirksam mit lexicographic: true
min_diversity: 0   # Optional, Default 0 - Code-Review-Umsetzung P3, siehe unten
rehint_found_solutions: true   # Optional, Default true - Code-Review-Umsetzung P3, siehe unten
quality_weights:   # Optional, alle Unterfelder optional (Phase 2.24) - siehe unten
  kann: 100.0
  class_gaps: 100.0
  teacher_gaps: 100.0
  edge_period: 5.0
  afternoon_day_count: 5.0
  class_load_variance: 3.0
  teacher_load_variance: 3.0
  include_class_gaps: true   # Optional, Default true - Code-Review-Umsetzung R3
  include_teacher_gaps: true   # Optional, Default true - Phase 2.25-Nachtrag-2, siehe unten
  include_edge_period: true   # Optional, Default true - siehe unten
  include_afternoon_day_count: true   # Optional, Default true - siehe unten
  include_class_load_variance: true   # Optional, Default true - siehe unten
  include_teacher_load_variance: true   # Optional, Default true - siehe unten
```

Fehlt die Datei komplett, gelten diese Defaults unverändert. `solve_time_limit_s`
begrenzt `Solver.SolveTop`s GESAMTBUDGET über alle Iterationen hinweg;
`per_solve_time_limit_s` (optional, fehlt es, gilt derselbe Wert wie
`solve_time_limit_s` - identisch zum bisherigen Verhalten) begrenzt
zusätzlich jede EINZELNE Solve-Iteration innerhalb dieses Gesamtbudgets.

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
Zeit benötigt wird. Zwei Hebel dafür: `solve_time_limit_s` selbst erhöhen
(mehr Gesamtzeit für alle Iterationen zusammen), oder gezielter
`per_solve_time_limit_s` NIEDRIGER als `solve_time_limit_s` setzen - das
zwingt jede einzelne Iteration früher zum Abbruch (statt das gesamte
Budget zu verbrauchen, bevor sie `Optimal` beweisen kann) und lässt so
innerhalb desselben Gesamtbudgets mehrere Iterationen zu.

**`stagnation_timeout_s`/`diversify_seed`/`randomize_search`/`relative_gap_limit`**
(Phase 2.25, direkte Antwort auf die Beobachtung, dass `Solver.SolveTop`s
`BestObjectiveBound` bei manchen Szenarien über lange Zeit unbewegt bleibt,
obwohl noch Budget übrig ist): `stagnation_timeout_s` (Default 45.0s,
**standardmäßig aktiv** - eine bewusste Ausnahme vom sonst üblichen
"fehlt das Feld, bleibt das Verhalten unverändert"-Prinzip) bricht eine
einzelne Solve-Iteration vorzeitig ab, sobald so lange keine neue
Verbesserung mehr gefunden wurde - die dadurch gesparte Zeit steht der
nächsten Iteration zur Verfügung, statt eine stehende Suche bis zum
Zeitlimit weiterlaufen zu lassen (sichtbar an `IterationsRun`/
`StagnationTriggeredCount`, siehe der entsprechende Hinweis in
`stundenplan.md`, falls der Mechanismus tatsächlich gegriffen hat). Bei
den üblichen kurzen `per_solve_time_limit_s`-Budgets greift dieser Cutoff
in der Praxis nie (das Budget ist ohnehin kürzer) - relevant wird er erst
bei großzügig konfigurierten Zeitbudgets. `diversify_seed`/
`randomize_search` (beide Default `true`) lassen aufeinanderfolgende
Iterationen unterschiedliche Teile des Suchraums erkunden (verschiedener
effektiver Seed pro Iteration bzw. CP-SATs eigene `randomize_search`-
Option) - beide bleiben für wiederholte Aufrufe mit demselben `seed`
weiterhin vollständig deterministisch. `relative_gap_limit` bleibt bewusst
NICHT standardmäßig aktiv (Default nicht gesetzt) - anders als die drei
Felder oben ändert es, WANN CP-SAT eine Lösung als bewiesen optimal
akzeptiert (eine Lücke bis zu diesem Prozentsatz wird toleriert statt
weiter nach Beweis gesucht), eine stärkere Verhaltensänderung, die diese
Phase nicht für jede Schule ungefragt erzwingt.

**`lexicographic`/`lex_tolerance`** (Code-Review-Umsetzung P2, siehe
`docs/code-review-cpsat-performance.md`): `lexicographic: true` ersetzt
die eine große gewichtete Zielfunktion durch drei nacheinander einzeln
optimierte Stufen - Kann-Verstöße, dann Klassen-Springstunden
(ClassGaps), dann Lehrer-Springstunden (TeacherGaps). Das Optimum jeder
Stufe wird als Constraint fixiert (`<= Optimum + lex_tolerance`), erst
danach laufen die normalen Iterationen über die gewichtete
REST-Zielfunktion (Randstunden/Nachmittags-Tage/Ausgewogenheit, soweit
per `include_*` aktiv). Vorteil: jede Stufe ist klein genug, dass CP-SAT
ihr Optimum in der Regel BEWEISEN kann (die Phase-2.25-Messungen zeigten
0,2s für Kann-only gegen 97-99% Restlücke beim Summenmodell) - alle
gefundenen Alternativen sind dann garantiert stufen-optimal statt
"irgendwo im Band einer nie geschlossenen Lücke". Zu beachten: in diesem
Modus legt die STUFENREIHENFOLGE die Priorität Kann > ClassGaps >
TeacherGaps fest - die `quality_weights` dieser drei Kriterien steuern
nur noch das nachgelagerte Ranking, nicht mehr die Suche (wer die
Priorität frei über Gewichte tauschen will, bleibt beim Default
`lexicographic: false`). `lex_tolerance` (Default 0) weitet das Band je
Stufe, z.B. `1` = "eine Springstunde mehr als das Optimum ist für
zusätzliche Alternativen akzeptabel".

**`min_diversity`/`rehint_found_solutions`** (Code-Review-Umsetzung P3):
Werkzeuge für "möglichst VERSCHIEDENE Alternativen statt
Ein-Slot-Nachbarn". `min_diversity` (Default 0 = bisheriges Verhalten)
verlangt, dass jede weitere Lösung mindestens so viele der bisher
belegten (Klasse,Fach,Lehrer,Tag,Stunde)-Slots ANDERS belegt - ein
echter Distanz-Cut gegen jede bereits gefundene Lösung, statt nur deren
exakte Wiederholung zu verbieten. Sinnvolle Werte: grob 5-10% der
Gesamt-Wochenstunden aller Klassen; zu hohe Werte erschöpfen den
Suchraum bewusst früher (`SearchSpaceExhausted` = "keine ausreichend
verschiedene Lösung existiert mehr"). `rehint_found_solutions: false`
schaltet zusätzlich ab, dass jede Iteration auf die soeben gefundene
Lösung "gehintet" wird - dieses Re-Hinting beschleunigt das Finden
IRGENDEINER nächsten Lösung, zieht die Suche aber systematisch zum
nächstgelegenen Nachbarn der Vorlösung; für Diversitäts-Läufe gehören
beide Hebel zusammen (`min_diversity` > 0 und `rehint_found_solutions:
false`).

**`quality_weights`** (Phase 2.24, komplett optional - fehlt der Block
oder ein einzelnes Unterfeld darin, gilt unverändert der jeweils oben
gezeigte Default) gewichtet, wie stark jedes der 7 Bewertungskriterien in
`Solver.SolveTop`s Zielfunktion UND in der angezeigten `Quality.Total`
(`stundenplan.md`/`stundenplan.json`) einfließt - dieselben Werte steuern
beides gleichzeitig, damit die Anzeige immer zu dem passt, wonach der
Solver tatsächlich gesucht hat:

| Feld | Bedeutet |
|---|---|
| `kann` | Verletzte "Kann"-Regeln (`priority: should`) - dominiert `edge_period`/`afternoon_day_count`/`*_load_variance` |
| `class_gaps` / `teacher_gaps` | Springstunden (Lücken zwischen belegten Stunden an einem Tag) für Klassen bzw. Lehrkräfte - seit Phase 2.25-Nachtrag-2 mit `kann` gleichgewichtet (früher `class_gaps` bewusst höher als `kann`; Live-Experimente zeigten, dass nicht das Gewicht, sondern die CP-SAT-Kodierung von `teacher_gaps` die eigentliche Ursache für schlecht beweisbare Lösungsschranken war - siehe `docs/phase2-25-stagnation-heuristik.md`, Nachtrag 2) |
| `edge_period` | Randstunden: 1. Stunde oder Nachmittag (Periode ≥ 7) |
| `afternoon_day_count` | Anzahl unterschiedlicher Tage mit Nachmittagsunterricht pro Klasse (nicht die Stundenanzahl - 4 Nachmittagsstunden an 1 Tag zählen als 1, an 4 Tagen verteilt als 4) |
| `class_load_variance` / `teacher_load_variance` | Ausgewogenheit der täglichen Stundenzahl (Lehrkräfte nur über ihre tatsächlichen Arbeitstage) |
| `include_teacher_gaps` | `false` schaltet `teacher_gaps` STRUKTURELL aus der Zielfunktion aus (keine Hilfsvariablen im Modell, nicht nur Gewicht 0) - Sicherheitsventil für Schulen, bei denen selbst die gefixte Kodierung (Phase 2.25-Nachtrag-2) noch zu teuer ist. Default `true`. |
| `include_edge_period` / `include_afternoon_day_count` / `include_class_load_variance` / `include_teacher_load_variance` | Gleiches strukturelles An/Aus-Muster wie `include_teacher_gaps` für die verbleibenden vier Sekundärkriterien - `false` entfernt das jeweilige Kriterium komplett aus `Solver.SolveTop`s CP-SAT-Modell (keine Hilfsvariablen, nicht nur Gewicht 0), z.B. für Schulen, bei denen nur Kann/`class_gaps`/`teacher_gaps` überhaupt eine Rolle spielen sollen. Beeinflusst NICHT `ScheduleQuality.Score`s unabhängig berechnete, immer angezeigte Werte in `stundenplan.md`/`stundenplan.json` (nur die SUCHE wird dafür blind, nicht die Anzeige). Jeweils Default `true`. |

Nur explizit gesetzte Unterfelder überschreiben ihren Default - eine
Schule kann also z.B. nur `edge_period` anpassen, ohne die übrigen 6
Gewichte mit angeben zu müssen. Ein Wert von `0` schaltet ein Kriterium
komplett ab (der Solver optimiert dann gar nicht mehr danach). Intern
fließen die Gewichte im CP-SAT-Modell als GANZE Zahlen ein (gerundet) -
für die reine Rangfolge zwischen Kriterien spielt das praktisch nie eine
Rolle, aber die in `stundenplan.md`/`stundenplan.json` angezeigte
`Quality.Total` selbst verwendet die exakten (nicht gerundeten) Werte.

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
      "muss_violation_count": 0, "quality_total": 12.5,
      "objective_value": 12.5, "best_objective_bound": 12.5, "gap_percent": 0.0,
      "convergence": [ {"elapsed_s": 0.3, "objective_value": 45.0}, {"elapsed_s": 2.1, "objective_value": 12.5} ],
      "classes": { "1a": { "Mo": { "1": null, "2": {"subject":"...","teacher":"...","room": null}, ... }, ... } } }
  ]
}
```

**Optimalitäts-Lücke + Konvergenz-Verlauf (Phase 2.22):** `objective_value`
ist CP-SATs eigener (roher) Zielfunktionswert für diese Lösung,
`best_objective_bound` die dazu bewiesene untere Schranke -
`gap_percent = 100 * (objective_value - best_objective_bound) / objective_value`
ist die maximal noch mögliche Verbesserung (bei `status: "Optimal"` immer
0%, da dann bewiesen keine bessere Lösung existiert). `convergence` ist
der Verlauf JEDER von CP-SAT gefundenen Verbesserung innerhalb DIESES
einen Solve-Versuchs (Zeitpunkt + Objective) - ein deutlich früherer
letzter Eintrag als das genutzte Zeitbudget zeigt an, dass mehr Zeit für
DIESEN Versuch vermutlich wenig gebracht hätte; `output/stundenplan.md`
und `stundentafel.html` (Diagramm unter der Lösungsauswahl) zeigen beides
bereits aufbereitet an. Wichtig: die Lücke ist eine bewiesene OBERGRENZE,
keine Vorhersage - die tatsächlich erreichbare Verbesserung kann kleiner
sein.

Details, Nutzerentscheidungen und Live-Verifikationsergebnisse siehe
`docs/phase2-21-stundentafel-visualisierung.md`.

## Veröffentlichung als GitHub Page (`main`-Branch)

Da `stundentafel.html` (siehe oben) komplett eigenständig ist (JSON-Daten
inline eingebettet, kein `fetch()`, kein Build-Schritt), lässt sie sich
1:1 als statische GitHub-Pages-Seite veröffentlichen. Dieses Repository
ist ein `<benutzer>.github.io`-Repo - **alles im `main`-Branch wird direkt
unter `https://srautip.github.io/` ausgeliefert**, unabhängig vom
Feature-Branch, auf dem dieses `timetable-dotnet/`-Verzeichnis entwickelt
wird.

Beide Referenzbeispiele sind auf diese Weise veröffentlicht, im
`stundentafel/`-Ordner an der Wurzel von `main` (NICHT unter
`timetable-dotnet/` - GitHub Pages braucht die Datei am erwarteten
öffentlichen Pfad, unabhängig von der internen Repo-Struktur):

- **Übersichtsseite:** <https://srautip.github.io/stundentafel/> (`stundentafel/index.html`)
- **BW-Grundschule:** <https://srautip.github.io/stundentafel/bw-grundschule-beispiel.html>
- **BW-Gemeinschaftsschule:** <https://srautip.github.io/stundentafel/bw-gms-beispiel.html>

**Wichtig - keine automatische Synchronisation:** ein `run`-Lauf hier im
Feature-Branch aktualisiert NUR die lokale
`tests/<schule>/output/stundentafel.html`. Die veröffentlichte Kopie auf
`main` muss nach jeder gewünschten Aktualisierung manuell nachgezogen
werden (ein Redeploy pro geändertem Beispiel, kein automatischer
Workflow) - über einen isolierten `git worktree` für `main`, damit der
aktuelle Feature-Branch-Arbeitsstand unangetastet bleibt:

```bash
git worktree add /tmp/main-worktree main
cp timetable-dotnet/tests/<schule>/output/stundentafel.html \
   /tmp/main-worktree/stundentafel/<schule>.html
cd /tmp/main-worktree
git add stundentafel/<schule>.html
git commit -m "Update <schule> Stundentafel"
git push origin main
cd -
git worktree remove /tmp/main-worktree
```

Nur die eine generierte `stundentafel.html`-Datei wird kopiert - kein
anderer Site-Content wird dabei angefasst. Da GitHub Pages nur eine
statische Momentaufnahme zeigt, kann die veröffentlichte Version
zwischenzeitlich hinter dem aktuellen `output/`-Stand im Feature-Branch
zurückliegen (z.B. nach einer neuen `config.yaml`-Kalibrierung wie in
den Kurzaufträgen zu Phase 2.22/2.25) - bei Bedarf erneut nachziehen.

## Referenzbeispiele

Zwei tatsächlich per CLI erzeugte UND ausgeführte Testfälle als
lauffähige Vorbilder zum Kopieren. Beide haben zusätzlich eine eigene
`input/config.yaml` mit explizit gesetztem `solve_time_limit_s`/
`per_solve_time_limit_s`/`max_solutions` (siehe Abschnitte oben, aktuelle
Werte direkt in den beiden `config.yaml`-Dateien nachsehen - sie wurden im
Lauf mehrerer Kurzaufträge testweise hochgesetzt, um die
Optimalitäts-Lücke auf einem realen Szenario zu beobachten, siehe
`docs/phase2-22-optimalitaetsluecke.md`) - ein direktes Vorbild dafür, wie
man diese Felder für die eigene Schule anpasst. Ein kurzer,
endnutzerorientierter Überblick über beide Beispiele steht außerdem in
`docs/schooltestrunner-benutzerhandbuch.md`:

- **`tests/bw-grundschule-beispiel/`** (4 Klassenstufen, 8 Klassen, 8
  Klassenlehrer, BW-Grundschule):
  ```bash
  dotnet run --project SchoolTestRunner -- new bw-grundschule-beispiel \
    --schulart Grundschule --bundesland BW --klassenstufen 4 --lehrer 8
  dotnet run --project SchoolTestRunner -- run bw-grundschule-beispiel
  ```
  Seine `constraints.yaml` enthält zusätzlich handverfasste
  `teacher_availability`-, `forbidden_slot`- (realistisches Zeitraster:
  Klasse 1/2 nur vormittags, Klasse 3/4 nur dienstags nachmittags) und
  einen `required_slot`-Regel (Chor donnerstags 6. Stunde für alle
  Klassen gleichzeitig, Phase 2.23) sowie (Phase 2.19/2.20) einen
  `schueler:`/`gruppen:`-Block in `stammdaten.yaml` für klassenstufen-
  übergreifende Religion-ev-/Religion-kath-/Ethik- und Chor-Gruppen. 140
  weitere `occupied_slot`-Kann-Regeln sorgen für eine durchgängige
  zeitliche Belegung (Klasse 1/2 soll täglich Stunde 2-4 belegt sein,
  Klasse 3/4 Stunde 2-5) - fach-unabhängig, im Gegensatz zum obigen
  `required_slot`.

- **`tests/bw-gms-beispiel/`** (6 Klassenstufen [5-10], 24 Klassen [4-zügig],
  ~696 Schüler, 48 Lehrkräfte, BW-Gemeinschaftsschule) - realitätsnah von
  Hand nach der BW-Kontingentstundentafel Gemeinschaftsschule (gültig ab
  1.8.2025) nachgebildet, **nicht** über den `new`-Scaffold erzeugt (der
  liefert nur ein generisches Grundgerüst ohne Differenzierung/Wahlbereich
  - dieses Beispiel demonstriert stattdessen bewusst die volle Bandbreite
  der Gruppen-/Parallelverbund-Mechanik aus Phase 2.20/2.23 an einem
  einzigen, in sich konsistenten Referenzfall):
  - **Niveaudifferenzierung ab Kl.7** (Deutsch/Mathematik/Englisch): G-/
    E-Kurs in Kl.7/8, zusätzlich A-Kurs ab Kl.9 - jeder Kurs läuft
    klassenstufenweit über alle 4 Parallelklassen synchron (`gruppen[]` +
    `parallelverbund`), zusätzlich in klassengroße **Sektionen** (max. 35
    Schüler) aufgeteilt, sobald die Kursgröße das überschreitet (z.B.
    `Mathematik-E-1`/`Mathematik-E-2` als separate `faecher[]`-Einträge
    mit identischem Stundenkontingent) - eine einzelne Gruppe mit >35
    Schülern wäre für klassenraumgebundenen Fachunterricht nicht plausibel
    (anders als die bewusst großflächige Chor-Gesamtprobe im
    Grundschulbeispiel).
  - **Wahlpflichtbereich ab Kl.6** (Technik/AES/Französisch als 2.
    Fremdsprache), ebenfalls sektioniert (Technik/AES sind Werkstatt-/
    Küchenräume mit echter Kapazitätsgrenze).
  - **Profilfach ab Kl.8** (NwT/IMP/Sport-Profil/Musik-Profil/BK-Profil) -
    laut Nutzervorgabe "i.d.R. Doppelqualifikation vorhandener
    Fachlehrer": keine eigenen Lehrkräfte, sondern zusätzliche
    `fach_lehrer_zuordnungen` für bereits bestehende Fachlehrkräfte.
  - **Religion-ev/-kath/Ethik** über alle 6 Klassenstufen, ebenfalls
    sektioniert.
  - **Fachraumbedarf** über `constraints.yaml`/`room_requirement`
    (`should`-Priorität): Sporthallen, NaWi-/Biologie-Fachräume,
    Musik-/Kunsträume, Technik-/AES-Räume, Computerraum (IMP) - inkl. je
    eines Eintrags pro tatsächlich generierter Sektionsvariante.
  - **Lehrkräfte bedarfsgenau bemessen** (~90% Ziel-Deputatsausschöpfung
    statt grosszügiger Pauschalgrössen): da eine klassenstufenweite Gruppe
    (z.B. ein G-Kurs) die Nachfrage über alle 4 Parallelklassen hinweg auf
    EINE Lehrkraft konsolidiert, wäre ein pauschal an der Klassenzahl
    bemessener Lehrerpool strukturell überdimensioniert und würde
    Deputat-Leerlauf erzeugen, den die (rein lineare) Deputat-Abweichungs-
    Kostenfunktion beliebig auf einzelne Lehrkräfte verteilen kann (siehe
    Kanarienvogel-Wirkung unten). Zusätzliche Sicherheitsmarge: jeder Pool
    hat mindestens so viele Köpfe wie die größte Anzahl zeitgleicher
    Gruppen im selben Parallelverbund (z.B. 2 gleichzeitige
    Technik-Sektionen brauchen zwingend 2 verschiedene Lehrkräfte -
    `Lehrereinsatzplanung.vb` prüft das selbst nicht, erst der
    nachgelagerte `no_overlap(teacher)`-Constraint der Tag/Periode-Stufe
    würde eine Kollision hart verhindern und das Szenario sonst
    unlösbar machen).
  - Erzeugt mit einem projektinternen, nicht committeten Python-
    Wegwerfskript (gleiche Disziplin wie die 92-Constraint-Generierung in
    Phase 2.23) - `stammdaten.yaml`/`constraints.yaml` selbst sind
    normale, direkt im GitHub-Web-Editor bearbeitbare YAML-Dateien wie
    jedes andere Beispiel.
  ```bash
  dotnet run --project SchoolTestRunner -- run bw-gms-beispiel
  ```
