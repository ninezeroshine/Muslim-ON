using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Core.Interfaces;

public interface ISchedulerService
{
    Task InitializeAsync();
    void RecalculateSchedule();
    DailyPrayerTimes? TodaysPrayers { get; }
    PrayerTime? NextPrayer { get; }

    // Phase 1: reminder (default 15 min before prayer)
    event EventHandler<PrayerTime>? PrayerTimeApproaching;

    // Phase 2: exact prayer time arrived
    event EventHandler<PrayerTime>? PrayerTimeArrived;

    // Phase 3: escalating nudges after prayer time
    event EventHandler<PrayerNudgeEventArgs>? PrayerNudge;

    // Phase 4: shutdown triggered
    event EventHandler<PrayerTime>? ShutdownTriggered;

    void MarkAsPrayed(PrayerTime prayer);
    void SnoozePrayer(PrayerTime prayer);

    /// <summary>
    /// User clicked "Going to pray" — hides overlay but keeps nudge/shutdown timers active.
    /// </summary>
    void SetWaitingForPrayer(PrayerTime prayer);
}
