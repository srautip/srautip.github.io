' Generalized completeness/recall scoring for LLM-extracted constraints
' against a hand-built ground truth, extracted out of
' LlmExtractionE2ETests.vb (Phase 2) so multiple scenario fixtures can
' reuse the same per-category recall logic instead of each reimplementing
' it. Each Score* function takes a scenario's expected set/dictionary (in
' whatever shape is natural for that category) plus the raw extracted
' constraint list, and returns a 0..1 recall fraction - it does not know
' or care which scenario it's being used for.
Imports System.Text.Json.Nodes

Public Module CompletenessScoring

    ''' <summary>Fraction of `expected` for which `isCovered` holds.
    ''' Vacuously 1.0 if nothing was expected in this category (avoids a
    ''' 0/0 division turning an irrelevant category into a hard
    ''' failure).</summary>
    Public Function RecallFraction(Of T)(expected As ICollection(Of T), isCovered As Func(Of T, Boolean)) As Double
        If expected.Count = 0 Then Return 1.0
        Return expected.Where(isCovered).Count() / CDbl(expected.Count)
    End Function

    Public Function OverallScore(scores As Dictionary(Of String, Double)) As Double
        Return scores.Values.Sum() / scores.Count
    End Function

    Public Function ScoreTeacherSubjectAssignment(expected As HashSet(Of (Cls As String, Subject As String, Teacher As String)),
                                                   extracted As List(Of JsonObject)) As Double
        Dim actual As New HashSet(Of (String, String, String))(
            extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "teacher_subject_assignment").
                Select(Function(c) (JsonHelpers.GetString(c, "class"), JsonHelpers.GetString(c, "subject"), JsonHelpers.GetString(c, "teacher"))))
        Return RecallFraction(expected, Function(x) actual.Contains(x))
    End Function

    Public Function ScoreWeeklyHours(expected As Dictionary(Of (Cls As String, Subject As String), (Hours As Integer, MaxPerDay As Integer)),
                                      extracted As List(Of JsonObject)) As Double
        Dim actual As New Dictionary(Of (String, String), (Integer?, Integer?))
        For Each item In extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "weekly_hours")
            actual((JsonHelpers.GetString(item, "class"), JsonHelpers.GetString(item, "subject"))) =
                (JsonHelpers.GetInt(item, "hours_per_week"), JsonHelpers.GetInt(item, "max_per_day"))
        Next
        Return RecallFraction(expected.Keys.ToList(),
                               Function(key)
                                   If Not actual.ContainsKey(key) Then Return False
                                   Dim actualPair = actual(key)
                                   Dim expectedPair = expected(key)
                                   Return actualPair.Item1 = expectedPair.Hours AndAlso actualPair.Item2 = expectedPair.MaxPerDay
                               End Function)
    End Function

    Public Function ScoreNoOverlap(expected As HashSet(Of (Resource As String, Entity As String)),
                                    extracted As List(Of JsonObject)) As Double
        Dim actual As New HashSet(Of (String, String))(
            extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "no_overlap").
                Select(Function(c) (JsonHelpers.GetString(c, "resource"), JsonHelpers.GetString(c, "entity"))))
        Return RecallFraction(expected, Function(x) actual.Contains(x))
    End Function

    Public Function ScoreRoomRequirement(expected As Dictionary(Of String, List(Of String)),
                                          extracted As List(Of JsonObject)) As Double
        Dim actual As New Dictionary(Of String, List(Of String))
        For Each item In extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "room_requirement")
            actual(JsonHelpers.GetString(item, "subject")) = JsonHelpers.AsStringList(item, "allowed_rooms").OrderBy(Function(s) s).ToList()
        Next
        Return RecallFraction(expected.Keys.ToList(),
                               Function(key) actual.ContainsKey(key) AndAlso actual(key).SequenceEqual(expected(key)))
    End Function

    Public Function ScoreConsecutiveRequired(expected As HashSet(Of (Cls As String, Subject As String, BlockLength As Integer)),
                                              extracted As List(Of JsonObject)) As Double
        Dim actual As New HashSet(Of (String, String, Integer))(
            extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "consecutive_required").
                Select(Function(c) (JsonHelpers.GetString(c, "class"), JsonHelpers.GetString(c, "subject"), JsonHelpers.GetInt(c, "block_length").Value)))
        Return RecallFraction(expected, Function(x) actual.Contains(x))
    End Function

    ''' <summary>Days on which `entry` (a teacher_availability constraint)
    ''' blocks EVERY period - via a day missing from available_days, or
    ''' via unavailable_periods listing all periods of that day. A day
    ''' with only some periods blocked does NOT count: that's a real,
    ''' catchable difference between "unavailable all day" and
    ''' "unavailable one period".</summary>
    Public Function FullyUnavailableDays(entry As JsonObject, allDays As List(Of String), periodsPerDay As Integer) As HashSet(Of String)
        Dim availDaysList = JsonHelpers.AsStringList(entry, "available_days")
        Dim availableDays As New HashSet(Of String)(If(availDaysList.Any(), availDaysList, allDays))
        Dim viaAvailableDays As New HashSet(Of String)(allDays.Where(Function(d) Not availableDays.Contains(d)))

        Dim blockedPeriodsByDay As New Dictionary(Of String, HashSet(Of Integer))
        If entry.ContainsKey("unavailable_periods") AndAlso entry("unavailable_periods") IsNot Nothing Then
            For Each node In entry("unavailable_periods").AsArray()
                Dim p = node.AsObject()
                Dim d = JsonHelpers.GetString(p, "day")
                If Not blockedPeriodsByDay.ContainsKey(d) Then blockedPeriodsByDay(d) = New HashSet(Of Integer)
                blockedPeriodsByDay(d).Add(JsonHelpers.GetInt(p, "period").Value)
            Next
        End If
        Dim viaUnavailablePeriods = blockedPeriodsByDay.Where(Function(kvp) kvp.Value.Count >= periodsPerDay).Select(Function(kvp) kvp.Key)

        viaAvailableDays.UnionWith(viaUnavailablePeriods)
        Return viaAvailableDays
    End Function

    ''' <summary>Sharpened check: not just "is there an entry for this
    ''' teacher", but "does the entry actually block every period on the
    ''' expected days". This is the check that would have caught the real
    ''' Phase-1 bug: an entry that only blocked one period instead of a
    ''' whole day still counted as "covered" under a presence-only
    ''' check.</summary>
    Public Function ScoreTeacherAvailability(expected As HashSet(Of (Teacher As String, Day As String)),
                                              extracted As List(Of JsonObject),
                                              allDays As List(Of String), periodsPerDay As Integer) As Double
        Dim actual As New HashSet(Of (String, String))
        For Each item In extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "teacher_availability")
            For Each d In FullyUnavailableDays(item, allDays, periodsPerDay)
                actual.Add((JsonHelpers.GetString(item, "teacher"), d))
            Next
        Next
        Return RecallFraction(expected, Function(x) actual.Contains(x))
    End Function

    Public Function ScoreForbiddenSlot(expected As HashSet(Of (Entity As String, Day As String, Period As Integer)),
                                        extracted As List(Of JsonObject)) As Double
        Dim actual As New HashSet(Of (String, String, Integer))(
            extracted.Where(Function(c) JsonHelpers.GetString(c, "type") = "forbidden_slot" AndAlso JsonHelpers.GetString(c, "scope") = "class").
                Select(Function(c) (JsonHelpers.GetString(c, "entity"), JsonHelpers.GetString(c, "day"), JsonHelpers.GetInt(c, "period").Value)))
        Return RecallFraction(expected, Function(x) actual.Contains(x))
    End Function

End Module
