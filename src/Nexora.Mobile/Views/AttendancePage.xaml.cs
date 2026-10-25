using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class AttendancePage : ContentPage
{
    private readonly AttendanceViewModel _viewModel;

    public AttendancePage(AttendanceViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    private async void OnClassSelectionChanged(object sender, EventArgs e)
    {
        await _viewModel.LoadAttendanceCommand.ExecuteAsync(null);
    }

    private async void OnDateSelected(object sender, DateChangedEventArgs e)
    {
        await _viewModel.LoadAttendanceCommand.ExecuteAsync(null);
    }
}
