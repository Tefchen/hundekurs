using Hundekurs.Domain;
using Microsoft.EntityFrameworkCore;
using P = Hundekurs.Planung;

namespace Hundekurs.Daten;

public sealed record Planungsdaten(int KursId, string Kursbezeichnung, List<P.Revier> Reviere, List<P.Gespann> Gespanne, P.PlanungsOptionen Optionen, P.Saisonplan Plan);

/// <summary>
/// Ein Gespann im Kurs. Beim Speichern legen leere <paramref name="FuehrerId"/>/<paramref name="HundId"/> neue Person bzw. neuen Hund an;
/// sonst wird die gewählte Person bzw. der gewählte Hund verknüpft. <paramref name="Vereinsmitglied"/> wird an der Person gespeichert;
/// <paramref name="BrauchbarkeitBestanden"/> und <paramref name="SchonImKurs"/> werden nur gelesen.
/// </summary>
public sealed record TeilnehmerDaten(
    int? KursteilnahmeId, string Vorname, string Nachname, string? EMail, string? Telefon, string Hund, P.Gruppe? Gruppe,
    bool Gehorsam = true, bool Schalenwild = false, bool Niederwild = false,
    bool ZulassungAusnahme = false, string? ZulassungBegruendung = null,
    bool Wiederholer = false, decimal? GebuehrVonHand = null, string? GebuehrHinweis = null, DateOnly? BezahltAm = null,
    int? FuehrerId = null, int? HundId = null,
    bool Vereinsmitglied = false, bool BrauchbarkeitBestanden = false, bool SchonImKurs = false)
{
    public P.Ausbildung Ausbildung => new(Gehorsam, Schalenwild, Niederwild, BrauchbarkeitBestanden);
}

public sealed record AusbilderDaten(
    int? PersonId, string Vorname, string Nachname, string? EMail, string? Telefon,
    string? Strasse = null, string? Plz = null, string? Ort = null, bool HatKoordinaten = false);

public sealed record RevierDaten(
    int? Id, string Name, string Kuerzel, P.Verwendung Verwendung, bool Aktiv, int AnzahlFaehrten,
    int MinRuhetage, int ZielRuhetage, IReadOnlyList<P.Sequenzpaar>? Paare, IReadOnlySet<int> Gesperrt,
    double? Breite = null, double? Laenge = null);

public sealed record Mailkontakt(int PersonId, string Name, string EMail, bool IstAusbilder);

public sealed record MailEinstellungen(string Einleitung, string Gruss, IReadOnlyList<string> Abgewaehlt);

public sealed record Historieneintrag(DateTimeOffset Zeitpunkt, string Benutzer, string Aktion, IReadOnlyList<string> Auswirkungen);

/// <summary>
/// Liest und schreibt Kursdaten. Übersetzt zwischen den Datenbank-Entitäten (Hundekurs.Domain)
/// und dem Modell des Planungskerns (Hundekurs.Planung). Ids werden 1:1 übernommen:
/// Gespann.Id = Kursteilnahme.Id, Termin.Id = Termin.Id, Revier.Id = Revier.Id.
/// </summary>
public sealed partial class Kursspeicher(IDbContextFactory<HundekursDb> fabrik)
{
    public const string StandardEinleitung = "Hallo zusammen,\n\nhier der Plan für das kommende Wochenende.";
    public const string StandardGruss = "Viele Grüße\nKursleitung";

    // ---------- Kurs ----------

