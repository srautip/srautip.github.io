' Phase 2.11a: LLM-free, solver-free unit tests for the Kursstufe/Kurssystem
' data model additions (entities.kurse/entities.schienen, the "kurswahl"
' constraint type) and Validation.ValidateKursstufeEntities. No CP-SAT
' involved here - that's Phase 2.11b (Kursblockung) onward.
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore

<TestClass>
Public Class KursstufeValidationTests

    Private Shared Function Kurs(id As String, subject As String, teacher As String,
                                  kursart As String, hoursPerWeek As Integer,
                                  Optional halbjahr As String = "12/1") As JsonObject
        Return New JsonObject From {
            {"id", id}, {"subject", subject}, {"teacher", teacher},
            {"kursart", kursart}, {"hours_per_week", hoursPerWeek}, {"halbjahr", halbjahr}
        }
    End Function

    Private Shared Function Schiene(id As String, kursart As String, hoursPerWeek As Integer) As JsonObject
        Return New JsonObject From {
            {"id", id}, {"kursart", kursart}, {"hours_per_week", hoursPerWeek}
        }
    End Function

    Private Shared Function Kurswahl(wahlprofilId As String, studentCount As Integer,
                                      kurse As IEnumerable(Of String),
                                      Optional reason As String = Nothing) As JsonObject
        Dim c As New JsonObject From {
            {"type", "kurswahl"}, {"wahlprofil_id", wahlprofilId}, {"student_count", studentCount},
            {"kurse", New JsonArray(kurse.Select(Function(k) CType(k, JsonNode)).ToArray())}
        }
        If reason IsNot Nothing Then c("reason") = reason
        Return c
    End Function

    ''' <summary>A minimal, internally-consistent baseline: 2 LK-Schienen
    ''' (5h), 1 GK-Schiene (3h), 4 Kurse (3 LK + 1 GK) forming exactly one
    ''' valid Wahlprofil.</summary>
    Private Shared Function BaselineData() As JsonObject
        Dim ent As New JsonObject From {
            {"classes", New JsonArray()},
            {"teachers", New JsonArray({"Frau Berger", "Herr Klein", "Frau Wolf"})},
            {"subjects", New JsonArray({"Deutsch", "Mathematik", "Biologie"})},
            {"rooms", New JsonArray()},
            {"timeslots", New JsonObject From {
                {"days", New JsonArray({"Mo", "Di", "Mi", "Do", "Fr"})},
                {"periods_per_day", 8}
            }},
            {"kurse", New JsonArray({
                CType(Kurs("D-LK1", "Deutsch", "Frau Berger", "LK", 5), JsonNode),
                Kurs("MA-LK1", "Mathematik", "Herr Klein", "LK", 5),
                Kurs("BIO-LK1", "Biologie", "Frau Wolf", "LK", 5),
                Kurs("MA-GK1", "Mathematik", "Herr Klein", "GK", 3)
            })},
            {"schienen", New JsonArray({
                CType(Schiene("S1", "LK", 5), JsonNode),
                Schiene("S2", "LK", 5),
                Schiene("S3", "GK", 3)
            })}
        }
        Return New JsonObject From {
            {"entities", ent},
            {"constraints", New JsonArray({
                CType(Kurswahl("WP1", 24, {"D-LK1", "MA-LK1", "BIO-LK1", "MA-GK1"}), JsonNode)
            })}
        }
    End Function

    <TestMethod>
    Public Sub BaselineScenarioHasNoErrors()
        Dim errors = Validation.ValidateKursstufeEntities(BaselineData())
        Assert.AreEqual(0, errors.Count, String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub PreExistingFixturesWithoutKurseAreUnaffected()
        ' entities.kurse/schienen absent entirely - the exact shape of every
        ' pre-Phase-2.11 fixture. Must behave identically to plain
        ' ValidateEntities (0 errors for a clean scenario).
        Dim data = TestBuilders.Scenario(
            TestBuilders.Mini({"1a"}, {"Frau Berger"}, {"Deutsch"}, {}, {"Mo"}, 4),
            {New JsonObject From {
                {"type", "teacher_subject_assignment"}, {"class", "1a"}, {"subject", "Deutsch"}, {"teacher", "Frau Berger"}
            }})
        Assert.AreEqual(0, Validation.ValidateKursstufeEntities(data).Count)
    End Sub

    <TestMethod>
    Public Sub InvalidKursartIsRejected()
        Dim data = BaselineData()
        CType(JsonHelpers.GetKurse(JsonHelpers.Entities(data))(0), JsonObject)("kursart") = "XY"
        Dim errors = Validation.ValidateKursstufeEntities(data)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("kursart") AndAlso e.Contains("XY")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub NonPositiveHoursPerWeekIsRejected()
        Dim data = BaselineData()
        CType(JsonHelpers.GetKurse(JsonHelpers.Entities(data))(0), JsonObject)("hours_per_week") = 0
        Dim errors = Validation.ValidateKursstufeEntities(data)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("hours_per_week")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub UnknownKursTeacherIsRejected()
        Dim data = BaselineData()
        CType(JsonHelpers.GetKurse(JsonHelpers.Entities(data))(0), JsonObject)("teacher") = "Herr Unbekannt"
        Dim errors = Validation.ValidateKursstufeEntities(data)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("teacher") AndAlso e.Contains("Herr Unbekannt")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub UnknownKursSubjectIsRejected()
        Dim data = BaselineData()
        CType(JsonHelpers.GetKurse(JsonHelpers.Entities(data))(0), JsonObject)("subject") = "Chemie"
        Dim errors = Validation.ValidateKursstufeEntities(data)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("subject") AndAlso e.Contains("Chemie")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub KursWithoutCompatibleSchieneIsRejected()
        Dim data = BaselineData()
        ' 4h/Woche has no matching Schiene in BaselineData (only 5h-LK/3h-GK exist).
        CType(JsonHelpers.GetKurse(JsonHelpers.Entities(data))(0), JsonObject)("hours_per_week") = 4
        Dim errors = Validation.ValidateKursstufeEntities(data)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("keine Schiene")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub KurswahlReferencingUnknownKursIsRejected()
        ' Exercises the generic FieldEntityKey cross-check (Validation.vb),
        ' now extended with {"kurse","kurse"} - a kurswahl.kurse entry that
        ' doesn't exist in entities.kurse must be caught the same way an
        ' unknown "class"/"teacher" reference already is.
        Dim data = BaselineData()
        CType(data("constraints")(0), JsonObject)("kurse") = New JsonArray({"D-LK1", "MA-LK1", "BIO-LK1", "NICHT-VORHANDEN"})
        Dim errors = Validation.ValidateKursstufeEntities(data)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("NICHT-VORHANDEN") AndAlso e.Contains("keine bekannte Entity")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub KurswahlWithWrongLeistungskursCountIsRejected()
        Dim data = BaselineData()
        ' Only 2 LK (D-LK1, MA-LK1) instead of the required 3.
        CType(data("constraints")(0), JsonObject)("kurse") = New JsonArray({"D-LK1", "MA-LK1", "MA-GK1"})
        Dim errors = Validation.ValidateKursstufeEntities(data)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("Leistungskurse") AndAlso e.Contains("gefunden: 2")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub KurswahlWithNonPositiveStudentCountIsRejected()
        Dim data = BaselineData()
        CType(data("constraints")(0), JsonObject)("student_count") = 0
        Dim errors = Validation.ValidateKursstufeEntities(data)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("student_count")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub DuplicateWahlprofilIdIsRejected()
        Dim data = BaselineData()
        CType(data("constraints"), JsonArray).Add(Kurswahl("WP1", 10, {"D-LK1", "MA-LK1", "BIO-LK1"}))
        Dim errors = Validation.ValidateKursstufeEntities(data)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("nicht eindeutig")), String.Join(vbLf, errors))
    End Sub

    <TestMethod>
    Public Sub KurswahlErrorIncludesReasonWhenSet()
        Dim data = BaselineData()
        Dim c = CType(data("constraints")(0), JsonObject)
        c("kurse") = New JsonArray({"D-LK1", "MA-LK1", "MA-GK1"})
        c("reason") = "Profil A waehlt nur 2 Leistungskurse (Texteingabefehler)"
        Dim errors = Validation.ValidateKursstufeEntities(data)
        Assert.IsTrue(errors.Any(Function(e) e.Contains("Regel-Herkunft") AndAlso e.Contains("Texteingabefehler")), String.Join(vbLf, errors))
    End Sub

End Class
