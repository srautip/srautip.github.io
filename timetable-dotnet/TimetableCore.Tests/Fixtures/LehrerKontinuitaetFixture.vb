' Phase 2.14: Lehrerkontinuitaet Kl.5 -> Kl.6 ueber ein "Vorjahresstundenplan"-
' Fixture, angelehnt an dieselbe fiktive, GSG-Fellbach-inspirierte Schule wie
' GymnasiumSekIFixture.vb.
'
' IMPORTANT: SYNTHETIC, plausible Testdaten - nicht die reale Lehrerbesetzung
' des GSG. Recherchierter realweltlicher Hintergrund (siehe
' docs/phase2-14-lehrerkontinuitaet.md fuer Details/Quellen): Baden-
' Wuerttemberg bezeichnet Kl.5/6 offiziell als "Orientierungsstufe" mit
' explizitem Fokus auf "personalen Bezug"; das reale Gustav-Stresemann-
' Gymnasium Fellbach praktiziert dafuer ein "Klassenlehrer-Tandem" in der
' Unterstufe. Dieses Fixture bildet die STAERKERE Variante ab (Nutzer-
' entscheidung: alle Faecher der Klasse bleiben kontinuierlich, nicht nur
' ein 2-Faecher-Tandem-Subset).
'
' Bewusst EIGENSTAENDIG, nicht Teil von GymnasiumSekIFixture.vb: dessen
' AddUniformSubject gruppiert Klassen zug-uebergreifend in Bloecke
' (classesPerTeacher=4..8) - eine strikte Pro-Zug-Kontinuitaet braucht
' stattdessen GENAU EINEN Lehrer pro Zug pro Fach (classesPerTeacher=1),
' was die bestehende 30-Klassen-Fixture und alle darauf aufbauenden Zahlen
' [z.B. Phase 2.13s "82 kombinierte Lehrer"] veraendern wuerde. Nur 5
' Zug-Klassen pro Jahrgang (nicht 30 Klassen) haelt den Solve schnell.
'
' Zentraler Befund (Solver.vb/Validation.vb): teacher_subject_assignment ist
' KEIN Kann-faehiges Constraint (Validation.vb's KannCapableTypes schliesst
' es explizit aus - "physically/structurally necessary") und wird bereits
' VOR ApplyConstraints in SessionsFromAssignments konsumiert, wo es direkt
' die Entscheidungsvariablen-Identitaet (LessonKey) definiert. Lehrer-
' kontinuitaet kann deshalb NICHT als lockerbares Solver-Constraint
' ausgedrueckt werden - nur durch Wiederverwendung desselben Lehrer-Namens
' beim Bau des Kl.6-Szenarios, eine reine Fixture-Konstruktions-Frage.
Imports System.Text.Json.Nodes

