' Phase 2.18e: wiederverwendbare Curriculum-Templates fuer den `new`-
' Subcommand (Scaffold.vb) - dieselben, bereits recherchierten Zahlen wie
' in TimetableCore.Tests/Fixtures/StammdatenBWFixture.vb (Grundschule
' Kl.1-4, Gemeinschaftsschule Kl.5-10), hier als reine Datenstruktur statt
' testgebundenem Code, damit sowohl die Tests als auch dieses CLI-Tool
' dieselbe recherchierte Grundlage nutzen (keine zweite, abweichende
' Quelle fuer dieselben Fakten). Nutzerentscheidung (Phase-2.18-
' Feinplanung): NUR Baden-Wuerttemberg - fuer jedes andere Bundesland
' liefert TemplateFuer eine klare Fehlermeldung statt erfundener
' Lehrplan-Daten.
Imports TimetableCore

Public NotInheritable Class TemplateFach
    Public Property Name As String
    Public Property WochenstundenSoll As Integer
    Public Property MaxProTag As Integer?
    Public Property BlockLength As Integer?
End Class

Public NotInheritable Class TemplateKlassenstufe
    Public Property Nummer As Integer
    Public Property Bezeichnung As String
    Public Property Faecher As New List(Of TemplateFach)
End Class

''' <summary>Ein Lehrkraefte-Pool-Typ des Templates. Zwei Arten (siehe
''' `GesteuertDurchAnzahlLehrerParameter`): der/die "Klassenlehrer"-Pool(s)
''' - deren Groesse der `--lehrer`-CLI-Parameter im Rundlauf auf alle so
''' markierten Pools verteilt (Nutzerentscheidung "exakte Anzahl") - und
''' die stets benoetigten Fachlehrer-Spezialisten (Religion/Englisch bei
''' Grundschule, NaWi/Sport/Musik-Kunst/Religion bei Gemeinschaftsschule),
''' deren Groesse Scaffold.vb automatisch aus dem tatsaechlichen
''' Wochenstunden-Bedarf der generierten Klassen herleitet (gleiche
''' Bedarfs-Methodik wie in Phase 2.16/2.16-Nachtrag-4 etabliert, hier als
''' Formel statt Handwahl).</summary>
Public NotInheritable Class TemplateLehrerPool
    Public Property NamePrefix As String
    Public Property Faecher As New List(Of String)
    Public Property Deputat As Double
    Public Property Anrechnungsstunden As Double = 0
    Public Property KlassenlehrerFaehig As Boolean
    Public Property GesteuertDurchAnzahlLehrerParameter As Boolean
End Class

Public NotInheritable Class SchoolTemplate
    Public Property Schulart As String
    Public Property PeriodsPerDay As Integer
    Public Property Klassenstufen As New List(Of TemplateKlassenstufe) ' aufsteigend sortiert
    Public Property LehrerPools As New List(Of TemplateLehrerPool)
End Class

