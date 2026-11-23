using System.Security.Cryptography;
using Hundekurs.Daten;
using Microsoft.AspNetCore.Identity;

namespace Hundekurs.Web.Services;

public static class Startpasswort
{
    // Ohne leicht verwechselbare Zeichen (0/O, 1/l/I), damit es sich gut vorlesen und abtippen lässt.
    private const string Zeichen = "abcdefghjkmnpqrstuvwxyzACDEFGHJKLMNPQRTUVWXYZ23456789";

    /// <summary>Zufälliges Passwort der Form „xxxx-xxxx-xxxx“ (14 Zeichen).</summary>
    public static string Erzeugen() =>
        string.Join('-', Enumerable.Range(0, 3).Select(_ => RandomNumberGenerator.GetString(Zeichen, 4)));
}

/// <summary>Rollen anlegen und beim allerersten Start ein Verwaltungskonto einrichten.</summary>
public static class KontenEinrichtung
{
    public static async Task EinrichtenAsync(IServiceProvider dienste, IConfiguration konfiguration, IHostEnvironment umgebung, bool demodaten, ILogger protokoll)
    {
        await using var bereich = dienste.CreateAsyncScope();
        var rollen = bereich.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var konten = bereich.ServiceProvider.GetRequiredService<UserManager<Benutzerkonto>>();

        foreach (var rolle in Rollen.Alle)
            if (!await rollen.RoleExistsAsync(rolle))
                Pruefen(await rollen.CreateAsync(new IdentityRole(rolle)), $"Rolle {rolle}");

        if (konten.Users.Any()) return;

        // Server: Erstkonto__Benutzername und Erstkonto__Passwort in app.env setzen, nach dem ersten Login wieder entfernen.
        var name = konfiguration["Erstkonto:Benutzername"];
        var passwort = konfiguration["Erstkonto:Passwort"];
        var mussAendern = true;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(passwort))
        {
            if (!(demodaten && umgebung.IsDevelopment()))
            {
                protokoll.LogWarning("Es gibt noch kein Konto. Erstkonto:Benutzername und Erstkonto:Passwort setzen und neu starten.");
                return;
            }
            // Nur lokal mit Demodaten: festes Konto, damit man sofort loslegen kann.
            name = "admin";
            passwort = "demo-passwort";
            mussAendern = false;
        }

        var konto = new Benutzerkonto { UserName = name.Trim(), Anzeigename = name.Trim(), MussPasswortAendern = mussAendern };
        Pruefen(await konten.CreateAsync(konto, passwort), $"Erstkonto {name}");
        Pruefen(await konten.AddToRoleAsync(konto, Rollen.Verwaltung), $"Rolle für {name}");
        protokoll.LogWarning("Erstes Konto „{Name}“ mit Rolle Verwaltung angelegt{Hinweis}.", konto.UserName,
            mussAendern ? "; das Passwort muss beim ersten Login geändert werden" : " (Demodaten, Passwort „demo-passwort“)");
    }

    private static void Pruefen(IdentityResult ergebnis, string was)
    {
        if (!ergebnis.Succeeded)
            throw new InvalidOperationException($"{was}: {string.Join(" ", ergebnis.Errors.Select(e => e.Description))}");
    }
}
