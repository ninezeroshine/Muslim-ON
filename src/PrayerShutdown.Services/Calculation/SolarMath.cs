namespace PrayerShutdown.Services.Calculation;

/// <summary>
/// Pure astronomical functions for solar position calculation.
/// Faithfully implements the PrayTimes.org algorithm (public domain).
/// All angles in degrees unless otherwise noted.
/// </summary>
public static class SolarMath
{
    public static double ToRadians(double deg) => deg * Math.PI / 180.0;
    public static double ToDegrees(double rad) => rad * 180.0 / Math.PI;

    public static double Dsin(double d) => Math.Sin(ToRadians(d));
    public static double Dcos(double d) => Math.Cos(ToRadians(d));
    public static double Dtan(double d) => Math.Tan(ToRadians(d));
    public static double Darcsin(double x) => ToDegrees(Math.Asin(x));
    public static double Darccos(double x) => ToDegrees(Math.Acos(Math.Clamp(x, -1.0, 1.0)));
    public static double Darctan2(double y, double x) => ToDegrees(Math.Atan2(y, x));

    /// <summary>
    /// Julian Day Number from Gregorian date.
    /// </summary>
    public static double JulianDate(int year, int month, int day)
    {
        if (month <= 2) { year--; month += 12; }
        double a = Math.Floor(year / 100.0);
        double b = 2 - a + Math.Floor(a / 4.0);
        return Math.Floor(365.25 * (year + 4716))
             + Math.Floor(30.6001 * (month + 1))
             + day + b - 1524.5;
    }

    /// <summary>
    /// Sun declination and equation of time for a Julian date.
    /// </summary>
    public static (double Declination, double Equation) SunPosition(double jd)
    {
        double D = jd - 2451545.0;
        double g = FixAngle(357.529 + 0.98560028 * D);
        double q = FixAngle(280.459 + 0.98564736 * D);
        double L = FixAngle(q + 1.915 * Dsin(g) + 0.020 * Dsin(2 * g));
        double e = 23.439 - 0.00000036 * D;
        double RA = Darctan2(Dcos(e) * Dsin(L), Dcos(L)) / 15.0;
        double eqt = q / 15.0 - FixHour(RA);
        double decl = Darcsin(Dsin(e) * Dsin(L));
        return (decl, eqt);
    }

    /// <summary>
    /// Solar noon in hours (local time).
    /// </summary>
    public static double MidDay(double eqt, double lng, double tz)
    {
        return FixHour(12 - eqt - lng / 15.0 + tz);
    }

    /// <summary>
    /// Hour angle for when the sun reaches a given angle below the horizon.
    /// Returns positive value in hours. NaN if the angle is never reached.
    /// </summary>
    public static double HourAngle(double angle, double lat, double decl)
    {
        double cosHA = (-Dsin(angle) - Dsin(lat) * Dsin(decl))
                     / (Dcos(lat) * Dcos(decl));
        if (cosHA < -1 || cosHA > 1)
            return double.NaN;
        return Darccos(cosHA) / 15.0;
    }

    /// <summary>
    /// Asr hour angle. shadowRatio: 1 for Shafi, 2 for Hanafi.
    /// </summary>
    public static double AsrHourAngle(int shadowRatio, double lat, double decl)
    {
        // Sun elevation angle when shadow = shadowRatio * object + noon shadow
        double elevation = ToDegrees(Math.Atan(1.0 / (shadowRatio + Dtan(Math.Abs(lat - decl)))));
        // HourAngle expects depression (negative elevation)
        return HourAngle(-elevation, lat, decl);
    }

    public static double FixAngle(double a) { a %= 360; return a < 0 ? a + 360 : a; }
    public static double FixHour(double h) { h %= 24; return h < 0 ? h + 24 : h; }

    public static TimeSpan HoursToTimeSpan(double hours)
    {
        if (double.IsNaN(hours) || double.IsInfinity(hours))
            return TimeSpan.Zero;
        hours = FixHour(hours);
        int h = (int)hours;
        double fractional = (hours - h) * 60;
        int m = (int)fractional;
        int s = (int)Math.Round((fractional - m) * 60);
        if (s >= 60) { m++; s = 0; }
        if (m >= 60) { h++; m = 0; }
        return new TimeSpan(h % 24, m, s);
    }
}
