# Schulplanung (Desktop) – Benutzerhandbuch

Dieses Handbuch richtet sich an **Menschen ohne Programmierkenntnisse** –
Schulleitung, Sekretariat, Stundenplanteam. Es beschreibt die
Windows-Anwendung *Schulplanung*.

Wer lieber auf der Kommandozeile arbeitet, findet den entsprechenden Weg
in [`schooltestrunner-benutzerhandbuch.md`](schooltestrunner-benutzerhandbuch.md).
Beide Wege rechnen mit demselben Kern und erzeugen dieselben Ergebnisse.

---

## Was die Anwendung tut

Sie beantwortet zwei getrennte Fragen:

1. **Klassenbildung** – welches Kind kommt in welche Klasse?
2. **Stundenplan** – wann liegt welche Stunde, bei wem, in welchem Raum?

Beides sind eigene Rechnungen mit eigenen Eingaben, eigenen Ergebnissen
und eigener Freigabe. Man kann die eine nutzen und die andere nicht.

**Was die Anwendung nicht tut:** Sie entscheidet nichts. Sie rechnet
Vorschläge und zeigt, was an ihnen nicht aufgeht. Die Entscheidung
trifft ein Mensch und bestätigt sie namentlich – siehe *Freigabe*.

---

## Der Weg durch die Anwendung

Links stehen vier Bereiche: **Start**, **Klassen**, **Stunden**, **Läufe**.

Die Startseite zeigt zwei Karten – eine je Rechnung. Jede Zeile sagt, wo
Sie stehen, und ist anklickbar: die Eingabe-Zeilen öffnen ihre Maske,
*Rechnen* rechnet, *Entscheiden* öffnet die Ansicht, *Freigabe* gibt frei.

```
Klassenbildung                          Stundenplan
[1] Kinder & Regeln  ▶ Noch keine Kinder [1] Stammdaten  ✓ 8 Klassen, 12 Lehrkräfte – Prüfung grün
[2] Rechnen          ○ Erst Kinder…      [2] Regeln      ▶ Keine Handregeln
[3] Entscheiden      ○ Erst rechnen      [3] Rechnen     ✓ Zuletzt 30.08. 14:32 – 3 Lösung(en)
[4] Freigabe         ○ Noch nicht        [4] Entscheiden ▶ Im Dashboard eine Lösung übernehmen
                                         [5] Freigabe    ○ Noch nicht freigegeben
```

In den Bereichen **Klassen** und **Stunden** steht dieselbe Information
in einer Leiste über der Ansicht: die Eingaben mit ihrem Stand, der
Rechnen-Knopf und rechts die Auswahl des Standes, den Sie sehen wollen.
Solange noch nichts gerechnet ist, zeigt der Bereich statt der Ansicht
seinen Ablauf.

| Zeichen | Bedeutung |
|---|---|
| ○ | offen – hier ist noch nichts passiert |
| ▶ | bereit – Sie können hier weitermachen |
| ✓ | erledigt |
| ! | Achtung – es gibt etwas, das noch nicht hält |

---

## 1. Ein Projekt anlegen

Ein *Projekt* ist eine einzelne, verschlüsselte Datei (`.splanx`) mit
allem darin: Stammdaten, Regeln, Ergebnisse, Protokoll. Sie können sie
kopieren, sichern und weitergeben wie jede andere Datei.

Drei Wege, oben auf der Startseite:

**Neues Projekt…** führt durch fünf Schritte:

1. **Schule** – Name, Schulart, Bundesland, Schuljahr
2. **Struktur** – wie viele Klassenstufen, wie viele Züge, wie viele
   Klassenlehrkräfte. Fachlehrkräfte ergänzt die Anwendung selbst, ihre
   Zahl folgt aus den Wochenstunden
