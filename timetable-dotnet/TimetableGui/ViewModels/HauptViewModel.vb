' Zustand und Aktionen des Hauptfensters (gui-ui-konzept.md 2 und 3).
'
' Enthaelt bewusst KEINE WPF-Dialoge: Datei- und Passwortabfragen kommen
' ueber IDialoge herein. Ohne diesen Schnitt waere von der Oberflaeche
' nichts pruefbar, weil jeder Test ein Fenster braeuchte - und ein
' Durchstich, dessen Ablauf man nur von Hand nachvollziehen kann, ist
' auf einem Server ohne Desktop-Sitzung gar nicht nachvollziehbar.
Imports System.Text.Json.Nodes
Imports System.Threading
Imports System.Threading.Tasks
Imports TimetableCore
Imports TimetableProjekt
Imports TimetableWorkflow

''' <summary>Die Bereiche der Seitenleiste. Reihenfolge = Reihenfolge im
''' Konzept (2): Start, Klassenbildung, Stundenplan, Stammdaten, Regeln,
''' Laeufe.</summary>
Public Enum Bereich
    Start
    Klassenbildung
    Stundenplan
    Stammdaten
    Regeln
    Laeufe
End Enum

''' <summary>Alles, wofuer die Oberflaeche den Nutzer fragen muss. Die
''' WPF-Umsetzung liegt im View, Tests setzen eine eigene ein.</summary>
Public Interface IDialoge
    ''' <summary>Pfad einer zu oeffnenden Projektdatei, Nothing bei
    ''' Abbruch.</summary>
    Function ProjektdateiOeffnen() As String
    Function ProjektdateiSpeichernUnter(vorschlag As String) As String
    Function SchulordnerWaehlen() As String
    ''' <summary>`bestaetigen` verlangt eine zweite Eingabe (beim Anlegen).
    ''' Nothing bei Abbruch.</summary>
    Function PasswortAbfragen(titel As String, bestaetigen As Boolean) As String
    Sub Hinweis(titel As String, text As String)
    Function Frage(titel As String, text As String) As Boolean
End Interface

Public NotInheritable Class HauptViewModel
    Inherits Beobachtbar

    Private ReadOnly _dialoge As IDialoge
    Private ReadOnly _jetzt As Func(Of DateTimeOffset)

    Private _projekt As Projekt
    Private _pfad As String
    Private _passwort As String
    Private _geaendert As Boolean
    Private _bereich As Bereich = Bereich.Start
    Private _meldung As String = ""

    Public Sub New(dialoge As IDialoge, Optional jetzt As Func(Of DateTimeOffset) = Nothing)
        _dialoge = dialoge
        _jetzt = If(jetzt, Function() DateTimeOffset.Now)

        Monitor = New LaufMonitorViewModel()
        Auslieferung = New ViewerAuslieferung()

        NeuBefehl = New Befehl(Sub() Neu(), Function() Not Monitor.Laeuft)
        OeffnenBefehl = New Befehl(Sub() Oeffnen(), Function() Not Monitor.Laeuft)
        SpeichernBefehl = New Befehl(Sub() Speichern(), Function() ProjektOffen AndAlso Not Monitor.Laeuft)
        ImportierenBefehl = New Befehl(Sub() Importieren(), Function() Not Monitor.Laeuft)
        KlassenbildungBefehl = New Befehl(Sub() KlassenbildungRechnenAsync(), Function() KannKlassenbildungRechnen)
        WechsleBefehl = New Befehl(Sub(z) Bereich = CType(z, Bereich))
    End Sub

    Public ReadOnly Property Monitor As LaufMonitorViewModel
    Public ReadOnly Property Auslieferung As ViewerAuslieferung

    Public ReadOnly Property NeuBefehl As Befehl
    Public ReadOnly Property OeffnenBefehl As Befehl
    Public ReadOnly Property SpeichernBefehl As Befehl
    Public ReadOnly Property ImportierenBefehl As Befehl
    Public ReadOnly Property KlassenbildungBefehl As Befehl
    Public ReadOnly Property WechsleBefehl As Befehl

    Public Property Projekt As Projekt
        Get
            Return _projekt
        End Get
        Private Set
            If Setze(_projekt, value) Then
                Melde(NameOf(ProjektOffen))
                Melde(NameOf(Titel))
                Melde(NameOf(KannKlassenbildungRechnen))
                Befehl.MeldeAenderung()
            End If
        End Set
    End Property

    Public ReadOnly Property ProjektOffen As Boolean
        Get
            Return _projekt IsNot Nothing
        End Get
    End Property

    ''' <summary>Ungespeicherte Aenderungen. Autosave ist im Konzept (7)
    ''' ausdruecklich abgelehnt - stattdessen dieser Indikator.</summary>
    Public Property Geaendert As Boolean
        Get
            Return _geaendert
        End Get
        Set
            If Setze(_geaendert, value) Then Melde(NameOf(Titel))
        End Set
    End Property

    Public Property Bereich As Bereich
        Get
            Return _bereich
        End Get
        Set
            Setze(_bereich, value)
        End Set
    End Property

    ''' <summary>Letzte Rueckmeldung an den Nutzer - was die Statusleiste
    ''' zeigt, solange kein Lauf aktiv ist.</summary>
    Public Property Meldung As String
        Get
            Return _meldung
        End Get
        Private Set
            Setze(_meldung, value)
        End Set
    End Property

    Public ReadOnly Property Titel As String
        Get
            If Not ProjektOffen Then Return "Schulplanung"
            Dim name = If(String.IsNullOrWhiteSpace(_projekt.Manifest.SchulName), "Unbenannt", _projekt.Manifest.SchulName)
            Dim jahr = If(String.IsNullOrWhiteSpace(_projekt.Manifest.Schuljahr), "", " " & _projekt.Manifest.Schuljahr)
            Return $"{If(Geaendert, "● ", "")}{name}{jahr} - Schulplanung"
        End Get
    End Property

    ''' <summary>Grundsatz aus dem Konzept (1): "Speichern ist immer
    ''' moeglich, Rechnen nur bei gruener Pruefung." Deshalb haengt DIESE
    ''' Eigenschaft an der Validierung, SpeichernBefehl nicht.</summary>
    Public ReadOnly Property KannKlassenbildungRechnen As Boolean
        Get
            If Not ProjektOffen OrElse Monitor.Laeuft Then Return False
            Return _projekt.Klassenbildung IsNot Nothing AndAlso _projekt.Klassenbildung.Schueler.Count > 0
        End Get
    End Property

    ''' <summary>Klartext-Fehler der Regelpruefung - die Oberflaeche listet
    ''' sie klickbar und deaktiviert Rechnen (Konzept 7).</summary>
    Public Function KlassenbildungPruefen() As List(Of String)
        If Not ProjektOffen Then Return New List(Of String) From {"Kein Projekt geoeffnet."}
        Return Klassenbildung.ValidateKlassenbildung(_projekt.Klassenbildung)
    End Function

    ''' <summary>Die globale Pruefung der Startseite (Konzept 7). Fuehrt
    ''' die BESTEHENDEN Validate-APIs aus - sie sind "die eine Wahrheit"
    ''' (Konzept 1), eine zweite Pruefung in der Oberflaeche waere eine
    ''' zweite Meinung.
    '''
    ''' Bewusst NUR die Referenz- und Strukturpruefung der Handregeln:
    ''' Vollstaendigkeitsmeldungen (fehlende teacher_subject_assignment)
    ''' entstehen erst durch die GENERIERTEN Regeln des Lehrereinsatzes
    ''' und waeren vor dem Lauf kein Fehler, sondern der Normalzustand.</summary>
    Public Function StammdatenPruefen() As List(Of String)
        If Not ProjektOffen Then Return New List(Of String) From {"Kein Projekt geoeffnet."}

        ' Vollstaendigkeit zuerst, und zwar HIER statt im Kern: die
        ' Validate-APIs pruefen Konsistenz, nicht Vollstaendigkeit - ein
        ' leeres Stammdatenmodell ist widerspruchsfrei und faellt dort
        ' durch. Der Kern scheitert erst spaet und technisch ("Keine
        ' teacher_subject_assignment-Constraints gefunden"); die
        ' Oberflaeche soll frueh und in Klartext sagen, was fehlt
        ' (Konzept 7: "Rechnen-Aktionen sind bei Fehlern deaktiviert und
        ' nennen den Grund").
        Dim fehlt As New List(Of String)
        If _projekt.Bestand.Klassen.Count = 0 Then fehlt.Add("Es ist keine Klasse angelegt.")
        If _projekt.Bestand.Faecher.Count = 0 Then fehlt.Add("Es ist kein Fach angelegt.")
        If _projekt.Bestand.Lehrkraefte.Count = 0 Then fehlt.Add("Es ist keine Lehrkraft angelegt.")
        If _projekt.Bestand.FachLehrerZuordnungen.Count = 0 Then fehlt.Add("Keine Lehrkraft ist fuer ein Fach qualifiziert.")
        If fehlt.Count > 0 Then Return fehlt

        Dim fehler = StammdatenValidation.ValidateStammdaten(_projekt.Bestand)
        If fehler.Count > 0 Then Return fehler

        If _projekt.Constraints.Count = 0 Then Return fehler
        Dim data As New JsonObject From {
            {"entities", Stammdaten.BuildEntitiesFragment(_projekt.Bestand)},
            {"constraints", New JsonArray(_projekt.Constraints.
                Select(Function(c) CType(c.DeepClone(), JsonNode)).ToArray())}
        }
        Return Validation.ValidateEntities(data).
            Where(Function(e) e.Contains("keine bekannte Entity") OrElse e.Contains("nicht in ")).ToList()
    End Function

    ''' <summary>"Speichern ist immer moeglich, Rechnen nur bei gruener
    ''' Pruefung" (Konzept 1). Ein Projekt DARF unfertig sein - nur
    ''' rechnen darf man damit nicht.</summary>
    Public ReadOnly Property PruefungGruen As Boolean
        Get
            Return ProjektOffen AndAlso StammdatenPruefen().Count = 0
        End Get
    End Property

    ' ---------------------------------------------------------------
    ' Projekt
    ' ---------------------------------------------------------------

    Public Sub Neu()
        Dim passwort = _dialoge.PasswortAbfragen("Passwort fuer das neue Projekt", bestaetigen:=True)
        If passwort Is Nothing Then Return
        Dim pfad = _dialoge.ProjektdateiSpeichernUnter("Neues Projekt.splanx")
        If pfad Is Nothing Then Return

        Dim p As New Projekt()
        p.Manifest.Angelegt = _jetzt()
        p.Manifest.Geaendert = _jetzt()
        Uebernehme(p, pfad, passwort)
        SpeichereAuf(pfad)
        Meldung = "Neues Projekt angelegt."
    End Sub

    Public Sub Oeffnen()
        Dim pfad = _dialoge.ProjektdateiOeffnen()
        If pfad Is Nothing Then Return
        If Not ProjektDatei.IstProjektdatei(pfad) Then
            _dialoge.Hinweis("Oeffnen", $"{IO.Path.GetFileName(pfad)} ist keine Projektdatei dieser Anwendung.")
            Return
        End If
        Dim passwort = _dialoge.PasswortAbfragen("Passwort", bestaetigen:=False)
        If passwort Is Nothing Then Return

        Try
            Uebernehme(ProjektDatei.Laden(pfad, passwort), pfad, passwort)
            Geaendert = False
            Meldung = $"{IO.Path.GetFileName(pfad)} geoeffnet."
        Catch ex As ProjektEntschluesselungException
            _dialoge.Hinweis("Oeffnen fehlgeschlagen", ex.Message)
        Catch ex As ProjektFormatException
            _dialoge.Hinweis("Oeffnen fehlgeschlagen", ex.Message)
        End Try
    End Sub

    Public Sub Speichern()
        If Not ProjektOffen Then Return
        Dim ziel = _pfad
        If ziel Is Nothing Then
            ziel = _dialoge.ProjektdateiSpeichernUnter("Projekt.splanx")
            If ziel Is Nothing Then Return
        End If
        SpeichereAuf(ziel)
        Meldung = $"Gespeichert: {IO.Path.GetFileName(ziel)}"
    End Sub

    ''' <summary>Uebernahme eines bestehenden tests/&lt;schule&gt;-Ordners -
    ''' der Einstieg "Bestehende Schule uebernehmen" (Konzept 9).</summary>
    Public Sub Importieren()
        Dim ordner = _dialoge.SchulordnerWaehlen()
        If ordner Is Nothing Then Return
        Dim passwort = _dialoge.PasswortAbfragen("Passwort fuer das neue Projekt", bestaetigen:=True)
        If passwort Is Nothing Then Return
        Dim ziel = _dialoge.ProjektdateiSpeichernUnter(IO.Path.GetFileName(ordner.TrimEnd(IO.Path.DirectorySeparatorChar)) & ".splanx")
        If ziel Is Nothing Then Return

        Try
            Dim p = ProjektOrdner.Importieren(ordner, _jetzt())
            Uebernehme(p, ziel, passwort)
            SpeichereAuf(ziel)
            Meldung = $"{p.Bestand.Klassen.Count} Klassen, {p.Bestand.Lehrkraefte.Count} Lehrkraefte, {p.Klassenbildung.Schueler.Count} Einschulungskinder uebernommen."
        Catch ex As Exception
            _dialoge.Hinweis("Uebernahme fehlgeschlagen", ex.Message)
        End Try
    End Sub

    Private Sub Uebernehme(p As Projekt, pfad As String, passwort As String)
        _pfad = pfad
        _passwort = passwort
        Projekt = p
        Geaendert = True
        Melde(NameOf(Titel))
    End Sub

    Private Sub SpeichereAuf(pfad As String)
        _projekt.Manifest.Geaendert = _jetzt()
        ProjektDatei.Speichern(_projekt, pfad, _passwort)
        _pfad = pfad
        Geaendert = False
    End Sub

    ' ---------------------------------------------------------------
    ' Klassenbildung rechnen
    ' ---------------------------------------------------------------

    ''' <summary>Startet den Lauf im Hintergrund und kehrt sofort zurueck -
    ''' "die GUI bleibt bedienbar" (Konzept, Vorbemerkung 4). Der
    ''' Progress(Of T) wird HIER erzeugt, also auf dem UI-Thread, und
    ''' marshallt seine Meldungen von selbst dorthin zurueck.</summary>
    Public Async Function KlassenbildungRechnenAsync() As Task
        If Not KannKlassenbildungRechnen Then Return

        Dim fehler = KlassenbildungPruefen()
        If fehler.Count > 0 Then
            _dialoge.Hinweis("Regelwerk unvollstaendig",
                             "Vor dem Rechnen muessen diese Punkte geklaert sein:" & vbLf & vbLf &
                             String.Join(vbLf, fehler.Select(Function(f) "- " & f)))
            Return
        End If

        Dim token = Monitor.Starte()
        Bereich = Bereich.Klassenbildung
        Befehl.MeldeAenderung()
        Dim fortschritt As New Progress(Of SolveProgress)(Sub(p) Monitor.Uebernehmen(p))

        Dim eingabe = _projekt.Klassenbildung
        Dim cfg = _projekt.Config
        Dim schule = _projekt.Manifest.SchulName

        Try
            Dim e = Await Task.Run(Function() KlassenbildungLauf.Ausfuehren(
                eingabe, cfg.Klassenbildung, cfg.Seed, cfg.NumWorkers, token, fortschritt))
            VerwerteErgebnis(schule, e)
        Catch ex As Exception
            _dialoge.Hinweis("Lauf fehlgeschlagen", ex.Message)
            Meldung = "Lauf fehlgeschlagen."
        Finally
            Monitor.Beende()
            Befehl.MeldeAenderung()
        End Try
    End Function

    ''' <summary>Verwertet das Lauf-Ergebnis: Seite an den Viewer,
    ''' Ergebnis als Stand ins Projekt, Audit-Zeile. Auch ein ABGEBROCHENER
    ''' Lauf wird verwertet, sofern er Varianten hervorgebracht hat - genau
    ''' das sichert KlassenbildungTopResult.Cancelled zu, und es waere
    ''' widersinnig, die fertige Variante wegzuwerfen, weil der Nutzer die
    ''' zweite nicht mehr abwarten wollte.</summary>
    Friend Sub VerwerteErgebnis(schule As String, e As KlassenbildungLaufErgebnis)
        If e.Geloeste.Count = 0 Then
            Auslieferung.Setze(Nothing)
            Meldung = If(e.Abgebrochen, "Abgebrochen - noch keine Variante fertig.",
                         "Keine Loesung: " & String.Join(" | ", e.Meldungen))
            Return
        End If

        Auslieferung.Setze(ViewerInhalt.KlassenbildungSeite(schule, e))

        Dim zeitpunkt = _jetzt()
        Dim stand As New ProjektStand With {
            .Id = zeitpunkt.ToString("yyyy-MM-dd-HHmmss") & "-klassenbildung",
            .Label = $"Klassenbildung, {e.Geloeste.Count} Variante(n)",
            .Erstellt = zeitpunkt,
            .Klassenbildung = KlassenbildungLauf.BaueViewerJson(schule, e)
        }
        _projekt.StandHinzufuegen(stand)
        _projekt.Protokolliere(Umgebung.Benutzer, "lauf",
            $"Klassenbildung gerechnet: {e.Geloeste.Count} Variante(n), Konsens-Kern " &
            $"{e.Top.KonsensKern.Count}/{e.Eingabe.Schueler.Count}" &
            If(e.Abgebrochen, " (abgebrochen)", ""), zeitpunkt)
        Geaendert = True

        Meldung = $"{e.Geloeste.Count} Variante(n), Konsens-Kern {e.Top.KonsensKern.Count}/{e.Eingabe.Schueler.Count} Kinder" &
                  If(e.Abgebrochen, " (abgebrochen)", "")
    End Sub

    ' ---------------------------------------------------------------
    ' Bruecke zum Board (U5)
    ' ---------------------------------------------------------------

    ''' <summary>Das Skript, das vor dem Laden der Seite injiziert wird:
    ''' gesicherter Board-Zustand plus Anzeige-Map. Die Map ist der
    ''' einzige Weg, auf dem Klarnamen in die Seite gelangen - dort
    ''' werden sie ausschliesslich in den DOM gerendert.</summary>
    Public Function BrueckenStartSkript() As String
        Dim namen As New Dictionary(Of String, String)
        If ProjektOffen Then
            For Each m In _projekt.Mapping
                Dim anzeige = $"{m.Vorname} {m.Nachname}".Trim()
                If anzeige.Length > 0 Then namen(m.Id) = anzeige
            Next
        End If
        Return Bruecke.StartSkript(If(ProjektOffen, _projekt.GuiState, Nothing), namen)
    End Function

    ''' <summary>Nimmt eine Nachricht des Boards entgegen. Unbekannte
    ''' Typen und neuere Versionen werden bewusst STILL ignoriert statt
    ''' zu werfen: eine aeltere, als Artifact veroeffentlichte Seite
    ''' (CLAUDE.md) darf den Host nicht abschiessen.</summary>
    Friend Sub VerarbeiteBrueckenNachricht(json As String)
        Dim n = BrueckenNachricht.Lesen(json)
        If n Is Nothing OrElse Not ProjektOffen Then Return
        If n.Version > BrueckenNachricht.AktuelleVersion Then Return

        Select Case n.Typ
            Case "zustand"
                ' Ersetzt die localStorage-Rolle der Vorlage: der Zustand
                ' wandert nach gui-state.json in der Projektdatei
                ' (Datenhaltung 7.6).
                _projekt.GuiState = n.Nutzlast
                Geaendert = True

            Case "neu-rechnen"
                NeuRechnenAsync(n.Nutzlast)
        End Select
    End Sub

    ''' <summary>U5, der geschlossene Loop: Pins und Haertungen aus dem
    ''' Board in den Projektbestand schreiben und neu rechnen. Damit
    ''' entfaellt der Weg "YAML-Block kopieren -> CLI aufrufen" - und zwar
    ''' NUR der; Board, Bewertung und Trichter bleiben unveraendert
    ''' (gui-ui-konzept.md 4).
    '''
    ''' Die Fixierungsliste ERSETZT die bisherige vollstaendig: sie
    ''' enthaelt die YAML-Bestandsfixierungen bereits mit (das Board
    ''' fuehrt sie als `herkunft: bestehend`), und ein Anhaengen wuerde
    ''' sie verdoppeln.</summary>
    Friend Async Function NeuRechnenAsync(nutzlast As JsonObject) As Task
        If Not ProjektOffen OrElse Monitor.Laeuft Then Return

        Dim fixierungen = Bruecke.LiesFixierungen(nutzlast)
        _projekt.Klassenbildung.Fixierungen.Clear()
        _projekt.Klassenbildung.Fixierungen.AddRange(fixierungen)
        Dim gehaertet = Bruecke.WendeHaertungenAn(nutzlast, _projekt.Klassenbildung)
        Geaendert = True

        _projekt.Protokolliere(Umgebung.Benutzer, "fixierung",
            $"Aus dem Board uebernommen: {fixierungen.Count} Fixierung(en)" &
            If(gehaertet > 0, $", {gehaertet} Regel(n) auf hart gesetzt", "") &
            " - Neuberechnung gestartet", _jetzt())

        Await KlassenbildungRechnenAsync()
    End Function

End Class

''' <summary>Umgebungsangaben, die ins Audit-Log gehoeren. "Wer" ist bei
''' einem Rechner und einer Person (Datenhaltung A5) der angemeldete
''' Benutzer - beim Projektanlegen erfragen waere die genauere Variante
''' und ist fuer eine spaetere Stufe vorgemerkt.</summary>
Friend Module Umgebung
    Friend ReadOnly Property Benutzer As String
        Get
            Return Environment.UserName
        End Get
    End Property
End Module
