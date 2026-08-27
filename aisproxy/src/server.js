"use strict";

const http = require("node:http");
const fs = require("node:fs");
const path = require("node:path");
const { WebSocketServer } = require("ws");
const draht = require("./draht");
const { Zustand } = require("./zustand");

// Die Schnittstelle zum Client.
//
//   GET  /v1/snapshot?bbox=          Vollbild + aktuelle rev
//   WS   /v1/live                    nur Aenderungen, dazu enter/leave
//   GET  /v1/replay?bbox=&von=&bis=  Stuetzpunktfolgen fuer die Animation
//   GET  /v1/track?mmsi=&von=&bis=   eine Spur in voller Aufloesung
//   GET  /v1/ship/{mmsi}             Stammdaten + Registertreffer + Foto
//   GET  /v1/status                  Ratenwaechter und Zaehler
//
// Positionen gehen binaer (20 Byte je Schiff), Stammdaten als JSON. Der
// Grund steht in draht.js: Faktor 15 gegenueber dem GeoJSON des Upstreams,
// und das entscheidet ueber die Vertretbarkeit eines Sekundentakts.

function bboxAus(text, vorgabe) {
  if (!text) return vorgabe;
  const t = String(text).split(",").map(Number);
  if (t.length !== 4 || t.some(x => !Number.isFinite(x))) return null;
  // Reihenfolge wie beim Upstream: latMin,lonMin,latMax,lonMax - Breite
  // zuerst. Eine andere Reihenfolge hier waere eine Falle fuer jeden, der
  // zwischen beiden Schnittstellen hin- und herspringt.
  return { latMin: Math.min(t[0], t[2]), lonMin: Math.min(t[1], t[3]),
           latMax: Math.max(t[0], t[2]), lonMax: Math.max(t[1], t[3]) };
}

function jsonAus(res, code, daten) {
  const koerper = Buffer.from(JSON.stringify(daten));
  res.writeHead(code, {
    "Content-Type": "application/json; charset=utf-8",
    "Content-Length": koerper.length,
    // Der Client liegt auf github.io, der Proxy woanders - ohne das kommt
    // keine einzige Antwort im Browser an.
    "Access-Control-Allow-Origin": "*"
  });
  res.end(koerper);
}

class Server {
  constructor(opt) {
    this.konfig = opt.konfig;
    this.zustand = opt.zustand;
    this.speicher = opt.speicher;
    this.log = opt.log || console.log;
    this.status = opt.status || (() => ({}));
    this.clients = new Set();
    this.http = http.createServer((req, res) => this.anfrage(req, res));
    this.wss = new WebSocketServer({ noServer: true });
    this.http.on("upgrade", (req, sock, kopf) => this.aufstieg(req, sock, kopf));
    this.wss.on("connection", (ws, req) => this.verbindung(ws, req));
  }

  hoere() {
    return new Promise(r => this.http.listen(this.konfig.PORT, () => {
      this.log("Server hoert auf Port " + this.konfig.PORT);
      r();
    }));
  }

  stopp() {
    for (const c of this.clients) { clearInterval(c.takt); try { c.ws.close(); } catch (e) {} }
    this.wss.close();
    this.http.close();
  }

  // Zugang: ein statisches Token, wenn eines gesetzt ist. Fuer den kleinen
  // Kreis reicht das; die eigentliche Absicherung macht Caddy davor.
  erlaubt(url, req) {
    if (!this.konfig.ZUGANG) return true;
    const kopf = req.headers.authorization || "";
    return url.searchParams.get("token") === this.konfig.ZUGANG ||
           kopf === "Bearer " + this.konfig.ZUGANG;
  }

  async anfrage(req, res) {
    const url = new URL(req.url, "http://x");
    if (req.method === "OPTIONS") {
      res.writeHead(204, {
        "Access-Control-Allow-Origin": "*",
        "Access-Control-Allow-Headers": "Authorization,Content-Type"
      });
      return res.end();
    }
    if (!this.erlaubt(url, req)) return jsonAus(res, 401, { fehler: "Zugang verweigert" });

    try {
      if (url.pathname === "/v1/status") return jsonAus(res, 200, this.status());
      if (url.pathname === "/v1/snapshot") return this.snapshot(url, res);
      if (url.pathname === "/v1/replay") return this.replay(url, res);
      if (url.pathname === "/v1/track") return this.track(url, res);
      if (url.pathname.startsWith("/v1/ship/")) return this.ship(url, res);
      if (url.pathname.startsWith("/v1/foto/")) return this.foto(url, res);
      if (url.pathname === "/" || url.pathname === "/v1") {
        return jsonAus(res, 200, {
          dienst: "aisproxy", region: this.konfig.REGION,
          endpunkte: ["/v1/snapshot", "/v1/live (WebSocket)", "/v1/replay",
                      "/v1/track", "/v1/ship/{mmsi}", "/v1/foto/{datei}", "/v1/status"]
        });
      }
      return jsonAus(res, 404, { fehler: "unbekannter Pfad" });
    } catch (e) {
      this.log("Anfragefehler " + url.pathname + ": " + e.message);
      return jsonAus(res, 500, { fehler: e.message });
    }
  }

