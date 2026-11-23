using Hundekurs.Domain;
using Hundekurs.Planung;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Gebuehrenpaket = Hundekurs.Planung.Gebuehrenpaket;
using Gruppe = Hundekurs.Planung.Gruppe;

namespace Hundekurs.Daten.Tests;

/// <summary>Fächer, Personen, Hunde, Ausbildung und Gebühren.</summary>
public sealed class StammdatenTests : IDisposable
{
    private readonly SqliteConnection _verbindung;
    private readonly Kursspeicher _speicher;
    private readonly int _kursId;

    public StammdatenTests()
    {
        _verbindung = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _verbindung.Open();
        var fabrik = new TestFabrik(_verbindung);
        using (var db = fabrik.CreateDbContext()) Datenbank.Vorbereiten(db);
        _speicher = new Kursspeicher(fabrik);
        _kursId = _speicher.KursSicherstellen(demodaten: true);
    }

    public void Dispose() => _verbindung.Dispose();

    private sealed class TestFabrik(SqliteConnection verbindung) : IDbContextFactory<HundekursDb>
    {
        public HundekursDb CreateDbContext() =>
            new(new DbContextOptionsBuilder<HundekursDb>().UseSqlite(verbindung).Options) { Benutzer = "test" };
    }

    private int FachId(string name) => _speicher.FaecherListe().Single(f => f.Bezeichnung == name).Id!.Value;

    [Fact]
    public void Faecher_und_Gebuehren_werden_angelegt()
    {
        Assert.Contains(_speicher.FaecherListe(), f => f.Bezeichnung == Kursspeicher.FachNiederwild);
        Assert.Equal(3, _speicher.Gebuehrenpakete(_kursId).Count);
        Assert.All(_speicher.GelaendeListe(), g => Assert.NotEmpty(g.FachIds));
    }

    [Fact]
    public void Ort_mit_mehreren_Faechern()
    {
        var faecher = new[] { FachId("Schussfestigkeit"), FachId("Freie Suche unter dem Schuss") };
        var id = _speicher.GelaendeSpeichern(new GelaendeDaten(null, "Schießstand", faecher, null, null, null, null));
        Assert.Equal(faecher.Order(), _speicher.GelaendeListe().Single(g => g.Id == id).FachIds.Order());
    }

    [Fact]
    public void Person_mit_Doppelrolle_und_Hund_mit_Eigentuemer()
    {
        var ausbilder = _speicher.PersonSpeichern(_kursId, new PersonDaten(null, "Max", "Doppel", "Weg 1", "00000", "Ort", null, null,
            Vereinsmitglied: true, Ausbilder: true, Pruefer: true));
        var eigentuemer = _speicher.PersonSpeichern(_kursId, new PersonDaten(null, "Eva", "Eigen", null, null, null, null, null, false));

        var hund = _speicher.HundSpeichern(new HundDaten(null, "Asta", null, "Deutsch Drahthaar", new DateOnly(2024, 4, 1), Geschlecht.Huendin,
            "276000000000001", null, null, ausbilder, eigentuemer, null, []));

        var p = _speicher.PersonenListe(_kursId).Single(x => x.Id == ausbilder);
        Assert.True(p.Ausbilder && p.Pruefer && p.Vereinsmitglied);
        Assert.Contains(_speicher.AusbilderListe(_kursId), a => a.PersonId == ausbilder);
        Assert.Contains(_speicher.PersonenListe(_kursId).Single(x => x.Id == eigentuemer).Hunde!, h => h.Contains("Asta"));
        Assert.Equal("Deutsch Drahthaar", _speicher.HundeListe().Single(h => h.Id == hund).Rasse);

        // Mit Rolle nicht löschbar, ohne Rolle schon
        Assert.Throws<InvalidOperationException>(() => _speicher.PersonLoeschen(ausbilder));
        _speicher.PersonLoeschen(eigentuemer);
        Assert.Null(_speicher.HundeListe().Single(h => h.Id == hund).EigentuemerId);
    }

