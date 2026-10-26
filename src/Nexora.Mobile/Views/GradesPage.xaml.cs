using Nexora.Application.DTOs;
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
}
