using Hundekurs.Planung;

namespace Hundekurs.Planung.Tests;

public class SchweissplanerTests
{
    private static readonly DateOnly Start = Demodaten.ErsterSonntagImJuni(2027);

    private static Schweissplaner Planer(int helfer = 2, List<Revier>? reviere = null, int gespanne = 14) =>
        new(reviere ?? Demodaten.Reviere(), Demodaten.Gespanne(gespanne), new PlanungsOptionen { HelferProTermin = helfer });

    private static List<Befund> Fehler(Schweissplaner p, Saisonplan plan) =>
        new PlanPruefer(p).Pruefen(plan).Where(b => b.Schwere == Schwere.Fehler).ToList();

    [Fact]
    public void Saison_mit_vier_Revieren_hat_keine_Fehler()
    {
        var p = Planer();
        var plan = p.SaisonErzeugen(Start, 16);

        Assert.Equal(16, plan.Termine.Count);
        Assert.Empty(Fehler(p, plan));
    }

    [Fact]
    public void Gruppen_wechseln_woechentlich()
    {
        var plan = Planer().SaisonErzeugen(Start, 6);
        var gruppen = plan.Chronologisch.Select(t => t.Gruppe).ToList();
        Assert.Equal(new[] { Gruppe.A, Gruppe.B, Gruppe.A, Gruppe.B, Gruppe.A, Gruppe.B }, gruppen);
    }

    [Fact]
    public void Revier_wechselt_jede_Woche_und_ruht_mindestens_21_Tage()
    {
        var plan = Planer().SaisonErzeugen(Start, 16);
        foreach (var gruppe in plan.Chronologisch.Where(t => t.RevierId is not null).GroupBy(t => t.RevierId))
        {
            var daten = gruppe.Select(t => t.Datum.DayNumber).Order().ToList();
            for (var i = 1; i < daten.Count; i++)
                Assert.True(daten[i] - daten[i - 1] >= 21, $"Revier {gruppe.Key}: nur {daten[i] - daten[i - 1]} Tage Ruhe");
        }
    }

    [Fact]
    public void Pruefungsrevier_wird_im_Training_nicht_genutzt()
    {
        var plan = Planer().SaisonErzeugen(Start, 16);
        Assert.DoesNotContain(plan.Termine, t => t.RevierId == 5);
    }

    [Fact]
    public void Jede_Gruppe_kommt_in_alle_vier_Trainingsreviere()
    {
        var plan = Planer().SaisonErzeugen(Start, 16);
        foreach (var gruppe in new[] { Gruppe.A, Gruppe.B })
        {
            var reviere = plan.Termine.Where(t => t.Gruppe == gruppe).Select(t => t.RevierId).Distinct().ToList();
            Assert.Equal(4, reviere.Count);
        }
    }

    [Fact]
    public void Reviere_sind_je_Gruppe_ausgeglichen_verteilt()
    {
        var plan = Planer().SaisonErzeugen(Start, 16);
        foreach (var gruppe in new[] { Gruppe.A, Gruppe.B })
        {
            var besuche = plan.Termine.Where(t => t.Gruppe == gruppe).GroupBy(t => t.RevierId).Select(x => x.Count()).ToList();
            Assert.True(besuche.Max() - besuche.Min() <= 1, $"Gruppe {gruppe}: {string.Join(", ", besuche)}");
        }
    }

    [Fact]
    public void Kein_Hund_laeuft_eine_Faehrte_zweimal()
    {
        var plan = Planer(helfer: 0).SaisonErzeugen(Start, 16);
        var laeufe = plan.Termine.SelectMany(t => t.Zuteilungen.Select(z => (z.GespannId, t.RevierId, z.FaehrteNr))).ToList();
        Assert.Equal(laeufe.Count, laeufe.Distinct().Count());
    }

    [Fact]
    public void Bei_sieben_Laeufern_wird_genau_eine_Faehrte_doppelt_gelaufen()
    {
        var p = Planer(helfer: 0);
        var plan = p.SaisonErzeugen(Start, 16);
        foreach (var t in plan.Termine)
        {
            Assert.Equal(7, t.Zuteilungen.Count);
            Assert.Single(t.Zuteilungen.GroupBy(z => z.FaehrteNr), x => x.Count() == 2);
            Assert.Single(t.Zuteilungen, z => z.ZweiterLaufAufFaehrte);
        }
        Assert.Empty(Fehler(p, plan));
    }

