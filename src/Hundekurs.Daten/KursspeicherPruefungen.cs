using Hundekurs.Domain;
using Microsoft.EntityFrameworkCore;
using P = Hundekurs.Planung;

namespace Hundekurs.Daten;

/// <summary>
/// Ein Prüfungstag mit Termin, Vortag und Besetzung. <paramref name="AusbilderVortag"/> und <paramref name="AusbilderPruefung"/>
/// sind die Ausbildereinsätze (Grundlage der km-Abrechnung); sie werden nur gelesen.
/// </summary>
public sealed record PruefungstagDaten(
    int Id, int TerminId, DateOnly Datum, TimeOnly? Beginn, int? RevierId, Terminstatus Status,
    DateOnly Vortag, TimeOnly? VortagUhrzeit, int MaxHunde, DateOnly? Meldeschluss,
    int? RichterobmannId, int? Richter2Id, int? Richter3Id, IReadOnlyList<int> PrueferAmVortag,
    int WeitereHelfer, string? Lokal, string? Bemerkung,
    IReadOnlySet<int> AusbilderVortag, IReadOnlySet<int> AusbilderPruefung)
{
    public P.Richterbesetzung Besetzung => new(RichterobmannId, Richter2Id, Richter3Id, PrueferAmVortag);
}

/// <summary>
/// Eine Nennung. Beim Speichern zählen nur die Felder bis <paramref name="Bemerkung"/>; der Rest (Namen, Hundedaten) wird nur gelesen.
/// <paramref name="KursteilnahmeId"/> wird beim Speichern selbst ermittelt: gesetzt, wenn der Hund im Kurs angemeldet ist.
/// </summary>
public sealed record NennungDaten(
    int? Id, int PruefungstagId, int HundId, int FuehrerId, DateTimeOffset GenanntAm,
    bool Gehorsam, bool Schalenwild, bool Niederwild, int Gaeste, decimal Nenngeld, DateOnly? NenngeldBezahltAm,
    int? Losnummer, string? Bemerkung,
    int? KursteilnahmeId = null, string Fuehrer = "", string FuehrerKurz = "", string? FuehrerAnzeigename = null,
    string Hund = "", string? Zuchtbuchname = null, string? Rasse = null, Geschlecht? Geschlecht = null, DateOnly? Wurfdatum = null,
    string? ChipNummer = null, string? Zuchtbuchnummer = null, bool Vereinsmitglied = false)
{
    public bool AusDemKurs => KursteilnahmeId is not null;
    public P.Ausbildung Faecher => new(Gehorsam, Schalenwild, Niederwild);
    public P.Pruefungsnennung FuerPlatzvergabe => new(Id ?? 0, AusDemKurs, GenanntAm);
}

/// <summary>Prüfungstage, Richterbesetzung und Nennungen.</summary>
public sealed partial class Kursspeicher
{
    // ---------- Prüfungstage ----------

    public List<PruefungstagDaten> Pruefungstage(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        var tage = db.Pruefungstage.AsNoTracking()
            .Include(p => p.Termin).ThenInclude(t => t!.Vorbereitung)
            .Where(p => p.Termin!.KursId == kursId)
            .ToList()
            .OrderBy(p => p.Termin!.Datum)
            .ToList();
        var terminIds = tage.Select(p => p.TerminId).ToList();
        var einsaetze = db.Einsaetze.AsNoTracking()
            .Where(e => e.TerminId != null && terminIds.Contains(e.TerminId.Value))
            .ToList().ToLookup(e => (e.TerminId!.Value, e.Art), e => e.PersonId);

        return tage.Select(p =>
        {
            var t = p.Termin!;
            return new PruefungstagDaten(
                p.Id, t.Id, t.Datum, t.UhrzeitUebrigeFaecher, t.RevierId, t.Status,
                t.Vorbereitung?.Datum ?? t.Datum.AddDays(-1), t.Vorbereitung?.Uhrzeit, p.MaxHunde, p.Meldeschluss,
                p.RichterobmannId, p.Richter2Id, p.Richter3Id, p.PrueferAmVortag,
                p.WeitereHelfer, p.Lokal, p.Bemerkung,
                einsaetze[(t.Id, Einsatzart.Vorbereitung)].ToHashSet(),
                einsaetze[(t.Id, Einsatzart.Pruefung)].ToHashSet());
        }).ToList();
    }

