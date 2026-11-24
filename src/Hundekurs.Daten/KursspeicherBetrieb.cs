using Hundekurs.Domain;
using Microsoft.EntityFrameworkCore;
using P = Hundekurs.Planung;

namespace Hundekurs.Daten;

public sealed record KursEinstellungen(int KursId, string Bezeichnung, int? Jahr, decimal KmSatz, int? GehorsamGelaendeId, int? ApportierGelaendeId, decimal Nenngeld = 0);

public sealed record GelaendeDaten(int? Id, string Bezeichnung, IReadOnlyList<int> FachIds, int? RevierId, string? Treffpunkt, double? Breite, double? Laenge, bool Aktiv = true);

public sealed record NebenterminDaten(
    int Id, DateOnly Datum, TimeOnly? Uhrzeit, Terminstatus Status, string? Notiz, int? GelaendeId,
    IReadOnlySet<int> Anwesend, IReadOnlySet<int> Erfasst, IReadOnlySet<int> Ausbilder);

public sealed record Anwesenheitszahlen(int Anwesend, int Stattgefunden, int Ausstehend);

public sealed record EinsatzZeile(
    int Id, int PersonId, string Person, DateOnly Datum, Einsatzart Art, int? TerminId, int? RevierId, int? GelaendeId, string Ziel,
    double? KmBerechnet, DateTimeOffset? KmBerechnetAm, double? KmKorrigiert, string? Bemerkung, bool Abgeschlossen);

/// <summary>Teil 1, Apportierarbeit, Orte und Einsätze der Ausbilder.</summary>
public sealed partial class Kursspeicher
{
    // ---------- Kurs ----------

    public KursEinstellungen KursEinstellungenLaden(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        var k = db.Kurse.AsNoTracking().Single(x => x.Id == kursId);
        return new KursEinstellungen(k.Id, k.Bezeichnung, k.Jahr, k.KmSatz, k.GehorsamGelaendeId, k.ApportierGelaendeId, k.Nenngeld);
    }

    public void KursEinstellungenSpeichern(KursEinstellungen e)
    {
        if (e.KmSatz is < 0 or > 5) throw new InvalidOperationException("Der km-Satz muss zwischen 0 und 5 € liegen.");
        if (e.Nenngeld < 0) throw new InvalidOperationException("Das Nenngeld darf nicht negativ sein.");
        using var db = fabrik.CreateDbContext();
        var k = db.Kurse.Single(x => x.Id == e.KursId);
        k.Bezeichnung = string.IsNullOrWhiteSpace(e.Bezeichnung) ? k.Bezeichnung : e.Bezeichnung.Trim();
        k.KmSatz = e.KmSatz;
        k.Nenngeld = Math.Round(e.Nenngeld, 2, MidpointRounding.AwayFromZero);

        // Neuer Standardort gilt auch für alle noch geplanten Termine
        if (k.GehorsamGelaendeId != e.GehorsamGelaendeId)
            foreach (var t in db.Termine.Where(t => t.KursId == k.Id && t.Art == Terminart.Gehorsam && t.Status == Terminstatus.Geplant).ToList())
                t.UebungsgelaendeId = e.GehorsamGelaendeId;
        if (k.ApportierGelaendeId != e.ApportierGelaendeId)
            foreach (var t in db.Termine.Where(t => t.KursId == k.Id && t.Art == Terminart.Apportierarbeit && t.Status == Terminstatus.Geplant).ToList())
                t.UebungsgelaendeId = e.ApportierGelaendeId;

        k.GehorsamGelaendeId = e.GehorsamGelaendeId;
        k.ApportierGelaendeId = e.ApportierGelaendeId;
        db.SaveChanges();
    }

