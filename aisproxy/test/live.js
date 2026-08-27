"use strict";

// Der Lauf gegen den echten Upstream. Kein Teil von `npm test` - er dauert
// Minuten und braucht Netz. Er misst genau das nach, was der Entwurf
// versprochen hat:
//
//   1. Handelt der Server permessage-deflate aus?
//   2. Bleibt die Rate unter dem 50er-Limit?
//   3. Kommen wirklich rund 2900 Schiffe zusammen?
//   4. Kostet ein Client tatsaechlich nur rund 10 KB/min?
//   5. Landen Punkte in der Historie, und liefert replay sie zurueck?
//
//   node test/live.test.js [sekunden]

const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const WebSocket = require("ws");
const draht = require("../src/draht");

const DAUER_S = Number(process.argv[2]) || 120;
const dir = fs.mkdtempSync(path.join(os.tmpdir(), "aisproxy-live-"));

process.env.AIS_DB = path.join(dir, "live.db");
process.env.AIS_FOTO_VERZEICHNIS = path.join(dir, "fotos");
process.env.PORT = "8099";
// Das Register bleibt aus: Es wuerde die Messung mit Fremdverkehr mischen
// und die freien Dienste fuer einen Testlauf belasten.
process.env.AIS_REGISTER = "0";
process.env.AIS_SCHREIB_MS = "2000";

let fehler = 0;
const ok = (b, t) => { if (!b) fehler++; console.log((b ? "  OK   " : "  FEHL ") + t); };

