using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Interfaces;
using PrayerShutdown.Services.Storage;

namespace PrayerShutdown.Services.Logging;

public sealed class ActionLogger : IActionLogger
{
    private readonly AppDbContext _db;
    private readonly ILogger<ActionLogger> _logger;

    public ActionLogger(AppDbContext db, ILogger<ActionLogger> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(ActionLogEntry entry)
    {
        try
        {
            _db.ActionLogs.Add(new ActionLogEntity
            {
                Timestamp = entry.Timestamp,
                Prayer = entry.Prayer.ToString(),
                Event = entry.Event,
                Detail = entry.Detail
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log action: {Event}", entry.Event);
        }
    }

    public async Task<IReadOnlyList<ActionLogEntry>> GetRecentAsync(int count = 50)
    {
        var entities = await _db.ActionLogs
            .OrderByDescending(x => x.Timestamp)
            .Take(count)
            .ToListAsync();

        return entities.Select(e => new ActionLogEntry
        {
            Id = e.Id,
            Timestamp = e.Timestamp,
            Prayer = Enum.TryParse<Core.Domain.Enums.PrayerName>(e.Prayer, out var p)
                ? p : Core.Domain.Enums.PrayerName.Fajr,
            Event = e.Event,
            Detail = e.Detail
        }).ToList();
    }

    public async Task ClearAsync()
    {
        await _db.ActionLogs.ExecuteDeleteAsync();
    }

    public async Task PruneOldEntriesAsync(int keepDays = 90)
    {
        var cutoff = DateTime.Now.AddDays(-keepDays);
        await _db.ActionLogs
            .Where(x => x.Timestamp < cutoff)
            .ExecuteDeleteAsync();
    }
}
