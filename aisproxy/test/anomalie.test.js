"use strict";
// Der Schleifenerkenner an gestellten Spuren.
//
// Warum gestellte und nicht echte: An echten Daten misst man, ob die Grenzen
// passen - das ist an 3 437 Spuren geschehen und steht in anomalie.js
// begruendet. Hier wird das andere geprueft, was echte Daten NICHT zeigen
// koennen: dass jede Regel fuer sich greift, und dass sie es aus dem
// richtigen Grund tut.
const test = require("node:test");
const assert = require("node:assert");
const A = require("../src/anomalie");

const MITTE = { lat: 54.0, lon: 8.0 };
const KY = 111320, KX = 111320 * Math.cos(54 * Math.PI / 180);

// Punkte im Format des Speichers: [t, lat*1e6, lon*1e6, sog*10, cog*10]
//
// Kein "sogKn || 5": Das machte aus der 0 eine 5, und jede Ruheprobe fand
// nichts, weil ihre stillliegenden Schiffe mit fuenf Knoten unterwegs waren.
// Derselbe Falsy-Fehler wie in pendel() weiter unten - beim zweiten Mal
// gehoert er benannt.
function pkt(t, dxM, dyM, sogKn) {
  return [t, Math.round((MITTE.lat + dyM / KY) * 1e6),
          Math.round((MITTE.lon + dxM / KX) * 1e6),
          Math.round((sogKn == null ? 5 : sogKn) * 10), 0];
}

// Ein Kreis mit r Metern Radius, in n Schritten, ab Zeitpunkt t0 alle dt s.
function kreis(r, n, t0, dt, x0, y0) {
  const aus = [];
  for (let i = 0; i <= n; i++) {
    const w = 2 * Math.PI * i / n;
    aus.push(pkt(t0 + i * dt, (x0 || 0) + r * Math.cos(w), (y0 || 0) + r * Math.sin(w)));
  }
  return aus;
}

// Hin und zurueck auf derselben Linie - die Faehre. Der Rueckweg liegt um
// versatz Meter daneben; versatz 0 ist der entartete Strich.
//
// Kein "versatz || 30": Das machte aus der 0 eine 30, und die Pruefung auf
// den entarteten Fall pruefte still den Gegenteil. Genau so ist sie beim
// Schreiben einmal fehlgeschlagen.
function pendel(laenge, runden, t0, dt, versatz) {
  const v = versatz == null ? 30 : versatz;
  const aus = [];
  let t = t0;
  for (let k = 0; k < runden; k++) {
    for (let i = 0; i <= 10; i++) aus.push(pkt(t += dt, laenge * i / 10, 0));
    for (let i = 10; i >= 0; i--) aus.push(pkt(t += dt, laenge * i / 10, v));
  }
  return aus;
}

// --- Was eine Schleife ist -------------------------------------------------

test("ein geschlossener Kreis ist eine Schleife", () => {
  const g = A.schleifen(kreis(600, 40, 1000, 60));
  assert.strictEqual(g.length, 1, "genau eine Runde");
  assert.ok(Math.abs(g[0].radius - 600) < 60, "Radius rund 600 m, ist " + g[0].radius);
  assert.ok(g[0].rund > 0.8, "ein Kreis ist rund, ist " + g[0].rund);
  // Der Ort ist der Mittelpunkt der Runde, nicht ihr Anfang.
  assert.ok(Math.abs(g[0].lat - MITTE.lat) < 0.002 &&
            Math.abs(g[0].lon - MITTE.lon) < 0.004, "Mittelpunkt sitzt in der Mitte");
});

test("eine gerade Fahrt ist keine Schleife", () => {
  const p = [];
  for (let i = 0; i <= 60; i++) p.push(pkt(1000 + i * 60, i * 300, 0));
  assert.deepStrictEqual(A.schleifen(p), []);
});

