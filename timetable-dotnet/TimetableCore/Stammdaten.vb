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
    ''' <summary>Phase 2.17: markiert ein im Kollegium unbeliebtes Fach -
    ''' fliesst als weiches Fairness-Ziel (niedrigste Prioritaet) in
    ''' Lehrereinsatzplanung.SolveLehrereinsatz ein: die Zuweisungen
    ''' unbeliebter Faecher sollen sich moeglichst gleichmaessig auf die
    ''' dafuer qualifizierten Lehrkraefte verteilen. Default False = keine
    ''' Auswirkung (heutiges Verhalten).</summary>
    Public Property Unbeliebt As Boolean = False
    Public Property Klassenstufen As New List(Of FachKlassenstufe)
End Class

Public NotInheritable Class Klasse
    Public Property Name As String
    Public Property Klassenstufe As Integer
    Public Property Schuelerzahl As Integer?
    ''' <summary>Phase 2.17: hebt die Klassenlehrer-Buendelungsgrenze
    ''' (siehe Lehrereinsatzplanung.vb, Nachtrag 2/3) fuer diese Klasse von
    ''' hoechstens 1 auf hoechstens 2 gleichzeitig aktive klassenlehrer-
    ''' faehige Kandidaten an - reales "Klassenlehrer-Tandem" (siehe
    ''' Phase-2.14-Recherche zum GSG-Fellbach-Modell). Default False =
    ''' heutiges Verhalten (genau 1).</summary>
    Public Property ErlaubtKlassenlehrerTandem As Boolean = False
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
    ''' <summary>Phase 2.17: Springerreserve/Vertretungspool - Stunden, die
    ''' bewusst NICHT durch Fachunterricht ausgeschoepft werden sollen
    ''' (Bereitschaft fuer kurzfristige Vertretung). Wird wie
    ''' Anrechnungsstunden vom nominalen Deputat abgezogen, BEVOR der
    ''' Deputat-Korridor gebildet wird - eine bewusst freigehaltene
    ''' Lehrkraft wird dadurch nicht fuer die Nicht-Ausschoepfung
    ''' bestraft. Default 0 = heutiges Verhalten.</summary>
    Public Property SpringerReserveStunden As Double = 0
    ''' <summary>Nothing = an allen Schultagen verfuegbar (Default fuer
    ''' Vollzeit-Lehrkraefte). Gesetzt = Teilzeit, nur an diesen Tagen im
    ''' Haus.</summary>
    Public Property VerfuegbareTage As List(Of String) = Nothing
    Public Property BevorzugteKlassenstufen As New List(Of Integer)
    Public Property KlassenlehrerFaehig As Boolean = True
    ''' <summary>Phase 2.17: weiches Ziel gegen Zersplitterung - Nothing =
    ''' kein Limit (heutiges Verhalten). Gesetzt: Ueberschreitung der
    ''' Anzahl unterschiedlicher Klassen, in denen diese Lehrkraft aktiv
    ''' unterrichtet, fliesst als Hinge-Loss-Malus in die
    ''' Lehrereinsatzplanung-Zielfunktion ein.</summary>
    Public Property MaxKlassen As Integer?
    ''' <summary>Wie MaxKlassen, aber fuer die Anzahl unterschiedlicher
    ''' Faecher.</summary>
    Public Property MaxFaecher As Integer?
End Class

''' <summary>Phase 2.19: ein Schueler - bewusst nur eine pseudonyme ID plus
''' Heimatklasse, kein Name-Feld (Datenschutz, fuers Scheduling nicht
''' gebraucht). Grundlage fuer das Mitgliedschaftsdatenmodell (siehe
''' `Gruppe`) - in diesem Schritt rein additiv, ohne jede Solver-/
''' Verifier-Wirkung (siehe docs/phase2-19-mitgliedschaftsmodell.md).</summary>
Public NotInheritable Class Schueler
    Public Property Id As String
    ''' <summary>Heimatklasse, referenziert Klasse.Name.</summary>
    Public Property Klasse As String
End Class

''' <summary>Phase 2.19: eine benannte, klassenunabhaengige Gruppe von
''' Schuelern (per ID referenziert) - deckt strukturell identisch alle drei
''' recherchierten Anwendungsfaelle ab (Fachgruppen wie Religion ev./
''' kath./Ethik, Foerdergruppen, Aufsichtsgruppen), ohne fuer jeden Fall
''' eine eigene Entitaetsklasse zu brauchen. `Typ` ist reiner Freitext zur
''' eigenen Einordnung (mirrort `Raum.Typ`) - hat in diesem Schritt KEINE
''' Solver-Bedeutung.</summary>
Public NotInheritable Class Gruppe
    Public Property Name As String
    ''' <summary>Freitext-Kategorie, informativ (z.B. "Fachgruppe",
    ''' "Foerderung", "Aufsicht") - keine Solver-Wirkung.</summary>
    Public Property Typ As String
    Public Property MitgliederSchuelerIds As New List(Of String)
    ''' <summary>Phase 2.20: welches Fach diese Gruppe unterrichtet.
    ''' Nothing (Default) = weiterhin komplett inert (Phase-2.19-Verhalten
    ''' unveraendert). Gesetzt = die Gruppe wird in
    ''' Lehrereinsatzplanung.SolveLehrereinsatz als eigenstaendige
    ''' Zuweisungseinheit gefuehrt statt jede Heimatklasse einzeln.</summary>
    Public Property FachName As String
    ''' <summary>Phase 2.20: Klassenstufe dieser Gruppe - noetig fuer
    ''' Lehrereinsatzplanung-Lookups (FachKlassenstufe/Praeferenzen), da
    ''' eine Gruppe mehrere echte Klassen umspannen kann und deshalb keiner
    ''' einzelnen Klasse.Klassenstufe zugeordnet werden kann.</summary>
    Public Property Klassenstufe As Integer?
    ''' <summary>Phase 2.20: Gruppen mit demselben (nicht-leeren) Wert
    ''' bilden gemeinsam eine Partition, die zeitgleich (auf identischen
    ''' Tag/Periode-Slots) stattfinden MUSS - z.B. alle drei Varianten
    ''' Religion-ev/Religion-kath/Ethik derselben Klassenstufe. Nothing
    ''' (Default) = keine Synchronisationsanforderung.</summary>
    Public Property Parallelverbund As String
End Class

''' <summary>Die "Zuordnung Faecher zu Lehrer" aus der Nutzeranfrage - die
''' Lehrbefaehigung/Einsatzfaehigkeit einer Lehrkraft fuer ein Fach. Nur
''' Paare, die hier gelistet sind, kommen als Kandidat fuer
''' Lehrereinsatzplanung.SolveLehrereinsatz in Frage.</summary>
Public NotInheritable Class FachLehrerZuordnung
    Public Property LehrerName As String
    Public Property FachName As String
    ''' <summary>Phase 2.17: markiert diese Zuordnung als fachfremden
    ''' Einsatz (Lehrkraft ist formal einsetzbar, aber nicht in ihrer
    ''' Hauptqualifikation) - bleibt weiterhin ein zulaessiger Kandidat
    ''' fuer Lehrereinsatzplanung.SolveLehrereinsatz, eine tatsaechliche
    ''' Zuweisung fliesst aber als weicher Malus in die Zielfunktion ein.
    ''' Default False = regulaer qualifiziert (heutiges Verhalten).</summary>
    Public Property Fachfremd As Boolean = False
End Class

''' <summary>Phase 2.26: eine explizite, harte Vorgabe "diese Lehrkraft
''' unterrichtet dieses Fach in dieser Klasse" - additiv zu den
''' bestehenden weichen Mechanismen (Lehrer.BevorzugteKlassenstufen,
''' Kontinuitaet-Vorjahreszuordnung). Anders als eine Praeferenz kann eine
''' FesteZuordnung NICHT durch die Zielfunktion "wegoptimiert" werden -
''' Lehrereinsatzplanung.SolveLehrereinsatz erzwingt sie als HARTEN
''' Constraint. Referenziert bewusst nur echte Klassen (kein Gruppen-
''' gefuehrtes Fach) - Gruppen-Unterstuetzung ist ein dokumentierter,
''' sauber trennbarer Folgeschritt.</summary>
Public NotInheritable Class FesteZuordnung
    Public Property LehrerName As String
    Public Property KlasseName As String
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
    ''' <summary>Phase 2.19: Mitgliedschaftsdatenmodell, Schritt 1. Property-
    ''' Name bewusst identisch zum Elementtyp (wie z.B. bei "Klassen As
    ''' List(Of Klasse)" der Property-Name vom Typnamen abweicht, waere das
    ''' hier "Schuelerschaft" gewesen - live verworfen: das YAML-Format
    ''' zielt auf einfache GitHub-Web-Bearbeitung ab, und "schueler:" ist
    ''' der YAML-Schluessel, den ein Autor tatsaechlich intuitiv erwartet,
    ''' nicht "schuelerschaft:" - VB.NET erlaubt einen Property-Namen
    ''' identisch zu seinem eigenen Elementtyp problemlos.</summary>
    Public Property Schueler As New List(Of Schueler)
    Public Property Gruppen As New List(Of Gruppe)
    ''' <summary>Phase 2.26: optionale, harte Lehrer-Klasse-Fach-Pinnungen -
    ''' leer = heutiges Verhalten unveraendert (rein additiv).</summary>
    Public Property FesteZuordnungen As New List(Of FesteZuordnung)
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

    ''' <summary>Phase 2.17: prueft, ob eine Lehrkraft mit eingeschraenkten
    ''' Praesenztagen (VerfuegbareTage) das Wochenpensum eines Fachs in
    ''' einer Klassenstufe strukturell unterbringen kann - eine Lehrkraft
    ''' ohne VerfuegbareTage-Einschraenkung (Vollzeit, Default) ist immer
    ''' kohaerent. Ohne gesetztes MaxProTag wird die Klassenobergrenze
    ''' `PeriodsPerDay` als effektive Tagesgrenze verwendet (mehr Stunden
    ''' als Perioden pro Tag kann keine Klasse an einem einzigen Tag
    ''' fassen, unabhaengig vom Fach). Wiederverwendet von
    ''' StammdatenValidation (Strukturpruefung) UND Lehrereinsatzplanung
    ''' (harter Kandidaten-Vorfilter) - dieselbe Formel an beiden
    ''' Stellen.</summary>
    Public Function IstTeilzeitKohaerent(lehrer As Lehrer, bestand As Stammdatenbestand, fk As FachKlassenstufe) As Boolean
        If lehrer.VerfuegbareTage Is Nothing Then Return True
        Dim effectiveMaxProTag = If(fk.MaxProTag.HasValue, fk.MaxProTag.Value, bestand.PeriodsPerDay)
        Return fk.WochenstundenSoll <= lehrer.VerfuegbareTage.Count * effectiveMaxProTag
    End Function

    ''' <summary>Phase 2.20: die distinct Heimatklassen (Klasse.Name) aller
    ''' Mitglieder einer Gruppe - z.B. {"1a","1b"} fuer eine Gruppe, die
    ''' Schueler beider Parallelklassen einer Klassenstufe kombiniert.
    ''' Genutzt von Lehrereinsatzplanung.vb (welche echten Klassen eine
    ''' Gruppen-Zuweisung betrifft) und BuildAssignmentConstraints (wo pro
    ''' echter Klasse die Session-Constraints emittiert werden).</summary>
    Public Function KlassenOfGruppe(bestand As Stammdatenbestand, gruppe As Gruppe) As List(Of String)
        Dim schuelerById = bestand.Schueler.ToDictionary(Function(s) s.Id)
        Return gruppe.MitgliederSchuelerIds.
            Where(Function(id) schuelerById.ContainsKey(id)).
            Select(Function(id) schuelerById(id).Klasse).
            Distinct().ToList()
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
