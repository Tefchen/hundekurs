using System.Globalization;

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
/// Teil 2 (jagdlich) direkt im Anschluss (oder ab einem gewählten Sonntag) über 12 Wochen, sonntags (samstags Vorbereitung), Feiertage werden durchtrainiert;
/// Apportierarbeit an 12 Mittwochen, beginnend am Mittwoch vor dem ersten Schweißsonntag
/// (sonst fiele der letzte Termin zu kurz vor die Prüfung).
/// </summary>
public static class Kurskalender
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

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

    /// <summary>Üblicher Beginn von Teil 2: direkt nach Teil 1, also der Sonntag nach dem letzten Gehorsamssonntag.</summary>
    public static DateOnly StandardBeginnTeil2(int jahr, int wochenTeil1 = 12) =>
        ErsterSonntagImMaerz(jahr).AddDays(7 * wochenTeil1);

    /// <summary>
    /// Prüft einen gewünschten Beginn der Schweißarbeit. Gibt eine Fehlermeldung zurück oder null, wenn er passt.
    /// </summary>
    public static string? BeginnTeil2Pruefen(DateOnly beginn, DateOnly letzterGehorsam)
    {
        if (beginn.DayOfWeek != DayOfWeek.Sunday)
            return $"Die Schweißarbeit beginnt an einem Sonntag – der {beginn:dd.MM.yyyy} ist ein {beginn.ToString("dddd", Deutsch)}.";
        if (beginn <= letzterGehorsam)
            return $"Die Schweißarbeit kann erst nach dem letzten Gehorsamssonntag ({letzterGehorsam:dd.MM.yyyy}) beginnen.";
        if (beginn.Year != letzterGehorsam.Year)
            return "Die Schweißarbeit muss im selben Jahr wie Teil 1 beginnen.";
        return null;
    }

    /// <summary>
    /// Termine von Teil 2 ab einem Sonntag: Training an aufeinanderfolgenden Sonntagen,
    /// Apportierarbeit an den Mittwochen, beginnend am Mittwoch vor dem ersten Schweißsonntag.
    /// </summary>
    public static (IReadOnlyList<DateOnly> Training, IReadOnlyList<DateOnly> Apportierarbeit) Teil2(
        DateOnly beginn, int wochenTeil2 = 12, int apportiertermine = 12) =>
        (Enumerable.Range(0, wochenTeil2).Select(w => beginn.AddDays(7 * w)).ToList(),
         Enumerable.Range(0, apportiertermine).Select(w => beginn.AddDays(7 * w - 4)).ToList());

    /// <summary>Alle Termine eines Kursjahres. <c>beginnTeil2</c> = erster Schweißsonntag; ohne Angabe direkt im Anschluss an Teil 1.</summary>
    public static Kurstermine Erzeugen(int jahr, int wochenTeil1 = 12, int wochenTeil2 = 12, int apportiertermine = 12, DateOnly? beginnTeil2 = null)
    {
        var start = ErsterSonntagImMaerz(jahr);
        var ostern = Ostersonntag(jahr);

        var gehorsam = Enumerable.Range(0, wochenTeil1)
            .Select(w => start.AddDays(7 * w))
            .Where(d => d != ostern)
            .ToList();

        var startTeil2 = beginnTeil2 ?? StandardBeginnTeil2(jahr, wochenTeil1);
        if (BeginnTeil2Pruefen(startTeil2, gehorsam[^1]) is { } fehler)
            throw new ArgumentException(fehler, nameof(beginnTeil2));

        var (training, apportier) = Teil2(startTeil2, wochenTeil2, apportiertermine);
        return new Kurstermine(jahr, ostern, gehorsam, training, apportier);
    }
}
