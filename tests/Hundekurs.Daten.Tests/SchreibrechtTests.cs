using Hundekurs.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hundekurs.Daten.Tests;

/// <summary>Angemeldeter Benutzer: Stempel in den Daten und Sperre für reines Leserecht.</summary>
public sealed class SchreibrechtTests : IDisposable
{
    private readonly SqliteConnection _verbindung;

    public SchreibrechtTests()
    {
        _verbindung = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _verbindung.Open();
        using var db = Kontext();
        Datenbank.Vorbereiten(db);
    }

    public void Dispose()
    {
        Benutzerkontext.Aktuell = null;
        _verbindung.Dispose();
    }

    // Ohne „Benutzer = …“, damit der Wert aus dem Benutzerkontext kommt.
    private HundekursDb Kontext() => new(new DbContextOptionsBuilder<HundekursDb>().UseSqlite(_verbindung).Options);

    [Fact]
    public void Ohne_Anmeldung_gilt_der_Betriebssystem_Benutzer()
    {
        Benutzerkontext.Aktuell = null;
        using var db = Kontext();
        Assert.Equal(Environment.UserName, db.Benutzer);
    }

    [Fact]
    public void Bearbeiten_darf_speichern_und_wird_eingetragen()
    {
        Benutzerkontext.Aktuell = new Sitzungsbenutzer("Franziska", DarfBearbeiten: true);
        using (var db = Kontext())
        {
            db.Rassen.Add(new Rasse { Kuerzel = "DD", Bezeichnung = "Deutsch Drahthaar" });
            db.SaveChanges();
        }
        using (var db = Kontext())
            Assert.Equal("Franziska", db.Rassen.Single(r => r.Kuerzel == "DD").ErstelltVon);
    }

    [Fact]
    public void Lesen_darf_keine_Kursdaten_aendern()
    {
        Benutzerkontext.Aktuell = new Sitzungsbenutzer("Gast", DarfBearbeiten: false);
        using var db = Kontext();
        db.Rassen.Add(new Rasse { Kuerzel = "BGS", Bezeichnung = "Bayerischer Gebirgsschweißhund" });
        var fehler = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Contains("Leserecht", fehler.Message);
    }

    [Fact]
    public async Task Lesen_darf_eigenes_Konto_aendern()
    {
        Benutzerkontext.Aktuell = new Sitzungsbenutzer("Gast", DarfBearbeiten: false);
        await using var db = Kontext();
        db.Users.Add(new Benutzerkonto { UserName = "gast", NormalizedUserName = "GAST", Anzeigename = "Gast" });
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.Users.CountAsync());
    }
}