    /// <summary>Liefert die Id des aktuellen Kurses und legt bei leerer Datenbank einen an (optional mit Demodaten).</summary>
    public int KursSicherstellen(bool demodaten)
    {
        using var db = fabrik.CreateDbContext();
        FaecherSicherstellen(db);
        var vorhanden = db.Kurse.OrderByDescending(k => k.Id).Select(k => (int?)k.Id).FirstOrDefault();
        if (vorhanden is int id)
        {
            GebuehrenSicherstellen(db, id, demodaten: false);
            return id;
        }

        var jahr = DateTime.Today.Year + (DateTime.Today.Month >= 9 ? 1 : 0);
        var kurs = new Kurs
        {
            Bezeichnung = $"Ausbildungskurs {jahr}",
            Beginn = P.Kurskalender.ErsterSonntagImMaerz(jahr),
            Ende = P.Kurskalender.ErsterSonntagImMaerz(jahr).AddDays(7 * 24),
            MailEinleitung = StandardEinleitung,
            MailGruss = StandardGruss,
        };
        db.Kurse.Add(kurs);
        db.SaveChanges();
        GebuehrenSicherstellen(db, kurs.Id, demodaten);

        if (demodaten)
        {
            // Erfundene Orte (Koordinaten frei gewählt, keine echten Reviere)
            var lage = new Dictionary<string, (double, double)>
            {
                ["N"] = (50.612, 9.241), ["O"] = (50.571, 9.338), ["S"] = (50.498, 9.262), ["W"] = (50.553, 9.152), ["P"] = (50.640, 9.300),
            };
            Revier? nord = null;
            foreach (var r in P.Demodaten.Reviere())
            {
                var revier = new Revier
                {
                    Name = r.Name,
                    Kuerzel = r.Kuerzel,
                    Verwendung = Abbilden(r.Verwendung),
                    Breitengrad = lage[r.Kuerzel].Item1,
                    Laengengrad = lage[r.Kuerzel].Item2,
                    Faehrten = Enumerable.Range(1, r.AnzahlFaehrten).Select(n => new Faehrte { Nummer = n }).ToList(),
                };
                db.Reviere.Add(revier);
                nord ??= revier;
            }
            var faecher = db.Faecher.ToDictionary(f => f.Bezeichnung, f => f.Id);
            var gehorsamsplatz = new Uebungsgelaende
            {
                Bezeichnung = "Gehorsamsplatz", Breitengrad = 50.585, Laengengrad = 9.205,
                Faecher = Zuordnen(faecher[FachGehorsam], faecher["Leinenführigkeit"], faecher["Standruhe"]),
            };
            var apportier = new Uebungsgelaende
            {
                Bezeichnung = "Niederwildgelände Nord", Breitengrad = 50.618, Laengengrad = 9.229,
                Faecher = Zuordnen(faecher[FachNiederwild], faecher["Freie Suche unter dem Schuss"], faecher["Schussfestigkeit"]),
            };
            nord!.Uebungsgelaende.Add(apportier);
            db.Uebungsgelaende.Add(gehorsamsplatz);
            db.SaveChanges();
            kurs.GehorsamGelaendeId = gehorsamsplatz.Id;
            kurs.ApportierGelaendeId = apportier.Id;
            foreach (var g in P.Demodaten.Gespanne())
            {
                var teile = g.Fuehrer.Split(' ', 2);
                var person = new Person
                {
                    Vorname = teile[0], Nachname = teile.Length > 1 ? teile[1] : "", EMail = $"hundefuehrer{g.Id:00}@example.org",
                    Vereinsmitglied = g.Id % 2 == 0,
                };
                var hund = new Hund { Rufname = g.Hund, Fuehrer = person };
                kurs.Teilnahmen.Add(new Kursteilnahme
                {
                    Hund = hund,
                    Fuehrer = person,
                    Gehorsam = true,
                    NachsucheSchalenwild = true,
                    NachsucheNiederwild = g.Id % 3 == 0,
                    Wiederholer = g.Id == 5,
                    Gruppe = g.Gruppe == P.Gruppe.A ? Trainingsgruppe.A : Trainingsgruppe.B,
                });
            }
            // Zwei zivile Gespanne nur für Teil 1
            foreach (var (vorname, hund) in new[] { ("Zivil", "Luna"), ("Zivil", "Bruno") })
            {
                var person = new Person { Vorname = vorname, Nachname = hund == "Luna" ? "15" : "16", EMail = $"zivil-{hund.ToLowerInvariant()}@example.org" };
                kurs.Teilnahmen.Add(new Kursteilnahme
                {
                    Hund = new Hund { Rufname = hund, Fuehrer = person },
                    Fuehrer = person,
                    Gehorsam = true,
                });
            }
            var wohnorte = new[] { (50.540, 9.180), (50.600, 9.400), (50.470, 9.300) };
            for (var i = 1; i <= 3; i++)
            {
                var person = new Person
                {
                    Vorname = "Ausbilder",
                    Nachname = i.ToString(),
                    EMail = $"ausbilder{i}@example.org",
                    Strasse = $"Beispielweg {i}",
                    Plz = "00000",
                    Ort = "Musterhausen",
                    Breitengrad = wohnorte[i - 1].Item1,
                    Laengengrad = wohnorte[i - 1].Item2,
                };
                person.GeokodierteAdresse = person.Adresse;
                kurs.Rollen.Add(new Kursrolle { Person = person, Rolle = Rolle.Ausbilder });
            }
        }

        db.SaveChanges();
        return kurs.Id;
    }

    // ---------- Planung laden und speichern ----------

