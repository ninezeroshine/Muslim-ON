using Microsoft.UI.Xaml.Controls;
using PrayerShutdown.Common.Localization;

namespace PrayerShutdown.UI.Views;

public sealed partial class ActionLogPage : Page
{
    public string ActivityLogTitle => Loc.S("activity_log");
    public string ClearLabel => Loc.S("clear");
    public string NoActivityTitle => Loc.S("no_activity");
    public string NoActivityDesc => Loc.S("no_activity_desc");

    public ActionLogPage()
    {
        InitializeComponent();
    }
}
