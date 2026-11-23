using Hundekurs.Daten;
using Hundekurs.Planung;
using Hundekurs.Planung.Strecken;

namespace Hundekurs.Web.Services;

public enum Kontaktrolle { Teilnehmer, Ausbilder }

public sealed record Kontakt(string Name, string EMail, Kontaktrolle Rolle);

/// <summary>Einstellungen der Wochenmail. Werden in der Datenbank am Kurs gespeichert.</summary>
public sealed class Mailvorlage
{
    public string Einleitung { get; set; } = Kursspeicher.StandardEinleitung;
    public string Gruss { get; set; } = Kursspeicher.StandardGruss;
    public HashSet<string> AbgewaehlteEmpfaenger { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record DatenbankInfo(string Pfad, bool Demodaten);

/// <summary>
/// Hält den aktuellen Kursstand im Speicher und schreibt jede Änderung sofort in die Datenbank.
/// Alle Nutzer dieser App-Instanz sehen denselben Stand.
/// </summary>
public sealed partial class PlanungsZustand
{
    private readonly Lock _sperre = new();
    private readonly Kursspeicher _speicher;
    private readonly int _kursId;
    private readonly IRoutendienst _routendienst;
    private Planungsdaten _daten = null!;

    public PlanungsZustand(Kursspeicher speicher, DatenbankInfo info, IRoutendienst routendienst)
    {
        _speicher = speicher;
        _routendienst = routendienst;
        Info = info;
        _kursId = speicher.KursSicherstellen(info.Demodaten);
        Neuladen();

        // Mit Demodaten gleich ein Kursjahr anlegen, damit es etwas zu sehen gibt.
        if (info.Demodaten && Plan.Termine.Count == 0 && Gespanne.Count > 0)
            KursjahrAnlegen(DateTime.Today.Year + 1);
    }

    public DatenbankInfo Info { get; }
    public string Kursbezeichnung => _daten.Kursbezeichnung;
    public List<Revier> Reviere => _daten.Reviere;
    public List<Gespann> Gespanne => _daten.Gespanne;
    public PlanungsOptionen Optionen => _daten.Optionen;
    public Saisonplan Plan => _daten.Plan;
    public List<Kontakt> Kontakte { get; private set; } = [];
    public Mailvorlage Mailvorlage { get; private set; } = new();

    public Schweissplaner Planer => new(Reviere, Gespanne, Optionen);
    public Tauschdienst Tauschdienst => new(Planer);
    public IReadOnlyList<Befund> Befunde => new PlanPruefer(Planer).Pruefen(Plan);
    public IReadOnlyList<Historieneintrag> Historie => _speicher.Historie();

    /// <summary>Wird ausgelöst, wenn sich etwas ändert, damit offene Seiten neu zeichnen.</summary>
    public event Action? Geaendert;

    public Gespann Gespann(int id) => Gespanne.SingleOrDefault(g => g.Id == id) ?? new Gespann(id, $"Gespann {id}", "", Gruppe.A);
    public Revier? Revier(int? id) => id is int r ? Reviere.SingleOrDefault(x => x.Id == r) : null;
    public string Name(int gespannId) => Gespann(gespannId) is var g && g.Hund.Length > 0 ? $"{g.Fuehrer} / {g.Hund}" : $"Gespann {gespannId}";

    public IReadOnlyList<Kontakt> AusgewaehlteEmpfaenger =>
        Kontakte.Where(k => !Mailvorlage.AbgewaehlteEmpfaenger.Contains(k.EMail)).ToList();

    public Mailinhalt Wochenmail(int terminId) =>
        Planung.Wochenmail.Erstellen(Planer, Plan.Termin(terminId), Mailvorlage.Einleitung, Mailvorlage.Gruss);

    /// <summary>Nächster Termin, der noch nicht durchgeführt ist.</summary>
    public Termin? NaechsterTermin =>
        Plan.Chronologisch.FirstOrDefault(t => t.Status != TerminStatus.Durchgefuehrt && t.Datum >= DateOnly.FromDateTime(DateTime.Today))
        ?? Plan.Chronologisch.FirstOrDefault(t => t.Status != TerminStatus.Durchgefuehrt);

    // ---------- Plan ----------

    public void SaisonErzeugen(DateOnly start, int anzahl)
    {
        lock (_sperre)
        {
            var leer = _speicher.SaisonTermineAnlegen(_kursId, start, anzahl, Gruppe.A);
            var neu = Planer.NeuPlanen(leer, ab: null, vollstaendig: true);
            _speicher.PlanSpeichern(_kursId, neu, $"Neue Saison ab {start:dd.MM.yyyy} mit {anzahl} Terminen angelegt", []);
            _daten = _daten with { Plan = neu };
        }
        Geaendert?.Invoke();
    }

    public void NeuPlanen(bool vollstaendig)
    {
        lock (_sperre)
        {
            var neu = Planer.NeuPlanen(Plan, ab: DateOnly.FromDateTime(DateTime.Today), vollstaendig: vollstaendig);
            PlanErsetzen(neu, vollstaendig ? "Plan vollständig neu berechnet" : "Plan aktualisiert");
        }
        Geaendert?.Invoke();
    }

    /// <summary>Übernimmt einen bestätigten Vorschlag. Schlägt fehl, wenn der Plan inzwischen geändert wurde.</summary>
    public bool Uebernehmen(Saisonplan basis, Aenderungsvorschlag vorschlag, string aktion)
    {
        if (!vorschlag.Moeglich || vorschlag.Plan is null) return false;
        lock (_sperre)
        {
            if (!ReferenceEquals(basis, Plan)) return false;
            _speicher.PlanSpeichern(_kursId, vorschlag.Plan, aktion, Beschreiben(vorschlag.Aenderungen));
            _daten = _daten with { Plan = vorschlag.Plan };
        }
        Geaendert?.Invoke();
        return true;
    }

    public void RevierFestlegen(int terminId, int? revierId)
    {
        lock (_sperre)
        {
            var neu = Plan.Kopie();
            var t = neu.Termin(terminId);
            t.RevierId = revierId;
            t.RevierFixiert = revierId is not null;
            t.ZuteilungFixiert = false;
            PlanErsetzen(Planer.NeuPlanen(neu, ab: t.Datum),
                $"Revier für {t.Datum:dd.MM.} {(revierId is null ? "wieder automatisch" : "von Hand festgelegt")}");
        }
        Geaendert?.Invoke();
    }

    public void Veroeffentlichen(int terminId)
    {
        lock (_sperre)
        {
            var neu = Plan.Kopie();
            neu.Termin(terminId).Status = TerminStatus.Veroeffentlicht;
            PlanErsetzen(neu, $"Termin {neu.Termin(terminId).Datum:dd.MM.} veröffentlicht");
        }
        Geaendert?.Invoke();
    }

    // ---------- Stammdaten ----------

    public void RevierSpeichern(RevierDaten d) =>
        Stammdaten(() => _speicher.RevierSpeichern(d), $"Revier {d.Name} {(d.Id is null ? "angelegt" : "geändert")}");

    public List<TeilnehmerDaten> Teilnehmer() => _speicher.TeilnehmerListe(_kursId);

    public void TeilnehmerSpeichern(TeilnehmerDaten d)
    {
        ZulassungPruefen(d);
        var aktion = $"Teilnehmer {d.Vorname} {d.Nachname} / {d.Hund} {(d.KursteilnahmeId is null ? "angelegt" : "geändert")}";
        if (d.ZulassungAusnahme && !string.IsNullOrWhiteSpace(d.ZulassungBegruendung))
            aktion += $" – Zulassung zu Teil 2 als Ausnahme: {d.ZulassungBegruendung.Trim()}";
        Stammdaten(() => _speicher.TeilnehmerSpeichern(_kursId, d), aktion);
    }

    public void TeilnehmerLoeschen(int kursteilnahmeId)
    {
        var name = Name(kursteilnahmeId);
        Stammdaten(() => _speicher.TeilnehmerLoeschen(kursteilnahmeId), $"Teilnehmer {name} gelöscht");
    }

    public List<AusbilderDaten> Ausbilder() => _speicher.AusbilderListe(_kursId);

    public void AusbilderSpeichern(AusbilderDaten d) =>
        Stammdaten(() => _speicher.AusbilderSpeichern(_kursId, d), $"Ausbilder {d.Vorname} {d.Nachname} {(d.PersonId is null ? "angelegt" : "geändert")}");

    public void AusbilderEntfernen(int personId) =>
        Stammdaten(() => _speicher.AusbilderEntfernen(_kursId, personId), "Ausbilder entfernt");

    // ---------- Wochenmail ----------

    public void MailvorlageSpeichern()
    {
        _speicher.MailEinstellungenSpeichern(_kursId,
            new MailEinstellungen(Mailvorlage.Einleitung, Mailvorlage.Gruss, Mailvorlage.AbgewaehlteEmpfaenger.Order().ToList()));
    }

    // ---------- intern ----------

    /// <summary>Speichert eine Stammdatenänderung, lädt neu und passt den Plan ab heute an.</summary>
    private void Stammdaten(Action speichern, string aktion)
    {
        lock (_sperre)
        {
            var alt = Plan;
            speichern();
            _speicher.Protokollieren(_kursId, aktion, []);
            Neuladen();

            var neu = Planer.NeuPlanen(Plan, ab: DateOnly.FromDateTime(DateTime.Today));
            var aenderungen = Tauschdienst.Vergleichen(Planer, alt, neu);
            if (aenderungen.Count > 0)
            {
                _speicher.PlanSpeichern(_kursId, neu, "Plan an geänderte Stammdaten angepasst", Beschreiben(aenderungen));
                _daten = _daten with { Plan = neu };
            }
        }
        Geaendert?.Invoke();
    }

    private void PlanErsetzen(Saisonplan neu, string aktion)
    {
        _speicher.PlanSpeichern(_kursId, neu, aktion, Beschreiben(Tauschdienst.Vergleichen(Planer, Plan, neu)));
        _daten = _daten with { Plan = neu };
    }

    private void Neuladen()
    {
        _daten = _speicher.Laden(_kursId);
        Kurs = _speicher.KursEinstellungenLaden(_kursId);
        Gelaende = _speicher.GelaendeListe();
        Faecher = _speicher.FaecherListe();
        Gebuehrenpakete = _speicher.Gebuehrenpakete(_kursId);
        Kontakte = _speicher.Mailkontakte(_kursId)
            .Select(k => new Kontakt(k.Name, k.EMail, k.IstAusbilder ? Kontaktrolle.Ausbilder : Kontaktrolle.Teilnehmer))
            .ToList();
        var e = _speicher.MailEinstellungenLaden(_kursId);
        var vorlage = new Mailvorlage { Einleitung = e.Einleitung, Gruss = e.Gruss };
        foreach (var a in e.Abgewaehlt) vorlage.AbgewaehlteEmpfaenger.Add(a);
        Mailvorlage = vorlage;
    }

    private static List<string> Beschreiben(IEnumerable<Aenderung> aenderungen) =>
        aenderungen.Select(a => $"{a.Datum:dd.MM.}: {a.Beschreibung}").ToList();
}
