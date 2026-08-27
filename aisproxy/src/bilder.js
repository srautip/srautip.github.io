"use strict";

// Die Bildwege. Spiegel der Regeln aus dem Client (aisstream/index.html ab
// Z. 5291) plus Flickr, das nur ein Server gehen kann.
//
// Warum gespiegelt und nicht geteilt: Der Client ist eine einzelne HTML-Datei
// ohne Modulsystem, hier laeuft Node. Dasselbe Verfahren wie bei src/ais.js -
// die Regeln stehen zweimal, mit Verweis aufeinander. Wer eine davon aendert,
// muss die andere mitziehen.
//
// Gemessen am 27. Aug. 2026, warum die Reihenfolge so ist:
//   Commons-Kategorie ueber die IMO   24 von 25
//   Commons-Volltext  ueber die IMO    6 von 25   <- das hatte der Proxy bisher
//   Commons ueber MMSI + Name          2 von 44
//   Flickr-Tag "imo<nr>"               9 von 17 (echte Schiffe der Region)
//   Flickr-Tag "mmsi<nr>"              3 von 25
//
// Und der Ertrag der ganzen Kette an 134 echten Schiffen: 65 Bilder statt 14.
// Davon Abzug 47, Commons-Kategorie 10, Flickr 7, Commons-MMSI 1. Der
// Volltextweg trug NICHTS mehr bei (22 Abrufe, 0 Treffer) - er bleibt als
// Rueckfall, wer hier kuerzen will, faengt bei ihm an.

const AGENT = "aisproxy/0.1 (https://github.com/srautip/srautip.github.io; privat)";

// Wortliste ohne Trennkraft. Ohne sie passt jedes Lotsenboot auf jedes andere.
const NAME_STOPP = ["pilotvessel", "pilot", "vessel", "ship", "boat", "tug",
  "supply", "the", "of", "van", "der", "den"];

// AIS sendet ASCII, Commons schreibt richtig: STORSKAR gegen "Storskaer"
// fiele sonst durch, obwohl es dasselbe Schiff ist. Beide Seiten falten.
function faltUm(s) {
  return String(s || "").toLowerCase()
    .replace(/ä|æ|å/g, "a").replace(/ö|ø|œ/g, "o").replace(/ü/g, "u")
    .replace(/ß/g, "ss")
    .normalize("NFD").replace(/[\u0300-\u036f]/g, "");
}

function nameWorte(name) {
  return faltUm(name).split(/[^a-z0-9]+/).filter(w => {
    if (w.length < 4) return false;             // zu kurz = zu unspezifisch
    if (NAME_STOPP.indexOf(w) >= 0) return false;
    if (/^\d+$/.test(w)) return false;          // reine Zahlen
    if (/^[ivxlcdm]+$/.test(w)) return false;   // roemische Ziffern
    return true;
  });
}

// Wortgrenzen, nicht Teilzeichenkette: "Marines" darf "Marlin" nicht
// erfuellen - genau daran haengt die Abweisung des Vietnamfotos, das die
// MMSI 304734000 im Beschreibungstext traegt.
function titelBestaetigt(titel, worte) {
  const t = faltUm(titel);
  for (const w of worte) {
    if (new RegExp("(^|[^a-z0-9])" + w + "($|[^a-z0-9])").test(t)) return true;
  }
  return false;
}

function html(s) { return String(s || "").replace(/<[^>]*>/g, "").trim(); }

function credit(seite) {
  const info = seite.imageinfo && seite.imageinfo[0];
  if (!info) return null;
  const meta = info.extmetadata || {};
  const wer = html(meta.Artist && meta.Artist.value);
  const lizenz = html(meta.LicenseShortName && meta.LicenseShortName.value);
  return {
    url: info.thumburl || info.url,
    credit: [wer, lizenz].filter(Boolean).join(" · ") || "Wikimedia Commons",
    seite: "https://commons.wikimedia.org/wiki/" + encodeURIComponent(seite.title),
    titel: seite.title
  };
}

