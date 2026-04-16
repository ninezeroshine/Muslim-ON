namespace PrayerShutdown.Core.Domain.Enums;

/// <summary>
/// Action invoked by the user from a Windows Toast notification.
/// Mapped from toast "arguments" query string back to a structured enum.
/// </summary>
public enum NotificationAction
{
    None,
    Prayed,
    GoingToPray,
    Snooze,
    Dismiss,
    OpenApp,
}