test("Fahrt auf derselben Linie hin und zurueck ist keine Schleife", () => {
  // Das ist es, was die Rundheitsschranke leistet: Sie schliesst den STRICH
  // aus. Gemessen an dieser gestellten Spur: bis 5 m Versatz null Funde,
  // ab 10 m greift sie.
  assert.deepStrictEqual(A.schleifen(pendel(2000, 6, 1000, 60, 0)), []);
  assert.deepStrictEqual(A.schleifen(pendel(2000, 6, 1000, 60, 5)), []);

  // Gegenprobe, damit die Pruefung nicht nur an zu wenigen Punkten scheitert:
  // Derselbe Weg als Bogen zurueck IST eine Schleife.
  assert.ok(A.schleifen(kreis(500, 30, 1000, 60)).length > 0,
    "die Gegenprobe muss anschlagen, sonst prueft der Test nichts");
});

test("eine Faehre mit echtem Versatz WIRD gefunden - und als gewohnt eingestuft", () => {
  // Bewusst so und nicht anders. Ein Kursaenderungs-Zaehler meldete fuer die
  // Elbfaehre BRESLAU 19 Runden und schob sie vor jedes Lotsenboot; die
  // Geometrie kann Faehre und Lotse aber nicht trennen (gemessen:
  // Laenglichkeit BRESLAU 5,92 gegen PILOTVESSEL HANSE 5,44). Die Trennung
  // faellt deshalb ueber den SCHIFFSTYP in hole(), nicht hier - und die
  // Faehre bleibt sichtbar, nur leiser.
  const g = A.schleifen(pendel(2000, 6, 1000, 60, 60));
  assert.ok(g.length > 0, "die Faehrenrunden werden erkannt");
  assert.ok(A.berufsschleifer(69), "und ihr Typ macht sie zum Berufsschleifer");
});

test("eine zu kleine Runde ist Manoevrieren, keine Schleife", () => {
  // Umfang 2*pi*100 = 628 m, unter MIN_WEG_M (800).
  assert.deepStrictEqual(A.schleifen(kreis(100, 30, 1000, 60)), []);
  // Und knapp darueber greift es.
  assert.ok(A.schleifen(kreis(200, 30, 1000, 60)).length > 0);
});

test("eine zu grosse Runde ist eine Reise, keine Schleife", () => {
  // Durchmesser 8 km > MAX_DURCHMESSER_M (6 km).
  assert.deepStrictEqual(A.schleifen(kreis(4000, 60, 1000, 60)), []);
  assert.ok(A.schleifen(kreis(2500, 60, 1000, 60)).length > 0, "5 km Durchmesser noch ja");
});

test("wer nach Stunden zufaellig wiederkommt, hat keine Runde gefahren", () => {
  // Derselbe Kreis, aber mit vier Stunden je Umlauf: ueber MAX_RUNDE_S (3 h).
  assert.deepStrictEqual(A.schleifen(kreis(600, 40, 1000, 400)), []);
});

test("zwei Runden hintereinander sind zwei Schleifen, keine geschachtelten", () => {
  const p = kreis(600, 40, 1000, 60).concat(kreis(600, 40, 1000 + 41 * 60, 60));
  const g = A.schleifen(p);
  assert.strictEqual(g.length, 2, "gefunden: " + g.length);
  assert.ok(g[1].von >= g[0].bis, "die zweite faengt nach der ersten an");
});

// --- Datenfehler -----------------------------------------------------------

test("ein Sprung ueber 40 kn wird verworfen, nicht mitgemessen", () => {
  // Der echte Fall: GRIETJE kam mit drei Spruengen auf 443 km in 8 h.
  const p = kreis(600, 40, 1000, 60);
  p.splice(20, 0, pkt(1000 + 19 * 60 + 1, 900000, 0));   // 900 km zur Seite
  const g = A.schleifen(p);
  assert.strictEqual(g.length, 1, "die Runde bleibt erkannt");
  assert.ok(g[0].radius < 700, "und der Ausreisser blaeht sie nicht auf, r = " + g[0].radius);
});

test("fehlende Koordinaten stuerzen nicht ab", () => {
  const p = kreis(600, 40, 1000, 60);
  p[5] = [p[5][0], null, null, 50, 0];
  assert.strictEqual(A.schleifen(p).length, 1);
});

