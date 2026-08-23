' Phase 2.18c: YAML-Persistenz fuer Stammdatenbestand - bewusst NUR hier
' im TimetableYaml-Projekt, nicht in TimetableCore selbst (haelt dessen
' Abhaengigkeitsoberflaeche minimal, siehe arc42 Abschnitt 8 "kein
' GUI-Dependency"-Philosophie, hier sinngemaess auf "kein YAML-Dependency
' im Kern" uebertragen). Deserialisiert DIREKT auf die bestehenden
' Stammdaten.vb-Klassen (Stammdatenbestand/Klassenstufe/Fach/
' FachKlassenstufe/Klasse/Raum/Lehrer/FachLehrerZuordnung) - keine neuen
' Modellklassen noetig, YamlDotNet arbeitet ueber Reflection direkt auf
' den bestehenden Public-Property-POCOs.
'
' UnderscoredNamingConvention matched YAML-Keys wie "deputat_sollstunden"
' auf die PascalCase-Property "DeputatSollstunden" - dieselbe Konvention
' wie System.Text.Json.JsonNamingPolicy.SnakeCaseLower, das Stammdaten.vb
' fuer sein JSON-Pendant bereits nutzt (siehe dortiger SerializerOptions-
' Kommentar).
Imports YamlDotNet.Serialization
Imports YamlDotNet.Serialization.NamingConventions
Imports TimetableCore

Public Module YamlStammdaten

    Private ReadOnly Serializer As ISerializer = New SerializerBuilder().
        WithNamingConvention(UnderscoredNamingConvention.Instance).
        Build()

    Private ReadOnly Deserializer As IDeserializer = New DeserializerBuilder().
        WithNamingConvention(UnderscoredNamingConvention.Instance).
        Build()

    Public Function SerializeStammdatenYaml(bestand As Stammdatenbestand) As String
        Return Serializer.Serialize(bestand)
    End Function

    Public Function DeserializeStammdatenYaml(yaml As String) As Stammdatenbestand
        Return Deserializer.Deserialize(Of Stammdatenbestand)(yaml)
    End Function

    Public Sub SaveStammdatenYaml(bestand As Stammdatenbestand, path As String)
        IO.File.WriteAllText(path, SerializeStammdatenYaml(bestand))
    End Sub

    Public Function LoadStammdatenYaml(path As String) As Stammdatenbestand
        Return DeserializeStammdatenYaml(IO.File.ReadAllText(path))
    End Function

End Module
