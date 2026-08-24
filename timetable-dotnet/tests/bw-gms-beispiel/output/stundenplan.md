# Stundenplan: bw-gms-beispiel (realitaetsnah, ~700 Schueler, 4-zuegig)

**Status:** SolveTop (TimeLimitReached)  |  **CP-SAT-Status:** Feasible  |  **Kann-Verstoesse:** 0  |  **Qualitaet (Total):** 19768.5  |  **Verstoesse:** 0

*Phase 2.25: die Stagnationserkennung hat 16 von 19 Solve-Iteration(en) vorzeitig abgebrochen, weil ueber `stagnation_timeout_s` hinweg keine Verbesserung mehr gefunden wurde - spart Zeit fuer weitere Iterationen statt eine stehende Suche bis zum Zeitlimit weiterlaufen zu lassen.*

*Hinweis: `Feasible` statt `Optimal` bedeutet, CP-SAT konnte innerhalb von `solve_time_limit_s` (aktuell 1200s) keinen Optimalitaetsbeweis erbringen - ein hoeherer Wert in `config.yaml` kann helfen, ein noch besseres Ergebnis zu finden oder das aktuelle als optimal zu beweisen.*
*Zusaetzlich begrenzt `per_solve_time_limit_s` (aktuell 120s) jede EINZELNE Solve-Iteration - ein hoeherer Wert kann derselben Iteration mehr Zeit fuer einen Optimalitaetsbeweis geben, auf Kosten weniger Iterationen fuer zusaetzliche `max_solutions`-Alternativen innerhalb desselben Gesamtbudgets.*

## Optimalitaets-Luecke

Gefundene Loesung (Objective): **78.0**  |  Bewiesene untere Schranke: **0.0**  |  Maximal noch moegliche Verbesserung: **100.0%**

*Diese Luecke ist eine bewiesene OBERGRENZE, keine Vorhersage - die tatsaechlich erreichbare Verbesserung kann kleiner sein (bis hin zu 0, falls die gefundene Loesung bereits optimal ist, CP-SAT das aber innerhalb der Zeit nicht beweisen konnte).*

**Verlauf dieses Solve-Versuchs** (jede gefundene Verbesserung, nicht in festen Zeitabstaenden):

| Zeit (s) | Objective |
|---|---|
| 6.9 | 100.0 |
| 13.6 | 99.0 |
| 14.0 | 98.0 |
| 14.0 | 97.0 |
| 20.2 | 96.0 |
| 20.6 | 95.0 |
| 21.4 | 94.0 |
| 23.3 | 93.0 |
| 25.4 | 92.0 |
| 25.8 | 91.0 |
| 26.1 | 88.0 |
| 27.1 | 87.0 |
| 27.2 | 86.0 |
| 27.7 | 85.0 |
| 29.3 | 84.0 |
| 29.6 | 83.0 |
| 30.2 | 82.0 |
| 36.7 | 81.0 |
| 37.9 | 80.0 |
| 41.4 | 79.0 |
| 49.1 | 78.0 |

*Letzte Verbesserung bei 49.1s - fand danach bis zum Abbruch keine weitere statt. Ein deutlich frueherer letzter Eintrag als das Zeitbudget legt nahe, dass zusaetzliche Zeit fuer DIESEN Versuch wenig bringen wuerde.*

## Klassen

### 5a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | Mathematik (Mathematiklehrer-1) | - | Mathematik (Mathematiklehrer-1) |
| 2 | Englisch (Englischlehrer-4) | Englisch (Englischlehrer-4) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Englisch (Englischlehrer-4) | Englisch (Englischlehrer-4) |
| 3 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | BNT (NaWi-Lehrer-4) [NaWi3] | Deutsch (Deutschlehrer-6) | Sport (Sportlehrer-3) [Sporthalle3] | Geschichte (GesWiss-Lehrer-5) |
| 4 | Geschichte (GesWiss-Lehrer-5) | BNT (NaWi-Lehrer-4) [NaWi3] | Sport (Sportlehrer-3) [Sporthalle3] | Mathematik (Mathematiklehrer-1) | Geographie (GesWiss-Lehrer-1) |
| 5 | Deutsch (Deutschlehrer-6) | Deutsch (Deutschlehrer-6) | BNT (NaWi-Lehrer-4) [NaWi3] | Deutsch (Deutschlehrer-6) | Mathematik (Mathematiklehrer-1) |
| 6 | Deutsch (Deutschlehrer-6) | Mathematik (Mathematiklehrer-1) | Musik (Musiklehrer-2) [Musikraum2] | Geographie (GesWiss-Lehrer-1) | Sport (Sportlehrer-3) [Sporthalle3] |
| 7 | - | - | - | Kunst (Kunstlehrer-2) [Kunstraum2] | - |
| 8 | - | - | - | - | - |

