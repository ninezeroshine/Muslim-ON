using Microsoft.UI.Xaml.Data;
using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.UI.Converters;

public sealed class PrayerNameToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is PrayerName name)
        {
            return name switch
            {
                PrayerName.Fajr => "\uE706",      // Sunrise-like
                PrayerName.Sunrise => "\uE706",
                PrayerName.Dhuhr => "\uE706",      // Sun
                PrayerName.Asr => "\uE793",        // Afternoon
                PrayerName.Maghrib => "\uE706",     // Sunset-like
                PrayerName.Isha => "\uE708",        // Night/Moon
                _ => "\uE712"
            };
        }
        return "\uE712";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
