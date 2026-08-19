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
        ' Phase 2.20f: a class can have MORE THAN ONE simultaneous session
        ' at the same (Day,Period) once it participates in a
        ' "parallel_group" (e.g. Religion-ev/Religion-kath/Ethik all firing
        ' together for the same class) - group by slot and combine into
        ' ONE GridCell instead of silently overwriting with whichever entry
        ' happens to be processed last (the pre-2.20 assumption, back when
        ' no_overlap(class) always guaranteed at most 1 entry per slot).
        ' For every pre-2.20 fixture this is a no-op: each group has
        ' exactly 1 element, and joining a 1-element list is byte-identical
        ' to using that single value directly.
        For Each classGroup In schedule.GroupBy(Function(l) l.ClassName)
            For Each slotGroup In classGroup.GroupBy(Function(l) (l.Day, l.Period))
                Dim entries = slotGroup.OrderBy(Function(l) l.Subject).ToList()
                Dim rooms = entries.Select(Function(l) l.Room).Where(Function(r) Not String.IsNullOrEmpty(r)).Distinct().ToList()
                grids(classGroup.Key)(slotGroup.Key.Day)(slotGroup.Key.Period) = New GridCell With {
                    .Subject = String.Join(" / ", entries.Select(Function(l) l.Subject)),
                    .Teacher = String.Join(" / ", entries.Select(Function(l) l.Teacher)),
                    .Room = If(rooms.Count > 0, String.Join(" / ", rooms), Nothing)
                }
            Next
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

    ''' <summary>Phase 2.11: {wahlprofil_id: {day: {period: GridCell |
    ''' Nothing}}} - one grid per "kurswahl" constraint, analogous to
    ''' ToClassGrids/ToTeacherGrids but keyed by Wahlprofil rather than by
    ''' a JSON entities list (kurswahl constraints ARE the Wahlprofil
    ''' list - there is no separate entities.wahlprofile). `kursSchedule`
    ''' is expected to be Solver.SolveKursstufe's final per-Kurs Schedule,
    ''' where .ClassName is the real Kurs id (see Raumzuordnung.vb) -
    ''' GridCell.ClassName is populated from it here so a Wahlprofil grid
    ''' cell can show which Kurs it is, unlike ToClassGrids's class-grid
    ''' cells which never need to.
    '''
    ''' Throws if two of a Wahlprofil's own Kurse land on the same
    ''' (day,period) slot - defense-in-depth only: Kursblockung.vb's own
    ''' constraints already make this structurally impossible (see
    ''' Verifier.VerifyKursblockung for the independent re-check), so
    ''' hitting this is a bug signal, not an input this function should
    ''' silently paper over.</summary>
    Public Function ToWahlprofilGrids(data As JsonObject, kursSchedule As List(Of ScheduleEntry)) _
        As Dictionary(Of String, Dictionary(Of String, Dictionary(Of Integer, GridCell)))
        Dim ent = JsonHelpers.Entities(data)
        Dim timeslots = JsonHelpers.Timeslots(ent)
        Dim days = JsonHelpers.AsStringList(timeslots, "days")
        Dim periods = Enumerable.Range(1, JsonHelpers.GetInt(timeslots, "periods_per_day").Value).ToList()

        Dim wahlprofilConstraints = JsonHelpers.Constraints(data).Where(Function(con) JsonHelpers.GetString(con, "type") = "kurswahl").ToList()
        Dim wahlprofilIds = wahlprofilConstraints.Select(Function(con) JsonHelpers.GetString(con, "wahlprofil_id")).ToList()
        Dim grids = EmptyGrid(wahlprofilIds, days, periods)

        Dim entriesByKursId = kursSchedule.GroupBy(Function(e) e.ClassName).ToDictionary(Function(g) g.Key, Function(g) g.ToList())

        For Each wahlprofilConstraint In wahlprofilConstraints
            Dim wahlprofilId = JsonHelpers.GetString(wahlprofilConstraint, "wahlprofil_id")
            For Each kursId In JsonHelpers.AsStringList(wahlprofilConstraint, "kurse")
                If Not entriesByKursId.ContainsKey(kursId) Then Continue For
                For Each entry In entriesByKursId(kursId)
                    If grids(wahlprofilId)(entry.Day)(entry.Period) IsNot Nothing Then
                        Throw New InvalidOperationException(
                            $"Wahlprofil '{wahlprofilId}' hat am {entry.Day}/{entry.Period} bereits einen Kurs belegt - " &
                            "Kursblockung sollte das strukturell verhindern (siehe Verifier.VerifyKursblockung).")
                    End If
                    grids(wahlprofilId)(entry.Day)(entry.Period) = New GridCell With {
                        .ClassName = kursId, .Subject = entry.Subject, .Teacher = entry.Teacher, .Room = entry.Room
                    }
                Next
            Next
        Next
        Return grids
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

    Private Function WahlprofilCellText(cell As GridCell) As String
        If cell Is Nothing Then Return "-"
        Dim text = $"{cell.ClassName}: {cell.Subject} ({cell.Teacher})"
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

    ''' <summary>Phase 2.18: GFM-Tabellen-Pendant zum ASCII-`FormatGrid`
    ''' (identische Signatur/Datenquelle - `ToClassGrids`/`ToTeacherGrids`
    ''' bleiben unveraendert, nur die Ausgabe wird als Markdown-Tabelle
    ''' statt Box-Drawing gerendert) - fuer die code-freien
    ''' `tests/&lt;schule&gt;/output/*.md`-Reports (siehe
    ''' `tests/README.md`).</summary>
    Public Function FormatGridMarkdown(entityName As String, gridForEntity As Dictionary(Of String, Dictionary(Of Integer, GridCell)),
                                        days As List(Of String), periods As List(Of Integer),
                                        cellTextFn As Func(Of GridCell, String)) As String
        Dim EscapeCell = Function(s As String) s.Replace("|", "\|")
        Dim lines As New List(Of String) From {
            $"### {entityName}",
            "",
            "| Std. | " & String.Join(" | ", days) & " |",
            "|---|" & String.Join("", days.Select(Function(d) "---|"))
        }
        For Each p In periods
            lines.Add($"| {p} | " & String.Join(" | ", days.Select(Function(d) EscapeCell(cellTextFn(gridForEntity(d)(p))))) & " |")
        Next
        Return String.Join(vbLf, lines)
    End Function

    ''' <summary>Phase 2.18: Markdown-Report fuer ein geloestes
    ''' `Lehrereinsatzplanung.SolveLehrereinsatz`-Ergebnis - Lehrkraefte-
    ''' Tabelle (Soll-/Ist-Deputat, Klassenlehrer-von, Zuweisungen) plus
    ''' Klassenlehrer-je-Klasse-Tabelle. Gleiche Datenquelle wie
    ''' `AFSFellbachGrundschuleBenchmarkTests.vb`s Konsolen-Report, hier
    ''' als wiederverwendbare Markdown-Ausgabe fuer die code-freien
    ''' `tests/&lt;schule&gt;/output/lehrerzuteilung.md`-Reports.</summary>
    Public Function FormatLehrereinsatzMarkdown(bestand As Stammdatenbestand, result As LehrereinsatzResult) As String
        Dim lines As New List(Of String) From {$"# Lehrerzuteilung: {bestand.SchulName}", ""}
        Dim objectiveText = If(result.Solver IsNot Nothing, result.Solver.ObjectiveValue.ToString("F1", Globalization.CultureInfo.InvariantCulture), "n/a")
        lines.Add($"**Status:** {result.Status}  |  **Objective:** {objectiveText}")
        lines.Add("")

        If result.Zuweisungen IsNot Nothing Then
            ' Phase 2.20f: welche (Klassenstufe,Fach)-Kombinationen sind
            ' ueber eine Gruppe gefuehrt? Fuer diese wurde eine einzelne
            ' Lehrereinsatzplanung-Zuweisung bereits bei der Loesungs-
            ' extraktion auf ALLE echten, von der Gruppe umspannten Klassen
            ' dupliziert (siehe Lehrereinsatzplanung.SolveLehrereinsatz) -
            ' ohne diese Deduplizierung hier wuerde die "Ist"-Spalte die
            ' tatsaechlichen Wochenstunden faelschlich vervielfachen (z.B.
            ' Ist=16h statt der wirklich unterrichteten 8h bei einer
            ' Gruppe, die 2 Klassen umspannt).
            Dim gruppenFachKlassenstufen As New HashSet(Of (Klassenstufe As Integer, Fach As String))(
                bestand.Gruppen.Where(Function(g) g.FachName IsNot Nothing AndAlso g.Klassenstufe.HasValue).
                    Select(Function(g) (g.Klassenstufe.Value, g.FachName)))

            lines.Add("## Lehrkraefte")
            lines.Add("")
            lines.Add("| Lehrkraft | Soll (h) | Ist (h) | Klassenlehrer von | Zuweisungen |")
            lines.Add("|---|---|---|---|---|")
            For Each l In bestand.Lehrkraefte
                Dim eigene = result.Zuweisungen.Where(Function(z) z.Lehrer = l.Name).ToList()
                Dim ist = 0
                For Each fachGroup In eigene.GroupBy(Function(z) (z.Fach, bestand.Klassen.Single(Function(k) k.Name = z.Klasse).Klassenstufe))
                    Dim stunden = Stammdaten.WochenstundenFuer(
                        bestand.Faecher.Single(Function(f) f.Name = fachGroup.Key.Item1), fachGroup.Key.Item2).WochenstundenSoll
                    If gruppenFachKlassenstufen.Contains((fachGroup.Key.Item2, fachGroup.Key.Item1)) Then
                        ist += stunden
                    Else
                        ist += stunden * fachGroup.Count()
                    End If
                Next
                Dim klassenlehrerVon = If(result.Klassenlehrer IsNot Nothing,
                    String.Join(", ", result.Klassenlehrer.Where(Function(kvp) kvp.Value = l.Name).Select(Function(kvp) kvp.Key)),
                    "")
                Dim zuweisungenText = String.Join(", ", eigene.Select(Function(z) $"{z.Klasse}/{z.Fach}"))
                lines.Add($"| {l.Name} | {l.DeputatSollstunden:F0} | {ist} | {klassenlehrerVon} | {zuweisungenText} |")
            Next
            lines.Add("")

            lines.Add("## Klassenlehrer je Klasse")
            lines.Add("")
            lines.Add("| Klasse | Klassenlehrer |")
            lines.Add("|---|---|")
            For Each k In bestand.Klassen
                Dim kl = If(result.Klassenlehrer IsNot Nothing AndAlso result.Klassenlehrer.ContainsKey(k.Name), result.Klassenlehrer(k.Name), "(keiner gefunden)")
                lines.Add($"| {k.Name} | {kl} |")
            Next
        End If

        Return String.Join(vbLf, lines)
    End Function

    ''' <summary>Phase 2.11: renders the "kurswahl" side of a solved
    ''' Kursstufe schedule (Solver.SolveKursstufe's Schedule) as ASCII
    ''' tables, one per Wahlprofil - the Kursstufe analogue of
    ''' FormatSchedule's "KLASSEN"/"LEHRKRAEFTE" sections, since a
    ''' Kursstufe scenario has no entities.classes to render there
    ''' instead.</summary>
    Public Function FormatKursstufeSchedule(data As JsonObject, kursSchedule As List(Of ScheduleEntry)) As String
        Dim ent = JsonHelpers.Entities(data)
        Dim timeslots = JsonHelpers.Timeslots(ent)
        Dim days = JsonHelpers.AsStringList(timeslots, "days")
        Dim periods = Enumerable.Range(1, JsonHelpers.GetInt(timeslots, "periods_per_day").Value).ToList()

        Dim wahlprofilIds = JsonHelpers.Constraints(data).
            Where(Function(con) JsonHelpers.GetString(con, "type") = "kurswahl").
            Select(Function(con) JsonHelpers.GetString(con, "wahlprofil_id")).ToList()

        Dim parts As New List(Of String) From {"WAHLPROFILE", ""}
        Dim grids = ToWahlprofilGrids(data, kursSchedule)
        For Each wahlprofilId In wahlprofilIds
            parts.Add(FormatGrid(wahlprofilId, grids(wahlprofilId), days, periods, AddressOf WahlprofilCellText))
            parts.Add("")
        Next
        Return String.Join(vbLf, parts)
    End Function

End Module