    public Planungsdaten Laden(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        var kurs = db.Kurse.AsNoTracking().Single(k => k.Id == kursId);

        var reviere = db.Reviere.AsNoTracking()
            .Include(r => r.Faehrten)
            .Include(r => r.Sequenzpaare)
            .OrderBy(r => r.Id)
            .ToList()
            .Select(Abbilden)
            .ToList();

        var gespanne = db.Kursteilnahmen.AsNoTracking()
            .Include(t => t.Fuehrer)
            .Include(t => t.Hund)
            .Where(t => t.KursId == kursId && t.Gruppe != null)
            .OrderBy(t => t.Id)
            .ToList()
            .Select(t => new P.Gespann(t.Id, $"{t.Fuehrer!.Vorname} {t.Fuehrer.Nachname}".Trim(), t.Hund!.Rufname, Abbilden(t.Gruppe!.Value)))
            .ToList();

        var termine = db.Termine.AsNoTracking()
            .Where(t => t.KursId == kursId && t.Art == Terminart.Training && t.Status != Terminstatus.Abgesagt)
            .OrderBy(t => t.Datum)
            .ToList();
        var ids = termine.Select(t => t.Id).ToList();

        var helfer = db.Helferdienste.AsNoTracking().Where(x => ids.Contains(x.TerminId)).ToList().ToLookup(x => x.TerminId);
        var abweichungen = db.Terminabweichungen.AsNoTracking().Where(x => ids.Contains(x.TerminId)).ToList().ToLookup(x => x.TerminId);
        var zuteilungen = db.Faehrtenzuteilungen.AsNoTracking().Where(x => ids.Contains(x.TerminId)).ToList().ToLookup(x => x.TerminId);
        var laeufe = db.Faehrtenlaeufe.AsNoTracking().Where(x => ids.Contains(x.TerminId)).ToList().ToLookup(x => x.TerminId);

        var plan = new P.Saisonplan
        {
            Termine = termine.Select(t => new P.Termin
            {
                Id = t.Id,
                Datum = t.Datum,
                Gruppe = Abbilden(t.GruppeSchweiss ?? Trainingsgruppe.A),
                Status = Abbilden(t.Status),
                RevierId = t.RevierId,
                RevierFixiert = t.RevierFixiert,
                HelferFixiert = t.HelferFixiert,
                ZuteilungFixiert = t.ZuteilungFixiert,
                Helfer = helfer[t.Id].Select(h => h.KursteilnahmeId).ToHashSet(),
                Abgemeldet = abweichungen[t.Id].Where(a => a.Art == Abweichungsart.Abgemeldet).Select(a => a.KursteilnahmeId).ToHashSet(),
                Gaeste = abweichungen[t.Id].Where(a => a.Art == Abweichungsart.Gast).Select(a => a.KursteilnahmeId).ToHashSet(),
                Zuteilungen = zuteilungen[t.Id]
                    .Select(z => new P.Zuteilung(z.KursteilnahmeId, z.FaehrteNummer, z.Sequenz, z.ZweiterLaufAufFaehrte))
                    .OrderBy(z => z.Sequenz).ThenBy(z => z.FaehrteNr).ToList(),
                Ist = laeufe[t.Id].Select(l => new P.Lauf(l.KursteilnahmeId, l.FaehrteNummer, Abbilden(l.Ergebnis))).ToList(),
            }).ToList(),
        };

        var optionen = new P.PlanungsOptionen
        {
            HelferProTermin = kurs.HelferProTermin,
            HelferAusAndererGruppe = kurs.HelferAusAndererGruppe,
        };

        return new Planungsdaten(kurs.Id, kurs.Bezeichnung, reviere, gespanne, optionen, plan);
    }

