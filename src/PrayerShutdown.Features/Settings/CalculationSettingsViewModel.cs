using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Features.Settings;

public partial class CalculationSettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly ISchedulerService _scheduler;

    [ObservableProperty]
    private CalculationMethod _selectedMethod = CalculationMethod.MWL;

    [ObservableProperty]
    private AsrJuristic _selectedAsrMethod = AsrJuristic.Shafi;

    [ObservableProperty]
    private HighLatitudeRule _selectedHighLatRule = HighLatitudeRule.AngleBased;

    public IReadOnlyList<CalculationMethod> AvailableMethods { get; } =
        Enum.GetValues<CalculationMethod>();

    public IReadOnlyList<AsrJuristic> AvailableAsrMethods { get; } =
        Enum.GetValues<AsrJuristic>();

    public IReadOnlyList<HighLatitudeRule> AvailableHighLatRules { get; } =
        Enum.GetValues<HighLatitudeRule>();

    public CalculationSettingsViewModel(
        ISettingsRepository settingsRepo,
        ISchedulerService scheduler)
    {
        _settingsRepo = settingsRepo;
        _scheduler = scheduler;
    }

    public async Task LoadAsync()
    {
        var settings = await _settingsRepo.LoadAsync();
        SelectedMethod = settings.Calculation.Method;
        SelectedAsrMethod = settings.Calculation.AsrMethod;
        SelectedHighLatRule = settings.Calculation.HighLatRule;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var settings = await _settingsRepo.LoadAsync();
        settings.Calculation.Method = SelectedMethod;
        settings.Calculation.AsrMethod = SelectedAsrMethod;
        settings.Calculation.HighLatRule = SelectedHighLatRule;
        await _settingsRepo.SaveAsync(settings);
        _scheduler.RecalculateSchedule();
    }
}
