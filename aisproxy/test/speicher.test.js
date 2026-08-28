"use strict";
const test = require("node:test");
const assert = require("node:assert");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { Speicher, tagName } = require("../src/speicher");

const TAG = 24 * 3600 * 1000;

function neu(ueber) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "aisproxy-"));
  const konfig = Object.assign({
    DB_DATEI: path.join(dir, "t.db"),
    ROH_STUNDEN: 24, HISTORIE_TAGE: 7, VERDICHTUNG_S: 60, FAHRT_KN: 0.5,
    SCHREIB_MS: 100000,
    REGISTER_TREFFER_MS: 30 * 24 * 3600 * 1000,
    REGISTER_FEHL_MS: 3 * 24 * 3600 * 1000,
    FOTO_TEIL_MS: 60 * 60 * 1000,
    FOTO_FEHL_MS: 7 * 24 * 3600 * 1000,
    ZIEL_VERLAUF_MAX: 12
  }, ueber || {});
  const s = new Speicher({ konfig, log: () => {} });
  s.__dir = dir;
  return s;
}

function punkt(mmsi, tMs, lat, lon, sog) {
  return { mmsi, seen: tMs, lat, lon, sog: sog === undefined ? 8 : sog, cog: 90 };
}

test("Positionen landen in der Tagestabelle des Meldezeitpunkts", () => {
  const sp = neu();
  const jetzt = Date.now();
  sp.merke(punkt(1, jetzt, 54, 8));
  sp.merke(punkt(2, jetzt - 2 * TAG, 54, 8));
  assert.strictEqual(sp.schreibe(), 2);
  const tage = sp.vorhandeneTage();
  assert.ok(tage.includes(tagName(jetzt)), "heute");
  assert.ok(tage.includes(tagName(jetzt - 2 * TAG)), "vorgestern");
  sp.stopp();
});

test("ein Stapel ueber Mitternacht faellt auf zwei Tabellen", () => {
  // Der Fall, an dem eine Buendelung ohne Gruppierung die Haelfte falsch
  // einsortiert haette.
  const sp = neu();
  const mitternacht = Date.UTC(2026, 7, 27, 0, 0, 0);
  sp.merke(punkt(1, mitternacht - 1000, 54, 8));
  sp.merke(punkt(1, mitternacht + 1000, 54, 8));
  assert.strictEqual(sp.schreibe(), 2);
  assert.ok(sp.vorhandeneTage().includes("pos_20260826"));
  assert.ok(sp.vorhandeneTage().includes("pos_20260827"));
  sp.stopp();
});

test("Verdichtung: ein Punkt je Schiff und Fenster, Liegende fliegen raus", () => {
  const sp = neu({ ROH_STUNDEN: 1, VERDICHTUNG_S: 60 });
  // Auf eine volle Minute ausrichten: Die Fensterkante liegt bei t/60, und
  // 290 Sekunden ab einem beliebigen Versatz beruehren 5 ODER 6 Fenster.
  // Ohne diese Ausrichtung waere die Probe mal gruen und mal rot - und man
  // haette den Code verdaechtigt statt den Test.
  const alt = Math.floor((Date.now() - 3 * 3600 * 1000) / 60000) * 60000;
  // Ein fahrendes Schiff, 30 Punkte in 5 Minuten -> 5 Fenster
  for (let i = 0; i < 30; i++) sp.merke(punkt(1, alt + i * 10000, 54 + i / 1000, 8, 8));
  // Ein liegendes Schiff, 30 Punkte -> soll ganz verschwinden
  for (let i = 0; i < 30; i++) sp.merke(punkt(2, alt + i * 10000, 55, 9, 0));
  // Ein frisches Schiff -> darf nicht angefasst werden
  for (let i = 0; i < 10; i++) sp.merke(punkt(3, Date.now() - i * 1000, 54, 8, 8));
  sp.schreibe();
  const vorher = sp.bericht().punkte;
  assert.strictEqual(vorher, 70);

  sp.verdichte();
  const name = tagName(alt);
  const je = m => sp.db.prepare(`SELECT COUNT(*) AS n FROM ${name} WHERE mmsi = ?`).get(m).n;
  assert.strictEqual(je(1), 5, "fahrend: ein Punkt je Minutenfenster");
  assert.strictEqual(je(2), 0, "liegend: nichts bleibt uebrig");
  const heute = tagName(Date.now());
  const frisch = sp.db.prepare(`SELECT COUNT(*) AS n FROM ${heute} WHERE mmsi = 3`).get().n;
  assert.strictEqual(frisch, 10, "frische Punkte bleiben unangetastet");
  sp.stopp();
});