    /// <summary>Schreibt den kompletten Plan (alle enthaltenen Termine) in einer Transaktion und protokolliert die Änderung.</summary>
    public void PlanSpeichern(int kursId, P.Saisonplan plan, string aktion, IEnumerable<string> auswirkungen)
    {
        using var db = fabrik.CreateDbContext();
        using var tx = db.Database.BeginTransaction();

        var ids = plan.Termine.Select(t => t.Id).ToList();
        var termine = db.Termine.Include(t => t.Vorbereitung).Where(t => ids.Contains(t.Id)).ToDictionary(t => t.Id);

        db.Helferdienste.Where(x => ids.Contains(x.TerminId)).ExecuteDelete();
        db.Terminabweichungen.Where(x => ids.Contains(x.TerminId)).ExecuteDelete();
        db.Faehrtenzuteilungen.Where(x => ids.Contains(x.TerminId)).ExecuteDelete();
        db.Faehrtenlaeufe.Where(x => ids.Contains(x.TerminId)).ExecuteDelete();

        foreach (var pt in plan.Termine)
        {
            if (!termine.TryGetValue(pt.Id, out var t))
                throw new InvalidOperationException($"Termin {pt.Id} gibt es nicht in der Datenbank.");
            if (t.KursId != kursId)
                throw new InvalidOperationException($"Termin {pt.Id} gehört zu einem anderen Kurs.");

            t.GruppeSchweiss = Abbilden(pt.Gruppe);
            t.Status = Abbilden(pt.Status);
            t.RevierId = pt.RevierId;
            t.RevierFixiert = pt.RevierFixiert;
            t.HelferFixiert = pt.HelferFixiert;
            t.ZuteilungFixiert = pt.ZuteilungFixiert;

            if (t.Vorbereitung is null)
                t.Vorbereitung = new Vorbereitungstermin { Datum = pt.Datum.AddDays(-1), RevierId = pt.RevierId };
            else
                t.Vorbereitung.RevierId = pt.RevierId;

            db.Helferdienste.AddRange(pt.Helfer.Select(h => new Helferdienst { TerminId = t.Id, KursteilnahmeId = h }));
            db.Terminabweichungen.AddRange(pt.Abgemeldet.Select(g => new Terminabweichung { TerminId = t.Id, KursteilnahmeId = g, Art = Abweichungsart.Abgemeldet }));
            db.Terminabweichungen.AddRange(pt.Gaeste.Select(g => new Terminabweichung { TerminId = t.Id, KursteilnahmeId = g, Art = Abweichungsart.Gast }));
            db.Faehrtenzuteilungen.AddRange(pt.Zuteilungen.Select(z => new Faehrtenzuteilung
            {
                TerminId = t.Id,
                KursteilnahmeId = z.GespannId,
                FaehrteNummer = z.FaehrteNr,
                Sequenz = z.Sequenz,
                ZweiterLaufAufFaehrte = z.ZweiterLaufAufFaehrte,
            }));
            db.Faehrtenlaeufe.AddRange(pt.Ist.Select(l => new Faehrtenlauf
            {
                TerminId = t.Id,
                KursteilnahmeId = l.GespannId,
                FaehrteNummer = l.FaehrteNr,
                Ergebnis = Abbilden(l.Ergebnis),
            }));
        }

        Protokoll(db, kursId, aktion, auswirkungen);
        db.SaveChanges();
        tx.Commit();
    }

    /// <summary>Ersetzt alle Trainingstermine des Kurses durch neue, wöchentliche Termine (noch ohne Einteilung).</summary>
    public P.Saisonplan SaisonTermineAnlegen(int kursId, DateOnly ersterTermin, int anzahl, P.Gruppe ersteGruppe)
    {
        using var db = fabrik.CreateDbContext();
        using var tx = db.Database.BeginTransaction();

        var alte = db.Termine.Where(t => t.KursId == kursId && t.Art == Terminart.Training).Select(t => t.Id).ToList();
        db.Helferdienste.Where(x => alte.Contains(x.TerminId)).ExecuteDelete();
        db.Terminabweichungen.Where(x => alte.Contains(x.TerminId)).ExecuteDelete();
        db.Faehrtenzuteilungen.Where(x => alte.Contains(x.TerminId)).ExecuteDelete();
        db.Faehrtenlaeufe.Where(x => alte.Contains(x.TerminId)).ExecuteDelete();
        db.Vorbereitungstermine.Where(x => alte.Contains(x.TerminId)).ExecuteDelete();
        db.Einsaetze.Where(x => x.TerminId != null && alte.Contains(x.TerminId.Value)).ExecuteDelete();
        db.Termine.Where(t => alte.Contains(t.Id)).ExecuteDelete();

        var andere = ersteGruppe == P.Gruppe.A ? Trainingsgruppe.B : Trainingsgruppe.A;
        var neue = Enumerable.Range(0, anzahl).Select(i => new Termin
        {
            KursId = kursId,
            Datum = ersterTermin.AddDays(7 * i),
            Art = Terminart.Training,
            Status = Terminstatus.Geplant,
            GruppeSchweiss = i % 2 == 0 ? Abbilden(ersteGruppe) : andere,
        }).ToList();
        db.Termine.AddRange(neue);
        db.SaveChanges();
        tx.Commit();

        return new P.Saisonplan
        {
            Termine = neue.Select(t => new P.Termin { Id = t.Id, Datum = t.Datum, Gruppe = Abbilden(t.GruppeSchweiss!.Value) }).ToList(),
        };
    }

    // ---------- Reviere ----------

