# JSON-Referenz: Entities und Constraints

Diese Referenz beschreibt das vollständige JSON-Wire-Format, das
`Solver.vb`/`Validation.vb`/`Verifier.vb`/`LlmExtraction.vb` konsumieren
bzw. erzeugen. Es ist unverändert (feldidentisch) aus dem Python-Prototyp
(`timetable/`) übernommen (siehe
[`docs/arc42-architecture.md`](arc42-architecture.md), Abschnitt 4). Für
den architektonischen Gesamtkontext siehe dieses Dokument; hier geht es
ausschließlich um das Datenformat selbst.

## Inhalt

- [1. Grundstruktur](#1-grundstruktur)
- [2. `entities`](#2-entities)
- [3. Muss/Kann-Priorität (`priority`)](#3-musskann-prioritt-priority)
- [4. Rückverfolgbarkeit (`reason`)](#4-rckverfolgbarkeit-reason)
- [5. Constraint-Typen (Referenz)](#5-constraint-typen-referenz)
- [6. Vollständiges Beispiel (klassenbasiert)](#6-vollstndiges-beispiel-klassenbasiert)
- [7. Kursstufe (Schienenmodell): zusätzliche Entities und `kurswahl`](#7-kursstufe-schienenmodell-zustzliche-entities-und-kurswahl)
- [8. Häufige Fehler](#8-hufige-fehler)

## 1. Grundstruktur

```jsonc
{
  "entities": {
    "classes": ["5a", "5b"],
    "teachers": ["Frau Müller", "Herr Schmidt"],
    "subjects": ["Deutsch", "Mathematik", "Sport"],
    "rooms": ["Turnhalle"],
    "timeslots": { "days": ["Mo", "Di", "Mi", "Do", "Fr"], "periods_per_day": 6 }
  },
  "constraints": [
    { "type": "teacher_subject_assignment", "class": "5a", "subject": "Deutsch", "teacher": "Frau Müller" }
  ]
}
```

- `entities` beschreibt, WAS es an einer Schule gibt (feste Namenslisten
  plus das Zeitraster).
- `constraints` ist eine flache Liste typisierter Regelobjekte
  (`"type"` bestimmt die übrigen Pflicht-/Optionalfelder, siehe
  Abschnitt 5).
- Felder, die laut Schema entweder ein einzelner String oder eine Liste
  sein dürfen (z.B. `class`/`classes` bei manchen Typen), werden intern
  über `JsonHelpers.AsStringList` normalisiert - beide Schreibweisen sind
  gültig.

## 2. `entities`

| Feld | Typ | Pflicht | Bedeutung |
|---|---|---|---|
| `classes` | `string[]` | ja (klassenbasiert) | Namen aller Klassen, z.B. `"5a"`. |
| `teachers` | `string[]` | ja | Namen aller Lehrkräfte. |
| `subjects` | `string[]` | ja | Namen aller Fächer. |
| `rooms` | `string[]` | ja (kann leer sein) | Namen aller (Fach-)Räume. Nur relevant für Fächer mit `room_requirement`. |
| `timeslots.days` | `string[]` | ja | Wochentage, z.B. `["Mo","Di","Mi","Do","Fr"]`. Reihenfolge bestimmt u.a. die Ausgabe-Spaltenreihenfolge. |
| `timeslots.periods_per_day` | `int` | ja | Anzahl Unterrichtsstunden pro Tag (Perioden sind 1-indiziert: `1..periods_per_day`). |
| `kurse` | `object[]` | nein (nur Kursstufe) | Kursangebote - siehe Abschnitt 7. |
| `schienen` | `object[]` | nein (nur Kursstufe) | Parallele Zeitblöcke - siehe Abschnitt 7. |

`classes`/`teachers`/`subjects`/`rooms` sind reine Namenslisten - jede
Referenz auf einen dort nicht enthaltenen Namen in `constraints` ist ein
harter Validierungsfehler (`Validation.ValidateEntities`), der das
Lösen blockiert.

## 3. Muss/Kann-Priorität (`priority`)

Optionales Feld `"priority"` auf einem Constraint-Objekt:

| Wert | Bedeutung |
|---|---|
| `"must"` (Default, auch wenn das Feld ganz fehlt) | Hart - darf im Ergebnis nie verletzt sein; eine unerfüllbare Kombination aus Muss-Constraints macht das gesamte Szenario `Infeasible`. |
| `"should"` | Weich ("Kann") - der Solver versucht, die Regel einzuhalten, darf sie aber verletzen, um überhaupt einen Plan zu finden. Die Anzahl verletzter `should`-Constraints wird minimiert (jede verletzte Regel zählt gleich 1, unabhängig davon, wie viele Slots sie betrifft). |

**Nur diese fünf Typen dürfen `"should"` sein:**
`teacher_availability`, `forbidden_slot`, `room_requirement`,
`consecutive_required`, sowie **nur der `max_per_day`-Teil** von
`weekly_hours` (die `hours_per_week`-Exaktzahl bleibt immer hart - ein
Fach mit z.B. `"should"` aber ohne gesetztes `max_per_day` ist ein
Validierungsfehler, da es dann nichts gäbe, das gelockert werden könnte).

**Immer hart, `"priority"` dort unzulässig:** `no_overlap`,
`shared_resource_conflict`, `teacher_subject_assignment` - diese sind
physisch/strukturell zwingend (ein Lehrer kann nicht an zwei Orten
gleichzeitig sein; eine Klasse wird nur von der zugewiesenen Lehrkraft
unterrichtet).

```jsonc
{ "type": "forbidden_slot", "scope": "class", "entity": "5a",
  "day": "Fr", "period": 6, "priority": "should",
  "reason": "soll wenn möglich frei bleiben" }
```

Prüfen, welche `should`-Constraints im gefundenen Plan tatsächlich
verletzt wurden: `Verifier.VerifyScheduleDetailed(data, schedule).KannViolations`
(unabhängig aus dem Schedule re-abgeleitet, nicht aus internen
Solver-Variablen gelesen).

## 4. Rückverfolgbarkeit (`reason`)

Optionales Freitextfeld `"reason"` auf jedem Constraint-Objekt - eine
kurze, menschenlesbare Herkunftsangabe (z.B. die Textstelle, aus der ein
LLM diese Regel abgeleitet hat). Wird, falls gesetzt, an jede zugehörige
Validierungs-/Verifier-Meldung angehängt: `"... (Regel-Herkunft: '...')"`.
Rein informativ, hat keinen Einfluss auf das Solver-Verhalten.

```jsonc
{ "type": "teacher_availability", "teacher": "Herr Schmidt",
  "available_days": ["Mo", "Di", "Mi"], "priority": "must",
  "reason": "arbeitet Teilzeit, nur Mo-Mi verfügbar" }
```

## 5. Constraint-Typen (Referenz)

Jeder Eintrag zeigt: Pflichtfelder, optionale Felder, ob `priority`
erlaubt ist, und ein Minimalbeispiel. Unbekannte `"type"`-Werte werden
sowohl von `Solver.ApplyConstraints` als auch von `Verifier.CollectViolations`
mit einer Ausnahme bzw. einer eigenen Verstoßmeldung quittiert - es gibt
keinen stillen Fallback.

---

### `teacher_subject_assignment`

Legt fest, welche Lehrkraft welches Fach in welcher Klasse unterrichtet -
**definiert die planbaren Einheiten selbst** (jede Kombination erzeugt
eine `Session`, für die dann Tag/Periode gesucht wird). Immer hart, kein
`priority`-Feld zulässig.

| Feld | Typ | Pflicht |
|---|---|---|
| `class` | string | ja |
| `subject` | string | ja |
| `teacher` | string | ja |
| `reason` | string | nein |

```json
{ "type": "teacher_subject_assignment", "class": "5a", "subject": "Deutsch", "teacher": "Frau Müller" }
```

Ohne mindestens ein solches Objekt gibt es nichts zu planen - `Solver.Solve`
wirft in diesem Fall eine `ArgumentException`.

---

### `weekly_hours`

Wochenstunden für eine Klasse+Fach-Kombination, optional mit
Tagesmaximum. `priority` betrifft **ausschließlich** `max_per_day` -
`hours_per_week` ist nie weich.

| Feld | Typ | Pflicht |
|---|---|---|
| `class` | string | ja |
| `subject` | string | ja |
| `hours_per_week` | int | ja |
| `max_per_day` | int | nein |
| `priority` | `"must"`\|`"should"` (nur mit gesetztem `max_per_day` sinnvoll) | nein |
| `reason` | string | nein |

```json
{ "type": "weekly_hours", "class": "5a", "subject": "Sport", "hours_per_week": 3, "max_per_day": 1 }
```

Weiche Variante (Tagesmaximum als Wunsch):

```json
{ "type": "weekly_hours", "class": "5a", "subject": "Mathematik",
  "hours_per_week": 4, "max_per_day": 2, "priority": "should",
  "reason": "wenn möglich höchstens 2 pro Tag" }
```

---

### `room_requirement`

Ein Fach darf nur in bestimmten Räumen stattfinden (typisch: Sport,
Naturwissenschaften, Kunst/Musik).

| Feld | Typ | Pflicht |
|---|---|---|
| `subject` | string | ja |
| `allowed_rooms` | string[] | ja |
| `priority` | `"must"`\|`"should"` | nein |
| `reason` | string | nein |

```json
{ "type": "room_requirement", "subject": "Sport", "allowed_rooms": ["Turnhalle1", "Turnhalle2"] }
```

Bei `"should"`: die Stunde findet auf jeden Fall statt, bekommt im
Verletzungsfall aber keinen der erlaubten Räume zugewiesen, statt das
gesamte Szenario unlösbar zu machen.

---

### `no_overlap`

Generelle Überschneidungsfreiheit: höchstens eine Session der genannten
Ressource pro Tag/Periode. Immer hart, kein `priority`-Feld zulässig -
das ist die grundlegendste physische Einschränkung des ganzen Modells
(eine Klasse/Lehrkraft/ein Raum kann nur an einem Ort gleichzeitig sein).

| Feld | Typ | Pflicht |
|---|---|---|
| `resource` | `"class"`\|`"teacher"`\|`"room"` | ja |
| `entity` | string | ja |
| `reason` | string | nein |

```json
{ "type": "no_overlap", "resource": "teacher", "entity": "Frau Müller" }
```

Üblich: **ein Objekt pro Klasse, pro Lehrkraft** (und pro geteiltem
Fachraum), damit wirklich niemand doppelt belegt werden kann.
`Validation.CoverageWarnings` meldet (nicht-blockierend) fehlende
`no_overlap`-Regeln für Klassen/Lehrkräfte - das kann gewollt sein (eine
Lehrkraft, die nur eine einzige Klasse unterrichtet, braucht z.B. keine
eigene Regel), sollte aber bewusst geprüft werden.

---

### `shared_resource_conflict`

Mehrere Klassen dürfen wegen derselben Lehrkraft nicht gleichzeitig
denselben (fächerübergreifend identischen) Unterricht haben - z.B. eine
gemeinsame AG oder ein im Klassenverband geteilter Kurs. Immer hart.

| Feld | Typ | Pflicht |
|---|---|---|
| `classes` | string[] | ja |
| `subject` | string | ja |
| `teacher` | string | ja |
| `reason` | string | nein |

```json
{ "type": "shared_resource_conflict", "classes": ["5a", "5b"], "subject": "Chor", "teacher": "Herr Klein" }
```

---

### `forbidden_slot`

Eine feste Sperrzeit (ein konkreter Tag+Periode-Slot) für eine Klasse,
Lehrkraft oder einen Raum.

| Feld | Typ | Pflicht |
|---|---|---|
| `scope` | `"class"`\|`"teacher"`\|`"room"` | ja |
| `entity` | string | ja |
| `day` | string | ja |
| `period` | int | ja |
| `priority` | `"must"`\|`"should"` | nein |
| `reason` | string | nein |

```json
{ "type": "forbidden_slot", "scope": "class", "entity": "5a", "day": "Fr", "period": 6 }
```

Gilt eine Sperrzeit schulweit für alle Klassen, wird üblicherweise **ein
Objekt pro Klasse** erzeugt (kein Wildcard-Mechanismus).

---

### `consecutive_required`

Ein Fach muss an jedem Tag, an dem es stattfindet, als zusammenhängender
Block der Länge `block_length` unterrichtet werden (z.B. Doppelstunde).
`hours_per_week` der zugehörigen `weekly_hours`-Regel muss ein exaktes
Vielfaches von `block_length` sein, und `max_per_day` (falls gesetzt)
muss `>= block_length` sein - andernfalls ist die Kombination
mathematisch unmöglich (bei LLM-Extraktion wird ein solcher
Widerspruch automatisch entfernt, siehe `LlmExtraction.DropContradictoryConsecutiveRequired`).

| Feld | Typ | Pflicht |
|---|---|---|
| `class` | string | ja |
| `subject` | string | ja |
| `block_length` | int | ja |
| `priority` | `"must"`\|`"should"` | nein |
| `reason` | string | nein |

```json
{ "type": "weekly_hours", "class": "5a", "subject": "Physik", "hours_per_week": 2, "max_per_day": 2 }
```
```json
{ "type": "consecutive_required", "class": "5a", "subject": "Physik", "block_length": 2 }
```

---

### `teacher_availability`

Verfügbarkeits-Einschränkung einer einzelnen Lehrkraft - entweder über
eine positive Liste erlaubter Tage (`available_days`) oder eine
punktgenaue Sperrliste (`unavailable_periods`); beides kann kombiniert
werden.

| Feld | Typ | Pflicht |
|---|---|---|
| `teacher` | string | ja |
| `available_days` | string[] | nein* |
| `unavailable_periods` | `{day, period}[]` | nein* |
| `priority` | `"must"`\|`"should"` | nein |
| `reason` | string | nein |

\* mindestens eines der beiden sollte gesetzt sein - ein Objekt ganz ohne
Einschränkungsinhalt ist wirkungslos.

```json
{ "type": "teacher_availability", "teacher": "Herr Schmidt",
  "available_days": ["Mo", "Di", "Mi"] }
```

```json
{ "type": "teacher_availability", "teacher": "Frau Berger",
  "unavailable_periods": [
    { "day": "Do", "period": 7 },
    { "day": "Do", "period": 8 }
  ],
  "priority": "should",
  "reason": "ist donnerstagnachmittags idealerweise nicht eingeplant" }
}
```

---

### `period_exception` (nur LLM-Extraktion, kein Solver-Constraint)

**Kein** von `Solver.vb` konsumierter Typ - existiert ausschließlich als
LLM-Extraktionsziel für Formulierungen wie *"7. Stunde findet nur
dienstags statt"*. Wird von `LlmExtraction.ExtractAllConstraints`
deterministisch (reine Tage-Mengendifferenz, kein LLM) in je einen
`forbidden_slot`-Eintrag pro (gesperrter Tag) × (Klasse) expandiert,
bevor das Ergebnis den Aufrufer erreicht - im finalen `constraints`-Array
taucht `"period_exception"` selbst nie auf.

| Feld | Typ | Pflicht |
|---|---|---|
| `period` | int | ja |
| `allowed_days` | string[] | ja |
| `reason` | string | nein |

```json
{ "type": "period_exception", "period": 7, "allowed_days": ["Di"] }
```

expandiert (bei `days: ["Mo","Di","Mi","Do","Fr"]`, `classes: ["5a"]`) zu:

```json
[
  { "type": "forbidden_slot", "scope": "class", "entity": "5a", "day": "Mo", "period": 7, "reason": "nur erlaubt an ['Di']" },
  { "type": "forbidden_slot", "scope": "class", "entity": "5a", "day": "Mi", "period": 7, "reason": "nur erlaubt an ['Di']" },
  { "type": "forbidden_slot", "scope": "class", "entity": "5a", "day": "Do", "period": 7, "reason": "nur erlaubt an ['Di']" },
  { "type": "forbidden_slot", "scope": "class", "entity": "5a", "day": "Fr", "period": 7, "reason": "nur erlaubt an ['Di']" }
]
```

---

### `kurswahl` (nur Kursstufe/Schienenmodell)

Ein Wahlprofil: eine Gruppe von Schülern mit identischer Kurswahl. Siehe
Abschnitt 7 für den vollständigen Kursstufen-Kontext.

| Feld | Typ | Pflicht |
|---|---|---|
| `wahlprofil_id` | string | ja (eindeutig) |
| `student_count` | int | ja (`> 0`) |
| `kurse` | string[] (Kurs-IDs aus `entities.kurse[].id`) | ja - **genau 3 mit `kursart="LK"`** |
| `reason` | string | nein |

```json
{ "type": "kurswahl", "wahlprofil_id": "WP1", "student_count": 24,
  "kurse": ["D-LK1", "MA-LK2", "BIO-LK1", "EN-GK3", "GE-GK1", "SPO-GK1"] }
```

## 6. Vollständiges Beispiel (klassenbasiert)

Ein kleines, vollständig lösbares Zwei-Klassen-Szenario:

```json
{
  "entities": {
    "classes": ["5a", "5b"],
    "teachers": ["Frau Müller", "Herr Schmidt", "Frau Berger", "Herr Klein"],
    "subjects": ["Deutsch", "Mathematik", "Sport"],
    "rooms": ["Turnhalle1"],
    "timeslots": { "days": ["Mo", "Di", "Mi", "Do", "Fr"], "periods_per_day": 6 }
  },
  "constraints": [
    { "type": "teacher_subject_assignment", "class": "5a", "subject": "Deutsch", "teacher": "Frau Müller" },
    { "type": "teacher_subject_assignment", "class": "5a", "subject": "Mathematik", "teacher": "Herr Schmidt" },
    { "type": "teacher_subject_assignment", "class": "5a", "subject": "Sport", "teacher": "Frau Berger" },
    { "type": "teacher_subject_assignment", "class": "5b", "subject": "Deutsch", "teacher": "Frau Müller" },
    { "type": "teacher_subject_assignment", "class": "5b", "subject": "Mathematik", "teacher": "Herr Klein" },
    { "type": "teacher_subject_assignment", "class": "5b", "subject": "Sport", "teacher": "Frau Berger" },

    { "type": "weekly_hours", "class": "5a", "subject": "Deutsch", "hours_per_week": 4, "max_per_day": 2 },
    { "type": "weekly_hours", "class": "5a", "subject": "Mathematik", "hours_per_week": 4, "max_per_day": 2 },
    { "type": "weekly_hours", "class": "5a", "subject": "Sport", "hours_per_week": 2, "max_per_day": 1 },
    { "type": "weekly_hours", "class": "5b", "subject": "Deutsch", "hours_per_week": 4, "max_per_day": 2 },
    { "type": "weekly_hours", "class": "5b", "subject": "Mathematik", "hours_per_week": 4, "max_per_day": 2 },
    { "type": "weekly_hours", "class": "5b", "subject": "Sport", "hours_per_week": 2, "max_per_day": 1 },

    { "type": "room_requirement", "subject": "Sport", "allowed_rooms": ["Turnhalle1"] },

    { "type": "no_overlap", "resource": "class", "entity": "5a" },
    { "type": "no_overlap", "resource": "class", "entity": "5b" },
    { "type": "no_overlap", "resource": "teacher", "entity": "Frau Müller" },
    { "type": "no_overlap", "resource": "teacher", "entity": "Herr Schmidt" },
    { "type": "no_overlap", "resource": "teacher", "entity": "Frau Berger" },
    { "type": "no_overlap", "resource": "teacher", "entity": "Herr Klein" },
    { "type": "no_overlap", "resource": "room", "entity": "Turnhalle1" },

    { "type": "teacher_availability", "teacher": "Herr Schmidt", "available_days": ["Mo", "Di", "Mi"] },

    { "type": "forbidden_slot", "scope": "class", "entity": "5a", "day": "Fr", "period": 6,
      "priority": "should", "reason": "soll wenn möglich frei bleiben" }
  ]
}
```

Aufruf:

```vb
Dim result = Solver.Solve(data, timeLimitS:=30, seed:=42, numWorkers:=1)
Dim violations = Verifier.VerifySchedule(data, result.Schedule)  ' erwartet: leer
```

## 7. Kursstufe (Schienenmodell): zusätzliche Entities und `kurswahl`

Für die Oberstufe/Kursstufe (individuelle Kurswahl statt Klassenverband)
kommen zwei zusätzliche `entities`-Listen hinzu. Sie sind rein additiv -
ein Szenario ohne diese Felder verhält sich exakt wie zuvor.

### `entities.kurse[]`

| Feld | Typ | Pflicht |
|---|---|---|
| `id` | string (eindeutig) | ja |
| `subject` | string (muss in `entities.subjects` vorkommen) | ja |
| `teacher` | string (muss in `entities.teachers` vorkommen) | ja |
| `kursart` | `"LK"` (Leistungskurs) \| `"GK"` (Grundkurs) | ja |
| `hours_per_week` | int (`> 0`) | ja |
| `halbjahr` | string (rein dokumentarisch, z.B. `"12/1"`) | nein |

```json
{ "id": "D-LK1", "subject": "Deutsch", "teacher": "Frau Berger", "kursart": "LK", "hours_per_week": 5, "halbjahr": "12/1" }
```

### `entities.schienen[]`

Ein paralleler Zeitblock, dem kompatible Kurse (gleiches `kursart` UND
gleiches `hours_per_week`) zugeordnet werden können.

| Feld | Typ | Pflicht |
|---|---|---|
| `id` | string (eindeutig) | ja |
| `kursart` | `"LK"`\|`"GK"` | ja |
| `hours_per_week` | int (`> 0`) | ja |
| `capacity` | int (`> 0`, falls gesetzt) | nein - max. gleichzeitige Kurse auf dieser Schiene (realer Raum-Engpass) |
| `block_length` | int | nein - erzwingt Doppelstunden-Blöcke für diese Schiene (muss `hours_per_week` glatt teilen) |

```json
{ "id": "S1", "kursart": "LK", "hours_per_week": 5, "capacity": 4 }
```

**Validierungsregel** (`Validation.ValidateKursstufeEntities`): jeder
Kurs braucht mindestens eine kompatible Schiene (gleiches `kursart` UND
`hours_per_week`) - sonst ist die spätere Kursblockung trivial
unlösbar, und der Fehler wird bereits hier statt erst beim Solve
gemeldet.

### Vollständiges Kursstufen-Beispiel (Ausschnitt)

```json
{
  "entities": {
    "classes": [], "teachers": ["Frau Berger", "Herr Wolf"],
    "subjects": ["Deutsch", "Biologie"], "rooms": ["Kursraum1", "Kursraum2"],
    "timeslots": { "days": ["Mo", "Di", "Mi", "Do", "Fr"], "periods_per_day": 8 },
    "kurse": [
      { "id": "D-LK1", "subject": "Deutsch", "teacher": "Frau Berger", "kursart": "LK", "hours_per_week": 5 },
      { "id": "BIO-LK1", "subject": "Biologie", "teacher": "Herr Wolf", "kursart": "LK", "hours_per_week": 5 }
    ],
    "schienen": [
      { "id": "S1", "kursart": "LK", "hours_per_week": 5, "capacity": 4 },
      { "id": "S2", "kursart": "LK", "hours_per_week": 5, "capacity": 4 }
    ]
  },
  "constraints": [
    { "type": "kurswahl", "wahlprofil_id": "WP1", "student_count": 22,
      "kurse": ["D-LK1", "BIO-LK1", "MA-LK1"] }
  ]
}
```

Aufruf über die dreistufige Pipeline (Kursblockung → Schienenraster →
Raumzuordnung, siehe `docs/arc42-architecture.md` Abschnitt 6.4):

```vb
Dim result = Solver.SolveKursstufe(data, timeLimitS:=30)
```

`kurswahl` ist der einzige Constraint-Typ, der ausschließlich im
Kursstufen-Kontext Sinn ergibt - er wird von `Kursblockung.vb` gelesen,
nie direkt von `Solver.ApplyConstraints`/`Verifier.CollectViolations`
(die kennen den klassenbasierten Constraint-Vorrat aus Abschnitt 5).

## 8. Häufige Fehler

| Fehler | Symptom | Fund-Ort |
|---|---|---|
| Constraint referenziert eine nicht in `entities` gelistete Klasse/Lehrkraft/Fach/Raum. | `Validation.ValidateEntities` liefert einen Eintrag: `"Feld 'X'='Y' ist keine bekannte Entity (erlaubt: [...])"`. Blockiert das Lösen. | Vor dem Solve, deterministisch. |
| `"should"` auf einem immer-harten Typ (`no_overlap`, `shared_resource_conflict`, `teacher_subject_assignment`). | Validierungsfehler: `"priority='should' ist für diesen Constraint-Typ nicht erlaubt (immer Muss)"`. | Vor dem Solve. |
| `"should"` auf `weekly_hours` ohne gesetztes `max_per_day`. | Validierungsfehler: `"priority='should' ohne gesetztes max_per_day ergibt nichts, das gelockert werden könnte"`. | Vor dem Solve. |
| `consecutive_required.block_length` teilt `weekly_hours.hours_per_week` nicht glatt, oder übersteigt `max_per_day`. | Mathematisch unmöglich → `Infeasible`. Bei LLM-Extraktion wird ein solcher Fall automatisch verworfen (`DropContradictoryConsecutiveRequired`), bei manuell erstelltem JSON bleibt er bestehen. | Beim Solve (`Infeasible`), bzw. still entfernt bei LLM-Herkunft. |
| Fehlende `no_overlap`-Regel für eine Klasse/Lehrkraft. | Kein harter Fehler - `Validation.CoverageWarnings` meldet es nur als Warnung (evtl. gewollt). | Vor dem Solve, nicht blockierend. |
| Kurs ohne kompatible Schiene (Kursstufe). | `Validation.ValidateKursstufeEntities`: `"keine Schiene mit kursart=... und hours_per_week=... vorhanden"`. | Vor dem Solve. |
| Wahlprofil ohne genau 3 `LK`-Kursen. | `Validation.ValidateKursstufeEntities`: `"genau 3 Leistungskurse (kursart=LK) erforderlich, gefunden: N"`. | Vor dem Solve. |
| `"type"` ist kein bekannter Constraint-Typ. | `Solver.ApplyConstraints` wirft `ArgumentException: "Unbekannter Constraint-Typ: '...'"`; `Verifier.CollectViolations` meldet es unabhängig ebenfalls als eigenen Verstoß. | Beim Solve bzw. bei der Verifikation. |
