using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class ParentDashboardPage : ContentPage
{
    private readonly ParentDashboardViewModel _viewModel;

    public ParentDashboardPage(ParentDashboardViewModel viewModel)
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
