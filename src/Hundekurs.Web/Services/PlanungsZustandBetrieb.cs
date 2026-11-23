using Hundekurs.Daten;
using Hundekurs.Planung;
using Einsatzart = Hundekurs.Domain.Einsatzart;
using Terminart = Hundekurs.Domain.Terminart;
using Terminstatus = Hundekurs.Domain.Terminstatus;
using Hundekurs.Planung.Strecken;

namespace Hundekurs.Web.Services;

/// <summary>Kursjahr, Teil 1, Apportierarbeit, Zulassung, Orte, Einsätze und km.</summary>
public sealed partial class PlanungsZustand
{
    /// <summary>Strecken werden höchstens 7 Tage aus dem Zwischenspeicher genommen, danach neu berechnet (Baustellen, Umleitungen).</summary>
    public static readonly TimeSpan StreckenHoechstalter = TimeSpan.FromDays(7);

    public KursEinstellungen Kurs { get; private set; } = null!;
    public List<GelaendeDaten> Gelaende { get; private set; } = [];
    public bool RoutendienstVerfuegbar => _routendienst.Verfuegbar;

    // ---------- Kursjahr und Einstellungen ----------

    /// <summary>Legt alle Termine eines Kursjahres neu an: Teil 1, Teil 2 (mit Schweißplanung) und Apportierarbeit.</summary>
    public void KursjahrAnlegen(int jahr, DateOnly? beginnTeil2 = null)
    {
        var kalender = Kurskalender.Erzeugen(jahr, beginnTeil2: beginnTeil2);
        lock (_sperre)
        {
            _speicher.NebentermineAnlegen(_kursId, kalender);
            Neuladen();
        }
        SaisonErzeugen(kalender.Training[0], kalender.Training.Count);
        _speicher.Protokollieren(_kursId,
            $"Kursjahr {jahr} angelegt: Teil 1 ab {kalender.Gehorsam[0]:dd.MM.} ({kalender.Gehorsam.Count} Sonntage, ohne Ostersonntag {kalender.Ostersonntag:dd.MM.}), " +
            $"Teil 2 ab {kalender.Training[0]:dd.MM.}, Apportierarbeit ab {kalender.Apportierarbeit[0]:dd.MM.}", []);
        lock (_sperre) Neuladen();
        Geaendert?.Invoke();
    }

    /// <summary>Erster Schweißsonntag des laufenden Kursjahres, falls Termine angelegt sind.</summary>
    public DateOnly? BeginnTeil2 => Plan.Termine.Count == 0 ? null : Plan.Termine.Min(t => t.Datum);

    /// <summary>
    /// Verschiebt Teil 2 (Schweißarbeit) und die Apportierarbeit auf einen neuen ersten Schweißsonntag.
    /// Teil 1 mit seinen Anwesenheiten bleibt erhalten. Der Schweißplan wird neu berechnet,
    /// von Hand gesetzte Reviere, Tausche und Abmeldungen gehen dabei verloren.
    /// </summary>
    public void Teil2Verschieben(DateOnly beginn)
    {
        var bisher = BeginnTeil2;
        int anzahlTraining, anzahlApport;
        IReadOnlyList<DateOnly> training;
        lock (_sperre)
        {
            if (Plan.Termine.Any(t => t.Status == TerminStatus.Durchgefuehrt))
                throw new InvalidOperationException("Im Schweißplan sind schon Termine durchgeführt – Teil 2 lässt sich nicht mehr verschieben.");

            var gehorsam = Nebentermine(Terminart.Gehorsam);
            if (gehorsam.Count == 0)
                throw new InvalidOperationException("Es ist noch kein Kursjahr angelegt.");
            if (Kurskalender.BeginnTeil2Pruefen(beginn, gehorsam.Max(t => t.Datum)) is { } fehler)
                throw new InvalidOperationException(fehler);

            anzahlTraining = Plan.Termine.Count > 0 ? Plan.Termine.Count : 12;
            var apport = Nebentermine(Terminart.Apportierarbeit);
            anzahlApport = apport.Count > 0 ? apport.Count : 12;

            var neu = Kurskalender.Teil2(beginn, anzahlTraining, anzahlApport);
            training = neu.Training;
            _speicher.ApportiertermineVerschieben(_kursId, neu.Apportierarbeit, training[^1]);
            Neuladen();
        }
        SaisonErzeugen(training[0], anzahlTraining);
        _speicher.Protokollieren(_kursId,
            $"Teil 2 verschoben: Schweißarbeit ab {training[0]:dd.MM.yyyy} (bisher {bisher?.ToString("dd.MM.yyyy") ?? "–"}), Apportierarbeit ab {training[0].AddDays(-4):dd.MM.yyyy}", []);
        lock (_sperre) Neuladen();
        Geaendert?.Invoke();
    }

