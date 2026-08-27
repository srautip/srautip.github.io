"use strict";
const test = require("node:test");
const assert = require("node:assert");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const WebSocket = require("ws");
const { Server } = require("../src/server");
const { Zustand } = require("../src/zustand");
const { Speicher } = require("../src/speicher");
const draht = require("../src/draht");

const REGION = { latMin: 53, lonMin: 6, latMax: 56, lonMax: 13 };

function aufbau(ueber) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "aisproxy-s-"));
  const konfig = Object.assign({
    REGION, PORT: 0, ZUGANG: "", TAKT_MS: 60, TAKT_MIN_MS: 20, TAKT_MAX_MS: 60000,
    DB_DATEI: path.join(dir, "t.db"), FOTO_VERZEICHNIS: path.join(dir, "fotos"),
    ROH_STUNDEN: 24, HISTORIE_TAGE: 7, VERDICHTUNG_S: 60, FAHRT_KN: 0.5,
    SCHREIB_MS: 100000, REGISTER_TREFFER_MS: 1e12, REGISTER_FEHL_MS: 1e12
  }, ueber || {});
  const zustand = new Zustand({ ttlMs: 60000 });
  const speicher = new Speicher({ konfig, log: () => {} });
  const server = new Server({ konfig, zustand, speicher, log: () => {},
                              status: () => ({ schiffe: zustand.anzahl }) });
  return { konfig, zustand, speicher, server, dir };
}

async function starte(s) {
  await new Promise(r => s.server.http.listen(0, r));
  s.port = s.server.http.address().port;
  s.basis = "http://127.0.0.1:" + s.port;
  return s;
}

function abbau(s) {
  s.server.stopp();
  s.speicher.stopp();
}

function warte(ms) { return new Promise(r => setTimeout(r, ms)); }

test("snapshot liefert nur den Ausschnitt und die aktuelle rev", async () => {
  const s = await starte(aufbau());
  try {
    s.zustand.melde(1, { lat: 54, lon: 8, sog: 9 }, Date.now(), "strom");
    s.zustand.melde(2, { lat: 60, lon: 8 }, Date.now(), "strom");   // ausserhalb
    const r = await (await fetch(s.basis + "/v1/snapshot?bbox=53,6,56,13")).json();
    assert.strictEqual(r.anzahl, 1);
    assert.strictEqual(r.schiffe[0].mmsi, 1);
    assert.strictEqual(r.rev, s.zustand.rev);
    // Ohne diesen Kopf kommt im Browser keine einzige Antwort an - der Client
    // liegt auf github.io, der Proxy woanders.
    const kopf = (await fetch(s.basis + "/v1/snapshot")).headers.get("access-control-allow-origin");
    assert.strictEqual(kopf, "*");
  } finally { abbau(s); }
});

test("unlesbare bbox wird abgewiesen, nicht stillschweigend ignoriert", async () => {
  const s = await starte(aufbau());
  try {
    const res = await fetch(s.basis + "/v1/snapshot?bbox=kaputt");
    assert.strictEqual(res.status, 400);
  } finally { abbau(s); }
});

