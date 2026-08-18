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

    <TestMethod>
    Public Sub KannViolationsDominateSecondaryCriteria()
        Dim data = BuildData({"Mo", "Di", "Mi", "Do", "Fr"}, 10)

        ' Deliberately bad schedule: several classes/teachers, each with
        ' widely-spread periods on several days, to rack up large
        ' gap/edge/variance sub-scores - but zero Kann violations.
        Dim badSchedule As New List(Of ScheduleEntry)
        For Each cls In {"5a", "5b", "5c"}
            For Each d In {"Mo", "Di"}
                badSchedule.Add(Entry(cls, "Fach1", cls & "-Lehrer", d, 1))
                badSchedule.Add(Entry(cls, "Fach2", cls & "-Lehrer", d, 10))
            Next
        Next
        Dim scoreBad = ScheduleQuality.Score(data, badSchedule, 0)

        ' "Perfect" schedule (empty - no gaps/edges/variance possible) but
        ' with a single Kann violation.
        Dim scoreGood = ScheduleQuality.Score(data, New List(Of ScheduleEntry)(), 1)

        Assert.IsTrue(scoreBad.Total < scoreGood.Total,
            $"Bad-secondary/0-Kann Total ({scoreBad.Total}) should be less than perfect-secondary/1-Kann Total ({scoreGood.Total})")
        Assert.AreEqual(ScheduleQuality.WeightKann, scoreGood.Total)
    End Sub

End Class
