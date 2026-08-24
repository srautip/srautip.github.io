# Stundenplan: bw-grundschule-beispiel (per CLI generiert)

**Status:** SolveTop (MaxSolutionsReached)  |  **CP-SAT-Status:** Optimal  |  **Kann-Verstoesse:** 0  |  **Qualitaet (Total):** 193.0  |  **Verstoesse:** 0

*Mehr-Zuteilungs-Modus: 2 Lehrer-Zuteilungen mit je eigenem Stufe-2-Lauf (90s / 15 Loesungen pro Zuteilung); die hier dokumentierte beste Loesung stammt aus Zuteilung 2. Alle Loesungen aller Zuteilungen: output/stundentafel.html (Spalte 'Zuteilung').*

*Phase 2.25: die Stagnationserkennung hat 1 von 15 Solve-Iteration(en) vorzeitig abgebrochen, weil ueber `stagnation_timeout_s` hinweg keine Verbesserung mehr gefunden wurde - spart Zeit fuer weitere Iterationen statt eine stehende Suche bis zum Zeitlimit weiterlaufen zu lassen.*

## Optimalitaets-Luecke

CP-SAT hat bewiesen, dass diese Loesung fuer das aktuelle Modell optimal ist (Luecke = 0%).

**Verlauf dieses Solve-Versuchs** (jede gefundene Verbesserung, nicht in festen Zeitabstaenden):

| Zeit (s) | Objective |
|---|---|
| 0.7 | 91.0 |
| 0.7 | 71.0 |
| 0.9 | 61.0 |
| 1.0 | 43.0 |
| 1.0 | 35.0 |
| 1.1 | 29.0 |
| 1.1 | 28.0 |
| 1.3 | 25.0 |
| 1.3 | 22.0 |
| 1.3 | 19.0 |
| 1.6 | 18.0 |
| 1.6 | 17.0 |
| 1.7 | 16.0 |
| 1.9 | 15.0 |
| 2.0 | 14.0 |
| 2.2 | 13.0 |
| 2.4 | 3.0 |
| 2.7 | 2.0 |
| 2.8 | 1.0 |
| 2.9 | 0.0 |

*Letzte Verbesserung bei 2.9s - fand danach bis zum Abbruch keine weitere statt. Ein deutlich frueherer letzter Eintrag als das Zeitbudget legt nahe, dass zusaetzliche Zeit fuer DIESEN Versuch wenig bringen wuerde.*

## Klassen

