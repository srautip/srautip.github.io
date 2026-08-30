"use strict";

// Wie weit ist ein Punkt vom Land entfernt?
//
// Gebraucht wird das, um Schleifen in Landnaehe zu verwerfen: Faehren fahren
// staendig Schleifen, und sie tun es dicht unter Land. Gemessen an 1 359
// echten Schleifen faellt mit einer 2-km-Schwelle 97 % des Faehrverkehrs weg
// (454 von 466), ohne dass irgendwo "Faehre" steht.
//
// Die Daten liegen als karten/kueste.json im Repo; wie sie entstehen und warum
// ausgerechnet aus dieser Quelle, steht in werkzeug/kueste-bauen.js.
//
// ZWEI DINGE, an denen ich beim Bauen zweimal falsch lag:
//
//   1. OSM fuehrt Fluesse oberhalb der Kuestenlinie ALS LAND - Elbe ab
//      Cuxhaven, Weser ab Bremerhaven, Nord-Ostsee-Kanal, die Foerden. Der
//      blosse Abstand zur naechsten Kante meldete fuer den Hamburger Hafen
//      "3,16 km", weil der Punkt INNEN liegt und der Rand wirklich so weit weg
//      ist. Ohne den Punkt-in-Polygon-Test waeren die 63 Hafenfaehren
//      stehengeblieben - also genau das, was die Regel loswerden soll.
//   2. Die OSM-Polygone sind KACHELWEISE ZERSCHNITTEN. Ein Strahlentest ueber
//      einen beschnittenen Segmentsatz erklaerte die offene Nordsee zu Land.
//      Geprueft wird deshalb je VOLLSTAENDIGEM Ring, dessen Huellbox den Punkt
//      enthaelt - fuer zerschnittene Polygone ist genau das richtig, denn ein
//      Punkt an Land liegt in genau einer Kachel.

const fs = require("node:fs");
const path = require("node:path");

// Gitterweite fuer beide Indizes. 0,02 Grad sind rund 2,2 x 1,3 km - fein
// genug, dass eine 2-km-Abfrage wenige Zellen anfasst, grob genug, dass der
// Index klein bleibt.
const GITTER = 0.02;

function meterJeGrad(lat) {
  return { ky: 111320, kx: 111320 * Math.cos(lat * Math.PI / 180) };
}

class Kueste {
  constructor({ datei, region, log } = {}) {
    this.log = log || (() => {});
    this.ringe = [];
    this.segmente = [];
    this.ringIndex = new Map();
    this.kantenIndex = new Map();
    this.box = null;
    // "da" heisst: Es gibt Daten UND sie decken die Region ab. Nur dann darf
    // gefiltert werden.
    this.da = false;
    this.grund = "nicht geladen";
    if (datei !== null) this.lade(datei, region);
  }

  lade(datei, region) {
    const pfad = datei || path.join(__dirname, "..", "karten", "kueste.json");
    let roh;
    try {
      roh = JSON.parse(fs.readFileSync(pfad, "utf8"));
    } catch (e) {
      this.grund = "karten/kueste.json fehlt oder ist unlesbar (" + e.message + ")";
      this.log("Kueste: " + this.grund + " - der Landfilter bleibt aus");
      return;
    }
    this.box = roh.box || null;
    // Deckt die Datei die Region nicht ab, wird NICHT gefiltert. Ein Punkt
    // ohne Kueste in Reichweite gaelte sonst als "weit draussen", und
    // ausgerechnet die Schleifen am Regionsrand blieben alle stehen - ein
    // stiller Fehler, der wie ein Ergebnis aussieht.
    if (region && this.box && !(
      this.box.latMin <= region.latMin && this.box.latMax >= region.latMax &&
      this.box.lonMin <= region.lonMin && this.box.lonMax >= region.lonMax)) {
      this.grund = "karten/kueste.json deckt die Region nicht ab (Datei " +
        [this.box.latMin, this.box.lonMin, this.box.latMax, this.box.lonMax].join(",") +
        ", Region " +
        [region.latMin, region.lonMin, region.latMax, region.lonMax].join(",") +
        ") - neu bauen mit werkzeug/kueste-bauen.js";
      this.log("Kueste: " + this.grund + "; der Landfilter bleibt aus");
      return;
    }

    for (const ring of roh.ringe || []) {
      if (!ring || ring.length < 3) continue;
      let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
      for (const [x, y] of ring) {
        if (x < x0) x0 = x; if (x > x1) x1 = x;
        if (y < y0) y0 = y; if (y > y1) y1 = y;
      }
      const ri = this.ringe.length;
      this.ringe.push({ x0, y0, x1, y1, pkt: ring });
      for (let gx = Math.floor(x0 / GITTER); gx <= Math.floor(x1 / GITTER); gx++) {
        for (let gy = Math.floor(y0 / GITTER); gy <= Math.floor(y1 / GITTER); gy++) {
          const k = gx + ":" + gy;
          let v = this.ringIndex.get(k);
          if (!v) this.ringIndex.set(k, v = []);
          v.push(ri);
        }
      }
      for (let i = 0; i < ring.length - 1; i++) {
        const [ax, ay] = ring[i], [bx, by] = ring[i + 1];
        const si = this.segmente.length;
        this.segmente.push([ax, ay, bx, by]);
        for (let gx = Math.floor(Math.min(ax, bx) / GITTER); gx <= Math.floor(Math.max(ax, bx) / GITTER); gx++) {
          for (let gy = Math.floor(Math.min(ay, by) / GITTER); gy <= Math.floor(Math.max(ay, by) / GITTER); gy++) {
            const k = gx + ":" + gy;
            let v = this.kantenIndex.get(k);
            if (!v) this.kantenIndex.set(k, v = []);
            v.push(si);
          }
        }
      }
    }
    this.da = this.ringe.length > 0;
    this.grund = this.da ? "" : "karten/kueste.json enthaelt keine Ringe";
    if (this.da) {
      this.log("Kueste: " + this.ringe.length + " Ringe, " + this.segmente.length +
        " Kanten aus " + path.basename(pfad));
    }
  }

