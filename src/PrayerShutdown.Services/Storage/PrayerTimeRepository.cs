using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Services.Storage;

public sealed class PrayerTimeRepository : IPrayerTimeRepository
{
    private readonly AppDbContext _db;

    public PrayerTimeRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DailyPrayerTimes?> GetAsync(DateOnly date)
    {
        var entity = await _db.CachedPrayerTimes
            .FirstOrDefaultAsync(x => x.Date == date);

        return entity is null ? null : Deserialize(entity.JsonPayload);
    }

    public async Task SaveAsync(DailyPrayerTimes times)
    {
        var existing = await _db.CachedPrayerTimes
            .FirstOrDefaultAsync(x => x.Date == times.Date);

        var json = JsonSerializer.Serialize(times);

        if (existing is not null)
        {
            existing.JsonPayload = json;
            existing.LocationHash = GetLocationHash(times.Location);
            existing.MethodHash = times.Method.ToString();
        }
        else
        {
            _db.CachedPrayerTimes.Add(new CachedPrayerTimesEntity
            {
                Date = times.Date,
                LocationHash = GetLocationHash(times.Location),
                MethodHash = times.Method.ToString(),
                JsonPayload = json
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<DailyPrayerTimes>> GetRangeAsync(DateOnly from, DateOnly to)
    {
        var entities = await _db.CachedPrayerTimes
            .Where(x => x.Date >= from && x.Date <= to)
            .OrderBy(x => x.Date)
            .ToListAsync();

        return entities
            .Select(e => Deserialize(e.JsonPayload))
            .Where(x => x is not null)
            .Cast<DailyPrayerTimes>()
            .ToList();
    }

    public async Task PruneOldEntriesAsync(int keepDays = 60)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.Today.AddDays(-keepDays));
        await _db.CachedPrayerTimes
            .Where(x => x.Date < cutoff)
            .ExecuteDeleteAsync();
    }

    private static string GetLocationHash(LocationInfo location)
        => $"{location.Coordinate.Latitude:F4},{location.Coordinate.Longitude:F4}";

    private static DailyPrayerTimes? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<DailyPrayerTimes>(json);
        }
        catch
        {
            return null;
        }
    }
}
