"""Turn the flat, unordered `schedule` list returned by `solve()` into
something readable: per-class/per-teacher grids, ASCII tables, and a
JSON-serializable per-class export.

`solve()` returns `schedule` as a flat list of lesson dicts in no
particular order - this module is the missing "presentation layer" on
top of that raw data.
"""
from __future__ import annotations


def _empty_grid(entity_names, days, periods):
    return {e: {d: {p: None for p in periods} for d in days} for e in entity_names}


def to_class_grids(data: dict, schedule: list[dict]) -> dict:
    """Returns {class: {day: {period: {"subject","teacher","room"} | None}}}."""
    ent = data["entities"]
    days = ent["timeslots"]["days"]
    periods = list(range(1, ent["timeslots"]["periods_per_day"] + 1))
    grids = _empty_grid(ent["classes"], days, periods)
    for l in schedule:
        grids[l["class"]][l["day"]][l["period"]] = {
            "subject": l["subject"],
            "teacher": l["teacher"],
            "room": l["room"],
        }
    return grids


def to_teacher_grids(data: dict, schedule: list[dict]) -> dict:
    """Returns {teacher: {day: {period: {"class","subject","room"} | None}}}."""
    ent = data["entities"]
    days = ent["timeslots"]["days"]
    periods = list(range(1, ent["timeslots"]["periods_per_day"] + 1))
    grids = _empty_grid(ent["teachers"], days, periods)
    for l in schedule:
        grids[l["teacher"]][l["day"]][l["period"]] = {
            "class": l["class"],
            "subject": l["subject"],
            "room": l["room"],
        }
    return grids


def to_json_per_class(data: dict, schedule: list[dict]) -> dict:
    """Same content as to_class_grids(), ready for json.dumps() (period
    keys are converted to str since JSON object keys must be strings)."""
    grids = to_class_grids(data, schedule)
    return {
        cls: {day: {str(p): cell for p, cell in by_period.items()} for day, by_period in by_day.items()}
        for cls, by_day in grids.items()
    }


def _class_cell_text(cell: dict | None) -> str:
    if cell is None:
        return "-"
    text = f"{cell['subject']} ({cell['teacher']})"
    if cell.get("room"):
        text += f" [{cell['room']}]"
    return text


def _teacher_cell_text(cell: dict | None) -> str:
    if cell is None:
        return "-"
    text = f"{cell['class']} {cell['subject']}"
    if cell.get("room"):
        text += f" [{cell['room']}]"
    return text


def format_grid(entity_name: str, grid_for_entity: dict, days: list[str], periods: list[int], cell_text_fn) -> str:
    """Render one entity's {day: {period: cell}} grid as an ASCII table
    (rows = periods, columns = days)."""
    cell_text = {(d, p): cell_text_fn(grid_for_entity[d][p]) for d in days for p in periods}
    col_width = {d: max(len(d), max(len(cell_text[(d, p)]) for p in periods)) for d in days}
    period_col_width = max(len("Std."), max(len(str(p)) for p in periods))

    header = "Std.".ljust(period_col_width) + " | " + " | ".join(d.ljust(col_width[d]) for d in days)
    lines = [f"=== {entity_name} ===", header, "-" * len(header)]
    for p in periods:
        row = str(p).ljust(period_col_width) + " | " + " | ".join(
            cell_text[(d, p)].ljust(col_width[d]) for d in days
        )
        lines.append(row)
    return "\n".join(lines)


def format_schedule(data: dict, schedule: list[dict], include_teachers: bool = True) -> str:
    """Render the full solved schedule as ASCII tables: one per class, and
    (by default) one per teacher."""
    ent = data["entities"]
    days = ent["timeslots"]["days"]
    periods = list(range(1, ent["timeslots"]["periods_per_day"] + 1))

    parts = ["KLASSEN", ""]
    class_grids = to_class_grids(data, schedule)
    for cls in ent["classes"]:
        parts.append(format_grid(cls, class_grids[cls], days, periods, _class_cell_text))
        parts.append("")

    if include_teachers:
        parts.append("LEHRKRAEFTE")
        parts.append("")
        teacher_grids = to_teacher_grids(data, schedule)
        for teacher in ent["teachers"]:
            parts.append(format_grid(teacher, teacher_grids[teacher], days, periods, _teacher_cell_text))
            parts.append("")

    return "\n".join(parts)
