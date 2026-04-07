using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Features.Settings;

namespace PrayerShutdown.UI.Views;

public sealed partial class SettingsPage : Page
{
    public GeneralSettingsViewModel ViewModel { get; }

    // All localized strings for XAML x:Bind
    public string L_Settings => Loc.S("settings");
    public string L_Location => Loc.S("location");
    public string L_LocationDesc => Loc.S("location_desc");
    public string L_CurrentCity => Loc.S("current_city");
    public string L_SearchCity => Loc.S("search_city");
    public string L_CalcMethod => Loc.S("calculation_method");
    public string L_CalcMethodDesc => Loc.S("calculation_method_desc");
    public string L_Method => Loc.S("method");
    public string L_AsrCalc => Loc.S("asr_calculation");
    public string L_HighLatRule => Loc.S("high_latitude_rule");
    public string L_HighLatDesc => Loc.S("high_lat_desc");
    public string L_ShutdownRules => Loc.S("shutdown_rules");
    public string L_ShutdownRulesDesc => Loc.S("shutdown_rules_desc");
    public string L_Notifications => Loc.S("notifications");
    public string L_NotificationsDesc => Loc.S("notifications_desc");
    public string L_ToastNotif => Loc.S("toast_notifications");
    public string L_AdhanSound => Loc.S("adhan_sound");
    public string L_Appearance => Loc.S("appearance");
    public string L_Language => Loc.S("language");
    public string L_LanguageHint => Loc.S("language_hint");
    public string L_StartWindows => Loc.S("start_with_windows");
    public string L_StartMinimized => Loc.S("start_minimized");
    public string L_SaveSettings => Loc.S("save_settings");
    public string L_Saved => Loc.S("saved");
    // ComboBox items
    public string L_MethodMwl => Loc.S("method_mwl");
    public string L_MethodEgyptian => Loc.S("method_egyptian");
    public string L_MethodKarachi => Loc.S("method_karachi");
    public string L_MethodUmmAlQura => Loc.S("method_ummAlQura");
    public string L_MethodIsna => Loc.S("method_isna");
    public string L_MethodTurkey => Loc.S("method_turkey");
    public string L_MethodTehran => Loc.S("method_tehran");
    public string L_AsrShafi => Loc.S("asr_shafi");
    public string L_AsrHanafi => Loc.S("asr_hanafi");
    public string L_HighLatAngle => Loc.S("highlat_angle");
    public string L_HighLatMiddle => Loc.S("highlat_middle");
    public string L_HighLatSeventh => Loc.S("highlat_seventh");
    // Prayer names for toggles
    public string L_Fajr => Loc.S("prayer_fajr");
    public string L_Dhuhr => Loc.S("prayer_dhuhr");
    public string L_Asr => Loc.S("prayer_asr");
    public string L_Maghrib => Loc.S("prayer_maghrib");
    public string L_Isha => Loc.S("prayer_isha");

    public SettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<GeneralSettingsViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        SyncComboBoxes();
    }

    private void SyncComboBoxes()
    {
        var methodTag = ViewModel.SelectedMethod.ToString();
        for (int i = 0; i < MethodCombo.Items.Count; i++)
            if (MethodCombo.Items[i] is ComboBoxItem item && item.Tag as string == methodTag)
            { MethodCombo.SelectedIndex = i; break; }

        AsrCombo.SelectedIndex = ViewModel.SelectedAsrMethod == AsrJuristic.Hanafi ? 1 : 0;

        var highLatTag = ViewModel.SelectedHighLatRule.ToString();
        for (int i = 0; i < HighLatCombo.Items.Count; i++)
            if (HighLatCombo.Items[i] is ComboBoxItem item && item.Tag as string == highLatTag)
            { HighLatCombo.SelectedIndex = i; break; }
    }

    private void MethodCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (MethodCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            if (Enum.TryParse<CalculationMethod>(tag, out var method))
                ViewModel.SelectedMethod = method;
    }

    private void AsrCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedAsrMethod = AsrCombo.SelectedIndex == 1 ? AsrJuristic.Hanafi : AsrJuristic.Shafi;
    }

    private void HighLatCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (HighLatCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            if (Enum.TryParse<HighLatitudeRule>(tag, out var rule))
                ViewModel.SelectedHighLatRule = rule;
    }

    private void CitySearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var svc = App.Current.Services.GetRequiredService<PrayerShutdown.Core.Interfaces.ILocationService>();
            sender.ItemsSource = svc.SearchCities(sender.Text).Select(c => $"{c.CityName}, {c.Country}").ToList();
        }
    }

    private async void CitySearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        var cityName = (args.SelectedItem?.ToString() ?? "").Split(',')[0].Trim();
        var svc = App.Current.Services.GetRequiredService<PrayerShutdown.Core.Interfaces.ILocationService>();
        var city = svc.SearchCities(cityName).FirstOrDefault();
        if (city is not null) await ViewModel.SelectCityCommand.ExecuteAsync(city);
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveCommand.ExecuteAsync(null);
        var dashboard = App.Current.Services.GetRequiredService<PrayerShutdown.Features.PrayerDashboard.PrayerDashboardViewModel>();
        await dashboard.ForceRefreshAsync();
    }
}
