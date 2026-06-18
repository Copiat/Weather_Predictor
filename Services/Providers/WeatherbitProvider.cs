using System.Globalization;
using System.Text.Json;

namespace WeatherWidget.Services.Providers;

/// <summary>
/// Weatherbit.io — free tier 16-day daily forecast. Returns max_temp directly.
/// </summary>
public sealed class WeatherbitProvider : WeatherProviderBase
{
    private readonly string _apiKey;
    public WeatherbitProvider(IHttpClientFactory factory, Dictionary<string, ProviderOptions> opts) : base(factory)
    {
        _apiKey = opts.GetValueOrDefault("Weatherbit")?.ApiKey ?? string.Empty;
    }
    public override string Name => "Weatherbit";
    protected override string UserAgent => "WeatherWidget/1.0 (weatherbit)";

    protected override string BuildUrl(double lat, double lon)
    { 
        var la = lat.ToString("F4", CultureInfo.InvariantCulture);
        var lo = lon.ToString("F4", CultureInfo.InvariantCulture);
        return $"https://api.weatherbit.io/v2.0/forecast/daily?lat={la}&lon={lo}" +
                $"&units=M&days=1&key={Uri.EscapeDataString(_apiKey)}";
    }
        

    protected override (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon)
    {
        using var doc = ParseJson(body);
        var data = doc.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) return (null, DateTime.Today);
        var first = data[0];
        var max = first.GetProperty("max_temp").GetDouble();
        var dateStr = first.GetProperty("valid_date").GetString()!;
        var date = DateTime.ParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (max, date);
    }
}
