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

    public static string AppDataPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName.Replace(" ", ""));

    public static string DatabasePath =>
        Path.Combine(AppDataPath, DatabaseFileName);

    public static string LogPath =>
        Path.Combine(AppDataPath, LogFolderName);
}
