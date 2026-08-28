#!/usr/bin/env bash
# Zeigt, zu welchen Schiffen der Proxy schon einen Zielverlauf mitgeschrieben
# hat - und wie lang er ist.
#
#   ./zielverlauf.sh              Schiffe mit mindestens 2 Zielen, 25 Zeilen
#   ./zielverlauf.sh 4            nur die mit mindestens 4
#   ./zielverlauf.sh 2 100        mehr Zeilen
#   ./zielverlauf.sh 1            auch die mit einem einzigen Ziel
#
# Gelesen wird NUR (readOnly): Der Proxy schreibt nebenher weiter, und im
# WAL-Modus stoert eine zweite lesende Verbindung ihn nicht.
set -euo pipefail

cd "$(dirname "$0")"

MIN="${1:-2}"
ZEILEN="${2:-25}"
if ! [[ "$MIN" =~ ^[0-9]+$ && "$ZEILEN" =~ ^[0-9]+$ ]]; then
  echo "Aufruf: $0 [mindestens-so-viele-Ziele] [Zeilen]" >&2
  exit 2
fi

if ! docker compose ps --status running --services 2>/dev/null | grep -qx proxy; then
  echo "Der Proxy laeuft nicht. Mit 'docker compose up -d' starten." >&2
  exit 1
fi

# < /dev/null, weil docker compose exec sonst den Rest des Skripts verschluckt,
# wenn es ueber eine Pipe laeuft.
docker compose exec -T proxy node -e '
const { DatabaseSync } = require("node:sqlite");
const min = Number(process.argv[1]), zeilen = Number(process.argv[2]);
const d = new DatabaseSync(process.env.AIS_DB || "/daten/ais.db", { readOnly: true });
const da = d.prepare(
  "SELECT name FROM sqlite_master WHERE type=\x27table\x27 AND name=\x27ziel_verlauf\x27").get();
if (!da) {
  console.log("Die Tabelle ziel_verlauf gibt es noch nicht - der Proxy laeuft auf");
  console.log("einem aelteren Stand. Einmal ./update.sh, dann fuellt sie sich.");
  process.exit(0);
}
const ges = d.prepare("SELECT COUNT(DISTINCT mmsi) AS n FROM ziel_verlauf").get().n;
const zeilenDb = d.prepare(
  "SELECT v.mmsi AS mmsi, IFNULL(s.name, \x27\x27) AS name, COUNT(*) AS n," +
  " MIN(v.zuerst) AS von, MAX(v.zuletzt) AS bis" +
  " FROM ziel_verlauf v LEFT JOIN schiff s ON s.mmsi = v.mmsi" +
  " GROUP BY v.mmsi HAVING n >= ? ORDER BY n DESC, bis DESC LIMIT ?").all(min, zeilen);
console.log(ges + " Schiffe haben einen Zielverlauf; " + zeilenDb.length +
  " davon mit mindestens " + min + (min === 1 ? " Ziel:" : " Zielen:"));
console.log("");
const zieleVon = d.prepare("SELECT ziel FROM ziel_verlauf WHERE mmsi = ? ORDER BY folge DESC");
const p = (s, n) => String(s).padEnd(n).slice(0, n);
// Kopfzeile nur, wenn auch etwas darunter steht - eine Tabellenueberschrift
// ueber nichts liest sich wie ein Fehler.
if (zeilenDb.length) {
  console.log(p("MMSI", 11) + p("Name", 22) + p("Ziele", 7) + p("Zeitraum", 10) +
    "juengstes zuerst");
}
for (const z of zeilenDb) {
  const tage = Math.round((z.bis - z.von) / 86400);
  console.log(p(z.mmsi, 11) + p(z.name || "-", 22) + p(z.n, 7) +
    p(tage + (tage === 1 ? " Tag" : " Tage"), 10) +
    zieleVon.all(z.mmsi).map(x => x.ziel).join(", "));
}
if (!ges) {
  console.log("");
  console.log("Noch nichts da. Der Verlauf entsteht erst, wenn ein Schiff sein Ziel");
  console.log("WECHSELT - rueckwirkend gibt es ihn nicht. Rechne in Tagen, nicht Minuten.");
}
' -- "$MIN" "$ZEILEN" < /dev/null
