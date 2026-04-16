using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Core.Domain.Models;

public sealed record NotificationInvokedEventArgs
{
    public required NotificationAction Action { get; init; }
    public required PrayerName Prayer { get; init; }
}
