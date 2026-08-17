"""Test suite demonstrating how to test CP-SAT-generated timetabling code.

Testing philosophy used here (see README.md in this folder for the write-up):

1. Unit-test each constraint TYPE in isolation with a minimal scenario -
   easier to reason about, fast to run, pinpoints exactly which translation
   is broken if it fails.
2. Verify actual SOLUTIONS with an independent, hand-written checker
   (verifier.py) that shares no code with the model builder - never assert
   only `status == FEASIBLE`, always check the returned schedule really
   satisfies every constraint.
3. Use "pigeonhole" tests: make demand exceed capacity by exactly one unit
   and assert INFEASIBLE. This proves a constraint has real teeth, rather
   than being silently ignored (a no-op constraint would incorrectly stay
   FEASIBLE).
4. One integration test against the full multi-constraint scenario that
   came out of the LLM extraction pipeline (curated/bug-fixed, see
   fixture_full_scenario.py).
5. A determinism test: CP-SAT is only reproducible run-to-run if you pin
   `random_seed` AND `num_search_workers=1`; multi-threaded portfolio
   search is explicitly not required to return the same solution twice.
6. A "mutation" test that takes the full scenario and adds one
   contradictory requirement, expecting the status to flip to INFEASIBLE -
   this checks the constraints actually constrain something, in a model
   that's otherwise realistic-sized (not just the toy unit scenarios).
"""
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))

from ortools.sat.python import cp_model

from timetable_model import solve, status_name
from verifier import verify_schedule

from fixture_full_scenario import FULL_SCENARIO


def mini(classes, teachers, subjects, rooms, days, periods_per_day):
    return {
        "classes": classes,
        "teachers": teachers,
        "subjects": subjects,
        "rooms": rooms,
        "timeslots": {"days": days, "periods_per_day": periods_per_day},
    }


def scenario(entities, constraints):
    return {"entities": entities, "constraints": constraints}


# ---------------------------------------------------------------------------
# 1. Isolated unit tests, one per constraint type
# ---------------------------------------------------------------------------

def test_weekly_hours_exact_count():
    ent = mini(["5a"], ["T1"], ["Mathe"], [], ["Mo", "Di", "Mi"], 4)
    data = scenario(ent, [
        {"type": "weekly_hours", "class": "5a", "subject": "Mathe", "hours_per_week": 3},
        {"type": "teacher_subject_assignment", "teacher": "T1", "class": "5a", "subject": "Mathe"},
    ])
    status, solver, schedule = solve(data)
    assert status in (cp_model.OPTIMAL, cp_model.FEASIBLE), status_name(status)
    assert len(schedule) == 3
    assert verify_schedule(data, schedule) == []


def test_teacher_availability_restricts_days():
    ent = mini(["5a"], ["T1"], ["Mathe"], [], ["Mo", "Di", "Mi"], 2)
    data = scenario(ent, [
        {"type": "teacher_availability", "teacher": "T1", "available_days": ["Di"]},
        {"type": "weekly_hours", "class": "5a", "subject": "Mathe", "hours_per_week": 2},
        {"type": "teacher_subject_assignment", "teacher": "T1", "class": "5a", "subject": "Mathe"},
    ])
    status, solver, schedule = solve(data)
    assert status in (cp_model.OPTIMAL, cp_model.FEASIBLE), status_name(status)
    assert all(l["day"] == "Di" for l in schedule)
    assert verify_schedule(data, schedule) == []


def test_no_overlap_has_real_teeth_pigeonhole():
    """4 lesson-hours demanded, only 2 slots exist, no_overlap present
    -> must be INFEASIBLE. If the translation of no_overlap were broken
    (e.g. wrong entity key), this scenario would wrongly come back FEASIBLE."""
    ent = mini(["5a"], ["T1", "T2"], ["Mathe", "Deutsch"], [], ["Mo"], 2)
    data = scenario(ent, [
        {"type": "weekly_hours", "class": "5a", "subject": "Mathe", "hours_per_week": 2},
        {"type": "weekly_hours", "class": "5a", "subject": "Deutsch", "hours_per_week": 2},
        {"type": "no_overlap", "resource": "class", "entity": "5a"},
        {"type": "teacher_subject_assignment", "teacher": "T1", "class": "5a", "subject": "Mathe"},
        {"type": "teacher_subject_assignment", "teacher": "T2", "class": "5a", "subject": "Deutsch"},
    ])
    status, solver, schedule = solve(data)
    assert status == cp_model.INFEASIBLE, status_name(status)


