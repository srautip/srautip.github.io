# Stundenplan: bw-gms-beispiel (realitaetsnah, ~700 Schueler, 4-zuegig)

**Status:** SolveTop (TimeLimitReached)  |  **CP-SAT-Status:** Feasible  |  **Kann-Verstoesse:** 0  |  **Qualitaet (Total):** 16014.3  |  **Verstoesse:** 0

*Phase 2.25: die Stagnationserkennung hat 20 von 22 Solve-Iteration(en) vorzeitig abgebrochen, weil ueber `stagnation_timeout_s` hinweg keine Verbesserung mehr gefunden wurde - spart Zeit fuer weitere Iterationen statt eine stehende Suche bis zum Zeitlimit weiterlaufen zu lassen.*

*Hinweis: `Feasible` statt `Optimal` bedeutet, CP-SAT konnte innerhalb von `solve_time_limit_s` (aktuell 1200s) keinen Optimalitaetsbeweis erbringen - ein hoeherer Wert in `config.yaml` kann helfen, ein noch besseres Ergebnis zu finden oder das aktuelle als optimal zu beweisen.*
*Zusaetzlich begrenzt `per_solve_time_limit_s` (aktuell 120s) jede EINZELNE Solve-Iteration - ein hoeherer Wert kann derselben Iteration mehr Zeit fuer einen Optimalitaetsbeweis geben, auf Kosten weniger Iterationen fuer zusaetzliche `max_solutions`-Alternativen innerhalb desselben Gesamtbudgets.*

## Optimalitaets-Luecke

Gefundene Loesung (Objective): **90.0**  |  Bewiesene untere Schranke: **0.0**  |  Maximal noch moegliche Verbesserung: **100.0%**

*Diese Luecke ist eine bewiesene OBERGRENZE, keine Vorhersage - die tatsaechlich erreichbare Verbesserung kann kleiner sein (bis hin zu 0, falls die gefundene Loesung bereits optimal ist, CP-SAT das aber innerhalb der Zeit nicht beweisen konnte).*

**Verlauf dieses Solve-Versuchs** (jede gefundene Verbesserung, nicht in festen Zeitabstaenden):

| Zeit (s) | Objective |
|---|---|
| 3.4 | 554.0 |
| 3.4 | 548.0 |
| 3.5 | 95.0 |
| 4.5 | 94.0 |
| 9.6 | 92.0 |
| 11.6 | 91.0 |
| 21.5 | 90.0 |

*Letzte Verbesserung bei 21.5s - fand danach bis zum Abbruch keine weitere statt. Ein deutlich frueherer letzter Eintrag als das Zeitbudget legt nahe, dass zusaetzliche Zeit fuer DIESEN Versuch wenig bringen wuerde.*

## Klassen

