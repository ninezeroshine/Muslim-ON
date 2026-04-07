using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Core.Interfaces;

public interface INotificationService
{
    Task ShowPrayerReminderAsync(PrayerTime prayer, int minutesBefore);
    Task ShowShutdownWarningAsync(PrayerTime prayer, int minutesUntilShutdown);
    void DismissAll();
}
