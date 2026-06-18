using System.Globalization;
using System.Text.Json;

namespace WeatherWidget.Services.Providers;

/// <summary>
/// Open-Meteo — completely free, no API key required.
/// Returns the ECMWF/GFS blended daily maximum 2 m temperature.
/// </summary>
public sealed class OpenMeteoProvider : WeatherProviderBase
{
    public OpenMeteoProvider(IHttpClientFactory factory) : base(factory) { }
    public override string Name => "Open-Meteo";
    protected override string UserAgent => "WeatherWidget/1.0 (open-meteo)";

    protected override string BuildUrl(double lat, double lon)
    {
        var la = lat.ToString("F4", CultureInfo.InvariantCulture);
        var lo = lon.ToString("F4", CultureInfo.InvariantCulture);
        return $"https://api.open-meteo.com/v1/forecast?latitude={la}&longitude={lo}" +
               $"&daily=temperature_2m_max&timezone=Europe%2FAmsterdam&forecast_days=1";
    }

    protected override (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon)
    {
        using var doc = ParseJson(body);
        var daily = doc.RootElement.GetProperty("daily");
        var temps = daily.GetProperty("temperature_2m_max");
        var times = daily.GetProperty("time");
        if (temps.GetArrayLength() == 0) return (null, DateTime.Today);
        var t = temps[0].GetDouble();
        var date = DateTime.Parse(times[0].GetString()!, CultureInfo.InvariantCulture);
        return (t, date);
    }
}
