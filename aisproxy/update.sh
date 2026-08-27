#!/usr/bin/env bash
# Aktualisierung einer laufenden Installation. Als root auf dem Server:
#
#   /opt/aisproxy/update.sh
#
# Was das Skript gegenueber "git pull && docker compose up -d --build" leistet:
#
#   1. Es sichert die Datenbank VORHER - und prueft die Sicherung, statt sie
#      nur zu schreiben. Eine ungeprueft weggelegte Sicherung ist keine.
#      Die Zugangsdaten (.env, zugangsdaten.txt) gehen mit; sie sind ungetrackt
#      und waeren durch nichts wiederherzustellen.
#   2. Es merkt sich den Datenbestand vor dem Umbau und liest ihn danach
#      gegen. Ein stiller Verlust faellt sonst erst Wochen spaeter auf, wenn
#      die Historie gebraucht wird.
#   3. Es merkt sich den alten Stand des Repos und nennt bei jedem Abbruch den
#      Weg zurueck.
#
# Die Historie liegt im Volume "aisproxy_daten" und ueberlebt jeden Neubau des
# Images - solange niemand "docker compose down -v" tippt. Dieses Skript tut
# das nicht und soll es auch nie tun.
set -euo pipefail

# --- Sich selbst aus dem Weg raeumen ----------------------------------------
#
# Dieses Skript liegt IM Repo, das es gleich aktualisiert. Bash liest ein
# Skript haeppchenweise nach, waehrend es laeuft - wenn "git pull" die Datei
# unter dem laufenden Prozess austauscht, springt die Ausfuehrung mitten in
# eine andere Zeile. Deshalb zuerst eine Kopie ausserhalb des Repos und die
# Arbeit von dort. Das Loeschen darf sofort passieren: Bash haelt die Datei
# offen, der Verzeichniseintrag wird nicht mehr gebraucht.
if [[ "${AIS_UPDATE_KOPIE:-}" != "1" ]]; then
  kopie="$(mktemp /tmp/aisproxy-update-XXXXXX.sh)"
  cat "$0" > "$kopie"
  AIS_UPDATE_KOPIE=1 exec bash "$kopie" "$@"
fi
[[ "$0" == /tmp/aisproxy-update-* ]] && rm -f "$0"

ZIEL="${AIS_ZIEL:-/opt/aisproxy}"
SICHERUNGEN="${AIS_SICHERUNGEN:-/opt/aisproxy-sicherungen}"
BEHALTEN="${AIS_BEHALTEN:-7}"
# 36 x 5 s = 180 s. Der Healthcheck im Dockerfile gibt dem Start 90 s, und der
# erste Snapshot braucht auf einer langsamen Maschine laenger.
WARTE_RUNDEN="${AIS_WARTE_RUNDEN:-36}"

# Die beiden Dateien mit den Zugangsdaten. Beide sind ungetrackt - .env steht
# in .gitignore, zugangsdaten.txt schreibt deploy.sh in den Arbeitsbaum. Kein
# git-Befehl holt sie zurueck, deshalb werden sie wie die Datenbank behandelt.
# Ohne .env startet der Stapel zwar, aber mit leerem Token und ohne
# Basic-Auth: Der Client kommt nicht mehr durch, und das sieht nach einem
# kaputten Proxy aus, nicht nach einer fehlenden Datei.
GEHEIM=(.env zugangsdaten.txt)
geheimZiel() {
  case "$1" in
    .env)             echo "$SICHERUNGEN/env-$STEMPEL" ;;
    zugangsdaten.txt) echo "$SICHERUNGEN/zugangsdaten-$STEMPEL.txt" ;;
    *)                echo "$SICHERUNGEN/$1-$STEMPEL" ;;
  esac
}

if [[ $EUID -ne 0 ]]; then echo "Bitte als root ausfuehren (sudo)."; exit 1; fi
if [[ ! -f "$ZIEL/docker-compose.yml" ]]; then
  echo "In $ZIEL steht keine docker-compose.yml."
  echo "Anderes Verzeichnis: AIS_ZIEL=/pfad $0"
  exit 1
fi
cd "$ZIEL"
if ! git rev-parse --git-dir >/dev/null 2>&1; then
  echo "$ZIEL ist kein Git-Arbeitsverzeichnis - siehe DEPLOY.md, Abschnitt"
  echo "\"Wenn dort not a git repository steht\"."
  exit 1
fi

