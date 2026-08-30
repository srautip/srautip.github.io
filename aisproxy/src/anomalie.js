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

// Wer als Lotsenboot gilt. AIS-Typ 50 ist die Angabe des Bootes selbst -
// aber sie stimmt nicht immer: Gemessen am Regionsbestand fuehren 49 Boote
// den Typ 50, sechs weitere Lotsenboote melden einen falschen (HAMBURG
// PILOT 3 und LOTSE 4 als 99, HAMBURG PILOT 4 und DANPILOT ALDEBARAN als
// 90, MEES (PILOTS) als 53). Ohne die Namensregel fehlt die
// ZWEITSTAERKSTE Station der Region ganz - Elbe bei Hamburg, 95 Runden in
// 24 h, ausschliesslich von solchen Booten.
//
// Ein Fehltreffer bliebe sichtbar, denn der Tooltip auf der Karte nennt die
// Bootsnamen des Gebiets. Dieselbe Regel steht als SQL in
// speicher.lotsenMmsis() - wer sie hier aendert, muss sie dort mitziehen.
const LOTSE_TYP = 50;
const LOTSE_NAME = /pilot|lotse/i;

function istLotse(typ, name) {
  if (typ != null && Number(typ) === LOTSE_TYP) return true;
  return typeof name === "string" && LOTSE_NAME.test(name);
}

// Vom STILLSTAND ausgenommen: Segelschiffe (36) und Passagierschiffe samt
// Faehren (60-69). Ausdrueckliche Vorgabe, und sie gilt nur hier - die
// Schleifenebene zeigt ohnehin nur noch Lotsenboote.
//
// Die Grundlinie zaehlt sie WEITER MIT: ausgenommen() wirkt beim Melden, nie
// in nachbarnZaehlen(). Wer beides mit demselben Sieb filtert, laesst die
// Nachbarn verschwinden - dann wirken MEHR Schiffe einsam (gemessen 136
// statt 67 Meldungen).
const AUSGENOMMEN = new Set([36, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69]);

function ausgenommen(typ) {
  return typ != null && AUSGENOMMEN.has(Number(typ));
}

// --- Stillstand ------------------------------------------------------------
//
// 77 % aller Schiffe liegen still (gemessen 2 346 von 3 021). "Stillstand"
// allein ist deshalb keine Auskunft - alles haengt am AUSSERHALB.
//
// Und dafuer braucht es keine Ankerplatzliste: "Ankerplatz" heisst "hier
// liegen viele Schiffe still", und das ist in denselben Daten messbar. Die
// Grundlinie deckt so Reeden UND Kais ab, was eine Ankerplatzliste nicht
// taete.

// Ab hier gilt ein Schiff als liegend. Derselbe Wert wie FAHRT_KN, mit dem
// die Verdichtung arbeitet - zwei verschiedene Grenzen fuer dieselbe Frage
// waeren eine Falle.
const STILL_KN = 0.5;
// So eng muss die Spur bleiben. Ein Schiff vor Anker schwojt; 300 m fasst
// das, ohne eine langsame Fahrt durchzulassen.
const STILL_M = 300;

// Zusammenhaengende Abschnitte, in denen ein Schiff steht.
function ruhephasen(punkte, mindestS) {
  const P = saeubere(punkte);
  if (P.length < 3) return [];
  const aus = [];
  let lauf = [];
  const schliesse = () => {
    if (lauf.length < 3) return;
    const dauer = lauf[lauf.length - 1].t - lauf[0].t;
    if (dauer < mindestS) return;
    let cx = 0, cy = 0;
    for (const q of lauf) { cx += q.x; cy += q.y; }
    cx /= lauf.length; cy /= lauf.length;
    let r = 0;
    for (const q of lauf) r = Math.max(r, Math.hypot(q.x - cx, q.y - cy));
    if (r > STILL_M) return;
    let la = 0, lo = 0;
    for (const q of lauf) { la += q.lat; lo += q.lon; }
    aus.push({
      von: lauf[0].t, bis: lauf[lauf.length - 1].t, dauer,
      lat: la / lauf.length, lon: lo / lauf.length, radius: Math.round(r)
    });
  };
  for (const q of P) {
    if (q.sog < STILL_KN) lauf.push(q);
    else { schliesse(); lauf = []; }
  }
  schliesse();
  return aus;
}

