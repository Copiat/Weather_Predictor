using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using WeatherWidget.Data;
using WeatherWidget.Native;
using WeatherWidget.Services;
using WeatherWidget.Services.Providers;

namespace WeatherWidget;

/// <summary>
/// Application entry point. Boots a Photino.Blazor native desktop window that is
/// chromeless, transparent and always-on-top, hosting the glass-morphic widget UI.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // ---- 1. Photino.Blazor host bootstrap ----
        var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);
        
        appBuilder.RootComponents.Add<App>("#app");

        // ---- 2. Configuration (explicitly load appsettings.json from the
        //         output directory so provider keys + location are picked up) ----
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        
        HttpClient.DefaultProxy =
            (config.GetValue<bool>("Proxy:Enabled") &&
             config.GetValue<string>("Proxy:Address") is { Length: > 0 } addr)
                ? new System.Net.WebProxy(addr)
                : new System.Net.WebProxy();

        var widgetOpts = config.GetSection("WeatherWidget").Get<WidgetOptions>() ?? new WidgetOptions();
        appBuilder.Services.AddSingleton(widgetOpts);

        var providerOpts = config.GetSection("WeatherProviders")
            .Get<Dictionary<string, ProviderOptions>>() ?? new Dictionary<string, ProviderOptions>();
        appBuilder.Services.AddSingleton(providerOpts);

        // ---- 3. Database (EF Core + SQLite) ----
        var dbPath = widgetOpts.Database?.Path ?? "weatherwidget.db";
        var dbFolder = AppContext.BaseDirectory;
        var dbFullPath = Path.IsPathRooted(dbPath) ? dbPath : Path.Combine(dbFolder, dbPath);
        // Factory is singleton-safe and lets background services + UI resolve
        // short-lived DbContexts without DI scope lifetime issues.
        appBuilder.Services.AddDbContextFactory<WeatherDbContext>(opts =>
            opts.UseSqlite($"Data Source={dbFullPath}"));

        // ---- 4. HttpClient for the providers ----
        appBuilder.Services.AddHttpClient();

        var proxyEnabled = config.GetValue<bool>("Proxy:Enabled");
        var proxyAddress = config.GetValue<string>("Proxy:Address");

        if (proxyEnabled && !string.IsNullOrEmpty(proxyAddress))
        {
            appBuilder.Services.ConfigureHttpClientDefaults(builder =>
            {
                builder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    Proxy = new System.Net.WebProxy(proxyAddress),
                    UseProxy = true
                });
            });
        }

        // ---- 5. Core services ----
        appBuilder.Services.AddSingleton<WidgetStateService>();
        appBuilder.Services.AddSingleton<PredictionEngine>();
        appBuilder.Services.AddSingleton<WeatherOrchestrator>();

        // ---- 6. Register all 11 weather providers behind the IWeatherProvider interface ----
        RegisterProviders(appBuilder.Services, providerOpts);

        // ---- 7. Build ----
        var app = appBuilder.Build();

        // Ensure the SQLite schema exists on first run.
        var dbf = app.Services.GetRequiredService<IDbContextFactory<WeatherDbContext>>();
        using (var db = dbf.CreateDbContext())
        {
            db.Database.EnsureCreated();
        }

        // ---- 8. Window configuration: borderless, transparent, top-most widget ----
        var window = app.MainWindow
            .SetTitle("Schiphol Smart Weather")
            .SetChromeless(true)          // no title bar / OS chrome
            .SetTransparent(true)         // transparent background canvas
            .SetSize(400, 300)
            .SetMinSize(400, 300)
            .SetMaxSize(520, 460)
            .SetUseOsDefaultSize(false)
            .SetUseOsDefaultLocation(false)
            .SetLeft(60)
            .SetTop(60)
            .SetContextMenuEnabled(false)
            .SetDevToolsEnabled(true);

        var state = app.Services.GetRequiredService<WidgetStateService>();
        var orchestrator = app.Services.GetRequiredService<WeatherOrchestrator>();

        // The native HWND only exists once Photino has actually created the OS
        // window. Reading it before app.Run() throws
        // "The Photino window is not initialized yet", so we defer all
        // HWND-dependent work to the WindowCreated event.
        window.WindowCreated += (s, e) =>
        {
            var hwnd = Win32.GetWindowHandle(window);
            if (hwnd != IntPtr.Zero)
            {
                Win32.SetTopMost(hwnd, true);
            }

            // Share the HWND with both the orchestrator and the UI bridge.
            state.WindowHandle = hwnd;
            orchestrator.WindowHandle = hwnd;
        };

        // ---- 9. Start the hourly background orchestrator ----
        _ = orchestrator.StartAsync();

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Console.Error.WriteLine($"[WeatherWidget] Unhandled exception: {ex}");
        };

        app.Run();
    }

    /// <summary>
    /// Registers every concrete weather provider against the shared
    /// <see cref="IWeatherProvider"/> interface so the orchestrator can
    /// resolve them all as a collection.
    /// </summary>
    private static void RegisterProviders(IServiceCollection services,
        Dictionary<string, ProviderOptions> opts)
    {
        bool IsEnabled(string key) =>
            opts.TryGetValue(key, out var p) && p.Enabled;

        services.AddSingleton<IWeatherProvider, OpenMeteoProvider>();
        services.AddSingleton<IWeatherProvider, YrNoProvider>();
        services.AddSingleton<IWeatherProvider, BrightSkyProvider>();
        if (IsEnabled("OpenWeatherMap")) services.AddSingleton<IWeatherProvider, OpenWeatherMapProvider>();
        if (IsEnabled("WeatherApi")) services.AddSingleton<IWeatherProvider, WeatherApiProvider>();
        if (IsEnabled("Weatherbit")) services.AddSingleton<IWeatherProvider, WeatherbitProvider>();
        if (IsEnabled("VisualCrossing")) services.AddSingleton<IWeatherProvider, VisualCrossingProvider>();
        if (IsEnabled("TomorrowIo")) services.AddSingleton<IWeatherProvider, TomorrowIoProvider>();
        if (IsEnabled("Meteoblue")) services.AddSingleton<IWeatherProvider, MeteoblueProvider>();
        if (IsEnabled("Meteomatics")) services.AddSingleton<IWeatherProvider, MeteomaticsProvider>();
        if (IsEnabled("Knmi")) services.AddSingleton<IWeatherProvider, KnmiProvider>();
    }
}