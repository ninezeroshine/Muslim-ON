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

            // Ensure DB is created and load initial data
            var settingsRepo = Services.GetRequiredService<ISettingsRepository>();
            var settings = await settingsRepo.LoadAsync();

            // Apply saved language
            PrayerShutdown.Common.Localization.LocalizationService.Instance.SetLanguage(settings.Language);

            // No hardcoded default — dashboard shows "Set Your Location" empty state
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LAUNCH ERROR: {ex}");
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "MuslimOn_crash.log"),
                $"{DateTime.Now}: {ex}");
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "MuslimOn_crash.log"),
            $"{DateTime.Now}: {e.Exception}");
    }
}
