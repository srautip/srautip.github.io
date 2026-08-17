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
        "Extrahiere feste Sperrzeiten (Tag+Stunde). Wenn die Sperrzeit "
        "schulweit fuer alle Klassen gilt, erzeuge JE EIN Objekt PRO Klasse "
        "aus entities.classes."
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
}

ALL_TYPES = list(_ITEM_SCHEMAS)


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
    """Runs extract_constraint_type() for each type (default: all 8),
    sequentially. Returns (merged_constraints, meta_per_type)."""
    types = types or ALL_TYPES
    all_constraints: list[dict] = []
    meta_list: list[dict] = []
    for ctype in types:
        constraints, meta = extract_constraint_type(entities, prompt_text, ctype, **kwargs)
        all_constraints.extend(constraints)
        meta_list.append(meta)
    return all_constraints, meta_list
