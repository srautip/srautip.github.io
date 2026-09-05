# timetable-dotnet - Arbeitsregeln

## Testumfang nach Aenderungsbereich (feste Regel)

Den Prueful auf den tatsaechlichen Aenderungsbereich zuschneiden, statt
pauschal die volle Suite zu fahren:

- **Aenderungen an `TimetableCore/`** (Solver, SolveTopObjective,
  Validation, Verifier, Formatting, ScheduleQuality, Stammdaten,
  Lehrereinsatzplanung, ...): volle Suite ist Pflicht -
  `dotnet test TimetableCore.Tests` muss vollstaendig gruen bleiben
  (Definition-of-Done-Konvention aller Phasen-Berichte in `docs/`).
- **Aenderungen NUR an der Klassenbildung** (`TimetableCore/
  Klassenbildung.vb`, `TimetableCore/KlassenbildungQuality.vb`,
  `TimetableWorkflow/KlassenbildungLauf.vb`, `TimetableWorkflow/
  KlassenbildungBericht.vb`, `TimetableYaml/YamlKlassenbildung.vb`,
  `SchoolTestRunner/KlassenRun.vb`, `tests/*/input/klassenbildung.yaml`): die
  EIGENE Suite genuegt - `dotnet test Klassenbildung.Tests` (laeuft in
  unter einer Sekunde; die Klassenbildung ist ein eigenstaendiges
  Modul ohne jede Kopplung an Solver/SolveTop, die teuren
  Stundenplan-Tests exerzieren sie nicht und umgekehrt). Bei
  querschneidenden Aenderungen (z.B. gemeinsame Helfer) beide Suiten
  fahren.
- **Aenderungen an `TimetableYaml/`, `TimetableWorkflow/`, `TimetableProjekt/` oder `TimetableGui/`** (seit dem
  GUI-Unterbau, siehe `docs/gui-implementierungsplan.md`): die eigenen
  Suiten `dotnet test TimetableYaml.Tests` (<1s) und
  `dotnet test TimetableWorkflow.Tests` (~3s), `dotnet test TimetableProjekt.Tests`
  (~2s) bzw. `dotnet test TimetableGui.Tests` (~45s, faehrt echte
  Klassenbildungs-Laeufe). Beruehrt die Aenderung die
  Pipeline-Orchestrierung oder die Berichts-Zeichenketten, zusaetzlich
  `dotnet run --project SchoolTestRunner -- run --all` - nur der echte
  Lauf belegt, dass die committeten Beispiel-Outputs unveraendert bleiben.

- **Aenderungen an einem Fenster unter `TimetableGui/Masken/`**: die
  Suite prueft ViewModels und laeuft sonst headless - ein
  `StaticResource`-Schluessel, den es nicht gibt, oder ein
  `FindResource` im Code-Behind knallt deshalb erst beim FENSTERAUFBAU
  und ist kein Compilerfehler. `Fensterprobe.vb` haelt das Werkzeug bereit:
  eigener STA-Thread, `Application.InitializeComponent` einmal, Fenster
  bauen, Ausnahme an den Test zurueckreichen (`RegelnFensterTests`,
  `ProjektAssistentFensterTests` nutzen es). Wer ein neues Fenster
  anlegt, haengt dort eine Aufbau-Pruefung an, statt sich auf einen
  manuellen Start zu verlassen. (Live belegt: `schrift-mono` stand in
  der Vorlage, aber nicht in `Tokens.xaml`.)

- **Sichtpruefung der GUI ohne Bildschirm: die Bildprobe.** Ein laufendes
  WPF-Fenster laesst sich von aussen NICHT fotografieren (DirectX -
  `CopyFromScreen`/`PrintWindow` liefern Weiss; live erlebt 05.09.2026).
  Stattdessen rendert die Anwendung sich selbst:

  ```bash
  dotnet run --project TimetableGui -- --bildprobe "$TEMP/bildproben" --schule tests/bw-grundschule-beispiel --masken
  ```

  schreibt `01-start-ohne-projekt.png`, `02-start.png`, `03-klassenbildung.png`
  (Board eingeblendet), `04-stundenplan.png`, `05-laeufe.png` und mit
  `--masken` die vier Pflegemasken, beendet sich dann (Exitcode 0; bei
  Fehler `fehler.txt` im Ordner, Exitcode 1). `--schule` haengt die
  `output/*.json` der Beispielschule als Staende ein - beide Dashboards
  ohne Solver-Lauf. `--projekt <datei.splanx>` mit Passwort aus
  `SCHULPLANUNG_PASSWORT`; `--rechnen stundenplan|klassenbildung` rechnet
  vorher wirklich. Die PNGs mit `Read` ansehen. Eine laufende Instanz
  sperrt `bin/` - vor dem Build pruefen (`Get-Process TimetableGui`).