### 5a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Sport (Sportlehrer-3) [Sporthalle3] | Mathematik (Mathematiklehrer-2) | - | - | Kunst (Kunstlehrer-2) [Kunstraum2] |
| 2 | Mathematik (Mathematiklehrer-2) | Geographie (GesWiss-Lehrer-3) | Musik (Musiklehrer-2) [Musikraum2] | Mathematik (Mathematiklehrer-2) | Englisch (Englischlehrer-4) |
| 3 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | BNT (NaWi-Lehrer-2) [NaWi3] | Geschichte (GesWiss-Lehrer-4) | Geschichte (GesWiss-Lehrer-4) |
| 4 | Mathematik (Mathematiklehrer-2) | BNT (NaWi-Lehrer-2) [NaWi3] | BNT (NaWi-Lehrer-2) [NaWi3] | Englisch (Englischlehrer-4) | Englisch (Englischlehrer-4) |
| 5 | Deutsch (Deutschlehrer-3) | Deutsch (Deutschlehrer-3) | Englisch (Englischlehrer-4) | Deutsch (Deutschlehrer-3) | Mathematik (Mathematiklehrer-2) |
| 6 | Deutsch (Deutschlehrer-3) | Deutsch (Deutschlehrer-3) | Sport (Sportlehrer-3) [Sporthalle3] | Sport (Sportlehrer-3) [Sporthalle3] | Geographie (GesWiss-Lehrer-3) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 5b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Deutsch (Deutschlehrer-1) | Englisch (Englischlehrer-3) | BNT (NaWi-Lehrer-2) [NaWi3] | Englisch (Englischlehrer-3) | - |
| 2 | Englisch (Englischlehrer-3) | Geographie (GesWiss-Lehrer-1) | Geschichte (GesWiss-Lehrer-4) | Mathematik (Mathematiklehrer-4) | Kunst (Kunstlehrer-2) [Kunstraum2] |
| 3 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Musik (Musiklehrer-2) [Musikraum2] | Geographie (GesWiss-Lehrer-1) | Sport (Sportlehrer-1) [Sporthalle3] |
| 4 | Mathematik (Mathematiklehrer-4) | Sport (Sportlehrer-1) [Sporthalle3] | Mathematik (Mathematiklehrer-4) | Deutsch (Deutschlehrer-1) | Geschichte (GesWiss-Lehrer-4) |
| 5 | Deutsch (Deutschlehrer-1) | Mathematik (Mathematiklehrer-4) | Englisch (Englischlehrer-3) | Mathematik (Mathematiklehrer-4) | BNT (NaWi-Lehrer-2) [NaWi3] |
| 6 | - | BNT (NaWi-Lehrer-2) [NaWi3] | Sport (Sportlehrer-1) [Sporthalle3] | Deutsch (Deutschlehrer-1) | Deutsch (Deutschlehrer-1) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 5c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | Sport (Sportlehrer-3) [Sporthalle3] | Kunst (Kunstlehrer-1) [Kunstraum2] | - | Sport (Sportlehrer-3) [Sporthalle3] |
| 2 | Mathematik (Mathematiklehrer-3) | Geographie (GesWiss-Lehrer-2) | Geschichte (GesWiss-Lehrer-1) | Englisch (Englischlehrer-2) | Mathematik (Mathematiklehrer-3) |
| 3 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Englisch (Englischlehrer-2) | Mathematik (Mathematiklehrer-3) | BNT (NaWi-Lehrer-3) [NaWi3] |
| 4 | Deutsch (Deutschlehrer-6) | Deutsch (Deutschlehrer-6) | Sport (Sportlehrer-3) [Sporthalle3] | Englisch (Englischlehrer-2) | Geschichte (GesWiss-Lehrer-1) |
| 5 | BNT (NaWi-Lehrer-3) [NaWi3] | Mathematik (Mathematiklehrer-3) | Musik (Musiklehrer-1) [Musikraum2] | Mathematik (Mathematiklehrer-3) | Geographie (GesWiss-Lehrer-2) |
| 6 | Englisch (Englischlehrer-2) | Deutsch (Deutschlehrer-6) | Deutsch (Deutschlehrer-6) | Deutsch (Deutschlehrer-6) | BNT (NaWi-Lehrer-3) [NaWi3] |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 5d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Sport (Sportlehrer-2) [Sporthalle3] | Deutsch (Deutschlehrer-1) | - | - | Deutsch (Deutschlehrer-1) |
| 2 | Deutsch (Deutschlehrer-1) | BNT (NaWi-Lehrer-3) [NaWi3] | Deutsch (Deutschlehrer-1) | Musik (Musiklehrer-1) [Musikraum2] | Mathematik (Mathematiklehrer-1) |
| 3 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | BNT (NaWi-Lehrer-3) [NaWi3] | Sport (Sportlehrer-2) [Sporthalle3] | Englisch (Englischlehrer-5) |
| 4 | Mathematik (Mathematiklehrer-1) | Geographie (GesWiss-Lehrer-4) | Mathematik (Mathematiklehrer-1) | Englisch (Englischlehrer-5) | BNT (NaWi-Lehrer-3) [NaWi3] |
| 5 | Geschichte (GesWiss-Lehrer-3) | Geschichte (GesWiss-Lehrer-3) | Deutsch (Deutschlehrer-1) | Englisch (Englischlehrer-5) | Mathematik (Mathematiklehrer-1) |
| 6 | Englisch (Englischlehrer-5) | Mathematik (Mathematiklehrer-1) | Kunst (Kunstlehrer-1) [Kunstraum2] | Geographie (GesWiss-Lehrer-4) | Sport (Sportlehrer-2) [Sporthalle3] |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 6a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Kunst (Kunstlehrer-1) [Kunstraum2] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Sport (Sportlehrer-3) [Sporthalle3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Mathematik (Mathematiklehrer-4) |
| 2 | Englisch (Englischlehrer-1) | Sport (Sportlehrer-3) [Sporthalle3] | Geschichte (GesWiss-Lehrer-2) | Sport (Sportlehrer-3) [Sporthalle3] | Deutsch (Deutschlehrer-1) |
| 3 | Mathematik (Mathematiklehrer-4) | Mathematik (Mathematiklehrer-4) | Mathematik (Mathematiklehrer-4) | BNT (NaWi-Lehrer-4) [NaWi3] | BNT (NaWi-Lehrer-4) [NaWi3] |
| 4 | Deutsch (Deutschlehrer-1) | Musik (Musiklehrer-1) [Musikraum2] | Deutsch (Deutschlehrer-1) | Englisch (Englischlehrer-1) | Geschichte (GesWiss-Lehrer-2) |
| 5 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | BNT (NaWi-Lehrer-4) [NaWi3] | Englisch (Englischlehrer-1) | Geographie (GesWiss-Lehrer-1) | Deutsch (Deutschlehrer-1) |
| 6 | Englisch (Englischlehrer-1) | Geographie (GesWiss-Lehrer-1) | Deutsch (Deutschlehrer-1) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | BNT (NaWi-Lehrer-4) [NaWi3] |
| 7 | Mathematik (Mathematiklehrer-4) | - | - | - | - |
| 8 | - | - | - | - | - |

### 6b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik (Mathematiklehrer-4) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Deutsch (Deutschlehrer-5) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Deutsch (Deutschlehrer-5) |
| 2 | Geschichte (GesWiss-Lehrer-2) | BNT (NaWi-Lehrer-2) [NaWi3] | BNT (NaWi-Lehrer-2) [NaWi3] | Deutsch (Deutschlehrer-5) | Deutsch (Deutschlehrer-5) |
| 3 | Englisch (Englischlehrer-1) | Deutsch (Deutschlehrer-5) | Geographie (GesWiss-Lehrer-1) | Mathematik (Mathematiklehrer-4) | Englisch (Englischlehrer-1) |
| 4 | Englisch (Englischlehrer-1) | Geographie (GesWiss-Lehrer-1) | Englisch (Englischlehrer-1) | Mathematik (Mathematiklehrer-4) | Sport (Sportlehrer-1) [Sporthalle3] |
| 5 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Musik (Musiklehrer-1) [Musikraum2] | Sport (Sportlehrer-1) [Sporthalle3] | Sport (Sportlehrer-1) [Sporthalle3] | Mathematik (Mathematiklehrer-4) |
| 6 | Kunst (Kunstlehrer-1) [Kunstraum2] | Mathematik (Mathematiklehrer-4) | Geschichte (GesWiss-Lehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | BNT (NaWi-Lehrer-2) [NaWi3] |
| 7 | - | - | - | BNT (NaWi-Lehrer-2) [NaWi3] | - |
| 8 | - | - | - | - | - |

### 6c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Englisch (Englischlehrer-1) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Mathematik (Mathematiklehrer-4) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | BNT (NaWi-Lehrer-4) [NaWi3] |
| 2 | Mathematik (Mathematiklehrer-4) | Mathematik (Mathematiklehrer-4) | Mathematik (Mathematiklehrer-4) | Geographie (GesWiss-Lehrer-1) | Mathematik (Mathematiklehrer-4) |
| 3 | Sport (Sportlehrer-3) [Sporthalle3] | Sport (Sportlehrer-3) [Sporthalle3] | Englisch (Englischlehrer-1) | Musik (Musiklehrer-1) [Musikraum2] | Geschichte (GesWiss-Lehrer-2) |
| 4 | Deutsch (Deutschlehrer-4) | Deutsch (Deutschlehrer-4) | BNT (NaWi-Lehrer-4) [NaWi3] | Deutsch (Deutschlehrer-4) | Sport (Sportlehrer-3) [Sporthalle3] |
| 5 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Kunst (Kunstlehrer-1) [Kunstraum2] | Geschichte (GesWiss-Lehrer-2) | Deutsch (Deutschlehrer-4) | BNT (NaWi-Lehrer-4) [NaWi3] |
| 6 | Geographie (GesWiss-Lehrer-1) | Deutsch (Deutschlehrer-4) | Englisch (Englischlehrer-1) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Englisch (Englischlehrer-1) |
| 7 | - | BNT (NaWi-Lehrer-4) [NaWi3] | - | - | - |
| 8 | - | - | - | - | - |

### 6d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Geschichte (GesWiss-Lehrer-3) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Deutsch (Deutschlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Englisch (Englischlehrer-1) |
| 2 | Geographie (GesWiss-Lehrer-3) | Englisch (Englischlehrer-1) | Sport (Sportlehrer-3) [Sporthalle3] | Englisch (Englischlehrer-1) | Kunst (Kunstlehrer-1) [Kunstraum2] |
| 3 | BNT (NaWi-Lehrer-5) [NaWi3] | BNT (NaWi-Lehrer-5) [NaWi3] | Deutsch (Deutschlehrer-1) | BNT (NaWi-Lehrer-5) [NaWi3] | Mathematik (Mathematiklehrer-3) |
| 4 | Mathematik (Mathematiklehrer-3) | BNT (NaWi-Lehrer-5) [NaWi3] | Musik (Musiklehrer-1) [Musikraum2] | Mathematik (Mathematiklehrer-3) | Geographie (GesWiss-Lehrer-3) |
| 5 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Deutsch (Deutschlehrer-1) | Geschichte (GesWiss-Lehrer-3) | Englisch (Englischlehrer-1) | Mathematik (Mathematiklehrer-3) |
| 6 | Deutsch (Deutschlehrer-1) | Deutsch (Deutschlehrer-1) | Mathematik (Mathematiklehrer-3) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-3 / Religionslehrer-ev-2 / Religionslehrer-kath-2) | Sport (Sportlehrer-3) [Sporthalle3] |
| 7 | - | - | - | Sport (Sportlehrer-3) [Sporthalle3] | - |
| 8 | - | - | - | - | - |

### 7a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Geographie (GesWiss-Lehrer-1) |
| 2 | Geschichte (GesWiss-Lehrer-1) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Biologie (NaWi-Lehrer-4) [Bio2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Geschichte (GesWiss-Lehrer-1) |
| 3 | Geographie (GesWiss-Lehrer-1) | Physik (NaWi-Lehrer-4) [NaWi3] | Sport (Sportlehrer-2) [Sporthalle3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Gemeinschaftskunde (GesWiss-Lehrer-1) |
| 4 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Physik (NaWi-Lehrer-4) [NaWi3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Sport (Sportlehrer-2) [Sporthalle3] | Sport (Sportlehrer-2) [Sporthalle3] |
| 5 | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Kunst (Kunstlehrer-1) [Kunstraum2] | Musik (Musiklehrer-1) [Musikraum2] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) |
| 6 | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Gemeinschaftskunde (GesWiss-Lehrer-1) | Biologie (NaWi-Lehrer-4) [Bio2] | - |
| 7 | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | - | - | - |
| 8 | - | - | - | - | - |

### 7b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Musik (Musiklehrer-1) [Musikraum2] |
| 2 | Physik (NaWi-Lehrer-3) [NaWi3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Geschichte (GesWiss-Lehrer-5) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Biologie (NaWi-Lehrer-2) [Bio2] |
| 3 | Physik (NaWi-Lehrer-3) [NaWi3] | Gemeinschaftskunde (GesWiss-Lehrer-5) | Sport (Sportlehrer-3) [Sporthalle3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Sport (Sportlehrer-3) [Sporthalle3] |
| 4 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Geschichte (GesWiss-Lehrer-5) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Kunst (Kunstlehrer-1) [Kunstraum2] | Geographie (GesWiss-Lehrer-5) |
| 5 | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Gemeinschaftskunde (GesWiss-Lehrer-5) | Sport (Sportlehrer-3) [Sporthalle3] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) |
| 6 | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Geographie (GesWiss-Lehrer-5) | Biologie (NaWi-Lehrer-2) [Bio2] | - |
| 7 | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | - | - | - |
| 8 | - | - | - | - | - |

### 7c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Biologie (NaWi-Lehrer-2) [Bio2] |
| 2 | Sport (Sportlehrer-3) [Sporthalle3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Kunst (Kunstlehrer-1) [Kunstraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Geographie (GesWiss-Lehrer-4) |
| 3 | Gemeinschaftskunde (GesWiss-Lehrer-4) | Biologie (NaWi-Lehrer-2) [Bio2] | Gemeinschaftskunde (GesWiss-Lehrer-4) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Physik (NaWi-Lehrer-2) [NaWi3] |
| 4 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Sport (Sportlehrer-3) [Sporthalle3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Sport (Sportlehrer-3) [Sporthalle3] | Physik (NaWi-Lehrer-2) [NaWi3] |
| 5 | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Geschichte (GesWiss-Lehrer-4) | Geschichte (GesWiss-Lehrer-4) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) |
| 6 | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Geographie (GesWiss-Lehrer-4) | Musik (Musiklehrer-1) [Musikraum2] | - |
| 7 | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | - | - | - |
| 8 | - | - | - | - | - |

### 7d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | - |
| 2 | Biologie (NaWi-Lehrer-4) [Bio2] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Sport (Sportlehrer-2) [Sporthalle3] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Geographie (GesWiss-Lehrer-2) |
| 3 | Sport (Sportlehrer-2) [Sporthalle3] | Musik (Musiklehrer-1) [Musikraum2] | Geschichte (GesWiss-Lehrer-2) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Sport (Sportlehrer-2) [Sporthalle3] |
| 4 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | Geographie (GesWiss-Lehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-2 / Religionslehrer-ev-1 / Religionslehrer-kath-2) | Gemeinschaftskunde (GesWiss-Lehrer-2) | Biologie (NaWi-Lehrer-4) [Bio2] |
| 5 | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Physik (NaWi-Lehrer-4) [NaWi3] | Geschichte (GesWiss-Lehrer-2) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) |
| 6 | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-6 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-3 / Mathematiklehrer-5 / Mathematiklehrer-3) | Physik (NaWi-Lehrer-4) [NaWi3] | Kunst (Kunstlehrer-1) [Kunstraum2] | Gemeinschaftskunde (GesWiss-Lehrer-2) |
| 7 | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-4) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-2 / Franzoesisch-Lehrer-3 / Franzoesisch-Lehrer-3 / Technik-Lehrer-3 / Technik-Lehrer-1) [AESraum2 / Technikraum2] | - | - | - |
| 8 | - | - | - | - | - |

### 8a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Geographie (GesWiss-Lehrer-2) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Physik (NaWi-Lehrer-5) [NaWi3] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) |
| 2 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Physik (NaWi-Lehrer-5) [NaWi3] | Sport (Sportlehrer-2) [Sporthalle3] |
| 3 | Geschichte (GesWiss-Lehrer-2) | Gemeinschaftskunde (GesWiss-Lehrer-2) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Kunst (Kunstlehrer-1) [Kunstraum2] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) |
| 4 | Biologie (NaWi-Lehrer-3) [Bio2] | Sport (Sportlehrer-2) [Sporthalle3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Chemie (NaWi-Lehrer-1) [NaWi3] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) |
| 5 | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) | Chemie (NaWi-Lehrer-1) [NaWi3] | Biologie (NaWi-Lehrer-3) [Bio2] |
| 6 | Gemeinschaftskunde (GesWiss-Lehrer-2) | Musik (Musiklehrer-1) [Musikraum2] | Sport (Sportlehrer-2) [Sporthalle3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) |
| 7 | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | - | Geographie (GesWiss-Lehrer-2) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - |
| 8 | - | - | Geschichte (GesWiss-Lehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - |

### 8b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Geographie (GesWiss-Lehrer-3) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Kunst (Kunstlehrer-1) [Kunstraum2] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) |
| 2 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Chemie (NaWi-Lehrer-1) [NaWi3] | Biologie (NaWi-Lehrer-1) [Bio2] |
| 3 | Musik (Musiklehrer-1) [Musikraum2] | Sport (Sportlehrer-1) [Sporthalle3] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Chemie (NaWi-Lehrer-1) [NaWi3] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) |
| 4 | Geschichte (GesWiss-Lehrer-3) | Biologie (NaWi-Lehrer-1) [Bio2] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Physik (NaWi-Lehrer-2) [NaWi3] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) |
| 5 | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) | Physik (NaWi-Lehrer-2) [NaWi3] | Sport (Sportlehrer-1) [Sporthalle3] |
| 6 | Gemeinschaftskunde (GesWiss-Lehrer-3) | Gemeinschaftskunde (GesWiss-Lehrer-3) | Geschichte (GesWiss-Lehrer-3) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) |
| 7 | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | - | Geographie (GesWiss-Lehrer-3) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - |
| 8 | - | - | Sport (Sportlehrer-1) [Sporthalle3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - |

### 8c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Sport (Sportlehrer-2) [Sporthalle3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Physik (NaWi-Lehrer-3) [NaWi3] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) |
| 2 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Physik (NaWi-Lehrer-3) [NaWi3] | Biologie (NaWi-Lehrer-4) [Bio2] |
| 3 | Kunst (Kunstlehrer-1) [Kunstraum2] | Gemeinschaftskunde (GesWiss-Lehrer-3) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Geographie (GesWiss-Lehrer-3) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) |
| 4 | Sport (Sportlehrer-2) [Sporthalle3] | Geschichte (GesWiss-Lehrer-3) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Geschichte (GesWiss-Lehrer-3) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) |
| 5 | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) | Gemeinschaftskunde (GesWiss-Lehrer-3) | Sport (Sportlehrer-2) [Sporthalle3] |
| 6 | Musik (Musiklehrer-1) [Musikraum2] | Biologie (NaWi-Lehrer-4) [Bio2] | Chemie (NaWi-Lehrer-1) [NaWi3] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) |
| 7 | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | - | Chemie (NaWi-Lehrer-1) [NaWi3] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - |
| 8 | - | - | Geographie (GesWiss-Lehrer-3) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - |

### 8d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Biologie (NaWi-Lehrer-2) [Bio2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Geographie (GesWiss-Lehrer-3) | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) |
| 2 | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Geschichte (GesWiss-Lehrer-3) | Sport (Sportlehrer-3) [Sporthalle3] |
| 3 | Geschichte (GesWiss-Lehrer-3) | Physik (NaWi-Lehrer-3) [NaWi3] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Sport (Sportlehrer-3) [Sporthalle3] | Englisch-E-1 / Englisch-E-2 / Englisch-G-1 / Englisch-G-2 (Englischlehrer-4 / Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-2) |
| 4 | Sport (Sportlehrer-3) [Sporthalle3] | Physik (NaWi-Lehrer-3) [NaWi3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Chemie (NaWi-Lehrer-4) [NaWi3] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) |
| 5 | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-2 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) | Chemie (NaWi-Lehrer-4) [NaWi3] | Gemeinschaftskunde (GesWiss-Lehrer-3) |
| 6 | Biologie (NaWi-Lehrer-2) [Bio2] | Kunst (Kunstlehrer-1) [Kunstraum2] | Musik (Musiklehrer-1) [Musikraum2] | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 / Mathematik-G-2 (Mathematiklehrer-3 / Mathematiklehrer-4 / Mathematiklehrer-1 / Mathematiklehrer-2) |
| 7 | Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-2 / Deutschlehrer-2 / Deutschlehrer-3 / Deutschlehrer-2) | Geographie (GesWiss-Lehrer-3) | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - |
| 8 | - | Gemeinschaftskunde (GesWiss-Lehrer-3) | - | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - |

### 9a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) | Geographie (GesWiss-Lehrer-2) | Sport (Sportlehrer-3) [Sporthalle3] | Geschichte (GesWiss-Lehrer-2) |
| 2 | Biologie (NaWi-Lehrer-1) [Bio2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) | Musik (Musiklehrer-1) [Musikraum2] | Kunst (Kunstlehrer-1) [Kunstraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) |
| 3 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Biologie (NaWi-Lehrer-1) [Bio2] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) |
| 4 | Gemeinschaftskunde (GesWiss-Lehrer-2) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) | Gemeinschaftskunde (GesWiss-Lehrer-2) | Physik (NaWi-Lehrer-5) [NaWi3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) |
| 5 | Geographie (GesWiss-Lehrer-2) | Geschichte (GesWiss-Lehrer-2) | Sport (Sportlehrer-3) [Sporthalle3] | Physik (NaWi-Lehrer-5) [NaWi3] | Sport (Sportlehrer-3) [Sporthalle3] |
| 6 | Chemie (NaWi-Lehrer-3) [NaWi3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] |
| 7 | Chemie (NaWi-Lehrer-3) [NaWi3] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-2 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - |
| 8 | - | - | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-2 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - |

### 9b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) | Sport (Sportlehrer-2) [Sporthalle3] | Physik (NaWi-Lehrer-4) [NaWi3] | Geographie (GesWiss-Lehrer-5) |
| 2 | Kunst (Kunstlehrer-1) [Kunstraum2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) | Biologie (NaWi-Lehrer-3) [Bio2] | Physik (NaWi-Lehrer-4) [NaWi3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) |
| 3 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Sport (Sportlehrer-2) [Sporthalle3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) |
| 4 | Musik (Musiklehrer-1) [Musikraum2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) | Chemie (NaWi-Lehrer-1) [NaWi3] | Gemeinschaftskunde (GesWiss-Lehrer-5) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) |
| 5 | Sport (Sportlehrer-2) [Sporthalle3] | Biologie (NaWi-Lehrer-3) [Bio2] | Chemie (NaWi-Lehrer-1) [NaWi3] | Geschichte (GesWiss-Lehrer-5) | Geschichte (GesWiss-Lehrer-5) |
| 6 | Geographie (GesWiss-Lehrer-5) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] |
| 7 | Gemeinschaftskunde (GesWiss-Lehrer-5) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-2 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - |
| 8 | - | - | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-2 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - |

### 9c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) | Chemie (NaWi-Lehrer-1) [NaWi3] | Biologie (NaWi-Lehrer-1) [Bio2] | Sport (Sportlehrer-2) [Sporthalle3] |
| 2 | Musik (Musiklehrer-1) [Musikraum2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) | Chemie (NaWi-Lehrer-1) [NaWi3] | Sport (Sportlehrer-2) [Sporthalle3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) |
| 3 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Geographie (GesWiss-Lehrer-1) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) |
| 4 | Physik (NaWi-Lehrer-5) [NaWi3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) | Geographie (GesWiss-Lehrer-1) | Gemeinschaftskunde (GesWiss-Lehrer-1) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) |
| 5 | Physik (NaWi-Lehrer-5) [NaWi3] | Geschichte (GesWiss-Lehrer-1) | Geschichte (GesWiss-Lehrer-1) | Kunst (Kunstlehrer-1) [Kunstraum2] | Gemeinschaftskunde (GesWiss-Lehrer-1) |
| 6 | Sport (Sportlehrer-2) [Sporthalle3] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] |
| 7 | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-2 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - |
| 8 | - | - | Biologie (NaWi-Lehrer-1) [Bio2] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-2 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - |

### 9d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) | Physik (NaWi-Lehrer-5) [NaWi3] | Sport (Sportlehrer-2) [Sporthalle3] | Biologie (NaWi-Lehrer-1) [Bio2] |
| 2 | Gemeinschaftskunde (GesWiss-Lehrer-4) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) | Physik (NaWi-Lehrer-5) [NaWi3] | Geographie (GesWiss-Lehrer-4) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) |
| 3 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Gemeinschaftskunde (GesWiss-Lehrer-4) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) |
| 4 | Kunst (Kunstlehrer-1) [Kunstraum2] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) | Geographie (GesWiss-Lehrer-4) | Chemie (NaWi-Lehrer-3) [NaWi3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-2 / Deutschlehrer-5 / Deutschlehrer-3) |
| 5 | Musik (Musiklehrer-1) [Musikraum2] | Geschichte (GesWiss-Lehrer-4) | Sport (Sportlehrer-2) [Sporthalle3] | Chemie (NaWi-Lehrer-3) [NaWi3] | Geschichte (GesWiss-Lehrer-4) |
| 6 | Biologie (NaWi-Lehrer-1) [Bio2] | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-2 / Englischlehrer-2 / Englischlehrer-5 / Englischlehrer-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-1 / Franzoesisch-Lehrer-1 / Technik-Lehrer-1 / Technik-Lehrer-2) [AESraum2 / Technikraum2] |
| 7 | Sport (Sportlehrer-2) [Sporthalle3] | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-2 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2 / Mathematiklehrer-2) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - |
| 8 | - | - | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-2 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - |

### 10a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Gemeinschaftskunde (GesWiss-Lehrer-1) | Physik (NaWi-Lehrer-5) [NaWi3] | Geographie (GesWiss-Lehrer-1) | Gemeinschaftskunde (GesWiss-Lehrer-1) | Biologie (NaWi-Lehrer-5) [Bio2] |
| 2 | Sport (Sportlehrer-1) [Sporthalle3] | Physik (NaWi-Lehrer-5) [NaWi3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Sport (Sportlehrer-1) [Sporthalle3] |
| 3 | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | Biologie (NaWi-Lehrer-5) [Bio2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) |
| 4 | Geographie (GesWiss-Lehrer-1) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Kunst (Kunstlehrer-1) [Kunstraum2] | Musik (Musiklehrer-1) [Musikraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) |
| 5 | Geschichte (GesWiss-Lehrer-1) | Chemie (NaWi-Lehrer-1) [NaWi3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-3) [AESraum2 / Technikraum2] |
| 6 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Chemie (NaWi-Lehrer-1) [NaWi3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Sport (Sportlehrer-1) [Sporthalle3] | Geschichte (GesWiss-Lehrer-1) |
| 7 | - | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | - |
| 8 | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - | - | - |

### 10b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Chemie (NaWi-Lehrer-2) [NaWi3] | Physik (NaWi-Lehrer-1) [NaWi3] | Gemeinschaftskunde (GesWiss-Lehrer-5) | Musik (Musiklehrer-1) [Musikraum2] | Kunst (Kunstlehrer-1) [Kunstraum2] |
| 2 | Chemie (NaWi-Lehrer-2) [NaWi3] | Physik (NaWi-Lehrer-1) [NaWi3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Geographie (GesWiss-Lehrer-5) |
| 3 | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | Biologie (NaWi-Lehrer-1) [Bio2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) |
| 4 | Gemeinschaftskunde (GesWiss-Lehrer-5) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Geschichte (GesWiss-Lehrer-5) | Sport (Sportlehrer-1) [Sporthalle3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) |
| 5 | Geographie (GesWiss-Lehrer-5) | Sport (Sportlehrer-1) [Sporthalle3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-3) [AESraum2 / Technikraum2] |
| 6 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Geschichte (GesWiss-Lehrer-5) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Biologie (NaWi-Lehrer-1) [Bio2] | Sport (Sportlehrer-1) [Sporthalle3] |
| 7 | - | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | - |
| 8 | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - | - | - |

### 10c

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Gemeinschaftskunde (GesWiss-Lehrer-4) | Musik (Musiklehrer-2) [Musikraum2] | Geschichte (GesWiss-Lehrer-4) | Geographie (GesWiss-Lehrer-4) | Geographie (GesWiss-Lehrer-4) |
| 2 | Sport (Sportlehrer-2) [Sporthalle3] | Sport (Sportlehrer-2) [Sporthalle3] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Biologie (NaWi-Lehrer-5) [Bio2] |
| 3 | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | Kunst (Kunstlehrer-2) [Kunstraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) |
| 4 | Chemie (NaWi-Lehrer-1) [NaWi3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Sport (Sportlehrer-2) [Sporthalle3] | Geschichte (GesWiss-Lehrer-4) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) |
| 5 | Chemie (NaWi-Lehrer-1) [NaWi3] | Physik (NaWi-Lehrer-5) [NaWi3] | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-3) [AESraum2 / Technikraum2] |
| 6 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Physik (NaWi-Lehrer-5) [NaWi3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Biologie (NaWi-Lehrer-5) [Bio2] | Gemeinschaftskunde (GesWiss-Lehrer-4) |
| 7 | - | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | - |
| 8 | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - | - | - |

### 10d

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Physik (NaWi-Lehrer-5) [NaWi3] | Gemeinschaftskunde (GesWiss-Lehrer-5) | Biologie (NaWi-Lehrer-3) [Bio2] | Kunst (Kunstlehrer-2) [Kunstraum2] | Sport (Sportlehrer-1) [Sporthalle3] |
| 2 | Physik (NaWi-Lehrer-5) [NaWi3] | Geschichte (GesWiss-Lehrer-5) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-3) [AESraum2 / Technikraum2] | Biologie (NaWi-Lehrer-3) [Bio2] |
| 3 | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | Geographie (GesWiss-Lehrer-5) | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) |
| 4 | Chemie (NaWi-Lehrer-2) [NaWi3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Sport (Sportlehrer-1) [Sporthalle3] | Musik (Musiklehrer-2) [Musikraum2] | Englisch-A-1 / Englisch-E-1 / Englisch-E-2 / Englisch-G-1 (Englischlehrer-5 / Englischlehrer-1 / Englischlehrer-2 / Englischlehrer-2) |
| 5 | Chemie (NaWi-Lehrer-2) [NaWi3] | Geographie (GesWiss-Lehrer-5) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Ethik / Religion-ev-1 / Religion-ev-2 / Religion-kath (Ethiklehrer-2 / Religionslehrer-ev-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | AES / Franzoesisch-1 / Franzoesisch-2 / Technik-1 / Technik-2 (AES-Lehrer-1 / Franzoesisch-Lehrer-2 / Franzoesisch-Lehrer-2 / Technik-Lehrer-1 / Technik-Lehrer-3) [AESraum2 / Technikraum2] |
| 6 | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Sport (Sportlehrer-1) [Sporthalle3] | Mathematik-A-1 / Mathematik-E-1 / Mathematik-E-2 / Mathematik-G-1 (Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-1 / Mathematiklehrer-4) | Gemeinschaftskunde (GesWiss-Lehrer-5) | Geschichte (GesWiss-Lehrer-5) |
| 7 | - | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | Deutsch-A-1 / Deutsch-E-1 / Deutsch-E-2 / Deutsch-G-1 / Deutsch-G-2 (Deutschlehrer-1 / Deutschlehrer-3 / Deutschlehrer-6 / Deutschlehrer-4 / Deutschlehrer-3) | - |
| 8 | - | BK-Profil / IMP / Musik-Profil / NwT / Sport-Profil (Kunstlehrer-1 / Mathematiklehrer-1 / Musiklehrer-1 / NaWi-Lehrer-4 / Sportlehrer-1) [Kunstraum2 / Computerraum2 / Musikraum2 / Technikraum2 / Sporthalle3] | - | - | - |

## Lehrkraefte

### Deutschlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 5b Deutsch | 5d Deutsch | 6d Deutsch | - | 5d Deutsch |
| 2 | 5d Deutsch | 9b Deutsch-A-1 | 5d Deutsch | - | 6a Deutsch |
| 3 | 10b Deutsch-A-1 | 10b Deutsch-A-1 | 6d Deutsch | - | 9b Deutsch-A-1 |
| 4 | 6a Deutsch | 9b Deutsch-A-1 | 6a Deutsch | 5b Deutsch | 9b Deutsch-A-1 |
| 5 | 5b Deutsch | 6d Deutsch | 5d Deutsch | - | 6a Deutsch |
| 6 | 6d Deutsch | 6d Deutsch | 6a Deutsch | 5b Deutsch | 5b Deutsch |
| 7 | - | 10b Deutsch-A-1 | - | 10b Deutsch-A-1 | - |
| 8 | - | - | - | - | - |

### Deutschlehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 7b Deutsch-E-2 | 8b Deutsch-E-2 | - | - |
| 2 | - | 9c Deutsch-E-2 | 8b Deutsch-E-2 | - | - |
| 3 | - | - | - | 7b Deutsch-E-2 | 9c Deutsch-E-2 |
| 4 | - | 9c Deutsch-E-2 | - | - | 9c Deutsch-E-2 |
| 5 | - | 7b Deutsch-E-2 | - | - | - |
| 6 | 7b Deutsch-E-2 | - | - | 8b Deutsch-E-2 | - |
| 7 | 8b Deutsch-E-2 | - | - | - | - |
| 8 | - | - | - | - | - |

### Deutschlehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | 8a Deutsch-G-1 | - | - |
| 2 | - | 9c Deutsch-E-1 | 8a Deutsch-G-1 | - | - |
| 3 | 10c Deutsch-E-1 | 10c Deutsch-E-1 | - | - | 9c Deutsch-E-1 |
| 4 | - | 9c Deutsch-E-1 | - | - | 9c Deutsch-E-1 |
| 5 | 5a Deutsch | 5a Deutsch | - | 5a Deutsch | - |
| 6 | 5a Deutsch | 5a Deutsch | - | 8a Deutsch-G-1 | - |
| 7 | 8a Deutsch-G-1 | 10c Deutsch-E-1 | - | 10c Deutsch-E-1 | - |
| 8 | - | - | - | - | - |

### Deutschlehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | - | - | - | - |
| 3 | 10a Deutsch-G-1 | 10a Deutsch-G-1 | - | - | - |
| 4 | 6c Deutsch | 6c Deutsch | - | 6c Deutsch | - |
| 5 | - | - | - | 6c Deutsch | - |
| 6 | - | 6c Deutsch | - | - | - |
| 7 | - | 10a Deutsch-G-1 | - | 10a Deutsch-G-1 | - |
| 8 | - | - | - | - | - |

### Deutschlehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 7a Deutsch-G-1 | 6b Deutsch | - | 6b Deutsch |
| 2 | - | 9a Deutsch-G-1 | - | 6b Deutsch | 6b Deutsch |
| 3 | - | 6b Deutsch | - | 7a Deutsch-G-1 | 9a Deutsch-G-1 |
| 4 | - | 9a Deutsch-G-1 | - | - | 9a Deutsch-G-1 |
| 5 | - | 7a Deutsch-G-1 | - | - | - |
| 6 | 7a Deutsch-G-1 | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Deutschlehrer-6

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 7b Deutsch-E-1 | - | - | - |
| 2 | - | - | - | - | - |
| 3 | 10c Deutsch-E-2 | 10c Deutsch-E-2 | - | 7b Deutsch-E-1 | - |
| 4 | 5c Deutsch | 5c Deutsch | - | - | - |
| 5 | - | 7b Deutsch-E-1 | - | - | - |
| 6 | 7b Deutsch-E-1 | 5c Deutsch | 5c Deutsch | 5c Deutsch | - |
| 7 | - | 10c Deutsch-E-2 | - | 10c Deutsch-E-2 | - |
| 8 | - | - | - | - | - |

### Mathematiklehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | - | - | - | 5d Mathematik |
| 3 | - | - | 8c IMP [Computerraum2] | - | 10d Mathematik-A-1 |
| 4 | 5d Mathematik | 10d Mathematik-A-1 | 5d Mathematik | - | 8b Mathematik-G-1 |
| 5 | 8b Mathematik-G-1 | - | 8b Mathematik-G-1 | - | 5d Mathematik |
| 6 | 10d Mathematik-A-1 | 5d Mathematik | 10d Mathematik-A-1 | - | 8b Mathematik-G-1 |
| 7 | - | 9c IMP [Computerraum2] | 10c IMP [Computerraum2] | 8c IMP [Computerraum2] | - |
| 8 | - | 10c IMP [Computerraum2] | - | 9c IMP [Computerraum2] | - |

### Mathematiklehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 9d Mathematik-A-1 | 5a Mathematik | - | - | - |
| 2 | 5a Mathematik | - | - | 5a Mathematik | - |
| 3 | 9d Mathematik-A-1 | - | - | - | - |
| 4 | 5a Mathematik | - | - | - | 8b Mathematik-G-2 |
| 5 | 8b Mathematik-G-2 | - | 8b Mathematik-G-2 | - | 5a Mathematik |
| 6 | - | - | 9d Mathematik-A-1 | - | 8b Mathematik-G-2 |
| 7 | - | - | 9d Mathematik-A-1 | - | - |
| 8 | - | - | - | - | - |

### Mathematiklehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 7d Mathematik-E-2 | - | - | 7d Mathematik-E-2 | - |
| 2 | 5c Mathematik | - | - | 7d Mathematik-E-2 | 5c Mathematik |
| 3 | - | - | - | 5c Mathematik | 6d Mathematik |
| 4 | 6d Mathematik | - | - | 6d Mathematik | 8b Mathematik-E-1 |
| 5 | 8b Mathematik-E-1 | 5c Mathematik | 8b Mathematik-E-1 | 5c Mathematik | 6d Mathematik |
| 6 | - | 7d Mathematik-E-2 | 6d Mathematik | - | 8b Mathematik-E-1 |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Mathematiklehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 6b Mathematik | - | 6c Mathematik | - | 6a Mathematik |
| 2 | 6c Mathematik | 6c Mathematik | 6c Mathematik | 5b Mathematik | 6c Mathematik |
| 3 | 6a Mathematik | 6a Mathematik | 6a Mathematik | 6b Mathematik | 10b Mathematik-G-1 |
| 4 | 5b Mathematik | 10b Mathematik-G-1 | 5b Mathematik | 6b Mathematik | 8d Mathematik-E-2 |
| 5 | 8d Mathematik-E-2 | 5b Mathematik | 8d Mathematik-E-2 | 5b Mathematik | 6b Mathematik |
| 6 | 10b Mathematik-G-1 | 6b Mathematik | 10b Mathematik-G-1 | - | 8d Mathematik-E-2 |
| 7 | 6a Mathematik | - | - | - | - |
| 8 | - | - | - | - | - |

### Mathematiklehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 7b Mathematik-G-1 | - | - | 7b Mathematik-G-1 | - |
| 2 | - | - | - | 7b Mathematik-G-1 | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | - | - | - |
| 6 | - | 7b Mathematik-G-1 | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 6c Englisch | 9b Englisch-G-1 | - | - | 6d Englisch |
| 2 | 6a Englisch | 6d Englisch | 10d Englisch-E-1 | 6d Englisch | 9b Englisch-G-1 |
| 3 | 6b Englisch | - | 6c Englisch | 10d Englisch-E-1 | 6b Englisch |
| 4 | 6b Englisch | - | 6b Englisch | 6a Englisch | 10d Englisch-E-1 |
| 5 | 7a Englisch-E-2 | - | 6a Englisch | 6d Englisch | 7a Englisch-E-2 |
| 6 | 6a Englisch | - | 6c Englisch | 9b Englisch-G-1 | 6c Englisch |
| 7 | 7a Englisch-E-2 | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 9a Englisch-A-1 | - | - | 8a Englisch-E-2 |
| 2 | - | 8a Englisch-E-2 | 10c Englisch-E-2 | 5c Englisch | 9a Englisch-A-1 |
| 3 | - | - | 5c Englisch | 10c Englisch-E-2 | 8a Englisch-E-2 |
| 4 | - | - | - | 5c Englisch | 10c Englisch-E-2 |
| 5 | 7b Englisch-G-1 | - | - | - | 7b Englisch-G-1 |
| 6 | 5c Englisch | - | - | 9a Englisch-A-1 | - |
| 7 | 7b Englisch-G-1 | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 5b Englisch | - | 5b Englisch | - |
| 2 | 5b Englisch | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | 5b Englisch | - | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | 8a Englisch-E-1 |
| 2 | - | 8a Englisch-E-1 | - | - | 5a Englisch |
| 3 | - | - | - | - | 8a Englisch-E-1 |
| 4 | - | - | - | 5a Englisch | 5a Englisch |
| 5 | 7b Englisch-G-2 | - | 5a Englisch | - | 7b Englisch-G-2 |
| 6 | - | - | - | - | - |
| 7 | 7b Englisch-G-2 | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 9c Englisch-E-2 | - | - | - |
| 2 | - | - | 10a Englisch-A-1 | - | 9c Englisch-E-2 |
| 3 | - | - | - | 10a Englisch-A-1 | 5d Englisch |
| 4 | - | - | - | 5d Englisch | 10a Englisch-A-1 |
| 5 | 7a Englisch-E-1 | - | - | 5d Englisch | 7a Englisch-E-1 |
| 6 | 5d Englisch | - | - | 9c Englisch-E-2 | - |
| 7 | 7a Englisch-E-1 | - | - | - | - |
| 8 | - | - | - | - | - |

### NaWi-Lehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 10b Physik [NaWi3] | 9c Chemie [NaWi3] | 9c Biologie [Bio2] | 9d Biologie [Bio2] |
| 2 | 9a Biologie [Bio2] | 10b Physik [NaWi3] | 9c Chemie [NaWi3] | 8b Chemie [NaWi3] | 8b Biologie [Bio2] |
| 3 | - | 9a Biologie [Bio2] | 10b Biologie [Bio2] | 8b Chemie [NaWi3] | - |
| 4 | 10c Chemie [NaWi3] | 8b Biologie [Bio2] | 9b Chemie [NaWi3] | 8a Chemie [NaWi3] | - |
| 5 | 10c Chemie [NaWi3] | 10a Chemie [NaWi3] | 9b Chemie [NaWi3] | 8a Chemie [NaWi3] | - |
| 6 | 9d Biologie [Bio2] | 10a Chemie [NaWi3] | 8c Chemie [NaWi3] | 10b Biologie [Bio2] | - |
| 7 | - | - | 8c Chemie [NaWi3] | - | - |
| 8 | - | - | 9c Biologie [Bio2] | - | - |

### NaWi-Lehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 10b Chemie [NaWi3] | 8d Biologie [Bio2] | 5b BNT [NaWi3] | - | 7c Biologie [Bio2] |
| 2 | 10b Chemie [NaWi3] | 6b BNT [NaWi3] | 6b BNT [NaWi3] | - | 7b Biologie [Bio2] |
| 3 | - | 7c Biologie [Bio2] | 5a BNT [NaWi3] | - | 7c Physik [NaWi3] |
| 4 | 10d Chemie [NaWi3] | 5a BNT [NaWi3] | 5a BNT [NaWi3] | 8b Physik [NaWi3] | 7c Physik [NaWi3] |
| 5 | 10d Chemie [NaWi3] | - | - | 8b Physik [NaWi3] | 5b BNT [NaWi3] |
| 6 | 8d Biologie [Bio2] | 5b BNT [NaWi3] | - | 7b Biologie [Bio2] | 6b BNT [NaWi3] |
| 7 | - | 9d NwT [Technikraum2] | - | 6b BNT [NaWi3] | - |
| 8 | - | - | - | 9d NwT [Technikraum2] | - |

### NaWi-Lehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | 10d Biologie [Bio2] | 8c Physik [NaWi3] | - |
| 2 | 7b Physik [NaWi3] | 5d BNT [NaWi3] | 9b Biologie [Bio2] | 8c Physik [NaWi3] | 10d Biologie [Bio2] |
| 3 | 7b Physik [NaWi3] | 8d Physik [NaWi3] | 5d BNT [NaWi3] | - | 5c BNT [NaWi3] |
| 4 | 8a Biologie [Bio2] | 8d Physik [NaWi3] | - | 9d Chemie [NaWi3] | 5d BNT [NaWi3] |
| 5 | 5c BNT [NaWi3] | 9b Biologie [Bio2] | - | 9d Chemie [NaWi3] | 8a Biologie [Bio2] |
| 6 | 9a Chemie [NaWi3] | - | - | - | 5c BNT [NaWi3] |
| 7 | 9a Chemie [NaWi3] | - | - | - | - |
| 8 | - | - | - | - | - |

### NaWi-Lehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | 9b Physik [NaWi3] | 6c BNT [NaWi3] |
| 2 | 7d Biologie [Bio2] | - | 7a Biologie [Bio2] | 9b Physik [NaWi3] | 8c Biologie [Bio2] |
| 3 | - | 7a Physik [NaWi3] | 8d NwT [Technikraum2] | 6a BNT [NaWi3] | 6a BNT [NaWi3] |
| 4 | - | 7a Physik [NaWi3] | 6c BNT [NaWi3] | 8d Chemie [NaWi3] | 7d Biologie [Bio2] |
| 5 | - | 6a BNT [NaWi3] | 7d Physik [NaWi3] | 8d Chemie [NaWi3] | 6c BNT [NaWi3] |
| 6 | - | 8c Biologie [Bio2] | 7d Physik [NaWi3] | 7a Biologie [Bio2] | 6a BNT [NaWi3] |
| 7 | - | 6c BNT [NaWi3] | 10d NwT [Technikraum2] | 8d NwT [Technikraum2] | - |
| 8 | - | 10d NwT [Technikraum2] | - | - | - |

### NaWi-Lehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 10d Physik [NaWi3] | 10a Physik [NaWi3] | 9d Physik [NaWi3] | 8a Physik [NaWi3] | 10a Biologie [Bio2] |
| 2 | 10d Physik [NaWi3] | 10a Physik [NaWi3] | 9d Physik [NaWi3] | 8a Physik [NaWi3] | 10c Biologie [Bio2] |
| 3 | 6d BNT [NaWi3] | 6d BNT [NaWi3] | 10a Biologie [Bio2] | 6d BNT [NaWi3] | - |
| 4 | 9c Physik [NaWi3] | 6d BNT [NaWi3] | - | 9a Physik [NaWi3] | - |
| 5 | 9c Physik [NaWi3] | 10c Physik [NaWi3] | - | 9a Physik [NaWi3] | - |
| 6 | - | 10c Physik [NaWi3] | - | 10c Biologie [Bio2] | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### GesWiss-Lehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 10a Gemeinschaftskunde | - | 10a Geographie | 10a Gemeinschaftskunde | 7a Geographie |
| 2 | 7a Geschichte | 5b Geographie | 5c Geschichte | 6c Geographie | 7a Geschichte |
| 3 | 7a Geographie | 9c Geographie | 6b Geographie | 5b Geographie | 7a Gemeinschaftskunde |
| 4 | 10a Geographie | 6b Geographie | 9c Geographie | 9c Gemeinschaftskunde | 5c Geschichte |
| 5 | 10a Geschichte | 9c Geschichte | 9c Geschichte | 6a Geographie | 9c Gemeinschaftskunde |
| 6 | 6c Geographie | 6a Geographie | 7a Gemeinschaftskunde | - | 10a Geschichte |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### GesWiss-Lehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 8a Geographie | 9a Geographie | - | 9a Geschichte |
| 2 | 6b Geschichte | 5c Geographie | 6a Geschichte | - | 7d Geographie |
| 3 | 8a Geschichte | 8a Gemeinschaftskunde | 7d Geschichte | - | 6c Geschichte |
| 4 | 9a Gemeinschaftskunde | 7d Geographie | 9a Gemeinschaftskunde | 7d Gemeinschaftskunde | 6a Geschichte |
| 5 | 9a Geographie | 9a Geschichte | 6c Geschichte | 7d Geschichte | 5c Geographie |
| 6 | 8a Gemeinschaftskunde | - | 6b Geschichte | - | 7d Gemeinschaftskunde |
| 7 | - | - | 8a Geographie | - | - |
| 8 | - | - | 8a Geschichte | - | - |

### GesWiss-Lehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 6d Geschichte | 8b Geographie | - | 8d Geographie | - |
| 2 | 6d Geographie | 5a Geographie | - | 8d Geschichte | - |
| 3 | 8d Geschichte | 8c Gemeinschaftskunde | - | 8c Geographie | - |
| 4 | 8b Geschichte | 8c Geschichte | - | 8c Geschichte | 6d Geographie |
| 5 | 5d Geschichte | 5d Geschichte | 6d Geschichte | 8c Gemeinschaftskunde | 8d Gemeinschaftskunde |
| 6 | 8b Gemeinschaftskunde | 8b Gemeinschaftskunde | 8b Geschichte | - | 5a Geographie |
| 7 | - | 8d Geographie | 8b Geographie | - | - |
| 8 | - | 8d Gemeinschaftskunde | 8c Geographie | - | - |

### GesWiss-Lehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 10c Gemeinschaftskunde | - | 10c Geschichte | 10c Geographie | 10c Geographie |
| 2 | 9d Gemeinschaftskunde | - | 5b Geschichte | 9d Geographie | 7c Geographie |
| 3 | 7c Gemeinschaftskunde | 9d Gemeinschaftskunde | 7c Gemeinschaftskunde | 5a Geschichte | 5a Geschichte |
| 4 | - | 5d Geographie | 9d Geographie | 10c Geschichte | 5b Geschichte |
| 5 | - | 9d Geschichte | 7c Geschichte | 7c Geschichte | 9d Geschichte |
| 6 | - | - | 7c Geographie | 5d Geographie | 10c Gemeinschaftskunde |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### GesWiss-Lehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 10d Gemeinschaftskunde | 10b Gemeinschaftskunde | - | 9b Geographie |
| 2 | - | 10d Geschichte | 7b Geschichte | - | 10b Geographie |
| 3 | - | 7b Gemeinschaftskunde | 10d Geographie | - | - |
| 4 | 10b Gemeinschaftskunde | 7b Geschichte | 10b Geschichte | 9b Gemeinschaftskunde | 7b Geographie |
| 5 | 10b Geographie | 10d Geographie | 7b Gemeinschaftskunde | 9b Geschichte | 9b Geschichte |
| 6 | 9b Geographie | 10b Geschichte | 7b Geographie | 10d Gemeinschaftskunde | 10d Geschichte |
| 7 | 9b Gemeinschaftskunde | - | - | - | - |
| 8 | - | - | - | - | - |

### Sportlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | 10d Sport [Sporthalle3] |
| 2 | 10a Sport [Sporthalle3] | - | - | - | 10a Sport [Sporthalle3] |
| 3 | - | 8b Sport [Sporthalle3] | 8a Sport-Profil [Sporthalle3] | - | 5b Sport [Sporthalle3] |
| 4 | - | 5b Sport [Sporthalle3] | 10d Sport [Sporthalle3] | 10b Sport [Sporthalle3] | 6b Sport [Sporthalle3] |
| 5 | - | 10b Sport [Sporthalle3] | 6b Sport [Sporthalle3] | 6b Sport [Sporthalle3] | 8b Sport [Sporthalle3] |
| 6 | - | 10d Sport [Sporthalle3] | 5b Sport [Sporthalle3] | 10a Sport [Sporthalle3] | 10b Sport [Sporthalle3] |
| 7 | - | 9a Sport-Profil [Sporthalle3] | 10a Sport-Profil [Sporthalle3] | 8a Sport-Profil [Sporthalle3] | - |
| 8 | - | 10a Sport-Profil [Sporthalle3] | 8b Sport [Sporthalle3] | 9a Sport-Profil [Sporthalle3] | - |

### Sportlehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 5d Sport [Sporthalle3] | 8c Sport [Sporthalle3] | 9b Sport [Sporthalle3] | 9d Sport [Sporthalle3] | 9c Sport [Sporthalle3] |
| 2 | 10c Sport [Sporthalle3] | 10c Sport [Sporthalle3] | 7d Sport [Sporthalle3] | 9c Sport [Sporthalle3] | 8a Sport [Sporthalle3] |
| 3 | 7d Sport [Sporthalle3] | 9b Sport [Sporthalle3] | 7a Sport [Sporthalle3] | 5d Sport [Sporthalle3] | 7d Sport [Sporthalle3] |
| 4 | 8c Sport [Sporthalle3] | 8a Sport [Sporthalle3] | 10c Sport [Sporthalle3] | 7a Sport [Sporthalle3] | 7a Sport [Sporthalle3] |
| 5 | 9b Sport [Sporthalle3] | - | 9d Sport [Sporthalle3] | - | 8c Sport [Sporthalle3] |
| 6 | 9c Sport [Sporthalle3] | - | 8a Sport [Sporthalle3] | - | 5d Sport [Sporthalle3] |
| 7 | 9d Sport [Sporthalle3] | - | - | - | - |
| 8 | - | - | - | - | - |

### Sportlehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 5a Sport [Sporthalle3] | 5c Sport [Sporthalle3] | 6a Sport [Sporthalle3] | 9a Sport [Sporthalle3] | 5c Sport [Sporthalle3] |
| 2 | 7c Sport [Sporthalle3] | 6a Sport [Sporthalle3] | 6d Sport [Sporthalle3] | 6a Sport [Sporthalle3] | 8d Sport [Sporthalle3] |
| 3 | 6c Sport [Sporthalle3] | 6c Sport [Sporthalle3] | 7b Sport [Sporthalle3] | 8d Sport [Sporthalle3] | 7b Sport [Sporthalle3] |
| 4 | 8d Sport [Sporthalle3] | 7c Sport [Sporthalle3] | 5c Sport [Sporthalle3] | 7c Sport [Sporthalle3] | 6c Sport [Sporthalle3] |
| 5 | - | - | 9a Sport [Sporthalle3] | 7b Sport [Sporthalle3] | 9a Sport [Sporthalle3] |
| 6 | - | - | 5a Sport [Sporthalle3] | 5a Sport [Sporthalle3] | 6d Sport [Sporthalle3] |
| 7 | - | - | - | 6d Sport [Sporthalle3] | - |
| 8 | - | - | - | - | - |

### Musiklehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | 10b Musik [Musikraum2] | 7b Musik [Musikraum2] |
| 2 | 9c Musik [Musikraum2] | - | 9a Musik [Musikraum2] | 5d Musik [Musikraum2] | - |
| 3 | 8b Musik [Musikraum2] | 7d Musik [Musikraum2] | 8c Musik-Profil [Musikraum2] | 6c Musik [Musikraum2] | - |
| 4 | 9b Musik [Musikraum2] | 6a Musik [Musikraum2] | 6d Musik [Musikraum2] | 10a Musik [Musikraum2] | - |
| 5 | 9d Musik [Musikraum2] | 6b Musik [Musikraum2] | 5c Musik [Musikraum2] | 7a Musik [Musikraum2] | - |
| 6 | 8c Musik [Musikraum2] | 8a Musik [Musikraum2] | 8d Musik [Musikraum2] | 7c Musik [Musikraum2] | - |
| 7 | - | 9c Musik-Profil [Musikraum2] | 10c Musik-Profil [Musikraum2] | 8c Musik-Profil [Musikraum2] | - |
| 8 | - | 10c Musik-Profil [Musikraum2] | - | 9c Musik-Profil [Musikraum2] | - |

### Musiklehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 10c Musik [Musikraum2] | - | - | - |
| 2 | - | - | 5a Musik [Musikraum2] | - | - |
| 3 | - | - | 5b Musik [Musikraum2] | - | - |
| 4 | - | - | - | 10d Musik [Musikraum2] | - |
| 5 | - | - | - | - | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Kunstlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 6a Kunst [Kunstraum2] | - | 5c Kunst [Kunstraum2] | 8b Kunst [Kunstraum2] | 10b Kunst [Kunstraum2] |
| 2 | 9b Kunst [Kunstraum2] | - | 7c Kunst [Kunstraum2] | 9a Kunst [Kunstraum2] | 6d Kunst [Kunstraum2] |
| 3 | 8c Kunst [Kunstraum2] | - | 8d BK-Profil [Kunstraum2] | 8a Kunst [Kunstraum2] | - |
| 4 | 9d Kunst [Kunstraum2] | - | 10a Kunst [Kunstraum2] | 7b Kunst [Kunstraum2] | - |
| 5 | - | 6c Kunst [Kunstraum2] | 7a Kunst [Kunstraum2] | 9c Kunst [Kunstraum2] | - |
| 6 | 6b Kunst [Kunstraum2] | 8d Kunst [Kunstraum2] | 5d Kunst [Kunstraum2] | 7d Kunst [Kunstraum2] | - |
| 7 | - | 9d BK-Profil [Kunstraum2] | 10d BK-Profil [Kunstraum2] | 8d BK-Profil [Kunstraum2] | - |
| 8 | - | 10d BK-Profil [Kunstraum2] | - | 9d BK-Profil [Kunstraum2] | - |

### Kunstlehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | 10d Kunst [Kunstraum2] | 5a Kunst [Kunstraum2] |
| 2 | - | - | - | - | 5b Kunst [Kunstraum2] |
| 3 | - | - | 10c Kunst [Kunstraum2] | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | - | - | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Religionslehrer-ev-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 8d Religion-ev-2 | - | - | - | - |
| 2 | - | 7d Religion-ev-2 | - | - | - |
| 3 | 5c Religion-ev-1 | 5c Religion-ev-1 | 9d Religion-ev-2 | - | - |
| 4 | - | - | 7d Religion-ev-2 | - | - |
| 5 | - | - | 10d Religion-ev-2 | 10d Religion-ev-2 | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | 9d Religion-ev-2 | - |
| 8 | - | - | - | 8d Religion-ev-2 | - |

### Religionslehrer-ev-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 6d Religion-ev-2 | - | - | - |
| 2 | - | 7c Religion-ev-1 | - | - | - |
| 3 | 5d Religion-ev-2 | 5d Religion-ev-2 | - | - | - |
| 4 | - | - | 7c Religion-ev-1 | - | - |
| 5 | - | - | - | - | - |
| 6 | - | - | - | 6d Religion-ev-2 | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Religionslehrer-ev-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 8c Religion-ev-1 | 6c Religion-ev-1 | - | - | - |
| 2 | - | - | - | - | - |
| 3 | - | - | 9c Religion-ev-1 | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | - | - | - |
| 6 | - | - | - | 6c Religion-ev-1 | - |
| 7 | - | - | - | 9c Religion-ev-1 | - |
| 8 | - | - | - | 8c Religion-ev-1 | - |

### Religionslehrer-kath-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 8d Religion-kath | - | - | - | - |
| 2 | - | - | - | - | - |
| 3 | - | - | 9d Religion-kath | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | 10d Religion-kath | 10d Religion-kath | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | 9d Religion-kath | - |
| 8 | - | - | - | 8d Religion-kath | - |

### Religionslehrer-kath-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 6d Religion-kath | - | - | - |
| 2 | - | 7d Religion-kath | - | - | - |
| 3 | 5d Religion-kath | 5d Religion-kath | - | - | - |
| 4 | - | - | 7d Religion-kath | - | - |
| 5 | - | - | - | - | - |
| 6 | - | - | - | 6d Religion-kath | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Ethiklehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 8b Ethik | - | - | - | - |
| 2 | - | 7b Ethik | - | - | - |
| 3 | - | - | 9b Ethik | - | - |
| 4 | - | - | 7b Ethik | - | - |
| 5 | - | - | - | - | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | 9b Ethik | - |
| 8 | - | - | - | 8b Ethik | - |

### Ethiklehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 6b Ethik | - | - | - |
| 2 | - | - | - | - | - |
| 3 | 5b Ethik | 5b Ethik | - | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | 10b Ethik | 10b Ethik | - |
| 6 | - | - | - | 6b Ethik | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Technik-Lehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | 7d Technik-2 [Technikraum2] | 6d Technik-2 [Technikraum2] | - |
| 2 | - | - | - | 10c Technik-1 [Technikraum2] | - |
| 3 | - | - | - | 9c Technik-1 [Technikraum2] | - |
| 4 | 7d Technik-2 [Technikraum2] | - | - | - | - |
| 5 | 6d Technik-2 [Technikraum2] | - | - | - | 10c Technik-1 [Technikraum2] |
| 6 | - | 9c Technik-1 [Technikraum2] | - | - | 9c Technik-1 [Technikraum2] |
| 7 | - | 7d Technik-2 [Technikraum2] | - | - | - |
| 8 | - | - | - | - | - |

### Technik-Lehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | 8c Technik-1 [Technikraum2] | - | - | - | - |
| 3 | - | - | - | 9d Technik-2 [Technikraum2] | - |
| 4 | - | - | 8c Technik-1 [Technikraum2] | - | - |
| 5 | - | 8c Technik-1 [Technikraum2] | - | - | - |
| 6 | - | 9d Technik-2 [Technikraum2] | - | - | 9d Technik-2 [Technikraum2] |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Technik-Lehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | 7c Technik-1 [Technikraum2] | - | - |
| 2 | 8d Technik-2 [Technikraum2] | - | - | 10d Technik-2 [Technikraum2] | - |
| 3 | - | - | - | - | - |
| 4 | 7c Technik-1 [Technikraum2] | - | 8d Technik-2 [Technikraum2] | - | - |
| 5 | - | 8d Technik-2 [Technikraum2] | - | - | 10d Technik-2 [Technikraum2] |
| 6 | - | - | - | - | - |
| 7 | - | 7c Technik-1 [Technikraum2] | - | - | - |
| 8 | - | - | - | - | - |

### AES-Lehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | 8b AES [AESraum2] | - | - | 10b AES [AESraum2] | - |
| 3 | - | - | - | 9b AES [AESraum2] | - |
| 4 | - | - | 8b AES [AESraum2] | - | - |
| 5 | - | 8b AES [AESraum2] | - | - | 10b AES [AESraum2] |
| 6 | - | 9b AES [AESraum2] | - | - | 9b AES [AESraum2] |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### AES-Lehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | 7b AES [AESraum2] | 6b AES [AESraum2] | - |
| 2 | - | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | 7b AES [AESraum2] | - | - | - | - |
| 5 | 6b AES [AESraum2] | - | - | - | - |
| 6 | - | - | - | - | - |
| 7 | - | 7b AES [AESraum2] | - | - | - |
| 8 | - | - | - | - | - |

### Franzoesisch-Lehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | 8c Franzoesisch-2 | - | - | - | - |
| 3 | - | - | - | 9c Franzoesisch-2 | - |
| 4 | - | - | 8c Franzoesisch-2 | - | - |
| 5 | - | 8c Franzoesisch-2 | - | - | - |
| 6 | - | 9c Franzoesisch-2 | - | - | 9c Franzoesisch-2 |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Franzoesisch-Lehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | 6c Franzoesisch-2 | - |
| 2 | - | - | - | 10c Franzoesisch-2 | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | 6c Franzoesisch-2 | - | - | - | 10c Franzoesisch-2 |
| 6 | - | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Franzoesisch-Lehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | 7c Franzoesisch-2 | - | - |
| 2 | - | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | 7c Franzoesisch-2 | - | - | - | - |
| 5 | - | - | - | - | - |
| 6 | - | - | - | - | - |
| 7 | - | 7c Franzoesisch-2 | - | - | - |
| 8 | - | - | - | - | - |