# Die Kennzahlen holt der Container selbst: Auf dem Server steht kein Node,
# und der Proxy haengt bewusst an keinem Host-Port.
#
# Das Token MUSS mit: /v1/status liegt hinter derselben Pruefung wie alle
# anderen Pfade (server.js, erlaubt()). Ohne Token kommt 401 zurueck, und die
# Statusabfrage sah dann 180 s lang aus wie "der Dienst meldet keine Schiffe"
# - genau das ist beim ersten echten Lauf passiert, waehrend der Proxy tadellos
# lief. Gelesen wird es aus der Umgebung DES CONTAINERS: Dort steht dieselbe
# Variable, aus der der Server sein konfig.ZUGANG bildet, es kann also gar
# nicht auseinanderlaufen. Und es steht nirgends auf einer Befehlszeile.
kennzahlen() {
  docker compose exec -T proxy node -e '
    const t = process.env.AIS_ZUGANG || "";
    fetch("http://127.0.0.1:8080/v1/status" +
          (t ? "?token=" + encodeURIComponent(t) : "")).then(r => {
      if (r.status === 401) process.exit(2);
      return r.json();
    }).then(s => {
      if (!s.speicher) process.exit(1);
      console.log([s.speicher.punkte, s.speicher.tage, s.speicher.stammEintraege,
                   s.schiffe].join(" "));
    }).catch(() => process.exit(1))' < /dev/null 2>/dev/null
}

# < /dev/null hinter jedem "docker compose exec": Ohne das schluckt das Kind
# den Rest dieses Skripts, wenn es ueber "curl | bash" laeuft.

echo "==> 1/6 Stand vor dem Umbau"
VOR_HEAD="$(git rev-parse HEAD)"
echo "    Repo: ${VOR_HEAD:0:8} ($(git log -1 --format=%s))"

# Fehlt die .env schon jetzt, wird gar nicht erst angefangen: Sonst sieht es
# hinterher aus, als haette das Update die Zugangsdaten verloren.
if [[ ! -s "$ZIEL/.env" ]]; then
  echo "    In $ZIEL fehlt die .env (oder sie ist leer). Hier wird nichts"
  echo "    angefasst. Eine aeltere Sicherung liegt gegebenenfalls unter"
  echo "    $SICHERUNGEN/env-*, sonst legt ein erneuter Lauf von deploy.sh"
  echo "    neue Zugangsdaten an - dann muss auch der Client sein Token bekommen."
  exit 1
fi
if ! grep -q '^AIS_ZUGANG=.' "$ZIEL/.env"; then
  echo "    Achtung: In der .env steht kein AIS_ZUGANG. Der Proxy laeuft dann"
  echo "    ohne eigenen Tokenschutz. Das Update laeuft weiter, die Datei wird"
  echo "    gesichert wie sie ist."
fi

VOR_PUNKTE=""; VOR_TAGE=""; VOR_STAMM=""; VOR_SCHIFFE=""
if docker compose ps --status running proxy 2>/dev/null | grep -q proxy; then
  rc=0; werte="$(kennzahlen)" || rc=$?
  if [[ $rc -eq 0 ]]; then
    read -r VOR_PUNKTE VOR_TAGE VOR_STAMM VOR_SCHIFFE <<< "$werte"
    echo "    Dienst: $VOR_SCHIFFE Schiffe, $VOR_PUNKTE Positionen in $VOR_TAGE Tagestabellen,"
    echo "            $VOR_STAMM Stammdatensaetze"
  elif [[ $rc -eq 2 ]]; then
    echo "    Dienst laeuft, weist die Statusabfrage aber mit 401 ab. Der Container"
    echo "    kennt AIS_ZUGANG offenbar nicht, der Server verlangt es aber."
    echo "    Nachsehen mit: docker compose config | grep AIS_ZUGANG"
    echo "    Es wird trotzdem gesichert, nur der Gegenvergleich entfaellt."
  else
    echo "    Dienst laeuft, antwortet aber nicht auf /v1/status - es wird trotzdem"
    echo "    gesichert, nur der Gegenvergleich hinterher entfaellt."
  fi
else
  echo "    Dienst laeuft gerade nicht."
fi

echo "==> 2/6 Sicherung"
mkdir -p "$SICHERUNGEN"
chmod 700 "$SICHERUNGEN"
STEMPEL="$(date +%Y%m%d-%H%M%S)"
SICHERUNG="$SICHERUNGEN/ais-$STEMPEL.db"

