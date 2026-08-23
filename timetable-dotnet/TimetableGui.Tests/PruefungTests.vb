' "Speichern ist immer moeglich, Rechnen nur bei gruener Pruefung"
' (gui-ui-konzept.md 1) - der Grundsatz, der ein unfertiges Projekt
' ausdruecklich erlaubt und trotzdem verhindert, dass daraus ein Lauf
' wird.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class PruefungTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 23, 17, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttpr-" & Guid.NewGuid().ToString("N"))
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

    Private Function ModellMitSchule() As HauptViewModel
        Dim d As New TestDialoge With {
            .OrdnerPfad = IO.Path.Combine(TestsRoot, "bw-grundschule-beispiel"),
            .SpeichernPfad = IO.Path.Combine(_ordner, "gs.splanx")}
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Importieren()
        Return m
    End Function

    <TestMethod>
    Public Sub EineIntakteSchuleIstGruen()
        Dim m = ModellMitSchule()

        Assert.AreEqual(0, m.StammdatenPruefen().Count,
                        "die Beispielschule ist nicht gruen: " & String.Join(" | ", m.StammdatenPruefen()))
        Assert.IsTrue(m.PruefungGruen)
    End Sub

    ''' <summary>Der Fall, den die Bestandspflege verhindern soll - hier
    ''' bewusst herbeigefuehrt, um zu belegen, dass die Pruefung ihn auch
    ''' FINDET. Ohne diesen Test koennte die Pruefung stumm alles
    ''' durchwinken.</summary>
    <TestMethod>
    Public Sub VerwaisteReferenzWirdGefunden()
        Dim m = ModellMitSchule()
        ' Lehrkraft direkt aus dem Bestand entfernen, OHNE Bestandspflege -
        ' die Regeln erwaehnen sie danach ins Leere.
        Dim name = m.Projekt.Constraints.
            Select(Function(c) JsonHelpers.GetString(c, "teacher")).
            First(Function(t) t IsNot Nothing)
        m.Projekt.Bestand.Lehrkraefte.RemoveAll(Function(l) l.Name = name)
        m.Projekt.Bestand.FachLehrerZuordnungen.RemoveAll(Function(z) z.LehrerName = name)

        Dim fehler = m.StammdatenPruefen()

        Assert.IsTrue(fehler.Count > 0, "die verwaiste Referenz wurde nicht gefunden")
        Assert.IsFalse(m.PruefungGruen)
    End Sub

    ''' <summary>Und die Gegenprobe: ueber Bestandspflege geloescht bleibt
    ''' die Pruefung gruen. Das ist der Sinn des Konsequenzen-Dialogs.</summary>
    <TestMethod>
    Public Sub UeberBestandspflegeGeloeschtBleibtGruen()
        Dim m = ModellMitSchule()
        Dim name = m.Projekt.Bestand.Lehrkraefte.First().Name

        Bestandspflege.Loesche(m.Projekt, Stammart.Lehrkraft, name)

        Assert.AreEqual(0, m.StammdatenPruefen().Count,
                        "nach dem Loeschen ueber Bestandspflege sind Referenzen verwaist: " &
                        String.Join(" | ", m.StammdatenPruefen()))
        Assert.IsTrue(m.PruefungGruen)
    End Sub

    ''' <summary>Ein unfertiges Projekt DARF gespeichert werden - nur
    ''' rechnen darf man damit nicht.</summary>
    <TestMethod>
    Public Sub UnfertigesProjektLaesstSichSpeichern()
        Dim d As New TestDialoge With {.SpeichernPfad = IO.Path.Combine(_ordner, "leer.splanx")}
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Neu()

        ' Ein leeres Projekt ist nicht gruen (keine Klassen, keine Faecher).
        Assert.IsTrue(m.StammdatenPruefen().Count > 0, "ein leeres Projekt gilt faelschlich als vollstaendig")
        Assert.IsFalse(m.PruefungGruen)

        ' Gespeichert wurde es trotzdem - und laesst sich wieder oeffnen.
        Assert.IsTrue(IO.File.Exists(d.SpeichernPfad))
        Assert.IsTrue(m.SpeichernBefehl.CanExecute(Nothing), "Speichern wurde gesperrt")
        Assert.IsFalse(m.KlassenbildungBefehl.CanExecute(Nothing), "Rechnen wurde nicht gesperrt")
    End Sub

    <TestMethod>
    Public Sub OhneProjektMeldetDiePruefungDasStattZuWerfen()
        Dim m As New HauptViewModel(New TestDialoge(), Function() Jetzt)

        Dim fehler = m.StammdatenPruefen()

        Assert.AreEqual(1, fehler.Count)
        StringAssert.Contains(fehler(0), "Kein Projekt")
        Assert.IsFalse(m.PruefungGruen)
    End Sub

End Class
