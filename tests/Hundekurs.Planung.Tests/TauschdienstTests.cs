using Hundekurs.Planung;

namespace Hundekurs.Planung.Tests;

public class TauschdienstTests
{
    private static readonly DateOnly Start = Demodaten.ErsterSonntagImJuni(2027);

    private readonly Schweissplaner _planer = new(Demodaten.Reviere(), Demodaten.Gespanne(), new PlanungsOptionen());
    private Tauschdienst Dienst => new(_planer);

    private List<Befund> Fehler(Saisonplan plan) =>
        new PlanPruefer(_planer).Pruefen(plan).Where(b => b.Schwere == Schwere.Fehler).ToList();

    [Fact]
    public void Einfacher_Faehrtentausch_wird_uebernommen()
    {
        var plan = _planer.SaisonErzeugen(Start, 16);
        var t = plan.Chronologisch.First();
        var (a, b) = (t.Zuteilungen[0], t.Zuteilungen[1]);

        var v = Dienst.FaehrtenTauschen(plan, t.Id, a.GespannId, b.GespannId);

        Assert.True(v.Moeglich, v.Grund);
        var nt = v.Plan!.Termin(t.Id);
        Assert.Equal(b.FaehrteNr, nt.Zuteilungen.Single(z => z.GespannId == a.GespannId).FaehrteNr);
        Assert.Equal(a.FaehrteNr, nt.Zuteilungen.Single(z => z.GespannId == b.GespannId).FaehrteNr);
        Assert.DoesNotContain(v.Befunde, x => x.Schwere == Schwere.Fehler);
        Assert.Contains(v.Aenderungen, x => x.TerminId == t.Id && x.GespannId == a.GespannId);
    }

    [Fact]
    public void Tausch_mit_spaeterem_Konflikt_wird_durch_Folgeaenderung_aufgeloest()
    {
        var plan = _planer.SaisonErzeugen(Start, 16);
        var termine = plan.Chronologisch.ToList();

        // Suche: Termin t, Gespanne a und b; a läuft später im selben Revier genau die Fährte, die b in t hat.
        foreach (var t in termine)
        foreach (var za in t.Zuteilungen)
        foreach (var zb in t.Zuteilungen.Where(z => z.GespannId != za.GespannId && z.FaehrteNr != za.FaehrteNr))
        {
            var spaeter = termine.FirstOrDefault(x => x.Datum > t.Datum && x.RevierId == t.RevierId
                && x.Zuteilungen.Any(z => z.GespannId == za.GespannId && z.FaehrteNr == zb.FaehrteNr));
            if (spaeter is null) continue;

            var v = Dienst.FaehrtenTauschen(plan, t.Id, za.GespannId, zb.GespannId);

            Assert.True(v.Moeglich, v.Grund);
            Assert.DoesNotContain(v.Befunde, x => x.Schwere == Schwere.Fehler);
            Assert.NotEqual(zb.FaehrteNr, v.Plan!.Termin(spaeter.Id).Zuteilungen.Single(z => z.GespannId == za.GespannId).FaehrteNr);
            Assert.Contains(v.Aenderungen, x => x.TerminId == spaeter.Id);
            return;
        }
        Assert.Fail("Keine passende Konstellation im Testplan gefunden.");
    }

    [Fact]
    public void Tausch_gegen_bereits_gelaufene_Faehrte_wird_abgelehnt_mit_Alternativen()
    {
        var plan = _planer.SaisonErzeugen(Start, 16);
        var termine = plan.Chronologisch.ToList();

        foreach (var t in termine)
        foreach (var za in t.Zuteilungen)
        {
            // Späterer Termin im selben Revier, in dem ein anderes Gespann die Fährte hat, die a in t gelaufen ist
            foreach (var t2 in termine.Where(x => x.Datum > t.Datum && x.RevierId == t.RevierId && x.Zuteilungen.Any(z => z.GespannId == za.GespannId)))
            {
                var zb = t2.Zuteilungen.FirstOrDefault(z => z.GespannId != za.GespannId && z.FaehrteNr == za.FaehrteNr);
                if (zb is null) continue;

                var durchgefuehrt = Dienst.Durchfuehren(plan, t.Id).Plan!;
                var v = Dienst.FaehrtenTauschen(durchgefuehrt, t2.Id, za.GespannId, zb.GespannId);

                Assert.False(v.Moeglich);
                Assert.Contains("bereits", v.Grund);
                Assert.NotEmpty(v.Alternativen);
                return;
            }
        }
        Assert.Fail("Keine passende Konstellation im Testplan gefunden.");
    }