if docker compose ps --status running proxy 2>/dev/null | grep -q proxy; then
  # VACUUM INTO statt cp. Die Datenbank laeuft im WAL-Modus: Neben ais.db
  # liegt ais.db-wal mit allem, was seit dem letzten Checkpoint geschrieben
  # wurde. Ein blosses cp der Hauptdatei laesst genau diesen Teil liegen -
  # die Sicherung sieht heil aus und ist Stunden alt. VACUUM INTO schreibt
  # aus derselben Sicht wie ein Leser, also einschliesslich WAL.
  # Nachgemessen: 5 000 Zeilen ohne Checkpoint geschrieben, Sicherung liest
  # 5 000 Zeilen zurueck.
  echo "    VACUUM INTO aus der laufenden Datenbank …"
  if ! ergebnis="$(docker compose exec -T proxy node -e '
      const { DatabaseSync } = require("node:sqlite");
      const fs = require("node:fs");
      const quelle = process.env.AIS_DB || "/daten/ais.db";
      const ziel = "/daten/sicherung.db";
      try { fs.unlinkSync(ziel); } catch (e) {}
      const d = new DatabaseSync(quelle);
      d.exec("VACUUM INTO \x27" + ziel + "\x27");
      d.close();
      // Die Sicherung sofort gegenlesen. Eine Datei, die keiner geoeffnet
      // hat, ist keine gepruefte Sicherung.
      const b = new DatabaseSync(ziel);
      const tab = b.prepare("SELECT name FROM sqlite_master WHERE type=\x27table\x27 AND name LIKE \x27pos_%\x27").all();
      let n = 0;
      for (const t of tab) n += b.prepare("SELECT COUNT(*) AS n FROM " + t.name).get().n;
      const s = b.prepare("SELECT COUNT(*) AS n FROM schiff").get().n;
      b.close();
      console.log([n, tab.length, s, fs.statSync(ziel).size].join(" "));
    ' < /dev/null)"; then
    echo "    Die Sicherung ist fehlgeschlagen. Es wird NICHTS aktualisiert."
    exit 1
  fi
  read -r S_PUNKTE S_TAGE S_STAMM S_GROESSE <<< "$ergebnis"
  if ! docker compose cp proxy:/daten/sicherung.db "$SICHERUNG" >/dev/null; then
    echo "    Die Sicherung liess sich nicht aus dem Container holen."
    echo "    Es wird NICHTS aktualisiert."
    exit 1
  fi
  docker compose exec -T proxy node -e \
    'require("node:fs").unlinkSync("/daten/sicherung.db")' < /dev/null || true
  echo "    $SICHERUNG"
  echo "    gegengelesen: $S_PUNKTE Positionen in $S_TAGE Tagestabellen, $S_STAMM Stammdaten"
else
  # Bei stehendem Dienst gibt es keinen Schreiber; dann reicht das Kopieren
  # der Dateien - aber samt -wal und -shm, sonst fehlt derselbe Teil wie oben.
  echo "    Dienst steht - Dateien aus dem Volume kopieren …"
  docker run --rm -v aisproxy_daten:/daten -v "$SICHERUNGEN":/aus alpine:3 \
    sh -c "cp /daten/ais.db /aus/ais-$STEMPEL.db 2>/dev/null &&
           for e in -wal -shm; do
             [ -f /daten/ais.db\$e ] && cp /daten/ais.db\$e /aus/ais-$STEMPEL.db\$e
           done; true" < /dev/null
  S_PUNKTE=""; S_TAGE=""; S_STAMM=""
fi

# Die zwei Fragen, die eine unbrauchbare Sicherung entlarven, ohne SQLite auf
# dem Host: Ist ueberhaupt etwas drin, und ist es eine SQLite-Datei?
if [[ ! -s "$SICHERUNG" ]]; then
  echo "    Die Sicherungsdatei ist leer oder fehlt. Abbruch vor dem Update."
  exit 1
fi
if [[ "$(head -c 15 "$SICHERUNG")" != "SQLite format 3" ]]; then
  echo "    $SICHERUNG traegt keinen SQLite-Kopf. Abbruch vor dem Update."
  exit 1
fi
echo "    $(du -h "$SICHERUNG" | cut -f1), Kopf geprueft"

# Und die Zugangsdaten daneben, mit demselben Zeitstempel. Sie sind winzig,
# aber ohne sie ist die Datenbank wertlos: An den Dienst kaeme niemand mehr
# heran, und ein neues Token muesste in jeden Client nachgetragen werden.
GESICHERT=()
for datei in "${GEHEIM[@]}"; do
  [[ -f "$ZIEL/$datei" ]] || continue
  ziel="$(geheimZiel "$datei")"
  cp -p "$ZIEL/$datei" "$ziel"
  chmod 600 "$ziel"
  GESICHERT+=("$datei")
  echo "    $ziel"
