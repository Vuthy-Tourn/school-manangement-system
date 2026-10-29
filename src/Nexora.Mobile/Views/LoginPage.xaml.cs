using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class LoginPage : ContentPage
{
    private bool _hasAnimated;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        Loaded += (s, e) => RunEntranceAnimation();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RunEntranceAnimation();
    }

    private async void RunEntranceAnimation()
    {
        if (_hasAnimated) return;
        _hasAnimated = true;

        await Task.Delay(40);

        // 1. Brand Header floats down smoothly
        _ = BrandHeaderSection.FadeTo(1.0, 420, Easing.CubicOut);
        _ = BrandHeaderSection.TranslateTo(0, 0, 420, Easing.CubicOut);

        await Task.Delay(70);

        // 2. Main Login Card floats up with gentle spring settle
        _ = LoginCardSection.FadeTo(1.0, 480, Easing.CubicOut);
        _ = LoginCardSection.TranslateTo(0, 0, 480, Easing.CubicOut);
        _ = LoginCardSection.ScaleTo(1.0, 480, Easing.CubicOut);

        await Task.Delay(110);

        // 3. Institutional Footer fades in
        await FooterSection.FadeTo(1.0, 380, Easing.CubicOut);
    }
}
