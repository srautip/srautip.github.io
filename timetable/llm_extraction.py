"""LLM-based extraction of CP-SAT constraints from natural-language
scheduling requirements, using a locally hosted Ollama model.

This is the "first half" of the pipeline that timetable_model.py assumes
as input: turning free text into the structured constraint JSON. It is
deliberately decomposed into one narrow, schema-constrained call PER
CONSTRAINT TYPE. This is a design decision backed by direct experiment,
not a guess:

- A single big extraction call with only `format: "json"` produced a
  syntactically valid but incomplete result (some general rules only
  applied to the first-mentioned entity instead of all of them).
- A two-stage "extract, then ask the model to check completeness and
  repair" pipeline made things WORSE: the repair pass hallucinated new,
  unsupported constraints instead of fixing the real gaps it was asked to
  check for.
- Decomposing into one call per constraint type, each with its own tight
  JSON Schema (via Ollama's `format: <schema>`, not just `"json"`) and
  `think: false`, produced the most complete and least hallucinated
  results, verified with a deterministic (non-LLM) cross-reference
  validator (validation.py) afterwards.

Requires a running Ollama server (default http://127.0.0.1:11434) with the
model already pulled (default qwen3.5:4b). Call `is_ollama_available()`
before `extract_all_constraints()` to fail fast with a clear reason if
not.
"""
from __future__ import annotations

import json
import time
import urllib.error
import urllib.request

OLLAMA_URL = "http://127.0.0.1:11434"
MODEL = "qwen3.5:4b"


def is_ollama_available(model: str = MODEL, base_url: str = OLLAMA_URL) -> tuple[bool, str]:
    """Returns (available, reason) - reason explains why not, if not available."""
    try:
        with urllib.request.urlopen(f"{base_url}/", timeout=3):
            pass
    except Exception as e:
        return False, f"Ollama nicht erreichbar unter {base_url}: {e}"
    try:
        with urllib.request.urlopen(f"{base_url}/api/tags", timeout=5) as resp:
            tags = json.loads(resp.read())
    except Exception as e:
        return False, f"Ollama /api/tags fehlgeschlagen: {e}"
    names = {m.get("name") for m in tags.get("models", [])}
    if model not in names:
        return False, f"Modell '{model}' nicht gepullt (vorhanden: {sorted(names)})"
    return True, ""


def _obj(props: dict, required: list[str]) -> dict:
    return {"type": "object", "properties": props, "required": required}


_ITEM_SCHEMAS = {
    "teacher_availability": _obj({
        "type": {"const": "teacher_availability"},
        "teacher": {"type": "string"},
        "available_days": {"type": "array", "items": {"type": "string"}},
        "unavailable_periods": {"type": "array", "items": _obj(
            {"day": {"type": "string"}, "period": {"type": "integer"}}, ["day", "period"])},
        "reason": {"type": "string"},
    }, ["type", "teacher"]),

    "weekly_hours": _obj({
        "type": {"const": "weekly_hours"},
        "class": {"type": "string"},
        "subject": {"type": "string"},
        "hours_per_week": {"type": "integer"},
        "max_per_day": {"type": "integer"},
    }, ["type", "class", "subject", "hours_per_week"]),

    "room_requirement": _obj({
        "type": {"const": "room_requirement"},
        "subject": {"type": "string"},
        "allowed_rooms": {"type": "array", "items": {"type": "string"}},
        "reason": {"type": "string"},
    }, ["type", "subject", "allowed_rooms"]),

    "no_overlap": _obj({
        "type": {"const": "no_overlap"},
        "resource": {"type": "string", "enum": ["teacher", "class", "room"]},
        "entity": {"type": "string"},
        "reason": {"type": "string"},
    }, ["type", "resource", "entity"]),

    "shared_resource_conflict": _obj({
        "type": {"const": "shared_resource_conflict"},
        "classes": {"type": "array", "items": {"type": "string"}},
        "subject": {"type": "string"},
        "teacher": {"type": "string"},
        "reason": {"type": "string"},
    }, ["type", "classes", "subject", "teacher"]),

    "forbidden_slot": _obj({
        "type": {"const": "forbidden_slot"},
        "scope": {"type": "string", "enum": ["class", "teacher", "room"]},
        "entity": {"type": "string"},
        "day": {"type": "string"},
        "period": {"type": "integer"},
        "reason": {"type": "string"},
    }, ["type", "scope", "entity", "day", "period"]),

    "consecutive_required": _obj({
        "type": {"const": "consecutive_required"},
        "class": {"type": "string"},
        "subject": {"type": "string"},
        "block_length": {"type": "integer"},
        "reason": {"type": "string"},
    }, ["type", "class", "subject", "block_length"]),

    "teacher_subject_assignment": _obj({
        "type": {"const": "teacher_subject_assignment"},
        "teacher": {"type": "string"},
        "class": {"type": "string"},
        "subject": {"type": "string"},
    }, ["type", "teacher", "class", "subject"]),

    # NOT a CP-SAT-facing constraint type. This captures "period X only
    # happens on days Y" as a single fact (an easy extraction) instead of
    # asking the model to enumerate "every day EXCEPT Y" (a set-difference
    # computation the model got wrong two different ways in testing - once
    # incomplete, once with the polarity inverted). extract_all_constraints()
    # deterministically expands this into forbidden_slot entries in pure
    # Python - see _expand_period_exception() below.
    "period_exception": _obj({
        "type": {"const": "period_exception"},
        "period": {"type": "integer"},
        "allowed_days": {"type": "array", "items": {"type": "string"}},
        "reason": {"type": "string"},
    }, ["type", "period", "allowed_days"]),
}