    /// <summary>
    /// Ersetzt die Termine für Gehorsam und Apportierarbeit durch die des Kalenders (samt Anwesenheiten und Einsätzen).
    /// Die Trainingstermine von Teil 2 legt <see cref="SaisonTermineAnlegen"/> an.
    /// </summary>
    public void NebentermineAnlegen(int kursId, P.Kurstermine kalender)
    {
        using var db = fabrik.CreateDbContext();
        using var tx = db.Database.BeginTransaction();
        var kurs = db.Kurse.Single(k => k.Id == kursId);

        var alte = db.Termine
            .Where(t => t.KursId == kursId && (t.Art == Terminart.Gehorsam || t.Art == Terminart.Apportierarbeit))
            .Select(t => t.Id).ToList();
        db.Anwesenheiten.Where(a => alte.Contains(a.TerminId)).ExecuteDelete();
        db.Einsaetze.Where(e => e.TerminId != null && alte.Contains(e.TerminId.Value)).ExecuteDelete();
        db.Termine.Where(t => alte.Contains(t.Id)).ExecuteDelete();

        db.Termine.AddRange(kalender.Gehorsam.Select(d => new Termin
        {
            KursId = kursId, Datum = d, Art = Terminart.Gehorsam, Status = Terminstatus.Geplant,
            UebungsgelaendeId = kurs.GehorsamGelaendeId,
        }));
        db.Termine.AddRange(kalender.Apportierarbeit.Select(d => new Termin
        {
            KursId = kursId, Datum = d, Art = Terminart.Apportierarbeit, Status = Terminstatus.Geplant,
            UebungsgelaendeId = kurs.ApportierGelaendeId, UhrzeitUebrigeFaecher = new TimeOnly(17, 30),
        }));

        kurs.Jahr = kalender.Jahr;
        kurs.Beginn = kalender.Gehorsam.Min();
        kurs.Ende = kalender.Training.Max();
        kurs.Bezeichnung = $"Ausbildungskurs {kalender.Jahr}";
        db.SaveChanges();
        tx.Commit();
    }

    /// <summary>
    /// Ersetzt die Termine der Apportierarbeit (z. B. weil Teil 2 später beginnt) und setzt das Kursende neu.
    /// Teil 1 bleibt unberührt. Abgelehnt, sobald für die Apportierarbeit schon etwas erfasst ist.
    /// </summary>
    public void ApportiertermineVerschieben(int kursId, IReadOnlyList<DateOnly> neu, DateOnly kursende)
    {
        using var db = fabrik.CreateDbContext();
        using var tx = db.Database.BeginTransaction();
        var kurs = db.Kurse.Single(k => k.Id == kursId);

        var alte = db.Termine
            .Where(t => t.KursId == kursId && t.Art == Terminart.Apportierarbeit)
            .Select(t => new { t.Id, t.Status }).ToList();
        var ids = alte.Select(t => t.Id).ToList();
        if (alte.Any(t => t.Status == Terminstatus.Durchgefuehrt)
            || db.Anwesenheiten.Any(a => ids.Contains(a.TerminId))
            || db.Einsaetze.Any(e => e.TerminId != null && ids.Contains(e.TerminId.Value)))
            throw new InvalidOperationException("Für die Apportierarbeit sind schon Anwesenheiten oder Ausbildereinsätze erfasst – die Termine lassen sich nicht mehr verschieben.");

        db.Termine.Where(t => ids.Contains(t.Id)).ExecuteDelete();
        db.Termine.AddRange(neu.Select(d => new Termin
        {
            KursId = kursId, Datum = d, Art = Terminart.Apportierarbeit, Status = Terminstatus.Geplant,
            UebungsgelaendeId = kurs.ApportierGelaendeId, UhrzeitUebrigeFaecher = new TimeOnly(17, 30),
        }));
        kurs.Ende = kursende;
        db.SaveChanges();
        tx.Commit();
    }

    // ---------- Gehorsam und Apportierarbeit ----------

