"use strict";
const test = require("node:test");
const assert = require("node:assert");
const { uebersetze, etaZahl } = require("../src/strom");
const { uebernimm } = require("../src/netz");
const { Zustand } = require("../src/zustand");
const draht = require("../src/draht");

// Die Nachrichtenform stammt aus einem echten Mitschnitt vom 27. Aug. 2026
// ueber der Deutschen Bucht - nicht aus der Dokumentation. Erfundene
// Testdaten pruefen die eigene Annahme, nicht den Feed.
const ECHT = {
  MessageType: "PositionReport",
  Message: { PositionReport: {
    Cog: 233.1, CommunicationState: 0, Latitude: 55.45812166666666,
    Longitude: 8.432158333333334, MessageID: 1, NavigationalStatus: 0,
    PositionAccuracy: false, Raim: false, RateOfTurn: 0, RepeatIndicator: 0,
    Sog: 0.6, Spare: 0, SpecialManoeuvreIndicator: 0, Timestamp: 58,
    TrueHeading: 315, UserID: 219000603, Valid: true } },
  MetaData: { MMSI: 219000603, ShipName: "TESTSCHIFF         ",
              time_utc: "2026-08-27 08:06:07.123 +0000 UTC",
              latitude: 55.45812166666666, longitude: 8.432158333333334 }
};

test("PositionReport wird vollstaendig uebernommen", () => {
  const r = uebersetze(ECHT);
  assert.strictEqual(r.mmsi, 219000603);
  assert.ok(r.hatPosition);
  assert.strictEqual(r.felder.sog, 0.6);
  assert.strictEqual(r.felder.cog, 233.1);
  assert.strictEqual(r.felder.hdg, 315);
  assert.strictEqual(r.felder.status, 0);
  // Der Name ist mit Leerzeichen aufgefuellt - ungetrimmt landete er so in
  // der Datenbank und spaeter auf der Karte.
  assert.strictEqual(r.felder.name, "TESTSCHIFF");
  assert.ok(Number.isFinite(r.stand) && r.stand > 0, "time_utc ist lesbar");
});

test("time_utc mit ' +0000 UTC' wird richtig gelesen", () => {
  // Diese Schreibweise ist Go-typisch und bringt Date.parse ohne Umbau zu
  // Fall - dann waere jeder Zeitstempel NaN und die Ordnung sinnlos.
  const r = uebersetze(ECHT);
  assert.strictEqual(new Date(r.stand).toISOString().slice(0, 19), "2026-08-27T08:06:07");
});

test("Sentinels werden zu null, nicht zu Zahlen", () => {
  const m = JSON.parse(JSON.stringify(ECHT));
  m.Message.PositionReport.Sog = 102.3;      // "nicht verfuegbar"
  m.Message.PositionReport.Cog = 360;        // "nicht verfuegbar"
  m.Message.PositionReport.TrueHeading = 511;// "nicht verfuegbar"
  const r = uebersetze(m);
  assert.strictEqual(r.felder.sog, null);
  assert.strictEqual(r.felder.cog, null);
  assert.strictEqual(r.felder.hdg, null);
});

test("ShipStaticData bringt IMO, Masse und Ziel", () => {
  const r = uebersetze({
    MessageType: "ShipStaticData",
    Message: { ShipStaticData: {
      Name: "MS NORDLICHT@@@@@@@@", CallSign: "DTST  ", ImoNumber: 9412517,
      Destination: "DEBRV@@@@@@@@@@@", Type: 70, MaximumStaticDraught: 6.8,
      Dimension: { A: 100, B: 50, C: 12, D: 12 },
      Eta: { Month: 8, Day: 28, Hour: 14, Minute: 30 } } },
    MetaData: { MMSI: 211900001, time_utc: "2026-08-27 08:00:00 +0000 UTC" }
  });
  assert.strictEqual(r.felder.name, "MS NORDLICHT", "das @-Auffuellzeichen faellt weg");
  assert.strictEqual(r.felder.rufzeichen, "DTST");
  assert.strictEqual(r.felder.imo, 9412517);
  assert.strictEqual(r.felder.ziel, "DEBRV");
  assert.strictEqual(r.felder.laenge, 150, "A + B");
  assert.strictEqual(r.felder.breite, 24, "C + D");
  assert.strictEqual(r.felder.tiefgang, 6.8);
});

