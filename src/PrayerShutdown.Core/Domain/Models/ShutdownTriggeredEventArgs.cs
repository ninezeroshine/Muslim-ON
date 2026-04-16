using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Core.Domain.Models;

public sealed record ShutdownTriggeredEventArgs
{
    public required PrayerTime Prayer { get; init; }
    public required ShutdownAction Action { get; init; }
}