test("unbekannte Geschwindigkeit gilt nicht als liegend", () => {
  // "sog IS NULL" heisst nicht "liegt still" - dessen Spur wegzuwerfen waere
  // ein Datenverlust, den man hinterher nicht mehr bemerkt.
  const sp = neu({ ROH_STUNDEN: 1, VERDICHTUNG_S: 60 });
  const alt = Math.floor((Date.now() - 3 * 3600 * 1000) / 60000) * 60000;
  for (let i = 0; i < 12; i++) sp.merke(punkt(7, alt + i * 10000, 54 + i / 1000, 8, null));
  sp.schreibe();
  sp.verdichte();
  const name = tagName(alt);
  const n = sp.db.prepare(`SELECT COUNT(*) AS n FROM ${name} WHERE mmsi = 7`).get().n;
  assert.strictEqual(n, 2, "verdichtet, aber nicht verworfen");
  sp.stopp();
});

test("Aufbewahrung: alte Tage werden per DROP verworfen", () => {
  const sp = neu({ HISTORIE_TAGE: 7 });
  const jetzt = Date.now();
  for (const d of [0, 3, 6, 8, 20]) sp.merke(punkt(1, jetzt - d * TAG, 54, 8));
  sp.schreibe();
  assert.strictEqual(sp.vorhandeneTage().length, 5);
  const weg = sp.verwirf(jetzt);
  assert.strictEqual(weg.length, 2, "der 8. und der 20. Tag");
  assert.strictEqual(sp.vorhandeneTage().length, 3);
  // Gegenprobe: Nichts von den behaltenen Tagen ist verschwunden.
  assert.ok(sp.bericht().punkte === 3);
  sp.stopp();
});

test("spuren(): Ausschnitt, Zeitfenster und Raster greifen", () => {
  const sp = neu();
  const t = Math.floor(Date.now() / 1000);
  for (let i = 0; i < 20; i++) sp.merke(punkt(1, (t - 600 + i * 30) * 1000, 54, 8));
  for (let i = 0; i < 20; i++) sp.merke(punkt(2, (t - 600 + i * 30) * 1000, 60, 8)); // ausserhalb
  sp.schreibe();
  const box = { latMin: 53, lonMin: 6, latMax: 56, lonMax: 13 };

  const fein = sp.spuren(box, t - 900, t, 0);
  assert.strictEqual(fein.length, 1, "nur das Schiff im Ausschnitt");
  assert.strictEqual(fein[0].punkte.length, 20);

  const grob = sp.spuren(box, t - 900, t, 120);
  assert.ok(grob[0].punkte.length < fein[0].punkte.length, "das Raster duennt aus");
  assert.ok(grob[0].punkte.length >= 5);

  const eng = sp.spuren(box, t - 60, t, 0);
  assert.ok(eng.length === 0 || eng[0].punkte.length <= 3, "das Zeitfenster greift");
  sp.stopp();
});

test("spur(): eine Einzelspur in voller Aufloesung, tagesuebergreifend", () => {
  const sp = neu();
  const t = Math.floor(Date.now() / 1000);
  sp.merke(punkt(5, (t - 2 * 86400) * 1000, 54, 8));
  sp.merke(punkt(5, (t - 86400) * 1000, 54.1, 8));
  sp.merke(punkt(5, t * 1000, 54.2, 8));
  sp.schreibe();
  const p = sp.spur(5, t - 3 * 86400, t);
  assert.strictEqual(p.length, 3);
  assert.ok(p[0][0] < p[1][0] && p[1][0] < p[2][0], "nach Zeit sortiert");
  sp.stopp();
});

