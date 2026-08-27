' Klarnamen-Export (gui-ui-konzept.md, Stufe G) - die EINZIGE
' gekennzeichnete Ausnahme von der Pseudonymitaets-Grenze.
'
' Ueberall sonst gilt: Ids verlassen die Projektdatei, Klarnamen nicht.
' Das eingebettete JSON der Viewer ist pseudonym, der YAML-Export ist es,
' die Berichte sind es. Hier nicht - und deshalb steht der Weg unter zwei
' Bedingungen, die der Plan ausdruecklich nennt: Warndialog UND
' Audit-Eintrag.
'
' Der Audit-Eintrag ist der wichtigere von beiden. Ein Warndialog schuetzt
' niemanden; er sorgt nur dafuer, dass die Entscheidung bewusst faellt.
' Nachvollziehbar wird sie erst durch das Protokoll - wer wann welche
' Namen ausgeleitet hat.
Imports TimetableProjekt

Public Module Klarnamenexport

    ''' <summary>Der Warntext. Er NENNT die Zahl der Namen und den Zweck
    ''' der Grenze, statt allgemein vor "personenbezogenen Daten" zu
    ''' warnen - eine Warnung, die man nicht nachrechnen kann, wird
    ''' weggeklickt.</summary>
    Public Function Warnung(anzahl As Integer) As String
        Return $"Diese Datei enthält {anzahl} Klarnamen im Klartext." & vbLf & vbLf &
               "Überall sonst verlässt nur die pseudonyme Id das Projekt – die Zuordnung " &
               "Id zu Kind bleibt in der verschlüsselten Projektdatei. Eine CSV-Datei ist " &
               "unverschlüsselt und wandert erfahrungsgemäß in Mail-Anhänge und " &
               "Netzlaufwerke." & vbLf & vbLf &
               "Der Export wird im Projekt protokolliert." & vbLf & vbLf &
               "Trotzdem exportieren?"
    End Function

    ''' <summary>Die Zeilen der Datei. Semikolon, weil Excel im deutschen
    ''' Gebietsschema danach trennt; ein Komma-CSV landet dort in einer
    ''' einzigen Spalte und wird von Hand nachbearbeitet - womit die
    ''' Datei noch einmal mehr herumliegt.</summary>
    Public Function Zeilen(projekt As Projekt) As List(Of String)
        Dim liste As New List(Of String) From {"id;nachname;vorname;klasse"}
        If projekt Is Nothing Then Return liste

        ' Die Klasse steht nur da, wenn eine Fixierung sie festhaelt -
        ' das Ergebnis eines Laufes ist ein STAND, kein Stammdatum, und
        ' hier ohne Zutun des Nutzers zu raten waere falsch.
        Dim klassen As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
        For Each f In projekt.Klassenbildung.Fixierungen
            If f.Kind IsNot Nothing AndAlso f.Klasse.HasValue Then klassen(f.Kind) = f.Klasse.Value
        Next

        For Each m In projekt.Mapping.OrderBy(Function(x) x.Id, StringComparer.Ordinal)
            Dim klasse = ""
            Dim n = 0
            If klassen.TryGetValue(m.Id, n) Then klasse = n.ToString()
            liste.Add(String.Join(";", {Feld(m.Id), Feld(m.Nachname), Feld(m.Vorname), Feld(klasse)}))
        Next
        Return liste
    End Function

    ''' <summary>CSV-Feld: Anfuehrungszeichen verdoppeln, und alles, was
    ''' Trenner oder Zeilenumbruch enthaelt, einfassen. Ein Name wie
    ''' "Meier; Anna" zerlegte die Datei sonst an der falschen Stelle -
    ''' derselbe Fall, der beim LESEN schon einmal zugeschlagen hat.</summary>
    Private Function Feld(wert As String) As String
        Dim w = If(wert, "")
        If w.IndexOfAny({";"c, """"c, ControlChars.Cr, ControlChars.Lf}) < 0 Then Return w
        Return """" & w.Replace("""", """""") & """"
    End Function

    Public Function Protokollzeile(anzahl As Integer, pfad As String) As String
        Return $"Klarnamen-Export: {anzahl} Name(n) nach {IO.Path.GetFileName(pfad)} " &
               "(Ausnahme von der Pseudonymitaets-Grenze, aktiv bestaetigt)"
    End Function

End Module
