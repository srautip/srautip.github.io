"use strict";
const test = require("node:test");
const assert = require("node:assert");
const { Zustand } = require("../src/zustand");

const BOX = { latMin: 53, lonMin: 6, latMax: 56, lonMax: 13 };

function neu() { return new Zustand({ ttlMs: 60000 }); }

test("rev waechst nur bei echter Aenderung", () => {
  const z = neu();
  z.melde(1, { lat: 54, lon: 8, sog: 5 }, 1000, "strom");
  const nach1 = z.rev;
  assert.ok(nach1 > 0, "erste Meldung setzt rev");

  // Dieselben Werte noch einmal - nichts hat sich geaendert.
  const r = z.melde(1, { lat: 54, lon: 8, sog: 5 }, 1000, "strom");
  assert.strictEqual(r, null, "unveraenderte Meldung liefert null");
  assert.strictEqual(z.rev, nach1, "und laesst rev in Ruhe");

  z.melde(1, { lat: 54.001, lon: 8, sog: 5 }, 2000, "strom");
  assert.ok(z.rev > nach1, "echte Aenderung zaehlt hoch");
});

test("Deltas: nur was sich seit rev geaendert hat", () => {
  const z = neu();
  z.melde(1, { lat: 54, lon: 8 }, 1000, "strom");
  z.melde(2, { lat: 54.5, lon: 8.5 }, 1000, "strom");
  const stand = z.rev;
  z.melde(2, { lat: 54.6, lon: 8.5 }, 2000, "strom");

  const delta = z.imAusschnitt(BOX, stand);
  assert.strictEqual(delta.length, 1, "nur das bewegte Schiff");
  assert.strictEqual(delta[0].mmsi, 2);

  // Gegenprobe: ohne seitRev kommen beide.
  assert.strictEqual(z.imAusschnitt(BOX).length, 2);
});

test("eine aeltere Positionsmeldung darf die neuere nicht ueberschreiben", () => {
  const z = neu();
  z.melde(1, { lat: 54.5, lon: 8 }, 5000, "strom");
  // Das Netz liefert dasselbe Schiff mit einem aelteren Stand nach.
  z.melde(1, { lat: 54.0, lon: 8 }, 3000, "netz");
  assert.strictEqual(z.hole(1).lat, 54.5, "die frischere Position bleibt stehen");
  assert.ok(z.zaehler.verworfen > 0, "und der Vorgang wird gezaehlt");

  // Gegenprobe: Stammdaten aus derselben alten Meldung duerfen trotzdem durch,
  // denn ein Name veraltet nicht.
  z.melde(1, { lat: 54.0, lon: 8, name: "SPAETER NAME" }, 3000, "netz");
  assert.strictEqual(z.hole(1).name, "SPAETER NAME");
  assert.strictEqual(z.hole(1).lat, 54.5);
});

test("Ausschnitt filtert wirklich", () => {
  const z = neu();
  z.melde(1, { lat: 54, lon: 8 }, 1000, "strom");      // drin
  z.melde(2, { lat: 60, lon: 8 }, 1000, "strom");      // zu weit noerdlich
  z.melde(3, { lat: 54, lon: 20 }, 1000, "strom");     // zu weit oestlich
  assert.deepStrictEqual(z.imAusschnitt(BOX).map(s => s.mmsi), [1]);
});

test("verlassen() meldet nur, was der Client auch hat", () => {
  const z = neu();
  z.melde(1, { lat: 54, lon: 8 }, 1000, "strom");
  z.melde(2, { lat: 54, lon: 8 }, 1000, "strom");
  const stand = z.rev;
  z.melde(1, { lat: 60, lon: 8 }, 2000, "strom");      // faehrt aus der Box
  z.melde(2, { lat: 61, lon: 8 }, 2000, "strom");      // ebenso

  const bekannt = new Set([1]);
  assert.deepStrictEqual(z.verlassen(BOX, stand, bekannt), [1],
    "nur das dem Client bekannte Schiff");

  // Gegenprobe ohne Filter: Das ist genau der Fall, der hunderte MMSIs je
  // Takt schicken wuerde.
  assert.strictEqual(z.verlassen(BOX, stand).length, 2);
});

test("TTL raeumt auf und meldet, was weg ist", () => {
  const z = new Zustand({ ttlMs: 1000 });
  z.melde(1, { lat: 54, lon: 8 }, Date.now() - 5000, "strom");
  z.melde(2, { lat: 54, lon: 8 }, Date.now(), "strom");
  const weg = z.aufraeumen(Date.now());
  assert.deepStrictEqual(weg, [1]);
  assert.strictEqual(z.anzahl, 1);
});

test("ergaenze() traegt Registerdaten nach und hebt rev", () => {
  const z = neu();
  z.melde(1, { lat: 54, lon: 8 }, 1000, "strom");
  const stand = z.rev;
  assert.ok(z.ergaenze(1, { name: "TESTSCHIFF", imo: 1234567 }));
  assert.ok(z.rev > stand, "damit verbundene Clients es ohne Nachfrage bekommen");
  assert.strictEqual(z.hole(1).name, "TESTSCHIFF");
  assert.strictEqual(z.ergaenze(1, { name: "TESTSCHIFF" }), null, "nichts Neues, nichts passiert");
  assert.strictEqual(z.ergaenze(999, { name: "X" }), null, "unbekannte MMSI");
});

test("lineares Durchsehen traegt die Regionsgroesse - deshalb kein zweiter Index", () => {
  // Der Entwurf sah ein Ortsgitter auch im heissen Zustand vor. Diese Probe
  // ist die Begruendung, warum es nicht eingebaut wurde: Bei 2915 Schiffen
  // (der gemessenen Groesse der Region) kostet ein voller Durchlauf so wenig,
  // dass ein zweiter, bei jeder Positionsaenderung zu pflegender Index mehr
  // Fehlerquelle als Gewinn waere.
  const z = neu();
  for (let i = 0; i < 2915; i++) {
    z.melde(200000000 + i, { lat: 53 + (i % 300) / 100, lon: 6 + (i % 700) / 100 }, 1000, "strom");
  }
  const t0 = process.hrtime.bigint();
  let n = 0;
  for (let i = 0; i < 100; i++) n += z.imAusschnitt(BOX, z.rev - 50).length;
  const jeDurchlauf = Number(process.hrtime.bigint() - t0) / 1e6 / 100;
  console.log("      Durchsehen von " + z.anzahl + " Schiffen: " +
    jeDurchlauf.toFixed(3) + " ms je Durchlauf");
  assert.ok(jeDurchlauf < 5, "unter 5 ms (gemessen " + jeDurchlauf.toFixed(3) + ")");
  assert.ok(n >= 0);
});