### 1a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | Mathematik (Klassenlehrer-1) | Sachunterricht (Klassenlehrer-1) | Sport (Klassenlehrer-1) | Deutsch (Klassenlehrer-1) | Mathematik (Klassenlehrer-1) |
| 3 | Deutsch (Klassenlehrer-1) | Deutsch (Klassenlehrer-1) | Deutsch (Klassenlehrer-1) | Mathematik (Klassenlehrer-1) | Mathematik (Klassenlehrer-1) |
| 4 | Mathe-Förderstunde (Klassenlehrer-1) | Deutsch (Klassenlehrer-1) | Mathematik (Klassenlehrer-1) | Sport (Klassenlehrer-1) | Deutsch (Klassenlehrer-1) |
| 5 | Kunst (Klassenlehrer-1) | Sachunterricht (Klassenlehrer-1) | Sachunterricht (Klassenlehrer-1) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Sport (Klassenlehrer-1) |
| 6 | Musik (Klassenlehrer-1) | Deutsch-Förderstunde (Klassenlehrer-1) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Chor (Chorleiterin-1) | Musik (Klassenlehrer-1) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 1b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | Mathematik (Klassenlehrer-2) | Deutsch (Klassenlehrer-2) | Deutsch (Klassenlehrer-2) | Deutsch (Klassenlehrer-2) | Deutsch (Klassenlehrer-2) |
| 3 | Deutsch (Klassenlehrer-2) | Musik (Klassenlehrer-2) | Musik (Klassenlehrer-2) | Sachunterricht (Klassenlehrer-2) | Mathematik (Klassenlehrer-2) |
| 4 | Sachunterricht (Klassenlehrer-2) | Deutsch (Klassenlehrer-2) | Mathematik (Klassenlehrer-2) | Mathematik (Klassenlehrer-2) | Mathematik (Klassenlehrer-2) |
| 5 | Mathe-Förderstunde (Klassenlehrer-2) | Deutsch-Förderstunde (Klassenlehrer-2) | Sachunterricht (Klassenlehrer-2) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Sport (Klassenlehrer-2) |
| 6 | Sport (Klassenlehrer-2) | Sport (Klassenlehrer-2) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Chor (Chorleiterin-1) | Kunst (Klassenlehrer-2) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 2a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Deutsch (Klassenlehrer-4) | Sachunterricht (Klassenlehrer-4) | Mathematik (Klassenlehrer-4) | Mathematik (Klassenlehrer-4) | Mathematik (Klassenlehrer-4) |
| 2 | Deutsch (Klassenlehrer-4) | Mathematik (Klassenlehrer-4) | Deutsch (Klassenlehrer-4) | Deutsch (Klassenlehrer-4) | Deutsch (Klassenlehrer-4) |
| 3 | Sport (Klassenlehrer-4) | Kunst (Klassenlehrer-4) | Deutsch (Klassenlehrer-4) | Deutsch (Klassenlehrer-4) | Mathematik (Klassenlehrer-4) |
| 4 | Ethik / Religion-ev / Religion-kath (Klassenlehrer-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Ethik / Religion-ev / Religion-kath (Klassenlehrer-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Sachunterricht (Klassenlehrer-4) | Deutsch-Förderstunde (Klassenlehrer-4) | Mathe-Förderstunde (Klassenlehrer-4) |
| 5 | - | - | Sport (Klassenlehrer-4) | Sachunterricht (Klassenlehrer-4) | Sport (Klassenlehrer-4) |
| 6 | - | - | Kunst (Klassenlehrer-4) | Chor (Chorleiterin-1) | Musik (Klassenlehrer-4) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 2b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik (Klassenlehrer-5) | Deutsch (Klassenlehrer-5) | Mathematik (Klassenlehrer-5) | Musik (Klassenlehrer-5) | Deutsch (Klassenlehrer-5) |
| 2 | Kunst (Klassenlehrer-5) | Deutsch (Klassenlehrer-5) | Deutsch (Klassenlehrer-5) | Mathe-Förderstunde (Klassenlehrer-5) | Mathematik (Klassenlehrer-5) |
| 3 | Mathematik (Klassenlehrer-5) | Mathematik (Klassenlehrer-5) | Deutsch (Klassenlehrer-5) | Deutsch (Klassenlehrer-5) | Sport (Klassenlehrer-5) |
| 4 | Ethik / Religion-ev / Religion-kath (Klassenlehrer-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Ethik / Religion-ev / Religion-kath (Klassenlehrer-3 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Deutsch-Förderstunde (Klassenlehrer-5) | Sport (Klassenlehrer-5) | Deutsch (Klassenlehrer-5) |
| 5 | - | - | Sport (Klassenlehrer-5) | Sachunterricht (Klassenlehrer-5) | Kunst (Klassenlehrer-5) |
| 6 | - | - | Sachunterricht (Klassenlehrer-5) | Chor (Chorleiterin-1) | Sachunterricht (Klassenlehrer-5) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 3a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Mathematik (Klassenlehrer-6) | Musik (Klassenlehrer-6) | Sport (Klassenlehrer-6) | Sport (Klassenlehrer-6) | Deutsch (Klassenlehrer-6) |
| 2 | Kunst (Klassenlehrer-6) | Deutsch (Klassenlehrer-6) | Deutsch (Klassenlehrer-6) | Mathematik (Klassenlehrer-6) | Sport (Klassenlehrer-6) |
| 3 | Deutsch (Klassenlehrer-6) | Deutsch (Klassenlehrer-6) | Mathematik (Klassenlehrer-6) | Deutsch (Klassenlehrer-6) | Mathematik (Klassenlehrer-6) |
| 4 | Deutsch (Klassenlehrer-6) | Sachunterricht (Klassenlehrer-6) | Mathematik (Klassenlehrer-6) | Kunst (Klassenlehrer-6) | Deutsch (Klassenlehrer-6) |
| 5 | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Sachunterricht (Klassenlehrer-6) | Sachunterricht (Klassenlehrer-6) | Englisch (Englischlehrer-1) |
| 6 | - | - | Englisch (Englischlehrer-1) | Chor (Chorleiterin-1) | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 3b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Musik (Klassenlehrer-3) | - | Mathematik (Klassenlehrer-3) | Sport (Klassenlehrer-3) | Deutsch (Klassenlehrer-3) |
| 2 | Mathematik (Klassenlehrer-3) | Deutsch (Klassenlehrer-3) | Deutsch (Klassenlehrer-3) | Deutsch (Klassenlehrer-3) | Sport (Klassenlehrer-3) |
| 3 | Deutsch (Klassenlehrer-3) | Mathematik (Klassenlehrer-3) | Deutsch (Klassenlehrer-3) | Mathematik (Klassenlehrer-3) | Mathematik (Klassenlehrer-3) |
| 4 | Englisch (Englischlehrer-1) | Englisch (Englischlehrer-1) | Sport (Klassenlehrer-3) | Deutsch (Klassenlehrer-3) | Deutsch (Klassenlehrer-3) |
| 5 | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Sachunterricht (Klassenlehrer-3) | Sachunterricht (Klassenlehrer-3) | Kunst (Klassenlehrer-3) |
| 6 | - | - | Kunst (Klassenlehrer-3) | Chor (Chorleiterin-1) | Sachunterricht (Klassenlehrer-3) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 4a

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | Musik (Klassenlehrer-7) | - | Deutsch (Klassenlehrer-7) | Sport (Klassenlehrer-7) | Mathematik (Klassenlehrer-7) |
| 2 | Deutsch (Klassenlehrer-7) | Sachunterricht (Klassenlehrer-7) | Mathematik (Klassenlehrer-7) | Mathematik (Klassenlehrer-7) | Deutsch (Klassenlehrer-7) |
| 3 | Sport (Klassenlehrer-7) | Deutsch (Klassenlehrer-7) | Mathematik (Klassenlehrer-7) | Deutsch (Klassenlehrer-7) | Mathematik (Klassenlehrer-7) |
| 4 | Mathematik (Klassenlehrer-7) | Musik (Klassenlehrer-7) | Sport (Klassenlehrer-7) | Deutsch (Klassenlehrer-7) | Deutsch (Klassenlehrer-7) |
| 5 | Englisch (Englischlehrer-1) | Kunst (Klassenlehrer-7) | Englisch (Englischlehrer-1) | Sachunterricht (Klassenlehrer-7) | Sachunterricht (Klassenlehrer-7) |
| 6 | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | - | Chor (Chorleiterin-1) | Kunst (Klassenlehrer-7) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### 4b

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | Mathematik (Klassenlehrer-8) | Mathematik (Klassenlehrer-8) | Englisch (Englischlehrer-1) | - |
| 2 | Kunst (Klassenlehrer-8) | Deutsch (Klassenlehrer-8) | Deutsch (Klassenlehrer-8) | Sachunterricht (Klassenlehrer-8) | Mathematik (Klassenlehrer-8) |
| 3 | Mathematik (Klassenlehrer-8) | Sport (Klassenlehrer-8) | Deutsch (Klassenlehrer-8) | Deutsch (Klassenlehrer-8) | Deutsch (Klassenlehrer-8) |
| 4 | Sport (Klassenlehrer-8) | Mathematik (Klassenlehrer-8) | Mathematik (Klassenlehrer-8) | Deutsch (Klassenlehrer-8) | Deutsch (Klassenlehrer-8) |
| 5 | Sachunterricht (Klassenlehrer-8) | Sachunterricht (Klassenlehrer-8) | Musik (Klassenlehrer-8) | Musik (Klassenlehrer-8) | Kunst (Klassenlehrer-8) |
| 6 | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Ethik / Religion-ev / Religion-kath (Ethiklehrer-1 / Religionslehrer-ev-1 / Religionslehrer-kath-1) | Sport (Klassenlehrer-8) | Chor (Chorleiterin-1) | Englisch (Englischlehrer-1) |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

## Lehrkraefte

### Klassenlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | 1a Mathematik | 1a Sachunterricht | 1a Sport | 1a Deutsch | 1a Mathematik |
| 3 | 1a Deutsch | 1a Deutsch | 1a Deutsch | 1a Mathematik | 1a Mathematik |
| 4 | 1a Mathe-Förderstunde | 1a Deutsch | 1a Mathematik | 1a Sport | 1a Deutsch |
| 5 | 1a Kunst | 1a Sachunterricht | 1a Sachunterricht | - | 1a Sport |
| 6 | 1a Musik | 1a Deutsch-Förderstunde | - | - | 1a Musik |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-2

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | 1b Mathematik | 1b Deutsch | 1b Deutsch | 1b Deutsch | 1b Deutsch |
| 3 | 1b Deutsch | 1b Musik | 1b Musik | 1b Sachunterricht | 1b Mathematik |
| 4 | 1b Sachunterricht | 1b Deutsch | 1b Mathematik | 1b Mathematik | 1b Mathematik |
| 5 | 1b Mathe-Förderstunde | 1b Deutsch-Förderstunde | 1b Sachunterricht | - | 1b Sport |
| 6 | 1b Sport | 1b Sport | - | - | 1b Kunst |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-3

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 3b Musik | - | 3b Mathematik | 3b Sport | 3b Deutsch |
| 2 | 3b Mathematik | 3b Deutsch | 3b Deutsch | 3b Deutsch | 3b Sport |
| 3 | 3b Deutsch | 3b Mathematik | 3b Deutsch | 3b Mathematik | 3b Mathematik |
| 4 | 2b Ethik | 2b Ethik | 3b Sport | 3b Deutsch | 3b Deutsch |
| 5 | - | - | 3b Sachunterricht | 3b Sachunterricht | 3b Kunst |
| 6 | - | - | 3b Kunst | - | 3b Sachunterricht |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-4

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 2a Deutsch | 2a Sachunterricht | 2a Mathematik | 2a Mathematik | 2a Mathematik |
| 2 | 2a Deutsch | 2a Mathematik | 2a Deutsch | 2a Deutsch | 2a Deutsch |
| 3 | 2a Sport | 2a Kunst | 2a Deutsch | 2a Deutsch | 2a Mathematik |
| 4 | - | - | 2a Sachunterricht | 2a Deutsch-Förderstunde | 2a Mathe-Förderstunde |
| 5 | - | - | 2a Sport | 2a Sachunterricht | 2a Sport |
| 6 | - | - | 2a Kunst | - | 2a Musik |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-5

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 2b Mathematik | 2b Deutsch | 2b Mathematik | 2b Musik | 2b Deutsch |
| 2 | 2b Kunst | 2b Deutsch | 2b Deutsch | 2b Mathe-Förderstunde | 2b Mathematik |
| 3 | 2b Mathematik | 2b Mathematik | 2b Deutsch | 2b Deutsch | 2b Sport |
| 4 | - | - | 2b Deutsch-Förderstunde | 2b Sport | 2b Deutsch |
| 5 | - | - | 2b Sport | 2b Sachunterricht | 2b Kunst |
| 6 | - | - | 2b Sachunterricht | - | 2b Sachunterricht |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-6

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 3a Mathematik | 3a Musik | 3a Sport | 3a Sport | 3a Deutsch |
| 2 | 3a Kunst | 3a Deutsch | 3a Deutsch | 3a Mathematik | 3a Sport |
| 3 | 3a Deutsch | 3a Deutsch | 3a Mathematik | 3a Deutsch | 3a Mathematik |
| 4 | 3a Deutsch | 3a Sachunterricht | 3a Mathematik | 3a Kunst | 3a Deutsch |
| 5 | - | - | 3a Sachunterricht | 3a Sachunterricht | - |
| 6 | - | - | - | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-7

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | 4a Musik | - | 4a Deutsch | 4a Sport | 4a Mathematik |
| 2 | 4a Deutsch | 4a Sachunterricht | 4a Mathematik | 4a Mathematik | 4a Deutsch |
| 3 | 4a Sport | 4a Deutsch | 4a Mathematik | 4a Deutsch | 4a Mathematik |
| 4 | 4a Mathematik | 4a Musik | 4a Sport | 4a Deutsch | 4a Deutsch |
| 5 | - | 4a Kunst | - | 4a Sachunterricht | 4a Sachunterricht |
| 6 | - | - | - | - | 4a Kunst |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Klassenlehrer-8

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | 4b Mathematik | 4b Mathematik | - | - |
| 2 | 4b Kunst | 4b Deutsch | 4b Deutsch | 4b Sachunterricht | 4b Mathematik |
| 3 | 4b Mathematik | 4b Sport | 4b Deutsch | 4b Deutsch | 4b Deutsch |
| 4 | 4b Sport | 4b Mathematik | 4b Mathematik | 4b Deutsch | 4b Deutsch |
| 5 | 4b Sachunterricht | 4b Sachunterricht | 4b Musik | 4b Musik | 4b Kunst |
| 6 | - | - | 4b Sport | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Religionslehrer-ev-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | 2b Religion-ev | 2b Religion-ev | - | - | - |
| 5 | 3b Religion-ev | 3b Religion-ev | - | 1b Religion-ev | - |
| 6 | 4b Religion-ev | 4b Religion-ev | 1b Religion-ev | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Religionslehrer-kath-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | 2b Religion-kath | 2b Religion-kath | - | - | - |
| 5 | 3b Religion-kath | 3b Religion-kath | - | 1b Religion-kath | - |
| 6 | 4b Religion-kath | 4b Religion-kath | 1b Religion-kath | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Ethiklehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | - | - |
| 2 | - | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | - | - | - | - | - |
| 5 | 3b Ethik | 3b Ethik | - | 1b Ethik | - |
| 6 | 4b Ethik | 4b Ethik | 1b Ethik | - | - |
| 7 | - | - | - | - | - |
| 8 | - | - | - | - | - |

### Englischlehrer-1

| Std. | Mo | Di | Mi | Do | Fr |
|---|---|---|---|---|---|
| 1 | - | - | - | 4b Englisch | - |
| 2 | - | - | - | - | - |
| 3 | - | - | - | - | - |
| 4 | 3b Englisch | 3b Englisch | - | - | - |
| 5 | 4a Englisch | - | 4a Englisch | - | 3a Englisch |
| 6 | - | - | 3a Englisch | - | 4b Englisch |
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
