namespace Hundekurs.Domain;

/// <summary>Geplante Fährte (Soll).</summary>
public class Faehrtenzuteilung : Entitaet
{
    public int TerminId { get; set; }
    public int KursteilnahmeId { get; set; }
    public int FaehrteNummer { get; set; }
    public int Sequenz { get; set; }
    public bool ZweiterLaufAufFaehrte { get; set; }
}

public enum Laufergebnis { Normal, Abbruch, NichtAngetreten }

/// <summary>Tatsächlich gelaufene Fährte (Ist). Grundlage der Historie „schon gelaufen auf …“.</summary>
public class Faehrtenlauf : Entitaet
{
    public int TerminId { get; set; }
    public int KursteilnahmeId { get; set; }
    public int FaehrteNummer { get; set; }
    public Laufergebnis Ergebnis { get; set; }
    public string? Notiz { get; set; }
}

public enum Tauschart { Faehrtentausch, Helfertausch, Helfervertretung, Abmeldung, Nachholen }

/// <summary>Protokoll eines Tauschs samt Auswirkungen auf den Plan.</summary>
public class Tauschvorgang : Entitaet
{
    public Tauschart Art { get; set; }
    public int TerminId { get; set; }
    public int? ZweiterTerminId { get; set; }
    public List<int> BeteiligteTeilnahmeIds { get; set; } = [];
    /// <summary>Lesbare Vorher/Nachher-Liste, wie sie bei der Bestätigung angezeigt wurde.</summary>
    public List<string> Auswirkungen { get; set; } = [];
}
