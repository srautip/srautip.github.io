' Umbenennen und Loeschen von Stammdaten-Objekten (Stufe F, Querschnitt
' aus gui-ui-konzept.md 7).
'
' "Namen sind Schluessel." Lehrkraft-, Fach-, Klassen- und Raumnamen
' referenzieren einander ueber den Namen (Wire-Format, arc42 8.7). Daraus
' folgt woertlich aus dem Konzept:
'   * Umbenennen kaskadiert automatisch ueber Qualifikationen, feste
'     Zuordnungen, Gruppen und Regeln - mit Vorschau ("12 Verweise werden
'     angepasst").
'   * Loeschen zeigt einen Konsequenzen-Dialog mit allen betroffenen
'     Objekten und bietet "mitloeschen" oder "abbrechen" - NIEMALS stilles
'     Verwaisen von Referenzen.
'
' Die Regel-Seite fragt dabei Validation.ReferenzenVon, statt die
' Feldzuordnung hier nachzubauen: eine vergessene Zuordnung ergaebe stumm
' verwaiste Verweise, die erst der naechste Solve-Lauf als "unbekannte
' Entity" meldet.
Imports TimetableCore

''' <summary>Was von einem Umbenennen oder Loeschen betroffen ist -
''' aufbereitet fuer die Vorschau, nicht fuer den Rechenkern.</summary>
Public NotInheritable Class Verweis
    ''' <summary>Wo der Verweis sitzt, z.B. "Qualifikation", "Feste
    ''' Zuordnung", "Regel", "Gruppe", "Klasse".</summary>
    Public Property Bereich As String = ""
    Public Property Beschreibung As String = ""
End Class

Public NotInheritable Class AenderungsFolgen
    Public Property Verweise As New List(Of Verweis)

    Public ReadOnly Property Anzahl As Integer
        Get
            Return Verweise.Count
        End Get
    End Property

    ''' <summary>Der Text der Vorschau: "12 Verweise werden angepasst".</summary>
    Public ReadOnly Property Zusammenfassung As String
        Get
            If Anzahl = 0 Then Return "Keine weiteren Verweise."
            Return $"{Anzahl} Verweis{If(Anzahl = 1, "", "e")} " &
                   String.Join(", ", Verweise.GroupBy(Function(v) v.Bereich).
                                              OrderBy(Function(g) g.Key).
                                              Select(Function(g) $"{g.Key}: {g.Count()}"))
        End Get
    End Property
End Class

''' <summary>Welche Art von Stammdatum gemeint ist. Die Zeichenketten
''' entsprechen den Entity-Arten des Wire-Formats, damit die Regel-Seite
''' ohne Uebersetzung auskommt.</summary>
Public Enum Stammart
    Lehrkraft
    Fach
    Klasse
    Raum
End Enum

