' Die Bildprobe (Infrastruktur/Bildprobe.vb): was ohne Fenster pruefbar
' ist - das Lesen der Argumente und das Projekt aus dem Schulordner
' samt eingehaengter Staende.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableGui

<TestClass>
Public Class BildprobeTests

    <TestMethod>
    Public Sub OhneSchalterKeinAuftrag()
        Assert.IsNull(Bildprobe.Lesen(New String() {}))
        Assert.IsNull(Bildprobe.Lesen({"irgendwas.splanx"}))
    End Sub

    <TestMethod>
    Public Sub AlleSchalterWerdenGelesen()
        Dim a = Bildprobe.Lesen({"--bildprobe", "C:\bilder", "--schule", "tests\gs", "--rechnen", "Stundenplan", "--masken", "--menues"})
        Assert.AreEqual("C:\bilder", a.Ordner)
        Assert.AreEqual("tests\gs", a.Schule)
        Assert.AreEqual("stundenplan", a.Rechnen)
        Assert.IsTrue(a.Masken)
        Assert.IsTrue(a.Menues)
        Assert.IsNull(a.Projekt)
    End Sub

    ''' <summary>Eine Bildprobe, die still etwas anderes tut als verlangt,
    ''' waere ein falscher Beleg - deshalb Fehler statt Raten.</summary>
    <TestMethod>
    Public Sub FehlerhafteArgumenteWerfen()
        Assert.ThrowsException(Of ArgumentException)(Sub() Bildprobe.Lesen({"--bildprobe"}))
        Assert.ThrowsException(Of ArgumentException)(Sub() Bildprobe.Lesen({"--bildprobe", "--masken"}))
        Assert.ThrowsException(Of ArgumentException)(Sub() Bildprobe.Lesen({"--bildprobe", "x", "--rechnen", "alles"}))
        Assert.ThrowsException(Of ArgumentException)(Sub() Bildprobe.Lesen({"--bildprobe", "x", "--foo"}))
    End Sub

    ''' <summary>Die Beispielschule bringt beide Ergebnisse als Staende
    ''' mit - so zeigen beide Dashboards etwas, ohne dass ein Solver
    ''' laeuft.</summary>
    <TestMethod>
    Public Sub SchulordnerBringtBeideStaendeMit()
        Dim ordner = IO.Path.Combine(TestsWurzel(), "bw-grundschule-beispiel")
        Dim p = Bildprobe.ProjektAusSchulordner(ordner, New DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero))

        Assert.IsTrue(p.Bestand.Klassen.Count > 0)
        Assert.AreEqual(2, p.Staende.Count)
        Assert.IsTrue(p.Staende.Any(Function(s) s.Klassenbildung IsNot Nothing))
        Assert.IsTrue(p.Staende.Any(Function(s) s.Stundenplan IsNot Nothing))
    End Sub

End Class