3. **Schüler & Gruppen** – überspringbar. Der Stundenplan braucht keine
   Einzelkinder; nötig ist dieser Schritt erst, wenn klassenübergreifende
   Gruppen (Religion, Förderung, Niveaukurse) mitgeplant werden sollen
4. **Schutz** – Passwort und Speicherort
5. **Zusammenfassung** – was gleich entsteht, in Zahlen

> **Das Passwort verschlüsselt die Datei. Es gibt keine
> Wiederherstellung.** Geht es verloren, ist der Inhalt verloren.

Bisher sind nur die Stundentafeln für **Baden-Württemberg** hinterlegt.
Für andere Länder legen Sie die Stammdaten von Hand an – erfundene
Lehrplanzahlen wären schlimmer als etwas Tipparbeit.

**Bestehende Schule übernehmen…** liest einen vorhandenen Schulordner
(das Format der Kommandozeilen-Variante).

**Öffnen…** öffnet eine `.splanx`-Datei.

---

## 2. Stammdaten

*Bearbeiten → Stammdaten…*, Klick auf die Zeile *Stammdaten* der
Stundenplan-Karte oder auf *Stammdaten* in der Leiste des Bereichs
**Stunden**.

Acht Bereiche, alle nach demselben Muster: **Liste links, Formular
rechts**, darunter *Neu · Duplizieren · Löschen · Prüfen*.

| Reiter | Inhalt |
|---|---|
| **Schuldaten** | Tage, Stunden pro Tag |
| **Klassen** | Klassenstufen und die Klassen darin |
| **Fächer** | Wochenstunden je Stufe, Blocklänge |
| **Räume** | |
| **Lehrkräfte** | Deputat, Anrechnungen |
| **Qualifikationen** | wer darf was unterrichten |
| **Schüler & Gruppen** | die Schülerliste des Stundenplans |
| **Feste Zuordnungen** | „Frau X behält die 2a in Deutsch" |

### Wann wird gespeichert?

Zwei verschiedene Dinge:

- **Ins Projekt übernommen** wird eine Eingabe, sobald Sie das Feld
  verlassen (Tab oder Klick woandershin). Auswahllisten und Häkchen
  wirken sofort.
- **In die Datei geschrieben** wird erst beim Speichern.

Unten rechts steht immer, woran Sie sind: `Alle Änderungen gespeichert`
oder `● Nicht gespeicherte Änderungen`. Daneben ein **Speichern**-Knopf;
**Strg+S** wirkt auch hier.

> **Namen sind Schlüssel.** Benennen Sie eine Lehrkraft, ein Fach, eine
> Klasse oder einen Raum um, zieht die Anwendung alle Verweise mit und
> zeigt vorher, wie viele es sind. Löschen zeigt die Folgen an, bevor es
> geschieht.

---

## 3. Regeln

*Bearbeiten → Regeln…*, Klick auf die Zeile *Regeln* der
Stundenplan-Karte oder auf *Regeln* in der Leiste des Bereichs **Stunden**.

Regeln sind **optional**. Viele Schulen kommen ohne aus – der Plan
rechnet dann allein aus Stammdaten und Kontingentstundentafel.

Acht Typen, jeder mit eigener Maske:

| Typ | wofür |
|---|---|
| Gesperrter Slot | „Frau Meier hat montags in der 1. Stunde nie Unterricht" |
| Fach-Zeitfenster | „Sport der 3. Klassen liegt Di–Do in Stunde 3–6" |
| Belegungsfenster | „die 1a ist Mo–Fr in Stunde 1–4 belegt" |
| Lehrkraft-Verfügbarkeit | verfügbare Tage, gesperrte Einzelstunden |
| Pflicht-Slot | „die Chor-Gesamtprobe liegt Do in der 7." |
| Raumbedarf | „Sport nur in Turnhalle 1 oder 2" |
| Einzelbelegung | punktuelle Fälle |
| Ad-hoc-Block | Ausnahmen zur Blocklänge des Fachs |

