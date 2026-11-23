using System.Net;
using System.Text;
using Hundekurs.Planung;
using Hundekurs.Planung.Strecken;

namespace Hundekurs.Planung.Tests;

public class KurskalenderTests
{
    [Fact]
    public void Ostersonntag_stimmt_fuer_bekannte_Jahre()
    {
        Assert.Equal(new DateOnly(2008, 3, 23), Kurskalender.Ostersonntag(2008));
        Assert.Equal(new DateOnly(2019, 4, 21), Kurskalender.Ostersonntag(2019));
        Assert.Equal(new DateOnly(2024, 3, 31), Kurskalender.Ostersonntag(2024));
        Assert.Equal(new DateOnly(2025, 4, 20), Kurskalender.Ostersonntag(2025));
        Assert.Equal(new DateOnly(2026, 4, 5), Kurskalender.Ostersonntag(2026));
        Assert.Equal(new DateOnly(2027, 3, 28), Kurskalender.Ostersonntag(2027));
        Assert.Equal(new DateOnly(2038, 4, 25), Kurskalender.Ostersonntag(2038));
    }

    [Fact]
    public void Erster_Sonntag_im_Maerz()
    {
        Assert.Equal(new DateOnly(2026, 3, 1), Kurskalender.ErsterSonntagImMaerz(2026));
        Assert.Equal(new DateOnly(2027, 3, 7), Kurskalender.ErsterSonntagImMaerz(2027));
    }

    [Fact]
    public void Teil1_hat_immer_11_Sonntage_ohne_Ostern_und_Teil2_schliesst_direkt_an()
    {
        for (var jahr = 2025; jahr <= 2060; jahr++)
        {
            var k = Kurskalender.Erzeugen(jahr);
            Assert.Equal(11, k.Gehorsam.Count);
            Assert.DoesNotContain(k.Ostersonntag, k.Gehorsam);
            Assert.All(k.Gehorsam, d => Assert.Equal(DayOfWeek.Sunday, d.DayOfWeek));
            Assert.Equal(Kurskalender.ErsterSonntagImMaerz(jahr), k.Gehorsam[0]);

            Assert.Equal(12, k.Training.Count);
            Assert.Equal(Kurskalender.ErsterSonntagImMaerz(jahr).AddDays(84), k.Training[0]);
            Assert.All(k.Training, d => Assert.Equal(DayOfWeek.Sunday, d.DayOfWeek));

            Assert.Equal(12, k.Apportierarbeit.Count);
            Assert.All(k.Apportierarbeit, d => Assert.Equal(DayOfWeek.Wednesday, d.DayOfWeek));
            Assert.Equal(k.Training[0].AddDays(-4), k.Apportierarbeit[0]);
            Assert.True(k.Apportierarbeit[0] > k.Gehorsam[^1]);
        }
    }

    [Fact]
    public void Teil2_kann_spaeter_beginnen_Apportieren_folgt_mit()
    {
        // 2027: Teil 1 endet am 23.05., üblicher Beginn wäre der 30.05.
        Assert.Equal(new DateOnly(2027, 5, 30), Kurskalender.StandardBeginnTeil2(2027));
        var k = Kurskalender.Erzeugen(2027, beginnTeil2: new DateOnly(2027, 6, 13));

        Assert.Equal(11, k.Gehorsam.Count);
        Assert.Equal(new DateOnly(2027, 6, 13), k.Training[0]);
        Assert.Equal(12, k.Training.Count);
        Assert.Equal(new DateOnly(2027, 6, 9), k.Apportierarbeit[0]);
        Assert.All(k.Apportierarbeit, d => Assert.Equal(DayOfWeek.Wednesday, d.DayOfWeek));
    }

