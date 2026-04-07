using Xunit;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Domain.Settings;
using PrayerShutdown.Services.Calculation;

namespace PrayerShutdown.Tests.Calculation;

public class PrayerTimeCalculatorTests
{
    [Fact]
    public void Calculate_Kazan_Today_PrintsTimes()
    {
        var kazan = new LocationInfo("Kazan", "Russia",
            new GeoCoordinate(55.7887, 49.1221), "Europe/Moscow");

        var settings = new CalculationSettings
        {
            Method = CalculationMethod.MWL,
            AsrMethod = AsrJuristic.Shafi,
            HighLatRule = HighLatitudeRule.AngleBased
        };

        var calc = new PrayerTimeCalculator();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var result = calc.Calculate(today, kazan, settings);

        // Print for debugging
        foreach (var p in result.Prayers)
        {
            Console.WriteLine($"{p.Name,-10} {p.Time:HH:mm}");
        }

        // Basic sanity: Fajr < Sunrise < Dhuhr < Asr < Maghrib < Isha
        var times = result.Prayers.Select(p => p.Time).ToList();
        for (int i = 1; i < times.Count; i++)
        {
            Assert.True(times[i] > times[i - 1],
                $"{result.Prayers[i].Name} ({times[i]:HH:mm}) should be after {result.Prayers[i - 1].Name} ({times[i - 1]:HH:mm})");
        }

        // Dhuhr should be around 12:00-13:30 for Kazan
        var dhuhr = result.GetPrayer(PrayerName.Dhuhr);
        Assert.NotNull(dhuhr);
        Assert.InRange(dhuhr.Time.Hour, 11, 13);
    }
}
