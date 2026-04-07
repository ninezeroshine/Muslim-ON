using Microsoft.Extensions.Logging;
using PrayerShutdown.Common;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Domain.Settings;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Services.Scheduling;

public sealed class PrayerScheduler : ISchedulerService, IDisposable
{
    private readonly IPrayerTimeCalculator _calculator;
    private readonly ISettingsRepository _settingsRepo;
    private readonly ILogger<PrayerScheduler> _logger;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> _reminderTimers = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> _shutdownTimers = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, int> _snoozeCounts = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, bool> _prayedToday = new();
    private Timer? _midnightTimer;

    public DailyPrayerTimes? TodaysPrayers { get; private set; }
    public PrayerTime? NextPrayer => TodaysPrayers?.GetNextPrayer(DateTime.Now);

    public event EventHandler<PrayerTime>? PrayerTimeApproaching;
    public event EventHandler<PrayerTime>? PrayerTimeReached;
    public event EventHandler<PrayerTime>? ShutdownTriggered;

    public PrayerScheduler(
        IPrayerTimeCalculator calculator,
        ISettingsRepository settingsRepo,
        ILogger<PrayerScheduler> logger)
    {
        _calculator = calculator;
        _settingsRepo = settingsRepo;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing prayer scheduler");
        await RecalculateAsync();
        ScheduleMidnightRecalculation();
    }

    public void RecalculateSchedule()
    {
        _ = RecalculateAsync();
    }

    public void MarkAsPrayed(PrayerTime prayer)
    {
        _logger.LogInformation("Prayer marked as prayed: {Prayer}", prayer.Name);
        _prayedToday.TryAdd(prayer.Name, true);
        CancelTimerFor(prayer.Name);
    }

    public void SnoozePrayer(PrayerTime prayer)
    {
        _snoozeCounts.TryGetValue(prayer.Name, out int count);
        if (count >= Constants.MaxSnoozeCount)
        {
            _logger.LogWarning("Max snooze count reached for {Prayer}", prayer.Name);
            return;
        }

        _snoozeCounts[prayer.Name] = count + 1;
        _logger.LogInformation("Snoozed {Prayer} ({Count}/{Max})", prayer.Name, count + 1, Constants.MaxSnoozeCount);

        // Reschedule shutdown timer
        var delay = TimeSpan.FromMinutes(Constants.SnoozeMinutes);
        _shutdownTimers[prayer.Name]?.Dispose();
        _shutdownTimers[prayer.Name] = new Timer(
            _ => OnShutdownTriggered(prayer),
            null, delay, Timeout.InfiniteTimeSpan);
    }

    private async Task RecalculateAsync()
    {
        CancelAllTimers();
        _prayedToday.Clear();
        _snoozeCounts.Clear();

        var settings = await _settingsRepo.LoadAsync();
        var location = settings.Location.SelectedLocation;
        if (location is null)
        {
            _logger.LogWarning("No location configured, skipping schedule");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        TodaysPrayers = _calculator.Calculate(today, location, settings.Calculation);

        foreach (var rule in settings.Shutdown.Rules.Where(r => r.IsEnabled))
        {
            var prayer = TodaysPrayers.GetPrayer(rule.Prayer);
            if (prayer is null) continue;

            SchedulePrayer(prayer, rule);
        }

        _logger.LogInformation("Scheduled {Count} prayers for {Date}", TodaysPrayers.Prayers.Count, today);
    }

    private void SchedulePrayer(PrayerTime prayer, PrayerShutdownRule rule)
    {
        var now = DateTime.Now;

        // Reminder timer
        var reminderTime = prayer.Time.AddMinutes(-rule.ReminderMinutesBefore);
        if (reminderTime > now)
        {
            var delay = reminderTime - now;
            _reminderTimers[prayer.Name] = new Timer(
                _ => OnPrayerApproaching(prayer),
                null, delay, Timeout.InfiniteTimeSpan);
        }

        // Shutdown timer
        var shutdownTime = prayer.Time.AddMinutes(rule.ShutdownMinutesAfter);
        if (shutdownTime > now)
        {
            var delay = shutdownTime - now;
            _shutdownTimers[prayer.Name] = new Timer(
                _ => OnShutdownTriggered(prayer),
                null, delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnPrayerApproaching(PrayerTime prayer)
    {
        if (_prayedToday.ContainsKey(prayer.Name)) return;
        _logger.LogInformation("Prayer approaching: {Prayer}", prayer.Name);
        PrayerTimeApproaching?.Invoke(this, prayer);
    }

    private void OnShutdownTriggered(PrayerTime prayer)
    {
        if (_prayedToday.ContainsKey(prayer.Name)) return;
        _logger.LogWarning("Shutdown triggered for: {Prayer}", prayer.Name);
        ShutdownTriggered?.Invoke(this, prayer);
    }

    private void ScheduleMidnightRecalculation()
    {
        var now = DateTime.Now;
        var midnight = DateTime.Today.AddDays(1).AddSeconds(5);
        var delay = midnight - now;

        _midnightTimer?.Dispose();
        _midnightTimer = new Timer(_ =>
        {
            _logger.LogInformation("Midnight recalculation triggered");
            RecalculateSchedule();
            ScheduleMidnightRecalculation();
        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    private void CancelTimerFor(PrayerName name)
    {
        if (_reminderTimers.TryRemove(name, out var rt)) rt?.Dispose();
        if (_shutdownTimers.TryRemove(name, out var st)) st?.Dispose();
    }

    private void CancelAllTimers()
    {
        foreach (var t in _reminderTimers.Values) t?.Dispose();
        foreach (var t in _shutdownTimers.Values) t?.Dispose();
        _reminderTimers.Clear();
        _shutdownTimers.Clear();
    }

    public void Dispose()
    {
        CancelAllTimers();
        _midnightTimer?.Dispose();
    }
}
