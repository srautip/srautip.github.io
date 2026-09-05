' Der einzige eigene Wertkonverter. Er haengt die Seitenleiste
' ZWEISEITIG an HauptViewModel.Bereich: jeder RadioButton nennt seinen
' Bereich als ConverterParameter, IsChecked ist dann "Bereich = Parameter"
' - und ein Klick setzt den Bereich. Vorher hing an den Schaltern nur ein
' Command; ein Wechsel aus dem Modell (F5 von der Startseite, "Ansehen" aus
' den Laeufen) liess die Markierung auf dem alten Eintrag stehen.
Imports System.Globalization
Imports System.Windows.Data

Public NotInheritable Class BereichGewaehlt
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object,
                            culture As CultureInfo) As Object Implements IValueConverter.Convert
        Return value IsNot Nothing AndAlso value.Equals(parameter)
    End Function

    ''' <summary>Nur das ABWAEHLEN eines RadioButtons (IsChecked=False,
    ''' weil ein anderer gewaehlt wurde) darf den Bereich nicht setzen -
    ''' sonst schriebe der abgewaehlte Schalter seinen Bereich zurueck
    ''' und ueberschriebe den neuen.</summary>
    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object,
                                culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        If TypeOf value Is Boolean AndAlso CBool(value) Then Return parameter
        Return Binding.DoNothing
    End Function
End Class
