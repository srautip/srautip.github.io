' Werkzeug fuer die wenigen Tests, die ein WPF-Fenster AUFBAUEN.
'
' Das Testprojekt laeuft sonst headless (siehe Kopf von
' DesignKanonTests.vb) und soll es bleiben. Genau deshalb bleibt eine
' Fehlerklasse unsichtbar: ein `StaticResource`-Schluessel, den es nicht
' gibt, oder ein `FindResource` im Code-Behind ist kein Compilerfehler
' und knallt erst beim Aufbau. Fenster, die ihre Maske zur LAUFZEIT
' bauen - die Regelmasken (F3) und der Projekt-Assistent (F5) - haengen
' davon besonders ab.
'
' Hier steht deshalb genau das Noetige: ein STA-Thread und ein einmal
' geladenes Application-Dictionary.
Imports System.Threading
Imports System.Windows
Imports Microsoft.VisualStudio.TestTools.UnitTesting

Friend Module Fensterprobe

    ''' <summary>Fuehrt `tat` auf einem STA-Thread aus und wirft eine dort
    ''' aufgetretene Ausnahme HIER erneut. Ohne das Weiterreichen wuerde
    ''' der Test gruen, waehrend der Thread still stirbt.</summary>
    Friend Sub AufSta(tat As Action)
        Dim fehler As Exception = Nothing
        Dim t As New Thread(Sub()
                                Try
                                    tat()
                                Catch ex As Exception
                                    fehler = ex
                                End Try
                            End Sub)
        t.SetApartmentState(ApartmentState.STA)
        t.Start()
        ' 90 s: der Aufbau dauert Sekundenbruchteile; die Grenze faengt
        ' nur ein haengendes WPF, nicht langsame Rechner.
        Assert.IsTrue(t.Join(TimeSpan.FromSeconds(90)), "Der STA-Thread haengt.")
        If fehler IsNot Nothing Then
            Throw New AssertFailedException("Fensteraufbau fehlgeschlagen: " & fehler.ToString(), fehler)
        End If
    End Sub

    ''' <summary>Die Anwendungsressourcen EINMAL laden. `Application` ist
    ''' pro AppDomain ein Singleton - ein zweites `New` wuerfe.</summary>
    Friend Sub RessourcenSicherstellen()
        If System.Windows.Application.Current IsNot Nothing Then Return
        Dim app As New TimetableGui.Application()
        app.InitializeComponent()
    End Sub

    ''' <summary>Wurzel des `tests/`-Verzeichnisses, von der Testbinaerdatei
    ''' aus aufwaerts gesucht.</summary>
    Friend Function TestsWurzel() As String
        Dim dir = New IO.DirectoryInfo(AppContext.BaseDirectory)
        While dir IsNot Nothing
            If IO.Directory.Exists(IO.Path.Combine(dir.FullName, "tests", "bw-grundschule-beispiel")) Then
                Return IO.Path.Combine(dir.FullName, "tests")
            End If
            dir = dir.Parent
        End While
        Throw New InvalidOperationException("tests/-Verzeichnis nicht gefunden")
    End Function

End Module
