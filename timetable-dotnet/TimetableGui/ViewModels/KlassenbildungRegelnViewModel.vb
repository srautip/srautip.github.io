' Gruppen, Balance, Wuensche und Fixierungen der Klassenbildung
' (gui-ui-konzept.md 6.11, dritter und vierter Punkt) - Stufe F6.
'
' Nachgezogen: F4 hat den Klassenrahmen und die Einschulungsliste
' gebaut, die Regeln aber nur ANGEZEIGT. Wer Gruppen oder Wuensche
' pflegen wollte, musste an der YAML-Datei vorbei - in einer Oberflaeche,
' deren Zweck es ist, genau das zu ersparen.
'
' Alle vier folgen dem Grundmuster aus ListenViewModel. Zwei Eigenheiten
' gegenueber den Stammdaten-Masken:
'
'   * Nur die Gruppe hat einen NAMEN (ihre Id). Balance, Wunsch und
'     Fixierung sind durch ihren Inhalt bestimmt; ihr Listentext ist
'     deshalb abgeleitet und `SetzeName` bewusst wirkungslos.
'   * Kinder erscheinen ueberall als Klarname, gespeichert wird die Id.
'     Die Aufloesung laeuft ueber KlassenbildungEingabeViewModel, also
'     ueber mapping.json - das eingebettete JSON und jeder Export bleiben
'     pseudonym (Datenhaltung 6.1/6.2).
Imports TimetableCore
Imports TimetableProjekt

