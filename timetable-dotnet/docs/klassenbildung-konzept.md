# Konzept: Regelbasierte Klassenbildung mit CP-SAT

## 1. Grundidee

Die Klassenbildung wird als **Zuordnungsproblem mit weichen und harten Regeln** modelliert. Alle pädagogischen Anforderungen werden auf vier Regeltypen abgebildet:

| Regeltyp | Bedeutung | Beispiel |
|---|---|---|
| **Bündelungsgruppe** | Mitglieder sollen in möglichst wenige (ideal: eine) Klassen | Kinder mit Gebärdensprachdolmetscher |
| **Verteilungsgruppe** | Mitglieder sollen gestreut werden, max. *k* pro Klasse | Förderschwerpunkt ESENT, max. 1/Klasse |
| **Balance-Kriterium** | Attribut soll über alle Klassen gleich verteilt sein | Geschlecht, Herkunftsgrundschule |
| **Individualwunsch** | Paar-Beziehung: `zusammen` oder `getrennt` | Kind1 will mit Kind2 in eine Klasse |

Jede Regel ist entweder **hart** (muss gelten, sonst keine Lösung) oder **weich** (Verletzung erlaubt, wird aber mit Strafpunkten bewertet). Die **Priorität** einer weichen Regel bestimmt ihr Gewicht in der Zielfunktion — so werden Regeln gegeneinander priorisiert.

Der Solver liefert immer nur einen **Vorschlag**; die Schulleitung kann einzelne Kinder fixieren und neu rechnen lassen (Human-in-the-Loop, wichtig wegen Art. 22 DSGVO).

---

## 2. Datenmodell

```yaml
schueler:                      # Solver arbeitet nur mit Pseudonym-IDs
  - id: S001
    attribute: {geschlecht: w}  # frei definierbare Tags für Balance-Kriterien

klassen:
  anzahl: 4
  min_groesse: 22
  max_groesse: 26

gruppen:
  - id: G_hoeren
    typ: buendelung
    mitglieder: [S003, S017, S021]
    modus: soft                # hard | soft
    prio: 3                    # Prioritätsstufe (siehe Abschnitt 4)

  - id: G_esent
    typ: verteilung
    mitglieder: [S004, S009, S013]
    max_pro_klasse: 1
    modus: soft
    prio: 3

balance:
  - attribut: geschlecht
    wert: w
    toleranz: 2                # erlaubte Abweichung vom Idealwert je Klasse
    modus: soft
    prio: 2

wuensche:
  - typ: zusammen              # zusammen | getrennt
    kinder: [S001, S002]
    prio: 1
  - typ: getrennt
    kinder: [S004, S009]
    prio: 2

fixierungen:                   # manuelle Vorgaben der Schulleitung
  - kind: S030
    klasse: 2
```

Hinweise:

- **Ein Kind kann in mehreren Gruppen sein** (z. B. Bündelungsgruppe „Hören“ *und* Verteilungsgruppe „ESENT“). Konflikte löst die Priorisierung.
- Attribute sind bewusst generisch (`Tags`): Für Förderbedarfe reicht dem Solver ein Flag bzw. die Gruppenmitgliedschaft — **die Diagnose selbst muss nie ins System** (Datenminimierung, Art. 9 DSGVO).
- „Getrennt“-Wünsche (Konfliktkinder) sind praktisch mindestens so wichtig wie „Zusammen“-Wünsche und im Modell fast identisch — deshalb gleich mit vorgesehen.

---

## 3. CP-SAT-Modell

Notation: `S` = Schülermenge, `C` = Klassenmenge.

### 3.1 Entscheidungsvariablen

```
x[s,c] ∈ {0,1}    Kind s ist in Klasse c
```

**Grundconstraints (immer hart):**

- Jedes Kind genau eine Klasse: `AddExactlyOne(x[s,c] for c in C)`
- Klassengröße: `min_groesse ≤ Σ_s x[s,c] ≤ max_groesse`
- Fixierungen: `x[s, c_fix] = 1`

### 3.2 Bündelungsgruppe G

**Hart:** Gruppenvariable `y[G,c] ∈ {0,1}` mit `Σ_c y[G,c] = 1` und für jedes Mitglied `x[s,c] = y[G,c]` — alle landen zwingend in derselben Klasse.

**Weich:** „Zerstreuung“ messen und bestrafen:

```
used[G,c] ∈ {0,1}                    # Gruppe hat mind. 1 Mitglied in c
x[s,c] = 1  ⇒  used[G,c] = 1         # AddImplication je Mitglied
spread[G] = Σ_c used[G,c]
Strafe: w_G · (spread[G] − 1)        # 0, wenn alle in einer Klasse
```

Die Implikation genügt in eine Richtung, weil die Minimierung `used` von selbst auf 0 drückt, wo kein Mitglied sitzt.

### 3.3 Verteilungsgruppe D (mit Kappe k)

**Hart:** `Σ_{s∈D} x[s,c] ≤ k` für jede Klasse c.

**Weich:** Überlauf-Variable je Klasse:

