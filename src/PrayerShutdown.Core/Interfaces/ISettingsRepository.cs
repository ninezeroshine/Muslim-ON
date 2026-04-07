using PrayerShutdown.Core.Domain.Settings;

namespace PrayerShutdown.Core.Interfaces;

public interface ISettingsRepository
{
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}