    public int RevierSpeichern(RevierDaten d)
    {
        if (string.IsNullOrWhiteSpace(d.Name) || string.IsNullOrWhiteSpace(d.Kuerzel))
            throw new InvalidOperationException("Name und Kürzel sind Pflicht.");
        if (d.AnzahlFaehrten is < 1 or > 20)
            throw new InvalidOperationException("Die Anzahl der Fährten muss zwischen 1 und 20 liegen.");
        if (d.MinRuhetage < 0 || d.ZielRuhetage < d.MinRuhetage)
            throw new InvalidOperationException("Die angestrebte Ruhezeit darf nicht kürzer sein als die Mindest-Ruhezeit.");

        using var db = fabrik.CreateDbContext();
        var kuerzel = d.Kuerzel.Trim();
        if (db.Reviere.Any(r => r.Kuerzel == kuerzel && r.Id != (d.Id ?? 0)))
            throw new InvalidOperationException($"Das Kürzel „{kuerzel}“ ist schon vergeben.");

        var revier = d.Id is int id
            ? db.Reviere.Include(r => r.Faehrten).Include(r => r.Sequenzpaare).Single(r => r.Id == id)
            : db.Reviere.Add(new Revier { Name = d.Name, Kuerzel = kuerzel }).Entity;

        revier.Name = d.Name.Trim();
        revier.Kuerzel = kuerzel;
        revier.Verwendung = Abbilden(d.Verwendung);
        revier.Aktiv = d.Aktiv;
        revier.MinRuhetage = d.MinRuhetage;
        revier.ZielRuhetage = d.ZielRuhetage;
        if (revier.Breitengrad != d.Breite || revier.Laengengrad != d.Laenge)
        {
            revier.Breitengrad = d.Breite;
            revier.Laengengrad = d.Laenge;
            if (revier.Id != 0) db.Standardstrecken.Where(s => s.RevierId == revier.Id).ExecuteDelete();
        }

        foreach (var f in revier.Faehrten.Where(f => f.Nummer > d.AnzahlFaehrten).ToList())
            revier.Faehrten.Remove(f);
        for (var n = 1; n <= d.AnzahlFaehrten; n++)
        {
            var f = revier.Faehrten.FirstOrDefault(x => x.Nummer == n);
            if (f is null) revier.Faehrten.Add(f = new Faehrte { Nummer = n });
            f.Aktiv = !d.Gesperrt.Contains(n);
        }

        revier.Sequenzpaare.Clear();
        if (d.Paare is not null)
            foreach (var p in d.Paare)
                revier.Sequenzpaare.Add(new Sequenzpaar { ErsteFaehrte = p.Erste, ZweiteFaehrte = p.Zweite });

        db.SaveChanges();
        return revier.Id;
    }

    // ---------- Teilnehmer ----------

    public List<TeilnehmerDaten> TeilnehmerListe(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        var teilnahmen = db.Kursteilnahmen.AsNoTracking()
            .Include(t => t.Fuehrer).Include(t => t.Hund).ThenInclude(h => h!.Brauchbarkeiten)
            .Where(t => t.KursId == kursId)
            .OrderBy(t => t.Gruppe).ThenBy(t => t.Id)
            .ToList();
        var hundIds = teilnahmen.Select(t => t.HundId).ToList();
        var schonImKurs = db.Kursteilnahmen.AsNoTracking()
            .Where(t => t.KursId != kursId && hundIds.Contains(t.HundId))
            .Select(t => t.HundId).ToHashSet();

        return teilnahmen.Select(t => new TeilnehmerDaten(t.Id, t.Fuehrer!.Vorname, t.Fuehrer.Nachname, t.Fuehrer.EMail, t.Fuehrer.Telefon,
                t.Hund!.Rufname, t.Gruppe is Trainingsgruppe g ? Abbilden(g) : null,
                t.Gehorsam, t.NachsucheSchalenwild, t.NachsucheNiederwild, t.ZulassungAusnahme, t.ZulassungBegruendung,
                t.Wiederholer, t.GebuehrVonHand, t.GebuehrHinweis, t.BezahltAm,
                t.FuehrerId, t.HundId, t.Fuehrer.Vereinsmitglied, t.Hund.Brauchbarkeiten.Count > 0, schonImKurs.Contains(t.HundId)))
            .ToList();
    }

