' Lesen und Schreiben der verschluesselten .splanx-Projektdatei
' (docs/gui-datenhaltung-konzept.md 5.1/5.3, Stufe C des GUI-Unterbaus).
'
' Aufbau der Datei:
'
'   Bytes 0..7    Magic "SPLANX01" (ASCII)
'   Bytes 8..11   Laenge des Klartext-Kopfes (Int32, little endian)
'   danach        Klartext-Kopf als JSON: KDF, Iterationszahl, Salt, Nonce
'   danach        16 Byte GCM-Tag
'   Rest          Chiffrat = ein ZIP-Archiv mit JSON-Eintraegen
'
' Der Kopf liegt bewusst UNVERSCHLUESSELT: die Iterationszahl muss lesbar
' sein, BEVOR der Schluessel abgeleitet werden kann, und sie soll spaeter
' erhoehbar sein, ohne alte Dateien unlesbar zu machen (Konzept 5.3). Er
' enthaelt kein Geheimnis - Salt und Nonce sind per Definition oeffentlich.
'
' Alles BCL, keine Krypto-Fremdpakete: Rfc2898DeriveBytes.Pbkdf2 und
' AesGcm. AES-256-GCM ist authentifiziert, ein falsches Passwort oder eine
' manipulierte Datei fallen daher beim Entschluesseln auf und liefern nie
' still falsche Daten.
'
' KEIN Recovery-Pfad: Passwort vergessen = Daten verloren. Das ist die
' bewusste Kehrseite echter Verschluesselung (Nutzerentscheidung, schliesst
' offenen Punkt 1 des Datenhaltungskonzepts) - abgemildert wird sie
' organisatorisch, nicht technisch.
Imports System.IO
Imports System.IO.Compression
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports TimetableCore
Imports TimetableYaml

''' <summary>Die Datei ist keine .splanx-Datei oder hat eine
''' Formatversion, die diese Anwendung nicht kennt.</summary>
Public Class ProjektFormatException
    Inherits Exception
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class

''' <summary>Entschluesseln fehlgeschlagen. AES-GCM kann "falsches
''' Passwort" und "veraenderte Datei" nicht unterscheiden - beides
''' scheitert an derselben Authentizitaetspruefung, und beides ehrlich zu
''' nennen ist besser, als eine der beiden Ursachen zu behaupten.</summary>
Public Class ProjektEntschluesselungException
    Inherits Exception
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class

