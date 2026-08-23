# GUI-/UI-Konzept für die Phase-3-GUI (WPF + WebView2)

Dieses Konzept beschreibt Fensteraufbau, Menüstruktur, Dashboards und
Eingabemasken der geplanten Desktop-GUI. Es baut auf dem
Datenhaltungskonzept (`docs/gui-datenhaltung-konzept.md`: verschlüsselte
Ein-Datei-Projektablage, Klarnamen-Mapping, Audit-Log) auf und
orientiert sich für die notwendigen Felder und Abläufe an den beiden
realen Beispielen (`tests/bw-grundschule-beispiel`,
`tests/bw-gms-beispiel`) sowie am Endnutzer-Workflow des
SchoolTestRunner-Benutzerhandbuchs. Es ist ein Konzept-, kein
Umsetzungsdokument.

Vorab geklärte Nutzerentscheidungen (Dialograunde zu diesem Konzept):

1. **Navigation**: EIN Hauptfenster mit schmaler Seitenleiste; die
   restliche Fläche gehört dem jeweiligen Vollflächen-Dashboard.
   Stammdaten-Pflege in modalen WPF-Dialogen darüber.
2. **Regel-Editor**: Formular-Masken für die real genutzten
   Constraint-Typen + validierter YAML-Expertenmodus für alles Übrige.
3. **Chat-/LLM-Eingabe** (lokal via Ollama, bestehende `LlmExtraction`):
   eingeplant, aber Ausbaustufe 2 - Version 1 läuft ohne Ollama.
4. **Solver-Läufe**: im Hintergrund mit Lauf-Monitor (Fortschritt,
   Konvergenz, Abbrechen); die GUI bleibt bedienbar.

## 1. Leitprinzipien

- **Dashboard-first.** Die Kernarbeit der Zielgruppe (Schulleitung /
  Planer) ist Entscheiden am Board bzw. an der Stundentafel - nicht
  Formulare ausfüllen. Deshalb bekommt WebView2 die maximale Fläche;
  WPF liefert Rahmen (Navigation, Menü, Status) und die Formulare.
- **Viewer wiederverwenden, nicht nachbauen.** Die beiden
  Self-contained-Viewer (`Templates/klassenbildung.html` mit Board,
  Ampel-Chips, Drag & Drop, Live-Bewertung;
  `Templates/stundentafel.html` mit Lösungsübersicht, Gewichte-Regler,
  Pareto-Filter, Lösungs-Diff, Lehrerplan, Tausch-Anzeige ⇄) sind
  fertig, per Chromium-Test verifiziert und werden 1:1 gehostet
  (arc42 §8.10). Die GUI ergänzt nur eine Bridge (Abschnitt 4/5).
- **Klarnamen nur in der Anzeigeschicht.** Überall, wo Schüler-IDs
  auftauchen, blendet die GUI "Mia Muster (S001)" ein - gespeichert,
  gerechnet und exportiert wird pseudonym (Datenhaltungskonzept 6).
- **Validieren mit den bestehenden APIs, nicht mit UI-Sonderlogik.**
  `StammdatenValidation.ValidateStammdaten`,
  `Validation.ValidateEntities` und
  `Klassenbildung.ValidateKlassenbildung` sind die eine Wahrheit.
  Grundsatz: **Speichern ist immer möglich** (ein Projekt darf
  unfertig/inkonsistent sein), **Rechnen nur bei grüner Prüfung** -
  dieselbe Fail-Fast-Disziplin wie CLI und Kern (arc42 §8.1).
- **Eine Statussprache.** Grün/Gelb/Rot mit Symbolen ✓/!/✗ wie in den
  Viewern; Prio-Wortschatz der Klassenbildung (kritisch/wichtig/wenn
  möglich) wird in der GUI unverändert weiterverwendet.
- **Jede planungsrelevante Aktion hinterlässt eine Audit-Log-Zeile**
  (Läufe, Variantenwahl, Fixierungen, Freigaben, Klarnamen-Exporte -
  Datenhaltungskonzept 7.3). Das passiert beiläufig, ohne eigene UI.

## 2. Fensterlayout