Public MustInherit Class KbRegelBasis(Of T As Class)
    Inherits ListenViewModel(Of T)

    Protected ReadOnly Eingabe As KlassenbildungInput
    Protected ReadOnly Kinder As KlassenbildungEingabeViewModel

    Protected Sub New(projekt As Projekt, dialoge As IDialoge,
                      kinderModell As KlassenbildungEingabeViewModel)
        MyBase.New(dialoge)
        Eingabe = projekt.Klassenbildung
        Kinder = kinderModell
    End Sub

    ''' <summary>Alle vier Masken pruefen mit DERSELBEN Kern-API. Eine
    ''' eigene Pruefung je Maske waere eine zweite Meinung darueber, was
    ''' ein gueltiges Regelwerk ist (Konzept 1).</summary>
    Public Overrides Function Pruefe() As List(Of String)
        Return Klassenbildung.ValidateKlassenbildung(Eingabe)
    End Function

    ''' <summary>Id -> "Mia Meier (S001)". Fuer Auswahllisten und
    ''' Listentexte; gespeichert wird immer die Id.</summary>
    Public Function Anzeige(id As String) As String
        Return Kinder.Anzeigename(id)
    End Function

    Public Function AlleKinder() As List(Of KlassenbildungSchueler)
        Return Eingabe.Schueler.OrderBy(Function(s) Anzeige(s.Id),
                                        StringComparer.CurrentCultureIgnoreCase).ToList()
    End Function

    Protected Function KinderText(ids As IEnumerable(Of String), hoechstens As Integer) As String
        Dim liste = ids.ToList()
        If liste.Count = 0 Then Return "(niemand)"
        Dim sichtbar = liste.Take(hoechstens).Select(AddressOf Anzeige)
        Return String.Join(", ", sichtbar) &
               If(liste.Count > hoechstens, $" … (+{liste.Count - hoechstens})", "")
    End Function
End Class

' ===================================================================
' Gruppen
' ===================================================================

Public NotInheritable Class KbGruppenViewModel
    Inherits KbRegelBasis(Of KlassenbildungGruppe)

    Public Sub New(projekt As Projekt, dialoge As IDialoge, kinderModell As KlassenbildungEingabeViewModel)
        MyBase.New(projekt, dialoge, kinderModell)
        Aktualisiere()
    End Sub

    Public Shared ReadOnly Typen As String() = {"buendelung", "verteilung"}

    Protected Overrides Function Quelle() As IList(Of KlassenbildungGruppe)
        Return Eingabe.Gruppen
    End Function

    ''' <summary>Die Vorgaben stammen aus dem YAML-Default, nicht aus
    ''' Geschmack: "dieselben Defaults wie das YAML (soft, Prio 2)".
    ''' Eine Maske, die haerter voreinstellt als die Datei, erzeugt
    ''' Regelwerke, die niemand so gemeint hat.</summary>
    Protected Overrides Function Erzeuge() As KlassenbildungGruppe
        Return New KlassenbildungGruppe With {.Typ = "buendelung", .Modus = "soft", .Prio = 2}
    End Function

    Protected Overrides Function Kopiere(vorlage As KlassenbildungGruppe) As KlassenbildungGruppe
        Return New KlassenbildungGruppe With {
            .Typ = vorlage.Typ, .MaxProKlasse = vorlage.MaxProKlasse, .MinProKlasse = vorlage.MinProKlasse,
            .Modus = vorlage.Modus, .Prio = vorlage.Prio, .Kuerzel = vorlage.Kuerzel,
            .Mitglieder = New List(Of String)(vorlage.Mitglieder)}
    End Function

    Protected Overrides Function NameVon(eintrag As KlassenbildungGruppe) As String
        Return If(eintrag.Id, "")
    End Function

    Protected Overrides Sub SetzeName(eintrag As KlassenbildungGruppe, name As String)
        eintrag.Id = name
    End Sub

    Protected Overrides Function BasisName() As String
        Return "G_neu"
    End Function

    Public Function Zeilentext(g As KlassenbildungGruppe) As String
        If g Is Nothing Then Return ""
        Return $"{If(g.Kuerzel, g.Id)} · {g.Typ} · {g.Modus}/P{g.Prio} · " &
               $"{g.Mitglieder.Count} Kind(er)" &
               If(g.MaxProKlasse.HasValue, $" · max {g.MaxProKlasse.Value}", "") &
               If(g.MinProKlasse.HasValue, $" · min {g.MinProKlasse.Value}", "")
    End Function

    Public Function IstMitglied(g As KlassenbildungGruppe, id As String) As Boolean
        Return g IsNot Nothing AndAlso g.Mitglieder.Contains(id)
    End Function

    Public Sub SetzeMitglied(g As KlassenbildungGruppe, id As String, drin As Boolean)
        If g Is Nothing Then Return
        If drin Then
            If Not g.Mitglieder.Contains(id) Then g.Mitglieder.Add(id)
        Else
            g.Mitglieder.Remove(id)
        End If
        MeldeAenderung()
    End Sub
End Class

' ===================================================================
' Balance
' ===================================================================

Public NotInheritable Class KbBalanceViewModel
    Inherits KbRegelBasis(Of KlassenbildungBalance)

    Public Sub New(projekt As Projekt, dialoge As IDialoge, kinderModell As KlassenbildungEingabeViewModel)
        MyBase.New(projekt, dialoge, kinderModell)
        Aktualisiere()
    End Sub

    Protected Overrides Function Quelle() As IList(Of KlassenbildungBalance)
        Return Eingabe.Balance
    End Function

    Protected Overrides Function Erzeuge() As KlassenbildungBalance
        ' Attribut und Wert bleiben leer: eine Balance auf ein geratenes
        ' Attribut waere schlimmer als eine unvollstaendige, weil sie
        ' unbemerkt wirkt.
        Return New KlassenbildungBalance With {.Toleranz = 0, .Modus = "soft", .Prio = 2}
    End Function

    Protected Overrides Function Kopiere(vorlage As KlassenbildungBalance) As KlassenbildungBalance
        Return New KlassenbildungBalance With {
            .Attribut = vorlage.Attribut, .Wert = vorlage.Wert,
            .Toleranz = vorlage.Toleranz, .Modus = vorlage.Modus, .Prio = vorlage.Prio}
    End Function

    ''' <summary>Eine Balance hat kein Namensfeld - ihr Listentext ist
    ''' abgeleitet. `SetzeName` bleibt deshalb wirkungslos; Neu und
    ''' Duplizieren funktionieren trotzdem, sie vergeben nur keinen
    ''' Namen.</summary>
    Protected Overrides Function NameVon(eintrag As KlassenbildungBalance) As String
        Return $"{If(eintrag.Attribut, "?")}={If(eintrag.Wert, "?")}"
    End Function

    Protected Overrides Sub SetzeName(eintrag As KlassenbildungBalance, name As String)
    End Sub

    Public Function Zeilentext(b As KlassenbildungBalance) As String
        If b Is Nothing Then Return ""
        Return $"{NameVon(b)} · ±{b.Toleranz} · {b.Modus}/P{b.Prio}"
    End Function

    Public Function Attributnamen() As List(Of String)
        Return Kinder.Attributnamen
    End Function

    Public Function Werte(attribut As String) As List(Of String)
        If String.IsNullOrEmpty(attribut) Then Return New List(Of String)
        Return Kinder.Werte(attribut)
    End Function
End Class

' ===================================================================
' Wuensche
' ===================================================================

Public NotInheritable Class KbWuenscheViewModel
    Inherits KbRegelBasis(Of KlassenbildungWunsch)

    Public Sub New(projekt As Projekt, dialoge As IDialoge, kinderModell As KlassenbildungEingabeViewModel)
        MyBase.New(projekt, dialoge, kinderModell)
        Aktualisiere()
    End Sub

    Public Shared ReadOnly Typen As String() = {"zusammen", "getrennt"}

    Protected Overrides Function Quelle() As IList(Of KlassenbildungWunsch)
        Return Eingabe.Wuensche
    End Function

    ''' <summary>Prio 1 statt 2 - auch das ist der YAML-Default. Wuensche
    ''' wiegen leichter als Gruppen und Balance.</summary>
    Protected Overrides Function Erzeuge() As KlassenbildungWunsch
        Return New KlassenbildungWunsch With {.Typ = "zusammen", .Modus = "soft", .Prio = 1}
    End Function

    Protected Overrides Function Kopiere(vorlage As KlassenbildungWunsch) As KlassenbildungWunsch
        Return New KlassenbildungWunsch With {
            .Typ = vorlage.Typ, .Modus = vorlage.Modus, .Prio = vorlage.Prio,
            .Kinder = New List(Of String)(vorlage.Kinder)}
    End Function

    Protected Overrides Function NameVon(eintrag As KlassenbildungWunsch) As String
        Return $"{If(eintrag.Typ, "?")}: {KinderText(eintrag.Kinder, 2)}"
    End Function

    Protected Overrides Sub SetzeName(eintrag As KlassenbildungWunsch, name As String)
    End Sub

    Public Function Zeilentext(w As KlassenbildungWunsch) As String
        If w Is Nothing Then Return ""
        Return $"{NameVon(w)} · {w.Modus}/P{w.Prio}"
    End Function

    ''' <summary>Ein Wunsch verbindet GENAU ZWEI Kinder - der Paar-Picker
    ''' aus 6.11. Das Modell erlaubt mehr, der Kern wertet aber Paare;
    ''' die Maske bietet deshalb zwei Auswahlfelder und nicht eine
    ''' Mehrfachliste, die etwas verspricht, was nicht eingeloest wird.</summary>
    Public Sub SetzeKind(w As KlassenbildungWunsch, position As Integer, id As String)
        If w Is Nothing OrElse position < 0 OrElse position > 1 Then Return
        While w.Kinder.Count < 2
            w.Kinder.Add(Nothing)
        End While
        w.Kinder(position) = id
        MeldeAenderung()
    End Sub

    Public Function Kind(w As KlassenbildungWunsch, position As Integer) As String
        If w Is Nothing OrElse position >= w.Kinder.Count Then Return Nothing
        Return w.Kinder(position)
    End Function
End Class

' ===================================================================
' Fixierungen
' ===================================================================

Public NotInheritable Class KbFixierungenViewModel
    Inherits KbRegelBasis(Of KlassenbildungFixierung)

    Private ReadOnly _projekt As Projekt

    Public Sub New(projekt As Projekt, dialoge As IDialoge, kinderModell As KlassenbildungEingabeViewModel)
        MyBase.New(projekt, dialoge, kinderModell)
        _projekt = projekt
        Aktualisiere()
    End Sub

    Protected Overrides Function Quelle() As IList(Of KlassenbildungFixierung)
        Return Eingabe.Fixierungen
    End Function

    Protected Overrides Function Erzeuge() As KlassenbildungFixierung
        Return New KlassenbildungFixierung()
    End Function

    Protected Overrides Function Kopiere(vorlage As KlassenbildungFixierung) As KlassenbildungFixierung
        Return New KlassenbildungFixierung With {
            .Kind = vorlage.Kind, .Klasse = vorlage.Klasse, .NichtKlasse = vorlage.NichtKlasse}
    End Function

    Protected Overrides Function NameVon(eintrag As KlassenbildungFixierung) As String
        Dim ziel = If(eintrag.Klasse.HasValue, $"Klasse {eintrag.Klasse}",
                      If(eintrag.NichtKlasse.HasValue, $"NICHT Klasse {eintrag.NichtKlasse}", "(ohne Ziel)"))
        Return $"{Anzeige(eintrag.Kind)} → {ziel}"
    End Function

    Protected Overrides Sub SetzeName(eintrag As KlassenbildungFixierung, name As String)
    End Sub

    Public Function Zeilentext(f As KlassenbildungFixierung) As String
        Return NameVon(f)
    End Function

    ''' <summary>Was 6.11 als "Herkunft aus dem Audit-Log" verlangt - und
    ''' zwar ehrlich: das Protokoll fuehrt Uebernahmen aus dem Board als
    ''' SAMMELZEILE ("12 Fixierungen uebernommen"), nicht je Kind. Eine
    ''' Herkunft je Zeile vorzugaukeln waere eine Erfindung; stattdessen
    ''' steht hier die juengste einschlaegige Protokollzeile.</summary>
    Public Function HerkunftHinweis() As String
        Dim zeile = _projekt.AuditLog.
            Where(Function(e) e.Aktion = "fixierung").
            OrderByDescending(Function(e) e.Zeitpunkt).FirstOrDefault()
        If zeile Is Nothing Then
            Return "Keine Übernahme aus dem Board protokolliert – diese Fixierungen " &
                   "stammen aus der Projektdatei oder wurden hier angelegt."
        End If
        Return $"Zuletzt aus dem Board übernommen am {zeile.Zeitpunkt:dd.MM.yyyy HH:mm} " &
               $"({zeile.Benutzer}): {zeile.Beschreibung}"
    End Function

    Public Sub SetzeZiel(f As KlassenbildungFixierung, klasse As Integer?, nichtKlasse As Integer?)
        If f Is Nothing Then Return
        ' Klasse und NichtKlasse schliessen einander aus - beides
        ' gleichzeitig waere eine Fixierung, die sich selbst widerspricht.
        f.Klasse = klasse
        f.NichtKlasse = If(klasse.HasValue, Nothing, nichtKlasse)
        MeldeAenderung()
    End Sub
End Class
