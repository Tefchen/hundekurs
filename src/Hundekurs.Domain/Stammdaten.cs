namespace Hundekurs.Domain;

/// <summary>Basisklasse mit Schlüssel und Änderungsinformationen für die Historie.</summary>
public abstract class Entitaet
{
    public int Id { get; set; }
    public DateTimeOffset ErstelltAm { get; set; }
    public string? ErstelltVon { get; set; }
    public DateTimeOffset? GeaendertAm { get; set; }
    public string? GeaendertVon { get; set; }
}

/// <summary>Anrede (m/w/d). 0 = keine Angabe, damit bestehende Einträge nicht plötzlich „Herr“ heißen.</summary>
public enum Anrede { KeineAngabe = 0, Herr = 1, Frau = 2, Divers = 3 }

public enum Geschlecht { Ruede, Huendin }

/// <summary>Eine Person. Welche Rolle sie im Kurs hat, steht in <see cref="Kursrolle"/>.</summary>
public class Person : Entitaet
{
    public Anrede Anrede { get; set; }
    /// <summary>Akademischer Titel, z. B. „Dr.“.</summary>
    public string? Titel { get; set; }
    public required string Nachname { get; set; }
    public required string Vorname { get; set; }
    public string? Strasse { get; set; }
    public string? Plz { get; set; }
    public string? Ort { get; set; }
    public string? Telefon { get; set; }
    public string? EMail { get; set; }
    public bool Vereinsmitglied { get; set; }

    /// <summary>Koordinaten der Wohnadresse (für die km-Berechnung). Nur Kursleitung und Kasse sehen die Adresse.</summary>
    public double? Breitengrad { get; set; }
    public double? Laengengrad { get; set; }
    /// <summary>Adresse, aus der die Koordinaten ermittelt wurden. Ändert sich die Adresse, wird neu gesucht.</summary>
    public string? GeokodierteAdresse { get; set; }

    /// <summary>Nur bei Richtern.</summary>
    public string? Richternummer { get; set; }

    /// <summary>Gesetzt, sobald die Person nach Ablauf der Löschfrist anonymisiert wurde.</summary>
    public DateTimeOffset? AnonymisiertAm { get; set; }

    public string Anzeigename => $"{Nachname}, {Vorname}";

    /// <summary>Vorname und abgekürzter Nachname, z. B. „Maximilian M.“ – für Konten, die nur lesen dürfen.</summary>
    public string Kurzname => Kuerzen(Vorname, Nachname);

    public static string Kuerzen(string? vorname, string? nachname)
    {
        var v = vorname?.Trim() ?? "";
        // Bei Namenszusätzen zählt das letzte Wort: „von der Heide“ → „H.“
        var n = (nachname ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        if (n.Length == 0) return v;
        return v.Length == 0 ? $"{char.ToUpperInvariant(n[0])}." : $"{v} {char.ToUpperInvariant(n[0])}.";
    }

    public string? Adresse =>
        string.IsNullOrWhiteSpace(Strasse) && string.IsNullOrWhiteSpace(Ort) ? null : $"{Strasse}, {Plz} {Ort}".Trim(' ', ',');
}

public class Rasse : Entitaet
{
    public required string Kuerzel { get; set; }
    public required string Bezeichnung { get; set; }
}

public class Hund : Entitaet
{
    public required string Rufname { get; set; }
    public string? Zuchtbuchname { get; set; }
    public int? RasseId { get; set; }
    public Rasse? Rasse { get; set; }
    public DateOnly? Wurfdatum { get; set; }
    public Geschlecht? Geschlecht { get; set; }
    public string? ChipNummer { get; set; }
    public string? Zuchtbuchnummer { get; set; }
    public string? HeimtierausweisNummer { get; set; }
    public string? Bemerkung { get; set; }

    /// <summary>Wer den Hund üblicherweise führt (im Kurs steht der Führer an der Kursteilnahme).</summary>
    public int? FuehrerId { get; set; }
    public Person? Fuehrer { get; set; }

    public int? EigentuemerId { get; set; }
    public Person? Eigentuemer { get; set; }

    public List<Brauchbarkeit> Brauchbarkeiten { get; set; } = [];
}

/// <summary>Bestandene Brauchbarkeitsprüfung eines Hundes. Befreit bei reiner Niederwildausbildung von Teil 1.</summary>
public class Brauchbarkeit : Entitaet
{
    public int HundId { get; set; }
    public DateOnly Datum { get; set; }
    public string? Ort { get; set; }
    /// <summary>Bestandene Fächer (Ids aus <see cref="Fach"/>).</summary>
    public List<int> FachIds { get; set; } = [];
    public string? Bemerkung { get; set; }
}

public enum RevierVerwendung { Training, Pruefung, Beides }

public class Revier : Entitaet
{
    public required string Name { get; set; }
    public required string Kuerzel { get; set; }
    public string? Treffpunkt { get; set; }
    public string? Hinweise { get; set; }
    public RevierVerwendung Verwendung { get; set; } = RevierVerwendung.Training;
    public bool Aktiv { get; set; } = true;
    public int MinRuhetage { get; set; } = 14;
    public int ZielRuhetage { get; set; } = 21;
    /// <summary>Koordinaten des Treffpunkts (Ziel für die km-Berechnung).</summary>
    public double? Breitengrad { get; set; }
    public double? Laengengrad { get; set; }

    public List<Faehrte> Faehrten { get; set; } = [];
    public List<Sequenzpaar> Sequenzpaare { get; set; } = [];
    public List<Uebungsgelaende> Uebungsgelaende { get; set; } = [];
}

public class Faehrte : Entitaet
{
    public int RevierId { get; set; }
    public Revier? Revier { get; set; }
    public int Nummer { get; set; }
    public bool Aktiv { get; set; } = true;
    public string? Bemerkung { get; set; }
}

/// <summary>Zwei Fährten eines Reviers, die gleichzeitig gelaufen werden dürfen.</summary>
public class Sequenzpaar : Entitaet
{
    public int RevierId { get; set; }
    public int ErsteFaehrte { get; set; }
    public int ZweiteFaehrte { get; set; }
}

public enum Disziplin
{
    Standruhe,
    Schussfestigkeit,
    FreieSuche,
    Leinenfuehrigkeit,
    Gehorsam,
    Haarwildschleppe,
    Federwildschleppe,
    NachsucheSchalenwild,
}

public enum Fachbereich { Grundgehorsam, Nachsuche, Sonstiges }

/// <summary>Ausbildungs- bzw. Prüfungsfach, z. B. Leinenführigkeit oder Nachsuche auf Niederwild. Die Liste ist pflegbar.</summary>
public class Fach : Entitaet
{
    public required string Bezeichnung { get; set; }
    public Fachbereich Bereich { get; set; }
    public int Reihenfolge { get; set; }
    public bool Aktiv { get; set; } = true;
}

/// <summary>
/// Übungsort außerhalb der Fährten: Gehorsamsplatz, Gelände für Niederwild, Schussfestigkeit usw.
/// Ein Ort kann in einem Revier liegen und für mehrere Fächer genutzt werden.
/// </summary>
public class Uebungsgelaende : Entitaet
{
    /// <summary>Revier, in dem der Ort liegt (optional, der Gehorsamsplatz liegt meist außerhalb).</summary>
    public int? RevierId { get; set; }
    public required string Bezeichnung { get; set; }
    public string? Treffpunkt { get; set; }
    public double? Breitengrad { get; set; }
    public double? Laengengrad { get; set; }
    public bool Aktiv { get; set; } = true;
    /// <summary>Fächer, für die der Ort genutzt wird.</summary>
    public List<GelaendeFach> Faecher { get; set; } = [];
}

/// <summary>Zuordnung Ort ↔ Fach (ein Ort kann für mehrere Fächer genutzt werden).</summary>
public class GelaendeFach
{
    public int Id { get; set; }
    public int UebungsgelaendeId { get; set; }
    public int FachId { get; set; }
}

/// <summary>Jährliche Auffrischung und Neumarkierung der Fährten eines Reviers.</summary>
public class Faehrtenauffrischung : Entitaet
{
    public int RevierId { get; set; }
    public Revier? Revier { get; set; }
    public DateOnly Datum { get; set; }
    /// <summary>Leer bedeutet: alle Fährten des Reviers.</summary>
    public List<int> Faehrten { get; set; } = [];
    public List<int> BeteiligtePersonIds { get; set; } = [];
    public string? Bemerkung { get; set; }
}
