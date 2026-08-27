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
  REGISTER_PAUSE_MS: 1200,
  // Klein halten: Der Fotolauf fasst je Schiff bis zu fuenf fremde Dienste
  // an. Fuer die Probe zaehlt, DASS die Kette traegt, nicht wie viele Bilder
  // zusammenkommen - die Zahl misst scratchpad/fotolauf.js am ganzen Bestand.
  FOTO_MAX_PRO_LAUF: 25,
  FOTO_PAUSE_MS: 700
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
  console.log("  Abfragen: Wikidata " + b.wikidata + ", Fotos " + b.fotos +
    ", Fotofehler " + b.fotoFehler + ", Fehler " + b.fehler);
  console.log("  Bildwege: " + JSON.stringify(b.wege));
  ok(lauf && lauf.faellig > 0, "6) es gab etwas zu tun");
  // Zwei Abfragen fuer den Abzug, dazu je ein Buendel MMSI und IMO.
  ok(b.wikidata <= 6, "7) und es kostete nur " + b.wikidata +
    " Wikidata-Abfragen fuer " + lauf.faellig + " Schiffe (einzeln waeren es " +
    lauf.faellig + ")");

  const abzug = speicher.bildIndexStand();
  console.log("  Bildabzug: " + JSON.stringify(abzug));
  ok(abzug.imo && abzug.imo.eintraege > 10000,
    "7a) der Bildabzug haelt die ganze IMO-Zuordnung (" +
    (abzug.imo ? abzug.imo.eintraege : 0) + " Zeilen) - danach kostet die " +
    "Fotofrage je Schiff keinen Abruf mehr");
  ok(abzug.mmsi && abzug.mmsi.eintraege > 5000,
    "7b) und die ueber die MMSI (" + (abzug.mmsi ? abzug.mmsi.eintraege : 0) + ")");
  ok(lauf.fotos && lauf.fotos.neu > 0,
    "7c) der Fotolauf bringt Bilder (" + (lauf.fotos ? lauf.fotos.neu : 0) +
    " von " + (lauf.fotos ? lauf.fotos.versucht : 0) + " versuchten)");
  ok(lauf.fotos && lauf.fotos.versucht <= konfig.FOTO_MAX_PRO_LAUF,
    "7d) die Obergrenze je Lauf greift");
  ok(lauf.fotos && lauf.fotos.offen === lauf.fotos.faellig - lauf.fotos.versucht,
    "7e) und was nicht drankam, steht als offen im Bericht (" +
    (lauf.fotos ? lauf.fotos.offen : "?") + ") - eine stille Kappung " +
    "liest sich wie \"alles abgearbeitet\"");

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
  // Der Fotolauf arbeitet weiter an seinem Rueckstand - das ist gewollt und
  // hat mit dem Registerstand nichts zu tun. Geprueft wird hier NUR, dass
  // keine Wikidata-Abfrage mehr faellt.
  ok(register.bericht().wikidata === vorher, "10) und stellt keine " +
    "Wikidata-Abfrage mehr - auch der Bildabzug wird nicht erneut geholt");

  // Und die Gegenprobe zum eigentlichen Befund: Ein Schiff, dessen Foto nicht
  // geklappt hat, darf nicht wie "hat keins" behandelt werden.
  console.log("\n=== Fotostand haengt nicht am Registerstand ===");
  speicher.stammSetze(999999999, { wd_entity: "Q1", gefunden: 1, geprueft: Date.now() });
  ok(speicher.stammFaellig([999999999]).length === 0 &&
     speicher.fotoFaellig([999999999]).length === 1,
    "11) Stammdaten erledigt, Foto weiter faellig - genau das war der Fehler, " +
    "der 19 von 22 Schiffen mit vorhandenem Wikidata-Bild ohne Foto liess");

  speicher.stopp();
  console.log("\n" + (fehler ? fehler + " Pruefung(en) fehlgeschlagen" : "alle Pruefungen bestanden"));
  process.exit(fehler ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
