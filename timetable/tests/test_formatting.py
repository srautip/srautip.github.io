"""Tests for the presentation layer on top of solve()'s raw schedule list."""
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))

from timetable_model import solve
from formatting import format_grid, format_schedule, to_class_grids, to_json_per_class, to_teacher_grids

from fixture_full_scenario import FULL_SCENARIO


def test_to_class_grids_matches_raw_schedule_exactly():
    status, solver, schedule = solve(FULL_SCENARIO, time_limit_s=20)
    grids = to_class_grids(FULL_SCENARIO, schedule)

    # Every scheduled lesson must show up at exactly its (class, day, period).
    for l in schedule:
        cell = grids[l["class"]][l["day"]][l["period"]]
        assert cell is not None
        assert cell["subject"] == l["subject"]
        assert cell["teacher"] == l["teacher"]
        assert cell["room"] == l["room"]

    # And the grid must not contain any lesson that isn't in the schedule.
    scheduled_slots = {(l["class"], l["day"], l["period"]) for l in schedule}
    for cls, by_day in grids.items():
        for day, by_period in by_day.items():
            for period, cell in by_period.items():
                if cell is not None:
                    assert (cls, day, period) in scheduled_slots


def test_to_teacher_grids_matches_raw_schedule_exactly():
    status, solver, schedule = solve(FULL_SCENARIO, time_limit_s=20)
    grids = to_teacher_grids(FULL_SCENARIO, schedule)

    for l in schedule:
        cell = grids[l["teacher"]][l["day"]][l["period"]]
        assert cell is not None
        assert cell["class"] == l["class"]
        assert cell["subject"] == l["subject"]


def test_grids_are_fully_populated_including_free_periods():
    """Every (day, period) slot must be a key in the grid, even if no
    lesson happens there (value None) - callers shouldn't need
    .get()/KeyError handling to render a full week."""
    status, solver, schedule = solve(FULL_SCENARIO, time_limit_s=20)
    grids = to_class_grids(FULL_SCENARIO, schedule)
    days = FULL_SCENARIO["entities"]["timeslots"]["days"]
    n_periods = FULL_SCENARIO["entities"]["timeslots"]["periods_per_day"]

    for cls in FULL_SCENARIO["entities"]["classes"]:
        assert set(grids[cls].keys()) == set(days)
        for day in days:
            assert set(grids[cls][day].keys()) == set(range(1, n_periods + 1))


def test_format_grid_renders_readable_ascii_table():
    grid_for_entity = {
        "Mo": {1: {"subject": "Mathe", "teacher": "T1", "room": None}, 2: None},
        "Di": {1: None, 2: {"subject": "Chemie", "teacher": "T2", "room": "R1"}},
    }
    text = format_grid("5a", grid_for_entity, ["Mo", "Di"], [1, 2], lambda c: "-" if c is None else c["subject"])
    lines = text.splitlines()
    assert lines[0] == "=== 5a ==="
    assert "Mo" in lines[1] and "Di" in lines[1]
    assert "Mathe" in text
    assert "Chemie" in text
    # 4 lines of content (title, header, separator, 2 period rows)
    assert len(lines) == 5


def test_format_schedule_contains_every_scheduled_lesson_once():
    status, solver, schedule = solve(FULL_SCENARIO, time_limit_s=20)
    text = format_schedule(FULL_SCENARIO, schedule)

    assert "KLASSEN" in text
    assert "LEHRKRAEFTE" in text
    for cls in FULL_SCENARIO["entities"]["classes"]:
        assert f"=== {cls} ===" in text
    for teacher in FULL_SCENARIO["entities"]["teachers"]:
        assert f"=== {teacher} ===" in text

    for l in schedule:
        assert l["subject"] in text
        assert l["teacher"] in text


def test_format_schedule_can_omit_teacher_tables():
    status, solver, schedule = solve(FULL_SCENARIO, time_limit_s=20)
    text = format_schedule(FULL_SCENARIO, schedule, include_teachers=False)
    assert "KLASSEN" in text
    assert "LEHRKRAEFTE" not in text


def test_to_json_per_class_round_trips_through_json_dumps():
    status, solver, schedule = solve(FULL_SCENARIO, time_limit_s=20)
    exported = to_json_per_class(FULL_SCENARIO, schedule)

    dumped = json.dumps(exported)  # must not raise
    reloaded = json.loads(dumped)

    for l in schedule:
        cell = reloaded[l["class"]][l["day"]][str(l["period"])]
        assert cell["subject"] == l["subject"]
        assert cell["teacher"] == l["teacher"]