    public int TeilnehmerSpeichern(int kursId, TeilnehmerDaten d)
    {
        if (string.IsNullOrWhiteSpace(d.Nachname) || string.IsNullOrWhiteSpace(d.Hund))
            throw new InvalidOperationException("Nachname und Hund sind Pflicht.");
        if (!d.Gehorsam && !d.Schalenwild && !d.Niederwild)
            throw new InvalidOperationException("Bitte mindestens einen Ausbildungsteil wählen.");
        if (d.Gruppe is not null && !d.Schalenwild)
            throw new InvalidOperationException("Eine Schweißgruppe (A/B) gibt es nur mit Nachsuche auf Schalenwild.");
        if (d.GebuehrVonHand is < 0) throw new InvalidOperationException("Die Gebühr darf nicht negativ sein.");

        using var db = fabrik.CreateDbContext();
        Kursteilnahme t;
        if (d.KursteilnahmeId is int id)
        {
            t = db.Kursteilnahmen.Include(x => x.Fuehrer).Include(x => x.Hund).Single(x => x.Id == id && x.KursId == kursId);
        }
        else
        {
            t = new Kursteilnahme { KursId = kursId };
            db.Kursteilnahmen.Add(t);
        }

        // Hundeführer: gewählte Person verknüpfen oder neu anlegen
        if (d.FuehrerId is int fuehrerId)
        {
            if (t.FuehrerId != fuehrerId) t.Fuehrer = db.Personen.Single(p => p.Id == fuehrerId);
        }
        else
        {
            // Keine Person gewählt: neue Person anlegen (eine bisher verknüpfte bleibt unverändert erhalten)
            t.Fuehrer = new Person { Vorname = d.Vorname.Trim(), Nachname = d.Nachname.Trim() };
        }

        // Hund: gewählten Hund verknüpfen oder neu anlegen
        if (d.HundId is int hundId)
        {
            if (t.HundId != hundId) t.Hund = db.Hunde.Single(h => h.Id == hundId);
        }
        else
        {
            t.Hund = new Hund { Rufname = d.Hund.Trim(), Fuehrer = t.Fuehrer };
        }

        if (t.Hund!.Id != 0 && db.Kursteilnahmen.Any(x => x.KursId == kursId && x.HundId == t.Hund.Id && x.Id != t.Id))
            throw new InvalidOperationException($"{t.Hund.Rufname} ist in diesem Kurs schon angemeldet.");

        t.Fuehrer!.Vorname = d.Vorname.Trim();
        t.Fuehrer.Nachname = d.Nachname.Trim();
        t.Fuehrer.EMail = Leer(d.EMail);
        t.Fuehrer.Telefon = Leer(d.Telefon);
        t.Fuehrer.Vereinsmitglied = d.Vereinsmitglied;
        t.Hund.Rufname = d.Hund.Trim();
        if (t.Hund.FuehrerId is null && t.Hund.Fuehrer is null) t.Hund.Fuehrer = t.Fuehrer;

        var brauchbar = t.Hund.Id != 0 && db.Brauchbarkeiten.Any(b => b.HundId == t.Hund.Id);
        var ausbildung = new P.Ausbildung(d.Gehorsam, d.Schalenwild, d.Niederwild, brauchbar).Bereinigt();
        t.Gehorsam = ausbildung.Gehorsam;
        t.NachsucheSchalenwild = ausbildung.Schalenwild;
        t.NachsucheNiederwild = ausbildung.Niederwild;
        t.Gruppe = d.Gruppe is P.Gruppe g ? Abbilden(g) : null;
        t.ZulassungAusnahme = d.ZulassungAusnahme;
        t.ZulassungBegruendung = d.ZulassungAusnahme ? Leer(d.ZulassungBegruendung) : null;
        t.Wiederholer = d.Wiederholer;
        t.GebuehrVonHand = d.GebuehrVonHand;
        t.GebuehrHinweis = d.GebuehrVonHand is null ? null : Leer(d.GebuehrHinweis);
        t.BezahltAm = d.BezahltAm;

        db.SaveChanges();
        return t.Id;
    }

    /// <summary>Löscht eine Kursteilnahme samt Einteilungen. Person und Hund bleiben erhalten.</summary>
    public void TeilnehmerLoeschen(int kursteilnahmeId)
    {
        using var db = fabrik.CreateDbContext();
        var t = db.Kursteilnahmen.Single(x => x.Id == kursteilnahmeId);

        using (var tx = db.Database.BeginTransaction())
        {
            // Abhängige Einträge ausdrücklich löschen (unabhängig davon, ob SQLite Fremdschlüssel durchsetzt)
            db.Helferdienste.Where(x => x.KursteilnahmeId == kursteilnahmeId).ExecuteDelete();
            db.Terminabweichungen.Where(x => x.KursteilnahmeId == kursteilnahmeId).ExecuteDelete();
            db.Faehrtenzuteilungen.Where(x => x.KursteilnahmeId == kursteilnahmeId).ExecuteDelete();
            db.Faehrtenlaeufe.Where(x => x.KursteilnahmeId == kursteilnahmeId).ExecuteDelete();
            db.Nachweise.Where(x => x.KursteilnahmeId == kursteilnahmeId).ExecuteDelete();
            db.Anwesenheiten.Where(x => x.KursteilnahmeId == kursteilnahmeId).ExecuteDelete();
            db.Kursteilnahmen.Remove(t);
            db.SaveChanges();
            tx.Commit();
        }

        // Person und Hund bleiben als Stammdaten erhalten (Löschen unter „Personen“ bzw. „Hunde“).
    }

