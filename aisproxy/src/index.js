"use strict";

const konfig = require("./konfig");
const { Zustand } = require("./zustand");
const { Strom } = require("./strom");
const { Netz } = require("./netz");
const { Speicher } = require("./speicher");
const { Register } = require("./register");
const { Server } = require("./server");

function log(msg) {
  console.log("[" + new Date().toISOString() + "] " + msg);
}

async function start() {
  log("aisproxy startet · Region " +
    [konfig.REGION.latMin, konfig.REGION.lonMin, konfig.REGION.latMax, konfig.REGION.lonMax].join(",") +
    " (" + konfig.flaeche().toFixed(1) + " sq)");

  const zustand = new Zustand({ ttlMs: konfig.TTL_MS });
  const speicher = new Speicher({ konfig, log });

  // Kaltstart abkuerzen: den letzten bekannten Stand aus der Tagestabelle
  // laden. Ohne das ist der Proxy erst nach einer halben Minute Stromsammeln
  // auskunftsfaehig - mit dem gleich darauf folgenden Netz-Abruf sind es
  // zwei Sekunden.
  const grenzeS = Math.floor((Date.now() - konfig.TTL_MS) / 1000);
  let wieder = 0;
  for (const r of speicher.letzterStand(grenzeS)) {
    zustand.melde(r.mmsi, {
      lat: r.lat / 1e6, lon: r.lon / 1e6,
      sog: r.sog == null ? null : r.sog / 10,
      cog: r.cog == null ? null : r.cog / 10
    }, r.t * 1000, "kalt");
    wieder++;
  }
  // Stammdaten dazu, sonst stehen die wiederhergestellten Schiffe namenlos da.
  for (const mmsi of zustand.schiffe.keys()) {
    const s = speicher.stammHole(mmsi);
    if (s) zustand.ergaenze(mmsi, {
      name: s.name, rufzeichen: s.rufzeichen, imo: s.imo, typ: s.typ,
      laenge: s.laenge, breite: s.breite, tiefgang: s.tiefgang
    });
  }
  if (wieder) log(wieder + " Schiff(e) aus der Historie wiederhergestellt");

  speicher.startSchreiben();

  const server = new Server({
    konfig, zustand, speicher, log,
    status: () => ({
      zeit: new Date().toISOString(),
      region: konfig.REGION,
      flaecheSq: Number(konfig.flaeche().toFixed(1)),
      schiffe: zustand.anzahl,
      rev: zustand.rev,
      zaehler: zustand.zaehler,
      strom: strom.bericht(),
      netz: netz.bericht(),
      speicher: speicher.bericht(),
      register: register.bericht(),
      clients: server.bericht(),
      speicherMB: Number((process.memoryUsage().heapUsed / 1e6).toFixed(1))
    })
  });

  // Jede Positionsaenderung wandert in den Schreibpuffer. Der Takt in
  // speicher.startSchreiben() buendelt sie - einzelne Inserts waeren bei
  // 37 Meldungen je Sekunde reine Verschwendung.
  const beiMeldung = (s, hatPosition) => {
    if (hatPosition !== false) speicher.merke(s);
  };

  const strom = new Strom({ konfig, zustand, log, beiMeldung });
  const netz = new Netz({ konfig, zustand, log, beiMeldung: (s) => beiMeldung(s, true) });
  const register = new Register({ konfig, speicher, zustand, log });

  await server.hoere();
  netz.start();
  try {
    await strom.start();
  } catch (e) {
    // Der Proxy bleibt brauchbar, auch wenn der Strom nicht zustande kommt -
    // das Netz allein traegt die Karte, nur mit groeberem Takt.
    log("Strom konnte nicht starten: " + e.message + " - das Netz laeuft weiter");
  }

  // Pflege: Verdichten, Verwerfen, und Schiffe ohne Meldung entfernen.
  setInterval(() => {
    const weg = zustand.aufraeumen();
    if (weg.length) { server.meldeEntfernt(weg); log(weg.length + " Schiff(e) ohne Meldung entfernt"); }
    speicher.pflege();
  }, konfig.PFLEGE_MS);

  // Register vorwaermen: erst kurz warten, damit der Bestand steht - sonst
  // waermt man 40 Schiffe vor und den Rest beim naechsten Lauf.
  if (konfig.REGISTER_AN) {
    setTimeout(() => register.lauf().catch(e => log("Register: " + e.message)), 90000);
    setInterval(() => register.lauf().catch(e => log("Register: " + e.message)), 6 * 3600 * 1000);
  }

  const ende = () => {
    log("beende…");
    strom.stopp(); netz.stopp(); server.stopp(); speicher.stopp();
    process.exit(0);
  };
  process.on("SIGINT", ende);
  process.on("SIGTERM", ende);
}

if (require.main === module) {
  start().catch(e => { console.error(e); process.exit(1); });
}

module.exports = { start };