    /// <summary>Legt Prüfungstage an (je ein Termin der Art Prüfung mit Vorbereitung am Vortag). Bestehende Tage bleiben unverändert.</summary>
    public List<int> PruefungstageAnlegen(int kursId, IEnumerable<DateOnly> daten, int? revierId, TimeOnly beginn, int maxHunde)
    {
        using var db = fabrik.CreateDbContext();
        var vorhanden = db.Termine.AsNoTracking()
            .Where(t => t.KursId == kursId && t.Art == Terminart.Pruefung)
            .Select(t => t.Datum).ToHashSet();

        var neu = new List<Pruefungstag>();
        foreach (var datum in daten.Distinct().Where(d => !vorhanden.Contains(d)).Order())
        {
            var termin = new Termin
            {
                KursId = kursId, Datum = datum, Art = Terminart.Pruefung, Status = Terminstatus.Geplant,
                RevierId = revierId, UhrzeitUebrigeFaecher = beginn,
                Vorbereitung = new Vorbereitungstermin { Datum = datum.AddDays(-1), RevierId = revierId },
            };
            var tag = new Pruefungstag { Termin = termin, MaxHunde = maxHunde };
            db.Pruefungstage.Add(tag);
            neu.Add(tag);
        }
        db.SaveChanges();
        return neu.Select(t => t.Id).ToList();
    }

    /// <summary>Speichert Termin, Vortag und Besetzung eines Prüfungstags. Die Ausbildereinsätze setzt <see cref="EinsaetzeSetzen"/>.</summary>
    public void PruefungstagSpeichern(int kursId, PruefungstagDaten d)
    {
        if (d.MaxHunde is < 1 or > 20) throw new InvalidOperationException("Bitte eine Höchstzahl zwischen 1 und 20 Hunden angeben.");
        if (d.WeitereHelfer is < 0 or > 50) throw new InvalidOperationException("Die Zahl der weiteren Helfer muss zwischen 0 und 50 liegen.");
        if (d.Vortag >= d.Datum) throw new InvalidOperationException("Der Vortag (Legen der Fährten) muss vor dem Prüfungstag liegen.");
        if (d.Meldeschluss is DateOnly m && m >= d.Datum) throw new InvalidOperationException("Der Meldeschluss muss vor dem Prüfungstag liegen.");

        using var db = fabrik.CreateDbContext();
        using var tx = db.Database.BeginTransaction();
        var tag = db.Pruefungstage.Include(p => p.Termin).ThenInclude(t => t!.Vorbereitung)
            .Single(p => p.Id == d.Id && p.Termin!.KursId == kursId);
        var termin = tag.Termin!;

        if (termin.Datum != d.Datum && db.Termine.Any(t => t.KursId == kursId && t.Art == Terminart.Pruefung && t.Datum == d.Datum && t.Id != termin.Id))
            throw new InvalidOperationException($"Am {d.Datum:dd.MM.yyyy} gibt es schon einen Prüfungstag.");

        var richter = new[] { d.RichterobmannId, d.Richter2Id, d.Richter3Id }.OfType<int>().ToList();
        var pruefer = db.Kursrollen.AsNoTracking().Where(r => r.KursId == kursId && r.Rolle == Rolle.Richter).Select(r => r.PersonId).ToHashSet();
        if (richter.Concat(d.PrueferAmVortag).Any(id => !pruefer.Contains(id)))
            throw new InvalidOperationException("Als Prüfer lassen sich nur Personen mit der Rolle „Prüfer“ eintragen (siehe Personen).");
        if (richter.Distinct().Count() != richter.Count)
            throw new InvalidOperationException("Ein Prüfer ist mehrfach eingetragen.");

        termin.Datum = d.Datum;
        termin.UhrzeitUebrigeFaecher = d.Beginn;
        termin.RevierId = d.RevierId;
        termin.Vorbereitung ??= new Vorbereitungstermin();
        termin.Vorbereitung.Datum = d.Vortag;
        termin.Vorbereitung.Uhrzeit = d.VortagUhrzeit;
        termin.Vorbereitung.RevierId = d.RevierId;

        tag.MaxHunde = d.MaxHunde;
        tag.Meldeschluss = d.Meldeschluss;
        tag.RichterobmannId = d.RichterobmannId;
        tag.Richter2Id = d.Richter2Id;
        tag.Richter3Id = d.Richter3Id;
        tag.PrueferAmVortag = d.PrueferAmVortag.Distinct().Order().ToList();
        tag.WeitereHelfer = d.WeitereHelfer;
        tag.Lokal = Leer(d.Lokal);
        tag.Bemerkung = Leer(d.Bemerkung);
        db.SaveChanges();
        tx.Commit();
    }

