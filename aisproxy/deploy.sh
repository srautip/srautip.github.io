#!/usr/bin/env bash
# Einrichtung auf einem frischen Ubuntu-Server (getestet gegen 26.04).
# Als root ausfuehren, einmal:
#
#   curl -fsSL https://raw.githubusercontent.com/srautip/srautip.github.io/main/aisproxy/deploy.sh | bash -s -- ais.example.org
#
# oder nach dem Klonen:  sudo ./deploy.sh ais.example.org
#
# Danach steht der Dienst unter https://<domain>/v1/status.
set -euo pipefail

DOMAIN="${1:-}"
BENUTZER="${AIS_BENUTZER:-skipper}"
ZIEL="/opt/aisproxy"
REPO="${AIS_REPO:-https://github.com/srautip/srautip.github.io.git}"

if [[ -z "$DOMAIN" ]]; then
  echo "Aufruf: $0 <domain>            (z. B. ais.example.org)"
  echo "        $0 localhost           (ohne TLS, nur zum Ausprobieren)"
  exit 1
fi
if [[ $EUID -ne 0 ]]; then echo "Bitte als root ausfuehren (sudo)."; exit 1; fi

# --- Vorabpruefung: zeigt der Name ueberhaupt hierher? -----------------
#
# Das nicht gesetzte DNS ist die haeufigste Stolperfalle. Ohne diese Pruefung
# laeuft die Einrichtung komplett durch, und erst Caddy scheitert danach
# still am Zertifikat - man sucht dann im Proxy statt beim Registrar.
#
# getent statt dig: dig steckt in dnsutils und ist auf einem frischen Ubuntu
# NICHT installiert (nachgesehen). getent gehoert zur glibc und ist immer da.
dns_pruefen() {
  local name="$1"
  [[ "$name" == "localhost" ]] && return 0
  # sslip.io und nip.io loesen bauartbedingt auf die IP im Namen auf.
  [[ "$name" == *.sslip.io || "$name" == *.nip.io ]] && return 0
  [[ "${AIS_DNS_EGAL:-}" == "1" ]] && return 0

  local ziel eigene
  ziel="$(getent ahostsv4 "$name" 2>/dev/null | awk '{print $1}' | sort -u | tr '\n' ' ')"
  if [[ -z "$ziel" ]]; then
    cat <<HINWEIS

Der Name "$name" loest nicht auf.

Fuer automatisches TLS braucht Caddy einen A-Record, der auf diesen Server
zeigt. Beim DNS-Anbieter der Domain anlegen:

    $name.    A    <oeffentliche IP dieses Servers>

Bis zu einer Stunde Wartezeit einplanen, dann diesen Befehl erneut starten.
Ohne eigene Domain geht es auch: <ip-mit-bindestrichen>.sslip.io eintragen,
das loest ohne jede DNS-Arbeit auf die IP im Namen auf.

Pruefung ueberspringen (auf eigene Gefahr): AIS_DNS_EGAL=1 davorsetzen.
HINWEIS
    exit 1
  fi

  eigene="$(hostname -I 2>/dev/null || true)"
  for ip in $ziel; do
    if [[ " $eigene " == *" $ip "* ]]; then
      echo "==> DNS: $name zeigt auf $ip - das ist dieser Server."
      return 0
    fi
  done
  # Kein Treffer heisst nicht zwingend falsch: Hinter NAT ist die oeffentliche
  # IP hier nicht gebunden. Also warnen statt abbrechen.
  echo "==> DNS: $name zeigt auf ${ziel% } - diese Maschine kennt sich unter"
  echo "         ${eigene% }. Falls das derselbe Server hinter NAT ist, ist"
  echo "         alles gut; sonst bekommt Caddy kein Zertifikat."
}
dns_pruefen "$DOMAIN"

echo "==> 1/6 Paketquellen und Grundwerkzeug"
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq ca-certificates curl git ufw >/dev/null

echo "==> 2/6 Docker"
# Das offizielle Bequemlichkeitsskript. Der Umweg ueber die apt-Quelle von
# Docker scheitert bei einer frisch erschienenen Ubuntu-Version regelmaessig
# daran, dass es fuer deren Codenamen noch kein Verzeichnis gibt; das Skript
# faellt in dem Fall selbst auf die vorige Version zurueck.
if ! command -v docker >/dev/null; then
  curl -fsSL https://get.docker.com | sh >/dev/null
