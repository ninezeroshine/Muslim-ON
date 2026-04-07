using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Services.Location;

public static class PresetCityProvider
{
    private static readonly Lazy<IReadOnlyList<LocationInfo>> _cities = new(BuildCityList);

    public static IReadOnlyList<LocationInfo> Cities => _cities.Value;

    public static IReadOnlyList<LocationInfo> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Cities;

        var q = query.Trim().ToLowerInvariant();
        return Cities.Where(c =>
            c.CityName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            c.Country.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            (c.CityNameRu?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (c.CityNameAr?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
        ).ToList();
    }

    private static IReadOnlyList<LocationInfo> BuildCityList() =>
    [
        City("Kazan", "Russia", 55.7887, 49.1221, "Europe/Moscow", "Казань", "قازان"),
        City("Moscow", "Russia", 55.7558, 37.6173, "Europe/Moscow", "Москва", "موسكو"),
        City("Saint Petersburg", "Russia", 59.9343, 30.3351, "Europe/Moscow", "Санкт-Петербург", "سانت بطرسبرغ"),
        City("Ufa", "Russia", 54.7388, 55.9721, "Asia/Yekaterinburg", "Уфа", "أوفا"),
        City("Grozny", "Russia", 43.3187, 45.6919, "Europe/Moscow", "Грозный", "غروزني"),
        City("Makhachkala", "Russia", 42.9849, 47.5047, "Europe/Moscow", "Махачкала", "محج قلعة"),
        City("Novosibirsk", "Russia", 55.0084, 82.9357, "Asia/Novosibirsk", "Новосибирск", "نوفوسيبيرسك"),
        City("Yekaterinburg", "Russia", 56.8389, 60.6057, "Asia/Yekaterinburg", "Екатеринбург", "يكاترينبورغ"),

        City("Makkah", "Saudi Arabia", 21.4225, 39.8262, "Asia/Riyadh", "Мекка", "مكة المكرمة"),
        City("Madinah", "Saudi Arabia", 24.4539, 39.6142, "Asia/Riyadh", "Медина", "المدينة المنورة"),
        City("Riyadh", "Saudi Arabia", 24.7136, 46.6753, "Asia/Riyadh", "Эр-Рияд", "الرياض"),
        City("Jeddah", "Saudi Arabia", 21.4858, 39.1925, "Asia/Riyadh", "Джидда", "جدة"),

        City("Istanbul", "Turkey", 41.0082, 28.9784, "Europe/Istanbul", "Стамбул", "اسطنبول"),
        City("Ankara", "Turkey", 39.9334, 32.8597, "Europe/Istanbul", "Анкара", "أنقرة"),

        City("Cairo", "Egypt", 30.0444, 31.2357, "Africa/Cairo", "Каир", "القاهرة"),
        City("Dubai", "UAE", 25.2048, 55.2708, "Asia/Dubai", "Дубай", "دبي"),
        City("Doha", "Qatar", 25.2854, 51.5310, "Asia/Qatar", "Доха", "الدوحة"),
        City("Kuwait City", "Kuwait", 29.3759, 47.9774, "Asia/Kuwait", "Эль-Кувейт", "مدينة الكويت"),
        City("Amman", "Jordan", 31.9454, 35.9284, "Asia/Amman", "Амман", "عمّان"),
        City("Baghdad", "Iraq", 33.3152, 44.3661, "Asia/Baghdad", "Багдад", "بغداد"),
        City("Tehran", "Iran", 35.6892, 51.3890, "Asia/Tehran", "Тегеран", "طهران"),

        City("Islamabad", "Pakistan", 33.6844, 73.0479, "Asia/Karachi", "Исламабад", "إسلام أباد"),
        City("Karachi", "Pakistan", 24.8607, 67.0011, "Asia/Karachi", "Карачи", "كراتشي"),
        City("Lahore", "Pakistan", 31.5204, 74.3587, "Asia/Karachi", "Лахор", "لاهور"),

        City("Jakarta", "Indonesia", -6.2088, 106.8456, "Asia/Jakarta", "Джакарта", "جاكرتا"),
        City("Kuala Lumpur", "Malaysia", 3.1390, 101.6869, "Asia/Kuala_Lumpur", "Куала-Лумпур", "كوالالمبور"),

        City("London", "United Kingdom", 51.5074, -0.1278, "Europe/London", "Лондон", "لندن"),
        City("Paris", "France", 48.8566, 2.3522, "Europe/Paris", "Париж", "باريس"),
        City("Berlin", "Germany", 52.5200, 13.4050, "Europe/Berlin", "Берлин", "برلين"),
        City("New York", "United States", 40.7128, -74.0060, "America/New_York", "Нью-Йорк", "نيويورك"),
        City("Toronto", "Canada", 43.6532, -79.3832, "America/Toronto", "Торонто", "تورنتو"),
    ];

    private static LocationInfo City(
        string name, string country, double lat, double lon, string tz,
        string? nameRu = null, string? nameAr = null)
        => new(name, country, new GeoCoordinate(lat, lon), tz, nameRu, nameAr);
}
