using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Core.Interfaces;

public interface IActionLogger
{
    Task LogAsync(ActionLogEntry entry);
    Task<IReadOnlyList<ActionLogEntry>> GetRecentAsync(int count = 50);
    Task ClearAsync();
    Task PruneOldEntriesAsync(int keepDays = 90);
}
