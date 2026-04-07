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
    public string L_WorkDayEdit => Loc.S("work_day_edit");
    public string L_WorkDayStart => Loc.S("work_day_start");
    public string L_WorkDayEnd => Loc.S("work_day_end");
    public string L_WorkDaySave => Loc.S("work_day_save");
    public string L_WorkDayEnable => Loc.S("work_day_enable");

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
        ViewModel.TimelineChanged += () => DispatcherQueue.TryEnqueue(RenderTimeline);
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
        PopulateWorkDayCombos();
        RenderTimeline();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _countdownTimer.Stop();

    // ── Work day time combos (30-min intervals) ──
    private static readonly string[] HalfHourSlots = Enumerable.Range(0, 48)
        .Select(i => $"{i / 2:D2}:{(i % 2 == 0 ? "00" : "30")}").ToArray();

    private void PopulateWorkDayCombos()
    {
        WorkStartCombo.ItemsSource = HalfHourSlots;
        WorkEndCombo.ItemsSource = HalfHourSlots;
        SyncWorkDayCombos();
    }

    private void SyncWorkDayCombos()
    {
        var startIdx = ViewModel.EditWorkStartHour * 2 + (ViewModel.EditWorkStartMinute >= 30 ? 1 : 0);
        var endIdx = ViewModel.EditWorkEndHour * 2 + (ViewModel.EditWorkEndMinute >= 30 ? 1 : 0);
        WorkStartCombo.SelectedIndex = Math.Clamp(startIdx, 0, 47);
        WorkEndCombo.SelectedIndex = Math.Clamp(endIdx, 0, 47);
    }

    private void WorkStartCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (WorkStartCombo.SelectedIndex < 0) return;
        ViewModel.EditWorkStartHour = WorkStartCombo.SelectedIndex / 2;
        ViewModel.EditWorkStartMinute = WorkStartCombo.SelectedIndex % 2 == 0 ? 0 : 30;
    }

    private void WorkEndCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (WorkEndCombo.SelectedIndex < 0) return;
        ViewModel.EditWorkEndHour = WorkEndCombo.SelectedIndex / 2;
        ViewModel.EditWorkEndMinute = WorkEndCombo.SelectedIndex % 2 == 0 ? 0 : 30;
    }

    /// <summary>
    /// Renders prayer dots + current time marker on the timeline canvas.
    /// Code-behind rendering is pragmatic for positioned elements in WinUI 3.
    /// </summary>
    /// <summary>
    /// Renders the day timeline with track thickening for work day.
    /// The track widens from 3px to 8px in the work region — no new colors, no overlays.
    /// </summary>
    private void RenderTimeline()
    {
        TimelineCanvas.Children.Clear();
        double w = TimelineCanvas.ActualWidth;
        if (w <= 0 || ViewModel.TimelineDots.Count == 0) return;

        const double trackY = 20;
        const double labelY = 34;
        const int dotSize = 8;
        const double thinH = 3, thickH = 8;

        var bTrack = Res("GeistGray200Brush") ?? new SolidColorBrush(Colors.LightGray);
        var bPassed = Res("GeistGray1000Brush") ?? new SolidColorBrush(Colors.Gray);
        var bFuture = Res("GeistGray400Brush") ?? new SolidColorBrush(Colors.LightGray);
        var bBlue = Res("GeistBlue700Brush") ?? new SolidColorBrush(Colors.Blue);
        var bGreen = Res("GeistSuccessBrush") ?? new SolidColorBrush(Colors.Green);
        var bLabel = Res("GeistGray600Brush") ?? new SolidColorBrush(Colors.DarkGray);
        var bScaleLabel = Res("GeistGray400Brush") ?? bLabel;
        var bBg = Res("GeistBackground200Brush") ?? new SolidColorBrush(Colors.White);

        bool hasWork = ViewModel.WorkDayEnabled;
        double wdL = hasWork ? Math.Clamp(ViewModel.WorkDayStartPos * w, 0, w) : 0;
        double wdR = hasWork ? Math.Clamp(ViewModel.WorkDayEndPos * w, 0, w) : 0;
        double px = Math.Clamp(ViewModel.CurrentTimePosition * w, 0, w);

        // ── 1) Track segments (3px normal, 8px in work zone) ──
        if (hasWork && wdL < wdR)
        {
            if (wdL > 0) AddTrackSegment(0, wdL, thinH, bTrack, trackY);
            AddTrackSegment(wdL, wdR - wdL, thickH, bTrack, trackY);
            if (wdR < w) AddTrackSegment(wdR, w - wdR, thinH, bTrack, trackY);
        }
        else
        {
            AddTrackSegment(0, w, thinH, bTrack, trackY);
        }

        // ── 2) Progress fill — blue outside work, GREEN inside work ──
        if (hasWork && wdL < wdR)
        {
            // Pre-work: blue thin
            if (px > 0 && wdL > 0)
                AddTrackSegment(0, Math.Min(px, wdL), thinH, bBlue, trackY, 0.35);
            // During work: GREEN thick
            if (px > wdL)
                AddTrackSegment(wdL, Math.Min(px, wdR) - wdL, thickH, bGreen, trackY, 0.4);
            // Post-work: blue thin
            if (px > wdR)
                AddTrackSegment(wdR, px - wdR, thinH, bBlue, trackY, 0.35);
        }
        else
        {
            AddTrackSegment(0, px, thinH, bBlue, trackY, 0.35);
        }

        // ── 3) Work day labels — three-point anchored above thick segment ──
        if (hasWork && wdL < wdR)
        {
            double infoY = trackY - 20; // above track
            var bWorkGreen = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 163, 74)); // green-600
            var bWorkBold = Res("GeistGray1000Brush") ?? bLabel;

            // Left: elapsed (green)
            AddStyledLabel(ViewModel.WorkDayElapsed, wdL, infoY, bWorkGreen, 9.5, false);

            // Center: schedule · total (gray, smaller)
            var centerText = ViewModel.WorkDayCenter;
            var centerX = wdL + (wdR - wdL) / 2 - (centerText.Length * 3.2); // approximate centering
            AddStyledLabel(centerText, centerX, infoY, bLabel, 8.5, false);

            // Right: remaining (bold, dark)
            var remText = ViewModel.WorkDayRemaining;
            AddStyledLabel(remText, wdR - (remText.Length * 6.5), infoY, bWorkBold, 9.5, true);
        }

        // ── 4) Scale edge labels (just 00:00 and 24:00) ──
        AddLabel("00", 0, labelY, bScaleLabel);
        AddLabel("24", w - 12, labelY, bScaleLabel);

        // ── 5) Prayer dots + time labels ──
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

        // ── 6) Current time marker ──
        double mx = Math.Clamp(ViewModel.CurrentTimePosition * w, 3, w - 3);
        TimelineCanvas.Children.Add(MakeCircle(14, bBlue, 0.2, mx, trackY));
        TimelineCanvas.Children.Add(MakeCircle(7, bBlue, 1.0, mx, trackY));
    }

    private void AddTrackSegment(double x, double width, double height, Brush bg, double centerY, double opacity = 1.0)
    {
        if (width <= 0) return;
        var b = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(height / 2),
            Background = bg,
            Opacity = opacity,
        };
        Canvas.SetLeft(b, x);
        Canvas.SetTop(b, centerY - height / 2);
        TimelineCanvas.Children.Add(b);
    }

    private static Ellipse MakeCircle(double size, Brush fill, double opacity, double cx, double cy)
    {
        var e = new Ellipse { Width = size, Height = size, Fill = fill, Opacity = opacity };
        Canvas.SetLeft(e, cx - size / 2.0);
        Canvas.SetTop(e, cy - size / 2.0);
        return e;
    }

    private void AddLabel(string text, double x, double y, Brush foreground) =>
        AddStyledLabel(text, x, y, foreground, 8.5, false);

    private void AddStyledLabel(string text, double x, double y, Brush foreground, double fontSize, bool bold)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            FontFamily = (FontFamily)Application.Current.Resources["GeistMonoFamily"],
            Foreground = foreground,
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        TimelineCanvas.Children.Add(tb);
    }

    private static Brush? Res(string key) =>
        Application.Current.Resources.TryGetValue(key, out var v) ? v as Brush : null;
}
