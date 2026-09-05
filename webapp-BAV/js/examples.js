/* =====================================================================
   Beispielfälle – entsprechen den Testfällen aus Abschnitt 6 der
   Spezifikation. Jeder Fall lässt sich in die Formulare laden.
   ===================================================================== */
(function (global) {
  'use strict';

  /* Gemeinsame Ehezeit: 01.05.2005 – 30.04.2020 = 180 Monate */
  var EHEZEIT = {
    beginn: '2005-05-01',
    ende: '2020-04-30',
    monate: 180,
    pruefdatum: '2021-03-01'
  };

  function fall(id, name, beschreibung, erwartung, geburtsdatum, anrecht) {
    return {
      id: id, name: name, beschreibung: beschreibung, erwartung: erwartung,
      ehezeit: { beginn: EHEZEIT.beginn, ende: EHEZEIT.ende, monate: EHEZEIT.monate,
                 pruefdatum: EHEZEIT.pruefdatum, geburtsdatum_inhaber: geburtsdatum },
      anrecht: anrecht
    };
  }

  function basisAnrecht(over) {
    var a = {
      id: 'A1', traeger: '',
      durchfuehrungsweg: 'DIREKTVERSICHERUNG', zusageart: 'BEITRAGSORIENTIERT',
      einheit: 'KAPITALWERT', bewertung: 'UNMITTELBAR',
      unverfallbar: true, unverfallbar_ab: '', laufende_leistung: false,
      kapitalwahlrecht_ausgeuebt: false,
      ehezeitanteil: null, ausgleichswert: null, ausgleichswert_kapital: null,
      korr_kapitalwert: null, rechnungszins: null, ehezeit_monate_traeger: 180,
      teilungskosten: 0, teilungsart_vorschlag: 'INTERN', auskunftsdatum: '2020-09-15',
      gesamtanrecht: null, diensteintritt: '', regelaltersgrenze: '',
      deckungskapital_ezbeginn: null, deckungskapital_ezende: null, vertragsbeginn: '',
      zustimmung_berechtigter: false, zielversorgung: '',
      zielversorgung_rentenfaktor: null, extern_erwartete_leistung: null,
      intern_vergleichswert: null,
      traeger_teilungsordnung_vorhanden: true
    };
    Object.keys(over || {}).forEach(function (k) { a[k] = over[k]; });
    return a;
  }

  var BEISPIELE = [
    fall('standard', 'Direktversicherung – unauffälliger Regelfall',
      'Kapitalwert-Anrecht, unmittelbare Bewertung, saubere Halbteilung, angemessene Teilungskosten.',
      'Status OK – Anordnung 9.800,00 € intern.',
      '1970-03-12',
      basisAnrecht({
        id: 'DV-100', traeger: 'Alpha Lebensversicherung AG',
        ehezeitanteil: 20000, ausgleichswert: 10000, korr_kapitalwert: 20000,
        rechnungszins: 1.75, teilungskosten: 400,
        deckungskapital_ezbeginn: 4000, deckungskapital_ezende: 24000,
        vertragsbeginn: '2001-07-01'
      })),

    fall('verfallbar', 'Verfallbare Anwartschaft',
      'Direktzusage, die zum Ehezeitende noch verfallbar war; laut Angabe ist die Unverfallbarkeit inzwischen eingetreten.',
      'Status SCHULDRECHTLICH_VORBEHALTEN – Abbruch nach Schritt 1 (GD05, GD06).',
      '1978-11-04',
      basisAnrecht({
        id: 'DZ-200', traeger: 'Beta Maschinenbau GmbH',
        durchfuehrungsweg: 'DIREKTZUSAGE', zusageart: 'LEISTUNGSZUSAGE',
        einheit: 'RENTE_MONAT', bewertung: 'ZEITRATIERLICH',
        unverfallbar: false, unverfallbar_ab: '2020-12-31',
        ehezeitanteil: 210, ausgleichswert: 105, korr_kapitalwert: 24000,
        rechnungszins: 3.0, teilungskosten: 350,
        gesamtanrecht: 480, diensteintritt: '2016-02-01', regelaltersgrenze: '2045-11-30',
        deckungskapital_ezbeginn: null, deckungskapital_ezende: null
      })),

    fall('mn-abweichung', 'Direktzusage mit m/n-Abweichung',
      'Zeitratierliche Bewertung: Der ausgewiesene Ehezeitanteil liegt spürbar über der Nachrechnung nach § 2 BetrAVG.',
      'Status FREIGABE_ERFORDERLICH – EA04 (rund 7 % Abweichung), Kostenumrechnung auf Rentenbasis.',
      '1965-07-15',
      basisAnrecht({
        id: 'DZ-300', traeger: 'Gamma Werke AG',
        durchfuehrungsweg: 'DIREKTZUSAGE', zusageart: 'LEISTUNGSZUSAGE',
        einheit: 'RENTE_MONAT', bewertung: 'ZEITRATIERLICH',
        ehezeitanteil: 560, ausgleichswert: 280, korr_kapitalwert: 62000,
        rechnungszins: 3.0, teilungskosten: 500,
        gesamtanrecht: 1200, diensteintritt: '1998-04-01', regelaltersgrenze: '2032-07-31',
        teilungsart_vorschlag: 'INTERN', traeger_teilungsordnung_vorhanden: true
      })),

    fall('deckungskapital-gesunken', 'Direktversicherung mit gesunkenem Deckungskapital',
      'Das Deckungskapital ist in der Ehezeit gefallen, der Träger weist dennoch den vollen Endwert als Ehezeitanteil aus.',
      'Status FREIGABE_ERFORDERLICH – EA08 und EA09.',
      '1972-01-30',
      basisAnrecht({
        id: 'DV-400', traeger: 'Delta Versicherung a. G.',
        ehezeitanteil: 15500, ausgleichswert: 7750, korr_kapitalwert: 15500,
        rechnungszins: 2.25, teilungskosten: 0,
        deckungskapital_ezbeginn: 18000, deckungskapital_ezende: 15500,
        vertragsbeginn: '1999-04-01'
      })),

    fall('kosten-enthalten', 'Teilungskosten bereits im Ausgleichswert',
      'Pensionskasse: Der Träger hat die Teilungskosten schon vor der Halbteilung abgezogen.',
      'Status OK – HT01 erkennt den Vorabzug, kein doppelter Abzug. Anordnung 14.850,00 €.',
      '1974-06-08',
      basisAnrecht({
        id: 'PK-500', traeger: 'Epsilon Pensionskasse VVaG',
        durchfuehrungsweg: 'PENSIONSKASSE',
        ehezeitanteil: 30000, ausgleichswert: 14850, korr_kapitalwert: 30000,
        rechnungszins: 1.25, teilungskosten: 300,
        deckungskapital_ezbeginn: 5000, deckungskapital_ezende: 35000
      })),

    fall('transferverlust', 'Externe Teilung mit Transferverlust über 10 %',
      'Direktzusage unterhalb der Beitragsbemessungsgrenze: einseitig externe Teilung möglich, die Zielversorgung liefert aber deutlich weniger als die interne Teilung.',
      'Status FREIGABE_ERFORDERLICH – TA04 (rund 18 % Transferverlust).',
      '1966-01-20',
      basisAnrecht({
        id: 'DZ-600', traeger: 'Zeta Industrie SE',
        durchfuehrungsweg: 'DIREKTZUSAGE', zusageart: 'LEISTUNGSZUSAGE',
        einheit: 'RENTE_MONAT', bewertung: 'ZEITRATIERLICH',
        ehezeitanteil: 800, ausgleichswert: 400, korr_kapitalwert: 90000,
        rechnungszins: 3.25, teilungskosten: 0,
        gesamtanrecht: 1770, diensteintritt: '2000-01-01', regelaltersgrenze: '2033-01-31',
        teilungsart_vorschlag: 'EXTERN', zustimmung_berechtigter: false,
        zielversorgung: 'Versorgungsausgleichskasse',
        zielversorgung_rentenfaktor: 55, intern_vergleichswert: 300
      })),

    fall('bbg-grenze', 'Direktzusage knapp über der Beitragsbemessungsgrenze',
      'Der Ausgleichswert als Kapital übersteigt die BBG um 200 € – die einseitige externe Teilung nach § 17 VersAusglG scheidet aus.',
      'Status FREIGABE_ERFORDERLICH – Teilungsart INTERN, TA01 und TA06.',
      '1968-09-02',
      basisAnrecht({
        id: 'DZ-700', traeger: 'Eta Holding AG',
        durchfuehrungsweg: 'DIREKTZUSAGE', zusageart: 'LEISTUNGSZUSAGE',
        einheit: 'KAPITALWERT', bewertung: 'ZEITRATIERLICH',
        ehezeitanteil: 166000, ausgleichswert: 83000, korr_kapitalwert: 166000,
        rechnungszins: 2.5, teilungskosten: 500,
        gesamtanrecht: 399700, diensteintritt: '1995-01-01', regelaltersgrenze: '2030-12-31',
        teilungsart_vorschlag: 'EXTERN', zustimmung_berechtigter: false,
        traeger_teilungsordnung_vorhanden: false
      })),

    fall('bagatelle', 'Geringfügiges Anrecht',
      'Kleine Direktversicherung, deren Ausgleichswert unter 120 % der monatlichen Bezugsgröße liegt.',
      'Status BAGATELL_VORSCHLAG – Abbruch nach Schritt 5 (BG01).',
      '1980-05-19',
      basisAnrecht({
        id: 'DV-800', traeger: 'Theta Direkt Leben AG',
        ehezeitanteil: 6800, ausgleichswert: 3400, korr_kapitalwert: 6800,
        rechnungszins: 1.5, teilungskosten: 150,
        deckungskapital_ezbeginn: 200, deckungskapital_ezende: 7000
      }))
  ];

  global.BAV = global.BAV || {};
  global.BAV.examples = { EHEZEIT: EHEZEIT, BEISPIELE: BEISPIELE, basisAnrecht: basisAnrecht };
})(typeof window !== 'undefined' ? window : this);
