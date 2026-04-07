using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Domain.Settings;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Services.Calculation;

/// <summary>
/// Calculates Islamic prayer times using the PrayTimes.org astronomical algorithm.
/// All times returned in local time for the specified timezone.
/// </summary>
public sealed class PrayerTimeCalculator : IPrayerTimeCalculator
{
    public DailyPrayerTimes Calculate(DateOnly date, LocationInfo location, CalculationSettings settings)
    {
        var param = CalculationParams.ForMethod(settings.Method);
        double lat = location.Coordinate.Latitude;
        double lng = location.Coordinate.Longitude;
        double tz = GetTimezoneOffset(location.TimeZoneId, date);

        double jd = SolarMath.JulianDate(date.Year, date.Month, date.Day);
        var (decl, eqt) = SolarMath.SunPosition(jd);

        double noon = SolarMath.MidDay(eqt, lng, tz);

        // Sunrise & Sunset: sun center at -0.8333 deg (refraction + solar disc radius)
        double sunHA = SolarMath.HourAngle(0.8333, lat, decl);
        double sunrise = noon - sunHA;
        double sunset = noon + sunHA;

        // Fajr: sun at fajrAngle below horizon
        double fajrHA = SolarMath.HourAngle(param.FajrAngle, lat, decl);
        double fajr = noon - fajrHA;

        // Dhuhr: solar noon + 1 min safety
        double dhuhr = noon + 1.0 / 60.0;

        // Asr: shadow ratio method
        int shadowRatio = (int)settings.AsrMethod;
        double asrHA = SolarMath.AsrHourAngle(shadowRatio, lat, decl);
        double asr = noon + asrHA;

        // Maghrib: at sunset, or custom angle
        double maghrib;
        if (param.MaghribAngle.HasValue)
        {
            double mHA = SolarMath.HourAngle(param.MaghribAngle.Value, lat, decl);
            maghrib = noon + mHA;
        }
        else
        {
            maghrib = sunset;
        }

        // Isha: angle-based or minutes after maghrib
        double isha;
        if (param.IshaMinutesAfterMaghrib.HasValue)
        {
            isha = maghrib + param.IshaMinutesAfterMaghrib.Value / 60.0;
        }
        else
        {
            double ishaHA = SolarMath.HourAngle(param.IshaAngle, lat, decl);
            isha = noon + ishaHA;
        }

        // High latitude adjustment for Fajr/Isha if they couldn't be calculated
        (fajr, isha) = HighLatitudeAdjuster.Adjust(
            settings.HighLatRule,
            fajr, isha,
            sunrise, sunset,
            param.FajrAngle,
            param.IshaAngle);

        var baseDate = date.ToDateTime(TimeOnly.MinValue);
        var prayers = new List<PrayerTime>
        {
            new(PrayerName.Fajr, ToLocal(baseDate, fajr)),
            new(PrayerName.Sunrise, ToLocal(baseDate, sunrise)),
            new(PrayerName.Dhuhr, ToLocal(baseDate, dhuhr)),
            new(PrayerName.Asr, ToLocal(baseDate, asr)),
            new(PrayerName.Maghrib, ToLocal(baseDate, maghrib)),
            new(PrayerName.Isha, ToLocal(baseDate, isha))
        };

        return new DailyPrayerTimes(date, location, settings.Method, prayers);
    }

    public IReadOnlyList<DailyPrayerTimes> CalculateMonth(DateOnly startDate, LocationInfo location, CalculationSettings settings)
    {
        var results = new List<DailyPrayerTimes>(30);
        for (int i = 0; i < 30; i++)
            results.Add(Calculate(startDate.AddDays(i), location, settings));
        return results;
    }

    private static DateTime ToLocal(DateTime baseDate, double hours)
        => baseDate.Add(SolarMath.HoursToTimeSpan(hours));

    private static double GetTimezoneOffset(string timeZoneId, DateOnly date)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var dt = date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)));
            return tz.GetUtcOffset(dt).TotalHours;
        }
        catch
        {
            return 0;
        }
    }
}