def test_no_overlap_exact_fit_is_feasible_and_clean():
    """Same shape as above but demand == capacity exactly -> FEASIBLE, and
    the independent verifier confirms no double-booking occurred."""
    ent = mini(["5a"], ["T1", "T2"], ["Mathe", "Deutsch"], [], ["Mo"], 2)
    data = scenario(ent, [
        {"type": "weekly_hours", "class": "5a", "subject": "Mathe", "hours_per_week": 1},
        {"type": "weekly_hours", "class": "5a", "subject": "Deutsch", "hours_per_week": 1},
        {"type": "no_overlap", "resource": "class", "entity": "5a"},
        {"type": "teacher_subject_assignment", "teacher": "T1", "class": "5a", "subject": "Mathe"},
        {"type": "teacher_subject_assignment", "teacher": "T2", "class": "5a", "subject": "Deutsch"},
    ])
    status, solver, schedule = solve(data)
    assert status in (cp_model.OPTIMAL, cp_model.FEASIBLE), status_name(status)
    assert len(schedule) == 2
    slots = {(l["day"], l["period"]) for l in schedule}
    assert len(slots) == 2  # both lessons landed in different slots
    assert verify_schedule(data, schedule) == []


def test_room_requirement_pigeonhole():
    """Two different subjects both restricted to the one lab room, both
    needing 2h, only 2 slots total -> INFEASIBLE proves room-level
    no_overlap is actually wired to the room-choice variables."""
    ent = mini(["5a", "5b"], ["T1", "T2"], ["Chemie", "Physik"], ["Lab"], ["Mo"], 2)
    data = scenario(ent, [
        {"type": "weekly_hours", "class": "5a", "subject": "Chemie", "hours_per_week": 2},
        {"type": "weekly_hours", "class": "5b", "subject": "Physik", "hours_per_week": 2},
        {"type": "room_requirement", "subject": "Chemie", "allowed_rooms": ["Lab"]},
        {"type": "room_requirement", "subject": "Physik", "allowed_rooms": ["Lab"]},
        {"type": "no_overlap", "resource": "room", "entity": "Lab"},
        {"type": "teacher_subject_assignment", "teacher": "T1", "class": "5a", "subject": "Chemie"},
        {"type": "teacher_subject_assignment", "teacher": "T2", "class": "5b", "subject": "Physik"},
    ])
    status, solver, schedule = solve(data)
    assert status == cp_model.INFEASIBLE, status_name(status)


def test_forbidden_slot_is_avoided():
    ent = mini(["5a"], ["T1"], ["Mathe"], [], ["Mo"], 2)
    data = scenario(ent, [
        {"type": "weekly_hours", "class": "5a", "subject": "Mathe", "hours_per_week": 1},
        {"type": "forbidden_slot", "scope": "class", "entity": "5a", "day": "Mo", "period": 1},
        {"type": "teacher_subject_assignment", "teacher": "T1", "class": "5a", "subject": "Mathe"},
    ])
    status, solver, schedule = solve(data)
    assert status in (cp_model.OPTIMAL, cp_model.FEASIBLE), status_name(status)
    assert schedule[0]["period"] == 2
    assert verify_schedule(data, schedule) == []


def test_consecutive_required_forms_a_block():
    ent = mini(["5a"], ["T1"], ["Chemie"], [], ["Mo"], 3)
    data = scenario(ent, [
        {"type": "weekly_hours", "class": "5a", "subject": "Chemie", "hours_per_week": 2},
        {"type": "consecutive_required", "class": "5a", "subject": "Chemie", "block_length": 2},
        {"type": "teacher_subject_assignment", "teacher": "T1", "class": "5a", "subject": "Chemie"},
    ])
    status, solver, schedule = solve(data)
    assert status in (cp_model.OPTIMAL, cp_model.FEASIBLE), status_name(status)
    periods = sorted(l["period"] for l in schedule)
    assert periods in ([1, 2], [2, 3])  # contiguous pair, not e.g. [1, 3]
    assert verify_schedule(data, schedule) == []


