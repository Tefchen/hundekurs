namespace Hundekurs.Planung;

/// <summary>
/// Eine Fahrt eines Ausbilders zu einem Einsatz (einfache Strecke, Hin- und Rückweg werden verrechnet).
/// <paramref name="Id"/> und <paramref name="PersonId"/> dienen dazu, Doppelrollen zu erkennen.
/// </summary>
public sealed record Fahrt(DateOnly Datum, double? KmEinfachBerechnet, double? KmEinfachKorrigiert, int PersonId = 0, int Id = 0)
{
    /// <summary>Maßgebliche einfache Strecke: die Korrektur hat Vorrang.</summary>
    public double? KmEinfach => KmEinfachKorrigiert ?? KmEinfachBerechnet;

    /// <summary>Hin- und Rückweg.</summary>
    public double KmGesamt => Math.Round(2 * (KmEinfach ?? 0), 1);
}

public static class Kilometergeld
{
    public const decimal StandardSatz = 0.30m;

    public static decimal Betrag(double km, decimal satzProKm) => Math.Round((decimal)km * satzProKm, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Fahrten, die erstattet werden: je Person und Tag nur eine. An einem Tag gibt es nur einen Termin;
    /// wer dort zwei Rollen hat (z. B. Ausbilder und Prüfer), fährt trotzdem nur einmal. Gezählt wird die längste Strecke.
    /// </summary>
    public static IReadOnlyList<Fahrt> Erstattet(IEnumerable<Fahrt> fahrten) =>
        fahrten.GroupBy(f => (f.PersonId, f.Datum))
            .Select(g => g.OrderByDescending(f => f.KmEinfach ?? 0).ThenBy(f => f.Id).First())
            .ToList();

    /// <summary>Summe der erstatteten Fahrten (Doppelrollen am selben Tag zählen einmal).</summary>
    public static (double Km, decimal Betrag) Summe(IEnumerable<Fahrt> fahrten, decimal satzProKm)
    {
        var km = Math.Round(Erstattet(fahrten).Sum(f => f.KmGesamt), 1);
        return (km, Betrag(km, satzProKm));
    }
}
