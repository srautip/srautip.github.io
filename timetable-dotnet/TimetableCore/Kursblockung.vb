' Phase 2.11b: Kursblockung - the first of three CP-SAT stages behind
' Solver.SolveKursstufe (see the phase-2.11 plan section): assigns each
' `entities.kurse` entry to one `entities.schienen` slot such that no
' Wahlprofil ever has two of its own chosen courses in the same Schiene.
' Deliberately a SEPARATE module from Solver.vb (mirrors the existing
' ScheduleQuality.vb/SolveTopObjective.vb split) - this is a structurally
' different CP-SAT model (course-to-Schiene assignment), not a day/period
' schedule, so it does not belong inside BuildCoreModel/ApplyConstraints.
'
' Precondition (same discipline as Solver.BuildModel assuming
' Validation.ValidateEntities already ran): callers must run
' Validation.ValidateKursstufeEntities(data) first and confirm it returns
' no errors. This module does not re-validate that every Kurs has at least
' one compatible Schiene - an unvalidated Kurs with zero compatible
' Schienen would make the "genau eine Schiene pro Kurs" constraint below
' sum over an empty variable list, which is exactly the case
' ValidateKursstufeEntities's "keine Schiene mit kursart=...gefunden"
' check exists to catch beforehand.
Imports System.Text.Json.Nodes
Imports System.Diagnostics
Imports System.Threading
Imports Google.OrTools.Sat


