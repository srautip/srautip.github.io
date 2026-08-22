# Datenhaltungskonzept für die Phase-3-GUI (WPF + WebView2)

Dieses Konzept beantwortet, wie die geplante Desktop-GUI Stammdaten und
Constraints verwaltet und Ergebnisse sichert - **lokal, ohne Backend/
Server**, DSGVO-konform und möglichst einfach wartbar. Es vergleicht
filebasierte und datenbankbasierte Ansätze und empfiehlt eine
verschlüsselte Ein-Datei-Projektablage. Es ist ein Konzept-, kein
Umsetzungsdokument: es ändert keinen Code. Die betroffenen
arc42-Abschnitte (§2, §3.2, §5.1, §7, §8.10, §11) sind auf
WPF + WebView2 und dieses Konzept nachgezogen.

Vorab geklärte Nutzerentscheidungen (Dialograunde zu diesem Konzept):

1. **Klarnamen + Pseudonym-Mapping**: Die GUI verwaltet Schüler-Klarnamen
   lokal; Solver, Exporte und Viewer sehen weiterhin nur Pseudonym-IDs.
   Eine separat gespeicherte, löschbare Zuordnungstabelle verbindet beides.
2. **Passwortschutz der Datendatei**: Verschlüsselung at rest mit einem
   vom Nutzer gewählten Passwort.
3. **Ein Rechner, eine Person**: kein Locking, kein Sync, keine
   Mehrbenutzer-Anforderung; Weitergabe nur bewusst per Export.
4. **Eigenes Projektformat + YAML-Export**: Die GUI hält eine eigene
   Projektdatei und ex-/importiert die bestehenden YAML/JSON-Formate
   verlustfrei - SchoolTestRunner, Viewer und Beispiele bleiben
   unverändert nutzbar.

## 1. Ziel und Rahmen

### 1.1 GUI-Technologie: WPF + WebView2

Die arc42-Doku vermerkt bisher "GUI-Technologie WinForms geplant"
(arc42 §2). Neue Vorgabe: **WPF** als Anwendungsrahmen (Formulare,
Stammdaten-/Constraint-Editoren, Databinding auf das typisierte
`Stammdaten.vb`-Modell - genau der in arc42 §8.7 vorgesehene Weg) plus
**WebView2** für die interaktiven Anteile **Klassenzuordnung** und
**Stundenplan-Interaktion**: die beiden bestehenden, vollständig
getesteten Self-contained-HTML-Viewer (`Templates/klassenbildung.html`
mit Board, Ampel-Chips, Drag & Drop und Live-Bewertung;
`Templates/stundentafel.html` mit Lösungsübersicht, Gewichte-Regler,
Pareto-Filter) werden gehostet statt in XAML nachgebaut. Das erhält die
komplette U1-U4-Investition inklusive der per Chromium-Test
verifizierten JS-Formel-Duplikate (arc42 §8.10) und macht die GUI zum
dritten Konsumenten derselben Artefakte neben Doppelklick-Datei und
Artifact.

Konsequenz für die Datenhaltung: sie muss (a) die Viewer-HTML/JSON aus
dem eigenen Bestand erzeugen können und (b) die WebView2-Laufzeitdaten
(Abschnitt 7.6) mitdenken.

### 1.2 Rahmenbedingungen

- **Lokal, kein Server**: kein Backend-Prozess, keine Cloud, kein
  Netzwerkdienst. Auch die LLM-Anbindung bleibt lokal (Ollama,
  `http://127.0.0.1:11434` - bestehende Randbedingung, arc42 §2).
- **Einfach wartbar**: ein Alleinentwickler (VB.NET); jede zusätzliche
  Abhängigkeit und jedes Migrationsregime ist laufender Wartungsaufwand.
- **Kontinuität**: Es gibt einen dokumentierten Alt-Beschluss
  "Persistenz: JSON-Dateien (konsistent mit dem bestehenden
  `entities`/`constraints`-Muster), keine eingebettete Datenbank"
  (`docs/phase2-15-lehrereinsatzplanung.md`, Nutzerentscheidung 1).
  Dieses Konzept prüft den Beschluss neu gegen die GUI-Anforderungen
  (Abschnitt 4) und bestätigt ihn - mit einer Erweiterung
  (Verschlüsselung + Container statt loser Dateien).

## 2. Datenbestand und Schutzbedarf

Was die GUI halten muss, mit DSGVO-Einstufung:

