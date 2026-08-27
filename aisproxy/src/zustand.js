"use strict";

const ais = require("./ais");
const draht = require("./draht");

// Der heisse Zustand: eine Map je MMSI, nie auf der Platte.
//
// `rev` ist der ganze Trick. Ein globaler Zaehler, der bei jeder echten
// Aenderung um eins waechst und den Datensatz stempelt. Ein Client, der
// zuletzt rev = R gesehen hat, fragt "alles ueber R in meinem Ausschnitt".
// Kein Zeitvergleich, kein Mengenabgleich, keine Zustandshaltung je Client.
//
// ABWEICHUNG VOM ENTWURF, bewusst: Der Entwurf sah ein 0,25-Grad-Ortsgitter
// auch fuer den heissen Zustand vor. Eingebaut ist es nur fuer die KALTE
// Ablage (dort als indizierte Spalte, wo es wirklich traegt). Im heissen
// Zustand wird linear durchgesehen - bei 2915 Schiffen sind das ein paar
// Mikrosekunden, und der Test misst es nach. Ein zweiter Index, der bei
// jeder Positionsaenderung gepflegt werden muss, waere mehr Fehlerquelle als
// Gewinn gewesen.

class Zustand {
  constructor(opt) {
    opt = opt || {};
    this.schiffe = new Map();
    this.rev = 0;
    this.ttlMs = opt.ttlMs || 30 * 60 * 1000;
    // Zaehler fuer /v1/status
    this.zaehler = { ausStrom: 0, ausNetz: 0, verworfen: 0, entfernt: 0 };
  }

  // Meldung einarbeiten. Gibt den Datensatz zurueck, wenn sich etwas
  // geaendert hat, sonst null.
  //
  // `stand` ist der Zeitstempel der MELDUNG, nicht des Empfangs. Aeltere
  // Meldungen als der vorhandene Stand werden verworfen - sonst springt die
  // Position hinter einen frischeren Wert zurueck, sobald Strom und Netz
  // sich ueberholen. Genau das hat den Client schon einmal zappeln lassen.
  melde(mmsi, felder, stand, quelle) {
    mmsi = Number(mmsi);
    if (!Number.isFinite(mmsi) || mmsi <= 0) { this.zaehler.verworfen++; return null; }

    let s = this.schiffe.get(mmsi);
    if (!s) {
      s = { mmsi, lat: null, lon: null, sog: null, cog: null, hdg: null,
            status: null, flags: 0, seen: 0, quelle: quelle || "?", rev: 0,
            name: null, rufzeichen: null, imo: null, typ: null,
            laenge: null, breite: null, tiefgang: null, ziel: null, eta: null,
            stammRev: 0 };
      this.schiffe.set(mmsi, s);
    }

    const hatPosition = felder.lat != null && felder.lon != null;
    if (hatPosition && s.seen && stand && stand < s.seen) {
      // Veraltete Positionsmeldung. Stammdaten duerfen trotzdem durch.
      this.zaehler.verworfen++;
      felder = Object.assign({}, felder);
      delete felder.lat; delete felder.lon; delete felder.sog;
      delete felder.cog; delete felder.hdg; delete felder.status;
    }

    let geaendert = false;
    let stammGeaendert = false;
    const POSITION = ["lat", "lon", "sog", "cog", "hdg", "status", "flags"];
    const STAMM = ["name", "rufzeichen", "imo", "typ", "laenge", "breite",
                   "tiefgang", "ziel", "eta"];

    for (const k of POSITION) {
      if (felder[k] === undefined) continue;
      if (s[k] !== felder[k]) { s[k] = felder[k]; geaendert = true; }
    }
    for (const k of STAMM) {
      if (felder[k] === undefined || felder[k] === null) continue;
      if (s[k] !== felder[k]) { s[k] = felder[k]; stammGeaendert = true; }
    }

    if (stand && stand > s.seen) { s.seen = stand; geaendert = true; }
    if (quelle) s.quelle = quelle;
    if (quelle === "strom") this.zaehler.ausStrom++;
    else if (quelle === "netz") this.zaehler.ausNetz++;

    // Ein Zaehlerschritt je Meldung, nicht zwei. Zwei waeren harmlos, aber
    // der Zaehler ist die Waehrung der Deltas - er soll zaehlen, was passiert
    // ist, nicht wie viele Felder betroffen waren.
    if (geaendert || stammGeaendert) {
      s.rev = ++this.rev;
      if (stammGeaendert) s.stammRev = s.rev;
      return s;
    }
    return null;
  }

