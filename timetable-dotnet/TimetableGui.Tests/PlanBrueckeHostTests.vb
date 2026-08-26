' Host-Seite der Stundentafel-Bruecke (Stufe G2).
'
' Die Seite selbst ist in TimetableViewer.Tests/PlanBrueckeTests geprueft
' (echter Browser). Hier geht es um das, was der Host mit den Nachrichten
' macht - und darum, dass eine manipulierte Seite hoechstens Unsinn
' bewirkt, nie einen Absturz oder einen Lauf ohne Ende.
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt
Imports TimetableWorkflow
Imports TimetableYaml

<TestClass>
Public Class PlanBrueckeHostTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 24, 14, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttpb-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(_ordner)
    End Sub

    <TestCleanup>
    Public Sub Abraeumen()
        If IO.Directory.Exists(_ordner) Then IO.Directory.Delete(_ordner, recursive:=True)
    End Sub

    Private Shared Function Nutzlast(json As String) As JsonObject
        Return JsonNode.Parse(json).AsObject()
    End Function

    ' ===============================================================
    ' Die Leser
    ' ===============================================================

    ''' <summary>Die Seite ist HTML - ein manipuliertes Feld darf
    ''' hoechstens eine dumme Einstellung ergeben. Ein Zeitbudget von
    ''' 10^9 Sekunden waere ein Lauf ohne Ende.</summary>
    <TestMethod>
    Public Sub KurzparameterWerdenGekapptStattAbgewiesen()
        Dim gross = Bruecke.LiesKurzparameter(Nutzlast("{""zeitbudget_s"": 1000000000, ""max_loesungen"": 99999}"))
        Assert.AreEqual(3600.0, gross.Zeitbudget.Value)
        Assert.AreEqual(200, gross.MaxLoesungen.Value)

        Dim klein = Bruecke.LiesKurzparameter(Nutzlast("{""zeitbudget_s"": -5, ""max_loesungen"": 0}"))
        Assert.AreEqual(1.0, klein.Zeitbudget.Value)
        Assert.AreEqual(1, klein.MaxLoesungen.Value)
    End Sub

    ''' <summary>Ein `value="45"` aus einem Eingabefeld kommt als
    ''' ZEICHENKETTE an. Das als Fehler zu behandeln waere eine
    ''' Fallunterscheidung, die der Nutzer ausbaden muesste.</summary>
    <TestMethod>
    Public Sub KurzparameterLesenAuchZeichenketten()
        Dim k = Bruecke.LiesKurzparameter(Nutzlast("{""zeitbudget_s"": ""45"", ""max_loesungen"": ""7""}"))
        Assert.AreEqual(45.0, k.Zeitbudget.Value)
        Assert.AreEqual(7, k.MaxLoesungen.Value)
    End Sub

    ''' <summary>Nichts mitgegeben heisst: die Projekt-Config gilt
    ''' unveraendert - nicht "nimm den Standardwert".</summary>
    <TestMethod>
    Public Sub FehlendeKurzparameterBleibenNothing()
        Dim leer = Bruecke.LiesKurzparameter(Nutzlast("{}"))
        Assert.IsFalse(leer.Zeitbudget.HasValue)
        Assert.IsFalse(leer.MaxLoesungen.HasValue)
        Assert.IsFalse(Bruecke.LiesKurzparameter(Nothing).Zeitbudget.HasValue)

        Dim murks = Bruecke.LiesKurzparameter(Nutzlast("{""zeitbudget_s"": ""viel""}"))
        Assert.IsFalse(murks.Zeitbudget.HasValue)
    End Sub

    <TestMethod>
    Public Sub PlanAuswahlWeistUnsinnigeIndizesAb()
        Assert.IsFalse(Bruecke.LiesPlanAuswahl(Nutzlast("{""zuteilung"": 0, ""loesung"": 1}")).HasValue)
        Assert.IsFalse(Bruecke.LiesPlanAuswahl(Nutzlast("{""loesung"": 3}")).HasValue)
        Assert.IsFalse(Bruecke.LiesPlanAuswahl(Nothing).HasValue)

        Dim gut = Bruecke.LiesPlanAuswahl(Nutzlast("{""zuteilung"": 2, ""loesung"": 5}"))
        Assert.AreEqual(2, gut.Value.Zuteilung)
        Assert.AreEqual(5, gut.Value.Loesung)
    End Sub

    ' ===============================================================
    ' Der Host
    ' ===============================================================

    Private Function MiniProjekt() As (Modell As HauptViewModel, Dialoge As TestDialoge)
        Dim entwurf As New ProjektEntwurf With {
            .Bestand = Scaffold.Baue("BW", "Grundschule", 1, 4, 1, "Minischule"),
            .Pfad = IO.Path.Combine(_ordner, "mini.splanx"),
            .Passwort = "geheim12"}
        Dim d As New TestDialoge With {.AssistentEntwurf = entwurf}
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Neu()
        m.Projekt.Config = New RunConfig With {
            .LehrereinsatzTimeLimitS = 30.0, .SolveTimeLimitS = 30.0,
            .MaxSolutions = 1, .NumWorkers = 1, .Seed = 42}
        Return (m, d)
    End Function

    ''' <summary>"Markiert die Loesung im Projekt (Audit-Eintrag) und macht
    ''' sie zum Default fuer Berichte/Exporte" (5). Der Stand traegt die
    ''' Markierung in seinem freien `lauf`-Objekt - dadurch ueberlebt sie
    ''' das Speichern, ohne dass das Dateiformat etwas Neues zusagen
    ''' muss. Genau das prueft dieser Test bis zur Datei und zurueck.</summary>
    <TestMethod>
    Public Async Function UebernommeneLoesungUeberlebtSpeichernUndLaden() As Task
        Dim m = MiniProjekt().Modell
        Await m.StundenplanRechnenAsync()
        Assert.IsTrue(m.Projekt.Staende.Any(Function(s) s.Stundenplan IsNot Nothing), m.Meldung)

        m.VerarbeiteBrueckenNachricht(
            "{""v"":1,""typ"":""plan-uebernehmen"",""nutzlast"":{""zuteilung"":1,""loesung"":1}}")

        Dim stand = m.Projekt.Staende.Last(Function(s) s.Stundenplan IsNot Nothing)
        Assert.IsNotNull(stand.Lauf, "der Stand traegt kein lauf-Objekt")
        Dim markierung = stand.Lauf("arbeitsstand").AsObject()
        Assert.AreEqual(1, markierung("loesung").GetValue(Of Integer)())
        Assert.IsTrue(m.Projekt.AuditLog.Any(Function(e) e.Aktion = "arbeitsstand"),
                      "die Markierung gehoert ins Protokoll")

        m.Speichern()
        Dim erneut = ProjektDatei.Laden(IO.Path.Combine(_ordner, "mini.splanx"), "geheim12")
        Dim geladen = erneut.Staende.Last(Function(s) s.Stundenplan IsNot Nothing)
        Assert.IsNotNull(geladen.Lauf("arbeitsstand"), "die Markierung hat das Speichern nicht ueberlebt")
        Assert.AreEqual(1, geladen.Lauf("arbeitsstand").AsObject()("loesung").GetValue(Of Integer)())
    End Function

    ''' <summary>Ohne Stand gibt es nichts zu markieren - und der Nutzer
    ''' erfaehrt das, statt dass nichts passiert.</summary>
    <TestMethod>
    Public Sub UebernehmenOhneStandMeldetStattStillZuVersagen()
        Dim entwurf As New ProjektEntwurf With {
            .Bestand = Scaffold.Baue("BW", "Grundschule", 1, 4, 1, "Minischule"),
            .Pfad = IO.Path.Combine(_ordner, "mini.splanx"), .Passwort = "geheim12"}
        Dim d As New TestDialoge With {.AssistentEntwurf = entwurf}
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Neu()

        m.VerarbeiteBrueckenNachricht(
            "{""v"":1,""typ"":""plan-uebernehmen"",""nutzlast"":{""zuteilung"":1,""loesung"":1}}")

        Assert.AreEqual(1, d.Hinweise.Count)
        StringAssert.Contains(d.Hinweise(0), "Stand")
    End Sub

    ''' <summary>Die Kurz-Parameter landen in der Projekt-Config, nicht nur
    ''' in diesem einen Lauf: sonst zeigten die Solver-Einstellungen (6.12)
    ''' danach etwas anderes an als gerechnet wurde.</summary>
    <TestMethod>
    Public Async Function NeuRechnenUebernimmtDieParameterInDieConfig() As Task
        Dim m = MiniProjekt().Modell
        Await m.PlanNeuRechnenAsync(Nutzlast("{""zeitbudget_s"": 25, ""max_loesungen"": 2}"))

        Assert.AreEqual(25.0, m.Projekt.Config.SolveTimeLimitS)
        Assert.AreEqual(2, m.Projekt.Config.MaxSolutions)
        Assert.IsTrue(m.Projekt.AuditLog.Any(Function(e) e.Aktion = "einstellung"))
        Assert.IsTrue(m.Auslieferung.SeitenGroesse > 0, "es wurde nicht gerechnet: " & m.Meldung)
    End Function


    ' ===============================================================
    ' Freigabe aus der Sicht (Nutzerwunsch 26.08.2026)
    ' ===============================================================

    ''' <summary>Freigegeben wird der Stand, den DIESE Sicht zeigt - nicht
    ''' der letzte Lauf. Der Unterschied wird sichtbar, sobald jemand
    ''' einen aelteren Stand angesehen hat.</summary>
    <TestMethod>
    Public Async Function DieSichtGibtIhrenEigenenStandFreiNichtDenLetzten() As Task
        Dim mini = MiniProjekt()
        Dim m = mini.Modell
        mini.Dialoge.FreigabeAntwort = New Freigabebestaetigung With {
            .Person = "Frau Meier", .Bestaetigt = True}
        Await m.StundenplanRechnenAsync()
        Dim erster = m.Projekt.Staende.Last()

        Await m.StundenplanRechnenAsync()
        Assert.AreEqual(2, m.Projekt.Staende.Count, "Testgrundlage: es braucht zwei Staende")

        ' Zurueck auf den ERSTEN - das Dashboard zeigt jetzt ihn.
        m.StandAnzeigen(erster)

        m.VerarbeiteBrueckenNachricht("{""v"":1,""typ"":""freigabe"",""nutzlast"":{}}")

        Assert.IsTrue(LaeufeViewModel.IstFreigabe(erster), "der angezeigte Stand wurde nicht freigegeben")
        Assert.IsFalse(LaeufeViewModel.IstFreigabe(m.Projekt.Staende.Last()),
                       "der LETZTE Stand wurde freigegeben - die Sicht wurde ignoriert")
    End Function

    ''' <summary>Der Weg durch die Sicht ist derselbe wie im Bereich
    ''' Laeufe: Dialog mit Abweichungen, Begruendungspflicht, Nachweis.
    ''' Eine bequemere Abkuerzung waere genau das, was den Nachweis
    ''' entwertet.</summary>
    <TestMethod>
    Public Async Function DieSichtNutztDenselbenDialogUndDieselbePflicht() As Task
        Dim mini = MiniProjekt()
        Dim m = mini.Modell
        Await m.StundenplanRechnenAsync()

        ' Vorgabe der Attrappe ist Abbruch - also passiert nichts.
        m.VerarbeiteBrueckenNachricht("{""v"":1,""typ"":""freigabe"",""nutzlast"":{}}")
        Assert.AreEqual(1, mini.Dialoge.FreigabeVorlagen.Count, "der Dialog muss gezeigt werden")
        Assert.IsFalse(LaeufeViewModel.IstFreigabe(m.Projekt.Staende.Last()))

        mini.Dialoge.FreigabeAntwort = New Freigabebestaetigung With {
            .Person = "Frau Meier", .Bestaetigt = True, .Notiz = "Geprüft."}
        m.VerarbeiteBrueckenNachricht("{""v"":1,""typ"":""freigabe"",""nutzlast"":{}}")

        Dim stand = m.Projekt.Staende.Last()
        Assert.IsTrue(LaeufeViewModel.IstFreigabe(stand))
        Assert.IsTrue(stand.Geschuetzt)
        StringAssert.Contains(m.Meldung, "Freigegeben")
    End Function

    ''' <summary>Ohne angezeigten Stand gibt es nichts freizugeben - und
    ''' der Nutzer erfaehrt warum, statt dass nichts passiert.</summary>
    <TestMethod>
    Public Sub FreigabeOhneAngezeigtenStandMeldetDenGrund()
        Dim mini = MiniProjekt()
        mini.Modell.VerarbeiteBrueckenNachricht("{""v"":1,""typ"":""freigabe"",""nutzlast"":{}}")

        Assert.AreEqual(1, mini.Dialoge.Hinweise.Count)
        StringAssert.Contains(mini.Dialoge.Hinweise(0), "keinem gespeicherten Stand")
    End Sub

    ''' <summary>Das Startskript traegt den Freigabestand in die Seite -
    ''' sonst boete das Dashboard eine Freigabe an, die laengst erfolgt
    ''' ist, und der Nutzer erfuehre es erst durch den Hinweis danach.</summary>
    <TestMethod>
    Public Async Function DasStartskriptTraegtDenFreigabestandInDieSeite() As Task
        Dim mini = MiniProjekt()
        Dim m = mini.Modell
        Await m.StundenplanRechnenAsync()

        StringAssert.Contains(m.BrueckenStartSkript(), "window.__freigabe = null")

        mini.Dialoge.FreigabeAntwort = New Freigabebestaetigung With {
            .Person = "Frau Meier", .Bestaetigt = True}
        m.VerarbeiteBrueckenNachricht("{""v"":1,""typ"":""freigabe"",""nutzlast"":{}}")

        StringAssert.Contains(m.BrueckenStartSkript(), "Frau Meier")
    End Function

End Class