    [Fact]
    public void Helfertausch_zwischen_zwei_Terminen_derselben_Gruppe()
    {
        var plan = _planer.SaisonErzeugen(Start, 16);
        var gruppeA = plan.Chronologisch.Where(t => t.Gruppe == Gruppe.A).ToList();
        var (t1, t2) = (gruppeA[0], gruppeA[1]);
        var a = t1.Helfer.Except(t2.Helfer).First();
        var b = t2.Helfer.Except(t1.Helfer).First();

        var v = Dienst.HelferTauschen(plan, t1.Id, a, t2.Id, b);

        Assert.True(v.Moeglich, v.Grund);
        var n1 = v.Plan!.Termin(t1.Id);
        var n2 = v.Plan!.Termin(t2.Id);
        Assert.Contains(b, n1.Helfer);
        Assert.DoesNotContain(a, n1.Helfer);
        Assert.Contains(a, n2.Helfer);
        Assert.DoesNotContain(b, n2.Helfer);
        Assert.DoesNotContain(v.Befunde, x => x.Schwere == Schwere.Fehler);
    }

    [Fact]
    public void Helfertausch_zwischen_Terminen_verschiedener_Gruppen_wird_abgelehnt()
    {
        var plan = _planer.SaisonErzeugen(Start, 4);
        var t1 = plan.Chronologisch.First(t => t.Gruppe == Gruppe.A);
        var t2 = plan.Chronologisch.First(t => t.Gruppe == Gruppe.B);

        var v = Dienst.HelferTauschen(plan, t1.Id, t1.Helfer.First(), t2.Id, t2.Helfer.First());

        Assert.False(v.Moeglich);
    }

    [Fact]
    public void Helfervertretung_durch_jemand_aus_der_Helfergruppe()
    {
        var plan = _planer.SaisonErzeugen(Start, 4);
        var t = plan.Chronologisch.First();
        var helfer = t.Helfer.First();
        var vertretung = _planer.Gespanne.First(g => g.Gruppe != t.Gruppe && !t.Helfer.Contains(g.Id)).Id;

        var v = Dienst.HelferVertreten(plan, t.Id, helfer, vertretung);

        Assert.True(v.Moeglich, v.Grund);
        var nt = v.Plan!.Termin(t.Id);
        Assert.Contains(vertretung, nt.Helfer);
        Assert.DoesNotContain(helfer, nt.Helfer);
        Assert.Equal(2, nt.Helfer.Count);
        Assert.DoesNotContain(v.Befunde, x => x.Schwere == Schwere.Fehler);
    }

    [Fact]
    public void Helfervertretung_durch_jemand_aus_der_Trainingsgruppe_wird_abgelehnt()
    {
        var plan = _planer.SaisonErzeugen(Start, 4);
        var t = plan.Chronologisch.First();

        var v = Dienst.HelferVertreten(plan, t.Id, t.Helfer.First(), t.Zuteilungen[0].GespannId);

        Assert.False(v.Moeglich);
    }

    [Fact]
    public void Abmeldung_aendert_die_anderen_Faehrten_des_Tages_nicht()
    {
        var plan = _planer.SaisonErzeugen(Start, 16);
        var t = plan.Chronologisch.First();
        var weg = t.Zuteilungen[0].GespannId;

        var v = Dienst.Abmelden(plan, t.Id, weg);

        Assert.True(v.Moeglich, v.Grund);
        var nt = v.Plan!.Termin(t.Id);
        Assert.DoesNotContain(nt.Zuteilungen, z => z.GespannId == weg);
        foreach (var z in t.Zuteilungen.Where(z => z.GespannId != weg))
            Assert.Equal(z.FaehrteNr, nt.Zuteilungen.Single(x => x.GespannId == z.GespannId).FaehrteNr);
        Assert.DoesNotContain(v.Befunde, x => x.Schwere == Schwere.Fehler);
    }

