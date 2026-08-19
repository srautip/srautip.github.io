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
            ElseIf l.Anrechnungsstunden >= l.DeputatSollstunden AndAlso l.DeputatSollstunden > 0 Then
                errors.Add($"lehrkraefte[{i}] (name={JsonHelpers.PyRepr(l.Name)}): Anrechnungsstunden ({l.Anrechnungsstunden}) darf nicht das gesamte Deputat ({l.DeputatSollstunden}) aufzehren")
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

        Return errors
    End Function

End Module