### 5b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Deutsch (Deutschlehrer-2) | - | Sport (Sportlehrer-3) [Sporthalle3] | - | BNT (NaWi-Lehrer-2) [NaWi3] |
| 2 | Kunst (Kunstlehrer-1) [Kunstraum2] | Mathematik (Mathematiklehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Deutsch (Deutschlehrer-2) | Geschichte (GesWiss-Lehrer-5) |
| 3 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Deutsch (Deutschlehrer-2) | BNT (NaWi-Lehrer-2) [NaWi3] | Geschichte (GesWiss-Lehrer-5) | Geographie (GesWiss-Lehrer-3) |
| 4 | Mathematik (Mathematiklehrer-2) | Sport (Sportlehrer-3) [Sporthalle3] | Geographie (GesWiss-Lehrer-3) | Deutsch (Deutschlehrer-2) | Englisch (Englischlehrer-1) |
| 5 | Englisch (Englischlehrer-1) | Deutsch (Deutschlehrer-2) | BNT (NaWi-Lehrer-2) [NaWi3] | Mathematik (Mathematiklehrer-2) | Sport (Sportlehrer-3) [Sporthalle3] |
| 6 | Englisch (Englischlehrer-1) | Musik (Musiklehrer-2) [Musikraum2] | Englisch (Englischlehrer-1) | Mathematik (Mathematiklehrer-2) | Mathematik (Mathematiklehrer-2) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 5c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik (Mathematiklehrer-2) | - | Kunst (Kunstlehrer-1) [Kunstraum2] | - | Deutsch (Deutschlehrer-3) |
| 2 | Geographie (GesWiss-Lehrer-5) | BNT (NaWi-Lehrer-4) [NaWi3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Englisch (Englischlehrer-1) | Englisch (Englischlehrer-1) |
| 3 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Geschichte (GesWiss-Lehrer-3) | BNT (NaWi-Lehrer-4) [NaWi3] | Mathematik (Mathematiklehrer-2) | Mathematik (Mathematiklehrer-2) |
| 4 | Sport (Sportlehrer-3) [Sporthalle3] | Mathematik (Mathematiklehrer-2) | Musik (Musiklehrer-1) [Musikraum2] | Geographie (GesWiss-Lehrer-5) | Deutsch (Deutschlehrer-3) |
| 5 | Deutsch (Deutschlehrer-3) | Englisch (Englischlehrer-1) | Sport (Sportlehrer-3) [Sporthalle3] | Deutsch (Deutschlehrer-3) | Mathematik (Mathematiklehrer-2) |
| 6 | BNT (NaWi-Lehrer-4) [NaWi3] | Englisch (Englischlehrer-1) | Deutsch (Deutschlehrer-3) | Sport (Sportlehrer-3) [Sporthalle3] | Geschichte (GesWiss-Lehrer-3) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 5d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Englisch (Englischlehrer-5) | Sport (Sportlehrer-2) [Sporthalle3] | BNT (NaWi-Lehrer-2) [NaWi3] | - | Deutsch (Deutschlehrer-2) |
| 2 | Deutsch (Deutschlehrer-2) | Englisch (Englischlehrer-5) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | - | BNT (NaWi-Lehrer-2) [NaWi3] |
| 3 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Mathematik (Mathematiklehrer-4) | Mathematik (Mathematiklehrer-4) | Mathematik (Mathematiklehrer-4) | Deutsch (Deutschlehrer-2) |
| 4 | Kunst (Kunstlehrer-1) [Kunstraum2] | Musik (Musiklehrer-1) [Musikraum2] | Deutsch (Deutschlehrer-2) | Sport (Sportlehrer-2) [Sporthalle3] | Geschichte (GesWiss-Lehrer-5) |
| 5 | BNT (NaWi-Lehrer-2) [NaWi3] | Mathematik (Mathematiklehrer-4) | Englisch (Englischlehrer-5) | Geographie (GesWiss-Lehrer-3) | Mathematik (Mathematiklehrer-4) |
| 6 | Deutsch (Deutschlehrer-2) | Geographie (GesWiss-Lehrer-3) | Englisch (Englischlehrer-5) | Geschichte (GesWiss-Lehrer-5) | Sport (Sportlehrer-2) [Sporthalle3] |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 6a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Mathematik (Mathematiklehrer-2) | Geschichte (GesWiss-Lehrer-4) | Sport (Sportlehrer-3) [Sporthalle3] | Deutsch (Deutschlehrer-4) |
| 2 | Sport (Sportlehrer-3) [Sporthalle3] | Englisch (Englischlehrer-1) | Mathematik (Mathematiklehrer-2) | Geographie (GesWiss-Lehrer-4) | Deutsch (Deutschlehrer-4) |
| 3 | Deutsch (Deutschlehrer-4) | BNT (NaWi-Lehrer-2) [NaWi3] | Deutsch (Deutschlehrer-4) | Geschichte (GesWiss-Lehrer-4) | Sport (Sportlehrer-3) [Sporthalle3] |
| 4 | Englisch (Englischlehrer-1) | Englisch (Englischlehrer-1) | BNT (NaWi-Lehrer-2) [NaWi3] | Mathematik (Mathematiklehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-2) |
| 5 | Mathematik (Mathematiklehrer-2) | Mathematik (Mathematiklehrer-2) | Kunst (Kunstlehrer-1) [Kunstraum2] | Englisch (Englischlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-2 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 6 | Geographie (GesWiss-Lehrer-4) | Deutsch (Deutschlehrer-4) | BNT (NaWi-Lehrer-2) [NaWi3] | BNT (NaWi-Lehrer-2) [NaWi3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-2 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 7 | - | - | - | Musik (Musiklehrer-1) [Musikraum2] | - |
| 8 | - | - | - | - | - |

### 6b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | BNT (NaWi-Lehrer-2) [NaWi3] | Mathematik (Mathematiklehrer-2) | Kunst (Kunstlehrer-1) [Kunstraum2] | Englisch (Englischlehrer-1) |
| 2 | Mathematik (Mathematiklehrer-2) | Deutsch (Deutschlehrer-2) | BNT (NaWi-Lehrer-2) [NaWi3] | Sport (Sportlehrer-2) [Sporthalle3] | Sport (Sportlehrer-2) [Sporthalle3] |
| 3 | Mathematik (Mathematiklehrer-2) | Mathematik (Mathematiklehrer-2) | Geographie (GesWiss-Lehrer-3) | Geschichte (GesWiss-Lehrer-3) | BNT (NaWi-Lehrer-2) [NaWi3] |
| 4 | Deutsch (Deutschlehrer-2) | Geographie (GesWiss-Lehrer-3) | Englisch (Englischlehrer-1) | Englisch (Englischlehrer-1) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-2) |
| 5 | Deutsch (Deutschlehrer-2) | Geschichte (GesWiss-Lehrer-3) | Sport (Sportlehrer-2) [Sporthalle3] | Musik (Musiklehrer-1) [Musikraum2] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-2 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 6 | BNT (NaWi-Lehrer-2) [NaWi3] | Mathematik (Mathematiklehrer-2) | Deutsch (Deutschlehrer-2) | Englisch (Englischlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-2 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 7 | - | - | Deutsch (Deutschlehrer-2) | - | - |
| 8 | - | - | - | - | - |

### 6c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Mathematik (Mathematiklehrer-1) | BNT (NaWi-Lehrer-3) [NaWi3] | Mathematik (Mathematiklehrer-1) | Geschichte (GesWiss-Lehrer-3) |
| 2 | Deutsch (Deutschlehrer-1) | Englisch (Englischlehrer-2) | BNT (NaWi-Lehrer-3) [NaWi3] | Englisch (Englischlehrer-2) | Englisch (Englischlehrer-2) |
| 3 | Musik (Musiklehrer-1) [Musikraum2] | Deutsch (Deutschlehrer-1) | Mathematik (Mathematiklehrer-1) | Deutsch (Deutschlehrer-1) | BNT (NaWi-Lehrer-3) [NaWi3] |
| 4 | Geographie (GesWiss-Lehrer-3) | Deutsch (Deutschlehrer-1) | Kunst (Kunstlehrer-1) [Kunstraum2] | Geschichte (GesWiss-Lehrer-3) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-2) |
| 5 | Mathematik (Mathematiklehrer-1) | Englisch (Englischlehrer-2) | Geographie (GesWiss-Lehrer-3) | Deutsch (Deutschlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-2 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 6 | BNT (NaWi-Lehrer-3) [NaWi3] | Sport (Sportlehrer-2) [Sporthalle3] | Mathematik (Mathematiklehrer-1) | Sport (Sportlehrer-2) [Sporthalle3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-2 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 7 | - | - | Sport (Sportlehrer-2) [Sporthalle3] | - | - |
| 8 | - | - | - | - | - |

### 6d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Englisch (Englischlehrer-3) | Mathematik (Mathematiklehrer-3) | Englisch (Englischlehrer-3) | BNT (NaWi-Lehrer-3) [NaWi3] |
| 2 | Geschichte (GesWiss-Lehrer-3) | Englisch (Englischlehrer-3) | Deutsch (Deutschlehrer-2) | BNT (NaWi-Lehrer-3) [NaWi3] | BNT (NaWi-Lehrer-3) [NaWi3] |
| 3 | Deutsch (Deutschlehrer-2) | Mathematik (Mathematiklehrer-3) | Kunst (Kunstlehrer-1) [Kunstraum2] | Deutsch (Deutschlehrer-2) | Mathematik (Mathematiklehrer-3) |
| 4 | Sport (Sportlehrer-1) [Sporthalle3] | Deutsch (Deutschlehrer-2) | Mathematik (Mathematiklehrer-3) | Geographie (GesWiss-Lehrer-4) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-2) |
| 5 | BNT (NaWi-Lehrer-3) [NaWi3] | Mathematik (Mathematiklehrer-3) | Sport (Sportlehrer-1) [Sporthalle3] | Deutsch (Deutschlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-2 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 6 | Englisch (Englischlehrer-3) | Sport (Sportlehrer-1) [Sporthalle3] | Geographie (GesWiss-Lehrer-4) | Geschichte (GesWiss-Lehrer-3) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-2 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 7 | - | - | Musik (Musiklehrer-1) [Musikraum2] | - | - |
| 8 | - | - | - | - | - |

### 7a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Kunst (Kunstlehrer-1) [Kunstraum2] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | - |
| 2 | Biologie (NaWi-Lehrer-2) [Bio2] | Biologie (NaWi-Lehrer-2) [Bio2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Sport (Sportlehrer-1) [Sporthalle3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) |
| 3 | Sport (Sportlehrer-1) [Sporthalle3] | Geographie (GesWiss-Lehrer-1) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Gemeinschaftskunde (GesWiss-Lehrer-1) |
| 4 | Gemeinschaftskunde (GesWiss-Lehrer-1) | Physik (NaWi-Lehrer-2) [NaWi3] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) |
| 5 | Musik (Musiklehrer-1) [Musikraum2] | Physik (NaWi-Lehrer-2) [NaWi3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | Sport (Sportlehrer-1) [Sporthalle3] |
| 6 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | Geschichte (GesWiss-Lehrer-1) |
| 7 | - | - | - | Geographie (GesWiss-Lehrer-1) | - |
| 8 | - | - | - | Geschichte (GesWiss-Lehrer-1) | - |

### 7b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Biologie (NaWi-Lehrer-4) [Bio2] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | Musik (Musiklehrer-1) [Musikraum2] |
| 2 | Geschichte (GesWiss-Lehrer-4) | Geschichte (GesWiss-Lehrer-4) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Sport (Sportlehrer-3) [Sporthalle3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) |
| 3 | Sport (Sportlehrer-3) [Sporthalle3] | Physik (NaWi-Lehrer-3) [NaWi3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Kunst (Kunstlehrer-1) [Kunstraum2] |
| 4 | Biologie (NaWi-Lehrer-4) [Bio2] | Physik (NaWi-Lehrer-3) [NaWi3] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) |
| 5 | Gemeinschaftskunde (GesWiss-Lehrer-4) | Sport (Sportlehrer-3) [Sporthalle3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | Gemeinschaftskunde (GesWiss-Lehrer-4) |
| 6 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | Geographie (GesWiss-Lehrer-4) |
| 7 | - | - | Geographie (GesWiss-Lehrer-4) | - | - |
| 8 | - | - | - | - | - |

### 7c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Gemeinschaftskunde (GesWiss-Lehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | Geschichte (GesWiss-Lehrer-1) |
| 2 | Musik (Musiklehrer-1) [Musikraum2] | Geschichte (GesWiss-Lehrer-1) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Kunst (Kunstlehrer-1) [Kunstraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) |
| 3 | Biologie (NaWi-Lehrer-4) [Bio2] | Sport (Sportlehrer-2) [Sporthalle3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Sport (Sportlehrer-2) [Sporthalle3] |
| 4 | Sport (Sportlehrer-2) [Sporthalle3] | Physik (NaWi-Lehrer-1) [NaWi3] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) |
| 5 | Geographie (GesWiss-Lehrer-1) | Physik (NaWi-Lehrer-1) [NaWi3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | Geographie (GesWiss-Lehrer-1) |
| 6 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | - |
| 7 | - | - | Biologie (NaWi-Lehrer-4) [Bio2] | - | - |
| 8 | - | - | Gemeinschaftskunde (GesWiss-Lehrer-1) | - | - |

### 7d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | - | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | - |
| 2 | Gemeinschaftskunde (GesWiss-Lehrer-1) | Kunst (Kunstlehrer-1) [Kunstraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Musik (Musiklehrer-1) [Musikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) |
| 3 | Geographie (GesWiss-Lehrer-1) | Biologie (NaWi-Lehrer-5) [Bio2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Sport (Sportlehrer-1) [Sporthalle3] |
| 4 | Biologie (NaWi-Lehrer-5) [Bio2] | Geographie (GesWiss-Lehrer-1) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) |
| 5 | Sport (Sportlehrer-1) [Sporthalle3] | Geschichte (GesWiss-Lehrer-1) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-2) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | Physik (NaWi-Lehrer-4) [NaWi3] |
| 6 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-4) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5) | Physik (NaWi-Lehrer-4) [NaWi3] |
| 7 | Geschichte (GesWiss-Lehrer-1) | Gemeinschaftskunde (GesWiss-Lehrer-1) | - | - | - |
| 8 | - | Sport (Sportlehrer-1) [Sporthalle3] | - | - | - |

### 8a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Geographie (GesWiss-Lehrer-1) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-3 / Religionslehrer-kath-1) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Sport (Sportlehrer-1) [Sporthalle3] |
| 2 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) |
| 3 | Physik (NaWi-Lehrer-3) [NaWi3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) |
| 4 | Physik (NaWi-Lehrer-3) [NaWi3] | Kunst (Kunstlehrer-1) [Kunstraum2] | Sport (Sportlehrer-1) [Sporthalle3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] |
| 5 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Musik (Musiklehrer-1) [Musikraum2] | Geschichte (GesWiss-Lehrer-1) | Chemie (NaWi-Lehrer-3) [NaWi3] |
| 6 | Gemeinschaftskunde (GesWiss-Lehrer-1) | Geschichte (GesWiss-Lehrer-1) | Biologie (NaWi-Lehrer-5) [Bio2] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-3 / Religionslehrer-kath-1) | Chemie (NaWi-Lehrer-3) [NaWi3] |
| 7 | Biologie (NaWi-Lehrer-5) [Bio2] | Sport (Sportlehrer-1) [Sporthalle3] | Gemeinschaftskunde (GesWiss-Lehrer-1) | - | - |
| 8 | - | Geographie (GesWiss-Lehrer-1) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - | - |

### 8b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Biologie (NaWi-Lehrer-2) [Bio2] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-3 / Religionslehrer-kath-1) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Sport (Sportlehrer-2) [Sporthalle3] |
| 2 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) |
| 3 | Physik (NaWi-Lehrer-2) [NaWi3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) |
| 4 | Physik (NaWi-Lehrer-2) [NaWi3] | Gemeinschaftskunde (GesWiss-Lehrer-5) | Geographie (GesWiss-Lehrer-5) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] |
| 5 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Gemeinschaftskunde (GesWiss-Lehrer-5) | Sport (Sportlehrer-2) [Sporthalle3] | Geschichte (GesWiss-Lehrer-5) |
| 6 | Geschichte (GesWiss-Lehrer-5) | Kunst (Kunstlehrer-1) [Kunstraum2] | Musik (Musiklehrer-1) [Musikraum2] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-3 / Religionslehrer-kath-1) | Geographie (GesWiss-Lehrer-5) |
| 7 | Chemie (NaWi-Lehrer-3) [NaWi3] | Sport (Sportlehrer-2) [Sporthalle3] | Biologie (NaWi-Lehrer-2) [Bio2] | - | - |
| 8 | Chemie (NaWi-Lehrer-3) [NaWi3] | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - | - |

### 8c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Gemeinschaftskunde (GesWiss-Lehrer-5) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-3 / Religionslehrer-kath-1) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Geographie (GesWiss-Lehrer-5) |
| 2 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) |
| 3 | Kunst (Kunstlehrer-1) [Kunstraum2] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) |
| 4 | Musik (Musiklehrer-1) [Musikraum2] | Sport (Sportlehrer-2) [Sporthalle3] | Physik (NaWi-Lehrer-3) [NaWi3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] |
| 5 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Physik (NaWi-Lehrer-3) [NaWi3] | Geographie (GesWiss-Lehrer-5) | Sport (Sportlehrer-2) [Sporthalle3] |
| 6 | Sport (Sportlehrer-2) [Sporthalle3] | Chemie (NaWi-Lehrer-4) [NaWi3] | Gemeinschaftskunde (GesWiss-Lehrer-5) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-3 / Religionslehrer-kath-1) | Biologie (NaWi-Lehrer-2) [Bio2] |
| 7 | Biologie (NaWi-Lehrer-2) [Bio2] | Chemie (NaWi-Lehrer-4) [NaWi3] | Geschichte (GesWiss-Lehrer-5) | Geschichte (GesWiss-Lehrer-5) | - |
| 8 | - | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - | - |

### 8d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Musik (Musiklehrer-1) [Musikraum2] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-3 / Religionslehrer-kath-1) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Kunst (Kunstlehrer-1) [Kunstraum2] |
| 2 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) |
| 3 | Gemeinschaftskunde (GesWiss-Lehrer-4) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-3 / Mathematiklehrer-5) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-2 / Englischlehrer-1 / Englischlehrer-4 / Englischlehrer-2) |
| 4 | Geschichte (GesWiss-Lehrer-4) | Sport (Sportlehrer-1) [Sporthalle3] | Gemeinschaftskunde (GesWiss-Lehrer-4) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] |
| 5 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-3 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-5 / Deutschlehrer-1 / Deutschlehrer-1 / Deutschlehrer-5) | Geographie (GesWiss-Lehrer-4) | Geschichte (GesWiss-Lehrer-4) | Physik (NaWi-Lehrer-1) [NaWi3] |
| 6 | Sport (Sportlehrer-1) [Sporthalle3] | Chemie (NaWi-Lehrer-1) [NaWi3] | Sport (Sportlehrer-1) [Sporthalle3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-3 / Religionslehrer-kath-1) | Physik (NaWi-Lehrer-1) [NaWi3] |
| 7 | Biologie (NaWi-Lehrer-1) [Bio2] | Chemie (NaWi-Lehrer-1) [NaWi3] | Biologie (NaWi-Lehrer-1) [Bio2] | - | - |
| 8 | Geographie (GesWiss-Lehrer-4) | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - | - |

### 9a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Biologie (NaWi-Lehrer-1) [Bio2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Sport (Sportlehrer-1) [Sporthalle3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) |
| 2 | Sport (Sportlehrer-1) [Sporthalle3] | Gemeinschaftskunde (GesWiss-Lehrer-3) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-1 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-1 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] |
| 3 | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Kunst (Kunstlehrer-1) [Kunstraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Chemie (NaWi-Lehrer-3) [NaWi3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 4 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Chemie (NaWi-Lehrer-3) [NaWi3] | Biologie (NaWi-Lehrer-1) [Bio2] |
| 5 | Physik (NaWi-Lehrer-1) [NaWi3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Geschichte (GesWiss-Lehrer-3) |
| 6 | Physik (NaWi-Lehrer-1) [NaWi3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Gemeinschaftskunde (GesWiss-Lehrer-3) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) |
| 7 | - | Geographie (GesWiss-Lehrer-3) | Sport (Sportlehrer-1) [Sporthalle3] | Geographie (GesWiss-Lehrer-3) | - |
| 8 | - | Geschichte (GesWiss-Lehrer-3) | - | Musik (Musiklehrer-1) [Musikraum2] | - |

### 9b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Kunst (Kunstlehrer-1) [Kunstraum2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Biologie (NaWi-Lehrer-3) [Bio2] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) |
| 2 | Biologie (NaWi-Lehrer-3) [Bio2] | Geschichte (GesWiss-Lehrer-2) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-1 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-1 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] |
| 3 | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Musik (Musiklehrer-1) [Musikraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Physik (NaWi-Lehrer-1) [NaWi3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 4 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Physik (NaWi-Lehrer-1) [NaWi3] | Chemie (NaWi-Lehrer-5) [NaWi3] |
| 5 | Gemeinschaftskunde (GesWiss-Lehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Chemie (NaWi-Lehrer-5) [NaWi3] |
| 6 | Sport (Sportlehrer-3) [Sporthalle3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Geschichte (GesWiss-Lehrer-2) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) |
| 7 | - | Geographie (GesWiss-Lehrer-2) | Sport (Sportlehrer-3) [Sporthalle3] | Sport (Sportlehrer-3) [Sporthalle3] | - |
| 8 | - | Gemeinschaftskunde (GesWiss-Lehrer-2) | Geographie (GesWiss-Lehrer-2) | - | - |

### 9c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Physik (NaWi-Lehrer-4) [NaWi3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Musik (Musiklehrer-1) [Musikraum2] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) |
| 2 | Physik (NaWi-Lehrer-4) [NaWi3] | Chemie (NaWi-Lehrer-1) [NaWi3] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-1 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-1 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] |
| 3 | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Chemie (NaWi-Lehrer-1) [NaWi3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Gemeinschaftskunde (GesWiss-Lehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 4 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Geschichte (GesWiss-Lehrer-2) | Geographie (GesWiss-Lehrer-2) |
| 5 | Sport (Sportlehrer-2) [Sporthalle3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Geschichte (GesWiss-Lehrer-2) |
| 6 | Gemeinschaftskunde (GesWiss-Lehrer-2) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Biologie (NaWi-Lehrer-1) [Bio2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) |
| 7 | Geographie (GesWiss-Lehrer-2) | - | Kunst (Kunstlehrer-1) [Kunstraum2] | Sport (Sportlehrer-2) [Sporthalle3] | - |
| 8 | Biologie (NaWi-Lehrer-1) [Bio2] | - | Sport (Sportlehrer-2) [Sporthalle3] | - | - |

### 9d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Gemeinschaftskunde (GesWiss-Lehrer-2) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Geographie (GesWiss-Lehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) |
| 2 | Geographie (GesWiss-Lehrer-2) | Sport (Sportlehrer-2) [Sporthalle3] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-1 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-1 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] |
| 3 | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Geschichte (GesWiss-Lehrer-2) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Sport (Sportlehrer-2) [Sporthalle3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] |
| 4 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Musik (Musiklehrer-1) [Musikraum2] | Physik (NaWi-Lehrer-2) [NaWi3] |
| 5 | Chemie (NaWi-Lehrer-5) [NaWi3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-1 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Physik (NaWi-Lehrer-2) [NaWi3] |
| 6 | Chemie (NaWi-Lehrer-5) [NaWi3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Kunst (Kunstlehrer-1) [Kunstraum2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-6 / Deutschlehrer-1 / Deutschlehrer-2 / Deutschlehrer-5) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) |
| 7 | Sport (Sportlehrer-2) [Sporthalle3] | - | Gemeinschaftskunde (GesWiss-Lehrer-2) | Geschichte (GesWiss-Lehrer-2) | - |
| 8 | - | - | Biologie (NaWi-Lehrer-5) [Bio2] | Biologie (NaWi-Lehrer-5) [Bio2] | - |

### 10a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Biologie (NaWi-Lehrer-3) [Bio2] | Geographie (GesWiss-Lehrer-2) | Musik (Musiklehrer-1) [Musikraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) | Gemeinschaftskunde (GesWiss-Lehrer-2) |
| 2 | Chemie (NaWi-Lehrer-1) [NaWi3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Sport (Sportlehrer-3) [Sporthalle3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Geschichte (GesWiss-Lehrer-2) |
| 3 | Chemie (NaWi-Lehrer-1) [NaWi3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) |
| 4 | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Sport (Sportlehrer-3) [Sporthalle3] |
| 5 | Kunst (Kunstlehrer-1) [Kunstraum2] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Geographie (GesWiss-Lehrer-2) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) |
| 6 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Physik (NaWi-Lehrer-3) [NaWi3] | Geschichte (GesWiss-Lehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-1) |
| 7 | Sport (Sportlehrer-3) [Sporthalle3] | - | Physik (NaWi-Lehrer-3) [NaWi3] | - | - |
| 8 | Gemeinschaftskunde (GesWiss-Lehrer-2) | - | Biologie (NaWi-Lehrer-3) [Bio2] | - | - |

### 10b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Gemeinschaftskunde (GesWiss-Lehrer-3) | Gemeinschaftskunde (GesWiss-Lehrer-3) | Chemie (NaWi-Lehrer-4) [NaWi3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) | Sport (Sportlehrer-3) [Sporthalle3] |
| 2 | Kunst (Kunstlehrer-2) [Kunstraum2] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Chemie (NaWi-Lehrer-4) [NaWi3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Geographie (GesWiss-Lehrer-3) |
| 3 | Geschichte (GesWiss-Lehrer-3) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) |
| 4 | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Geschichte (GesWiss-Lehrer-3) |
| 5 | Biologie (NaWi-Lehrer-4) [Bio2] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Sport (Sportlehrer-3) [Sporthalle3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) |
| 6 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Biologie (NaWi-Lehrer-4) [Bio2] | Musik (Musiklehrer-1) [Musikraum2] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-1) |
| 7 | - | - | Geographie (GesWiss-Lehrer-3) | Physik (NaWi-Lehrer-1) [NaWi3] | - |
| 8 | - | - | Sport (Sportlehrer-3) [Sporthalle3] | Physik (NaWi-Lehrer-1) [NaWi3] | - |

### 10c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Gemeinschaftskunde (GesWiss-Lehrer-4) | Geographie (GesWiss-Lehrer-4) | Kunst (Kunstlehrer-2) [Kunstraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) | Musik (Musiklehrer-2) [Musikraum2] |
| 2 | Chemie (NaWi-Lehrer-5) [NaWi3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Gemeinschaftskunde (GesWiss-Lehrer-4) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Sport (Sportlehrer-3) [Sporthalle3] |
| 3 | Chemie (NaWi-Lehrer-5) [NaWi3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) |
| 4 | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Geographie (GesWiss-Lehrer-4) |
| 5 | Sport (Sportlehrer-3) [Sporthalle3] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Physik (NaWi-Lehrer-1) [NaWi3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) |
| 6 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Sport (Sportlehrer-3) [Sporthalle3] | Physik (NaWi-Lehrer-1) [NaWi3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-1) |
| 7 | Geschichte (GesWiss-Lehrer-4) | - | Biologie (NaWi-Lehrer-5) [Bio2] | - | - |
| 8 | Biologie (NaWi-Lehrer-5) [Bio2] | - | Geschichte (GesWiss-Lehrer-4) | - | - |

### 10d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Biologie (NaWi-Lehrer-5) [Bio2] | Kunst (Kunstlehrer-2) [Kunstraum2] | Gemeinschaftskunde (GesWiss-Lehrer-5) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) | Chemie (NaWi-Lehrer-4) [NaWi3] |
| 2 | Musik (Musiklehrer-2) [Musikraum2] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Geographie (GesWiss-Lehrer-5) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Chemie (NaWi-Lehrer-4) [NaWi3] |
| 3 | Gemeinschaftskunde (GesWiss-Lehrer-5) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-2 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) |
| 4 | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-3 / Deutschlehrer-4 / Deutschlehrer-3 / Deutschlehrer-3 / Deutschlehrer-6) | Sport (Sportlehrer-2) [Sporthalle3] |
| 5 | Geschichte (GesWiss-Lehrer-5) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Physik (NaWi-Lehrer-5) [NaWi3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-1) |
| 6 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-4) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Sport (Sportlehrer-2) [Sporthalle3] | Physik (NaWi-Lehrer-5) [NaWi3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-1) |
| 7 | Geographie (GesWiss-Lehrer-5) | - | - | Biologie (NaWi-Lehrer-5) [Bio2] | - |
| 8 | Sport (Sportlehrer-2) [Sporthalle3] | - | - | Geschichte (GesWiss-Lehrer-5) | - |

## Lehrkraefte

### Deutschlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 9b Deutsch-A-1 | 9b Deutsch-A-1 | 7b Deutsch-E-1 | - |
| 2 | 6c Deutsch | 8b Deutsch-E-2 | 8b Deutsch-E-2 | - | 7b Deutsch-E-1 |
| 3 | - | 6c Deutsch | 7b Deutsch-E-1 | 6c Deutsch | - |
| 4 | - | 6c Deutsch | - | 8b Deutsch-E-2 | - |
| 5 | - | 8b Deutsch-E-2 | 7b Deutsch-E-1 | 6c Deutsch | - |
| 6 | - | 9b Deutsch-A-1 | - | 9b Deutsch-A-1 | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Deutschlehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 5b Deutsch | 9a Deutsch-G-1 | 9a Deutsch-G-1 | 7a Deutsch-G-2 | 5d Deutsch |
| 2 | 5d Deutsch | 6b Deutsch | 6d Deutsch | 5b Deutsch | 7a Deutsch-G-2 |
| 3 | 6d Deutsch | 5b Deutsch | 7a Deutsch-G-2 | 6d Deutsch | 5d Deutsch |
| 4 | 6b Deutsch | 6d Deutsch | 5d Deutsch | 5b Deutsch | - |
| 5 | 6b Deutsch | 5b Deutsch | 7a Deutsch-G-2 | 6d Deutsch | - |
| 6 | 5d Deutsch | 9a Deutsch-G-1 | 6b Deutsch | 9a Deutsch-G-1 | - |
| 7 | - | - | 6b Deutsch | - | - |
| 8 | - | - | - | - | - |

### Deutschlehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | 7b Deutsch-E-2 | 5c Deutsch |
| 2 | - | - | - | - | 7b Deutsch-E-2 |
| 3 | - | - | 7b Deutsch-E-2 | - | 10b Deutsch-A-1 |
| 4 | 10b Deutsch-A-1 | - | 10b Deutsch-A-1 | 10b Deutsch-A-1 | 5c Deutsch |
| 5 | 5c Deutsch | - | 7b Deutsch-E-2 | 5c Deutsch | - |
| 6 | - | - | 5c Deutsch | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Deutschlehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | 6a Deutsch |
| 2 | - | - | - | - | 6a Deutsch |
| 3 | 6a Deutsch | - | 6a Deutsch | - | 10c Deutsch-E-1 |
| 4 | 10c Deutsch-E-1 | - | 10c Deutsch-E-1 | 10c Deutsch-E-1 | - |
| 5 | - | - | - | - | - |
| 6 | - | 6a Deutsch | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Deutschlehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 9a Deutsch-G-2 | 9a Deutsch-G-2 | - | - |
| 2 | - | 8b Deutsch-E-1 | 8b Deutsch-E-1 | - | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | 8b Deutsch-E-1 | - |
| 5 | - | 8b Deutsch-E-1 | - | - | - |
| 6 | - | 9a Deutsch-G-2 | - | 9a Deutsch-G-2 | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Deutschlehrer-6

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 9c Deutsch-E-1 | 9c Deutsch-E-1 | - | - |
| 2 | - | - | - | - | - |
| 3 | - | - | 5a Deutsch | - | 10a Deutsch-G-2 |
| 4 | 10a Deutsch-G-2 | - | 10a Deutsch-G-2 | 10a Deutsch-G-2 | - |
| 5 | 5a Deutsch | 5a Deutsch | - | 5a Deutsch | - |
| 6 | 5a Deutsch | 9c Deutsch-E-1 | - | 9c Deutsch-E-1 | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Mathematiklehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 6c Mathematik | 5a Mathematik | 6c Mathematik | 5a Mathematik |
| 2 | - | 10c Mathematik-E-2 | 9c IMP [Computerraum2] | 9d Mathematik-A-1 | 9c IMP [Computerraum2] |
| 3 | - | - | 6c Mathematik | 10c IMP [Computerraum2] | - |
| 4 | - | 10c Mathematik-E-2 | 9d Mathematik-A-1 | 5a Mathematik | 8c IMP [Computerraum2] |
| 5 | 6c Mathematik | 10c IMP [Computerraum2] | 10c Mathematik-E-2 | 9d Mathematik-A-1 | 5a Mathematik |
| 6 | 10c Mathematik-E-2 | 5a Mathematik | 6c Mathematik | - | 9d Mathematik-A-1 |
| 7 | - | - | - | - | - |
| 8 | - | - | 8c IMP [Computerraum2] | - | - |

### Mathematiklehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 5c Mathematik | 6a Mathematik | 6b Mathematik | 8d Mathematik-E-2 | - |
| 2 | 6b Mathematik | 5b Mathematik | 6a Mathematik | 8d Mathematik-E-2 | 8d Mathematik-E-2 |
| 3 | 6b Mathematik | 6b Mathematik | 8d Mathematik-E-2 | 5c Mathematik | 5c Mathematik |
| 4 | 5b Mathematik | 5c Mathematik | - | 6a Mathematik | - |
| 5 | 6a Mathematik | 6a Mathematik | - | 5b Mathematik | 5c Mathematik |
| 6 | - | 6b Mathematik | - | 5b Mathematik | 5b Mathematik |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Mathematiklehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | 6d Mathematik | 8b Mathematik-G-1 | - |
| 2 | - | 10d Mathematik-A-1 | 7d Mathematik-E-2 | 8b Mathematik-G-1 | 8b Mathematik-G-1 |
| 3 | - | 6d Mathematik | 8b Mathematik-G-1 | - | 6d Mathematik |
| 4 | - | 10d Mathematik-A-1 | 6d Mathematik | - | 7d Mathematik-E-2 |
| 5 | - | 6d Mathematik | 10d Mathematik-A-1 | - | - |
| 6 | 10d Mathematik-A-1 | 7d Mathematik-E-2 | 7d Mathematik-E-2 | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Mathematiklehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | 10d Mathematik-E-1 | 7b Mathematik-G-2 | 9d Mathematik-E-1 | - |
| 3 | - | 5d Mathematik | 5d Mathematik | 5d Mathematik | - |
| 4 | - | 10d Mathematik-E-1 | 9d Mathematik-E-1 | - | 7b Mathematik-G-2 |
| 5 | - | 5d Mathematik | 10d Mathematik-E-1 | 9d Mathematik-E-1 | 5d Mathematik |
| 6 | 10d Mathematik-E-1 | 7b Mathematik-G-2 | 7b Mathematik-G-2 | - | 9d Mathematik-E-1 |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Mathematiklehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | 8b Mathematik-G-2 | - |
| 2 | - | - | - | 8b Mathematik-G-2 | 8b Mathematik-G-2 |
| 3 | - | - | 8b Mathematik-G-2 | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | - | - | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 8a Englisch-E-2 | - | 10b Englisch-G-1 | 6b Englisch |
| 2 | - | 6a Englisch | - | 5c Englisch | 5c Englisch |
| 3 | 9a Englisch-A-1 | 10b Englisch-G-1 | 9a Englisch-A-1 | 8a Englisch-E-2 | 8a Englisch-E-2 |
| 4 | 6a Englisch | 6a Englisch | 6b Englisch | 6b Englisch | 5b Englisch |
| 5 | 5b Englisch | 5c Englisch | 9a Englisch-A-1 | 6a Englisch | 10b Englisch-G-1 |
| 6 | 5b Englisch | 5c Englisch | 5b Englisch | 6b Englisch | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 8a Englisch-E-1 | - | 10c Englisch-E-2 | - |
| 2 | - | 6c Englisch | - | 6c Englisch | 6c Englisch |
| 3 | 9c Englisch-E-2 | 10c Englisch-E-2 | 9c Englisch-E-2 | 8a Englisch-E-1 | 8a Englisch-E-1 |
| 4 | - | - | 7a Englisch-E-2 | - | - |
| 5 | - | 6c Englisch | 9c Englisch-E-2 | 7a Englisch-E-2 | 10c Englisch-E-2 |
| 6 | - | - | - | 7a Englisch-E-2 | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 6d Englisch | - | 6d Englisch | - |
| 2 | - | 6d Englisch | - | - | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | - | - | - |
| 6 | 6d Englisch | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 8b Englisch-G-1 | - | 10a Englisch-A-1 | - |
| 2 | 5a Englisch | 5a Englisch | - | 5a Englisch | 5a Englisch |
| 3 | - | 10a Englisch-A-1 | - | 8b Englisch-G-1 | 8b Englisch-G-1 |
| 4 | - | - | - | - | - |
| 5 | - | - | - | - | 10a Englisch-A-1 |
| 6 | - | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 5d Englisch | - | - | - | - |
| 2 | - | 5d Englisch | - | - | - |
| 3 | - | - | - | - | - |
| 4 | - | - | 7a Englisch-E-1 | - | - |
| 5 | - | - | 5d Englisch | 7a Englisch-E-1 | - |
| 6 | - | - | 5d Englisch | 7a Englisch-E-1 | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### NaWi-Lehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 9a Biologie [Bio2] | - | - | - | - |
| 2 | 10a Chemie [NaWi3] | 9c Chemie [NaWi3] | 9d NwT [Technikraum2] | - | 9d NwT [Technikraum2] |
| 3 | 10a Chemie [NaWi3] | 9c Chemie [NaWi3] | - | 9b Physik [NaWi3] | - |
| 4 | - | 7c Physik [NaWi3] | - | 9b Physik [NaWi3] | 9a Biologie [Bio2] |
| 5 | 9a Physik [NaWi3] | 7c Physik [NaWi3] | - | 10c Physik [NaWi3] | 8d Physik [NaWi3] |
| 6 | 9a Physik [NaWi3] | 8d Chemie [NaWi3] | 9c Biologie [Bio2] | 10c Physik [NaWi3] | 8d Physik [NaWi3] |
| 7 | 8d Biologie [Bio2] | 8d Chemie [NaWi3] | 8d Biologie [Bio2] | 10b Physik [NaWi3] | - |
| 8 | 9c Biologie [Bio2] | - | - | 10b Physik [NaWi3] | - |

### NaWi-Lehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 8b Biologie [Bio2] | 6b BNT [NaWi3] | 5d BNT [NaWi3] | - | 5b BNT [NaWi3] |
| 2 | 7a Biologie [Bio2] | 7a Biologie [Bio2] | 6b BNT [NaWi3] | - | 5d BNT [NaWi3] |
| 3 | 8b Physik [NaWi3] | 6a BNT [NaWi3] | 5b BNT [NaWi3] | - | 6b BNT [NaWi3] |
| 4 | 8b Physik [NaWi3] | 7a Physik [NaWi3] | 6a BNT [NaWi3] | - | 9d Physik [NaWi3] |
| 5 | 5d BNT [NaWi3] | 7a Physik [NaWi3] | 5b BNT [NaWi3] | - | 9d Physik [NaWi3] |
| 6 | 6b BNT [NaWi3] | - | 6a BNT [NaWi3] | 6a BNT [NaWi3] | 8c Biologie [Bio2] |
| 7 | 8c Biologie [Bio2] | - | 8b Biologie [Bio2] | - | - |
| 8 | - | - | - | - | - |

### NaWi-Lehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 10a Biologie [Bio2] | - | 6c BNT [NaWi3] | 9b Biologie [Bio2] | 6d BNT [NaWi3] |
| 2 | 9b Biologie [Bio2] | - | 6c BNT [NaWi3] | 6d BNT [NaWi3] | 6d BNT [NaWi3] |
| 3 | 8a Physik [NaWi3] | 7b Physik [NaWi3] | - | 9a Chemie [NaWi3] | 6c BNT [NaWi3] |
| 4 | 8a Physik [NaWi3] | 7b Physik [NaWi3] | 8c Physik [NaWi3] | 9a Chemie [NaWi3] | - |
| 5 | 6d BNT [NaWi3] | - | 8c Physik [NaWi3] | - | 8a Chemie [NaWi3] |
| 6 | 6c BNT [NaWi3] | - | 10a Physik [NaWi3] | - | 8a Chemie [NaWi3] |
| 7 | 8b Chemie [NaWi3] | - | 10a Physik [NaWi3] | - | - |
| 8 | 8b Chemie [NaWi3] | - | 10a Biologie [Bio2] | - | - |

### NaWi-Lehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 9c Physik [NaWi3] | 7b Biologie [Bio2] | 10b Chemie [NaWi3] | - | 10d Chemie [NaWi3] |
| 2 | 9c Physik [NaWi3] | 5c BNT [NaWi3] | 10b Chemie [NaWi3] | - | 10d Chemie [NaWi3] |
| 3 | 7c Biologie [Bio2] | 5a BNT [NaWi3] | 5c BNT [NaWi3] | 10d NwT [Technikraum2] | - |
| 4 | 7b Biologie [Bio2] | 5a BNT [NaWi3] | - | - | 8d NwT [Technikraum2] |
| 5 | 10b Biologie [Bio2] | 10d NwT [Technikraum2] | 5a BNT [NaWi3] | - | 7d Physik [NaWi3] |
| 6 | 5c BNT [NaWi3] | 8c Chemie [NaWi3] | 10b Biologie [Bio2] | - | 7d Physik [NaWi3] |
| 7 | - | 8c Chemie [NaWi3] | 7c Biologie [Bio2] | - | - |
| 8 | - | - | 8d NwT [Technikraum2] | - | - |

### NaWi-Lehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 10d Biologie [Bio2] | - | - | - | - |
| 2 | 10c Chemie [NaWi3] | - | - | - | - |
| 3 | 10c Chemie [NaWi3] | 7d Biologie [Bio2] | - | - | - |
| 4 | 7d Biologie [Bio2] | - | - | - | 9b Chemie [NaWi3] |
| 5 | 9d Chemie [NaWi3] | - | - | 10d Physik [NaWi3] | 9b Chemie [NaWi3] |
| 6 | 9d Chemie [NaWi3] | - | 8a Biologie [Bio2] | 10d Physik [NaWi3] | - |
| 7 | 8a Biologie [Bio2] | - | 10c Biologie [Bio2] | 10d Biologie [Bio2] | - |
| 8 | 10c Biologie [Bio2] | - | 9d Biologie [Bio2] | 9d Biologie [Bio2] | - |

### GesWiss-Lehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 8a Geographie | 7c Gemeinschaftskunde | - | - | 7c Geschichte |
| 2 | 7d Gemeinschaftskunde | 7c Geschichte | - | - | - |
| 3 | 7d Geographie | 7a Geographie | - | - | 7a Gemeinschaftskunde |
| 4 | 7a Gemeinschaftskunde | 7d Geographie | - | - | 5a Geographie |
| 5 | 7c Geographie | 7d Geschichte | - | 8a Geschichte | 7c Geographie |
| 6 | 8a Gemeinschaftskunde | 8a Geschichte | - | 5a Geographie | 7a Geschichte |
| 7 | 7d Geschichte | 7d Gemeinschaftskunde | 8a Gemeinschaftskunde | 7a Geographie | - |
| 8 | - | 8a Geographie | 7c Gemeinschaftskunde | 7a Geschichte | - |

### GesWiss-Lehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 9d Gemeinschaftskunde | 10a Geographie | - | 9d Geographie | 10a Gemeinschaftskunde |
| 2 | 9d Geographie | 9b Geschichte | - | - | 10a Geschichte |
| 3 | - | 9d Geschichte | - | 9c Gemeinschaftskunde | - |
| 4 | - | - | - | 9c Geschichte | 9c Geographie |
| 5 | 9b Gemeinschaftskunde | - | - | 10a Geographie | 9c Geschichte |
| 6 | 9c Gemeinschaftskunde | - | 9b Geschichte | 10a Geschichte | - |
| 7 | 9c Geographie | 9b Geographie | 9d Gemeinschaftskunde | 9d Geschichte | - |
| 8 | 10a Gemeinschaftskunde | 9b Gemeinschaftskunde | 9b Geographie | - | - |

### GesWiss-Lehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 10b Gemeinschaftskunde | 10b Gemeinschaftskunde | - | - | 6c Geschichte |
| 2 | 6d Geschichte | 9a Gemeinschaftskunde | - | - | 10b Geographie |
| 3 | 10b Geschichte | 5c Geschichte | 6b Geographie | 6b Geschichte | 5b Geographie |
| 4 | 6c Geographie | 6b Geographie | 5b Geographie | 6c Geschichte | 10b Geschichte |
| 5 | - | 6b Geschichte | 6c Geographie | 5d Geographie | 9a Geschichte |
| 6 | - | 5d Geographie | 9a Gemeinschaftskunde | 6d Geschichte | 5c Geschichte |
| 7 | - | 9a Geographie | 10b Geographie | 9a Geographie | - |
| 8 | - | 9a Geschichte | - | - | - |

### GesWiss-Lehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 10c Gemeinschaftskunde | 10c Geographie | 6a Geschichte | - | - |
| 2 | 7b Geschichte | 7b Geschichte | 10c Gemeinschaftskunde | 6a Geographie | - |
| 3 | 8d Gemeinschaftskunde | - | - | 6a Geschichte | - |
| 4 | 8d Geschichte | - | 8d Gemeinschaftskunde | 6d Geographie | 10c Geographie |
| 5 | 7b Gemeinschaftskunde | - | 8d Geographie | 8d Geschichte | 7b Gemeinschaftskunde |
| 6 | 6a Geographie | - | 6d Geographie | - | 7b Geographie |
| 7 | 10c Geschichte | - | 7b Geographie | - | - |
| 8 | 8d Geographie | - | 10c Geschichte | - | - |

### GesWiss-Lehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 8c Gemeinschaftskunde | - | 10d Gemeinschaftskunde | - | 8c Geographie |
| 2 | 5c Geographie | - | 10d Geographie | - | 5b Geschichte |
| 3 | 10d Gemeinschaftskunde | - | - | 5b Geschichte | 5a Geschichte |
| 4 | 5a Geschichte | 8b Gemeinschaftskunde | 8b Geographie | 5c Geographie | 5d Geschichte |
| 5 | 10d Geschichte | - | 8b Gemeinschaftskunde | 8c Geographie | 8b Geschichte |
| 6 | 8b Geschichte | - | 8c Gemeinschaftskunde | 5d Geschichte | 8b Geographie |
| 7 | 10d Geographie | - | 8c Geschichte | 8c Geschichte | - |
| 8 | - | - | - | 10d Geschichte | - |

### Sportlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | 9a Sport [Sporthalle3] | 8a Sport [Sporthalle3] |
| 2 | 9a Sport [Sporthalle3] | - | 9a Sport-Profil [Sporthalle3] | 7a Sport [Sporthalle3] | 9a Sport-Profil [Sporthalle3] |
| 3 | 7a Sport [Sporthalle3] | - | - | 10a Sport-Profil [Sporthalle3] | 7d Sport [Sporthalle3] |
| 4 | 6d Sport [Sporthalle3] | 8d Sport [Sporthalle3] | 8a Sport [Sporthalle3] | - | 8a Sport-Profil [Sporthalle3] |
| 5 | 7d Sport [Sporthalle3] | 10a Sport-Profil [Sporthalle3] | 6d Sport [Sporthalle3] | - | 7a Sport [Sporthalle3] |
| 6 | 8d Sport [Sporthalle3] | 6d Sport [Sporthalle3] | 8d Sport [Sporthalle3] | - | - |
| 7 | - | 8a Sport [Sporthalle3] | 9a Sport [Sporthalle3] | - | - |
| 8 | - | 7d Sport [Sporthalle3] | 8a Sport-Profil [Sporthalle3] | - | - |

### Sportlehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 5d Sport [Sporthalle3] | - | - | 8b Sport [Sporthalle3] |
| 2 | - | 9d Sport [Sporthalle3] | - | 6b Sport [Sporthalle3] | 6b Sport [Sporthalle3] |
| 3 | - | 7c Sport [Sporthalle3] | - | 9d Sport [Sporthalle3] | 7c Sport [Sporthalle3] |
| 4 | 7c Sport [Sporthalle3] | 8c Sport [Sporthalle3] | - | 5d Sport [Sporthalle3] | 10d Sport [Sporthalle3] |
| 5 | 9c Sport [Sporthalle3] | - | 6b Sport [Sporthalle3] | 8b Sport [Sporthalle3] | 8c Sport [Sporthalle3] |
| 6 | 8c Sport [Sporthalle3] | 6c Sport [Sporthalle3] | 10d Sport [Sporthalle3] | 6c Sport [Sporthalle3] | 5d Sport [Sporthalle3] |
| 7 | 9d Sport [Sporthalle3] | 8b Sport [Sporthalle3] | 6c Sport [Sporthalle3] | 9c Sport [Sporthalle3] | - |
| 8 | 10d Sport [Sporthalle3] | - | 9c Sport [Sporthalle3] | - | - |

### Sportlehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | 5b Sport [Sporthalle3] | 6a Sport [Sporthalle3] | 10b Sport [Sporthalle3] |
| 2 | 6a Sport [Sporthalle3] | - | 10a Sport [Sporthalle3] | 7b Sport [Sporthalle3] | 10c Sport [Sporthalle3] |
| 3 | 7b Sport [Sporthalle3] | - | - | 5a Sport [Sporthalle3] | 6a Sport [Sporthalle3] |
| 4 | 5c Sport [Sporthalle3] | 5b Sport [Sporthalle3] | 5a Sport [Sporthalle3] | - | 10a Sport [Sporthalle3] |
| 5 | 10c Sport [Sporthalle3] | 7b Sport [Sporthalle3] | 5c Sport [Sporthalle3] | 10b Sport [Sporthalle3] | 5b Sport [Sporthalle3] |
| 6 | 9b Sport [Sporthalle3] | - | 10c Sport [Sporthalle3] | 5c Sport [Sporthalle3] | 5a Sport [Sporthalle3] |
| 7 | 10a Sport [Sporthalle3] | - | 9b Sport [Sporthalle3] | 9b Sport [Sporthalle3] | - |
| 8 | - | - | 10b Sport [Sporthalle3] | - | - |

### Musiklehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 8d Musik [Musikraum2] | - | 10a Musik [Musikraum2] | 9c Musik [Musikraum2] | 7b Musik [Musikraum2] |
| 2 | 7c Musik [Musikraum2] | - | 9c Musik-Profil [Musikraum2] | 7d Musik [Musikraum2] | 9c Musik-Profil [Musikraum2] |
| 3 | 6c Musik [Musikraum2] | 9b Musik [Musikraum2] | - | 10c Musik-Profil [Musikraum2] | - |
| 4 | 8c Musik [Musikraum2] | 5d Musik [Musikraum2] | 5c Musik [Musikraum2] | 9d Musik [Musikraum2] | 8c Musik-Profil [Musikraum2] |
| 5 | 7a Musik [Musikraum2] | 10c Musik-Profil [Musikraum2] | 8a Musik [Musikraum2] | 6b Musik [Musikraum2] | - |
| 6 | - | - | 8b Musik [Musikraum2] | 10b Musik [Musikraum2] | - |
| 7 | - | - | 6d Musik [Musikraum2] | 6a Musik [Musikraum2] | - |
| 8 | - | - | 8c Musik-Profil [Musikraum2] | 9a Musik [Musikraum2] | - |

### Musiklehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | 10c Musik [Musikraum2] |
| 2 | 10d Musik [Musikraum2] | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | - | - | - |
| 6 | - | 5b Musik [Musikraum2] | 5a Musik [Musikraum2] | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Kunstlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 9b Kunst [Kunstraum2] | 7a Kunst [Kunstraum2] | 5c Kunst [Kunstraum2] | 6b Kunst [Kunstraum2] | 8d Kunst [Kunstraum2] |
| 2 | 5b Kunst [Kunstraum2] | 7d Kunst [Kunstraum2] | 9d BK-Profil [Kunstraum2] | 7c Kunst [Kunstraum2] | 9d BK-Profil [Kunstraum2] |
| 3 | 8c Kunst [Kunstraum2] | 9a Kunst [Kunstraum2] | 6d Kunst [Kunstraum2] | 10d BK-Profil [Kunstraum2] | 7b Kunst [Kunstraum2] |
| 4 | 5d Kunst [Kunstraum2] | 8a Kunst [Kunstraum2] | 6c Kunst [Kunstraum2] | - | 8d BK-Profil [Kunstraum2] |
| 5 | 10a Kunst [Kunstraum2] | 10d BK-Profil [Kunstraum2] | 6a Kunst [Kunstraum2] | - | - |
| 6 | - | 8b Kunst [Kunstraum2] | 9d Kunst [Kunstraum2] | - | - |
| 7 | - | - | 9c Kunst [Kunstraum2] | - | - |
| 8 | - | - | 8d BK-Profil [Kunstraum2] | - | - |

### Kunstlehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 10d Kunst [Kunstraum2] | 10c Kunst [Kunstraum2] | - | - |
| 2 | 10b Kunst [Kunstraum2] | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | - | - | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | 5a Kunst [Kunstraum2] | - |
| 8 | - | - | - | - | - |

### Religionslehrer-ev-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 6d Religion-ev-2 | - | - | - | 9d Religion-ev-2 |
| 2 | - | - | 5c Religion-ev-1 | - | - |
| 3 | 5c Religion-ev-1 | - | - | - | - |
| 4 | 9d Religion-ev-2 | - | - | 7d Religion-ev-2 | 6d Religion-ev-2 |
| 5 | - | - | - | - | - |
| 6 | 7d Religion-ev-2 | 10d Religion-ev-2 | - | - | 10d Religion-ev-2 |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Religionslehrer-ev-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | - | 5d Religion-ev-2 | - | - |
| 3 | 5d Religion-ev-2 | - | - | - | - |
| 4 | - | - | - | 7c Religion-ev-1 | - |
| 5 | - | - | - | - | - |
| 6 | 7c Religion-ev-1 | 10c Religion-ev-1 | - | - | 10c Religion-ev-1 |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Religionslehrer-ev-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 6c Religion-ev-1 | - | 8d Religion-ev-2 | - | - |
| 2 | - | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | 6c Religion-ev-1 |
| 5 | - | - | - | - | - |
| 6 | - | - | - | 8d Religion-ev-2 | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Religionslehrer-kath-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | 8d Religion-kath | - | 9d Religion-kath |
| 2 | - | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | 9d Religion-kath | - | - | - | - |
| 5 | - | - | - | - | - |
| 6 | - | 10d Religion-kath | - | 8d Religion-kath | 10d Religion-kath |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Religionslehrer-kath-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 6d Religion-kath | - | - | - | - |
| 2 | - | - | 5d Religion-kath | - | - |
| 3 | 5d Religion-kath | - | - | - | - |
| 4 | - | - | - | 7d Religion-kath | 6d Religion-kath |
| 5 | - | - | - | - | - |
| 6 | 7d Religion-kath | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Ethiklehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | 8b Ethik | - | 9b Ethik |
| 2 | - | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | 9b Ethik | - | - | 7b Ethik | - |
| 5 | - | - | - | - | - |
| 6 | 7b Ethik | - | - | 8b Ethik | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Ethiklehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 6b Ethik | - | - | - | - |
| 2 | - | - | 5b Ethik | - | - |
| 3 | 5b Ethik | - | - | - | - |
| 4 | - | - | - | - | 6b Ethik |
| 5 | - | - | - | - | - |
| 6 | - | 10b Ethik | - | - | 10b Ethik |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Technik-Lehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 7d Technik-2 [Technikraum2] | - | 7d Technik-2 [Technikraum2] | - | - |
| 2 | 8c Technik-1 [Technikraum2] | - | - | - | - |
| 3 | - | 8c Technik-1 [Technikraum2] | - | 7d Technik-2 [Technikraum2] | 9d Technik-2 [Technikraum2] |
| 4 | - | 9d Technik-2 [Technikraum2] | - | - | - |
| 5 | 8c Technik-1 [Technikraum2] | 9d Technik-2 [Technikraum2] | - | - | 6d Technik-2 [Technikraum2] |
| 6 | - | - | - | - | 6d Technik-2 [Technikraum2] |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Technik-Lehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | 8d Technik-2 [Technikraum2] | - | - | 10c Technik-1 [Technikraum2] | - |
| 3 | - | 8d Technik-2 [Technikraum2] | 10c Technik-1 [Technikraum2] | - | - |
| 4 | - | - | - | - | - |
| 5 | 8d Technik-2 [Technikraum2] | - | - | - | 6c Technik-1 [Technikraum2] |
| 6 | - | - | - | - | 6c Technik-1 [Technikraum2] |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Technik-Lehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | - | - | 10d Technik-2 [Technikraum2] | - |
| 3 | - | - | 10d Technik-2 [Technikraum2] | - | 9c Technik-1 [Technikraum2] |
| 4 | - | 9c Technik-1 [Technikraum2] | - | - | - |
| 5 | - | 9c Technik-1 [Technikraum2] | - | - | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### AES-Lehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | 8b AES [AESraum2] | - | - | - | - |
| 3 | - | 8b AES [AESraum2] | - | - | 9b AES [AESraum2] |
| 4 | - | 9b AES [AESraum2] | - | - | - |
| 5 | 8b AES [AESraum2] | 9b AES [AESraum2] | - | - | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### AES-Lehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 7b AES [AESraum2] | - | 7b AES [AESraum2] | - | - |
| 2 | - | - | - | 10b AES [AESraum2] | - |
| 3 | - | - | 10b AES [AESraum2] | 7b AES [AESraum2] | - |
| 4 | - | - | - | - | - |
| 5 | - | - | - | - | 6b AES [AESraum2] |
| 6 | - | - | - | - | 6b AES [AESraum2] |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Franzoesisch-Lehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 7a Franzoesisch-1 | - | 7a Franzoesisch-1 | - | - |
| 2 | 8a Franzoesisch-1 | - | - | 10a Franzoesisch-1 | - |
| 3 | - | 8a Franzoesisch-1 | 10a Franzoesisch-1 | 7a Franzoesisch-1 | 9a Franzoesisch-1 |
| 4 | - | 9a Franzoesisch-1 | - | - | - |
| 5 | 8a Franzoesisch-1 | 9a Franzoesisch-1 | - | - | 6a Franzoesisch-1 |
| 6 | - | - | - | - | 6a Franzoesisch-1 |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Franzoesisch-Lehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | - | - | 10c Franzoesisch-2 | - |
| 3 | - | - | 10c Franzoesisch-2 | - | 9c Franzoesisch-2 |
| 4 | - | 9c Franzoesisch-2 | - | - | - |
| 5 | - | 9c Franzoesisch-2 | - | - | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Franzoesisch-Lehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 7c Franzoesisch-2 | - | 7c Franzoesisch-2 | - | - |
| 2 | 8c Franzoesisch-2 | - | - | - | - |
| 3 | - | 8c Franzoesisch-2 | - | 7c Franzoesisch-2 | - |
| 4 | - | - | - | - | - |
| 5 | 8c Franzoesisch-2 | - | - | - | 6c Franzoesisch-2 |
| 6 | - | - | - | - | 6c Franzoesisch-2 |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |
