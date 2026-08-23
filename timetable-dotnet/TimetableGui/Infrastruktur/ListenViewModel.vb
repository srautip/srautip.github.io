' Das Grundmuster aller Listen-Dialoge (gui-ui-konzept.md 6):
' "Liste links (sortier-/filterbar), Detailformular rechts, Aktionen
' Neu . Duplizieren . Loeschen . Pruefen".
'
' Einmal hier statt sechzehnmal in den Masken - sonst driften Filter,
' Loeschverhalten und Pruefung zwischen den Dialogen auseinander, und
' genau das ist die Sorte Unterschied, die niemandem auffaellt, bis er
' stoert.
'
' BEWUSST OHNE ICollectionView/CollectionViewSource: die waeren
' DispatcherObject und haetten das Testprojekt an einen UI-Thread
' gebunden. Die gefilterte Sicht ist deshalb eine schlichte abgeleitete
' Sammlung - damit bleibt jede Maske ohne Fenster pruefbar, so wie es
' der Plan fuer Stufe F verlangt ("je Maske ein Test, der ueber das
' ViewModel schreibt").
Imports System.Collections.ObjectModel
Imports TimetableProjekt

''' <summary>Die nicht-generische Sicht auf eine Listenmaske. Das Fenster
''' haelt acht Masken verschiedenen Typs in EINER Aktionsleiste - ohne
''' diese Schnittstelle bliebe nur Reflexion, und die verschiebt jeden
''' Tippfehler von der Uebersetzung in die Laufzeit.</summary>
Public Interface IListenMaske
    Sub Neu()
    Sub Duplizieren()
    Sub Loeschen()
    Function Pruefe() As List(Of String)
    ''' <summary>Anzeigename eines Eintrags fuer Liste und Loeschdialog.
    ''' Bei festen Zuordnungen ist das ein abgeleitetes Tripel und kein
    ''' Feld - deshalb eine Funktion und kein DisplayMemberPath.</summary>
    Function AnzeigeName(eintrag As Object) As String
End Interface

Public MustInherit Class ListenViewModel(Of T As Class)
    Inherits Beobachtbar
    Implements IListenMaske

    Private _auswahl As T
    Private _filter As String = ""

    Protected ReadOnly Dialoge As IDialoge

    Protected Sub New(dialoge As IDialoge)
        Me.Dialoge = dialoge
        NeuBefehl = New Befehl(Sub() Neu())
        DuplizierenBefehl = New Befehl(Sub() Duplizieren(), Function() _auswahl IsNot Nothing)
        LoeschenBefehl = New Befehl(Sub() Loeschen(), Function() _auswahl IsNot Nothing)
        PruefenBefehl = New Befehl(Sub() PruefungZeigen())
    End Sub

    ' ---------------------------------------------------------------
    ' Was die konkrete Maske beisteuert
    ' ---------------------------------------------------------------

    ''' <summary>Die Quelle im Bestand. Wird NICHT kopiert - die Maske
    ''' arbeitet auf dem echten Bestand, damit "Speichern ist immer
    ''' moeglich" (Konzept 1) ohne Zwischenstand funktioniert.</summary>
    Protected MustOverride Function Quelle() As IList(Of T)

    Protected MustOverride Function Erzeuge() As T
    Protected MustOverride Function Kopiere(vorlage As T) As T
    Protected MustOverride Function NameVon(eintrag As T) As String
    Protected MustOverride Sub SetzeName(eintrag As T, name As String)

    ''' <summary>Die passende Validate*-API des Kerns. Die Maske erfindet
    ''' keine eigene Pruefung - "Validieren mit den bestehenden APIs,
    ''' nicht mit UI-Sonderlogik" (Konzept 1).</summary>
    Public MustOverride Function Pruefe() As List(Of String) Implements IListenMaske.Pruefe

    ''' <summary>Was das Loeschen nach sich zieht. Nothing heisst: dieser
    ''' Eintragstyp hat keine Verweise (z.B. eine Klassenstufe).</summary>
    Protected Overridable Function LoeschFolgen(eintrag As T) As AenderungsFolgen
        Return Nothing
    End Function

    ''' <summary>Wird nach jeder Aenderung gerufen, damit die Huelle
    ''' "ungespeicherte Aenderungen" anzeigen kann.</summary>
    Public Event Geaendert As EventHandler

    Protected Sub MeldeAenderung()
        RaiseEvent Geaendert(Me, EventArgs.Empty)
        Melde(NameOf(Anzahl))
        Befehl.MeldeAenderung()
    End Sub

    ' ---------------------------------------------------------------
    ' Liste, Filter, Auswahl
    ' ---------------------------------------------------------------

    Public Function AnzeigeName(eintrag As Object) As String Implements IListenMaske.AnzeigeName
        Dim e = TryCast(eintrag, T)
        Return If(e Is Nothing, "", NameVon(e))
    End Function

    Public ReadOnly Property Eintraege As New ObservableCollection(Of T)

    ''' <summary>Freitextfilter ueber den Namen. Der einzige Filter, den
    ''' das Grundmuster verlangt; typspezifische Filter (Prio, Typ,
    ''' Betroffene - 6.10) ergaenzen die Masken selbst.</summary>
    Public Property Filter As String
        Get
            Return _filter
        End Get
        Set
            If Setze(_filter, If(value, "")) Then Aktualisiere()
        End Set
    End Property

    Public Property Auswahl As T
        Get
            Return _auswahl
        End Get
        Set
            If Setze(_auswahl, value) Then Befehl.MeldeAenderung()
        End Set
    End Property

    Public ReadOnly Property Anzahl As Integer
        Get
            Return Quelle().Count
        End Get
    End Property

    ''' <summary>Baut die sichtbare Liste neu auf. Sortiert wird nach
    ''' Name - die Reihenfolge im Bestand ist Wire-Format-Reihenfolge und
    ''' fuer den Menschen bedeutungslos.</summary>
    Public Sub Aktualisiere()
        Dim vorherige = _auswahl
        Eintraege.Clear()
        For Each e In Quelle().
                Where(Function(x) _filter = "" OrElse
                                  NameVon(x).IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0).
                OrderBy(Function(x) NameVon(x), StringComparer.CurrentCultureIgnoreCase)
            Eintraege.Add(e)
        Next
        ' Auswahl halten, wenn sie noch sichtbar ist - sonst springt der
        ' Dialog bei jedem Tastendruck im Filterfeld weg.
        Auswahl = If(vorherige IsNot Nothing AndAlso Eintraege.Contains(vorherige), vorherige, Eintraege.FirstOrDefault())
        Melde(NameOf(Anzahl))
    End Sub

    ' ---------------------------------------------------------------
    ' Neu . Duplizieren . Loeschen . Pruefen
    ' ---------------------------------------------------------------

    Public ReadOnly Property NeuBefehl As Befehl
    Public ReadOnly Property DuplizierenBefehl As Befehl
    Public ReadOnly Property LoeschenBefehl As Befehl
    Public ReadOnly Property PruefenBefehl As Befehl

    Public Sub Neu() Implements IListenMaske.Neu
        Dim e = Erzeuge()
        SetzeName(e, FreierName(BasisName()))
        Quelle().Add(e)
        Aktualisiere()
        Auswahl = e
        MeldeAenderung()
    End Sub

    Protected Overridable Function BasisName() As String
        Return "Neu"
    End Function

    Public Sub Duplizieren() Implements IListenMaske.Duplizieren
        If _auswahl Is Nothing Then Return
        Dim kopie = Kopiere(_auswahl)
        SetzeName(kopie, FreierName(NameVon(_auswahl)))
        Quelle().Add(kopie)
        Aktualisiere()
        Auswahl = kopie
        MeldeAenderung()
    End Sub

    ''' <summary>"Name (2)", "Name (3)", ... Namen sind Schluessel
    ''' (arc42 8.15) - ein Duplikat mit gleichem Namen waere im
    ''' Wire-Format nicht unterscheidbar, deshalb wird hier gezaehlt
    ''' statt es dem Nutzer zu ueberlassen.</summary>
    Private Function FreierName(basis As String) As String
        Dim vorhanden As New HashSet(Of String)(Quelle().Select(Function(x) NameVon(x)), StringComparer.CurrentCultureIgnoreCase)
        If Not vorhanden.Contains(basis) Then Return basis
        Dim n = 2
        While vorhanden.Contains($"{basis} ({n})")
            n += 1
        End While
        Return $"{basis} ({n})"
    End Function

    ''' <summary>Loeschen zeigt IMMER erst die Folgen (Konzept 7:
    ''' "niemals stilles Verwaisen von Referenzen"). Die Folgen kommen
    ''' aus Bestandspflege, nicht aus einer eigenen Suche hier - sonst
    ''' gaebe es zwei Antworten auf dieselbe Frage.</summary>
    Public Sub Loeschen() Implements IListenMaske.Loeschen
        If _auswahl Is Nothing Then Return
        Dim name = NameVon(_auswahl)
        Dim folgen = LoeschFolgen(_auswahl)

        Dim text As String
        If folgen Is Nothing OrElse folgen.Verweise.Count = 0 Then
            text = $"„{name}" & Chr(34) & " loeschen?"
        Else
            Dim zeilen = folgen.Verweise.Take(12).Select(Function(v) $"  - {v.Bereich}: {v.Beschreibung}")
            Dim rest = folgen.Verweise.Count - 12
            text = $"„{name}" & Chr(34) & $" wird von {folgen.Verweise.Count} Stelle(n) verwendet:" & vbLf &
                   String.Join(vbLf, zeilen) &
                   If(rest > 0, vbLf & $"  … und {rest} weitere", "") & vbLf & vbLf &
                   "Trotzdem loeschen? Die Verweise werden mitentfernt."
        End If

        If Not Dialoge.Frage("Loeschen", text) Then Return
        Entferne(_auswahl)
        Aktualisiere()
        MeldeAenderung()
    End Sub

    ''' <summary>Das eigentliche Entfernen. Typen mit Verweisen
    ''' ueberschreiben das und gehen ueber Bestandspflege.Loesche, damit
    ''' die Kaskade laeuft.</summary>
    Protected Overridable Sub Entferne(eintrag As T)
        Quelle().Remove(eintrag)
    End Sub

    Private Sub PruefungZeigen()
        Dim fehler = Pruefe()
        If fehler.Count = 0 Then
            Dialoge.Hinweis("Pruefung", "Keine Beanstandungen.")
        Else
            Dialoge.Hinweis("Pruefung", String.Join(vbLf, fehler.Take(20)) &
                            If(fehler.Count > 20, vbLf & $"… und {fehler.Count - 20} weitere", ""))
        End If
    End Sub

    ''' <summary>Umbenennen laeuft ueber Bestandspflege, damit es
    ''' kaskadiert (arc42 8.15). Liefert die Zahl angepasster Verweise
    ''' fuer die Vorschau „12 Verweise werden angepasst".</summary>
    Public Overridable Function BenenneUm(alt As String, neu As String) As Integer
        Return 0
    End Function

End Class
