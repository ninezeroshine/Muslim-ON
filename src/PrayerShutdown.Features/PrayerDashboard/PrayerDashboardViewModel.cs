using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Extensions;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Features.PrayerDashboard;

public partial class PrayerDashboardViewModel : ObservableObject
{
    private readonly IPrayerTimeCalculator _calculator;
    private readonly ISettingsRepository _settingsRepo;
    private readonly ISchedulerService _scheduler;
    private bool _initialized;

    // ── Core ──
    [ObservableProperty] private ObservableCollection<PrayerCardModel> _prayers = new();
    [ObservableProperty] private string _countdownText = "--:--";
    [ObservableProperty] private string _nextPrayerName = "";
    [ObservableProperty] private string _currentDate = "";
    [ObservableProperty] private string _hijriDate = "";
    [ObservableProperty] private string _greeting = "";
    [ObservableProperty] private string _locationName = "";
    [ObservableProperty] private string _methodName = "";
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _hasLocation;
    [ObservableProperty] private bool _allPrayersPassed;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private string _countdownColor = "#171717";

    // ── Shutdown info ──
    [ObservableProperty] private bool _showShutdownInfo;
    [ObservableProperty] private bool _anyShutdownEnabled;
    public bool ShowShutdownHint => !ShowShutdownInfo && HasLocation;
    public string ShutdownStep1 => string.Format(Loc.S("shutdown_step1"), "15");
    public string ShutdownStep2 => Loc.S("shutdown_step2");
    public string ShutdownStep3 => string.Format(Loc.S("shutdown_step3"), "15");
    public string ShutdownStep4 => string.Format(Loc.S("shutdown_step4"), "3");

    // ── Feature 6: Jumu'ah ──
    [ObservableProperty] private bool _isFriday;
    [ObservableProperty] private string _fridayMessage = "";

    // ── Feature 2: Progress bar ──
    [ObservableProperty] private double _prayerProgressPercent;
    [ObservableProperty] private string _progressFromName = "";
    [ObservableProperty] private string _progressToName = "";
    [ObservableProperty] private bool _showPrayerProgress;

    [ObservableProperty] private string _progressElapsed = "";
    [ObservableProperty] private string _progressRemaining = "";
    [ObservableProperty] private string _progressPercentText = "";

    // ── Feature 4: Day timeline ──
    [ObservableProperty] private double _currentTimePosition;
    [ObservableProperty] private ObservableCollection<TimelineDotModel> _timelineDots = new();

    // ── Feature 5: Wisdom ──
    [ObservableProperty] private string _wisdomText = "";
    [ObservableProperty] private string _wisdomSource = "";
    [ObservableProperty] private bool _showWisdom = true;
    [ObservableProperty] private bool _showWisdomHint;

    partial void OnShowWisdomChanged(bool value) => ShowWisdomHint = !value;

    public PrayerDashboardViewModel(
        IPrayerTimeCalculator calculator,
        ISettingsRepository settingsRepo,
        ISchedulerService scheduler)
    {
        _calculator = calculator;
        _settingsRepo = settingsRepo;
        _scheduler = scheduler;
    }

    // ═══════════════════════════════════════════
    //  Initialization
    // ═══════════════════════════════════════════

    public async Task InitializeAsync()
    {
        if (_initialized) { UpdateCountdown(); return; }
        await ForceRefreshAsync();
    }

