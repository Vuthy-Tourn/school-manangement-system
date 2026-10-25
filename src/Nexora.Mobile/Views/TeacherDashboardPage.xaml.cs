using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class TeacherDashboardPage : ContentPage
{
    private readonly TeacherDashboardViewModel _viewModel;

    public TeacherDashboardPage(TeacherDashboardViewModel viewModel)
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
