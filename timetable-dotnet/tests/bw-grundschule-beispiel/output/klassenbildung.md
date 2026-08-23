# Klassenbildung: bw-grundschule-beispiel

100 Kinder -> 4 Klassen (22-26) | Regeln: 16 Gruppen, 3 Balance, 34 Wuensche, 1 Fixierungen | Parameter: 3 Varianten, epsilon 0.05, min_distanz 8, Zeitlimit 20s je Lauf

*Der Solver liefert VORSCHLAEGE - die Klassenzuordnung entscheidet die Schulleitung (menschliche Letztentscheidung, Konzept Abschnitt 10). Dieser Report dokumentiert Regeln, Parameter und Abweichungen jedes Vorschlags.*

## Konsens-Kern

27 von 100 Kindern sind in allen 3 Varianten identisch zugeordnet - der stabile Kern fuer eine Bulk-Fixierung.

## Variante 1 (Feasible, Zielwert 1001)

Ampel: 4 rot, 53 gelb, 32 gruen, 11 frei (von keinem Kriterium betroffen).

| Klasse | Groesse | Kinder |
|---|---|---|
| 1a | 26 | S001, S008, S009, S025, S026, S028, S029, S030, S037, S039, S050, S054, S055, S058, S067, S068, S070, S071, S073, S075, S076, S077, S078, S081, S092, S097 |
| 1b | 26 | S002, S010, S013, S015, S016, S020, S022, S027, S031, S032, S034, S035, S036, S040, S042, S052, S056, S061, S062, S066, S072, S074, S082, S083, S090, S091 |
| 1c | 22 | S003, S021, S024, S038, S041, S044, S046, S048, S049, S051, S057, S059, S064, S069, S079, S087, S088, S089, S094, S095, S098, S100 |
| 1d | 26 | S004, S005, S006, S007, S011, S012, S014, S017, S018, S019, S023, S033, S043, S045, S047, S053, S060, S063, S065, S080, S084, S085, S086, S093, S096, S099 |

Balance-Kennzahlen (Anzahl je Klasse):

- geschlecht=w: 14 / 10 / 10 / 14 (Ziel ~12 +/- 2)
- sprachfoerderung=ja: 4 / 3 / 5 / 4 (Ziel ~4 +/- 1)
- kann_kind=ja: 2 / 2 / 3 / 2 (Ziel ~2 +/- 1)

Verletzungsreport (weiche Regeln):

- [VERLETZT] G_sozialverhalten (verteilung, Prio 3): Mass 1
- [ok] G_kita_sonnenblume_1 (buendelung, Prio 2): Mass 0
- [ok] G_kita_regenbogen_2 (buendelung, Prio 2): Mass 0
- [ok] G_kita_pusteblume_3 (buendelung, Prio 2): Mass 0
- [ok] G_kita_wirbelwind_4 (buendelung, Prio 2): Mass 0
- [ok] G_kita_loewenzahn_5 (buendelung, Prio 2): Mass 0
- [ok] G_kita_villakunterbunt_6 (buendelung, Prio 2): Mass 0
- [ok] G_kita_sonnenblume_7 (buendelung, Prio 2): Mass 0
- [ok] G_kita_regenbogen_8 (buendelung, Prio 2): Mass 0
- [ok] G_kita_pusteblume_9 (buendelung, Prio 2): Mass 0
- [ok] G_kita_wirbelwind_10 (buendelung, Prio 2): Mass 0
- [ok] G_kita_loewenzahn_11 (buendelung, Prio 2): Mass 0
- [ok] G_kita_villakunterbunt_12 (buendelung, Prio 2): Mass 0
- [ok] G_schulbegleitung (buendelung, Prio 2): Mass 0
- [ok] geschlecht=w (balance, Prio 2): Mass 0
- [ok] sprachfoerderung=ja (balance, Prio 2): Mass 0
- [ok] wunsch[31]:S053+S025 (wunsch_getrennt, Prio 2): Mass 0
- [ok] wunsch[32]:S013+S085 (wunsch_getrennt, Prio 2): Mass 0
- [ok] wunsch[33]:S056+S046 (wunsch_getrennt, Prio 2): Mass 0
- [VERLETZT] wunsch[3]:S061+S071 (wunsch_zusammen, Prio 1): Mass 1
- [ok] G_nordstadt (buendelung, Prio 1): Mass 0
- [ok] kann_kind=ja (balance, Prio 1): Mass 0
- [ok] wunsch[0]:S098+S069 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[1]:S099+S017 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[2]:S017+S085 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[4]:S022+S034 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[5]:S068+S078 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[6]:S055+S028 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[7]:S070+S097 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[8]:S094+S089 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[9]:S026+S092 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[10]:S040+S052 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[11]:S086+S084 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[12]:S048+S057 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[13]:S067+S058 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[14]:S016+S032 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[15]:S029+S009 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[16]:S044+S003 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[17]:S076+S071 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[18]:S030+S076 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[19]:S029+S001 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[20]:S010+S091 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[21]:S081+S008 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[22]:S030+S009 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[23]:S005+S043 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[24]:S010+S066 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[25]:S031+S036 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[26]:S086+S063 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[27]:S028+S070 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[28]:S017+S093 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[29]:S074+S061 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[30]:S032+S061 (wunsch_zusammen, Prio 1): Mass 0

