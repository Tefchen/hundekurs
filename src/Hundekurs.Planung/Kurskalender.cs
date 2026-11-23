namespace Hundekurs.Planung;

/// <summary>Termine eines Kursjahres.</summary>
public sealed record Kurstermine(
    int Jahr,
    DateOnly Ostersonntag,
    IReadOnlyList<DateOnly> Gehorsam,
    IReadOnlyList<DateOnly> Training,
    IReadOnlyList<DateOnly> Apportierarbeit);

/// <summary>
/// Erzeugt die Termine eines Kursjahres:
/// Teil 1 (Gehorsam) ab dem 1. Sonntag im März über 12 Wochen, Ostersonntag fällt aus;
/// Teil 2 (jagdlich) direkt im Anschluss über 12 Wochen, sonntags (samstags Vorbereitung), Feiertage werden durchtrainiert;
/// Apportierarbeit an 12 Mittwochen, beginnend am Mittwoch vor dem ersten Schweißsonntag
/// (sonst fiele der letzte Termin zu kurz vor die Prüfung).
/// </summary>
public static class Kurskalender
{
    /// <summary>Ostersonntag nach dem gregorianischen Kalender (anonymer Algorithmus nach Meeus/Jones/Butcher).</summary>
    public static DateOnly Ostersonntag(int jahr)
    {
        var a = jahr % 19;
        var b = jahr / 100;
        var c = jahr % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var monat = (h + l - 7 * m + 114) / 31;
        var tag = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(jahr, monat, tag);
    }

    public static DateOnly ErsterSonntagImMaerz(int jahr)
    {
        var d = new DateOnly(jahr, 3, 1);
        return d.AddDays(((int)DayOfWeek.Sunday - (int)d.DayOfWeek + 7) % 7);
    }

    public static Kurstermine Erzeugen(int jahr, int wochenTeil1 = 12, int wochenTeil2 = 12, int apportiertermine = 12)
    {
        var start = ErsterSonntagImMaerz(jahr);
        var ostern = Ostersonntag(jahr);

        var gehorsam = Enumerable.Range(0, wochenTeil1)
            .Select(w => start.AddDays(7 * w))
            .Where(d => d != ostern)
            .ToList();

        var startTeil2 = start.AddDays(7 * wochenTeil1);
        var training = Enumerable.Range(0, wochenTeil2).Select(w => startTeil2.AddDays(7 * w)).ToList();
        var apportier = Enumerable.Range(0, apportiertermine).Select(w => startTeil2.AddDays(7 * w - 4)).ToList();

        return new Kurstermine(jahr, ostern, gehorsam, training, apportier);
    }
}
