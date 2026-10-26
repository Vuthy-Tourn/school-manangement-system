using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class ParentDashboardPage : ContentPage
{
    private readonly ParentDashboardViewModel _viewModel;

    public ParentDashboardPage(ParentDashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ParentDashboardViewModel.IsPermissionModalVisible))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    bool isModal = _viewModel.IsPermissionModalVisible;
                    Shell.SetNavBarIsVisible(this, !isModal);
                    Shell.SetTabBarIsVisible(this, !isModal);
                });
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDashboardCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Shell.SetNavBarIsVisible(this, true);
        Shell.SetTabBarIsVisible(this, true);
    }
}
