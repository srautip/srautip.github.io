' Phase 2 scenario B: "Berufsschule Oberstufe" - a technical Berufskolleg
' (Elektrotechnik + Metalltechnik tracks, 4 classes), larger and more
' cross-linked than GymnasiumKlasse5Fixture:
' - Several special rooms shared across classes/subjects (PC-Raum used by
'   all 4 classes' Informatik; Elektrolabor/Werkstatt each used by one
'   track's Praktikum).
' - A real teacher/subject "cross matrix": Herr Krause and Frau Nguyen each
'   teach TWO different subjects (Mathematik AND Wirtschaftskunde) to the
'   SAME class pairing, unlike Gymnasium's cleaner one-teacher-one-subject
'   layout.
' - Two shared_resource_conflict facts (deliberately redundant with the
'   corresponding no_overlap(teacher) rule, matching the real precedent in
'   FullScenarioFixture.vb/timetable/tests - an LLM asked to extract "which
'   classes share a teacher for the same subject" will naturally restate a
'   fact already covered by the general overlap rule).
' - A period_exception with MULTIPLE allowed days ("nur mittwochs und
'   freitags") - Phase 1 only ever exercised a single allowed day ("nur
'   dienstags"); this is the untested generalization.
Imports System.Text.Json.Nodes

Public Module OberstufeFixture

    ' Private, see GrundschuleFixture.vb's header comment for why these
    ' aren't Public (would collide with GymnasiumKlasse5Fixture's
    ' same-named module members via VB's unqualified Module exposure).
    Private ReadOnly Classes As New List(Of String) From {"E11", "E12", "M11", "M12"}
    Private ReadOnly Days As New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}
    Private Const PeriodsPerDay As Integer = 8

    Public ReadOnly OberstufeAssignments As New List(Of SubjectAssignment) From {
        New SubjectAssignment("Fachtheorie Elektro", "Herr Sailer", New List(Of String) From {"E11", "E12"}, 5, 2, Nothing, Nothing),
        New SubjectAssignment("Fachtheorie Metall", "Frau Huber", New List(Of String) From {"M11", "M12"}, 5, 2, Nothing, Nothing),
        New SubjectAssignment("Elektropraktikum", "Herr Sailer", New List(Of String) From {"E11", "E12"}, 4, 2, 2, New List(Of String) From {"Elektrolabor"}),
        New SubjectAssignment("Metallpraktikum", "Frau Huber", New List(Of String) From {"M11", "M12"}, 4, 2, 2, New List(Of String) From {"Werkstatt"}),
        New SubjectAssignment("Mathematik", "Herr Krause", New List(Of String) From {"E11", "M11"}, 3, 2, Nothing, Nothing),
        New SubjectAssignment("Mathematik", "Frau Nguyen", New List(Of String) From {"E12", "M12"}, 3, 2, Nothing, Nothing),
        New SubjectAssignment("Wirtschaftskunde", "Herr Krause", New List(Of String) From {"E11", "M11"}, 2, 1, Nothing, Nothing),
        New SubjectAssignment("Wirtschaftskunde", "Frau Nguyen", New List(Of String) From {"E12", "M12"}, 2, 1, Nothing, Nothing),
        New SubjectAssignment("Deutsch", "Frau Adler", New List(Of String) From {"E11", "E12", "M11", "M12"}, 2, 1, Nothing, Nothing),
        New SubjectAssignment("Informatik", "Herr Vogt", New List(Of String) From {"E11", "E12", "M11", "M12"}, 2, 1, Nothing, New List(Of String) From {"PC-Raum"})
    }

    ' teacher_availability ground truth (not derivable from
    ' OberstufeAssignments): "Frau Huber ... nur montags bis donnerstags".
    Public ReadOnly ExpectedUnavailableDays As New Dictionary(Of String, HashSet(Of String)) From {
        {"Frau Huber", New HashSet(Of String) From {"Fr"}}
    }

    ' period_exception ground truth: period 8 allowed only Mi/Fr -> blocked
    ' Mo/Di/Do for every class (the multi-allowed-day case).
    Public Function ExpectedForbiddenSlots() As HashSet(Of (Entity As String, Day As String, Period As Integer))
        Dim result As New HashSet(Of (String, String, Integer))
        For Each cls In Classes
            For Each d In {"Mo", "Di", "Do"}
                result.Add((cls, d, PeriodsPerDay))
            Next
        Next
        Return result
    End Function

    Public Function ExpectedSharedResourceConflicts() As HashSet(Of (Classes As String, Subject As String, Teacher As String))
        Return New HashSet(Of (String, String, String)) From {
            (String.Join(",", New List(Of String) From {"E11", "E12", "M11", "M12"}), "Informatik", "Herr Vogt"),
            (String.Join(",", New List(Of String) From {"E11", "M11"}), "Wirtschaftskunde", "Herr Krause")
        }
    End Function

    Private Function ExtraConstraints() As List(Of JsonObject)
        Dim result As New List(Of JsonObject) From {
            New JsonObject From {
                {"type", "teacher_availability"}, {"teacher", "Frau Huber"},
                {"available_days", New JsonArray From {"Mo", "Di", "Mi", "Do"}}
            },
            New JsonObject From {
                {"type", "shared_resource_conflict"},
                {"classes", New JsonArray From {"E11", "E12", "M11", "M12"}},
                {"subject", "Informatik"}, {"teacher", "Herr Vogt"}
            },
            New JsonObject From {
                {"type", "shared_resource_conflict"},
                {"classes", New JsonArray From {"E11", "M11"}},
                {"subject", "Wirtschaftskunde"}, {"teacher", "Herr Krause"}
            }
        }
        For Each cls In Classes
            For Each d In {"Mo", "Di", "Do"}
                result.Add(New JsonObject From {
                    {"type", "forbidden_slot"}, {"scope", "class"}, {"entity", cls}, {"day", d}, {"period", PeriodsPerDay}
                })
            Next
        Next
        Return result
    End Function

    Public Function BuildOberstufeScenario() As JsonObject
        Return AssignmentScenarioBuilder.BuildScenario(Classes, Days, PeriodsPerDay, OberstufeAssignments, ExtraConstraints())
    End Function

    Public ReadOnly Prompt As String =
        "Wir sind ein technisches Berufskolleg mit vier Klassen: E11 und E12" & vbLf &
        "(Elektrotechnik), M11 und M12 (Metalltechnik). Der Unterricht laeuft" & vbLf &
        "Montag bis Freitag mit je 8 Stunden pro Tag." & vbLf & vbLf &
        "Faecher und Zuordnung:" & vbLf &
        "- Fachtheorie Elektro (5 Stunden/Woche, hoechstens 2 pro Tag): Herr" & vbLf &
        "  Sailer unterrichtet E11 und E12." & vbLf &
        "- Fachtheorie Metall (5 Stunden/Woche, hoechstens 2 pro Tag): Frau" & vbLf &
        "  Huber unterrichtet M11 und M12." & vbLf &
        "- Elektropraktikum (4 Stunden/Woche, hoechstens 2 pro Tag, muss als" & vbLf &
        "  zwei Doppelstunden stattfinden, immer im Elektrolabor): Herr Sailer" & vbLf &
        "  unterrichtet E11 und E12." & vbLf &
        "- Metallpraktikum (4 Stunden/Woche, hoechstens 2 pro Tag, muss als" & vbLf &
        "  zwei Doppelstunden stattfinden, immer in der Werkstatt): Frau Huber" & vbLf &
        "  unterrichtet M11 und M12." & vbLf &
        "- Mathematik (3 Stunden/Woche, hoechstens 2 pro Tag): Herr Krause" & vbLf &
        "  unterrichtet E11 und M11, Frau Nguyen unterrichtet E12 und M12." & vbLf &
        "- Wirtschaftskunde (2 Stunden/Woche, hoechstens 1 pro Tag): Herr" & vbLf &
        "  Krause unterrichtet ZUSAETZLICH E11 und M11, Frau Nguyen" & vbLf &
        "  unterrichtet zusaetzlich E12 und M12." & vbLf &
        "- Deutsch (2 Stunden/Woche, hoechstens 1 pro Tag): Frau Adler" & vbLf &
        "  unterrichtet alle vier Klassen." & vbLf &
        "- Informatik (2 Stunden/Woche, hoechstens 1 pro Tag, immer im" & vbLf &
        "  PC-Raum): Herr Vogt unterrichtet alle vier Klassen." & vbLf & vbLf &
        "Verfuegbarkeit:" & vbLf &
        "- Frau Huber arbeitet Teilzeit und ist nur montags bis donnerstags an" & vbLf &
        "  der Schule." & vbLf & vbLf &
        "Sperrzeiten:" & vbLf &
        "- Die 8. Stunde findet fuer alle vier Klassen nur mittwochs und" & vbLf &
        "  freitags statt. An Montag, Dienstag und Donnerstag gibt es keine" & vbLf &
        "  8. Stunde." & vbLf & vbLf &
        "Weil Herr Vogt und Herr Krause jeweils mehrere Klassen gleichzeitig im" & vbLf &
        "selben Fach unterrichten wuerden, koennen diese Klassen fuer das" & vbLf &
        "jeweilige Fach nicht gleichzeitig Unterricht haben." & vbLf & vbLf &
        "Zusaetzlich gilt fuer alle Klassen, Lehrkraefte und Fachraeume die" & vbLf &
        "uebliche Ueberschneidungsfreiheit: niemand kann zwei Dinge" & vbLf &
        "gleichzeitig haben, und kein Fachraum kann von zwei Gruppen" & vbLf &
        "gleichzeitig genutzt werden." & vbLf & vbLf &
        "Erzeuge daraus die passenden Constraints im vereinbarten JSON-Format" & vbLf &
        "fuer den CP-SAT-Solver." & vbLf

End Module
