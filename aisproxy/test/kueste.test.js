"use strict";
// Der Landabstand an echten Orten.
//
// Diese Probe ist an einer Stelle ungewoehnlich: Sie prueft gegen die
// MITGELIEFERTE Datei, nicht gegen gestellte Geometrie. Das ist Absicht -
// der Fehler, den es zu verhindern gilt, steckte nicht in der Formel, sondern
// darin, WELCHE Daten sie bekommt. Zwei kleinere Kuestenquellen sind daran
// gescheitert (Natural Earth 10m und die simplifizierte OSM-Fassung loesen die
// Elbe nicht auf), und beide haetten jede rein rechnerische Probe bestanden.
const test = require("node:test");
const assert = require("node:assert");
const path = require("node:path");
const { Kueste } = require("../src/kueste");

const REGION = { latMin: 53, lonMin: 6, latMax: 56, lonMax: 13 };
const k = new Kueste({ region: REGION, log: () => {} });

// Wenn die Datei fehlt, sollen die Pruefungen das SAGEN und nicht still
// durchlaufen - eine gruene Probe ohne Daten waere die schlechteste Auskunft.
test("die mitgelieferte Kuestendatei ist da und deckt die Region ab", () => {
  assert.ok(k.da, "kueste.json nicht nutzbar: " + k.grund);
  assert.ok(k.ringe.length > 500, "zu wenige Ringe: " + k.ringe.length);
  assert.ok(k.box.latMin <= REGION.latMin && k.box.latMax >= REGION.latMax &&
            k.box.lonMin <= REGION.lonMin && k.box.lonMax >= REGION.lonMax);
});

// Die Tabelle, an der die Datenquelle entschieden wurde. Jede Zeile hat einen
// Grund: die linke Spalte muss weg, die rechte muss bleiben.
const NAH = [
  ["Elbfaehre bei Glueckstadt", 53.8919, 9.1438],
  ["Hamburger Hafen", 53.5399, 9.9514],
  ["Bremerhaven", 53.5309, 8.5631],
  ["Emden", 53.3393, 7.1859],
  ["Nord-Ostsee-Kanal", 54.1, 9.5],
  ["Kieler Foerde", 54.42, 10.16],
  ["Weser bei Bremen", 53.0993, 8.6853],
  ["Reede vor Helgoland", 54.18, 7.90]
];
const FERN = [
  ["Lotsenstation Weser", 53.8692, 7.8723],
  ["Lotsen Elbe-Anfahrt", 54.0026, 8.2539],
  ["GENTLE LEADER, gemeldete Schleife", 53.8520, 7.4752],
  ["HMM ALGECIRAS vor Anker", 54.0710, 7.7444],
  ["offene Nordsee", 54.5, 6.5]
];

test("landnah ist, was landnah ist - und nur das", () => {
  for (const [name, la, lo] of NAH) {
    assert.ok(k.landabstand(la, lo, 20000) < 2000,
      name + " muesste unter 2 km liegen, ist " +
      Math.round(k.landabstand(la, lo, 20000)) + " m");
  }
  for (const [name, la, lo] of FERN) {
    assert.ok(k.landabstand(la, lo, 20000) >= 2000,
      name + " muesste ueber 2 km liegen, ist " +
      Math.round(k.landabstand(la, lo, 20000)) + " m");
  }
});

test("der Punkt-in-Polygon-Test ist es, der die Fluesse erwischt", () => {
  // Ohne ihn meldete der Hamburger Hafen 3,16 km - die Entfernung zum weit
  // entfernten Rand desselben Landpolygons. Genau diese Zeile faellt durch,
  // wenn jemand anLand() aus landabstand() entfernt.
  assert.ok(k.anLand(53.5399, 9.9514), "Hamburger Hafen liegt im Landpolygon");
  assert.strictEqual(k.landabstand(53.5399, 9.9514, 20000), 0);
  assert.ok(k.anLand(54.1, 9.5), "Nord-Ostsee-Kanal ebenso");
  // Und die Gegenprobe: offene See ist NICHT an Land. Ein Strahlentest ueber
  // beschnittene Polygone hat hier einmal "ja" gesagt.
  assert.ok(!k.anLand(54.5, 6.5), "die offene Nordsee ist kein Land");
  assert.ok(!k.anLand(53.8692, 7.8723), "die Lotsenstation auch nicht");
});

test("der Deckel begrenzt die Auskunft, nicht die Wahrheit", () => {
  // Weit draussen wird nicht weitergesucht - die Zahl ist dann der Deckel.
  assert.strictEqual(k.landabstand(54.5, 6.5, 5000), 5000);
  assert.ok(k.landabstand(54.5, 6.5, 20000) > 19000);
  // Und in Kuestennaehe ist der Deckel wirkungslos.
  const d = k.landabstand(53.7161, 7.4663, 20000);   // KAYLEE bei Langeoog
  assert.ok(d > 0 && d < 2000, "Langeoog: " + Math.round(d) + " m");
});

test("ohne Daten wird NICHT gefiltert, statt alles wegzuwerfen", () => {
  const leer = new Kueste({ datei: path.join(__dirname, "gibtesnicht.json"),
                            region: REGION, log: () => {} });
  assert.strictEqual(leer.da, false);
  assert.match(leer.grund, /fehlt|unlesbar/);
  // Der Rueckgabewert ist der Deckel, nicht 0: So faellt keine einzige
  // Schleife weg, wenn die Datei fehlt. Andersherum waere die Karte leer und
  // niemand wuesste warum.
  assert.strictEqual(leer.landabstand(53.5399, 9.9514, 20000), 20000);
  assert.strictEqual(leer.anLand(53.5399, 9.9514), false);
});

test("deckt die Datei die Region nicht ab, wird der Filter abgeschaltet", () => {
  const zuGross = { latMin: 40, lonMin: -10, latMax: 70, lonMax: 30 };
  const gross = new Kueste({ region: zuGross, log: () => {} });
  assert.strictEqual(gross.da, false, "muss sich abschalten");
  assert.match(gross.grund, /deckt die Region nicht ab/);
  assert.match(gross.grund, /kueste-bauen/, "und den Weg nennen");
});
