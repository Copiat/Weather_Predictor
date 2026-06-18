using System.Globalization;
using System.Text.Json;

namespace WeatherWidget.Services.Providers;

/// <summary>
/// Bright Sky API (DWD open data, Germany). Free, no key. Bright Sky serves
/// per-hour observations/forecasts; we take the maximum temperature reported
/// for today. Coverage is concentrated on Germany, so for Schiphol the call
/// may legitimately return no rows — the base class converts that into a
/// graceful failed result that the engine simply skips.
/// </summary>
public sealed class BrightSkyProvider : WeatherProviderBase
{
    public BrightSkyProvider(IHttpClientFactory factory) : base(factory) { }
    public override string Name => "Bright Sky";
    protected override string UserAgent => "WeatherWidget/1.0 (brightsky)";

    protected override string BuildUrl(double lat, double lon)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var la = lat.ToString("F4", CultureInfo.InvariantCulture);
        var lo = lon.ToString("F4", CultureInfo.InvariantCulture);
        return $"https://api.brightsky.dev/weather?lat={la}&lon={lo}&date={today}&tz=Europe%2FAmsterdam";
    }

    protected override (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon)
    {
        using var doc = ParseJson(body);
        if (!doc.RootElement.TryGetProperty("weather", out var weather) || weather.GetArrayLength() == 0)
            return (null, DateTime.Today);

        double max = double.MinValue;
        var found = false;
        foreach (var w in weather.EnumerateArray())
        {
            if (w.TryGetProperty("temperature", out var t) && t.TryGetDouble(out var val))
            {
                if (val > max) max = val;
                found = true;
            }
        }
        return (found ? max : (double?)null, DateTime.Today);
    }
}
