namespace PrayerShutdown.Core.Interfaces;

public interface IShutdownService
{
    void ExecuteShutdown();
    void ExecuteHibernate();
    void ExecuteSleep();
    void CancelPendingShutdown();
    bool HasPendingShutdown { get; }
}
