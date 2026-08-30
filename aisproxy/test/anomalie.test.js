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
function pkt(t, dxM, dyM, sogKn) {
  return [t, Math.round((MITTE.lat + dyM / KY) * 1e6),
          Math.round((MITTE.lon + dxM / KX) * 1e6), Math.round((sogKn || 5) * 10), 0];
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
