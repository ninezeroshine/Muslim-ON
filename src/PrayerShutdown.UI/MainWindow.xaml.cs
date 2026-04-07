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
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow ??= AppWindow.GetFromWindowId(windowId);

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                _appWindow.SetIcon(iconPath);
            }
        }
        catch
        {
            // Icon is cosmetic — don't crash if missing
        }
    }
}
