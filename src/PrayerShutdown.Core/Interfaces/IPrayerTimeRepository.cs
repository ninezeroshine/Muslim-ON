using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Core.Interfaces;

public interface IPrayerTimeRepository
{
    Task<DailyPrayerTimes?> GetAsync(DateOnly date);
    Task SaveAsync(DailyPrayerTimes times);
    Task<IReadOnlyList<DailyPrayerTimes>> GetRangeAsync(DateOnly from, DateOnly to);
    Task PruneOldEntriesAsync(int keepDays = 60);
}
