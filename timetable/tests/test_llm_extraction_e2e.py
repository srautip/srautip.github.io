"""End-to-end test: a natural-language prompt describing the
Gymnasium-Klasse-5 scenario -> LLM-based extraction (llm_extraction.py,
decomposed per constraint type, running against a local Ollama server) ->
validate_entities() -> solve() -> verify_schedule(), plus a completeness
score against the hand-built ground truth
(fixture_gymnasium_klasse5.ASSIGNMENTS).

Unlike the rest of the suite, this test:
- needs a running Ollama server with qwen3.5:4b pulled (skipped otherwise)
- is NOT deterministic - LLM output varies run to run
- can take several minutes on CPU (8 sequential model calls covering a
  4-class, 9-subject, 15-teacher scenario)

It is skipped by default. Run explicitly with:
    RUN_LLM_TESTS=1 python -m pytest tests/test_llm_extraction_e2e.py -v -s
(-s so the extraction/completeness diagnostics print live.)
"""
import os
import pathlib
import sys

import pytest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))

from ortools.sat.python import cp_model

from llm_extraction import extract_all_constraints, is_ollama_available
from timetable_model import solve, status_name
from validation import validate_entities
from verifier import verify_schedule

from fixture_gymnasium_klasse5 import ASSIGNMENTS, GYMNASIUM_KLASSE5_SCENARIO

pytestmark = pytest.mark.skipif(
    os.environ.get("RUN_LLM_TESTS") != "1",
    reason=(
        "LLM e2e test skipped by default (needs a running Ollama server with "
        "qwen3.5:4b, takes several minutes, and is not deterministic). "
        "Set RUN_LLM_TESTS=1 to run it."
    ),
)

GYMNASIUM_PROMPT = """\
Wir sind ein vierzuegiges Gymnasium (Klassen 5a, 5b, 5c, 5d) in
Baden-Wuerttemberg. Der Stundenplan laeuft Montag bis Freitag mit je 7
Stunden pro Tag.

Faecher und Zuordnung:
- Deutsch (5 Stunden/Woche, hoechstens 2 pro Tag): Frau Vogel unterrichtet
  5a und 5b, Herr Baumann unterrichtet 5c und 5d.
- Mathematik (5 Stunden/Woche, hoechstens 2 pro Tag): Herr Krause
  unterrichtet 5a und 5c, Frau Nguyen unterrichtet 5b und 5d.
- Englisch (5 Stunden/Woche, hoechstens 2 pro Tag): Frau Fischer
  unterrichtet 5a und 5d, Herr Roth unterrichtet 5b und 5c.
- BNT, Biologie/Naturphaenomene und Technik (4 Stunden/Woche, hoechstens 2
  pro Tag, muss als zwei Doppelstunden stattfinden, immer im NaWi-Raum):
  Frau Kraemer unterrichtet 5a und 5b, Herr Werner unterrichtet 5c und 5d.
- Sport (3 Stunden/Woche, hoechstens 2 pro Tag, in Sporthalle1 oder
  Sporthalle2): Herr Braun unterrichtet 5a und 5b, Frau Lang unterrichtet
  5c und 5d.
- Musik (2 Stunden/Woche, hoechstens 2 pro Tag, im Musiksaal): Frau Adler
  unterrichtet alle vier Klassen 5a, 5b, 5c und 5d.
- Kunst (2 Stunden/Woche, hoechstens 2 pro Tag, im Kunstraum): Herr
  Schuster unterrichtet 5a und 5c, Frau Weiss unterrichtet 5b und 5d.
- Religion (2 Stunden/Woche, hoechstens 2 pro Tag): Pfarrer Huber
  unterrichtet alle vier Klassen.
- Erdkunde (2 Stunden/Woche, hoechstens 2 pro Tag): Herr Fink unterrichtet
  alle vier Klassen.

Verfuegbarkeit:
- Frau Nguyen arbeitet Teilzeit und ist nur montags, dienstags und
  mittwochs verfuegbar.
- Herr Werner hat freitags einen festen Fortbildungstag und ist dann
  nicht verfuegbar.

Sperrzeiten:
- Mittwochs ist die 7. Stunde fuer alle vier Klassen frei
  (Mittwochnachmittag).
- Freitags ist die 7. Stunde fuer alle vier Klassen frei (fruehere
  Schulschluss).

Zusaetzlich gilt fuer alle Klassen, Lehrkraefte und Fachraeume die
uebliche Ueberschneidungsfreiheit: niemand kann zwei Dinge gleichzeitig
haben, und kein Fachraum kann von zwei Gruppen gleichzeitig genutzt
werden.

Erzeuge daraus die passenden Constraints im vereinbarten JSON-Format fuer
den CP-SAT-Solver.
"""


def _expected_teacher_subject_assignments() -> set[tuple[str, str, str]]:
    return {
        (cls, subject, teacher)
        for subject, teacher, classes, *_rest in ASSIGNMENTS
        for cls in classes
    }


def _expected_weekly_hours() -> dict[tuple[str, str], tuple[int, int]]:
    return {
        (cls, subject): (hours, max_per_day)
        for subject, _teacher, classes, hours, max_per_day, *_rest in ASSIGNMENTS
        for cls in classes
    }


