using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Core.Domain.Settings;

public sealed class LocationSettings
{
    public LocationInfo? SelectedLocation { get; set; }
    public bool UseAutoDetect { get; set; }
}
