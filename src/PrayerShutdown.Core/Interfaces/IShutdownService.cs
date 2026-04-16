using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Core.Interfaces;

public interface IShutdownService
{
    /// <summary>
    /// Dispatch based on the rule's configured action. Shutdown does a delayed
    /// <c>shutdown /s /t 60</c>; Sleep/Hibernate/Lock are immediate Win32 calls.
    /// Returns immediately — the OS may take seconds to act.
    /// </summary>
    void Execute(ShutdownAction action);

    void CancelPendingShutdown();

    /// <summary>True while <c>shutdown /s /t 60</c> is armed and cancellable via /a.</summary>
    bool HasPendingShutdown { get; }
}
