using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Core.Domain.Models;

public sealed record DailyPrayerTimes(
    DateOnly Date,
    LocationInfo Location,
    CalculationMethod Method,
    IReadOnlyList<PrayerTime> Prayers)
{
    public PrayerTime? GetNextPrayer(DateTime now)
    {
        return Prayers.FirstOrDefault(p =>
            p.Name != PrayerName.Sunrise && p.Time > now);
    }

    public PrayerTime? GetPrayer(PrayerName name)
    {
        return Prayers.FirstOrDefault(p => p.Name == name);
    }
}
