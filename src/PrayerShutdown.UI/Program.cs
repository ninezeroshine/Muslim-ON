using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace PrayerShutdown.UI;

/// <summary>
/// Application entry point with single-instance enforcement.
///
/// Replaces the auto-generated Main (see <c>DisableXamlGeneratedMain=true</c> in csproj)
/// to register the app as single-instance via <see cref="AppInstance.FindOrRegisterForKey"/>.
///
/// Why this matters:
/// <list type="bullet">
/// <item>Without it, every shortcut click / Start menu launch / auto-start spawns a new
///   process. Each runs its own scheduler, timers, tray icon and SQLite connection.</item>
/// <item>Auto-update fails when zombie instances hold .xbf/.dll files open. v1.2.9
///   added <c>Stop-Process -Name</c> + robocopy as defence-in-depth, but this fixes
///   the root cause: there can only be one process to begin with.</item>
/// </list>
///
/// Pattern source: Microsoft official docs
/// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance
/// </summary>
internal static class Program
{
    /// <summary>
    /// Stable key used by every Muslim ON process to find each other.
    /// Must not collide with any other app on the machine.
    /// </summary>
    private const string SingleInstanceKey = "MuslimON-Main-SingleInstance";

    /// <summary>
    /// Windows App SDK version we depend on (matches <c>Microsoft.WindowsAppSDK</c>
    /// version in <c>Directory.Packages.props</c>). Format: <c>0xMMmmRRRR</c>.
    /// 0x00010005 = 1.5.
    /// </summary>
    private const uint WindowsAppSdkVersion = 0x00010005;

    [STAThread]
    private static int Main(string[] args)
    {
        // Bootstrap the Windows App Runtime into our process. The auto-generated Main
        // does this via an MSBuild target; with our custom Main we have to do it
        // ourselves. Calling Initialize twice is a no-op, so it is safe even if a
        // future MSBuild change starts injecting it again.
        Bootstrap.Initialize(WindowsAppSdkVersion);

        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            if (DecideRedirection())
            {
                // We are a secondary instance — the launch arguments have been
                // forwarded to the main instance. Exit cleanly.
                return 0;
            }

            Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });

            return 0;
        }
        finally
        {
            Bootstrap.Shutdown();
        }
    }

    /// <summary>
    /// Returns <c>true</c> if this process is NOT the primary instance and has
    /// successfully redirected its activation to the existing one. Caller must
    /// exit immediately when this returns <c>true</c>.
    /// </summary>
    private static bool DecideRedirection()
    {
        var keyInstance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);

        if (keyInstance.IsCurrent)
        {
            // We are the primary instance — listen for redirected activations
            // from any future secondary processes.
            keyInstance.Activated += OnReactivated;
            return false;
        }

        // Another instance already owns the key — forward our activation args
        // to it and signal the caller to exit.
        var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        RedirectActivationTo(activationArgs, keyInstance);
        return true;
    }

    /// <summary>
    /// Synchronous wrapper around the WinRT-async <c>RedirectActivationToAsync</c>.
    /// A static <c>Main</c> cannot be <c>async</c>, so we block on a worker thread
    /// instead of risking deadlock on the STA thread.
    /// </summary>
    private static void RedirectActivationTo(AppActivationArguments args, AppInstance target)
    {
        using var done = new ManualResetEvent(false);
        Task.Run(() =>
        {
            try
            {
                target.RedirectActivationToAsync(args).AsTask().Wait();
            }
            finally
            {
                done.Set();
            }
        });
        done.WaitOne();
    }

    /// <summary>
    /// Fired in the primary instance when a secondary launch redirects its
    /// activation to us. Hands the request off to <see cref="App"/> which marshals
    /// to the UI thread and brings the main window forward.
    /// </summary>
    private static void OnReactivated(object? sender, AppActivationArguments args)
    {
        try
        {
            (Application.Current as App)?.OnReactivated();
        }
        catch
        {
            // Re-activation is a UI nicety; never crash the primary instance because of it.
        }
    }
}
