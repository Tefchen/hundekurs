using System.Globalization;
using System.Text;

namespace Hundekurs.Planung;

public sealed record Mailinhalt(string Betreff, string Text);

/// <summary>Erzeugt den Text der wöchentlichen Mail mit dem Plan für Vorbereitungsabend und Trainingstag.</summary>
public static class Wochenmail
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public static Mailinhalt Erstellen(Schweissplaner planer, Termin t, string einleitung, string gruss)
    {
        var revier = t.RevierId is int r ? planer.Revier(r) : null;
        var revierName = revier?.Name ?? "Revier noch offen";
        string N(int id) => planer.Gespann(id) is var g ? $"{g.Fuehrer} / {g.Hund}" : "";

        var betreff = $"Schweißtraining {t.Datum.ToString("dddd, dd.MM.", De)} – Gruppe {t.Gruppe}, {revierName}";

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(einleitung))
        {
            sb.AppendLine(einleitung.TrimEnd());
            sb.AppendLine();
        }

        var vorabend = t.Datum.AddDays(-1);
        sb.AppendLine($"VORBEREITUNG: {vorabend.ToString("dddd, dd.MM.", De)}, {revierName}");
        sb.AppendLine("Helfer (legen die Fährten und gehen am Sonntag als Beobachter mit):");
        foreach (var h in t.Helfer.OrderBy(h => h)) sb.AppendLine($"  - {N(h)}");
        if (t.Helfer.Count == 0) sb.AppendLine("  - noch niemand eingeteilt");
        sb.AppendLine();

        sb.AppendLine($"SCHWEISSTRAINING: {t.Datum.ToString("dddd, dd.MM.", De)}, Gruppe {t.Gruppe}, {revierName}");
        foreach (var seq in t.Zuteilungen.GroupBy(z => z.Sequenz).OrderBy(x => x.Key))
        {
            sb.AppendLine($"  Sequenz {seq.Key}:");
            foreach (var z in seq.OrderBy(z => z.FaehrteNr))
                sb.AppendLine($"    Fährte {z.FaehrteNr}{(z.ZweiterLaufAufFaehrte ? " (zweiter Lauf)" : "")}: {N(z.GespannId)}{(t.Gaeste.Contains(z.GespannId) ? " (holt nach)" : "")}");
        }
        if (t.Abgemeldet.Count > 0)
            sb.AppendLine($"  Abgemeldet: {string.Join(", ", t.Abgemeldet.Order().Select(N))}");
        sb.AppendLine();
        sb.AppendLine($"Gruppe {(t.Gruppe == Gruppe.A ? Gruppe.B : Gruppe.A)} kommt später zu den übrigen Fächern dazu.");

        if (!string.IsNullOrWhiteSpace(gruss))
        {
            sb.AppendLine();
            sb.AppendLine(gruss.TrimEnd());
        }

        return new Mailinhalt(betreff, sb.ToString().Replace("\r\n", "\n"));
    }
}
