using System.Drawing;
using H.NotifyIcon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Core.Extensions;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.UI.TrayIcon;

public sealed class TrayIconManager : IDisposable
{
    private readonly ISchedulerService _scheduler;
    private TaskbarIcon? _trayIcon;
    private Timer? _tooltipTimer;
    private Window? _mainWindow;
    private DispatcherQueue? _dispatcher;

    public TrayIconManager(ISchedulerService scheduler)
    {
        _scheduler = scheduler;
    }

    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        Icon? icon = File.Exists(iconPath) ? new Icon(iconPath) : null;

        _trayIcon = new TaskbarIcon();
        _trayIcon.ToolTipText = "Muslim ON";
        _trayIcon.NoLeftClickDelay = true;

        if (icon is not null)
            _trayIcon.Icon = icon;

        _trayIcon.LeftClickCommand = new SimpleCommand(ShowWindow);

        // Update tooltip every 30 seconds (marshal to UI thread)
        _tooltipTimer = new Timer(_ =>
            _dispatcher?.TryEnqueue(UpdateTooltip),
            null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
    }

    private void UpdateTooltip()
    {
        if (_trayIcon is null) return;

        var next = _scheduler.NextPrayer;
        if (next is null)
        {
            _trayIcon.ToolTipText = "Muslim ON";
            return;
        }

        var remaining = next.Time.TimeUntil().ToCountdownString();
        var name = Loc.S($"prayer_{next.Name.ToString().ToLowerInvariant()}");
        _trayIcon.ToolTipText = $"Muslim ON — {name} {Loc.S("until")} {remaining}";
    }

    public void ShowWindow()
    {
        _mainWindow?.Activate();
    }

    public void ExitApplication()
    {
        Dispose();
        Application.Current.Exit();
    }

    public void Dispose()
    {
        _tooltipTimer?.Dispose();
        _trayIcon?.Dispose();
    }

    private sealed class SimpleCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        public SimpleCommand(Action execute) => _execute = execute;
#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
