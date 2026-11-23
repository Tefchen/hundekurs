namespace Hundekurs.Planung;

/// <summary>Erfundene Daten für Entwicklung und Tests. Enthält keine echten Personen- oder Revierdaten.</summary>
public static class Demodaten
{
    private static readonly string[] Hunde =
    [
        "Arko", "Bella", "Cora", "Dino", "Emma", "Falko", "Greta",
        "Hasso", "Ilka", "Jaro", "Kira", "Lotte", "Moritz", "Nora",
    ];

    public static List<Revier> Reviere() =>
    [
        new() { Id = 1, Name = "Revier Nord", Kuerzel = "N" },
        new() { Id = 2, Name = "Revier Ost", Kuerzel = "O" },
        new() { Id = 3, Name = "Revier Süd", Kuerzel = "S" },
        new() { Id = 4, Name = "Revier West", Kuerzel = "W" },
        new() { Id = 5, Name = "Prüfungsrevier", Kuerzel = "P", Verwendung = Verwendung.Pruefung },
    ];

    /// <summary>Gespanne, abwechselnd auf Gruppe A und B verteilt.</summary>
    public static List<Gespann> Gespanne(int anzahl = 14) =>
        Enumerable.Range(1, anzahl)
            .Select(i => new Gespann(i, $"Hundeführer {i:00}", Hunde[(i - 1) % Hunde.Length], i <= (anzahl + 1) / 2 ? Gruppe.A : Gruppe.B))
            .ToList();

    /// <summary>Erster Sonntag im Juni des angegebenen Jahres.</summary>
    public static DateOnly ErsterSonntagImJuni(int jahr)
    {
        var d = new DateOnly(jahr, 6, 1);
        while (d.DayOfWeek != DayOfWeek.Sunday) d = d.AddDays(1);
        return d;
    }
}
