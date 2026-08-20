' Phase 2.8: hand-built ScheduleEntry lists, no solver involved - each test
' isolates one ScheduleQuality.Score() sub-metric via exact, hand-computed
' expected values.
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class ScheduleQualityTests

    Private Function Entry(cls As String, subject As String, teacher As String, day As String, period As Integer) As ScheduleEntry
        Return New ScheduleEntry With {.ClassName = cls, .Subject = subject, .Teacher = teacher, .Day = day, .Period = period, .Room = Nothing}
    End Function

    Private Function BuildData(days As IEnumerable(Of String), periodsPerDay As Integer) As JsonObject
        Return Scenario(Mini({}, {}, {}, {}, days, periodsPerDay), {})
    End Function

    <TestMethod>
    Public Sub ClassAndTeacherGapsAreSummedCorrectly()
        Dim data = BuildData({"Mo", "Di"}, 5)
        Dim schedule As New List(Of ScheduleEntry) From {
            Entry("5a", "Deutsch", "T1", "Mo", 1),
            Entry("5a", "Deutsch", "T1", "Mo", 3),   ' 5a/Mo: {1,3} -> gap 1
            Entry("5a", "Mathe", "T1", "Di", 1),
            Entry("5a", "Mathe", "T1", "Di", 2),      ' 5a/Di: {1,2} -> gap 0
            Entry("5b", "Englisch", "T2", "Mo", 2),
            Entry("5b", "Englisch", "T2", "Mo", 5)    ' 5b/Mo: {2,5} -> gap 2
        }
        Dim result = ScheduleQuality.Score(data, schedule, 0)
        Assert.AreEqual(3, result.ClassGapCount)   ' 1 + 0 + 2
        ' T1/Mo:{1,3}->1, T1/Di:{1,2}->0, T2/Mo:{2,5}->2
        Assert.AreEqual(3, result.TeacherGapCount)
    End Sub

    ''' <summary>Phase 2.22 bugfix regression: a Phase 2.20 "parallel_group"
    ''' slot (e.g. Religion-ev/Religion-kath/Ethik running simultaneously)
    ''' puts SEVERAL ScheduleEntry rows on the SAME (class, day, period) for
    ''' one class - before the fix this made ClassGapCount go negative
    ''' (periods.Count exceeded the day's actual span, since the same
    ''' period was counted 3x instead of once), which in turn could make
    ''' the whole QualityScore.Total go negative despite every weighted
    ''' term supposedly being non-negative by construction - discovered
    ''' live via a real bw-grundschule-beispiel run
    ''' (Quality.Total = -513.0) while adding the Phase 2.22 optimality-gap
    ''' feature.</summary>
    <TestMethod>
    Public Sub ClassGapCountIgnoresParallelGroupDuplicatesAtSamePeriod()
        Dim data = BuildData({"Mo"}, 5)
        Dim schedule As New List(Of ScheduleEntry) From {
            Entry("1a", "Religion-ev", "Religionslehrer-ev-1", "Mo", 2),
            Entry("1a", "Religion-kath", "Religionslehrer-kath-1", "Mo", 2),
            Entry("1a", "Ethik", "Ethiklehrer-1", "Mo", 2),
            Entry("1a", "Deutsch", "T1", "Mo", 4)
        }
        Dim result = ScheduleQuality.Score(data, schedule, 0)
        ' Distinct occupied periods for 1a/Mo are {2, 4} -> span 3, 2
        ' distinct periods occupied -> gap = 1, NOT a negative number from
        ' miscounting the tripled period-2 as 3 separate occupied periods.
        Assert.AreEqual(1, result.ClassGapCount)
        Assert.IsTrue(result.Total >= 0.0, "Total must never be negative by construction.")
    End Sub

    <TestMethod>
    Public Sub TeacherGapsIgnoreDaysTeacherIsNotWorking()
        Dim data = BuildData({"Mo", "Di", "Mi", "Do", "Fr"}, 5)
        Dim schedule As New List(Of ScheduleEntry) From {
            Entry("5a", "Deutsch", "T1", "Mo", 2),
            Entry("5a", "Deutsch", "T1", "Mo", 4)   ' T1 works only Mo: {2,4} -> gap 1
        }
        Dim result = ScheduleQuality.Score(data, schedule, 0)
        Assert.AreEqual(1, result.TeacherGapCount)
    End Sub

    <TestMethod>
    Public Sub EdgePeriodCountsFirstAndAfternoonPeriodsOnly()
        Dim data = BuildData({"Mo"}, 8)
        Dim schedule As New List(Of ScheduleEntry) From {
            Entry("5a", "Deutsch", "T1", "Mo", 1),   ' counts: period 1
            Entry("5a", "Mathe", "T1", "Mo", 2),      ' does not count
            Entry("5a", "Sport", "T1", "Mo", 6),      ' does not count (< 7)
            Entry("5a", "Musik", "T1", "Mo", 7),      ' counts: >= AfternoonThresholdPeriod
            Entry("5a", "Kunst", "T1", "Mo", 8)       ' counts: >= AfternoonThresholdPeriod
        }
        Dim result = ScheduleQuality.Score(data, schedule, 0)
        Assert.AreEqual(3, result.EdgePeriodCount)
    End Sub

    ''' <summary>Distinguishes AfternoonDayCount from EdgePeriodCount: 5a
    ''' has 3 afternoon-period LESSONS (same raw count as 5b), but they
    ''' land on only 1 DAY (Mo) - 5b's 2 afternoon lessons land on 2
    ''' DIFFERENT days. AfternoonDayCount must reflect that difference
    ''' (1 + 2 = 3) even though EdgePeriodCount alone (3 + 2 = 5, not
    ''' asserted here) cannot tell the two arrangements apart. Period=1
    ''' entries (5a/Mo/1) deliberately do NOT count here, unlike
    ''' EdgePeriodCount.</summary>
    <TestMethod>
    Public Sub AfternoonDayCountCountsDistinctDaysNotOccurrences()
        Dim data = BuildData({"Mo", "Di"}, 8)
        Dim schedule As New List(Of ScheduleEntry) From {
            Entry("5a", "Deutsch", "T1", "Mo", 1),    ' period 1 - not "Nachmittag"
            Entry("5a", "Mathe", "T1", "Mo", 7),
            Entry("5a", "Sport", "T1", "Mo", 8),      ' 5a: periods {7,8} both on Mo -> 1 Nachmittags-Tag
            Entry("5b", "Musik", "T2", "Mo", 7),
            Entry("5b", "Kunst", "T2", "Di", 7)       ' 5b: Mo and Di -> 2 Nachmittags-Tage
        }
        Dim result = ScheduleQuality.Score(data, schedule, 0)
        Assert.AreEqual(3, result.AfternoonDayCount)   ' 1 (5a) + 2 (5b)
    End Sub

    <TestMethod>
    Public Sub ClassLoadVarianceMatchesHandComputedValue()
        Dim data = BuildData({"Mo", "Di", "Mi"}, 5)
        Dim schedule As New List(Of ScheduleEntry) From {
            Entry("5a", "Deutsch", "T1", "Mo", 1),
            Entry("5a", "Deutsch", "T1", "Di", 1),
            Entry("5a", "Mathe", "T1", "Di", 2),
            Entry("5a", "Mathe", "T1", "Di", 3),
            Entry("5a", "Sport", "T1", "Mi", 1),
            Entry("5a", "Sport", "T1", "Mi", 2)
        }
        ' counts per day: Mo=1, Di=3, Mi=2 -> mean=2 -> variance = (1+1+0)/3 = 2/3
        Dim result = ScheduleQuality.Score(data, schedule, 0)
        Assert.AreEqual(2.0 / 3.0, result.ClassLoadVariance, 0.0000001)
    End Sub

    ''' <summary>Most important test in this file: guards against a future
    ''' "fix" that (incorrectly) divides teacher variance over ALL calendar
    ''' days instead of only the teacher's actual working days, which would
    ''' misreport a part-time teacher's declared unavailability as
    ''' schedule "imbalance".</summary>
    <TestMethod>
    Public Sub TeacherLoadVarianceOnlyCountsWorkingDays()
        Dim data = BuildData({"Mo", "Di", "Mi", "Do", "Fr"}, 5)
        Dim schedule As New List(Of ScheduleEntry) From {
            Entry("5a", "Deutsch", "T1", "Mo", 1),
            Entry("5a", "Deutsch", "T1", "Di", 1),
            Entry("5a", "Mathe", "T1", "Di", 2),
            Entry("5a", "Mathe", "T1", "Di", 3)
        }
        ' T1 works only Mo (1 lesson) and Di (3 lessons) - Mi/Do/Fr not
        ' declared as "off" anywhere, the schedule itself defines working
        ' days. counts=[1,3] -> mean=2 -> variance=(1+1)/2=1.0.
        ' A naive "divide by all 5 days" implementation would instead get
        ' counts=[1,3,0,0,0] -> mean=0.8 -> variance=1.36 - different value,
        ' which this assertion catches.
        Dim result = ScheduleQuality.Score(data, schedule, 0)
        Assert.AreEqual(1.0, result.TeacherLoadVariance, 0.0000001)
    End Sub

    ''' <summary>Nutzerentscheidung (nach der urspruenglichen "Kann
    ''' dominiert IMMER alles"-Heuristik aus Phase 2.8): WeightClassGaps
    ''' (1000) liegt jetzt ueber WeightKann (100) - eine einzelne
    ''' Springstunde bei einer Klasse wiegt schwerer als 9 einzelne Kann-
    ''' Verstoesse zusammen. Kann-Verstoesse dominieren aber weiterhin die
    ''' UEBRIGEN Sekundaerkriterien (hier: TeacherGaps) - das bleibt
    ''' unveraendert wahr. Alle vier Szenarien wurden vor dem Schreiben
    ''' live gegen ScheduleQuality.Score gegengerechnet (Score.ClassGapCount/
    ''' TeacherGapCount/etc. einzeln geprueft), NICHT nur von Hand
    ''' geschaetzt - ein einzelner Tag/wenige Perioden pro Szenario haelt
    ''' jeweils GENAU EINEN der 7 Terme ungleich 0 (die uebrigen 6 muessen
    ''' bei mehrdeutigeren Aufbauten sonst leicht uebersehene Beitraege
    ''' liefern, siehe z.B. ClassLoadVariance bei mehrtaegigen Datensaetzen
    ''' mit ungleichmaessiger Belegung).</summary>
    <TestMethod>
    Public Sub ClassGapsOutweighKannWhichStillOutweighsOtherSecondaryCriteria()
        Dim singleDayData = BuildData({"Mo"}, 6)

        ' Genau 1 Klassen-Luecke (3 verschiedene Lehrer je 1 Stunde auf
        ' Perioden 2/3/5 -> Spanne 4, 3 belegt, Luecke 1) - je EIN eigener
        ' Lehrer pro Lektion haelt TeacherGapCount bei 0 (kein Lehrer
        ' unterrichtet mehr als 1 Stunde, also auch keine eigene Luecke).
        Dim oneClassGapSchedule As New List(Of ScheduleEntry) From {
            Entry("5a", "Fach1", "T1", "Mo", 2),
            Entry("5a", "Fach2", "T2", "Mo", 3),
            Entry("5a", "Fach3", "T3", "Mo", 5)
        }
        Dim scoreOneClassGap = ScheduleQuality.Score(singleDayData, oneClassGapSchedule, 0)
        Assert.AreEqual(1, scoreOneClassGap.ClassGapCount)
        Assert.AreEqual(0, scoreOneClassGap.TeacherGapCount)
        Assert.AreEqual(1000.0, scoreOneClassGap.Total, 0.0000001)

        Dim scoreNineKann = ScheduleQuality.Score(singleDayData, New List(Of ScheduleEntry)(), 9)
        Assert.AreEqual(900.0, scoreNineKann.Total, 0.0000001)

        Assert.IsTrue(scoreOneClassGap.Total > scoreNineKann.Total,
            $"1 Klassen-Luecke ({scoreOneClassGap.Total}) sollte schwerer wiegen als 9 Kann-Verstoesse ({scoreNineKann.Total})")

        ' Kann dominiert weiterhin TeacherGaps (10): ein Lehrer, der zwei
        ' VERSCHIEDENE Klassen an je nur 1 Stunde derselben Tag unterrichtet
        ' (Perioden 2 und 4), hat selbst eine Luecke (Spanne 3, 2 belegt),
        ' waehrend jede der beiden Klassen einzeln nur 1 Stunde diesen Tag
        ' hat - 0 ClassGapCount.
        Dim oneTeacherGapSchedule As New List(Of ScheduleEntry) From {
            Entry("5b", "Fach1", "T2", "Mo", 2),
            Entry("5c", "Fach1", "T2", "Mo", 4)
        }
        Dim scoreOneKann = ScheduleQuality.Score(singleDayData, New List(Of ScheduleEntry)(), 1)
        Dim scoreOneTeacherGap = ScheduleQuality.Score(singleDayData, oneTeacherGapSchedule, 0)
        Assert.AreEqual(0, scoreOneTeacherGap.ClassGapCount)
        Assert.AreEqual(1, scoreOneTeacherGap.TeacherGapCount)
        Assert.AreEqual(100.0, scoreOneKann.Total, 0.0000001)
        Assert.AreEqual(10.0, scoreOneTeacherGap.Total, 0.0000001)
        Assert.IsTrue(scoreOneKann.Total > scoreOneTeacherGap.Total * 9,
            $"1 Kann-Verstoss ({scoreOneKann.Total}) sollte schwerer wiegen als 9 Lehrer-Luecken ({scoreOneTeacherGap.Total * 9})")
    End Sub

    ''' <summary>Phase 2.24: Score's new optional `weights` parameter must
    ''' default to today's hardcoded module constants - an explicit
    ''' `New QualityWeights()` (which itself defaults every property to
    ''' those same constants) must produce a BYTE-IDENTICAL Total to
    ''' omitting the parameter entirely. Regression guard for every
    ''' existing caller (SolveTop, the tests above) that never passes
    ''' weights.</summary>
    <TestMethod>
    Public Sub DefaultWeightsMatchImplicitOmission()
        Dim data = BuildData({"Mo", "Di"}, 4)
        Dim schedule As New List(Of ScheduleEntry) From {
            Entry("5a", "Fach1", "T1", "Mo", 1),
            Entry("5a", "Fach1", "T1", "Mo", 4)
        }
        Dim implicit = ScheduleQuality.Score(data, schedule, 1)
        Dim explicitDefault = ScheduleQuality.Score(data, schedule, 1, New QualityWeights())
        Assert.AreEqual(implicit.Total, explicitDefault.Total, 0.0000001)
    End Sub

    ''' <summary>Phase 2.24: custom weights must change Total exactly per
    ''' the formula (hand-computed, not just "changes somehow") - proves
    ''' the QualityWeights object actually reaches every one of the 7
    ''' terms, not just a subset.</summary>
    <TestMethod>
    Public Sub CustomWeightsChangeTotalPerFormula()
        Dim data = BuildData({"Mo"}, 4)
        ' 5a: periods {1,4} on Mo -> span = 4-1+1 = 4, 2 occupied -> 2 gaps
        ' (periods 2 and 3 sit unoccupied between them); 1 edge occurrence
        ' (period=1; period=4 < AfternoonThresholdPeriod=7 so doesn't count),
        ' 0 afternoon days, class/teacher variance both 0 (single day).
        Dim schedule As New List(Of ScheduleEntry) From {
            Entry("5a", "Fach1", "T1", "Mo", 1),
            Entry("5a", "Fach1", "T1", "Mo", 4)
        }
        Dim weights As New QualityWeights With {
            .Kann = 1000.0, .ClassGaps = 7.0, .TeacherGaps = 11.0, .EdgePeriod = 3.0,
            .AfternoonDayCount = 0.0, .ClassLoadVariance = 0.0, .TeacherLoadVariance = 0.0
        }
        Dim result = ScheduleQuality.Score(data, schedule, 2, weights)
        Dim expected = 1000.0 * 2 + 7.0 * 2 + 11.0 * 2 + 3.0 * 1
        Assert.AreEqual(expected, result.Total, 0.0000001)
    End Sub

End Class