  // Stammdaten aus dem Register nachtragen - dieselbe Buchhaltung, damit ein
  // verbundener Client sie ohne Nachfrage bekommt.
  ergaenze(mmsi, felder) {
    const s = this.schiffe.get(Number(mmsi));
    if (!s) return null;
    let geaendert = false;
    for (const k of Object.keys(felder)) {
      if (felder[k] == null) continue;
      if (s[k] !== felder[k]) { s[k] = felder[k]; geaendert = true; }
    }
    if (!geaendert) return null;
    s.stammRev = s.rev = ++this.rev;
    return s;
  }

  hole(mmsi) { return this.schiffe.get(Number(mmsi)) || null; }
  get anzahl() { return this.schiffe.size; }

  // Alle Schiffe mit Position in der Box. `seitRev` filtert zusaetzlich auf
  // das, was sich seither geaendert hat.
  imAusschnitt(box, seitRev) {
    const out = [];
    for (const s of this.schiffe.values()) {
      if (s.lat == null || s.lon == null) continue;
      if (seitRev != null && s.rev <= seitRev) continue;
      if (box && !ais.inBox(box, s.lat, s.lon)) continue;
      out.push(s);
    }
    return out;
  }

  // Was hat den Ausschnitt seit `seitRev` VERLASSEN?
  //
  // `bekannt` ist hier nicht optional gemeint: Ohne diesen Filter meldete
  // jeder Takt alle geaenderten Schiffe AUSSERHALB der Box als "weg" - bei
  // 2915 Schiffen und einer kleinen Box sind das hunderte MMSIs je Takt und
  // damit genau der Verkehr, den das Binaerformat einspart. Gemeldet wird
  // nur, was der Client wirklich hat.
  verlassen(box, seitRev, bekannt) {
    const out = [];
    for (const s of this.schiffe.values()) {
      if (s.rev <= seitRev) continue;
      if (bekannt && !bekannt.has(s.mmsi)) continue;
      if (s.lat == null || s.lon == null || !ais.inBox(box, s.lat, s.lon)) out.push(s.mmsi);
    }
    return out;
  }

  // Fuer das Drahtformat aufbereiten.
  static alsDraht(s) {
    return { mmsi: s.mmsi, lat: s.lat, lon: s.lon, sog: s.sog, cog: s.cog,
             hdg: s.hdg, status: s.status, flags: s.flags };
  }

  // Stammdaten als JSON - gehen nur einmal je MMSI hinueber.
  static alsStamm(s) {
    return { mmsi: s.mmsi, name: s.name, rufzeichen: s.rufzeichen, imo: s.imo,
             typ: s.typ, laenge: s.laenge, breite: s.breite,
             tiefgang: s.tiefgang, ziel: s.ziel, eta: s.eta,
             seen: s.seen, quelle: s.quelle };
  }

  // Schiffe ohne Meldung seit TTL fliegen raus. Gibt die entfernten MMSIs
  // zurueck, damit der Server sie den Clients als "weg" melden kann.
  aufraeumen(jetzt) {
    jetzt = jetzt || Date.now();
    const grenze = jetzt - this.ttlMs;
    const weg = [];
    for (const [mmsi, s] of this.schiffe) {
      if (s.seen && s.seen >= grenze) continue;
      this.schiffe.delete(mmsi);
      weg.push(mmsi);
    }
    this.zaehler.entfernt += weg.length;
    return weg;
  }

  // Alles, was noch keine Registerabfrage hatte oder deren Frist abgelaufen
  // ist - Arbeitsvorrat fuer das Vorwaermen.
  ohneRegister(gepruefteMmsis) {
    const out = [];
    for (const mmsi of this.schiffe.keys()) {
      if (!gepruefteMmsis.has(mmsi)) out.push(mmsi);
    }
    return out;
  }
}

// Flaggen aus der Art der Station.
function flaggen(art, klasseB) {
  let f = 0;
  if (art === "aton") f |= draht.FLAG_SEEZEICHEN;
  if (art === "base") f |= draht.FLAG_LANDSTATION;
  if (klasseB) f |= draht.FLAG_KLASSE_B;
  return f;
}

module.exports = { Zustand, flaggen };
