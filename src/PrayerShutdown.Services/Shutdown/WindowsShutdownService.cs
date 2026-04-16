using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Services.Shutdown;

public sealed class WindowsShutdownService : IShutdownService
{
    private readonly ILogger<WindowsShutdownService> _logger;
    private bool _hasPending;

    public bool HasPendingShutdown => _hasPending;

    public WindowsShutdownService(ILogger<WindowsShutdownService> logger)
    {
        _logger = logger;
    }

    public void Execute(ShutdownAction action)
    {
        _logger.LogWarning("Executing {Action}", action);

        switch (action)
        {
            case ShutdownAction.Shutdown:
                _hasPending = true;
                StartProcess("shutdown", "/s /t 60 /c \"Muslim ON: Time for prayer.\"");
                break;
            case ShutdownAction.Hibernate:
                SetSuspendState(hibernate: true);
                break;
            case ShutdownAction.Sleep:
                SetSuspendState(hibernate: false);
                break;
            case ShutdownAction.Lock:
                LockWorkstation();
                break;
            case ShutdownAction.None:
                _logger.LogInformation("Execute({Action}) — no-op", action);
                break;
        }
    }

    public void CancelPendingShutdown()
    {
        if (!_hasPending) return;
        _logger.LogInformation("Cancelling pending shutdown");
        _hasPending = false;
        StartProcess("shutdown", "/a");
    }

    private void StartProcess(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute {FileName} {Arguments}", fileName, arguments);
        }
    }

    private void SetSuspendState(bool hibernate)
    {
        try { NativeMethods.SetSuspendState(hibernate, true, true); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to set suspend state (hibernate={Hibernate})", hibernate); }
    }

    private void LockWorkstation()
    {
        try { NativeMethods.LockWorkStation(); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to lock workstation"); }
    }

    private static class NativeMethods
    {
        [DllImport("PowrProf.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetSuspendState(
            [MarshalAs(UnmanagedType.Bool)] bool hibernate,
            [MarshalAs(UnmanagedType.Bool)] bool forceCritical,
            [MarshalAs(UnmanagedType.Bool)] bool disableWakeEvent);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LockWorkStation();
    }
}