```
over[D,c] ∈ ℤ≥0
Σ_{s∈D} x[s,c] − k ≤ over[D,c]
Strafe: w_D · Σ_c over[D,c]
```

### 3.4 Balance-Kriterium (z. B. Geschlecht = w)

Sei `A` die Menge der Kinder mit dem Attribut, `target = round(|A| / |C|)`:

```
cnt[c]  = Σ_{s∈A} x[s,c]
diff[c] ∈ [−|A|, |A|],  diff[c] = cnt[c] − target
dev[c]  = |diff[c]|                          # AddAbsEquality
excess[c] ∈ ℤ≥0,  excess[c] ≥ dev[c] − toleranz
Strafe: w_B · Σ_c excess[c]
```

Innerhalb der Toleranz kostet die Abweichung nichts — das hält den Suchraum flexibel. Als harte Variante stattdessen direkt `target−tol ≤ cnt[c] ≤ target+tol` setzen. Dasselbe Muster funktioniert für jedes zählbare Attribut (Herkunftsschule, Ganztagskinder, …) und auch für die Klassengrößen selbst, wenn man sie möglichst gleich haben will.

### 3.5 Individualwünsche (Paar s, t)

```
b[c] ∈ {0,1}  mit  b[c] = x[s,c] AND x[t,c]
   (Linearisierung: b[c] ≤ x[s,c];  b[c] ≤ x[t,c];  b[c] ≥ x[s,c] + x[t,c] − 1)
together = Σ_c b[c]                          # ∈ {0,1} automatisch

typ zusammen:  Strafe w_W · (1 − together)
typ getrennt:  Strafe w_W · together   (oder hart: together = 0)
```

### 3.6 Zielfunktion

```
Minimize  Σ_Regeln  w_Regel · Verletzungsmaß_Regel
```

---

## 4. Priorisierung: zwei Strategien

### Variante A: Gewichtsstufen (einfach, ein Solve)

Prioritätsstufen auf stark gespreizte Gewichte abbilden, z. B.

| Prio | Gewicht |
|---|---|
| 3 (hoch) | 10 000 |
| 2 (mittel) | 100 |
| 1 (niedrig) | 1 |

Die Spreizung muss so groß sein, dass **viele** niedrige Verletzungen nie eine hohe „erkaufen“ können (Faustregel: Faktor > maximale Anzahl möglicher Verletzungen der darunterliegenden Stufe). Vorteil: ein einziger Solver-Lauf, einfach erklärbar.

### Variante B: Lexikographisch (sauber, mehrere Solves)

1. Nur Strafen der Stufe 3 minimieren → Optimum `z3*`.
2. Constraint `Strafen_3 ≤ z3*` fixieren, dann Stufe 2 minimieren.
3. Analog Stufe 1.

Garantiert strikte Prioritätsordnung ohne Gewichts-Tuning. CP-SAT unterstützt das nicht nativ in einem Aufruf, aber sequenzielle Solves mit fixierten Schranken sind unproblematisch (Lösung des Vorlaufs als `AddHint` mitgeben → schnelle Folgeläufe).

**Empfehlung:** Variante A als Default im Produkt, Variante B als „Exakt-Modus“.

---

## 5. Praktische Solver-Themen

**Symmetriebrechung.** Klassen sind austauschbar (Klasse 1 ↔ Klasse 3 ergibt dieselbe Lösung) — das bläht den Suchraum auf. Abhilfe: das Kind mit der kleinsten ID ohne Fixierung in Klasse 0 zwingen und z. B. fordern, dass der kleinste Schülerindex je Klasse aufsteigend ist. Entfällt teilweise, sobald Fixierungen existieren.

**Unlösbarkeit erklären.** Wenn harte Regeln kollidieren (z. B. Bündelung hart + Verteilung hart über dieselben Kinder), liefert CP-SAT `INFEASIBLE`. Jede harte Regel an eine Assumption-Literal koppeln (`AddAssumptions`); im Infeasible-Fall gibt `sufficient_assumptions_for_infeasibility` die widersprüchliche Regelmenge zurück → dem Nutzer anzeigen: *„Regel G_hoeren (hart) und Regel G_esent (hart) sind unvereinbar — eine auf weich stellen.“*

**Erklärbarkeit des Ergebnisses.** Nach dem Solve je weiche Regel den Verletzungswert auslesen und als Report ausgeben („Wunsch S001+S002 erfüllt ✓ / Balance Klasse 3: 2 über Toleranz ✗“). Das ist zugleich die Dokumentation für die menschliche Letztentscheidung (Art. 22).

**Iterativer Workflow.** Schulleitung sieht Vorschlag → verschiebt Kinder manuell (werden zu Fixierungen) → Re-Solve mit alter Lösung als `AddHint`. So bleibt das Tool ein Assistent, keine Blackbox.

**Performance.** Realistische Größen (≤ 150 Kinder, ≤ 6 Klassen, wenige hundert Regeln) löst CP-SAT typischerweise in Sekunden. Trotzdem `max_time_in_seconds` setzen (z. B. 30 s) und die beste gefundene Lösung akzeptieren — Optimalität ist hier kein Muss.

