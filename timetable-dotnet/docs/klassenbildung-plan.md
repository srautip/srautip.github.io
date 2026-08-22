# Plan: Klassenbildung mit CP-SAT — Integration in die bestehende Loesung

Grundlage: `docs/klassenbildung-konzept.md` (Regeltypen, CP-SAT-Modell,
Varianten, Fixierungen, Ampel-Visualisierung, DSGVO-Anforderungen).
Dieser Plan uebersetzt das Konzept auf die vorhandene Architektur
(TimetableCore + SchoolTestRunner mit YAML-Input/-Output) und schlaegt
eine Umsetzung in sechs Schritten vor. Leitidee: **kein neues Muster
erfinden** — fast alles, was das Konzept verlangt, existiert in diesem
Repository bereits in erprobter Form und wird nur auf das neue Problem
uebertragen.

## 1. Einordnung in die Pipeline

Die Klassenbildung ist eine **Stufe 0** VOR den bestehenden Stufen
(Lehrereinsatzplanung -> Stundenplan): sie erzeugt erst die Klassen-
zusammensetzung, auf der alles Weitere aufbaut. Sie ist aber bewusst
**eigenstaendig lauffaehig** — eine Schule kann sie nutzen, ohne je
einen Stundenplan zu rechnen (und umgekehrt bleiben die bestehenden
Beispiele ohne Klassenbildungs-Input voellig unveraendert):

```
input/klassenbildung.yaml ──> Stufe 0: Klassenbildung (NEU)
                                 │  output/klassenbildung.md/.json/.html
                                 ▼  (optional: schueler[].klasse in Stammdaten befuellen)
input/stammdaten.yaml     ──> Stufe 1: Lehrereinsatzplanung
input/constraints.yaml    ──> Stufe 2: Stundenplan (SolveTop)
```

Die Kopplung an Stufe 1/2 ist bewusst LOSE: `Stammdaten.Schueler`
(Phase 2.19) kennt bereits `Id` + `Klasse` — das Ergebnis der
Klassenbildung ist exakt eine Befuellung dieses Feldes. Mehr
Integration braucht der erste Wurf nicht; die Klassen selbst
(`Klasse`-Objekte) existieren in den Stammdaten weiterhin unabhaengig.

## 2. Wiederverwendete Muster (mit konkreten Verweisen)

| Konzept-Anforderung | Vorhandenes Muster im Repo |
|---|---|
| hart/weich je Regel (`modus`) | `priority: must/should` der Constraint-Typen (`Validation.KannCapableTypes`); hier als `modus: hard/soft` im eigenen Schema, gleiche Semantik |
| Prioritaetsstufen -> Zielfunktion | Variante A = gewichtete Summe wie `SolveTopObjective.WeightedTotal` inkl. **GCD-Normalisierung** (P6) gegen grosse Koeffizienten; Variante B = lexikografische Stufen exakt wie `Solver.SolveTop` (Stufen-Minimize, Optimum als Band fixieren, `AddHint` aus dem Vorlauf — Re-Minimize auf demselben CpModel ist hier live verifiziert) |
| Varianten-Schleife (Abschnitt 8) | `Solver.SolveTop`-Enumeration: Qualitaetsschranke = `laterIterationsGapLimit`/Band-Muster, Diversitaets-Cut = `BlockSolution`-`minDiversity` (identische Formel `Σ x_true ≤ n − d`), Hint-Steuerung = `rehintFoundSolutions` |
| Klassen-Symmetriebrechung (8.2) | frisch gebaut fuer Lehrer-Aequivalenz: `Lehrereinsatzplanung.AddSymmetryBreaking` (Lex-Kette mit equal-so-far-BoolVars). Fuer Klassen einfacher: Klassen ohne Fixierung sind VOLL austauschbar — kanonische Ordnung „kleinster nicht fixierter Schueler-Index je Klasse aufsteigend“ genuegt; Klassen mit Fixierungen (z. B. barrierefreier Raum, Blaeserklasse) sind Singletons, exakt die konservative Logik aus `TeacherEquivalenceClasses` |
| Unabhaengige Nachpruefung / Bewertungslauf (9.2 Phase 3, 11.2) | Verifier-Philosophie („solver proposes, independent re-derivation is ground truth“, `Verifier.vb`-Kopfkommentar): ein reiner Auszaehl-Bewerter `KlassenbildungQuality` analog `ScheduleQuality.Score` — er liefert zugleich Verletzungsreport, Scorecard UND die Ampel-Chips |
| Fail-Fast-Validierung | `Validation.ValidateEntities`-Stil: unbekannte Schueler-IDs, `fixierungen.klasse` ausserhalb `1..anzahl`, Gruppen ohne Mitglieder, Summe `min_groesse·anzahl > |S|` usw. als harte Fehler VOR dem Solve (die „stiller-No-Op“-Falle aus dem Validation-Kopfkommentar gilt hier identisch) |
| YAML-Konventionen | `UnderscoredNamingConvention` + nullable Felder mit „Nothing = Default“ wie `RunConfig`/`QualityWeightsConfig` (`Run.vb`) |
| Viewer | Muster der Stundentafel: self-contained ES5-HTML mit eingebettetem JSON (Embedded Resource + `render`-Subkommando), sortierbare Uebersicht, Scorecard-Spalten, Diff zwischen Varianten (analog „Vergleichen mit“) |
| Messlaeufe/Doku | CLAUDE.md-Konventionen: Beleg-Laeufe mit ehrlicher Messhistorie als config-Kommentar, Outputs mitcommitten |