    public void KursEinstellungenSpeichern(KursEinstellungen e)
    {
        lock (_sperre)
        {
            _speicher.KursEinstellungenSpeichern(e);
            _speicher.Protokollieren(_kursId, $"Kurseinstellungen geändert (km-Satz {e.KmSatz:0.00} €)", []);
            Neuladen();
        }
        Geaendert?.Invoke();
    }

    public void GelaendeSpeichern(GelaendeDaten d)
    {
        lock (_sperre)
        {
            _speicher.GelaendeSpeichern(d);
            _speicher.Protokollieren(_kursId, $"Ort {d.Bezeichnung} {(d.Id is null ? "angelegt" : "geändert")}", []);
            Neuladen();
        }
        Geaendert?.Invoke();
    }

    public Dictionary<int, Koordinate?> RevierKoordinaten() => _speicher.RevierKoordinaten();

    public string GelaendeName(int? id) => Gelaende.FirstOrDefault(g => g.Id == id)?.Bezeichnung ?? "–";

    // ---------- Teil 1 und Apportierarbeit ----------

    public List<NebenterminDaten> Nebentermine(Terminart art) => _speicher.Nebentermine(_kursId, art);

    /// <summary>Gespanne, die den jeweiligen Teil gewählt haben (Grundgehorsam bzw. Nachsuche auf Niederwild).</summary>
    public List<TeilnehmerDaten> Gebucht(Terminart art) => Teilnehmer()
        .Where(t => art == Terminart.Gehorsam ? t.Gehorsam : t.Niederwild)
        .OrderBy(t => t.Nachname).ThenBy(t => t.Vorname)
        .ToList();

    public async Task<string?> AnwesenheitSpeichern(NebenterminDaten termin, Terminart art, IReadOnlySet<int> anwesend, IReadOnlySet<int> ausbilder, string? notiz)
    {
        var gebucht = Gebucht(art).Select(t => t.KursteilnahmeId!.Value).ToList();
        lock (_sperre)
        {
            _speicher.AnwesenheitSpeichern(termin.Id, gebucht, anwesend, notiz);
            _speicher.EinsaetzeSetzen(termin.Id, art == Terminart.Gehorsam ? Einsatzart.Gehorsam : Einsatzart.Apportierarbeit,
                termin.Datum, null, termin.GelaendeId, ausbilder);
            _speicher.Protokollieren(_kursId,
                $"{Bezeichnung(art)} {termin.Datum:dd.MM.}: Anwesenheit erfasst ({anwesend.Count(gebucht.Contains)} von {gebucht.Count}), {ausbilder.Count} Ausbilder", []);
        }
        var meldung = await KmBerechnen(_speicher.EinsaetzeOhneKm(termin.Datum, termin.Datum));
        Geaendert?.Invoke();
        return meldung;
    }

    public void TerminStatusSetzen(int terminId, DateOnly datum, Terminart art, Terminstatus status)
    {
        lock (_sperre)
        {
            _speicher.TerminStatusSetzen(terminId, status);
            _speicher.Protokollieren(_kursId,
                $"{Bezeichnung(art)} {datum:dd.MM.} {(status == Terminstatus.Abgesagt ? "abgesagt" : "wieder eingeplant")}", []);
        }
        Geaendert?.Invoke();
    }

