using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Core.Domain.Enums;

namespace PrayerShutdown.Features.PrayerDashboard;

public partial class PrayerCardModel : ObservableObject
{
    private CancellationTokenSource? _undoCts;

    public PrayerName Name { get; init; }
    public DateTime Time { get; init; }
    public int ShutdownDelayMinutes { get; init; } = 15;
    public string TimeFormatted => Time.ToString("HH:mm");

    [ObservableProperty] private bool _isNext;
    [ObservableProperty] private bool _shutdownEnabled;
    [ObservableProperty] private bool _isPrayed;
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private int _undoSecondsLeft;

    public bool IsPassed => Time < DateTime.Now;
    public bool IsInformational => Name == PrayerName.Sunrise;
    public bool IsActionable => !IsInformational;
    public bool CanMarkPrayed => IsActionable && !IsPrayed && !CanUndo;

    public string LocalizedName => Loc.S($"prayer_{Name.ToString().ToLowerInvariant()}");
    public string PrayedTooltip => Loc.S("mark_prayed");
    public string UndoTooltip => $"{Loc.S("undo")} ({UndoSecondsLeft})";

    public string StatusText =>
        IsPrayed || CanUndo ? Loc.S("prayed_check") :
        IsInformational ? "" :
        IsPassed ? Loc.S("status_passed") :
        IsNext ? Loc.S("status_now") :
        Loc.S("status_upcoming");

    public string ShutdownBadgeText =>
        !IsActionable || IsPassed || IsPrayed ? "" :
        ShutdownEnabled ? $"\u26A1 {Loc.S("shutdown_at")} {Time.AddMinutes(ShutdownDelayMinutes):HH:mm}" : "";

    // Time until this prayer (for upcoming non-next prayers)
    public string TimeUntilText
    {
        get
        {
            if (IsInformational || IsPassed || IsNext || IsPrayed) return "";
            var delta = Time - DateTime.Now;
            if (delta.TotalMinutes < 1) return "";
            return delta.TotalHours >= 1
                ? $"{Loc.S("until")} {(int)delta.TotalHours}h {delta.Minutes}m"
                : $"{Loc.S("until")} {(int)delta.TotalMinutes}m";
        }
    }

    public bool HasTimeUntil => !string.IsNullOrEmpty(TimeUntilText);

    public string StatusColor =>
        IsPrayed || CanUndo ? ColorTokens.Success :
        IsPassed ? ColorTokens.Gray :
        IsNext ? ColorTokens.Blue : ColorTokens.Gray;

    public double CardOpacity => IsPassed && !IsPrayed ? 0.55 : 1.0;

    public string AccentColor =>
        IsPrayed || CanUndo ? ColorTokens.Success :
        IsPassed ? ColorTokens.GrayLight :
        IsNext ? ColorTokens.Blue : ColorTokens.GrayLighter;

    public int AccentWidth => IsPrayed || CanUndo || IsNext ? 4 : 3;
    public string ShutdownIconColor => ShutdownEnabled ? ColorTokens.Error : ColorTokens.GrayLight;

    [RelayCommand]
    private void ToggleShutdown()
    {
        ShutdownEnabled = !ShutdownEnabled;
        RefreshState();
    }

    [RelayCommand]
    private async Task MarkAsPrayedAsync()
    {
        _undoCts?.Cancel();
        _undoCts = new CancellationTokenSource();
        var token = _undoCts.Token;

        CanUndo = true;
        UndoSecondsLeft = 5;
        RefreshState();

        try
        {
            // Countdown 5→0 with visual feedback
            for (int i = 5; i > 0; i--)
            {
                UndoSecondsLeft = i;
                OnPropertyChanged(nameof(UndoTooltip));
                await Task.Delay(1000, token);
            }

            CanUndo = false;
            IsPrayed = true;
            RefreshState();
        }
        catch (TaskCanceledException)
        {
            // Undo pressed
        }
    }

    [RelayCommand]
    private void UndoPrayed()
    {
        _undoCts?.Cancel();
        CanUndo = false;
        IsPrayed = false;
        RefreshState();
    }

    public void RefreshState()
    {
        OnPropertyChanged(nameof(IsPassed));
        OnPropertyChanged(nameof(CanMarkPrayed));
        OnPropertyChanged(nameof(LocalizedName));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ShutdownBadgeText));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(CardOpacity));
        OnPropertyChanged(nameof(AccentColor));
        OnPropertyChanged(nameof(AccentWidth));
        OnPropertyChanged(nameof(ShutdownIconColor));
        OnPropertyChanged(nameof(TimeUntilText));
        OnPropertyChanged(nameof(HasTimeUntil));
        OnPropertyChanged(nameof(UndoTooltip));
    }
}
