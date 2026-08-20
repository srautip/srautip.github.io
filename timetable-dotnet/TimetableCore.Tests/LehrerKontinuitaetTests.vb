' Phase 2.14: deterministische Tests fuer die Lehrerkontinuitaet-Mechanik
' (LehrerKontinuitaetFixture.vb) - kein LLM/Ollama involviert, alles hier
' ist eine pruefbare Tatsache ueber die Fixture-Konstruktion.
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class LehrerKontinuitaetTests

    Private Function Entry(cls As String, subject As String, teacher As String, day As String, period As Integer) As ScheduleEntry
        Return New ScheduleEntry With {.ClassName = cls, .Subject = subject, .Teacher = teacher, .Day = day, .Period = period, .Room = Nothing}
    End Function

    ''' <summary>Handgebaute, kleine Schedule ueber 2 Zuege x 2 Faecher -
    ''' prueft, dass DeriveVorjahresTeacherMap exakt nach (Zug,Fach)
    ''' gruppiert und pro Gruppe die einzige vorkommende Lehrkraft
    ''' zurueckliefert, ohne Solve()-Abhaengigkeit.</summary>
    <TestMethod>
    Public Sub DeriveVorjahresTeacherMapGroupsCorrectlyByZugAndSubject()
        Dim schedule As New List(Of ScheduleEntry) From {
            Entry("5a", "Deutsch", "Deutsch-5a", "Mo", 1),
            Entry("5a", "Deutsch", "Deutsch-5a", "Mo", 2),
            Entry("5a", "Mathematik", "Mathematik-5a", "Di", 1),
            Entry("5b", "Deutsch", "Deutsch-5b", "Mo", 1),
            Entry("5b", "Mathematik", "Mathematik-5b", "Di", 1)
        }
        Dim map = LehrerKontinuitaetFixture.DeriveVorjahresTeacherMap(schedule)

        Assert.AreEqual(4, map.Count)
        Assert.AreEqual("Deutsch-5a", map(("a", "Deutsch")))
        Assert.AreEqual("Mathematik-5a", map(("a", "Mathematik")))
        Assert.AreEqual("Deutsch-5b", map(("b", "Deutsch")))
        Assert.AreEqual("Mathematik-5b", map(("b", "Mathematik")))
    End Sub

    ''' <summary>Ein (Zug,Fach) mit zwei verschiedenen Lehrern waere ein
    ''' echter Solver-/Fixture-Bug (die "genau ein Lehrer pro Zug/Fach"-
    ''' Invariante gilt per Konstruktion immer) - DeriveVorjahresTeacherMap
    ''' muss das laut melden statt still einen der beiden zu waehlen.</summary>
    <TestMethod>
    Public Sub DeriveVorjahresTeacherMapThrowsOnInconsistentTeacherPerZugSubject()
        Dim schedule As New List(Of ScheduleEntry) From {
            Entry("5a", "Deutsch", "Deutsch-5a", "Mo", 1),
            Entry("5a", "Deutsch", "EIN-ANDERER-LEHRER", "Mo", 2)
        }
        Assert.ThrowsException(Of InvalidOperationException)(
            Sub() LehrerKontinuitaetFixture.DeriveVorjahresTeacherMap(schedule))
    End Sub

    ''' <summary>Kleines, eigenstaendiges 2-Zug/2-Fach-Szenario (bewusst
    ''' NICHT die volle LehrerKontinuitaetFixture, um unabhaengig von deren
    ''' konkreten Zahlen per Hand pruefen zu koennen): Kl.5 loesen, Map
    ''' ableiten, Kl.6 fuer dieselben 2 fortbestehenden Faecher bauen+
    ''' loesen, dann PER HAND bestaetigen, dass Kl.6s teacher_subject_
    ''' assignment-Lehrer exakt Kl.5s entspricht - Fach fuer Fach, Zug
    ''' fuer Zug.</summary>
    <TestMethod>
    Public Sub Klasse6ReusesExactKlasse5TeacherForPersistingSubjects()
        Dim days = New List(Of String) From {"Mo", "Di"}
        Dim klasse5Assignments = New List(Of SubjectAssignment) From {
            New SubjectAssignment("Deutsch", "Deutsch-5a", New List(Of String) From {"5a"}, 2, 1, Nothing, Nothing),
            New SubjectAssignment("Mathematik", "Mathematik-5a", New List(Of String) From {"5a"}, 2, 1, Nothing, Nothing),
            New SubjectAssignment("Deutsch", "Deutsch-5b", New List(Of String) From {"5b"}, 2, 1, Nothing, Nothing),
            New SubjectAssignment("Mathematik", "Mathematik-5b", New List(Of String) From {"5b"}, 2, 1, Nothing, Nothing)
        }
        Dim klasse5Data = AssignmentScenarioBuilder.BuildScenario(New List(Of String) From {"5a", "5b"}, days, 4, klasse5Assignments)
        Dim klasse5Result = Solver.Solve(klasse5Data, timeLimitS:=30)
        Assert.IsTrue(klasse5Result.Status = CpSolverStatus.Optimal OrElse klasse5Result.Status = CpSolverStatus.Feasible)

        Dim vorjahrMap = LehrerKontinuitaetFixture.DeriveVorjahresTeacherMap(klasse5Result.Schedule)

        Dim klasse6Assignments = New List(Of SubjectAssignment) From {
            New SubjectAssignment("Deutsch", vorjahrMap(("a", "Deutsch")), New List(Of String) From {"6a"}, 2, 1, Nothing, Nothing),
            New SubjectAssignment("Mathematik", vorjahrMap(("a", "Mathematik")), New List(Of String) From {"6a"}, 2, 1, Nothing, Nothing),
            New SubjectAssignment("Deutsch", vorjahrMap(("b", "Deutsch")), New List(Of String) From {"6b"}, 2, 1, Nothing, Nothing),
            New SubjectAssignment("Mathematik", vorjahrMap(("b", "Mathematik")), New List(Of String) From {"6b"}, 2, 1, Nothing, Nothing)
        }
        Dim klasse6Data = AssignmentScenarioBuilder.BuildScenario(New List(Of String) From {"6a", "6b"}, days, 4, klasse6Assignments)
        Dim klasse6Result = Solver.Solve(klasse6Data, timeLimitS:=30)
        Assert.IsTrue(klasse6Result.Status = CpSolverStatus.Optimal OrElse klasse6Result.Status = CpSolverStatus.Feasible)
        Assert.AreEqual(0, Verifier.VerifySchedule(klasse6Data, klasse6Result.Schedule).Count)

        ' Per Hand bestaetigt: Kl.6s teacher_subject_assignment-Eintraege
        ' fuer "6a"/"6b" nennen exakt dieselben Lehrer wie Kl.5s "5a"/"5b".
        Dim kl6Constraints = JsonHelpers.Constraints(klasse6Data).
            Where(Function(c) JsonHelpers.GetString(c, "type") = "teacher_subject_assignment").ToList()
        Assert.AreEqual("Deutsch-5a", kl6Constraints.Single(Function(c) JsonHelpers.GetString(c, "class") = "6a" AndAlso JsonHelpers.GetString(c, "subject") = "Deutsch").Item("teacher").ToString())
        Assert.AreEqual("Mathematik-5a", kl6Constraints.Single(Function(c) JsonHelpers.GetString(c, "class") = "6a" AndAlso JsonHelpers.GetString(c, "subject") = "Mathematik").Item("teacher").ToString())
        Assert.AreEqual("Deutsch-5b", kl6Constraints.Single(Function(c) JsonHelpers.GetString(c, "class") = "6b" AndAlso JsonHelpers.GetString(c, "subject") = "Deutsch").Item("teacher").ToString())
        Assert.AreEqual("Mathematik-5b", kl6Constraints.Single(Function(c) JsonHelpers.GetString(c, "class") = "6b" AndAlso JsonHelpers.GetString(c, "subject") = "Mathematik").Item("teacher").ToString())
    End Sub

    ''' <summary>Nutzt die echte LehrerKontinuitaetFixture (nicht ein
    ''' Mini-Szenario) fuer die 2. Fremdsprache [Franzoesisch/Latein] -
    ''' das einzige in Kl.6 NEUE Fach, fuer das per Konstruktion kein
    ''' Vorjahreslehrer existieren kann. Bestaetigt: (a) jeder gemintete
    ''' Name ist garantiert von JEDEM Kl.5-Namen verschieden [genau der
    ''' Fall, den ein Bug faelschlich einen Kl.5-Namen "zufaellig
    ''' wiederverwenden" lassen koennte], (b) der Name ist deterministisch/
    ''' stabil [zweimaliger Bau liefert identische Namen].</summary>
    <TestMethod>
    Public Sub Klasse6MintsFreshDistinctTeacherForSubjectNewInKlasse6()
        Dim klasse5Data = LehrerKontinuitaetFixture.BuildKlasse5Scenario()
        Dim klasse5Teachers = New HashSet(Of String)(JsonHelpers.AsStringList(JsonHelpers.Entities(klasse5Data), "teachers"))

        Dim klasse5Result = Solver.Solve(klasse5Data, timeLimitS:=30)
        Assert.IsTrue(klasse5Result.Status = CpSolverStatus.Optimal OrElse klasse5Result.Status = CpSolverStatus.Feasible)
        Dim vorjahrMap = LehrerKontinuitaetFixture.DeriveVorjahresTeacherMap(klasse5Result.Schedule)

        Dim klasse6AssignmentsA = LehrerKontinuitaetFixture.BuildKlasse6Assignments(vorjahrMap)
        Dim klasse6AssignmentsB = LehrerKontinuitaetFixture.BuildKlasse6Assignments(vorjahrMap)

        Dim neueFaecher = New HashSet(Of String) From {"Franzoesisch", "Latein"}
        Dim neuGemintetA = klasse6AssignmentsA.Where(Function(a) neueFaecher.Contains(a.Subject)).ToList()
        Dim neuGemintetB = klasse6AssignmentsB.Where(Function(a) neueFaecher.Contains(a.Subject)).ToList()

        Assert.AreEqual(5, neuGemintetA.Count, "Erwartete 5 neue 2.FS-Zuordnungen (1 pro Zug).")
        For Each a In neuGemintetA
            Assert.IsFalse(klasse5Teachers.Contains(a.Teacher),
                $"'{a.Teacher}' (Fach {a.Subject}) darf unter keinem Kl.5-Namen bereits vorkommen.")
        Next

        ' Stabilitaet: zweimaliger Bau mit derselben vorjahrMap liefert identische Namen.
        For i = 0 To neuGemintetA.Count - 1
            Assert.AreEqual(neuGemintetA(i).Teacher, neuGemintetB(i).Teacher)
        Next
    End Sub

End Class