Public Module ProjektDatei

    Private ReadOnly Magic As Byte() = Encoding.ASCII.GetBytes("SPLANX01")
    Private Const TagLaenge As Integer = 16
    Private Const NonceLaenge As Integer = 12   ' AES-GCM-Standard
    Private Const SaltLaenge As Integer = 16
    Private Const SchluesselLaenge As Integer = 32  ' AES-256

    ''' <summary>Startwert laut Konzept 5.3. Bewusst als Untergrenze
    ''' formuliert und im Dateikopf mitgefuehrt, damit er spaeter erhoeht
    ''' werden kann, ohne bestehende Dateien zu entwerten.</summary>
    Public Const StandardIterationen As Integer = 600000

    Private ReadOnly JsonOptionen As New JsonSerializerOptions With {
        .WriteIndented = True,
        .PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    }

    ' --- Eintragsnamen im Container ---------------------------------
    Private Const EManifest As String = "manifest.json"
    Private Const EStammdaten As String = "stammdaten.json"
    Private Const EConstraints As String = "constraints.json"
    Private Const EKlassenbildung As String = "klassenbildung.json"
    Private Const EConfig As String = "config.json"
    Private Const EMapping As String = "mapping.json"
    Private Const EAudit As String = "audit-log.json"
    Private Const EGuiState As String = "gui-state.json"
    Private Const ErgebnisPraefix As String = "ergebnisse/"

    ''' <summary>Schreibt das Projekt verschluesselt nach `pfad`.
    '''
    ''' ATOMAR (Konzept-Anforderung A9): serialisieren -> ZIP im Speicher ->
    ''' verschluesseln -> in eine Temp-Datei IM SELBEN ORDNER schreiben ->
    ''' File.Replace. Ein Absturz mitten im Speichern hinterlaesst damit die
    ''' alte, intakte Datei statt einer halben neuen. Die Temp-Datei liegt
    ''' bewusst neben dem Ziel und nicht in %TEMP%: gleicher Datentraeger
    ''' (File.Replace ist sonst kein Rename), gleiche
    ''' Verschluesselungssituation, kein Klartext-Umweg ueber ein fremdes
    ''' Verzeichnis.</summary>
    Public Sub Speichern(projekt As Projekt, pfad As String, passwort As String,
                          Optional iterationen As Integer = StandardIterationen)
        If projekt Is Nothing Then Throw New ArgumentNullException(NameOf(projekt))
        If String.IsNullOrEmpty(passwort) Then Throw New ArgumentException("Ein Projekt ohne Passwort waere unverschluesselt - das sieht das Konzept nicht vor.", NameOf(passwort))

        Dim klartext = BaueZip(projekt)

        Dim salt = RandomNumberGenerator.GetBytes(SaltLaenge)
        Dim nonce = RandomNumberGenerator.GetBytes(NonceLaenge)
        Dim schluessel = SchluesselAbleiten(passwort, salt, iterationen)

        Dim chiffrat(klartext.Length - 1) As Byte
        Dim tag(TagLaenge - 1) As Byte
        Using gcm As New AesGcm(schluessel, TagLaenge)
            gcm.Encrypt(nonce, klartext, chiffrat, tag)
        End Using
        CryptographicOperations.ZeroMemory(schluessel)

        Dim kopf As New JsonObject From {
            {"kdf", "PBKDF2-SHA256"},
            {"iterationen", iterationen},
            {"salt", Convert.ToBase64String(salt)},
            {"nonce", Convert.ToBase64String(nonce)}
        }
        Dim kopfBytes = Encoding.UTF8.GetBytes(kopf.ToJsonString())

        Dim ordner = IO.Path.GetDirectoryName(IO.Path.GetFullPath(pfad))
        Directory.CreateDirectory(ordner)
        Dim temp = IO.Path.Combine(ordner, IO.Path.GetFileName(pfad) & ".tmp-" & Guid.NewGuid().ToString("N").Substring(0, 8))
        Try
            Using fs = File.Create(temp)
                fs.Write(Magic, 0, Magic.Length)
                fs.Write(BitConverter.GetBytes(kopfBytes.Length), 0, 4)
                fs.Write(kopfBytes, 0, kopfBytes.Length)
                fs.Write(tag, 0, tag.Length)
                fs.Write(chiffrat, 0, chiffrat.Length)
                fs.Flush(flushToDisk:=True)
            End Using

            If File.Exists(pfad) Then
                ' Ohne Backup-Datei; File.Replace ist auch so atomar und
                ' behaelt bei einem Fehler das Original.
                File.Replace(temp, pfad, Nothing)
            Else
                File.Move(temp, pfad)
            End If
        Finally
            If File.Exists(temp) Then File.Delete(temp)
        End Try
    End Sub

    ''' <summary>Liest eine .splanx-Datei. Wirft
    ''' ProjektEntschluesselungException bei falschem Passwort ODER
    ''' veraenderter Datei (GCM unterscheidet das nicht) und
    ''' ProjektFormatException bei fremdem Format.</summary>
    Public Function Laden(pfad As String, passwort As String) As Projekt
        Dim roh = File.ReadAllBytes(pfad)
        If roh.Length < Magic.Length + 4 Then Throw New ProjektFormatException($"{pfad}: zu kurz fuer eine Projektdatei.")
        For i = 0 To Magic.Length - 1
            If roh(i) <> Magic(i) Then
                Throw New ProjektFormatException($"{pfad}: keine Projektdatei dieser Anwendung (oder eine neuere Formatversion).")
            End If
        Next

        Dim kopfLaenge = BitConverter.ToInt32(roh, Magic.Length)
        Dim kopfStart = Magic.Length + 4
        If kopfLaenge <= 0 OrElse kopfStart + kopfLaenge + TagLaenge > roh.Length Then
            Throw New ProjektFormatException($"{pfad}: Dateikopf beschaedigt.")
        End If

        Dim kopf = JsonNode.Parse(Encoding.UTF8.GetString(roh, kopfStart, kopfLaenge)).AsObject()
        Dim iterationen = kopf("iterationen").GetValue(Of Integer)()
        Dim salt = Convert.FromBase64String(kopf("salt").GetValue(Of String)())
        Dim nonce = Convert.FromBase64String(kopf("nonce").GetValue(Of String)())

        Dim tagStart = kopfStart + kopfLaenge
        Dim chiffratStart = tagStart + TagLaenge
        Dim tag(TagLaenge - 1) As Byte
        Array.Copy(roh, tagStart, tag, 0, TagLaenge)
        Dim chiffrat(roh.Length - chiffratStart - 1) As Byte
        Array.Copy(roh, chiffratStart, chiffrat, 0, chiffrat.Length)

        Dim schluessel = SchluesselAbleiten(passwort, salt, iterationen)
        Dim klartext(chiffrat.Length - 1) As Byte
        Try
            Using gcm As New AesGcm(schluessel, TagLaenge)
                gcm.Decrypt(nonce, chiffrat, tag, klartext)
            End Using
        Catch ex As CryptographicException
            Throw New ProjektEntschluesselungException(
                $"{pfad} konnte nicht entschluesselt werden - falsches Passwort oder die Datei wurde veraendert.")
        Finally
            CryptographicOperations.ZeroMemory(schluessel)
        End Try

        Return LiesZip(klartext)
    End Function

    ''' <summary>Prueft ohne Entschluesselung, ob die Datei ueberhaupt eine
    ''' Projektdatei ist - fuer den Oeffnen-Dialog, damit die
    ''' Passwortabfrage nicht fuer eine beliebige Fremddatei erscheint.</summary>
    Public Function IstProjektdatei(pfad As String) As Boolean
        If Not File.Exists(pfad) Then Return False
        Dim puffer(Magic.Length - 1) As Byte
        Using fs = File.OpenRead(pfad)
            If fs.Read(puffer, 0, puffer.Length) < puffer.Length Then Return False
        End Using
        Return puffer.SequenceEqual(Magic)
    End Function

    Private Function SchluesselAbleiten(passwort As String, salt As Byte(), iterationen As Integer) As Byte()
        Return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passwort), salt, iterationen, HashAlgorithmName.SHA256, SchluesselLaenge)
    End Function

    ' ---------------------------------------------------------------
    ' Container-Inhalt
    ' ---------------------------------------------------------------

    Private Function BaueZip(projekt As Projekt) As Byte()
        Using ms As New MemoryStream()
            Using zip As New ZipArchive(ms, ZipArchiveMode.Create, leaveOpen:=True)
                SchreibeEintrag(zip, EManifest, JsonSerializer.Serialize(projekt.Manifest, JsonOptionen))
                ' Bewusst ueber Stammdaten.SerializeStammdaten statt eigener
                ' Serialisierung: identisch zur bestehenden JSON-Datei, damit
                ' Import/Export und der CLI-Kanal dasselbe Format sehen.
                SchreibeEintrag(zip, EStammdaten, Stammdaten.SerializeStammdaten(projekt.Bestand))
                SchreibeEintrag(zip, EConstraints,
                    New JsonArray(projekt.Constraints.Select(Function(c) CType(c.DeepClone(), JsonNode)).ToArray()).
                        ToJsonString(JsonOptionen))
                SchreibeEintrag(zip, EKlassenbildung, JsonSerializer.Serialize(projekt.Klassenbildung, JsonOptionen))
                SchreibeEintrag(zip, EConfig, JsonSerializer.Serialize(projekt.Config, JsonOptionen))
                SchreibeEintrag(zip, EMapping, JsonSerializer.Serialize(projekt.Mapping, JsonOptionen))
                SchreibeEintrag(zip, EAudit, JsonSerializer.Serialize(projekt.AuditLog, JsonOptionen))
                If projekt.GuiState IsNot Nothing Then
                    SchreibeEintrag(zip, EGuiState, projekt.GuiState.ToJsonString(JsonOptionen))
                End If

                For Each stand In projekt.Staende
                    Dim basis = ErgebnisPraefix & stand.Id & "/"
                    Dim kopf As New JsonObject From {
                        {"id", stand.Id}, {"label", stand.Label},
                        {"erstellt", stand.Erstellt.ToString("o")},
                        {"geschuetzt", stand.Geschuetzt}
                    }
                    If stand.Lauf IsNot Nothing Then kopf("lauf") = stand.Lauf.DeepClone()
                    SchreibeEintrag(zip, basis & "lauf.json", kopf.ToJsonString(JsonOptionen))
                    If stand.Stundenplan IsNot Nothing Then
                        SchreibeEintrag(zip, basis & "stundenplan.json", stand.Stundenplan.ToJsonString(JsonOptionen))
                    End If
                    If stand.Klassenbildung IsNot Nothing Then
                        SchreibeEintrag(zip, basis & "klassenbildung.json", stand.Klassenbildung.ToJsonString(JsonOptionen))
                    End If
                Next
            End Using
            Return ms.ToArray()
        End Using
    End Function

    Private Sub SchreibeEintrag(zip As ZipArchive, name As String, inhalt As String)
        Using w As New StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), New UTF8Encoding(False))
            w.Write(inhalt)
        End Using
    End Sub

    Private Function LiesZip(bytes As Byte()) As Projekt
        Dim projekt As New Projekt()
        Using ms As New MemoryStream(bytes, writable:=False)
            Using zip As New ZipArchive(ms, ZipArchiveMode.Read)
                Dim staende As New Dictionary(Of String, ProjektStand)

                For Each eintrag In zip.Entries
                    Dim text = Lies(eintrag)
                    Select Case eintrag.FullName
                        Case EManifest
                            projekt.Manifest = JsonSerializer.Deserialize(Of ProjektManifest)(text, JsonOptionen)
                        Case EStammdaten
                            projekt.Bestand = Stammdaten.DeserializeStammdaten(text)
                        Case EConstraints
                            projekt.Constraints = JsonNode.Parse(text).AsArray().
                                Select(Function(n) CType(n.DeepClone(), JsonObject)).ToList()
                        Case EKlassenbildung
                            projekt.Klassenbildung = JsonSerializer.Deserialize(Of KlassenbildungInput)(text, JsonOptionen)
                        Case EConfig
                            projekt.Config = JsonSerializer.Deserialize(Of RunConfig)(text, JsonOptionen)
                        Case EMapping
                            projekt.Mapping = JsonSerializer.Deserialize(Of List(Of MappingEintrag))(text, JsonOptionen)
                        Case EAudit
                            projekt.AuditLog = JsonSerializer.Deserialize(Of List(Of AuditEintrag))(text, JsonOptionen)
                        Case EGuiState
                            projekt.GuiState = JsonNode.Parse(text).AsObject()
                        Case Else
                            If eintrag.FullName.StartsWith(ErgebnisPraefix, StringComparison.Ordinal) Then
                                LiesStandEintrag(staende, eintrag.FullName, text)
                            End If
                            ' Unbekannte Eintraege werden ignoriert - eine
                            ' aeltere Anwendung darf eine neuere Datei
                            ' oeffnen, ohne an einem Zusatzteil zu
                            ' scheitern (Schema-Evolution, Konzept 5.1).
                    End Select
                Next

                projekt.Staende = staende.Values.OrderBy(Function(s) s.Erstellt).ToList()
            End Using
        End Using
        Return projekt
    End Function

    Private Sub LiesStandEintrag(staende As Dictionary(Of String, ProjektStand), voll As String, text As String)
        Dim rest = voll.Substring(ErgebnisPraefix.Length)
        Dim schraeg = rest.IndexOf("/"c)
        If schraeg <= 0 Then Return
        Dim id = rest.Substring(0, schraeg)
        Dim datei = rest.Substring(schraeg + 1)

        If Not staende.ContainsKey(id) Then staende(id) = New ProjektStand With {.Id = id}
        Dim stand = staende(id)

        Select Case datei
            Case "lauf.json"
                Dim kopf = JsonNode.Parse(text).AsObject()
                stand.Label = TextOder(kopf, "label", id)
                stand.Geschuetzt = kopf.ContainsKey("geschuetzt") AndAlso kopf("geschuetzt").GetValue(Of Boolean)()
                Dim erstellt As DateTimeOffset
                If kopf.ContainsKey("erstellt") AndAlso
                   DateTimeOffset.TryParse(kopf("erstellt").GetValue(Of String)(),
                                           Globalization.CultureInfo.InvariantCulture,
                                           Globalization.DateTimeStyles.RoundtripKind, erstellt) Then
                    stand.Erstellt = erstellt
                End If
                If kopf.ContainsKey("lauf") Then stand.Lauf = kopf("lauf").DeepClone().AsObject()
            Case "stundenplan.json"
                stand.Stundenplan = JsonNode.Parse(text).AsObject()
            Case "klassenbildung.json"
                stand.Klassenbildung = JsonNode.Parse(text).AsObject()
        End Select
    End Sub

    Private Function TextOder(obj As JsonObject, schluessel As String, ersatz As String) As String
        If Not obj.ContainsKey(schluessel) OrElse obj(schluessel) Is Nothing Then Return ersatz
        Return obj(schluessel).GetValue(Of String)()
    End Function

    Private Function Lies(eintrag As ZipArchiveEntry) As String
        Using r As New StreamReader(eintrag.Open(), Encoding.UTF8)
            Return r.ReadToEnd()
        End Using
    End Function

End Module
