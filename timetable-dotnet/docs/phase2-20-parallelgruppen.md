# Phase 2.20: Fachgruppen bekommen eine echte Solver-Wirkung ("Parallelgruppe"-Primitive)

Dieser Bericht dokumentiert Phase 2.20 (siehe Plan, Abschnitt "Phase 2.20
(feingeplant)"): der in Phase 2.19 bewusst zurückgestellte nächste Schritt
- die dort rein inerten `Schueler`/`Gruppe`-Stammdaten bekommen jetzt eine
echte Wirkung auf den gelösten Stundenplan.

## Kontext

Nutzer stellte fest, dass die in Phase 2.19 ergänzten Religion-ev-/
Religion-kath-/Ethik-Gruppen im generierten Stundenplan gar nicht
auftauchten - stattdessen weiterhin das alte, monolithische Fach
"Religion". Kein Bug, sondern der explizit dokumentierte Zwischenstand aus
Phase 2.19. Diese Phase behebt das: die drei Gruppen erscheinen jetzt
tatsächlich als gleichzeitig stattfindende, parallele Unterrichtseinheiten.

## Nutzerentscheidungen

1. Jetzt implementieren (nicht länger zurückstellen).
2. Ethik UND jede Religionsart bekommen je ein EIGENES Fach
   (`Religion-ev`, `Religion-kath`, `Ethik`) statt eines gemeinsamen
   "Religion"-Fachs.
3. Die Lehrkraft je Gruppe wird automatisch durch
   `Lehrereinsatzplanung.SolveLehrereinsatz` bestimmt (CP-SAT), nicht
   manuell vorgegeben.
4. Die drei neuen Fach-Spezialisten im Referenzbeispiel werden als 3 NEUE,
   dedizierte Teilzeitlehrer besetzt (je 8h, analog zum bestehenden
   Englischlehrer-1-Muster) - ersetzt den bisherigen Religionslehrer-1
   (16h).

## Zentraler technischer Befund

Ein vorab beauftragter Plan-Agent verifizierte live gegen den echten Code:
würden `Religion-ev`/`Religion-kath`/`Ethik` einfach als 3 unabhängige,
normal geplante Fächer je Klasse eingeführt, würde `no_overlap(class="1a")`
(summiert ALLE Sessions einer Klasse pro Slot, erzwingt `<= 1`) automatisch
verhindern, dass mehr als eine der drei gleichzeitig läuft. Ein naiver Fix
(die drei Lesson-Variablen per Gleichheit verkoppeln) macht es NOCH
schlimmer: `no_overlap` würde dieselbe (jetzt gleiche) Variable dreifach in
die Summe aufnehmen (`3x <= 1`), was sie permanent auf 0 zwingt. Zwei
bestehende Mechanismen wurden gezielt gegengeprüft und als NICHT
wiederverwendbar bestätigt: `shared_resource_conflict` tut das Gegenteil
(verhindert Gleichzeitigkeit); das Kursstufen-Schienenmodell (Phase 2.11)
löst Synchronisation nur über eine komplett separate Nebenrechnung, was
hier nicht geht, da Religion/Ethik im SELBEN `no_overlap("1a")`-Slot-Budget
konkurrieren müssen wie Deutsch/Mathe derselben Klasse.

## Die neue Primitive: `parallel_group`

Neuer, immer harter JSON-Constraint-Typ, siehe
`docs/json-constraints-reference.md` Abschnitt 5 für die vollständige
Referenz:

```json
{ "type": "parallel_group",
  "classes":  ["1a", "1a", "1a", "1b", "1b", "1b"],
  "subjects": ["Religion-ev", "Religion-kath", "Ethik", "Religion-ev", "Religion-kath", "Ethik"],
  "teachers": ["Religionslehrer-ev-1", "Religionslehrer-kath-1", "Ethiklehrer-1", "Religionslehrer-ev-1", "Religionslehrer-kath-1", "Ethiklehrer-1"] }
```

**`Solver.vb`-Mechanik** (Pre-Pass in `BuildCoreModel`, vor
`ApplyConstraints`): pro Constraint und pro (Tag,Periode) EINE geteilte
BoolVar; jedes Mitglied-Tripel wird per `model.Add(lesson(key) =
parallelVar)` daran gekoppelt - erzwingt automatisch identische Slots über
alle Mitglieder hinweg, auch über mehrere echte Klassen. `ApplyConstraints`s
`"no_overlap"`-Fall (für `resource="class"`/`"teacher"`, NICHT `"room"`)
partitioniert Sessions zuerst nach Parallelgruppen-Zugehörigkeit und trägt
pro Gruppe nur EINEN Term (die geteilte `parallelVar`) statt je einem Term
pro Mitglied zur Summe bei - sonst würde die (jetzt gleiche) Variable
mehrfach gezählt und permanent auf 0 gezwungen.

## Umsetzungsreihenfolge (2.20a-f, jede Stufe einzeln live verifiziert)

- **2.20a** - `Stammdaten.vb`/`StammdatenValidation.vb`: `Gruppe` bekommt
  drei neue optionale Felder (`FachName`, `Klassenstufe`,
  `Parallelverbund`), neuer Helper `Stammdaten.KlassenOfGruppe`. Neue harte
  Validierung: alle Gruppen eines Parallelverbunds haben `FachName`
  gesetzt, paarweise verschiedene `FachName`, identische `Klassenstufe`
  UND identisches `WochenstundenSoll`/`BlockLength` (sonst wäre das
  CP-SAT-Modell strukturell unlösbar - die erzwungene Gleichheit koppelt
  Stundenzahlen). Überschneidende Schüler-IDs innerhalb eines
  Parallelverbunds sind ebenfalls ein harter Fehler.
- **2.20b** - `Solver.vb`: die oben beschriebene Pre-Pass +
  `no_overlap`-Deduplizierung, `Validation.vb`s `FieldEntityKey` um
  `{"teachers","teachers"}`/`{"subjects","subjects"}` ergänzt. Gate: neuer
  Hand-Smoke-Test `ParallelGroupSynchronizesSessionsAcrossClasses`
  (2 Klassen x 3 Fächer x 3 Lehrer) - direkt über `Solver.Solve` gelöst,
  BEVOR `Lehrereinsatzplanung.vb` überhaupt angefasst wurde.
- **2.20c** - `Verifier.vb`: neuer, unabhängig re-derivierter
  `"parallel_group"`-Check (alle Mitglieder pro Slot entweder ALLE oder
  KEINES aktiv). **Dabei ein echter, live entdeckter Bug behoben:** die
  bereits BESTEHENDE `"no_overlap"`-Prüfung in `Verifier.CollectViolations`
  hatte KEINE Parallelgruppen-Kenntnis und flaggte deshalb jede
  legitim-gleichzeitige Session als falsch-positive "doppelt belegt" -
  behoben durch dieselbe Deduplizierungslogik, die bereits in `Solver.vb`
  für den Muss-Constraint selbst existierte (unabhängig dupliziert, kein
  geteilter Code). Zusätzlich eine Gruppen-bewusste Ergänzung von
  `VerifyLehrereinsatz`: jede (Gruppe,Fach)-Kombination muss über alle
  real umspannten Klassen hinweg vom SELBEN Lehrer unterrichtet werden.
  Gate: `ParallelGroupDetectsDesynchronizedScheduleAsViolation` (Verifier
  erkennt ein von Hand gebautes, absichtlich desynchronisiertes Schedule)
  + `GruppeWithDifferingTeachersAcrossClassesIsDetected`.
- **2.20d** - `Lehrereinsatzplanung.vb`: `AssignKey.IstGruppe`-Flag; für
  ein Gruppen-geführtes Fach wird die normale Pro-Klasse-Variablenerzeugung
  übersprungen und stattdessen EINMAL pro Gruppe erzeugt (kritisch: das
  Deputat wird dadurch korrekt einmal, nicht einmal pro real umspannter
  Klasse, gezählt). Bei der Lösungsextraktion wird eine Gruppen-Zuweisung
  sofort auf alle echten Klassen expandiert (`Stammdaten.KlassenOfGruppe`)
  - hält `LehrereinsatzResult.Zuweisungen` für jeden nachgelagerten
  Konsumenten uniform (nur echte Klassennamen). Akzeptierte Nebenwirkungen
  (dokumentiert, keine gesonderte Rückfrage nötig): Gruppen-geführte
  Fächer fallen automatisch aus der Klassenlehrer-Bündelung heraus;
  `MaxKlassen`/`MaxFaecher` zählen eine Gruppe wie eine zusätzliche Klasse.
  Gate: `GruppenGefuehrtesFachZaehltDeputatEinmalProGruppe` (3 Lehrer, je
  exakt 1 Gruppe, Deputat nachweislich nicht verdoppelt).
- **2.20e** - `BuildAssignmentConstraints`: für eine Gruppen-basierte
  Zuweisung werden `teacher_subject_assignment`/`weekly_hours`/
  `consecutive_required` einmal PRO ECHTER KLASSE emittiert, plus genau
  ein `parallel_group`-Constraint pro Parallelverbund. Gate:
  `GruppenBasierteZuweisungLoestEndToEndMitSynchronenSessions` - komplette
  Pipeline (Stammdaten -> Lehrereinsatzplanung -> BuildAssignmentConstraints
  -> Solver.Solve -> Verifier.VerifySchedule), 0 Verstöße, alle 6 Sessions
  landen auf den erwarteten 2 gemeinsamen Slots.
- **2.20f** - Referenzbeispiel + Formatting-Fix (siehe unten) + Doku.

## Ein zweiter, live entdeckter Bug: `Formatting.ToClassGrids`

Beim finalen Live-Neulauf von `bw-grundschule-beispiel` zeigte der
gerenderte Stundenplan zunächst nur "Ethik" an den synchronisierten
Slots - Religion-ev und Religion-kath waren unsichtbar. Ursache:
`Formatting.ToClassGrids` schrieb pro Schedule-Zeile unbedingt in
`grids(class)(day)(period)`, ohne Kollisionsbewusstsein - vor Phase 2.20
garantierte `no_overlap(class)` immer höchstens EINE Session pro
(Klasse,Tag,Periode), jetzt überschrieb die zuletzt verarbeitete
Parallelgruppen-Session einfach die beiden vorherigen. Behoben durch
Gruppierung nach (Klasse,Tag,Periode) VOR dem Schreiben in die Grid-Zelle:
mehrere gleichzeitige Sessions werden zu einer kombinierten `GridCell`
zusammengeführt (`"Ethik / Religion-ev / Religion-kath"`,
`"Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1"`). Für
jedes bestehende Fixture (immer genau 1 Session pro Slot) ist das ein
No-op - bewiesen durch die unveränderte `FormattingTests.vb`-Suite plus
einen neuen, gezielten Test
`ToClassGridsCombinesSimultaneousParallelGroupSessions`.

## Ein dritter, live entdeckter Bug: `Formatting.FormatLehrereinsatzMarkdown`

Nach dem Formatting-Fix zeigte `lehrerzuteilung.md` für die drei neuen
Spezialisten `Soll=8h`/`Ist=16h` an - obwohl das CP-SAT-Modell die
Deputat-Abweichung intern korrekt auf 0 berechnet hatte (bewiesen durch
`GruppenGefuehrtesFachZaehltDeputatEinmalProGruppe`). Ursache: die
Report-Funktion summierte `WochenstundenSoll` naiv über JEDE Zeile in
`result.Zuweisungen` - für einen Gruppen-geführten Lehrer gibt es aber
(seit der 2.20d-Expansion) eine Zeile PRO ECHTER, von der Gruppe
umspannter Klasse, nicht eine pro tatsächlich gehaltener Unterrichts-
einheit. Behoben durch Gruppierung nach (Fach,Klassenstufe) VOR der
Summierung: ist diese Kombination laut `bestand.Gruppen` Gruppen-geführt,
zählen ihre Wochenstunden nur EINMAL (unabhängig von der Anzahl real
umspannter Klassen); jeder normale, nicht-Gruppen-geführte Fall bleibt
byte-identisch zum bisherigen Verhalten (ein Term pro echter
Zuweisungszeile). Neuer Test
`FormatLehrereinsatzMarkdownCountsGruppenFachOnceNotPerRealClass` beweist
die Korrektur; die unveränderte `FormatLehrereinsatzMarkdownRendersLehrkraefteAndKlassenlehrerTables`
bestätigt keine Regression für den bereits bestehenden Nicht-Gruppen-Fall.

## Referenzbeispiel `bw-grundschule-beispiel`

Das bisherige Fach "Religion" (2h, alle 4 Klassenstufen,
Religionslehrer-1 mit 16h) wurde ersetzt durch drei Fächer
`Religion-ev`/`Religion-kath`/`Ethik` (je identisch 2h/Klassenstufe 1-4)
und drei neue Teilzeitlehrer (`Religionslehrer-ev-1`/
`Religionslehrer-kath-1`/`Ethiklehrer-1`, je 8h = 4 Klassenstufen x 2h,
`klassenlehrer_faehig: false`). Die 12 bestehenden Gruppen bekamen ihr
passendes `fach_name`, ihre `klassenstufe` (1-4) sowie ein gemeinsames
`parallelverbund` pro Klassenstufe (`Religion-Ethik-Kl{1..4}`). Die
bestehende `teacher_availability`-Regel in `constraints.yaml`
(Teilzeit-Hinweis "freitags nicht im Haus") wurde von der alten, jetzt
nicht mehr existenten `Religionslehrer-1`-Identität auf alle drei neuen
Spezialisten übertragen.

**Live-Ergebnis** (`dotnet run --project SchoolTestRunner -- run
bw-grundschule-beispiel`): PASS, `Lehrereinsatzplanung=Optimal
(Objective=2800)`, `Verstoesse=0`. Der gerenderte `output/stundenplan.md`
zeigt für jede der 8 Klassen (1a-4b) an genau 2 gemeinsamen Wochenslots
`"Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 /
Religionslehrer-ev-1 / Religionslehrer-kath-1)"` - synchronisiert über
BEIDE Parallelklassen jeder Klassenstufe hinweg, statt des alten
monolithischen "Religion"-Fachs.

## Definition of Done

- `dotnet test TimetableCore.Tests` blieb nach jedem Teilschritt
  vollständig grün, 0 Regressionen gegenüber dem Phase-2.19-Stand (finaler
  Stand: 189 bestanden, 11 korrekt übersprungen ohne `RUN_LLM_TESTS`).
- Neue Hand-Smoke-Tests für jede der 5 neuen Mechaniken: Solver-Primitive
  (`ParallelGroupSynchronizesSessionsAcrossClasses`), Verifier
  (`ParallelGroupDetectsDesynchronizedScheduleAsViolation`,
  `GruppeWithDifferingTeachersAcrossClassesIsDetected`),
  Lehrereinsatzplanung-Zuweisung
  (`GruppenGefuehrtesFachZaehltDeputatEinmalProGruppe`), End-to-End
  (`GruppenBasierteZuweisungLoestEndToEndMitSynchronenSessions`),
  Formatting (`ToClassGridsCombinesSimultaneousParallelGroupSessions`).
- `bw-grundschule-beispiel` läuft live durch, PASS, 0 Verstöße, und der
  gerenderte `output/stundenplan.md` zeigt nachweislich für jede
  Klassenstufe drei zeitgleiche Religion-ev-/Religion-kath-/
  Ethik-Sessions in beiden Parallelklassen statt des alten
  monolithischen Fachs.
- `docs/json-constraints-reference.md` dokumentiert `parallel_group`
  vollständig (Feldschema, Beispiel, Mechanik, "immer hart").
- Drei echte, während der Live-Verifikation gefundene Bugs behoben (nicht
  nur angenommen korrekt): die fehlende Parallelgruppen-Deduplizierung im
  bestehenden `Verifier.vb`-`no_overlap`-Check, die Kollisions-blinde
  `Formatting.ToClassGrids`-Zellenzuweisung, und die naive (verdoppelnde)
  Ist-Stunden-Summierung in `Formatting.FormatLehrereinsatzMarkdown`.
- Committet und gepusht auf `claude/qwen-3.5-sandbox-test-ubfhmo`.

**Kritische Dateien:**
- `timetable-dotnet/TimetableCore/Solver.vb` - `parallel_group`-Pre-Pass +
  `no_overlap`-Term-Deduplizierung.
- `timetable-dotnet/TimetableCore/Stammdaten.vb` - neue `Gruppe`-Felder +
  `KlassenOfGruppe`.
- `timetable-dotnet/TimetableCore/StammdatenValidation.vb` - neue
  Parallelverbund-Konsistenzprüfungen.
- `timetable-dotnet/TimetableCore/Lehrereinsatzplanung.vb` -
  `AssignKey.IstGruppe`, Gruppen-Zweige, `parallel_group`-Emission.
- `timetable-dotnet/TimetableCore/Validation.vb` - `FieldEntityKey`-
  Ergänzung für `parallel_group`.
- `timetable-dotnet/TimetableCore/Verifier.vb` - `parallel_group`-Check,
  `no_overlap`-Fix, Gruppen-bewusste `VerifyLehrereinsatz`-Ergänzung.
- `timetable-dotnet/TimetableCore/Formatting.vb` - `ToClassGrids`-Fix.
- `timetable-dotnet/TimetableCore.Tests/{SolverTests,VerifyLehrereinsatzTests,
  LehrereinsatzplanungTests,FormattingTests}.vb` - neue Tests.
- `timetable-dotnet/docs/json-constraints-reference.md` - neuer Abschnitt.
- `timetable-dotnet/tests/bw-grundschule-beispiel/input/{stammdaten,constraints}.yaml` -
  Fach-/Lehrer-/Gruppen-Anpassung, live neu durchlaufen.
