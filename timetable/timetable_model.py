"""Build and solve a CP-SAT timetabling model from the JSON constraint format
produced by the Qwen extraction pipeline.

Modeling approach
------------------
Every ``teacher_subject_assignment`` entry defines a "session": a
(class, subject, teacher) triple that needs to be scheduled some number of
times per week. For each session and each (day, period) slot we create a
boolean decision variable ``lesson[class, subject, teacher, day, period]``.

If a subject has a ``room_requirement``, each of its sessions additionally
gets one boolean room-choice variable per allowed room and slot, linked to
the lesson variable by an equality constraint.

All other constraint types translate directly into CP-SAT constraints over
these variables. See ``_apply_constraints`` for the mapping.

Known simplification: a session with no matching ``weekly_hours`` constraint
is left unconstrained in count - the solver may schedule it zero times.
Production use should require ``weekly_hours`` for every session.
"""
from __future__ import annotations

from dataclasses import dataclass

from ortools.sat.python import cp_model


@dataclass(frozen=True)
class Session:
    class_name: str
    subject: str
    teacher: str


def _sessions_from_assignments(data: dict) -> list[Session]:
    sessions = [
        Session(c["class"], c["subject"], c["teacher"])
        for c in data["constraints"]
        if c["type"] == "teacher_subject_assignment"
    ]
    if not sessions:
        raise ValueError(
            "Keine teacher_subject_assignment-Constraints gefunden - "
            "es gibt nichts zu planen."
        )
    return sessions


def build_model(data: dict):
    """Build the CP-SAT model.

    Returns (model, lesson_vars, room_vars, sessions, days, periods).
    """
    model = cp_model.CpModel()
    ent = data["entities"]
    days: list[str] = ent["timeslots"]["days"]
    periods: list[int] = list(range(1, ent["timeslots"]["periods_per_day"] + 1))

    sessions = _sessions_from_assignments(data)

    lesson: dict[tuple, cp_model.IntVar] = {}
    for s in sessions:
        for d in days:
            for p in periods:
                lesson[(s.class_name, s.subject, s.teacher, d, p)] = model.NewBoolVar(
                    f"lesson_{s.class_name}_{s.subject}_{s.teacher}_{d}_{p}"
                )

    room_req = {
        c["subject"]: c["allowed_rooms"]
        for c in data["constraints"]
        if c["type"] == "room_requirement"
    }

    room: dict[tuple, cp_model.IntVar] = {}
    for s in sessions:
        allowed_rooms = room_req.get(s.subject)
        if not allowed_rooms:
            continue
        for d in days:
            for p in periods:
                choices = []
                for r in allowed_rooms:
                    v = model.NewBoolVar(
                        f"room_{s.class_name}_{s.subject}_{s.teacher}_{d}_{p}_{r}"
                    )
                    room[(s.class_name, s.subject, s.teacher, d, p, r)] = v
                    choices.append(v)
                # Exactly one allowed room is chosen iff the lesson happens.
                model.Add(sum(choices) == lesson[(s.class_name, s.subject, s.teacher, d, p)])

    _apply_constraints(model, data, sessions, lesson, room, days, periods)

    return model, lesson, room, sessions, days, periods


def _add_block_constraint(model, lesson, session: Session, days, periods, block_len: int):
    """Force the periods where `session` runs to form contiguous blocks of
    exactly `block_len`, independently per day (no partial/odd-length runs)."""
    for d in days:
        valid_starts = [p for p in periods if p + block_len - 1 <= periods[-1]]
        block_start = {
            p0: model.NewBoolVar(
                f"block_{session.class_name}_{session.subject}_{session.teacher}_{d}_{p0}"
            )
            for p0 in valid_starts
        }
        for p in periods:
            covering = [
                block_start[p0]
                for p0 in valid_starts
                if p0 <= p <= p0 + block_len - 1
            ]
            key = (session.class_name, session.subject, session.teacher, d, p)
            # Equality to a 0/1 variable also enforces "at most one covering
            # block", so no separate non-overlap constraint is needed.
            model.Add(lesson[key] == sum(covering))


