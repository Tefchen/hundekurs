namespace Hundekurs.Domain;

public class Kurs : Entitaet
{
    public required string Bezeichnung { get; set; }
    public DateOnly Beginn { get; set; }
    public DateOnly Ende { get; set; }
    public int MaxTrainingsplaetzeSchweiss { get; set; } = 14;
    public int HelferProTermin { get; set; } = 2;
    /// <summary>Helfer kommen aus der Gruppe, die an dem Tag nicht auf Schweiß ist.</summary>
    public bool HelferAusAndererGruppe { get; set; } = true;
    public decimal KmSatz { get; set; } = 0.30m;
    /// <summary>Standardort für Teil 1 (Gehorsam).</summary>
    public int? GehorsamGelaendeId { get; set; }
    /// <summary>Standardort für die Apportierarbeit.</summary>
    public int? ApportierGelaendeId { get; set; }
    /// <summary>Kursjahr, nach dem die Termine erzeugt wurden.</summary>
    public int? Jahr { get; set; }

    // Wochenmail
    public string? MailEinleitung { get; set; }
    public string? MailGruss { get; set; }
    /// <summary>E-Mail-Adressen, die bei der Wochenmail abgewählt sind.</summary>
    public List<string> MailAbgewaehlt { get; set; } = [];

    public List<Kursteilnahme> Teilnahmen { get; set; } = [];
    public List<Kursrolle> Rollen { get; set; } = [];
}

public enum Rolle { Kursleitung, Kasse, Ausbilder, Pruefungsleitung, Richter, Teilnehmer }

/// <summary>Rolle einer Person in einem Kurs (steuert auch die Rechte in der App).</summary>
public class Kursrolle : Entitaet
{
    public int KursId { get; set; }
    public int PersonId { get; set; }
    public Person? Person { get; set; }
    public Rolle Rolle { get; set; }
}

[Flags]
public enum Faecher { Keine = 0, Schweiss = 1, Niederwild = 2, Beide = Schweiss | Niederwild }

public enum Trainingsgruppe { A, B }

/// <summary>Ein Gespann (Führer + Hund) in einem Kurs.</summary>
public class Kursteilnahme : Entitaet
{
    public int KursId { get; set; }
    public Kurs? Kurs { get; set; }
    public int HundId { get; set; }
    public Hund? Hund { get; set; }
    public int FuehrerId { get; set; }
    public Person? Fuehrer { get; set; }

    // Ausbildung (Spaltennamen aus der ersten Fassung, siehe HundekursDb)
    /// <summary>Teil 1: Grundgehorsam.</summary>
    public bool Gehorsam { get; set; } = true;
    /// <summary>Nachsuche auf Schalenwild (Schweiß, Teil 2 mit Schweißgruppe).</summary>
    public bool NachsucheSchalenwild { get; set; }
    /// <summary>Nachsuche auf Niederwild (Apportierarbeit).</summary>
    public bool NachsucheNiederwild { get; set; }

    /// <summary>Zulassung zu Teil 2 trotz zu geringer Anwesenheit in Teil 1 (Entscheidung der Kursleitung).</summary>
    public bool ZulassungAusnahme { get; set; }
    public string? ZulassungBegruendung { get; set; }

    // Gebühren
    /// <summary>Wiederholer zahlen die Hälfte.</summary>
    public bool Wiederholer { get; set; }
    /// <summary>Von Hand festgelegte Gebühr (Sonderfall); hat Vorrang vor der Berechnung.</summary>
    public decimal? GebuehrVonHand { get; set; }
    public string? GebuehrHinweis { get; set; }
    /// <summary>Gesetzt, sobald der Schatzmeister den Zahlungseingang meldet.</summary>
    public DateOnly? BezahltAm { get; set; }

    public Trainingsgruppe? Gruppe { get; set; }
    public string? Pruefungsziel { get; set; }
    public string? Bemerkung { get; set; }

    public List<Nachweis> Nachweise { get; set; } = [];
}

/// <summary>Preis einer Kombination der Ausbildungsteile in einem Kurs.</summary>
public class Gebuehrenpaket : Entitaet
{
    public int KursId { get; set; }
    public required string Bezeichnung { get; set; }
    public bool Gehorsam { get; set; }
    public bool NachsucheSchalenwild { get; set; }
    public bool NachsucheNiederwild { get; set; }
    public decimal Preis { get; set; }
    public decimal Mitgliederpreis { get; set; }
    public int Reihenfolge { get; set; }
}

public enum Nachweisart { Anmeldung, Impfpass, Versicherung, Ahnentafel, Lautnachweis, Jagdschein }

public enum Nachweisstatus { Offen, Vorhanden, Fehlt, NichtErforderlich }

public class Nachweis : Entitaet
{
    public int KursteilnahmeId { get; set; }
    public Nachweisart Art { get; set; }
    public Nachweisstatus Status { get; set; }
    public DateOnly? GeprueftAm { get; set; }
    public int? GeprueftVonId { get; set; }
    /// <summary>Optionale Datei; wird nach Prüfung bzw. spätestens zum Kursende gelöscht.</summary>
    public string? Dateipfad { get; set; }
}
