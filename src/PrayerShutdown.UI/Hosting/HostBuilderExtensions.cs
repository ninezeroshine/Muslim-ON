using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrayerShutdown.Core.Interfaces;
using PrayerShutdown.Features.ActionLog;
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
using PrayerShutdown.UI.Notifications;

namespace PrayerShutdown.UI.Hosting;

public static class HostBuilderExtensions
{
    public static IHostBuilder ConfigurePrayerShutdownServices(this IHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddDebug();
            });

            // Transient — each resolve gets a fresh DbContext. Safe for singletons
            // that call GetRequiredService<T>() inside a scope they own.
            services.AddTransient<AppDbContext>();
            services.AddTransient<IPrayerTimeRepository, PrayerTimeRepository>();
            services.AddTransient<ISettingsRepository, SettingsRepository>();
            services.AddTransient<IActionLogger, ActionLogger>();

            services.AddSingleton<IPrayerTimeCalculator, PrayerTimeCalculator>();
            services.AddSingleton<IShutdownService, WindowsShutdownService>();
            services.AddSingleton<ILocationService, LocationService>();
            services.AddSingleton<IAdhanPlayer, AdhanPlayer>();
            services.AddSingleton<INotificationService, ToastNotificationService>();
            services.AddSingleton<UpdateService>();

            services.AddSingleton<ISchedulerService>(sp => new PrayerScheduler(
                sp.GetRequiredService<IPrayerTimeCalculator>(),
                sp.GetRequiredService<ISettingsRepository>(),
                sp.GetRequiredService<IShutdownService>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<PrayerScheduler>>()));

            services.AddSingleton<NavigationService>();

            services.AddSingleton<PrayerDashboardViewModel>();
            services.AddSingleton<ActionLogViewModel>();
            services.AddSingleton<GeneralSettingsViewModel>(sp =>
            {
                var vm = new GeneralSettingsViewModel(
                    sp.GetRequiredService<ISettingsRepository>(),
                    sp.GetRequiredService<ILocationService>(),
                    sp.GetRequiredService<ISchedulerService>());
                var dashboard = sp.GetRequiredService<PrayerDashboardViewModel>();
                vm.OnSettingsSaved += dashboard.ForceRefreshAsync;
                return vm;
            });

            services.AddSingleton<MainWindow>();
        });

        return builder;
    }
}
