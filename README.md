# Schiphol Smart Weather Widget

Schiphol Smart Weather is a highly optimized, native desktop widget that aggregates, persists, and analyzes data across 11 different weather forecasting services to compute a consolidated "smart" weather prediction. 

Built using **Photino.Blazor**, this application delivers a lightweight web-style UI (HTML/CSS/Razor components) rendered inside a highly efficient, chromeless native OS window canvas instead of a heavy framework like Electron.

## Features

- **Glass-morphic Desktop Widget:** Native Win32 window initialization that is borderless (chromeless), transparent, and forced always-on-top [Program.cs].
- **Multi-Provider Aggregation:** Fans out parallel asynchronous API requests to up to 11 different weather services [WeatherOrchestrator.cs]:
  - Always active: OpenMeteo, Yr.no, BrightSky [Program.cs]
  - Configurable: OpenWeatherMap, WeatherAPI, Weatherbit, VisualCrossing, Tomorrow.io, Meteoblue, Meteomatics, and KNMI [Program.cs]
- **Smart Prediction Engine:** Automatically executes localized prediction algorithms over multiple aggregated provider data snapshots to evaluate the most accurate current outlook [WeatherOrchestrator.cs].
- **Local SQLite Persistence:** Fully offline-first historical tracking using Entity Framework Core (EF Core) to log provider statuses, raw responses, and high-temperature trends [Program.cs, WeatherOrchestrator.cs].
- **Enterprise Network Ready:** Built-in configurable network proxy mapping directly within the app bootstrap configuration [Program.cs].

## Architecture & Technology Stack

- **Runtime:** .NET 8.0 (Windows GUI Executable / `WinExe`) [WeatherWidget.csproj]
- **UI Shell:** [Photino.Blazor (v3.1.0)](https://www.tryphotino.io/) for cross-platform native lightweight rendering [WeatherWidget.csproj].
- **Database Layer:** Entity Framework Core & SQLite (v8.0.8) [WeatherWidget.csproj].
- **Interoperability:** Native Win32 API P/Invokes to seamlessly set window dimensions and enforce topmost behavior directly on the OS window handle (`HWND`) [Program.cs].

## Configuration

The widget relies on an `appsettings.json` deployment file located in the application execution directory [Program.cs, WeatherWidget.csproj].

```json
{
  "Proxy": {
    "Enabled": false,
    "Address": ""
  },
  "WeatherWidget": {
    "Database": {
      "Path": "weatherwidget.db"
    },
    "Location": {
      "Latitude": 52.3105,
      "Longitude": 4.7683
    },
    "Polling": {
      "RunImmediatelyOnStart": true,
      "IntervalMinutes": 60
    }
  },
  "WeatherProviders": {
    "OpenWeatherMap": { "Enabled": true },
    "WeatherApi": { "Enabled": false }
    // Add configurations for remaining optional providers here
  }
}
