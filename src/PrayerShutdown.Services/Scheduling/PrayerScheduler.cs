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
    public event EventHandler<PrayerTime>? PrayerMarkedAsPrayed;

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

    /// <summary>
    /// Called by settings screens and shutdown-rule toggles. Preserves prayed/snooze state
    /// so that "Я помолился" clicks survive unrelated settings changes.
    /// Only the midnight timer resets prayed state.
    /// </summary>
    public void RecalculateSchedule() => _ = RecalculateAsync(preservePrayedState: true);

    public void MarkAsPrayed(PrayerTime prayer)
    {
        _logger.LogInformation(
            "[MarkAsPrayed] {Prayer} at {Time:HH:mm} — set prayed=true, disposing all phase timers",
            prayer.Name, prayer.Time);

        // Order matters: set flag BEFORE disposing timers so any in-flight callback sees prayed=true.
        _prayedToday[prayer.Name] = true;
        _waitingForPrayer.TryRemove(prayer.Name, out _);
        CancelAllTimersFor(prayer.Name);

        if (_shutdownService.HasPendingShutdown)
        {
            _logger.LogInformation("[MarkAsPrayed] {Prayer} — cancelling pending system shutdown", prayer.Name);
            _shutdownService.CancelPendingShutdown();
        }

        // Notify dashboard (or any other listener) so it can sync its card state.
        PrayerMarkedAsPrayed?.Invoke(this, prayer);
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

    private async Task RecalculateAsync(bool preservePrayedState = true)
    {
        try
        {
            CancelAllTimers();

            if (!preservePrayedState)
            {
                _logger.LogInformation("[Recalculate] resetting prayed/snooze/waiting state (midnight)");
                _prayedToday.Clear();
                _snoozeCounts.Clear();
                _waitingForPrayer.Clear();
            }
            else
            {
                _logger.LogInformation(
                    "[Recalculate] preserving prayed state ({Count} prayers marked)",
                    _prayedToday.Count);
            }

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
        // Skip entirely if user already marked this prayer as prayed today.
        // Prevents settings-save → RecalculateSchedule from re-arming cancelled nudge/shutdown timers.
        if (_prayedToday.ContainsKey(prayer.Name))
        {
            _logger.LogInformation(
                "[SchedulePrayer] {Prayer} already prayed today — skipping all phase timers",
                prayer.Name);
            return;
        }

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
        // Guard #1: already prayed
        if (_prayedToday.ContainsKey(prayer.Name))
        {
            _logger.LogInformation("[Phase 1] {Prayer} — skipped, already prayed", prayer.Name);
            return;
        }
        // Guard #2: timer disposed (race between Dispose and pending ThreadPool callback)
        if (!_reminderTimers.ContainsKey(prayer.Name))
        {
            _logger.LogInformation("[Phase 1] {Prayer} — skipped, timer disposed", prayer.Name);
            return;
        }

        _logger.LogInformation("[Phase 1] {Prayer} approaching in {Min} min", prayer.Name, minutesBefore);
        PrayerTimeApproaching?.Invoke(this, prayer);
        _ = _notificationService.ShowPrayerReminderAsync(prayer, minutesBefore);
    }

    // ── Phase 2: Prayer Time Arrived ──
    private void OnPrayerTimeArrived(PrayerTime prayer)
    {
        if (_prayedToday.ContainsKey(prayer.Name))
        {
            _logger.LogInformation("[Phase 2] {Prayer} — skipped, already prayed", prayer.Name);
            return;
        }
        if (!_prayerTimeTimers.ContainsKey(prayer.Name))
        {
            _logger.LogInformation("[Phase 2] {Prayer} — skipped, timer disposed", prayer.Name);
            return;
        }

        _logger.LogInformation("[Phase 2] {Prayer} time arrived", prayer.Name);
        PrayerTimeArrived?.Invoke(this, prayer);
    }

    // ── Phase 3: Escalating Nudge ──
    private void OnPrayerNudge(PrayerTime prayer, int nudgeNumber)
    {
        if (_prayedToday.ContainsKey(prayer.Name))
        {
            _logger.LogInformation("[Phase 3 Nudge #{N}] {Prayer} — skipped, already prayed", nudgeNumber, prayer.Name);
            return;
        }
        if (!_nudgeTimers.ContainsKey(prayer.Name))
        {
            _logger.LogInformation("[Phase 3 Nudge #{N}] {Prayer} — skipped, timer disposed", nudgeNumber, prayer.Name);
            return;
        }

        var maxNudges = Constants.MaxSnoozeCount;
        _logger.LogWarning("[Phase 3 Nudge #{N}/{Max}] {Prayer} — firing", nudgeNumber, maxNudges, prayer.Name);

        PrayerNudge?.Invoke(this, new PrayerNudgeEventArgs
        {
            Prayer = prayer,
            NudgeNumber = nudgeNumber,
            MaxNudges = maxNudges,
        });
    }

    // ── Phase 4: Shutdown ──
    private void OnShutdownTriggered(PrayerTime prayer)
    {
        if (_prayedToday.ContainsKey(prayer.Name))
        {
            _logger.LogInformation("[Phase 4] {Prayer} — skipped, already prayed", prayer.Name);
            return;
        }
        if (!_shutdownTimers.ContainsKey(prayer.Name))
        {
            _logger.LogInformation("[Phase 4] {Prayer} — skipped, timer disposed", prayer.Name);
            return;
        }

        _logger.LogWarning("[Phase 4] {Prayer} — SHUTDOWN TRIGGERED", prayer.Name);
        ShutdownTriggered?.Invoke(this, prayer);
        _shutdownService.ExecuteShutdown();
    }

    // ── Debug: manual phase trigger ──
    /// <summary>
    /// Debug helper — synthesizes a test PrayerTime and fires the phase event immediately.
    /// Used by the tray Debug menu to exercise the overlay without waiting for the next prayer.
    /// Does NOT schedule real timers and does NOT touch _prayedToday state.
    /// </summary>
    public void TriggerTestPhase(TestPhase phase, PrayerName prayerName)
    {
        var test = new PrayerTime(prayerName, DateTime.Now);
        _logger.LogWarning("[DEBUG] Manually triggering {Phase} for {Prayer}", phase, prayerName);
        switch (phase)
        {
            case TestPhase.Remind:
                PrayerTimeApproaching?.Invoke(this, test);
                break;
            case TestPhase.PrayNow:
                PrayerTimeArrived?.Invoke(this, test);
                break;
            case TestPhase.Nudge:
                PrayerNudge?.Invoke(this, new PrayerNudgeEventArgs
                {
                    Prayer = test,
                    NudgeNumber = 1,
                    MaxNudges = Constants.MaxSnoozeCount,
                });
                break;
            case TestPhase.Shutdown:
                // Fire the event ONLY. Do NOT call _shutdownService.ExecuteShutdown() in debug.
                ShutdownTriggered?.Invoke(this, test);
                break;
        }
    }

    private void ScheduleMidnightRecalculation()
    {
        var now = DateTime.Now;
        var midnight = DateTime.Today.AddDays(1).AddSeconds(5);
        var delay = midnight - now;

        _midnightTimer?.Dispose();
        _midnightTimer = new Timer(_ =>
        {
            _logger.LogInformation("Midnight recalculation triggered — resetting prayed state");
            _ = RecalculateAsync(preservePrayedState: false);
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
