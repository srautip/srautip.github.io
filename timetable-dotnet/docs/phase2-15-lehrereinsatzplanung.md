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

*(Diese Tabelle ist der historische Stand vor der Fächer-Bündelung, siehe
"Nachtrag 2" unten für die aktualisierten Objective-Werte nach deren
Einführung.)*

## Zurückgestellte Erweiterungen (nicht Teil dieses MVP)

Direkte Antwort auf den Nutzerwunsch "bestimme weitere mögliche
Constraints" - dokumentiert, aber gemäß der Nutzerentscheidung "schlanker
Kern zuerst" bewusst nicht implementiert:

- ~~**Fächer-Bündelung pro Klassenlehrer**~~ - **umgesetzt**, siehe
  "Nachtrag 2 (Phase 2.16-Folgeauftrag)" unten.
- ~~**Kontinuität über Jahre als aktive Solver-Präferenz**~~ (nicht wie in
  Phase 2.14 nur aus einem bereits gelösten Vorjahr abgeleitet) -
  **umgesetzt**, siehe "Nachtrag 5" unten.
- ~~**Fachfremder Einsatz minimieren**~~ - **umgesetzt**, siehe
  "Nachtrag 5" unten.
- ~~**Maximale Anzahl Klassen/Fächer pro Lehrer**~~ - **umgesetzt**, siehe
  "Nachtrag 5" unten.
- ~~**Teilzeit-Tage-Kohärenz-Vorprüfung**~~ - **umgesetzt**, siehe
  "Nachtrag 5" unten.
- ~~**Klassenlehrer-Tandem-Balance**~~ - **umgesetzt (vereinfachte
  Variante)**, siehe "Nachtrag 5" unten.
- ~~**Springerreserve/Vertretungspool**~~ - **umgesetzt**, siehe
  "Nachtrag 5" unten.
- ~~**Gleichmäßige Verteilung unbeliebter Fächer/Randbedingungen**~~ -
  **umgesetzt**, siehe "Nachtrag 5" unten.

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

## Nachtrag (Phase 2.16): kritischer Bug in `BuildAssignmentConstraints` gefunden und behoben

Beim Bau des AFS-Fellbach-Benchmarks (Phase 2.16) wurde der gedruckte
Stundenplan der Klasse 4b erstmals visuell geprüft (nicht nur per
`Verifier.VerifySchedule`-Zähler) - und zeigte einen echten Fehler:
**`BuildAssignmentConstraints` emittierte nie `no_overlap`-Regeln**, weder
für Klassen noch für Lehrkräfte. Da `weekly_hours` nur die
Gesamtstundenzahl zählt, nie die Verteilung auf verschiedene Slots
erzwingt, hatte der Solver dadurch keinerlei Grund, eine Klasse über die
Woche zu verteilen - im konkreten Fall häufte er alle 24 Wochenstunden von
Klasse 4b auf nur zwei Slots (z.B. montags 8 verschiedene Fächer
gleichzeitig in Periode 6). `Verifier.VerifySchedule` meldete dabei
`0 Verstöße`, weil es nur Verstöße gegen tatsächlich VORHANDENE
Constraint-Typen prüft (siehe `Verifier.vb`s Kopfkommentar) - eine fehlende
Regel selbst kann es strukturell nicht erkennen, nur eine vorhandene, aber
verletzte.

**Fix:** `BuildAssignmentConstraints` emittiert jetzt zusätzlich
`no_overlap(resource:="class", ...)` für jede betroffene Klasse und
`no_overlap(resource:="teacher", ...)` für jede betroffene Lehrkraft -
exakt dasselbe Muster, das jede andere synthetische Szenario-Konstruktion
in diesem Projekt (`Schienenraster.vb`/`Raumzuordnung.vb`) bereits nutzt.
Live erneut geprüft: derselbe AFS-Fellbach-Benchmark verteilt Klasse 4bs
24 Wochenstunden danach korrekt über 24 verschiedene (Tag,Periode)-Slots,
0 Verstöße. Neuer Regressionstest
`BuildAssignmentConstraintsResultingScheduleHasNoOverlaps`
(`LehrereinsatzplanungTests.vb`) prüft das jetzt zusätzlich direkt
(Gruppierung nach (Klasse/Lehrkraft, Tag, Periode), harte Assertion auf
Gruppengröße 1) statt sich nur auf `Verifier.VerifySchedule` zu verlassen.

