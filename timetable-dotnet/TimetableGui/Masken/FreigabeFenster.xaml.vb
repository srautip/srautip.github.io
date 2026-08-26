' Code-Behind des Freigabe-Dialogs. Reine Verdrahtung; was angezeigt
' wird, entscheidet Freigabe.vb, und ob freigegeben werden darf,
' entscheidet LaeufeViewModel.
Imports System.Windows.Media

Partial Class FreigabeFenster

    Private ReadOnly _vorlage As Freigabevorlage

    Public ReadOnly Property Bestaetigung As Freigabebestaetigung

    Public Sub New(vorlage As Freigabevorlage)
        InitializeComponent()
        _vorlage = vorlage

        Kopf.Text = $"{vorlage.Art} freigeben: {vorlage.Label}"
        Kennzahlen.Text = vorlage.Kennzahlen

        If vorlage.Abweichungen.Count = 0 Then
            Abweichungskopf.Text = "Keine verbleibenden Regelabweichungen."
            Abweichungen.ItemsSource = New List(Of String) From {
                "Alle Regeln sind erfüllt. Es gibt nichts, was hier zu prüfen wäre."}
        Else
            Abweichungskopf.Text = $"{vorlage.Abweichungen.Count} verbleibende Regelabweichung(en) – bitte prüfen:"
            Abweichungen.ItemsSource = vorlage.Abweichungen
        End If

        ' Harte Verstoesse verhindern die Freigabe NICHT - die Entscheidung
        ' gehoert dem Menschen. Sie muessen aber anders aussehen als eine
        ' nicht erfuellte Kann-Regel, sonst gehen sie in der Liste unter.
        If vorlage.HarteVerstoesse > 0 Then
            Abweichungskopf.Foreground = CType(FindResource("farbe-krit-text"), Brush)
            Satzrahmen.BorderBrush = CType(FindResource("farbe-krit-rand"), Brush)
        End If

        Satz.Text = Freigabe.Bestaetigungssatz(vorlage)
        Notizfrage.Text = vorlage.Notizfrage
        PersonFeld.Text = Umgebung.Benutzer
        Pruefe()
    End Sub

    ''' <summary>Der Knopf bleibt aus, solange Haken oder Name fehlen.
    ''' Hier ist das AUSGRAUEN richtig - anders als sonst in dieser
    ''' Anwendung: es gibt keine Erklaerung nachzureichen, der Satz
    ''' daneben sagt bereits alles.</summary>
    Private Sub Pruefe()
        Dim notizFehlt = _vorlage.NotizPflicht AndAlso String.IsNullOrWhiteSpace(NotizFeld.Text)
        FreigebenKnopf.IsEnabled = Haken.IsChecked = True AndAlso
                                   Not String.IsNullOrWhiteSpace(PersonFeld.Text) AndAlso
                                   Not notizFehlt
    End Sub

    Private Sub AufEingabe(sender As Object, e As RoutedEventArgs)
        Pruefe()
    End Sub

    Private Sub AufFreigeben(sender As Object, e As RoutedEventArgs)
        _Bestaetigung = New Freigabebestaetigung With {
            .Person = PersonFeld.Text.Trim(), .Bestaetigt = True,
            .Notiz = NotizFeld.Text.Trim()}
        DialogResult = True
    End Sub

End Class
