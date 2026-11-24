using Hundekurs.Domain;
using Microsoft.EntityFrameworkCore;
using P = Hundekurs.Planung;

namespace Hundekurs.Daten;

public sealed record FachDaten(int? Id, string Bezeichnung, Fachbereich Bereich, int Reihenfolge, bool Aktiv = true);

/// <summary>Person mit Kontakt- und Adressdaten. <paramref name="Ausbilder"/> und <paramref name="Pruefer"/> sind Rollen im aktuellen Kurs.</summary>
public sealed record PersonDaten(
    int? Id, string Vorname, string Nachname, string? Strasse, string? Plz, string? Ort, string? Telefon, string? EMail,
    bool Vereinsmitglied, bool Ausbilder = false, bool Pruefer = false,
    IReadOnlyList<string>? Hunde = null, bool HatKoordinaten = false, Anrede Anrede = Anrede.KeineAngabe, string? Titel = null,
    string? Richternummer = null)
{
    public string Anzeigename => $"{Nachname}, {Titel} {Vorname}".Replace("  ", " ").Trim(' ', ',');

    /// <summary>Für Briefe und Listen: „Herr Dr. Max Muster“; bei divers oder ohne Angabe nur der Name.</summary>
    public string VollerName => string.Join(" ", new[] { AnredeText(Anrede), Titel, Vorname, Nachname }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public static string? AnredeText(Anrede a) => a switch { Anrede.Herr => "Herr", Anrede.Frau => "Frau", _ => null };
}

public sealed record BrauchbarkeitDaten(int? Id, DateOnly Datum, string? Ort, IReadOnlyList<int> FachIds, string? Bemerkung);

public sealed record HundDaten(
    int? Id, string Rufname, string? Zuchtbuchname, string? Rasse, DateOnly? Wurfdatum, Geschlecht? Geschlecht,
    string? ChipNummer, string? Zuchtbuchnummer, string? HeimtierausweisNummer, int? FuehrerId, int? EigentuemerId, string? Bemerkung,
    IReadOnlyList<BrauchbarkeitDaten> Brauchbarkeiten);

/// <summary>Fächer, Personen, Hunde und Kursgebühren.</summary>
public sealed partial class Kursspeicher
{
    public const string FachGehorsam = "Grundgehorsam (Teil 1)";
    public const string FachSchalenwild = "Nachsuche auf Schalenwild";
    public const string FachNiederwild = "Nachsuche auf Niederwild";

    /// <summary>Fächer der Brauchbarkeitsprüfung (Hessen), soweit im Kurs ausgebildet. Stöber- und Bauarbeit lassen sich ergänzen.</summary>
    private static readonly (string Name, Fachbereich Bereich)[] Grundfaecher =
    [
        (FachGehorsam, Fachbereich.Grundgehorsam),
        ("Freie Suche unter dem Schuss", Fachbereich.Grundgehorsam),
        ("Leinenführigkeit", Fachbereich.Grundgehorsam),
        ("Standruhe", Fachbereich.Grundgehorsam),
        ("Schussfestigkeit", Fachbereich.Grundgehorsam),
        (FachSchalenwild, Fachbereich.Nachsuche),
        (FachNiederwild, Fachbereich.Nachsuche),
    ];

    // ---------- Fächer ----------

    private static List<GelaendeFach> Zuordnen(params int[] fachIds) => fachIds.Select(id => new GelaendeFach { FachId = id }).ToList();

    private static void FaecherSicherstellen(HundekursDb db)
    {
        if (db.Faecher.Any()) return;
        var i = 0;
        foreach (var (name, bereich) in Grundfaecher)
            db.Faecher.Add(new Fach { Bezeichnung = name, Bereich = bereich, Reihenfolge = ++i * 10 });
        db.SaveChanges();
    }

    public List<FachDaten> FaecherListe()
    {
        using var db = fabrik.CreateDbContext();
        return db.Faecher.AsNoTracking().OrderBy(f => f.Reihenfolge).ThenBy(f => f.Bezeichnung).ToList()
            .Select(f => new FachDaten(f.Id, f.Bezeichnung, f.Bereich, f.Reihenfolge, f.Aktiv))
            .ToList();
    }

    public int FachSpeichern(FachDaten d)
    {
        if (string.IsNullOrWhiteSpace(d.Bezeichnung)) throw new InvalidOperationException("Die Bezeichnung ist Pflicht.");
        using var db = fabrik.CreateDbContext();
        var name = d.Bezeichnung.Trim();
        if (db.Faecher.Any(f => f.Bezeichnung == name && f.Id != (d.Id ?? 0)))
            throw new InvalidOperationException($"Das Fach „{name}“ gibt es schon.");
        var fach = d.Id is int id ? db.Faecher.Single(f => f.Id == id) : db.Faecher.Add(new Fach { Bezeichnung = name }).Entity;
        fach.Bezeichnung = name;
        fach.Bereich = d.Bereich;
        fach.Reihenfolge = d.Id is null && d.Reihenfolge == 0 ? (db.Faecher.Max(f => (int?)f.Reihenfolge) ?? 0) + 10 : d.Reihenfolge;
        fach.Aktiv = d.Aktiv;
        db.SaveChanges();
        return fach.Id;
    }

    // ---------- Personen ----------

    public List<PersonDaten> PersonenListe(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        var rollen = db.Kursrollen.AsNoTracking().Where(r => r.KursId == kursId).ToList().ToLookup(r => r.PersonId, r => r.Rolle);
        var hunde = db.Hunde.AsNoTracking().ToList();
        return db.Personen.AsNoTracking()
            .Where(p => p.AnonymisiertAm == null)
            .OrderBy(p => p.Nachname).ThenBy(p => p.Vorname)
            .ToList()
            .Select(p => new PersonDaten(p.Id, p.Vorname, p.Nachname, p.Strasse, p.Plz, p.Ort, p.Telefon, p.EMail, p.Vereinsmitglied,
                rollen[p.Id].Contains(Rolle.Ausbilder), rollen[p.Id].Contains(Rolle.Richter),
                hunde.Where(h => h.FuehrerId == p.Id || h.EigentuemerId == p.Id)
                    .Select(h => h.Rufname + (h.EigentuemerId == p.Id && h.FuehrerId != p.Id ? " (Eigentum)" : "")).ToList(),
                p.Breitengrad is not null && p.Laengengrad is not null, p.Anrede, p.Titel, p.Richternummer))
            .ToList();
    }

    public int PersonSpeichern(int kursId, PersonDaten d)
    {
        if (string.IsNullOrWhiteSpace(d.Nachname)) throw new InvalidOperationException("Der Nachname ist Pflicht.");
        using var db = fabrik.CreateDbContext();
        var person = d.Id is int id
            ? db.Personen.Single(p => p.Id == id)
            : db.Personen.Add(new Person { Vorname = d.Vorname.Trim(), Nachname = d.Nachname.Trim() }).Entity;
        person.Anrede = d.Anrede;
        person.Titel = Leer(d.Titel);
        person.Vorname = d.Vorname.Trim();
        person.Nachname = d.Nachname.Trim();
        person.Telefon = Leer(d.Telefon);
        person.EMail = Leer(d.EMail);
        person.Vereinsmitglied = d.Vereinsmitglied;
        person.Richternummer = d.Pruefer ? Leer(d.Richternummer) : person.Richternummer;
        AdresseSetzen(db, person, d.Strasse, d.Plz, d.Ort);
        db.SaveChanges();

        RolleSetzen(db, kursId, person.Id, Rolle.Ausbilder, d.Ausbilder);
        RolleSetzen(db, kursId, person.Id, Rolle.Richter, d.Pruefer);
        db.SaveChanges();
        return person.Id;
    }

    /// <summary>Löscht eine Person, sofern sie nirgends mehr vorkommt (Kurs, Hund, Rolle, Einsatz).</summary>
    public void PersonLoeschen(int personId)
    {
        using var db = fabrik.CreateDbContext();
        if (db.Kursteilnahmen.Any(x => x.FuehrerId == personId))
            throw new InvalidOperationException("Die Person ist als Hundeführer in einem Kurs angemeldet.");
        if (db.Einsaetze.Any(e => e.PersonId == personId))
            throw new InvalidOperationException("Für die Person sind Einsätze (km) erfasst.");
        if (db.Nennungen.Any(n => n.FuehrerId == personId))
            throw new InvalidOperationException("Die Person ist als Hundeführer zu einer Prüfung genannt.");
        if (db.Auslagen.Any(a => a.PersonId == personId))
            throw new InvalidOperationException("Für die Person sind Auslagen erfasst.");
        if (db.Kursrollen.Any(r => r.PersonId == personId))
            throw new InvalidOperationException("Die Person hat noch eine Rolle im Kurs (Ausbilder oder Prüfer).");
        foreach (var h in db.Hunde.Where(h => h.FuehrerId == personId || h.EigentuemerId == personId).ToList())
        {
            if (h.FuehrerId == personId) h.FuehrerId = null;
            if (h.EigentuemerId == personId) h.EigentuemerId = null;
        }
        db.Standardstrecken.Where(s => s.PersonId == personId).ExecuteDelete();
        db.Personen.Remove(db.Personen.Single(p => p.Id == personId));
        db.SaveChanges();
    }

    private static void RolleSetzen(HundekursDb db, int kursId, int personId, Rolle rolle, bool an)
    {
        var vorhanden = db.Kursrollen.Where(r => r.KursId == kursId && r.PersonId == personId && r.Rolle == rolle).ToList();
        if (an && vorhanden.Count == 0) db.Kursrollen.Add(new Kursrolle { KursId = kursId, PersonId = personId, Rolle = rolle });
        if (!an) db.Kursrollen.RemoveRange(vorhanden);
    }

    /// <summary>Übernimmt die Adresse. Hat sie sich geändert, werden Koordinaten und zwischengespeicherte Strecken verworfen.</summary>
    private static void AdresseSetzen(HundekursDb db, Person person, string? strasse, string? plz, string? ort)
    {
        person.Strasse = Leer(strasse);
        person.Plz = Leer(plz);
        person.Ort = Leer(ort);
        if (person.Adresse == person.GeokodierteAdresse) return;
        person.Breitengrad = person.Laengengrad = null;
        person.GeokodierteAdresse = null;
        if (person.Id != 0) db.Standardstrecken.Where(s => s.PersonId == person.Id).ExecuteDelete();
    }

    // ---------- Hunde ----------

    public List<HundDaten> HundeListe()
    {
        using var db = fabrik.CreateDbContext();
        return db.Hunde.AsNoTracking().Include(h => h.Rasse).Include(h => h.Brauchbarkeiten)
            .OrderBy(h => h.Rufname)
            .ToList()
            .Select(h => new HundDaten(h.Id, h.Rufname, h.Zuchtbuchname, h.Rasse?.Bezeichnung, h.Wurfdatum, h.Geschlecht,
                h.ChipNummer, h.Zuchtbuchnummer, h.HeimtierausweisNummer, h.FuehrerId, h.EigentuemerId, h.Bemerkung,
                h.Brauchbarkeiten.OrderBy(b => b.Datum)
                    .Select(b => new BrauchbarkeitDaten(b.Id, b.Datum, b.Ort, b.FachIds, b.Bemerkung)).ToList()))
            .ToList();
    }

    public List<string> Rassen()
    {
        using var db = fabrik.CreateDbContext();
        return db.Rassen.AsNoTracking().OrderBy(r => r.Bezeichnung).Select(r => r.Bezeichnung).ToList();
    }

    public int HundSpeichern(HundDaten d)
    {
        if (string.IsNullOrWhiteSpace(d.Rufname)) throw new InvalidOperationException("Der Rufname ist Pflicht.");
        if (d.Brauchbarkeiten.Any(b => b.FachIds.Count == 0))
            throw new InvalidOperationException("Bitte bei der Brauchbarkeit die bestandenen Fächer ankreuzen.");

        using var db = fabrik.CreateDbContext();
        var hund = d.Id is int id
            ? db.Hunde.Include(h => h.Brauchbarkeiten).Single(h => h.Id == id)
            : db.Hunde.Add(new Hund { Rufname = d.Rufname.Trim() }).Entity;

        hund.Rufname = d.Rufname.Trim();
        hund.Zuchtbuchname = Leer(d.Zuchtbuchname);
        hund.Wurfdatum = d.Wurfdatum;
        hund.Geschlecht = d.Geschlecht;
        hund.ChipNummer = Leer(d.ChipNummer);
        hund.Zuchtbuchnummer = Leer(d.Zuchtbuchnummer);
        hund.HeimtierausweisNummer = Leer(d.HeimtierausweisNummer);
        hund.FuehrerId = d.FuehrerId;
        hund.EigentuemerId = d.EigentuemerId;
        hund.Bemerkung = Leer(d.Bemerkung);

        if (Leer(d.Rasse) is string rasse)
        {
            var r = db.Rassen.FirstOrDefault(x => x.Bezeichnung == rasse)
                ?? db.Rassen.Add(new Rasse { Bezeichnung = rasse, Kuerzel = rasse.Length <= 10 ? rasse : rasse[..10] }).Entity;
            hund.Rasse = r;
        }
        else
        {
            hund.RasseId = null;
            hund.Rasse = null;
        }

        hund.Brauchbarkeiten.Clear();
        foreach (var b in d.Brauchbarkeiten)
            hund.Brauchbarkeiten.Add(new Brauchbarkeit { Datum = b.Datum, Ort = Leer(b.Ort), FachIds = b.FachIds.Distinct().ToList(), Bemerkung = Leer(b.Bemerkung) });

        db.SaveChanges();
        return hund.Id;
    }

    public void HundLoeschen(int hundId)
    {
        using var db = fabrik.CreateDbContext();
        if (db.Kursteilnahmen.Any(t => t.HundId == hundId))
            throw new InvalidOperationException("Der Hund ist in einem Kurs angemeldet. Bitte zuerst dort abmelden.");
        if (db.Nennungen.Any(n => n.HundId == hundId))
            throw new InvalidOperationException("Der Hund ist zu einer Prüfung genannt. Bitte zuerst die Nennung löschen.");
        db.Hunde.Remove(db.Hunde.Single(h => h.Id == hundId));
        db.SaveChanges();
    }

    // ---------- Gebühren ----------

    /// <summary>Legt für einen Kurs ohne Preise die drei bisherigen Pakete an (Preise 0 €, mit Demodaten Beispielpreise).</summary>
    private static void GebuehrenSicherstellen(HundekursDb db, int kursId, bool demodaten)
    {
        if (db.Gebuehrenpakete.Any(p => p.KursId == kursId)) return;
        db.Gebuehrenpakete.AddRange(
            new Gebuehrenpaket { KursId = kursId, Bezeichnung = "Teil 1", Gehorsam = true, Reihenfolge = 10,
                Preis = demodaten ? 120 : 0, Mitgliederpreis = demodaten ? 100 : 0 },
            new Gebuehrenpaket { KursId = kursId, Bezeichnung = "Teil 1 + Teil 2", Gehorsam = true, NachsucheSchalenwild = true, Reihenfolge = 20,
                Preis = demodaten ? 220 : 0, Mitgliederpreis = demodaten ? 190 : 0 },
            new Gebuehrenpaket { KursId = kursId, Bezeichnung = "Teil 1 + Teil 2 + Apport", Gehorsam = true, NachsucheSchalenwild = true, NachsucheNiederwild = true,
                Reihenfolge = 30, Preis = demodaten ? 270 : 0, Mitgliederpreis = demodaten ? 230 : 0 });
        db.SaveChanges();
    }

    public List<P.Gebuehrenpaket> Gebuehrenpakete(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        return db.Gebuehrenpakete.AsNoTracking().Where(p => p.KursId == kursId).OrderBy(p => p.Reihenfolge).ThenBy(p => p.Id).ToList()
            .Select(p => new P.Gebuehrenpaket(p.Id, p.Bezeichnung, p.Gehorsam, p.NachsucheSchalenwild, p.NachsucheNiederwild, p.Preis, p.Mitgliederpreis))
            .ToList();
    }

    /// <summary>Speichert ein Paket (Id 0 = neu). Jede Kombination der Teile darf es nur einmal geben.</summary>
    public int GebuehrenpaketSpeichern(int kursId, P.Gebuehrenpaket d)
    {
        if (string.IsNullOrWhiteSpace(d.Bezeichnung)) throw new InvalidOperationException("Die Bezeichnung ist Pflicht.");
        if (!d.Gehorsam && !d.Schalenwild && !d.Niederwild) throw new InvalidOperationException("Bitte mindestens einen Teil ankreuzen.");
        if (d.Preis < 0 || d.Mitgliederpreis < 0) throw new InvalidOperationException("Preise dürfen nicht negativ sein.");

        using var db = fabrik.CreateDbContext();
        if (db.Gebuehrenpakete.Any(p => p.KursId == kursId && p.Id != d.Id
                && p.Gehorsam == d.Gehorsam && p.NachsucheSchalenwild == d.Schalenwild && p.NachsucheNiederwild == d.Niederwild))
            throw new InvalidOperationException("Für diese Kombination gibt es schon einen Preis.");

        var paket = d.Id != 0
            ? db.Gebuehrenpakete.Single(p => p.Id == d.Id && p.KursId == kursId)
            : db.Gebuehrenpakete.Add(new Gebuehrenpaket
            {
                KursId = kursId, Bezeichnung = d.Bezeichnung,
                Reihenfolge = (db.Gebuehrenpakete.Where(p => p.KursId == kursId).Max(p => (int?)p.Reihenfolge) ?? 0) + 10,
            }).Entity;
        paket.Bezeichnung = d.Bezeichnung.Trim();
        paket.Gehorsam = d.Gehorsam;
        paket.NachsucheSchalenwild = d.Schalenwild;
        paket.NachsucheNiederwild = d.Niederwild;
        paket.Preis = d.Preis;
        paket.Mitgliederpreis = d.Mitgliederpreis;
        db.SaveChanges();
        return paket.Id;
    }

    public void GebuehrenpaketLoeschen(int kursId, int paketId)
    {
        using var db = fabrik.CreateDbContext();
        db.Gebuehrenpakete.Where(p => p.Id == paketId && p.KursId == kursId).ExecuteDelete();
    }

    /// <summary>Vermerkt den Zahlungseingang (null = offen).</summary>
    public void BezahltSetzen(int kursteilnahmeId, DateOnly? bezahltAm)
    {
        using var db = fabrik.CreateDbContext();
        var t = db.Kursteilnahmen.Single(x => x.Id == kursteilnahmeId);
        t.BezahltAm = bezahltAm;
        db.SaveChanges();
    }
}
