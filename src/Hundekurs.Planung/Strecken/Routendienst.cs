using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hundekurs.Planung.Strecken;

public sealed record Koordinate(double Breite, double Laenge)
{
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Breite:0.######}, {Laenge:0.######}");

    /// <summary>Liest „50.123, 8.456“ (Punkt oder Komma als Dezimaltrennzeichen bei Leerzeichen-Trennung).</summary>
    public static bool TryParse(string? text, out Koordinate? koordinate)
    {
        koordinate = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var teile = text.Contains(';') ? text.Split(';') : text.Contains(", ") ? text.Split(", ") : text.Split(',');
        if (teile.Length != 2) return false;
        if (!double.TryParse(teile[0].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var b)) return false;
        if (!double.TryParse(teile[1].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var l)) return false;
        if (b is < -90 or > 90 || l is < -180 or > 180) return false;
        koordinate = new Koordinate(b, l);
        return true;
    }
}

public sealed class RoutendienstException(string meldung, Exception? inner = null) : Exception(meldung, inner);

/// <summary>Adresssuche und Streckenberechnung. Übertragen werden nur Adressen bzw. Koordinaten, niemals Namen.</summary>
public interface IRoutendienst
{
    bool Verfuegbar { get; }

    /// <summary>Wandelt eine Adresse in Koordinaten um. Liefert null, wenn nichts gefunden wurde.</summary>
    Task<Koordinate?> AdresseSuchen(string adresse, CancellationToken ct = default);

    /// <summary>Kürzeste Autostrecke in km (einfach).</summary>
    Task<double> StreckeKm(Koordinate von, Koordinate nach, CancellationToken ct = default);
}

/// <summary>Kein Schlüssel hinterlegt: km werden nur von Hand erfasst.</summary>
public sealed class KeinRoutendienst : IRoutendienst
{
    public bool Verfuegbar => false;
    public Task<Koordinate?> AdresseSuchen(string adresse, CancellationToken ct = default) => Task.FromResult<Koordinate?>(null);
    public Task<double> StreckeKm(Koordinate von, Koordinate nach, CancellationToken ct = default) =>
        throw new RoutendienstException("Kein Routendienst eingerichtet (Schlüssel fehlt).");
}

/// <summary>
/// openrouteservice von HeiGIT (Heidelberg). Routen: /openrouteservice/v2/directions/driving-car,
/// Adresssuche: /pelias/v1/search. Der Schlüssel wird im Header „Authorization“ übertragen.
/// </summary>
public sealed class OpenRouteService : IRoutendienst
{
    public static readonly Uri StandardBasis = new("https://api.heigit.org/");

    private readonly HttpClient _http;
    private readonly string _schluessel;
    private readonly Uri _basis;

    public OpenRouteService(HttpClient http, string schluessel, Uri? basis = null)
    {
        if (string.IsNullOrWhiteSpace(schluessel)) throw new ArgumentException("Schlüssel fehlt.", nameof(schluessel));
        _http = http;
        _schluessel = schluessel.Trim();
        _basis = basis ?? StandardBasis;
    }

    public bool Verfuegbar => true;

    public async Task<Koordinate?> AdresseSuchen(string adresse, CancellationToken ct = default)
    {
        var url = new Uri(_basis, $"pelias/v1/search?text={Uri.EscapeDataString(adresse)}&boundary.country=DE&size=1");
        using var anfrage = new HttpRequestMessage(HttpMethod.Get, url);
        anfrage.Headers.TryAddWithoutValidation("Authorization", _schluessel);

        using var antwort = await Senden(anfrage, ct);
        using var json = await JsonDocument.ParseAsync(await antwort.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        try
        {
            var features = json.RootElement.GetProperty("features");
            if (features.GetArrayLength() == 0) return null;
            var koordinaten = features[0].GetProperty("geometry").GetProperty("coordinates");
            // GeoJSON: [Länge, Breite]
            return new Koordinate(koordinaten[1].GetDouble(), koordinaten[0].GetDouble());
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new RoutendienstException("Unerwartete Antwort der Adresssuche.", ex);
        }
    }

    public async Task<double> StreckeKm(Koordinate von, Koordinate nach, CancellationToken ct = default)
    {
        var url = new Uri(_basis, "openrouteservice/v2/directions/driving-car");
        using var anfrage = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                coordinates = new[] { new[] { von.Laenge, von.Breite }, new[] { nach.Laenge, nach.Breite } },
            }),
        };
        anfrage.Headers.TryAddWithoutValidation("Authorization", _schluessel);

        using var antwort = await Senden(anfrage, ct);
        using var json = await JsonDocument.ParseAsync(await antwort.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        try
        {
            var meter = json.RootElement.GetProperty("routes")[0].GetProperty("summary").GetProperty("distance").GetDouble();
            return Math.Round(meter / 1000, 1);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new RoutendienstException("Unerwartete Antwort der Streckenberechnung.", ex);
        }
    }

    private async Task<HttpResponseMessage> Senden(HttpRequestMessage anfrage, CancellationToken ct)
    {
        HttpResponseMessage antwort;
        try
        {
            antwort = await _http.SendAsync(anfrage, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new RoutendienstException("Der Routendienst ist nicht erreichbar. Besteht eine Internetverbindung?", ex);
        }

        if (antwort.IsSuccessStatusCode) return antwort;

        var status = (int)antwort.StatusCode;
        antwort.Dispose();
        throw new RoutendienstException(status switch
        {
            401 or 403 => "Der Routendienst lehnt den Schlüssel ab. Bitte den Schlüssel prüfen.",
            404 => "Der Routendienst kennt die Adresse der Schnittstelle nicht (404). Bitte die Einstellung „Routendienst:Basis“ prüfen.",
            429 => "Das Tageskontingent des Routendienstes ist erschöpft. Bitte später erneut versuchen.",
            _ => $"Der Routendienst meldet einen Fehler ({status}).",
        });
    }
}
