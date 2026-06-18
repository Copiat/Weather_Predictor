namespace WeatherWidget.Services;

/// <summary>
/// Result returned by a single weather provider for one poll cycle.
/// </summary>
public sealed record WeatherProviderResult
{
    public required string Provider { get; init; }
    public bool Success { get; init; }
    public double? TemperatureCelsius { get; init; }
    public DateTime? ForecastDate { get; init; }
    public string? Raw { get; init; }
    public string? Error { get; init; }
    public TimeSpan Elapsed { get; init; }

    public static WeatherProviderResult Ok(string provider, double tempC, DateTime forecastDate, string? raw = null, TimeSpan elapsed = default)
        => new() { Provider = provider, Success = true, TemperatureCelsius = tempC, ForecastDate = forecastDate, Raw = raw, Elapsed = elapsed };

    public static WeatherProviderResult Fail(string provider, string error, TimeSpan elapsed = default)
        => new() { Provider = provider, Success = false, Error = error, Elapsed = elapsed };
}

/// <summary>
/// Modular contract for a single weather data source. Add a new source by
/// implementing this interface and registering it in <see cref="Program"/>.
/// </summary>
public interface IWeatherProvider
{
    /// <summary>Human-friendly name shown in the UI / stored in the DB.</summary>
    string Name { get; }

    /// <summary>
    /// Fetch the predicted daily-high temperature (°C) for the given location.
    /// Implementations must be resilient: never throw — return a failed result.
    /// </summary>
    Task<WeatherProviderResult> FetchDailyHighAsync(double latitude, double longitude, CancellationToken ct = default);
}