Bei Zeitangaben klicken oder ziehen Sie im **Raster** (Tage × Stunden) –
dieselbe Optik wie in der Stundentafel.

Zwei Dinge sind bewusst nicht bearbeitbar:

- **Generierte Regeln** (Reiter daneben) entstehen bei jedem Lauf neu aus
  den Stammdaten. Handpflege ist dort ausgeschlossen.
- Wer es genau wissen will, findet im **YAML-Expertenmodus** den
  vollständigen Regeltext – auch für seltene Typen ohne eigene Maske.

---

## 4. Klassenbildung: Kinder, Gruppen, Wünsche

*Bearbeiten → Klassenbildung: Kinder & Regeln…*, Klick auf die Zeile
*Kinder & Regeln* der Klassenbildungs-Karte oder in der Leiste des
Bereichs **Klassen**.

Diese Liste ist **nicht** die Schülerliste der Stammdaten. Die
Klassenbildung läuft *vor* der Klassenzuteilung; sie hat ihre eigene
Einschulungsliste.

### Kinder & Rahmen

Klassenanzahl, Mindest- und Höchstgröße, und wahlweise eine Stufe oder
eigene Klassennamen (`5a, 5b, 5c, 5d`).

Die Kinder tragen Sie ein, fügen sie aus der Zwischenablage ein oder
lesen sie aus einer CSV-Datei.

### Kinder aus einer Datei übernehmen

**„Aus CSV-Datei…"** – eine Semikolon- oder Tabulator-getrennte Datei,
wie Excel sie mit *Speichern unter → CSV* erzeugt. Sie können auch
einfach einen Bereich in Excel markieren, kopieren und **„Aus
Zwischenablage einfügen…"** wählen.

Danach ordnen Sie **jede Spalte** zu:

| Auswahl | Wirkung |
|---|---|
| *verwerfen* | die Spalte bleibt draußen – **das ist die Vorgabe** |
| Nachname / Vorname | der Klarname; die Id vergibt die Anwendung |
| Attribut: … | ein Merkmal, auf das Balance-Regeln zugreifen |
| Gruppe | je vorkommendem Wert eine Gruppe (Spalte *Religion* mit ev/kath/ethik ergibt drei) |
| Klasse (als Fixierung) | eine bestehende Einteilung übernehmen |

> **Warum nichts automatisch übernommen wird:** Eine Klassenliste aus
> dem Sekretariat enthält oft Telefonnummern und Geburtsdaten. Was
> niemand ausdrücklich haben will, kommt nicht herein. Die Vorschau
> streicht verworfene Spalten durch, und nach dem Übernehmen nennt der
> Bericht sie namentlich.

Führt Ihr Projekt schon Merkmale, stehen sie **namentlich** in der Liste
– wählen Sie sie dort, statt ein zweites mit anderer Schreibweise
anzulegen.

Zum Ausprobieren liegen zwei Beispieldateien bereit:
`tests/bw-grundschule-beispiel/import-beispiel/` und
`tests/bw-gms-beispiel/import-beispiel/`, jeweils mit einer eigenen
Erklärung.

### Gruppen, Balance, Wünsche

| Reiter | Bedeutung | Vorgabe |
|---|---|---|
| **Gruppen** | Kinder, die zusammengehören (*Bündelung*) oder verteilt werden sollen (*Verteilung*, mit „Höchstens je Klasse“). Eine große Bündelung darf mit „Mindestens je Klasse“ in Grüppchen sitzen: wo die Gruppe in einer Klasse vorkommt, mindestens so viele – niemand allein | weich, Priorität 2 |
| **Balance** | ein Merkmal gleichmäßig über die Klassen verteilen, mit Toleranz | weich, Priorität 2 |
| **Wünsche** | zwei Kinder zusammen oder getrennt | weich, Priorität 1 |
| **Fixierungen** | ein Kind fest in eine Klasse | entstehen meist am Board |