---

## 6. Referenzimplementierung (Python / OR-Tools)

```python
from ortools.sat.python import cp_model

def solve(schueler, n_klassen, min_g, max_g,
          buendelungen, verteilungen, balances, wuensche, fixierungen,
          gewichte={3: 10_000, 2: 100, 1: 1}):
    m = cp_model.CpModel()
    S = [s["id"] for s in schueler]
    C = range(n_klassen)
    x = {(s, c): m.NewBoolVar(f"x_{s}_{c}") for s in S for c in C}

    # Grundconstraints
    for s in S:
        m.AddExactlyOne(x[s, c] for c in C)
    for c in C:
        m.AddLinearConstraint(sum(x[s, c] for s in S), min_g, max_g)
    for f in fixierungen:
        m.Add(x[f["kind"], f["klasse"]] == 1)

    strafen = []  # Liste von (IntVar/LinearExpr, gewicht)

    # Bündelungsgruppen
    for g in buendelungen:
        M = g["mitglieder"]
        if g["modus"] == "hard":
            y = [m.NewBoolVar(f"y_{g['id']}_{c}") for c in C]
            m.AddExactlyOne(y)
            for s in M:
                for c in C:
                    m.Add(x[s, c] == y[c])
        else:
            used = [m.NewBoolVar(f"u_{g['id']}_{c}") for c in C]
            for c in C:
                for s in M:
                    m.AddImplication(x[s, c], used[c])
            spread = sum(used)
            strafen.append((spread - 1, gewichte[g["prio"]]))

    # Verteilungsgruppen
    for d in verteilungen:
        M, k = d["mitglieder"], d["max_pro_klasse"]
        for c in C:
            belegung = sum(x[s, c] for s in M)
            if d["modus"] == "hard":
                m.Add(belegung <= k)
            else:
                over = m.NewIntVar(0, len(M), f"ov_{d['id']}_{c}")
                m.Add(belegung - k <= over)
                strafen.append((over, gewichte[d["prio"]]))

    # Balance-Kriterien
    for b in balances:
        A = [s["id"] for s in schueler
             if s["attribute"].get(b["attribut"]) == b["wert"]]
        target = round(len(A) / n_klassen)
        for c in C:
            cnt = sum(x[s, c] for s in A)
            diff = m.NewIntVar(-len(A), len(A), f"df_{b['attribut']}_{c}")
            m.Add(diff == cnt - target)
            dev = m.NewIntVar(0, len(A), f"dv_{b['attribut']}_{c}")
            m.AddAbsEquality(dev, diff)
            if b["modus"] == "hard":
                m.Add(dev <= b["toleranz"])
            else:
                exc = m.NewIntVar(0, len(A), f"ex_{b['attribut']}_{c}")
                m.Add(exc >= dev - b["toleranz"])
                strafen.append((exc, gewichte[b["prio"]]))

    # Individualwünsche
    for w in wuensche:
        s1, s2 = w["kinder"]
        bs = []
        for c in C:
            bc = m.NewBoolVar(f"b_{s1}_{s2}_{c}")
            m.Add(bc <= x[s1, c]); m.Add(bc <= x[s2, c])
            m.Add(bc >= x[s1, c] + x[s2, c] - 1)
            bs.append(bc)
        together = sum(bs)
        if w["typ"] == "zusammen":
            strafen.append((1 - together, gewichte[w["prio"]]))
        else:  # getrennt
            strafen.append((together, gewichte[w["prio"]]))

    m.Minimize(sum(expr * g for expr, g in strafen))

    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = 30
    solver.parameters.num_workers = 8
    status = solver.Solve(m)

    if status in (cp_model.OPTIMAL, cp_model.FEASIBLE):
        return {s: next(c for c in C if solver.Value(x[s, c])) for s in S}
    return None  # → Infeasibility-Analyse mit Assumptions fahren
```

---

## 7. Erweiterungen (Ausbaustufen)

- **Schulweg-Kriterium** aus dem ursprünglichen Konzept: einfach als Bündelungsgruppen modellieren — pro Haltestelle/Rasterzelle eine (weiche, niedrig priorisierte) Bündelungsgruppe. Damit fügt sich das Wegkriterium ohne neuen Regeltyp ein und die Segregationsproblematik bleibt über die Priorisierung kontrollierbar (Balance-Regeln höher gewichten als Weg-Bündelung!).
- **Wunsch-Ketten** (A→B, B→C): entstehen automatisch, jedes Paar einzeln modellieren genügt; der Solver findet die beste Teilerfüllung.
- **Mehrfachlösungen**: mit `SolutionCallback` mehrere gute Varianten sammeln und der Schulleitung zur Wahl stellen.
- **Stabilität bei Nachzüglern**: bisherige Zuordnung als Hint + Strafterm für jede Änderung („möglichst wenig umsortieren“).

---

## 8. Mehrere Lösungsvarianten in einem Lauf