(async () => {
  console.log("Starte Proxy gegen den echten Upstream, messe " + DAUER_S + " s\n");
  const { start } = require("../src/index");
  await start();

  const basis = "http://127.0.0.1:8099";
  // Erst dem Netz-Abruf Zeit geben - der ist die Grundlage fuer alles.
  await new Promise(r => setTimeout(r, 12000));

  const nachStart = await (await fetch(basis + "/v1/status")).json();
  console.log("Nach 12 s: " + nachStart.schiffe + " Schiffe, Netz-Abruf " +
    (nachStart.netz.letzteBytes / 1024).toFixed(0) + " KB auf der Leitung (" +
    (nachStart.netz.letzteBytesEntpackt / 1024).toFixed(0) + " KB entpackt, " +
    nachStart.netz.kodierung + ") in " + nachStart.netz.letzteDauerMs + " ms\n");
  ok(nachStart.schiffe > 1500,
    "1) der Netz-Abruf fuellt den Bestand sofort (" + nachStart.schiffe + " Schiffe)");
  ok(nachStart.netz.kodierung === "gzip",
    "2) der Abruf kommt komprimiert (" + nachStart.netz.kodierung + ")");
  ok(nachStart.netz.letzteBytes > 0 && nachStart.netz.letzteBytes < 300000,
    "3) und bleibt damit klein: " + (nachStart.netz.letzteBytes / 1024).toFixed(0) +
    " KB auf der Leitung statt " + (nachStart.netz.letzteBytesEntpackt / 1024).toFixed(0) +
    " KB entpackt = " + nachStart.netz.hochgerechnetGBproTag + " GB/Tag");

  // Ein Client wie der echte: abonniert einen Kartenausschnitt und zaehlt,
  // was wirklich ueber die Leitung geht.
  const AUSSCHNITT = "53.4,6.5,54.6,9.0";
  const ws = new WebSocket("ws://127.0.0.1:8099/v1/live?bbox=" + AUSSCHNITT + "&takt=2000");
  let bytesErst = 0, bytesLaufend = 0, saetze = 0, stammBytes = 0, erstesFertig = false;
  ws.on("message", (d, binaer) => {
    const n = Buffer.byteLength(d);
    if (!erstesFertig) bytesErst += n; else bytesLaufend += n;
    if (binaer) { try { saetze += draht.entpacke(d).schiffe.length; } catch (e) {} }
    else stammBytes += n;
  });
  await new Promise(r => ws.on("open", r));
  await new Promise(r => setTimeout(r, 4000));
  erstesFertig = true;
  const erstbild = bytesErst;
  const t0 = Date.now();

  await new Promise(r => setTimeout(r, DAUER_S * 1000));
  const dauerMin = (Date.now() - t0) / 60000;

  const s = await (await fetch(basis + "/v1/status")).json();
  console.log("\n--- Upstream ---");
  console.log("  Strom: " + s.strom.nachrichten + " Nachrichten, " +
    s.strom.rate.toFixed(1) + " msg/s (Spitze " + s.strom.rateSpitze +
    ", Limit " + s.strom.rateLimit + ")");
  console.log("  Strom-Volumen: " + (s.strom.bytesProSekunde / 1024).toFixed(1) +
    " KB/s = " + s.strom.hochgerechnetGBproTag + " GB/Tag");
  console.log("  Netz: " + s.netz.abrufe + " Abrufe, " + (s.netz.bytes / 1024).toFixed(0) + " KB");
  console.log("  Schiffe: " + s.schiffe + ", rev " + s.rev);
  console.log("  Speicher: " + s.speicher.geschrieben + " Punkte, " +
    s.speicher.tage + " Tagestabelle(n), Heap " + s.speicherMB + " MB");
  console.log("\n--- Was ein Client kostet ---");
  console.log("  Erstbild: " + (erstbild / 1024).toFixed(1) + " KB");
  console.log("  laufend:  " + (bytesLaufend / 1024 / dauerMin).toFixed(1) + " KB/min" +
    "  (" + saetze + " Saetze, davon " + (stammBytes / 1024).toFixed(1) + " KB Stammdaten)");

  ok(s.strom.verbunden, "4) der Strom steht");
  ok(s.strom.rateSpitze < s.strom.rateLimit,
    "5) die Rate bleibt unter dem Limit (Spitze " + s.strom.rateSpitze + " von " + s.strom.rateLimit + ")");
  // Der Entwurf rechnet mit 0,62 GB/Tag durch permessage-deflate. Ohne
  // Komprimierung waeren es 1,80 - der Unterschied ist nicht zu uebersehen.
  ok(s.strom.hochgerechnetGBproTag < 1.2,
    "6) permessage-deflate greift (" + s.strom.hochgerechnetGBproTag +
    " GB/Tag, ohne Komprimierung waeren es rund 1,8)");
  ok(s.schiffe > 2000, "7) der Bestand ist beisammen (" + s.schiffe + " Schiffe)");
  ok(s.speicher.geschrieben > 0, "8) die Historie fuellt sich (" + s.speicher.geschrieben + " Punkte)");
  const proMinute = bytesLaufend / 1024 / dauerMin;
  ok(proMinute < 60, "9) der laufende Client-Verkehr bleibt klein (" +
    proMinute.toFixed(1) + " KB/min, Entwurf rechnet mit rund 10)");

  // Replay auf dem, was gerade aufgezeichnet wurde.
  const jetzt = Math.floor(Date.now() / 1000);
  const rp = await (await fetch(basis + "/v1/replay?bbox=" + AUSSCHNITT +
    "&von=" + (jetzt - DAUER_S - 60) + "&bis=" + jetzt + "&schritt=0")).json();
  console.log("\n--- Historie ---");
  console.log("  replay: " + rp.anzahl + " Spuren ueber " + DAUER_S + " s");
  ok(rp.anzahl > 0, "10) replay liefert Spuren aus der eigenen Aufzeichnung");
  if (rp.anzahl) {
    const laengste = rp.spuren.reduce((a, b) => a.punkte.length > b.punkte.length ? a : b);
    console.log("  laengste Spur: " + laengste.punkte.length + " Punkte (MMSI " + laengste.mmsi + ")");
    ok(laengste.punkte.length >= 2, "11) und die Spuren haben mehr als einen Punkt");
  }

  // Ein Schiff nachschlagen
  const einer = (await (await fetch(basis + "/v1/snapshot?bbox=" + AUSSCHNITT)).json()).schiffe[0];
  if (einer) {
    const sh = await (await fetch(basis + "/v1/ship/" + einer.mmsi)).json();
    console.log("\n  Beispielschiff " + sh.mmsi + ": " + (sh.name || "(ohne Namen)") +
      ", Register " + sh.register);
    ok(sh.mmsi === einer.mmsi, "12) /v1/ship antwortet");
  }

  ws.close();
  console.log("\n" + (fehler ? fehler + " Pruefung(en) fehlgeschlagen" : "alle Pruefungen bestanden"));
  process.exit(fehler ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
