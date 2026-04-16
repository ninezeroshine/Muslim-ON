using System.Collections.ObjectModel;
using PrayerShutdown.Common.Localization;

namespace PrayerShutdown.Features.ActionLog;

/// <summary>Entries grouped by local-day bucket with a localized header.</summary>
public sealed class ActionLogDayGroup
{
    public required string Header { get; init; }
    public required DateOnly Day { get; init; }
    public required ObservableCollection<ActionLogEntryView> Entries { get; init; }

    public static string FormatHeader(DateOnly day, DateOnly today)
    {
        if (day == today) return Loc.S("log_group_today");
        if (day == today.AddDays(-1)) return Loc.S("log_group_yesterday");
        return day.ToDateTime(TimeOnly.MinValue).ToString("dddd, d MMMM");
    }
}
