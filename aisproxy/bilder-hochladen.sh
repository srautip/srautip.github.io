#!/usr/bin/env bash
# Einen ganzen Ordner Schiffsbilder in den Proxy laden.
#
#   ./bilder-hochladen.sh <ordner> [https://proxy/v1] [token]
#
# Der Dateiname traegt die Zuordnung: 211224140.jpg, "211224140 nordsee.png",
# foto_211224140_2.jpg - neun Ziffern am Stueck genuegen. Alles andere wird
# uebersprungen und am Ende genannt; stillschweigend etwas Falsches abzulegen
# waere schlimmer als es liegen zu lassen.
#
# Gedacht fuer den Fall "ich habe die Bilder im Browser gespeichert und will
# sie jetzt nicht einzeln anklicken". Der Proxy prueft jede Datei selbst:
# Token, Groessengrenze und die ersten Bytes.
set -euo pipefail

ORDNER="${1:-}"
BASIS="${2:-${AIS_PROXY:-}}"
TOKEN="${3:-${AIS_TOKEN:-}}"

if [[ -z "$ORDNER" || ! -d "$ORDNER" ]]; then
  echo "Aufruf: $0 <ordner> [proxy-basis] [token]"
  echo "  z. B. $0 ~/schiffsbilder https://ais.example.org/v1 geheim"
  echo "  oder:  AIS_PROXY=… AIS_TOKEN=… $0 ~/schiffsbilder"
  exit 1
fi
if [[ -z "$BASIS" ]]; then echo "Keine Proxy-Adresse angegeben."; exit 1; fi
BASIS="${BASIS%/}"

gut=0; schlecht=0; ohne=0
for datei in "$ORDNER"/*.{jpg,jpeg,JPG,JPEG,png,PNG}; do
  [[ -f "$datei" ]] || continue
  name="$(basename "$datei")"
  # Neun Ziffern am Stueck, nicht in eine laengere Zahl eingebettet.
  mmsi="$(sed -E 's/.*(^|[^0-9])([2-7][0-9]{8})([^0-9]|$).*/\2/;t;d' <<< "$name")"
  if [[ -z "$mmsi" ]]; then
    echo "  uebersprungen (keine MMSI im Namen): $name"
    ohne=$((ohne + 1))
    continue
  fi
  ziel="$BASIS/foto/$mmsi"
  [[ -n "$TOKEN" ]] && ziel="$ziel?token=$TOKEN"
  code="$(curl -sS -o /tmp/aisupload.$$ -w '%{http_code}' -X POST \
          --data-binary "@$datei" "$ziel" || echo 000)"
  if [[ "$code" == "200" ]]; then
    echo "  $mmsi  <- $name  ($(du -h "$datei" | cut -f1))"
    gut=$((gut + 1))
  else
    echo "  FEHLER $code bei $name: $(head -c 200 /tmp/aisupload.$$ 2>/dev/null)"
    schlecht=$((schlecht + 1))
  fi
  rm -f /tmp/aisupload.$$
done

echo
echo "$gut geladen, $schlecht fehlgeschlagen, $ohne ohne MMSI im Namen."
[[ $schlecht -eq 0 ]]
