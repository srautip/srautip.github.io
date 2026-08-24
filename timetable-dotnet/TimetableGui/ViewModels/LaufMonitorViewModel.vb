' Der Lauf-Monitor (gui-ui-konzept.md 6.13): Stufen-Fortschritt,
' Konvergenzverlauf, wachsende Loesungsliste, Abbrechen.
'
' Das ist reine Anbindung an den Abbruch-/Fortschrittskanal des Kerns
' (arc42 8.11). SolveProgress liefert Phase, PhaseIndex/PhaseCount,
' ElapsedS gegen BudgetS, SolutionsFound und - seit dem Kanal - auch
' IncumbentObjective und BestObjectiveBound. Die Optimalitaetsluecke ist
' damit LIVE zeigbar, nicht erst nach dem Lauf aus ScoredSolution.
'
' Threading: der Solver laeuft in Task.Run, die Meldungen kommen ueber
' Progress(Of T) zurueck. Progress(Of T) faengt den
' SynchronizationContext beim Konstruieren ein - deshalb MUSS dieses
' ViewModel auf dem UI-Thread erzeugt werden, dann marshallt es
' selbsttaetig, und kein Handler fasst ein DispatcherObject vom falschen
' Thread an.
Imports System.Collections.ObjectModel
Imports System.Threading
Imports TimetableCore

