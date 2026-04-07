using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Core.Domain.Settings;

public sealed class ShutdownSettings
{
    public List<PrayerShutdownRule> Rules { get; set; } = new()
    {
        new(PrayerName.Fajr, false),
        new(PrayerName.Dhuhr, true),
        new(PrayerName.Asr, true),
        new(PrayerName.Maghrib, true),
        new(PrayerName.Isha, false)
    };

    public ShutdownAction DefaultAction { get; set; } = ShutdownAction.Shutdown;
    public int DefaultShutdownDelayMinutes { get; set; } = 15;
}
