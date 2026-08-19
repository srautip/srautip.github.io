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
2. **Constraints → Solver.Solve** (automatisch + optional handverfasst):
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

YAML-Pendant zu `Stammdatenbestand` in `TimetableCore/Stammdaten.vb` -
dieselben Felder, in snake_case (`deputat_sollstunden` statt
`DeputatSollstunden`). Ausschnitt:

```yaml
schul_name: Beispiel-Grundschule
bundesland: BW
schulart: Grundschule
tage: [Mo, Di, Mi, Do, Fr]
periods_per_day: 6
klassenstufen:
  - {nummer: 1, bezeichnung: "Klasse 1"}
klassen:
  - {name: 1a, klassenstufe: 1, schuelerzahl: 22}
    # erlaubt_klassenlehrer_tandem: true   # optional, siehe Phase 2.17
faecher:
  - name: Deutsch
    # block_length: 2                      # optional (Doppelstunde)
    # unbeliebt: true                      # optional, siehe Phase 2.17
    klassenstufen:
      - {klassenstufe: 1, wochenstunden_soll: 6, max_pro_tag: 2}
raeume:
  - {name: Turnhalle1, typ: Turnhalle}
lehrkraefte:
  - name: Klassenlehrer-1
    deputat_sollstunden: 28
    # anrechnungsstunden: 2                # optional
    # springer_reserve_stunden: 2          # optional, siehe Phase 2.17
    # verfuegbare_tage: [Mo, Di]           # optional, Nothing=Vollzeit
    # bevorzugte_klassenstufen: [1, 2]     # optional
    klassenlehrer_faehig: true
    # max_klassen: 1                       # optional, siehe Phase 2.17
    # max_faecher: 3                       # optional
fach_lehrer_zuordnungen:
  - lehrer_name: Klassenlehrer-1
    fach_name: Deutsch
    # fachfremd: true                      # optional, siehe Phase 2.17
```

Alle mit `#` markierten Felder sind optional und defaulten auf ein
neutrales Verhalten (kein Limit / kein Effekt), wenn sie weggelassen
werden - siehe `docs/phase2-15-lehrereinsatzplanung.md` (Nachtrag 4/5) für
die genaue Bedeutung jedes Phase-2.17-Feldes. Ein leer gelassener Wert
(`block_length:` ohne Text) bedeutet in YAML "nicht gesetzt" - identisch zu
komplettem Weglassen des Feldes.

Vor jedem Lauf prüft `StammdatenValidation.ValidateStammdaten` die Datei
auf Konsistenz (unbekannte Klassenstufen-Referenzen, Fach ohne
qualifizierte Lehrkraft, Deputat-Unsinn, Teilzeit-Tage-Kohärenz, ...) -
Fehler werden mit Datei-/Objektbezug in `output/lehrerzuteilung.md`
gemeldet.

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
num_workers: 1
```

Fehlt die Datei komplett, gelten diese Defaults unverändert.

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
+ `output/stundenplan.md` - auch bei einem Abbruch in einer späten Stufe
bleibt der bis dahin erreichte Fortschritt sichtbar (kein
Alles-oder-Nichts). Gibt pro Schule eine `PASS`/`FAIL`-Zeile aus und
liefert Exitcode 0 nur, wenn ALLE Stufen (StammdatenValidation,
Lehrereinsatzplanung, VerifyLehrereinsatz, Validation.ValidateEntities,
Solver.Solve, VerifySchedule) sauber durchlaufen - Exitcode 1 sonst
(nutzbar für eine spätere CI-Anbindung, ohne dass diese schon Teil dieses
Tools ist).

## Referenzbeispiele

Zwei tatsächlich per CLI erzeugte UND ausgeführte Testfälle als
lauffähige Vorbilder zum Kopieren:

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
