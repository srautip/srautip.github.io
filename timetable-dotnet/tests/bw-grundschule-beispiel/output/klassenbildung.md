# Klassenbildung: bw-grundschule-beispiel

100 Kinder -> 4 Klassen (22-26) | Regeln: 16 Gruppen, 3 Balance, 34 Wuensche, 1 Fixierungen | Parameter: 3 Varianten, epsilon 0.05, min_distanz 8, Zeitlimit 20s je Lauf

*Der Solver liefert VORSCHLAEGE - die Klassenzuordnung entscheidet die Schulleitung (menschliche Letztentscheidung, Konzept Abschnitt 10). Dieser Report dokumentiert Regeln, Parameter und Abweichungen jedes Vorschlags.*

## Konsens-Kern

51 von 100 Kindern sind in allen 3 Varianten identisch zugeordnet - der stabile Kern fuer eine Bulk-Fixierung.

## Variante 1 (Feasible, Zielwert 1002)

Ampel: 10 rot, 22 gelb, 57 gruen, 11 frei (von keinem Kriterium betroffen).

| Klasse | Groesse | Kinder |
|---|---|---|
| 1a | 26 | S001, S009, S011, S013, S016, S021, S026, S029, S030, S032, S039, S040, S052, S054, S058, S061, S067, S071, S074, S076, S087, S088, S090, S092, S097, S100 |
| 1b | 22 | S002, S006, S010, S014, S020, S023, S037, S042, S047, S050, S056, S059, S060, S065, S066, S072, S075, S082, S083, S091, S095, S096 |
| 1c | 26 | S003, S007, S008, S012, S015, S022, S033, S034, S035, S041, S044, S046, S048, S049, S051, S053, S057, S063, S069, S079, S081, S084, S086, S089, S094, S098 |
| 1d | 26 | S004, S005, S017, S018, S019, S024, S025, S027, S028, S031, S036, S038, S043, S045, S055, S062, S064, S068, S070, S073, S077, S078, S080, S085, S093, S099 |

Balance-Kennzahlen (Anzahl je Klasse):

- geschlecht=w: 13 / 12 / 10 / 13 (Ziel ~12 +/- 2)
- sprachfoerderung=ja: 5 / 4 / 4 / 3 (Ziel ~4 +/- 1)
- kann_kind=ja: 1 / 2 / 3 / 3 (Ziel ~2 +/- 1)

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
- [VERLETZT] wunsch[7]:S070+S097 (wunsch_zusammen, Prio 1): Mass 1
- [ok] kann_kind=ja (balance, Prio 1): Mass 0
- [ok] wunsch[0]:S098+S069 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[1]:S099+S017 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[2]:S017+S085 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[3]:S061+S071 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[4]:S022+S034 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[5]:S068+S078 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[6]:S055+S028 (wunsch_zusammen, Prio 1): Mass 0
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

Diff zu Variante 1: **35 Kinder anders zugeordnet**.

Ampel: 9 rot, 31 gelb, 49 gruen, 11 frei (von keinem Kriterium betroffen).

| Klasse | Groesse | Kinder |
|---|---|---|
| 1a | 26 | S001, S009, S013, S016, S026, S028, S029, S030, S032, S037, S040, S050, S052, S055, S058, S061, S067, S068, S070, S071, S074, S076, S078, S088, S092, S097 |
| 1b | 24 | S002, S006, S008, S010, S020, S022, S023, S027, S034, S042, S046, S051, S060, S065, S066, S072, S073, S075, S081, S082, S083, S087, S091, S096 |
| 1c | 24 | S003, S007, S011, S012, S015, S019, S021, S024, S038, S044, S048, S049, S053, S056, S057, S059, S063, S069, S079, S084, S086, S095, S098, S100 |
| 1d | 26 | S004, S005, S014, S017, S018, S025, S031, S033, S035, S036, S039, S041, S043, S045, S047, S054, S062, S064, S077, S080, S085, S089, S090, S093, S094, S099 |

Balance-Kennzahlen (Anzahl je Klasse):

- geschlecht=w: 13 / 10 / 11 / 14 (Ziel ~12 +/- 2)
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
- [VERLETZT] G_nordstadt (buendelung, Prio 1): Mass 1
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
- [ok] wunsch[22]:S030+S009 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[23]:S005+S043 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[24]:S010+S066 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[25]:S031+S036 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[26]:S086+S063 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[27]:S028+S070 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[28]:S017+S093 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[29]:S074+S061 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[30]:S032+S061 (wunsch_zusammen, Prio 1): Mass 0

## Variante 3 (Feasible, Zielwert 1001)

Diff zu Variante 1: **43 Kinder anders zugeordnet**.

Ampel: 9 rot, 32 gelb, 48 gruen, 11 frei (von keinem Kriterium betroffen).

| Klasse | Groesse | Kinder |
|---|---|---|
| 1a | 26 | S001, S009, S013, S016, S021, S025, S026, S028, S029, S030, S032, S040, S052, S055, S058, S060, S061, S067, S070, S071, S073, S074, S076, S083, S092, S097 |
| 1b | 26 | S002, S008, S010, S011, S020, S023, S027, S031, S033, S035, S036, S039, S046, S049, S051, S054, S062, S065, S066, S068, S072, S077, S078, S081, S087, S091 |
| 1c | 26 | S003, S007, S012, S015, S019, S022, S024, S034, S044, S048, S053, S056, S057, S059, S063, S069, S079, S082, S084, S086, S088, S089, S094, S095, S098, S100 |
| 1d | 22 | S004, S005, S006, S014, S017, S018, S037, S038, S041, S042, S043, S045, S047, S050, S064, S075, S080, S085, S090, S093, S096, S099 |

Balance-Kennzahlen (Anzahl je Klasse):

- geschlecht=w: 14 / 11 / 13 / 10 (Ziel ~12 +/- 2)
- sprachfoerderung=ja: 5 / 4 / 3 / 4 (Ziel ~4 +/- 1)
- kann_kind=ja: 1 / 2 / 3 / 3 (Ziel ~2 +/- 1)

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
- [ok] wunsch[22]:S030+S009 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[23]:S005+S043 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[24]:S010+S066 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[25]:S031+S036 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[26]:S086+S063 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[27]:S028+S070 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[28]:S017+S093 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[29]:S074+S061 (wunsch_zusammen, Prio 1): Mass 0
- [ok] wunsch[30]:S032+S061 (wunsch_zusammen, Prio 1): Mass 0
