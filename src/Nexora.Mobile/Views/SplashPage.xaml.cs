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
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasStartedAnimation)
        {
            _hasStartedAnimation = true;

            // Failsafe timer: guarantee transition within 2 seconds even if animation is delayed
            Dispatcher.StartTimer(TimeSpan.FromMilliseconds(1800), () =>
            {
                NavigateToNextPage();
                return false;
            });

            _ = StartSplashSequenceAsync();
        }
    }

    private async Task StartSplashSequenceAsync()
    {
        try
        {
            // Initial view state
            GradientCard.TranslationX = 0;
            GradientCard.Opacity = 1;
            RevealLogo.Scale = 0.90;
            RevealLogo.Opacity = 1;

            HeroTitle.Opacity = 0;
            HeroTitle.TranslationX = -14;

            // Wait a brief tick for native layout to measure
            await Task.Delay(100);

            // Shift icon so it begins centered on the screen before the wordmark reveals
            double offset = (HeroLockup.Width - HeroIcon.Width) / 2.0;
            if (offset > 10)
            {
                HeroIcon.TranslationX = offset;
            }

            // 1. Gentle Breathe Contraction Pulse on the centered Crest (150ms)
            await HeroIcon.ScaleTo(0.94, 150, Easing.CubicInOut);

            // 2. Icon glides smoothly left; Title slides out from behind it and fades in (380ms)
            await Task.WhenAll(
                HeroIcon.TranslateTo(0, 0, 380, Easing.CubicOut),
                HeroIcon.ScaleTo(1.0, 380, Easing.CubicOut),
                HeroTitle.FadeTo(1.0, 380, Easing.CubicOut),
                HeroTitle.TranslateTo(0, 0, 380, Easing.CubicOut)
            );

            // 3. Brand Lockup Recognition Pause (250ms)
            await Task.Delay(250);

            // 4. Trigger Celebratory Burst as the card wipes away
            _ = Task.Run(async () =>
            {
                await Task.Delay(60);
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await TriggerRevealCelebrationAsync();
                });
            });

            // 5. Horizontal Card Swipe Wipe to the right (420ms)
            double screenWidth = Width > 100 ? Width : 420;
            await Task.WhenAll(
                GradientCard.TranslateTo(screenWidth * 1.3, 0, 420, Easing.CubicOut),
                GradientCard.FadeTo(0, 380, Easing.CubicIn)
            );

            GradientCard.IsVisible = false;

            // 6. Seamless handoff into main application
            await Task.Delay(150);
            NavigateToNextPage();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Nexora Splash] Sequence exception: {ex.Message}");
            NavigateToNextPage();
        }
    }

    private async Task TriggerRevealCelebrationAsync()
    {
        // Reveal Logo spring bounce
        var logoBounceTask = Task.Run(async () =>
        {
            await RevealLogo.ScaleTo(1.06, 180, Easing.SpringOut);
            await RevealLogo.ScaleTo(1.0, 140, Easing.CubicOut);
        });

        // Staggered micro-particle bursts
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

        await Task.WhenAll(logoBounceTask, Task.WhenAll(particleTasks));
    }

    private void NavigateToNextPage()
    {
        if (_hasNavigated) return;
        _hasNavigated = true;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Page targetPage = _apiService.IsAuthenticated
                ? new AppShell(_apiService)
                : new NavigationPage(new LoginPage(_loginViewModel));

            if (Window != null)
            {
                Window.Page = targetPage;
            }
            else if (Microsoft.Maui.Controls.Application.Current?.Windows.Count > 0)
            {
                Microsoft.Maui.Controls.Application.Current.Windows[0].Page = targetPage;
            }
        });
    }
}