test("letzterStand(): der juengste Punkt je Schiff, fuer den Kaltstart", () => {
  const sp = neu();
  const t = Math.floor(Date.now() / 1000);
  sp.merke(punkt(1, (t - 300) * 1000, 54.0, 8));
  sp.merke(punkt(1, (t - 60) * 1000, 54.9, 8));
  sp.merke(punkt(2, (t - 100000) * 1000, 55, 9));   // zu alt
  sp.schreibe();
  const stand = sp.letzterStand(t - 1800);
  assert.strictEqual(stand.length, 1);
  assert.strictEqual(stand[0].mmsi, 1);
  assert.strictEqual(stand[0].lat, Math.round(54.9 * 1e6), "der juengste, nicht der erste");
  sp.stopp();
});

test("Stammdaten: anlegen, ergaenzen, Faelligkeit", () => {
  const sp = neu();
  const jetzt = Date.now();
  assert.deepStrictEqual(sp.stammFaellig([1, 2], jetzt), [1, 2], "noch nie gefragt");

  sp.stammSetze(1, { name: "EINS", gefunden: 1, geprueft: jetzt });
  sp.stammSetze(2, { gefunden: 0, geprueft: jetzt });
  assert.deepStrictEqual(sp.stammFaellig([1, 2], jetzt), [], "frisch geprueft");

  // Der Fehltreffer wird frueher wieder faellig als der Treffer - die IMO
  // kann jede Minute eintreffen und die Lage aendern.
  const spaeter = jetzt + 4 * 24 * 3600 * 1000;
  assert.deepStrictEqual(sp.stammFaellig([1, 2], spaeter), [2]);

  sp.stammSetze(1, { imo: 9876543 });
  const s = sp.stammHole(1);
  assert.strictEqual(s.name, "EINS", "das Ergaenzen loescht nichts");
  assert.strictEqual(s.imo, 9876543);
  sp.stopp();
});

test("der Fotostand haengt NICHT am Registerstand", () => {
  const s = neu();
  // Genau der gemeldete Zustand: Wikidata-Treffer sitzt, Bild fehlt, weil der
  // Download an einem HTTP 429 gescheitert ist. Frueher war das Schiff damit
  // 30 Tage gesperrt.
  s.stammSetze(211209320, { wd_entity: "Q1585523", gefunden: 1, geprueft: Date.now() });
  assert.deepStrictEqual(s.stammFaellig([211209320]), [],
    "fuer die Stammdaten ist es erledigt");
  assert.deepStrictEqual(s.fotoFaellig([211209320]), [211209320],
    "fuer das Foto nicht");
});

test("die kurze Frist gilt nur mit Anlass, nicht nach der Uhr", () => {
  // Diese Probe hiess frueher "zwei Fristen: ohne IMO kurz, mit IMO lang" und
  // hat genau das Gegenteil geprueft. Sie ist bewusst umgedreht: Am laufenden
  // Server kostete die stuendliche Wiederholung 524 Abrufe fuer 7 Bilder und
  // hielt den Rueckstand bei 1 587 Schiffen fest. Eine kurze Frist ohne
  // Aussicht ist kein Vorteil, sondern eine Tretmuehle.
  const s = neu();
  const jetzt = Date.now();
  s.stammSetze(1, { foto_geprueft: jetzt - 2 * 3600 * 1000, foto_quelle: "teil" });
  s.stammSetze(2, { foto_geprueft: jetzt - 2 * 3600 * 1000, foto_quelle: "nichts" });
  s.stammSetze(3, { foto_geprueft: jetzt - 2 * 3600 * 1000, foto_quelle: "teil",
                    imo: 9330032 });
  // Ohne IMO: liegen lassen. Mit IMO: sofort, denn jetzt lohnen die
  // Kategorie- und Volltextwege.
  assert.deepStrictEqual(s.fotoFaellig([1, 2, 3], jetzt), [3]);
  // Nach einer Woche ist alles wieder dran - Wikidata waechst.
  assert.deepStrictEqual(s.fotoFaellig([1, 2, 3], jetzt + 8 * TAG), [1, 2, 3]);
});

