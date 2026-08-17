' Ported 1:1 from tests/fixture_gymnasium_klasse5.py. Test scenario for a
' 4-zuegiges Gymnasium, Klassenstufe 5, angelehnt an eine typische
' Baden-Wuerttemberg-Stundentafel (G9) und die Groessenordnung einer Schule
' wie dem GSG Fellbach.
'
' Wichtiger Hinweis: Dies ist KEIN verifizierter Auszug aus dem echten
' Lehrplan, Kollegium oder Raumplan einer bestimmten Schule. Fach- und
' Stundenverteilung orientieren sich an oeffentlich bekannten, typischen
' BW-Gymnasium-Kontingentstundentafeln fuer Klasse 5, wurden aber
' vereinfacht und an das aktuelle Constraint-Schema angepasst -
' insbesondere:
'
' - Konfessioneller Religionsunterricht (klassenuebergreifende Gruppen)
'   wird NICHT abgebildet, da das aktuelle Schema keine
'   klassenuebergreifenden Lerngruppen kennt.
' - BNT wird real oft als 2+1-Aufteilung unterrichtet, aber
'   consecutive_required erzwingt ALLE Wochenstunden eines Fachs in
'   gleich langen Bloecken - daher hier bewusst auf 4h/Woche (2x
'   Doppelstunde) gesetzt, um innerhalb der Schema-Grenzen zu bleiben.
'   Das ist eine reale Einschraenkung des aktuellen Modells, keine
'   Vereinfachung der Testdaten aus Bequemlichkeit.
Imports System.Text.Json.Nodes

