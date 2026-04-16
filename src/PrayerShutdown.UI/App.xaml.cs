using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PrayerShutdown.Common;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Interfaces;
using PrayerShutdown.UI.Dialogs;
using PrayerShutdown.UI.Hosting;
using PrayerShutdown.UI.TrayIcon;

namespace PrayerShutdown.UI;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayIconManager? _trayIcon;
    private DispatcherQueue? _dispatcher;
    private PrayerOverlayWindow? _overlay;
    private PrayerTime? _activePrayer;
    private ShutdownAction _activeShutdownAction = ShutdownAction.Shutdown;
    private ISchedulerService? _scheduler;
    private IShutdownService? _shutdownService;
    private INotificationService? _notificationService;
    private IAdhanPlayer? _adhanPlayer;
    private IServiceScopeFactory? _scopeFactory;

    // Single-instance reactivation state — see OnReactivated.
    private volatile bool _isReady;
    private volatile bool _pendingReactivation;

    public IHost Host { get; }
    public static new App Current => (App)Application.Current;
    public IServiceProvider Services => Host.Services;
    public TrayIconManager? TrayIcon => _trayIcon;
    public ISchedulerService? Scheduler => _scheduler;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigurePrayerShutdownServices()
            .Build();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            _mainWindow = Services.GetRequiredService<MainWindow>();
            _mainWindow.Activate();

            var settingsRepo = Services.GetRequiredService<ISettingsRepository>();
            var settings = await settingsRepo.LoadAsync();
            LocalizationService.Instance.SetLanguage(settings.Language);

            _scheduler = Services.GetRequiredService<ISchedulerService>();
            _shutdownService = Services.GetRequiredService<IShutdownService>();
            _notificationService = Services.GetRequiredService<INotificationService>();
            _adhanPlayer = Services.GetRequiredService<IAdhanPlayer>();
            _scopeFactory = Services.GetRequiredService<IServiceScopeFactory>();

            _scheduler.PrayerTimeApproaching += OnPhase1_Reminder;
            _scheduler.PrayerTimeArrived += OnPhase2_PrayNow;
            _scheduler.PrayerNudge += OnPhase3_Nudge;
            _scheduler.ShutdownTriggered += OnPhase4_Shutdown;

            _notificationService.ActionInvoked += OnToastActionInvoked;
            _notificationService.Initialize();

            if (settings.Location.SelectedLocation is not null)
                await _scheduler.InitializeAsync();

            _trayIcon = new TrayIconManager(_scheduler);
            _trayIcon.Initialize(_mainWindow);

            _isReady = true;
            if (_pendingReactivation)
            {
                _pendingReactivation = false;
                BringToForeground();
            }
        }
        catch (Exception ex)
        {
            WriteLog("OnLaunched", ex);
        }
    }

    // ── Single-instance reactivation ──

    public void OnReactivated()
    {
        var dispatcher = _dispatcher;
        if (dispatcher is null)
        {
            _pendingReactivation = true;
            return;
        }

        dispatcher.TryEnqueue(() =>
        {
            if (!_isReady)
            {
                _pendingReactivation = true;
                return;
            }
            BringToForeground();
        });
    }

    private void BringToForeground()
    {
        try
        {
            if (_mainWindow is null) return;
            _mainWindow.AppWindow?.Show();
            _mainWindow.Activate();
        }
        catch (Exception ex) { WriteLog("BringToForeground", ex); }
    }

    // ── Phase handlers ──

    private void OnPhase1_Reminder(object? sender, PrayerTime prayer)
    {
        _dispatcher?.TryEnqueue(async () =>
        {
            try
            {
                _activePrayer = prayer;
                if (_notificationService is not null)
                    await _notificationService.ShowReminderAsync(prayer, Constants.DefaultReminderMinutes);
                // Remind phase uses the soft toast only by default. The overlay is
                // reserved for the more assertive PrayNow/Nudge/Shutdown phases.
            }
            catch (Exception ex) { WriteLog("Phase1_Reminder", ex); }
        });
    }

    private void OnPhase2_PrayNow(object? sender, PrayerTime prayer)
    {
        _dispatcher?.TryEnqueue(async () =>
        {
            try
            {
                _activePrayer = prayer;
                if (_notificationService is not null)
                    await _notificationService.ShowPrayerNowAsync(prayer);
                await PlayAdhanIfEnabledAsync();
                ShowOverlay(OverlayPhase.PrayNow, prayer);
            }
            catch (Exception ex) { WriteLog("Phase2_PrayNow", ex); }
        });
    }

    private void OnPhase3_Nudge(object? sender, PrayerNudgeEventArgs e)
    {
        _dispatcher?.TryEnqueue(async () =>
        {
            try
            {
                _activePrayer = e.Prayer;
                if (_notificationService is not null)
                    await _notificationService.ShowNudgeAsync(e.Prayer, e.NudgeNumber, e.MaxNudges);
                ShowOverlay(OverlayPhase.Nudge, e.Prayer, e.NudgeNumber, e.MaxNudges);
            }
            catch (Exception ex) { WriteLog("Phase3_Nudge", ex); }
        });
    }

    private void OnPhase4_Shutdown(object? sender, ShutdownTriggeredEventArgs e)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            try
            {
                _activePrayer = e.Prayer;
                _activeShutdownAction = e.Action;
                _notificationService?.DismissFor(e.Prayer.Name);
                ShowOverlay(OverlayPhase.Shutdown, e.Prayer, shutdownAction: e.Action);
            }
            catch (Exception ex) { WriteLog("Phase4_Shutdown", ex); }
        });
    }

    // ── Toast actions ──

    private void OnToastActionInvoked(object? sender, NotificationInvokedEventArgs e)
    {
        try
        {
            var prayer = _scheduler?.TodaysPrayers?.GetPrayer(e.Prayer);
            if (prayer is null)
            {
                BringToForeground();
                return;
            }

            switch (e.Action)
            {
                case NotificationAction.Prayed:
                    _scheduler?.MarkAsPrayed(prayer);
                    _adhanPlayer?.Stop();
                    break;
                case NotificationAction.Snooze:
                    _scheduler?.SnoozePrayer(prayer);
                    break;
                case NotificationAction.GoingToPray:
                    LogAction(prayer.Name, "GoingToPray", "toast");
                    _adhanPlayer?.Stop();
                    break;
                case NotificationAction.Dismiss:
                    LogAction(prayer.Name, "ToastDismissed", "user");
                    break;
                case NotificationAction.OpenApp:
                    BringToForeground();
                    break;
            }
        }
        catch (Exception ex) { WriteLog("OnToastActionInvoked", ex); }
    }

    // ── Adhan ──

    private async Task PlayAdhanIfEnabledAsync()
    {
        try
        {
            if (_adhanPlayer is null) return;
            var settings = await Services.GetRequiredService<ISettingsRepository>().LoadAsync();
            if (!settings.Notification.EnableAdhanSound) return;
            await _adhanPlayer.PlayAsync(settings.Notification.AdhanSoundPath);
        }
        catch (Exception ex) { WriteLog("PlayAdhan", ex); }
    }

    // ── Overlay Management ──

    private void ShowOverlay(OverlayPhase phase, PrayerTime prayer,
        int nudgeNumber = 0, int maxNudges = 0,
        ShutdownAction shutdownAction = ShutdownAction.Shutdown)
    {
        CloseOverlay();

        _overlay = new PrayerOverlayWindow();
        _overlay.PrayedClicked += OnOverlay_Prayed;
        _overlay.DismissClicked += OnOverlay_Dismiss;
        _overlay.GoingToPrayClicked += OnOverlay_GoingToPray;
        _overlay.SnoozeClicked += OnOverlay_Snooze;
        _overlay.ShutdownCountdownFinished += OnOverlay_ShutdownFinished;
        _overlay.Closed += (_, _) =>
        {
            if (_activePrayer is not null)
                LogAction(_activePrayer.Name, "Overlay_Closed", phase.ToString());
            _overlay = null;
        };

        LogAction(prayer.Name, "Overlay_Shown", phase.ToString());
        _overlay.ShowPhase(phase, prayer, nudgeNumber, maxNudges, shutdownAction);
    }

    private void CloseOverlay()
    {
        if (_overlay is null) return;

        _overlay.PrayedClicked -= OnOverlay_Prayed;
        _overlay.DismissClicked -= OnOverlay_Dismiss;
        _overlay.GoingToPrayClicked -= OnOverlay_GoingToPray;
        _overlay.SnoozeClicked -= OnOverlay_Snooze;
        _overlay.ShutdownCountdownFinished -= OnOverlay_ShutdownFinished;

        _overlay.HideOverlay();
        _overlay = null;
    }

    private void OnOverlay_Prayed(object? sender, EventArgs e)
    {
        var prayer = _activePrayer;
        if (prayer is null || _scheduler is null) { CloseOverlay(); return; }

        _scheduler.MarkAsPrayed(prayer);
        _notificationService?.DismissFor(prayer.Name);
        _adhanPlayer?.Stop();
        _activePrayer = null;
        CloseOverlay();
    }

    private void OnOverlay_Dismiss(object? sender, EventArgs e)
    {
        _activePrayer = null;
        _adhanPlayer?.Stop();
        CloseOverlay();
    }

    private void OnOverlay_GoingToPray(object? sender, EventArgs e)
    {
        if (_activePrayer is not null)
        {
            LogAction(_activePrayer.Name, "GoingToPray", "overlay");
            _adhanPlayer?.Stop();
        }
        CloseOverlay();
    }

    private void OnOverlay_Snooze(object? sender, EventArgs e)
    {
        if (_activePrayer is not null)
            _scheduler?.SnoozePrayer(_activePrayer);
        CloseOverlay();
    }

    private void OnOverlay_ShutdownFinished(object? sender, EventArgs e)
    {
        // Overlay countdown reached zero. We are responsible for firing the actual
        // shutdown — and must cancel the scheduler's safety-net timer because we
        // are about to do exactly what it was guarding.
        var prayer = _activePrayer;
        var action = _activeShutdownAction;

        if (prayer is not null)
        {
            _scheduler?.CancelShutdownSafety(prayer.Name);
            LogAction(prayer.Name, $"Shutdown_{action}", "overlay countdown ended");
        }

        _shutdownService?.Execute(action);
        CloseOverlay();
    }

    // ── Action log helper ──

    private void LogAction(PrayerName prayer, string eventName, string detail)
    {
        if (_scopeFactory is null) return;
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
            catch (Exception ex) { WriteLog("LogAction", ex); }
        });
    }

    // ── Crash logging ──

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        WriteLog("UnhandledException", e.Exception);
    }

    private static void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            WriteLog("AppDomain.UnhandledException", ex);
    }

    private static void WriteLog(string context, Exception ex)
    {
        try
        {
            var logDir = Path.Combine(Constants.AppDataPath, "logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "crash.log");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}] {ex}\n\n";
            File.AppendAllText(logPath, entry);
        }
        catch { }
    }
}