- **Aenderungen an `TimetableWorkflow/Templates/design-tokens.css` oder
  `design-basis.css`** (Designsystem-Quellen, arc42 8.16): beide Dateien
  werden NICHT zur Laufzeit injiziert - die zwei Viewer-Vorlagen und
  `TimetableGui/Design/Tokens.xaml` tragen zeichengleiche Kopien.
  Die Waechter ziehen eine Aenderung NICHT selbst nach, sondern melden
  die Abweichung und drucken den kopierbaren Soll-Block.
  Zu fahren: `dotnet test TimetableWorkflow.Tests` (Regionen, Lints) und
  `dotnet test TimetableGui.Tests` (Kanon CSS <-> XAML, Symbolzeichen,
  tote Token). Beruehrt die Aenderung das Aussehen, zusaetzlich
  `dotnet test TimetableViewer.Tests` (Tastatur, Dichte, Lesbarkeits-
  grenze). Begruendung fuer "Kopie statt Injektion" im Kopf der
  CSS-Datei.

  Die Kopien setzt `perl tools/design-einsetzen.pl <vorlage.html>
  <eigen.css> <tokens.css> <basis.css>` - es baut den `<style>`-Inhalt
  aus Token-Region, Basis-Region und vorlageneigenem CSS in genau dieser
  Reihenfolge und schreibt CRLF.

- **NIEMALS `sed -i` auf `Templates/*.html` oder `tests/*/output/*.html`.**
  Diese Dateien sind CRLF; `sed -i` schreibt sie still auf LF um und
  erzeugt damit einen Diff ueber die ganze Datei (live erlebt: 1.269
  stille Byte-Aenderungen). `perl -0pi` mit `:raw` oder das Edit-Werkzeug
  erhalten die Zeilenenden; `Write` dagegen schreibt LF.

- **Reine CSS-Umbauten** an den Vorlagen zusaetzlich mit
  `powershell -File tools\css-umbau-pruefen.ps1 -Referenz <ordner>`
  belegen: alles AUSSERHALB von `<style>` muss byteweise unveraendert
  bleiben - das sind ueber 99 % der Datei. Vor dem Umbau einmal mit
  `-Anlegen` die Referenz erzeugen.

- **Aenderungen nur an `SchoolTestRunner/`-CLI oder
  `TimetableWorkflow/Templates/*.html`** (Viewer): die Suite
  deckt beides NICHT ab - stattdessen gezielt pruefen:
  1. `dotnet build TimetableCore.sln` (Compile inkl. Embedded-Resource),
  2. `dotnet run --project SchoolTestRunner -- render <schule>` gegen
     beide Beispiel-Schulen (baut stundentafel.html aus vorhandener
     stundenplan.json neu - KEIN teurer Solver-Lauf noetig),
  3. Bei Aenderungen an den Vorlagen selbst: `dotnet test
     TimetableViewer.Tests` (Playwright gegen Edge, headless, ~1min) -
     prueft VERHALTEN (Drag & Drop, Pins, beide Betriebsarten der
     Bruecke), nicht nur "laeuft das JS".
  4. Headless-Browser-Smoke gegen die generierten Seiten. Unter Windows:
     `powershell -File tools\viewer-smoke.ps1` (nutzt Edge, prueft alle
     Viewer-Seiten unter `tests/`). Unter Linux liegt Chromium unter
     `/opt/pw-browsers/`; in beiden Faellen fuehrt `--headless --dump-dom
     --virtual-time-budget=...` das Inline-JS aus. Fuer Interaktionstests
     ein kleines Skript vor `</body>` injizieren, das Events dispatcht und
     Ergebnisse in `document.title` schreibt.
  Aendert sich das JSON-Schema des Exports (`Formatting.
  ToStundentafelJson` liegt in TimetableCore!), gilt wieder die volle
  Suite plus `StundentafelJsonTests`.
