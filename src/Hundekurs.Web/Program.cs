using System.Globalization;
using Hundekurs.Daten;
using Hundekurs.Planung.Strecken;
using Hundekurs.Web.Components;
using Hundekurs.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var deutsch = CultureInfo.GetCultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = deutsch;
CultureInfo.DefaultThreadCurrentUICulture = deutsch;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Datenbank: SQLite-Datei außerhalb des Projektordners. Demodaten und echte Daten liegen in getrennten Dateien.
var demodaten = builder.Configuration.GetValue<bool>("Datenbank:Demodaten");
var pfad = builder.Configuration["Datenbank:Pfad"] is { Length: > 0 } eingestellt
    ? Path.GetFullPath(Environment.ExpandEnvironmentVariables(eingestellt))
    : Datenbank.StandardPfad(demodaten);
Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);

// Schlüssel für Anmelde-Cookies und Formulare neben der Datenbank ablegen, damit Anmeldungen einen Neustart überstehen.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetDirectoryName(pfad)!, "Schluessel")))
    .SetApplicationName("Hundekurs");

builder.Services.AddDbContextFactory<HundekursDb>(o => o.UseSqlite(Datenbank.Verbindung(pfad)));
// Identity braucht den Kontext zusätzlich als Scoped-Dienst (eine Instanz je Anfrage bzw. je Vorgang).
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<HundekursDb>>().CreateDbContext());

// ---------- Anmeldung (ASP.NET Core Identity mit eigenen Seiten) ----------
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, KontoPruefenderAuthenticationStateProvider>();
builder.Services.AddScoped<CircuitHandler, BenutzerkontextCircuitHandler>();

builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = IdentityConstants.ApplicationScheme;
        o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddIdentityCore<Benutzerkonto>(o =>
    {
        // Lieber lang als kompliziert: mindestens 10 Zeichen, keine Pflicht zu Sonderzeichen.
        o.Password.RequiredLength = 10;
        o.Password.RequiredUniqueChars = 4;
        o.Password.RequireDigit = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;
        o.User.RequireUniqueEmail = false;
        o.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddClaimsPrincipalFactory<KontoClaimsFabrik>()
    .AddErrorDescriber<DeutscheIdentityFehler>()
    .AddEntityFrameworkStores<HundekursDb>()
    .AddSignInManager();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/anmelden";
    o.LogoutPath = "/abmelden";
    o.AccessDeniedPath = "/kein-zugriff";
    o.ExpireTimeSpan = TimeSpan.FromHours(12);
    o.SlidingExpiration = true;
    o.Cookie.Name = "Hundekurs.Anmeldung";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
// Gesperrte Konten und zurückgesetzte Passwörter wirken nach spätestens 5 Minuten.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.AddAuthorizationBuilder()
    // Alles, was nicht ausdrücklich freigegeben ist, verlangt eine Anmeldung.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Richtlinien.Lesen, p => p.RequireRole(Rollen.Lesen, Rollen.Bearbeiten, Rollen.Verwaltung))
    .AddPolicy(Richtlinien.Bearbeiten, p => p.RequireRole(Rollen.Bearbeiten, Rollen.Verwaltung))
    .AddPolicy(Richtlinien.Verwaltung, p => p.RequireRole(Rollen.Verwaltung));
builder.Services.AddSingleton<Kursspeicher>();
builder.Services.AddSingleton(new DatenbankInfo(pfad, demodaten));

// Routendienst für die km-Berechnung. Schlüssel per „dotnet user-secrets set Routendienst:Schluessel …“, nie in appsettings.json.
builder.Services.AddHttpClient("routendienst", c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddSingleton<IRoutendienst>(sp =>
{
    var schluessel = builder.Configuration["Routendienst:Schluessel"];
    if (string.IsNullOrWhiteSpace(schluessel)) return new KeinRoutendienst();
    var basis = builder.Configuration["Routendienst:Basis"] is { Length: > 0 } b ? new Uri(b) : null;
    return new OpenRouteService(sp.GetRequiredService<IHttpClientFactory>().CreateClient("routendienst"), schluessel, basis);
});
builder.Services.AddSingleton<PlanungsZustand>();

var app = builder.Build();

// Beim Start: Sicherung (höchstens einmal täglich), Schema anlegen bzw. aktualisieren, Kurs laden
var sicherung = Datenbank.Sichern(pfad);
string? neuAngelegt;
using (var db = app.Services.GetRequiredService<IDbContextFactory<HundekursDb>>().CreateDbContext())
    neuAngelegt = Datenbank.Vorbereiten(db, demodaten);
if (neuAngelegt is not null) app.Logger.LogWarning("{Hinweis}", neuAngelegt);
app.Services.GetRequiredService<PlanungsZustand>();
app.Logger.LogInformation("Datenbank: {Pfad}{Demo}", pfad, demodaten ? " (Demodaten)" : "");
app.Logger.LogInformation("Routendienst: {Status}", app.Services.GetRequiredService<IRoutendienst>().Verfuegbar ? "eingerichtet" : "nicht eingerichtet (km nur von Hand)");
if (sicherung is not null) app.Logger.LogInformation("Sicherung angelegt: {Sicherung}", sicherung);
await KontenEinrichtung.EinrichtenAsync(app.Services, app.Configuration, app.Environment, demodaten, app.Logger);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();

// Angemeldeten Benutzer für Stempel, Historie und Schreibsperre bereitstellen (in Blazor-Verbindungen übernimmt das der CircuitHandler).
app.Use(async (kontext, weiter) =>
{
    Benutzerkontext.Aktuell = Ansprueche.Sitzung(kontext.User);
    await weiter(kontext);
});

// Mit Startpasswort angemeldet: Bis ein eigenes Passwort vergeben ist, führt jede Seite zu „Passwort ändern“.
app.Use(async (kontext, weiter) =>
{
    var frei = kontext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null
        || kontext.Request.Path.StartsWithSegments("/passwort-aendern")
        || kontext.Request.Path.StartsWithSegments("/abmelden");
    if (!frei && kontext.User.HasClaim(c => c.Type == Ansprueche.PasswortAendern))
    {
        kontext.Response.Redirect("/passwort-aendern");
        return;
    }
    await weiter(kontext);
});

app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();

app.MapPost("/abmelden", async ([FromForm] string? ziel, SignInManager<Benutzerkonto> anmeldung) =>
{
    await anmeldung.SignOutAsync();
    return TypedResults.LocalRedirect("~/anmelden");
});

// Mailentwurf für die Wochenmail (Empfänger und Texte aus der Mail-Seite)
app.MapGet("/mail/entwurf/{terminId:int}", (int terminId, PlanungsZustand zustand) =>
{
    if (zustand.Plan.Termine.All(t => t.Id != terminId)) return Results.NotFound();
    var termin = zustand.Plan.Termin(terminId);
    var inhalt = MailEntwurf.Erzeugen(zustand.Wochenmail(terminId), zustand.AusgewaehlteEmpfaenger);
    return Results.File(inhalt, "message/rfc822", $"Schweisstraining-{termin.Datum:yyyy-MM-dd}.eml");
}).RequireAuthorization(Richtlinien.Bearbeiten);
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
