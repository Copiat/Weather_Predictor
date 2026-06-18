using System.Globalization;
using System.Text.Json;

namespace WeatherWidget.Services.Providers;

/// <summary>
/// Yr.no (MET Norway) Locationforecast 2.0 — free, no key, requires a
/// descriptive User-Agent (set by the base class). We reduce the compact
/// time series to today's maximum air temperature.
/// </summary>
public sealed class YrNoProvider : WeatherProviderBase
{
    public YrNoProvider(IHttpClientFactory factory) : base(factory) { }
    public override string Name => "Yr.no";
    protected override string UserAgent => "WeatherWidget/1.0 github.com/weatherwidget (Schiphol)";

    protected override string BuildUrl(double lat, double lon)
    {
        var la = lat.ToString("F4", CultureInfo.InvariantCulture);
        var lo = lon.ToString("F4", CultureInfo.InvariantCulture);
        return $"https://api.met.no/weatherapi/locationforecast/2.0/compact?lat={la}&lon={lo}";
    }
        

    protected override (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon)
    {
        using var doc = ParseJson(body);
        var series = doc.RootElement.GetProperty("properties").GetProperty("timeseries");
        var today = DateTime.Today;
        double max = double.MinValue;
        var found = false;
        foreach (var item in series.EnumerateArray())
        {
            var timeStr = item.GetProperty("time").GetString()!;
            var time = DateTime.Parse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            if (time.Date != today) continue;
            if (item.TryGetProperty("data", out var data) &&
                data.TryGetProperty("instant", out var inst) &&
                inst.TryGetProperty("details", out var det) &&
                det.TryGetProperty("air_temperature", out var at) &&
                at.TryGetDouble(out var t))
            {
                if (t > max) max = t;
                found = true;
            }
        }
        return (found ? max : (double?)null, today);
    }
}
