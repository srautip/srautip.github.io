"use strict";

const fs = require("node:fs");
const path = require("node:path");
const ais = require("./ais");
const draht = require("./draht");

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

// Was aus dem AIS-Strom in die Stammtabelle wandert. Register- und
// Fotospalten stehen bewusst NICHT hier: Die fuellt register.js, und ein
// Schreiber, der beide Seiten anfasst, ueberschreibt frueher oder spaeter die
// eine mit dem Nichtwissen der anderen.
const STAMM_SPALTEN = ["name", "rufzeichen", "imo", "typ", "laenge", "breite",
  "dimA", "dimB", "dimC", "dimD", "tiefgang", "ziel", "eta",
  "klasse", "geraet", "aisVersion", "dte",
  "hersteller", "modell", "seriennr",
  "atonTyp", "atonVirtuell", "atonAusserPosition"];

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
    this.stammPuffer = new Map();
    this.stammGeschrieben = 0;
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

    // Der Fotostand steht GETRENNT vom Registerstand.
    //
    // Vorher gab es nur "gefunden": Ein Schiff mit Wikidata-Stammdaten galt
    // damit als erledigt und war 30 Tage gesperrt - auch wenn der Bilddownload
    // in derselben Sekunde an einem HTTP 429 gescheitert war. Gemessen an drei
    // echten Schiffen (HARLINGERLAND, Spiekeroog II, NORDSEE): wd_entity
    // gesetzt, foto_datei leer, und bei einer Wiederholung heute liefert
    // dieselbe Abfrage sehr wohl ein Bild. Ein verlorener Download darf nicht
    // wie "hat kein Bild" aussehen.
    // Die Bezugspunkte der Antenne. SQLite kennt kein "ADD COLUMN IF NOT
    // EXISTS", deshalb der Umweg ueber spalteErgaenzen.
    for (const k of ["dimA", "dimB", "dimC", "dimD"]) this.spalteErgaenzen("schiff", k, "REAL");
    // Der Rest der Statik aus Msg 5 und 24.
    for (const [k, t] of [["klasse", "TEXT"], ["geraet", "INTEGER"],
                          ["aisVersion", "INTEGER"], ["dte", "INTEGER"],
                          ["hersteller", "TEXT"], ["modell", "TEXT"],
                          ["seriennr", "TEXT"], ["atonTyp", "INTEGER"],
                          ["atonVirtuell", "INTEGER"], ["atonAusserPosition", "INTEGER"]]) {
      this.spalteErgaenzen("schiff", k, t);
    }
    // Der Registertyp kommt spaeter als die Registersaetze selbst. Wer schon
    // als "gefunden" vermerkt ist, wuerde sonst 30 Tage lang nicht mehr
    // gefragt und bliebe so lange ohne Typ. Deshalb GENAU EINMAL - beim
    // Anlegen der Spalte - die Frist dieser Saetze zuruecksetzen. Der
    // naechste Registerlauf holt sie in wenigen Buendelabfragen nach.
    if (this.spalteErgaenzen("schiff", "wd_typ", "TEXT")) {
      const n = this.db.prepare(
        "UPDATE schiff SET geprueft = 0 WHERE wd_entity IS NOT NULL").run().changes;
      if (n) this.log(n + " Registersatz/-saetze zum Nachfragen vorgemerkt (neuer Typ)");
    }
    this.spalteErgaenzen("schiff", "gesehen", "INTEGER");
    this.spalteErgaenzen("schiff", "foto_geprueft", "INTEGER DEFAULT 0");
    this.spalteErgaenzen("schiff", "foto_quelle", "TEXT");
    this.spalteErgaenzen("schiff", "foto_seite", "TEXT");

    // Wohin ein Schiff frueher unterwegs war. Das Zielfeld im AIS traegt immer
    // nur den aktuellen Stand, und die Stammtabelle ueberschreibt ihn - die
    // vorherige Reise waere damit weg, obwohl der Proxy sie mitgehoert hat.
    //
    // Geschrieben wird NUR beim Wechsel (stammSetze), nicht bei jeder
    // Wiederholung: Msg 5 kommt je Schiff alle sechs Minuten, ein Upsert je
    // Nachricht waere bei 3 000 Schiffen Arbeit fuer nichts.
    //
    // `zuerst` ist die erste Sichtung dieses Ziels, `zuletzt` der letzte
    // Wechsel DARAUF - nicht die letzte Wiederholung. Fuer die Reihenfolge
    // der vergangenen Ziele ist genau das die richtige Angabe.
    //
    // Sortiert wird trotzdem nach `folge`, einem schlichten Zaehler. Der
    // Zeitstempel hat Sekundenaufloesung, und im Test lagen vier Wechsel in
    // derselben Sekunde - die Reihenfolge war damit dem Zufall ueberlassen.
    // Ein Zaehler ist auch gegen eine zurueckgestellte Uhr immun.
    this.db.exec(`
      CREATE TABLE IF NOT EXISTS ziel_verlauf (
        mmsi INTEGER NOT NULL, ziel TEXT NOT NULL,
        zuerst INTEGER NOT NULL, zuletzt INTEGER NOT NULL,
        folge INTEGER NOT NULL DEFAULT 0,
        PRIMARY KEY (mmsi, ziel)
      )`);

    // Die Ortstabelle: UN/LOCODE -> Name, aus der offiziellen Liste der UNECE.
    //
    // Warum im Proxy und nicht im Client: Die Liste hat 116 213 Zeilen, davon
    // 17 596 Seehaefen (Function beginnt mit 1). Als Zeichenkette im Client
    // waeren das 289 KB - fast so viel wie der ganze ausgelieferte Client.
    // Hier kostet sie nichts und deckt die ganze Welt ab; der Client fragt
    // nur die Codes ab, die er gerade sieht, und merkt sie sich lokal.
    this.db.exec(`
      CREATE TABLE IF NOT EXISTS ort (
        code TEXT PRIMARY KEY, name TEXT, land TEXT, funktion TEXT, geholt INTEGER
      )`);
    // Der amtliche Name bleibt daneben stehen. `name` traegt die deutsche
    // Form, wo es eine gibt ("Kopenhagen"), `amtlich` die aus der UNECE-Liste
    // ("København"). Beides zu behalten kostet nichts und erlaubt, den Weg
    // jederzeit zurueckzugehen - ein ueberschriebener Quellwert waere weg.
    this.spalteErgaenzen("ort", "amtlich", "TEXT");

    // Der Bildabzug aus Wikidata. Statt je Schiff zu fragen, wird die
    // vollstaendige Zuordnung einmal geholt und hier gehalten - gemessen
    // 17 144 Zeilen IMO->Bild in EINER Abfrage (0,89 MB, 9,8 s) und 8 638
    // ueber die MMSI. Danach ist "hat dieses Schiff ein Bild?" ein
    // Datenbankzugriff, auch fuer ein Schiff, das gerade erst auftaucht.
    this.db.exec(`
      CREATE TABLE IF NOT EXISTS bild_index (
        art TEXT, kennung TEXT, url TEXT, geholt INTEGER,
        PRIMARY KEY (art, kennung)
      )`);
  }

  // SQLite kennt kein "ADD COLUMN IF NOT EXISTS". Ein bestehender Server hat
  // die Tabelle laengst, CREATE TABLE IF NOT EXISTS aendert daran nichts -
  // ohne diesen Weg braeuchte jede neue Spalte eine Handwanderung auf dem
  // Server.
  spalteErgaenzen(tabelle, name, typ) {
    const da = this.db.prepare(`PRAGMA table_info(${tabelle})`).all()
      .some(s => s.name === name);
    if (da) return false;
    this.db.exec(`ALTER TABLE ${tabelle} ADD COLUMN ${name} ${typ}`);
    return true;
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
    // Seezeichen bewegen sich nicht. Sie melden trotzdem alle paar Minuten -
    // und weil ihre Geschwindigkeit unbekannt ist, wuerde die Verdichtung sie
    // ausdruecklich BEHALTEN ("ein Schiff ohne Fahrtangabe, das seine Position
    // aendert, faehrt sehr wohl"). Das ergaebe eine Spur aus lauter identischen
    // Punkten. Ihr Stammsatz wird geschrieben, ihre Historie nicht.
    if (s.flags & draht.FLAG_SEEZEICHEN) return;
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

  // Stammdaten aus dem Strom vormerken. Frueher standen sie NUR im
  // Arbeitsspeicher: Nach jedem Neustart musste der Proxy die Abmessungen neu
  // lernen, und weil ShipStaticData je Schiff nur etwa alle sechs Minuten
  // kommt, dauerte das gemessen 13 Minuten fuer 668 von 2 800 Schiffen. Ein
  // Client mit leerem Zwischenspeicher sah in der Zeit lauter Schiffe "ohne
  // bekannte Masse".
  //
  // Geschrieben wird nur, was auch bekannt ist: Ein null wuerde in
  // stammSetze() eine vorhandene Registerangabe ueberschreiben.
  merkeStamm(s) {
    const felder = {};
    for (const k of STAMM_SPALTEN) if (s[k] != null) felder[k] = s[k];
    if (!Object.keys(felder).length) return;
    felder.gesehen = Math.floor((s.seen || Date.now()) / 1000);
    const mmsi = Number(s.mmsi);
    // Der Puffer haelt je Schiff nur die JUENGSTE Meldung - ein Zielwechsel
    // innerhalb eines Schreibtakts fiele damit unter den Tisch, und
    // stammSetze() saehe ihn nie. Im Betrieb liegen Wechsel Stunden
    // auseinander, aber "faellt selten weg" ist kein Zustand fuer einen
    // Verlauf. Aufgefallen ist es in der Probe, wo drei Ziele in 600 ms kamen
    // und am Ende genau eines davon in der Datenbank stand.
    const vorher = this.stammPuffer.get(mmsi);
    if (vorher && vorher.ziel && felder.ziel && vorher.ziel !== felder.ziel) {
      this.zielMerke(mmsi, String(vorher.ziel).trim(), s.seen);
    }
    this.stammPuffer.set(mmsi, felder);
  }

  schreibeStamm() {
    if (!this.stammPuffer.size) return 0;
    const stapel = this.stammPuffer;
    this.stammPuffer = new Map();
    let n = 0;
    this.db.exec("BEGIN");
    try {
      for (const [mmsi, felder] of stapel) { this.stammSetze(mmsi, felder); n++; }
      this.db.exec("COMMIT");
    } catch (e) {
      this.db.exec("ROLLBACK");
      this.log("Stammdaten schreiben fehlgeschlagen: " + e.message);
      return 0;
    }
    this.stammGeschrieben += n;
    return n;
  }

  schreibe() {
    // Vor der Abkuerzung unten: Stammdaten fallen auch dann an, wenn gerade
    // keine Position im Puffer liegt.
    this.schreibeStamm();
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

  // --- Zielverlauf ------------------------------------------------------

  zielMerke(mmsi, ziel, jetzt) {
    const t = Math.floor((jetzt || Date.now()) / 1000);
    try {
      if (this.zielFolge == null) {
        this.zielFolge = this.db.prepare(
          "SELECT IFNULL(MAX(folge), 0) AS n FROM ziel_verlauf").get().n;
      }
      const folge = ++this.zielFolge;
      this.db.prepare(
        "INSERT INTO ziel_verlauf (mmsi, ziel, zuerst, zuletzt, folge) VALUES (?, ?, ?, ?, ?) " +
        "ON CONFLICT(mmsi, ziel) DO UPDATE SET zuletzt = excluded.zuletzt, folge = excluded.folge")
        .run(Number(mmsi), ziel, t, t, folge);
      // Gedeckelt je Schiff. Ein Transponder, der jede Woche etwas anderes
      // sendet, fuellte die Tabelle sonst unbegrenzt - und mehr als eine
      // Handvoll frueherer Ziele liest ohnehin niemand.
      this.db.prepare(
        "DELETE FROM ziel_verlauf WHERE mmsi = ? AND ziel NOT IN (" +
        "SELECT ziel FROM ziel_verlauf WHERE mmsi = ? ORDER BY folge DESC LIMIT ?)")
        .run(Number(mmsi), Number(mmsi), this.konfig.ZIEL_VERLAUF_MAX);
    } catch (e) {
      // Ein misslungener Verlaufseintrag darf die Stammdaten nicht mitreissen.
      this.log("Zielverlauf schreiben fehlgeschlagen: " + e.message);
    }
  }

  zielVerlauf(mmsi) {
    return this.db.prepare(
      "SELECT ziel, zuerst, zuletzt FROM ziel_verlauf WHERE mmsi = ? " +
      "ORDER BY folge DESC").all(Number(mmsi));
  }

  stammHole(mmsi) {
    return this.db.prepare("SELECT * FROM schiff WHERE mmsi = ?").get(Number(mmsi)) || null;
  }

  // Die Lotsenboote der Region. AIS-Typ 50 ODER der Name traegt PILOT/LOTSE.
  //
  // Die Namensregel ist keine Bequemlichkeit: Gemessen am Regionsbestand
  // (2801 Schiffe) fuehren 49 Boote den Typ 50, aber 6 weitere Lotsenboote
  // melden einen falschen (HAMBURG PILOT 3 und LOTSE 4 als 99, HAMBURG
  // PILOT 4 und DANPILOT ALDEBARAN als 90, MEES (PILOTS) als 53). Ohne die
  // Namensregel fehlt die ZWEITSTAERKSTE Station der Region ganz - Elbe bei
  // Hamburg, 95 Runden in 24 h, ausschliesslich von solchen Booten.
  //
  // Ein Fehltreffer bliebe sichtbar: Der Tooltip auf der Karte nennt die
  // Bootsnamen des Gebiets.
  lotsenMmsis() {
    try {
      return this.db.prepare(
        "SELECT mmsi FROM schiff WHERE typ = 50" +
        " OR upper(name) LIKE '%PILOT%' OR upper(name) LIKE '%LOTSE%'"
      ).all().map(r => Number(r.mmsi));
    } catch (e) {
      this.log("Lotsenliste fehlgeschlagen: " + e.message);
      return [];
    }
  }

  stammSetze(mmsi, felder) {
    const vorhanden = this.stammHole(mmsi);
    // Vor dem Ueberschreiben: Ein gewechseltes Ziel gehoert in den Verlauf.
    if (typeof felder.ziel === "string" && felder.ziel.trim() &&
        (!vorhanden || vorhanden.ziel !== felder.ziel)) {
      this.zielMerke(mmsi, felder.ziel.trim());
    }
    const erlaubt = ["name", "rufzeichen", "imo", "typ", "laenge", "breite", "tiefgang",
      "dimA", "dimB", "dimC", "dimD", "gesehen",
      "klasse", "geraet", "aisVersion", "dte",
      "hersteller", "modell", "seriennr",
      "atonTyp", "atonVirtuell", "atonAusserPosition",
      "ziel", "eta", "wd_entity", "wd_typ", "brz", "baujahr", "eigner", "betreiber", "werft",
      "flagge", "heimathafen", "foto_datei", "foto_credit", "foto_seite",
      "foto_quelle", "foto_geprueft", "quelle", "gefunden", "geprueft"];
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

  // --- Orte (UN/LOCODE) -------------------------------------------------

  ortSchreibe(zeilen) {
    // Der amtliche Name wird immer aufgefrischt, der angezeigte NICHT
    // ueberschrieben, wenn schon ein deutscher dasteht: Sonst machte jeder
    // Abzug aus "Kopenhagen" wieder "København".
    const stmt = this.db.prepare(
      "INSERT INTO ort (code, name, amtlich, land, funktion, geholt) VALUES (?, ?, ?, ?, ?, ?) " +
      "ON CONFLICT(code) DO UPDATE SET amtlich = excluded.amtlich, " +
      "name = CASE WHEN ort.name IS NULL OR ort.name = ort.amtlich " +
      "            THEN excluded.name ELSE ort.name END, " +
      "land = excluded.land, funktion = excluded.funktion, geholt = excluded.geholt");
    const jetzt = Date.now();
    let n = 0;
    this.db.exec("BEGIN");
    try {
      for (const z of zeilen) {
        stmt.run(z.code, z.name, z.name, z.land, z.funktion || null, jetzt); n++;
      }
      this.db.exec("COMMIT");
    } catch (e) {
      this.db.exec("ROLLBACK");
      this.log("Ortsliste schreiben fehlgeschlagen: " + e.message);
      return 0;
    }
    return n;
  }

  ortHole(codes) {
    const out = {};
    if (!codes || !codes.length) return out;
    // Einzelabfragen statt IN (...): Bei hoechstens ein paar Dutzend Codes je
    // Anfrage ist der Unterschied nicht messbar, und eine zusammengebaute
    // IN-Liste waere die Stelle, an der eine Eingabe in die Abfrage geriete.
    const stmt = this.db.prepare("SELECT name FROM ort WHERE code = ?");
    for (const c of codes) {
      const r = stmt.get(String(c).toUpperCase());
      if (r) out[String(c).toUpperCase()] = r.name;
    }
    return out;
  }

  // Deutsche Namen nachtragen. Nur wo es einen gibt - sonst bleibt der
  // amtliche stehen, und das ist die richtige Auskunft, keine Luecke.
  ortDeutsch(paare) {
    const stmt = this.db.prepare("UPDATE ort SET name = ? WHERE code = ? AND name IS NOT ?");
    let n = 0;
    this.db.exec("BEGIN");
    try {
      for (const [code, name] of paare) {
        if (stmt.run(name, String(code).toUpperCase(), name).changes) n++;
      }
      this.db.exec("COMMIT");
    } catch (e) {
      this.db.exec("ROLLBACK");
      this.log("Deutsche Ortsnamen schreiben fehlgeschlagen: " + e.message);
      return 0;
    }
    return n;
  }

  ortStand() {
    const r = this.db.prepare(
      "SELECT COUNT(*) AS n, MAX(geholt) AS geholt, " +
      "SUM(CASE WHEN name IS NOT amtlich THEN 1 ELSE 0 END) AS deutsch FROM ort").get();
    return { eintraege: r.n, geholt: r.geholt || 0, deutsch: r.deutsch || 0 };
  }

  bildIndexSchreibe(art, paare) {
    const stmt = this.db.prepare(
      "INSERT INTO bild_index (art, kennung, url, geholt) VALUES (?, ?, ?, ?) " +
      "ON CONFLICT(art, kennung) DO UPDATE SET url = excluded.url, geholt = excluded.geholt");
    const jetzt = Date.now();
    this.db.exec("BEGIN");
    try {
      for (const [kennung, url] of paare) stmt.run(art, String(kennung), url, jetzt);
      this.db.exec("COMMIT");
    } catch (e) {
      this.db.exec("ROLLBACK");
      this.log("Bildindex schreiben fehlgeschlagen: " + e.message);
      return 0;
    }
    return paare.length;
  }

  bildIndexHole(art, kennung) {
    if (kennung == null) return null;
    const r = this.db.prepare("SELECT url FROM bild_index WHERE art = ? AND kennung = ?")
      .get(art, String(kennung));
    return r ? r.url : null;
  }

  bildIndexStand() {
    const r = this.db.prepare(
      "SELECT art, COUNT(*) AS n, MAX(geholt) AS geholt FROM bild_index GROUP BY art").all();
    const out = {};
    for (const z of r) out[z.art] = { eintraege: z.n, geholt: z.geholt };
    return out;
  }

  // Wer braucht einen FOTO-Versuch? Getrennt vom Registerstand: Ein Schiff mit
  // Stammdaten, aber ohne Bild, gehoert weiter gefragt - nur eben seltener als
  // eines, das noch nie dran war.
  fotoFaellig(mmsis, jetzt) {
    jetzt = jetzt || Date.now();
    const out = [];
    for (const mmsi of mmsis) {
      const s = this.stammHole(mmsi);
      if (s && s.foto_datei) continue;              // hat eins, fertig
      if (!s || !s.foto_geprueft) { out.push(mmsi); continue; }
      // "teil" heisst: gesucht wurde ohne IMO, der Versuch war unvollstaendig.
      // Frueher stand hier eine kurze Frist ("die IMO kann jede Minute per
      // ShipStaticData eintreffen"). Nachgemessen am laufenden Server
      // (28. Aug. 2026) kostete das mehr, als es brachte: Jede Stunde lief
      // dieselbe Suche ueber die MMSI erneut mit - 243 Commons-Abrufe fuer
      // 1 Bild, 281 Flickr-Abrufe fuer 6 - und das Kontingent von 300
      // Versuchen je Lauf ging dafuer drauf. Der Rueckstand blieb bei 1 587
      // offenen Schiffen stehen.
      //
      // Jetzt entscheidet der ANLASS, nicht die Uhr: Wieder faellig wird ein
      // solches Schiff, sobald die IMO wirklich da ist (dann lohnen
      // Kategorie- und Volltextweg) oder der Bildabzug es ueber die MMSI
      // kennt (dann ist es ein Download ohne jede Suche). Ohne beides bleibt
      // es liegen - aber nicht fuer immer: Nach der langen Frist wird noch
      // einmal gefragt, denn Wikidata waechst.
      const anlass = s.foto_quelle === "teil" &&
        !!(s.imo || this.bildIndexHole("mmsi", String(mmsi)));
      const frist = anlass ? this.konfig.FOTO_TEIL_MS : this.konfig.FOTO_FEHL_MS;
      if (jetzt - s.foto_geprueft > frist) out.push(mmsi);
    }
    return out;
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
    // Die Bilderzahl gehoert in den Bericht, weil sie das Einzige ist, was
    // die Fotoarbeit DAUERHAFT festhaelt: Die Zaehler im Register leben im
    // Prozess und stehen nach jedem Neustart auf null. Wer nach einem Update
    // "0 Fotos" liest, soll daneben sehen, wie viele Bilder wirklich da sind.
    const bilder = this.db.prepare(
      "SELECT COUNT(*) AS n, SUM(CASE WHEN foto_quelle = 'eigen' THEN 1 ELSE 0 END) AS eigen " +
      "FROM schiff WHERE foto_datei IS NOT NULL").get();
    return {
      tage: tage.length, tabellen: tage, punkte: zeilen,
      geschrieben: this.geschrieben, puffer: this.puffer.length,
      stammGeschrieben: this.stammGeschrieben, stammPuffer: this.stammPuffer.size,
      stammEintraege: stamm.n, stammTreffer: stamm.treffer || 0,
      fotos: bilder.n, fotosEigen: bilder.eigen || 0
    };
  }
}

module.exports = { Speicher, tagName };
