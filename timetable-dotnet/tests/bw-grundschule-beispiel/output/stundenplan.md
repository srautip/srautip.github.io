# Stundenplan: bw-grundschule-beispiel (per CLI generiert)

**Status:** SolveTop (TimeLimitReached)  |  **CP-SAT-Status:** Feasible  |  **Kann-Verstoesse:** 0  |  **Qualitaet (Total):** 183.6  |  **Verstoesse:** 0

*Phase 2.25: die Stagnationserkennung hat 16 von 16 Solve-Iteration(en) vorzeitig abgebrochen, weil ueber `stagnation_timeout_s` hinweg keine Verbesserung mehr gefunden wurde - spart Zeit fuer weitere Iterationen statt eine stehende Suche bis zum Zeitlimit weiterlaufen zu lassen.*

*Hinweis: `Feasible` statt `Optimal` bedeutet, CP-SAT konnte innerhalb von `solve_time_limit_s` (aktuell 120s) keinen Optimalitaetsbeweis erbringen - ein hoeherer Wert in `config.yaml` kann helfen, ein noch besseres Ergebnis zu finden oder das aktuelle als optimal zu beweisen.*
*Zusaetzlich begrenzt `per_solve_time_limit_s` (aktuell 30s) jede EINZELNE Solve-Iteration - ein hoeherer Wert kann derselben Iteration mehr Zeit fuer einen Optimalitaetsbeweis geben, auf Kosten weniger Iterationen fuer zusaetzliche `max_solutions`-Alternativen innerhalb desselben Gesamtbudgets.*

## Optimalitaets-Luecke

Gefundene Loesung (Objective): **195.0**  |  Bewiesene untere Schranke: **67.0**  |  Maximal noch moegliche Verbesserung: **65.6%**

*Diese Luecke ist eine bewiesene OBERGRENZE, keine Vorhersage - die tatsaechlich erreichbare Verbesserung kann kleiner sein (bis hin zu 0, falls die gefundene Loesung bereits optimal ist, CP-SAT das aber innerhalb der Zeit nicht beweisen konnte).*

**Verlauf dieses Solve-Versuchs** (jede gefundene Verbesserung, nicht in festen Zeitabstaenden):

| Zeit (s) | Objective |
|---|---|
| 0.4 | 1551.0 |
| 0.4 | 855.0 |
| 0.5 | 288.0 |
| 0.6 | 264.0 |
| 0.7 | 259.0 |
| 0.7 | 251.0 |
| 0.9 | 243.0 |
| 1.3 | 235.0 |
| 1.6 | 232.0 |
| 1.7 | 229.0 |
| 1.8 | 224.0 |
| 2.0 | 221.0 |
| 2.2 | 218.0 |
| 2.9 | 217.0 |
| 3.0 | 212.0 |
| 3.2 | 210.0 |
| 3.3 | 198.0 |
| 5.7 | 195.0 |

*Letzte Verbesserung bei 5.7s - fand danach bis zum Abbruch keine weitere statt. Ein deutlich frueherer letzter Eintrag als das Zeitbudget legt nahe, dass zusaetzliche Zeit fuer DIESEN Versuch wenig bringen wuerde.*

## Klassen