test("zu kurze Spuren liefern nichts, statt zu werfen", () => {
  assert.deepStrictEqual(A.schleifen([]), []);
  assert.deepStrictEqual(A.schleifen([pkt(1, 0, 0), pkt(2, 10, 0)]), []);
});

// --- Verdichtung zu Gebieten -----------------------------------------------

test("nahe Schleifen werden ein Gebiet, ferne bleiben getrennt", () => {
  const ev = [
    { mmsi: 1, lat: 54.0, lon: 8.0, radius: 500, von: 10, bis: 20 },
    { mmsi: 2, lat: 54.005, lon: 8.005, radius: 400, von: 30, bis: 40 },  // ~600 m
    { mmsi: 3, lat: 54.3, lon: 8.5, radius: 300, von: 50, bis: 60 }       // ~40 km
  ];
  const g = A.gebiete(ev, 3000);
  assert.strictEqual(g.length, 2);
  const gross = g.find(x => x.schiffe.length === 2);
  assert.ok(gross, "die beiden nahen liegen zusammen");
  assert.deepStrictEqual(gross.schiffe, [1, 2]);
  assert.strictEqual(gross.schleifen, 2);
  assert.strictEqual(gross.von, 10, "das Gebiet reicht von der fruehesten");
  assert.strictEqual(gross.bis, 40, "bis zur spaetesten Schleife");
  // Der Radius muss beide Schleifen ganz enthalten, nicht nur ihre Mitten.
  assert.ok(gross.radius >= 500, "Radius deckt die Schleifen ab, ist " + gross.radius);
});

test("dasselbe Schiff zweimal im selben Gebiet zaehlt einmal", () => {
  const g = A.gebiete([
    { mmsi: 7, lat: 54.0, lon: 8.0, radius: 300, von: 1, bis: 2 },
    { mmsi: 7, lat: 54.001, lon: 8.001, radius: 300, von: 3, bis: 4 }
  ], 3000);
  assert.strictEqual(g.length, 1);
  assert.deepStrictEqual(g[0].schiffe, [7]);
  assert.strictEqual(g[0].schleifen, 2, "die Schleifen zaehlen aber beide");
});

// --- Wer von Berufs wegen kreist -------------------------------------------

test("Berufsschleifer sind benannt, nicht geraten", () => {
  for (const t of [30, 31, 32, 33, 50, 51, 52, 53, 54, 55, 60, 69]) {
    assert.ok(A.berufsschleifer(t), "typ " + t + " gehoert dazu");
  }
  for (const t of [70, 79, 80, 89, 36, 37, 40, 90, 99]) {
    assert.ok(!A.berufsschleifer(t), "typ " + t + " gehoert NICHT dazu");
  }
  // Der gemeldete Fall: MMSI 311003300 ist ein Autotransporter, typ 70.
  assert.ok(!A.berufsschleifer(70), "ein Frachter, der kreist, ist auffaellig");
  assert.ok(!A.berufsschleifer(null), "ohne Typ gilt niemand als Berufsschleifer");
  assert.ok(!A.berufsschleifer(undefined));
});

// --- Der Laufbetrieb -------------------------------------------------------

function stellVertreter(spurenJeKachel) {
  let i = 0;
  return {
    spuren() { return spurenJeKachel[i++] || []; },
    stammHole(mmsi) { return { typ: mmsi === 70 ? 70 : 50 }; }
  };
}

const KONFIG = {
  REGION: { latMin: 54, lonMin: 8, latMax: 55, lonMax: 9 },
  ANOMALIE_STUNDEN: 8, ANOMALIE_SCHRITT_S: 60,
  ANOMALIE_KACHEL_GRAD: 1, ANOMALIE_KACHEL_RAND_GRAD: 0.15,
  ANOMALIE_ZUSAMMEN_M: 3000
};

test("der Lauf sammelt Schleifen und meldet den Stand", async () => {
  const jetzt = Math.floor(Date.now() / 1000);
  const speicher = stellVertreter([[{ mmsi: 70, punkte: kreis(600, 40, jetzt - 3000, 60) }]]);
  const a = new A.Anomalie({ konfig: KONFIG, speicher, zustand: null });
  await a.lauf();
  assert.strictEqual(a.ereignisse.length, 1);
  assert.strictEqual(a.ereignisse[0].mmsi, 70, "die MMSI haengt am Ereignis");
  assert.strictEqual(a.bericht().schleifen, 1);
  assert.ok(a.bericht().gerechnet, "der Stand traegt eine Zeit");
});

test("dieselbe Schleife aus zwei ueberlappenden Kacheln zaehlt einmal", async () => {
  const jetzt = Math.floor(Date.now() / 1000);
  const spur = { mmsi: 70, punkte: kreis(600, 40, jetzt - 3000, 60) };
  // Beide Kacheln liefern dieselbe Spur - genau das passiert im Ueberlappungsrand.
  const speicher = stellVertreter([[spur], [spur]]);
  const a = new A.Anomalie({ konfig: KONFIG, speicher, zustand: null });
  await a.lauf();
  assert.strictEqual(a.ereignisse.length, 1, "gefunden: " + a.ereignisse.length);
});

test("hole() stuft nach Schiffstyp ein und begrenzt das Fenster", async () => {
  const jetzt = Math.floor(Date.now() / 1000);
  const speicher = stellVertreter([[
    { mmsi: 70, punkte: kreis(600, 40, jetzt - 3000, 60) },     // Frachter
    { mmsi: 50, punkte: kreis(600, 40, jetzt - 3000, 60, 20000, 0) }  // Lotse, 20 km weiter
  ]]);
  const a = new A.Anomalie({ konfig: KONFIG, speicher, zustand: null });
  await a.lauf();
  const box = { latMin: 53, lonMin: 7, latMax: 56, lonMax: 10 };
  const g = a.hole(box, 8);
  assert.strictEqual(g.length, 2, "zwei getrennte Gebiete");
  assert.strictEqual(g[0].stufe, "auffaellig", "das Auffaellige steht vorn");
  assert.strictEqual(g[0].schiffe[0], 70);
  assert.strictEqual(g[1].stufe, "gewohnt", "der Lotse gilt als gewohnt");
  assert.strictEqual(g[1].beruf, 1);
  assert.strictEqual(g[1].andere, 0);

  // Ein kurzes Fenster laesst die aeltere Schleife draussen.
  assert.strictEqual(a.hole(box, 0.1).length, 0, "vor 50 Minuten ist ausserhalb 6 Minuten");
  // Und eine bbox neben den Schleifen liefert nichts.
  assert.strictEqual(a.hole({ latMin: 40, lonMin: 0, latMax: 41, lonMax: 1 }, 8).length, 0);
});

test("die Kacheln decken die Region ab und ueberlappen", () => {
  const a = new A.Anomalie({
    konfig: Object.assign({}, KONFIG,
      { REGION: { latMin: 53, lonMin: 6, latMax: 56, lonMax: 13 } }),
    speicher: stellVertreter([]), zustand: null
  });
  const k = a.kacheln();
  assert.strictEqual(k.length, 21, "3 x 7 Grad zu je 1 Grad");
  // Jeder Punkt der Region liegt in mindestens einer Kachel.
  for (const [la, lo] of [[53.0, 6.0], [54.5, 9.5], [55.99, 12.99], [55.0, 7.0]]) {
    assert.ok(k.some(b => la >= b.latMin && la <= b.latMax && lo >= b.lonMin && lo <= b.lonMax),
      la + "/" + lo + " liegt in keiner Kachel");
  }
  // Und der Rand ist wirklich groesser als die groesste zugelassene Schleife.
  const randM = KONFIG.ANOMALIE_KACHEL_RAND_GRAD * 111320 * Math.cos(56 * Math.PI / 180);
  assert.ok(randM > A.MAX_DURCHMESSER_M,
    "Rand " + Math.round(randM) + " m muss ueber " + A.MAX_DURCHMESSER_M + " m liegen");
});

// --- Stillstand ------------------------------------------------------------

