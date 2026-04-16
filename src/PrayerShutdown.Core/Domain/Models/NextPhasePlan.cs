using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Core.Domain.Models;

/// <summary>
/// Forward-looking plan for a single prayer — the four moments the scheduler will
/// trigger if the user does nothing. Used by the dashboard "Next Steps" block so
/// the user can see at a glance what the app is about to do on their behalf.
/// Times are local clock-time (<see cref="DateTime.Now"/> coordinates), not timers.
/// </summary>
public sealed record NextPhasePlan
{
    public required PrayerTime Prayer { get; init; }
    public required DateTime? RemindAt { get; init; }
    public required DateTime PrayAt { get; init; }
    public required DateTime? NudgeAt { get; init; }
    public required DateTime? ShutdownAt { get; init; }
    public required ShutdownAction Action { get; init; }
    public required bool ShutdownEnabled { get; init; }
}
