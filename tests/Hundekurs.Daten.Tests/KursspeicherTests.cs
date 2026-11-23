using Hundekurs.Daten;
using Hundekurs.Domain;
using Hundekurs.Planung;
using Hundekurs.Planung.Strecken;
using Gruppe = Hundekurs.Planung.Gruppe;
using Lauf = Hundekurs.Planung.Lauf;
using Revier = Hundekurs.Planung.Revier;
using Sequenzpaar = Hundekurs.Planung.Sequenzpaar;
using TerminStatus = Hundekurs.Planung.TerminStatus;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hundekurs.Daten.Tests;

/// <summary>Jeder Test bekommt eine eigene SQLite-Datenbank im Arbeitsspeicher.</summary>
public sealed class KursspeicherTests : IDisposable
{
    private static readonly DateOnly Start = Demodaten.ErsterSonntagImJuni(2027);

    private readonly SqliteConnection _verbindung;
    private readonly Kursspeicher _speicher;

    public KursspeicherTests()
    {
        _verbindung = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _verbindung.Open();
        var fabrik = new TestFabrik(_verbindung);
        using (var db = fabrik.CreateDbContext()) Datenbank.Vorbereiten(db);
        _speicher = new Kursspeicher(fabrik);
    }

    public void Dispose() => _verbindung.Dispose();

    private sealed class TestFabrik(SqliteConnection verbindung) : IDbContextFactory<HundekursDb>
    {
        public HundekursDb CreateDbContext() =>
            new(new DbContextOptionsBuilder<HundekursDb>().UseSqlite(verbindung).Options) { Benutzer = "test" };
    }

    private (int KursId, Schweissplaner Planer, Saisonplan Plan) SaisonMitPlan(int termine = 8)
    {
        var kursId = _speicher.KursSicherstellen(demodaten: true);
        var daten = _speicher.Laden(kursId);
        var planer = new Schweissplaner(daten.Reviere, daten.Gespanne, daten.Optionen);
        var leer = _speicher.SaisonTermineAnlegen(kursId, Start, termine, Gruppe.A);
        var plan = planer.NeuPlanen(leer, vollstaendig: true);
        _speicher.PlanSpeichern(kursId, plan, "Saison angelegt", []);
        return (kursId, planer, plan);
    }

    [Fact]
    public void Demodaten_werden_angelegt_und_geladen()
    {
        var kursId = _speicher.KursSicherstellen(demodaten: true);
        var daten = _speicher.Laden(kursId);

        Assert.Equal(5, daten.Reviere.Count);
        Assert.All(daten.Reviere, r => Assert.Equal(6, r.AnzahlFaehrten));
        Assert.Single(daten.Reviere, r => r.Verwendung == Verwendung.Pruefung);
        Assert.Equal(14, daten.Gespanne.Count);
        Assert.Equal(7, daten.Gespanne.Count(g => g.Gruppe == Gruppe.A));
        Assert.Empty(daten.Plan.Termine);
        Assert.Equal(3, _speicher.AusbilderListe(kursId).Count);
        Assert.Equal(19, _speicher.Mailkontakte(kursId).Count); // 14 jagdlich, 2 zivil, 3 Ausbilder
        Assert.Equal(16, _speicher.TeilnehmerListe(kursId).Count);
        Assert.Equal(2, _speicher.GelaendeListe().Count);
        Assert.Equal(kursId, _speicher.KursSicherstellen(demodaten: true));
    }

