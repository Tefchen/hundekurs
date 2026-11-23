namespace Hundekurs.Planung;

/// <summary>
/// Was das angemeldete Konto von anderen Teilnehmenden sehen darf.
/// Mit Bearbeitungsrecht: alles. Sonst nur „Maximilian M.“ und keine Ergebnisse – außer beim eigenen Gespann.
/// </summary>
/// <param name="Vollzugriff">Konto darf bearbeiten (Rolle Bearbeiten oder Verwaltung).</param>
/// <param name="PersonId">Mit dem Konto verknüpfte Person, falls vorhanden.</param>
public sealed record Sicht(bool Vollzugriff, int? PersonId)
{
    /// <summary>Solange die Anmeldung noch nicht ausgewertet ist: so wenig wie möglich zeigen.</summary>
    public static readonly Sicht Eingeschraenkt = new(false, null);

    public bool IstEigenes(Gespann g) => PersonId is { } id && g.PersonId == id;

    /// <summary>Darf alles zu diesem Gespann sehen (vollen Namen, Ergebnisse).</summary>
    public bool Voll(Gespann g) => Vollzugriff || IstEigenes(g);

    public string Fuehrer(Gespann g) => Voll(g) ? g.Fuehrer : g.FuehrerKurz ?? Kuerzen(g.Fuehrer);

    /// <summary>„Fuehrer / Hund“ wie bisher, mit Kurzname wo nötig.</summary>
    public string Name(Gespann g) => g.Hund.Length > 0 ? $"{Fuehrer(g)} / {g.Hund}" : $"Gespann {g.Id}";

    /// <summary>Notlösung, falls kein Kurzname vorliegt: erstes Wort plus Anfangsbuchstabe des letzten.</summary>
    public static string Kuerzen(string vollerName)
    {
        var teile = vollerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return teile.Length switch
        {
            0 => "",
            1 => teile[0],
            _ => $"{teile[0]} {char.ToUpperInvariant(teile[^1][0])}.",
        };
    }
}
