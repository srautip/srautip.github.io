"""Independent solution checker.

This module deliberately shares NO code with timetable_model.py. It
re-derives every check directly from the JSON constraint list and the
solver's output schedule, in plain Python with no CP-SAT involved.

Rationale: if the checker reused the same encoding logic as the model
builder (e.g. the same "sessions_of_class" helper), a bug in that shared
logic would be invisible to tests - the checker would simply agree with
whatever the buggy model produced. A truly independent verifier is the
only way to catch translation bugs in the CP-SAT builder itself.
"""
from __future__ import annotations


def verify_schedule(data: dict, schedule: list[dict]) -> list[str]:
    """Returns a list of human-readable violation strings (empty = OK)."""
    violations: list[str] = []
    ent = data["entities"]
    all_days = ent["timeslots"]["days"]
    all_periods = list(range(1, ent["timeslots"]["periods_per_day"] + 1))

    def find(cls=None, teacher=None, day=None, period=None, room=None, subject=None):
        return [
            l
            for l in schedule
            if (cls is None or l["class"] == cls)
            and (teacher is None or l["teacher"] == teacher)
            and (day is None or l["day"] == day)
            and (period is None or l["period"] == period)
            and (room is None or l["room"] == room)
            and (subject is None or l["subject"] == subject)
        ]

    for c in data["constraints"]:
        t = c["type"]

        if t == "teacher_availability":
            teacher = c["teacher"]
            avail = set(c.get("available_days") or all_days)
            blocked = {(p["day"], p["period"]) for p in c.get("unavailable_periods", [])}
            for l in find(teacher=teacher):
                if l["day"] not in avail:
                    violations.append(
                        f"{teacher} unterrichtet an {l['day']}, ist dort aber nicht verfuegbar"
                    )
                if (l["day"], l["period"]) in blocked:
                    violations.append(
                        f"{teacher} unterrichtet {l['day']}/{l['period']}, obwohl explizit gesperrt"
                    )

        elif t == "weekly_hours":
            cnt = len(find(cls=c["class"], subject=c["subject"]))
            if cnt != c["hours_per_week"]:
                violations.append(
                    f"{c['class']}/{c['subject']}: {cnt}h geplant, {c['hours_per_week']}h gefordert"
                )
            if c.get("max_per_day"):
                by_day: dict[str, int] = {}
                for l in find(cls=c["class"], subject=c["subject"]):
                    by_day[l["day"]] = by_day.get(l["day"], 0) + 1
                for d, n in by_day.items():
                    if n > c["max_per_day"]:
                        violations.append(
                            f"{c['class']}/{c['subject']} am {d}: {n}h > erlaubtes "
                            f"Maximum {c['max_per_day']}h/Tag"
                        )

        elif t == "room_requirement":
            for l in find(subject=c["subject"]):
                if l["room"] not in c["allowed_rooms"]:
                    violations.append(
                        f"{c['subject']} ({l['class']}, {l['day']}/{l['period']}) in Raum "
                        f"{l['room']}, erlaubt sind nur {c['allowed_rooms']}"
                    )

        elif t == "no_overlap":
            resource, entity = c["resource"], c["entity"]
            key = {"class": "class", "teacher": "teacher", "room": "room"}[resource]
            seen: dict[tuple, list] = {}
            for l in schedule:
                if l[key] != entity:
                    continue
                seen.setdefault((l["day"], l["period"]), []).append(l)
            for slot, items in seen.items():
                if len(items) > 1:
                    violations.append(f"{resource} {entity} doppelt belegt am {slot}: {items}")

        elif t == "shared_resource_conflict":
            for d in all_days:
                for p in all_periods:
                    hits = [
                        l
                        for l in schedule
                        if l["teacher"] == c["teacher"]
                        and l["subject"] == c["subject"]
                        and l["class"] in c["classes"]
                        and l["day"] == d
                        and l["period"] == p
                    ]
                    if len(hits) > 1:
                        violations.append(
                            f"{c['teacher']} gleichzeitig in {[h['class'] for h in hits]} "
                            f"am {d}/{p} ({c['subject']})"
                        )

        elif t == "forbidden_slot":
            scope, entity = c["scope"], c["entity"]
            key = {"class": "class", "teacher": "teacher", "room": "room"}[scope]
            for l in find(day=c["day"], period=c["period"]):
                if l[key] == entity:
                    violations.append(
                        f"{entity} ({scope}) hat Unterricht im gesperrten Slot "
                        f"{c['day']}/{c['period']}"
                    )

        elif t == "consecutive_required":
            by_day: dict[str, list] = {}
            for l in find(cls=c["class"], subject=c["subject"]):
                by_day.setdefault(l["day"], []).append(l["period"])
            for d, ps in by_day.items():
                ps = sorted(ps)
                i = 0
                while i < len(ps):
                    run = [ps[i]]
                    while i + 1 < len(ps) and ps[i + 1] == ps[i] + 1:
                        i += 1
                        run.append(ps[i])
                    if len(run) != c["block_length"]:
                        violations.append(
                            f"{c['class']}/{c['subject']} am {d}: Block der Laenge "
                            f"{len(run)} statt geforderter {c['block_length']} ({run})"
                        )
                    i += 1

        elif t == "teacher_subject_assignment":
            for l in find(cls=c["class"], subject=c["subject"]):
                if l["teacher"] != c["teacher"]:
                    violations.append(
                        f"{c['class']}/{c['subject']} wird von {l['teacher']} statt "
                        f"vorgeschriebener Lehrkraft {c['teacher']} unterrichtet"
                    )

        else:
            violations.append(f"Unbekannter Constraint-Typ im Verifier: {t!r}")

    return violations
