namespace Hundekurs.Planung.Tests;

public class SichtTests
{
    private static readonly Gespann Maximilian = new(1, "Maximilian Mustermann", "Asta", Gruppe.A, PersonId: 10, FuehrerKurz: "Maximilian M.");
    private static readonly Gespann Erika = new(2, "Erika Beispiel", "Bodo", Gruppe.B, PersonId: 20, FuehrerKurz: "Erika B.");

    [Fact]
    public void Bearbeiten_sieht_volle_Namen()
    {
        var sicht = new Sicht(Vollzugriff: true, PersonId: null);
        Assert.Equal("Maximilian Mustermann", sicht.Fuehrer(Maximilian));
        Assert.True(sicht.Voll(Erika));
    }

    [Fact]
    public void Lesen_sieht_bei_anderen_nur_den_Kurznamen()
    {
        var sicht = new Sicht(Vollzugriff: false, PersonId: 20);
        Assert.Equal("Maximilian M. / Asta", sicht.Name(Maximilian));
        Assert.False(sicht.Voll(Maximilian));
    }

    [Fact]
    public void Lesen_sieht_beim_eigenen_Gespann_alles()
    {
        var sicht = new Sicht(Vollzugriff: false, PersonId: 20);
        Assert.True(sicht.IstEigenes(Erika));
        Assert.Equal("Erika Beispiel", sicht.Fuehrer(Erika));
    }

    [Fact]
    public void Eingeschraenkt_zeigt_niemandem_den_vollen_Namen()
    {
        Assert.Equal("Erika B.", Sicht.Eingeschraenkt.Fuehrer(Erika));
        Assert.False(Sicht.Eingeschraenkt.IstEigenes(new Gespann(3, "Ohne Person", "Cito", Gruppe.A)));
    }

    [Theory]
    [InlineData("Maximilian Mustermann", "Maximilian M.")]
    [InlineData("Anna Maria von der Heide", "Anna H.")]
    [InlineData("Hundeführer 01", "Hundeführer 0.")]
    [InlineData("Solo", "Solo")]
    [InlineData("", "")]
    public void Ersatz_Kuerzung_ohne_Kurzname(string voll, string erwartet) =>
        Assert.Equal(erwartet, Sicht.Kuerzen(voll));
}
