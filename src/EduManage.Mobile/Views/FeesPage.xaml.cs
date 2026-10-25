using EduManage.Mobile.ViewModels;

namespace EduManage.Mobile.Views;

public partial class FeesPage : ContentPage
{
    private readonly FeesViewModel _viewModel;

    public FeesPage(FeesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadInvoicesCommand.ExecuteAsync(null);
    }
}