test("Dimension aus lauter Nullen ist keine Groesse", () => {
  // Das Objekt ist truthy, der Inhalt ist "keine Angabe" - genau die Falle,
  // an der der Client schon einmal haengen geblieben ist.
  const r = uebersetze({
    MessageType: "ShipStaticData",
    Message: { ShipStaticData: { Name: "X", Dimension: { A: 0, B: 0, C: 0, D: 0 } } },
    MetaData: { MMSI: 1, time_utc: "2026-08-27 08:00:00 +0000 UTC" }
  });
  assert.strictEqual(r.felder.laenge, undefined);
  assert.strictEqual(r.felder.breite, undefined);
});

test("Class-B-Statik kommt in zwei Teilen", () => {
  const r = uebersetze({
    MessageType: "StaticDataReport",
    Message: { StaticDataReport: {
      ReportA: { Name: "KLEINBOOT@@@" },
      ReportB: { CallSign: "DABC", ShipType: 37, Dimension: { A: 6, B: 4, C: 2, D: 2 } } } },
    MetaData: { MMSI: 211111111, time_utc: "2026-08-27 08:00:00 +0000 UTC" }
  });
  assert.strictEqual(r.felder.name, "KLEINBOOT");
  assert.strictEqual(r.felder.rufzeichen, "DABC");
  assert.strictEqual(r.felder.typ, 37);
  assert.strictEqual(r.felder.laenge, 10);
});

test("Class B wird als solche markiert", () => {
  const b = uebersetze({
    MessageType: "StandardClassBPositionReport",
    Message: { StandardClassBPositionReport: { Latitude: 54, Longitude: 8, Sog: 3, Cog: 90 } },
    MetaData: { MMSI: 2, time_utc: "2026-08-27 08:00:00 +0000 UTC" }
  });
  assert.ok(b.felder.flags & draht.FLAG_KLASSE_B);
  const a = uebersetze(ECHT);
  assert.ok(!(a.felder.flags & draht.FLAG_KLASSE_B));
});

test("ETA: Objekt aus dem Strom und Integer von Digitraffic werden gleich abgelegt", () => {
  const ausStrom = etaZahl({ Month: 8, Day: 28, Hour: 14, Minute: 30 });
  const gepackt = (8 << 16) | (28 << 11) | (14 << 6) | 30;
  assert.strictEqual(ausStrom, gepackt, "eine Form in der Ablage, nicht zwei");
  assert.strictEqual(etaZahl(1596), null, "der Sentinel 'keine Angabe'");
  assert.strictEqual(etaZahl(0), null);
  assert.strictEqual(etaZahl(null), null);
});

test("unbrauchbare Nachrichten werden verworfen, nicht halb uebernommen", () => {
  assert.strictEqual(uebersetze({ MessageType: "PositionReport", MetaData: {} }), null,
    "ohne MMSI");
  const ohnePos = uebersetze({
    MessageType: "PositionReport",
    Message: { PositionReport: { Latitude: 91, Longitude: 181 } },
    MetaData: { MMSI: 5, time_utc: "2026-08-27 08:00:00 +0000 UTC" }
  });
  assert.ok(!ohnePos.hatPosition, "91/181 sind die Sentinels fuer 'unbekannt'");
});

