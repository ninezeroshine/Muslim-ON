using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Core.Domain.Models;

public sealed record PrayerShutdownRule(
    PrayerName Prayer,
    bool IsEnabled,
    int ReminderMinutesBefore = 15,
    int ShutdownMinutesAfter = 15,
    ShutdownAction Action = ShutdownAction.Shutdown);
