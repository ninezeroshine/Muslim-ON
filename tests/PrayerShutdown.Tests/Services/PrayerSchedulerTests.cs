using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Domain.Settings;
using PrayerShutdown.Core.Interfaces;
using PrayerShutdown.Services.Scheduling;
using Xunit;

namespace PrayerShutdown.Tests.Services;

public sealed class PrayerSchedulerTests
{
    private static PrayerScheduler Build(AppSettings settings, DailyPrayerTimes today)
    {
        var calc = new Mock<IPrayerTimeCalculator>();
        calc.Setup(c => c.Calculate(It.IsAny<DateOnly>(), It.IsAny<LocationInfo>(), It.IsAny<CalculationSettings>()))
            .Returns(today);

        var repo = new Mock<ISettingsRepository>();
        repo.Setup(r => r.LoadAsync()).ReturnsAsync(settings);

        var shutdown = new Mock<IShutdownService>();

        var actionLogger = new Mock<IActionLogger>();
        actionLogger.Setup(a => a.LogAsync(It.IsAny<ActionLogEntry>())).Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(actionLogger.Object);
        var sp = services.BuildServiceProvider();

        return new PrayerScheduler(
            calc.Object, repo.Object, shutdown.Object,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PrayerScheduler>.Instance);
    }

    private static readonly LocationInfo Moscow = new(
        "Moscow", "Russia", new GeoCoordinate(55.75, 37.61), "Europe/Moscow");

    private static AppSettings SettingsWith(params PrayerShutdownRule[] rules)
    {
        return new AppSettings
        {
            Location = new LocationSettings { SelectedLocation = Moscow },
            Shutdown = new ShutdownSettings { Rules = rules.ToList() },
        };
    }

    private static DailyPrayerTimes TodayWith(DateTime fajr, DateTime sunrise, DateTime dhuhr,
        DateTime asr, DateTime maghrib, DateTime isha)
    {
        return new DailyPrayerTimes(
            DateOnly.FromDateTime(DateTime.Today),
            Moscow,
            CalculationMethod.MWL,
            new[]
            {
                new PrayerTime(PrayerName.Fajr, fajr),
                new PrayerTime(PrayerName.Sunrise, sunrise),
                new PrayerTime(PrayerName.Dhuhr, dhuhr),
                new PrayerTime(PrayerName.Asr, asr),
                new PrayerTime(PrayerName.Maghrib, maghrib),
                new PrayerTime(PrayerName.Isha, isha),
            });
    }

    [Fact]
    public async Task GetNextPhasePlan_ReturnsNull_WhenNoPrayersLeftToday()
    {
        var past = DateTime.Now.AddHours(-1);
        var day = TodayWith(past.AddHours(-5), past.AddHours(-4), past.AddHours(-3),
                            past.AddHours(-2), past.AddHours(-1), past);
        var scheduler = Build(SettingsWith(), day);

        await scheduler.InitializeAsync();

        scheduler.GetNextPhasePlan().Should().BeNull();
    }

    [Fact]
    public async Task GetNextPhasePlan_ReturnsAllFourPhases_WhenShutdownEnabled()
    {
        var now = DateTime.Now;
        var future = now.AddHours(2);
        // Put all earlier prayers in the past so GetNextPrayer returns Maghrib.
        var day = TodayWith(now.AddHours(-10),
                            now.AddHours(-9),
                            now.AddHours(-6),
                            now.AddHours(-2),
                            future,
                            future.AddHours(2));

        var rule = new PrayerShutdownRule(PrayerName.Maghrib, IsEnabled: true,
            ReminderMinutesBefore: 10, ShutdownMinutesAfter: 20, Action: ShutdownAction.Sleep);
        var scheduler = Build(SettingsWith(rule), day);

        await scheduler.InitializeAsync();
        var plan = scheduler.GetNextPhasePlan();

        plan.Should().NotBeNull();
        plan!.Prayer.Name.Should().Be(PrayerName.Maghrib);
        plan.ShutdownEnabled.Should().BeTrue();
        plan.Action.Should().Be(ShutdownAction.Sleep);
        plan.RemindAt.Should().Be(future.AddMinutes(-10));
        plan.PrayAt.Should().Be(future);
        plan.ShutdownAt.Should().Be(future.AddMinutes(20));
    }

    [Fact]
    public async Task GetNextPhasePlan_OmitsRemindAndShutdown_WhenRuleDisabled()
    {
        var now = DateTime.Now;
        var future = now.AddHours(2);
        // Put all earlier prayers in the past so GetNextPrayer returns Maghrib.
        var day = TodayWith(now.AddHours(-10),
                            now.AddHours(-9),
                            now.AddHours(-6),
                            now.AddHours(-2),
                            future,
                            future.AddHours(2));

        var scheduler = Build(SettingsWith(
            new PrayerShutdownRule(PrayerName.Maghrib, IsEnabled: false)), day);

        await scheduler.InitializeAsync();
        var plan = scheduler.GetNextPhasePlan();

        plan.Should().NotBeNull();
        plan!.ShutdownEnabled.Should().BeFalse();
        plan.RemindAt.Should().BeNull();
        plan.NudgeAt.Should().BeNull();
        plan.ShutdownAt.Should().BeNull();
        plan.PrayAt.Should().Be(future);
    }

    [Fact]
    public async Task MarkAsPrayed_FiresEvent_AndPreventsFurtherPhases()
    {
        var future = DateTime.Now.AddSeconds(1); // will tick fast
        var day = TodayWith(DateTime.Today.AddHours(5),
                            DateTime.Today.AddHours(6),
                            DateTime.Today.AddHours(12),
                            DateTime.Today.AddHours(15),
                            future,
                            future.AddHours(2));
        var scheduler = Build(SettingsWith(
            new PrayerShutdownRule(PrayerName.Maghrib, IsEnabled: true)), day);

        await scheduler.InitializeAsync();

        var markedFired = false;
        scheduler.PrayerMarkedAsPrayed += (_, _) => markedFired = true;

        var maghrib = day.GetPrayer(PrayerName.Maghrib)!;
        scheduler.MarkAsPrayed(maghrib);

        markedFired.Should().BeTrue();
    }
}
