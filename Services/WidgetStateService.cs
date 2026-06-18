namespace WeatherWidget.Services;

/// <summary>
/// In-memory bridge between the background <see cref="WeatherOrchestrator"/>
/// and the Blazor UI. Holds the latest computed prediction + per-provider
/// status and notifies subscribed components so they re-render.
/// </summary>
public sealed class WidgetStateService
{
    private PredictionResult? _prediction;
    private DateTime _lastRefreshUtc;
    private IReadOnlyList<ProviderPollStatus> _lastPoll = Array.Empty<ProviderPollStatus>();
    private bool _isPolling;

    public PredictionResult? Prediction => _prediction;
    public DateTime LastRefreshUtc => _lastRefreshUtc;
    public IReadOnlyList<ProviderPollStatus> LastPoll => _lastPoll;
    public bool IsPolling => _isPolling;

    /// <summary>
    /// Native window handle, set by <c>Program.cs</c> after the Photino window
    /// is created. The UI uses it to forward HTML drag events to Win32.
    /// </summary>
    public IntPtr WindowHandle { get; set; }

    /// <summary>UI components subscribe here to trigger StateHasChanged.</summary>
    public event Action? OnChange;

    public void SetPolling(bool polling)
    {
        _isPolling = polling;
        OnChange?.Invoke();
    }

    public void Update(PredictionResult? prediction, DateTime refreshUtc, IReadOnlyList<ProviderPollStatus> poll)
    {
        _prediction = prediction;
        _lastRefreshUtc = refreshUtc;
        _lastPoll = poll;
        OnChange?.Invoke();
    }

    public void Notify() => OnChange?.Invoke();
}

/// <summary>Compact per-provider status for the "last hour" indicator.</summary>
public sealed record ProviderPollStatus(string Provider, bool Success, double? TemperatureCelsius, string? Error, DateTime CheckedUtc);
