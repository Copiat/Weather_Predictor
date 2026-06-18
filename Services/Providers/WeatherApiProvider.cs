using System.Globalization;
using System.Text.Json;

namespace WeatherWidget.Services.Providers;

/// <summary>
/// WeatherAPI.com — free tier forecast endpoint, returns a ready-made daily
/// maximum temperature in °C.
/// </summary>
public sealed class WeatherApiProvider : WeatherProviderBase
{
    private readonly string _apiKey;
    public WeatherApiProvider(IHttpClientFactory factory, Dictionary<string, ProviderOptions> opts) : base(factory)
    {
        _apiKey = opts.GetValueOrDefault("WeatherApi")?.ApiKey ?? string.Empty;
    }
    public override string Name => "WeatherAPI.com";
    protected override string UserAgent => "WeatherWidget/1.0 (weatherapi)";

    protected override string BuildUrl(double lat, double lon) =>
        $"https://api.weatherapi.com/v1/forecast.json?key={Uri.EscapeDataString(_apiKey)}" +
        $"&q={lat:F4},{lon:F4}&days=1&aqi=no&alerts=no";

    protected override (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon)
    {
        using var doc = ParseJson(body);
        var day = doc.RootElement.GetProperty("forecast").GetProperty("forecastday")[0];
        var maxC = day.GetProperty("day").GetProperty("maxtemp_c").GetDouble();
        var date = DateTime.Parse(day.GetProperty("date").GetString()!, CultureInfo.InvariantCulture);
        return (maxC, date);
    }
}