// Eine Ruhephase: n Punkte am selben Ort, alle dt Sekunden.
function liegt(t0, stunden, dt, x0, y0, streu) {
  const aus = [];
  const n = Math.round(stunden * 3600 / dt);
  for (let i = 0; i <= n; i++) {
    // Ein bisschen Zappeln wie ein schwojendes Schiff, aber innerhalb streu.
    const w = 2 * Math.PI * i / 7;
    aus.push(pkt(t0 + i * dt, (x0 || 0) + Math.cos(w) * (streu || 30),
                 (y0 || 0) + Math.sin(w) * (streu || 30), 0));
  }
  return aus;
}

test("wer sieben Stunden auf der Stelle liegt, hat eine Ruhephase", () => {
  const g = A.ruhephasen(liegt(1000, 7, 300), 6 * 3600);
  assert.strictEqual(g.length, 1, "gefunden: " + g.length);
  assert.ok(g[0].dauer >= 6 * 3600);
  assert.ok(g[0].radius <= A.STILL_M, "Radius " + g[0].radius);
});

test("fuenf Stunden reichen nicht, sieben schon", () => {
  assert.deepStrictEqual(A.ruhephasen(liegt(1000, 5, 300), 6 * 3600), []);
  assert.strictEqual(A.ruhephasen(liegt(1000, 7, 300), 6 * 3600).length, 1);
});

test("wer sich um 500 m bewegt, liegt nicht still", () => {
  // Dieselbe Dauer, aber ueber STILL_M verteilt: keine Ruhephase.
  assert.deepStrictEqual(A.ruhephasen(liegt(1000, 7, 300, 0, 0, 500), 6 * 3600), []);
  // Gegenprobe mit 100 m - sonst pruefte der Test nur die Dauer.
  assert.strictEqual(A.ruhephasen(liegt(1000, 7, 300, 0, 0, 100), 6 * 3600).length, 1);
});

test("Fahrt dazwischen zerteilt die Ruhe in zwei Phasen", () => {
  const p = liegt(1000, 7, 300)
    .concat([pkt(1000 + 7 * 3600 + 300, 3000, 0, 8)])   // faehrt weg
    .concat(liegt(1000 + 7 * 3600 + 600, 7, 300, 3000, 0));
  const g = A.ruhephasen(p, 6 * 3600);
  assert.strictEqual(g.length, 2, "gefunden: " + g.length);
});

test("die Nachbarschaft zaehlt Schiffe, nicht Phasen", () => {
  const ph = [
    { mmsi: 1, lat: 54.0, lon: 8.0 },
    { mmsi: 2, lat: 54.002, lon: 8.002 },      // rund 250 m
    { mmsi: 2, lat: 54.003, lon: 8.003 },      // dasselbe Schiff nochmal
    { mmsi: 9, lat: 54.9, lon: 8.9 }           // weit weg
  ];
  A.nachbarnZaehlen(ph, 3000);
  assert.strictEqual(ph[0].nachbarn, 1, "Schiff 2 zaehlt einmal, nicht zweimal");
  assert.strictEqual(ph[3].nachbarn, 0);
});

// --- Der Kern: was ist ein Ankerplatz --------------------------------------

const STILL_KONFIG = Object.assign({}, KONFIG, {
  ANOMALIE_STILL_STUNDEN: 24, ANOMALIE_STILL_SCHRITT_S: 300,
  ANOMALIE_STILL_MIN_S: 6 * 3600, ANOMALIE_STILL_GRUND_S: 2 * 3600,
  ANOMALIE_STILL_UMKREIS_M: 3000, ANOMALIE_STILL_MIN: 2,
  ANOMALIE_LAND_M: 0
});
const WEITBOX = { latMin: 40, lonMin: 0, latMax: 60, lonMax: 20 };

function ruheProxy(spuren, typen) {
  let i = 0;
  const speicher = {
    spuren() { return i++ === 0 ? [] : spuren; },   // 1. Durchgang Schleifen, 2. Ruhe
    stammHole(m) { return { typ: (typen || {})[m] }; }
  };
  return new A.Anomalie({ konfig: STILL_KONFIG, speicher, zustand: null });
}

