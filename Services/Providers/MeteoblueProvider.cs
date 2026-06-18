using System.Globalization;
using System.Text.Json;

namespace WeatherWidget.Services.Providers;

/// <summary>
/// Meteoblue — free NDF (Numeric Data Feed) "genius" endpoint. Requires an
/// API key. Returns <c>data_day.temperature_max</c> for the first forecast day.
/// Note: the exact package available depends on your Meteoblue subscription;
/// this reads the standard daily temperature_max array.
/// </summary>
public sealed class MeteoblueProvider : WeatherProviderBase
{
    private readonly string _apiKey;
    public MeteoblueProvider(IHttpClientFactory factory, Dictionary<string, ProviderOptions> opts) : base(factory)
    {
        _apiKey = opts.GetValueOrDefault("Meteoblue")?.ApiKey ?? string.Empty;
    }
    public override string Name => "Meteoblue";
    protected override string UserAgent => "WeatherWidget/1.0 (meteoblue)";

    protected override string BuildUrl(double lat, double lon) =>
        $"https://my.meteoblue.com/data/ndfgenius/1.0?apikey={Uri.EscapeDataString(_apiKey)}" +
        $"&lat={lat:F4}&lon={lon:F4}&asl=3&format=json&tz=Europe%2FAmsterdam";

    protected override (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon)
    {
        using var doc = ParseJson(body);
        var root = doc.RootElement;
        // data_day.temperature_max is the canonical key in the NDF genius feed.
        if (root.TryGetProperty("data_day", out var day) &&
            day.TryGetProperty("temperature_max", out var arr) &&
            arr.GetArrayLength() > 0 &&
            arr[0].TryGetDouble(out var max))
        {
            DateTime date = DateTime.Today;
            if (day.TryGetProperty("time", out var times) && times.GetArrayLength() > 0 &&
                times[0].ValueKind == JsonValueKind.String)
            {
                date = DateTime.Parse(times[0].GetString()!, CultureInfo.InvariantCulture).Date;
            }
            return (max, date);
        }
        return (null, DateTime.Today);
    }
}