def test_consecutive_required_rejects_non_multiple_of_block_length():
    """3 hours demanded with block_length=2: 3 is not a multiple of 2, so no
    arrangement of whole blocks can sum to exactly 3 -> INFEASIBLE."""
    ent = mini(["5a"], ["T1"], ["Chemie"], [], ["Mo", "Di"], 3)
    data = scenario(ent, [
        {"type": "weekly_hours", "class": "5a", "subject": "Chemie", "hours_per_week": 3},
        {"type": "consecutive_required", "class": "5a", "subject": "Chemie", "block_length": 2},
        {"type": "teacher_subject_assignment", "teacher": "T1", "class": "5a", "subject": "Chemie"},
    ])
    status, solver, schedule = solve(data)
    assert status == cp_model.INFEASIBLE, status_name(status)


def test_shared_resource_conflict_pigeonhole():
    ent = mini(["5a", "5b"], ["T1"], ["Sport"], [], ["Mo"], 1)
    data = scenario(ent, [
        {"type": "weekly_hours", "class": "5a", "subject": "Sport", "hours_per_week": 1},
        {"type": "weekly_hours", "class": "5b", "subject": "Sport", "hours_per_week": 1},
        {"type": "shared_resource_conflict", "classes": ["5a", "5b"], "subject": "Sport", "teacher": "T1"},
        {"type": "teacher_subject_assignment", "teacher": "T1", "class": "5a", "subject": "Sport"},
        {"type": "teacher_subject_assignment", "teacher": "T1", "class": "5b", "subject": "Sport"},
    ])
    status, solver, schedule = solve(data)
    # Only 1 slot exists and both classes need it with the same teacher.
    assert status == cp_model.INFEASIBLE, status_name(status)


# ---------------------------------------------------------------------------
# 2. Integration test against the (curated) real LLM extraction output
# ---------------------------------------------------------------------------

def test_full_scenario_from_llm_extraction_is_solvable_and_clean():
    status, solver, schedule = solve(FULL_SCENARIO, time_limit_s=20)
    assert status in (cp_model.OPTIMAL, cp_model.FEASIBLE), status_name(status)
    violations = verify_schedule(FULL_SCENARIO, schedule)
    assert violations == [], "\n".join(violations)


def test_full_scenario_becomes_infeasible_when_overconstrained():
    """Mutation test: block every single slot for Herr Meier while he still
    has 4h/week of Mathematik required -> must flip to INFEASIBLE."""
    import copy

    broken = copy.deepcopy(FULL_SCENARIO)
    days = broken["entities"]["timeslots"]["days"]
    periods = range(1, broken["entities"]["timeslots"]["periods_per_day"] + 1)
    for c in broken["constraints"]:
        if c["type"] == "teacher_availability" and c["teacher"] == "Herr Meier":
            c["available_days"] = []
            c["unavailable_periods"] = [{"day": d, "period": p} for d in days for p in periods]
    status, _, _ = solve(broken, time_limit_s=20)
    assert status == cp_model.INFEASIBLE, status_name(status)


def test_determinism_with_fixed_seed_and_single_worker():
    """CP-SAT is only guaranteed reproducible with num_search_workers=1 and
    a fixed random_seed. This asserts two independent solves return the
    exact same schedule under those settings."""
    status1, _, schedule1 = solve(FULL_SCENARIO, time_limit_s=20, seed=7, num_workers=1)
    status2, _, schedule2 = solve(FULL_SCENARIO, time_limit_s=20, seed=7, num_workers=1)
    assert status1 == status2

    def signature(schedule):
        return sorted((l["class"], l["subject"], l["teacher"], l["day"], l["period"]) for l in schedule)

    assert signature(schedule1) == signature(schedule2)
