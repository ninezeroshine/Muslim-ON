using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Interfaces;
using PrayerShutdown.UI.Hosting;
using PrayerShutdown.UI.TrayIcon;

namespace PrayerShutdown.UI;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayIconManager? _trayIcon;
    private DispatcherQueue? _dispatcher;

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

            // Initialize scheduler (MUST be in App, not Dashboard — runs even if window is hidden)
            var scheduler = Services.GetRequiredService<ISchedulerService>();
            scheduler.PrayerTimeApproaching += OnPrayerApproaching;
            scheduler.ShutdownTriggered += OnShutdownTriggered;

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

    /// <summary>
    /// 15 min before prayer — show reminder popup.
    /// </summary>
    private void OnPrayerApproaching(object? sender, PrayerTime prayer)
    {
        _dispatcher?.TryEnqueue(async () =>
        {
            if (_mainWindow is null) return;

            var name = Loc.S($"prayer_{prayer.Name.ToString().ToLowerInvariant()}");
            var dialog = new ContentDialog
            {
                Title = $"\uD83D\uDD4C {Loc.S("prayer_approaching_title")}",
                Content = $"{name} — {prayer.Time:HH:mm}\n\n{Loc.S("prayer_approaching_desc")}",
                PrimaryButtonText = Loc.S("mark_prayed"),
                SecondaryButtonText = "OK",
                XamlRoot = _mainWindow.Content.XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var scheduler = Services.GetRequiredService<ISchedulerService>();
                scheduler.MarkAsPrayed(prayer);
            }

            // Bring window to front
            _mainWindow.Activate();
        });
    }

    /// <summary>
    /// Shutdown timer fired — show urgent dialog with countdown.
    /// Shutdown is already initiated by PrayerScheduler (shutdown /s /t 60).
    /// User can cancel here.
    /// </summary>
    private void OnShutdownTriggered(object? sender, PrayerTime prayer)
    {
        _dispatcher?.TryEnqueue(async () =>
        {
            if (_mainWindow is null) return;

            // Bring window to front urgently
            _mainWindow.Activate();

            var name = Loc.S($"prayer_{prayer.Name.ToString().ToLowerInvariant()}");
            var dialog = new ContentDialog
            {
                Title = $"\u26A0 {Loc.S("shutdown_warning_title")}",
                Content = $"{Loc.S("shutdown_warning_desc")}\n\n{name} — {prayer.Time:HH:mm}",
                PrimaryButtonText = Loc.S("mark_prayed"),
                SecondaryButtonText = Loc.S("cancel_shutdown"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = _mainWindow.Content.XamlRoot,
            };

            var result = await dialog.ShowAsync();

            // Both buttons cancel the shutdown — user responded
            var scheduler = Services.GetRequiredService<ISchedulerService>();
            var shutdownService = Services.GetRequiredService<IShutdownService>();

            if (result == ContentDialogResult.Primary)
                scheduler.MarkAsPrayed(prayer);

            // Cancel the pending Windows shutdown
            shutdownService.CancelPendingShutdown();
        });
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
