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

# Historie und Fotos liegen im Volume, nicht im Image.
RUN mkdir -p /daten/fotos && chown -R node:node /daten
ENV AIS_DB=/daten/ais.db AIS_FOTO_VERZEICHNIS=/daten/fotos
VOLUME ["/daten"]

USER node
EXPOSE 8080

# Ein Zustand ohne Schiffe ist kaputt, auch wenn der Prozess laeuft - genau
# der Fall, den eine blosse Portpruefung durchgehen liesse.
HEALTHCHECK --interval=60s --timeout=10s --start-period=90s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:8080/v1/status').then(r=>r.json()).then(s=>process.exit(s.schiffe>0?0:1)).catch(()=>process.exit(1))"

CMD ["node", "src/index.js"]
