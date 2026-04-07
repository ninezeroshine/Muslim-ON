using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Core.Interfaces;

public interface ISchedulerService
{
    Task InitializeAsync();
    void RecalculateSchedule();
    DailyPrayerTimes? TodaysPrayers { get; }
    PrayerTime? NextPrayer { get; }
    event EventHandler<PrayerTime>? PrayerTimeApproaching;
    event EventHandler<PrayerTime>? PrayerTimeReached;
    event EventHandler<PrayerTime>? ShutdownTriggered;
    void MarkAsPrayed(PrayerTime prayer);
    void SnoozePrayer(PrayerTime prayer);
}
