# Beispiel-Import: bestehende Einteilung

`bestehende-einteilung.csv` — 116 Kinder der Klassenstufe 5, verteilt
auf 5a–5d. Semikolon-getrennt, UTF-8 mit BOM, Kopfzeile.

**Die Daten sind synthetisch** — Namen und Mailadressen aus festen
Listen und dem laufenden Index erzeugt, die Domain ist `.invalid`
(RFC 2606, kann per Definition nicht existieren).

## Wofür

Diese Datei führt den Fall aus §9.3 vor: eine Schule wird **unterjährig
eingeführt** und hat ihre Klasseneinteilung bereits.

| Spalte | gedachte Rolle | Ergebnis |
|---|---|---|
| Nachname, Vorname | Nachname / Vorname | Klarname nach `mapping.json` |
| Klasse | Klasse (als Fixierung) | je Kind eine `fixierung` auf seine Klasse |
| Religion | Gruppe | drei Gruppen `Religion-ev`, `Religion-kath`, `Religion-ethik` |
| Niveau | Attribut | G/M/E als Vokabular für Balance-Regeln |
| Mailadresse | *(verwerfen)* | bleibt draußen |

Der Kern ist die **Klasse-Spalte**. Sie enthält `5a`…`5d` und nicht
`1`…`4` — so schreiben es echte Listen. Der Importer löst das über die
Klassen-Labels auf; wer keine Labels gesetzt hat, muss Zahlen liefern
und bekommt sonst einen Hinweis statt stiller Auslassungen.

Damit die Auflösung greift, müssen die Labels **vorher** gesetzt sein:
*Klassenbildung → Kinder & Rahmen → Labels* auf `5a, 5b, 5c, 5d`. Ohne
sie entstehen keine Fixierungen, und der Bericht sagt es.

Anschließend ist der Ist-Zustand vollständig fixiert — der Ausgangspunkt
für den Board-Workflow: Pins lösen, einzelne Kinder verschieben, neu
rechnen.

Geprüft wird die Datei von
`TimetableGui.Tests/ImportZuordnungTests.DieBestehendeEinteilungDerGmsWirdZuFixierungen`.
