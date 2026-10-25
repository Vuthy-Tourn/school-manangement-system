using Nexora.Mobile.Services;
using Nexora.Mobile.ViewModels;
using Nexora.Mobile.Views;

namespace Nexora.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IApiService _apiService;
    private readonly LoginViewModel _loginViewModel;

    public App(IApiService apiService, LoginViewModel loginViewModel)
    {
        InitializeComponent();
        _apiService = apiService;
        _loginViewModel = loginViewModel;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Page initialPage = _apiService.IsAuthenticated
            ? new AppShell(_apiService)
            : new NavigationPage(new LoginPage(_loginViewModel));

        return new Window(initialPage);
    }
}