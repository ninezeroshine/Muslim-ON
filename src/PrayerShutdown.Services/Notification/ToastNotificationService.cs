using Microsoft.Extensions.Logging;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Services.Notification;

public sealed class ToastNotificationService : INotificationService
{
    private readonly ILogger<ToastNotificationService> _logger;

    public ToastNotificationService(ILogger<ToastNotificationService> logger)
    {
        _logger = logger;
    }

    public Task ShowPrayerReminderAsync(PrayerTime prayer, int minutesBefore)
    {
        _logger.LogInformation(
            "Prayer reminder: {Prayer} in {Minutes} minutes",
            prayer.Name, minutesBefore);

        // TODO: Implement Windows App SDK toast notifications
        // For now, log the reminder
        return Task.CompletedTask;
    }

    public Task ShowShutdownWarningAsync(PrayerTime prayer, int minutesUntilShutdown)
    {
        _logger.LogWarning(
            "Shutdown warning: {Prayer} — PC will shut down in {Minutes} minutes",
            prayer.Name, minutesUntilShutdown);

        return Task.CompletedTask;
    }

    public void DismissAll()
    {
        _logger.LogInformation("All notifications dismissed");
    }
}