    [Fact]
    public void Plan_kommt_nach_Speichern_und_Laden_unveraendert_zurueck()
    {
        var (kursId, planer, plan) = SaisonMitPlan();
        var dienst = new Tauschdienst(planer);

        // Ein paar Änderungen, damit alle Teile des Plans vorkommen
        var t1 = plan.Chronologisch.First();
        var t2 = plan.Chronologisch.Skip(1).First();
        plan = dienst.Durchfuehren(plan, t1.Id, t1.Zuteilungen.Select((z, i) =>
            new Lauf(z.GespannId, z.FaehrteNr, i == 0 ? LaufErgebnis.Abbruch : LaufErgebnis.Normal))).Plan!;
        var abgemeldet = t2.Zuteilungen[0].GespannId;
        plan = dienst.Abmelden(plan, t2.Id, abgemeldet).Plan!;
        var gast = t1.Zuteilungen.Select(z => z.GespannId).First(g => !t2.Helfer.Contains(g));
        plan = dienst.Nachholen(plan, t2.Id, gast).Plan!;
        plan.Termin(t2.Id).Status = TerminStatus.Veroeffentlicht;
        plan.Termin(t2.Id).RevierFixiert = true;

        _speicher.PlanSpeichern(kursId, plan, "Test", ["Zeile 1", "Zeile 2"]);
        var geladen = _speicher.Laden(kursId).Plan;

        Assert.Equal(plan.Termine.Count, geladen.Termine.Count);
        foreach (var erwartet in plan.Chronologisch)
        {
            var t = geladen.Termin(erwartet.Id);
            Assert.Equal(erwartet.Datum, t.Datum);
            Assert.Equal(erwartet.Gruppe, t.Gruppe);
            Assert.Equal(erwartet.Status, t.Status);
            Assert.Equal(erwartet.RevierId, t.RevierId);
            Assert.Equal(erwartet.RevierFixiert, t.RevierFixiert);
            Assert.Equal(erwartet.ZuteilungFixiert, t.ZuteilungFixiert);
            Assert.Equal(erwartet.Helfer.Order().ToList(), t.Helfer.Order().ToList());
            Assert.Equal(erwartet.Abgemeldet.Order().ToList(), t.Abgemeldet.Order().ToList());
            Assert.Equal(erwartet.Gaeste.Order().ToList(), t.Gaeste.Order().ToList());
            Assert.Equal(
                erwartet.Zuteilungen.OrderBy(z => z.GespannId).ToList(),
                t.Zuteilungen.OrderBy(z => z.GespannId).ToList());
            Assert.Equal(erwartet.Ist.OrderBy(l => l.GespannId).ToList(), t.Ist.OrderBy(l => l.GespannId).ToList());
        }
        Assert.Contains(abgemeldet, geladen.Termin(t2.Id).Abgemeldet);
        Assert.Equal(TerminStatus.Durchgefuehrt, geladen.Termin(t1.Id).Status);
        Assert.Contains(gast, geladen.Termin(t2.Id).Gaeste);

        var historie = _speicher.Historie();
        Assert.Equal("Test", historie[0].Aktion);
        Assert.Equal(new[] { "Zeile 1", "Zeile 2" }, historie[0].Auswirkungen);
    }

    [Fact]
    public void Neue_Saison_ersetzt_die_alten_Termine()
    {
        var (kursId, _, _) = SaisonMitPlan(8);
        var neu = _speicher.SaisonTermineAnlegen(kursId, Start.AddDays(14), 4, Gruppe.B);

        var geladen = _speicher.Laden(kursId).Plan;
        Assert.Equal(4, geladen.Termine.Count);
        Assert.Equal(neu.Termine.Select(t => t.Id).Order().ToList(), geladen.Termine.Select(t => t.Id).Order().ToList());
        Assert.Equal(Gruppe.B, geladen.Chronologisch.First().Gruppe);
        Assert.All(geladen.Termine, t => Assert.Empty(t.Zuteilungen));
    }

    [Fact]
    public void Revier_mit_gesperrten_Faehrten_und_eigenen_Paaren()
    {
        _speicher.KursSicherstellen(demodaten: false);
        var paare = new List<Sequenzpaar> { new(1, 2), new(3, 4), new(5, 6) };
        var id = _speicher.RevierSpeichern(new RevierDaten(null, "Testrevier", "T", Verwendung.Beides, true, 6, 14, 28, paare, new HashSet<int> { 2 }));

        var r = _speicher.Laden(_speicher.KursSicherstellen(false)).Reviere.Single(x => x.Id == id);
        Assert.Equal(6, r.AnzahlFaehrten);
        Assert.Equal(new[] { 2 }, r.GesperrteFaehrten.Order().ToArray());
        Assert.Equal(paare, r.Paare);
        Assert.Equal(Verwendung.Beides, r.Verwendung);
        Assert.Equal(28, r.ZielRuhetage);

        _speicher.RevierSpeichern(new RevierDaten(id, "Testrevier", "T", Verwendung.Training, false, 4, 14, 21, null, new HashSet<int>()));
        r = _speicher.Laden(_speicher.KursSicherstellen(false)).Reviere.Single(x => x.Id == id);
        Assert.Equal(4, r.AnzahlFaehrten);
        Assert.Empty(r.GesperrteFaehrten);
        Assert.Null(r.Paare);
        Assert.False(r.Aktiv);
    }

