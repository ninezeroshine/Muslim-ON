namespace PrayerShutdown.Core.Interfaces;

public interface IAdhanPlayer
{
    Task PlayAsync(string? customPath = null);
    void Stop();
    bool IsPlaying { get; }
}
