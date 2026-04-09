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
    private ISchedulerService? _scheduler;
    private IShutdownService? _shutdownService;

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
            _scheduler.PrayerTimeApproaching += OnPhase1_Reminder;
            _scheduler.PrayerTimeArrived += OnPhase2_PrayNow;
            _scheduler.PrayerNudge += OnPhase3_Nudge;
            _scheduler.ShutdownTriggered += OnPhase4_Shutdown;

            if (settings.Location.SelectedLocation is not null)
                await _scheduler.InitializeAsync();

            _trayIcon = new TrayIconManager(_scheduler);
            _trayIcon.Initialize(_mainWindow);

            // App is now fully initialized — flush any reactivation requests that
            // arrived from secondary instances during startup.
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

    /// <summary>
    /// Called by <see cref="Program.OnReactivated"/> when a secondary process
    /// redirects its activation here. Marshals to the UI thread and brings the
    /// main window forward. If the app is still initializing, queues the request
    /// until <see cref="OnLaunched"/> completes.
    /// </summary>
    public void OnReactivated()
    {
        var dispatcher = _dispatcher;
        if (dispatcher is null)
        {
            // Activation arrived before we even captured the dispatcher —
            // OnLaunched will pick this up at the end.
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
        catch (Exception ex)
        {
            WriteLog("BringToForeground", ex);
        }
    }

    // ── Phase handlers ──

    private void OnPhase1_Reminder(object? sender, PrayerTime prayer)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            try
            {
                _activePrayer = prayer;
                ShowOverlay(OverlayPhase.Remind, prayer);
            }
            catch (Exception ex) { WriteLog("Phase1_Reminder", ex); }
        });
    }

    private void OnPhase2_PrayNow(object? sender, PrayerTime prayer)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            try
            {
                _activePrayer = prayer;
                ShowOverlay(OverlayPhase.PrayNow, prayer);
            }
            catch (Exception ex) { WriteLog("Phase2_PrayNow", ex); }
        });
    }

    private void OnPhase3_Nudge(object? sender, PrayerNudgeEventArgs e)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            try
            {
                _activePrayer = e.Prayer;
                ShowOverlay(OverlayPhase.Nudge, e.Prayer, e.NudgeNumber, e.MaxNudges);
            }
            catch (Exception ex) { WriteLog("Phase3_Nudge", ex); }
        });
    }

    private void OnPhase4_Shutdown(object? sender, PrayerTime prayer)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            try
            {
                _activePrayer = prayer;
                ShowOverlay(OverlayPhase.Shutdown, prayer);
            }
            catch (Exception ex) { WriteLog("Phase4_Shutdown", ex); }
        });
    }

    // ── Overlay Management ──

    private void ShowOverlay(OverlayPhase phase, PrayerTime prayer, int nudgeNumber = 0, int maxNudges = 0)
    {
        CloseOverlay();

        _overlay = new PrayerOverlayWindow();
        _overlay.PrayedClicked += OnOverlay_Prayed;
        _overlay.DismissClicked += OnOverlay_Dismiss;
        _overlay.GoingToPrayClicked += OnOverlay_GoingToPray;
        _overlay.SnoozeClicked += OnOverlay_Snooze;
        _overlay.ShutdownCountdownFinished += OnOverlay_ShutdownFinished;
        _overlay.Closed += (_, _) => _overlay = null;

        _overlay.ShowPhase(phase, prayer, nudgeNumber, maxNudges);
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
        if (prayer is null || _scheduler is null)
        {
            WriteLog("OnOverlay_Prayed", new InvalidOperationException(
                $"Skipped: _activePrayer={(prayer is null ? "null" : prayer.Name.ToString())}, " +
                $"_scheduler={(_scheduler is null ? "null" : "ok")}"));
            CloseOverlay();
            return;
        }

        _scheduler.MarkAsPrayed(prayer);
        _activePrayer = null;
        CloseOverlay();
    }

    private void OnOverlay_Dismiss(object? sender, EventArgs e)
    {
        _activePrayer = null;
        CloseOverlay();
    }

    private void OnOverlay_GoingToPray(object? sender, EventArgs e)
    {
        if (_activePrayer is not null)
            _scheduler?.SetWaitingForPrayer(_activePrayer);
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
        // Overlay countdown reached zero. We are now responsible for firing the
        // actual shutdown command — and we must cancel the scheduler's safety-net
        // timer because we are about to do exactly the job it was guarding.
        var prayer = _activePrayer;
        if (prayer is not null)
        {
            _scheduler?.CancelShutdownSafety(prayer.Name);
            _shutdownService?.ExecuteShutdown();
        }
        else
        {
            WriteLog("OnOverlay_ShutdownFinished", new InvalidOperationException(
                "Overlay countdown finished but _activePrayer was null"));
        }
        CloseOverlay();
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
