"use strict";
const test = require("node:test");
const assert = require("node:assert");
const { Bilder, faltUm, nameWorte, titelBestaetigt, waehle,
        ersteUnterkategorie, flickrAutor } = require("../src/bilder");

const KONFIG = {
  COMMONS_URL: "https://commons.example/w/api.php",
  FLICKR_URL: "https://flickr.example/feed",
  FOTO_BREITE: 480
};

// Ein Bilder-Objekt, dessen Netzabrufe aus einer Liste vorbereiteter Antworten
// kommen. Ohne das liesse sich nur das Netz pruefen, nicht die Regeln.
function mitAntworten(antworten) {
  const gefragt = [];
  const b = new Bilder({
    konfig: KONFIG, log: () => {},
    hole: (url) => { gefragt.push(url); return Promise.resolve(antworten.shift()); }
  });
  b.gefragt = gefragt;
  return b;
}

const datei = (titel, url) => ({
  ns: 6, title: titel,
  imageinfo: [{ thumburl: url || "https://bild/" + encodeURIComponent(titel),
                extmetadata: { Artist: { value: "<a href='#'>Foto Mueller</a>" },
                               LicenseShortName: { value: "CC BY-SA 4.0" } } }]
});

test("Umlaute falten - STORSKAR trifft Storskaer", () => {
  assert.strictEqual(faltUm("Storskär"), "storskar");
  assert.ok(titelBestaetigt("Storskär in Stockholm.jpg", nameWorte("STORSKAR")));
  assert.strictEqual(faltUm("Süderoog"), "suderoog");
  assert.strictEqual(faltUm("Weiß"), "weiss");
});

test("Wortgrenzen statt Teilzeichenkette - Marines ist nicht Marlin", () => {
  // Der gemessene Fall: MMSI 304734000 steht im Beschreibungstext eines
  // Vietnamkriegsfotos. Ohne Wortgrenzen erfuellte "Marines" das "Marlin".
  assert.ok(!titelBestaetigt("US Marines land during Operation Jackstay",
    nameWorte("HAV MARLIN")));
  assert.ok(!titelBestaetigt("Bremerhaven, Überseehafen, Autoterminal",
    nameWorte("VEGA LEADER")));
  // Und der richtige Treffer bleibt: im Titel steht keine MMSI, nur der Name.
  assert.ok(titelBestaetigt("Pilot vessel Hanse (1).jpg", nameWorte("PILOTVESSEL HANSE")));
});

test("Stoppwoerter und zu kurze Wortteile tragen nicht", () => {
  assert.deepStrictEqual(nameWorte("PILOTVESSEL HANSE"), ["hanse"]);
  assert.deepStrictEqual(nameWorte("MS 4"), []);
  // Roemische Ziffern sind keine Unterscheidung: "Spiekeroog II" darf nicht
  // ueber die II auf ein beliebiges "II" passen.
  assert.deepStrictEqual(nameWorte("SPIEKEROOG II"), ["spiekeroog"]);
});

test("Auswahl ist stabil: Kennung im Namen zuerst, dann alphabetisch", () => {
  const seiten = {
    a: datei("Zeta ship.jpg"),
    b: datei("Alpha ship.jpg"),
    c: datei("IMO 9321483 bow.jpg"),
    d: { ns: 14, title: "Category:Irgendwas" },
    e: { ns: 6, title: "Kein Bild.svg", imageinfo: [{ url: "x" }] }
  };
  const eins = waehle(seiten, "9321483");
  assert.strictEqual(eins.titel, "IMO 9321483 bow.jpg");
  // Ohne Kennung im Namen entscheidet das Alphabet - und zwar bei jedem Lauf
  // gleich, sonst zeigt dasselbe Schiff jedes Mal ein anderes Bild.
  const zwei = waehle({ a: seiten.a, b: seiten.b }, "9321483");
  assert.strictEqual(zwei.titel, "Alpha ship.jpg");
  // HTML im Urhebervermerk wird gestrippt, Lizenz haengt an.
  assert.strictEqual(zwei.credit, "Foto Mueller · CC BY-SA 4.0");
});

test("nur Bilddateien - SVG und Kategorien fallen heraus", () => {
  assert.strictEqual(waehle({ a: { ns: 14, title: "Category:X" } }, "1"), null);
  assert.strictEqual(waehle({ a: { ns: 6, title: "x.svg", imageinfo: [{ url: "u" }] } }, "1"), null);
  assert.strictEqual(waehle({}, "1"), null);
  assert.strictEqual(ersteUnterkategorie({ a: datei("x.jpg"), b: { ns: 14, title: "Category:Y" } }),
    "Category:Y");
});