Public NotInheritable Class LaufMonitorViewModel
    Inherits Beobachtbar

    Private _quelle As CancellationTokenSource
    Private _laeuft As Boolean
    Private _label As String = ""
    Private _stufe As Integer
    Private _stufenGesamt As Integer
    Private _verstricheneS As Double
    Private _budgetS As Double
    Private _loesungen As Integer
    Private _zielwert As Double?
    Private _schranke As Double?
    Private _abgebrochen As Boolean

    Public Sub New()
        AbbrechenBefehl = New Befehl(Sub() Abbrechen(), Function() Laeuft)
    End Sub

    Public ReadOnly Property AbbrechenBefehl As Befehl

    ''' <summary>Verlauf fuer die Konvergenzkurve - je Meldung mit
    ''' Zielwert ein Punkt. ObservableCollection, damit die Kurve
    ''' mitwaechst, statt erst am Ende zu erscheinen.</summary>
    Public ReadOnly Property Verlauf As New ObservableCollection(Of ConvergencePoint)

    Public Property Laeuft As Boolean
        Get
            Return _laeuft
        End Get
        Private Set
            If Setze(_laeuft, value) Then
                Melde(NameOf(StatusZeile))
                Befehl.MeldeAenderung()
            End If
        End Set
    End Property

    Public Property Abgebrochen As Boolean
        Get
            Return _abgebrochen
        End Get
        Private Set
            Setze(_abgebrochen, value)
        End Set
    End Property

    Public Property Label As String
        Get
            Return _label
        End Get
        Private Set
            If Setze(_label, value) Then Melde(NameOf(StatusZeile))
        End Set
    End Property

    Public Property Stufe As Integer
        Get
            Return _stufe
        End Get
        Private Set
            If Setze(_stufe, value) Then Melde(NameOf(StatusZeile))
        End Set
    End Property

    Public Property StufenGesamt As Integer
        Get
            Return _stufenGesamt
        End Get
        Private Set
            If Setze(_stufenGesamt, value) Then Melde(NameOf(StatusZeile))
        End Set
    End Property

    Public Property VerstricheneS As Double
        Get
            Return _verstricheneS
        End Get
        Private Set
            If Setze(_verstricheneS, value) Then
                Melde(NameOf(FortschrittProzent))
                Melde(NameOf(StatusZeile))
            End If
        End Set
    End Property

    Public Property BudgetS As Double
        Get
            Return _budgetS
        End Get
        Private Set
            If Setze(_budgetS, value) Then Melde(NameOf(FortschrittProzent))
        End Set
    End Property

    Public Property Loesungen As Integer
        Get
            Return _loesungen
        End Get
        Private Set
            If Setze(_loesungen, value) Then Melde(NameOf(StatusZeile))
        End Set
    End Property

    Public Property Zielwert As Double?
        Get
            Return _zielwert
        End Get
        Private Set
            If Setze(_zielwert, value) Then Melde(NameOf(LueckeProzent))
        End Set
    End Property

    Public Property Schranke As Double?
        Get
            Return _schranke
        End Get
        Private Set
            If Setze(_schranke, value) Then Melde(NameOf(LueckeProzent))
        End Set
    End Property

    ''' <summary>0..100. Ohne bekanntes Budget bleibt es bei 0 - ein
    ''' Balken, der raet, ist schlechter als keiner.</summary>
    Public ReadOnly Property FortschrittProzent As Double
        Get
            If BudgetS <= 0.0 Then Return 0.0
            Return Math.Min(100.0, 100.0 * VerstricheneS / BudgetS)
        End Get
    End Property

    ''' <summary>Die noch offene Optimalitaetsluecke in Prozent, live.
    ''' Nothing, solange CP-SAT keine Zwischenloesung hat.</summary>
    Public ReadOnly Property LueckeProzent As Double?
        Get
            If Not Zielwert.HasValue OrElse Not Schranke.HasValue Then Return Nothing
            If Zielwert.Value <= 0.0 Then Return 0.0
            Return 100.0 * Math.Max(Zielwert.Value - Schranke.Value, 0.0) / Zielwert.Value
        End Get
    End Property

    ''' <summary>Die Zeile der Statusleiste, z.B.
    ''' "Stundenplan wird gerechnet (2/5) - 3 Loesungen, 02:41".</summary>
    Public ReadOnly Property StatusZeile As String
        Get
            If Not Laeuft AndAlso Label.Length = 0 Then Return "Bereit"
            Dim teile As New List(Of String)
            If Label.Length > 0 Then
                teile.Add(If(StufenGesamt > 0, $"{Label} ({Stufe}/{StufenGesamt})", Label))
            End If
            If Loesungen > 0 Then teile.Add($"{Loesungen} Loesungen")
            teile.Add(TimeSpan.FromSeconds(VerstricheneS).ToString("mm\:ss"))
            Return String.Join(" - ", teile)
        End Get
    End Property

    ''' <summary>Setzt den Monitor fuer einen neuen Lauf zurueck und
    ''' liefert das Token, das der Aufrufer an den Kern durchreicht.</summary>
    Public Function Starte() As CancellationToken
        _quelle?.Dispose()
        _quelle = New CancellationTokenSource()
        Verlauf.Clear()
        Label = ""
        Stufe = 0
        StufenGesamt = 0
        VerstricheneS = 0.0
        BudgetS = 0.0
        Loesungen = 0
        Zielwert = Nothing
        Schranke = Nothing
        Abgebrochen = False
        Laeuft = True
        Return _quelle.Token
    End Function

    Public Sub Abbrechen()
        If Not Laeuft Then Return
        Abgebrochen = True
        _quelle?.Cancel()
    End Sub

    Public Sub Beende()
        Laeuft = False
    End Sub

    ''' <summary>Nimmt eine Meldung des Kerns entgegen. Wird ueber
    ''' Progress(Of SolveProgress) aufgerufen und landet damit auf dem
    ''' UI-Thread.</summary>
    Public Sub Uebernehmen(p As SolveProgress)
        If p Is Nothing Then Return
        If Not String.IsNullOrWhiteSpace(p.Label) Then Label = p.Label
        Stufe = p.PhaseIndex
        StufenGesamt = p.PhaseCount
        BudgetS = p.BudgetS
        ' Monoton halten: die Kernmeldungen sind es, aber ein
        ' zurueckspringender Balken waere so irritierend, dass die
        ' Absicherung billiger ist als das Risiko.
        If p.ElapsedS > VerstricheneS Then VerstricheneS = p.ElapsedS
        If p.SolutionsFound > Loesungen Then Loesungen = p.SolutionsFound

        If p.IncumbentObjective.HasValue Then
            Dim neu = p.IncumbentObjective.Value
            Zielwert = neu
            Schranke = p.BestObjectiveBound
            ' Nur echte Verbesserungen aufzeichnen - sonst waechst der
            ' Verlauf mit jedem 500ms-Tick, statt die Kurve zu zeigen.
            If Verlauf.Count = 0 OrElse Verlauf.Last().ObjectiveValue <> neu Then
                Verlauf.Add(New ConvergencePoint With {.ElapsedS = p.ElapsedS, .ObjectiveValue = neu})
            End If
        End If
    End Sub

End Class
