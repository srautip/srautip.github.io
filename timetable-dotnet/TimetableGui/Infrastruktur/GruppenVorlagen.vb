' Die "typischen Gruppen-Vorlagen" aus gui-ui-konzept.md 6.8, als DATEN.
'
' Was eine Vorlage wirklich tut, ist mehr als "Gruppen anlegen": ein
' Parallelverbund verlangt laut Stammdaten-Regel, dass seine Gruppen
' PAARWEISE VERSCHIEDENE Faecher haben. Religion ev/kath/Ethik ist also
' keine Aufteilung von Schuelern auf ein Fach, sondern eine Aufspaltung
' des Fachs selbst - genau so steht es im GMS-Beispiel, wo aus "Religion"
' die Faecher Religion-ev, Religion-kath und Ethik geworden sind.
'
' Die Vorlage macht deshalb vier Dinge auf einmal: Fach aufspalten,
' Qualifikationen mitnehmen, Gruppen mit Verbund anlegen, Schueler
' verteilen. Wer nur eines davon taete, hinterliesse einen Bestand, den
' StammdatenValidation zu Recht ablehnt.
Imports System.Text.RegularExpressions
Imports TimetableCore

Public NotInheritable Class GruppenTeil
    Public Property FachName As String = ""
    ''' <summary>Anteil in Prozent. Der Rundungsrest geht an den ersten
    ''' Teil.</summary>
    Public Property Anteil As Integer
End Class

Public NotInheritable Class GruppenVorlage
    Public Property Name As String = ""
    ''' <summary>Das Fach, das aufgespalten wird. Fehlt es auf einer
    ''' Klassenstufe, wird diese Stufe uebersprungen - nicht erfunden.</summary>
    Public Property QuellFach As String = ""
    Public Property Verbundname As String = ""
    Public Property Bemerkung As String = ""
    Public Property Teile As New List(Of GruppenTeil)
End Class