Public NotInheritable Class KursblockungResult
    Public Property Status As CpSolverStatus
    ''' <summary>KursId -> SchieneId. Nothing unless Status is
    ''' Optimal/Feasible.</summary>
    Public Property Assignment As Dictionary(Of String, String)
    ''' <summary>Abbruch durch den Aufrufer (arc42 8.11).</summary>
    Public Property Cancelled As Boolean
End Class

Public Module Kursblockung

    Private NotInheritable Class KursInfo
        Public Property Id As String
        Public Property Kursart As String
        Public Property HoursPerWeek As Integer
        Public Property Teacher As String
    End Class

    Private NotInheritable Class SchieneInfo
        Public Property Id As String
        Public Property Kursart As String
        Public Property HoursPerWeek As Integer
        Public Property Capacity As Integer?
    End Class

    Private Function KurseFrom(ent As JsonObject) As List(Of KursInfo)
        Return JsonHelpers.GetKurse(ent).Select(Function(k) New KursInfo With {
            .Id = JsonHelpers.GetString(k, "id"),
            .Kursart = JsonHelpers.GetString(k, "kursart"),
            .HoursPerWeek = JsonHelpers.GetInt(k, "hours_per_week").GetValueOrDefault(),
            .Teacher = JsonHelpers.GetString(k, "teacher")
        }).ToList()
    End Function

    Private Function SchienenFrom(ent As JsonObject) As List(Of SchieneInfo)
        Return JsonHelpers.GetSchienen(ent).Select(Function(s) New SchieneInfo With {
            .Id = JsonHelpers.GetString(s, "id"),
            .Kursart = JsonHelpers.GetString(s, "kursart"),
            .HoursPerWeek = JsonHelpers.GetInt(s, "hours_per_week").GetValueOrDefault(),
            .Capacity = JsonHelpers.GetInt(s, "capacity")
        }).ToList()
    End Function

    ''' <summary>Every `kurswahl` constraint's chosen-course-id list, one
    ''' entry per Wahlprofil. student_count/wahlprofil_id are irrelevant to
    ''' feasibility here (see plan: Kursblockung only cares about which
    ''' courses a profile holds, not how many students hold it) and are
    ''' therefore not read.</summary>
    Private Function WahlprofileFrom(data As JsonObject) As List(Of List(Of String))
        Return JsonHelpers.Constraints(data).
            Where(Function(c) JsonHelpers.GetString(c, "type") = "kurswahl").
            Select(Function(c) JsonHelpers.AsStringList(c, "kurse")).
            ToList()
    End Function

    ''' <summary>Solves the Kursblockung sub-problem: assign every Kurs to
    ''' exactly one compatible Schiene (same kursart AND hours_per_week),
    ''' such that (1) no Wahlprofil has two of its own courses in the same
    ''' Schiene, (2) no teacher has two of their courses in the same
    ''' Schiene (a teacher can't teach two simultaneous courses), and (3)
    ''' if a Schiene's JSON carries an optional "capacity" (max simultaneous
    ''' Kurse - a real room-count limit, since every Kurs on one Schiene
    ''' runs at the identical time and each needs its own room), no more
    ''' than that many Kurse land on it. Capacity is OPTIONAL and additive -
    ''' a Schiene without it has no cap at all (byte-identical to every
    ''' Kursblockung test written before this was added). Otherwise purely
    ''' a feasibility problem - no objective function (see plan's
    ''' rationale: finding ANY valid Kursblockung is already the hard part
    ''' in real school scheduling; a secondary objective, e.g. balancing
    ''' Schiene load beyond the hard capacity cap, is an explicitly
    ''' deferred future extension, same as how Kann-constraints/SolveTop's
    ''' quality objective were added only after the deterministic core
    ''' existed in Phase 2.5/2.8/2.9).
    '''
    ''' Phase 2.11h finding: without SOME capacity notion, nothing stops
    ''' this feasibility-only model from piling arbitrarily many Kurse
    ''' onto one popular Schiene (there is no cost to doing so) - which
    ''' then made the downstream Raumzuordnung stage (room assignment)
    ''' Infeasible once tried against a realistic-scale fixture (11 Kurse
    ''' landed on one Schiene against only 8 available rooms). Capacity
    ''' closes that gap directly at its source instead of leaving stage C
    ''' to discover it indirectly.</summary>
    Public Function SolveKursblockung(data As JsonObject,
                                       Optional timeLimitS As Double = 30.0,
                                       Optional seed As Integer = 42,
                                       Optional numWorkers As Integer = 1,
                                       Optional cancellationToken As CancellationToken = Nothing,
                                       Optional progress As IProgress(Of SolveProgress) = Nothing) As KursblockungResult
        If cancellationToken.IsCancellationRequested Then
            Return New KursblockungResult With {.Status = CpSolverStatus.Unknown, .Cancelled = True}
        End If

        Dim ent = JsonHelpers.Entities(data)
        Dim kurse = KurseFrom(ent)
        Dim schienen = SchienenFrom(ent)
        Dim wahlprofile = WahlprofileFrom(data)

        Dim model As New CpModel()
        Dim assign As New Dictionary(Of (KursId As String, SchieneId As String), BoolVar)

        For Each k In kurse
            For Each s In schienen
                If k.Kursart = s.Kursart AndAlso k.HoursPerWeek = s.HoursPerWeek Then
                    assign((k.Id, s.Id)) = model.NewBoolVar($"assign_{k.Id}_{s.Id}")
                End If
            Next
        Next

        ' Jeder Kurs landet in genau einer (kompatiblen) Schiene.
        For Each k In kurse
            Dim vars = schienen.
                Where(Function(s) assign.ContainsKey((k.Id, s.Id))).
                Select(Function(s) assign((k.Id, s.Id))).ToList()
            model.Add(LinearExpr.Sum(vars) = 1)
        Next

        ' Pro Wahlprofil, pro Schiene: hoechstens einer der eigenen Kurse.
        For Each wp In wahlprofile
            For Each s In schienen
                Dim vars = wp.
                    Where(Function(kid) assign.ContainsKey((kid, s.Id))).
                    Select(Function(kid) assign((kid, s.Id))).ToList()
                If vars.Count > 1 Then
                    model.Add(LinearExpr.Sum(vars) <= 1)
                End If
            Next
        Next

        ' Pro Lehrkraft (mit >=2 Kursen), pro Schiene: hoechstens ein Kurs
        ' dieser Lehrkraft - sonst muesste sie zwei simultane Kurse
        ' gleichzeitig unterrichten. Nicht in der urspruenglichen
        ' Nutzer-Vorgabe explizit genannt, aber logisch notwendig (siehe
        ' Plan Abschnitt 2, Punkt 3) - ohne diese Constraint waere die
        ' spaetere Raumzuordnungs-Stufe u.U. unloesbar, ohne dass der Grund
        ' aus der Kursblockung selbst ersichtlich waere.
        For Each teacherGroup In kurse.GroupBy(Function(k) k.Teacher)
            If teacherGroup.Count() < 2 Then Continue For
            For Each s In schienen
                Dim vars = teacherGroup.
                    Where(Function(k) assign.ContainsKey((k.Id, s.Id))).
                    Select(Function(k) assign((k.Id, s.Id))).ToList()
                If vars.Count > 1 Then
                    model.Add(LinearExpr.Sum(vars) <= 1)
                End If
            Next
        Next

        ' Pro Schiene mit gesetzter capacity: hoechstens so viele Kurse
        ' gleichzeitig, wie Raeume verfuegbar sind (siehe Doku-Kommentar
        ' oben - ohne das haeuft das Modell beliebig viele Kurse auf einer
        ' beliebten Schiene, was Stufe C spaeter unloesbar machen kann).
        For Each s In schienen
            If Not s.Capacity.HasValue Then Continue For
            Dim vars = kurse.
                Where(Function(k) assign.ContainsKey((k.Id, s.Id))).
                Select(Function(k) assign((k.Id, s.Id))).ToList()
            model.Add(LinearExpr.Sum(vars) <= s.Capacity.Value)
        Next

        Dim solver As New CpSolver()
        solver.StringParameters = $"max_time_in_seconds:{timeLimitS.ToString(Globalization.CultureInfo.InvariantCulture)},random_seed:{seed},num_search_workers:{numWorkers}"
        Dim sw = Stopwatch.StartNew()
        Dim cb As ConvergenceCallback = If(progress Is Nothing, Nothing, New ConvergenceCallback())
        Dim run = SolveRunner.RunSolve(model, solver, cb,
                                       SolveRunner.SingleStage(SolvePhase.Iteration, "Kursblockung wird gerechnet",
                                                               timeLimitS, cancellationToken, progress, sw))
        Dim status = run.Status

        Dim result As New KursblockungResult With {.Status = status, .Cancelled = run.Cancelled}
        If status = CpSolverStatus.Optimal OrElse status = CpSolverStatus.Feasible Then
            Dim assignment As New Dictionary(Of String, String)
            For Each kvp In assign
                If solver.BooleanValue(kvp.Value) Then
                    assignment(kvp.Key.KursId) = kvp.Key.SchieneId
                End If
            Next
            result.Assignment = assignment
        End If
        Return result
    End Function

End Module
