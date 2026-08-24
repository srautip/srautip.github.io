' Passwortabfrage. Eigenes Fenster statt einer MessageBox, weil WPF keine
' Eingabe mit PasswordBox mitbringt - und Klartext-Eingabe fuer ein
' Passwort, das eine ganze Schuldatei schuetzt, waere die falsche
' Bequemlichkeit.
Class PasswortFenster

    Public ReadOnly Property Passwort As String

    Public Sub New(titel As String, bestaetigen As Boolean)
        InitializeComponent()
        Titelzeile.Text = titel
        WiederholungsBlock.Visibility = If(bestaetigen, Visibility.Visible, Visibility.Collapsed)
        Feld.Focus()
    End Sub

    Private Sub AufOk(sender As Object, e As RoutedEventArgs)
        If Feld.Password.Length = 0 Then
            Zeige("Ein Projekt ohne Passwort ist nicht vorgesehen.")
            Return
        End If
        If WiederholungsBlock.Visibility = Visibility.Visible AndAlso Feld.Password <> Wiederholung.Password Then
            Zeige("Die beiden Eingaben stimmen nicht überein.")
            Return
        End If
        _Passwort = Feld.Password
        DialogResult = True
    End Sub

    Private Sub Zeige(text As String)
        Fehler.Text = text
        Fehler.Visibility = Visibility.Visible
    End Sub

End Class
