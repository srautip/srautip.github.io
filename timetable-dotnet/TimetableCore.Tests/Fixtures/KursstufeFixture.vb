' Phase 2.11h: a full-size Kursstufe (Kl. 11/12, BW-Qualifikationsphase)
' scenario for Solver.SolveKursstufe benchmarking at real school scale,
' continuing the same fictional, GSG-Fellbach-inspired school as
' GymnasiumSekIFixture.vb (which deliberately stopped at Sekundarstufe I
' because this project's data model back then had no Kurssystem concept
' at all - Phase 2.11 is precisely what closes that gap).
'
' IMPORTANT: this is SYNTHETIC, plausible test data following the current
' (2025-reform) Baden-Wuerttemberg Kursstufen-Struktur (3 Leistungsfaecher
' @5h/Woche, Basisfaecher @2-3h/Woche) - it is NOT the real school's actual
' Kursangebot, Lehrkraefte, or Schuelerzahlen.
'
' Deliberate simplifications, documented here rather than left implicit:
' - Wahlprofile-mit-Schueleranzahl (per the user's own scoping decision for
'   Phase 2.11): 14 representative choice-bundles, each with a plausible
'   student COUNT, not individually named students.
' - Every Kurs has exactly one teacher, and every teacher teaches exactly
'   one Kurs (the same simplification GymnasiumSekIFixture.vb already
'   documents for its own subject assignments) - avoids any risk of an
'   accidental teacher-name collision (the real bug bisected out of that
'   earlier fixture) while still exercising Kursblockung's teacher-
'   collision constraint structurally (see Kursblockung.vb).
' - GK (Grundkurs) selection per Wahlprofil follows a simple, deterministic
'   rule (see BuildWahlprofile) approximating the real BW Basisfach logic
'   (Deutsch/Mathematik/Englisch/1 Naturwissenschaft as Basisfach unless
'   already chosen as Leistungsfach, plus Geschichte-or-Gemeinschaftskunde,
'   Religion-or-Ethik, Sport, and one elective) - not a literal, curriculum-
'   accurate implementation of every BW Kursstufen-VO rule (this project's
'   data model plans exactly one Halbjahr-sized weekly grid at a time, not
'   a 4-Halbjahre progression - an explicitly documented scoping choice
'   from the Phase 2.11 plan, not an oversight).
' - ~32 Kurse (below the originally sketched "~45-65") and ~343 Schueler
'   (above the originally sketched "~250-300") - both approached, not hit
'   exactly, the same honesty convention GymnasiumSekIFixture.vb already
'   established for its own teacher-count target.
'
' Like GrundschuleGrossFixture/GymnasiumSekIFixture, this has NO prompt
' text and NO Expected*/CompletenessReport functions - pure
' Solver.SolveKursstufe benchmark input, not an LLM-extraction test
' fixture (see KursstufePromptFixture.vb, Phase 2.11h's smaller,
' LLM-testable sibling, for that).
Imports System.Text.Json.Nodes

