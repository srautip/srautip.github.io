"use strict";

// AIS-Fachlogik, die Proxy und Client teilen MUESSEN.
//
// Spiegel von aisstream/index.html - normalizeSog/normalizeCog/
// normalizeHeading dort stehen bei Z. 2556 ff. Wenn sich dort etwas aendert,
// aendert es sich hier mit, sonst hat dieselbe Meldung zwei Wahrheiten: eine
// im Browser und eine in der Datenbank.
//
// EHRLICHE EINSCHRAENKUNG zum Entwurf: Der Anteil geteilter Fachlogik ist
// kleiner, als die Node-Entscheidung im Entwurf behauptet hat. Typtabellen,
// MMSI/MID-Dekodierung und ETA-Beschriftung braucht der Proxy gar nicht - er
// speichert Rohwerte und laesst den Client beschriften. Geteilt werden muss
// nur, was den GESPEICHERTEN Wert veraendert, und das sind die Sentinels.
// Genau die stehen hier.

// 1023 heisst "nicht verfuegbar", 1022 "102,2 Knoten oder mehr". Beides ist
// keine Geschwindigkeit, mit der man rechnen darf.
function sogNormal(sog) {
  if (sog == null) return null;
  const n = Number(sog);
  if (!Number.isFinite(n)) return null;
  return n >= 102.3 ? null : n;
}

// 3600 (also 360,0 Grad) ist der Sentinel "nicht verfuegbar".
function cogNormal(cog) {
  if (cog == null) return null;
  const n = Number(cog);
  if (!Number.isFinite(n)) return null;
  return n >= 360 ? null : n;
}

// 511 heisst "nicht verfuegbar". Ein Kurs ueber Grund kann 359,9 sein, eine
// Kopfrichtung nur ganzzahlig 0..359.
function headingNormal(hdg) {
  if (hdg == null) return null;
  const n = Number(hdg);
  if (!Number.isFinite(n)) return null;
  return n === 511 || n < 0 || n > 359 ? null : n;
}

// Dimension {A:0,B:0,C:0,D:0} ist "keine Angabe" - aber das Objekt selbst ist
// truthy, und genau daran ist der Client schon einmal haengengeblieben.
// A+B ist die Laenge, C+D die Breite, beides in Metern.
function masse(dim) {
  if (!dim) return null;
  const a = Number(dim.A) || 0, b = Number(dim.B) || 0;
  const c = Number(dim.C) || 0, d = Number(dim.D) || 0;
  const laenge = a + b, breite = c + d;
  if (!laenge || !breite) return null;
  return { laenge, breite, a, b, c, d };
}

// AIS packt die ETA in einen Integer: month<<16 | day<<11 | hour<<6 | minute.
// Der Proxy speichert diesen Rohwert und rechnet ihn nicht um - die
// Beschriftung ist Sache des Clients. Geprueft wird nur der Sentinel, damit
// kein Unsinn in die Datenbank wandert.
const ETA_LEER = 1596; // Monat 0, Tag 0, Stunde 24, Minute 60
function etaRoh(wert) {
  if (wert == null) return null;
  const n = Number(wert);
  if (!Number.isFinite(n) || n === 0 || n === ETA_LEER) return null;
  return n;
}

// Text aus AIS ist mit "@" aufgefuellt und trailing-space-behaftet.
function textSauber(s) {
  if (s == null) return null;
  const t = String(s).replace(/@+$/g, "").replace(/\s+/g, " ").trim();
  return t.length ? t : null;
}

// Eine Position ist nur brauchbar, wenn sie im gueltigen Bereich liegt.
// 91/181 sind die AIS-Sentinels fuer "unbekannt".
function positionGueltig(lat, lon) {
  return Number.isFinite(lat) && Number.isFinite(lon) &&
    lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180 &&
    !(lat === 91 || lon === 181);
}

function inBox(box, lat, lon) {
  return lat >= box.latMin && lat <= box.latMax &&
         lon >= box.lonMin && lon <= box.lonMax;
}

// Gitterzelle fuer bbox-Abfragen auf der kalten Ablage. 0,25 Grad, als eine
// Zahl kodiert, damit sie in eine INTEGER-Spalte passt und indizierbar ist.
const GITTER = 0.25;
function zelle(lat, lon) {
  const zl = Math.floor((lat + 90) / GITTER);
  const zo = Math.floor((lon + 180) / GITTER);
  return zl * 2000 + zo;
}

// Alle Zellen, die eine Box beruehrt - fuer die IN-Liste einer SQL-Abfrage.
function zellen(box) {
  const out = [];
  const l0 = Math.floor((box.latMin + 90) / GITTER);
  const l1 = Math.floor((box.latMax + 90) / GITTER);
  const o0 = Math.floor((box.lonMin + 180) / GITTER);
  const o1 = Math.floor((box.lonMax + 180) / GITTER);
  for (let l = l0; l <= l1; l++) for (let o = o0; o <= o1; o++) out.push(l * 2000 + o);
  return out;
}

module.exports = {
  sogNormal, cogNormal, headingNormal, masse, etaRoh, textSauber,
  positionGueltig, inBox, zelle, zellen, GITTER, ETA_LEER
};
