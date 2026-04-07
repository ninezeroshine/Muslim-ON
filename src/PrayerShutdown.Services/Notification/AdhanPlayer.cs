using Microsoft.Extensions.Logging;
using PrayerShutdown.Core.Interfaces;

namespace PrayerShutdown.Services.Notification;

public sealed class AdhanPlayer : IAdhanPlayer
{
    private readonly ILogger<AdhanPlayer> _logger;

    public bool IsPlaying { get; private set; }

    public AdhanPlayer(ILogger<AdhanPlayer> logger)
    {
        _logger = logger;
    }

    public Task PlayAsync(string? customPath = null)
    {
        _logger.LogInformation("Playing adhan (path: {Path})", customPath ?? "default");
        IsPlaying = true;

        // TODO: Implement MediaPlayer for adhan playback
        // Auto-stop after 60 seconds
        _ = Task.Delay(TimeSpan.FromSeconds(60)).ContinueWith(_ => Stop());

        return Task.CompletedTask;
    }

    public void Stop()
    {
        IsPlaying = false;
        _logger.LogInformation("Adhan stopped");
    }
}
