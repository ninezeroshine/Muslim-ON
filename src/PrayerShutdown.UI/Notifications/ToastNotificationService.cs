using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using PrayerShutdown.Common;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.UI.Notifications;

/// <summary>
/// Windows App SDK Toast notifications. Works in unpackaged apps — <see cref="Register"/>
/// handles COM activator registration automatically as of WindowsAppSDK 1.2+.
///
/// Toast arguments are encoded as <c>action=X;prayer=Y</c>. We use semicolons rather
/// than URL query separators to side-step the built-in Arguments.Add escaping rules —
/// and parse them back manually in OnInvoked.
/// </summary>
public sealed class ToastNotificationService : INotificationService, IDisposable
{
    private const string ArgAction = "action";
    private const string ArgPrayer = "prayer";
    private const string TagPrefix = "muslimon-";

    private readonly ILogger<ToastNotificationService> _logger;
    private readonly ISettingsRepository _settingsRepo;
    private DispatcherQueue? _dispatcher;
    private bool _registered;

    public event EventHandler<NotificationInvokedEventArgs>? ActionInvoked;

    public ToastNotificationService(
        ILogger<ToastNotificationService> logger,
        ISettingsRepository settingsRepo)
    {
        _logger = logger;
        _settingsRepo = settingsRepo;
    }

    public void Initialize()
    {
        if (_registered) return;
        try
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            AppNotificationManager.Default.NotificationInvoked += OnInvoked;
            AppNotificationManager.Default.Register();
            _registered = true;
            _logger.LogInformation("AppNotificationManager registered");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Toast registration failed — notifications disabled");
        }
    }

    public async Task ShowReminderAsync(PrayerTime prayer, int minutesBefore)
    {
        if (!await IsEnabledAsync()) return;
        var prayerName = Loc.S($"prayer_{prayer.Name.ToString().ToLowerInvariant()}");
        var title = string.Format(Loc.S("toast_remind_title"), prayerName);
        var body = string.Format(Loc.S("toast_remind_body"), minutesBefore, prayer.Time.ToString("HH:mm"));

        Show(prayer.Name, title, body,
            (Loc.S("mark_prayed"), NotificationAction.Prayed),
            (Loc.S("ill_pray_soon"), NotificationAction.Dismiss));
    }

    public async Task ShowPrayerNowAsync(PrayerTime prayer)
    {
        if (!await IsEnabledAsync()) return;
        var prayerName = Loc.S($"prayer_{prayer.Name.ToString().ToLowerInvariant()}");
        var title = string.Format(Loc.S("toast_pray_title"), prayerName);
        var body = string.Format(Loc.S("toast_pray_body"), prayer.Time.ToString("HH:mm"));

        Show(prayer.Name, title, body,
            (Loc.S("mark_prayed"), NotificationAction.Prayed),
            (Loc.S("going_to_pray"), NotificationAction.GoingToPray));
    }

    public async Task ShowNudgeAsync(PrayerTime prayer, int nudgeNumber, int maxNudges)
    {
        if (!await IsEnabledAsync()) return;
        var prayerName = Loc.S($"prayer_{prayer.Name.ToString().ToLowerInvariant()}");
        var title = Loc.S("toast_nudge_title");
        var body = string.Format(Loc.S("toast_nudge_body"), prayerName, nudgeNumber, maxNudges);

        Show(prayer.Name, title, body,
            (Loc.S("mark_prayed"), NotificationAction.Prayed),
            (string.Format(Loc.S("snooze"), Constants.NudgeIntervalMinutes), NotificationAction.Snooze));
    }

    public void DismissAll()
    {
        try { _ = AppNotificationManager.Default.RemoveAllAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "DismissAll failed"); }
    }

    public void DismissFor(PrayerName prayer)
    {
        try { _ = AppNotificationManager.Default.RemoveByTagAsync(TagPrefix + prayer); }
        catch (Exception ex) { _logger.LogWarning(ex, "DismissFor {Prayer} failed", prayer); }
    }

    private void Show(PrayerName prayer, string title, string body,
        params (string label, NotificationAction action)[] buttons)
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body)
                .AddArgument(ArgPrayer, prayer.ToString())
                .AddArgument(ArgAction, NotificationAction.OpenApp.ToString());

            foreach (var (label, action) in buttons)
            {
                builder.AddButton(new AppNotificationButton(label)
                    .AddArgument(ArgPrayer, prayer.ToString())
                    .AddArgument(ArgAction, action.ToString()));
            }

            var notification = builder.BuildNotification();
            notification.Tag = TagPrefix + prayer; // lets DismissFor target this prayer's toasts
            notification.ExpiresOnReboot = true;

            AppNotificationManager.Default.Show(notification);
            _logger.LogInformation("Toast shown: {Prayer} — {Title}", prayer, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show toast for {Prayer}", prayer);
        }
    }

    private async Task<bool> IsEnabledAsync()
    {
        if (!_registered) return false;
        try
        {
            var settings = await _settingsRepo.LoadAsync();
            return settings.Notification.EnableToastNotifications;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Settings load for toast check failed — falling back to enabled");
            return true;
        }
    }

    private void OnInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        try
        {
            if (!args.Arguments.TryGetValue(ArgPrayer, out var prayerStr)) return;
            if (!args.Arguments.TryGetValue(ArgAction, out var actionStr)) return;

            if (!Enum.TryParse<PrayerName>(prayerStr, out var prayer)) return;
            if (!Enum.TryParse<NotificationAction>(actionStr, ignoreCase: true, out var action)) return;

            _logger.LogInformation("Toast invoked: {Prayer} {Action}", prayer, action);

            var payload = new NotificationInvokedEventArgs { Action = action, Prayer = prayer };

            // AppNotificationManager fires on a background MTA thread; marshal to UI.
            if (_dispatcher is not null)
                _dispatcher.TryEnqueue(() => ActionInvoked?.Invoke(this, payload));
            else
                ActionInvoked?.Invoke(this, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Toast invocation handler threw");
        }
    }

    public void Dispose()
    {
        try
        {
            if (_registered)
            {
                AppNotificationManager.Default.NotificationInvoked -= OnInvoked;
                AppNotificationManager.Default.Unregister();
            }
        }
        catch { /* shutting down */ }
    }
}