*Weich* heißt: die Regel soll erfüllt werden, verhindert aber keine
Lösung. *Hart* heißt: ohne sie gibt es keine.

### Solver

*Bearbeiten → Solver-Einstellungen…* stellt Zeitbudget, Anzahl der
Lösungen und der Varianten ein – für beide Rechnungen. Die Vorgaben
sind brauchbar; ein Expertenbereich darunter öffnet die übrigen
Stellschrauben, falls ein Lauf partout nichts findet.

---

## 5. Rechnen

- **F5** – Klassenbildung
- **F6** – Stundenplan

Dieselben Knöpfe stehen in der Leiste des jeweiligen Bereichs, in der
Zeile *Rechnen* der Startkarte und im Menü *Planung*.

Beide laufen im Hintergrund; die Anwendung bleibt bedienbar. Die
Statuszeile zeigt Stufe, verstrichene Zeit und gefundene Lösungen. Der
Lauf lässt sich jederzeit abbrechen – bereits fertige Ergebnisse bleiben
erhalten.

Findet der Solver nichts, sagt die Statuszeile, in welcher Stufe es
hakte. „Stammdatenprüfung" heißt: Ihre Daten widersprechen sich.
„Solverlauf" heißt: die Regeln lassen keinen Plan zu, oder die Zeit
reichte nicht.

Fehlt in den Stammdaten etwas, startet der Lauf gar nicht erst und die
Anwendung nennt den Grund.

---

## 6. Entscheiden

Nach dem Lauf zeigt das Dashboard das Ergebnis – der eingebettete
Viewer, mit Lösungsübersicht, Qualitätsspalten, Klassen- und
Lehrerplänen und der Vergleichsansicht zwischen zwei Lösungen.

Zwei Aktionen kommen aus der Anwendung dazu:

- **„Diese Lösung als Arbeitsstand übernehmen"** – markiert die in der
  Übersicht gewählte Lösung. Sie gilt danach für Berichte und Freigabe.
- **„Neu rechnen"** mit Zeitbudget und Lösungszahl, direkt aus der
  Ansicht. Die Werte werden in die Solver-Einstellungen übernommen,
  damit dort später nichts anderes steht als gerechnet wurde.

Beim Klassenbildungs-Board können Sie zusätzlich Kinder anpinnen und
verschieben; „Neu rechnen" übernimmt diese Fixierungen unmittelbar.

---

## 7. Freigabe