```
┌──────────────────────────────────────────────────────────────────────┐
│ Datei  Bearbeiten  Planung  Extras  Hilfe                 (Menüzeile)│
├────────┬─────────────────────────────────────────────────────────────┤
│ ⌂      │                                                             │
│ Start  │                                                             │
│ ──     │                                                             │
│ 🧩     │                                                             │
│ Klassen│                                                             │
│ ──     │                WebView2-Dashboard                           │
│ 📅     │                (randlos, volle Fläche)                      │
│ Stunden│                                                             │
│ ──     │                                                             │
│ 👥     │   Stammdaten/Regeln öffnen als modale WPF-Dialoge           │
│ Stamm- │   ÜBER dieser Fläche - das Dashboard bleibt der Anker.      │
│ daten  │                                                             │
│ ──     │                                                             │
│ 📏     │                                                             │
│ Regeln │                                                             │
│ ──     │                                                             │
│ ▶      │                                                             │
│ Läufe  │                                                             │
├────────┴─────────────────────────────────────────────────────────────┤
│ GS Musterstadt 2026/27 · gespeichert 14:32 · Lauf: Stundenplan 3/10  │
│ Lösungen (02:41) ▓▓▓▓░░ [Abbrechen]                     (Statusleiste)│
└──────────────────────────────────────────────────────────────────────┘
```

- **Seitenleiste** (schmal, Icon + Kurzlabel): Start · Klassenbildung ·
  Stundenplan · Stammdaten · Regeln · Läufe. "Stammdaten" und "Regeln"
  öffnen Übersichtslisten (WPF), aus denen die Detail-Dialoge starten;
  die übrigen vier sind Vollflächen-Bereiche.
- **Menüzeile**: klassisch und schlank (Abschnitt 3) - kein Ribbon,
  keine zweite Toolbar; Aktionen leben kontextnah in den Bereichen.
- **Statusleiste**: Projektname + Schuljahr, Speicherstatus
  (ungespeicherte Änderungen als ●), laufender Solver-Lauf mit
  Kurzfortschritt und Abbrechen (Klick öffnet den Lauf-Monitor).
- **Modale Dialoge** für Stammdaten/Regeln halten den mentalen Anker
  ("ich arbeite an diesem Plan") und ersparen Fensterverwaltung -
  bewusste Dialogentscheidung 1.

## 3. Menüstruktur (Vorschlag)

| Menü | Einträge |
|---|---|
| **Datei** | Neues Projekt… (Assistent, 6.1) · Öffnen… · Zuletzt verwendet ▸ · Speichern (Strg+S) · Sicherungskopie erstellen · Importieren ▸ (YAML-Ordner nach `tests/<schule>/`-Layout) · Exportieren ▸ (YAML-Ordner / Viewer-HTML / Berichte md/pdf / **Klassenlisten mit Klarnamen…** [Warndialog + Audit-Eintrag]) · Projekt schließen · Beenden |
| **Bearbeiten** | Rückgängig/Wiederholen (formularbezogen) · Suchen… (Strg+F: global über Kinder [Klarname+ID], Lehrkräfte, Fächer, Regeln) · Projekt-Passwort ändern… |
| **Planung** | Klassenbildung rechnen · Lehrereinsatz + Stundenplan rechnen · Lauf abbrechen · Lauf-Monitor anzeigen · Stand als Freigabe markieren… (aktive Bestätigung mit Substanz, Konzept klassenbildung §10) · Solver-Einstellungen… (6.12) |
| **Extras** | Regeln aus Freitext… (Chat, **Ausbaustufe 2**; Menüpunkt prüft `LlmExtraction.IsOllamaAvailable` und erklärt bei Fehlen die lokale Ollama-Einrichtung) · Browserdaten bereinigen (WebView2-Profil, Datenhaltung 7.6) · Einstellungen… |
| **Hilfe** | Handbuch · Feldreferenz (tests/README-Inhalte) · Über |

Nicht als Menüpunkte, sondern kontextnah: Fixieren/Härten (im Board),
Lösung übernehmen (in der Stundentafel), Anlegen/Löschen (in den
Listen), Prüfen (in jedem Dialog).

## 4. Dashboard Klassenbildung (WebView2)

