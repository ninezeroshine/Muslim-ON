using System.Drawing;
using System.Runtime.InteropServices;
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

    // Win32 menu item IDs
    private const int ID_OPEN = 1;
    private const int ID_EXIT = 2;

    public TrayIconManager(ISchedulerService scheduler)
    {
        _scheduler = scheduler;
    }

    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Enable dark context menus to match app theme
        NativeMenu.EnableDarkMode();

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Muslim ON",
            NoLeftClickDelay = true,
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
            _trayIcon.Icon = new Icon(iconPath);

        _trayIcon.LeftClickCommand = new SimpleCommand(ShowWindow);
        _trayIcon.RightClickCommand = new SimpleCommand(ShowNativeContextMenu);

        _trayIcon.ForceCreate();

        _tooltipTimer = new Timer(_ =>
            _dispatcher?.TryEnqueue(UpdateTooltip),
            null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Shows a native Win32 popup menu at the cursor position (next to tray icon).
    /// </summary>
    private void ShowNativeContextMenu()
    {
        _dispatcher?.TryEnqueue(() =>
        {
            var hMenu = NativeMenu.CreatePopupMenu();
            NativeMenu.AppendMenu(hMenu, 0x0000, ID_OPEN, Loc.S("tray_open")); // MF_STRING
            NativeMenu.AppendMenu(hMenu, 0x0800, 0, null);                      // MF_SEPARATOR
            NativeMenu.AppendMenu(hMenu, 0x0000, ID_EXIT, Loc.S("tray_exit")); // MF_STRING

            // Get cursor position for menu placement
            NativeMenu.GetCursorPos(out var pt);

            // Required: set foreground so menu dismisses on click-away
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
            NativeMenu.SetForegroundWindow(hwnd);

            int cmd = NativeMenu.TrackPopupMenu(hMenu,
                0x0100 | 0x0002 | 0x0008, // TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN
                pt.X, pt.Y, 0, hwnd, IntPtr.Zero);

            NativeMenu.DestroyMenu(hMenu);

            if (cmd == ID_OPEN) ShowWindow();
            else if (cmd == ID_EXIT) ExitApplication();
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

    /// <summary>Win32 native context menu API — appears at cursor position near tray.</summary>
    private static class NativeMenu
    {
        [DllImport("user32.dll")] public static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll")] public static extern bool DestroyMenu(IntPtr hMenu);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, nint uIDNewItem, string? lpNewItem);
        [DllImport("user32.dll")]
        public static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT lpPoint);

        // Enable dark mode for context menus (Windows 10 1903+)
        // uxtheme.dll ordinal 135 — used by Explorer, Notepad, Terminal etc.
        [DllImport("uxtheme.dll", EntryPoint = "#135")]
        public static extern int SetPreferredAppMode(int mode); // 0=Default, 1=AllowDark, 2=ForceDark

        [DllImport("uxtheme.dll", EntryPoint = "#136")]
        public static extern void FlushMenuThemes();

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        /// <summary>Call once at startup to enable dark context menus.</summary>
        public static void EnableDarkMode()
        {
            try { SetPreferredAppMode(2); FlushMenuThemes(); } catch { }
        }
    }
}
