using System.Diagnostics;
using Nexora.Mobile.Services;
using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class SplashPage : ContentPage
{
    private readonly IApiService _apiService;
    private readonly LoginViewModel _loginViewModel;
    private bool _hasStartedAnimation;
    private bool _hasNavigated;

    // Particle Radial Burst Offsets (X, Y)
    private readonly (double X, double Y)[] _particleOffsets =
    [
        (-44, -44),
        (42, -46),
        (-52, -8),
        (54, -6),
        (-38, 40),
        (42, 38)
    ];

    public SplashPage(IApiService apiService, LoginViewModel loginViewModel)
    {
        InitializeComponent();
        _apiService = apiService;
        _loginViewModel = loginViewModel;
        BindingContext = loginViewModel;
    }

#if IOS
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler?.PlatformView is UIKit.UIView uiView)
        {
            uiView.BackgroundColor = UIKit.UIColor.FromRGB(6, 14, 31);
            if (uiView.Window != null)
            {
                uiView.Window.BackgroundColor = UIKit.UIColor.FromRGB(6, 14, 31);
            }
        }
    }
#endif

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasStartedAnimation)
        {
            _hasStartedAnimation = true;
            _ = StartSplashSequenceAsync();
        }
    }

    private async Task StartSplashSequenceAsync()
    {
        try
        {
            // Initial view states: BrandHeaderSection starts in the exact center of the screen
            double screenH = Height > 200 ? Height : 850;
            double centerOffset = (screenH / 2.0) - 170;
            if (centerOffset < 120) centerOffset = 220;

            BrandHeaderSection.TranslationY = centerOffset;
            BrandHeaderSection.Scale = 0.95;
            BrandHeaderSection.Opacity = 1;

            LoginCardSection.TranslationY = 480;
            LoginCardSection.Opacity = 0;
            LoginCardSection.Scale = 0.96;

            FooterSection.Opacity = 0;

            GradientCard.TranslationX = 0;
            GradientCard.Opacity = 1;

            GradientBackgroundLayer.Opacity = 0;
            AmbientElementsLayer.Opacity = 0;
            BottomPill.Opacity = 0;

            HeroWhiteIcon.Opacity = 1;
            HeroIcon.Opacity = 0;

            HeroTitle.Opacity = 0;
            HeroTitle.TranslationX = -18;

            HeroSubtitle.Opacity = 0;
            HeroSubtitle.TranslationY = 12;

            // Wait a brief tick for native layout to measure
            await Task.Delay(80);

            // Shift icon container so it begins centered on the screen before the wordmark reveals
            double offset = (HeroLockup.Width - HeroIconContainer.Width) / 2.0;
            if (offset < 10)
            {
                offset = (180 + 16) / 2.0;
            }
            HeroIconContainer.TranslationX = offset;
            HeroIconContainer.Scale = 1.15;

            // 1. Native Splash Continuity Hold (180ms)
            await Task.Delay(180);

            // 2. Gentle Center Settle & Breathe (200ms)
            await HeroIconContainer.ScaleTo(1.0, 200, Easing.CubicInOut);

            // 3. Morph & Bloom Reveal (350ms):
            // - Radiant blue gradient expands across the card
            // - Ambient stardust & orbits illuminate
            // - White crest crossfades into colored crest
            // - Crest glides left from center to lockup position
            // - "NEXORA" wordmark and subtitle slide out and fade in
            // - Bottom pill fades in
            await Task.WhenAll(
                GradientBackgroundLayer.FadeTo(1.0, 350, Easing.CubicOut),
                AmbientElementsLayer.FadeTo(1.0, 350, Easing.CubicOut),
                HeroWhiteIcon.FadeTo(0.0, 300, Easing.CubicOut),
                HeroIcon.FadeTo(1.0, 300, Easing.CubicOut),
                HeroIconContainer.TranslateTo(0, 0, 350, Easing.CubicOut),
                HeroTitle.FadeTo(1.0, 350, Easing.CubicOut),
                HeroTitle.TranslateTo(0, 0, 350, Easing.CubicOut),
                HeroSubtitle.FadeTo(1.0, 380, Easing.CubicOut),
                HeroSubtitle.TranslateTo(0, 0, 380, Easing.CubicOut),
                BottomPill.FadeTo(1.0, 380, Easing.CubicOut)
            );

            // 4. Brand Lockup Recognition Pause (250ms)
            await Task.Delay(250);

            // 4. Reveal underlying Login canvas
            RevealLayer.IsVisible = true;
            double screenWidth = Width > 100 ? Width : 420;

            // 5. Horizontal Card Swipe Wipe to the right (420ms)
            _ = Task.WhenAll(
                GradientCard.TranslateTo(screenWidth * 1.3, 0, 420, Easing.CubicOut),
                GradientCard.FadeTo(0, 380, Easing.CubicIn)
            );

            // 6. Celebratory particle sparks burst at the center logo
            _ = TriggerRevealCelebrationAsync();

            // 7. MATCHED MOVE ANIMATION: Center Logo & Pill GLIDE UP to top header position!
            _ = BrandHeaderSection.TranslateTo(0, 0, 520, Easing.CubicOut);
            _ = BrandHeaderSection.ScaleTo(1.0, 520, Easing.CubicOut);

            // 8. STAGGERED ENTRANCE: Login Card rises up into place from below
            await Task.Delay(90);
            _ = Task.WhenAll(
                LoginCardSection.TranslateTo(0, 0, 500, Easing.CubicOut),
                LoginCardSection.FadeTo(1.0, 460, Easing.CubicOut),
                LoginCardSection.ScaleTo(1.0, 500, Easing.CubicOut)
            );

            await Task.Delay(110);
            await FooterSection.FadeTo(1.0, 380, Easing.CubicOut);

            GradientCard.IsVisible = false;
            GradientCard.InputTransparent = true;

            // 9. If already authenticated, proceed to AppShell
            if (_apiService.IsAuthenticated)
            {
                await Task.Delay(200);
                NavigateToAppShell();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Nexora Splash] Sequence exception: {ex.Message}");
            GradientCard.IsVisible = false;
            RevealLayer.IsVisible = true;
            BrandHeaderSection.TranslationY = 0;
            BrandHeaderSection.Opacity = 1;
            LoginCardSection.TranslationY = 0;
            LoginCardSection.Opacity = 1;
            FooterSection.Opacity = 1;
        }
    }

    private async Task TriggerRevealCelebrationAsync()
    {
        View[] particles = [Particle1, Particle2, Particle3, Particle4, Particle5, Particle6];
        var particleTasks = new List<Task>();

        for (int i = 0; i < particles.Length; i++)
        {
            int index = i;
            var particle = particles[index];
            var (targetX, targetY) = _particleOffsets[index % _particleOffsets.Length];

            particleTasks.Add(Task.Run(async () =>
            {
                await Task.Delay(index * 16);
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    particle.Opacity = 1;
                    particle.Scale = 0.2;
                    particle.TranslationX = 0;
                    particle.TranslationY = 0;

                    await Task.WhenAll(
                        particle.TranslateTo(targetX, targetY, 380, Easing.CubicOut),
                        particle.ScaleTo(1.15, 200, Easing.CubicOut),
                        particle.FadeTo(0, 380, Easing.CubicIn)
                    );
                });
            }));
        }

        await Task.WhenAll(particleTasks);
    }

    private void NavigateToAppShell()
    {
        if (_hasNavigated) return;
        _hasNavigated = true;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var appShell = new AppShell(_apiService);
            if (Window != null)
            {
                Window.Page = appShell;
            }
            else if (Microsoft.Maui.Controls.Application.Current?.Windows.Count > 0)
            {
                Microsoft.Maui.Controls.Application.Current.Windows[0].Page = appShell;
            }
        });
    }
}
