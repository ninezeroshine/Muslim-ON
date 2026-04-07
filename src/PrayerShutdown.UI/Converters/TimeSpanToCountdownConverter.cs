using Microsoft.UI.Xaml.Data;
using PrayerShutdown.Core.Extensions;

namespace PrayerShutdown.UI.Converters;

public sealed class TimeSpanToCountdownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is TimeSpan span)
            return span.ToCountdownString();

        return "--:--";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
