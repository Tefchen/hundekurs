using Hundekurs.Daten;
using Hundekurs.Planung;
using Einsatzart = Hundekurs.Domain.Einsatzart;
using Terminart = Hundekurs.Domain.Terminart;
using Terminstatus = Hundekurs.Domain.Terminstatus;

namespace Hundekurs.Web.Services;

public sealed record PrueferDaten(int PersonId, string Name, string? Richternummer);

/// <summary>Prüfungstage, Richterbesetzung, Nennungen, Platzvergabe und Verpflegung.</summary>
public sealed partial class PlanungsZustand
{
    public List<PruefungstagDaten> Pruefungstage() => _speicher.Pruefungstage(_kursId);

    public List<NennungDaten> Nennungen() => _speicher.Nennungen(_kursId);

    /// <summary>Personen mit der Rolle Prüfer im Kurs.</summary>
    public List<PrueferDaten> Pruefer() => _speicher.PrueferListe(_kursId).Select(p => new PrueferDaten(p.PersonId, p.Name, p.Richternummer)).ToList();

    /// <summary>Letzter Trainingstag von Teil 2 (Schweiß oder Apportierarbeit), abgesagte zählen nicht.</summary>
    public DateOnly? LetzterTrainingstag
    {
        get
        {
            var daten = Plan.Termine.Select(t => t.Datum)
                .Concat(Nebentermine(Terminart.Apportierarbeit).Where(t => t.Status != Terminstatus.Abgesagt).Select(t => t.Datum))
                .ToList();
            return daten.Count == 0 ? null : daten.Max();
        }
    }

    /// <summary>Revier, das für die Prüfung vorgesehen ist (Verwendung „Prüfung“, sonst „beides“).</summary>
    public int? Pruefungsrevier =>
        (Reviere.FirstOrDefault(r => r.Aktiv && r.Verwendung == Verwendung.Pruefung)
         ?? Reviere.FirstOrDefault(r => r.Aktiv && r.Verwendung == Verwendung.Beides))?.Id;

    /// <summary>Vorschlag für neue Prüfungstage: die Sonntage nach dem letzten Trainingstag, ohne schon angelegte.</summary>
    public IReadOnlyList<DateOnly> PruefungsterminVorschlag()
    {
        if (LetzterTrainingstag is not DateOnly letzter) return [];
        var vorhanden = Pruefungstage().Select(p => p.Datum).ToHashSet();
        return Pruefungsregeln.Terminvorschlag(letzter, Pruefungsregeln.StandardTermine + vorhanden.Count)
            .Where(d => !vorhanden.Contains(d))
            .Take(Pruefungsregeln.StandardTermine)
            .ToList();
    }

    public void PruefungstageAnlegen(IReadOnlyCollection<DateOnly> daten, int? revierId)
    {
        if (daten.Count == 0) throw new InvalidOperationException("Bitte mindestens ein Datum angeben.");
        foreach (var d in daten)
            if (Pruefungsregeln.DatumPruefen(d, LetzterTrainingstag) is { } fehler) throw new InvalidOperationException(fehler);
        Einfach(() => _speicher.PruefungstageAnlegen(_kursId, daten, revierId, Pruefungsregeln.StandardBeginn, Pruefungsregeln.StandardMaxHunde),
            $"Prüfungstage angelegt: {string.Join(", ", daten.Order().Select(d => d.ToString("dd.MM.yyyy")))}");
    }

    /// <summary>Speichert einen Prüfungstag samt Ausbildern am Vortag und am Prüfungstag (km-Abrechnung).</summary>
    public async Task<string?> PruefungstagSpeichern(PruefungstagDaten d, IReadOnlySet<int> ausbilderVortag, IReadOnlySet<int> ausbilderPruefung)
    {
        var bisher = Pruefungstage().Single(p => p.Id == d.Id);
        if (d.Datum != bisher.Datum && Pruefungsregeln.DatumPruefen(d.Datum, LetzterTrainingstag) is { } fehler)
            throw new InvalidOperationException(fehler);

        lock (_sperre)
        {
            _speicher.PruefungstagSpeichern(_kursId, d);
            _speicher.EinsaetzeSetzen(d.TerminId, Einsatzart.Vorbereitung, d.Vortag, d.RevierId, null, ausbilderVortag);
            _speicher.EinsaetzeSetzen(d.TerminId, Einsatzart.Pruefung, d.Datum, d.RevierId, null, ausbilderPruefung);
            var aktion = $"Prüfungstag {d.Datum:dd.MM.yyyy} geändert" + (d.Datum != bisher.Datum ? $" (bisher {bisher.Datum:dd.MM.yyyy})" : "") +
                $": {d.Besetzung.Richter.Count} Prüfer, {d.PrueferAmVortag.Count} am Vortag, Ausbilder Vortag {ausbilderVortag.Count}, Prüfung {ausbilderPruefung.Count}";
            _speicher.Protokollieren(_kursId, aktion, []);
        }
        var meldung = await KmBerechnen(_speicher.EinsaetzeOhneKm(d.Vortag, d.Datum));
        Geaendert?.Invoke();
        return meldung;
    }

