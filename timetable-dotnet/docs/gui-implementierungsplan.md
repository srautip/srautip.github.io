# TimetableGui: Implementierungsplan für die Phase-3-GUI (WPF + WebView2)

## Stand

Stand 05.09.2026. Die Nachträge zu den einzelnen Stufen stehen jeweils
am Ende ihres Abschnitts und halten fest, wo die Umsetzung vom Plan
abgewichen ist und warum.

| Stufe | Inhalt | Stand |
|---|---|---|
| A | Unterbau-Umbau (`TimetableYaml`, `TimetableWorkflow`) | **erledigt** |
| B | YAML-Schreiber, Klassenbildungs-JSON öffentlich, Pipeline als Dienst | **erledigt** |
| C | Projektablage `.splanx` | **erledigt** |
| D | GUI-Durchstich (Klassenbildung) | **erledigt** |
| E | Bridge und U5-Re-Solve | **erledigt** (Klassenbildung; Stundentafel-Bridge in G2) |
| F | Eingabemasken | **erledigt** – siehe Feinschnitt unten |
| G | Stundenplan-Dashboard, Im-/Export, Startseite, Freigabe | **teilweise** – G4 offen |
| H | Hauptfenster nach den zwei Rechnungen: Seitenleiste, Bereichskopf, Leerseite, Startkarten, Menü | **erledigt** |

**Feinschnitt F** (beim Umsetzen entstanden, nicht im ursprünglichen Plan):

| | Inhalt | Stand |
|---|---|---|
| F1 | Querschnitt: Umbenennen kaskadiert, Löschen zeigt Folgen, Rasterpicker | erledigt |
| F2 | Stammdaten-Masken §6.2–6.9 | erledigt |
| F3 | Regeln §6.10: acht Masken, generierte read-only, YAML-Expertenmodus | erledigt |
| F4 | Klassenbildungs-Eingaben §6.11 (Rahmen, Kinder, Zwischenablage), Solver §6.12 | erledigt |
| F5 | Projekt-Assistent §6.1 | erledigt |
| F6 | Gruppen, Balance, Wünsche, Fixierungen – der in F4 offengebliebene Teil von §6.11 | erledigt |

**Feinschnitt G:**

| | Inhalt | Stand |
|---|---|---|
| G1 | Stundenplan rechnen und anzeigen (zweites Dashboard) | erledigt |
| G2 | Brücke im Stundentafel-Viewer (Lösung übernehmen, neu rechnen) | erledigt |
| G3 | Bereich *Läufe*: Stände-Historie, Freigabe, Schutz, Audit | erledigt |
| G4 | **YAML-Ex-/Import im `tests/<schule>/`-Layout** | **offen** |
| G5 | CSV-Import mit freier Spalten-Zuordnung §9.1, Klarnamen-Export | erledigt (ohne §9.2, s.u.) |
| G6 | Startseite als vollständige Schrittleiste §8 | erledigt – durch H2 ersetzt (zwei Karten statt einer Leiste) |

**Feinschnitt H** (Nutzerauftrag 05.09.2026: „Menü nicht sauber
strukturiert, Ablauf zwischen Start und Klassen/Stunden nicht schlüssig“):

| | Inhalt | Stand |
|---|---|---|
| H1 | Seitenleiste auf vier Einträge; `IsChecked` zweiseitig an `Bereich` (Fehlerbehebung) | erledigt |
| H2 | ViewModel: Schritt-Logik je `Rechnungsart`, Startkarten statt Schrittleiste, Pflegemasken hinter `IDialoge` | erledigt |
| H3 | Bereichskopf (Eingaben, Rechnen, Stand-Wechsler) und Leerseite je Rechnung | erledigt |
| H4 | Menü mit *Bearbeiten*; `SolverEinstellungenFenster`; *Speichern unter*, *Projekt schließen* | erledigt |
| H5 | Tests (`StartkartenTests`, Fensteraufbau), Doku | erledigt |

## Was noch offen ist

Zwei Punkte, beide bewusst zurückgestellt und hier festgehalten, damit
sie nicht in Commit-Nachrichten verschwinden – und zwei kleine aus
Stufe H.

**Aus Stufe H, bewusst weggelassen:** *Datei → Zuletzt verwendet* (braucht
eine Einstellungsdatei außerhalb des Projekts – die gibt es bisher nicht)
und *Hilfe → Handbuch* (das Handbuch liegt unter `docs/` und wird nicht
mit der Anwendung ausgeliefert; ein Menüpunkt, der ins Leere zeigt, wäre
schlimmer als keiner).

**G4 – YAML-Ex-/Import im `tests/<schule>/`-Layout.** Ziel laut Stufe G:
„der CLI-Kanal bleibt damit vollwertig erhalten." Ein Schulordner soll
sich als Projekt einlesen *und* ein Projekt als Schulordner
zurückschreiben lassen, sodass beide Wege dieselbe Schule bearbeiten
können.

Der Import-Weg existiert (*Datei → Bestehende Schule übernehmen…*,
`ProjektOrdner.Importieren`); **es fehlt der Export.** Die YAML-Schreiber
selbst gibt es seit Stufe B – `YamlStammdaten.SaveStammdatenYaml` und
die beiden Geschwister für Constraints und Klassenbildung –, die GUI
ruft nur keinen davon auf. Ohne den Export ist der Kanal einseitig: man
kommt von der Kommandozeile in die Oberfläche, aber nicht zurück.

Offen ist dabei auch eine Entscheidung: Was geschieht mit dem
**Klarnamen-Mapping** beim Export? Es gehört nicht in einen
Schulordner (dort liegen pseudonyme Fixtures), darf aber auch nicht
stillschweigend verlorengehen.

**§9.2 – bestehenden Lehrereinsatz per CSV übernehmen.** Die gelebte
Lehrer-Klasse-Fach-Zuteilung als `feste_zuordnungen`, „derselbe
CSV-/Einfüge-Weg mit Spalten Lehrkraft/Klasse/Fach". In G5 ausgelassen,
weil es ein anderer Zielbereich ist – Stammdaten statt Klassenbildung,
eigene Spalten, eigene Prüfung. Es hätte G5 verdoppelt, ohne etwas zu
teilen außer dem Wort „CSV": `Spaltenzuordnung.vb` ist auf Kinder
zugeschnitten (eine Zeile = ein Kind, Rollen für Name/Attribut/Gruppe)
und wäre für Zuordnungs-Tripel neu zu denken.

