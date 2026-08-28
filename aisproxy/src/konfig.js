"use strict";

// Alle Stellschrauben an einem Ort. Jede Zahl, die aus einer Messung stammt,
// traegt sie im Kommentar - sonst weiss beim naechsten Anfassen niemand mehr,
// ob sie begruendet oder geraten war.

function zahl(name, vorgabe) {
  const roh = process.env[name];
  if (roh === undefined || roh === "") return vorgabe;
  const n = Number(roh);
  return Number.isFinite(n) ? n : vorgabe;
}

function text(name, vorgabe) {
  const roh = process.env[name];
  return roh === undefined || roh === "" ? vorgabe : roh;
}

// Die Region: Deutsche Bucht + westliche Ostsee. 21 Quadratgrad, am
// 27. Aug. 2026 gemessene 2915 Schiffe.
//
// Warum nicht groesser: Nordsee + Ostsee waeren 412 sq und 19511 Schiffe -
// 890 KB je Abruf statt 109 KB. Und warum nicht schlauer geschnitten: Ein
// 100-km-Kuestenband spart nur 4,4 %, weil 95,8 % der Schiffe ohnehin darin
// liegen; die Rechteck-Naeherung braeuchte 215 Anfragen je Zyklus fuer 3 %.
const REGION = {
  latMin: zahl("AIS_LAT_MIN", 53.0),
  lonMin: zahl("AIS_LON_MIN", 6.0),
  latMax: zahl("AIS_LAT_MAX", 56.0),
  lonMax: zahl("AIS_LON_MAX", 13.0)
};

