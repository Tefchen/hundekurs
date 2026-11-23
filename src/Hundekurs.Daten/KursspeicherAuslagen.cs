using Hundekurs.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hundekurs.Daten;

/// <summary>Eine Auslage. <paramref name="PersonId"/> null = direkt aus der Vereinskasse bezahlt.</summary>
public sealed record AuslageDaten(
    int? Id, DateOnly Datum, Auslagenart Art, string Bezeichnung, decimal Betrag, int? PersonId, string? Anlass,
    bool BelegLiegtVor, DateOnly? ErstattetAm, string? Bemerkung);

/// <summary>Auslagen: Verpflegung, Anschaffungen und Sonstiges.</summary>
public sealed partial class Kursspeicher
{
    public List<AuslageDaten> Auslagen(int kursId)
    {
        using var db = fabrik.CreateDbContext();
        return db.Auslagen.AsNoTracking().Where(a => a.KursId == kursId).OrderBy(a => a.Datum).ThenBy(a => a.Id).ToList()
            .Select(a => new AuslageDaten(a.Id, a.Datum, a.Art, a.Bezeichnung, a.Betrag, a.PersonId, a.Anlass, a.BelegLiegtVor, a.ErstattetAm, a.Bemerkung))
            .ToList();
    }

    public int AuslageSpeichern(int kursId, AuslageDaten d)
    {
        if (string.IsNullOrWhiteSpace(d.Bezeichnung)) throw new InvalidOperationException("Bitte angeben, wofür das Geld ausgegeben wurde.");
        if (d.Betrag <= 0) throw new InvalidOperationException("Der Betrag muss größer als 0 sein.");
        if (d.ErstattetAm is not null && d.PersonId is null)
            throw new InvalidOperationException("Erstattet werden kann nur an eine Person. Bei Zahlung aus der Vereinskasse bitte keine Erstattung vermerken.");

        using var db = fabrik.CreateDbContext();
        var a = d.Id is int id
            ? db.Auslagen.Single(x => x.Id == id && x.KursId == kursId)
            : db.Auslagen.Add(new Auslage { KursId = kursId, Bezeichnung = d.Bezeichnung }).Entity;
        a.Datum = d.Datum;
        a.Art = d.Art;
        a.Bezeichnung = d.Bezeichnung.Trim();
        a.Betrag = Math.Round(d.Betrag, 2, MidpointRounding.AwayFromZero);
        a.PersonId = d.PersonId;
        a.Anlass = Leer(d.Anlass);
        a.BelegLiegtVor = d.BelegLiegtVor;
        a.ErstattetAm = d.PersonId is null ? null : d.ErstattetAm;
        a.Bemerkung = Leer(d.Bemerkung);
        db.SaveChanges();
        return a.Id;
    }

    public void AuslageLoeschen(int kursId, int auslageId)
    {
        using var db = fabrik.CreateDbContext();
        db.Auslagen.Where(a => a.Id == auslageId && a.KursId == kursId).ExecuteDelete();
    }

    public void AuslageErstattetSetzen(int kursId, int auslageId, DateOnly? am)
    {
        using var db = fabrik.CreateDbContext();
        var a = db.Auslagen.Single(x => x.Id == auslageId && x.KursId == kursId);
        if (a.PersonId is null && am is not null) throw new InvalidOperationException("Aus der Vereinskasse bezahlt – keine Erstattung nötig.");
        a.ErstattetAm = am;
        db.SaveChanges();
    }
}