    [Fact]
    public void Teilnahme_mit_vorhandener_Person_und_Hund_Gehorsam_wird_Pflicht()
    {
        var person = _speicher.PersonSpeichern(_kursId, new PersonDaten(null, "Nina", "Neu", null, null, null, null, "nina@example.org", true));
        var hund = _speicher.HundSpeichern(new HundDaten(null, "Fido", null, null, null, null, null, null, null, person, null, null, []));

        var id = _speicher.TeilnehmerSpeichern(_kursId, new TeilnehmerDaten(null, "Nina", "Neu", "nina@example.org", null, "Fido", null,
            Gehorsam: false, Schalenwild: true, FuehrerId: person, HundId: hund, Vereinsmitglied: true));

        var t = _speicher.TeilnehmerListe(_kursId).Single(x => x.KursteilnahmeId == id);
        Assert.Equal(person, t.FuehrerId);
        Assert.Equal(hund, t.HundId);
        Assert.True(t.Gehorsam); // bei Schalenwild Pflicht
        Assert.True(t.Ausbildung.Jagdlich);

        // Derselbe Hund zweimal im Kurs geht nicht
        Assert.Throws<InvalidOperationException>(() => _speicher.TeilnehmerSpeichern(_kursId,
            new TeilnehmerDaten(null, "Nina", "Neu", null, null, "Fido", null, Gehorsam: true, FuehrerId: person, HundId: hund)));

        // Abmelden lässt Person und Hund bestehen
        _speicher.TeilnehmerLoeschen(id);
        Assert.Contains(_speicher.HundeListe(), h => h.Id == hund);
        Assert.Contains(_speicher.PersonenListe(_kursId), p => p.Id == person);
    }

    [Fact]
    public void Schweissgruppe_nur_mit_Schalenwild()
    {
        Assert.Throws<InvalidOperationException>(() => _speicher.TeilnehmerSpeichern(_kursId,
            new TeilnehmerDaten(null, "Otto", "Ohne", null, null, "Rex", Gruppe.A, Gehorsam: true, Niederwild: true)));
    }

    [Fact]
    public void Brauchbarkeit_befreit_bei_Niederwild_von_Teil1()
    {
        var hund = _speicher.HundSpeichern(new HundDaten(null, "Bella", null, null, null, null, null, null, null, null, null, null,
            [new BrauchbarkeitDaten(null, new DateOnly(2025, 9, 20), "Prüfungsrevier", [FachId("Leinenführigkeit"), FachId(Kursspeicher.FachNiederwild)], null)]));

        var id = _speicher.TeilnehmerSpeichern(_kursId, new TeilnehmerDaten(null, "Bea", "Befreit", null, null, "Bella", null,
            Gehorsam: false, Niederwild: true, HundId: hund));

        var t = _speicher.TeilnehmerListe(_kursId).Single(x => x.KursteilnahmeId == id);
        Assert.True(t.BrauchbarkeitBestanden);
        Assert.False(t.Gehorsam);
        Assert.False(t.Ausbildung.ZulassungErforderlich);
    }

