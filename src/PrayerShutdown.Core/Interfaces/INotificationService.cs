using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Core.Interfaces;

/// <summary>
/// Windows Toast notifications. Soft, non-blocking alternative to the overlay window.
/// Toasts carry action buttons ("Помолился", "Иду молиться", "Отложить") whose clicks
/// are routed back through <see cref="ActionInvoked"/>.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Register the COM activator and subscribe to Windows App SDK events.
    /// Must be called once from the UI thread before any ShowXxxAsync.
    /// </summary>
    void Initialize();

    Task ShowReminderAsync(PrayerTime prayer, int minutesBefore);
    Task ShowPrayerNowAsync(PrayerTime prayer);
    Task ShowNudgeAsync(PrayerTime prayer, int nudgeNumber, int maxNudges);

    /// <summary>Dismiss all active Muslim ON toasts.</summary>
    void DismissAll();

    /// <summary>Dismiss only toasts for a specific prayer (e.g. when user marked it as prayed).</summary>
    void DismissFor(Core.Domain.Enums.PrayerName prayer);

    /// <summary>Fired on the UI dispatcher after the user clicks a toast body or action button.</summary>
    event EventHandler<NotificationInvokedEventArgs>? ActionInvoked;
}