| Datenkategorie | Inhalt (heutige Quelle) | Personenbezug / Schutzbedarf |
|---|---|---|
| Schul-Stammdaten | Schulname, Bundesland, Schulart, Zeitraster, Klassenstufen, Fächer, Räume (`stammdaten.yaml` bzw. `Stammdaten.vb`) | kein Personenbezug |
| **Lehrkräfte** | Klarname (zugleich Schlüssel), Deputat, Anrechnungsstunden, `verfuegbare_tage` (Teilzeit!), Präferenzen, `klassenlehrer_faehig`, Qualifikationen inkl. `fachfremd`, Fach-Attribut `unbeliebt` | **personenbezogen, teils bewertend** (Arbeitszeitmodell, Qualifikationsurteile) - mittlerer bis hoher Schutzbedarf |
| **Schüler (Scheduling)** | pseudonyme ID + Heimatklasse, Gruppenmitgliedschaften (`schueler:`/`gruppen:`) - bewusst KEIN Name-Feld (`Stammdaten.vb`, Schueler-Kommentar) | pseudonymisiert; ABER: Gruppenzugehörigkeit kann Besonderes offenbaren (Religion-ev/-kath/Ethik → Art. 9; Typ "Foerderung") |
| **Klassenbildungsdaten** | Attribute (`geschlecht`, `sprachfoerderung`, `kann_kind`, ...), Gruppen (Sozialverhalten-Verteilung, Schulbegleitung, Kita-Herkunft, Wohngebiet), Wünsche inkl. `getrennt`-**Konfliktpaaren**, Fixierungen (`klassenbildung.yaml`) | **hoch, Art.-9-nah** (Förderbedarf, Sozialdaten, ggf. Gesundheit) - höchster Schutzbedarf im System |
| Constraints | typisierte Regeln + `reason`-Freitext (`constraints.yaml`) | meist sachlich, aber `reason` und Referenzen auf Lehrkräfte sind personenbezogene Kanäle |
| **Freitexte/Kommentare** | heute: YAML-Kommentare, `reason` | **unterschätzter Kanal**: in der Beispiel-Fixture steht die Begründung einer Fixierung ("Kind mit Rollstuhl → einziger barrierefreier Raum") als Klartext-Kommentar - eine Gesundheitsinformation. Die GUI muss solchen Freitext IN der geschützten Ablage halten, nie in exportierten Kommentaren (Abschnitt 6.3). |
| Ergebnisse | Lehrerzuteilungen, Stundenpläne (alle Lösungen), Klassenbildungs-Varianten inkl. Ampel-Chips (`output/*.json/md/html`) | erben den Personenbezug ihrer Eingaben (Lehrer-Klarnamen, Schüler-Pseudonyme) |
| **NEU: Klarnamen-Mapping** | Pseudonym-ID → Name/Vorname (+ ggf. Geburtsdatum zur Unterscheidung) | **personenbezogen; der Schlüssel, der alles andere de-pseudonymisiert** - höchster Schutzbedarf, strikt separater Teil |
| **NEU: Audit-Log** | Art.-22-Nachweis nach `klassenbildung-konzept.md` §10: Regelwerk-Snapshots, Läufe, Variantenwahl, jede Fixierung (wer/wann/von→nach), Freigabe | pseudonymisiert zu führen (§10.3); enthält Handelnden-Namen (Schulleitung) |
| GUI-Zustand | Pins, Filter, Fensterlayout, zuletzt geöffnet | unkritisch, aber Pins referenzieren Schüler-IDs → bleibt in der Projektdatei |

Zwei Grundsätze aus dem Bestand werden übernommen und erste Klasse:

- **Pseudonym-IDs sind Vertragsbestandteil** des Rechenkerns
  (`docs/klassenbildung-plan.md`, Architektur-Notizen): Solver, Runner,
  Viewer und alle committeten Artefakte kennen nur S-IDs.
- **Gruppen statt Diagnosen** (Datenminimierung, Art. 9 -
  `klassenbildung-konzept.md` Abschnitt zum Datenmodell): Förderbedarf
  geht als Flag/Gruppenmitgliedschaft ins System, nie als Diagnose.
  Die Beispiel-Fixture lebt das bereits (Sozialverhalten als
  Verteilungs-GRUPPE, nicht als Attribut).

## 3. Anforderungen an die Datenhaltung