    [Fact]
    public void Doppelte_Faehrte_wechselt_reihum()
    {
        var plan = Planer(helfer: 0).SaisonErzeugen(Start, 16);
        var doppelt = plan.Termine
            .Select(t => (t.RevierId, Nr: t.Zuteilungen.Single(z => z.ZweiterLaufAufFaehrte).FaehrteNr))
            .GroupBy(x => x)
            .Max(x => x.Count());
        Assert.True(doppelt <= 2, $"Eine Fährte wurde {doppelt}-mal doppelt belastet.");
    }

    [Fact]
    public void Standardpaare_laufen_parallel()
    {
        var plan = Planer(helfer: 0).SaisonErzeugen(Start, 1);
        var z = plan.Termine[0].Zuteilungen.Where(x => !x.ZweiterLaufAufFaehrte).ToDictionary(x => x.FaehrteNr, x => x.Sequenz);
        Assert.Equal(z[1], z[4]);
        Assert.Equal(z[2], z[5]);
        Assert.Equal(z[3], z[6]);
        Assert.Equal(3, z.Values.Distinct().Count());
        var zweiter = plan.Termine[0].Zuteilungen.Single(x => x.ZweiterLaufAufFaehrte);
        Assert.Equal(4, zweiter.Sequenz);
    }

    [Fact]
    public void Eigene_Paare_je_Revier_werden_beachtet()
    {
        var paare = new List<Sequenzpaar> { new(1, 2), new(3, 4), new(5, 6) };
        var reviere = new List<Revier> { new() { Id = 1, Name = "Neu", Kuerzel = "X", Paare = paare } };
        var plan = Planer(helfer: 0, reviere: reviere).SaisonErzeugen(Start, 1);
        var z = plan.Termine[0].Zuteilungen.Where(x => !x.ZweiterLaufAufFaehrte).ToDictionary(x => x.FaehrteNr, x => x.Sequenz);
        Assert.Equal(z[1], z[2]);
        Assert.Equal(z[3], z[4]);
        Assert.Equal(z[5], z[6]);
    }

    [Fact]
    public void Zwei_Helfer_kommen_aus_der_anderen_Gruppe_und_sind_gleichmaessig_verteilt()
    {
        var p = Planer();
        var plan = p.SaisonErzeugen(Start, 16);
        foreach (var t in plan.Termine)
        {
            Assert.Equal(2, t.Helfer.Count);
            Assert.All(t.Helfer, h => Assert.NotEqual(t.Gruppe, p.Gespann(h).Gruppe));
            Assert.Equal(7, t.Zuteilungen.Count);
        }
        var dienste = p.Gespanne.Select(g => plan.Termine.Count(t => t.Helfer.Contains(g.Id))).ToList();
        Assert.True(dienste.Max() - dienste.Min() <= 1, string.Join(", ", dienste));
    }

    [Fact]
    public void Helfer_aus_eigener_Gruppe_laufen_nicht_mit()
    {
        var p = new Schweissplaner(Demodaten.Reviere(), Demodaten.Gespanne(),
            new PlanungsOptionen { HelferProTermin = 1, HelferAusAndererGruppe = false });
        var plan = p.SaisonErzeugen(Start, 8);
        foreach (var t in plan.Termine)
        {
            Assert.Equal(t.Gruppe, p.Gespann(t.Helfer.Single()).Gruppe);
            Assert.DoesNotContain(t.Zuteilungen, z => t.Helfer.Contains(z.GespannId));
        }
    }

    [Fact]
    public void Gesperrte_Faehrte_wird_nicht_eingeplant()
    {
        var reviere = Demodaten.Reviere();
        reviere[0] = reviere[0] with { GesperrteFaehrten = new HashSet<int> { 6 } };
        var p = Planer(reviere: reviere, gespanne: 12); // 6 je Gruppe, passt auch mit 5 Fährten + 1 doppelt
        var plan = p.SaisonErzeugen(Start, 16);
        Assert.DoesNotContain(plan.Termine.Where(t => t.RevierId == 1).SelectMany(t => t.Zuteilungen), z => z.FaehrteNr == 6);
        Assert.Empty(Fehler(p, plan));
    }

