using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class StudentDashboardPage : ContentPage
{
    private readonly StudentDashboardViewModel _viewModel;

    public StudentDashboardPage(StudentDashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDashboardCommand.ExecuteAsync(null);
    }
}