test("live: Erstbild, dann nur noch Deltas", async () => {
  const s = await starte(aufbau());
  try {
    for (let i = 1; i <= 3; i++) {
      s.zustand.melde(i, { lat: 54 + i / 100, lon: 8, sog: 9, name: "S" + i }, Date.now(), "strom");
    }
    const ws = new WebSocket("ws://127.0.0.1:" + s.port + "/v1/live?bbox=53,6,56,13&takt=40");
    const stamm = [], rahmen = [], weg = [];
    ws.on("message", (d, binaer) => {
      if (binaer) rahmen.push(draht.entpacke(d));
      else { const o = JSON.parse(d); (o.typ === "weg" ? weg : stamm).push(o); }
    });
    await new Promise(r => ws.on("open", r));
    await warte(200);

    assert.strictEqual(stamm.length, 1, "Stammdaten kommen einmal");
    assert.strictEqual(stamm[0].schiffe.length, 3);
    assert.ok(rahmen.length >= 1);
    assert.strictEqual(rahmen[0].schiffe.length, 3, "das Erstbild traegt alle drei");

    const bisher = rahmen.length;
    const stammBisher = stamm.length;
    // Nur eines bewegt sich.
    s.zustand.melde(2, { lat: 54.5, lon: 8, sog: 9 }, Date.now() + 1000, "strom");
    await warte(150);
    const neue = rahmen.slice(bisher);
    assert.ok(neue.length >= 1, "es kam ein Delta");
    assert.strictEqual(neue[0].schiffe.length, 1, "und zwar nur das bewegte Schiff");
    assert.strictEqual(neue[0].schiffe[0].mmsi, 2);
    assert.strictEqual(stamm.length, stammBisher,
      "Stammdaten reisen NICHT bei jedem Takt mit - sonst waere das Binaerformat umsonst");

    // Faehrt es aus dem Ausschnitt, muss der Client es loeschen duerfen.
    s.zustand.melde(2, { lat: 61, lon: 8 }, Date.now() + 2000, "strom");
    await warte(150);
    assert.ok(weg.length >= 1, "Verlassen wird gemeldet");
    assert.ok(weg[weg.length - 1].mmsi.includes(2));
    ws.close();
  } finally { abbau(s); }
});

test("live: ein ruhiger Takt sendet gar nichts", async () => {
  // Das ist die eigentliche Sparleistung: Wenn sich nichts bewegt, geht auch
  // nichts ueber die Leitung.
  const s = await starte(aufbau());
  try {
    s.zustand.melde(1, { lat: 54, lon: 8 }, Date.now(), "strom");
    const ws = new WebSocket("ws://127.0.0.1:" + s.port + "/v1/live?takt=30");
    let zaehler = 0;
    ws.on("message", () => zaehler++);
    await new Promise(r => ws.on("open", r));
    await warte(150);
    const nachErstbild = zaehler;
    await warte(300);   // rund zehn Takte ohne jede Aenderung
    assert.strictEqual(zaehler, nachErstbild, "kein einziges Byte im Leerlauf");
    ws.close();
  } finally { abbau(s); }
});

test("live: der Client darf den Ausschnitt nachziehen", async () => {
  const s = await starte(aufbau());
  try {
    s.zustand.melde(1, { lat: 54, lon: 8 }, Date.now(), "strom");
    s.zustand.melde(2, { lat: 55.5, lon: 12 }, Date.now(), "strom");
    const ws = new WebSocket("ws://127.0.0.1:" + s.port + "/v1/live?bbox=53,6,54.5,9&takt=40");
    const rahmen = [];
    ws.on("message", (d, binaer) => { if (binaer) rahmen.push(draht.entpacke(d)); });
    await new Promise(r => ws.on("open", r));
    await warte(150);
    assert.strictEqual(rahmen[0].schiffe.length, 1, "erst nur das eine");

    ws.send(JSON.stringify({ bbox: "53,6,56,13" }));
    await warte(150);
    const letzter = rahmen[rahmen.length - 1];
    assert.strictEqual(letzter.schiffe.length, 2, "nach dem Schwenk beide");
    ws.close();
  } finally { abbau(s); }
});

test("replay liefert Stuetzpunktfolgen aus der Historie", async () => {
  const s = await starte(aufbau());
  try {
    const t = Math.floor(Date.now() / 1000);
    for (let i = 0; i < 10; i++) {
      s.speicher.merke({ mmsi: 42, seen: (t - 600 + i * 60) * 1000,
                         lat: 54 + i / 200, lon: 8, sog: 9, cog: 90 });
    }
    s.speicher.schreibe();
    const r = await (await fetch(s.basis +
      "/v1/replay?bbox=53,6,56,13&von=" + (t - 900) + "&bis=" + t + "&schritt=0")).json();
    assert.strictEqual(r.anzahl, 1);
    assert.strictEqual(r.spuren[0].mmsi, 42);
    assert.strictEqual(r.spuren[0].punkte.length, 10);
    // Die Form steht in der Antwort - sonst muss der Client raten.
    assert.deepStrictEqual(r.form, ["t", "lat_1e6", "lon_1e6", "sog_01", "cog_01"]);
    assert.strictEqual(r.spuren[0].punkte[0][1], 54000000);
  } finally { abbau(s); }
});

