' Abgeleitete Kennzahlen der Stammdaten-Masken (gui-ui-konzept.md 6.4,
' 6.6, 6.7). Sie beantworten Fragen, die das Konzept ausdruecklich VOR
' dem Lauf beantwortet sehen will:
'
'   6.4 "Fusszeile: Summen-Kontrolle Wochenstunden je Stufe (Soll gegen
'        periods_per_day x Tage)."
'   6.6 "Live-Plausibilitaet im Kopf des Dialogs: Summe der
'        Soll-Deputate (abzueglich Anrechnungen/Reserve) gegen den
'        Gesamtstundenbedarf aus 6.4 - die 'Kanarienvogel'-Erfahrung des
'        GMS-Beispiels (ueberdimensionierte Pools erzeugen verteilten
'        Deputat-Leerlauf) wird so VOR dem Lauf sichtbar, nicht erst im
'        Ergebnis."
'   6.7 "Spaltenfuss zeigt Bedarf vs. verfuegbare Deputate je Fach und
'        faerbt Engpaesse rot."
'
' EINSEITIGKEIT, die man kennen muss: diese Zahlen koennen ein Problem
' BEWEISEN, aber nie dessen Abwesenheit. Ist der Bedarf groesser als die
' Kapazitaet, ist der Plan sicher unloesbar. Umgekehrt heisst genug
' Deputat NICHT, dass es aufgeht - dafuer gibt es den Solver. Die Masken
' formulieren ihre Meldungen entsprechend.
'
' Bewusst hier und nicht in TimetableCore: es ist Anzeigearithmetik der
' Oberflaeche, keine Modelllogik. Im Kern haette sie die volle Suite
' ausgeloest, ohne dort je gebraucht zu werden.
Imports TimetableCore

