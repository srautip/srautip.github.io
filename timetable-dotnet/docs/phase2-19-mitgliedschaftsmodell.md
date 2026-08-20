# Phase 2.19: Mitgliedschaftsdatenmodell (Schüler + Gruppen) - Schritt 1

Dieser Bericht dokumentiert Phase 2.19 (siehe Plan, Abschnitt "Phase 2.19
(feingeplant)"): der erste Schritt hin zu einer Antwort auf die Nutzerfrage,
wie Fächer wie Religion (klassenübergreifend in ev./kath./Ethik aufgeteilt),
Fördergruppen (klassen-/jahrgangsübergreifend, teils parallel zum
Regelunterricht) und Lerngruppen mit Aufsichtspools in der Architektur
abgebildet werden können.

## Kontext: die drei recherchierten Grenzfälle

Eine Bestandsaufnahme gegen den echten Code ergab drei wiederkehrende
strukturelle Grenzen, keine drei unabhängigen Einzellücken:

1. **Kein Schüler-/Individualmodell.** Weder `Stammdaten.vb` noch
   `Solver.vb`/`Models.vb` kannten vor dieser Phase ein Konzept von
   einzelnen Schülern - nur `Klasse` (atomares Ganzes) oder
   Kursstufen-Wahlprofile (reine Anzahl, keine Identität).
2. **`no_overlap(class=X)` behandelt eine Klasse als unteilbares
   Zeit-Resource** (`Solver.vb`, Zeile 463-495): höchstens EINE Lesson
   dieser Klasse pro Slot - kein Konzept einer Teilbelegung/eines Splits.
3. **Kein Lehrer-Pool-Konzept** - weder `Lehrereinsatzplanung.vb` noch
   `Kursblockung.vb` kennen "irgendeine von mehreren austauschbaren
   Aufsichten", nur feste 1:1-Zuordnungen.

Ein anschließender Vergleich "vollständiges Individualmodell" vs.
"klassenunabhängiges Gruppenmodell ohne echte Schüler-Identität" ergab:
nur ein (und sei es leichtgewichtiges) Mitgliedschaftsmodell kann für
Fördergruppen (Teilmenge einer Klasse, parallel zum Rest) die im Projekt
durchgängig eingehaltene Garantie "Verifier beweist unabhängig 0
Kollisionen" aufrechterhalten. Ein reines Gruppenmodell ohne
Schüler-Identität kann das nur für den einfacheren Fall einer
VOLLSTÄNDIGEN Klassen-Partition (Religion: jeder Schüler ist in genau
einer Gruppe) - für einen echten Pull-out (nur 3 von 25 Schülern
verlassen die Klasse) fehlt ohne Individualdaten die Grundlage, um
Kollisionsfreiheit überhaupt zu verifizieren.

## Nutzerentscheidungen

1. **Umfang dieses ersten Schritts:** Kern-Datenmodell (neue Entitäten +
   Laden/Speichern + Validierung) PLUS YAML/SchoolTestRunner-Anbindung,
   damit sich das Modell direkt in einem `tests/<schule>/`-Testfall
   ausprobieren lässt. Ausdrücklich NICHT Teil dieses Schritts: jede
   Solver- oder Verifier-Wirkung (kein neuer `no_overlap`-Mechanismus,
   keine Kollisionsprüfung, keine Session-Erzeugung aus Gruppen) - siehe
   "Nächster Schritt" unten.
2. **Schüler-Identität:** nur eine pseudonyme ID (z.B. `"S-3a-07"`), KEIN
   Name-Feld - hält das Modell datenschutzfreundlich, ein Klarname wird
   fürs Scheduling nicht gebraucht.

**Eigene Design-Entscheidung** (nicht explizit erfragt, geringes Risiko):
EIN generischer `Gruppe`-Typ mit einem informativen, freien `Typ`-Textfeld
(z.B. `"Fachgruppe"`/`"Foerderung"`/`"Aufsicht"`) statt drei getrennter
Entitätsklassen - deckt alle drei recherchierten Anwendungsfälle
strukturell identisch ab, vermeidet Code-Verdreifachung, `Typ` hat in
diesem Schritt keine Solver-Bedeutung (mirrort `Raum.Typ`).

## Datenmodell

`TimetableCore/Stammdaten.vb`, zwei neue Klassen (gleiches Stil-Muster wie
`Klasse`/`Raum`):

```vb
Public NotInheritable Class Schueler
    Public Property Id As String
    Public Property Klasse As String   ' Heimatklasse, referenziert Klasse.Name
End Class

Public NotInheritable Class Gruppe
    Public Property Name As String
    Public Property Typ As String      ' Freitext, informativ
    Public Property MitgliederSchuelerIds As New List(Of String)
End Class
```

Auf `Stammdatenbestand`: `Public Property Schueler As New List(Of
Schueler)` und `Public Property Gruppen As New List(Of Gruppe)`.

**Live-verworfene Zwischenentscheidung, dokumentiert als Lehrstück:** die
Property hieß zunächst `Schuelerschaft` (nach dem Vorbild
`Lehrkraefte As List(Of Lehrer)`, wo Property- und Typname bewusst
verschieden sind, weil "Schüler" im Deutschen keine vom Singular
verschiedene Pluralform hat). Der Pflicht-Live-Rundtrip-Test (siehe
unten) scheiterte prompt mit `YamlException: Property 'schueler' not
found` - die eigene Diagnose-YAML war intuitiv mit `schueler:` geschrieben
worden, nicht mit `schuelerschaft:`. Da das YAML-Format explizit auf
einfache GitHub-Web-Bearbeitung zielt, zählt genau dieses intuitive
Autoren-Verhalten mehr als die zunächst gewählte grammatische
Konsistenz - die Property wurde noch in derselben Sitzung auf `Schueler`
umbenannt (VB.NET erlaubt einen Property-Namen identisch zu seinem
eigenen Elementtyp problemlos).

**Persistenz vollständig kostenlos:** `Stammdaten.SerializeStammdaten`/
`DeserializeStammdaten` (System.Text.Json) UND
`SchoolTestRunner/YamlStammdaten.vb` (YamlDotNet) sind beide
reflection-basiert und liefen ohne jede Code-Änderung korrekt, sobald die
beiden neuen Properties existierten. `BuildEntitiesFragment` bleibt
bewusst unverändert - Schüler/Gruppen haben in diesem Schritt keine
Solver-relevante Projektion.

## Validierung

`TimetableCore/StammdatenValidation.vb`, drei neue Prüfungen (gleicher
Stil wie die bestehenden Cross-Reference-Schleifen):

- `schueler[i].klasse` muss eine bekannte `klassen[].name` referenzieren.
- Doppelte `schueler[i].id` werden abgelehnt (die ID ist der alleinige
  Schlüssel für Gruppen-Mitgliedschaft - eine stille Dopplung wäre ein
  echter Fallstrick für jeden späteren Verifier-Schritt).
- `gruppen[i].mitglieder_schueler_ids[j]` muss eine bekannte Schüler-ID
  referenzieren.

Bewusst NICHT geprüft (zurückgestellt, da noch keine Solver-Bedeutung
existiert): Vollständigkeit einer Gruppen-Partition, leere Gruppen,
Überschneidung mehrerer Gruppenmitgliedschaften.

## Live-Verifikation

- **YAML-Rundtrip (Pflicht-Gate):** ein temporäres Diagnose-Programm lud
  eine handgeschriebene YAML mit `schueler:`/`gruppen:`-Abschnitten
  (3 Schüler, 2 Fachgruppen à 1-2 Mitglieder), bestätigte
  `ValidateStammdaten`-Fehler = 0, und re-serialisierte den Bestand -
  beides live geprüft, nicht nur angenommen. Deckte dabei den oben
  beschriebenen Property-Namen-Fehlgriff auf.
- **Referenzbeispiel-Erweiterung:** `tests/bw-grundschule-beispiel/input/
  stammdaten.yaml` bekam einen kleinen, illustrativen `schueler:`/
  `gruppen:`-Block (4 Schüler der Klasse 1a, zwei Fachgruppen
  `Religion-ev-Kl1a`/`Religion-kath-Kl1a`). `dotnet run --project
  SchoolTestRunner -- run bw-grundschule-beispiel` lief live erneut
  durch: weiterhin PASS, 0 Kann-/Muss-Verstöße, identisches
  Lehrereinsatzplanung-Objective. Die einzige Output-Änderung war eine
  andere, gleichwertig optimale Klassenlehrer-Permutation (bekannte
  `numWorkers`-Nichtdeterminismus, siehe frühere Phasen) - ein direkter
  Beleg, dass die neuen Felder tatsächlich inert sind: geladen und
  validiert, aber von `Run.vb`/`Formatting.vb` nirgends gerendert oder
  solver-seitig verwendet.
- **`dotnet test TimetableCore.Tests`:** 4 neue Validierungstests (je ein
  Test pro Fehlerklasse plus ein sauberer Kontrollfall) und die um
  Schüler/Gruppen erweiterten `StammdatenTests.vb`-Rundtrip-Assertions,
  alle grün, 0 Regressionen gegenüber dem Phase-2.18-Stand.

## Nächster Schritt (bewusst zurückgestellt)

Dieses Datenmodell ist reine Grundlage - es entscheidet noch nicht, WANN
eine Gruppe stattfindet, erzeugt keine Sessions, prüft keine Kollisionen.
Der als nächstes sinnvolle Schritt (nicht Teil dieser Phase): eine
Solver-/Verifier-Erweiterung mindestens für den einfacheren Fall der
VOLLSTÄNDIGEN Klassen-Partition (Religion: jede Gruppe zusammen deckt
genau die ganze Klasse ab) - dafür genügt eine neue Solver.vb-Primitive
("parallele Sessions, die gemeinsam den Klassen-Slot belegen"), ohne dass
echte Schüler-Kollisionsprüfung nötig wäre. Fördergruppen (echte
Teilmengen, parallel zum Rest der Klasse) brauchen zusätzlich eine
Verifier-seitige Pro-Schüler-Kollisionsprüfung, die jetzt erstmals
möglich ist, weil die dafür nötigen Mitgliedschaftsdaten existieren.

## Definition of Done

- `dotnet test TimetableCore.Tests` bleibt vollständig grün, 0
  Regressionen, inkl. 4 neuer Validierungstests.
- YAML-Rundtrip für `schueler:`/`gruppen:` live bestätigt.
- `bw-grundschule-beispiel` läuft mit dem erweiterten `stammdaten.yaml`
  weiterhin sauber durch (PASS, 0 Verstöße).
- `StammdatenValidation.ValidateStammdaten` blockiert nachweislich:
  unbekannte Klassen-Referenz bei Schüler, unbekannte Schüler-ID-Referenz
  bei Gruppe, doppelte Schüler-ID.
- Kein Code in `Solver.vb`/`Verifier.vb`/`Lehrereinsatzplanung.vb`/
  `Kursblockung.vb` geändert - das Datenmodell ist in diesem Schritt rein
  additiv und wirkungslos für jeden bestehenden Solve-Pfad.
- Committet und gepusht auf `claude/qwen-3.5-sandbox-test-ubfhmo`.