    public List<NebenterminDaten> Nebentermine(int kursId, Terminart art)
    {
        using var db = fabrik.CreateDbContext();
        var termine = db.Termine.AsNoTracking().Where(t => t.KursId == kursId && t.Art == art).OrderBy(t => t.Datum).ToList();
        var ids = termine.Select(t => t.Id).ToList();
        var anwesenheiten = db.Anwesenheiten.AsNoTracking().Where(a => ids.Contains(a.TerminId)).ToList().ToLookup(a => a.TerminId);
        var einsaetze = db.Einsaetze.AsNoTracking().Where(e => e.TerminId != null && ids.Contains(e.TerminId.Value)).ToList().ToLookup(e => e.TerminId!.Value);

        return termine.Select(t => new NebenterminDaten(
            t.Id, t.Datum, t.UhrzeitUebrigeFaecher, t.Status, t.Notiz, t.UebungsgelaendeId,
            anwesenheiten[t.Id].Where(a => a.Anwesend).Select(a => a.KursteilnahmeId).ToHashSet(),
            anwesenheiten[t.Id].Select(a => a.KursteilnahmeId).ToHashSet(),
            einsaetze[t.Id].Select(e => e.PersonId).ToHashSet())).ToList();
    }

    /// <summary>Speichert die Anwesenheitsliste eines Termins und markiert ihn als durchgeführt.</summary>
    public void AnwesenheitSpeichern(int terminId, IReadOnlyCollection<int> teilnehmer, IReadOnlySet<int> anwesend, string? notiz)
    {
        using var db = fabrik.CreateDbContext();
        using var tx = db.Database.BeginTransaction();
        var termin = db.Termine.Single(t => t.Id == terminId);
        if (termin.Art is not (Terminart.Gehorsam or Terminart.Apportierarbeit))
            throw new InvalidOperationException("Anwesenheitslisten gibt es nur für Gehorsam und Apportierarbeit.");

        db.Anwesenheiten.Where(a => a.TerminId == terminId).ExecuteDelete();
        db.Anwesenheiten.AddRange(teilnehmer.Distinct().Select(id => new Anwesenheit { TerminId = terminId, KursteilnahmeId = id, Anwesend = anwesend.Contains(id) }));
        termin.Status = Terminstatus.Durchgefuehrt;
        termin.Notiz = Leer(notiz);
        db.SaveChanges();
        tx.Commit();
    }

    public void TerminNotizSpeichern(int terminId, string? notiz)
    {
        using var db = fabrik.CreateDbContext();
        var termin = db.Termine.Single(t => t.Id == terminId);
        termin.Notiz = Leer(notiz);
        db.SaveChanges();
    }

    /// <summary>Sagt einen Termin ab (zählt nicht für die Quote, Einsätze entfallen) oder plant ihn wieder ein.</summary>
    public void TerminStatusSetzen(int terminId, Terminstatus status)
    {
        using var db = fabrik.CreateDbContext();
        using var tx = db.Database.BeginTransaction();
        var termin = db.Termine.Single(t => t.Id == terminId);
        if (status == Terminstatus.Abgesagt)
        {
            if (db.Einsaetze.Any(e => e.TerminId == terminId && e.AbrechnungId != null))
                throw new InvalidOperationException("Für diesen Termin sind schon abgerechnete Einsätze erfasst.");
            db.Einsaetze.Where(e => e.TerminId == terminId).ExecuteDelete();
            db.Anwesenheiten.Where(a => a.TerminId == terminId).ExecuteDelete();
        }
        if (status == Terminstatus.Geplant)
            db.Anwesenheiten.Where(a => a.TerminId == terminId).ExecuteDelete();
        termin.Status = status;
        db.SaveChanges();
        tx.Commit();
    }

    /// <summary>Anwesenheit je Kursteilnahme in Teil 1: anwesend, stattgefunden (durchgeführt), noch ausstehend (geplant).</summary>
    public Dictionary<int, Anwesenheitszahlen> GehorsamZahlen(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        var termine = db.Termine.AsNoTracking().Where(t => t.KursId == kursId && t.Art == Terminart.Gehorsam).ToList();
        var stattgefunden = termine.Where(t => t.Status == Terminstatus.Durchgefuehrt).Select(t => t.Id).ToList();
        var ausstehend = termine.Count(t => t.Status is Terminstatus.Geplant or Terminstatus.Veroeffentlicht);

        var anwesend = db.Anwesenheiten.AsNoTracking()
            .Where(a => a.Anwesend && stattgefunden.Contains(a.TerminId))
            .GroupBy(a => a.KursteilnahmeId)
            .Select(g => new { g.Key, Anzahl = g.Count() })
            .ToDictionary(x => x.Key, x => x.Anzahl);

        return db.Kursteilnahmen.AsNoTracking()
            .Where(t => t.KursId == kursId)
            .Select(t => t.Id)
            .ToList()
            .ToDictionary(id => id, id => new Anwesenheitszahlen(anwesend.GetValueOrDefault(id), stattgefunden.Count, ausstehend));
    }

