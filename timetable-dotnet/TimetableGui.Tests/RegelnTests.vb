' Regel-Masken, Stufe F3 (gui-ui-konzept.md 6.10).
'
' Geprueft wird, was beim Anlegen ins WIRE-FORMAT geschrieben wird - das
' ist die Stelle, an der eine Maske realen Schaden anrichten kann: eine
' Regel mit falschem Feldnamen laeuft durch Validation und Solver und
' wirkt einfach nicht.
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class RegelnTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 23, 21, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttrg-" & Guid.NewGuid().ToString("N"))
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
                If IO.Directory.Exists(IO.Path.Combine(dir.FullName, "tests", "bw-grundschule-beispiel")) Then
                    Return IO.Path.Combine(dir.FullName, "tests")
                End If
                dir = dir.Parent
            End While
            Throw New InvalidOperationException("tests/-Verzeichnis nicht gefunden")
        End Get
    End Property

    Private Function Beispielprojekt() As Projekt
        Dim d As New TestDialoge With {
            .OrdnerPfad = IO.Path.Combine(TestsRoot, "bw-grundschule-beispiel"),
            .SpeichernPfad = IO.Path.Combine(_ordner, "gs.splanx")}
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Importieren()
        Return m.Projekt
    End Function

    Private Shared Function Raster(p As Projekt) As RasterAuswahl
        Return New RasterAuswahl(p.Bestand.Tage, p.Bestand.PeriodsPerDay)
    End Function

    ' ===============================================================
    ' Die acht Typen
    ' ===============================================================

    ''' <summary>"Masken fuer genau die Typen, die die Beispiele real
    ''' nutzen" (6.10). Der Test haelt fest, dass es genau diese acht
    ''' sind - und dass jeder ein Grund-Feld hat.</summary>
    <TestMethod>
    Public Sub EsGibtAchtTypenUndJederHatEinenGrund()
        Assert.AreEqual(8, Regeltypen.Alle.Count)
        For Each t In Regeltypen.Alle
            Assert.IsTrue(t.Felder.Any(Function(f) f.Name = "reason"), $"{t.Typ} ohne Grund-Feld.")
            Assert.IsTrue(t.Felder.Any(Function(f) f.Name = "priority"), $"{t.Typ} ohne Prio-Feld.")
        Next
    End Sub

    ''' <summary>Der ausdrueckliche Befund aus 6.10: room_requirement ist
    ''' rein fachbezogen. Zwei Doku-Beispiele zeigen faelschlich ein
    ''' class-Feld - die Maske darf es nicht anbieten.</summary>
    <TestMethod>
    Public Sub RaumbedarfHatKeinKlassenFeld()
        Dim t = Regeltypen.Finde("room_requirement")
        Assert.IsFalse(t.Felder.Any(Function(f) f.Name = "class" OrElse f.Name = "classes"),
                       "room_requirement ist rein fachbezogen.")
        Assert.IsTrue(t.Felder.Any(Function(f) f.Name = "allowed_rooms"))
    End Sub

    ' ===============================================================
    ' Vervielfachen
    ' ===============================================================

    ''' <summary>"Mehrfach-Slot-Auswahl erzeugt eine Regel je Slot (wie im
    ''' Beispiel)" - das ist kein Komfort, sondern das Format: der Kern
    ''' kennt keine forbidden_slot-Regel ueber mehrere Slots.</summary>
    <TestMethod>
    Public Sub EineSlotauswahlErzeugtEineRegelJeSlot()
        Dim p = Beispielprojekt()
        Dim vm As New RegelnViewModel(p, New TestDialoge())
        Dim r = Raster(p)
        r.Bereich("Mo", 5, "Di", 6, True)   ' 2 Tage x 2 Stunden

        Dim regeln = vm.Baue("forbidden_slot",
            New Dictionary(Of String, String) From {{"scope", "class"}, {"entity", "1a"}},
            Nothing, r)

        Assert.AreEqual(4, regeln.Count)
        For Each c In regeln
            Assert.AreEqual("forbidden_slot", c("type").GetValue(Of String)())
            Assert.AreEqual("1a", c("entity").GetValue(Of String)())
            Assert.IsTrue(c.ContainsKey("day") AndAlso c.ContainsKey("period"))
        Next
        CollectionAssert.AreEquivalent({5, 6, 5, 6},
            regeln.Select(Function(c) c("period").GetValue(Of Integer)()).ToArray())
    End Sub

    ''' <summary>"Mehrfachauswahl von Klassen erzeugt je Klasse eine
    ''' Regel" - auch hier kennt der Kern keine Regel ueber mehrere
    ''' Klassen.</summary>
    <TestMethod>
    Public Sub EineKlassenauswahlErzeugtEineRegelJeKlasse()
        Dim p = Beispielprojekt()
        Dim vm As New RegelnViewModel(p, New TestDialoge())
        Dim r = Raster(p)
        r.Bereich("Mo", 1, "Fr", 4, True)

        Dim regeln = vm.Baue("subject_period_window",
            New Dictionary(Of String, String) From {{"subject", "Deutsch"}},
            New Dictionary(Of String, List(Of String)) From {
                {"class", New List(Of String) From {"1a", "1b", "2a"}}},
            r)

        Assert.AreEqual(3, regeln.Count)
        CollectionAssert.AreEquivalent({"1a", "1b", "2a"},
            regeln.Select(Function(c) c("class").GetValue(Of String)()).ToArray())
        For Each c In regeln
            Assert.AreEqual(1, c("from_period").GetValue(Of Integer)())
            Assert.AreEqual(4, c("to_period").GetValue(Of Integer)())
        Next
    End Sub

    ''' <summary>Sind ALLE Tage gewaehlt, wird kein days-Feld
    ''' geschrieben: "Tag nicht gewaehlt = ganz ausserhalb" (6.10) - eine
    ''' vollstaendige Liste ist dieselbe Aussage wie keine, und die
    ''' kuerzere Regel ist die ehrlichere.</summary>
    <TestMethod>
    Public Sub AlleTageGewaehltSchreibtKeinTagesfeld()
        Dim p = Beispielprojekt()
        Dim vm As New RegelnViewModel(p, New TestDialoge())

        Dim alle = Raster(p)
        alle.Bereich(p.Bestand.Tage.First(), 1, p.Bestand.Tage.Last(), 4, True)
        Dim r1 = vm.Baue("occupied_window",
            New Dictionary(Of String, String) From {{"scope", "class"}, {"entity", "1a"}}, Nothing, alle)
        Assert.IsFalse(r1(0).ContainsKey("days"), "Alle Tage = keine Einschraenkung.")

        Dim zwei = Raster(p)
        zwei.Bereich("Mo", 1, "Di", 4, True)
        Dim r2 = vm.Baue("occupied_window",
            New Dictionary(Of String, String) From {{"scope", "class"}, {"entity", "1a"}}, Nothing, zwei)
        CollectionAssert.AreEqual({"Mo", "Di"},
            JsonHelpers.AsStringList(r2(0)("days")).ToArray())
    End Sub

    ''' <summary>Ist die Auswahl kein Rechteck, entsteht KEIN Fenster -
    ''' die Maske muss dann nachfragen statt still auf die Huelle zu
    ''' runden.</summary>
    <TestMethod>
    Public Sub OhneRechteckEntstehtKeinFenster()
        Dim p = Beispielprojekt()
        Dim vm As New RegelnViewModel(p, New TestDialoge())
        Dim r = Raster(p)
        r.Bereich("Mo", 1, "Mi", 4, True)
        r.Setze("Di", 2, False)

        Dim regeln = vm.Baue("occupied_window",
            New Dictionary(Of String, String) From {{"scope", "class"}, {"entity", "1a"}}, Nothing, r)

        Assert.AreEqual(1, regeln.Count)
        Assert.IsFalse(regeln(0).ContainsKey("from_period"),
                       "Eine loechrige Auswahl darf kein von/bis erzeugen.")
    End Sub

    ' ===============================================================
    ' Wire-Format
    ' ===============================================================

    ''' <summary>Was die Maske schreibt, muss die Pruefung des Kerns
    ''' gruen sehen - die Zusage des Plans fuer Stufe F.</summary>
    <TestMethod>
    Public Sub WasDieMaskeSchreibtBleibtValide()
        Dim p = Beispielprojekt()
        Dim vm As New RegelnViewModel(p, New TestDialoge())
        Assert.AreEqual(0, vm.Pruefe().Count, String.Join(" | ", vm.Pruefe()))

        Dim r = Raster(p)
        r.Setze("Fr", 6, True)
        vm.Hinzufuegen(vm.Baue("forbidden_slot",
            New Dictionary(Of String, String) From {
                {"scope", "class"}, {"entity", "1a"}, {"priority", "must"}, {"reason", "Testregel"}},
            Nothing, r))

        Assert.AreEqual(0, vm.Pruefe().Count, String.Join(" | ", vm.Pruefe()))
    End Sub

    ''' <summary>Und der Gegenbeweis: eine Regel auf eine Klasse, die es
    ''' nicht gibt, MUSS auffallen. Sonst waere der Test oben wertlos.</summary>
    <TestMethod>
    Public Sub EineUnbekannteReferenzFaelltAuf()
        Dim p = Beispielprojekt()
        Dim vm As New RegelnViewModel(p, New TestDialoge())
        Dim r = Raster(p)
        r.Setze("Fr", 6, True)
        vm.Hinzufuegen(vm.Baue("forbidden_slot",
            New Dictionary(Of String, String) From {{"scope", "class"}, {"entity", "9z"}},
            Nothing, r))

        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains("9z")), String.Join(" | ", vm.Pruefe()))
    End Sub

    <TestMethod>
    Public Sub FehlendePflichtfelderWerdenInKlartextGenannt()
        Dim p = Beispielprojekt()
        Dim vm As New RegelnViewModel(p, New TestDialoge())

        Dim fehlend = vm.PflichtfelderFehlen("room_requirement",
            New Dictionary(Of String, String)(), Nothing, Nothing)

        CollectionAssert.Contains(fehlend, "Fach")
        CollectionAssert.Contains(fehlend, "Erlaubte Räume")
        Assert.IsFalse(fehlend.Contains("Grund"), "Der Grund ist optional.")
    End Sub

    ' ===============================================================
    ' Generierte Regeln
    ' ===============================================================

    ''' <summary>"Handpflege ist dort ausdruecklich verboten und die GUI
    ''' erzwingt das strukturell" (6.10) - strukturell heisst: es gibt
    ''' keine Bearbeitungsmoeglichkeit, nicht bloss einen Warnhinweis.</summary>
    <TestMethod>
    Public Sub GenerierteRegelnStehenNichtInDerBearbeitbarenListe()
        Dim p = Beispielprojekt()
        Dim vm As New RegelnViewModel(p, New TestDialoge())
        Dim erzeugt = JsonNode.Parse(
            "{""type"":""weekly_hours"",""class"":""1a"",""subject"":""Deutsch"",""hours_per_week"":5}").AsObject()
        p.Constraints.Add(erzeugt)

        Assert.IsFalse(vm.Handregeln().Contains(erzeugt),
                       "Eine generierte Regel darf nicht in der bearbeitbaren Liste stehen.")

        vm.Entfernen(erzeugt)
        Assert.IsTrue(p.Constraints.Contains(erzeugt),
                      "Loeschen einer generierten Regel muss wirkungslos bleiben.")
    End Sub

    ' ===============================================================
    ' YAML-Expertenmodus
    ' ===============================================================

    ''' <summary>"Masken und Editor arbeiten auf demselben Bestand"
    ''' (6.10). Der Rundlauf belegt, dass der Editor nichts verliert.</summary>
    <TestMethod>
    Public Sub DerEditorVerliertNichts()
        Dim p = Beispielprojekt()
        Dim vm As New RegelnViewModel(p, New TestDialoge())
        Dim vorher = vm.Handregeln().Count
        Assert.IsTrue(vorher > 0, "Testgrundlage: die Schule hat Handregeln.")

        Dim yaml = vm.AlsYaml()
        Assert.AreEqual(0, vm.YamlPruefen(yaml).Count, String.Join(" | ", vm.YamlPruefen(yaml)))
        Assert.IsTrue(vm.YamlUebernehmen(yaml))

        Assert.AreEqual(vorher, vm.Handregeln().Count)
        Assert.AreEqual(0, vm.Pruefe().Count, String.Join(" | ", vm.Pruefe()))
    End Sub

    ''' <summary>Syntaxfehler melden und NICHTS anfassen - ein Editor,
    ''' der bei kaputtem Text den Bestand leert, ist eine Falle.</summary>
    <TestMethod>
    Public Sub KaputtesYamlLaesstDenBestandUnberuehrt()
        Dim p = Beispielprojekt()
        Dim vm As New RegelnViewModel(p, New TestDialoge())
        Dim vorher = p.Constraints.Count

        Dim kaputt = "- type: forbidden_slot" & vbLf & "   scope: [unbalanced"
        Assert.IsTrue(vm.YamlPruefen(kaputt).Any(Function(f) f.StartsWith("YAML-Syntax")),
                      String.Join(" | ", vm.YamlPruefen(kaputt)))
        Assert.IsFalse(vm.YamlUebernehmen(kaputt))
        Assert.AreEqual(vorher, p.Constraints.Count)
    End Sub

    ''' <summary>ValidateEntities-Fehler HINDERN NICHT am Uebernehmen:
    ''' "Speichern ist immer moeglich, Rechnen nur bei gruener Pruefung"
    ''' (Konzept 1). Ein Zwischenstand mit noch unbekannter Referenz muss
    ''' sich ablegen lassen.</summary>
    <TestMethod>
    Public Sub EineUnbekannteReferenzHindertNichtAmSpeichern()
        Dim p = Beispielprojekt()
        Dim vm As New RegelnViewModel(p, New TestDialoge())
        Dim yaml = "- type: forbidden_slot" & vbLf &
                   "  scope: class" & vbLf &
                   "  entity: 9z" & vbLf &
                   "  day: Mo" & vbLf &
                   "  period: 1" & vbLf

        Dim befunde = vm.YamlPruefen(yaml)
        Assert.IsTrue(befunde.Any(Function(f) f.Contains("9z")), String.Join(" | ", befunde))
        Assert.IsFalse(befunde.Any(Function(f) f.StartsWith("YAML-Syntax")))

        Assert.IsTrue(vm.YamlUebernehmen(yaml), "Speichern muss trotzdem gehen.")
        Assert.AreEqual(1, vm.Handregeln().Count)
    End Sub

End Class
