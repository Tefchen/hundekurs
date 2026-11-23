using System.Globalization;
using System.Security.Claims;
using Hundekurs.Daten;
using Hundekurs.Planung;

namespace Hundekurs.Web.Services;

/// <summary>Die drei Rollen. Jedes Konto hat genau eine; jede schließt die Rechte der vorigen ein.</summary>
public static class Rollen
{
    /// <summary>Sieht Übersicht, Schweißplan und Termine, ändert nichts.</summary>
    public const string Lesen = "Lesen";

    /// <summary>Pflegt alle Kursdaten.</summary>
    public const string Bearbeiten = "Bearbeiten";

    /// <summary>Wie Bearbeiten, verwaltet zusätzlich die Konten.</summary>
    public const string Verwaltung = "Verwaltung";

    public static readonly IReadOnlyList<string> Alle = [Lesen, Bearbeiten, Verwaltung];

    public static string Beschreibung(string rolle) => rolle switch
    {
        Lesen => "Lesen (Plan und Termine)",
        Bearbeiten => "Bearbeiten (alle Kursdaten)",
        Verwaltung => "Verwaltung (Kursdaten und Konten)",
        _ => rolle,
    };
}

/// <summary>Namen der Berechtigungsrichtlinien für [Authorize(Policy = …)] und AuthorizeView.</summary>
public static class Richtlinien
{
    public const string Lesen = "Lesen";
    public const string Bearbeiten = "Bearbeiten";
    public const string Verwaltung = "Verwaltung";
}

/// <summary>Eigene Claims im Anmelde-Cookie.</summary>
public static class Ansprueche
{
    public const string Anzeigename = "hundekurs:anzeigename";
    public const string PasswortAendern = "hundekurs:passwort-aendern";
    public const string Person = "hundekurs:person";

    public static string Name(ClaimsPrincipal benutzer) =>
        benutzer.FindFirstValue(Anzeigename) is { Length: > 0 } name ? name : benutzer.Identity?.Name ?? "unbekannt";

    public static bool DarfBearbeiten(ClaimsPrincipal benutzer) =>
        benutzer.IsInRole(Rollen.Bearbeiten) || benutzer.IsInRole(Rollen.Verwaltung);

    public static int? PersonId(ClaimsPrincipal benutzer) =>
        int.TryParse(benutzer.FindFirstValue(Person), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    /// <summary>Was das Konto von anderen Teilnehmenden sehen darf (Namen, Ergebnisse).</summary>
    public static Sicht Sichtrechte(ClaimsPrincipal benutzer) =>
        benutzer.Identity?.IsAuthenticated == true ? new Sicht(DarfBearbeiten(benutzer), PersonId(benutzer)) : Sicht.Eingeschraenkt;

    /// <summary>Für Stempel, Historie und die serverseitige Schreibsperre in der Datenebene.</summary>
    public static Sitzungsbenutzer? Sitzung(ClaimsPrincipal benutzer) =>
        benutzer.Identity?.IsAuthenticated == true ? new Sitzungsbenutzer(Name(benutzer), DarfBearbeiten(benutzer)) : null;
}
