' Stufe B des GUI-Unterbaus (docs/gui-implementierungsplan.md): fuer
' constraints.yaml, klassenbildung.yaml und config.yaml gab es bisher NUR
' eine Leseseite. Die Phase-3-GUI braucht den Rueckschreibweg (Regel-Masken,
' Klassenbildungs-Board mit U5-Re-Solve, Solver-Einstellungsdialog).
'
' Geprueft wird gegen die ECHTEN Beispieldateien der beiden committeten
' Schulen, nicht gegen handgeschriebene Minimalbeispiele - die Dateien sind
' mit 780 bzw. 2204 Zeilen Constraints die realistischste verfuegbare
' Belastungsprobe, und ein Schreiber, der nur synthetische Faelle
' uebersteht, ist wertlos.
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableYaml

<TestClass>
Public Class YamlRoundTripTests

    ''' <summary>Findet das Repo-Verzeichnis, indem vom Assembly-Ort aus
    ''' aufwaerts gesucht wird - robust gegen Debug/Release und gegen den
    ''' Arbeitsverzeichnis-Wechsel, den der Testrunner vornimmt.</summary>
    Private Shared ReadOnly Property TestsRoot As String
        Get
            Dim dir = New IO.DirectoryInfo(AppContext.BaseDirectory)
            While dir IsNot Nothing
                Dim kandidat = IO.Path.Combine(dir.FullName, "tests", "bw-grundschule-beispiel")
                If IO.Directory.Exists(kandidat) Then Return IO.Path.Combine(dir.FullName, "tests")
                dir = dir.Parent
            End While
            Throw New InvalidOperationException("tests/-Verzeichnis oberhalb von " & AppContext.BaseDirectory & " nicht gefunden")
        End Get
    End Property

    Private Shared Function InputPfad(schule As String, datei As String) As String
        Return IO.Path.Combine(TestsRoot, schule, "input", datei)
    End Function

    ''' <summary>Vergleichsform mit rekursiv SORTIERTEN Schluesseln. Ohne
    ''' das haenge der Test an der Aufzaehlungsreihenfolge eines
    ''' Dictionary(Of String, Object), die .NET nicht zusichert - er wuerde
    ''' dann gelegentlich rot, ohne dass inhaltlich etwas falsch waere.</summary>
    Private Shared Function Kanonisch(node As JsonNode) As String
        Return SortiereNode(node).ToJsonString()
    End Function

    Private Shared Function SortiereNode(node As JsonNode) As JsonNode
        If node Is Nothing Then Return Nothing
        Dim obj = TryCast(node, JsonObject)
        If obj IsNot Nothing Then
            Dim neu As New JsonObject()
            For Each schluessel In obj.Select(Function(k) k.Key).OrderBy(Function(k) k, StringComparer.Ordinal)
                neu(schluessel) = SortiereNode(obj(schluessel))
            Next
            Return neu
        End If
        Dim arr = TryCast(node, JsonArray)
        If arr IsNot Nothing Then
            Dim neuArr As New JsonArray()
            ' Reihenfolge der Sequenz ist bedeutungstragend (je Constraint
            ' ein Eintrag) und wird deshalb NICHT sortiert.
            For Each item In arr
                neuArr.Add(SortiereNode(item))
            Next
            Return neuArr
        End If
        Return JsonNode.Parse(node.ToJsonString())
    End Function

    ' ---------------------------------------------------------------
    ' constraints.yaml
    ' ---------------------------------------------------------------

    <DataTestMethod>
    <DataRow("bw-grundschule-beispiel")>
    <DataRow("bw-gms-beispiel")>
    Public Sub ConstraintsRoundTripPreservesEveryRule(schule As String)
        Dim pfad = InputPfad(schule, "constraints.yaml")
        Dim original = YamlConstraints.LoadConstraintsYaml(pfad)
        Assert.IsTrue(original.Count > 0, $"{schule}: keine Constraints geladen - Testgrundlage fehlt")

        Dim yaml = YamlConstraints.SerializeConstraintsYaml(original)
        Dim temp = IO.Path.Combine(IO.Path.GetTempPath(), $"ttyaml-{schule}-{original.Count}.yaml")
        Try
            IO.File.WriteAllText(temp, yaml)
            Dim erneut = YamlConstraints.LoadConstraintsYaml(temp)

            Assert.AreEqual(original.Count, erneut.Count, $"{schule}: Regelanzahl veraendert")
            For i = 0 To original.Count - 1
                Assert.AreEqual(Kanonisch(original(i)), Kanonisch(erneut(i)),
                                $"{schule}: Regel {i} unterscheidet sich nach dem Round-Trip")
            Next
        Finally
            If IO.File.Exists(temp) Then IO.File.Delete(temp)
        End Try
    End Sub

    ''' <summary>Zahlenfelder muessen Zahlen bleiben. Kaemen sie als Strings
    ''' zurueck, wuerfe JsonHelpers.GetInt spaeter im Solver - genau der
    ''' Fehler, den ScalarStringToJsonValue auf der Leseseite abfaengt.</summary>
    <TestMethod>
    Public Sub ConstraintsRoundTripKeepsNumbersNumeric()
        Dim regeln As New List(Of JsonObject) From {
            New JsonObject From {{"type", "forbidden_slot"}, {"scope", "class"}, {"entity", "1a"},
                                 {"day", "Mo"}, {"period", 7}, {"priority", "must"},
                                 {"reason", "1. Klasse: nur Vormittagsunterricht"}},
            New JsonObject From {{"type", "teacher_availability"}, {"teacher", "T1"},
                                 {"available_days", New JsonArray("Mo", "Di")}}
        }

        Dim yaml = YamlConstraints.SerializeConstraintsYaml(regeln)
        Dim temp = IO.Path.Combine(IO.Path.GetTempPath(), "ttyaml-zahlen.yaml")
        Try
            IO.File.WriteAllText(temp, yaml)
            Dim erneut = YamlConstraints.LoadConstraintsYaml(temp)

            Assert.AreEqual(2, erneut.Count)
            Assert.AreEqual(JsonValueKind.Number, erneut(0)("period").GetValueKind(),
                            "period ist nach dem Round-Trip keine Zahl mehr")
            Assert.AreEqual(7, erneut(0)("period").GetValue(Of Integer)())
            Assert.AreEqual("must", erneut(0)("priority").GetValue(Of String)())
            Assert.AreEqual(2, erneut(1)("available_days").AsArray().Count)
        Finally
            If IO.File.Exists(temp) Then IO.File.Delete(temp)
        End Try
    End Sub

    ' ---------------------------------------------------------------
    ' klassenbildung.yaml
    ' ---------------------------------------------------------------

    ''' <summary>`min_pro_klasse` (Buendelung in Gruppchen) faehrt ueber
    ''' die Underscore-Konvention ohne eigenen Code - und bleibt weg,
    ''' wenn es nicht gesetzt ist (OmitNull).</summary>
    <TestMethod>
    Public Sub MinProKlasseUeberlebtDenRoundTripUndBleibtSonstWeg()
        Dim input = YamlKlassenbildung.DeserializeKlassenbildungYaml(
            "klassen: {anzahl: 2, min_groesse: 1, max_groesse: 9}" & vbLf &
            "schueler: [{id: S1}, {id: S2}, {id: S3}]" & vbLf &
            "gruppen:" & vbLf &
            "  - {id: G_a, typ: buendelung, mitglieder: [S1, S2, S3], min_pro_klasse: 2}" & vbLf &
            "  - {id: G_b, typ: buendelung, mitglieder: [S1, S2]}" & vbLf)
        Assert.AreEqual(2, input.Gruppen(0).MinProKlasse)
        Assert.IsFalse(input.Gruppen(1).MinProKlasse.HasValue)

        Dim yaml = YamlKlassenbildung.SerializeKlassenbildungYaml(input)
        StringAssert.Contains(yaml, "min_pro_klasse: 2")
        Assert.AreEqual(1, yaml.Split({"min_pro_klasse"}, StringSplitOptions.None).Length - 1,
                        "eine nicht gesetzte Mindestzahl darf nicht als null erscheinen")
        Dim erneut = YamlKlassenbildung.DeserializeKlassenbildungYaml(yaml)
        Assert.AreEqual(2, erneut.Gruppen(0).MinProKlasse)
        Assert.IsFalse(erneut.Gruppen(1).MinProKlasse.HasValue)
    End Sub

    <TestMethod>
    Public Sub KlassenbildungRoundTripPreservesInput()
        Dim pfad = InputPfad("bw-grundschule-beispiel", "klassenbildung.yaml")
        Dim original = YamlKlassenbildung.LoadKlassenbildungYaml(pfad)
        Assert.IsTrue(original.Schueler.Count > 0, "keine Schueler geladen - Testgrundlage fehlt")

        Dim erneut = YamlKlassenbildung.DeserializeKlassenbildungYaml(
            YamlKlassenbildung.SerializeKlassenbildungYaml(original))

        Assert.AreEqual(original.Schueler.Count, erneut.Schueler.Count, "Schuelerzahl")
        Assert.AreEqual(original.Klassen.Anzahl, erneut.Klassen.Anzahl, "Klassenanzahl")
        Assert.AreEqual(original.Klassen.MinGroesse, erneut.Klassen.MinGroesse, "MinGroesse")
        Assert.AreEqual(original.Klassen.MaxGroesse, erneut.Klassen.MaxGroesse, "MaxGroesse")
        Assert.AreEqual(original.Klassen.Stufe, erneut.Klassen.Stufe, "Stufe (Nullable!)")
        Assert.AreEqual(original.Gruppen.Count, erneut.Gruppen.Count, "Gruppenzahl")
        Assert.AreEqual(original.Balance.Count, erneut.Balance.Count, "Balance-Kriterien")
        Assert.AreEqual(original.Wuensche.Count, erneut.Wuensche.Count, "Wuensche")
        Assert.AreEqual(original.Fixierungen.Count, erneut.Fixierungen.Count, "Fixierungen")

        ' Stichproben in die Tiefe - Zaehlerstaende allein wuerden einen
        ' Schreiber durchgehen lassen, der die Inhalte verliert.
        For i = 0 To original.Gruppen.Count - 1
            Assert.AreEqual(original.Gruppen(i).Id, erneut.Gruppen(i).Id, $"Gruppe {i} Id")
            Assert.AreEqual(original.Gruppen(i).Typ, erneut.Gruppen(i).Typ, $"Gruppe {i} Typ")
            Assert.AreEqual(original.Gruppen(i).Modus, erneut.Gruppen(i).Modus, $"Gruppe {i} Modus")
            Assert.AreEqual(original.Gruppen(i).Prio, erneut.Gruppen(i).Prio, $"Gruppe {i} Prio")
            Assert.AreEqual(original.Gruppen(i).MaxProKlasse, erneut.Gruppen(i).MaxProKlasse,
                            $"Gruppe {i} MaxProKlasse (Nullable!)")
            Assert.AreEqual(original.Gruppen(i).MinProKlasse, erneut.Gruppen(i).MinProKlasse,
                            $"Gruppe {i} MinProKlasse (Nullable!)")
            CollectionAssert.AreEqual(original.Gruppen(i).Mitglieder, erneut.Gruppen(i).Mitglieder,
                                      $"Gruppe {i} Mitglieder")
        Next
        For i = 0 To original.Fixierungen.Count - 1
            Assert.AreEqual(original.Fixierungen(i).Kind, erneut.Fixierungen(i).Kind, $"Fixierung {i} Kind")
            Assert.AreEqual(original.Fixierungen(i).Klasse, erneut.Fixierungen(i).Klasse, $"Fixierung {i} Klasse")
            Assert.AreEqual(original.Fixierungen(i).NichtKlasse, erneut.Fixierungen(i).NichtKlasse,
                            $"Fixierung {i} NichtKlasse")
        Next
        For i = 0 To original.Schueler.Count - 1
            Assert.AreEqual(original.Schueler(i).Id, erneut.Schueler(i).Id, $"Schueler {i} Id")
            CollectionAssert.AreEquivalent(original.Schueler(i).Attribute.ToList(),
                                           erneut.Schueler(i).Attribute.ToList(),
                                           $"Schueler {i} Attribute")
        Next
    End Sub

    ' ---------------------------------------------------------------
    ' config.yaml
    ' ---------------------------------------------------------------

    <DataTestMethod>
    <DataRow("bw-grundschule-beispiel")>
    <DataRow("bw-gms-beispiel")>
    Public Sub ConfigRoundTripPreservesEveryField(schule As String)
        Dim pfad = InputPfad(schule, "config.yaml")
        Dim original = YamlConfig.LoadConfig(pfad)

        Dim temp = IO.Path.Combine(IO.Path.GetTempPath(), $"ttconfig-{schule}.yaml")
        Try
            YamlConfig.SaveConfig(original, temp)
            Dim erneut = YamlConfig.LoadConfig(temp)

            ' Vergleich ueber Reflection: so kann kein spaeter ergaenztes
            ' Feld stillschweigend aus dem Test herausfallen.
            For Each p In GetType(RunConfig).GetProperties()
                If p.Name = NameOf(RunConfig.QualityWeights) OrElse p.Name = NameOf(RunConfig.Klassenbildung) Then Continue For
                Assert.AreEqual(p.GetValue(original), p.GetValue(erneut), $"{schule}: RunConfig.{p.Name}")
            Next

            If original.QualityWeights IsNot Nothing Then
                Assert.IsNotNull(erneut.QualityWeights, $"{schule}: quality_weights-Block verloren")
                For Each p In GetType(QualityWeightsConfig).GetProperties()
                    Assert.AreEqual(p.GetValue(original.QualityWeights), p.GetValue(erneut.QualityWeights),
                                    $"{schule}: QualityWeights.{p.Name}")
                Next
            End If

            If original.Klassenbildung IsNot Nothing Then
                Assert.IsNotNull(erneut.Klassenbildung, $"{schule}: klassenbildung-Block verloren")
                Assert.AreEqual(original.Klassenbildung.ZeitlimitS, erneut.Klassenbildung.ZeitlimitS)
                Assert.AreEqual(original.Klassenbildung.NVarianten, erneut.Klassenbildung.NVarianten)
                Assert.AreEqual(original.Klassenbildung.Epsilon, erneut.Klassenbildung.Epsilon)
                Assert.AreEqual(original.Klassenbildung.MinDistanz, erneut.Klassenbildung.MinDistanz)
                Assert.AreEqual(original.Klassenbildung.Symmetriebrechung, erneut.Klassenbildung.Symmetriebrechung)
            End If
        Finally
            If IO.File.Exists(temp) Then IO.File.Delete(temp)
        End Try
    End Sub

    ''' <summary>`Nothing` heisst "nicht gesetzt". Wuerde der Serializer es
    ''' als `null` ausschreiben, behauptete die Datei faelschlich, dort sei
    ''' eine Entscheidung getroffen worden - deshalb OmitNull.</summary>
    <TestMethod>
    Public Sub ConfigSerializerOmitsUnsetNullableFields()
        Dim cfg As New RunConfig With {.SolveTimeLimitS = 120.0, .MaxSolutions = 5}

        Dim yaml = YamlConfig.SerializeConfig(cfg)

        StringAssert.Contains(yaml, "solve_time_limit_s: 120")
        StringAssert.Contains(yaml, "max_solutions: 5")
        Assert.IsFalse(yaml.Contains("per_solve_time_limit_s"),
                       "nicht gesetztes Nullable-Feld wurde trotzdem geschrieben:" & vbLf & yaml)
        Assert.IsFalse(yaml.Contains("quality_weights"),
                       "leerer quality_weights-Block wurde geschrieben:" & vbLf & yaml)
        Assert.IsFalse(yaml.Contains("null"), "OmitNull greift nicht:" & vbLf & yaml)
    End Sub

    ''' <summary>Die Defaults duerfen ein exportiertes config.yaml nicht
    ''' anders interpretieren lassen als die Vorlage: fehlende Datei und
    ''' leere Datei liefern beide reine Defaults.</summary>
    <TestMethod>
    Public Sub LoadConfigFallsBackToDefaults()
        Dim fehlend = YamlConfig.LoadConfig(IO.Path.Combine(IO.Path.GetTempPath(), "gibt-es-nicht-" & Guid.NewGuid().ToString("N") & ".yaml"))
        Assert.AreEqual(30.0, fehlend.SolveTimeLimitS)
        Assert.AreEqual(42, fehlend.Seed)
        Assert.IsFalse(fehlend.PerSolveTimeLimitS.HasValue)

        Dim leer = IO.Path.Combine(IO.Path.GetTempPath(), "leer-" & Guid.NewGuid().ToString("N") & ".yaml")
        Try
            IO.File.WriteAllText(leer, "   " & vbLf)
            Dim ausLeer = YamlConfig.LoadConfig(leer)
            Assert.AreEqual(30.0, ausLeer.SolveTimeLimitS)
            Assert.AreEqual(1, ausLeer.MaxSolutions)
        Finally
            If IO.File.Exists(leer) Then IO.File.Delete(leer)
        End Try
    End Sub

    ''' <summary>BuildQualityWeights war bis Stufe A Private im Runner. Es
    ''' ist das Overlay-Muster, das der GUI-Einstellungsdialog spiegelt -
    ''' nur gesetzte Felder ueberschreiben die Kern-Defaults.</summary>
    <TestMethod>
    Public Sub BuildQualityWeightsOverlaysOnlySetFields()
        Dim defaults = YamlConfig.BuildQualityWeights(Nothing)
        Assert.AreEqual(New QualityWeights().Kann, defaults.Kann)
        Assert.IsTrue(defaults.IncludeTeacherGaps)

        Dim teilweise = YamlConfig.BuildQualityWeights(
            New QualityWeightsConfig With {.ClassGaps = 4321.0, .IncludeEdgePeriod = False})
        Assert.AreEqual(4321.0, teilweise.ClassGaps, "gesetztes Feld nicht uebernommen")
        Assert.IsFalse(teilweise.IncludeEdgePeriod, "gesetztes Include-Flag nicht uebernommen")
        Assert.AreEqual(New QualityWeights().Kann, teilweise.Kann, "nicht gesetztes Feld wurde veraendert")
        Assert.AreEqual(New QualityWeights().TeacherGaps, teilweise.TeacherGaps, "nicht gesetztes Feld wurde veraendert")
    End Sub

End Class