**Nicht offen, sondern außerhalb dieses Plans:** §9.3 (Bestandsplan
einfrieren) und alles unter „Nicht Teil dieses Plans" am Ende. Von §9.3
ist die eine Hälfte beiläufig entstanden – eine Klasse-Spalte im
CSV-Import erzeugt Fixierungen (G5) –, die andere fehlt: der
`required_slot`-Weg, mit dem ein erfasster Stundenplan zum Startpunkt
wird.

## Context

`docs/gui-ui-konzept.md` (553 Zeilen) und `docs/gui-datenhaltung-konzept.md` (481 Zeilen)
beschreiben die Phase-3-GUI vollständig — beide sind ausdrücklich
**„Konzept-, kein Umsetzungsdokument"**. Dieser Plan übersetzt sie in
umsetzbare Stufen.

Der Kern ist bereit: seit dem Abbruch-/Fortschrittskanal (arc42 §8.11) sind
alle neun Solver-Einstiegspunkte abbrechbar und beobachtbar — die
Voraussetzung für „Lauf im Hintergrund, GUI bleibt bedienbar" (Konzept,
Vorbemerkung 4).

**Der eigentliche Befund der Bestandsaufnahme ist aber ein anderer: fast
alles, was die GUI wiederverwenden soll, liegt heute im falschen Projekt.**
`SchoolTestRunner` ist ein **Exe**-Projekt (`OutputType Exe`, eigene `Main`)
und enthält die YAML-Schicht, die Viewer-Templates, die HTML-Erzeugung, die
Curriculum-Templates und die gesamte Pipeline-Orchestrierung. Eine WPF-App
kann das nicht sauber referenzieren. Vor der ersten XAML-Zeile steht deshalb
ein verhaltensneutraler Umbau.

### Sieben konkrete Lücken (alle verifiziert)