### 1a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | Mathematik (Klassenlehrer-1) | Deutsch (Klassenlehrer-1) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Musik (Klassenlehrer-1) | Sachunterricht (Klassenlehrer-1) |
| 3 | Kunst (Klassenlehrer-1) | Deutsch (Klassenlehrer-1) | Deutsch-Förderstunde (Klassenlehrer-1) | Sport (Klassenlehrer-1) | Mathematik (Klassenlehrer-1) |
| 4 | Mathe-Förderstunde (Klassenlehrer-1) | Sachunterricht (Klassenlehrer-1) | Mathematik (Klassenlehrer-1) | Mathematik (Klassenlehrer-1) | Deutsch (Klassenlehrer-1) |
| 5 | Deutsch (Klassenlehrer-1) | Sport (Klassenlehrer-1) | Musik (Klassenlehrer-1) | Mathematik (Klassenlehrer-1) | Sachunterricht (Klassenlehrer-1) |
| 6 | Sport (Klassenlehrer-1) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Deutsch (Klassenlehrer-1) | Chor (Chorleiterin-1) | Deutsch (Klassenlehrer-1) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 1b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | Mathe-Förderstunde (Klassenlehrer-3) | Musik (Klassenlehrer-3) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Deutsch (Klassenlehrer-3) | Mathematik (Klassenlehrer-3) |
| 3 | Sport (Klassenlehrer-3) | Deutsch (Klassenlehrer-3) | Deutsch (Klassenlehrer-3) | Sport (Klassenlehrer-3) | Sachunterricht (Klassenlehrer-3) |
| 4 | Mathematik (Klassenlehrer-3) | Sport (Klassenlehrer-3) | Musik (Klassenlehrer-3) | Mathematik (Klassenlehrer-3) | Deutsch (Klassenlehrer-3) |
| 5 | Sachunterricht (Klassenlehrer-3) | Sachunterricht (Klassenlehrer-3) | Kunst (Klassenlehrer-3) | Deutsch (Klassenlehrer-3) | Deutsch-Förderstunde (Klassenlehrer-3) |
| 6 | Deutsch (Klassenlehrer-3) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Mathematik (Klassenlehrer-3) | Chor (Chorleiterin-1) | Mathematik (Klassenlehrer-3) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 2a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Ethik / Religion-ev / Religion-kath (Klassenlehrer-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - | - | Ethik / Religion-ev / Religion-kath (Klassenlehrer-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - |
| 2 | Sport (Klassenlehrer-5) | Mathematik (Klassenlehrer-5) | Kunst (Klassenlehrer-5) | Deutsch-Förderstunde (Klassenlehrer-5) | Kunst (Klassenlehrer-5) |
| 3 | Deutsch (Klassenlehrer-5) | Sachunterricht (Klassenlehrer-5) | Sachunterricht (Klassenlehrer-5) | Sport (Klassenlehrer-5) | Sport (Klassenlehrer-5) |
| 4 | Mathematik (Klassenlehrer-5) | Mathematik (Klassenlehrer-5) | Mathematik (Klassenlehrer-5) | Sachunterricht (Klassenlehrer-5) | Mathematik (Klassenlehrer-5) |
| 5 | Deutsch (Klassenlehrer-5) | Deutsch (Klassenlehrer-5) | Musik (Klassenlehrer-5) | Deutsch (Klassenlehrer-5) | Deutsch (Klassenlehrer-5) |
| 6 | - | Deutsch (Klassenlehrer-5) | Deutsch (Klassenlehrer-5) | Chor (Chorleiterin-1) | Mathe-Förderstunde (Klassenlehrer-5) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 2b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Ethik / Religion-ev / Religion-kath (Klassenlehrer-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - | - | Ethik / Religion-ev / Religion-kath (Klassenlehrer-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - |
| 2 | Deutsch (Klassenlehrer-8) | Sport (Klassenlehrer-8) | Deutsch-Förderstunde (Klassenlehrer-8) | Mathematik (Klassenlehrer-8) | Mathematik (Klassenlehrer-8) |
| 3 | Sport (Klassenlehrer-8) | Mathematik (Klassenlehrer-8) | Deutsch (Klassenlehrer-8) | Deutsch (Klassenlehrer-8) | Kunst (Klassenlehrer-8) |
| 4 | Mathematik (Klassenlehrer-8) | Mathematik (Klassenlehrer-8) | Musik (Klassenlehrer-8) | Sport (Klassenlehrer-8) | Sachunterricht (Klassenlehrer-8) |
| 5 | Deutsch (Klassenlehrer-8) | Mathe-Förderstunde (Klassenlehrer-8) | Sachunterricht (Klassenlehrer-8) | Kunst (Klassenlehrer-8) | Deutsch (Klassenlehrer-8) |
| 6 | - | Deutsch (Klassenlehrer-8) | Deutsch (Klassenlehrer-8) | Chor (Chorleiterin-1) | Sachunterricht (Klassenlehrer-8) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 3a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | Englisch (Englischlehrer-1) | Englisch (Englischlehrer-1) | - |
| 2 | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Sachunterricht (Klassenlehrer-2) | Mathematik (Klassenlehrer-2) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Musik (Klassenlehrer-2) |
| 3 | Sport (Klassenlehrer-2) | Deutsch (Klassenlehrer-2) | Sport (Klassenlehrer-2) | Mathematik (Klassenlehrer-2) | Deutsch (Klassenlehrer-2) |
| 4 | Deutsch (Klassenlehrer-2) | Deutsch (Klassenlehrer-2) | Kunst (Klassenlehrer-2) | Kunst (Klassenlehrer-2) | Sport (Klassenlehrer-2) |
| 5 | Deutsch (Klassenlehrer-2) | Sachunterricht (Klassenlehrer-2) | Deutsch (Klassenlehrer-2) | Deutsch (Klassenlehrer-2) | Mathematik (Klassenlehrer-2) |
| 6 | Sachunterricht (Klassenlehrer-2) | Mathematik (Klassenlehrer-2) | Mathematik (Klassenlehrer-2) | Chor (Chorleiterin-1) | Deutsch (Klassenlehrer-2) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 3b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Englisch (Englischlehrer-1) | - | - | - | Englisch (Englischlehrer-1) |
| 2 | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Deutsch (Klassenlehrer-7) | Deutsch (Klassenlehrer-7) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Mathematik (Klassenlehrer-7) |
| 3 | Musik (Klassenlehrer-7) | Sachunterricht (Klassenlehrer-7) | Sachunterricht (Klassenlehrer-7) | Deutsch (Klassenlehrer-7) | Sachunterricht (Klassenlehrer-7) |
| 4 | Kunst (Klassenlehrer-7) | Sport (Klassenlehrer-7) | Mathematik (Klassenlehrer-7) | Deutsch (Klassenlehrer-7) | Sport (Klassenlehrer-7) |
| 5 | Sport (Klassenlehrer-7) | Deutsch (Klassenlehrer-7) | Kunst (Klassenlehrer-7) | Mathematik (Klassenlehrer-7) | Deutsch (Klassenlehrer-7) |
| 6 | Deutsch (Klassenlehrer-7) | Mathematik (Klassenlehrer-7) | Mathematik (Klassenlehrer-7) | Chor (Chorleiterin-1) | Deutsch (Klassenlehrer-7) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 4a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Kunst (Klassenlehrer-4) | Sport (Klassenlehrer-4) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Sport (Klassenlehrer-4) | - |
| 2 | Mathematik (Klassenlehrer-4) | Sachunterricht (Klassenlehrer-4) | Englisch (Englischlehrer-1) | Musik (Klassenlehrer-4) | Englisch (Englischlehrer-1) |
| 3 | Deutsch (Klassenlehrer-4) | Deutsch (Klassenlehrer-4) | Sport (Klassenlehrer-4) | Mathematik (Klassenlehrer-4) | Sachunterricht (Klassenlehrer-4) |
| 4 | Sachunterricht (Klassenlehrer-4) | Mathematik (Klassenlehrer-4) | Kunst (Klassenlehrer-4) | Deutsch (Klassenlehrer-4) | Mathematik (Klassenlehrer-4) |
| 5 | Mathematik (Klassenlehrer-4) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Musik (Klassenlehrer-4) | Deutsch (Klassenlehrer-4) | Deutsch (Klassenlehrer-4) |
| 6 | Deutsch (Klassenlehrer-4) | - | Mathematik (Klassenlehrer-4) | Chor (Chorleiterin-1) | Deutsch (Klassenlehrer-4) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 4b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | Sport (Klassenlehrer-6) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Musik (Klassenlehrer-6) | - |
| 2 | Englisch (Englischlehrer-1) | Sachunterricht (Klassenlehrer-6) | Sachunterricht (Klassenlehrer-6) | Deutsch (Klassenlehrer-6) | Mathematik (Klassenlehrer-6) |
| 3 | Mathematik (Klassenlehrer-6) | Musik (Klassenlehrer-6) | Mathematik (Klassenlehrer-6) | Mathematik (Klassenlehrer-6) | Sachunterricht (Klassenlehrer-6) |
| 4 | Deutsch (Klassenlehrer-6) | Kunst (Klassenlehrer-6) | Mathematik (Klassenlehrer-6) | Sport (Klassenlehrer-6) | Deutsch (Klassenlehrer-6) |
| 5 | Sport (Klassenlehrer-6) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Deutsch (Klassenlehrer-6) | Deutsch (Klassenlehrer-6) | Deutsch (Klassenlehrer-6) |
| 6 | Kunst (Klassenlehrer-6) | Englisch (Englischlehrer-1) | Deutsch (Klassenlehrer-6) | Chor (Chorleiterin-1) | Mathematik (Klassenlehrer-6) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

## Lehrkraefte

### Klassenlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | 1a Mathematik | 1a Deutsch | - | 1a Musik | 1a Sachunterricht |
| 3 | 1a Kunst | 1a Deutsch | 1a Deutsch-Förderstunde | 1a Sport | 1a Mathematik |
| 4 | 1a Mathe-Förderstunde | 1a Sachunterricht | 1a Mathematik | 1a Mathematik | 1a Deutsch |
| 5 | 1a Deutsch | 1a Sport | 1a Musik | 1a Mathematik | 1a Sachunterricht |
| 6 | 1a Sport | - | 1a Deutsch | - | 1a Deutsch |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | 3a Sachunterricht | 3a Mathematik | - | 3a Musik |
| 3 | 3a Sport | 3a Deutsch | 3a Sport | 3a Mathematik | 3a Deutsch |
| 4 | 3a Deutsch | 3a Deutsch | 3a Kunst | 3a Kunst | 3a Sport |
| 5 | 3a Deutsch | 3a Sachunterricht | 3a Deutsch | 3a Deutsch | 3a Mathematik |
| 6 | 3a Sachunterricht | 3a Mathematik | 3a Mathematik | - | 3a Deutsch |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 2b Ethik | - | - | 2b Ethik | - |
| 2 | 1b Mathe-Förderstunde | 1b Musik | - | 1b Deutsch | 1b Mathematik |
| 3 | 1b Sport | 1b Deutsch | 1b Deutsch | 1b Sport | 1b Sachunterricht |
| 4 | 1b Mathematik | 1b Sport | 1b Musik | 1b Mathematik | 1b Deutsch |
| 5 | 1b Sachunterricht | 1b Sachunterricht | 1b Kunst | 1b Deutsch | 1b Deutsch-Förderstunde |
| 6 | 1b Deutsch | - | 1b Mathematik | - | 1b Mathematik |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 4a Kunst | 4a Sport | - | 4a Sport | - |
| 2 | 4a Mathematik | 4a Sachunterricht | - | 4a Musik | - |
| 3 | 4a Deutsch | 4a Deutsch | 4a Sport | 4a Mathematik | 4a Sachunterricht |
| 4 | 4a Sachunterricht | 4a Mathematik | 4a Kunst | 4a Deutsch | 4a Mathematik |
| 5 | 4a Mathematik | - | 4a Musik | 4a Deutsch | 4a Deutsch |
| 6 | 4a Deutsch | - | 4a Mathematik | - | 4a Deutsch |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | 2a Sport | 2a Mathematik | 2a Kunst | 2a Deutsch-Förderstunde | 2a Kunst |
| 3 | 2a Deutsch | 2a Sachunterricht | 2a Sachunterricht | 2a Sport | 2a Sport |
| 4 | 2a Mathematik | 2a Mathematik | 2a Mathematik | 2a Sachunterricht | 2a Mathematik |
| 5 | 2a Deutsch | 2a Deutsch | 2a Musik | 2a Deutsch | 2a Deutsch |
| 6 | - | 2a Deutsch | 2a Deutsch | - | 2a Mathe-Förderstunde |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-6

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 4b Sport | - | 4b Musik | - |
| 2 | - | 4b Sachunterricht | 4b Sachunterricht | 4b Deutsch | 4b Mathematik |
| 3 | 4b Mathematik | 4b Musik | 4b Mathematik | 4b Mathematik | 4b Sachunterricht |
| 4 | 4b Deutsch | 4b Kunst | 4b Mathematik | 4b Sport | 4b Deutsch |
| 5 | 4b Sport | - | 4b Deutsch | 4b Deutsch | 4b Deutsch |
| 6 | 4b Kunst | - | 4b Deutsch | - | 4b Mathematik |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-7

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | 3b Deutsch | 3b Deutsch | - | 3b Mathematik |
| 3 | 3b Musik | 3b Sachunterricht | 3b Sachunterricht | 3b Deutsch | 3b Sachunterricht |
| 4 | 3b Kunst | 3b Sport | 3b Mathematik | 3b Deutsch | 3b Sport |
| 5 | 3b Sport | 3b Deutsch | 3b Kunst | 3b Mathematik | 3b Deutsch |
| 6 | 3b Deutsch | 3b Mathematik | 3b Mathematik | - | 3b Deutsch |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-8

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | 2b Deutsch | 2b Sport | 2b Deutsch-Förderstunde | 2b Mathematik | 2b Mathematik |
| 3 | 2b Sport | 2b Mathematik | 2b Deutsch | 2b Deutsch | 2b Kunst |
| 4 | 2b Mathematik | 2b Mathematik | 2b Musik | 2b Sport | 2b Sachunterricht |
| 5 | 2b Deutsch | 2b Mathe-Förderstunde | 2b Sachunterricht | 2b Kunst | 2b Deutsch |
| 6 | - | 2b Deutsch | 2b Deutsch | - | 2b Sachunterricht |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Religionslehrer-ev-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 2b Religion-ev | - | 4b Religion-ev | 2b Religion-ev | - |
| 2 | 3b Religion-ev | - | 1b Religion-ev | 3b Religion-ev | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | - | 4b Religion-ev | - | - | - |
| 6 | - | 1b Religion-ev | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Religionslehrer-kath-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 2b Religion-kath | - | 4b Religion-kath | 2b Religion-kath | - |
| 2 | 3b Religion-kath | - | 1b Religion-kath | 3b Religion-kath | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | - | 4b Religion-kath | - | - | - |
| 6 | - | 1b Religion-kath | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Ethiklehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | 4b Ethik | - | - |
| 2 | 3b Ethik | - | 1b Ethik | 3b Ethik | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | - | 4b Ethik | - | - | - |
| 6 | - | 1b Ethik | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 3b Englisch | - | 3a Englisch | 3a Englisch | 3b Englisch |
| 2 | 4b Englisch | - | 4a Englisch | - | 4a Englisch |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | - | - | - |
| 6 | - | 4b Englisch | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Chorleiterin-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | - | - | - | - | - |
| 6 | - | - | - | 4b Chor | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |
