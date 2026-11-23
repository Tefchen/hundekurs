using Microsoft.AspNetCore.Identity;

namespace Hundekurs.Daten;

/// <summary>Anmeldekonto für die Web-App (ASP.NET Core Identity).</summary>
public class Benutzerkonto : IdentityUser
{
    /// <summary>Name, der in Kopfzeile und Historie erscheint, z. B. „Franziska“.</summary>
    public string Anzeigename { get; set; } = "";

    /// <summary>Gesetzt bei neuen Konten und nach dem Zurücksetzen: Beim nächsten Login muss ein eigenes Passwort vergeben werden.</summary>
    public bool MussPasswortAendern { get; set; }
}