test("allein liegen wird gemeldet, zu dritt liegen nicht", async () => {
  const jetzt = Math.floor(Date.now() / 1000);
  const t0 = jetzt - 10 * 3600;
  const allein = ruheProxy([{ mmsi: 70, punkte: liegt(t0, 7, 300) }]);
  await allein.lauf();
  assert.strictEqual(allein.stillstand(WEITBOX, 24).length, 1, "einer allein: Meldung");

  // Dieselbe Lage, aber zwei Nachbarn in 500 m: ein Ankerplatz.
  const zuDritt = ruheProxy([
    { mmsi: 70, punkte: liegt(t0, 7, 300) },
    { mmsi: 71, punkte: liegt(t0, 7, 300, 400, 0) },
    { mmsi: 72, punkte: liegt(t0, 7, 300, 0, 400) }
  ]);
  await zuDritt.lauf();
  assert.strictEqual(zuDritt.stillstand(WEITBOX, 24).length, 0,
    "zu dritt ist ein Ankerplatz, keine Meldung");
});

test("die Grundlinie zaehlt auch Schiffe, die selbst nie gemeldet wuerden", async () => {
  // Genau der Fehler, der beim Messen unterlaufen ist: Wer Grundlinie und
  // Meldung mit demselben Sieb filtert, laesst die Nachbarn verschwinden -
  // und dann wirken MEHR Schiffe einsam statt weniger.
  const jetzt = Math.floor(Date.now() / 1000);
  const t0 = jetzt - 10 * 3600;
  const a = ruheProxy([
    { mmsi: 70, punkte: liegt(t0, 7, 300) },                    // Frachter
    { mmsi: 36, punkte: liegt(t0, 3, 300, 400, 0) },            // Segler, ausgenommen
    { mmsi: 37, punkte: liegt(t0, 3, 300, 0, 400) }             // noch einer
  ], { 70: 70, 36: 36, 37: 36 });
  await a.lauf();
  assert.strictEqual(a.stillstand(WEITBOX, 24).length, 0,
    "die beiden Segler machen den Ort zum Liegeplatz, auch wenn sie selbst " +
    "nie gemeldet wuerden");
});

test("wer das ganze Fenster ueber liegt, ist Moebel", async () => {
  const jetzt = Math.floor(Date.now() / 1000);
  // Von Fensteranfang bis Fensterende - Kai, Plattform, Hubinsel.
  const a = ruheProxy([{ mmsi: 70, punkte: liegt(jetzt - 24 * 3600, 24, 300) }]);
  await a.lauf();
  assert.strictEqual(a.stillstand(WEITBOX, 24).length, 0);
  assert.ok(a.ruhe.length > 0, "die Phase ist erkannt, nur nicht gemeldet");
  assert.ok(a.ruhe[0].dauerlieger, "und ausdruecklich als Dauerlieger vermerkt");
});

test("ein ausgenommener Typ wird nicht gemeldet, ein anderer schon", async () => {
  const jetzt = Math.floor(Date.now() / 1000);
  const t0 = jetzt - 10 * 3600;
  const segler = ruheProxy([{ mmsi: 36, punkte: liegt(t0, 7, 300) }], { 36: 36 });
  await segler.lauf();
  assert.strictEqual(segler.stillstand(WEITBOX, 24).length, 0, "Segel ist ausgenommen");
  const frachter = ruheProxy([{ mmsi: 70, punkte: liegt(t0, 7, 300) }], { 70: 70 });
  await frachter.lauf();
  assert.strictEqual(frachter.stillstand(WEITBOX, 24).length, 1, "ein Frachter nicht");
});

// --- Der Landfilter --------------------------------------------------------