## 3. Neue Bausteine

### 3.1 TimetableCore: `Klassenbildung.vb` (+ `KlassenbildungQuality.vb`)

Neues Modul neben `Lehrereinsatzplanung.vb`/`Kursblockung.vb` — die
Klassenbildung ist wie diese ein eigenstaendiges Zuordnungsproblem und
beruehrt `Solver.vb` mit keiner Zeile.

**Datenklassen** (YAML-gebunden, analog Stammdaten):

```vb
KlassenbildungInput:  Schueler (Id, Attribute As Dictionary(Of String,String)),
                      KlassenAnzahl/MinGroesse/MaxGroesse,
                      Gruppen (Id, Typ=buendelung|verteilung, Mitglieder,
                               MaxProKlasse?, Modus=hard|soft, Prio=1..3),
                      Balance (Attribut, Wert, Toleranz, Modus, Prio),
                      Wuensche (Typ=zusammen|getrennt, Kinder(2), Modus?, Prio),
                      Fixierungen (Kind, Klasse | NichtKlasse)   ' F1 + F2
KlassenbildungResult: Zuordnung (Id -> Klassenindex), Status, Objective,
                      Verletzungen (je Regel: Regel-Id, Mass, Prio, Modus)
```

**`SolveKlassenbildung(input, ...)`** setzt das Modell aus Konzept
Abschnitt 3 um (x[s,c], ExactlyOne, Groessen-Korridor, vier Regeltyp-
Encodings 1:1 wie beschrieben — die Referenzimplementierung in Python
ist mechanisch nach VB/OrTools uebertragbar, alle verwendeten
Operationen (`AddExactlyOne`, `AddImplication`, `AddAbsEquality`,
`OnlyEnforceIf`) sind im Repo bereits im Einsatz oder trivial).

**`SolveKlassenbildungTop(...)`** ist die Varianten-Schleife nach dem
SolveTop-/SolveLehrereinsatzTop-Muster: erst Optimum, dann
`obj <= round(z* · (1+eps))` (Zielfunktion dafuer als eigene IntVar
fuehren, wie im Konzept 8.1), No-Good + `min_dist`-Cut je Variante,
`AddHint` aus der Vorloesung. Abbruch bei Infeasible oder
`n_varianten` erreicht. **Konsens-Kern** (11.3) faellt gratis ab:
Schueler mit identischer Klasse in allen Varianten werden im Ergebnis
markiert (`konsens: true`).

