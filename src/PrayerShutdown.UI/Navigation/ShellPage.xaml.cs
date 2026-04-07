using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.UI.Views;

namespace PrayerShutdown.UI.Navigation;

public sealed partial class ShellPage : Page
{
    public ShellPage()
    {
        InitializeComponent();

        var navService = App.Current.Services.GetRequiredService<NavigationService>();
        navService.Frame = ContentFrame;

        UpdateNavLabels();
        ContentFrame.Navigate(typeof(PrayerDashboardPage));
        NavView.SelectedItem = NavView.MenuItems[0];

        LocalizationService.Instance.LanguageChanged += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateNavLabels();
                ReloadCurrentPage();
            });
        };
    }

    private void UpdateNavLabels()
    {
        if (NavView.MenuItems[0] is NavigationViewItem d) d.Content = Loc.S("nav_dashboard");
        if (NavView.MenuItems[1] is NavigationViewItem s) s.Content = Loc.S("nav_settings");
        if (NavView.MenuItems[2] is NavigationViewItem l) l.Content = Loc.S("nav_log");
        if (NavView.FooterMenuItems[0] is NavigationViewItem a) a.Content = Loc.S("nav_about");
    }

    /// <summary>
    /// Force-reload current page so all L_ properties re-evaluate with new language.
    /// </summary>
    private void ReloadCurrentPage()
    {
        var currentType = ContentFrame.CurrentSourcePageType;
        if (currentType is null) return;

        // Navigate away then back to force page re-creation
        ContentFrame.Navigate(typeof(Page)); // blank
        ContentFrame.Navigate(currentType);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;

        var tag = item.Tag as string;
        var pageType = tag switch
        {
            "Dashboard" => typeof(PrayerDashboardPage),
            "Settings" => typeof(SettingsPage),
            "ActionLog" => typeof(ActionLogPage),
            "About" => typeof(AboutPage),
            _ => typeof(PrayerDashboardPage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType, null,
                new Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo
                {
                    Effect = Microsoft.UI.Xaml.Media.Animation.SlideNavigationTransitionEffect.FromRight
                });
        }
    }
}
