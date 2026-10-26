using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class AttendancePage : ContentPage
{
    private readonly AttendanceViewModel _viewModel;

    public AttendancePage(AttendanceViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AttendanceViewModel.IsPermissionModalVisible))
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
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Shell.SetNavBarIsVisible(this, true);
        Shell.SetTabBarIsVisible(this, true);
    }
}
