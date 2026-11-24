using Hundekurs.Daten;
using Hundekurs.Domain;
using Hundekurs.Planung;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Hundekurs.Daten.Tests;

/// <summary>Prüfungstage und Nennungen in einer SQLite-Datenbank im Arbeitsspeicher.</summary>
public sealed class PruefungTests : IDisposable
{
    private static readonly DateOnly Sonntag = new(2027, 8, 29);

    private readonly SqliteConnection _verbindung;
    private readonly TestFabrik _fabrik;
    private readonly Kursspeicher _speicher;
    private readonly int _kursId;

    public PruefungTests()
    {
        _verbindung = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        _verbindung.Open();
        _fabrik = new TestFabrik(_verbindung);
        using (var db = _fabrik.CreateDbContext()) Datenbank.Vorbereiten(db);
        _speicher = new Kursspeicher(_fabrik);
        _kursId = _speicher.KursSicherstellen(demodaten: true);
    }

    public void Dispose() => _verbindung.Dispose();

    private sealed class TestFabrik(SqliteConnection verbindung) : IDbContextFactory<HundekursDb>
    {
        public HundekursDb CreateDbContext() =>
            new(new DbContextOptionsBuilder<HundekursDb>().UseSqlite(verbindung).Options) { Benutzer = "test" };
    }

    private int TagAnlegen(DateOnly datum) =>
        _speicher.PruefungstageAnlegen(_kursId, [datum], null, Pruefungsregeln.StandardBeginn, Pruefungsregeln.StandardMaxHunde).Single();

    private int PrueferAnlegen(string name) =>
        _speicher.PersonSpeichern(_kursId, new PersonDaten(null, name, "Prüfer", null, null, null, null, null, false, Pruefer: true, Richternummer: "R-" + name));

    private (int HundId, int FuehrerId) KursfremdesGespann(string hund)
    {
        var fuehrer = _speicher.PersonSpeichern(_kursId, new PersonDaten(null, "Gast", hund, null, null, null, null, null, false));
        var hundId = _speicher.HundSpeichern(new HundDaten(null, hund, null, "Deutsch Drahthaar", null, Geschlecht.Huendin,
            "276000000000001", null, null, fuehrer, null, null, []));
        return (hundId, fuehrer);
    }

    private static NennungDaten Nennung(int tagId, int hundId, int fuehrerId, int gaeste = 0) =>
        new(null, tagId, hundId, fuehrerId, DateTimeOffset.Now, true, true, false, gaeste, 30m, null, null, null);

    [Fact]
    public void Pruefungstag_bekommt_Termin_Vortag_und_Standardwerte()
    {
        var id = TagAnlegen(Sonntag);
        var tag = Assert.Single(_speicher.Pruefungstage(_kursId));

        Assert.Equal(id, tag.Id);
        Assert.Equal(Sonntag, tag.Datum);
        Assert.Equal(new TimeOnly(8, 0), tag.Beginn);
        Assert.Equal(Sonntag.AddDays(-1), tag.Vortag);
        Assert.Equal(6, tag.MaxHunde);

        // Ein zweites Anlegen desselben Datums legt nichts doppelt an
        Assert.Empty(_speicher.PruefungstageAnlegen(_kursId, [Sonntag], null, Pruefungsregeln.StandardBeginn, 6));
    }

    [Fact]
    public void Besetzung_mit_drei_Pruefern_und_Vortag_wird_gespeichert()
    {
        TagAnlegen(Sonntag);
        var (a, b, c) = (PrueferAnlegen("Anna"), PrueferAnlegen("Bernd"), PrueferAnlegen("Carla"));
        var tag = _speicher.Pruefungstage(_kursId).Single();

        _speicher.PruefungstagSpeichern(_kursId, tag with { RichterobmannId = a, Richter2Id = b, Richter3Id = c, PrueferAmVortag = [b], Meldeschluss = Sonntag.AddDays(-14) });

        var neu = _speicher.Pruefungstage(_kursId).Single();
        Assert.Equal(new[] { a, b, c }, neu.Besetzung.Richter);
        Assert.Equal(new[] { b }, neu.PrueferAmVortag);
        Assert.Empty(Pruefungsregeln.Pruefen(neu.TerminId, neu.Datum, null, neu.Besetzung));
        Assert.Contains(_speicher.PrueferListe(_kursId), p => p.PersonId == a && p.Richternummer == "R-Anna");
    }

    [Fact]
    public void Nur_Personen_mit_Prueferrolle_und_keiner_doppelt()
    {
        TagAnlegen(Sonntag);
        var a = PrueferAnlegen("Anna");
        var keinPruefer = _speicher.PersonSpeichern(_kursId, new PersonDaten(null, "Kein", "Prüfer", null, null, null, null, null, false));
        var tag = _speicher.Pruefungstage(_kursId).Single();

        Assert.Throws<InvalidOperationException>(() => _speicher.PruefungstagSpeichern(_kursId, tag with { RichterobmannId = keinPruefer }));
        Assert.Throws<InvalidOperationException>(() => _speicher.PruefungstagSpeichern(_kursId, tag with { RichterobmannId = a, Richter2Id = a }));
    }