| # | Anforderung | Herkunft |
|---|---|---|
| A1 | Lokal, kein Server-/Dienstprozess, keine Cloud | Nutzervorgabe |
| A2 | Einfach wartbar: minimale Abhängigkeiten, einfache Migration, im Fehlerfall inspizierbar | Nutzervorgabe; Alleinentwickler-Realität |
| A3 | Verschlüsselung at rest mit Passwort | Dialogentscheidung 2; Schutzbedarf aus Abschnitt 2 |
| A4 | Klarnamen nur in einem separaten, löschbaren Teil; alles Exportierte pseudonym | Dialogentscheidung 1 |
| A5 | Ein Rechner / eine Person; Weitergabe nur als bewusster Export | Dialogentscheidung 3 |
| A6 | Verlustfreier Ex-/Import der bestehenden YAML/JSON-Formate (`tests/<schule>/input/*`), damit SchoolTestRunner/CI/Beispiele weiterlaufen | Dialogentscheidung 4 |
| A7 | Ergebnisse als benannte Stände sichern (mehrere Läufe je Projekt), inkl. Art.-22-Audit-Log | `klassenbildung-konzept.md` §10; Nachvollziehbarkeits-Qualitätsziel |
| A8 | DSGVO: Löschung muss wirklich löschen; Auskunft je Kind aus gefilterter Sicht möglich | §10.2/§10.3; Abschnitt 7 |
| A9 | Atomares Speichern (kein korrupter Zustand bei Absturz/Stromausfall) | Korrektheits-Qualitätsziel |
| A10 | Schema-Evolution ohne Datenverlust über App-Versionen hinweg | Wartbarkeit |

## 4. Vergleich: filebasiert vs. datenbankbasiert

Verglichen werden die zwei realistischen Kandidaten: **(A) Datei-Ablage**
(JSON-Dokumente, als Container gebündelt) und **(B) eingebettete
Datenbank** (SQLite via `Microsoft.Data.Sqlite`, für A3 zwingend mit
SQLCipher-Bundle `SQLitePCLRaw.bundle_e_sqlcipher`). Server-Datenbanken
scheiden per A1 aus.

| Kriterium | (A) Datei/Container | (B) SQLite (+SQLCipher) |
|---|---|---|
| Abhängigkeiten | **keine neuen** - ZIP (`System.IO.Compression`), AES/PBKDF2 (`System.Security.Cryptography`), JSON (`System.Text.Json`) sind BCL; Serializer existieren bereits | 2-3 NuGet-Pakete inkl. nativer Binärdateien je Plattform - zweiter Native-Stack neben `Google.OrTools` |
| Verschlüsselung (A3) | AES-GCM über den ganzen Container, Schlüssel per PBKDF2 aus Passwort - Standard-BCL-Bausteine | SQLCipher: etabliert, aber zusätzliche native Lib + Key-Handling über Connection-Pragmas |
| Atomarität (A9) | Schreiben in Temp-Datei + `File.Replace` (atomar auf NTFS) - ein Commit-Punkt fürs ganze Projekt | ACID pro Transaktion (Stärke von B) - aber App-Zustand umfasst ohnehin immer das Gesamtdokument |
| **DSGVO-Löschsemantik (A8)** | Jeder Speichervorgang schreibt die Datei komplett neu - entfernte Daten sind **physisch weg** | Gelöschte Zeilen verbleiben in Freelist-Pages bis `VACUUM`; bei SQLCipher zusätzlich zu bedenken. Machbar, aber ein Muss-Wissen mehr |
| Schema-Evolution (A10) | `schema_version` im Manifest + tolerante, reflection-basierte Serializer - im Repo belegt: neue Properties liefen "ohne jede Code-Änderung" durch beide Serializer (`phase2-19-mitgliedschaftsmodell.md`, "Persistenz vollständig kostenlos") | SQL-Migrationsskripte je Versionssprung, die dauerhaft gepflegt und getestet werden müssen |
| Datenform | Bestand ist **dokumentenförmig** (Stammdatenbaum, Constraint-Liste als rohe `JsonObject`s per Wire-Format-Parität arc42 §8.7, Ergebnis-JSONs) - 1:1 ablegbar | Dokumente müssten als BLOB/JSON-Spalten gespeichert werden - die DB verwaltete dann Bytes, ohne relationale Stärken zu nutzen; echte Normalisierung wäre ein zweites, abweichendes Schema neben dem Wire-Format |
| Abfragen/Teil-Laden | ganzes Projekt laden (bei KB-MB-Größe: Millisekunden) | selektive Queries - **bei dieser Datenmenge ohne Nutzen** |
| Größenordnung | eine Schule: zweistellige KB Eingaben, einstellige MB Ergebnisse (belegt durch `tests/*/output/`) | dito - weit unter jeder Schwelle, ab der eine DB trägt |
| Mehrbenutzer/parallel | nicht nötig (A5) | Stärke von B, hier ohne Anforderung |
| Support/Debug | Container mit bekanntem Passwort öffnen → lesbares JSON, diff-bar, im Fehlerbericht anonymisierbar | DB-Browser + Cipher-Key nötig |
| Backup/Umzug | Datei kopieren (bleibt verschlüsselt) | Datei kopieren (dito) - Gleichstand |

