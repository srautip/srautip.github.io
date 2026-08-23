# TimetableGui: Implementierungsplan für die Phase-3-GUI (WPF + WebView2)

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
Die GMS-Stundentafel ist **2,49 MB** — über der dokumentierten ~2-MB-Grenze
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
GMS-Stundentafel (2,49 MB) lädt fehlerfrei — der Größen-Regressionstest.

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
- Klassenbildungs-Eingaben §6.11, Solver-Einstellungen §6.12 (einfach/Experten
  gegen die `RunConfig`-Felder aus Stufe A).
- Querschnitt §7: **Umbenennen kaskadiert** über alle Referenzen (Vorschau
  „12 Verweise werden angepasst"), **Löschen** zeigt Konsequenzen-Dialog —
  niemals stilles Verwaisen. „Speichern immer möglich, Rechnen nur bei grüner
  Prüfung." Kein Autosave.

**Verifikation:** je Maske ein Test, der über das ViewModel (nicht über XAML)
schreibt und danach die passende `Validate*`-API grün sieht;
Kaskaden-Umbenennung und Lösch-Konsequenzen als eigene Tests gegen ein
Beispielprojekt.

---

## Stufe G — Stundenplan-Dashboard, Im-/Export, Startseite, Freigabe

- Zweites Dashboard mit Bridge (Lösung übernehmen, Stände-Auswahl,
  „neu rechnen" mit Kurzparametern).
- YAML-Ex-/Import in `tests/<schule>/`-Layout — der CLI-Kanal bleibt damit
  vollwertig erhalten.
- CSV-/Zwischenablage-Import mit Spalten-Zuordnung (§9.1/9.2). **Kein
  xlsx-Parser** — bewusst nicht in V1.
- Startseite als Schrittleiste (§8).
- **Freigabe:** aktive Bestätigung mit Substanz (Klassenbildungs-Konzept §10),
  Freigabe-Stand gegen Löschen/Verdrängen geschützt, Audit-Zeile.
- Klarnamen-Export nur hinter Warndialog **und** Audit-Eintrag — die einzige
  gekennzeichnete Ausnahme von der Pseudonymitäts-Grenze.

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
| **GMS-Viewer 2,49 MB** | `WebResourceRequested` statt `NavigateToString`; Ladezeit als Regressionstest festhalten |

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
