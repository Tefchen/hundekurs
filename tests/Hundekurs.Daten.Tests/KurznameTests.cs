using Hundekurs.Domain;

namespace Hundekurs.Daten.Tests;

public class KurznameTests
{
    [Theory]
    [InlineData("Maximilian", "Mustermann", "Maximilian M.")]
    [InlineData("Erika", "von der Heide", "Erika H.")]
    [InlineData("Erika", "", "Erika")]
    [InlineData("", "Mustermann", "M.")]
    public void Vorname_und_abgekuerzter_Nachname(string vorname, string nachname, string erwartet) =>
        Assert.Equal(erwartet, new Person { Vorname = vorname, Nachname = nachname }.Kurzname);
}
