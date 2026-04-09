namespace PrayerShutdown.Core.Domain.Enums;

/// <summary>
/// Debug-only phase selector used by <c>ISchedulerService.TriggerTestPhase</c>
/// to manually fire overlay events without waiting for real prayer timers.
/// </summary>
public enum TestPhase
{
    Remind,
    PrayNow,
    Nudge,
    Shutdown,
}