def _apply_constraints(model, data, sessions, lesson, room, days, periods):
    def sessions_of_class(cls):
        return [s for s in sessions if s.class_name == cls]

    def sessions_of_teacher(t):
        return [s for s in sessions if s.teacher == t]

    def sessions_of_subject_class(cls, subj):
        return [s for s in sessions if s.class_name == cls and s.subject == subj]

    for c in data["constraints"]:
        ctype = c["type"]

        if ctype == "teacher_availability":
            teacher = c["teacher"]
            avail_days = set(c.get("available_days") or days)
            blocked = {(p["day"], p["period"]) for p in c.get("unavailable_periods", [])}
            for s in sessions_of_teacher(teacher):
                for d in days:
                    for p in periods:
                        if d not in avail_days or (d, p) in blocked:
                            model.Add(lesson[(s.class_name, s.subject, s.teacher, d, p)] == 0)

        elif ctype == "weekly_hours":
            cls, subj = c["class"], c["subject"]
            for s in sessions_of_subject_class(cls, subj):
                total = sum(
                    lesson[(s.class_name, s.subject, s.teacher, d, p)]
                    for d in days
                    for p in periods
                )
                model.Add(total == c["hours_per_week"])
                if c.get("max_per_day"):
                    for d in days:
                        day_total = sum(
                            lesson[(s.class_name, s.subject, s.teacher, d, p)] for p in periods
                        )
                        model.Add(day_total <= c["max_per_day"])

        elif ctype == "no_overlap":
            resource, entity = c["resource"], c["entity"]
            if resource == "class":
                relevant = sessions_of_class(entity)
                for d in days:
                    for p in periods:
                        vs = [lesson[(s.class_name, s.subject, s.teacher, d, p)] for s in relevant]
                        if vs:
                            model.Add(sum(vs) <= 1)
            elif resource == "teacher":
                relevant = sessions_of_teacher(entity)
                for d in days:
                    for p in periods:
                        vs = [lesson[(s.class_name, s.subject, s.teacher, d, p)] for s in relevant]
                        if vs:
                            model.Add(sum(vs) <= 1)
            elif resource == "room":
                for d in days:
                    for p in periods:
                        vs = [
                            v
                            for (cl, su, te, dd, pp, r), v in room.items()
                            if r == entity and dd == d and pp == p
                        ]
                        if vs:
                            model.Add(sum(vs) <= 1)

        elif ctype == "shared_resource_conflict":
            classes, subj, teacher = c["classes"], c["subject"], c["teacher"]
            for d in days:
                for p in periods:
                    vs = [
                        lesson[(s.class_name, s.subject, s.teacher, d, p)]
                        for cls in classes
                        for s in sessions_of_subject_class(cls, subj)
                        if s.teacher == teacher
                    ]
                    if vs:
                        model.Add(sum(vs) <= 1)

        elif ctype == "forbidden_slot":
            scope, entity, day, period = c["scope"], c["entity"], c["day"], c["period"]
            if scope == "class":
                for s in sessions_of_class(entity):
                    model.Add(lesson[(s.class_name, s.subject, s.teacher, day, period)] == 0)
            elif scope == "teacher":
                for s in sessions_of_teacher(entity):
                    model.Add(lesson[(s.class_name, s.subject, s.teacher, day, period)] == 0)
            elif scope == "room":
                for (cl, su, te, d, p, r), v in room.items():
                    if r == entity and d == day and p == period:
                        model.Add(v == 0)

        elif ctype == "consecutive_required":
            cls, subj, block_len = c["class"], c["subject"], c["block_length"]
            for s in sessions_of_subject_class(cls, subj):
                _add_block_constraint(model, lesson, s, days, periods, block_len)

        elif ctype in ("teacher_subject_assignment", "room_requirement"):
            pass  # already consumed above to build sessions / room variables

        else:
            raise ValueError(f"Unbekannter Constraint-Typ: {ctype!r}")


def solve(data: dict, time_limit_s: float = 30.0, seed: int = 42, num_workers: int = 1):
    """Solve the model. Returns (status, solver, schedule).

    schedule is a list of dicts (or None if infeasible/unknown), one per
    scheduled lesson: {"class", "subject", "teacher", "day", "period", "room"}.
    """
    model, lesson, room, sessions, days, periods = build_model(data)

    solver = cp_model.CpSolver()
    solver.parameters.max_time_in_seconds = time_limit_s
    solver.parameters.random_seed = seed
    solver.parameters.num_search_workers = num_workers
    status = solver.Solve(model)

    schedule = None
    if status in (cp_model.OPTIMAL, cp_model.FEASIBLE):
        schedule = []
        for (cls, subj, teacher, d, p), v in lesson.items():
            if solver.Value(v):
                assigned_room = None
                for (cl, su, te, dd, pp, r), rv in room.items():
                    if (cl, su, te, dd, pp) == (cls, subj, teacher, d, p) and solver.Value(rv):
                        assigned_room = r
                        break
                schedule.append(
                    {
                        "class": cls,
                        "subject": subj,
                        "teacher": teacher,
                        "day": d,
                        "period": p,
                        "room": assigned_room,
                    }
                )
    return status, solver, schedule


def status_name(status) -> str:
    return cp_model.CpSolver().StatusName(status)
