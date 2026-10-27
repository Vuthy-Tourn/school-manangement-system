using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class ClassesPage : ContentPage
{
    private readonly ClassesViewModel _viewModel;

    public ClassesPage(ClassesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDataCommand.ExecuteAsync(null);
    }
}
