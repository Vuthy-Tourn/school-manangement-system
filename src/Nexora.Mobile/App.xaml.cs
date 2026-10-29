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
        // Boot directly into the motion-driven Tinder-style SplashPage
        return new Window(new SplashPage(_apiService, _loginViewModel));
    }
}