**`KlassenbildungQuality.Bewerte(input, zuordnung)`** — reine
Auszaehlung ohne Solver (Verifier-Prinzip, teilt keinen Code mit dem
Modell): je Regel Verletzungsmass + je (Kind, Regel) ein Chip-Status
`gruen|gelb|rot` nach der Tabelle in Konzept 11.1 (gelb = Kappe exakt
voll / Balance am Toleranzrand). Dient dem Verletzungsreport, der
Scorecard, dem Ampel-Export — und ist die spaetere Grundlage fuer den
Live-Bewerter einer interaktiven UI.

### 3.2 Priorisierung: Empfehlung fuer DIESES Repo

Das Konzept empfiehlt Variante A (Gewichtsstufen) als Default. Die
Projekterfahrung spricht fuer eine Nuancierung: grosse gespreizte
Gewichte (10 000/100/1) sind exakt das, was der P6-Befund als
Bound-Schwaeche identifiziert hat. Deshalb:

- **Default: Variante A**, aber mit GCD-Normalisierung und moderater
  Spreizung, die die Faustregel des Konzepts einhaelt (Faktor >
  maximale Verletzungszahl der darunterliegenden Stufe — bei den
  realen Groessen reicht 1000/50/1 locker). Bei den kleinen Modellen
  (600 Kernvariablen) ist der Bound-Beweis ohnehin unkritisch.
- **`prio_modus: lexikographisch`** als Exakt-Modus von Anfang an
  mitbauen — die Stufen-Schleife ist mit dem SolveTop-Muster ~30
  Zeilen und im Repo das nachweislich robustere Verfahren.

### 3.3 Unloesbarkeits-Erklaerung (Assumptions)

Neu fuers Repo, aber vom Konzept klar spezifiziert: jede harte Regel
und jede Fixierung erhaelt ein Assumption-Literal
(`Model.AddAssumptions`); bei `Infeasible` liefert
`solver.SufficientAssumptionsForInfeasibility()` die Indizes des
Konfliktkerns, die auf Regel-Ids zurueckgemappt und im Output in
Klartext ausgegeben werden („Regel G_hoeren (hart) + Fixierung
S014->1a unvereinbar — eine Regel auf soft stellen oder Pin loesen“).
Vorab per Mini-Test gegen die installierte OrTools-DLL (9.15)
verifizieren — dieselbe Disziplin wie bei jeder neuen CP-SAT-Technik
in diesem Projekt (arc42 Abschnitt 9). Fallback, falls die API sich
sperrig zeigt: Loeschprobe (Regeln einzeln deaktivieren) als naive,
aber korrekte Konfliktkern-Annaeherung.

## 4. SchoolTestRunner: YAML-Input/-Output

### 4.1 Input: `tests/<schule>/input/klassenbildung.yaml`

Eigene Datei (nicht in `stammdaten.yaml` mischen — die Klassenbildung
laeuft zeitlich VOR der Stundenplanung und arbeitet auf Pseudonym-IDs,
Datenminimierung nach Konzept Abschnitt 2). Schema exakt wie Konzept
Abschnitt 2, snake_case:

```yaml
schueler:
  - id: S001
    attribute: {geschlecht: w, herkunft: kita_sonnenblume}
klassen: {anzahl: 4, min_groesse: 22, max_groesse: 26}
gruppen: [...]
balance: [...]
wuensche: [...]
fixierungen:
  - {kind: S030, klasse: 2}
  - {kind: S031, nicht_klasse: 3}     # F2 gleich mitnehmen (trivial: x=0)
```

Solver-Parameter in der bestehenden `config.yaml` unter einem eigenen
Block (Nothing = Default, wie ueberall):

```yaml
klassenbildung:
  zeitlimit_s: 30.0
  n_varianten: 3
  epsilon: 0.05          # Qualitaetsschranke der Varianten
  min_distanz: 8         # Kinder, die sich je Variante unterscheiden muessen
  prio_modus: gewichte   # gewichte | lexikographisch
  prio_gewichte: {3: 1000, 2: 50, 1: 1}
```

