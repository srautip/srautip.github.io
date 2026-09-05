' Lauf-Monitor und Hauptfenster-Zustand.
'
' Diese Tests erzeugen KEIN Fenster und kein WPF-Steuerelement - genau
' deshalb liegen Dialoge hinter IDialoge und der Zustand im ViewModel. Auf
' diesem Rechner gibt es nur eine getrennte Desktop-Sitzung; waere die
' Logik im Code-Behind, waere von der Oberflaeche nichts nachpruefbar.
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt
Imports TimetableYaml
Imports TimetableWorkflow

''' <summary>Dialoge fuer Tests: liefert vorbereitete Antworten und
''' protokolliert, was die Oberflaeche gefragt haette.</summary>
Friend NotInheritable Class TestDialoge
    Implements IDialoge

    Public Property OeffnenPfad As String
    Public Property SpeichernPfad As String
    Public Property OrdnerPfad As String
    Public Property Passwort As String = "geheim"
    Public ReadOnly Property Hinweise As New List(Of String)

    Public Function ProjektdateiOeffnen() As String Implements IDialoge.ProjektdateiOeffnen
        Return OeffnenPfad
    End Function

    Public Function ProjektdateiSpeichernUnter(vorschlag As String) As String Implements IDialoge.ProjektdateiSpeichernUnter
        Return SpeichernPfad
    End Function

    Public Function SchulordnerWaehlen() As String Implements IDialoge.SchulordnerWaehlen
        Return OrdnerPfad
    End Function

    Public Function PasswortAbfragen(titel As String, bestaetigen As Boolean) As String Implements IDialoge.PasswortAbfragen
        Return Passwort
    End Function

    ''' <summary>Was der Projekt-Assistent (6.1) liefern soll. Vorgabe
    ''' ist ein LEERES Projekt am `SpeichernPfad` - genau das, was
    ''' "Neues Projekt" vor Stufe F5 erzeugte. Tests, die den Assistenten
    ''' selbst meinen, setzen `AssistentEntwurf`; `AssistentBricht`
    ''' probt den Abbruch.</summary>
    Public Property AssistentEntwurf As ProjektEntwurf
    Public Property AssistentBricht As Boolean
    Public ReadOnly Property AssistentGefragt As Integer

    Public Function ProjektAssistent() As ProjektEntwurf Implements IDialoge.ProjektAssistent
        _AssistentGefragt += 1
        If AssistentBricht Then Return Nothing
        If AssistentEntwurf IsNot Nothing Then Return AssistentEntwurf
        If SpeichernPfad Is Nothing Then Return Nothing
        Return New ProjektEntwurf With {
            .Bestand = New TimetableCore.Stammdatenbestand(),
            .Pfad = SpeichernPfad, .Passwort = Passwort}
    End Function

    ''' <summary>Was der Freigabe-Dialog antworten soll. Vorgabe ist
    ''' ABBRUCH - eine Attrappe, die stillschweigend bestaetigt, wuerde
    ''' genau das Durchwinken einbauen, das die Freigabe ausschliessen soll.</summary>
    Public Property FreigabeAntwort As Freigabebestaetigung
    Public ReadOnly Property FreigabeVorlagen As New List(Of Freigabevorlage)

    Public Function FreigabeBestaetigen(vorlage As Freigabevorlage) As Freigabebestaetigung _
        Implements IDialoge.FreigabeBestaetigen
        FreigabeVorlagen.Add(vorlage)
        Return FreigabeAntwort
    End Function

    ''' <summary>Pfade fuer die Datei-Dialoge des Imports und der
    ''' Exporte (9.1). Nothing heisst Abbruch.</summary>
    Public Property DateiOeffnenPfad As String
    Public Property DateiSpeichernPfad As String

    Public Function DateiOeffnen(titel As String, filter As String) As String _
        Implements IDialoge.DateiOeffnen
        Return DateiOeffnenPfad
    End Function

    Public Function DateiSpeichernUnter(titel As String, filter As String, vorschlag As String) As String _
        Implements IDialoge.DateiSpeichernUnter
        Return DateiSpeichernPfad
    End Function

    Public Sub Hinweis(titel As String, text As String) Implements IDialoge.Hinweis
        Hinweise.Add($"{titel}: {text}")
    End Sub

    ''' <summary>Gestellte Fragen im Wortlaut. Der Loeschdialog muss die
    ''' Folgen NENNEN (Konzept 7, "niemals stilles Verwaisen") - das ist
    ''' nur pruefbar, wenn die Attrappe den Text aufhebt.</summary>
    Public ReadOnly Property Fragen As New List(Of String)

    ''' <summary>Steuerbar, damit auch der Nein-Fall pruefbar ist. Eine
    ''' Attrappe, die immer Ja sagt, kann nicht belegen, dass ein Nein
    ''' wirklich nichts aendert.</summary>
    Public Property FrageAntwort As Boolean = True

    Public Function Frage(titel As String, text As String) As Boolean Implements IDialoge.Frage
        Fragen.Add(text)
        Return FrageAntwort
    End Function

    ''' <summary>Welche Pflegemasken geoeffnet worden waeren, in der
    ''' Reihenfolge des Aufrufs. Die Attrappe oeffnet nichts - sie
    ''' belegt nur, dass die Karte oder der Kopf die richtige Maske
    ''' meint.</summary>
    Public ReadOnly Property Masken As New List(Of String)

    Public Sub StammdatenPflegen() Implements IDialoge.StammdatenPflegen
        Masken.Add("Stammdaten")
    End Sub

    Public Sub RegelnPflegen() Implements IDialoge.RegelnPflegen
        Masken.Add("Regeln")
    End Sub

    Public Sub KlassenbildungPflegen() Implements IDialoge.KlassenbildungPflegen
        Masken.Add("Klassenbildung")
    End Sub

    Public Sub SolverEinstellungenPflegen() Implements IDialoge.SolverEinstellungenPflegen
        Masken.Add("Solver")
    End Sub
