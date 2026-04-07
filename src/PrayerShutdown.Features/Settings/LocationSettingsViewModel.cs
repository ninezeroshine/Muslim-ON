using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Features.Settings;

public partial class LocationSettingsViewModel : ObservableObject
{
    private readonly ILocationService _locationService;
    private readonly ISettingsRepository _settingsRepo;

    [ObservableProperty]
    private ObservableCollection<LocationInfo> _searchResults = new();

    [ObservableProperty]
    private LocationInfo? _selectedLocation;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private bool _isDetecting;

    public LocationSettingsViewModel(
        ILocationService locationService,
        ISettingsRepository settingsRepo)
    {
        _locationService = locationService;
        _settingsRepo = settingsRepo;
    }

    partial void OnSearchQueryChanged(string value)
    {
        var results = string.IsNullOrWhiteSpace(value)
            ? _locationService.GetPresetCities()
            : _locationService.SearchCities(value);

        SearchResults = new ObservableCollection<LocationInfo>(results);
    }

    [RelayCommand]
    private async Task DetectLocationAsync()
    {
        IsDetecting = true;
        try
        {
            var location = await _locationService.DetectCurrentLocationAsync();
            if (location is not null)
            {
                SelectedLocation = location;
            }
        }
        finally
        {
            IsDetecting = false;
        }
    }

    [RelayCommand]
    private void SelectCity(LocationInfo city)
    {
        SelectedLocation = city;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedLocation is null) return;

        var settings = await _settingsRepo.LoadAsync();
        settings.Location.SelectedLocation = SelectedLocation;
        await _settingsRepo.SaveAsync(settings);
    }
}
