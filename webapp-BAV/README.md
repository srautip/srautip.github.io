# Versorgungsausgleich bAV – Webanwendung

Eigenständige Webanwendung (HTML + JavaScript, ohne Build-Schritt und ohne
Abhängigkeiten) zur schrittweisen Prüfung und Berechnung des Versorgungsausgleichs
für Anrechte der betrieblichen Altersversorgung. Umgesetzt ist die Spezifikation
„Versorgungsausgleich – Betriebliche Altersversorgung (bAV), ein Ehegatte".

Alle Berechnungen laufen im Browser; es werden keine Daten übertragen. Der
Bearbeitungsstand wird im `localStorage` des Browsers gehalten und lässt sich als
JSON exportieren und wieder importieren.

## Aufruf

`index.html` direkt im Browser öffnen oder über einen beliebigen statischen
Webserver ausliefern. `tests.html` führt die Testfälle im Browser aus.

```
npx http-server -p 8899 .      # optional
node js/tests.js               # dieselben Tests auf der Kommandozeile
```

## Aufbau

| Datei | Inhalt |
|---|---|
| `index.html` | Formulare und Ergebnisbereich |
| `css/styles.css` | Gestaltung, helles und dunkles Farbschema, Druckansicht |
| `js/engine.js` | Prüf- und Berechnungslogik, ohne DOM-Zugriffe |
| `js/config.js` | Bezugsgröße, Beitragsbemessungsgrenze und Prüfgrenzen je Jahr |
| `js/examples.js` | Beispielfälle (entsprechen den Testfällen der Spezifikation) |
| `js/hilfe.js` | Erläuterungen zu den Eingabefeldern für den Schulungseinsatz |
| `js/ui.js` | Formularaufbau, Zustand, schrittweise Ergebnisdarstellung |
| `js/tests.js`, `tests.html` | Testfälle für Browser und Kommandozeile |

Die Engine ist bewusst von der Oberfläche getrennt: `berechneBavAusgleich(anrecht,
ehezeit, config, optionen)` verändert die Eingabedaten nicht und liefert

```js
{
  status,     // OK | FREIGABE_ERFORDERLICH | ABBRUCH | BAGATELL_VORSCHLAG |
              // SCHULDRECHTLICH_VORBEHALTEN | NICHT_VA_SONDERN_ZUGEWINN
  anordnung,  // Tenorvorschlag oder null
  befunde,    // alle Befunde mit Code, Schweregrad, Rechtsgrundlage, Erläuterung
  schritte    // je Schritt: Beschreibung, Rechenzeilen, Befunde, Zwischenergebnis
}
```

## Eingabemasken

1. **Verfahren und Ehezeit** – Ehezeitbeginn und -ende, Monatszahl (wird
   vorgeschlagen), Geburtsdatum des Inhabers, Prüfdatum für die Fristprüfungen.
2. **Anrechte des Inhabers** – beliebig viele Anrechte, jedes wird eigenständig
   geprüft; „Alle Anrechte auswerten" liefert eine Übersicht.
3. **Stammdaten** – Durchführungsweg, Zusageart, Einheit, Bewertungsmethode,
   Verfallbarkeit, Leistungsbezug, Kapitalwahlrecht, Teilungsordnung.
4. **Trägerauskunft** – Ehezeitanteil, Ausgleichswert, korrespondierender
   Kapitalwert, Rechnungszins, Ehezeitmonate, Teilungskosten, Teilungsvorschlag.
5. **Zeitratierliche Bewertung** – Gesamtanrecht, Diensteintritt, feste Altersgrenze.
6. **Unmittelbare Bewertung** – Deckungskapital zu Ehezeitbeginn und -ende,
   Vertragsbeginn.
7. **Externe Teilung** – Zustimmung, Zielversorgung, Rentenfaktor bzw. erwartete
   Leistung, Vergleichswert der internen Teilung.
8. **Konfiguration** – alle Grenzwerte, vorbelegt nach dem Jahr des Ehezeitendes
   und frei überschreibbar.

## Einsatz in der Schulung

Die Anwendung ist durchgängig mit Erläuterungen unterlegt. Ein kleiner
(i)-Knopf steht an vier Stellen: hinter jedem Feld in Abschnitt 3 (Stammdaten
des Anrechts) und Abschnitt 4 (Angaben der Trägerauskunft), in jedem
Schrittkopf der Auswertung, an jedem Befund und an der Statuszeile.

