' Ported 1:1 from timetable/formatting.py. Turns the flat, unordered
' `Schedule` list returned by Solver.Solve into something readable:
' per-class/per-teacher grids and ASCII tables. This is the presentation
' layer the WinForms GUI (Phase 3) will consume via ToClassGrids/
' ToTeacherGrids - it has no GUI dependency itself.
Imports System.Text.Json.Nodes


Public NotInheritable Class GridCell
    Public Property ClassName As String
    Public Property Subject As String
    Public Property Teacher As String
    Public Property Room As String
End Class

Public Module Formatting

    Private Function EmptyGrid(entityNames As List(Of String), days As List(Of String), periods As List(Of Integer)) _
        As Dictionary(Of String, Dictionary(Of String, Dictionary(Of Integer, GridCell)))
        Dim grids As New Dictionary(Of String, Dictionary(Of String, Dictionary(Of Integer, GridCell)))
        For Each e In entityNames
            Dim byDay As New Dictionary(Of String, Dictionary(Of Integer, GridCell))
            For Each d In days
                Dim byPeriod As New Dictionary(Of Integer, GridCell)
                For Each p In periods
                    byPeriod(p) = Nothing
                Next
                byDay(d) = byPeriod
            Next
            grids(e) = byDay
        Next
        Return grids
    End Function

    ''' <summary>Returns {class: {day: {period: GridCell | Nothing}}}.</summary>
    Public Function ToClassGrids(data As JsonObject, schedule As List(Of ScheduleEntry)) _
        As Dictionary(Of String, Dictionary(Of String, Dictionary(Of Integer, GridCell)))
        Dim ent = JsonHelpers.Entities(data)
        Dim timeslots = JsonHelpers.Timeslots(ent)
        Dim days = JsonHelpers.AsStringList(timeslots, "days")
        Dim periods = Enumerable.Range(1, JsonHelpers.GetInt(timeslots, "periods_per_day").Value).ToList()
        Dim grids = EmptyGrid(JsonHelpers.AsStringList(ent, "classes"), days, periods)
        For Each l In schedule
            grids(l.ClassName)(l.Day)(l.Period) = New GridCell With {
                .Subject = l.Subject, .Teacher = l.Teacher, .Room = l.Room
            }
        Next
        Return grids
    End Function

    ''' <summary>Returns {teacher: {day: {period: GridCell | Nothing}}}
    ''' (GridCell.ClassName populated instead of .Teacher).</summary>
    Public Function ToTeacherGrids(data As JsonObject, schedule As List(Of ScheduleEntry)) _
        As Dictionary(Of String, Dictionary(Of String, Dictionary(Of Integer, GridCell)))
        Dim ent = JsonHelpers.Entities(data)
        Dim timeslots = JsonHelpers.Timeslots(ent)
        Dim days = JsonHelpers.AsStringList(timeslots, "days")
        Dim periods = Enumerable.Range(1, JsonHelpers.GetInt(timeslots, "periods_per_day").Value).ToList()
        Dim grids = EmptyGrid(JsonHelpers.AsStringList(ent, "teachers"), days, periods)
        For Each l In schedule
            grids(l.Teacher)(l.Day)(l.Period) = New GridCell With {
                .ClassName = l.ClassName, .Subject = l.Subject, .Room = l.Room
            }
        Next
        Return grids
    End Function

    ''' <summary>Same content as ToClassGrids, ready for JSON export
    ''' (period keys converted to string since JSON object keys must be
    ''' strings).</summary>
    Public Function ToJsonPerClass(data As JsonObject, schedule As List(Of ScheduleEntry)) As JsonObject
        Dim grids = ToClassGrids(data, schedule)
        Dim result As New JsonObject()
        For Each clsEntry In grids
            Dim byDayObj As New JsonObject()
            For Each dayEntry In clsEntry.Value
                Dim byPeriodObj As New JsonObject()
                For Each periodEntry In dayEntry.Value
                    Dim cell = periodEntry.Value
                    If cell Is Nothing Then
                        byPeriodObj(periodEntry.Key.ToString()) = Nothing
                    Else
                        byPeriodObj(periodEntry.Key.ToString()) = New JsonObject From {
                            {"subject", cell.Subject}, {"teacher", cell.Teacher}, {"room", cell.Room}
                        }
                    End If
                Next
                byDayObj(dayEntry.Key) = byPeriodObj
            Next
            result(clsEntry.Key) = byDayObj
        Next
        Return result
    End Function

    Private Function ClassCellText(cell As GridCell) As String
        If cell Is Nothing Then Return "-"
        Dim text = $"{cell.Subject} ({cell.Teacher})"
        If Not String.IsNullOrEmpty(cell.Room) Then text &= $" [{cell.Room}]"
        Return text
    End Function

    Private Function TeacherCellText(cell As GridCell) As String
        If cell Is Nothing Then Return "-"
        Dim text = $"{cell.ClassName} {cell.Subject}"
        If Not String.IsNullOrEmpty(cell.Room) Then text &= $" [{cell.Room}]"
        Return text
    End Function

    ''' <summary>Renders one entity's {day: {period: cell}} grid as an
    ''' ASCII table (rows = periods, columns = days).</summary>
    Public Function FormatGrid(entityName As String, gridForEntity As Dictionary(Of String, Dictionary(Of Integer, GridCell)),
                                days As List(Of String), periods As List(Of Integer),
                                cellTextFn As Func(Of GridCell, String)) As String
        Dim cellText As New Dictionary(Of (Day As String, Period As Integer), String)
        For Each d In days
            For Each p In periods
                cellText((d, p)) = cellTextFn(gridForEntity(d)(p))
            Next
        Next
        Dim colWidth As New Dictionary(Of String, Integer)
        For Each d In days
            colWidth(d) = Math.Max(d.Length, periods.Max(Function(p) cellText((d, p)).Length))
        Next
        Dim periodColWidth = Math.Max("Std.".Length, periods.Max(Function(p) p.ToString().Length))

        Dim header = "Std.".PadRight(periodColWidth) & " | " & String.Join(" | ", days.Select(Function(d) d.PadRight(colWidth(d))))
        Dim lines As New List(Of String) From {$"=== {entityName} ===", header, New String("-"c, header.Length)}
        For Each p In periods
            Dim row = p.ToString().PadRight(periodColWidth) & " | " &
                String.Join(" | ", days.Select(Function(d) cellText((d, p)).PadRight(colWidth(d))))
            lines.Add(row)
        Next
        Return String.Join(vbLf, lines)
    End Function

    ''' <summary>Renders the full solved schedule as ASCII tables: one
    ''' per class, and (by default) one per teacher.</summary>
    Public Function FormatSchedule(data As JsonObject, schedule As List(Of ScheduleEntry),
                                    Optional includeTeachers As Boolean = True) As String
        Dim ent = JsonHelpers.Entities(data)
        Dim timeslots = JsonHelpers.Timeslots(ent)
        Dim days = JsonHelpers.AsStringList(timeslots, "days")
        Dim periods = Enumerable.Range(1, JsonHelpers.GetInt(timeslots, "periods_per_day").Value).ToList()

        Dim parts As New List(Of String) From {"KLASSEN", ""}
        Dim classGrids = ToClassGrids(data, schedule)
        For Each cls In JsonHelpers.AsStringList(ent, "classes")
            parts.Add(FormatGrid(cls, classGrids(cls), days, periods, AddressOf ClassCellText))
            parts.Add("")
        Next

        If includeTeachers Then
            parts.Add("LEHRKRAEFTE")
            parts.Add("")
            Dim teacherGrids = ToTeacherGrids(data, schedule)
            For Each teacher In JsonHelpers.AsStringList(ent, "teachers")
                parts.Add(FormatGrid(teacher, teacherGrids(teacher), days, periods, AddressOf TeacherCellText))
                parts.Add("")
            Next
        End If

        Return String.Join(vbLf, parts)
    End Function

End Module