test("Schleifen in Landnaehe fallen weg, die draussen bleiben", async () => {
  const { Kueste } = require("../src/kueste");
  const kueste = new Kueste({ region: KONFIG.REGION, log: () => {} });
  assert.ok(kueste.da, "ohne Kuestendatei prueft dieser Test nichts: " + kueste.grund);

  const jetzt = Math.floor(Date.now() / 1000);
  // Zwei gleiche Schleifen an verschiedenen Orten. Der Kreis wird um MITTE
  // gebaut; fuer den zweiten Ort wird die Spur verschoben.
  function kreisBei(la, lo) {
    return kreis(600, 40, jetzt - 3000, 60).map(p => [
      p[0], Math.round((la + (p[1] / 1e6 - MITTE.lat)) * 1e6),
      Math.round((lo + (p[2] / 1e6 - MITTE.lon)) * 1e6), p[3], p[4]]);
  }
  const spuren = [
    { mmsi: 70, punkte: kreisBei(54.5, 6.5) },      // offene Nordsee
    { mmsi: 71, punkte: kreisBei(53.8919, 9.1438) } // Elbe bei Glueckstadt
  ];
  const konfig = Object.assign({}, KONFIG, { ANOMALIE_LAND_M: 2000 });
  const mkProxy = (k) => {
    const speicher = { spuren: () => spuren, stammHole: () => ({ typ: 70 }) };
    return new A.Anomalie({ konfig, speicher, zustand: null, kueste: k });
  };
  const mit = mkProxy(kueste);
  await mit.lauf();
  const g = mit.hole({ latMin: 50, lonMin: 0, latMax: 60, lonMax: 15 }, 8);
  const mmsis = g.flatMap(x => x.schiffe).sort();
  assert.deepStrictEqual(mmsis, [70], "nur die Schleife auf offener See bleibt");

  // Gegenprobe OHNE Kuestendatei: dann faellt nichts weg. Ein stiller
  // Totalausfall waere schlimmer als gar kein Filter.
  const ohne = mkProxy(null);
  await ohne.lauf();
  const g2 = ohne.hole({ latMin: 50, lonMin: 0, latMax: 60, lonMax: 15 }, 8);
  assert.deepStrictEqual(g2.flatMap(x => x.schiffe).sort((a, b) => a - b), [70, 71],
    "ohne Kueste bleiben beide");
});

test("der Dauerlieger-Vermerk haengt an den Daten, nicht am Wunschfenster", async () => {
  const jetzt = Math.floor(Date.now() / 1000);
  // Ein Frachter liegt 7 h, waehrend daneben 20 h lang Verkehr laeuft. Die
  // Beobachtung ist damit lang genug fuer ein Urteil - und der Frachter lag
  // NICHT die ganze Zeit da.
  const fahrend = [];
  for (let i = 0; i <= 240; i++) fahrend.push(pkt(jetzt - 20 * 3600 + i * 300, i * 200, 5000, 8));
  const a = ruheProxy([
    { mmsi: 70, punkte: liegt(jetzt - 10 * 3600, 7, 300) },
    { mmsi: 99, punkte: fahrend }
  ]);
  await a.lauf();
  assert.strictEqual(a.stillstand(WEITBOX, 24).length, 1, "wird gemeldet");
  assert.ok(a.ruhe.some(f => !f.dauerlieger));

  // Derselbe Frachter, aber er liegt ueber die GANZE beobachtete Zeit.
  const b = ruheProxy([
    { mmsi: 70, punkte: liegt(jetzt - 20 * 3600, 20, 300) },
    { mmsi: 99, punkte: fahrend }
  ]);
  await b.lauf();
  assert.strictEqual(b.stillstand(WEITBOX, 24).length, 0, "Moebel, keine Meldung");
});

test("bei zu kurzer Beobachtung gilt niemand als Moebel", async () => {
  // Ein frisch gestarteter Proxy hat nur Stunden Historie. Nach dem
  // nominellen 24-h-Fenster waere dann NIEMAND Dauerlieger und jedes
  // festgemachte Schiff im Hafen bekaeme eine Meldung; nach der Datenspanne
  // waere umgekehrt JEDER Moebel. Beides ist falsch - deshalb urteilt der
  // Vermerk erst ab dem Doppelten der Meldeschwelle.
  const jetzt = Math.floor(Date.now() / 1000);
  const a = ruheProxy([{ mmsi: 70, punkte: liegt(jetzt - 7 * 3600, 7, 300) }]);
  await a.lauf();
  assert.ok(a.ruhe.length > 0);
  assert.ok(a.ruhe.every(f => !f.dauerlieger), "kein Urteil bei 7 h Beobachtung");
  assert.strictEqual(a.stillstand(WEITBOX, 24).length, 1, "im Zweifel melden");
});