    [Fact]
    public void Gesperrte_Faehrte_bei_voller_Gruppe_wird_gemeldet()
    {
        var reviere = Demodaten.Reviere();
        reviere[0] = reviere[0] with { GesperrteFaehrten = new HashSet<int> { 6 } };
        var p = Planer(reviere: reviere);
        var befunde = new PlanPruefer(p).Pruefen(p.SaisonErzeugen(Start, 4));
        Assert.Contains(befunde, b => b.Schwere == Schwere.Warnung && b.Text.Contains("fasst 6"));
    }

    [Fact]
    public void Deaktiviertes_Revier_wird_beim_Neuplanen_ersetzt()
    {
        var p = Planer();
        var plan = p.SaisonErzeugen(Start, 16);

        var reviere = Demodaten.Reviere();
        reviere[3] = reviere[3] with { Aktiv = false };
        var p2 = Planer(reviere: reviere);
        var neu = p2.NeuPlanen(plan);

        Assert.DoesNotContain(neu.Termine, t => t.RevierId == 4);
        Assert.Empty(Fehler(p2, neu));
    }

    [Fact]
    public void Zwei_Reviere_ergeben_Warnung_aber_keinen_Fehler_ein_Revier_einen_Fehler()
    {
        var zwei = Demodaten.Reviere().Take(2).ToList();
        var p2 = Planer(reviere: zwei);
        var plan2 = p2.SaisonErzeugen(Start, 6);
        var befunde2 = new PlanPruefer(p2).Pruefen(plan2);
        Assert.DoesNotContain(befunde2, b => b.Schwere == Schwere.Fehler);
        Assert.Contains(befunde2, b => b.Schwere == Schwere.Warnung && b.Text.Contains("Ruhe"));

        var p1 = Planer(reviere: Demodaten.Reviere().Take(1).ToList());
        var plan1 = p1.SaisonErzeugen(Start, 3);
        Assert.Contains(Fehler(p1, plan1), b => b.Text.Contains("Ruhe"));
    }

    [Fact]
    public void Zu_grosse_Gruppe_wird_gemeldet()
    {
        var p = Planer(helfer: 1, gespanne: 18); // 9 je Gruppe, 8 Läufer, Kapazität 7
        var plan = p.SaisonErzeugen(Start, 2);
        var befunde = new PlanPruefer(p).Pruefen(plan);
        Assert.Contains(befunde, b => b.Schwere == Schwere.Warnung && b.Text.Contains("fasst"));
        Assert.Contains(befunde, b => b.Schwere == Schwere.Fehler && b.Text.Contains("keine freie Fährte"));
    }

    [Fact]
    public void Durchgefuehrte_Termine_bleiben_beim_Neuplanen_unveraendert()
    {
        var p = Planer();
        var dienst = new Tauschdienst(p);
        var plan = p.SaisonErzeugen(Start, 8);
        var t1 = plan.Chronologisch.First();
        plan = dienst.Durchfuehren(plan, t1.Id).Plan!;
        var vorher = plan.Termin(t1.Id).Ist.ToList();

        var neu = p.NeuPlanen(plan, vollstaendig: true);

        Assert.True(vorher.SequenceEqual(neu.Termin(t1.Id).Ist), "Ist-Läufe wurden verändert.");
        Assert.Equal(TerminStatus.Durchgefuehrt, neu.Termin(t1.Id).Status);
        Assert.Empty(Fehler(p, neu));
    }

    [Fact]
    public void Nicht_angetretene_Faehrte_gilt_nicht_als_gelaufen()
    {
        var p = Planer();
        var dienst = new Tauschdienst(p);
        var plan = p.SaisonErzeugen(Start, 16);
        var t1 = plan.Chronologisch.First();
        var z = t1.Zuteilungen[0];
        var ist = t1.Zuteilungen.Select(x => new Lauf(x.GespannId, x.FaehrteNr,
            x.GespannId == z.GespannId ? LaufErgebnis.NichtAngetreten : LaufErgebnis.Normal));

        var neu = dienst.Durchfuehren(plan, t1.Id, ist).Plan!;

        Assert.DoesNotContain(Schweissplaner.GezaehlteLaeufe(neu.Termin(t1.Id)), l => l.GespannId == z.GespannId);
        Assert.Empty(Fehler(p, neu));
    }
}