    [Fact]
    public void Doppeltes_Kuerzel_wird_abgelehnt()
    {
        _speicher.KursSicherstellen(demodaten: true);
        Assert.Throws<InvalidOperationException>(() =>
            _speicher.RevierSpeichern(new RevierDaten(null, "Noch ein Nord", "N", Verwendung.Training, true, 6, 14, 21, null, new HashSet<int>())));
    }

    [Fact]
    public void Teilnehmer_anlegen_aendern_und_loeschen()
    {
        var (kursId, _, _) = SaisonMitPlan(4);

        var id = _speicher.TeilnehmerSpeichern(kursId, new TeilnehmerDaten(null, "Erika", "Muster", "erika@example.org", null, "Paula", Gruppe.B, Gehorsam: true, Schalenwild: true));
        Assert.Contains(_speicher.Laden(kursId).Gespanne, g => g.Id == id && g.Hund == "Paula" && g.Gruppe == Gruppe.B);

        var erika = _speicher.TeilnehmerListe(kursId).Single(t => t.KursteilnahmeId == id);
        _speicher.TeilnehmerSpeichern(kursId, erika with { Telefon = "0123", Gruppe = null, Schalenwild = false });
        Assert.DoesNotContain(_speicher.Laden(kursId).Gespanne, g => g.Id == id); // ohne Gruppe nicht in der Planung
        Assert.Contains(_speicher.TeilnehmerListe(kursId), t => t.KursteilnahmeId == id && t.Telefon == "0123");

        // Ein eingeplantes Gespann löschen: seine Einteilungen verschwinden mit
        var eingeplant = _speicher.Laden(kursId).Plan.Termine.SelectMany(t => t.Zuteilungen).First().GespannId;
        _speicher.TeilnehmerLoeschen(eingeplant);
        var daten = _speicher.Laden(kursId);
        Assert.DoesNotContain(daten.Gespanne, g => g.Id == eingeplant);
        Assert.DoesNotContain(daten.Plan.Termine.SelectMany(t => t.Zuteilungen), z => z.GespannId == eingeplant);
        Assert.DoesNotContain(daten.Plan.Termine.SelectMany(t => t.Helfer), h => h == eingeplant);
    }

    [Fact]
    public void Mail_Einstellungen_werden_gespeichert()
    {
        var kursId = _speicher.KursSicherstellen(demodaten: true);
        _speicher.MailEinstellungenSpeichern(kursId, new MailEinstellungen("Hallo", "Tschüss", ["a@example.org"]));

        var e = _speicher.MailEinstellungenLaden(kursId);
        Assert.Equal("Hallo", e.Einleitung);
        Assert.Equal("Tschüss", e.Gruss);
        Assert.Equal(new[] { "a@example.org" }, e.Abgewaehlt);
    }

    // ---------- Teil 1, Apportierarbeit, Einsätze ----------

    [Fact]
    public void Kursjahr_legt_Gehorsam_und_Apportierarbeit_mit_Standardorten_an()
    {
        var kursId = _speicher.KursSicherstellen(demodaten: true);
        var kalender = Kurskalender.Erzeugen(2027);
        _speicher.NebentermineAnlegen(kursId, kalender);

        var gehorsam = _speicher.Nebentermine(kursId, Terminart.Gehorsam);
        var apportier = _speicher.Nebentermine(kursId, Terminart.Apportierarbeit);
        var kurs = _speicher.KursEinstellungenLaden(kursId);

        Assert.Equal(11, gehorsam.Count);
        Assert.Equal(12, apportier.Count);
        Assert.All(gehorsam, t => Assert.Equal(kurs.GehorsamGelaendeId, t.GelaendeId));
        Assert.All(apportier, t => Assert.Equal(new TimeOnly(17, 30), t.Uhrzeit));
        Assert.Equal(2027, kurs.Jahr);

        // Zweites Anlegen ersetzt, statt zu verdoppeln
        _speicher.NebentermineAnlegen(kursId, kalender);
        Assert.Equal(11, _speicher.Nebentermine(kursId, Terminart.Gehorsam).Count);
    }

