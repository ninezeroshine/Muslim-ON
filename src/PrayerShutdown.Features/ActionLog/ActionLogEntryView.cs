using PrayerShutdown.Common.Localization;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;

namespace PrayerShutdown.Features.ActionLog;

/// <summary>View-model projection of one <see cref="ActionLogEntry"/>.</summary>
public sealed class ActionLogEntryView
{
    public required ActionLogEntry Source { get; init; }

    public string TimeFormatted => Source.Timestamp.ToString("HH:mm:ss");
    public string PrayerName => Loc.S($"prayer_{Source.Prayer.ToString().ToLowerInvariant()}");

    public string EventLabel
    {
        get
        {
            var key = $"log_event_{Source.Event}";
            var localized = Loc.S(key);
            return localized == key ? Source.Event : localized;
        }
    }

    public string Detail => Source.Detail;
    public bool HasDetail => !string.IsNullOrEmpty(Source.Detail);

    /// <summary>Segoe MDL2 glyph per event family.</summary>
    public string Icon => Source.Event switch
    {
        "Remind_Fired" => "\uE787",                         // Alarm
        "PrayNow_Fired" => "\uE13D",                        // Clock
        var e when e.StartsWith("Nudge_") => "\uE7E7",      // Warning
        "Shutdown_Triggered" => "\uE7E8",                   // Power
        "Shutdown_SafetyNet_Fired" => "\uE7E8",
        "Shutdown_Shutdown" => "\uE7E8",
        "Shutdown_Sleep" => "\uEC46",                       // Brightness
        "Shutdown_Hibernate" => "\uE945",                   // Snooze
        "Shutdown_Lock" => "\uE72E",                        // Lock
        "Shutdown_Cancelled" => "\uE711",                   // Cancel
        "MarkedAsPrayed" => "\uE73E",                       // Check
        "Snoozed" => "\uE823",                              // Refresh
        "GoingToPray" => "\uE805",                          // Go
        "Overlay_Shown" => "\uE7B3",                        // Eye
        "Overlay_Closed" => "\uE711",
        "ToastDismissed" => "\uE7C2",
        _ => "\uE946",                                      // Info
    };

    public string AccentColor => Source.Event switch
    {
        "Remind_Fired" or "PrayNow_Fired" => "#3B82F6",
        var e when e.StartsWith("Nudge_") => "#F59E0B",
        "Shutdown_Triggered" or "Shutdown_SafetyNet_Fired"
            or "Shutdown_Shutdown" or "Shutdown_Sleep"
            or "Shutdown_Hibernate" or "Shutdown_Lock" => "#EF4444",
        "MarkedAsPrayed" or "Shutdown_Cancelled" => "#22C55E",
        "Snoozed" => "#F59E0B",
        _ => "#737373",
    };
}
