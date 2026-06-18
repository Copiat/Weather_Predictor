using System.Diagnostics;
using System.Text.Json;

namespace WeatherWidget.Services;

/// <summary>
/// Shared plumbing for every <see cref="IWeatherProvider"/>: a resilient HTTP
/// GET, response truncation for storage, and stopwatch timing. Each provider
/// owns its own <see cref="HttpClient"/> (created from the factory so handlers
/// are pooled) because they set provider-specific User-Agent / auth headers.
/// Concrete providers only implement <see cref="ParseHighTemperature"/>.
/// </summary>
public abstract class WeatherProviderBase : IWeatherProvider
{
    protected HttpClient Http { get; }
    protected abstract string UserAgent { get; }

    protected WeatherProviderBase(IHttpClientFactory factory)
    {
        Http = factory.CreateClient();
        Http.Timeout = TimeSpan.FromSeconds(20);
        // A descriptive User-Agent keeps us polite with free APIs (e.g. Yr.no).
        if (!Http.DefaultRequestHeaders.UserAgent.Any())
            Http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public abstract string Name { get; }

    public async Task<WeatherProviderResult> FetchDailyHighAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var url = BuildUrl(latitude, longitude);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            ConfigureRequest(req);
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return WeatherProviderResult.Fail(Name,
                    $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}", sw.Elapsed);
            }
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var (temp, date) = ParseHighTemperature(body, latitude, longitude);
            if (temp is null)
                return WeatherProviderResult.Fail(Name, "Unable to parse daily high from response.", sw.Elapsed);

            return WeatherProviderResult.Ok(Name, temp.Value, date, Truncate(body), sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return WeatherProviderResult.Fail(Name, "Request timed out.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return WeatherProviderResult.Fail(Name, ex.GetType().Name + ": " + ex.Message, sw.Elapsed);
        }
    }

    protected abstract string BuildUrl(double latitude, double longitude);
    protected abstract (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon);

    /// <summary>Override to add auth headers (API keys, basic auth, etc.).</summary>
    protected virtual void ConfigureRequest(HttpRequestMessage req) { }

    protected static string Truncate(string s, int max = 2048) =>
        s.Length <= max ? s : s[..max];

    protected static JsonDocument ParseJson(string body)
    {
        try { return JsonDocument.Parse(body); }
        catch { throw new InvalidOperationException("Invalid JSON payload."); }
    }
}
