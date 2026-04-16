using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using PrayerShutdown.Common;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using WinRT.Interop;

namespace PrayerShutdown.UI.Dialogs;

public sealed partial class PrayerOverlayWindow : Window
{
    private OverlayPhase _phase;
    private PrayerTime? _prayer;
    private int _nudgeNumber;
    private int _maxNudges;
    private ShutdownAction _shutdownAction = ShutdownAction.Shutdown;
    private int _shutdownCountdown;
    private DispatcherQueueTimer? _countdownTimer;
    private DispatcherQueueTimer? _clockTimer;
    private bool _isClosed;

    public event EventHandler? PrayedClicked;
    public event EventHandler? DismissClicked;
    public event EventHandler? GoingToPrayClicked;
    public event EventHandler? SnoozeClicked;
    public event EventHandler? ShutdownCountdownFinished;

    public PrayerOverlayWindow()
    {
        InitializeComponent();

        Title = Constants.AppName;
        ExtendsContentIntoTitleBar = true;

        _clockTimer = DispatcherQueue.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (_, _) => SafeUpdateUI(UpdateClockCountdown);

        Closed += (_, _) => _isClosed = true;
    }

    public void ShowPhase(OverlayPhase phase, PrayerTime prayer,
        int nudgeNumber = 0, int maxNudges = 0,
        ShutdownAction shutdownAction = ShutdownAction.Shutdown)
    {
        _phase = phase;
        _prayer = prayer;
        _nudgeNumber = nudgeNumber;
        _maxNudges = maxNudges;
        _shutdownAction = shutdownAction;

        UpdateUI();
        SafeWin32(SetTopmost);
        SafeWin32(ResizeAndCenter);
        Activate();

        _clockTimer?.Start();

        if (phase == OverlayPhase.Shutdown)
            StartShutdownCountdown();
    }

    public void HideOverlay()
    {
        _clockTimer?.Stop();
        _countdownTimer?.Stop();
        _isClosed = true;

        try { Close(); }
        catch { /* window may already be closed */ }
    }

    private void UpdateUI()
    {
        if (_prayer is null || _isClosed) return;

        var prayerName = Loc.S($"prayer_{_prayer.Name.ToString().ToLowerInvariant()}");
        PrayerNameText.Text = prayerName;
        PrayerTimeText.Text = _prayer.Time.ToString("HH:mm");
        PrayedButton.Content = Loc.S("mark_prayed");

        BackdropBorder.Visibility = _phase == OverlayPhase.Shutdown
            ? Visibility.Visible : Visibility.Collapsed;

        switch (_phase)
        {
            case OverlayPhase.Remind:
                PhaseIcon.Text = "\U0001F54C";
                TitleText.Text = Loc.S("prayer_approaching_title");
                DescriptionText.Text = Loc.S("prayer_approaching_desc");
                SecondaryButton.Content = Loc.S("ill_pray_soon");
                SecondaryButton.Visibility = Visibility.Visible;
                SnoozeInfoText.Visibility = Visibility.Collapsed;
                SetCountdownColor("GeistBlue700Brush");
                break;

            case OverlayPhase.PrayNow:
                PhaseIcon.Text = "\U0001F9CE";
                TitleText.Text = Loc.S("prayer_arrived_title");
                DescriptionText.Text = string.Format(Loc.S("prayer_arrived_desc"), prayerName);
                SecondaryButton.Content = Loc.S("going_to_pray");
                SecondaryButton.Visibility = Visibility.Visible;
                SnoozeInfoText.Visibility = Visibility.Collapsed;
                SetCountdownColor("GeistWarningBrush");
                break;

            case OverlayPhase.Nudge:
                PhaseIcon.Text = "\u26A0\uFE0F";
                var isLast = _nudgeNumber >= _maxNudges;
                TitleText.Text = Loc.S("prayer_nudge_title");
                DescriptionText.Text = string.Format(Loc.S("prayer_nudge_desc"), prayerName);

                if (isLast)
                {
                    SnoozeInfoText.Text = Loc.S("final_warning");
                    SetSnoozeColor("GeistErrorBrush");
                    SecondaryButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    var snoozesLeft = _maxNudges - _nudgeNumber;
                    SnoozeInfoText.Text = string.Format(Loc.S("snoozes_left"), snoozesLeft);
                    SetSnoozeColor("GeistWarningBrush");
                    SecondaryButton.Content = string.Format(Loc.S("snooze"), Constants.NudgeIntervalMinutes);
                    SecondaryButton.Visibility = Visibility.Visible;
                }
                SnoozeInfoText.Visibility = Visibility.Visible;
                SetCountdownColor("GeistErrorBrush");
                break;

            case OverlayPhase.Shutdown:
                PhaseIcon.Text = "\U0001F6A8";
                TitleText.Text = GetShutdownTitle(_shutdownAction);
                _shutdownCountdown = Constants.ShutdownCountdownSeconds;
                DescriptionText.Text = string.Format(GetShutdownDescKey(_shutdownAction), _shutdownCountdown);
                SecondaryButton.Visibility = Visibility.Collapsed;
                SnoozeInfoText.Visibility = Visibility.Collapsed;
                CountdownText.Text = $"{_shutdownCountdown}s";
                SetCountdownColor("GeistErrorBrush");
                break;
        }

        UpdateClockCountdown();
    }