test("wer ein Bild hat, wird nicht mehr gefragt", () => {
  const s = neu();
  s.stammSetze(7, { foto_datei: "7.jpg", foto_geprueft: 1 });
  assert.deepStrictEqual(s.fotoFaellig([7], Date.now() + 99 * TAG), []);
});

test("der Bildabzug haelt Zuordnungen und laesst sich auffrischen", () => {
  const s = neu();
  assert.strictEqual(s.bildIndexSchreibe("imo", [["9321483", "https://a.jpg"],
                                                 ["7904592", "https://b.jpg"]]), 2);
  s.bildIndexSchreibe("mmsi", [["211209320", "https://c.jpg"]]);
  assert.strictEqual(s.bildIndexHole("imo", "9321483"), "https://a.jpg");
  assert.strictEqual(s.bildIndexHole("imo", 9321483), "https://a.jpg", "Zahl wie Text");
  assert.strictEqual(s.bildIndexHole("mmsi", "211209320"), "https://c.jpg");
  assert.strictEqual(s.bildIndexHole("imo", "211209320"), null, "die Arten sind getrennt");
  assert.strictEqual(s.bildIndexHole("imo", null), null);
  // Ein zweiter Abzug ueberschreibt, statt zu verdoppeln.
  s.bildIndexSchreibe("imo", [["9321483", "https://neu.jpg"]]);
  assert.strictEqual(s.bildIndexHole("imo", "9321483"), "https://neu.jpg");
  assert.strictEqual(s.bildIndexStand().imo.eintraege, 2);
});

test("neue Spalten kommen auch in eine bestehende Datenbank", () => {
  const s = neu();
  // spalteErgaenzen ist das Mittel gegen die Handwanderung auf dem Server:
  // CREATE TABLE IF NOT EXISTS aendert an einer vorhandenen Tabelle nichts.
  assert.strictEqual(s.spalteErgaenzen("schiff", "foto_quelle", "TEXT"), false,
    "schon da");
  assert.strictEqual(s.spalteErgaenzen("schiff", "probe_spalte", "TEXT"), true);
  assert.strictEqual(s.spalteErgaenzen("schiff", "probe_spalte", "TEXT"), false);
});

test("Stammdaten aus dem Strom loeschen keine Registerangabe", () => {
  // stammSetze() schreibt jedes uebergebene Feld. Kaeme aus dem Strom ein
  // null fuer die Laenge, waere die Angabe aus Wikidata weg - und niemand
  // wuerde es merken, weil die Spalte ja "schon einmal gefuellt war".
  const speicher = neu();
  speicher.stammSetze(211000009, { name: "AUS WIKIDATA", laenge: 95, gefunden: 1 });
  speicher.merkeStamm({ mmsi: 211000009, name: "AUS AIS", laenge: null, breite: null,
                        dimA: null, seen: Date.now() });
  assert.strictEqual(speicher.schreibeStamm(), 1);
  const a = speicher.stammHole(211000009);
  assert.strictEqual(a.laenge, 95, "die Registerlaenge muss stehen bleiben");
  assert.strictEqual(a.name, "AUS AIS", "der frische AIS-Name darf durch");
  assert.strictEqual(a.gefunden, 1, "und der Registerstand bleibt unberuehrt");

  // Und der vollstaendige Satz kommt samt Bezugspunkten an.
  speicher.merkeStamm({ mmsi: 211000010, name: "MS PROBE", laenge: 400, breite: 61,
                        dimA: 350, dimB: 50, dimC: 30, dimD: 31, typ: 70, seen: 1000 });
  speicher.schreibeStamm();
  const b = speicher.stammHole(211000010);
  assert.deepStrictEqual([b.dimA, b.dimB, b.dimC, b.dimD], [350, 50, 30, 31]);
  assert.strictEqual(b.gesehen, 1);
  speicher.stopp();
});

