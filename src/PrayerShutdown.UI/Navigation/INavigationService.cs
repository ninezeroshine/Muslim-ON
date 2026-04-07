namespace PrayerShutdown.UI.Navigation;

public interface INavigationService
{
    void NavigateTo(Type pageType);
    void NavigateTo<TPage>() where TPage : class;
    bool CanGoBack { get; }
    void GoBack();
}
