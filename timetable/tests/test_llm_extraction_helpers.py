"""Fast, deterministic tests for the pure-Python parts of llm_extraction.py
(no Ollama needed - these don't call the LLM at all).
"""
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))

from llm_extraction import _expand_period_exception


def test_expand_period_exception_blocks_every_day_except_allowed():
    entities = {
        "classes": ["5a", "5b"],
        "timeslots": {"days": ["Mo", "Di", "Mi", "Do", "Fr"], "periods_per_day": 7},
    }
    item = {"type": "period_exception", "period": 7, "allowed_days": ["Di"]}

    result = _expand_period_exception(entities, item)

    assert len(result) == 4 * 2  # 4 blocked days x 2 classes
    days_seen = {c["day"] for c in result}
    assert days_seen == {"Mo", "Mi", "Do", "Fr"}
    assert all(c["period"] == 7 for c in result)
    assert all(c["type"] == "forbidden_slot" and c["scope"] == "class" for c in result)
    classes_seen = {c["entity"] for c in result}
    assert classes_seen == {"5a", "5b"}


def test_expand_period_exception_multiple_allowed_days():
    entities = {
        "classes": ["5a"],
        "timeslots": {"days": ["Mo", "Di", "Mi", "Do", "Fr"], "periods_per_day": 7},
    }
    item = {"type": "period_exception", "period": 7, "allowed_days": ["Mo", "Di"]}

    result = _expand_period_exception(entities, item)

    days_seen = {c["day"] for c in result}
    assert days_seen == {"Mi", "Do", "Fr"}


def test_expand_period_exception_no_allowed_days_blocks_everything():
    entities = {
        "classes": ["5a"],
        "timeslots": {"days": ["Mo", "Di"], "periods_per_day": 7},
    }
    item = {"type": "period_exception", "period": 7, "allowed_days": []}

    result = _expand_period_exception(entities, item)

    days_seen = {c["day"] for c in result}
    assert days_seen == {"Mo", "Di"}