Public Module LehrerKontinuitaetFixture

    Public ReadOnly ClassLetters As New List(Of String) From {"a", "b", "c", "d", "e"}
    Public ReadOnly Klasse5Classes As List(Of String) = ClassLetters.Select(Function(l) $"5{l}").ToList()
    Public ReadOnly Klasse6Classes As List(Of String) = ClassLetters.Select(Function(l) $"6{l}").ToList()
    Public ReadOnly Days As New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}
    Public Const PeriodsPerDay As Integer = 8

    Public Function LetterOf(cls As String) As String
        Return cls.Substring(cls.Length - 1)
    End Function

    ''' <summary>Faecher, die es sowohl in Kl.5 als auch in Kl.6 gibt - 1:1
    ''' aus GymnasiumSekIFixture.vb's eigener Kontingentstundentafel
    ''' uebernommen (Deutsch/Mathematik/Englisch dort Kl.5-7/5-8/5-6
    ''' gebaendert, Erdkunde/Biologie/Religion durchgehend alle Jahrgaenge,
    ''' Sport/Musik Kl.5-6 gebaendert, Kunst Kl.5 mit 2h dann Kl.6-8 mit
    ''' 1h - die Stundenzahl darf sich aendern, das Fach UND die Lehrkraft
    ''' bleiben trotzdem kontinuierlich).</summary>
    Private ReadOnly PersistingSubjects As New List(Of (Subject As String, HoursKl5 As Integer, HoursKl6 As Integer, MaxPerDay As Integer, Rooms As List(Of String))) From {
        ("Deutsch", 4, 4, 2, Nothing),
        ("Mathematik", 4, 4, 2, Nothing),
        ("Englisch", 4, 4, 2, Nothing),
        ("Erdkunde", 2, 2, 1, Nothing),
        ("Biologie", 2, 2, 1, Nothing),
        ("Sport", 3, 3, 1, New List(Of String) From {"Turnhalle1", "Turnhalle2", "Turnhalle3"}),
        ("Musik", 2, 2, 1, New List(Of String) From {"Musikraum"}),
        ("Kunst", 2, 1, 1, New List(Of String) From {"Kunstraum"}),
        ("Religion", 2, 2, 1, Nothing)
    }

    ''' <summary>2. Fremdsprache: NEU ab Kl.6 (kein Kl.5-Pendant, wie bei
    ''' GymnasiumSekIFixture.vb) - a/c/e -> Franzoesisch, b/d -> Latein,
    ''' 4h/Woche. Hier ist keine Lehrerkontinuitaet moeglich, da das Fach in
    ''' Kl.5 schlicht nicht existiert - bewusst als solches ausgewiesen,
    ''' nicht stillschweigend uebergangen (siehe BuildKlasse6Assignments).</summary>
    Public ReadOnly Franzoesisch2FsLetters As New List(Of String) From {"a", "c", "e"}
    Public ReadOnly Latein2FsLetters As New List(Of String) From {"b", "d"}
    Public Const Hours2Fs As Integer = 4
    Public Const MaxPerDay2Fs As Integer = 2

    Public Function BuildKlasse5Assignments() As List(Of SubjectAssignment)
        Dim result As New List(Of SubjectAssignment)
        For Each entry In PersistingSubjects
            For Each cls In Klasse5Classes
                result.Add(New SubjectAssignment(entry.Subject, $"{entry.Subject}-{cls}", New List(Of String) From {cls},
                                                  entry.HoursKl5, entry.MaxPerDay, Nothing, entry.Rooms))
            Next
        Next
        Return result
    End Function

    Public Function BuildKlasse5Scenario() As JsonObject
        Return AssignmentScenarioBuilder.BuildScenario(Klasse5Classes, Days, PeriodsPerDay, BuildKlasse5Assignments())
    End Function

    ''' <summary>Gruppiert eine geloeste Kl.5-ScheduleEntry-Liste nach
    ''' (Zug-Buchstabe, Fach) und liefert die dabei eingesetzte Lehrkraft -
    ''' der "Vorjahresstundenplan" in Kurzform. Bewusst ueber die GELOESTE
    ''' Schedule abgeleitet (nicht ueber die rohen teacher_subject_
    ''' assignment-Constraints) - erzwingt, dass Kl.5 nachweislich loesbar
    ''' ist, BEVOR Kl.6 ueberhaupt gebaut wird, und passt zur woertlichen
    ''' "Vorjahresstundenplan"-Formulierung (ein tatsaechlich geloester,
    ''' verifiziert sauberer Plan). Wirft laut bei einer inkonsistenten
    ''' Zuordnung [mehr als 1 Lehrer pro (Zug,Fach)] statt das still zu
    ''' ignorieren - waere ein echter Fixture-/Solver-Bug, kein
    ''' erwarteter Zustand.</summary>
    Public Function DeriveVorjahresTeacherMap(schedule As List(Of ScheduleEntry)) As Dictionary(Of (Zug As String, Subject As String), String)
        Dim result As New Dictionary(Of (String, String), String)
        Dim grouped = schedule.GroupBy(Function(e) (Zug:=LetterOf(e.ClassName), Subject:=e.Subject))
        For Each g In grouped
            Dim teachersInGroup = g.Select(Function(e) e.Teacher).Distinct().ToList()
            If teachersInGroup.Count > 1 Then
                Throw New InvalidOperationException(
                    $"Inkonsistenter Vorjahresstundenplan: Zug '{g.Key.Zug}', Fach '{g.Key.Subject}' hat " &
                    $"{teachersInGroup.Count} verschiedene Lehrer statt genau 1: {String.Join(", ", teachersInGroup)}")
            End If
            result(g.Key) = teachersInGroup(0)
        Next
        Return result
    End Function

    ''' <summary>Baut Kl.6s Faecher-Zuordnung: fortbestehende Faecher
    ''' uebernehmen den Lehrer aus `vorjahrMap` (fehlt ein Eintrag, ist das
    ''' ein echter Fixture-Bug - Kl.5 und Kl.6 Zug/Fach-Kombinationen
    ''' muessen fuer PersistingSubjects exakt uebereinstimmen, also wird
    ''' laut geworfen statt still auf einen frischen Namen auszuweichen).
    ''' Die 2. Fremdsprache [neu ab Kl.6] mintet dagegen bewusst einen
    ''' frischen, von jedem Kl.5-Namen unterscheidbaren Namen - kein
    ''' Vorjahreslehrer moeglich, das Fach beginnt erst in Kl.6.</summary>
    Public Function BuildKlasse6Assignments(vorjahrMap As Dictionary(Of (Zug As String, Subject As String), String)) As List(Of SubjectAssignment)
        Dim result As New List(Of SubjectAssignment)
        For Each entry In PersistingSubjects
            For Each cls In Klasse6Classes
                Dim letter = LetterOf(cls)
                Dim key = (Zug:=letter, Subject:=entry.Subject)
                If Not vorjahrMap.ContainsKey(key) Then
                    Throw New InvalidOperationException($"Kein Vorjahreslehrer fuer Zug '{letter}', Fach '{entry.Subject}' gefunden - Kl.5/Kl.6-Faecherliste ist inkonsistent.")
                End If
                result.Add(New SubjectAssignment(entry.Subject, vorjahrMap(key), New List(Of String) From {cls},
                                                  entry.HoursKl6, entry.MaxPerDay, Nothing, entry.Rooms))
            Next
        Next

        ' 2. Fremdsprache: kein Vorjahreslehrer moeglich, Fach beginnt erst in Kl.6.
        For Each letter In Franzoesisch2FsLetters
            Dim cls = $"6{letter}"
            result.Add(New SubjectAssignment("Franzoesisch", $"Franzoesisch-{cls}-neu", New List(Of String) From {cls},
                                              Hours2Fs, MaxPerDay2Fs, Nothing, Nothing))
        Next
        For Each letter In Latein2FsLetters
            Dim cls = $"6{letter}"
            result.Add(New SubjectAssignment("Latein", $"Latein-{cls}-neu", New List(Of String) From {cls},
                                              Hours2Fs, MaxPerDay2Fs, Nothing, Nothing))
        Next

        Return result
    End Function

    Public Function BuildKlasse6Scenario(vorjahrMap As Dictionary(Of (Zug As String, Subject As String), String)) As JsonObject
        Return AssignmentScenarioBuilder.BuildScenario(Klasse6Classes, Days, PeriodsPerDay, BuildKlasse6Assignments(vorjahrMap))
    End Function

    ''' <summary>Eine Zeile pro (Zug, Fach): Kl.5-Lehrer, Kl.6-Lehrer, ob es
    ''' ein in Kl.6 neues Fach ist (dann IST ein Lehrerwechsel erwartet -
    ''' keine Kontinuitaet moeglich, wo es kein Vorjahr gibt). Basis fuer
    ''' sowohl die deterministischen Tests als auch die Benchmark-
    ''' Konsolenausgabe.</summary>
    Public Function BuildContinuityReport(vorjahrMap As Dictionary(Of (Zug As String, Subject As String), String),
                                           klasse6Assignments As List(Of SubjectAssignment)) _
        As List(Of (Zug As String, Subject As String, Kl5Teacher As String, Kl6Teacher As String, IsNewSubject As Boolean))

        Dim result As New List(Of (Zug As String, Subject As String, Kl5Teacher As String, Kl6Teacher As String, IsNewSubject As Boolean))
        For Each a In klasse6Assignments
            For Each cls In a.TaughtClasses
                Dim letter = LetterOf(cls)
                Dim key = (Zug:=letter, Subject:=a.Subject)
                Dim isNew = Not vorjahrMap.ContainsKey(key)
                Dim kl5Teacher = If(isNew, Nothing, vorjahrMap(key))
                result.Add((letter, a.Subject, kl5Teacher, a.Teacher, isNew))
            Next
        Next
        Return result.OrderBy(Function(r) r.Zug).ThenBy(Function(r) r.Subject).ToList()
    End Function

End Module