    [Fact]
    public void Anwesenheit_und_Absage_ergeben_die_richtigen_Zahlen()
    {
        var kursId = _speicher.KursSicherstellen(demodaten: true);
        _speicher.NebentermineAnlegen(kursId, Kurskalender.Erzeugen(2027));
        var termine = _speicher.Nebentermine(kursId, Terminart.Gehorsam);
        var teilnehmer = _speicher.TeilnehmerListe(kursId).Select(t => t.KursteilnahmeId!.Value).ToList();
        var (fleissig, selten) = (teilnehmer[0], teilnehmer[1]);

        // 7 Termine durchgeführt: „fleißig“ immer da, „selten“ nur zweimal
        for (var i = 0; i < 7; i++)
            _speicher.AnwesenheitSpeichern(termine[i].Id, teilnehmer, i < 2 ? new HashSet<int> { fleissig, selten } : new HashSet<int> { fleissig }, null);
        // ein Termin abgesagt
        _speicher.TerminStatusSetzen(termine[7].Id, Terminstatus.Abgesagt);

        var zahlen = _speicher.GehorsamZahlen(kursId);
        Assert.Equal(new Anwesenheitszahlen(7, 7, 3), zahlen[fleissig]);
        Assert.Equal(new Anwesenheitszahlen(2, 7, 3), zahlen[selten]);

        // Mit der Zulassungsregel: 10 Termine insgesamt → 6 nötig
        var stand = new Zulassungsstand(fleissig, 7, 7, 3, false);
        Assert.Equal(6, stand.Erforderlich);
        Assert.Equal(Ampel.Gruen, stand.Ampel);
        Assert.Equal(Ampel.Rot, new Zulassungsstand(selten, 2, 7, 3, false).Ampel);

        var gespeichert = _speicher.Nebentermine(kursId, Terminart.Gehorsam).First();
        Assert.Equal(Terminstatus.Durchgefuehrt, gespeichert.Status);
        Assert.Equal(teilnehmer.Count, gespeichert.Erfasst.Count);
    }

