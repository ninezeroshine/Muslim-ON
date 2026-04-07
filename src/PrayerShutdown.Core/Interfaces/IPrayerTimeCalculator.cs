using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Domain.Settings;

namespace PrayerShutdown.Core.Interfaces;

public interface IPrayerTimeCalculator
{
    DailyPrayerTimes Calculate(DateOnly date, LocationInfo location, CalculationSettings settings);
    IReadOnlyList<DailyPrayerTimes> CalculateMonth(DateOnly startDate, LocationInfo location, CalculationSettings settings);
}