_INSTRUCTIONS = {
    "teacher_availability": (
        "Extrahiere NUR Verfuegbarkeits-Einschraenkungen einzelner Lehrkraefte "
        "(Teilzeit, feste Sperrtage/-stunden). Ein Objekt pro betroffener "
        "Lehrkraft, keine Duplikate. Keine Lehrkraft ohne Einschraenkung im "
        "Text erwaehnen."
    ),
    "weekly_hours": (
        "Extrahiere fuer JEDE im Text genannte Klasse+Fach-Kombination die "
        "Wochenstunden (und, falls genannt, das Tagesmaximum). Erfinde keine "
        "Werte fuer nicht genannte Kombinationen."
    ),
    "room_requirement": (
        "Extrahiere Faecher, die laut Text nur in bestimmten Raeumen "
        "stattfinden duerfen."
    ),
    "no_overlap": (
        "Extrahiere die generelle Ueberschneidungsfreiheit-Regel. Falls der "
        "Text eine solche allgemeine Regel nennt, erzeuge JE EIN Objekt fuer "
        "JEDE Klasse aus entities.classes (resource=class), JEDEN Lehrer aus "
        "entities.teachers (resource=teacher) und JEDEN Fachraum aus "
        "entities.rooms, der als gemeinsam genutzter Raum vorkommt "
        "(resource=room). Liste alle vollstaendig auf, keine auslassen."
    ),
    "shared_resource_conflict": (
        "Extrahiere Faelle, in denen mehrere Klassen wegen derselben "
        "Lehrkraft nicht gleichzeitig denselben Unterricht haben duerfen."
    ),
    "forbidden_slot": (
        "Extrahiere feste Sperrzeiten (Tag+Stunde), die der Text DIREKT als "
        "gesperrt benennt (z.B. 'freitags 6. Stunde frei'). Wenn die "
        "Sperrzeit schulweit fuer alle Klassen gilt, erzeuge JE EIN Objekt "
        "PRO Klasse aus entities.classes. Ignoriere Ausnahme-Formulierungen "
        "der Form 'nur an Tag X erlaubt' / 'hoechstens an einem Tag' - "
        "dafuer gibt es einen anderen, spezialisierten Constraint-Typ."
    ),
    "consecutive_required": (
        "Extrahiere Faecher, die als zusammenhaengender Block (Doppelstunde "
        "o.ae.) unterrichtet werden muessen. Erzeuge JE EIN Objekt PRO "
        "betroffener Klasse."
    ),
    "teacher_subject_assignment": (
        "Extrahiere, welche Lehrkraft welches Fach in welcher Klasse "
        "unterrichtet. Ein Objekt PRO genannter Klasse, auch wenn eine "
        "Lehrkraft mehrere Klassen unterrichtet."
    ),
    "period_exception": (
        "Extrahiere Regeln der Form 'Stunde X findet hoechstens an einem "
        "Tag pro Woche statt, idealerweise Tag Y' bzw. 'Stunde X nur an "
        "bestimmten Tagen'. Erzeuge EIN Objekt mit der Stundennummer und "
        "der Liste der ERLAUBTEN Tage (NICHT der gesperrten!). Beispiel: "
        "'7. Stunde nur dienstags, idealerweise' -> "
        "{\"period\": 7, \"allowed_days\": [\"Di\"]}. Ignoriere normale "
        "Sperrzeiten, die direkt einen gesperrten Tag nennen (z.B. "
        "'freitags 6. Stunde frei') - dafuer gibt es einen anderen "
        "Constraint-Typ."
    ),
}