### 4.2 CLI und Ablauf

- **`dotnet run --project SchoolTestRunner -- klassen <schule>`**:
  eigenstaendiges Subkommando (Muster `render`): laedt
  `input/klassenbildung.yaml` + config, validiert fail-fast, loest,
  schreibt Outputs. Bewusst NICHT in `run` integriert — `run` bleibt
  Stundenplan-Pipeline; Schulen ohne `klassenbildung.yaml` sind
  unberuehrt.
- **Optionale Kopplung (spaeterer Schritt):** `klassen <schule>
  --uebernehmen` schreibt die gewaehlte Variante als
  `schueler[].klasse` in eine Kopie der Stammdaten (bzw. ein
  `output/stammdaten-mit-klassen.yaml`) — der Anschluss an Stufe 1/2,
  ohne die Eingabedatei der Schule ungefragt zu veraendern.

### 4.3 Output

- **`output/klassenbildung.md`** — der menschenlesbare Report und
  zugleich die Art.-22-taugliche Dokumentationsgrundlage des CLI:
  je Variante die Klassenlisten (Pseudonym-IDs), Scorecard
  (Zielwert, Verletzungen je Prio-Stufe, Balance-Kennzahlen je
  Klasse), Diff-Indikator zwischen den Varianten („V2: 11 Kinder
  anders als V1“), Konsens-Kern („41 von 96 in allen Varianten
  identisch“), vollstaendiger Verletzungsreport in Klartext; bei
  Infeasible der Konfliktkern.
- **`output/klassenbildung.json`** — maschinenlesbar fuers UI/den
  Viewer: `varianten[]` mit Zuordnung, Scorecard, per-Kind-Chips
  (`kriterium, status, text`), Konsens-Flag; `regeln[]`-Snapshot
  (Regelwerk-Stand des Laufs, Konzept 10.1); `parameter` (eps, d,
  Zeitlimit, Seed).
- **`output/klassenbildung.html`** (Schritt K5) — Viewer nach dem
  Stundentafel-Muster.

## 5. Viewer (Ampel + Varianten), abgespeckt fuers CLI

Der volle interaktive Workflow (Drag & Drop, Pins, Re-Solve-Buttons,
Rollen/Freigabe — Konzept Abschnitte 9/10) braucht einen Server bzw.
eine App und ist hier bewusst NICHT Teil des Plans. Was der
self-contained Viewer aber sofort leisten kann (alles read-only auf
dem eingebetteten JSON, exakt die Stundentafel-Technik):

- Varianten-Auswahl (Uebersichtstabelle mit Scorecard-Spalten,
  Zeilenklick — dasselbe Interaktionsmuster wie die
  Loesungsuebersicht), „Vergleichen mit“-Diff zwischen Varianten
  (anders zugeordnete Kinder markiert).
- Board-Ansicht: Klassen als Spalten, Kinder als Karten mit
  **Ampel-Chips je betroffenem Kriterium** (Farbe + Symbol ✓/!/✗/–,
  Tooltip mit Klartext aus dem Bewertungslauf) und Worst-of-Rand;
  Filter „nur gelb/rot“ und „Konsens-Kern“.
- Kennzahlen je Klasse (Groesse, Balance-Balken w/m etc.).

Manuelle Fixierungen im Viewer bleiben eine Ausbaustufe: der ehrliche
CLI-Workflow dafuer ist „`fixierungen:` in der YAML ergaenzen und
`klassen` erneut laufen lassen“ — das deckt F1/F2 (und via
`modus: hard` auch F3/F5) bereits vollstaendig ab, nur eben ueber die
Eingabedatei statt per Klick.

## 6. Umsetzungsschritte

