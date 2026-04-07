using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Services.Calculation;

/// <summary>
/// Maps calculation methods to their specific angle parameters.
/// </summary>
public sealed record CalculationParams(
    double FajrAngle,
    double IshaAngle,
    double? MaghribAngle = null,
    int? IshaMinutesAfterMaghrib = null)
{
    public static CalculationParams ForMethod(CalculationMethod method) => method switch
    {
        CalculationMethod.MWL => new(18.0, 17.0),
        CalculationMethod.Egyptian => new(19.5, 17.5),
        CalculationMethod.Karachi => new(18.0, 18.0),
        CalculationMethod.UmmAlQura => new(18.5, 0, IshaMinutesAfterMaghrib: 90),
        CalculationMethod.ISNA => new(15.0, 15.0),
        CalculationMethod.MoonsightingCommittee => new(18.0, 18.0),
        CalculationMethod.Turkey => new(18.0, 17.0),
        CalculationMethod.Tehran => new(17.7, 14.0, MaghribAngle: 4.5),
        CalculationMethod.Custom => new(18.0, 17.0),
        _ => new(18.0, 17.0)
    };
}
