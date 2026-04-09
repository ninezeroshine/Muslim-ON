using PrayerShutdown.Core.Domain.Enums;
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

    /// <summary>
    /// Fired whenever a prayer is marked as prayed — from overlay, dashboard, or any other source.
    /// Lets the dashboard keep its cards in sync when the overlay is used.
    /// </summary>
    event EventHandler<PrayerTime>? PrayerMarkedAsPrayed;

    void MarkAsPrayed(PrayerTime prayer);
    void SnoozePrayer(PrayerTime prayer);

    /// <summary>
    /// User clicked "Going to pray" — hides overlay but keeps nudge/shutdown timers active.
    /// </summary>
    void SetWaitingForPrayer(PrayerTime prayer);

    /// <summary>
    /// Debug helper — manually fires a phase event with a synthetic PrayerTime (now).
    /// Does not touch prayed state or schedule real timers. Phase 4 fires the event only
    /// and does NOT call the real shutdown service.
    /// </summary>
    void TriggerTestPhase(TestPhase phase, PrayerName prayerName);
}
