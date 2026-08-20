"""Curated version of the LLM-generated `decomposed_result.json` from the
chat session, used for the integration test.

Manual corrections applied on top of the raw LLM output (documented here,
not silently):

1. The `consecutive_required` entry had `"class": "Chemie"` (subject name
   used as class name - a real bug the deterministic validator caught).
   Fixed to two entries, one per class (5a, 5b).
2. The original natural-language prompt never named a Chemistry teacher or
   said which classes take Chemistry - so the LLM correctly did not invent
   a `teacher_subject_assignment` for it. For this integration test we add
   one explicitly (Frau Wagner, Chemie, 5a+5b) plus matching `weekly_hours`
   (2h/week - a multiple of the block_length so the schedule is solvable),
   otherwise there is nothing to schedule for the room/block constraints to
   act on.
"""

FULL_SCENARIO = {
    "entities": {
        "classes": ["5a", "5b"],
        "teachers": ["Herr Meier", "Frau Schmidt", "Frau Wagner"],
        "subjects": ["Mathematik", "Chemie", "Sport"],
        "rooms": ["R101"],
        "timeslots": {"days": ["Mo", "Di", "Mi", "Do", "Fr"], "periods_per_day": 6},
    },
    "constraints": [
        {
            "type": "teacher_availability",
            "teacher": "Herr Meier",
            "available_days": ["Di", "Do"],
            "unavailable_periods": [{"day": "Mo", "period": 1}],
        },
        {"type": "weekly_hours", "class": "5a", "subject": "Mathematik",
         "hours_per_week": 4, "max_per_day": 2},
        {"type": "weekly_hours", "class": "5a", "subject": "Chemie",
         "hours_per_week": 2},
        {"type": "weekly_hours", "class": "5b", "subject": "Chemie",
         "hours_per_week": 2},
        {"type": "room_requirement", "subject": "Chemie", "allowed_rooms": ["R101"]},
        {"type": "no_overlap", "resource": "class", "entity": "5a"},
        {"type": "no_overlap", "resource": "class", "entity": "5b"},
        {"type": "no_overlap", "resource": "teacher", "entity": "Herr Meier"},
        {"type": "no_overlap", "resource": "teacher", "entity": "Frau Schmidt"},
        {"type": "no_overlap", "resource": "teacher", "entity": "Frau Wagner"},
        {"type": "shared_resource_conflict", "classes": ["5a", "5b"],
         "subject": "Sport", "teacher": "Frau Schmidt"},
        {"type": "forbidden_slot", "scope": "class", "entity": "5a", "day": "Fr", "period": 6},
        {"type": "forbidden_slot", "scope": "class", "entity": "5b", "day": "Fr", "period": 6},
        {"type": "consecutive_required", "class": "5a", "subject": "Chemie", "block_length": 2},
        {"type": "consecutive_required", "class": "5b", "subject": "Chemie", "block_length": 2},
        {"type": "teacher_subject_assignment", "teacher": "Herr Meier", "class": "5a", "subject": "Mathematik"},
        {"type": "teacher_subject_assignment", "teacher": "Herr Meier", "class": "5b", "subject": "Mathematik"},
        {"type": "teacher_subject_assignment", "teacher": "Frau Schmidt", "class": "5a", "subject": "Sport"},
        {"type": "teacher_subject_assignment", "teacher": "Frau Schmidt", "class": "5b", "subject": "Sport"},
        {"type": "teacher_subject_assignment", "teacher": "Frau Wagner", "class": "5a", "subject": "Chemie"},
        {"type": "teacher_subject_assignment", "teacher": "Frau Wagner", "class": "5b", "subject": "Chemie"},
    ],
}
