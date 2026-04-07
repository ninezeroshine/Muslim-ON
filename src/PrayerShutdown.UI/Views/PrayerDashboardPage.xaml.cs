using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Features.PrayerDashboard;

namespace PrayerShutdown.UI.Views;

public sealed partial class PrayerDashboardPage : Page
{
    public PrayerDashboardViewModel ViewModel { get; }

    // Localized strings
    public string L_Until => Loc.S("until");
    public string L_TodaysPrayers => Loc.S("todays_prayers");
    public string L_AllCompleted => Loc.S("all_prayers_completed");
    public string L_AllCompletedMsg => Loc.S("all_prayers_message");
    public string L_SetLocation => Loc.S("set_location_title");
    public string L_SetLocationDesc => Loc.S("set_location_desc");
    public string L_ShutdownHow => Loc.S("shutdown_how_title");
    public string L_GotIt => Loc.S("shutdown_got_it");
    public string L_Shutdown => Loc.S("shutdown_enabled");
    public string L_WisdomTitle => Loc.S("wisdom_title");
    public string L_UpdateAvailable => Loc.S("update_available");
    public string L_UpdateNow => Loc.S("update_now");

    private readonly DispatcherTimer _countdownTimer;
    private int _tickCount;

    public PrayerDashboardPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<PrayerDashboardViewModel>();
        InitializeComponent();

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += OnTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        TimelineCanvas.SizeChanged += (_, _) => RenderTimeline();
    }

    private void OnTick(object? s, object e)
    {
        ViewModel.UpdateCountdown();
        if (++_tickCount % 30 == 0)
            RenderTimeline();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        _countdownTimer.Start();
        RenderTimeline();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _countdownTimer.Stop();

    /// <summary>
    /// Renders prayer dots + current time marker on the timeline canvas.
    /// Code-behind rendering is pragmatic for positioned elements in WinUI 3.
    /// </summary>
    /// <summary>
    /// Renders the day timeline: progress track, prayer dots, time labels, current position marker.
    /// Layout: Grid Height=36, track center at Y=10, labels at Y=22.
    /// </summary>
    private void RenderTimeline()
    {
        TimelineCanvas.Children.Clear();
        double w = TimelineCanvas.ActualWidth;
        if (w <= 0 || ViewModel.TimelineDots.Count == 0) return;

        const double trackY = 10;   // center of the 3px track
        const double labelY = 20;   // top of time labels
        const int dotSize = 8;

        var bPassed = Res("GeistGray1000Brush") ?? new SolidColorBrush(Colors.Gray);
        var bFuture = Res("GeistGray400Brush") ?? new SolidColorBrush(Colors.LightGray);
        var bBlue = Res("GeistBlue700Brush") ?? new SolidColorBrush(Colors.Blue);
        var bLabel = Res("GeistGray600Brush") ?? new SolidColorBrush(Colors.DarkGray);
        var bBg = Res("GeistBackground200Brush") ?? new SolidColorBrush(Colors.White);

        // 1) Progress fill — colored track from 0 to current time position
        double progressX = Math.Clamp(ViewModel.CurrentTimePosition * w, 0, w);
        var progressTrack = new Border
        {
            Width = progressX,
            Height = 3,
            CornerRadius = new CornerRadius(1.5),
            Background = bBlue,
            Opacity = 0.35,
        };
        Canvas.SetLeft(progressTrack, 0);
        Canvas.SetTop(progressTrack, trackY - 1.5);
        TimelineCanvas.Children.Add(progressTrack);

        // 2) Prayer dots + labels
        foreach (var dot in ViewModel.TimelineDots)
        {
            double cx = Math.Clamp(dot.Position * w, dotSize / 2.0, w - dotSize / 2.0);

            var ellipse = new Ellipse
            {
                Width = dotSize,
                Height = dotSize,
                Fill = dot.IsPassed ? bPassed : bBg,
                Stroke = dot.IsPassed ? bPassed : bFuture,
                StrokeThickness = dot.IsPassed ? 0 : 1.5,
            };
            Canvas.SetLeft(ellipse, cx - dotSize / 2.0);
            Canvas.SetTop(ellipse, trackY - dotSize / 2.0);
            ToolTipService.SetToolTip(ellipse, $"{dot.Name}  {dot.TimeFormatted}");
            TimelineCanvas.Children.Add(ellipse);

            var label = new TextBlock
            {
                Text = dot.TimeFormatted,
                FontSize = 8.5,
                FontFamily = (FontFamily)Application.Current.Resources["GeistMonoFamily"],
                Foreground = bLabel,
            };
            Canvas.SetLeft(label, cx - 13);
            Canvas.SetTop(label, labelY);
            TimelineCanvas.Children.Add(label);
        }

        // 3) Current time marker — glow + dot
        double mx = Math.Clamp(ViewModel.CurrentTimePosition * w, 3, w - 3);
        TimelineCanvas.Children.Add(MakeCircle(14, bBlue, 0.2, mx, trackY));  // glow
        TimelineCanvas.Children.Add(MakeCircle(7, bBlue, 1.0, mx, trackY));   // marker
    }

    private static Ellipse MakeCircle(double size, Brush fill, double opacity, double cx, double cy)
    {
        var e = new Ellipse { Width = size, Height = size, Fill = fill, Opacity = opacity };
        Canvas.SetLeft(e, cx - size / 2.0);
        Canvas.SetTop(e, cy - size / 2.0);
        return e;
    }

    private static Brush? Res(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) ? v as Brush : null;
}