Public Module Bestandspflege

    Private Function EntityArtVon(art As Stammart) As String
        Select Case art
            Case Stammart.Lehrkraft : Return "teachers"
            Case Stammart.Fach : Return "subjects"
            Case Stammart.Klasse : Return "classes"
            Case Else : Return "rooms"
        End Select
    End Function

    ''' <summary>Alle Stellen, die den Namen erwaehnen - OHNE etwas zu
    ''' aendern. Grundlage der Vorschau beim Umbenennen UND des
    ''' Konsequenzen-Dialogs beim Loeschen.</summary>
    Public Function Verweise(p As Projekt, art As Stammart, name As String) As AenderungsFolgen
        Dim f As New AenderungsFolgen()
        If p Is Nothing OrElse String.IsNullOrEmpty(name) Then Return f
        Dim b = p.Bestand

        Select Case art
            Case Stammart.Lehrkraft
                For Each z In b.FachLehrerZuordnungen.Where(Function(x) x.LehrerName = name)
                    f.Verweise.Add(New Verweis With {.Bereich = "Qualifikation", .Beschreibung = $"{name} unterrichtet {z.FachName}"})
                Next
                For Each z In b.FesteZuordnungen.Where(Function(x) x.LehrerName = name)
                    f.Verweise.Add(New Verweis With {.Bereich = "Feste Zuordnung", .Beschreibung = $"{z.KlasseName} {z.FachName}"})
                Next

            Case Stammart.Fach
                For Each z In b.FachLehrerZuordnungen.Where(Function(x) x.FachName = name)
                    f.Verweise.Add(New Verweis With {.Bereich = "Qualifikation", .Beschreibung = $"{z.LehrerName} unterrichtet {name}"})
                Next
                For Each z In b.FesteZuordnungen.Where(Function(x) x.FachName = name)
                    f.Verweise.Add(New Verweis With {.Bereich = "Feste Zuordnung", .Beschreibung = $"{z.LehrerName} in {z.KlasseName}"})
                Next
                For Each g In b.Gruppen.Where(Function(x) x.FachName = name)
                    f.Verweise.Add(New Verweis With {.Bereich = "Gruppe", .Beschreibung = g.Name})
                Next

            Case Stammart.Klasse
                For Each z In b.FesteZuordnungen.Where(Function(x) x.KlasseName = name)
                    f.Verweise.Add(New Verweis With {.Bereich = "Feste Zuordnung", .Beschreibung = $"{z.LehrerName} {z.FachName}"})
                Next
                For Each s In b.Schueler.Where(Function(x) x.Klasse = name)
                    f.Verweise.Add(New Verweis With {.Bereich = "Schueler", .Beschreibung = s.Id})
                Next

            Case Stammart.Raum
                ' Raeume werden ausschliesslich ueber Regeln referenziert
                ' (room_requirement/forbidden_slot) - im typisierten
                ' Stammdatenmodell gibt es keinen Verweis auf sie.
        End Select

        Dim entityArt = EntityArtVon(art)
        For i = 0 To p.Constraints.Count - 1
            ' .Where(...).Count statt .Count(...): List(Of T).Count ist in
            ' VB eine Eigenschaft und verschattet die LINQ-Ueberladung.
            Dim treffer = Validation.ReferenzenVon(p.Constraints(i)).
                Where(Function(r) r.EntityArt = entityArt AndAlso r.Wert = name).Count
            If treffer = 0 Then Continue For
            Dim typ = JsonHelpers.GetString(p.Constraints(i), "type")
            f.Verweise.Add(New Verweis With {
                .Bereich = "Regel",
                .Beschreibung = $"{typ} (Regel {i + 1})" & If(treffer > 1, $", {treffer}x erwaehnt", "")})
        Next

        Return f
    End Function

    ''' <summary>Benennt um und zieht ALLE Verweise mit. Liefert, was
    ''' angepasst wurde - die Oberflaeche zeigt es als Vorschau (vorher
    ''' ueber Verweise()) und als Bestaetigung (nachher).
    '''
    ''' Ein Name, den es schon gibt, wird abgelehnt: zwei Lehrkraefte
    ''' gleichen Namens waeren im Wire-Format nicht unterscheidbar, und der
    ''' Solver wuerde ihre Stunden stillschweigend zusammenlegen.</summary>
    Public Function BenenneUm(p As Projekt, art As Stammart, alt As String, neu As String) As AenderungsFolgen
        If String.IsNullOrWhiteSpace(neu) Then Throw New ArgumentException("Ein leerer Name ist kein Name.", NameOf(neu))
        If alt = neu Then Return New AenderungsFolgen()
        If NameVergeben(p, art, neu) Then
            Throw New InvalidOperationException($"'{neu}' ist bereits vergeben - Namen sind in diesem Modell Schluessel und muessen eindeutig bleiben.")
        End If

        Dim folgen = Verweise(p, art, alt)
        Dim b = p.Bestand

        Select Case art
            Case Stammart.Lehrkraft
                For Each l In b.Lehrkraefte.Where(Function(x) x.Name = alt) : l.Name = neu : Next
                For Each z In b.FachLehrerZuordnungen.Where(Function(x) x.LehrerName = alt) : z.LehrerName = neu : Next
                For Each z In b.FesteZuordnungen.Where(Function(x) x.LehrerName = alt) : z.LehrerName = neu : Next

            Case Stammart.Fach
                For Each fa In b.Faecher.Where(Function(x) x.Name = alt) : fa.Name = neu : Next
                For Each z In b.FachLehrerZuordnungen.Where(Function(x) x.FachName = alt) : z.FachName = neu : Next
                For Each z In b.FesteZuordnungen.Where(Function(x) x.FachName = alt) : z.FachName = neu : Next
                For Each g In b.Gruppen.Where(Function(x) x.FachName = alt) : g.FachName = neu : Next

            Case Stammart.Klasse
                For Each k In b.Klassen.Where(Function(x) x.Name = alt) : k.Name = neu : Next
                For Each z In b.FesteZuordnungen.Where(Function(x) x.KlasseName = alt) : z.KlasseName = neu : Next
                For Each s In b.Schueler.Where(Function(x) x.Klasse = alt) : s.Klasse = neu : Next

            Case Stammart.Raum
                For Each r In b.Raeume.Where(Function(x) x.Name = alt) : r.Name = neu : Next
        End Select

        Dim entityArt = EntityArtVon(art)
        For Each c In p.Constraints
            Validation.BenenneUm(c, entityArt, alt, neu)
        Next

        Return folgen
    End Function

    ''' <summary>Loescht das Objekt UND alles, was ohne es sinnlos waere -
    ''' Qualifikationen, feste Zuordnungen, Regeln, die es erwaehnen. Der
    ''' Aufrufer hat die Folgen vorher ueber Verweise() gezeigt und
    ''' bestaetigen lassen (Konzept 7: "mitloeschen oder abbrechen").
    '''
    ''' Nicht mitgeloescht werden Schueler einer geloeschten Klasse: ein
    ''' Kind ist kein Anhaengsel seiner Klasse. Seine Heimatklasse wird
    ''' geleert, damit es neu zugeordnet werden kann.</summary>
    Public Function Loesche(p As Projekt, art As Stammart, name As String) As AenderungsFolgen
        Dim folgen = Verweise(p, art, name)
        Dim b = p.Bestand

        Select Case art
            Case Stammart.Lehrkraft
                b.Lehrkraefte.RemoveAll(Function(x) x.Name = name)
                b.FachLehrerZuordnungen.RemoveAll(Function(x) x.LehrerName = name)
                b.FesteZuordnungen.RemoveAll(Function(x) x.LehrerName = name)

            Case Stammart.Fach
                b.Faecher.RemoveAll(Function(x) x.Name = name)
                b.FachLehrerZuordnungen.RemoveAll(Function(x) x.FachName = name)
                b.FesteZuordnungen.RemoveAll(Function(x) x.FachName = name)
                b.Gruppen.RemoveAll(Function(x) x.FachName = name)

            Case Stammart.Klasse
                b.Klassen.RemoveAll(Function(x) x.Name = name)
                b.FesteZuordnungen.RemoveAll(Function(x) x.KlasseName = name)
                For Each s In b.Schueler.Where(Function(x) x.Klasse = name) : s.Klasse = Nothing : Next

            Case Stammart.Raum
                b.Raeume.RemoveAll(Function(x) x.Name = name)
        End Select

        ' Regeln, die das Objekt erwaehnen, verlieren ihren Bezug. Bei
        ' Listenfeldern (allowed_rooms, classes) wird nur der Eintrag
        ' entfernt - die Regel bleibt sinnvoll, solange noch etwas
        ' drinsteht.
        Dim entityArt = EntityArtVon(art)
        Dim zuEntfernen As New List(Of Integer)
        For i = 0 To p.Constraints.Count - 1
            Dim c = p.Constraints(i)
            Dim refs = Validation.ReferenzenVon(c).Where(Function(r) r.EntityArt = entityArt AndAlso r.Wert = name).ToList()
            If refs.Count = 0 Then Continue For

            Dim einzelTreffer = refs.Where(Function(r) r.Position < 0).ToList()
            If einzelTreffer.Count > 0 Then
                zuEntfernen.Add(i)
                Continue For
            End If
            ' Nur Listenfelder betroffen: Eintraege herausnehmen (von
            ' hinten, damit die Positionen nicht verrutschen).
            For Each r In refs.OrderByDescending(Function(x) x.Position)
                c(r.Feld).AsArray().RemoveAt(r.Position)
            Next
            If refs.Any(Function(r) c(r.Feld).AsArray().Count = 0) Then zuEntfernen.Add(i)
        Next
        For Each i In zuEntfernen.OrderByDescending(Function(x) x)
            p.Constraints.RemoveAt(i)
        Next

        Return folgen
    End Function

    ''' <summary>Ist der Name in dieser Art schon vergeben? Grundlage der
    ''' Eindeutigkeitspruefung beim Anlegen UND beim Umbenennen.</summary>
    Public Function NameVergeben(p As Projekt, art As Stammart, name As String) As Boolean
        Dim b = p.Bestand
        Select Case art
            Case Stammart.Lehrkraft : Return b.Lehrkraefte.Any(Function(x) x.Name = name)
            Case Stammart.Fach : Return b.Faecher.Any(Function(x) x.Name = name)
            Case Stammart.Klasse : Return b.Klassen.Any(Function(x) x.Name = name)
            Case Else : Return b.Raeume.Any(Function(x) x.Name = name)
        End Select
    End Function

End Module
