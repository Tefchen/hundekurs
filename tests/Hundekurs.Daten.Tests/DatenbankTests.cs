using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hundekurs.Daten.Tests;

/// <summary>Schemaprüfung beim Start: veraltete Dateien werden erkannt.</summary>
public sealed class DatenbankTests : IDisposable
{
    private readonly string _pfad = Path.Combine(Path.GetTempPath(), $"hundekurs-test-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_pfad)) File.Delete(_pfad);
    }

    private HundekursDb Kontext() =>
        new(new DbContextOptionsBuilder<HundekursDb>().UseSqlite(Datenbank.Verbindung(_pfad)).Options) { Benutzer = "test" };

    /// <summary>Simuliert eine Datei aus einem älteren Programmstand: Tabelle „Kurse“ ohne die neueren Spalten.</summary>
    private void AlteDateiAnlegen()
    {
        using var verbindung = new SqliteConnection(Datenbank.Verbindung(_pfad));
        verbindung.Open();
        using var befehl = verbindung.CreateCommand();
        befehl.CommandText = "CREATE TABLE Kurse (Id INTEGER PRIMARY KEY, Bezeichnung TEXT)";
        befehl.ExecuteNonQuery();
    }

    [Fact]
    public void NeueDatei_PasstZumModell()
    {
        using var db = Kontext();
        Assert.Null(Datenbank.Vorbereiten(db));
        Assert.Empty(Datenbank.FehlendeSpalten(db));
    }

    [Fact]
    public void AlteDatei_EchteDaten_BrichtMitMeldungAb()
    {
        AlteDateiAnlegen();
        using var db = Kontext();
        Assert.Contains("Kurse.KmSatz", Datenbank.FehlendeSpalten(db));
        // Mit oder ohne Migrationen: echte Daten werden nie automatisch gelöscht.
        var fehler = Assert.Throws<InvalidOperationException>(() => Datenbank.Vorbereiten(db, demodaten: false));
        Assert.Contains("Sicherungen", fehler.Message);
        Assert.True(File.Exists(_pfad));
    }

    [Fact]
    public void AlteDatei_Demodaten_WirdNeuAngelegt()
    {
        AlteDateiAnlegen();
        using (var db = Kontext())
            Assert.NotNull(Datenbank.Vorbereiten(db, demodaten: true));
        using (var db = Kontext())
            Assert.Empty(Datenbank.FehlendeSpalten(db));
    }
}