## Variante 2 (Feasible, Zielwert 1001)

Diff zu Variante 1: **8 Kinder anders zugeordnet**.

Ampel: 4 rot, 29 gelb, 56 gruen, 11 frei (von keinem Kriterium betroffen).

| Klasse | Groesse | Kinder |
|---|---|---|
| 1a | 26 | S001, S008, S009, S025, S026, S028, S029, S030, S037, S039, S050, S054, S055, S056, S058, S067, S068, S070, S071, S075, S076, S077, S078, S081, S092, S097 |
| 1b | 26 | S002, S010, S013, S016, S020, S022, S023, S024, S027, S031, S032, S034, S035, S036, S040, S052, S061, S062, S065, S066, S072, S074, S082, S083, S090, S091 |
| 1c | 22 | S003, S015, S021, S038, S041, S042, S044, S046, S048, S049, S051, S057, S059, S064, S069, S079, S087, S088, S089, S094, S095, S098 |
| 1d | 26 | S004, S005, S006, S007, S011, S012, S014, S017, S018, S019, S033, S043, S045, S047, S053, S060, S063, S073, S080, S084, S085, S086, S093, S096, S099, S100 |

Balance-Kennzahlen (Anzahl je Klasse):

- geschlecht=w: 14 / 11 / 11 / 12 (Ziel ~12 +/- 2)
- sprachfoerderung=ja: 4 / 3 / 5 / 4 (Ziel ~4 +/- 1)
- kann_kind=ja: 2 / 1 / 3 / 3 (Ziel ~2 +/- 1)

Verletzungsreport (weiche Regeln):

- [VERLETZT] G_sozialverhalten (verteilung, Prio 3): Mass 1
- [ok] G_kita_sonnenblume_1 (buendelung, Prio 2): Mass 0
- [ok] G_kita_regenbogen_2 (buendelung, Prio 2): Mass 0
- [ok] G_kita_pusteblume_3 (buendelung, Prio 2): Mass 0
- [ok] G_kita_wirbelwind_4 (buendelung, Prio 2): Mass 0
- [ok] G_kita_loewenzahn_5 (buendelung, Prio 2): Mass 0
- [ok] G_kita_villakunterbunt_6 (buendelung, Prio 2): Mass 0
- [ok] G_kita_sonnenblume_7 (buendelung, Prio 2): Mass 0
- [ok] G_kita_regenbogen_8 (buendelung, Prio 2): Mass 0
- [ok] G_kita_pusteblume_9 (buendelung, Prio 2): Mass 0
- [ok] G_kita_wirbelwind_10 (buendelung, Prio 2): Mass 0
- [ok] G_kita_loewenzahn_11 (buendelung, Prio 2): Mass 0
- [ok] G_kita_villakunterbunt_12 (buendelung, Prio 2): Mass 0
- [ok] G_schulbegleitung (buendelung, Prio 2): Mass 0
- [ok] geschlecht=w (balance, Prio 2): Mass 0
- [ok] sprachfoerderung=ja (balance, Prio 2): Mass 0
- [ok] wunsch[31]:S053+S025 (wunsch_getrennt, Prio 2): Mass 0
- [ok] wunsch[32]:S013+S085 (wunsch_getrennt, Prio 2): Mass 0
- [ok] wunsch[33]:S056+S046 (wunsch_getrennt, Prio 2): Mass 0
- [VERLETZT] wunsch[3]:S061+S071 (wunsch_zusammen, Prio 1): Mass 1
- [ok] G_nordstadt (buendelung, Prio 1): Mass 0
- [ok] kann_kind=ja (balance, Prio 1): Mass 0
- [ok] wunsch[0]:S098+S069 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[1]:S099+S017 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[2]:S017+S085 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[4]:S022+S034 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[5]:S068+S078 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[6]:S055+S028 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[7]:S070+S097 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[8]:S094+S089 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[9]:S026+S092 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[10]:S040+S052 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[11]:S086+S084 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[12]:S048+S057 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[13]:S067+S058 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[14]:S016+S032 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[15]:S029+S009 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[16]:S044+S003 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[17]:S076+S071 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[18]:S030+S076 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[19]:S029+S001 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[20]:S010+S091 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[21]:S081+S008 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[22]:S030+S009 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[23]:S005+S043 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[24]:S010+S066 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[25]:S031+S036 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[26]:S086+S063 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[27]:S028+S070 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[28]:S017+S093 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[29]:S074+S061 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[30]:S032+S061 (wunsch_zusammen, Prio 1): Mass 0

