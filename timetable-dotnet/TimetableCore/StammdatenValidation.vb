' Phase 2.15b: deterministische Cross-Reference-Pruefung fuer
' Stammdatenbestand - dieselbe "Fail-Fast VOR jedem Solve"-Philosophie wie
' Validation.ValidateEntities (siehe docs/arc42-architecture.md Abschnitt
' 8.1), hier fuer die typisierten Stammdaten statt fuer das rohe
' entities/constraints-JSON. Eigenes Modul (mirrort die bestehende
' Trennung Validation.vb/Verifier.vb als jeweils eigenstaendige Module).
Public Module StammdatenValidation

    ''' <summary>Prueft einen Stammdatenbestand auf harte, das Loesen
    ''' blockierende Fehler: unbekannte Klassenstufen-Referenzen (Fach/
    ''' Klasse), unbekannte Lehrer-/Fach-Referenzen in
    ''' FachLehrerZuordnungen, unplausible Deputate, sowie zwei
    ''' strukturelle Luecken, die Lehrereinsatzplanung.SolveLehrereinsatz
    ''' sonst erst als schwer diagnostizierbares Infeasible entdecken
    ''' wuerde: eine tatsaechlich genutzte Klassenstufe ganz ohne Fach, und
    ''' ein in einer genutzten Klassenstufe gefuehrtes Fach ganz ohne
    ''' qualifizierte Lehrkraft.</summary>
    Public Function ValidateStammdaten(bestand As Stammdatenbestand) As List(Of String)
        Dim errors As New List(Of String)
        Dim klassenstufenNummern As New HashSet(Of Integer)(bestand.Klassenstufen.Select(Function(k) k.Nummer))
        Dim fachNamen As New HashSet(Of String)(bestand.Faecher.Select(Function(f) f.Name))
        Dim lehrerNamen As New HashSet(Of String)(bestand.Lehrkraefte.Select(Function(l) l.Name))

        For i = 0 To bestand.Faecher.Count - 1
            Dim fach = bestand.Faecher(i)
            For j = 0 To fach.Klassenstufen.Count - 1
                Dim fk = fach.Klassenstufen(j)
                If Not klassenstufenNummern.Contains(fk.Klassenstufe) Then
                    errors.Add($"faecher[{i}] (name={JsonHelpers.PyRepr(fach.Name)}): klassenstufen[{j}].Klassenstufe={fk.Klassenstufe} ist keine bekannte Klassenstufe")
                End If
                If fk.WochenstundenSoll <= 0 Then
                    errors.Add($"faecher[{i}] (name={JsonHelpers.PyRepr(fach.Name)}): klassenstufen[{j}].WochenstundenSoll muss > 0 sein")
                End If
            Next
        Next

        For i = 0 To bestand.Klassen.Count - 1
            Dim klasse = bestand.Klassen(i)
            If Not klassenstufenNummern.Contains(klasse.Klassenstufe) Then
                errors.Add($"klassen[{i}] (name={JsonHelpers.PyRepr(klasse.Name)}): Klassenstufe={klasse.Klassenstufe} ist keine bekannte Klassenstufe")
            End If
        Next

        For i = 0 To bestand.FachLehrerZuordnungen.Count - 1
            Dim z = bestand.FachLehrerZuordnungen(i)
            If Not lehrerNamen.Contains(z.LehrerName) Then
                errors.Add($"fach_lehrer_zuordnungen[{i}]: lehrer_name={JsonHelpers.PyRepr(z.LehrerName)} ist keine bekannte Lehrkraft")
            End If
            If Not fachNamen.Contains(z.FachName) Then
                errors.Add($"fach_lehrer_zuordnungen[{i}]: fach_name={JsonHelpers.PyRepr(z.FachName)} ist kein bekanntes Fach")
            End If
        Next

        For i = 0 To bestand.Lehrkraefte.Count - 1
            Dim l = bestand.Lehrkraefte(i)
            If l.DeputatSollstunden <= 0 Then
                errors.Add($"lehrkraefte[{i}] (name={JsonHelpers.PyRepr(l.Name)}): DeputatSollstunden muss > 0 sein")
            End If
            If l.Anrechnungsstunden < 0 Then
                errors.Add($"lehrkraefte[{i}] (name={JsonHelpers.PyRepr(l.Name)}): Anrechnungsstunden darf nicht negativ sein")
            End If
            If l.SpringerReserveStunden < 0 Then
                errors.Add($"lehrkraefte[{i}] (name={JsonHelpers.PyRepr(l.Name)}): SpringerReserveStunden darf nicht negativ sein")
            End If
            If l.DeputatSollstunden > 0 AndAlso (l.Anrechnungsstunden + l.SpringerReserveStunden) >= l.DeputatSollstunden Then
                errors.Add($"lehrkraefte[{i}] (name={JsonHelpers.PyRepr(l.Name)}): Anrechnungsstunden ({l.Anrechnungsstunden}) + SpringerReserveStunden ({l.SpringerReserveStunden}) darf nicht das gesamte Deputat ({l.DeputatSollstunden}) aufzehren")
            End If
            If l.MaxKlassen.HasValue AndAlso l.MaxKlassen.Value <= 0 Then
                errors.Add($"lehrkraefte[{i}] (name={JsonHelpers.PyRepr(l.Name)}): MaxKlassen muss > 0 sein, wenn gesetzt")
            End If
            If l.MaxFaecher.HasValue AndAlso l.MaxFaecher.Value <= 0 Then
                errors.Add($"lehrkraefte[{i}] (name={JsonHelpers.PyRepr(l.Name)}): MaxFaecher muss > 0 sein, wenn gesetzt")
            End If
        Next

        ' Strukturelle Luecken (nur fuer tatsaechlich genutzte Klassenstufen relevant -
        ' eine im Katalog vorgehaltene, aber noch von keiner Klasse benutzte
        ' Klassenstufe ohne Fach ist kein Fehler).
        Dim genutzteKlassenstufen As New HashSet(Of Integer)(bestand.Klassen.Select(Function(k) k.Klassenstufe))

        For Each ks In bestand.Klassenstufen
            If genutzteKlassenstufen.Contains(ks.Nummer) AndAlso
               Not bestand.Faecher.Any(Function(f) f.Klassenstufen.Any(Function(fk) fk.Klassenstufe = ks.Nummer)) Then
                errors.Add($"klassenstufe {ks.Nummer} ({ks.Bezeichnung}): wird von mindestens einer Klasse genutzt, hat aber kein einziges Fach")
            End If
        Next

        For Each fach In bestand.Faecher
            Dim inGenutzterKlassenstufeGefuehrt = fach.Klassenstufen.Any(Function(fk) genutzteKlassenstufen.Contains(fk.Klassenstufe))
            If inGenutzterKlassenstufeGefuehrt AndAlso Stammdaten.LehrerFuerFach(bestand, fach.Name).Count = 0 Then
                errors.Add($"fach {JsonHelpers.PyRepr(fach.Name)}: keine qualifizierte Lehrkraft in fach_lehrer_zuordnungen gefunden, wird aber in einer genutzten Klassenstufe gefuehrt")
            End If
        Next

        ' Phase 2.17: Teilzeit-Tage-Kohaerenz - fuer jede (Klassenstufe,Fach)-
        ' Kombination, die tatsaechlich genutzt wird, muss mindestens EIN
        ' fachlich qualifizierter Kandidat auch teilzeit-tage-kohaerent sein
        ' (siehe Stammdaten.IstTeilzeitKohaerent). Sonst wuerde
        ' Lehrereinsatzplanung.SolveLehrereinsatz diesen Kandidaten beim
        ' harten Vorfilter komplett ausschliessen und die "genau 1
        ' Lehrkraft"-Vollstaendigkeitssumme koennte leer bleiben (0 = 1,
        ' garantiert Infeasible) - dieselbe strukturelle Luecke, die die
        ' bereits bestehende "Fach ohne qualifizierte Lehrkraft"-Pruefung
        ' oben fuer den einfacheren Fall (gar keine Qualifikation) abfaengt.
        For Each fach In bestand.Faecher
            For Each fk In fach.Klassenstufen
                If Not genutzteKlassenstufen.Contains(fk.Klassenstufe) Then Continue For
                Dim qualifiziert = Stammdaten.LehrerFuerFach(bestand, fach.Name)
                If qualifiziert.Count = 0 Then Continue For ' bereits oben gemeldet
                If Not qualifiziert.Any(Function(l) Stammdaten.IstTeilzeitKohaerent(l, bestand, fk)) Then
                    errors.Add($"fach {JsonHelpers.PyRepr(fach.Name)}, klassenstufe {fk.Klassenstufe}: kein teilzeit-tage-kohaerenter Kandidat gefunden (WochenstundenSoll={fk.WochenstundenSoll} passt bei keiner qualifizierten Lehrkraft in deren VerfuegbareTage)")
                End If
            Next
        Next

        ' Phase 2.19: Mitgliedschaftsdatenmodell, Schritt 1 - reine
        ' Referenz-/Eindeutigkeitspruefung, noch ohne jede Solver-Bedeutung
        ' (siehe docs/phase2-19-mitgliedschaftsmodell.md fuer den bewusst
        ' zurueckgestellten naechsten Schritt).
        Dim klassenNamen As New HashSet(Of String)(bestand.Klassen.Select(Function(k) k.Name))
        Dim schuelerIds As New HashSet(Of String)
        For i = 0 To bestand.Schueler.Count - 1
            Dim s = bestand.Schueler(i)
            If Not klassenNamen.Contains(s.Klasse) Then
                errors.Add($"schueler[{i}] (id={JsonHelpers.PyRepr(s.Id)}): klasse={JsonHelpers.PyRepr(s.Klasse)} ist keine bekannte Klasse")
            End If
            If Not schuelerIds.Add(s.Id) Then
                errors.Add($"schueler[{i}]: id={JsonHelpers.PyRepr(s.Id)} ist bereits vergeben (doppelte Schueler-ID)")
            End If
        Next

        For i = 0 To bestand.Gruppen.Count - 1
            Dim g = bestand.Gruppen(i)
            For j = 0 To g.MitgliederSchuelerIds.Count - 1
                Dim schuelerId = g.MitgliederSchuelerIds(j)
                If Not schuelerIds.Contains(schuelerId) Then
                    errors.Add($"gruppen[{i}] (name={JsonHelpers.PyRepr(g.Name)}): mitglieder_schueler_ids[{j}]={JsonHelpers.PyRepr(schuelerId)} ist keine bekannte Schueler-ID")
                End If
            Next
        Next

        ' Phase 2.20: Parallelgruppe-Referenz-/Konsistenzpruefung - faengt
        ' strukturelle Fehler ab, die sonst erst als schwer
        ' diagnostizierbares Solver.vb-Infeasible auftauchen wuerden:
        ' unterschiedliche WochenstundenSoll/BlockLength innerhalb eines
        ' Parallelverbunds machen die per Gleichheit erzwungene Slot-
        ' Synchronisation strukturell unloesbar (siehe
        ' docs/phase2-20-parallelgruppen.md).
        Dim klasseByName = bestand.Klassen.ToDictionary(Function(k) k.Name)
        Dim fachByName = bestand.Faecher.ToDictionary(Function(f) f.Name)

        For i = 0 To bestand.Gruppen.Count - 1
            Dim g = bestand.Gruppen(i)
            If g.FachName IsNot Nothing AndAlso Not fachByName.ContainsKey(g.FachName) Then
                errors.Add($"gruppen[{i}] (name={JsonHelpers.PyRepr(g.Name)}): fach_name={JsonHelpers.PyRepr(g.FachName)} ist kein bekanntes Fach")
            End If
            If g.Klassenstufe.HasValue Then
                Dim mitgliederKlassenstufen = Stammdaten.KlassenOfGruppe(bestand, g).
                    Where(Function(kn) klasseByName.ContainsKey(kn)).
                    Select(Function(kn) klasseByName(kn).Klassenstufe).Distinct().ToList()
                If mitgliederKlassenstufen.Any(Function(ks) ks <> g.Klassenstufe.Value) Then
                    errors.Add($"gruppen[{i}] (name={JsonHelpers.PyRepr(g.Name)}): klassenstufe={g.Klassenstufe.Value} stimmt nicht mit der tatsaechlichen Klassenstufe aller Mitglieder ueberein ({String.Join(",", mitgliederKlassenstufen)})")
                End If
                If g.FachName IsNot Nothing AndAlso fachByName.ContainsKey(g.FachName) Then
                    If Stammdaten.WochenstundenFuer(fachByName(g.FachName), g.Klassenstufe.Value) Is Nothing Then
                        errors.Add($"gruppen[{i}] (name={JsonHelpers.PyRepr(g.Name)}): fach {JsonHelpers.PyRepr(g.FachName)} wird in klassenstufe {g.Klassenstufe.Value} nicht gefuehrt")
                    End If
                End If
            End If
        Next

        For Each verbundGroup In bestand.Gruppen.Where(Function(g) g.Parallelverbund IsNot Nothing).GroupBy(Function(g) g.Parallelverbund)
            Dim mitglieder = verbundGroup.ToList()
            Dim verbund = verbundGroup.Key

            Dim ohneFach = mitglieder.Where(Function(g) g.FachName Is Nothing).ToList()
            If ohneFach.Count > 0 Then
                errors.Add($"parallelverbund {JsonHelpers.PyRepr(verbund)}: {ohneFach.Count} Gruppe(n) ohne fach_name ({String.Join(",", ohneFach.Select(Function(g) g.Name))})")
            End If

            ' Phase 2.23: Dublettenpruefung auf das Tupel (Klassenstufe,
            ' FachName) statt nur FachName - deckt weiterhin den
            ' urspruenglichen Fehlerfall ab ("2 Gruppen derselben
            ' Klassenstufe beanspruchen dasselbe Fach", z.B. 2x
            ' Religion-ev in Klassenstufe 1), erlaubt aber jetzt zusaetzlich
            ' den umgekehrten, klassenstufenuebergreifenden Fall (dasselbe
            ' Fach ueber mehrere Klassenstufen hinweg synchronisiert, z.B.
            ' eine schulweite Chor-Gesamtprobe).
            Dim tupelInVerbund = mitglieder.
                Where(Function(g) g.FachName IsNot Nothing AndAlso g.Klassenstufe.HasValue).
                Select(Function(g) (g.Klassenstufe.Value, g.FachName)).ToList()
            If tupelInVerbund.Distinct().Count() < tupelInVerbund.Count Then
                errors.Add($"parallelverbund {JsonHelpers.PyRepr(verbund)}: mehrere Gruppen beanspruchen dieselbe Kombination aus klassenstufe und fach_name - muss innerhalb eines Parallelverbunds eindeutig sein")
            End If

            ' Phase 2.23: die WochenstundenSoll/BlockLength-Konsistenzpruefung
            ' gilt jetzt fuer ALLE Mitglieder unabhaengig davon, ob sie
            ' dieselbe oder unterschiedliche Klassenstufen haben - jedes
            ' Mitglied schlaegt seinen Wert ueber seine EIGENE Klassenstufe
            ' nach. Die zugrunde liegende Notwendigkeit bleibt unveraendert:
            ' Solver.vb erzwingt fuer den ganzen Verbund eine Lesson-
            ' Gleichheitskette (parallel_group) - unterschiedliche
            ' Wochenstunden/Blocklaengen waeren dabei strukturell
            ' unloesbar, egal ob die Mitglieder dieselbe Klassenstufe teilen
            ' oder nicht.
            Dim details As New List(Of (Gruppe As String, Stunden As Integer?, Block As Integer?))
            For Each g In mitglieder.Where(Function(mg) mg.FachName IsNot Nothing AndAlso mg.Klassenstufe.HasValue AndAlso fachByName.ContainsKey(mg.FachName))
                Dim fach = fachByName(g.FachName)
                Dim fk = Stammdaten.WochenstundenFuer(fach, g.Klassenstufe.Value)
                details.Add((g.Name, If(fk IsNot Nothing, CType(fk.WochenstundenSoll, Integer?), Nothing), fach.BlockLength))
            Next
            If details.Select(Function(d) d.Stunden).Distinct().Count() > 1 Then
                errors.Add($"parallelverbund {JsonHelpers.PyRepr(verbund)}: unterschiedliche wochenstunden_soll je Fach/Klassenstufe ({String.Join(", ", details.Select(Function(d) $"{d.Gruppe}={d.Stunden}"))}) - muss fuer eine synchrone Partition identisch sein")
            End If
            If details.Select(Function(d) d.Block).Distinct().Count() > 1 Then
                errors.Add($"parallelverbund {JsonHelpers.PyRepr(verbund)}: unterschiedliche block_length je Fach/Klassenstufe ({String.Join(", ", details.Select(Function(d) $"{d.Gruppe}={d.Block}"))}) - muss fuer eine synchrone Partition identisch sein")
            End If

            ' Ueberschneidende Mitgliedschaft: ein Schueler kann nicht
            ' gleichzeitig zwei Varianten desselben Parallelverbunds
            ' besuchen.
            Dim gesehen As New HashSet(Of String)
            For Each g In mitglieder
                For Each schuelerId In g.MitgliederSchuelerIds
                    If Not gesehen.Add(schuelerId) Then
                        errors.Add($"parallelverbund {JsonHelpers.PyRepr(verbund)}: schueler-id {JsonHelpers.PyRepr(schuelerId)} ist in mehr als einer Gruppe dieses Parallelverbunds Mitglied")
                    End If
                Next
            Next
        Next

        Return errors
    End Function

End Module