done

echo "==> 3/6 Neuen Stand holen"
if ! git pull --ff-only; then
  echo
  echo "    git pull ist gescheitert. Nichts wurde angefasst, der Dienst laeuft"
  echo "    unveraendert weiter. Bei lokalen Aenderungen: git status ansehen."
  exit 1
fi
NEU_HEAD="$(git rev-parse HEAD)"
if [[ "$NEU_HEAD" == "$VOR_HEAD" ]]; then
  echo "    Schon aktuell (${NEU_HEAD:0:8}) - der Neubau laeuft trotzdem, er ist"
  echo "    dann ein Leerlauf."
else
  echo "    ${VOR_HEAD:0:8} -> ${NEU_HEAD:0:8}"
  git --no-pager log --oneline "$VOR_HEAD..$NEU_HEAD" -- . | sed 's/^/      /'
fi

# Der git pull ist der einzige Schritt, der im Arbeitsbaum etwas anfassen kann
# - also wird hier nachgesehen, noch VOR dem Neubau. Dann liest der neue
# Stapel gleich die richtige .env, und ein Neustart ist nicht noetig.
#
# Wiederhergestellt wird ohne Rueckfrage: Die Kopie ist Minuten alt und
# nachweislich dieselbe Datei. Das ist etwas anderes als ein Rollback der
# Datenbank, bei dem zwischendurch neue Daten hinzugekommen sein koennen.
GEHEIM_HINWEIS="unveraendert"
for datei in "${GESICHERT[@]}"; do
  ziel="$(geheimZiel "$datei")"
  if cmp -s "$ziel" "$ZIEL/$datei"; then continue; fi
  if [[ -f "$ZIEL/$datei" ]]; then
    echo "    ACHTUNG: $datei hat sich beim Aktualisieren geaendert."
    cp -p "$ZIEL/$datei" "$ziel.abweichend"
    echo "    Die vorgefundene Fassung liegt als $ziel.abweichend."
  else
    echo "    ACHTUNG: $datei ist beim Aktualisieren verschwunden."
  fi
  cp -p "$ziel" "$ZIEL/$datei"
  chmod 600 "$ZIEL/$datei"
  echo "    Aus der Sicherung von vorhin wiederhergestellt."
  GEHEIM_HINWEIS="wiederhergestellt"
done

zurueck() {
  cat <<ENDE

Der Weg zurueck:

    cd $ZIEL
    git checkout $VOR_HEAD
    docker compose up -d --build

Und, falls die Daten wirklich Schaden genommen haben:

    docker compose stop proxy
    docker compose cp $SICHERUNG proxy:/daten/ais.db
    docker compose exec proxy sh -c 'rm -f /daten/ais.db-wal /daten/ais.db-shm'
    docker compose start proxy

Die Zugangsdaten von vorhin liegen als:

$(for d in "${GESICHERT[@]}"; do echo "    $(geheimZiel "$d")  ->  $ZIEL/$d"; done)
ENDE
}

echo "==> 4/6 Bauen und starten"
if ! docker compose up -d --build; then
  echo "    Der Neubau ist gescheitert."
  zurueck
  exit 1
fi

echo "==> 5/6 Warten, bis der Dienst wieder Schiffe meldet"
NACH=""; ABGEWIESEN=0
for i in $(seq 1 "$WARTE_RUNDEN"); do
  sleep 5
  rc=0; NACH="$(kennzahlen)" || rc=$?
  if [[ $rc -eq 0 ]]; then
    read -r N_PUNKTE N_TAGE N_STAMM N_SCHIFFE <<< "$NACH"
    if [[ "$N_SCHIFFE" -gt 0 ]]; then
      echo "    nach $((i * 5)) s: $N_SCHIFFE Schiffe"
      break
    fi
  elif [[ $rc -eq 2 ]]; then
    # 401 heisst: Der HTTP-Server antwortet schon, er laesst nur diese Abfrage
    # nicht zu. Daran aendern weitere 175 Sekunden Warten nichts.
    ABGEWIESEN=1; NACH=""; break
  fi
  NACH=""