    private static string GetShutdownTitle(ShutdownAction action) => action switch
    {
        ShutdownAction.Sleep => Loc.S("action_sleep_title"),
        ShutdownAction.Hibernate => Loc.S("action_hibernate_title"),
        ShutdownAction.Lock => Loc.S("action_lock_title"),
        _ => Loc.S("shutdown_warning_title"),
    };

    private static string GetShutdownDescKey(ShutdownAction action) => action switch
    {
        ShutdownAction.Sleep => Loc.S("action_sleep_desc"),
        ShutdownAction.Hibernate => Loc.S("action_hibernate_desc"),
        ShutdownAction.Lock => Loc.S("action_lock_desc"),
        _ => Loc.S("shutdown_warning_desc"),
    };

    private void UpdateClockCountdown()
    {
        if (_prayer is null || _phase == OverlayPhase.Shutdown || _isClosed) return;

        var diff = _prayer.Time - DateTime.Now;
        if (diff.TotalSeconds > 0)
            CountdownText.Text = diff.TotalHours >= 1
                ? $"-{(int)diff.TotalHours}:{diff.Minutes:D2}:{diff.Seconds:D2}"
                : $"-{diff.Minutes:D2}:{diff.Seconds:D2}";
        else
        {
            var elapsed = DateTime.Now - _prayer.Time;
            CountdownText.Text = $"+{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
        }
    }

    private void StartShutdownCountdown()
    {
        _shutdownCountdown = Constants.ShutdownCountdownSeconds;
        _countdownTimer?.Stop();
        _countdownTimer = DispatcherQueue.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.Tick += (_, _) => SafeUpdateUI(() =>
        {
            _shutdownCountdown--;
            CountdownText.Text = $"{_shutdownCountdown}s";
            DescriptionText.Text = string.Format(GetShutdownDescKey(_shutdownAction), _shutdownCountdown);

            if (_shutdownCountdown <= 0)
            {
                _countdownTimer?.Stop();
                ShutdownCountdownFinished?.Invoke(this, EventArgs.Empty);
            }
        });
        _countdownTimer.Start();
    }

    // ── Button handlers ──

    private void OnPrayedClicked(object sender, RoutedEventArgs e)
        => PrayedClicked?.Invoke(this, EventArgs.Empty);

    private void OnSecondaryClicked(object sender, RoutedEventArgs e)
    {
        switch (_phase)
        {
            case OverlayPhase.Remind: DismissClicked?.Invoke(this, EventArgs.Empty); break;
            case OverlayPhase.PrayNow: GoingToPrayClicked?.Invoke(this, EventArgs.Empty); break;
            case OverlayPhase.Nudge: SnoozeClicked?.Invoke(this, EventArgs.Empty); break;
        }
    }

    // ── Safe helpers ──

    private void SafeUpdateUI(Action action)
    {
        if (_isClosed) return;
        try { action(); }
        catch (Exception ex) { LogError("UI update", ex); }
    }

    private static void SafeWin32(Action action)
    {
        try { action(); }
        catch (Exception ex) { LogError("Win32", ex); }
    }

    private void SetCountdownColor(string brushKey)
    {
        try
        {
            if (Content is FrameworkElement fe && fe.Resources.TryGetValue(brushKey, out var brush))
                CountdownText.Foreground = (Microsoft.UI.Xaml.Media.Brush)brush;
        }
        catch { }
    }

    private void SetSnoozeColor(string brushKey)
    {
        try
        {
            if (Content is FrameworkElement fe && fe.Resources.TryGetValue(brushKey, out var brush))
                SnoozeInfoText.Foreground = (Microsoft.UI.Xaml.Media.Brush)brush;
        }
        catch { }
    }

    // ── Win32 Topmost ──

    private void SetTopmost()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero) return;

        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
        NativeMethods.SetForegroundWindow(hwnd);
    }

    private void ResizeAndCenter()
    {
        var appWindow = GetAppWindow();
        if (appWindow is null) return;

        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        int width, height;
        if (_phase == OverlayPhase.Shutdown)
        {
            width = workArea.Width;
            height = workArea.Height;
            appWindow.Move(new Windows.Graphics.PointInt32(workArea.X, workArea.Y));
        }
        else
        {
            width = 500;
            height = 520;
            var x = workArea.X + (workArea.Width - width) / 2;
            var y = workArea.Y + (workArea.Height - height) / 2;
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }

        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }
    }

    private AppWindow? GetAppWindow()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd == IntPtr.Zero) return null;
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }
        catch { return null; }
    }

    private static void LogError(string context, Exception ex)
    {
        try
        {
            var logPath = Path.Combine(Constants.AppDataPath, "logs", "overlay_errors.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}] {ex}\n");
        }
        catch { }
    }

    private static class NativeMethods
    {
        public static readonly IntPtr HWND_TOPMOST = new(-1);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
