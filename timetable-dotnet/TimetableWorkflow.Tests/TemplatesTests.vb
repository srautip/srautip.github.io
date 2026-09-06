' Der Klassenteiler ist ein VORSCHLAG fuer den Klassenrahmen der
' Klassenbildung - anders als TemplateFuer darf er nie werfen, auch nicht
' bei fehlender Schulart oder fremdem Bundesland.
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports TimetableWorkflow

<TestClass>
Public Class TemplatesTests

    <TestMethod>
    Public Sub DerKlassenteilerFolgtDerSchulartUndWirftNie()
        Assert.AreEqual(28, Templates.Klassenteiler("BW", "Grundschule"))
        Assert.AreEqual(28, Templates.Klassenteiler("BW", "Gemeinschaftsschule"))
        Assert.AreEqual(30, Templates.Klassenteiler("BW", "Realschule"))
        Assert.AreEqual(30, Templates.Klassenteiler("BW", "Gymnasium"))
        Assert.AreEqual(28, Templates.Klassenteiler("BW", Nothing), "ohne Schulart der Grundschulwert")
        Assert.AreEqual(28, Templates.Klassenteiler("BY", "Grundschule"), "fremdes Bundesland: kein Fehler")
    End Sub

End Class
