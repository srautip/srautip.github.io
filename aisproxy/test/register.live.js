"use strict";

// Registerlauf gegen die echten Dienste. Getrennt vom uebrigen Live-Test,
// weil er drei fremde Systeme anfasst - und zwar freie, unbezahlte.
// Deshalb absichtlich klein gehalten.
//
//   node test/register.live.js

const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { Speicher } = require("../src/speicher");
const { Zustand } = require("../src/zustand");
const { Register } = require("../src/register");

const dir = fs.mkdtempSync(path.join(os.tmpdir(), "aisproxy-reg-"));
const konfig = Object.assign({}, require("../src/konfig"), {
  DB_DATEI: path.join(dir, "r.db"),
  FOTO_VERZEICHNIS: path.join(dir, "fotos"),
  WIKIDATA_BUENDEL: 200,
  REGISTER_PAUSE_MS: 1200
});

let fehler = 0;
const ok = (b, t) => { if (!b) fehler++; console.log((b ? "  OK   " : "  FEHL ") + t); };

(async () => {
  const speicher = new Speicher({ konfig, log: console.log });
  const zustand = new Zustand({ ttlMs: 60 * 60 * 1000 });
  const register = new Register({ konfig, speicher, zustand, log: console.log });

  // Echte MMSIs aus der Region holen - erfundene pruefen nur die eigene
  // Annahme, nicht das Register.
  const r = konfig.REGION;
  const res = await fetch(konfig.REST_URL + "?bbox=" +
    [r.latMin, r.lonMin, r.latMax, r.lonMax].join(","));
  const geo = await res.json();
  const feats = geo.features.slice(0, 200);
  for (const f of feats) {
    const c = f.geometry.coordinates;
    zustand.melde(f.properties.mmsi, { lat: c[1], lon: c[0], name: f.properties.name },
      Date.now(), "netz");
  }
  console.log(feats.length + " echte Schiffe aus der Region geladen\n");

  console.log("=== Wikidata-Buendelabfrage ===");
  const mmsis = [...zustand.schiffe.keys()];
  const t0 = Date.now();
  const treffer = await register.wikidata(mmsis, false);
  const dauer = Date.now() - t0;
  console.log("  " + mmsis.length + " MMSIs in EINER Abfrage -> " + treffer.length +
    " Treffer in " + (dauer / 1000).toFixed(1) + " s");
  ok(dauer < 20000, "1) eine Buendelabfrage bleibt schnell (" + (dauer / 1000).toFixed(1) + " s)");
  ok(treffer.length > 0, "2) sie liefert Treffer (" + treffer.length + ")");
  if (treffer.length) {
    const b = treffer[0];
    console.log("  Beispiel: MMSI " + b.schluessel + " -> " + (b.felder.name || "(ohne Label)") +
      (b.felder.imo ? ", IMO " + b.felder.imo : "") +
      (b.felder.bild ? ", mit Foto" : ""));
    ok(b.felder.wd_entity && /wikidata\.org/.test(b.felder.wd_entity),
      "3) mit Verweis auf den Wikidata-Eintrag");
    ok(!b.felder.bild || /^https:/.test(b.felder.bild),
      "4) Bild-Adressen sind auf https gehoben (sonst blockiert der Browser sie)");
  }

  console.log("\n=== Digitraffic-Sammelabruf ===");
  const t1 = Date.now();
  const dt = await register.digitraffic();
  console.log("  " + dt + " von " + zustand.anzahl + " Schiffen der Region bekannt, " +
    ((Date.now() - t1) / 1000).toFixed(1) + " s");
  ok(true, "5) der Sammelabruf laeuft durch (Treffer in dieser Region erwartungsgemaess nahe null)");

  console.log("\n=== Vollstaendiger Lauf ===");
  const t2 = Date.now();
  const lauf = await register.lauf();
  console.log("  " + JSON.stringify(lauf));
  const b = register.bericht();
  console.log("  Abfragen: Wikidata " + b.wikidata + ", Commons " + b.commons +
    ", Fotos " + b.fotos + ", Fehler " + b.fehler);
  ok(lauf && lauf.faellig > 0, "6) es gab etwas zu tun");
  ok(b.wikidata <= 4, "7) und es kostete nur " + b.wikidata +
    " Wikidata-Abfragen fuer " + lauf.faellig + " Schiffe (einzeln waeren es " +
    lauf.faellig + ")");

  const stand = speicher.bericht();
  console.log("  Stammdaten: " + stand.stammEintraege + " Eintraege, " +
    stand.stammTreffer + " mit Treffer");
  ok(stand.stammEintraege === lauf.faellig,
    "8) jedes faellige Schiff hat einen Vermerk - auch die ohne Treffer");

  // Der zweite Lauf darf nichts mehr tun: "nichts gefunden" IST ein Ergebnis.
  const vorher = register.bericht().wikidata;
  const zweiter = await register.lauf();
  ok(zweiter.faellig === 0, "9) der zweite Lauf findet nichts Faelliges mehr - " +
    "ohne den Fehltreffer-Vermerk liefe er endlos gegen dieselben Schiffe");
  ok(register.bericht().wikidata === vorher, "10) und stellt keine einzige Abfrage");

  speicher.stopp();
  console.log("\n" + (fehler ? fehler + " Pruefung(en) fehlgeschlagen" : "alle Pruefungen bestanden"));
  process.exit(fehler ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
