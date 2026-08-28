"use strict";

const fs = require("node:fs");
const path = require("node:path");
const ais = require("./ais");
const { Bilder } = require("./bilder");

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
    this.bericht_ = { laeufe: 0, wikidata: 0, wdTreffer: 0, digitraffic: 0,
                      fotos: 0, fotoFehler: 0, fehler: 0,
                      faellig: 0, fotoFaellig: 0, fotoVersucht: 0, fotoOffen: 0,
                      abzug: null, orte: null, wege: null,
                      letzterLauf: null, laeuftSeit: null, dauerMs: 0 };
    this.bilder = opt.bilder || new Bilder({ konfig: this.konfig, log: this.log });
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
        const m = ais.masseFelder({ A: v.referencePointA, B: v.referencePointB,
                                    C: v.referencePointC, D: v.referencePointD });
        if (m) Object.assign(felder, m);
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
            // Der Typ stand schon in der Abfrage (P31) und wurde bis zum
            // 27. Aug. 2026 weggeworfen. Er ist die einzige Typangabe fuer
            // Schiffe, von denen nie eine Msg 5 hereinkommt - gemessen an der
            // "Liberty of the Seas" (MMSI 309436000): in sechs Minuten neun
            // Positionsmeldungen und keine einzige Statiknachricht, im
            // Snapshot von openwaters kein Typ - Wikidata dagegen sagt
            // "Kreuzfahrtschiff".
            wd_typ: /^Q\d+$/.test(wert("typeLabel") || "") ? null : wert("typeLabel"),
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

  // Bild holen und lokal ablegen. Verkleinert wird NICHT hier - Commons und
  // Wikimedia liefern die gewuenschte Breite selbst (iiurlwidth bzw.
  // ?width=), also braucht der Proxy keine Bildbibliothek.
  // Liefert den Dateinamen, null bei "gibt es nicht" - und wirft bei einem
  // FEHLGESCHLAGENEN Abruf. Der Unterschied ist der Kern: Ein verlorener
  // Download darf nicht als "hat kein Bild" festgeschrieben werden.
  async holeFoto(mmsi, url) {
    const mitBreite = /Special:FilePath/i.test(url) && !/[?&]width=/.test(url)
      ? url + (url.indexOf("?") === -1 ? "?" : "&") + "width=" + this.konfig.FOTO_BREITE
      : url;
    for (let versuch = 0; versuch < 2; versuch++) {
      const res = await fetch(mitBreite, { headers: { "User-Agent": AGENT },
        signal: AbortSignal.timeout(30000) });
      if (res.status === 429) {
        // Gemessen: 25 Bilder ohne Pause hintereinander ergeben 2x HTTP 429.
        // Einmal laenger warten und noch einmal versuchen.
        if (versuch === 0) { await schlaf(this.konfig.FOTO_PAUSE_MS * 5); continue; }
        throw new Error("HTTP 429");
      }
      if (!res.ok) throw new Error("HTTP " + res.status);
      const typ = res.headers.get("content-type") || "";
      if (!/^image\//.test(typ)) return null;
      const buf = Buffer.from(await res.arrayBuffer());
      if (!buf.length) return null;
      const endung = /png/.test(typ) ? ".png" : ".jpg";
      const datei = String(mmsi) + endung;
      fs.writeFileSync(path.join(this.konfig.FOTO_VERZEICHNIS, datei), buf);
      this.bericht_.fotos++;
      return datei;
    }
    return null;
  }

  // --- Die Ortsliste: einmal holen, dann Jahre still ---------------------
  //
  // Das Zielfeld im AIS ist Freitext (ITU-R M.1371, 20 Zeichen). Viele Crews
  // schreiben freiwillig einen UN/LOCODE hinein, viele aber auch "HAMBURG",
  // "FOR ORDERS" oder einen Tippfehler. Gemessen an 1 538 Zielangaben aus dem
  // Betrieb: 320 reine Codes, 84 Routen mit Trenner, 55 Codes mit
  // Leerzeichen - und 1 079 Klartext.
  //
  // Aufgeloest wird deshalb NUR gegen die offizielle Liste. Sie hat 116 213
  // Zeilen; behalten werden die 17 596 Seehaefen (Function beginnt mit 1) -
  // ein Schiff faehrt keinen Bahnhof an, und die Auswahl haelt die Tabelle
  // klein, ohne bei der Abdeckung etwas zu kosten (gemessen 84 % gegen 86 %
  // mit allen Codes).
  async ortAbzug() {
    const stand = this.speicher.ortStand();
    if (stand.eintraege && Date.now() - stand.geholt < this.konfig.ORT_ABZUG_MS) {
      this.bericht_.orte = {
        eintraege: stand.eintraege, uebersprungen: true,
        zeit: new Date(stand.geholt).toISOString()
      };
      return null;
    }
    try {
      const res = await fetch(this.konfig.ORT_URL, {
        headers: { "User-Agent": AGENT, "Accept-Encoding": "gzip" },
        signal: AbortSignal.timeout(120000)
      });
      if (!res.ok) throw new Error("HTTP " + res.status);
      const text = await res.text();
      const zeilen = [];
      // Handgeschriebener CSV-Leser statt einer Abhaengigkeit: Die Datei hat
      // genau ein Format, und Namen mit Komma stehen in Anfuehrungszeichen.
      for (const zeile of text.split("\n")) {
        const f = csvFelder(zeile);
        if (f.length < 8) continue;
        const [, land, ort, name, , , , funktion] = f;
        if (!land || !ort || !name) continue;
        if ((funktion || "")[0] !== "1") continue;      // nur Seehaefen
        zeilen.push({ code: land + ort, name, land, funktion });
      }
      const n = this.speicher.ortSchreibe(zeilen);
      this.log("Ortsliste: " + n + " Seehaefen uebernommen");
      this.bericht_.orte = { eintraege: n, zeit: new Date().toISOString() };
      return n;
    } catch (e) {
      this.bericht_.fehler++;
      this.log("Ortsliste holen fehlgeschlagen: " + e.message);
      this.bericht_.orte = { fehler: e.message };
      return null;
    }
  }

  // --- Der Bildabzug: alles auf einmal statt je Schiff --------------------
  //
  // Gemessen am 27. Aug. 2026: Wikidata fuehrt 96 120 Objekte mit IMO, davon
  // 17 127 mit Bild, und 38 167 mit MMSI, davon 8 638 mit Bild. Der KOMPLETTE
  // Abzug IMO->Bild kommt in EINER Abfrage: 17 144 Zeilen, 0,89 MB schlank,
  // 9,8 s. Ueber die MMSI 8 638 Zeilen in 0,9 s.
  //
  // Das ist der eigentliche Vorteil des Servers. Der Client kann nur je Schiff
  // fragen und trifft dabei den Moment, in dem er fragt: Ist die IMO noch
  // nicht per ShipStaticData eingetroffen, geht die Frage ins Leere - und war
  // danach beantwortet. Mit dem Abzug ist die Antwort schon da, wenn die IMO
  // Stunden spaeter kommt.
  async bildAbzug() {
    const stand = this.speicher.bildIndexStand();
    const juengste = Math.max(0, ...Object.keys(stand).map(a => stand[a].geholt || 0));
    if (juengste && Date.now() - juengste < this.konfig.BILD_ABZUG_MS) {
      // Uebersprungen, weil der Abzug frisch ist - und genau das muss im
      // Bericht stehen. Vorher blieb `abzug` in diesem Fall null, und ein
      // frisch neu gestarteter Proxy sah aus, als haette er den Abzug nie
      // geholt: Die Zaehler leben im Prozess, der Index in der Datenbank.
      this.bericht_.abzug = {
        zeit: new Date(juengste).toISOString(),
        uebersprungen: true,
        alterStunden: Number(((Date.now() - juengste) / 3600000).toFixed(1)),
        naechster: new Date(juengste + this.konfig.BILD_ABZUG_MS).toISOString()
      };
      for (const art of Object.keys(stand)) this.bericht_.abzug[art] = stand[art].eintraege;
      return null;
    }

    const abfragen = [
      ["imo", "SELECT ?k ?bild WHERE { ?s wdt:P458 ?k ; wdt:P18 ?bild }"],
      ["mmsi", "SELECT ?k ?bild WHERE { ?s wdt:P587 ?k ; wdt:P18 ?bild }"]
    ];
    const bericht = {};
    for (const [art, q] of abfragen) {
      try {
        this.bericht_.wikidata++;
        const res = await fetch(this.konfig.WIKIDATA_URL + "?format=json&query=" +
          encodeURIComponent(q), {
          headers: { "User-Agent": AGENT, "Accept-Encoding": "gzip" },
          signal: AbortSignal.timeout(120000)
        });
        if (!res.ok) throw new Error("HTTP " + res.status);
        const json = await res.json();
        const zeilen = (json.results && json.results.bindings) || [];
        const paare = [];
        for (const z of zeilen) {
          if (!z.k || !z.bild) continue;
          // Wikidata gibt http:// aus - auf einer https-Seite waere das
          // Mischinhalt, und der Proxy laedt es ohnehin selbst.
          paare.push([z.k.value, z.bild.value.replace(/^http:\/\//i, "https://")]);
        }
        bericht[art] = this.speicher.bildIndexSchreibe(art, paare);
        this.log("Bildabzug " + art + ": " + paare.length + " Zuordnungen");
      } catch (e) {
        this.bericht_.fehler++;
        this.log("Bildabzug " + art + " fehlgeschlagen: " + e.message);
      }
      await schlaf(this.konfig.REGISTER_PAUSE_MS);
    }
    this.bericht_.abzug = Object.assign({ zeit: new Date().toISOString() }, bericht);
    return bericht;
  }

  // --- Der Fotolauf ------------------------------------------------------
  //
  // Getrennt vom Registerlauf, weil "hat Stammdaten" und "hat ein Bild" zwei
  // Fragen sind. Vorher gab es nur ein gefunden-Flag: Ein Schiff mit
  // Wikidata-Stammdaten ohne Bild war 30 Tage gesperrt, auch wenn nur der
  // Download an einem 429 gescheitert war.
  //
  // Reihenfolge der Wege, jeder Schritt gemessen (siehe bilder.js):
  //   Abzug (kein Netzabruf) -> Commons-Kategorie -> Commons-Volltext
  //   -> Commons MMSI+Name -> Flickr imo -> Flickr mmsi
  async fotoLauf(alle) {
    const faellig = this.speicher.fotoFaellig(alle);
    // Reihenfolge nach Aussicht und Preis, in drei Stufen:
    //
    //   1. im Abzug              ein Download, KEIN Suchabruf
    //   2. mit IMO, nicht im Abzug   bis zu vier Suchabrufe, gute Aussicht
    //   3. ohne IMO                  zwei Abrufe, magere Aussicht
    //
    // Der Grund ist der Rueckstand: Beim ersten Lauf auf einem gefuellten
    // Server sind alle 2900 Schiffe faellig, die Obergrenze laesst aber nur
    // einen Teil zu. Gemessen kostet ein Schiff im Schnitt 2,8 s, eines aus
    // dem Abzug knapp eine - die billigen Treffer zuerst zu nehmen bringt in
    // denselben Minuten ein Vielfaches an Bildern.
    const ausAbzug = [], mitImo = [], ohneImo = [];
    for (const mmsi of faellig) {
      const imo = this.imoVon(mmsi);
      const imAbzug = (imo && this.speicher.bildIndexHole("imo", imo)) ||
        this.speicher.bildIndexHole("mmsi", String(mmsi));
      if (imAbzug) ausAbzug.push(mmsi);
      else if (imo) mitImo.push(mmsi);
      else ohneImo.push(mmsi);
    }
    const reihe = ausAbzug.concat(mitImo, ohneImo);
    const grenze = Math.min(reihe.length, this.konfig.FOTO_MAX_PRO_LAUF);
    let neu = 0, versucht = 0;
    // Die Zahlen wandern SOFORT in den Bericht, nicht erst am Ende: Der erste
    // Lauf auf einem gefuellten Server dauert Minuten, und bis dahin stand im
    // Status ueberall 0 - ununterscheidbar von "tut nichts".
    this.bericht_.fotoFaellig = faellig.length;
    this.bericht_.fotoOffen = reihe.length;
    for (let i = 0; i < grenze; i++) {
      const mmsi = reihe[i];
      versucht++;
      this.bericht_.fotoVersucht = versucht;
      this.bericht_.fotoOffen = reihe.length - versucht;
      try {
        if (await this.einFoto(mmsi)) neu++;
      } catch (e) {
        // Ein gescheiterter Abruf wird NICHT vermerkt - das Schiff bleibt
        // faellig. Genau daran hing der gemeldete Zustand.
        this.bericht_.fotoFehler++;
      }
      await schlaf(this.konfig.FOTO_PAUSE_MS);
    }
    const offen = reihe.length - versucht;
    this.bericht_.fotoFaellig = faellig.length;
    this.bericht_.fotoVersucht = versucht;
    this.bericht_.fotoOffen = offen;
    if (offen) this.log("Fotolauf: " + offen + " Schiffe bleiben fuer den naechsten Lauf");
    return { faellig: faellig.length, versucht, neu, offen };
  }

  imoVon(mmsi) {
    const s = this.zustand.hole(mmsi);
    if (s && s.imo) return String(s.imo);
    const stamm = this.speicher.stammHole(mmsi);
    return stamm && stamm.imo ? String(stamm.imo) : null;
  }

  // Ein Schiff, alle Wege der Reihe nach. Wirft, wenn ein Abruf scheitert -
  // dann bleibt das Schiff faellig, statt als "kein Bild" zu gelten.
  async einFoto(mmsi) {
    const imo = this.imoVon(mmsi);
    const stamm = this.speicher.stammHole(mmsi) || {};
    const s = this.zustand.hole(mmsi);
    const name = (s && s.name) || stamm.name || null;

    let treffer = null, quelle = null;
    // 1) Der Abzug - ohne Netzabruf, deshalb zuerst.
    const ausAbzug = (imo && this.speicher.bildIndexHole("imo", imo)) ||
      this.speicher.bildIndexHole("mmsi", String(mmsi));
    if (ausAbzug) {
      treffer = { url: ausAbzug, credit: "Wikidata / Wikimedia Commons", seite: null };
      quelle = "wikidata";
    }
    const wege = [];
    if (imo) {
      wege.push(["commonsKategorie", () => this.bilder.commonsKategorie(imo)]);
      wege.push(["commonsVolltext", () => this.bilder.commonsVolltext(imo)]);
    }
    wege.push(["commonsMmsi", () => this.bilder.commonsMmsi(String(mmsi), name)]);
    if (this.konfig.FLICKR_AN) {
      if (imo) wege.push(["flickrImo", () => this.bilder.flickr("imo", imo)]);
      wege.push(["flickrMmsi", () => this.bilder.flickr("mmsi", String(mmsi))]);
    }
    for (const [weg, fn] of wege) {
      if (treffer) break;
      const t = await fn();
      await schlaf(this.konfig.FOTO_PAUSE_MS);
      if (t) { treffer = t; quelle = weg; }
    }

    if (!treffer) {
      // "Nichts gefunden" ist ein Ergebnis - aber ein unvollstaendiges, wenn
      // die IMO fehlte. Dann haelt der Vermerk nur kurz, damit das Schiff
      // wiederkommt, sobald ShipStaticData die IMO nachreicht.
      this.speicher.stammSetze(mmsi, {
        foto_geprueft: Date.now(), foto_quelle: imo ? "nichts" : "teil"
      });
      return false;
    }
    const datei = await this.holeFoto(mmsi, treffer.url);
    if (!datei) {
      this.speicher.stammSetze(mmsi, {
        foto_geprueft: Date.now(), foto_quelle: imo ? "nichts" : "teil"
      });
      return false;
    }
    this.speicher.stammSetze(mmsi, {
      foto_datei: datei, foto_credit: treffer.credit, foto_seite: treffer.seite,
      foto_quelle: quelle, foto_geprueft: Date.now()
    });
    return true;
  }

  // --- Der Lauf ----------------------------------------------------------
  // Vorratshaltung statt Bedarfsabruf: Die Region ist fest und hat rund 2900
  // Schiffe, also wird der ganze Bestand vorgewaermt. Danach hat jedes Schiff
  // seine Stammdaten sofort, ohne Wartezeit beim Anklicken.
  async lauf() {
    if (this.laeuft) return null;
    this.laeuft = true;
    const t0 = Date.now();
    this.bericht_.laeuftSeit = new Date(t0).toISOString();
    try {
      const alle = [...this.zustand.schiffe.keys()];

      // 0) Der Bildabzug. Muss VOR allem anderen laufen: Er beantwortet die
      //    Fotofrage fuer den halben Bestand ohne eine einzige Abfrage je
      //    Schiff.
      await this.bildAbzug();
      // Die Ortsliste steht daneben: einmal geholt, dann 90 Tage still.
      await this.ortAbzug();

      const faellig = this.speicher.stammFaellig(alle);
      if (!faellig.length) {
        // Auch ohne faellige Stammdaten kann ein Foto fehlen - der Fotolauf
        // hat sein eigenes Fristenwerk und darf hier nicht mit aussteigen.
        const nur = await this.fotoLauf(alle);
        this.bericht_.laeufe++;
        this.bericht_.letzterLauf = new Date().toISOString();
        return { faellig: 0, fotos: nur };
      }

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
          this.uebernimm(mmsi, t.felder);
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
          this.uebernimm(mmsi, t.felder);
        }
        await schlaf(this.konfig.REGISTER_PAUSE_MS);
      }

      // 4) Was nichts ergeben hat, wird als Fehltreffer vermerkt. "Nichts
      //    gefunden" IST ein Ergebnis - ohne diesen Vermerk fragt der Proxy
      //    dieselben aussichtslosen Schiffe endlos nach.
      const jetzt = Date.now();
      for (const mmsi of faellig) {
        if (gefunden.has(mmsi)) continue;
        this.speicher.stammSetze(mmsi, { gefunden: 0, geprueft: jetzt });
      }

      // 5) Die Fotos, getrennt vom Registerstand und mit eigenem Takt.
      const fotos = await this.fotoLauf(alle);

      this.bericht_.laeufe++;
      this.bericht_.faellig = faellig.length;
      this.bericht_.letzterLauf = new Date().toISOString();
      this.bericht_.dauerMs = Date.now() - t0;
      this.log("Register: " + faellig.length + " faellig, " + gefunden.size +
        " gefunden, " + fotos.neu + " Fotos (" + fotos.versucht + " von " +
        fotos.faellig + " Schiffen versucht, " + fotos.offen + " offen), " +
        ((Date.now() - t0) / 1000).toFixed(1) + " s");
      return { faellig: faellig.length, gefunden: gefunden.size,
               fotos, dauerMs: Date.now() - t0 };
    } finally {
      this.laeuft = false;
    }
  }

  // Kein Fotodownload mehr hier drin. Vorher hing er in dieser Schleife und
  // lief damit ohne jede Pause 26-mal hintereinander - gemessen 2 von 25 mit
  // HTTP 429, und jedes verlorene Bild galt danach 30 Tage als "hat keins".
  // Die Fotos holt jetzt fotoLauf(), getaktet und mit eigenem Fristenwerk.
  uebernimm(mmsi, felder) {
    const daten = { gefunden: 1, quelle: "wikidata", geprueft: Date.now() };
    for (const k of ["name", "wd_entity", "wd_typ", "imo", "rufzeichen", "brz", "laenge",
                     "breite", "tiefgang", "baujahr", "eigner", "betreiber",
                     "werft", "flagge", "heimathafen"]) {
      if (felder[k] != null) daten[k] = felder[k];
    }
    this.speicher.stammSetze(mmsi, daten);
    this.zustand.ergaenze(mmsi, {
      name: daten.name, rufzeichen: daten.rufzeichen, imo: daten.imo,
      laenge: daten.laenge, breite: daten.breite, tiefgang: daten.tiefgang
    });
  }

  // wege kommt direkt aus den Zaehlern: Sie erst am Ende des Laufs zu
  // uebernehmen hiess, waehrend des ersten - minutenlangen - Laufs steht dort
  // null, obwohl gerade Bilder gesucht werden.
  bericht() {
    return Object.assign({}, this.bericht_, {
      laeuft: this.laeuft,
      wege: this.bilder.zaehler
    });
  }
}

// Eine CSV-Zeile in Felder zerlegen. Anfuehrungszeichen umschliessen Felder
// mit Komma ("Saint Petersburg (ex Leningrad), Port"), doppelte
// Anfuehrungszeichen stehen fuer eines.
function csvFelder(zeile) {
  const out = [];
  let feld = "", inAnf = false;
  for (let i = 0; i < zeile.length; i++) {
    const c = zeile[i];
    if (inAnf) {
      if (c === '"') {
        if (zeile[i + 1] === '"') { feld += '"'; i++; } else inAnf = false;
      } else feld += c;
    } else if (c === '"') inAnf = true;
    else if (c === ",") { out.push(feld); feld = ""; }
    else if (c !== "\r") feld += c;
  }
  out.push(feld);
  return out;
}

module.exports = { Register, csvFelder };