| # | Lücke | Fundstelle |
|---|---|---|
| 1 | Viewer-Templates + `Build*Html` liegen in der Exe; Ressourcenname ist **hartkodiert** `"SchoolTestRunner.stundentafel.html"` und wird über `GetExecutingAssembly()` aufgelöst | `StundentafelHtml.vb:15,24`, `KlassenbildungHtml.vb:7` |
| 2 | Die **Klassenbildungs-JSON** für den Viewer wird nirgends öffentlich gebaut — `BaueJson` ist `Private` | `KlassenRun.vb:187` |
| 3 | **Keine YAML-Schreiber** für `constraints.yaml`, `klassenbildung.yaml`, `config.yaml`; nur `SaveStammdatenYaml` existiert | `YamlConstraints.vb`, `YamlKlassenbildung.vb` |
| 4 | Die Pipeline ist **nicht extrahierbar**: `RunOne` ist eine 280-Zeilen-Prozedur mit 8 `File.WriteAllText` und 7 `Console.WriteLine`; nur ~60–70 Zeilen davon sind echte Logik | `Run.vb:276-556` |
| 5 | `RunConfig`/`LoadConfig` sind **`Friend`** — hinter der Assemblygrenze unsichtbar | `Run.vb:16-155,222` |
| 6 | **`RunOne` nutzt den Abbruch-/Fortschrittskanal nicht** — die Parameter existieren, werden aber nirgends übergeben | `Run.vb:314,424` |
| 7 | Die Templates haben **keine Host-Schnittstelle**: kein `postMessage`, kein `chrome.webview`. `stundentafel.html` hat gar keine; `klassenbildung.html` nur `window.__kbTest` (ausdrücklich „Kein UI-Vertrag") | `klassenbildung.html:1519` |

Gute Nachricht: **kein einziger Test bricht beim Verschieben.** Beide
Testprojekte referenzieren ausschließlich `TimetableCore`; repo-weite Suche
nach `SchoolTestRunner|Yaml*|StundentafelHtml|Scaffold` in Testdateien
liefert null Treffer.

### Entschieden (Nutzerentscheidungen dieser Runde)

- **Planumfang:** ganz V1, in unabhängig abnehmbare Stufen geschnitten.
- **Passwort vergessen = Daten verloren.** Kein Recovery-Schlüssel, keine
  Hintertür. Abmilderung rein organisatorisch (Handbuch: Passwort in den
  Schultresor). Damit ist offener Punkt 1 des Datenhaltungskonzepts geschlossen.
- **Klassenbildung ist das erste Dashboard** — entkoppeltes Modul,
  Sekunden-Laufzeit, einziger Viewer mit vorhandenem JS-Hook, und schließt
  zugleich U5.

---

## Zielarchitektur

Vier neue Projekte. `TimetableCore` bleibt unangetastet — es bekommt
**weder Krypto noch ZIP noch YAML** (Datenhaltungskonzept §8.3).

```
TimetableCore/            unverändert (net8.0)
TimetableYaml/            NEU  net8.0  — YamlDotNet, Lesen UND Schreiben
TimetableWorkflow/        NEU  net8.0  — Pipeline, Viewer-HTML/JSON, Scaffold, RunConfig
TimetableProjekt/         NEU  net8.0  — .splanx-Container, Krypto, Mapping, Audit-Log
TimetableGui/             NEU  net8.0-windows  — WPF + WebView2
SchoolTestRunner/         wird zur dünnen CLI über TimetableWorkflow
RobustnessRunner/         unverändert
```

Warum `TimetableWorkflow` und nicht alles in die GUI: die Pipeline muss
**ohne WPF testbar** bleiben, und die CLI muss sie weiter benutzen — sonst
driften zwei Implementierungen derselben Orchestrierung auseinander.
`TimetableProjekt` ist aus demselben Grund getrennt (Krypto- und
Container-Tests brauchen keinen UI-Thread). Das Datenhaltungskonzept lässt
diese Aufteilung offen („entscheidet die Umsetzung", §8.3); die YAML-Extraktion
folgt seiner ausdrücklichen Empfehlung.

**Neue Abhängigkeiten**, bewusst minimal: `YamlDotNet` (nur `TimetableYaml`,
schon im Repo) und `Microsoft.Web.WebView2` (nur `TimetableGui`). Kein
MVVM-Framework — `INotifyPropertyChanged`/`ICommand` von Hand, passend zur
BCL-Linie des Projekts. Krypto und ZIP sind BCL (`AesGcm`,
`Rfc2898DeriveBytes`, `System.IO.Compression`).

---

## Stufe A — Unterbau-Umbau, verhaltensneutral

Reine Verschiebung, **kein Umbau von Logik**. Ziel: alles, was die GUI
braucht, liegt in referenzierbaren Bibliotheken.

- `TimetableYaml`: `YamlStammdaten.vb`, `YamlConstraints.vb`,
  `YamlKlassenbildung.vb` verschieben; `RunConfig`/`KlassenbildungConfig`/
  `QualityWeightsConfig` aus `Run.vb:16-214` mitnehmen und `LoadConfig`
  **von `Friend` auf `Public`** heben (Lücke 5). `BuildQualityWeights`
  (`Run.vb:235-256`) ebenfalls — es ist das Overlay-Muster, das der
  GUI-Einstellungsdialog spiegelt.
- `TimetableWorkflow`: `StundentafelHtml.vb`, `KlassenbildungHtml.vb`,
  `Templates/*.html` (als `EmbeddedResource`), `Templates.vb`
  (Curriculum-Daten), `Scaffold.vb`.
  **Fallstrick:** beide `ResourceName`-Konstanten tragen das Assembly-Präfix
  `SchoolTestRunner.` und werden über `GetExecutingAssembly()` aufgelöst —
  beim Verschieben müssen Konstante *und* `EmbeddedResource`-Item mitziehen,
  sonst schlägt der Ressourcenzugriff erst zur Laufzeit fehl.
- Projektweiter Global-Import `<Import Include="TimetableCore" />` in jede
  neue vbproj übernehmen (Muster aus `SchoolTestRunner.vbproj:20`).
- `SchoolTestRunner` behält nur `Program.vb` (Argument-Parsing) und die
  Datei-/Konsolen-Schicht.

**Verifikation** — der Umbau muss beweisbar nichts ändern:
1. Beide Suiten grün (`TimetableCore.Tests` 269, `Klassenbildung.Tests` 19).
2. `render bw-grundschule-beispiel` und `render bw-gms-beispiel` → die
   erzeugten HTML sind **byte-identisch** zum Stand vor dem Umbau
   (`render` läuft ohne Solver, ist also deterministisch — der schärfste
   verfügbare Nachweis).
3. Ein `run`-Lauf gegen einen Vorher-Lauf, beide mit `num_workers: 1`
   (nur so greift die Determinismus-Zusage aus arc42 §8.5).

---

## Stufe B — Die vier fehlenden Bausteine

- **YAML-Schreiber** (Lücke 3) für `constraints.yaml`, `klassenbildung.yaml`,
  `config.yaml`. `YamlStammdaten.vb:22` ist die Vorlage: `SerializerBuilder`
  mit `UnderscoredNamingConvention`. Achtung bei `constraints.yaml` — die
  Leseseite ist untypisiert (`JsonObject`) und hat in
  `ScalarStringToJsonValue` (`YamlConstraints.vb:90`) eine handgeschriebene
  Skalar-Typinferenz; der Schreiber muss dazu **symmetrisch** sein.
- **Klassenbildungs-JSON öffentlich** (Lücke 2): `BaueJson`, `BaueGruppenJson`,
  `DiffZuErster`, `AmpelZaehler` aus `KlassenRun.vb` nach `TimetableWorkflow`
  heben. Das Gegenstück für den Stundenplan existiert bereits öffentlich im
  Kern (`Formatting.ToStundentafelJsonMulti`).
- **Pipeline als Service** (Lücken 4+6): `RunOne` und `KlassenOne` in
  I/O-freie Funktionen zerlegen, die Modelle rein und Ergebnisse als Objekte
  nehmen — und **`cancellationToken`/`progress` durchreichen**. Die
  Markdown-Erzeugung bleibt als eigene Funktion daneben (die CLI braucht sie,
  die GUI zeigt sie im Bericht-Export).
  Kritisch zu erhaltende Logik, sonst driftet die GUI von der CLI ab:
  Budget-Aufteilung `SolveTimeLimitS / einsaetze.Count` und
  `(MaxSolutions + n - 1) \ n` (`Run.vb:421-422`), Bestauswahl
  `OrderBy(Quality.Total).First()` (`Run.vb:451`), die Nullable-Auflösung der
  elf Config-Defaults (`Run.vb:394-417`) und die Gap-Formel (`Run.vb:484-485`).
- **Achtung Signaturänderung:** `SolveLehrereinsatzTop` kann seit dem
  Abbruchkanal eine **leere Liste** liefern; `Run.vb:321` greift heute
  unbesehen auf `einsaetze(0)` zu. Der Service muss das abfangen.

**Verifikation:** neue Round-Trip-Tests je YAML-Typ (laden → schreiben →
laden → strukturgleich); Pipeline-Service-Test, der denselben Input wie das
Grundschul-Beispiel fährt und dieselben Kennzahlen liefert; beide Altsuiten
weiterhin grün; `run --all` unverändert `PASS`.

---

## Stufe C — Projektablage `.splanx`

`TimetableProjekt`, vollständig nach Datenhaltungskonzept §5:

- **Container:** ZIP (`System.IO.Compression`) mit JSON-Einträgen, als Ganzes
  verschlüsselt. Layout exakt wie §5.1: `manifest.json`, `stammdaten.json`
  (byte-gleich `Stammdaten.SerializeStammdaten`), `constraints.json` (rohes
  Wire-Format), `klassenbildung.json`, `config.json`, `mapping.json`,
  `audit-log.json`, `gui-state.json`, `ergebnisse/<stand>/…`.
- **Krypto:** AES-256-GCM, Nonce je Speichervorgang frisch; PBKDF2-SHA256,
  Iterationszahl **unverschlüsselt im Header** (Start ≥ 600.000, per Feld
  erhöhbar), Salt je Datei. Kein Schlüsselmaterial auf Platte; „Passwort
  merken" nur via DPAPI und abschaltbar. **Kein Recovery-Pfad** (Entscheidung
  oben).
- **Atomar speichern:** serialisieren → ZIP in Temp **im selben Ordner** →
  verschlüsseln → `File.Replace`. Kein Klartext-Umweg über `%TEMP%`.
- **Mapping:** ID-Vergabe fortlaufend, **nie wiederverwendet** („eine
  gelöschte ID bleibt verbrannt"). Platzhalter-Schüler bekommen *keinen*
  Mapping-Eintrag.
- **Audit-Log:** append-only aus App-Sicht, pseudonymisiert; Löschen eines
  Standes entfernt Ergebnisdaten, **lässt die Log-Zeile stehen**.
- **Stände:** Obergrenze (Default 10) mit Verdrängung; Freigabe-Stand und
  Bestands-Stand sind geschützt.

**Verifikation:** Round-Trip mit allen Teilen; falsches Passwort → sauberer
Fehler statt Datenmüll (GCM authentifiziert); Manipulation eines Bytes wird
erkannt; Absturzsimulation mitten im Speichern lässt die Altdatei intakt;
Schema-Toleranz (unbekanntes Feld beim Laden ignoriert, bekanntes fehlendes
Feld defaultet); Import eines echten `tests/<schule>`-Ordners und
YAML-Export zurück → strukturgleich.

---

## Stufe D — GUI-Durchstich (Klassenbildung)

Erstes lauffähiges Ergebnis: Projekt öffnen → rechnen → Board sehen.

- `TimetableGui` als `net8.0-windows`, `UseWPF` (VB-WPF-Templates sind
  verfügbar — geprüft: `dotnet new wpf -lang VB`).
- **Shell** nach Konzept §2: ein Hauptfenster, schmale Seitenleiste
  (Start · Klassenbildung · Stundenplan · Stammdaten · Regeln · Läufe),
  Menüzeile, Statusleiste mit Speicherstatus (●) und Lauf-Kurzfortschritt.
- Projekt öffnen/speichern/anlegen, Import eines bestehenden
  `tests/<schule>`-Ordners.
- **Lauf-Monitor** (§6.13): Stufen-Fortschritt, Konvergenzkurve, wachsende
  Lösungsliste, Abbrechen. Das ist reine Anbindung — `SolveProgress` liefert
  `Phase`/`PhaseIndex`/`PhaseCount`/`ElapsedS`/`BudgetS`/`SolutionsFound` und
  seit dem Kanal auch `IncumbentObjective`/`BestObjectiveBound`, also die
  Kurve **live** statt erst nachgelagert. Aufruf in `Task.Run`, Rückmeldung
  über `Progress(Of SolveProgress)` auf den Dispatcher.
- **WebView2-Hosting** des Klassenbildungs-Viewers, read-only.

**Technische Weiche, die das Konzept offen ließ** (§7.6 nennt
„`NavigateToString` bzw. virtuelles Host-Mapping"): **beides scheidet aus.**
Die Stundentafel-Seite überschreitet die dokumentierte ~2-MB-Grenze
von `NavigateToString`; Host-Mapping wiederum bräuchte einen Ordner mit
Klartext-HTML, was §7.6 gerade verbietet. Stattdessen:
`AddWebResourceRequestedFilter` + `WebResourceRequested`, der die in-memory
erzeugte HTML unter einer synthetischen Origin (z.B. `https://viewer.local/`)
ausliefert. Kein Klartext auf Platte, keine Größengrenze, externe Navigation
blockierbar. User-Data-Folder explizit auf
`%LocalAppData%\Schulplanung\WebView2\`.

**Verifikation:** Projekt anlegen → Klassenbildung rechnen → Board zeigt
Varianten; Abbrechen während des Laufs liefert die bereits fertigen Varianten
(genau das Verhalten, das `KlassenbildungTopResult.Cancelled` zusichert);
Eine Seite über 2 MB wird unverändert ausgeliefert — der Größen-Regressionstest (synthetisch, weil die reale Größe lauf- und konfigurationsabhängig ist).

---

## Stufe E — Bridge und U5-Re-Solve

Der riskanteste Teil, weil er **die verifizierten Templates anfasst**.

- Beide Templates um einen **additiven, feature-erkannten** Kanal erweitern:
  `if (window.chrome && window.chrome.webview) { … }`. Der
  `try/catch`-localStorage-Pfad (`klassenbildung.html:453,464`) bleibt
  unverändert — **Doppelklick-Betrieb muss weiter funktionieren**, das ist
  eine dokumentierte Zusage (arc42 §8.10) und wird per Test abgesichert.
- **JS → Host:** `window.chrome.webview.postMessage` mit versioniertem
  Umschlag (`{v:1, typ:…, nutzlast:…}`). Nachrichten: Pins/Härtungen/Filter
  geändert, „neu rechnen", „Lösung als Arbeitsstand übernehmen".
  Das Nachrichtenschema ist in **keinem** der beiden Konzepte spezifiziert —
  es entsteht hier und gehört als Querschnittsabschnitt in arc42.
- **Host → JS:** Zustandsinjektion beim Laden plus die **Anzeige-Map**
  (ID → „Mia M."), die ausschließlich in den DOM gerendert wird — das
  eingebettete JSON und jeder Export bleiben pseudonym.
- Im GUI-Betrieb ersetzt das Fixierungen-Panel den YAML-Block
  (`klassenbildung.html:1498`) durch Schaltfläche + Zusammenfassung.
- **U5:** „Neu rechnen" nimmt Pins (F1/F2) und Härtungen (F3/F5) entgegen,
  schreibt sie in den Projektbestand und startet den `klassen`-Lauf im
  Hintergrund. Damit ist der einzige als offen markierte UI-Punkt geschlossen.

**Verifikation — hier ist eine echte Vorarbeit nötig:** `CLAUDE.md` schreibt
für Template-Änderungen einen Headless-Chromium-Smoke gegen
`/opt/pw-browsers/` vor — ein **Linux-Pfad, der auf dieser Windows-Maschine
nicht existiert**. Das Prüfrezept muss zuerst auf Edge/Chrome portiert werden
(`msedge --headless --dump-dom --virtual-time-budget=…`), sonst hat die
Template-Änderung keine tragende Absicherung. Zusätzlich: eine Datei per
Doppelklick öffnen und belegen, dass der localStorage-Pfad unberührt
funktioniert.

---

## Stufe F — Eingabemasken

Der Mengenanteil von V1. Alle nach dem Grundmuster aus §6: Liste links,
Detailformular rechts, Aktionen Neu · Duplizieren · Löschen · Prüfen;
**Referenzfelder immer Auswahllisten aus dem Bestand, nie Freitext** — damit
unbekannte Referenzen (der klassische Validierungsfehler, arc42 §8.1) gar
nicht entstehen können.

- Projekt-Assistent (§6.1, 5 Schritte) über die vorhandene `Scaffold`-Logik,
  inkl. anonymem Schüler-/Gruppen-Generator (§6.8).
- Stammdaten-Dialoge §6.2–6.9: Schuldaten, Klassenstufen/Klassen, Fächer,
  Räume, Lehrkräfte (mit Deputat-Plausibilität im Kopf),
  Qualifikationsmatrix, Schüler & Gruppen, feste Zuordnungen.
- Regeln §6.10: acht Masken plus **Rasterpicker als gemeinsames Control**
  (Tag × Stunde, gleiche Optik wie die Stundentafel), generierte Regeln
  read-only, validierter YAML-Expertenmodus.
- Klassenbildungs-Eingaben §6.11 **inklusive Zwischenablage-Import**,
  Solver-Einstellungen §6.12 (einfach/Experten gegen die `RunConfig`-Felder
  aus Stufe A).

  **Nachtrag (Nutzerentscheidung):** der Zwischenablage-Import war ursprünglich
  in Stufe G eingeplant. Das war falsch geschnitten: eine Einschulungsliste,
  die man abtippen muss, wird nicht benutzt – die Maske wäre fertig, aber
  praktisch unbenutzbar. Der Zwischenablage-Weg (`Name;Klasse` bzw.
  Einschulungszeilen mit Attributspalten, IDs vergibt die GUI) gehört deshalb
  hierher. In G bleibt nur der CSV-Dialog mit freier Spalten-Zuordnung
  (§9.1) – das ist der Komfortweg, nicht der Grundweg.
- Querschnitt §7: **Umbenennen kaskadiert** über alle Referenzen (Vorschau
  „12 Verweise werden angepasst"), **Löschen** zeigt Konsequenzen-Dialog —
  niemals stilles Verwaisen. „Speichern immer möglich, Rechnen nur bei grüner
  Prüfung." Kein Autosave.

**Verifikation:** je Maske ein Test, der über das ViewModel (nicht über XAML)
schreibt und danach die passende `Validate*`-API grün sieht;
Kaskaden-Umbenennung und Lösch-Konsequenzen als eigene Tests gegen ein
Beispielprojekt.

**Nachtrag zu F6 (Regeln der Klassenbildung).** F4 hatte Gruppen, Balance,
Wünsche und Fixierungen nur ANGEZEIGT – und F wurde trotzdem als
abgeschlossen gemeldet. Das war falsch: §6.11 führt die drei als
„Listen-Dialoge nach Grundmuster“ auf, und wer Gruppen pflegen wollte,
musste an der YAML-Datei vorbei – in einer Oberfläche, deren Zweck es ist,
genau das zu ersparen. Aufgefallen ist es im manuellen Test
(Nutzerhinweis 26.08.2026), nicht mir.

Der Nur-Lese-Reiter *Regeln* ist durch vier bearbeitbare ersetzt. Drei
Punkte, die dabei Entscheidungen verlangten:

- **Nur die Gruppe hat einen Namen.** Balance, Wunsch und Fixierung sind
  durch ihren Inhalt bestimmt; ihr Listentext ist abgeleitet und
  `SetzeName` bewusst wirkungslos. Neu, Duplizieren, Löschen und Prüfen
  funktionieren dadurch trotzdem einheitlich – genau dafür gibt es das
  Grundmuster.
- **Kein Freitext bei Balance.** Attribut und Wert stammen aus dem Vokabular
  der Einschulungsliste. Eine Balance auf ein Attribut, das kein Kind trägt,
  bliebe unbemerkt wirkungslos; die Maske nennt deshalb auch, wie viele
  Kinder der gewählte Wert betrifft.
- **Herkunft der Fixierungen ehrlich.** §6.11 verlangt sie „aus dem
  Audit-Log“. Das Protokoll führt Board-Übernahmen aber als SAMMELZEILE,
  nicht je Kind. Statt eine Herkunft je Zeile vorzugaukeln, steht über der
  Liste die jüngste einschlägige Protokollzeile.

Nebenbefund: der Negativtest zur Werteliste blieb grün, weil dieselbe
Zeile zweimal stand – einmal in `AufBalanceAttribut`, einmal in
`BalanceZeigen`. Die redundante ist entfernt; toter Code, der
tragend aussieht, ist eine Falle für den nächsten Leser.

**Nachtrag zu F5 (Projekt-Assistent).** Der Assistent nutzt die
Scaffold-Logik als Motor, wie geplant – aber sie schrieb bisher nur Dateien.
Deshalb ist `Scaffold.Run` in `Run` (Datei) und **`Scaffold.Baue`** (Bestand im
Speicher) geteilt; CLI und Assistent haben damit weiterhin GENAU EINE
Vorstellung davon, wie eine frische Schule aussieht. Belegt ist das durch
einen Vorher/Nachher-Vergleich der `new`-Ausgabe: byteweise identisch.

Zwei Dinge sind größer ausgefallen als der Konzepttext vermuten lässt:

- Die **Gruppen-Vorlage** „Religion ev/kath/Ethik“ teilt nicht Kinder auf ein
  Fach auf, sondern **spaltet das Fach selbst**. Ein Parallelverbund verlangt
  paarweise verschiedene Fächer – genau deshalb heißen die Fächer im
  GMS-Beispiel `Religion-ev`, `Religion-kath`, `Ethik` und nicht dreimal
  „Religion“. `GruppenVorlagen.Anwenden` erledigt darum vier Dinge auf einmal:
  Fach spalten, Qualifikationen mitnehmen, Gruppen mit Verbund anlegen,
  Kinder verteilen. Wer nur eines davon täte, hinterließe einen Bestand, den
  `ValidateStammdaten` zu Recht ablehnt.
- Nach der Aufspaltung tragen **Gruppen** den Bedarf, nicht mehr die Klassen:
  drei parallele Gruppen je Stufe statt zwei Klassen. Der Assistent bemisst
  die Lehrkräfte deshalb nach – sonst hätte der Nutzer ein Projekt, das
  der Solver als infeasible zurückgibt, ohne dass er wüsste, warum.

**Datei → Neues Projekt** legt seither kein leeres Projekt mehr an. Ein leeres
wäre formal richtig und praktisch nutzlos: vor dem ersten Rechnen lägen dann
acht Masken. Die Herkunft des Startbestands landet im Audit-Log – einer
generierten Schule sieht man später nicht mehr an, dass ihre Zahlen aus einem
Template stammen und nicht aus dem Sekretariat.

**Nachtrag zu F3 (Regeln).** Zwei Entscheidungen, die beim Umsetzen fielen:

- Der Seitenleisten-Eintrag *Regeln* zeigte seit F4 auf die
  Klassenbildungs-Eingaben. Er zeigt jetzt auf die Regelverwaltung; die
  Klassenbildungs-Eingaben bleiben über *Extras* erreichbar. Sie sind
  Stammdaten eines anderen Laufs, keine Regeln – beide um denselben
  Platz konkurrieren zu lassen war ein Schnittfehler.
- Der Grundsatz „Tests über das ViewModel, nicht über XAML" bekommt hier
  eine **begründete Ausnahme**. Die acht Masken werden zur Laufzeit aus
  `Regeltypen.vb` GEBAUT. Damit wandert eine Fehlerklasse ins Fenster, die
  das ViewModel gar nicht sehen kann: ein `StaticResource`-Schlüssel, den
  es nicht gibt, oder eine Feldart, für die der Erbauer kein
  Steuerelement kennt. Beides ist kein Compilerfehler. `RegelnFensterTests`
  baut deshalb auf einem eigenen STA-Thread das Fenster samt aller acht
  Masken auf und legt einmal eine Regel über die Steuerelemente an. Das
  Testprojekt bleibt für alles übrige headless – nur dieser eine Thread
  kennt WPF. (Live belegt: `schrift-mono` stand in der Vorlage, aber nicht
  in `Tokens.xaml`; kein anderer Test hätte das gefunden.)

---

## Stufe G — Stundenplan-Dashboard, Im-/Export, Startseite, Freigabe

- Zweites Dashboard mit Bridge (Lösung übernehmen, Stände-Auswahl,
  „neu rechnen" mit Kurzparametern).
- YAML-Ex-/Import in `tests/<schule>/`-Layout — der CLI-Kanal bleibt damit
  vollwertig erhalten.
- CSV-Import mit freier Spalten-Zuordnung (§9.1/9.2). Der
  Zwischenablage-Weg steht bereits seit Stufe F4. **Kein xlsx-Parser** —
  bewusst nicht in V1.
- Startseite als Schrittleiste (§8).
- **Freigabe:** aktive Bestätigung mit Substanz (Klassenbildungs-Konzept §10),
  Freigabe-Stand gegen Löschen/Verdrängen geschützt, Audit-Zeile.
- Klarnamen-Export nur hinter Warndialog **und** Audit-Eintrag — die einzige
  gekennzeichnete Ausnahme von der Pseudonymitäts-Grenze.


**Schnitt (analog zu F).** G1 zweites Dashboard und Lauf – G2 Brücke im
Stundentafel-Viewer – G3 Bereich *Läufe* mit Stände-Historie und Freigabe –
G4 YAML-Ex-/Import – G5 CSV-Import mit Spalten-Zuordnung und
Klarnamen-Export – G6 Startseite als vollständige Schrittleiste.

**Nachtrag zu G1/G2.** Drei Dinge, die beim Umsetzen auffielen:

- **Ein `ViewerAuslieferung`, zwei Dashboards.** Der Wechsel von
  Klassenbildung auf Stundenplan zeigte weiter das Board – gleiche URL,
  alter Inhalt, und nichts daran sichtbar falsch. Das `HauptViewModel` hält
  jetzt je Bereich eine Seite und liefert beim Wechsel die passende aus.
  Ein `Verwerten` wechselt dabei selbst in seinen Bereich – ein Ergebnis
  gehört dorthin, wo man es sieht.
- **Die Vorlage `stundentafel.html` hatte gar keine Brücke.** Sie ist jetzt
  nach dem Muster des Klassenbildungs-Boards nachgerüstet: additiv, an der
  Feature-Erkennung hängend, standardmäßig verborgen. Der
  Doppelklick-Betrieb ist eine dokumentierte Zusage (arc42 8.10); belegt ist
  sie durch einen Test, der ohne Host prüft, dass die Leiste unsichtbar
  bleibt – und durch den Diff der neu gerenderten Beispielseiten:
  111 Zeilen hinzu, null geändert.
- **Der Arbeitsstand braucht kein neues Dateiformat.** `ProjektStand.Lauf`
  ist ein freies `JsonObject` und wird bereits round-trip-sicher
  gespeichert; die Markierung liegt darin unter `arbeitsstand`.

**Nachtrag zu G3 (Läufe und Freigabe).** Die Freigabe ist die einzige Stelle
der Anwendung, deren Zweck ein RECHTLICHER Nachweis ist: Art. 22 DSGVO
verlangt den Beleg, dass eine echte menschliche Prüfung stattgefunden hat.
Daraus folgen drei Entwurfsentscheidungen, die sonst willkürlich wirken
würden:

- **Fester Satz UND eigene Begründung** (Nutzerfrage 26.08.2026: wäre eine
  selbstformulierte Notiz nicht besser?). Beides, nicht eines. Der feste Satz
  trägt die juristischen Pflichtbestandteile – Bezug auf die geprüften
  Abweichungen und „in eigener Verantwortung“ –, ein Freitext allein
  könnte beides weglassen und wäre dann ein SCHWÄCHERER Nachweis. Die Notiz
  wiederum ist der Beleg der tatsächlichen Befassung: wer die Abweichungen
  in eigenen Worten abwägt, hat sie gelesen – das lässt sich nicht
  mechanisch erzeugen, ein Häkchen schon. Pflicht ist sie NUR bei
  verbleibenden Abweichungen; ohne etwas abzuwägen wäre der Zwang zur Notiz
  selbst wieder Theater. Die Feldfrage lautet deshalb nicht „Bemerkung“
  (das erzeugt „ok“), sondern „Warum ist der Stand trotz dieser Abweichungen
  vertretbar?“ Geprüft wird die Pflicht im ViewModel, nicht nur im Fenster:
  das Fenster kann man umgehen.
- **Der Dialog zeigt die Abweichungen im Wortlaut, nicht als Zahl.** Ein
  bloßer OK-Klick ohne angezeigte Abweichungen wäre als menschliche
  Beteiligung angreifbar (Klassenbildungs-Konzept 10.1). Der
  Bestätigungssatz nennt deshalb ihre Anzahl, und der Knopf bleibt aus,
  solange Haken oder Name fehlen.
- **Gespeichert wird der Bericht, WIE ER ANGEZEIGT WURDE** – nicht ein
  Verweis, aus dem man ihn später neu berechnen könnte. Freigegeben wurde,
  was auf dem Bildschirm stand.
- **Lücken und Randstunden zählen NICHT als Abweichung.** Sie sind
  Qualitätsmerkmale, keine Regelverletzungen; sie mitzuzählen blähte den
  Bestätigungssatz mit Dingen auf, die niemand versprochen hat – und
  entwertete damit den Nachweis.

Der Schutz gegen Löschen und Verdrängen lag bereits im Kern
(`Projekt.StandHinzufuegen`/`StandLoeschen`); G3 setzt nur die Marke. Dabei
fiel eine Lücke auf: `StandHinzufuegen` liefert die verdrängten Ids zurück,
damit die Oberfläche sie melden kann – ausgewertet hatte sie bis dahin
niemand. Jetzt erscheinen sie als Hinweis und im Protokoll.

**Nachtrag zu G6 (Startseite).** Die Leiste zeigte bisher drei Schritte, zwei
davon mit dem Text „noch nicht umgesetzt“. Jetzt sind es die fünf des
Konzepts, jeder mit echtem Zustand und einer Zeile Substanz – die Skizze in
8 ist wörtlich gemeint: eine Leiste, die nur Überschriften zeigt, ist ein
Inhaltsverzeichnis, keine Standortbestimmung.

Drei Entscheidungen:

- **Das Anlegen eines Projekts ist kein Schritt.** „Neue Projekte starten bei
  [1] mit dem Assistenten-Ergebnis“ – die Leiste setzt ein offenes Projekt
  voraus. Die Einstiegsknöpfe stehen darüber.
- **Keine Handregel ist Bereit, nicht Warnung.** Regeln sind optional; eine
  Warnung, die den Normalfall trifft, erzieht dazu, Warnungen zu übersehen.
- **„Entscheiden“ führt in das Dashboard, das etwas zeigt.** Immer zum
  Stundenplan zu springen wäre bei einer Schule, die nur die Klassenbildung
  nutzt, ein Klick ins Leere.

**Dabei fiel ein Fehler auf**, den erst das zweite Dashboard sichtbar machte:
`SchrittRechnen` hing am Auslieferungs-Slot („wird gerade etwas
angezeigt?“). Ein Wechsel auf das leere Board liess damit einen gerechneten
Stundenplan als ungerechnet erscheinen. Der Schritt liest jetzt die
STÄNDE – ein Lauf bleibt ein Lauf, unabhängig davon, wohin man schaut.

Der Aufbautest der Leiste ist zugleich die erste Aufbauprüfung, die
`MainWindow` überhaupt hat.

**Freigabe aus der Sicht** (Nutzerwunsch 26.08.2026): die Klassenbildung ist
aus dem Board freizugeben, der Stundenplan aus der Stundentafel. Beide Wege
führen durch DENSELBEN Dialog mit Abweichungen und Begründungspflicht – eine
zweite, bequemere Freigabe wäre genau die Abkürzung, die den Nachweis
entwertet. Die Seiten entscheiden nichts, sie melden nur.

Damit das geht, musste der Host lernen, WELCHER Stand in welchem Dashboard
steht (`_standIds`). „Der letzte Lauf“ wäre die falsche Antwort, sobald
jemand über den Bereich *Läufe* einen älteren Stand geöffnet hat.

**Dabei fiel ein echter Fehler auf.** Der Negativtest zur Standwahl blieb
grün, obwohl er hätte rot werden müssen. Ursache: die Stand-Id wird
sekundengenau aus dem Zeitstempel gebildet, zwei Läufe in derselben Sekunde
erhielten also dieselbe – und beide Codewege trafen denselben Stand. Die Id
ist aber zugleich der ORDNERNAME im Container (`ergebnisse/<id>/`), zwei
gleiche Ids überschreiben sich beim Speichern. `Projekt.StandHinzufuegen`
macht die Id jetzt eindeutig; zwei Tests in `TimetableProjekt.Tests` halten
das fest, einer davon über Speichern und Laden.

---

## Stufe H — Hauptfenster nach den zwei Rechnungen

Auslöser (05.09.2026): „Das Menü ist nicht sauber strukturiert und der
Ablauf zwischen den Tabs Start und Klassen/Stunden ist nicht schlüssig.“
Der Befund dahinter: die Anwendung beantwortet zwei unabhängige Fragen
(Klassenbildung, Stundenplan) mit je eigenen Eingaben, eigenem Ergebnis
und eigener Freigabe – das Hauptfenster bildete das nirgends ab.

- Die Seitenleiste mischte drei Dinge: echte Bereiche (Start, Läufe),
  reine Ergebnis-Sichten (Klassen, Stunden) und Dialog-Starter
  (Stammdaten, Regeln), die nach dem Schließen auf Start zurücksprangen.
- Die Eingaben lagen asymmetrisch: Stammdaten und Regeln prominent in der
  Seitenleiste, die Klassenbildungs-Eingaben unter *Extras* neben
  „Browserdaten bereinigen“ – und der Solver-Reiter für BEIDE Rechnungen
  im Klassenbildungsfenster.
- Klassen/Stunden ohne Ergebnis: ein weißes bzw. auf dem vorigen Inhalt
  stehengebliebenes WebView2, ohne Hinweis und ohne Weg zu Eingaben oder
  Rechnen.
- Die 5er-Schrittleiste zog beide Rechnungen in EINE Kette; „Rechnen“ galt
  als erledigt, sobald irgendein Lauf da war, und führte in die Historie
  statt zum Rechnen; „Freigabe“ ebenfalls.
- Ein echter Fehler: die Seitenleiste hing nur per `Command` am Modell,
  nicht per `IsChecked`. Nach F5 aus der Startseite, nach „Ansehen“ aus den
  Läufen oder nach dem Schließen eines Dialogs zeigte die Markierung einen
  anderen Bereich als der Inhalt.

**Was jetzt gilt.** Seitenleiste `Start · Klassen · Stunden · Läufe`. Jeder
Rechnungs-Bereich trägt einen **Bereichskopf** über dem Dashboard: die
Eingabe-Zeilen mit Stand (Stammdaten/Regeln bzw. Kinder & Regeln), der
Rechnen-Knopf, rechts der Stand-Wechsler. Ohne Ergebnis zeigt der Bereich
statt des WebView2 eine **Leerseite** mit dem Ablauf der Rechnung. Die
Startseite besteht aus **zwei Karten**, je Rechnung eine, mit den Zeilen
Eingaben · Rechnen · Entscheiden · Freigabe; jede Zeile führt eine Aktion
aus (Maske öffnen, rechnen, Dashboard, Freigabe), nicht in einen Bereich.
Das Menü hat ein **Bearbeiten** (Stammdaten…, Regeln…, Klassenbildung:
Kinder & Regeln…, Solver-Einstellungen…); *Extras* behält nur, was keiner
Rechnung gehört.

**Entscheidungen beim Umsetzen:**

- **Pflegemasken hinter `IDialoge`.** Damit eine Karte „Stammdaten…“
  anbieten kann, ohne dass das ViewModel ein Fenster kennt, hat `IDialoge`
  vier Methoden `…Pflegen()` bekommen; `WpfDialoge` öffnet die Masken,
  `TestDialoge` zählt. Nebeneffekt: nach dem Schließen einer Maske werden
  die Karten neu bewertet – vorher zeigte die Startseite nach dem Anlegen
  einer Klasse noch „keine Klasse angelegt“, weil `Geaendert` längst True
  war und nichts mehr meldete.
- **Kein Freigeben-Knopf im Bereichskopf.** Den hat die Aktionsleiste im
  Viewer bereits (G2), und ohne Stand gibt es nichts freizugeben. Die
  Freigabe-Zeile der Karte gibt den angezeigten Stand frei oder führt in
  die Läufe, wo man einen wählt.
- **Bei der Klassenbildung IST die Freigabe die Entscheidung.** Es gibt
  dort keinen „Arbeitsstand“ wie beim Stundenplan – die Zeile
  „Entscheiden“ ist Bereit, sobald ein Stand da ist, und Erledigt mit der
  Freigabe.
- **`SolverEinstellungenViewModel` unverändert**, nur das Fenster ist
  neu; der Reiter im Klassenbildungsfenster ist weg. `Feldbeschriftung`
  liegt jetzt in `Application.xaml` statt als Kopie in zwei Masken.
- **Der Konverter `BereichGewaehlt`** ist der erste eigene im Projekt.
  `ConvertBack` liefert bei `False` `Binding.DoNothing` – sonst schriebe
  der ABGEWÄHLTE Schalter seinen Bereich zurück und überschriebe den neuen.

**Verifikation.** `TimetableGui.Tests` (278 Tests, ~1 min): `StartkartenTests`
ersetzt `SchrittleisteTests` und prüft je Karte Zustand und Text, die
Trennung der Rechnungen (ein Stundenplan-Lauf macht die Klassenbildung
nicht „gerechnet“), die Aktionen der Zeilen gegen die Dialog-Attrappe, den
Stand-Wechsler und `Schliessen`. Der Fensteraufbau-Test baut `MainWindow`
auf, wechselt den Bereich AUS DEM MODELL und belegt, dass die Seitenleiste
folgt und die Leerseite statt des Dashboards steht. Die Designkanon-Wächter
laufen unverändert mit (neue Schlüssel, keine Farbliterale, keine neuen
Tokens). Live per UI Automation belegt: Menü, Seitenleiste, Karten,
Bereichskopf mit Rechnen-Knopf und Stand-Wechsler, Leerseite.

---

## Risiken

| Risiko | Umgang |
|---|---|
| **Template-Änderung ohne tragende Prüfung** — das dokumentierte Smoke-Rezept zeigt auf einen Linux-Pfad | Portierung auf Edge/Chrome headless ist **Voraussetzung** für Stufe E, nicht Nacharbeit |
| **Pipeline-Drift CLI ↔ GUI** | Genau ein Service in `TimetableWorkflow`, die CLI wird dessen erster Konsument; Budget-/Auswahl-Logik nicht duplizieren |
| **Verschieben bricht Ressourcen erst zur Laufzeit** (hartkodierte Präfixe, `GetExecutingAssembly`) | `render` gegen beide Beispielschulen direkt nach Stufe A, byte-identisch |
| **VB + WPF ist weniger ausgetreten als C# + WPF** (XAML-Designer, MVVM-Tooling) | Templates existieren; MVVM von Hand ohne Framework; Logik in testbaren ViewModels statt Code-Behind |
| **`SolveLehrereinsatzTop` kann leer sein** (neu seit dem Abbruchkanal) | In Stufe B abfangen; `Run.vb:321` greift heute unbesehen auf Index 0 zu |
| **Passwort vergessen = Totalverlust** | Bewusst akzeptiert; Handbuch-Hinweis, Warnung im Assistenten, „Sicherungskopie erstellen" prominent |
|  **Viewer-Seite kann 2 MB überschreiten** (am GMS-Beispiel 2,49 MB bei 28 Lösungen gemessen; derselbe Datensatz ergab bei 20 Lösungen 1,77 MB — der Export enthält ALLE Lösungen, skaliert also mit `max_solutions`) | `WebResourceRequested` statt `NavigateToString`; Ladezeit als Regressionstest festhalten |

---

## Gesamtverifikation

Nach jeder Stufe gilt unverändert die Regel aus `CLAUDE.md`: Änderungen an
`TimetableCore/` verlangen **beide** Suiten vollständig grün. Die neuen
Projekte bekommen eigene Testprojekte in derselben Disziplin
(`TimetableYaml.Tests`, `TimetableProjekt.Tests`, `TimetableWorkflow.Tests`).

Der durchgehende End-to-End-Beleg, der über alle Stufen trägt:

```bash
cd /c/Develop/Stundenplan/timetable-dotnet && dotnet test TimetableCore.Tests
```

```bash
cd /c/Develop/Stundenplan/timetable-dotnet && dotnet run --project SchoolTestRunner -- run --all
```

Beide müssen nach jeder Stufe unverändert durchlaufen — die CLI ist der
Wächter dagegen, dass der Umbau die bestehende Funktion beschädigt.

**Doku:** arc42 §5 (neue Projekte im Bausteinbaum), §7 (Verteilungssicht),
ein neuer Querschnittsabschnitt für das Bridge-Nachrichtenschema, und die
Auflösung der offenen Punkte 1 (Passwort) und 4 (Projekt-Feinschnitt) aus
dem Datenhaltungskonzept.

---

## Nicht Teil dieses Plans

V2 (Chat-Regeln via Ollama, Bestandsplan-Einfrieren, „Lehrkraft fällt länger
aus", Konfliktkern-Dialog, Art.-15-Bericht) und V3 (Vertretungsmodus mit
Datums-Ebene, Kursstufen-Sichten, GMS-Sektionen, kombinierte Schule) —
gestaffelt laut Konzept §11.
