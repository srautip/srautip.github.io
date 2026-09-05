/* =====================================================================
   Oberfläche: Formulare, Zustand, schrittweise Ergebnisdarstellung
   ===================================================================== */
(function (global) {
  'use strict';

  var E = global.BAV.engine;
  var C = global.BAV.config;
  var X = global.BAV.examples;
  var H = E.helpers;
  var HILFE = (global.BAV.hilfe && global.BAV.hilfe.FELDER) || {};
  var SPEICHER = 'bav-va-state-v1';

  /* ------------------------------------------------------------------
     Beschriftungen der Aufzählungstypen
     ------------------------------------------------------------------ */
  var LABELS = {
    durchfuehrungsweg: {
      DIREKTZUSAGE: 'Direktzusage', UKASSE: 'Unterstützungskasse',
      PENSIONSKASSE: 'Pensionskasse', PENSIONSFONDS: 'Pensionsfonds',
      DIREKTVERSICHERUNG: 'Direktversicherung'
    },
    zusageart: {
      LEISTUNGSZUSAGE: 'Leistungszusage', BEITRAGSORIENTIERT: 'Beitragsorientierte Leistungszusage',
      BEITRAGSZUSAGE_MIT_MINDESTLEISTUNG: 'Beitragszusage mit Mindestleistung'
    },
    einheit: { RENTE_MONAT: 'Monatsrente', KAPITALWERT: 'Kapitalwert' },
    bewertung: { UNMITTELBAR: 'unmittelbar', ZEITRATIERLICH: 'zeitratierlich' },
    teilungsart: { INTERN: 'interne Teilung', EXTERN: 'externe Teilung' }
  };
  function label(feld, wert) {
    if (!wert) return '–';
    return (LABELS[feld] && LABELS[feld][wert]) || wert;
  }
  function optionenAus(feld) {
    return Object.keys(LABELS[feld]).map(function (k) { return { wert: k, text: LABELS[feld][k] }; });
  }

  var STATUS_TEXT = {
    OK: { badge: 'ok', text: 'Prüfung ohne Beanstandung' },
    FREIGABE_ERFORDERLICH: { badge: 'warn', text: 'Freigabe erforderlich' },
    ABBRUCH: { badge: 'error', text: 'Abbruch – Fehlerbefunde' },
    BAGATELL_VORSCHLAG: { badge: 'info', text: 'Geringfügiges Anrecht' },
    SCHULDRECHTLICH_VORBEHALTEN: { badge: 'info', text: 'Nicht ausgleichsreif' },
    NICHT_VA_SONDERN_ZUGEWINN: { badge: 'info', text: 'Kein Versorgungsausgleich' }
  };

  /* ------------------------------------------------------------------
     Felddefinitionen
     ------------------------------------------------------------------ */
  var F_EHEZEIT = [
    { key: 'beginn', typ: 'date', label: 'Ehezeitbeginn', hint: 'Erster Tag des Heiratsmonats.' },
    { key: 'ende', typ: 'date', label: 'Ehezeitende', hint: 'Letzter Tag des Monats vor Zustellung des Scheidungsantrags. Bestimmt Bewertungsstichtag und Konfigurationsjahr.' },
    { key: 'monate', typ: 'num', label: 'Ehezeitmonate', hint: 'Wird aus den Daten vorgeschlagen; Vergleichswert zur Trägerauskunft (Befund GD02).' },
    { key: 'geburtsdatum_inhaber', typ: 'date', label: 'Geburtsdatum des Inhabers', hint: 'Nur für die Barwert-Näherung (Befund KW04).' },
    { key: 'pruefdatum', typ: 'date', label: 'Prüfdatum', hint: 'Stichtag für Fristprüfungen (Befunde GD04, GD06). Vorbelegt mit dem heutigen Tag.' }
  ];

  var F_STAMM = [
    { key: 'id', typ: 'text', label: 'Kennung des Anrechts', hint: 'Frei wählbar, erscheint in Übersicht und Anordnung.' },
    { key: 'traeger', typ: 'text', label: 'Versorgungsträger', hint: 'Arbeitgeber bzw. Versorgungseinrichtung.' },
    { key: 'durchfuehrungsweg', typ: 'select', quelle: 'durchfuehrungsweg', label: 'Durchführungsweg',
      hint: 'Bestimmt Bewertungsmethode und den Grenzwert der externen Teilung.' },
    { key: 'zusageart', typ: 'select', quelle: 'zusageart', label: 'Zusageart',
      hint: 'Beitragsorientierte Zusagen dürfen auch unmittelbar bewertet werden.' },
    { key: 'einheit', typ: 'select', quelle: 'einheit', label: 'Einheit des Anrechts',
      hint: 'Monatsrente oder Kapitalwert – steuert alle Grenzwertvergleiche.' },
    { key: 'bewertung', typ: 'select', quelle: 'bewertung', label: 'Bewertungsmethode',
      hint: 'Unmittelbar (Deckungskapital) oder zeitratierlich (m/n nach § 2 BetrAVG).' },
    { key: 'unverfallbar', typ: 'check', label: 'Anwartschaft ist unverfallbar',
      hint: 'Nur unverfallbare Anrechte sind ausgleichsreif (§ 19 Abs. 2 Nr. 1 VersAusglG).' },
    { key: 'unverfallbar_ab', typ: 'date', label: 'Unverfallbar ab',
      hint: 'Nur bei noch verfallbarer Anwartschaft auszufüllen.' },
    { key: 'laufende_leistung', typ: 'check', label: 'Anrecht befindet sich im Leistungsbezug',
      hint: 'Löst die Prüfung des Leistungsschutzes nach § 30 VersAusglG aus.' },
    { key: 'kapitalwahlrecht_ausgeuebt', typ: 'check', label: 'Kapitalwahlrecht wurde ausgeübt',
      hint: 'Dann fällt das Anrecht in den Zugewinnausgleich, nicht in den Versorgungsausgleich.' },
    { key: 'traeger_teilungsordnung_vorhanden', typ: 'check', label: 'Teilungsordnung des Trägers liegt vor',
      hint: 'Voraussetzung der internen Teilung, insbesondere bei Direktzusagen (§ 10 VersAusglG).' }
  ];

  var F_AUSKUNFT = [
    { key: 'ehezeitanteil', typ: 'num', label: 'Ehezeitanteil', einheitAbh: true,
      hint: 'In der Ehezeit erworbener Teil des Anrechts.' },
    { key: 'ausgleichswert', typ: 'num', label: 'Ausgleichswert laut Träger', einheitAbh: true,
      hint: 'Wird gegen die Halbteilung geprüft (Schritt 4).' },
    { key: 'ausgleichswert_kapital', typ: 'num', label: 'Ausgleichswert als Kapital', unit: '€',
      hint: 'Nur bei Rentenanrechten und nur, wenn der Träger zusätzlich einen Kapitalbetrag nennt.' },
    { key: 'korr_kapitalwert', typ: 'num', label: 'Korrespondierender Kapitalwert', unit: '€',
      hint: 'Übertragungswert nach § 4 Abs. 5 BetrAVG bzw. Barwert (§ 47 VersAusglG).' },
    { key: 'rechnungszins', typ: 'num', label: 'Rechnungszins', unit: '% p. a.',
      hint: 'Vom Träger verwendeter Zins – wesentlicher Prüfgegenstand.' },
    { key: 'ehezeit_monate_traeger', typ: 'num', label: 'Ehezeitmonate laut Träger', unit: 'Monate',
      hint: 'Muss mit der Ehezeit des Verfahrens übereinstimmen.' },
    { key: 'teilungskosten', typ: 'num', label: 'Teilungskosten', unit: '€',
      hint: 'Kosten der internen Teilung nach § 13 VersAusglG.' },
    { key: 'teilungsart_vorschlag', typ: 'select', quelle: 'teilungsart', label: 'Vorschlag des Trägers',
      hint: 'Interne oder externe Teilung laut Auskunft.' },
    { key: 'auskunftsdatum', typ: 'date', label: 'Datum der Auskunft',
      hint: 'Grundlage der Aktualitätsprüfung (Befunde GD03, GD04).' }
  ];

  var F_ZEITRATIERLICH = [
    { key: 'gesamtanrecht', typ: 'num', label: 'Gesamtanrecht bei Altersgrenze', einheitAbh: true,
      hint: 'Volle Anwartschaft, die bis zur festen Altersgrenze erreichbar wäre.' },
    { key: 'diensteintritt', typ: 'date', label: 'Diensteintritt',
      hint: 'Beginn der anrechnungsfähigen Betriebszugehörigkeit.' },
    { key: 'regelaltersgrenze', typ: 'date', label: 'Feste Altersgrenze der Zusage',
      hint: 'Datum, zu dem die volle Anwartschaft erreicht wäre.' }
  ];

  var F_UNMITTELBAR = [
    { key: 'deckungskapital_ezbeginn', typ: 'num', label: 'Deckungskapital zu Ehezeitbeginn', unit: '€' },
    { key: 'deckungskapital_ezende', typ: 'num', label: 'Deckungskapital zu Ehezeitende', unit: '€' },
    { key: 'vertragsbeginn', typ: 'date', label: 'Vertragsbeginn',
      hint: 'Optional. Liegt er in der Ehezeit, ist das gesamte Anrecht Ehezeitanteil (Befund EA10).' }
  ];

  var F_EXTERN = [
    { key: 'zustimmung_berechtigter', typ: 'check', label: 'Berechtigter stimmt externer Teilung zu',
      hint: 'Mit Zustimmung ist die externe Teilung ohne Grenzwert möglich (§ 14 Abs. 2 Nr. 1 VersAusglG).' },
    { key: 'zielversorgung', typ: 'text', label: 'Zielversorgung',
      hint: 'Wahl des Berechtigten. Ohne Wahl greift die Auffangversorgung (§ 15 Abs. 5 VersAusglG).' },
    { key: 'zielversorgung_rentenfaktor', typ: 'num', label: 'Rentenfaktor der Zielversorgung', unit: '€ Monatsrente je 10.000 € Kapital',
      hint: 'Für die Transferverlustprüfung. Alternativ die erwartete Leistung direkt eintragen.' },
    { key: 'extern_erwartete_leistung', typ: 'num', label: 'Erwartete Leistung der Zielversorgung', unit: '€/Monat',
      hint: 'Hat Vorrang vor dem Rentenfaktor, wenn die Zielversorgung einen Wert nennt.' },
    { key: 'intern_vergleichswert', typ: 'num', label: 'Leistung bei interner Teilung', unit: '€/Monat',
      hint: 'Was der Berechtigte bei interner Teilung erhielte – Vergleichsmaßstab des BVerfG.' }
  ];

  var F_CONFIG = [
    { key: 'bezugsgroesse_monat', typ: 'num', label: 'Monatliche Bezugsgröße', unit: '€', hint: '§ 18 Abs. 1 SGB IV.' },
    { key: 'bbg_grv_jahr', typ: 'num', label: 'Beitragsbemessungsgrenze GRV', unit: '€/Jahr', hint: 'Grenzwert des § 17 VersAusglG.' },
    { key: 'extern_rente_faktor', typ: 'num', label: 'Grenzwert externe Teilung (Rente)', unit: '× Bezugsgröße', hint: '§ 14 Abs. 2 Nr. 2 VersAusglG, üblich 0,02.' },
    { key: 'extern_kapital_faktor', typ: 'num', label: 'Grenzwert externe Teilung (Kapital)', unit: '× Bezugsgröße', hint: 'Üblich 2,40.' },
    { key: 'bagatell_rente_faktor', typ: 'num', label: 'Bagatellgrenze (Rente)', unit: '× Bezugsgröße', hint: '§ 18 Abs. 3 VersAusglG, üblich 0,01.' },
    { key: 'bagatell_kapital_faktor', typ: 'num', label: 'Bagatellgrenze (Kapital)', unit: '× Bezugsgröße', hint: 'Üblich 1,20.' },
    { key: 'zins_min', typ: 'num', label: 'Rechnungszins minimal', unit: '%' },
    { key: 'zins_max', typ: 'num', label: 'Rechnungszins maximal', unit: '%' },
    { key: 'zins_max_direktzusage', typ: 'num', label: 'Zinsgrenze Direktzusage/U-Kasse', unit: '%', hint: 'Vergleichsmaßstab für Befund KW06.' },
    { key: 'kosten_quote_max', typ: 'num', label: 'Höchste Kostenquote', unit: 'Anteil', hint: 'Rechtsprechung: 2–3 %, also 0,03.' },
    { key: 'kosten_abs_min', typ: 'num', label: 'Kosten-Mindestpauschale', unit: '€' },
    { key: 'kosten_abs_max', typ: 'num', label: 'Kosten-Obergrenze', unit: '€' },
    { key: 'transferverlust_max', typ: 'num', label: 'Zulässiger Transferverlust', unit: 'Anteil', hint: 'BVerfG: rund 0,10.' },
    { key: 'toleranz_relativ', typ: 'num', label: 'Rechentoleranz', unit: 'Anteil', hint: 'Üblich 0,005.' },
    { key: 'barwert_faktor_min', typ: 'num', label: 'Barwertfaktor minimal', unit: 'Jahresrenten' },
    { key: 'barwert_faktor_max', typ: 'num', label: 'Barwertfaktor maximal', unit: 'Jahresrenten' }
  ];

  /* ------------------------------------------------------------------
     Zustand
     ------------------------------------------------------------------ */
  var state = null;
  var letztesErgebnis = null;
  var sichtbareSchritte = 0;

  function heuteIso() {
    var d = new Date();
    return d.getFullYear() + '-' + ('0' + (d.getMonth() + 1)).slice(-2) + '-' + ('0' + d.getDate()).slice(-2);
  }

  function neuerZustand() {
    var ehezeit = { beginn: '', ende: '', monate: null, geburtsdatum_inhaber: '', pruefdatum: heuteIso() };
    return {
      ehezeit: ehezeit,
      anrechte: [X.basisAnrecht({ id: 'A1' })],
      aktiv: 0,
      cfg: C.configFuerJahr(new Date().getFullYear()),
      cfgJahrGeladen: new Date().getFullYear(),
      alleSchritte: false
    };
  }

  function speichern() {
    try { localStorage.setItem(SPEICHER, JSON.stringify(state)); } catch (e) { /* z. B. privater Modus */ }
  }
  function laden() {
    try {
      var roh = localStorage.getItem(SPEICHER);
      if (!roh) return null;
      var s = JSON.parse(roh);
      if (!s || !s.anrechte || !s.anrechte.length) return null;
      return s;
    } catch (e) { return null; }
  }

  function aktivesAnrecht() { return state.anrechte[state.aktiv]; }

  /* ------------------------------------------------------------------
     Kleine DOM-Helfer
     ------------------------------------------------------------------ */
  function el(tag, attrs, kinder) {
    var n = document.createElement(tag);
    Object.keys(attrs || {}).forEach(function (k) {
      if (k === 'class') n.className = attrs[k];
      else if (k === 'text') n.textContent = attrs[k];
      else if (k === 'html') n.innerHTML = attrs[k];
      else if (attrs[k] !== null && attrs[k] !== undefined && attrs[k] !== false) n.setAttribute(k, attrs[k]);
    });
    (kinder || []).forEach(function (k) { if (k) n.appendChild(k); });
    return n;
  }
  function $(id) { return document.getElementById(id); }

  function numToInput(v) {
    if (v === null || v === undefined || v === '') return '';
    return String(v).replace('.', ',');
  }
  function inputToNum(s) {
    if (s === null || s === undefined) return null;
    s = String(s).trim().replace(/\./g, '').replace(',', '.');
    if (s === '') return null;
    var n = Number(s);
    return isNaN(n) ? null : n;
  }

  function einheitSuffix(a) {
    return a.einheit === E.EINHEIT.RENTE_MONAT ? '€/Monat' : '€';
  }

  /* ------------------------------------------------------------------
     Formularaufbau
     ------------------------------------------------------------------ */
  /** Kleiner (i)-Knopf hinter der Feldbeschriftung, sofern eine Erläuterung hinterlegt ist. */
  function baueInfoKnopf(key) {
    if (!HILFE[key]) return null;
    var b = el('button', {
      type: 'button', class: 'info-btn',
      'aria-label': 'Erläuterung zu „' + HILFE[key].titel + '“ anzeigen',
      title: 'Erläuterung anzeigen', text: 'i'
    });
    b.addEventListener('click', function (ev) {
      ev.preventDefault();
      zeigeHilfe(key);
    });
    return b;
  }

  function baueBeschriftung(id, text, key) {
    return el('div', { class: 'label-row' }, [
      el('label', { for: id, text: text }),
      baueInfoKnopf(key)
    ]);
  }

  function baueFeld(def, objekt, aufAenderung) {
    var id = 'f-' + def.key + '-' + Math.random().toString(36).slice(2, 7);
    var wrap, input;

    if (def.typ === 'check') {
      input = el('input', { type: 'checkbox', id: id });
      input.checked = !!objekt[def.key];
      input.addEventListener('change', function () {
        objekt[def.key] = input.checked;
        aufAenderung(true);
      });
      wrap = el('div', { class: 'field check wide' }, [
        input,
        el('div', { class: 'txt' }, [
          baueBeschriftung(id, def.label, def.key),
          def.hint ? el('span', { class: 'hint', text: def.hint }) : null
        ])
      ]);
      return wrap;
    }

    if (def.typ === 'select') {
      input = el('select', { id: id });
      optionenAus(def.quelle).forEach(function (o) {
        var opt = el('option', { value: o.wert, text: o.text });
        if (objekt[def.key] === o.wert) opt.selected = true;
        input.appendChild(opt);
      });
      input.addEventListener('change', function () {
        objekt[def.key] = input.value;
        aufAenderung(true);
      });
    } else if (def.typ === 'date') {
      input = el('input', { type: 'date', id: id });
      input.value = objekt[def.key] || '';
      input.addEventListener('change', function () {
        objekt[def.key] = input.value;
        aufAenderung(true);
      });
    } else if (def.typ === 'num') {
      input = el('input', { type: 'text', inputmode: 'decimal', id: id, autocomplete: 'off' });
      input.value = numToInput(objekt[def.key]);
      input.addEventListener('input', function () {
        objekt[def.key] = inputToNum(input.value);
        aufAenderung(false);
      });
    } else {
      input = el('input', { type: 'text', id: id, autocomplete: 'off' });
      input.value = objekt[def.key] || '';
      input.addEventListener('input', function () {
        objekt[def.key] = input.value;
        aufAenderung(false);
      });
    }

    var unit = def.unit;
    if (def.einheitAbh) unit = einheitSuffix(aktivesAnrecht());

    wrap = el('div', { class: 'field' }, [
      baueBeschriftung(id, def.label + (unit ? ' (' + unit + ')' : ''), def.key),
      input,
      def.hint ? el('span', { class: 'hint', text: def.hint }) : null
    ]);
    return wrap;
  }

  function baueFormular(containerId, defs, objekt, aufAenderung) {
    var c = $(containerId);
    c.innerHTML = '';
    defs.forEach(function (d) { c.appendChild(baueFeld(d, objekt, aufAenderung)); });
  }

  /* ------------------------------------------------------------------
     Reiterleiste der Anrechte
     ------------------------------------------------------------------ */
  function renderTabs() {
    var c = $('tabs-anrechte');
    c.innerHTML = '';
    state.anrechte.forEach(function (a, i) {
      var t = el('button', {
        type: 'button',
        class: 'tab' + (i === state.aktiv ? ' active' : ''),
        text: (a.id || ('Anrecht ' + (i + 1))) + (a.traeger ? ' · ' + a.traeger : '')
      });
      t.addEventListener('click', function () {
        state.aktiv = i;
        speichern();
        renderAlles();
      });
      c.appendChild(t);
    });
    $('btn-anrecht-loeschen').disabled = state.anrechte.length <= 1;
  }

  /* ------------------------------------------------------------------
     Vollständiges Rendern
     ------------------------------------------------------------------ */
  function aufAenderung(neuZeichnen) {
    pruefeJahreswechsel();
    speichern();
    if (neuZeichnen) renderAlles();
    else aktualisiereHinweise();
  }

  function pruefeJahreswechsel() {
    var ende = H.toDate(state.ehezeit.ende);
    if (!ende) return;
    var jahr = ende.getFullYear();
    if (jahr !== state.cfgJahrGeladen) {
      state.cfg = C.configFuerJahr(jahr);
      state.cfgJahrGeladen = jahr;
    }
  }

  function aktualisiereHinweise() {
    var ez = state.ehezeit;
    var teile = [];
    var vorschlag = H.ehezeitMonate(ez.beginn, ez.ende);
    if (vorschlag !== null) {
      teile.push('Aus den Daten berechnet: ' + vorschlag + ' Monate.');
      if (!H.istMonatsErster(ez.beginn)) teile.push('Der Ehezeitbeginn ist nicht der Monatserste – § 3 Abs. 1 VersAusglG prüfen.');
      if (!H.istMonatsLetzter(ez.ende)) teile.push('Das Ehezeitende ist nicht der Monatsletzte – § 3 Abs. 1 VersAusglG prüfen.');
      if (ez.monate !== null && ez.monate !== vorschlag) {
        teile.push('Eingetragen sind ' + ez.monate + ' Monate.');
      }
    } else {
      teile.push('Ehezeitbeginn und -ende erfassen, dann wird die Monatszahl vorgeschlagen.');
    }
    $('ehezeit-hinweis').textContent = teile.join(' ');

    var cfgHinweis = 'Werte für das Jahr des Ehezeitendes: ' + state.cfg.jahr + '.';
    if (state.cfg.hinweis) cfgHinweis += ' ' + state.cfg.hinweis;
    $('config-hinweis').textContent = cfgHinweis;

    var a = aktivesAnrecht();
    $('card-zeitratierlich').classList.toggle('muted', a.bewertung !== E.BEWERTUNG.ZEITRATIERLICH);
    $('card-unmittelbar').classList.toggle('muted', a.bewertung !== E.BEWERTUNG.UNMITTELBAR);
    $('ergebnis-anrecht').textContent = a.id ? (a.id + (a.traeger ? ' · ' + a.traeger : '')) : '';
  }

  function renderAlles() {
    var a = aktivesAnrecht();
    renderTabs();
    baueFormular('form-ehezeit', F_EHEZEIT, state.ehezeit, function (n) {
      if (state.ehezeit.monate === null) {
        var v = H.ehezeitMonate(state.ehezeit.beginn, state.ehezeit.ende);
        if (v !== null) state.ehezeit.monate = v;
      }
      aufAenderung(n);
    });
    baueFormular('form-stamm', F_STAMM, a, aufAenderung);
    baueFormular('form-auskunft', F_AUSKUNFT, a, aufAenderung);
    baueFormular('form-zeitratierlich', F_ZEITRATIERLICH, a, aufAenderung);
    baueFormular('form-unmittelbar', F_UNMITTELBAR, a, aufAenderung);
    baueFormular('form-extern', F_EXTERN, a, aufAenderung);
    baueFormular('form-config', F_CONFIG, state.cfg, aufAenderung);
    aktualisiereHinweise();
  }

  /* ------------------------------------------------------------------
     Infofenster – generischer Aufbau aus Abschnitten
     ------------------------------------------------------------------ */
  var HILFE_SCHRITTE = (global.BAV.hilfe && global.BAV.hilfe.SCHRITTE) || {};
  var HILFE_BEFUNDE = (global.BAV.hilfe && global.BAV.hilfe.BEFUNDE) || {};
  var HILFE_GRADE = (global.BAV.hilfe && global.BAV.hilfe.SCHWEREGRADE) || null;

  var dialog = null;

  function baueDialog() {
    if (dialog) return dialog;
    dialog = el('dialog', { class: 'info-dialog', id: 'hilfe-dialog' });
    dialog.appendChild(el('div', { class: 'info-inhalt' }));
    dialog.addEventListener('click', function (ev) {
      if (ev.target === dialog) dialog.close();
    });
    document.body.appendChild(dialog);
    return dialog;
  }

  /** Baut einen Abschnitt aus {titel, art, inhalt}. */
  function baueAbschnitt(a) {
    if (!a || !a.inhalt || (Array.isArray(a.inhalt) && !a.inhalt.length)) return null;
    var koerper;

    if (a.art === 'definitionen') {
      koerper = el('dl', { class: 'auspraegungen' });
      a.inhalt.forEach(function (d) {
        koerper.appendChild(el('dt', { text: d.name }));
        koerper.appendChild(el('dd', { text: d.text }));
      });
    } else if (a.art === 'befundliste') {
      koerper = el('ul', { class: 'befund-liste' });
      a.inhalt.forEach(function (eintrag) {
        var text = eintrag;
        if (E.KATALOG[eintrag]) {
          text = eintrag + ' – ' + E.KATALOG[eintrag].titel + ' (' + E.KATALOG[eintrag].sev + ')';
        }
        koerper.appendChild(el('li', { text: text }));
      });
    } else if (a.art === 'liste') {
      koerper = el('ul', {});
      a.inhalt.forEach(function (t) { koerper.appendChild(el('li', { text: t })); });
    } else if (a.art === 'praxis') {
      koerper = el('p', { class: 'praxis', text: a.inhalt });
    } else {
      koerper = el('p', { text: a.inhalt });
    }

    return el('section', {}, [el('h3', { text: a.titel }), koerper]);
  }

  /** daten = {titel, recht, badge, kopf:{label,text}, lead, abschnitte:[...]} */
  function oeffneHilfe(daten) {
    var d = baueDialog();
    var c = d.querySelector('.info-inhalt');
    c.innerHTML = '';

    var schliessen = el('button', { type: 'button', class: 'info-close', 'aria-label': 'Schließen', text: '×' });
    schliessen.addEventListener('click', function () { d.close(); });

    c.appendChild(el('header', { class: 'info-head' }, [
      el('div', {}, [
        el('div', { class: 'info-titelzeile' }, [
          daten.badge ? el('span', { class: 'badge ' + daten.badge.klasse, text: daten.badge.text }) : null,
          el('h2', { text: daten.titel })
        ]),
        daten.recht ? el('div', { class: 'legal', text: daten.recht }) : null
      ]),
      schliessen
    ]));

    var koerper = el('div', { class: 'info-body' });
    if (daten.kopf) {
      koerper.appendChild(el('p', { class: 'woher' }, [
        el('strong', { text: daten.kopf.label + ': ' }),
        el('span', { text: daten.kopf.text })
      ]));
    }
    if (daten.lead) koerper.appendChild(el('p', { class: 'lead', text: daten.lead }));
    (daten.abschnitte || []).forEach(function (a) {
      var s = baueAbschnitt(a);
      if (s) koerper.appendChild(s);
    });
    c.appendChild(koerper);

    if (typeof d.showModal === 'function') d.showModal();
    else d.setAttribute('open', 'open');
    schliessen.focus();
  }

  /* ---- Erläuterung zu einem Eingabefeld ---- */
  function zeigeHilfe(key) {
    var e = HILFE[key];
    if (!e) return;
    oeffneHilfe({
      titel: e.titel,
      recht: e.recht,
      kopf: e.woher ? { label: 'Quelle der Angabe', text: e.woher } : null,
      lead: e.bedeutung,
      abschnitte: [
        { titel: 'Ausprägungen', art: 'definitionen', inhalt: e.auspraegungen },
        { titel: 'Auswirkung auf die Berechnung', art: 'liste', inhalt: e.wirkung },
        { titel: 'Betroffene Befunde', art: 'liste', inhalt: e.befunde },
        { titel: 'Praxishinweis', art: 'praxis', inhalt: e.praxis }
      ]
    });
  }

  /* ---- Erläuterung zu einem Prüfschritt ---- */
  function zeigeHilfeSchritt(nr) {
    var e = HILFE_SCHRITTE[nr];
    if (!e) return;
    oeffneHilfe({
      titel: 'Schritt ' + nr + ' – ' + e.titel,
      recht: e.recht,
      lead: e.zweck,
      abschnitte: [
        { titel: 'Was geprüft wird', art: 'liste', inhalt: e.prueft },
        { titel: 'Wie es weitergeht', art: 'liste', inhalt: e.ergebnis },
        { titel: 'Mögliche Befunde in diesem Schritt', art: 'befundliste', inhalt: e.befunde },
        { titel: 'Praxishinweis', art: 'praxis', inhalt: e.praxis }
      ]
    });
  }

  /* ---- Erläuterung zu einem Befund ---- */
  function zeigeHilfeBefund(code) {
    var k = E.KATALOG[code];
    if (!k) return;
    var e = HILFE_BEFUNDE[code] || {};
    oeffneHilfe({
      titel: code + ' – ' + k.titel,
      recht: k.recht,
      badge: { klasse: k.sev.toLowerCase(), text: k.sev },
      kopf: { label: 'Befundart', text: befundartText(k.sev) },
      lead: k.erklaerung,
      abschnitte: [
        { titel: 'Warum der Befund erscheint', art: 'liste', inhalt: e.ursachen },
        { titel: 'Was jetzt zu tun ist', art: 'liste', inhalt: e.massnahmen },
        { titel: 'Vertiefung', art: 'praxis', inhalt: e.vertiefung }
      ]
    });
  }

  function befundartText(sev) {
    if (sev === 'ERROR') return 'Fehler – die Berechnung ist nicht tragfähig, die Prüfung endet mit Abbruch.';
    if (sev === 'WARN') return 'Warnung – die Anordnung ist möglich, braucht aber eine protokollierte Freigabe.';
    return 'Hinweis – kein Mangel, nur eine Feststellung.';
  }

  /* ---- Erläuterung zu Befundarten und Ergebnisstatus ---- */
  function zeigeHilfeStatus() {
    var e = HILFE_GRADE;
    if (!e) return;
    oeffneHilfe({
      titel: e.titel,
      recht: e.recht,
      lead: e.bedeutung,
      abschnitte: [
        { titel: 'Befundarten', art: 'definitionen', inhalt: e.arten },
        { titel: 'Ergebnisstatus', art: 'definitionen', inhalt: e.status },
        { titel: 'Praxishinweis', art: 'praxis', inhalt: e.praxis }
      ]
    });
  }

  /** Infoknopf für Schritte, Befunde und Statuszeile (verhindert das Zuklappen). */
  function baueInfoKnopfFuer(beschriftung, aktion) {
    var b = el('button', {
      type: 'button', class: 'info-btn',
      'aria-label': beschriftung, title: 'Erläuterung anzeigen', text: 'i'
    });
    b.addEventListener('click', function (ev) {
      ev.preventDefault();
      ev.stopPropagation();
      aktion();
    });
    return b;
  }

  /* ------------------------------------------------------------------
     Berechnung ausführen
     ------------------------------------------------------------------ */
  function fmtDatum(d) {
    d = H.toDate(d);
    if (!d) return '–';
    return ('0' + d.getDate()).slice(-2) + '.' + ('0' + (d.getMonth() + 1)).slice(-2) + '.' + d.getFullYear();
  }

  function rechne(anrecht) {
    return E.berechneBavAusgleich(anrecht, state.ehezeit, state.cfg, {
      heute: state.ehezeit.pruefdatum || heuteIso(),
      geburtsdatum: state.ehezeit.geburtsdatum_inhaber,
      fmtDatum: fmtDatum,
      label: label
    });
  }

  function berechnen() {
    letztesErgebnis = rechne(aktivesAnrecht());
    sichtbareSchritte = state.alleSchritte ? letztesErgebnis.schritte.length : 1;
    renderErgebnis();
    $('uebersicht-karte').hidden = true;
  }

  /* ------------------------------------------------------------------
     Ergebnisdarstellung
     ------------------------------------------------------------------ */
  function renderBefund(b) {
    return el('div', { class: 'befund ' + b.severity }, [
      el('div', { class: 'kopf' }, [
        el('span', { class: 'code', text: b.code }),
        el('span', { class: 'badge ' + b.severity.toLowerCase(), text: b.severity }),
        el('span', { class: 'titel', text: b.titel }),
        baueInfoKnopfFuer('Erläuterung zum Befund ' + b.code + ' anzeigen', function () {
          zeigeHilfeBefund(b.code);
        }),
        b.rechtsgrundlage ? el('span', { class: 'legal', text: b.rechtsgrundlage }) : null
      ]),
      b.text ? el('p', { class: 'text', text: b.text }) : null,
      b.erklaerung ? el('p', { class: 'erklaerung', text: b.erklaerung }) : null
    ]);
  }

  function renderSchritt(s, offen) {
    var kopf = el('div', { class: 'step-head' }, [
      el('span', { class: 'nr', text: String(s.nr) }),
      el('span', { class: 'titel', text: s.titel }),
      baueInfoKnopfFuer('Erläuterung zu Schritt ' + s.nr + ' anzeigen', function () {
        zeigeHilfeSchritt(s.nr);
      }),
      s.rechtsgrundlage ? el('span', { class: 'legal', text: s.rechtsgrundlage }) : null,
      el('span', { class: 'spacer', style: 'flex:1 1 auto;' }),
      s.befunde.length ? el('span', {
        class: 'badge ' + (s.befunde.some(function (b) { return b.severity === 'ERROR'; }) ? 'error'
          : s.befunde.some(function (b) { return b.severity === 'WARN'; }) ? 'warn' : 'info'),
        text: s.befunde.length + (s.befunde.length === 1 ? ' Befund' : ' Befunde')
      }) : null
    ]);

    var body = el('div', { class: 'step-body' });
    if (s.beschreibung) body.appendChild(el('p', { class: 'beschreibung', text: s.beschreibung }));

    if (s.zeilen.length) {
      var tab = el('table', { class: 'werte' });
      s.zeilen.forEach(function (z) {
        tab.appendChild(el('tr', {}, [
          el('th', { text: z.label }),
          el('td', { class: 'wert', text: z.wert }),
          el('td', { class: 'formel' }, [
            el('span', { text: z.formel || '' }),
            z.hinweis ? el('div', { style: 'margin-top:.15rem;font-style:italic;', text: z.hinweis }) : null
          ])
        ]));
      });
      body.appendChild(tab);
    }

    s.befunde.forEach(function (b) { body.appendChild(renderBefund(b)); });

    if (s.fazit) {
      body.appendChild(el('div', { class: 'fazit' }, [
        el('strong', { text: 'Zwischenergebnis: ' }),
        el('span', { text: s.fazit })
      ]));
    }

    if (!offen) body.style.display = 'none';
    kopf.addEventListener('click', function () {
      body.style.display = body.style.display === 'none' ? '' : 'none';
    });

    return el('div', { class: 'step s-' + s.status }, [kopf, body]);
  }

  function renderErgebnis() {
    var c = $('ergebnis-inhalt');
    c.innerHTML = '';
    var r = letztesErgebnis;
    if (!r) return;

    var alleSichtbar = sichtbareSchritte >= r.schritte.length;

    /* Statuszeile und Anordnung erst, wenn alle Schritte durchlaufen sind */
    if (alleSichtbar) {
      var st = STATUS_TEXT[r.status] || { badge: 'neutral', text: r.status };
      c.appendChild(el('div', { class: 'status-line' }, [
        el('span', { class: 'badge ' + st.badge, text: st.text }),
        el('span', { style: 'font-size:.8rem;color:var(--text-muted);', text: 'Status: ' + r.status }),
        baueInfoKnopfFuer('Erläuterung zu Befundarten und Ergebnisstatus anzeigen', zeigeHilfeStatus),
        el('span', { style: 'flex:1 1 auto;' }),
        el('span', {
          class: 'badge neutral',
          text: plural(zaehle(r.befunde, 'ERROR'), 'Fehler', 'Fehler') + ' · ' +
                plural(zaehle(r.befunde, 'WARN'), 'Warnung', 'Warnungen') + ' · ' +
                plural(zaehle(r.befunde, 'INFO'), 'Hinweis', 'Hinweise')
        })
      ]));

      if (r.anordnung) {
        var an = r.anordnung;
        c.appendChild(el('div', { class: 'anordnung' }, [
          el('div', { style: 'font-size:.78rem;color:var(--text-muted);text-transform:uppercase;letter-spacing:.05em;', text: 'Vorschlag für die Anordnung' }),
          el('div', { class: 'betrag', text: H.fmtWert(an.betrag, an.einheit) }),
          el('dl', {}, [
            el('dt', { text: 'Anrecht' }), el('dd', { text: (an.anrecht_id || '–') + (an.traeger ? ', ' + an.traeger : '') }),
            el('dt', { text: 'Teilungsart' }), el('dd', { text: label('teilungsart', an.teilungsart) }),
            el('dt', { text: 'Kostenabzug' }), el('dd', { text: H.fmtWert(an.kosten_abzug, an.einheit) }),
            el('dt', { text: 'Stichtag' }), el('dd', { text: fmtDatum(an.stichtag) }),
            el('dt', { text: 'Konfigurationsjahr' }), el('dd', { text: String(an.bezugsgroesse_jahr) })
          ])
        ]));
      } else {
        c.appendChild(el('div', { class: 'anordnung' }, [
          el('div', { style: 'font-size:.78rem;color:var(--text-muted);text-transform:uppercase;letter-spacing:.05em;', text: 'Ergebnis' }),
          el('div', { style: 'font-weight:600;margin-top:.2rem;', text: ergebnisErlaeuterung(r.status) })
        ]));
      }
    }

    /* Schritte */
    var steps = el('div', { class: 'steps' });
    r.schritte.forEach(function (s, i) {
      if (i < sichtbareSchritte) steps.appendChild(renderSchritt(s, true));
    });
    c.appendChild(steps);

    /* Navigation */
    var nav = el('div', { class: 'step-nav' });
    var weiter = el('button', { type: 'button', class: 'primary', text: 'Nächster Schritt' });
    weiter.disabled = alleSichtbar;
    weiter.addEventListener('click', function () {
      sichtbareSchritte = Math.min(sichtbareSchritte + 1, r.schritte.length);
      renderErgebnis();
      c.parentNode.scrollIntoView({ block: 'nearest' });
    });
    var alle = el('button', { type: 'button', text: 'Alle Schritte anzeigen' });
    alle.disabled = alleSichtbar;
    alle.addEventListener('click', function () {
      sichtbareSchritte = r.schritte.length;
      renderErgebnis();
    });
    var zurueck = el('button', { type: 'button', text: 'Ein Schritt zurück' });
    zurueck.disabled = sichtbareSchritte <= 1;
    zurueck.addEventListener('click', function () {
      sichtbareSchritte = Math.max(1, sichtbareSchritte - 1);
      renderErgebnis();
    });

    nav.appendChild(weiter);
    nav.appendChild(alle);
    nav.appendChild(zurueck);
    nav.appendChild(el('span', { class: 'fortschritt', text: 'Schritt ' + sichtbareSchritte + ' von ' + r.schritte.length }));

    var auto = el('label', { style: 'margin-left:auto;font-size:.8rem;display:flex;align-items:center;gap:.35rem;' });
    var chk = el('input', { type: 'checkbox' });
    chk.checked = !!state.alleSchritte;
    chk.addEventListener('change', function () {
      state.alleSchritte = chk.checked;
      speichern();
      if (chk.checked) { sichtbareSchritte = r.schritte.length; renderErgebnis(); }
    });
    auto.appendChild(chk);
    auto.appendChild(el('span', { text: 'immer alle Schritte zeigen' }));
    nav.appendChild(auto);

    c.appendChild(nav);

    /* Befundübersicht */
    if (alleSichtbar && r.befunde.length) {
      var ueb = el('div', { class: 'body', style: 'border-top:1px solid var(--border);' }, [
        el('div', { style: 'display:flex;align-items:center;gap:.4rem;margin:0 0 .5rem;' }, [
          el('h3', { style: 'margin:0;font-size:.88rem;', text: 'Alle Befunde im Überblick' }),
          baueInfoKnopfFuer('Erläuterung zu Befundarten und Ergebnisstatus anzeigen', zeigeHilfeStatus)
        ])
      ]);
      ['ERROR', 'WARN', 'INFO'].forEach(function (sev) {
        r.befunde.filter(function (b) { return b.severity === sev; })
                 .forEach(function (b) { ueb.appendChild(renderBefund(b)); });
      });
      if (zaehle(r.befunde, 'WARN') > 0) {
        ueb.appendChild(el('div', { class: 'notice', style: 'margin-top:.7rem;',
          text: 'Alle WARN-Befunde brauchen eine protokollierte Freigabe mit Begründung, bevor die Anordnung ergeht.' }));
      }
      c.appendChild(ueb);
    }
  }

  function ergebnisErlaeuterung(status) {
    switch (status) {
      case 'SCHULDRECHTLICH_VORBEHALTEN':
        return 'Keine Anordnung im Wertausgleich bei der Scheidung – Vorbehalt des schuldrechtlichen Ausgleichs (§§ 20 ff. VersAusglG).';
      case 'NICHT_VA_SONDERN_ZUGEWINN':
        return 'Das Anrecht ist dem Zugewinnausgleich zuzuordnen, nicht dem Versorgungsausgleich.';
      case 'BAGATELL_VORSCHLAG':
        return 'Vorschlag: Ausschluss des Anrechts wegen Geringfügigkeit (§ 18 Abs. 2 VersAusglG).';
      case 'ABBRUCH':
        return 'Abbruch – die Fehlerbefunde sind vor einer Anordnung zu klären.';
      default:
        return status;
    }
  }

  function plural(n, sg, pl) { return n + ' ' + (n === 1 ? sg : pl); }

  function zaehle(befunde, sev) {
    return befunde.filter(function (b) { return b.severity === sev; }).length;
  }

  /* ------------------------------------------------------------------
     Übersicht aller Anrechte
     ------------------------------------------------------------------ */
  function alleAuswerten() {
    var c = $('uebersicht-inhalt');
    c.innerHTML = '';
    var tab = el('table', { class: 'uebersicht' });
    tab.appendChild(el('tr', {}, [
      el('th', { text: 'Anrecht' }), el('th', { text: 'Träger' }), el('th', { text: 'Status' }),
      el('th', { text: 'Teilung' }), el('th', { text: 'Ausgleichsbetrag' }), el('th', { text: 'Befunde' })
    ]));

    state.anrechte.forEach(function (a, i) {
      var r = rechne(a);
      var st = STATUS_TEXT[r.status] || { badge: 'neutral', text: r.status };
      var zeileEl = el('tr', {}, [
        el('td', {}, [el('button', { type: 'button', class: 'small', text: a.id || ('Anrecht ' + (i + 1)) })]),
        el('td', { text: a.traeger || '–' }),
        el('td', {}, [el('span', { class: 'badge ' + st.badge, text: st.text })]),
        el('td', { text: r.anordnung ? label('teilungsart', r.anordnung.teilungsart) : '–' }),
        el('td', { class: 'num', text: r.anordnung ? H.fmtWert(r.anordnung.betrag, r.anordnung.einheit) : '–' }),
        el('td', { text: zaehle(r.befunde, 'ERROR') + '/' + zaehle(r.befunde, 'WARN') + '/' + zaehle(r.befunde, 'INFO') })
      ]);
      zeileEl.querySelector('button').addEventListener('click', function () {
        state.aktiv = i;
        speichern();
        renderAlles();
        berechnen();
      });
      tab.appendChild(zeileEl);
    });

    c.appendChild(tab);
    c.appendChild(el('p', { class: 'hint', style: 'margin:.6rem 0 0;font-size:.76rem;color:var(--text-muted);',
      text: 'Befunde in der Reihenfolge Fehler / Warnungen / Hinweise. Ein Klick auf die Kennung öffnet die Schrittfolge des Anrechts.' }));
    $('uebersicht-karte').hidden = false;
  }

  /* ------------------------------------------------------------------
     Beispiele, Import, Export
     ------------------------------------------------------------------ */
  function fuelleBeispielListe() {
    var sel = $('sel-beispiel');
    X.BEISPIELE.forEach(function (f) {
      sel.appendChild(el('option', { value: f.id, text: f.name }));
    });
    sel.addEventListener('change', function () {
      var f = X.BEISPIELE.filter(function (b) { return b.id === sel.value; })[0];
      if (!f) return;
      state.ehezeit = JSON.parse(JSON.stringify(f.ehezeit));
      state.anrechte = [JSON.parse(JSON.stringify(f.anrecht))];
      state.aktiv = 0;
      state.cfgJahrGeladen = null;
      pruefeJahreswechsel();
      speichern();
      renderAlles();
      berechnen();
      $('ergebnis-inhalt').insertBefore(
        el('div', { class: 'notice plain', style: 'margin:.8rem .95rem 0;' }, [
          el('strong', { text: f.name + ': ' }),
          el('span', { text: f.beschreibung + ' Erwartet: ' + f.erwartung })
        ]),
        $('ergebnis-inhalt').firstChild
      );
      sel.value = '';
    });
  }

  function exportieren() {
    var daten = JSON.stringify(state, null, 2);
    var blob = new Blob([daten], { type: 'application/json' });
    var url = URL.createObjectURL(blob);
    var a = el('a', { href: url, download: 'versorgungsausgleich-bav.json' });
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  }

  function importieren(datei) {
    var leser = new FileReader();
    leser.onload = function () {
      try {
        var s = JSON.parse(leser.result);
        if (!s.anrechte || !s.anrechte.length) throw new Error('Keine Anrechte enthalten.');
        state = s;
        if (!state.cfg) { state.cfgJahrGeladen = null; pruefeJahreswechsel(); }
        state.aktiv = Math.min(state.aktiv || 0, state.anrechte.length - 1);
        speichern();
        renderAlles();
        berechnen();
      } catch (e) {
        alert('Die Datei konnte nicht gelesen werden: ' + e.message);
      }
    };
    leser.readAsText(datei);
  }

  /* ------------------------------------------------------------------
     Start
     ------------------------------------------------------------------ */
  function init() {
    state = laden() || neuerZustand();
    if (!state.ehezeit.pruefdatum) state.ehezeit.pruefdatum = heuteIso();

    fuelleBeispielListe();
    renderAlles();

    $('btn-berechnen').addEventListener('click', berechnen);
    $('btn-alle').addEventListener('click', alleAuswerten);
    $('btn-drucken').addEventListener('click', function () { window.print(); });
    $('btn-export').addEventListener('click', exportieren);
    $('btn-import').addEventListener('click', function () { $('file-import').click(); });
    $('file-import').addEventListener('change', function (ev) {
      if (ev.target.files && ev.target.files[0]) importieren(ev.target.files[0]);
      ev.target.value = '';
    });
    $('btn-reset').addEventListener('click', function () {
      if (!confirm('Alle Eingaben verwerfen und neu beginnen?')) return;
      state = neuerZustand();
      letztesErgebnis = null;
      speichern();
      renderAlles();
      $('ergebnis-inhalt').innerHTML = '<p class="placeholder">Eingaben erfassen oder einen Beispielfall laden ' +
        'und anschließend „Prüfung &amp; Berechnung starten“ wählen.</p>';
      $('uebersicht-karte').hidden = true;
    });

    $('btn-anrecht-neu').addEventListener('click', function () {
      state.anrechte.push(X.basisAnrecht({ id: 'A' + (state.anrechte.length + 1) }));
      state.aktiv = state.anrechte.length - 1;
      speichern();
      renderAlles();
    });
    $('btn-anrecht-kopie').addEventListener('click', function () {
      var kopie = JSON.parse(JSON.stringify(aktivesAnrecht()));
      kopie.id = (kopie.id || 'A') + '-Kopie';
      state.anrechte.push(kopie);
      state.aktiv = state.anrechte.length - 1;
      speichern();
      renderAlles();
    });
    $('btn-anrecht-loeschen').addEventListener('click', function () {
      if (state.anrechte.length <= 1) return;
      if (!confirm('Aktives Anrecht löschen?')) return;
      state.anrechte.splice(state.aktiv, 1);
      state.aktiv = Math.max(0, state.aktiv - 1);
      speichern();
      renderAlles();
    });
    $('btn-config-reset').addEventListener('click', function () {
      state.cfgJahrGeladen = null;
      pruefeJahreswechsel();
      speichern();
      renderAlles();
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})(typeof window !== 'undefined' ? window : this);
