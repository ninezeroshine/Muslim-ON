using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Features.ActionLog;

public partial class ActionLogViewModel : ObservableObject
{
    private const int MaxEntriesDisplayed = 300;

    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty] private ObservableCollection<ActionLogDayGroup> _days = new();
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _isLoading;

    public ActionLogViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<IActionLogger>();
            var entries = await logger.GetRecentAsync(MaxEntriesDisplayed);

            var today = DateOnly.FromDateTime(DateTime.Today);
            var groups = entries
                .GroupBy(e => DateOnly.FromDateTime(e.Timestamp))
                .OrderByDescending(g => g.Key)
                .Select(g => new ActionLogDayGroup
                {
                    Day = g.Key,
                    Header = ActionLogDayGroup.FormatHeader(g.Key, today),
                    Entries = new ObservableCollection<ActionLogEntryView>(
                        g.OrderByDescending(e => e.Timestamp)
                         .Select(e => new ActionLogEntryView { Source = e })),
                });

            Days = new ObservableCollection<ActionLogDayGroup>(groups);
            IsEmpty = Days.Count == 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ClearAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<IActionLogger>();
        await logger.ClearAsync();
        await RefreshAsync();
    }
}
