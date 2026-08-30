"use strict";
// Die Kuestenlinie der Region als kleine Datei - EINMAL erzeugt, danach liegt
// sie im Repo (karten/kueste.json) und der Proxy liest nur noch die.
//
// NICHT unter daten/ - das steht in .gitignore, weil dort die Datenbank
// liegt. Eine Kuestendatei, die stillschweigend nie mitcommittet wird, waere
// ein Landfilter, der auf dem Server einfach aus ist.
//
//   node werkzeug/kueste-bauen.js /pfad/zu/land_polygons.shp [daten/kueste.json]
//
// Quelle: https://osmdata.openstreetmap.de/data/land-polygons.html
//   -> land-polygons-split-4326.zip (925 MB, EPSG:4326, volle Aufloesung)
//
// Warum ausgerechnet die grosse Datei - drei kleinere sind gemessen
// durchgefallen (30. Aug. 2026):
//
//   Natural Earth 10m (10 MB)        Die Elbe fehlt. Der Hamburger Hafen laege
//                                    "7,9 km von Land", und genau dort liegen
//                                    63 Hafenfaehren.
//   OSM simplified (24 MB)           Noch schlimmer: Die Vereinfachung hat die
//                                    Elbe ganz geschluckt, Hamburg "20 km".
//   Overpass API                     Nicht erreichbar, und als Abhaengigkeit
//                                    zur Laufzeit ohnehin fragil.
//
// Ein vierter Weg klang nach diesem Projekt und war trotzdem falsch: "Wasser
// ist, wo Schiffe fahren", also die Maske aus den eigenen AIS-Daten bauen.
// Gemessen sind nur 4 % der Regionszellen je befahren - die naechste
// unbefahrene Zelle liegt selbst mitten in der Nordsee 280 m entfernt. Die
// Regel haette alles weggeworfen.
//
// ZWEI FALLSTRICKE, beide beim Bauen aufgetreten und beide teuer:
//
//   1. OSM fuehrt Fluesse oberhalb der Kuestenlinie ALS LAND - die Elbe ab
//      Cuxhaven, die Weser ab Bremerhaven, den Nord-Ostsee-Kanal, die Foerden.
//      Ein reiner Abstand-zur-Kante-Test meldete fuer den Hamburger Hafen
//      "3,16 km", weil der Punkt INNEN liegt und bis zum Rand wirklich so weit
//      ist. Es braucht deshalb zusaetzlich einen Punkt-in-Polygon-Test.
//   2. Die Polygone sind KACHELWEISE ZERSCHNITTEN. Ein Strahlentest ueber den
//      beschnittenen Segmentsatz erklaerte die offene Nordsee zu Land. Gebraucht
//      wird der Test je VOLLSTAENDIGEM Ring - deshalb schreibt diese Datei
//      Ringe, keine losen Segmente.
const fs = require("node:fs");
const path = require("node:path");

// Region plus Rand. Der Rand muss groesser sein als die groesste Schwelle,
// mit der spaeter gefragt wird - sonst faende ein Punkt am Regionsrand keine
// Kueste und gaelte als "weit draussen".
const RAND_GRAD = 0.4;
// Douglas-Peucker. Gemessen: 100 m ergibt 20 483 von 404 091 Punkten und
// 390 KB (25 m waeren 1 083 KB, 50 m 654 KB). Gegen eine 2-km-Schwelle ist
// ein Vereinfachungsfehler von 100 m belanglos.
const TOLERANZ_M = 100;

function bogen(pts, tol, von, bis, behalten) {
  if (bis <= von + 1) return;
  const [ax, ay] = pts[von], [bx, by] = pts[bis];
  const dx = bx - ax, dy = by - ay, L = dx * dx + dy * dy;
  let best = 0, bi = -1;
  for (let k = von + 1; k < bis; k++) {
    const [px, py] = pts[k];
    const t = L === 0 ? 0 : Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / L));
    const d = Math.hypot(px - (ax + t * dx), py - (ay + t * dy));
    if (d > best) { best = d; bi = k; }
  }
  if (best <= tol) return;
  behalten[bi] = true;
  bogen(pts, tol, von, bi, behalten);
  bogen(pts, tol, bi, bis, behalten);
}

// Douglas-Peucker auf einem GESCHLOSSENEN Ring braucht zwei Anker, nicht
// einen. Mit nur Anfang und Ende faellt beides auf denselben Punkt, die
// Sehne hat die Laenge null, und uebrig bleiben drei Punkte - also ein
// entarteter Strich, der anschliessend verworfen wird. Gemessen kostete das
// 535 Ringe, davon 37 groesser als 200 m und der groesste 1 373 m: echte
// Inseln, neben denen ein Schiff dann nicht mehr "in Landnaehe" gewesen
// waere. Deshalb wird zuerst der vom Anfang am weitesten entfernte Punkt
// gesucht und der Ring an beiden Ankern in zwei Boegen geteilt.
function vereinfache(pts, tol) {
  if (pts.length < 4) return pts;
  const geschlossen = pts[0][0] === pts[pts.length - 1][0] &&
                      pts[0][1] === pts[pts.length - 1][1];
  const behalten = new Array(pts.length).fill(false);
  behalten[0] = behalten[pts.length - 1] = true;
  if (geschlossen) {
    let fern = 0, weit = -1;
    for (let i = 1; i < pts.length - 1; i++) {
      const d = Math.hypot(pts[i][0] - pts[0][0], pts[i][1] - pts[0][1]);
      if (d > fern) { fern = d; weit = i; }
    }
    if (weit < 0) return pts;
    behalten[weit] = true;
    bogen(pts, tol, 0, weit, behalten);
    bogen(pts, tol, weit, pts.length - 1, behalten);
  } else {
    bogen(pts, tol, 0, pts.length - 1, behalten);
  }
  return pts.filter((_, i) => behalten[i]);
}

function ringeLesen(datei, box) {
  const buf = fs.readFileSync(datei);
  const aus = [];
  let off = 100;                     // 100 Byte Dateikopf
  while (off + 8 <= buf.length) {
    const laenge = buf.readInt32BE(off + 4);       // in 16-Bit-Woertern
    off += 8;
    const typ = buf.readInt32LE(off);
    if (typ !== 5) { off += laenge * 2; continue; }   // 5 = Polygon
    const xmin = buf.readDoubleLE(off + 4), ymin = buf.readDoubleLE(off + 12);
    const xmax = buf.readDoubleLE(off + 20), ymax = buf.readDoubleLE(off + 28);
    if (xmax < box.lonMin || xmin > box.lonMax ||
        ymax < box.latMin || ymin > box.latMax) { off += laenge * 2; continue; }
    const nteile = buf.readInt32LE(off + 36);
    const npunkte = buf.readInt32LE(off + 40);
    const teile = [];
    for (let i = 0; i < nteile; i++) teile.push(buf.readInt32LE(off + 44 + i * 4));
    const p0 = off + 44 + nteile * 4;
    for (let k = 0; k < nteile; k++) {
      const i0 = teile[k], i1 = k + 1 < nteile ? teile[k + 1] : npunkte;
      const ring = [];
      for (let i = i0; i < i1; i++) {
        ring.push([buf.readDoubleLE(p0 + i * 16), buf.readDoubleLE(p0 + i * 16 + 8)]);
      }
      aus.push(ring);
    }
    off += laenge * 2;
  }
  return aus;
}

function main() {
  const quelle = process.argv[2];
  const ziel = process.argv[3] ||
    path.join(__dirname, "..", "karten", "kueste.json");
  if (!quelle) {
    console.error("Aufruf: node werkzeug/kueste-bauen.js <land_polygons.shp> [ziel.json]");
    console.error("Quelle: https://osmdata.openstreetmap.de/download/land-polygons-split-4326.zip");
    process.exit(1);
  }
  // Die Box kommt aus der Konfiguration, damit Datei und Region nicht
  // auseinanderlaufen koennen.
  const konfig = require("../src/konfig");
  const box = {
    latMin: konfig.REGION.latMin - RAND_GRAD, lonMin: konfig.REGION.lonMin - RAND_GRAD,
    latMax: konfig.REGION.latMax + RAND_GRAD, lonMax: konfig.REGION.lonMax + RAND_GRAD
  };

  const t0 = Date.now();
  const ringe = ringeLesen(quelle, box);
  const roh = ringe.reduce((n, r) => n + r.length, 0);
  const tol = TOLERANZ_M / 111320;
  // Mindestens DREI Punkte, nicht vier. Eine lange schmale Sandbank faellt
  // bei 100 m Toleranz zu Recht auf eine Linie zusammen - sie ist damit kein
  // Polygon mehr, aber immer noch Land. Wer sie hier wegwirft, macht ein
  // Schiff daneben "nicht landnah": gemessen waren das 37 Gebilde ueber 200 m,
  // das groesste 1 373 m. Fuer den Abstand zaehlen ihre Kanten weiter; beim
  // Punkt-in-Polygon-Test faellt eine Linie von selbst durch.
  const klein = ringe.map(r => vereinfache(r, tol)).filter(r => r.length >= 3);
  const nachher = klein.reduce((n, r) => n + r.length, 0);

  const inhalt = {
    quelle: "OpenStreetMap land polygons (osmdata.openstreetmap.de), ODbL",
    erzeugt: new Date().toISOString().slice(0, 10),
    toleranzM: TOLERANZ_M,
    // Was diese Datei abdeckt. src/kueste.js prueft es gegen die Region und
    // schaltet den Landfilter ab, statt still falsch zu filtern.
    box,
    ringe: klein.map(r => r.map(([x, y]) => [Number(x.toFixed(5)), Number(y.toFixed(5))]))
  };
  fs.mkdirSync(path.dirname(ziel), { recursive: true });
  fs.writeFileSync(ziel, JSON.stringify(inhalt));
  const kb = Math.round(fs.statSync(ziel).size / 1024);
  console.log(`${ringe.length} Ringe, ${roh} Punkte -> ${klein.length} Ringe, ` +
    `${nachher} Punkte (${Math.round(nachher / roh * 100)} %), ${kb} KB, ` +
    `${((Date.now() - t0) / 1000).toFixed(1)} s`);
  console.log(`  Box: ${box.latMin}..${box.latMax} N, ${box.lonMin}..${box.lonMax} O`);
  console.log(`  -> ${ziel}`);
}

if (require.main === module) main();
module.exports = { ringeLesen, vereinfache };
