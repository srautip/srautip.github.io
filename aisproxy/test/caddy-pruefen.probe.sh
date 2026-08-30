#!/usr/bin/env bash
# caddy-pruefen.sh gegen gestellte Lagen. Es gibt hier kein Docker, deshalb
# wird "docker" durch ein Skript auf dem PATH ersetzt, das die laufende
# Konfiguration liefert - genau die Stelle, an der die Entscheidung faellt.
#
# Wichtig ist nicht, dass die Pruefung "ja" sagen kann, sondern dass sie
# "nein" sagt, wenn ein Pfad fehlt. Ohne diese Gegenprobe waere sie nur eine
# Zeile, die immer gruen leuchtet.
set -uo pipefail
HIER="$(cd "$(dirname "$0")/.." && pwd)"
ARBEIT="$(mktemp -d)"
trap 'rm -rf "$ARBEIT"' EXIT
n=0; schlecht=0
pruef() {
  n=$((n+1))
  if [[ "$2" == "$3" ]]; then echo "  ok   $1  [$2]"
  else schlecht=$((schlecht+1)); echo "  NEIN $1  [erwartet $3, ist $2]"; fi
}

# Ein Ziel mit echter Caddyfile.
mkdir -p "$ARBEIT/ziel" "$ARBEIT/bin"
cp "$HIER/Caddyfile" "$ARBEIT/ziel/Caddyfile"
echo 'AIS_DOMAIN=beispiel.invalid' > "$ARBEIT/ziel/.env"

# Der gestellte docker-Befehl: Er gibt aus, was in DOCKER_ANTWORT steht.
cat > "$ARBEIT/bin/docker" <<'DOCK'
#!/usr/bin/env bash
if [[ "${DOCKER_ANTWORT:-}" == "LEER" ]]; then exit 1; fi
if [[ "${1:-}" == "compose" && "${2:-}" == "restart" ]]; then
  # Ein Neustart macht die Konfiguration vollstaendig - so verhaelt sich der
  # echte Fall, wenn die eingehaengte Datei stimmt.
  [[ -n "${DOCKER_MARKE:-}" ]] && : > "$DOCKER_MARKE"
  exit 0
fi
cat "${DOCKER_ANTWORT:-/dev/null}"
DOCK
chmod +x "$ARBEIT/bin/docker"
export PATH="$ARBEIT/bin:$PATH"

pfade() { grep -m1 -- '@schnittstelle path ' "$ARBEIT/ziel/Caddyfile" | sed 's/.*@schnittstelle path //'; }

# Lage 1: die laufende Konfiguration kennt ALLE Pfade.
{ printf '{"routes":['; for p in $(pfade); do printf '{"path":["%s"]},' "$p"; done; printf '{}]}'; } \
  > "$ARBEIT/voll.json"
DOCKER_ANTWORT="$ARBEIT/voll.json" AIS_ZIEL="$ARBEIT/ziel" bash "$HIER/caddy-pruefen.sh" > "$ARBEIT/a1.txt" 2>&1
pruef "vollstaendige Konfiguration -> 0" "$?" "0"
grep -q "laeuft mit dieser Datei" "$ARBEIT/a1.txt" && t=ja || t=nein
pruef "und sagt es auch" "$t" "ja"

# Lage 2: /v1/anomalien fehlt - der echte Fall vom 30. Aug. 2026.
{ printf '{"routes":['; for p in $(pfade); do
    [[ "$p" == "/v1/anomalien*" ]] && continue; printf '{"path":["%s"]},' "$p"; done; printf '{}]}'; } \
  > "$ARBEIT/alt.json"