// Wie viele ANDERE Schiffe lagen in Reichweite still? Gitterbasiert, nicht
// paarweise: Bei rund 4 000 Phasen waeren das 16 Mio. Vergleiche je Anfrage.
function nachbarnZaehlen(phasen, umkreisM) {
  const G = umkreisM / 111320;          // Zellenkante = Umkreis, dann reichen 3x3
  const eimer = new Map();
  for (const f of phasen) {
    const k = Math.floor(f.lon / G) + ":" + Math.floor(f.lat / G);
    let v = eimer.get(k);
    if (!v) eimer.set(k, v = []);
    v.push(f);
  }
  for (const f of phasen) {
    const { kx, ky } = massstab(f.lat);
    const gx = Math.floor(f.lon / G), gy = Math.floor(f.lat / G);
    const andere = new Set();
    for (let dx = -1; dx <= 1; dx++) {
      for (let dy = -1; dy <= 1; dy++) {
        const v = eimer.get((gx + dx) + ":" + (gy + dy));
        if (!v) continue;
        for (const g of v) {
          if (g.mmsi === f.mmsi) continue;
          if (Math.hypot((g.lon - f.lon) * kx, (g.lat - f.lat) * ky) <= umkreisM) {
            andere.add(g.mmsi);
          }
        }
      }
    }
    f.nachbarn = andere.size;
  }
  return phasen;
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
  constructor(opt) {
    const { konfig, speicher, zustand, log } = opt;
    this.konfig = konfig;
    this.speicher = speicher;
    this.zustand = zustand;
    this.log = log || (() => {});
    this.ereignisse = [];            // Schleifen der Lotsenboote
    this.lotsen = 0;                 // wie viele Boote der letzte Lauf kannte
    this.lotsenGerechnet = 0;
    this.lotsenDauerMs = 0;
    this.ruhe = [];                  // alle Ruhephasen, auch die gewoehnlichen
    this.ruheFenster = null;         // { von, bis } des letzten Ruhelaufs
    this.ruheGerechnet = 0;
    this.ruheDauerMs = 0;
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

  // mitRuhe = false laesst den Ruhedurchgang aus. Er liest ein 24-h-Fenster
  // ueber ALLE Spuren der Region und kostet damit den Loewenanteil eines
  // Laufs; eine Ruhephase dauert aber mindestens sechs Stunden, er braucht
  // also einen eigenen, langsameren Takt. Der letzte Ruhestand bleibt dabei
  // stehen - ihn zu leeren hiesse, die Ebene zwischen zwei Ruhelaeufen stumm
  // abzuschalten.
  async lauf(mitRuhe) {
    const ruheJetzt = mitRuhe !== false;
    if (this.laeuft) return;         // ein zweiter Lauf brauchte doppelt so lange
    this.laeuft = true;
    try {
      await this.lotsenLauf();
      if (ruheJetzt && this.konfig.ANOMALIE_STILL_STUNDEN > 0) await this.ruheLauf();
      this.laeufe++;
      this.log("Lotsen: " + this.ereignisse.length + " Schleifen von " +
        new Set(this.ereignisse.map(e => e.mmsi)).size + " von " + this.lotsen +
        " Booten in " + this.lotsenDauerMs + " ms" +
        (ruheJetzt ? " · Ruhe: " + this.ruhe.length + " Phasen von " +
          new Set(this.ruhe.map(f => f.mmsi)).size + " Schiffen in " +
          this.ruheDauerMs + " ms" : " (ohne Ruhedurchgang)"));
    } finally {
      this.laeuft = false;
    }
  }

  // Die Lotsenboote der Region - aus dem Speicher und aus dem heissen
  // Zustand. Der heisse Zustand zaehlt mit, weil ein eben erst aufgetauchtes
  // Boot seinen Stammsatz erst im naechsten Schreibtakt bekommt.
  lotsenListe() {
    const aus = new Set();
    try { for (const m of this.speicher.lotsenMmsis()) aus.add(Number(m)); }
    catch (e) { this.log("Lotsenliste aus dem Speicher fehlgeschlagen: " + e.message); }
    if (this.zustand && this.zustand.schiffe) {
      for (const [mmsi, x] of this.zustand.schiffe) {
        if (istLotse(x.typ, x.name)) aus.add(Number(mmsi));
      }
    }
    return [...aus];
  }

  // Der Lotsendurchgang. Er fragt die Boote EINZELN ueber spur() statt die
  // ganze Region in Kacheln zu lesen: Das sind rund 55 Indexzugriffe auf
  // (mmsi, t) gegen 21 Kachelabfragen ueber alle 3600 Spuren. Deshalb
  // braucht es hier weder Kacheln noch einen Dopplungsschluessel - eine
  // Spur kommt genau einmal.
  //
  // Und deshalb wird auch NICHT gerastert: Bei 55 Spuren ist die volle
  // Aufloesung geschenkt, und sie findet gemessen MEHR Schleifen als das
  // 60-s-Raster (194 statt 147 in denselben acht Stunden).
  async lotsenLauf() {
    const t0 = Date.now();
    const bisS = Math.floor(Date.now() / 1000);
    const vonS = bisS - this.konfig.ANOMALIE_LOTSE_STUNDEN * 3600;
    const mmsis = this.lotsenListe();
    const aus = [];
    let i = 0;
    for (const mmsi of mmsis) {
      // Alle 20 Boote die Schleife freigeben. Der Durchgang ist kurz, aber
      // der AIS-Strom soll auch dann nicht warten, wenn die Lotsenliste
      // einmal laenger wird als heute.
      if (++i % 20 === 0) await new Promise(f => setImmediate(f));
      let punkte;
      try { punkte = this.speicher.spur(mmsi, vonS, bisS); }
      catch (e) { this.log("Lotse " + mmsi + " uebersprungen: " + e.message); continue; }
      for (const e of schleifen(punkte)) {
        e.mmsi = mmsi;
        aus.push(e);
      }
    }
    this.ereignisse = aus;
    this.lotsen = mmsis.length;
    this.lotsenGerechnet = Date.now();
    this.lotsenDauerMs = this.lotsenGerechnet - t0;
  }

  // Der Ruhedurchgang - unveraendert in Kacheln, denn er braucht ALLE
  // liegenden Schiffe: Die Grundlinie ("hier liegen viele still") entsteht
  // erst aus ihnen.
  async ruheLauf() {
    const t0 = Date.now();
    const stillBis = Math.floor(Date.now() / 1000);
    const stillVon = stillBis - this.konfig.ANOMALIE_STILL_STUNDEN * 3600;
    const gesehenRuhe = new Set();
    const ruhe = [];
    let datenVon = Infinity, datenBis = -Infinity;
    for (const kachel of this.kacheln()) {
      // Zwischen den Kacheln die Schleife freigeben. setImmediate und nicht
      // await auf nichts: Nur so kommen die wartenden Ein-/Ausgaben - der
      // AIS-Strom - vor der naechsten Kachel dran.
      await new Promise(f => setImmediate(f));
      let liegend;
      try {
        liegend = this.speicher.spuren(kachel, stillVon, stillBis,
          this.konfig.ANOMALIE_STILL_SCHRITT_S);
      } catch (e) { this.log("Ruhekachel uebersprungen: " + e.message); continue; }
      for (const s2 of liegend) {
        // Die beobachtete Spanne kommt aus ALLEN Spuren, nicht nur aus den
        // ruhenden: Sonst waere bei einem einzigen liegenden Schiff dessen
        // eigene Phase die ganze "Beobachtung", und es gaelte immer als
        // Moebel.
        for (const p of s2.punkte) {
          if (p[0] < datenVon) datenVon = p[0];
          if (p[0] > datenBis) datenBis = p[0];
        }
        for (const f of ruhephasen(s2.punkte, this.konfig.ANOMALIE_STILL_GRUND_S)) {
          const k = s2.mmsi + ":" + f.von;
          if (gesehenRuhe.has(k)) continue;
          gesehenRuhe.add(k);
          f.mmsi = s2.mmsi;
          ruhe.push(f);
        }
      }
    }
    // Die Nachbarschaft ueber ALLE Phasen, nicht nur die meldenswerten.
    // Gerade die Festgemachten und die Dauerlieger definieren, wo Liegen
    // normal ist; sie mit demselben Sieb wegzufiltern liess gemessen MEHR
    // Schiffe einsam wirken (136 statt 67 Meldungen).
    nachbarnZaehlen(ruhe, this.konfig.ANOMALIE_STILL_UMKREIS_M);

    // Moebel sind Kai, Plattform, Hubinsel - und die Frage dahinter ist
    // genau eine: HABEN WIR DAS SCHIFF ANKOMMEN SEHEN? Wer schon dalag,
    // als die Beobachtung begann, hat nirgends angehalten.
    //
    // Deshalb zaehlt NUR DER ANFANG. Zuerst stand hier zusaetzlich "und
    // am Ende noch da"; an der laufenden Anlage scheiterte das in 24 von
    // 92 Faellen an Meldeluecken - ein festgemachtes Kleinfahrzeug sendet
    // unregelmaessig, sein letzter Punkt lag 24 Minuten vor dem juengsten
    // Punkt der Region, und schon galt es nicht mehr als Moebel. Ob es
    // inzwischen weg ist, aendert an der Frage aber nichts.
    //
    // Bezug ist der Rand der WIRKLICH VORHANDENEN Daten, nicht das
    // angefragte Fenster. Ein Proxy, der erst seit drei Stunden laeuft,
    // haette nach dem nominellen Fenster keinen einzigen Dauerlieger - und
    // meldete dann jedes festgemachte Schiff im Hafen. Aufgefallen ist das
    // an einer Probe mit zwei Stunden alten Daten: Aus 39 Meldungen wurden
    // 107, und die laengsten hiessen alle "22,0 h".
    //
    // Und der Vermerk urteilt nur, wenn die Beobachtung lang genug ist:
    // "lag die ganze Zeit da" heisst bei drei Stunden nichts. Darunter ist
    // niemand Moebel - im Zweifel lieber melden als still verschweigen.
    const spanne = datenBis - datenVon;
    const urteilsfaehig = spanne >= 2 * this.konfig.ANOMALIE_STILL_MIN_S;
    // ?? und nicht ||: Eine 0 waere eine gueltige Angabe ("keine
    // Toleranz"), und || machte daraus stillschweigend 3600. Fehlt der
    // Schluessel dagegen ganz, ergaebe der Vergleich NaN und der Vermerk
    // fiele lautlos aus - beim ersten Lauf mit einer aelteren
    // Testkonfiguration ist genau das passiert.
    const rand = this.konfig.ANOMALIE_STILL_RAND_S ?? 3600;

    // Der Rand allein reicht nicht, und das hat eine Messung gezeigt:
    // HMM ALGECIRAS (400 m) lag 16,4 h vor Anker und begann 18 Sekunden
    // nach dem Datenrand - nach der blossen Randregel also "Moebel",
    // obwohl es der interessanteste Fall im ganzen Datensatz war. Wer
    // laenger ankert als das Fenster, wird sonst ununterscheidbar von
    // einem Kai.
    //
    // Die Historie kann die Frage beantworten. Die Verdichtung loescht
    // nach ROH_STUNDEN zwar die LIEGENDEN Punkte, behaelt aber die
    // FAHRENDEN - wer angekommen ist, hat davor eine Anfahrt, ein Kai
    // oder eine Plattform hat keine. Gefragt wird nur fuer die Kandidaten
    // am Rand und nur einmal je Schiff; pos_* traegt einen Index auf
    // (mmsi, t), das sind also ein paar Dutzend Indexzugriffe.
    const fahrt = Math.round(this.konfig.FAHRT_KN * 10);
    const vorlauf = this.konfig.ANOMALIE_STILL_VORLAUF_S ?? 24 * 3600;
    const angekommen = new Map();
    for (const f of ruhe) {
      if (!(urteilsfaehig && f.von <= datenVon + rand)) { f.dauerlieger = false; continue; }
      if (!angekommen.has(f.mmsi)) {
        let fuhr = false;
        try {
          const vorher = this.speicher.spur(f.mmsi, datenVon - vorlauf, datenVon);
          fuhr = vorher.some(p => p[3] != null && p[3] >= fahrt);
        } catch (e) { fuhr = false; }
        angekommen.set(f.mmsi, fuhr);
      }
      f.angekommen = angekommen.get(f.mmsi);
      f.dauerlieger = !f.angekommen;
    }
    this.ruhe = ruhe;
    this.ruheFenster = { von: stillVon, bis: stillBis, datenVon, datenBis };
    this.ruheGerechnet = Date.now();
    this.ruheDauerMs = this.ruheGerechnet - t0;
  }

  // Die Auskunft fuer die Karte. Verdichtet wird bei JEDER Anfrage neu, nicht
  // im Lauf: Das kostet Millisekunden und erlaubt dafuer ein freies
  // Zeitfenster, ohne neu erkennen zu muessen.
  //
  // Gefiltert wird hier NICHT mehr nach Schiffstyp - der Lauf sammelt schon
  // nur Lotsenboote ein. Was hier faellt, ist die Schwelle: Ein Gebiet aus
  // einer einzigen Runde ist ein Boot auf dem Weg, keine Station. Gemessen
  // an 24 h echter Daten sind das 18 von 58 Gebieten mit genau einer Runde;
  // ab drei Runden bleiben 31, und alle acht bekannten Reviere der Region
  // haben neun Runden und mehr - die Schwelle trifft sie also nicht knapp.
  lotsenGebiete(box, stunden) {
    const grenze = Math.floor(Date.now() / 1000) - stunden * 3600;
    const drin = this.ereignisse.filter(e =>
      e.bis >= grenze &&
      e.lat >= box.latMin && e.lat <= box.latMax &&
      e.lon >= box.lonMin && e.lon <= box.lonMax);
    // ?? und nicht ||: Eine 0 ist eine gueltige Angabe ("alle zeigen").
    const min = this.konfig.ANOMALIE_LOTSE_MIN ?? 3;
    const gb = gebiete(drin, this.konfig.ANOMALIE_ZUSAMMEN_M)
      .filter(g => g.schleifen >= min);
    gb.sort((a, b) => (b.schleifen - a.schleifen) || (b.schiffe.length - a.schiffe.length));
    return gb;
  }

  // Der Typ eines Schiffs - aus dem heissen Zustand, sonst aus dem Speicher.
  typVon(mmsi) {
    const s = this.zustand && this.zustand.schiffe.get(mmsi);
    if (s && s.typ != null) return s.typ;
    return (this.speicher.stammHole(mmsi) || {}).typ;
  }

  // Stillstand ausserhalb der gewohnten Liegeplaetze.
  //
  // Gemeldet wird eine Ruhephase, wenn sie lang genug ist, das Schiff nicht
  // ausgenommen ist, sie nicht das ganze Fenster fuellt - und wenn im
  // Umkreis WENIGER als ANOMALIE_STILL_MIN andere Schiffe lagen. Die
  // Nachbarzahl stammt aus dem Lauf und zaehlt ALLE Liegenden.
  stillstand(box, stunden) {
    if (!this.ruhe.length) return [];
    const grenze = Math.floor(Date.now() / 1000) - stunden * 3600;
    const aus = [];
    for (const f of this.ruhe) {
      if (f.bis < grenze) continue;
      if (f.dauer < this.konfig.ANOMALIE_STILL_MIN_S) continue;
      if (f.dauerlieger) continue;
      if (f.nachbarn >= this.konfig.ANOMALIE_STILL_MIN) continue;
      if (f.lat < box.latMin || f.lat > box.latMax ||
          f.lon < box.lonMin || f.lon > box.lonMax) continue;
      if (ausgenommen(this.typVon(f.mmsi))) continue;
      aus.push({
        mmsi: f.mmsi,
        lat: Number(f.lat.toFixed(5)), lon: Number(f.lon.toFixed(5)),
        von: f.von, bis: f.bis, dauer: f.dauer,
        radius: f.radius, nachbarn: f.nachbarn
      });
    }
    aus.sort((a, b) => b.dauer - a.dauer);
    return aus;
  }

  bericht() {
    return {
      laeufe: this.laeufe,
      // Der Lotsendurchgang und der Ruhedurchgang laufen in verschiedenen
      // Takten. EINE gemeinsame Zahl liesse den aelteren aktueller aussehen,
      // als er ist - genau die Sorte Bericht, die zum Suchen an einer
      // funktionierenden Anlage einlaedt.
      lotsen: this.lotsen,
      schleifen: this.ereignisse.length,
      schiffe: new Set(this.ereignisse.map(e => e.mmsi)).size,
      lotsenGerechnet: this.lotsenGerechnet
        ? new Date(this.lotsenGerechnet).toISOString() : null,
      lotsenDauerMs: this.lotsenDauerMs,
      lotsenStunden: this.konfig.ANOMALIE_LOTSE_STUNDEN,
      ruhephasen: this.ruhe.length,
      ruheSchiffe: new Set(this.ruhe.map(f => f.mmsi)).size,
      ruheGerechnet: this.ruheGerechnet
        ? new Date(this.ruheGerechnet).toISOString() : null,
      ruheDauerMs: this.ruheDauerMs,
      stillStunden: this.konfig.ANOMALIE_STILL_STUNDEN
    };
  }
}

module.exports = {
  Anomalie, schleifen, gebiete, saeubere, istLotse, ausgenommen,
  ruhephasen, nachbarnZaehlen, STILL_KN, STILL_M,
  RUECKKEHR_M, MIN_WEG_M, MAX_DURCHMESSER_M, MIN_RUNDHEIT, MAX_RUNDE_S,
  SPRUNG_MS, ZUSAMMEN_M
};
