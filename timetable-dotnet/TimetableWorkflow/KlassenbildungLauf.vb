' Die Klassenbildung (Stufe 0) als wiederverwendbarer Dienst - das
' Gegenstueck zu StundenplanLauf.
'
' Nachtrag zu Stufe B des GUI-Unterbaus: dort wurde nur RunOne zerlegt,
' KlassenOne blieb als Prozedur mit Dateipfad-Ein- und -Ausgabe in der CLI
' stehen. Fuer den GUI-Durchstich (Stufe D) ist ausgerechnet DIESE Stufe
' die erste, die gebraucht wird - das Klassenbildungs-Board ist das erste
' Dashboard.
'
' Wie beim Stundenplan-Dienst gilt: kein Dateizugriff, keine
' Konsolenausgabe, Abbruch und Fortschritt durchgereicht. Die
' Berichtserzeugung liegt unveraendert in KlassenbildungBericht.
Imports System.Text.Json.Nodes
Imports System.Threading
Imports Google.OrTools.Sat
Imports TimetableCore
Imports TimetableYaml

''' <summary>Wie weit die Klassenbildung gekommen ist.</summary>
Public Enum KlassenbildungStufe
    Regelpruefung
    Varianten
    Nachpruefung
    Fertig
End Enum

Public NotInheritable Class KlassenbildungLaufErgebnis
    Public Property Stufe As KlassenbildungStufe
    Public Property Erfolgreich As Boolean
    Public Property Abgebrochen As Boolean
    ''' <summary>Klartextmeldungen der gescheiterten Stufe.</summary>
    Public Property Meldungen As New List(Of String)

    Public Property Eingabe As KlassenbildungInput
    Public Property Top As KlassenbildungTopResult
    ''' <summary>Nur die Varianten MIT Zuordnung - die Reihenfolge
    ''' entspricht Bewertungen.</summary>
    Public Property Geloeste As New List(Of KlassenbildungResult)
    Public Property Bewertungen As New List(Of KlassenbildungBewertung)

    ''' <summary>Aufgeloeste Parameter; die Berichte zitieren sie.</summary>
    Public Property ZeitlimitS As Double
    Public Property NVarianten As Integer
    Public Property Epsilon As Double
    Public Property MinDistanz As Integer
End Class

Public Module KlassenbildungLauf

    ''' <summary>Rechnet die Varianten-Schleife und prueft sie unabhaengig
    ''' nach. Die Nachpruefung ist kein Beiwerk: sie ist das
    ''' Verifier-Prinzip dieses Projekts (arc42 8.2) - der Bewertungslauf
    ''' muss die Solver-Verletzungen exakt reproduzieren, sonst stimmt
    ''' eines von beiden nicht, und dann darf kein Ergebnis
    ''' herauskommen.</summary>
    Public Function Ausfuehren(input As KlassenbildungInput,
                                kb As KlassenbildungConfig,
                                seed As Integer,
                                numWorkers As Integer,
                                Optional cancellationToken As CancellationToken = Nothing,
                                Optional progress As IProgress(Of SolveProgress) = Nothing) As KlassenbildungLaufErgebnis
        If kb Is Nothing Then kb = New KlassenbildungConfig()
        Dim e As New KlassenbildungLaufErgebnis With {
            .Eingabe = input, .Stufe = KlassenbildungStufe.Regelpruefung,
            .ZeitlimitS = If(kb.ZeitlimitS, 30.0),
            .NVarianten = If(kb.NVarianten, 3),
            .Epsilon = If(kb.Epsilon, 0.05),
            .MinDistanz = If(kb.MinDistanz, 8)
        }

        If cancellationToken.IsCancellationRequested Then
            e.Abgebrochen = True
            Return e
        End If

        Dim fehler = Klassenbildung.ValidateKlassenbildung(input)
        If fehler.Count > 0 Then
            e.Meldungen = fehler
            Return e
        End If

        e.Stufe = KlassenbildungStufe.Varianten
        e.Top = Klassenbildung.SolveKlassenbildungTop(input,
            zeitlimitS:=e.ZeitlimitS, seed:=seed, numWorkers:=numWorkers,
            prioGewichte:=kb.PrioGewichte,
            symmetriebrechung:=If(kb.Symmetriebrechung, True),
            nVarianten:=e.NVarianten, epsilon:=e.Epsilon, minDistanz:=e.MinDistanz,
            cancellationToken:=cancellationToken, progress:=progress)
        e.Abgebrochen = e.Top.Cancelled

        e.Geloeste = e.Top.Varianten.Where(Function(v) v.Zuordnung IsNot Nothing).ToList()
        If e.Geloeste.Count = 0 Then Return e

        ' Unabhaengige Nachpruefung (Verifier-Prinzip): der Bewertungslauf
        ' muss die Solver-Verletzungen exakt reproduzieren.
        e.Stufe = KlassenbildungStufe.Nachpruefung
        For Each v In e.Geloeste
            Dim bewertung = KlassenbildungQuality.Bewerte(input, v.Zuordnung)
            e.Bewertungen.Add(bewertung)
            For Each sv In v.Verletzungen
                Dim unabhaengig = bewertung.Verletzungen.Single(Function(b) b.RegelId = sv.RegelId)
                If unabhaengig.Mass <> sv.Mass Then
                    e.Meldungen.Add($"Bewertung widerspricht Solver: Regel {sv.RegelId} Solver={sv.Mass} Bewertung={unabhaengig.Mass}")
                    Return e
                End If
            Next
        Next

        e.Stufe = KlassenbildungStufe.Fertig
        e.Erfolgreich = Not e.Abgebrochen
        Return e
    End Function

    ''' <summary>Der Markdown-Report. Nothing, solange keine Variante
    ''' vorliegt - dann hat der Aufrufer nur die Fehlermeldungen.</summary>
    Public Function BaueBerichtMarkdown(schule As String, e As KlassenbildungLaufErgebnis) As String
        If e.Stufe = KlassenbildungStufe.Regelpruefung Then
            If e.Meldungen.Count = 0 Then Return Nothing
            Return $"# Klassenbildung: {schule}{vbLf}{vbLf}**Status:** Validierung FEHLGESCHLAGEN{vbLf}{vbLf}" &
                String.Join(vbLf, e.Meldungen.Select(Function(m) $"- {m}"))
        End If
        If e.Geloeste.Count = 0 Then
            Dim status = If(e.Top IsNot Nothing AndAlso e.Top.Varianten.Count > 0,
                            e.Top.Varianten(0).Status.ToString(), CpSolverStatus.Unknown.ToString())
            Return $"# Klassenbildung: {schule}{vbLf}{vbLf}**Status:** {status} (keine Loesung - harte Regeln/Fixierungen kollidieren; Konfliktkern-Analyse siehe Plan K6){vbLf}"
        End If
        Return KlassenbildungBericht.BaueMarkdown(schule, e.Eingabe, e.Top, e.Geloeste, e.Bewertungen,
                                                  e.ZeitlimitS, e.NVarianten, e.Epsilon, e.MinDistanz)
    End Function

    ''' <summary>Das JSON, das der Viewer einbettet. Nothing, solange keine
    ''' Variante vorliegt.</summary>
    Public Function BaueViewerJson(schule As String, e As KlassenbildungLaufErgebnis) As JsonObject
        If e.Geloeste.Count = 0 Then Return Nothing
        Return KlassenbildungBericht.BaueJson(schule, e.Eingabe, e.Top, e.Geloeste, e.Bewertungen,
                                              e.ZeitlimitS, e.Epsilon, e.MinDistanz)
    End Function

End Module
