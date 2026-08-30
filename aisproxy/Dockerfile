# node:sqlite ist in Node 22 eingebaut - deshalb braucht dieses Image weder
# python3 noch make noch g++. Mit better-sqlite3 waere ein zweistufiger Bau
# mit Werkzeugkette noetig gewesen.
FROM node:22-alpine

WORKDIR /app

# Erst die Abhaengigkeiten, dann der Quellcode: So wird die npm-Schicht nur
# neu gebaut, wenn sich package.json wirklich aendert.
COPY package.json package-lock.json* ./
RUN npm install --omit=dev --no-audit --no-fund

COPY src ./src

# Die Kuestenlinie MUSS mit ins Abbild. Ohne sie laeuft der Proxy zwar, aber
# der Landfilter der Schleifenerkennung schaltet sich ab - und dann stehen die
# Faehren wieder auf der Karte, ohne dass irgendwo ein Fehler auftaucht.
# Erzeugt wird sie von werkzeug/kueste-bauen.js, sie liegt im Repo.
#
# NICHT unter daten/: Das steht in .dockerignore (dort liegt die Datenbank),
# und /daten wird zur Laufzeit ohnehin vom Volume ueberdeckt.
COPY karten ./karten

# Historie und Fotos liegen im Volume, nicht im Image.
RUN mkdir -p /daten/fotos && chown -R node:node /daten
ENV AIS_DB=/daten/ais.db AIS_FOTO_VERZEICHNIS=/daten/fotos
VOLUME ["/daten"]

USER node
EXPOSE 8080

# Ein Zustand ohne Schiffe ist kaputt, auch wenn der Prozess laeuft - genau
# der Fall, den eine blosse Portpruefung durchgehen liesse.
#
# Das Token muss mit: /v1/status liegt hinter derselben Pruefung wie jeder
# andere Pfad (server.js, erlaubt()). Ohne Token kam 401 zurueck, der
# Healthcheck war also seit dem Setzen von AIS_ZUGANG dauerhaft rot, ohne dass
# irgendetwas fehlte. Die Variable stammt aus derselben Umgebung, aus der der
# Server sein konfig.ZUGANG bildet - die beiden koennen nicht auseinanderlaufen.
HEALTHCHECK --interval=60s --timeout=10s --start-period=90s --retries=3 \
  CMD node -e "const t=process.env.AIS_ZUGANG||'';fetch('http://127.0.0.1:8080/v1/status'+(t?'?token='+encodeURIComponent(t):'')).then(r=>r.json()).then(s=>process.exit(s.schiffe>0?0:1)).catch(()=>process.exit(1))"

CMD ["node", "src/index.js"]