const konfig = {
  REGION,

  // --- Upstream ---
  STROM_URL: text("AIS_STROM_URL", "wss://ais.openwaters.io/v0/stream"),
  REST_URL: text("AIS_REST_URL", "https://ais.openwaters.io/v1/vessels"),
  SCHLUESSEL_URL: text("AIS_SCHLUESSEL_URL", "https://ais.openwaters.io/v1/keys"),
  // Leer lassen: Dann holt sich der Proxy beim Start selbst einen Token,
  // genau wie der "Token holen"-Knopf im Client.
  TOKEN: text("AIS_TOKEN", ""),

  // Nachrichtentypen. Die beiden binaeren fehlen bewusst: Gemessen fuehrt der
  // Feed sie nicht (5 Minuten ueber der Rhein-/Maasdelta: 4783 Nachrichten,
  // davon 0 binaere).
  NACHRICHTENTYPEN: [
    "PositionReport",
    "ShipStaticData",
    "StandardClassBPositionReport",
    "StaticDataReport",
    "ExtendedClassBPositionReport",
    // Seezeichen. Gemessen am 27. Aug. 2026: Der Upstream openwaters liefert
    // davon KEINE - weder im Strom (75 s ohne Filter: nur die vier obigen
    // Typen) noch im REST-Abzug (2 762 Datensaetze, alle kind=vessel). Der
    // Typ im Abo schadet trotzdem nicht (gemessen: die Schiffe laufen
    // unveraendert weiter), und wer den Strom auf aisstream.io umstellt
    // (AIS_STROM_URL + AIS_TOKEN), bekommt die Tonnen damit sofort mit.
    "AidsToNavigationReport"
  ],

  // Das Sicherheitsnetz. 109 KB gzip je Abruf, bei 60 s also 0,16 GB/Tag.
  NETZ_MS: zahl("AIS_NETZ_MS", 60000),

  // Der Ratenwaechter. Gemessen laeuft die Region mit 37,6 msg/s gegen ein
  // Limit von 50 - nur 25 % Luft, und der Server drosselt kommentarlos.
  // Ueber dieser Marke wird gewarnt, damit man es merkt, bevor Daten fehlen.
  RATE_LIMIT: zahl("AIS_RATE_LIMIT", 50),
  RATE_WARNUNG: zahl("AIS_RATE_WARNUNG", 42),

  // --- Zustand ---
  // Ohne Meldung faellt ein Schiff nach dieser Zeit aus dem heissen Zustand.
  // 30 Minuten wie im Client, damit beide dasselbe "veraltet" meinen.
  TTL_MS: zahl("AIS_TTL_MS", 30 * 60 * 1000),

  // --- Historie ---
  DB_DATEI: text("AIS_DB", "./daten/ais.db"),
  // Roh vorhalten, danach verdichten.
  ROH_STUNDEN: zahl("AIS_ROH_STUNDEN", 24),
  // Insgesamt vorhalten. Aeltere Tagestabellen werden verworfen.
  HISTORIE_TAGE: zahl("AIS_HISTORIE_TAGE", 7),
  // Verdichtungsraster fuer alles aelter als ROH_STUNDEN.
  VERDICHTUNG_S: zahl("AIS_VERDICHTUNG_S", 60),
  // Ab dieser Fahrt gilt ein Schiff als fahrend. Gemessen sind nur 27 % der
  // Schiffe in Fahrt - die Verdichtung wirft die Liegenden ganz weg.
  FAHRT_KN: zahl("AIS_FAHRT_KN", 0.5),
  // Wie oft Aufraeumen laeuft.
  PFLEGE_MS: zahl("AIS_PFLEGE_MS", 15 * 60 * 1000),
  // Sammelschreiben: Einzelne Inserts waeren Verschwendung, ein zu grosser
  // Puffer verliert bei einem Absturz zu viel.
  SCHREIB_MS: zahl("AIS_SCHREIB_MS", 2000),

  // --- Register ---
  WIKIDATA_URL: text("AIS_WIKIDATA_URL", "https://query.wikidata.org/sparql"),
  DIGITRAFFIC_URL: text("AIS_DIGITRAFFIC_URL", "https://meri.digitraffic.fi/api/ais/v1/vessels"),
  COMMONS_URL: text("AIS_COMMONS_URL", "https://commons.wikimedia.org/w/api.php"),
  // Gemessen: 200 MMSIs in einer VALUES-Abfrage, 0,5 s, 12 % Treffer.
  WIKIDATA_BUENDEL: zahl("AIS_WIKIDATA_BUENDEL", 200),
  // Abstand zwischen Abfragen an die freien Dienste. Sie kosten nichts, also
  // ist Zurueckhaltung das Mindeste.
  REGISTER_PAUSE_MS: zahl("AIS_REGISTER_PAUSE_MS", 1000),
  // Fristen wie im Client: ein Treffer haelt lange, ein Fehltreffer kurz -
  // die IMO kann jede Minute per ShipStaticData eintreffen und die Lage aendern.
  REGISTER_TREFFER_MS: zahl("AIS_REGISTER_TREFFER_MS", 30 * 24 * 3600 * 1000),
  REGISTER_FEHL_MS: zahl("AIS_REGISTER_FEHL_MS", 3 * 24 * 3600 * 1000),
  // Obergrenze fuer ein selbst beigesteuertes Bild. 6 MB nimmt ein Foto aus
  // dem Telefon auf, ist aber klein genug, dass ein Schreibpfad im Netz nicht
  // zur Ablage wird.
  // Die offizielle UN/LOCODE-Liste der UNECE, ueber den Datensatz-Spiegel von
  // Open Knowledge. 7 MB CSV, 116 213 Zeilen - geholt wird sie hoechstens
  // alle 90 Tage, die Liste erscheint zweimal im Jahr.
  ORT_URL: text("AIS_ORT_URL",
    "https://raw.githubusercontent.com/datasets/un-locode/main/data/code-list.csv"),
  ORT_ABZUG_MS: zahl("AIS_ORT_ABZUG_MS", 90 * 24 * 3600 * 1000),
  FOTO_UPLOAD_MAX: zahl("AIS_FOTO_UPLOAD_MAX", 6 * 1024 * 1024),
  FOTO_BREITE: zahl("AIS_FOTO_BREITE", 480),
  FOTO_VERZEICHNIS: text("AIS_FOTO_VERZEICHNIS", "./daten/fotos"),
  REGISTER_AN: text("AIS_REGISTER", "1") !== "0",

  // --- Bilder ---
  // Der vollstaendige Abzug aus Wikidata. Gemessen: 17 144 Zeilen IMO->Bild in
  // einer Abfrage (0,89 MB, 9,8 s) und 8 638 ueber die MMSI. Einmal am Tag
  // reicht - die Zuordnung Kennung->Bild aendert sich in Wochen, nicht in
  // Stunden.
  BILD_ABZUG_MS: zahl("AIS_BILD_ABZUG_MS", 24 * 3600 * 1000),
  // Pause zwischen zwei BILDDOWNLOADS. Vorher gab es keine: Die Fotos liefen
  // innerhalb der Trefferschleife hintereinander weg, und gemessen antwortet
  // Commons dann auf 2 von 25 mit HTTP 429. Jedes verlorene Bild galt danach
  // 30 Tage als "hat kein Bild".
  FOTO_PAUSE_MS: zahl("AIS_FOTO_PAUSE_MS", 1000),
  // Wie lange ein erfolgloser Fotoversuch haelt. Zwei Fristen, wie im Client:
  // Lief der Versuch OHNE IMO, ist er unvollstaendig und wird bald wiederholt -
  // die IMO kann jede Minute per ShipStaticData eintreffen. Mit IMO war es
  // eine vollstaendige Suche, die haelt lange.
  FOTO_TEIL_MS: zahl("AIS_FOTO_TEIL_MS", 60 * 60 * 1000),
  // Wie viele frueherere Ziele je Schiff aufgehoben werden. Mehr liest
  // niemand, und ein Transponder mit wechselndem Freitext fuellte die Tabelle
  // sonst unbegrenzt.
  ZIEL_VERLAUF_MAX: zahl("AIS_ZIEL_VERLAUF_MAX", 12),
  FOTO_FEHL_MS: zahl("AIS_FOTO_FEHL_MS", 7 * 24 * 3600 * 1000),
  // Obergrenze je Lauf. Ohne sie liefe der erste Lauf ueber 2900 Schiffe mal
  // bis zu fuenf Abrufen mal einer Sekunde Pause - Stunden. Was nicht drankam,
  // bleibt faellig und kommt im naechsten Lauf, und der Bericht sagt, wie viel
  // offen blieb.
  FOTO_MAX_PRO_LAUF: zahl("AIS_FOTO_MAX_PRO_LAUF", 300),
  // Flickr ohne Schluessel: der oeffentliche Feed nimmt Tags entgegen.
  // Gemessen an echten Schiffen der Region: "imo<nr>" 9 von 17, "mmsi<nr>"
  // 3 von 25. Der Feed nennt Titel, Autor und Link, aber keine Lizenz.
  FLICKR_URL: text("AIS_FLICKR_URL", "https://api.flickr.com/services/feeds/photos_public.gne"),
  FLICKR_AN: text("AIS_FLICKR", "1") !== "0",

  // --- Server ---
  PORT: zahl("PORT", 8080),
  // Leer = offen. Hinter Caddy mit Basic-Auth ist das in Ordnung; steht der
  // Proxy blank im Netz, gehoert hier ein Wert hinein.
  ZUGANG: text("AIS_ZUGANG", ""),

  // Zugaenge fuer die 3D-Ansicht, hier hinterlegt statt in jedem Browser
  // einzeln. Sie sind KEIN Geheimnis: Der Proxy gibt sie jedem heraus, der
  // das Proxy-Token hat, und das steht auf Entscheidung des Betreibers
  // oeffentlich im Client. Der Gewinn ist eine Stelle zum Wechseln, nicht
  // Geheimhaltung - ein auf Lesezugriff und die benoetigten Assets
  // beschraenkter ion-Token ist deshalb die richtige Wahl.
  ION_TOKEN: text("AIS_ION_TOKEN", ""),
  GOOGLE_KEY: text("AIS_GOOGLE_KEY", ""),
  // Standardtakt der Delta-Auslieferung. Der Client darf ihn beim Abonnieren
  // ueberschreiben.
  TAKT_MS: zahl("AIS_TAKT_MS", 2000),
  TAKT_MIN_MS: zahl("AIS_TAKT_MIN_MS", 500),
  TAKT_MAX_MS: zahl("AIS_TAKT_MAX_MS", 60000)
};

// Flaeche in Quadratgrad - der Wert, an dem das area-Limit des Tokens haengt.
konfig.flaeche = function (box) {
  const b = box || REGION;
  return Math.abs(b.latMax - b.latMin) * Math.abs(b.lonMax - b.lonMin);
};

module.exports = konfig;