**Eingabefelder** – das Fenster zeigt

* der fachlichen Bedeutung des Feldes und der Quelle der Angabe,
* **allen** Ausprägungen einzeln erläutert – bei Auswahlfeldern etwa die fünf
  Durchführungswege mit ihren unterschiedlichen Bewertungsmethoden und
  Grenzwerten, bei Zahlenfeldern die Wertebereiche, die zu einem Befund führen
  (Teilungskosten von null über die übliche Pauschale bis zum negativen Wert),
* der konkreten Auswirkung auf die Berechnung, mit Angabe des betroffenen Schritts,
* den Befunden, die dieses Feld auslösen kann,
* einem Praxishinweis auf typische Fehler und Streitpunkte.

**Prüfschritte** – Zweck des Schritts, was konkret geprüft wird, wie es je nach
Ergebnis weitergeht (welcher Ausgang zum Abbruch führt und welcher nicht), alle
Befunde, die dieser Schritt erzeugen kann, mit Titel und Schweregrad, sowie ein
Praxishinweis.

**Befunde** – die fachliche Bedeutung, warum der Befund erscheint, und vor allem
**was jetzt zu tun ist**: konkrete Handlungsschritte von der Rückfrage beim
Träger über die Korrektur der Erfassung bis zur Entscheidung des Gerichts. Wo es
trägt, kommt eine Vertiefung hinzu, etwa zur Entscheidung des BVerfG zum
Transferverlust.

**Statuszeile** – die drei Befundarten ERROR, WARN und INFO und die sechs
Ergebnisstatus, jeweils mit der Konsequenz für die weitere Bearbeitung.

Alle Fenster schließen über Esc, das Kreuz oder einen Klick auf den Hintergrund.
Der (i)-Knopf im Schrittkopf klappt den Schritt nicht zu.

Sämtliche Inhalte stehen in `js/hilfe.js`, getrennt von der Oberfläche:
`STAMMDATEN` und `TRAEGERAUSKUNFT` (über `FELDER` gemeinsam nachschlagbar),
`SCHRITTE`, `BEFUNDE` und `SCHWEREGRADE`. Ein weiteres Feld wird dokumentiert,
indem dort ein Eintrag unter dem Feldschlüssel ergänzt wird – der (i)-Knopf
erscheint dann automatisch, auch in den übrigen Formularabschnitten.

Acht Testfälle sichern ab, dass die Erläuterungen nicht von der Engine abdriften:

* Jedes Feld beider Formularabschnitte ist dokumentiert, kein Eintrag ist verwaist.
* Jeder Feldeintrag ist inhaltlich vollständig.
* Die Ausprägungen der Auswahlfelder entsprechen den Aufzählungstypen aus
  `engine.js` exakt – in beide Richtungen geprüft.
* Jeder genannte Befundcode existiert im Katalog.
* Jeder der 46 Befunde des Katalogs hat Ursachen und Handlungsschritte, und kein
  dokumentierter Befund existiert ohne Gegenstück im Katalog.
* Alle acht Prüfschritte sind vollständig erläutert.
* Jeder Befund ist genau einem Schritt zugeordnet – keiner fehlt, keiner doppelt.
* Alle drei Befundarten und alle sechs Ergebnisstatus sind erläutert.

## Prüf- und Rechenschritte

Die Ergebnisdarstellung lässt sich Schritt für Schritt aufdecken. Jeder Schritt
nennt Rechtsgrundlage, fachliche Beschreibung, alle Zwischenwerte mit Rechenweg,
die ausgelösten Befunde und ein Zwischenergebnis.

1. **Grunddaten und Ausgleichsreife** (GD01–GD08) – Pflichtfelder, Ehezeitabgleich,
   Aktualität der Auskunft, Unverfallbarkeit, Passung von Durchführungsweg und
   Bewertungsmethode. Verfallbare Anwartschaft und ausgeübtes Kapitalwahlrecht
   beenden die Prüfung.
2. **Ehezeitanteil** (EA01–EA10) – Nachrechnung m/n bei zeitratierlicher Bewertung,
   Abgleich mit dem Zuwachs des Deckungskapitals bei unmittelbarer Bewertung.
3. **Kapitalwert und Rechnungszins** (KW01–KW06) – Barwertfaktor, Näherungsbarwert,
   Plausibilität des Zinses.