fi
systemctl enable --now docker >/dev/null

echo "==> 3/6 Quellcode nach $ZIEL"
if [[ -d "$ZIEL/.git" ]]; then
  git -C "$ZIEL" pull --ff-only
else
  rm -rf "$ZIEL"
  # Nur das Unterverzeichnis holen - das Repo enthaelt mehrere Projekte.
  git clone --depth 1 --filter=blob:none --sparse "$REPO" "$ZIEL.repo"
  git -C "$ZIEL.repo" sparse-checkout set aisproxy
  mv "$ZIEL.repo/aisproxy" "$ZIEL"
  rm -rf "$ZIEL.repo"
fi

echo "==> 4/6 Zugangsdaten"
if [[ ! -f "$ZIEL/.env" ]]; then
  # Ein Passwort, das man nicht selbst erfinden muss - und ein Zugangstoken
  # fuer den Proxy dahinter.
  PASSWORT="$(head -c 18 /dev/urandom | base64 | tr -d '/+=' | head -c 20)"
  ZUGANG="$(head -c 24 /dev/urandom | base64 | tr -d '/+=' | head -c 32)"
  HASH="$(docker run --rm caddy:2-alpine caddy hash-password --plaintext "$PASSWORT" < /dev/null)"
  if [[ -z "$HASH" ]]; then
    echo "    Passwort-Hash konnte nicht erzeugt werden - Caddy bekommt keine Basic-Auth."
    echo "    Von Hand nachholen: docker run --rm caddy:2-alpine caddy hash-password"
  fi
  # Bcrypt-Hashes stecken voller Dollarzeichen ($2a$14$...), und Docker Compose
  # deutet ein $ in der .env als Variable - der Hash kaeme zerstueckelt beim
  # Container an und JEDE Anmeldung schluege fehl, auch die richtige. Genau das
  # ist beim ersten Deployment passiert. Verdoppeln macht daraus ein Literal.
  # ACHTUNG bei der Schreibweise: ${HASH//$/$$} sieht richtig aus, ist es aber
  # nicht - $$ expandiert in bash zur Prozess-ID, und der Hash kaeme als
  # "102942a10294..." heraus. Die Dollarzeichen muessen maskiert werden.
  HASH_ESC="${HASH//\$/\$\$}"
  cat > "$ZIEL/.env" <<EOF
AIS_DOMAIN=$DOMAIN
AIS_BENUTZER=$BENUTZER
AIS_PASSWORT_HASH=$HASH_ESC
AIS_ZUGANG=$ZUGANG
AIS_TOKEN=
EOF
  chmod 600 "$ZIEL/.env"
  cat > "$ZIEL/zugangsdaten.txt" <<EOF
Adresse:   https://$DOMAIN/v1/status
Benutzer:  $BENUTZER
Passwort:  $PASSWORT
Token:     $ZUGANG
EOF
  chmod 600 "$ZIEL/zugangsdaten.txt"
  echo "    Zugangsdaten stehen in $ZIEL/zugangsdaten.txt"
else
  echo "    .env ist schon da, bleibt unangetastet"
fi

echo "==> 5/6 Firewall"
ufw allow OpenSSH >/dev/null
ufw allow 80/tcp >/dev/null
ufw allow 443/tcp >/dev/null
ufw --force enable >/dev/null

echo "==> 6/6 Starten"
cd "$ZIEL"
docker compose up -d --build

echo
echo "Warte auf den ersten Snapshot (bis zu 60 s) …"
for i in $(seq 1 30); do
  sleep 5
  if docker compose exec -T proxy node -e \
      "fetch('http://127.0.0.1:8080/v1/status').then(r=>r.json()).then(s=>{console.log(s.schiffe+' Schiffe, '+(s.strom.verbunden?'Strom steht':'Strom fehlt noch'));process.exit(s.schiffe>0?0:1)}).catch(()=>process.exit(1))" < /dev/null 2>/dev/null; then
    echo
    echo "Fertig. https://$DOMAIN/v1/status"
    [[ -f "$ZIEL/zugangsdaten.txt" ]] && cat "$ZIEL/zugangsdaten.txt"
    exit 0
  fi
done

echo "Der Dienst meldet nach 150 s noch keine Schiffe. Nachsehen mit:"
echo "  cd $ZIEL && docker compose logs --tail=50"
exit 1
