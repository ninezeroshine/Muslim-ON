using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Features.ActionLog;

namespace PrayerShutdown.UI.Views;

public sealed partial class ActionLogPage : Page
{
    public ActionLogViewModel ViewModel { get; }

    public string ActivityLogTitle => Loc.S("activity_log");
    public string ClearLabel => Loc.S("clear");
    public string LogRefreshLabel => Loc.S("log_refresh");
    public string NoActivityTitle => Loc.S("log_empty_title");
    public string NoActivityDesc => Loc.S("log_empty_desc");

    public ActionLogPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ActionLogViewModel>();
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }

    private async void OnClearClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = Loc.S("clear"),
            Content = Loc.S("log_confirm_clear"),
            PrimaryButtonText = Loc.S("clear"),
            CloseButtonText = Loc.S("shutdown_got_it"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            await ViewModel.ClearCommand.ExecuteAsync(null);
    }
}
