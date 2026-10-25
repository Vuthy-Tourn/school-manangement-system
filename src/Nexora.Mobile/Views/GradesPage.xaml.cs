using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class GradesPage : ContentPage
{
    private readonly GradesViewModel _viewModel;

    public GradesPage(GradesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    private async void OnFilterChanged(object sender, EventArgs e)
    {
        await _viewModel.LoadComponentsCommand.ExecuteAsync(null);
    }

    private async void OnComponentChanged(object sender, EventArgs e)
    {
        await _viewModel.LoadGradesCommand.ExecuteAsync(null);
    }
}
