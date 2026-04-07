using Microsoft.Extensions.Logging;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Services.Location;

public sealed class LocationService : ILocationService
{
    private readonly ILogger<LocationService> _logger;

    public LocationService(ILogger<LocationService> logger)
    {
        _logger = logger;
    }

    public async Task<LocationInfo?> DetectCurrentLocationAsync()
    {
        try
        {
            var locator = new Windows.Devices.Geolocation.Geolocator();
            var access = await Windows.Devices.Geolocation.Geolocator.RequestAccessAsync();

            if (access != Windows.Devices.Geolocation.GeolocationAccessStatus.Allowed)
            {
                _logger.LogWarning("Geolocation access denied");
                return null;
            }

            var position = await locator.GetGeopositionAsync();
            var coord = new GeoCoordinate(
                position.Coordinate.Point.Position.Latitude,
                position.Coordinate.Point.Position.Longitude,
                position.Coordinate.Point.Position.Altitude);

            // Find nearest preset city
            return FindNearestCity(coord);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect location");
            return null;
        }
    }

    public IReadOnlyList<LocationInfo> GetPresetCities() => PresetCityProvider.Cities;

    public IReadOnlyList<LocationInfo> SearchCities(string query) => PresetCityProvider.Search(query);

    private static LocationInfo? FindNearestCity(GeoCoordinate coord)
    {
        LocationInfo? nearest = null;
        double minDist = double.MaxValue;

        foreach (var city in PresetCityProvider.Cities)
        {
            var dist = HaversineDistance(
                coord.Latitude, coord.Longitude,
                city.Coordinate.Latitude, city.Coordinate.Longitude);

            if (dist < minDist)
            {
                minDist = dist;
                nearest = city;
            }
        }

        // If nearest city is within 100km, use it; otherwise create custom entry
        if (nearest is not null && minDist < 100)
            return nearest;

        return new LocationInfo(
            $"{coord.Latitude:F2}, {coord.Longitude:F2}",
            "Detected",
            coord,
            TimeZoneInfo.Local.Id);
    }

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth radius in km
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