    // ---------- Orte ----------

    public List<GelaendeDaten> GelaendeListe()
    {
        using var db = fabrik.CreateDbContext();
        return db.Uebungsgelaende.AsNoTracking().Include(g => g.Faecher).OrderBy(g => g.Bezeichnung).ToList()
            .Select(g => new GelaendeDaten(g.Id, g.Bezeichnung, g.Faecher.Select(f => f.FachId).ToList(), g.RevierId, g.Treffpunkt, g.Breitengrad, g.Laengengrad, g.Aktiv))
            .ToList();
    }

    public int GelaendeSpeichern(GelaendeDaten d)
    {
        if (string.IsNullOrWhiteSpace(d.Bezeichnung)) throw new InvalidOperationException("Die Bezeichnung ist Pflicht.");
        if ((d.Breite is null) != (d.Laenge is null)) throw new InvalidOperationException("Bitte Breite und Länge zusammen angeben.");

        using var db = fabrik.CreateDbContext();
        var g = d.Id is int id
            ? db.Uebungsgelaende.Include(x => x.Faecher).Single(x => x.Id == id)
            : db.Uebungsgelaende.Add(new Uebungsgelaende { Bezeichnung = d.Bezeichnung }).Entity;
        if (g.Id != 0 && (g.Breitengrad != d.Breite || g.Laengengrad != d.Laenge))
            db.Standardstrecken.Where(s => s.UebungsgelaendeId == g.Id).ExecuteDelete();
        g.Bezeichnung = d.Bezeichnung.Trim();
        g.Faecher.RemoveAll(f => !d.FachIds.Contains(f.FachId));
        foreach (var fachId in d.FachIds.Distinct().Where(f => g.Faecher.All(x => x.FachId != f)))
            g.Faecher.Add(new GelaendeFach { FachId = fachId });
        g.RevierId = d.RevierId;
        g.Treffpunkt = Leer(d.Treffpunkt);
        g.Breitengrad = d.Breite;
        g.Laengengrad = d.Laenge;
        g.Aktiv = d.Aktiv;
        db.SaveChanges();
        return g.Id;
    }

    public Dictionary<int, P.Strecken.Koordinate?> RevierKoordinaten()
    {
        using var db = fabrik.CreateDbContext();
        return db.Reviere.AsNoTracking().ToList().ToDictionary(r => r.Id, r => Koordinate(r.Breitengrad, r.Laengengrad));
    }

    // ---------- Ausbilder: Koordinaten ----------

    /// <summary>Ausbilder, deren Adresse noch nicht in Koordinaten umgerechnet wurde: (PersonId, Adresse). Namen werden nicht herausgegeben.</summary>
    public List<(int PersonId, string Adresse)> AdressenOhneKoordinaten(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        return db.Kursrollen.AsNoTracking().Include(r => r.Person)
            .Where(r => r.KursId == kursId && r.Rolle == Rolle.Ausbilder)
            .Select(r => r.Person!)
            .ToList()
            .Where(p => p.Adresse is not null && (p.Breitengrad is null || p.GeokodierteAdresse != p.Adresse))
            .Select(p => (p.Id, p.Adresse!))
            .ToList();
    }

    public void PersonKoordinatenSetzen(int personId, P.Strecken.Koordinate k, string adresse)
    {
        using var db = fabrik.CreateDbContext();
        var p = db.Personen.Single(x => x.Id == personId);
        p.Breitengrad = k.Breite;
        p.Laengengrad = k.Laenge;
        p.GeokodierteAdresse = adresse;
        db.Standardstrecken.Where(s => s.PersonId == personId).ExecuteDelete();
        db.SaveChanges();
    }