done
# 401 heisst: Der Dienst antwortet, er laesst nur diese Abfrage nicht zu. Das
# ist ein Fehler der Pruefung und kein Grund, einen Rueckbau nahezulegen -
# sonst baut jemand eine laufende Anlage zurueck, weil das Skript nicht
# hineinsehen konnte.
if [[ -z "$NACH" && $ABGEWIESEN -eq 1 ]]; then
  echo "    Der Dienst antwortet, weist die Statusabfrage aber mit 401 ab."
  echo "    Der Container kennt AIS_ZUGANG nicht, der Server verlangt es."
  echo "    Ob er laeuft, zeigen:  docker compose logs --tail=20 proxy"
  echo "                           curl -u <benutzer>:<passwort> https://<domain>/v1/status?token=<token>"
  echo "    Nachsehen:             docker compose config | grep AIS_ZUGANG"
  echo "    Der Umbau selbst ist durch; nur nachgerechnet wurde nichts."
  exit 1
fi
if [[ -z "$NACH" ]]; then
  echo "    Nach $((WARTE_RUNDEN * 5)) s meldet der Dienst noch keine Schiffe."
  echo "    Protokoll:  cd $ZIEL && docker compose logs --tail=50 proxy"
  zurueck
  exit 1
fi

echo "==> 6/6 Datenbestand gegenlesen"
echo "    $N_PUNKTE Positionen in $N_TAGE Tagestabellen, $N_STAMM Stammdaten"
echo "    Zugangsdaten: ${GESICHERT[*]} $GEHEIM_HINWEIS"
if [[ -n "$VOR_PUNKTE" ]]; then
  if [[ "$N_PUNKTE" -ge "$VOR_PUNKTE" ]]; then
    echo "    vorher $VOR_PUNKTE, jetzt $N_PUNKTE - die Historie ist vollstaendig da."
  elif [[ "$N_TAGE" -lt "$VOR_TAGE" ]]; then
    # Der eine erlaubte Rueckgang: Die Pflege wirft Tagestabellen jenseits von
    # AIS_HISTORIE_TAGE weg. Das ist gewollt und faellt zufaellig manchmal in
    # dasselbe Zeitfenster wie das Update.
    echo "    vorher $VOR_PUNKTE, jetzt $N_PUNKTE - dabei ist eine Tagestabelle"
    echo "    ausgelaufen ($VOR_TAGE -> $N_TAGE Tage). Das ist die normale Pflege,"
    echo "    kein Verlust durch das Update."
  else
    echo
    echo "    ACHTUNG: vorher $VOR_PUNKTE Positionen, jetzt $N_PUNKTE - und die Zahl"
    echo "    der Tagestabellen ist gleich geblieben. Das sieht nach Verlust aus."
    echo "    Die Sicherung von vorhin liegt unter $SICHERUNG."
    zurueck
    exit 1
  fi
fi

# Zum Schluss der Registerstand: Was der letzte Lauf gebracht hat, und wie
# viele Fotos noch offen sind. Der erste Lauf startet 90 s nach dem Start,
# direkt nach dem Update steht hier also meist noch nichts.
docker compose exec -T proxy node -e '
  const t = process.env.AIS_ZUGANG || "";
  fetch("http://127.0.0.1:8080/v1/status" +
        (t ? "?token=" + encodeURIComponent(t) : "")).then(r => r.json()).then(s => {
    const r2 = s.register || {};
    console.log("    Register: " + (r2.laeufe || 0) + " Laeufe, " +
      (r2.fotos || 0) + " Fotos, " + (r2.fotoOffen || 0) + " offen" +
      (r2.wege ? ", Wege " + JSON.stringify(r2.wege) : ""));
  }).catch(() => {})' < /dev/null 2>/dev/null || true

# Alte Sicherungen wegraeumen. Ohne das laeuft die Platte irgendwann voll -
# und ein voller Datentraeger sieht aus wie ein kaputter Proxy. Die
# Zugangsdaten desselben Standes gehen mit: Ein Datenbankstand ohne die
# passenden Zugangsdaten waere nur eine halbe Sicherung, und umgekehrt liegen
# Geheimnisse sonst unbegrenzt lange herum.
ls -1t "$SICHERUNGEN"/ais-*.db 2>/dev/null | tail -n +$((BEHALTEN + 1)) | \
  while read -r alt; do
    stempel="$(basename "$alt" .db)"; stempel="${stempel#ais-}"
    rm -f "$alt" "$alt"-wal "$alt"-shm \
          "$SICHERUNGEN/env-$stempel" "$SICHERUNGEN/env-$stempel.abweichend" \
          "$SICHERUNGEN/zugangsdaten-$stempel.txt" \
          "$SICHERUNGEN/zugangsdaten-$stempel.txt.abweichend"
    echo "    entfernt: Stand $stempel"
  done

echo
echo "Fertig. Sicherung: $SICHERUNG"
for datei in "${GESICHERT[@]}"; do echo "        $(geheimZiel "$datei")"; done
