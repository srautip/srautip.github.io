' Anwendungseinstieg. Bewusst schlank: der Zustand lebt im
' HauptViewModel, nicht hier.
Class Application

    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        ' Die Bildprobe wird HIER erkannt, ausgefuehrt aber vom
        ' Hauptfenster nach dem Laden - erst dann gibt es etwas zu
        ' rendern. Ein Fehler in den Argumenten soll den Aufrufer
        ' erreichen, nicht ein Meldungsfenster, das niemand sieht.
        Try
            Bildprobe.Auftrag = Bildprobe.Lesen(e.Args)
        Catch ex As ArgumentException
            Console.Error.WriteLine(ex.Message)
            Shutdown(2)
            Return
        End Try
        MyBase.OnStartup(e)
    End Sub

    Private Sub Application_DispatcherUnhandledException(sender As Object,
                                                         e As DispatcherUnhandledExceptionEventArgs) _
                                                         Handles Me.DispatcherUnhandledException
        If Bildprobe.Auftrag IsNot Nothing Then
            ' Ohne Bediener: den Fehler in den Zielordner schreiben und
            ' mit Fehlercode enden, statt auf ein OK zu warten.
            Try
                IO.Directory.CreateDirectory(Bildprobe.Auftrag.Ordner)
                IO.File.WriteAllText(IO.Path.Combine(Bildprobe.Auftrag.Ordner, "fehler.txt"), e.Exception.ToString())
            Catch
            End Try
            e.Handled = True
            Shutdown(1)
            Return
        End If

        ' Ein unbehandelter Fehler soll die Anwendung nicht wortlos
        ' beenden - der Nutzer arbeitet an ungespeicherten Schuldaten.
        MessageBox.Show($"Unerwarteter Fehler:{vbLf}{vbLf}{e.Exception.Message}",
                        "Schulplanung", MessageBoxButton.OK, MessageBoxImage.Error)
        e.Handled = True
    End Sub

End Class