Public Module KursstufeFixture

    Private ReadOnly Days As New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}
    ' 11 statt 8 Perioden/Tag (laenger als GymnasiumSekIFixture's Sek-I-Tag) -
    ' die Kursstufe ist an vielen BW-Schulen die einzige Stufe mit
    ' regelmaessigem Nachmittagsunterricht. Empirisch noetig: selbst nach
    ' der Konfliktgruppen-Optimierung (Schienenraster.vb) UND der
    ' Schienen-Kapazitaetsbegrenzung (Kursblockung.vb) braucht die
    ' groesste zusammenhaengende Schienen-Gruppe hier ca. 50
    ' Wochenstunden (fast alle Schienen haengen ueber die von JEDEM
    ' Wahlprofil gewaehlten GK2-Kurse zusammen) - ein 5x8=40-Slot-Raster
    ' reicht dafuer nicht annaehernd, 5x11=55 laesst spuerbaren Puffer.
    Private Const PeriodsPerDay As Integer = 11

    ' (Subject, Sections) - "Sections" = how many parallel Kurs offerings
    ' of that subject/kursart exist, distinct teachers each.
    Private ReadOnly LkSubjects As New List(Of (Subject As String, Sections As Integer)) From {
        ("Deutsch", 2), ("Mathematik", 2), ("Englisch", 2),
        ("Biologie", 1), ("Physik", 1), ("Geschichte", 1), ("Chemie", 1)
    }
    Private ReadOnly Gk3hSubjects As New List(Of (Subject As String, Sections As Integer)) From {
        ("Deutsch", 2), ("Mathematik", 2), ("Englisch", 2), ("Biologie", 1), ("Physik", 1)
    }
    Private ReadOnly Gk2hSubjects As New List(Of (Subject As String, Sections As Integer)) From {
        ("Geschichte", 1), ("Gemeinschaftskunde", 1), ("Religion", 1), ("Ethik", 1),
        ("Sport", 2), ("Kunst", 1), ("Musik", 1), ("Chemie", 1), ("Informatik", 1),
        ("Geographie", 1), ("Franzoesisch", 1), ("Spanisch", 1), ("Wirtschaft", 1),
        ("Psychologie", 1), ("NWT", 1), ("DarstellendesSpiel", 1)
    }

    Private ReadOnly LkCombos As New List(Of List(Of String)) From {
        New List(Of String) From {"Deutsch", "Mathematik", "Englisch"},
        New List(Of String) From {"Mathematik", "Physik", "Chemie"},
        New List(Of String) From {"Biologie", "Chemie", "Mathematik"},
        New List(Of String) From {"Deutsch", "Geschichte", "Englisch"},
        New List(Of String) From {"Mathematik", "Biologie", "Chemie"},
        New List(Of String) From {"Deutsch", "Englisch", "Biologie"},
        New List(Of String) From {"Physik", "Mathematik", "Englisch"},
        New List(Of String) From {"Geschichte", "Deutsch", "Chemie"},
        New List(Of String) From {"Biologie", "Physik", "Chemie"},
        New List(Of String) From {"Englisch", "Geschichte", "Mathematik"},
        New List(Of String) From {"Deutsch", "Biologie", "Chemie"},
        New List(Of String) From {"Mathematik", "Englisch", "Chemie"},
        New List(Of String) From {"Physik", "Chemie", "Deutsch"},
        New List(Of String) From {"Geschichte", "Englisch", "Biologie"}
    }
    Private ReadOnly StudentCounts As New List(Of Integer) From {28, 22, 19, 31, 24, 26, 18, 29, 21, 33, 17, 25, 30, 20}
    Private ReadOnly Electives As New List(Of String) From {
        "Kunst", "Musik", "Informatik", "Geographie", "Franzoesisch", "Spanisch",
        "Wirtschaft", "Psychologie", "NWT", "DarstellendesSpiel"
    }
    Private ReadOnly NaturalSciences As New List(Of String) From {"Biologie", "Physik", "Chemie"}

    Private Function LkId(subject As String, section As Integer) As String
        Return $"LK-{subject}-{section}"
    End Function

    Private Function Gk3Id(subject As String, section As Integer) As String
        Return $"GK3-{subject}-{section}"
    End Function

    Private Function Gk2Id(subject As String, section As Integer) As String
        Return $"GK2-{subject}-{section}"
    End Function

    Private Function SectionCount(subjects As List(Of (Subject As String, Sections As Integer)), subject As String) As Integer
        Return subjects.First(Function(e) e.Subject = subject).Sections
    End Function

    ''' <summary>Adds one Kurs JSON object per (subject, section) pair -
    ''' `teacherCounters` is keyed by "{subject}-{kursart}" so a subject
    ''' offered at both kursart values (e.g. Deutsch as both LK and GK)
    ''' never collides, mirroring the teacherCounters discipline
    ''' GymnasiumSekIFixture.vb's AddUniformSubject already established
    ''' (there: banduebergreifend per subject; here: kursart-uebergreifend
    ''' per subject) - this remains the FALLBACK naming path for subjects
    ''' with no Sek-I counterpart.
    '''
    ''' Phase 2.13: for a subject in SharedSchoolPool.SharedTeacherPool
    ''' (via PoolKeyFor's Geographie->Erdkunde alias), the teacher instead
    ''' comes from that shared pool, round-robin via `poolCounters` - kept
    ''' as a SEPARATE counter keyed by subject ALONE (not subject-kursart),
    ''' deliberately spanning LK+GK Kurse of the same subject together, so
    ''' each shared pool member gets AT MOST ONE Kursstufe Kurs even when a
    ''' subject offers both LK and GK sections - never two simultaneous
    ''' Kursstufe assignments stacked on the same shared teacher on top of
    ''' their existing Sek-I load. Every pool size was chosen so this
    ''' round-robin never needs to wrap (verified live, see
    ''' RealSchoolFixtureTests.vb).</summary>
    Private Sub AddSections(result As List(Of JsonObject), teacherCounters As Dictionary(Of String, Integer), poolCounters As Dictionary(Of String, Integer),
                             subjects As List(Of (Subject As String, Sections As Integer)),
                             kursart As String, hours As Integer, idPrefix As String,
                             idFn As Func(Of String, Integer, String))
        For Each entry In subjects
            For i = 1 To entry.Sections
                Dim poolKey = SharedSchoolPool.PoolKeyFor(entry.Subject)
                Dim pool = SharedSchoolPool.SharedTeacherPool.GetValueOrDefault(poolKey, Nothing)
                Dim teacherName As String
                If pool IsNot Nothing Then
                    Dim poolIndex = poolCounters.GetValueOrDefault(poolKey, 0)
                    teacherName = pool(poolIndex Mod pool.Count)
                    poolCounters(poolKey) = poolIndex + 1
                Else
                    Dim counterKey = $"{entry.Subject}-{kursart}"
                    Dim nextIndex = teacherCounters.GetValueOrDefault(counterKey, 0) + 1
                    teacherCounters(counterKey) = nextIndex
                    teacherName = $"T-{entry.Subject}-{kursart}-{nextIndex}"
                End If
                result.Add(New JsonObject From {
                    {"id", idFn(entry.Subject, i)},
                    {"subject", entry.Subject},
                    {"teacher", teacherName},
                    {"kursart", kursart},
                    {"hours_per_week", hours}
                })
            Next
        Next
    End Sub

    Private Function BuildKurse() As List(Of JsonObject)
        Dim result As New List(Of JsonObject)
        Dim teacherCounters As New Dictionary(Of String, Integer)
        Dim poolCounters As New Dictionary(Of String, Integer)
        AddSections(result, teacherCounters, poolCounters, LkSubjects, "LK", 5, "LK", AddressOf LkId)
        AddSections(result, teacherCounters, poolCounters, Gk3hSubjects, "GK", 3, "GK3", AddressOf Gk3Id)
        AddSections(result, teacherCounters, poolCounters, Gk2hSubjects, "GK", 2, "GK2", AddressOf Gk2Id)
        Return result
    End Function

    ''' <summary>Phase 2.11h finding: Kursblockung has no load-balancing
    ''' objective (see Kursblockung.vb - a deliberate, deferred stretch
    ''' feature), so without a hard capacity cap it happily piles
    ''' arbitrarily many Kurse onto one popular Schiene (nothing makes
    ''' that costly) - which then made Raumzuordnung (stage C, real room
    ''' assignment) Infeasible once this fixture reached realistic scale
    ''' (11 Kurse landed on one Schiene against only 8 available rooms in
    ''' an earlier version of this fixture). `RoomCount` below matches
    ''' this cap with a little headroom for the rare cross-group overlap
    ''' (see Schienenraster.GroupSchienenByConflict).</summary>
    Private Const SchienenCapacity As Integer = 5
    Private Const RoomCount As Integer = 10

    Private Function BuildSchienen() As List(Of JsonObject)
        Dim result As New List(Of JsonObject)
        For i = 1 To 5
            result.Add(New JsonObject From {{"id", $"S-LK{i}"}, {"kursart", "LK"}, {"hours_per_week", 5}, {"capacity", SchienenCapacity}})
        Next
        For i = 1 To 5
            result.Add(New JsonObject From {{"id", $"S-GK3-{i}"}, {"kursart", "GK"}, {"hours_per_week", 3}, {"capacity", SchienenCapacity}})
        Next
        ' 8 statt urspruenglich 4 GK2-Schienen: mit nur 4 Schienen musste
        ' JEDES Wahlprofil (das immer genau 4 GK2-Kurse waehlt - Geschichte/
        ' Gemeinschaftskunde, Religion/Ethik, Sport, 1 Wahlfach, siehe
        ' BuildWahlprofile) alle 4 verfuegbaren Schienen exakt einmal
        ' belegen - eine sehr enge Bijektions-Anforderung ueber alle 14
        ' Profile hinweg gleichzeitig, die sich empirisch als Infeasible
        ' erwies (auch mit ausreichender Gesamt-Kapazitaet). Mehr Schienen
        ' geben der Kursblockung genug Spielraum.
        For i = 1 To 8
            result.Add(New JsonObject From {{"id", $"S-GK2-{i}"}, {"kursart", "GK"}, {"hours_per_week", 2}, {"capacity", SchienenCapacity}})
        Next
        Return result
    End Function

    ''' <summary>Deterministic, plausible (not curriculum-exact - see the
    ''' module header) Wahlprofil construction: exactly the profile's 3
    ''' chosen LK subjects (round-robin across parallel sections for
    ''' balance) plus a GK bundle approximating BW's Basisfach rule -
    ''' Deutsch/Mathematik/Englisch as GK for whichever of the three
    ''' ISN'T this profile's LK choice, one Naturwissenschaft GK if none
    ''' of Biologie/Physik/Chemie is an LK choice, Geschichte-or-
    ''' Gemeinschaftskunde, Religion-or-Ethik, Sport, and one rotating
    ''' elective.</summary>
    Private Function BuildWahlprofile() As List(Of JsonObject)
        Dim result As New List(Of JsonObject)
        For i = 0 To LkCombos.Count - 1
            Dim profileLk = LkCombos(i)
            Dim kurse As New List(Of String)

            For Each subject In profileLk
                Dim section = (i Mod SectionCount(LkSubjects, subject)) + 1
                kurse.Add(LkId(subject, section))
            Next

            For Each subject In New List(Of String) From {"Deutsch", "Mathematik", "Englisch"}
                If Not profileLk.Contains(subject) Then
                    Dim section = (i Mod SectionCount(Gk3hSubjects, subject)) + 1
                    kurse.Add(Gk3Id(subject, section))
                End If
            Next

            If Not NaturalSciences.Any(Function(s) profileLk.Contains(s)) Then
                Dim section = (i Mod SectionCount(Gk3hSubjects, "Biologie")) + 1
                kurse.Add(Gk3Id("Biologie", section))
            End If

            kurse.Add(If(profileLk.Contains("Geschichte"), Gk2Id("Gemeinschaftskunde", 1), Gk2Id("Geschichte", 1)))
            kurse.Add(If(i Mod 2 = 0, Gk2Id("Religion", 1), Gk2Id("Ethik", 1)))
            kurse.Add(Gk2Id("Sport", (i Mod SectionCount(Gk2hSubjects, "Sport")) + 1))
            kurse.Add(Gk2Id(Electives(i Mod Electives.Count), 1))

            result.Add(New JsonObject From {
                {"type", "kurswahl"}, {"wahlprofil_id", $"WP{i + 1}"}, {"student_count", StudentCounts(i)},
                {"kurse", New JsonArray(kurse.Select(Function(k) CType(k, JsonNode)).ToArray())}
            })
        Next
        Return result
    End Function

    Public Function BuildKursstufeScenario() As JsonObject
        Dim kurse = BuildKurse()
        Dim schienen = BuildSchienen()
        Dim wahlprofile = BuildWahlprofile()

        Dim teachers = kurse.Select(Function(k) JsonHelpers.GetString(k, "teacher")).Distinct().OrderBy(Function(s) s).ToList()
        Dim subjects = kurse.Select(Function(k) JsonHelpers.GetString(k, "subject")).Distinct().OrderBy(Function(s) s).ToList()
        ' Phase 2.13: the generic Kursraum pool stays as the fallback for
        ' subjects with no real room need, PLUS Sek I's own special rooms
        ' (Turnhallen/NaWi/Musik/Kunst/PC) - listed here so
        ' Validation.ValidateEntities accepts them as known rooms when a
        ' caller (e.g. Raumzuordnung.BuildRaumzuordnungScenario's new
        ' specialRoomsBySubject parameter) actually assigns a subject like
        ' Sport or Biologie to one of them instead of a generic Kursraum.
        Dim rooms = Enumerable.Range(1, RoomCount).Select(Function(i) $"Kursraum{i}").ToList()
        rooms.AddRange({"Turnhalle1", "Turnhalle2", "Turnhalle3", "NaWi1", "NaWi2", "NaWi3", "Musikraum", "Kunstraum", "PC-Raum"})

        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray(teachers.Select(Function(t) CType(t, JsonNode)).ToArray())},
            {"subjects", New JsonArray(subjects.Select(Function(s) CType(s, JsonNode)).ToArray())},
            {"rooms", New JsonArray(rooms.Select(Function(r) CType(r, JsonNode)).ToArray())},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray(Days.Select(Function(d) CType(d, JsonNode)).ToArray())},
                {"periods_per_day", PeriodsPerDay}
            }},
            {"kurse", New JsonArray(kurse.Select(Function(k) CType(k, JsonNode)).ToArray())},
            {"schienen", New JsonArray(schienen.Select(Function(s) CType(s, JsonNode)).ToArray())}
        }
        Return New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray(wahlprofile.Select(Function(w) CType(w, JsonNode)).ToArray())}
        }
    End Function

End Module