Ziel: Die Schulleitung soll nicht *eine* Blackbox-Lösung bekommen, sondern **3–5 qualitativ gleichwertige, aber spürbar unterschiedliche Varianten** zur Auswahl.

### 8.1 Verfahren: Optimum + Diversifikations-Schleife

1. **Lauf 1:** Normal optimieren → Optimalwert `z*`, Variante V1.
2. **Qualitätsschranke** einziehen: `Zielfunktion ≤ z* · (1 + ε)` (z. B. ε = 5 %). Dazu die Zielfunktion als eigene `IntVar` führen (`obj == Σ Strafen`, `Minimize(obj)`), damit sie beschränkbar ist.
3. **Diversitäts-Constraint** gegen jede bereits gefundene Variante: mindestens `d` Kinder müssen anders zugeordnet sein.
4. Erneut lösen → V2, V3, … Abbruch, wenn infeasible (es gibt keine weitere hinreichend andere Lösung in der Qualitätstoleranz) oder Variantenzahl erreicht.

Alle Läufe teilen sich dasselbe Modellobjekt; jeder Folgelauf startet mit der Vorlösung als `AddHint` und ist dadurch schnell — für den Nutzer fühlt es sich wie *ein* Lauf an.

```python
def solve_varianten(m, x, obj, S, C, n_var=3, min_dist=8, eps=0.05):
    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = 20
    varianten = []
    if solver.Solve(m) not in (cp_model.OPTIMAL, cp_model.FEASIBLE):
        return varianten
    varianten.append({s: next(c for c in C if solver.Value(x[s, c])) for s in S})
    m.Add(obj <= int(solver.Value(obj) * (1 + eps)))     # Qualitätsschranke
    while len(varianten) < n_var:
        letzte = varianten[-1]
        m.Add(sum(x[s, letzte[s]].Not() for s in S) >= min_dist)  # Diversität
        if solver.Solve(m) not in (cp_model.OPTIMAL, cp_model.FEASIBLE):
            break
        varianten.append({s: next(c for c in C if solver.Value(x[s, c])) for s in S})
    return varianten
```

### 8.2 Zwei Fallstricke

- **Scheinvielfalt durch Klassensymmetrie:** Ohne Symmetriebrechung ist „V2“ womöglich nur V1 mit vertauschten Klassennummern. Entweder Symmetriebrechung aktiv lassen, oder Diversität permutationsinvariant messen: nicht über Klassenlabels, sondern über Paar-Indikatoren *„sitzen s und t zusammen?“* — dann zählt nur die tatsächliche Gruppenzusammensetzung.
- **`enumerate_all_solutions` ist hier das falsche Werkzeug:** Es funktioniert nur ohne Zielfunktion und würde tausende praktisch identische Lösungen liefern. Die Diversifikations-Schleife liefert gezielt wenige, unterschiedliche, gute Varianten.

### 8.3 Darstellung der Varianten

Jede Variante bekommt eine **Scorecard**: Zielwert, erfüllte/verletzte Regeln je Prioritätsstufe, Balance-Kennzahlen je Klasse (z. B. w/m-Verteilung als Balken), Anzahl erfüllter Wünsche. Zusätzlich ein **Diff-Indikator** zwischen den Varianten („V2: 11 Kinder anders als V1, betrifft v. a. Klasse 3“), damit die Auswahl informiert erfolgt — das ist bereits Teil der Art.-22-Dokumentation (Abschnitt 10).

---

## 9. Fixierungen und UI-Interaktion

### 9.1 Fixierungsarten (Taxonomie)

| # | Fixierung | Modell-Umsetzung | Typischer UI-Auslöser |
|---|---|---|---|
| F1 | **Kind → Klasse** | `x[s,c] = 1` | Drag & Drop eines Kindes, danach Pin-Symbol |
| F2 | **Kind ∉ Klasse** | `x[s,c] = 0` | Kontextmenü „nicht in 3b“ (z. B. Lehrkraft-Konflikt) |
| F3 | **Paar hart zusammen/getrennt** | `together = 1` bzw. `= 0` | Wunsch per Klick von *soft* auf *hart* hochstufen |
| F4 | **Gruppenergebnis → Klasse** | `y[G,c] = 1` (bzw. `x[s,c]=1` für alle Mitglieder) | „Dieses Gruppenergebnis übernehmen“ auf der Gruppenkachel |
| F5 | **Gruppe geschlossen, Klasse offen** | Bündelung auf *hart* stellen, Ziel­klasse frei | Lock-Symbol auf der Gruppe |
| F6 | **Klasse einfrieren** | `x[s,c] = 1` für alle aktuellen Mitglieder | Lock auf der Klassenspalte („3a ist fertig“) |
| F7 | **Variante als weiche Basis** | Strafterm `w_stab · Σ_s [x ≠ Variante]` | „Variante übernehmen, behutsam nachbessern“ |
| F8 | **Variante als harte Basis** | alle `x` fixiert außer explizit gelösten | „Nur markierte Kinder neu verteilen“ |

