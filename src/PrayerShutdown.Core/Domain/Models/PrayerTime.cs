using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Core.Domain.Models;

public sealed record PrayerTime(
    PrayerName Name,
    DateTime Time);