test("neue Spalte wd_typ merkt vorhandene Registersaetze zum Nachfragen vor", () => {
  // Der Registertyp kam spaeter als die Registersaetze. Ohne diesen einmaligen
  // Nachzug bliebe ein als "gefunden" vermerktes Schiff 30 Tage ohne Typ -
  // genau der Fall der "Liberty of the Seas": Wikidata weiss
  // "Kreuzfahrtschiff", der Proxy fragte nur nicht mehr nach.
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "aisproxy-mig-"));
  const konfig = {
    DB_DATEI: path.join(dir, "m.db"), ROH_STUNDEN: 24, HISTORIE_TAGE: 7,
    VERDICHTUNG_S: 60, FAHRT_KN: 0.5, SCHREIB_MS: 100000,
    REGISTER_TREFFER_MS: 30 * 24 * 3600 * 1000, REGISTER_FEHL_MS: 3 * 24 * 3600 * 1000,
    FOTO_TEIL_MS: 3600 * 1000, FOTO_FEHL_MS: 7 * 24 * 3600 * 1000
  };
  let sp = new Speicher({ konfig, log: () => {} });
  sp.db.exec("ALTER TABLE schiff DROP COLUMN wd_typ");   // Stand vor der Aenderung
  sp.stammSetze(211000001, { name: "MIT REGISTER", wd_entity: "Q1", gefunden: 1, geprueft: Date.now() });
  sp.stammSetze(211000002, { name: "OHNE REGISTER", gefunden: 0, geprueft: Date.now() });
  sp.stopp();

  sp = new Speicher({ konfig, log: () => {} });
  assert.strictEqual(sp.stammFaellig([211000001]).length, 1, "der Registersatz wird neu gefragt");
  assert.strictEqual(sp.stammFaellig([211000002]).length, 0,
    "ein Fehltreffer ohne Wikidata-Eintrag wird NICHT angefasst - sonst laeuft " +
    "die Abfrage wieder gegen aussichtslose Schiffe");
  sp.stammSetze(211000001, { geprueft: Date.now() });
  sp.stopp();

  // Und beim naechsten Start passiert nichts mehr: Der Nachzug haengt am
  // Anlegen der Spalte, nicht an ihrem Inhalt.
  sp = new Speicher({ konfig, log: () => {} });
  assert.strictEqual(sp.stammFaellig([211000001]).length, 0);
  sp.stopp();
});

test("ein Schiff ohne IMO wird erst wieder faellig, wenn es einen Anlass gibt", () => {
  // Gemessen am laufenden Server: Die MMSI-Wege kosteten 524 Abrufe fuer 7
  // Bilder, und weil "teil" jede Stunde erneut faellig wurde, ging das
  // Kontingent von 300 Fotoversuchen je Lauf dafuer drauf - der Rueckstand
  // blieb bei 1 587 stehen.
  const sp = neu();
  const vorZweiStunden = Date.now() - 2 * 3600 * 1000;
  const vorAchtTagen = Date.now() - 8 * 24 * 3600 * 1000;

  // a) Ohne IMO, vor zwei Stunden geprueft: bleibt liegen.
  sp.stammSetze(211000001, { foto_geprueft: vorZweiStunden, foto_quelle: "teil" });
  assert.deepStrictEqual(sp.fotoFaellig([211000001]), [],
    "die aussichtslose MMSI-Suche darf nicht stuendlich wiederholt werden");

  // b) Dieselbe Lage, aber die IMO ist inzwischen eingetroffen: sofort faellig.
  sp.stammSetze(211000002, { foto_geprueft: vorZweiStunden, foto_quelle: "teil",
                             imo: 9330032 });
  assert.deepStrictEqual(sp.fotoFaellig([211000002]), [211000002],
    "mit IMO lohnen Kategorie- und Volltextweg - das ist der Anlass");

  // c) Keine IMO, aber der Bildabzug kennt die MMSI: ein Download ohne Suche.
  sp.stammSetze(211000003, { foto_geprueft: vorZweiStunden, foto_quelle: "teil" });
  sp.bildIndexSchreibe("mmsi", [["211000003", "https://bild/x.jpg"]]);
  assert.deepStrictEqual(sp.fotoFaellig([211000003]), [211000003]);

  // d) Und "liegen bleiben" heisst nicht "nie wieder": Nach der langen Frist
  //    wird auch ohne Anlass noch einmal gefragt - Wikidata waechst.
  sp.stammSetze(211000004, { foto_geprueft: vorAchtTagen, foto_quelle: "teil" });
  assert.deepStrictEqual(sp.fotoFaellig([211000004]), [211000004]);

  // e) Ein vorhandenes Bild bleibt in Ruhe, egal was sonst gilt.
  sp.stammSetze(211000005, { foto_geprueft: vorAchtTagen, foto_quelle: "teil",
                             foto_datei: "211000005.jpg", imo: 123 });
  assert.deepStrictEqual(sp.fotoFaellig([211000005]), []);
  sp.stopp();
});

