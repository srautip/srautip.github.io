# Phase 2.11: Kursstufe/Kurssystem (Schienenmodell)

## Kontext

Nutzerwunsch, das Tool end-to-end um die Faehigkeit zu erweitern, auch die
gymnasiale Oberstufe (Kl. 11/12, BW-Qualifikationsphase) mit ihrem
individualisierten Kurswahlsystem zu planen - im Gegensatz zum bisherigen,
ausschliesslich klassenbasierten Modell (`Session = (ClassName, Subject,
Teacher)`), das in jeder Phase davor (0-2.10) durchgehend vorausgesetzt
wurde. Nutzerentscheidungen aus der Feinplanungsrunde: Schienenmodell (nicht
direkte Pro-Schueler-CP-SAT-Overlap-Constraints), alle 4 End-to-End-Bereiche
(Solver-Kern, LLM-Extraktion, realistisches Fixture, Formatierung), und
Wahlprofile-mit-Schueleranzahl statt einzeln benannter Schueler.

## Architektur-Ergebnis: drei CP-SAT-Stufen

Die urspruengliche Hypothese ("Schiene = virtuelle Klasse, die Kurse
traegt") wurde live gegen den echten Code widerlegt: `no_overlap(class=X)`
in `Solver.vb` erzwingt hoechstens EINE Lektion von X pro Slot - das
Gegenteil dessen, was eine Schiene braucht (mehrere Kurse SYNCHRON). Die
korrigierte Form - "alle Schienen zusammen sind die Faecher EINER
gemeinsamen Pseudo-Klasse" - ist die tatsaechlich umgesetzte Loesung, mit
einer wichtigen Praezisierung, die erst beim Bau des realistischen
Fixtures (2.11h) empirisch auffiel (siehe unten): nicht EINE gemeinsame
Pseudo-Klasse fuer ALLE Schienen, sondern eine PRO ZUSAMMENHANGSKOMPONENTE
des "teilt sich ein Wahlprofil"-Graphen.

- **(A) Kursblockung** (`Kursblockung.vb`) - neues CP-SAT-Teilmodell:
  Kurs-zu-Schiene-Zuordnung, Wahlprofil-Kollisionsfreiheit,
  Lehrkraft-Kollisionsfreiheit, optionale Schienen-Kapazitaet.
- **(B) Schienenraster** (`Schienenraster.vb`) - 100% Wiederverwendung von
  `Solver.Solve()`/`BuildModel()` unveraendert, ueber eine synthetisch
  konstruierte JSON-Szenario-Instanz mit einer Pseudo-Klasse pro
  Konfliktgruppe.
- **(C) Raumzuordnung** (`Raumzuordnung.vb`) - ebenfalls Wiederverwendung
  der bestehenden `room_requirement`/`no_overlap`-Mechanik, mit aus (B)
  abgeleiteten, per `forbidden_slot` gepinnten Tag/Periode-Werten.

`Solver.SolveKursstufe` orchestriert alle drei Stufen; `Solver.vb`s
bestehende Funktionen (`BuildModel`/`BuildCoreModel`/`ApplyConstraints`/
`AddBlockConstraint`/`Solve`/`SolveTop`) bleiben ueber die gesamte Phase
byte-identisch unveraendert - jede Wiederverwendung erfolgt ausschliesslich
ueber neue Szenario-Konstruktion in den drei neuen Modulen.

## Zwei empirische Korrekturen beim Bau des realistischen Fixtures

Der erste Versuch, `Solver.SolveKursstufe` auf ein realistisch grosses
Szenario (14 Schienen, ~32 Kurse, 14 Wahlprofile) anzuwenden, deckte zwei
echte, in der urspruenglichen Planung nicht vorhergesehene Probleme auf -
beide durch iteratives Nachjustieren und Nachmessen behoben (gleiche
Disziplin wie bei jeder vorherigen realen Fixture, z.B.
`GymnasiumSekIFixture`s Lehrer-Namenskollisions-Bug):

1. **Schienenraster-Skalierbarkeit:** die urspruengliche Umsetzung (2.11c)
   packte ALLE Schienen in eine einzige Pseudo-Klasse, wodurch
   `no_overlap` ALLE Schienen schulweit paarweise zeitlich trennte -
   sicher, aber verlangt so viele Wochen-Slots wie die SUMME aller
   Schienen-Wochenstunden (hier: 48h > 40 verfuegbare Slots). Behoben durch
   `GroupSchienenByConflict`: Zusammenhangskomponenten des
   "teilt-sich-ein-Wahlprofil"-Graphen werden je eine eigene Pseudo-Klasse,
   Schienen in verschiedenen Komponenten laufen frei parallel.
2. **Kursblockung ohne Kapazitaetsgrenze haeuft beliebig viele Kurse auf
   einer Schiene an** (kein Lastverteilungs-Ziel in der reinen
   Machbarkeits-Zielfunktion) - ein erster Testlauf zeigte 11 Kurse
   gleichzeitig auf einer Schiene, unloesbar bei nur 8 Raeumen in Stufe C.
   Behoben durch ein neues, optionales `entities.schienen[].capacity`-Feld
   (rueckwaertskompatibel: ohne das Feld bleibt eine Schiene unbegrenzt).

Zusaetzlich zeigte sich, dass 4 GK2-Schienen fuer 14 Wahlprofile, die JEDES
exakt 4 GK2-Kurse waehlen, eine zu enge Bijektions-Anforderung sind (auch
mit ausreichender Gesamtkapazitaet Infeasible) - behoben durch mehr
GK2-Schienen (8 statt 4) statt eines weiteren Modell-Umbaus, sowie eine
Anhebung auf 11 Perioden/Tag (die groesste Konfliktgruppe braucht ca. 50
Wochenstunden).

## Umfang je Teilphase

| Teilphase | Inhalt | Neue Tests |
|---|---|---|
| 2.11a | Datenmodell (`entities.kurse`/`schienen`, `kurswahl`), `ValidateKursstufeEntities` | 12 |
| 2.11b | `Kursblockung.vb` (Stufe A) | 4 |
| 2.11c | `Schienenraster.vb` (Stufe B) | 2 |
| 2.11d | `Raumzuordnung.vb` (Stufe C) + `Solver.SolveKursstufe` | 5 |
| 2.11e | `Verifier.VerifyKursblockung` | 6 |
| 2.11f | `Formatting.ToWahlprofilGrids`/`FormatKursstufeSchedule` | 3 |
| 2.11g | `LlmExtraction.vb` - Typ `kurswahl`, `CompletenessScoring.ScoreKurswahl(StudentCount)` | 5 + 1 gegateter Live-Test |
| 2.11h (Korrektur) | Schienenraster-Konfliktgruppen | 0 (bestehende Tests beweisen Nicht-Regression) |
| 2.11h | Schienen-Kapazitaet + `KursstufeFixture.vb` (real, 18 Schienen/32 Kurse/14 Wahlprofile) | 4 + 3 |
| 2.11h | `KursstufePromptFixture.vb` (klein, promptfaehig, 10 Schienen/14 Kurse/5 Wahlprofile) | 2 |

Insgesamt: 104 bestanden, 7 gated uebersprungen (0 Regressionen ueber alle
9 Teil-Commits).

## Offener Punkt: keine Live-Verifikation gegen Qwen

In der Sandbox-Session, in der Phase 2.11 umgesetzt wurde, war **kein
Ollama-Server erreichbar** (`curl localhost:11434` schlug fehl). Damit
konnten fuer den neuen `kurswahl`-Typ weder der geforderte isolierte
Live-Diagnose-Lauf noch der volle gegatete E2E-Test noch die
RobustnessRunner-Wiederholungsstudie durchgefuehrt werden - alle 3 bleiben
als offener Folgeschritt dokumentiert, sobald eine Umgebung mit
Ollama-Zugriff verfuegbar ist. Insbesondere ungeklaert: ob die direkte
Ausgabe roher Kurs-IDs durch Qwen zuverlaessig gelingt, oder ob das im Plan
dokumentierte Plan-B (Extraktion von `{subject, kursart}`-Paaren statt roher
IDs, deterministisch in Code aufgeloest) noetig wird. Der neue Test
`LlmExtractionE2EKurswahlDiagnostic` faellt in dieser Umgebung korrekt auf
`Inconclusive` zurueck (wie vorgesehen), ist aber bislang nie gegen echtes
Modellverhalten gelaufen.

Ebenfalls nicht Teil dieser Phase (bewusst zurueckgestellt, siehe
Kursblockung.vb-Kommentar): eine Sekundaer-Zielfunktion fuer Kursblockung
(z.B. Schienen-Auslastung glaetten) - analog zum bereits etablierten Muster
Phase 2.5 (nur Kann) -> 2.8 (SolveTop) -> 2.9 (volle Zielfunktion).

## Kritische Dateien

- `timetable-dotnet/TimetableCore/{Models,Validation,Solver}.vb`
- `timetable-dotnet/TimetableCore/{Kursblockung,Schienenraster,Raumzuordnung}.vb` (neu)
- `timetable-dotnet/TimetableCore/{Verifier,Formatting,LlmExtraction}.vb`
- `timetable-dotnet/TimetableCore.Tests/Fixtures/{KursstufeFixture,KursstufePromptFixture}.vb` (neu)
- `timetable-dotnet/TimetableCore.Tests/RealSchoolFixtureTests.vb`
