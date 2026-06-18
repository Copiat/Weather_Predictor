using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using WeatherWidget.Data;
using WeatherWidget.Data.Models;

namespace WeatherWidget.Services;

/// <summary>
/// Background orchestrator that polls every registered <see cref="IWeatherProvider"/>
/// once per hour (configurable), persists each forecast to SQLite, runs the
/// <see cref="PredictionEngine"/>, and pushes the result to the UI through
/// <see cref="WidgetStateService"/>.
/// </summary>
public sealed class WeatherOrchestrator
{
    private readonly IEnumerable<IWeatherProvider> _providers;
    private readonly IDbContextFactory<WeatherDbContext> _dbf;
    private readonly PredictionEngine _engine;
    private readonly WidgetStateService _state;
    private readonly WidgetOptions _opts;
    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    /// <summary>Native HWND, set by Program.cs — kept so future native ops work.</summary>
    public IntPtr WindowHandle { get; set; }

    public WeatherOrchestrator(
        IEnumerable<IWeatherProvider> providers,
        IDbContextFactory<WeatherDbContext> dbf,
        PredictionEngine engine,
        WidgetStateService state,
        WidgetOptions opts)
    {
        _providers = providers;
        _dbf = dbf;
        _engine = engine;
        _state = state;
        _opts = opts;
    }

    public Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        if (_opts.Polling.RunImmediatelyOnStart)
        {
            await PollAsync(ct).ConfigureAwait(false);
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _opts.Polling.IntervalMinutes));
        _timer = new PeriodicTimer(interval);

        try
        {
            while (await _timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await PollAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    /// <summary>Manual refresh entry-point invoked by the UI "refresh" button.</summary>
    public Task PollNowAsync() => PollAsync(_cts?.Token ?? CancellationToken.None);

    private async Task PollAsync(CancellationToken ct)
    {
        _state.SetPolling(true);
        try
        {
            var lat = _opts.Location.Latitude;
            var lon = _opts.Location.Longitude;
            var today = DateTime.Today;
            var nowUtc = DateTime.UtcNow;

            // Fan out every provider in parallel; each is self-contained and
            // never throws (the base class guarantees a result).
            var tasks = _providers.Select(p => p.FetchDailyHighAsync(lat, lon, ct)).ToList();
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            // Persist snapshots + status logs.
            await using var db = await _dbf.CreateDbContextAsync(ct);
            foreach (var r in results)
            {
                db.Snapshots.Add(new WeatherSnapshot
                {
                    Provider = r.Provider,
                    FetchedUtc = nowUtc,
                    ForecastDate = today,
                    TemperatureCelsius = r.TemperatureCelsius,
                    Success = r.Success,
                    Error = r.Error,
                    Raw = r.Raw
                });
                db.ProviderStatuses.Add(new ProviderStatusLog
                {
                    Provider = r.Provider,
                    CheckedUtc = nowUtc,
                    Success = r.Success,
                    TemperatureCelsius = r.TemperatureCelsius,
                    Error = r.Error
                });
            }
            await db.SaveChangesAsync(ct);

            // Compute the smart prediction against today.
            var prediction = await _engine.ComputeAsync(today, ct).ConfigureAwait(false);

            var poll = results.Select(r => new ProviderPollStatus(
                r.Provider, r.Success, r.TemperatureCelsius, r.Error, nowUtc)).ToList();

            _state.Update(prediction, nowUtc, poll);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WeatherOrchestrator] Poll failed: {ex}");
            _state.Update(null, DateTime.UtcNow,
                new[] { new ProviderPollStatus("orchestrator", false, null, ex.Message, DateTime.UtcNow) });
        }
        finally
        {
            _state.SetPolling(false);
        }
    }
}