4. **Halbteilung** (HT01–HT03) – Ausgleichswert gegen den halben Ehezeitanteil,
   Erkennung eines bereits vorgenommenen Kostenabzugs auf Renten- oder Kapitalebene.
5. **Geringfügigkeit** (BG01) – § 18 Abs. 2, 3 VersAusglG.
6. **Teilungsart** (TA01–TA06) – §§ 10, 14, 17 VersAusglG, Transferverlustprüfung
   nach BVerfG 26.05.2020 – 1 BvL 5/18.
7. **Teilungskosten** (TK01–TK07) – § 13 VersAusglG, Umrechnung von Eurokosten auf
   Rentenanrechte über den Kapitalwert.
8. **Ergebnis** (ER01–ER05) – Schlüssigkeitsprüfung und Tenorvorschlag.

Befunde tragen die Schweregrade `ERROR` (Abbruch), `WARN` (manuelle Freigabe) und
`INFO` (Hinweis). Der Status `FREIGABE_ERFORDERLICH` bedeutet, dass die Rechnung
schlüssig ist, aber jeder WARN-Befund eine protokollierte Freigabe braucht.

## Bewusste Abweichungen von der Spezifikation

Beide Abweichungen sind in der Oberfläche als Hinweis sichtbar.

* **Kostenabzug bei bereits kostenbereinigtem Ausgleichswert (Schritt 7).**
  Der Pseudocode liefert in `berechne_kostenabzug` den Wert 0, wenn der Träger die
  Kosten schon vor der Halbteilung abgezogen hat. Schritt 8 rechnet aber vom
  ungekürzten halben Ehezeitanteil (`ehezeitanteil / 2`), sodass der Kostenabzug
  vollständig verloren ginge und das Ergebnis über dem Ausgleichswert der Auskunft
  läge. Die Anwendung zieht den hälftigen Kostenanteil deshalb genau einmal ab; das
  Ergebnis entspricht dann dem kostenbereinigten Ausgleichswert des Trägers
  (Befund TK07).
* **Bagatellvorschlag bei vorliegenden Fehlerbefunden (Schritt 5).**
  Der Pseudocode kehrt bei Geringfügigkeit sofort zurück, auch wenn zuvor bereits
  `ERROR`-Befunde aufgetreten sind. Ein Ausschluss nach § 18 Abs. 2 VersAusglG setzt
  jedoch belastbare Zahlen voraus. Liegen Fehlerbefunde vor, wird BG01 gemeldet,
  aber nicht abgebrochen; die Prüfung endet regulär mit `ABBRUCH`.

## Ergänzungen gegenüber der Spezifikation

* `vertragsbeginn` als optionales Feld – der Pseudocode verweist in EA10 auf ein
  nicht definiertes `vertragsbeginn_vermutet`.
* `ausgleichswert_kapital` (für HT02), `regelaltersgrenze_alter` (Ersatz für ein
  fehlendes Geburtsdatum in KW04) und `traeger_teilungsordnung_vorhanden` (für TA06)
  sind im Pseudocode verwendet, aber nicht im Datenmodell aufgeführt.
* `erwartete_rente_zielversorgung` ist im Pseudocode nicht ausformuliert. Umgesetzt
  ist wahlweise eine direkte Angabe der erwarteten Leistung oder ein Rentenfaktor
  (Monatsrente je 10.000 € Kapital).
* `zins_max_direktzusage` wird in KW06 verwendet, fehlt aber in der Config-Liste;
  vorbelegt mit 3,5 % und überschreibbar.
* Der Näherungsbarwert für KW04 nutzt eine Gompertz-Makeham-Sterblichkeit statt einer
  Sterbetafel. Er ist bewusst nur ein Plausibilitätsmaß mit 15 % Toleranz und löst
  nie einen Fehler aus.

## Grenzen

Die Anwendung unterstützt die fachliche Vorprüfung von Trägerauskünften. Sie ersetzt
keine rechtliche Beratung und keine gerichtliche Entscheidung. Bezugsgröße und
Beitragsbemessungsgrenze sind vorbelegt und vor dem Einsatz gegen die amtlichen Werte
des jeweiligen Jahres zu prüfen; die Werte für 2026 sind ausdrücklich als vorläufig
gekennzeichnet. Die Kostenrechtsprechung ändert sich laufend – die Grenzwerte in
Abschnitt 8 sind deshalb frei einstellbar.
