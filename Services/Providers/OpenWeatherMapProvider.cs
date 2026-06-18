using System.Globalization;
using System.Text.Json;

namespace WeatherWidget.Services.Providers;

/// <summary>
/// OpenWeatherMap — free tier "5 day / 3 hour forecast". We reduce the 3-hourly
/// entries that fall on today to a single daily high.
/// </summary>
public sealed class OpenWeatherMapProvider : WeatherProviderBase
{
    private readonly string _apiKey;
    public OpenWeatherMapProvider(IHttpClientFactory factory, Dictionary<string, ProviderOptions> opts) : base(factory)
    {
        _apiKey = opts.GetValueOrDefault("OpenWeatherMap")?.ApiKey ?? string.Empty;
    }
    public override string Name => "OpenWeatherMap";
    protected override string UserAgent => "WeatherWidget/1.0 (openweathermap)";

    protected override string BuildUrl(double lat, double lon) =>
        $"https://api.openweathermap.org/data/2.5/forecast?lat={lat:F4}&lon={lon:F4}" +
        $"&units=metric&appid={Uri.EscapeDataString(_apiKey)}";

    protected override (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon)
    {
        using var doc = ParseJson(body);
        var list = doc.RootElement.GetProperty("list");
        var today = DateTime.Today;
        double max = double.MinValue;
        var found = false;
        foreach (var e in list.EnumerateArray())
        {
            var dtTxt = e.GetProperty("dt_txt").GetString()!;
            var dt = DateTime.ParseExact(dtTxt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            if (dt.Date != today) continue;
            if (e.TryGetProperty("main", out var main) && main.TryGetProperty("temp", out var t) && t.TryGetDouble(out var v))
            {
                if (v > max) max = v;
                found = true;
            }
        }
        return (found ? max : (double?)null, today);
    }
}
