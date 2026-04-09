using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrayerShutdown.Common.Localization;
using PrayerShutdown.Core.Domain.Enums;
using PrayerShutdown.Core.Domain.Models;
using PrayerShutdown.Core.Extensions;
using PrayerShutdown.Core.Interfaces;
using PrayerShutdown.Services.Update;

namespace PrayerShutdown.Features.PrayerDashboard;

public partial class PrayerDashboardViewModel : ObservableObject
{
    private readonly IPrayerTimeCalculator _calculator;
    private readonly ISettingsRepository _settingsRepo;
    private readonly ISchedulerService _scheduler;
    private readonly UpdateService _updateService;
    private bool _initialized;
    // Re-entrance guard for Dashboard ↔ Scheduler "prayed" sync.
    // Dashboard click → MarkAsPrayed → event → our handler → Card.IsPrayed = true → PropertyChanged → HandlePrayerMarked.
    // Without this flag, the loop would call MarkAsPrayed twice.
    private bool _suppressPrayedSync;

    /// <summary>Code-behind hooks this to redraw Canvas after data changes.</summary>
    public event Action? TimelineChanged;

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

    // ── Work day overlay ──
    [ObservableProperty] private bool _workDayEnabled;
    [ObservableProperty] private double _workDayStartPos;
    [ObservableProperty] private double _workDayEndPos;
    [ObservableProperty] private string _workDayStartText = "09:00";
    [ObservableProperty] private string _workDayEndText = "18:00";
    [ObservableProperty] private string _workDayElapsed = "";
    [ObservableProperty] private string _workDayRemaining = "";
    [ObservableProperty] private string _workDayCenter = "";
    [ObservableProperty] private bool _showWorkDayEditor;
    [ObservableProperty] private int _editWorkStartHour = 9;
    [ObservableProperty] private int _editWorkStartMinute = 0;
    [ObservableProperty] private int _editWorkEndHour = 18;
    [ObservableProperty] private int _editWorkEndMinute = 0;
    [ObservableProperty] private bool _editWorkDayEnabled;

    // ── Feature 5: Wisdom ──
    [ObservableProperty] private string _wisdomText = "";
    [ObservableProperty] private string _wisdomSource = "";
    [ObservableProperty] private bool _showWisdom = true;
    [ObservableProperty] private bool _showWisdomHint;

    partial void OnShowWisdomChanged(bool value) => ShowWisdomHint = !value;

    // ── Auto-update ──
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string _updateVersion = "";
    [ObservableProperty] private bool _isUpdating;
    [ObservableProperty] private int _updatePercent;
    private string? _updateDownloadUrl;

    public PrayerDashboardViewModel(
        IPrayerTimeCalculator calculator,
        ISettingsRepository settingsRepo,
        ISchedulerService scheduler,
        UpdateService updateService)
    {
        _calculator = calculator;
        _settingsRepo = settingsRepo;
        _scheduler = scheduler;
        _updateService = updateService;

        // Sync dashboard cards when prayer is marked from overlay (or anywhere else).
        _scheduler.PrayerMarkedAsPrayed += OnSchedulerPrayerMarked;
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

            // Load work day
            var wd = settings.WorkDay;
            WorkDayEnabled = wd.Enabled;
            WorkDayStartPos = wd.StartPosition;
            WorkDayEndPos = wd.EndPosition;
            WorkDayStartText = wd.StartFormatted;
            WorkDayEndText = wd.EndFormatted;
            EditWorkStartHour = wd.StartHour;
            EditWorkStartMinute = wd.StartMinute;
            EditWorkEndHour = wd.EndHour;
            EditWorkEndMinute = wd.EndMinute;
            EditWorkDayEnabled = wd.Enabled;

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
                _ = CheckForUpdateAsync(); // fire-and-forget, non-blocking
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
            CountdownColor = ColorTokens.Gray;
            ShowPrayerProgress = false;
            return;
        }

        AllPrayersPassed = false;
        var remaining = nextCard.Time.TimeUntil();
        CountdownText = FormatCountdown(remaining);
        NextPrayerName = nextCard.LocalizedName;
        CountdownColor = remaining.TotalMinutes switch
        {
            < 10 => ColorTokens.Error,
            < 30 => ColorTokens.Warning,
            _ => ColorTokens.Text
        };

        UpdateProgress(now, nextCard);
        UpdateWorkDayInfo(now);
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
        s.TotalHours >= 1
            ? s.Minutes > 0 ? $"{(int)s.TotalHours}{Loc.S("h")} {s.Minutes}{Loc.S("m")}"
                            : $"{(int)s.TotalHours}{Loc.S("h")}"
            : $"{Math.Max(1, (int)s.TotalMinutes)}{Loc.S("m")}";

    // ═══════════════════════════════════════════
    //  Work day info
    // ═══════════════════════════════════════════