    [Fact]
    public void Kursteilnehmer_und_kursfremde_Gespanne_lassen_sich_nennen()
    {
        var tagId = TagAnlegen(Sonntag);
        var teilnehmer = _speicher.TeilnehmerListe(_kursId).First(t => t.Schalenwild);
        var (fremdHund, fremdFuehrer) = KursfremdesGespann("Kira");

        _speicher.NennungSpeichern(_kursId, Nennung(tagId, teilnehmer.HundId!.Value, teilnehmer.FuehrerId!.Value, gaeste: 2));
        _speicher.NennungSpeichern(_kursId, Nennung(tagId, fremdHund, fremdFuehrer));

        var nennungen = _speicher.Nennungen(_kursId);
        Assert.Equal(2, nennungen.Count);
        Assert.Equal(teilnehmer.KursteilnahmeId, nennungen.Single(n => n.HundId == teilnehmer.HundId).KursteilnahmeId);
        var fremd = nennungen.Single(n => n.HundId == fremdHund);
        Assert.False(fremd.AusDemKurs);
        Assert.Equal("Deutsch Drahthaar", fremd.Rasse);
        Assert.Equal("276000000000001", fremd.ChipNummer);
    }

    [Fact]
    public void Ein_Hund_wird_nur_einmal_genannt_und_kann_den_Tag_wechseln()
    {
        var tag1 = TagAnlegen(Sonntag);
        var tag2 = TagAnlegen(Sonntag.AddDays(7));
        var (hund, fuehrer) = KursfremdesGespann("Kira");

        var id = _speicher.NennungSpeichern(_kursId, Nennung(tag1, hund, fuehrer));
        Assert.Throws<InvalidOperationException>(() => _speicher.NennungSpeichern(_kursId, Nennung(tag2, hund, fuehrer)));

        _speicher.LoseSetzen(_kursId, tag1, [id]);
        _speicher.NennungSpeichern(_kursId, Nennung(tag2, hund, fuehrer) with { Id = id });
        var n = Assert.Single(_speicher.Nennungen(_kursId));
        Assert.Equal(tag2, n.PruefungstagId);
        Assert.Null(n.Losnummer); // das Los galt für den alten Tag
    }

    [Fact]
    public void Pruefungstag_mit_Nennungen_laesst_sich_nicht_loeschen_Hund_und_Fuehrer_auch_nicht()
    {
        var tagId = TagAnlegen(Sonntag);
        var (hund, fuehrer) = KursfremdesGespann("Kira");
        var id = _speicher.NennungSpeichern(_kursId, Nennung(tagId, hund, fuehrer));

        Assert.Throws<InvalidOperationException>(() => _speicher.PruefungstagLoeschen(_kursId, tagId));
        Assert.Throws<InvalidOperationException>(() => _speicher.HundLoeschen(hund));
        Assert.Throws<InvalidOperationException>(() => _speicher.PersonLoeschen(fuehrer));

        _speicher.NennungLoeschen(_kursId, id);
        _speicher.PruefungstagLoeschen(_kursId, tagId);
        Assert.Empty(_speicher.Pruefungstage(_kursId));
    }

    [Fact]
    public void Lose_und_Nenngeld()
    {
        var tagId = TagAnlegen(Sonntag);
        var (h1, f1) = KursfremdesGespann("Kira");
        var (h2, f2) = KursfremdesGespann("Bella");
        var n1 = _speicher.NennungSpeichern(_kursId, Nennung(tagId, h1, f1));
        var n2 = _speicher.NennungSpeichern(_kursId, Nennung(tagId, h2, f2));

        _speicher.LoseSetzen(_kursId, tagId, [n2, n1]);
        _speicher.NenngeldBezahltSetzen(_kursId, n1, Sonntag.AddDays(-20));

        var nennungen = _speicher.Nennungen(_kursId).ToDictionary(n => n.Id!.Value);
        Assert.Equal(2, nennungen[n1].Losnummer);
        Assert.Equal(1, nennungen[n2].Losnummer);
        Assert.Equal(Sonntag.AddDays(-20), nennungen[n1].NenngeldBezahltAm);
        Assert.Null(nennungen[n2].NenngeldBezahltAm);
        Assert.Equal(30m, nennungen[n2].Nenngeld);
    }

    [Fact]
    public void Ausbildereinsaetze_am_Vortag_und_Pruefungstag()
    {
        TagAnlegen(Sonntag);
        var tag = _speicher.Pruefungstage(_kursId).Single();
        var ausbilder = _speicher.AusbilderListe(_kursId).Select(a => a.PersonId!.Value).ToList();

        _speicher.EinsaetzeSetzen(tag.TerminId, Einsatzart.Vorbereitung, tag.Vortag, null, null, ausbilder.Take(1).ToHashSet());
        _speicher.EinsaetzeSetzen(tag.TerminId, Einsatzart.Pruefung, tag.Datum, null, null, ausbilder.ToHashSet());

        var neu = _speicher.Pruefungstage(_kursId).Single();
        Assert.Single(neu.AusbilderVortag);
        Assert.Equal(ausbilder.Count, neu.AusbilderPruefung.Count);
        Assert.Contains(_speicher.Einsaetze(tag.Vortag, tag.Vortag), e => e.Art == Einsatzart.Vorbereitung);
    }
}
