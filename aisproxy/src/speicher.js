"use strict";

const fs = require("node:fs");
const path = require("node:path");
const ais = require("./ais");

// node:sqlite kam in Node 22.5 hinzu und war eine Weile hinter
// --experimental-sqlite versteckt. Welche 22er-Minorversion auf einem
// fremden Server liegt, weiss man vorher nicht - und ein blankes
// "Cannot find module 'node:sqlite'" beim ersten Start waere eine
// unnoetig lange Fehlersuche.
let DatabaseSync;
try {
  ({ DatabaseSync } = require("node:sqlite"));
} catch (e) {
  console.error(
    "\naisproxy braucht node:sqlite (in Node ab 22.5 enthalten).\n" +
    "Gefunden: Node " + process.versions.node + "\n" +
    "Abhilfe: Node auf 22.22 oder neuer heben - oder ersatzweise mit\n" +
    "  node --experimental-sqlite src/index.js\n" +
    "starten, falls diese Version das Flag noch verlangt.\n");
  throw e;
}

// Die kalte Ablage.
//
// node:sqlite statt better-sqlite3: In Node 22 eingebaut, also kein nativer
// Build im Container (better-sqlite3 braucht python3, make und g++ im Image).
// Der Preis ist der Experimentell-Vermerk der API - ein Wechsel waere ein
// kleiner Eingriff, die Aufrufe sind fast deckungsgleich.
//
// TAGESWEISE TABELLEN sind der Kern: Aufraeumen heisst DROP TABLE. Ein
// DELETE ueber Millionen Zeilen laesst die Datei aufgeblaeht zurueck und
// braucht VACUUM, das die Datenbank minutenlang sperrt.

const TAG_MS = 24 * 3600 * 1000;

function tagName(ms) {
  const d = new Date(ms);
  return "pos_" + d.getUTCFullYear() +
    String(d.getUTCMonth() + 1).padStart(2, "0") +
    String(d.getUTCDate()).padStart(2, "0");
}

class Speicher {
  constructor(opt) {
    this.konfig = opt.konfig;
    this.log = opt.log || console.log;
    const datei = opt.datei || this.konfig.DB_DATEI;
    if (datei !== ":memory:") fs.mkdirSync(path.dirname(datei), { recursive: true });
    this.db = new DatabaseSync(datei);
    // WAL: Leser blockieren den Schreiber nicht. Bei einem Prozess, der
    // dauernd schreibt und gelegentlich gelesen wird, ist das der Unterschied
    // zwischen fluessig und haakelig.
    this.db.exec("PRAGMA journal_mode = WAL");
    this.db.exec("PRAGMA synchronous = NORMAL");
    this.tabellen = new Set();
    this.puffer = [];
    this.geschrieben = 0;
    this.stammTabelle();
    this.taktSchreiben = null;
  }

  stammTabelle() {
    // Stammdaten sind langlebig - eine Tabelle, nicht tagesweise.
    this.db.exec(`
      CREATE TABLE IF NOT EXISTS schiff (
        mmsi INTEGER PRIMARY KEY,
        name TEXT, rufzeichen TEXT, imo INTEGER, typ INTEGER,
        laenge REAL, breite REAL, tiefgang REAL,
        ziel TEXT, eta INTEGER,
        wd_entity TEXT, brz INTEGER, baujahr TEXT,
        eigner TEXT, betreiber TEXT, werft TEXT, flagge TEXT, heimathafen TEXT,
        foto_datei TEXT, foto_credit TEXT,
        quelle TEXT,
        gefunden INTEGER DEFAULT 0,
        geprueft INTEGER DEFAULT 0
      )`);
    this.db.exec("CREATE INDEX IF NOT EXISTS schiff_geprueft ON schiff (geprueft)");
    this.db.exec("CREATE INDEX IF NOT EXISTS schiff_imo ON schiff (imo)");
  }

