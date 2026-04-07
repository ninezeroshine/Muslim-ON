namespace PrayerShutdown.Core.Domain.Settings;

public sealed class WorkDaySettings
{
    public bool Enabled { get; set; }
    public int StartHour { get; set; } = 9;
    public int StartMinute { get; set; } = 0;
    public int EndHour { get; set; } = 18;
    public int EndMinute { get; set; } = 0;

    public double StartPosition => (StartHour * 60 + StartMinute) / 1440.0;
    public double EndPosition => (EndHour * 60 + EndMinute) / 1440.0;
    public string StartFormatted => $"{StartHour:D2}:{StartMinute:D2}";
    public string EndFormatted => $"{EndHour:D2}:{EndMinute:D2}";
}
