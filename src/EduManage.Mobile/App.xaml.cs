using EduManage.Mobile.Services;
using EduManage.Mobile.ViewModels;
using EduManage.Mobile.Views;

namespace EduManage.Mobile;

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
        // If already authenticated, show AppShell; otherwise show LoginPage
        Page initialPage = _apiService.IsAuthenticated
            ? new AppShell(_apiService)
            : new NavigationPage(new LoginPage(_loginViewModel));

        return new Window(initialPage);
    }
}