**Ergebnis:** Bei dokumentenförmigen Daten in KB-MB-Größe, einem
einzelnen lokalen Nutzer und vorhandenen JSON-Serializern liefert eine
eingebettete Datenbank keine der Stärken, für die man ihre Kosten
(nativer Stack, Migrationen, VACUUM-Disziplin) bezahlen würde. Der
phase2-15-Beschluss "JSON-Dateien, keine eingebettete Datenbank" wird
**bestätigt** und um zwei GUI-Anforderungen erweitert: Bündelung als EIN
Container (statt loser Dateien) und Verschlüsselung des Containers.

SQLite bleibt der dokumentierte **Fallback**, falls sich Anforderungen
ändern - Wechselkriterien in Abschnitt 9.

## 5. Empfehlung: verschlüsselte Ein-Datei-Projektablage

### 5.1 Grundidee

**Eine Schule + ein Schuljahr = eine Projektdatei** (Arbeitstitel
`.splanx`, z.B. `GS-Musterstadt-2026-27.splanx`). Technisch: ein
ZIP-Container mit JSON-Teilen, als Ganzes verschlüsselt.

```
GS-Musterstadt-2026-27.splanx        (AES-GCM-verschlüsselt, Abschnitt 5.3)
└── (ZIP)
    ├── manifest.json          schema_version, app_version, Schulname,
    │                          Schuljahr, angelegt/geändert-Zeitstempel
    ├── stammdaten.json        EXAKT Stammdaten.SerializeStammdaten
    │                          (snake_case, wie heutige JSON-Datei)
    ├── constraints.json       rohes Constraint-Array (Wire-Format §8.7,
    │                          unverändert solver-tauglich)
    ├── klassenbildung.json    KlassenbildungInput (serialisiert wie die
    │                          YAML-Seite, snake_case)
    ├── config.json            Solve-Parameter (RunConfig-Äquivalent)
    ├── mapping.json           Klarnamen-Tabelle (Abschnitt 6) - der
    │                          EINZIGE Ort mit Schülernamen
    ├── audit-log.json         Art.-22-Log nach konzept §10 (append-only
    │                          aus App-Sicht, pseudonymisiert)
    ├── ergebnisse/
    │   ├── 2026-08-20-entwurf-1/
    │   │   ├── lauf.json          Parameter + Zeitstempel + Statuszeile
    │   │   ├── stundenplan.json   wie heutiger Output
    │   │   └── klassenbildung.json
    │   └── 2026-08-22-freigabe/...
    └── gui-state.json         Pins, Filter, Layout (ersetzt die
                               localStorage-Rolle der Viewer, 7.6)
```

Warum dieser Zuschnitt trägt:

- **Die inneren Formate existieren schon.** `stammdaten.json` ist das
  unveränderte Ausgabeformat von `Stammdaten.SerializeStammdaten`
  (`TimetableCore/Stammdaten.vb`); `constraints.json` ist das rohe
  `JsonObject`-Array, das `Solver.Solve` heute schon konsumiert;
  Ergebnis-JSONs sind die heutigen Runner-Outputs. Die Projektdatei
  erfindet kein neues Datenmodell - sie bündelt die vorhandenen
  Wire-Formate plus die drei neuen Teile (mapping, audit-log,
  gui-state).
- **Ein Commit-Punkt.** Der gesamte Projektzustand wird als Einheit
  gespeichert (A9): serialisieren → ZIP in Temp-Datei → verschlüsseln →
  `File.Replace`. Absturz mitten im Speichern hinterlässt die alte,
  intakte Datei.
- **Migration bleibt "kostenlos".** `schema_version` im Manifest;
  neue Felder tolerieren beide Seiten per Reflection (belegt in
  phase2-19). Nur echte Umbauten brauchen eine explizite
  Migrationsfunktion beim Laden - eine pro Bruch, keine Kette von
  SQL-Skripten.

### 5.2 Ergebnis-Historie