| Schritt | Inhalt | Aufwand | Ergebnis/Beleg |
|---|---|---|---|
| K1 | Datenmodell + YAML-Laden + Fail-Fast-Validierung (`Klassenbildung.vb`-Datenteil, `YamlKlassenbildung`) | klein | Unit-Tests: Laden des Konzept-Beispiels, alle Validierungsfaelle |
| K2 | Kern-Solver: Grundconstraints + 4 Regeltypen hart/weich, Gewichts-Modus, Symmetriebrechung, Fixierungen F1/F2 | mittel | handnachgerechnete Mini-Szenarien je Regeltyp (Muster `LehrereinsatzplanungTests`): Buendelung hart erzwingt, weich zaehlt spread−1; Kappe; Balance mit Toleranzband; zusammen/getrennt; Prio-Dominanz (1 kritische schlaegt n niedrige) |
| K3 | `KlassenbildungQuality` (Bewertung + Chips) + Varianten-Schleife (`SolveKlassenbildungTop`) + Konsens-Kern | mittel | Tests: Bewerter reproduziert Solver-Verletzungen unabhaengig; Varianten paarweise ≥ d verschieden und ≤ (1+eps)·z*; Symmetrie: ohne Brechung Scheinvielfalt (Testfall), mit Brechung nicht |
| K4 | SchoolTestRunner: `klassen`-Subkommando, config-Block, `klassenbildung.md` + `.json` | klein-mittel | Beispiel-Fixture „Beispiel A“ (Konzept 12.1: ~100 Kinder, 4 Klassen, komplettes Regelwerk als YAML, Schuelerdaten per Skript generiert) unter `tests/bw-grundschule-beispiel/input/`; Beleg-Lauf mit Messhistorie |
| K5 | Viewer `klassenbildung.html` (Varianten + Board + Ampel + Diff) + `render`-Anbindung | mittel | Headless-Chromium-Smoke nach CLAUDE.md-Regel (Chips zaehlen, Variantenwechsel, Diff) |
| K6 | Ausbaustufen nach Bedarf: Assumptions-Konfliktkern (falls nicht schon in K2 gelungen), lexikographischer Prio-Modus, F7-Stabilitaets-Strafterm (`basis_variante:` in der YAML), `--uebernehmen`-Kopplung an die Stammdaten, GMS-Fixture „Beispiel B“ | je klein-mittel | jeweils Test + Beleg-Lauf |

Empfohlener Zuschnitt: K1+K2 als erster Commit (Solver steht, volle
Suite Pflicht), K3+K4 als zweiter (nutzbares CLI-Ergebnis), K5 als
dritter. K6 einzeln auf Zuruf.

## 7. Bewusste Entscheidungen / Abgrenzung

- **Eigenes Schema statt `entities/constraints`-JSON:** Die
  Klassenbildung teilt mit dem Stundenplan-Constraint-Format nur die
  Philosophie (hart/weich, Fail-Fast), nicht die Struktur — ein
  eigenes, dem Konzept 1:1 folgendes YAML ist erklaerbarer als ein
  Zwang in `constraints[]`. Gemeinsame Muster bleiben auf Code-Ebene
  (Validation-Stil, Verifier-Prinzip, SolveTop-Schleife).
- **Pseudonym-IDs sind Vertragsbestandteil:** Der Runner kennt nur
  S-IDs (Konzept: Datenminimierung); die Aufloesungstabelle bleibt
  ausserhalb des Repos. In den Beispiel-Fixtures werden synthetische
  Schueler generiert.
- **Art. 22/Audit-Log:** Das CLI liefert die *Inhalte* des Nachweises
  (Regelwerk-Snapshot, Parameter, Scorecards, Verletzungsreport im
  Output je Lauf — Konzept 10.1, Zeilen 1–2). Wer/wann/Freigabe sind
  Prozess- und UI-Themen einer spaeteren Anwendung, nicht des
  Test-Runners — das dokumentiert der Report ausdruecklich
  („menschliche Letztentscheidung erforderlich“).
- **Kein `enumerate_all_solutions`, kein SolutionCallback fuer
  Varianten** — die Diversifikations-Schleife ist das im Repo
  vielfach validierte Werkzeug (Konzept 8.2 kommt zum selben Schluss).

## Umsetzungsstand