// Dateien mit der Kennung im Namen zuerst, sonst alphabetisch: Die Auswahl
// muss ueber Laeufe hinweg dieselbe bleiben, sonst zeigt dasselbe Schiff bei
// jedem Abruf ein anderes Bild.
function waehle(seiten, kennung) {
  const dateien = [];
  for (const k of Object.keys(seiten || {})) {
    const s = seiten[k];
    if (!s || s.ns !== 6 || !s.imageinfo) continue;
    if (!/\.(jpe?g|png)$/i.test(s.title)) continue;
    dateien.push(s);
  }
  if (!dateien.length) return null;
  dateien.sort((a, b) => {
    const ai = a.title.indexOf(String(kennung)) !== -1 ? 0 : 1;
    const bi = b.title.indexOf(String(kennung)) !== -1 ? 0 : 1;
    if (ai !== bi) return ai - bi;
    return a.title < b.title ? -1 : 1;
  });
  return credit(dateien[0]);
}

function ersteUnterkategorie(seiten) {
  for (const k of Object.keys(seiten || {})) {
    if (seiten[k] && seiten[k].ns === 14) return seiten[k].title;
  }
  return null;
}

// Der Flickr-Feed nennt den Urheber als 'nobody@flickr.com ("name")'.
function flickrAutor(roh) {
  const m = /\("([^"]+)"\)/.exec(String(roh || ""));
  return m ? m[1] : (roh || "Flickr");
}

class Bilder {
  // hole(url) liefert das geparste JSON. Als Abhaengigkeit hereingereicht,
  // damit die Regeln offline gegen feste Antworten pruefbar sind - ohne das
  // liesse sich nur das Netz testen, nicht die Logik.
  constructor(opt) {
    this.konfig = opt.konfig;
    this.log = opt.log || console.log;
    this.hole = opt.hole || ((url) => this.holeNetz(url));
    this.zaehler = { commonsKategorie: 0, commonsVolltext: 0, commonsMmsi: 0,
                     flickrImo: 0, flickrMmsi: 0, treffer: {}, fehler: 0 };
  }

  async holeNetz(url) {
    const res = await fetch(url, {
      headers: { "User-Agent": AGENT, "Accept-Encoding": "gzip" },
      signal: AbortSignal.timeout(30000)
    });
    if (!res.ok) throw new Error("HTTP " + res.status);
    return res.json();
  }

  commonsUrl(teil) {
    return this.konfig.COMMONS_URL + "?" + teil +
      "&prop=imageinfo&iiprop=url|extmetadata&iiurlwidth=" + this.konfig.FOTO_BREITE +
      "&format=json";
  }

  zaehle(weg, treffer) {
    this.zaehler[weg]++;
    if (treffer) this.zaehler.treffer[weg] = (this.zaehler.treffer[weg] || 0) + 1;
  }

  // Commons pflegt fuer Schiffe eine Kategorie je IMO-Nummer. Sie enthaelt
  // meist keine Dateien direkt, sondern die Namenskategorie des Schiffs -
  // daher zwei Schritte. Kuratiert statt geraten, und deshalb OHNE Titelregel:
  // Ein Foto darf zu Recht einen anderen Namen tragen als der AIS-Feed, weil
  // die IMO am Rumpf bleibt und der Name mit dem Eigner wechselt (Fall
  // BON VIVANT / VESTFJORD, IMO 9052692).
  async commonsKategorie(imo) {
    const json = await this.hole(this.commonsUrl(
      "action=query&generator=categorymembers&gcmtitle=" +
      encodeURIComponent("Category:IMO " + imo) + "&gcmtype=file|subcat&gcmlimit=20"));
    const seiten = json && json.query && json.query.pages;
    const treffer = waehle(seiten, imo);
    if (treffer) { this.zaehle("commonsKategorie", true); return treffer; }
    const unter = ersteUnterkategorie(seiten);
    if (!unter) { this.zaehle("commonsKategorie", false); return null; }
    const json2 = await this.hole(this.commonsUrl(
      "action=query&generator=categorymembers&gcmtitle=" +
      encodeURIComponent(unter) + "&gcmtype=file&gcmlimit=20"));
    const zwei = waehle(json2 && json2.query && json2.query.pages, imo);
    this.zaehle("commonsKategorie", !!zwei);
    return zwei;
  }

