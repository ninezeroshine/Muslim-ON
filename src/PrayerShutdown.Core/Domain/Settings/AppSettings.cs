namespace PrayerShutdown.Core.Domain.Settings;

public sealed class AppSettings
{
    public LocationSettings Location { get; set; } = new();
    public CalculationSettings Calculation { get; set; } = new();
    public NotificationSettings Notification { get; set; } = new();
    public ShutdownSettings Shutdown { get; set; } = new();
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "Default";
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool IsOnboardingCompleted { get; set; }
    public WorkDaySettings WorkDay { get; set; } = new();
}
