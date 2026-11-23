using System.Globalization;
using System.Security.Claims;
using Hundekurs.Daten;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Hundekurs.Web.Services;

/// <summary>Schreibt Anzeigename und „muss Passwort ändern“ in das Anmelde-Cookie.</summary>
public sealed class KontoClaimsFabrik(
    UserManager<Benutzerkonto> konten,
    RoleManager<IdentityRole> rollen,
    IOptions<IdentityOptions> optionen)
    : UserClaimsPrincipalFactory<Benutzerkonto, IdentityRole>(konten, rollen, optionen)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Benutzerkonto konto)
    {
        var identitaet = await base.GenerateClaimsAsync(konto);
        identitaet.AddClaim(new Claim(Ansprueche.Anzeigename, konto.Anzeigename));
        if (konto.MussPasswortAendern) identitaet.AddClaim(new Claim(Ansprueche.PasswortAendern, "1"));
        if (konto.PersonId is { } personId) identitaet.AddClaim(new Claim(Ansprueche.Person, personId.ToString(CultureInfo.InvariantCulture)));
        return identitaet;
    }
}

/// <summary>
/// Prüft offene Blazor-Verbindungen alle 5 Minuten gegen die Datenbank.
/// Gesperrte Konten, zurückgesetzte Passwörter und geänderte Rollen wirken dadurch auch in bereits geöffneten Fenstern.
/// </summary>
public sealed class KontoPruefenderAuthenticationStateProvider(
    ILoggerFactory protokoll,
    IServiceScopeFactory bereiche,
    IOptions<IdentityOptions> optionen)
    : RevalidatingServerAuthenticationStateProvider(protokoll)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(5);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState zustand, CancellationToken abbruch)
    {
        await using var bereich = bereiche.CreateAsyncScope();
        var konten = bereich.ServiceProvider.GetRequiredService<UserManager<Benutzerkonto>>();
        var konto = await konten.GetUserAsync(zustand.User);
        if (konto is null || await konten.IsLockedOutAsync(konto)) return false;

        var imCookie = zustand.User.FindFirstValue(optionen.Value.ClaimsIdentity.SecurityStampClaimType);
        return imCookie == await konten.GetSecurityStampAsync(konto);
    }
}

/// <summary>
/// Setzt für jedes Ereignis im Blazor-Circuit (Klick, Eingabe, …) den angemeldeten Benutzer in den <see cref="Benutzerkontext"/>.
/// Die Singleton-Dienste (PlanungsZustand, Kursspeicher) stempeln und prüfen damit, ohne den Benutzer selbst zu kennen.
/// </summary>
public sealed class BenutzerkontextCircuitHandler(AuthenticationStateProvider anmeldung) : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> weiter) =>
        async kontext =>
        {
            var zustand = await anmeldung.GetAuthenticationStateAsync();
            Benutzerkontext.Aktuell = Ansprueche.Sitzung(zustand.User);
            await weiter(kontext);
        };
}

/// <summary>Deutsche Fehlermeldungen für die Fälle, die bei Anmeldung und Kontenpflege vorkommen.</summary>
public sealed class DeutscheIdentityFehler : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => new() { Code = nameof(DefaultError), Description = "Unbekannter Fehler." };
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "Das bisherige Passwort stimmt nicht." };
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"Das Passwort muss mindestens {length} Zeichen lang sein." };
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Das Passwort muss mindestens {uniqueChars} verschiedene Zeichen enthalten." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = $"Den Benutzernamen „{userName}“ gibt es schon." };
    public override IdentityError InvalidUserName(string? userName) => new() { Code = nameof(InvalidUserName), Description = $"„{userName}“ ist kein gültiger Benutzername. Erlaubt sind Buchstaben ohne Umlaute, Ziffern sowie - . _" };
    public override IdentityError UserAlreadyInRole(string role) => new() { Code = nameof(UserAlreadyInRole), Description = $"Das Konto hat die Rolle „{role}“ bereits." };
    public override IdentityError ConcurrencyFailure() => new() { Code = nameof(ConcurrencyFailure), Description = "Das Konto wurde inzwischen von jemand anderem geändert. Bitte neu laden." };
}
