using System.Globalization;
using System.Text.Json;

namespace WeatherWidget.Services.Providers;

/// <summary>
/// Tomorrow.io v4 timelines — free tier. Requests a 1-day timeline of
/// <c>temperatureMax</c> for today.
/// </summary>
public sealed class TomorrowIoProvider : WeatherProviderBase
{
    private readonly string _apiKey;
    public TomorrowIoProvider(IHttpClientFactory factory, Dictionary<string, ProviderOptions> opts) : base(factory)
    {
        _apiKey = opts.GetValueOrDefault("TomorrowIo")?.ApiKey ?? string.Empty;
    }
    public override string Name => "Tomorrow.io";
    protected override string UserAgent => "WeatherWidget/1.0 (tomorrow.io)";

    protected override string BuildUrl(double lat, double lon) =>
        $"https://api.tomorrow.io/v4/timelines?location={lat:F4},{lon:F4}" +
        $"&fields=temperatureMax&timesteps=1d&units=metric&apikey={Uri.EscapeDataString(_apiKey)}";

    protected override (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon)
    {
        using var doc = ParseJson(body);
        var intervals = doc.RootElement.GetProperty("data").GetProperty("timelines")[0].GetProperty("intervals");
        if (intervals.GetArrayLength() == 0) return (null, DateTime.Today);
        var iv = intervals[0];
        var max = iv.GetProperty("values").GetProperty("temperatureMax").GetDouble();
        var startStr = iv.GetProperty("startTime").GetString()!;
        var date = DateTime.Parse(startStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).Date;
        return (max, date);
    }
}
