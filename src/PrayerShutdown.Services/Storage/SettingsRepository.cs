using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PrayerShutdown.Core.Domain.Settings;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Services.Storage;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _db;

    public SettingsRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AppSettings> LoadAsync()
    {
        await EnsureDatabaseCreatedAsync();

        var entity = await _db.Settings.FirstOrDefaultAsync(x => x.Key == "AppSettings");
        if (entity is null)
            return new AppSettings();

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(entity.JsonValue) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await EnsureDatabaseCreatedAsync();

        var json = JsonSerializer.Serialize(settings);
        var entity = await _db.Settings.FirstOrDefaultAsync(x => x.Key == "AppSettings");

        if (entity is not null)
        {
            entity.JsonValue = json;
        }
        else
        {
            _db.Settings.Add(new SettingsEntity
            {
                Key = "AppSettings",
                JsonValue = json
            });
        }

        await _db.SaveChangesAsync();
    }

    private static bool _dbCreated;

    private async Task EnsureDatabaseCreatedAsync()
    {
        if (_dbCreated) return;
        await _db.Database.EnsureCreatedAsync();
        _dbCreated = true;
    }
}
