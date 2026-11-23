namespace Hundekurs.Planung;

public enum Ampel
{
    /// <summary>Quote sicher erreicht.</summary>
    Gruen,
    /// <summary>Noch erreichbar.</summary>
    Gelb,
    /// <summary>Nicht mehr erreichbar.</summary>
    Rot,
}

/// <summary>Anwesenheit eines Gespanns in Teil 1 und daraus folgende Zulassung zu Teil 2.</summary>
public sealed record Zulassungsstand(int GespannId, int Anwesend, int Stattgefunden, int Ausstehend, bool Ausnahme)
{
    /// <summary>Gesamtzahl der Termine, die bis zum Ende von Teil 1 stattfinden (abgesagte zählen nicht).</summary>
    public int Termine => Stattgefunden + Ausstehend;

    /// <summary>Mindestanzahl Anwesenheiten bezogen auf alle stattfindenden Termine.</summary>
    public int Erforderlich => Zulassungsregel.Erforderlich(Termine);

    public Ampel Ampel =>
        Anwesend >= Erforderlich ? Ampel.Gruen
        : Anwesend + Ausstehend >= Erforderlich ? Ampel.Gelb
        : Ampel.Rot;

    public bool Zugelassen => Ausnahme || (Termine > 0 && Ampel == Ampel.Gruen);

    public string Text => $"{Anwesend} von {Stattgefunden}" + (Ausstehend > 0 ? $" (noch {Ausstehend} offen)" : "");
}

public sealed record Anwaerter(Zulassungsstand Stand, int Rang, bool ImKontingent);

public static class Zulassungsregel
{
    public const int Prozent = 60;

    /// <summary>Mindestanzahl Anwesenheiten: 60 % der Termine, aufgerundet (11 → 7, 10 → 6).</summary>
    public static int Erforderlich(int termine) => (termine * Prozent + 99) / 100;

    /// <summary>
    /// Anwärterliste für Teil 2: zuerst alle Zugelassenen, sortiert nach Anwesenheit (absteigend).
    /// Gleich viele Anwesenheiten ergeben denselben Rang. Die ersten <paramref name="plaetze"/> sind im Kontingent;
    /// bei Gleichstand an der Grenze sind alle Ranggleichen markiert, damit die Kursleitung entscheiden kann.
    /// </summary>
    public static IReadOnlyList<Anwaerter> Anwaerterliste(IEnumerable<Zulassungsstand> staende, int plaetze)
    {
        var sortiert = staende
            .Where(s => s.Zugelassen)
            .OrderByDescending(s => s.Anwesend)
            .ThenBy(s => s.GespannId)
            .ToList();

        var ergebnis = new List<Anwaerter>();
        var grenzAnwesend = sortiert.Count > plaetze ? sortiert[plaetze - 1].Anwesend : int.MinValue;
        for (var i = 0; i < sortiert.Count; i++)
        {
            var rang = i > 0 && sortiert[i].Anwesend == sortiert[i - 1].Anwesend ? ergebnis[i - 1].Rang : i + 1;
            var imKontingent = i < plaetze || sortiert[i].Anwesend == grenzAnwesend;
            ergebnis.Add(new Anwaerter(sortiert[i], rang, imKontingent));
        }
        return ergebnis;
    }
}