test("Snapshot-Feature landet im selben Datensatz wie der Strom", () => {
  const z = new Zustand({ ttlMs: 60000 });
  const s = uebernimm(z, {
    type: "Feature",
    geometry: { type: "Point", coordinates: [7.89, 53.79] },
    properties: { mmsi: 211900002, name: "AUS DEM SNAPSHOT", type: 70,
                  sog: 9.1, cog: 45, heading: 44, nav_status: 0,
                  kind: "vessel", seen: "2026-08-27T08:05:00Z" }
  });
  assert.ok(s);
  assert.strictEqual(s.lat, 53.79);
  assert.strictEqual(s.lon, 7.89);
  assert.strictEqual(s.sog, 9.1);
  assert.strictEqual(s.name, "AUS DEM SNAPSHOT");
  // Der Snapshot bringt seinen eigenen Zeitstempel mit - ihn zu benutzen
  // statt "jetzt" haelt die Altersanzeige ehrlich.
  assert.strictEqual(s.seen, Date.parse("2026-08-27T08:05:00Z"));
  assert.strictEqual(s.quelle, "netz");
});

test("Seezeichen bekommen ihre Flagge", () => {
  const z = new Zustand({ ttlMs: 60000 });
  const s = uebernimm(z, {
    geometry: { coordinates: [8, 54] },
    properties: { mmsi: 992111111, kind: "aton", seen: "2026-08-27T08:00:00Z" }
  });
  assert.ok(s.flags & draht.FLAG_SEEZEICHEN);
});

// Beide Nachrichten stammen aus einem echten Mitschnitt vom 27. Aug. 2026.
// Die Feldnamen sind der Grund fuer diese Proben: aisstream schreibt
// VenderIDModel/VenderIDSerial - samt Tippfehler. Geraten hatte ich
// UnitModelCode und SerialNumber; beide Spalten blieben in der Messung leer
// (0 von 2 775 Schiffen), ohne dass irgendetwas fehlgeschlagen waere.
const STATIK5 = {
  MessageType: "ShipStaticData",
  Message: { ShipStaticData: {
    AisVersion: 0, CallSign: "V2IK2", Destination: "DE EME",
    Dimension: { A: 116, B: 27, C: 2, D: 20 }, Dte: false,
    Eta: { Day: 22, Hour: 18, Minute: 0, Month: 8 }, FixType: 1,
    ImoNumber: 9553646, MaximumStaticDraught: 6.8, MessageID: 5,
    Name: "GAMMAGAS", RepeatIndicator: 0, Spare: false, Type: 83,
    UserID: 305491000, Valid: true } },
  MetaData: { MMSI: 305491000, time_utc: "2026-08-27 20:00:00.0 +0000 UTC" }
};

test("Msg 5 wird vollstaendig uebernommen, nicht nur die Tabellenfelder", () => {
  const f = uebersetze(STATIK5).felder;
  assert.strictEqual(f.klasse, "A");
  assert.strictEqual(f.name, "GAMMAGAS");
  assert.strictEqual(f.rufzeichen, "V2IK2");
  assert.strictEqual(f.imo, 9553646);
  assert.strictEqual(f.typ, 83);
  assert.strictEqual(f.ziel, "DE EME");
  assert.strictEqual(f.tiefgang, 6.8);
  assert.strictEqual(f.laenge, 143);
  assert.deepStrictEqual([f.dimA, f.dimB, f.dimC, f.dimD], [116, 27, 2, 20]);
  assert.strictEqual(f.geraet, 1, "FixType: 1 = GPS");
  assert.strictEqual(f.aisVersion, 0);
  assert.strictEqual(f.dte, 0, "aus dem Bool wird 0/1, sonst passt es in keine Spalte");
});

