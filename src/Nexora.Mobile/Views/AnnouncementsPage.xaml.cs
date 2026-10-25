using Nexora.Mobile.ViewModels;

namespace Nexora.Mobile.Views;

public partial class AnnouncementsPage : ContentPage
{
    private readonly AnnouncementsViewModel _viewModel;

    public AnnouncementsPage(AnnouncementsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAnnouncementsCommand.ExecuteAsync(null);
    }
}