End Class

<TestClass>
Public Class LaufMonitorTests

    Private Shared Function Meldung(stufe As Integer, gesamt As Integer, label As String, s As Double,
                                     Optional loesungen As Integer = 0,
                                     Optional ziel As Double? = Nothing,
                                     Optional schranke As Double? = Nothing) As SolveProgress
        Return New SolveProgress With {
            .Phase = SolvePhase.Stufe, .PhaseIndex = stufe, .PhaseCount = gesamt,
            .Label = label, .ElapsedS = s, .BudgetS = 100.0, .SolutionsFound = loesungen,
            .IncumbentObjective = ziel, .BestObjectiveBound = schranke}
    End Function

    <TestMethod>
    Public Sub MonitorUebernimmtStufeUndFortschritt()
        Dim m As New LaufMonitorViewModel()
        m.Starte()

        m.Uebernehmen(Meldung(2, 5, "Lehrereinsatz wird geplant", 25.0, loesungen:=0))

        Assert.AreEqual(2, m.Stufe)
        Assert.AreEqual(5, m.StufenGesamt)
        Assert.AreEqual("Lehrereinsatz wird geplant", m.Label)
        Assert.AreEqual(25.0, m.FortschrittProzent, 0.001)
        StringAssert.Contains(m.StatusZeile, "(2/5)")
        StringAssert.Contains(m.StatusZeile, "00:25")
    End Sub

    ''' <summary>Ein zurueckspringender Fortschrittsbalken ist so
    ''' irritierend, dass die Absicherung billiger ist als das Risiko -
    ''' auch wenn die Kernmeldungen selbst monoton sind.</summary>
    <TestMethod>
    Public Sub FortschrittUndLoesungszahlSpringenNieZurueck()
        Dim m As New LaufMonitorViewModel()
        m.Starte()

        m.Uebernehmen(Meldung(1, 5, "A", 30.0, loesungen:=3))
        m.Uebernehmen(Meldung(1, 5, "A", 10.0, loesungen:=1))

        Assert.AreEqual(30.0, m.VerstricheneS, 0.001)
        Assert.AreEqual(3, m.Loesungen)
    End Sub

    ''' <summary>Die Konvergenzkurve soll VERBESSERUNGEN zeigen, nicht
    ''' 500ms-Ticks: ohne diese Entdopplung waere sie nach einer Minute
    ''' eine waagerechte Linie aus 120 identischen Punkten.</summary>
    <TestMethod>
    Public Sub VerlaufZeichnetNurEchteVerbesserungenAuf()
        Dim m As New LaufMonitorViewModel()
        m.Starte()

        m.Uebernehmen(Meldung(1, 1, "A", 1.0, ziel:=500.0, schranke:=100.0))
        m.Uebernehmen(Meldung(1, 1, "A", 1.5, ziel:=500.0, schranke:=100.0))
        m.Uebernehmen(Meldung(1, 1, "A", 2.0, ziel:=400.0, schranke:=100.0))

        Assert.AreEqual(2, m.Verlauf.Count)
        Assert.AreEqual(500.0, m.Verlauf(0).ObjectiveValue)
        Assert.AreEqual(400.0, m.Verlauf(1).ObjectiveValue)
    End Sub

    ''' <summary>Die Optimalitaetsluecke LIVE - moeglich, seit der
    ''' Abbruchkanal BestObjectiveBound aus dem Callback mitfuehrt
    ''' (arc42 8.11). Vorher gab es sie erst nach dem Lauf.</summary>
    <TestMethod>
    Public Sub LueckeWirdAusZielwertUndSchrankeBerechnet()
        Dim m As New LaufMonitorViewModel()
        m.Starte()
        Assert.IsFalse(m.LueckeProzent.HasValue, "ohne Zwischenloesung darf keine Luecke behauptet werden")

        m.Uebernehmen(Meldung(1, 1, "A", 1.0, ziel:=200.0, schranke:=50.0))

        Assert.IsTrue(m.LueckeProzent.HasValue)
        Assert.AreEqual(75.0, m.LueckeProzent.Value, 0.001)
    End Sub

    <TestMethod>
    Public Sub AbbrechenSetztDasTokenUndBeendetDenLauf()
        Dim m As New LaufMonitorViewModel()
        Dim token = m.Starte()

        Assert.IsTrue(m.Laeuft)
        Assert.IsFalse(token.IsCancellationRequested)

        m.Abbrechen()

        Assert.IsTrue(token.IsCancellationRequested)
        Assert.IsTrue(m.Abgebrochen)

        m.Beende()
        Assert.IsFalse(m.Laeuft)
    End Sub

    <TestMethod>
    Public Sub StarteSetztDenVorigenLaufZurueck()
        Dim m As New LaufMonitorViewModel()
        m.Starte()
        m.Uebernehmen(Meldung(3, 5, "A", 40.0, loesungen:=7, ziel:=100.0, schranke:=10.0))
        m.Abbrechen()
        m.Beende()

        m.Starte()

        Assert.AreEqual(0, m.Verlauf.Count)
        Assert.AreEqual(0, m.Loesungen)
        Assert.AreEqual(0.0, m.VerstricheneS, 0.001)
        Assert.IsFalse(m.Abgebrochen)
        Assert.IsFalse(m.Zielwert.HasValue)
        Assert.IsTrue(m.Laeuft)
    End Sub

End Class

<TestClass>
Public Class HauptViewModelTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttgui-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(_ordner)
    End Sub

    <TestCleanup>
    Public Sub Abraeumen()
        If IO.Directory.Exists(_ordner) Then IO.Directory.Delete(_ordner, recursive:=True)
    End Sub

    Private Shared ReadOnly Property TestsRoot As String
        Get
            Dim dir = New IO.DirectoryInfo(AppContext.BaseDirectory)
            While dir IsNot Nothing
                Dim kandidat = IO.Path.Combine(dir.FullName, "tests", "bw-grundschule-beispiel")
                If IO.Directory.Exists(kandidat) Then Return IO.Path.Combine(dir.FullName, "tests")
                dir = dir.Parent
            End While
            Throw New InvalidOperationException("tests/-Verzeichnis nicht gefunden")
        End Get
    End Property

    Private Function NeuesModell(dialoge As TestDialoge) As HauptViewModel
        Return New HauptViewModel(dialoge, Function() Jetzt)
    End Function

    <TestMethod>
    Public Sub NeuesProjektWirdAngelegtUndGespeichert()
        Dim d As New TestDialoge With {.SpeichernPfad = IO.Path.Combine(_ordner, "neu.splanx")}
        Dim m = NeuesModell(d)

        m.Neu()

        Assert.IsTrue(m.ProjektOffen)
        Assert.IsFalse(m.Geaendert, "nach dem Speichern darf nichts mehr offen sein")
        Assert.IsTrue(IO.File.Exists(d.SpeichernPfad))
        Assert.IsTrue(ProjektDatei.IstProjektdatei(d.SpeichernPfad))
    End Sub

    ''' <summary>Der Weg "Bestehende Schule uebernehmen" (Konzept 9) -
    ''' Ordner rein, verschluesselte Projektdatei raus.</summary>
    <TestMethod>
    Public Sub SchulordnerWirdUebernommenUndVerschluesseltAbgelegt()
        Dim d As New TestDialoge With {
            .OrdnerPfad = IO.Path.Combine(TestsRoot, "bw-grundschule-beispiel"),
            .SpeichernPfad = IO.Path.Combine(_ordner, "gs.splanx")}
        Dim m = NeuesModell(d)

        m.Importieren()

        Assert.AreEqual(0, d.Hinweise.Count, "Uebernahme meldete einen Fehler: " & String.Join(" | ", d.Hinweise))
        Assert.IsTrue(m.ProjektOffen)
        Assert.IsTrue(m.Projekt.Bestand.Klassen.Count > 0)
        Assert.IsTrue(m.Projekt.Klassenbildung.Schueler.Count > 0)
        Assert.IsTrue(m.KannKlassenbildungRechnen, "mit Einschulungsdaten muss gerechnet werden koennen")

        ' Und die Datei ist wirklich eine Projektdatei mit demselben Inhalt.
        Dim erneut = ProjektDatei.Laden(d.SpeichernPfad, "geheim")
        Assert.AreEqual(m.Projekt.Bestand.Klassen.Count, erneut.Bestand.Klassen.Count)
        Assert.AreEqual(m.Projekt.Klassenbildung.Schueler.Count, erneut.Klassenbildung.Schueler.Count)
    End Sub

    <TestMethod>
    Public Sub OeffnenMitFalschemPasswortMeldetStattAbzustuerzen()
        Dim d As New TestDialoge With {.SpeichernPfad = IO.Path.Combine(_ordner, "p.splanx")}
        NeuesModell(d).Neu()

        Dim d2 As New TestDialoge With {.OeffnenPfad = d.SpeichernPfad, .Passwort = "falsch"}
        Dim m2 = NeuesModell(d2)
        m2.Oeffnen()

        Assert.IsFalse(m2.ProjektOffen)
        Assert.AreEqual(1, d2.Hinweise.Count)
        StringAssert.Contains(d2.Hinweise(0), "Passwort")
    End Sub

    <TestMethod>
    Public Sub FremdeDateiWirdAbgelehntOhnePasswortAbfrage()
        Dim fremd = IO.Path.Combine(_ordner, "fremd.splanx")
        IO.File.WriteAllText(fremd, "kein Projekt")
        Dim d As New TestDialoge With {.OeffnenPfad = fremd}
        Dim m = NeuesModell(d)

        m.Oeffnen()

        Assert.IsFalse(m.ProjektOffen)
        Assert.AreEqual(1, d.Hinweise.Count)
        StringAssert.Contains(d.Hinweise(0), "keine Projektdatei")
    End Sub

    ''' <summary>"Speichern ist immer moeglich, Rechnen nur bei gruener
    ''' Pruefung" (Konzept 1) - ohne Einschulungsdaten ist der
    ''' Rechnen-Befehl aus, der Speichern-Befehl nicht.</summary>
    <TestMethod>
    Public Sub OhneEinschulungsdatenIstRechnenAusAberSpeichernAn()
        Dim d As New TestDialoge With {.SpeichernPfad = IO.Path.Combine(_ordner, "leer.splanx")}
        Dim m = NeuesModell(d)
        m.Neu()

        Assert.IsFalse(m.KannKlassenbildungRechnen)
        Assert.IsFalse(m.KlassenbildungBefehl.CanExecute(Nothing))
        Assert.IsTrue(m.SpeichernBefehl.CanExecute(Nothing))
    End Sub

    ''' <summary>Der Durchstich: Projekt uebernehmen, rechnen, Board
    ''' bekommt eine Seite, Ergebnis wird Stand und Audit-Zeile.</summary>
    <TestMethod>
    Public Async Function KlassenbildungRechnenFuelltBoardStandUndAuditLog() As Task
        Dim d As New TestDialoge With {
            .OrdnerPfad = IO.Path.Combine(TestsRoot, "bw-grundschule-beispiel"),
            .SpeichernPfad = IO.Path.Combine(_ordner, "gs.splanx")}
        Dim m = NeuesModell(d)
        m.Importieren()
        ' Knapp halten - der Test belegt den ABLAUF, nicht die Planqualitaet.
        m.Projekt.Config.Klassenbildung = New KlassenbildungConfig With {.ZeitlimitS = 10.0, .NVarianten = 2, .MinDistanz = 4}
        m.Projekt.Config.NumWorkers = 1
        Dim staendeVorher = m.Projekt.Staende.Count
        Dim logVorher = m.Projekt.AuditLog.Count

        Await m.KlassenbildungRechnenAsync()

        Assert.AreEqual(0, d.Hinweise.Count, "Lauf meldete einen Fehler: " & String.Join(" | ", d.Hinweise))
        Assert.IsFalse(m.Monitor.Laeuft, "der Monitor blieb nach dem Lauf aktiv")
        Assert.IsTrue(m.Auslieferung.SeitenGroesse > 0, "das Board hat keine Seite bekommen")
        Assert.AreEqual(staendeVorher + 1, m.Projekt.Staende.Count, "kein Stand gesichert")
        Assert.AreEqual(logVorher + 1, m.Projekt.AuditLog.Count, "keine Audit-Zeile geschrieben")
        Assert.AreEqual("lauf", m.Projekt.AuditLog.Last().Aktion)
        Assert.IsTrue(m.Geaendert, "der Lauf hat das Projekt veraendert, ohne es zu markieren")
        Assert.AreEqual(Bereich.Klassenbildung, m.Bereich, "der Lauf muss ins Board wechseln")

        ' Die Seite ist der echte Viewer, nicht irgendein HTML.
        Dim seite = System.Text.Encoding.UTF8.GetString(m.Auslieferung.Antwort(m.Auslieferung.SeitenUrl).Inhalt)
        StringAssert.Contains(seite, "klassenbildung-data")
    End Function

    ''' <summary>Abbruch mit bereits fertiger Variante: die darf NICHT
    ''' weggeworfen werden - genau das sichert
    ''' KlassenbildungTopResult.Cancelled zu, und es waere widersinnig,
    ''' die fertige Variante zu verlieren, weil der Nutzer die zweite
    ''' nicht abwarten wollte.</summary>
    <TestMethod>
    Public Sub AbgebrochenerLaufMitTeilergebnisWirdVerwertet()
        Dim d As New TestDialoge With {
            .OrdnerPfad = IO.Path.Combine(TestsRoot, "bw-grundschule-beispiel"),
            .SpeichernPfad = IO.Path.Combine(_ordner, "gs.splanx")}
        Dim m = NeuesModell(d)
        m.Importieren()

        Dim cts As New CancellationTokenSource()
        Dim progress As New SofortProgress(Sub(p)
                                               If p.SolutionsFound >= 1 Then cts.Cancel()
                                           End Sub)
        ' Zeitlimit bewusst KNAPP: es begrenzt jede EINZELNE Variante, und
        ' Variante 1 muss vollstaendig fertig werden, bevor der Abbruch
        ' ueberhaupt etwas zu retten hat. Ein grosszuegiger Wert (der erste
        ' Entwurf hatte 600s) laesst genau diese erste Variante beliebig
        ' lange laufen - der Test haengt dann, statt schnell rot zu werden.
        Dim e = KlassenbildungLauf.Ausfuehren(m.Projekt.Klassenbildung,
            New KlassenbildungConfig With {.ZeitlimitS = 20.0, .NVarianten = 3, .MinDistanz = 4},
            seed:=42, numWorkers:=1, cancellationToken:=cts.Token, progress:=progress)

        Assert.IsTrue(e.Abgebrochen, "der Lauf wurde nicht abgebrochen - Testgrundlage fehlt")
        Assert.IsTrue(e.Geloeste.Count >= 1, "es wurde abgebrochen, bevor eine Variante fertig war")

        m.VerwerteErgebnis("gs", e)

        Assert.IsTrue(m.Auslieferung.SeitenGroesse > 0, "die fertige Variante wurde weggeworfen")
        Assert.AreEqual(1, m.Projekt.Staende.Count)
        StringAssert.Contains(m.Meldung, "abgebrochen")
    End Sub

    ''' <summary>Abbruch VOR der ersten Variante: dann gibt es nichts zu
    ''' zeigen, und die Oberflaeche muss das sagen statt eine leere Seite
    ''' anzuzeigen.</summary>
    <TestMethod>
    Public Sub AbgebrochenerLaufOhneErgebnisZeigtKeineLeereSeite()
        Dim d As New TestDialoge With {
            .OrdnerPfad = IO.Path.Combine(TestsRoot, "bw-grundschule-beispiel"),
            .SpeichernPfad = IO.Path.Combine(_ordner, "gs.splanx")}
        Dim m = NeuesModell(d)
        m.Importieren()

        Dim cts As New CancellationTokenSource()
        cts.Cancel()
        Dim e = KlassenbildungLauf.Ausfuehren(m.Projekt.Klassenbildung, New KlassenbildungConfig(),
                                              seed:=42, numWorkers:=1, cancellationToken:=cts.Token)

        m.VerwerteErgebnis("gs", e)

        Assert.AreEqual(0, m.Auslieferung.SeitenGroesse)
        Assert.AreEqual(0, m.Projekt.Staende.Count, "ein leerer Stand wurde gesichert")
        StringAssert.Contains(m.Meldung, "Abgebrochen")
    End Sub

End Class

''' <summary>Synchroner IProgress - Progress(Of T) waere im Testkontext
''' ueber den ThreadPool zugestellt und damit ein Wettrennen.</summary>
Friend NotInheritable Class SofortProgress
    Implements IProgress(Of SolveProgress)

    Private ReadOnly _aktion As Action(Of SolveProgress)

    Public Sub New(aktion As Action(Of SolveProgress))
        _aktion = aktion
    End Sub

    Public Sub Report(value As SolveProgress) Implements IProgress(Of SolveProgress).Report
        If _aktion IsNot Nothing Then _aktion(value)
    End Sub
End Class
