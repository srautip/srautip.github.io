' Zerlegt eingefuegten Text in Spalten (gui-ui-konzept.md 6.8/6.11:
' "Einfuegen aus Zwischenablage").
'
' BEWUSST OHNE WPF: `Clipboard.GetText` steht im Fenster, das Zerlegen
' hier. Sonst waere die einzige Stelle, an der sich pruefen liesse, ob
' eine Zeile mit Semikolon im Namen richtig zerfaellt, das laufende
' Fenster - und das ist auf diesem Rechner nicht beobachtbar
' (arc42 8.13).
'
' Was NICHT hierher gehoert: das Erraten der Bedeutung. Welche Spalte
' der Nachname ist, entscheidet die Maske bzw. der Nutzer - ein
' Import, der Spalten selbsttaetig deutet, liegt irgendwann falsch und
' schreibt Klarnamen ins falsche Feld.
Imports System.Text

Public Module ZeilenImport

    ''' <summary>Trennzeichen in der Reihenfolge, in der sie gepruefen
    ''' werden. Tabulator zuerst: wer aus einer Tabellenkalkulation
    ''' kopiert - der mit Abstand haeufigste Fall - bekommt Tabulatoren,
    ''' und Namen wie "Meier, Anna" wuerden bei Komma-Vorrang zerrissen.</summary>
    Private ReadOnly Trenner As Char() = {vbTab(0), ";"c, ","c}

    ''' <summary>Ermittelt das Trennzeichen aus dem Text selbst: gewaehlt
    ''' wird das erste, das in der MEHRHEIT der Zeilen gleich oft
    ''' vorkommt. Ein Semikolon, das nur in einer von vierzig Zeilen
    ''' steht, ist eher Teil eines Namens als eine Spaltengrenze.</summary>
    ''' <summary>Nothing heisst: KEIN Trennzeichen ueberzeugt, der Text
    ''' ist einspaltig.
    '''
    ''' Die erste Fassung fiel hier auf ";" zurueck - und zerriss damit
    ''' ausgerechnet den Ausreisser, den die Mehrheitspruefung eben noch
    ''' geschuetzt hatte ("Clara; die Zweite" in einer Liste blosser
    ''' Vornamen). Ein Rueckfall auf irgendein Zeichen ist schlechter als
    ''' die ehrliche Auskunft "hier gibt es keine Spalten".</summary>
    Public Function Trennzeichen(text As String) As Char?
        Dim zeilen = AlsZeilen(text)
        If zeilen.Count = 0 Then Return Nothing

        For Each t In Trenner
            ' Gewaehlt wird das erste Zeichen, das in der MEHRHEIT der
            ' Zeilen ueberhaupt vorkommt.
            '
            ' Die erste Fassung verlangte dieselbe ANZAHL in der Mehrheit
            ' und war damit zu streng: bei eingefuegten Listen fehlen am
            ' Zeilenende regelmaessig leere Felder ("a;b;c" neben "d;e"),
            ' und dann galt der Text ploetzlich als einspaltig. Fuer die
            ' Ausreisser-Abwehr genuegt das blosse Vorkommen - ein
            ' Semikolon in einer von fuenf Zeilen ist eher Teil eines
            ' Namens als eine Spaltengrenze.
            Dim mitTrenner = zeilen.Where(Function(z) AusserhalbAnfuehrung(z, t) > 0).Count
            If mitTrenner * 2 > zeilen.Count Then Return t
        Next
        Return Nothing
    End Function

    ''' <summary>Zaehlt Vorkommen AUSSERHALB von Anfuehrungszeichen -
    ''' sonst zaehlte `"Meier; Anna";SOZ` als zweispaltig statt
    ''' einspaltig-mit-Semikolon.</summary>
    Private Function AusserhalbAnfuehrung(zeile As String, t As Char) As Integer
        Dim n = 0, inAnfuehrung = False
        For Each c In zeile
            If c = """"c Then
                inAnfuehrung = Not inAnfuehrung
            ElseIf c = t AndAlso Not inAnfuehrung Then
                n += 1
            End If
        Next
        Return n
    End Function

    ''' <summary>Zerlegt EINE Zeile und respektiert dabei
    ''' Anfuehrungszeichen: Tabellenkalkulationen setzen sie um Felder,
    ''' die das Trennzeichen enthalten. Ein naives Split zerbricht genau
    ''' diese Felder - und zwar still.</summary>
    Private Function SpalteAuf(zeile As String, t As Char) As String()
        Dim felder As New List(Of String)
        Dim puffer As New StringBuilder()
        Dim inAnfuehrung = False
        Dim i = 0
        While i < zeile.Length
            Dim c = zeile(i)
            If c = """"c Then
                ' Verdoppeltes Anfuehrungszeichen innerhalb eines
                ' quotierten Feldes ist ein literales Zeichen.
                If inAnfuehrung AndAlso i + 1 < zeile.Length AndAlso zeile(i + 1) = """"c Then
                    puffer.Append(""""c)
                    i += 1
                Else
                    inAnfuehrung = Not inAnfuehrung
                End If
            ElseIf c = t AndAlso Not inAnfuehrung Then
                felder.Add(puffer.ToString())
                puffer.Clear()
            Else
                puffer.Append(c)
            End If
            i += 1
        End While
        felder.Add(puffer.ToString())
        Return felder.Select(Function(f) f.Trim()).ToArray()
    End Function

    Private Function AlsZeilen(text As String) As List(Of String)
        If String.IsNullOrWhiteSpace(text) Then Return New List(Of String)
        Return text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).
            Split(CChar(vbLf)).
            Select(Function(z) z.Trim()).
            Where(Function(z) z <> "").ToList()
    End Function

    ''' <summary>Zerlegt den Text in Zeilen und Spalten. Leere Zeilen
    ''' fallen weg; Felder werden getrimmt und von umschliessenden
    ''' Anfuehrungszeichen befreit (Tabellenkalkulationen setzen sie um
    ''' Felder mit Trennzeichen).
    '''
    ''' Alle Zeilen bekommen dieselbe Spaltenzahl - kuerzere werden mit
    ''' Leerstrings aufgefuellt. Sonst muesste jeder Aufrufer bei jedem
    ''' Zugriff auf die Laenge pruefen, und irgendeiner vergisst es.</summary>
    Public Function Zerlege(text As String, Optional trenner As Char? = Nothing) As List(Of String())
        Dim t = If(trenner.HasValue, trenner, Trennzeichen(text))
        Dim roh = AlsZeilen(text).
            Select(Function(z) If(t.HasValue, SpalteAuf(z, t.Value), {Saeubere(z)})).
            ToList()
        If roh.Count = 0 Then Return roh

        Dim breite = roh.Max(Function(r) r.Length)
        Return roh.Select(Function(r)
                              If r.Length = breite Then Return r
                              Dim voll(breite - 1) As String
                              For i = 0 To breite - 1
                                  voll(i) = If(i < r.Length, r(i), "")
                              Next
                              Return voll
                          End Function).ToList()
    End Function

    Private Function Saeubere(feld As String) As String
        Dim f = If(feld, "").Trim()
        If f.Length >= 2 AndAlso f.StartsWith("""", StringComparison.Ordinal) AndAlso
           f.EndsWith("""", StringComparison.Ordinal) Then
            f = f.Substring(1, f.Length - 2).Replace("""""", """")
        End If
        Return f.Trim()
    End Function

    ''' <summary>Erkennt eine Kopfzeile: sie enthaelt keine Zahl, die
    ''' wie ein Wert aussieht, und unterscheidet sich in ihrer Machart
    ''' von der Folgezeile.
    '''
    ''' BEWUSST NUR EIN VORSCHLAG. Die Maske zeigt ihn als Haekchen an,
    ''' das der Nutzer abwaehlen kann - eine falsch verworfene erste
    ''' Zeile ist ein verlorenes Kind, und das faellt niemandem auf.</summary>
    Public Function SiehtNachKopfzeileAus(zeilen As List(Of String())) As Boolean
        If zeilen Is Nothing OrElse zeilen.Count < 2 Then Return False
        Dim erste = zeilen(0)
        ' Steht in der ersten Zeile irgendwo eine Zahl, ist es eher ein
        ' Datensatz als eine Beschriftung.
        For Each feld In erste
            Dim unbenutzt As Double
            If feld <> "" AndAlso Double.TryParse(feld, Globalization.NumberStyles.Any,
                                                  Globalization.CultureInfo.CurrentCulture, unbenutzt) Then Return False
        Next
        ' Und sie darf sich nicht mit einer Folgezeile decken.
        Return Not zeilen.Skip(1).Any(Function(z) z.SequenceEqual(erste, StringComparer.CurrentCultureIgnoreCase))
    End Function

End Module
