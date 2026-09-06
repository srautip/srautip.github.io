' Klassenbildung (K1, siehe docs/klassenbildung-plan.md): YAML-Persistenz
' fuer KlassenbildungInput - wie YamlStammdaten bewusst NUR hier im
' TimetableYaml-Projekt ("kein YAML-Dependency im Kern").
' UnderscoredNamingConvention matched z.B. "max_pro_klasse" auf
' MaxProKlasse und "nicht_klasse" auf NichtKlasse.
Imports YamlDotNet.Serialization
Imports YamlDotNet.Serialization.NamingConventions
Imports TimetableCore

Public Module YamlKlassenbildung

    Private ReadOnly Deserializer As IDeserializer = New DeserializerBuilder().
        WithNamingConvention(UnderscoredNamingConvention.Instance).
        Build()

    ''' <summary>OmitNull statt aller Defaults: `Nothing` heisst bei den
    ''' Optional-Feldern (MaxProKlasse, MinProKlasse, Stufe, Klasse/NichtKlasse) "nicht
    ''' gesetzt", und genau diese Bedeutung geht verloren, wenn sie als
    ''' `null` in die Datei geschrieben werden. Nicht-nullable Felder mit
    ''' Default (Modus "soft", Prio 2/1) werden dagegen bewusst
    ''' ausgeschrieben - eine exportierte Datei soll ihre wirksamen Werte
    ''' zeigen, statt sie im Code zu verstecken.</summary>
    Private ReadOnly Serializer As ISerializer = New SerializerBuilder().
        WithNamingConvention(UnderscoredNamingConvention.Instance).
        ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).
        Build()

    Public Function DeserializeKlassenbildungYaml(yaml As String) As KlassenbildungInput
        Return Deserializer.Deserialize(Of KlassenbildungInput)(yaml)
    End Function

    Public Function LoadKlassenbildungYaml(path As String) As KlassenbildungInput
        Return DeserializeKlassenbildungYaml(IO.File.ReadAllText(path))
    End Function

    ''' <summary>Gegenstueck zum Laden - der Rueckschreibweg, den die
    ''' Phase-3-GUI fuer den U5-Re-Solve-Loop braucht (Pins und Haertungen
    ''' aus dem Board zurueck in den Bestand, gui-ui-konzept.md 4).</summary>
    Public Function SerializeKlassenbildungYaml(input As KlassenbildungInput) As String
        Return Serializer.Serialize(input)
    End Function

    Public Sub SaveKlassenbildungYaml(input As KlassenbildungInput, path As String)
        IO.File.WriteAllText(path, SerializeKlassenbildungYaml(input))
    End Sub

End Module