def _completeness_report(extracted: list[dict]) -> dict[str, float]:
    ent = GYMNASIUM_KLASSE5_SCENARIO["entities"]

    expected_tsa = _expected_teacher_subject_assignments()
    actual_tsa = {
        (c["class"], c["subject"], c["teacher"])
        for c in extracted if c.get("type") == "teacher_subject_assignment"
    }
    tsa_recall = len(expected_tsa & actual_tsa) / len(expected_tsa)

    expected_wh = _expected_weekly_hours()
    actual_wh = {
        (c.get("class"), c.get("subject")): (c.get("hours_per_week"), c.get("max_per_day"))
        for c in extracted if c.get("type") == "weekly_hours"
    }
    wh_recall = sum(1 for k, v in expected_wh.items() if actual_wh.get(k) == v) / len(expected_wh)

    expected_no_overlap = (
        {("class", c) for c in ent["classes"]}
        | {("teacher", t) for t in ent["teachers"]}
        | {("room", r) for r in ent["rooms"]}
    )
    actual_no_overlap = {
        (c.get("resource"), c.get("entity"))
        for c in extracted if c.get("type") == "no_overlap"
    }
    no_overlap_recall = len(expected_no_overlap & actual_no_overlap) / len(expected_no_overlap)

    expected_rooms = {
        subject: tuple(sorted(rooms))
        for subject, _t, _c, _h, _m, _b, rooms in ASSIGNMENTS if rooms
    }
    actual_rooms = {
        c.get("subject"): tuple(sorted(c.get("allowed_rooms") or []))
        for c in extracted if c.get("type") == "room_requirement"
    }
    room_recall = sum(1 for k, v in expected_rooms.items() if actual_rooms.get(k) == v) / len(expected_rooms)

    expected_consecutive = {
        (cls, subject, block_len)
        for subject, _t, classes, _h, _m, block_len, _r in ASSIGNMENTS
        if block_len
        for cls in classes
    }
    actual_consecutive = {
        (c.get("class"), c.get("subject"), c.get("block_length"))
        for c in extracted if c.get("type") == "consecutive_required"
    }
    consecutive_recall = len(expected_consecutive & actual_consecutive) / len(expected_consecutive)

    expected_availability_teachers = {"Frau Nguyen", "Herr Werner"}
    actual_availability_teachers = {
        c.get("teacher") for c in extracted if c.get("type") == "teacher_availability"
    }
    availability_recall = (
        len(expected_availability_teachers & actual_availability_teachers)
        / len(expected_availability_teachers)
    )

    periods = ent["timeslots"]["periods_per_day"]
    expected_forbidden = {
        (cls, day, periods) for cls in ent["classes"] for day in ("Mi", "Fr")
    }
    actual_forbidden = {
        (c.get("entity"), c.get("day"), c.get("period"))
        for c in extracted
        if c.get("type") == "forbidden_slot" and c.get("scope") == "class"
    }
    forbidden_recall = len(expected_forbidden & actual_forbidden) / len(expected_forbidden)

    scores = {
        "teacher_subject_assignment": tsa_recall,
        "weekly_hours": wh_recall,
        "no_overlap": no_overlap_recall,
        "room_requirement": room_recall,
        "consecutive_required": consecutive_recall,
        "teacher_availability": availability_recall,
        "forbidden_slot": forbidden_recall,
    }
    scores["overall"] = sum(scores.values()) / len(scores)
    return scores


def test_llm_extraction_e2e_gymnasium_klasse5():
    available, reason = is_ollama_available()
    if not available:
        pytest.skip(reason)

    entities = GYMNASIUM_KLASSE5_SCENARIO["entities"]
    extracted, meta = extract_all_constraints(entities, GYMNASIUM_PROMPT)

    print("\n=== Extraction meta ===")
    for m in meta:
        print(f"  {m['type']:<28} duration={m['duration_s']:6.1f}s "
              f"valid_json={m['valid_json']} n_items={m['n_items']}")

    for m in meta:
        assert m["valid_json"], f"{m['type']}: kein valides JSON - {m.get('parse_error')}"

    scenario = {"entities": entities, "constraints": extracted}

    errors = validate_entities(scenario)
    assert errors == [], "Ungueltige Entity-Referenzen im LLM-Output:\n" + "\n".join(errors)

    scores = _completeness_report(extracted)
    print("\n=== Vollstaendigkeit vs. Ground Truth (ASSIGNMENTS) ===")
    for k, v in scores.items():
        print(f"  {k:<28} {v:.0%}")

    assert scores["overall"] >= 0.5, f"Vollstaendigkeit nur {scores['overall']:.0%} - Details: {scores}"

    status, solver, schedule = solve(scenario, time_limit_s=60)
    print(f"\nsolve() status: {status_name(status)}")
    assert status in (cp_model.OPTIMAL, cp_model.FEASIBLE), status_name(status)

    violations = verify_schedule(scenario, schedule)
    assert violations == [], "\n".join(violations)
