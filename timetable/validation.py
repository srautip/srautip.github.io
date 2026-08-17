"""Deterministic, LLM-free validation of the constraint JSON, run *before*
it reaches the CP-SAT model builder.

Two categories, kept deliberately separate:

- ``validate_entities`` returns HARD errors: a constraint references a
  class/teacher/subject/room that does not exist in ``entities``. Such a
  constraint is not just wrong - the model builder silently drops it
  (there is no session/variable to attach it to), which can make an
  incomplete schedule solve as OPTIMAL. Confirmed while building this
  module: an LLM-generated ``consecutive_required`` entry had
  ``"class": "Chemie"`` (a subject name, not a class) and the solver
  happily produced a schedule with Chemistry missing entirely. These
  errors must block solving, so ``build_model`` raises on them.

- ``coverage_warnings`` returns SOFT warnings: a general rule
  (``no_overlap``, a schoolwide ``forbidden_slot``) does not cover every
  class/teacher. This may be intentional (not every teacher needs a
  standalone no_overlap rule if they only teach one class), so it does
  NOT block solving - callers can inspect/log it themselves.
"""
from __future__ import annotations

_FIELD_ENTITY_KEY = {
    "class": "classes",
    "classes": "classes",  # list-valued variant (shared_resource_conflict)
    "teacher": "teachers",
    "subject": "subjects",
    "room": "rooms",
    "allowed_rooms": "rooms",  # list-valued variant (room_requirement)
}

_RESOURCE_ENTITY_KEY = {"teacher": "teachers", "class": "classes", "room": "rooms"}


def validate_entities(data: dict) -> list[str]:
    """Cross-reference every class/teacher/subject/room value used in
    `constraints` against `entities`. Returns a list of error strings
    (empty = all references are valid)."""
    ent = data["entities"]
    known = {k: set(ent[k]) for k in ("classes", "teachers", "subjects", "rooms")}

    errors: list[str] = []
    for i, c in enumerate(data["constraints"]):
        for field, entity_key in _FIELD_ENTITY_KEY.items():
            if field not in c:
                continue
            val = c[field]
            values = val if isinstance(val, list) else [val]
            for v in values:
                if v not in known[entity_key]:
                    errors.append(
                        f"constraints[{i}] (type={c.get('type')}): Feld '{field}'='{v}' "
                        f"ist keine bekannte Entity (erlaubt: {sorted(known[entity_key])})"
                    )

        if c.get("type") == "no_overlap":
            entity_key = _RESOURCE_ENTITY_KEY.get(c.get("resource"))
            if entity_key is None:
                errors.append(
                    f"constraints[{i}]: no_overlap.resource={c.get('resource')!r} ungueltig "
                    "(erlaubt: teacher/class/room)"
                )
            elif c.get("entity") not in known[entity_key]:
                errors.append(
                    f"constraints[{i}]: no_overlap.entity='{c.get('entity')}' nicht in {entity_key}"
                )

        if c.get("type") == "forbidden_slot":
            entity_key = _RESOURCE_ENTITY_KEY.get(c.get("scope"))
            if entity_key is None:
                errors.append(
                    f"constraints[{i}]: forbidden_slot.scope={c.get('scope')!r} ungueltig "
                    "(erlaubt: teacher/class/room)"
                )
            elif c.get("entity") not in known[entity_key]:
                errors.append(
                    f"constraints[{i}]: forbidden_slot.entity='{c.get('entity')}' nicht in {entity_key}"
                )

    return errors


def coverage_warnings(data: dict) -> list[str]:
    """Advisory (non-blocking) checks: does a general no_overlap rule cover
    every class/teacher? Returns a list of warning strings."""
    ent = data["entities"]
    classes = set(ent["classes"])
    teachers = set(ent["teachers"])

    no_overlap_classes = {
        c["entity"] for c in data["constraints"]
        if c.get("type") == "no_overlap" and c.get("resource") == "class"
    }
    no_overlap_teachers = {
        c["entity"] for c in data["constraints"]
        if c.get("type") == "no_overlap" and c.get("resource") == "teacher"
    }

    warnings: list[str] = []
    missing_classes = classes - no_overlap_classes
    missing_teachers = teachers - no_overlap_teachers
    if missing_classes:
        warnings.append(
            f"no_overlap fehlt fuer Klassen {sorted(missing_classes)} (evtl. gewollt, bitte pruefen)"
        )
    if missing_teachers:
        warnings.append(
            f"no_overlap fehlt fuer Lehrer {sorted(missing_teachers)} (evtl. gewollt, bitte pruefen)"
        )
    return warnings