- **K1 umgesetzt:** Datenmodell (`KlassenbildungInput` mit Schueler/
  Klassen/Gruppen/Balance/Wuensche/Fixierungen inkl. F2
  `nicht_klasse`), YAML-Loader (`YamlKlassenbildung`,
  SchoolTestRunner) und Fail-Fast-Validierung
  (`ValidateKlassenbildung`: unbekannte IDs, Verteilung ohne Kappe,
  treffer-lose Balance, Kapazitaets-/Fixierungs-Widersprueche).
- **K2 umgesetzt:** `SolveKlassenbildung` mit allen vier Regeltypen
  hart/weich (Buendelung y-/used-Encoding, Verteilungs-Kappe/-Ueberlauf,
  Balance-Korridor bzw. einseitige |diff|-Schranken, Wunsch-Paar-
  Linearisierung), Gewichtsstufen-Modus (Default 1000/50/1 statt
  10000/100/1 - P6-Befund), Praezedenz-Symmetriebrechung ueber die
  freien Klassen, Fixierungen F1/F2, Verletzungsreport je weicher
  Regel. Bewusst nur die im Repo live verifizierten CP-SAT-Primitiven.
  9 handnachgerechnete Tests (Regeltyp-Encodings, Prio-Dominanz,
  kanonischer Repraesentant, Fixierungs-Vertraeglichkeit der
  Symmetriekette); volle Suite 271/271 gruen.
- **K3 umgesetzt:** `KlassenbildungQuality.Bewerte` (unabhaengiger
  Auszaehl-Bewerter nach Verifier-Prinzip: Verletzungsmass je Regel +
  Ampel-Chips gruen/gelb/rot je (Kind, Kriterium) inkl. der
  rechnerischen Gelb-Faelle Kappe-voll/Toleranzrand) und
  `SolveKlassenbildungTop` (Varianten-Schleife mit Qualitaetsschranke
  optimum*(1+epsilon), Diversitaets-Cut, Hints, Konsens-Kern). 4 neue
  Tests, u.a. Bewerter==Solver-Cross-Check und Qualitaetsband bei
  epsilon 0.
- **K4 umgesetzt:** `klassen <schule>`-Subkommando (KlassenRun.vb) mit
  `klassenbildung:`-Config-Block, klassenbildung.md/.json-Outputs und
  Bewerter-Cross-Check als FAIL-Kriterium. Beispiel-A-Fixture (100
  synthetische Kinder, Regelwerk 1:1 nach Konzept 12.1) im
  bw-grundschule-beispiel; Beleg-Lauf: 3 Varianten, alle Optimal
  (Zielwert 1001 = vorhergesagter Sozialverhalten-Ueberlauf 1000 +
  Nordstadt-Split 1), Konsens-Kern 33/100, Diffs 11/59 Kinder.
- **Separate Regressionssuite:** die Klassenbildungs-Tests leben im
  eigenen Testprojekt `Klassenbildung.Tests` (13 Tests, < 1 s) - bei
  reinen Klassenbildungs-Aenderungen muss die teure Stundenplan-Suite
  (`TimetableCore.Tests`, ~6 Min) nicht mitlaufen; Regel in CLAUDE.md.
- **K5 umgesetzt:** self-contained Viewer `klassenbildung.html`
  (Template + KlassenbildungHtml als Embedded Resource, vom
  `klassen`-Lauf geschrieben und per `render` aus vorhandener JSON
  regenerierbar): Varianten-Uebersicht mit Ampel-Zaehlern,
  Board-Ansicht (Klassen-Spalten, Karten mit Chips ✓/!/✗ +
  Klartext-Tooltips, Worst-of-Rand, Konsens-Punkt),
  Balance-Kennzahlen je Spaltenkopf, Varianten-Diff und
  Kritisch-Filter. JSON dafuer um balance_kennzahlen und den
  Klassenrahmen erweitert. Chromium-Interaktionstest gegen
  unabhaengige Python-Nachrechnung: Ampel 4/41/44/11, Konsens 23,
  Diff V2-V1 54, Kritisch-Filter 32 - alles exakt getroffen.
