' Das Datenmodell der Projektablage (Stufe C des GUI-Unterbaus, siehe
' docs/gui-datenhaltung-konzept.md 5.1).
'
' Grundsatz aus dem Konzept: "Die Projektdatei erfindet kein neues
' Datenmodell - sie buendelt die vorhandenen Wire-Formate plus die drei
' neuen Teile (mapping, audit-log, gui-state)." Entsprechend sind
' Stammdatenbestand, Constraint-JsonObjects, KlassenbildungInput und
' RunConfig hier UNVERAENDERT eingebettet; neu sind nur Manifest, Mapping,
' Audit-Log, GUI-Zustand und die Ergebnis-Staende.
Imports System.Text.Json.Nodes
Imports TimetableCore
Imports TimetableYaml

''' <summary>Kopfdaten der Projektdatei. `SchemaVersion` traegt die
''' Schema-Evolution (Konzept 5.1: neue Felder tolerieren beide Seiten per
''' Reflection, nur echte Umbrueche brauchen eine Migrationsfunktion).</summary>
Public NotInheritable Class ProjektManifest
    Public Property SchemaVersion As Integer = 1
    Public Property AppVersion As String = ""
    Public Property SchulName As String = ""
    Public Property Schuljahr As String = ""
    Public Property Angelegt As DateTimeOffset
    Public Property Geaendert As DateTimeOffset
    ''' <summary>Naechste zu vergebende Schueler-ID. Zaehlt nur AUFWAERTS -
    ''' eine geloeschte ID bleibt verbrannt, damit alte Audit-Log-Eintraege
    ''' nie auf ein anderes Kind zeigen koennen (Konzept 6.1).</summary>
    Public Property NaechsteSchuelerNummer As Integer = 1
    ''' <summary>Obergrenze gespeicherter Staende; geschuetzte Staende
    ''' zaehlen mit, werden aber nie verdraengt (Konzept 5.2).</summary>
    Public Property MaxStaende As Integer = 10
End Class

''' <summary>Ein Eintrag der Klarnamen-Tabelle - der EINZIGE Ort im
''' Projekt mit Schuelernamen (Konzept 6.1). `Hinweis` ist bewusst hier
''' angesiedelt und verlaesst die Datei nie.</summary>
Public NotInheritable Class MappingEintrag
    Public Property Id As String = ""
    Public Property Nachname As String = ""
    Public Property Vorname As String = ""
    Public Property Hinweis As String
End Class

''' <summary>Eine Zeile des Art.-22-Nachweises (Konzept 7.3). Aus Sicht der
''' Anwendung append-only: Eintraege werden nie geaendert oder entfernt -
''' auch dann nicht, wenn der zugehoerige Stand geloescht wird.</summary>
Public NotInheritable Class AuditEintrag
    Public Property Zeitpunkt As DateTimeOffset
    ''' <summary>Die eine benannte Nutzerin des Projekts (A5: ein Rechner,
    ''' eine Person) - beim Anlegen erfragt.</summary>
    Public Property Benutzer As String = ""
    ''' <summary>Kurzschluessel, z.B. "lauf", "variantenwahl", "fixierung",
    ''' "freigabe", "klarnamen-export".</summary>
    Public Property Aktion As String = ""
    ''' <summary>Menschenlesbare Zusammenfassung - pseudonymisiert.</summary>
    Public Property Beschreibung As String = ""
End Class

''' <summary>Ein gesicherter Ergebnisstand unter `ergebnisse/`.</summary>
Public NotInheritable Class ProjektStand
    ''' <summary>Ordnername im Container, z.B. "2026-08-20-entwurf-1".</summary>
    Public Property Id As String = ""
    Public Property Label As String = ""
    Public Property Erstellt As DateTimeOffset
    ''' <summary>Freigabe- und Bestands-Staende sind gegen automatisches
    ''' Verdraengen UND gegen Loeschen geschuetzt (Konzept 5.2/8.2).</summary>
    Public Property Geschuetzt As Boolean
    ''' <summary>lauf.json: Parameter, Zeitstempel, Statuszeile - zugleich
    ''' der "Solver-Lauf"-Eintrag des Audit-Logs.</summary>
    Public Property Lauf As JsonObject
    Public Property Stundenplan As JsonObject
    Public Property Klassenbildung As JsonObject
End Class

''' <summary>Der vollstaendige Inhalt einer .splanx-Datei.</summary>
Public NotInheritable Class Projekt
    Public Property Manifest As New ProjektManifest()
    Public Property Bestand As New Stammdatenbestand()
    Public Property Constraints As New List(Of JsonObject)
    Public Property Klassenbildung As New KlassenbildungInput()
    Public Property Config As New RunConfig()
    Public Property Mapping As New List(Of MappingEintrag)
    Public Property AuditLog As New List(Of AuditEintrag)
    Public Property GuiState As JsonObject
    Public Property Staende As New List(Of ProjektStand)

    ''' <summary>Vergibt die naechste Schueler-ID im Format S001. Zaehlt
    ''' den Manifest-Zaehler hoch und gibt ihn NIE wieder frei - siehe
    ''' ProjektManifest.NaechsteSchuelerNummer.</summary>
    Public Function NeueSchuelerId() As String
        Dim nummer = Manifest.NaechsteSchuelerNummer
        Manifest.NaechsteSchuelerNummer = nummer + 1
        Return "S" & nummer.ToString("D3")
    End Function

    ''' <summary>Loescht einen Schueler samt Mapping-Eintrag und allen
    ''' Mitgliedschaften. Die ID bleibt verbrannt (der Zaehler wird nicht
    ''' zurueckgesetzt); Art. 17 verlangt physische Loeschung, die der
    ''' naechste Speichervorgang durch das vollstaendige Neuschreiben der
    ''' Datei erbringt (Konzept 7.4).</summary>
    Public Sub SchuelerLoeschen(id As String)
        Mapping.RemoveAll(Function(m) m.Id = id)
        Klassenbildung.Schueler.RemoveAll(Function(s) s.Id = id)
        Klassenbildung.Fixierungen.RemoveAll(Function(f) f.Kind = id)
        For Each g In Klassenbildung.Gruppen
            g.Mitglieder.RemoveAll(Function(m) m = id)
        Next
        For Each w In Klassenbildung.Wuensche
            w.Kinder.RemoveAll(Function(k) k = id)
        Next
        Bestand.Schueler.RemoveAll(Function(s) s.Id = id)
        For Each g In Bestand.Gruppen
            g.MitgliederSchuelerIds.RemoveAll(Function(m) m = id)
        Next
    End Sub

    ''' <summary>Klarnamen-Anzeige fuer die Oberflaeche ("Mia Muster
    ''' (S001)"). Ohne Mapping-Eintrag - etwa bei anonymen Platzhaltern,
    ''' die bewusst KEINEN bekommen - bleibt es bei der reinen ID.</summary>
    Public Function Anzeigename(id As String) As String
        Dim m = Mapping.FirstOrDefault(Function(x) x.Id = id)
        If m Is Nothing Then Return id
        Dim name = $"{m.Vorname} {m.Nachname}".Trim()
        If name.Length = 0 Then Return id
        Return $"{name} ({id})"
    End Function

    ''' <summary>Entfernt die Klarnamen-Tabelle vollstaendig. Das Projekt
    ''' bleibt danach uneingeschraenkt rechenbar - genau die Zusage aus
    ''' Konzept 6.1, die die Aufbewahrungs-Empfehlung ueberhaupt erst
    ''' praktikabel macht.</summary>
    Public Sub MappingLoeschen()
        Mapping.Clear()
    End Sub

    ''' <summary>Haengt einen Stand an und verdraengt danach die aeltesten
    ''' UNGESCHUETZTEN, bis die Obergrenze eingehalten ist. Liefert die
    ''' verdraengten Stand-Ids - die Oberflaeche kann sie melden, statt
    ''' still zu loeschen.</summary>
    Public Function StandHinzufuegen(stand As ProjektStand) As List(Of String)
        Staende.Add(stand)
        Dim verdraengt As New List(Of String)
        Dim grenze = Math.Max(Manifest.MaxStaende, 1)
        While Staende.Count > grenze
            Dim opfer = Staende.Where(Function(s) Not s.Geschuetzt).
                                OrderBy(Function(s) s.Erstellt).FirstOrDefault()
            ' Nur geschuetzte Staende uebrig: die Grenze tritt zurueck.
            ' Lieber ueber der Obergrenze liegen als eine Freigabe
            ' wegwerfen.
            If opfer Is Nothing Then Exit While
            Staende.Remove(opfer)
            verdraengt.Add(opfer.Id)
        End While
        Return verdraengt
    End Function

    ''' <summary>Loescht einen Stand. Geschuetzte Staende (Freigabe,
    ''' Bestand) bleiben stehen und liefern False. Die zugehoerige
    ''' Audit-Log-Zeile bleibt in JEDEM Fall erhalten (Konzept 7.3).</summary>
    Public Function StandLoeschen(id As String) As Boolean
        Dim stand = Staende.FirstOrDefault(Function(s) s.Id = id)
        If stand Is Nothing OrElse stand.Geschuetzt Then Return False
        Staende.Remove(stand)
        Return True
    End Function

    Public Sub Protokolliere(benutzer As String, aktion As String, beschreibung As String, zeitpunkt As DateTimeOffset)
        AuditLog.Add(New AuditEintrag With {
            .Zeitpunkt = zeitpunkt, .Benutzer = benutzer, .Aktion = aktion, .Beschreibung = beschreibung})
    End Sub
End Class
