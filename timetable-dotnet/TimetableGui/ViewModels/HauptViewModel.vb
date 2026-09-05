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

''' <summary>Zustand eines Schritts auf der Startseite (gui-ui-konzept.md 8).
''' Bewusst SEMANTISCH und nicht als Zeichen: welches Symbol dafuer
''' steht, ist eine Frage der Ansicht und gehoert nicht ins ViewModel.
''' Die Stufen folgen der Statussprache des Projekts - erledigt, offen,
''' und "Warnung" fuer den Fall, den das Konzept als "!" fuehrt: da ist
''' etwas, aber es haelt noch nicht.</summary>
Public Enum SchrittStand
    Offen
    Bereit
    Erledigt
    Warnung
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
    ''' <summary>Fuehrt durch den Projekt-Assistenten (6.1) und liefert
    ''' den Entwurf, oder Nothing bei Abbruch. Der Assistent ist ein
    ''' Fenster und gehoert deshalb zu den uebrigen Dialogen - das
    ''' HauptViewModel soll auch weiterhin ohne WPF laufen.</summary>
    Function ProjektAssistent() As ProjektEntwurf
    ''' <summary>Der Freigabe-Dialog (klassenbildung-konzept 10.1): er
    ''' ZEIGT die verbleibenden Abweichungen und verlangt Name plus
    ''' aktive Bestaetigung. Nothing bei Abbruch.</summary>
    Function FreigabeBestaetigen(vorlage As Freigabevorlage) As Freigabebestaetigung
    ''' <summary>Beliebige Datei zum Lesen (CSV-Import, 9.1). Nothing
    ''' bei Abbruch.</summary>
    Function DateiOeffnen(titel As String, filter As String) As String
    ''' <summary>Beliebige Datei zum Schreiben (Exporte). Nothing bei
    ''' Abbruch.</summary>
    Function DateiSpeichernUnter(titel As String, filter As String, vorschlag As String) As String
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
        StundenplanBefehl = New Befehl(Sub() StundenplanRechnenAsync(), Function() KannStundenplanRechnen)
        WechsleBefehl = New Befehl(Sub(z) Bereich = CType(z, Bereich))
    End Sub

    Public ReadOnly Property Monitor As LaufMonitorViewModel
    Public ReadOnly Property Auslieferung As ViewerAuslieferung

    Public ReadOnly Property NeuBefehl As Befehl
    Public ReadOnly Property OeffnenBefehl As Befehl
    Public ReadOnly Property SpeichernBefehl As Befehl
    Public ReadOnly Property ImportierenBefehl As Befehl
    Public ReadOnly Property KlassenbildungBefehl As Befehl
    Public ReadOnly Property StundenplanBefehl As Befehl
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
                Melde(NameOf(KannStundenplanRechnen))
                MeldeSchritte()
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
            If _bereich = value Then Return
            _bereich = value
            ' Die Seite VOR dem Melden wechseln: das Fenster reagiert auf
            ' die Meldung und zeigt dann schon die richtige an.
            SeiteFuerBereichAusliefern()
            Melde(NameOf(Bereich))
        End Set
    End Property

    ''' <summary>Zwei Dashboards, aber nur EIN ViewerAuslieferung-Slot.
    ''' Ohne dieses Gedaechtnis zeigte der Wechsel von Klassenbildung auf
    ''' Stundenplan weiter das Board - dieselbe URL, alter Inhalt, und
    ''' nichts daran sichtbar falsch.</summary>
    Private ReadOnly _seiten As New Dictionary(Of Bereich, String)

    Private Sub SeiteFuerBereichAusliefern()
        Dim html As String = Nothing
        _seiten.TryGetValue(_bereich, html)
        Auslieferung.Setze(html)
    End Sub

    ''' <summary>Welcher Stand in welchem Dashboard steht. Ohne das
    ''' waere die Freigabe AUS DER SICHT heraus nicht moeglich: die Seite
    ''' kennt ihre Herkunft nicht, und der letzte Lauf waere die falsche
    ''' Antwort, sobald jemand einen aelteren Stand angesehen hat.</summary>
    Private ReadOnly _standIds As New Dictionary(Of Bereich, String)

    ''' <summary>Synthetischer PropertyChanged-Name (keine echte
    ''' Eigenschaft): feuert IMMER, wenn SeiteHinterlegen die aktuell
    ''' sichtbare Seite neu setzt - auch wenn Bereich sich dabei NICHT
    ''' aendert. Ohne dieses Signal blieb WebView2 nach einem zweiten
    ''' Rechnen aus einem bereits offenen Dashboard auf dem alten Stand
    ''' stehen: Bereichs Setter meldet nur bei echtem Wertwechsel
    ''' (Beobachtbar.Setze), und genau der bleibt beim erneuten Rechnen
    ''' im selben Dashboard aus - live erlebt bei der Klassenbildung
    ''' (Statuszeile zeigte den neuen Lauf, das Board noch den alten).</summary>
    Public Const AnzeigeAktualisiert As String = "AnzeigeAktualisiert"

    ''' <summary>Hinterlegt die Seite eines Bereichs samt zugehoerigem
    ''' Stand und liefert sie aus, falls dieser Bereich sichtbar ist.</summary>
    Private Sub SeiteHinterlegen(zielbereich As Bereich, html As String,
                                 Optional standId As String = Nothing)
        If html Is Nothing Then
            _seiten.Remove(zielbereich)
            _standIds.Remove(zielbereich)
        Else
            _seiten(zielbereich) = html
            If standId Is Nothing Then
                _standIds.Remove(zielbereich)
            Else
                _standIds(zielbereich) = standId
            End If
        End If
        If _bereich = zielbereich Then
            Auslieferung.Setze(html)
            Melde(AnzeigeAktualisiert)
        End If
    End Sub

    ''' <summary>Der Stand, den das aktuelle Dashboard zeigt - Nothing,
    ''' wenn dort nichts steht oder der Stand inzwischen geloescht
    ''' wurde.</summary>
    Public Function AngezeigterStand() As ProjektStand
        If Not ProjektOffen Then Return Nothing
        Dim id As String = Nothing
        If Not _standIds.TryGetValue(_bereich, id) Then Return Nothing
        Return _projekt.Staende.FirstOrDefault(Function(x) x.Id = id)
    End Function

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
    ' Startseite als Schrittleiste (gui-ui-konzept.md 8) - Stufe G6
    ' ---------------------------------------------------------------
    '
    ' Fuenf Schritte, jeder mit ECHTEM Zustand und einer Zeile Substanz.
    ' Die Skizze im Konzept ist woertlich gemeint: "[3] Rechnen ▶ zuletzt
    ' 22.08. 14:32 - 10 Loesungen, beste 198.6". Eine Leiste, die nur
    ' Ueberschriften zeigt, waere ein Inhaltsverzeichnis - sie soll aber
    ' sagen, wo man steht.
    '
    ' Das ANLEGEN eines Projekts ist bewusst kein Schritt: "neue Projekte
    ' starten bei [1] mit dem Assistenten-Ergebnis" (8), die Leiste setzt
    ' ein offenes Projekt also voraus. Die Einstiegsknoepfe stehen
    ' darueber.

    ''' <summary>Eine Zeile der Leiste - fertig aufbereitet, damit die
    ''' Ansicht nichts entscheidet.</summary>
    Public NotInheritable Class Startschritt
        Public Property Nummer As Integer
        Public Property Titel As String = ""
        Public Property Text As String = ""
        Public Property Stand As SchrittStand
        ''' <summary>Wohin der Klick fuehrt. "Jeder Schritt ist klickbar
        ''' (fuehrt in den Bereich)" (8).</summary>
        Public Property Ziel As Bereich
    End Class

    Public Function Schritte() As List(Of Startschritt)
        Return New List(Of Startschritt) From {
            New Startschritt With {.Nummer = 1, .Titel = "Stammdaten", .Ziel = Bereich.Stammdaten,
                                   .Stand = SchrittStammdaten, .Text = SchrittStammdatenText},
            New Startschritt With {.Nummer = 2, .Titel = "Regeln", .Ziel = Bereich.Regeln,
                                   .Stand = SchrittRegeln, .Text = SchrittRegelnText},
            New Startschritt With {.Nummer = 3, .Titel = "Rechnen", .Ziel = Bereich.Laeufe,
                                   .Stand = SchrittRechnen, .Text = SchrittRechnenText},
            New Startschritt With {.Nummer = 4, .Titel = "Entscheiden", .Ziel = Entscheidungsbereich(),
                                   .Stand = SchrittEntscheiden, .Text = SchrittEntscheidenText},
            New Startschritt With {.Nummer = 5, .Titel = "Freigabe & Export", .Ziel = Bereich.Laeufe,
                                   .Stand = SchrittFreigabe, .Text = SchrittFreigabeText}
        }
    End Function

    ' --- [2] Regeln -------------------------------------------------

    ''' <summary>Regeln sind OPTIONAL - viele Schulen kommen ohne aus.
    ''' "Keine Handregel" ist deshalb `Bereit` und nicht `Warnung`: eine
    ''' Warnung, die den Normalfall trifft, erzieht dazu, Warnungen zu
    ''' uebersehen.</summary>
    Public ReadOnly Property SchrittRegeln As SchrittStand
        Get
            If Not ProjektOffen Then Return SchrittStand.Offen
            If RegelBefunde().Count > 0 Then Return SchrittStand.Warnung
            Return If(_projekt.Constraints.Count > 0, SchrittStand.Erledigt, SchrittStand.Bereit)
        End Get
    End Property

    Public ReadOnly Property SchrittRegelnText As String
        Get
            If Not ProjektOffen Then Return "Erst ein Projekt anlegen oder oeffnen."
            Dim n = _projekt.Constraints.Count
            Dim basis = If(n = 0, "Keine Handregeln - der Plan rechnet allein aus den Stammdaten.",
                           $"{n} Handregel(n).")
            Dim befunde = RegelBefunde()
            If befunde.Count = 0 Then Return basis
            Return $"{basis} {befunde.Count} Hinweis(e): {befunde(0)}"
        End Get
    End Property

    ''' <summary>Was an den Handregeln zu beanstanden ist. Bewusst
    ''' dieselbe Quelle wie die Regelmaske - eine zweite Pruefung hier
    ''' waere eine zweite Meinung.</summary>
    Private Function RegelBefunde() As List(Of String)
        If Not ProjektOffen Then Return New List(Of String)
        Return Kennzahlen.RegelnAusserhalb(_projekt.Constraints, _projekt.Bestand.Tage,
                                           _projekt.Bestand.PeriodsPerDay)
    End Function

    ' --- [3] Rechnen ------------------------------------------------

    ''' <summary>Die Skizze aus 8: "zuletzt 22.08. 14:32 - 10 Loesungen".
    ''' Gelesen wird aus den STAENDEN, nicht aus dem Auslieferungs-Slot -
    ''' ein Lauf bleibt ein Lauf, auch wenn gerade ein anderes Dashboard
    ''' angezeigt wird.</summary>
    Public ReadOnly Property SchrittRechnenText As String
        Get
            If Not ProjektOffen Then Return "Erst ein Projekt anlegen oder oeffnen."
            Dim letzter = LetzterStand()
            If letzter Is Nothing Then
                Return "Noch nicht gerechnet. Klassenbildung F5, Stundenplan F6."
            End If
            Return $"Zuletzt {letzter.Erstellt:dd.MM. HH:mm} - {letzter.Label}. " &
                   $"{_projekt.Staende.Count} Stand/Staende gesichert."
        End Get
    End Property

    Private Function LetzterStand() As ProjektStand
        If Not ProjektOffen Then Return Nothing
        Return _projekt.Staende.OrderByDescending(Function(s) s.Erstellt).FirstOrDefault()
    End Function

    ' --- [4] Entscheiden --------------------------------------------

    ''' <summary>Entschieden ist, wenn eine Loesung als Arbeitsstand
    ''' markiert wurde (5). Ohne Stand gibt es nichts zu entscheiden.</summary>
    Public ReadOnly Property SchrittEntscheiden As SchrittStand
        Get
            If Not ProjektOffen OrElse LetzterStand() Is Nothing Then Return SchrittStand.Offen
            Return If(Arbeitsstand() IsNot Nothing, SchrittStand.Erledigt, SchrittStand.Bereit)
        End Get
    End Property

    Public ReadOnly Property SchrittEntscheidenText As String
        Get
            If Not ProjektOffen Then Return "Erst ein Projekt anlegen oder oeffnen."
            If LetzterStand() Is Nothing Then Return "Erst rechnen - dann gibt es etwas zu vergleichen."
            Dim gewaehlt = Arbeitsstand()
            Dim offen = _projekt.Klassenbildung.Fixierungen.Count
            Dim zusatz = If(offen = 0, "", $" {offen} Fixierung(en) gesetzt.")
            If gewaehlt Is Nothing Then
                Return "Im Dashboard vergleichen und eine Loesung als Arbeitsstand uebernehmen." & zusatz
            End If
            Return $"Arbeitsstand: {gewaehlt.Label}." & zusatz
        End Get
    End Property

    ''' <summary>Der Stand, in dem eine Loesung als Arbeitsstand markiert
    ''' ist (lauf.arbeitsstand, gesetzt aus dem Dashboard).</summary>
    Private Function Arbeitsstand() As ProjektStand
        If Not ProjektOffen Then Return Nothing
        Return _projekt.Staende.LastOrDefault(
            Function(s) s.Lauf IsNot Nothing AndAlso s.Lauf.ContainsKey("arbeitsstand"))
    End Function

    ''' <summary>Der Klick auf "Entscheiden" fuehrt in das Dashboard, das
    ''' tatsaechlich etwas zeigt. "Immer Stundenplan" waere bei einer
    ''' Schule, die nur die Klassenbildung nutzt, ein Klick ins Leere.</summary>
    Private Function Entscheidungsbereich() As Bereich
        Dim letzter = LetzterStand()
        If letzter IsNot Nothing AndAlso letzter.Klassenbildung IsNot Nothing Then Return Bereich.Klassenbildung
        Return Bereich.Stundenplan
    End Function

    ' --- [5] Freigabe & Export --------------------------------------

    Public ReadOnly Property SchrittFreigabe As SchrittStand
        Get
            If Not ProjektOffen Then Return SchrittStand.Offen
            If Freigaben().Count > 0 Then Return SchrittStand.Erledigt
            Return If(Arbeitsstand() IsNot Nothing, SchrittStand.Bereit, SchrittStand.Offen)
        End Get
    End Property

    Public ReadOnly Property SchrittFreigabeText As String
        Get
            If Not ProjektOffen Then Return "Erst ein Projekt anlegen oder oeffnen."
            Dim frei = Freigaben()
            If frei.Count = 0 Then Return "Noch nicht freigegeben."
            ' Klassenbildung und Stundenplan sind zwei Entscheidungen mit
            ' je eigenem Nachweis - beide nennen, nicht nur die letzte.
            Return String.Join("  ·  ", frei.Select(
                Function(s) $"{Freigabe.ArtVon(s)} freigegeben am {s.Erstellt:dd.MM.yyyy}"))
        End Get
    End Property

    Private Function Freigaben() As List(Of ProjektStand)
        If Not ProjektOffen Then Return New List(Of ProjektStand)
        Return _projekt.Staende.Where(AddressOf LaeufeViewModel.IstFreigabe).ToList()
    End Function
    ' ---------------------------------------------------------------

    Public ReadOnly Property SchrittProjekt As SchrittStand
        Get
            Return If(ProjektOffen, SchrittStand.Erledigt, SchrittStand.Bereit)
        End Get
    End Property

    Public ReadOnly Property SchrittStammdaten As SchrittStand
        Get
            If Not ProjektOffen Then Return SchrittStand.Offen
            Return If(PruefungGruen, SchrittStand.Erledigt, SchrittStand.Warnung)
        End Get
    End Property

    ''' <summary>"Erledigt" heisst: es gibt einen gesicherten STAND.
    '''
    ''' Frueher hing das am Auslieferungs-Slot ("wird gerade etwas
    ''' angezeigt?"). Mit zwei Dashboards war das falsch: ein Wechsel
    ''' auf das leere Board liess einen gerechneten Stundenplan als
    ''' ungerechnet erscheinen. Ein Lauf bleibt ein Lauf, unabhaengig
    ''' davon, wohin man gerade schaut.</summary>
    Public ReadOnly Property SchrittRechnen As SchrittStand
        Get
            If Not ProjektOffen Then Return SchrittStand.Offen
            If _projekt.Staende.Count > 0 Then Return SchrittStand.Erledigt
            Return If(KannKlassenbildungRechnen OrElse KannStundenplanRechnen,
                      SchrittStand.Bereit, SchrittStand.Offen)
        End Get
    End Property

    ''' <summary>Die Zusammenfassung, die das Konzept in seiner Skizze
    ''' zeigt ("8 Klassen, 12 Lehrkraefte, Pruefung gruen"). Nennt im
    ''' Fehlerfall den ERSTEN Befund im Klartext - eine blosse Zahl
    ''' zwaenge zum Weiterklicken, um ueberhaupt zu erfahren, worum es
    ''' geht.</summary>
    Public ReadOnly Property SchrittStammdatenText As String
        Get
            If Not ProjektOffen Then Return "Erst ein Projekt anlegen oder oeffnen."
            Dim b = _projekt.Bestand
            Dim basis = $"{b.Klassen.Count} Klassen, {b.Lehrkraefte.Count} Lehrkraefte, {b.Faecher.Count} Faecher"
            Dim fehler = StammdatenPruefen()
            If fehler.Count = 0 Then Return basis & " - Pruefung gruen."
            Return $"{basis} - {fehler.Count} offen: {fehler(0)}"
        End Get
    End Property

    ''' <summary>Sammelmeldung nach jedem Vorgang, der einen Schritt
    ''' weiterbewegt haben kann. Bewusst an EINER Stelle: sonst
    ''' vergisst der naechste Aufrufer eine der Eigenschaften, und die
    ''' Leiste zeigt stillschweigend einen alten Stand.</summary>
    Private Sub MeldeSchritte()
        Melde(NameOf(SchrittProjekt))
        Melde(NameOf(SchrittStammdaten))
        Melde(NameOf(SchrittRechnen))
        Melde(NameOf(SchrittStammdatenText))
        Melde(NameOf(SchrittRegeln))
        Melde(NameOf(SchrittRegelnText))
        Melde(NameOf(SchrittRechnenText))
        Melde(NameOf(SchrittEntscheiden))
        Melde(NameOf(SchrittEntscheidenText))
        Melde(NameOf(SchrittFreigabe))
        Melde(NameOf(SchrittFreigabeText))
        Melde(NameOf(PruefungGruen))
    End Sub

    ' ---------------------------------------------------------------
    ' Projekt
    ' ---------------------------------------------------------------

    ''' <summary>"Datei -> Neues Projekt" ist seit Stufe F5 der
    ''' ASSISTENT (6.1), nicht mehr die leere Datei. Begruendung steht
    ''' im Konzept: "der Nutzer sieht in Minuten ein erstes Ergebnis".
    ''' Ein leeres Projekt waere formal richtig und praktisch nutzlos -
    ''' vor dem ersten Rechnen laegen dann acht Masken.</summary>
    Public Sub Neu()
        Dim entwurf = _dialoge.ProjektAssistent()
        If entwurf Is Nothing Then Return

        Dim p As New Projekt()
        p.Manifest.SchulName = entwurf.Bestand.SchulName
        p.Manifest.Schuljahr = entwurf.Schuljahr
        p.Manifest.Angelegt = _jetzt()
        p.Manifest.Geaendert = _jetzt()
        p.Bestand = entwurf.Bestand
        ' Woher der Startbestand stammt, gehoert ins Protokoll: spaeter
        ' ist einer generierten Schule nicht mehr anzusehen, dass ihre
        ' Zahlen aus einem Template kommen und nicht aus dem Sekretariat.
        p.Protokolliere(Umgebung.Benutzer, "assistent",
                        "Projekt per Assistent erzeugt: " & String.Join(" | ", entwurf.Bericht),
                        _jetzt())

        Uebernehme(p, entwurf.Pfad, entwurf.Passwort)
        SpeichereAuf(entwurf.Pfad)
        Meldung = $"Neues Projekt angelegt: {p.Bestand.Klassen.Count} Klassen, {p.Bestand.Lehrkraefte.Count} Lehrkraefte."
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
        ' Ein Ergebnis gehoert dorthin, wo man es sieht. Auf dem
        ' regulaeren Weg hat der Lauf den Bereich schon gewechselt -
        ' hier steht es fuer den direkten Aufruf, damit beide Wege
        ' dasselbe tun.
        Bereich = Bereich.Klassenbildung
        If e.Geloeste.Count = 0 Then
            SeiteHinterlegen(Bereich.Klassenbildung, Nothing)
            Meldung = If(e.Abgebrochen, "Abgebrochen - noch keine Variante fertig.",
                         "Keine Loesung: " & String.Join(" | ", e.Meldungen))
            Return
        End If

        Dim zeitpunkt = _jetzt()
        Dim stand As New ProjektStand With {
            .Id = zeitpunkt.ToString("yyyy-MM-dd-HHmmss") & "-klassenbildung",
            .Label = $"Klassenbildung, {e.Geloeste.Count} Variante(n)",
            .Erstellt = zeitpunkt,
            .Klassenbildung = KlassenbildungLauf.BaueViewerJson(schule, e)
        }
        MeldeVerdraengte(_projekt.StandHinzufuegen(stand))
        ' Seite und Stand gehoeren zusammen: erst jetzt gibt es eine
        ' Id, unter der die Sicht ihren eigenen Stand freigeben kann.
        SeiteHinterlegen(Bereich.Klassenbildung,
                         ViewerInhalt.KlassenbildungSeite(schule, e), stand.Id)
        _projekt.Protokolliere(Umgebung.Benutzer, "lauf",
            $"Klassenbildung gerechnet: {e.Geloeste.Count} Variante(n), Konsens-Kern " &
            $"{e.Top.KonsensKern.Count}/{e.Eingabe.Schueler.Count}" &
            If(e.Abgebrochen, " (abgebrochen)", ""), zeitpunkt)
        Geaendert = True

        Meldung = $"{e.Geloeste.Count} Variante(n), Konsens-Kern {e.Top.KonsensKern.Count}/{e.Eingabe.Schueler.Count} Kinder" &
                  If(e.Abgebrochen, " (abgebrochen)", "")
        MeldeSchritte()
    End Sub


    ' ---------------------------------------------------------------
    ' Stundenplan rechnen (Stufe G1)
    ' ---------------------------------------------------------------

    ''' <summary>Anders als bei der Klassenbildung haengt hier nichts an
    ''' einer Einschulungsliste: der Stundenplan braucht grundsaetzlich
    ''' KEINE Einzelschueler (6.1). Die Schwelle ist deshalb der
    ''' Stammdatenkern - Klassen, Faecher, Lehrkraefte.</summary>
    Public ReadOnly Property KannStundenplanRechnen As Boolean
        Get
            If Not ProjektOffen OrElse Monitor.Laeuft Then Return False
            Dim b = _projekt.Bestand
            Return b.Klassen.Count > 0 AndAlso b.Faecher.Count > 0 AndAlso b.Lehrkraefte.Count > 0
        End Get
    End Property

    Public Async Function StundenplanRechnenAsync() As Task
        If Not KannStundenplanRechnen Then Return

        Dim fehler = StammdatenPruefen()
        If fehler.Count > 0 Then
            _dialoge.Hinweis("Stammdaten unvollstaendig",
                             "Vor dem Rechnen muessen diese Punkte geklaert sein:" & vbLf & vbLf &
                             String.Join(vbLf, fehler.Take(20).Select(Function(f) "- " & f)))
            Return
        End If

        Dim token = Monitor.Starte()
        Bereich = Bereich.Stundenplan
        Befehl.MeldeAenderung()
        Dim fortschritt As New Progress(Of SolveProgress)(Sub(p) Monitor.Uebernehmen(p))

        ' Der Bestand geht als REFERENZ in den Hintergrund-Task. Das ist
        ' vertretbar, weil die Masken waehrend eines Laufs gesperrt sind
        ' (Befehl.MeldeAenderung oben, Monitor.Laeuft) - eine Tiefkopie
        ' waere bei 3.000 Schuelern teurer als der Schutz wert ist.
        Dim bestand = _projekt.Bestand
        Dim regeln = _projekt.Constraints
        Dim cfg = _projekt.Config

        Try
            Dim e = Await Task.Run(Function() StundenplanLauf.Ausfuehren(bestand, regeln, cfg, token, fortschritt))
            VerwerteStundenplan(e)
        Catch ex As Exception
            _dialoge.Hinweis("Lauf fehlgeschlagen", ex.Message)
            Meldung = "Lauf fehlgeschlagen."
        Finally
            Monitor.Beende()
            Befehl.MeldeAenderung()
        End Try
    End Function

    ''' <summary>Wie bei der Klassenbildung: Seite an den Viewer, Ergebnis
    ''' als Stand, Audit-Zeile. Auch ein ABGEBROCHENER Lauf wird verwertet,
    ''' sofern er eine Loesung hervorgebracht hat.
    '''
    ''' Ein Lauf MIT Loesung, aber mit Verstoessen, ist kein Fehlschlag,
    ''' sondern ein Befund: die Loesung wird gezeigt und die Verstoesse
    ''' benannt. Sie wegzuwerfen naehme dem Nutzer genau die Information,
    ''' die er zum Nachbessern braucht.</summary>
    Friend Sub VerwerteStundenplan(e As LaufErgebnis)
        Bereich = Bereich.Stundenplan
        Dim seite = ViewerInhalt.StundenplanSeite(e)
        If seite Is Nothing Then
            SeiteHinterlegen(Bereich.Stundenplan, Nothing)
            ' Ein leeres Meldungen-Feld ist kein Versehen: die Solverstufe
            ' hat nichts zu melden, wenn sie schlicht nichts gefunden hat.
            ' "Keine Loesung (Solverlauf):" mit nichts dahinter waere die
            ' unbrauchbarste aller Meldungen.
            Dim grund = If(e.Meldungen.Count > 0,
                           String.Join(" | ", e.Meldungen.Take(3)),
                           "im Zeitbudget wurde keine gefunden - mehr Zeit geben oder Regeln lockern")
            Meldung = If(e.Abgebrochen, "Abgebrochen - noch keine Loesung fertig.",
                         $"Keine Loesung ({StufenName(e.Stufe)}): {grund}")
            Return
        End If



        Dim loesungen = e.Laeufe.Sum(Function(l) l.Result.Solutions.Count)
        Dim zeitpunkt = _jetzt()
        Dim stand As New ProjektStand With {
            .Id = zeitpunkt.ToString("yyyy-MM-dd-HHmmss") & "-stundenplan",
            .Label = $"Stundenplan, {loesungen} Loesung(en)",
            .Erstellt = zeitpunkt,
            .Stundenplan = StundenplanBericht.BaueStundentafelJson(e)
        }
        MeldeVerdraengte(_projekt.StandHinzufuegen(stand))
        SeiteHinterlegen(Bereich.Stundenplan, seite, stand.Id)

        Dim verstoesse = e.PlanVerstoesse.Count + e.LehrereinsatzVerstoesse
        _projekt.Protokolliere(Umgebung.Benutzer, "lauf",
            $"Stundenplan gerechnet: {loesungen} Loesung(en) aus {e.Einsaetze.Count} Zuteilung(en), " &
            $"{verstoesse} Verstoss/Verstoesse" & If(e.Abgebrochen, " (abgebrochen)", ""), zeitpunkt)
        Geaendert = True

        Meldung = $"{loesungen} Loesung(en) aus {e.Einsaetze.Count} Zuteilung(en)" &
                  If(verstoesse = 0, ", ohne Verstoesse", $", {verstoesse} Verstoss/Verstoesse") &
                  If(e.Abgebrochen, " (abgebrochen)", "")
        MeldeSchritte()
    End Sub

    ''' <summary>Die gescheiterte Stufe im Klartext. Ohne sie hiesse es nur
    ''' "keine Loesung" - und der Unterschied zwischen "die Stammdaten
    ''' widersprechen sich" und "der Solver fand nichts" ist genau der,
    ''' den der Nutzer wissen muss.</summary>
    Private Shared Function StufenName(stufe As LaufStufe) As String
        Select Case stufe
            Case LaufStufe.Stammdatenpruefung : Return "Stammdatenpruefung"
            Case LaufStufe.Lehrereinsatz : Return "Lehrereinsatzplanung"
            Case LaufStufe.Lehrereinsatzpruefung : Return "Pruefung des Lehrereinsatzes"
            Case LaufStufe.Szenarienaufbau : Return "Szenarienaufbau"
            Case LaufStufe.Stundenplan : Return "Solverlauf"
            Case Else : Return stufe.ToString()
        End Select
    End Function

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
        ' Was die Kurz-Parameter-Felder des Dashboards vorbelegen soll:
        ' womit zuletzt gerechnet WURDE. Ein leeres Feld zwaenge den
        ' Nutzer zu raten, was gerade gilt.
        Dim planParameter As JsonObject = Nothing
        If ProjektOffen Then
            planParameter = New JsonObject From {
                {"zeitbudget_s", _projekt.Config.SolveTimeLimitS},
                {"max_loesungen", _projekt.Config.MaxSolutions}}
        End If
        ''' Der Freigabestand des Standes, den diese Sicht zeigt. Ohne ihn
        ''' boete das Dashboard eine Freigabe an, die laengst erfolgt ist -
        ''' und der Nutzer erfuehre es erst durch den Hinweis danach.
        Dim freigabe As JsonObject = Nothing
        Dim stand = AngezeigterStand()
        If stand IsNot Nothing AndAlso LaeufeViewModel.IstFreigabe(stand) Then
            freigabe = stand.Lauf("freigabe").DeepClone().AsObject()
        End If
        Return Bruecke.StartSkript(If(ProjektOffen, _projekt.GuiState, Nothing), namen,
                                   planParameter, freigabe)
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

            Case "plan-uebernehmen"
                LoesungUebernehmen(n.Nutzlast)

            Case "plan-neu-rechnen"
                PlanNeuRechnenAsync(n.Nutzlast)

            Case "freigabe"
                FreigabeAusSicht()
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


    ''' <summary>"Diese Loesung als Arbeitsstand uebernehmen" (5). Der
    ''' Stand traegt die Markierung selbst, in seinem freien `lauf`-Objekt
    ''' - so ueberlebt sie das Speichern, ohne dass das Dateiformat eine
    ''' neue Zusage braucht.</summary>
    Friend Sub LoesungUebernehmen(nutzlast As JsonObject)
        Dim wahl = Bruecke.LiesPlanAuswahl(nutzlast)
        If Not wahl.HasValue Then Return

        Dim stand = _projekt.Staende.LastOrDefault(Function(s) s.Stundenplan IsNot Nothing)
        If stand Is Nothing Then
            _dialoge.Hinweis("Kein Stand", "Es gibt noch keinen Stundenplan-Stand, den man markieren koennte.")
            Return
        End If

        Dim zeitpunkt = _jetzt()
        If stand.Lauf Is Nothing Then stand.Lauf = New JsonObject()
        stand.Lauf("arbeitsstand") = New JsonObject From {
            {"zuteilung", wahl.Value.Zuteilung},
            {"loesung", wahl.Value.Loesung},
            {"gewaehlt_am", zeitpunkt.ToString("o")}
        }
        _projekt.Protokolliere(Umgebung.Benutzer, "arbeitsstand",
            $"Loesung {wahl.Value.Loesung} der Zuteilung {wahl.Value.Zuteilung} " &
            $"als Arbeitsstand markiert (Stand {stand.Id})", zeitpunkt)
        Geaendert = True
        Meldung = $"Arbeitsstand: Zuteilung {wahl.Value.Zuteilung}, Loesung {wahl.Value.Loesung}."
    End Sub

    ''' <summary>"Neu rechnen" mit Kurz-Parametern direkt aus dem Dashboard
    ''' (5). Die Werte werden in die Projekt-Config UEBERNOMMEN, nicht nur
    ''' fuer diesen Lauf verwendet: sonst zeigten die Solver-Einstellungen
    ''' (6.12) danach etwas anderes an als gerechnet wurde.</summary>
    Friend Async Function PlanNeuRechnenAsync(nutzlast As JsonObject) As Task
        If Not ProjektOffen OrElse Monitor.Laeuft Then Return

        Dim kurz = Bruecke.LiesKurzparameter(nutzlast)
        Dim uebernommen As New List(Of String)
        If kurz.Zeitbudget.HasValue Then
            _projekt.Config.SolveTimeLimitS = kurz.Zeitbudget.Value
            uebernommen.Add($"Zeitbudget {kurz.Zeitbudget.Value:0.#} s")
        End If
        If kurz.MaxLoesungen.HasValue Then
            _projekt.Config.MaxSolutions = kurz.MaxLoesungen.Value
            uebernommen.Add($"{kurz.MaxLoesungen.Value} Loesung(en)")
        End If

        If uebernommen.Count > 0 Then
            Geaendert = True
            _projekt.Protokolliere(Umgebung.Benutzer, "einstellung",
                "Aus dem Dashboard uebernommen: " & String.Join(", ", uebernommen), _jetzt())
        End If

        Await StundenplanRechnenAsync()
    End Function


    ''' <summary>Einen gesicherten Stand anzeigen, ohne neu zu rechnen
    ''' (6.13, "ansehen"). Der Bereich richtet sich nach der ART des
    ''' Standes, nicht danach, wo der Nutzer gerade steht - ein
    ''' Klassenbildungs-Stand im Stundenplan-Dashboard waere sinnlos.</summary>
    Public Sub StandAnzeigen(stand As ProjektStand)
        If stand Is Nothing Then Return
        Dim istKlassenbildung = stand.Klassenbildung IsNot Nothing
        Dim json = If(istKlassenbildung, stand.Klassenbildung, stand.Stundenplan)
        Dim seite = ViewerInhalt.AusGespeichertemJson(json, istKlassenbildung)
        If seite Is Nothing Then
            _dialoge.Hinweis("Nichts anzuzeigen", "Dieser Stand enthält kein Ergebnis.")
            Return
        End If

        Dim ziel = If(istKlassenbildung, Bereich.Klassenbildung, Bereich.Stundenplan)
        SeiteHinterlegen(ziel, seite, stand.Id)
        Bereich = ziel
        Meldung = $"Stand angezeigt: {stand.Label}"
    End Sub


    ''' <summary>Die Obergrenze verdraengt die aeltesten ungeschuetzten
    ''' Staende. Das darf nicht still geschehen: wer zehn Laeufe
    ''' vergleicht, muss erfahren, dass der erste nicht mehr da ist -
    ''' Projekt.StandHinzufuegen liefert die Ids genau dafuer zurueck,
    ''' und bis hierher hat sie niemand ausgewertet.</summary>
    Private Sub MeldeVerdraengte(verdraengt As List(Of String))
        If verdraengt Is Nothing OrElse verdraengt.Count = 0 Then Return
        _projekt.Protokolliere(Umgebung.Benutzer, "stand",
            $"Verdraengt (Obergrenze {_projekt.Manifest.MaxStaende}): " &
            String.Join(", ", verdraengt), _jetzt())
        _dialoge.Hinweis("Ältere Stände verdrängt",
            $"Die Obergrenze von {_projekt.Manifest.MaxStaende} Ständen ist erreicht. " &
            $"Entfernt wurde(n): {String.Join(", ", verdraengt)}." & vbLf & vbLf &
            "Freigegebene und geschützte Stände werden nie verdrängt.")
    End Sub

    ''' <summary>Freigabe direkt aus dem Dashboard (Nutzerwunsch
    ''' 26.08.2026). Freigegeben wird der Stand, den DIESE Sicht gerade
    ''' zeigt - nicht der letzte Lauf: wer sich einen aelteren Stand
    ''' angesehen hat, meint auch diesen.
    '''
    ''' Der Weg ist derselbe wie im Bereich Laeufe, samt Dialog mit
    ''' Abweichungen und Begruendungspflicht. Eine zweite, bequemere
    ''' Freigabe waere genau die Abkuerzung, die den Nachweis entwertet.</summary>
    Friend Sub FreigabeAusSicht()
        Dim stand = AngezeigterStand()
        If stand Is Nothing Then
            _dialoge.Hinweis("Kein Stand",
                "Diese Ansicht gehört zu keinem gespeicherten Stand. " &
                "Erst rechnen oder im Bereich Läufe einen Stand öffnen.")
            Return
        End If

        Dim historie As New LaeufeViewModel(_projekt, _dialoge, _jetzt)
        If Not historie.Freigeben(stand.Id) Then Return

        Geaendert = True
        Meldung = $"Freigegeben: {stand.Label}"
        MeldeSchritte()
    End Sub


    ''' <summary>Klarnamen-Export - die einzige gekennzeichnete Ausnahme
    ''' von der Pseudonymitaets-Grenze. Zwei Bedingungen, beide vom Plan
    ''' gefordert: Warndialog UND Audit-Eintrag. Der Eintrag ist der
    ''' wichtigere - ein Dialog sorgt nur dafuer, dass die Entscheidung
    ''' bewusst faellt, nachvollziehbar wird sie erst im Protokoll.</summary>
    Public Sub KlarnamenExportieren()
        If Not ProjektOffen Then Return
        Dim anzahl = _projekt.Mapping.Count
        If anzahl = 0 Then
            _dialoge.Hinweis("Nichts zu exportieren",
                "Das Projekt fuehrt keine Klarnamen - alle Kinder sind Platzhalter.")
            Return
        End If

        If Not _dialoge.Frage("Klarnamen exportieren", Klarnamenexport.Warnung(anzahl)) Then Return

        Dim vorschlag = $"{If(_projekt.Manifest.SchulName, "Schule")}-klarnamen.csv"
        Dim pfad = _dialoge.DateiSpeichernUnter("Klarnamen exportieren",
                                                "CSV-Datei (*.csv)|*.csv", vorschlag)
        If pfad Is Nothing Then Return

        Try
            ' Mit BOM: sonst zeigt Excel im deutschen Gebietsschema
            ' Mueller statt Mueller mit Umlaut - und jemand bessert die
            ' Datei von Hand nach, womit sie noch einmal mehr herumliegt.
            IO.File.WriteAllLines(pfad, Klarnamenexport.Zeilen(_projekt), New Text.UTF8Encoding(True))
        Catch ex As IO.IOException
            _dialoge.Hinweis("Export fehlgeschlagen", ex.Message)
            Return
        Catch ex As UnauthorizedAccessException
            _dialoge.Hinweis("Export fehlgeschlagen", ex.Message)
            Return
        End Try

        _projekt.Protokolliere(Umgebung.Benutzer, "export",
                               Klarnamenexport.Protokollzeile(anzahl, pfad), _jetzt())
        Geaendert = True
        Meldung = $"{anzahl} Klarname(n) exportiert - der Vorgang steht im Protokoll."
    End Sub
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
