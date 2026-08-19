' Phase 2.15e: VerifyLehrereinsatz ist unabhaengig von
' Lehrereinsatzplanung.vb's CP-SAT-Code - jeder Test hier baut ein
' LehrereinsatzResult per Hand (kein Solve noetig) und bestaetigt, dass
' ein gezielt eingebauter Fehler erkannt wird, mirrort
' VerifyKursblockungTests.vb's Muster.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class VerifyLehrereinsatzTests

    Private Function Bestand() As Stammdatenbestand
        Dim b As New Stammdatenbestand With {.SchulName = "Test", .Schulart = "Grundschule"}
        b.Klassenstufen.Add(New Klassenstufe With {.Nummer = 1, .Bezeichnung = "Klasse 1"})

        Dim deutsch As New Fach With {.Name = "Deutsch"}
        deutsch.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = 1, .WochenstundenSoll = 4})
        b.Faecher.Add(deutsch)

        b.Klassen.Add(New Klasse With {.Name = "1a", .Klassenstufe = 1})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer A", .DeputatSollstunden = 4, .KlassenlehrerFaehig = True})
        b.Lehrkraefte.Add(New Lehrer With {.Name = "Lehrer B", .DeputatSollstunden = 4, .KlassenlehrerFaehig = False})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer A", .FachName = "Deutsch"})
        Return b
    End Function

    Private Function CleanResult() As LehrereinsatzResult
        Return New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung) From {
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer A", .Klasse = "1a", .Fach = "Deutsch"}
            },
            .Klassenlehrer = New Dictionary(Of String, String) From {{"1a", "Lehrer A"}}
        }
    End Function

    <TestMethod>
    Public Sub CleanResultHasNoViolations()
        Assert.AreEqual(0, Verifier.VerifyLehrereinsatz(Bestand(), CleanResult()).Count)
    End Sub

    <TestMethod>
    Public Sub NoResultYetProducesNoViolations()
        Dim result As New LehrereinsatzResult With {.Zuweisungen = Nothing}
        Assert.AreEqual(0, Verifier.VerifyLehrereinsatz(Bestand(), result).Count)
    End Sub

    <TestMethod>
    Public Sub MissingAssignmentIsDetected()
        Dim result As New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung),
            .Klassenlehrer = New Dictionary(Of String, String)
        }
        Dim violations = Verifier.VerifyLehrereinsatz(Bestand(), result)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("1a/Deutsch") AndAlso v.Contains("erwartet genau 1")), String.Join(vbLf, violations))
    End Sub

    <TestMethod>
    Public Sub DuplicateAssignmentIsDetected()
        Dim result = CleanResult()
        result.Zuweisungen.Add(New LehrereinsatzZuweisung With {.Lehrer = "Lehrer B", .Klasse = "1a", .Fach = "Deutsch"})
        Dim violations = Verifier.VerifyLehrereinsatz(Bestand(), result)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("1a/Deutsch") AndAlso v.Contains("2 Zuweisungen")), String.Join(vbLf, violations))
    End Sub

    <TestMethod>
    Public Sub UnqualifiedTeacherAssignmentIsDetected()
        ' Simuliert einen Bug (z.B. in Lehrereinsatzplanung.vb): Lehrer B
        ' ist laut fach_lehrer_zuordnungen NICHT fuer Deutsch qualifiziert.
        Dim result As New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung) From {
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer B", .Klasse = "1a", .Fach = "Deutsch"}
            },
            .Klassenlehrer = New Dictionary(Of String, String)
        }
        Dim violations = Verifier.VerifyLehrereinsatz(Bestand(), result)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("Lehrer B") AndAlso v.Contains("nicht qualifiziert")), String.Join(vbLf, violations))
    End Sub

    ''' <summary>Lehrer B ist laut Bestand() gar nicht klassenlehrerfaehig.</summary>
    <TestMethod>
    Public Sub KlassenlehrerNotKlassenlehrerFaehigIsDetected()
        Dim result As New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung) From {
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer A", .Klasse = "1a", .Fach = "Deutsch"}
            },
            .Klassenlehrer = New Dictionary(Of String, String) From {{"1a", "Lehrer B"}}
        }
        Dim violations = Verifier.VerifyLehrereinsatz(Bestand(), result)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("'Lehrer B'") AndAlso v.Contains("nicht klassenlehrerfaehig")), String.Join(vbLf, violations))
    End Sub

    ''' <summary>Lehrer A ist klassenlehrerfaehig, unterrichtet aber laut
    ''' Zuweisungen gar nicht in Klasse "1b".</summary>
    <TestMethod>
    Public Sub KlassenlehrerNotActuallyTeachingClassIsDetected()
        Dim result As New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung) From {
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer A", .Klasse = "1a", .Fach = "Deutsch"}
            },
            .Klassenlehrer = New Dictionary(Of String, String) From {{"1b", "Lehrer A"}}
        }
        Dim violations = Verifier.VerifyLehrereinsatz(Bestand(), result)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("1b") AndAlso v.Contains("unterrichtet dort laut Zuweisungen kein Fach")), String.Join(vbLf, violations))
    End Sub

End Class