  snapshot(url, res) {
    const box = bboxAus(url.searchParams.get("bbox"), this.konfig.REGION);
    if (!box) return jsonAus(res, 400, { fehler: "bbox unlesbar" });
    const schiffe = this.zustand.imAusschnitt(box);
    // Binaer wuerde hier auch gehen, aber das Erstbild kommt genau einmal je
    // Ausschnitt - dafuer ist Lesbarkeit mehr wert als 8 KB.
    return jsonAus(res, 200, {
      rev: this.zustand.rev,
      zeit: Date.now(),
      anzahl: schiffe.length,
      schiffe: schiffe.map(s => Object.assign(Zustand.alsStamm(s), Zustand.alsDraht(s, Date.now())))
    });
  }

  replay(url, res) {
    const box = bboxAus(url.searchParams.get("bbox"), this.konfig.REGION);
    if (!box) return jsonAus(res, 400, { fehler: "bbox unlesbar" });
    const jetzt = Math.floor(Date.now() / 1000);
    const von = Number(url.searchParams.get("von")) || (jetzt - 6 * 3600);
    const bis = Number(url.searchParams.get("bis")) || jetzt;
    const schritt = Number(url.searchParams.get("schritt")) || 60;
    if (bis <= von) return jsonAus(res, 400, { fehler: "bis muss nach von liegen" });
    const spuren = this.speicher.spuren(box, von, bis, schritt);
    return jsonAus(res, 200, {
      von, bis, schritt, anzahl: spuren.length,
      // [t, lat*1e6, lon*1e6, sog*10, cog*10] je Stuetzpunkt. Bewusst keine
      // Abtastung zu festen Zeiten - der Client interpoliert dazwischen.
      form: ["t", "lat_1e6", "lon_1e6", "sog_01", "cog_01"],
      spuren
    });
  }

  track(url, res) {
    const mmsi = Number(url.searchParams.get("mmsi"));
    if (!mmsi) return jsonAus(res, 400, { fehler: "mmsi fehlt" });
    const jetzt = Math.floor(Date.now() / 1000);
    const von = Number(url.searchParams.get("von")) || (jetzt - 24 * 3600);
    const bis = Number(url.searchParams.get("bis")) || jetzt;
    return jsonAus(res, 200, {
      mmsi, von, bis,
      form: ["t", "lat_1e6", "lon_1e6", "sog_01", "cog_01"],
      punkte: this.speicher.spur(mmsi, von, bis)
    });
  }

  ship(url, res) {
    const mmsi = Number(url.pathname.split("/").pop());
    if (!mmsi) return jsonAus(res, 400, { fehler: "mmsi unlesbar" });
    const heiss = this.zustand.hole(mmsi);
    const stamm = this.speicher.stammHole(mmsi);
    if (!heiss && !stamm) return jsonAus(res, 404, { fehler: "unbekannte MMSI" });
    const antwort = Object.assign({ mmsi }, stamm || {});
    if (heiss) Object.assign(antwort, Zustand.alsStamm(heiss), Zustand.alsDraht(heiss, Date.now()));
    if (stamm && stamm.foto_datei) antwort.foto = "/v1/foto/" + stamm.foto_datei;
    // Ehrlichkeit ueber den Registerstand: "noch nicht gefragt" und
    // "gefragt, nichts gefunden" sind zwei verschiedene Dinge.
    antwort.register = !stamm || !stamm.geprueft ? "offen"
      : stamm.gefunden ? "gefunden" : "nichts gefunden";
    return jsonAus(res, 200, antwort);
  }

  foto(url, res) {
    const datei = path.basename(decodeURIComponent(url.pathname.split("/").pop()));
    const voll = path.join(this.konfig.FOTO_VERZEICHNIS, datei);
    if (!fs.existsSync(voll)) return jsonAus(res, 404, { fehler: "kein Foto" });
    const buf = fs.readFileSync(voll);
    res.writeHead(200, {
      "Content-Type": /\.png$/.test(datei) ? "image/png" : "image/jpeg",
      "Content-Length": buf.length,
      "Cache-Control": "public, max-age=604800",
      "Access-Control-Allow-Origin": "*"
    });
    res.end(buf);
  }

