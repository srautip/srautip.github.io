"use strict";

// Schleifen finden: wo ein Schiff dorthin zurueckkehrt, wo es schon war.
//
// Der Weg hierher, damit ihn niemand noch einmal gehen muss. Nachgemessen am
// 30. Aug. 2026 an 3 437 echten Spuren (553 457 Punkte, 8 h, ganze Region):
//
//   1. "Viel Kursaenderung" allein reicht NICHT. Die Elbfaehren (BRESLAU,
//      KLEINENSIEL, OPPELN) sammeln in acht Stunden mehr Kursaenderung als
//      jedes Lotsenboot - sie wenden ja an jedem Ende. Nach dieser Kennzahl
//      standen sie ganz oben, und die Lotsen darunter.
//   2. "Umweg = Weg/Luftlinie" ist unbrauchbar: Wer dort endet, wo er
//      angefangen hat, hat Luftlinie 0 und damit Umweg unendlich. Gemessen
//      kamen Werte bis 101 606 heraus - eine Kennzahl, die so ausschlaegt,
//      sagt nichts mehr.
//   3. "Laenglichkeit" (Hauptachsen der Punktwolke) trennt Faehre und Lotse
//      auch nicht sauber: BRESLAU 5,92 gegen PILOTVESSEL HANSE 5,44. Zwei
//      Prozent Abstand ist keine Grenze, das ist ein Zufall.
//
// Was wirklich trennt, ist die Schleife SELBST, nicht eine Kennzahl ueber die
// ganze Spur: Die Spur kommt an einen frueheren Punkt zurueck, und die so
// geschlossene Runde umschliesst Flaeche. Eine Pendelfahrt kommt auch zurueck,
// umschliesst aber fast nichts - sie ist ein Strich. Deshalb steht hier die
// Rundheit als Bedingung und nicht die Kursaenderung.
//
// Der Nebengewinn: Eine Schleife hat damit einen ORT und eine ZEIT, keine
// Note. Genau das braucht die Karte.

// --- Die Stellschrauben, jede an einer Messung ------------------------------

// So nah muss die Spur an einen frueheren Punkt zurueck, damit die Runde als
// geschlossen gilt. 400 m: Der Rasterabstand im Replay ist 60 s, ein Schiff
// mit 8 kn legt darin 250 m zurueck - enger waere ein Wettlauf mit der
// Abtastung.
const RUECKKEHR_M = 400;
// Kuerzere Runden sind Manoevrieren am Liegeplatz, keine Schleife.
const MIN_WEG_M = 800;
// Groesser ist eine Reise, keine Schleife. Gemessen liegen 90 % der gefundenen
// Gebiete unter 3,7 km Radius; 6 km Durchmesser laesst die echten durch und
// haelt Kuestenrundfahrten draussen.
const MAX_DURCHMESSER_M = 6000;
// A / (L^2/4pi): 1,0 waere ein exakter Kreis, 0 ein Strich. Gemessen:
// GENTLE LEADER 0,58 · KAYLEE 0,56 · PILOTT.WANGEROOG 0,46 · WESER PILOT 0,33
// gegen die Faehren KLEINENSIEL 0,22 und OPPELN 0,001. Die Schranke liegt
// bewusst NIEDRIG: Sie soll den Strich ausschliessen, nicht die Faehre - die
// wird in der Anzeige unterschieden, nicht hier verworfen.
const MIN_RUNDHEIT = 0.06;
// Laenger her ist Zufall: dass ein Schiff nach vier Stunden zufaellig wieder
// an derselben Stelle vorbeikommt, ist keine Runde.
const MAX_RUNDE_S = 3 * 3600;
// 40 kn. Darueber ist es ein Datenfehler, kein Manoever. Gemessen: GRIETJE
// (Autotransporter) kam mit drei solchen Spruengen auf 443 km Weg in 8 h -
// nach dem Filter sind es 2,7 km, und die Spur ist unauffaellig.
const SPRUNG_MS = 20.6;
// Was naeher als das beieinander liegt, ist EIN Gebiet. 3 km: Die verdichteten
// Gebiete bleiben damit gemessen bei median 1,6 km Radius und hoechstens
// 6,2 km - sie wachsen also nicht aus dem Ruder.
const ZUSAMMEN_M = 3000;

