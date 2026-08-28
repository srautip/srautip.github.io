"use strict";
// Der Caddyfile-Matcher und die Routen in server.js muessen zusammenpassen.
//
// Anlass: /v1/ort kam nach dem Caddyfile dazu und stand nicht im Matcher.
// Caddy verlangte dafuer Basic-Auth, die ein Browser-fetch nicht mitschickt,
// und antwortete mit 401 - der Proxy wurde nie gefragt. Im Client blieb
// "CIABJ" (Abidjan) unaufgeloest stehen, und weil europaeische Haefen in
// seiner eigenen Rueckfalltabelle stehen, fiel es lange nicht auf. Ein
// stiller 401 auf einem Pfad, den niemand geprueft hat, ist genau die Sorte
// Fehler, gegen die eine Datei allein nicht hilft.
const test = require("node:test");
const assert = require("node:assert");
const fs = require("node:fs");
const path = require("node:path");

// Was ein Mensch im Browser aufruft, soll hinter Basic-Auth bleiben - das ist
// der Zweck der Trennung, nicht ein Versehen.
const MENSCH = ["/v1/status", "/v1", "/"];

function routen() {
  const quelle = fs.readFileSync(path.join(__dirname, "..", "src", "server.js"), "utf8");
  const aus = new Set();
  // url.pathname === "/v1/x"  und  url.pathname.startsWith("/v1/x/")
  for (const m of quelle.matchAll(/url\.pathname(?:\s*===\s*|\.startsWith\()\s*"(\/v1[^"]*)"/g)) {
    aus.add(m[1]);
  }
  return [...aus].sort();
}

function matcher() {
  const roh = fs.readFileSync(path.join(__dirname, "..", "Caddyfile"), "utf8");
  const zeile = roh.split("\n").find(z => z.includes("@schnittstelle path "));
  assert.ok(zeile, "der Matcher @schnittstelle steht nicht mehr im Caddyfile");
  return zeile.split("@schnittstelle path ")[1].trim().split(/\s+/);
}

function gedeckt(pfad, muster) {
  return muster.some(m => m.endsWith("*") ? pfad.startsWith(m.slice(0, -1)) : pfad === m);
}

test("jeder Schnittstellenpfad steht im Caddyfile - sonst kommt der Client nicht durch", () => {
  const muster = matcher();
  const offen = routen().filter(p => !MENSCH.includes(p) && !gedeckt(p, muster));
  assert.deepStrictEqual(offen, [],
    "diese Pfade beantwortet der Server, aber Caddy verlangt dafuer Basic-Auth, " +
    "die ein Browser-fetch und erst recht ein WebSocket nicht mitschicken kann");

  // Die Gegenprobe: Der Matcher deckt /v1/ort wirklich ab, und /v1/status
  // ausdruecklich NICHT - sonst pruefte der Test nur sich selbst.
  assert.ok(gedeckt("/v1/ort", muster), "/v1/ort gehoert hinter den Matcher");
  assert.ok(!gedeckt("/v1/status", muster), "/v1/status bleibt hinter Basic-Auth");

  // /v1/live steht nicht im Routenblock (es ist der WebSocket-Aufstieg), muss
  // aber unbedingt durch: Ein Browser kann bei einem WebSocket keine Header
  // setzen, Basic-Auth ist dort grundsaetzlich nicht erfuellbar.
  assert.ok(gedeckt("/v1/live", muster), "/v1/live gehoert hinter den Matcher");
});
