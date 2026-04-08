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
    private int _shutdownCountdown;
    private DispatcherQueueTimer? _countdownTimer;
    private DispatcherQueueTimer? _clockTimer;

    /// <summary>Whether the full-screen semi-transparent backdrop is visible (Phase 4).</summary>
    public bool IsFullOverlay => _phase == OverlayPhase.Shutdown;

    // Events for the App to handle
    public event EventHandler? PrayedClicked;
    public event EventHandler? DismissClicked;
    public event EventHandler? GoingToPrayClicked;
    public event EventHandler? SnoozeClicked;
    public event EventHandler? ShutdownCountdownFinished;

    public PrayerOverlayWindow()
    {
        InitializeComponent();

        // Set window properties
        Title = Constants.AppName;
        ExtendsContentIntoTitleBar = true;

        // Start clock timer for prayer countdown display
        _clockTimer = DispatcherQueue.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (_, _) => UpdateClockCountdown();
    }

    public void ShowPhase(OverlayPhase phase, PrayerTime prayer, int nudgeNumber = 0, int maxNudges = 0)
    {
        _phase = phase;
        _prayer = prayer;
        _nudgeNumber = nudgeNumber;
        _maxNudges = maxNudges;

        UpdateUI();
        SetTopmost();
        ResizeAndCenter();
        Activate();

        _clockTimer?.Start();

        if (phase == OverlayPhase.Shutdown)
            StartShutdownCountdown();
    }

    public void HideOverlay()
    {
        _clockTimer?.Stop();
        _countdownTimer?.Stop();

        try { Close(); }
        catch { /* window may already be closed */ }
    }

    private void UpdateUI()
    {
        if (_prayer is null) return;

        var prayerName = Loc.S($"prayer_{_prayer.Name.ToString().ToLowerInvariant()}");
        PrayerNameText.Text = prayerName;
        PrayerTimeText.Text = _prayer.Time.ToString("HH:mm");
        PrayedButton.Content = Loc.S("mark_prayed");

        switch (_phase)
        {
            case OverlayPhase.Remind:
                PhaseIcon.Text = "\U0001F54C"; // mosque
                TitleText.Text = Loc.S("prayer_approaching_title");
                DescriptionText.Text = Loc.S("prayer_approaching_desc");
                SecondaryButton.Content = Loc.S("ill_pray_soon");
                SecondaryButton.Visibility = Visibility.Visible;
                SnoozeInfoText.Visibility = Visibility.Collapsed;
                CountdownText.Foreground = FindBrush("GeistBlue700Brush");
                break;

            case OverlayPhase.PrayNow:
                PhaseIcon.Text = "\U0001F9CE"; // kneeling person
                TitleText.Text = Loc.S("prayer_arrived_title");
                DescriptionText.Text = string.Format(Loc.S("prayer_arrived_desc"), prayerName);
                SecondaryButton.Content = Loc.S("going_to_pray");
                SecondaryButton.Visibility = Visibility.Visible;
                SnoozeInfoText.Visibility = Visibility.Collapsed;
                CountdownText.Foreground = FindBrush("GeistWarningBrush");
                break;

            case OverlayPhase.Nudge:
                PhaseIcon.Text = "\u26A0\uFE0F"; // warning
                var isLast = _nudgeNumber >= _maxNudges;
                TitleText.Text = Loc.S("prayer_nudge_title");
                DescriptionText.Text = string.Format(Loc.S("prayer_nudge_desc"), prayerName);

                if (isLast)
                {
                    SnoozeInfoText.Text = Loc.S("final_warning");
                    SnoozeInfoText.Foreground = FindBrush("GeistErrorBrush");
                    SecondaryButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    var snoozesLeft = _maxNudges - _nudgeNumber;
                    SnoozeInfoText.Text = string.Format(Loc.S("snoozes_left"), snoozesLeft);
                    SnoozeInfoText.Foreground = FindBrush("GeistWarningBrush");
                    SecondaryButton.Content = string.Format(Loc.S("snooze"), Constants.NudgeIntervalMinutes);
                    SecondaryButton.Visibility = Visibility.Visible;
                }
                SnoozeInfoText.Visibility = Visibility.Visible;
                CountdownText.Foreground = FindBrush("GeistErrorBrush");
                break;

            case OverlayPhase.Shutdown:
                PhaseIcon.Text = "\U0001F6A8"; // rotating light
                TitleText.Text = Loc.S("shutdown_warning_title");
                _shutdownCountdown = Constants.ShutdownCountdownSeconds;
                DescriptionText.Text = string.Format(Loc.S("shutdown_warning_desc"), _shutdownCountdown);
                SecondaryButton.Visibility = Visibility.Collapsed;
                SnoozeInfoText.Visibility = Visibility.Collapsed;
                CountdownText.Text = $"{_shutdownCountdown}s";
                CountdownText.Foreground = FindBrush("GeistErrorBrush");
                break;
        }

        UpdateClockCountdown();
    }

    private void UpdateClockCountdown()
    {
        if (_prayer is null || _phase == OverlayPhase.Shutdown) return;

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
        _countdownTimer.Tick += (_, _) =>
        {
            _shutdownCountdown--;
            CountdownText.Text = $"{_shutdownCountdown}s";
            DescriptionText.Text = string.Format(Loc.S("shutdown_warning_desc"), _shutdownCountdown);

            if (_shutdownCountdown <= 0)
            {
                _countdownTimer?.Stop();
                ShutdownCountdownFinished?.Invoke(this, EventArgs.Empty);
            }
        };
        _countdownTimer.Start();
    }

    // ── Button handlers ──

    private void OnPrayedClicked(object sender, RoutedEventArgs e)
    {
        PrayedClicked?.Invoke(this, EventArgs.Empty);
    }

    private void OnSecondaryClicked(object sender, RoutedEventArgs e)
    {
        switch (_phase)
        {
            case OverlayPhase.Remind:
                DismissClicked?.Invoke(this, EventArgs.Empty);
                break;
            case OverlayPhase.PrayNow:
                GoingToPrayClicked?.Invoke(this, EventArgs.Empty);
                break;
            case OverlayPhase.Nudge:
                SnoozeClicked?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    // ── Win32 Topmost ──

    private void SetTopmost()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);

        // Also set as foreground window
        NativeMethods.SetForegroundWindow(hwnd);
    }

    private void ResizeAndCenter()
    {
        var appWindow = GetAppWindow();
        if (appWindow is null) return;

        // Get display area
        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        int width, height;
        if (_phase == OverlayPhase.Shutdown)
        {
            // Full screen for shutdown phase
            width = workArea.Width;
            height = workArea.Height;
            appWindow.Move(new Windows.Graphics.PointInt32(workArea.X, workArea.Y));
        }
        else
        {
            // Centered card
            width = 500;
            height = 520;
            var x = workArea.X + (workArea.Width - width) / 2;
            var y = workArea.Y + (workArea.Height - height) / 2;
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }

        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

        // Remove title bar buttons for cleaner look
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }
    }

    private AppWindow? GetAppWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    private Microsoft.UI.Xaml.Media.Brush FindBrush(string key)
    {
        if (Content is FrameworkElement fe && fe.Resources.TryGetValue(key, out var brush))
            return (Microsoft.UI.Xaml.Media.Brush)brush;

        // Fallback: try from theme dictionaries in merged resources
        if (Content is FrameworkElement el)
        {
            foreach (var dict in el.Resources.MergedDictionaries)
            {
                if (dict.ThemeDictionaries.Count > 0)
                {
                    var themeKey = Application.Current.RequestedTheme == ApplicationTheme.Dark ? "Dark" : "Light";
                    if (dict.ThemeDictionaries.TryGetValue(themeKey, out var themeDict) &&
                        themeDict is ResourceDictionary rd && rd.TryGetValue(key, out var themeBrush))
                        return (Microsoft.UI.Xaml.Media.Brush)themeBrush;
                }
            }
        }

        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
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