test("der Bericht nennt die Zahl der Bilder - die einzige dauerhafte", () => {
  // Nach einem Update steht im Register "0 Laeufe, 0 Fotos": Die Zaehler
  // leben im Prozess. Wer wissen will, ob die Fotoarbeit etwas gebracht hat,
  // braucht die Zahl aus der Datenbank.
  const sp = neu();
  sp.stammSetze(1, { foto_datei: "1.jpg", foto_quelle: "wikidata" });
  sp.stammSetze(2, { foto_datei: "2.jpg", foto_quelle: "eigen" });
  sp.stammSetze(3, { foto_geprueft: Date.now(), foto_quelle: "nichts" });
  const b = sp.bericht();
  assert.strictEqual(b.fotos, 2, "zwei Schiffe haben ein Bild");
  assert.strictEqual(b.fotosEigen, 1, "eines davon selbst beigesteuert");
  assert.strictEqual(b.stammEintraege, 3);
  sp.stopp();
});

test("ein deutscher Ortsname ueberlebt den naechsten Listenabzug", () => {
  // Die UNECE-Liste wird alle 90 Tage neu geholt. Schriebe sie `name`
  // bedingungslos zurueck, machte jeder Abzug aus "Kopenhagen" wieder
  // "København" - die deutsche Arbeit waere nach 90 Tagen weg.
  const sp = neu();
  sp.ortSchreibe([
    { code: "DKCPH", name: "København", land: "DK", funktion: "1234----" },
    { code: "PLGDN", name: "Gdansk", land: "PL", funktion: "12345---" }
  ]);
  assert.strictEqual(sp.ortDeutsch([["dkcph", "Kopenhagen"]]), 1,
    "kleingeschriebene Codes werden gehoben");
  assert.strictEqual(sp.ortStand().deutsch, 1);

  // Derselbe Abzug noch einmal, mit unveraendertem amtlichem Namen.
  sp.ortSchreibe([
    { code: "DKCPH", name: "København", land: "DK", funktion: "1234----" },
    { code: "PLGDN", name: "Gdansk", land: "PL", funktion: "12345---" }
  ]);
  const nach = sp.ortHole(["DKCPH", "PLGDN"]);
  assert.strictEqual(nach.DKCPH, "Kopenhagen", "der deutsche Name bleibt stehen");
  assert.strictEqual(nach.PLGDN, "Gdansk",
    "wo es keinen deutschen gibt, traegt weiter der amtliche");

  // Ein zweiter Durchlauf setzt nichts noch einmal - so ist die Zahl im
  // Bericht die Zahl der wirklich neuen Namen, nicht die der Versuche.
  assert.strictEqual(sp.ortDeutsch([["DKCPH", "Kopenhagen"]]), 0);
  sp.stopp();
});

