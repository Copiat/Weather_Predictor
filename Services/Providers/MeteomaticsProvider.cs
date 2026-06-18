using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WeatherWidget.Services.Providers;

/// <summary>
/// Meteomatics — Basic credentials (HTTP Basic auth). Free tier. Requests the
/// <c>t_max_2m_24h:C</c> parameter over a 1-day window and reads the value.
/// </summary>
public sealed class MeteomaticsProvider : WeatherProviderBase
{
    private readonly string _user;
    private readonly string _pass;

    public MeteomaticsProvider(IHttpClientFactory factory, Dictionary<string, ProviderOptions> opts) : base(factory)
    {
        var o = opts.GetValueOrDefault("Meteomatics");
        _user = o?.Username ?? string.Empty;
        _pass = o?.Password ?? string.Empty;
    }
    public override string Name => "Meteomatics";
    protected override string UserAgent => "WeatherWidget/1.0 (meteomatics)";

    protected override string BuildUrl(double lat, double lon)
    {
        var start = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var end = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        return $"https://api.meteomatics.com/{start}--{end}:P1D/t_max_2m_24h:C/{lat:F4},{lon:F4}/json";
    }

    protected override void ConfigureRequest(HttpRequestMessage req)
    {
        if (!string.IsNullOrEmpty(_user))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_user}:{_pass}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
    }

    protected override (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon)
    {
        using var doc = ParseJson(body);
        var data = doc.RootElement.GetProperty("data");
        var dates = data[0].GetProperty("coordinates")[0].GetProperty("dates");
        if (dates.GetArrayLength() == 0) return (null, DateTime.Today);
        var first = dates[0];
        var val = first.GetProperty("value").GetDouble();
        var dateStr = first.GetProperty("date").GetString()!;
        var date = DateTime.Parse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).Date;
        return (val, date);
    }
}
