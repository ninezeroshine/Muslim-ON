namespace PrayerShutdown.Features.PrayerDashboard;

/// <summary>Row in the "Next Steps" block on the dashboard.</summary>
public sealed class PhaseStepModel
{
    public required string Label { get; init; }
    public required string TimeFormatted { get; init; }

    /// <summary>Hex color for the row dot (re-uses ColorTokens).</summary>
    public required string Dot { get; init; }

    public required bool IsDone { get; init; }
    public string? Detail { get; init; }

    public double DoneOpacity => IsDone ? 0.45 : 1.0;
    public bool HasDetail => !string.IsNullOrEmpty(Detail);
}
