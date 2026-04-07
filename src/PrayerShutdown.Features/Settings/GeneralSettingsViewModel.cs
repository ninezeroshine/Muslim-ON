using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Features.Settings;

public partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepo;
    private readonly ILocationService _locationService;
    private readonly ISchedulerService _scheduler;

    /// <summary>Dashboard subscribes to refresh after settings save.</summary>
    public event Func<Task>? OnSettingsSaved;

    [ObservableProperty] private string _selectedCity = "";
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private bool _isDetecting;
    [ObservableProperty] private CalculationMethod _selectedMethod = CalculationMethod.MWL;
    [ObservableProperty] private AsrJuristic _selectedAsrMethod = AsrJuristic.Shafi;
    [ObservableProperty] private HighLatitudeRule _selectedHighLatRule = HighLatitudeRule.AngleBased;
    [ObservableProperty] private bool _enableNotifications = true;
    [ObservableProperty] private bool _enableAdhan;
    [ObservableProperty] private bool _shutdownFajr;
    [ObservableProperty] private bool _shutdownDhuhr = true;
    [ObservableProperty] private bool _shutdownAsr = true;
    [ObservableProperty] private bool _shutdownMaghrib = true;
    [ObservableProperty] private bool _shutdownIsha;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private int _selectedThemeIndex;
    [ObservableProperty] private int _selectedLanguageIndex;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _showStatus;

    public IReadOnlyList<LocationInfo> Cities => _locationService.GetPresetCities();
    public IReadOnlyList<LanguageOption> AvailableLanguages => LocalizationService.AvailableLanguages;

    public GeneralSettingsViewModel(
        ISettingsRepository settingsRepo,
        ILocationService locationService,
        ISchedulerService scheduler)
    {
        _settingsRepo = settingsRepo;
        _locationService = locationService;
        _scheduler = scheduler;
    }

    public async Task LoadAsync()
    {
        var s = await _settingsRepo.LoadAsync();

        SelectedCity = s.Location.SelectedLocation?.CityName ?? Loc.S("location_not_set");
        SelectedMethod = s.Calculation.Method;
        SelectedAsrMethod = s.Calculation.AsrMethod;
        SelectedHighLatRule = s.Calculation.HighLatRule;
        EnableNotifications = s.Notification.EnableToastNotifications;
        EnableAdhan = s.Notification.EnableAdhanSound;
        StartWithWindows = s.StartWithWindows;
        StartMinimized = s.StartMinimized;

        // Language
        var langIdx = AvailableLanguages
            .Select((l, i) => (l, i))
            .FirstOrDefault(x => x.l.Code == s.Language).i;
        SelectedLanguageIndex = langIdx;

        // Shutdown rules
        foreach (var rule in s.Shutdown.Rules)
        {
            switch (rule.Prayer)
            {
                case PrayerName.Fajr: ShutdownFajr = rule.IsEnabled; break;
                case PrayerName.Dhuhr: ShutdownDhuhr = rule.IsEnabled; break;
                case PrayerName.Asr: ShutdownAsr = rule.IsEnabled; break;
                case PrayerName.Maghrib: ShutdownMaghrib = rule.IsEnabled; break;
                case PrayerName.Isha: ShutdownIsha = rule.IsEnabled; break;
            }
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var s = await _settingsRepo.LoadAsync();

        s.Calculation.Method = SelectedMethod;
        s.Calculation.AsrMethod = SelectedAsrMethod;
        s.Calculation.HighLatRule = SelectedHighLatRule;
        s.Notification.EnableToastNotifications = EnableNotifications;
        s.Notification.EnableAdhanSound = EnableAdhan;
        s.StartWithWindows = StartWithWindows;
        s.StartMinimized = StartMinimized;

        // Language
        if (SelectedLanguageIndex >= 0 && SelectedLanguageIndex < AvailableLanguages.Count)
        {
            var lang = AvailableLanguages[SelectedLanguageIndex].Code;
            s.Language = lang;
            LocalizationService.Instance.SetLanguage(lang);
        }

        s.Shutdown.Rules = new()
        {
            new(PrayerName.Fajr, ShutdownFajr),
            new(PrayerName.Dhuhr, ShutdownDhuhr),
            new(PrayerName.Asr, ShutdownAsr),
            new(PrayerName.Maghrib, ShutdownMaghrib),
            new(PrayerName.Isha, ShutdownIsha),
        };

        await _settingsRepo.SaveAsync(s);
        _scheduler.RecalculateSchedule();

        // Refresh dashboard prayer times with new calculation method
        if (OnSettingsSaved is not null)
            await OnSettingsSaved.Invoke();

        StatusMessage = Loc.S("settings_saved");
        ShowStatus = true;
        _ = HideStatusAfterDelay();
    }

    [RelayCommand]
    private async Task SelectCityAsync(LocationInfo city)
    {
        var s = await _settingsRepo.LoadAsync();
        s.Location.SelectedLocation = city;
        await _settingsRepo.SaveAsync(s);
        SelectedCity = city.CityName;
        _scheduler.RecalculateSchedule();

        StatusMessage = $"Location set to {city.CityName}";
        ShowStatus = true;
        _ = HideStatusAfterDelay();
    }

    private async Task HideStatusAfterDelay()
    {
        await Task.Delay(2000);
        ShowStatus = false;
    }
}