test("track liefert eine Einzelspur", async () => {
  const s = await starte(aufbau());
  try {
    const t = Math.floor(Date.now() / 1000);
    s.speicher.merke({ mmsi: 7, seen: (t - 100) * 1000, lat: 54, lon: 8, sog: 5, cog: 10 });
    s.speicher.merke({ mmsi: 7, seen: (t - 50) * 1000, lat: 54.1, lon: 8, sog: 5, cog: 10 });
    s.speicher.schreibe();
    const r = await (await fetch(s.basis + "/v1/track?mmsi=7")).json();
    assert.strictEqual(r.punkte.length, 2);
    const ohne = await fetch(s.basis + "/v1/track");
    assert.strictEqual(ohne.status, 400, "ohne mmsi ist das eine Fehlbedienung");
  } finally { abbau(s); }
});

test("ship unterscheidet 'noch nicht gefragt' von 'nichts gefunden'", async () => {
  const s = await starte(aufbau());
  try {
    s.zustand.melde(1, { lat: 54, lon: 8, name: "OFFEN" }, Date.now(), "strom");
    let r = await (await fetch(s.basis + "/v1/ship/1")).json();
    assert.strictEqual(r.register, "offen");

    s.speicher.stammSetze(1, { gefunden: 0, geprueft: Date.now() });
    r = await (await fetch(s.basis + "/v1/ship/1")).json();
    assert.strictEqual(r.register, "nichts gefunden");

    s.speicher.stammSetze(1, { gefunden: 1, geprueft: Date.now(), baujahr: "1998-01-01" });
    r = await (await fetch(s.basis + "/v1/ship/1")).json();
    assert.strictEqual(r.register, "gefunden");
    assert.strictEqual(r.baujahr, "1998-01-01");

    assert.strictEqual((await fetch(s.basis + "/v1/ship/999999")).status, 404);
  } finally { abbau(s); }
});

test("Zugangstoken sperrt HTTP und WebSocket", async () => {
  const s = await starte(aufbau({ ZUGANG: "geheim" }));
  try {
    assert.strictEqual((await fetch(s.basis + "/v1/snapshot")).status, 401);
    assert.strictEqual((await fetch(s.basis + "/v1/snapshot?token=geheim")).status, 200);
    assert.strictEqual((await fetch(s.basis + "/v1/snapshot",
      { headers: { authorization: "Bearer geheim" } })).status, 200);

    const abgewiesen = new WebSocket("ws://127.0.0.1:" + s.port + "/v1/live");
    await new Promise(r => { abgewiesen.on("error", r); abgewiesen.on("open", r); });
    assert.notStrictEqual(abgewiesen.readyState, WebSocket.OPEN, "ohne Token kein Strom");
  } finally { abbau(s); }
});

test("Fotos werden ausgeliefert, und Pfadtricks laufen ins Leere", async () => {
  const s = await starte(aufbau());
  try {
    fs.mkdirSync(s.konfig.FOTO_VERZEICHNIS, { recursive: true });
    fs.writeFileSync(path.join(s.konfig.FOTO_VERZEICHNIS, "123.jpg"), Buffer.from([0xff, 0xd8, 0xff]));
    const res = await fetch(s.basis + "/v1/foto/123.jpg");
    assert.strictEqual(res.status, 200);
    assert.strictEqual(res.headers.get("content-type"), "image/jpeg");

    // basename() schneidet jeden Verzeichniswechsel weg - ohne das koennte
    // ein Client beliebige Dateien des Servers anfordern.
    const versuch = await fetch(s.basis + "/v1/foto/" + encodeURIComponent("../../../etc/passwd"));
    assert.strictEqual(versuch.status, 404);
  } finally { abbau(s); }
});