- **Aenderungen nur an `tests/<schule>/input/*.yaml` oder Doku**: kein
  Testlauf noetig; Config-Aenderungen werden per `run <schule>`-Lauf
  live belegt und die Outputs mitcommittet (bestehende Konvention).

"Volle Suite" heisst seit dem GUI-Unterbau ALLE SIEBEN Testprojekte, am
einfachsten ueber `dotnet test TimetableCore.sln`.

Hintergrund: Die Regel entstand, nachdem fuer einen reinen
Viewer-Commit die volle 5-Minuten-Suite lief, obwohl sie den
geaenderten Code gar nicht exerziert - Build + render + Browser-Smoke
waren dort die tatsaechlich tragende Validierung.

## Weitere Konventionen (Kurzreferenz)

- Beispiel-Outputs (`tests/*/output/`) sind generiert und werden nach
  jedem Lauf mitcommittet.
- **GitHub Pages wird nicht mehr gepflegt** (Nutzerentscheidung
  23.08.2026). Die Kopien unter `../stundentafel/*.html` sind damit ein
  eingefrorener Altstand und werden nach einem Lauf NICHT mehr
  nachgezogen. Der Vorschau-Kanal sind die Claude-Artifacts unten.
  Sollen die Seiten spaeter wieder aktuell sein, ist das ein bewusster
  Schritt - nicht die Nebenwirkung eines Laufs.
- Merge nach `main` weiterhin nur auf explizite Nutzeranweisung. Der
  Grund ist jetzt ein anderer: `main` ist der Standardzweig eines
  oeffentlichen Repositorys, nicht mehr die Quelle einer
  ausgelieferten Seite.
- Messlaeufe ehrlich dokumentieren (inkl. negativer Befunde) als
  Kommentar in der jeweiligen `config.yaml` bzw. in `docs/`; bei
  `num_workers > 1` sind Laeufe trotz fixem Seed nicht deterministisch -
  Einzellaeufe entsprechend vorsichtig interpretieren.
- Lange Laeufe (>10 Min) nicht direkt im Tool-Aufruf starten, sondern
  als `setsid nohup`-Skript entkoppeln und per Log ueberwachen.

## VB-Fallstricke bei der Namensaufloesung (wiederholt aufgetreten)

**VB unterscheidet KEINE Gross-/Kleinschreibung.** `Stunden` und
`stunden` sind derselbe Bezeichner. Das ist die mit Abstand haeufigste
Fehlerquelle in diesem Projekt gewesen - fuenfmal in verschiedenen
Ausfuehrungen.

**Die Regel:** Ein Parameter oder eine lokale Variable bekommt NIE
denselben Namen wie ein Mitglied, mit dem sie im selben Rumpf zu tun
hat. Also `anzahlStunden` statt `stunden` neben `Stunden`, `BaueZelle`
statt `Zelle` neben `zelle`, `LadeSchule` statt `Schule` neben
`schule`.

**Warum das nicht der Compiler erledigt:** Er faengt nur einen Teil der
Faelle - und ausgerechnet der gefaehrlichste bleibt still.

