using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PrayerShutdown.Services.Update;

/// <summary>
/// Auto-updater that checks GitHub Releases for new versions.
/// Flow: Check → Download ZIP → Extract to temp → Replace files → Restart.
/// </summary>
public sealed class UpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/ninezeroshine/Muslim-ON/releases/latest";
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "MuslimON-Updater" },
            { "Accept", "application/vnd.github.v3+json" }
        }
    };

    private readonly ILogger<UpdateService> _logger;

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
    }

    public static string CurrentVersion => "1.0.1";

    /// <summary>
    /// Check GitHub Releases for a newer version.
    /// Returns (newVersion, downloadUrl) or (null, null) if up to date.
    /// </summary>
    public async Task<(string? Version, string? DownloadUrl)> CheckForUpdateAsync()
    {
        try
        {
            var release = await Http.GetFromJsonAsync<GitHubRelease>(GitHubApiUrl);
            if (release is null) return (null, null);

            var latest = release.TagName?.TrimStart('v') ?? "";
            if (string.IsNullOrEmpty(latest)) return (null, null);

            if (IsNewer(latest, CurrentVersion))
            {
                // Find the ZIP asset
                var asset = release.Assets?.FirstOrDefault(a =>
                    a.Name?.Contains("x64", StringComparison.OrdinalIgnoreCase) == true &&
                    a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                var url = asset?.BrowserDownloadUrl ?? release.Assets?.FirstOrDefault()?.BrowserDownloadUrl;
                _logger.LogInformation("Update available: {Current} → {Latest}", CurrentVersion, latest);
                return (latest, url);
            }

            _logger.LogInformation("Up to date: {Version}", CurrentVersion);
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed");
            return (null, null);
        }
    }

    /// <summary>
    /// Download ZIP, extract, replace app files, launch new version, exit current.
    /// </summary>
    public async Task<bool> DownloadAndApplyAsync(string downloadUrl, IProgress<int>? progress = null)
    {
        try
        {
            var appDir = AppContext.BaseDirectory;
            var tempDir = Path.Combine(Path.GetTempPath(), "MuslimON_Update");
            var zipPath = Path.Combine(Path.GetTempPath(), "MuslimON_Update.zip");

            // Clean previous update attempt
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (File.Exists(zipPath)) File.Delete(zipPath);

            // Download
            _logger.LogInformation("Downloading update from {Url}", downloadUrl);
            progress?.Report(10);

            using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(zipPath);

            var buffer = new byte[81920];
            long downloaded = 0;
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                downloaded += bytesRead;
                if (totalBytes > 0)
                    progress?.Report(10 + (int)(80.0 * downloaded / totalBytes));
            }

            fileStream.Close();
            progress?.Report(90);

            // Write updater script that waits for app to exit, then copies files
            var scriptPath = Path.Combine(Path.GetTempPath(), "MuslimON_Update.bat");
            var exeName = Path.GetFileName(Environment.ProcessPath ?? "PrayerShutdown.UI.exe");
            var script = $"""
                @echo off
                echo Updating Muslim ON...
                timeout /t 2 /nobreak >nul
                powershell -NoProfile -Command "Expand-Archive -Path '{zipPath}' -DestinationPath '{tempDir}' -Force"
                xcopy /s /y /q "{tempDir}\*" "{appDir}"
                rmdir /s /q "{tempDir}"
                del "{zipPath}"
                start "" "{Path.Combine(appDir, exeName)}"
                del "%~f0"
                """;

            await File.WriteAllTextAsync(scriptPath, script);
            progress?.Report(95);

            // Launch updater script and exit
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            progress?.Report(100);
            _logger.LogInformation("Update downloaded, restarting...");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed");
            return false;
        }
    }

    private static bool IsNewer(string latest, string current)
    {
        if (Version.TryParse(latest, out var vLatest) && Version.TryParse(current, out var vCurrent))
            return vLatest > vCurrent;
        return string.Compare(latest, current, StringComparison.Ordinal) > 0;
    }
}

// GitHub API models
public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string? TagName { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("assets")] public List<GitHubAsset>? Assets { get; set; }
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    [JsonPropertyName("size")] public long Size { get; set; }
}
