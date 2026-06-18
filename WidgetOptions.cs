namespace WeatherWidget;

/// <summary>
/// Strongly typed mapping of the <c>WeatherWidget</c> section in appsettings.json.
/// </summary>
public sealed class WidgetOptions
{
    public LocationOptions Location { get; set; } = new();
    public PollingOptions Polling { get; set; } = new();
    public PredictionOptions Prediction { get; set; } = new();
    public DatabaseOptions Database { get; set; } = new();

    public sealed class LocationOptions
    {
        public string Name { get; set; } = "Amsterdam Airport Schiphol";
        public string Icao { get; set; } = "EHAM";
        public double Latitude { get; set; } = 52.318;
        public double Longitude { get; set; } = 4.7639;
        public string Timezone { get; set; } = "Europe/Amsterdam";
    }

    public sealed class PollingOptions
    {
        public int IntervalMinutes { get; set; } = 60;
        public bool RunImmediatelyOnStart { get; set; } = true;
    }

    public sealed class PredictionOptions
    {
        public int AccuracyWindowDays { get; set; } = 7;
    }

    public sealed class DatabaseOptions
    {
        public string Path { get; set; } = "weatherwidget.db";
    }
}

/// <summary>
/// Per-provider toggle + credentials read from the <c>WeatherProviders</c>
/// section of appsettings.json.
/// </summary>
public sealed class ProviderOptions
{
    public bool Enabled { get; set; } = true;
    public string? ApiKey { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}
