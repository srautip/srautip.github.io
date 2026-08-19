# Phase 2.15: Stammdatenverwaltung + Lehrereinsatzplanung-Solver

Dieser Bericht dokumentiert Phase 2.15 (siehe Plan, Abschnitt "Phase 2.15
(feingeplant)"): Nutzerwunsch nach einer dauerhaften Verwaltung von
Schul-Stammdaten (Klassenstufen, Klassen, Räume, Lehrkräfte, Fächer je
Klassenstufe, Fach-Lehrer-Zuordnung, Deputate) sowie einem neuen Solver,
der Lehrkräfte anhand von Abhängigkeiten (Deputatsstunden, Präferenzen,
Klassenlehrer-Bedarf) IDEAL auf Klassen/Fächer verteilt - vorgeschaltet vor
dem bestehenden, unveränderten Stundenplan-Solver. Ausgangspunkt war die
in der vorherigen Konversationsrunde diskutierte Beobachtung: bislang legt
`teacher_subject_assignment` bereits fest, WER eine Klasse unterrichtet -
diese Zuordnung selbst war nie Gegenstand einer Optimierung, sondern immer
fixer Input.

## Nutzerentscheidungen aus der Feinplanungsrunde

1. **Persistenz:** JSON-Dateien (konsistent mit dem bestehenden
   `entities`/`constraints`-Muster), keine eingebettete Datenbank.
2. **Klassenlehrer-Zuweisung:** weiches Ziel (bevorzugt, aber lockerbar) -
   passt zum etablierten Muss/Kann-Prinzip (Phase 2.5).
3. **Deputat:** Zielkorridor mit Toleranz (weiches Optimierungsziel), keine
   exakte Gleichung.
4. **Umfang des ersten Umsetzungsschritts:** schlanker Kern zuerst
   (Vollständigkeit + Qualifikation + Deputat-Korridor + weiche
   Klassenlehrer-Zuweisung + einfache Präferenzen); weitere Constraints
   dokumentiert, aber zurückgestellt (siehe Abschnitt "Zurückgestellte
   Erweiterungen" unten).

## Realweltlicher Hintergrund (Kultusministerium Baden-Württemberg, Stufe 1)

- **Grundschule (Kl. 1-4):** Deutsch, Mathematik, Sachunterricht, Musik,
  Kunst/Werken, Sport, Religion (evangelisch/katholisch) oder Ethik,
  Englisch (seit Schuljahr 2018/19 ab Klassenstufe 3, davor ab Kl. 1). Die
  Kontingentstundentafel legt nur die **Gesamtstundenzahl über den
  gesamten Bildungsgang** fest - die Verteilung auf einzelne
  Klassenstufen entscheidet jede Schule selbst (pädagogischer Freiraum).
  Lehrerdeputat: **28 Wochenstunden**.
- **Gemeinschaftsschule (Kl. 5-10, die "kleine Gesamtschule" in BW):**
  verpflichtende Ganztagsschule; Englisch ab Kl. 5 für alle; Fächerverbund
  BNT (Biologie, Naturphänomene und Technik) in Kl. 5-6; Biologie
  eigenständig ab Kl. 7, Physik ab Kl. 7, Chemie/Gemeinschaftskunde ab
  Kl. 7/8; Wahlpflichtbereich ab Kl. 6 (Technik/AES oder 2. Fremdsprache);
  Unterricht in den Kernfächern auf Niveaustufen G/M/E statt klassischer
  leistungsgetrennter Parallelklassen. Lehrerdeputat: **27 Wochenstunden**
  (28 bei über 50% Einsatz im Grundschulbereich).
- Quellen: Ministerium für Kultus, Jugend und Sport Baden-Württemberg
  (Bildungspläne-Portal, Kontingentstundentafeln Grundschule/
  Gemeinschaftsschule), allgemeine Recherche zu Lehrerdeputaten
  (Pflichtstunden-Übersicht). Die exakten Kontingentstundentafel-Zahlen
  wurden für die Testfixture (2.15f) plausibel, aber synthetisch gewählt -
  gleiche Disziplin wie bei `GymnasiumSekIFixture.vb`/
  `GrundschuleGrossFixture.vb` (Phase 2.10): kein Anspruch auf exakte
  amtliche Aktualität.
- **"Stufe 1 Baden-Württemberg":** Das Stammdaten-Schema trägt deshalb von
  Anfang an ein `Bundesland`-Feld (Default `"BW"`) - keine
  BW-spezifische Hartkodierung, die künftige Bundesländer/Schularten
  einen Schema-Bruch kosten würde.

## Architektur

Die Lehrereinsatzplanung ist ein eigenständiges, vorgeschaltetes
CP-SAT-Teilmodell (gleiches Muster wie `Kursblockung.vb`) - keine einzige
Zeile in `Solver.vb`/`BuildCoreModel`/`ApplyConstraints` wurde verändert.
Ihr Ergebnis wird deterministisch in `teacher_subject_assignment`/
`weekly_hours`[/`consecutive_required`]-Constraint-Objekte übersetzt - das
bestehende, unveränderte JSON-Format aus
`docs/json-constraints-reference.md`.

```
Stammdaten (JSON, persistiert: Schulart/Klassenstufen/Klassen/Raeume/
            Lehrkraefte/Faecher je Klassenstufe/FachLehrerZuordnung)
        │
        ▼
Lehrereinsatzplanung.SolveLehrereinsatz   (NEU: wer unterrichtet was -
        │                                  eigenstaendiges CP-SAT-Modell,
        │                                  kein Tag/Periode-Bezug)
        ▼
BuildAssignmentConstraints                 (NEU: reine Uebersetzung,
        │                                   kein CP-SAT)
        ▼
Solver.Solve / SolveTop                    (UNVERAENDERT: Tag/Periode/Raum)
```

### `Stammdaten.vb`

Typisiertes Domänenmodell (bewusst NICHT das rohe `JsonObject`-Muster der
Constraints - Stammdaten sind auf Dauer angelegte Verwaltungsdaten, kein
Wegwerf-Szenario, siehe `docs/arc42-architecture.md` Abschnitt 8.7):
`Klassenstufe`, `Fach` (mit `FachKlassenstufe` je Klassenstufe -
Wochenstunden/`MaxProTag`), `Klasse`, `Raum`, `Lehrer` (Deputat,
Anrechnungsstunden, Verfügbare Tage, bevorzugte Klassenstufen,
`KlassenlehrerFaehig`), `FachLehrerZuordnung` (die Lehrbefähigung/
Einsatzfähigkeit). `SerializeStammdaten`/`DeserializeStammdaten`
(`System.Text.Json`, `JsonNamingPolicy.SnakeCaseLower` - dieselbe
Namenskonvention wie das bestehende `entities`/`constraints`-JSON) plus
`SaveStammdaten`/`LoadStammdaten` als Datei-Wrapper.
`BuildEntitiesFragment` projiziert einen Stammdatenbestand direkt in das
`classes`/`teachers`/`subjects`/`rooms`/`timeslots`-Fragment, das die
bestehende Solve()-Pipeline erwartet.

### `StammdatenValidation.vb`

Dieselbe "Fail-Fast VOR jedem Solve"-Philosophie wie
`Validation.ValidateEntities`: unbekannte Klassenstufen-Referenzen (Fach/
Klasse), unbekannte Lehrer-/Fach-Referenzen in `FachLehrerZuordnungen`,
unplausible Deputate (≤0, oder Anrechnungsstunden ≥ Deputat), sowie zwei
strukturelle Lücken, die der Solver sonst erst als schwer diagnostizierbares
Infeasible entdecken würde: eine tatsächlich genutzte Klassenstufe ganz
ohne Fach, und ein in einer genutzten Klassenstufe geführtes Fach ganz
ohne qualifizierte Lehrkraft.

### `Lehrereinsatzplanung.vb`

**Entscheidungsvariablen:** `assign(Lehrer, Klasse, Fach) As BoolVar`, nur
für kompatible Tripel erzeugt (Lehrer laut `FachLehrerZuordnung`
qualifiziert, Klasse gehört zu einer Klassenstufe, die das Fach führt).

**Hart:** jede (Klasse,Fach)-Pflichtkombination bekommt genau eine
Lehrkraft (`Sum = 1`).

**Weich (gewichtete Zielfunktion, absteigende Priorität):**
- **Deputat-Korridor** (Gewicht 100): pro Lehrkraft ein Hinge-Loss-Paar
  (`ueberschussPos`/`ueberschussNeg`), das nur die Abweichung JENSEITS der
  konfigurierbaren Toleranz (Default 2h) bestraft - innerhalb der Toleranz
  bleibt eine Über-/Unterdeckung kostenlos.
- **Klassenlehrer-Fehlen** (Gewicht 20): pro Klasse eine `hatKlassenlehrer`-
  BoolVar (`AddMaxEquality` über alle klassenlehrerfähigen, dieser Klasse
  tatsächlich zugewiesenen Kandidaten), Verletzung wird bestraft, wenn
  keiner gefunden wird - kein Infeasible.
- **Klassenstufen-Präferenz** (Gewicht 1): eine Zuweisung außerhalb der
  von der Lehrkraft angegebenen bevorzugten Klassenstufe(n) zählt als
  Verletzung (leere Präferenzliste = nie eine Verletzung).

Live gegen die installierte `Google.OrTools` 9.15.6755 verifiziert (Phase
2.15c, `LehrereinsatzplanungTests.vb`): vier Hand-Smoke-Tests mit von Hand
nachgerechneten Erwartungswerten (u.a. eine 1:1-Aufteilung zweier
gleichwertiger Lehrkräfte auf zwei Klassen mit exakt bewiesenem
`Objective=0`) bestätigten sowohl die Modellierung selbst als auch die
zugrunde liegenden CP-SAT-API-Annahmen (`LinearExpr`-Arithmetik mit
Int64-Gewichten, `AddMaxEquality` über eine `List(Of BoolVar)`) im ersten
Anlauf.

### `BuildAssignmentConstraints`

Reine Ableitungsfunktion (kein CP-SAT): übersetzt jede gefundene
Zuweisung in ein `teacher_subject_assignment`- plus ein
`weekly_hours`-Objekt (Wochenstunden/`max_per_day` aus der zugehörigen
`FachKlassenstufe`), sowie bei gesetztem `Fach.BlockLength` zusätzlich ein
`consecutive_required`-Objekt. Das Ergebnis ist, zusammen mit
`Stammdaten.BuildEntitiesFragment`, ein vollständiges, unverändert an
`Solver.Solve`/`SolveTop` übergebbares `entities`/`constraints`-JSON.

### `Verifier.VerifyLehrereinsatz`

Unabhängige Re-Prüfung direkt aus den rohen Stammdaten (kein Aufruf in
`Lehrereinsatzplanung.vb`s CP-SAT-Code hinein, gleiches Prinzip wie
`VerifyKursblockung`): jede (Klasse,Pflichtfach)-Kombination hat genau
eine Zuweisung, jede zugewiesene Lehrkraft ist laut
`FachLehrerZuordnungen` qualifiziert, jede gemeldete
Klassenlehrer-Zuweisung ist sowohl klassenlehrerfähig als auch
tatsächlich eine der Zuweisungen dieser Klasse.

## Realmaßstab-Beleg

Live gemessen (`StammdatenBWFixtureTests.vb`, `TempLehrereinsatzDiagnostic`-
Spike, erstellt/ausgeführt/wieder gelöscht - nie committet) auf den beiden
neuen `StammdatenBWFixture.vb`-Referenzdatensätzen:

| Schule | Klassen | Lehrkräfte | Fächer | Lehrereinsatz | Klassenlehrer | Solver.Solve |
|---|---|---|---|---|---|---|
| Grundschule (Kl. 1-4, 2-zügig) | 8 | 9 | 8 | Optimal, 0,12s, Objective=0 | 8/8 | Optimal, 0,06s, 0 Verstöße |
| Gemeinschaftsschule (Kl. 5-10, 2-zügig) | 12 | 12 | 14 | Optimal, 0,04s, Objective=2600 | 12/12 | Optimal, 0,15s, 0 Verstöße |

Die Grundschule erreicht `Objective=0` (die vier Kernfach-Lehrkräfte
decken die Deutsch/Mathematik/Sachunterricht-Gesamtnachfrage nahezu
passgenau ab). Die Gemeinschaftsschule zeigt ehrlich ein deutlich
größeres `Objective=2600` (überwiegend Deputat-Abweichung, keine
fehlenden Klassenlehrer): bei mehreren kleineren Fachpools (z.B. NaWi,
Sport, Musik/Kunst mit je nur einer Lehrkraft) übersteigt die tatsächliche
Fachnachfrage das nominale Deputat der einzigen qualifizierten
Lehrkraft - ein realistischer Effekt kleiner, wenig differenzierter
Kollegien, keine Fehlmodellierung. Beide Szenarien lösen dennoch
end-to-end sauber durch (0 `Validation`-/`Verifier`-Verstöße auf jeder
Stufe) - der eigentliche Beweis der Phase.

## Zurückgestellte Erweiterungen (nicht Teil dieses MVP)

Direkte Antwort auf den Nutzerwunsch "bestimme weitere mögliche
Constraints" - dokumentiert, aber gemäß der Nutzerentscheidung "schlanker
Kern zuerst" bewusst nicht implementiert:

- **Fächer-Bündelung pro Klassenlehrer**: aktuell erzwingt nichts, dass
  eine Klasse ihre drei Kernfächer (Deutsch/Mathematik/Sachunterricht)
  von EINER einzigen Lehrkraft bekommt - der Deputat-Korridor optimiert
  Fächer/Klassen-Kombinationen frei, ohne Rücksicht auf "gehört
  eigentlich zusammen". Live in Phase 2.16 beobachtet (AFS-Fellbach-
  Benchmark): eine klassenlehrerfähige Lehrkraft unterrichtet häufig nur
  1 von 3 Kernfächern einer Klasse, während die anderen zwei von anderen
  Lehrkräften übernommen werden - das "Klassenlehrer"-Ergebnis bleibt
  dadurch nur eine Näherung (wer die meisten eigenen Fächer in dieser
  Klasse hat), kein echtes Klassenlehrerprinzip. Eine harte oder weiche
  Bündelungs-Regel wäre die naheliegende Erweiterung.
- **Kontinuität über Jahre als aktive Solver-Präferenz** (nicht wie in
  Phase 2.14 nur aus einem bereits gelösten Vorjahr abgeleitet): ein
  Lehrer, der Kl. 1a unterrichtet hat, wird in der Zielfunktion bevorzugt
  wieder Kl. 2a zugewiesen - an der Grundschule besonders relevant
  (Klassenlehrerprinzip über alle 4 Jahre häufig gewünscht).
- **Fachfremder Einsatz minimieren**: falls `FachLehrerZuordnung` um eine
  Qualifikationsstufe ("Hauptfach" vs. "fachfremd möglich") erweitert
  wird, ein weiches Ziel, fachfremden Einsatz zu vermeiden statt ihn nur
  überhaupt zuzulassen.
- **Maximale Anzahl Klassen/Fächer pro Lehrer**: Zersplitterung vermeiden
  (Beziehungsqualität/Präsenz).
- **Teilzeit-Tage-Kohärenz-Vorprüfung**: ein Lehrer mit nur 2
  Präsenztagen bekommt keine Fachzuordnung, deren Wochenstunden
  strukturell nicht in 2 Tagen unterbringbar sind - Brücke zum
  bestehenden `teacher_availability`-Constraint der zweiten
  Solver-Stufe.
- **Klassenlehrer-Tandem-Balance**: bei zwei Klassenlehrern pro Klasse
  (siehe die GSG-Fellbach-Recherche aus Phase 2.14: häufig ein gemischtes
  Tandem) eine weiche Ausgewogenheits-Präferenz.
- **Springerreserve/Vertretungspool**: Lehrkräfte bewusst ohne volle
  Deputatsausschöpfung für kurzfristige Vertretung freihalten.
- **Gleichmäßige Verteilung unbeliebter Fächer/Randbedingungen** im
  Kollegium (Fairness-Ziel, niedrigste Priorität).

## Verifikation

- `StammdatenTests.vb`: Serialisierungs-Rundtrip (String und Datei),
  `BuildEntitiesFragment`-Projektion, Helper-Funktionen
  (`FaecherOfKlassenstufe`/`WochenstundenFuer`/`LehrerFuerFach`).
- `StammdatenValidationTests.vb`: 12 Tests, ein Test pro Fehlerklasse plus
  ein Test, der bestätigt, dass eine ungenutzte Klassenstufe ohne Fach
  KEIN Fehler ist.
- `LehrereinsatzplanungTests.vb`: 5 Tests, davon 4 Hand-Smoke-Tests mit
  live gegen OrTools nachgerechneten Erwartungswerten (siehe oben) plus
  ein Test, der die Übersetzung in valides `entities`/`constraints`-JSON
  bestätigt.
- `VerifyLehrereinsatzTests.vb`: 7 Tests, je einer pro erkennbarer
  Fehlerklasse (fehlende/doppelte Zuweisung, unqualifizierte Zuweisung,
  nicht klassenlehrerfähiger/nicht tatsächlich unterrichtender
  gemeldeter Klassenlehrer), plus ein sauberer Referenzfall.
- `StammdatenBWFixtureTests.vb`: 2 End-to-End-Tests (Stammdaten →
  Lehrereinsatzplanung → `BuildAssignmentConstraints` →
  UNVERÄNDERTER `Solver.Solve`), je 0 Verstöße auf jeder Stufe.
- Vollständige Regressionssuite grün, 0 Regressionen gegenüber dem
  Phase-2.14-Stand.

## Definition of Done — Status

- [x] `dotnet test TimetableCore.Tests` bleibt vollständig grün, 0
      Regressionen.
- [x] Stammdaten lassen sich speichern und wieder laden (Rundtrip-Test),
      Validierung blockiert nachweislich jede dokumentierte Fehlerklasse.
- [x] `SolveLehrereinsatz` liefert für beide BW-Testfixturen
      Optimal/Feasible, `VerifyLehrereinsatz` meldet 0 Verstöße.
- [x] Das übersetzte Ergebnis löst end-to-end sauber durch den
      unveränderten `Solver.Solve` (0 `Verifier.VerifySchedule`-Verstöße).
- [x] `Solver.vb`/`Validation.vb`/`Verifier.vb`/`ApplyConstraints`/
      `BuildCoreModel` bleiben byte-identisch unverändert - die neue
      Fähigkeit lebt vollständig in neuen Dateien.
- [x] Recherche-Grundlage (BW-Kultusministerium) mit Quellenangaben
      dokumentiert.
- [x] Zurückgestellte Constraint-Ideen dokumentiert (siehe oben).
- [x] `docs/arc42-architecture.md` um die neue Pipeline-Stufe ergänzt.
- [x] Committet und gepusht auf `claude/qwen-3.5-sandbox-test-ubfhmo`.