Der bestehende Klassenbildungs-Viewer (U1-U4) IST das Dashboard -
Varianten-Kacheln, Prio-Panel, Board mit Stapeln/Badges, Drag & Drop
mit Live-Bewertung, Fortschritts-Trichter. Die GUI ergänzt drei Dinge:

1. **Bridge statt localStorage.** Pins, Härtungen und Filterzustand
   wandern über `window.chrome.webview.postMessage` in die GUI und
   damit in `gui-state.json` der Projektdatei (+ Audit-Log je
   Fixierung). Der Viewer behält seinen try/catch-localStorage-Pfad
   für den Doppelklick-Betrieb; im WebView2-Betrieb injiziert die GUI
   den Zustand beim Laden (Datenhaltung 7.6).
2. **Der Loop wird geschlossen (= UI-Konzept U5).** Der bisherige Weg
   "YAML-Block kopieren → CLI-Lauf" entfällt: eine
   **"Neu rechnen"**-Aktion nimmt die aktuellen Pins (F1/F2) und
   Härtungen (F3/F5) über die Bridge entgegen, schreibt sie als
   Fixierungen/Modus-Änderungen in den Projektbestand und startet den
   `klassen`-Lauf im Hintergrund. Genau wie im UI-Konzept vorgesehen
   ersetzt das NUR den Export-Teil - Board, Bewertung und Trichter
   bleiben unverändert. Das Fixierungen-Panel zeigt im GUI-Betrieb
   statt des YAML-Blocks die Schaltfläche samt Zusammenfassung
   ("31 Fixierungen, 2 Härtungen → neu rechnen").
3. **Schmale WPF-Randleiste** (einklappbar, rechts): aktiver Stand/
   Variante, Ampel-Zähler, Schaltflächen "Neu rechnen", "Stand
   sichern", "Freigeben…"; darunter die Stände-Historie
   (Datenhaltung 5.2) zum Umschalten.

Klarnamen: die GUI reicht dem Viewer eine Anzeige-Map (ID → "Mia M.")
mit, die NUR in den DOM gerendert wird - das eingebettete JSON und
jeder Export bleiben pseudonym.

## 5. Dashboard Stundenplan (WebView2)

Der Stundentafel-Viewer wird unverändert gehostet: Lösungsübersicht
(ersetzt das frühere Dropdown; Klick wählt), Qualitätsvektor-Spalten,
Gewichte-Regler (JS-Neusortierung), Pareto-Filter + Pareto-Hinweis,
"Vergleichen mit"-Diff, Klassen- und Lehrerpläne,
Zuteilungs-Spalte und ⇄-Tausch-Anzeige des Mehr-Zuteilungs-Modus.

GUI-Ergänzungen über die Bridge:

- **"Diese Lösung als Arbeitsstand übernehmen"** je Zeile der
  Lösungsübersicht - markiert die Lösung im Projekt (Audit-Eintrag)
  und macht sie zum Default für Berichte/Exporte.
- **Stände-Auswahl** (Randleiste wie in 4): frühere Läufe laden.
- **"Neu rechnen"** mit Kurz-Parametern (Zeitbudget, Anzahl Lösungen)
  direkt aus dem Dashboard; Details in den Solver-Einstellungen (6.12).

## 6. WPF-Eingabemasken

Grundmuster aller Listen-Dialoge: Liste links (sortier-/filterbar),
Detailformular rechts, Aktionen **Neu · Duplizieren · Löschen ·
Prüfen**; Pflichtfelder markiert, Zahlenfelder strikt numerisch
(period/hours/block-Felder sind im Schema Zahlen). Referenzfelder sind
IMMER Auswahllisten aus dem Bestand, nie Freitext - damit können
unbekannte Referenzen (der klassische Validierungsfehler, arc42 §8.1)
gar nicht erst entstehen.

### 6.1 Projekt-Assistent (Datei → Neues Projekt)

Nutzt die vorhandene Scaffold-Logik (`new`-Kommando) als Motor:

