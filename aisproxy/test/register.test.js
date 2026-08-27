"use strict";
const test = require("node:test");
const assert = require("node:assert");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { Speicher } = require("../src/speicher");
const { Zustand } = require("../src/zustand");
const { Register } = require("../src/register");

function neu() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "aisproxy-reg-"));
  const konfig = Object.assign({}, require("../src/konfig"), {
    DB_DATEI: path.join(dir, "r.db"), FOTO_VERZEICHNIS: path.join(dir, "fotos")
  });
  const speicher = new Speicher({ konfig, log: () => {} });
  const zustand = new Zustand({ ttlMs: 3600000 });
  return { speicher, register: new Register({ konfig, speicher, zustand, log: () => {} }) };
}

test("ein uebersprungener Bildabzug steht als solcher im Bericht", async () => {
  // Gemeldet: Auf dem Server steht bei "abzug" null, obwohl der Abzug laengst
  // geholt war. Ursache: Der Zaehler lebt im Prozess, der Index in der
  // Datenbank - nach einem Neustart wird der Abzug uebersprungen (er ist
  // keine 24 h alt), und die frueher Fassung liess `abzug` dann auf null
  // stehen. "Frisch und deshalb nicht geholt" sah aus wie "nie geholt".
  const { speicher, register } = neu();
  speicher.bildIndexSchreibe("imo", [["9330032", "https://bild/a.jpg"],
                                     ["9552991", "https://bild/b.jpg"]]);
  speicher.bildIndexSchreibe("mmsi", [["211224140", "https://bild/c.jpg"]]);

  const zurueck = await register.bildAbzug();
  assert.strictEqual(zurueck, null, "kein neuer Abzug, der Index ist frisch");

  const b = register.bericht();
  assert.ok(b.abzug, "aber der Bericht darf nicht null melden");
  assert.strictEqual(b.abzug.uebersprungen, true);
  assert.strictEqual(b.abzug.imo, 2);
  assert.strictEqual(b.abzug.mmsi, 1);
  assert.ok(typeof b.abzug.naechster === "string", "und wann wieder geholt wird");
  assert.strictEqual(register.bericht_.wikidata, 0, "ohne eine einzige Abfrage");
  speicher.stopp();
});

test("die Bildwege stehen im Bericht, auch ohne fertigen Lauf", () => {
  // Der erste Lauf auf einem gefuellten Server dauert Minuten. Solange stand
  // in `wege` null - ununterscheidbar von "sucht keine Bilder".
  const { speicher, register } = neu();
  register.bilder.zaehler.commonsKategorie = 3;
  const b = register.bericht();
  assert.ok(b.wege, "wege kommt direkt aus den Zaehlern");
  assert.strictEqual(b.wege.commonsKategorie, 3);
  speicher.stopp();
});