Aus dem Bereich **Läufe** oder direkt aus der Ansicht („Freigeben…").

Der Dialog zeigt **die verbleibenden Regelabweichungen im Wortlaut** und
verlangt:

1. das Häkchen unter dem Bestätigungssatz,
2. den **Namen** der freigebenden Person,
3. bei verbleibenden Abweichungen eine **eigene Begründung**: *Warum ist
   der Stand trotz dieser Abweichungen vertretbar?*

> **Warum so umständlich?** Weil das Ergebnis ein Nachweis sein muss.
> Art. 22 DSGVO verbietet Entscheidungen mit erheblicher Wirkung, die
> ausschließlich automatisiert getroffen werden. Ein Häkchen belegt, dass
> geklickt wurde – nicht, dass geprüft wurde. Ihre Begründung in eigenen
> Worten belegt es.
>
> Sie wird **wörtlich ins Protokoll übernommen** und bleibt dort. Tragen
> Sie keine Namen Dritter und keine Gesundheitsangaben ein; die
> Regelbezeichnungen im Dialog genügen.

Nach der Freigabe ist der Stand **geschützt** – gegen Löschen und gegen
Verdrängen durch ältere Läufe.

Klassenbildung und Stundenplan werden **getrennt** freigegeben. Eine
zweite Freigabe derselben Art zieht die erste zurück, sichtbar und mit
Protokolleintrag.

---

## 8. Läufe und Stände

Jeder Lauf hinterlässt einen *Stand*. Der schnellste Weg zu einem
früheren Stand ist die Auswahl rechts in der Leiste des Bereichs
**Klassen** bzw. **Stunden** – sie zeigt nur die Stände dieser Rechnung.
Der Bereich **Läufe** listet alle, neueste zuerst:

- **Ansehen** – zeigt den Stand wieder an, ohne neu zu rechnen
- **Umbenennen** – außer bei freigegebenen Ständen; deren Label gehört
  zum Nachweis
- **Löschen** – mit Folgenhinweis. Die Protokollzeile des Laufs bleibt in
  jedem Fall erhalten
- **Als Freigabe markieren**

Ältere Stände werden verdrängt, sobald die Obergrenze erreicht ist – die
Anwendung sagt, welche. Freigegebene und geschützte nie.

---

## 9. Klarnamen exportieren

*Extras → Klarnamen exportieren…* schreibt eine CSV mit Id, Nachname,
Vorname und – falls fixiert – Klasse.

> **Das ist die einzige Stelle, an der Klarnamen das Projekt verlassen.**
> Überall sonst wandert nur die pseudonyme Id nach draußen; die Zuordnung
> Id ↔ Kind bleibt in der verschlüsselten Projektdatei. Eine CSV ist
> unverschlüsselt und landet erfahrungsgemäß in Mail-Anhängen und auf
> Netzlaufwerken.
>
> Der Export wird im Projekt protokolliert.

---

## Bekannte Grenzen

- **Nur Baden-Württemberg** ist als Kontingentstundentafel hinterlegt.
- **Kein Excel-Import** (`.xlsx`). CSV und Kopieren aus Excel decken die
  Fälle ab, ohne eine weitere Abhängigkeit einzuführen.
- **Kein Mehrbenutzerbetrieb.** Ein Projekt ist eine Datei; zwei Personen
  sollten nicht gleichzeitig daran arbeiten.
- **Kein automatisches Speichern.** Bewusst: Sie sollen entscheiden, wann
  ein Zwischenstand gilt. Der Indikator unten rechts sagt Ihnen, ob
  etwas offen ist.
- **Ein verlorenes Passwort ist endgültig.**
- **Vertretungsplanung** ist nicht enthalten.

---

## Wenn etwas nicht funktioniert

| Beobachtung | wahrscheinliche Ursache |
|---|---|
| „Rechnen" bleibt aus | Es fehlen Klassen, Fächer oder Lehrkräfte. Die Zeile *Stammdaten* der Stundenplan-Karte nennt den ersten Befund |
| Lauf endet ohne Lösung | Die Regeln lassen keinen Plan zu, oder das Zeitbudget war zu knapp. Die Statuszeile nennt die Stufe |
| Import legt keine Fixierungen an | Die Klasse-Spalte enthält Namen (`5a`), aber im Rahmen sind keine Klassennamen gesetzt. Erst *Labels* eintragen |
| Umlaute nach dem Import kaputt | Sehr selten – die Anwendung erkennt UTF-8 und Windows-1252. Speichern Sie die Datei notfalls aus Excel neu als „CSV UTF-8" |
| Eine Regel wirkt nicht | Sie zeigt womöglich auf einen Slot außerhalb des Rasters. Die Zeile *Regeln* weist darauf hin |

---

## Weiterführende Dokumentation

- [`schooltestrunner-benutzerhandbuch.md`](schooltestrunner-benutzerhandbuch.md)
  – derselbe Kern auf der Kommandozeile
- [`gui-ui-konzept.md`](gui-ui-konzept.md) – warum die Oberfläche so
  aufgebaut ist
- [`klassenbildung-konzept.md`](klassenbildung-konzept.md) – das
  Rechenmodell der Klassenbildung, inklusive der DSGVO-Anforderungen
- [`arc42-architecture.md`](arc42-architecture.md) – die
  Architekturdokumentation
