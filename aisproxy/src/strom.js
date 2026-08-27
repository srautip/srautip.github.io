"use strict";

const WebSocket = require("ws");
const ais = require("./ais");
const { flaggen } = require("./zustand");

// Der Livestream - die Hauptquelle.
//
// Gemessen am 27. Aug. 2026 ueber der Region 53-56 N / 6-13 E:
//   37,6 msg/s   (Limit des personal-Tokens: 50)
//   1717 verschiedene Schiffe je Minute von 2915
//   Verzoegerung min 1,3 s, Median 30,7 s, 90 % 63,5 s
//   1,80 GB/Tag roh - mit permessage-deflate 0,62 GB/Tag
//
// permessage-deflate ist deshalb keine Feinheit, sondern der Grund, warum
// der Stream ueberhaupt die Hauptquelle sein darf. Node's eingebauter
// WebSocket kann es nicht aushandeln, das ws-Paket schon.

const RUECKZUG_MS = [2000, 5000, 10000, 20000, 30000];

class Strom {
  constructor(opt) {
    this.konfig = opt.konfig;
    this.zustand = opt.zustand;
    this.log = opt.log || console.log;
    this.beiMeldung = opt.beiMeldung || function () {};
    this.ws = null;
    this.token = null;
    this.versuch = 0;
    this.wecker = null;
    this.aus = false;

    // Ratenwaechter: gleitendes Fenster ueber 60 s.
    this.fenster = [];
    this.rateSpitze = 0;
    this.gewarnt = false;
    this.gesamt = 0;
    this.bytes = 0;
    this.seitWann = 0;
    this.verbunden = false;
  }

  async start() {
    this.token = this.konfig.TOKEN || await this.holeToken();
    this.verbinde();
  }

  // Wie der "Token holen"-Knopf im Client: Ed25519-Schluesselpaar erzeugen,
  // oeffentlichen Teil einreichen, Token bekommen. Kostenlos, ohne Anmeldung.
  async holeToken() {
    const paar = await crypto.subtle.generateKey({ name: "Ed25519" }, true, ["sign", "verify"]);
    const roh = await crypto.subtle.exportKey("raw", paar.publicKey);
    const b64 = Buffer.from(roh).toString("base64")
      .replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    const res = await fetch(this.konfig.SCHLUESSEL_URL, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ pubkey: b64, bind_ip: false })
    });
    if (!res.ok) throw new Error("Token holen fehlgeschlagen: HTTP " + res.status);
    const data = await res.json();
    const token = data.token || data.key || data.apikey;
    if (!token) throw new Error("Antwort enthielt keinen Token");
    // Die Ansprueche stehen im Token selbst - sie zu protokollieren spart die
    // halbe Debug-Runde, wenn spaeter "verbunden, aber keine Daten" auftritt.
    try {
      const anspruch = JSON.parse(Buffer.from(token.split(".")[1], "base64").toString());
      this.log("Token geholt: conns=" + anspruch.conns + " rate=" + anspruch.rate +
        " area=" + anspruch.area + " mmsis=" + anspruch.mmsis);
      const flaeche = this.konfig.flaeche();
      if (anspruch.area && flaeche > anspruch.area) {
        // Der Server verwirft eine zu grosse Box KOMMENTARLOS - keine
        // Fehlermeldung, einfach nie Daten. Also hier laut werden.
        this.log("ACHTUNG: Region " + flaeche.toFixed(1) + " sq ueberschreitet das " +
          "Limit von " + anspruch.area + " sq. Der Server verwirft die " +
          "Subscription stillschweigend - es kommen keine Daten.");
      }
    } catch (e) { /* kein JWT-artiger Token: nicht schlimm */ }
    return token;
  }

  verbinde() {
    if (this.aus) return;
    const r = this.konfig.REGION;
    // perMessageDeflate: die Messung, die den Entwurf gedreht hat.
    this.ws = new WebSocket(this.konfig.STROM_URL, { perMessageDeflate: true });

    this.ws.on("upgrade", (res) => {
      const ext = res.headers["sec-websocket-extensions"] || "(keine)";
      if (!/permessage-deflate/.test(ext)) {
        this.log("WARNUNG: Server handelt permessage-deflate NICHT aus (" + ext +
          "). Der Stream kostet dann rund das Dreifache.");
      }
    });

    this.ws.on("open", () => {
      this.verbunden = true;
      this.versuch = 0;
      this.seitWann = Date.now();
      // Rohes Netzvolumen mitzaehlen - sonst misst man die entpackte Menge
      // und haelt den Stream faelschlich fuer teuer.
      const sock = this.ws._socket;
      if (sock) sock.on("data", (b) => { this.bytes += b.length; });
      this.ws.send(JSON.stringify({
        APIKey: this.token,
        BoundingBoxes: [[[r.latMin, r.lonMin], [r.latMax, r.lonMax]]],
        FilterMessageTypes: this.konfig.NACHRICHTENTYPEN
      }));
      this.log("Stream verbunden, Region " + this.konfig.flaeche().toFixed(1) + " sq");
    });

    this.ws.on("message", (roh) => this.nachricht(roh));

    this.ws.on("close", (code) => {
      this.verbunden = false;
      if (this.aus) return;
      const wartezeit = RUECKZUG_MS[Math.min(this.versuch, RUECKZUG_MS.length - 1)];
      this.versuch++;
      this.log("Stream getrennt (Code " + code + "), neuer Versuch in " + (wartezeit / 1000) + " s");
      this.wecker = setTimeout(() => this.verbinde(), wartezeit);
    });

    this.ws.on("error", (e) => this.log("Stream-Fehler: " + e.message));
  }

  stopp() {
    this.aus = true;
    clearTimeout(this.wecker);
    if (this.ws) try { this.ws.close(); } catch (e) {}
  }

  // Der Ratenwaechter. 37,6 von 50 msg/s sind nur 25 % Luft, und ueber der
  // Grenze drosselt der Server ohne ein Wort - man merkt es nur daran, dass
  // Schiffe fehlen. Deshalb wird gewarnt, bevor es passiert.
  zaehleRate(jetzt) {
    this.fenster.push(jetzt);
    const alt = jetzt - 60000;
    while (this.fenster.length && this.fenster[0] < alt) this.fenster.shift();
    const rate = this.fenster.length / 60;
    if (rate > this.rateSpitze) this.rateSpitze = rate;
    if (rate >= this.konfig.RATE_WARNUNG && !this.gewarnt) {
      this.gewarnt = true;
      this.log("ACHTUNG: " + rate.toFixed(1) + " msg/s - das Limit liegt bei " +
        this.konfig.RATE_LIMIT + ". Bei Erreichen drosselt der Server " +
        "kommentarlos. Region auf zwei Verbindungen aufteilen.");
    } else if (rate < this.konfig.RATE_WARNUNG * 0.8) {
      this.gewarnt = false;
    }
  }

  nachricht(roh) {
    const jetzt = Date.now();
    this.gesamt++;
    this.zaehleRate(jetzt);
    let o;
    try { o = JSON.parse(roh); } catch (e) { return; }
    const satz = uebersetze(o);
    if (!satz) return;
    const s = this.zustand.melde(satz.mmsi, satz.felder, satz.stand, "strom");
    if (s) this.beiMeldung(s, satz.hatPosition);
  }

  bericht() {
    const dauer = this.seitWann ? (Date.now() - this.seitWann) / 1000 : 0;
    return {
      verbunden: this.verbunden,
      nachrichten: this.gesamt,
      rate: this.fenster.length / 60,
      rateSpitze: Number(this.rateSpitze.toFixed(1)),
      rateLimit: this.konfig.RATE_LIMIT,
      bytes: this.bytes,
      bytesProSekunde: dauer > 0 ? Math.round(this.bytes / dauer) : 0,
      hochgerechnetGBproTag: dauer > 0 ? Number((this.bytes / dauer * 86400 / 1e9).toFixed(2)) : 0
    };
  }
}