    /// <summary>Löscht einen Prüfungstag samt Termin und Einsätzen. Abgelehnt, solange es Nennungen oder abgerechnete Einsätze gibt.</summary>
    public void PruefungstagLoeschen(int kursId, int pruefungstagId)
    {
        using var db = fabrik.CreateDbContext();
        var tag = db.Pruefungstage.Include(p => p.Termin).Single(p => p.Id == pruefungstagId && p.Termin!.KursId == kursId);
        if (db.Nennungen.Any(n => n.PruefungstagId == pruefungstagId))
            throw new InvalidOperationException("Für diesen Prüfungstag gibt es Nennungen. Bitte sie zuerst auf einen anderen Tag legen oder löschen.");
        if (db.Einsaetze.Any(e => e.TerminId == tag.TerminId && e.AbrechnungId != null))
            throw new InvalidOperationException("Für diesen Prüfungstag sind schon abgerechnete Einsätze erfasst.");
        db.Termine.Remove(tag.Termin!);
        db.SaveChanges();
    }

    /// <summary>Kursteilnehmer mit Prüferrolle: (PersonId, Name, Richternummer).</summary>
    public List<(int PersonId, string Name, string? Richternummer)> PrueferListe(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        return db.Kursrollen.AsNoTracking().Include(r => r.Person)
            .Where(r => r.KursId == kursId && r.Rolle == Rolle.Richter)
            .Select(r => r.Person!)
            .ToList()
            .OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .Select(p => (p.Id, $"{p.Vorname} {p.Nachname}".Trim(), p.Richternummer))
            .ToList();
    }

    // ---------- Nennungen ----------

    public List<NennungDaten> Nennungen(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        var tagIds = db.Pruefungstage.AsNoTracking().Where(p => p.Termin!.KursId == kursId).Select(p => p.Id).ToList();
        return db.Nennungen.AsNoTracking()
            .Include(n => n.Fuehrer).Include(n => n.Hund).ThenInclude(h => h!.Rasse)
            .Where(n => tagIds.Contains(n.PruefungstagId))
            .ToList()
            .OrderBy(n => n.GenanntAm).ThenBy(n => n.Id)
            .Select(n => new NennungDaten(
                n.Id, n.PruefungstagId, n.HundId, n.FuehrerId, n.GenanntAm,
                n.Gehorsam, n.NachsucheSchalenwild, n.NachsucheNiederwild, n.Gaeste, n.Nenngeld, n.NenngeldBezahltAm,
                n.Losnummer, n.Bemerkung,
                n.KursteilnahmeId, $"{n.Fuehrer!.Vorname} {n.Fuehrer.Nachname}".Trim(), n.Fuehrer.Kurzname, n.Fuehrer.Anzeigename,
                n.Hund!.Rufname, n.Hund.Zuchtbuchname, n.Hund.Rasse?.Bezeichnung, n.Hund.Geschlecht, n.Hund.Wurfdatum,
                n.Hund.ChipNummer, n.Hund.Zuchtbuchnummer, n.Fuehrer.Vereinsmitglied))
            .ToList();
    }

