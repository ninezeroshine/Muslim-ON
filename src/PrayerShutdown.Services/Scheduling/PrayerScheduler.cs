using Microsoft.Extensions.Logging;
using PrayerShutdown.Common;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Services.Scheduling;

public sealed class PrayerScheduler : ISchedulerService, IDisposable
{
    private readonly IPrayerTimeCalculator _calculator;
    private readonly ISettingsRepository _settingsRepo;
    private readonly IShutdownService _shutdownService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PrayerScheduler> _logger;

    // Phase 1: reminder timers (X min before prayer)
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> _reminderTimers = new();
    // Phase 2: prayer time arrived timers
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> _prayerTimeTimers = new();
    // Phase 3: nudge timers (escalating after prayer)
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> _nudgeTimers = new();
    // Phase 4: shutdown timers
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> _shutdownTimers = new();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, int> _snoozeCounts = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, bool> _prayedToday = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, bool> _waitingForPrayer = new();
    private Timer? _midnightTimer;

    public DailyPrayerTimes? TodaysPrayers { get; private set; }
    public PrayerTime? NextPrayer => TodaysPrayers?.GetNextPrayer(DateTime.Now);

    // Phase events
    public event EventHandler<PrayerTime>? PrayerTimeApproaching;
    public event EventHandler<PrayerTime>? PrayerTimeArrived;
    public event EventHandler<PrayerNudgeEventArgs>? PrayerNudge;
    public event EventHandler<PrayerTime>? ShutdownTriggered;

    public PrayerScheduler(
        IPrayerTimeCalculator calculator,
        ISettingsRepository settingsRepo,
        IShutdownService shutdownService,
        INotificationService notificationService,
        ILogger<PrayerScheduler> logger)
    {
        _calculator = calculator;
        _settingsRepo = settingsRepo;
        _shutdownService = shutdownService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing prayer scheduler");
        await RecalculateAsync();
        ScheduleMidnightRecalculation();
    }

    public void RecalculateSchedule() => _ = RecalculateAsync();

    public void MarkAsPrayed(PrayerTime prayer)
    {
        _logger.LogInformation("Prayer marked as prayed: {Prayer}", prayer.Name);
        _prayedToday[prayer.Name] = true;
        _waitingForPrayer.TryRemove(prayer.Name, out _);
        CancelAllTimersFor(prayer.Name);

        if (_shutdownService.HasPendingShutdown)
            _shutdownService.CancelPendingShutdown();
    }

    public void SetWaitingForPrayer(PrayerTime prayer)
    {
        _logger.LogInformation("User going to pray: {Prayer}", prayer.Name);
        _waitingForPrayer[prayer.Name] = true;
        // Don't cancel nudge/shutdown timers — user might not come back
    }

    public void SnoozePrayer(PrayerTime prayer)
    {
        _snoozeCounts.TryGetValue(prayer.Name, out int count);
        if (count >= Constants.MaxSnoozeCount)
        {
            _logger.LogWarning("Max snooze count reached for {Prayer}, triggering shutdown", prayer.Name);
            OnShutdownTriggered(prayer);
            return;
        }

        _snoozeCounts[prayer.Name] = count + 1;
        _logger.LogInformation("Snoozed {Prayer} ({Count}/{Max})", prayer.Name, count + 1, Constants.MaxSnoozeCount);

        if (_shutdownService.HasPendingShutdown)
            _shutdownService.CancelPendingShutdown();

        // Reschedule next nudge
        DisposeTimer(_nudgeTimers, prayer.Name);
        var delay = TimeSpan.FromMinutes(Constants.NudgeIntervalMinutes);
        _nudgeTimers[prayer.Name] = new Timer(
            _ => OnPrayerNudge(prayer, count + 2), // next nudge number (1-indexed, count+1 already happened)
            null, delay, Timeout.InfiniteTimeSpan);

        // Also reschedule shutdown after the nudge interval
        DisposeTimer(_shutdownTimers, prayer.Name);
        var shutdownDelay = TimeSpan.FromMinutes(Constants.NudgeIntervalMinutes * 2);
        _shutdownTimers[prayer.Name] = new Timer(
            _ => OnShutdownTriggered(prayer),
            null, shutdownDelay, Timeout.InfiniteTimeSpan);
    }

    private async Task RecalculateAsync()
    {
        try
        {
            CancelAllTimers();
            _prayedToday.Clear();
            _snoozeCounts.Clear();
            _waitingForPrayer.Clear();

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

            _logger.LogInformation("Scheduled {Count} shutdown-enabled prayers for {Date}",
                settings.Shutdown.Rules.Count(r => r.IsEnabled), today);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recalculate prayer schedule");
        }
    }

