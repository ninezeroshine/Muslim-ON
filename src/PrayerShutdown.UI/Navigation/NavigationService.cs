using Microsoft.UI.Xaml.Controls;

namespace PrayerShutdown.UI.Navigation;

public sealed class NavigationService : INavigationService
{
    private Frame? _frame;

    public Frame? Frame
    {
        get => _frame;
        set => _frame = value;
    }

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public void NavigateTo(Type pageType)
    {
        _frame?.Navigate(pageType);
    }

    public void NavigateTo<TPage>() where TPage : class
    {
        _frame?.Navigate(typeof(TPage));
    }

    public void GoBack()
    {
        if (_frame?.CanGoBack == true)
            _frame.GoBack();
    }
}
