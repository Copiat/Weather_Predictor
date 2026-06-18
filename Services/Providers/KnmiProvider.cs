using System.Globalization;

namespace WeatherWidget.Services.Providers;

/// <summary>
/// KNMI (Royal Netherlands Meteorological Institute).
///
/// KNMI's cleanest free, keyless endpoint is the daily station archive
/// (<c>daggegevens.knmi.nl</c>) for Schiphol (station 240). It returns the
/// official observed daily maximum temperature (TX) as CSV. Because the daily
/// value publishes after the day closes, this provider contributes the latest
/// available KNMI-verified Schiphol high as a persistence/anchor source — the
/// most authoritative reading of the location.
///
/// KNMI's full forecast models (HARMONIE-AROME) are distributed via the KNMI
/// Data Platform as downloadable dataset files; that path can be wired in
/// behind this same <see cref="IWeatherProvider"/> contract without touching
/// the rest of the app.
/// </summary>
public sealed class KnmiProvider : WeatherProviderBase
{
    public KnmiProvider(IHttpClientFactory factory) : base(factory) { }
    public override string Name => "KNMI";
    protected override string UserAgent => "WeatherWidget/1.0 (knmi-station-240)";

    protected override string BuildUrl(double lat, double lon)
    {
        // Fetch the most recent few days so the latest published TX is returned.
        var end = DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var start = DateTime.Today.AddDays(-4).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"https://www.daggegevens.knmi.nl/klimatologie/daggegevens" +
               $"?start={start}&end={end}&stns=240&vars=TX";
    }

    protected override (double? tempC, DateTime forecastDate) ParseHighTemperature(string body, double lat, double lon)
    {
        // KNMI returns a CSV file with '#' comment lines followed by a header
        // row and data rows. TX is expressed in 0.1 °C.
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int txIndex = -1, dateIndex = -1;
        string? headerLine = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            if (headerLine is null)
            {
                headerLine = line;
                var cols = line.Split(',');
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i].Trim();
                    if (c.Equals("TX", StringComparison.OrdinalIgnoreCase)) txIndex = i;
                    if (c.Equals("YYYYMMDD", StringComparison.OrdinalIgnoreCase)) dateIndex = i;
                }
                continue;
            }

            // First data row = most recent published day (KNMI orders descending).
            var parts = line.Split(',');
            if (txIndex < 0 || txIndex >= parts.Length) return (null, DateTime.Today);

            var txStr = parts[txIndex].Trim();
            if (txStr == "" || !double.TryParse(txStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var txTenths))
                return (null, DateTime.Today);

            DateTime date = DateTime.Today;
            if (dateIndex >= 0 && dateIndex < parts.Length &&
                DateTime.TryParseExact(parts[dateIndex].Trim(), "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                date = d;
            }

            return (txTenths / 10.0, date);
        }

        return (null, DateTime.Today);
    }
}
