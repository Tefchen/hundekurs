using System.Globalization;
using Hundekurs.Daten;
using Hundekurs.Planung.Strecken;
using Hundekurs.Web.Components;
using Hundekurs.Web.Services;
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

builder.Services.AddDbContextFactory<HundekursDb>(o => o.UseSqlite(Datenbank.Verbindung(pfad)));
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

// Mailentwurf für die Wochenmail (Empfänger und Texte aus der Mail-Seite)
app.MapGet("/mail/entwurf/{terminId:int}", (int terminId, PlanungsZustand zustand) =>
{
    if (zustand.Plan.Termine.All(t => t.Id != terminId)) return Results.NotFound();
    var termin = zustand.Plan.Termin(terminId);
    var inhalt = MailEntwurf.Erzeugen(zustand.Wochenmail(terminId), zustand.AusgewaehlteEmpfaenger);
    return Results.File(inhalt, "message/rfc822", $"Schweisstraining-{termin.Datum:yyyy-MM-dd}.eml");
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
