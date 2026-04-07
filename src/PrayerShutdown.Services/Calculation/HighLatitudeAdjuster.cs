using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Services.Calculation;

/// <summary>
/// Adjusts prayer times for high latitudes where Fajr/Isha
/// angles may not occur (e.g., Kazan at 55.8N in summer).
/// </summary>
public static class HighLatitudeAdjuster
{
    /// <summary>
    /// Adjust Fajr and Isha times that couldn't be calculated
    /// (returned NaN from SunAngleTime).
    /// </summary>
    public static (double fajrHours, double ishaHours) Adjust(
        HighLatitudeRule rule,
        double fajrHours,
        double ishaHours,
        double sunriseHours,
        double sunsetHours,
        double fajrAngle,
        double ishaAngle)
    {
        double nightHours = NightPortion(sunriseHours, sunsetHours);

        if (double.IsNaN(fajrHours))
        {
            double portion = PortionForAngle(rule, fajrAngle, nightHours);
            fajrHours = sunriseHours - portion;
        }

        if (double.IsNaN(ishaHours))
        {
            double portion = PortionForAngle(rule, ishaAngle, nightHours);
            ishaHours = sunsetHours + portion;
        }

        return (fajrHours, ishaHours);
    }

    private static double NightPortion(double sunriseHours, double sunsetHours)
    {
        // Night duration = time from sunset to next sunrise
        double night = sunriseHours + 24 - sunsetHours;
        if (night > 24) night -= 24;
        return night;
    }

    private static double PortionForAngle(HighLatitudeRule rule, double angle, double nightHours)
    {
        return rule switch
        {
            HighLatitudeRule.NightMiddle => nightHours / 2.0,
            HighLatitudeRule.OneSeventh => nightHours / 7.0,
            HighLatitudeRule.AngleBased => nightHours * angle / 60.0,
            _ => nightHours / 2.0
        };
    }
}
