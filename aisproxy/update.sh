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

# Damit ein Abbruch nie mehr stumm passiert: Jeder unerwartete Fehlschlag
# nennt die Zeile. Die beabsichtigten Ausstiege gehen ueber "exit" und loesen
# das hier nicht aus.
trap 'echo "    Unerwarteter Abbruch in Zeile $LINENO (Exitcode $?). Bitte melden." >&2' ERR

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
[[ "$0" == /tmp/aisproxy-update-* ]] && rm -f "$0" || true

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

laeuft() { docker compose ps --status running proxy 2>/dev/null | grep -q proxy; }

# Alte Staende wegraeumen. Das passiert VOR der neuen Sicherung, nicht danach:
# Erst 146 MB dazuzulegen und dann aufzuraeumen heisst, den Spitzenbedarf um
# einen ganzen Stand hoeher zu legen - auf einer knappen Platte genau der
# Unterschied. Der JUENGSTE Stand bleibt dabei immer stehen, auch wenn die
# Grenze rechnerisch 0 ergaebe: Waehrend des Updates ohne jede Sicherung
# dazustehen ist das eine, was nicht passieren darf.
aufraeumen() {
  local behalten="$1"
  [[ "$behalten" -lt 1 ]] && behalten=1
  ls -1t "$SICHERUNGEN"/ais-*.db 2>/dev/null | tail -n +$((behalten + 1)) | \
    while read -r alt; do
      local stempel
      stempel="$(basename "$alt" .db)"; stempel="${stempel#ais-}"
      rm -f "$alt" "$alt"-wal "$alt"-shm \
            "$SICHERUNGEN/env-$stempel" "$SICHERUNGEN/env-$stempel.abweichend" \
            "$SICHERUNGEN/zugangsdaten-$stempel.txt" \
            "$SICHERUNGEN/zugangsdaten-$stempel.txt.abweichend"
      echo "    entfernt: Stand $stempel"
    done
}

# Freier Platz in KB auf dem Dateisystem, auf dem der Pfad liegt.
#
# Zwei Fallen, beide beim Pruefen aufgetreten: df kennt nur vorhandene
# Verzeichnisse (also erst hocharbeiten), und ein gescheitertes df reisst
# wegen "set -o pipefail" die ganze Zuweisung mit - das Skript starb dann
# mitten im Schritt 2, ohne ein Wort. Deshalb endet die Funktion auf "|| true"
# und liefert im Zweifel eine leere Zeichenkette, die der Aufrufer als
# "unbekannt" behandelt.
freiKB() {
  local pfad="$1"
  while [[ -n "$pfad" && "$pfad" != "/" && ! -d "$pfad" ]]; do pfad="$(dirname "$pfad")"; done
  df -Pk "${pfad:-/}" 2>/dev/null | awk 'NR == 2 { print $4 }' || true
}

# Groesse der Datenbank in Byte - aus dem Container, denn auf dem Host liegt
# sie in einem Docker-Volume. Der laufende Dienst wird gefragt, sonst startet
# "compose run" kurz einen Wegwerf-Container aus DEMSELBEN Image. Kein
# alpine-Abruf: Auf einer vollen Platte scheitert schon der Download, und dann
# steht da eine Fehlermeldung ueber ein fremdes Image statt ueber den Platz.
dbGroesse() {
  local z
  if laeuft; then
    z="$(docker compose exec -T proxy sh -c 'stat -c %s "${AIS_DB:-/daten/ais.db}"' < /dev/null 2>/dev/null | tr -d '\r' | tail -1)"
  else
    z="$(docker compose run --rm --no-deps -T --entrypoint sh proxy \
         -c 'stat -c %s "${AIS_DB:-/daten/ais.db}"' < /dev/null 2>/dev/null | tr -d '\r' | tail -1)"
  fi
  [[ "$z" =~ ^[0-9]+$ ]] && echo "$z" || echo 0
}

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
if laeuft; then
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

# Erst Platz machen: einen Stand weniger behalten, damit der neue hineinpasst.
aufraeumen $((BEHALTEN - 1))