// Wer von Berufs wegen kreist. Das ist KEIN Ausschluss - diese Schiffe stehen
// weiter auf der Karte, nur leiser. Ein Lotsenboot, das Schleifen faehrt, ist
// keine Auffaelligkeit; ein Autotransporter, der es tut, ist eine.
const BERUF = new Set([
  30,             // Fischerei
  31, 32, 52,     // Schlepper
  33,             // Bagger und Unterwasserarbeiten
  50,             // Lotsenboot
  51,             // SAR
  53,             // Hafenboot
  54,             // Umweltschutz
  55,             // Behoerden
  60, 61, 62, 63, 64, 65, 66, 67, 68, 69   // Passagier, hier ueberwiegend Faehren
]);

function berufsschleifer(typ) {
  return typ != null && BERUF.has(Number(typ));
}

// --- Geometrie -------------------------------------------------------------

// Meter je Grad. In der Region (53-56 Grad Nord) ist die ebene Naeherung auf
// wenige Meter genau, und die Schleifen sind Kilometer gross.
function massstab(lat) {
  return { ky: 111320, kx: 111320 * Math.cos(lat * Math.PI / 180) };
}

// Spurpunkte [t, lat*1e6, lon*1e6, sog*10, cog*10] in ebene Meter, ohne
// Spruenge. Rueckgabe: [{ t, x, y, sog, lat, lon }]
function saeubere(punkte) {
  const roh = [];
  for (const p of punkte) {
    if (!p || p[1] == null || p[2] == null) continue;
    roh.push({ t: p[0], lat: p[1] / 1e6, lon: p[2] / 1e6, sog: (p[3] || 0) / 10 });
  }
  if (roh.length < 8) return [];
  const mittelLat = roh.reduce((s, q) => s + q.lat, 0) / roh.length;
  const { kx, ky } = massstab(mittelLat);
  const aus = [];
  for (const q of roh) {
    const x = q.lon * kx, y = q.lat * ky;
    if (aus.length) {
      const v = aus[aus.length - 1];
      const dt = Math.max(q.t - v.t, 1);
      if (Math.hypot(x - v.x, y - v.y) / dt > SPRUNG_MS) continue;
    }
    aus.push({ t: q.t, x, y, sog: q.sog, lat: q.lat, lon: q.lon });
  }
  return aus;
}

// Die Schleifen einer einzelnen Spur.
function schleifen(punkte) {
  const P = saeubere(punkte);
  if (P.length < 8) return [];
  const kum = [0];
  for (let i = 1; i < P.length; i++) {
    kum.push(kum[i - 1] + Math.hypot(P[i].x - P[i - 1].x, P[i].y - P[i - 1].y));
  }
  const aus = [];
  let i = 0;
  while (i < P.length - 3) {
    let ende = -1;
    for (let j = i + 3; j < P.length; j++) {
      if (P[j].t - P[i].t > MAX_RUNDE_S) break;
      if (kum[j] - kum[i] < MIN_WEG_M) continue;
      if (Math.hypot(P[j].x - P[i].x, P[j].y - P[i].y) <= RUECKKEHR_M) { ende = j; break; }
    }
    if (ende < 0) { i++; continue; }

    const runde = P.slice(i, ende + 1);
    const L = kum[ende] - kum[i];
    // Schnuersenkelformel ueber die geschlossene Runde. Der Betrag, weil die
    // Drehrichtung hier nichts zur Sache tut.
    let zwei = 0;
    for (let k = 0; k < runde.length; k++) {
      const a = runde[k], b = runde[(k + 1) % runde.length];
      zwei += a.x * b.y - b.x * a.y;
    }
    const flaeche = Math.abs(zwei) / 2;
    let cx = 0, cy = 0;
    for (const q of runde) { cx += q.x; cy += q.y; }
    cx /= runde.length; cy /= runde.length;
    let r = 0;
    for (const q of runde) r = Math.max(r, Math.hypot(q.x - cx, q.y - cy));
    const rundheit = L > 1 ? flaeche / (L * L / (4 * Math.PI)) : 0;

    if (2 * r <= MAX_DURCHMESSER_M && rundheit >= MIN_RUNDHEIT) {
      let la = 0, lo = 0;
      for (const q of runde) { la += q.lat; lo += q.lon; }
      const sogs = runde.map(q => q.sog).sort((a, b) => a - b);
      aus.push({
        von: P[i].t, bis: P[ende].t,
        lat: la / runde.length, lon: lo / runde.length,
        radius: Math.round(r), weg: Math.round(L),
        rund: Number(rundheit.toFixed(3)),
        sog: sogs[sogs.length >> 1]
      });
      i = ende;          // abgehakte Runde nicht noch einmal von innen finden
    } else {
      i++;
    }
  }
  return aus;
}

