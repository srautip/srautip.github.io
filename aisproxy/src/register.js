"use strict";

const fs = require("node:fs");
const path = require("node:path");
const ais = require("./ais");

// Das Schiffsregister - der Teil, an dem ein Proxy etwas kann, das ein
// Browser prinzipiell nicht kann.
//
// Gemessen am 27. Aug. 2026:
//   Wikidata mit VALUES: 200 MMSIs in EINER Abfrage, 0,5 s, 12 % Treffer
//     -> der ganze Bestand in 15 Abfragen statt 2915
//   Digitraffic-Sammelabruf: 1165 Schiffe, 58 KB, 0,8 s - ein Abruf statt 2915
//   Digitraffic fuer unsere Region: 0 von 20. Deren AIS deckt die noerdliche
//     und oestliche Ostsee ab (Flaggen: SE 106, FI 67, RU 40, EE 26 von 400),
//     nicht die Deutsche Bucht. Der Sammelabruf bleibt trotzdem drin, weil er
//     einen einzigen Abruf kostet und die Ostseeraender mitnimmt.
//
// Daraus folgt die geaenderte Kette: Die IMO kommt NICHT mehr aus Digitraffic,
// sondern aus ShipStaticData (AIS-Typ 5) - die liefert der Strom ohnehin.

const AGENT = "aisproxy/0.1 (https://github.com/srautip/srautip.github.io; privat)";

function schlaf(ms) { return new Promise(r => setTimeout(r, ms)); }

class Register {
  constructor(opt) {
    this.konfig = opt.konfig;
    this.speicher = opt.speicher;
    this.zustand = opt.zustand;
    this.log = opt.log || console.log;
    this.laeuft = false;
    this.bericht_ = { laeufe: 0, wikidata: 0, wdTreffer: 0, commons: 0,
                      commonsTreffer: 0, digitraffic: 0, fotos: 0, fehler: 0,
                      letzterLauf: null, dauerMs: 0 };
    fs.mkdirSync(this.konfig.FOTO_VERZEICHNIS, { recursive: true });
  }

  // --- Digitraffic: ein Abruf fuer alles ---------------------------------
  async digitraffic() {
    try {
      // Der Dienst verlangt Accept-Encoding: gzip und antwortet sonst mit 406.
      const res = await fetch(this.konfig.DIGITRAFFIC_URL, {
        headers: { "Accept-Encoding": "gzip", "User-Agent": AGENT },
        signal: AbortSignal.timeout(45000)
      });
      if (!res.ok) throw new Error("HTTP " + res.status);
      const liste = await res.json();
      if (!Array.isArray(liste)) return 0;
      this.bericht_.digitraffic++;
      let n = 0;
      for (const v of liste) {
        if (!v || v.mmsi == null) continue;
        const felder = { quelle: "digitraffic" };
        if (v.imo) felder.imo = Number(v.imo);
        const name = ais.textSauber(v.name); if (name) felder.name = name;
        const call = ais.textSauber(v.callSign); if (call) felder.rufzeichen = call;
        const ziel = ais.textSauber(v.destination); if (ziel) felder.ziel = ziel;
        const eta = ais.etaRoh(v.eta); if (eta) felder.eta = eta;
        // draught kommt in Dezimetern (68 = 6,8 m), Masse in Metern.
        if (v.draught) felder.tiefgang = v.draught / 10;
        if (v.shipType != null) felder.typ = Number(v.shipType);
        const m = ais.masse({ A: v.referencePointA, B: v.referencePointB,
                              C: v.referencePointC, D: v.referencePointD });
        if (m) { felder.laenge = m.laenge; felder.breite = m.breite; }
        // Nur eintragen, was uns betrifft - der Bestand ist ostseeweit.
        if (!this.zustand.hole(v.mmsi)) continue;
        this.speicher.stammSetze(v.mmsi, felder);
        this.zustand.ergaenze(v.mmsi, felder);
        n++;
      }
      return n;
    } catch (e) {
      this.bericht_.fehler++;
      this.log("Digitraffic-Sammelabruf fehlgeschlagen: " + e.message);
      return 0;
    }
  }