| Fall | Was passiert | Compiler |
|---|---|---|
| `Public Sub New(stunden As Integer)` mit Eigenschaft `Stunden`; im Rumpf `Stunden = stunden` | **Selbstzuweisung des Parameters.** Die Eigenschaft bleibt auf ihrem Standardwert - hier 0, wodurch KEINE Rasterzelle mehr gueltig war. | **schweigt** |
| Lokales `Dim zelle = Zelle(...)` neben `Function Zelle` | - | `BC30980: Type of 'zelle' cannot be inferred from an expression containing 'zelle'` |
| `Region(...)`-Funktion, lokal `Dim region = Region(...)` | - | `BC30980` |
| Helfer `Schule(name)` neben Parameter `schule`; `Schule(schule)` | indiziert den STRING statt die Funktion zu rufen | `BC30311: Value of type 'Char' cannot be converted to 'Projekt'` |
| `Melde(SolveProgress)` in einer Klasse mit geerbtem `Melde(name)` | Ueberladung kollidiert mit der Basis | Fehler beim Ueberschreiben |
| `x:Name="Matrix"` im XAML, dazu `Private _matrix` im Code-Behind | WPF erzeugt `_Matrix` - fuer VB derselbe Bezeichner | `BC30260: '_matrix' is already declared` |
| `“` (U+201C) in einem VB-String, z.B. `"“Pruefen"` | VB akzeptiert die typografischen Anfuehrungszeichen ALS STRINGBEGRENZER - der String endet dort | Folgefehler an ganz anderer Stelle |

**Diagnose fuer den letzten Fall** (dreimal an einem Tag passiert): meldet
der Compiler `'Module' statement must end with a matching 'End Module'`,
`'For' must end with a matching 'Next'` oder `Character is not valid` an
einer Stelle, die offensichtlich in Ordnung ist, dann steht weiter OBEN ein
`“` in einem String. Die Meldung zeigt nie auf die Ursache. Suchen mit:

```bash
grep -rn $'\u201c' --include='*.vb' TimetableGui
```

`TimetableGui.Tests/QuelltextWaechterTests` faengt die Faelle, die den Build
NICHT brechen; die uebrigen findet nur der grep - denn wenn die Uebersetzung
scheitert, laeuft auch der Test nicht.

Die erste Zeile ist der Punkt: **ein Test hat sie gefunden, nicht der
Compiler.** Wer eine Eigenschaft im Konstruktor setzt, prueft ihren
Wert danach - oder benennt den Parameter von vornherein anders.

**Verwandt, gleiche Familie:** `List(Of T).Count` ist in VB eine
EIGENSCHAFT und verschattet die LINQ-Ueberladung `Count(predicate)`.
Statt `liste.Count(Function(x) ...)` also `liste.Where(...).Count`.

## Viewer-Artifacts (der Vorschau-Kanal)

Seit GitHub Pages nicht mehr gepflegt wird, sind die Claude-Artifacts
der EINZIGE Vorschau-Kanal: private, stabil verlinkte Seiten - ohne
Merge nach `main`.

**Die drei Beispiel-Artifacts werden NICHT mehr aktualisiert**
(Nutzerentscheidung 28.08.2026). Sie sind damit ein eingefrorener
Altstand, so wie die GitHub-Pages-Kopien seit dem 23.08. Nicht
nachziehen, auch nicht nach einem `run`-Lauf, und nicht danach fragen.
Wer eine aktuelle Ansicht braucht, öffnet
`tests/<schule>/output/stundentafel.html` per Doppelklick - der
Doppelklick-Betrieb ist eine zugesicherte Betriebsart (arc42 8.10) und
per Test abgesichert.

- Grundschule: https://claude.ai/code/artifact/d644d791-48e1-4bbd-89fa-02d2cf13fe09
- GMS: https://claude.ai/code/artifact/ae942861-a1ae-4664-a1f5-51ac9ce702d1
- Klassenbildung Grundschule: https://claude.ai/code/artifact/d00f8a57-75ca-4809-9690-19613dd071a1

Stand der drei: vor Stufe G2. Ihnen fehlt die Aktionsleiste der
Brücke (+111 Zeilen je Stundentafel) und der Freigabe-Knopf im
Board. Sichtbar wäre der Unterschied ohnehin nicht - ohne
WebView2-Host bleibt beides verborgen.

- Designsystem-Muster: https://claude.ai/code/artifact/44e7835c-6ceb-467e-b7d8-234be0185cdb
  (Favicon 🎨) - KEIN Generat aus dem Repo, sondern die Entwurfsseite
  zu arc42 8.16: Palette, Typo-Skala, Komponenten und Symbolsatz mit
  Umschaltern für Dichte und Vorher/Nachher. Dieses eine wird bei
  einer Änderung AM DESIGNSYSTEM weiterhin nachgezogen - es bildet
  keinen Lauf ab, sondern den Entwurf.