    [Fact]
    public void Beginn_Teil2_muss_ein_Sonntag_nach_Teil1_sein()
    {
        var letzterGehorsam = new DateOnly(2027, 5, 23);
        Assert.Null(Kurskalender.BeginnTeil2Pruefen(new DateOnly(2027, 5, 30), letzterGehorsam));
        Assert.Contains("Sonntag", Kurskalender.BeginnTeil2Pruefen(new DateOnly(2027, 6, 1), letzterGehorsam));
        Assert.Contains("nach dem letzten", Kurskalender.BeginnTeil2Pruefen(new DateOnly(2027, 5, 23), letzterGehorsam));
        Assert.Throws<ArgumentException>(() => Kurskalender.Erzeugen(2027, beginnTeil2: new DateOnly(2027, 5, 16)));
    }
}

public class ZulassungTests
{
    [Fact]
    public void Erforderlich_sind_60_Prozent_aufgerundet()
    {
        Assert.Equal(7, Zulassungsregel.Erforderlich(11));
        Assert.Equal(6, Zulassungsregel.Erforderlich(10));
        Assert.Equal(6, Zulassungsregel.Erforderlich(9));
        Assert.Equal(0, Zulassungsregel.Erforderlich(0));
    }

    [Fact]
    public void Ampel_zeigt_erreicht_erreichbar_und_verfehlt()
    {
        Assert.Equal(Ampel.Gruen, new Zulassungsstand(1, 7, 8, 3, false).Ampel);
        Assert.Equal(Ampel.Gelb, new Zulassungsstand(1, 4, 8, 3, false).Ampel);
        Assert.Equal(Ampel.Rot, new Zulassungsstand(1, 3, 8, 3, false).Ampel);
        // Fällt ein Sonntag aus (10 statt 11), reichen 6
        Assert.Equal(Ampel.Gruen, new Zulassungsstand(1, 6, 10, 0, false).Ampel);
        Assert.Equal(Ampel.Rot, new Zulassungsstand(1, 6, 11, 0, false).Ampel);
    }

    [Fact]
    public void Zulassung_nur_bei_Gruen_oder_Ausnahme()
    {
        Assert.True(new Zulassungsstand(1, 7, 11, 0, false).Zugelassen);
        Assert.False(new Zulassungsstand(1, 6, 11, 0, false).Zugelassen);
        Assert.True(new Zulassungsstand(1, 6, 11, 0, true).Zugelassen);
        Assert.False(new Zulassungsstand(1, 0, 0, 0, false).Zugelassen);
    }

    [Fact]
    public void Anwaerterliste_sortiert_nach_Anwesenheit_mit_Gleichstand_an_der_Grenze()
    {
        var staende = new[]
        {
            new Zulassungsstand(1, 9, 11, 0, false),
            new Zulassungsstand(2, 11, 11, 0, false),
            new Zulassungsstand(3, 8, 11, 0, false),
            new Zulassungsstand(4, 8, 11, 0, false),
            new Zulassungsstand(5, 5, 11, 0, false), // nicht zugelassen
            new Zulassungsstand(6, 7, 11, 0, false),
        };

        var liste = Zulassungsregel.Anwaerterliste(staende, plaetze: 3);

        Assert.Equal(new[] { 2, 1, 3, 4, 6 }, liste.Select(a => a.Stand.GespannId));
        Assert.Equal(new[] { 1, 2, 3, 3, 5 }, liste.Select(a => a.Rang));
        Assert.Equal(new[] { true, true, true, true, false }, liste.Select(a => a.ImKontingent));
    }
}

public class KilometergeldTests
{
    [Fact]
    public void Hin_und_Rueckweg_mit_Vorrang_der_Korrektur()
    {
        Assert.Equal(25.0, new Fahrt(new DateOnly(2027, 6, 6), 12.5, null).KmGesamt);
        Assert.Equal(40.0, new Fahrt(new DateOnly(2027, 6, 6), 12.5, 20).KmGesamt);
        Assert.Equal(0.0, new Fahrt(new DateOnly(2027, 6, 6), null, null).KmGesamt);
    }

    [Fact]
    public void Summe_und_Betrag_mit_30_Cent()
    {
        var fahrten = new[]
        {
            new Fahrt(new DateOnly(2027, 6, 5), 12.4, null),
            new Fahrt(new DateOnly(2027, 6, 6), 12.4, null),
            new Fahrt(new DateOnly(2027, 6, 9), 8.0, 9.5),
        };
        var (km, betrag) = Kilometergeld.Summe(fahrten, Kilometergeld.StandardSatz);
        Assert.Equal(68.6, km);
        Assert.Equal(20.58m, betrag);
    }