  // --- Wikidata: Buendelabfrage ------------------------------------------
  // 200 Kennungen in einer Abfrage statt 200 Abfragen. Genau das kann ein
  // Browser nicht sinnvoll tun - dort interessiert immer nur ein Schiff.
  async wikidata(werte, ueberImo) {
    if (!werte.length) return [];
    const eigenschaft = ueberImo ? "wdt:P458" : "wdt:P587";
    const values = werte.map(w => '"' + String(w) + '"').join(" ");
    const q =
      "SELECT ?ship ?shipLabel ?typeLabel ?schluessel ?imo ?mmsi ?callsign ?gt ?loa ?beam " +
      "?draft ?built ?ownerLabel ?operatorLabel ?builderLabel ?flagLabel ?homeportLabel ?image WHERE { " +
      "VALUES ?schluessel { " + values + " } ?ship " + eigenschaft + " ?schluessel." +
      " OPTIONAL { ?ship wdt:P458 ?imo } OPTIONAL { ?ship wdt:P587 ?mmsi }" +
      " OPTIONAL { ?ship wdt:P2317 ?callsign } OPTIONAL { ?ship wdt:P1093 ?gt }" +
      " OPTIONAL { ?ship wdt:P2043 ?loa } OPTIONAL { ?ship wdt:P2261 ?beam }" +
      " OPTIONAL { ?ship wdt:P2262 ?draft } OPTIONAL { ?ship wdt:P729 ?built }" +
      " OPTIONAL { ?ship wdt:P31 ?type } OPTIONAL { ?ship wdt:P127 ?owner }" +
      " OPTIONAL { ?ship wdt:P137 ?operator } OPTIONAL { ?ship wdt:P176 ?builder }" +
      " OPTIONAL { ?ship wdt:P8047 ?flag } OPTIONAL { ?ship wdt:P532 ?homeport }" +
      " OPTIONAL { ?ship wdt:P18 ?image }" +
      ' SERVICE wikibase:label { bd:serviceParam wikibase:language "de,en" } }';
    try {
      this.bericht_.wikidata++;
      // format=json in der Abfragezeile spart den Accept-Header und damit
      // einen CORS-Vorabflug - im Browser wichtig, hier schadet es nicht.
      const res = await fetch(this.konfig.WIKIDATA_URL + "?format=json&query=" +
        encodeURIComponent(q), {
        headers: { "User-Agent": AGENT },
        signal: AbortSignal.timeout(60000)
      });
      if (!res.ok) throw new Error("HTTP " + res.status);
      const json = await res.json();
      const zeilen = (json.results && json.results.bindings) || [];
      const out = [];
      for (const r of zeilen) {
        const wert = k => (r[k] ? r[k].value : null);
        const zahl = k => (r[k] ? Number(r[k].value) : null);
        let name = wert("shipLabel");
        // Eine blanke Q-Nummer heisst: Wikidata kennt das Objekt, hat aber
        // keine Beschriftung auf de/en. Das ist kein Name.
        if (name && /^Q\d+$/.test(name)) name = null;
        out.push({
          schluessel: wert("schluessel"),
          felder: {
            name, wd_entity: wert("ship"),
            imo: wert("imo") ? Number(wert("imo")) : null,
            rufzeichen: wert("callsign"),
            brz: zahl("gt"), laenge: zahl("loa"), breite: zahl("beam"),
            tiefgang: zahl("draft"),
            baujahr: wert("built") ? wert("built").slice(0, 10) : null,
            eigner: wert("ownerLabel"), betreiber: wert("operatorLabel"),
            werft: wert("builderLabel"), flagge: wert("flagLabel"),
            heimathafen: wert("homeportLabel"),
            // Wikidata gibt http:// aus - auf einer https-Seite ist das
            // Mischinhalt und wird blockiert.
            bild: wert("image") ? wert("image").replace(/^http:\/\//i, "https://") : null
          }
        });
      }
      this.bericht_.wdTreffer += out.length;
      return out;
    } catch (e) {
      this.bericht_.fehler++;
      this.log("Wikidata-Buendel (" + werte.length + (ueberImo ? " IMO" : " MMSI") +
        ") fehlgeschlagen: " + e.message);
      return [];
    }
  }

  // --- Commons: Foto ueber die IMO ---------------------------------------
  // Die IMO MUSS im Dateinamen stehen. Die Volltextsuche ist unscharf: Bei
  // IMO 9892896 ("Bore Wave") lieferte sie "Aftermath of Severn Bore wave" -
  // einen Fluss. Lieber kein Bild als das falsche Schiff.
  async commons(imo) {
    try {
      this.bericht_.commons++;
      const url = this.konfig.COMMONS_URL + "?action=query&generator=search" +
        "&gsrsearch=" + encodeURIComponent("IMO " + imo) +
        "&gsrnamespace=6&gsrlimit=20&prop=imageinfo&iiprop=url|extmetadata" +
        "&iiurlwidth=" + this.konfig.FOTO_BREITE + "&format=json&origin=*";
      const res = await fetch(url, { headers: { "User-Agent": AGENT },
        signal: AbortSignal.timeout(30000) });
      if (!res.ok) throw new Error("HTTP " + res.status);
      const json = await res.json();
      const seiten = (json.query && json.query.pages) || {};
      const treffer = [];
      for (const k of Object.keys(seiten)) {
        const p = seiten[k];
        if (!p || p.ns !== 6 || !p.imageinfo || !/\.(jpe?g|png)$/i.test(p.title)) continue;
        // Die harte Regel: IMO im Dateinamen.
        if (p.title.indexOf(String(imo)) === -1) continue;
        treffer.push(p);
      }
      if (!treffer.length) return null;
      treffer.sort((a, b) => a.title.localeCompare(b.title));
      const p = treffer[0];
      const info = p.imageinfo[0];
      const meta = info.extmetadata || {};
      const entfernen = s => s ? String(s).replace(/<[^>]*>/g, " ").replace(/\s+/g, " ").trim() : null;
      this.bericht_.commonsTreffer++;
      return {
        url: info.thumburl || info.url,
        credit: [entfernen(meta.Artist && meta.Artist.value),
                 entfernen(meta.LicenseShortName && meta.LicenseShortName.value)]
          .filter(Boolean).join(" · ") || "Wikimedia Commons",
        seite: "https://commons.wikimedia.org/wiki/" + encodeURIComponent(p.title)
      };
    } catch (e) {
      this.bericht_.fehler++;
      return null;
    }
  }

  // Bild holen und lokal ablegen. Verkleinert wird NICHT hier - Commons und
  // Wikimedia liefern die gewuenschte Breite selbst (iiurlwidth bzw.
  // ?width=), also braucht der Proxy keine Bildbibliothek.
  async holeFoto(mmsi, url) {
    try {
      const mitBreite = /Special:FilePath/i.test(url) && !/[?&]width=/.test(url)
        ? url + (url.indexOf("?") === -1 ? "?" : "&") + "width=" + this.konfig.FOTO_BREITE
        : url;
      const res = await fetch(mitBreite, { headers: { "User-Agent": AGENT },
        signal: AbortSignal.timeout(30000) });
      if (!res.ok) return null;
      const typ = res.headers.get("content-type") || "";
      if (!/^image\//.test(typ)) return null;
      const buf = Buffer.from(await res.arrayBuffer());
      if (!buf.length) return null;
      const endung = /png/.test(typ) ? ".png" : ".jpg";
      const datei = String(mmsi) + endung;
      fs.writeFileSync(path.join(this.konfig.FOTO_VERZEICHNIS, datei), buf);
      this.bericht_.fotos++;
      return datei;
    } catch (e) { return null; }
  }

  // --- Der Lauf ----------------------------------------------------------
  // Vorratshaltung statt Bedarfsabruf: Die Region ist fest und hat rund 2900
  // Schiffe, also wird der ganze Bestand vorgewaermt. Danach hat jedes Schiff
  // seine Stammdaten sofort, ohne Wartezeit beim Anklicken.
  async lauf() {
    if (this.laeuft) return null;
    this.laeuft = true;
    const t0 = Date.now();
    try {
      const alle = [...this.zustand.schiffe.keys()];
      const faellig = this.speicher.stammFaellig(alle);
      if (!faellig.length) return { faellig: 0 };

      // 1) Digitraffic: ein Abruf, nimmt die Ostseeraender mit.
      await this.digitraffic();
      await schlaf(this.konfig.REGISTER_PAUSE_MS);

      // 2) Wikidata ueber die MMSI, in Buendeln.
      const groesse = this.konfig.WIKIDATA_BUENDEL;
      const gefunden = new Set();
      for (let i = 0; i < faellig.length; i += groesse) {
        const teil = faellig.slice(i, i + groesse);
        const treffer = await this.wikidata(teil, false);
        for (const t of treffer) {
          const mmsi = Number(t.schluessel);
          if (!Number.isFinite(mmsi)) continue;
          gefunden.add(mmsi);
          await this.uebernimm(mmsi, t.felder);
        }
        await schlaf(this.konfig.REGISTER_PAUSE_MS);
      }

      // 3) Wer per MMSI nicht gefunden wurde, aber eine IMO aus AIS hat:
      //    ueber die IMO nachfassen. Das war frueher der Digitraffic-Umweg -
      //    die IMO steht aber ohnehin in ShipStaticData.
      const perImo = new Map();
      for (const mmsi of faellig) {
        if (gefunden.has(mmsi)) continue;
        const s = this.zustand.hole(mmsi);
        const imo = s && s.imo ? s.imo : (this.speicher.stammHole(mmsi) || {}).imo;
        if (imo) perImo.set(String(imo), mmsi);
      }
      const imos = [...perImo.keys()];
      for (let i = 0; i < imos.length; i += groesse) {
        const teil = imos.slice(i, i + groesse);
        const treffer = await this.wikidata(teil, true);
        for (const t of treffer) {
          const mmsi = perImo.get(String(t.schluessel));
          if (!mmsi) continue;
          gefunden.add(mmsi);
          await this.uebernimm(mmsi, t.felder);
        }
        await schlaf(this.konfig.REGISTER_PAUSE_MS);
      }

      // 4) Commons nur fuer Schiffe mit IMO und ohne Wikidata-Foto.
      for (const [imo, mmsi] of perImo) {
        const stamm = this.speicher.stammHole(mmsi);
        if (stamm && stamm.foto_datei) continue;
        const foto = await this.commons(imo);
        await schlaf(this.konfig.REGISTER_PAUSE_MS);
        if (!foto) continue;
        const datei = await this.holeFoto(mmsi, foto.url);
        if (datei) {
          this.speicher.stammSetze(mmsi, {
            foto_datei: datei, foto_credit: foto.credit, gefunden: 1,
            quelle: "commons", geprueft: Date.now()
          });
          gefunden.add(mmsi);
        }
      }

      // 5) Was nichts ergeben hat, wird als Fehltreffer vermerkt. "Nichts
      //    gefunden" IST ein Ergebnis - ohne diesen Vermerk fragt der Proxy
      //    dieselben aussichtslosen Schiffe endlos nach.
      const jetzt = Date.now();
      for (const mmsi of faellig) {
        if (gefunden.has(mmsi)) continue;
        this.speicher.stammSetze(mmsi, { gefunden: 0, geprueft: jetzt });
      }

      this.bericht_.laeufe++;
      this.bericht_.letzterLauf = new Date().toISOString();
      this.bericht_.dauerMs = Date.now() - t0;
      this.log("Register: " + faellig.length + " faellig, " + gefunden.size +
        " gefunden, " + ((Date.now() - t0) / 1000).toFixed(1) + " s");
      return { faellig: faellig.length, gefunden: gefunden.size, dauerMs: Date.now() - t0 };
    } finally {
      this.laeuft = false;
    }
  }

  async uebernimm(mmsi, felder) {
    const daten = { gefunden: 1, quelle: "wikidata", geprueft: Date.now() };
    for (const k of ["name", "wd_entity", "imo", "rufzeichen", "brz", "laenge",
                     "breite", "tiefgang", "baujahr", "eigner", "betreiber",
                     "werft", "flagge", "heimathafen"]) {
      if (felder[k] != null) daten[k] = felder[k];
    }
    if (felder.bild) {
      const datei = await this.holeFoto(mmsi, felder.bild);
      if (datei) { daten.foto_datei = datei; daten.foto_credit = "Wikidata / Wikimedia Commons"; }
    }
    this.speicher.stammSetze(mmsi, daten);
    this.zustand.ergaenze(mmsi, {
      name: daten.name, rufzeichen: daten.rufzeichen, imo: daten.imo,
      laenge: daten.laenge, breite: daten.breite, tiefgang: daten.tiefgang
    });
  }

  bericht() { return Object.assign({}, this.bericht_, { laeuft: this.laeuft }); }
}

module.exports = { Register };
