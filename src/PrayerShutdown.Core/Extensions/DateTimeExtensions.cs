namespace PrayerShutdown.Core.Extensions;

public static class DateTimeExtensions
{
    public static TimeSpan TimeUntil(this DateTime target)
    {
        var diff = target - DateTime.Now;
        return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
    }

    public static string ToCountdownString(this TimeSpan span)
    {
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m";

        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes}m {span.Seconds}s";

        return $"{span.Seconds}s";
    }

    public static bool IsToday(this DateOnly date)
        => date == DateOnly.FromDateTime(DateTime.Today);

    public static DateOnly ToDateOnly(this DateTime dt)
        => DateOnly.FromDateTime(dt);
}
