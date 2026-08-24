# SchoolTestRunner - Benutzerhandbuch

Dieses Dokument richtet sich an **Endnutzer ohne Programmierkenntnisse**
(z.B. Schulleitung, Sekretariat), die für ihre Schule automatisch einen
Stundenplan erstellen lassen möchten. Wer Testfälle selbst als YAML-Dateien
anlegen oder anpassen will, findet die vollständige technische
Feldreferenz in [`tests/README.md`](../tests/README.md) - dieses Dokument
hier ist bewusst kürzer und erklärt nur den Weg von "leeres Verzeichnis"
bis "fertiger Stundenplan".

## Was ist SchoolTestRunner?

`SchoolTestRunner` ist ein Kommandozeilen-Werkzeug, das aus einer
einfachen Beschreibung Ihrer Schule (Klassenstufen, Klassen, Lehrkräfte,
Fächer) **automatisch** einen kompletten, geprüften Stundenplan erzeugt -
inklusive der Zuteilung, wer welches Fach in welcher Klasse unterrichtet.
Sie müssen dafür **keinen Programmcode schreiben**: die gesamte Eingabe
sind drei Textdateien im YAML-Format, die auch direkt im Web-Editor von
GitHub bearbeitet werden können.

Das Werkzeug prüft dabei automatisch, ob der erzeugte Plan tatsächlich
widerspruchsfrei ist (kein Lehrer an zwei Orten gleichzeitig, keine Klasse
doppelt belegt, alle Wochenstunden korrekt verteilt, ...) - ein Lauf, der
mit `PASS` endet, hat 0 offene Verstöße.

## Voraussetzungen

- Das .NET 8 SDK ist installiert (`dotnet --version` zeigt `8.x`).
- Eine lokale Kopie dieses Repositories.
- Für die Kommandos unten: ein Terminal, geöffnet im Verzeichnis
  `timetable-dotnet/` (wichtig - die Pfade `tests/...` werden relativ
  dazu aufgelöst).

Kein Internet, kein LLM/Ollama-Server nötig - `SchoolTestRunner` arbeitet
rein mit den YAML-Dateien, keine KI-Extraktion beteiligt (das ist ein
anderer, separater Teil des Projekts).

## Schnellstart

**1. Grundgerüst für eine neue Schule erzeugen:**

```bash
cd timetable-dotnet
dotnet run --project SchoolTestRunner -- new meine-schule \
  --schulart Grundschule --bundesland BW --klassenstufen 4 --lehrer 8
```

Das legt `tests/meine-schule/input/stammdaten.yaml` (mit plausiblen
Standardwerten für eine 4-stufige, 2-zügige BW-Grundschule mit 8
Klassenlehrkräften) und eine leere `constraints.yaml` an. Unterstützte
Schularten aktuell: `Grundschule` und `Gemeinschaftsschule`, jeweils nur
für `--bundesland BW` (andere Bundesländer liefern eine klare
Fehlermeldung statt erfundener Zahlen).

**2. Stundenplan berechnen:**

```bash
dotnet run --project SchoolTestRunner -- run meine-schule
```

Das dauert je nach Schulgröße von wenigen Sekunden bis zu mehreren
Minuten. Am Ende steht eine Zeile wie

```
[meine-schule] PASS - Lehrereinsatzplanung=Optimal (Objective=0), Solver.SolveTop=MaxSolutionsReached, CP-SAT-Status=Optimal (Kann-Verstoesse=0, Quality.Total=12.3), Verstoesse=0
```

`PASS` und `Verstoesse=0` bedeuten: der Plan ist vollständig und
widerspruchsfrei. (`FAIL` bzw. eine Verstoß-Anzahl > 0 zeigt an, dass die
Stammdaten überarbeitet werden müssen - z.B. zu wenige Lehrkräfte für die
vorgegebenen Wochenstunden.)

**3. Ergebnis ansehen** - alles landet in `tests/meine-schule/output/`:

| Datei | Inhalt |
|---|---|
| `lehrerzuteilung.md` | Wer unterrichtet was, wer ist Klassenlehrer:in welcher Klasse |
| `stundenplan.md` | Der fertige Stundenplan, ein Raster pro Klasse und pro Lehrkraft |
| `stundenplan.json` | Dieselben Daten maschinenlesbar, inkl. aller gefundenen Alternativ-Pläne |
| `stundentafel.html` | Eine interaktive Gesamtübersicht - **einfach im Browser per Doppelklick öffnen**, kein Server nötig |

`stundentafel.html` zeigt alle Klassen gleichzeitig in einer Tabelle
(Wochentage × Klassenstufen, Schulstunden × Parallelklassen a/b/c/...) und
bietet - falls mehrere Alternativ-Pläne berechnet wurden - ein
Auswahlmenü, um zwischen ihnen umzuschalten.

## Eigene Anpassungen

Danach können Sie `tests/meine-schule/input/stammdaten.yaml` direkt
bearbeiten (z.B. Wochenstunden ändern, weitere Klassen ergänzen) und
`constraints.yaml` um zusätzliche Regeln erweitern, die das Grundgerüst
nicht automatisch ableitet - z.B. "Lehrkraft X ist nur montags bis
mittwochs verfügbar" oder "Sport findet immer in der Turnhalle statt".
Nach jeder Änderung genügt ein erneutes `run meine-schule`, um einen
aktualisierten Plan zu erhalten - `output/` wird dabei jedes Mal komplett
neu geschrieben.