test("Kategorieweg: zwei Schritte ueber die Namenskategorie", async () => {
  const b = mitAntworten([
    { query: { pages: { 1: { ns: 14, title: "Category:Vestfjord (ship, 1993)" } } } },
    { query: { pages: { 2: datei("Vestfjord anchored in Tallinn Bay.jpg") } } }
  ]);
  const t = await b.commonsKategorie("9052692");
  assert.ok(t, "die Datei aus der Unterkategorie muss ankommen");
  assert.strictEqual(t.titel, "Vestfjord anchored in Tallinn Bay.jpg");
  assert.strictEqual(b.gefragt.length, 2);
  assert.ok(b.gefragt[0].includes(encodeURIComponent("Category:IMO 9052692")));
  assert.strictEqual(b.zaehler.treffer.commonsKategorie, 1);
});

test("Kategorieweg OHNE Titelregel - der Rumpf zaehlt, nicht der Name", async () => {
  // IMO 9052692 faehrt heute als BON VIVANT, das Foto heisst "Vestfjord".
  // Eine Namenspruefung hier wuerfe die Bilder aller umbenannten Schiffe weg.
  const b = mitAntworten([
    { query: { pages: { 1: datei("Vestfjord anchored in Tallinn Bay.jpg") } } }
  ]);
  const t = await b.commonsKategorie("9052692");
  assert.ok(t, "ein abweichender Schiffsname darf den Treffer nicht kippen");
});

test("Volltextweg MIT Titelregel - der Fluss faellt heraus", async () => {
  // Der gemessene Fehltreffer: "Aftermath of Severn Bore wave" fuer die
  // "Bore Wave", IMO 9892896. Im Titel steht die IMO nicht.
  const b = mitAntworten([
    { query: { pages: { 1: datei("Aftermath of Severn Bore wave.jpg") } } }
  ]);
  assert.strictEqual(await b.commonsVolltext("9892896"), null);

  const b2 = mitAntworten([
    { query: { pages: { 1: datei("Aftermath of Severn Bore wave.jpg"),
                        2: datei("Bore Wave IMO 9892896.jpg") } } }
  ]);
  const t = await b2.commonsVolltext("9892896");
  assert.strictEqual(t.titel, "Bore Wave IMO 9892896.jpg");
});

test("MMSI-Weg: Kennung sucht, Name bestaetigt", async () => {
  const b = mitAntworten([
    { query: { pages: { 1: datei("US Marines land during Operation Jackstay.jpg") } } }
  ]);
  assert.strictEqual(await b.commonsMmsi("304734000", "HAV MARLIN"), null);

  const b2 = mitAntworten([
    { query: { pages: { 1: datei("Pilot vessel Hanse (1).jpg") } } }
  ]);
  const t = await b2.commonsMmsi("211324470", "PILOTVESSEL HANSE");
  assert.strictEqual(t.titel, "Pilot vessel Hanse (1).jpg");
});

test("ohne brauchbare Namensworte gibt es keinen MMSI-Weg", async () => {
  const b = mitAntworten([]);
  assert.strictEqual(await b.commonsMmsi("211324470", "MS 4"), null);
  assert.strictEqual(b.gefragt.length, 0, "und es wird gar nicht erst gefragt");
});

test("Flickr: Autor aus der Klammer, Bildgroesse hochgesetzt", async () => {
  const b = mitAntworten([{ items: [
    { title: "Zweite", link: "https://flickr/b", author: 'nobody@flickr.com ("zweiter")',
      media: { m: "https://live.staticflickr.com/1/b_x_m.jpg" } },
    { title: "Emma Maersk", link: "https://flickr/a", author: 'nobody@flickr.com ("andreasspoerri")',
      media: { m: "https://live.staticflickr.com/1/a_x_m.jpg" } }
  ] }]);
  const t = await b.flickr("imo", "9321483");
  // Sortiert nach dem Link, damit dasselbe Schiff bei jedem Lauf dasselbe
  // Bild bekommt - der Feed liefert die neuesten zuerst und waere sonst
  // von Lauf zu Lauf ein anderes.
  assert.strictEqual(t.titel, "Emma Maersk");
  assert.strictEqual(t.url, "https://live.staticflickr.com/1/a_x_z.jpg");
  assert.strictEqual(t.credit, "andreasspoerri · Flickr");
  assert.strictEqual(t.seite, "https://flickr/a");
  assert.ok(b.gefragt[0].includes("tags=imo9321483"));
});

test("Flickr: leerer Feed ist kein Treffer, MMSI benutzt das gepraegte Tag", async () => {
  const b = mitAntworten([{ items: [] }]);
  assert.strictEqual(await b.flickr("mmsi", "211224140"), null);
  assert.ok(b.gefragt[0].includes("tags=mmsi211224140"),
    "die nackte Zahl waere kein Schiffsbeleg - nur das gepraegte Tag zaehlt");
  assert.strictEqual(flickrAutor("nobody@flickr.com (\"wer\")"), "wer");
  assert.strictEqual(flickrAutor("ohne Klammer"), "ohne Klammer");
});
