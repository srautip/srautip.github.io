"use strict";

const https = require("node:https");
const http = require("node:http");
const zlib = require("node:zlib");
const ais = require("./ais");
const { flaggen } = require("./zustand");

// Abrufen mit EIGENER Zaehlung der Bytes auf der Leitung.
//
// Warum nicht fetch(): Dessen arrayBuffer() liefert die ENTPACKTEN Daten.
// Wer die zaehlt, misst 938 KB und haelt den Abruf fuer teuer, waehrend
// tatsaechlich rund 110 KB uebertragen wurden - derselbe Fehler, der beim
// Stream schon einmal fast die Architektur gedreht haette. Hier wird am
// Socket gezaehlt und selbst entpackt, dann stimmt die Zahl.
function holeGezaehlt(url, zeitLimitMs) {
  return new Promise((erfuellt, abgelehnt) => {
    const mod = url.startsWith("https:") ? https : http;
    const req = mod.get(url, { headers: { "Accept-Encoding": "gzip, deflate" } }, (res) => {
      if (res.statusCode !== 200) {
        res.resume();
        return abgelehnt(new Error("HTTP " + res.statusCode));
      }
      let drahtBytes = 0;
      const stuecke = [];
      const kodierung = (res.headers["content-encoding"] || "").toLowerCase();
      const entpacker = kodierung === "gzip" ? zlib.createGunzip()
        : kodierung === "deflate" ? zlib.createInflate() : null;
      res.on("data", (b) => { drahtBytes += b.length; });
      const quelle = entpacker ? res.pipe(entpacker) : res;
      quelle.on("data", (b) => stuecke.push(b));
      quelle.on("end", () => erfuellt({
        koerper: Buffer.concat(stuecke), drahtBytes, kodierung: kodierung || "keine"
      }));
      quelle.on("error", abgelehnt);
    });
    req.setTimeout(zeitLimitMs, () => { req.destroy(new Error("Zeitueberschreitung")); });
    req.on("error", abgelehnt);
  });
}

// Das Sicherheitsnetz: GET /v1/vessels?bbox= alle 60 s.
//
// Gemessen: 109 KB gzip je Abruf ueber der Region, Boden des Erzeugungs-
// verzugs rund 20 s. Bei 60 s Takt sind das 0,16 GB/Tag - ein Zehntel des
// Stroms, und es faengt drei Dinge, die der Strom nicht kann:
//
//   1. Schiffe, die gerade gar nicht melden (drei Viertel liegen still).
//   2. Die Luecke nach einem Verbindungsabriss.
//   3. Den Kaltstart - sonst sammelt sich das Bild ueber Minuten zusammen.
//
// WICHTIG: niemals ohne bbox abrufen. Ohne den Parameter liefert der Dienst
// kommentarlos den kompletten Weltbestand (rund 55 000 Features, 17 MB).

class Netz {
  constructor(opt) {
    this.konfig = opt.konfig;
    this.zustand = opt.zustand;
    this.log = opt.log || console.log;
    this.beiMeldung = opt.beiMeldung || function () {};
    this.takt = null;
    this.laeuft = false;
    this.letzter = { zeit: 0, schiffe: 0, neu: 0, bytes: 0, dauerMs: 0, fehler: null };
    this.abrufe = 0;
    this.bytes = 0;
  }

  start() {
    // Sofort einmal, damit der Proxy nach Sekunden auskunftsfaehig ist und
    // nicht erst nach einer halben Minute Stromsammeln.
    this.abrufen();
    this.takt = setInterval(() => this.abrufen(), this.konfig.NETZ_MS);
  }

  stopp() { clearInterval(this.takt); }

  async abrufen() {
    if (this.laeuft) return;              // Ein langsamer Abruf darf sich nicht stapeln
    this.laeuft = true;
    const t0 = Date.now();
    try {
      const r = this.konfig.REGION;
      const url = this.konfig.REST_URL + "?bbox=" +
        [r.latMin, r.lonMin, r.latMax, r.lonMax].join(",");
      const antwort = await holeGezaehlt(url, 45000);
      this.bytes += antwort.drahtBytes;
      const geo = JSON.parse(antwort.koerper.toString("utf8"));
      const feats = (geo && geo.features) || [];
      let neu = 0;
      for (const f of feats) {
        const s = uebernimm(this.zustand, f);
        if (s) { neu++; this.beiMeldung(s); }
      }
      this.abrufe++;
      this.letzter = {
        zeit: Date.now(), schiffe: feats.length, neu,
        // Beide Zahlen, damit niemand die eine fuer die andere haelt.
        bytes: antwort.drahtBytes,
        bytesEntpackt: antwort.koerper.length,
        kodierung: antwort.kodierung,
        dauerMs: Date.now() - t0, fehler: null
      };
      if (antwort.kodierung === "keine" && this.abrufe === 1) {
        this.log("WARNUNG: Der Snapshot kommt unkomprimiert (" +
          (antwort.koerper.length / 1024).toFixed(0) + " KB statt rund einem Siebtel).");
      }
    } catch (e) {
      this.letzter = Object.assign({}, this.letzter, {
        zeit: Date.now(), fehler: e.message, dauerMs: Date.now() - t0
      });
      this.log("Netz-Abruf fehlgeschlagen: " + e.message);
    } finally {
      this.laeuft = false;
    }
  }

  bericht() {
    return {
      abrufe: this.abrufe,
      bytes: this.bytes,
      letzterAbruf: this.letzter.zeit ? new Date(this.letzter.zeit).toISOString() : null,
      letzteSchiffe: this.letzter.schiffe,
      letzteNeu: this.letzter.neu,
      letzteBytes: this.letzter.bytes,
      letzteBytesEntpackt: this.letzter.bytesEntpackt,
      kodierung: this.letzter.kodierung,
      hochgerechnetGBproTag: this.letzter.bytes
        ? Number((this.letzter.bytes * (86400000 / this.konfig.NETZ_MS) / 1e9).toFixed(3)) : 0,
      letzteDauerMs: this.letzter.dauerMs,
      letzterFehler: this.letzter.fehler
    };
  }
}

// Ein GeoJSON-Feature in denselben Datensatz uebersetzen, den der Strom
// fuellt. Der Snapshot ist KEIN Register, sondern eine Positionsmeldung ueber
// einen anderen Weg - also dieselbe Datenklasse, mit demselben Vorrang nach
// Zeitstempel.
function uebernimm(zustand, f) {
  const p = f && f.properties;
  const c = f && f.geometry && f.geometry.coordinates;
  if (!p || !c || p.mmsi == null) return null;
  const lat = c[1], lon = c[0];
  if (!ais.positionGueltig(lat, lon)) return null;

  const felder = { lat, lon };
  if (p.sog != null) felder.sog = ais.sogNormal(p.sog);
  if (p.cog != null) felder.cog = ais.cogNormal(p.cog);
  if (p.heading != null) felder.hdg = ais.headingNormal(p.heading);
  if (p.nav_status != null) felder.status = Number(p.nav_status);
  const name = ais.textSauber(p.name); if (name) felder.name = name;
  if (p.type != null) felder.typ = Number(p.type);
  felder.flags = flaggen(p.kind, false);

  // Der Snapshot bringt seinen eigenen Zeitstempel mit. Ihn zu benutzen statt
  // "jetzt" haelt Sortierung und Altersanzeige ehrlich.
  const stand = p.seen ? Date.parse(p.seen) : Date.now();
  return zustand.melde(p.mmsi, felder, Number.isFinite(stand) ? stand : Date.now(), "netz");
}

module.exports = { Netz, uebernimm };
