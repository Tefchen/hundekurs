namespace Hundekurs.Planung;

/// <summary>
/// Erzeugt und aktualisiert den Saisonplan für die Schweißarbeit.
/// Die Termine werden chronologisch abgearbeitet. Je Termin werden Revier, Helfer und Fährten zugeteilt;
/// die Fährtenzuteilung ist eine exakte Optimierung (Tiefensuche mit Schranke) über höchstens 7 Hunde.
/// </summary>
public sealed class Schweissplaner
{
    private readonly Dictionary<int, Revier> _reviere;
    private readonly IReadOnlyList<Gespann> _gespanne;

    public Schweissplaner(IEnumerable<Revier> reviere, IEnumerable<Gespann> gespanne, PlanungsOptionen? optionen = null)
    {
        _reviere = reviere.ToDictionary(r => r.Id);
        _gespanne = gespanne.OrderBy(g => g.Id).ToList();
        Optionen = optionen ?? new PlanungsOptionen();
    }

    public PlanungsOptionen Optionen { get; }
    public IReadOnlyCollection<Revier> Reviere => _reviere.Values;
    public IReadOnlyList<Gespann> Gespanne => _gespanne;

    public Revier Revier(int id) => _reviere[id];
    public Gespann Gespann(int id) => _gespanne.Single(g => g.Id == id);

    /// <summary>Legt wöchentliche Termine ab dem ersten Sonntag an und plant sie vollständig.</summary>
    public Saisonplan SaisonErzeugen(DateOnly ersterTermin, int anzahlTermine, Gruppe ersteGruppe = Gruppe.A)
    {
        var plan = new Saisonplan();
        for (var i = 0; i < anzahlTermine; i++)
        {
            plan.Termine.Add(new Termin
            {
                Id = i + 1,
                Datum = ersterTermin.AddDays(7 * i),
                Gruppe = i % 2 == 0 ? ersteGruppe : Andere(ersteGruppe),
            });
        }
        return NeuPlanen(plan, ab: null, vollstaendig: true);
    }