    // ---------- Ausbilder ----------

    public List<AusbilderDaten> AusbilderListe(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        return db.Kursrollen.AsNoTracking().Include(r => r.Person)
            .Where(r => r.KursId == kursId && r.Rolle == Rolle.Ausbilder)
            .OrderBy(r => r.Id)
            .ToList()
            .Select(r => new AusbilderDaten(r.PersonId, r.Person!.Vorname, r.Person.Nachname, r.Person.EMail, r.Person.Telefon,
                r.Person.Strasse, r.Person.Plz, r.Person.Ort, r.Person.Breitengrad is not null && r.Person.Laengengrad is not null))
            .ToList();
    }

    public int AusbilderSpeichern(int kursId, AusbilderDaten d)
    {
        if (string.IsNullOrWhiteSpace(d.Nachname)) throw new InvalidOperationException("Der Nachname ist Pflicht.");

        using var db = fabrik.CreateDbContext();
        Person person;
        if (d.PersonId is int id)
        {
            person = db.Personen.Single(p => p.Id == id);
        }
        else
        {
            person = new Person { Vorname = d.Vorname.Trim(), Nachname = d.Nachname.Trim() };
            db.Kursrollen.Add(new Kursrolle { KursId = kursId, Person = person, Rolle = Rolle.Ausbilder });
        }
        person.Vorname = d.Vorname.Trim();
        person.Nachname = d.Nachname.Trim();
        person.EMail = Leer(d.EMail);
        person.Telefon = Leer(d.Telefon);
        AdresseSetzen(db, person, d.Strasse, d.Plz, d.Ort);
        db.SaveChanges();
        return person.Id;
    }

    public void AusbilderEntfernen(int kursId, int personId)
    {
        using var db = fabrik.CreateDbContext();
        // Nur die Rolle entfällt; die Person bleibt (Einsätze und km-Abrechnungen verweisen auf sie).
        db.Kursrollen.Where(r => r.KursId == kursId && r.PersonId == personId && r.Rolle == Rolle.Ausbilder).ExecuteDelete();
    }

    // ---------- Wochenmail ----------

    public List<Mailkontakt> Mailkontakte(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        var teilnehmer = db.Kursteilnahmen.AsNoTracking().Include(t => t.Fuehrer)
            .Where(t => t.KursId == kursId && t.Fuehrer!.EMail != null)
            .OrderBy(t => t.Gruppe).ThenBy(t => t.Id)
            .Select(t => t.Fuehrer!)
            .ToList()
            .Select(p => new Mailkontakt(p.Id, $"{p.Vorname} {p.Nachname}".Trim(), p.EMail!, false));
        var ausbilder = db.Kursrollen.AsNoTracking().Include(r => r.Person)
            .Where(r => r.KursId == kursId && r.Rolle == Rolle.Ausbilder && r.Person!.EMail != null)
            .OrderBy(r => r.Id)
            .Select(r => r.Person!)
            .ToList()
            .Select(p => new Mailkontakt(p.Id, $"{p.Vorname} {p.Nachname}".Trim(), p.EMail!, true));

        return teilnehmer.Concat(ausbilder)
            .Where(k => !string.IsNullOrWhiteSpace(k.EMail))
            .DistinctBy(k => k.EMail.ToLowerInvariant())
            .ToList();
    }

    public MailEinstellungen MailEinstellungenLaden(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        var k = db.Kurse.AsNoTracking().Single(x => x.Id == kursId);
        return new MailEinstellungen(k.MailEinleitung ?? StandardEinleitung, k.MailGruss ?? StandardGruss, k.MailAbgewaehlt);
    }

    public void MailEinstellungenSpeichern(int kursId, MailEinstellungen e)
    {
        using var db = fabrik.CreateDbContext();
        var k = db.Kurse.Single(x => x.Id == kursId);
        k.MailEinleitung = e.Einleitung;
        k.MailGruss = e.Gruss;
        k.MailAbgewaehlt = e.Abgewaehlt.ToList();
        db.SaveChanges();
    }

