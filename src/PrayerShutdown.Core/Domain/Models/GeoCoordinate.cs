namespace PrayerShutdown.Core.Domain.Models;

public sealed record GeoCoordinate(
    double Latitude,
    double Longitude,
    double Elevation = 0);
