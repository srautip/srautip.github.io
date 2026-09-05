/* =====================================================================
   BAV-Versorgungsausgleich – Prüf- und Berechnungs-Engine
   Umsetzung der Spezifikation "Versorgungsausgleich – Betriebliche
   Altersversorgung (bAV), ein Ehegatte".
   Reine Logik: keine DOM-Zugriffe, keine Seiteneffekte.
   ===================================================================== */
(function (global) {
  'use strict';

  /* ------------------------------------------------------------------
     Enums
     ------------------------------------------------------------------ */
  var DURCHFUEHRUNGSWEG = {
    DIREKTZUSAGE: 'DIREKTZUSAGE',
    UKASSE: 'UKASSE',
    PENSIONSKASSE: 'PENSIONSKASSE',
    PENSIONSFONDS: 'PENSIONSFONDS',
    DIREKTVERSICHERUNG: 'DIREKTVERSICHERUNG'
  };
  var ZUSAGEART = {
    LEISTUNGSZUSAGE: 'LEISTUNGSZUSAGE',
    BEITRAGSORIENTIERT: 'BEITRAGSORIENTIERT',
    BEITRAGSZUSAGE_MIT_MINDESTLEISTUNG: 'BEITRAGSZUSAGE_MIT_MINDESTLEISTUNG'
  };
  var EINHEIT = { RENTE_MONAT: 'RENTE_MONAT', KAPITALWERT: 'KAPITALWERT' };
  var BEWERTUNG = { UNMITTELBAR: 'UNMITTELBAR', ZEITRATIERLICH: 'ZEITRATIERLICH' };
  var TEILUNGSART = { INTERN: 'INTERN', EXTERN: 'EXTERN' };

  /* Erwartete Bewertungsmethode je Durchführungsweg (§ 45 VersAusglG) */
  var ERWARTETE_BEWERTUNG = {
    DIREKTZUSAGE: BEWERTUNG.ZEITRATIERLICH,
    UKASSE: BEWERTUNG.ZEITRATIERLICH,
    PENSIONSKASSE: BEWERTUNG.UNMITTELBAR,
    PENSIONSFONDS: BEWERTUNG.UNMITTELBAR,
    DIREKTVERSICHERUNG: BEWERTUNG.UNMITTELBAR
  };

  /* ------------------------------------------------------------------
     Hilfsfunktionen (Abschnitt 5 der Spezifikation)
     ------------------------------------------------------------------ */
  function toDate(v) {
    if (v === null || v === undefined || v === '') return null;
    if (v instanceof Date) return isNaN(v.getTime()) ? null : v;
    var m = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(v).trim());
    if (m) return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
    var d = new Date(v);
    return isNaN(d.getTime()) ? null : d;
  }

  function isNum(v) { return typeof v === 'number' && isFinite(v); }

  /** volle Monate zwischen zwei Daten (angefangener Monat zählt nicht) */
  function monate(von, bis) {
    von = toDate(von); bis = toDate(bis);
    if (!von || !bis) return null;
    var m = (bis.getFullYear() - von.getFullYear()) * 12 + (bis.getMonth() - von.getMonth());
    if (bis.getDate() < von.getDate()) m -= 1;
    return m;
  }

  /** Ehezeitmonate nach § 3 Abs. 1 VersAusglG: Heiratsmonat und Endmonat zählen mit */
  function ehezeitMonate(beginn, ende) {
    beginn = toDate(beginn); ende = toDate(ende);
    if (!beginn || !ende) return null;
    return (ende.getFullYear() - beginn.getFullYear()) * 12 +
           (ende.getMonth() - beginn.getMonth()) + 1;
  }

  function istMonatsErster(d) { d = toDate(d); return d ? d.getDate() === 1 : false; }
  function istMonatsLetzter(d) {
    d = toDate(d);
    if (!d) return false;
    return d.getDate() === new Date(d.getFullYear(), d.getMonth() + 1, 0).getDate();
  }

  /** Alter in Jahren (dezimal) */
  function alter(geburt, stichtag) {
    geburt = toDate(geburt); stichtag = toDate(stichtag);
    if (!geburt || !stichtag) return null;
    return (stichtag.getTime() - geburt.getTime()) / (365.2425 * 24 * 3600 * 1000);
  }

  function datumPlusMonate(d, n) {
    d = toDate(d);
    if (!d) return null;
    return new Date(d.getFullYear(), d.getMonth() + n, d.getDate());
  }

  /** abweichung(soll, ist) = |soll-ist| / |soll| */
  function abweichung(soll, ist) {
    if (!isNum(soll) || !isNum(ist)) return null;
    if (soll === 0 && ist === 0) return 0;
    if (soll === 0) return 1;
    return Math.abs(soll - ist) / Math.abs(soll);
  }

  function nahe(x, y, tol) {
    var a = abweichung(x, y);
    return a !== null && a <= tol;
  }

  function round(v, n) {
    if (!isNum(v)) return v;
    var f = Math.pow(10, n === undefined ? 2 : n);
    return Math.round((v + Number.EPSILON) * f) / f;
  }

  /* ------------------------------------------------------------------
     Vereinfachte Sterblichkeit / Barwerte
     Gompertz-Makeham-Näherung. Ersetzt KEINE Sterbetafel des Trägers,
     dient nur der Plausibilitätsprüfung KW03/KW04 (WARN, nie ERROR).
     ------------------------------------------------------------------ */
  function sterblichkeitsintensitaet(x) {
    return 0.0002 + 2.0e-5 * Math.pow(1.10, x);
  }

  /** Überlebenswahrscheinlichkeit von Alter "von" bis Alter "bis" */
  function ueberlebenswkt(von, bis) {
    if (!isNum(von) || !isNum(bis) || bis <= von) return 1;
    var p = 1, schritt = 0.5;
    for (var x = von; x < bis; x += schritt) {
      var h = Math.min(schritt, bis - x);
      p *= Math.exp(-sterblichkeitsintensitaet(x + h / 2) * h);
    }
    return p;
  }

  /** Barwertfaktor einer vorschüssigen lebenslangen Jahresrente ab "startalter" */
  function leibrentenBarwertfaktor(startalter, zinsProzent) {
    if (!isNum(startalter)) return null;
    var z = (isNum(zinsProzent) ? zinsProzent : 0) / 100;
    var v = 1 / (1 + z);
    var summe = 0, p = 1;
    for (var k = 0; k <= 70; k++) {
      summe += p * Math.pow(v, k);
      p *= ueberlebenswkt(startalter + k, startalter + k + 1);
    }
    return summe;
  }

  /**
   * barwert_aufgeschobene_rente(jr, alter, endalter, zins)
   * Näherung: Leibrentenbarwert ab Endalter, um (endalter-alter) Jahre
   * aufgeschoben, mit Zins diskontiert und mit Erlebensfallwahrscheinlichkeit
   * gewichtet. Monatliche Zahlweise über den Abschlag 11/24.
   */
  function barwertAufgeschobeneRente(jahresrente, alterJetzt, endalter, zinsProzent) {
    if (!isNum(jahresrente) || !isNum(alterJetzt) || !isNum(endalter)) return null;
    var z = (isNum(zinsProzent) ? zinsProzent : 0) / 100;
    var v = 1 / (1 + z);
    var aufschub = Math.max(0, endalter - alterJetzt);
    var startalter = Math.max(alterJetzt, endalter);
    var faktor = leibrentenBarwertfaktor(startalter, zinsProzent);
    var faktor12 = Math.max(0, faktor - 11 / 24);
    return jahresrente * faktor12 * Math.pow(v, aufschub) * ueberlebenswkt(alterJetzt, endalter);
  }

  /* ------------------------------------------------------------------
     Befund-Katalog: Code -> Schweregrad, Titel, Rechtsgrundlage,
     fachliche Erläuterung (wird in der Oberfläche angezeigt)
     ------------------------------------------------------------------ */
  var KATALOG = {
    /* Schritt 1 */
    GD01: { sev: 'ERROR', titel: 'Pflichtfeld fehlt', recht: '§ 5 VersAusglG',
      erklaerung: 'Ohne die Kernangaben der Trägerauskunft lässt sich weder der Ehezeitanteil noch der Ausgleichswert nachvollziehen. Die Auskunft ist nachzufordern.' },
    GD02: { sev: 'ERROR', titel: 'Ehezeitmonate weichen ab', recht: '§ 3 Abs. 1 VersAusglG',
      erklaerung: 'Die Ehezeit läuft vom ersten Tag des Heiratsmonats bis zum letzten Tag des Monats vor Zustellung des Scheidungsantrags. Rechnet der Träger mit einer anderen Monatszahl, ist der gesamte Ehezeitanteil falsch abgegrenzt.' },
    GD03: { sev: 'WARN', titel: 'Auskunft vor Ehezeitende erstellt', recht: '§ 5 Abs. 2 VersAusglG',
      erklaerung: 'Stichtag der Bewertung ist das Ehezeitende. Eine früher erstellte Auskunft kann den Ehezeitanteil noch nicht endgültig ausweisen.' },
    GD04: { sev: 'WARN', titel: 'Auskunft älter als 12 Monate', recht: '§ 5 Abs. 2 S. 2 VersAusglG',
      erklaerung: 'Rechtlich maßgeblich bleibt der Stichtag Ehezeitende; nachträgliche Wertänderungen mit Rückwirkung auf die Ehezeit sind aber zu berücksichtigen. Bei alten Auskünften ist eine Aktualisierung üblich.' },
    GD05: { sev: 'INFO', titel: 'Anwartschaft verfallbar – nicht ausgleichsreif', recht: '§ 19 Abs. 2 Nr. 1 VersAusglG',
      erklaerung: 'Eine noch verfallbare Anwartschaft wird im Wertausgleich bei der Scheidung nicht geteilt. Sie bleibt dem schuldrechtlichen Ausgleich nach der Scheidung vorbehalten (§§ 20 ff. VersAusglG).' },
    GD06: { sev: 'WARN', titel: 'Laut Datum inzwischen unverfallbar', recht: '§ 1b BetrAVG',
      erklaerung: 'Die Unverfallbarkeit ist nach dem angegebenen Datum bereits eingetreten. Die Auskunft ist zu aktualisieren, das Anrecht ist dann ausgleichsreif.' },
    GD07: { sev: 'WARN', titel: 'Bewertungsmethode untypisch für Durchführungsweg', recht: '§ 45 VersAusglG',
      erklaerung: 'Versicherungsförmige Wege (Pensionskasse, Pensionsfonds, Direktversicherung) werden unmittelbar bewertet, Direktzusage und Unterstützungskasse zeitratierlich nach § 2 BetrAVG. Abweichungen muss der Träger begründen.' },
    GD08: { sev: 'WARN', titel: 'Kein Deckungskapital ausgewiesen', recht: '§ 4 Abs. 5 BetrAVG, § 46 VersAusglG',
      erklaerung: 'Bei versicherungsförmiger bAV ist das Deckungskapital die Rechengrundlage des Kapitalwerts. Ohne diese Angabe ist der korrespondierende Kapitalwert nicht prüfbar.' },
    /* Schritt 2 */
    EA01: { sev: 'ERROR', titel: 'Negativer Ehezeitanteil', recht: '§ 39 VersAusglG',
      erklaerung: 'Ein Ehezeitanteil kann nicht negativ sein.' },
    EA02: { sev: 'INFO', titel: 'Kein Erwerb in der Ehezeit', recht: '§ 3 Abs. 1 VersAusglG',
      erklaerung: 'Der Ehezeitanteil beträgt 0. Es findet kein Ausgleich statt.' },
    EA03: { sev: 'WARN', titel: 'm/n-Grunddaten fehlen', recht: '§ 45 Abs. 1 S. 2 VersAusglG, § 2 BetrAVG',
      erklaerung: 'Für die zeitratierliche Bewertung braucht es Gesamtanrecht, Diensteintritt und feste Altersgrenze. Ohne diese Werte ist die Auskunft nicht nachrechenbar.' },
    EA04: { sev: 'WARN', titel: 'Abweichung zur m/n-Nachrechnung', recht: '§ 2 Abs. 1 BetrAVG',
      erklaerung: 'Der Ehezeitanteil entspricht nicht dem Gesamtanrecht multipliziert mit dem Verhältnis Ehezeit/mögliche Betriebszugehörigkeit. Sonderregelungen der Zusage (Festbeträge, Bausteine, Anrechnungszeiten) sind zu prüfen.' },
    EA05: { sev: 'ERROR', titel: 'Diensteintritt nach Ehezeitende', recht: '§ 3 Abs. 1 VersAusglG',
      erklaerung: 'Wenn das Dienstverhältnis erst nach Ehezeitende begonnen hat, kann in der Ehezeit nichts erworben worden sein – der Ehezeitanteil muss 0 betragen.' },
    EA06: { sev: 'ERROR', titel: 'Ehezeitanteil größer als Gesamtanrecht', recht: '§ 39 VersAusglG',
      erklaerung: 'Der in der Ehezeit erworbene Teil kann das gesamte Anrecht nicht übersteigen.' },
    EA07: { sev: 'WARN', titel: 'Ehezeit umfasst gesamte Dienstzeit', recht: '§ 2 Abs. 1 BetrAVG',
      erklaerung: 'Liegt die gesamte mögliche Betriebszugehörigkeit in der Ehezeit, müsste der Ehezeitanteil dem Gesamtanrecht entsprechen.' },
    EA08: { sev: 'WARN', titel: 'Deckungskapital in der Ehezeit gesunken', recht: '§ 46 VersAusglG',
      erklaerung: 'Ein Rückgang deutet auf Beitragsfreistellung, Entnahme, Beleihung oder Verrechnung hin. Die Ursache ist zu klären, ggf. liegt eine illoyale Vermögensminderung vor (§ 27 VersAusglG).' },
    EA09: { sev: 'WARN', titel: 'Ehezeitanteil ≠ Zuwachs des Deckungskapitals', recht: '§ 46 VersAusglG',
      erklaerung: 'Bei unmittelbarer Bewertung sollte der Ehezeitanteil dem Kapitalzuwachs in der Ehezeit entsprechen. Abweichungen entstehen durch Überschussanteile, Abschluss- und Verwaltungskosten – der Träger muss sie erläutern.' },
    EA10: { sev: 'INFO', titel: 'Vertrag in der Ehezeit begonnen', recht: '§ 3 Abs. 1 VersAusglG',
      erklaerung: 'Beginnt der Vertrag innerhalb der Ehezeit, ist das gesamte Anrecht Ehezeitanteil.' },
    /* Schritt 3 */
    KW01: { sev: 'ERROR', titel: 'Kapitalwert 0 bei positivem Ehezeitanteil', recht: '§ 47 VersAusglG',
      erklaerung: 'Zu jedem werthaltigen Anrecht gehört ein korrespondierender Kapitalwert. Fehlt er, ist weder die Bagatellgrenze noch die externe Teilung prüfbar.' },
    KW02: { sev: 'WARN', titel: 'Ehezeitanteil ≠ korrespondierender Kapitalwert', recht: '§ 47 Abs. 2 VersAusglG',
      erklaerung: 'Ist das Anrecht bereits als Kapitalwert ausgewiesen, müssen Ehezeitanteil und korrespondierender Kapitalwert übereinstimmen.' },
    KW03: { sev: 'WARN', titel: 'Barwertfaktor außerhalb plausibler Spanne', recht: '§ 47 Abs. 4 VersAusglG',
      erklaerung: 'Der Kapitalwert entspricht üblicherweise dem 8- bis 25-fachen der Jahresrente. Ausreißer deuten auf einen unüblichen Rechnungszins, ein falsches Alter oder eine unpassende Sterbetafel hin.' },
    KW04: { sev: 'WARN', titel: 'Kapitalwert weicht > 15 % von der Näherungsrechnung ab', recht: '§ 47 VersAusglG',
      erklaerung: 'Die interne Vergleichsrechnung ist nur eine grobe Näherung (Gompertz-Sterblichkeit, keine Trägertafel). Sie kann eine abweichende Bewertung anzeigen, ist aber nie allein tragfähig.' },
    KW05: { sev: 'WARN', titel: 'Rechnungszins außerhalb üblicher Spanne', recht: '§ 47 Abs. 4 VersAusglG',
      erklaerung: 'Ein unüblicher Rechnungszins verschiebt den Kapitalwert erheblich. Der Träger muss die Herleitung offenlegen.' },
    KW06: { sev: 'WARN', titel: 'Hoher Rechnungszins bei Direktzusage/U-Kasse', recht: '§ 47 Abs. 4 VersAusglG',
      erklaerung: 'Ein hoher Zins senkt den Barwert und damit den Ausgleichswert. Das begünstigt den Verpflichteten und ist für Transferverlust und Bagatellgrenze unmittelbar relevant.' },
    /* Schritt 4 */
    HT01: { sev: 'INFO', titel: 'Teilungskosten bereits im Ausgleichswert enthalten', recht: '§ 13 VersAusglG',
      erklaerung: 'Der Träger hat die Kosten schon vor der Halbteilung abgezogen. Ein erneuter Abzug wäre eine doppelte Belastung des Berechtigten.' },
    HT02: { sev: 'INFO', titel: 'Kostenabzug auf Kapitalwertebene erkannt', recht: '§ 13 VersAusglG',
      erklaerung: 'Bei Rentenanrechten rechnen Träger die Eurokosten häufig über den Kapitalwert um. Der Abzug ist bereits berücksichtigt.' },
    HT03: { sev: 'ERROR', titel: 'Ausgleichswert nicht als Halbteilung nachvollziehbar', recht: '§ 1 Abs. 1 VersAusglG',
      erklaerung: 'Der Halbteilungsgrundsatz verlangt, dass der Ausgleichswert die Hälfte des Ehezeitanteils ist – ggf. nach angemessenem Kostenabzug. Lässt sich der Wert nicht herleiten, ist die Auskunft zu beanstanden.' },
    /* Schritt 5 */
    BG01: { sev: 'INFO', titel: 'Geringfügiges Anrecht – Ausschluss möglich', recht: '§ 18 Abs. 2, 3 VersAusglG',
      erklaerung: 'Das Gericht soll ein Anrecht mit geringem Ausgleichswert nicht ausgleichen. Die Grenze liegt bei 1 % (Rente) bzw. 120 % (Kapital) der monatlichen Bezugsgröße. Es handelt sich um eine Ermessensentscheidung.' },
    /* Schritt 6 */
    TA01: { sev: 'WARN', titel: 'Externe Teilung ohne Rechtsgrundlage verlangt', recht: '§§ 14, 17 VersAusglG',
      erklaerung: 'Ohne Zustimmung des Berechtigten ist die externe Teilung nur bis zu den Grenzwerten möglich. Andernfalls ist intern zu teilen oder die Zustimmung einzuholen.' },
    TA02: { sev: 'WARN', titel: 'Keine Zielversorgung gewählt', recht: '§ 15 Abs. 5 VersAusglG',
      erklaerung: 'Ohne Wahl des Berechtigten geht der Ausgleichswert in die gesetzliche Rentenversicherung, bei Direktzusage und Unterstützungskasse in die Versorgungsausgleichskasse (§ 15 Abs. 5 S. 2 VersAusglG).' },
    TA03: { sev: 'WARN', titel: 'Vergleichswert der internen Teilung fehlt', recht: 'BVerfG 26.05.2020 – 1 BvL 5/18',
      erklaerung: 'Ohne den Wert, den der Berechtigte bei interner Teilung erhielte, lässt sich der verfassungsrechtlich begrenzte Transferverlust nicht prüfen.' },
    TA04: { sev: 'WARN', titel: 'Transferverlust über der Zumutbarkeitsgrenze', recht: 'BVerfG 26.05.2020 – 1 BvL 5/18',
      erklaerung: 'Die externe Teilung ist nur verfassungsgemäß, wenn der Berechtigte keine unzumutbaren Verluste erleidet. Das Gericht muss den Ausgleichswert erhöhen oder intern teilen.' },
    TA05: { sev: 'WARN', titel: 'Externe Teilung einer laufenden Rente', recht: '§ 14 VersAusglG',
      erklaerung: 'Befindet sich das Anrecht bereits im Leistungsbezug, wirft die externe Teilung besondere Fragen zur Zielversorgung und zum Leistungsbeginn auf.' },
    TA06: { sev: 'WARN', titel: 'Interne Teilung ohne Teilungsordnung', recht: '§ 10 Abs. 1 VersAusglG',
      erklaerung: 'Die interne Teilung setzt eine Regelung des Versorgungsträgers voraus, aus der sich der Vollzug für das Anrecht des Berechtigten ergibt. Sie ist anzufordern.' },
    /* Schritt 7 */
    TK01: { sev: 'ERROR', titel: 'Negative Teilungskosten', recht: '§ 13 VersAusglG',
      erklaerung: 'Teilungskosten können nicht negativ sein.' },
    TK02: { sev: 'INFO', titel: 'Keine Teilungskosten', recht: '§ 13 VersAusglG',
      erklaerung: 'Der Träger macht keine Kosten geltend. Der volle halbe Ehezeitanteil wird übertragen.' },
    TK03: { sev: 'WARN', titel: 'Kostenquote über üblicher Obergrenze', recht: '§ 13 VersAusglG',
      erklaerung: 'Angemessen sind nach der Rechtsprechung regelmäßig 2–3 % des Ehezeitanteils. Höhere Ansätze muss der Träger belegen.' },
    TK04: { sev: 'WARN', titel: 'Teilungskosten über absoluter Obergrenze', recht: '§ 13 VersAusglG',
      erklaerung: 'Neben der relativen Quote begrenzt die Rechtsprechung die Kosten der Höhe nach (häufig genannt: 500 €).' },
    TK05: { sev: 'INFO', titel: 'Mindestpauschale des Trägers', recht: '§ 13 VersAusglG',
      erklaerung: 'Bei kleinen Anrechten überschreitet eine Mindestpauschale die Quote zwangsläufig. Das ist im Grundsatz zulässig, aber zu prüfen.' },
    TK06: { sev: 'INFO', titel: 'Externe Teilung – keine Teilungskosten', recht: '§ 13 VersAusglG',
      erklaerung: 'Teilungskosten dürfen nur bei interner Teilung abgezogen werden. Bei externer Teilung bleiben sie unberücksichtigt.' },
    TK07: { sev: 'INFO', titel: 'Kostenabzug einmalig nachvollzogen', recht: '§ 13 VersAusglG',
      erklaerung: 'Der Träger hat die Kosten bereits vor der Halbteilung abgezogen. Da Schritt 8 vom ungekürzten halben Ehezeitanteil ausgeht, wird der hälftige Kostenanteil hier genau einmal berücksichtigt – das Ergebnis entspricht dem Ausgleichswert der Auskunft.' },
    /* Schritt 8 */
    ER01: { sev: 'ERROR', titel: 'Ausgleichsbetrag ≤ 0', recht: '§ 13 VersAusglG',
      erklaerung: 'Nach Kostenabzug bleibt kein übertragbarer Wert. Der Kostenansatz ist unangemessen oder das Anrecht ist geringfügig.' },
    ER02: { sev: 'ERROR', titel: 'Ausgleichsbetrag größer als halber Ehezeitanteil', recht: '§ 1 Abs. 1 VersAusglG',
      erklaerung: 'Mehr als die Hälfte des Ehezeitanteils darf nicht übertragen werden.' },
    ER03: { sev: 'WARN', titel: 'Anrecht bereits im Leistungsbezug', recht: '§ 30 VersAusglG',
      erklaerung: 'Die Kürzung greift ab Rechtskraft. Bis dahin erbrachte Leistungen genießen Vertrauensschutz nach § 30 VersAusglG.' },
    ER04: { sev: 'INFO', titel: 'Beitragszusage mit Mindestleistung', recht: '§ 1 Abs. 2 Nr. 2 BetrAVG',
      erklaerung: 'Die Mindestleistung ist kapitalbezogen definiert. Der Träger muss die Teilung auf Kapitalbasis darstellen.' },
    ER05: { sev: 'INFO', titel: 'Vorgeschlagene Anordnung', recht: '§§ 10, 14 VersAusglG',
      erklaerung: 'Zusammenfassung des Tenorvorschlags für den Beschluss.' }
  };

  function befund(code, text, details) {
    var k = KATALOG[code] || { sev: 'INFO', titel: code, recht: '', erklaerung: '' };
    return {
      code: code,
      severity: k.sev,
      titel: k.titel,
      rechtsgrundlage: k.recht,
      erklaerung: k.erklaerung,
      text: text || '',
      details: details || null
    };
  }

  function hatError(befunde) {
    return befunde.some(function (b) { return b.severity === 'ERROR'; });
  }
  function hatWarn(befunde) {
    return befunde.some(function (b) { return b.severity === 'WARN'; });
  }

  /* Formatierung für Befundtexte */
  function fmtEur(v) {
    if (!isNum(v)) return '–';
    return v.toLocaleString('de-DE', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €';
  }
  function fmtProzent(v, nk) {
    if (!isNum(v)) return '–';
    return v.toLocaleString('de-DE', { minimumFractionDigits: nk === undefined ? 2 : nk,
                                       maximumFractionDigits: nk === undefined ? 2 : nk }) + ' %';
  }
  function fmtWert(v, einheit) {
    if (!isNum(v)) return '–';
    if (einheit === EINHEIT.RENTE_MONAT) {
      return v.toLocaleString('de-DE', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' €/Monat';
    }
    return fmtEur(v);
  }
  function fmtZahl(v, nk) {
    if (!isNum(v)) return '–';
    return v.toLocaleString('de-DE', { minimumFractionDigits: nk === undefined ? 2 : nk,
                                       maximumFractionDigits: nk === undefined ? 2 : nk });
  }

  /* ------------------------------------------------------------------
     Schritt-Objekte für die schrittweise Darstellung
     ------------------------------------------------------------------ */
  function neuerSchritt(nr, titel, recht, beschreibung) {
    return {
      nr: nr, titel: titel, rechtsgrundlage: recht, beschreibung: beschreibung,
      zeilen: [], befunde: [], status: 'ok', fazit: ''
    };
  }
  function zeile(s, label, wert, formel, hinweis) {
    s.zeilen.push({ label: label, wert: wert, formel: formel || '', hinweis: hinweis || '' });
  }

  /* ==================================================================
     Schritt 1 – Grunddaten und Ausgleichsreife
     ================================================================== */
  var PFLICHTFELDER = [
    ['ehezeitanteil', 'Ehezeitanteil'],
    ['ausgleichswert', 'Ausgleichswert'],
    ['korr_kapitalwert', 'korrespondierender Kapitalwert'],
    ['rechnungszins', 'Rechnungszins'],
    ['ehezeit_monate_traeger', 'Ehezeitmonate laut Träger'],
    ['durchfuehrungsweg', 'Durchführungsweg'],
    ['einheit', 'Einheit']
  ];

  function checkGrunddaten(a, ez, cfg, ctx, s) {
    var b = [];

    var fehlend = PFLICHTFELDER.filter(function (f) {
      var v = a[f[0]];
      return v === null || v === undefined || v === '';
    }).map(function (f) { return f[1]; });

    if (fehlend.length) {
      b.push(befund('GD01', 'Es fehlen: ' + fehlend.join(', ') + '.', { fehlend: fehlend }));
    }

    zeile(s, 'Ehezeit', (ctx.fmtDatum(ez.beginn) + ' – ' + ctx.fmtDatum(ez.ende)),
      'Beginn bis Ende laut Antrag');
    zeile(s, 'Ehezeitmonate (System)', fmtZahl(ez.monate, 0) + ' Monate',
      '§ 3 Abs. 1 VersAusglG');
    zeile(s, 'Ehezeitmonate (Träger)', fmtZahl(a.ehezeit_monate_traeger, 0) + ' Monate',
      'Angabe der Trägerauskunft');

    if (isNum(a.ehezeit_monate_traeger) && isNum(ez.monate) && a.ehezeit_monate_traeger !== ez.monate) {
      b.push(befund('GD02',
        'Träger rechnet mit ' + a.ehezeit_monate_traeger + ' Monaten, das System mit ' + ez.monate + ' Monaten (Differenz ' +
        (a.ehezeit_monate_traeger - ez.monate) + ').',
        { traeger: a.ehezeit_monate_traeger, system: ez.monate }));
    }

    var auskunft = toDate(a.auskunftsdatum);
    var ezEnde = toDate(ez.ende);
    if (auskunft && ezEnde && auskunft < ezEnde) {
      b.push(befund('GD03', 'Auskunft vom ' + ctx.fmtDatum(auskunft) + ' liegt vor dem Ehezeitende ' + ctx.fmtDatum(ezEnde) + '.'));
    }
    if (auskunft) {
      var grenze = datumPlusMonate(ctx.heute, -12);
      if (auskunft < grenze) {
        b.push(befund('GD04', 'Auskunft vom ' + ctx.fmtDatum(auskunft) + ' ist älter als 12 Monate (Stand heute: ' + ctx.fmtDatum(ctx.heute) + ').'));
      }
    }

    /* Verfallbarkeit */
    zeile(s, 'Unverfallbarkeit', a.unverfallbar ? 'unverfallbar' : 'noch verfallbar',
      '§ 1b BetrAVG, § 19 Abs. 2 Nr. 1 VersAusglG');
    if (!a.unverfallbar) {
      b.push(befund('GD05', 'Das Anrecht ist nicht ausgleichsreif; der Ausgleich bleibt dem schuldrechtlichen Verfahren vorbehalten.'));
      var uab = toDate(a.unverfallbar_ab);
      if (uab && uab <= toDate(ctx.heute)) {
        b.push(befund('GD06', 'Unverfallbarkeit laut Angabe seit ' + ctx.fmtDatum(uab) + ' eingetreten – Auskunft aktualisieren.'));
      }
    }

    /* Bewertungsmethode zum Durchführungsweg */
    var erwartet = ERWARTETE_BEWERTUNG[a.durchfuehrungsweg];
    zeile(s, 'Durchführungsweg / Bewertung',
      ctx.label('durchfuehrungsweg', a.durchfuehrungsweg) + ' / ' + ctx.label('bewertung', a.bewertung),
      'erwartet: ' + (erwartet ? ctx.label('bewertung', erwartet) : '–'));
    if (erwartet && a.bewertung !== erwartet) {
      var ausnahme = (a.zusageart === ZUSAGEART.BEITRAGSORIENTIERT && a.bewertung === BEWERTUNG.UNMITTELBAR);
      if (!ausnahme) {
        b.push(befund('GD07',
          ctx.label('durchfuehrungsweg', a.durchfuehrungsweg) + ' wird üblicherweise ' +
          ctx.label('bewertung', erwartet).toLowerCase() + ' bewertet, die Auskunft nutzt ' +
          ctx.label('bewertung', a.bewertung).toLowerCase() + '.'));
      }
    }

    if ((a.durchfuehrungsweg === DURCHFUEHRUNGSWEG.PENSIONSKASSE ||
         a.durchfuehrungsweg === DURCHFUEHRUNGSWEG.DIREKTVERSICHERUNG) &&
        a.einheit === EINHEIT.RENTE_MONAT &&
        !isNum(a.deckungskapital_ezende)) {
      b.push(befund('GD08', 'Versicherungsförmige bAV ohne Angabe des Deckungskapitals zum Ehezeitende.'));
    }

    return b;
  }

  /* ==================================================================
     Schritt 2 – Ehezeitanteil
     ================================================================== */
  function checkEhezeitanteil(a, ez, cfg, ctx, s) {
    var b = [];

    zeile(s, 'Ehezeitanteil laut Träger', fmtWert(a.ehezeitanteil, a.einheit), 'Auskunft');

    if (isNum(a.ehezeitanteil) && a.ehezeitanteil < 0) {
      b.push(befund('EA01', 'Ehezeitanteil ' + fmtWert(a.ehezeitanteil, a.einheit) + '.'));
    }
    if (isNum(a.ehezeitanteil) && a.ehezeitanteil === 0) {
      b.push(befund('EA02', 'Der Ehezeitanteil beträgt 0.'));
    }

    if (a.bewertung === BEWERTUNG.ZEITRATIERLICH) {
      if (!isNum(a.gesamtanrecht) || !toDate(a.diensteintritt) || !toDate(a.regelaltersgrenze)) {
        b.push(befund('EA03', 'Es fehlen Gesamtanrecht, Diensteintritt oder feste Altersgrenze.'));
      } else {
        var n = monate(a.diensteintritt, a.regelaltersgrenze);
        var von = new Date(Math.max(toDate(a.diensteintritt).getTime(), toDate(ez.beginn).getTime()));
        var bis = new Date(Math.min(toDate(a.regelaltersgrenze).getTime(), toDate(ez.ende).getTime()));
        var m = monate(von, bis);
        if (m === null || m < 0) m = 0;

        var erwartetWert = n > 0 ? a.gesamtanrecht * m / n : null;
        var abw = abweichung(erwartetWert, a.ehezeitanteil);

        zeile(s, 'n – mögliche Betriebszugehörigkeit', fmtZahl(n, 0) + ' Monate',
          'Diensteintritt ' + ctx.fmtDatum(a.diensteintritt) + ' bis feste Altersgrenze ' + ctx.fmtDatum(a.regelaltersgrenze));
        zeile(s, 'm – davon in der Ehezeit', fmtZahl(m, 0) + ' Monate',
          'Überschneidung von Dienstzeit und Ehezeit (' + ctx.fmtDatum(von) + ' – ' + ctx.fmtDatum(bis) + ')');
        zeile(s, 'Gesamtanrecht', fmtWert(a.gesamtanrecht, a.einheit), 'Auskunft');
        zeile(s, 'Nachrechnung m/n', fmtWert(erwartetWert, a.einheit),
          'Gesamtanrecht × m / n = ' + fmtWert(a.gesamtanrecht, a.einheit) + ' × ' + m + ' / ' + n);
        zeile(s, 'Abweichung zur Auskunft', abw === null ? '–' : fmtProzent(abw * 100, 3),
          '|Nachrechnung − Auskunft| / Nachrechnung, Toleranz ' + fmtProzent(cfg.toleranz_relativ * 100, 2));

        if (abw !== null && abw > cfg.toleranz_relativ) {
          b.push(befund('EA04',
            'Ehezeitanteil weicht um ' + fmtProzent(abw * 100, 2) + ' von der m/n-Nachrechnung ab (m = ' + m + ', n = ' + n +
            ', erwartet ' + fmtWert(erwartetWert, a.einheit) + ').',
            { m: m, n: n, erwartet: erwartetWert, abweichung: abw }));
        }
        if (toDate(a.diensteintritt) > toDate(ez.ende)) {
          b.push(befund('EA05', 'Diensteintritt ' + ctx.fmtDatum(a.diensteintritt) + ' liegt nach dem Ehezeitende ' + ctx.fmtDatum(ez.ende) + '.'));
        }
        if (isNum(a.ehezeitanteil) && a.ehezeitanteil > a.gesamtanrecht) {
          b.push(befund('EA06', fmtWert(a.ehezeitanteil, a.einheit) + ' > ' + fmtWert(a.gesamtanrecht, a.einheit) + '.'));
        }
        if (toDate(ez.beginn) <= toDate(a.diensteintritt) && toDate(ez.ende) >= toDate(a.regelaltersgrenze)) {
          var abwGes = abweichung(a.gesamtanrecht, a.ehezeitanteil);
          if (abwGes !== null && abwGes > cfg.toleranz_relativ) {
            b.push(befund('EA07', 'Ehezeit deckt die gesamte Dienstzeit ab, Ehezeitanteil weicht dennoch um ' +
              fmtProzent(abwGes * 100, 2) + ' vom Gesamtanrecht ab.'));
          }
        }
      }
    }

    if (a.bewertung === BEWERTUNG.UNMITTELBAR) {
      if (isNum(a.deckungskapital_ezbeginn) && isNum(a.deckungskapital_ezende)) {
        var delta = a.deckungskapital_ezende - a.deckungskapital_ezbeginn;
        zeile(s, 'Deckungskapital Ehezeitbeginn', fmtEur(a.deckungskapital_ezbeginn), 'Auskunft');
        zeile(s, 'Deckungskapital Ehezeitende', fmtEur(a.deckungskapital_ezende), 'Auskunft');
        zeile(s, 'Zuwachs in der Ehezeit', fmtEur(delta), 'Deckungskapital Ende − Deckungskapital Beginn');

        if (delta < 0) {
          b.push(befund('EA08', 'Rückgang um ' + fmtEur(Math.abs(delta)) + '.', { delta: delta }));
        }
        if (a.einheit === EINHEIT.KAPITALWERT) {
          var abwD = abweichung(delta, a.ehezeitanteil);
          zeile(s, 'Abweichung Zuwachs / Ehezeitanteil', abwD === null ? '–' : fmtProzent(abwD * 100, 3),
            'Toleranz ' + fmtProzent(cfg.toleranz_relativ * 100, 2));
          if (abwD !== null && abwD > cfg.toleranz_relativ) {
            b.push(befund('EA09', 'Zuwachs ' + fmtEur(delta) + ' gegenüber Ehezeitanteil ' +
              fmtEur(a.ehezeitanteil) + ' (Abweichung ' + fmtProzent(abwD * 100, 2) + ').',
              { delta: delta, abweichung: abwD }));
          }
        }
      } else {
        zeile(s, 'Deckungskapital', 'nicht vollständig angegeben',
          'Ohne Anfangs- und Endwert ist der Zuwachs nicht prüfbar');
      }

      var vb = toDate(a.vertragsbeginn);
      if (a.deckungskapital_ezbeginn === 0 && (!vb || vb > toDate(ez.beginn))) {
        b.push(befund('EA10', 'Deckungskapital zu Ehezeitbeginn = 0' +
          (vb ? ' und Vertragsbeginn ' + ctx.fmtDatum(vb) + ' nach Ehezeitbeginn' : '') +
          ' – der Ehezeitanteil müsste dem Gesamtanrecht entsprechen.'));
      }
    }

    return b;
  }

  /* ==================================================================
     Schritt 3 – Kapitalwert und Rechnungszins
     ================================================================== */
  function checkKapitalwert(a, ez, cfg, ctx, s) {
    var b = [];

    zeile(s, 'Korrespondierender Kapitalwert', fmtEur(a.korr_kapitalwert), '§ 47 VersAusglG (Auskunft)');
    zeile(s, 'Rechnungszins', fmtProzent(a.rechnungszins), 'Angabe des Trägers');

    if (isNum(a.korr_kapitalwert) && isNum(a.ehezeitanteil) &&
        a.korr_kapitalwert <= 0 && a.ehezeitanteil > 0) {
      b.push(befund('KW01', 'Kapitalwert ' + fmtEur(a.korr_kapitalwert) + ' bei Ehezeitanteil ' +
        fmtWert(a.ehezeitanteil, a.einheit) + '.'));
    }

    if (a.einheit === EINHEIT.KAPITALWERT) {
      var abwK = abweichung(a.korr_kapitalwert, a.ehezeitanteil);
      zeile(s, 'Abgleich Kapital-Anrecht', abwK === null ? '–' : fmtProzent(abwK * 100, 3),
        'Abweichung zwischen Ehezeitanteil und Kapitalwert, Toleranz ' + fmtProzent(cfg.toleranz_relativ * 100, 2));
      if (abwK !== null && abwK > cfg.toleranz_relativ) {
        b.push(befund('KW02', 'Ehezeitanteil ' + fmtEur(a.ehezeitanteil) + ' gegenüber Kapitalwert ' +
          fmtEur(a.korr_kapitalwert) + ' (Abweichung ' + fmtProzent(abwK * 100, 2) + ').'));
      }
    }

    if (a.einheit === EINHEIT.RENTE_MONAT && isNum(a.ehezeitanteil) && a.ehezeitanteil > 0 &&
        isNum(a.korr_kapitalwert)) {
      var jahresrente = a.ehezeitanteil * 12;
      var faktor = a.korr_kapitalwert / jahresrente;
      zeile(s, 'Jahresrente (Ehezeitanteil)', fmtEur(jahresrente), 'Monatsrente × 12');
      zeile(s, 'Barwertfaktor', fmtZahl(faktor, 2) + ' Jahresrenten',
        'Kapitalwert / Jahresrente, plausibel ' + fmtZahl(cfg.barwert_faktor_min, 0) + '–' + fmtZahl(cfg.barwert_faktor_max, 0));

      if (faktor < cfg.barwert_faktor_min || faktor > cfg.barwert_faktor_max) {
        b.push(befund('KW03', 'Barwertfaktor ' + fmtZahl(faktor, 2) + ' Jahresrenten liegt außerhalb der Spanne ' +
          fmtZahl(cfg.barwert_faktor_min, 0) + '–' + fmtZahl(cfg.barwert_faktor_max, 0) + '.',
          { faktor: faktor }));
      }

      /* grobe Gegenrechnung */
      var alterEnde = alter(ctx.geburtsdatum, ez.ende);
      var endalter = null;
      if (toDate(a.regelaltersgrenze) && ctx.geburtsdatum) {
        endalter = alter(ctx.geburtsdatum, a.regelaltersgrenze);
      } else if (isNum(a.regelaltersgrenze_alter)) {
        endalter = a.regelaltersgrenze_alter;
      }

      if (isNum(alterEnde) && isNum(endalter)) {
        var erwarteterBarwert = barwertAufgeschobeneRente(jahresrente, alterEnde, endalter, a.rechnungszins);
        var abwB = abweichung(erwarteterBarwert, a.korr_kapitalwert);
        zeile(s, 'Alter bei Ehezeitende', fmtZahl(alterEnde, 1) + ' Jahre', 'aus Geburtsdatum des Inhabers');
        zeile(s, 'Leistungsbeginn (Alter)', fmtZahl(endalter, 1) + ' Jahre', 'feste Altersgrenze der Zusage');
        zeile(s, 'Näherungsbarwert', fmtEur(erwarteterBarwert),
          'aufgeschobene Leibrente, Zins ' + fmtProzent(a.rechnungszins) + ', vereinfachte Sterblichkeit',
          'Nur Plausibilitätsmaß – ersetzt keine Sterbetafel des Trägers.');
        zeile(s, 'Abweichung zur Auskunft', abwB === null ? '–' : fmtProzent(abwB * 100, 1), 'Toleranz 15 %');
        if (abwB !== null && abwB > 0.15) {
          b.push(befund('KW04', 'Kapitalwert ' + fmtEur(a.korr_kapitalwert) + ' gegenüber Näherung ' +
            fmtEur(erwarteterBarwert) + ' (Abweichung ' + fmtProzent(abwB * 100, 1) + ').',
            { naeherung: erwarteterBarwert, abweichung: abwB }));
        }
      } else {
        zeile(s, 'Näherungsbarwert', 'nicht berechenbar',
          'Geburtsdatum des Inhabers und feste Altersgrenze erforderlich');
      }
    }

    if (isNum(a.rechnungszins)) {
      if (a.rechnungszins < cfg.zins_min || a.rechnungszins > cfg.zins_max) {
        b.push(befund('KW05', 'Rechnungszins ' + fmtProzent(a.rechnungszins) + ' außerhalb der Spanne ' +
          fmtProzent(cfg.zins_min) + ' – ' + fmtProzent(cfg.zins_max) + '.'));
      }
      if ((a.durchfuehrungsweg === DURCHFUEHRUNGSWEG.DIREKTZUSAGE ||
           a.durchfuehrungsweg === DURCHFUEHRUNGSWEG.UKASSE) &&
          a.rechnungszins > cfg.zins_max_direktzusage) {
        b.push(befund('KW06', 'Rechnungszins ' + fmtProzent(a.rechnungszins) + ' über der Vergleichsgrenze ' +
          fmtProzent(cfg.zins_max_direktzusage) + ' bei ' + ctx.label('durchfuehrungsweg', a.durchfuehrungsweg) + '.'));
      }
    }

    return b;
  }

  /* ==================================================================
     Schritt 4 – Halbteilung
     ================================================================== */
  function checkHalbteilung(a, cfg, ctx, s) {
    var b = [];
    var soll = isNum(a.ehezeitanteil) ? a.ehezeitanteil / 2 : null;

    zeile(s, 'Halber Ehezeitanteil (Soll)', fmtWert(soll, a.einheit), 'Ehezeitanteil / 2');
    zeile(s, 'Ausgleichswert laut Träger', fmtWert(a.ausgleichswert, a.einheit), 'Auskunft');

    a.kosten_bereits_abgezogen = false;

    if (nahe(a.ausgleichswert, soll, cfg.toleranz_relativ)) {
      a.kosten_bereits_abgezogen = false;
      s.fazit = 'Der Ausgleichswert entspricht exakt der Hälfte des Ehezeitanteils; Teilungskosten sind noch nicht abgezogen.';
    } else if (isNum(a.teilungskosten) && a.teilungskosten > 0 &&
               nahe(a.ausgleichswert, (a.ehezeitanteil - a.teilungskosten) / 2, cfg.toleranz_relativ)) {
      a.kosten_bereits_abgezogen = true;
      zeile(s, 'Alternative: (Ehezeitanteil − Kosten) / 2',
        fmtWert((a.ehezeitanteil - a.teilungskosten) / 2, a.einheit),
        'Prüfvariante mit vorab abgezogenen Teilungskosten');
      b.push(befund('HT01', 'Der Ausgleichswert entspricht (Ehezeitanteil − Teilungskosten) / 2.'));
      s.fazit = 'Kostenabzug bereits in der Auskunft enthalten – kein zweiter Abzug in Schritt 7.';
    } else if (a.einheit === EINHEIT.RENTE_MONAT && isNum(a.teilungskosten) && a.teilungskosten > 0) {
      var sollKap = isNum(a.korr_kapitalwert) ? (a.korr_kapitalwert - a.teilungskosten) / 2 : null;
      zeile(s, 'Alternative auf Kapitalwertebene', fmtEur(sollKap),
        '(Kapitalwert − Kosten) / 2');
      zeile(s, 'Ausgleichswert als Kapital (Auskunft)', fmtEur(a.ausgleichswert_kapital),
        'Angabe des Trägers, falls vorhanden');
      if (isNum(a.ausgleichswert_kapital) && nahe(a.ausgleichswert_kapital, sollKap, cfg.toleranz_relativ)) {
        a.kosten_bereits_abgezogen = true;
        b.push(befund('HT02', 'Der als Kapital ausgewiesene Ausgleichswert entspricht (Kapitalwert − Kosten) / 2.'));
        s.fazit = 'Kostenabzug auf Kapitalwertebene bereits enthalten.';
      } else {
        b.push(befund('HT03', 'Ausgleichswert ' + fmtWert(a.ausgleichswert, a.einheit) + ' passt weder zu ' +
          fmtWert(soll, a.einheit) + ' noch zu einem Kostenabzug.'));
        s.status = 'fehler';
      }
    } else {
      var abwH = abweichung(soll, a.ausgleichswert);
      b.push(befund('HT03', 'Ausgleichswert ' + fmtWert(a.ausgleichswert, a.einheit) + ' weicht um ' +
        (abwH === null ? '–' : fmtProzent(abwH * 100, 2)) + ' vom halben Ehezeitanteil ' +
        fmtWert(soll, a.einheit) + ' ab.', { soll: soll, abweichung: abwH }));
      s.status = 'fehler';
    }

    return b;
  }

  /* ==================================================================
     Schritt 5 – Bagatelle (§ 18 Abs. 2, 3 VersAusglG)
     ================================================================== */
  function bagatellGrenze(a, cfg) {
    return a.einheit === EINHEIT.RENTE_MONAT
      ? cfg.bezugsgroesse_monat * cfg.bagatell_rente_faktor
      : cfg.bezugsgroesse_monat * cfg.bagatell_kapital_faktor;
  }
  function istBagatell(a, cfg) {
    if (!isNum(a.ausgleichswert)) return false;
    return a.ausgleichswert <= bagatellGrenze(a, cfg);
  }

  /* ==================================================================
     Schritt 6 – Teilungsart (§§ 10, 14, 17 VersAusglG)
     ================================================================== */
  function bestimmeTeilungsart(a, cfg, s, ctx) {
    if (a.teilungsart_vorschlag === TEILUNGSART.INTERN) {
      if (s) zeile(s, 'Vorschlag des Trägers', 'interne Teilung', '§ 10 VersAusglG – wird übernommen');
      return TEILUNGSART.INTERN;
    }
    if (a.zustimmung_berechtigter) {
      if (s) zeile(s, 'Zustimmung des Berechtigten', 'liegt vor', '§ 14 Abs. 2 Nr. 1 VersAusglG – externe Teilung zulässig');
      return TEILUNGSART.EXTERN;
    }

    if (a.durchfuehrungsweg === DURCHFUEHRUNGSWEG.DIREKTZUSAGE ||
        a.durchfuehrungsweg === DURCHFUEHRUNGSWEG.UKASSE) {
      var kapAusgleich = isNum(a.korr_kapitalwert) ? a.korr_kapitalwert / 2 : null;
      if (s) {
        zeile(s, 'Grenzwert § 17 VersAusglG', fmtEur(cfg.bbg_grv_jahr),
          'Beitragsbemessungsgrenze GRV (jährlich) – Sonderregel für Direktzusage und Unterstützungskasse');
        zeile(s, 'Ausgleichswert als Kapital', fmtEur(kapAusgleich), 'korrespondierender Kapitalwert / 2');
      }
      if (isNum(kapAusgleich) && kapAusgleich <= cfg.bbg_grv_jahr) return TEILUNGSART.EXTERN;
      return TEILUNGSART.INTERN;
    }

    if (a.einheit === EINHEIT.RENTE_MONAT) {
      var grenzeR = cfg.bezugsgroesse_monat * cfg.extern_rente_faktor;
      if (s) zeile(s, 'Grenzwert § 14 Abs. 2 Nr. 2 VersAusglG', fmtEur(grenzeR),
        fmtProzent(cfg.extern_rente_faktor * 100, 0) + ' der monatlichen Bezugsgröße (' + fmtEur(cfg.bezugsgroesse_monat) + ')');
      return a.ausgleichswert <= grenzeR ? TEILUNGSART.EXTERN : TEILUNGSART.INTERN;
    }
    var grenzeK = cfg.bezugsgroesse_monat * cfg.extern_kapital_faktor;
    if (s) zeile(s, 'Grenzwert § 14 Abs. 2 Nr. 2 VersAusglG', fmtEur(grenzeK),
      fmtProzent(cfg.extern_kapital_faktor * 100, 0) + ' der monatlichen Bezugsgröße (' + fmtEur(cfg.bezugsgroesse_monat) + ')');
    return a.ausgleichswert <= grenzeK ? TEILUNGSART.EXTERN : TEILUNGSART.INTERN;
  }

  /** Erwartete monatliche Rente aus dem übertragenen Kapital in der Zielversorgung */
  function erwarteteRenteZielversorgung(kapital, a) {
    if (!isNum(kapital)) return null;
    if (isNum(a.extern_erwartete_leistung) && a.extern_erwartete_leistung > 0) {
      return a.extern_erwartete_leistung;
    }
    if (isNum(a.zielversorgung_rentenfaktor) && a.zielversorgung_rentenfaktor > 0) {
      /* Rentenfaktor: monatliche Rente je 10.000 EUR Kapital */
      return kapital / 10000 * a.zielversorgung_rentenfaktor;
    }
    return null;
  }

  function checkTeilungsart(a, cfg, ctx, s) {
    var b = [];

    zeile(s, 'Ergebnis der Prüfung', ctx.label('teilungsart', a.teilungsart),
      a.teilungsart === TEILUNGSART.INTERN ? '§ 10 VersAusglG' : '§§ 14, 17 VersAusglG');

    if (a.teilungsart_vorschlag === TEILUNGSART.EXTERN && a.teilungsart === TEILUNGSART.INTERN) {
      b.push(befund('TA01', 'Der Träger schlägt externe Teilung vor, der Grenzwert ist aber überschritten und es liegt keine Zustimmung des Berechtigten vor.'));
    }

    if (a.teilungsart === TEILUNGSART.EXTERN) {
      if (!a.zielversorgung) {
        b.push(befund('TA02', 'Auffangzielversorgung: ' +
          ((a.durchfuehrungsweg === DURCHFUEHRUNGSWEG.DIREKTZUSAGE || a.durchfuehrungsweg === DURCHFUEHRUNGSWEG.UKASSE)
            ? 'Versorgungsausgleichskasse' : 'gesetzliche Rentenversicherung') + '.'));
      } else {
        zeile(s, 'Zielversorgung', String(a.zielversorgung), '§ 15 VersAusglG (Wahl des Berechtigten)');
      }

      if (!isNum(a.intern_vergleichswert)) {
        b.push(befund('TA03', 'Es fehlt die Angabe, welche Leistung der Berechtigte bei interner Teilung erhielte.'));
      } else {
        var uebertrag = isNum(a.korr_kapitalwert) ? a.korr_kapitalwert / 2 : null;
        var externWert = erwarteteRenteZielversorgung(uebertrag, a);
        zeile(s, 'Übertragenes Kapital', fmtEur(uebertrag), 'korrespondierender Kapitalwert / 2');
        zeile(s, 'Leistung bei interner Teilung', fmtWert(a.intern_vergleichswert, EINHEIT.RENTE_MONAT), 'Angabe des Trägers');
        if (externWert === null) {
          zeile(s, 'Leistung in der Zielversorgung', 'nicht ermittelbar',
            'Erwartete Leistung oder Rentenfaktor der Zielversorgung angeben');
          b.push(befund('TA03', 'Die erwartete Leistung der Zielversorgung ist nicht hinterlegt – Transferverlust nicht berechenbar.'));
        } else {
          var verlust = 1 - externWert / a.intern_vergleichswert;
          zeile(s, 'Leistung in der Zielversorgung', fmtWert(externWert, EINHEIT.RENTE_MONAT),
            isNum(a.extern_erwartete_leistung) && a.extern_erwartete_leistung > 0
              ? 'Angabe der Zielversorgung'
              : 'Rentenfaktor ' + fmtZahl(a.zielversorgung_rentenfaktor, 2) + ' € je 10.000 € Kapital');
          zeile(s, 'Transferverlust', fmtProzent(verlust * 100, 1),
            '1 − Leistung extern / Leistung intern, Grenze ' + fmtProzent(cfg.transferverlust_max * 100, 0));
          if (verlust > cfg.transferverlust_max) {
            b.push(befund('TA04', 'Transferverlust ' + fmtProzent(verlust * 100, 1) + ' übersteigt die Grenze von ' +
              fmtProzent(cfg.transferverlust_max * 100, 0) + '.', { verlust: verlust }));
          }
        }
      }

      if (a.laufende_leistung) {
        b.push(befund('TA05', 'Das Anrecht befindet sich bereits im Leistungsbezug.'));
      }
    }

    if (a.teilungsart === TEILUNGSART.INTERN &&
        a.durchfuehrungsweg === DURCHFUEHRUNGSWEG.DIREKTZUSAGE &&
        !a.traeger_teilungsordnung_vorhanden) {
      b.push(befund('TA06', 'Für die Direktzusage liegt keine Teilungsordnung des Arbeitgebers vor.'));
    }

    return b;
  }

  /* ==================================================================
     Schritt 7 – Teilungskosten (§ 13 VersAusglG)
     ================================================================== */
  function berechneKostenabzug(a, cfg, ctx, s) {
    if (a.teilungsart === TEILUNGSART.EXTERN) {
      if (s) zeile(s, 'Kostenabzug', fmtWert(0, a.einheit), 'Externe Teilung – § 13 VersAusglG gilt nur für die interne Teilung');
      return 0;
    }
    if (!isNum(a.teilungskosten) || a.teilungskosten <= 0) {
      if (s) zeile(s, 'Kostenabzug', fmtWert(0, a.einheit), 'Keine Teilungskosten geltend gemacht');
      return 0;
    }

    if (a.kosten_bereits_abgezogen && s) {
      zeile(s, 'Hinweis zum Rechenweg', 'Kosten bereits in der Auskunft abgezogen',
        'Schritt 8 rechnet vom ungekürzten halben Ehezeitanteil (Ehezeitanteil / 2). Der hälftige Kostenanteil wird deshalb hier genau einmal abgezogen – nicht doppelt.',
        'Bewusste Abweichung vom Pseudocode, der an dieser Stelle 0 zurückgibt und den Kostenabzug damit verlieren würde.');
    }

    var kosten = a.teilungskosten;
    if (a.einheit === EINHEIT.RENTE_MONAT) {
      if (!isNum(a.korr_kapitalwert) || a.korr_kapitalwert === 0) {
        if (s) zeile(s, 'Kostenabzug', 'nicht berechenbar', 'Kapitalwert = 0, keine Umrechnung der Eurokosten möglich');
        return 0;
      }
      kosten = a.ehezeitanteil * (a.teilungskosten / a.korr_kapitalwert);
      if (s) zeile(s, 'Kosten umgerechnet in Rente', fmtWert(kosten, a.einheit),
        'Ehezeitanteil × (Kosten / Kapitalwert) = ' + fmtWert(a.ehezeitanteil, a.einheit) + ' × (' +
        fmtEur(a.teilungskosten) + ' / ' + fmtEur(a.korr_kapitalwert) + ')');
    }
    if (s) zeile(s, 'Hälftiger Kostenabzug', fmtWert(kosten / 2, a.einheit),
      'Die Kosten tragen beide Ehegatten je zur Hälfte');
    return kosten / 2;
  }

  function checkTeilungskosten(a, cfg, ctx, s) {
    var b = [];

    zeile(s, 'Teilungskosten laut Träger', fmtEur(a.teilungskosten), 'Auskunft');

    if (isNum(a.teilungskosten) && a.teilungskosten < 0) {
      b.push(befund('TK01', 'Angegeben: ' + fmtEur(a.teilungskosten) + '.'));
    }
    if ((!isNum(a.teilungskosten) || a.teilungskosten === 0) && a.teilungsart === TEILUNGSART.INTERN) {
      b.push(befund('TK02', 'Der Träger erhebt keine Teilungskosten.'));
    }
    if (isNum(a.teilungskosten) && a.teilungskosten > 0) {
      var quote = isNum(a.korr_kapitalwert) && a.korr_kapitalwert !== 0
        ? a.teilungskosten / a.korr_kapitalwert : null;
      zeile(s, 'Kostenquote', quote === null ? '–' : fmtProzent(quote * 100, 2),
        'Kosten / Kapitalwert, Obergrenze ' + fmtProzent(cfg.kosten_quote_max * 100, 0));
      if (quote !== null && quote > cfg.kosten_quote_max) {
        b.push(befund('TK03', 'Kostenquote ' + fmtProzent(quote * 100, 2) + ' über der Obergrenze ' +
          fmtProzent(cfg.kosten_quote_max * 100, 0) + '.', { quote: quote }));
      }
      if (a.teilungskosten > cfg.kosten_abs_max) {
        b.push(befund('TK04', fmtEur(a.teilungskosten) + ' über der absoluten Obergrenze ' + fmtEur(cfg.kosten_abs_max) + '.'));
      }
      if (a.teilungskosten < cfg.kosten_abs_min && quote !== null && quote > cfg.kosten_quote_max) {
        b.push(befund('TK05', 'Pauschale ' + fmtEur(a.teilungskosten) + ' unterschreitet die Mindestgrenze ' +
          fmtEur(cfg.kosten_abs_min) + ', übersteigt aber die Quote.'));
      }
    }
    if (a.teilungsart === TEILUNGSART.EXTERN && isNum(a.teilungskosten) && a.teilungskosten > 0) {
      b.push(befund('TK06', 'Die angegebenen Kosten von ' + fmtEur(a.teilungskosten) + ' bleiben unberücksichtigt.'));
    }
    if (a.kosten_bereits_abgezogen && a.teilungsart === TEILUNGSART.INTERN) {
      b.push(befund('TK07', 'Der Ausgleichsbetrag aus Schritt 8 entspricht dem bereits kostenbereinigten Ausgleichswert der Auskunft.'));
    }

    return b;
  }

  /* ==================================================================
     Schritt 8 – Ergebnis
     ================================================================== */
  function checkErgebnis(a, betrag, cfg, ctx, s) {
    var b = [];

    if (!isNum(betrag) || betrag <= 0) {
      b.push(befund('ER01', 'Ausgleichsbetrag ' + fmtWert(betrag, a.einheit) + ' nach Kostenabzug.'));
    }
    if (isNum(betrag) && isNum(a.ausgleichswert_berechnet) && betrag > a.ausgleichswert_berechnet + 1e-9) {
      b.push(befund('ER02', fmtWert(betrag, a.einheit) + ' > ' + fmtWert(a.ausgleichswert_berechnet, a.einheit) + '.'));
    }
    if (a.laufende_leistung) {
      b.push(befund('ER03', 'Die Kürzung des Anrechts wirkt ab Rechtskraft der Entscheidung.'));
    }
    if (a.zusageart === ZUSAGEART.BEITRAGSZUSAGE_MIT_MINDESTLEISTUNG && a.einheit === EINHEIT.RENTE_MONAT) {
      b.push(befund('ER04', 'Das Anrecht ist als Monatsrente ausgewiesen, die Mindestleistung ist aber kapitalbezogen.'));
    }
    b.push(befund('ER05', 'Anordnung: ' + fmtWert(round(betrag, 2), a.einheit) + ', ' +
      ctx.label('teilungsart', a.teilungsart) + ', Kostenabzug ' + fmtWert(round(a.kosten_abzug, 2), a.einheit) + '.'));

    return b;
  }

  /* ==================================================================
     Hauptablauf (Abschnitt 3 der Spezifikation)
     ================================================================== */
  function berechneBavAusgleich(anrechtIn, ezIn, cfg, optionen) {
    optionen = optionen || {};

    /* Arbeitskopie, damit Eingabedaten unverändert bleiben */
    var a = JSON.parse(JSON.stringify(anrechtIn));
    var ez = JSON.parse(JSON.stringify(ezIn));

    var ctx = {
      heute: toDate(optionen.heute) || new Date(),
      geburtsdatum: toDate(optionen.geburtsdatum || ez.geburtsdatum_inhaber),
      fmtDatum: optionen.fmtDatum || function (d) {
        d = toDate(d);
        return d ? ('0' + d.getDate()).slice(-2) + '.' + ('0' + (d.getMonth() + 1)).slice(-2) + '.' + d.getFullYear() : '–';
      },
      label: optionen.label || function (feld, wert) { return wert || '–'; }
    };

    var befunde = [];
    var schritte = [];

    function abschliessen(status, anordnung) {
      return {
        status: status,
        anordnung: anordnung || null,
        befunde: befunde,
        schritte: schritte,
        anrecht: a
      };
    }

    /* ---- Schritt 1 ---------------------------------------------- */
    var s1 = neuerSchritt(1, 'Grunddaten und Ausgleichsreife', '§§ 5, 19 VersAusglG, § 1b BetrAVG',
      'Vollständigkeit der Trägerauskunft, Abgleich der Ehezeit, Unverfallbarkeit und Passung von Durchführungsweg und Bewertungsmethode. Ein verfallbares Anrecht ist nicht ausgleichsreif; ein ausgeübtes Kapitalwahlrecht führt das Anrecht aus dem Versorgungsausgleich heraus.');
    s1.befunde = checkGrunddaten(a, ez, cfg, ctx, s1);
    befunde = befunde.concat(s1.befunde);
    schritte.push(s1);

    if (!a.unverfallbar) {
      s1.fazit = 'Anrecht nicht ausgleichsreif – Vorbehalt des schuldrechtlichen Ausgleichs (§ 19 Abs. 2 Nr. 1, §§ 20 ff. VersAusglG).';
      s1.status = 'gestoppt';
      return abschliessen('SCHULDRECHTLICH_VORBEHALTEN');
    }
    if (a.kapitalwahlrecht_ausgeuebt) {
      s1.fazit = 'Kapitalwahlrecht vor Ehezeitende ausgeübt – das Anrecht unterliegt dem Zugewinnausgleich, nicht dem Versorgungsausgleich.';
      s1.status = 'gestoppt';
      return abschliessen('NICHT_VA_SONDERN_ZUGEWINN');
    }
    if (hatError(s1.befunde)) {
      s1.status = 'fehler';
      s1.fazit = 'Abbruch: Die Grunddaten enthalten Fehler, die eine Berechnung ausschließen.';
      return abschliessen('ABBRUCH');
    }
    if (!s1.fazit) {
      s1.fazit = 'Das Anrecht ist ausgleichsreif, die Grunddaten sind verwendbar.';
    }

    /* ---- Schritt 2 ---------------------------------------------- */
    var s2 = neuerSchritt(2, 'Ehezeitanteil prüfen und nachrechnen', '§ 45 VersAusglG, § 2 BetrAVG',
      a.bewertung === BEWERTUNG.ZEITRATIERLICH
        ? 'Zeitratierliche Bewertung: Der Ehezeitanteil ergibt sich aus dem Gesamtanrecht im Verhältnis der Ehezeitmonate (m) zur gesamten möglichen Betriebszugehörigkeit bis zur festen Altersgrenze (n).'
        : 'Unmittelbare Bewertung: Maßgeblich ist der in der Ehezeit erwirtschaftete Zuwachs des Deckungskapitals bzw. der Anwartschaft.');
    s2.befunde = checkEhezeitanteil(a, ez, cfg, ctx, s2);
    befunde = befunde.concat(s2.befunde);
    if (hatError(s2.befunde)) s2.status = 'fehler';
    else if (hatWarn(s2.befunde)) s2.status = 'warnung';
    s2.fazit = s2.status === 'ok'
      ? 'Der Ehezeitanteil ist nachvollziehbar.'
      : 'Der Ehezeitanteil ist zu hinterfragen (siehe Befunde).';
    schritte.push(s2);

    /* ---- Schritt 3 ---------------------------------------------- */
    var s3 = neuerSchritt(3, 'Kapitalwert und Rechnungszins', '§ 47 VersAusglG, § 4 Abs. 5 BetrAVG',
      'Der korrespondierende Kapitalwert entscheidet über Bagatellgrenze, externe Teilung und Transferverlust. Geprüft werden die innere Stimmigkeit von Rente und Kapitalwert sowie die Plausibilität des Rechnungszinses.');
    s3.befunde = checkKapitalwert(a, ez, cfg, ctx, s3);
    befunde = befunde.concat(s3.befunde);
    if (hatError(s3.befunde)) s3.status = 'fehler';
    else if (hatWarn(s3.befunde)) s3.status = 'warnung';
    s3.fazit = s3.status === 'ok'
      ? 'Kapitalwert und Rechnungszins sind plausibel.'
      : 'Kapitalwert bzw. Rechnungszins sind erläuterungsbedürftig.';
    schritte.push(s3);

    /* ---- Schritt 4 ---------------------------------------------- */
    var s4 = neuerSchritt(4, 'Halbteilung', '§ 1 Abs. 1 VersAusglG',
      'Der Ausgleichswert muss die Hälfte des Ehezeitanteils sein. Weicht er ab, wird geprüft, ob der Träger die Teilungskosten bereits vorab abgezogen hat – auf Renten- oder auf Kapitalwertebene.');
    a.ausgleichswert_berechnet = isNum(a.ehezeitanteil) ? a.ehezeitanteil / 2 : null;
    s4.befunde = checkHalbteilung(a, cfg, ctx, s4);
    befunde = befunde.concat(s4.befunde);
    if (hatError(s4.befunde)) s4.status = 'fehler';
    else if (hatWarn(s4.befunde)) s4.status = 'warnung';
    schritte.push(s4);

    /* ---- Schritt 5 ---------------------------------------------- */
    var s5 = neuerSchritt(5, 'Geringfügigkeit (Bagatellprüfung)', '§ 18 Abs. 2, 3 VersAusglG',
      'Ein Anrecht mit geringem Ausgleichswert soll nicht ausgeglichen werden. Grenze: 1 % der monatlichen Bezugsgröße bei Renten, 120 % bei Kapitalwerten.');
    var grenze = bagatellGrenze(a, cfg);
    zeile(s5, 'Monatliche Bezugsgröße', fmtEur(cfg.bezugsgroesse_monat), 'Konfiguration für ' + cfg.jahr);
    zeile(s5, 'Bagatellgrenze', fmtWert(grenze, a.einheit),
      a.einheit === EINHEIT.RENTE_MONAT
        ? fmtProzent(cfg.bagatell_rente_faktor * 100, 0) + ' der Bezugsgröße'
        : fmtProzent(cfg.bagatell_kapital_faktor * 100, 0) + ' der Bezugsgröße');
    zeile(s5, 'Ausgleichswert', fmtWert(a.ausgleichswert, a.einheit), 'Vergleichswert');
    schritte.push(s5);

    if (istBagatell(a, cfg)) {
      var bg = befund('BG01', 'Ausgleichswert ' + fmtWert(a.ausgleichswert, a.einheit) +
        ' liegt unter der Grenze von ' + fmtWert(grenze, a.einheit) + '.');
      s5.befunde.push(bg);
      befunde.push(bg);
      /* Ein Bagatellvorschlag setzt belastbare Zahlen voraus. Liegen bereits
         Fehlerbefunde vor, wird nicht abgebrochen, sondern weitergeprüft. */
      if (!hatError(befunde)) {
        s5.status = 'gestoppt';
        s5.fazit = 'Geringfügiges Anrecht – Ausschluss nach § 18 Abs. 2 VersAusglG vorschlagen. Die Berechnung endet hier.';
        return abschliessen('BAGATELL_VORSCHLAG');
      }
      s5.status = 'fehler';
      s5.fazit = 'Der Ausgleichswert liegt zwar unter der Bagatellgrenze, wegen der bisherigen Fehlerbefunde ist er aber nicht belastbar. Es wird kein Ausschluss vorgeschlagen.';
    } else {
      s5.fazit = 'Keine Geringfügigkeit – der Ausgleich ist durchzuführen.';
    }

    /* ---- Schritt 6 ---------------------------------------------- */
    var s6 = neuerSchritt(6, 'Teilungsart bestimmen', '§§ 10, 14, 17 VersAusglG',
      'Grundsatz ist die interne Teilung. Extern wird geteilt bei Zustimmung des Berechtigten, innerhalb der Grenzwerte des § 14 Abs. 2 Nr. 2 VersAusglG oder – bei Direktzusage und Unterstützungskasse – einseitig bis zur Beitragsbemessungsgrenze der gesetzlichen Rentenversicherung (§ 17 VersAusglG).');
    zeile(s6, 'Vorschlag der Auskunft', ctx.label('teilungsart', a.teilungsart_vorschlag), 'Angabe des Trägers');
    a.teilungsart = bestimmeTeilungsart(a, cfg, s6, ctx);
    s6.befunde = checkTeilungsart(a, cfg, ctx, s6);
    befunde = befunde.concat(s6.befunde);
    if (hatError(s6.befunde)) s6.status = 'fehler';
    else if (hatWarn(s6.befunde)) s6.status = 'warnung';
    if (!s6.fazit) {
      s6.fazit = 'Anzuordnen ist die ' + (a.teilungsart === TEILUNGSART.INTERN ? 'interne' : 'externe') + ' Teilung.';
    }
    schritte.push(s6);

    /* ---- Schritt 7 ---------------------------------------------- */
    var s7 = neuerSchritt(7, 'Teilungskosten', '§ 13 VersAusglG',
      'Der Träger darf angemessene Kosten der internen Teilung mit dem Ehezeitanteil verrechnen. Beide Ehegatten tragen sie hälftig; bei externer Teilung entfällt der Abzug.');
    a.kosten_abzug = berechneKostenabzug(a, cfg, ctx, s7);
    s7.befunde = checkTeilungskosten(a, cfg, ctx, s7);
    befunde = befunde.concat(s7.befunde);
    if (hatError(s7.befunde)) s7.status = 'fehler';
    else if (hatWarn(s7.befunde)) s7.status = 'warnung';
    s7.fazit = 'Vom halben Ehezeitanteil abzuziehen: ' + fmtWert(round(a.kosten_abzug, 2), a.einheit) + '.';
    schritte.push(s7);

    /* ---- Schritt 8 ---------------------------------------------- */
    var betrag = (isNum(a.ausgleichswert_berechnet) ? a.ausgleichswert_berechnet : 0) - (a.kosten_abzug || 0);
    var s8 = neuerSchritt(8, 'Ergebnis und Anordnung', '§§ 10, 14, 30 VersAusglG',
      'Der Ausgleichsbetrag ergibt sich aus dem halben Ehezeitanteil abzüglich des hälftigen Kostenanteils. Abschließend wird die Schlüssigkeit des Ergebnisses geprüft.');
    zeile(s8, 'Halber Ehezeitanteil', fmtWert(a.ausgleichswert_berechnet, a.einheit), 'aus Schritt 4');
    zeile(s8, 'Kostenabzug', fmtWert(a.kosten_abzug, a.einheit), 'aus Schritt 7');
    zeile(s8, 'Ausgleichsbetrag', fmtWert(round(betrag, 2), a.einheit),
      'halber Ehezeitanteil − Kostenabzug');
    zeile(s8, 'Teilungsart', ctx.label('teilungsart', a.teilungsart), 'aus Schritt 6');
    s8.befunde = checkErgebnis(a, betrag, cfg, ctx, s8);
    befunde = befunde.concat(s8.befunde);
    schritte.push(s8);

    if (hatError(befunde)) {
      s8.status = 'fehler';
      s8.fazit = 'Abbruch: Es liegen Fehlerbefunde vor, die vor einer Anordnung zu klären sind.';
      return abschliessen('ABBRUCH');
    }

    var status = hatWarn(befunde) ? 'FREIGABE_ERFORDERLICH' : 'OK';
    s8.status = status === 'OK' ? 'ok' : 'warnung';
    s8.fazit = status === 'OK'
      ? 'Die Anordnung kann so ergehen.'
      : 'Die Anordnung ist rechnerisch schlüssig, alle WARN-Befunde brauchen jedoch eine dokumentierte Freigabe.';

    var anordnung = {
      anrecht_id: a.id,
      traeger: a.traeger,
      betrag: round(betrag, 2),
      einheit: a.einheit,
      teilungsart: a.teilungsart,
      kosten_abzug: round(a.kosten_abzug, 2),
      bezugsgroesse_jahr: cfg.jahr,
      stichtag: ez.ende
    };

    return abschliessen(status, anordnung);
  }

  /* ------------------------------------------------------------------
     Export
     ------------------------------------------------------------------ */
  global.BAV = global.BAV || {};
  global.BAV.engine = {
    DURCHFUEHRUNGSWEG: DURCHFUEHRUNGSWEG,
    ZUSAGEART: ZUSAGEART,
    EINHEIT: EINHEIT,
    BEWERTUNG: BEWERTUNG,
    TEILUNGSART: TEILUNGSART,
    ERWARTETE_BEWERTUNG: ERWARTETE_BEWERTUNG,
    KATALOG: KATALOG,
    berechneBavAusgleich: berechneBavAusgleich,
    /* Hilfsfunktionen auch einzeln nutzbar (Tests, Oberfläche) */
    helpers: {
      toDate: toDate, monate: monate, ehezeitMonate: ehezeitMonate,
      istMonatsErster: istMonatsErster, istMonatsLetzter: istMonatsLetzter,
      alter: alter, abweichung: abweichung, nahe: nahe, round: round,
      barwertAufgeschobeneRente: barwertAufgeschobeneRente,
      leibrentenBarwertfaktor: leibrentenBarwertfaktor,
      ueberlebenswkt: ueberlebenswkt,
      bagatellGrenze: bagatellGrenze, istBagatell: istBagatell,
      fmtEur: fmtEur, fmtProzent: fmtProzent, fmtWert: fmtWert, fmtZahl: fmtZahl,
      isNum: isNum
    }
  };
})(typeof window !== 'undefined' ? window : this);
