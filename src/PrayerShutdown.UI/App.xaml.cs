using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
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

    public IHost Host { get; }
    public static new App Current => (App)Application.Current;
    public IServiceProvider Services => Host.Services;
    public TrayIconManager? TrayIcon => _trayIcon;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;

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

            // Load settings + language
            var settingsRepo = Services.GetRequiredService<ISettingsRepository>();
            var settings = await settingsRepo.LoadAsync();
            LocalizationService.Instance.SetLanguage(settings.Language);

            // Initialize scheduler with 4-phase events
            var scheduler = Services.GetRequiredService<ISchedulerService>();
            scheduler.PrayerTimeApproaching += OnPhase1_Reminder;
            scheduler.PrayerTimeArrived += OnPhase2_PrayNow;
            scheduler.PrayerNudge += OnPhase3_Nudge;
            scheduler.ShutdownTriggered += OnPhase4_Shutdown;

            if (settings.Location.SelectedLocation is not null)
                await scheduler.InitializeAsync();

            // Tray icon
            _trayIcon = new TrayIconManager(scheduler);
            _trayIcon.Initialize(_mainWindow);
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
        }
    }

    // ═══════════════════════════════════════════
    //  Phase 1: Gentle Reminder (15 min before)
    // ═══════════════════════════════════════════
    private void OnPhase1_Reminder(object? sender, PrayerTime prayer)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            _activePrayer = prayer;
            ShowOverlay(OverlayPhase.Remind, prayer);
        });
    }

    // ═══════════════════════════════════════════
    //  Phase 2: Prayer Time Arrived
    // ═══════════════════════════════════════════
    private void OnPhase2_PrayNow(object? sender, PrayerTime prayer)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            _activePrayer = prayer;
            ShowOverlay(OverlayPhase.PrayNow, prayer);
        });
    }

    // ═══════════════════════════════════════════
    //  Phase 3: Escalating Nudges
    // ═══════════════════════════════════════════
    private void OnPhase3_Nudge(object? sender, PrayerNudgeEventArgs e)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            _activePrayer = e.Prayer;
            ShowOverlay(OverlayPhase.Nudge, e.Prayer, e.NudgeNumber, e.MaxNudges);
        });
    }

    // ═══════════════════════════════════════════
    //  Phase 4: Shutdown
    // ═══════════════════════════════════════════
    private void OnPhase4_Shutdown(object? sender, PrayerTime prayer)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            _activePrayer = prayer;
            ShowOverlay(OverlayPhase.Shutdown, prayer);
        });
    }

    // ═══════════════════════════════════════════
    //  Overlay Management
    // ═══════════════════════════════════════════
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

    // ── Overlay event handlers ──

    private void OnOverlay_Prayed(object? sender, EventArgs e)
    {
        if (_activePrayer is null) return;

        var scheduler = Services.GetRequiredService<ISchedulerService>();
        scheduler.MarkAsPrayed(_activePrayer);
        CloseOverlay();
        _activePrayer = null;
    }

    private void OnOverlay_Dismiss(object? sender, EventArgs e)
    {
        // Phase 1: "I'll pray soon" — close overlay, Phase 2 will fire at prayer time
        CloseOverlay();
    }

    private void OnOverlay_GoingToPray(object? sender, EventArgs e)
    {
        if (_activePrayer is null) return;

        // Phase 2: "Going to pray" — hide overlay, keep timers active
        var scheduler = Services.GetRequiredService<ISchedulerService>();
        scheduler.SetWaitingForPrayer(_activePrayer);
        CloseOverlay();
    }

    private void OnOverlay_Snooze(object? sender, EventArgs e)
    {
        if (_activePrayer is null) return;

        // Phase 3: snooze — reschedule nudge
        var scheduler = Services.GetRequiredService<ISchedulerService>();
        scheduler.SnoozePrayer(_activePrayer);
        CloseOverlay();
    }

    private void OnOverlay_ShutdownFinished(object? sender, EventArgs e)
    {
        // Phase 4 countdown reached zero — shutdown already initiated by scheduler
        CloseOverlay();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        WriteCrashLog(e.Exception);
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "MuslimOn_crash.log");
            File.WriteAllText(path, $"{DateTime.Now}: {ex}");
        }
        catch { }
    }
}
