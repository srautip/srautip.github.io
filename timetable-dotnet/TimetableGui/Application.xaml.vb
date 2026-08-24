' Anwendungseinstieg. Bewusst schlank: der Zustand lebt im
' HauptViewModel, nicht hier.
Class Application

    Private Sub Application_DispatcherUnhandledException(sender As Object,
                                                         e As DispatcherUnhandledExceptionEventArgs) _
                                                         Handles Me.DispatcherUnhandledException
        ' Ein unbehandelter Fehler soll die Anwendung nicht wortlos
        ' beenden - der Nutzer arbeitet an ungespeicherten Schuldaten.
        MessageBox.Show($"Unerwarteter Fehler:{vbLf}{vbLf}{e.Exception.Message}",
                        "Schulplanung", MessageBoxButton.OK, MessageBoxImage.Error)
        e.Handled = True
    End Sub

End Class
