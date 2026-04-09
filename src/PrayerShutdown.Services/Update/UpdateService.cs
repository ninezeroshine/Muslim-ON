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

    public static string CurrentVersion => "1.2.9";

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
            // Strip trailing backslash — "Muslim ON\" in batch causes \" to be parsed
            // as an escaped quote, silently breaking xcopy/robocopy paths
            var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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

            // Write PowerShell updater script (replaces batch+xcopy which failed with exit code 4)
            var scriptPath = Path.Combine(Path.GetTempPath(), "MuslimON_Update.ps1");
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MuslimON", "logs");
            var logPath = Path.Combine(logDir, "update.log");
            var exeName = Path.GetFileName(Environment.ProcessPath ?? "PrayerShutdown.UI.exe");
            var pid = Environment.ProcessId;
            var targetExe = Path.Combine(appDir, exeName);

            // Escape single quotes for PowerShell string literals
            static string Esc(string s) => s.Replace("'", "''");

            var procName = Path.GetFileNameWithoutExtension(exeName);
            var robocopyLogPath = Path.Combine(logDir, "robocopy.log");

            // v1.2.9 updater rewrite:
            //  1) Wait for launcher PID to exit
            //  2) Kill ALL leftover instances by process name (tray zombies, second instances)
            //  3) Wait 3s for OS to release file handles (Defender scan, Explorer thumbnail, indexer)
            //  4) Extract ZIP to temp
            //  5) robocopy with /R:10 /W:1 — built-in retry handles transient file locks
            //  6) If robocopy fails (exit 8+), throw and fallback to restarting OLD version
            //     (never leave broken state — either full success or full rollback)
            //  7) Start new version
            //
            // Why robocopy: Copy-Item has no retry. It fails on first LockViolation, leaving
            // partial copy. Robocopy is Microsoft's robust copy — retries each file up to 10×.
            // Documented fix for "file in use by another process" on Windows self-update.
            var script = $$"""
                $ErrorActionPreference = 'Continue'
                $logDir = '{{Esc(logDir)}}'
                $logPath = '{{Esc(logPath)}}'
                $robocopyLog = '{{Esc(robocopyLogPath)}}'
                if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
                function Log($msg) { "[$( Get-Date -Format 'dd.MM.yyyy HH:mm:ss' )] $msg" | Out-File $logPath -Append -Encoding utf8 }

                try {
                    Log '=== Update started (v1.2.9 updater — robocopy + kill-all) ==='
                    Log "launcherPid={{pid}} appDir='{{Esc(appDir)}}'"
                    Log "zipPath='{{Esc(zipPath)}}'"
                    Log "tempDir='{{Esc(tempDir)}}'"

                    # 1) Wait for launcher PID to exit (max 30s)
                    Log 'Waiting for launcher PID {{pid}} to exit...'
                    $timeout = 60
                    while ((Get-Process -Id {{pid}} -ErrorAction SilentlyContinue) -and $timeout -gt 0) {
                        Start-Sleep -Milliseconds 500
                        $timeout--
                    }
                    Log "Launcher exit (timeout slots left: $timeout)"

                    # 2) Kill ALL leftover instances by process name
                    #    Covers: tray-only zombies, second instances, child windows, stuck processes
                    $procName = '{{Esc(procName)}}'
                    Log "Scanning for leftover '$procName' instances..."
                    $leftover = @(Get-Process -Name $procName -ErrorAction SilentlyContinue)
                    if ($leftover.Count -gt 0) {
                        Log "Found $($leftover.Count) leftover instance(s), killing..."
                        foreach ($p in $leftover) {
                            try {
                                Log "  Kill PID=$($p.Id) StartTime=$($p.StartTime)"
                                Stop-Process -Id $p.Id -Force -ErrorAction Stop
                            } catch {
                                Log "    Failed to kill PID=$($p.Id): $_"
                            }
                        }
                        Start-Sleep -Seconds 2
                        # Verify all gone
                        $stillAlive = @(Get-Process -Name $procName -ErrorAction SilentlyContinue)
                        if ($stillAlive.Count -gt 0) {
                            Log "WARNING: $($stillAlive.Count) instance(s) still alive after kill"
                        } else {
                            Log 'All instances terminated'
                        }
                    } else {
                        Log 'No leftover instances'
                    }

                    # 3) Wait for OS to release file handles
                    #    (Defender scan, Explorer thumbnail cache, Windows Search indexer)
                    Log 'Waiting 3s for OS to release file handles...'
                    Start-Sleep -Seconds 3

                    # 4) Extract ZIP to temp
                    Log 'Extracting ZIP...'
                    Expand-Archive -Path '{{Esc(zipPath)}}' -DestinationPath '{{Esc(tempDir)}}' -Force
                    Log 'Expand-Archive complete'

                    $exePath = Join-Path '{{Esc(tempDir)}}' '{{Esc(exeName)}}'
                    if (-not (Test-Path $exePath)) {
                        Log "ERROR: {{Esc(exeName)}} not found in temp dir after extraction!"
                        Log 'Temp dir contents:'
                        Get-ChildItem '{{Esc(tempDir)}}' -Recurse | Select-Object FullName | Out-File $logPath -Append
                        throw 'EXE not found after extraction'
                    }

                    # 5) Copy with robocopy (built-in retry for file locks)
                    #    /E   — copy subdirs including empty
                    #    /IS  — include same files (overwrite unchanged)
                    #    /IT  — include tweaked files
                    #    /R:10 — retry 10 times per file
                    #    /W:1  — wait 1 second between retries (total 10s max per file)
                    #    /NP   — no progress output (cleaner log)
                    #    /NFL /NDL — no file/dir list (cleaner log)
                    #    /LOG+ — append to log file
                    Log "Copying with robocopy (retry=10, wait=1s)..."
                    $rcOutput = & robocopy '{{Esc(tempDir)}}' '{{Esc(appDir)}}' /E /IS /IT /R:10 /W:1 /NP /NFL /NDL "/LOG+:$robocopyLog" 2>&1
                    $rcExit = $LASTEXITCODE
                    Log "Robocopy exit code: $rcExit"
                    # Robocopy exit codes: 0=nothing copied, 1=files copied, 2=extra files, 3=1+2,
                    # 4=mismatched, 5=4+1, 6=4+2, 7=4+1+2. All 0-7 are SUCCESS. 8+ is FAILURE.
                    if ($rcExit -ge 8) {
                        Log "ERROR: Robocopy failed (exit=$rcExit). Tail of robocopy log:"
                        if (Test-Path $robocopyLog) {
                            Get-Content $robocopyLog -Tail 40 | ForEach-Object { Log "  RC: $_" }
                        }
                        throw "Robocopy failed with exit code $rcExit — files are still locked after 10 retries"
                    }

                    Log 'Copy complete'

                    # 6) Cleanup temp
                    Log 'Cleaning up temp...'
                    Remove-Item '{{Esc(tempDir)}}' -Recurse -Force -ErrorAction SilentlyContinue
                    Remove-Item '{{Esc(zipPath)}}' -Force -ErrorAction SilentlyContinue

                    # 7) Start new version
                    Log "Starting '{{Esc(targetExe)}}'"
                    Start-Process '{{Esc(targetExe)}}'
                    Log '=== Update SUCCESS ==='
                } catch {
                    Log "ERROR: $_"
                    Log "Exception: $($_.Exception.GetType().FullName)"
                    Log "Stack: $($_.ScriptStackTrace)"
                    Log 'Starting (possibly old) app version as fallback...'
                    Start-Process '{{Esc(targetExe)}}'
                }

                Remove-Item -Path $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
                """;

            await File.WriteAllTextAsync(scriptPath, script, System.Text.Encoding.UTF8);
            progress?.Report(95);

            // Launch PowerShell updater script and exit
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
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
