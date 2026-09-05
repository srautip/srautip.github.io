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
│ ⌂      │ Stundenplan  [✓ Stammdaten] [▶ Regeln] [Rechnen F6]  Stand ▾│
│ Start  ├─────────────────────────────────────────────────────────────┤
│ ──     │                                                             │
│ 🧩     │                                                             │
│ Klassen│                                                             │
│ ──     │                WebView2-Dashboard                           │
│ 📅     │                (randlos, volle Fläche)                      │
│ Stunden│                                                             │
│ ──     │   - oder, solange kein Ergebnis da ist, die Leerseite       │
│ ▶      │     mit dem Ablauf dieser Rechnung -                        │
│ Läufe  │                                                             │
│        │   Stammdaten/Regeln öffnen als modale WPF-Dialoge           │
│        │   ÜBER dieser Fläche - das Dashboard bleibt der Anker.      │
├────────┴─────────────────────────────────────────────────────────────┤
│ GS Musterstadt 2026/27 · gespeichert 14:32 · Lauf: Stundenplan 3/10  │
│ Lösungen (02:41) ▓▓▓▓░░ [Abbrechen]                     (Statusleiste)│
└──────────────────────────────────────────────────────────────────────┘
```

- **Seitenleiste** (schmal, Icon + Kurzlabel): Start · Klassenbildung ·
  Stundenplan · Läufe - vier Vollflächen-Bereiche. Stammdaten und Regeln
  sind KEINE Bereiche (Stufe H): sie sind Eingaben des Stundenplans und
  öffnen als Dialog aus dessen Bereichskopf, aus der Startkarte und aus
  dem Menü *Bearbeiten*. Die Markierung der Seitenleiste folgt dem
  Bereich auch dann, wenn das Modell ihn wechselt (F5 von der Startseite,
  „Ansehen“ aus den Läufen).
- **Bereichskopf** über jedem Dashboard: die Eingabe-Zeilen dieser
  Rechnung mit Stand (Zeichen und Farbe), der Rechnen-Knopf, rechts der
  Stand-Wechsler. Freigeben steht nicht hier - das hat die Aktionsleiste
  im Viewer (Abschnitt 4/5).
- **Leerseite**: ein Rechnungs-Bereich ohne Ergebnis zeigt statt eines
  leeren WebView2 seinen Ablauf - dieselben Zeilen wie die Startkarte.
- **Menüzeile**: klassisch und schlank (Abschnitt 3) - kein Ribbon,
  keine zweite Toolbar; Aktionen leben kontextnah in den Bereichen.
- **Statusleiste**: Projektname + Schuljahr, Speicherstatus
  (ungespeicherte Änderungen als ●), laufender Solver-Lauf mit
  Kurzfortschritt und Abbrechen (Klick öffnet den Lauf-Monitor).
- **Modale Dialoge** für Stammdaten/Regeln halten den mentalen Anker
  ("ich arbeite an diesem Plan") und ersparen Fensterverwaltung -
  bewusste Dialogentscheidung 1.

## 3. Menüstruktur

Stand Stufe H (umgesetzt); in eckigen Klammern, was das ursprüngliche
Konzept zusätzlich vorsah und noch aussteht.

| Menü | Einträge |
|---|---|
| **Datei** | Neues Projekt… (Assistent, 6.1) · Bestehende Schule übernehmen… (Migrations-Einstieg, 9) · Öffnen… · Speichern (Strg+S) · Speichern unter… · Projekt schließen · Beenden [Zuletzt verwendet ▸ · Sicherungskopie · Importieren/Exportieren ▸ (YAML-Ordner, Berichte)] |
| **Bearbeiten** | Stammdaten… (6.2-6.9) · Regeln… (6.10) · Klassenbildung: Kinder & Regeln… (6.11) · Solver-Einstellungen… (6.12) - die Eingaben beider Rechnungen an einem Ort [Rückgängig/Wiederholen · Suchen… · Projekt-Passwort ändern…] |
| **Planung** | Klassenbildung rechnen (F5) · Stundenplan rechnen (F6) · Lauf abbrechen · Läufe und Stände [Lehrkraft fällt länger aus… (10.1, V2) · Vertretung planen… (10.2, V3)] |
| **Extras** | Klarnamen exportieren… (Warndialog + Audit-Eintrag) · Browserdaten bereinigen (WebView2-Profil, Datenhaltung 7.6) [Regeln aus Freitext… (Chat, Ausbaustufe 2) · Einstellungen…] |
| **Hilfe** | Über [Handbuch · Feldreferenz] |

Die Freigabe ist bewusst KEIN Menüpunkt mehr: sie geschieht aus der
Sicht (Aktionsleiste im Viewer) oder im Bereich *Läufe*, immer durch
denselben Dialog.

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
| 3 Schüler & Gruppen (optional, anonym) | Anzahl Schüler je Klassenstufe + Auswahl typischer Gruppen-Vorlagen (z.B. Religion ev/kath/Ethik) mit Aufteilung - erzeugt Platzhalter-Schüler samt Gruppen (6.8); überspringbar: ohne diesen Schritt rechnet der Plan rein klassenbasiert |
| 4 Schutz | Projekt-Passwort (+ Wiederholung, Stärke-Hinweis), Speicherort |
| 5 Zusammenfassung | erzeugter Startbestand in Zahlen (Klassen, Fächer aus dem Kontingent-Template, automatisch ergänzte Fachlehrkräfte, ggf. Platzhalter-Schüler/Gruppen) |

Ergebnis: ein sofort rechenbares Projekt (wie der CLI-Scaffold: plausible
Stammdaten, leeres Regelwerk) - der Nutzer sieht in Minuten ein erstes
Ergebnis, exakt der Schnellstart-Pfad des Benutzerhandbuchs. Der
Stundenplan braucht dabei grundsätzlich KEINE Einzelschüler
(`schueler`/`gruppen` sind im Modell optional, Klassen tragen nur eine
informative `schuelerzahl`) - Schritt 3 ist nur nötig, wenn
klassenübergreifende Gruppen (Religion, Förderung, Niveaukurse)
mitgeplant werden sollen, und dafür genügen anonyme Platzhalter.

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
  Einfügen aus Zwischenablage (Name;Klasse - IDs vergibt die GUI)
  oder CSV-Datei mit Spalten-Zuordnung (Abschnitt 9.1).
- **"Anonyme Schüler erzeugen…"** (Aktion, auch als Assistent-Schritt
  3 in 6.1): Eingabe der Anzahl je Klassenstufe bzw. je Klasse plus
  Aufteilung auf **typische Gruppen-Vorlagen** - mitgelieferte
  Vorlagen z.B. "Religion ev/kath/Ethik" (Aufteilung in Prozent oder
  absolut, je Klassenstufe), "Förderung (n Kinder, max 1 je Klasse)",
  "Niveaukurse G/E/A je Fach" (GMS); eigene Vorlagen speicherbar.
  Erzeugt deterministisch Platzhalter-Schüler (S001…) mit
  Heimatklasse und den Gruppenzuordnungen - exakt das, was die
  Beispiel-Fixtures bisher per Wegwerf-Skript erzeugt haben
  (GMS-Beispiel, Klassenbildungs-Fixture). Platzhalter erhalten
  KEINEN mapping.json-Eintrag (kein Personenbezug), sind als solche
  markiert und können später durch eine echte Liste ersetzt werden -
  dabei bleiben Klassen-/Gruppenstruktur erhalten, die Mitgliedschaften
  werden aber neu zugeordnet (ehrliche Grenze: eine automatische
  1:1-Übernahme von Platzhalter- auf echte Kinder gibt es nicht).
- **Gruppen**: `name`, `typ`, Mitglieder-Picker (Mehrfachauswahl mit
  Klarnamen-Anzeige), und für solver-wirksame Fachgruppen `fach_name`,
  `klassenstufe`, `parallelverbund`. Die harte Verbund-Regel wird live
  geprüft (alle Gruppen eines Verbunds: Fach paarweise verschieden,
  gleiche Stufe, gleiches `wochenstunden_soll`/`block_length`) - mit
  Klartext-Fehlern statt späterem Infeasible.
- GMS-Komfort (Ausbaustufe, 11): "Sektionen bilden" teilt eine zu große
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

Eigener Dialog unter *Bearbeiten* (Stufe H) - die Einstellungen gelten
für BEIDE Rechnungen und saßen davor als Reiter im Klassenbildungsfenster,
wo sie niemand vermutete, der einen Stundenplan rechnen wollte.
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

Die Startseite übersetzt den Benutzerhandbuch-Workflow in **zwei
Karten**, je Rechnung eine, mit Status je Zeile (analog zum
Fortschritts-Trichter U3 des Klassenbildungs-Boards). Ursprünglich war
das EINE Leiste über beide Rechnungen; ihr Schritt „Rechnen“ galt als
erledigt, sobald irgendein Lauf da war - Stufe H hat sie getrennt.

```
┌ Klassenbildung ─────────────────────┐ ┌ Stundenplan ─────────────────────────┐
│ [1] Kinder & Regeln ✓ 84 Kinder, 4  │ │ [1] Stammdaten ✓ 8 Klassen, 12 Lehrk.│
│     Klassen, 3 Regeln - Prüfung grün│ │ [2] Regeln     ! 4 Handregeln, 1 Hinw│
│ [2] Rechnen   ✓ zuletzt 30.08. 14:32│ │ [3] Rechnen    ▶ noch nicht - F6     │
│ [3] Entscheiden ▶ am Board, 2 Fix.  │ │ [4] Entscheiden ○ erst rechnen       │
│ [4] Freigabe  ○ noch nicht          │ │ [5] Freigabe   ○ noch nicht          │
└─────────────────────────────────────┘ └──────────────────────────────────────┘
```

Jede Zeile führt eine AKTION aus: die Eingabe-Zeilen öffnen ihre Maske,
„Rechnen“ rechnet, „Entscheiden“ öffnet das Dashboard, „Freigabe“ gibt
den angezeigten Stand frei oder führt in die Läufe. Einen eigenen
„Öffnen“-Knopf hat die Karte bewusst nicht: er stünde direkt unter
*Öffnen…* (Datei) und täte etwas anderes; in den Bereich führen die
Zeile „Entscheiden“ und die Seitenleiste. Neue Projekte
starten bei [1] mit dem Assistenten-Ergebnis. Dieselben Zeilen stehen
als Leerseite im Bereich der Rechnung, solange dort kein Ergebnis liegt,
und die Eingabe-Zeilen im Bereichskopf über dem Dashboard (Abschnitt 2).
Die Klassenbildung hat dieselbe Leiste in ihrer Board-Randleiste (Basis
wählen → bulk fixieren → Gruppen/Einzelfälle → neu rechnen → Freigabe).

## 9. Unterjährige Einführung (Migration aus Bestandssystemen)

Die Software wird realistischerweise mitten im Schuljahr eingeführt -
Klasseneinteilungen und Stundenpläne existieren dann bereits in anderer
Software oder in Office-Dateien. Die GUI behandelt das als eigenes
Einstiegsszenario ("Bestehende Schule übernehmen" als Alternative zum
leeren Assistenten-Projekt), in drei unabhängig nutzbaren Bausteinen:

### 9.1 Klasseneinteilung übernehmen (CSV/Zwischenablage)

Import-Dialog mit **Spalten-Zuordnung**: eine CSV-Datei oder ein
eingefügter Tabellenbereich (aus Excel kopiert - der verlässlichste
Office-Weg ohne neue Abhängigkeit) wird als Vorschau angezeigt, der
Nutzer ordnet Spalten zu: Name → mapping.json, Klasse → Heimatklasse,
weitere Spalten wahlweise → Klassenbildungs-Attribut (z.B.
"Geschlecht") oder → Gruppenmitgliedschaft (z.B. Spalte "Religion" mit
Werten ev/kath/ethik erzeugt die drei Gruppen und verteilt die Kinder).
IDs vergibt die GUI; nicht zugeordnete Spalten werden verworfen (mit
Hinweis - Datenminimierung als Default). Ein direkter xlsx-Parser ist
bewusst NICHT Teil von V1 (er bräuchte eine neue Abhängigkeit entgegen
dem BCL-only-Grundsatz der Datenhaltung); CSV-Export bzw.
Kopieren/Einfügen aus Excel decken die Fälle ab.

### 9.2 Bestehenden Lehrereinsatz übernehmen

Die gelebte Lehrer-Klasse-Fach-Zuteilung wird als `feste_zuordnungen`
erfasst (Maske 6.9 bzw. derselbe CSV-/Einfüge-Weg mit Spalten
Lehrkraft/Klasse/Fach) - Stufe 1 reproduziert dann die Realität, statt
neu zu verteilen, und der Mechanismus ist zugleich die vorhandene
Antwort auf Lehrerkontinuität (gleiche Namen, `docs/
phase2-14-lehrerkontinuitaet.md`). Beim nächsten Schuljahr können die
Zuordnungen schrittweise gelöst werden.

### 9.3 Bestehenden Stundenplan übernehmen

Der Ist-Plan wird über ein Erfassungsraster (Rasterpicker je Klasse:
Fach+Lehrkraft je Slot) oder per CSV (Klasse, Tag, Stunde, Fach,
Lehrkraft) eingelesen und zweifach genutzt:

- **Als Referenz-Stand "Bestand"** in der Läufe-Historie (6.13): jeder
  neue Vorschlag wird in der Stundentafel per vorhandener
  "Vergleichen mit"-Diff-Ansicht GEGEN den Ist-Zustand gelesen -
  unterjährig die entscheidende Frage ("was ändert sich für wen?").
  Beim Import läuft `Verifier.VerifySchedule` über den Bestandsplan:
  Abweichungen von den erfassten Stammdaten/Regeln (der Altplan
  verletzt z.B. eine Verfügbarkeit) werden gelistet statt
  stillschweigend übernommen - oft der erste Datenqualitäts-Gewinn
  der Einführung.
- **Als Startpunkt zum Weiterplanen**: die Aktion **"Bestandsplan
  einfrieren"** erzeugt je erfasster Stunde eine `required_slot`-
  must-Regel (der Typ existiert; Herkunfts-Badge "Bestand") - der
  Solver reproduziert den Plan exakt. Anschließend wird gezielt
  gelockert: Bereichsauswahl im Rasterpicker ("Nachmittage der
  Stufe 3", "alle Stunden von Frau X") entfernt die betreffenden
  Bestands-Regeln, und nur dieser Ausschnitt wird neu optimiert,
  während der Rest fixiert bleibt. Das ist der unterjährige
  Normalfall (Lehrkraft fällt aus, Raum entfällt) - ohne dass der
  restliche Plan sich bewegt.

Analog auf der Klassenbildungs-Seite: eine importierte Einteilung
(9.1 mit Spalte Klasse) kann als `fixierungen:`-Vollbestand ins Board
übernommen werden - Umverteilung einzelner Kinder läuft dann über den
normalen Board-Workflow (Pins lösen, verschieben, neu rechnen).

## 10. Ausfall und Vertretung ("Lehrer krank" / "Schüler krank")

Zwei Anwendungsfälle des laufenden Betriebs, bewusst getrennt nach
Zeithorizont - sie sind strukturell verschiedene Probleme:

### 10.1 Längerfristiger Ausfall (Wochen: Langzeiterkrankung, Elternzeit)

Hier wird tatsächlich ein neuer **Dauerplan** gebraucht - und der
Mechanismus dafür existiert bereits (9.3): aktuellen Plan als Stand
sichern und einfrieren, nur die Stunden der ausfallenden Lehrkraft
lockern, neu rechnen; die Diff-Ansicht zeigt exakt, was sich für wen
ändert. Ergänzend kann der Lehrereinsatz (Stufe 1) mit einer um die
Lehrkraft reduzierten Liste neu gerechnet werden, wenn die Vertretung
dauerhaft übernommen wird - `springer_reserve_stunden` ist die schon
vorhandene Kapazitätsvorsorge dafür. Kein neuer Baustein nötig; die
GUI verpackt den Ablauf als geführte Aktion "Lehrkraft fällt länger
aus…" (Planung-Menü), die diese Schritte in der richtigen Reihenfolge
anbietet. Nutzbar ab V2 (setzt Bestandsplan-Einfrieren voraus).

### 10.2 Kurzfristige Vertretung (Tage: "Frau X ist Mo/Di krank")

Ein strukturell ANDERES Problem als Planung: gesucht ist kein neuer
Plan, sondern die **minimal störende Überbrückung** konkreter Tage -
und dafür fehlt dem gesamten System heute die Datums-Dimension
(geplant wird ein Wochenraster, kein Kalender). Der Vertretungs-Modus
ist deshalb eine echte neue Ausbaustufe (V3):

- **Datum als flüchtige Ebene ÜBER dem Wochenplan**, kein Umbau des
  Kernrasters: ein Vertretungsplan referenziert Datum → Wochentag →
  betroffene Slots des gültigen Dauerplans.
- **Ablauf**: Planung → "Vertretung planen…" → Datum(e) und
  ausfallende Lehrkraft(en)/Räume wählen → je betroffener Stunde eine
  Vorschlagsliste mit den üblichen Optionen: qualifizierte Lehrkraft
  mit Freistunde (Springer zuerst), fachfremde Aufsicht, Raumtausch,
  Klassen zusammenlegen, Verlegung, ersatzlos entfallen (Randstunden
  zuerst) - der Nutzer entscheidet je Stunde, Vorschläge sind
  Angebote.
- **Eigenes kleines Teilmodell** nach dem etablierten Projektmuster
  (eigenständiges Modul, Kern unverändert, unabhängige Nachprüfung):
  Zielfunktion "minimale Störung" (wenige Betroffene, keine
  Mehrfachbelastung, Springer vor Mehrarbeit) statt Planqualität;
  angesichts der Größe (eine Handvoll Stunden) genügt ggf. sogar eine
  regelbasierte Vorschlagsliste ohne Solver - die Entscheidung fällt
  in der Umsetzung.
- **Ergebnis**: tagesbezogener Vertretungsplan als eigenes, flüchtiges
  Artefakt ("Vertretungsplan Mo 12.01.") - druckbar/exportierbar,
  gespeichert als Stand unter `ergebnisse/` (Muster Datenhaltung 5.2)
  mit Audit-Log-Zeile; der Dauerplan bleibt unberührt.

### 10.3 "Schüler krank": ehrliche Einordnung

Im klassenbasierten Modell ist ein erkranktes Kind **planungsneutral**:
Stunden- und Vertretungsplan hängen an Klassen/Gruppen, nicht an
Einzelschülern. Relevant ist der Fall nur an zwei Rändern: in der
**Klassenbildung** (Kind fällt vor der Einschulung langfristig aus →
Fixierung lösen, ggf. Nachrücker, neu rechnen - das deckt der
bestehende Board-Workflow bereits ab, kein neuer Baustein) und
perspektivisch in der **Kursstufe** (dort zählen Wahlprofile, aber
ebenfalls keine Einzel-Absenzen). Eine Absenzen-/Fehlzeiten-Verwaltung
wäre ein neues Fachgebiet außerhalb der Planungs-Domäne dieses
Projekts und wird bewusst NICHT eingeplant.

## 11. Ausbaustufen

| Stufe | Inhalt |
|---|---|
| **V1** | Projekt-Assistent inkl. anonymer Schüler-/Gruppen-Generator (6.1/6.8), Stammdaten-Dialoge (6.2-6.9), Regel-Masken + Expertenmodus (6.10), Klassenbildungs-Eingaben (6.11), Solver-Einstellungen einfach/Experten, Lauf-Monitor, beide Dashboards mit Bridge **inklusive U5-Re-Solve** aus dem Board, Stände-Historie, YAML-Ex-/Import, CSV-/Zwischenablage-Import für Schülerlisten und feste Zuordnungen (9.1/9.2), Startseite |
| **V2** | Chat-Regeln (Freitext → `LlmExtraction` → Vorschlagsliste mit Prüfen/Übernehmen je Regel - nie Direktübernahme), Bestandsplan-Übernahme mit Einfrieren/Lockern (9.3), geführte Aktion "Lehrkraft fällt länger aus" (10.1), Klarnamen-Druckexporte, Konfliktkern-Dialog (setzt Klassenbildungs-Plan K6 voraus), Bericht-Generator (Art.-15-Bericht je Kind aus der gefilterten Sicht) |
| **V3** | Vertretungs-Modus für kurzfristige Ausfälle (10.2), Kursstufen-/Schienenmodell-Sichten (Kurse, Schienen, Wahlprofile), GMS-Assistent für Sektionen/Parallelverbünde, kombinierte Schule |

Abhängigkeiten und Datenfluss (Projektdatei, Bridge, WebView2-Hygiene,
Audit-Log) sind im Datenhaltungskonzept festgelegt; dieses Dokument
ergänzt die Bedienschicht. Das Klassenbildungs-UI-Konzept
(`docs/klassenbildung-ui-konzept.md`) bleibt die Detailreferenz für das
Board - dessen offene Stufe U5 wird durch Abschnitt 4 dieses Konzepts
konkretisiert und in der GUI umgesetzt.
