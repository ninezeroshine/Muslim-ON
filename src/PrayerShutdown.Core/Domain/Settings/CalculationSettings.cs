using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Core.Domain.Settings;

public sealed class CalculationSettings
{
    public CalculationMethod Method { get; set; } = CalculationMethod.MWL;
    public AsrJuristic AsrMethod { get; set; } = AsrJuristic.Shafi;
    public HighLatitudeRule HighLatRule { get; set; } = HighLatitudeRule.AngleBased;
}