    /// <summary>
    /// Speichert eine Nennung. Ein Hund wird je Kurs nur einmal genannt (alle Fächer an einem Tag);
    /// zum Wechsel des Tages die bestehende Nennung ändern. Ist der Hund im Kurs angemeldet, wird die Kursteilnahme verknüpft.
    /// </summary>
    public int NennungSpeichern(int kursId, NennungDaten d)
    {
        if (!d.Gehorsam && !d.Schalenwild && !d.Niederwild) throw new InvalidOperationException("Bitte mindestens ein Prüfungsfach wählen.");
        if (d.Gaeste is < 0 or > 20) throw new InvalidOperationException("Die Zahl der Gäste muss zwischen 0 und 20 liegen.");
        if (d.Nenngeld < 0) throw new InvalidOperationException("Das Nenngeld darf nicht negativ sein.");

        using var db = fabrik.CreateDbContext();
        if (!db.Pruefungstage.Any(p => p.Id == d.PruefungstagId && p.Termin!.KursId == kursId))
            throw new InvalidOperationException("Den Prüfungstag gibt es nicht (mehr).");
        var hund = db.Hunde.AsNoTracking().Include(h => h.Brauchbarkeiten).SingleOrDefault(h => h.Id == d.HundId)
            ?? throw new InvalidOperationException("Bitte einen Hund wählen.");
        if (!db.Personen.Any(p => p.Id == d.FuehrerId)) throw new InvalidOperationException("Bitte einen Hundeführer wählen.");

        var tagIds = db.Pruefungstage.Where(p => p.Termin!.KursId == kursId).Select(p => p.Id).ToList();
        if (db.Nennungen.Any(n => tagIds.Contains(n.PruefungstagId) && n.HundId == d.HundId && n.Id != (d.Id ?? 0)))
            throw new InvalidOperationException($"{hund.Rufname} ist schon genannt. Für einen anderen Tag bitte die bestehende Nennung ändern.");

        var n = d.Id is int id
            ? db.Nennungen.Single(x => x.Id == id && tagIds.Contains(x.PruefungstagId))
            : db.Nennungen.Add(new Nennung { GenanntAm = d.GenanntAm == default ? DateTimeOffset.Now : d.GenanntAm }).Entity;

        // Wechsel des Tages: das Los gilt nur für den alten Tag
        if (n.Id != 0 && n.PruefungstagId != d.PruefungstagId) n.Losnummer = null;

        var faecher = new P.Ausbildung(d.Gehorsam, d.Schalenwild, d.Niederwild, hund.Brauchbarkeiten.Count > 0).Bereinigt();
        n.PruefungstagId = d.PruefungstagId;
        n.HundId = d.HundId;
        n.FuehrerId = d.FuehrerId;
        n.KursteilnahmeId = db.Kursteilnahmen.Where(t => t.KursId == kursId && t.HundId == d.HundId).Select(t => (int?)t.Id).FirstOrDefault();
        if (d.Id is not null && d.GenanntAm != default) n.GenanntAm = d.GenanntAm;
        n.Gehorsam = faecher.Gehorsam;
        n.NachsucheSchalenwild = faecher.Schalenwild;
        n.NachsucheNiederwild = faecher.Niederwild;
        n.Gaeste = d.Gaeste;
        n.Nenngeld = Math.Round(d.Nenngeld, 2, MidpointRounding.AwayFromZero);
        n.NenngeldBezahltAm = d.NenngeldBezahltAm;
        n.Bemerkung = Leer(d.Bemerkung);
        db.SaveChanges();
        return n.Id;
    }

    public void NennungLoeschen(int kursId, int nennungId)
    {
        using var db = fabrik.CreateDbContext();
        var tagIds = db.Pruefungstage.Where(p => p.Termin!.KursId == kursId).Select(p => p.Id).ToList();
        db.Nennungen.Where(n => n.Id == nennungId && tagIds.Contains(n.PruefungstagId)).ExecuteDelete();
    }

    public void NenngeldBezahltSetzen(int kursId, int nennungId, DateOnly? am)
    {
        using var db = fabrik.CreateDbContext();
        var tagIds = db.Pruefungstage.Where(p => p.Termin!.KursId == kursId).Select(p => p.Id).ToList();
        var n = db.Nennungen.Single(x => x.Id == nennungId && tagIds.Contains(x.PruefungstagId));
        n.NenngeldBezahltAm = am;
        db.SaveChanges();
    }

    /// <summary>Setzt die Startreihenfolge eines Prüfungstags (Los 1, 2, …). Nennungen, die nicht aufgeführt sind, verlieren ihr Los.</summary>
    public void LoseSetzen(int kursId, int pruefungstagId, IReadOnlyList<int> nennungIdsInReihenfolge)
    {
        using var db = fabrik.CreateDbContext();
        if (!db.Pruefungstage.Any(p => p.Id == pruefungstagId && p.Termin!.KursId == kursId))
            throw new InvalidOperationException("Den Prüfungstag gibt es nicht (mehr).");
        var los = nennungIdsInReihenfolge.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i + 1);
        foreach (var n in db.Nennungen.Where(n => n.PruefungstagId == pruefungstagId).ToList())
            n.Losnummer = los.TryGetValue(n.Id, out var nr) ? nr : null;
        db.SaveChanges();
    }
}