**Betroffen war der gesamte bisherige Phase-2.15-Realmaßstab-Beleg**
(Grundschule/Gemeinschaftsschule-Endergebnisse oben) - beide Szenarien
lösen nach dem Fix weiterhin sauber (erneut live bestätigt), die
`Lehrereinsatzplanung`-Objective-Werte (0 bzw. 2600) ändern sich NICHT
(der Fehler betraf ausschließlich die zweite, nachgelagerte
`Solver.Solve`-Stufe, nicht die Lehrer-Klasse-Fach-Zuordnung selbst) -
die oben dokumentierten Tabellenwerte bleiben also gültig, nur die
tatsächliche Slot-Verteilung im finalen Stundenplan war vorher fehlerhaft
unterspezifiziert.

## Nachtrag 2 (Phase 2.16-Folgeauftrag): Fächer-Bündelung pro Klassenlehrer implementiert

Direkte Umsetzung der oben zurückgestellten "Fächer-Bündelung"-Erweiterung,
auf expliziten Nutzerwunsch ("Implementiere die dokumentierte Erweiterung
zur Bündelung auf einen richtigen Klassenlehrer").

**Mechanik:** ein neues weiches Ziel `WeightBuendelungVerletzt` (gleiche
Gewichtsstufe wie `WeightKlassenlehrerFehlt`, 20) bestraft pro Klasse, wenn
MEHR als eine klassenlehrerfähige Lehrkraft gleichzeitig in ihr aktiv ist
(wiederverwendet dieselben `unterrichtet[l,k]`-BoolVars, die bereits für
das bestehende Klassenlehrer-Ziel existierten - reifiziert als
`Sum(kandidaten) <= 1` außer bei bestrafter Verletzung). Bewusst weiterhin
ein **weiches**, kein hartes Ziel: bei Stammdaten, in denen die
klassenlehrerfähigen Kandidaten nicht alle für dieselben Fächer
qualifiziert sind, könnte ein hartes "höchstens 1 aktiv" das Szenario
unnötig `Infeasible` werden lassen (Vollständigkeit könnte pro Fach
unterschiedliche Kandidaten erzwingen). Zwei neue Hand-Smoke-Tests
(`LehrereinsatzplanungTests.vb`) belegen live gegen die installierte
OrTools-DLL beide Seiten:

- `CoreSubjectsAreBundledOntoASingleTeacherPerClass`: zwei Klassen, zwei
  für alle drei Kernfächer qualifizierte Lehrkräfte mit knapper
  Deputat-Kapazität (je genau 1 Klasse) - bestätigt `Objective=0` UND dass
  jede Klasse alle drei Fächer bei EINER einzigen Lehrkraft gebündelt
  bekommt.
- `BundlingViolationIsPenalizedNotInfeasibleWhenQualificationsAreDisjoint`:
  eine Klasse, deren zwei Fächer nur von zwei UNTERSCHIEDLICHEN,
  disjunkt qualifizierten klassenlehrerfähigen Lehrkräften abgedeckt
  werden können - bestätigt `Optimal` (nicht `Infeasible`) mit exakt
  `Objective=WeightBuendelungVerletzt`.

**Realmaßstab-Beleg (live erneut gemessen, Referenzfixturen unverändert):**

| Schule | Lehrereinsatz | Klassenlehrer | Beobachtung |
|---|---|---|---|
| BW-Grundschule (2-zügig, Phase 2.15) | Optimal, 0,22s, `Objective=0` | 8/8 | Klassenlehrerprinzip-Pool bleibt bündelbar - Objective unverändert 0. |
| AFS-Fellbach-Grundschule (3-zügig, Phase 2.16) | Optimal, 0,49s, `Objective=0` | 12/12 | Jede Klasse bekommt jetzt nachweislich EINE Lehrkraft für Deutsch+Mathematik+Sachunterricht (vorher bis zu 3 verschiedene) - live im Konsolen-Report von `AFSFellbachGrundschuleBenchmarkTests.vb` bestätigt. |
| BW-Gemeinschaftsschule (2-zügig, Phase 2.15) | Optimal, 0,05s, `Objective=2840` (vorher 2600) | 12/12 | Anstieg um genau 240 = 12 Klassen × Gewicht 20 - **erwartet, nicht fehlerhaft**: die Gemeinschaftsschul-Lehrkräftepools (Deutsch-Geschichte, Mathematik-Physik, Englisch-Erdkunde) sind alle klassenlehrerfähig, aber jeweils für DISJUNKTE Fächer qualifiziert (echtes Fachlehrerprinzip, siehe Phase-2.14-Recherche) - echte Bündelung ist dort strukturell gar nicht möglich, das Modell zeigt diesen realen Unterschied zwischen Grundschule und Gemeinschaftsschule jetzt korrekt in den Zahlen, statt ihn zu verschleiern. |

Alle drei Szenarien bleiben `Optimal`/0 `VerifyLehrereinsatz`-Verstöße/0
`Verifier.VerifySchedule`-Verstöße - die Bündelung verändert nur, WELCHE
Lehrkraft welche Fächer bekommt, nie die strukturelle Korrektheit.

`docs/arc42-architecture.md` wurde nicht zusätzlich geändert (die
Bausteinsicht beschreibt `Lehrereinsatzplanung.vb`s Zielfunktion bereits
allgemein als "Deputat-Korridor/Klassenlehrer/Präferenzen weich" - die
neue Komponente fügt sich dort ohne Formulierungsbruch ein).

## Nachtrag 3: "ein Klassenlehrer hat üblicherweise nur eine Klasse"

Live-Rückmeldung nach Nachtrag 2: der AFS-Fellbach-Benchmark zeigte zwar
gebündelte Kernfächer pro Klasse, aber weil 6 Klassenlehrer mit je 28h
Deputat für 12 Klassen genügend Kapazität für je ZWEI Klassen hatten,
tauchte dieselbe Lehrkraft als Klassenlehrer von zwei verschiedenen
Klassen auf - unrealistisch, da eine Lehrkraft üblicherweise nur für
GENAU EINE Klasse Klassenlehrer ist (kann aber durchaus in mehreren
Klassen Fachunterricht geben).

**Mechanik:** eine zweite, symmetrische weiche Zielfunktions-Komponente in
`Lehrereinsatzplanung.vb` (derselbe `unterrichtet[l,k]`-BoolVar-Bestand,
diesmal nach Lehrkraft statt nach Klasse gruppiert): bestraft, wenn
dieselbe klassenlehrerfähige Lehrkraft in MEHR als einer Klasse als
bündelnder Kandidat aktiv ist. Fließt in denselben `WeightBuendelungVerletzt`-
Topf wie die Klassen-Richtung aus Nachtrag 2 (beide beantworten dieselbe
Frage: "hat diese Klasse GENAU EINE klare, nur für sie zuständige
Klassenlehrkraft?"). Weiterhin bewusst weich, nicht hart - siehe
Code-Kommentar in `Lehrereinsatzplanung.vb`.

**Fixture-Konsequenz:** die reine Modelländerung allein reichte nicht -
mit zu wenigen, zu voll ausgelasteten Klassenlehrer-Kandidaten hätte das
neue Ziel nur zusätzliche `fehltKlassenlehrer`-Strafen für die
überzähligen Klassen produziert. `AFSFellbachStammdatenFixture.vb` und
`StammdatenBWFixture.BuildBWGrundschule` wurden deshalb auf **eine
Klassenlehrkraft pro Klasse** umgestellt (12 bzw. 8 statt vorher 6 bzw.
4), mit auf ca. 14h reduziertem (hälftigem Teilzeit-)Deputat pro
Klassenlehrkraft, passend zum eigenen Kernfach-Bedarf einer einzelnen
Klasse (13-14h) - realistisch für eine Grundschule, an der
Teilzeitbeschäftigung sehr verbreitet ist.

**Realmaßstab-Beleg (live erneut gemessen):**

| Schule | Lehrereinsatz | Klassenlehrer | Beobachtung |
|---|---|---|---|
| BW-Grundschule (2-zügig, 8 Klassenlehrer @14h) | Optimal, 0,19s, `Objective=0` | 8/8 | Jede Klassenlehrkraft hat nachweislich genau 1 Klasse. |
| AFS-Fellbach-Grundschule (3-zügig, 12 Klassenlehrer @14h) | Optimal, 0,12s, `Objective=0` | 12/12 | Jede Klassenlehrkraft hat nachweislich genau 1 Klasse - live im Konsolen-Report von `AFSFellbachGrundschuleBenchmarkTests.vb` bestätigt (Klasse 4b durchgehend von derselben Lehrkraft in Deutsch/Mathematik/Sachunterricht unterrichtet). |
| BW-Gemeinschaftsschule (2-zügig, unverändert) | Optimal, 0,13s, `Objective=3000` | 12/12 | Weiterhin mehrere Lehrkräfte mit >1 Klasse als "Klassenlehrer" (z.B. `Deutsch-Geschichte-Lehrer-1` bei 4 Klassen) - **bewusst nicht angepasst**: die dortigen Lehrkräftepools sind strukturell disjunkt qualifiziert (echtes Fachlehrerprinzip der Sekundarstufe, siehe Phase-2.14-Recherche), sodass weder echte Fächer-Bündelung noch "1 Klasse pro Klassenlehrer" dort ohne eine grundlegend andere Fixture-Modellierung erreichbar wären. Bewusst als offene, dokumentierte Grenze stehen gelassen statt einer nicht angeforderten Neugestaltung des Gemeinschaftsschule-Fixtures. |

Ein neuer, isolierter Hand-Smoke-Test
(`SameTeacherIsNotBundledAsKlassenlehrerOfTwoClassesWhenDeputatDoesNotForceIt`,
`LehrereinsatzplanungTests.vb`) beweist die Lehrkraft-Richtung gezielt und
losgelöst vom Deputat-Effekt: Deputat-Toleranz bewusst riesig gesetzt
(1000h), damit jede denkbare Aufteilung deputat-neutral bleibt - einzig
die neue Regel selbst kann den Solver dann noch dazu zwingen, zwei
verschiedene Klassen zwei verschiedenen Lehrkräften zuzuweisen, statt
einer einzigen (von Hand nachgerechnet: `Objective=0` nur bei Aufteilung
erreichbar, `WeightBuendelungVerletzt` sonst).

## Nachtrag 4: Vollzeit-Klassenlehrer statt hälftiger Teilzeit

Direkte Folgeanweisung nach Nachtrag 3: die dortige Fixture-Anpassung
("eine Klassenlehrkraft pro Klasse") wich auf hälftiges Teilzeit-Deputat
(14h) aus, um den Deputat-Korridor eng am Kernfach-Bedarf einer Klasse zu
halten - Nutzerwunsch jetzt: mehr Realismus mit VOLLZEIT-Klassenlehrern
(28h), explizit ohne neuen Testaufwand (weder `StammdatenBWFixtureTests.vb`
noch `AFSFellbachGrundschuleBenchmarkTests.vb` prüfen einen Objective-Wert,
nur Optimal/Feasible + 0 Verstöße auf jeder Pipeline-Stufe - beide bleiben
dadurch unverändert grün).

**Fachaufteilung erweitert (reales Klassenlehrerprinzip):** Klassenlehrer
übernehmen neben Deutsch/Mathematik/Sachunterricht zusätzlich Musik und
Kunst der eigenen Klasse, bei der BW-Grundschule (ohne Sport-/
Bewegungsprofil) zusätzlich auch Sport. Religion (konfessionsgebunden,
klassenübergreifend organisiert) und Englisch (eigene Qualifikation)
bleiben bei dedizierten Fachlehrkräften. Bei AFS Fellbach bleibt zusätzlich
Sport bei den Sportlehrkräften (bildet das reale Sport-/Bewegungsprofil der
Schule ab, bereits in Phase 2.10 recherchiert - Kapazität 2×18h=36h deckt
sich weiterhin exakt mit der Sport-Gesamtnachfrage). Neue, optionale
Klassenleitungs-Anrechnungsstunde (2h, reale BW-Praxis) auf den
`AddLehrerPool`-Helper beider Fixtures ergänzt.

**Ehrliche, strukturelle Deputat-Lücke:** selbst bei voller Übernahme aller
eigenen Fächer erreicht eine Klasse strukturell weniger als das volle
28h-Deputat, da eine Grundschulklasse schlicht nicht mehr Wochenstunden
hat (BW-Grundschule max. ~21h, AFS Fellbach max. ~18h, da dort Sport bei
den Sportlehrkräften bleibt). Diese Lücke bleibt bewusst als
Rest-Abweichung im Objective sichtbar (Nutzerentscheidung, analog zur
bereits dokumentierten BW-Gemeinschaftsschule-Grenze), statt sie über ein
erweitertes Toleranzband wegzuoptimieren.

**Realmaßstab-Beleg (live gemessen):**

| Schule | Lehrereinsatz | Klassenlehrer | Ist-Deputat je Klassenlehrer |
|---|---|---|---|
| BW-Grundschule (2-zügig, 8 Klassenlehrer @28h, Fächer inkl. Sport) | Optimal, `Objective=2800` | 8/8, je genau 1 Klasse | 20-21h von 28h Soll (26h Sollnetto nach 2h Anrechnung) |
| AFS-Fellbach-Grundschule (3-zügig, 12 Klassenlehrer @28h, ohne Sport) | Optimal, `Objective=7800` | 12/12, je genau 1 Klasse | 17-18h von 28h Soll (26h Sollnetto nach 2h Anrechnung) |

Beide Läufe bleiben `Optimal` mit 0 `VerifyLehrereinsatz`-Verstößen und 0
`VerifySchedule`-Verstößen im übersetzten Stundenplan - die Bündelung
selbst (genau 1 Klassenlehrer pro Klasse, siehe Nachtrag 2/3) ist
unverändert intakt, nur die Deputat-Komponente des Objectives ist jetzt
größer, was erwartungsgemäß und ehrlich dokumentiert ist.

## Nachtrag 5: die 7 zurückgestellten Constraints aus Abschnitt "Zurückgestellte Erweiterungen"

Direkte Nutzeranweisung, die oben dokumentierte Liste jetzt umzusetzen.
Alle 7 erweitern ausschließlich `Stammdaten.vb`/`StammdatenValidation.vb`/
`Lehrereinsatzplanung.vb`/`Verifier.vb` - `Solver.vb`/`Validation.vb`/
`Verifier.VerifySchedule` bleiben unangetastet (gleiches Grundprinzip wie
die gesamte Phase 2.15/2.16: neue Fähigkeit lebt in der vorgeschalteten
Planungsstufe). Alle neuen Stammdaten-Felder sind additiv mit
rückwärtskompatiblem Default (`Nothing`/`False`/`0`) - die bestehenden
`StammdatenBWFixture.vb`/`AFSFellbachStammdatenFixture.vb`-Fixtures und
ihre Tests bleiben byte-identisch unverändert grün, kein
Regressionsrisiko für die dort dokumentierten Objective-Werte.

**Nutzerentscheidungen aus der Rückfragerunde:**
1. **Kontinuität über Jahre:** gilt für ALLE Fächer der Klasse (nicht nur
   die Klassenlehrer-Rolle).
2. **Teilzeit-Tage-Kohärenz:** HART ausgeschlossen (Kandidat wird gar
   nicht als Variable erzeugt).
3. **Klassenlehrer-Tandem-Balance:** vereinfachte Variante (Bündelungs-
   grenze von <=1 auf <=2 anheben plus ein weiches Ausgewogenheitsziel).
4. **Max. Klassen/Fächer pro Lehrer:** weich, per Lehrer konfigurierbar.

**Erweiterte Gewichtstabelle** (bestehende Reihenfolge unverändert,
neue Zwischenstufen ergänzt):

| Gewicht | Wert | Bedeutung |
|---|---|---|
| `WeightDeputatAbweichung` | 100 | unverändert |
| `WeightKlassenlehrerFehlt` | 20 | unverändert |
| `WeightBuendelungVerletzt` | 20 | unverändert |
| `WeightKontinuitaetVerletzt` | 20 | NEU - gleiche Stufe: "bleibt die Zuordnung stabil?" |
| `WeightFachfremdEinsatz` | 10 | NEU |
| `WeightMaxKlassenVerletzt` | 5 | NEU |
| `WeightMaxFaecherVerletzt` | 5 | NEU |
| `WeightTandemBalance` | 5 | NEU |
| `WeightPraeferenzVerletzt` | 1 | unverändert |
| `WeightUnbeliebteFaecherUngleichheit` | 1 | NEU - niedrigste Priorität |

Springerreserve braucht kein eigenes Gewicht - realisiert als Erweiterung
der bestehenden Deputat-Korridor-Berechnung (`sollNetto -=
SpringerReserveStunden`, analog zu `Anrechnungsstunden`).

**Mechanik je Ziel** (alle in `Lehrereinsatzplanung.SolveLehrereinsatz`,
sofern nicht anders angegeben):

1. **Kontinuität (alle Fächer):** neuer optionaler Parameter
   `vorjahresZuordnung As Dictionary(Of (Klasse, Fach), Lehrername)`.
   Statt der ursprünglich geplanten "Sum(1-assign)"-Formulierung wurde
   während der Umsetzung eine mathematisch äquivalente, aber sicherere
   Variante gewählt: die tatsächliche Wiederverwendung (`assign(key)`
   selbst) fließt direkt als NEGATIV gewichteter Bonus-Term in die
   Zielfunktion ein (`Sum(kontinuitaetErhalten) * -WeightKontinuitaetVerletzt`)
   - unterscheidet sich vom ursprünglichen Ansatz nur um eine von den
   Entscheidungsvariablen unabhängige additive Konstante (dieselbe Lösung
   minimiert beides), vermeidet aber eine `Integer minus LinearExpr`-
   Subtraktion, deren Operator-Richtung in diesem Projekt bislang nirgends
   verifiziert war (nur `LinearExpr minus Integer` wird bereits im
   Deputat-Korridor genutzt).
2. **Fachfremder Einsatz:** `FachLehrerZuordnung.Fachfremd`-Flag, aktive
   Zuweisung eines so markierten Kandidaten fließt direkt (die
   `assign`-Variable selbst, kein neuer BoolVar) in die Zielfunktion ein.
3. **Max. Klassen/Fächer:** neue `unterrichtetKlasseAlle[l,k]`/
   `unterrichtetFach[l,f]`-BoolVars (gleiche Reifikationstechnik wie das
   bestehende `unterrichtet[l,k]` der Bündelungslogik, hier für ALLE statt
   nur klassenlehrerfähige Lehrkräfte), Hinge-Loss-Überschreitung wie beim
   Deputat-Korridor.
4. **Teilzeit-Tage-Kohärenz:** harter Filter direkt bei der
   `assign()`-Variablenerzeugung, neue geteilte Funktion
   `Stammdaten.IstTeilzeitKohaerent` (wiederverwendet von
   `StammdatenValidation` UND `Lehrereinsatzplanung`).
5. **Klassenlehrer-Tandem-Balance:** `Klasse.ErlaubtKlassenlehrerTandem`
   hebt die Bündelungsgrenze auf 2; Ausgewogenheit über den bereits in
   `SolveTopObjective.vb` live verifizierten Sentinel-Min/Max-Trick (nur
   AKTIVE Kandidaten zählen für das Minimum, ein Sentinel-Wert
   `bigStunden` für inaktive verhindert eine Verzerrung). Live entdeckte
   Feinheit während der Umsetzung: bei GENAU EINEM (oder null) aktiven
   Kandidaten kann `tandemMax - tandemMin` rechnerisch negativ werden
   (Sentinel-Min bleibt bei `bigStunden`, Rohwert-Max bei 0) - eine
   erzwungene Gleichheit wäre dort unlösbar gewesen. Behoben durch eine
   Ungleichung (`tandemRange >= tandemMax - tandemMin`, `>= 0`, derselbe
   Hinge-Trick wie beim Deputat-Korridor) statt einer Gleichheit - die
   Zielfunktion drückt `tandemRange` ohnehin auf 0, sobald das zulässig
   ist.
6. **Springerreserve:** `Lehrer.SpringerReserveStunden` erweitert die
   bestehende `sollNetto`-Berechnung, kein neuer Zielfunktions-Term.
7. **Faire Verteilung unbeliebter Fächer:** `Fach.Unbeliebt`-Flag, Bereich
   (Max-Min, kein Sentinel nötig) der Anzahl unbeliebter-Fach-Zuweisungen
   über alle dafür qualifizierten Lehrkräfte.

**`Verifier.VerifyLehrereinsatz`** bekam eine additive
Kanarienvogel-Prüfung für die Teilzeit-Tage-Kohärenz (unabhängig aus den
rohen Stammdaten re-derivert) - die übrigen 6 weichen Ziele werden bewusst
NICHT im Verifier geprüft (Präferenzen, keine harten Invarianten, gleiches
Muster wie bisher).

**Live-Verifikation (7 neue Hand-Smoke-Tests in
`LehrereinsatzplanungTests.vb`, alle grün, Erwartungswerte von Hand
nachgerechnet):** beim Entwerfen mehrerer Tests wurde wiederholt eine
bereits bestehende, unabhängige Nebenwirkung übersehen und musste
nachträglich in den erwarteten Wert eingerechnet werden - ein Lehrer ohne
`KlassenlehrerFaehig` in einer Klasse ganz ohne klassenlehrerfähigen
Kandidaten löst unconditional `WeightKlassenlehrerFehlt` aus (bereits seit
Phase 2.15 bestehendes Verhalten), und ein als alleiniger Kandidat über
mehrere Klassen hinweg zwangsläufig aktiver klassenlehrerfähiger Lehrer
löst zusätzlich die bereits bestehende Pro-Lehrkraft-Bündelungsregel aus
(Nachtrag 3) - beide Effekte sind in den finalen Testkommentaren korrekt
dokumentiert, kein Hinweis auf einen Fehler in der neuen Logik selbst,
sondern auf eine anfänglich unvollständige Testisolation.

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