## Variante 3 (Feasible, Zielwert 1002)

Diff zu Variante 1: **72 Kinder anders zugeordnet**.

Ampel: 11 rot, 19 gelb, 59 gruen, 11 frei (von keinem Kriterium betroffen).

| Klasse | Groesse | Kinder |
|---|---|---|
| 1a | 26 | S002, S007, S010, S012, S013, S014, S016, S020, S030, S032, S037, S040, S047, S050, S052, S053, S061, S063, S066, S071, S074, S076, S084, S086, S091, S100 |
| 1b | 22 | S001, S009, S011, S019, S025, S026, S028, S029, S039, S049, S054, S055, S058, S064, S067, S068, S070, S073, S077, S078, S092, S097 |
| 1c | 26 | S003, S006, S022, S023, S024, S027, S031, S034, S035, S036, S041, S044, S048, S056, S057, S060, S062, S065, S069, S079, S089, S090, S094, S095, S096, S098 |
| 1d | 26 | S004, S005, S008, S015, S017, S018, S021, S033, S038, S042, S043, S045, S046, S051, S059, S072, S075, S080, S081, S082, S083, S085, S087, S088, S093, S099 |

Balance-Kennzahlen (Anzahl je Klasse):

- geschlecht=w: 10 / 12 / 13 / 13 (Ziel ~12 +/- 2)
- sprachfoerderung=ja: 5 / 4 / 3 / 4 (Ziel ~4 +/- 1)
- kann_kind=ja: 2 / 2 / 3 / 2 (Ziel ~2 +/- 1)

Verletzungsreport (weiche Regeln):

- [VERLETZT] G_sozialverhalten (verteilung, Prio 3): Mass 1
- [ok] G_kita_sonnenblume_1 (buendelung, Prio 2): Mass 0
- [ok] G_kita_regenbogen_2 (buendelung, Prio 2): Mass 0
- [ok] G_kita_pusteblume_3 (buendelung, Prio 2): Mass 0
- [ok] G_kita_wirbelwind_4 (buendelung, Prio 2): Mass 0
- [ok] G_kita_loewenzahn_5 (buendelung, Prio 2): Mass 0
- [ok] G_kita_villakunterbunt_6 (buendelung, Prio 2): Mass 0
- [ok] G_kita_sonnenblume_7 (buendelung, Prio 2): Mass 0
- [ok] G_kita_regenbogen_8 (buendelung, Prio 2): Mass 0
- [ok] G_kita_pusteblume_9 (buendelung, Prio 2): Mass 0
- [ok] G_kita_wirbelwind_10 (buendelung, Prio 2): Mass 0
- [ok] G_kita_loewenzahn_11 (buendelung, Prio 2): Mass 0
- [ok] G_kita_villakunterbunt_12 (buendelung, Prio 2): Mass 0
- [ok] G_schulbegleitung (buendelung, Prio 2): Mass 0
- [ok] geschlecht=w (balance, Prio 2): Mass 0
- [ok] sprachfoerderung=ja (balance, Prio 2): Mass 0
- [ok] wunsch[31]:S053+S025 (wunsch_getrennt, Prio 2): Mass 0
- [ok] wunsch[32]:S013+S085 (wunsch_getrennt, Prio 2): Mass 0
- [ok] wunsch[33]:S056+S046 (wunsch_getrennt, Prio 2): Mass 0
- [VERLETZT] G_nordstadt (buendelung, Prio 1): Mass 1
- [VERLETZT] wunsch[22]:S030+S009 (wunsch_zusammen, Prio 1): Mass 1
- [ok] kann_kind=ja (balance, Prio 1): Mass 0
- [ok] wunsch[0]:S098+S069 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[1]:S099+S017 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[2]:S017+S085 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[3]:S061+S071 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[4]:S022+S034 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[5]:S068+S078 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[6]:S055+S028 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[7]:S070+S097 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[8]:S094+S089 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[9]:S026+S092 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[10]:S040+S052 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[11]:S086+S084 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[12]:S048+S057 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[13]:S067+S058 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[14]:S016+S032 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[15]:S029+S009 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[16]:S044+S003 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[17]:S076+S071 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[18]:S030+S076 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[19]:S029+S001 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[20]:S010+S091 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[21]:S081+S008 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[23]:S005+S043 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[24]:S010+S066 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[25]:S031+S036 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[26]:S086+S063 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[27]:S028+S070 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[28]:S017+S093 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[29]:S074+S061 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[30]:S032+S061 (wunsch_zusammen, Prio 1): Mass 0
