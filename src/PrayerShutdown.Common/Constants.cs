namespace PrayerShutdown.Common;

public static class Constants
{
    public const string AppName = "Muslim ON";
    public const string DatabaseFileName = "muslimOn.db";
    public const string LogFolderName = "logs";

    public const int DefaultReminderMinutes = 15;
    public const int DefaultShutdownDelayMinutes = 15;
    public const int MaxSnoozeCount = 3;
    public const int SnoozeMinutes = 5;
    public const int NudgeIntervalMinutes = 5;
    public const int ShutdownCountdownSeconds = 60;

    /// <summary>
    /// Extra grace added to the shutdown safety-net timer in PrayerScheduler.
    /// If the overlay never confirms shutdown within
    /// (<see cref="ShutdownCountdownSeconds"/> + this) seconds, the scheduler
    /// fires the shutdown itself as a fallback.
    /// </summary>
    public const int ShutdownSafetyBufferSeconds = 10;

    public static string AppDataPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName.Replace(" ", ""));

    public static string DatabasePath =>
        Path.Combine(AppDataPath, DatabaseFileName);

    public static string LogPath =>
        Path.Combine(AppDataPath, LogFolderName);
}