  // Liegt der Punkt IN einem Landpolygon? Nur Ringe, deren Huellbox ihn
  // enthaelt - fuer kachelweise zerschnittene Polygone ist das der richtige
  // Test, und er ist zugleich der schnelle.
  anLand(lat, lon) {
    if (!this.da) return false;
    const kandidaten = this.ringIndex.get(
      Math.floor(lon / GITTER) + ":" + Math.floor(lat / GITTER));
    if (!kandidaten) return false;
    for (const ri of kandidaten) {
      const r = this.ringe[ri];
      if (lon < r.x0 || lon > r.x1 || lat < r.y0 || lat > r.y1) continue;
      const p = r.pkt;
      let drin = false;
      for (let i = 0, j = p.length - 1; i < p.length; j = i++) {
        const [xi, yi] = p[i], [xj, yj] = p[j];
        if ((yi > lat) !== (yj > lat) &&
            lon < (xj - xi) * (lat - yi) / (yj - yi) + xi) drin = !drin;
      }
      if (drin) return true;
    }
    return false;
  }

  // Abstand zur naechsten Landkante in Metern, gedeckelt bei "kappe". Der
  // Deckel ist kein Sparzwang, sondern Teil der Auskunft: Weiter als gefragt
  // interessiert niemanden, und ohne ihn muesste der Index bis zum Horizont
  // durchsucht werden.
  kantenAbstand(lat, lon, kappe) {
    if (!this.da) return kappe;
    const { kx, ky } = meterJeGrad(lat);
    const spanne = Math.ceil(kappe / 111320 / GITTER) + 1;
    const gx0 = Math.floor(lon / GITTER), gy0 = Math.floor(lat / GITTER);
    const x0 = lon * kx, y0 = lat * ky;
    let best = kappe;
    for (let gx = gx0 - spanne; gx <= gx0 + spanne; gx++) {
      for (let gy = gy0 - spanne; gy <= gy0 + spanne; gy++) {
        const v = this.kantenIndex.get(gx + ":" + gy);
        if (!v) continue;
        for (const si of v) {
          const s = this.segmente[si];
          const ax = s[0] * kx, ay = s[1] * ky, bx = s[2] * kx, by = s[3] * ky;
          const dx = bx - ax, dy = by - ay, L = dx * dx + dy * dy;
          const t = L === 0 ? 0 : Math.max(0, Math.min(1, ((x0 - ax) * dx + (y0 - ay) * dy) / L));
          const d = Math.hypot(x0 - (ax + t * dx), y0 - (ay + t * dy));
          if (d < best) best = d;
        }
      }
    }
    return best;
  }

  // Der Wert, um den es geht. An Land ist er 0 - nicht "weit vom Rand".
  landabstand(lat, lon, kappe) {
    const deckel = kappe || 20000;
    if (!this.da) return deckel;
    if (this.anLand(lat, lon)) return 0;
    return this.kantenAbstand(lat, lon, deckel);
  }

  bericht() {
    return {
      da: this.da,
      ringe: this.ringe.length,
      kanten: this.segmente.length,
      box: this.box,
      grund: this.grund || undefined
    };
  }
}

module.exports = { Kueste, GITTER };
