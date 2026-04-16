using Microsoft.Extensions.Logging;
using PrayerShutdown.Core.Interfaces;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace PrayerShutdown.Services.Notification;

public sealed class AdhanPlayer : IAdhanPlayer, IDisposable
{
    private readonly ILogger<AdhanPlayer> _logger;
    private MediaPlayer? _player;

    public bool IsPlaying { get; private set; }

    public AdhanPlayer(ILogger<AdhanPlayer> logger)
    {
        _logger = logger;
    }

    public Task PlayAsync(string? customPath = null)
    {
        try
        {
            var path = ResolveSource(customPath);
            if (path is null)
            {
                _logger.LogInformation("Adhan source not available, skipping playback");
                return Task.CompletedTask;
            }

            Stop();

            _player = new MediaPlayer
            {
                Source = MediaSource.CreateFromUri(new Uri(path)),
                Volume = 1.0,
                AutoPlay = false,
            };
            _player.MediaEnded += OnEnded;
            _player.MediaFailed += OnFailed;
            _player.Play();

            IsPlaying = true;
            _logger.LogInformation("Adhan started ({Path})", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play adhan (path: {Path})", customPath ?? "default");
            IsPlaying = false;
        }
        return Task.CompletedTask;
    }

    public void Stop()
    {
        if (_player is null) return;
        try
        {
            _player.MediaEnded -= OnEnded;
            _player.MediaFailed -= OnFailed;
            _player.Pause();
            _player.Dispose();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Adhan stop threw"); }
        finally
        {
            _player = null;
            IsPlaying = false;
        }
    }

    private void OnEnded(MediaPlayer sender, object args) => Stop();
    private void OnFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        _logger.LogWarning("Adhan playback failed: {Error}", args.ErrorMessage);
        Stop();
    }

    /// <summary>
    /// Resolve a playable file URI in this order:
    /// 1. custom path if supplied and existing
    /// 2. <c>Assets/adhan.mp3</c> shipped with the app
    /// 3. null — silent (no sound plays; toast/overlay still appear)
    /// </summary>
    private static string? ResolveSource(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            return new Uri(customPath).AbsoluteUri;

        var bundled = Path.Combine(AppContext.BaseDirectory, "Assets", "adhan.mp3");
        if (File.Exists(bundled)) return new Uri(bundled).AbsoluteUri;

        return null;
    }

    public void Dispose() => Stop();
}
