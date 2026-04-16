using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PrayerScheduler> _logger;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> _reminderTimers = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> _prayerTimeTimers = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> _nudgeTimers = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> _shutdownTimers = new();
    // Phase 4 safety net — fires ExecuteShutdown if the overlay never confirms.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, Timer?> _shutdownSafetyTimers = new();

    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, int> _snoozeCounts = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, bool> _prayedToday = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<PrayerName, PrayerShutdownRule> _activeRules = new();
    private Timer? _midnightTimer;

    public DailyPrayerTimes? TodaysPrayers { get; private set; }
    public PrayerTime? NextPrayer => TodaysPrayers?.GetNextPrayer(DateTime.Now);

    public event EventHandler<PrayerTime>? PrayerTimeApproaching;
    public event EventHandler<PrayerTime>? PrayerTimeArrived;
    public event EventHandler<PrayerNudgeEventArgs>? PrayerNudge;
    public event EventHandler<ShutdownTriggeredEventArgs>? ShutdownTriggered;
    public event EventHandler<PrayerTime>? PrayerMarkedAsPrayed;

    public PrayerScheduler(
        IPrayerTimeCalculator calculator,
        ISettingsRepository settingsRepo,
        IShutdownService shutdownService,
        IServiceScopeFactory scopeFactory,
        ILogger<PrayerScheduler> logger)
    {
        _calculator = calculator;
        _settingsRepo = settingsRepo;
        _shutdownService = shutdownService;
        _scopeFactory = scopeFactory;
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
        _logger.LogInformation("[MarkAsPrayed] {Prayer} at {Time:HH:mm}", prayer.Name, prayer.Time);

        // Order matters: set flag BEFORE disposing timers so any in-flight callback sees prayed=true.
        _prayedToday[prayer.Name] = true;
        CancelAllTimersFor(prayer.Name);

        if (_shutdownService.HasPendingShutdown)
            _shutdownService.CancelPendingShutdown();

        LogAction(prayer.Name, "MarkedAsPrayed", "user action");
        PrayerMarkedAsPrayed?.Invoke(this, prayer);
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
        LogAction(prayer.Name, "Snoozed", $"{count + 1}/{Constants.MaxSnoozeCount}");

        if (_shutdownService.HasPendingShutdown)
            _shutdownService.CancelPendingShutdown();

        // Snooze re-arms the NEXT nudge only. The shutdown timer stays anchored to
        // prayer.Time + rule.ShutdownMinutesAfter — snoozing must not push the
        // deadline past what the user configured.
        DisposeTimer(_nudgeTimers, prayer.Name);
        var nudgeDelay = TimeSpan.FromMinutes(Constants.NudgeIntervalMinutes);
        _nudgeTimers[prayer.Name] = new Timer(
            _ => OnPrayerNudge(prayer, count + 2),
            null, nudgeDelay, Timeout.InfiniteTimeSpan);
    }

    public void CancelShutdownSafety(PrayerName prayerName)
    {
        if (_shutdownSafetyTimers.TryRemove(prayerName, out var timer))
        {
            timer?.Dispose();
            _logger.LogInformation("[CancelShutdownSafety] {Prayer} — overlay handled it", prayerName);
        }
    }

    public NextPhasePlan? GetNextPhasePlan()
    {
        var today = TodaysPrayers;
        if (today is null) return null;

        var now = DateTime.Now;
        var next = today.GetNextPrayer(now);
        if (next is null) return null;

        var rule = _activeRules.TryGetValue(next.Name, out var r) ? r : null;
        var enabled = rule?.IsEnabled == true;
        var reminderMin = rule?.ReminderMinutesBefore ?? Constants.DefaultReminderMinutes;
        var shutdownMin = rule?.ShutdownMinutesAfter ?? Constants.DefaultShutdownDelayMinutes;
        var action = rule?.Action ?? ShutdownAction.Shutdown;

        return new NextPhasePlan
        {
            Prayer = next,
            RemindAt = enabled ? next.Time.AddMinutes(-reminderMin) : null,
            PrayAt = next.Time,
            NudgeAt = enabled ? next.Time.AddMinutes(Constants.NudgeIntervalMinutes) : null,
            ShutdownAt = enabled ? next.Time.AddMinutes(shutdownMin) : null,
            Action = action,
            ShutdownEnabled = enabled,
        };
    }

    private async Task RecalculateAsync(bool preservePrayedState = true)
    {
        try
        {
            CancelAllTimers();
            _activeRules.Clear();

            if (!preservePrayedState)
            {
                _logger.LogInformation("[Recalculate] resetting prayed/snooze state (midnight)");
                _prayedToday.Clear();
                _snoozeCounts.Clear();
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

            foreach (var rule in settings.Shutdown.Rules)
            {
                var prayer = TodaysPrayers.GetPrayer(rule.Prayer);
                if (prayer is null) continue;
                _activeRules[rule.Prayer] = rule;
                if (rule.IsEnabled) SchedulePrayer(prayer, rule);
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
        if (_prayedToday.ContainsKey(prayer.Name))
        {
            _logger.LogInformation("[SchedulePrayer] {Prayer} already prayed — skipping", prayer.Name);
            return;
        }

        var now = DateTime.Now;

        // Phase 1: Reminder
        var reminderTime = prayer.Time.AddMinutes(-rule.ReminderMinutesBefore);
        if (reminderTime > now)
            _reminderTimers[prayer.Name] = new Timer(
                _ => OnPrayerApproaching(prayer, rule.ReminderMinutesBefore),
                null, reminderTime - now, Timeout.InfiniteTimeSpan);

        // Phase 2: Prayer time arrived
        if (prayer.Time > now)
            _prayerTimeTimers[prayer.Name] = new Timer(
                _ => OnPrayerTimeArrived(prayer),
                null, prayer.Time - now, Timeout.InfiniteTimeSpan);

        // Phase 3: First nudge
        var firstNudgeTime = prayer.Time.AddMinutes(Constants.NudgeIntervalMinutes);
        if (firstNudgeTime > now)
            _nudgeTimers[prayer.Name] = new Timer(
                _ => OnPrayerNudge(prayer, 1),
                null, firstNudgeTime - now, Timeout.InfiniteTimeSpan);

        // Phase 4: Shutdown
        var shutdownTime = prayer.Time.AddMinutes(rule.ShutdownMinutesAfter);
        if (shutdownTime > now)
            _shutdownTimers[prayer.Name] = new Timer(
                _ => OnShutdownTriggered(prayer),
                null, shutdownTime - now, Timeout.InfiniteTimeSpan);
    }

    // ── Phase 1: Gentle Reminder ──
    private void OnPrayerApproaching(PrayerTime prayer, int minutesBefore)
    {
        if (_prayedToday.ContainsKey(prayer.Name)) return;
        if (!_reminderTimers.ContainsKey(prayer.Name)) return;

        _logger.LogInformation("[Phase 1] {Prayer} approaching in {Min} min", prayer.Name, minutesBefore);
        LogAction(prayer.Name, "Remind_Fired", $"{minutesBefore} min before");
        PrayerTimeApproaching?.Invoke(this, prayer);
    }

    // ── Phase 2: Prayer Time Arrived ──
    private void OnPrayerTimeArrived(PrayerTime prayer)
    {
        if (_prayedToday.ContainsKey(prayer.Name)) return;
        if (!_prayerTimeTimers.ContainsKey(prayer.Name)) return;

        _logger.LogInformation("[Phase 2] {Prayer} time arrived", prayer.Name);
        LogAction(prayer.Name, "PrayNow_Fired", prayer.Time.ToString("HH:mm"));
        PrayerTimeArrived?.Invoke(this, prayer);
    }

    // ── Phase 3: Escalating Nudge ──
    private void OnPrayerNudge(PrayerTime prayer, int nudgeNumber)
    {
        if (_prayedToday.ContainsKey(prayer.Name)) return;
        if (!_nudgeTimers.ContainsKey(prayer.Name)) return;

        var maxNudges = Constants.MaxSnoozeCount;
        _logger.LogWarning("[Phase 3 Nudge #{N}/{Max}] {Prayer}", nudgeNumber, maxNudges, prayer.Name);
        LogAction(prayer.Name, $"Nudge_{nudgeNumber}_Fired", $"{nudgeNumber}/{maxNudges}");

        PrayerNudge?.Invoke(this, new PrayerNudgeEventArgs
        {
            Prayer = prayer,
            NudgeNumber = nudgeNumber,
            MaxNudges = maxNudges,
        });
    }

    // ── Phase 4: Shutdown ──
    //
    // PRIMARY: fire event → UI shows overlay countdown → on countdown=0 UI calls
    //   IShutdownService.Execute AND ISchedulerService.CancelShutdownSafety.
    //
    // SAFETY:  arm a timer for (CountdownSeconds + SafetyBuffer). If it fires, the
    //   overlay never confirmed — call Execute ourselves so the user still gets
    //   the rule's action. Covers: overlay crash, UI freeze, user closed overlay.
    private void OnShutdownTriggered(PrayerTime prayer)
    {
        if (_prayedToday.ContainsKey(prayer.Name)) return;
        if (!_shutdownTimers.ContainsKey(prayer.Name)) return;

        var action = _activeRules.TryGetValue(prayer.Name, out var r) ? r.Action : ShutdownAction.Shutdown;

        _logger.LogWarning("[Phase 4] {Prayer} — firing event, arming safety net (action={Action})", prayer.Name, action);
        LogAction(prayer.Name, "Shutdown_Triggered", action.ToString());

        ShutdownTriggered?.Invoke(this, new ShutdownTriggeredEventArgs
        {
            Prayer = prayer,
            Action = action,
        });

        DisposeTimer(_shutdownSafetyTimers, prayer.Name);
        var safetyDelay = TimeSpan.FromSeconds(
            Constants.ShutdownCountdownSeconds + Constants.ShutdownSafetyBufferSeconds);
        _shutdownSafetyTimers[prayer.Name] = new Timer(
            _ => OnShutdownSafetyNetFired(prayer, action),
            null,
            safetyDelay,
            Timeout.InfiniteTimeSpan);
    }

    private void OnShutdownSafetyNetFired(PrayerTime prayer, ShutdownAction action)
    {
        if (_prayedToday.ContainsKey(prayer.Name)) return;
        if (_shutdownService.HasPendingShutdown)
        {
            _logger.LogInformation("[Phase 4 SafetyNet] {Prayer} — overlay already handled", prayer.Name);
            return;
        }
        if (!_shutdownSafetyTimers.ContainsKey(prayer.Name)) return;

        _logger.LogWarning("[Phase 4 SafetyNet] {Prayer} — FORCING fallback {Action}", prayer.Name, action);
        LogAction(prayer.Name, "Shutdown_SafetyNet_Fired", action.ToString());
        _shutdownService.Execute(action);
    }

    // ── Debug: manual phase trigger ──
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
                ShutdownTriggered?.Invoke(this, new ShutdownTriggeredEventArgs
                {
                    Prayer = test,
                    Action = ShutdownAction.Shutdown,
                });
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
            _logger.LogInformation("Midnight recalculation triggered");
            _ = RecalculateAsync(preservePrayedState: false);
            ScheduleMidnightRecalculation();
        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    private void LogAction(PrayerName prayer, string eventName, string detail)
    {
        // Fire-and-forget; uses a short-lived DI scope so the scoped IActionLogger
        // (and its transient DbContext) get disposed after each write.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<IActionLogger>();
                await logger.LogAsync(new ActionLogEntry
                {
                    Prayer = prayer,
                    Event = eventName,
                    Detail = detail,
                });
            }
            catch (Exception ex) { _logger.LogWarning(ex, "ActionLogger write failed"); }
        });
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
        DisposeTimer(_shutdownSafetyTimers, name);
    }

    private void CancelAllTimers()
    {
        DisposeAll(_reminderTimers);
        DisposeAll(_prayerTimeTimers);
        DisposeAll(_nudgeTimers);
        DisposeAll(_shutdownTimers);
        DisposeAll(_shutdownSafetyTimers);
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