    [Fact]
    public void Doppelrolle_am_selben_Tag_ist_eine_Fahrt()
    {
        var tag = new DateOnly(2027, 9, 5);
        var fahrten = new[]
        {
            new Fahrt(tag, 10.0, null, PersonId: 1, Id: 1),       // Ausbilder
            new Fahrt(tag, 10.0, null, PersonId: 1, Id: 2),       // Prüfer, gleicher Tag
            new Fahrt(tag, 15.0, null, PersonId: 2, Id: 3),       // andere Person
            new Fahrt(tag.AddDays(-1), 10.0, null, PersonId: 1, Id: 4), // Vortag: eigene Fahrt
        };
        var erstattet = Kilometergeld.Erstattet(fahrten);
        Assert.True(new[] { 1, 3, 4 }.SequenceEqual(erstattet.Select(f => f.Id).OrderBy(x => x)));
        Assert.Equal(70.0, Kilometergeld.Summe(fahrten, Kilometergeld.StandardSatz).Km);
    }
}

public class AusbildungTests
{
    [Fact]
    public void Zivil_ohne_Pflicht_jagdlich_mit_Pflicht()
    {
        var zivil = new Ausbildung(Gehorsam: true, Schalenwild: false, Niederwild: false);
        Assert.False(zivil.Jagdlich);
        Assert.False(zivil.ZulassungErforderlich);

        var schweiss = new Ausbildung(Gehorsam: false, Schalenwild: true, Niederwild: false);
        Assert.True(schweiss.Jagdlich);
        Assert.True(schweiss.Teil1Pflicht);
        Assert.True(schweiss.Bereinigt().Gehorsam);
    }

    [Fact]
    public void Brauchbarkeit_befreit_nur_bei_reinem_Niederwild()
    {
        Assert.False(new Ausbildung(false, false, true, BrauchbarkeitBestanden: true).Teil1Pflicht);
        Assert.True(new Ausbildung(false, false, true, BrauchbarkeitBestanden: false).Teil1Pflicht);
        Assert.True(new Ausbildung(false, true, true, BrauchbarkeitBestanden: true).Teil1Pflicht); // wer Schweiß macht, macht alles
        Assert.False(new Ausbildung(false, false, true, BrauchbarkeitBestanden: true).Bereinigt().Gehorsam);
    }
}

public class GebuehrenTests
{
    private static readonly Gebuehrenpaket[] Pakete =
    [
        new(1, "Teil 1", true, false, false, 120m, 100m),
        new(2, "Teil 1 + Teil 2", true, true, false, 200m, 180m),
        new(3, "Teil 1 + Teil 2 + Apport", true, true, true, 250m, 225m),
    ];

    [Fact]
    public void Paket_nach_Kombination_und_Mitgliederpreis()
    {
        Assert.Equal(200m, Gebuehrenregel.Berechnen(Pakete, new Ausbildung(true, true, false), mitglied: false, wiederholer: false).Betrag);
        Assert.Equal(180m, Gebuehrenregel.Berechnen(Pakete, new Ausbildung(true, true, false), mitglied: true, wiederholer: false).Betrag);
        Assert.Equal(100m, Gebuehrenregel.Berechnen(Pakete, new Ausbildung(true, false, false), mitglied: true, wiederholer: false).Betrag);
    }

    [Fact]
    public void Wiederholer_zahlt_Haelfte_des_fuer_ihn_gueltigen_Preises()
    {
        Assert.Equal(100m, Gebuehrenregel.Berechnen(Pakete, new Ausbildung(true, true, false), mitglied: false, wiederholer: true).Betrag);
        // erst Mitgliederpreis, dann halbieren
        Assert.Equal(90m, Gebuehrenregel.Berechnen(Pakete, new Ausbildung(true, true, false), mitglied: true, wiederholer: true).Betrag);
        Assert.Equal(112.5m, Gebuehrenregel.Berechnen(Pakete, new Ausbildung(true, true, true), mitglied: true, wiederholer: true).Betrag);
    }