    public void TerminNotizSpeichern(int terminId, string? notiz) => _speicher.TerminNotizSpeichern(terminId, notiz);

    /// <summary>Zulassungsstand aller Kursteilnahmen (Schlüssel: Kursteilnahme-Id).</summary>
    public Dictionary<int, Zulassungsstand> Zulassungen()
    {
        var zahlen = _speicher.GehorsamZahlen(_kursId);
        return Teilnehmer().ToDictionary(
            t => t.KursteilnahmeId!.Value,
            t =>
            {
                var z = zahlen.GetValueOrDefault(t.KursteilnahmeId!.Value) ?? new Anwesenheitszahlen(0, 0, 0);
                return new Zulassungsstand(t.KursteilnahmeId.Value, t.Gehorsam ? z.Anwesend : 0,
                    z.Stattgefunden, z.Ausstehend, t.ZulassungAusnahme);
            });
    }

    /// <summary>
    /// Wer neu einer Schweißgruppe zugeordnet wird, braucht die Zulassung (60 % in Teil 1) oder eine begründete Ausnahme.
    /// Bereits eingeteilte Gespanne können weiter bearbeitet werden.
    /// </summary>
    private void ZulassungPruefen(TeilnehmerDaten d)
    {
        if (d.ZulassungAusnahme && string.IsNullOrWhiteSpace(d.ZulassungBegruendung))
            throw new InvalidOperationException("Für eine Ausnahme bei der Zulassung ist eine Begründung nötig.");
        // Eine Schweißgruppe gibt es nur mit Schalenwild – dort ist Teil 1 immer Pflicht.
        if (d.Gruppe is null || d.ZulassungAusnahme || !d.Ausbildung.ZulassungErforderlich) return;

        var bisher = d.KursteilnahmeId is int id ? Teilnehmer().FirstOrDefault(t => t.KursteilnahmeId == id) : null;
        if (bisher?.Gruppe is not null) return;

        var zahlen = d.KursteilnahmeId is int k ? _speicher.GehorsamZahlen(_kursId).GetValueOrDefault(k) : null;
        var gehorsamTermine = Nebentermine(Terminart.Gehorsam).Count(t => t.Status != Terminstatus.Abgesagt);
        if (gehorsamTermine == 0) return; // ohne Teil 1 (z. B. alter Kurs ohne Anwesenheiten) keine Prüfung

        var stand = new Zulassungsstand(d.KursteilnahmeId ?? 0, d.Gehorsam ? zahlen?.Anwesend ?? 0 : 0,
            zahlen?.Stattgefunden ?? 0, zahlen?.Ausstehend ?? gehorsamTermine, false);
        if (!stand.Zugelassen)
            throw new InvalidOperationException(
                $"Keine Zulassung zu Teil 2: {stand.Anwesend} Anwesenheiten in Teil 1, nötig sind {stand.Erforderlich} von {stand.Termine}" +
                (stand.Ausstehend > 0 ? $" (noch {stand.Ausstehend} Termine offen)" : "") +
                ". Für eine Ausnahme bitte „Ausnahme“ ankreuzen und begründen.");
    }

    // ---------- Einsätze Teil 2 ----------

    public (HashSet<int> Vorbereitung, HashSet<int> Training) TrainingEinsaetze(int terminId) =>
        (_speicher.EinsatzPersonen(terminId, Einsatzart.Vorbereitung), _speicher.EinsatzPersonen(terminId, Einsatzart.Training));

    public async Task<string?> TrainingEinsaetzeSpeichern(Termin termin, IReadOnlySet<int> vorbereitung, IReadOnlySet<int> training)
    {
        lock (_sperre)
        {
            _speicher.EinsaetzeSetzen(termin.Id, Einsatzart.Vorbereitung, termin.Datum.AddDays(-1), termin.RevierId, null, vorbereitung);
            _speicher.EinsaetzeSetzen(termin.Id, Einsatzart.Training, termin.Datum, termin.RevierId, null, training);
            _speicher.Protokollieren(_kursId,
                $"Ausbilder für {termin.Datum:dd.MM.} erfasst: Vorbereitung {vorbereitung.Count}, Training {training.Count}", []);
        }
        var meldung = await KmBerechnen(_speicher.EinsaetzeOhneKm(termin.Datum.AddDays(-1), termin.Datum));
        Geaendert?.Invoke();
        return meldung;
    }

