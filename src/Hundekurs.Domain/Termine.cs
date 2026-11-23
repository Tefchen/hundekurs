namespace Hundekurs.Domain;

public enum Terminart { Training, Pruefung, Gehorsam, Apportierarbeit }

public enum Terminstatus { Geplant, Veroeffentlicht, Durchgefuehrt, Abgesagt }

/// <summary>Ein Trainings- oder Prüfungstag mit Schweißblock und Block für die übrigen Fächer.</summary>
public class Termin : Entitaet
{
    public int KursId { get; set; }
    public DateOnly Datum { get; set; }
    public Terminart Art { get; set; }
    public Terminstatus Status { get; set; }

    // Block 1: Schweiß
    public Trainingsgruppe? GruppeSchweiss { get; set; }
    public int? RevierId { get; set; }
    public Revier? Revier { get; set; }
    public TimeOnly? UhrzeitSchweiss { get; set; }
    public bool RevierFixiert { get; set; }
    public bool HelferFixiert { get; set; }
    public bool ZuteilungFixiert { get; set; }

    // Block 2: übrige Fächer bzw. Ort bei Gehorsam und Apportierarbeit
    public int? UebungsgelaendeId { get; set; }
    public TimeOnly? UhrzeitUebrigeFaecher { get; set; }

    public string? Notiz { get; set; }

    public Vorbereitungstermin? Vorbereitung { get; set; }
}

/// <summary>Legen der Fährten, in der Regel am Vorabend.</summary>
public class Vorbereitungstermin : Entitaet
{
    public int TerminId { get; set; }
    public DateOnly Datum { get; set; }
    public TimeOnly? Uhrzeit { get; set; }
    public int? RevierId { get; set; }
}

public enum Abweichungsart { Abgemeldet, Gast }

/// <summary>Abweichung von der Gruppeneinteilung an einem Termin: abgemeldet oder als Gast (Nachholen, Termintausch).</summary>
public class Terminabweichung : Entitaet
{
    public int TerminId { get; set; }
    public int KursteilnahmeId { get; set; }
    public Abweichungsart Art { get; set; }
}

public enum Helferstatus { Eingeteilt, Getauscht, Erledigt }

public class Helferdienst : Entitaet
{
    public int TerminId { get; set; }
    public int KursteilnahmeId { get; set; }
    public Helferstatus Status { get; set; }
}

public enum Einsatzart { Vorbereitung, Training, Pruefung, Faehrtenauffrischung, Gehorsam, Apportierarbeit }

/// <summary>Tagesgenauer Einsatz eines Ausbilders, Grundlage der km-Abrechnung (Hin- und Rückweg).</summary>
public class Einsatz : Entitaet
{
    public int PersonId { get; set; }
    public Person? Person { get; set; }
    public DateOnly Datum { get; set; }
    public Einsatzart Art { get; set; }
    public int? TerminId { get; set; }
    /// <summary>Ziel der Fahrt: Revier oder Übungsgelände.</summary>
    public int? RevierId { get; set; }
    public int? UebungsgelaendeId { get; set; }

    /// <summary>Einfache Strecke laut Routendienst zum Zeitpunkt der Erfassung.</summary>
    public double? KmBerechnet { get; set; }
    public DateTimeOffset? KmBerechnetAm { get; set; }
    /// <summary>Einfache Strecke von Hand (z. B. Umleitung). Hat Vorrang vor der Berechnung.</summary>
    public double? KmKorrigiert { get; set; }
    public string? Bemerkung { get; set; }
    /// <summary>Gesetzt, wenn der Abrechnungszeitraum abgeschlossen ist; danach keine Änderung mehr.</summary>
    public int? AbrechnungId { get; set; }
}

/// <summary>Zwischenspeicher der Strecke Wohnort → Ziel; wird spätestens nach 7 Tagen neu berechnet (Baustellen).</summary>
public class Standardstrecke : Entitaet
{
    public int PersonId { get; set; }
    public int? RevierId { get; set; }
    public int? UebungsgelaendeId { get; set; }
    public double Kilometer { get; set; }
    public DateTimeOffset BerechnetAm { get; set; }
}

/// <summary>Anwesenheit eines Gespanns bei Gehorsam oder Apportierarbeit.</summary>
public class Anwesenheit : Entitaet
{
    public int TerminId { get; set; }
    public int KursteilnahmeId { get; set; }
    public bool Anwesend { get; set; }
    public string? Notiz { get; set; }
}

public enum Verfuegbarkeitsstatus { Kann, KannNicht, Alternative, Ersatz }

public class Verfuegbarkeit : Entitaet
{
    public int TerminId { get; set; }
    public int? PersonId { get; set; }
    public int? KursteilnahmeId { get; set; }
    public Verfuegbarkeitsstatus Status { get; set; }
}
