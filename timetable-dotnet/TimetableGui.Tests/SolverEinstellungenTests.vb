' Solver-Einstellungen, Stufe F4 (gui-ui-konzept.md 6.12).
'
' Der Kern der Maske ist nicht das Formular, sondern die Bedeutung von
' LEER: "Werte = config.yaml-Felder mit deren Defaults". Ein leeres Feld
' heisst "Default des Kerns" - fuellt die Maske es beim Oeffnen mit dem
' aktuellen Default, stehen danach ueberall explizite Werte, und eine
' spaetere Aenderung des Defaults erreicht das Projekt nie mehr.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class SolverEinstellungenTests

    <TestMethod>
    Public Sub LeereExpertenfelderBleibenLeer()
        Dim p As New Projekt()
        Dim vm As New SolverEinstellungenViewModel(p)

        Dim felder = vm.Expertenfelder()
        Assert.IsTrue(felder.Count >= 25, $"Nur {felder.Count} Expertenfelder - 6.12 nennt deutlich mehr.")

        ' Frisches Projekt: alle Expertenfelder sind Nothing und muessen
        ' als leerer Text erscheinen.
        For Each f In felder
            Assert.AreEqual("", f.Lesen.Invoke(),
                            $"Feld „{f.Name}"" ist vorbelegt - damit ginge der Kern-Default verloren.")
        Next
    End Sub

    <TestMethod>
    Public Sub EinGesetztesFeldLaesstSichWiederLeerenUndIstDannDefault()
        Dim p As New Projekt()
        Dim vm As New SolverEinstellungenViewModel(p)
        Dim feld = vm.Expertenfelder().First(Function(f) f.Name.Contains("Stagnations"))

        feld.Schreiben.Invoke("12")
        Assert.AreEqual("12", feld.Lesen.Invoke())
        Assert.IsTrue(p.Config.StagnationTimeoutS.HasValue)

        feld.Schreiben.Invoke("")
        Assert.IsFalse(p.Config.StagnationTimeoutS.HasValue,
                       "Ein geleertes Feld muss auf den Kern-Default zurueckfallen.")
    End Sub

    ''' <summary>Unsinn wird nicht uebernommen, sondern verworfen - sonst
    ''' stuende im Projekt ein Wert, den niemand gemeint hat.</summary>
    <TestMethod>
    Public Sub UnsinnigeEingabenWerdenVerworfen()
        Dim p As New Projekt()
        Dim vm As New SolverEinstellungenViewModel(p)
        Dim feld = vm.Expertenfelder().First(Function(f) f.Name.Contains("Toleranz") AndAlso f.Gruppe = "Lexikografisch")

        feld.Schreiben.Invoke("drei")
        Assert.AreEqual("", feld.Lesen.Invoke())
        Assert.IsFalse(p.Config.LexTolerance.HasValue)
    End Sub

    ''' <summary>Der Determinismus-Hinweis, den 6.12 am Seed verlangt -
    ''' und zwar NUR bei mehreren Workern. Eine Warnung, die immer da
    ''' ist, wird nicht gelesen. Die Aussage stammt aus arc42 8.5.</summary>
    <TestMethod>
    Public Sub DerDeterminismusHinweisErscheintNurBeiMehrerenWorkern()
        Dim p As New Projekt()
        Dim vm As New SolverEinstellungenViewModel(p)

        vm.NumWorkers = 1
        Assert.IsFalse(vm.DeterminismusHinweis.Contains("nicht"),
                       "Mit einem Worker ist der Lauf reproduzierbar: " & vm.DeterminismusHinweis)

        vm.NumWorkers = 4
        StringAssert.Contains(vm.DeterminismusHinweis, "nicht")
        StringAssert.Contains(vm.DeterminismusHinweis, "TROTZ")
    End Sub

    ''' <summary>Der Klassenbildungs-Block der Config entsteht erst beim
    ''' Setzen - vorher gilt der Kern-Default. Ein Block, den die Maske
    ''' beim Oeffnen anlegt, waere schon eine Festlegung.</summary>
    <TestMethod>
    Public Sub DerKlassenbildungsBlockEntstehtErstBeimSetzen()
        Dim p As New Projekt()
        Dim vm As New SolverEinstellungenViewModel(p)
        Assert.IsNull(p.Config.Klassenbildung)
        Assert.IsFalse(vm.Varianten.HasValue)

        vm.Varianten = 3
        Assert.IsNotNull(p.Config.Klassenbildung)
        Assert.AreEqual(3, p.Config.Klassenbildung.NVarianten)
    End Sub

    ''' <summary>Die haeufigste Fehlbedienung: viele Loesungen in einem
    ''' knappen Budget. Kein Fehler, aber ein Zusammenhang, den ohne
    ''' Hinweis niemand sieht.</summary>
    <TestMethod>
    Public Sub EinKnappesBudgetBeiVielenLoesungenFaelltAuf()
        Dim p As New Projekt()
        Dim vm As New SolverEinstellungenViewModel(p)
        vm.MaxSolutions = 30
        vm.ZeitbudgetS = 5

        Assert.IsTrue(vm.Pruefe().Any(Function(f) f.Contains("weniger als eine Sekunde")),
                      String.Join(" | ", vm.Pruefe()))
    End Sub

    <TestMethod>
    Public Sub GrundlegendUnsinnigeWerteFallenAuf()
        Dim p As New Projekt()
        Dim vm As New SolverEinstellungenViewModel(p)
        vm.MaxSolutions = 0
        vm.ZeitbudgetS = 0

        Dim fehler = vm.Pruefe()
        Assert.IsTrue(fehler.Any(Function(f) f.Contains("Zeitbudget")))
        Assert.IsTrue(fehler.Any(Function(f) f.Contains("mindestens eine Loesung")))
    End Sub

End Class
