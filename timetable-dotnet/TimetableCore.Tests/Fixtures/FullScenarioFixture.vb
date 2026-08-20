' Ported 1:1 from tests/fixture_full_scenario.py. Curated version of the
' LLM-generated `decomposed_result.json` from the chat session, used for
' the integration tests below.
'
' Manual corrections applied on top of the raw LLM output (documented
' here, not silently):
'
' 1. The `consecutive_required` entry had `"class": "Chemie"` (subject
'    name used as class name - a real bug the deterministic validator
'    caught). Fixed to two entries, one per class (5a, 5b).
' 2. The original natural-language prompt never named a Chemistry teacher
'    or said which classes take Chemistry - so the LLM correctly did not
'    invent a `teacher_subject_assignment` for it. For this integration
'    test we add one explicitly (Frau Wagner, Chemie, 5a+5b) plus matching
'    `weekly_hours` (2h/week - a multiple of the block_length so the
'    schedule is solvable), otherwise there is nothing to schedule for the
'    room/block constraints to act on.
Imports System.Text.Json.Nodes

Public Module FullScenarioFixture

    Public Function BuildFullScenario() As JsonObject
        Return New JsonObject From {
            {"entities", New JsonObject From {
                {"classes", New JsonArray From {"5a", "5b"}},
                {"teachers", New JsonArray From {"Herr Meier", "Frau Schmidt", "Frau Wagner"}},
                {"subjects", New JsonArray From {"Mathematik", "Chemie", "Sport"}},
                {"rooms", New JsonArray From {"R101"}},
                {"timeslots", New JsonObject From {
                    {"days", New JsonArray From {"Mo", "Di", "Mi", "Do", "Fr"}},
                    {"periods_per_day", 6}
                }}
            }},
            {"constraints", New JsonArray From {
                New JsonObject From {
                    {"type", "teacher_availability"},
                    {"teacher", "Herr Meier"},
                    {"available_days", New JsonArray From {"Di", "Do"}},
                    {"unavailable_periods", New JsonArray From {
                        New JsonObject From {{"day", "Mo"}, {"period", 1}}
                    }}
                },
                New JsonObject From {
                    {"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Mathematik"},
                    {"hours_per_week", 4}, {"max_per_day", 2}
                },
                New JsonObject From {
                    {"type", "weekly_hours"}, {"class", "5a"}, {"subject", "Chemie"},
                    {"hours_per_week", 2}
                },
                New JsonObject From {
                    {"type", "weekly_hours"}, {"class", "5b"}, {"subject", "Chemie"},
                    {"hours_per_week", 2}
                },
                New JsonObject From {
                    {"type", "room_requirement"}, {"subject", "Chemie"},
                    {"allowed_rooms", New JsonArray From {"R101"}}
                },
                New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "5a"}},
                New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", "5b"}},
                New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", "Herr Meier"}},
                New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", "Frau Schmidt"}},
                New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", "Frau Wagner"}},
                New JsonObject From {
                    {"type", "shared_resource_conflict"},
                    {"classes", New JsonArray From {"5a", "5b"}},
                    {"subject", "Sport"}, {"teacher", "Frau Schmidt"}
                },
                New JsonObject From {
                    {"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5a"},
                    {"day", "Fr"}, {"period", 6}
                },
                New JsonObject From {
                    {"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "5b"},
                    {"day", "Fr"}, {"period", 6}
                },
                New JsonObject From {
                    {"type", "consecutive_required"}, {"class", "5a"}, {"subject", "Chemie"}, {"block_length", 2}
                },
                New JsonObject From {
                    {"type", "consecutive_required"}, {"class", "5b"}, {"subject", "Chemie"}, {"block_length", 2}
                },
                New JsonObject From {
                    {"type", "teacher_subject_assignment"}, {"teacher", "Herr Meier"}, {"class", "5a"}, {"subject", "Mathematik"}
                },
                New JsonObject From {
                    {"type", "teacher_subject_assignment"}, {"teacher", "Herr Meier"}, {"class", "5b"}, {"subject", "Mathematik"}
                },
                New JsonObject From {
                    {"type", "teacher_subject_assignment"}, {"teacher", "Frau Schmidt"}, {"class", "5a"}, {"subject", "Sport"}
                },
                New JsonObject From {
                    {"type", "teacher_subject_assignment"}, {"teacher", "Frau Schmidt"}, {"class", "5b"}, {"subject", "Sport"}
                },
                New JsonObject From {
                    {"type", "teacher_subject_assignment"}, {"teacher", "Frau Wagner"}, {"class", "5a"}, {"subject", "Chemie"}
                },
                New JsonObject From {
                    {"type", "teacher_subject_assignment"}, {"teacher", "Frau Wagner"}, {"class", "5b"}, {"subject", "Chemie"}
                }
            }}
        }
    End Function

End Module
