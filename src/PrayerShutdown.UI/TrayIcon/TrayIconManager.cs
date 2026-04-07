using H.NotifyIcon;
using Microsoft.UI.Xaml;
using PrayerShutdown.Core.Extensions;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.UI.TrayIcon;

public sealed class TrayIconManager : IDisposable
{
    private readonly ISchedulerService _scheduler;
    private TaskbarIcon? _trayIcon;
    private Timer? _tooltipTimer;
    private Window? _mainWindow;

    public TrayIconManager(ISchedulerService scheduler)
    {
        _scheduler = scheduler;
    }

    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Muslim ON",
            NoLeftClickDelay = true,
        };

        _trayIcon.LeftClickCommand = new RelayCommand(ShowWindow);

        // Update tooltip every 30 seconds
        _tooltipTimer = new Timer(_ => UpdateTooltip(), null,
            TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    private void UpdateTooltip()
    {
        var next = _scheduler.NextPrayer;
        if (next is null)
        {
            _trayIcon!.ToolTipText = "Muslim ON";
            return;
        }

        var remaining = next.Time.TimeUntil().ToCountdownString();
        _trayIcon!.ToolTipText = $"Next: {next.Name} in {remaining}";
    }

    public void ShowWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Activate();
    }

    public void Dispose()
    {
        _tooltipTimer?.Dispose();
        _trayIcon?.Dispose();
    }

    private sealed class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