Public Module GruppenVorlagen

    ''' <summary>Mitgelieferte Vorlagen. Bewusst wenige und belegte: die
    ''' Religionsaufteilung stammt 1:1 aus dem GMS-Beispiel. "Eigene
    ''' Vorlagen speicherbar" (6.8) ist Ausbaustufe - eine erfundene
    ''' zweite Vorlage waere nur Fassade.</summary>
    Public ReadOnly Property Alle As List(Of GruppenVorlage) = New List(Of GruppenVorlage) From {
        New GruppenVorlage With {
            .Name = "Religion ev / kath / Ethik",
            .QuellFach = "Religion",
            .Verbundname = "Religion-Ethik",
            .Bemerkung = "Spaltet das Fach Religion je gewaehlter Klassenstufe in drei Faecher " &
                         "auf und legt sie als Parallelverbund an - sie liegen damit zeitgleich, " &
                         "wie es der Unterrichtsalltag verlangt.",
            .Teile = New List(Of GruppenTeil) From {
                New GruppenTeil With {.FachName = "Religion-ev", .Anteil = 40},
                New GruppenTeil With {.FachName = "Religion-kath", .Anteil = 25},
                New GruppenTeil With {.FachName = "Ethik", .Anteil = 35}}}
    }

    ''' <summary>Wendet `vorlage` auf die gewaehlten Klassenstufen an und
    ''' meldet in Klartext, was geschehen ist - die Zeilen landen in der
    ''' Zusammenfassung des Assistenten (Schritt 5).
    '''
    ''' Erwartet, dass die Platzhalter-Schueler bereits existieren: ohne
    ''' Kinder gibt es nichts zu verteilen, und eine leere Fachgruppe waere
    ''' ein Bestand, den niemand gewollt hat.</summary>
    Public Function Anwenden(bestand As Stammdatenbestand, vorlage As GruppenVorlage,
                             stufen As IEnumerable(Of Integer)) As List(Of String)
        Dim bericht As New List(Of String)
        If bestand Is Nothing OrElse vorlage Is Nothing Then Return bericht
        If vorlage.Teile.Count < 2 Then Return bericht

        Dim quelle = bestand.Faecher.FirstOrDefault(Function(f) f.Name = vorlage.QuellFach)
        If quelle Is Nothing Then
            bericht.Add($"Vorlage '{vorlage.Name}' uebersprungen: das Fach {vorlage.QuellFach} gibt es nicht.")
            Return bericht
        End If

        ' Die Qualifikationen der Quelle EINMAL merken - unten wird die
        ' Quelle womoeglich entfernt, und danach waere die Information weg.
        Dim quellLehrer = bestand.FachLehrerZuordnungen.
            Where(Function(z) z.FachName = vorlage.QuellFach).
            Select(Function(z) z.LehrerName).Distinct().ToList()

        For Each stufe In stufen.Distinct().OrderBy(Function(s) s)
            Dim fk = quelle.Klassenstufen.FirstOrDefault(Function(k) k.Klassenstufe = stufe)
            If fk Is Nothing Then
                bericht.Add($"Klassenstufe {stufe} uebersprungen: dort wird {vorlage.QuellFach} nicht unterrichtet.")
                Continue For
            End If

            Dim kinder = KinderDerStufe(bestand, stufe)
            If kinder.Count < vorlage.Teile.Count Then
                bericht.Add($"Klassenstufe {stufe} uebersprungen: dort gibt es weniger Kinder als Gruppen.")
                Continue For
            End If

            Dim verbund = $"{vorlage.Verbundname}-Kl{stufe}"
            Dim groessen = Aufteilen(kinder.Count, vorlage.Teile.Select(Function(t) t.Anteil).ToList())
            Dim ab = 0

            For i = 0 To vorlage.Teile.Count - 1
                Dim teil = vorlage.Teile(i)
                Dim ziel = FachSicherstellen(bestand, teil.FachName, quelle)
                ' Gleiche Wochenstunden und gleiches max_pro_tag wie die
                ' Quelle - der Verbund verlangt ausdruecklich gleiches
                ' wochenstunden_soll ueber alle seine Gruppen.
                If Not ziel.Klassenstufen.Any(Function(k) k.Klassenstufe = stufe) Then
                    ziel.Klassenstufen.Add(New FachKlassenstufe With {
                        .Klassenstufe = stufe, .WochenstundenSoll = fk.WochenstundenSoll, .MaxProTag = fk.MaxProTag})
                End If

                Dim g As New Gruppe With {
                    .Name = $"{teil.FachName}-Kl{stufe}", .Typ = "Fachgruppe",
                    .FachName = teil.FachName, .Klassenstufe = stufe, .Parallelverbund = verbund}
                For j = ab To ab + groessen(i) - 1
                    g.MitgliederSchuelerIds.Add(kinder(j).Id)
                Next
                ab += groessen(i)
                If Not bestand.Gruppen.Any(Function(x) x.Name = g.Name) Then bestand.Gruppen.Add(g)

                For Each lehrername In quellLehrer
                    QualifikationSicherstellen(bestand, lehrername, teil.FachName)
                Next
            Next

            quelle.Klassenstufen.Remove(fk)
            bericht.Add($"Klassenstufe {stufe}: {vorlage.QuellFach} aufgeteilt in " &
                        String.Join(", ", vorlage.Teile.Select(Function(t) t.FachName)) &
                        $" ({kinder.Count} Kinder, Verbund {verbund}).")
        Next

        ' Ist die Quelle auf KEINER Stufe mehr gefuehrt, verschwindet sie
        ' samt Qualifikationen - ein Fach ohne Klassenstufe waere ein
        ' Restposten, den spaeter niemand mehr einordnen kann.
        If quelle.Klassenstufen.Count = 0 Then
            bestand.Faecher.Remove(quelle)
            bestand.FachLehrerZuordnungen.RemoveAll(Function(z) z.FachName = vorlage.QuellFach)
            bericht.Add($"Das Fach {vorlage.QuellFach} wird nicht mehr gefuehrt und wurde entfernt.")
        End If

        bericht.AddRange(LehrkraefteErgaenzen(bestand, vorlage, quellLehrer))
        Return bericht
    End Function

    ''' <summary>Nach der Aufspaltung tragen GRUPPEN den Bedarf, nicht mehr
    ''' die Klassen - und drei parallele Gruppen je Stufe verlangen mehr
    ''' Lehrerstunden als Klassenunterricht bei zwei Zuegen. Deshalb wird
    ''' nachbemessen, statt es dem Nutzer als Infeasible zu hinterlassen.
    '''
    ''' Ehrliche Grenze: gerechnet wird gegen das Deputat der Lehrkraefte,
    ''' die diese Faecher unterrichten - unterrichtet eine davon noch
    ''' andere Faecher, ist ihr Deputat zu grosszuegig gezaehlt. Fuer den
    ''' Startbestand des Assistenten trifft das nicht zu (die
    ''' Religionslehrer des Templates fuehren nur Religion); die echte
    ''' Bilanz zeigt die Lehrkraefte-Maske (6.6).</summary>
    Private Function LehrkraefteErgaenzen(bestand As Stammdatenbestand, vorlage As GruppenVorlage,
                                          quellLehrer As List(Of String)) As List(Of String)
        Dim bericht As New List(Of String)
        Dim muster = bestand.Lehrkraefte.FirstOrDefault(Function(l) quellLehrer.Contains(l.Name))
        If muster Is Nothing Then Return bericht

        Dim deputat = Kennzahlen.DeputatVon(muster)
        If deputat <= 0 Then Return bericht

        Dim faecher = vorlage.Teile.Select(Function(t) t.FachName).ToList()
        Dim bedarf = faecher.Sum(Function(f) Kennzahlen.BedarfJeFach(bestand, f))
        Dim gedeckt = bestand.Lehrkraefte.Where(Function(l) quellLehrer.Contains(l.Name)).
                          Sum(Function(l) Kennzahlen.DeputatVon(l))
        If gedeckt >= bedarf Then Return bericht

        Dim praefix = Regex.Replace(muster.Name, "-\d+$", "")
        Dim fehlend = CInt(Math.Ceiling((bedarf - gedeckt) / deputat))
        Dim naechste = bestand.Lehrkraefte.Where(
            Function(l) l.Name.StartsWith(praefix & "-", StringComparison.Ordinal)).Count + 1
        For i = 0 To fehlend - 1
            Dim neuerName = $"{praefix}-{naechste + i}"
            bestand.Lehrkraefte.Add(New Lehrer With {
                .Name = neuerName, .DeputatSollstunden = muster.DeputatSollstunden,
                .Anrechnungsstunden = muster.Anrechnungsstunden,
                .KlassenlehrerFaehig = muster.KlassenlehrerFaehig})
            For Each f In faecher
                QualifikationSicherstellen(bestand, neuerName, f)
            Next
        Next
        bericht.Add($"{fehlend} zusaetzliche Lehrkraft(e) '{praefix}-…' ergaenzt: die parallelen " &
                    $"Gruppen verlangen {bedarf} Stunden, gedeckt waren {gedeckt:0}.")
        Return bericht
    End Function

    Private Function KinderDerStufe(bestand As Stammdatenbestand, stufe As Integer) As List(Of Schueler)
        Dim klassen = bestand.Klassen.Where(Function(k) k.Klassenstufe = stufe).
                          Select(Function(k) k.Name).ToHashSet(StringComparer.Ordinal)
        Return bestand.Schueler.Where(Function(s) klassen.Contains(s.Klasse)).
                   OrderBy(Function(s) s.Id, StringComparer.Ordinal).ToList()
    End Function

    Private Function FachSicherstellen(bestand As Stammdatenbestand, name As String, quelle As Fach) As Fach
        Dim f = bestand.Faecher.FirstOrDefault(Function(x) x.Name = name)
        If f Is Nothing Then
            f = New Fach With {.Name = name, .BlockLength = quelle.BlockLength, .Unbeliebt = quelle.Unbeliebt}
            bestand.Faecher.Add(f)
        End If
        Return f
    End Function

    Private Sub QualifikationSicherstellen(bestand As Stammdatenbestand, lehrername As String, fachname As String)
        If bestand.FachLehrerZuordnungen.Any(
            Function(z) z.LehrerName = lehrername AndAlso z.FachName = fachname) Then Return
        bestand.FachLehrerZuordnungen.Add(
            New FachLehrerZuordnung With {.LehrerName = lehrername, .FachName = fachname})
    End Sub

    ''' <summary>Verteilt `gesamt` Kinder nach Prozentanteilen. Jeder Teil
    ''' bekommt mindestens ein Kind, der Rundungsrest geht an den ersten -
    ''' eine leere Fachgruppe waere ein Fach ohne Lernende, das der Solver
    ''' trotzdem einplanen muesste.</summary>
    Friend Function Aufteilen(gesamt As Integer, anteile As List(Of Integer)) As List(Of Integer)
        Dim n = anteile.Count
        Dim ergebnis = Enumerable.Repeat(0, n).ToList()
        If gesamt <= 0 OrElse n = 0 Then Return ergebnis
        If gesamt < n Then
            For i = 0 To gesamt - 1
                ergebnis(i) = 1
            Next
            Return ergebnis
        End If

        Dim summe = anteile.Sum()
        If summe <= 0 Then summe = n
        Dim rest = gesamt
        For i = 1 To n - 1
            Dim anzahl = Math.Max(1, CInt(Math.Floor(gesamt * anteile(i) / summe)))
            ' Genug fuer die noch folgenden Teile uebrig lassen - sonst
            ' bekaeme der letzte null.
            If rest - anzahl < n - i Then anzahl = rest - (n - i)
            ergebnis(i) = anzahl
            rest -= anzahl
        Next
        ergebnis(0) = rest
        Return ergebnis
    End Function

End Module