  tabelleFuer(ms) {
    const name = tagName(ms);
    if (this.tabellen.has(name)) return name;
    this.db.exec(`
      CREATE TABLE IF NOT EXISTS ${name} (
        t INTEGER, mmsi INTEGER, zelle INTEGER,
        lat INTEGER, lon INTEGER, sog INTEGER, cog INTEGER
      )`);
    // (zelle, t) traegt die bbox-Abfrage der Animation, (mmsi, t) die
    // Einzelspur. Beide werden gebraucht, beide sind billig.
    this.db.exec(`CREATE INDEX IF NOT EXISTS ${name}_zt ON ${name} (zelle, t)`);
    this.db.exec(`CREATE INDEX IF NOT EXISTS ${name}_mt ON ${name} (mmsi, t)`);
    this.tabellen.add(name);
    return name;
  }

  // Eine Position vormerken. Geschrieben wird gebuendelt - einzelne Inserts
  // waeren bei 37 Meldungen je Sekunde reine Verschwendung.
  merke(s) {
    if (s.lat == null || s.lon == null) return;
    this.puffer.push({
      t: Math.floor((s.seen || Date.now()) / 1000),
      mmsi: s.mmsi,
      zelle: ais.zelle(s.lat, s.lon),
      lat: Math.round(s.lat * 1e6),
      lon: Math.round(s.lon * 1e6),
      sog: s.sog == null ? null : Math.round(s.sog * 10),
      cog: s.cog == null ? null : Math.round(s.cog * 10)
    });
  }

  schreibe() {
    if (!this.puffer.length) return 0;
    const stapel = this.puffer;
    this.puffer = [];
    // Nach Tagestabelle gruppieren - um Mitternacht faellt ein Stapel auf
    // zwei Tabellen, und ohne diese Gruppierung landete die Haelfte falsch.
    const nachTag = new Map();
    for (const z of stapel) {
      const name = this.tabelleFuer(z.t * 1000);
      if (!nachTag.has(name)) nachTag.set(name, []);
      nachTag.get(name).push(z);
    }
    let n = 0;
    this.db.exec("BEGIN");
    try {
      for (const [name, zeilen] of nachTag) {
        const stmt = this.db.prepare(
          `INSERT INTO ${name} (t, mmsi, zelle, lat, lon, sog, cog) VALUES (?, ?, ?, ?, ?, ?, ?)`);
        for (const z of zeilen) { stmt.run(z.t, z.mmsi, z.zelle, z.lat, z.lon, z.sog, z.cog); n++; }
      }
      this.db.exec("COMMIT");
    } catch (e) {
      this.db.exec("ROLLBACK");
      this.log("Schreiben fehlgeschlagen: " + e.message);
      return 0;
    }
    this.geschrieben += n;
    return n;
  }

  startSchreiben() {
    this.taktSchreiben = setInterval(() => this.schreibe(), this.konfig.SCHREIB_MS);
  }
  stopp() {
    clearInterval(this.taktSchreiben);
    this.schreibe();
    try { this.db.close(); } catch (e) {}
  }

  vorhandeneTage() {
    const rows = this.db.prepare(
      "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'pos_%' ORDER BY name").all();
    return rows.map(r => r.name);
  }

  // --- Pflege: verdichten und verwerfen ---------------------------------