Public Module Templates

    Public Function TemplateFuer(bundesland As String, schulart As String) As SchoolTemplate
        If Not String.Equals(bundesland, "BW", StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException(
                $"Kein Template fuer Bundesland '{bundesland}' hinterlegt - aktuell ist nur 'BW' recherchiert " &
                "(siehe docs/phase2-15-lehrereinsatzplanung.md). Lege die Stammdaten fuer andere Bundeslaender " &
                "bitte von Hand an, statt erfundene Lehrplan-Zahlen zu riskieren.")
        End If
        Select Case schulart
            Case "Grundschule" : Return BWGrundschuleTemplate()
            Case "Gemeinschaftsschule" : Return BWGemeinschaftsschuleTemplate()
            Case Else
                Throw New InvalidOperationException(
                    $"Kein Template fuer Schulart '{schulart}' in Bundesland 'BW' hinterlegt - bekannt: " &
                    "'Grundschule', 'Gemeinschaftsschule'.")
        End Select
    End Function

    Private Function Fach(name As String, wochenstunden As Integer, Optional maxProTag As Integer? = Nothing, Optional blockLength As Integer? = Nothing) As TemplateFach
        Return New TemplateFach With {.Name = name, .WochenstundenSoll = wochenstunden, .MaxProTag = maxProTag, .BlockLength = blockLength}
    End Function

    ''' <summary>1:1 dieselben Zahlen wie
    ''' `StammdatenBWFixture.BuildBWGrundschule()`.</summary>
    Private Function BWGrundschuleTemplate() As SchoolTemplate
        Dim t As New SchoolTemplate With {.Schulart = "Grundschule", .PeriodsPerDay = 6}

        Dim kl1Kl2 = Function() As List(Of TemplateFach)
                         Return New List(Of TemplateFach) From {
                             Fach("Deutsch", 6, maxProTag:=2), Fach("Mathematik", 5, maxProTag:=2), Fach("Sachunterricht", 3, maxProTag:=2),
                             Fach("Sport", 3, maxProTag:=1), Fach("Musik", 2, maxProTag:=1), Fach("Kunst", 2, maxProTag:=1), Fach("Religion", 2, maxProTag:=1)
                         }
                     End Function
        Dim kl3Kl4 = Function() As List(Of TemplateFach)
                         Return New List(Of TemplateFach) From {
                             Fach("Deutsch", 5, maxProTag:=2), Fach("Mathematik", 5, maxProTag:=2), Fach("Sachunterricht", 3, maxProTag:=2),
                             Fach("Englisch", 2, maxProTag:=1), Fach("Sport", 3, maxProTag:=1), Fach("Musik", 2, maxProTag:=1),
                             Fach("Kunst", 2, maxProTag:=1), Fach("Religion", 2, maxProTag:=1)
                         }
                     End Function

        t.Klassenstufen.Add(New TemplateKlassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1", .Faecher = kl1Kl2()})
        t.Klassenstufen.Add(New TemplateKlassenstufe With {.Nummer = 2, .Bezeichnung = "Klasse 2", .Faecher = kl1Kl2()})
        t.Klassenstufen.Add(New TemplateKlassenstufe With {.Nummer = 3, .Bezeichnung = "Klasse 3", .Faecher = kl3Kl4()})
        t.Klassenstufen.Add(New TemplateKlassenstufe With {.Nummer = 4, .Bezeichnung = "Klasse 4", .Faecher = kl3Kl4()})

        t.LehrerPools.Add(New TemplateLehrerPool With {
            .NamePrefix = "Klassenlehrer", .Faecher = New List(Of String) From {"Deutsch", "Mathematik", "Sachunterricht", "Sport", "Musik", "Kunst"},
            .Deputat = 28, .Anrechnungsstunden = 2, .KlassenlehrerFaehig = True, .GesteuertDurchAnzahlLehrerParameter = True
        })
        t.LehrerPools.Add(New TemplateLehrerPool With {
            .NamePrefix = "Religionslehrer", .Faecher = New List(Of String) From {"Religion"},
            .Deputat = 16, .KlassenlehrerFaehig = False, .GesteuertDurchAnzahlLehrerParameter = False
        })
        t.LehrerPools.Add(New TemplateLehrerPool With {
            .NamePrefix = "Englischlehrer", .Faecher = New List(Of String) From {"Englisch"},
            .Deputat = 8, .KlassenlehrerFaehig = False, .GesteuertDurchAnzahlLehrerParameter = False
        })
        Return t
    End Function

    ''' <summary>1:1 dieselben Zahlen wie
    ''' `StammdatenBWFixture.BuildBWGemeinschaftsschule()`.</summary>
    Private Function BWGemeinschaftsschuleTemplate() As SchoolTemplate
        Dim t As New SchoolTemplate With {.Schulart = "Gemeinschaftsschule", .PeriodsPerDay = 8}

        Dim kernfaecher = Function() As List(Of TemplateFach)
                              Return New List(Of TemplateFach) From {
                                  Fach("Deutsch", 4, maxProTag:=2), Fach("Mathematik", 4, maxProTag:=2), Fach("Englisch", 4, maxProTag:=2),
                                  Fach("Erdkunde", 2, maxProTag:=1), Fach("Sport", 3, maxProTag:=1), Fach("Religion", 2, maxProTag:=1)
                              }
                          End Function

        t.Klassenstufen.Add(New TemplateKlassenstufe With {.Nummer = 5, .Bezeichnung = "Klasse 5", .Faecher = Combine(kernfaecher(),
            Fach("BNT", 2, maxProTag:=1), Fach("Musik", 2, maxProTag:=1), Fach("Kunst", 2, maxProTag:=1))})
        t.Klassenstufen.Add(New TemplateKlassenstufe With {.Nummer = 6, .Bezeichnung = "Klasse 6", .Faecher = Combine(kernfaecher(),
            Fach("BNT", 2, maxProTag:=1), Fach("Geschichte", 2, maxProTag:=1), Fach("Musik", 2, maxProTag:=1), Fach("Kunst", 2, maxProTag:=1))})
        t.Klassenstufen.Add(New TemplateKlassenstufe With {.Nummer = 7, .Bezeichnung = "Klasse 7", .Faecher = Combine(kernfaecher(),
            Fach("Biologie", 2, maxProTag:=1), Fach("Physik", 2, maxProTag:=2, blockLength:=2), Fach("Geschichte", 2, maxProTag:=1),
            Fach("Musik", 2, maxProTag:=1), Fach("Kunst", 2, maxProTag:=1))})
        t.Klassenstufen.Add(New TemplateKlassenstufe With {.Nummer = 8, .Bezeichnung = "Klasse 8", .Faecher = Combine(kernfaecher(),
            Fach("Biologie", 2, maxProTag:=1), Fach("Physik", 2, maxProTag:=2, blockLength:=2), Fach("Chemie", 2, maxProTag:=2, blockLength:=2),
            Fach("Gemeinschaftskunde", 2, maxProTag:=1), Fach("Geschichte", 2, maxProTag:=1), Fach("Musik", 2, maxProTag:=1), Fach("Kunst", 2, maxProTag:=1))})
        t.Klassenstufen.Add(New TemplateKlassenstufe With {.Nummer = 9, .Bezeichnung = "Klasse 9", .Faecher = Combine(kernfaecher(),
            Fach("Biologie", 2, maxProTag:=1), Fach("Physik", 2, maxProTag:=2, blockLength:=2), Fach("Chemie", 2, maxProTag:=2, blockLength:=2),
            Fach("Gemeinschaftskunde", 2, maxProTag:=1), Fach("Geschichte", 2, maxProTag:=1))})
        t.Klassenstufen.Add(New TemplateKlassenstufe With {.Nummer = 10, .Bezeichnung = "Klasse 10", .Faecher = Combine(kernfaecher(),
            Fach("Biologie", 2, maxProTag:=1), Fach("Physik", 2, maxProTag:=2, blockLength:=2), Fach("Chemie", 2, maxProTag:=2, blockLength:=2),
            Fach("Gemeinschaftskunde", 2, maxProTag:=1), Fach("Geschichte", 2, maxProTag:=1))})

        t.LehrerPools.Add(New TemplateLehrerPool With {
            .NamePrefix = "Deutsch-Geschichte-Lehrer", .Faecher = New List(Of String) From {"Deutsch", "Geschichte", "Gemeinschaftskunde"},
            .Deputat = 27, .KlassenlehrerFaehig = True, .GesteuertDurchAnzahlLehrerParameter = True
        })
        t.LehrerPools.Add(New TemplateLehrerPool With {
            .NamePrefix = "Mathematik-Physik-Lehrer", .Faecher = New List(Of String) From {"Mathematik", "Physik"},
            .Deputat = 27, .KlassenlehrerFaehig = True, .GesteuertDurchAnzahlLehrerParameter = True
        })
        t.LehrerPools.Add(New TemplateLehrerPool With {
            .NamePrefix = "Englisch-Erdkunde-Lehrer", .Faecher = New List(Of String) From {"Englisch", "Erdkunde"},
            .Deputat = 27, .KlassenlehrerFaehig = True, .GesteuertDurchAnzahlLehrerParameter = True
        })
        t.LehrerPools.Add(New TemplateLehrerPool With {
            .NamePrefix = "NaWi-Lehrer", .Faecher = New List(Of String) From {"Biologie", "Chemie", "BNT"},
            .Deputat = 27, .KlassenlehrerFaehig = False, .GesteuertDurchAnzahlLehrerParameter = False
        })
        t.LehrerPools.Add(New TemplateLehrerPool With {
            .NamePrefix = "Sportlehrer", .Faecher = New List(Of String) From {"Sport"},
            .Deputat = 27, .KlassenlehrerFaehig = False, .GesteuertDurchAnzahlLehrerParameter = False
        })
        t.LehrerPools.Add(New TemplateLehrerPool With {
            .NamePrefix = "Musik-Kunst-Lehrer", .Faecher = New List(Of String) From {"Musik", "Kunst"},
            .Deputat = 27, .KlassenlehrerFaehig = False, .GesteuertDurchAnzahlLehrerParameter = False
        })
        t.LehrerPools.Add(New TemplateLehrerPool With {
            .NamePrefix = "Religionslehrer", .Faecher = New List(Of String) From {"Religion"},
            .Deputat = 24, .KlassenlehrerFaehig = False, .GesteuertDurchAnzahlLehrerParameter = False
        })
        Return t
    End Function

    Private Function Combine(basis As List(Of TemplateFach), ParamArray zusatz() As TemplateFach) As List(Of TemplateFach)
        basis.AddRange(zusatz)
        Return basis
    End Function

End Module
