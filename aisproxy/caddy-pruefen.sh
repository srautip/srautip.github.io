#!/usr/bin/env bash
# Laeuft Caddy wirklich mit der Caddyfile, die hier liegt?
#
#   /opt/aisproxy/caddy-pruefen.sh            nur nachsehen
#   /opt/aisproxy/caddy-pruefen.sh --heilen   und bei Abweichung neu starten
#
# Der Anlass, dreimal derselbe: Die Caddyfile ist eingehaengt, nicht ins Abbild
# gebaut - "docker compose up -d --build" tauscht den Container also NICHT aus,
# wenn sich nur diese Datei aendert. Ein neuer Pfad steht danach in der Datei
# und wirkt trotzdem nicht; Caddy verlangt dafuer weiter Basic-Auth, die ein
# Browser-fetch nicht mitschickt, und antwortet mit 401, bevor der Proxy
# ueberhaupt gefragt wird. So ist es /v1/ort ergangen, dann /v1/einstellungen,
# und am 30. Aug. 2026 /v1/anomalien - bei letzterem lief der Detektor im
# Proxy nachweislich (1 505 Schleifen von 471 Schiffen), und die Karte blieb
# trotzdem leer.
#
# test/caddy.test.js vergleicht Matcher und Routen IN DER DATEI. Dieses Skript
# vergleicht die Datei mit dem, was LAEUFT. Beides wird gebraucht.
#
# Rueckgabe: 0 alles da · 1 es fehlt etwas · 2 nicht feststellbar.
set -uo pipefail

ZIEL="${AIS_ZIEL:-/opt/aisproxy}"
HEILEN=0
[[ "${1:-}" == "--heilen" ]] && HEILEN=1

# Die Pfade aus der @schnittstelle-Zeile, einer je Zeile.
caddyPfade() {
  local zeile
  zeile="$(grep -m1 -- '@schnittstelle path ' "$ZIEL/Caddyfile" 2>/dev/null)" || return 1
  zeile="${zeile#*@schnittstelle path }"
  # Wortweise trennen ist hier gewollt.
  # shellcheck disable=SC2086
  printf '%s\n' $zeile
}

# Die laufende Konfiguration. Caddys Admin-Schnittstelle horcht im Container
# auf 2019 und ist von aussen nicht erreichbar - genau der richtige Weg.
caddyLaufend() {
  (cd "$ZIEL" && docker compose exec -T caddy \
     wget -qO- http://localhost:2019/config/ < /dev/null 2>/dev/null) || true
}

# Die Domain aus der .env, fuer den Rueckfall von aussen.
caddyDomain() {
  local w
  w="$(grep -m1 '^AIS_DOMAIN=' "$ZIEL/.env" 2>/dev/null | cut -d= -f2-)" || return 1
  w="${w%\"}"; w="${w#\"}"; w="${w%\'}"; w="${w#\'}"
  [[ -n "$w" && "$w" != "localhost" ]] && printf '%s\n' "$w"
}

# Welche Pfade fehlen in der laufenden Fassung?
# Schreibt die fehlenden auf stdout. 0 alles da · 1 es fehlt etwas · 2 unklar.
caddyFehlend() {
  local laufend fehlt="" p
  laufend="$(caddyLaufend)"
  if [[ -n "$laufend" ]]; then
    while read -r p; do
      [[ -n "$p" ]] || continue
      grep -qF -- "\"$p\"" <<< "$laufend" || fehlt="${fehlt:+$fehlt }$p"
    done < <(caddyPfade)
  else
    # Rueckfall von aussen. Der Unterschied ist am KOPF zu sehen, nicht am
    # Code: Caddys 401 traegt "www-authenticate: Basic", der 401 des Proxys
    # nicht. Diese Probe braucht deshalb KEIN Token, und es steht keins auf
    # einer Befehlszeile.
    local domain erreicht=0 kopf
    domain="$(caddyDomain)" || return 2
    [[ -n "$domain" ]] || return 2
    command -v curl > /dev/null || return 2
    while read -r p; do
      [[ -n "$p" ]] || continue
      kopf="$(curl -sS -m 15 -D - -o /dev/null "https://$domain${p%\*}" 2>/dev/null)" || continue
      erreicht=1
      grep -qi '^www-authenticate: *Basic' <<< "$kopf" && fehlt="${fehlt:+$fehlt }$p"
    done < <(caddyPfade)
    [[ $erreicht -eq 0 ]] && return 2
  fi
  [[ -z "$fehlt" ]] && return 0
  printf '%s\n' "$fehlt"
  return 1
}

if ! caddyPfade > /dev/null 2>&1; then
  echo "    Caddy: keine @schnittstelle-Zeile in $ZIEL/Caddyfile - nicht pruefbar."
  exit 2
fi

rc=0; fehlt="$(caddyFehlend)" || rc=$?
if [[ $rc -eq 0 ]]; then
  echo "    Caddy laeuft mit dieser Datei ($(caddyPfade | grep -c .) Pfade nachgewiesen)"
  exit 0
fi
if [[ $rc -eq 2 ]]; then
  # Kein Alarm: Ein falscher Alarm, der jemanden eine laufende Anlage
  # zurueckbauen laesst, waere schlimmer als die Luecke.
  echo "    Caddy nicht pruefbar (weder Admin-Schnittstelle noch Abruf von aussen)."
  echo "    Im Zweifel: cd $ZIEL && docker compose restart caddy"
  exit 2
fi

echo "    ACHTUNG: Caddy laeuft mit einer ANDEREN Konfiguration."
echo "    In der laufenden Fassung fehlen: $fehlt"
echo "    Der Browser bekommt dafuer 401 (Basic-Auth), der Proxy wird nie gefragt."
if [[ $HEILEN -eq 0 ]]; then
  echo "    Behoben mit: cd $ZIEL && docker compose restart caddy"
  exit 1
fi

# Neu starten und NACHSEHEN. Ein Neustart, dessen Wirkung niemand prueft, ist
# genau der Schritt, der hier schon einmal Erfolg gemeldet hat, ohne einen zu
# haben. Er kostet rund eine Sekunde Aussetzer - waehrend eines Updates, bei
# dem der Proxy ohnehin gerade neu gebaut wurde.
echo "    Caddy wird neu gestartet …"
if ! (cd "$ZIEL" && docker compose restart caddy) > /dev/null 2>&1; then
  echo "    Der Neustart ist gescheitert. Von Hand:"
  echo "        cd $ZIEL && docker compose restart caddy"
  exit 1
fi
sleep 3
rc=0; fehlt="$(caddyFehlend)" || rc=$?
if [[ $rc -eq 0 ]]; then
  echo "    Nach dem Neustart sind alle Pfade da."
  exit 0
fi
if [[ $rc -eq 2 ]]; then
  echo "    Nach dem Neustart nicht mehr pruefbar. Bitte einmal nachsehen:"
  echo "        curl -sD - -o /dev/null https://<domain>/v1/anomalien | grep -i www-auth"
  exit 2
fi
echo "    ACHTUNG: Auch nach dem Neustart fehlen: $fehlt"
echo "    Dann weicht $ZIEL/Caddyfile von dem ab, was eingehaengt ist:"
echo "        docker compose exec caddy grep schnittstelle /etc/caddy/Caddyfile"
exit 1
