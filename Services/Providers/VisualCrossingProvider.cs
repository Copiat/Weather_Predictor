using System.Globalization;
using System.Text.Json;

namespace WeatherWidget.Services.Providers;

/// <summary>
/// Visual Crossing Weather API — free tier timeline endpoint. We request only
/// the daily aggregate fields for today and read <c>tempmax</c>.
/// </summary>
public sealed class VisualCrossingProvider : WeatherProviderBase
{
    private readonly string _apiKey;
    public VisualCrossingProvider(IHttpClientFactory factory, Dictionary<string, ProviderOptions> opts) : base(factory)
    {
        _apiKey = opts.GetValueOrDefault("VisualCrossing")?.ApiKey ?? string.Empty;
    }
    public override string Name => "Visual Crossing";
    protected override string UserAgent => "WeatherWidget/1.0 (visualcrossing)";

    protected override string BuildUrl(double lat, double lon)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var la = lat.ToString("F4", CultureInfo.InvariantCulture);
        var lo = lon.ToString("F4", CultureInfo.InvariantCulture);
        return $"https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline/" +
               $"{la},{lo}/{today}?unitGroup=metric&key={Uri.EscapeDataString(_apiKey)}" +
               $"&include=days&elements=datetime,tempmax";
    }

    protected override (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon)
    {
        using var doc = ParseJson(body);
        var days = doc.RootElement.GetProperty("days");
        if (days.GetArrayLength() == 0) return (null, DateTime.Today);
        var d = days[0];
        var max = d.GetProperty("tempmax").GetDouble();
        var dateStr = d.GetProperty("datetime").GetString()!;
        var date = DateTime.ParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (max, date);
    }
}