  // Rueckfall: Volltextsuche. Hier bleibt die strenge Titelregel noetig - ohne
  // sie lieferte die Suche fuer IMO 9892896 ("Bore Wave") den "Aftermath of
  // Severn Bore wave", also einen Fluss.
  async commonsVolltext(imo) {
    const json = await this.hole(this.commonsUrl(
      "action=query&generator=search&gsrsearch=" +
      encodeURIComponent("IMO " + imo) + "&gsrnamespace=6&gsrlimit=10"));
    const seiten = json && json.query && json.query.pages;
    const streng = {};
    for (const k of Object.keys(seiten || {})) {
      if (seiten[k].title && seiten[k].title.indexOf(String(imo)) !== -1) streng[k] = seiten[k];
    }
    const treffer = waehle(streng, imo);
    this.zaehle("commonsVolltext", !!treffer);
    return treffer;
  }

  // Der zweite Identifikator. Die Kennung liefert die Identitaet, der Name
  // bestaetigt sie - eine neunstellige Zahl kommt auch zufaellig in
  // Beschreibungstexten vor.
  async commonsMmsi(mmsi, name) {
    const worte = nameWorte(name);
    if (!mmsi || !worte.length) return null;
    const json = await this.hole(this.commonsUrl(
      "action=query&generator=search&gsrsearch=" +
      encodeURIComponent('"' + mmsi + '"') + "&gsrnamespace=6&gsrlimit=15"));
    const seiten = json && json.query && json.query.pages;
    const streng = {};
    for (const k of Object.keys(seiten || {})) {
      if (seiten[k].title && titelBestaetigt(seiten[k].title, worte)) streng[k] = seiten[k];
    }
    const treffer = waehle(streng, mmsi);
    this.zaehle("commonsMmsi", !!treffer);
    return treffer;
  }

  // Flickr ueber den oeffentlichen Feed, ohne Schluessel. Der Feed nennt
  // Titel, Autor und Link, aber KEINE Lizenz - bewusste Entscheidung des
  // Betreibers. Ausgewiesen wird deshalb, was da ist: Urheber und Link auf die
  // Fotoseite.
  //
  // Der Tag traegt die Identitaet: "imo9321483" ist eindeutig, eine nackte
  // Zahl waere es nicht. Deshalb nur die beiden gepraegten Formen.
  async flickr(art, kennung) {
    if (!kennung) return null;
    const tag = art + String(kennung);
    const json = await this.hole(this.konfig.FLICKR_URL + "?tags=" +
      encodeURIComponent(tag) + "&format=json&nojsoncallback=1");
    const eintraege = (json && json.items) || [];
    const weg = art === "imo" ? "flickrImo" : "flickrMmsi";
    if (!eintraege.length) { this.zaehle(weg, false); return null; }
    // Aeltester zuerst? Nein - der Feed liefert die neuesten. Sortiert wird
    // nach dem Link, damit dasselbe Schiff bei jedem Lauf dasselbe Bild
    // bekommt; ein wechselndes Bild sieht wie ein Fehler aus.
    eintraege.sort((a, b) => (a.link < b.link ? -1 : 1));
    const e = eintraege[0];
    const klein = (e.media && e.media.m) || "";
    if (!klein) { this.zaehle(weg, false); return null; }
    // _m ist 240 px. _z sind 640 und damit die naechste Stufe ueber den 480,
    // die der Proxy vorhaelt - er verkleinert nicht selbst.
    this.zaehle(weg, true);
    return {
      url: klein.replace(/_m\.jpg$/i, "_z.jpg"),
      credit: flickrAutor(e.author) + " · Flickr",
      seite: e.link,
      titel: e.title
    };
  }
}

module.exports = { Bilder, faltUm, nameWorte, titelBestaetigt, waehle,
                   ersteUnterkategorie, credit, flickrAutor, NAME_STOPP };
