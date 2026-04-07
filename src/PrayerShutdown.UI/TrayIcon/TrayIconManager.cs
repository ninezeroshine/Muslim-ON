using System.Drawing;
using H.NotifyIcon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Muslim ON",
            NoLeftClickDelay = true,
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
            _trayIcon.Icon = new Icon(iconPath);

        _trayIcon.LeftClickCommand = new SimpleCommand(ShowWindow);
        _trayIcon.RightClickCommand = new SimpleCommand(ShowTrayMenu);

        _trayIcon.ForceCreate();

        _tooltipTimer = new Timer(_ =>
            _dispatcher?.TryEnqueue(UpdateTooltip),
            null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
    }

    private void ShowTrayMenu()
    {
        _dispatcher?.TryEnqueue(() =>
        {
            if (_mainWindow is null) return;

            // Ensure window is visible so MenuFlyout has a XamlRoot
            _mainWindow.Activate();

            var menu = new MenuFlyout();

            var openItem = new MenuFlyoutItem { Text = Loc.S("tray_open") };
            openItem.Click += (_, _) => ShowWindow();
            menu.Items.Add(openItem);

            menu.Items.Add(new MenuFlyoutSeparator());

            var exitItem = new MenuFlyoutItem
            {
                Text = Loc.S("tray_exit"),
                Icon = new FontIcon { Glyph = "\uE7E8", FontSize = 12 }
            };
            exitItem.Click += (_, _) => ExitApplication();
            menu.Items.Add(exitItem);

            // Show at top-left of content (will appear near tray area)
            if (_mainWindow.Content is FrameworkElement root)
                menu.ShowAt(root, new Windows.Foundation.Point(root.ActualWidth - 160, 0));
        });
    }

    private void UpdateTooltip()
    {
        if (_trayIcon is null) return;
        var next = _scheduler.NextPrayer;
        if (next is null) { _trayIcon.ToolTipText = "Muslim ON"; return; }

        var remaining = next.Time.TimeUntil().ToCountdownString();
        var name = Loc.S($"prayer_{next.Name.ToString().ToLowerInvariant()}");
        _trayIcon.ToolTipText = $"Muslim ON — {name} {Loc.S("until")} {remaining}";
    }

    public void ShowWindow() => _mainWindow?.Activate();

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