// Einzelne Schleifen zu Gebieten verdichten. Der Keim ist jeweils die
// GROESSTE noch freie Schleife - so waechst ein Gebiet um seinen Kern und
// nicht von einem zufaelligen Rand aus.
function gebiete(ereignisse, zusammenM) {
  const naehe = zusammenM || ZUSAMMEN_M;
  const sortiert = ereignisse.slice().sort((a, b) => b.radius - a.radius);
  const roh = [];
  for (const e of sortiert) {
    const { kx, ky } = massstab(e.lat);
    let dazu = null;
    for (const g of roh) {
      if (Math.hypot((e.lon - g.lat0lon) * kx, (e.lat - g.lat0lat) * ky) <= naehe) { dazu = g; break; }
    }
    if (dazu) dazu.ev.push(e);
    else roh.push({ lat0lat: e.lat, lat0lon: e.lon, ev: [e] });
  }
  return roh.map(g => {
    let la = 0, lo = 0;
    for (const e of g.ev) { la += e.lat; lo += e.lon; }
    la /= g.ev.length; lo /= g.ev.length;
    const { kx, ky } = massstab(la);
    let r = 0;
    for (const e of g.ev) {
      r = Math.max(r, Math.hypot((e.lon - lo) * kx, (e.lat - la) * ky) + e.radius);
    }
    const mmsis = [...new Set(g.ev.map(e => e.mmsi))].sort((a, b) => a - b);
    return {
      lat: Number(la.toFixed(5)), lon: Number(lo.toFixed(5)),
      radius: Math.round(r),
      schleifen: g.ev.length,
      schiffe: mmsis,
      von: Math.min(...g.ev.map(e => e.von)),
      bis: Math.max(...g.ev.map(e => e.bis))
    };
  });
}

// --- Der Laufbetrieb -------------------------------------------------------

class Anomalie {
  // Rechnet in Kacheln und gibt zwischen ihnen die Schleife frei. Der Grund
  // steht in einer Messung: Die Erkennung ueber die GANZE Region auf einmal
  // brauchte 4,6 s am Stueck. So lange darf hier nichts blockieren - der
  // Proxy nimmt in dieser Zeit rund 150 AIS-Meldungen entgegen, und die
  // laegen dann alle im Rueckstau.
  constructor({ konfig, speicher, zustand, log }) {
    this.konfig = konfig;
    this.speicher = speicher;
    this.zustand = zustand;
    this.log = log || (() => {});
    this.ereignisse = [];
    this.gerechnet = 0;
    this.dauerMs = 0;
    this.laeuft = false;
    this.laeufe = 0;
  }

  // Die Region in Kacheln, mit Ueberlappung. Ohne den Rand faende eine
  // Schleife genau auf einer Kachelgrenze in keiner der beiden Kacheln statt,
  // weil ihre Spur dort zerschnitten waere. Der Rand ist groesser als der
  // groesste zugelassene Schleifendurchmesser (6 km), also kann keine
  // durchrutschen; die Dopplungen faengt der Schluessel unten ab.
  kacheln() {
    const R = this.konfig.REGION;
    const kante = this.konfig.ANOMALIE_KACHEL_GRAD;
    const rand = this.konfig.ANOMALIE_KACHEL_RAND_GRAD;
    const aus = [];
    for (let la = R.latMin; la < R.latMax; la += kante) {
      for (let lo = R.lonMin; lo < R.lonMax; lo += kante) {
        aus.push({
          latMin: Math.max(la - rand, R.latMin - rand),
          lonMin: Math.max(lo - rand, R.lonMin - rand),
          latMax: Math.min(la + kante + rand, R.latMax + rand),
          lonMax: Math.min(lo + kante + rand, R.lonMax + rand)
        });
      }
    }
    return aus;
  }

