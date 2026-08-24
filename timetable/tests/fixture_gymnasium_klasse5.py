"""Test scenario for a 4-zuegiges Gymnasium, Klassenstufe 5, angelehnt an
eine typische Baden-Wuerttemberg-Stundentafel (G9) und die Groessenordnung
einer Schule wie dem GSG Fellbach.

Wichtiger Hinweis: Dies ist KEIN verifizierter Auszug aus dem echten
Lehrplan, Kollegium oder Raumplan einer bestimmten Schule. Fach- und
Stundenverteilung orientieren sich an oeffentlich bekannten, typischen
BW-Gymnasium-Kontingentstundentafeln fuer Klasse 5, wurden aber vereinfacht
und an das aktuelle Constraint-Schema angepasst - insbesondere:

- Konfessioneller Religionsunterricht (katholisch/evangelisch/Ethik in
  klassenuebergreifenden Gruppen zur gleichen "Bandzeit") wird NICHT
  abgebildet, da das aktuelle Schema keine klassenuebergreifenden
  Lerngruppen kennt. Religion/Ethik ist hier vereinfacht ein normales
  Fach pro Klasse mit einer festen Lehrkraft.
- BNT (Biologie, Naturphaenomene und Technik) wird real oft als 3h/Woche
  (Doppelstunde + Einzelstunde) unterrichtet. Das aktuelle
  `consecutive_required` erzwingt aber ALLE Wochenstunden eines Fachs in
  gleich langen Bloecken (siehe timetable_model.py) - eine gemischte
  2+1-Aufteilung ist damit nicht darstellbar. Hier daher bewusst auf
  4h/Woche (2x Doppelstunde) gesetzt, um innerhalb der Schema-Grenzen zu
  bleiben. Das ist eine reale Einschraenkung des aktuellen Modells, keine
  Vereinfachung der Testdaten aus Bequemlichkeit.

Groessenordnung: 4 Klassen, 9 Faecher, 15 Lehrkraefte, 5 Fachraeume,
30 Wochenstunden/Klasse bei 5 Tagen x 7 Stunden (35 Slots) - deutlich
groesser als die bisherige 2-Klassen-Fixture, um die Pipeline unter
realistischerer Last zu testen.
"""

CLASSES = ["5a", "5b", "5c", "5d"]
DAYS = ["Mo", "Di", "Mi", "Do", "Fr"]
PERIODS_PER_DAY = 7

# subject, teacher, classes taught by this teacher, hours_per_week,
# max_per_day, consecutive block_length (None = kein Block-Zwang),
# allowed_rooms (None = normaler Klassenraum, kein Fachraum-Zwang)
ASSIGNMENTS = [
    ("Deutsch", "Frau Vogel", ["5a", "5b"], 5, 2, None, None),
    ("Deutsch", "Herr Baumann", ["5c", "5d"], 5, 2, None, None),
    ("Mathematik", "Herr Krause", ["5a", "5c"], 5, 2, None, None),
    ("Mathematik", "Frau Nguyen", ["5b", "5d"], 5, 2, None, None),
    ("Englisch", "Frau Fischer", ["5a", "5d"], 5, 2, None, None),
    ("Englisch", "Herr Roth", ["5b", "5c"], 5, 2, None, None),
    ("BNT", "Frau Kraemer", ["5a", "5b"], 4, 2, 2, ["NaWi-Raum"]),
    ("BNT", "Herr Werner", ["5c", "5d"], 4, 2, 2, ["NaWi-Raum"]),
    ("Sport", "Herr Braun", ["5a", "5b"], 3, 2, None, ["Sporthalle1", "Sporthalle2"]),
    ("Sport", "Frau Lang", ["5c", "5d"], 3, 2, None, ["Sporthalle1", "Sporthalle2"]),
    ("Musik", "Frau Adler", ["5a", "5b", "5c", "5d"], 2, 2, None, ["Musiksaal"]),
    ("Kunst", "Herr Schuster", ["5a", "5c"], 2, 2, None, ["Kunstraum"]),
    ("Kunst", "Frau Weiss", ["5b", "5d"], 2, 2, None, ["Kunstraum"]),
    ("Religion", "Pfarrer Huber", ["5a", "5b", "5c", "5d"], 2, 2, None, None),
    ("Erdkunde", "Herr Fink", ["5a", "5b", "5c", "5d"], 2, 2, None, None),
]


def _build_scenario() -> dict:
    teachers = sorted({row[1] for row in ASSIGNMENTS})
    subjects = sorted({row[0] for row in ASSIGNMENTS})
    rooms = sorted({r for row in ASSIGNMENTS if row[6] for r in row[6]})

    constraints = []

    for subject, teacher, subj_classes, hours, max_per_day, block_len, _rooms in ASSIGNMENTS:
        for cls in subj_classes:
            constraints.append({
                "type": "teacher_subject_assignment",
                "teacher": teacher, "class": cls, "subject": subject,
            })
            constraints.append({
                "type": "weekly_hours",
                "class": cls, "subject": subject,
                "hours_per_week": hours, "max_per_day": max_per_day,
            })
            if block_len:
                constraints.append({
                    "type": "consecutive_required",
                    "class": cls, "subject": subject, "block_length": block_len,
                })

    # room_requirement genau einmal pro Fach (mehrere Lehrkraefte pro Fach
    # teilen sich denselben Fachraum-Pool, siehe ASSIGNMENTS oben)
    rooms_per_subject = {}
    for subject, _teacher, _classes, _hours, _max, _block, subj_rooms in ASSIGNMENTS:
        if subj_rooms and subject not in rooms_per_subject:
            rooms_per_subject[subject] = subj_rooms
    for subject, allowed_rooms in rooms_per_subject.items():
        constraints.append({
            "type": "room_requirement", "subject": subject, "allowed_rooms": allowed_rooms,
        })

    # Standard-Ueberschneidungsfreiheit: jede Klasse, jede Lehrkraft, jeder Fachraum
    for cls in CLASSES:
        constraints.append({"type": "no_overlap", "resource": "class", "entity": cls})
    for teacher in teachers:
        constraints.append({"type": "no_overlap", "resource": "teacher", "entity": teacher})
    for room in rooms:
        constraints.append({"type": "no_overlap", "resource": "room", "entity": room})

    constraints.append({
        "type": "teacher_availability", "teacher": "Frau Nguyen",
        "available_days": ["Mo", "Di", "Mi"],
        "reason": "Teilzeit",
    })
    constraints.append({
        "type": "teacher_availability", "teacher": "Herr Werner",
        "available_days": ["Mo", "Di", "Mi", "Do"],
        "reason": "Fortbildungstag freitags",
    })

    # Schulweite Sperrzeiten fuer Klasse 5: Mittwochnachmittag frei,
    # frueherer Schulschluss freitags.
    for cls in CLASSES:
        constraints.append({
            "type": "forbidden_slot", "scope": "class", "entity": cls,
            "day": "Mi", "period": PERIODS_PER_DAY,
            "reason": "Mittwochnachmittag frei (Kl. 5)",
        })
        constraints.append({
            "type": "forbidden_slot", "scope": "class", "entity": cls,
            "day": "Fr", "period": PERIODS_PER_DAY,
            "reason": "Fruehere Schulschluss freitags",
        })

    return {
        "entities": {
            "classes": CLASSES,
            "teachers": teachers,
            "subjects": subjects,
            "rooms": rooms,
            "timeslots": {"days": DAYS, "periods_per_day": PERIODS_PER_DAY},
        },
        "constraints": constraints,
    }


GYMNASIUM_KLASSE5_SCENARIO = _build_scenario()
