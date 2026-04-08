namespace PrayerShutdown.Core.Domain.Models;

public sealed class PrayerNudgeEventArgs : EventArgs
{
    public required PrayerTime Prayer { get; init; }
    public int NudgeNumber { get; init; }
    public int MaxNudges { get; init; }
    public bool IsLastChance => NudgeNumber >= MaxNudges;
}