  // Alles aelter als ROH_STUNDEN wird auf ein Punkt je VERDICHTUNG_S
  // zusammengefasst, und liegende Schiffe fallen dabei ganz heraus.
  // Gemessen sind nur 27 % der Schiffe in Fahrt - der Rest kostet Platz fuer
  // eine Linie, die sich nicht bewegt.
  verdichte(jetzt) {
    jetzt = jetzt || Date.now();
    const grenze = Math.floor((jetzt - this.konfig.ROH_STUNDEN * 3600 * 1000) / 1000);
    const raster = this.konfig.VERDICHTUNG_S;
    const fahrt = Math.round(this.konfig.FAHRT_KN * 10);
    let entfernt = 0;
    for (const name of this.vorhandeneTage()) {
      const rest = this.db.prepare(`SELECT COUNT(*) AS n FROM ${name} WHERE t < ?`).get(grenze);
      if (!rest || !rest.n) continue;
      const vorher = rest.n;
      this.db.exec("BEGIN");
      try {
        // Je Schiff und Rasterfenster den ersten Punkt behalten. rowid ist
        // der stabile Anker - ohne ihn traefe DELETE auch die Behalter.
        // sog IS NULL bleibt DRIN: "unbekannt" ist nicht "liegt still". Ein
        // Schiff ohne Geschwindigkeitsangabe, das seine Position aendert,
        // faehrt sehr wohl - dessen Spur wegzuwerfen waere ein Datenverlust,
        // der sich hinterher nicht mehr bemerken laesst. Gemessen betrifft
        // das 117 von 12921 Schiffen (0,9 %), kostet also fast nichts.
        this.db.exec(`
          DELETE FROM ${name} WHERE t < ${grenze} AND rowid NOT IN (
            SELECT MIN(rowid) FROM ${name}
            WHERE t < ${grenze} AND (sog IS NULL OR sog >= ${fahrt})
            GROUP BY mmsi, t / ${raster}
          )`);
        this.db.exec("COMMIT");
      } catch (e) {
        this.db.exec("ROLLBACK");
        this.log("Verdichten von " + name + " fehlgeschlagen: " + e.message);
        continue;
      }
      const nachher = this.db.prepare(`SELECT COUNT(*) AS n FROM ${name} WHERE t < ?`).get(grenze);
      entfernt += vorher - (nachher ? nachher.n : 0);
    }
    return entfernt;
  }

  // Tagestabellen jenseits der Aufbewahrung verwerfen. DROP statt DELETE.
  verwirf(jetzt) {
    jetzt = jetzt || Date.now();
    const aeltester = tagName(jetzt - (this.konfig.HISTORIE_TAGE - 1) * TAG_MS);
    const weg = [];
    for (const name of this.vorhandeneTage()) {
      if (name >= aeltester) continue;
      this.db.exec(`DROP TABLE ${name}`);
      this.tabellen.delete(name);
      weg.push(name);
    }
    return weg;
  }

  pflege(jetzt) {
    const v = this.verdichte(jetzt);
    const w = this.verwirf(jetzt);
    if (v || w.length) {
      this.log("Pflege: " + v + " Punkte verdichtet" +
        (w.length ? ", verworfen: " + w.join(", ") : ""));
    }
    return { verdichtet: v, verworfen: w };
  }

  // --- Abfragen ---------------------------------------------------------

  // Stuetzpunktfolgen fuer die Animation. Bewusst KEINE Abtastung zu festen
  // Zeitpunkten: Der Client interpoliert zwischen den Punkten. Das ist
  // sparsamer und passt zu Meldungen, die unregelmaessig eintreffen.
  spuren(box, vonS, bisS, schrittS) {
    const zellen = ais.zellen(box);
    const platz = zellen.map(() => "?").join(",");
    const nachSchiff = new Map();
    for (const name of this.vorhandeneTage()) {
      let rows;
      try {
        rows = this.db.prepare(
          `SELECT t, mmsi, lat, lon, sog, cog FROM ${name}
           WHERE zelle IN (${platz}) AND t >= ? AND t <= ? ORDER BY mmsi, t`
        ).all(...zellen, vonS, bisS);
      } catch (e) { continue; }
      for (const r of rows) {
        let sp = nachSchiff.get(r.mmsi);
        if (!sp) { sp = []; nachSchiff.set(r.mmsi, sp); }
        // Ausduennen auf das gewuenschte Raster erst hier: Die Datenbank
        // liefert, was sie hat, und das Raster ist eine Anzeigefrage.
        const letzt = sp.length ? sp[sp.length - 1] : null;
        if (letzt && schrittS > 0 && r.t - letzt[0] < schrittS) continue;
        sp.push([r.t, r.lat, r.lon, r.sog, r.cog]);
      }
    }
    const out = [];
    for (const [mmsi, punkte] of nachSchiff) {
      if (punkte.length < 2) continue;    // Ein einzelner Punkt ist keine Spur
      out.push({ mmsi, punkte });
    }
    return out;
  }