F7 ist im Alltag die wichtigste: Nach jeder Regeländerung (neuer Wunsch trifft ein, Nachzügler) bleibt die Zuordnung *möglichst stabil*, statt komplett umgeworfen zu werden — Eltern wurden ggf. schon informiert.

### 9.2 Interaktionsablauf

**Phase 1 — Regelwerk erfassen.** Gruppen anlegen (Typ, Mitglieder, hart/soft, Prio-Stufe), Balance-Kriterien und Wünsche eintragen. Prioritäten als drei benannte Stufen („kritisch / wichtig / wenn möglich“) statt freier Zahlen — das ist erklärbar und verhindert Gewichts-Wildwuchs.

**Phase 2 — Varianten rechnen und wählen.** Button „Vorschläge berechnen“ → 3 Varianten-Karten mit Scorecards nebeneinander. Auswahl einer Variante macht sie zum **Arbeitsstand** (F7-Modus als Default).

**Phase 3 — Board-Ansicht nachbearbeiten.** Klassen als Spalten, Kinder als Karten; Gruppenzugehörigkeiten und Regelverletzungen als farbige Badges direkt an den Karten. Jede manuelle Verschiebung erzeugt automatisch eine F1-Fixierung (sichtbares Pin-Symbol, jederzeit lösbar). Nach jeder Änderung läuft eine **reine Bewertung** (kein Solve, nur Constraints auszählen — Millisekunden), die Verletzungen live aktualisiert: *„Achtung: damit sind 2 ESENT-Kinder in 2c.“*

**Phase 4 — Neu optimieren.** Button „Neu optimieren (Fixierungen beibehalten)“: Re-Solve mit allen Pins als harten Constraints und dem Arbeitsstand als Hint. Ergebnis ersetzt den Arbeitsstand nicht automatisch, sondern wird als Diff angezeigt („würde 6 Kinder verschieben“) und muss übernommen werden.

**Phase 5 — Konfliktbehandlung.** Ist das Modell durch Fixierungen unlösbar geworden, zeigt der Dialog den per Assumptions ermittelten Konfliktkern in Klartext: *„Pin ‚S014 → 1a‘ + Regel ‚G_esent max. 1/Klasse (hart)‘ + Pin ‚S009 → 1a‘ sind unvereinbar“* — mit direkten Aktionen: Pin lösen, Regel auf soft stellen, Kappe erhöhen.

**Phase 6 — Freigabe.** „Zuordnung freigeben“ ist der Schulleitung (Rollenkonzept!) vorbehalten und erzeugt den Abschlussdatensatz für den Export (→ ASV-BW-Sammelversetzung) sowie das Abschlussprotokoll (Abschnitt 10).

---

## 10. Dokumentation im Sinne von Art. 22 DSGVO

Art. 22 verbietet Entscheidungen mit erheblicher Wirkung, die *ausschließlich* auf automatisierter Verarbeitung beruhen. Die Software muss deshalb nicht nur menschliche Eingriffe *ermöglichen*, sondern **nachweisbar machen, dass eine echte menschliche Prüfung und Letztentscheidung stattgefunden hat**. Das Audit-Log ist genau dieser Nachweis.

### 10.1 Was protokolliert wird

| Ereignis | Inhalt |
|---|---|
| Regelwerk-Stand | Snapshot aller Gruppen/Balances/Wünsche mit Modus + Prio bei jedem Lauf (versioniert) |
| Solver-Lauf | Zeitstempel, Parameter (ε, d, Zeitlimit), Zielwerte aller Varianten, Verletzungsreport je Variante |
| Variantenwahl | Wer hat wann welche Variante als Arbeitsstand übernommen; angezeigte Scorecards |
| Manuelle Eingriffe | Jede Fixierung F1–F8: wer, wann, welches Kind/welche Gruppe, von → nach |
| Konfliktdialoge | Angezeigter Konfliktkern und gewählte Auflösung |
| Freigabe | Benannte Person, Zeitstempel, finaler Verletzungsreport, aktive Bestätigung |

Die Freigabe verlangt eine **aktive Bestätigung mit Substanz**, z. B.: *„Ich habe die verbleibenden 3 Regelabweichungen (Liste) geprüft und entscheide die Klassenzuordnung in eigener Verantwortung.“* Ein bloßer OK-Klick ohne angezeigte Abweichungen wäre als „menschliche Beteiligung“ angreifbar (Stichwort bloßes Durchwinken / rubber-stamping). Dass Varianten verglichen, Kinder manuell verschoben und Läufe wiederholt wurden, belegt zusätzlich faktische Einflussnahme — das Log macht diese Historie vorzeigbar.

### 10.2 Begründungsreport je Kind (Art. 15/Transparenz)

Auf Anfrage der Eltern muss die Schule die Logik der Zuordnung erläutern können. Der Report je Kind listet: zugeordnete Klasse, welche Regeln das Kind betrafen (Gruppenmitgliedschaften nur in zulässiger Granularität, z. B. „Verteilungskriterium“ statt Diagnose), ob manuelle Eingriffe das Kind betrafen, und den Hinweis auf die menschliche Letztentscheidung.

