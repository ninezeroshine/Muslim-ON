using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Core.Domain.Models;

public sealed record ActionLogEntry
{
    public int Id { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public PrayerName Prayer { get; init; }
    public string Event { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}
