# Timetable CP-SAT Proof of Concept

Prototype for a school-timetabling tool: a chat-based LLM (tested with a
locally hosted `qwen3.5:4b` via Ollama) turns natural-language scheduling
requirements into structured JSON constraints, which are then translated
into a Google OR-Tools CP-SAT model and solved.

This module covers the second half of that pipeline — JSON constraints in,
a solved (and independently verified) timetable out — plus the test suite
that exercises it. It does not include the LLM extraction step itself.

## Layout

```
timetable/
├── timetable_model.py        # JSON constraints -> CP-SAT model -> solve()
├── validation.py              # deterministic pre-solve checks, called from
│                               # build_model() (hard errors) plus advisory
│                               # coverage warnings (soft, non-blocking)
├── verifier.py                # independent solution checker (no shared code
│                               # with timetable_model.py, on purpose)
├── requirements.txt
└── tests/
    ├── fixture_full_scenario.py   # curated example scenario (see file header
    │                               # for the manual fixes applied on top of
    │                               # raw LLM output)
    └── test_timetable_model.py    # 16 tests, see its docstring for the
                                    # testing philosophy
```

## Constraint JSON format

```json
{
  "entities": {
    "classes": ["5a", "5b"],
    "teachers": ["Herr Meier"],
    "subjects": ["Mathematik"],
    "rooms": ["R101"],
    "timeslots": {"days": ["Mo","Di","Mi","Do","Fr"], "periods_per_day": 6}
  },
  "constraints": [
    {"type": "teacher_subject_assignment", "teacher": "Herr Meier", "class": "5a", "subject": "Mathematik"},
    {"type": "weekly_hours", "class": "5a", "subject": "Mathematik", "hours_per_week": 4, "max_per_day": 2},
    {"type": "teacher_availability", "teacher": "Herr Meier", "available_days": ["Di","Do"]},
    {"type": "no_overlap", "resource": "class", "entity": "5a"}
  ]
}
```

Supported constraint types: `teacher_availability`, `weekly_hours`,
`room_requirement`, `no_overlap`, `shared_resource_conflict`,
`forbidden_slot`, `consecutive_required`, `teacher_subject_assignment`.

Every `teacher_subject_assignment` entry defines a "session" (class +
subject + teacher) that gets a boolean decision variable per timeslot. All
other constraint types translate directly into CP-SAT constraints over
those variables — see the module docstring in `timetable_model.py` for the
exact mapping and known simplifications.

## Validation is built into build_model()

`build_model()` calls `validation.validate_entities()` first and raises
`ValueError` if any constraint references a class/teacher/subject/room
that isn't listed in `entities`. This is deterministic, plain-Python, and
needs no extra LLM call.

This exists because of a concrete bug found while building this: an
LLM-generated `consecutive_required` entry had `"class": "Chemie"` (a
subject name, not a class). Without validation, that constraint was
silently dropped — there was no session/variable to attach it to — and
the solver happily returned `OPTIMAL` with Chemistry missing entirely
from the schedule. No error, no warning, just a wrong schedule. With
validation wired in, the same input now fails fast:

```
ValueError: Ungueltige Constraint-Referenzen - build_model() bricht ab...
  - constraints[10] (type=consecutive_required): Feld 'class'='Chemie' ist
    keine bekannte Entity (erlaubt: ['5a', '5b'])
```

`validation.coverage_warnings()` is separate and does **not** raise: it
flags classes/teachers with no `no_overlap` entry as advisory warnings,
since that omission may be intentional (e.g. a teacher who only ever
teaches one class). Callers can log these without blocking a solve.

## Running the tests

```bash
pip install -r requirements.txt
python -m pytest tests/ -v
```

All 16 tests currently pass in well under a second. Test categories:

- One isolated unit test per constraint type, using minimal scenarios.
- "Pigeonhole" tests for `no_overlap`, `room_requirement`,
  `shared_resource_conflict`, `consecutive_required`: demand is set to
  exceed capacity by exactly one unit, expecting `INFEASIBLE` — proves the
  constraint has real teeth instead of silently becoming a no-op.
- An integration test against a realistic multi-constraint scenario,
  checked with the independent `verify_schedule()` (not just solver
  status).
- A mutation test on the full scenario (block every slot for one teacher)
  expecting the status to flip to `INFEASIBLE`.
- A determinism test: CP-SAT is only reproducible run-to-run with
  `num_search_workers=1` and a fixed `random_seed`.
- Validation tests: `build_model()` rejects unknown class/teacher/room
  references (reproducing the exact bug described above) and accepts a
  valid scenario without raising; `coverage_warnings()` flags a missing
  `no_overlap` entry without blocking the solve.

## Example usage

```python
from timetable_model import solve, status_name
from validation import coverage_warnings
from verifier import verify_schedule

for w in coverage_warnings(my_constraints_json):
    print("WARNUNG:", w)  # advisory only, does not raise

status, solver, schedule = solve(my_constraints_json, time_limit_s=30)
# solve() -> build_model() raises ValueError here if a constraint
# references an unknown class/teacher/subject/room - see above.
print(status_name(status))
if schedule is not None:
    violations = verify_schedule(my_constraints_json, schedule)
    assert not violations, violations
```