  async lauf() {
    if (this.laeuft) return;         // ein zweiter Lauf brauchte doppelt so lange
    this.laeuft = true;
    const t0 = Date.now();
    const bisS = Math.floor(Date.now() / 1000);
    const vonS = bisS - this.konfig.ANOMALIE_STUNDEN * 3600;
    const gesehen = new Set();
    const aus = [];
    try {
      for (const kachel of this.kacheln()) {
        // Zwischen den Kacheln die Schleife freigeben. setImmediate und nicht
        // await auf nichts: Nur so kommen die wartenden Ein-/Ausgaben - der
        // AIS-Strom - vor der naechsten Kachel dran.
        await new Promise(f => setImmediate(f));
        let spuren;
        try {
          spuren = this.speicher.spuren(kachel, vonS, bisS, this.konfig.ANOMALIE_SCHRITT_S);
        } catch (e) { this.log("Anomalie, Kachel uebersprungen: " + e.message); continue; }
        for (const s of spuren) {
          for (const e of schleifen(s.punkte)) {
            // Dieselbe Schleife kann aus zwei ueberlappenden Kacheln kommen.
            // Der Schluessel ist Schiff und Startzeit - beides steht fest,
            // egal aus welcher Kachel die Spur kam.
            const k = s.mmsi + ":" + e.von;
            if (gesehen.has(k)) continue;
            gesehen.add(k);
            e.mmsi = s.mmsi;
            aus.push(e);
          }
        }
      }
      this.ereignisse = aus;
      this.gerechnet = Date.now();
      this.dauerMs = this.gerechnet - t0;
      this.laeufe++;
      this.log("Anomalie: " + aus.length + " Schleifen von " +
        new Set(aus.map(e => e.mmsi)).size + " Schiffen in " + this.dauerMs + " ms");
    } finally {
      this.laeuft = false;
    }
  }

  // Die Auskunft fuer die Karte. Verdichtet wird bei JEDER Anfrage neu, nicht
  // im Lauf: Das kostet gemessen 23 ms fuer die ganze Region und erlaubt
  // dafuer ein freies Zeitfenster, ohne alles neu zu erkennen.
  hole(box, stunden) {
    const grenze = Math.floor(Date.now() / 1000) - stunden * 3600;
    const drin = this.ereignisse.filter(e =>
      e.bis >= grenze &&
      e.lat >= box.latMin && e.lat <= box.latMax &&
      e.lon >= box.lonMin && e.lon <= box.lonMax);
    const gb = gebiete(drin, this.konfig.ANOMALIE_ZUSAMMEN_M);
    for (const g of gb) {
      let beruf = 0;
      for (const m of g.schiffe) {
        const s = this.zustand && this.zustand.schiffe.get(m);
        const typ = s && s.typ != null ? s.typ
          : (this.speicher.stammHole(m) || {}).typ;
        if (berufsschleifer(typ)) beruf++;
      }
      g.beruf = beruf;
      g.andere = g.schiffe.length - beruf;
      // Die Einstufung faellt HIER und nicht im Client: Sie haengt an den
      // Schiffstypen, die nur der Proxy kennt.
      g.stufe = g.andere > 0 ? "auffaellig" : "gewohnt";
    }
    gb.sort((a, b) => (b.andere - a.andere) || (b.schiffe.length - a.schiffe.length));
    return gb;
  }

  bericht() {
    return {
      laeufe: this.laeufe,
      schleifen: this.ereignisse.length,
      schiffe: new Set(this.ereignisse.map(e => e.mmsi)).size,
      gerechnet: this.gerechnet ? new Date(this.gerechnet).toISOString() : null,
      dauerMs: this.dauerMs,
      stunden: this.konfig.ANOMALIE_STUNDEN
    };
  }
}

module.exports = {
  Anomalie, schleifen, gebiete, saeubere, berufsschleifer,
  RUECKKEHR_M, MIN_WEG_M, MAX_DURCHMESSER_M, MIN_RUNDHEIT, MAX_RUNDE_S,
  SPRUNG_MS, ZUSAMMEN_M
};