**Wichtige Grenze:** Der Report darf keine Daten Dritter offenlegen — nicht *„S002 wollte nicht mit Ihrem Kind in eine Klasse“* und keine Förderinformationen von Mitschülern. Der Report wird deshalb aus einer gefilterten Sicht generiert, nie aus dem Roh-Log.

### 10.3 Aufbewahrung und Löschung

- Audit-Log und Regelwerk-Snapshots pseudonymisiert speichern; die Auflösungstabelle (Pseudonym → Kind) bleibt bei der Schule.
- Aufbewahrung nur solange Widerspruchs- und Beschwerdewege realistisch offen sind (z. B. bis Ende des ersten Schulhalbjahres), danach Löschung gemäß Löschkonzept; das Abschlussergebnis selbst lebt ohnehin in ASV-BW weiter.
- Geodaten/Wegdaten (falls genutzt) unmittelbar nach Freigabe löschen — sie werden für keinen der Nachweise gebraucht, der Verletzungsreport genügt.

---

## 11. Ampel-Visualisierung der Klassenlisten

Ziel: Auf einen Blick erkennen, **welche Kinder „durch“ sind** (alles erfüllt oder gar nicht von Regeln betroffen) und wo noch Diskussionsbedarf besteht — damit der unkritische Teil schnell fixiert und die Aufmerksamkeit auf den Rest gelenkt werden kann.

### 11.1 Status je Kriterium und Kind

Jedes Kind erhält auf seiner Karte eine Reihe kleiner Status-Chips — **ein Chip pro Kriterium, das dieses Kind betrifft** (Gruppenmitgliedschaften, Wünsche, Balance-Beitrag), in fester Reihenfolge (Bündelung → Verteilung → Balance → Wunsch):

| Farbe | Symbol | Bedeutung (je Kriterium) |
|---|---|---|
| 🟢 Grün | ✓ | Erfüllt und unkritisch |
| 🟡 Gelb | ! | Erfüllt, aber *knapp oder fragil*: Verteilungs-Kappe exakt ausgeschöpft, Balance am Toleranzrand, Wunsch erfüllt, kollidiert aber mit einer anderen Regel — **oder** Kriterium gehört zu einer als *diskussionswürdig* markierten Gruppe |
| 🔴 Rot | ✗ | Weiche Regel verletzt (harte Regeln können in einer gültigen Lösung nie verletzt sein) |
| ⚪ Grau | – | Kind ist von keinem Kriterium betroffen („freies Kind“) |

Symbole zusätzlich zur Farbe sind Pflicht (Farbfehlsichtigkeit). Tippen auf einen Chip zeigt Klartext: *„Verteilung ESENT: 1/1 in dieser Klasse — Kappe voll.“*

**Aggregat je Kind** = Worst-of über alle Chips, dargestellt als Kartenrand bzw. führender Punkt. Damit lässt sich je Klasse sortieren (Rot oben) und global filtern.

### 11.2 Woher die Zustände kommen

Alle Zustände stammen aus dem **Bewertungslauf ohne Optimierung** (Abschnitt 9.2, Phase 3): Constraints werden gegen die aktuelle Zuordnung nur *ausgezählt* — Millisekunden, daher live bei jedem Drag & Drop aktualisierbar. Gelb entsteht aus zwei Quellen: rechnerisch (Kappe/Toleranz exakt erreicht) und redaktionell (Gruppe wurde im Regelwerk als „diskussionswürdig“ geflaggt, z. B. weil sie im letzten Konfliktkern auftauchte — dieses Flag kann das System nach jedem Konfliktdialog automatisch setzen).

### 11.3 Schnell-Fixieren

- **Filter „unkritisch“** = Aggregat Grün oder Grau → Bulk-Aktion *„Angezeigte Kinder fixieren“* (erzeugt F1-Pins mit Herkunft „Bulk“, gesammelt wieder lösbar).
- **Vorsicht Grün ≠ gefahrlos:** Ein grünes Kind kann trotzdem genau das Kind sein, das der Solver verschieben müsste, um ein rotes Problem woanders zu lösen. Zwei Absicherungen:
  1. **Konsens-Fixierung (empfohlen):** Kinder, die in *allen* berechneten Varianten (Abschnitt 8) in derselben Klasse gelandet sind, bilden den stabilen Kern — diese Menge als Standard-Vorschlag für die Bulk-Fixierung anbieten („41 von 96 Kindern sind in allen 3 Varianten identisch zugeordnet — fixieren?“).
  2. **Fixierungs-Probe:** Vor Übernahme der Bulk-Pins einen kurzen Testlauf mit den Pins als Assumptions fahren; verschlechtert sich der Zielwert spürbar oder wird das Modell unlösbar, die verantwortlichen Pins aus dem Konfliktkern benennen und aus der Auswahl nehmen.
- Graue Kinder sind tatsächlich (fast) gefahrlos fixierbar — sie tauchen in keiner Regel auf und beeinflussen nur Klassengröße und ggf. Balance-Zähler.

### 11.4 Nebeneffekt für den Prozess

