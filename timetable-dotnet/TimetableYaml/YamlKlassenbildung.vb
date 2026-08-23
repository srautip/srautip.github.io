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

    Public Function DeserializeKlassenbildungYaml(yaml As String) As KlassenbildungInput
        Return Deserializer.Deserialize(Of KlassenbildungInput)(yaml)
    End Function

    Public Function LoadKlassenbildungYaml(path As String) As KlassenbildungInput
        Return DeserializeKlassenbildungYaml(IO.File.ReadAllText(path))
    End Function

End Module