test("das Zielfeld wird ueberschrieben - der Verlauf haelt fest, wo es hinging", () => {
  // Msg 5 traegt immer nur das aktuelle Ziel, und stammSetze schreibt es in
  // dieselbe Spalte. Ohne eigenen Verlauf waere die vorherige Reise weg,
  // obwohl der Proxy sie mitgehoert hat.
  const sp = neu();
  sp.stammSetze(211000001, { name: "TESTER", ziel: "DEHAM" });
  sp.stammSetze(211000001, { ziel: "DEHAM" });          // Wiederholung
  sp.stammSetze(211000001, { ziel: "NLRTM" });
  sp.stammSetze(211000001, { ziel: "DEBRV" });

  assert.strictEqual(sp.stammHole(211000001).ziel, "DEBRV", "aktuell bleibt aktuell");
  const v = sp.zielVerlauf(211000001);
  assert.deepStrictEqual(v.map(z => z.ziel), ["DEBRV", "NLRTM", "DEHAM"],
    "juengster Wechsel zuerst");

  // Zurueck nach Hamburg: derselbe Eintrag, aber wieder vorn.
  sp.stammSetze(211000001, { ziel: "DEHAM" });
  assert.deepStrictEqual(sp.zielVerlauf(211000001).map(z => z.ziel),
    ["DEHAM", "DEBRV", "NLRTM"], "kein zweiter Eintrag, nur neu einsortiert");
  assert.strictEqual(sp.zielVerlauf(211000001).length, 3);

  // Ein anderes Schiff bleibt unberuehrt.
  sp.stammSetze(211000002, { ziel: "DKCPH" });
  assert.deepStrictEqual(sp.zielVerlauf(211000002).map(z => z.ziel), ["DKCPH"]);
  sp.stopp();
});

test("der Zielverlauf ist je Schiff gedeckelt", () => {
  // Ein Transponder mit wechselndem Freitext fuellte die Tabelle sonst
  // unbegrenzt - und mehr als eine Handvoll liest ohnehin niemand.
  const sp = neu({ ZIEL_VERLAUF_MAX: 3 });
  ["A", "B", "C", "D", "E"].forEach(z => sp.stammSetze(211000003, { ziel: z }));
  assert.deepStrictEqual(sp.zielVerlauf(211000003).map(z => z.ziel), ["E", "D", "C"]);
  sp.stopp();
});

test("ein Zielwechsel im selben Schreibtakt geht nicht verloren", () => {
  // Der Puffer haelt je Schiff nur die juengste Meldung. Ohne eigene
  // Behandlung saehe stammSetze() den mittleren Wechsel nie - der Verlauf
  // haette ein Loch, und zwar ein unauffaelliges.
  const sp = neu();
  sp.merkeStamm({ mmsi: 211000011, name: "TAKT", ziel: "DEHAM", seen: Date.now() });
  sp.merkeStamm({ mmsi: 211000011, name: "TAKT", ziel: "NLRTM", seen: Date.now() });
  sp.merkeStamm({ mmsi: 211000011, name: "TAKT", ziel: "DEBRV", seen: Date.now() });
  assert.strictEqual(sp.schreibeStamm(), 1, "geschrieben wird trotzdem nur einmal");
  assert.deepStrictEqual(sp.zielVerlauf(211000011).map(z => z.ziel),
    ["DEBRV", "NLRTM", "DEHAM"]);
  assert.strictEqual(sp.stammHole(211000011).ziel, "DEBRV");

  // Eine Wiederholung desselben Ziels legt keinen zweiten Eintrag an.
  sp.merkeStamm({ mmsi: 211000012, ziel: "DKCPH", seen: Date.now() });
  sp.merkeStamm({ mmsi: 211000012, ziel: "DKCPH", seen: Date.now() });
  sp.schreibeStamm();
  assert.strictEqual(sp.zielVerlauf(211000012).length, 1);
  sp.stopp();
});
