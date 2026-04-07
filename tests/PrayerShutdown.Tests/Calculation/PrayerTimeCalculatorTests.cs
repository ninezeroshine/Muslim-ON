using Xunit;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Domain.Settings;
using PrayerShutdown.Services.Calculation;

namespace PrayerShutdown.Tests.Calculation;

public class PrayerTimeCalculatorTests
{
    private static readonly LocationInfo Kazan = new("Kazan", "Russia",
        new GeoCoordinate(55.7887, 49.1221), "Europe/Moscow");

    private static PrayerTimeCalculator Calc => new();

    [Fact]
    public void Kazan_MWL_Shafi_OrderIsCorrect()
    {
        var settings = new CalculationSettings
        {
            Method = CalculationMethod.MWL,
            AsrMethod = AsrJuristic.Shafi,
            HighLatRule = HighLatitudeRule.AngleBased
        };

        var result = Calc.Calculate(DateOnly.FromDateTime(DateTime.Today), Kazan, settings);

        foreach (var p in result.Prayers)
            Console.WriteLine($"{p.Name,-10} {p.Time:HH:mm}");

        var times = result.Prayers.Select(p => p.Time).ToList();
        for (int i = 1; i < times.Count; i++)
            Assert.True(times[i] > times[i - 1],
                $"{result.Prayers[i].Name} ({times[i]:HH:mm}) should be after {result.Prayers[i - 1].Name} ({times[i - 1]:HH:mm})");
    }

    [Fact]
    public void Kazan_Hanafi_AsrIsLater()
    {
        var shafi = new CalculationSettings { Method = CalculationMethod.MWL, AsrMethod = AsrJuristic.Shafi };
        var hanafi = new CalculationSettings { Method = CalculationMethod.MWL, AsrMethod = AsrJuristic.Hanafi };

        var date = DateOnly.FromDateTime(DateTime.Today);
        var asrShafi = Calc.Calculate(date, Kazan, shafi).GetPrayer(PrayerName.Asr)!;
        var asrHanafi = Calc.Calculate(date, Kazan, hanafi).GetPrayer(PrayerName.Asr)!;

        Console.WriteLine($"Asr Shafi:  {asrShafi.Time:HH:mm}");
        Console.WriteLine($"Asr Hanafi: {asrHanafi.Time:HH:mm}");

        Assert.True(asrHanafi.Time > asrShafi.Time,
            $"Hanafi Asr ({asrHanafi.Time:HH:mm}) should be after Shafi ({asrShafi.Time:HH:mm})");

        // Hanafi should be ~45-60 min later than Shafi
        var diff = (asrHanafi.Time - asrShafi.Time).TotalMinutes;
        Assert.InRange(diff, 30, 120);
    }

    [Fact]
    public void DifferentMethods_ProduceDifferentFajr()
    {
        var date = DateOnly.FromDateTime(DateTime.Today);
        var mwl = Calc.Calculate(date, Kazan, new() { Method = CalculationMethod.MWL });
        var isna = Calc.Calculate(date, Kazan, new() { Method = CalculationMethod.ISNA });

        var fajrMwl = mwl.GetPrayer(PrayerName.Fajr)!;
        var fajrIsna = isna.GetPrayer(PrayerName.Fajr)!;

        Console.WriteLine($"Fajr MWL:  {fajrMwl.Time:HH:mm}");
        Console.WriteLine($"Fajr ISNA: {fajrIsna.Time:HH:mm}");

        // MWL uses 18°, ISNA uses 15° — ISNA Fajr should be later
        Assert.True(fajrIsna.Time > fajrMwl.Time,
            $"ISNA Fajr ({fajrIsna.Time:HH:mm}) should be later than MWL ({fajrMwl.Time:HH:mm})");
    }
}
