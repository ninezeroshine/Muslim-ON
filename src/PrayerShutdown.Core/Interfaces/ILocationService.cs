using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Core.Interfaces;

public interface ILocationService
{
    Task<LocationInfo?> DetectCurrentLocationAsync();
    IReadOnlyList<LocationInfo> GetPresetCities();
    IReadOnlyList<LocationInfo> SearchCities(string query);
}