    [Fact]
    public void Abgemeldeter_Helfer_wird_ersetzt()
    {
        var plan = _planer.SaisonErzeugen(Start, 4);
        var t = plan.Chronologisch.First();
        var helfer = t.Helfer.First();

        var v = Dienst.Abmelden(plan, t.Id, helfer);

        var nt = v.Plan!.Termin(t.Id);
        Assert.Equal(2, nt.Helfer.Count);
        Assert.DoesNotContain(helfer, nt.Helfer);
        Assert.DoesNotContain(v.Befunde, x => x.Schwere == Schwere.Fehler);
    }

    [Fact]
    public void Nachholen_in_der_anderen_Gruppe()
    {
        var plan = _planer.SaisonErzeugen(Start, 16);
        var tA = plan.Chronologisch.First(t => t.Gruppe == Gruppe.A);
        var g = tA.Zuteilungen[0].GespannId;
        var tB = plan.Chronologisch.First(t => t.Gruppe == Gruppe.B && !t.Helfer.Contains(g));
        var fehltInB = tB.Zuteilungen[0].GespannId;

        var abgemeldet = Dienst.Abmelden(plan, tA.Id, g).Plan!;
        abgemeldet = Dienst.Abmelden(abgemeldet, tB.Id, fehltInB).Plan!;
        var v = Dienst.Nachholen(abgemeldet, tB.Id, g);

        Assert.True(v.Moeglich, v.Grund);
        Assert.Contains(v.Plan!.Termin(tB.Id).Zuteilungen, z => z.GespannId == g);
        Assert.DoesNotContain(v.Befunde, x => x.Schwere == Schwere.Fehler);
    }

    [Fact]
    public void Nachholen_bei_vollem_Schweissblock_wird_abgelehnt()
    {
        var plan = _planer.SaisonErzeugen(Start, 4);
        var tA = plan.Chronologisch.First(t => t.Gruppe == Gruppe.A);
        var g = tA.Zuteilungen[0].GespannId;
        var tB = plan.Chronologisch.First(t => t.Gruppe == Gruppe.B && !t.Helfer.Contains(g));

        var v = Dienst.Nachholen(plan, tB.Id, g);

        Assert.False(v.Moeglich);
        Assert.Contains("voll", v.Grund);
    }

    [Fact]
    public void Nachholen_waehrend_eigenem_Helferdienst_wird_abgelehnt()
    {
        var plan = _planer.SaisonErzeugen(Start, 4);
        var t = plan.Chronologisch.First();

        var v = Dienst.Nachholen(plan, t.Id, t.Helfer.First());

        Assert.False(v.Moeglich);
    }

    [Fact]
    public void Wochenmail_enthaelt_Vorbereitung_Helfer_und_alle_Laeufe()
    {
        var plan = _planer.SaisonErzeugen(Start, 4);
        var t = plan.Chronologisch.First();

        var mail = Wochenmail.Erstellen(_planer, t, "Hallo zusammen,", "Viele Grüße");

        Assert.Contains(t.Datum.ToString("dd.MM."), mail.Betreff);
        Assert.Contains("Gruppe A", mail.Betreff);
        Assert.Contains("VORBEREITUNG", mail.Text);
        Assert.Contains(t.Datum.AddDays(-1).ToString("dd.MM."), mail.Text);
        foreach (var h in t.Helfer) Assert.Contains(_planer.Gespann(h).Fuehrer, mail.Text);
        foreach (var z in t.Zuteilungen) Assert.Contains($"{_planer.Gespann(z.GespannId).Fuehrer} / {_planer.Gespann(z.GespannId).Hund}", mail.Text);
        Assert.Contains("zweiter Lauf", mail.Text);
        Assert.StartsWith("Hallo zusammen,", mail.Text);
    }

