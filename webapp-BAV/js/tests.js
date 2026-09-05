/* =====================================================================
   Testfälle – Abschnitt 6 der Spezifikation und Hilfsfunktionen.
   Läuft im Browser (tests.html) und über node (node js/tests.js).
   ===================================================================== */
(function (global) {
  'use strict';

  var E = global.BAV.engine, C = global.BAV.config, X = global.BAV.examples, H = E.helpers;
  var tests = [];

  function test(name, fn) { tests.push({ name: name, fn: fn }); }
  function gleich(ist, soll, was) {
    if (ist !== soll) throw new Error((was || 'Wert') + ': erwartet ' + soll + ', erhalten ' + ist);
  }
  function nahebei(ist, soll, tol, was) {
    if (Math.abs(ist - soll) > (tol || 0.005)) {
      throw new Error((was || 'Wert') + ': erwartet ' + soll + ' ± ' + tol + ', erhalten ' + ist);
    }
  }
  function enthaelt(befunde, code) {
    if (!befunde.some(function (b) { return b.code === code; })) {
      throw new Error('Befund ' + code + ' fehlt (vorhanden: ' + befunde.map(function (b) { return b.code; }).join(', ') + ')');
    }
  }
  function enthaeltNicht(befunde, code) {
    if (befunde.some(function (b) { return b.code === code; })) {
      throw new Error('Befund ' + code + ' hätte nicht auftreten dürfen');
    }
  }

  function laufeBeispiel(id, aenderungen) {
    var f = X.BEISPIELE.filter(function (b) { return b.id === id; })[0];
    if (!f) throw new Error('Beispiel ' + id + ' nicht gefunden');
    var a = JSON.parse(JSON.stringify(f.anrecht));
    Object.keys(aenderungen || {}).forEach(function (k) { a[k] = aenderungen[k]; });
    var cfg = C.configFuerJahr(H.toDate(f.ehezeit.ende).getFullYear());
    return E.berechneBavAusgleich(a, f.ehezeit, cfg, {
      heute: f.ehezeit.pruefdatum,
      geburtsdatum: f.ehezeit.geburtsdatum_inhaber
    });
  }

  /* ---------------- Hilfsfunktionen ---------------- */
  test('monate zählt nur volle Monate', function () {
    gleich(H.monate('2005-05-01', '2020-04-30'), 179, 'volle Monate');
    gleich(H.monate('2020-01-15', '2020-02-14'), 0, 'angefangener Monat');
    gleich(H.monate('2020-01-15', '2020-02-15'), 1, 'voller Monat');
  });

  test('ehezeitMonate zählt Anfangs- und Endmonat mit (§ 3 Abs. 1 VersAusglG)', function () {
    gleich(H.ehezeitMonate('2005-05-01', '2020-04-30'), 180, 'Ehezeitmonate');
    gleich(H.ehezeitMonate('2010-03-01', '2010-03-31'), 1, 'ein Monat');
  });

  test('abweichung und nahe verhalten sich wie spezifiziert', function () {
    gleich(H.abweichung(0, 0), 0, 'beide 0');
    gleich(H.abweichung(0, 5), 1, 'soll 0');
    nahebei(H.abweichung(100, 105), 0.05, 1e-9, 'relative Abweichung');
    gleich(H.nahe(100, 100.4, 0.005), true, 'innerhalb Toleranz');
    gleich(H.nahe(100, 101, 0.005), false, 'außerhalb Toleranz');
  });

  test('Bagatellgrenzen folgen § 18 Abs. 3 VersAusglG', function () {
    var cfg = C.configFuerJahr(2020);
    nahebei(H.bagatellGrenze({ einheit: 'RENTE_MONAT' }, cfg), 31.85, 0.001, 'Rentengrenze');
    nahebei(H.bagatellGrenze({ einheit: 'KAPITALWERT' }, cfg), 3822, 0.001, 'Kapitalgrenze');
  });

  test('Barwertnäherung liefert plausible Faktoren', function () {
    var f = H.leibrentenBarwertfaktor(65, 2);
    if (f < 12 || f > 22) throw new Error('Leibrentenfaktor unplausibel: ' + f);
  });

  /* ---------------- Testfälle der Spezifikation ---------------- */
  test('Regelfall: saubere Halbteilung mit Kostenabzug', function () {
    var r = laufeBeispiel('standard');
    gleich(r.status, 'OK', 'Status');
    nahebei(r.anordnung.betrag, 9800, 0.005, 'Ausgleichsbetrag');
    nahebei(r.anordnung.kosten_abzug, 200, 0.005, 'Kostenabzug');
    gleich(r.anordnung.teilungsart, 'INTERN', 'Teilungsart');
  });

  test('Verfallbare Anwartschaft ist nicht ausgleichsreif', function () {
    var r = laufeBeispiel('verfallbar');
    gleich(r.status, 'SCHULDRECHTLICH_VORBEHALTEN', 'Status');
    enthaelt(r.befunde, 'GD05');
    enthaelt(r.befunde, 'GD06');
    gleich(r.anordnung, null, 'keine Anordnung');
    gleich(r.schritte.length, 1, 'Abbruch nach Schritt 1');
  });

  test('Direktzusage mit m/n-Abweichung wird erkannt', function () {
    var r = laufeBeispiel('mn-abweichung');
    gleich(r.status, 'FREIGABE_ERFORDERLICH', 'Status');
    enthaelt(r.befunde, 'EA04');
    /* Kosten in Euro werden bei Rentenanrechten über den Kapitalwert umgerechnet */
    nahebei(r.anordnung.kosten_abzug, 2.26, 0.01, 'Kostenabzug in Rente');
    nahebei(r.anordnung.betrag, 277.74, 0.01, 'Ausgleichsbetrag');
  });

  test('Gesunkenes Deckungskapital erzeugt EA08 und EA09', function () {
    var r = laufeBeispiel('deckungskapital-gesunken');
    gleich(r.status, 'FREIGABE_ERFORDERLICH', 'Status');
    enthaelt(r.befunde, 'EA08');
    enthaelt(r.befunde, 'EA09');
    enthaelt(r.befunde, 'TK02');
  });

  test('Bereits abgezogene Teilungskosten werden nicht doppelt abgezogen', function () {
    var r = laufeBeispiel('kosten-enthalten');
    gleich(r.status, 'OK', 'Status');
    enthaelt(r.befunde, 'HT01');
    enthaeltNicht(r.befunde, 'HT03');
    /* Das Ergebnis entspricht exakt dem Ausgleichswert der Auskunft */
    nahebei(r.anordnung.betrag, 14850, 0.005, 'Ausgleichsbetrag');
    nahebei(r.anordnung.kosten_abzug, 150, 0.005, 'einmaliger Kostenabzug');
  });

  test('Externe Teilung mit Transferverlust über 10 %', function () {
    var r = laufeBeispiel('transferverlust');
    gleich(r.status, 'FREIGABE_ERFORDERLICH', 'Status');
    gleich(r.anordnung.teilungsart, 'EXTERN', 'Teilungsart');
    enthaelt(r.befunde, 'TA04');
    gleich(r.anordnung.kosten_abzug, 0, 'keine Kosten bei externer Teilung');
  });

  test('Direktzusage knapp über der BBG muss intern geteilt werden', function () {
    var r = laufeBeispiel('bbg-grenze');
    gleich(r.anordnung.teilungsart, 'INTERN', 'Teilungsart');
    enthaelt(r.befunde, 'TA01');
    enthaelt(r.befunde, 'TA06');
    nahebei(r.anordnung.betrag, 82750, 0.005, 'Ausgleichsbetrag');
  });

  test('Knapp unter der BBG bleibt es bei der externen Teilung', function () {
    /* 165.000 € Kapitalwert -> Ausgleichswert 82.500 € <= BBG 82.800 € (2020) */
    var r = laufeBeispiel('bbg-grenze', {
      ehezeitanteil: 165000, ausgleichswert: 82500, korr_kapitalwert: 165000,
      gesamtanrecht: 397290
    });
    gleich(r.anordnung.teilungsart, 'EXTERN', 'Teilungsart');
    enthaeltNicht(r.befunde, 'TA01');
  });

  test('Geringfügiges Anrecht führt zum Bagatellvorschlag', function () {
    var r = laufeBeispiel('bagatelle');
    gleich(r.status, 'BAGATELL_VORSCHLAG', 'Status');
    enthaelt(r.befunde, 'BG01');
    gleich(r.schritte.length, 5, 'Abbruch nach Schritt 5');
  });

  /* ---------------- Gezielte Fehlerfälle ---------------- */
  test('Abweichende Ehezeitmonate brechen die Prüfung ab (GD02)', function () {
    var r = laufeBeispiel('standard', { ehezeit_monate_traeger: 179 });
    gleich(r.status, 'ABBRUCH', 'Status');
    enthaelt(r.befunde, 'GD02');
  });

  test('Nicht nachvollziehbarer Ausgleichswert erzeugt HT03', function () {
    var r = laufeBeispiel('standard', { ausgleichswert: 12000 });
    gleich(r.status, 'ABBRUCH', 'Status');
    enthaelt(r.befunde, 'HT03');
  });

  test('Ausgeübtes Kapitalwahlrecht führt aus dem Versorgungsausgleich heraus', function () {
    var r = laufeBeispiel('standard', { kapitalwahlrecht_ausgeuebt: true });
    gleich(r.status, 'NICHT_VA_SONDERN_ZUGEWINN', 'Status');
  });

  test('Untypische Bewertungsmethode erzeugt GD07', function () {
    var r = laufeBeispiel('standard', { bewertung: 'ZEITRATIERLICH', zusageart: 'LEISTUNGSZUSAGE' });
    enthaelt(r.befunde, 'GD07');
  });

  test('Beitragsorientierte Direktzusage darf unmittelbar bewertet werden', function () {
    var r = laufeBeispiel('mn-abweichung', {
      zusageart: 'BEITRAGSORIENTIERT', bewertung: 'UNMITTELBAR',
      deckungskapital_ezbeginn: 10000, deckungskapital_ezende: 72000
    });
    enthaeltNicht(r.befunde, 'GD07');
  });

  test('Unplausibler Rechnungszins und Barwertfaktor werden gemeldet', function () {
    var r = laufeBeispiel('mn-abweichung', { rechnungszins: 8.5, korr_kapitalwert: 15000 });
    enthaelt(r.befunde, 'KW05');
    enthaelt(r.befunde, 'KW03');
  });

  test('Überhöhte Teilungskosten werden beanstandet', function () {
    var r = laufeBeispiel('standard', { teilungskosten: 1200, ausgleichswert: 10000 });
    enthaelt(r.befunde, 'TK03');
    enthaelt(r.befunde, 'TK04');
  });

  test('Negativer Ehezeitanteil ist ein Fehler', function () {
    var r = laufeBeispiel('standard', { ehezeitanteil: -100, ausgleichswert: -50, korr_kapitalwert: -100 });
    gleich(r.status, 'ABBRUCH', 'Status');
    enthaelt(r.befunde, 'EA01');
  });

  test('Diensteintritt nach Ehezeitende ist ein Fehler', function () {
    var r = laufeBeispiel('mn-abweichung', { diensteintritt: '2021-01-01' });
    gleich(r.status, 'ABBRUCH', 'Status');
    enthaelt(r.befunde, 'EA05');
  });

  test('Laufende Leistung löst den Hinweis auf § 30 VersAusglG aus', function () {
    var r = laufeBeispiel('standard', { laufende_leistung: true });
    enthaelt(r.befunde, 'ER03');
  });

  test('Eingabedaten werden nicht verändert', function () {
    var f = X.BEISPIELE[0];
    var kopie = JSON.stringify(f.anrecht);
    laufeBeispiel('standard');
    gleich(JSON.stringify(f.anrecht), kopie, 'Anrecht unverändert');
  });

  /* ---------------- Erläuterungen zu den Stammdaten ---------------- */
  var HILFE = (global.BAV.hilfe && global.BAV.hilfe.STAMMDATEN) || {};

  /* Muss mit F_STAMM in ui.js übereinstimmen */
  var STAMMDATEN_FELDER = ['id', 'traeger', 'durchfuehrungsweg', 'zusageart', 'einheit',
    'bewertung', 'unverfallbar', 'unverfallbar_ab', 'laufende_leistung',
    'kapitalwahlrecht_ausgeuebt', 'traeger_teilungsordnung_vorhanden'];

  test('Jedes Stammdatenfeld hat eine Erläuterung', function () {
    STAMMDATEN_FELDER.forEach(function (k) {
      if (!HILFE[k]) throw new Error('Erläuterung fehlt für Feld ' + k);
    });
    Object.keys(HILFE).forEach(function (k) {
      if (STAMMDATEN_FELDER.indexOf(k) < 0) throw new Error('Erläuterung ohne zugehöriges Feld: ' + k);
    });
  });

  test('Jede Erläuterung ist inhaltlich vollständig', function () {
    Object.keys(HILFE).forEach(function (k) {
      var e = HILFE[k];
      if (!e.titel) throw new Error(k + ': Titel fehlt');
      if (!e.bedeutung || e.bedeutung.length < 40) throw new Error(k + ': Bedeutung fehlt oder ist zu knapp');
      if (!e.auspraegungen || !e.auspraegungen.length) throw new Error(k + ': keine Ausprägungen');
      e.auspraegungen.forEach(function (a) {
        if (!a.name || !a.text) throw new Error(k + ': Ausprägung ohne Bezeichnung oder Erläuterung');
      });
      if (!e.wirkung || !e.wirkung.length) throw new Error(k + ': Auswirkung auf die Berechnung fehlt');
    });
  });

  test('Ausprägungen der Auswahlfelder decken die Aufzählungstypen exakt ab', function () {
    [['durchfuehrungsweg', E.DURCHFUEHRUNGSWEG], ['zusageart', E.ZUSAGEART],
     ['einheit', E.EINHEIT], ['bewertung', E.BEWERTUNG]].forEach(function (paar) {
      var feld = paar[0], enumWerte = Object.keys(paar[1]);
      var dokumentiert = HILFE[feld].auspraegungen.map(function (a) { return a.wert; });
      enumWerte.forEach(function (w) {
        if (dokumentiert.indexOf(w) < 0) throw new Error(feld + ': Ausprägung ' + w + ' ist nicht erläutert');
      });
      dokumentiert.forEach(function (w) {
        if (enumWerte.indexOf(w) < 0) throw new Error(feld + ': erläuterte Ausprägung ' + w + ' kennt die Engine nicht');
      });
    });
  });

  test('Alle in den Erläuterungen genannten Befundcodes existieren', function () {
    Object.keys(HILFE).forEach(function (k) {
      var text = JSON.stringify(HILFE[k]);
      var codes = text.match(/\b(GD|EA|KW|HT|BG|TA|TK|ER)\d{2}\b/g) || [];
      codes.forEach(function (code) {
        if (!E.KATALOG[code]) throw new Error(k + ': unbekannter Befundcode ' + code);
      });
    });
  });

  /* ---------------- Ausführung ---------------- */
  function run() {
    return tests.map(function (t) {
      try { t.fn(); return { name: t.name, ok: true }; }
      catch (e) { return { name: t.name, ok: false, fehler: e.message }; }
    });
  }

  global.BAV.tests = { run: run, anzahl: tests.length };

  /* Direktaufruf über node */
  if (typeof module !== 'undefined' && require.main === module) {
    var erg = run();
    erg.forEach(function (r) {
      console.log((r.ok ? '  ok  ' : 'FEHLER') + ' | ' + r.name + (r.ok ? '' : '\n         ' + r.fehler));
    });
    var fehler = erg.filter(function (r) { return !r.ok; }).length;
    console.log('\n' + (erg.length - fehler) + ' von ' + erg.length + ' Tests bestanden.');
    process.exit(fehler ? 1 : 0);
  }
})(typeof window !== 'undefined' ? window : this);
