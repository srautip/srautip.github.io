# Klassenbildung: bw-grundschule-beispiel

100 Kinder -> 4 Klassen (22-26) | Regeln: 16 Gruppen, 3 Balance, 34 Wuensche, 1 Fixierungen | Parameter: 3 Varianten, epsilon 0.05, min_distanz 8, Zeitlimit 20s je Lauf

*Der Solver liefert VORSCHLAEGE - die Klassenzuordnung entscheidet die Schulleitung (menschliche Letztentscheidung, Konzept Abschnitt 10). Dieser Report dokumentiert Regeln, Parameter und Abweichungen jedes Vorschlags.*

## Konsens-Kern

54 von 100 Kindern sind in allen 3 Varianten identisch zugeordnet - der stabile Kern fuer eine Bulk-Fixierung.

## Variante 1 (Optimal, Zielwert 1001)

Ampel: 4 rot, 41 gelb, 44 gruen, 11 frei (von keinem Kriterium betroffen).

| Klasse | Groesse | Kinder |
|---|---|---|
| 1a | 26 | S001, S009, S025, S026, S028, S029, S030, S037, S039, S050, S054, S055, S058, S067, S068, S070, S071, S073, S075, S076, S077, S078, S088, S092, S097, S100 |
| 1b | 26 | S002, S006, S010, S013, S016, S019, S020, S031, S032, S033, S035, S036, S040, S052, S060, S061, S062, S066, S072, S074, S087, S089, S090, S091, S094, S096 |
| 1c | 26 | S003, S004, S005, S008, S015, S017, S018, S021, S022, S024, S027, S034, S043, S044, S045, S046, S048, S057, S069, S079, S080, S081, S085, S093, S098, S099 |
| 1d | 22 | S007, S011, S012, S014, S023, S038, S041, S042, S047, S049, S051, S053, S056, S059, S063, S064, S065, S082, S083, S084, S086, S095 |

Balance-Kennzahlen (Anzahl je Klasse):

- geschlecht=w: 14 / 12 / 12 / 10 (Ziel ~12 +/- 2)
- sprachfoerderung=ja: 3 / 5 / 3 / 5 (Ziel ~4 +/- 1)
- kann_kind=ja: 3 / 1 / 3 / 2 (Ziel ~2 +/- 1)

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

## Variante 2 (Optimal, Zielwert 1001)

Diff zu Variante 1: **37 Kinder anders zugeordnet**.

Ampel: 4 rot, 30 gelb, 55 gruen, 11 frei (von keinem Kriterium betroffen).

| Klasse | Groesse | Kinder |
|---|---|---|
| 1a | 26 | S001, S008, S009, S025, S026, S028, S029, S030, S033, S038, S039, S054, S055, S058, S067, S068, S070, S071, S075, S076, S077, S078, S081, S088, S092, S097 |
| 1b | 26 | S002, S003, S010, S013, S016, S020, S022, S023, S032, S034, S040, S044, S048, S052, S057, S061, S065, S066, S069, S074, S079, S089, S091, S094, S098, S100 |
| 1c | 23 | S004, S005, S017, S018, S019, S021, S024, S027, S035, S037, S043, S045, S049, S050, S056, S059, S080, S082, S083, S085, S090, S093, S099 |
| 1d | 25 | S006, S007, S011, S012, S014, S015, S031, S036, S041, S042, S046, S047, S051, S053, S060, S062, S063, S064, S072, S073, S084, S086, S087, S095, S096 |

Balance-Kennzahlen (Anzahl je Klasse):

- geschlecht=w: 14 / 12 / 11 / 11 (Ziel ~12 +/- 2)
- sprachfoerderung=ja: 5 / 5 / 3 / 3 (Ziel ~4 +/- 1)
- kann_kind=ja: 2 / 3 / 2 / 2 (Ziel ~2 +/- 1)

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

## Variante 3 (Optimal, Zielwert 1001)

Diff zu Variante 1: **41 Kinder anders zugeordnet**.

Ampel: 4 rot, 33 gelb, 52 gruen, 11 frei (von keinem Kriterium betroffen).

| Klasse | Groesse | Kinder |
|---|---|---|
| 1a | 23 | S001, S009, S025, S026, S028, S029, S030, S033, S038, S039, S042, S054, S055, S056, S058, S067, S068, S070, S076, S077, S078, S092, S097 |
| 1b | 26 | S002, S003, S010, S013, S016, S020, S022, S031, S032, S034, S036, S040, S044, S048, S052, S057, S061, S062, S066, S069, S071, S072, S074, S079, S091, S098 |
| 1c | 26 | S004, S005, S014, S017, S018, S019, S021, S023, S024, S027, S037, S043, S045, S047, S049, S050, S051, S059, S065, S075, S080, S082, S085, S093, S099, S100 |
| 1d | 25 | S006, S007, S008, S011, S012, S015, S035, S041, S046, S053, S060, S063, S064, S073, S081, S083, S084, S086, S087, S088, S089, S090, S094, S095, S096 |

Balance-Kennzahlen (Anzahl je Klasse):

- geschlecht=w: 14 / 10 / 12 / 12 (Ziel ~12 +/- 2)
- sprachfoerderung=ja: 3 / 5 / 4 / 4 (Ziel ~4 +/- 1)
- kann_kind=ja: 3 / 2 / 3 / 1 (Ziel ~2 +/- 1)

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
- [VERLETZT] wunsch[17]:S076+S071 (wunsch_zusammen, Prio 1): Mass 1
- [ok] G_nordstadt (buendelung, Prio 1): Mass 0
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