  spur(mmsi, vonS, bisS) {
    const punkte = [];
    for (const name of this.vorhandeneTage()) {
      let rows;
      try {
        rows = this.db.prepare(
          `SELECT t, lat, lon, sog, cog FROM ${name} WHERE mmsi = ? AND t >= ? AND t <= ? ORDER BY t`
        ).all(Number(mmsi), vonS, bisS);
      } catch (e) { continue; }
      for (const r of rows) punkte.push([r.t, r.lat, r.lon, r.sog, r.cog]);
    }
    punkte.sort((a, b) => a[0] - b[0]);
    return punkte;
  }

  // Beim Start: der letzte bekannte Stand je Schiff. Damit ist der Proxy
  // nach zwei Sekunden auskunftsfaehig statt nach einer halben Minute.
  letzterStand(grenzeS) {
    const tage = this.vorhandeneTage().slice(-2);   // gestern und heute reichen
    const je = new Map();
    for (const name of tage) {
      let rows;
      try {
        // Die blanken Spalten neben MAX(t) stammen in SQLite garantiert aus
        // genau der Zeile, die das Maximum geliefert hat - das ist dort
        // zugesichert, in anderen Datenbanken waere es undefiniert.
        rows = this.db.prepare(
          `SELECT mmsi, MAX(t) AS t, lat, lon, sog, cog FROM ${name}
           WHERE t >= ? GROUP BY mmsi`).all(grenzeS);
      } catch (e) { continue; }
      for (const r of rows) {
        const alt = je.get(r.mmsi);
        if (!alt || r.t > alt.t) je.set(r.mmsi, r);
      }
    }
    return [...je.values()];
  }

  // --- Stammdaten -------------------------------------------------------

  stammHole(mmsi) {
    return this.db.prepare("SELECT * FROM schiff WHERE mmsi = ?").get(Number(mmsi)) || null;
  }

  stammSetze(mmsi, felder) {
    const vorhanden = this.stammHole(mmsi);
    const erlaubt = ["name", "rufzeichen", "imo", "typ", "laenge", "breite", "tiefgang",
      "ziel", "eta", "wd_entity", "brz", "baujahr", "eigner", "betreiber", "werft",
      "flagge", "heimathafen", "foto_datei", "foto_credit", "quelle", "gefunden", "geprueft"];
    const daten = {};
    for (const k of erlaubt) if (felder[k] !== undefined) daten[k] = felder[k];
    if (!vorhanden) {
      const spalten = ["mmsi", ...Object.keys(daten)];
      const platz = spalten.map(() => "?").join(",");
      this.db.prepare(`INSERT INTO schiff (${spalten.join(",")}) VALUES (${platz})`)
        .run(Number(mmsi), ...Object.keys(daten).map(k => daten[k]));
    } else {
      const keys = Object.keys(daten);
      if (!keys.length) return;
      this.db.prepare(`UPDATE schiff SET ${keys.map(k => k + " = ?").join(", ")} WHERE mmsi = ?`)
        .run(...keys.map(k => daten[k]), Number(mmsi));
    }
  }

  // Wer ist faellig? Ein Treffer haelt lange, ein Fehltreffer kurz - die IMO
  // kann jede Minute per ShipStaticData eintreffen und die Lage aendern.
  stammFaellig(mmsis, jetzt) {
    jetzt = jetzt || Date.now();
    const out = [];
    for (const mmsi of mmsis) {
      const s = this.stammHole(mmsi);
      if (!s || !s.geprueft) { out.push(mmsi); continue; }
      const frist = s.gefunden ? this.konfig.REGISTER_TREFFER_MS : this.konfig.REGISTER_FEHL_MS;
      if (jetzt - s.geprueft > frist) out.push(mmsi);
    }
    return out;
  }

  bericht() {
    const tage = this.vorhandeneTage();
    let zeilen = 0;
    for (const name of tage) {
      try { zeilen += this.db.prepare(`SELECT COUNT(*) AS n FROM ${name}`).get().n; } catch (e) {}
    }
    const stamm = this.db.prepare(
      "SELECT COUNT(*) AS n, SUM(gefunden) AS treffer FROM schiff").get();
    return {
      tage: tage.length, tabellen: tage, punkte: zeilen,
      geschrieben: this.geschrieben, puffer: this.puffer.length,
      stammEintraege: stamm.n, stammTreffer: stamm.treffer || 0
    };
  }
}

module.exports = { Speicher, tagName };