    // ---------- Einsätze ----------

    /// <summary>
    /// Legt fest, welche Ausbilder an einem Termin im Einsatz waren. Bestehende Einsätze (mit km und Korrekturen) bleiben erhalten,
    /// entfernte Personen werden gelöscht, neue ohne km angelegt.
    /// </summary>
    public void EinsaetzeSetzen(int terminId, Einsatzart art, DateOnly datum, int? revierId, int? gelaendeId, IReadOnlySet<int> personIds)
    {
        using var db = fabrik.CreateDbContext();
        var vorhanden = db.Einsaetze.Where(e => e.TerminId == terminId && e.Art == art).ToList();

        foreach (var e in vorhanden.Where(e => !personIds.Contains(e.PersonId)))
        {
            if (e.AbrechnungId is not null) throw new InvalidOperationException("Ein abgerechneter Einsatz kann nicht mehr entfernt werden.");
            db.Einsaetze.Remove(e);
        }
        foreach (var e in vorhanden.Where(e => personIds.Contains(e.PersonId)))
        {
            if (e.Datum != datum || e.RevierId != revierId || e.UebungsgelaendeId != gelaendeId)
            {
                if (e.AbrechnungId is not null) continue;
                e.Datum = datum;
                e.RevierId = revierId;
                e.UebungsgelaendeId = gelaendeId;
                e.KmBerechnet = null;
                e.KmBerechnetAm = null;
            }
        }
        foreach (var personId in personIds.Where(p => vorhanden.All(e => e.PersonId != p)))
        {
            db.Einsaetze.Add(new Einsatz
            {
                PersonId = personId, TerminId = terminId, Art = art, Datum = datum, RevierId = revierId, UebungsgelaendeId = gelaendeId,
            });
        }
        db.SaveChanges();
    }

    public HashSet<int> EinsatzPersonen(int terminId, Einsatzart art)
    {
        using var db = fabrik.CreateDbContext();
        return db.Einsaetze.AsNoTracking().Where(e => e.TerminId == terminId && e.Art == art).Select(e => e.PersonId).ToHashSet();
    }

    public List<EinsatzZeile> Einsaetze(DateOnly von, DateOnly bis)
    {
        using var db = fabrik.CreateDbContext();
        var einsaetze = db.Einsaetze.AsNoTracking().Include(e => e.Person)
            .Where(e => e.Datum >= von && e.Datum <= bis)
            .ToList();
        var reviere = db.Reviere.AsNoTracking().ToDictionary(r => r.Id, r => r.Name);
        var gelaende = db.Uebungsgelaende.AsNoTracking().ToDictionary(g => g.Id, g => g.Bezeichnung);

        return einsaetze
            .OrderBy(e => e.Person!.Nachname).ThenBy(e => e.Person!.Vorname).ThenBy(e => e.Datum).ThenBy(e => e.Art)
            .Select(e => new EinsatzZeile(
                e.Id, e.PersonId, $"{e.Person!.Vorname} {e.Person.Nachname}".Trim(), e.Datum, e.Art, e.TerminId, e.RevierId, e.UebungsgelaendeId,
                e.UebungsgelaendeId is int g && gelaende.TryGetValue(g, out var gn) ? gn
                    : e.RevierId is int r && reviere.TryGetValue(r, out var rn) ? rn : "–",
                e.KmBerechnet, e.KmBerechnetAm, e.KmKorrigiert, e.Bemerkung, e.AbrechnungId is not null))
            .ToList();
    }