    /// <summary>
    /// Plant alle änderbaren Termine ab dem angegebenen Datum neu.
    /// Durchgeführte und von Hand fixierte Teile bleiben unverändert.
    /// Ohne <paramref name="vollstaendig"/> bleiben gültige Reviere und Helfer erhalten und Fährten ändern sich so wenig wie möglich.
    /// </summary>
    public Saisonplan NeuPlanen(Saisonplan plan, DateOnly? ab = null, bool vollstaendig = false)
    {
        var alt = plan.Termine.ToDictionary(t => t.Id);
        var neu = plan.Kopie();
        var termine = neu.Chronologisch.ToList();

        bool Aenderbar(Termin t) => t.Status != TerminStatus.Durchgefuehrt && (ab is null || t.Datum >= ab);

        // Reservierungen aus Terminen, die nicht neu geplant werden (Vergangenheit, durchgeführt, fixiert).
        var gelaufen = _gespanne.ToDictionary(g => g.Id, _ => new HashSet<(int Revier, int Nr)>());
        foreach (var t in termine.Where(t => t.RevierId is not null && (!Aenderbar(t) || t.ZuteilungFixiert)))
            foreach (var (g, nr) in GezaehlteLaeufe(t))
                if (gelaufen.TryGetValue(g, out var set)) set.Add((t.RevierId!.Value, nr));

        var revierBelegt = _reviere.Keys.ToDictionary(id => id, _ => new List<(int TerminId, DateOnly Datum)>());
        foreach (var t in termine.Where(t => t.RevierId is not null && !RevierNeuWaehlbar(t, Aenderbar(t), vollstaendig)))
            if (revierBelegt.TryGetValue(t.RevierId!.Value, out var l)) l.Add((t.Id, t.Datum));

        var gruppeJeTermin = termine.ToDictionary(t => t.Id, t => t.Gruppe);
        var helferAnzahl = _gespanne.ToDictionary(g => g.Id, _ => 0);
        var helferZuletzt = new Dictionary<int, DateOnly>();
        var doppeltAnzahl = new Dictionary<(int Revier, int Nr), int>();
        var gruppenBesuche = new Dictionary<(Gruppe, int Revier), int>();

        foreach (var t in termine)
        {
            var aenderbar = Aenderbar(t);

            if (aenderbar && RevierNeuWaehlbar(t, aenderbar, vollstaendig))
            {
                t.RevierId = WaehleRevier(t, revierBelegt, gruppenBesuche, gruppeJeTermin);
                if (t.RevierId is int rid) revierBelegt[rid].Add((t.Id, t.Datum));
            }

            if (aenderbar)
            {
                PlaneHelfer(t, vollstaendig, helferAnzahl, helferZuletzt);
                if (!t.ZuteilungFixiert)
                {
                    alt.TryGetValue(t.Id, out var vorher);
                    t.Zuteilungen = t.RevierId is int r
                        ? Zuteilen(t, _reviere[r], vorher?.RevierId == r ? vorher : null, gelaufen, doppeltAnzahl)
                        : [];
                }
            }

            // Zähler fortschreiben
            foreach (var h in t.Helfer)
            {
                helferAnzahl[h] = helferAnzahl.GetValueOrDefault(h) + 1;
                helferZuletzt[h] = t.Datum;
            }
            if (t.RevierId is int revierId)
            {
                gruppenBesuche[(t.Gruppe, revierId)] = gruppenBesuche.GetValueOrDefault((t.Gruppe, revierId)) + 1;
                var laeufe = GezaehlteLaeufe(t).ToList();
                foreach (var (g, nr) in laeufe)
                    if (gelaufen.TryGetValue(g, out var set)) set.Add((revierId, nr));
                foreach (var nr in laeufe.GroupBy(l => l.Nr).Where(x => x.Count() > 1).Select(x => x.Key))
                    doppeltAnzahl[(revierId, nr)] = doppeltAnzahl.GetValueOrDefault((revierId, nr)) + 1;
            }
        }

        return neu;
    }

    /// <summary>Gespanne, die an einem Termin eine Fährte laufen sollen.</summary>
    public IReadOnlyList<int> Laeufer(Termin t) =>
        _gespanne.Where(g => g.Gruppe == t.Gruppe).Select(g => g.Id)
            .Concat(t.Gaeste)
            .Distinct()
            .Where(id => !t.Abgemeldet.Contains(id))
            .Where(id => Optionen.HelferLaufenMit || !t.Helfer.Contains(id))
            .OrderBy(id => id)
            .ToList();

    /// <summary>Läufe, die als „gelaufen“ zählen: Ist bei durchgeführten Terminen, sonst Soll.</summary>
    public static IEnumerable<(int GespannId, int Nr)> GezaehlteLaeufe(Termin t) =>
        t.Status == TerminStatus.Durchgefuehrt
            ? t.Ist.Where(l => l.Ergebnis != LaufErgebnis.NichtAngetreten).Select(l => (l.GespannId, l.FaehrteNr))
            : t.Zuteilungen.Select(z => (z.GespannId, z.FaehrteNr));