  aufstieg(req, sock, kopf) {
    const url = new URL(req.url, "http://x");
    if (url.pathname !== "/v1/live" || !this.erlaubt(url, req)) {
      sock.write("HTTP/1.1 401 Unauthorized\r\n\r\n");
      return sock.destroy();
    }
    this.wss.handleUpgrade(req, sock, kopf, ws => this.wss.emit("connection", ws, req));
  }

  verbindung(ws, req) {
    const url = new URL(req.url, "http://x");
    const c = {
      ws,
      box: bboxAus(url.searchParams.get("bbox"), this.konfig.REGION) || this.konfig.REGION,
      seitRev: Number(url.searchParams.get("seit_rev")) || 0,
      taktMs: this.takt(url.searchParams.get("takt")),
      bekannt: new Set(),      // MMSIs, deren Stammdaten der Client schon hat
      gesendet: 0, bytes: 0,
      takt: null
    };
    this.clients.add(c);
    this.log("Client verbunden (Takt " + c.taktMs + " ms, " + this.clients.size + " gesamt)");

    ws.on("message", (roh) => {
      // Der Client darf Ausschnitt und Takt jederzeit nachziehen - beim
      // Schwenken der Karte ist genau das der Normalfall.
      try {
        const o = JSON.parse(roh);
        if (o.bbox) { const b = bboxAus(o.bbox, null); if (b) { c.box = b; c.seitRev = 0; c.bekannt.clear(); } }
        if (o.takt) { c.taktMs = this.takt(o.takt); clearInterval(c.takt); c.takt = setInterval(() => this.tick(c), c.taktMs); }
        if (o.seit_rev != null) c.seitRev = Number(o.seit_rev) || 0;
      } catch (e) {}
    });
    ws.on("close", () => { clearInterval(c.takt); this.clients.delete(c); });
    ws.on("error", () => { clearInterval(c.takt); this.clients.delete(c); });

    this.tick(c);
    c.takt = setInterval(() => this.tick(c), c.taktMs);
  }

  takt(roh) {
    const n = Number(roh);
    if (!Number.isFinite(n)) return this.konfig.TAKT_MS;
    return Math.max(this.konfig.TAKT_MIN_MS, Math.min(this.konfig.TAKT_MAX_MS, n));
  }

  tick(c) {
    if (c.ws.readyState !== c.ws.OPEN) return;
    const stand = this.zustand.rev;
    const geaendert = this.zustand.imAusschnitt(c.box, c.seitRev);
    const weg = c.seitRev ? this.zustand.verlassen(c.box, c.seitRev, c.bekannt) : [];

    if (!geaendert.length && !weg.length) { c.seitRev = stand; return; }

    // Stammdaten zuerst und nur einmal je MMSI - sonst reisen Namen bei
    // jedem Takt mit und fressen das ein, was das Binaerformat spart.
    const neueStamm = geaendert.filter(s => !c.bekannt.has(s.mmsi));
    if (neueStamm.length) {
      const nachricht = JSON.stringify({
        typ: "stamm", schiffe: neueStamm.map(Zustand.alsStamm)
      });
      c.ws.send(nachricht);
      c.bytes += Buffer.byteLength(nachricht);
      for (const s of neueStamm) c.bekannt.add(s.mmsi);
    }
    if (weg.length) {
      const nachricht = JSON.stringify({ typ: "weg", mmsi: weg });
      c.ws.send(nachricht);
      c.bytes += Buffer.byteLength(nachricht);
      for (const m of weg) c.bekannt.delete(m);
    }
    if (geaendert.length) {
      const jetzt = Date.now();
      const rahmen = draht.packe(stand, geaendert.map(s => Zustand.alsDraht(s, jetzt)));
      c.ws.send(rahmen);
      c.bytes += rahmen.length;
      c.gesendet += geaendert.length;
    }
    c.seitRev = stand;
  }

  // Schiffe, die aus dem heissen Zustand gefallen sind, muessen auch beim
  // Client verschwinden - sonst bleiben Geistermarker stehen.
  meldeEntfernt(mmsis) {
    if (!mmsis.length) return;
    const nachricht = JSON.stringify({ typ: "weg", mmsi: mmsis });
    for (const c of this.clients) {
      if (c.ws.readyState !== c.ws.OPEN) continue;
      c.ws.send(nachricht);
      for (const m of mmsis) c.bekannt.delete(m);
    }
  }

  bericht() {
    let gesendet = 0, bytes = 0;
    for (const c of this.clients) { gesendet += c.gesendet; bytes += c.bytes; }
    return { clients: this.clients.size, gesendeteSaetze: gesendet, gesendeteBytes: bytes };
  }
}

module.exports = { Server, bboxAus };