| Schritt | Felder |
|---|---|
| 1 Schule | Schulname, Schulart (Grundschule/Gemeinschaftsschule), Bundesland (BW; weitere folgen), Schuljahr |
| 2 Struktur | Klassenstufen (1..4 GS / 1..6 GMS), Züge (Default 2), Anzahl Klassenlehrkräfte |
| 3 Schutz | Projekt-Passwort (+ Wiederholung, Stärke-Hinweis), Speicherort |
| 4 Zusammenfassung | erzeugter Startbestand in Zahlen (Klassen, Fächer aus dem Kontingent-Template, automatisch ergänzte Fachlehrkräfte) |

Ergebnis: ein sofort rechenbares Projekt (wie der CLI-Scaffold: plausible
Stammdaten, leeres Regelwerk) - der Nutzer sieht in Minuten ein erstes
Ergebnis, exakt der Schnellstart-Pfad des Benutzerhandbuchs.

### 6.2 Schuldaten (allgemein)

`schul_name`, `schulart`, `bundesland` (informativ), `tage` (Mo-Fr
an/abwählbar), `periods_per_day`. **Warnung mit Konsequenzliste**, wenn
Tage/Stunden verkleinert werden, während Slot-Regeln oder Fenster
existieren, die dann ins Leere zeigen.

### 6.3 Klassenstufen & Klassen

Master-Detail: Stufen (`nummer`, `bezeichnung`) und je Stufe die
Klassen (`name` [eindeutig], `schuelerzahl` [informativ],
`erlaubt_klassenlehrer_tandem`). Operation "Zug ergänzen" legt die
nächste Parallelklasse (c, d, …) über alle gewählten Stufen an.

### 6.4 Fächer

`name`, `block_length` (Doppelstunde, optional), `unbeliebt`
(Verteilungs-Fairness); Untertabelle **Wochenstunden je Stufe**
(`klassenstufe`, `wochenstunden_soll`, `max_pro_tag` optional). Eine
Stufe ohne Zeile heißt "wird dort nicht unterrichtet" - der Dialog
zeigt das explizit als Badge statt es stumm zu lassen. Fußzeile:
Summen-Kontrolle Wochenstunden je Stufe (Soll gegen `periods_per_day` ×
Tage).

### 6.5 Räume

`name`, `typ` (Freitext-Kategorie). Bewusst minimal - Räume sind nur
für `room_requirement`-Regeln nötig (6.10).

### 6.6 Lehrkräfte

Alle neun Felder: `name` (eindeutig - ist Schlüssel!, siehe 7),
`deputat_sollstunden`, `anrechnungsstunden`,
`springer_reserve_stunden`, `verfuegbare_tage` (Tages-Toggles; leer =
Vollzeit), `bevorzugte_klassenstufen`, `klassenlehrer_faehig`
(Default an), `max_klassen`, `max_faecher`. Eingebettet:
**Qualifikationen** als Fach-Checkliste mit `fachfremd`-Häkchen je
Fach (pflegt `fach_lehrer_zuordnungen`).

Live-Plausibilität im Kopf des Dialogs: Summe der Soll-Deputate
(abzüglich Anrechnungen/Reserve) gegen den Gesamtstundenbedarf aus 6.4
- die "Kanarienvogel"-Erfahrung des GMS-Beispiels (überdimensionierte
Pools erzeugen verteilten Deputat-Leerlauf) wird so VOR dem Lauf
sichtbar, nicht erst im Ergebnis.

### 6.7 Qualifikationsmatrix (Zweitsicht)