Jede Bulk-Fixierung verkleinert das Restproblem für Folge-Solves (schneller, stabiler) und strukturiert die Konferenz: Die Diskussion konzentriert sich sichtbar auf die gelb/rot markierten Kinder. Die Bulk-Aktion samt Kinderliste wandert wie jeder Eingriff ins Audit-Log (Abschnitt 10.1) — auch das schnelle Fixieren bleibt damit eine dokumentierte menschliche Entscheidung.

---

## 12. Durchgespielte Beispiele

### 12.1 Beispiel A: Grundschule, Klasse 1, ca. 100 Kinder → 4 Klassen à 25

**Datenlage vor der Einschulung:** Schulanmeldung (inkl. Elternwunsch-Feld „Nennen Sie ein Kind, mit dem Ihr Kind gerne in eine Klasse möchte“), Rückmeldungen der Kooperationslehrkräfte aus den Kitas, Ergebnisse der Einschulungsuntersuchung (Sprachstand), ggf. bereits festgestellte Ansprüche auf sonderpädagogische Bildungsangebote.

**Regelwerk:**

| Regel | Typ | Modus/Prio | Erklärung |
|---|---|---|---|
| Zwillinge Mia & Lena | Bündelung (2 Kinder) *oder* Getrennt-Wunsch | hart | Elternwunsch entscheidet die Richtung; wird fast immer hart umgesetzt, weil die Familie sonst dauerhaft unzufrieden ist |
| Kita „Sonnenblume“, Freundeskreis (4 Kinder) | Bündelung | soft / Prio wichtig | Klassiker der Einschulung: **kein Kind soll ganz allein starten** — pro Kita werden kleine Freundes-Cluster (2–4 Kinder, von den Erzieherinnen benannt) gebündelt. Bei 6 Kitas ergibt das ~10–15 kleine Bündelungsgruppen |
| Wohngebiet „Nordstadt“ (7 Kinder) | Bündelung | soft / Prio wenn möglich | Laufgruppen für den Schulweg (der ursprüngliche Anwendungsfall). Bewusst niedrigste Prio: Weg-Bündelung darf nie soziale Durchmischung schlagen (Segregationsrisiko, Abschnitt 7) |
| Auffälliges Sozialverhalten lt. Kita (5 Kinder) | Verteilung, max. 1–2/Klasse | soft / Prio kritisch | Erfahrungswert: mehrere Kinder mit hohem Unterstützungsbedarf im Sozialverhalten schaukeln sich hoch und binden die Lehrkraft. Bewusst *soft*: bei 5 Kindern auf 4 Klassen geht max. 1/Klasse rechnerisch nicht auf — der Solver findet die beste Annäherung |
| Zwei Kinder mit Schulbegleitung | Bündelung | soft / Prio wichtig | Gegenintuitiv, aber realistisch: Träger können Begleitstunden effizienter stellen, und der Klassenraum wird nur einmal angepasst. Alternativ Verteilung, wenn die Schule Entlastung der Lehrkräfte höher gewichtet — genau diese Abwägung bildet der Regeltyp-Schalter ab |
| Geschlecht (48 w / 52 m) | Balance, Toleranz 2 | soft / Prio wichtig | Ziel ~12 Mädchen/Klasse, 10–14 akzeptiert |
| Sprachförderbedarf Deutsch (16 Kinder) | Balance, Toleranz 1 | soft / Prio wichtig | Ziel 4/Klasse. Als *Balance* statt Verteilung modelliert, weil auch eine Unterschreitung schlecht wäre: Die Förderstunden werden klassenweise organisiert, gleichmäßige Verteilung nutzt sie am besten |
| Kann-Kinder / früh eingeschult (9 Kinder) | Balance, Toleranz 1 | soft / Prio wenn möglich | Altersstruktur je Klasse ähnlich halten |
| 31 Zusammen-Wünsche aus der Anmeldung | Wunsch | soft / Prio wenn möglich | Übliche Zusage der Schule: „*ein* Wunschkind wird nach Möglichkeit berücksichtigt“ — deshalb weich und niedrig priorisiert |
| 3 Getrennt-Hinweise aus den Kitas | Wunsch getrennt | soft / Prio wichtig | Bekannte Konfliktpaare; bewusst höher priorisiert als Zusammen-Wünsche |
| Kind mit Rollstuhl → Klasse 1a | Fixierung (F1) | hart | Einziger barrierefreier Klassenraum liegt im Erdgeschoss — Raum bestimmt Klasse |

**Typischer Ablauf:** Erster Lauf liefert 3 Varianten; ~55 Kinder sind in allen Varianten identisch zugeordnet und werden per Konsens-Fixierung gepinnt. Die Konferenz diskutiert die gelben/roten Karten — typischerweise die Sozialverhalten-Verteilung (5 auf 4 Klassen) und 4–5 unerfüllbare Wünsche, deren Kinder in kollidierenden Bündelungsgruppen stecken. Zwei manuelle Verschiebungen, ein Re-Solve, Freigabe.