    public void PruefungstagLoeschen(PruefungstagDaten d) =>
        Einfach(() => _speicher.PruefungstagLoeschen(_kursId, d.Id), $"Prüfungstag {d.Datum:dd.MM.yyyy} gelöscht");

    // ---------- Platzvergabe, Prüfung der Besetzung, Verpflegung ----------

    /// <summary>Startplätze und Warteliste eines Prüfungstags (Schlüssel: Nennung-Id).</summary>
    public static Dictionary<int, Pruefungsplatz> Plaetze(PruefungstagDaten tag, IEnumerable<NennungDaten> nennungen) =>
        Pruefungsregeln.PlaetzeVergeben(nennungen.Where(n => n.PruefungstagId == tag.Id).Select(n => n.FuerPlatzvergabe), tag.MaxHunde, tag.Meldeschluss)
            .ToDictionary(p => p.NennungId);

    public IReadOnlyList<Befund> PruefungBefunde(PruefungstagDaten tag) =>
        Pruefungsregeln.Pruefen(tag.TerminId, tag.Datum, LetzterTrainingstag, tag.Besetzung);

    /// <summary>
    /// Personen für die Verpflegung: Hundeführer mit Startplatz und ihre Gäste, die Prüfer, die Ausbilder am Prüfungstag und weitere Helfer.
    /// Wer zwei Rollen hat (z. B. Ausbilder und Prüfer), zählt einmal.
    /// </summary>
    public static Verpflegung VerpflegungZaehlen(PruefungstagDaten tag, IEnumerable<NennungDaten> nennungen)
    {
        var plaetze = Plaetze(tag, nennungen);
        var starter = nennungen.Where(n => n.PruefungstagId == tag.Id && plaetze[n.Id!.Value].Status == Platzstatus.Startplatz).ToList();
        var fuehrer = starter.Select(n => n.FuehrerId).ToHashSet();
        var pruefer = tag.Besetzung.Richter.Where(p => !fuehrer.Contains(p)).ToHashSet();
        var ausbilder = tag.AusbilderPruefung.Count(a => !fuehrer.Contains(a) && !pruefer.Contains(a));
        return new Verpflegung(fuehrer.Count, starter.Sum(n => n.Gaeste), pruefer.Count, ausbilder, tag.WeitereHelfer);
    }

    // ---------- Nennungen ----------

    public void NennungSpeichern(NennungDaten d, string beschreibung) =>
        Einfach(() => _speicher.NennungSpeichern(_kursId, d), $"Nennung {beschreibung} {(d.Id is null ? "erfasst" : "geändert")}");

    public void NennungLoeschen(NennungDaten d) =>
        Einfach(() => _speicher.NennungLoeschen(_kursId, d.Id!.Value), $"Nennung {d.Fuehrer} / {d.Hund} gelöscht");

    public void NenngeldBezahltSetzen(NennungDaten d, DateOnly? am) =>
        Einfach(() => _speicher.NenngeldBezahltSetzen(_kursId, d.Id!.Value, am),
            $"Nenngeld {d.Fuehrer} / {d.Hund}: {(am is DateOnly t ? $"bezahlt am {t:dd.MM.yyyy}" : "wieder offen")}");

    /// <summary>Lost die Startreihenfolge der Gespanne mit Startplatz aus. Gespanne auf der Warteliste bekommen kein Los.</summary>
    public void LoseZiehen(PruefungstagDaten tag)
    {
        var nennungen = Nennungen();
        var plaetze = Plaetze(tag, nennungen);
        var starter = plaetze.Values.Where(p => p.Status == Platzstatus.Startplatz).Select(p => p.NennungId).ToArray();
        Random.Shared.Shuffle(starter);
        Einfach(() => _speicher.LoseSetzen(_kursId, tag.Id, starter), $"Startreihenfolge für {tag.Datum:dd.MM.yyyy} gelost ({starter.Length} Gespanne)");
    }
}
