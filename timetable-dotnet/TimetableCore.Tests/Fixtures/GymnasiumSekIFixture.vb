' Phase 2.10: a full-size Gymnasium Sekundarstufe-I scenario (Kl. 5-10
' only) for Solve()/SolveTop benchmarking at real school scale, loosely
' inspired by the public profile of the Gustav-Stresemann-Gymnasium
' Fellbach (Fellbach-Schmiden, real-world abbreviation "GSG") - a real
' G8 Gymnasium whose documented profile system (Sportprofil,
' Sprachenprofil with a 3rd foreign language, NWT-Profil, IMP-Profil,
' chosen from grade 7/8 onward) this scenario's Profilfach mechanism
' mirrors.
'
' IMPORTANT: this is SYNTHETIC, plausible test data following typical
' Baden-Wuerttemberg G8-Gymnasium Kontingentstundentafel structure - it is
' NOT the real school's actual timetable, staff list, or student data.
'
' Deliberately scoped to Sekundarstufe I (Kl. 5-10) only: this project's
' data model is strictly class-based (Session = class+subject+teacher),
' with no concept of individual Kurswahl - real Kursstufe (Kl. 11/12)
' students take individual courses, not fixed classes, so faking a
' "Kursstufen-Klasse" here would misrepresent how the real Oberstufe
' works rather than simplify it. The user-provided "~800 Schueler/~80
' Lehrkraefte" therefore is only approached, not matched exactly, by
' this Sek-I-only scenario: 5 Zuege x 6 Jahrgaenge x ~25 Schueler/Klasse
' = ~750 Schueler, and the built scenario resolves to 75 distinct
' teachers (verified via Solve() - see RealSchoolFixtureTests.vb) - both
' land close to the real figures despite the deliberately excluded
' Kursstufe.
'
' Like GrundschuleGrossFixture, this has NO prompt text and NO
' Expected*/CompletenessReport functions - pure Solve()/SolveTop input,
' not an LLM-extraction test fixture.
Imports System.Text.Json.Nodes