    /// <summary>Koordinaten von Start (Wohnort) und Ziel eines Einsatzes; null, wenn eines fehlt.</summary>
    public (P.Strecken.Koordinate Von, P.Strecken.Koordinate Nach)? Fahrtkoordinaten(int einsatzId)
    {
        using var db = fabrik.CreateDbContext();
        var e = db.Einsaetze.AsNoTracking().Include(x => x.Person).Single(x => x.Id == einsatzId);
        var von = Koordinate(e.Person!.Breitengrad, e.Person.Laengengrad);
        P.Strecken.Koordinate? nach = null;
        if (e.UebungsgelaendeId is int g)
        {
            var gelaende = db.Uebungsgelaende.AsNoTracking().Single(x => x.Id == g);
            nach = Koordinate(gelaende.Breitengrad, gelaende.Laengengrad);
        }
        else if (e.RevierId is int r)
        {
            var revier = db.Reviere.AsNoTracking().Single(x => x.Id == r);
            nach = Koordinate(revier.Breitengrad, revier.Laengengrad);
        }
        return von is not null && nach is not null ? (von, nach) : null;
    }

    /// <summary>Zwischengespeicherte Strecke, sofern jünger als <paramref name="hoechstalter"/>.</summary>
    public double? StreckeAusZwischenspeicher(int einsatzId, TimeSpan hoechstalter)
    {
        using var db = fabrik.CreateDbContext();
        var e = db.Einsaetze.AsNoTracking().Single(x => x.Id == einsatzId);
        var grenze = DateTimeOffset.Now - hoechstalter;
        var (personId, gelaendeId) = (e.PersonId, e.UebungsgelaendeId);
        var revierId = gelaendeId is null ? e.RevierId : null;
        return db.Standardstrecken.AsNoTracking()
            .Where(s => s.PersonId == personId && s.RevierId == revierId && s.UebungsgelaendeId == gelaendeId)
            .ToList()
            .Where(s => s.BerechnetAm >= grenze)
            .OrderByDescending(s => s.BerechnetAm)
            .Select(s => (double?)s.Kilometer)
            .FirstOrDefault();
    }

    /// <summary>Setzt die berechnete Strecke eines Einsatzes und legt sie im Zwischenspeicher ab.</summary>
    public void EinsatzKmBerechnetSetzen(int einsatzId, double km, bool zwischenspeichern)
    {
        using var db = fabrik.CreateDbContext();
        var e = db.Einsaetze.Single(x => x.Id == einsatzId);
        if (e.AbrechnungId is not null) return;
        e.KmBerechnet = km;
        e.KmBerechnetAm = DateTimeOffset.Now;
        if (zwischenspeichern)
        {
            var (personId, gelaendeId) = (e.PersonId, e.UebungsgelaendeId);
            var revierId = gelaendeId is null ? e.RevierId : null;
            db.Standardstrecken.Where(s => s.PersonId == personId && s.RevierId == revierId && s.UebungsgelaendeId == gelaendeId).ExecuteDelete();
            db.Standardstrecken.Add(new Standardstrecke
            {
                PersonId = e.PersonId, RevierId = revierId, UebungsgelaendeId = e.UebungsgelaendeId, Kilometer = km, BerechnetAm = DateTimeOffset.Now,
            });
        }
        db.SaveChanges();
    }

    public void EinsatzKorrigieren(int einsatzId, double? kmEinfach, string? bemerkung)
    {
        if (kmEinfach is < 0 or > 1000) throw new InvalidOperationException("Bitte eine einfache Strecke zwischen 0 und 1000 km angeben.");
        using var db = fabrik.CreateDbContext();
        var e = db.Einsaetze.Single(x => x.Id == einsatzId);
        if (e.AbrechnungId is not null) throw new InvalidOperationException("Der Einsatz ist bereits abgerechnet.");
        e.KmKorrigiert = kmEinfach;
        e.Bemerkung = Leer(bemerkung);
        db.SaveChanges();
    }

    /// <summary>Einsätze ohne berechnete Strecke im Zeitraum (zum Nachberechnen).</summary>
    public List<int> EinsaetzeOhneKm(DateOnly von, DateOnly bis)
    {
        using var db = fabrik.CreateDbContext();
        return db.Einsaetze.AsNoTracking()
            .Where(e => e.Datum >= von && e.Datum <= bis && e.KmBerechnet == null && e.AbrechnungId == null)
            .Select(e => e.Id).ToList();
    }

    private static P.Strecken.Koordinate? Koordinate(double? breite, double? laenge) =>
        breite is double b && laenge is double l ? new P.Strecken.Koordinate(b, l) : null;
}