// Eine aisstream-Nachricht in unser Modell uebersetzen.
// Die Feldnamen spiegeln aisstream/index.html ab Z. 3820.
function uebersetze(o) {
  const typ = o.MessageType;
  const meta = o.MetaData || {};
  const msg = o.Message ? o.Message[typ] : null;
  const mmsi = meta.MMSI != null ? Number(meta.MMSI) : (msg && msg.UserID);
  if (!mmsi) return null;

  const stand = meta.time_utc ? Date.parse(String(meta.time_utc)
    .replace(" +0000 UTC", "Z").replace(" ", "T")) : Date.now();
  const felder = {};
  let hatPosition = false;

  // MetaData.ShipName kommt bei JEDER Nachricht mit - auch bei Typen ohne
  // eigene Behandlung. Genau daran ist der Client schon einmal haengen
  // geblieben und behielt den alten Namen.
  const metaName = ais.textSauber(meta.ShipName);
  if (metaName) felder.name = metaName;

  const istPosition = typ === "PositionReport" ||
    typ === "StandardClassBPositionReport" || typ === "ExtendedClassBPositionReport";

  if (istPosition && msg) {
    const lat = msg.Latitude != null ? msg.Latitude : meta.latitude;
    const lon = msg.Longitude != null ? msg.Longitude : meta.longitude;
    if (ais.positionGueltig(lat, lon)) {
      felder.lat = lat; felder.lon = lon; hatPosition = true;
    }
    if (msg.Sog != null) felder.sog = ais.sogNormal(msg.Sog);
    if (msg.Cog != null) felder.cog = ais.cogNormal(msg.Cog);
    if (msg.TrueHeading != null) felder.hdg = ais.headingNormal(msg.TrueHeading);
    if (typ === "PositionReport" && msg.NavigationalStatus != null) {
      felder.status = Number(msg.NavigationalStatus);
    }
    felder.flags = flaggen(null, typ !== "PositionReport");
    if (typ === "ExtendedClassBPositionReport") {
      // Msg 19 bringt Statik mit - genau die Felder, auf die man sonst
      // minutenlang wartet.
      const n = ais.textSauber(msg.Name); if (n) felder.name = n;
      if (msg.ShipType != null) felder.typ = Number(msg.ShipType);
      const m = ais.masseFelder(msg.Dimension);
      if (m) Object.assign(felder, m);
    }
  } else if (typ === "ShipStaticData" && msg) {
    // Alles, was Msg 5 traegt. Frueher standen hier nur die Felder, die die
    // Tabelle im Client fuellen - Geraetetyp, AIS-Fassung und das
    // DTE-Bit fielen unter den Tisch, und ueber den Proxy wusste der Client
    // nicht einmal, ob ein Schiff Klasse A oder B faehrt.
    felder.klasse = "A";
    const n = ais.textSauber(msg.Name); if (n) felder.name = n;
    const c = ais.textSauber(msg.CallSign); if (c) felder.rufzeichen = c;
    if (msg.ImoNumber) felder.imo = Number(msg.ImoNumber);
    const z = ais.textSauber(msg.Destination); if (z) felder.ziel = z;
    const eta = etaZahl(msg.Eta); if (eta != null) felder.eta = eta;
    if (msg.MaximumStaticDraught) felder.tiefgang = Number(msg.MaximumStaticDraught);
    if (msg.Type != null) felder.typ = Number(msg.Type);
    // aisstream schreibt FixType, aeltere Faelle Fixtype - beides annehmen,
    // sonst bleibt die Spalte leer und niemand merkt es.
    const g = geraeteTyp(msg); if (g != null) felder.geraet = g;
    if (msg.AisVersion != null) felder.aisVersion = Number(msg.AisVersion);
    if (msg.Dte != null) felder.dte = msg.Dte ? 1 : 0;
    const m = ais.masseFelder(msg.Dimension);
    if (m) Object.assign(felder, m);
  } else if (typ === "StaticDataReport" && msg) {
    // Class B verteilt seine Statik auf Teil A (Name) und Teil B (Rufzeichen,
    // Typ, Masse, Transponder). Teil B traegt statt der Masse ein
    // Mutterschiff, wenn das Geraet an einem Beiboot haengt - dann ist die
    // Dimension leer, und das ist kein Fehler.
    const a = msg.ReportA || {}, b = msg.ReportB || {};
    felder.klasse = "B";
    const n = ais.textSauber(a.Name); if (n) felder.name = n;
    // Teil A und Teil B kommen als ZWEI Nachrichten, und die jeweils andere
    // Haelfte ist dann mit Nullen gefuellt und traegt Valid: false. Ohne
    // diesen Riegel setzte jede Teil-A-Meldung den Schiffstyp auf 0
    // ("keine Angabe") - ein bekannter Typ ging dabei verloren. Nachgesehen
    // an echten Nachrichten: ReportB {CallSign:"", ShipType:0, Valid:false}.
    if (b.Valid !== false) {
      const c = ais.textSauber(b.CallSign); if (c) felder.rufzeichen = c;
      if (b.ShipType) felder.typ = Number(b.ShipType);
      // Die Schreibweise stammt von aisstream und ist samt Tippfehler echt:
      // VenderIDModel/VenderIDSerial. Geraten hatte ich UnitModelCode und
      // SerialNumber - beide Spalten blieben in der Messung leer, 0 von 2775.
      const h = ais.textSauber(b.VendorIDName); if (h) felder.hersteller = h;
      const mo = b.VenderIDModel != null ? b.VenderIDModel : b.UnitModelCode;
      if (mo) felder.modell = String(mo);
      const sn = b.VenderIDSerial != null ? b.VenderIDSerial : b.SerialNumber;
      if (sn) felder.seriennr = String(sn);
      const g = geraeteTyp(b); if (g) felder.geraet = g;
      const m = ais.masseFelder(b.Dimension);
      if (m) Object.assign(felder, m);
    }
  } else {
    return Object.keys(felder).length ? { mmsi, felder, stand, hatPosition } : null;
  }

  return { mmsi, felder, stand, hatPosition };
}

// Der Typ des Positionsgeraets (1 = GPS, 3 = Loran, 15 = intern). aisstream
// schreibt ihn je nach Nachricht als FixType oder Fixtype - der Client nimmt
// aus demselben Grund beide Schreibweisen.
function geraeteTyp(msg) {
  const v = msg.FixType != null ? msg.FixType : msg.Fixtype;
  return v == null ? null : Number(v);
}

// AIS liefert die ETA im Strom als Objekt, Digitraffic als gepackten Integer.
// Gespeichert wird immer der gepackte Integer, damit die Ablage EINE Form hat.
function etaZahl(eta) {
  if (eta == null) return null;
  if (typeof eta === "number") return ais.etaRoh(eta);
  const M = Number(eta.Month) || 0, T = Number(eta.Day) || 0;
  const S = Number(eta.Hour) || 0, m = Number(eta.Minute) || 0;
  if (!M && !T) return null;
  return ais.etaRoh((M << 16) | (T << 11) | (S << 6) | m);
}

module.exports = { Strom, uebersetze, etaZahl };