    private void SchedulePrayer(PrayerTime prayer, PrayerShutdownRule rule)
    {
        var now = DateTime.Now;

        // Phase 1: Reminder (X minutes before prayer)
        var reminderTime = prayer.Time.AddMinutes(-rule.ReminderMinutesBefore);
        if (reminderTime > now)
        {
            var delay = reminderTime - now;
            _reminderTimers[prayer.Name] = new Timer(
                _ => OnPrayerApproaching(prayer, rule.ReminderMinutesBefore),
                null, delay, Timeout.InfiniteTimeSpan);
            _logger.LogDebug("Phase 1 (Reminder) for {Prayer} in {Delay}", prayer.Name, delay);
        }

        // Phase 2: Prayer time arrived
        if (prayer.Time > now)
        {
            var delay = prayer.Time - now;
            _prayerTimeTimers[prayer.Name] = new Timer(
                _ => OnPrayerTimeArrived(prayer),
                null, delay, Timeout.InfiniteTimeSpan);
            _logger.LogDebug("Phase 2 (PrayNow) for {Prayer} in {Delay}", prayer.Name, delay);
        }

        // Phase 3: First nudge (5 min after prayer)
        var firstNudgeTime = prayer.Time.AddMinutes(Constants.NudgeIntervalMinutes);
        if (firstNudgeTime > now)
        {
            var delay = firstNudgeTime - now;
            _nudgeTimers[prayer.Name] = new Timer(
                _ => OnPrayerNudge(prayer, 1),
                null, delay, Timeout.InfiniteTimeSpan);
            _logger.LogDebug("Phase 3 (Nudge #1) for {Prayer} in {Delay}", prayer.Name, delay);
        }

        // Phase 4: Shutdown (ShutdownMinutesAfter after prayer)
        var shutdownTime = prayer.Time.AddMinutes(rule.ShutdownMinutesAfter);
        if (shutdownTime > now)
        {
            var delay = shutdownTime - now;
            _shutdownTimers[prayer.Name] = new Timer(
                _ => OnShutdownTriggered(prayer),
                null, delay, Timeout.InfiniteTimeSpan);
            _logger.LogDebug("Phase 4 (Shutdown) for {Prayer} in {Delay}", prayer.Name, delay);
        }
    }

    // ── Phase 1: Gentle Reminder ──
    private void OnPrayerApproaching(PrayerTime prayer, int minutesBefore)
    {
        if (_prayedToday.ContainsKey(prayer.Name)) return;
        _logger.LogInformation("Phase 1 — Prayer approaching: {Prayer} in {Min} min", prayer.Name, minutesBefore);

        PrayerTimeApproaching?.Invoke(this, prayer);
        _ = _notificationService.ShowPrayerReminderAsync(prayer, minutesBefore);
    }

    // ── Phase 2: Prayer Time Arrived ──
    private void OnPrayerTimeArrived(PrayerTime prayer)
    {
        if (_prayedToday.ContainsKey(prayer.Name)) return;
        _logger.LogInformation("Phase 2 — Prayer time arrived: {Prayer}", prayer.Name);

        PrayerTimeArrived?.Invoke(this, prayer);
    }

    // ── Phase 3: Escalating Nudge ──
    private void OnPrayerNudge(PrayerTime prayer, int nudgeNumber)
    {
        if (_prayedToday.ContainsKey(prayer.Name)) return;

        var maxNudges = Constants.MaxSnoozeCount;
        _logger.LogWarning("Phase 3 — Nudge #{Number}/{Max} for {Prayer}", nudgeNumber, maxNudges, prayer.Name);

        PrayerNudge?.Invoke(this, new PrayerNudgeEventArgs
        {
            Prayer = prayer,
            NudgeNumber = nudgeNumber,
            MaxNudges = maxNudges,
        });
    }

    // ── Phase 4: Shutdown ──
    // NOTE: Only fires event. Actual shutdown execution moved to App.xaml.cs (overlay finish handler)
    // to avoid race condition where shutdown starts before overlay appears.
    private void OnShutdownTriggered(PrayerTime prayer)
    {
        if (_prayedToday.ContainsKey(prayer.Name)) return;
        _logger.LogWarning("Phase 4 — SHUTDOWN TRIGGERED for: {Prayer}", prayer.Name);

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

    private static void DisposeTimer(
        System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> dict,
        PrayerName name)
    {
        if (dict.TryRemove(name, out var timer)) timer?.Dispose();
    }

    private void CancelAllTimersFor(PrayerName name)
    {
        DisposeTimer(_reminderTimers, name);
        DisposeTimer(_prayerTimeTimers, name);
        DisposeTimer(_nudgeTimers, name);
        DisposeTimer(_shutdownTimers, name);
    }

    private void CancelAllTimers()
    {
        DisposeAll(_reminderTimers);
        DisposeAll(_prayerTimeTimers);
        DisposeAll(_nudgeTimers);
        DisposeAll(_shutdownTimers);
    }

    private static void DisposeAll(System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> dict)
    {
        foreach (var t in dict.Values) t?.Dispose();
        dict.Clear();
    }

    public void Dispose()
    {
        CancelAllTimers();
        _midnightTimer?.Dispose();
    }
}
