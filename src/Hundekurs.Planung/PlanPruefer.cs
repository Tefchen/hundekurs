using System.Globalization;

namespace Hundekurs.Planung;

/// <summary>Prüft einen Saisonplan gegen die harten (Fehler) und weichen (Warnung) Regeln.</summary>
public sealed class PlanPruefer(Schweissplaner planer)
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    public IReadOnlyList<Befund> Pruefen(Saisonplan plan)
    {
        var befunde = new List<Befund>();
        var termine = plan.Chronologisch.ToList();
        var erstmals = new Dictionary<(int Gespann, int Revier, int Nr), Termin>();

        foreach (var t in termine)
        {
            var tag = t.Datum.ToString("ddd dd.MM.", De);

            if (t.RevierId is not int revierId || !planer.Reviere.Any(r => r.Id == revierId))
            {
                befunde.Add(new(Schwere.Fehler, t.Id, null, $"{tag}: kein Revier verfügbar."));
                continue;
            }

            var revier = planer.Revier(revierId);
            if (!revier.FuerTrainingNutzbar)
                befunde.Add(new(Schwere.Fehler, t.Id, null, $"{tag}: Revier {revier.Name} ist für Trainings nicht nutzbar."));

            PruefeRuhezeit(termine, t, revier, tag, befunde);

            var laeufe = Schweissplaner.GezaehlteLaeufe(t).ToList();
            foreach (var (g, nr) in laeufe)
            {
                if (nr < 1 || nr > revier.AnzahlFaehrten || revier.GesperrteFaehrten.Contains(nr))
                    befunde.Add(new(Schwere.Fehler, t.Id, g, $"{tag}: {Name(g)} ist auf Fährte {nr} eingeteilt, die nicht genutzt werden darf."));

                if (erstmals.TryGetValue((g, revierId, nr), out var frueher))
                    befunde.Add(new(Schwere.Fehler, t.Id, g,
                        $"{tag}: {Name(g)} läuft {revier.Kuerzel} {nr} zum zweiten Mal (zuerst am {frueher.Datum.ToString("dd.MM.", De)})."));
                else
                    erstmals[(g, revierId, nr)] = t;
            }

            var proFaehrte = laeufe.GroupBy(l => l.Nr).ToList();
            if (proFaehrte.Any(x => x.Count() > 2))
                befunde.Add(new(Schwere.Fehler, t.Id, null, $"{tag}: eine Fährte ist mehr als zweimal belegt."));
            if (proFaehrte.Count(x => x.Count() > 1) > 1)
                befunde.Add(new(Schwere.Fehler, t.Id, null, $"{tag}: mehr als eine Fährte wird doppelt gelaufen."));

            if (t.Status == TerminStatus.Durchgefuehrt) continue;

            foreach (var g in t.Abgemeldet.Where(g => t.Zuteilungen.Any(z => z.GespannId == g)))
                befunde.Add(new(Schwere.Fehler, t.Id, g, $"{tag}: {Name(g)} ist abgemeldet, hat aber eine Fährte."));

            if (!planer.Optionen.HelferLaufenMit)
                foreach (var g in t.Helfer.Where(g => t.Zuteilungen.Any(z => z.GespannId == g)))
                    befunde.Add(new(Schwere.Fehler, t.Id, g, $"{tag}: {Name(g)} hat Helferdienst, hat aber eine Fährte."));

            foreach (var g in planer.Laeufer(t).Where(g => t.Zuteilungen.All(z => z.GespannId != g)))
                befunde.Add(new(Schwere.Fehler, t.Id, g, $"{tag}: für {Name(g)} ist keine freie Fährte mehr übrig."));

            foreach (var h in t.Helfer.Where(h => !planer.KannHelfen(t, h)))
                befunde.Add(new(Schwere.Warnung, t.Id, h, $"{tag}: {Name(h)} ist als Helfer eingeteilt, gehört aber nicht zur Helfergruppe {planer.Helfergruppe(t)}."));

            if (t.Helfer.Count < planer.Optionen.HelferProTermin)
                befunde.Add(new(Schwere.Warnung, t.Id, null, $"{tag}: zu wenige Helfer eingeteilt."));
        }

        foreach (var gruppe in planer.Gespanne.GroupBy(g => g.Gruppe))
        {
            var kapazitaet = planer.Reviere.Where(r => r.FuerTrainingNutzbar).Select(r => r.Kapazitaet).DefaultIfEmpty(0).Min();
            var helferAusGruppe = !planer.Optionen.HelferAusAndererGruppe && !planer.Optionen.HelferLaufenMit;
            var laeufer = gruppe.Count() - (helferAusGruppe ? planer.Optionen.HelferProTermin : 0);
            if (laeufer > kapazitaet)
                befunde.Add(new(Schwere.Warnung, null, null,
                    $"Gruppe {gruppe.Key} hat {gruppe.Count()} Gespanne, ein Schweißblock fasst {kapazitaet} Läufe."));
        }

        return befunde;
    }

    private static void PruefeRuhezeit(List<Termin> termine, Termin t, Revier revier, string tag, List<Befund> befunde)
    {
        var vorher = termine.LastOrDefault(x => x.Datum < t.Datum && x.RevierId == revier.Id);
        if (vorher is null) return;
        var tage = t.Datum.DayNumber - vorher.Datum.DayNumber;
        if (tage < revier.MinRuhetage)
            befunde.Add(new(Schwere.Fehler, t.Id, null, $"{tag}: {revier.Name} hatte nur {tage} Tage Ruhe (mindestens {revier.MinRuhetage})."));
        else if (tage < revier.ZielRuhetage)
            befunde.Add(new(Schwere.Warnung, t.Id, null, $"{tag}: {revier.Name} hatte {tage} Tage Ruhe (angestrebt {revier.ZielRuhetage})."));
    }

    private string Name(int gespannId)
    {
        var g = planer.Gespanne.FirstOrDefault(x => x.Id == gespannId);
        return g is null ? $"Gespann {gespannId}" : $"{g.Fuehrer} / {g.Hund}";
    }
}
