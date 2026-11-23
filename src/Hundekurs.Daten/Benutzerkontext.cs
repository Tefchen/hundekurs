namespace Hundekurs.Daten;

/// <summary>Wer gerade arbeitet und ob er Kursdaten ändern darf.</summary>
public sealed record Sitzungsbenutzer(string Name, bool DarfBearbeiten);

/// <summary>
/// Angemeldeter Benutzer der laufenden Anfrage bzw. der laufenden Blazor-Aktion.
/// Die Web-App setzt ihn pro HTTP-Anfrage und pro Ereignis im Blazor-Circuit.
/// Ohne Wert (Programmstart, Tests) gilt der Windows- bzw. Linux-Benutzer und es gibt keine Sperre.
/// </summary>
public static class Benutzerkontext
{
    private static readonly AsyncLocal<Sitzungsbenutzer?> Wert = new();

    public static Sitzungsbenutzer? Aktuell
    {
        get => Wert.Value;
        set => Wert.Value = value;
    }
}