    // ---------- km ----------

    public List<EinsatzZeile> Einsaetze(DateOnly von, DateOnly bis) => _speicher.Einsaetze(von, bis);

    public void EinsatzKorrigieren(EinsatzZeile e, double? kmEinfach, string? bemerkung)
    {
        _speicher.EinsatzKorrigieren(e.Id, kmEinfach, bemerkung);
        _speicher.Protokollieren(_kursId,
            $"km korrigiert: {e.Person}, {e.Datum:dd.MM.} {e.Ziel}: {(kmEinfach is null ? "Korrektur entfernt" : $"{kmEinfach:0.#} km einfach")}" +
            (string.IsNullOrWhiteSpace(bemerkung) ? "" : $" ({bemerkung.Trim()})"), []);
        Geaendert?.Invoke();
    }

    /// <summary>
    /// Berechnet fehlende Strecken: zuerst Wohnadressen in Koordinaten umrechnen (nur die Adresse wird übertragen),
    /// dann je Einsatz die Strecke – aus dem Zwischenspeicher, wenn sie höchstens 7 Tage alt ist.
    /// </summary>
    public async Task<string?> KmBerechnen(IEnumerable<int> einsatzIds, bool neuBerechnen = false)
    {
        var ids = einsatzIds.ToList();
        if (ids.Count == 0) return null;
        if (!_routendienst.Verfuegbar) return "Kein Routendienst eingerichtet – bitte km von Hand eintragen.";

        var probleme = new List<string>();
        try
        {
            foreach (var (personId, adresse) in _speicher.AdressenOhneKoordinaten(_kursId))
            {
                var k = await _routendienst.AdresseSuchen(adresse);
                if (k is null) probleme.Add($"Adresse „{adresse}“ wurde nicht gefunden.");
                else _speicher.PersonKoordinatenSetzen(personId, k, adresse);
            }

            var berechnet = 0;
            var ohneKoordinaten = 0;
            foreach (var id in ids)
            {
                var fahrt = _speicher.Fahrtkoordinaten(id);
                if (fahrt is null) { ohneKoordinaten++; continue; }

                var zwischen = neuBerechnen ? null : _speicher.StreckeAusZwischenspeicher(id, StreckenHoechstalter);
                if (zwischen is double km)
                {
                    _speicher.EinsatzKmBerechnetSetzen(id, km, zwischenspeichern: false);
                }
                else
                {
                    km = await _routendienst.StreckeKm(fahrt.Value.Von, fahrt.Value.Nach);
                    _speicher.EinsatzKmBerechnetSetzen(id, km, zwischenspeichern: true);
                }
                berechnet++;
            }
            if (ohneKoordinaten > 0)
                probleme.Add($"{ohneKoordinaten} Einsätze ohne Koordinaten (Wohnadresse oder Ort fehlt) – dort bitte km von Hand eintragen.");
            if (berechnet > 0) Geaendert?.Invoke();
        }
        catch (RoutendienstException ex)
        {
            probleme.Add(ex.Message);
        }
        return probleme.Count == 0 ? null : string.Join(" ", probleme);
    }

    public static string Bezeichnung(Terminart art) => art switch
    {
        Terminart.Gehorsam => "Gehorsam",
        Terminart.Apportierarbeit => "Nachsuche auf Niederwild",
        Terminart.Pruefung => "Prüfung",
        _ => "Training",
    };

    public static string Bezeichnung(Einsatzart art) => art switch
    {
        Einsatzart.Vorbereitung => "Vorbereitung",
        Einsatzart.Training => "Schweißtraining",
        Einsatzart.Pruefung => "Prüfung",
        Einsatzart.Faehrtenauffrischung => "Fährtenauffrischung",
        Einsatzart.Gehorsam => "Gehorsam",
        _ => "Niederwild",
    };
}