Public Module Kennzahlen

    ''' <summary>Wieviele Klassen es in dieser Stufe gibt. Ohne Klassen
    ''' erzeugt ein Fachkontingent keinen Bedarf - eine Stufe auf dem
    ''' Papier unterrichtet niemanden.</summary>
    Public Function KlassenInStufe(b As Stammdatenbestand, stufe As Integer) As Integer
        Return b.Klassen.Where(Function(k) k.Klassenstufe = stufe).Count
    End Function

    ''' <summary>Der Parallelverbund, in dem dieses Fach auf dieser Stufe
    ''' unterrichtet wird - oder Nothing. Faecher eines Verbunds laufen
    ''' GLEICHZEITIG (Religion-ev / Religion-kath / Ethik), belegen die
    ''' Klassenwoche also nur einmal.</summary>
    Private Function VerbundVon(b As Stammdatenbestand, fachName As String, stufe As Integer) As String
        Return b.Gruppen.
            Where(Function(g) g.FachName = fachName AndAlso
                              g.Klassenstufe.GetValueOrDefault(-1) = stufe AndAlso
                              Not String.IsNullOrWhiteSpace(g.Parallelverbund)).
            Select(Function(g) g.Parallelverbund).
            FirstOrDefault()
    End Function

    ''' <summary>Stunden, die eine einzelne Klasse dieser Stufe laut
    ''' Kontingenten braucht.
    '''
    ''' Ein Parallelverbund zaehlt EINMAL, nicht je Fach: das Kind sitzt
    ''' in Religion ODER Ethik, nicht nacheinander in beidem. Eine naive
    ''' Summe ueber alle Faecher haette die Klassenwoche der
    ''' Beispielschule um vier Stunden zu voll gerechnet (live
    ''' gemessen).</summary>
    Public Function SollJeKlasse(b As Stammdatenbestand, stufe As Integer) As Integer
        Dim gezaehlteVerbuende As New HashSet(Of String)(StringComparer.Ordinal)
        Dim summe = 0
        For Each f In b.Faecher
            Dim fk = f.Klassenstufen.FirstOrDefault(Function(x) x.Klassenstufe = stufe)
            If fk Is Nothing Then Continue For
            Dim verbund = VerbundVon(b, f.Name, stufe)
            If verbund IsNot Nothing AndAlso Not gezaehlteVerbuende.Add(verbund) Then Continue For
            summe += fk.WochenstundenSoll
        Next
        Return summe
    End Function

    ''' <summary>Stunden, die in eine Klassenwoche passen.</summary>
    Public Function KapazitaetJeKlasse(b As Stammdatenbestand) As Integer
        Return b.PeriodsPerDay * b.Tage.Count
    End Function

    ''' <summary>Gesamter LEHRERstundenbedarf - die Zahl, gegen die 6.6
    ''' die Deputate haelt. Bewusst die Summe der Fachbedarfe und nicht
    ''' "Klassenwoche x Klassen": Fachgruppen und Parallelverbuende
    ''' machen beide Zahlen verschieden, und gefragt ist hier, wieviele
    ''' Lehrerstunden gegeben werden muessen.</summary>
    Public Function Gesamtbedarf(b As Stammdatenbestand) As Integer
        Return b.Faecher.Sum(Function(f) BedarfJeFach(b, f.Name))
    End Function

    ''' <summary>Deputat, das nach Abzug von Anrechnungen und
    ''' Springerreserve tatsaechlich fuer Unterricht zur Verfuegung
    ''' steht.</summary>
    Public Function VerfuegbaresDeputat(b As Stammdatenbestand) As Double
        Return b.Lehrkraefte.Sum(Function(l) l.DeputatSollstunden - l.Anrechnungsstunden - l.SpringerReserveStunden)
    End Function

    Public Function DeputatVon(l As Lehrer) As Double
        Return l.DeputatSollstunden - l.Anrechnungsstunden - l.SpringerReserveStunden
    End Function

    ''' <summary>Lehrerstunden, die dieses Fach woechentlich verlangt.
    '''
    ''' DIE BEDARFSTRAEGER SIND NICHT IMMER DIE KLASSEN. Wird ein Fach
    ''' ueber Fachgruppen unterrichtet (Religion, Foerderung,
    ''' Niveaukurse), zaehlen die GRUPPEN - eine Religionsgruppe fasst
    ''' die Kinder mehrerer Parallelklassen zusammen und braucht
    ''' trotzdem nur eine Lehrkraft.
    '''
    ''' Die erste Fassung rechnete stur "Stunden x Klassen" und meldete
    ''' fuer die Grundschul-Beispielschule drei Engpaesse, die es nicht
    ''' gibt: Religion-ev 16 statt 8, Religion-kath 16 statt 8, Chor 8
    ''' statt 4 - jeweils genau um die Zahl der Zuege zu hoch. Die Schule
    ''' loest nachweislich; der Fehler lag in der Kennzahl.</summary>
    Public Function BedarfJeFach(b As Stammdatenbestand, fachName As String) As Integer
        Dim f = b.Faecher.FirstOrDefault(Function(x) x.Name = fachName)
        If f Is Nothing Then Return 0

        Dim gruppen = b.Gruppen.Where(Function(g) g.FachName = fachName).ToList()
        If gruppen.Count > 0 Then
            Return gruppen.Sum(Function(g)
                                   Dim fk = f.Klassenstufen.FirstOrDefault(
                                       Function(x) x.Klassenstufe = g.Klassenstufe.GetValueOrDefault(-1))
                                   Return If(fk Is Nothing, 0, fk.WochenstundenSoll)
                               End Function)
        End If

        Return f.Klassenstufen.Sum(Function(fk) fk.WochenstundenSoll * KlassenInStufe(b, fk.Klassenstufe))
    End Function

    ''' <summary>Deputat aller Lehrkraefte, die dieses Fach unterrichten
    ''' duerfen.
    '''
    ''' OBERGRENZE, keine Zusage: dieselbe Lehrkraft zaehlt bei jedem
    ''' ihrer Faecher voll mit. Wer Deutsch UND Mathe kann, erscheint in
    ''' beiden Spalten mit vollem Deputat, kann seine Stunden aber nur
    ''' einmal geben. Zu wenig ist deshalb ein BEWEIS fuer einen
    ''' Engpass; genug ist keiner fuer das Gegenteil.</summary>
    Public Function DeputatFuerFach(b As Stammdatenbestand, fachName As String) As Double
        Dim qualifiziert = b.FachLehrerZuordnungen.
            Where(Function(z) z.FachName = fachName).
            Select(Function(z) z.LehrerName).
            ToHashSet(StringComparer.Ordinal)
        Return b.Lehrkraefte.Where(Function(l) qualifiziert.Contains(l.Name)).Sum(AddressOf DeputatVon)
    End Function

    ''' <summary>Die Faecher, deren Bedarf das Deputat ihrer
    ''' qualifizierten Lehrkraefte uebersteigt - der Engpass, den 6.7 rot
    ''' faerbt. Faecher ohne jede qualifizierte Lehrkraft stehen zuerst:
    ''' das ist die Luecke, die StammdatenValidation offenlaesst und die
    ''' 6.7 praeventiv sichtbar machen soll.</summary>
    Public Function EngpassFaecher(b As Stammdatenbestand) As List(Of (Fach As String, Bedarf As Integer, Deputat As Double))
        Dim liste As New List(Of (Fach As String, Bedarf As Integer, Deputat As Double))
        For Each f In b.Faecher
            Dim bedarf = BedarfJeFach(b, f.Name)
            If bedarf = 0 Then Continue For
            Dim deputat = DeputatFuerFach(b, f.Name)
            If deputat < bedarf Then liste.Add((f.Name, bedarf, deputat))
        Next
        Return liste.OrderBy(Function(x) x.Deputat).ThenBy(Function(x) x.Fach).ToList()
    End Function

    ''' <summary>Welche Regeln ins Leere zeigen wuerden, wenn das Raster
    ''' auf diese Groesse verkleinert wird (6.2: "Warnung mit
    ''' Konsequenzliste, wenn Tage/Stunden verkleinert werden, waehrend
    ''' Slot-Regeln oder Fenster existieren, die dann ins Leere
    ''' zeigen").</summary>
    Public Function RegelnAusserhalb(constraints As IEnumerable(Of Text.Json.Nodes.JsonObject),
                                     tage As IEnumerable(Of String),
                                     periodsPerDay As Integer) As List(Of String)
        Dim erlaubteTage As New HashSet(Of String)(tage, StringComparer.Ordinal)
        Dim betroffen As New List(Of String)
        If constraints Is Nothing Then Return betroffen

        For Each c In constraints
            Dim typ = JsonHelpers.GetString(c, "type")
            Dim grund As String = Nothing

            ' Einzelner Tag bzw. einzelne Stunde
            Dim tag = JsonHelpers.GetString(c, "day")
            If tag IsNot Nothing AndAlso Not erlaubteTage.Contains(tag) Then grund = $"Tag {tag}"
            If grund Is Nothing AndAlso c.ContainsKey("period") AndAlso c("period") IsNot Nothing Then
                Dim p = JsonHelpers.GetInt(c, "period")
                If p > periodsPerDay Then grund = $"Stunde {p}"
            End If

            ' Tageslisten und von/bis-Fenster
            If grund Is Nothing AndAlso c.ContainsKey("days") AndAlso c("days") IsNot Nothing Then
                Dim fehlende = JsonHelpers.AsStringList(c("days")).Where(Function(d) Not erlaubteTage.Contains(d)).ToList()
                If fehlende.Count > 0 Then grund = "Tage " & String.Join("/", fehlende)
            End If
            For Each feld In {"period_from", "period_to", "from_period", "to_period"}
                If grund IsNot Nothing Then Exit For
                If c.ContainsKey(feld) AndAlso c(feld) IsNot Nothing Then
                    Dim p = JsonHelpers.GetInt(c, feld)
                    If p > periodsPerDay Then grund = $"{feld} {p}"
                End If
            Next

            If grund IsNot Nothing Then
                Dim wen = JsonHelpers.GetString(c, "entity")
                betroffen.Add($"{typ}{If(wen Is Nothing, "", " (" & wen & ")")}: {grund}")
            End If
        Next
        Return betroffen
    End Function

End Module
