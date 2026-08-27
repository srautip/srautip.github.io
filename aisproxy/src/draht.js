"use strict";

// Das Drahtformat zum Client. Feste 20 Byte je Schiff.
//
// Warum binaer und nicht JSON: Gemessen kostet dasselbe Schiff im GeoJSON des
// Upstreams rund 310 Byte. 20 Byte sind Faktor 15 - und das entscheidet
// darueber, ob ein Zwei-Sekunden-Takt auf dem Handy vertretbar ist oder nicht.
//
//   u32 mmsi │ i32 lat │ i32 lon │ u16 sog │ u16 cog │ u16 hdg │ u8 status │ u8 flags
//
// lat/lon als Grad x 1e6: 11 cm Aufloesung, und +-180e6 passt bequem in i32.
// Namen und Stammdaten reisen NICHT hier mit - die aendern sich fast nie und
// gehen einmal je MMSI als JSON hinueber.

const SATZ = 20;

// Sentinels auf der Leitung. null wird zu diesen Werten, und der Client macht
// daraus wieder null. Ein eigenes Anwesenheitsbit je Feld waere sparsamer,
// aber schwerer richtig zu machen - und 20 Byte sind ohnehin klein genug.
const U16_LEER = 0xffff;
const U8_LEER = 0xff;

const FLAG_SEEZEICHEN = 1;
const FLAG_LANDSTATION = 2;
const FLAG_KLASSE_B = 4;

function schreibe(buf, off, s) {
  buf.writeUInt32LE(s.mmsi >>> 0, off);
  buf.writeInt32LE(Math.round(s.lat * 1e6), off + 4);
  buf.writeInt32LE(Math.round(s.lon * 1e6), off + 8);
  buf.writeUInt16LE(s.sog == null ? U16_LEER : Math.min(0xfffe, Math.round(s.sog * 10)), off + 12);
  buf.writeUInt16LE(s.cog == null ? U16_LEER : Math.min(0xfffe, Math.round(s.cog * 10)), off + 14);
  buf.writeUInt16LE(s.hdg == null ? U16_LEER : Math.min(0xfffe, Math.round(s.hdg)), off + 16);
  buf.writeUInt8(s.status == null ? U8_LEER : (s.status & 0xff), off + 18);
  buf.writeUInt8((s.flags || 0) & 0xff, off + 19);
}

function lies(buf, off) {
  const sog = buf.readUInt16LE(off + 12);
  const cog = buf.readUInt16LE(off + 14);
  const hdg = buf.readUInt16LE(off + 16);
  const st = buf.readUInt8(off + 18);
  return {
    mmsi: buf.readUInt32LE(off),
    lat: buf.readInt32LE(off + 4) / 1e6,
    lon: buf.readInt32LE(off + 8) / 1e6,
    sog: sog === U16_LEER ? null : sog / 10,
    cog: cog === U16_LEER ? null : cog / 10,
    hdg: hdg === U16_LEER ? null : hdg,
    status: st === U8_LEER ? null : st,
    flags: buf.readUInt8(off + 19)
  };
}

// Ein Rahmen: [0x01][rev u32][anzahl u16][saetze...]
// Die rev im Kopf ist der Stand, den der Client danach hat - er schickt sie
// beim naechsten Mal zurueck, und der Server weiss ohne Zustandshaltung je
// Client, was fehlt.
const RAHMEN_KOPF = 7;
const TYP_DELTA = 0x01;

function packe(rev, schiffe) {
  const buf = Buffer.allocUnsafe(RAHMEN_KOPF + schiffe.length * SATZ);
  buf.writeUInt8(TYP_DELTA, 0);
  buf.writeUInt32LE(rev >>> 0, 1);
  buf.writeUInt16LE(schiffe.length, 5);
  for (let i = 0; i < schiffe.length; i++) schreibe(buf, RAHMEN_KOPF + i * SATZ, schiffe[i]);
  return buf;
}

function entpacke(buf) {
  if (buf.length < RAHMEN_KOPF) throw new Error("Rahmen zu kurz");
  if (buf.readUInt8(0) !== TYP_DELTA) throw new Error("unbekannter Rahmentyp");
  const rev = buf.readUInt32LE(1);
  const n = buf.readUInt16LE(5);
  if (buf.length !== RAHMEN_KOPF + n * SATZ) {
    throw new Error("Rahmenlaenge passt nicht zur Anzahl (" + buf.length + " statt " +
      (RAHMEN_KOPF + n * SATZ) + ")");
  }
  const schiffe = [];
  for (let i = 0; i < n; i++) schiffe.push(lies(buf, RAHMEN_KOPF + i * SATZ));
  return { rev, schiffe };
}

module.exports = {
  SATZ, RAHMEN_KOPF, TYP_DELTA, U16_LEER, U8_LEER,
  FLAG_SEEZEICHEN, FLAG_LANDSTATION, FLAG_KLASSE_B,
  packe, entpacke, schreibe, lies
};