    [Fact]
    public void Fehlender_Preis_und_Betrag_von_Hand()
    {
        var ohne = Gebuehrenregel.Berechnen(Pakete, new Ausbildung(false, false, true, true), mitglied: false, wiederholer: false);
        Assert.False(ohne.Ermittelt);
        Assert.Contains("kein Preis", ohne.Rechenweg);

        var hand = Gebuehrenregel.Berechnen(Pakete, new Ausbildung(true, true, false), mitglied: true, wiederholer: true, vonHand: 50m, hinweis: "Absprache");
        Assert.Equal(50m, hand.Betrag);
        Assert.Contains("Absprache", hand.Rechenweg);
    }
}

public class RoutendienstTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> antwort) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Anfragen { get; } = [];
        public List<string> Inhalte { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Anfragen.Add(request);
            Inhalte.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return antwort(request);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Adresssuche_liefert_Breite_und_Laenge_und_sendet_den_Schluessel_im_Header()
    {
        var handler = new FakeHandler(_ => Json("""{"features":[{"geometry":{"type":"Point","coordinates":[8.2345,50.4321]}}]}"""));
        var dienst = new OpenRouteService(new HttpClient(handler), "geheim");

        var k = await dienst.AdresseSuchen("Hauptstraße 1, 56370 Musterdorf");

        Assert.Equal(new Koordinate(50.4321, 8.2345), k);
        var anfrage = handler.Anfragen.Single();
        Assert.Equal("https://api.heigit.org/pelias/v1/search", anfrage.RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("boundary.country=DE", anfrage.RequestUri.Query);
        Assert.Equal("geheim", anfrage.Headers.Authorization?.ToString() ?? string.Join("", anfrage.Headers.GetValues("Authorization")));
        Assert.DoesNotContain("geheim", anfrage.RequestUri.ToString());
    }

    [Fact]
    public async Task Adresssuche_ohne_Treffer_liefert_null()
    {
        var dienst = new OpenRouteService(new HttpClient(new FakeHandler(_ => Json("""{"features":[]}"""))), "k");
        Assert.Null(await dienst.AdresseSuchen("gibt es nicht"));
    }

    [Fact]
    public async Task Strecke_wird_in_km_umgerechnet_und_Koordinaten_als_Laenge_Breite_gesendet()
    {
        var handler = new FakeHandler(_ => Json("""{"routes":[{"summary":{"distance":23456.7,"duration":1800}}]}"""));
        var dienst = new OpenRouteService(new HttpClient(handler), "k");

        var km = await dienst.StreckeKm(new Koordinate(50.1, 8.1), new Koordinate(50.2, 8.3));

        Assert.Equal(23.5, km);
        Assert.Equal("https://api.heigit.org/openrouteservice/v2/directions/driving-car", handler.Anfragen.Single().RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.Anfragen.Single().Method);
        Assert.Contains("[[8.1,50.1],[8.3,50.2]]", handler.Inhalte.Single());
    }

    [Fact]
    public async Task Abgelehnter_Schluessel_ergibt_verstaendliche_Meldung()
    {
        var dienst = new OpenRouteService(new HttpClient(new FakeHandler(_ => Json("{}", HttpStatusCode.Forbidden))), "falsch");
        var ex = await Assert.ThrowsAsync<RoutendienstException>(() => dienst.StreckeKm(new Koordinate(1, 1), new Koordinate(2, 2)));
        Assert.Contains("Schlüssel", ex.Message);
    }

    [Fact]
    public void Koordinaten_lassen_sich_aus_Text_lesen()
    {
        Assert.True(Koordinate.TryParse("50.4321, 8.2345", out var a));
        Assert.Equal(new Koordinate(50.4321, 8.2345), a);
        Assert.True(Koordinate.TryParse("50,4321; 8,2345", out var b));
        Assert.Equal(new Koordinate(50.4321, 8.2345), b);
        Assert.False(Koordinate.TryParse("Musterdorf", out _));
        Assert.False(Koordinate.TryParse("95, 8", out _));
    }
}
