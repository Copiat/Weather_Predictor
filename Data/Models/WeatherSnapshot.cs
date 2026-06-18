using System.ComponentModel.DataAnnotations;

namespace WeatherWidget.Data.Models;

/// <summary>
/// One row per (provider, snapshot time) — the raw forecast value each API
/// returned for today's daily high temperature. Written every hour.
/// </summary>
public sealed class WeatherSnapshot
{
    public int Id { get; set; }

    /// <summary>Which weather API produced this value (e.g. "Open-Meteo").</summary>
    [MaxLength(64)] public string Provider { get; set; } = string.Empty;

    /// <summary>When the snapshot was fetched (UTC).</summary>
    public DateTime FetchedUtc { get; set; }

    /// <summary>The calendar day the forecast applies to (local, date-only).</summary>
    public DateTime ForecastDate { get; set; }

    /// <summary>Predicted daily high in °C. Null when the API call failed.</summary>
    public double? TemperatureCelsius { get; set; }

    public bool Success { get; set; }

    [MaxLength(512)] public string? Error { get; set; }

    /// <summary>Raw response payload kept for auditing (truncated).</summary>
    [MaxLength(2048)] public string? Raw { get; set; }
}

/// <summary>
/// Manually entered actual observed high temperature for a given day, used to
/// score how accurate each provider's predictions have been.
/// </summary>
public sealed class ActualTemperature
{
    public int Id { get; set; }

    /// <summary>The calendar day this actual temperature applies to.</summary>
    public DateTime Date { get; set; }

    /// <summary>Verified actual daily high in °C.</summary>
    public double TemperatureCelsius { get; set; }

    public DateTime EnteredUtc { get; set; }

    [MaxLength(160)] public string? Source { get; set; }
}

/// <summary>
/// Lightweight rolling log of which providers answered OK in the last poll,
/// used to drive the "last hour" status indicator in the config view.
/// </summary>
public sealed class ProviderStatusLog
{
    public int Id { get; set; }

    [MaxLength(64)] public string Provider { get; set; } = string.Empty;

    public DateTime CheckedUtc { get; set; }

    public bool Success { get; set; }

    public double? TemperatureCelsius { get; set; }

    [MaxLength(512)] public string? Error { get; set; }
}