# Und dann nachsehen, ob es reicht. Grund: Am 28. Aug. 2026 stand der Dienst
# mit "disk I/O error" beim Oeffnen der Datenbank - SQLite konnte das WAL
# nicht anlegen. Ein Sicherungsskript, das die Platte vollschreibt, ist das
# Gegenteil dessen, wofuer es gebaut ist.
#
# Zweimal die Groesse: VACUUM INTO legt die Sicherung zuerst IM Volume an
# (Dateisystem von Docker), danach wandert sie nach $SICHERUNGEN. Beide Seiten
# brauchen den Platz, mit einem Aufschlag von 30 % fuer alles andere.
DB_BYTE="$(dbGroesse)"
if [[ "$DB_BYTE" -gt 0 ]]; then
  NOETIG_KB=$(( DB_BYTE / 1024 * 13 / 10 ))
  DOCKER_WURZEL="$(docker info --format '{{.DockerRootDir}}' 2>/dev/null || echo /var/lib/docker)"
  FREI_ZIEL="$(freiKB "$SICHERUNGEN")"
  FREI_DOCKER="$(freiKB "$DOCKER_WURZEL")"
  mb() { [[ -n "$1" ]] && echo "$(( $1 / 1024 )) MB" || echo "unbekannt"; }
  echo "    Datenbank $(( DB_BYTE / 1048576 )) MB; frei: $(mb "$FREI_ZIEL") unter $SICHERUNGEN," \
       "$(mb "$FREI_DOCKER") unter $DOCKER_WURZEL"
  knapp=""
  if [[ -n "$FREI_ZIEL" && "$FREI_ZIEL" -lt "$NOETIG_KB" ]]; then
    knapp="$SICHERUNGEN"
  fi
  if [[ -n "$FREI_DOCKER" && "$FREI_DOCKER" -lt "$NOETIG_KB" ]]; then
    knapp="${knapp:+$knapp und }$DOCKER_WURZEL"
  fi
  if [[ -n "$knapp" ]]; then
    cat <<HINWEIS
    Zu wenig Platz auf $knapp - gebraucht werden rund $(( NOETIG_KB / 1024 )) MB.
    Es wird NICHTS gesichert und NICHTS aktualisiert; der Dienst bleibt, wie er ist.

    Platz schaffen (die juengste Sicherung bleibt absichtlich liegen):
        du -sh $SICHERUNGEN/* | sort -h | tail
        docker system prune -f
        docker image prune -a -f
    Und wenn die Historie der Grund ist, hilft eine kuerzere Aufbewahrung:
        AIS_HISTORIE_TAGE in der docker-compose.yml kleiner setzen.
HINWEIS
    exit 1
  fi
fi

if laeuft; then
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
  # Bei stehendem Dienst gibt es keinen Schreiber; dann reicht das Kopieren
  # der Dateien - aber samt -wal und -shm, sonst fehlt derselbe Teil wie oben.
  #
  # "docker compose cp" arbeitet auch an einem gestoppten Container und
  # braucht damit KEIN fremdes Image. Der frueher hier benutzte alpine-Abruf
  # scheiterte auf einer vollen Platte als Erstes - und die Fehlermeldung
  # handelte dann von einem Image statt vom eigentlichen Problem.
  echo "    Dienst steht - Dateien aus dem Container kopieren …"
  if ! docker compose cp proxy:/daten/ais.db "$SICHERUNG" >/dev/null 2>&1; then
    echo "    Kein Container zum Kopieren - Rueckfall auf ein Hilfsimage."
    docker run --rm -v aisproxy_daten:/daten -v "$SICHERUNGEN":/aus alpine:3 \
      sh -c "cp /daten/ais.db /aus/ais-$STEMPEL.db 2>/dev/null &&
             for e in -wal -shm; do
               [ -f /daten/ais.db\$e ] && cp /daten/ais.db\$e /aus/ais-$STEMPEL.db\$e
             done; true" < /dev/null
  else
    for e in -wal -shm; do
      docker compose cp "proxy:/daten/ais.db$e" "$SICHERUNG$e" >/dev/null 2>&1 || true
    done
  fi
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

# Der Caddyfile ist eingehaengt, nicht ins Abbild gebaut: "up -d --build"
# tauscht den Container also NICHT aus, wenn sich nur diese Datei aendert -
# Caddy laeuft mit der Fassung weiter, die es beim Start gelesen hat. Genau
# das ist einmal teuer geworden: Der Pfad /v1/ort kam neu dazu, stand nach
# einem Update in der Datei, und Caddy verlangte trotzdem weiter Basic-Auth
# dafuer. Ein Neuladen kostet nichts und ist ohne Aenderung wirkungslos.
if docker compose ps --status running --services 2>/dev/null | grep -qx caddy; then
  if docker compose exec -T caddy caddy validate --config /etc/caddy/Caddyfile \
       --adapter caddyfile < /dev/null > /dev/null 2>&1; then
    if docker compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile \
         --adapter caddyfile < /dev/null > /dev/null 2>&1; then
      echo "    Caddy hat seine Konfiguration neu gelesen"
    else
      echo "    ACHTUNG: Caddy hat das Neuladen abgelehnt. Die alte Konfiguration"
      echo "    laeuft weiter - die Seite ist also erreichbar, aber Aenderungen am"
      echo "    Caddyfile wirken nicht. Von Hand: docker compose restart caddy"
    fi
  else
    echo "    ACHTUNG: Der Caddyfile ist fehlerhaft, es wurde NICHT neu geladen."
    echo "    Die alte Konfiguration laeuft weiter. Pruefen mit:"
    echo "    docker compose exec caddy caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile"
  fi

  # Und jetzt nachsehen, ob das Neuladen auch GEWIRKT hat. "reload" meldet
  # Erfolg, wenn der Befehl abgesetzt werden konnte - nicht, dass die neue
  # Fassung greift. Am 30. Aug. 2026 lief ein Update sauber durch, der Proxy
  # rechnete nachweislich (1 505 Schleifen von 471 Schiffen), und
  # /v1/anomalien antwortete dem Browser trotzdem mit 401: Caddy hielt seine
  # alte Konfiguration. Von aussen sah das aus wie "die Funktion gibt es
  # nicht". Dasselbe schon bei /v1/ort und /v1/einstellungen - dreimal
  # derselbe Fehler, und jedes Mal hat ihn ein Mensch gefunden, nicht dieses
  # Skript.
  #
  # --heilen: Bei Abweichung wird der Container neu gestartet UND danach
  # nachgesehen. Das ist die einzige Stelle, an der hier ohne Rueckfrage
  # etwas angefasst wird; sie kostet eine Sekunde Aussetzer waehrend eines
  # Updates, bei dem der Proxy ohnehin gerade neu gebaut wurde.
  if [[ -x "$ZIEL/caddy-pruefen.sh" ]]; then
    AIS_ZIEL="$ZIEL" bash "$ZIEL/caddy-pruefen.sh" --heilen || true
  fi
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

# Zum Schluss der Bilderstand - und zwar der aus der DATENBANK. Die Zaehler
# des Registers leben im Prozess und stehen nach jedem Neustart auf null;
# "Register: 0 Laeufe, 0 Fotos" direkt nach einem Update ist deshalb keine
# Aussage ueber die Fotoarbeit, sondern nur ueber die letzten 20 Sekunden.
# Genau das hat schon einmal wie ein Ausfall ausgesehen.
docker compose exec -T proxy node -e '
  const t = process.env.AIS_ZUGANG || "";
  fetch("http://127.0.0.1:8080/v1/status" +
        (t ? "?token=" + encodeURIComponent(t) : "")).then(r => r.json()).then(s => {
    const sp = s.speicher || {}, r2 = s.register || {};
    console.log("    Bilder: " + (sp.fotos || 0) + " Schiffe haben eines" +
      (sp.fotosEigen ? " (" + sp.fotosEigen + " davon selbst beigesteuert)" : "") +
      ", von " + (sp.stammEintraege || 0) + " im Bestand");
    if (r2.abzug) {
      console.log("    Bildabzug: " + (r2.abzug.imo || 0) + " ueber die IMO, " +
        (r2.abzug.mmsi || 0) + " ueber die MMSI" +
        (r2.abzug.uebersprungen ? " (vorhanden, " + r2.abzug.alterStunden + " h alt)" : ""));
    }
    if (r2.laeufe) {
      console.log("    Register: " + r2.laeufe + " Lauf/Laeufe, " + (r2.fotos || 0) +
        " Fotos geholt, " + (r2.fotoOffen || 0) + " offen");
    } else {
      console.log("    Register: noch kein Lauf in DIESEM Prozess - der erste" +
        " startet 90 s nach dem Start.");
    }
  }).catch(() => {})' < /dev/null 2>/dev/null || true

# Der Hinweis steht hier und NICHT im Node-Schnipsel: ein Befehl mit
# Anfuehrungszeichen, in einer einfach zitierten Zeichenkette, in einem
# einfach zitierten node -e - genau die Stelle, an der es beim Schreiben
# schon einmal auseinandergeflogen ist. Ein Hierdokument hat dieses Problem
# nicht.
cat <<'HINWEIS'
    Registerstand spaeter nachsehen:
      cd /opt/aisproxy && docker compose exec -T proxy node -e '
        const t = process.env.AIS_ZUGANG || "";
        fetch("http://127.0.0.1:8080/v1/status" + (t ? "?token=" + t : ""))
          .then(r => r.json())
          .then(s => console.log(JSON.stringify(s.register, null, 1)))' < /dev/null
HINWEIS

# Aufgeraeumt wurde schon vor der Sicherung (Schritt 2). Hier steht bewusst
# nichts mehr: Ein zweiter Durchgang koennte nur den gerade erzeugten Stand
# treffen - und der ist der einzige, der zu diesem Update gehoert.

echo
echo "Fertig. Sicherung: $SICHERUNG"
for datei in "${GESICHERT[@]}"; do echo "        $(geheimZiel "$datei")"; done
