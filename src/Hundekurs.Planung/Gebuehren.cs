namespace Hundekurs.Planung;

/// <summary>
/// Preis für eine Kombination der Ausbildungsteile, z. B. „Teil 1“, „Teil 1 + Teil 2“, „Teil 1 + Teil 2 + Apport“.
/// Vereinsmitglieder zahlen den Mitgliederpreis.
/// </summary>
public sealed record Gebuehrenpaket(int Id, string Bezeichnung, bool Gehorsam, bool Schalenwild, bool Niederwild, decimal Preis, decimal Mitgliederpreis)
{
    public bool Passt(Ausbildung a) => a.Gehorsam == Gehorsam && a.Schalenwild == Schalenwild && a.Niederwild == Niederwild;
}

/// <summary>Ergebnis der Gebührenberechnung samt Rechenweg für die Anzeige.</summary>
public sealed record Gebuehr(decimal? Betrag, Gebuehrenpaket? Paket, string Rechenweg)
{
    public bool Ermittelt => Betrag is not null;
}

public static class Gebuehrenregel
{
    /// <summary>
    /// Gebühr eines Gespanns: Preis des passenden Pakets, für Vereinsmitglieder der Mitgliederpreis.
    /// Wiederholer zahlen die Hälfte des für sie gültigen Preises (erst Mitgliederpreis, dann halbieren).
    /// Ein von Hand festgelegter Betrag hat Vorrang.
    /// </summary>
    public static Gebuehr Berechnen(IEnumerable<Gebuehrenpaket> pakete, Ausbildung ausbildung, bool mitglied, bool wiederholer, decimal? vonHand = null, string? hinweis = null)
    {
        if (vonHand is decimal hand)
            return new Gebuehr(hand, null, $"von Hand festgelegt: {Euro(hand)}" + (string.IsNullOrWhiteSpace(hinweis) ? "" : $" ({hinweis.Trim()})"));

        if (ausbildung.Leer)
            return new Gebuehr(null, null, "keine Ausbildung gewählt");

        var paket = pakete.FirstOrDefault(p => p.Passt(ausbildung));
        if (paket is null)
            return new Gebuehr(null, null, $"kein Preis für „{ausbildung.Text}“ hinterlegt");

        var basis = mitglied ? paket.Mitgliederpreis : paket.Preis;
        var weg = $"{paket.Bezeichnung}: {(mitglied ? "Mitgliederpreis" : "Preis")} {Euro(basis)}";
        if (!wiederholer) return new Gebuehr(basis, paket, weg);

        var halb = Math.Round(basis / 2, 2, MidpointRounding.AwayFromZero);
        return new Gebuehr(halb, paket, $"{weg}, Wiederholer: Hälfte = {Euro(halb)}");
    }

    public static string Euro(decimal betrag) => betrag.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-DE")) + " €";
}