    [Fact]
    public void Gebuehren_Pakete_und_bezahlt()
    {
        var neu = _speicher.GebuehrenpaketSpeichern(_kursId, new Gebuehrenpaket(0, "Nur Niederwild", false, false, true, 80m, 60m));
        Assert.Contains(_speicher.Gebuehrenpakete(_kursId), p => p.Id == neu && p.Mitgliederpreis == 60m);
        Assert.Throws<InvalidOperationException>(() =>
            _speicher.GebuehrenpaketSpeichern(_kursId, new Gebuehrenpaket(0, "Doppelt", false, false, true, 1m, 1m)));

        var t = _speicher.TeilnehmerListe(_kursId).First(x => x.Wiederholer);
        var gebuehr = Gebuehrenregel.Berechnen(_speicher.Gebuehrenpakete(_kursId), t.Ausbildung.Bereinigt(), t.Vereinsmitglied, t.Wiederholer);
        Assert.True(gebuehr.Ermittelt);
        Assert.Contains("Hälfte", gebuehr.Rechenweg);

        _speicher.BezahltSetzen(t.KursteilnahmeId!.Value, new DateOnly(2027, 3, 1));
        Assert.Equal(new DateOnly(2027, 3, 1), _speicher.TeilnehmerListe(_kursId).Single(x => x.KursteilnahmeId == t.KursteilnahmeId).BezahltAm);

        // Beim Bearbeiten des Gespanns bleibt der Vermerk erhalten
        var mitDatum = _speicher.TeilnehmerListe(_kursId).Single(x => x.KursteilnahmeId == t.KursteilnahmeId);
        _speicher.TeilnehmerSpeichern(_kursId, mitDatum with { Telefon = "0123" });
        Assert.NotNull(_speicher.TeilnehmerListe(_kursId).Single(x => x.KursteilnahmeId == t.KursteilnahmeId).BezahltAm);
    }

    [Fact]
    public void Anrede_und_Titel()
    {
        var id = _speicher.PersonSpeichern(_kursId, new PersonDaten(null, "Max", "Muster", null, null, null, null, null, false,
            Anrede: Anrede.Herr, Titel: "Dr."));
        var p = _speicher.PersonenListe(_kursId).Single(x => x.Id == id);
        Assert.Equal("Herr Dr. Max Muster", p.VollerName);
        Assert.Equal("Muster, Dr. Max", p.Anzeigename);

        // divers bzw. ohne Angabe: keine Anrede im Namen; bestehende Personen haben „keine Angabe“
        Assert.Equal("Kim Muster", (p with { Anrede = Anrede.Divers, Titel = null, Vorname = "Kim" }).VollerName);
        Assert.All(_speicher.PersonenListe(_kursId).Where(x => x.Id != id), x => Assert.Equal(Anrede.KeineAngabe, x.Anrede));
    }

    [Fact]
    public void Auslagen_erfassen_und_erstatten()
    {
        var person = _speicher.PersonSpeichern(_kursId, new PersonDaten(null, "Karla", "Kasse", null, null, null, null, null, true));
        var essen = _speicher.AuslageSpeichern(_kursId, new AuslageDaten(null, new DateOnly(2027, 9, 18), Auslagenart.Verpflegung,
            "Brötchen und Getränke", 84.355m, person, "Prüfung September", true, null, null));
        _speicher.AuslageSpeichern(_kursId, new AuslageDaten(null, new DateOnly(2027, 3, 1), Auslagenart.Anschaffung,
            "Westen für die Ausbilder", 120m, null, null, true, null, null));

        var liste = _speicher.Auslagen(_kursId);
        Assert.Equal(2, liste.Count);
        Assert.Equal(84.36m, liste.Single(a => a.Id == essen).Betrag);

        _speicher.AuslageErstattetSetzen(_kursId, essen, new DateOnly(2027, 9, 30));
        Assert.Equal(new DateOnly(2027, 9, 30), _speicher.Auslagen(_kursId).Single(a => a.Id == essen).ErstattetAm);

        // Vereinskasse: keine Erstattung; Person mit Auslagen nicht löschbar
        var weste = liste.Single(a => a.PersonId is null).Id!.Value;
        Assert.Throws<InvalidOperationException>(() => _speicher.AuslageErstattetSetzen(_kursId, weste, new DateOnly(2027, 3, 2)));
        Assert.Throws<InvalidOperationException>(() => _speicher.PersonLoeschen(person));
        Assert.Throws<InvalidOperationException>(() => _speicher.AuslageSpeichern(_kursId,
            new AuslageDaten(null, new DateOnly(2027, 3, 1), Auslagenart.Sonstiges, "nichts", 0m, null, null, false, null, null)));

        _speicher.AuslageLoeschen(_kursId, weste);
        Assert.Single(_speicher.Auslagen(_kursId));
    }
}
