' Phase 2.18f: `new`-Subcommand - baut aus einem Templates.vb-Template +
' den CLI-Parametern (Bundesland/Schulart/Anzahl Klassenstufen/Anzahl
' Lehrer[/Zuege]) einen kompletten Stammdatenbestand und schreibt ihn als
' tests/<schule>/input/stammdaten.yaml + eine leere, kommentierte
' constraints.yaml.
Imports TimetableCore

Public Module Scaffold

    ''' <summary>Erzeugt `tests/&lt;schule&gt;/input/{stammdaten,constraints}.yaml`.
    ''' Schreibt NICHT, falls das Zielverzeichnis bereits existiert (kein
    ''' versehentliches Ueberschreiben eines vorhandenen Testfalls).
    ''' `lehrerAnzahl` (Nutzerentscheidung "exakte Anzahl", Phase-2.18-
    ''' Feinplanung) wird per Rundlauf auf alle Klassenlehrer-Pool-Typen
    ''' des Templates verteilt - fuer die Grundschule gibt es genau einen
    ''' solchen Pool-Typ, fuer die Gemeinschaftsschule drei (Zwei-Faecher-
    ''' Prinzip). Die stets benoetigten Fachlehrer-Spezialisten (Religion/
    ''' Englisch bzw. NaWi/Sport/Musik-Kunst/Religion) haben dafuer KEINEN
    ''' eigenen Parameter und werden automatisch bedarfsgerecht bemessen
    ''' (Math.Ceiling(Wochenstunden-Bedarf / Pool-Deputat), mindestens
    ''' 1).</summary>
    Public Sub Run(testsRoot As String, schule As String, bundesland As String, schulart As String,
                    klassenstufenAnzahl As Integer, lehrerAnzahl As Integer, Optional zuege As Integer = 2)
        Dim inputDir = IO.Path.Combine(testsRoot, schule, "input")
        If IO.Directory.Exists(inputDir) Then
            Throw New InvalidOperationException(
                $"'{inputDir}' existiert bereits - kein Ueberschreiben eines vorhandenen Testfalls. " &
                "Loesche das Verzeichnis von Hand, falls ein Neuaufbau gewuenscht ist.")
        End If

        Dim b = Baue(bundesland, schulart, klassenstufenAnzahl, lehrerAnzahl, zuege,
                     $"{schule} (per CLI generiert)")

        IO.Directory.CreateDirectory(inputDir)
        YamlStammdaten.SaveStammdatenYaml(b, IO.Path.Combine(inputDir, "stammdaten.yaml"))
        IO.File.WriteAllText(IO.Path.Combine(inputDir, "constraints.yaml"), LeereConstraints())

        Console.WriteLine($"Erzeugt: {inputDir}/stammdaten.yaml ({b.Klassen.Count} Klassen, {b.Lehrkraefte.Count} Lehrkraefte, {b.Faecher.Count} Faecher)")
        Console.WriteLine($"Erzeugt: {inputDir}/constraints.yaml (leer, mit Beispiel-Kommentar)")
    End Sub

    ''' <summary>Der kommentierte Rumpf einer leeren constraints.yaml.
    ''' Eigene Funktion, seit der Projekt-Assistent (gui-ui-konzept 6.1)
    ''' denselben Text braucht - ein zweiter Wortlaut waere eine zweite
    ''' Wahrheit.</summary>
    Public Function LeereConstraints() As String
        Return "# Zusaetzliche, handverfasste Regeln der 2. Solver-Stufe (siehe tests/README.md)." & vbLf &
            "# Nur Typen, die die Stammdaten NICHT bereits abdecken - z.B. teacher_availability," & vbLf &
            "# forbidden_slot, room_requirement. Oberste Ebene ist eine YAML-Liste, ein Mapping" & vbLf &
            "# pro Constraint. Beispiel (auskommentiert):" & vbLf &
            "#" & vbLf &
            "# - type: room_requirement" & vbLf &
            "#   class: 1a" & vbLf &
            "#   subject: Sport" & vbLf &
            "#   allowed_rooms: [Turnhalle1, Turnhalle2]" & vbLf
    End Function

    ''' <summary>Derselbe Bestand, den `Run` in eine Datei schreibt - nur
    ''' im Speicher und ohne Dateisystem. Der Projekt-Assistent der
    ''' Oberflaeche (6.1) haengt HIER an statt eine eigene Erzeugung
    ''' mitzubringen: sonst haetten CLI und GUI zwei Vorstellungen davon,
    ''' wie eine frische Schule aussieht - und die eine wuerde still von
    ''' der anderen abweichen.</summary>
    Public Function Baue(bundesland As String, schulart As String,
                         klassenstufenAnzahl As Integer, lehrerAnzahl As Integer,
                         Optional zuege As Integer = 2,
                         Optional schulName As String = Nothing) As Stammdatenbestand
        Dim template = Templates.TemplateFuer(bundesland, schulart)

        If klassenstufenAnzahl < 1 OrElse klassenstufenAnzahl > template.Klassenstufen.Count Then
            Throw New InvalidOperationException($"--klassenstufen muss zwischen 1 und {template.Klassenstufen.Count} liegen fuer Schulart '{schulart}'.")
        End If
        If zuege < 1 Then Throw New InvalidOperationException("--zuege muss mindestens 1 sein.")

        Dim gesteuertePools = template.LehrerPools.Where(Function(p) p.GesteuertDurchAnzahlLehrerParameter).ToList()
        If lehrerAnzahl < gesteuertePools.Count Then
            Throw New InvalidOperationException(
                $"--lehrer muss mindestens {gesteuertePools.Count} sein fuer Schulart '{schulart}' " &
                $"(je ein Klassenlehrer-Pool-Typ: {String.Join(", ", gesteuertePools.Select(Function(p) p.NamePrefix))}) - " &
                "sonst bliebe mindestens ein Kernfach ganz ohne qualifizierte Lehrkraft.")
        End If

        Dim gewaehlt = template.Klassenstufen.Take(klassenstufenAnzahl).ToList()
        Dim zuegeBuchstaben = Enumerable.Range(0, zuege).Select(Function(i) Chr(Asc("a"c) + i)).ToList()

        Dim b As New Stammdatenbestand With {
            .SchulName = If(schulName, "Neue Schule"),
            .Bundesland = bundesland, .Schulart = schulart,
            .Tage = New List(Of String) From {"Mo", "Di", "Mi", "Do", "Fr"},
            .PeriodsPerDay = template.PeriodsPerDay
        }

        For Each ks In gewaehlt
            b.Klassenstufen.Add(New Klassenstufe With {.Nummer = ks.Nummer, .Bezeichnung = ks.Bezeichnung})
            For Each buchstabe In zuegeBuchstaben
                b.Klassen.Add(New Klasse With {.Name = $"{ks.Nummer}{buchstabe}", .Klassenstufe = ks.Nummer, .Schuelerzahl = 22})
            Next
        Next

        For Each ks In gewaehlt
            For Each tf In ks.Faecher
                Dim fach = b.Faecher.FirstOrDefault(Function(f) f.Name = tf.Name)
                If fach Is Nothing Then
                    fach = New Fach With {.Name = tf.Name, .BlockLength = tf.BlockLength}
                    b.Faecher.Add(fach)
                End If
                fach.Klassenstufen.Add(New FachKlassenstufe With {.Klassenstufe = ks.Nummer, .WochenstundenSoll = tf.WochenstundenSoll, .MaxProTag = tf.MaxProTag})
            Next
        Next

        ' Klassenlehrer-Pool(s): "--lehrer" wird NICHT blind im Rundlauf
        ' verteilt, sondern proportional zum tatsaechlichen Wochenstunden-
        ' Bedarf jedes Pool-Typs (live entdeckt, Phase 2.18 End-to-End-
        ' Testlauf: ein blinder Rundlauf ueber die 3 Gemeinschaftsschule-
        ' Pools erzeugte fuer den Englisch-Erdkunde-Pool eine Lehrkraft mit
        ' 42 Wochenstunden - physisch unmoeglich in 40 verfuegbaren Slots
        ' [5 Tage x 8 Perioden], Solver.Solve wurde dadurch Infeasible. Die
        ' Bedarfs-Formel ist dieselbe wie fuer die automatisch bemessenen
        ' Spezialisten-Pools unten, hier per "Largest-Remainder"-Verfahren
        ' auf die exakt angeforderte Gesamtzahl `lehrerAnzahl` skaliert -
        ' garantiert mindestens 1 pro Pool-Typ (bereits oben geprueft) UND
        ' eine Summe von exakt `lehrerAnzahl`.
        Dim gesteuerteBedarfe = gesteuertePools.Select(Function(p) FachBedarfStunden(b, p)).ToList()
        Dim gesteuerteAnzahlen = AllocateProportional(lehrerAnzahl, gesteuerteBedarfe)
        For poolIndex = 0 To gesteuertePools.Count - 1
            Dim pool = gesteuertePools(poolIndex)
            For i = 1 To gesteuerteAnzahlen(poolIndex)
                AddLehrkraft(b, pool, $"{pool.NamePrefix}-{i}")
            Next
        Next

        ' Stets benoetigte Fachlehrer-Spezialisten: automatisch
        ' bedarfsgerecht bemessen (Wochenstunden-Bedarf der tatsaechlich
        ' gefuehrten Faecher dieses Pools / Pool-Deputat, aufgerundet,
        ' mindestens 1 - Faecher, die von den gewaehlten Klassenstufen gar
        ' nicht gefuehrt werden, tragen 0 bei und ueberspringen den Pool
        ' komplett).
        For Each pool In template.LehrerPools.Where(Function(p) Not p.GesteuertDurchAnzahlLehrerParameter)
            Dim bedarf = FachBedarfStunden(b, pool)
            If bedarf <= 0 Then Continue For

            Dim anzahl = Math.Max(1, CInt(Math.Ceiling(bedarf / pool.Deputat)))
            For i = 1 To anzahl
                AddLehrkraft(b, pool, $"{pool.NamePrefix}-{i}")
            Next
        Next

        Dim errors = StammdatenValidation.ValidateStammdaten(b)
        If errors.Count > 0 Then
            Throw New InvalidOperationException(
                "Generiertes Grundgeruest ist ungueltig (interner Fehler in Scaffold.vb/Templates.vb):" & vbLf & String.Join(vbLf, errors))
        End If

        Return b
    End Function

    ''' <summary>Gesamter Wochenstunden-Bedarf der Faecher eines Pools ueber
    ''' alle bereits in `b.Klassen`/`b.Faecher` gebauten Klassen - dieselbe
    ''' Formel, die sowohl die gesteuerten (proportionale Aufteilung) als
    ''' auch die automatisch bemessenen Pools verwenden.</summary>
    Private Function FachBedarfStunden(b As Stammdatenbestand, pool As TemplateLehrerPool) As Double
        Dim bedarf = 0.0
        For Each fachName In pool.Faecher
            Dim fach = b.Faecher.FirstOrDefault(Function(f) f.Name = fachName)
            If fach Is Nothing Then Continue For
            For Each klasse In b.Klassen
                Dim fk = Stammdaten.WochenstundenFuer(fach, klasse.Klassenstufe)
                If fk IsNot Nothing Then bedarf += fk.WochenstundenSoll
            Next
        Next
        Return bedarf
    End Function

    ''' <summary>Verteilt `total` ganzzahlige Einheiten proportional zu
    ''' `weights` (Largest-Remainder-Verfahren, wie bei Sitzverteilungen) -
    ''' garantiert mindestens 1 Einheit pro Gewicht (Aufrufer muss
    ''' `total &gt;= weights.Count` sicherstellen) und eine Summe von exakt
    ''' `total`. Ein Gewicht von 0 (kein Bedarf) bekommt trotzdem die
    ''' garantierte 1 Basis-Einheit, aber keine der proportional verteilten
    ''' Zusatz-Einheiten.</summary>
    Private Function AllocateProportional(total As Integer, weights As List(Of Double)) As List(Of Integer)
        Dim n = weights.Count
        Dim result = Enumerable.Repeat(1, n).ToList()
        Dim extra = total - n
        If extra <= 0 Then Return result

        Dim totalWeight = weights.Sum()
        If totalWeight <= 0 Then
            For i = 0 To extra - 1
                result(i Mod n) += 1
            Next
            Return result
        End If

        Dim shares = weights.Select(Function(w) extra * w / totalWeight).ToList()
        Dim bases = shares.Select(Function(s) CInt(Math.Floor(s))).ToList()
        Dim rest = extra - bases.Sum()
        Dim remainders = Enumerable.Range(0, n).Select(Function(i) (Index:=i, Remainder:=shares(i) - bases(i))).OrderByDescending(Function(r) r.Remainder).ToList()
        For Each item In remainders.Take(rest)
            bases(item.Index) += 1
        Next
        For i = 0 To n - 1
            result(i) += bases(i)
        Next
        Return result
    End Function

    Private Sub AddLehrkraft(b As Stammdatenbestand, pool As TemplateLehrerPool, name As String)
        b.Lehrkraefte.Add(New Lehrer With {
            .Name = name, .DeputatSollstunden = pool.Deputat, .Anrechnungsstunden = pool.Anrechnungsstunden,
            .KlassenlehrerFaehig = pool.KlassenlehrerFaehig
        })
        For Each fachName In pool.Faecher
            If b.Faecher.Any(Function(f) f.Name = fachName) Then
                b.FachLehrerZuordnungen.Add(New FachLehrerZuordnung With {.LehrerName = name, .FachName = fachName})
            End If
        Next
    End Sub

End Module
