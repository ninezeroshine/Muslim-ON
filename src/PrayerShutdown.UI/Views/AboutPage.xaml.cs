using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PrayerShutdown.Common.Localization;

namespace PrayerShutdown.UI.Views;

public sealed partial class AboutPage : Page
{
    public string Title => Loc.S("about_title");
    public string Version => Loc.S("about_version");
    public string Description => Loc.S("about_desc");
    public string Tech => Loc.S("about_tech");
    public string Algorithm => Loc.S("about_algorithm");

    public AboutPage()
    {
        InitializeComponent();
    }
}