    // ---------- Historie ----------

    public void Protokollieren(int kursId, string aktion, IEnumerable<string> auswirkungen)
    {
        using var db = fabrik.CreateDbContext();
        Protokoll(db, kursId, aktion, auswirkungen);
        db.SaveChanges();
    }

    public List<Historieneintrag> Historie(int anzahl = 200)
    {
        using var db = fabrik.CreateDbContext();
        return db.Aenderungsprotokoll.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Take(anzahl)
            .ToList()
            .Select(a => new Historieneintrag(a.Zeitpunkt, a.Benutzer, a.Aktion,
                string.IsNullOrEmpty(a.WerteNachher) ? Array.Empty<string>() : a.WerteNachher.Split('\n')))
            .ToList();
    }

    // ---------- Hilfen ----------

    private static void Protokoll(HundekursDb db, int kursId, string aktion, IEnumerable<string> auswirkungen) =>
        db.Aenderungsprotokoll.Add(new Aenderungsprotokoll
        {
            Zeitpunkt = DateTimeOffset.Now,
            Benutzer = db.Benutzer,
            Entitaet = "Kurs",
            EntitaetId = kursId,
            Aktion = aktion,
            WerteNachher = string.Join("\n", auswirkungen),
        });

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static P.Revier Abbilden(Revier r)
    {
        var anzahl = r.Faehrten.Count == 0 ? 6 : r.Faehrten.Max(f => f.Nummer);
        return new P.Revier
        {
            Id = r.Id,
            Name = r.Name,
            Kuerzel = r.Kuerzel,
            AnzahlFaehrten = anzahl,
            Verwendung = r.Verwendung switch
            {
                RevierVerwendung.Pruefung => P.Verwendung.Pruefung,
                RevierVerwendung.Beides => P.Verwendung.Beides,
                _ => P.Verwendung.Training,
            },
            Aktiv = r.Aktiv,
            MinRuhetage = r.MinRuhetage,
            ZielRuhetage = r.ZielRuhetage,
            Paare = r.Sequenzpaare.Count == 0 ? null : r.Sequenzpaare.OrderBy(p => p.Id).Select(p => new P.Sequenzpaar(p.ErsteFaehrte, p.ZweiteFaehrte)).ToList(),
            GesperrteFaehrten = Enumerable.Range(1, anzahl)
                .Where(n => r.Faehrten.FirstOrDefault(f => f.Nummer == n) is not { Aktiv: true })
                .ToHashSet(),
        };
    }

    private static RevierVerwendung Abbilden(P.Verwendung v) => v switch
    {
        P.Verwendung.Pruefung => RevierVerwendung.Pruefung,
        P.Verwendung.Beides => RevierVerwendung.Beides,
        _ => RevierVerwendung.Training,
    };

    private static P.Gruppe Abbilden(Trainingsgruppe g) => g == Trainingsgruppe.A ? P.Gruppe.A : P.Gruppe.B;
    private static Trainingsgruppe Abbilden(P.Gruppe g) => g == P.Gruppe.A ? Trainingsgruppe.A : Trainingsgruppe.B;

    private static P.TerminStatus Abbilden(Terminstatus s) => s switch
    {
        Terminstatus.Veroeffentlicht => P.TerminStatus.Veroeffentlicht,
        Terminstatus.Durchgefuehrt => P.TerminStatus.Durchgefuehrt,
        _ => P.TerminStatus.Geplant,
    };

    private static Terminstatus Abbilden(P.TerminStatus s) => s switch
    {
        P.TerminStatus.Veroeffentlicht => Terminstatus.Veroeffentlicht,
        P.TerminStatus.Durchgefuehrt => Terminstatus.Durchgefuehrt,
        _ => Terminstatus.Geplant,
    };

    private static P.LaufErgebnis Abbilden(Laufergebnis e) => e switch
    {
        Laufergebnis.Abbruch => P.LaufErgebnis.Abbruch,
        Laufergebnis.NichtAngetreten => P.LaufErgebnis.NichtAngetreten,
        _ => P.LaufErgebnis.Normal,
    };

    private static Laufergebnis Abbilden(P.LaufErgebnis e) => e switch
    {
        P.LaufErgebnis.Abbruch => Laufergebnis.Abbruch,
        P.LaufErgebnis.NichtAngetreten => Laufergebnis.NichtAngetreten,
        _ => Laufergebnis.Normal,
    };
}
