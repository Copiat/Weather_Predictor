using Microsoft.EntityFrameworkCore;
using WeatherWidget.Data.Models;

namespace WeatherWidget.Data;

/// <summary>
/// SQLite context backing the widget. <see cref="Program"/> calls
/// <c>Database.EnsureCreated()</c> on startup, so the schema is created
/// automatically the first time the app runs — no migrations needed.
/// </summary>
public sealed class WeatherDbContext : DbContext
{
    public WeatherDbContext(DbContextOptions<WeatherDbContext> options) : base(options) { }

    public DbSet<WeatherSnapshot> Snapshots => Set<WeatherSnapshot>();
    public DbSet<ActualTemperature> Actuals => Set<ActualTemperature>();
    public DbSet<ProviderStatusLog> ProviderStatuses => Set<ProviderStatusLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WeatherSnapshot>(e =>
        {
            e.HasIndex(x => new { x.Provider, x.ForecastDate });
            e.HasIndex(x => x.FetchedUtc);
        });

        b.Entity<ActualTemperature>(e =>
        {
            e.HasIndex(x => x.Date).IsUnique();
        });

        b.Entity<ProviderStatusLog>(e =>
        {
            e.HasIndex(x => new { x.Provider, x.CheckedUtc });
        });
    }
}
