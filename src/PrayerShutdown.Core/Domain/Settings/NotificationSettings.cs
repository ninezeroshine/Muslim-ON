namespace PrayerShutdown.Core.Domain.Settings;

public sealed class NotificationSettings
{
    public bool EnableToastNotifications { get; set; } = true;
    public bool EnableAdhanSound { get; set; }
    public string? AdhanSoundPath { get; set; }
    public int DefaultReminderMinutes { get; set; } = 15;
}
