using Hundekurs.Planung;

namespace Hundekurs.Planung.Tests;

public class PruefungTests
{
    private static readonly DateTimeOffset Basis = new(2027, 8, 1, 10, 0, 0, TimeSpan.FromHours(2));

    private static Pruefungsnennung Kurs(int id, int tag) => new(id, true, Basis.AddDays(tag));
    private static Pruefungsnennung Fremd(int id, int tag) => new(id, false, Basis.AddDays(tag));

    [Fact]
    public void Terminvorschlag_sind_die_drei_Sonntage_nach_dem_letzten_Training()
    {
        var letzter = new DateOnly(2027, 8, 22); // Sonntag
        var vorschlag = Pruefungsregeln.Terminvorschlag(letzter);

        Assert.Equal(new[] { new DateOnly(2027, 8, 29), new DateOnly(2027, 9, 5), new DateOnly(2027, 9, 12) }, vorschlag);
        Assert.All(vorschlag, d => Assert.Equal(DayOfWeek.Sunday, d.DayOfWeek));
    }

    [Fact]
    public void Terminvorschlag_nach_einem_Mittwoch_beginnt_am_folgenden_Sonntag()
    {
        var vorschlag = Pruefungsregeln.Terminvorschlag(new DateOnly(2027, 8, 18), 1);
        Assert.Equal(new DateOnly(2027, 8, 22), Assert.Single(vorschlag));
    }

    [Fact]
    public void Pruefung_erst_nach_dem_letzten_Trainingstag()
    {
        var letzter = new DateOnly(2027, 8, 22);
        Assert.NotNull(Pruefungsregeln.DatumPruefen(letzter, letzter));
        Assert.NotNull(Pruefungsregeln.DatumPruefen(letzter.AddDays(-7), letzter));
        Assert.Null(Pruefungsregeln.DatumPruefen(letzter.AddDays(7), letzter));
        Assert.Null(Pruefungsregeln.DatumPruefen(letzter, null));
    }

    [Fact]
    public void Hoechstens_sechs_Startplaetze_der_Rest_auf_die_Warteliste()
    {
        var nennungen = Enumerable.Range(1, 8).Select(i => Kurs(i, i)).ToList();
        var plaetze = Pruefungsregeln.PlaetzeVergeben(nennungen, 6, null);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, plaetze.Where(p => p.Status == Platzstatus.Startplatz).Select(p => p.NennungId));
        Assert.Equal(new[] { 7, 8 }, plaetze.Where(p => p.Status == Platzstatus.Warteliste).Select(p => p.NennungId));
        Assert.Equal(7, plaetze.Single(p => p.NennungId == 7).Position);
    }

    [Fact]
    public void Kursteilnehmer_verdraengen_bis_zum_Meldeschluss_kursfremde_Gespanne()
    {
        var meldeschluss = DateOnly.FromDateTime(Basis.AddDays(10).LocalDateTime);
        var nennungen = new List<Pruefungsnennung>
        {
            Fremd(1, 0), Fremd(2, 1),
            Kurs(3, 2), Kurs(4, 3), Kurs(5, 4), Kurs(6, 5), Kurs(7, 6),
            Kurs(8, 10), // am Tag des Meldeschlusses: hat noch Vorrang
        };
        var plaetze = Pruefungsregeln.PlaetzeVergeben(nennungen, 6, meldeschluss);

        Assert.Equal(new[] { 3, 4, 5, 6, 7, 8 }, plaetze.Where(p => p.Status == Platzstatus.Startplatz).Select(p => p.NennungId));
        // Die kursfremden rücken in Reihenfolge ihres Eingangs nach
        Assert.Equal(new[] { 1, 2 }, plaetze.Where(p => p.Status == Platzstatus.Warteliste).Select(p => p.NennungId));
    }

    [Fact]
    public void Nach_dem_Meldeschluss_zaehlt_nur_noch_der_Eingang()
    {
        var meldeschluss = DateOnly.FromDateTime(Basis.AddDays(10).LocalDateTime);
        var nennungen = new List<Pruefungsnennung>
        {
            Kurs(1, 0), Kurs(2, 1), Kurs(3, 2), Kurs(4, 3), Fremd(5, 4), Fremd(6, 5),
            Kurs(7, 11), // nach Meldeschluss: verdrängt niemanden mehr
        };
        var plaetze = Pruefungsregeln.PlaetzeVergeben(nennungen, 6, meldeschluss);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, plaetze.Where(p => p.Status == Platzstatus.Startplatz).Select(p => p.NennungId));
        Assert.Equal(7, Assert.Single(plaetze, p => p.Status == Platzstatus.Warteliste).NennungId);
    }

    [Fact]
    public void Wird_ein_Platz_frei_rueckt_die_Warteliste_nach()
    {
        var nennungen = Enumerable.Range(1, 7).Select(i => i <= 6 ? Kurs(i, i) : Fremd(i, i)).ToList();
        Assert.Equal(Platzstatus.Warteliste, Pruefungsregeln.PlaetzeVergeben(nennungen, 6, null).Single(p => p.NennungId == 7).Status);

        nennungen.RemoveAll(n => n.Id == 2);
        Assert.Equal(Platzstatus.Startplatz, Pruefungsregeln.PlaetzeVergeben(nennungen, 6, null).Single(p => p.NennungId == 7).Status);
    }

    [Fact]
    public void Vollstaendige_Besetzung_ohne_Befund()
    {
        var sonntag = new DateOnly(2027, 8, 29);
        var befunde = Pruefungsregeln.Pruefen(1, sonntag, sonntag.AddDays(-7), new Richterbesetzung(10, 11, 12, [11]));
        Assert.Empty(befunde);
    }

    [Fact]
    public void Fehlende_Pruefer_und_Vortag_werden_gemeldet()
    {
        var sonntag = new DateOnly(2027, 8, 29);
        var befunde = Pruefungsregeln.Pruefen(1, sonntag, sonntag.AddDays(-7), new Richterbesetzung(null, 11, null, []));

        Assert.All(befunde, b => Assert.Equal(Schwere.Fehler, b.Schwere));
        Assert.Contains(befunde, b => b.Text.Contains("Richterobmann"));
        Assert.Contains(befunde, b => b.Text.Contains("2 von 3"));
        Assert.Contains(befunde, b => b.Text.Contains("Vortag"));
    }

    [Fact]
    public void Doppelt_eingetragener_Pruefer_ist_ein_Fehler()
    {
        var sonntag = new DateOnly(2027, 8, 29);
        var befunde = Pruefungsregeln.Pruefen(1, sonntag, null, new Richterbesetzung(10, 10, 12, [10]));
        Assert.Contains(befunde, b => b.Schwere == Schwere.Fehler && b.Text.Contains("mehrfach"));
    }

    [Fact]
    public void Samstag_ist_erlaubt_aber_ein_Hinweis_und_Pruefer_am_Vortag_sollte_mitrichten()
    {
        var samstag = new DateOnly(2027, 8, 28);
        var befunde = Pruefungsregeln.Pruefen(1, samstag, null, new Richterbesetzung(10, 11, 12, [99]));

        Assert.Equal(2, befunde.Count);
        Assert.All(befunde, b => Assert.Equal(Schwere.Warnung, b.Schwere));
    }

    [Fact]
    public void Verpflegung_zaehlt_alle_zusammen()
    {
        var v = new Verpflegung(Hundefuehrer: 6, Gaeste: 4, Pruefer: 3, Ausbilder: 2, WeitereHelfer: 1);
        Assert.Equal(16, v.Gesamt);
    }
}
