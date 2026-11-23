using Hundekurs.Daten;
using Hundekurs.Planung;

namespace Hundekurs.Web.Services;

/// <summary>Fächer, Personen, Hunde und Kursgebühren.</summary>
public sealed partial class PlanungsZustand
{
    public List<FachDaten> Faecher { get; private set; } = [];
    public List<Gebuehrenpaket> Gebuehrenpakete { get; private set; } = [];

    public string FachName(int id) => Faecher.FirstOrDefault(f => f.Id == id)?.Bezeichnung ?? $"Fach {id}";

    public string FaecherText(IEnumerable<int> ids) =>
        string.Join(", ", Faecher.Where(f => ids.Contains(f.Id!.Value)).Select(f => f.Bezeichnung));

    // ---------- Fächer ----------

    public void FachSpeichern(FachDaten d) =>
        Einfach(() => _speicher.FachSpeichern(d), $"Fach {d.Bezeichnung} {(d.Id is null ? "angelegt" : "geändert")}");

    // ---------- Personen ----------

    public List<PersonDaten> Personen() => _speicher.PersonenListe(_kursId);

    public void PersonSpeichern(PersonDaten d) =>
        Stammdaten(() => _speicher.PersonSpeichern(_kursId, d), $"Person {d.Vorname} {d.Nachname} {(d.Id is null ? "angelegt" : "geändert")}");

    public void PersonLoeschen(PersonDaten d) =>
        Stammdaten(() => _speicher.PersonLoeschen(d.Id!.Value), $"Person {d.Vorname} {d.Nachname} gelöscht");

    // ---------- Hunde ----------

    public List<HundDaten> Hunde() => _speicher.HundeListe();
    public List<string> Rassen() => _speicher.Rassen();

    public void HundSpeichern(HundDaten d) =>
        Stammdaten(() => _speicher.HundSpeichern(d), $"Hund {d.Rufname} {(d.Id is null ? "angelegt" : "geändert")}");

    public void HundLoeschen(HundDaten d) =>
        Stammdaten(() => _speicher.HundLoeschen(d.Id!.Value), $"Hund {d.Rufname} gelöscht");

    // ---------- Gebühren ----------

    public Gebuehr Gebuehr(TeilnehmerDaten t) =>
        Gebuehrenregel.Berechnen(Gebuehrenpakete, t.Ausbildung.Bereinigt(), t.Vereinsmitglied, t.Wiederholer, t.GebuehrVonHand, t.GebuehrHinweis);

    public void GebuehrenpaketSpeichern(Gebuehrenpaket p) =>
        Einfach(() => _speicher.GebuehrenpaketSpeichern(_kursId, p),
            $"Kursgebühr {p.Bezeichnung} {(p.Id == 0 ? "angelegt" : "geändert")}: {Gebuehrenregel.Euro(p.Preis)}, Mitglieder {Gebuehrenregel.Euro(p.Mitgliederpreis)}");

    public void GebuehrenpaketLoeschen(Gebuehrenpaket p) =>
        Einfach(() => _speicher.GebuehrenpaketLoeschen(_kursId, p.Id), $"Kursgebühr {p.Bezeichnung} gelöscht");

    public void BezahltSetzen(TeilnehmerDaten t, DateOnly? am) =>
        Einfach(() => _speicher.BezahltSetzen(t.KursteilnahmeId!.Value, am),
            $"Kursgebühr {t.Vorname} {t.Nachname} / {t.Hund}: {(am is DateOnly d ? $"bezahlt am {d:dd.MM.yyyy}" : "wieder offen")}");

    // ---------- Auslagen ----------

    public List<AuslageDaten> Auslagen() => _speicher.Auslagen(_kursId);

    public void AuslageSpeichern(AuslageDaten d) =>
        Einfach(() => _speicher.AuslageSpeichern(_kursId, d),
            $"Auslage {d.Bezeichnung} ({Gebuehrenregel.Euro(d.Betrag)}, {d.Datum:dd.MM.yyyy}) {(d.Id is null ? "erfasst" : "geändert")}");

    public void AuslageLoeschen(AuslageDaten d) =>
        Einfach(() => _speicher.AuslageLoeschen(_kursId, d.Id!.Value), $"Auslage {d.Bezeichnung} ({Gebuehrenregel.Euro(d.Betrag)}) gelöscht");

    public void AuslageErstattetSetzen(AuslageDaten d, DateOnly? am) =>
        Einfach(() => _speicher.AuslageErstattetSetzen(_kursId, d.Id!.Value, am),
            $"Auslage {d.Bezeichnung}: {(am is DateOnly t ? $"erstattet am {t:dd.MM.yyyy}" : "Erstattung zurückgenommen")}");

    public static string ArtText(Hundekurs.Domain.Auslagenart a) => a switch
    {
        Hundekurs.Domain.Auslagenart.Verpflegung => "Verpflegung",
        Hundekurs.Domain.Auslagenart.Anschaffung => "Anschaffung",
        _ => "Sonstiges",
    };

    /// <summary>Speichert ohne Auswirkung auf die Schweißplanung, protokolliert und lädt neu.</summary>
    private void Einfach(Action speichern, string aktion)
    {
        lock (_sperre)
        {
            speichern();
            _speicher.Protokollieren(_kursId, aktion, []);
            Neuladen();
        }
        Geaendert?.Invoke();
    }
}
