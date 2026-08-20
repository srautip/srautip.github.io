' Phase 2.11 (Nachtrag): combines GymnasiumSekIFixture (Sek I, Kl. 5-10)
' and KursstufeFixture (Kursstufe, Kl. 11/12) into ONE end-to-end
' benchmark run - closing the gap GymnasiumSekIFixture's own header
' comment explicitly deferred ("real Kursstufe... faking a Kursstufen-
' Klasse here would misrepresent how the real Oberstufe works... only
' approached, not matched exactly, by this Sek-I-only scenario"). This is
' the full Gustav-Stresemann-Gymnasium-inspired school (Kl. 5-12) Phase
' 2.10 originally set out to approximate ("~800 Schueler, ~80
' Lehrkraefte"), now solvable end-to-end since Phase 2.11 added Kursstufe
' support.
'
' IMPORTANT: same SYNTHETIC-data disclaimer as both source fixtures - not
' the real school's actual timetable, staff list, or student data.
'
' Solved as two SEPARATE Solver calls (Solve() for the class-based Sek-I
' part, SolveKursstufe() for the Kursstufe part) rather than one merged
' JSON scenario: the two fixtures use disjoint entity namespaces
' (classes/teachers/rooms vs. kurse/schienen/kurswahl), and Solver.vb/
' Kursblockung.vb were never designed to cross-reference a shared
' teacher/room pool between a class-based and a Kurs-based part of the
' same JSON document - merging them would add complexity without testing
' any real constraint (this project has no notion of "this Sek-I teacher
' ALSO teaches a Kursstufe-Kurs" anywhere). Running both halves together
' in one test is the meaningful "complete school" proof: the full Kl.5-12
' structure solves end-to-end - not that the two halves compete for
' physical resources, which is outside this project's current data model
' (same documented scope as the rest of Phase 2.11).
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class GsgCompleteScenarioTests

    ''' <summary>Live-measured: the Sek-I half alone takes ~93s (see
    ''' RealSchoolFixtureTests.GymnasiumSekISolvesAndVerifiesClean), the
    ''' Kursstufe half &lt;1s (see
    ''' RealSchoolFixtureTests.KursstufeSolvesAndVerifiesClean) - solved
    ''' here again (this test is self-contained, MSTest tests don't share
    ''' state) with the same generous 150s budget for the Sek-I half.</summary>
    <TestMethod>
    Public Sub CompleteGsgScenarioSolvesEndToEnd()
        Dim sekI = GymnasiumSekIFixture.BuildGymnasiumSekIScenario()
        Dim sekIResult = Solver.Solve(sekI, timeLimitS:=150)
        Assert.IsTrue(sekIResult.Status = CpSolverStatus.Optimal OrElse sekIResult.Status = CpSolverStatus.Feasible,
            $"Sek I: {Solver.StatusName(sekIResult.Status)}")
        Dim sekIViolations = Verifier.VerifySchedule(sekI, sekIResult.Schedule)
        Assert.AreEqual(0, sekIViolations.Count, String.Join(vbLf, sekIViolations))

        Dim kursstufe = KursstufeFixture.BuildKursstufeScenario()
        Dim kursstufeResult = Solver.SolveKursstufe(kursstufe, timeLimitS:=60)
        Assert.IsTrue(
            kursstufeResult.RaumzuordnungStatus = CpSolverStatus.Optimal OrElse kursstufeResult.RaumzuordnungStatus = CpSolverStatus.Feasible,
            $"Kursblockung={kursstufeResult.KursblockungStatus}, Schienenraster={kursstufeResult.SchienenrasterStatus}, " &
            $"Raumzuordnung={kursstufeResult.RaumzuordnungStatus}")
        Assert.IsNotNull(kursstufeResult.Schedule)

        Dim sekIEnt = JsonHelpers.Entities(sekI)
        Dim kursstufeEnt = JsonHelpers.Entities(kursstufe)
        Dim sekIClasses = JsonHelpers.AsStringList(sekIEnt, "classes").Count
        Dim sekITeachers = JsonHelpers.AsStringList(sekIEnt, "teachers").Count
        Dim kursstufeTeachers = JsonHelpers.AsStringList(kursstufeEnt, "teachers").Count
        Dim kursstufeStudents = JsonHelpers.Constraints(kursstufe).
            Where(Function(c) JsonHelpers.GetString(c, "type") = "kurswahl").
            Sum(Function(c) JsonHelpers.GetInt(c, "student_count").GetValueOrDefault())
        Dim sekIStudents = sekIClasses * 25 ' ~25 Schueler/Klasse, siehe GymnasiumSekIFixture-Kommentar

        Console.WriteLine(
            $"Komplettes GSG-Szenario (Kl. 5-12): Sek I = {sekIClasses} Klassen / {sekITeachers} Lehrkraefte " &
            $"(~{sekIStudents} Schueler); Kursstufe = {kursstufeTeachers} Lehrkraefte / {kursstufeStudents} Schueler " &
            $"(ueber die Wahlprofile). Gesamt ~{sekIStudents + kursstufeStudents} Schueler, " &
            $"{sekITeachers + kursstufeTeachers} Lehrkraefte.")
    End Sub

End Class