    [Fact]
    public void Termintausch_zwischen_den_Gruppen_uebernimmt_die_Faehrte_des_anderen()
    {
        var plan = _planer.SaisonErzeugen(Start, 16);
        var termine = plan.Chronologisch.ToList();
        var gelaufen = (int gespann, Termin bis) => termine.Where(t => t.Datum < bis.Datum)
            .SelectMany(t => t.Zuteilungen.Where(z => z.GespannId == gespann).Select(z => (t.RevierId, z.FaehrteNr))).ToHashSet();

        for (var i = 0; i + 1 < termine.Count; i++)
        {
            var (t1, t2) = (termine[i], termine[i + 1]);
            foreach (var za in t1.Zuteilungen)
            foreach (var zb in t2.Zuteilungen)
            {
                if (t2.Helfer.Contains(za.GespannId) || t1.Helfer.Contains(zb.GespannId)) continue;
                if (gelaufen(zb.GespannId, t1).Contains((t1.RevierId, za.FaehrteNr))) continue;
                if (gelaufen(za.GespannId, t2).Contains((t2.RevierId, zb.FaehrteNr))) continue;
                if (termine.Any(t => t.Datum > t1.Datum && t.RevierId == t1.RevierId && t.Zuteilungen.Any(z => z.GespannId == zb.GespannId && z.FaehrteNr == za.FaehrteNr))) continue;
                if (termine.Any(t => t.Datum > t2.Datum && t.RevierId == t2.RevierId && t.Zuteilungen.Any(z => z.GespannId == za.GespannId && z.FaehrteNr == zb.FaehrteNr))) continue;

                var v = Dienst.TermineTauschen(plan, t1.Id, za.GespannId, t2.Id, zb.GespannId);

                Assert.True(v.Moeglich, v.Grund);
                var n1 = v.Plan!.Termin(t1.Id);
                var n2 = v.Plan!.Termin(t2.Id);
                Assert.Equal(za.FaehrteNr, n1.Zuteilungen.Single(z => z.GespannId == zb.GespannId).FaehrteNr);
                Assert.Equal(zb.FaehrteNr, n2.Zuteilungen.Single(z => z.GespannId == za.GespannId).FaehrteNr);
                Assert.DoesNotContain(n1.Zuteilungen, z => z.GespannId == za.GespannId);
                Assert.DoesNotContain(n2.Zuteilungen, z => z.GespannId == zb.GespannId);
                // Für alle anderen an beiden Terminen ändert sich nichts
                Assert.DoesNotContain(v.Aenderungen, a => (a.TerminId == t1.Id || a.TerminId == t2.Id)
                    && a.GespannId is int g && g != za.GespannId && g != zb.GespannId);
                Assert.DoesNotContain(v.Befunde, x => x.Schwere == Schwere.Fehler);
                return;
            }
        }
        Assert.Fail("Keine passende Konstellation im Testplan gefunden.");
    }

    [Fact]
    public void Termintausch_mit_Helferdienst_am_Zieltermin_wird_abgelehnt()
    {
        var plan = _planer.SaisonErzeugen(Start, 4);
        var t1 = plan.Chronologisch.First();             // Gruppe A, Helfer aus B
        var t2 = plan.Chronologisch.Skip(1).First();     // Gruppe B
        var b = t1.Helfer.First(g => t2.Zuteilungen.Any(z => z.GespannId == g));

        var v = Dienst.TermineTauschen(plan, t1.Id, t1.Zuteilungen[0].GespannId, t2.Id, b);

        Assert.False(v.Moeglich);
        Assert.Contains("Helferdienst", v.Grund);
    }

    [Fact]
    public void Termintausch_haelt_Kapazitaet_und_Regeln_ein()
    {
        var plan = _planer.SaisonErzeugen(Start, 8);
        var t1 = plan.Chronologisch.First();
        var t2 = plan.Chronologisch.Skip(1).First();
        var a = t1.Zuteilungen.First(z => !t2.Helfer.Contains(z.GespannId)).GespannId;
        var b = t2.Zuteilungen.First(z => !t1.Helfer.Contains(z.GespannId)).GespannId;

        var v = Dienst.TermineTauschen(plan, t1.Id, a, t2.Id, b);

        Assert.True(v.Moeglich, v.Grund);
        Assert.Equal(7, v.Plan!.Termin(t1.Id).Zuteilungen.Count);
        Assert.Equal(7, v.Plan!.Termin(t2.Id).Zuteilungen.Count);
        Assert.DoesNotContain(v.Befunde, x => x.Schwere == Schwere.Fehler);
    }
}
