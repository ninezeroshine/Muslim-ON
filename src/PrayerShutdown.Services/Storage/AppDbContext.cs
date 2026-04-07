using Microsoft.EntityFrameworkCore;
using PrayerShutdown.Common;

namespace PrayerShutdown.Services.Storage;

public sealed class AppDbContext : DbContext
{
    public DbSet<CachedPrayerTimesEntity> CachedPrayerTimes => Set<CachedPrayerTimesEntity>();
    public DbSet<SettingsEntity> Settings => Set<SettingsEntity>();
    public DbSet<ActionLogEntity> ActionLogs => Set<ActionLogEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var dbPath = Constants.DatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        options.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CachedPrayerTimesEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Date);
            e.Property(x => x.JsonPayload).IsRequired();
        });

        modelBuilder.Entity<SettingsEntity>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ActionLogEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Timestamp);
        });
    }
}

public sealed class CachedPrayerTimesEntity
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string LocationHash { get; set; } = string.Empty;
    public string MethodHash { get; set; } = string.Empty;
    public string JsonPayload { get; set; } = string.Empty;
}

public sealed class SettingsEntity
{
    public int Id { get; set; }
    public string Key { get; set; } = "AppSettings";
    public string JsonValue { get; set; } = "{}";
}

public sealed class ActionLogEntity
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Prayer { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
