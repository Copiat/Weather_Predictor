using Microsoft.EntityFrameworkCore;
using WeatherWidget.Data;
using WeatherWidget.Data.Models;

namespace WeatherWidget.Services;

/// <summary>
/// One provider's contribution to today's smart prediction: the value it
/// reported plus the weight the engine assigned to it.
/// </summary>
public sealed class ProviderContribution
{
    public string Provider { get; set; } = string.Empty;
    public double TemperatureCelsius { get; set; }
    public double Weight { get; set; }
    public double MeanAbsoluteError { get; set; }
    public int Samples { get; set; }
}

/// <summary>
/// Output of the smart-prediction engine for a single day.
/// </summary>
public sealed class PredictionResult
{
    public DateTime ForecastDate { get; set; }
    public double PredictedHighC { get; set; }
    public DateTime ComputedUtc { get; set; }
    public double Mean { get; set; }
    public double StdDev { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public int SourceCount { get; set; }
    /// <summary>0..100 — agreement between sources (higher = more confident).</summary>
    public double Confidence { get; set; }
    public List<ProviderContribution> Contributions { get; set; } = new();
}

/// <summary>
/// Computes the daily "Smart Prediction" for Schiphol's highest temperature.
///
/// The algorithm is intentionally NOT a plain average and NOT a "pick the max":
///   1. Collect the freshest forecast for today from every provider.
///   2. For each provider, look up the last N days (default 7) of manually
///      verified actual highs and compute its Mean Absolute Error (MAE).
///   3. Convert each MAE into a weight with Laplace smoothing:
///        weight = 1 / (1 + MAE)
///      Providers with no track record receive a neutral baseline so a brand
///      new source isn't ignored, and a perfect source doesn't dominate
///      infinitely.
///   4. Normalise the weights and produce a weighted sum.
///   5. Derive a confidence score from how tightly the sources agree
///      (inverse of the coefficient of variation).
/// </summary>
public sealed class PredictionEngine
{
    private readonly IDbContextFactory<WeatherDbContext> _dbf;
    private readonly int _accuracyWindowDays;

    // Baseline weight granted to a provider that has zero historical samples,
    // so it can still influence the result while it builds a track record.
    private const double BaselineWeight = 0.15;

    public PredictionEngine(IDbContextFactory<WeatherDbContext> dbf, WidgetOptions opts)
    {
        _dbf = dbf;
        _accuracyWindowDays = opts.Prediction.AccuracyWindowDays;
    }

    public async Task<PredictionResult?> ComputeAsync(DateTime forecastDate, CancellationToken ct = default)
    {
        await using var db = await _dbf.CreateDbContextAsync(ct);

        var today = DateOnly.FromDateTime(forecastDate);

        // 1. Freshest snapshot per provider for the forecast day.
        var snapshots = await db.Snapshots
            .Where(s => s.ForecastDate == forecastDate && s.Success && s.TemperatureCelsius != null)
            .ToListAsync(ct);

        var latest = snapshots
            .GroupBy(s => s.Provider)
            .Select(g => g.OrderByDescending(s => s.FetchedUtc).First())
            .ToList();

        if (latest.Count == 0) return null;

        // 2. Load actuals inside the accuracy window.
        var windowStart = forecastDate.AddDays(-_accuracyWindowDays);
        var actuals = await db.Actuals
            .Where(a => a.Date >= windowStart && a.Date < forecastDate)
            .ToListAsync(ct);
        var actualByDate = actuals.ToDictionary(a => DateOnly.FromDateTime(a.Date));

        // 3. Load historical predictions per provider within the window so we
        //    can score accuracy. For each forecast day we use the snapshot that
        //    was made earliest that day (closest to "predicting ahead").
        var hist = await db.Snapshots
            .Where(s => s.ForecastDate >= windowStart && s.ForecastDate < forecastDate
                        && s.Success && s.TemperatureCelsius != null)
            .ToListAsync(ct);

        var histByProvider = hist
            .GroupBy(s => s.Provider)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 4. Compute per-provider MAE + weight.
        var contributions = new List<ProviderContribution>();
        foreach (var snap in latest)
        {
            var mae = 0.0;
            var samples = 0;

            if (histByProvider.TryGetValue(snap.Provider, out var list))
            {
                foreach (var grp in list.GroupBy(s => DateOnly.FromDateTime(s.ForecastDate)))
                {
                    if (!actualByDate.TryGetValue(grp.Key, out var actual)) continue;
                    var predictedTemp = grp.OrderBy(s => s.FetchedUtc).First().TemperatureCelsius!.Value;
                    mae += Math.Abs(predictedTemp - actual.TemperatureCelsius);
                    samples++;
                }
            }

            var avgError = samples > 0 ? mae / samples : 0.0;
            var weight = samples > 0 ? 1.0 / (1.0 + avgError) : BaselineWeight;

            contributions.Add(new ProviderContribution
            {
                Provider = snap.Provider,
                TemperatureCelsius = snap.TemperatureCelsius!.Value,
                Weight = weight,
                MeanAbsoluteError = avgError,
                Samples = samples
            });
        }

        // 5. Normalise weights → weighted prediction.
        var totalW = contributions.Sum(c => c.Weight);
        if (totalW <= 0)
        {
            // Degenerate: fall back to a plain mean.
            foreach (var c in contributions) c.Weight = 1.0 / contributions.Count;
            totalW = 1;
        }
        foreach (var c in contributions) c.Weight /= totalW;

        var predicted = contributions.Sum(c => c.Weight * c.TemperatureCelsius);

        // 6. Variance distribution across sources.
        var temps = contributions.Select(c => c.TemperatureCelsius).ToList();
        var mean = temps.Average();
        var variance = temps.Average(t => Math.Pow(t - mean, 2));
        var std = Math.Sqrt(variance);

        // Confidence: inverse coefficient of variation, scaled to 0..100.
        // Tight agreement → high confidence; wide spread → low confidence.
        double confidence;
        if (Math.Abs(mean) < 0.001)
            confidence = Math.Max(0, 100 - std * 25);
        else
        {
            var cv = std / Math.Abs(mean);
            confidence = Math.Clamp(100 - cv * 100, 0, 100);
        }

        return new PredictionResult
        {
            ForecastDate = forecastDate,
            PredictedHighC = Math.Round(predicted, 1),
            ComputedUtc = DateTime.UtcNow,
            Mean = Math.Round(mean, 1),
            StdDev = Math.Round(std, 2),
            Min = Math.Round(temps.Min(), 1),
            Max = Math.Round(temps.Max(), 1),
            SourceCount = contributions.Count,
            Confidence = Math.Round(confidence, 0),
            Contributions = contributions.OrderByDescending(c => c.Weight).ToList()
        };
    }
}