Public Module GymnasiumKlasse5Fixture

    Public ReadOnly Classes As New List(Of String) From {"5a", "5b", "5c", "5d"}
    Public ReadOnly Days As New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}
    Public Const PeriodsPerDay As Integer = 7

    Public NotInheritable Class Assignment
        Public ReadOnly Subject As String
        Public ReadOnly Teacher As String
        Public ReadOnly TaughtClasses As List(Of String)
        Public ReadOnly Hours As Integer
        Public ReadOnly MaxPerDay As Integer
        Public ReadOnly BlockLength As Integer?
        Public ReadOnly AllowedRooms As List(Of String)

        Public Sub New(subject As String, teacher As String, taughtClasses As List(Of String),
                        hours As Integer, maxPerDay As Integer, blockLength As Integer?, allowedRooms As List(Of String))
            Me.Subject = subject
            Me.Teacher = teacher
            Me.TaughtClasses = taughtClasses
            Me.Hours = hours
            Me.MaxPerDay = maxPerDay
            Me.BlockLength = blockLength
            Me.AllowedRooms = allowedRooms
        End Sub
    End Class

    ' subject, teacher, classes taught by this teacher, hours_per_week,
    ' max_per_day, consecutive block_length (Nothing = kein Block-Zwang),
    ' allowed_rooms (Nothing = normaler Klassenraum, kein Fachraum-Zwang)
    Public ReadOnly Assignments As New List(Of Assignment) From {
        New Assignment("Deutsch", "Frau Vogel", New List(Of String) From {"5a", "5b"}, 5, 2, Nothing, Nothing),
        New Assignment("Deutsch", "Herr Baumann", New List(Of String) From {"5c", "5d"}, 5, 2, Nothing, Nothing),
        New Assignment("Mathematik", "Herr Krause", New List(Of String) From {"5a", "5c"}, 5, 2, Nothing, Nothing),
        New Assignment("Mathematik", "Frau Nguyen", New List(Of String) From {"5b", "5d"}, 5, 2, Nothing, Nothing),
        New Assignment("Englisch", "Frau Fischer", New List(Of String) From {"5a", "5d"}, 5, 2, Nothing, Nothing),
        New Assignment("Englisch", "Herr Roth", New List(Of String) From {"5b", "5c"}, 5, 2, Nothing, Nothing),
        New Assignment("BNT", "Frau Kraemer", New List(Of String) From {"5a", "5b"}, 4, 2, 2, New List(Of String) From {"NaWi-Raum"}),
        New Assignment("BNT", "Herr Werner", New List(Of String) From {"5c", "5d"}, 4, 2, 2, New List(Of String) From {"NaWi-Raum"}),
        New Assignment("Sport", "Herr Braun", New List(Of String) From {"5a", "5b"}, 3, 2, Nothing, New List(Of String) From {"Sporthalle1", "Sporthalle2"}),
        New Assignment("Sport", "Frau Lang", New List(Of String) From {"5c", "5d"}, 3, 2, Nothing, New List(Of String) From {"Sporthalle1", "Sporthalle2"}),
        New Assignment("Musik", "Frau Adler", New List(Of String) From {"5a", "5b", "5c", "5d"}, 2, 2, Nothing, New List(Of String) From {"Musiksaal"}),
        New Assignment("Kunst", "Herr Schuster", New List(Of String) From {"5a", "5c"}, 2, 2, Nothing, New List(Of String) From {"Kunstraum"}),
        New Assignment("Kunst", "Frau Weiss", New List(Of String) From {"5b", "5d"}, 2, 2, Nothing, New List(Of String) From {"Kunstraum"}),
        New Assignment("Religion", "Pfarrer Huber", New List(Of String) From {"5a", "5b", "5c", "5d"}, 2, 2, Nothing, Nothing),
        New Assignment("Erdkunde", "Herr Fink", New List(Of String) From {"5a", "5b", "5c", "5d"}, 2, 2, Nothing, Nothing)
    }

    Private Function BuildScenario() As JsonObject
        Dim teachers = Assignments.Select(Function(a) a.Teacher).Distinct().OrderBy(Function(s) s).ToList()
        Dim subjects = Assignments.Select(Function(a) a.Subject).Distinct().OrderBy(Function(s) s).ToList()
        Dim rooms = Assignments.Where(Function(a) a.AllowedRooms IsNot Nothing).
            SelectMany(Function(a) a.AllowedRooms).Distinct().OrderBy(Function(s) s).ToList()

        Dim constraints As New JsonArray()

        For Each a In Assignments
            For Each cls In a.TaughtClasses
                constraints.Add(New JsonObject From {
                    {"type", "teacher_subject_assignment"}, {"teacher", a.Teacher}, {"class", cls}, {"subject", a.Subject}
                })
                Dim wh As New JsonObject From {
                    {"type", "weekly_hours"}, {"class", cls}, {"subject", a.Subject},
                    {"hours_per_week", a.Hours}, {"max_per_day", a.MaxPerDay}
                }
                constraints.Add(wh)
                If a.BlockLength.HasValue Then
                    constraints.Add(New JsonObject From {
                        {"type", "consecutive_required"}, {"class", cls}, {"subject", a.Subject}, {"block_length", a.BlockLength.Value}
                    })
                End If
            Next
        Next

        ' room_requirement genau einmal pro Fach (mehrere Lehrkraefte pro Fach
        ' teilen sich denselben Fachraum-Pool, siehe Assignments oben)
        Dim roomsPerSubject As New Dictionary(Of String, List(Of String))
        For Each a In Assignments
            If a.AllowedRooms IsNot Nothing AndAlso Not roomsPerSubject.ContainsKey(a.Subject) Then
                roomsPerSubject(a.Subject) = a.AllowedRooms
            End If
        Next
        For Each kvp In roomsPerSubject
            constraints.Add(New JsonObject From {
                {"type", "room_requirement"}, {"subject", kvp.Key},
                {"allowed_rooms", New JsonArray(kvp.Value.Select(Function(r) CType(r, JsonNode)).ToArray())}
            })
        Next

        ' Standard-Ueberschneidungsfreiheit: jede Klasse, jede Lehrkraft, jeder Fachraum
        For Each cls In Classes
            constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "class"}, {"entity", cls}})
        Next
        For Each teacher In teachers
            constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "teacher"}, {"entity", teacher}})
        Next
        For Each room In rooms
            constraints.Add(New JsonObject From {{"type", "no_overlap"}, {"resource", "room"}, {"entity", room}})
        Next

        constraints.Add(New JsonObject From {
            {"type", "teacher_availability"}, {"teacher", "Frau Nguyen"},
            {"available_days", New JsonArray From {"Mo", "Di", "Mi"}},
            {"reason", "Teilzeit"}
        })
        constraints.Add(New JsonObject From {
            {"type", "teacher_availability"}, {"teacher", "Herr Werner"},
            {"available_days", New JsonArray From {"Mo", "Di", "Mi", "Do"}},
            {"reason", "Fortbildungstag freitags"}
        })

        ' Schulweite Sperrzeiten fuer Klasse 5: Mittwochnachmittag frei,
        ' frueherer Schulschluss freitags.
        For Each cls In Classes
            constraints.Add(New JsonObject From {
                {"type", "forbidden_slot"}, {"scope", "class"}, {"entity", cls},
                {"day", "Mi"}, {"period", PeriodsPerDay},
                {"reason", "Mittwochnachmittag frei (Kl. 5)"}
            })
            constraints.Add(New JsonObject From {
                {"type", "forbidden_slot"}, {"scope", "class"}, {"entity", cls},
                {"day", "Fr"}, {"period", PeriodsPerDay},
                {"reason", "Fruehere Schulschluss freitags"}
            })
        Next

        Return New JsonObject From {
            {"entities", New JsonObject From {
                {"classes", New JsonArray(Classes.Select(Function(c) CType(c, JsonNode)).ToArray())},
                {"teachers", New JsonArray(teachers.Select(Function(t) CType(t, JsonNode)).ToArray())},
                {"subjects", New JsonArray(subjects.Select(Function(s) CType(s, JsonNode)).ToArray())},
                {"rooms", New JsonArray(rooms.Select(Function(r) CType(r, JsonNode)).ToArray())},
                {"timeslots", New JsonObject From {
                    {"days", New JsonArray(Days.Select(Function(d) CType(d, JsonNode)).ToArray())},
                    {"periods_per_day", PeriodsPerDay}
                }}
            }},
            {"constraints", constraints}
        }
    End Function

    Public Function BuildGymnasiumKlasse5Scenario() As JsonObject
        Return BuildScenario()
    End Function

End Module