Public Module GymnasiumSekIFixture

    Private ReadOnly Grades As New List(Of Integer) From {5, 6, 7, 8, 9, 10}
    Private ReadOnly ClassLetters As New List(Of String) From {"a", "b", "c", "d", "e"}
    Private ReadOnly Classes As List(Of String) =
        (From g In New List(Of Integer) From {5, 6, 7, 8, 9, 10}
         From letter In New List(Of String) From {"a", "b", "c", "d", "e"}
         Select $"{g}{letter}").ToList()
    Private ReadOnly Days As New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}
    Private Const PeriodsPerDay As Integer = 8

    Private Function GradeOf(cls As String) As Integer
        Return Integer.Parse(cls.Substring(0, cls.Length - 1))
    End Function

    Private Function LetterOf(cls As String) As String
        Return cls.Substring(cls.Length - 1)
    End Function

    Private Function ClassesInGrades(grades As IEnumerable(Of Integer)) As List(Of String)
        Return Classes.Where(Function(c) grades.Contains(GradeOf(c))).ToList()
    End Function

    Private Function SplitInto(items As List(Of String), groupSize As Integer) As List(Of List(Of String))
        Dim result As New List(Of List(Of String))
        Dim current As New List(Of String)
        For Each item In items
            current.Add(item)
            If current.Count = groupSize Then
                result.Add(current)
                current = New List(Of String)
            End If
        Next
        If current.Count > 0 Then result.Add(current)
        Return result
    End Function

    ''' <summary>Adds one SubjectAssignment per teacher-group for `subject`
    ''' across `classes`, splitting into groups of roughly
    ''' `classesPerTeacher` classes each (one teacher per group). Callers
    ''' with grade-varying weekly hours (Deutsch, Mathematik, ...) call
    ''' this once per distinct hour-value/grade-band. Each teacher here
    ''' covers exactly one subject - a simplification of real Gymnasium
    ''' teachers' typical 2-subject combinations, made to keep this
    ''' scenario's construction tractable.
    '''
    ''' `teacherCounters` MUST be shared across every call for the same
    ''' subject (i.e. passed in from BuildAssignments, not a fresh
    ''' Dictionary per call) - a per-call-local counter would restart at 1
    ''' each time, causing two different grade-bands' "Deutsch-1" to
    ''' collide into literally the SAME teacher name and therefore the
    ''' SAME no_overlap(teacher) constraint, double-booking that teacher
    ''' across both bands' classes at once. This is exactly the bug an
    ''' earlier version of this fixture had (root-caused via bisection:
    ''' Solve() was Infeasible only with no_overlap(teacher) present).</summary>
    Private Sub AddUniformSubject(result As List(Of SubjectAssignment), teacherCounters As Dictionary(Of String, Integer),
                                   subject As String, classes As List(Of String),
                                   hours As Integer, maxPerDay As Integer, classesPerTeacher As Integer,
                                   Optional blockLength As Integer? = Nothing, Optional rooms As List(Of String) = Nothing)
        Dim groups = SplitInto(classes, classesPerTeacher)
        Dim nextIndex = teacherCounters.GetValueOrDefault(subject, 0)
        For i = 0 To groups.Count - 1
            nextIndex += 1
            result.Add(New SubjectAssignment(subject, $"{subject}-{nextIndex}", groups(i), hours, maxPerDay, blockLength, rooms))
        Next
        teacherCounters(subject) = nextIndex
    End Sub

    Private Function BuildAssignments() As List(Of SubjectAssignment)
        Dim result As New List(Of SubjectAssignment)
        Dim teacherCounters As New Dictionary(Of String, Integer)

        ' Deutsch, Mathematik, Englisch: durchgehend Kl.5-10, Wochenstunden
        ' sinken zur Mittelstufe hin leicht (typisches BW-G8-Muster).
        AddUniformSubject(result, teacherCounters, "Deutsch", ClassesInGrades({5, 6, 7}), 4, 2, 4)
        AddUniformSubject(result, teacherCounters, "Deutsch", ClassesInGrades({8, 9, 10}), 3, 2, 4)

        AddUniformSubject(result, teacherCounters, "Mathematik", ClassesInGrades({5, 6, 7, 8}), 4, 2, 4)
        AddUniformSubject(result, teacherCounters, "Mathematik", ClassesInGrades({9, 10}), 3, 2, 4)

        AddUniformSubject(result, teacherCounters, "Englisch", ClassesInGrades({5, 6}), 4, 2, 5)
        AddUniformSubject(result, teacherCounters, "Englisch", ClassesInGrades({7, 8, 9, 10}), 3, 2, 5)

        ' 2. Fremdsprache ab Kl.6, je Klasse FEST auf Franzoesisch ODER
        ' Latein - je Jahrgang werden die 5 Zuege 3:2 aufgeteilt (a/c/e ->
        ' Franzoesisch, b/d -> Latein), mirrort GSGs reale 2.FS-Wahl ab
        ' Klasse 6.
        Dim fs2Grades = New List(Of Integer) From {6, 7, 8, 9, 10}
        Dim fs2Classes = ClassesInGrades(fs2Grades)
        Dim franzoesisch = fs2Classes.Where(Function(c) {"a", "c", "e"}.Contains(LetterOf(c))).ToList()
        Dim latein = fs2Classes.Where(Function(c) {"b", "d"}.Contains(LetterOf(c))).ToList()
        For Each grade In fs2Grades
            Dim hours = If(grade = 6, 4, 3)
            AddUniformSubject(result, teacherCounters, "Franzoesisch", franzoesisch.Where(Function(c) GradeOf(c) = grade).ToList(), hours, 2, 5)
            AddUniformSubject(result, teacherCounters, "Latein", latein.Where(Function(c) GradeOf(c) = grade).ToList(), hours, 2, 5)
        Next

        AddUniformSubject(result, teacherCounters, "Geschichte", ClassesInGrades({7, 8, 9, 10}), 2, 1, 7)
        AddUniformSubject(result, teacherCounters, "Erdkunde", ClassesInGrades(Grades), 2, 1, 7)
        AddUniformSubject(result, teacherCounters, "Gemeinschaftskunde", ClassesInGrades({8, 9, 10}), 2, 1, 8)
        AddUniformSubject(result, teacherCounters, "Biologie", ClassesInGrades(Grades), 2, 1, 7)
        ' Physik/Chemie als Doppelstunden-Praktikum (block_length=2, daher
        ' max_per_day=2 - die eine wöchentliche Doppelstunde braucht 2
        ' Perioden am selben Tag, ein max_per_day=1 wäre ein Widerspruch).
        AddUniformSubject(result, teacherCounters, "Physik", ClassesInGrades({7, 8, 9, 10}), 2, 2, 7,
                           blockLength:=2, rooms:=New List(Of String) From {"NaWi1", "NaWi2", "NaWi3"})
        AddUniformSubject(result, teacherCounters, "Chemie", ClassesInGrades({8, 9, 10}), 2, 2, 8,
                           blockLength:=2, rooms:=New List(Of String) From {"NaWi1", "NaWi2", "NaWi3"})

        AddUniformSubject(result, teacherCounters, "Sport", ClassesInGrades({5, 6}), 3, 1, 5,
                           rooms:=New List(Of String) From {"Turnhalle1", "Turnhalle2", "Turnhalle3"})
        AddUniformSubject(result, teacherCounters, "Sport", ClassesInGrades({7, 8, 9, 10}), 2, 1, 5,
                           rooms:=New List(Of String) From {"Turnhalle1", "Turnhalle2", "Turnhalle3"})

        AddUniformSubject(result, teacherCounters, "Musik", ClassesInGrades({5, 6}), 2, 1, 7, rooms:=New List(Of String) From {"Musikraum"})
        AddUniformSubject(result, teacherCounters, "Musik", ClassesInGrades({7, 8}), 1, 1, 7, rooms:=New List(Of String) From {"Musikraum"})
        AddUniformSubject(result, teacherCounters, "Kunst", ClassesInGrades({5}), 2, 1, 7, rooms:=New List(Of String) From {"Kunstraum"})
        AddUniformSubject(result, teacherCounters, "Kunst", ClassesInGrades({6, 7, 8}), 1, 1, 7, rooms:=New List(Of String) From {"Kunstraum"})

        AddUniformSubject(result, teacherCounters, "Religion", ClassesInGrades(Grades), 2, 1, 7)

        ' Profilfach ab Kl.8 (GSGs reales Profilsystem: Sportprofil /
        ' Sprachenprofil [3. FS Spanisch] / NWT-Profil / IMP-Profil) - der
        ' Zug-Buchstabe legt das Profil fest, konsistent ueber Kl.8-10
        ' (a+e -> Sportprofil, b -> Sprachenprofil, c -> NWT-Profil,
        ' d -> IMP-Profil).
        Dim profilClasses = ClassesInGrades({8, 9, 10})
        Dim sportProfil = profilClasses.Where(Function(c) {"a", "e"}.Contains(LetterOf(c))).ToList()
        Dim sprachenProfil = profilClasses.Where(Function(c) LetterOf(c) = "b").ToList()
        Dim nwtProfil = profilClasses.Where(Function(c) LetterOf(c) = "c").ToList()
        Dim impProfil = profilClasses.Where(Function(c) LetterOf(c) = "d").ToList()
        result.Add(New SubjectAssignment("Sportprofil", "SportProfil-1", sportProfil, 3, 1, Nothing,
                                          New List(Of String) From {"Turnhalle1", "Turnhalle2", "Turnhalle3"}))
        result.Add(New SubjectAssignment("Spanisch", "SprachenProfil-1", sprachenProfil, 3, 1, Nothing, Nothing))
        result.Add(New SubjectAssignment("NWT", "NWTProfil-1", nwtProfil, 3, 1, Nothing,
                                          New List(Of String) From {"NaWi1", "NaWi2", "NaWi3"}))
        result.Add(New SubjectAssignment("Informatik", "IMPProfil-1", impProfil, 3, 1, Nothing,
                                          New List(Of String) From {"PC-Raum"}))

        Return result
    End Function

    Public ReadOnly GymnasiumSekIAssignments As List(Of SubjectAssignment) = BuildAssignments()

    Public Function BuildGymnasiumSekIScenario() As JsonObject
        Return AssignmentScenarioBuilder.BuildScenario(Classes, Days, PeriodsPerDay, GymnasiumSekIAssignments)
    End Function

End Module
