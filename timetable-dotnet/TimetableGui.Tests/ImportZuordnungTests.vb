' Freie Spalten-Zuordnung und Klarnamen-Export (gui-ui-konzept 9.1) -
' Stufe G5.
'
' Der Import ist die Stelle, an der fremde Daten ins Projekt kommen.
' Zwei Eigenschaften traegt er deshalb, und beide sind hier festgehalten:
' die Vorgabe ist VERWERFEN, und was verworfen wurde, wird GENANNT.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableCore
Imports TimetableGui
Imports TimetableProjekt

<TestClass>
Public Class ImportZuordnungTests

    Private Shared ReadOnly Jetzt As New DateTimeOffset(2026, 8, 26, 16, 0, 0, TimeSpan.Zero)
    Private _ordner As String

    <TestInitialize>
    Public Sub Aufbauen()
        _ordner = IO.Path.Combine(IO.Path.GetTempPath(), "ttim-" & Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(_ordner)
    End Sub

    <TestCleanup>
    Public Sub Abraeumen()
        If IO.Directory.Exists(_ordner) Then IO.Directory.Delete(_ordner, recursive:=True)
    End Sub

    ''' <summary>Ein offenes Projekt ueber den ECHTEN Weg: der Assistent
    ''' liefert den Entwurf, das HauptViewModel uebernimmt und speichert.
    ''' Der Setzer von Projekt ist bewusst privat - ihn fuer Tests zu
    ''' oeffnen hiesse, die Kapselung der Bequemlichkeit zu opfern.</summary>
    Private Function Huelle(d As TestDialoge) As HauptViewModel
        d.SpeichernPfad = IO.Path.Combine(_ordner, "p.splanx")
        Dim m As New HauptViewModel(d, Function() Jetzt)
        m.Neu()
        Return m
    End Function

    Private Shared Function Modell(p As Projekt) As KlassenbildungEingabeViewModel
        Return New KlassenbildungEingabeViewModel(p, New TestDialoge())
    End Function

    ' ===============================================================
    ' Der Vorschlag
    ' ===============================================================

    ''' <summary>Der Vorschlag ist bewusst ZURUECKHALTEND. Ein Vorschlag,
    ''' der freimuetig uebernimmt, hebelt die Datenminimierung aus, ohne
    ''' dass es jemand merkt - eine Klassenliste aus dem Sekretariat
    ''' traegt Telefonnummern und Geburtsdaten.</summary>
    <TestMethod>
    Public Sub DerVorschlagErkenntNurNamenUndKlasse()
        Dim w = Spaltenzuordnung.Vorschlag({"Nachname", "Vorname", "Klasse", "Telefon", "Geburtsdatum"})

        Assert.AreEqual(Spaltenrolle.Nachname, w(0).Rolle)
        Assert.AreEqual(Spaltenrolle.Vorname, w(1).Rolle)
        Assert.AreEqual(Spaltenrolle.Klasse, w(2).Rolle)
        Assert.AreEqual(Spaltenrolle.Verwerfen, w(3).Rolle, "Telefonnummern gehören nicht ins Projekt")
        Assert.AreEqual(Spaltenrolle.Verwerfen, w(4).Rolle)
    End Sub

    <TestMethod>
    Public Sub JedeRolleWirdHoechstensEinmalVorgeschlagen()
        Dim w = Spaltenzuordnung.Vorschlag({"Name", "Nachname", "Klasse", "Klasse"})
        Assert.AreEqual(1, w.Where(Function(x) x.Rolle = Spaltenrolle.Nachname).Count)
        Assert.AreEqual(1, w.Where(Function(x) x.Rolle = Spaltenrolle.Klasse).Count)
    End Sub

    ' ===============================================================
    ' Die Einwaende
    ' ===============================================================

    <TestMethod>
    Public Sub OhneNamenGibtEsEinenEinwand()
        Dim w = New List(Of Spaltenwahl) From {
            New Spaltenwahl With {.Name = "A", .Rolle = Spaltenrolle.Attribut}}
        Assert.IsTrue(Spaltenzuordnung.Einwaende(w).Any(Function(f) f.Contains("namenlos")))
    End Sub

    <TestMethod>
    Public Sub EinmaligeRollenDuerfenNichtDoppeltVergebenWerden()
        Dim w = New List(Of Spaltenwahl) From {
            New Spaltenwahl With {.Name = "A", .Rolle = Spaltenrolle.Nachname},
            New Spaltenwahl With {.Name = "B", .Rolle = Spaltenrolle.Nachname}}
        Assert.IsTrue(Spaltenzuordnung.Einwaende(w).Any(Function(f) f.Contains("nur einmal")))
    End Sub

    ''' <summary>Zwei gleichnamige Attributspalten ergaeben ein Attribut,
    ''' das mal den einen und mal den anderen Wert traegt.</summary>
    <TestMethod>
    Public Sub GleichnamigeAttributspaltenWerdenAbgelehnt()
        Dim w = New List(Of Spaltenwahl) From {
            New Spaltenwahl With {.Name = "Nachname", .Rolle = Spaltenrolle.Nachname},
            New Spaltenwahl With {.Name = "Betreuung", .Rolle = Spaltenrolle.Attribut},
            New Spaltenwahl With {.Name = "betreuung", .Rolle = Spaltenrolle.Attribut}}
        Assert.IsTrue(Spaltenzuordnung.Einwaende(w).Any(Function(f) f.Contains("eindeutig")))
    End Sub

    ' ===============================================================
    ' Der Import
    ' ===============================================================

    Private Shared Function Beispiel() As String
        Return "Nachname;Vorname;Religion;Betreuung;Telefon" & vbLf &
               "Meier;Mia;ev;ja;0711-1" & vbLf &
               "Schulz;Tom;kath;nein;0711-2" & vbLf &
               "Braun;Ida;ev;ja;0711-3" & vbLf &
               "Klein;Nils;ethik;nein;0711-4"
    End Function

    ''' <summary>Der Durchstich: Namen nach mapping.json, ein Attribut,
    ''' Gruppen aus einer Spalte - und die Telefonnummern bleiben
    ''' draussen.</summary>
    <TestMethod>
    Public Sub ImportLegtKinderAttributeUndGruppenAn()
        Dim p As New Projekt()
        Dim m = Modell(p)
        Dim v = m.ImportPruefen(Beispiel())
        Dim w = Spaltenzuordnung.Vorschlag(v.Spalten)
        w(2).Rolle = Spaltenrolle.Gruppe
        w(3).Rolle = Spaltenrolle.Attribut
        ' w(4) = Telefon bleibt auf Verwerfen.

        Dim bericht = m.ImportUebernehmen(v, w)

        Assert.AreEqual(4, bericht.Kinder)
        Assert.AreEqual(4, p.Klassenbildung.Schueler.Count)
        Assert.AreEqual(4, p.Mapping.Count, "die Klarnamen gehen nach mapping.json")

        ' Das Attribut ist da, die Telefonnummer nicht.
        Dim erstes = p.Klassenbildung.Schueler(0)
        Assert.AreEqual("ja", erstes.Attribute("Betreuung"))
        Assert.IsFalse(erstes.Attribute.ContainsKey("Telefon"),
                       "eine verworfene Spalte darf nirgends landen")
        Assert.IsFalse(erstes.Attribute.ContainsKey("Religion"),
                       "eine Gruppenspalte wird Gruppe, nicht zusaetzlich Attribut")

        ' Drei Gruppen, jedes Kind genau einmal.
        Assert.AreEqual(3, p.Klassenbildung.Gruppen.Count)
        CollectionAssert.AreEquivalent(
            New List(Of String) From {"Religion-ethik", "Religion-ev", "Religion-kath"},
            p.Klassenbildung.Gruppen.Select(Function(g) g.Id).ToList())
        Dim evGruppe = p.Klassenbildung.Gruppen.Single(Function(g) g.Id = "Religion-ev")
        Assert.AreEqual(2, evGruppe.Mitglieder.Count)
        Assert.AreEqual("verteilung", evGruppe.Typ, "die Vorgabe verteilt, statt zu buendeln")
    End Sub

    ''' <summary>"Nicht zugeordnete Spalten werden verworfen (mit
    ''' Hinweis)". Der Hinweis ist der Punkt: still zu verwerfen sieht
    ''' aus wie ein vollstaendiger Import.</summary>
    <TestMethod>
    Public Sub DerBerichtNenntWasVerworfenWurde()
        Dim p As New Projekt()
        Dim m = Modell(p)
        Dim v = m.ImportPruefen(Beispiel())
        Dim bericht = m.ImportUebernehmen(v, Spaltenzuordnung.Vorschlag(v.Spalten))

        CollectionAssert.AreEquivalent(New List(Of String) From {"Religion", "Betreuung", "Telefon"},
                                       bericht.Verworfen)
        StringAssert.Contains(bericht.Klartext(), "Nicht übernommen")
        StringAssert.Contains(bericht.Klartext(), "Telefon")
    End Sub

    ''' <summary>Eine bestehende Einteilung wird zur Fixierung (9.3) -
    ''' der Weg, ueber den eine unterjaehrig eingefuehrte Schule ihren
    ''' Ist-Zustand einbringt.</summary>
    <TestMethod>
    Public Sub EineKlassenspalteWirdZurFixierung()
        Dim p As New Projekt()
        Dim m = Modell(p)
        Dim v = m.ImportPruefen("Nachname;Klasse" & vbLf & "Meier;1" & vbLf & "Schulz;2" & vbLf & "Braun;1")
        Dim bericht = m.ImportUebernehmen(v, Spaltenzuordnung.Vorschlag(v.Spalten))

        Assert.AreEqual(3, bericht.Fixierungen)
        Assert.AreEqual(3, p.Klassenbildung.Fixierungen.Count)
        Assert.AreEqual(2, p.Klassenbildung.Fixierungen.Where(Function(f) f.Klasse = 1).Count)
    End Sub

    ''' <summary>"1a" statt "1" ist der haeufigste Fall. Ihn still zu
    ''' schlucken laesst den Nutzer spaeter nach fehlenden Fixierungen
    ''' suchen.</summary>
    <TestMethod>
    Public Sub EineUnlesbareKlassenangabeWirdGemeldet()
        Dim p As New Projekt()
        Dim m = Modell(p)
        Dim v = m.ImportPruefen("Nachname;Klasse" & vbLf & "Meier;1a" & vbLf & "Schulz;2")
        Dim bericht = m.ImportUebernehmen(v, Spaltenzuordnung.Vorschlag(v.Spalten))

        Assert.AreEqual(2, bericht.Kinder)
        Assert.AreEqual(1, bericht.Fixierungen)
        Assert.IsTrue(bericht.Hinweise.Any(Function(h) h.Contains("weder eine Zahl")))
    End Sub

    ' ===============================================================
    ' Dateien lesen
    ' ===============================================================

    ''' <summary>Eine als Windows-1252 gespeicherte Datei aus Excel darf
    ''' keine kaputten Umlaute ergeben - der Name landet so in
    ''' mapping.json.</summary>
    <TestMethod>
    Public Sub DateienWerdenMitUndOhneBomRichtigGelesen()
        Dim inhalt = "Nachname;Vorname" & vbLf & "Müller;Jörg"

        Dim mitBom = IO.Path.Combine(_ordner, "bom.csv")
        IO.File.WriteAllText(mitBom, inhalt, New Text.UTF8Encoding(True))
        StringAssert.Contains(ImportDialog.DateiLesen(mitBom), "Müller")

        Dim ohneBom = IO.Path.Combine(_ordner, "utf8.csv")
        IO.File.WriteAllText(ohneBom, inhalt, New Text.UTF8Encoding(False))
        StringAssert.Contains(ImportDialog.DateiLesen(ohneBom), "Jörg")

        Dim ansi = IO.Path.Combine(_ordner, "ansi.csv")
        IO.File.WriteAllBytes(ansi, Text.Encoding.Latin1.GetBytes(inhalt))
        StringAssert.Contains(ImportDialog.DateiLesen(ansi), "Müller")
    End Sub

    ' ===============================================================
    ' Klarnamen-Export
    ' ===============================================================

    ''' <summary>Ein Name wie "Meier; Anna" zerlegte die Datei sonst an
    ''' der falschen Stelle - derselbe Fall, der beim LESEN schon einmal
    ''' zugeschlagen hat.</summary>
    <TestMethod>
    Public Sub DerExportFasstFelderMitTrennzeichenEin()
        Dim p As New Projekt()
        p.Mapping.Add(New MappingEintrag With {.Id = "S001", .Nachname = "Meier; Anna", .Vorname = "Mia"})
        p.Mapping.Add(New MappingEintrag With {.Id = "S002", .Nachname = "O""Brien", .Vorname = "Tom"})

        Dim zeilen = Klarnamenexport.Zeilen(p)

        Assert.AreEqual("id;nachname;vorname;klasse", zeilen(0))
        StringAssert.Contains(zeilen(1), """Meier; Anna""")
        StringAssert.Contains(zeilen(2), """O""""Brien""")
    End Sub

    <TestMethod>
    Public Sub DerExportTraegtDieKlasseNurAusEinerFixierung()
        Dim p As New Projekt()
        p.Mapping.Add(New MappingEintrag With {.Id = "S001", .Nachname = "Meier", .Vorname = "Mia"})
        p.Mapping.Add(New MappingEintrag With {.Id = "S002", .Nachname = "Schulz", .Vorname = "Tom"})
        p.Klassenbildung.Fixierungen.Add(New KlassenbildungFixierung With {.Kind = "S001", .Klasse = 3})

        Dim zeilen = Klarnamenexport.Zeilen(p)
        Assert.AreEqual("S001;Meier;Mia;3", zeilen(1))
        Assert.AreEqual("S002;Schulz;Tom;", zeilen(2), "ohne Fixierung bleibt die Klasse leer")
    End Sub

    ''' <summary>Der Warntext NENNT die Zahl. Eine Warnung, die man nicht
    ''' nachrechnen kann, wird weggeklickt.</summary>
    <TestMethod>
    Public Sub DieWarnungNenntDieZahlUndDenGrund()
        Dim w = Klarnamenexport.Warnung(42)
        StringAssert.Contains(w, "42")
        StringAssert.Contains(w, "unverschlüsselt")
        StringAssert.Contains(w, "protokolliert")
    End Sub

    ''' <summary>Zwei Bedingungen, beide vom Plan gefordert: Warndialog
    ''' UND Audit-Eintrag. Ein Nein im Dialog schreibt nichts.</summary>
    <TestMethod>
    Public Sub OhneBestaetigungWirdNichtsGeschrieben()
        Dim pfad = IO.Path.Combine(_ordner, "namen.csv")
        Dim d As New TestDialoge With {.FrageAntwort = False, .DateiSpeichernPfad = pfad}
        Dim m = Huelle(d)
        Dim p = m.Projekt
        p.Mapping.Add(New MappingEintrag With {.Id = "S001", .Nachname = "Meier", .Vorname = "Mia"})

        m.KlarnamenExportieren()

        Assert.IsFalse(IO.File.Exists(pfad))
        Assert.AreEqual(0, p.AuditLog.Where(Function(e) e.Aktion = "export").Count)
    End Sub

    <TestMethod>
    Public Sub MitBestaetigungEntstehtDateiUndProtokollzeile()
        Dim pfad = IO.Path.Combine(_ordner, "namen.csv")
        Dim d As New TestDialoge With {.FrageAntwort = True, .DateiSpeichernPfad = pfad}
        Dim m = Huelle(d)
        Dim p = m.Projekt
        p.Mapping.Add(New MappingEintrag With {.Id = "S001", .Nachname = "Meier", .Vorname = "Mia"})
        p.Mapping.Add(New MappingEintrag With {.Id = "S001", .Nachname = "Meier; Anna", .Vorname = "Mia"})
        p.Mapping.Add(New MappingEintrag With {.Id = "S001", .Nachname = "Meier", .Vorname = "Mia"})

        m.KlarnamenExportieren()

        Assert.IsTrue(IO.File.Exists(pfad))
        StringAssert.Contains(IO.File.ReadAllText(pfad), "Meier")
        Dim protokoll = p.AuditLog.Where(Function(e) e.Aktion = "export").ToList()
        Assert.AreEqual(1, protokoll.Count, "ohne Protokollzeile ist der Vorgang nicht nachvollziehbar")
        StringAssert.Contains(protokoll(0).Beschreibung, "Klarnamen-Export")
    End Sub

    <TestMethod>
    Public Sub OhneKlarnamenGibtEsNichtsZuExportieren()
        Dim d As New TestDialoge With {.FrageAntwort = True}
        Dim m = Huelle(d)

        m.KlarnamenExportieren()

        Assert.AreEqual(0, d.Fragen.Count, "ohne Namen darf gar nicht erst gefragt werden")
        Assert.IsTrue(d.Hinweise.Any(Function(h) h.Contains("Platzhalter")))
    End Sub


    ''' <summary>Echte Klassenlisten schreiben nicht "1", sondern "5a".
    ''' Ohne den Abgleich gegen die Labels haette die Oberflaeche vom
    ''' Nutzer verlangt, seine Datei vorher umzuschreiben - genau das
    ''' soll sie ihm ersparen.</summary>
    <TestMethod>
    Public Sub EineKlassenspalteErkenntAuchDieLabels()
        Dim p As New Projekt()
        p.Klassenbildung.Klassen.Labels = New List(Of String) From {"5a", "5b", "5c", "5d"}
        Dim m = Modell(p)
        Dim v = m.ImportPruefen("Nachname;Klasse" & vbLf & "Meier;5a" & vbLf & "Schulz;5C" & vbLf & "Braun;2")
        Dim bericht = m.ImportUebernehmen(v, Spaltenzuordnung.Vorschlag(v.Spalten))

        Assert.AreEqual(3, bericht.Fixierungen)
        Dim fix = p.Klassenbildung.Fixierungen
        Assert.AreEqual(1, fix(0).Klasse.Value, "5a ist die erste Klasse")
        Assert.AreEqual(3, fix(1).Klasse.Value, "die Schreibweise darf nicht zaehlen")
        Assert.AreEqual(2, fix(2).Klasse.Value, "eine Zahl gilt weiterhin unmittelbar")
    End Sub

    <TestMethod>
    Public Sub EineUnbekannteKlassenangabeBleibtEinHinweis()
        Dim p As New Projekt()
        p.Klassenbildung.Klassen.Labels = New List(Of String) From {"5a", "5b"}
        Dim m = Modell(p)
        Dim v = m.ImportPruefen("Nachname;Klasse" & vbLf & "Meier;9z")
        Dim bericht = m.ImportUebernehmen(v, Spaltenzuordnung.Vorschlag(v.Spalten))

        Assert.AreEqual(0, bericht.Fixierungen)
        Assert.IsTrue(bericht.Hinweise.Any(Function(h) h.Contains("Klassen-Labels")))
    End Sub


    ' ===============================================================
    ' Die mitgelieferten Beispieldateien
    ' ===============================================================

    Private Shared Function Beispieldatei(schule As String, name As String) As String
        Return IO.Path.Combine(TestsWurzel(), schule, "import-beispiel", name)
    End Function

    ''' <summary>Eine Beispieldatei, die nie durch den Importer laeuft,
    ''' ist eine Behauptung. Diese beiden Tests fahren sie wirklich
    ''' hinein - inklusive BOM-Erkennung und Trennzeichen.</summary>
    <TestMethod>
    Public Sub DieEinschulungslisteDerGrundschuleLaeuftDurch()
        Dim p As New Projekt()
        Dim m = Modell(p)
        Dim text = ImportDialog.DateiLesen(
            Beispieldatei("bw-grundschule-beispiel", "einschulungsliste.csv"))
        Dim v = m.ImportPruefen(text)

        Assert.IsTrue(v.Kopfzeile, "die Kopfzeile muss erkannt werden")
        Assert.AreEqual(100, v.Datensaetze)
        CollectionAssert.Contains(v.Spalten, "Sprachfoerderung")

        ' So, wie die Anleitung es beschreibt: Attribute setzen, Kita als
        ' Gruppe, Telefon und Geburtsdatum verwerfen.
        Dim w = Spaltenzuordnung.Vorschlag(v.Spalten)
        For Each name In {"Geschlecht", "Sprachfoerderung", "Kann-Kind", "Wohngebiet"}
            w.Single(Function(x) x.Name = name).Rolle = Spaltenrolle.Attribut
        Next
        w.Single(Function(x) x.Name = "Kita").Rolle = Spaltenrolle.Gruppe
        Assert.AreEqual(0, Spaltenzuordnung.Einwaende(w).Count)

        Dim bericht = m.ImportUebernehmen(v, w)

        Assert.AreEqual(100, bericht.Kinder)
        Assert.AreEqual(100, p.Mapping.Count)
        Assert.AreEqual(6, bericht.Gruppen.Count, "sechs Kitas")
        CollectionAssert.AreEquivalent(New List(Of String) From {"Telefon", "Geburtsdatum"},
                                       bericht.Verworfen)
        ' Die Telefonnummern sind wirklich nirgends gelandet.
        Assert.IsFalse(p.Klassenbildung.Schueler.Any(
            Function(s) s.Attribute.Keys.Any(Function(k) k.Contains("Telefon"))))
        CollectionAssert.AreEquivalent(
            New List(Of String) From {"Geschlecht", "Sprachfoerderung", "Kann-Kind", "Wohngebiet"},
            m.Attributnamen)
    End Sub

    ''' <summary>Die GMS-Datei fuehrt den Fall aus 9.3 vor: eine
    ''' bestehende Einteilung mit den echten Klassennamen 5a..5d.</summary>
    <TestMethod>
    Public Sub DieBestehendeEinteilungDerGmsWirdZuFixierungen()
        Dim p As New Projekt()
        p.Klassenbildung.Klassen.Labels = New List(Of String) From {"5a", "5b", "5c", "5d"}
        Dim m = Modell(p)
        Dim text = ImportDialog.DateiLesen(
            Beispieldatei("bw-gms-beispiel", "bestehende-einteilung.csv"))
        Dim v = m.ImportPruefen(text)

        Assert.AreEqual(116, v.Datensaetze)

        Dim w = Spaltenzuordnung.Vorschlag(v.Spalten)
        Assert.AreEqual(Spaltenrolle.Klasse, w.Single(Function(x) x.Name = "Klasse").Rolle,
                        "eine Spalte namens Klasse soll vorgeschlagen werden")
        w.Single(Function(x) x.Name = "Religion").Rolle = Spaltenrolle.Gruppe
        w.Single(Function(x) x.Name = "Niveau").Rolle = Spaltenrolle.Attribut

        Dim bericht = m.ImportUebernehmen(v, w)

        Assert.AreEqual(116, bericht.Kinder)
        Assert.AreEqual(116, bericht.Fixierungen, "5a..5d muessen ueber die Labels aufgeloest werden")
        Assert.AreEqual(0, bericht.Hinweise.Count, String.Join(" | ", bericht.Hinweise))
        Assert.AreEqual(3, bericht.Gruppen.Count, "ev, kath, ethik")
        CollectionAssert.AreEquivalent(New List(Of String) From {"Mailadresse"}, bericht.Verworfen)
        ' 116 Kinder auf vier Klassen, gleichmaessig verteilt.
        For k = 1 To 4
            Dim n = k
            Assert.AreEqual(29, p.Klassenbildung.Fixierungen.Where(Function(f) f.Klasse = n).Count)
        Next
    End Sub


    ' ===============================================================
    ' Das Ziel eines Attributs (Nutzerbefund 01.09.2026)
    ' ===============================================================

    ''' <summary>Der Befund aus dem manuellen Test: in der Rollenliste
    ''' stand "Attribut", aber nicht, WIE das Attribut heissen wird - man
    ''' sucht dort dann nach dem eigenen Spaltennamen. Der Schluessel ist
    ''' jetzt ein eigener, sichtbarer Wert.</summary>
    <TestMethod>
    Public Sub OhneZielnameGiltDerSpaltenname()
        Dim w As New Spaltenwahl With {.Name = "Geschlecht", .Rolle = Spaltenrolle.Attribut}
        Assert.AreEqual("Geschlecht", w.Schluessel)

        w.Zielname = "  geschlecht  "
        Assert.AreEqual("geschlecht", w.Schluessel, "Leerraum gehoert nicht in einen Schluessel")
    End Sub

    ''' <summary>Der eigentliche Fehler dahinter: die Beispiel-CSV
    ''' schreibt `Geschlecht`, die mitgelieferte Klassenbildung
    ''' `geschlecht`. Ohne Zielname entstuende ein ZWEITES Attribut, und
    ''' eine Balance-Regel auf dem alten griffe bei den importierten
    ''' Kindern nicht mehr - ohne jeden Hinweis.</summary>
    <TestMethod>
    Public Sub EinZielDasSichNurInDerSchreibweiseUnterscheidetFaelltAuf()
        Dim w = New List(Of Spaltenwahl) From {
            New Spaltenwahl With {.Name = "Nachname", .Rolle = Spaltenrolle.Nachname},
            New Spaltenwahl With {.Name = "Geschlecht", .Rolle = Spaltenrolle.Attribut}}

        Assert.AreEqual(0, Spaltenzuordnung.Einwaende(w).Count,
                        "ohne bekanntes Vokabular gibt es nichts zu vergleichen")

        Dim einwaende = Spaltenzuordnung.Einwaende(w, {"geschlecht", "kann_kind"})
        Assert.AreEqual(1, einwaende.Count)
        StringAssert.Contains(einwaende(0), "geschlecht")
        StringAssert.Contains(einwaende(0), "Schreibweise")
    End Sub

    <TestMethod>
    Public Sub MitPassendemZielnameGibtEsKeinenEinwandMehr()
        Dim w = New List(Of Spaltenwahl) From {
            New Spaltenwahl With {.Name = "Nachname", .Rolle = Spaltenrolle.Nachname},
            New Spaltenwahl With {.Name = "Geschlecht", .Rolle = Spaltenrolle.Attribut,
                                  .Zielname = "geschlecht"}}
        Assert.AreEqual(0, Spaltenzuordnung.Einwaende(w, {"geschlecht"}).Count)
    End Sub

    ''' <summary>Und das Ziel wirkt auch wirklich: die Kinder tragen den
    ''' gewaehlten Schluessel, nicht den Spaltennamen.</summary>
    <TestMethod>
    Public Sub DerZielnameLandetAlsAttributschluesselImBestand()
        Dim p As New Projekt()
        Dim m = Modell(p)
        Dim v = m.ImportPruefen("Nachname;Geschlecht" & vbLf & "Meier;w" & vbLf & "Schulz;m")
        Dim w = Spaltenzuordnung.Vorschlag(v.Spalten)
        w(1).Rolle = Spaltenrolle.Attribut
        w(1).Zielname = "geschlecht"

        m.ImportUebernehmen(v, w)

        CollectionAssert.AreEqual(New List(Of String) From {"geschlecht"}, m.Attributnamen)
        Assert.AreEqual("w", p.Klassenbildung.Schueler(0).Attribute("geschlecht"))
    End Sub

    ''' <summary>Dasselbe fuer Gruppen: der Schluessel bestimmt den
    ''' Gruppennamen, damit eine Spalte "Rel" Gruppen "Religion-ev"
    ''' erzeugen kann.</summary>
    <TestMethod>
    Public Sub DerZielnameBestimmtAuchDenGruppennamen()
        Dim p As New Projekt()
        Dim m = Modell(p)
        Dim v = m.ImportPruefen("Nachname;Rel" & vbLf & "Meier;ev" & vbLf & "Schulz;kath")
        Dim w = Spaltenzuordnung.Vorschlag(v.Spalten)
        w(1).Rolle = Spaltenrolle.Gruppe
        w(1).Zielname = "Religion"

        m.ImportUebernehmen(v, w)

        CollectionAssert.AreEquivalent(New List(Of String) From {"Religion-ev", "Religion-kath"},
                                       p.Klassenbildung.Gruppen.Select(Function(g) g.Id).ToList())
    End Sub

    ' ===============================================================
    ' Regeln aus einer Attributspalte ableiten
    ' ===============================================================

    ''' <summary>ja/nein ist binaer: EINE Balance, und zwar auf "ja" -
    ''' auch wenn die Ja-Kinder die Mehrheit stellen. Wer Ganztag
    ''' importiert, will die Ganztagskinder verteilen, nicht die anderen.</summary>
    <TestMethod>
    Public Sub JaNeinErgibtEineBalanceAufJa()
        Dim a = Spaltenzuordnung.Ableitung({"ja", "ja", "ja", "nein", "ja"})
        Assert.AreEqual("balance", a.Art)
        Assert.AreEqual("ja", a.BalanceWert)
    End Sub

    ''' <summary>Ohne ein erkennbares Ja gilt der SELTENERE Wert: dessen
    ''' Verteilung steuert man, die Gegenseite folgt aus der
    ''' Klassengroesse.</summary>
    <TestMethod>
    Public Sub ZweiWerteOhneJaBalancierenDenSelteneren()
        Dim a = Spaltenzuordnung.Ableitung({"m", "w", "m", "m", "w"})
        Assert.AreEqual("balance", a.Art)
        Assert.AreEqual("w", a.BalanceWert)
        Assert.AreEqual("m", Spaltenzuordnung.Ableitung({"w", "w", "m"}).BalanceWert)
    End Sub

    ''' <summary>Eine Markierungsspalte - "ja" oder leer - ist ebenfalls
    ''' binaer; die Leerfelder sind die Kinder ohne das Attribut.</summary>
    <TestMethod>
    Public Sub EinWertNebenLeerfeldernIstBinaer()
        Dim a = Spaltenzuordnung.Ableitung({"", "ja", "", "", "ja"})
        Assert.AreEqual("balance", a.Art)
        Assert.AreEqual("ja", a.BalanceWert)
        Assert.AreEqual("x", Spaltenzuordnung.Ableitung({"x", "", " ", "x"}).BalanceWert,
                        "Leerraum zaehlt als leer")
    End Sub

    <TestMethod>
    Public Sub DreiUndMehrWerteErgebenBuendelungen()
        Dim a = Spaltenzuordnung.Ableitung({"Sonne", "Mond", "Sonne", "Stern", "Sonne", "Mond"})
        Assert.AreEqual("buendelung", a.Art)
        CollectionAssert.AreEqual(New List(Of String) From {"Sonne", "Mond", "Stern"}, a.Werte,
                                  "haeufigster Wert zuerst")
    End Sub

    ''' <summary>Tragen alle denselben Wert oder keiner einen, gibt es
    ''' nichts zu balancieren - und keine Regel, die dann ein stiller
    ''' No-Op waere.</summary>
    <TestMethod>
    Public Sub OhneUnterschiedGibtEsKeineRegel()
        Assert.AreEqual("keine", Spaltenzuordnung.Ableitung({"ja", "ja", "ja"}).Art)
        Assert.AreEqual("keine", Spaltenzuordnung.Ableitung({"", "", ""}).Art)
        Assert.AreEqual("keine", Spaltenzuordnung.Ableitung(New String() {}).Art)
        Assert.AreEqual("keine Regel ableitbar", Spaltenzuordnung.Ableitung({"ja"}).Beschreibung())
    End Sub

    ''' <summary>Der Durchstich: das Attribut bleibt am Kind, UND die
    ''' Regel entsteht - eine Balance auf ja, Buendelungen je Kita mit
    ''' der Konvention attribut_wert.</summary>
    <TestMethod>
    Public Sub AbleitungLegtBalanceUndBuendelungenAn()
        Dim p As New Projekt()
        Dim m = Modell(p)
        Dim v = m.ImportPruefen(
            "Nachname;Betreuung;Kita" & vbLf &
            "Meier;ja;Sonne" & vbLf & "Schulz;;Mond" & vbLf & "Braun;ja;Sonne" & vbLf &
            "Klein;;Stern" & vbLf & "Wolf;nein;Mond")
        Dim w = Spaltenzuordnung.Vorschlag(v.Spalten)
        w(1).Rolle = Spaltenrolle.Attribut : w(1).RegelAbleiten = True
        w(2).Rolle = Spaltenrolle.Attribut : w(2).RegelAbleiten = True

        Dim bericht = m.ImportUebernehmen(v, w)

        Assert.AreEqual("ja", p.Klassenbildung.Schueler(0).Attribute("Betreuung"))
        Assert.AreEqual("Sonne", p.Klassenbildung.Schueler(0).Attribute("Kita"), "das Attribut bleibt am Kind")

        Dim b = p.Klassenbildung.Balance.Single()
        Assert.AreEqual("Betreuung", b.Attribut)
        Assert.AreEqual("ja", b.Wert)
        Assert.AreEqual(1, b.Toleranz)
        Assert.AreEqual("soft", b.Modus)
        Assert.AreEqual(2, b.Prio)
        CollectionAssert.AreEqual(New List(Of String) From {"Betreuung = ja"}, bericht.Balance)

        CollectionAssert.AreEquivalent(New List(Of String) From {"Kita_Sonne", "Kita_Mond", "Kita_Stern"},
                                       p.Klassenbildung.Gruppen.Select(Function(g) g.Id).ToList())
        Dim sonne = p.Klassenbildung.Gruppen.Single(Function(g) g.Id = "Kita_Sonne")
        Assert.AreEqual("buendelung", sonne.Typ)
        Assert.AreEqual(2, sonne.Mitglieder.Count)
        CollectionAssert.AreEquivalent(
            p.Klassenbildung.Schueler.Where(Function(s) s.Attribute("Kita") = "Sonne").Select(Function(s) s.Id).ToList(),
            sonne.Mitglieder)
        StringAssert.Contains(bericht.Klartext(), "Balance-Regel(n) angelegt: Betreuung = ja")
        StringAssert.Contains(bericht.Klartext(), "Kita_Sonne")
    End Sub

    ''' <summary>Ohne den Haken passiert nichts Neues - der Import legt
    ''' Regeln nur an, wenn es jemand gesagt hat.</summary>
    <TestMethod>
    Public Sub OhneHakenEntstehtKeineRegel()
        Dim p As New Projekt()
        Dim m = Modell(p)
        Dim v = m.ImportPruefen("Nachname;Betreuung" & vbLf & "Meier;ja" & vbLf & "Schulz;nein")
        Dim w = Spaltenzuordnung.Vorschlag(v.Spalten)
        w(1).Rolle = Spaltenrolle.Attribut

        m.ImportUebernehmen(v, w)

        Assert.AreEqual(0, p.Klassenbildung.Balance.Count)
        Assert.AreEqual(0, p.Klassenbildung.Gruppen.Count)
    End Sub

    ''' <summary>Dieselbe Liste ein zweites Mal: die Regel gibt es schon,
    ''' sie wird NICHT verdoppelt - und der Bericht sagt das.</summary>
    <TestMethod>
    Public Sub EineVorhandeneRegelWirdNichtVerdoppelt()
        Dim p As New Projekt()
        Dim m = Modell(p)
        Dim text = "Nachname;Betreuung;Kita" & vbLf & "Meier;ja;A" & vbLf & "Schulz;nein;B" & vbLf & "Braun;ja;C"
        Dim wahl =
            Function(v As KlassenbildungEingabeViewModel.ImportVorschau) As List(Of Spaltenwahl)
                Dim w = Spaltenzuordnung.Vorschlag(v.Spalten)
                w(1).Rolle = Spaltenrolle.Attribut : w(1).RegelAbleiten = True
                w(2).Rolle = Spaltenrolle.Attribut : w(2).RegelAbleiten = True
                Return w
            End Function
        Dim v1 = m.ImportPruefen(text)
        m.ImportUebernehmen(v1, wahl(v1))
        Dim v2 = m.ImportPruefen(text)
        Dim bericht = m.ImportUebernehmen(v2, wahl(v2))

        Assert.AreEqual(1, p.Klassenbildung.Balance.Count)
        Assert.AreEqual(3, p.Klassenbildung.Gruppen.Count)
        Assert.AreEqual(0, bericht.Balance.Count)
        Assert.IsTrue(bericht.Hinweise.Any(Function(h) h.Contains("Betreuung = ja") AndAlso h.Contains("gab es schon")))
        Assert.IsTrue(bericht.Hinweise.Any(Function(h) h.Contains("Kita_A") AndAlso h.Contains("gab es schon")))
    End Sub

    ''' <summary>Eine Spalte, aus der nichts folgt, bleibt Attribut - und
    ''' der Bericht sagt, warum keine Regel entstand.</summary>
    <TestMethod>
    Public Sub EineEinheitlicheSpalteErgibtEinenHinweis()
        Dim p As New Projekt()
        Dim m = Modell(p)
        Dim v = m.ImportPruefen("Nachname;Stufe" & vbLf & "Meier;1" & vbLf & "Schulz;1")
        Dim w = Spaltenzuordnung.Vorschlag(v.Spalten)
        w(1).Rolle = Spaltenrolle.Attribut : w(1).RegelAbleiten = True

        Dim bericht = m.ImportUebernehmen(v, w)

        Assert.AreEqual("1", p.Klassenbildung.Schueler(0).Attribute("Stufe"))
        Assert.AreEqual(0, p.Klassenbildung.Balance.Count)
        Assert.IsTrue(bericht.Hinweise.Any(Function(h) h.Contains("Stufe") AndAlso h.Contains("denselben Wert")))
    End Sub

    ''' <summary>Die Einschulungsliste mit Ableitung auf allen vier
    ''' Merkmalen: drei binaere ergeben drei Balancen, das Wohngebiet
    ''' Buendelungen - und der KERN nimmt das Ergebnis ohne Einwand an.
    ''' Eine abgeleitete Regel, die der Validator ablehnt, waere ein
    ''' Import, der das Rechnen kaputt macht.</summary>
    <TestMethod>
    Public Sub DieAbgeleitetenRegelnDerEinschulungslisteBestehenDenKern()
        Dim p As New Projekt()
        p.Klassenbildung.Klassen.Anzahl = 4
        p.Klassenbildung.Klassen.MinGroesse = 20
        p.Klassenbildung.Klassen.MaxGroesse = 30
        Dim m = Modell(p)
        Dim v = m.ImportPruefen(ImportDialog.DateiLesen(
            Beispieldatei("bw-grundschule-beispiel", "einschulungsliste.csv")))
        Dim w = Spaltenzuordnung.Vorschlag(v.Spalten)
        For Each name In {"Geschlecht", "Sprachfoerderung", "Kann-Kind", "Wohngebiet"}
            Dim x = w.Single(Function(s) s.Name = name)
            x.Rolle = Spaltenrolle.Attribut
            x.RegelAbleiten = True
        Next

        Dim bericht = m.ImportUebernehmen(v, w)

        CollectionAssert.AreEquivalent(
            New List(Of String) From {"Geschlecht = w", "Sprachfoerderung = ja", "Kann-Kind = ja"},
            bericht.Balance)
        Assert.IsTrue(bericht.Gruppen.Count >= 3, "das Wohngebiet ist eine Werteliste")
        Assert.IsTrue(bericht.Gruppen.All(Function(g) g.StartsWith("Wohngebiet_")))
        Assert.IsTrue(p.Klassenbildung.Gruppen.All(Function(g) g.Typ = "buendelung"))
        Assert.AreEqual(100, p.Klassenbildung.Gruppen.Sum(Function(g) g.Mitglieder.Count),
                        "jedes Kind in genau einer Wohngebiets-Buendelung")

        Dim einwaende = Klassenbildung.ValidateKlassenbildung(p.Klassenbildung)
        Assert.AreEqual(0, einwaende.Count, String.Join(" | ", einwaende))
    End Sub

End Class



