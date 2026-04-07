using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrayerShutdown.Core.Interfaces;
using PrayerShutdown.Features.PrayerDashboard;
using PrayerShutdown.Features.Settings;
using PrayerShutdown.Services.Calculation;
using PrayerShutdown.Services.Location;
using PrayerShutdown.Services.Logging;
using PrayerShutdown.Services.Notification;
using PrayerShutdown.Services.Scheduling;
using PrayerShutdown.Services.Shutdown;
using PrayerShutdown.Services.Storage;
using PrayerShutdown.Services.Update;
using PrayerShutdown.UI.Navigation;

namespace PrayerShutdown.UI.Hosting;

public static class HostBuilderExtensions
{
    public static IHostBuilder ConfigurePrayerShutdownServices(this IHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            // Logging
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddDebug();
            });

            // Database — transient so each operation gets a fresh context
            services.AddTransient<AppDbContext>();

            // Repositories
            services.AddTransient<IPrayerTimeRepository, PrayerTimeRepository>();
            services.AddTransient<ISettingsRepository, SettingsRepository>();

            // Core Services
            services.AddSingleton<IPrayerTimeCalculator, PrayerTimeCalculator>();
            services.AddSingleton<IShutdownService, WindowsShutdownService>();
            services.AddSingleton<ILocationService, LocationService>();
            services.AddTransient<IActionLogger, ActionLogger>();
            services.AddSingleton<INotificationService, ToastNotificationService>();
            services.AddSingleton<IAdhanPlayer, AdhanPlayer>();
            services.AddSingleton<UpdateService>();

            // Scheduler — singleton, creates its own scoped dependencies via factory
            services.AddSingleton<ISchedulerService>(sp =>
            {
                var calculator = sp.GetRequiredService<IPrayerTimeCalculator>();
                var settingsRepo = sp.GetRequiredService<ISettingsRepository>();
                var logger = sp.GetRequiredService<ILogger<PrayerScheduler>>();
                return new PrayerScheduler(calculator, settingsRepo, logger);
            });

            // Navigation
            services.AddSingleton<NavigationService>();

            // ViewModels
            services.AddSingleton<PrayerDashboardViewModel>();
            services.AddTransient<GeneralSettingsViewModel>();

            // Window
            services.AddSingleton<MainWindow>();
        });

        return builder;
    }
}