ALL_TYPES = list(_ITEM_SCHEMAS)


def _expand_period_exception(entities: dict, item: dict) -> list[dict]:
    """Deterministically turns {"period": X, "allowed_days": [...]} into one
    forbidden_slot entry per (blocked day, class) - pure set difference over
    entities.timeslots.days, no LLM involved. See the module docstring and
    the "period_exception" entries above for why this exists."""
    all_days = entities["timeslots"]["days"]
    allowed = set(item.get("allowed_days") or [])
    blocked_days = [d for d in all_days if d not in allowed]
    return [
        {
            "type": "forbidden_slot", "scope": "class", "entity": cls,
            "day": day, "period": item["period"],
            "reason": f"nur erlaubt an {sorted(allowed)}",
        }
        for day in blocked_days
        for cls in entities["classes"]
    ]


_EXPANDERS = {
    "period_exception": _expand_period_exception,
}


def extract_constraint_type(
    entities: dict, prompt_text: str, constraint_type: str,
    model: str = MODEL, base_url: str = OLLAMA_URL,
    temperature: float = 0.1, num_ctx: int = 8192,
    num_predict: int = 1800, timeout_s: int = 600,
) -> tuple[list[dict], dict]:
    """Calls the LLM for exactly one constraint type. Returns (constraints, meta)."""
    schema = _obj({"constraints": {"type": "array", "items": _ITEM_SCHEMAS[constraint_type]}},
                  ["constraints"])

    system_prompt = (
        "Du bist ein spezialisierter Extraktions-Assistent fuer GENAU EINEN "
        f"Constraint-Typ ('{constraint_type}') fuer einen Schul-Stundenplan-Solver "
        "(CP-SAT). Ignoriere alle anderen Einschraenkungsarten im Text "
        "vollstaendig.\n\n" + _INSTRUCTIONS[constraint_type]
    )
    user_content = (
        "ENTITIES (vollstaendige Liste, nur diese Namen verwenden):\n"
        + json.dumps(entities, ensure_ascii=False)
        + "\n\nANFORDERUNGEN:\n" + prompt_text
    )

    payload = {
        "model": model,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_content},
        ],
        "stream": False,
        "format": schema,
        "think": False,
        "options": {"temperature": temperature, "num_ctx": num_ctx, "num_predict": num_predict},
    }
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(f"{base_url}/api/chat", data=data,
                                  headers={"Content-Type": "application/json"})

    t0 = time.time()
    with urllib.request.urlopen(req, timeout=timeout_s) as resp:
        outer = json.loads(resp.read())
    dt = time.time() - t0

    content = outer["message"].get("content") or ""
    meta = {"type": constraint_type, "duration_s": dt, "done_reason": outer.get("done_reason")}
    try:
        parsed = json.loads(content)
        constraints = parsed.get("constraints", [])
        meta["valid_json"] = True
    except Exception as e:
        constraints = []
        meta["valid_json"] = False
        meta["parse_error"] = str(e)
        meta["raw"] = content[:1000]
    meta["n_items"] = len(constraints)
    return constraints, meta


def extract_all_constraints(
    entities: dict, prompt_text: str, types: list[str] | None = None, **kwargs,
) -> tuple[list[dict], list[dict]]:
    """Runs extract_constraint_type() for each type (default: all, including
    period_exception), sequentially. Types with an entry in _EXPANDERS are
    deterministically expanded into their real CP-SAT constraint(s) before
    being merged in - the caller always gets back a flat list of valid
    constraint types, never period_exception itself.
    Returns (merged_constraints, meta_per_type)."""
    types = types or ALL_TYPES
    all_constraints: list[dict] = []
    meta_list: list[dict] = []
    for ctype in types:
        raw_constraints, meta = extract_constraint_type(entities, prompt_text, ctype, **kwargs)
        expander = _EXPANDERS.get(ctype)
        if expander:
            expanded = [c for item in raw_constraints for c in expander(entities, item)]
            meta["expanded_to_n_items"] = len(expanded)
            all_constraints.extend(expanded)
        else:
            all_constraints.extend(raw_constraints)
        meta_list.append(meta)
    return all_constraints, meta_list