    [RelayCommand]
    public async Task ForceRefreshAsync()
    {
        IsLoading = true;
        HasError = false;

        try
        {
            var now = DateTime.Now;

            // Header
            Greeting = GetGreeting(now.Hour);
            CurrentDate = now.ToString("dddd, d MMMM yyyy");
            HijriDate = GetHijriDate(now);
            IsFriday = now.DayOfWeek == DayOfWeek.Friday;
            FridayMessage = IsFriday ? Loc.S("jumuah_notice") : "";

            // Wisdom
            var (wText, wSrc) = DailyWisdomProvider.GetForToday();
            WisdomText = wText;
            WisdomSource = wSrc;

            var settings = await _settingsRepo.LoadAsync();
            var location = settings.Location.SelectedLocation;

            if (location is null)
            {
                HasLocation = false;
                IsLoading = false;
                return;
            }

            HasLocation = true;
            LocationName = location.CityNameRu ?? location.CityName;
            MethodName = settings.Calculation.Method.ToString();

            // Calculate prayer times
            var today = DateOnly.FromDateTime(DateTime.Today);
            var dailyPrayers = _calculator.Calculate(today, location, settings.Calculation);
            var nextPrayer = dailyPrayers.GetNextPrayer(now);

            // Build cards
            var cards = dailyPrayers.Prayers
                .Select(p =>
                {
                    var rule = settings.Shutdown.Rules.FirstOrDefault(r => r.Prayer == p.Name);
                    return new PrayerCardModel
                    {
                        Name = p.Name,
                        Time = p.Time,
                        IsNext = nextPrayer?.Name == p.Name,
                        ShutdownEnabled = rule?.IsEnabled ?? false,
                        ShutdownDelayMinutes = rule?.ShutdownMinutesAfter ?? 15,
                    };
                })
                .ToList();

            // Subscribe to property changes for persistence
            foreach (var card in cards)
                card.PropertyChanged += OnCardPropertyChanged;

            Prayers = new ObservableCollection<PrayerCardModel>(cards);
            AnyShutdownEnabled = cards.Any(c => c.ShutdownEnabled);

            // Build timeline dots
            TimelineDots = new ObservableCollection<TimelineDotModel>(
                cards.Select(c => new TimelineDotModel
                {
                    Name = c.LocalizedName,
                    Position = c.Time.TimeOfDay.TotalMinutes / 1440.0,
                    IsPassed = c.IsPassed,
                    TimeFormatted = c.TimeFormatted,
                }));

            if (!_initialized)
            {
                await _scheduler.InitializeAsync();
                _initialized = true;
            }

            UpdateCountdown();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"{Loc.S("error_load_failed")}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ═══════════════════════════════════════════
    //  Per-second tick
    // ═══════════════════════════════════════════

    public void UpdateCountdown()
    {
        var now = DateTime.Now;
        PrayerCardModel? nextCard = null;

        foreach (var p in Prayers)
        {
            p.RefreshState();
            if (!p.IsPassed && !p.IsInformational && !p.IsPrayed && nextCard is null)
                nextCard = p;
        }

        foreach (var p in Prayers)
            if (!p.IsInformational) p.IsNext = p == nextCard;

        if (nextCard is null)
        {
            CountdownText = "00:00";
            NextPrayerName = "";
            AllPrayersPassed = true;
            CountdownColor = "#8F8F8F";
            ShowPrayerProgress = false;
            return;
        }

        AllPrayersPassed = false;
        var remaining = nextCard.Time.TimeUntil();
        CountdownText = FormatCountdown(remaining);
        NextPrayerName = nextCard.LocalizedName;
        CountdownColor = remaining.TotalMinutes switch
        {
            < 10 => "#E5484D",
            < 30 => "#F5A623",
            _ => "#171717"
        };

        UpdateProgress(now, nextCard);
        CurrentTimePosition = now.TimeOfDay.TotalMinutes / 1440.0;
    }

    // ═══════════════════════════════════════════
    //  Progress bar (Feature 2)
    // ═══════════════════════════════════════════

    private void UpdateProgress(DateTime now, PrayerCardModel next)
    {
        var actionable = Prayers.Where(p => !p.IsInformational).ToList();
        var idx = actionable.IndexOf(next);
        var prev = idx > 0 ? actionable[idx - 1] : null;

        if (prev is null) { ShowPrayerProgress = false; return; }

        ShowPrayerProgress = true;
        ProgressFromName = prev.LocalizedName;
        ProgressToName = next.LocalizedName;

        var totalSpan = next.Time - prev.Time;
        var elapsedSpan = now - prev.Time;
        var remainSpan = next.Time - now;
        var total = totalSpan.TotalSeconds;
        var elapsed = elapsedSpan.TotalSeconds;

        PrayerProgressPercent = total > 0 ? Math.Clamp(elapsed / total, 0, 1) : 0;
        ProgressPercentText = $"{(int)(PrayerProgressPercent * 100)}%";
        ProgressElapsed = FormatShortSpan(elapsedSpan);
        ProgressRemaining = FormatShortSpan(remainSpan);
    }

    private static string FormatShortSpan(TimeSpan s) =>
        s.TotalHours >= 1 ? $"{(int)s.TotalHours}h {s.Minutes}m"
                          : $"{(int)s.TotalMinutes}m";

    // ═══════════════════════════════════════════
    //  Card property change handlers
    // ═══════════════════════════════════════════

    private async void OnCardPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (s is not PrayerCardModel c) return;

        if (e.PropertyName == nameof(PrayerCardModel.ShutdownEnabled))
            await PersistShutdownToggleAsync(c);

        if (e.PropertyName == nameof(PrayerCardModel.IsPrayed) && c.IsPrayed)
            HandlePrayerMarked(c);
    }

    private async Task PersistShutdownToggleAsync(PrayerCardModel card)
    {
        card.RefreshState();
        var settings = await _settingsRepo.LoadAsync();
        var rule = settings.Shutdown.Rules.FirstOrDefault(r => r.Prayer == card.Name);
        if (rule is not null)
        {
            var idx = settings.Shutdown.Rules.IndexOf(rule);
            settings.Shutdown.Rules[idx] = rule with { IsEnabled = card.ShutdownEnabled };
        }
        await _settingsRepo.SaveAsync(settings);
        AnyShutdownEnabled = Prayers.Any(c => c.ShutdownEnabled);
        _scheduler.RecalculateSchedule();
    }

    private void HandlePrayerMarked(PrayerCardModel card)
    {
        var prayer = _scheduler.TodaysPrayers?.GetPrayer(card.Name);
        if (prayer is not null)
            _scheduler.MarkAsPrayed(prayer);
    }

    // ═══════════════════════════════════════════
    //  Shutdown info commands
    // ═══════════════════════════════════════════

    [RelayCommand] private void DismissShutdownInfo()  { ShowShutdownInfo = false; OnPropertyChanged(nameof(ShowShutdownHint)); }
    [RelayCommand] private void ExpandShutdownInfo()   { ShowShutdownInfo = true;  OnPropertyChanged(nameof(ShowShutdownHint)); }
    [RelayCommand] private void DismissWisdom()        { ShowWisdom = false; }
    [RelayCommand] private void ExpandWisdom()         { ShowWisdom = true; }

    // ═══════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════

    private static string FormatCountdown(TimeSpan span) =>
        span.TotalHours >= 1
            ? $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{(int)span.TotalMinutes:D2}:{span.Seconds:D2}";

    private static string GetGreeting(int hour) => hour switch
    {
        < 5 => Loc.S("greeting_default"),
        < 12 => Loc.S("greeting_morning"),
        < 17 => Loc.S("greeting_afternoon"),
        < 21 => Loc.S("greeting_evening"),
        _ => Loc.S("greeting_default")
    };

    private static string GetHijriDate(DateTime dt)
    {
        try
        {
            var hijri = new UmAlQuraCalendar();
            int y = hijri.GetYear(dt), m = hijri.GetMonth(dt), d = hijri.GetDayOfMonth(dt);
            string[] months = ["Muharram","Safar","Rabi al-Awwal","Rabi al-Thani",
                "Jumada al-Ula","Jumada al-Thani","Rajab","Sha'ban",
                "Ramadan","Shawwal","Dhu al-Qi'dah","Dhu al-Hijjah"];
            return $"{d} {months[m - 1]} {y} AH";
        }
        catch { return ""; }
    }
}
