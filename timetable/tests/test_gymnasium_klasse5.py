"""Integration test for the larger, more realistic Gymnasium-Klasse-5
scenario (4 Klassen, 9 Faecher, 15 Lehrkraefte, 5 Fachraeume - see
fixture_gymnasium_klasse5.py for details and caveats).

This exercises the full pipeline (validate -> solve -> verify -> format)
under a load an order of magnitude larger than fixture_full_scenario.py,
including room contention shared across two different teachers (Kunst,
BNT), a shared single-room pool across four classes (Musik), and two
independent teacher-availability restrictions.
"""
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))

from ortools.sat.python import cp_model

from timetable_model import solve, status_name
from validation import coverage_warnings, validate_entities
from verifier import verify_schedule
from formatting import format_schedule

from fixture_gymnasium_klasse5 import GYMNASIUM_KLASSE5_SCENARIO


def test_gymnasium_klasse5_entities_are_valid():
    assert validate_entities(GYMNASIUM_KLASSE5_SCENARIO) == []
    assert coverage_warnings(GYMNASIUM_KLASSE5_SCENARIO) == []


def test_gymnasium_klasse5_solves_and_verifies_clean():
    status, solver, schedule = solve(GYMNASIUM_KLASSE5_SCENARIO, time_limit_s=60)
    assert status in (cp_model.OPTIMAL, cp_model.FEASIBLE), status_name(status)

    ent = GYMNASIUM_KLASSE5_SCENARIO["entities"]
    expected_hours = sum(
        c["hours_per_week"]
        for c in GYMNASIUM_KLASSE5_SCENARIO["constraints"]
        if c["type"] == "weekly_hours"
    )
    assert len(schedule) == expected_hours

    violations = verify_schedule(GYMNASIUM_KLASSE5_SCENARIO, schedule)
    assert violations == [], "\n".join(violations)


def test_gymnasium_klasse5_renders_without_error():
    status, solver, schedule = solve(GYMNASIUM_KLASSE5_SCENARIO, time_limit_s=60)
    text = format_schedule(GYMNASIUM_KLASSE5_SCENARIO, schedule)
    for cls in GYMNASIUM_KLASSE5_SCENARIO["entities"]["classes"]:
        assert f"=== {cls} ===" in text
