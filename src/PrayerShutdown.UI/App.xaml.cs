using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using PrayerShutdown.Core.Interfaces;
using PrayerShutdown.UI.Hosting;
using PrayerShutdown.UI.TrayIcon;

namespace PrayerShutdown.UI;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayIconManager? _trayIcon;

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
            _mainWindow = Services.GetRequiredService<MainWindow>();
            _mainWindow.Activate();

            // Load settings + language
            var settingsRepo = Services.GetRequiredService<ISettingsRepository>();
            var settings = await settingsRepo.LoadAsync();
            PrayerShutdown.Common.Localization.LocalizationService.Instance.SetLanguage(settings.Language);

            // Initialize tray icon for background operation
            var scheduler = Services.GetRequiredService<ISchedulerService>();
            _trayIcon = new TrayIconManager(scheduler);
            _trayIcon.Initialize(_mainWindow);
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
        }
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
        catch { /* last resort — can't even write log */ }
    }
}
