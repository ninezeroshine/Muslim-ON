using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Core.Interfaces;

public interface ISchedulerService
{
    Task InitializeAsync();
    void RecalculateSchedule();
    DailyPrayerTimes? TodaysPrayers { get; }
    PrayerTime? NextPrayer { get; }

    /// <summary>
    /// Forward-looking schedule for the upcoming prayer — Remind/Pray/Nudge/Shutdown
    /// clock times. Returns null when no more prayers today or location not set.
    /// </summary>
    NextPhasePlan? GetNextPhasePlan();

    /// <summary>Phase 1: reminder (rule.ReminderMinutesBefore before prayer time).</summary>
    event EventHandler<PrayerTime>? PrayerTimeApproaching;

    /// <summary>Phase 2: exact prayer time arrived.</summary>
    event EventHandler<PrayerTime>? PrayerTimeArrived;

    /// <summary>Phase 3: escalating nudges after prayer time, up to MaxSnoozeCount.</summary>
    event EventHandler<PrayerNudgeEventArgs>? PrayerNudge;

    /// <summary>
    /// Phase 4: shutdown is about to happen. Args carry the configured action
    /// (Shutdown/Sleep/Hibernate/Lock) so the UI can both label the countdown
    /// correctly and invoke the right <see cref="IShutdownService"/> method.
    /// </summary>
    event EventHandler<ShutdownTriggeredEventArgs>? ShutdownTriggered;

    /// <summary>
    /// Fired whenever a prayer is marked as prayed — from overlay, toast, or dashboard.
    /// Lets the dashboard keep its cards in sync when the action came from elsewhere.
    /// </summary>
    event EventHandler<PrayerTime>? PrayerMarkedAsPrayed;

    void MarkAsPrayed(PrayerTime prayer);
    void SnoozePrayer(PrayerTime prayer);

    /// <summary>
    /// Cancels the Phase 4 shutdown safety-net timer for the given prayer. Called
    /// by the UI when the overlay countdown reaches zero and the UI itself is firing
    /// <see cref="IShutdownService.Execute"/> — without this the safety net would
    /// fire a second shutdown call seconds later.
    /// </summary>
    void CancelShutdownSafety(PrayerName prayerName);

    /// <summary>
    /// Debug helper — manually fires a phase event with a synthetic PrayerTime (now).
    /// Does not touch prayed state or schedule real timers. Phase 4 fires the event only
    /// and does NOT call the real shutdown service.
    /// </summary>
    void TriggerTestPhase(TestPhase phase, PrayerName prayerName);
}
