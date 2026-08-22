' Abbruch- und Fortschrittskanal fuer die Klassenbildung (arc42 8.11).
' Gleiche Leitregel wie in TimetableCore.Tests/CancellationProgressTests.vb:
' keine Sleeps, keine Timing-Annahmen - abgebrochen wird entweder vorab
' oder deterministisch aus dem (synchron aufgerufenen) Fortschritts-Handler.
Imports System.Threading
Imports Google.OrTools.Sat
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

''' <summary>Synchroner IProgress - Progress(Of T) waere im Testkontext
''' asynchron ueber den ThreadPool zugestellt und damit ein Wettrennen.</summary>
Friend NotInheritable Class SofortProgressKb
    Implements IProgress(Of SolveProgress)

    Private ReadOnly _aktion As Action(Of SolveProgress)
    Private ReadOnly _gate As New Object()
    Private ReadOnly _gesehen As New List(Of SolveProgress)

    Public Sub New(aktion As Action(Of SolveProgress))
        _aktion = aktion
    End Sub

    Public Sub Report(value As SolveProgress) Implements IProgress(Of SolveProgress).Report
        SyncLock _gate
            _gesehen.Add(value)
        End SyncLock
        If _aktion IsNot Nothing Then _aktion(value)
    End Sub

    Public ReadOnly Property Meldungen As List(Of SolveProgress)
        Get
            SyncLock _gate
                Return New List(Of SolveProgress)(_gesehen)
            End SyncLock
        End Get
    End Property
End Class

<TestClass>
Public Class KlassenbildungCancellationTests

    ''' <summary>40 Kinder auf 4 Klassen - gross genug fuer mehrere echte
    ''' Varianten, klein genug fuer die Sub-Sekunden-Suite.</summary>
    Private Shared Function Basis() As KlassenbildungInput
        Dim input As New KlassenbildungInput With {
            .Klassen = New KlassenbildungKlassen With {.Anzahl = 4, .MinGroesse = 8, .MaxGroesse = 12}
        }
        For i = 1 To 40
            input.Schueler.Add(New KlassenbildungSchueler With {.Id = $"S{i}"})
        Next
        Return input
    End Function

    <TestMethod>
    Public Sub KlassenbildungPreCancelledTokenReturnsEmpty()
        Dim cts As New CancellationTokenSource()
        cts.Cancel()

        Dim einzeln = Klassenbildung.SolveKlassenbildung(Basis(), zeitlimitS:=600.0, cancellationToken:=cts.Token)
        Assert.IsTrue(einzeln.Cancelled, "SolveKlassenbildung: Cancelled nicht gesetzt")
        Assert.AreEqual(CpSolverStatus.Unknown, einzeln.Status)
        Assert.IsNull(einzeln.Zuordnung, "Es haette keine Zuordnung entstehen duerfen")

        Dim top = Klassenbildung.SolveKlassenbildungTop(Basis(), zeitlimitS:=600.0, nVarianten:=3,
                                                        cancellationToken:=cts.Token)
        Assert.IsTrue(top.Cancelled, "SolveKlassenbildungTop: Cancelled nicht gesetzt")
        Assert.AreEqual(0, top.Varianten.Count)
        Assert.AreEqual(0, top.KonsensKern.Count)
    End Sub

    ''' <summary>Gegenprobe: ohne Token liefert dasselbe Szenario echte
    ''' Varianten. Sonst koennte der Test oben auch dann gruen sein, wenn
    ''' das Szenario schlicht unloesbar waere.</summary>
    <TestMethod>
    Public Sub KlassenbildungCounterProofScenarioIsSolvable()
        Dim top = Klassenbildung.SolveKlassenbildungTop(Basis(), zeitlimitS:=10.0, nVarianten:=3, minDistanz:=4)
        Assert.IsFalse(top.Cancelled)
        Assert.IsTrue(top.Varianten.Count >= 2,
                      $"Erwartet wurden mehrere Varianten, gefunden: {top.Varianten.Count}")
    End Sub

    ''' <summary>Abbruch nach der ersten Variante: die bereits gerechneten
    ''' Varianten muessen erhalten bleiben - die GUI soll Variante 1 sofort
    ''' zeigen koennen, statt bei "Abbrechen" alles zu verlieren.</summary>
    <TestMethod>
    Public Sub SolveKlassenbildungTopCancelledReturnsPartialVarianten()
        Dim cts As New CancellationTokenSource()
        Dim progress As New SofortProgressKb(Sub(p)
                                                 If p.SolutionsFound >= 1 Then cts.Cancel()
                                             End Sub)

        Dim top = Klassenbildung.SolveKlassenbildungTop(Basis(), zeitlimitS:=600.0, nVarianten:=3, minDistanz:=4,
                                                        cancellationToken:=cts.Token, progress:=progress)

        Assert.IsTrue(progress.Meldungen.Count > 0, "Es kam keine Fortschrittsmeldung an")
        Assert.IsTrue(top.Cancelled, "Cancelled nicht gesetzt")
        Assert.IsTrue(top.Varianten.Count >= 1, "Der Abbruch hat die fertige Variante verworfen")
        Assert.IsTrue(top.Varianten.Count < 3, $"Es wurden trotz Abbruch alle 3 Varianten gerechnet")
        Assert.IsNotNull(top.Varianten(0).Zuordnung)
        ' Der Konsens-Kern wird nur ueber die vorhandenen Varianten gebildet;
        ' bei genau einer Variante ist das folgerichtig jedes Kind.
        Assert.IsNotNull(top.KonsensKern)
    End Sub

    ''' <summary>Die Phasenangaben muessen fuer die Varianten-Schleife
    ''' stimmen - "Variante 2 von 3" ist das, was die GUI anzeigt.</summary>
    <TestMethod>
    Public Sub KlassenbildungProgressReportsVariantePhase()
        Dim progress As New SofortProgressKb(Nothing)

        Klassenbildung.SolveKlassenbildungTop(Basis(), zeitlimitS:=10.0, nVarianten:=3, minDistanz:=4,
                                              progress:=progress)

        Dim meldungen = progress.Meldungen
        Assert.IsTrue(meldungen.Count > 0, "Es kam keine Fortschrittsmeldung an")
        For Each p In meldungen
            Assert.AreEqual(SolvePhase.Variante, p.Phase)
            Assert.AreEqual(3, p.PhaseCount)
            Assert.IsTrue(p.PhaseIndex >= 1 AndAlso p.PhaseIndex <= 3,
                          $"PhaseIndex {p.PhaseIndex} liegt ausserhalb von 1..3")
            Assert.IsFalse(String.IsNullOrWhiteSpace(p.Label))
        Next
    End Sub

End Class
