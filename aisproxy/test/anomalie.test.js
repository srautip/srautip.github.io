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

test("eine Faehre mit echtem Versatz WAERE eine Schleife - sie wird nur nie gelesen", () => {
  // Die Geometrie kann Faehre und Lotse nicht trennen (gemessen:
  // Laenglichkeit BRESLAU 5,92 gegen PILOTVESSEL HANSE 5,44), und genau
  // deshalb versucht sie es gar nicht mehr: Der Lauf fragt nur noch die
  // Lotsenboote ab, eine Faehre kommt nie in den Detektor.
  const g = A.schleifen(pendel(2000, 6, 1000, 60, 60));
  assert.ok(g.length > 0, "erkennbar waeren die Faehrenrunden");
  assert.ok(!A.istLotse(69, "BRESLAU"), "aber die Faehre steht nicht auf der Lotsenliste");
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

// --- Wer als Lotsenboot gilt ------------------------------------------------

test("ein Lotsenboot erkennt man am Typ 50 - oder am Namen", () => {
  assert.ok(A.istLotse(50), "Typ 50 ist die Angabe des Bootes selbst");
  assert.ok(A.istLotse(50, "IRGENDWAS"), "der Typ genuegt, der Name muss nicht passen");

  // Der Grund fuer die Namensregel: Gemessen fahren sechs Lotsenboote der
  // Region unter falschem Typ, und die zweitstaerkste Station (Elbe bei
  // Hamburg, 95 Runden in 24 h) besteht AUSSCHLIESSLICH aus solchen.
  assert.ok(A.istLotse(99, "HAMBURG PILOT 3"), "Typ 99, aber ein Lotsenboot");
  assert.ok(A.istLotse(90, "HAMBURG PILOT 4"));
  assert.ok(A.istLotse(99, "LOTSE 4"));
  assert.ok(A.istLotse(53, "MEES (PILOTS)"));
  assert.ok(A.istLotse(null, "pilot steinburg"), "Gross- und Kleinschreibung egal");

  // Und die Gegenprobe, sonst prueft der Test nur, dass true true ist.
  for (const [t, n] of [[70, "MSC DAKAR X"], [31, "FAIRPLAY-57"], [30, "FRIEDRICH WESSELS"],
                        [69, "BRESLAU"], [36, "SEGLER"], [null, null]]) {
    assert.ok(!A.istLotse(t, n), (n || "ohne Namen") + " ist kein Lotsenboot");
  }
  assert.ok(!A.istLotse(undefined, undefined));
});

test("ausgenommen gilt nur noch fuer den Stillstand", () => {
  for (const t of [36, 60, 61, 64, 69]) {
    assert.ok(A.ausgenommen(t), "typ " + t + " liegt still, ohne gemeldet zu werden");
  }
  // Sportboote (37) waren NICHT gefragt - gefragt waren Segelschiffe.
  for (const t of [37, 30, 50, 70, 80, 90]) {
    assert.ok(!A.ausgenommen(t), "typ " + t + " bleibt drin");
  }
  assert.ok(!A.ausgenommen(null), "ohne Typ wird niemand ausgenommen");
  assert.ok(!A.ausgenommen(undefined));
});

// --- Der Laufbetrieb -------------------------------------------------------

// Der Lotsendurchgang liest je MMSI, nicht mehr in Kacheln. Der Vertreter
// bildet das ab: lotsenMmsis() nennt die Boote, spur() liefert ihre Spur.
// spuren() bleibt fuer den Ruhedurchgang, der weiter in Kacheln liest.
function stellVertreter(spuren) {
  const nachMmsi = new Map(spuren.map(s => [s.mmsi, s.punkte]));
  return {
    lotsenMmsis() { return [...nachMmsi.keys()]; },
    spur(mmsi) { return nachMmsi.get(Number(mmsi)) || []; },
    spuren() { return []; },
    stammHole(mmsi) { return { typ: 50 }; }
  };
}

const KONFIG = {
  REGION: { latMin: 54, lonMin: 8, latMax: 55, lonMax: 9 },
  ANOMALIE_LOTSE_STUNDEN: 24, ANOMALIE_LOTSE_MIN: 1,
  ANOMALIE_KACHEL_GRAD: 1, ANOMALIE_KACHEL_RAND_GRAD: 0.15,
  ANOMALIE_ZUSAMMEN_M: 3000
};

test("der Lauf sammelt die Runden der Lotsenboote und meldet den Stand", async () => {
  const jetzt = Math.floor(Date.now() / 1000);
  const speicher = stellVertreter([{ mmsi: 50, punkte: kreis(600, 40, jetzt - 3000, 60) }]);
  const a = new A.Anomalie({ konfig: KONFIG, speicher, zustand: null });
  await a.lauf(false);
  assert.strictEqual(a.ereignisse.length, 1);
  assert.strictEqual(a.ereignisse[0].mmsi, 50, "die MMSI haengt am Ereignis");
  assert.strictEqual(a.bericht().schleifen, 1);
  assert.strictEqual(a.bericht().lotsen, 1, "der Bericht nennt die Zahl der bekannten Boote");
  assert.ok(a.bericht().lotsenGerechnet, "der Stand traegt eine Zeit");
});

test("wer kein Lotsenboot ist, wird gar nicht erst gelesen", async () => {
  // Der Kern des Rueckbaus: Frueher las der Lauf ALLE Spuren der Region und
  // siebte hinterher. Jetzt fragt er nur die Lotsenboote - ein Schlepper,
  // der Runden dreht, kommt nicht einmal in die Naehe des Detektors.
  const jetzt = Math.floor(Date.now() / 1000);
  const gelesen = [];
  const speicher = {
    lotsenMmsis() { return [50]; },
    spur(mmsi) {
      gelesen.push(Number(mmsi));
      return kreis(600, 40, jetzt - 3000, 60);
    },
    spuren() { return []; },
    stammHole() { return { typ: 50 }; }
  };
  const a = new A.Anomalie({ konfig: KONFIG, speicher, zustand: null });
  await a.lauf(false);
  assert.deepStrictEqual(gelesen, [50], "genau eine Spur gelesen, die des Lotsen");
  assert.strictEqual(a.ereignisse.length, 1);
});

test("der heisse Zustand ergaenzt die Lotsenliste aus dem Speicher", async () => {
  // Ein eben aufgetauchtes Boot hat seinen Stammsatz noch nicht - es steht
  // aber schon im heissen Zustand, und dort auch mit seinem Namen.
  const jetzt = Math.floor(Date.now() / 1000);
  const speicher = {
    lotsenMmsis() { return [50]; },
    spur() { return kreis(600, 40, jetzt - 3000, 60); },
    spuren() { return []; },
    stammHole() { return { typ: 50 }; }
  };
  const zustand = { schiffe: new Map([
    [99, { typ: 99, name: "HAMBURG PILOT 3" }],     // falscher Typ, richtiger Name
    [70, { typ: 70, name: "MSC DAKAR X" }]          // Frachter, gehoert nicht dazu
  ]) };
  const a = new A.Anomalie({ konfig: KONFIG, speicher, zustand });
  assert.deepStrictEqual(a.lotsenListe().sort((x, y) => x - y), [50, 99]);
});

test("lotsenGebiete() begrenzt Fenster, Ausschnitt und Mindestzahl", async () => {
  const jetzt = Math.floor(Date.now() / 1000);
  const speicher = stellVertreter([
    { mmsi: 50, punkte: kreis(600, 40, jetzt - 3000, 60) },
    { mmsi: 51, punkte: kreis(600, 40, jetzt - 3000, 60, 20000, 0) }   // 20 km weiter
  ]);
  const konfig = Object.assign({}, KONFIG, { ANOMALIE_LOTSE_MIN: 1 });
  const a = new A.Anomalie({ konfig, speicher, zustand: null });
  await a.lauf(false);
  const box = { latMin: 53, lonMin: 7, latMax: 56, lonMax: 10 };
  const g = a.lotsenGebiete(box, 24);
  assert.strictEqual(g.length, 2, "zwei getrennte Gebiete");
  assert.ok(g.every(x => x.stufe === undefined), "keine Einstufung mehr - alle sind Lotsen");

  // Ein kurzes Fenster laesst die aeltere Runde draussen.
  assert.strictEqual(a.lotsenGebiete(box, 0.1).length, 0,
    "vor 50 Minuten ist ausserhalb von 6 Minuten");
  // Und eine bbox neben den Runden liefert nichts.
  assert.strictEqual(a.lotsenGebiete({ latMin: 40, lonMin: 0, latMax: 41, lonMax: 1 }, 24).length, 0);
});

test("die Mindestzahl wirft Einzelrunden weg und laesst Stationen stehen", async () => {
  // Gemessen beruhen 18 von 58 Gebieten auf genau EINER Runde - ein Boot auf
  // dem Weg, keine Station. Zwei Runden nebeneinander sind eine Station.
  const jetzt = Math.floor(Date.now() / 1000);
  // Ein Boot dreht drei Runden am selben Ort, ein zweites eine einzige,
  // 20 km entfernt.
  const drei = [];
  for (let k = 0; k < 3; k++) drei.push(...kreis(600, 40, jetzt - 9000 + k * 2700, 60));
  const speicher = stellVertreter([
    { mmsi: 50, punkte: drei },
    { mmsi: 51, punkte: kreis(600, 40, jetzt - 3000, 60, 20000, 0) }
  ]);
  const box = { latMin: 53, lonMin: 7, latMax: 56, lonMax: 10 };

  const alle = new A.Anomalie({
    konfig: Object.assign({}, KONFIG, { ANOMALIE_LOTSE_MIN: 1 }), speicher, zustand: null });
  await alle.lauf(false);
  const g1 = alle.lotsenGebiete(box, 24);
  assert.strictEqual(g1.length, 2, "ohne Schwelle sind es zwei Gebiete");
  assert.ok(g1.some(g => g.schleifen >= 3), "eines hat drei Runden");
  assert.ok(g1.some(g => g.schleifen === 1), "das andere genau eine");

  const ab3 = new A.Anomalie({
    konfig: Object.assign({}, KONFIG, { ANOMALIE_LOTSE_MIN: 3 }), speicher, zustand: null });
  await ab3.lauf(false);
  const g2 = ab3.lotsenGebiete(box, 24);
  assert.strictEqual(g2.length, 1, "mit Schwelle bleibt nur die Station");
  assert.ok(g2[0].schleifen >= 3);
  // Die Gegenprobe zur Schwelle selbst: Bei 4 faellt auch die Station weg.
  const ab4 = new A.Anomalie({
    konfig: Object.assign({}, KONFIG, { ANOMALIE_LOTSE_MIN: 99 }), speicher, zustand: null });
  await ab4.lauf(false);
  assert.strictEqual(ab4.lotsenGebiete(box, 24).length, 0, "die Schwelle wirkt wirklich");
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
  ANOMALIE_STILL_RAND_S: 3600, ANOMALIE_STILL_VORLAUF_S: 24 * 3600,
  FAHRT_KN: 0.5
});
const WEITBOX = { latMin: 40, lonMin: 0, latMax: 60, lonMax: 20 };

// vorfahrt: Punkte VOR dem Datenrand je MMSI - damit laesst sich pruefen, ob
// der Detektor die Ankunft in der Historie findet. Ohne Eintrag gilt ein
// Schiff als "nie in Fahrt gesehen", also als Moebel.
function ruheProxy(spuren, typen, vorfahrt) {
  const speicher = {
    // Der Ruhedurchgang liest weiter in Kacheln - er BRAUCHT alle liegenden
    // Schiffe, denn die Grundlinie entsteht erst aus ihnen.
    spuren() { return spuren; },
    // Der Lotsendurchgang liest je MMSI. Hier ist kein Lotsenboot dabei, er
    // findet also nichts - das ist der Punkt dieser Proben.
    lotsenMmsis() { return []; },
    spur(mmsi) { return (vorfahrt || {})[mmsi] || []; },
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

// --- Der eigene Takt fuer den Ruhedurchgang --------------------------------

test("lauf(false) laesst den letzten Ruhestand stehen, statt ihn zu leeren", async () => {
  // Der Ruhedurchgang laeuft seltener als die Schleifensuche. Wuerde ein
  // gewoehnlicher Lauf this.ruhe leeren, waere die Ebene zwischen zwei
  // Ruhelaeufen stumm - und niemand saehe, warum.
  const jetzt = Math.floor(Date.now() / 1000);
  const spuren = [{ mmsi: 70, punkte: liegt(jetzt - 10 * 3600, 7, 300) }];
  let ruheGelesen = 0;
  const speicher = {
    spuren() { ruheGelesen++; return spuren; },
    lotsenMmsis() { return []; },
    spur() { return []; },
    stammHole() { return { typ: 70 }; }
  };
  const a = new A.Anomalie({ konfig: STILL_KONFIG, speicher, zustand: null });
  await a.lauf(true);
  const vorher = a.stillstand(WEITBOX, 24).length;
  assert.strictEqual(vorher, 1, "erst einmal mit Ruhedurchgang");
  const stand = a.ruheGerechnet;
  const gelesen = ruheGelesen;
  assert.ok(gelesen > 0, "der Ruhedurchgang hat wirklich gelesen");

  await a.lauf(false);
  assert.strictEqual(ruheGelesen, gelesen,
    "und lauf(false) liest die Kacheln gar nicht erst");
  assert.strictEqual(a.stillstand(WEITBOX, 24).length, vorher,
    "nach einem Lauf ohne Ruhedurchgang steht der Stillstand noch");
  assert.strictEqual(a.ruheGerechnet, stand,
    "und sein Zeitstempel wandert nicht mit - sonst saehe er aktueller aus, als er ist");
  assert.ok(a.lotsenGerechnet > stand || a.lotsenGerechnet > 0,
    "der Lotsenstand ist dagegen frisch");
});

// --- Moebel: nur der Anfang zaehlt -----------------------------------------

test("Moebel ist, wer schon dalag - auch wenn er inzwischen weg ist", async () => {
  // Zuerst verlangte der Vermerk "am Anfang da UND am Ende noch da". An der
  // laufenden Anlage scheiterte die zweite Bedingung in 24 von 92 Faellen an
  // Meldeluecken. Die Frage ist aber nur: Haben wir das Schiff ankommen sehen?
  const jetzt = Math.floor(Date.now() / 1000);
  const fahrend = [];
  for (let k = 0; k <= 240; k++) fahrend.push(pkt(jetzt - 20 * 3600 + k * 300, k * 200, 5000, 8));

  // Liegt ab Beobachtungsbeginn, hoert aber 8 h vor dem Ende auf zu melden.
  const frueh = ruheProxy([
    { mmsi: 70, punkte: liegt(jetzt - 20 * 3600, 12, 300) },
    { mmsi: 99, punkte: fahrend }
  ]);
  await frueh.lauf();
  assert.strictEqual(frueh.stillstand(WEITBOX, 24).length, 0,
    "wir haben ihn nie ankommen sehen - keine Meldung");
  assert.ok(frueh.ruhe.some(f => f.dauerlieger));

  // Gegenprobe: Dasselbe Schiff, aber es kommt MITTEN in der Beobachtung an.
  const spaet = ruheProxy([
    { mmsi: 70, punkte: liegt(jetzt - 12 * 3600, 12, 300) },
    { mmsi: 99, punkte: fahrend }
  ]);
  await spaet.lauf();
  assert.strictEqual(spaet.stillstand(WEITBOX, 24).length, 1,
    "hier haben wir die Ankunft gesehen - Meldung");
});

test("die Toleranz von einer Stunde greift, und nicht mehr", async () => {
  const jetzt = Math.floor(Date.now() / 1000);
  const fahrend = [];
  for (let k = 0; k <= 240; k++) fahrend.push(pkt(jetzt - 20 * 3600 + k * 300, k * 200, 5000, 8));
  const nach = async (minuten) => {
    const a = ruheProxy([
      { mmsi: 70, punkte: liegt(jetzt - 20 * 3600 + minuten * 60, 8, 300) },
      { mmsi: 99, punkte: fahrend }
    ]);
    await a.lauf();
    return a.ruhe.find(f => f.mmsi === 70).dauerlieger;
  };
  assert.strictEqual(await nach(50), true, "50 min nach Beginn gilt noch als 'lag schon da'");
  assert.strictEqual(await nach(70), false, "70 min nach Beginn nicht mehr");
});

test("wer am Datenrand liegt, aber vorher in Fahrt war, IST angekommen", () => {
  // Der Fall HMM ALGECIRAS: 16,4 h vor Anker, Beginn 18 Sekunden nach dem
  // Datenrand. Nach der blossen Randregel waere das Moebel gewesen - und
  // damit ausgerechnet der interessanteste Fall im Datensatz verschwunden.
  // Die Historie entscheidet: Ein Kai hat keine Anfahrt, ein Ankerlieger schon.
  return (async () => {
    const jetzt = Math.floor(Date.now() / 1000);
    const fahrend = [];
    for (let k = 0; k <= 240; k++) fahrend.push(pkt(jetzt - 20 * 3600 + k * 300, k * 200, 5000, 8));
    const amRand = [
      { mmsi: 70, punkte: liegt(jetzt - 20 * 3600, 12, 300) },
      { mmsi: 99, punkte: fahrend }
    ];
    // Ohne Anfahrt in der Historie: Moebel.
    const ohne = ruheProxy(amRand);
    await ohne.lauf();
    assert.strictEqual(ohne.stillstand(WEITBOX, 24).length, 0, "keine Anfahrt -> Moebel");

    // Mit Anfahrt: angekommen, also Meldung. Der einzige Unterschied ist die
    // Historie - dieselben Ruhedaten, dasselbe Fenster.
    const mit = ruheProxy(amRand, null, {
      70: [[jetzt - 26 * 3600, 54000000, 8000000, 80, 0],
           [jetzt - 25 * 3600, 54010000, 8010000, 75, 0]]
    });
    await mit.lauf();
    assert.strictEqual(mit.stillstand(WEITBOX, 24).length, 1, "mit Anfahrt -> Meldung");
    assert.ok(mit.ruhe.find(f => f.mmsi === 70).angekommen);

    // Gegenprobe: Eine Historie voller LIEGENDER Punkte ist keine Anfahrt.
    const still = ruheProxy(amRand, null, {
      70: [[jetzt - 26 * 3600, 54000000, 8000000, 0, 0],
           [jetzt - 25 * 3600, 54000000, 8000000, 2, 0]]
    });
    await still.lauf();
    assert.strictEqual(still.stillstand(WEITBOX, 24).length, 0,
      "nur liegende Punkte davor -> weiterhin Moebel");
  })();
});
