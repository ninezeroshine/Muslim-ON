namespace PrayerShutdown.Features.PrayerDashboard;

public sealed class TimelineDotModel
{
    public required string Name { get; init; }
    public required double Position { get; init; }
    public required bool IsPassed { get; init; }
    public required string TimeFormatted { get; init; }
}
