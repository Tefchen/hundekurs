namespace Hundekurs.Planung;

public enum Schwere
{
    /// <summary>Harte Regel verletzt (rot).</summary>
    Fehler,
    /// <summary>Weiche Regel verletzt (gelb).</summary>
    Warnung,
}

public sealed record Befund(Schwere Schwere, int? TerminId, int? GespannId, string Text);

public sealed record Aenderung(int TerminId, DateOnly Datum, int? GespannId, string Beschreibung);

/// <summary>Ergebnis einer Planänderung (Tausch, Abmeldung, …). Wird erst übernommen, wenn die Kursleitung bestätigt.</summary>
public sealed record Aenderungsvorschlag
{
    public required bool Moeglich { get; init; }
    public string? Grund { get; init; }
    public Saisonplan? Plan { get; init; }
    public IReadOnlyList<Aenderung> Aenderungen { get; init; } = [];
    public IReadOnlyList<Befund> Befunde { get; init; } = [];
    public IReadOnlyList<string> Alternativen { get; init; } = [];

    public static Aenderungsvorschlag Abgelehnt(string grund, IReadOnlyList<string>? alternativen = null) =>
        new() { Moeglich = false, Grund = grund, Alternativen = alternativen ?? [] };
}
