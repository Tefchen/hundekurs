using System.Globalization;

namespace Hundekurs.Planung;

/// <summary>Eine Nennung zur Prüfung, soweit sie für die Platzvergabe zählt.</summary>
/// <param name="Id">Id der Nennung.</param>
/// <param name="AusDemKurs">Das Gespann ist im laufenden Kurs angemeldet (sonst kursfremd).</param>
/// <param name="GenanntAm">Eingang der Nennung; entscheidet die Reihenfolge.</param>
public sealed record Pruefungsnennung(int Id, bool AusDemKurs, DateTimeOffset GenanntAm);

public enum Platzstatus { Startplatz, Warteliste }

/// <summary>Ergebnis der Platzvergabe für eine Nennung. <paramref name="Position"/> zählt ab 1 über Startplätze und Warteliste.</summary>
public sealed record Pruefungsplatz(int NennungId, Platzstatus Status, int Position, bool AusDemKurs);

/// <summary>Besetzung eines Prüfungstags: drei Verbandsprüfer, einer davon Richterobmann, und wer am Vortag beim Legen der Fährten dabei ist.</summary>
public sealed record Richterbesetzung(int? RichterobmannId, int? Richter2Id, int? Richter3Id, IReadOnlyCollection<int> AmVortag)
{
    public IReadOnlyList<int> Richter => new[] { RichterobmannId, Richter2Id, Richter3Id }.OfType<int>().ToList();
}

/// <summary>Personenzahl für die Verpflegung an einem Prüfungstag.</summary>
public sealed record Verpflegung(int Hundefuehrer, int Gaeste, int Pruefer, int Ausbilder, int WeitereHelfer)
{
    public int Gesamt => Hundefuehrer + Gaeste + Pruefer + Ausbilder + WeitereHelfer;
}

/// <summary>
/// Regeln der Brauchbarkeitsprüfung im Kurs:
/// drei Termine nach dem letzten Trainingstag, meist sonntags ab 8:00 Uhr; höchstens sechs Hunde je Tag;
/// drei Verbandsprüfer, einer davon Richterobmann; mindestens ein Prüfer kommt am Vortag zum Legen der Fährten
/// und prüft, ob sie der Prüfungsordnung entsprechen.
/// </summary>
public static class Pruefungsregeln
{
    public const int StandardTermine = 3;
    public const int StandardMaxHunde = 6;
    public const int AnzahlPruefer = 3;
    public static readonly TimeOnly StandardBeginn = new(8, 0);

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Vorschlag: die Sonntage nach dem letzten Trainingstag, Woche für Woche.</summary>
    public static IReadOnlyList<DateOnly> Terminvorschlag(DateOnly letzterTrainingstag, int anzahl = StandardTermine)
    {
        var tage = ((int)DayOfWeek.Sunday - (int)letzterTrainingstag.DayOfWeek + 7) % 7;
        var erster = letzterTrainingstag.AddDays(tage == 0 ? 7 : tage);
        return Enumerable.Range(0, anzahl).Select(w => erster.AddDays(7 * w)).ToList();
    }

    /// <summary>Harte Regel: Geprüft wird erst nach dem letzten Trainingstag. Gibt eine Fehlermeldung zurück oder null.</summary>
    public static string? DatumPruefen(DateOnly datum, DateOnly? letzterTrainingstag) =>
        letzterTrainingstag is DateOnly letzter && datum <= letzter
            ? $"Die Prüfung findet erst nach dem letzten Trainingstag ({letzter:dd.MM.yyyy}) statt."
            : null;

    /// <summary>
    /// Vergibt die Startplätze eines Prüfungstags.
    /// Vorrang haben Gespanne aus dem Kurs, die bis zum Meldeschluss genannt haben (in der Reihenfolge des Eingangs).
    /// Danach kommen alle übrigen nach Eingang: kursfremde Gespanne und Kursteilnehmer, die nach dem Meldeschluss nennen.
    /// Eine spätere Nennung aus dem Kurs verdrängt also bis zum Meldeschluss eine kursfremde; danach rückt niemand mehr vor.
    /// Wer keinen Startplatz hat, steht auf der Warteliste und rückt nach, sobald ein Platz frei wird.
    /// </summary>
    public static IReadOnlyList<Pruefungsplatz> PlaetzeVergeben(IEnumerable<Pruefungsnennung> nennungen, int maxHunde, DateOnly? meldeschluss)
    {
        if (maxHunde < 0) throw new ArgumentOutOfRangeException(nameof(maxHunde));
        var reihenfolge = nennungen
            .OrderBy(n => Vorrang(n, meldeschluss) ? 0 : 1)
            .ThenBy(n => n.GenanntAm)
            .ThenBy(n => n.Id)
            .ToList();
        return reihenfolge
            .Select((n, i) => new Pruefungsplatz(n.Id, i < maxHunde ? Platzstatus.Startplatz : Platzstatus.Warteliste, i + 1, n.AusDemKurs))
            .ToList();
    }

    /// <summary>Nennung aus dem Kurs, eingegangen spätestens am Tag des Meldeschlusses (ohne Meldeschluss immer).</summary>
    public static bool Vorrang(Pruefungsnennung n, DateOnly? meldeschluss) =>
        n.AusDemKurs && (meldeschluss is not DateOnly m || DateOnly.FromDateTime(n.GenanntAm.LocalDateTime) <= m);

    /// <summary>Prüft Datum und Richterbesetzung eines Prüfungstags.</summary>
    public static IReadOnlyList<Befund> Pruefen(int terminId, DateOnly datum, DateOnly? letzterTrainingstag, Richterbesetzung besetzung)
    {
        var befunde = new List<Befund>();
        var tag = $"Prüfung {datum:dd.MM.}";

        if (DatumPruefen(datum, letzterTrainingstag) is { } fehler)
            befunde.Add(new Befund(Schwere.Fehler, terminId, null, $"{tag}: {fehler}"));
        else if (datum.DayOfWeek != DayOfWeek.Sunday)
            befunde.Add(new Befund(Schwere.Warnung, terminId, null, $"{tag}: kein Sonntag, sondern {datum.ToString("dddd", Deutsch)}."));

        var richter = besetzung.Richter;
        if (richter.Distinct().Count() != richter.Count)
            befunde.Add(new Befund(Schwere.Fehler, terminId, null, $"{tag}: Ein Prüfer ist mehrfach eingetragen."));
        if (besetzung.RichterobmannId is null)
            befunde.Add(new Befund(Schwere.Fehler, terminId, null, $"{tag}: Kein Richterobmann eingetragen."));
        var fehlen = AnzahlPruefer - richter.Distinct().Count();
        if (fehlen > 0)
            befunde.Add(new Befund(Schwere.Fehler, terminId, null,
                $"{tag}: Es fehlen noch {fehlen} von {AnzahlPruefer} Verbandsprüfern."));
        if (besetzung.AmVortag.Count == 0)
            befunde.Add(new Befund(Schwere.Fehler, terminId, null,
                $"{tag}: Am Vortag ist noch kein Prüfer eingeteilt, der die Fährten abnimmt."));
        else if (!besetzung.AmVortag.Any(richter.Contains))
            befunde.Add(new Befund(Schwere.Warnung, terminId, null,
                $"{tag}: Der Prüfer am Vortag richtet am Prüfungstag nicht mit."));
        return befunde;
    }
}