Jeder Lauf wird als benannter Stand unter `ergebnisse/` gesichert
(Label vom Nutzer, Default Zeitstempel), mit `lauf.json` als
Parameter-/Herkunftsnachweis (Seed, Limits, Gewichte, App-Version) -
das ist zugleich der "Solver-Lauf"-Eintrag des Audit-Logs. Eine
konfigurierbare Obergrenze (Default z.B. 10 Stände) plus explizites
"Stand löschen" hält die Datei klein; der Freigabe-Stand ist gegen
automatisches Verdrängen geschützt. Ein Stand kann jederzeit als
YAML/JSON/HTML exportiert werden (pseudonym, Abschnitt 6.2).

### 5.3 Kryptographie (TOM, prüfbar dokumentiert)

- Schlüsselableitung: PBKDF2 (SHA-256, Iterationszahl im Manifest-Header
  UNVERSCHLÜSSELT abgelegt, Startwert ≥ 600.000, per Feld erhöhbar),
  Salt pro Datei zufällig.
- Verschlüsselung: AES-256-GCM (authentifiziert - Manipulation oder
  falsches Passwort werden erkannt, kein stilles Falsch-Entschlüsseln);
  Nonce pro Speichervorgang frisch.
- Kein Schlüssel-/Passwort-Material auf Platte; optionales
  "Passwort merken" nur via Windows-DPAPI (an das Benutzerkonto
  gebunden) und abschaltbar.
- Alles BCL (`Rfc2898DeriveBytes`, `AesGcm`) - keine Krypto-Fremdpakete.
- **Passwort vergessen = Daten verloren.** Das ist die bewusste
  Kehrseite echter Verschlüsselung; abzumildern nur organisatorisch
  (Passwort im Schultresor) - als offener Punkt in Abschnitt 9
  (Recovery-Schlüssel als mögliche Ausbaustufe).

### 5.4 Speicherorte und Backups

