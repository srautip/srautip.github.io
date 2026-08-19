' Phase 2.15: Stammdatenverwaltung - dauerhaft verwaltbare Schul-Grunddaten
' (Klassenstufen, Klassen, Raeume, Lehrkraefte, Faecher je Klassenstufe,
' Fach-Lehrer-Zuordnung, Deputate), im Unterschied zum wegwerfbaren
' `entities`/`constraints`-JSON, das pro Solve-Aufruf konstruiert wird (siehe
' Models.vb). Bewusst ein TYPISIERTES Domaenenmodell statt des rohen
' JsonObject-Musters der Constraints - Stammdaten sind auf Dauer angelegte
' Verwaltungsdaten, kein Wegwerf-Szenario, ein typisiertes Modell ist hier
' GUI-Databinding-freundlicher (siehe docs/arc42-architecture.md Abschnitt
' 8.7, das genau diesen spaeteren Schritt bereits vorsieht).
'
' Ein Stammdatenbestand repraesentiert GENAU EINE konkrete Schule (eine
' Schulart, ein Bundesland) - siehe Nutzerentscheidung "JSON-Dateien"
' (Phase 2.15 Feinplanung): eine Schule = eine JSON-Datei. Das
' `Bundesland`/`Schulart`-Feld traegt trotzdem die Erweiterbarkeit auf
' kuenftige Stufen (andere Bundeslaender/Schularten) mit, ohne dass dafuer
' bereits ein schulübergreifendes Katalog-Register existieren muesste.
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Nodes

Public NotInheritable Class Klassenstufe
    Public Property Nummer As Integer
    Public Property Bezeichnung As String
End Class

