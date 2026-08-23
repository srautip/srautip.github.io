' Minimale MVVM-Basis. Bewusst KEIN Framework: das Projekt haelt seine
' Abhaengigkeitsoberflaeche klein (arc42 5.1), und fuer
' INotifyPropertyChanged plus ICommand braucht es keines.
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Windows.Input

Public MustInherit Class Beobachtbar
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Protected Sub Melde(<CallerMemberName> Optional name As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(name))
    End Sub

    ''' <summary>Setzt das Feld und meldet nur bei echter Aenderung.
    ''' Liefert True, wenn sich etwas geaendert hat - so koennen Aufrufer
    ''' Folgemeldungen anhaengen, ohne erneut zu vergleichen.</summary>
    Protected Function Setze(Of T)(ByRef feld As T, wert As T,
                                    <CallerMemberName> Optional name As String = Nothing) As Boolean
        If Equals(feld, wert) Then Return False
        feld = wert
        Melde(name)
        Return True
    End Function
End Class

''' <summary>ICommand ohne Framework. `KannAusfuehren` wird nicht
''' automatisch neu ausgewertet - WPF fragt ueber CommandManager nach,
''' und fuer die wenigen Stellen, an denen das nicht reicht, gibt es
''' MeldeAenderung().</summary>
Public NotInheritable Class Befehl
    Implements ICommand

    Private ReadOnly _ausfuehren As Action(Of Object)
    Private ReadOnly _kannAusfuehren As Func(Of Object, Boolean)

    Public Sub New(ausfuehren As Action(Of Object), Optional kannAusfuehren As Func(Of Object, Boolean) = Nothing)
        _ausfuehren = ausfuehren
        _kannAusfuehren = kannAusfuehren
    End Sub

    Public Custom Event CanExecuteChanged As EventHandler Implements ICommand.CanExecuteChanged
        AddHandler(value As EventHandler)
            AddHandler CommandManager.RequerySuggested, value
        End AddHandler
        RemoveHandler(value As EventHandler)
            RemoveHandler CommandManager.RequerySuggested, value
        End RemoveHandler
        RaiseEvent(sender As Object, e As EventArgs)
        End RaiseEvent
    End Event

    Public Function CanExecute(parameter As Object) As Boolean Implements ICommand.CanExecute
        Return _kannAusfuehren Is Nothing OrElse _kannAusfuehren(parameter)
    End Function

    Public Sub Execute(parameter As Object) Implements ICommand.Execute
        _ausfuehren(parameter)
    End Sub

    Public Shared Sub MeldeAenderung()
        CommandManager.InvalidateRequerySuggested()
    End Sub
End Class
