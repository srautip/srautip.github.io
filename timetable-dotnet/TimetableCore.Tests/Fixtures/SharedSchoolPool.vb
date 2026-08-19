' Phase 2.13: a shared per-subject teacher-name pool, used by BOTH
' GymnasiumSekIFixture.vb and KursstufeFixture.vb, so a teacher who teaches
' a subject in Sek I can be the SAME named individual as one teaching that
' subject's Kursstufe Kurs - closing a gap explicitly documented since
' Phase 2.11 (GsgCompleteScenarioTests.vb's header comment: "this project
' has no notion of 'this Sek-I teacher ALSO teaches a Kursstufe-Kurs'").
'
' Pool sizes below are each EXACTLY GymnasiumSekIFixture.vb's own,
' already-existing per-subject teacher-group count (recomputed by hand
' from AddUniformSubject's SplitInto(classes, classesPerTeacher) calls in
' BuildAssignments - not guessed) - so routing Sek I's own teacher naming
' through this pool (see AddUniformSubject's change) produces the EXACT
' SAME 75 names Sek I already had, net zero effect on Sek I's own
' identity/count. Only KursstufeFixture.vb's naming actually changes
' behavior: for a Kurs in one of these subjects, it now reuses a pool
' member (round-robin, one shared per-subject counter spanning LK+GK
' Kurse of that subject together - deliberately NOT per-kursart, so no
' single shared teacher ever ends up with TWO simultaneous Kursstufe
' Kurse on top of their Sek-I load) instead of minting a fresh
' "T-{subject}-{kursart}-{n}" name.
'
' Subjects deliberately KEPT OUT of this pool, and why:
' - Latein: Sek-I-only, no Kursstufe counterpart at all.
' - Sportprofil/Spanisch/NWT/Informatik (Sek I's 4 literal Profilfach
'   SubjectAssignment adds in BuildAssignments, NOT routed through
'   AddUniformSubject): Kursstufe DOES offer Spanisch/NWT/Informatik as
'   GK2 electives, but pooling these would require changing those 4
'   hardcoded single-teacher lines too - out of scope for this pass to
'   keep Sek I's own construction untouched everywhere except
'   AddUniformSubject. These 3 Kursstufe subjects (plus Kursstufe-only
'   Ethik/Wirtschaft/Psychologie/DarstellendesSpiel) keep minting their
'   own names exactly as before.
' - Geographie (Kursstufe's own term for BW Oberstufen-Erdkunde) is an
'   ALIAS of Sek I's "Erdkunde", not a separate pool entry - see
'   KursstufeSubjectAlias/PoolKeyFor. no_overlap(teacher=...) is subject-
'   agnostic (Solver.vb's ApplyConstraints), so a teacher appearing as
'   "Erdkunde" in one half's Session and "Geographie" in the other's is
'   safe - nothing compares subject strings for that check.
Public Module SharedSchoolPool

    Public ReadOnly SharedSubjectPoolSize As New Dictionary(Of String, Integer) From {
        {"Deutsch", 8}, {"Mathematik", 8}, {"Englisch", 6}, {"Biologie", 5},
        {"Chemie", 2}, {"Physik", 3}, {"Geschichte", 3}, {"Sport", 6},
        {"Religion", 5}, {"Musik", 4}, {"Kunst", 4}, {"Gemeinschaftskunde", 2},
        {"Erdkunde", 5}, {"Franzoesisch", 5}
    }

    Private ReadOnly KursstufeSubjectAlias As New Dictionary(Of String, String) From {
        {"Geographie", "Erdkunde"}
    }

    ''' <summary>Maps a Kursstufe Kurs's own `subject` field to the pool
    ''' key it should look up under (identity for every subject except the
    ''' Geographie/Erdkunde alias). Sek I always calls this with its own
    ''' subject name directly, which is already a valid pool key or absent
    ''' from the pool entirely.</summary>
    Public Function PoolKeyFor(subject As String) As String
        Return KursstufeSubjectAlias.GetValueOrDefault(subject, subject)
    End Function

    Public ReadOnly SharedTeacherPool As Dictionary(Of String, List(Of String)) =
        SharedSubjectPoolSize.ToDictionary(
            Function(kv) kv.Key,
            Function(kv) Enumerable.Range(1, kv.Value).Select(Function(i) $"{kv.Key}-{i}").ToList())

End Module
