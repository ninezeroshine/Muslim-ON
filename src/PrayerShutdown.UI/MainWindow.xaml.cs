using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using PrayerShutdown.UI.Navigation;
using WinRT.Interop;

namespace PrayerShutdown.UI;

public sealed partial class MainWindow : Window
{
    private readonly NavigationService _navigationService;
    private AppWindow? _appWindow;

    public MainWindow(NavigationService navigationService)
    {
        _navigationService = navigationService;
        InitializeComponent();

        Title = "Muslim ON";
        ExtendsContentIntoTitleBar = true;

        SetWindowSize(900, 640);
        SetWindowIcon();

        RootFrame.Navigate(typeof(ShellPage));
    }

    private void SetWindowSize(int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    private void SetWindowIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (!File.Exists(iconPath)) return;

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow ??= AppWindow.GetFromWindowId(windowId);
            _appWindow.SetIcon(iconPath);

            // Win32 SendMessage for taskbar + Alt+Tab (ExtendsContentIntoTitleBar hides AppWindow icon)
            var hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
            if (hIcon != IntPtr.Zero)
            {
                SendMessage(hwnd, WM_SETICON, ICON_BIG, hIcon);
                SendMessage(hwnd, WM_SETICON, ICON_SMALL, hIcon);
            }
        }
        catch
        {
            // Icon is cosmetic — don't crash if missing
        }
    }

    // Win32 interop for window icon
    private const int WM_SETICON = 0x0080;
    private const nint ICON_SMALL = 0, ICON_BIG = 1;
    private const uint IMAGE_ICON = 1, LR_LOADFROMFILE = 0x10, LR_DEFAULTSIZE = 0x40;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, nint wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);
}
