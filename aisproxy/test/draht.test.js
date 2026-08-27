"use strict";
const test = require("node:test");
const assert = require("node:assert");
const draht = require("../src/draht");

test("Rundlauf: was hineingeht, kommt heraus", () => {
  const rein = [
    { mmsi: 211900001, lat: 53.7912345, lon: 7.8912345, sog: 12.3, cog: 245.7, hdg: 246, status: 0, flags: 0 },
    { mmsi: 244123456, lat: -33.9, lon: 151.2, sog: 0, cog: 0, hdg: 0, status: 5, flags: draht.FLAG_KLASSE_B }
  ];
  const { rev, schiffe } = draht.entpacke(draht.packe(4711, rein));
  assert.strictEqual(rev, 4711);
  assert.strictEqual(schiffe.length, 2);
  for (let i = 0; i < rein.length; i++) {
    // 1e-6 Grad sind 11 cm - die Rundung darf nicht mehr kosten.
    assert.ok(Math.abs(schiffe[i].lat - rein[i].lat) < 1e-6, "lat " + i);
    assert.ok(Math.abs(schiffe[i].lon - rein[i].lon) < 1e-6, "lon " + i);
    assert.strictEqual(schiffe[i].mmsi, rein[i].mmsi);
    assert.strictEqual(schiffe[i].sog, rein[i].sog);
    assert.strictEqual(schiffe[i].cog, rein[i].cog);
    assert.strictEqual(schiffe[i].status, rein[i].status);
    assert.strictEqual(schiffe[i].flags, rein[i].flags);
  }
});

test("null bleibt null - Sentinels ueberleben die Leitung", () => {
  const { schiffe } = draht.entpacke(draht.packe(1, [
    { mmsi: 1, lat: 0, lon: 0, sog: null, cog: null, hdg: null, status: null, flags: 0 }
  ]));
  assert.strictEqual(schiffe[0].sog, null);
  assert.strictEqual(schiffe[0].cog, null);
  assert.strictEqual(schiffe[0].hdg, null);
  assert.strictEqual(schiffe[0].status, null);
});

test("20 Byte je Schiff, 7 Byte Kopf - die Rechnung im Entwurf haengt daran", () => {
  assert.strictEqual(draht.packe(0, []).length, 7);
  assert.strictEqual(draht.packe(0, [{ mmsi: 1, lat: 0, lon: 0 }]).length, 27);
  const sechshundert = [];
  for (let i = 0; i < 600; i++) sechshundert.push({ mmsi: i, lat: 53, lon: 8 });
  // Der Entwurf verspricht ~12 KB fuer ein Erstbild von 600 Schiffen.
  assert.strictEqual(draht.packe(0, sechshundert).length, 7 + 600 * 20);
  assert.ok(draht.packe(0, sechshundert).length < 12.5 * 1024);
});

test("ein abgeschnittener Rahmen wird erkannt, nicht stillschweigend halb gelesen", () => {
  const voll = draht.packe(9, [{ mmsi: 1, lat: 1, lon: 1 }, { mmsi: 2, lat: 2, lon: 2 }]);
  assert.throws(() => draht.entpacke(voll.subarray(0, voll.length - 5)), /Rahmenlaenge/);
  const falscherTyp = Buffer.from(voll); falscherTyp.writeUInt8(0x99, 0);
  assert.throws(() => draht.entpacke(falscherTyp), /Rahmentyp/);
});

test("Extremwerte laufen nicht ueber", () => {
  const { schiffe } = draht.entpacke(draht.packe(0xffffffff, [
    { mmsi: 4294967295, lat: 89.999999, lon: -179.999999, sog: 999, cog: 359.9, hdg: 359, status: 15, flags: 255 }
  ]));
  assert.strictEqual(schiffe[0].mmsi, 4294967295);
  assert.ok(Math.abs(schiffe[0].lat - 89.999999) < 1e-6);
  assert.ok(Math.abs(schiffe[0].lon + 179.999999) < 1e-6);
  // 999 kn gibt es nicht, aber der Puffer darf davon nicht kaputtgehen.
  assert.ok(schiffe[0].sog > 0);
});