test("Msg 24: Teil A darf den Schiffstyp aus Teil B nicht auf 0 setzen", () => {
  // Teil A und Teil B kommen als zwei Nachrichten. Die jeweils andere Haelfte
  // ist mit Nullen gefuellt und traegt Valid: false - ohne Riegel setzte jede
  // Teil-A-Meldung typ auf 0 ("keine Angabe") und loeschte damit einen
  // bekannten Typ.
  const teilA = {
    MessageType: "StaticDataReport",
    Message: { StaticDataReport: {
      MessageID: 24, PartNumber: false, RepeatIndicator: 0,
      ReportA: { Name: "BLUEFIN", Valid: true },
      ReportB: { CallSign: "", Dimension: { A: 0, B: 0, C: 0, D: 0 }, FixType: 0,
                 ShipType: 0, Spare: 0, Valid: false, VenderIDModel: 0,
                 VenderIDSerial: 0, VendorIDName: "" },
      UserID: 211714360, Valid: true } },
    MetaData: { MMSI: 211714360, time_utc: "2026-08-27 20:00:00.0 +0000 UTC" }
  };
  const fa = uebersetze(teilA).felder;
  assert.strictEqual(fa.name, "BLUEFIN");
  assert.strictEqual(fa.klasse, "B");
  assert.strictEqual(fa.typ, undefined, "kein typ 0 aus einer leeren Haelfte");
  assert.strictEqual(fa.hersteller, undefined);

  const teilB = JSON.parse(JSON.stringify(teilA));
  teilB.Message.StaticDataReport.ReportA = { Name: "", Valid: false };
  teilB.Message.StaticDataReport.ReportB = {
    CallSign: "DK1234", Dimension: { A: 6, B: 4, C: 2, D: 2 }, FixType: 1,
    ShipType: 37, Spare: 0, Valid: true, VenderIDModel: 1,
    VenderIDSerial: 499761, VendorIDName: "SMT" };
  const fb = uebersetze(teilB).felder;
  assert.strictEqual(fb.typ, 37);
  assert.strictEqual(fb.rufzeichen, "DK1234");
  assert.strictEqual(fb.hersteller, "SMT");
  assert.strictEqual(fb.modell, "1");
  assert.strictEqual(fb.seriennr, "499761", "VenderIDSerial, nicht SerialNumber");
  assert.strictEqual(fb.geraet, 1);
  assert.strictEqual(fb.laenge, 10);
});

test("Msg 21: Seezeichen kommen als Seezeichen an, nicht als Schiffe", () => {
  // Der Typcode zaehlt hier eine ANDERE Liste als bei Schiffen: 20 heisst
  // "Leuchttonne", waehrend derselbe Code in Msg 5 ein Segelboot waere. Ihn
  // in `typ` zu legen liesse jede Tonne als Fahrzeug erscheinen.
  const r = uebersetze({
    MessageType: "AidsToNavigationReport",
    Message: { AidsToNavigationReport: {
      Latitude: 54.0, Longitude: 8.0, Name: "ELBE APPROACH LIGHT",
      NameExtension: "BUOY", Type: 20, VirtualAtoN: false, OffPosition: true,
      Fixtype: 1, Dimension: { A: 4, B: 4, C: 3, D: 3 },
      UserID: 992111840, Valid: true } },
    MetaData: { MMSI: 992111840, time_utc: "2026-08-27 21:00:00.0 +0000 UTC" }
  });
  assert.ok(r.hatPosition);
  assert.strictEqual(r.felder.lat, 54.0);
  assert.strictEqual(r.felder.atonTyp, 20);
  assert.strictEqual(r.felder.typ, undefined, "kein Schiffstyp aus einer Tonne");
  // Der Name kann laenger als 20 Zeichen sein - der Rest steht in der
  // Erweiterung und gehoert direkt angehaengt, ohne Leerzeichen.
  assert.strictEqual(r.felder.name, "ELBE APPROACH LIGHTBUOY");
  assert.strictEqual(r.felder.atonVirtuell, 0);
  assert.strictEqual(r.felder.atonAusserPosition, 1);
  assert.strictEqual(r.felder.geraet, 1);
  assert.strictEqual(r.felder.laenge, 8);
  // Die Flagge traegt die Art schon im Binaerrahmen - daraus macht der Client
  // "Seezeichen", ohne auf die Stammdaten zu warten.
  assert.strictEqual(r.felder.flags & 1, 1);
});
