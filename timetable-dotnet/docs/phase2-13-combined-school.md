# Phase 2.13: Echte Lehrer-/Raum-Überschneidung zwischen Sek I und Kursstufe

Dieser Bericht dokumentiert Phase 2.13 (siehe Plan, Abschnitt "Phase 2.13
(feingeplant)"): eine berechtigte Nutzerfrage zur vorgeschlagenen Sek-I/
Kursstufe-Parallelisierung ("in der Realität gibt es doch Überschneidungen
was Lehrer und Räume anbelangt?") deckte eine seit Phase 2.11 explizit
dokumentierte, aber nie behobene Modellgrenze auf: Sek I (75 Lehrer) und
Kursstufe (35 Lehrer) nutzten komplett getrennte Namenspools - ein reines
Konstruktionsartefakt, keine bewusste Modellierung.

## Nutzerentscheidungen

1. **Echte Durchsetzung, nicht nur kosmetische Namen** - der Solver muss
   eine Doppelbelegung eines geteilten Lehrers/Raums tatsächlich
   verhindern.
2. **Konfigurierbare Reihenfolge**: Default zuerst Sek I dann Kursstufe,
   optional auch umgekehrt - beide Richtungen müssen echt funktionieren.
3. **Umfassende Überlappung**: kombinierter Lehrerpool auf realistische
   ~80-90 reduziert (statt 75+35=110 getrennte), Spezialräume (Turnhallen/
   NaWi) von beiden Hälften geteilt nutzbar.

## Mechanik

### Geteilter Lehrer-/Raumpool (`SharedSchoolPool.vb`)

Ein neues, drittes Modul (analog zu `AssignmentScenarioBuilder.vb`, das
beide Fixtures bereits gemeinsam nutzen) mit einer festen Pool-Größe pro
geteiltem Fach - hergeleitet aus `GymnasiumSekIFixture.vb`s **eigener**,
bereits bestehender Pro-Fach-Lehrerzahl. `AddUniformSubject` (Sek I) und
`AddSections` (Kursstufe) greifen für geteilte Fächer auf denselben Pool
zu, mit einem fach-übergreifenden (nicht kursart-getrennten) Rundlauf-
Zähler auf Kursstufen-Seite, damit kein einzelner Lehrer zwei gleichzeitige
Kursstufen-Kurse bekommt (jeder Pool-Lehrer maximal ein Kurs, plus dessen
bestehende Sek-I-Last).

**Ergebnis (live geprüft, nicht nur berechnet):** Sek I bleibt bei 75
Lehrern **byte-identisch** (gleiche Namen, gleiche Anzahl), Kursstufe bei
35. **Kombiniert: 82 Lehrer** (Zielbereich ~80-90 erreicht), davon **28**
tatsächlich geteilt, **0** überlastet (kein Lehrer mit >1 gleichzeitigem
Kursstufen-Kurs).

### Geteilte Spezialräume (`Raumzuordnung.vb`)

Neuer optionaler `specialRoomsBySubject`-Parameter auf
`BuildRaumzuordnungScenario` - ein Fach mit echtem Raumbedarf (Sport →
Turnhallen, Biologie/Chemie/Physik → NaWi-Räume, etc.) bekommt diese Liste
statt der generischen `KursraumN`-Räume als `allowed_rooms`. `Nothing`
(Default) reproduziert das bisherige Verhalten byte-identisch.

### Pro-Schiene-Platzhalter-Lehrer + externe Verfügbarkeit (`Schienenraster.vb`)

Die eigentliche architektonische Kernarbeit dieser Phase: bislang hatten
**alle** Schienen denselben geteilten Platzhalter-Lehrer `"_schiene"` -
eine `teacher_availability`-Regel darauf hätte fälschlich **jede** Schiene
blockiert, nicht nur die eine betroffene. Jede Schiene bekommt jetzt eine
**eigene** Platzhalter-Identität (`"_schiene_{SchieneId}"`) - ändert nichts
an der bestehenden `no_overlap(class:=Gruppenname)`-Semantik (die ist nach
`.ClassName` indiziert, nie nach `.Teacher`), und die beiden bestehenden
Tests, die exakt auf `"_schiene"` prüften, blieben unverändert grün. Damit
kann ein neuer optionaler `externalTeacherBusySlots`-Parameter pro Schiene
gezielt nur deren tatsächlich betroffene Slots sperren (Union der Sek-I-
Belegzeiten aller ihrer Kurs-Lehrer) - per isoliertem Test bewiesen, dass
benachbarte Schienen derselben Konfliktgruppe **nicht** mitblockiert
werden.

Spiegelbildlich bekam `Raumzuordnung.vb` einen `externalRoomBusySlots`-
Parameter (`forbidden_slot(scope:="room", ...)` - bereits bestehende,
unveränderte Solver.vb-Mechanik).

### `CombinedSchool.vb`: Orchestrierung beider Reihenfolgen

`SolveCombinedSchool(sekIData, kursstufeData, order, ...)` ändert
`Solver.Solve`/`SolveTop`/`SolveKursstufe` **nicht** - "Geteiltheit" wird
rein strukturell aus der Namensschnittmenge beider `entities.teachers`/
`entities.rooms`-Listen abgeleitet:

- **KursstufeFirst:** strukturell einfach - `Solver.SolveKursstufe`
  unverändert aufgerufen, danach eine `DeepClone()`-Kopie der Sek-I-JSON
  um `teacher_availability`/`forbidden_slot(room)`-Constraints für die
  geteilten, jetzt belegten Namen ergänzt, dann `Solver.Solve` darauf
  unverändert aufgerufen.
- **SekIFirst (Default):** die 3-Stufen-Pipeline wird hier manuell
  nachgebaut (mirrort das bereits in `GsgCompleteScenarioSolveTopTests.vb`
  etablierte Muster), damit `externalTeacherBusySlots`/
  `externalRoomBusySlots` an Schienenraster/Raumzuordnung durchgereicht
  werden können - `Solver.SolveKursstufe` selbst bleibt unangetastet.

`BuildMergedVerificationScenario` baut eine `data`-JSON mit frischen
`no_overlap(teacher)`/`no_overlap(room)`-Constraints für die **volle
Vereinigung** beider Namenslisten - keine Änderung an `Verifier.vb` nötig,
dessen bestehende `no_overlap`-Erkennung ist bereits schedule-listen-
agnostisch.

## Live-diagnostizierter Fehlermodus (beim Realmaßstab-Benchmark gefunden)

**Symptom:** der erste Lauf des ungegateten Sek-I-zuerst-Realmaßstab-Tests
schlug mit 27 gemeldeten Verstößen fehl - u.a. "Physik (LK-Physik-1,
Mo/7) in Raum Kursraum2, erlaubt sind nur ['NaWi1', 'NaWi2', 'NaWi3']".

**Root Cause:** `BuildMergedVerificationScenario` hatte ursprünglich
**beide Hälften eigene Original-Constraints** in die Merge-Szenario-JSON
übernommen. `Verifier.vb`s `room_requirement`-Prüfung matcht aber **rein
über den Fach-Namen-String**, ohne jede Herkunfts-/Klassen-Filterung
(`CollectViolations`, Case `"room_requirement"`). Da Sek I und Kursstufe
absichtlich denselben Fach-Namen "Physik" verwenden (das ist ja gerade der
Sinn dieser Phase), griff Sek I's eigene `room_requirement(subject:=
"Physik", NaWi-only)`-Regel in der Merge-Prüfung fälschlich auch auf
Kursstufes eigene Physik-Kurse durch - obwohl deren TATSÄCHLICHER Solve
diese Einschränkung nie kannte (kein `specialRoomsBySubject` war für den
Test verdrahtet).

**Fix:** `BuildMergedVerificationScenario` übernimmt die Original-
Constraints beider Hälften jetzt **nicht mehr** - nur noch die frischen,
namens-vereinigten `no_overlap(teacher)`/`no_overlap(room)`-Regeln. Das
ist nicht nur der Bugfix, sondern die konzeptionell richtigere Lösung:
jede Hälfte garantiert ihre eigenen Constraints bereits durch ihren
eigenen erfolgreichen Solve - eine erneute Prüfung über die gemeinsame
JSON wäre redundant gewesen, und bei zufällig gleichen Fach-Namen sogar
aktiv falsch. Die Merge-Prüfung muss nur genau das beweisen, wofür sie
gebaut wurde: keine Doppelbelegung eines geteilten Namens über beide
Hälften hinweg.

## Realmaßstab-Beleg

| Reihenfolge | Ergebnis |
|---|---|
| **SekIFirst** (ungegated, 1m33s) | 82 kombinierte Lehrer, **0 Verstöße** |
| **KursstufeFirst** (`RUN_SLOW_BENCHMARKS=1`-gegated, 40s) | **0 Verstöße** |

Beide Reihenfolgen lösen das komplette GSG-Szenario (30 Klassen/75
Sek-I-Lehrer + 35 Kursstufen-Lehrer, 82 kombiniert) end-to-end mit
nachweislich 0 Doppelbelegungen eines geteilten Namens - der direkte,
ehrliche Beleg für die Kernaussage dieser Phase. Auffällig:
KursstufeFirst ist mit 40s deutlich schneller als SekIFirst mit 1m33s -
plausibel, da in dieser Richtung der teure Sek-I-Solve (~93s) erst NACH
der schnellen Kursstufe (&lt;1s) läuft und dabei von den zusätzlichen
`teacher_availability`/`forbidden_slot`-Einschränkungen aus der bereits
gelösten Kursstufe profitiert (kleinerer effektiver Suchraum) statt sie
selbst erzeugen zu müssen.

## Verifikation

- `SharedTeacherNeverDoubleBookedInEitherSolveOrder`: deterministischer
  Dual-Order-Test - ein bewusst geteilter Lehrer, enger Slot-Raum (1 Tag ×
  2 Perioden), beide Reihenfolgen, 0 Verifier-Verstöße.
- `ExternalTeacherBusySlotsOnlyBlocksTheAffectedSchiene`
  (`SchienenrasterTests.vb`): beweist, dass die Pro-Schiene-Sperre NUR die
  betroffene Schiene trifft, nicht benachbarte Schienen derselben
  Konfliktgruppe.
- `SpecialRoomsBySubjectRestrictsAllowedRoomsForThatSubject`/
  `ExternalRoomBusySlotsExcludesThatRoomAtThePinnedSlot`
  (`RaumzuordnungTests.vb`): beweisen beide neuen `Raumzuordnung.vb`-
  Parameter isoliert.
- `SekIFirstReportsCleanInfeasibleWhenSharedRoomBlocksTheOnlyPinnedSlot`:
  beweist das dokumentierte Restrisiko der Sek-I-zuerst-Richtung (ein
  geteilter Raum blockiert den einzigen gepinnten Slot) sauber als
  `Infeasible`, nicht als unklarer Absturz.
- Vollständige Regressionssuite nach jedem Teilschritt grün, 0
  Regressionen gegenüber dem Phase-2.12-Stand.

## Dokumentiertes Restrisiko (Sek-I-zuerst-Richtung)

Kursstufens Raumzuordnung (Stufe C) wählt Räume erst, **nachdem** Tag/
Periode bereits aus Stufe B fixiert ist. Ist ein geteilter Spezialraum
für GENAU den gepinnten Slot durch Sek I komplett belegt, kann Stufe C
nicht mehr ausweichen - `Infeasible`, kein Rückfallpfad ohne Stufe B selbst
raumfähig zu machen (ein deutlich größerer Eingriff, bewusst nicht Teil
dieser Phase). Durch einen eigenen Test explizit demonstriert statt
stillschweigend in Kauf genommen; in der Praxis abgemildert (nicht
eliminiert) dadurch, dass jede geteilte Spezialraum-Kategorie mehrere
Räume hat (3 Turnhallen, 3 NaWi-Räume), nicht nur einen.

## Definition of Done — Status

- [x] `dotnet test TimetableCore.Tests` bleibt vollständig grün, 0
      Regressionen gegenüber dem Phase-2.12-Stand.
- [x] Kombinierte, tatsächlich geteilte Lehrerzahl liegt im Zielbereich
      ~80-90 (82, empirisch bestätigt).
- [x] Ein Kursstufen-Sport-Kurs landet nachweislich in einem mit Sek I
      geteilten Spezialraum.
- [x] Beide Lösungsreihenfolgen verhindern nachweislich eine
      Doppelbelegung eines geteilten Lehrers - belegt durch denselben
      Dual-Order-Test.
- [x] Das Raum-Restrisiko der Sek-I-zuerst-Richtung ist durch einen
      dedizierten Test explizit demonstriert.
- [x] `Solver.Solve`/`SolveTop`/`SolveKursstufe`/`Verifier.vb` bleiben
      unverändert.
- [x] Dieser Bericht committet.
- [x] Committet und gepusht auf `claude/qwen-3.5-sandbox-test-ubfhmo`.
