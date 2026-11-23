using System.Globalization;

namespace Hundekurs.Planung;

/// <summary>
/// Änderungen im laufenden Betrieb: Fährtentausch, Helfertausch, Abmeldung, Nachholen, Durchführung.
/// Jede Methode verändert den übergebenen Plan nicht, sondern liefert einen Vorschlag mit Vorher/Nachher-Liste.
/// </summary>
public sealed class Tauschdienst(Schweissplaner planer)
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private readonly PlanPruefer _pruefer = new(planer);

    public Aenderungsvorschlag FaehrtenTauschen(Saisonplan plan, int terminId, int gespannA, int gespannB)
    {
        var t = plan.Termin(terminId);
        if (t.Status == TerminStatus.Durchgefuehrt) return Aenderungsvorschlag.Abgelehnt("Der Termin ist bereits durchgeführt.");
        if (t.RevierId is not int revierId) return Aenderungsvorschlag.Abgelehnt("Für den Termin ist kein Revier geplant.");

        var za = t.Zuteilungen.SingleOrDefault(z => z.GespannId == gespannA);
        var zb = t.Zuteilungen.SingleOrDefault(z => z.GespannId == gespannB);
        if (za is null || zb is null) return Aenderungsvorschlag.Abgelehnt("Beide Gespanne müssen an diesem Termin eine Fährte haben.");
        if (za.FaehrteNr == zb.FaehrteNr) return Aenderungsvorschlag.Abgelehnt("Beide laufen dieselbe Fährte, ein Tausch ändert nichts.");

        var konflikte = new List<string>();
        if (FestGelaufen(plan, t, gespannA, revierId, zb.FaehrteNr) is Termin fa)
            konflikte.Add($"{Name(gespannA)} ist Fährte {zb.FaehrteNr} bereits am {fa.Datum.ToString("dd.MM.", De)} gelaufen.");
        if (FestGelaufen(plan, t, gespannB, revierId, za.FaehrteNr) is Termin fb)
            konflikte.Add($"{Name(gespannB)} ist Fährte {za.FaehrteNr} bereits am {fb.Datum.ToString("dd.MM.", De)} gelaufen.");
        if (konflikte.Count > 0)
            return Aenderungsvorschlag.Abgelehnt(string.Join(" ", konflikte), Alternativen(plan, t, gespannA, revierId));

        var neu = plan.Kopie();
        var nt = neu.Termin(terminId);
        var zuordnung = nt.Zuteilungen
            .Select(z => (z.GespannId, Nr: z.GespannId == gespannA ? zb.FaehrteNr : z.GespannId == gespannB ? za.FaehrteNr : z.FaehrteNr))
            .ToList();
        nt.Zuteilungen = Schweissplaner.MitSequenzen(planer.Revier(revierId), zuordnung);
        nt.ZuteilungFixiert = true;

        return Vorschlag(plan, planer.NeuPlanen(neu, ab: nt.Datum));
    }

    /// <summary>
    /// Zwei Gespanne tauschen ihre Termine: <paramref name="gespannA"/> läuft statt an Termin 1 an Termin 2,
    /// <paramref name="gespannB"/> statt an Termin 2 an Termin 1. Funktioniert auch zwischen den Gruppen.
    /// Jedes Gespann übernimmt nach Möglichkeit die Fährte des anderen, damit sich für alle übrigen nichts ändert.
    /// </summary>
    public Aenderungsvorschlag TermineTauschen(Saisonplan plan, int termin1, int gespannA, int termin2, int gespannB)
    {
        if (termin1 == termin2) return Aenderungsvorschlag.Abgelehnt("Für einen Tausch am selben Termin bitte „Fährten tauschen“ verwenden.");
        if (gespannA == gespannB) return Aenderungsvorschlag.Abgelehnt("Bitte zwei verschiedene Gespanne wählen.");
        var t1 = plan.Termin(termin1);
        var t2 = plan.Termin(termin2);
        if (t1.Status == TerminStatus.Durchgefuehrt || t2.Status == TerminStatus.Durchgefuehrt)
            return Aenderungsvorschlag.Abgelehnt("Durchgeführte Termine können nicht mehr getauscht werden.");

        var za = t1.Zuteilungen.SingleOrDefault(z => z.GespannId == gespannA);
        var zb = t2.Zuteilungen.SingleOrDefault(z => z.GespannId == gespannB);
        if (za is null) return Aenderungsvorschlag.Abgelehnt($"{Name(gespannA)} hat am {t1.Datum.ToString("dd.MM.", De)} keine Fährte.");
        if (zb is null) return Aenderungsvorschlag.Abgelehnt($"{Name(gespannB)} hat am {t2.Datum.ToString("dd.MM.", De)} keine Fährte.");
        if (t2.Zuteilungen.Any(z => z.GespannId == gespannA) || t1.Zuteilungen.Any(z => z.GespannId == gespannB))
            return Aenderungsvorschlag.Abgelehnt("Eines der Gespanne läuft am anderen Termin bereits.");
        if (t2.Helfer.Contains(gespannA))
            return Aenderungsvorschlag.Abgelehnt($"{Name(gespannA)} hat am {t2.Datum.ToString("dd.MM.", De)} Helferdienst. Bitte den Dienst zuerst übergeben.");
        if (t1.Helfer.Contains(gespannB))
            return Aenderungsvorschlag.Abgelehnt($"{Name(gespannB)} hat am {t1.Datum.ToString("dd.MM.", De)} Helferdienst. Bitte den Dienst zuerst übergeben.");

        var neu = plan.Kopie();
        var n1 = neu.Termin(termin1);
        var n2 = neu.Termin(termin2);
        Verlegen(n1, gespannA, raus: true); Verlegen(n1, gespannB, raus: false);
        Verlegen(n2, gespannB, raus: true); Verlegen(n2, gespannA, raus: false);

        // Wunschfährte: jeder übernimmt die Fährte des anderen. Die Neuplanung hält daran fest, soweit erlaubt.
        n1.Zuteilungen = n1.Zuteilungen.Select(z => z.GespannId == gespannA ? z with { GespannId = gespannB } : z).ToList();
        n2.Zuteilungen = n2.Zuteilungen.Select(z => z.GespannId == gespannB ? z with { GespannId = gespannA } : z).ToList();
        n1.ZuteilungFixiert = n2.ZuteilungFixiert = false;

        var ab = n1.Datum < n2.Datum ? n1.Datum : n2.Datum;
        return Vorschlag(plan, planer.NeuPlanen(neu, ab: ab));
    }

    private void Verlegen(Termin t, int gespannId, bool raus)
    {
        var eigeneGruppe = planer.Gespann(gespannId).Gruppe == t.Gruppe;
        if (raus)
        {
            if (eigeneGruppe) t.Abgemeldet.Add(gespannId);
            t.Gaeste.Remove(gespannId);
        }
        else
        {
            t.Abgemeldet.Remove(gespannId);
            if (!eigeneGruppe) t.Gaeste.Add(gespannId);
        }
    }

    /// <summary>Helfer <paramref name="gespannA"/> an Termin 1 und Helfer <paramref name="gespannB"/> an Termin 2 tauschen ihre Dienste.</summary>
    public Aenderungsvorschlag HelferTauschen(Saisonplan plan, int termin1, int gespannA, int termin2, int gespannB)
    {
        var t1 = plan.Termin(termin1);
        var t2 = plan.Termin(termin2);
        if (t1.Status == TerminStatus.Durchgefuehrt || t2.Status == TerminStatus.Durchgefuehrt)
            return Aenderungsvorschlag.Abgelehnt("Durchgeführte Termine können nicht mehr getauscht werden.");
        if (!t1.Helfer.Contains(gespannA) || !t2.Helfer.Contains(gespannB))
            return Aenderungsvorschlag.Abgelehnt("Beide müssen am jeweiligen Termin als Helfer eingeteilt sein.");
        if (t1.Helfer.Contains(gespannB) || t2.Helfer.Contains(gespannA))
            return Aenderungsvorschlag.Abgelehnt("Einer der beiden hat am anderen Termin bereits Helferdienst.");
        if (!planer.KannHelfen(t1, gespannB) || !planer.KannHelfen(t2, gespannA))
            return Aenderungsvorschlag.Abgelehnt("Den Helferdienst übernimmt immer die Gruppe, die an dem Tag nicht auf Schweiß ist. Wer an einem Tag selbst auf Schweiß ist, kann dort keinen Helferdienst übernehmen; getauscht werden kann daher nur zwischen Terminen derselben Gruppe.");

        var neu = plan.Kopie();
        var n1 = neu.Termin(termin1);
        var n2 = neu.Termin(termin2);
        n1.Helfer.Remove(gespannA); n1.Helfer.Add(gespannB); n1.HelferFixiert = true;
        n2.Helfer.Remove(gespannB); n2.Helfer.Add(gespannA); n2.HelferFixiert = true;
        n1.ZuteilungFixiert = n2.ZuteilungFixiert = false;

        var ab = n1.Datum < n2.Datum ? n1.Datum : n2.Datum;
        return Vorschlag(plan, planer.NeuPlanen(neu, ab: ab));
    }

    /// <summary><paramref name="vertretung"/> übernimmt am selben Termin den Helferdienst von <paramref name="helfer"/>.</summary>
    public Aenderungsvorschlag HelferVertreten(Saisonplan plan, int terminId, int helfer, int vertretung)
    {
        var t = plan.Termin(terminId);
        if (t.Status == TerminStatus.Durchgefuehrt) return Aenderungsvorschlag.Abgelehnt("Der Termin ist bereits durchgeführt.");
        if (!t.Helfer.Contains(helfer)) return Aenderungsvorschlag.Abgelehnt($"{Name(helfer)} ist an diesem Termin nicht als Helfer eingeteilt.");
        if (t.Helfer.Contains(vertretung)) return Aenderungsvorschlag.Abgelehnt($"{Name(vertretung)} hat an diesem Termin bereits Helferdienst.");
        if (!planer.KannHelfen(t, vertretung))
            return Aenderungsvorschlag.Abgelehnt($"{Name(vertretung)} kann hier nicht helfen: Helfer kommen aus Gruppe {planer.Helfergruppe(t)}.");

        var neu = plan.Kopie();
        var nt = neu.Termin(terminId);
        nt.Helfer.Remove(helfer); nt.Helfer.Add(vertretung);
        nt.HelferFixiert = true; nt.ZuteilungFixiert = false;
        return Vorschlag(plan, planer.NeuPlanen(neu, ab: nt.Datum));
    }

    public Aenderungsvorschlag Abmelden(Saisonplan plan, int terminId, int gespannId)
    {
        var t = plan.Termin(terminId);
        if (t.Status == TerminStatus.Durchgefuehrt) return Aenderungsvorschlag.Abgelehnt("Der Termin ist bereits durchgeführt.");

        var neu = plan.Kopie();
        var nt = neu.Termin(terminId);
        nt.Abgemeldet.Add(gespannId);
        nt.Gaeste.Remove(gespannId);
        nt.Helfer.Remove(gespannId);
        nt.ZuteilungFixiert = false;
        return Vorschlag(plan, planer.NeuPlanen(neu, ab: nt.Datum));
    }

    /// <summary>Ein Gespann läuft zusätzlich an einem Termin (z. B. der anderen Gruppe) mit, etwa zum Nachholen.</summary>
    public Aenderungsvorschlag Nachholen(Saisonplan plan, int terminId, int gespannId)
    {
        var t = plan.Termin(terminId);
        if (t.Status == TerminStatus.Durchgefuehrt) return Aenderungsvorschlag.Abgelehnt("Der Termin ist bereits durchgeführt.");

        if (t.Helfer.Contains(gespannId))
            return Aenderungsvorschlag.Abgelehnt($"{Name(gespannId)} hat an diesem Termin Helferdienst. Bitte zuerst den Dienst übergeben.");
        if (t.RevierId is int revierId && !planer.Laeufer(t).Contains(gespannId)
            && planer.Laeufer(t).Count >= planer.Revier(revierId).Kapazitaet)
            return Aenderungsvorschlag.Abgelehnt(
                $"Der Schweißblock ist voll ({planer.Laeufer(t).Count} von {planer.Revier(revierId).Kapazitaet} Läufen). Nachholen geht nur, wenn sich jemand abmeldet.");

        var neu = plan.Kopie();
        var nt = neu.Termin(terminId);
        nt.Abgemeldet.Remove(gespannId);
        if (planer.Gespann(gespannId).Gruppe != nt.Gruppe) nt.Gaeste.Add(gespannId);
        nt.ZuteilungFixiert = false;
        return Vorschlag(plan, planer.NeuPlanen(neu, ab: nt.Datum));
    }

    /// <summary>Markiert den Termin als durchgeführt. Ohne Angabe wird der Plan als Ist übernommen.</summary>
    public Aenderungsvorschlag Durchfuehren(Saisonplan plan, int terminId, IEnumerable<Lauf>? ist = null)
    {
        var neu = plan.Kopie();
        var nt = neu.Termin(terminId);
        nt.Ist = (ist ?? nt.Zuteilungen.Select(z => new Lauf(z.GespannId, z.FaehrteNr))).ToList();
        nt.Status = TerminStatus.Durchgefuehrt;
        return Vorschlag(plan, planer.NeuPlanen(neu, ab: nt.Datum.AddDays(1)));
    }

    public static IReadOnlyList<Aenderung> Vergleichen(Schweissplaner planer, Saisonplan alt, Saisonplan neu)
    {
        var ergebnis = new List<Aenderung>();
        string N(int id) => planer.Gespanne.FirstOrDefault(g => g.Id == id) is { } g ? $"{g.Fuehrer} / {g.Hund}" : $"Gespann {id}";
        string R(int? id) => id is int r ? planer.Revier(r).Name : "–";

        foreach (var tn in neu.Chronologisch)
        {
            var ta = alt.Termine.SingleOrDefault(t => t.Id == tn.Id);
            if (ta is null) { ergebnis.Add(new(tn.Id, tn.Datum, null, "Termin neu angelegt")); continue; }

            if (ta.RevierId != tn.RevierId)
                ergebnis.Add(new(tn.Id, tn.Datum, null, $"Revier {R(ta.RevierId)} → {R(tn.RevierId)}"));
            foreach (var h in tn.Helfer.Except(ta.Helfer)) ergebnis.Add(new(tn.Id, tn.Datum, h, $"{N(h)} übernimmt Helferdienst"));
            foreach (var h in ta.Helfer.Except(tn.Helfer)) ergebnis.Add(new(tn.Id, tn.Datum, h, $"{N(h)} hat keinen Helferdienst mehr"));

            var va = ta.Zuteilungen.ToDictionary(z => z.GespannId, z => z.FaehrteNr);
            var vn = tn.Zuteilungen.ToDictionary(z => z.GespannId, z => z.FaehrteNr);
            foreach (var g in va.Keys.Union(vn.Keys).Order())
            {
                var hatAlt = va.TryGetValue(g, out var a);
                var hatNeu = vn.TryGetValue(g, out var b);
                if (hatAlt && hatNeu && a != b && ta.RevierId == tn.RevierId) ergebnis.Add(new(tn.Id, tn.Datum, g, $"{N(g)}: Fährte {a} → {b}"));
                else if (hatAlt && hatNeu && ta.RevierId != tn.RevierId) ergebnis.Add(new(tn.Id, tn.Datum, g, $"{N(g)}: Fährte {b}"));
                else if (hatAlt && !hatNeu) ergebnis.Add(new(tn.Id, tn.Datum, g, $"{N(g)}: läuft nicht mehr (vorher Fährte {a})"));
                else if (!hatAlt && hatNeu) ergebnis.Add(new(tn.Id, tn.Datum, g, $"{N(g)}: läuft neu Fährte {b}"));
            }
        }
        return ergebnis;
    }

    private Aenderungsvorschlag Vorschlag(Saisonplan alt, Saisonplan neu) => new()
    {
        Moeglich = true,
        Plan = neu,
        Aenderungen = Vergleichen(planer, alt, neu),
        Befunde = _pruefer.Pruefen(neu),
    };

    /// <summary>Termin, an dem das Gespann die Fährte bereits fest gelaufen hat oder fest eingeplant ist (ohne den betrachteten Termin).</summary>
    private static Termin? FestGelaufen(Saisonplan plan, Termin aktuell, int gespann, int revierId, int nr) =>
        plan.Chronologisch.FirstOrDefault(t =>
            t.Id != aktuell.Id && t.RevierId == revierId
            && (t.Status == TerminStatus.Durchgefuehrt || t.ZuteilungFixiert || t.Datum < aktuell.Datum)
            && Schweissplaner.GezaehlteLaeufe(t).Any(l => l.GespannId == gespann && l.Nr == nr));

    private List<string> Alternativen(Saisonplan plan, Termin t, int gespann, int revierId)
    {
        var ergebnis = new List<string>();
        var eigene = t.Zuteilungen.Single(z => z.GespannId == gespann);
        foreach (var z in t.Zuteilungen.Where(z => z.GespannId != gespann && z.FaehrteNr != eigene.FaehrteNr))
        {
            if (FestGelaufen(plan, t, gespann, revierId, z.FaehrteNr) is null
                && FestGelaufen(plan, t, z.GespannId, revierId, eigene.FaehrteNr) is null)
                ergebnis.Add($"Tausch mit {Name(z.GespannId)} (Fährte {z.FaehrteNr})");
        }
        return ergebnis;
    }


    private string Name(int id) => planer.Gespann(id) is var g ? $"{g.Fuehrer} / {g.Hund}" : $"Gespann {id}";
}