DOCKER_ANTWORT="$ARBEIT/alt.json" AIS_ZIEL="$ARBEIT/ziel" bash "$HIER/caddy-pruefen.sh" > "$ARBEIT/a2.txt" 2>&1
pruef "fehlender Pfad -> 1" "$?" "1"
grep -q "/v1/anomalien\*" "$ARBEIT/a2.txt" && t=ja || t=nein
pruef "und der fehlende Pfad wird BENANNT" "$t" "ja"
grep -q "docker compose restart caddy" "$ARBEIT/a2.txt" && t=ja || t=nein
pruef "und der Weg dorthin genannt" "$t" "ja"

# Lage 3: --heilen startet neu und sieht nach. Der gestellte docker liefert
# nach dem Neustart die vollstaendige Fassung.
cat > "$ARBEIT/bin/docker" <<'DOCK'
#!/usr/bin/env bash
if [[ "${1:-}" == "compose" && "${2:-}" == "restart" ]]; then : > "$DOCKER_MARKE"; exit 0; fi
if [[ -f "${DOCKER_MARKE:-/nirgends}" ]]; then cat "$DOCKER_NACHHER"; else cat "$DOCKER_VORHER"; fi
DOCK
chmod +x "$ARBEIT/bin/docker"
DOCKER_MARKE="$ARBEIT/neugestartet" DOCKER_VORHER="$ARBEIT/alt.json" DOCKER_NACHHER="$ARBEIT/voll.json" \
  AIS_ZIEL="$ARBEIT/ziel" bash "$HIER/caddy-pruefen.sh" --heilen > "$ARBEIT/a3.txt" 2>&1
pruef "--heilen behebt es -> 0" "$?" "0"
[[ -f "$ARBEIT/neugestartet" ]] && t=ja || t=nein
pruef "und hat dafuer wirklich neu gestartet" "$t" "ja"
grep -q "Nach dem Neustart sind alle Pfade da" "$ARBEIT/a3.txt" && t=ja || t=nein
pruef "und danach NACHGESEHEN" "$t" "ja"

# Lage 4: --heilen, aber der Neustart hilft nicht. Muss 1 bleiben und es sagen.
rm -f "$ARBEIT/neugestartet"
DOCKER_MARKE="$ARBEIT/neugestartet" DOCKER_VORHER="$ARBEIT/alt.json" DOCKER_NACHHER="$ARBEIT/alt.json" \
  AIS_ZIEL="$ARBEIT/ziel" bash "$HIER/caddy-pruefen.sh" --heilen > "$ARBEIT/a4.txt" 2>&1
pruef "vergeblicher Neustart -> 1" "$?" "1"
grep -q "Auch nach dem Neustart" "$ARBEIT/a4.txt" && t=ja || t=nein
pruef "und meldet es unmissverstaendlich" "$t" "ja"

# Lage 5: Weder Admin-Schnittstelle noch erreichbare Domain -> "nicht pruefbar",
# und ausdruecklich KEIN Alarm.
cat > "$ARBEIT/bin/docker" <<'DOCK'
#!/usr/bin/env bash
exit 1
DOCK
chmod +x "$ARBEIT/bin/docker"
echo 'AIS_DOMAIN=localhost' > "$ARBEIT/ziel/.env"
AIS_ZIEL="$ARBEIT/ziel" bash "$HIER/caddy-pruefen.sh" > "$ARBEIT/a5.txt" 2>&1
pruef "nichts feststellbar -> 2" "$?" "2"
grep -q "ACHTUNG" "$ARBEIT/a5.txt" && t=ja || t=nein
pruef "und KEIN falscher Alarm" "$t" "nein"

# Lage 6: Kein Caddyfile-Matcher -> 2, kein Absturz.
mkdir -p "$ARBEIT/leer"; echo "nichts" > "$ARBEIT/leer/Caddyfile"
AIS_ZIEL="$ARBEIT/leer" bash "$HIER/caddy-pruefen.sh" > "$ARBEIT/a6.txt" 2>&1
pruef "Caddyfile ohne Matcher -> 2" "$?" "2"

echo
echo "$n Pruefungen, $schlecht fehlgeschlagen"
exit $(( schlecht > 0 ))
