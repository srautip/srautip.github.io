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
├── formatting.py               # solve()'s flat, unordered schedule list ->
│                               # per-class/per-teacher grids, ASCII tables,
│                               # JSON-per-class export
├── verifier.py                # independent solution checker (no shared code
│                               # with timetable_model.py, on purpose)
├── requirements.txt
└── tests/
    ├── fixture_full_scenario.py       # curated example scenario (see file
    │                                   # header for the manual fixes applied
    │                                   # on top of raw LLM output)
    ├── fixture_gymnasium_klasse5.py    # larger, more realistic scenario: a
    │                                   # 4-zuegiges Gymnasium, Klasse 5 (see
    │                                   # file header for scope/caveats)
    ├── test_timetable_model.py        # 16 tests, see its docstring for the
    │                                   # testing philosophy
    ├── test_formatting.py             # 7 tests for the presentation layer
    └── test_gymnasium_klasse5.py      # 3 integration tests against the
                                        # larger scenario
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

## Larger test scenario: 4-zuegiges Gymnasium, Klasse 5

`tests/fixture_gymnasium_klasse5.py` is a bigger, more realistic scenario
than the 2-class example above: 4 parallel classes (5a-5d), 9 subjects, 15
teachers, 5 shared specialist rooms (2 Sporthallen, Musiksaal, Kunstraum,
NaWi-Raum), 30 weekly hours per class, loosely modeled on a typical
Baden-Wuerttemberg Gymnasium Kontingentstundentafel (comparable in scale
to a school like GSG Fellbach) — **not** a verified extract of any real
school's actual curriculum, staff, or rooms.

It also documents two concrete schema limitations hit while building it
(see the file's docstring for details):

- **No cross-class groups.** Real confessional Religion/Ethik teaching
  (katholisch/evangelisch/Ethik groups mixed across all 4 parallel
  classes, taught in a shared "Bandzeit") can't be expressed — the
  current schema only knows per-class subjects. Simplified here to one
  teacher per class.
- **No mixed block/single periods.** `consecutive_required` forces *all*
  weekly hours of a class+subject into equal-length blocks. Real BNT is
  often 3h/week (one double + one single period), which isn't
  representable — the fixture uses 4h/week (two double periods) instead
  to stay within what the model can express.

Despite the added scale (114 constraints, 4x the sessions of the small
fixture), it solves to `OPTIMAL` in well under a second and passes the
independent verifier with zero violations - see
`tests/test_gymnasium_klasse5.py`.

## Output: rendering the solved schedule

`solve()` on its own returns `schedule` as a **flat, unordered list** of
lesson dicts (`{"class","subject","teacher","day","period","room"}`) — no
grouping, no sorting, no table. `formatting.py` is the presentation layer
on top of that:

- `to_class_grids(data, schedule)` / `to_teacher_grids(...)` — nested
  dicts `{entity: {day: {period: cell_or_None}}}`, fully populated (every
  day/period is a key, `None` for a free period) so a UI can render a
  complete week without `.get()`/`KeyError` handling.
- `format_grid(...)` / `format_schedule(data, schedule)` — renders those
  grids as aligned ASCII tables, one per class and (optionally) one per
  teacher.
- `to_json_per_class(data, schedule)` — the class grids with period keys
  converted to strings, ready for `json.dumps()` (e.g. for a frontend or
  file export).

```
$ python -c "
from timetable_model import solve
from formatting import format_schedule
from tests.fixture_full_scenario import FULL_SCENARIO
status, solver, schedule = solve(FULL_SCENARIO)
print(format_schedule(FULL_SCENARIO, schedule))
"

KLASSEN

=== 5a ===
Std. | Mo | Di                      | Mi | Do                      | Fr
------------------------------------------------------------------------
1    | -  | -                       | -  | -                       | -
2    | -  | Mathematik (Herr Meier) | -  | -                       | Chemie (Frau Wagner) [R101]
3    | -  | Mathematik (Herr Meier) | -  | Mathematik (Herr Meier) | Chemie (Frau Wagner) [R101]
...

LEHRKRAEFTE

=== Herr Meier ===
...
```

## Running the tests

```bash
pip install -r requirements.txt
python -m pytest tests/ -v
```

All 26 tests currently pass in well under a second. Test categories:

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
- Formatting tests: grids exactly match the raw schedule (every scheduled
  lesson shows up, nothing extra), every slot is populated (including free
  periods as `None`), the ASCII tables contain every scheduled lesson, and
  the JSON export survives a `json.dumps`/`json.loads` round trip.
- Gymnasium-Klasse-5 integration tests: the larger 4-class scenario
  validates cleanly, solves to `OPTIMAL`/`FEASIBLE`, passes the
  independent verifier, and renders without error.

## Example usage

```python
from timetable_model import solve, status_name
from validation import coverage_warnings
from verifier import verify_schedule
from formatting import format_schedule, to_json_per_class

for w in coverage_warnings(my_constraints_json):
    print("WARNUNG:", w)  # advisory only, does not raise

status, solver, schedule = solve(my_constraints_json, time_limit_s=30)
# solve() -> build_model() raises ValueError here if a constraint
# references an unknown class/teacher/subject/room - see above.
print(status_name(status))
if schedule is not None:
    violations = verify_schedule(my_constraints_json, schedule)
    assert not violations, violations

    print(format_schedule(my_constraints_json, schedule))       # for a terminal/log
    export = to_json_per_class(my_constraints_json, schedule)   # for a frontend/file
```
