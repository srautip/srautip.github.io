# Beispiel-Import: Einschulungsliste

`einschulungsliste.csv` — 100 Kinder, so wie eine Liste aus dem
Sekretariat aussieht: Semikolon-getrennt, UTF-8 mit BOM, Kopfzeile.

**Die Daten sind synthetisch.** Namen, Telefonnummern und Geburtsdaten
sind aus festen Listen und dem laufenden Index erzeugt — kein
Personenbezug, kein Zufall (Datenhaltungs-Konzept 8.3, wie die übrigen
Fixtures).

## Wofür

Die Datei führt in *Klassenbildung → Kinder & Rahmen → Einfügen* jede
Spaltenrolle aus §9.1 einmal vor:

| Spalte | gedachte Rolle | Ergebnis |
|---|---|---|
| Nachname, Vorname | Nachname / Vorname | Klarname nach `mapping.json`, die Id vergibt die GUI |
| Geschlecht, Sprachfoerderung, Kann-Kind, Wohngebiet | Attribut | freies Vokabular, auf das Balance-Regeln zugreifen |
| Kita | Gruppe | sechs Gruppen `Kita-Sonnenblume` … — die Kita-Freundes-Cluster aus dem Konzept |
| Telefon, Geburtsdatum | *(verwerfen)* | bleiben draußen |

Die beiden letzten sind der eigentliche Punkt: **die Vorgabe ist
verwerfen.** Eine Klassenliste trägt Daten, die im Projekt nichts zu
suchen haben, und der Import soll sie nicht ungefragt mitnehmen. Nach
dem Übernehmen nennt der Bericht sie namentlich.

Das Vokabular entspricht dem der mitgelieferten
`input/klassenbildung.yaml` (`geschlecht`, `sprachfoerderung`,
`kann_kind`) — die importierten Kinder lassen sich also mit denselben
Balance-Regeln weiterverwenden. Die Schreibweise unterscheidet sich
bewusst (`Sprachfoerderung` statt `sprachfoerderung`): der Spaltenname
wird 1:1 zum Attributschlüssel, und genau das soll man beim Zuordnen
sehen.

Geprüft wird die Datei von
`TimetableGui.Tests/ImportZuordnungTests.DieEinschulungslisteDerGrundschuleLaeuftDurch`
— eine Beispieldatei, die nie durch den Importer läuft, ist eine
Behauptung.
