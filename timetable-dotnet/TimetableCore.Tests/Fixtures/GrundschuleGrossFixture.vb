' Phase 2.10: a full-size Grundschule scenario for Solve()/SolveTop
' benchmarking at real school scale, loosely inspired by the public
' profile of the Anne-Frank-Schule Fellbach (Fellbach-Schmiden) - a real
' Ganztagesschule with a sport/movement focus that itself names
' "Dreizuegigkeit" (3 parallel classes per grade) as its target.
'
' IMPORTANT: this is SYNTHETIC, plausible test data following typical
' Baden-Wuerttemberg Grundschule Kontingentstundentafel structure - it is
' NOT the real school's actual timetable, staff list, or student data.
' The real AFS targets 3 classes/grade; this scenario deliberately uses 4
' (the upper end of the "3-4-zuegig" range given for this fixture) to get
' a fuller stress-test size for the solver.
'
' Unlike GrundschuleFixture (Phase 2, small, LLM-extraction-focused, has
' a Prompt + ground truth for scoring Qwen), this fixture has NO prompt
' text and NO Expected*/CompletenessReport functions - it exists purely
' to feed Solver.Solve()/Solver.SolveTop() directly with realistic-size
' JSON constraints, not to test LLM extraction.
Imports System.Text.Json.Nodes

Public Module GrundschuleGrossFixture

    Private ReadOnly Grades As New List(Of Integer) From {1, 2, 3, 4}
    Private ReadOnly ClassLetters As New List(Of String) From {"a", "b", "c", "d"}
    Private ReadOnly Classes As List(Of String) =
        (From g In Grades From letter In ClassLetters Select $"{g}{letter}").ToList()
    Private ReadOnly Days As New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}
    Private Const PeriodsPerDay As Integer = 6

    Private Function SplitInto(items As List(Of String), groups As Integer) As List(Of List(Of String))
        Dim result As New List(Of List(Of String))
        For i = 0 To groups - 1
            result.Add(New List(Of String))
        Next
        For i = 0 To items.Count - 1
            result(i Mod groups).Add(items(i))
        Next
        Return result
    End Function

    ''' <summary>Klassenlehrer-Prinzip (realistic for a Grundschule): each
    ''' class has one homeroom teacher covering Deutsch/Mathematik/
    ''' Sachunterricht for ONLY that class, plus a handful of Fachlehrer
    ''' (Sport/Musik/Kunst/Religion/Englisch) each covering several
    ''' classes across grades - built programmatically rather than as a
    ''' hand-written literal list, since 16 classes x ~8 subjects would be
    ''' a very long, error-prone list to maintain by hand.</summary>
    Private Function BuildAssignments() As List(Of SubjectAssignment)
        Dim result As New List(Of SubjectAssignment)

        For Each cls In Classes
            Dim grade = Integer.Parse(cls.Substring(0, 1))
            Dim teacher = $"KL-{cls}"
            Dim deutschHours = If(grade <= 2, 6, 5)
            result.Add(New SubjectAssignment("Deutsch", teacher, New List(Of String) From {cls}, deutschHours, 2, Nothing, Nothing))
            result.Add(New SubjectAssignment("Mathematik", teacher, New List(Of String) From {cls}, 5, 2, Nothing, Nothing))
            result.Add(New SubjectAssignment("Sachunterricht", teacher, New List(Of String) From {cls}, 3, 2, Nothing, Nothing))
        Next

        Dim sportGroups = SplitInto(Classes, 3)
        For i = 0 To sportGroups.Count - 1
            result.Add(New SubjectAssignment("Sport", $"Sport-{i + 1}", sportGroups(i), 3, 1, Nothing,
                                              New List(Of String) From {"Turnhalle1", "Turnhalle2"}))
        Next

        Dim musikGroups = SplitInto(Classes, 2)
        For i = 0 To musikGroups.Count - 1
            result.Add(New SubjectAssignment("Musik", $"Musik-{i + 1}", musikGroups(i), 2, 1, Nothing, Nothing))
        Next

        Dim kunstGroups = SplitInto(Classes, 2)
        For i = 0 To kunstGroups.Count - 1
            result.Add(New SubjectAssignment("Kunst", $"Kunst-{i + 1}", kunstGroups(i), 2, 1, Nothing, Nothing))
        Next

        Dim religionGroups = SplitInto(Classes, 2)
        For i = 0 To religionGroups.Count - 1
            result.Add(New SubjectAssignment("Religion", $"Religion-{i + 1}", religionGroups(i), 2, 1, Nothing, Nothing))
        Next

        ' Englisch ist in BW ab Klasse 3 Pflicht.
        Dim englischClasses = Classes.Where(Function(c) Integer.Parse(c.Substring(0, 1)) >= 3).ToList()
        Dim englischGroups = SplitInto(englischClasses, 2)
        For i = 0 To englischGroups.Count - 1
            result.Add(New SubjectAssignment("Englisch", $"Englisch-{i + 1}", englischGroups(i), 2, 1, Nothing, Nothing))
        Next

        Return result
    End Function

    Public ReadOnly GrundschuleGrossAssignments As List(Of SubjectAssignment) = BuildAssignments()

    Public Function BuildGrundschuleGrossScenario() As JsonObject
        Return AssignmentScenarioBuilder.BuildScenario(Classes, Days, PeriodsPerDay, GrundschuleGrossAssignments)
    End Function

End Module