Grid Fach × Lehrkraft mit drei Zuständen (qualifiziert / fachfremd /
nein) - dieselben Daten wie 6.6, aber für den Überblick "hat jedes Fach
genug Lehrkräfte?"; Spaltenfuß zeigt Bedarf vs. verfügbare Deputate je
Fach und färbt Engpässe rot (die StammdatenValidation-Lücke "Fach ohne
qualifizierte Lehrkraft" wird hier präventiv sichtbar).

### 6.8 Schüler & Gruppen (Scheduling-Seite)

- **Schülerliste**: ID (automatisch vergeben, nicht editierbar),
  Klarname (→ mapping.json), Heimatklasse. Import per
  Einfügen aus Zwischenablage (Name;Klasse - IDs vergibt die GUI).
- **Gruppen**: `name`, `typ`, Mitglieder-Picker (Mehrfachauswahl mit
  Klarnamen-Anzeige), und für solver-wirksame Fachgruppen `fach_name`,
  `klassenstufe`, `parallelverbund`. Die harte Verbund-Regel wird live
  geprüft (alle Gruppen eines Verbunds: Fach paarweise verschieden,
  gleiche Stufe, gleiches `wochenstunden_soll`/`block_length`) - mit
  Klartext-Fehlern statt späterem Infeasible.
- GMS-Komfort (Ausbaustufe, 9): "Sektionen bilden" teilt eine zu große
  Fachgruppe automatisch in nummerierte Sektionen nach dem Muster des
  GMS-Beispiels.

### 6.9 Feste Zuordnungen

`lehrer_name` × `klasse_name` (Klasse ODER aktive Gruppe -
gemeinsamer Namensraum) × `fach_name`; die Auswahllisten filtern auf
qualifizierte Kombinationen. Typischer Anwendungsfall Klassenlehrer-
Kontinuität ("Frau X behält die 2a in Deutsch").

### 6.10 Regeln (Constraints)

**Listenansicht** mit Typ-/Prio-/Betroffenen-Filter und zwei Ebenen:

- **Handregeln** (editierbar) - Masken für genau die Typen, die die
  Beispiele real nutzen (Häufigkeit GS/GMS in Klammern):

  | Maske | Felder | Bemerkung |
  |---|---|---|
  | Gesperrter Slot (`forbidden_slot`, 82/48) | Geltung (Klasse/Lehrkraft/Raum) + Betroffene, Slots per **Rasterpicker**, must/should, Grund | Mehrfach-Slot-Auswahl erzeugt eine Regel je Slot (wie im Beispiel), Anzeige gruppiert sie |
  | Fach-Zeitfenster / Rhythmisierung (`subject_period_window`, 16/224) | Klasse(n), Fach, Tage + von/bis-Stunde per Rasterpicker, must/should, Grund | Mehrfachauswahl von Klassen erzeugt je Klasse eine Regel; Hinweis "Tag nicht gewählt = ganz außerhalb" |
  | Belegungsfenster (`occupied_window`, 8/24) | Geltung Klasse/Lehrkraft, Tage + von/bis, should/must, Grund | ersetzt die frühere `occupied_slot`-Batterie |
  | Lehrkraft-Verfügbarkeit (`teacher_availability`, 3/0) | Lehrkraft, verfügbare Tage und/oder gesperrte Einzelstunden (Rasterpicker), must/should, Grund | |
  | Pflicht-Slot (`required_slot`, 1/0) | Klasse(n), Fach, Tag+Stunde, must/should, Grund | z.B. Chor-Gesamtprobe |
  | Raumbedarf (`room_requirement`, 0/15) | Fach, erlaubte Räume (Mehrfachauswahl aus 6.5), must/should, Grund | **kein Klassen-Feld** - die Regel ist rein fachbezogen (Befund der Konzept-Recherche: zwei Doku-Beispiele zeigen fälschlich `class`) |
  | Einzelbelegung (`occupied_slot`) | Klasse/Lehrkraft, Tag+Stunde, should/must, Grund | für punktuelle Fälle; Fenster bevorzugen |
  | Ad-hoc-Block (`consecutive_required`) | Klasse, Fach, Blocklänge, must/should, Grund | normalerweise über `Fach.block_length` (6.4) - Maske für Ausnahmen |

- **Generierte Regeln** (read-only, grau, Herkunfts-Badge "aus
  Lehrereinsatz"): `teacher_subject_assignment`, `weekly_hours`,
  `no_overlap`, `parallel_group` und die aus `block_length` erzeugten
  `consecutive_required` - `BuildAssignmentConstraints` erzeugt sie je
  Lauf; Handpflege ist dort ausdrücklich verboten (tests/README
  "Architektur-Hintergrund") und die GUI erzwingt das strukturell.

**Rasterpicker** (zentrales Shared Control): Tag-×-Stunde-Gitter aus
`tage`/`periods_per_day`, Klick/Ziehen wählt Slots oder Fenster;
dieselbe Optik wie die Stundentafel, damit "wo" immer gleich aussieht.

**Grund-Feld (`reason`)**: Freitext mit Hinweis "erscheint in Prüf- und
Verletzungsmeldungen; beim Export auf neutralen Wortlaut achten"
(Export-Vorschau, Datenhaltung 6.3).

**Expertenmodus**: validierter YAML-Editor über den vollen
`constraints.yaml`-Inhalt (Syntaxfehler + `ValidateEntities`-Fehler
inline) - für seltene Typen und Power-User; Masken und Editor arbeiten
auf demselben Bestand.

### 6.11 Klassenbildungs-Eingaben

- **Klassenrahmen**: Anzahl, min/max Größe, Stufe ODER explizite
  Labels (Vorschau "1a, 1b, …"). Live-Check Anzahl×Größe gegen
  Schülerzahl.
- **Einschulungs-Schülerliste**: eigene Liste (getrennt von 6.8 - die
  Klassenbildung läuft VOR der Klassenzuteilung): ID (automatisch),
  Klarname, Attribut-Spalten (frei definierbares Vokabular; die
  Spaltenverwaltung empfiehlt wertneutrale Tags und verlinkt den
  Grundsatz "die Diagnose selbst muss nie ins System"); Einfügen aus
  Zwischenablage.
- **Gruppen** (`buendelung`/`verteilung` + `max_pro_klasse`, Modus,
  Prio 1-3, Kürzel), **Balance** (Attribut+Wert aus vorhandenem
  Vokabular, Toleranz, Modus, Prio), **Wünsche** (Paar-Picker
  zusammen/getrennt, Modus, Prio) - jeweils Listen-Dialoge nach
  Grundmuster mit denselben Defaults wie das YAML (soft, Prio 2 bzw. 1).
- **Fixierungen**: Liste vorhanden, aber primär entstehen sie am
  Board (F1/F2 per Pin/Drag & Drop) - der Dialog dient der Durchsicht
  und dem gezielten Lösen, inklusive Herkunft aus dem Audit-Log.

### 6.12 Solver-Einstellungen

Zweistufig, damit der Standardfall einfach bleibt:

- **Einfach**: Zeitbudget gesamt, Anzahl Lösungen (`max_solutions`),
  Anzahl Klassenbildungs-Varianten, Parallelität (`num_workers`),
  Seed (mit Determinismus-Hinweis bei workers > 1).
- **Experten** (Ausklappbereich, Werte = config.yaml-Felder mit deren
  Defaults): per-solve-/Stufen-Limits, Stagnations-Timeout,
  Lex-Schalter (`lexicographic`, `lex_tolerance`, drei
  Stufen-Opt-ins), `quality_weights` inkl. der strukturellen
  `include_*`-Schalter, Mehr-Zuteilungs-Block (`max_assignments`,
  Toleranz, Diversität), Klassenbildungs-Block (ε, Mindestdistanz,
  Prio-Gewichte). Jedes Feld mit dem Kurz-Hilfetext aus tests/README.

### 6.13 Lauf-Monitor (Bereich "Läufe")

- **Aktiver Lauf**: Stufen-Fortschritt (Validierung → Lehrereinsatz →
  Verifikation → Stundenplan-Iterationen), Konvergenzkurve Zeit vs.
  Zielwert (der `convergence`-Export existiert bereits je Lösung),
  Lösungsliste wächst live, Abbrechen (`StopSearch` ist dokumentiert
  cross-thread-sicher - derselbe Mechanismus wie der
  Stagnations-Cutoff). Während des Laufs sind die betroffenen
  Eingabebereiche schreibgeschützt (Banner "Lauf aktiv").
- **Historie**: alle gesicherten Stände (Datenhaltung 5.2) mit Label,
  Parametern, Kennzahlen (beste Qualität, Kann-Verstöße, Zielwert);
  Aktionen: ansehen (öffnet Dashboard mit diesem Stand), vergleichen,
  Label ändern, löschen (Freigabe-Stand geschützt), als Freigabe
  markieren.

## 7. Operationen und Querschnitts-Verhalten

- **Namen sind Schlüssel.** Lehrkraft-, Fach-, Klassen- und Raumnamen
  referenzieren einander über den Namen (Wire-Format). Deshalb:
  *Umbenennen* kaskadiert automatisch über Qualifikationen, feste
  Zuordnungen, Gruppen und Regeln (mit Vorschau "12 Verweise werden
  angepasst"); *Löschen* zeigt einen Konsequenzen-Dialog mit allen
  betroffenen Objekten und bietet "mitlöschen" oder "abbrechen" -
  niemals stilles Verwaisen von Referenzen.
- **Prüfen** (je Dialog und global auf der Startseite): führt die
  bestehenden Validate-APIs aus und listet Fehler klickbar (Sprung zum
  Objekt). Rechnen-Aktionen sind bei Fehlern deaktiviert und nennen
  den Grund.
- **Ungespeicherte Änderungen**: ●-Indikator in Statusleiste und
  Titel; Schließen fragt nach. Autosave bewusst NICHT (atomares,
  bewusstes Speichern der verschlüsselten Datei - Datenhaltung 5.1);
  stattdessen Erinnerung nach konfigurierbarer Zeit.
- **Duplizieren** überall dort, wo Objekte einander ähneln
  (Lehrkräfte, Regeln, Gruppen) - der schnellste Erfassungsweg in der
  Praxis.
- **Tastatur**: Strg+S Speichern, Strg+F Suche, F5 Rechnen (mit
  Bestätigung), Entf mit Konsequenzen-Dialog; Dialoge vollständig
  tastaturbedienbar (Zugänglichkeitslinie des UI-Konzepts
  Klassenbildung gilt GUI-weit).

## 8. Geführter Ablauf (Startseite)

Die Startseite übersetzt den Benutzerhandbuch-Workflow in eine
Schrittleiste mit Status je Schritt (analog zum Fortschritts-Trichter
U3 des Klassenbildungs-Boards):

```
[1] Stammdaten ✓ 8 Klassen, 12 Lehrkräfte, Prüfung grün
[2] Regeln     ! 4 Handregeln, 1 Hinweis (Fach ohne Fenster)
[3] Rechnen    ▶ zuletzt 22.08. 14:32 - 10 Lösungen, beste 198.6
[4] Entscheiden → Dashboards (2 Fixierungen offen)
[5] Freigabe & Export  ○ noch nicht freigegeben
```

Jeder Schritt ist klickbar (führt in den Bereich), neue Projekte
starten bei [1] mit dem Assistenten-Ergebnis. Die Klassenbildung hat
dieselbe Leiste in ihrer Board-Randleiste (Basis wählen → bulk
fixieren → Gruppen/Einzelfälle → neu rechnen → Freigabe).

## 9. Ausbaustufen

| Stufe | Inhalt |
|---|---|
| **V1** | Projekt-Assistent, Stammdaten-Dialoge (6.2-6.9), Regel-Masken + Expertenmodus (6.10), Klassenbildungs-Eingaben (6.11), Solver-Einstellungen einfach/Experten, Lauf-Monitor, beide Dashboards mit Bridge **inklusive U5-Re-Solve** aus dem Board, Stände-Historie, YAML-Ex-/Import, Startseite |
| **V2** | Chat-Regeln (Freitext → `LlmExtraction` → Vorschlagsliste mit Prüfen/Übernehmen je Regel - nie Direktübernahme), Klarnamen-Druckexporte, Konfliktkern-Dialog (setzt Klassenbildungs-Plan K6 voraus), Bericht-Generator (Art.-15-Bericht je Kind aus der gefilterten Sicht) |
| **V3** | Kursstufen-/Schienenmodell-Sichten (Kurse, Schienen, Wahlprofile), GMS-Assistent für Sektionen/Parallelverbünde, kombinierte Schule |

Abhängigkeiten und Datenfluss (Projektdatei, Bridge, WebView2-Hygiene,
Audit-Log) sind im Datenhaltungskonzept festgelegt; dieses Dokument
ergänzt die Bedienschicht. Das Klassenbildungs-UI-Konzept
(`docs/klassenbildung-ui-konzept.md`) bleibt die Detailreferenz für das
Board - dessen offene Stufe U5 wird durch Abschnitt 4 dieses Konzepts
konkretisiert und in der GUI umgesetzt.
