namespace PrayerShutdown.Core.Domain.Models;

public sealed record LocationInfo(
    string CityName,
    string Country,
    GeoCoordinate Coordinate,
    string TimeZoneId,
    string? CityNameRu = null,
    string? CityNameAr = null);
