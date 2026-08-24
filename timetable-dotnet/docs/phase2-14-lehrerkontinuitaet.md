# Phase 2.14: Lehrerkontinuität Kl.5→Kl.6 über ein "Vorjahresstundenplan"-Fixture

Dieser Bericht dokumentiert Phase 2.14 (siehe Plan, Abschnitt "Phase 2.14
(feingeplant)"): Nutzerwunsch nach einem weiteren Benchmark-Test, bei dem
Lehrerzuordnungen vom Vorjahresstundenplan abhängen - Lehrer sollen über 2
Jahre möglichst stabil bleiben (Beispiel des Nutzers: Kl.5/6).

## Nutzerentscheidung

Kontinuität gilt für **alle** Fächer der Klasse (nicht nur ein
2-Fächer-Klassenlehrer-Tandem-Subset) - jeder Fachlehrer, der eine Klasse
in Kl.5 unterrichtet hat, unterrichtet sie auch in Kl.6 weiter.

## Realweltlicher Hintergrund

- **Baden-Württemberg** bezeichnet Kl.5/6 offiziell als "Orientierungsstufe"
  mit explizitem Fokus auf "personalen Bezug" (Kontinuität), um den
  Übergang von der Grundschule zu erleichtern.
- Das reale **Gustav-Stresemann-Gymnasium Fellbach** (die Inspiration für
  `GymnasiumSekIFixture.vb`) praktiziert dafür ein **Klassenlehrer-Tandem**:
  zwei Klassenlehrer pro Klasse (üblicherweise eine Frau, ein Mann), die
  gemeinsam einen Großteil der Unterrichtsstunden inkl. zweier Hauptfächer
  abdecken - explizit für "Klassen 5 und 6" als zusammenhängende Einheit
  beschrieben.
- Allgemein gilt am Gymnasium das **Fachlehrerprinzip** (spezialisierte
  Fachlehrer je Fach), im Gegensatz zum **Klassenlehrerprinzip** der
  Grundschule (eine Lehrkraft für fast alle Fächer). Viele Gymnasien
  mildern das Fachlehrerprinzip gezielt in der Erprobungs-/
  Orientierungsstufe ab, um Schülerinnen und Schülern den Wechsel von der
  personenbezogenen Grundschule zum Fachunterricht zu erleichtern - die
  pädagogische Begründung, warum ausgerechnet Kl.5→6 Kontinuität erwartet
  wird, nicht bei einem beliebigen anderen Jahrgangsübergang.

Quellen: Ministerium für Kultus Baden-Württemberg (Orientierungsstufe),
gsg-fellbach.de (Unterstufe/Klassenlehrer-Tandem), allgemeine Recherche zu
Klassenlehrerprinzip vs. Fachlehrerprinzip (u.a. Wikipedia, THG Waltrop,
Pascal-Gymnasium Münster).

## Zentraler Befund: reine Fixture-Konstruktions-Frage, kein Solver-Feature

`teacher_subject_assignment` ist im Solver **kein** weiches/Kann-fähiges
Constraint - es wird bereits vor `ApplyConstraints` in
`SessionsFromAssignments` (`Solver.vb:199-211`) konsumiert und definiert
direkt die Entscheidungsvariablen-Identität selbst
(`LessonKey(class, subject, teacher, day, period)`). `Validation.vb`s
`KannCapableTypes` schließt `teacher_subject_assignment` explizit aus
("physically/structurally necessary and must always stay 'must'").
**Konsequenz:** Lehrerkontinuität kann nicht als lockerbares
Solver-Constraint ausgedrückt werden - nur durch Wiederverwendung
desselben Lehrer-Namens beim Bau des Kl.6-Szenarios. `Solver.vb` und
`Validation.vb` bleiben in dieser Phase komplett unverändert.

## Mechanik

### `LehrerKontinuitaetFixture.vb` (neu, eigenständig)

Bewusst **nicht** Teil von `GymnasiumSekIFixture.vb`: dessen
`AddUniformSubject` gruppiert Klassen zug-übergreifend in Blöcke
(`classesPerTeacher=4..8`) - eine strikte Pro-Zug-Kontinuität braucht
stattdessen `classesPerTeacher=1` (ein dedizierter Lehrer pro Zug pro
Fach), was die bestehende, umfangreich getestete 30-Klassen-Fixture und
alle darauf aufbauenden Zahlen (Phase 2.13s "82 kombinierte Lehrer" etc.)
verändert hätte. Stattdessen ein neues, kleines Modul: 5 Zug-Klassen
("5a".."5e" bzw. "6a".."6e"), Fächer/Wochenstunden 1:1 aus
`GymnasiumSekIFixture.vb`s eigener Kontingentstundentafel übernommen.

- **Fortbestehende Fächer** (Deutsch, Mathematik, Englisch, Erdkunde,
  Biologie, Sport, Musik, Kunst, Religion - existieren laut
  `GymnasiumSekIFixture.vb` sowohl in Kl.5 als auch Kl.6): jeder Zug
  bekommt in Kl.5 einen dedizierten Lehrer pro Fach.
- **2. Fremdsprache** (Französisch a/c/e, Latein b/d): NEU ab Kl.6, kein
  Kl.5-Pendant - hier ist keine Kontinuität möglich, das wird explizit im
  Code dokumentiert ("kein Vorjahreslehrer möglich, Fach beginnt erst in
  Kl.6") statt stillschweigend übergangen.

### `DeriveVorjahresTeacherMap` (reine Funktion)

Gruppiert eine **geloeste** Kl.5-`ScheduleEntry`-Liste nach (Zug-Buchstabe,
Fach) und liefert die eingesetzte Lehrkraft - der "Vorjahresstundenplan"
in Kurzform. Bewusst über die geloeste Schedule abgeleitet (nicht über die
rohen `teacher_subject_assignment`-Constraints) - erzwingt, dass Kl.5
nachweislich lösbar ist, BEVOR Kl.6 überhaupt gebaut wird, und passt zur
wörtlichen "Vorjahresstundenplan"-Formulierung. Wirft laut bei einer
inkonsistenten Zuordnung (>1 Lehrer pro Zug/Fach) statt das still zu
ignorieren.

### `BuildKlasse6Assignments`/`BuildKlasse6Scenario`/`BuildContinuityReport`

Fortbestehende Fächer übernehmen den Lehrer aus der Vorjahres-Map (fehlt
ein Eintrag: wirft laut - wäre ein echter Fixture-Bug). Die 2.
Fremdsprache mintet frische, garantiert von jedem Kl.5-Namen
unterscheidbare Namen (`"{Fach}-6{Zug}-neu"`). `BuildContinuityReport`
liefert pro Zug/Fach eine Zeile (Kl.5-Lehrer, Kl.6-Lehrer, ob neues Fach) -
Basis für sowohl die Tests als auch die Benchmark-Konsolenausgabe.

## Realmaßstab-Beleg

Live gemessen (`LehrerKontinuitaetBenchmarkTests.KlasseSechsUebernimmtLehrerAusVorjahresstundenplan`,
ungegated, Gesamtlaufzeit ~2,7s):

| Jahrgang | Klassen | Lehrer | Status | 0 Verstöße |
|---|---|---|---|---|
| Kl.5 (Vorjahresstundenplan) | 5 | 45 | Optimal | ✓ |
| Kl.6 (aktuelles Jahr) | 5 | 45 + 5 neu (2.FS) | Optimal | ✓ |

**Kontinuitäts-Quote: 45/45 (100%)** aller fortbestehenden Fach/Zug-
Kombinationen behalten exakt denselben Lehrer von Kl.5 zu Kl.6. Die 5
neuen 2.-Fremdsprache-Zuordnungen (1 pro Zug) bekommen korrekt einen
frischen, unterscheidbaren Lehrer - ehrlich als "kein Vorjahr möglich"
ausgewiesen, nicht versteckt.

Stundenplan-Ausschnitt Zug "a" (gekürzt) zeigt die Kontinuität sichtbar:
Kl.6s "6a" nutzt für Deutsch/Mathematik/Englisch/etc. dieselben
Lehrer-Namen wie Kl.5s "5a" (z.B. "Deutsch-5a" bleibt "Deutsch-5a"), nur
Französisch trägt sichtbar den neuen Namen "Franzoesisch-6a-neu".

## Verifikation

- `DeriveVorjahresTeacherMapGroupsCorrectlyByZugAndSubject`/
  `DeriveVorjahresTeacherMapThrowsOnInconsistentTeacherPerZugSubject`:
  reine Unit-Tests der Ableitungsfunktion, kein Solve() nötig.
- `Klasse6ReusesExactKlasse5TeacherForPersistingSubjects`: kleines,
  eigenständiges 2-Zug/2-Fach-Szenario, per Hand bestätigt exakte
  Lehrer-Übernahme.
- `Klasse6MintsFreshDistinctTeacherForSubjectNewInKlasse6`: bestätigt an
  der echten Fixture, dass die 2.-Fremdsprache-Lehrer garantiert von
  jedem Kl.5-Namen verschieden UND deterministisch/stabil sind.
- `Klasse5AndKlasse6EntitiesAreValid`/`Klasse5AndKlasse6SolveAndVerifyClean`
  (in `RealSchoolFixtureTests.vb`): LLM-freie Sanity-Checks, analog zu den
  bestehenden Fixture-Tests.
- `KlasseSechsUebernimmtLehrerAusVorjahresstundenplan`: der eigentliche
  Benchmark mit Konsolen-Report und harter Assertion auf 100%-Kontinuität.
- Vollständige Regressionssuite grün, 0 Regressionen gegenüber dem
  Phase-2.13-Stand.

## Definition of Done — Status

- [x] `dotnet test TimetableCore.Tests` bleibt vollständig grün, 0
      Regressionen.
- [x] Kl.5- und Kl.6-Szenario lösen beide sauber (0 Verifier-Muss-Verstöße).
- [x] Jedes fortbestehende Fach behält nachweislich denselben Lehrer von
      Kl.5 zu Kl.6, pro Zug (45/45 = 100%).
- [x] Das neue Fach (2. Fremdsprache) bekommt nachweislich einen frischen,
      von jedem Kl.5-Namen unterscheidbaren Lehrer.
- [x] `Solver.vb`/`Validation.vb` bleiben unverändert.
- [x] Dieser Bericht committet, inkl. des recherchierten realweltlichen
      Hintergrunds.
- [x] Committet und gepusht auf `claude/qwen-3.5-sandbox-test-ubfhmo`.