    /// <summary>Baut die Zuteilungen samt Sequenzen aus einer Zuordnung Gespann → Fährte.</summary>
    public static List<Zuteilung> MitSequenzen(Revier revier, IReadOnlyList<(int GespannId, int Nr)> zuordnung, Termin? vorher = null)
    {
        var ergebnis = new List<Zuteilung>();
        var proFaehrte = zuordnung.GroupBy(z => z.Nr).ToDictionary(
            x => x.Key,
            x => x.OrderBy(z => vorher?.Zuteilungen.Any(v => v.GespannId == z.GespannId && v.FaehrteNr == z.Nr && v.ZweiterLaufAufFaehrte) == true ? 1 : 0)
                  .ThenBy(z => z.GespannId)
                  .Select(z => z.GespannId)
                  .ToList());

        var sequenz = 0;
        var vergeben = new HashSet<int>();
        foreach (var paar in revier.EffektivePaare)
        {
            var belegt = new[] { paar.Erste, paar.Zweite }.Where(proFaehrte.ContainsKey).ToList();
            if (belegt.Count == 0) continue;
            sequenz++;
            foreach (var nr in belegt)
            {
                ergebnis.Add(new Zuteilung(proFaehrte[nr][0], nr, sequenz, false));
                vergeben.Add(nr);
            }
        }
        foreach (var nr in proFaehrte.Keys.Where(n => !vergeben.Contains(n)).Order())
        {
            sequenz++;
            ergebnis.Add(new Zuteilung(proFaehrte[nr][0], nr, sequenz, false));
        }
        var zweite = proFaehrte.Where(x => x.Value.Count > 1).OrderBy(x => x.Key).ToList();
        foreach (var (nr, gespanne) in zweite)
        {
            sequenz++;
            foreach (var g in gespanne.Skip(1))
                ergebnis.Add(new Zuteilung(g, nr, sequenz, true));
        }
        return ergebnis.OrderBy(z => z.Sequenz).ThenBy(z => z.FaehrteNr).ToList();
    }

    private bool RevierNeuWaehlbar(Termin t, bool aenderbar, bool vollstaendig) =>
        aenderbar && !t.RevierFixiert && t.Status == TerminStatus.Geplant
        && (vollstaendig || t.RevierId is not int id || !_reviere.TryGetValue(id, out var r) || !r.FuerTrainingNutzbar);

    private static Gruppe Andere(Gruppe g) => g == Gruppe.A ? Gruppe.B : Gruppe.A;

    private int? WaehleRevier(Termin t, Dictionary<int, List<(int TerminId, DateOnly Datum)>> belegt, Dictionary<(Gruppe, int), int> besuche, Dictionary<int, Gruppe> gruppeJeTermin)
    {
        var kandidaten = _reviere.Values.Where(r => r.FuerTrainingNutzbar && r.NutzbareFaehrten.Any()).ToList();
        if (kandidaten.Count == 0) return null;

        // Wann war die Gruppe zuletzt in diesem Revier? Nie = bevorzugt.
        int LetzterBesuch(int revierId) =>
            belegt[revierId].Where(b => b.Datum < t.Datum && gruppeJeTermin.TryGetValue(b.TerminId, out var g) && g == t.Gruppe)
                .Select(b => b.Datum.DayNumber).DefaultIfEmpty(int.MinValue).Max();

        int Abstand(Revier r)
        {
            var andere = belegt[r.Id].Where(b => b.TerminId != t.Id).ToList();
            return andere.Count == 0 ? int.MaxValue : andere.Min(b => Math.Abs(t.Datum.DayNumber - b.Datum.DayNumber));
        }

        return kandidaten
            .Select(r => (Revier: r, Abstand: Abstand(r)))
            .OrderBy(x => x.Abstand >= x.Revier.MinRuhetage ? 0 : 1)
            .ThenBy(x => x.Abstand >= x.Revier.ZielRuhetage ? 0 : 1)
            .ThenBy(x => besuche.GetValueOrDefault((t.Gruppe, x.Revier.Id)))
            .ThenBy(x => LetzterBesuch(x.Revier.Id))
            .ThenByDescending(x => x.Abstand)
            .ThenBy(x => x.Revier.Id)
            .First().Revier.Id;
    }

    /// <summary>Gruppe, aus der die Helfer eines Termins kommen.</summary>
    public Gruppe Helfergruppe(Termin t) => Optionen.HelferAusAndererGruppe ? Andere(t.Gruppe) : t.Gruppe;

    /// <summary>Ob ein Gespann an diesem Termin Helferdienst übernehmen kann.</summary>
    public bool KannHelfen(Termin t, int gespannId) =>
        _gespanne.Any(g => g.Id == gespannId && g.Gruppe == Helfergruppe(t))
        && !t.Abgemeldet.Contains(gespannId)
        && !t.Gaeste.Contains(gespannId);