    [Fact]
    public void Einsaetze_behalten_Korrekturen_und_nutzen_den_Streckenspeicher()
    {
        var kursId = _speicher.KursSicherstellen(demodaten: true);
        _speicher.NebentermineAnlegen(kursId, Kurskalender.Erzeugen(2027));
        var termin = _speicher.Nebentermine(kursId, Terminart.Gehorsam).First();
        var ausbilder = _speicher.AusbilderListe(kursId).Select(a => a.PersonId!.Value).ToList();

        _speicher.EinsaetzeSetzen(termin.Id, Einsatzart.Gehorsam, termin.Datum, null, termin.GelaendeId, new HashSet<int> { ausbilder[0], ausbilder[1] });
        var einsaetze = _speicher.Einsaetze(termin.Datum, termin.Datum);
        Assert.Equal(2, einsaetze.Count);
        Assert.All(einsaetze, e => Assert.Equal("Gehorsamsplatz", e.Ziel));

        var erster = einsaetze.Single(e => e.PersonId == ausbilder[0]);
        Assert.NotNull(_speicher.Fahrtkoordinaten(erster.Id)); // Demodaten haben Wohnort- und Ortskoordinaten
        _speicher.EinsatzKmBerechnetSetzen(erster.Id, 12.3, zwischenspeichern: true);
        _speicher.EinsatzKorrigieren(erster.Id, 15, "Umleitung");

        // Erneutes Setzen mit einer weiteren Person: Korrektur bleibt, zweiter entfällt, dritter kommt dazu
        _speicher.EinsaetzeSetzen(termin.Id, Einsatzart.Gehorsam, termin.Datum, null, termin.GelaendeId, new HashSet<int> { ausbilder[0], ausbilder[2] });
        einsaetze = _speicher.Einsaetze(termin.Datum, termin.Datum);
        Assert.Equal(new[] { ausbilder[0], ausbilder[2] }.Order(), einsaetze.Select(e => e.PersonId).Order());
        var behalten = einsaetze.Single(e => e.PersonId == ausbilder[0]);
        Assert.Equal(12.3, behalten.KmBerechnet);
        Assert.Equal(15, behalten.KmKorrigiert);
        Assert.Equal("Umleitung", behalten.Bemerkung);

        // Streckenspeicher: gleiche Person, gleicher Ort, anderer Termin
        var zweiter = _speicher.Nebentermine(kursId, Terminart.Gehorsam)[1];
        _speicher.EinsaetzeSetzen(zweiter.Id, Einsatzart.Gehorsam, zweiter.Datum, null, zweiter.GelaendeId, new HashSet<int> { ausbilder[0] });
        var neu = _speicher.Einsaetze(zweiter.Datum, zweiter.Datum).Single();
        Assert.Equal(12.3, _speicher.StreckeAusZwischenspeicher(neu.Id, TimeSpan.FromDays(7)));
        Assert.Contains(neu.Id, _speicher.EinsaetzeOhneKm(zweiter.Datum, zweiter.Datum));

        // Absage löscht die Einsätze des Termins
        _speicher.TerminStatusSetzen(zweiter.Id, Terminstatus.Abgesagt);
        Assert.Empty(_speicher.Einsaetze(zweiter.Datum, zweiter.Datum));
    }

    [Fact]
    public void Geaenderte_Adresse_verwirft_Koordinaten_und_gespeicherte_Strecken()
    {
        var kursId = _speicher.KursSicherstellen(demodaten: true);
        var a = _speicher.AusbilderListe(kursId).First();
        Assert.True(a.HatKoordinaten);
        Assert.Empty(_speicher.AdressenOhneKoordinaten(kursId));

        _speicher.AusbilderSpeichern(kursId, a with { Strasse = "Neue Straße 5" });

        var geaendert = _speicher.AusbilderListe(kursId).Single(x => x.PersonId == a.PersonId);
        Assert.False(geaendert.HatKoordinaten);
        var offen = Assert.Single(_speicher.AdressenOhneKoordinaten(kursId));
        Assert.Equal(a.PersonId, offen.PersonId);
        Assert.StartsWith("Neue Straße 5", offen.Adresse);

        _speicher.PersonKoordinatenSetzen(a.PersonId!.Value, new Koordinate(50.5, 9.2), offen.Adresse);
        Assert.Empty(_speicher.AdressenOhneKoordinaten(kursId));
    }

    [Fact]
    public void Neuer_Standardort_gilt_fuer_geplante_Termine()
    {
        var kursId = _speicher.KursSicherstellen(demodaten: true);
        _speicher.NebentermineAnlegen(kursId, Kurskalender.Erzeugen(2027));
        var termine = _speicher.Nebentermine(kursId, Terminart.Gehorsam);
        var teilnehmer = _speicher.TeilnehmerListe(kursId).Select(t => t.KursteilnahmeId!.Value).ToList();
        _speicher.AnwesenheitSpeichern(termine[0].Id, teilnehmer, new HashSet<int>(), null);

        var neuerPlatz = _speicher.GelaendeSpeichern(new GelaendeDaten(null, "Ausweichplatz", [], null, null, 50.6, 9.1));
        _speicher.KursEinstellungenSpeichern(_speicher.KursEinstellungenLaden(kursId) with { GehorsamGelaendeId = neuerPlatz });

        var nachher = _speicher.Nebentermine(kursId, Terminart.Gehorsam);
        Assert.NotEqual(neuerPlatz, nachher[0].GelaendeId); // durchgeführt: bleibt
        Assert.All(nachher.Skip(1), t => Assert.Equal(neuerPlatz, t.GelaendeId));
    }
}
