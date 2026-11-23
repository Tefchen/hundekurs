namespace Hundekurs.Planung;

/// <summary>Trainingsgruppe. Die Gruppen wechseln sich wöchentlich mit der Schweißarbeit ab.</summary>
public enum Gruppe { A, B }

/// <summary>Wofür ein Revier eingeplant werden darf.</summary>
public enum Verwendung { Training, Pruefung, Beides }

public enum TerminStatus { Geplant, Veroeffentlicht, Durchgefuehrt }

public enum LaufErgebnis
{
    Normal,
    /// <summary>Abbruch. Die Fährte gilt als gelaufen.</summary>
    Abbruch,
    /// <summary>Nicht angetreten. Die Fährte gilt als nicht gelaufen.</summary>
    NichtAngetreten,
}

/// <summary>Zwei Fährten, die gleichzeitig gelaufen werden dürfen.</summary>
public sealed record Sequenzpaar(int Erste, int Zweite);

public sealed record Revier
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Kuerzel { get; init; }
    public int AnzahlFaehrten { get; init; } = 6;
    public Verwendung Verwendung { get; init; } = Verwendung.Training;
    public bool Aktiv { get; init; } = true;

    /// <summary>Harte Grenze: so viele Tage liegen mindestens zwischen zwei Nutzungen.</summary>
    public int MinRuhetage { get; init; } = 14;

    /// <summary>Weiches Ziel: so viele Tage Ruhe werden angestrebt.</summary>
    public int ZielRuhetage { get; init; } = 21;

    /// <summary>Parallel laufende Fährten. Standard: eine aus der ersten Hälfte mit einer aus der zweiten (1+4, 2+5, 3+6).</summary>
    public IReadOnlyList<Sequenzpaar>? Paare { get; init; }

    /// <summary>Fährtennummern, die derzeit nicht genutzt werden dürfen (z. B. nicht aufgefrischt).</summary>
    public IReadOnlySet<int> GesperrteFaehrten { get; init; } = new HashSet<int>();

    public IReadOnlyList<Sequenzpaar> EffektivePaare => Paare ?? Standardpaare(AnzahlFaehrten);

    public IEnumerable<int> NutzbareFaehrten =>
        Enumerable.Range(1, AnzahlFaehrten).Where(n => !GesperrteFaehrten.Contains(n));

    public bool FuerTrainingNutzbar => Aktiv && Verwendung != Verwendung.Pruefung;

    /// <summary>Kapazität eines Schweißblocks: jede Fährte einmal plus eine Fährte doppelt.</summary>
    public int Kapazitaet => NutzbareFaehrten.Count() + 1;

    public static IReadOnlyList<Sequenzpaar> Standardpaare(int anzahl)
    {
        var haelfte = anzahl / 2;
        return Enumerable.Range(1, haelfte).Select(i => new Sequenzpaar(i, i + haelfte)).ToList();
    }
}

/// <summary>Ein Gespann aus Hundeführer und Hund in einem Kurs.</summary>
public sealed record Gespann(int Id, string Fuehrer, string Hund, Gruppe Gruppe);

/// <summary>Geplante Fährte eines Gespanns an einem Termin.</summary>
public sealed record Zuteilung(int GespannId, int FaehrteNr, int Sequenz, bool ZweiterLaufAufFaehrte);

/// <summary>Tatsächlich gelaufene Fährte.</summary>
public sealed record Lauf(int GespannId, int FaehrteNr, LaufErgebnis Ergebnis = LaufErgebnis.Normal);

/// <summary>Ein Trainingssonntag mit dem Schweißblock einer Gruppe.</summary>
public sealed class Termin
{
    public required int Id { get; init; }
    public required DateOnly Datum { get; init; }
    public required Gruppe Gruppe { get; set; }
    public TerminStatus Status { get; set; } = TerminStatus.Geplant;

    public int? RevierId { get; set; }
    /// <summary>Revier wurde von Hand festgelegt und wird bei der Neuplanung nicht verändert.</summary>
    public bool RevierFixiert { get; set; }

    public HashSet<int> Helfer { get; set; } = [];
    /// <summary>Helfer wurden von Hand festgelegt (z. B. nach einem Tausch).</summary>
    public bool HelferFixiert { get; set; }

    public HashSet<int> Abgemeldet { get; set; } = [];
    /// <summary>Gespanne aus der anderen Gruppe, die hier nachholen.</summary>
    public HashSet<int> Gaeste { get; set; } = [];

    public List<Zuteilung> Zuteilungen { get; set; } = [];
    /// <summary>Zuteilung wurde von Hand festgelegt (z. B. nach einem Fährtentausch).</summary>
    public bool ZuteilungFixiert { get; set; }

    /// <summary>Tatsächliche Läufe, sobald der Termin durchgeführt ist.</summary>
    public List<Lauf> Ist { get; set; } = [];

    public Termin Kopie() => new()
    {
        Id = Id,
        Datum = Datum,
        Gruppe = Gruppe,
        Status = Status,
        RevierId = RevierId,
        RevierFixiert = RevierFixiert,
        Helfer = [.. Helfer],
        HelferFixiert = HelferFixiert,
        Abgemeldet = [.. Abgemeldet],
        Gaeste = [.. Gaeste],
        Zuteilungen = [.. Zuteilungen],
        ZuteilungFixiert = ZuteilungFixiert,
        Ist = [.. Ist],
    };
}

public sealed class Saisonplan
{
    public List<Termin> Termine { get; init; } = [];

    public IEnumerable<Termin> Chronologisch => Termine.OrderBy(t => t.Datum).ThenBy(t => t.Id);

    public Termin Termin(int id) => Termine.Single(t => t.Id == id);

    public Saisonplan Kopie() => new() { Termine = Termine.Select(t => t.Kopie()).ToList() };
}

public sealed class PlanungsOptionen
{
    /// <summary>Anzahl der Helfer je Schweißblock. Sie legen am Vorabend die Fährten und gehen beim Training als Beobachter mit.</summary>
    public int HelferProTermin { get; init; } = 2;

    /// <summary>Helfer kommen aus der Gruppe, die an diesem Tag nicht auf Schweiß ist (Standard).</summary>
    public bool HelferAusAndererGruppe { get; init; } = true;

    /// <summary>Nur relevant, wenn Helfer aus der eigenen Gruppe kommen: ob sie am selben Tag selbst eine Fährte laufen.</summary>
    public bool HelferLaufenMit { get; init; }

    public int KostenOhneFaehrte { get; init; } = 10_000;
    public int KostenPlanaenderung { get; init; } = 50;
    public int KostenDoppelt { get; init; } = 20;
    public int KostenDoppeltJeVorherigemMal { get; init; } = 10;
}