### 12.2 Beispiel B: Gemeinschaftsschule/Gesamtschule, Klasse 5, ca. 120 Kinder → 5 Klassen à 24

**Datenlage:** Anmeldebogen (Wunschkinder, Profil-/AG-Wahl), Herkunftsgrundschule, Grundschulempfehlung (an der GMS bewusst heterogen), Übergabegespräche mit den Grundschulen, festgestellte Inklusions-Ansprüche.

**Regelwerk:**

| Regel | Typ | Modus/Prio | Erklärung |
|---|---|---|---|
| Bläserklasse (24 Anmeldungen) | Bündelung → faktisch eine ganze Klasse | hart | Musikprofil mit eigenem Stundenplan (Instrumentalunterricht im Vormittag) — organisatorisch nur als geschlossene Klasse machbar. Praktisch: `y[G_blaeser, 5d] = 1` fixieren |
| Herkunftsgrundschule, kleine Zubringer (je 2–4 Kinder aus 6 Dorfschulen) | Bündelung je Schule | soft / Prio wichtig | Wie bei der Einschulung: niemand startet allein. Kleine Zubringergruppen bleiben zusammen |
| Herkunftsgrundschule, große Zubringer (GS Mitte: 38 Kinder, GS West: 29) | **Verteilung**, max. 9/Klasse | soft / Prio wichtig | Dieselbe Information, umgekehrter Regeltyp: Große Feeder-Schulen müssen *aufgeteilt* werden, sonst reproduziert Klasse 5a einfach die alte 4a und Neuankömmlinge bleiben außen vor. Schönes Beispiel dafür, dass ein Attribut je nach Ausprägung Bündelung *oder* Verteilung sein kann |
| Fahrschüler Buslinie Talheim (11 Kinder) | Bündelung | soft / Prio wenn möglich | Gemeinsamer langer Schulweg, gemeinsame Randstunden erleichtern die Busplanung — wieder bewusst niedrigste Prio |
| Inklusionskinder (6, versch. Förderschwerpunkte) | Verteilung, max. 2/Klasse | soft / Prio kritisch | Sonderpädagogische Stunden und Differenzierungsaufwand verteilen; max. 2 statt 1, weil sich die Stunden des Sonderpädagogen dann noch sinnvoll bündeln lassen — der Kompromiss aus Abschnitt „gemeinsam oder getrennt“ als konkreter Parameter |
| ESENT-Anspruch (3 der 6 Inklusionskinder) | Verteilung, max. 1/Klasse | soft / Prio kritisch | Verschachtelte Gruppe: Die drei ESENT-Kinder unterliegen *zusätzlich* der strengeren Kappe — beide Regeln wirken gleichzeitig auf dieselben Kinder, genau dafür ist die Mehrfach-Mitgliedschaft da |
| Grundschulempfehlung (G: 41 / M: 52 / E: 27) | Balance je Ausprägung, Toleranz 2 | soft / Prio kritisch | Kern der GMS-Idee: jede Klasse ähnlich leistungsheterogen. Drei Balance-Regeln (eine je Ausprägung), Ziel z. B. ~8 G, ~10 M, ~5 E pro Klasse. **Achtung Bläserklasse:** Da 5d hart vorbelegt ist, gilt die Balance real nur über die restlichen 4 Klassen — die Toleranz muss das abfedern, sonst wird das Modell unnötig straff |
| Geschlecht (57 w / 63 m) | Balance, Toleranz 2 | soft / Prio wichtig | Standard |
| Ganztagsanmeldung (44 Kinder) | Balance, Toleranz 3 | soft / Prio wenn möglich | Erleichtert die Nachmittagsorganisation |
| 74 Zusammen-Wünsche, 5 Getrennt-Hinweise | Wunsch | soft / Prio wenn möglich bzw. wichtig | In Klasse 5 besonders zahlreich; viele erfüllen sich „gratis“ über die Grundschul-Bündelungen |

**Typische Konflikte in diesem Szenario:**
- *Bläserklasse × Balance:* Melden sich überproportional viele E-Empfehlungen oder Mädchen zur Bläserklasse an, reißt 5d jede Balance — die harte Profilregel gewinnt, der Verletzungsreport macht das sichtbar und die Schulleitung entscheidet dokumentiert, ob sie das hinnimmt (typisch: ja, mit Vermerk).
- *Große Zubringer × Wünsche:* Kinder der GS Mitte wünschen sich fast nur untereinander — die Verteilungs-Kappe (max. 9) erzwingt, dass ein Teil der Wünsche unerfüllt bleibt. Die Priorisierung (Verteilung *wichtig* > Wünsche *wenn möglich*) löst das automatisch und begründbar: „Wunsch nicht erfüllbar, da sonst mehr als 9 Kinder der GS Mitte in einer Klasse.“ Genau dieser Satz landet im Begründungsreport.

**Größenordnung fürs Modell:** 120 Kinder × 5 Klassen = 600 Boolesche Kernvariablen plus einige hundert Hilfsvariablen — für CP-SAT trivial, Lösungen inkl. 3 Diversifikations-Läufen in wenigen Sekunden.