''' <summary>Ein Fach hat pro Klassenstufe eine eigene Standard-
''' Wochenstundenzahl - die Kontingentstundentafel legt in BW nur die
''' GESAMTSTUNDENZAHL ueber den Bildungsgang fest, die Schule verteilt
''' selbst auf einzelne Klassenstufen (siehe Phase-2.15-Recherche in
''' docs/phase2-15-lehrereinsatzplanung.md) - dieses Feld traegt genau
''' diese schuleigene Verteilungsentscheidung.</summary>
Public NotInheritable Class FachKlassenstufe
    Public Property Klassenstufe As Integer
    Public Property WochenstundenSoll As Integer
    Public Property MaxProTag As Integer?
End Class

Public NotInheritable Class Fach
    Public Property Name As String
    ''' <summary>Optionale Doppelstunden-Empfehlung (z.B. Sport, NaWi) -
    ''' identische Bedeutung wie `consecutive_required.block_length` im
    ''' bestehenden Constraint-Format, siehe BuildAssignmentConstraints in
    ''' Lehrereinsatzplanung.vb.</summary>
    Public Property BlockLength As Integer?
    Public Property Klassenstufen As New List(Of FachKlassenstufe)
End Class

Public NotInheritable Class Klasse
    Public Property Name As String
    Public Property Klassenstufe As Integer
    Public Property Schuelerzahl As Integer?
End Class

Public NotInheritable Class Raum
    Public Property Name As String
    ''' <summary>Freitext-Typ-Tag (z.B. "Turnhalle", "NaWi", "Musikraum",
    ''' leer/Nothing fuer generische Raeume) - wiederverwendbar fuer
    ''' `room_requirement.allowed_rooms`-Ableitung.</summary>
    Public Property Typ As String
End Class

Public NotInheritable Class Lehrer
    Public Property Name As String
    Public Property DeputatSollstunden As Double
    ''' <summary>Deputatsermaessigung (z.B. fuer eine Funktionsstelle) -
    ''' wird vom nominalen Deputat abgezogen, bevor der Lehrereinsatz-
    ''' Solver den tatsaechlichen Soll-Korridor bildet.</summary>
    Public Property Anrechnungsstunden As Double = 0
    ''' <summary>Nothing = an allen Schultagen verfuegbar (Default fuer
    ''' Vollzeit-Lehrkraefte). Gesetzt = Teilzeit, nur an diesen Tagen im
    ''' Haus.</summary>
    Public Property VerfuegbareTage As List(Of String) = Nothing
    Public Property BevorzugteKlassenstufen As New List(Of Integer)
    Public Property KlassenlehrerFaehig As Boolean = True
End Class

''' <summary>Die "Zuordnung Faecher zu Lehrer" aus der Nutzeranfrage - die
''' Lehrbefaehigung/Einsatzfaehigkeit einer Lehrkraft fuer ein Fach. Nur
''' Paare, die hier gelistet sind, kommen als Kandidat fuer
''' Lehrereinsatzplanung.SolveLehrereinsatz in Frage.</summary>
Public NotInheritable Class FachLehrerZuordnung
    Public Property LehrerName As String
    Public Property FachName As String
End Class

Public NotInheritable Class Stammdatenbestand
    Public Property SchulName As String
    Public Property Bundesland As String = "BW"
    Public Property Schulart As String
    Public Property Tage As New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"}
    Public Property PeriodsPerDay As Integer = 6
    Public Property Klassenstufen As New List(Of Klassenstufe)
    Public Property Faecher As New List(Of Fach)
    Public Property Klassen As New List(Of Klasse)
    Public Property Raeume As New List(Of Raum)
    Public Property Lehrkraefte As New List(Of Lehrer)
    Public Property FachLehrerZuordnungen As New List(Of FachLehrerZuordnung)
End Class

Public Module Stammdaten

    Private ReadOnly SerializerOptions As New JsonSerializerOptions With {
        .WriteIndented = True,
        .PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    }

    Public Function SerializeStammdaten(bestand As Stammdatenbestand) As String
        Return JsonSerializer.Serialize(bestand, SerializerOptions)
    End Function

    Public Function DeserializeStammdaten(json As String) As Stammdatenbestand
        Return JsonSerializer.Deserialize(Of Stammdatenbestand)(json, SerializerOptions)
    End Function

    Public Sub SaveStammdaten(bestand As Stammdatenbestand, path As String)
        File.WriteAllText(path, SerializeStammdaten(bestand))
    End Sub

    Public Function LoadStammdaten(path As String) As Stammdatenbestand
        Return DeserializeStammdaten(File.ReadAllText(path))
    End Function

    ''' <summary>Alle Faecher, die laut Stammdaten in dieser Klassenstufe
    ''' gefuehrt werden (haben einen FachKlassenstufe-Eintrag fuer sie).</summary>
    Public Function FaecherOfKlassenstufe(bestand As Stammdatenbestand, klassenstufe As Integer) As List(Of Fach)
        Return bestand.Faecher.Where(Function(f) f.Klassenstufen.Any(Function(fk) fk.Klassenstufe = klassenstufe)).ToList()
    End Function

    ''' <summary>Das FachKlassenstufe-Detail (Wochenstunden/MaxProTag) fuer
    ''' ein Fach in einer bestimmten Klassenstufe, oder Nothing falls das
    ''' Fach dort nicht gefuehrt wird.</summary>
    Public Function WochenstundenFuer(fach As Fach, klassenstufe As Integer) As FachKlassenstufe
        Return fach.Klassenstufen.FirstOrDefault(Function(fk) fk.Klassenstufe = klassenstufe)
    End Function

    ''' <summary>Alle laut FachLehrerZuordnungen fuer ein Fach qualifizierten
    ''' Lehrkraefte.</summary>
    Public Function LehrerFuerFach(bestand As Stammdatenbestand, fachName As String) As List(Of Lehrer)
        Dim qualifiziert = New HashSet(Of String)(
            bestand.FachLehrerZuordnungen.Where(Function(z) z.FachName = fachName).Select(Function(z) z.LehrerName))
        Return bestand.Lehrkraefte.Where(Function(l) qualifiziert.Contains(l.Name)).ToList()
    End Function

    ''' <summary>Projiziert einen Stammdatenbestand in das
    ''' entities-JSON-Fragment, das die bestehende Solver.Solve()/
    ''' BuildCoreModel-Pipeline bereits konsumiert (classes/teachers/
    ''' subjects/rooms/timeslots) - reine Ableitung, kein neues Konzept:
    ''' Faecher werden zu subjects, Klassen zu classes, Lehrkraefte zu
    ''' teachers, Raeume zu rooms, jeweils nur ueber den Namen.</summary>
    Public Function BuildEntitiesFragment(bestand As Stammdatenbestand) As JsonObject
        Return New JsonObject From {
            {"classes", New JsonArray(bestand.Klassen.Select(Function(k) CType(k.Name, JsonNode)).ToArray())},
            {"teachers", New JsonArray(bestand.Lehrkraefte.Select(Function(l) CType(l.Name, JsonNode)).ToArray())},
            {"subjects", New JsonArray(bestand.Faecher.Select(Function(f) CType(f.Name, JsonNode)).ToArray())},
            {"rooms", New JsonArray(bestand.Raeume.Select(Function(r) CType(r.Name, JsonNode)).ToArray())},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray(bestand.Tage.Select(Function(d) CType(d, JsonNode)).ToArray())},
                {"periods_per_day", bestand.PeriodsPerDay}
            }}
        }
    End Function

End Module