    private void PlaneHelfer(Termin t, bool vollstaendig, Dictionary<int, int> anzahl, Dictionary<int, DateOnly> zuletzt)
    {
        if (vollstaendig && !t.HelferFixiert && t.Status == TerminStatus.Geplant)
            t.Helfer.Clear();

        // Ungültige Helfer (abgemeldet, falsche Gruppe, holen hier nach) entfernen
        t.Helfer.RemoveWhere(h => !KannHelfen(t, h));

        var fehlend = Optionen.HelferProTermin - t.Helfer.Count;
        if (fehlend <= 0) return;

        var neu = _gespanne
            .Where(g => KannHelfen(t, g.Id) && !t.Helfer.Contains(g.Id))
            .Select(g => g.Id)
            .OrderBy(id => anzahl.GetValueOrDefault(id))
            .ThenBy(id => zuletzt.TryGetValue(id, out var d) ? d.DayNumber : int.MinValue)
            .ThenBy(id => id)
            .Take(fehlend);
        foreach (var id in neu) t.Helfer.Add(id);
    }

    private List<Zuteilung> Zuteilen(
        Termin t,
        Revier revier,
        Termin? vorher,
        Dictionary<int, HashSet<(int Revier, int Nr)>> gelaufen,
        Dictionary<(int Revier, int Nr), int> doppeltAnzahl)
    {
        var laeufer = Laeufer(t);
        var faehrten = revier.NutzbareFaehrten.ToList();
        var bisher = vorher?.Zuteilungen.ToDictionary(z => z.GespannId, z => z.FaehrteNr) ?? [];

        // Optionen je Läufer: erlaubte Fährten, bisherige Fährte zuerst (bessere Schranke)
        var optionen = laeufer.Select(g =>
        {
            var schon = gelaufen.GetValueOrDefault(g) ?? [];
            var erlaubt = faehrten.Where(nr => !schon.Contains((revier.Id, nr))).ToList();
            if (bisher.TryGetValue(g, out var b) && erlaubt.Remove(b)) erlaubt.Insert(0, b);
            return erlaubt;
        }).ToList();

        var belegung = faehrten.ToDictionary(nr => nr, _ => 0);
        var aktuell = new int[laeufer.Count];
        var beste = new int[laeufer.Count];
        var besteKosten = int.MaxValue;
        var doppeltGenutzt = false;

        void Suche(int i, int kosten)
        {
            if (kosten >= besteKosten) return;
            if (i == laeufer.Count)
            {
                besteKosten = kosten;
                Array.Copy(aktuell, beste, aktuell.Length);
                return;
            }

            var g = laeufer[i];
            foreach (var nr in optionen[i])
            {
                var k = kosten + (bisher.TryGetValue(g, out var b) && b != nr ? Optionen.KostenPlanaenderung : 0);
                if (belegung[nr] == 0)
                {
                    belegung[nr] = 1; aktuell[i] = nr;
                    Suche(i + 1, k);
                    belegung[nr] = 0;
                }
                else if (belegung[nr] == 1 && !doppeltGenutzt)
                {
                    belegung[nr] = 2; doppeltGenutzt = true; aktuell[i] = nr;
                    Suche(i + 1, k + Optionen.KostenDoppelt + Optionen.KostenDoppeltJeVorherigemMal * doppeltAnzahl.GetValueOrDefault((revier.Id, nr)));
                    belegung[nr] = 1; doppeltGenutzt = false;
                }
            }
            aktuell[i] = 0; // keine Fährte
            Suche(i + 1, kosten + Optionen.KostenOhneFaehrte);
        }

        Suche(0, 0);

        var zuordnung = laeufer.Select((g, i) => (GespannId: g, Nr: beste[i])).Where(x => x.Nr != 0).ToList();
        return MitSequenzen(revier, zuordnung, vorher);
    }
}