    private void UpdateWorkDayInfo(DateTime now)
    {
        if (!WorkDayEnabled) return;

        var startMin = EditWorkStartHour * 60 + EditWorkStartMinute;
        var endMin = EditWorkEndHour * 60 + EditWorkEndMinute;
        var nowMin = now.Hour * 60 + now.Minute;
        var totalMin = endMin - startMin;
        if (totalMin <= 0) return;

        var totalSpan = TimeSpan.FromMinutes(totalMin);
        WorkDayCenter = $"{WorkDayStartText} \u2013 {WorkDayEndText} \u00b7 {FormatShortSpan(totalSpan)}";

        if (nowMin < startMin)
        {
            // Before work
            var until = TimeSpan.FromMinutes(startMin - nowMin);
            WorkDayElapsed = $"{Loc.S("work_starts_in")} {FormatShortSpan(until)}";
            WorkDayRemaining = FormatShortSpan(totalSpan);
        }
        else if (nowMin >= endMin)
        {
            // After work
            var overtime = TimeSpan.FromMinutes(nowMin - endMin);
            WorkDayElapsed = FormatShortSpan(totalSpan);
            WorkDayRemaining = overtime.TotalMinutes > 0
                ? $"+{FormatShortSpan(overtime)}"
                : Loc.S("work_done");
        }
        else
        {
            // During work
            var elapsed = TimeSpan.FromMinutes(nowMin - startMin);
            var remaining = TimeSpan.FromMinutes(endMin - nowMin);
            WorkDayElapsed = FormatShortSpan(elapsed);
            WorkDayRemaining = FormatShortSpan(remaining);
        }
    }

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
        if (_suppressPrayedSync) return;

        var prayer = _scheduler.TodaysPrayers?.GetPrayer(card.Name);
        if (prayer is null) return;

        _suppressPrayedSync = true;
        try { _scheduler.MarkAsPrayed(prayer); }
        finally { _suppressPrayedSync = false; }
    }

    private void OnSchedulerPrayerMarked(object? sender, PrayerTime prayer)
    {
        if (_suppressPrayedSync) return;

        var card = Prayers.FirstOrDefault(c => c.Name == prayer.Name);
        if (card is null || card.IsPrayed) return;

        _suppressPrayedSync = true;
        try
        {
            card.IsPrayed = true;
            card.RefreshState();
        }
        finally { _suppressPrayedSync = false; }
    }

    // ═══════════════════════════════════════════
    //  Shutdown info commands
    // ═══════════════════════════════════════════

    [RelayCommand] private void DismissShutdownInfo()  { ShowShutdownInfo = false; OnPropertyChanged(nameof(ShowShutdownHint)); }
    [RelayCommand] private void ExpandShutdownInfo()   { ShowShutdownInfo = true;  OnPropertyChanged(nameof(ShowShutdownHint)); }
    [RelayCommand] private void DismissWisdom()        { ShowWisdom = false; }
    [RelayCommand] private void ExpandWisdom()         { ShowWisdom = true; }
    [RelayCommand] private void ToggleWorkDayEditor()  { ShowWorkDayEditor = !ShowWorkDayEditor; }

    [RelayCommand]
    private async Task SaveWorkDayAsync()
    {
        var settings = await _settingsRepo.LoadAsync();
        settings.WorkDay.Enabled = EditWorkDayEnabled;
        settings.WorkDay.StartHour = EditWorkStartHour;
        settings.WorkDay.StartMinute = EditWorkStartMinute;
        settings.WorkDay.EndHour = EditWorkEndHour;
        settings.WorkDay.EndMinute = EditWorkEndMinute;
        await _settingsRepo.SaveAsync(settings);

        WorkDayEnabled = EditWorkDayEnabled;
        WorkDayStartPos = settings.WorkDay.StartPosition;
        WorkDayEndPos = settings.WorkDay.EndPosition;
        WorkDayStartText = settings.WorkDay.StartFormatted;
        WorkDayEndText = settings.WorkDay.EndFormatted;
        ShowWorkDayEditor = false;
        TimelineChanged?.Invoke();
    }

    // ═══════════════════════════════════════════
    //  Auto-update
    // ═══════════════════════════════════════════

    private async Task CheckForUpdateAsync()
    {
        var (version, url) = await _updateService.CheckForUpdateAsync();
        if (version is not null && url is not null)
        {
            UpdateVersion = version;
            _updateDownloadUrl = url;
            UpdateAvailable = true;
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (_updateDownloadUrl is null) return;
        IsUpdating = true;
        var progress = new Progress<int>(p => UpdatePercent = p);
        var ok = await _updateService.DownloadAndApplyAsync(_updateDownloadUrl, progress);
        if (ok)
        {
            // Script will restart the app — exit current instance
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
        else
        {
            IsUpdating = false;
        }
    }

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
