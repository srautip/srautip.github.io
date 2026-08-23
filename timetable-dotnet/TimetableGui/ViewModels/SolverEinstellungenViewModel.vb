' Solver-Einstellungen (gui-ui-konzept.md 6.12). "Zweistufig, damit der
' Standardfall einfach bleibt": fuenf Felder oben, alles Uebrige in
' einem Ausklappbereich.
'
' "Werte = config.yaml-Felder mit deren Defaults" - die Maske bildet
' RunConfig ab und erfindet keine eigenen Vorgaben. Deshalb sind die
' Expertenfelder durchweg Nullable: `Nothing` heisst "Default des Kerns",
' und genau diese Bedeutung ginge verloren, wenn die Maske sie beim
' Oeffnen mit dem aktuellen Default fuellen wuerde. Ein Projekt haette
' danach ueberall explizite Werte stehen - und wuerde eine spaetere
' Aenderung des Defaults nicht mehr mitbekommen.
Imports TimetableProjekt
Imports TimetableYaml

Public NotInheritable Class SolverEinstellungenViewModel
    Inherits Beobachtbar

    Private ReadOnly _projekt As Projekt

    Public Sub New(projekt As Projekt)
        _projekt = projekt
    End Sub

    Public Event Geaendert As EventHandler

    Private Sub Meldung()
        RaiseEvent Geaendert(Me, EventArgs.Empty)
        Melde(NameOf(DeterminismusHinweis))
    End Sub

    Private ReadOnly Property Config As RunConfig
        Get
            Return _projekt.Config
        End Get
    End Property

    ' ===============================================================
    ' Einfach (6.12): fuenf Felder
    ' ===============================================================

    Public Property ZeitbudgetS As Double
        Get
            Return Config.SolveTimeLimitS
        End Get
        Set
            Config.SolveTimeLimitS = value
            Melde()
            Meldung()
        End Set
    End Property

    Public Property MaxSolutions As Integer
        Get
            Return Config.MaxSolutions
        End Get
        Set
            Config.MaxSolutions = value
            Melde()
            Meldung()
        End Set
    End Property

    ''' <summary>Anzahl Klassenbildungs-Varianten. Liegt im
    ''' Klassenbildungs-Block der Config - wird er erst hier angelegt,
    ''' bleibt er sonst Nothing und der Kern nimmt seine Defaults.</summary>
    Public Property Varianten As Integer?
        Get
            Return Config.Klassenbildung?.NVarianten
        End Get
        Set
            KlassenbildungBlock().NVarianten = value
            Melde()
            Meldung()
        End Set
    End Property

    Public Property NumWorkers As Integer
        Get
            Return Config.NumWorkers
        End Get
        Set
            Config.NumWorkers = Math.Max(1, value)
            Melde()
            Meldung()
        End Set
    End Property

    Public Property Seed As Integer
        Get
            Return Config.Seed
        End Get
        Set
            Config.Seed = value
            Melde()
            Meldung()
        End Set
    End Property

    ''' <summary>Der Hinweis, den 6.12 ausdruecklich am Seed verlangt.
    ''' Er steht NUR bei mehreren Workern - eine Warnung, die immer da
    ''' ist, wird nicht gelesen.
    '''
    ''' Die Aussage stammt nicht aus der Oberflaeche, sondern aus arc42
    ''' 8.5: bei num_workers > 1 sind Laeufe trotz fixem Seed nicht
    ''' deterministisch.</summary>
    Public ReadOnly Property DeterminismusHinweis As String
        Get
            If Config.NumWorkers <= 1 Then
                Return "Mit einem Worker ist der Lauf bei gleichem Seed reproduzierbar."
            End If
            Return $"Mit {Config.NumWorkers} Workern ist der Lauf TROTZ festem Seed nicht " &
                   "reproduzierbar - zwei Laeufe koennen verschiedene Loesungen liefern. " &
                   "Fuer vergleichende Messungen einen Worker waehlen."
        End Get
    End Property

    ' ===============================================================
    ' Experten (6.12): Ausklappbereich
    ' ===============================================================

    Private Function KlassenbildungBlock() As KlassenbildungConfig
        If Config.Klassenbildung Is Nothing Then Config.Klassenbildung = New KlassenbildungConfig()
        Return Config.Klassenbildung
    End Function

    Private Function GewichteBlock() As QualityWeightsConfig
        If Config.QualityWeights Is Nothing Then Config.QualityWeights = New QualityWeightsConfig()
        Return Config.QualityWeights
    End Function

    ''' <summary>Alle Expertenfelder als benannte Liste - so baut die
    ''' Maske sie in einer Schleife und nicht in vierzig fast gleichen
    ''' XAML-Bloecken. `Lesen`/`Schreiben` arbeiten mit Text, weil das
    ''' Feld leer bleiben koennen MUSS: leer = Default des Kerns.</summary>
    Public NotInheritable Class Feld
        Public Property Gruppe As String = ""
        Public Property Name As String = ""
        Public Property Hilfe As String = ""
        Public Property Lesen As Func(Of String)
        Public Property Schreiben As Action(Of String)
    End Class

    Private Shared Function Zahl(t As String) As Double?
        If String.IsNullOrWhiteSpace(t) Then Return Nothing
        Dim d As Double
        If Double.TryParse(t.Trim(), Globalization.NumberStyles.Any,
                           Globalization.CultureInfo.CurrentCulture, d) Then Return d
        Return Nothing
    End Function

    Private Shared Function Ganz(t As String) As Integer?
        Dim d = Zahl(t)
        Return If(d.HasValue, CInt(Math.Round(d.Value)), CType(Nothing, Integer?))
    End Function

    Private Shared Function Wahr(t As String) As Boolean?
        Dim s = If(t, "").Trim().ToLowerInvariant()
        If s = "" Then Return Nothing
        If s = "ja" OrElse s = "true" OrElse s = "1" Then Return True
        If s = "nein" OrElse s = "false" OrElse s = "0" Then Return False
        Return Nothing
    End Function

    Private Shared Function Text(v As Object) As String
        If v Is Nothing Then Return ""
        If TypeOf v Is Boolean Then Return If(CBool(v), "ja", "nein")
        Return Convert.ToString(v, Globalization.CultureInfo.CurrentCulture)
    End Function

    Public Function Expertenfelder() As List(Of Feld)
        Dim f As New List(Of Feld)

        f.Add(New Feld With {.Gruppe = "Grenzen", .Name = "Zeitlimit je Einzel-Solve (s)",
            .Hilfe = "Leer = kein eigenes Limit; dann teilt sich der Lauf das Gesamtbudget.",
            .Lesen = Function() Text(Config.PerSolveTimeLimitS),
            .Schreiben = Sub(t) Config.PerSolveTimeLimitS = Zahl(t)})
        f.Add(New Feld With {.Gruppe = "Grenzen", .Name = "Zeitlimit Stufe 1 (s)",
            .Hilfe = "Lehrereinsatz getrennt begrenzen.",
            .Lesen = Function() Text(Config.Stage1TimeLimitS),
            .Schreiben = Sub(t) Config.Stage1TimeLimitS = Zahl(t)})
        f.Add(New Feld With {.Gruppe = "Grenzen", .Name = "Stagnations-Timeout (s)",
            .Hilfe = "Abbruch, wenn sich der Zielwert so lange nicht verbessert. " &
                     "ACHTUNG: die Abbruchzeit ist Wanduhrzeit - Laeufe mit Timeout sind nicht bitgleich reproduzierbar.",
            .Lesen = Function() Text(Config.StagnationTimeoutS),
            .Schreiben = Sub(t) Config.StagnationTimeoutS = Zahl(t)})
        f.Add(New Feld With {.Gruppe = "Grenzen", .Name = "Relative Luecke",
            .Hilfe = "Abbruch, sobald die bewiesene Optimalitaetsluecke darunter liegt (z.B. 0.02).",
            .Lesen = Function() Text(Config.RelativeGapLimit),
            .Schreiben = Sub(t) Config.RelativeGapLimit = Zahl(t)})

        f.Add(New Feld With {.Gruppe = "Lexikografisch", .Name = "aktiv (ja/nein)",
            .Hilfe = "Kriterien nacheinander optimieren statt gewichtet zu summieren.",
            .Lesen = Function() Text(Config.Lexicographic),
            .Schreiben = Sub(t) Config.Lexicographic = Wahr(t)})
        f.Add(New Feld With {.Gruppe = "Lexikografisch", .Name = "Toleranz",
            .Hilfe = "Wieviel eine fertige Stufe fuer die naechste nachgeben darf.",
            .Lesen = Function() Text(Config.LexTolerance),
            .Schreiben = Sub(t) Config.LexTolerance = Ganz(t)})
        f.Add(New Feld With {.Gruppe = "Lexikografisch", .Name = "Stufe TeacherGaps (ja/nein)",
            .Lesen = Function() Text(Config.LexTeacherGapsStage),
            .Schreiben = Sub(t) Config.LexTeacherGapsStage = Wahr(t)})
        f.Add(New Feld With {.Gruppe = "Lexikografisch", .Name = "Stufe OccupiedDensity (ja/nein)",
            .Lesen = Function() Text(Config.LexOccupiedDensityStage),
            .Schreiben = Sub(t) Config.LexOccupiedDensityStage = Wahr(t)})
        f.Add(New Feld With {.Gruppe = "Lexikografisch", .Name = "Stufe SubjectWindow (ja/nein)",
            .Lesen = Function() Text(Config.LexSubjectWindowStage),
            .Schreiben = Sub(t) Config.LexSubjectWindowStage = Wahr(t)})

        ' Gewichte samt der strukturellen include_*-Schalter. Der
        ' Unterschied ist wichtig genug fuer einen Hilfetext: ein
        ' abgeschaltetes Kriterium wird nicht GESUCHT, sein Wert aber
        ' weiterhin gezaehlt und angezeigt.
        Dim gewicht = Sub(name As String, lesen As Func(Of Double?), schreiben As Action(Of Double?))
                          f.Add(New Feld With {.Gruppe = "Gewichte", .Name = name,
                              .Lesen = Function() Text(lesen()), .Schreiben = Sub(t) schreiben(Zahl(t))})
                      End Sub
        gewicht("Kann-Verstoss", Function() Config.QualityWeights?.Kann, Sub(v) GewichteBlock().Kann = v)
        gewicht("ClassGaps", Function() Config.QualityWeights?.ClassGaps, Sub(v) GewichteBlock().ClassGaps = v)
        gewicht("TeacherGaps", Function() Config.QualityWeights?.TeacherGaps, Sub(v) GewichteBlock().TeacherGaps = v)
        gewicht("EdgePeriod", Function() Config.QualityWeights?.EdgePeriod, Sub(v) GewichteBlock().EdgePeriod = v)
        gewicht("AfternoonDayCount", Function() Config.QualityWeights?.AfternoonDayCount, Sub(v) GewichteBlock().AfternoonDayCount = v)
        gewicht("ClassLoadVariance", Function() Config.QualityWeights?.ClassLoadVariance, Sub(v) GewichteBlock().ClassLoadVariance = v)
        gewicht("TeacherLoadVariance", Function() Config.QualityWeights?.TeacherLoadVariance, Sub(v) GewichteBlock().TeacherLoadVariance = v)
        gewicht("OccupiedDensity", Function() Config.QualityWeights?.OccupiedDensity, Sub(v) GewichteBlock().OccupiedDensity = v)
        gewicht("SubjectWindow", Function() Config.QualityWeights?.SubjectWindow, Sub(v) GewichteBlock().SubjectWindow = v)

        Dim schalter = Sub(name As String, lesen As Func(Of Boolean?), schreiben As Action(Of Boolean?))
                           f.Add(New Feld With {.Gruppe = "In der Suche beruecksichtigen", .Name = name,
                               .Hilfe = "Aus = das Kriterium wird nicht GESUCHT, sein Wert aber weiterhin gezaehlt und angezeigt.",
                               .Lesen = Function() Text(lesen()), .Schreiben = Sub(t) schreiben(Wahr(t))})
                       End Sub
        schalter("ClassGaps (ja/nein)", Function() Config.QualityWeights?.IncludeClassGaps, Sub(v) GewichteBlock().IncludeClassGaps = v)
        schalter("TeacherGaps (ja/nein)", Function() Config.QualityWeights?.IncludeTeacherGaps, Sub(v) GewichteBlock().IncludeTeacherGaps = v)
        schalter("EdgePeriod (ja/nein)", Function() Config.QualityWeights?.IncludeEdgePeriod, Sub(v) GewichteBlock().IncludeEdgePeriod = v)
        schalter("AfternoonDayCount (ja/nein)", Function() Config.QualityWeights?.IncludeAfternoonDayCount, Sub(v) GewichteBlock().IncludeAfternoonDayCount = v)
        schalter("ClassLoadVariance (ja/nein)", Function() Config.QualityWeights?.IncludeClassLoadVariance, Sub(v) GewichteBlock().IncludeClassLoadVariance = v)
        schalter("TeacherLoadVariance (ja/nein)", Function() Config.QualityWeights?.IncludeTeacherLoadVariance, Sub(v) GewichteBlock().IncludeTeacherLoadVariance = v)
        schalter("OccupiedDensity (ja/nein)", Function() Config.QualityWeights?.IncludeOccupiedDensity, Sub(v) GewichteBlock().IncludeOccupiedDensity = v)
        schalter("SubjectWindow (ja/nein)", Function() Config.QualityWeights?.IncludeSubjectWindow, Sub(v) GewichteBlock().IncludeSubjectWindow = v)

        f.Add(New Feld With {.Gruppe = "Mehr-Zuteilungen", .Name = "max. Zuteilungen",
            .Hilfe = "Wieviele Stufe-1-Ergebnisse als Basis fuer Stufe 2 dienen.",
            .Lesen = Function() Text(Config.MaxAssignments),
            .Schreiben = Sub(t) Config.MaxAssignments = Ganz(t)})
        f.Add(New Feld With {.Gruppe = "Mehr-Zuteilungen", .Name = "Toleranz",
            .Lesen = Function() Text(Config.AssignmentTolerance),
            .Schreiben = Sub(t) Config.AssignmentTolerance = Ganz(t)})
        f.Add(New Feld With {.Gruppe = "Mehr-Zuteilungen", .Name = "Mindestdistanz",
            .Lesen = Function() Text(Config.AssignmentMinDiversity),
            .Schreiben = Sub(t) Config.AssignmentMinDiversity = Ganz(t)})

        f.Add(New Feld With {.Gruppe = "Klassenbildung", .Name = "Zeitlimit je Variante (s)",
            .Hilfe = "Gilt JE VARIANTE, nicht fuer den ganzen Lauf - bei drei Varianten also dreimal.",
            .Lesen = Function() Text(Config.Klassenbildung?.ZeitlimitS),
            .Schreiben = Sub(t) KlassenbildungBlock().ZeitlimitS = Zahl(t)})
        f.Add(New Feld With {.Gruppe = "Klassenbildung", .Name = "Epsilon",
            .Hilfe = "Qualitaetsband: wieviel schlechter eine Variante als die beste sein darf.",
            .Lesen = Function() Text(Config.Klassenbildung?.Epsilon),
            .Schreiben = Sub(t) KlassenbildungBlock().Epsilon = Zahl(t)})
        f.Add(New Feld With {.Gruppe = "Klassenbildung", .Name = "Mindestdistanz",
            .Hilfe = "Wieviele Kinder sich zwischen zwei Varianten mindestens unterscheiden muessen.",
            .Lesen = Function() Text(Config.Klassenbildung?.MinDistanz),
            .Schreiben = Sub(t) KlassenbildungBlock().MinDistanz = Ganz(t)})

        Return f
    End Function

    Public Function Pruefe() As List(Of String)
        Dim fehler As New List(Of String)
        If Config.SolveTimeLimitS <= 0 Then fehler.Add("Das Zeitbudget muss groesser als null sein.")
        If Config.MaxSolutions < 1 Then fehler.Add("Es muss mindestens eine Loesung gesucht werden.")
        If Config.NumWorkers < 1 Then fehler.Add("Es braucht mindestens einen Worker.")
        ' NICHT als `Config.Klassenbildung?.NVarianten.HasValue AndAlso ...`:
        ' der Null-Conditional liefert dann ein Boolean?, und ist der
        ' Block Nothing, wirft das If eine NullReferenceException (live
        ' erlebt - gefunden hat es nur der Test).
        Dim varianten = Config.Klassenbildung?.NVarianten
        If varianten.HasValue AndAlso varianten.Value < 1 Then
            fehler.Add("Es muss mindestens eine Klassenbildungs-Variante gerechnet werden.")
        End If
        ' Kein Fehler, aber die haeufigste Fehlbedienung: ein sehr
        ' knappes Budget bei vielen Loesungen liefert schlechte
        ' Ergebnisse, ohne dass jemand den Zusammenhang sieht.
        If Config.MaxSolutions > 10 AndAlso Config.SolveTimeLimitS < Config.MaxSolutions Then
            fehler.Add($"{Config.MaxSolutions} Loesungen in {Config.SolveTimeLimitS:0.#} s - " &
                       "das ist weniger als eine Sekunde je Loesung.")
        End If
        Return fehler
    End Function

End Class
