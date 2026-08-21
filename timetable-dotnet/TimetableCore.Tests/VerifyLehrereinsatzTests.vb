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
    ''' <summary>Phase 2.20c: a Fach taught via a klassenuebergreifende
    ''' Gruppe (Stammdaten.Gruppe.FachName set, spanning 2 real classes via
    ''' Stammdaten.KlassenOfGruppe) requires the SAME teacher in every real
    ''' class it spans - otherwise the "parallel_group" Solver.vb
    ''' constraint's forced slot-synchronization would be physically
    ''' impossible.</summary>
    Private Function BestandMitGruppe() As Stammdatenbestand
        Dim b = Bestand()
        b.Klassen.Add(New Klasse With {.Name = "1b", .Klassenstufe = 1})
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "Deutsch"})
        b.Schueler.Add(New Schueler With {.Id = "S1", .Klasse = "1a"})
        b.Schueler.Add(New Schueler With {.Id = "S2", .Klasse = "1b"})
        b.Gruppen.Add(New Gruppe With {
            .Name = "Deutsch-Gruppe-Kl1", .FachName = "Deutsch", .Klassenstufe = 1,
            .MitgliederSchuelerIds = New List(Of String) From {"S1", "S2"}
        })
        Return b
    End Function

    <TestMethod>
    Public Sub GruppeWithConsistentTeacherAcrossClassesHasNoGruppenViolation()
        Dim b = BestandMitGruppe()
        Dim result As New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung) From {
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer A", .Klasse = "1a", .Fach = "Deutsch"},
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer A", .Klasse = "1b", .Fach = "Deutsch"}
            },
            .Klassenlehrer = New Dictionary(Of String, String)
        }
        Dim violations = Verifier.VerifyLehrereinsatz(b, result)
        Assert.IsFalse(violations.Any(Function(v) v.Contains("Gruppe")), String.Join(vbLf, violations))
    End Sub

    <TestMethod>
    Public Sub GruppeWithDifferingTeachersAcrossClassesIsDetected()
        Dim b = BestandMitGruppe()
        Dim result As New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung) From {
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer A", .Klasse = "1a", .Fach = "Deutsch"},
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer B", .Klasse = "1b", .Fach = "Deutsch"}
            },
            .Klassenlehrer = New Dictionary(Of String, String)
        }
        Dim violations = Verifier.VerifyLehrereinsatz(b, result)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("Gruppe 'Deutsch-Gruppe-Kl1'") AndAlso v.Contains("unterschiedliche Lehrkraefte")), String.Join(vbLf, violations))
    End Sub

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

    ' Phase 2.26: Kanarienvogel-Pruefung fuer die harte FesteZuordnung-
    ' Pinnung - unabhaengig von Lehrereinsatzplanung.vb's Constraint-Code.

    <TestMethod>
    Public Sub FesteZuordnungHonoredProducesNoViolation()
        Dim b = Bestand()
        b.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Lehrer A", .KlasseName = "1a", .FachName = "Deutsch"})
        Assert.AreEqual(0, Verifier.VerifyLehrereinsatz(b, CleanResult()).Count)
    End Sub

    ''' <summary>Simuliert einen Bug in Lehrereinsatzplanung.vb: die
    ''' FesteZuordnung verlangt Lehrer A, das Ergebnis weist aber Lehrer B
    ''' zu - unabhaengig aus bestand.FesteZuordnungen + result.Zuweisungen
    ''' erkannt, kein geteilter Code mit dem Solver.</summary>
    <TestMethod>
    Public Sub FesteZuordnungViolatedByDifferentTeacherIsDetected()
        Dim b = Bestand()
        b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = "Lehrer B", .FachName = "Deutsch"})
        b.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Lehrer A", .KlasseName = "1a", .FachName = "Deutsch"})
        Dim result As New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung) From {
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer B", .Klasse = "1a", .Fach = "Deutsch"}
            },
            .Klassenlehrer = New Dictionary(Of String, String)
        }
        Dim violations = Verifier.VerifyLehrereinsatz(b, result)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("Lehrer A/1a/Deutsch") AndAlso v.Contains("tatsaechlich zugewiesen ist 'Lehrer B'")), String.Join(vbLf, violations))
    End Sub

    ''' <summary>FesteZuordnung existiert, aber das Ergebnis enthaelt gar
    ''' keine Zuweisung fuer diese (Klasse,Fach)-Kombination.</summary>
    <TestMethod>
    Public Sub FesteZuordnungWithMissingAssignmentIsDetected()
        Dim b = Bestand()
        b.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Lehrer A", .KlasseName = "1a", .FachName = "Deutsch"})
        Dim result As New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung),
            .Klassenlehrer = New Dictionary(Of String, String)
        }
        Dim violations = Verifier.VerifyLehrereinsatz(b, result)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("Lehrer A/1a/Deutsch") AndAlso v.Contains("keine Zuweisung fuer 1a/Deutsch im Ergebnis gefunden")), String.Join(vbLf, violations))
    End Sub

    ' Phase 2.27: FesteZuordnung-Erweiterung auf Gruppen-gefuehrte Faecher -
    ' klasse_name traegt hier den Gruppennamen; result.Zuweisungen ist laut
    ' bestehender Dokumentation IMMER Gruppen-EXPANDIERT (eine Zeile pro real
    ' umspannter Klasse), der Kanarienvogel muss deshalb ALLE von
    ' Stammdaten.KlassenOfGruppe gelieferten Klassen pruefen. Wiederverwendet
    ' die bereits bestehende BestandMitGruppe()-Helper-Funktion (1a+1b,
    ' Gruppe "Deutsch-Gruppe-Kl1", Lehrer A UND B beide fuer Deutsch
    ' qualifiziert).

    <TestMethod>
    Public Sub FesteZuordnungOnGruppeHonoredProducesNoViolation()
        Dim b = BestandMitGruppe()
        b.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Lehrer A", .KlasseName = "Deutsch-Gruppe-Kl1", .FachName = "Deutsch"})
        Dim result As New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung) From {
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer A", .Klasse = "1a", .Fach = "Deutsch"},
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer A", .Klasse = "1b", .Fach = "Deutsch"}
            },
            .Klassenlehrer = New Dictionary(Of String, String)
        }
        Assert.AreEqual(0, Verifier.VerifyLehrereinsatz(b, result).Count)
    End Sub

    ''' <summary>Simuliert einen Bug: der Gruppen-Pin verlangt Lehrer A fuer
    ''' ALLE real umspannten Klassen, das Ergebnis weist aber fuer 1b Lehrer
    ''' B zu.</summary>
    <TestMethod>
    Public Sub FesteZuordnungOnGruppeViolatedByDifferentTeacherIsDetected()
        Dim b = BestandMitGruppe()
        b.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Lehrer A", .KlasseName = "Deutsch-Gruppe-Kl1", .FachName = "Deutsch"})
        Dim result As New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung) From {
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer A", .Klasse = "1a", .Fach = "Deutsch"},
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer B", .Klasse = "1b", .Fach = "Deutsch"}
            },
            .Klassenlehrer = New Dictionary(Of String, String)
        }
        Dim violations = Verifier.VerifyLehrereinsatz(b, result)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("Lehrer A/Deutsch-Gruppe-Kl1/Deutsch") AndAlso v.Contains("tatsaechlich zugewiesen ist 'Lehrer B'") AndAlso v.Contains("1b")), String.Join(vbLf, violations))
    End Sub

    ''' <summary>Gruppen-Pin existiert, aber das Ergebnis enthaelt fuer eine
    ''' der real umspannten Klassen (1b) gar keine Zuweisung.</summary>
    <TestMethod>
    Public Sub FesteZuordnungOnGruppeWithMissingAssignmentIsDetected()
        Dim b = BestandMitGruppe()
        b.FesteZuordnungen.Add(New FesteZuordnung With {.LehrerName = "Lehrer A", .KlasseName = "Deutsch-Gruppe-Kl1", .FachName = "Deutsch"})
        Dim result As New LehrereinsatzResult With {
            .Zuweisungen = New List(Of LehrereinsatzZuweisung) From {
                New LehrereinsatzZuweisung With {.Lehrer = "Lehrer A", .Klasse = "1a", .Fach = "Deutsch"}
            },
            .Klassenlehrer = New Dictionary(Of String, String)
        }
        Dim violations = Verifier.VerifyLehrereinsatz(b, result)
        Assert.IsTrue(violations.Any(Function(v) v.Contains("Lehrer A/Deutsch-Gruppe-Kl1/Deutsch") AndAlso v.Contains("keine Zuweisung fuer 1b/Deutsch im Ergebnis gefunden")), String.Join(vbLf, violations))
    End Sub

End Class