Wie lange der Solver nach dem bestmöglichen Plan suchen darf, und wie
stark verschiedene Qualitätskriterien (z.B. "möglichst wenige
Springstunden") gegeneinander gewichtet werden, lässt sich über eine
optionale `config.yaml` einstellen - die vollständige Liste aller Felder
mit Erklärung steht in [`tests/README.md`](../tests/README.md), Abschnitt
"`config.yaml` (optional)".

## Die beiden mitgelieferten Beispiele

Im Repository liegen zwei bereits fertig erzeugte und durchgerechnete
Beispielschulen, die Sie direkt als Vorlage kopieren oder einfach zur
Ansicht öffnen können (`tests/<schule>/output/stundentafel.html`).

### `tests/bw-grundschule-beispiel/` - Grundschule Baden-Württemberg

Eine 4-stufige, 2-zügige BW-Grundschule (Klassenstufen 1-4, Klassen
1a/1b bis 4a/4b - 8 Klassen, 13 Lehrkräfte, 13 Fächer inkl.
Deutsch-/Mathematik-Förderstunden). Über das Grundgerüst hinaus wurde
dieses Beispiel handverfasst erweitert, um mehrere reale
Schulalltags-Situationen zu zeigen:

- **Realistisches Zeitraster**: Klasse 1 und 2 haben ausschließlich
  Vormittagsunterricht (Klasse 1 zusätzlich ohne 1. und - nach Möglichkeit
  - ohne 6. Stunde), Klasse 3 und 4 dürfen nur dienstags Nachmittags-
  unterricht haben.
- **Klassenübergreifende Gruppen**: Religion (evangelisch/katholisch) und
  Ethik werden nicht pro Klasse, sondern pro Klassenstufe unterrichtet -
  Schüler:innen aus beiden Parallelklassen einer Stufe kommen für diese
  Stunde zusammen.
- **Chor-Gesamtprobe**: donnerstags in der 6. Stunde findet für ALLE 8
  Klassen gleichzeitig eine gemeinsame Chorstunde statt (erzwungener
  Termin, der automatisch mit der Klasse-1-Ausnahme oben zusammenspielt).

Damit ist dieses Beispiel das umfangreichere der beiden - ein gutes
Vorbild dafür, wie feste Zeitraster-Vorgaben, gemeinsame Fächer über
Klassengrenzen hinweg und ein schulweiter Fixtermin gleichzeitig
abgebildet werden können.

### `tests/bw-gms-beispiel/` - Gemeinschaftsschule Baden-Württemberg (Sek. I)

Eine realitätsnahe, 6-stufige, 4-zügige BW-Gemeinschaftsschule
(Klassenstufen 5-10, 24 Klassen, ~696 Schüler, 48 Lehrkräfte) nach der
BW-Kontingentstundentafel Gemeinschaftsschule (gültig ab 1.8.2025) -
anders als `bw-grundschule-beispiel` NICHT über den `new`-Scaffold
erzeugt, sondern von Hand nachgebildet, um die volle Bandbreite der
Gruppen-/Parallelverbund-Mechanik (Phase 2.20/2.23) an einem einzigen,
in sich konsistenten Referenzfall zu zeigen:

- **Niveaudifferenzierung ab Kl.7** (Deutsch/Mathematik/Englisch): G-/
  E-Kurs in Kl.7/8, zusätzlich A-Kurs ab Kl.9 - jeder Kurs läuft
  klassenstufenweit über alle 4 Parallelklassen synchron, zusätzlich in
  klassengroße Sektionen (max. 35 Schüler) aufgeteilt, sobald die
  Kursgröße das überschreitet.
- **Wahlpflichtbereich ab Kl.6** (Technik/AES/Französisch), **Profilfach
  ab Kl.8** (NwT/IMP/Sport-/Musik-/BK-Profil - laut Vorgabe
  "Doppelqualifikation vorhandener Fachlehrer", keine eigenen
  Lehrkräfte) und **Religion-ev/-kath/Ethik** über alle 6 Klassenstufen.
- **Fachraumbedarf** über `constraints.yaml`/`room_requirement`
  (Sporthallen, NaWi-/Bio-/Musik-/Kunst-/Technik-/AES-/Computerräume).
- Lehrkräfte sind bedarfsgenau (statt pauschal an der Klassenzahl)
  bemessen, da eine klassenstufenweite Gruppe die Nachfrage über alle 4
  Parallelklassen auf eine Lehrkraft konsolidiert - siehe `tests/README.md`
  für die vollständige Beschreibung inkl. der Kollisions-Sicherheitsmarge
  bei zeitgleichen Sektionen.

Dieses Beispiel eignet sich als Vorlage für eine größere, weiterführende
Schule mit klassischem Fachlehrerprinzip, echter Niveaudifferenzierung
und Wahlbereichen statt des Grundschul-typischen Klassenlehrerprinzips.

Beide Beispiele erreichen bei ihrem hinterlegten Zeitbudget `PASS` mit 0
Verstößen. Die konkreten Zahlen (Wartezeit, wie "gut" der gefundene Plan
laut Qualitätskriterien ist) hängen vom eingestellten Zeitbudget in der
jeweiligen `config.yaml` ab - Details dazu direkt in `output/stundenplan.md`
unter "Optimalitäts-Lücke".

## Bekannte Grenzen

- Räume werden aus den Stammdaten NICHT automatisch abgeleitet - eine
  Raumbindung (z.B. "Sport nur in der Turnhalle") muss handverfasst in
  `constraints.yaml` ergänzt werden.
- Bei sehr großzügigem Zeitbudget kann CP-SAT (der zugrundeliegende
  Solver) einen guten Plan finden, aber trotzdem nicht beweisen können,
  dass es keinen noch besseren gibt (`CP-SAT-Status: Feasible` statt
  `Optimal` in `stundenplan.md`) - das ist normal bei größeren Schulen und
  kein Fehler.
- `output/` wird bei jedem Lauf komplett überschrieben - eigene Notizen
  gehören nicht dorthin.

## Weiterführende Dokumentation

- [`tests/README.md`](../tests/README.md) - vollständige YAML-Feldreferenz,
  alle CLI-Parameter, alle `config.yaml`-Optionen im Detail.
- [`docs/arc42-architecture.md`](arc42-architecture.md), Abschnitt 6.7 -
  technischer Aufbau der `SchoolTestRunner`-Pipeline.
- [`docs/json-constraints-reference.md`](json-constraints-reference.md) -
  Referenz aller in `constraints.yaml` verwendbaren Regeltypen.