- Default-Ablage: `Dokumente\Schulplanung\` (sichtbar, bewusst KEIN
  verstecktes AppData - der Nutzer soll wissen, wo seine Daten liegen).
- Backup = Kopie der Datei (bleibt verschlüsselt); die App bietet
  "Sicherungskopie erstellen" und legt vor riskanten Operationen
  (Migration, Import) automatisch `*.bak` daneben.
- Temp-Dateien beim Speichern entstehen im selben Ordner (gleiche
  Verschlüsselung/gleicher Datenträger, kein Klartext-Umweg über %TEMP%).

## 6. Klarnamen und Pseudonymisierung

### 6.1 mapping.json - der einzige Klarnamen-Ort

```jsonc
{
  "schueler": [
    { "id": "S001", "nachname": "Muster", "vorname": "Mia",
      "hinweis": null }   // optionaler Freitext - lebt HIER, nie im Export
  ]
}
```

- Die GUI zeigt überall "Mia Muster (S001)" an, gespeichert und
  gerechnet wird mit `S001`. Die Auflösung passiert ausschließlich zur
  Laufzeit in der Anzeige-Schicht - Solver-Inputs, Ergebnis-JSONs und
  Viewer-HTML bleiben pseudonym (bestehender Vertrag,
  `klassenbildung-plan.md`).
- ID-Vergabe übernimmt die GUI (fortlaufend, nie wiederverwendet -
  eine gelöschte ID bleibt verbrannt, damit alte Audit-Log-Einträge
  nicht auf ein anderes Kind zeigen können).
- "Mapping löschen" entfernt nur `mapping.json` und macht das Projekt
  dauerhaft pseudonym (z.B. für Langzeit-Archivierung nach §10.3) -
  die Planung bleibt vollständig funktionsfähig.
- **Lehrkräfte bleiben Klarnamen im Modell** (der Name ist heute
  Schlüssel in `Stammdaten.vb` und in allen Constraints/Ergebnissen).
  Das ist vertretbar - Lehrereinsatz ist normale schulische
  Personalverwaltung -, macht aber die GESAMTE Projektdatei
  personenbezogen und ist damit ein Treiber für A3. Eine spätere
  Lehrer-Pseudonymisierung wäre ein Kern-Umbau (Schlüsselfeld) und
  bleibt bewusst außerhalb dieses Konzepts (Abschnitt 9).

### 6.2 Die harte Export-Grenze

Alles, was die Projektdatei verlässt, ist pseudonym: YAML-Exporte,
Ergebnis-JSONs, Viewer-HTML, Zwischenablage-Kopien aus Listen. Es gibt
genau EINE gekennzeichnete Ausnahme: ein expliziter
**Klarnamen-Druckexport** (z.B. Klassenlisten zur Einschulung) hinter
einem Warndialog ("enthält Namen - Empfänger und Ablage prüfen"), der
im Audit-Log protokolliert wird. Alles andere darf gefahrlos geteilt,
committet oder als Artifact publiziert werden - exakt der Status quo
der `tests/`-Beispiele.

### 6.3 Freitexte

Konsequenz aus dem Fixture-Befund (Gesundheitsinfo im YAML-Kommentar):
Begründungen und Notizen zu Fixierungen/Wünschen/Constraints erfasst
die GUI als Datenfelder IN der verschlüsselten Projektdatei
(`hinweis`-Felder, Audit-Log). Der YAML-Export schreibt solche Texte
NICHT als Kommentare hinaus; das bestehende `reason`-Feld der
Constraints wird beim Export auf einen neutralen, vom Nutzer
freigegebenen Wortlaut beschränkt (Vorschau im Export-Dialog).

## 7. DSGVO-Betrachtung

### 7.1 Rollen und Rechtsgrundlage

Verantwortliche ist die **Schule** (bzw. der Schulträger);
Rechtsgrundlage der Verarbeitung ist die schulische Aufgabenerfüllung
(Art. 6 Abs. 1 lit. e DSGVO i.V.m. Landesschulrecht, in BW §§ 1, 115
SchG - die konkrete Norm gehört ins Verzeichnis der
Verarbeitungstätigkeiten der Schule, nicht in die Software). Da die
Anwendung vollständig lokal läuft - Solver in-process, LLM lokal via
Ollama, keine Telemetrie, keine Cloud - gibt es **keinen
Auftragsverarbeiter** und keine Drittlandsübermittlung; ein AV-Vertrag
entfällt. Das ist ein bewusster Architekturvorteil des
"kein Backend"-Ansatzes und bleibt Invariante: jede künftige
Online-Funktion wäre eine DSGVO-Neubewertung.

### 7.2 Besondere Kategorien (Art. 9)

Förderbedarf, Sozialverhalten, Schulbegleitung, Religionszugehörigkeit
(über Gruppen ableitbar) und Gesundheitsdaten (Barrierefreiheit) sind
Art.-9-nah bis Art.-9-Daten. Maßnahmen:

- Verschlüsselung der gesamten Projektdatei (5.3) als technische
  Maßnahme nach Art. 32.
- **Gruppen-statt-Diagnose-Vokabular** als UI-Leitplanke fortführen:
  die GUI bietet beim Anlegen von Attributen/Gruppen wertneutrale
  Vorschläge und dokumentiert im Handbuch, dass Diagnosen nicht erfasst
  werden sollen ("die Diagnose selbst muss nie ins System",
  `klassenbildung-konzept.md`).
- Freitext-Disziplin nach 6.3.

### 7.3 Art. 22 - menschliche Letztentscheidung

Das Audit-Log-Konzept aus `klassenbildung-konzept.md` §10 bekommt mit
`audit-log.json` seinen konkreten Speicherort: Regelwerk-Snapshots je
Lauf, Solver-Läufe (Parameter/Zielwerte/Verletzungsreport),
Variantenwahl, jede Fixierung (wer/wann/Kind/von→nach - "wer" ist bei
A5 die eine benannte Nutzerin, von der App beim Projektanlegen
erfragt), Freigabe mit aktiver Bestätigung. Die GUI schreibt die
Einträge automatisch bei den jeweiligen Aktionen - genau die Aktionen,
die der Klassenbildungs-Viewer heute schon als Pins/Herkünfte kennt.
Das Log ist aus App-Sicht append-only; Läufe-Löschung (5.2) entfernt
Ergebnisdaten, lässt aber die Log-Zeile stehen.

### 7.4 Betroffenenrechte

- **Auskunft (Art. 15):** "Bericht je Kind" generiert die GUI aus der
  gefilterten Sicht nach §10.2 - zugeordnete Klasse, betroffene Regeln
  in zulässiger Granularität ("Verteilungskriterium" statt Diagnose),
  manuelle Eingriffe, Hinweis auf menschliche Letztentscheidung.
  **Nie aus dem Roh-Log, keine Daten Dritter** (keine Konfliktpaar-
  Gegenseite, keine Förderinfo von Mitschülern).
- **Berichtigung (Art. 16):** normale Editor-Funktion.
- **Löschung (Art. 17):** Kind entfernen löscht Attribute, Gruppen-/
  Wunsch-Mitgliedschaften und Mapping-Eintrag; der nächste
  Speichervorgang schreibt die Datei neu - dank Rewrite-Semantik (4)
  ist der Datensatz physisch weg. Hinweisdialog erinnert an manuell
  erstellte Kopien/Backups und Papierkorb.

### 7.5 Speicherbegrenzung und Löschkonzept

- Projekt = Schuljahr; die App schlägt nach übernommener Frist (§10.3:
  z.B. Ende erstes Schulhalbjahr nach der Klassenbildung) aktiv vor,
  das Klarnamen-Mapping zu löschen (6.1) und Alt-Stände auszudünnen.
- Projektdatei löschen = vollständige Löschung (ein Artefakt, A5);
  das Handbuch weist auf Backups und WebView2-Daten (7.6) hin.
- Die Ergebnis-Obergrenze (5.2) ist zugleich Speicherbegrenzungs-TOM.

### 7.6 WebView2-Spezifika

WebView2 legt einen **User-Data-Folder** an (Cache, localStorage,
IndexedDB der gehosteten Seiten). Die Viewer nutzen localStorage heute
als Komfort (Pins des Klassenbildungs-Boards). Vorgaben:

- User-Data-Folder explizit auf einen app-eigenen Pfad setzen
  (`%LocalAppData%\Schulplanung\WebView2\`) - nie den Default neben
  der EXE.
- Die localStorage-Rolle übernimmt `gui-state.json` in der
  verschlüsselten Projektdatei: die GUI injiziert den Zustand beim
  Laden des Viewers und liest ihn über die WebView2-Bridge zurück
  (die Viewer behalten ihren try/catch-localStorage-Fallback für den
  Doppelklick-Betrieb - im WebView2-Betrieb bleibt localStorage leer).
- Viewer-Inhalte werden aus dem entschlüsselten Bestand in-memory
  erzeugt (bestehende `Build*Html`-Module) und per `NavigateToString`
  bzw. virtuellem Host-Mapping geladen - **keine Klartext-HTML-Datei
  auf Platte, kein Netzzugriff** (Navigation auf externe URLs wird in
  WebView2 blockiert).
- "Browserdaten bereinigen" beim Projektschließen/-löschen ruft die
  WebView2-Profil-Löschung auf; der Ordnerpfad steht im Handbuch
  (Löschkonzept 7.5).

### 7.7 Übersicht TOMs

Verschlüsselung at rest (5.3) · pseudonymer Rechenkern + harte
Export-Grenze (6.2) · Freitext-Disziplin (6.3) · atomares Speichern
ohne Klartext-Temp (5.4) · Audit-Log (7.3) · Lösch-Rewrite (7.4) ·
WebView2-Datenhygiene (7.6) · kein Netz/keine Telemetrie (7.1) ·
Zugriffsschutz auf OS-Ebene ergänzend (BitLocker empfohlen im
Handbuch, ersetzt A3 nicht).

## 8. Integration in die bestehende Architektur

### 8.1 Wiederverwendung (kein neues Datenmodell)

| Baustein | Rolle in der GUI-Datenhaltung |
|---|---|
| `Stammdaten.SerializeStammdaten`/`DeserializeStammdaten` (`TimetableCore/Stammdaten.vb`) | liest/schreibt `stammdaten.json` im Container unverändert; das typisierte Modell ist zugleich die WPF-Databinding-Quelle (genau der in arc42 §8.7 vorgesehene Weg) |
| `StammdatenValidation.ValidateStammdaten`, `Validation.ValidateEntities`, `Klassenbildung.ValidateKlassenbildung` | Fail-Fast vor jedem Solve aus der GUI - identische Disziplin wie CLI |
| `BuildEntitiesFragment` + `BuildAssignmentConstraints` | Projektion Projektdatei → Solve-Pipeline, wie heute im Runner |
| `Formatting.ToStundentafelJson(Multi)`, `KlassenRun`-JSON-Aufbau, `StundentafelHtml`/`KlassenbildungHtml` | erzeugen Ergebnis-JSON/Viewer-HTML aus dem Projektbestand für `ergebnisse/` und WebView2 |
| `Verifier.*`, `KlassenbildungQuality.Bewerte` | unabhängige Nachprüfung bleibt Pflichtschritt jedes GUI-Laufs |

### 8.2 YAML-Ex-/Import (A6)

Die YAML-Module leben bewusst NUR im SchoolTestRunner (YamlDotNet
außerhalb des Kerns, arc42 §5.1). Für die GUI:

- **Empfehlung**: die vier YAML-Module (`YamlStammdaten`,
  `YamlConstraints`, `YamlKlassenbildung`, `LoadConfig`-Teil) bei
  GUI-Umsetzung in ein kleines gemeinsames Projekt `TimetableYaml`
  extrahieren, das Runner und GUI referenzieren - hält `TimetableCore`
  YAML-frei (bestehende Entscheidung) und vermeidet, dass die GUI das
  Konsolenprojekt referenziert. Reine Verschiebung, kein Umbau.
- **Lücke, die die Umsetzung schließen muss**: heute existieren
  Schreib-APIs nur für Stammdaten (`SaveStammdatenYaml`; `Scaffold`
  schreibt ein constraints-Gerüst). `constraints.yaml`,
  `klassenbildung.yaml` und `config.yaml` werden nie programmatisch
  geschrieben - der GUI-Export braucht Serialize-Gegenstücke
  (YamlDotNet-Serializer mit denselben NamingConventions; dank
  Reflection-Symmetrie überschaubar).
- Import = bestehende Lader + Validierung + Übernahme in den Container;
  Export = Gegenrichtung in einen `input/`-Ordner nach
  `tests/<schule>/`-Layout. Damit bleibt der komplette CLI-Weg
  (`run`/`klassen`/`render`) als zweiter, skriptbarer Kanal erhalten.

### 8.3 Was sich am Bestand NICHT ändert

`TimetableCore` bekommt weder Krypto noch ZIP noch YAML - die
Projektdatei-Logik (Container, Verschlüsselung, Mapping, Audit-Log)
gehört in die GUI-Schicht (`TimetableGui` bzw. ein schlankes
`TimetableProjekt`-Modul daneben, entscheidet die Umsetzung).
SchoolTestRunner, Tests, Beispiele und Viewer-Templates bleiben
unverändert; die committeten `tests/`-Fixtures bleiben pseudonym und
unverschlüsselt (synthetische Daten, kein Personenbezug).

## 9. Bewusst nicht umgesetzt + offene Punkte

**Bewusst nicht** (mit Wechselkriterien):

- **Kein Server/Backend/Cloud-Sync** - Invariante dieses Konzepts;
  jede Aufweichung ist eine DSGVO-Neubewertung (7.1).
- **Keine Mehrbenutzer-/Parallelzugriffe** (A5). Wechselkriterium:
  sobald mehrere Personen denselben Bestand gleichzeitig bearbeiten
  sollen, ist SQLite (dann mit SQLCipher) die richtige Basis - der
  Container-Inhalt ist dafür vorbereitet (JSON-Dokumente ließen sich
  1:1 in Dokument-Tabellen überführen).
- **Kein SQLite jetzt** (Abschnitt 4). Weitere Wechselkriterien:
  Ergebnis-Historien deutlich jenseits einiger MB mit
  Teil-Lade-Bedarf; feldgenaue Abfragen über viele Projekte hinweg.
- **Keine Lehrkräfte-Pseudonymisierung** (6.1) - Kern-Umbau
  (Name = Schlüssel), als mögliche spätere Stufe notiert.

**Offene Punkte für die Umsetzungsphase:**

1. **Passwort-vergessen-Politik**: Datenverlust akzeptiert (Empfehlung:
   ja, mit organisatorischer Abmilderung) oder Recovery-Schlüssel beim
   Anlegen (ausgedruckt in den Schultresor)? Entscheidung vor dem
   ersten Release.
2. **Dateiendung/Branding** (`.splanx` ist Arbeitstitel) und
   Explorer-Verknüpfung.
3. **`reason`-Export-Vorschau** (6.3): UI-Detail des Export-Dialogs.
4. **arc42-Nachzug**: für die Technologie- und Verteilungsangaben
   erledigt (§2 WPF+WebView2, §3.2 WebView2-Kontextzeile, §5.1, §7
   inkl. Projektdatei + Evergreen-Runtime, §8.10, §11). Bei
   Umsetzungsbeginn verbleibt der Feinschnitt der neuen Projekte
   (TimetableGui/TimetableYaml/TimetableProjekt) in §5.1 sowie ggf.
   ein eigenes Querschnittskonzept in §8.
5. **Muster-Eintrag fürs Verzeichnis von Verarbeitungstätigkeiten** der
   Schule als Handbuch-Anhang (Hilfestellung, keine Rechtsberatung).
