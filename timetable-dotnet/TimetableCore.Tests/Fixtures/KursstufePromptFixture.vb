' Phase 2.11h: a SMALL Kursstufe scenario with a natural-language Prompt
' and hand-built ground truth, for LlmExtraction.vb's "kurswahl" type
' (Phase 2.11g) - the LLM-extraction-focused sibling of KursstufeFixture.vb
' (which is deliberately a large, prompt-free Solve-benchmark instead).
' Sized like MussKannFixture/OberstufeFixture, NOT KursstufeFixture's
' realistic ~18-Schienen/32-Kurs scale - this project's established
' lesson (see docs/phase2-robustness-report.md) is that bigger entity
' lists make LLM extraction harder, so an extraction-focused fixture
' should stay small on purpose.
'
' IMPORTANT: SYNTHETIC, plausible test data (same BW-Kursstufen-Struktur
' as KursstufeFixture.vb) - not a real school's actual Kursangebot.
'
' Deliberately over-provisioned on Schienen relative to the strict
' minimum (10 total for only 14 Kurse) so every Kurs maps close to 1:1
' onto its own Schiene and Kursblockung never has to make a genuine
' packing choice - this fixture's purpose is exercising LLM extraction,
' not stress-testing Kursblockung's combinatorics (KursstufeFixture.vb
' already does that). No "capacity" field is set here for the same
' reason - not needed at this scale (see Kursblockung.vb's capacity
' feature, added because the LARGE fixture needed it).
Imports System.Text.Json.Nodes

Public Module KursstufePromptFixture

    Private ReadOnly Days As New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}
    Private Const PeriodsPerDay As Integer = 8

    Private Function Kurs(id As String, subject As String, teacher As String, kursart As String, hours As Integer) As JsonObject
        Return New JsonObject From {
            {"id", id}, {"subject", subject}, {"teacher", teacher}, {"kursart", kursart}, {"hours_per_week", hours}
        }
    End Function

    Private Function Schiene(id As String, kursart As String, hours As Integer) As JsonObject
        Return New JsonObject From {{"id", id}, {"kursart", kursart}, {"hours_per_week", hours}}
    End Function

    ''' <summary>Functions, not cached lists - a JsonNode can only have one
    ''' parent at a time (see Models.vb's header comment), so
    ''' BuildKursstufePromptScenario needs a FRESH set of JsonObject
    ''' instances on every call rather than reusing shared ones (a shared
    ''' instance would throw "node already has a parent" the second time
    ''' this fixture is built in the same test run).</summary>
    Private Function BuildKurse() As List(Of JsonObject)
        Return New List(Of JsonObject) From {
            Kurs("D-LK1", "Deutsch", "Frau Berger", "LK", 5),
            Kurs("MA-LK1", "Mathematik", "Herr Klein", "LK", 5),
            Kurs("BIO-LK1", "Biologie", "Frau Wolf", "LK", 5),
            Kurs("EN-LK1", "Englisch", "Herr Otto", "LK", 5),
            Kurs("D-GK1", "Deutsch", "Frau Fell", "GK", 3),
            Kurs("MA-GK1", "Mathematik", "Herr Sturm", "GK", 3),
            Kurs("EN-GK1", "Englisch", "Frau Nagel", "GK", 3),
            Kurs("BIO-GK1", "Biologie", "Herr Reiter", "GK", 3),
            Kurs("GE-GK1", "Geschichte", "Herr Vogt", "GK", 2),
            Kurs("SP-GK1", "Sport", "Frau Kraus", "GK", 2),
            Kurs("REL-GK1", "Religion", "Pfarrer Huber", "GK", 2),
            Kurs("ETH-GK1", "Ethik", "Frau Lange", "GK", 2),
            Kurs("CH-GK1", "Chemie", "Herr Schmid", "GK", 2),
            Kurs("KU-GK1", "Kunst", "Frau Weiss", "GK", 2)
        }
    End Function

    Private Function BuildSchienenList() As List(Of JsonObject)
        Return New List(Of JsonObject) From {
            Schiene("S-LK1", "LK", 5), Schiene("S-LK2", "LK", 5), Schiene("S-LK3", "LK", 5), Schiene("S-LK4", "LK", 5),
            Schiene("S-GK3-1", "GK", 3), Schiene("S-GK3-2", "GK", 3),
            Schiene("S-GK2-1", "GK", 2), Schiene("S-GK2-2", "GK", 2), Schiene("S-GK2-3", "GK", 2), Schiene("S-GK2-4", "GK", 2)
        }
    End Function

    ''' <summary>WahlprofilId -> its chosen Kurs-IDs. Public so both the
    ''' LLM-extraction ground truth (via CompletenessReport below) and a
    ''' plain Solve-sanity test can use the exact same source of truth.</summary>
    Public ReadOnly ExpectedKurswahl As New Dictionary(Of String, List(Of String)) From {
        {"WP-Natur", New List(Of String) From {"D-LK1", "MA-LK1", "BIO-LK1", "EN-GK1", "GE-GK1", "REL-GK1", "SP-GK1"}},
        {"WP-Sprachen", New List(Of String) From {"MA-LK1", "EN-LK1", "BIO-LK1", "D-GK1", "GE-GK1", "ETH-GK1", "SP-GK1"}},
        {"WP-Mint", New List(Of String) From {"D-LK1", "EN-LK1", "BIO-LK1", "MA-GK1", "GE-GK1", "REL-GK1", "SP-GK1"}},
        {"WP-Gesellschaft", New List(Of String) From {"D-LK1", "MA-LK1", "EN-LK1", "BIO-GK1", "GE-GK1", "ETH-GK1", "SP-GK1"}},
        {"WP-Bio", New List(Of String) From {"MA-LK1", "BIO-LK1", "EN-LK1", "D-GK1", "GE-GK1", "REL-GK1", "SP-GK1"}}
    }

    Public ReadOnly ExpectedStudentCount As New Dictionary(Of String, Integer) From {
        {"WP-Natur", 22}, {"WP-Sprachen", 18}, {"WP-Mint", 25}, {"WP-Gesellschaft", 20}, {"WP-Bio", 17}
    }

    Public Function BuildKursstufePromptScenario() As JsonObject
        Dim kurse = BuildKurse()
        Dim schienen = BuildSchienenList()
        Dim teachers = kurse.Select(Function(k) JsonHelpers.GetString(k, "teacher")).Distinct().OrderBy(Function(s) s).ToList()
        Dim subjects = kurse.Select(Function(k) JsonHelpers.GetString(k, "subject")).Distinct().OrderBy(Function(s) s).ToList()

        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray(teachers.Select(Function(t) CType(t, JsonNode)).ToArray())},
            {"subjects", New JsonArray(subjects.Select(Function(s) CType(s, JsonNode)).ToArray())},
            {"rooms", New JsonArray()},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray(Days.Select(Function(d) CType(d, JsonNode)).ToArray())},
                {"periods_per_day", PeriodsPerDay}
            }},
            {"kurse", New JsonArray(kurse.Select(Function(k) CType(k, JsonNode)).ToArray())},
            {"schienen", New JsonArray(schienen.Select(Function(s) CType(s, JsonNode)).ToArray())}
        }

        Dim constraints = ExpectedKurswahl.Select(Function(kvp) CType(New JsonObject From {
            {"type", "kurswahl"}, {"wahlprofil_id", kvp.Key}, {"student_count", ExpectedStudentCount(kvp.Key)},
            {"kurse", New JsonArray(kvp.Value.Select(Function(k) CType(k, JsonNode)).ToArray())}
        }, JsonNode)).ToList()

        Return New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray(constraints.ToArray())}
        }
    End Function

    Public ReadOnly Prompt As String =
        "Wir sind die Kursstufe eines Gymnasiums in Baden-Wuerttemberg. Es " &
        "gibt 5 Wahlprofile mit folgender Kurswahl:" & vbLf &
        "- Profil 'WP-Natur' (22 Schueler): Leistungskurse Deutsch, " &
        "Mathematik und Biologie. Grundkurse: Englisch, Geschichte, " &
        "Religion, Sport." & vbLf &
        "- Profil 'WP-Sprachen' (18 Schueler): Leistungskurse Mathematik, " &
        "Englisch und Biologie. Grundkurse: Deutsch, Geschichte, Ethik, " &
        "Sport." & vbLf &
        "- Profil 'WP-Mint' (25 Schueler): Leistungskurse Deutsch, " &
        "Englisch und Biologie. Grundkurse: Mathematik, Geschichte, " &
        "Religion, Sport." & vbLf &
        "- Profil 'WP-Gesellschaft' (20 Schueler): Leistungskurse Deutsch, " &
        "Mathematik und Englisch. Grundkurse: Biologie, Geschichte, " &
        "Ethik, Sport." & vbLf &
        "- Profil 'WP-Bio' (17 Schueler): Leistungskurse Mathematik, " &
        "Biologie und Englisch. Grundkurse: Deutsch, Geschichte, " &
        "Religion, Sport." & vbLf &
        "Erzeuge daraus die Kurswahl-Constraints im vereinbarten " &
        "JSON-Format fuer den Solver." & vbLf

    ''' <summary>Public (mirrors GrundschuleFixture.CompletenessReport's
    ''' pattern) so both a gated LLM E2E test and any future
    ''' RobustnessRunner integration can call it.</summary>
    Public Function CompletenessReport(extracted As List(Of JsonObject)) As Dictionary(Of String, Double)
        Dim expectedSorted = ExpectedKurswahl.ToDictionary(Function(kvp) kvp.Key, Function(kvp) kvp.Value.OrderBy(Function(s) s).ToList())
        Dim scores As New Dictionary(Of String, Double) From {
            {"kurswahl", CompletenessScoring.ScoreKurswahl(expectedSorted, extracted)},
            {"kurswahl_student_count", CompletenessScoring.ScoreKurswahlStudentCount(ExpectedStudentCount, extracted)}
        }
        scores("overall") = CompletenessScoring.OverallScore(scores)
        Return scores
    End Function

End Module
