namespace Hundekurs.Domain;

/// <summary>Grundbewertung aus dem bisherigen Katalog: 0 unauffällig, 1 auffällig, + sehr gut, -- fehlt, – entschuldigt, 2 nicht geprüft.</summary>
public enum Bewertung { Unauffaellig, Auffaellig, SehrGut, Fehlt, Entschuldigt, NichtGeprueft }

/// <summary>Merkmal je Disziplin, z. B. Standruhe „A – Aufstehen“, Schussfestigkeit „C – Schussscheu“.</summary>
public class Merkmal : Entitaet
{
    public Disziplin Disziplin { get; set; }
    public required string Code { get; set; }
    public required string Bezeichnung { get; set; }
}

public class Beobachtung : Entitaet
{
    public int TerminId { get; set; }
    public int KursteilnahmeId { get; set; }
    public Disziplin Disziplin { get; set; }
    public Bewertung Bewertung { get; set; }
    public List<string> MerkmalCodes { get; set; } = [];
    public string? Notiz { get; set; }
    public int? ErfasstVonId { get; set; }
}

/// <summary>
/// Ein Prüfungstag (Brauchbarkeitsprüfung). Datum, Uhrzeit (Standard 8:00) und Revier stehen am <see cref="Termin"/> (Art Prüfung),
/// das Legen der Fährten am Vortag an dessen <see cref="Vorbereitungstermin"/>.
/// Ausbilder, die am Prüfungstag helfen, werden als <see cref="Einsatz"/> (Art Prüfung) erfasst.
/// </summary>
public class Pruefungstag : Entitaet
{
    public int TerminId { get; set; }
    public Termin? Termin { get; set; }

    /// <summary>Höchstzahl Hunde an diesem Tag.</summary>
    public int MaxHunde { get; set; } = 6;
    /// <summary>Bis zu diesem Tag haben Gespanne aus dem Kurs Vorrang vor kursfremden.</summary>
    public DateOnly? Meldeschluss { get; set; }

    // Drei Verbandsprüfer, einer davon Richterobmann
    public int? RichterobmannId { get; set; }
    public int? Richter2Id { get; set; }
    public int? Richter3Id { get; set; }
    /// <summary>Prüfer, die am Vortag beim Legen der Fährten dabei sind und sie abnehmen (mindestens einer).</summary>
    public List<int> PrueferAmVortag { get; set; } = [];

    public int? HelferStandruheId { get; set; }
    public string? Treiber { get; set; }
    /// <summary>Weitere Helfer ohne eigene Erfassung (z. B. Treiber), zählen für die Verpflegung.</summary>
    public int WeitereHelfer { get; set; }
    public string? Lokal { get; set; }
    public string? Bemerkung { get; set; }
}

/// <summary>
/// Nennung eines Gespanns zu einem Prüfungstag. Hund und Führer stehen direkt daran, damit auch kursfremde Gespanne
/// genannt werden können; bei Gespannen aus dem Kurs ist zusätzlich die Kursteilnahme gesetzt.
/// Alle genannten Fächer werden am selben Tag geprüft.
/// </summary>
public class Nennung : Entitaet
{
    public int PruefungstagId { get; set; }
    public int HundId { get; set; }
    public Hund? Hund { get; set; }
    public int FuehrerId { get; set; }
    public Person? Fuehrer { get; set; }
    public int? KursteilnahmeId { get; set; }

    /// <summary>Eingang der Nennung; bestimmt die Reihenfolge bei der Platzvergabe.</summary>
    public DateTimeOffset GenanntAm { get; set; }

    // Fächer (Grundgehorsam ist Pflichtfach, außer bei bereits bestandener Brauchbarkeit mit reiner Niederwild-Nennung)
    public bool Gehorsam { get; set; } = true;
    public bool NachsucheSchalenwild { get; set; }
    public bool NachsucheNiederwild { get; set; }

    /// <summary>Begleitpersonen, die mitessen.</summary>
    public int Gaeste { get; set; }
    /// <summary>Nenngeld (fester Betrag des Kurses zum Zeitpunkt der Nennung).</summary>
    public decimal Nenngeld { get; set; }
    public DateOnly? NenngeldBezahltAm { get; set; }
    public int? Losnummer { get; set; }
    public string? Bemerkung { get; set; }
}

public class Pruefungsergebnis : Entitaet
{
    public int NennungId { get; set; }
    public Disziplin Fach { get; set; }
    public string? Ergebnis { get; set; }
    public string? Bemerkung { get; set; }
}

public enum Auslagenart { Verpflegung, Anschaffung, Sonstiges }

/// <summary>
/// Auslage im Kurs, z. B. Essen und Getränke zur Prüfung oder Westen für die Ausbilder.
/// Ohne Person wurde direkt aus der Vereinskasse bezahlt.
/// </summary>
public class Auslage : Entitaet
{
    public int KursId { get; set; }
    public DateOnly Datum { get; set; }
    public Auslagenart Art { get; set; }
    public required string Bezeichnung { get; set; }
    public decimal Betrag { get; set; }
    /// <summary>Wer ausgelegt hat und die Erstattung bekommt.</summary>
    public int? PersonId { get; set; }
    /// <summary>Anlass, z. B. „Prüfung September“.</summary>
    public string? Anlass { get; set; }
    public bool BelegLiegtVor { get; set; }
    public DateOnly? ErstattetAm { get; set; }
    public string? Bemerkung { get; set; }
}

/// <summary>Abgeschlossener Abrechnungszeitraum für Einsätze; danach sind die Einsätze gesperrt.</summary>
public class Einsatzabrechnung : Entitaet
{
    public int KursId { get; set; }
    public DateOnly Von { get; set; }
    public DateOnly Bis { get; set; }
    public decimal KmSatz { get; set; }
    public DateTimeOffset? AbgeschlossenAm { get; set; }
}

/// <summary>Eintrag der Historie: wer hat wann was geändert.</summary>
public class Aenderungsprotokoll
{
    public long Id { get; set; }
    public DateTimeOffset Zeitpunkt { get; set; }
    public required string Benutzer { get; set; }
    public required string Entitaet { get; set; }
    public int EntitaetId { get; set; }
    public required string Aktion { get; set; }
    public string? WerteVorher { get; set; }
    public string? WerteNachher { get; set; }
}
